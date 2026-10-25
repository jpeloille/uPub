using System;
using System.Collections.Generic;
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
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Masquer un bloc — poste I8, sur la vraie fenêtre. L'œil de « Blocs de la page » masque
/// le bloc en un pas d'annulation ; masqué, il n'est plus dans l'encre de <c>PageRoot</c>
/// ni dans la vignette de sa page, un clic à sa place prend le bloc d'en dessous, et il
/// ne s'aligne pas ; il reste dans la liste, l'œil fermé, et s'y sélectionne sans que la
/// planche en montre rien.
///
/// Ce que le noyau en fait — le fichier, la recopie, l'export, les polices — est éprouvé
/// par <c>MasquerTests</c> ; le PDF, par la recette <c>I8-masquer</c>, qui joue ceux-ci.
/// </summary>
public class MasquerEcranTests
{
    /// <summary>
    /// Deux blocs l'un sur l'autre. Dessous, un aplat clair, qui n'est pas de l'encre au
    /// sens de <see cref="Rendu.EstEncre"/> ; dessus, un aplat noir, qui en est. Le centre
    /// du second tombe sur le premier : c'est là qu'on clique et qu'on lit l'encre.
    /// </summary>
    private static Document DeuxBlocs() => Rendu.Feuille(
        new ShapeFrame { Name = "Dessous", X = 100, Y = 100, Width = 240, Height = 160, Fill = new SolidFill("#FF9DC3E6"), Stroke = null },
        new ShapeFrame { Name = "Dessus", X = 160, Y = 140, Width = 100, Height = 60, Fill = new SolidFill("#FF000000"), Stroke = null });

    private const double CentreX = 210, CentreY = 170;

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().First(c => c.Name == nom);

    private static Point SurLaPage(Window fenetre, double x, double y)
    {
        var origine = Trouver<Panel>(fenetre, "PageRoot").TranslatePoint(new Point(0, 0), fenetre) ?? new Point(0, 0);
        return new Point(origine.X + x, origine.Y + y);
    }

    private static void Cliquer(Window fenetre, Point point, RawInputModifiers touches = RawInputModifiers.None)
    {
        fenetre.MouseDown(point, MouseButton.Left, touches);
        fenetre.MouseUp(point, MouseButton.Left, touches);
        Rendu.Battre();
    }

    private static Point Centre(Window fenetre, Control controle) =>
        controle.TranslatePoint(new Point(controle.Bounds.Width / 2, controle.Bounds.Height / 2), fenetre)
        ?? throw new InvalidOperationException($"« {controle.Name} » n'est pas dans la fenêtre");

    /// <summary>La ligne d'un bloc dans « Blocs de la page ».</summary>
    private static ListBoxItem Ligne(Window fenetre, Frame bloc) =>
        (ListBoxItem)Trouver<ListBox>(fenetre, "ListeDesBlocs").ContainerFromItem(bloc)!;

    private static Button Oeil(Window fenetre, Frame bloc) => Trouver<Button>(Ligne(fenetre, bloc), "OeilDuBloc");

    private static bool Encre(Window fenetre, double x, double y) =>
        Rendu.EstEncre(Rendu.Encre(fenetre), (int)(x * Rendu.PixelsParPoint), (int)(y * Rendu.PixelsParPoint));

    private static Frame Bloc(MainWindowViewModel vm, string nom) => vm.CurrentPage!.Frames.Single(f => f.Name == nom);

    /// <summary>Ce que l'Overlay montre de la sélection : cadres et poignées allumés.</summary>
    private static int MarquesDeSelection(Window fenetre) =>
        Trouver<Canvas>(fenetre, "Overlay").Children
            .Count(c => c.IsVisible && (c.Classes.Contains("cadreDeSelection") || c.Classes.Contains("poignee")));

    // ------------------------------------------------------------------ l'œil

    /// <summary>
    /// La recette : l'œil se ferme d'un clic, le bloc quitte l'encre de la page, le
    /// document est « modifié », et un pas d'annulation — un seul — rouvre l'œil. Le clic
    /// ne sélectionne rien : on masque un bloc sans le prendre.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void L_oeil_masque_le_bloc_en_un_pas_et_l_annulation_le_rouvre()
    {
        var (fenetre, vm) = Rendu.Ouvrir(DeuxBlocs());
        var dessus = Bloc(vm, "Dessus");

        Assert.True(Encre(fenetre, CentreX, CentreY));
        Assert.True(Trouver<Viewbox>(Ligne(fenetre, dessus), "OeilFerme").IsVisible is false);
        Assert.False(vm.IsDirty);

        Cliquer(fenetre, Centre(fenetre, Oeil(fenetre, dessus)));

        Assert.False(dessus.Visible);
        Assert.False(Encre(fenetre, CentreX, CentreY));
        Assert.True(vm.IsDirty);
        Assert.StartsWith("• ", vm.WindowTitle);
        Assert.Empty(vm.Selection);
        // L'œil fermé à la place de l'ouvert, sur la même ligne : le bloc reste dans la liste.
        Assert.Contains(dessus, Trouver<ListBox>(fenetre, "ListeDesBlocs").Items.OfType<Frame>());
        Assert.True(Trouver<Viewbox>(Ligne(fenetre, dessus), "OeilFerme").IsVisible);

        fenetre.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        Rendu.Battre();

        Assert.True(Bloc(vm, "Dessus").Visible);
        Assert.True(Encre(fenetre, CentreX, CentreY));
        Assert.False(vm.CanUndo);
        Assert.False(Trouver<Viewbox>(Ligne(fenetre, Bloc(vm, "Dessus")), "OeilFerme").IsVisible);

        // Fermé puis rouvert : deux pas, et le premier qu'on défait referme l'œil.
        vm.BasculerLaVisibiliteCommand.Execute(Bloc(vm, "Dessus"));
        vm.BasculerLaVisibiliteCommand.Execute(Bloc(vm, "Dessus"));
        Assert.True(Bloc(vm, "Dessus").Visible);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.False(Bloc(vm, "Dessus").Visible);
        vm.UndoCommand.Execute(null);
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// L'œil est un bouton, pas une prise : tiré vers le bas de la liste, il ne réordonne
    /// pas la pile (A4), et ne masque rien quand on le lâche ailleurs.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void On_ne_tire_pas_un_bloc_par_son_oeil()
    {
        var (fenetre, vm) = Rendu.Ouvrir(DeuxBlocs());
        var dessus = Bloc(vm, "Dessus");
        var depart = Centre(fenetre, Oeil(fenetre, dessus));
        var bas = Ligne(fenetre, Bloc(vm, "Dessous"));
        var arrivee = bas.TranslatePoint(new Point(bas.Bounds.Width / 2, bas.Bounds.Height * 0.9), fenetre)!.Value;

        fenetre.MouseDown(depart, MouseButton.Left);
        fenetre.MouseMove(new Point(depart.X, depart.Y + 6), RawInputModifiers.LeftMouseButton);
        fenetre.MouseMove(arrivee, RawInputModifiers.LeftMouseButton);
        fenetre.MouseUp(arrivee, MouseButton.Left);
        Rendu.Battre();

        Assert.Equal(["Dessous", "Dessus"], vm.CurrentPage!.Frames.Select(f => f.Name));
        Assert.True(dessus.Visible);
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// Règle 2 de la phase : l'œil fait son propre pas même pendant qu'un champ du dock
    /// tient sa transaction ouverte — le bouton ne prend pas le focus, et une seule
    /// annulation défairait sinon à la fois la saisie et le masquage.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void L_oeil_fait_son_pas_pendant_une_saisie_du_dock()
    {
        var (fenetre, vm) = Rendu.Ouvrir(DeuxBlocs());
        var dessus = Bloc(vm, "Dessus");

        vm.BeginEdit();
        dessus.Name = "Renommé";
        Cliquer(fenetre, Centre(fenetre, Oeil(fenetre, dessus)));
        vm.CommitEdit();

        Assert.False(dessus.Visible);
        vm.UndoCommand.Execute(null);
        Assert.True(Bloc(vm, "Renommé").Visible);
        vm.UndoCommand.Execute(null);
        Assert.True(Bloc(vm, "Dessus").Visible);
        Assert.False(vm.CanUndo);
    }

    // ------------------------------------------------------------------ la planche

    /// <summary>
    /// Masqué, le bloc ne se saisit plus : un clic à sa place prend le bloc d'en dessous,
    /// et le pointeur n'y annonce plus un déplacement. Visible, le même clic le prend.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void Un_clic_a_la_place_d_un_bloc_masque_prend_le_bloc_d_en_dessous()
    {
        var (fenetre, vm) = Rendu.Ouvrir(DeuxBlocs());

        Cliquer(fenetre, SurLaPage(fenetre, CentreX, CentreY));
        Assert.Equal("Dessus", vm.Selected?.Name);

        vm.SetSelection([]);
        vm.BasculerLaVisibiliteCommand.Execute(Bloc(vm, "Dessus"));
        Rendu.Battre();

        Cliquer(fenetre, SurLaPage(fenetre, CentreX, CentreY));
        Assert.Equal("Dessous", vm.Selected?.Name);

        // Seul, masqué, sur du vide : le clic ne prend rien, et le pointeur, qui annonçait
        // un déplacement, n'annonce plus rien (repris après relecture, 26 octobre 2026 : ce
        // résumé le promettait, le test ne le lisait pas).
        vm.SetSelection([]);
        Assert.Same(CurseurDeDeplacement, CurseurAuSurvol(fenetre, CentreX, CentreY));
        vm.BasculerLaVisibiliteCommand.Execute(Bloc(vm, "Dessous"));
        Assert.NotSame(CurseurDeDeplacement, CurseurAuSurvol(fenetre, CentreX, CentreY));
        Cliquer(fenetre, SurLaPage(fenetre, CentreX, CentreY));
        Assert.Empty(vm.Selection);
    }

    /// <summary>Le curseur que la planche pose sur un bloc qu'on peut saisir — le sien, et non un égal.</summary>
    private static Cursor CurseurDeDeplacement =>
        (Cursor)typeof(PageCanvas).GetField("MoveCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;

    /// <summary>Le curseur de la couche de saisie, le pointeur posé sur ce point de la page.</summary>
    private static Cursor? CurseurAuSurvol(Window fenetre, double x, double y)
    {
        fenetre.MouseMove(SurLaPage(fenetre, x + 1, y + 1));
        fenetre.MouseMove(SurLaPage(fenetre, x, y));
        Rendu.Battre();
        return Trouver<Border>(fenetre, "InputLayer").Cursor;
    }

    /// <summary>
    /// Repris après relecture. Un bloc masqué pris dans la liste n'a ni cadre ni poignée sur
    /// la planche ; une pression là où serait sa poignée ne le redimensionne pas non plus.
    /// La contre-épreuve : visible, le même geste le redimensionne.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void La_poignee_d_un_bloc_masque_ne_se_saisit_pas()
    {
        var bloc = new ShapeFrame { Name = "Caché", X = 100, Y = 100, Width = 100, Height = 60 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        void TirerLeCoin()
        {
            vm.SelectOnly(bloc);
            Rendu.Battre();
            var coin = SurLaPage(fenetre, bloc.X + bloc.Width, bloc.Y + bloc.Height);
            fenetre.MouseDown(coin, MouseButton.Left);
            fenetre.MouseMove(new Point(coin.X + 20, coin.Y + 10), RawInputModifiers.LeftMouseButton);
            fenetre.MouseMove(new Point(coin.X + 40, coin.Y + 30), RawInputModifiers.LeftMouseButton);
            fenetre.MouseUp(new Point(coin.X + 40, coin.Y + 30), MouseButton.Left);
            Rendu.Battre();
        }

        TirerLeCoin();
        Assert.True(bloc.Width > 100, "visible, le coin tiré devait agrandir le bloc");
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        bloc = (ShapeFrame)Bloc(vm, "Caché");
        Assert.Equal(100, bloc.Width);

        vm.BasculerLaVisibiliteCommand.Execute(bloc);
        Rendu.Battre();
        var pas = vm.CanUndo;
        TirerLeCoin();
        Assert.Equal(100, bloc.Width);
        Assert.Equal(60, bloc.Height);
        vm.UndoCommand.Execute(null);
        Assert.True(Bloc(vm, "Caché").Visible, "le coin tiré d'un bloc masqué a empilé un pas");
        Assert.True(pas);
    }

    /// <summary>
    /// Repris après relecture. Un bloc de texte masqué ne se saisit pas sur la page : pris
    /// dans la liste, un Ctrl+double-clic sur la page — qui garde la sélection — n'ouvre
    /// pas sa saisie.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void Un_texte_masque_ne_s_ouvre_pas_en_saisie_sur_la_page()
    {
        var texte = new TextFrame { Name = "Caché", Text = "Escamoté", X = 100, Y = 100, Width = 200, Height = 60, Visible = false };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(texte));
        Rendu.Cliquer(NomDansLaListe(fenetre, texte));
        Assert.Same(texte, vm.SingleSelected);

        var point = SurLaPage(fenetre, texte.X + 40, texte.Y + 8);
        for (var clic = 0; clic < 2; clic++)
        {
            fenetre.MouseDown(point, MouseButton.Left, RawInputModifiers.Control);
            fenetre.MouseUp(point, MouseButton.Left, RawInputModifiers.Control);
        }

        Rendu.Battre(8);
        Assert.Same(texte, vm.SingleSelected);
        Assert.False(texte.IsEditing, "un texte masqué s'est ouvert en saisie sur la page");
    }

    /// <summary>
    /// Un bloc masqué se sélectionne dans la liste, et le dock le montre ; la planche
    /// n'en montre rien — ni cadre, ni poignée — et PageRoot garde ses pixels (règle 4 de
    /// la phase).
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void Un_bloc_masque_se_selectionne_dans_la_liste_sans_rien_montrer_sur_la_planche()
    {
        var document = Rendu.Feuille(
            new TextFrame { Name = "Caché", Text = "Escamoté", X = 100, Y = 100, Width = 200, Height = 40, Visible = false });
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var cache = Bloc(vm, "Caché");
        var avant = Rendu.Encre(fenetre).Pixels;

        // Sur le nom, et non sur l'œil : c'est la ligne qui sélectionne.
        var nom = Ligne(fenetre, cache).GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Caché");
        Cliquer(fenetre, Centre(fenetre, nom));

        Assert.Same(cache, vm.Selected);
        Assert.Same(cache, vm.SingleSelected);
        Assert.Equal(0, MarquesDeSelection(fenetre));
        Assert.Equal(avant, Rendu.Encre(fenetre).Pixels);
        Assert.False(vm.CanUndo);

        // Le même, visible : sélectionné de la même façon, il a son cadre et ses poignées.
        cache.Visible = true;
        vm.SelectOnly(cache);
        Rendu.Battre();
        Assert.True(MarquesDeSelection(fenetre) > 0);
    }

    /// <summary>
    /// Fermer l'œil d'un bloc qui fait partie d'une sélection multiple l'en retire : une
    /// sélection de plusieurs blocs ne porte que ce que la planche saisit. Seul
    /// sélectionné, il le reste.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void Masquer_un_bloc_d_une_selection_multiple_l_en_retire()
    {
        var (fenetre, vm) = Rendu.Ouvrir(DeuxBlocs());
        var dessous = Bloc(vm, "Dessous");
        var dessus = Bloc(vm, "Dessus");

        vm.SetSelection([dessous, dessus]);
        Cliquer(fenetre, Centre(fenetre, Oeil(fenetre, dessus)));
        Assert.Equal([dessous], vm.Selection);

        vm.SelectOnly(dessous);
        Cliquer(fenetre, Centre(fenetre, Oeil(fenetre, dessous)));
        Assert.Equal([dessous], vm.Selection);
        Assert.Equal(0, MarquesDeSelection(fenetre));
    }

    /// <summary>
    /// Ni l'alignement ni la répartition ne voient un bloc masqué : il ne bouge pas, et
    /// ne sert pas de repère aux autres. « Tout sélectionner » ne le prend pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void Un_bloc_masque_ne_s_aligne_ni_ne_se_repartit_ni_ne_se_prend_d_un_coup()
    {
        var (_, vm) = Rendu.Ouvrir(Rendu.Feuille(
            new ShapeFrame { Name = "A", X = 200, Y = 100, Width = 50, Height = 50 },
            new ShapeFrame { Name = "B", X = 300, Y = 200, Width = 50, Height = 50 },
            new ShapeFrame { Name = "Caché", X = 40, Y = 300, Width = 50, Height = 50, Visible = false }));
        var a = Bloc(vm, "A");
        var b = Bloc(vm, "B");
        var cache = Bloc(vm, "Caché");

        vm.SelectAllCommand.Execute(null);
        Assert.Equal([a, b], vm.Selection);

        // Même s'il est de la sélection, l'alignement l'ignore : le bord gauche commun
        // est celui de A, et non celui du bloc masqué, à 40.
        vm.AlignToMargins = false;
        vm.SetSelection([a, b, cache]);
        Assert.False(vm.CanDistribute);
        vm.AlignLeftCommand.Execute(null);
        Assert.Equal(200, a.X);
        Assert.Equal(200, b.X);
        Assert.Equal(40, cache.X);

        // Seul sélectionné, il ne s'aligne pas sur la page.
        vm.SelectOnly(cache);
        vm.AlignRightCommand.Execute(null);
        Assert.Equal(40, cache.X);
    }

    /// <summary>
    /// Un groupe garde la visibilité : dupliquer un groupe masqué donne un groupe masqué,
    /// et le dissocier laisse ses blocs masqués — dissocier ne fait rien paraître.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void Un_groupe_masque_se_duplique_et_se_dissocie_masque()
    {
        var groupe = new GroupFrame { Name = "Groupe", X = 100, Y = 100, Width = 200, Height = 100, Visible = false };
        groupe.Children.Add(new ShapeFrame { Name = "Un", Width = 100, Height = 100 });
        groupe.Children.Add(new ShapeFrame { Name = "Deux", X = 100, Width = 100, Height = 100 });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));
        var avant = Rendu.Encre(fenetre).Pixels;

        vm.SelectOnly(groupe);
        vm.DuplicateSelectedCommand.Execute(null);
        Assert.Equal(2, vm.CurrentPage!.Frames.Count);
        Assert.All(vm.CurrentPage.Frames, bloc => Assert.False(bloc.Visible));

        vm.SelectOnly(groupe);
        vm.UngroupSelectedCommand.Execute(null);
        Rendu.Battre();
        var un = Bloc(vm, "Un");
        var deux = Bloc(vm, "Deux");
        Assert.False(un.Visible);
        Assert.False(deux.Visible);
        Assert.Empty(vm.Selection);
        Assert.Equal(avant, Rendu.Encre(fenetre).Pixels);
    }

    // ------------------------------------------------------------------ la vignette

    /// <summary>
    /// La vignette de la page (I5) ne dessine pas un bloc masqué : masqué, elle rend les
    /// pixels qu'elle rend sans lui ; l'œil rouvert, ceux d'avant.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void La_vignette_de_la_page_ne_dessine_pas_un_bloc_masque()
    {
        var (fenetre, vm) = Rendu.Ouvrir(DeuxBlocs());
        VignetteDePage Vignette() => fenetre.GetVisualDescendants().OfType<VignetteDePage>().Single();
        var avant = Rendu.Bande(fenetre, Vignette());

        vm.BasculerLaVisibiliteCommand.Execute(Bloc(vm, "Dessus"));
        Rendu.Battre();
        var masque = Rendu.Bande(fenetre, Vignette());
        Assert.NotEqual(avant, masque);

        vm.BasculerLaVisibiliteCommand.Execute(Bloc(vm, "Dessus"));
        Rendu.Battre();
        Assert.Equal(avant, Rendu.Bande(fenetre, Vignette()));

        vm.SelectOnly(Bloc(vm, "Dessus"));
        vm.DeleteSelectedCommand.Execute(null);
        vm.SetSelection([]);
        Rendu.Battre();
        Assert.Equal(masque, Rendu.Bande(fenetre, Vignette()));
    }

    /// <summary>
    /// La vignette retient les filets de chaque bloc de texte par son rang dans le parcours
    /// (I5), et un bloc masqué n'est pas parcouru : rouvrir son œil décale les rangs de
    /// tous les blocs de texte qui le suivent. Elle doit alors tout recomposer — sans quoi
    /// le bloc rouvert reprendrait les filets de son suivant. Ce qu'elle montre après
    /// est ce que montre une vignette neuve du même document.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void Rouvrir_l_oeil_d_un_texte_recompose_les_filets_de_la_vignette()
    {
        Document Deux(bool premierVisible) => Rendu.Feuille(
            new TextFrame { Name = "Premier", Text = "Court", X = 60, Y = 60, Width = 400, Height = 80, FontSize = 24, Visible = premierVisible },
            new TextFrame
            {
                Name = "Second", X = 60, Y = 400, Width = 400, Height = 300, FontSize = 24,
                Text = "Un texte bien plus long, qui compose plusieurs lignes dans la vignette de sa page.",
            });
        static byte[] Bande(Window fenetre) =>
            Rendu.Bande(fenetre, fenetre.GetVisualDescendants().OfType<VignetteDePage>().Single());

        var (fenetre, vm) = Rendu.Ouvrir(Deux(premierVisible: false));
        _ = Bande(fenetre);
        vm.BasculerLaVisibiliteCommand.Execute(Bloc(vm, "Premier"));
        Rendu.Battre();
        var rouvert = Bande(fenetre);
        fenetre.Close();

        var (neuve, _) = Rendu.Ouvrir(Deux(premierVisible: true));
        Assert.Equal(Bande(neuve), rouvert);
    }

    // ------------------------------------------------------------------ repris après relecture

    /// <summary>Le nom d'un bloc dans sa ligne : c'est là qu'on clique pour le sélectionner.</summary>
    private static TextBlock NomDansLaListe(Window fenetre, Frame bloc) =>
        Ligne(fenetre, bloc).GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == bloc.DisplayName);

    /// <summary>Entre en saisie sur la page par un double-clic, comme <c>FrappeTests</c>.</summary>
    private static void EntrerEnSaisie(Window fenetre, MainWindowViewModel vm, TextFrame bloc)
    {
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var point = SurLaPage(fenetre, bloc.X + 40, bloc.Y + 8);
        for (var clic = 0; clic < 2; clic++)
        {
            fenetre.MouseDown(point, MouseButton.Left);
            fenetre.MouseUp(point, MouseButton.Left);
        }

        Rendu.Battre(8);
        Assert.True(bloc.IsEditing, "le double-clic n'a pas ouvert la saisie sur la page");
    }

    /// <summary>
    /// Un clic sur l'œil d'un bloc. Ce que la pression poste passe, dans l'application, tantôt
    /// avant le relâchement, tantôt après : deux événements que le serveur d'affichage livre
    /// d'un même trait ne laissent rien passer entre eux. Les deux ordres sont joués.
    /// </summary>
    private static void CliquerLOeil(Window fenetre, Frame bloc, bool entreLesDeux)
    {
        var oeil = Oeil(fenetre, bloc);
        if (!entreLesDeux)
        {
            Rendu.Cliquer(oeil);
            return;
        }

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Rendu.Battre();
        var centre = Centre(fenetre, oeil);
        fenetre.MouseDown(centre, MouseButton.Left);
        Rendu.Battre();
        fenetre.MouseUp(centre, MouseButton.Left);
        Rendu.Battre();
    }

    /// <summary>
    /// Repris après relecture (26 octobre 2026). Une sélection de plusieurs blocs ne porte
    /// que ce que la planche saisit ; or un bloc masqué pris seul dans la liste, puis un
    /// Ctrl+clic ou un Maj+clic sur un bloc de la planche, faisaient une sélection de deux
    /// blocs dont l'un ne se voit pas — et Suppr emportait les deux. Étendue depuis un bloc
    /// masqué, la sélection le lâche.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void Etendre_la_selection_depuis_un_bloc_masque_le_lache()
    {
        var (fenetre, vm) = Rendu.Ouvrir(DeuxBlocs());
        var dessous = Bloc(vm, "Dessous");
        var dessus = Bloc(vm, "Dessus");
        vm.BasculerLaVisibiliteCommand.Execute(dessus);
        Rendu.Battre();

        foreach (var touche in new[] { RawInputModifiers.Control, RawInputModifiers.Shift })
        {
            Rendu.Cliquer(NomDansLaListe(fenetre, dessus));
            Assert.Equal([dessus], vm.Selection);

            Cliquer(fenetre, SurLaPage(fenetre, CentreX, CentreY), touche);
            Assert.Equal([dessous], vm.Selection);
        }

        fenetre.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Rendu.Battre();
        Assert.Equal([dessus], vm.CurrentPage!.Frames);
    }

    /// <summary>
    /// Repris après relecture. Fermer l'œil du bloc qu'on tape sur la page le laissait en
    /// saisie : invisible mais « IsEditing », la couche de saisie éteinte, Échap sans effet,
    /// et le clic suivant sur la planche ne servait qu'à sortir de cette saisie fantôme. Un
    /// bloc masqué ne se saisit pas sur la page : la saisie se referme, la frappe faite
    /// garde son pas, l'œil a le sien, et le clic suivant prend le bloc qu'il vise.
    /// </summary>
    [AvaloniaTheory]
    [Trait("poste", "I8")]
    [InlineData(false)]
    [InlineData(true)]
    public void Fermer_l_oeil_du_bloc_qu_on_tape_referme_la_saisie(bool entreLesDeux)
    {
        var texte = new TextFrame { Name = "Texte", Text = "abc", X = 100, Y = 100, Width = 200, Height = 60 };
        var voisin = new ShapeFrame { Name = "Voisin", X = 100, Y = 300, Width = 100, Height = 100, Fill = new SolidFill("#FF000000") };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(voisin, texte));

        EntrerEnSaisie(fenetre, vm, texte);
        fenetre.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.Control);
        fenetre.KeyTextInput("X");
        Rendu.Battre();
        CliquerLOeil(fenetre, texte, entreLesDeux);

        Assert.False(texte.Visible);
        Assert.False(texte.IsEditing, "le bloc masqué est resté en saisie sur la page");
        Assert.Equal("abcX", texte.Text);

        Cliquer(fenetre, SurLaPage(fenetre, 150, 350));
        Assert.Equal("Voisin", vm.Selected?.Name);

        vm.UndoCommand.Execute(null);
        Assert.True(Bloc(vm, "Texte").Visible);
        Assert.Equal("abcX", ((TextFrame)Bloc(vm, "Texte")).Text);
        vm.UndoCommand.Execute(null);
        Assert.Equal("abc", ((TextFrame)Bloc(vm, "Texte")).Text);
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// Repris après relecture. L'œil ne prend pas le focus : la frappe en cours garde le
    /// sien. Le bouton n'est pas focalisable, mais la pression donnait le focus à sa ligne,
    /// premier ancêtre qui l'est : le champ du dock qu'on tapait le perdait, et la lettre
    /// suivante armait un outil au lieu de s'écrire. Chaque geste garde son pas : le début
    /// du nom, l'œil, la fin du nom.
    /// </summary>
    [AvaloniaTheory]
    [Trait("poste", "I8")]
    [InlineData(false)]
    [InlineData(true)]
    public void L_oeil_laisse_le_focus_au_champ_du_dock_qu_on_tape(bool entreLesDeux)
    {
        var (fenetre, vm) = Rendu.Ouvrir(DeuxBlocs());
        var dessous = Bloc(vm, "Dessous");
        var dessus = Bloc(vm, "Dessus");
        vm.SelectOnly(dessous);
        Rendu.Battre();

        var nom = Trouver<TextBox>(fenetre, "ChampNom");
        Rendu.Cliquer(nom);
        fenetre.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        fenetre.KeyTextInput("1");
        Rendu.Battre();
        CliquerLOeil(fenetre, dessus, entreLesDeux);

        Assert.False(dessus.Visible);
        Assert.Same(nom, fenetre.FocusManager?.GetFocusedElement());
        fenetre.KeyTextInput("2");
        Rendu.Battre();
        Assert.Equal("Dessous12", dessous.Name);
        Assert.Equal(EditorTool.Select, vm.ActiveTool);

        vm.SetSelection([]);
        Rendu.Battre();
        vm.UndoCommand.Execute(null);
        Assert.Equal("Dessous1", Bloc(vm, "Dessous1").Name);
        vm.UndoCommand.Execute(null);
        Assert.True(Bloc(vm, "Dessus").Visible);
        vm.UndoCommand.Execute(null);
        Assert.Equal("Dessous", Bloc(vm, "Dessous").Name);
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// Repris après relecture. Pendant une saisie sur la page, l'œil d'un autre bloc le
    /// masque sans interrompre la frappe : le bloc qu'on tape reste en saisie, avec le
    /// focus, et la suite du texte y arrive. Trois pas : avant l'œil, l'œil, après.
    /// </summary>
    [AvaloniaTheory]
    [Trait("poste", "I8")]
    [InlineData(false)]
    [InlineData(true)]
    public void L_oeil_d_un_voisin_laisse_la_saisie_sur_la_page_se_poursuivre(bool entreLesDeux)
    {
        var texte = new TextFrame { Name = "Texte", Text = "abc", X = 100, Y = 100, Width = 200, Height = 60 };
        var voisin = new ShapeFrame { Name = "Voisin", X = 100, Y = 300, Width = 100, Height = 100 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(voisin, texte));

        EntrerEnSaisie(fenetre, vm, texte);
        fenetre.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.Control);
        fenetre.KeyTextInput("X");
        Rendu.Battre();
        CliquerLOeil(fenetre, voisin, entreLesDeux);

        Assert.False(voisin.Visible);
        Assert.True(texte.IsEditing);
        fenetre.KeyTextInput("Y");
        Rendu.Battre();
        Assert.Equal("abcXY", texte.Text);
        fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Rendu.Battre();
        Assert.False(texte.IsEditing);

        vm.UndoCommand.Execute(null);
        Assert.Equal("abcX", ((TextFrame)Bloc(vm, "Texte")).Text);
        Assert.False(Bloc(vm, "Voisin").Visible);
        vm.UndoCommand.Execute(null);
        Assert.True(Bloc(vm, "Voisin").Visible);
        vm.UndoCommand.Execute(null);
        Assert.Equal("abc", ((TextFrame)Bloc(vm, "Texte")).Text);
        Assert.False(vm.CanUndo);
    }

    // ------------------------------------------------------------------ l'import

    /// <summary>Un <c>.pub</c> importé n'a que des blocs visibles, jusque dans ses groupes.</summary>
    [AvaloniaFact]
    [Trait("poste", "I8")]
    public void Un_pub_importe_n_a_que_des_blocs_visibles()
    {
        var document = Rendu.Ata21();

        static IEnumerable<Frame> Tous(IEnumerable<Frame> blocs) =>
            blocs.SelectMany(b => b is GroupFrame g ? Tous(g.Children).Prepend(b) : [b]);

        var blocs = document.Pages.SelectMany(p => Tous(p.Frames)).ToList();
        Assert.NotEmpty(blocs);
        Assert.All(blocs, bloc => Assert.True(bloc.Visible));
    }
}
