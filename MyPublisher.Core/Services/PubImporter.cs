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
/// Ce que la traduction NE rend PAS, faute que le modèle sache l'exprimer, et qui
/// remonte en avertissement plutôt que de disparaître en silence : les remplissages
/// par image autres que posés en bloc, et les chemins de plus de deux points.
/// L'alignement vertical, lui, y était jusqu'au poste A7 ; le modèle le porte
/// désormais.
/// </summary>
public static class PubImporter
{
    private const double PointsParPouce = 72;

    public static ImportReport Importer(string releve, string? titre = null)
    {
        var etat = new Etat(titre);

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

    private static string? Couleur(string? valeur) =>
        valeur is { Length: 7 } && valeur[0] == '#' ? "#FF" + valeur[1..].ToUpperInvariant() : null;

    // ------------------------------------------------------------------ l'état

    private sealed class Etat(string? titre)
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

                case "setStyle": _habillage = Arguments(brut); break;

                case "startTextObject": DebutTexte(Arguments(brut)); break;
                case "endTextObject": FinTexte(); break;

                case "openParagraph": DebutParagraphe(Arguments(brut)); break;
                case "closeParagraph": _paragraphe++; break;

                case "openSpan": _styleCourant = Arguments(brut); _debutFragment = _texte.Length; break;
                case "closeSpan": FinFragment(); break;

                case "insertText": _texte.Append(brut); break;
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
                FontFamily = style.GetValueOrDefault("style:font-name"),
                FontSize = style.TryGetValue("fo:font-size", out var corps) ? Points(corps) : null,
                Bold = style.GetValueOrDefault("fo:font-weight") == "bold",
                Italic = style.GetValueOrDefault("fo:font-style") == "italic",
                Foreground = Couleur(style.GetValueOrDefault("fo:color")),
                BaselineShift = Decalage(position),
            });
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
            else if (_bloc.Text.Length > 0 || _bloc.Fill is not null || _bloc.Stroke is not null)
            {
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
                .GroupBy(f => (f.FontFamily, f.FontSize, f.Bold, f.Italic, f.Foreground))
                .OrderByDescending(g => g.Sum(f => f.Length))
                .First().Key;

            if (dominant.FontFamily is { Length: > 0 } famille) bloc.FontFamily = famille;
            if (dominant.FontSize is { } corps and > 0) bloc.FontSize = corps;
            bloc.Bold = dominant.Bold ?? false;
            bloc.Italic = dominant.Italic ?? false;
            if (dominant.Foreground is { Length: > 0 } couleur) bloc.Foreground = couleur;

            // Un fragment qui répète le style du bloc n'apprend plus rien ; le garder
            // alourdirait le fichier et ferait croire à une exception là où il n'y en a
            // pas. Le décalage de ligne de base, lui, n'est jamais celui du bloc.
            _fragments.RemoveAll(f =>
                f.BaselineShift == 0 &&
                (f.FontFamily, f.FontSize, f.Bold, f.Italic, f.Foreground) == dominant);
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

            forme.Fill = remplissage == "solid"
                ? Couleur(_habillage.GetValueOrDefault("draw:fill-color"))
                : null;

            if (remplissage == "bitmap")
            {
                Avertissements.Add(
                    "Remplissage par image perdu : le modèle ne porte pas encore les " +
                    "remplissages non unis (poste C5c).");
            }

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
