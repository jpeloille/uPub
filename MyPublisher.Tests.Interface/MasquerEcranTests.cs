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

        // Seul, masqué, sur du vide : le clic ne prend rien.
        vm.BasculerLaVisibiliteCommand.Execute(Bloc(vm, "Dessous"));
        Cliquer(fenetre, SurLaPage(fenetre, CentreX, CentreY));
        Assert.Empty(vm.Selection);
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
