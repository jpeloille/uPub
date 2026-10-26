using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// L'objet trait, et la flèche — poste A10, sur la vraie fenêtre : l'outil Ligne du rail et
/// sa touche, le tracé contraint sous <c>Maj</c>, les deux poignées d'extrémité, le survol à
/// trois points, le dock — extrémités, pointes, contour —, l'encre à l'écran et les
/// vignettes des pages. Le noyau est éprouvé par <c>MyPublisher.Tests/TraitTests.cs</c>, le
/// PDF et l'import par la recette <c>Scripts/recettes/A10-trait.sh</c>, qui joue ces tests
/// par leur trait.
/// </summary>
[Trait("poste", "A10")]
public class TraitTests
{
    // ------------------------------------------------------------------ outillage

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static ShapeFrame Trait(double x1, double y1, double x2, double y2, double epaisseur = 2)
    {
        var trait = new ShapeFrame
        {
            Name = "Trait", Shape = ShapeKind.Line, Fill = null, Stroke = "#FF000000", StrokeThickness = epaisseur,
        };
        trait.PoserLesExtremites(x1, y1, x2, y2);
        return trait;
    }

    /// <summary>Les poignées visibles de l'Overlay, leur centre en points de page.</summary>
    private static Point[] Poignees(Window fenetre)
    {
        var overlay = Trouver<Canvas>(fenetre, "Overlay");
        return overlay.Children.OfType<Shape>()
            .Where(s => s.IsVisible && s.Classes.Contains("poignee"))
            .Select(s => new Point(Canvas.GetLeft(s) + s.Width / 2, Canvas.GetTop(s) + s.Height / 2))
            .ToArray();
    }

    // ------------------------------------------------------------------ l'outil Ligne

    [AvaloniaFact]
    public void Le_rail_porte_Ligne_entre_Ellipse_et_la_loupe_et_L_l_arme()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille());

        var outil = Trouver<ToggleButton>(fenetre, "OutilLigne");
        Assert.Equal("Ligne (L)", ToolTip.GetTip(outil));
        // L'icône de l'apparence (I1), créée à chaque emploi : la recette I1 vérifie qu'elle sert.
        Assert.IsType<Viewbox>(outil.Content);

        fenetre.KeyPressQwerty(PhysicalKey.L, RawInputModifiers.None);
        Rendu.Battre();
        Assert.Equal(EditorTool.Line, vm.ActiveTool);
        Assert.True(outil.IsChecked);
        Assert.True(vm.IsDrawing);

        // Échap le désarme, comme tout outil.
        fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Rendu.Battre();
        Assert.Equal(EditorTool.Select, vm.ActiveTool);
    }

    [AvaloniaFact]
    public void Un_trait_trace_va_dune_extremite_a_l_autre_en_un_pas_et_Maj_le_tient_a_l_horizontale()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille());
        var avant = DocumentSerializer.ToJson(vm.Document);

        vm.ActiveTool = EditorTool.Line;
        Rendu.Battre();

        // Pendant le tracé : l'aperçu est un trait, et la bulle dit la longueur et l'angle.
        fenetre.MouseDown(Rendu.SurLaPage(fenetre, 100, 300), MouseButton.Left);
        fenetre.MouseMove(Rendu.SurLaPage(fenetre, 300, 307), RawInputModifiers.LeftMouseButton | RawInputModifiers.Shift);
        Rendu.Battre();
        var apercu = Trouver<Canvas>(fenetre, "Overlay").Children.OfType<Line>().Single(l => l.Classes.Contains("traitEnCours"));
        Assert.True(apercu.IsVisible);
        Assert.Equal(apercu.StartPoint.Y, apercu.EndPoint.Y, precision: 6);
        var bulle = Trouver<Canvas>(fenetre, "Overlay").Children.OfType<Border>().Single(b => b.Classes.Contains("bulleDeDimensions"));
        Assert.Equal($"L {Units.PointsToMm(200):0.0} mm · 0°", Assert.IsType<TextBlock>(bulle.Child).Text);

        fenetre.MouseUp(Rendu.SurLaPage(fenetre, 300, 307), MouseButton.Left, RawInputModifiers.Shift);
        Rendu.Battre();

        var trait = Assert.IsType<ShapeFrame>(vm.CurrentPage!.Frames.Single());
        Assert.True(trait.IsLine);
        Assert.Equal(100, trait.Debut.X, precision: 6);
        Assert.Equal(300, trait.Debut.Y, precision: 6);
        Assert.Equal(300, trait.Fin.X, precision: 6);
        Assert.Equal(300, trait.Fin.Y, precision: 6);
        Assert.Equal(0, trait.Height);
        Assert.Null(trait.Fill);
        Assert.False(apercu.IsVisible);
        Assert.False(bulle.IsVisible);
        Assert.Same(trait, vm.SingleSelected);
        Assert.Equal(EditorTool.Select, vm.ActiveTool);

        Rendu.UnSeulPas(vm, avant, "le tracé d'un trait");
    }

    [AvaloniaFact]
    public void Sans_Maj_le_trait_suit_le_pointeur_et_un_simple_clic_le_pose_a_plat()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille());

        vm.ActiveTool = EditorTool.Line;
        Rendu.Glisser(fenetre, Rendu.SurLaPage(fenetre, 300, 400), Rendu.SurLaPage(fenetre, 200, 300));
        var oblique = Assert.IsType<ShapeFrame>(vm.CurrentPage!.Frames.Single());
        Assert.Equal(225, oblique.Rotation, precision: 6);
        Assert.Equal(300, oblique.Debut.X, precision: 6);
        Assert.Equal(300, oblique.Fin.Y, precision: 6);

        // Un clic : la longueur par défaut, à plat depuis le point de départ.
        vm.ActiveTool = EditorTool.Line;
        fenetre.MouseDown(Rendu.SurLaPage(fenetre, 50, 600), MouseButton.Left);
        fenetre.MouseUp(Rendu.SurLaPage(fenetre, 50, 600), MouseButton.Left);
        Rendu.Battre();
        var pose = Assert.IsType<ShapeFrame>(vm.CurrentPage.Frames[1]);
        Assert.Equal((50.0, 600.0), pose.Debut);
        Assert.Equal((50 + MainWindowViewModel.LongueurDuTraitParDefaut, 600.0), pose.Fin);
    }

    [AvaloniaFact]
    public void Le_menu_Insertion_pose_un_trait()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille());

        var menu = fenetre.GetLogicalDescendants().OfType<MenuItem>().Single(m => Equals(m.Header, "Tr_ait"));
        Assert.Same(vm.AddLineCommand, menu.Command);
        vm.AddLineCommand.Execute(null);
        Rendu.Battre();

        var trait = Assert.IsType<ShapeFrame>(vm.CurrentPage!.Frames.Single());
        Assert.True(trait.IsLine);
        Assert.Equal(MainWindowViewModel.LongueurDuTraitParDefaut, trait.Width, precision: 6);
        Assert.Equal("Trait", vm.Pastille);
        Assert.Same(trait, vm.SingleSelected);
    }

    // ------------------------------------------------------------------ survol et poignées

    [AvaloniaFact]
    public void Le_survol_prend_le_trait_a_moins_de_trois_points_et_plus_au_dela()
    {
        var trait = Trait(100, 300, 400, 300, epaisseur: 1);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(trait));
        var couche = Trouver<Border>(fenetre, "InputLayer");

        // À 4 pt de l'axe, soit 3,5 de l'encre : rien, ni curseur, ni sélection.
        fenetre.MouseMove(Rendu.SurLaPage(fenetre, 250, 304));
        Rendu.Battre();
        Assert.NotSame(CurseurDeDeplacement, couche.Cursor);
        fenetre.MouseDown(Rendu.SurLaPage(fenetre, 250, 304), MouseButton.Left);
        fenetre.MouseUp(Rendu.SurLaPage(fenetre, 250, 304), MouseButton.Left);
        Rendu.Battre();
        Assert.Null(vm.SingleSelected);

        // À 3 pt de l'axe, 2,5 de l'encre : le curseur de déplacement, et le clic le prend.
        fenetre.MouseMove(Rendu.SurLaPage(fenetre, 250, 303));
        Rendu.Battre();
        Assert.Same(CurseurDeDeplacement, couche.Cursor);
        fenetre.MouseDown(Rendu.SurLaPage(fenetre, 250, 303), MouseButton.Left);
        fenetre.MouseUp(Rendu.SurLaPage(fenetre, 250, 303), MouseButton.Left);
        Rendu.Battre();
        Assert.Same(trait, vm.SingleSelected);
    }

    /// <summary>Le curseur que la planche pose sur un bloc qu'on peut saisir — le sien, et non un égal.</summary>
    private static Cursor CurseurDeDeplacement =>
        (Cursor)typeof(PageCanvas).GetField("MoveCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;

    [AvaloniaFact]
    public void Un_trait_pris_na_que_ses_deux_poignees_d_extremite_et_pas_de_rotation()
    {
        var trait = Trait(100, 300, 300, 500);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(trait));
        vm.SelectOnly(trait);
        Rendu.Battre();

        var poignees = Poignees(fenetre);
        Assert.Equal(2, poignees.Length);
        Assert.Contains(poignees, p => Math.Abs(p.X - 100) < 0.01 && Math.Abs(p.Y - 300) < 0.01);
        Assert.Contains(poignees, p => Math.Abs(p.X - 300) < 0.01 && Math.Abs(p.Y - 500) < 0.01);
        Assert.False(Trouver<Canvas>(fenetre, "Overlay").Children.OfType<Polyline>().Single().IsVisible);

        // Un rectangle, lui, garde ses neuf poignées.
        var forme = new ShapeFrame { X = 350, Y = 100, Width = 100, Height = 60 };
        vm.CurrentPage!.Frames.Add(forme);
        vm.SelectOnly(forme);
        Rendu.Battre();
        Assert.Equal(9, Poignees(fenetre).Length);
    }

    [AvaloniaFact]
    public void Tirer_une_extremite_tourne_le_trait_autour_de_l_autre_et_Maj_le_tient_a_45_degres()
    {
        var trait = Trait(100, 300, 300, 300);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(trait));
        vm.SelectOnly(trait);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        // La fin, menée près de la diagonale descendante, sous Maj.
        Rendu.Glisser(fenetre, Rendu.SurLaPage(fenetre, 300, 300), Rendu.SurLaPage(fenetre, 295, 497), RawInputModifiers.Shift);

        Assert.Equal(100, trait.Debut.X, precision: 6);
        Assert.Equal(300, trait.Debut.Y, precision: 6);
        Assert.Equal(45, trait.Rotation, precision: 6);
        Assert.Equal(trait.Fin.X - 100, trait.Fin.Y - 300, precision: 6);
        Assert.Equal(0, trait.Height);
        Rendu.UnSeulPas(vm, avant, "une extrémité tirée");

        // L'annulation relit le document : le trait est une autre instance, toujours prise.
        trait = Assert.IsType<ShapeFrame>(vm.SingleSelected);

        // Le début, sans Maj : il suit le pointeur, la fin reste.
        Rendu.Glisser(fenetre, Rendu.SurLaPage(fenetre, 100, 300), Rendu.SurLaPage(fenetre, 130, 250), RawInputModifiers.Alt);
        Assert.Equal(130, trait.Debut.X, precision: 6);
        Assert.Equal(250, trait.Debut.Y, precision: 6);
        Assert.Equal(300, trait.Fin.X, precision: 6);
        Assert.Equal(300, trait.Fin.Y, precision: 6);
        // Un trait raccourci sous la taille minimale d'un bloc le reste : il n'a pas de cadre.
        Rendu.Glisser(fenetre, Rendu.SurLaPage(fenetre, 130, 250), Rendu.SurLaPage(fenetre, 299, 300), RawInputModifiers.Alt);
        Assert.True(trait.Width < Snap.MinFrameSize, $"trait de {trait.Width} pt");
    }

    [AvaloniaFact]
    public void Un_clic_sur_une_extremite_ne_change_rien()
    {
        var trait = Trait(100.3, 300.7, 251.9, 412.1);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(trait));
        vm.SelectOnly(trait);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        var prise = Rendu.SurLaPage(fenetre, trait.Fin.X, trait.Fin.Y);
        fenetre.MouseDown(prise, MouseButton.Left);
        fenetre.MouseMove(prise, RawInputModifiers.LeftMouseButton);
        fenetre.MouseUp(prise, MouseButton.Left);
        Rendu.Battre();

        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
    }

    // ------------------------------------------------------------------ le dock

    [AvaloniaFact]
    public void Le_dock_donne_les_extremites_a_la_place_du_cadre_et_ne_touche_pas_le_trait_en_le_montrant()
    {
        var trait = Trait(100, 300, 300, 300);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(trait));
        var avant = DocumentSerializer.ToJson(vm.Document);

        vm.SelectOnly(trait);
        Rendu.Battre();

        // Montrer le trait ne l'a pas changé : le champ H, borné à 2 mm, n'est pas là pour
        // lui en donner la hauteur.
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);

        bool Montre(string nom) => Trouver<Control>(fenetre, nom).IsEffectivelyVisible;
        foreach (var nom in new[] { "ChampDebutX", "ChampDebutY", "ChampFinX", "ChampFinY", "ChampAngle", "PointeAuDebut", "PointeALaFin", "EpaisseurDuContour" })
        {
            Assert.True(Montre(nom), $"{nom} manque au dock d'un trait");
        }

        foreach (var nom in new[] { "ChampX", "ChampY", "ChampLargeur", "ChampHauteur", "ConserverLesProportions", "SorteDeFond" })
        {
            Assert.False(Montre(nom), $"{nom} est montré pour un trait");
        }

        // Un contrôle par propriété (règle 1).
        Assert.Single(fenetre.GetVisualDescendants().OfType<CheckBox>(), c => c.Name == "PointeALaFin");

        // La fin, saisie au dock : un pas d'annulation, le début ne bouge pas.
        Rendu.Saisir(fenetre, Trouver<NumericUpDown>(fenetre, "ChampFinY"), Units.PointsToMm(500).ToString("0.###"));
        Assert.Equal(500, trait.Fin.Y, precision: 2);
        Assert.Equal(300, trait.Fin.X, precision: 2);
        Assert.Equal((100.0, 300.0), (Math.Round(trait.Debut.X, 6), Math.Round(trait.Debut.Y, 6)));
        Assert.Equal(0, trait.Height);
        Rendu.UnSeulPas(vm, avant, "une extrémité saisie au dock");

        // Un rectangle retrouve ses champs.
        var forme = new ShapeFrame { X = 350, Y = 100, Width = 100, Height = 60 };
        vm.CurrentPage!.Frames.Add(forme);
        vm.SelectOnly(forme);
        Rendu.Battre();
        Assert.True(Montre("ChampHauteur") && Montre("SorteDeFond"));
        Assert.False(Montre("ChampFinX"));
    }

    [AvaloniaFact]
    public void Une_pointe_cochee_au_dock_se_dessine_en_un_pas()
    {
        var trait = Trait(100, 300, 300, 300);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(trait));
        vm.SelectOnly(trait);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        // Sans pointe : rien à 5 pt au-dessus de la fin, où la pointe s'élargit.
        var image = Rendu.Encre(fenetre);
        Assert.False(Rendu.EstEncre(image, (int)(296 * Rendu.PixelsParPoint), (int)(296 * Rendu.PixelsParPoint)));

        var case_ = Trouver<CheckBox>(fenetre, "PointeALaFin");
        var centre = case_.TranslatePoint(new Point(8, case_.Bounds.Height / 2), fenetre)!.Value;
        fenetre.MouseDown(centre, MouseButton.Left);
        fenetre.MouseUp(centre, MouseButton.Left);
        Rendu.Battre();
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();

        Assert.True(trait.EndArrow);
        Assert.False(trait.StartArrow);
        image = Rendu.Encre(fenetre);
        // Une pointe de 6 pt de long et 3 de demi-largeur pour un trait de 2 pt : à 4 pt du
        // bout, elle couvre ±2 pt autour de l'axe. Le début, lui, reste nu.
        Assert.True(Rendu.EstEncre(image, (int)(296 * Rendu.PixelsParPoint), (int)(298.3 * Rendu.PixelsParPoint)));
        Assert.True(Rendu.EstEncre(image, (int)(296 * Rendu.PixelsParPoint), (int)(301.7 * Rendu.PixelsParPoint)));
        Assert.False(Rendu.EstEncre(image, (int)(104 * Rendu.PixelsParPoint), (int)(298.3 * Rendu.PixelsParPoint)));
        Rendu.UnSeulPas(vm, avant, "une pointe cochée");
    }

    [AvaloniaFact]
    public void Le_contour_du_dock_regle_l_epaisseur_et_l_angle_tourne_le_trait_autour_de_son_milieu()
    {
        var trait = Trait(100, 300, 300, 300);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(trait));
        vm.SelectOnly(trait);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        // L'épaisseur du contour est celle du trait, en un pas.
        Rendu.Saisir(fenetre, Trouver<NumericUpDown>(fenetre, "EpaisseurDuContour"), "5");
        Assert.Equal(5, trait.StrokeThickness);
        var image = Rendu.Encre(fenetre);
        var px = Rendu.PixelsParPoint;
        Assert.Equal(5 * px, Enumerable.Range((int)(290 * px), (int)(20 * px)).Count(y => Rendu.EstEncre(image, (int)(200 * px), y)));
        Rendu.UnSeulPas(vm, avant, "l'épaisseur du contour");

        // L'angle tourne le trait autour de son milieu, et les extrémités du dock le suivent.
        trait = Assert.IsType<ShapeFrame>(vm.SingleSelected);
        Rendu.Saisir(fenetre, Trouver<NumericUpDown>(fenetre, "ChampAngle"), "90");
        Assert.Equal(200, trait.Debut.X, precision: 6);
        Assert.Equal(200, trait.Debut.Y, precision: 6);
        Assert.Equal(200, trait.Fin.X, precision: 6);
        Assert.Equal(400, trait.Fin.Y, precision: 6);
        Assert.Equal(200, vm.DebutDuTraitX, precision: 6);
        Assert.Equal(400, vm.FinDuTraitY, precision: 6);
        Rendu.UnSeulPas(vm, avant, "l'angle du trait");
    }

    // ------------------------------------------------------------------ l'encre

    [AvaloniaFact]
    public void A_l_ecran_le_trait_tourne_tombe_entre_ses_extremites_a_son_epaisseur()
    {
        var horizontal = Trait(100, 200, 300, 200, epaisseur: 4);
        var vertical = Trait(400, 100, 400, 300, epaisseur: 2);
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(horizontal, vertical));
        var image = Rendu.Encre(fenetre);
        var px = Rendu.PixelsParPoint;

        // L'horizontal : 4 pt d'épaisseur, de 198 à 202, et ses bouts carrés s'arrêtent sur
        // les extrémités.
        var encrees = Enumerable.Range((int)(190 * px), (int)(20 * px)).Count(y => Rendu.EstEncre(image, (int)(200 * px), y));
        Assert.Equal(4 * px, encrees);
        Assert.True(Rendu.EstEncre(image, (int)(101 * px), (int)(200 * px)));
        Assert.False(Rendu.EstEncre(image, (int)(99 * px), (int)(200 * px)));
        Assert.False(Rendu.EstEncre(image, (int)(301 * px), (int)(200 * px)));

        // Le vertical, tracé couché puis tourné d'un quart de tour autour de son milieu.
        encrees = Enumerable.Range((int)(390 * px), (int)(20 * px)).Count(x => Rendu.EstEncre(image, x, (int)(200 * px)));
        Assert.Equal(2 * px, encrees);
        Assert.True(Rendu.EstEncre(image, (int)(400 * px), (int)(101 * px)));
        Assert.False(Rendu.EstEncre(image, (int)(400 * px), (int)(99 * px)));
        Assert.False(Rendu.EstEncre(image, (int)(400 * px), (int)(301 * px)));
    }

    [AvaloniaFact]
    public void La_vignette_de_la_page_montre_le_trait_et_le_suit()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille());
        var vignette = fenetre.GetVisualDescendants().OfType<VignetteDePage>().Single();
        var vide = Rendu.Bande(fenetre, vignette);

        var trait = Trait(60, 400, 540, 400, epaisseur: 6);
        vm.CurrentPage!.Frames.Add(trait);
        Rendu.Battre();
        var avecLeTrait = Rendu.Bande(fenetre, vignette);
        Assert.NotEqual(vide, avecLeTrait);

        // Une extrémité tirée se voit dans la vignette, dès la retouche.
        trait.PoserLesExtremites(60, 400, 540, 700);
        Rendu.Battre();
        Assert.NotEqual(avecLeTrait, Rendu.Bande(fenetre, vignette));

        // Le trait ôté, la vignette redevient vide, au pixel.
        vm.CurrentPage.Frames.Remove(trait);
        Rendu.Battre();
        Assert.Equal(vide, Rendu.Bande(fenetre, vignette));
    }
}
