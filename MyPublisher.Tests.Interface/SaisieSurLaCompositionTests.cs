using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;
using Xunit.Abstractions;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// La saisie montre le texte composé — le poste N21, dans la voie 2a que le propriétaire a
/// choisie le 26 octobre 2026 sur la mesure de N7 (fiche 23, a10, a11, a20 et a21).
///
/// Pendant la saisie, l'écran montrait un champ en texte brut à la place de la composition : ni
/// fragments, ni écarts de paragraphe, ni taquets, ni colonnes, une autre coupure — jusqu'à 471 pt
/// entre son curseur et le caractère composé. Désormais la composition reste à l'écran, et le
/// champ, caché et hors du test de survol, ne fait plus que la frappe. Le curseur, la sélection et
/// le pré-édit se dessinent sur la composition, dans l'Overlay ; le clic, le glissé, le double et
/// le triple clic se lisent sur elle.
///
/// Les gestes sont ceux du banc (<see cref="Rendu"/>, poste N1). Les points visés sont lus sur ce
/// que la composition dessine — ses morceaux posés et leur texte façonné —, et non sur la
/// géométrie que la planche emploie : un test qui referait le calcul de la planche ne prouverait
/// rien d'elle.
/// </summary>
public class SaisieSurLaCompositionTests(ITestOutputHelper sortie)
{
    // ------------------------------------------------------------------ le banc du poste

    /// <summary>Le seuil de N7 : un pixel au zoom 100 %, en points.</summary>
    private const double Seuil = 0.75;

    private static TextFrame Texte(string nom = "Texte", double x = 60, double y = 60,
                                   string texte = "Bonjour le monde", double rotation = 0) => new()
    {
        Name = nom, X = x, Y = y, Width = 300, Height = 120, Text = texte, Rotation = rotation,
        FontFamily = "DejaVu Sans", FontSize = 14, Foreground = "#FF000000",
    };

    private static Document Exemple() =>
        DocumentSerializer.FromJson(File.ReadAllText(Path.Combine(Rendu.Racine(), "Samples", "exemple-bulletin.mypub")));

    private static TexteCompose Compose(Window fenetre, TextFrame bloc) =>
        fenetre.GetVisualDescendants().OfType<TexteCompose>().Single(c => ReferenceEquals(c.Bloc, bloc));

    private static GeometrieDuTexte Geometrie(Window fenetre, TextFrame bloc) =>
        Compose(fenetre, bloc).Geometrie() ?? throw new InvalidOperationException($"« {bloc.Name} » n'a pas de géométrie");

    private static MarquesDuTexte Marques(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<MarquesDuTexte>().Single(m => m.Name == "MarquesDeLaSaisie");

    private static (int, int) Selection(TextBox zone) =>
        (Math.Min(zone.SelectionStart, zone.SelectionEnd), Math.Max(zone.SelectionStart, zone.SelectionEnd));

    /// <summary>Referme la saisie et vide la sélection.</summary>
    private static void Sortir(Window fenetre, MainWindowViewModel vm)
    {
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        vm.SetSelection([]);
        Rendu.UneImage();
    }

    /// <summary>
    /// Un caractère tel que la composition le dessine : sa position dans le texte, le bord gauche
    /// et le bord droit de sa grappe dans le morceau façonné qui le porte, et la ligne posée —
    /// tout dans le repère du bloc non tourné. Lu sur ce que <see cref="TexteCompose.Render"/>
    /// dessine (<see cref="TexteCompose.Poser"/>) : le texte formaté de chaque morceau et la
    /// boîte de chacun de ses caractères (<c>FormattedText.BuildHighlightGeometry</c>).
    /// </summary>
    private readonly record struct CaractereDessine(int Indice, double Gauche, double Droite, TexteCompose.LignePosee Ligne);

    /// <summary>
    /// Chaque caractère dessiné d'un bloc qui commence une grappe — une lettre seule, ou la
    /// première d'une ligature : la seconde n'a pas de bord à elle dans le dessin.
    /// </summary>
    private static List<CaractereDessine> Dessines(TexteCompose compose)
    {
        var geometrie = compose.Geometrie() ?? throw new InvalidOperationException("pas de géométrie");
        var metriques = new MetriquesAvalonia();
        var poses = compose.Poser(metriques, metriques);
        var dessines = new List<CaractereDessine>();

        foreach (var posee in geometrie.Lignes)
        {
            var morceaux = posee.Ligne.Morceaux.Where(m => m.Texte.Trim().Length > 0).ToList();
            var siens = poses.Where(p => p.Ligne == posee.Rang).ToList();
            Assert.Equal(morceaux.Count, siens.Count);

            for (var m = 0; m < morceaux.Count; m++)
            {
                Assert.Equal(morceaux[m].Texte, siens[m].Texte);
                Rect? precedente = null;
                for (var dedans = 0; dedans < morceaux[m].Texte.Length; dedans++)
                {
                    var boite = siens[m].Rendu.BuildHighlightGeometry(siens[m].Ancre, dedans, 1)?.Bounds;
                    if (boite is not { } b || b == precedente) continue;
                    precedente = b;
                    dessines.Add(new CaractereDessine(morceaux[m].Debut + dedans, b.Left, b.Right, posee));
                }
            }
        }

        return dessines;
    }

    /// <summary>Un point du repère du bloc non tourné, dans la fenêtre : lu sur le contrôle qui dessine.</summary>
    private static Point DansLaFenetre(Window fenetre, TexteCompose compose, Point local) =>
        compose.TranslatePoint(local, fenetre) ?? throw new InvalidOperationException("la composition n'est pas dans la fenêtre");

    /// <summary>Le point visé d'un caractère dessiné : au tiers de sa largeur, à mi-hauteur de sa ligne.</summary>
    private static Point Vise(CaractereDessine caractere) =>
        new(caractere.Gauche + (caractere.Droite - caractere.Gauche) / 3,
            (caractere.Ligne.Haut + caractere.Ligne.LigneDeBase) / 2);

    /// <summary>
    /// Le curseur dessiné tombe-t-il sous le caractère : son trait au bord gauche de la grappe, à
    /// moins du seuil de N7, et sa hauteur sur la ligne du caractère, ligne de base comprise.
    /// </summary>
    private static void CurseurSous(MarquesDuTexte marques, CaractereDessine caractere, string quoi)
    {
        var curseur = marques.CurseurPose ?? throw new InvalidOperationException($"{quoi} : aucun curseur dessiné");
        Assert.True(Math.Abs(curseur.X - caractere.Gauche) < Seuil,
            $"{quoi} : le curseur est dessiné à {curseur.X:F2} pt, le caractère à {caractere.Gauche:F2} pt");
        Assert.True(curseur.Top <= caractere.Ligne.LigneDeBase && caractere.Ligne.LigneDeBase <= curseur.Bottom,
            $"{quoi} : le curseur ({curseur.Top:F1} à {curseur.Bottom:F1}) n'est pas sur la ligne du caractère");
    }

    // ------------------------------------------------------------------ ce qu'on voit

    /// <summary>
    /// La recette : pendant la saisie, l'encre de la page est celle d'avant l'entrée, au pixel, sur
    /// l'exemple et sur ATA 21 — la composition reste, le champ caché n'en dessine rien. Puis ce
    /// qu'on tape est ce qui s'imprime : l'encre de la page pendant la saisie est celle de la page
    /// une fois la saisie refermée. Le champ est caché et hors du test de survol, la couche garde le
    /// clic sur le texte en saisie. Au juge : la composition de nouveau masquée pendant la saisie.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N21")]
    public void Pendant_la_saisie_la_page_montre_la_composition_au_pixel()
    {
        foreach (var (nom, document) in new[] { ("l'exemple", Exemple()), ("ATA 21", Rendu.Ata21()) })
        {
            var (fenetre, vm) = Rendu.Ouvrir(document);
            var page = vm.CurrentPage!;
            var bloc = page.Frames.OfType<TextFrame>()
                .Where(b => b.Visible && !b.HasField && b.Rotation == 0 && (b.Text?.Length ?? 0) > 20)
                .First(b => Dessines(Compose(fenetre, b)).Any(c => Atteint(page, b, c)));
            var vise = Dessines(Compose(fenetre, bloc)).First(c => Atteint(page, bloc, c));

            Rendu.UneImage();
            var avant = Rendu.Encre(fenetre);
            Rendu.Cliquer(fenetre, DansLaFenetre(fenetre, Compose(fenetre, bloc), Vise(vise)));
            Assert.True(bloc.IsEditing, $"{nom} : le clic n'a pas ouvert la saisie de « {bloc.Name} »");

            var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
            Assert.True(zone.IsFocused, $"{nom} : le champ caché n'a pas le focus");
            Assert.Equal(0, zone.Opacity);
            Assert.False(zone.IsHitTestVisible, $"{nom} : le champ est encore au test de survol");
            Assert.True(Compose(fenetre, bloc).IsEffectivelyVisible, $"{nom} : la composition est masquée pendant la saisie");

            var pendant = Rendu.Encre(fenetre);
            Assert.True(avant.Pixels.SequenceEqual(pendant.Pixels),
                $"{nom} : l'encre de la page change quand on entre dans « {bloc.Name} » ({Ecarts(avant, pendant)} pixels)");

            // Le clic sur le texte en saisie est à la couche, et non au champ.
            var couche = fenetre.GetVisualDescendants().OfType<CoucheDeSaisie>().Single(c => c.Name == "InputLayer");
            var surLaCouche = Compose(fenetre, bloc).TranslatePoint(Vise(vise), couche)!.Value;
            Assert.True(couche.HitTest(surLaCouche), $"{nom} : la couche laisse le texte en saisie au champ");

            Rendu.Taper(fenetre, "Xy");
            Assert.Contains("Xy", bloc.Text);
            var tape = Rendu.Encre(fenetre);
            Assert.False(tape.Pixels.SequenceEqual(pendant.Pixels), $"{nom} : la frappe ne se voit pas sur la page");
            Rendu.Touche(fenetre, PhysicalKey.Escape);
            Assert.False(bloc.IsEditing);
            var imprime = Rendu.Encre(fenetre);
            Assert.True(tape.Pixels.SequenceEqual(imprime.Pixels),
                $"{nom} : ce qu'on voit en tapant n'est pas ce que la page montre hors saisie ({Ecarts(tape, imprime)} pixels)");
            sortie.WriteLine($"{nom} : « {bloc.Name} », {avant.Largeur} × {avant.Hauteur} px identiques");
            fenetre.Close();
        }
    }

    private static int Ecarts(Rendu.Image a, Rendu.Image b) =>
        Enumerable.Range(0, Math.Min(a.Pixels.Length, b.Pixels.Length) / 4)
            .Count(i => !a.Pixels.AsSpan(i * 4, 4).SequenceEqual(b.Pixels.AsSpan(i * 4, 4)));

    /// <summary>Le caractère est-il atteint par un clic, son bloc au-dessus des autres en ce point ?</summary>
    private static bool Atteint(Page page, TextFrame bloc, CaractereDessine caractere)
    {
        if (caractere.Droite - caractere.Gauche < 3) return false;
        var point = Vise(caractere);
        var bord = 8;
        if (point.X < bord || point.Y < bord || point.X > bloc.Width - bord || point.Y > bloc.Height - bord) return false;
        var (x, y) = bloc.ToPage(point.X, point.Y);
        return ReferenceEquals(bloc, page.Frames.LastOrDefault(f => f.Visible && f.HitTest(x, y)));
    }

    /// <summary>
    /// La recette : sur chaque bloc de texte d'ATA 21 qu'un clic atteint, un clic sur un caractère
    /// — repéré sur ce que la composition dessine, au milieu du bloc — y pose le curseur, et le
    /// curseur dessiné est sous lui, à moins du seuil de N7 : son bord gauche dans le morceau
    /// façonné. Puis, la saisie ouverte, un clic sur un caractère d'une autre ligne — c'est la
    /// couche qui le prend, et non plus le champ — l'y déplace de même. Le document reste intact.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N21")]
    public void Un_clic_sur_un_caractere_d_ATA_21_y_pose_le_curseur_dessine_sous_lui()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Ata21());
        var page = vm.CurrentPage!;
        var avant = DocumentSerializer.ToJson(vm.Document);
        var marques = Marques(fenetre);
        var joues = 0;

        foreach (var bloc in page.Frames.OfType<TextFrame>().Where(b => b.Visible && !b.HasField).ToList())
        {
            var compose = Compose(fenetre, bloc);
            var atteints = Dessines(compose).Where(c => Atteint(page, bloc, c)).ToList();
            if (atteints.Count < 2) continue;

            var premier = atteints[atteints.Count / 2];
            var second = atteints.FirstOrDefault(c => c.Ligne.Rang != premier.Ligne.Rang);
            if (second == default) second = atteints[0];

            Rendu.Cliquer(fenetre, DansLaFenetre(fenetre, compose, Vise(premier)));
            Assert.True(bloc.IsEditing, $"« {bloc.Name} » : le clic n'a pas ouvert la saisie");
            var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
            Assert.Equal((premier.Indice, premier.Indice), Selection(zone));
            CurseurSous(marques, premier, $"« {bloc.Name} », caractère {premier.Indice}");

            Rendu.Cliquer(fenetre, DansLaFenetre(fenetre, compose, Vise(second)));
            Assert.True(bloc.IsEditing, $"« {bloc.Name} » : le clic dans le texte en saisie l'a refermée");
            Assert.Equal((second.Indice, second.Indice), Selection(zone));
            CurseurSous(marques, second, $"« {bloc.Name} », caractère {second.Indice}");

            sortie.WriteLine($"« {bloc.Name} » : {premier.Indice} « {bloc.Text![premier.Indice]} », " +
                             $"puis {second.Indice} « {bloc.Text[second.Indice]} »");
            joues++;
            Sortir(fenetre, vm);
            Assert.False(marques.Visibles, "les marques restent après la sortie de la saisie");
        }

        Assert.True(joues >= 5, $"{joues} blocs d'ATA 21 joués seulement");
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// Le bord d'un caractère est lu dans le morceau façonné d'un tenant, comme le dessin le pose
    /// (la reprise de N3 que la décision du 26 octobre 2026 demandait avant la voie 2a) : sur
    /// l'exemple et ATA 21, chaque caractère qui commence une grappe a son point d'insertion au
    /// bord que le dessin lui donne, à un centième de point. La chasse du préfixe mesurée seule,
    /// comme avant, s'en écartait de plus du seuil sur des ligatures (« fl » de « butterfly »,
    /// « fi » de « fontconfig ») : le test le vérifie aussi, pour ne pas se prouver sur un texte
    /// où les deux mesures se confondent. Le clic retrouve chaque position, ligatures comprises.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N21")]
    public void Le_bord_de_chaque_caractere_est_lu_dans_le_morceau_faconne()
    {
        foreach (var (nom, document) in new[] { ("l'exemple", Exemple()), ("ATA 21", Rendu.Ata21()) })
        {
            var (fenetre, vm) = Rendu.Ouvrir(document);
            var metriques = new MetriquesAvalonia();
            int positions = 0, auDelaDuSeuilAvant = 0;
            var pire = 0.0;

            foreach (var feuille in vm.Document.Pages)
            {
                vm.CurrentPage = feuille;
                Rendu.UneImage();
                foreach (var compose in fenetre.GetVisualDescendants().OfType<TexteCompose>().ToList())
                {
                    if (compose.Geometrie() is not { } geometrie) continue;
                    var texte = compose.Bloc!.Text ?? "";

                    foreach (var caractere in Dessines(compose))
                    {
                        var ecart = Math.Abs(geometrie.PointDe(caractere.Indice).X - caractere.Gauche);
                        pire = Math.Max(pire, ecart);
                        positions++;
                        Assert.True(ecart < 0.01,
                            $"{nom}, « {compose.Bloc.Name} », position {caractere.Indice} : point d'insertion à {ecart:F3} pt du dessin");
                    }

                    foreach (var posee in geometrie.Lignes)
                    foreach (var morceau in posee.Ligne.Morceaux)
                    {
                        for (var dedans = 1; dedans < morceau.Texte.Length; dedans++)
                        {
                            var prefixe = metriques.Largeur(morceau.Texte[..dedans], morceau.Style);
                            if (Math.Abs(prefixe - metriques.Bord(morceau.Texte, morceau.Style, dedans)) > Seuil) auDelaDuSeuilAvant++;
                        }
                    }

                    for (var i = 0; i <= texte.Length; i++)
                    {
                        if (i > 0 && i < texte.Length && char.IsLowSurrogate(texte[i]) && char.IsHighSurrogate(texte[i - 1])) continue;
                        Assert.Equal(i, geometrie.IndexSous(geometrie.PointDe(i)));
                    }
                }
            }

            sortie.WriteLine($"{nom} : {positions} bords au dessin (pire {pire:F4} pt) ; " +
                             $"{auDelaDuSeuilAvant} positions où la chasse du préfixe s'écartait de plus de {Seuil} pt");
            Assert.True(positions > 1000, $"{nom} : {positions} bords seulement");
            Assert.True(auDelaDuSeuilAvant > 0, $"{nom} : aucune ligature ni crénage au-delà du seuil, le test ne prouve rien");
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ la souris

    /// <summary>
    /// Le glissé dans le texte en saisie se lit sur la composition, que le champ ne suit pas : un
    /// bloc dont le second paragraphe porte un retrait de 60 pt et un mot de 24 pt. Le glissé du
    /// 3ᵉ caractère du second paragraphe au 8ᵉ sélectionne exactement ceux-là, et la sélection se
    /// surligne sur eux — un rectangle, du bord de l'un au bord de l'autre.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N21")]
    public void Le_glisse_dans_le_texte_en_saisie_suit_la_composition()
    {
        var bloc = Texte(texte: "Premier paragraphe\nGRAND second paragraphe");
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 1, Length = 1, IndentLeft = 60 });
        bloc.Spans.Add(new StyleSpan { Start = 19, Length = 5, FontSize = 24 });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var compose = Compose(fenetre, bloc);
        var dessines = Dessines(compose);
        var premier = dessines.Single(c => c.Indice == 3);
        Rendu.Cliquer(fenetre, DansLaFenetre(fenetre, compose, Vise(premier)));
        Assert.True(bloc.IsEditing);
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;

        var debut = dessines.Single(c => c.Indice == 21);
        var fin = dessines.Single(c => c.Indice == 27);
        Rendu.Glisser(fenetre, DansLaFenetre(fenetre, compose, Vise(debut)),
            DansLaFenetre(fenetre, compose, new Point(fin.Droite - 0.5, (fin.Ligne.Haut + fin.Ligne.LigneDeBase) / 2)));
        Rendu.Battre();

        Assert.Equal((21, 28), Selection(zone));
        Assert.Equal(new SelectionDuTexte(bloc, null, 21, 28, 28), vm.SelectionDuTexte);
        var marques = Marques(fenetre);
        var plage = Assert.Single(marques.Plages);
        Assert.True(Math.Abs(plage.Left - debut.Gauche) < 0.01, $"la sélection commence à {plage.Left:F2}, le caractère à {debut.Gauche:F2}");
        Assert.True(Math.Abs(plage.Right - fin.Droite) < 0.01, $"la sélection finit à {plage.Right:F2}, le caractère à {fin.Droite:F2}");
        Assert.Null(marques.CurseurPose);
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// Le double-clic prend le mot sous le pointeur, et non le mot de la position la plus proche :
    /// la moitié droite du « e » de « monde, » prend « monde » ; la virgule se prend seule. Le
    /// triple-clic prend le paragraphe, saut de ligne forcé compris, sans le saut qui le ferme —
    /// le champ prenait tout le texte (fiche 23, a11). Le surlignage suit, ligne par ligne.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N21")]
    public void Le_double_clic_prend_le_mot_et_le_triple_clic_le_paragraphe()
    {
        var texte = "Bonjour le monde, entier.\nDeuxième paragraphe" + Compositeur.SautDeLigne + "suite\nTrois";
        var bloc = Texte(texte: texte);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var compose = Compose(fenetre, bloc);
        var dessines = Dessines(compose);
        Point Sur(int indice, double fx)
        {
            var c = dessines.Single(d => d.Indice == indice);
            return DansLaFenetre(fenetre, compose, new Point(c.Gauche + fx * (c.Droite - c.Gauche), (c.Ligne.Haut + c.Ligne.LigneDeBase) / 2));
        }

        Rendu.Cliquer(fenetre, Sur(1, 0.3));
        Assert.True(bloc.IsEditing);
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;

        Rendu.DoubleCliquer(fenetre, Sur(15, 0.8));
        Assert.Equal("monde", zone.SelectedText);
        Assert.Equal((11, 16), Selection(zone));

        Rendu.DoubleCliquer(fenetre, Sur(16, 0.5));
        Assert.Equal(",", zone.SelectedText);

        Rendu.TripleCliquer(fenetre, Sur(30, 0.5));
        Assert.Equal("Deuxième paragraphe" + Compositeur.SautDeLigne + "suite", zone.SelectedText);
        Assert.True(bloc.IsEditing, "le triple-clic a refermé la saisie");
        Assert.Equal(2, Marques(fenetre).Plages.Count);
        Assert.Equal(new Plage(1, 1), vm.ParagraphesDeLaSelection);
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// Après un double-clic, le glissé étend la sélection par mots, sans perdre le mot pris : vers
    /// l'avant, jusqu'au bout du mot atteint ; vers l'arrière, jusqu'à son début, le mot pris
    /// gardé en entier. Le geste est joué en ligne : deux pressions, la seconde tenue et menée.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N21")]
    public void Apres_un_double_clic_le_glisse_etend_par_mots()
    {
        var bloc = Texte(texte: "un deux trois quatre cinq");
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var compose = Compose(fenetre, bloc);
        var dessines = Dessines(compose);
        Point Sur(int indice) => DansLaFenetre(fenetre, compose, Vise(dessines.Single(d => d.Indice == indice)));

        Rendu.Cliquer(fenetre, Sur(0));
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;

        foreach (var (vers, attendu) in new[] { (15, (8, 20)), (4, (3, 13)) })
        {
            fenetre.MouseDown(Sur(9), MouseButton.Left);
            fenetre.MouseUp(Sur(9), MouseButton.Left);
            Rendu.UneImage();
            fenetre.MouseDown(Sur(9), MouseButton.Left);
            fenetre.MouseMove(Sur(vers), RawInputModifiers.LeftMouseButton);
            fenetre.MouseUp(Sur(vers), MouseButton.Left);
            Rendu.UneImage();
            Assert.Equal(attendu, Selection(zone));

            // Une pression ailleurs, et le temps du double-clic passé : le geste suivant repart.
            Rendu.Cliquer(fenetre, Sur(22));
            System.Threading.Thread.Sleep(600);
        }
    }

    /// <summary>
    /// <c>Maj</c>+clic dans le texte en saisie étend la sélection depuis son ancre, sur la
    /// composition. Le clic droit garde la saisie et la sélection, et sa demande de menu remonte
    /// de la couche à la planche, où le menu du texte (A12) la trouvera telle qu'elle était.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N21")]
    public void Maj_clic_etend_la_selection_et_le_clic_droit_la_garde()
    {
        var bloc = Texte(texte: "Bonjour le monde entier");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var compose = Compose(fenetre, bloc);
        var dessines = Dessines(compose);
        Point Sur(int indice) => DansLaFenetre(fenetre, compose, Vise(dessines.Single(d => d.Indice == indice)));

        Rendu.Cliquer(fenetre, Sur(4));
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
        Rendu.Cliquer(fenetre, Sur(13), RawInputModifiers.Shift);
        Assert.True(bloc.IsEditing);
        Assert.Equal((4, 13), Selection(zone));
        Assert.Equal(new SelectionDuTexte(bloc, null, 4, 13, 13), vm.SelectionDuTexte);

        var planche = fenetre.GetVisualDescendants().OfType<PageCanvas>().Single();
        // Le menu du texte (A12) prend la demande, et la marque traitée : on la compte au passage,
        // traitée ou non (règle 14, repris à la fusion d'A12).
        var demandes = 0;
        planche.AddHandler(Control.ContextRequestedEvent, (_, _) => demandes++, RoutingStrategies.Bubble, handledEventsToo: true);
        var menu = Rendu.CliquerDroit(fenetre, Sur(8));
        Assert.True(bloc.IsEditing, "le clic droit dans le texte a refermé la saisie");
        Assert.Equal(1, demandes);
        Assert.Same(planche.ContextMenu, menu);
        Assert.Equal((4, 13), Selection(zone));
        Assert.Single(Marques(fenetre).Plages);
    }

    // ------------------------------------------------------------------ les marques

    /// <summary>
    /// La sélection dessinée est celle que le modèle de vue garde (N15) : quand le champ Corps
    /// prend le focus, le champ de la saisie réduit la sienne à un curseur, et le surlignage reste
    /// sur les caractères sélectionnés — c'est là que la barre mettra en forme (R7). Le curseur ne
    /// se dessine pas sans le focus, ni sur une sélection. Échap efface tout.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N21")]
    public void La_selection_dessinee_reste_celle_du_modele_de_vue_quand_la_barre_prend_le_focus()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.SelectionnerDuTexte(zone, 4, 7);
        Assert.Equal((4, 7), Selection(zone));

        var marques = Marques(fenetre);
        var geometrie = Geometrie(fenetre, bloc);
        var plage = Assert.Single(marques.Plages);
        Assert.Equal(geometrie.PointDe(4).X, plage.Left, 6);
        Assert.Equal(geometrie.PointDe(7).X, plage.Right, 6);
        Assert.Null(marques.CurseurPose);

        var corps = fenetre.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "CorpsDuBloc")
            .GetVisualDescendants().OfType<TextBox>().First();
        Rendu.Cliquer(corps);
        Assert.True(corps.IsFocused);
        Assert.Equal(zone.SelectionStart, zone.SelectionEnd);
        Rendu.Battre();
        plage = Assert.Single(marques.Plages);
        Assert.Equal(geometrie.PointDe(4).X, plage.Left, 6);
        Assert.Null(marques.CurseurPose);

        // Retour au texte : un clic y pose le curseur, qui se dessine, et le surlignage part.
        Rendu.Cliquer(fenetre, Rendu.BordDuCaractere(zone, 2) + new Vector(1, 0));
        Assert.True(zone.IsFocused);
        Assert.Empty(marques.Plages);
        Assert.Equal(geometrie.CurseurDe(2), marques.CurseurPose);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.Battre();
        Assert.False(marques.Visibles, "les marques restent après Échap");
    }

    /// <summary>
    /// Le curseur se dessine sur la composition, mené dans l'Overlay par la transformation du
    /// contrôle qui la dessine : sur un bloc tourné de 30°, à 200 %, ses deux bouts tombent là où la
    /// composition met le haut et le bas de la bande de sa ligne, au centième de pixel ; une flèche
    /// le déplace d'un caractère, le champ toujours caché.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N21")]
    public void Le_curseur_se_dessine_sur_la_composition_d_un_bloc_tourne_au_zoom_200()
    {
        var bloc = Texte(texte: "Bonjour le monde", rotation: 30);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.Zoom = 2 / Units.PointToDip;
        Rendu.UneImage();
        var compose = Compose(fenetre, bloc);
        var dessines = Dessines(compose);
        Rendu.Cliquer(fenetre, DansLaFenetre(fenetre, compose, Vise(dessines.Single(d => d.Indice == 5))));
        Assert.True(bloc.IsEditing);
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
        Assert.Equal((5, 5), Selection(zone));

        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        Rendu.Battre();
        Assert.Equal((6, 6), Selection(zone));

        var marques = Marques(fenetre);
        var curseur = marques.CurseurPose ?? throw new InvalidOperationException("aucun curseur dessiné");
        Assert.Equal(Geometrie(fenetre, bloc).CurseurDe(6), curseur);
        var overlay = fenetre.GetVisualDescendants().OfType<Canvas>().Single(c => c.Name == "Overlay");
        foreach (var bout in new[] { curseur.TopLeft, curseur.BottomLeft })
        {
            var attendu = compose.TranslatePoint(bout, overlay)!.Value;
            var dessine = marques.TranslatePoint(bout.Transform(marques.Transformation), overlay)!.Value;
            Assert.True(Point.Distance(attendu, dessine) < 0.01, $"le curseur est dessiné en {dessine}, la composition le met en {attendu}");
        }

        Assert.True(curseur.Height * 2 > 14, "le curseur n'a pas la hauteur de sa ligne à 200 %");
    }

    /// <summary>
    /// Le pré-édit de la méthode d'entrée — ce qu'elle compose avant de l'écrire, que le champ
    /// caché reçoit — se dessine au curseur, sur la composition, au style où il entrera, et le
    /// curseur le suit ; il part quand la méthode d'entrée le retire. Rien n'entre au texte, ni à
    /// l'historique.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N21")]
    public void Le_pre_edit_se_dessine_au_curseur_sur_la_composition()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.Cliquer(fenetre, Rendu.BordDuCaractere(zone, 7) + new Vector(1, 0));
        Assert.Equal((7, 7), Selection(zone));

        var presentation = zone.GetVisualDescendants().OfType<TextPresenter>().Single();
        presentation.PreeditText = "かな";
        Rendu.Battre();

        var marques = Marques(fenetre);
        var geometrie = Geometrie(fenetre, bloc);
        var preEdit = marques.PreEdit ?? throw new InvalidOperationException("le pré-édit ne se dessine pas");
        Assert.Equal("かな", preEdit.Chaine);
        Assert.Equal(geometrie.PointDe(7).X, preEdit.Fond.Left, 6);
        Assert.Equal(geometrie.CurseurDe(7).Top, preEdit.Fond.Top, 6);
        Assert.True(preEdit.Fond.Width > 0);
        Assert.Equal(preEdit.Fond.Right, marques.CurseurPose!.Value.X, 6);
        Assert.Equal("Bonjour le monde", bloc.Text);

        presentation.PreeditText = null;
        Rendu.Battre();
        Assert.Null(marques.PreEdit);
        Assert.Equal(geometrie.CurseurDe(7), marques.CurseurPose);
        Assert.False(vm.CanUndo);
    }
}
