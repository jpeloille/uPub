using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les trois finitions du panneau et de la barre : l'ordre de superposition au clavier
/// et à la souris (A4), le format de page libre (A5), la taille du bloc dans la barre
/// d'état (A6).
///
/// Elles tiennent dans un seul fichier parce qu'elles tiennent dans les deux mêmes
/// fichiers de l'application, et que ce sont les mêmes gestes synthétisés qui les
/// éprouvent.
///
/// L'attribut « poste » n'est pas une décoration : c'est ce que la recette de chaque
/// poste filtre, et ce qui lui permet de compter les tests qu'elle a réellement joués.
/// Un filtre qui ne désignerait plus rien sortirait en 0, et la recette se dirait verte
/// sans avoir rien vu.
/// </summary>
public class PanneauEtBarreTests
{
    private static ShapeFrame Carre(string nom, double x, double y, double cote = 60) =>
        new() { Name = nom, X = x, Y = y, Width = cote, Height = cote, Fill = "#FFCC0000" };

    private static Panel Planche(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");

    /// <summary>Un point de la page, en points typographiques, dans le repère fenêtre.</summary>
    private static Point SurLaPage(Window fenetre, double x, double y)
    {
        var origine = Planche(fenetre).TranslatePoint(new Point(0, 0), fenetre) ?? new Point(0, 0);
        return new Point(origine.X + x, origine.Y + y);
    }

    private static T Trouver<T>(Window fenetre, string nom) where T : Control =>
        fenetre.GetVisualDescendants().OfType<T>().First(c => c.Name == nom);

    private static string[] Pile(MainWindowViewModel vm) =>
        vm.CurrentPage!.Frames.Select(bloc => bloc.Name).ToArray();

    // ------------------------------------------------------------------ A4 · clavier

    [AvaloniaFact]
    [Trait("poste", "A4")]
    public void Le_raccourci_porte_le_bloc_au_premier_plan_puis_a_larriere()
    {
        // Le clavier passe par « MainWindow.OnKeyDown », qui rend la main aux
        // KeyBindings mais intercepte au passage Ctrl+N, O, S, E et les touches d'outil
        // à une lettre. Éprouver la commande sans la touche ne dirait rien de ce
        // filtre-là : c'est la frappe qu'on synthétise.
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("Fond", 60, 60), Carre("Avant", 90, 90)));

        vm.SelectOnly(vm.CurrentPage!.Frames[0]);
        Rendu.Battre();

        fenetre.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Control | RawInputModifiers.Shift);
        Rendu.Battre();

        Assert.Equal(new[] { "Avant", "Fond" }, Pile(vm));
        // Le bloc reste pris : un raccourci sert à enchaîner, et une commande qui
        // lâche sa sélection oblige à recliquer entre deux frappes.
        Assert.Equal("Fond", vm.Selected?.Name);
        Assert.Single(vm.Selection);

        fenetre.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.Control | RawInputModifiers.Shift);
        Rendu.Battre();

        Assert.Equal(new[] { "Fond", "Avant" }, Pile(vm));
        Assert.Equal("Fond", vm.Selected?.Name);
    }

    [AvaloniaFact]
    [Trait("poste", "A4")]
    public void Ctrl_Maj_B_narme_pas_loutil_tableau()
    {
        // « B » seul arme l'outil tableau. Le raccourci d'arrière-plan le porte avec
        // Ctrl et Maj : la garde qui les sépare est « KeyModifiers.None », et rien ne
        // la rappelle à qui ajoutera la prochaine lettre.
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("Fond", 60, 60), Carre("Avant", 90, 90)));

        vm.SelectOnly(vm.CurrentPage!.Frames[1]);
        Rendu.Battre();

        fenetre.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.Control | RawInputModifiers.Shift);
        Rendu.Battre();

        Assert.Equal(EditorTool.Select, vm.ActiveTool);
        Assert.Equal(new[] { "Avant", "Fond" }, Pile(vm));
    }

    [AvaloniaFact]
    [Trait("poste", "A4")]
    public void Porter_au_premier_plan_garde_lordre_relatif_dun_ensemble()
    {
        // Le geste passe par des déplacements et non par des retraits suivis
        // d'insertions ; c'est ce qui lui conserve sa sélection. Reste à vérifier que
        // le changement de mécanique n'a pas retourné l'ensemble au passage.
        var (_, vm) = Rendu.Ouvrir(Rendu.Feuille(
            Carre("A", 20, 20), Carre("B", 40, 40), Carre("C", 60, 60), Carre("D", 80, 80)));

        var page = vm.CurrentPage!;
        vm.SetSelection([page.Frames[0], page.Frames[2]]);
        Rendu.Battre();

        vm.BringToFrontCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(new[] { "B", "D", "A", "C" }, Pile(vm));
        Assert.Equal(2, vm.Selection.Count);

        vm.SendToBackCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(new[] { "A", "C", "B", "D" }, Pile(vm));
        Assert.Equal(2, vm.Selection.Count);
    }

    // ------------------------------------------------------- A4 · glisser dans la liste

    /// <summary>Un point dans l'élément de rang donné de la liste des blocs, en fraction de sa hauteur.</summary>
    private static Point DansLaListe(Window fenetre, int rang, double fraction)
    {
        var liste = Trouver<ListBox>(fenetre, "ListeDesBlocs");
        var element = (Control)liste.ContainerFromIndex(rang)!;
        return element.TranslatePoint(
            new Point(element.Bounds.Width / 2, element.Bounds.Height * fraction), fenetre)!.Value;
    }

    [AvaloniaFact]
    [Trait("poste", "A4")]
    public void Tirer_un_bloc_dans_la_liste_le_reordonne_et_sannule()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(
            Carre("A", 20, 20), Carre("B", 40, 40), Carre("C", 60, 60)));

        Assert.Equal(new[] { "A", "B", "C" }, Pile(vm));

        var depart = DansLaListe(fenetre, 0, 0.5);
        var arrivee = DansLaListe(fenetre, 2, 0.8);

        // Le bouton est déclaré à chaque mouvement : c'est ce que fait une vraie
        // plate-forme, et le geste s'y fie pour ne pas réordonner la pile sous un
        // simple survol.
        const RawInputModifiers bouton = RawInputModifiers.LeftMouseButton;

        fenetre.MouseDown(depart, MouseButton.Left);
        fenetre.MouseMove(new Point(depart.X, depart.Y + 6), bouton);
        fenetre.MouseMove(arrivee, bouton);
        fenetre.MouseUp(arrivee, MouseButton.Left);
        Rendu.Battre();

        Assert.Equal(new[] { "B", "C", "A" }, Pile(vm));
        // Le bloc déplacé reste celui qu'on regarde, comme dans n'importe quelle liste
        // qu'on réordonne.
        Assert.Equal("A", vm.Selected?.Name);

        // La liste est hors du défilement du panneau de propriétés : sa transaction
        // d'annulation n'est ouverte par personne d'autre que le geste lui-même.
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();

        Assert.Equal(new[] { "A", "B", "C" }, Pile(vm));
    }

    [AvaloniaFact]
    [Trait("poste", "A4")]
    public void Un_clic_dans_la_liste_selectionne_sans_reordonner()
    {
        // Désigner un bloc ne doit rien remuer, quel que soit le tremblement du clic.
        //
        // Deux choses s'en chargent, et il a fallu saboter le seuil pour apprendre
        // laquelle fait quoi. Ce qui empêche la pile de bouger n'est PAS le seuil :
        // c'est l'arithmétique du rang d'insertion — un point visé qui reste dans le
        // même intervalle rend le rang de départ, et le déplacement est sans objet.
        // Un seuil ramené à zéro laisse donc l'ordre intact, et un test qui ne
        // regarderait que l'ordre le déclarerait sain.
        //
        // Ce que le seuil tient, c'est le geste : sans lui, le moindre frémissement
        // capture le pointeur et allume le trait d'insertion sous le doigt de quelqu'un
        // qui ne fait que cliquer. C'est cela qu'on vérifie, pendant le geste.
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(
            Carre("A", 20, 20), Carre("B", 40, 40), Carre("C", 60, 60)));

        var trait = Trouver<Rectangle>(fenetre, "TraitDInsertion");
        var point = DansLaListe(fenetre, 0, 0.5);

        fenetre.MouseDown(point, MouseButton.Left);
        fenetre.MouseMove(new Point(point.X, point.Y + 2), RawInputModifiers.LeftMouseButton);
        Rendu.Battre();

        Assert.False(trait.IsVisible);

        fenetre.MouseUp(new Point(point.X, point.Y + 2), MouseButton.Left);
        Rendu.Battre();

        Assert.Equal(new[] { "A", "B", "C" }, Pile(vm));
        Assert.Equal("A", vm.Selected?.Name);
        Assert.False(vm.CanUndo);
        Assert.False(trait.IsVisible);
    }

    // ------------------------------------------------------------------ A5 · formats

    [AvaloniaFact]
    [Trait("poste", "A5")]
    public void Une_dimension_libre_vide_le_selecteur_sans_effacer_la_page()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 20, 20)));
        Assert.Same(PageSize.A4, vm.SelectedPageSize);

        vm.Document.PageWidth = Units.MmToPoints(100);
        vm.Document.PageHeight = Units.MmToPoints(200);
        Rendu.Battre();

        // Le piège du poste, écrit dans la consigne : « Match » rend null pour un
        // format libre, et il ne faut surtout pas en conclure qu'il faut effacer les
        // dimensions. Ce sont elles qui font foi.
        Assert.Null(vm.SelectedPageSize);
        Assert.Null(Trouver<ComboBox>(fenetre, "FormatDePage").SelectedItem);
        Assert.Equal(Units.MmToPoints(100), vm.Document.PageWidth, precision: 9);
        Assert.Equal(Units.MmToPoints(200), vm.Document.PageHeight, precision: 9);

        // Et la planche a réellement changé de taille : un format libre n'est pas une
        // étiquette, c'est la page qu'on compose. À un pixel près, la mise en page
        // arrondissant au pixel entier ce que le modèle exprime au centième de point.
        Assert.True(Math.Abs(Planche(fenetre).Bounds.Width - Units.MmToPoints(100)) <= 1,
            $"la planche mesure {Planche(fenetre).Bounds.Width} pt de large");
        Assert.True(Math.Abs(Planche(fenetre).Bounds.Height - Units.MmToPoints(200)) <= 1,
            $"la planche mesure {Planche(fenetre).Bounds.Height} pt de haut");
    }

    [AvaloniaFact]
    [Trait("poste", "A5")]
    public void Un_format_du_catalogue_repose_ses_dimensions_en_un_seul_pas_dannulation()
    {
        // Poser un format écrit DEUX dimensions l'une après l'autre : entre les deux,
        // le document porte une largeur de B5 et une hauteur de format libre, et le
        // ré-accord du sélecteur rejouerait le geste qui vient de l'appeler. Un pas
        // d'annulation par geste est ce qui dit que la boucle est fermée.
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 20, 20)));

        vm.Document.PageWidth = Units.MmToPoints(100);
        vm.Document.PageHeight = Units.MmToPoints(200);
        Rendu.Battre();
        Assert.False(vm.CanUndo);   // une écriture directe n'ouvre aucune transaction

        vm.SelectedPageSize = PageSize.B5;
        Rendu.Battre();

        // 176 × 250 mm, la norme, et non « PageSize.B5.Width » : se comparer à la
        // constante qu'on vient de poser ne dit rien de sa valeur. C'est ce qui fait
        // que la recette du poste attrape aussi un format du catalogue mal mesuré, et
        // pas seulement un panneau qui ne l'appliquerait plus.
        Assert.Equal(176, Units.PointsToMm(vm.Document.PageWidth), precision: 1);
        Assert.Equal(250, Units.PointsToMm(vm.Document.PageHeight), precision: 1);
        Assert.Same(PageSize.B5, Trouver<ComboBox>(fenetre, "FormatDePage").SelectedItem);

        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();

        Assert.Equal(Units.MmToPoints(100), vm.Document.PageWidth, precision: 6);
        Assert.Null(vm.SelectedPageSize);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    [Trait("poste", "A5")]
    public void Le_selecteur_offre_les_neuf_formats_du_catalogue()
    {
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille());
        var formats = Trouver<ComboBox>(fenetre, "FormatDePage").ItemsSource!
            .Cast<PageSize>().Select(format => format.Name).ToArray();

        Assert.Equal(
            new[] { "A3", "A4", "A5", "B4", "B5", "Executive", "Letter", "Legal", "Tabloid" },
            formats);
    }

    // ------------------------------------------------------------- A6 · barre d'état

    [AvaloniaFact]
    [Trait("poste", "A6")]
    public void La_barre_annonce_la_position_et_la_taille_du_bloc_selectionne()
    {
        var bloc = Carre("A", 100, 100);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        var position = Trouver<TextBlock>(fenetre, "PositionDuBloc");
        var taille = Trouver<TextBlock>(fenetre, "TailleDuBloc");

        Assert.False(position.IsVisible);
        Assert.False(taille.IsVisible);

        vm.SelectOnly(bloc);
        Rendu.Battre();

        Assert.True(taille.IsVisible);
        Assert.Contains(Millimetres(100), position.Text);
        Assert.Contains(Millimetres(60), taille.Text);
    }

    [AvaloniaFact]
    [Trait("poste", "A6")]
    public void La_taille_suit_le_geste_de_redimensionnement_et_pas_seulement_sa_fin()
    {
        var bloc = Carre("A", 100, 100);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        // Le bloc est pris AVANT la première image : sans cela la case passerait de
        // vide à remplie, et la différence prouverait la sélection, pas le geste.
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var taille = Trouver<TextBlock>(fenetre, "TailleDuBloc");
        Assert.Contains(Millimetres(60), taille.Text);
        var avant = Rendu.Bande(fenetre, taille);

        // Poignée sud-est, tirée de 60 pt. Le bouton n'est PAS relâché : c'est
        // précisément pendant le geste que la mesure doit se lire.
        fenetre.MouseDown(SurLaPage(fenetre, 160, 160), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 220, 220), RawInputModifiers.LeftMouseButton);
        Rendu.Battre();

        Assert.Equal(120, bloc.Width, precision: 6);
        Assert.Contains(Millimetres(120), taille.Text);

        // Et l'écran a réellement suivi. « CaptureRenderedFrame » et non
        // « RenderTargetBitmap.Render » : le second redessine de force et passerait
        // même si plus rien ne se rafraîchissait.
        Assert.NotEqual(avant, Rendu.Bande(fenetre, taille));

        fenetre.MouseUp(SurLaPage(fenetre, 220, 220), MouseButton.Left);
    }

    [AvaloniaFact]
    [Trait("poste", "A6")]
    public void La_position_suit_le_geste_de_deplacement()
    {
        var bloc = Carre("A", 100, 100);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        vm.SelectOnly(bloc);
        Rendu.Battre();

        var position = Trouver<TextBlock>(fenetre, "PositionDuBloc");
        var avant = Rendu.Bande(fenetre, position);

        // Alt écarte l'aimantation : ce test mesure la barre d'état, pas les repères.
        fenetre.MouseDown(SurLaPage(fenetre, 130, 130), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 230, 190),
            RawInputModifiers.LeftMouseButton | RawInputModifiers.Alt);
        Rendu.Battre();

        Assert.Equal(200, bloc.X, precision: 6);
        Assert.Contains(Millimetres(200), position.Text);
        Assert.NotEqual(avant, Rendu.Bande(fenetre, position));

        fenetre.MouseUp(SurLaPage(fenetre, 230, 190), MouseButton.Left);
    }

    [AvaloniaFact]
    [Trait("poste", "A6")]
    public void A_plusieurs_blocs_la_barre_annonce_lempreinte_de_lensemble()
    {
        // Choix délibéré, et il se voit : à un bloc c'est son cadre — les quatre
        // nombres que le panneau donne à saisir —, à plusieurs c'est ce que la
        // sélection occupe, faute d'un cadre commun à nommer.
        //
        // Et c'est le cas de figure qui garde la ligne de « NotifySelection » : à
        // plusieurs blocs le panneau de propriétés ne montre aucun champ, donc aucune
        // géométrie n'est réécrite, donc l'abonnement aux blocs ne dit rien. Seule la
        // liste écrite à la main de « NotifySelection » peut alors rafraîchir la barre.
        // Interroger le modèle de vue plutôt que la case ne prouverait rien : une
        // propriété calculée rend la bonne valeur même quand personne n'a été prévenu —
        // la première écriture de ce test s'y est laissé prendre.
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 100, 100), Carre("B", 200, 300)));

        var page = vm.CurrentPage!;
        vm.SetSelection([page.Frames[0], page.Frames[1]]);
        Rendu.Battre();

        Assert.Contains(Millimetres(100), Trouver<TextBlock>(fenetre, "PositionDuBloc").Text);
        // De 100 à 260 en abscisse, de 100 à 360 en ordonnée.
        var taille = Trouver<TextBlock>(fenetre, "TailleDuBloc").Text;
        Assert.Contains(Millimetres(160), taille);
        Assert.Contains(Millimetres(260), taille);
    }

    /// <summary>
    /// Des points en millimètres, écrits comme la barre d'état les écrit. On ne fige
    /// pas « 21,2 » : le séparateur décimal suit la langue de la machine, et les
    /// recettes tournent sous « LC_ALL=C ».
    /// </summary>
    private static string Millimetres(double points) => $"{Units.PointsToMm(points):0.0}";
}
