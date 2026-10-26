using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;
using MyPublisher.Views;

namespace MyPublisher.Banc;

/// <summary>
/// L'écart de la première voie de R7, mesuré sur la vraie fenêtre (poste N7) : pour chaque
/// position du curseur d'un bloc, la distance, en points, entre le point d'insertion que le
/// champ de saisie dessine et celui du caractère composé.
///
/// Côté champ, la mise en page du texte du <c>PART_TextPresenter</c> d'une saisie que le banc
/// ouvre lui-même — <see cref="TextFrame.IsEditing"/> posé, la zone au focus —, sans
/// transaction : la planche ne connaît pas cette saisie-là, et n'ouvre rien quand la zone
/// prend le focus. <c>HitTestTextPosition(i)</c> donne l'abscisse ; la ligne de base est celle
/// de la ligne où il la place, telle que le présentateur la dessine. Côté composition,
/// <see cref="GeometrieDuTexte.PointDe"/>, sur la ligne de base de sa <c>LignePosee</c> (N3).
/// Les deux dans le repère du bloc non tourné, en points ; l'écart vaut
/// <c>max(|Δx|, |Δ ligne de base|)</c>.
///
/// Deux configurations. A, le champ tel que l'application le pose. B, « au mieux de la voie 1 » :
/// le banc pose, sur l'instance du champ et sur elle seule, l'interligne de la composition — la
/// hauteur naturelle résolue multipliée par l'interligne du bloc — et retire l'arrondi de mise
/// en page du champ et de ses parties ; rien de l'application ne change, et l'instance est
/// rendue à ses valeurs après la mesure.
/// </summary>
public static class Saisie
{
    /// <summary>Un pixel au zoom 100 %, en points : 72/96.</summary>
    public const double Seuil = 0.75;

    /// <summary>
    /// Une position mesurée : son indice dans le texte, et les deux écarts, champ − composition.
    /// <paramref name="Motif"/> dit, pour une position au-dessus du seuil, ce qui l'y met (voir
    /// <see cref="Motif"/>) ; rien sous le seuil.
    /// </summary>
    public sealed record Position(int Indice, double Dx, double Dy, string? Motif = null)
    {
        public double Ecart => Math.Max(Math.Abs(Dx), Math.Abs(Dy));
    }

    /// <summary>
    /// Un bloc mesuré : où il est, ce qu'il porte, les lignes de chaque côté, et chaque position
    /// mesurée. <paramref name="Causes"/> nomme ce que le bloc porte et que le champ ne suit pas.
    /// </summary>
    /// <remarks>
    /// <paramref name="Faconnage"/> est l'écart de la composition à son propre dessin : le plus
    /// grand, sur les caractères du bloc, entre le bord que le morceau façonné d'un tenant leur
    /// donne et la chasse de ce qui les précède, mesuré seul, que <see cref="GeometrieDuTexte.PointDe"/>
    /// compte (voir <see cref="Faconnage(Morceau, MetriquesAvalonia)"/>).
    /// </remarks>
    public sealed record Bloc(
        string Nom, int Page, double Rotation, string Texte, IReadOnlyList<string> Causes,
        int LignesComposees, int LignesDuChamp, IReadOnlyList<Position> Positions, double Faconnage)
    {
        public double Maximum => Positions.Count == 0 ? 0 : Positions.Max(p => p.Ecart);

        /// <summary>Le 95ᵉ centile, au rang le plus proche : la plus petite valeur que 95 % des positions ne dépassent pas.</summary>
        public double Centile95 => Centile(Positions.Select(p => p.Ecart), 0.95);

        public int AuDessusDuSeuil => Positions.Count(p => p.Ecart > Seuil);

        /// <summary>La première position dont l'écart dépasse le seuil, ou rien.</summary>
        public int? PremiereQuiDiverge => Positions.FirstOrDefault(p => p.Ecart > Seuil)?.Indice;
    }

    /// <summary>Un bloc que la mesure compte sans le mesurer, et pourquoi.</summary>
    public sealed record Ecarte(string Nom, int Page, string Raison);

    /// <summary>Le relevé d'un document dans une configuration, à un zoom.</summary>
    /// <remarks>
    /// <paramref name="Echelle"/> est ce que vaut un point de la page en pixels de la fenêtre :
    /// 1 au zoom du banc, 8/3 au zoom 2 — de quoi voir que la contre-épreuve a bien changé de zoom.
    /// </remarks>
    public sealed record Releve(
        string Document, string Configuration, double Zoom, double Echelle,
        IReadOnlyList<Bloc> Blocs, IReadOnlyList<Ecarte> Ecartes);

    public static double Centile(IEnumerable<double> valeurs, double rang)
    {
        var triees = valeurs.Order().ToList();
        if (triees.Count == 0) return 0;
        var k = (int)Math.Ceiling(rang * triees.Count) - 1;
        return triees[Math.Clamp(k, 0, triees.Count - 1)];
    }

    /// <summary>
    /// Les positions du curseur d'un texte, de 0 à sa longueur, hors de l'intérieur d'une
    /// paire de substitution ou d'un « \r\n » : elles ne désignent aucun caractère.
    /// </summary>
    public static IEnumerable<int> Positions(string texte)
    {
        for (var i = 0; i <= texte.Length; i++)
        {
            var interieure = i > 0 && i < texte.Length &&
                             (char.IsLowSurrogate(texte[i]) && char.IsHighSurrogate(texte[i - 1]) ||
                              texte[i] == '\n' && texte[i - 1] == '\r');
            if (!interieure) yield return i;
        }
    }

    public static int Lancer(string[] args)
    {
        var chemin = args.Length > 1 && !args[1].StartsWith('-') ? args[1] : null;
        var sortie = Option(args, "--sortie");
        if (chemin is null || sortie is null)
        {
            Console.Error.WriteLine("Usage : MyPublisher.Banc saisie <doc> --sortie <écarts.json> [--au-mieux] [--zoom 2]");
            return 2;
        }

        var auMieux = args.Contains("--au-mieux");
        var zoom = Option(args, "--zoom") is { } z ? double.Parse(z, CultureInfo.InvariantCulture) : (double?)null;

        Banc.Configurer().SetupWithoutStarting();
        var (fenetre, vm) = Banc.Ouvrir(chemin);
        vm.ShowGuides = false;
        vm.Selection.Clear();
        // Le zoom du banc est celui où une unité de mise en page vaut un point ; la
        // contre-épreuve en pose un autre, comme le ferait l'utilisateur.
        if (zoom is { } valeur) vm.Zoom = valeur;
        Banc.Battre();

        var releve = Mesurer(fenetre, vm, auMieux, Path.GetFileName(chemin));
        fenetre.Close();

        File.WriteAllText(sortie, JsonSerializer.Serialize(Exporter(releve),
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        var toutes = releve.Blocs.SelectMany(b => b.Positions).ToList();
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{releve.Blocs.Count} bloc(s) mesuré(s), {toutes.Count} position(s), {releve.Ecartes.Count} compté(s) sans mesure ; " +
            $"configuration {releve.Configuration}, zoom {releve.Zoom:0.####} (un point vaut {releve.Echelle:0.####} px) ; " +
            $"maximum {(toutes.Count == 0 ? 0 : toutes.Max(p => p.Ecart)):0.###} pt, " +
            $"95e centile {Centile(toutes.Select(p => p.Ecart), 0.95):0.###} pt, " +
            $"{toutes.Count(p => p.Ecart > Seuil)} au-dessus de {Seuil} pt, dans {sortie}"));
        return 0;
    }

    /// <summary>
    /// Mesure chaque bloc de texte visible, sans champ, au premier niveau de chaque page ; compte
    /// les autres — masqués, à champ, membres d'un groupe, cases d'un tableau — sans les mesurer.
    /// La page courante et la sélection sont rendues comme elles étaient.
    /// </summary>
    public static Releve Mesurer(Window fenetre, MainWindowViewModel vm, bool auMieux, string document)
    {
        var blocs = new List<Bloc>();
        var ecartes = new List<Ecarte>();
        var courante = vm.CurrentPage;
        for (var page = 1; page <= vm.Document.Pages.Count; page++)
        {
            vm.CurrentPage = vm.Document.Pages[page - 1];
            Banc.Battre();
            foreach (var frame in vm.Document.Pages[page - 1].Frames)
            {
                switch (frame)
                {
                    case TextFrame { Visible: false } masque:
                        ecartes.Add(new Ecarte(masque.DisplayName, page, "masqué"));
                        break;
                    case TextFrame { HasField: true } champ:
                        ecartes.Add(new Ecarte(champ.DisplayName, page, "champ"));
                        break;
                    case TextFrame texte:
                        blocs.Add(MesurerLeBloc(fenetre, texte, page, auMieux));
                        break;
                    case GroupFrame groupe:
                        foreach (var membre in Membres(groupe))
                            ecartes.Add(new Ecarte(membre.DisplayName, page, "membre d'un groupe (N26)"));
                        break;
                    case TableFrame tableau:
                        foreach (var cellule in tableau.CellFrames.OfType<TextFrame>())
                            ecartes.Add(new Ecarte(cellule.DisplayName, page, "case d'un tableau (N27)"));
                        break;
                }
            }
        }

        vm.CurrentPage = courante;
        Banc.Battre();
        var echelle = Banc.Page(fenetre).TransformToVisual(fenetre)?.M11 ?? double.NaN;
        return new Releve(document, auMieux ? "B" : "A", vm.Zoom, echelle, blocs, ecartes);
    }

    private static IEnumerable<TextFrame> Membres(GroupFrame groupe) =>
        groupe.Children.SelectMany(enfant => enfant switch
        {
            GroupFrame g => Membres(g),
            TextFrame t => [t],
            TableFrame t => t.CellFrames.OfType<TextFrame>(),
            _ => [],
        });

    /// <summary>
    /// Mesure un bloc de la page courante : la composition d'abord, telle qu'elle est dessinée,
    /// puis la saisie, ouverte par le banc et refermée après.
    /// </summary>
    public static Bloc MesurerLeBloc(Window fenetre, TextFrame bloc, int page, bool auMieux)
    {
        var planche = Banc.Page(fenetre);
        var controle = planche.GetVisualDescendants().OfType<TexteCompose>()
                           .SingleOrDefault(c => ReferenceEquals(c.Bloc, bloc))
                       ?? throw new InvalidOperationException($"« {bloc.DisplayName} » n'a pas de texte composé sur la page {page}.");
        var cadre = controle.GetVisualParent() as Visual
                    ?? throw new InvalidOperationException($"« {bloc.DisplayName} » : le texte composé n'a pas de parent.");
        var geometrie = controle.Geometrie()
                        ?? throw new InvalidOperationException($"« {bloc.DisplayName} » n'a pas de géométrie (N3).");
        // Le texte composé est dans son cadre à sa place de mise en page ; on la relève tant
        // qu'il est visible — la saisie le masque.
        var origine = controle.Bounds.Position;

        var texte = bloc.Text ?? string.Empty;
        var indices = Positions(texte).ToList();
        var composes = indices.Select(geometrie.PointDe).ToList();
        var commun = JeuAffiche.De(controle).StyleCommun(bloc);
        var metriques = new MetriquesAvalonia();

        var zone = Ouvrir(cadre, bloc);
        var poses = new List<(Layoutable Partie, bool Arrondi)>();
        try
        {
            if (auMieux)
            {
                // Au mieux de la voie 1 : l'interligne de la composition, et pas d'arrondi.
                zone.LineHeight = Compositeur.Avance(metriques.Naturelle(commun), bloc.LineHeight);
                foreach (var partie in zone.GetSelfAndVisualDescendants().OfType<Layoutable>())
                {
                    poses.Add((partie, partie.UseLayoutRounding));
                    partie.UseLayoutRounding = false;
                    partie.InvalidateMeasure();
                }

                Banc.Battre();
            }

            if (zone.Text != texte)
                throw new InvalidOperationException($"« {bloc.DisplayName} » : le champ ne montre pas le texte du bloc.");

            var lignes = Presentateur(zone).TextLayout.TextLines;
            var champ = PointsDuChamp(zone, cadre, origine, indices);
            var minimum = new Size(zone.MinWidth, zone.MinHeight);
            var naturelle = metriques.Naturelle(commun);
            var positions = new List<Position>(indices.Count);
            for (var n = 0; n < indices.Count; n++)
            {
                var (point, k, debut) = champ[n];
                var compose = composes[n];
                var position = new Position(indices[n], point.X - compose.X, point.Y - compose.Y);
                positions.Add(position with
                {
                    Motif = Motif(position, geometrie, k, debut, bloc, commun, naturelle, minimum, metriques),
                });
            }

            var causes = Causes(bloc, geometrie, commun, lignes.Select(l => l.FirstTextSourceIndex).ToList(),
                minimum, metriques);
            return new Bloc(bloc.DisplayName, page, bloc.Rotation, texte, causes,
                geometrie.Lignes.Count, lignes.Count, positions,
                geometrie.Lignes.SelectMany(l => l.Ligne.Morceaux).Select(m => Faconnage(m, metriques)).DefaultIfEmpty(0).Max());
        }
        finally
        {
            if (auMieux)
            {
                zone.ClearValue(TextBox.LineHeightProperty);
                foreach (var (partie, arrondi) in poses) partie.UseLayoutRounding = arrondi;
            }

            Fermer(zone, bloc);
        }
    }

    /// <summary>Le point d'insertion du champ, sa ligne et le premier caractère de celle-ci.</summary>
    public readonly record struct PointDuChamp(Point Point, int Ligne, int DebutDeLigne);

    /// <summary>Le présentateur du texte d'un champ de saisie, celui que le thème nomme <c>PART_TextPresenter</c>.</summary>
    public static TextPresenter Presentateur(TextBox zone) =>
        zone.GetVisualDescendants().OfType<TextPresenter>().SingleOrDefault(p => p.Name == "PART_TextPresenter")
        ?? throw new InvalidOperationException("le champ n'a pas de PART_TextPresenter.");

    /// <summary>
    /// Le point d'insertion que le champ ouvert dessine pour chaque position : l'abscisse que
    /// <c>HitTestTextPosition</c> donne, sur la ligne de base de la ligne où il la place, telle
    /// que le présentateur empile ses lignes au dessin ; ramené dans le repère du texte composé,
    /// <paramref name="cadre"/> étant leur parent commun et <paramref name="origine"/> la place du
    /// texte composé dans ce cadre. Le champ, ouvert par le banc ou au double-clic, ne bouge pas.
    /// </summary>
    public static IReadOnlyList<PointDuChamp> PointsDuChamp(TextBox zone, Visual cadre, Point origine, IReadOnlyList<int> indices)
    {
        var presentateur = Presentateur(zone);
        var repere = presentateur.TransformToVisual(cadre)
                     ?? throw new InvalidOperationException("le présentateur n'est pas dans le cadre du bloc.");
        var miseEnPage = presentateur.TextLayout;
        var lignes = miseEnPage.TextLines;
        var sommets = new double[lignes.Count];
        for (var k = 1; k < lignes.Count; k++) sommets[k] = sommets[k - 1] + lignes[k - 1].Height;

        var points = new List<PointDuChamp>(indices.Count);
        foreach (var indice in indices)
        {
            var caret = miseEnPage.HitTestTextPosition(indice);
            var k = 0;
            while (k + 1 < lignes.Count && sommets[k + 1] <= caret.Y + 1e-6) k++;
            var point = new Point(caret.X, sommets[k] + lignes[k].Baseline).Transform(repere) - origine;
            points.Add(new PointDuChamp(point, k, lignes[k].FirstTextSourceIndex));
        }

        return points;
    }

    /// <summary>
    /// Ouvre la saisie du bloc sans la planche : le champ se montre, et prend le focus, comme
    /// au double-clic — le gabarit du thème change d'allure au focus, et c'est ce champ-là que
    /// l'on tape. La planche n'ouvre une transaction que pour la saisie qu'elle a ouverte.
    /// </summary>
    private static TextBox Ouvrir(Visual cadre, TextFrame bloc)
    {
        bloc.IsEditing = true;
        Banc.Battre();
        var zone = cadre.GetVisualChildren().OfType<TextBox>().SingleOrDefault(t => ReferenceEquals(t.DataContext, bloc))
                   ?? throw new InvalidOperationException($"« {bloc.DisplayName} » n'a pas de champ de saisie.");
        if (!zone.IsEffectivelyVisible)
            throw new InvalidOperationException($"« {bloc.DisplayName} » : le champ de saisie ne se montre pas.");
        zone.Focus();
        Banc.Battre();
        return zone;
    }

    private static void Fermer(TextBox zone, TextFrame bloc)
    {
        bloc.IsEditing = false;
        TopLevel.GetTopLevel(zone)?.FocusManager?.ClearFocus();
        Banc.Battre();
    }

    /// <summary>
    /// Ce que le bloc porte et que le champ ne suit pas, en A : les causes que N7 nomme, dans son
    /// ordre, puis celles que la mesure a ajoutées — l'italique et l'exposant des fragments, la
    /// justification, des marges ou une largeur hors du point entier, que l'arrondi de mise en
    /// page déplace. Un bloc peut n'en porter aucune.
    /// </summary>
    public static IReadOnlyList<string> Causes(
        TextFrame bloc, GeometrieDuTexte geometrie, StyleEffectif commun, IReadOnlyList<int> debutsDuChamp,
        Size minimum, MetriquesAvalonia metriques)
    {
        var causes = new List<string>();
        var styles = geometrie.Lignes.SelectMany(l => l.Ligne.Morceaux).Select(m => m.Style).ToList();
        var ecarts = bloc.Paragraphs.Where(p =>
            p.IndentLeft is not null || p.IndentRight is not null || p.IndentFirstLine is not null ||
            p.SpaceBefore is not null || p.SpaceAfter is not null || p.Align is not null).ToList();
        var reglages = bloc.Paragraph;

        if (styles.Any(s => Math.Abs(s.FontSize - commun.FontSize) > 1e-9)) causes.Add("corps");
        if (styles.Any(s => !string.Equals(s.FontFamily, commun.FontFamily, StringComparison.Ordinal))) causes.Add("police");
        if (styles.Any(s => s.Bold != commun.Bold)) causes.Add("graisse");
        if (ecarts.Count > 0) causes.Add("écarts de paragraphe");
        if (reglages.IndentLeft != 0 || reglages.IndentRight != 0 || reglages.IndentFirstLine != 0)
            causes.Add("retraits du bloc");
        if (reglages.SpaceBefore != 0 || reglages.SpaceAfter != 0) causes.Add("espaces du bloc");
        if (Math.Abs(bloc.LineHeight - 1) > 1e-9) causes.Add("interligne");
        if (bloc.Columns > 1) causes.Add("colonnes");
        if ((bloc.Text ?? string.Empty).Contains('\t')) causes.Add("tabulations");
        if (bloc.VerticalAlign != VerticalAlign.Top) causes.Add("calage");
        var hauteur = Math.Max(1, bloc.Height - bloc.PaddingTop - bloc.PaddingBottom);
        if (Compositeur.Deborde(geometrie.Lignes.Select(l => l.Ligne).ToList(), hauteur, bloc.PaddingBottom, bloc.VerticalAlign))
            causes.Add("débordement");
        if (styles.Select(s => s.FontFamily).Append(commun.FontFamily).Any(Symboles)) causes.Add("police de symboles");
        if (!geometrie.Lignes.Select(l => l.Ligne.Debut).SequenceEqual(debutsDuChamp)) causes.Add("coupure");

        // Ce que N7 ne nommait pas, et qui déplace aussi un caractère.
        if (styles.Any(s => s.Italic != commun.Italic)) causes.Add("italique");
        if (styles.Any(s => s.BaselineShift != 0)) causes.Add("exposant");
        if (bloc.Align == TextAlign.Justify || ecarts.Any(p => p.Align == TextAlign.Justify)) causes.Add("justifié");
        if (new[] { bloc.PaddingLeft, bloc.PaddingTop, bloc.PaddingRight, bloc.PaddingBottom }.Any(Fractionnaire))
            causes.Add("marges fractionnaires");
        if (Fractionnaire(bloc.Width)) causes.Add("largeur fractionnaire");
        if (bloc.Width < minimum.Width || bloc.Height < minimum.Height) causes.Add("taille minimale du champ");
        if (geometrie.Lignes.SelectMany(l => l.Ligne.Morceaux).Any(m => Faconnage(m, metriques) > 0.01)) causes.Add("crénage ou ligature");
        return causes;
    }

    /// <summary>
    /// De combien le bord d'un caractère du morceau, dans le morceau façonné d'un tenant — tel
    /// qu'il se dessine —, s'écarte au plus de la chasse de ce qui le précède, mesuré seul — tel
    /// que <see cref="Compositeur.Abscisse"/> le compte : un crénage entre deux lettres, une
    /// ligature, dont l'intérieur prend le bord de la ligature entière. L'écart mesuré est alors,
    /// en partie, celui de la composition à son propre dessin.
    /// </summary>
    public static double Faconnage(Morceau morceau, MetriquesAvalonia metriques)
    {
        if (morceau.Texte.Length < 2) return 0;
        var style = morceau.Style;
        using var faconne = new Avalonia.Media.TextFormatting.TextLayout(
            morceau.Texte,
            new Avalonia.Media.Typeface(PolicesDuDocument.Famille(style.FontFamily),
                style.Italic ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal,
                style.Bold ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal),
            style.FontSize, null);
        var pire = 0.0;
        for (var k = 1; k < morceau.Texte.Length; k++)
        {
            if (char.IsLowSurrogate(morceau.Texte[k])) continue;
            var dessine = faconne.HitTestTextPosition(k).X;
            pire = Math.Max(pire, Math.Abs(dessine - metriques.Largeur(morceau.Texte[..k], style)));
        }

        return pire;
    }

    /// <summary>
    /// Ce qui met une position au-dessus du seuil, lu sur sa ligne — la première raison qui
    /// s'applique, dans cet ordre : la ligne est dans une autre colonne que la première, que le
    /// champ n'a pas ; elle ne commence pas au même caractère des deux côtés (« coupure ») ; elle
    /// commence au même, mais n'a pas le même rang, une coupure d'avant ayant ajouté ou ôté une
    /// ligne (« coupure en amont »). Puis, si l'écart est vertical : le champ plus haut que le
    /// bloc, un espace de paragraphe sur cette ligne ou avant elle, une ligne plus haute que celle
    /// du style du bloc — un fragment d'un autre corps ou d'une autre graisse —, le calage,
    /// l'interligne. S'il est horizontal : une tabulation avant la position, un retrait, la
    /// justification, un fragment d'un autre style avant elle (ou sur la ligne, si elle n'est pas
    /// au fer à gauche), un fer de paragraphe autre que celui du bloc, une police de symboles, un
    /// crénage ou une ligature dans son morceau (<see cref="Faconnage(Morceau, MetriquesAvalonia)"/>) ;
    /// sinon « autre ».
    /// </summary>
    public static string? Motif(
        Position position, GeometrieDuTexte geometrie, int rangDuChamp, int debutDuChamp, TextFrame bloc,
        StyleEffectif commun, double naturelle, Size minimum, MetriquesAvalonia metriques)
    {
        if (position.Ecart <= Seuil) return null;

        var posee = geometrie.LigneDe(position.Indice);
        var ligne = posee.Ligne;
        if (ligne.Colonne > 0) return "colonnes";
        if (ligne.Debut != debutDuChamp) return "coupure";
        if (posee.Rang != rangDuChamp) return "coupure en amont";

        var jusquIci = geometrie.Lignes.Take(posee.Rang + 1).ToList();
        if (Math.Abs(position.Dy) > Seuil)
        {
            if (bloc.Height < minimum.Height) return "taille minimale du champ";
            if (jusquIci.Any(l => l.Ligne.EspaceAvant != 0) || jusquIci.SkipLast(1).Any(l => l.Ligne.EspaceApres != 0))
                return "espaces de paragraphe";
            if (jusquIci.Any(l => Math.Abs(l.Ligne.Naturelle - naturelle) > 1e-6)) return "hauteur des lignes";
            if (bloc.VerticalAlign != VerticalAlign.Top) return "calage";
            if (Math.Abs(bloc.LineHeight - 1) > 1e-9) return "interligne";
            return "ordonnée, autre";
        }

        if (bloc.Width < minimum.Width) return "taille minimale du champ";
        var texte = bloc.Text ?? string.Empty;
        if (texte.AsSpan(ligne.Debut, Math.Max(0, position.Indice - ligne.Debut)).Contains('\t')) return "tabulations";
        if (ligne.Retrait != 0) return "retraits";
        if (ligne.Fer == TextAlign.Justify) return "justification";
        if (ligne.Morceaux.Any(m => (ligne.Fer != TextAlign.Left || m.Debut < position.Indice) && Autre(m.Style, commun)))
            return "fragments";
        if (ligne.Fer != bloc.Align) return "fer du paragraphe";
        if (ligne.Morceaux.Any(m => Symboles(m.Style.FontFamily))) return "police de symboles";
        if (ligne.Morceaux.Any(m => m.Debut < position.Indice && position.Indice <= m.Debut + m.Texte.Length &&
                                    Faconnage(m, metriques) > 0.01))
            return "crénage ou ligature";
        return "autre";
    }

    private static bool Autre(StyleEffectif style, StyleEffectif commun) =>
        Math.Abs(style.FontSize - commun.FontSize) > 1e-9 || style.Bold != commun.Bold || style.Italic != commun.Italic ||
        style.BaselineShift != 0 || !string.Equals(style.FontFamily, commun.FontFamily, StringComparison.Ordinal);

    private static bool Fractionnaire(double valeur) => Math.Abs(valeur - Math.Round(valeur)) > 1e-9;

    /// <summary>Les polices dont les caractères ne sont pas des lettres : le champ les montre en lettres.</summary>
    private static bool Symboles(string famille) =>
        famille.StartsWith("Wingdings", StringComparison.OrdinalIgnoreCase) ||
        famille.StartsWith("Webdings", StringComparison.OrdinalIgnoreCase) ||
        famille.Equals("Symbol", StringComparison.OrdinalIgnoreCase) ||
        famille.Contains("Dingbats", StringComparison.OrdinalIgnoreCase) ||
        famille.Equals("Marlett", StringComparison.OrdinalIgnoreCase) ||
        famille.Equals("MT Extra", StringComparison.OrdinalIgnoreCase);

    /// <summary>Le relevé tel qu'il s'écrit : par bloc, le résumé, puis chaque position, au dix-millième de point.</summary>
    private static object Exporter(Releve releve) => new
    {
        releve.Document,
        releve.Configuration,
        releve.Zoom,
        releve.Echelle,
        Seuil,
        Blocs = releve.Blocs.Select(b => new
        {
            b.Nom,
            b.Page,
            b.Rotation,
            b.Texte,
            b.Causes,
            b.LignesComposees,
            b.LignesDuChamp,
            Maximum = Math.Round(b.Maximum, 4),
            Centile95 = Math.Round(b.Centile95, 4),
            b.AuDessusDuSeuil,
            b.PremiereQuiDiverge,
            Faconnage = Math.Round(b.Faconnage, 4),
            // [indice, Δx, Δy, motif] : le motif n'est écrit qu'au-dessus du seuil.
            Positions = b.Positions.Select(p => p.Motif is null
                ? new object[] { p.Indice, Math.Round(p.Dx, 4), Math.Round(p.Dy, 4) }
                : new object[] { p.Indice, Math.Round(p.Dx, 4), Math.Round(p.Dy, 4), p.Motif }),
        }),
        releve.Ecartes,
    };

    private static string? Option(string[] args, string nom)
    {
        var i = Array.IndexOf(args, nom);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
