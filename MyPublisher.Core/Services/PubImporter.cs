using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MyPublisher.Models;

namespace MyPublisher.Services;

/// <summary>Ce qu'un import a rendu, et ce qu'il a laissé en route.</summary>
public sealed record ImportReport(Document Document, IReadOnlyList<string> Warnings);

/// <summary>
/// Traduit le relevé d'appels de dessin d'un <c>.pub</c> en document MyPublisher.
///
/// Cette classe est PURE : elle prend le texte du relevé et rend un document. Rien
/// n'y touche au disque ni à un processus — c'est <see cref="PubReader"/> qui va
/// chercher le relevé. La séparation n'est pas cosmétique : elle permet d'éprouver
/// toute la traduction sur des relevés écrits à la main, sans installer libmspub, et
/// c'est ce qui rend ce poste testable en quelques millisecondes.
///
/// Les longueurs du relevé sont en pouces ; le modèle est en points typographiques.
/// La conversion se fait à un seul endroit, <see cref="Points"/>.
///
/// Les images, elles, ne tiennent pas dans un document : le relevé les donne en base64
/// et le modèle les référence par chemin. La traduction reste pure en demandant ce
/// chemin à un <see cref="IDepotImages"/> qu'on lui prête ; sans dépôt, elle dit qu'elle
/// a perdu l'image plutôt que de l'écrire quelque part de son propre chef.
///
/// Ce que la traduction NE rend PAS, faute que le modèle sache l'exprimer, et qui
/// remonte en avertissement plutôt que de disparaître en silence : les remplissages
/// par image répétés en mosaïque, et les chemins de plus de deux points.
/// L'alignement vertical y était jusqu'au poste A7, et le remplissage par image
/// jusqu'à C5c ; le modèle porte désormais les deux.
/// </summary>
public static class PubImporter
{
    private const double PointsParPouce = 72;

    public static ImportReport Importer(
        string releve, string? titre = null, IDepotImages? depot = null)
    {
        var etat = new Etat(titre, depot);

        foreach (var ligne in releve.Split('\n'))
        {
            var nu = ligne.Trim();
            if (nu.Length == 0) continue;

            var parenthese = nu.IndexOf('(');
            var appel = (parenthese < 0 ? nu : nu[..parenthese]).Trim();
            var brut = parenthese < 0 ? string.Empty : Interieur(nu, parenthese);

            etat.Traiter(appel, brut);
        }

        etat.Terminer();
        return new ImportReport(etat.Document, etat.Avertissements);
    }

    /// <summary>Le contenu entre la première parenthèse ouvrante et la dernière fermante.</summary>
    private static string Interieur(string ligne, int ouverture)
    {
        var fermeture = ligne.LastIndexOf(')');
        return fermeture > ouverture ? ligne[(ouverture + 1)..fermeture] : string.Empty;
    }

    private static double Points(string? valeur)
    {
        if (valeur is null) return 0;
        var nombre = valeur.EndsWith("in", StringComparison.Ordinal) ? valeur[..^2] : valeur;
        return double.TryParse(nombre, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v * PointsParPouce
            : 0;
    }

    /// <summary>
    /// Découpe « clé: valeur, clé: valeur » en tenant compte des parenthèses : les
    /// points d'un polygone et les colonnes d'un tableau sont eux-mêmes des listes
    /// entre parenthèses, et contiennent des virgules.
    /// </summary>
    private static Dictionary<string, string> Arguments(string brut)
    {
        var resultat = new Dictionary<string, string>(StringComparer.Ordinal);
        var profondeur = 0;
        var debut = 0;

        for (var i = 0; i <= brut.Length; i++)
        {
            if (i < brut.Length)
            {
                if (brut[i] == '(') { profondeur++; continue; }
                if (brut[i] == ')') { profondeur--; continue; }
                if (profondeur > 0 || brut[i] != ',') continue;
            }

            var morceau = brut[debut..i].Trim();
            debut = i + 1;
            if (morceau.Length == 0) continue;

            // La clé porte elle-même un deux-points — « svg:width », « fo:margin-left ».
            // C'est donc le deux-points SUIVI D'UNE ESPACE qui sépare la clé de sa
            // valeur, et non le premier venu. Couper au premier rendait « svg » pour
            // clé et « width: 8.2677in » pour valeur : tous les arguments étaient
            // silencieusement perdus, et les blocs importés naissaient à l'origine avec
            // une taille nulle.
            var separateur = morceau.IndexOf(": ", StringComparison.Ordinal);
            if (separateur <= 0) continue;
            resultat[morceau[..separateur].Trim()] = morceau[(separateur + 2)..].Trim();
        }

        return resultat;
    }

    /// <summary>Les couples (x, y) d'une liste de points, en points typographiques.</summary>
    private static List<(double X, double Y)> Sommets(string brut)
    {
        var points = new List<(double, double)>();
        double? x = null;

        foreach (var champ in brut.Split(','))
        {
            var nu = champ.Trim().TrimStart('(').TrimEnd(')').Trim();
            if (nu.StartsWith("svg:x:", StringComparison.Ordinal)) x = Points(nu[6..].Trim());
            else if (nu.StartsWith("svg:y:", StringComparison.Ordinal) && x is { } abscisse)
            {
                points.Add((abscisse, Points(nu[6..].Trim())));
                x = null;
            }
        }

        return points;
    }

    /// <summary>
    /// Un angle du relevé, en degrés.
    ///
    /// Le relevé l'écrit « librevenge:rotate: 90.0000in » — des degrés affublés d'une
    /// unité de longueur, parce que le générateur brut n'a qu'un seul format pour les
    /// nombres. Le passer à <see cref="Points"/> ferait 6 480.
    /// </summary>
    private static double Angle(string? valeur)
    {
        if (valeur is null) return 0;
        var nombre = valeur.EndsWith("in", StringComparison.Ordinal) ? valeur[..^2] : valeur;
        return double.TryParse(nombre, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;
    }

    private static string? Couleur(string? valeur) =>
        valeur is { Length: 7 } && valeur[0] == '#' ? "#FF" + valeur[1..].ToUpperInvariant() : null;

    // ------------------------------------------------------------------ l'état

    private sealed class Etat(string? titre, IDepotImages? depot)
    {
        public Document Document { get; } = new()
        {
            Title = titre ?? "Document importé",
            Pages = [],
        };

        public List<string> Avertissements { get; } = [];

        private Page? _page;

        /// <summary>Le dernier « setStyle » : il habille le prochain objet dessiné.</summary>
        private Dictionary<string, string> _habillage = [];

        /// <summary>
        /// L'image du dernier « setStyle », déposée une seule fois. Le même habillage peut
        /// servir à plusieurs objets ; décoder 140 000 caractères de base64 à chaque fois
        /// serait inutile, et compter deux images là où il n'y en a qu'une le serait aussi.
        /// </summary>
        private string? _imageDeposee;
        private bool _imageTentee;

        private TextFrame? _bloc;
        private TableFrame? _tableau;
        private TableCell? _cellule;
        private int _ligneTableau = -1;

        private readonly StringBuilder _texte = new();
        private readonly List<StyleSpan> _fragments = [];
        private readonly List<ParagraphSpan> _paragraphes = [];
        private int _paragraphe;
        private int _debutFragment;
        private Dictionary<string, string>? _styleCourant;
        private int _tabulations;

        public void Traiter(string appel, string brut)
        {
            switch (appel)
            {
                case "startPage": DebutPage(Arguments(brut)); break;
                case "endPage": _page = null; break;

                case "setStyle":
                    _habillage = Arguments(brut);
                    _imageDeposee = null;
                    _imageTentee = false;
                    break;

                case "startTextObject": DebutTexte(Arguments(brut)); break;
                case "endTextObject": FinTexte(); break;

                case "openParagraph": DebutParagraphe(Arguments(brut)); break;
                case "closeParagraph": _paragraphe++; break;

                case "openSpan": _styleCourant = Arguments(brut); _debutFragment = _texte.Length; break;
                case "closeSpan": FinFragment(); break;

                // Le retour chariot est écarté, et c'est décisif. libmspub termine
                // CHAQUE paragraphe par un « insertText (\r) » : c'est la marque de
                // paragraphe de Publisher, rendue telle quelle. Or la structure des
                // paragraphes nous vient déjà de « openParagraph », qui pose son propre
                // saut de ligne — le \r fait donc doublon. À l'intérieur d'un bloc les
                // deux se collent en « \r\n » que la composition normalise, et le défaut
                // ne se voit pas ; mais celui du DERNIER paragraphe n'a rien derrière lui
                // et devient une ligne vide fantôme.
                //
                // Les vingt-sept blocs des deux documents réels finissent par un \r :
                // quatre des cinq débordements signalés sur ATA 21 étaient donc faux, et
                // quatre blocs portaient un marqueur rouge alors que rien n'y était coupé.
                case "insertText": _texte.Append(brut.Replace("\r", "")); break;
                case "insertTab": _texte.Append('\t'); _tabulations++; break;
                case "insertLineBreak": _texte.Append('\n'); break;

                case "drawPolygon": Polygone(Arguments(brut)); break;
                case "drawPath": Chemin(Arguments(brut)); break;

                case "startTableObject": DebutTableau(Arguments(brut)); break;
                case "openTableRow": DebutLigne(Arguments(brut)); break;
                case "openTableCell": DebutCellule(Arguments(brut)); break;
                case "closeTableCell": FinCellule(); break;
                case "endTableObject": FinTableau(); break;
            }
        }

        public void Terminer()
        {
            if (Document.Pages.Count == 0) Document.Pages.Add(new Page { Name = "Page 1" });
            if (_tabulations > 0)
            {
                Avertissements.Add(
                    $"{_tabulations} tabulation(s) rendues aux taquets par défaut " +
                    "(un demi-pouce) : le document d'origine pouvait en poser d'autres.");
            }
        }

        // -------------------------------------------------------------- la page

        private void DebutPage(Dictionary<string, string> args)
        {
            // Le format vient de la première page ; le modèle n'en porte qu'un.
            if (Document.Pages.Count == 0)
            {
                Document.PageWidth = Points(args.GetValueOrDefault("svg:width"));
                Document.PageHeight = Points(args.GetValueOrDefault("svg:height"));
                // Publisher place ses objets en absolu ; les marges du document ne se
                // lisent pas dans le relevé. On les met à zéro plutôt que d'inventer
                // des repères qui ne correspondraient à rien.
                Document.MarginLeft = Document.MarginRight = 0;
                Document.MarginTop = Document.MarginBottom = 0;
            }

            _page = new Page { Name = $"Page {Document.Pages.Count + 1}" };
            Document.Pages.Add(_page);
        }

        private Page Page()
        {
            if (_page is not null) return _page;
            _page = new Page { Name = $"Page {Document.Pages.Count + 1}" };
            Document.Pages.Add(_page);
            return _page;
        }

        // -------------------------------------------------------------- le texte

        private void DebutTexte(Dictionary<string, string> args)
        {
            var largeur = Points(args.GetValueOrDefault("svg:width"));
            var gouttiere = Points(args.GetValueOrDefault("fo:column-gap"));
            var largeurColonne = Points(args.GetValueOrDefault("style:column-width"));

            _bloc = new TextFrame
            {
                Name = "Bloc de texte",
                X = Points(args.GetValueOrDefault("svg:x")),
                Y = Points(args.GetValueOrDefault("svg:y")),
                Width = largeur,
                Height = Points(args.GetValueOrDefault("svg:height")),
                // Deux blocs d'ATA 21 sont tournés d'un quart de tour ; l'import les
                // posait droits, sans un mot.
                Rotation = Angle(args.GetValueOrDefault("librevenge:rotate")),
                PaddingLeft = Points(args.GetValueOrDefault("fo:padding-left")),
                PaddingTop = Points(args.GetValueOrDefault("fo:padding-top")),
                PaddingRight = Points(args.GetValueOrDefault("fo:padding-right")),
                PaddingBottom = Points(args.GetValueOrDefault("fo:padding-bottom")),
                ColumnGap = gouttiere > 0 ? gouttiere : 5.67,
                Columns = Colonnes(largeur, largeurColonne, gouttiere),
                // L'interligne du relevé est absent quand Publisher s'en tient à celui
                // de la police, ce qui est le cas des documents à reprendre : « 1 » est
                // donc la valeur juste, et non un défaut de repli.
                LineHeight = 1,
                VerticalAlign = Calage(args.GetValueOrDefault("draw:textarea-vertical-align")),
            };

            Habiller(_bloc);

            _texte.Clear();
            _fragments.Clear();
            _paragraphes.Clear();
            _paragraphe = 0;
        }

        /// <summary>
        /// Le calage vertical du texte dans sa zone (<c>draw:textarea-vertical-align</c>).
        ///
        /// L'absence de la clé vaut « en haut », qui est le défaut de Publisher. Des
        /// quatre valeurs que le format nomme, libmspub 0.1.4 n'écrit que « middle » :
        /// la chaîne est seule dans la bibliothèque, sans « top » ni « bottom » à côté
        /// d'elle. Les deux autres sont reconnues quand même — elles ne coûtent rien et
        /// une version ultérieure du lecteur pourrait les rendre.
        /// </summary>
        private static VerticalAlign Calage(string? valeur) => valeur switch
        {
            "middle" => VerticalAlign.Middle,
            "bottom" => VerticalAlign.Bottom,
            _ => VerticalAlign.Top,
        };

        /// <summary>
        /// Combien de colonnes tiennent dans le bloc. Le relevé donne la largeur d'une
        /// colonne et la gouttière ; le compte s'en déduit plutôt que de se lire.
        /// </summary>
        private static int Colonnes(double largeur, double largeurColonne, double gouttiere)
        {
            if (largeurColonne <= 0 || largeur <= 0) return 1;
            var pas = largeurColonne + gouttiere;
            if (pas <= 0) return 1;
            var compte = (int)Math.Round((largeur + gouttiere) / pas);
            return Math.Clamp(compte, 1, 40);
        }

        private void DebutParagraphe(Dictionary<string, string> args)
        {
            if (_texte.Length > 0) _texte.Append('\n');

            var gauche = Points(args.GetValueOrDefault("fo:margin-left"));
            var droite = Points(args.GetValueOrDefault("fo:margin-right"));
            var premiere = Points(args.GetValueOrDefault("fo:text-indent"));
            var avant = Points(args.GetValueOrDefault("fo:margin-top"));
            var apres = Points(args.GetValueOrDefault("fo:margin-bottom"));
            var fer = args.GetValueOrDefault("fo:text-align") switch
            {
                "center" => TextAlign.Center,
                "right" => TextAlign.Right,
                "justify" => TextAlign.Justify,
                _ => TextAlign.Left,
            };

            if (gauche != 0 || droite != 0 || premiere != 0 || avant != 0 || apres != 0 ||
                fer != TextAlign.Left)
            {
                _paragraphes.Add(new ParagraphSpan
                {
                    Start = _paragraphe,
                    Length = 1,
                    IndentLeft = gauche,
                    IndentRight = droite,
                    IndentFirstLine = premiere,
                    SpaceBefore = avant,
                    SpaceAfter = apres,
                    Align = fer,
                });
            }
        }

        private void FinFragment()
        {
            if (_styleCourant is not { } style) return;
            var longueur = _texte.Length - _debutFragment;
            _styleCourant = null;
            if (longueur <= 0) return;

            var position = style.GetValueOrDefault("style:text-position");

            _fragments.Add(new StyleSpan
            {
                Start = _debutFragment,
                Length = longueur,
                // Un .pub ne connaît pas le jeu de MyPublisher : ses polices restent libres,
                // même celle dont le nom aurait la forme d'une référence (poste C5b). Ses
                // couleurs le sont par construction, « Couleur » ne rendant que de l'hexa.
                FontFamily = ReferenceDuJeu.Liberer(style.GetValueOrDefault("style:font-name")),
                FontSize = style.TryGetValue("fo:font-size", out var corps) ? Points(corps) : null,
                Bold = style.GetValueOrDefault("fo:font-weight") == "bold",
                Italic = style.GetValueOrDefault("fo:font-style") == "italic",
                Underline = LireSouligne(style),
                Foreground = Couleur(style.GetValueOrDefault("fo:color")),
                BaselineShift = Decalage(position),
            });
        }

        /// <summary>
        /// Le souligné d'un fragment, dans les quatre réglages d'OpenDocument où libmspub
        /// range les dix-sept sortes de Publisher (<c>fillUnderline</c>,
        /// <c>MSPUBCollector.cpp:254-330</c>) : le dessin du trait, simple ou double,
        /// l'épaisseur, et « mots seuls » (poste I7).
        ///
        /// libmspub écrit toujours les quatre ensemble ; un relevé qui n'en donnerait
        /// qu'une partie — le type sans le dessin, d'un autre générateur — est lu au plus
        /// près : un type sans dessin est un trait plein. Un fragment qui n'en dit rien
        /// n'est pas souligné, et le dit : comme le gras, l'absence est une valeur, sinon
        /// le fragment prendrait le souligné du bloc, qui est celui des autres. Une
        /// épaisseur autre que « bold » — les longueurs et les pourcentages
        /// d'OpenDocument, que libmspub n'écrit pas — est celle de la police.
        /// </summary>
        private static Souligne LireSouligne(Dictionary<string, string> style)
        {
            var type = style.GetValueOrDefault("style:text-underline-type");
            var dessin = style.GetValueOrDefault("style:text-underline-style");
            if (type is null && dessin is null) return Souligne.Aucun;
            if (type == "none" || dessin == "none") return Souligne.Aucun;

            var trait = dessin switch
            {
                "dotted" => TraitDeSouligne.Dotted,
                "dash" => TraitDeSouligne.Dash,
                "long-dash" => TraitDeSouligne.LongDash,
                "dot-dash" => TraitDeSouligne.DotDash,
                "dot-dot-dash" => TraitDeSouligne.DotDotDash,
                "wave" => TraitDeSouligne.Wave,
                _ => TraitDeSouligne.Solid,
            };

            return new Souligne(
                trait,
                @double: type == "double",
                thick: style.GetValueOrDefault("style:text-underline-width") is "bold" or "thick",
                wordsOnly: style.GetValueOrDefault("style:text-underline-mode") == "skip-white-space");
        }

        /// <summary>
        /// « style:text-position: -50% 67% » : le premier nombre est le décalage de la
        /// ligne de base en pourcentage du corps, le second la taille relative — que le
        /// relevé a déjà répercutée dans fo:font-size, et qu'on ignore donc ici.
        /// </summary>
        private static double Decalage(string? position)
        {
            if (position is null) return 0;
            var premier = position.Split(' ')[0].TrimEnd('%');
            return double.TryParse(premier, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v / 100
                : 0;
        }

        private void FinTexte()
        {
            if (_bloc is null) return;

            _bloc.Text = _texte.ToString();
            Degager(_bloc);
            foreach (var fragment in _fragments) _bloc.Spans.Add(fragment);
            foreach (var paragraphe in _paragraphes) _bloc.Paragraphs.Add(paragraphe);

            // L'alignement le plus répandu devient celui du bloc ; les autres restent
            // portés par leur paragraphe. Sans cela un bloc mêlant justifié et fer à
            // gauche perdrait la moitié de sa mise en forme.
            var fers = _paragraphes.GroupBy(p => p.Align ?? TextAlign.Left)
                                   .OrderByDescending(g => g.Count()).ToList();
            if (fers.Count > 0) _bloc.Align = fers[0].Key;

            Nommer(_bloc);

            if (_cellule is not null)
            {
                // Un bloc ouvert dans une case appartient à la case, pas à la page.
                _cellule.Text = _bloc.Text;
                _cellule.Align = _bloc.Align;
            }
            else
            {
                // Le bloc est posé même vide. La source en compte dix-huit sur les
                // fiches, dont un qui ne portait que sa marque de paragraphe : depuis
                // que celle-ci est écartée, son texte est vide, et l'écarter à son tour
                // ferait mentir le compte. Un bloc vide reste un bloc — on peut le
                // sélectionner et y taper, et c'est ainsi qu'un modèle réserve sa place.
                Page().Frames.Add(_bloc);
            }

            _bloc = null;
        }

        /// <summary>
        /// Fait du style le plus répandu celui du bloc, et retire les fragments qui n'en
        /// disent alors plus rien.
        ///
        /// Publisher ne pose aucun style sur la zone de texte elle-même : tout est dans
        /// les fragments. Importer tel quel laissait le bloc à la police par défaut de
        /// MyPublisher — DejaVu Sans 11 — alors que pas un caractère ne s'en servait.
        /// Le document restait juste à l'œil, mais il était faux : une ligne vide
        /// s'ouvrait à la hauteur d'une police absente du texte, et retirer un fragment
        /// dans le panneau faisait apparaître une police qu'on n'avait jamais choisie.
        ///
        /// On compte par caractères et non par fragments : dix mots en gras pèsent moins
        /// qu'un paragraphe ordinaire.
        /// </summary>
        private void Degager(TextFrame bloc)
        {
            if (_fragments.Count == 0) return;

            var dominant = _fragments
                .GroupBy(f => (f.FontFamily, f.FontSize, f.Bold, f.Italic, f.Underline, f.Foreground))
                .OrderByDescending(g => g.Sum(f => f.Length))
                .First().Key;

            if (dominant.FontFamily is { Length: > 0 } famille) bloc.FontFamily = famille;
            if (dominant.FontSize is { } corps and > 0) bloc.FontSize = corps;
            bloc.Bold = dominant.Bold ?? false;
            bloc.Italic = dominant.Italic ?? false;
            bloc.Underline = dominant.Underline ?? Souligne.Aucun;
            if (dominant.Foreground is { Length: > 0 } couleur) bloc.Foreground = couleur;

            // Un fragment qui répète le souligné du bloc le lui laisse (poste I7). Le gras,
            // lui, reste écrit sur chaque fragment gardé ; le souligné non, parce que celui
            // du bloc est ce que la commande « Souligné » change : un fragment qui l'aurait
            // figé — le cas de tous ceux d'un document qui ne souligne rien — garderait son
            // texte nu sous un bloc qu'on vient de souligner.
            foreach (var fragment in _fragments)
            {
                if (fragment.Underline == dominant.Underline) fragment.Underline = null;
            }

            // Un fragment qui répète le style du bloc n'apprend plus rien ; le garder
            // alourdirait le fichier et ferait croire à une exception là où il n'y en a
            // pas. Le décalage de ligne de base, lui, n'est jamais celui du bloc.
            _fragments.RemoveAll(f =>
                f.BaselineShift == 0 && f.Underline is null &&
                (f.FontFamily, f.FontSize, f.Bold, f.Italic, f.Foreground) ==
                (dominant.FontFamily, dominant.FontSize, dominant.Bold, dominant.Italic, dominant.Foreground));
        }

        /// <summary>Un nom lisible dans la liste des blocs : les premiers mots du texte.</summary>
        private static void Nommer(TextFrame bloc)
        {
            var premiere = bloc.Text.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim();
            if (string.IsNullOrEmpty(premiere)) return;
            bloc.Name = premiere.Length <= 40 ? premiere : premiere[..40] + "…";
        }

        // ------------------------------------------------------------- les formes

        /// <summary>
        /// Applique au bloc le dernier « setStyle » rencontré.
        ///
        /// Le fond et le contour sont posés dans les DEUX cas, y compris à néant. Une
        /// forme naît avec un fond bleu et un contour — c'est ce qu'on veut quand on la
        /// trace à la souris, et c'est faux quand on la lit d'un fichier : ne rien
        /// écrire laissait ces défauts en place, et le premier import a rendu un
        /// document entièrement bleu. Ce que la source ne dit pas, elle le dit :
        /// « draw:fill: none » veut dire aucun fond, pas « à toi de voir ».
        /// </summary>
        private void Habiller(Frame forme)
        {
            var remplissage = _habillage.GetValueOrDefault("draw:fill");

            forme.Fill = remplissage switch
            {
                "solid" => Couleur(_habillage.GetValueOrDefault("draw:fill-color")) is { } teinte
                    ? new SolidFill(teinte)
                    : null,
                "bitmap" => Image(),
                _ => null,
            };

            var trait = _habillage.GetValueOrDefault("draw:stroke");
            if (trait is "solid" or "dash")
            {
                forme.Stroke = Couleur(_habillage.GetValueOrDefault("svg:stroke-color"));
                var epaisseur = Points(_habillage.GetValueOrDefault("svg:stroke-width"));
                if (epaisseur > 0) forme.StrokeThickness = epaisseur;
                forme.StrokeStyle = trait == "dash" ? StrokeStyle.Dashed : StrokeStyle.Solid;
            }
            else
            {
                forme.Stroke = null;
            }
        }

        /// <summary>
        /// Le remplissage par image du dernier « setStyle », s'il a pu être déposé.
        ///
        /// C'est la raison d'être de ce poste : les cinq PNG d'ATA 21 ne sont pas des
        /// blocs image, ce sont des remplissages de forme, et le relevé les donne en
        /// clair — <c>draw:fill-image</c>, du base64, jusqu'à 142 000 caractères.
        ///
        /// Trois choses s'y lisent, et les trois comptent :
        ///
        /// <c>style:repeat: stretch</c> étire l'image sur la forme, ce qui est le cadrage
        /// « Remplir ». Une mosaïque — la seule autre valeur que Publisher pose — n'est pas
        /// au modèle : on cadre alors sans déformer, et on le dit.
        ///
        /// <c>librevenge:rotate</c> fait pivoter l'image dans son cadre. Sur les cinq
        /// images d'ATA 21, une seule le porte, et sans elle cette image-là arrive couchée
        /// et écrasée d'un facteur trois : son PNG fait 1062 × 617 dans un cadre de
        /// 3,04 × 5,23 in. Une sur cinq, ce n'est pas une finition.
        ///
        /// <c>librevenge:mime-type</c> nomme le format, dont le dépôt tire l'extension.
        /// </summary>
        private FillBrush? Image()
        {
            if (!_imageTentee)
            {
                _imageTentee = true;
                _imageDeposee = Deposer();
            }

            if (_imageDeposee is null) return null;

            var repetition = _habillage.GetValueOrDefault("style:repeat");
            if (repetition is not null and not "stretch")
            {
                Avertissements.Add(
                    $"Remplissage par image « {repetition} » rendu sans répétition : le " +
                    "modèle ne sait pas mettre une image en mosaïque.");
            }

            return new ImageFill(
                _imageDeposee,
                repetition == "stretch" ? ImageFit.Fill : ImageFit.Uniform,
                Rotation(_habillage.GetValueOrDefault("librevenge:rotate")));
        }

        private string? Deposer()
        {
            var base64 = _habillage.GetValueOrDefault("draw:fill-image");
            if (string.IsNullOrEmpty(base64)) return null;

            if (depot is null)
            {
                Avertissements.Add(
                    "Remplissage par image perdu : aucun dépôt d'images n'a été fourni à " +
                    "l'import.");
                return null;
            }

            byte[] donnees;
            try
            {
                donnees = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                Avertissements.Add("Remplissage par image perdu : l'image du relevé est illisible.");
                return null;
            }

            var chemin = depot.Deposer(donnees, _habillage.GetValueOrDefault("librevenge:mime-type") ?? "");
            if (chemin is null)
            {
                Avertissements.Add("Remplissage par image perdu : l'image n'a pas pu être rangée.");
            }

            return chemin;
        }

        private static double Rotation(string? valeur) =>
            double.TryParse(valeur, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private void Polygone(Dictionary<string, string> args)
        {
            var points = Sommets(args.GetValueOrDefault("svg:points") ?? string.Empty);
            if (points.Count < 3) return;

            var gauche = points.Min(p => p.X);
            var haut = points.Min(p => p.Y);
            var largeur = points.Max(p => p.X) - gauche;
            var hauteur = points.Max(p => p.Y) - haut;

            // Un polygone qui n'est pas un rectangle à angles droits est rendu par son
            // cadre : c'est une perte, elle est dite. Les documents à reprendre n'en
            // portent aucun de cette sorte.
            if (!EstRectangle(points))
            {
                Avertissements.Add(
                    $"Polygone à {points.Count} points rendu par son cadre : le modèle ne " +
                    "porte que rectangles et ellipses.");
            }

            var forme = new ShapeFrame
            {
                Name = "Cadre", Shape = ShapeKind.Rectangle,
                X = gauche, Y = haut, Width = largeur, Height = hauteur,
            };
            Habiller(forme);
            Page().Frames.Add(forme);
        }

        private static bool EstRectangle(List<(double X, double Y)> points)
        {
            // Fermé, cinq points, et chaque côté horizontal ou vertical.
            if (points.Count is not (4 or 5)) return false;
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                if (Math.Abs(a.X - b.X) > 0.01 && Math.Abs(a.Y - b.Y) > 0.01) return false;
            }

            return true;
        }

        private void Chemin(Dictionary<string, string> args)
        {
            var points = Sommets(args.GetValueOrDefault("svg:d") ?? string.Empty);
            if (points.Count < 2) return;

            if (points.Count > 2)
            {
                Avertissements.Add(
                    $"Chemin à {points.Count} points ramené à un trait entre ses extrémités.");
            }

            var gauche = points.Min(p => p.X);
            var haut = points.Min(p => p.Y);

            // Un trait est un rectangle sans épaisseur : c'est son contour qu'on voit,
            // avec ses tirets s'il en a. Le modèle n'a pas encore d'objet trait
            // (poste A10) ; celui-ci s'en approche sans rien inventer.
            var trait = new ShapeFrame
            {
                Name = "Trait", Shape = ShapeKind.Rectangle,
                X = gauche, Y = haut,
                Width = points.Max(p => p.X) - gauche,
                Height = points.Max(p => p.Y) - haut,
            };
            Habiller(trait);
            Page().Frames.Add(trait);
        }

        // ----------------------------------------------------------- les tableaux

        private void DebutTableau(Dictionary<string, string> args)
        {
            _tableau = new TableFrame
            {
                Name = "Bande",
                X = Points(args.GetValueOrDefault("svg:x")),
                Y = Points(args.GetValueOrDefault("svg:y")),
                Width = Points(args.GetValueOrDefault("svg:width")),
                Height = Points(args.GetValueOrDefault("svg:height")),
                Rotation = Angle(args.GetValueOrDefault("librevenge:rotate")),
            };

            var largeurs = Largeurs(args.GetValueOrDefault("librevenge:table-columns"));
            _tableau.ColumnCount = Math.Max(1, largeurs.Count);
            for (var i = 0; i < largeurs.Count && i < _tableau.Columns.Count; i++)
            {
                _tableau.Columns[i].Width = largeurs[i];
            }

            _ligneTableau = -1;
        }

        private static List<double> Largeurs(string? brut)
        {
            var largeurs = new List<double>();
            if (brut is null) return largeurs;

            foreach (var champ in brut.Split(','))
            {
                var nu = champ.Trim().TrimStart('(').TrimEnd(')').Trim();
                if (nu.StartsWith("style:column-width:", StringComparison.Ordinal))
                {
                    largeurs.Add(Points(nu[19..].Trim()));
                }
            }

            return largeurs;
        }

        private void DebutLigne(Dictionary<string, string> args)
        {
            if (_tableau is null) return;
            _ligneTableau++;
            _tableau.RowCount = Math.Max(_tableau.RowCount, _ligneTableau + 1);

            var hauteur = Points(args.GetValueOrDefault("librevenge:row-height"));
            if (hauteur > 0 && _ligneTableau < _tableau.Rows.Count)
            {
                _tableau.Rows[_ligneTableau].Height = hauteur;
            }
        }

        private void DebutCellule(Dictionary<string, string> args)
        {
            if (_tableau is null) return;

            var ligne = Entier(args.GetValueOrDefault("librevenge:row"), _ligneTableau);
            var colonne = Entier(args.GetValueOrDefault("librevenge:column"), 0);
            _cellule = _tableau.Cells.FirstOrDefault(c => c.Row == ligne && c.Column == colonne);

            _texte.Clear();
            _fragments.Clear();
            _paragraphes.Clear();
            _paragraphe = 0;
        }

        private static int Entier(string? valeur, int defaut) =>
            int.TryParse(valeur, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v : defaut;

        private void FinCellule()
        {
            // Une case dont le bloc de texte n'a pas été ouvert n'a rien reçu ; les
            // documents à reprendre en comptent trente-cinq sur cinquante et une.
            if (_cellule is not null && _cellule.Text.Length == 0 && _texte.Length > 0)
            {
                _cellule.Text = _texte.ToString();
            }

            _cellule = null;
        }

        private void FinTableau()
        {
            if (_tableau is null) return;
            Habiller(_tableau);
            Page().Frames.Add(_tableau);
            _tableau = null;
        }
    }
}
