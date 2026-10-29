using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// L'application connaît la sélection du texte — le poste N15 (fiche 23, b1 et b4).
///
/// <c>CaretIndex</c> et <c>SelectionStart</c> n'apparaissaient que dans les tests : seule la
/// planche tenait le champ de la saisie, et le modèle de vue ne savait pas ce qui était
/// sélectionné dans le texte. Désormais la planche le lui dit — le bloc, le début, la fin, le
/// curseur —, à chaque geste, et le vide à la sortie de la saisie ; le modèle de vue en tire les
/// paragraphes que la sélection touche. La sélection survit quand le focus va à la barre : le
/// champ, lui, l'oublie en perdant le focus. C'est ce que R6, R7, N20 et N21 liront.
///
/// Les gestes sont ceux du banc (<see cref="Rendu"/>, poste N1). Les points visés sont calculés
/// sur la composition (N3), comme ceux de <see cref="ClicDansLeTexteTests"/>.
/// </summary>
public class SelectionDuTexteTests
{
    // ------------------------------------------------------------------ le banc du poste

    private static TextFrame Texte(string nom = "Texte", double x = 60, double y = 60, string texte = "Bonjour le monde") => new()
    {
        Name = nom, X = x, Y = y, Width = 300, Height = 120, Text = texte,
        FontFamily = "DejaVu Sans", FontSize = 14, Foreground = "#FF000000",
    };

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    /// <summary>Le champ de saisie d'un champ numérique : c'est lui qui prend le focus.</summary>
    private static TextBox SaisieDe(NumericUpDown champ) => champ.GetVisualDescendants().OfType<TextBox>().First();

    /// <summary>La géométrie du texte du bloc, telle que la planche la compose (poste N3).</summary>
    private static GeometrieDuTexte Geometrie(Window fenetre, TextFrame bloc) =>
        fenetre.GetVisualDescendants().OfType<TexteCompose>().Single(c => ReferenceEquals(c.Bloc, bloc)).Geometrie()
        ?? throw new InvalidOperationException($"« {bloc.Name} » n'a pas de géométrie");

    /// <summary>
    /// Le point du caractère <paramref name="indice"/> de la composition, à la fraction
    /// <paramref name="fx"/> de sa largeur, à mi-hauteur de sa ligne, dans la fenêtre.
    /// </summary>
    private static Point SurLeCaractere(Window fenetre, TextFrame bloc, int indice, double fx)
    {
        var geometrie = Geometrie(fenetre, bloc);
        var avant = geometrie.PointDe(indice);
        var apres = geometrie.PointDe(indice + 1);
        var ligne = geometrie.LigneDe(indice);
        var (x, y) = bloc.ToPage(avant.X + fx * (apres.X - avant.X), (ligne.Haut + ligne.LigneDeBase) / 2);
        return Rendu.SurLaPage(fenetre, x, y);
    }

    /// <summary>
    /// Un glissé de la composition qui couvre les caractères [<paramref name="debut"/>,
    /// <paramref name="fin"/>) : la pression juste avant le milieu du premier, le relâché juste
    /// après le milieu du dernier, comme le glissé de N12. Il ouvre la saisie.
    /// </summary>
    private static TextBox GlisserSur(Window fenetre, TextFrame bloc, int debut, int fin)
    {
        Rendu.Glisser(fenetre, SurLeCaractere(fenetre, bloc, debut, 0.45), SurLeCaractere(fenetre, bloc, fin - 1, 0.55));
        Assert.True(bloc.IsEditing, "le glissé dans le texte n'a pas ouvert la saisie");
        return Rendu.ZoneDeSaisie(fenetre, bloc)!;
    }

    /// <summary>La sélection du modèle de vue : (début, fin), et le curseur.</summary>
    private static ((int, int), int) Lue(MainWindowViewModel vm)
    {
        var selection = vm.SelectionDuTexte ?? throw new InvalidOperationException("le modèle de vue n'a pas de sélection du texte");
        return ((selection.Debut, selection.Fin), selection.Curseur);
    }

    // ------------------------------------------------------------------ la recette

    /// <summary>
    /// La recette, d'un trait : un glissé sur les caractères 4 à 7 donne (4, 7) ; <c>Maj+→</c>
    /// donne (4, 8) ; une frappe donne (5, 5). Le modèle de vue suit le champ à chaque geste, le
    /// bloc et le curseur compris.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N15")]
    public void Un_glisse_puis_Maj_fleche_puis_une_frappe_sont_suivis_par_le_modele_de_vue()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        Assert.Null(vm.SelectionDuTexte);

        var zone = GlisserSur(fenetre, bloc, 4, 7);
        Assert.Equal("our", zone.SelectedText);
        Assert.Equal(((4, 7), 7), Lue(vm));
        Assert.Same(bloc, vm.SelectionDuTexte!.Bloc);
        Assert.Null(vm.SelectionDuTexte.Case);
        Assert.Equal(new Plage(4, 3), vm.SelectionDuTexte.Plage);
        Assert.Equal(4, vm.SelectionDuTexte.Ancre);

        Rendu.Touche(fenetre, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        Assert.Equal("our ", zone.SelectedText);
        Assert.Equal(((4, 8), 8), Lue(vm));

        Rendu.Taper(fenetre, "x");
        Assert.Equal("Bonjxle monde", bloc.Text);
        Assert.Equal(((5, 5), 5), Lue(vm));
        Assert.True(vm.SelectionDuTexte!.EstVide);
    }

    /// <summary>
    /// Le glissé à rebours donne la même plage, le curseur au début : c'est lui qui dit le sens,
    /// et <c>Maj+←</c> l'étend de ce côté-là.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N15")]
    public void Un_glisse_a_rebours_donne_la_meme_plage_le_curseur_au_debut()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        Rendu.Glisser(fenetre, SurLeCaractere(fenetre, bloc, 6, 0.55), SurLeCaractere(fenetre, bloc, 4, 0.45));
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
        Assert.Equal((7, 4), (zone.SelectionStart, zone.SelectionEnd));
        Assert.Equal(((4, 7), 4), Lue(vm));
        Assert.Equal(7, vm.SelectionDuTexte!.Ancre);

        Rendu.Touche(fenetre, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
        Assert.Equal(((3, 7), 3), Lue(vm));
    }

    /// <summary>
    /// Le clic qui ouvre la saisie (N12) donne le curseur seul, au caractère visé ; une flèche
    /// le déplace ; un clic dans un autre bloc donne la sélection de celui-là.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N15")]
    public void Le_clic_d_entree_et_les_fleches_donnent_le_curseur_seul_dans_le_bloc_vise()
    {
        var bloc = Texte();
        var autre = Texte("Autre", 60, 260, "Second bloc");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, autre));

        Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, bloc, 4, 0.3));
        Assert.Equal(((4, 4), 4), Lue(vm));
        Assert.Same(bloc, vm.SelectionDuTexte!.Bloc);

        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        Assert.Equal(((6, 6), 6), Lue(vm));

        Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, autre, 2, 0.3));
        Assert.True(autre.IsEditing, "le clic dans l'autre bloc n'y est pas entré");
        Assert.False(bloc.IsEditing);
        Assert.Same(autre, vm.SelectionDuTexte!.Bloc);
        Assert.Equal(((2, 2), 2), Lue(vm));
    }

    // ------------------------------------------------------------------ le focus et la sortie

    /// <summary>
    /// La recette : le focus pris par le champ Corps ne change rien. Le champ de la saisie, lui,
    /// a oublié sa sélection en perdant le focus — c'est pour cela que le modèle de vue garde la
    /// sienne : le corps réglé à la barre (R7) doit s'appliquer à ce que l'utilisateur avait
    /// sélectionné. La saisie reste ouverte, et Échap dans le corps rend la planche sans y
    /// toucher non plus.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N15")]
    public void Le_focus_pris_par_le_champ_Corps_ne_change_rien_a_la_selection()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = GlisserSur(fenetre, bloc, 4, 7);
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        Assert.Equal(((4, 8), 8), Lue(vm));

        var corps = SaisieDe(Trouver<NumericUpDown>(fenetre, "CorpsDuBloc"));
        Rendu.Cliquer(corps);
        Assert.True(corps.IsFocused, "le clic n'a pas donné le focus au corps");
        Assert.True(bloc.IsEditing, "le clic dans la barre a refermé la saisie");

        // Ce que le champ de la saisie a fait de sa sélection, et ce que le modèle de vue garde.
        Assert.Equal(zone.SelectionStart, zone.SelectionEnd);
        Assert.Equal(((4, 8), 8), Lue(vm));
        Assert.Equal(new Plage(0, 1), vm.ParagraphesDeLaSelection);

        Rendu.Taper(fenetre, "1");
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(corps.IsKeyboardFocusWithin, "Échap n'a pas quitté le corps");
        Assert.True(bloc.IsEditing);
        Assert.Equal(((4, 8), 8), Lue(vm));
    }

    /// <summary>
    /// La recette : Échap vide la sélection. De même un clic hors du bloc, et un changement de
    /// sélection des blocs : la saisie refermée, il n'y a plus de sélection du texte, ni de
    /// paragraphes.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N15")]
    public void Echap_un_clic_dehors_ou_une_autre_selection_vident_la_selection_du_texte()
    {
        var bloc = Texte();
        var forme = new ShapeFrame { Name = "Forme", X = 100, Y = 400, Width = 120, Height = 100, Fill = new SolidFill("#FF336699") };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, forme));

        GlisserSur(fenetre, bloc, 4, 7);
        Assert.NotNull(vm.SelectionDuTexte);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(bloc.IsEditing);
        Assert.Null(vm.SelectionDuTexte);
        Assert.Null(vm.ParagraphesDeLaSelection);

        GlisserSur(fenetre, bloc, 4, 7);
        Assert.NotNull(vm.SelectionDuTexte);
        Rendu.Cliquer(fenetre, Rendu.Centre(fenetre, forme));
        Assert.False(bloc.IsEditing);
        Assert.Null(vm.SelectionDuTexte);

        GlisserSur(fenetre, bloc, 4, 7);
        Assert.NotNull(vm.SelectionDuTexte);
        vm.SetSelection([forme]);
        Rendu.Battre();
        Assert.False(bloc.IsEditing);
        Assert.Null(vm.SelectionDuTexte);
    }

    /// <summary>
    /// Le texte changé ailleurs pendant que le champ de la saisie n'a pas le focus — au champ
    /// Contenu du dock, lié au même bloc — ramène la sélection gardée dans les bornes du texte.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N15")]
    public void Le_texte_raccourci_au_dock_ramene_la_selection_dans_ses_bornes()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        GlisserSur(fenetre, bloc, 4, 7);
        Rendu.Onglet(fenetre, "Propriétés");

        var contenu = Trouver<TextBox>(fenetre, "SaisieDuTexte");
        Rendu.Cliquer(contenu);
        Assert.True(contenu.IsFocused, "le clic n'a pas donné le focus au champ Contenu");
        Assert.True(bloc.IsEditing);
        Assert.Equal(((4, 7), 7), Lue(vm));

        contenu.SelectAll();
        Rendu.Taper(fenetre, "abc");
        Assert.Equal("abc", bloc.Text);
        Assert.Equal(((3, 3), 3), Lue(vm));
    }

    // ------------------------------------------------------------------ les paragraphes

    /// <summary>
    /// Les paragraphes de la sélection, comptés comme les écarts (<c>Recalage.ParagrapheDe</c>) :
    /// le curseur dans le 2ᵉ de trois paragraphes donne le 2ᵉ ; une sélection du 2ᵉ au 3ᵉ donne
    /// les deux ; une sélection qui finit sur le saut du 2ᵉ ne touche pas le 3ᵉ.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N15")]
    public void Les_paragraphes_de_la_selection_sont_comptes_comme_les_ecarts()
    {
        var bloc = Texte(texte: "Un\nDeux\nTrois");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, bloc, 4, 0.3));
        Assert.Equal(((4, 4), 4), Lue(vm));
        Assert.Equal(new Plage(1, 1), vm.ParagraphesDeLaSelection);

        Rendu.Touche(fenetre, PhysicalKey.End, RawInputModifiers.Shift);
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        Assert.Equal(((4, 8), 8), Lue(vm));
        Assert.Equal(new Plage(1, 1), vm.ParagraphesDeLaSelection);

        Rendu.Touche(fenetre, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        Assert.Equal(((4, 9), 9), Lue(vm));
        Assert.Equal(new Plage(1, 2), vm.ParagraphesDeLaSelection);
    }

    /// <summary>
    /// La sélection d'un champ, de l'ancre au curseur, ramenée à la forme du modèle de vue : le
    /// début avant la fin, le tout dans les bornes du texte, le curseur au bout qui bouge. Une case de tableau n'a pas de
    /// paragraphes.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N15")]
    public void La_selection_d_un_champ_se_range_debut_avant_fin_dans_les_bornes()
    {
        var bloc = new TextFrame { Text = "Bonjour" };
        Assert.Equal(new SelectionDuTexte(bloc, null, 2, 5, 2), SelectionDuTexte.De(bloc, null, 5, 2, 7));
        Assert.Equal(new SelectionDuTexte(bloc, null, 2, 5, 5), SelectionDuTexte.De(bloc, null, 2, 5, 7));
        Assert.Equal(new SelectionDuTexte(bloc, null, 2, 7, 7), SelectionDuTexte.De(bloc, null, 2, 40, 7));
        Assert.Equal(new SelectionDuTexte(bloc, null, 0, 0, 0), SelectionDuTexte.De(bloc, null, -3, -1, 7));
        Assert.Equal(5, SelectionDuTexte.De(bloc, null, 5, 2, 7).Ancre);
        Assert.Equal("Bonjour", SelectionDuTexte.De(bloc, null, 0, 0, 7).Texte);

        var tableau = new TableFrame();
        var case_ = new TableCell { Text = "case" };
        var vm = new MainWindowViewModel();
        vm.SuivreLaSelectionDuTexte(SelectionDuTexte.De(tableau, case_, 1, 3, 4));
        Assert.Equal("case", vm.SelectionDuTexte!.Texte);
        Assert.Null(vm.ParagraphesDeLaSelection);
    }
}
