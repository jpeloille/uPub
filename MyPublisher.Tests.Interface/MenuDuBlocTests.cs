using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
/// Le menu contextuel du bloc — poste A12 (fiche 11 ; fiche 23, c3, c16, c25, e1, e2, f16).
///
/// Un clic droit sur un bloc le sélectionne et ouvre un menu qui reprend les commandes des menus
/// Edition, Disposition, Texte et Affichage, chaque entrée liée à la même commande que son pendant,
/// avec le même geste affiché ; « Format du bloc… » mène à la section du dock propre à la sorte du
/// bloc, comme le double-clic sur une image ou une forme. Dans le vide, le menu de la page ; en
/// saisie, celui du texte. <c>Maj+F10</c> et la touche Menu l'ouvrent sous la sélection ; les règles
/// et les barres de défilement n'ouvrent rien.
///
/// Tout se joue sur la vraie fenêtre, aux gestes du banc (poste N1). La recette du poste —
/// <c>Scripts/recettes/A12-menu-contextuel.sh</c> — confronte en plus, dans les sources, chaque
/// entrée du menu à son pendant aux menus.
/// </summary>
[Trait("poste", "A12")]
public class MenuDuBlocTests
{
    // ------------------------------------------------------------------ outillage

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static ShapeFrame Forme(string nom, double x, double y) =>
        new() { Name = nom, X = x, Y = y, Width = 120, Height = 80 };

    private static TextFrame Texte(string nom, double x, double y, string texte = "Bonjour le monde") => new()
    {
        Name = nom, X = x, Y = y, Width = 260, Height = 90, Text = texte,
        FontFamily = "DejaVu Sans", FontSize = 14, Foreground = "#FF000000",
    };

    private static PageCanvas Planche(Window fenetre) => fenetre.GetVisualDescendants().OfType<PageCanvas>().Single();

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom && c.IsEffectivelyVisible)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas visible dans la fenêtre");

    /// <summary>Les libellés des entrées que le menu montre, sans ses séparateurs.</summary>
    private static string[] Visibles(ContextMenu menu) =>
        menu.Items.OfType<MenuItem>().Where(e => e.IsVisible).Select(e => $"{e.Header}").ToArray();

    private static MenuItem Entree(ItemsControl menu, string libelle) =>
        menu.Items.OfType<MenuItem>().SingleOrDefault(e => Equals(e.Header, libelle))
        ?? throw new InvalidOperationException($"le menu n'a pas d'entrée « {libelle} »");

    /// <summary>Les séparateurs que le menu montre : aucun en tête, en queue, ni deux à la suite.</summary>
    private static void SeparateursAuJuste(ContextMenu menu)
    {
        var montrees = menu.Items.OfType<Control>().Where(e => e.IsVisible).ToList();
        Assert.False(montrees.FirstOrDefault() is Separator, "le menu commence par un séparateur");
        Assert.False(montrees.LastOrDefault() is Separator, "le menu finit par un séparateur");
        for (var i = 1; i < montrees.Count; i++)
        {
            Assert.False(montrees[i] is Separator && montrees[i - 1] is Separator, "deux séparateurs à la suite");
        }
    }

    private static List<ContextMenu> MenusOuverts(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<ContextMenu>().Where(m => m.IsOpen).ToList();

    private static void Refermer(ContextMenu menu)
    {
        menu.Close();
        Rendu.UneImage();
    }

    private static string? GesteAffiche(MenuItem entree) =>
        entree.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == GestesEnFrancais.PartieDuGeste).Text;

    private static Visual? LeFocus(Window fenetre) => fenetre.FocusManager?.GetFocusedElement() as Visual;

    private static bool Dans(Visual? element, Control conteneur) =>
        element is not null && (ReferenceEquals(element, conteneur) || conteneur.IsVisualAncestorOf(element));

    /// <summary>Toutes les entrées d'un niveau de menu et de ses sous-menus.</summary>
    private static IEnumerable<MenuItem> Toutes(ItemsControl menu) =>
        menu.Items.OfType<MenuItem>().SelectMany(e => new[] { e }.Concat(Toutes(e)));

    // ------------------------------------------------------------ le clic droit, la sélection

    /// <summary>
    /// La recette d'origine : un clic droit au centre d'un bloc le sélectionne et ouvre le menu — le
    /// menu de la planche, et non celui de sa couche. Le bloc était hors de la sélection : il est pris
    /// seul. Le menu des blocs commence par Couper, Copier, Coller, comme celui de Publisher, et ne
    /// montre rien du vide ni de la saisie. Le document ne change pas, et rien ne s'annule.
    /// </summary>
    [AvaloniaFact]
    public void Un_clic_droit_au_centre_d_un_bloc_le_prend_seul_et_ouvre_le_menu()
    {
        var a = Forme("A", 60, 60);
        var b = Forme("B", 320, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b));
        vm.SelectOnly(a);
        Rendu.Battre();
        var avant = Etat(vm);

        var menu = Rendu.CliquerDroit(fenetre, Rendu.Centre(fenetre, b));

        Assert.Same(Planche(fenetre).ContextMenu, menu);
        Assert.Equal([b], vm.Selection);
        Assert.Equal(new[]
        {
            "Co_uper", "_Copier", "C_oller", "_Supprimer", "O_rdre", "Faire _pivoter ou retourner",
            "_Grouper", "_Dissocier", "R_egrouper", "A_ligner ou répartir", "Format du _bloc…", "_Zoom",
        }, Visibles(menu));
        SeparateursAuJuste(menu);
        Assert.True(Entree(menu, "Format du _bloc…").IsEffectivelyEnabled);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "le clic droit a empilé un pas");
    }

    /// <summary>
    /// Un clic droit sur un bloc d'une sélection la garde entière : le menu vaut pour l'ensemble —
    /// Grouper s'offre, Format du bloc…, qui ne vise qu'un bloc seul, est grisé. Il ne glisse rien :
    /// le bouton droit tenu et mené ailleurs ne déplace aucun bloc.
    /// </summary>
    [AvaloniaFact]
    public void Un_clic_droit_dans_la_selection_la_garde_et_ne_glisse_rien()
    {
        var a = Forme("A", 60, 60);
        var b = Forme("B", 320, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b));
        vm.SetSelection([a, b]);
        Rendu.Battre();
        var avant = Etat(vm);

        var menu = Rendu.CliquerDroit(fenetre, Rendu.Centre(fenetre, a));

        Assert.Equal(2, vm.Selection.Count);
        Assert.Contains(a, vm.Selection);
        Assert.Contains(b, vm.Selection);
        Assert.True(Entree(menu, "_Grouper").IsEffectivelyEnabled, "Grouper ne s'offre pas aux deux blocs");
        Assert.False(Entree(menu, "Format du _bloc…").IsEffectivelyEnabled, "Format du bloc… s'offre à deux blocs");
        Refermer(menu);

        // Le bouton droit tenu, mené à 80 px : aucun glissé.
        var prise = Rendu.Centre(fenetre, a);
        fenetre.MouseDown(prise, MouseButton.Right);
        fenetre.MouseMove(prise + new Vector(80, 40), RawInputModifiers.RightMouseButton);
        fenetre.MouseUp(prise + new Vector(80, 40), MouseButton.Right);
        Rendu.UneImage();
        Refermer(Rendu.MenuOuvert(fenetre, "le clic droit glissé"));

        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// La recette d'origine : un clic droit hors de tout bloc vide la sélection — sur la page comme
    /// sur le gris — et le menu est celui de la page, comme chez Publisher : Coller, les repères de
    /// mise en page, l'arrière-plan.
    /// </summary>
    [AvaloniaFact]
    public void Un_clic_droit_dans_le_vide_vide_la_selection_et_ouvre_le_menu_de_la_page()
    {
        var a = Forme("A", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a));

        foreach (var (ou, point) in new[]
                 {
                     ("la page", Rendu.SurLaPage(fenetre, 400, 500)),
                     ("le gris", Rendu.SurLaPage(fenetre, -20, 300)),
                 })
        {
            vm.SelectOnly(a);
            Rendu.Battre();

            var menu = Rendu.CliquerDroit(fenetre, point);

            Assert.True(vm.Selection.Count == 0, $"un clic droit sur {ou} n'a pas vidé la sélection");
            Assert.Equal(new[] { "C_oller", "Repères de m_ise en page…", "_Arrière-plan…" }, Visibles(menu));
            SeparateursAuJuste(menu);
            Refermer(menu);
        }

        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// Le menu se ferme, le focus revient à la planche : la touche suivante — Suppr — y joue, comme
    /// avant le clic droit.
    /// </summary>
    [AvaloniaFact]
    public void Le_menu_referme_rend_le_focus_a_la_planche()
    {
        var a = Forme("A", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a));

        var menu = Rendu.CliquerDroit(fenetre, Rendu.Centre(fenetre, a));
        Assert.False(Dans(LeFocus(fenetre), Planche(fenetre)), "le menu ouvert n'a pas pris le focus");
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UneImage();

        Assert.False(menu.IsOpen, "Échap n'a pas refermé le menu");
        Assert.True(Dans(LeFocus(fenetre), Planche(fenetre)), $"le focus n'est pas revenu à la planche ({LeFocus(fenetre)})");
        Rendu.Touche(fenetre, PhysicalKey.Delete);
        Assert.Empty(vm.CurrentPage!.Frames);
    }

    // --------------------------------------------------------- les entrées et leurs commandes

    /// <summary>
    /// La recette : chaque entrée du menu est liée à la même commande que son pendant aux menus — la
    /// même instance, au même paramètre —, avec le même geste, et le même geste affiché, écrit en
    /// français (N13). Seules les trois entrées neuves n'ont pas de pendant. Les entrées qui passent
    /// par un gestionnaire de la fenêtre — Couper, Copier, Coller, Importer un texte…, Modifier les
    /// coordonnées… — sont confrontées dans les sources par la recette, et jouées plus bas.
    /// </summary>
    [AvaloniaFact]
    public void Chaque_entree_est_liee_a_la_commande_de_son_pendant_avec_le_meme_geste_affiche()
    {
        var a = Forme("A", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a));
        var menu = Rendu.CliquerDroit(fenetre, Rendu.Centre(fenetre, a));

        var barre = Trouver<Menu>(fenetre, "Menus");
        var auxMenus = Toutes(barre).ToList();
        var neuves = new[] { vm.FormatDuBlocCommand, vm.SupprimerLeTexteCommand, vm.ConvertirEnTexteCommand };
        var confrontees = 0;
        foreach (var entree in Toutes(menu).Where(e => e.Command is not null))
        {
            var pendants = auxMenus.Where(m => ReferenceEquals(m.Command, entree.Command) &&
                                               Equals(m.CommandParameter, entree.CommandParameter)).ToList();
            if (neuves.Contains(entree.Command))
            {
                Assert.Empty(pendants);
                continue;
            }

            Assert.True(pendants.Count > 0, $"« {entree.Header} » n'a pas de pendant aux menus");
            Assert.All(pendants, p => Assert.Equal(p.InputGesture, entree.InputGesture));
            // Le libellé dit la commande : liée à celle d'une voisine, une entrée aurait encore un pendant.
            Assert.True(pendants.Any(p => $"{p.Header}".Replace("_", "") == $"{entree.Header}".Replace("_", "")),
                $"« {entree.Header} » est liée à la commande de « {pendants[0].Header} »");
            confrontees++;
        }

        Assert.True(confrontees >= 25, $"{confrontees} entrées confrontées seulement");

        // Le geste affiché : celui des menus, en français.
        Assert.Equal("Ctrl+X", GesteAffiche(Entree(menu, "Co_uper")));
        Assert.Equal("Ctrl+V", GesteAffiche(Entree(menu, "C_oller")));
        Assert.Equal("Suppr", GesteAffiche(Entree(menu, "_Supprimer")));
        Assert.Equal("Ctrl+Maj+G", GesteAffiche(Entree(menu, "_Grouper")));
        Rendu.Cliquer(Entree(menu, "O_rdre"));
        Assert.Equal("Ctrl+Maj+F", GesteAffiche(Entree(Entree(menu, "O_rdre"), "Premier _plan")));
        Assert.Equal("Ctrl+Maj+B", GesteAffiche(Entree(Entree(menu, "O_rdre"), "Arrière-p_lan")));
        Rendu.Cliquer(Entree(menu, "_Zoom"));
        Assert.Equal("Ctrl++", GesteAffiche(Entree(Entree(menu, "_Zoom"), "Zoom _avant")));
        Assert.Equal("Ctrl+Maj+L", GesteAffiche(Entree(Entree(menu, "_Zoom"), "Page _entière")));
    }

    /// <summary>
    /// Une entrée du menu joue sa commande comme son pendant : Premier plan, au sous-menu Ordre, porte
    /// le bloc au premier plan en un pas.
    /// </summary>
    [AvaloniaFact]
    public void Premier_plan_au_menu_porte_le_bloc_au_premier_plan_en_un_pas()
    {
        var a = Forme("A", 60, 60);
        var b = Forme("B", 100, 80);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b));
        var avant = Etat(vm);

        var menu = Rendu.CliquerDroit(fenetre, Rendu.SurLaPage(fenetre, 70, 70));
        Assert.Equal([a], vm.Selection);
        Rendu.Cliquer(Entree(menu, "O_rdre"));
        Rendu.Cliquer(Entree(Entree(menu, "O_rdre"), "Premier _plan"));
        Rendu.Battre();

        Assert.False(menu.IsOpen);
        Assert.Equal(["B", "A"], vm.CurrentPage!.Frames.Select(f => f.Name));
        Rendu.UnSeulPas(vm, avant, "Premier plan au menu du bloc");
    }

    /// <summary>
    /// Couper, au menu d'un bloc, l'emporte au presse-papiers en un pas ; Coller, au menu du vide, le
    /// rend à sa place — l'original n'y est plus. Les deux passent par les gestionnaires de la
    /// fenêtre, ceux du menu Edition (N8).
    /// </summary>
    [AvaloniaFact]
    public void Couper_au_menu_du_bloc_puis_Coller_au_menu_du_vide_le_rend_a_sa_place()
    {
        var a = Forme("A", 60, 60);
        var b = Forme("B", 320, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b));
        var avant = Etat(vm);

        var menu = Rendu.CliquerDroit(fenetre, Rendu.Centre(fenetre, a));
        Rendu.Cliquer(Entree(menu, "Co_uper"));
        Rendu.Battre(8);

        Assert.Equal(["B"], vm.CurrentPage!.Frames.Select(f => f.Name));
        var coupe = Etat(vm);

        menu = Rendu.CliquerDroit(fenetre, Rendu.SurLaPage(fenetre, 400, 500));
        Rendu.Cliquer(Entree(menu, "C_oller"));
        Rendu.Battre(8);

        var colle = vm.CurrentPage!.Frames.Single(f => f.Name == "A");
        Assert.NotSame(a, colle);
        Assert.Equal((60.0, 60.0), (colle.X, colle.Y));
        Assert.Equal([colle], vm.Selection);

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(coupe, Etat(vm));
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "Couper ou Coller a fait plus d'un pas");
    }

    // ------------------------------------------------------------ Supprimer le texte

    /// <summary>
    /// Supprimer le texte, une commande neuve : sur un bloc de texte, le texte part en un pas, le bloc
    /// reste à sa place. Elle n'est offerte qu'à un bloc de texte seul, et grisée sur un bloc vide ou
    /// à champ. Modifier le texte ▸ Importer un texte… suit la garde du menu Texte (B2).
    /// </summary>
    [AvaloniaFact]
    public void Supprimer_le_texte_vide_le_bloc_en_un_pas_et_se_grise_sur_un_bloc_vide_ou_a_champ()
    {
        var texte = Texte("T", 60, 60);
        var vide = Texte("V", 60, 200, "");
        var champ = Texte("N", 60, 340, "caché");
        champ.Field = FieldKind.PageNumber;
        var forme = Forme("F", 380, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(texte, vide, champ, forme));
        var avant = Etat(vm);

        var menu = Rendu.CliquerDroit(fenetre, Rendu.Centre(fenetre, texte));
        Assert.False(texte.IsEditing, "le clic droit est entré dans le texte");
        Assert.Equal([texte], vm.Selection);
        Assert.Contains("Supprimer le _texte", Visibles(menu));
        Assert.Contains("_Modifier le texte", Visibles(menu));
        Assert.True(Entree(Entree(menu, "_Modifier le texte"), "Importer un te_xte…").IsEnabled);
        Rendu.Cliquer(Entree(menu, "Supprimer le _texte"));
        Rendu.Battre();

        Assert.Equal("", texte.Text);
        Assert.Equal((60.0, 60.0, 260.0, 90.0), (texte.X, texte.Y, texte.Width, texte.Height));
        Rendu.UnSeulPas(vm, avant, "Supprimer le texte");

        foreach (var (bloc, pourquoi) in new[] { ((Frame)vide, "vide"), (champ, "à champ") })
        {
            menu = Rendu.CliquerDroit(fenetre, Rendu.Centre(fenetre, vm.CurrentPage!.Frames.Single(f => f.Name == bloc.Name)));
            Assert.Contains("Supprimer le _texte", Visibles(menu));
            Assert.False(Entree(menu, "Supprimer le _texte").IsEffectivelyEnabled, $"Supprimer le texte s'offre à un bloc {pourquoi}");
            Refermer(menu);
        }

        menu = Rendu.CliquerDroit(fenetre, Rendu.Centre(fenetre, vm.CurrentPage!.Frames.Single(f => f.Name == "N")));
        Assert.False(Entree(Entree(menu, "_Modifier le texte"), "Importer un te_xte…").IsEnabled, "l'import s'offre sous un champ");
        Refermer(menu);

        menu = Rendu.CliquerDroit(fenetre, Rendu.Centre(fenetre, vm.CurrentPage!.Frames.Single(f => f.Name == "F")));
        Assert.DoesNotContain("Supprimer le _texte", Visibles(menu));
        Assert.DoesNotContain("_Modifier le texte", Visibles(menu));
    }

    /// <summary>
    /// La balise du bloc de coordonnées de Publisher (p. 54) : Modifier les coordonnées… et Convertir
    /// en texte, sur un bloc qui porte un champ du jeu — et sur lui seul. Convertir en texte pose le
    /// texte que le champ affichait, sans le champ, en un pas.
    /// </summary>
    [AvaloniaFact]
    public void Un_bloc_de_coordonnees_se_modifie_et_se_convertit_en_texte_en_un_pas()
    {
        var nom = Texte("Nom", 60, 60, "");
        nom.Field = FieldKind.ContactName;
        var folio = Texte("Folio", 60, 200, "");
        folio.Field = FieldKind.PageNumber;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(nom, folio));
        vm.Contacts.Sets.Add(new BusinessInfo { Name = "Julie Martin" });
        Rendu.Battre();

        var menu = Rendu.CliquerDroit(fenetre, Rendu.Centre(fenetre, folio));
        Assert.DoesNotContain("Con_vertir en texte", Visibles(menu));
        Assert.DoesNotContain("Modifier les coordo_nnées…", Visibles(menu));
        Refermer(menu);

        var avant = Etat(vm);
        menu = Rendu.CliquerDroit(fenetre, Rendu.Centre(fenetre, nom));
        Assert.Contains("Modifier les coordo_nnées…", Visibles(menu));
        Assert.Contains("Con_vertir en texte", Visibles(menu));
        Rendu.Cliquer(Entree(menu, "Con_vertir en texte"));
        Rendu.Battre();

        Assert.Equal(FieldKind.None, nom.Field);
        Assert.Equal("Julie Martin", nom.Text);
        Rendu.UnSeulPas(vm, avant, "Convertir en texte");
    }

    // ------------------------------------------------------------ vers le dock

    /// <summary>
    /// Format du bloc… ouvre la section du dock propre à la sorte du bloc, même dock replié et section
    /// fermée, le focus dans son premier champ, sans pas : le cadre d'un texte, l'image, le fond et
    /// le contour d'un rectangle, le trait, le tableau.
    /// </summary>
    [AvaloniaFact]
    public void Format_du_bloc_ouvre_la_section_de_sa_sorte_sans_pas()
    {
        var texte = Texte("T", 40, 40);
        var image = new ImageFrame { Name = "I", X = 340, Y = 40, Width = 120, Height = 90, Path = "/nulle/part.png" };
        var forme = Forme("R", 40, 200);
        var trait = new ShapeFrame { Name = "L", Shape = ShapeKind.Line, StrokeThickness = 2 };
        trait.PoserLesExtremites(340, 240, 480, 240);
        var tableau = new TableFrame { Name = "Tb", X = 40, Y = 360, FontFamily = "DejaVu Sans" };
        tableau.Columns.Add(new TableColumn(100));
        tableau.Rows.Add(new TableRow(30));
        tableau.Cells.Add(new TableCell { Row = 0, Column = 0, Text = "x" });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(texte, image, forme, trait, tableau));
        var avant = Etat(vm);

        // Le point se lit dock replié : la planche s'élargit, et la page se recentre.
        var cas = new (Frame Bloc, Func<Point> Point, string Section, Action Fermer)[]
        {
            (texte, () => Rendu.Centre(fenetre, texte), "ContenuCadreDeTexte", () => vm.SectionCadreOuverte = false),
            (image, () => Rendu.Centre(fenetre, image), "ContenuImage", () => vm.SectionImageOuverte = false),
            (forme, () => Rendu.Centre(fenetre, forme), "ContenuFondEtContour", () => vm.SectionFondOuverte = false),
            (trait, () => Rendu.SurLaPage(fenetre, 410, 240), "ContenuTrait", () => vm.SectionTraitOuverte = false),
            (tableau, () => Rendu.Centre(fenetre, tableau), "ContenuTableau", () => vm.SectionTableauOuverte = false),
        };

        foreach (var (bloc, point, section, fermer) in cas)
        {
            Rendu.Onglet(fenetre, "Styles");
            fermer();
            if (vm.DockVisible) vm.ReplierLesPanneauxCommand.Execute(null);
            Rendu.UneImage();

            var menu = Rendu.CliquerDroit(fenetre, point());
            Assert.Equal([bloc], vm.Selection);
            Rendu.Cliquer(Entree(menu, "Format du _bloc…"));
            Rendu.Battre(8);

            Assert.True(vm.DockVisible, $"{bloc.Name} : le dock n'est pas rouvert");
            var contenu = Trouver<Control>(fenetre, section);
            Assert.True(Dans(LeFocus(fenetre), contenu), $"{bloc.Name} : le focus n'est pas dans {section} ({LeFocus(fenetre)})");
            fenetre.FocusManager!.ClearFocus();
            Rendu.Battre();
        }

        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "Format du bloc… a empilé un pas");
    }

    /// <summary>
    /// La recette : un double-clic sur une image ouvre la section Image du dock (décision 5), et sur
    /// une forme sa section, le fond et le contour — sans déplacer le bloc ni empiler de pas. Le
    /// double-clic dans un texte, lui, y prend un mot (N12) : il n'ouvre rien.
    /// </summary>
    [AvaloniaFact]
    public void Un_double_clic_sur_une_image_ouvre_la_section_Image_et_sur_une_forme_la_sienne()
    {
        var image = new ImageFrame { Name = "I", X = 60, Y = 60, Width = 160, Height = 120, Path = "/nulle/part.png" };
        var forme = Forme("R", 320, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(image, forme));
        var avant = Etat(vm);

        foreach (var (bloc, section, fermer) in new (Frame, string, Action)[]
                 {
                     (image, "ContenuImage", () => vm.SectionImageOuverte = false),
                     (forme, "ContenuFondEtContour", () => vm.SectionFondOuverte = false),
                 })
        {
            Rendu.Onglet(fenetre, "Styles");
            fermer();
            if (vm.DockVisible) vm.ReplierLesPanneauxCommand.Execute(null);
            vm.SelectOnly(null);
            Rendu.UneImage();

            Rendu.DoubleCliquer(fenetre, Rendu.Centre(fenetre, bloc));

            Assert.Equal([bloc], vm.Selection);
            Assert.True(vm.DockVisible, $"{bloc.Name} : le dock n'est pas rouvert");
            var contenu = Trouver<Control>(fenetre, section);
            Assert.True(Dans(LeFocus(fenetre), contenu), $"{bloc.Name} : le focus n'est pas dans {section} ({LeFocus(fenetre)})");
            fenetre.FocusManager!.ClearFocus();
            Rendu.Battre();
        }

        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "le double-clic a empilé un pas");
    }

    // ------------------------------------------------------------ au clavier

    /// <summary>
    /// La recette : <c>Maj+F10</c> ouvre le menu, sous la sélection, et non au bas de la planche ; la
    /// touche Menu de même. Sans rien de pris, c'est le menu de la page.
    /// </summary>
    [AvaloniaFact]
    public void Maj_F10_et_la_touche_Menu_ouvrent_le_menu_sous_la_selection()
    {
        var a = Forme("A", 200, 120);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a));
        Rendu.Cliquer(fenetre, Rendu.Centre(fenetre, a));
        Assert.Equal([a], vm.Selection);
        Assert.True(Dans(LeFocus(fenetre), Planche(fenetre)));
        var bas = Rendu.SurLaPage(fenetre, a.X + a.Width / 2, a.Y + a.Height);

        foreach (var (touche, modificateurs, relacher, nom) in new[]
                 {
                     (PhysicalKey.F10, RawInputModifiers.Shift, false, "Maj+F10"),
                     (PhysicalKey.ContextMenu, RawInputModifiers.None, true, "la touche Menu"),
                 })
        {
            Rendu.Touche(fenetre, touche, modificateurs, relacher);
            var menu = Rendu.MenuOuvert(fenetre, nom);

            Assert.Contains("Format du _bloc…", Visibles(menu));
            var coin = menu.TranslatePoint(default, fenetre) ?? throw new InvalidOperationException("le menu n'est pas dans la fenêtre");
            Assert.True(Math.Abs(coin.Y - bas.Y) < 12, $"{nom} : le menu s'ouvre en {coin}, et non sous le bloc ({bas})");
            Assert.True(coin.X < bas.X && coin.X + menu.Bounds.Width > bas.X, $"{nom} : le menu ({coin}) n'est pas sous le bloc ({bas})");
            Refermer(menu);
        }

        vm.SelectOnly(null);
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.F10, RawInputModifiers.Shift);
        var page = Rendu.MenuOuvert(fenetre, "Maj+F10 sans sélection");
        Assert.Equal(new[] { "C_oller", "Repères de m_ise en page…", "_Arrière-plan…" }, Visibles(page));
    }

    /// <summary>
    /// La recette : un clic droit sur une règle n'ouvre rien, ni ne pose de repère ; sur une barre de
    /// défilement non plus. La sélection ne change pas.
    /// </summary>
    [AvaloniaFact]
    public void Un_clic_droit_sur_une_regle_ou_une_barre_de_defilement_n_ouvre_rien()
    {
        var a = Forme("A", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a));
        vm.Zoom *= 4;
        vm.SelectOnly(a);
        Rendu.Battre(8);
        var avant = Etat(vm);

        var barres = Trouver<ScrollViewer>(fenetre, "Scroller").GetVisualDescendants().OfType<ScrollBar>()
            .Where(b => b.IsEffectivelyVisible && b.Bounds.Width > 0 && b.Bounds.Height > 0).ToList();
        Assert.NotEmpty(barres);
        var cibles = new List<(string, Control)>
        {
            ("la règle du haut", Trouver<Control>(fenetre, "TopRuler")),
            ("la règle de gauche", Trouver<Control>(fenetre, "LeftRuler")),
        };
        cibles.AddRange(barres.Select(b => ($"la barre {b.Orientation}", (Control)b)));

        foreach (var (nom, cible) in cibles)
        {
            Rendu.Cliquer(fenetre, Rendu.Centre(cible), bouton: MouseButton.Right);
            for (var image = 0; image < 10; image++)
            {
                Rendu.UneImage();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }

            Assert.True(MenusOuverts(fenetre).Count == 0, $"un clic droit sur {nom} a ouvert un menu");
            Assert.Equal([a], vm.Selection);
        }

        Assert.Equal(avant, Etat(vm));
    }

    // ------------------------------------------------------------ en saisie

    /// <summary>
    /// La recette : en saisie, un clic droit dans le texte ouvre le menu du texte — Couper, Copier,
    /// Coller et Format du bloc… —, la saisie ouverte et la sélection du texte gardée. Copier y met le
    /// texte sélectionné au presse-papiers, sans changer le document ; la frappe reprend dans la zone.
    /// Couper l'emporte.
    /// </summary>
    [AvaloniaFact]
    public void En_saisie_le_menu_du_texte_garde_Couper_Copier_Coller_et_ajoute_Format_du_bloc()
    {
        var bloc = Texte("T", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.SelectionnerDuTexte(zone, 0, 7);
        Assert.Equal("Bonjour", zone.SelectedText);
        var avant = Etat(vm);

        var menu = Rendu.CliquerDroit(fenetre, Rendu.BordDuCaractere(zone, 3));

        Assert.True(bloc.IsEditing, "le clic droit dans le texte a refermé la saisie");
        Assert.Equal(new[] { "Co_uper", "_Copier", "C_oller", "Format du _bloc…" }, Visibles(menu));
        SeparateursAuJuste(menu);
        Assert.Equal("Bonjour", zone.SelectedText);

        Rendu.Cliquer(Entree(menu, "_Copier"));
        Rendu.Battre(8);

        Assert.Equal("Bonjour", Rendu.TexteCopie(fenetre));
        Assert.True(bloc.IsEditing);
        Assert.True(zone.IsFocused, "la zone n'a pas repris le focus");
        Assert.Equal("Bonjour", zone.SelectedText);
        Assert.Equal(avant, Etat(vm));

        menu = Rendu.CliquerDroit(fenetre, Rendu.BordDuCaractere(zone, 3));
        Rendu.Cliquer(Entree(menu, "Co_uper"));
        Rendu.Battre(8);

        Assert.Equal(" le monde", bloc.Text);
        Assert.True(bloc.IsEditing);
        Rendu.Taper(fenetre, "Salut");
        Assert.Equal("Salut le monde", bloc.Text);
    }

    /// <summary>
    /// En saisie, Échap referme le menu du texte et rend la frappe à la zone, la saisie ouverte.
    /// Format du bloc… y mène au cadre du texte, la saisie gardée.
    /// </summary>
    [AvaloniaFact]
    public void En_saisie_le_menu_referme_rend_la_frappe_et_Format_du_bloc_mene_au_cadre()
    {
        var bloc = Texte("T", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);

        var menu = Rendu.CliquerDroit(fenetre, Rendu.BordDuCaractere(zone, 5));
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UneImage();
        Assert.False(menu.IsOpen);
        Assert.True(bloc.IsEditing, "Échap au menu a refermé la saisie");
        Assert.True(zone.IsFocused, "la zone n'a pas repris le focus");
        Rendu.Taper(fenetre, "X");
        Assert.Equal("BonjoXur le monde", bloc.Text);

        vm.SectionCadreOuverte = false;
        menu = Rendu.CliquerDroit(fenetre, Rendu.BordDuCaractere(zone, 5));
        Rendu.Cliquer(Entree(menu, "Format du _bloc…"));
        Rendu.Battre(8);
        Assert.True(Dans(LeFocus(fenetre), Trouver<Control>(fenetre, "ContenuCadreDeTexte")),
            $"le focus n'est pas au cadre du texte ({LeFocus(fenetre)})");
        Assert.True(bloc.IsEditing, "Format du bloc… a refermé la saisie");
    }
}
