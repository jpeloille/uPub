using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les modificateurs du glisser, et le rectangle de sélection — le poste N17 (fiche 23, c8, c10
/// à c14), sur la vraie fenêtre, au pointeur synthétisé.
///
/// La planche ne lisait qu'<c>Alt</c> pendant un déplacement ; <c>Ctrl</c> et <c>Maj</c>
/// basculaient le bloc dans la sélection à la pression, et rien d'autre ; la seule copie était
/// <c>Ctrl+D</c>, décalée de 12 pt ; une pression dans le vide vidait la sélection, sans plus ;
/// le curseur de la rotation était une main. Désormais, comme Publisher : <c>Ctrl</c>+glisser
/// copie, <c>Maj</c>+glisser tient l'axe, <c>Maj</c> au tracé donne un carré, <c>Ctrl</c> trace
/// depuis le centre et redimensionne depuis le centre sur une poignée ; un rectangle tiré dans le
/// vide prend les blocs tout entiers dedans ; Échap annule le glissé en cours ; le curseur de la
/// rotation est la flèche circulaire de l'apparence.
///
/// Les gestes sont ceux du banc (<see cref="Rendu"/>) ; <see cref="Rendu.Tenir"/> et
/// <see cref="Rendu.Lacher"/> coupent un glissé en deux, pour ce qui se passe avant le relâché.
/// Les blocs sont des formes, sans texte où entrer, posés loin des marges et du milieu de la
/// page, sauf là où l'aimantation est ce qu'on éprouve.
/// </summary>
public class ModificateursDuGlisserTests
{
    // ------------------------------------------------------------------ le banc du poste

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static ShapeFrame Forme(string nom, double x, double y, double largeur = 80, double hauteur = 60) => new()
    {
        Name = nom, X = x, Y = y, Width = largeur, Height = hauteur, Fill = new SolidFill("#FF336699"),
    };

    private static void RienNaChange(MainWindowViewModel vm, string avant, string geste)
    {
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, $"{geste} a empilé un pas d'annulation");
        Assert.False(vm.IsDirty, $"{geste} a marqué le document modifié");
    }

    private static void Echap(Window fenetre) => Rendu.Touche(fenetre, PhysicalKey.Escape);

    /// <summary>Le centre d'un bloc, en points de page, d'après le modèle : le pivot de sa rotation.</summary>
    private static (double X, double Y) CentreDe(Frame bloc) => (bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2);

    // ------------------------------------------------------------------ Ctrl+glisser copie

    /// <summary>
    /// La recette du plan : Ctrl+glisser donne deux blocs en un pas. L'original reste à sa place,
    /// la copie suit le pointeur et devient la sélection ; une annulation rend la page d'un bloc.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Ctrl_glisser_copie_le_bloc_et_laisse_l_original_en_un_pas()
    {
        var original = Forme("A", 150, 200);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(original));
        var avant = Etat(vm);
        var centre = Rendu.Centre(fenetre, original);

        Rendu.Glisser(fenetre, centre, centre + new Vector(50, 30), RawInputModifiers.Control);

        var page = vm.CurrentPage!;
        Assert.Equal(2, page.Frames.Count);
        Assert.Same(original, page.Frames[0]);
        Assert.Equal((150.0, 200.0), (original.X, original.Y));
        var copie = page.Frames[1];
        Assert.NotSame(original, copie);
        Assert.Equal((200.0, 230.0, 80.0, 60.0), (copie.X, copie.Y, copie.Width, copie.Height));
        Assert.Equal("A", copie.Name);
        Assert.Equal([copie], vm.Selection);

        Rendu.UnSeulPas(vm, avant, "Ctrl+glisser");
        Assert.Single(vm.CurrentPage!.Frames);
    }

    /// <summary>
    /// Sous Ctrl, le bloc pris s'ajoute à la sélection, et c'est toute la sélection qui est
    /// copiée, chaque copie au même écart de son original ; les originaux restent.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Ctrl_glisser_un_bloc_non_selectionne_copie_la_selection_et_lui()
    {
        var a = Forme("A", 120, 200);
        var b = Forme("B", 320, 200);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b));
        vm.SelectOnly(a);
        Rendu.Battre();
        var avant = Etat(vm);
        var centre = Rendu.Centre(fenetre, b);

        Rendu.Glisser(fenetre, centre, centre + new Vector(0, 150), RawInputModifiers.Control);

        var page = vm.CurrentPage!;
        Assert.Equal(4, page.Frames.Count);
        Assert.Equal((120.0, 200.0, 320.0, 200.0), (a.X, a.Y, b.X, b.Y));
        var copies = page.Frames.Skip(2).ToList();
        Assert.Equal(["A", "B"], copies.Select(c => c.Name));
        Assert.Equal([(120.0, 350.0), (320.0, 350.0)], copies.Select(c => (c.X, c.Y)));
        Assert.Equal(copies, vm.Selection);

        Rendu.UnSeulPas(vm, avant, "Ctrl+glisser deux blocs");
    }

    /// <summary>
    /// Le piège du plan : Ctrl change de sens à la pression. La bascule attend le relâché — un
    /// bloc pris le reste tant que le bouton est tenu, et le relâché le retire —, et un Ctrl+clic
    /// qui tremble, sous le seuil, ajoute le bloc sans rien copier ni empiler.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Ctrl_clic_bascule_au_relache_et_ne_copie_rien()
    {
        var a = Forme("A", 120, 200);
        var b = Forme("B", 320, 200);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b));
        vm.SelectOnly(a);
        Rendu.Battre();
        var avant = Etat(vm);

        var centre = Rendu.Centre(fenetre, a);
        Rendu.Tenir(fenetre, centre, centre, RawInputModifiers.Control);
        Assert.Equal([a], vm.Selection);
        Rendu.Lacher(fenetre, centre, RawInputModifiers.Control);
        Assert.Empty(vm.Selection);

        Rendu.Trembler(fenetre, Rendu.Centre(fenetre, b), new Vector(1, 1), RawInputModifiers.Control);
        Assert.Equal([b], vm.Selection);
        Assert.Equal(2, vm.CurrentPage!.Frames.Count);
        RienNaChange(vm, avant, "un Ctrl+clic");
    }

    // ------------------------------------------------------------------ Maj+glisser tient l'axe

    /// <summary>
    /// La recette du plan : Maj+glisser garde Y quand le chemin est surtout horizontal, X quand il
    /// est surtout vertical. Sur un bloc déjà pris, Maj ne le retire pas : il le déplace — la
    /// bascule attend le relâché, comme sous Ctrl. Chaque glissé fait son pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Maj_glisser_tient_l_axe_dominant()
    {
        var bloc = Forme("A", 150, 200);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        var centre = Rendu.Centre(fenetre, bloc);
        Rendu.Glisser(fenetre, centre, centre + new Vector(40, 10), RawInputModifiers.Shift | RawInputModifiers.Alt);
        Assert.Equal((190.0, 200.0), (bloc.X, bloc.Y));
        Assert.Equal([bloc], vm.Selection);
        Rendu.UnSeulPas(vm, avant, "Maj+glisser à l'horizontale");

        bloc = (ShapeFrame)vm.CurrentPage!.Frames.Single();
        vm.SelectOnly(bloc);
        Rendu.Battre();
        centre = Rendu.Centre(fenetre, bloc);
        Rendu.Glisser(fenetre, centre, centre + new Vector(-6, -40), RawInputModifiers.Shift | RawInputModifiers.Alt);
        Assert.Equal((150.0, 160.0), (bloc.X, bloc.Y));
        Assert.Equal([bloc], vm.Selection);
        Rendu.UnSeulPas(vm, avant, "Maj+glisser à la verticale, le bloc déjà pris");
    }

    /// <summary>
    /// L'axe tenu ne s'aimante pas : un bloc à 3 pt sous la marge du haut, mené à l'horizontale
    /// avec un point de dérive, garde son ordonnée sous Maj, et la marge l'attire sans Maj. Sur
    /// l'axe libre, l'aimantation joue encore.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Maj_glisser_n_aimante_pas_l_axe_tenu()
    {
        double Ordonnee(RawInputModifiers touches)
        {
            var bloc = Forme("Près de la marge", 150, 0);
            var document = Rendu.Feuille(bloc);
            bloc.Y = document.MarginTop + 3;
            var (fenetre, _) = Rendu.Ouvrir(document);
            var centre = Rendu.Centre(fenetre, bloc);
            Rendu.Glisser(fenetre, centre, centre + new Vector(40, 1), touches);
            Assert.Equal(190, bloc.X, precision: 9);
            return bloc.Y - document.MarginTop;
        }

        Assert.Equal(3, Ordonnee(RawInputModifiers.Shift), precision: 9);
        Assert.Equal(0, Ordonnee(RawInputModifiers.None), precision: 9);
    }

    // ------------------------------------------------------------------ le tracé

    /// <summary>
    /// La recette du plan : un tracé sous Maj a une largeur égale à sa hauteur — le côté est le
    /// plus grand des deux chemins —, au cadre d'aperçu comme au bloc posé, carré ou cercle ; sous
    /// Ctrl, la pression est le centre du cadre ; les deux ensemble, un carré centré.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Un_trace_sous_Maj_est_un_carre_et_sous_Ctrl_part_du_centre()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille());
        var page = vm.CurrentPage!;

        Frame Tracer(EditorTool outil, Point de, Point a, RawInputModifiers touches)
        {
            vm.ActiveTool = outil;
            Rendu.Battre();
            Rendu.Glisser(fenetre, Rendu.SurLaPage(fenetre, de.X, de.Y), Rendu.SurLaPage(fenetre, a.X, a.Y), touches);
            return page.Frames.Last();
        }

        var carre = Tracer(EditorTool.Rectangle, new(100, 100), new(180, 140), RawInputModifiers.Shift);
        Assert.Equal((100.0, 100.0, 80.0, 80.0), (carre.X, carre.Y, carre.Width, carre.Height));

        var cercle = Assert.IsType<ShapeFrame>(Tracer(EditorTool.Ellipse, new(400, 150), new(350, 120), RawInputModifiers.Shift));
        Assert.True(cercle.IsEllipse);
        Assert.Equal((350.0, 100.0, 50.0, 50.0), (cercle.X, cercle.Y, cercle.Width, cercle.Height));

        var centre = Tracer(EditorTool.Rectangle, new(300, 300), new(340, 330), RawInputModifiers.Control);
        Assert.Equal((260.0, 270.0, 80.0, 60.0), (centre.X, centre.Y, centre.Width, centre.Height));

        var carreCentre = Tracer(EditorTool.Rectangle, new(300, 500), new(340, 520), RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal((260.0, 460.0, 80.0, 80.0), (carreCentre.X, carreCentre.Y, carreCentre.Width, carreCentre.Height));

        // L'aperçu dit déjà le carré, pendant le geste.
        vm.ActiveTool = EditorTool.Rectangle;
        Rendu.Battre();
        var depart = Rendu.SurLaPage(fenetre, 100, 650);
        Rendu.Tenir(fenetre, depart, Rendu.SurLaPage(fenetre, 160, 670), RawInputModifiers.Shift);
        var apercu = fenetre.GetVisualDescendants().OfType<Rectangle>().Single(r => r.Classes.Contains("traceEnCours"));
        Assert.True(apercu.IsVisible);
        Assert.Equal(60, apercu.Width, precision: 9);
        Assert.Equal(60, apercu.Height, precision: 9);
        Rendu.Lacher(fenetre, Rendu.SurLaPage(fenetre, 160, 670), RawInputModifiers.Shift);
        Assert.Equal(60, page.Frames.Last().Height, precision: 9);
    }

    // ------------------------------------------------------------------ Ctrl sur une poignée

    /// <summary>
    /// La recette du plan : Ctrl sur une poignée garde le centre. Le coin sud-est tiré de
    /// (20, 10) élargit le bloc de 40 et le grandit de 20 ; le côté ouest tiré de 10 vers la
    /// gauche l'élargit de 20, sans toucher à la hauteur. Chaque geste fait son pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Ctrl_sur_une_poignee_redimensionne_depuis_le_centre()
    {
        var bloc = Forme("A", 100, 100, 100, 80);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var avant = Etat(vm);
        var coin = Rendu.Poignee(fenetre, bloc, 1, 1);
        Rendu.Glisser(fenetre, coin, coin + new Vector(20, 10), RawInputModifiers.Control);
        Assert.Equal((80.0, 90.0, 140.0, 100.0), (bloc.X, bloc.Y, bloc.Width, bloc.Height));
        Assert.Equal((150.0, 140.0), CentreDe(bloc));
        Rendu.UnSeulPas(vm, avant, "Ctrl sur le coin");

        bloc = (ShapeFrame)vm.CurrentPage!.Frames.Single();
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var cote = Rendu.Poignee(fenetre, bloc, 0, 0.5);
        Rendu.Glisser(fenetre, cote, cote + new Vector(-10, 0), RawInputModifiers.Control);
        Assert.Equal((90.0, 100.0, 120.0, 80.0), (bloc.X, bloc.Y, bloc.Width, bloc.Height));
        Rendu.UnSeulPas(vm, avant, "Ctrl sur le côté");
    }

    /// <summary>
    /// Depuis le centre aussi sur un bloc tourné, où la poignée tire dans le repère du bloc :
    /// le pivot reste où il était, et Maj y garde les proportions.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Ctrl_sur_une_poignee_d_un_bloc_tourne_garde_son_centre()
    {
        var bloc = Forme("Tourné", 200, 300, 120, 60);
        bloc.Rotation = 30;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var (cx, cy) = CentreDe(bloc);

        var ecart = 3.25 / vm.RenderScale;
        var (px, py) = bloc.ToPage(bloc.Width + ecart, bloc.Height + ecart);
        var prise = Rendu.SurLaPage(fenetre, px, py);
        var (ax, ay) = bloc.ToPage(bloc.Width + ecart + 30, bloc.Height + ecart + 10);
        Rendu.Glisser(fenetre, prise, Rendu.SurLaPage(fenetre, ax, ay), RawInputModifiers.Control | RawInputModifiers.Shift);

        var (nx, ny) = CentreDe(bloc);
        Assert.Equal(cx, nx, precision: 6);
        Assert.Equal(cy, ny, precision: 6);
        // Depuis un seul côté, 150 de large, ramenés à 140 par les proportions ; depuis le centre,
        // 180, ramenés à 160.
        Assert.Equal(160, bloc.Width, precision: 6);
        Assert.Equal(80, bloc.Height, precision: 6);
        Assert.Equal(30, bloc.Rotation, precision: 9);
    }

    // ------------------------------------------------------------------ le rectangle de sélection

    /// <summary>Trois blocs en ligne : le rectangle de (90, 290) à (270, 370) couvre A et B, et la moitié de C.</summary>
    private static (ShapeFrame A, ShapeFrame B, ShapeFrame C, Document Document) EnLigne()
    {
        var a = Forme("A", 100, 300, 60, 60);
        var b = Forme("B", 180, 300, 60, 60);
        var c = Forme("C", 240, 300, 60, 60);
        return (a, b, c, Rendu.Feuille(a, b, c));
    }

    /// <summary>
    /// La recette du plan : un rectangle prend deux blocs sur trois — ceux dont l'emprise est
    /// tout entière dedans —, tiré dans le vide de la page comme depuis le gris, à gauche de la
    /// page. Il ne change que la sélection.
    /// </summary>
    [AvaloniaTheory]
    [Trait("poste", "N17")]
    [InlineData(90.0)]
    [InlineData(-20.0)]
    public void Un_rectangle_prend_les_blocs_tout_entiers_dedans(double departX)
    {
        var (a, b, _, document) = EnLigne();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var avant = Etat(vm);

        Rendu.Glisser(fenetre, Rendu.SurLaPage(fenetre, departX, 290), Rendu.SurLaPage(fenetre, 270, 370));

        Assert.Equal([a, b], vm.Selection);
        RienNaChange(vm, avant, "un rectangle de sélection");
    }

    /// <summary>
    /// Tiré à rebours, de bas en haut et de droite à gauche, le rectangle prend de même ; un bloc
    /// masqué tout entier dedans n'est pas pris (poste I8) ; une pression sans glissé dans le
    /// vide vide la sélection, comme avant.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Un_rectangle_a_rebours_prend_de_meme_sauf_un_bloc_masque()
    {
        var (a, b, c, document) = EnLigne();
        var masque = Forme("Masqué", 110, 310, 20, 20);
        masque.Visible = false;
        document.Pages[0].Frames.Add(masque);
        var (fenetre, vm) = Rendu.Ouvrir(document);

        Rendu.Glisser(fenetre, Rendu.SurLaPage(fenetre, 270, 370), Rendu.SurLaPage(fenetre, 90, 290));
        Assert.Equal([a, b], vm.Selection);

        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 400, 500));
        Assert.Empty(vm.Selection);
        Assert.Contains(c, vm.CurrentPage!.Frames);
    }

    /// <summary>Sous Maj ou sous Ctrl, le rectangle ajoute ce qu'il prend à la sélection.</summary>
    [AvaloniaTheory]
    [Trait("poste", "N17")]
    [InlineData(RawInputModifiers.Shift)]
    [InlineData(RawInputModifiers.Control)]
    public void Maj_ou_Ctrl_ajoute_le_rectangle_a_la_selection(RawInputModifiers touche)
    {
        var (a, b, c, document) = EnLigne();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.SelectOnly(c);
        Rendu.Battre();

        Rendu.Glisser(fenetre, Rendu.SurLaPage(fenetre, 90, 290), Rendu.SurLaPage(fenetre, 245, 370), touche);

        Assert.Equal([c, a, b], vm.Selection);
    }

    /// <summary>
    /// Le rectangle se dessine dans l'Overlay, et non dans PageRoot (règle 4), aux pinceaux de
    /// l'apparence : l'accent pour le trait, l'accent à 14 % pour le fond. Il ne paraît qu'au-delà
    /// du seuil du glissé, et s'efface au relâché.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Le_rectangle_se_dessine_dans_l_overlay_aux_pinceaux_de_l_apparence()
    {
        var (_, _, _, document) = EnLigne();
        var (fenetre, _) = Rendu.Ouvrir(document);
        var depart = Rendu.SurLaPage(fenetre, 90, 290);

        Rectangle? Dessine() => fenetre.GetVisualDescendants().OfType<Rectangle>()
            .SingleOrDefault(r => r.Name == "RectangleDeSelection" && r.IsVisible);

        Rendu.Tenir(fenetre, depart, depart + new Vector(2, 0));
        Assert.Null(Dessine());
        Rendu.Lacher(fenetre, depart + new Vector(2, 0));

        Rendu.Tenir(fenetre, depart, depart + new Vector(100, 50));
        var rectangle = Dessine();
        Assert.NotNull(rectangle);
        Assert.Equal((100.0, 50.0), (rectangle.Width, rectangle.Height));
        Assert.IsType<Canvas>(rectangle.Parent);
        Assert.Equal("Overlay", ((Canvas)rectangle.Parent!).Name);
        Assert.DoesNotContain(rectangle, fenetre.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "PageRoot").GetVisualDescendants());
        Assert.True(fenetre.TryFindResource("Accent", out var accent));
        Assert.True(fenetre.TryFindResource("Projection", out var projection));
        Assert.Same(accent, rectangle.Stroke);
        Assert.Same(projection, rectangle.Fill);

        Rendu.Lacher(fenetre, depart + new Vector(100, 50));
        Assert.Null(Dessine());
    }

    // ------------------------------------------------------------------ Échap

    /// <summary>
    /// La recette du plan : Échap après un glissé rend le bloc à sa place, sans pas d'annulation ;
    /// la sélection reste, et la suite du geste jusqu'au relâché ne fait plus rien.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Echap_pendant_un_glisse_rend_le_bloc_a_sa_place_sans_pas()
    {
        var bloc = Forme("A", 150, 200);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);
        var centre = Rendu.Centre(fenetre, bloc);

        Rendu.Tenir(fenetre, centre, centre + new Vector(40, 30), RawInputModifiers.Alt);
        Assert.Equal((190.0, 230.0), (bloc.X, bloc.Y));

        Echap(fenetre);
        Assert.Equal((150.0, 200.0), (bloc.X, bloc.Y));
        Assert.Equal([bloc], vm.Selection);

        fenetre.MouseMove(centre + new Vector(80, 60), RawInputModifiers.LeftMouseButton);
        Rendu.Lacher(fenetre, centre + new Vector(80, 60));
        Assert.Equal((150.0, 200.0), (bloc.X, bloc.Y));
        Assert.Equal([bloc], vm.Selection);
        RienNaChange(vm, avant, "un glissé annulé par Échap");
    }

    /// <summary>
    /// Échap défait aussi une copie en cours — la copie s'en va, l'original est resélectionné —,
    /// un redimensionnement et une rotation, et efface le rectangle de sélection sans rien
    /// prendre. Aucun pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Echap_defait_une_copie_un_redimensionnement_une_rotation_et_un_rectangle()
    {
        var (a, b, c, document) = EnLigne();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var avant = Etat(vm);
        var page = vm.CurrentPage!;

        var centre = Rendu.Centre(fenetre, a);
        Rendu.Tenir(fenetre, centre, centre + new Vector(0, 120), RawInputModifiers.Control);
        Assert.Equal(4, page.Frames.Count);
        Echap(fenetre);
        Rendu.Lacher(fenetre, centre + new Vector(0, 120), RawInputModifiers.Control);
        Assert.Equal([a, b, c], page.Frames);
        Assert.Equal([a], vm.Selection);
        RienNaChange(vm, avant, "une copie annulée par Échap");

        var coin = Rendu.Poignee(fenetre, a, 1, 1);
        Rendu.Tenir(fenetre, coin, coin + new Vector(30, 30));
        Assert.NotEqual(60, a.Width);
        Echap(fenetre);
        Rendu.Lacher(fenetre, coin + new Vector(30, 30));
        Assert.Equal((100.0, 300.0, 60.0, 60.0), (a.X, a.Y, a.Width, a.Height));
        RienNaChange(vm, avant, "un redimensionnement annulé par Échap");

        var poignee = Rendu.CentreDeLaRotation(fenetre);
        var prise = Rendu.SurLaPage(fenetre, poignee.X, poignee.Y);
        Rendu.Tenir(fenetre, prise, Rendu.SurLaPage(fenetre, 300, 330));
        Assert.NotEqual(0, a.Rotation);
        Echap(fenetre);
        Rendu.Lacher(fenetre, Rendu.SurLaPage(fenetre, 300, 330));
        Assert.Equal(0, a.Rotation);
        RienNaChange(vm, avant, "une rotation annulée par Échap");

        // Loin de la poignée nord-ouest de A, qui est pris.
        var depart = Rendu.SurLaPage(fenetre, 80, 280);
        Rendu.Tenir(fenetre, depart, Rendu.SurLaPage(fenetre, 270, 370));
        Echap(fenetre);
        Rendu.Lacher(fenetre, Rendu.SurLaPage(fenetre, 270, 370));
        Assert.Empty(vm.Selection);
        Assert.DoesNotContain(fenetre.GetVisualDescendants().OfType<Rectangle>(), r => r.Name == "RectangleDeSelection" && r.IsVisible);
        RienNaChange(vm, avant, "un rectangle annulé par Échap");
    }

    // ------------------------------------------------------------------ le curseur de rotation

    /// <summary>
    /// Le curseur de la poignée de rotation n'est plus la main du jeu standard : il est dessiné,
    /// au tracé de l'icône de rotation de l'apparence — que la planche y trouve —, et ce n'est
    /// aucun des curseurs du jeu standard que la planche emploie. Le même à chaque survol. Son
    /// allure ne s'éprouve pas sans affichage (poste N1).
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N17")]
    public void Le_curseur_de_rotation_est_la_fleche_de_l_apparence()
    {
        var bloc = Forme("A", 150, 200);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var planche = fenetre.GetVisualDescendants().OfType<PageCanvas>().Single();
        var couche = fenetre.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "InputLayer");

        var trace = (Geometry)typeof(PageCanvas).GetMethod("TraceDeLIcone", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(planche, ["IconeRotation"])!;
        Assert.True(trace.Bounds.Width > 10 && trace.Bounds.Height > 10, $"l'icône de rotation n'a pas de tracé : {trace.Bounds}");

        var poignee = Rendu.CentreDeLaRotation(fenetre);
        var sur = Rendu.SurLaPage(fenetre, poignee.X, poignee.Y);
        Rendu.Survoler(fenetre, sur);
        var curseur = couche.Cursor;
        Assert.NotNull(curseur);

        var standards = typeof(PageCanvas).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .SelectMany(champ => champ.GetValue(null) switch
            {
                Cursor seul => new[] { seul },
                Cursor[] plusieurs => plusieurs,
                _ => Array.Empty<Cursor>(),
            })
            .ToList();
        Assert.True(standards.Count >= 10, $"{standards.Count} curseurs standard relus seulement");
        Assert.DoesNotContain(curseur, standards);

        Rendu.Survoler(fenetre, Rendu.Centre(fenetre, bloc));
        Assert.NotSame(curseur, couche.Cursor);
        Rendu.Survoler(fenetre, sur);
        Assert.Same(curseur, couche.Cursor);
    }
}
