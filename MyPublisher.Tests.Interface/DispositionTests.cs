using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MyPublisher.Converters;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// La disposition de la fenêtre — poste I2, avec B7 : la fenêtre de la maquette, de haut
/// en bas, et les panneaux qui se replient.
///
/// Les tailles attendues sont celles que la maquette donne et que la recette I2 rappelle :
/// une barre de 34 px et son filet, une de 44, la place de la barre contextuelle de 40, un
/// rail de 46, un panneau Pages de 168, un dock de 292, une barre d'état de 26. Les
/// couleurs sont lues dans l'apparence (I1), qui les tient sous leur nom ; c'est sa
/// recette qui vérifie qu'elles sont celles de la maquette.
/// </summary>
[Trait("poste", "I2")]
public class DispositionTests
{
    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static Panel Planche(Window fenetre) => Trouver<Panel>(fenetre, "PageRoot");

    private static Point Origine(Window fenetre) =>
        Planche(fenetre).TranslatePoint(new Point(0, 0), fenetre) ?? throw new InvalidOperationException();

    private static Point SurLaPage(Window fenetre, double x, double y)
    {
        var origine = Origine(fenetre);
        return new Point(origine.X + x, origine.Y + y);
    }

    /// <summary>La boîte d'un contrôle dans le repère de la fenêtre.</summary>
    private static Rect Boite(Window fenetre, Control controle)
    {
        var coin = controle.TranslatePoint(new Point(0, 0), fenetre) ?? throw new InvalidOperationException();
        return new Rect(coin, controle.Bounds.Size);
    }

    /// <summary>Le mélange d'une couleur à 40 % sur le blanc : un contrôle grisé, sur un fond blanc.</summary>
    private static Color Grisee(Color couleur)
    {
        static byte Canal(byte c) => (byte)Math.Round(255 - (255 - c) * 0.4);
        return Color.FromRgb(Canal(couleur.R), Canal(couleur.G), Canal(couleur.B));
    }

    private static void ProcheDe(Color attendue, Color lue, string quoi)
    {
        Assert.True(Math.Abs(attendue.R - lue.R) <= 1 && Math.Abs(attendue.G - lue.G) <= 1 && Math.Abs(attendue.B - lue.B) <= 1,
            $"{quoi} : {lue} au lieu de {attendue}");
    }

    private static Color Couleur(string cle)
    {
        Assert.True(Application.Current!.TryGetResource(cle, ThemeVariant.Light, out var valeur), cle);
        return Assert.IsType<Color>(valeur);
    }

    /// <summary>Un pixel de l'image composée, quel que soit l'ordre de ses octets.</summary>
    private static Color Pixel(Window fenetre, int x, int y)
    {
        using var image = fenetre.CaptureRenderedFrame() ?? throw new InvalidOperationException("aucune image composée");
        using var verrou = image.Lock();
        var octets = new byte[verrou.RowBytes * image.PixelSize.Height];
        System.Runtime.InteropServices.Marshal.Copy(verrou.Address, octets, 0, octets.Length);
        var i = y * verrou.RowBytes + x * 4;
        var bgra = verrou.Format == PixelFormat.Bgra8888;
        return Color.FromArgb(octets[i + 3], octets[bgra ? i + 2 : i], octets[i + 1], octets[bgra ? i : i + 2]);
    }

    private static TextFrame Texte(string nom, double x, double y) =>
        new() { Name = nom, X = x, Y = y, Width = 200, Height = 80, Text = "Texte" };

    private static ShapeFrame Carre(string nom, double x, double y) =>
        new() { Name = nom, X = x, Y = y, Width = 60, Height = 60, Fill = new SolidFill("#FF336699") };

    // --------------------------------------------------------------- les zones

    [AvaloniaFact]
    public void Chaque_zone_est_a_sa_place_a_1400_sur_900()
    {
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(Texte("A", 60, 60)));
        Assert.Equal(new Size(1400, 900), fenetre.ClientSize);

        // De haut en bas : 34 + 1, 44 + 1, 40 + 1 ; la barre d'état, 26 et son filet, au pied.
        Assert.Equal(new Rect(0, 0, 1400, 35), Boite(fenetre, Trouver<Border>(fenetre, "BarreDeMenus")));
        Assert.Equal(new Rect(0, 35, 1400, 45), Boite(fenetre, Trouver<Border>(fenetre, "BarrePrincipale")));
        Assert.Equal(new Rect(0, 80, 1400, 41), Boite(fenetre, Trouver<BarreContextuelle>(fenetre, "BarreContextuelle")));
        Assert.Equal(new Rect(0, 873, 1400, 27), Boite(fenetre, Trouver<Border>(fenetre, "BarreDEtat")));

        // De gauche à droite : le rail, le panneau Pages, la planche, le dock.
        var rail = Boite(fenetre, Trouver<Border>(fenetre, "RailDOutils"));
        var pages = Boite(fenetre, Trouver<Border>(fenetre, "PanneauPages"));
        var planche = Boite(fenetre, Trouver<PageCanvas>(fenetre, "PageView"));
        var dock = Boite(fenetre, Trouver<Border>(fenetre, "Dock"));
        Assert.Equal(new Rect(0, 121, 47, 752), rail);
        Assert.Equal(new Rect(47, 121, 169, 752), pages);
        Assert.Equal(new Rect(216, 121, 1400 - 216 - 293, 752), planche);
        Assert.Equal(new Rect(1400 - 293, 121, 293, 752), dock);
        // 292 px de dock, son filet à gauche.
        Assert.Equal(292, Trouver<TabControl>(fenetre, "OngletsDuDock").Bounds.Width);

        // Un menu de la barre : le « .menu » de la maquette, 4 + la ligne + 4 — 23 px chez
        // elle —, centré dans les 34 de la barre, et non les 32 de Fluent, où le survol
        // s'étirerait d'un bord à l'autre.
        var fichier = Trouver<Menu>(fenetre, "Menus").Items.OfType<MenuItem>().First();
        var survol = Boite(fenetre, fichier.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_LayoutRoot"));
        Assert.InRange(survol.Height, 22, 25);
        Assert.InRange(survol.Center.Y, 16, 18);

        // Les couleurs des zones, et leurs filets.
        Assert.Equal(Couleur("CouleurBlanc"), Pixel(fenetre, 700, 10));
        Assert.Equal(Couleur("CouleurFilet"), Pixel(fenetre, 700, 34));
        Assert.Equal(Couleur("CouleurFilet"), Pixel(fenetre, 700, 79));
        Assert.Equal(Couleur("CouleurFondBarreContextuelle"), Pixel(fenetre, 700, 110));
        Assert.Equal(Couleur("CouleurFiletBarreContextuelle"), Pixel(fenetre, 700, 120));
        Assert.Equal(Couleur("CouleurBlanc"), Pixel(fenetre, 23, 700));
        Assert.Equal(Couleur("CouleurFilet"), Pixel(fenetre, 46, 700));
        Assert.Equal(Couleur("CouleurFondPanneau"), Pixel(fenetre, 130, 700));
        Assert.Equal(Couleur("CouleurFilet"), Pixel(fenetre, 215, 700));
        Assert.Equal(Couleur("CouleurFilet"), Pixel(fenetre, 1107, 300));
        Assert.Equal(Couleur("CouleurFilet"), Pixel(fenetre, 700, 873));

        // Le rail : les outils seuls, dans l'ordre de la maquette — Main, Ligne et Zoom
        // n'y sont pas avant leur geste (I6, A10), la pipette jamais.
        // « ToggleButton » dérive de « Button » : les outils armables y sont compris.
        var outils = Trouver<Border>(fenetre, "RailDOutils").GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("ib"))
            .OrderBy(b => Boite(fenetre, b).Y)
            .Select(b => ToolTip.GetTip(b) as string)
            .ToArray();
        Assert.Equal(new[]
        {
            "Sélection (V)", "Bloc de texte (T)", "Titre (H)", "Image…", "Tableau (B)", "Rectangle (R)", "Ellipse (E)",
        }, outils);

        // Le dock : quatre onglets, Propriétés d'abord et choisi ; « Blocs de la page » à son pied.
        var onglets = Trouver<TabControl>(fenetre, "OngletsDuDock");
        Assert.Equal(new[] { "Propriétés", "Styles", "Couleurs", "Document" },
            onglets.Items.OfType<TabItem>().Select(o => o.Header as string));
        Assert.Equal(0, onglets.SelectedIndex);
        var blocs = Boite(fenetre, Trouver<Border>(fenetre, "BlocsDeLaPage"));
        Assert.Equal(dock.Bottom, blocs.Bottom);
        Assert.Equal(dock.X + 1, blocs.X);
    }

    // ------------------------------------------------------------- règle 3

    [AvaloniaFact]
    public void Selectionner_puis_deselectionner_ne_deplace_pas_la_planche()
    {
        // La pression qui sélectionne, le relâchement, Échap : la page ne bouge à aucun de
        // ces trois instants. La barre contextuelle garde sa hauteur sans sélection, et
        // montre alors la pastille « Page ».
        var texte = Texte("Texte", 60, 60);
        var forme = Carre("Forme", 60, 220);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(texte, forme));
        var barre = Trouver<BarreContextuelle>(fenetre, "BarreContextuelle");
        var pastille = Trouver<Border>(fenetre, "PastilleDeLaSelection").GetVisualDescendants().OfType<TextBlock>().Single();

        var bordDeLaPastille = Trouver<Border>(fenetre, "PastilleDeLaSelection");

        // Le texte ne suffit pas : une pastille masquée garde son texte, et la barre sa
        // hauteur. C'est ce qu'on voit qui compte.
        void PageSeVoit()
        {
            Assert.Equal("Page", pastille.Text);
            Assert.True(bordDeLaPastille.IsEffectivelyVisible, "la pastille ne se voit pas sans sélection");
            Assert.True(bordDeLaPastille.Bounds.Width > 0 && bordDeLaPastille.Bounds.Height > 0);
        }

        var origine = Origine(fenetre);
        PageSeVoit();
        Assert.Equal(41, barre.Bounds.Height);

        foreach (var (bloc, nature) in new (Frame, string)[] { (texte, "Bloc de texte"), (forme, "Rectangle") })
        {
            var point = SurLaPage(fenetre, bloc.X + 20, bloc.Y + 20);
            fenetre.MouseDown(point, MouseButton.Left);
            Rendu.Battre();
            Assert.Same(bloc, vm.SingleSelected);
            Assert.Equal(nature, pastille.Text);
            Assert.Equal(origine, Origine(fenetre));
            Assert.Equal(41, barre.Bounds.Height);

            fenetre.MouseUp(point, MouseButton.Left);
            Rendu.Battre();
            Assert.Equal(origine, Origine(fenetre));

            fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Rendu.Battre();
            Assert.Null(vm.SingleSelected);
            PageSeVoit();
            Assert.Equal(origine, Origine(fenetre));
            Assert.Equal(41, barre.Bounds.Height);
        }

        vm.SetSelection([texte, forme]);
        Rendu.Battre();
        Assert.Equal("2 blocs", pastille.Text);
        Assert.Equal(origine, Origine(fenetre));
    }

    // ------------------------------------------------------------- règle 2

    /// <summary>Tape dans un champ numérique, valide, et rend le focus — comme on quitte un champ.</summary>
    private static void Saisir(Window fenetre, NumericUpDown champ, string texte)
    {
        var saisie = champ.GetVisualDescendants().OfType<TextBox>().First();
        saisie.Focus();
        Rendu.Battre();
        saisie.SelectAll();
        fenetre.KeyTextInput(texte);
        fenetre.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Rendu.Battre();
        Quitter(fenetre);
    }

    private static void Quitter(Window fenetre)
    {
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
    }

    /// <summary>
    /// Le geste a fait exactement un pas : il y en a un, et une seule annulation rend le
    /// document d'avant, sans en laisser d'autre.
    /// </summary>
    /// L'annulation relit le document : les blocs d'avant sont d'autres instances, et le
    /// test reprend le sien dans la sélection, que l'annulation rétablit.
    private static void UnSeulPas(MainWindowViewModel vm, string avant, string quoi)
    {
        Assert.True(vm.CanUndo, $"{quoi} : aucun pas d'annulation");
        Assert.NotEqual(avant, DocumentSerializer.ToJson(vm.Document));
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.False(vm.CanUndo, $"{quoi} : plus d'un pas d'annulation");
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
    }

    [AvaloniaFact]
    public void Chaque_champ_du_dock_quel_que_soit_son_onglet_fait_un_seul_pas()
    {
        var bloc = Texte("Texte", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        // Propriétés : un champ en millimètres.
        Saisir(fenetre, Trouver<NumericUpDown>(fenetre, "ChampX"), "52.5");
        Assert.Equal(Units.MmToPoints(52.5), bloc.X, precision: 9);
        UnSeulPas(vm, avant, "Propriétés, X");

        // Propriétés : la marge commune, qui écrit quatre propriétés du bloc.
        bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
        Saisir(fenetre, Trouver<NumericUpDown>(fenetre, "MargeInterieureCommune"), "3");
        Assert.All(new[] { bloc.PaddingLeft, bloc.PaddingRight, bloc.PaddingTop, bloc.PaddingBottom },
            marge => Assert.Equal(Units.MmToPoints(3), marge, precision: 9));
        UnSeulPas(vm, avant, "Propriétés, marge commune");

        // Propriétés : le fond, deux propriétés du modèle sous une seule liste.
        bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
        var fond = Trouver<ComboBox>(fenetre, "SorteDeFond");
        fond.Focus();
        fond.SelectedItem = vm.SortesDeFond.Single(o => o.Sorte == FillKind.Gradient);
        Rendu.Battre();
        Quitter(fenetre);
        Assert.IsType<GradientFill>(bloc.Fill);
        UnSeulPas(vm, avant, "Propriétés, fond");

        // Document : une marge de la page.
        Rendu.Onglet(fenetre, "Document");
        Saisir(fenetre, Trouver<NumericUpDown>(fenetre, "MargeGauche"), "25");
        Assert.Equal(Units.MmToPoints(25), vm.Document.MarginLeft, precision: 9);
        UnSeulPas(vm, avant, "Document, marge");

        // Styles et Couleurs : les jeux du document.
        Rendu.Onglet(fenetre, "Styles");
        var polices = Trouver<ComboBox>(fenetre, "JeuDePolices");
        polices.Focus();
        polices.SelectedItem = JeuxPredefinis.Polices.First(j => j != vm.Document.FontScheme);
        Rendu.Battre();
        Quitter(fenetre);
        UnSeulPas(vm, avant, "Styles, jeu de polices");

        Rendu.Onglet(fenetre, "Couleurs");
        var couleurs = Trouver<ComboBox>(fenetre, "JeuDeCouleurs");
        couleurs.Focus();
        couleurs.SelectedItem = JeuxPredefinis.Couleurs.First(j => j != vm.Document.ColorScheme);
        Rendu.Battre();
        Quitter(fenetre);
        UnSeulPas(vm, avant, "Couleurs, jeu de couleurs");
    }

    [AvaloniaFact]
    public void Changer_d_onglet_referme_la_saisie_en_cours()
    {
        // Le champ qui avait le focus quitte l'arbre avec son onglet : sa transaction se
        // referme au changement d'onglet, et la saisie suivante, ailleurs, fait son propre pas.
        var bloc = Texte("Texte", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var saisie = Trouver<NumericUpDown>(fenetre, "ChampY").GetVisualDescendants().OfType<TextBox>().First();
        saisie.Focus();
        saisie.SelectAll();
        fenetre.KeyTextInput("40");
        Rendu.Battre();
        Rendu.Onglet(fenetre, "Document");
        Assert.True(vm.CanUndo, "la saisie laissée dans l'onglet quitté n'a pas fait son pas");

        Saisir(fenetre, Trouver<NumericUpDown>(fenetre, "MargeGauche"), "30");
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(Units.MmToPoints(40), bloc.Y, precision: 9);
        Assert.NotEqual(Units.MmToPoints(30), vm.Document.MarginLeft, precision: 6);
    }

    // ------------------------------------------------------- les unités du dock

    [AvaloniaFact]
    public void Les_longueurs_du_dock_portent_leur_unite_et_se_relisent()
    {
        var bloc = Texte("Texte", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        var x = Trouver<NumericUpDown>(fenetre, "ChampX");
        var culture = CultureInfo.CurrentCulture;
        Assert.Equal(Units.PointsToMm(60).ToString("0.0", culture) + " mm", x.Text);
        // Afficher n'écrit rien : l'arrondi au dixième reste au texte.
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));

        // La virgule comme le point, et l'unité ou non : « 52,5 mm » ne devient pas 525.
        foreach (var (tape, attendu) in new[] { ("52,5 mm", 52.5), ("41.25", 41.25), ("30 mm", 30.0) })
        {
            Saisir(fenetre, x, tape);
            Assert.Equal(Units.MmToPoints(attendu), bloc.X, precision: 9);
        }

        // Un texte illisible ne touche pas au bloc.
        Saisir(fenetre, x, "trente");
        Assert.Equal(Units.MmToPoints(30), bloc.X, precision: 9);

        Assert.EndsWith("°", Trouver<NumericUpDown>(fenetre, "ChampAngle").Text);
    }

    // ---------------------------------------------------------- les marges intérieures

    [AvaloniaFact]
    public void Quatre_marges_egales_tiennent_dans_un_champ_et_quatre_distinctes_dans_quatre()
    {
        var egal = Texte("Égal", 60, 60);
        egal.PaddingLeft = egal.PaddingRight = egal.PaddingTop = egal.PaddingBottom = 4;
        var inegal = Texte("Inégal", 60, 200);
        inegal.PaddingLeft = 2;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(egal, inegal));

        vm.SelectOnly(egal);
        Rendu.Battre();
        var commune = Trouver<NumericUpDown>(fenetre, "MargeInterieureCommune");
        Assert.True(commune.IsEffectivelyVisible);
        Assert.Empty(fenetre.GetVisualDescendants().OfType<NumericUpDown>()
            .Where(n => n.Classes.Contains("marge") && n.IsEffectivelyVisible));

        vm.SelectOnly(inegal);
        Rendu.Battre();
        Assert.False(Trouver<NumericUpDown>(fenetre, "MargeInterieureCommune").IsEffectivelyVisible);
        Assert.Equal(4, fenetre.GetVisualDescendants().OfType<NumericUpDown>()
            .Count(n => n.Classes.Contains("marge") && n.IsEffectivelyVisible));
        Assert.False(vm.CanUndo, "sélectionner a écrit dans le document");
    }

    // ------------------------------------------------------------ « Blocs de la page »

    [AvaloniaFact]
    public void Blocs_de_la_page_montre_l_avant_en_tete_et_suit_la_page()
    {
        var a = Carre("A", 20, 20);
        var b = Carre("B", 40, 40);
        var c = Carre("C", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b, c));
        var liste = Trouver<ListBox>(fenetre, "ListeDesBlocs");

        string[] Lue() => liste.Items.OfType<Frame>().Select(f => f.Name).ToArray();
        // Les conteneurs recyclés restent dans l'arbre, masqués : seuls comptent ceux qu'on voit.
        string[] Montree() => liste.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.IsVisible)
            .OrderBy(i => Boite(fenetre, i).Y).Select(i => ((Frame)i.DataContext!).Name).ToArray();

        Assert.Equal(new[] { "C", "B", "A" }, Lue());
        Assert.Equal(new[] { "C", "B", "A" }, Montree());

        // Chaque changement de la page se retraduit au rang miroir : premier plan, ajout,
        // retrait, annulation.
        vm.SelectOnly(a);
        vm.BringToFrontCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(new[] { "A", "C", "B" }, Montree());

        vm.CurrentPage!.Frames.Insert(1, Carre("D", 80, 80));
        Rendu.Battre();
        Assert.Equal(new[] { "A", "C", "D", "B" }, Montree());

        vm.CurrentPage.Frames.RemoveAt(0);
        Rendu.Battre();
        Assert.Equal(new[] { "A", "C", "D" }, Montree());

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(new[] { "C", "B", "A" }, Lue());
        Assert.Equal(new[] { "C", "B", "A" }, Montree());

        // Le type de chaque bloc, par son initiale, comme la maquette.
        Assert.All(liste.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.IsVisible), i =>
            Assert.Contains(i.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "R"));
    }

    // ------------------------------------------------------------------- B7

    [AvaloniaFact]
    public void Ctrl_F1_replie_les_deux_panneaux_et_les_rend()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte("A", 60, 60)));
        var pages = Trouver<Border>(fenetre, "PanneauPages");
        var dock = Trouver<Border>(fenetre, "Dock");
        var planche = Trouver<PageCanvas>(fenetre, "PageView");
        var largeur = planche.Bounds.Width;
        var panneaux = pages.Bounds.Width + dock.Bounds.Width;

        fenetre.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.Control);
        Rendu.Battre();
        Assert.False(pages.IsVisible);
        Assert.False(dock.IsVisible);
        Assert.True(planche.Bounds.Width >= largeur + panneaux,
            $"la planche passe de {largeur} à {planche.Bounds.Width} px, pour {panneaux} px de panneaux");
        // Le rail reste : sans lui, aucun outil ne s'arme plus qu'au clavier.
        Assert.True(Trouver<Border>(fenetre, "RailDOutils").IsVisible);

        fenetre.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.Control);
        Rendu.Battre();
        Assert.True(pages.IsVisible);
        Assert.True(dock.IsVisible);
        Assert.Equal(largeur, planche.Bounds.Width);

        // Le second Ctrl+F1 rend l'état d'avant, et non « les deux » : le dock seul masqué
        // au menu Fenêtre le reste. Entre les deux, le premier a bien tout replié — un
        // raccourci qui inverserait chaque panneau montrerait le dock et masquerait Pages.
        vm.DockVisible = false;
        Rendu.Battre();
        var dockMasque = planche.Bounds.Width;
        fenetre.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.Control);
        Rendu.Battre();
        Assert.False(pages.IsVisible);
        Assert.False(dock.IsVisible);
        Assert.True(planche.Bounds.Width >= largeur + panneaux,
            $"le dock masqué, la planche passe de {dockMasque} à {planche.Bounds.Width} px au repli");
        fenetre.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.Control);
        Rendu.Battre();
        Assert.True(pages.IsVisible);
        Assert.False(dock.IsVisible);
        Assert.Equal(dockMasque, planche.Bounds.Width);

        // Un état d'avant périmé ne revient pas. Un panneau masqué, replié ; puis l'autre
        // rouvert et remasqué à la main : le raccourci les déplie tous deux, et non le repli
        // d'avant. Chaque panneau à son tour, chacun son geste au menu.
        foreach (var (masque, touche) in new (Action, Action<bool>)[]
                 {
                     (() => vm.PanneauPagesVisible = false, v => vm.DockVisible = v),
                     (() => vm.DockVisible = false, v => vm.PanneauPagesVisible = v),
                 })
        {
            vm.PanneauPagesVisible = vm.DockVisible = true;
            masque();
            Rendu.Battre();
            fenetre.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.Control);
            Rendu.Battre();
            touche(true);
            touche(false);
            Rendu.Battre();
            fenetre.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.Control);
            Rendu.Battre();
            Assert.True(pages.IsVisible && dock.IsVisible,
                $"un repli plus ancien est revenu : Pages {pages.IsVisible}, dock {dock.IsVisible}");
        }

        // Un état de la vue, et non du document.
        Assert.False(vm.CanUndo);
        Assert.False(vm.IsDirty);
    }

    [AvaloniaFact]
    public void Le_menu_Fenetre_masque_chaque_panneau_au_pointeur()
    {
        // Au pointeur, comme la main : une case de menu se lie dans un seul sens par défaut,
        // et la coche s'inverserait sans que le panneau bouge.
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte("A", 60, 60)));
        var menu = Trouver<Menu>(fenetre, "Menus").Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Fe_nêtre"));

        foreach (var (nom, panneau) in new[] { ("MenuPanneauPages", "PanneauPages"), ("MenuDock", "Dock") })
        {
            var bord = Trouver<Border>(fenetre, panneau);
            foreach (var visible in new[] { false, true })
            {
                Rendu.Cliquer(menu);
                var entree = menu.Items.OfType<MenuItem>().Single(m => m.Name == nom);
                Assert.True(entree.IsEffectivelyVisible, $"« {entree.Header} » n'est pas à l'écran, menu ouvert");
                Rendu.Cliquer(entree);
                Assert.Equal(visible, entree.IsChecked);
                Assert.Equal(visible, bord.IsVisible);
            }
        }

        Assert.True(vm.PanneauPagesVisible && vm.DockVisible);

        // Masqué à la main, rendu par le raccourci.
        Rendu.Cliquer(menu);
        Rendu.Cliquer(menu.Items.OfType<MenuItem>().Single(m => m.Name == "MenuDock"));
        Assert.False(Trouver<Border>(fenetre, "Dock").IsVisible);
        fenetre.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.Control);
        fenetre.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.Control);
        Rendu.Battre();
        Assert.False(Trouver<Border>(fenetre, "Dock").IsVisible);
        Rendu.Cliquer(menu);
        Rendu.Cliquer(menu.Items.OfType<MenuItem>().Single(m => m.Name == "MenuDock"));
        Assert.True(Trouver<Border>(fenetre, "Dock").IsVisible);
        Assert.False(vm.CanUndo);
    }

    // ------------------------------------------------------------ barre principale

    [AvaloniaFact]
    public void Annuler_et_Retablir_se_grisent_quand_l_historique_est_vide()
    {
        var bloc = Carre("A", 20, 20);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var annuler = Trouver<Button>(fenetre, "BoutonAnnuler");
        var retablir = Trouver<Button>(fenetre, "BoutonRetablir");

        Assert.False(annuler.IsEffectivelyEnabled);
        Assert.False(retablir.IsEffectivelyEnabled);

        vm.SelectOnly(bloc);
        vm.NudgeSelection(1, 0, fine: false);
        Rendu.Battre();
        Assert.True(annuler.IsEffectivelyEnabled);
        Assert.False(retablir.IsEffectivelyEnabled);

        annuler.Command!.Execute(null);
        Rendu.Battre();
        Assert.False(annuler.IsEffectivelyEnabled);
        Assert.True(retablir.IsEffectivelyEnabled);
        // L'annulation rend un document relu : le bloc est une autre instance.
        Assert.Equal(20, vm.CurrentPage!.Frames[0].X);
    }

    [AvaloniaFact]
    public void Annuler_reste_actif_pendant_une_saisie_qui_a_change_le_bloc()
    {
        // Une valeur validée sans quitter le champ : le bloc a bougé, mais le pas n'entre dans
        // l'historique qu'à la perte du focus. Annuler ne se grise pas pour autant, et son
        // clic — qui prend le focus — referme la saisie puis l'annule.
        var bloc = Texte("Texte", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var annuler = Trouver<Button>(fenetre, "BoutonAnnuler");
        var retablir = Trouver<Button>(fenetre, "BoutonRetablir");
        var titre = Trouver<TextBlock>(fenetre, "TitreDuDocument");
        Assert.False(annuler.IsEffectivelyEnabled);

        var saisie = Trouver<NumericUpDown>(fenetre, "ChampX").GetVisualDescendants().OfType<TextBox>().First();
        saisie.Focus();
        Rendu.Battre();
        // Sélectionner n'est pas retoucher : le focus seul ne rend pas Annuler.
        Assert.False(annuler.IsEffectivelyEnabled);
        saisie.SelectAll();
        fenetre.KeyTextInput("52.5");
        fenetre.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Rendu.Battre();
        Assert.Equal(Units.MmToPoints(52.5), bloc.X, precision: 9);
        Assert.True(saisie.IsFocused);
        Assert.False(vm.CanUndo);
        Assert.True(annuler.IsEffectivelyEnabled, "le bloc a bougé et Annuler est grisé");
        Assert.False(retablir.IsEffectivelyEnabled);
        Assert.Equal("banc.mypub — modifié", titre.Text);

        Rendu.Cliquer(annuler);
        Assert.Equal(60, vm.CurrentPage!.Frames[0].X, precision: 9);
        Assert.False(vm.CanUndo);
        Assert.True(vm.CanRedo);
        Assert.False(annuler.IsEffectivelyEnabled);
        Assert.True(retablir.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void Page_entiere_attend_le_geste_qui_l_ecoute()
    {
        // Le bouton est posé par I2 ; le calcul est celui de la planche (I6). Sans personne
        // pour l'entendre, il est grisé plutôt que de ne rien faire.
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille());
        var bouton = Trouver<Button>(fenetre, "BoutonPageEntiere");
        Assert.False(bouton.IsEffectivelyEnabled);

        var entendu = 0;
        EventHandler ecoute = (_, _) => entendu++;
        vm.PageEntiereDemandee += ecoute;
        Rendu.Battre();
        Assert.True(bouton.IsEffectivelyEnabled);
        bouton.Command!.Execute(null);
        Assert.Equal(1, entendu);

        vm.PageEntiereDemandee -= ecoute;
        Rendu.Battre();
        Assert.False(bouton.IsEffectivelyEnabled);
    }

    // ---------------------------------------------------------- le menu Texte

    [AvaloniaFact]
    public void Le_menu_Texte_porte_le_gras_l_italique_les_alignements_et_l_import()
    {
        var bloc = Texte("Texte", 60, 60);
        var forme = Carre("Forme", 60, 220);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, forme));

        var menu = fenetre.GetVisualDescendants().OfType<MenuItem>().Single(m => Equals(m.Header, "_Texte"));
        var entrees = menu.Items.OfType<MenuItem>().ToList();
        Assert.Equal(new[]
        {
            "_Gras", "_Italique", "_Souligné", "Aligner le texte à _gauche", "_Centrer le texte", "Aligner le texte à _droite",
            "_Justifier", "Importer un te_xte…",
        }, entrees.Select(e => e.Header as string));
        Assert.Equal("MenuImporterTexte", entrees[^1].Name);
        Assert.Equal(KeyGesture.Parse("Ctrl+G"), entrees[0].InputGesture);
        Assert.Equal(KeyGesture.Parse("Ctrl+I"), entrees[1].InputGesture);
        // Le souligné y entre avec la barre contextuelle (I3) : sa recette l'éprouve.
        Assert.Equal(KeyGesture.Parse("Ctrl+U"), entrees[2].InputGesture);

        // Sans bloc de texte, rien n'est offert.
        vm.SelectOnly(forme);
        Rendu.Battre();
        Assert.False(vm.BasculerGrasCommand.CanExecute(null));
        Assert.False(vm.AlignerLeTexteCommand.CanExecute(TextAlign.Center));

        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        fenetre.KeyPressQwerty(PhysicalKey.G, RawInputModifiers.Control);
        Rendu.Battre();
        Assert.True(bloc.Bold);
        UnSeulPas(vm, avant, "Ctrl+G");

        bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
        fenetre.KeyPressQwerty(PhysicalKey.I, RawInputModifiers.Control);
        Rendu.Battre();
        Assert.True(bloc.Italic);
        UnSeulPas(vm, avant, "Ctrl+I");

        bloc = Assert.IsType<TextFrame>(vm.SingleSelected);

        foreach (var (entree, alignement) in new[]
                 {
                     (entrees[4], TextAlign.Center), (entrees[5], TextAlign.Right),
                     (entrees[6], TextAlign.Justify), (entrees[3], TextAlign.Left),
                 })
        {
            entree.Command!.Execute(entree.CommandParameter);
            Rendu.Battre();
            Assert.Equal(alignement, bloc.Align);
        }

        // Ni l'un ni l'autre n'arme d'outil : G et I ne sont pas des lettres d'outil, et
        // Ctrl les écarte de toute façon.
        Assert.Equal(EditorTool.Select, vm.ActiveTool);
    }

    [AvaloniaFact]
    public void Ctrl_G_pendant_une_saisie_du_dock_fait_son_propre_pas()
    {
        // Un geste, un pas, où que soit le focus (règle 2). Le raccourci joue sans retirer le
        // focus au champ, dont la transaction est ouverte : le gras ne doit pas s'y fondre,
        // et la frappe qui suit, dans le même champ, fait encore le sien.
        var bloc = Texte("Nom1", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var nom = Trouver<TextBox>(fenetre, "ChampNom");
        nom.Focus();
        Rendu.Battre();
        nom.SelectAll();
        fenetre.KeyTextInput("Nouveau");
        Rendu.Battre();
        fenetre.KeyPressQwerty(PhysicalKey.G, RawInputModifiers.Control);
        Rendu.Battre();
        Assert.True(bloc.Bold);
        Assert.True(nom.IsFocused);
        Assert.True(vm.CanUndo, "le gras s'est fondu dans la saisie en cours");
        fenetre.KeyTextInput(" bis");
        Rendu.Battre();
        Quitter(fenetre);

        TextFrame Bloc() => Assert.IsType<TextFrame>(vm.CurrentPage!.Frames[0]);
        Assert.Equal(("Nouveau bis", true), (Bloc().Name, Bloc().Bold));
        foreach (var (attendu, gras) in new[] { ("Nouveau", true), ("Nouveau", false), ("Nom1", false) })
        {
            Assert.True(vm.CanUndo, $"il manque un pas avant « {attendu} »");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal((attendu, gras), (Bloc().Name, Bloc().Bold));
        }

        Assert.False(vm.CanUndo, "trois gestes, plus de trois pas");
    }

    // ------------------------------------------------------------ les sections

    [AvaloniaFact]
    public void Une_section_fermee_le_reste_d_un_bloc_a_l_autre()
    {
        var a = Texte("A", 60, 60);
        var b = Texte("B", 60, 200);
        var forme = Carre("Forme", 300, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b, forme));

        ToggleButton Titre(string texte) => fenetre.GetVisualDescendants().OfType<ToggleButton>()
            .Single(t => t.Classes.Contains("titreSection") && t.IsEffectivelyVisible &&
                         t.GetVisualDescendants().OfType<TextBlock>().Any(l => l.Text == texte));

        vm.SelectOnly(a);
        Rendu.Battre();
        var cadre = Titre("Cadre de texte");
        Assert.True(cadre.IsChecked);
        var colonnes = Trouver<NumericUpDown>(fenetre, "NombreDeColonnes");
        Assert.True(colonnes.IsEffectivelyVisible);

        // Un clic sur le titre ferme la section.
        Rendu.Cliquer(cadre);
        Assert.False(Titre("Cadre de texte").IsChecked);
        Assert.False(Trouver<NumericUpDown>(fenetre, "NombreDeColonnes").IsEffectivelyVisible);

        // Un autre bloc de texte, une forme, puis le premier : elle est restée fermée, et
        // les autres ouvertes.
        foreach (var bloc in new Frame[] { b, forme, a })
        {
            vm.SelectOnly(bloc);
            Rendu.Battre();
        }

        Assert.False(Titre("Cadre de texte").IsChecked);
        Assert.False(Trouver<NumericUpDown>(fenetre, "NombreDeColonnes").IsEffectivelyVisible);
        Assert.True(Titre("Paragraphe").IsChecked);
        Assert.True(Titre("Position et taille").IsChecked);
        Assert.False(vm.CanUndo);
    }

    // ------------------------------------------------------------ Fond et contour

    [AvaloniaFact]
    public void Fond_et_contour_ont_leur_vignette_de_couleur()
    {
        // La vignette de 22 en tête de chaque ligne, comme la maquette : la couleur, ou la
        // diagonale rouge de « Aucun ».
        var sans = Texte("Sans", 60, 60);
        var avec = Carre("Avec", 60, 220);
        avec.HasStroke = true;
        avec.StrokeColor = "#FF884422";
        avec.StrokeThickness = 2;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(sans, avec));

        Color Centre(string nom, out Rect boite)
        {
            var vignette = Trouver<Border>(fenetre, nom);
            vignette.BringIntoView();
            Rendu.Battre();
            boite = Boite(fenetre, vignette);
            Assert.Equal(new Size(22, 22), boite.Size);
            return Pixel(fenetre, (int)boite.Center.X, (int)boite.Center.Y);
        }

        vm.SelectOnly(sans);
        Rendu.Battre();
        foreach (var nom in new[] { "VignetteDuFond", "VignetteDuContour" })
        {
            Assert.Equal(Couleur("CouleurAucun"), Centre(nom, out var boite));
            // Hors de la diagonale, le blanc ; sur le bord, le bord des champs.
            Assert.Equal(Couleur("CouleurBlanc"), Pixel(fenetre, (int)boite.X + 5, (int)boite.Y + 5));
            Assert.Equal(Couleur("CouleurBordChamp"), Pixel(fenetre, (int)boite.Center.X, (int)boite.Y));
        }

        vm.SelectOnly(avec);
        Rendu.Battre();
        Assert.Equal(Color.Parse("#336699"), Centre("VignetteDuFond", out _));
        Assert.Equal(Color.Parse("#884422"), Centre("VignetteDuContour", out _));
        Assert.False(vm.CanUndo, "montrer la couleur a écrit dans le document");
    }

    [AvaloniaFact]
    public void Un_champ_grise_garde_le_fond_et_le_bord_de_la_maquette()
    {
        // Le contour « Aucun » grise son épaisseur, un champ calculé grise la saisie : le fond
        // et le bord du champ, à 40 %, et non le gris de Fluent (#CCCCCC, bordé de #94979D).
        var bloc = Texte("Texte", 60, 60);
        bloc.Field = FieldKind.PageNumber;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        foreach (var champ in new Control[]
                 {
                     Trouver<NumericUpDown>(fenetre, "EpaisseurDuContour"), Trouver<TextBox>(fenetre, "SaisieDuTexte"),
                 })
        {
            Assert.False(champ.IsEffectivelyEnabled);
            champ.BringIntoView();
            Rendu.Battre();
            var boite = Boite(fenetre, champ);
            Assert.InRange(boite.Bottom, 0, 860);
            ProcheDe(Couleur("CouleurBlanc"), Pixel(fenetre, (int)boite.Right - 4, (int)boite.Center.Y), $"{champ.Name}, fond");
            ProcheDe(Grisee(Couleur("CouleurBordChamp")), Pixel(fenetre, (int)boite.Center.X, (int)boite.Y), $"{champ.Name}, bord");
        }
    }

    [AvaloniaFact]
    public void Le_bouton_Italique_a_une_vraie_italique()
    {
        // Le « I » de la maquette est en Georgia, serif. Inter, la police du chrome, n'a pas
        // d'italique : penché par simulation, son « I » se lit comme une barre oblique.
        var bloc = Texte("Texte", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var bouton = fenetre.GetVisualDescendants().OfType<ToggleButton>()
            .Single(t => Equals(ToolTip.GetTip(t), "Italique (Ctrl+I)") && t.IsEffectivelyVisible);
        var texte = bouton.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.Equal("I", texte.Text);
        Assert.Equal(FontStyle.Italic, texte.FontStyle);
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(texte.FontFamily, FontStyle.Italic, texte.FontWeight),
            out var police));
        Assert.Equal(FontStyle.Italic, police.Style);
        Assert.Equal(FontSimulations.None, police.FontSimulations);
        // Une serif nommée : la serif générique peut tomber sur une sans.
        Assert.Contains(police.FamilyName, texte.FontFamily.FamilyNames.Where(n => n != "serif"));
    }

    // ------------------------------------------------------------ barre d'état

    [AvaloniaFact]
    public void La_barre_d_etat_dit_la_page_le_format_et_le_zoom()
    {
        var document = Rendu.Feuille(Texte("A", 60, 60));
        document.Pages.Add(new Page { Name = "Page 2" });
        document.Pages.Add(new Page { Name = "Page 3" });
        var (fenetre, vm) = Rendu.Ouvrir(document);

        var page = Trouver<TextBlock>(fenetre, "PageSurPages");
        var format = Trouver<TextBlock>(fenetre, "FormatDeLaPage");
        Assert.Equal("Page 1 sur 3", page.Text);
        Assert.Equal("A4 portrait", format.Text);

        vm.CurrentPage = document.Pages[1];
        Rendu.Battre();
        Assert.Equal("Page 2 sur 3", page.Text);

        vm.IsLandscape = true;
        Rendu.Battre();
        Assert.Equal("A4 paysage", format.Text);

        vm.Document.PageWidth = Units.MmToPoints(100);
        vm.Document.PageHeight = Units.MmToPoints(200);
        Rendu.Battre();
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "{0:0.#} × {1:0.#} mm", 100.0, 200.0), format.Text);

        // Le curseur du zoom, en échelle logarithmique entre les bornes du zoom : au
        // milieu du rail, la moyenne géométrique des bornes.
        var curseur = Trouver<Slider>(fenetre, "CurseurDuZoom");
        Assert.Equal(Math.Log(MainWindowViewModel.ZoomMinimum), curseur.Minimum, precision: 9);
        Assert.Equal(Math.Log(MainWindowViewModel.ZoomMaximum), curseur.Maximum, precision: 9);
        Assert.Equal(Math.Log(vm.Zoom), curseur.Value, precision: 9);

        curseur.Value = (curseur.Minimum + curseur.Maximum) / 2;
        Rendu.Battre();
        Assert.Equal(Math.Sqrt(MainWindowViewModel.ZoomMinimum * MainWindowViewModel.ZoomMaximum), vm.Zoom, precision: 9);

        vm.ZoomInCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(Math.Log(vm.Zoom), curseur.Value, precision: 9);
    }

    [AvaloniaFact]
    public void Le_titre_du_document_se_lit_dans_la_barre_de_menus()
    {
        var bloc = Carre("A", 20, 20);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var titre = Trouver<TextBlock>(fenetre, "TitreDuDocument");
        Assert.Equal("banc.mypub", titre.Text);

        vm.SelectOnly(bloc);
        vm.NudgeSelection(0, 1, fine: false);
        Rendu.Battre();
        Assert.Equal("banc.mypub — modifié", titre.Text);

        // Centré dans ce que la barre laisse après les menus.
        var barre = Boite(fenetre, Trouver<Border>(fenetre, "BarreDeMenus"));
        var menus = Boite(fenetre, Trouver<Menu>(fenetre, "Menus"));
        var boite = Boite(fenetre, titre);
        var milieu = (menus.Right + barre.Right - 10) / 2;
        Assert.InRange(boite.Center.X, milieu - 1, milieu + 1);
    }
}
