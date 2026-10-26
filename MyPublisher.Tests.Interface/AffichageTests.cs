using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// L'affichage au clavier — poste N4 : <c>F9</c>, qui bascule entre l'affichage courant et
/// la taille réelle et rend l'affichage d'avant ; <c>Ctrl+Maj+L</c>, la page entière ; le
/// champ du zoom de la barre principale, où l'on tape un pourcentage ou choisit dans la
/// liste de Publisher ; et les deux bascules de Publisher, « Limites et repères »
/// (<c>Ctrl+Maj+O</c>) et « Repères de lignes de base » (<c>Ctrl+F7</c>).
///
/// Tout se joue au clavier et au pointeur sur la vraie fenêtre, et se mesure dans le repère
/// de la fenêtre. La recette du poste — <c>Scripts/recettes/N4-affichage-au-clavier.sh</c> —
/// joue ces tests par leur trait.
/// </summary>
[Trait("poste", "N4")]
public class AffichageTests
{
    // ------------------------------------------------------------------ outillage
    //
    // Les gestes sont ceux du banc (Rendu, poste N1). « MilieuDeLaZone » n'est pas celui de
    // NavigationTests : il lit le milieu de la vue qui défile, barres déduites, non des bornes.

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    /// <summary>Une entrée de menu : hors de l'arbre visuel tant que son menu est replié, elle est dans l'arbre logique.</summary>
    private static MenuItem Menu(Window fenetre, string nom) =>
        fenetre.GetLogicalDescendants().OfType<MenuItem>().FirstOrDefault(m => m.Name == nom)
        ?? throw new InvalidOperationException($"le menu n'a pas d'entrée « {nom} »");

    private static ScrollViewer Defilement(Window fenetre) => Trouver<ScrollViewer>(fenetre, "Scroller");

    /// <summary>Le milieu de la zone qui défile, dans la fenêtre.</summary>
    private static Point MilieuDeLaZone(Window fenetre)
    {
        var zone = Defilement(fenetre);
        return zone.TranslatePoint(new Point(zone.Viewport.Width / 2, zone.Viewport.Height / 2), fenetre)
               ?? throw new InvalidOperationException();
    }

    private static TextFrame Texte(double x, double y, double largeur, double hauteur) =>
        new() { Name = "Texte", X = x, Y = y, Width = largeur, Height = hauteur, Text = "Texte" };

    private static void PresDe(Point attendu, Point mesure, string quoi)
    {
        Assert.True(Math.Abs(attendu.X - mesure.X) <= 1 && Math.Abs(attendu.Y - mesure.Y) <= 1,
            $"{quoi} : le point était en {attendu}, il est en {mesure}");
    }

    /// <summary>Le zoom n'est pas le document : rien n'entre à l'historique, rien n'est « modifié », aucune transaction ne reste ouverte.</summary>
    private static void RienAuDocument(MainWindowViewModel vm, string avant, string quoi)
    {
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, $"{quoi} : un pas d'annulation est entré à l'historique");
        Assert.False(vm.IsDirty, $"{quoi} : le document est « modifié »");
        var profondeur = (int)typeof(MainWindowViewModel)
            .GetField("_editDepth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(vm)!;
        Assert.True(profondeur == 0, $"{quoi} : {profondeur} transaction(s) restée(s) ouverte(s)");
    }

    private static TextBox Champ(Window fenetre) => Trouver<TextBox>(fenetre, "SaisieDuZoom");

    private static Popup Liste(Window fenetre) => Trouver<Popup>(fenetre, "ListeDuZoom");

    private static Button Entree(Window fenetre, string libelle) =>
        (Liste(fenetre).Child ?? throw new InvalidOperationException("la liste du zoom est vide"))
        .GetVisualDescendants().OfType<Button>().FirstOrDefault(b => Equals(b.Content, libelle))
        ?? throw new InvalidOperationException($"la liste du zoom n'a pas d'entrée « {libelle} »");

    /// <summary>Le champ pris d'un clic, comme une main y entre : tout le texte pris, la liste déroulée.</summary>
    private static void EntrerDansLeChamp(Window fenetre)
    {
        var champ = Champ(fenetre);
        Rendu.Cliquer(champ);
        Assert.True(champ.IsFocused, "un clic dans le champ du zoom ne lui donne pas le focus");
        Assert.True(Liste(fenetre).IsOpen, "un clic dans le champ du zoom ne déroule pas la liste");
        Assert.Equal(champ.Text ?? "", champ.SelectedText);
    }

    // ------------------------------------------------------------------ F9

    [AvaloniaFact]
    public void F9_met_une_page_a_50_pour_cent_a_100_la_selection_sous_le_meme_point_et_le_second_rend_50()
    {
        // Au milieu de la largeur d'un A4 : la page, centrée à 50 %, l'est encore à 100 % si
        // elle ne déborde pas en largeur — le défilement ne peut rien pour un point décentré.
        var bloc = Texte(247.5, 460, 100, 80);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            vm.SelectOnly(bloc);
            vm.Zoom = 0.5;
            Rendu.Battre();
            var zone = Defilement(fenetre);
            var defilement = zone.Offset;
            var avant = DocumentSerializer.ToJson(vm.Document);
            var centre = Rendu.SurLaPage(fenetre, 297.5, 500);

            Rendu.Touche(fenetre, PhysicalKey.F9, relacher: true);
            Assert.Equal(MainWindowViewModel.TailleReelle, vm.Zoom, precision: 9);
            Assert.Equal("100 %", vm.ZoomLabel);
            Assert.True(zone.Extent.Height > zone.Viewport.Height, "à 100 %, l'A4 ne déborde pas : la mesure ne prouve rien");
            PresDe(centre, Rendu.SurLaPage(fenetre, 297.5, 500), "premier F9");
            Assert.Same(bloc, vm.SingleSelected);

            Rendu.Touche(fenetre, PhysicalKey.F9, relacher: true);
            Assert.Equal(0.5, vm.Zoom, precision: 9);
            Assert.Equal(defilement, zone.Offset);
            PresDe(centre, Rendu.SurLaPage(fenetre, 297.5, 500), "second F9");
            Assert.Same(bloc, vm.SingleSelected);

            RienAuDocument(vm, avant, "F9");
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Sans_selection_F9_garde_ce_que_montre_le_milieu_de_la_zone_et_le_second_rend_zoom_et_defilement()
    {
        // Une grande page, qui déborde de la zone dans les deux sens à 100 % comme à 200 % :
        // le défilement peut toujours ramener le point visé au milieu.
        var document = Rendu.Feuille(Texte(60, 60, 200, 80));
        document.PageWidth = 1400;
        document.PageHeight = 1800;
        var (fenetre, vm) = Rendu.Ouvrir(document);
        try
        {
            vm.Zoom = 2;
            Rendu.Battre();
            var zone = Defilement(fenetre);
            zone.Offset = new Vector(900, 1300);
            Rendu.Battre();
            var defilement = zone.Offset;
            var avant = DocumentSerializer.ToJson(vm.Document);
            var milieu = MilieuDeLaZone(fenetre);
            var vise = Rendu.SousLaFenetre(fenetre, milieu);

            Rendu.Touche(fenetre, PhysicalKey.F9, relacher: true);
            Assert.Equal(1, vm.Zoom, precision: 9);
            PresDe(milieu, Rendu.SurLaPage(fenetre, vise.X, vise.Y), "F9 sans sélection");
            Assert.Empty(vm.Selection);

            // Le second rend l'affichage d'avant tel qu'il était, défilement compris — même
            // quand on a fait défiler la planche entre les deux : zoomer autour du milieu de
            // la zone ne le rendrait plus.
            zone.Offset += new Vector(150, -120);
            Rendu.Battre();
            Rendu.Touche(fenetre, PhysicalKey.F9, relacher: true);
            Assert.Equal(2, vm.Zoom, precision: 9);
            Assert.Equal(defilement, zone.Offset);
            RienAuDocument(vm, avant, "F9 sans sélection");
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Un_autre_geste_qui_change_le_zoom_fait_oublier_l_affichage_d_avant()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte(60, 60, 200, 80)));
        try
        {
            vm.Zoom = 0.5;
            Rendu.Battre();
            Rendu.Touche(fenetre, PhysicalKey.F9, relacher: true);
            Assert.Equal(1, vm.Zoom, precision: 9);

            // Zoom avant, puis taille réelle par Ctrl+0 : on est à 100 %, mais ce n'est plus
            // F9 qui l'a posé. Le F9 qui suit n'a rien à rendre, et le dit.
            vm.ZoomInCommand.Execute(null);
            Rendu.Touche(fenetre, PhysicalKey.Digit0, RawInputModifiers.Control, relacher: true);
            Assert.Equal(1, vm.Zoom, precision: 9);
            Rendu.Touche(fenetre, PhysicalKey.F9, relacher: true);
            Assert.Equal(1, vm.Zoom, precision: 9);
            Assert.Equal("Déjà en taille réelle.", vm.Status);

            // Hors de la taille réelle, F9 repart de l'affichage courant.
            vm.ZoomOutCommand.Execute(null);
            Rendu.Battre();
            var zoom = vm.Zoom;
            Rendu.Touche(fenetre, PhysicalKey.F9, relacher: true);
            Assert.Equal(1, vm.Zoom, precision: 9);
            Rendu.Touche(fenetre, PhysicalKey.F9, relacher: true);
            Assert.Equal(zoom, vm.Zoom, precision: 9);
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ Ctrl+Maj+L

    [AvaloniaFact]
    public void Ctrl_Maj_L_donne_le_zoom_du_bouton_Page_entiere()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte(60, 60, 200, 80)));
        try
        {
            var avant = DocumentSerializer.ToJson(vm.Document);
            var depart = vm.Zoom;
            Rendu.Cliquer(Trouver<Button>(fenetre, "BoutonPageEntiere"));
            var duBouton = vm.Zoom;
            Assert.NotEqual(depart, duBouton);

            vm.Zoom = depart;
            Rendu.Battre();
            Rendu.Touche(fenetre, PhysicalKey.L, RawInputModifiers.Control | RawInputModifiers.Shift, relacher: true);
            Assert.Equal(duBouton, vm.Zoom, precision: 9);
            RienAuDocument(vm, avant, "Ctrl+Maj+L");
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ le champ du zoom

    [AvaloniaFact]
    public void Le_champ_du_zoom_a_l_allure_du_libelle_tant_qu_il_n_a_pas_le_focus()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte(60, 60, 200, 80)));
        try
        {
            var champ = Champ(fenetre);
            var bord = champ.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");

            // Le libellé de la maquette : 44 px, corps 12, centré, ni fond ni bord.
            Assert.Equal(44, champ.Bounds.Width);
            Assert.Equal(12, champ.FontSize);
            Assert.Equal(TextAlignment.Center, champ.TextAlignment);
            Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(bord.Background).Color);
            Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(bord.BorderBrush).Color);
            Assert.Equal(vm.ZoomLabel, champ.Text);
            Assert.False(Liste(fenetre).IsOpen);

            // Le zoom changé ailleurs s'y lit.
            vm.ZoomInCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal(vm.ZoomLabel, champ.Text);

            // Au focus, le bord d'accent des champs de l'apparence, sans que la place change.
            var place = champ.Bounds;
            EntrerDansLeChamp(fenetre);
            Assert.Equal(((ISolidColorBrush)fenetre.FindResource("Accent")!).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(bord.BorderBrush).Color);
            Assert.Equal(place, champ.Bounds);
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Cent_cinquante_puis_Entree_donne_150_pour_cent_et_mille_est_refuse_avec_un_message()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte(60, 60, 200, 80)));
        try
        {
            var champ = Champ(fenetre);
            var avant = DocumentSerializer.ToJson(vm.Document);

            EntrerDansLeChamp(fenetre);
            fenetre.KeyTextInput("150");
            Rendu.Battre();
            // La frappe ne zoome pas : « 15 », en route vers « 150 », n'est pas un zoom demandé.
            Assert.Equal(0.75, vm.Zoom, precision: 9);
            RienAuDocument(vm, avant, "la frappe dans le champ");
            Rendu.Touche(fenetre, PhysicalKey.Enter, relacher: true);
            Assert.Equal(1.5, vm.Zoom, precision: 9);
            Assert.Equal("150 %", champ.Text);
            Assert.False(champ.IsFocused, "Entrée ne rend pas la planche");
            Assert.False(Liste(fenetre).IsOpen, "Entrée ne replie pas la liste");

            // Hors des bornes : refusé, le zoom ne bouge pas, la barre d'état dit pourquoi.
            EntrerDansLeChamp(fenetre);
            fenetre.KeyTextInput("1000");
            Rendu.Touche(fenetre, PhysicalKey.Enter, relacher: true);
            Assert.Equal(1.5, vm.Zoom, precision: 9);
            Assert.Contains("1000", vm.Status);
            Assert.Contains("10 à 800 %", vm.Status);
            Assert.Equal("150 %", champ.Text);

            // Illisible : refusé de même.
            fenetre.KeyTextInput("abc");
            Rendu.Touche(fenetre, PhysicalKey.Enter, relacher: true);
            Assert.Equal(1.5, vm.Zoom, precision: 9);
            Assert.Contains("n'est pas un zoom", vm.Status);

            // À la virgule, avec le signe : pris.
            fenetre.KeyTextInput("66,5 %");
            Rendu.Touche(fenetre, PhysicalKey.Enter, relacher: true);
            Assert.Equal(0.665, vm.Zoom, precision: 9);

            RienAuDocument(vm, avant, "le champ du zoom");
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Echap_renonce_a_ce_qu_on_a_tape_et_rend_la_planche()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte(60, 60, 200, 80)));
        try
        {
            var champ = Champ(fenetre);
            EntrerDansLeChamp(fenetre);
            fenetre.KeyTextInput("300");
            Rendu.Battre();
            Assert.Equal("300", champ.Text);

            Rendu.Touche(fenetre, PhysicalKey.Escape, relacher: true);
            Assert.Equal(0.75, vm.Zoom, precision: 9);
            Assert.Equal("75 %", champ.Text);
            Assert.False(champ.IsFocused, "Échap ne rend pas la planche");
            Assert.False(Liste(fenetre).IsOpen, "Échap ne replie pas la liste");

            // La planche a la main : une lettre seule y arme un outil, et ne s'écrit pas au champ.
            Rendu.Touche(fenetre, PhysicalKey.Z, relacher: true);
            Assert.Equal(EditorTool.Zoom, vm.ActiveTool);
            Assert.Equal("75 %", champ.Text);
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void La_liste_du_zoom_propose_les_entrees_de_Publisher_et_chacune_se_choisit()
    {
        var bloc = Texte(200, 300, 120, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            var champ = Champ(fenetre);
            var avant = DocumentSerializer.ToJson(vm.Document);
            Assert.Equal(
                ["Page entière", "Objets sélectionnés", "400 %", "300 %", "200 %", "150 %", "100 %", "75 %", "66 %", "50 %", "33 %", "25 %", "10 %"],
                vm.ZoomsProposes);

            // Un pourcentage, d'un clic.
            EntrerDansLeChamp(fenetre);
            Rendu.Cliquer(Entree(fenetre, "200 %"));
            Assert.Equal(2, vm.Zoom, precision: 9);
            Assert.False(Liste(fenetre).IsOpen);
            Assert.False(champ.IsFocused);

            // Les flèches parcourent la liste, Entrée prend : de 200 % vers le bas, 150 %.
            EntrerDansLeChamp(fenetre);
            Rendu.Touche(fenetre, PhysicalKey.ArrowDown, relacher: true);
            Assert.Equal("150 %", champ.Text);
            Assert.Equal(2, vm.Zoom, precision: 9);
            Rendu.Touche(fenetre, PhysicalKey.Enter, relacher: true);
            Assert.Equal(1.5, vm.Zoom, precision: 9);

            // « Page entière » : le zoom du bouton.
            Rendu.Cliquer(Trouver<Button>(fenetre, "BoutonPageEntiere"));
            var duBouton = vm.Zoom;
            vm.Zoom = 3;
            Rendu.Battre();
            EntrerDansLeChamp(fenetre);
            Rendu.Cliquer(Entree(fenetre, "Page entière"));
            Assert.Equal(duBouton, vm.Zoom, precision: 9);

            // « Objets sélectionnés » sans sélection : rien, et la barre d'état le dit.
            EntrerDansLeChamp(fenetre);
            Rendu.Cliquer(Entree(fenetre, "Objets sélectionnés"));
            Assert.Equal(duBouton, vm.Zoom, precision: 9);
            Assert.Contains("Aucun bloc", vm.Status);

            // Avec un bloc : il tient dans la zone, un côté touchant à la marge de la planche,
            // et son centre est au milieu de la zone.
            vm.SelectOnly(bloc);
            Rendu.Battre();
            EntrerDansLeChamp(fenetre);
            Rendu.Cliquer(Entree(fenetre, "Objets sélectionnés"));
            var zone = Defilement(fenetre);
            var marge = Trouver<Border>(fenetre, "Feuille").Margin;
            var libreX = zone.Viewport.Width - marge.Left - marge.Right;
            var libreY = zone.Viewport.Height - marge.Top - marge.Bottom;
            var largeur = bloc.Width * vm.RenderScale;
            var hauteur = bloc.Height * vm.RenderScale;
            Assert.True(largeur <= libreX + 0.5 && hauteur <= libreY + 0.5,
                $"le bloc de {largeur} × {hauteur} ne tient pas dans {libreX} × {libreY}");
            Assert.True(Math.Abs(largeur - libreX) <= 1 || Math.Abs(hauteur - libreY) <= 1,
                $"aucun côté du bloc de {largeur} × {hauteur} ne touche la zone de {libreX} × {libreY}");
            PresDe(MilieuDeLaZone(fenetre), Rendu.SurLaPage(fenetre, 260, 330), "« Objets sélectionnés »");

            RienAuDocument(vm, avant, "la liste du zoom");
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ limites et repères

    [AvaloniaFact]
    public void Ctrl_F7_ne_masque_que_les_lignes_de_base_et_Ctrl_Maj_O_masque_limites_et_reperes()
    {
        var document = Rendu.Feuille(Texte(60, 60, 200, 80));
        document.RowCount = 3;
        document.RowSpacing = 12;
        document.ColumnCount = 2;
        document.BaselineSpacing = 12;
        var (fenetre, vm) = Rendu.Ouvrir(document);
        try
        {
            vm.ShowGuides = true;
            Rendu.Battre();
            var avant = DocumentSerializer.ToJson(vm.Document);
            var lignes = fenetre.GetVisualDescendants().OfType<ItemsControl>().Single(i => i.Classes.Contains("horizontalGuides"));
            var colonnes = fenetre.GetVisualDescendants().OfType<ItemsControl>().Single(i => i.Classes.Contains("verticalGuides"));
            var limites = Assert.IsType<Panel>(lignes.Parent);
            var menuLimites = Menu(fenetre, "MenuLimitesEtReperes");
            var menuBases = Menu(fenetre, "MenuLignesDeBase");

            Rectangle[] Traits(ItemsControl liste, bool deBase) => liste.GetVisualDescendants().OfType<Rectangle>()
                .Where(r => r.DataContext is GuideLine ligne && ligne.IsBaseline == deBase).ToArray();

            var bases = Traits(lignes, true);
            var traits = Traits(lignes, false);
            Assert.True(bases.Length > 10, $"{bases.Length} lignes de base dessinées");
            Assert.Equal(4, traits.Length);
            Assert.NotEmpty(colonnes.GetVisualDescendants().OfType<Rectangle>());
            Assert.All(bases, r => Assert.True(r.IsEffectivelyVisible));
            Assert.True(menuLimites.IsChecked && menuBases.IsChecked);

            // Ctrl+F7 : les lignes de base seules s'en vont ; marges, lignes et colonnes restent.
            Rendu.Touche(fenetre, PhysicalKey.F7, RawInputModifiers.Control, relacher: true);
            Assert.False(vm.ShowBaselines);
            Assert.True(vm.ShowGuides);
            Assert.All(bases, r => Assert.False(r.IsVisible));
            Assert.All(traits, r => Assert.True(r.IsEffectivelyVisible));
            Assert.True(limites.IsEffectivelyVisible);
            Assert.All(colonnes.GetVisualDescendants().OfType<Rectangle>(), r => Assert.True(r.IsEffectivelyVisible));
            Assert.False(menuBases.IsChecked);
            Assert.True(menuLimites.IsChecked);

            Rendu.Touche(fenetre, PhysicalKey.F7, RawInputModifiers.Control, relacher: true);
            Assert.True(vm.ShowBaselines);
            Assert.All(bases, r => Assert.True(r.IsEffectivelyVisible));

            // Ctrl+Maj+O : tout ce qui ne s'imprime pas s'en va, lignes de base comprises — la
            // case « Repères » d'avant —, et revient.
            Rendu.Touche(fenetre, PhysicalKey.O, RawInputModifiers.Control | RawInputModifiers.Shift, relacher: true);
            Assert.False(vm.ShowGuides);
            Assert.False(limites.IsEffectivelyVisible);
            Assert.False(Trouver<Canvas>(fenetre, "CoucheDesReperes").IsVisible);
            Assert.False(menuLimites.IsChecked);
            Assert.True(menuBases.IsChecked);

            Rendu.Touche(fenetre, PhysicalKey.O, RawInputModifiers.Control | RawInputModifiers.Shift, relacher: true);
            Assert.True(vm.ShowGuides);
            Assert.True(limites.IsEffectivelyVisible);
            Assert.All(bases, r => Assert.True(r.IsEffectivelyVisible));
            Assert.True(menuLimites.IsChecked);

            // Ni l'une ni l'autre n'entre au document, ni n'ouvre de document : Ctrl+Maj+O
            // n'est pas lu comme Ctrl+O.
            RienAuDocument(vm, avant, "les deux bascules");
            Assert.Equal("banc.mypub", vm.FilePath);
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Le_menu_Affichage_montre_ses_gestes()
    {
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(Texte(60, 60, 200, 80)));
        try
        {
            Assert.Equal(KeyGesture.Parse("Ctrl+Shift+O"), Menu(fenetre, "MenuLimitesEtReperes").InputGesture);
            Assert.Equal(KeyGesture.Parse("Ctrl+F7"), Menu(fenetre, "MenuLignesDeBase").InputGesture);
            Assert.Equal(KeyGesture.Parse("F9"), Menu(fenetre, "MenuBasculerLaTailleReelle").InputGesture);
        }
        finally
        {
            fenetre.Close();
        }
    }
}
