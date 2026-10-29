using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
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
/// Les menus vers le dock, et les pages au clavier — poste N18 (fiche 23, b17, d16, e8, e9, e12,
/// e16, e18, f17).
///
/// Publisher règle un objet par ses dialogues « Format… » ; MyPublisher les a dans le dock, et
/// aucune entrée de menu n'y menait : les repères de mise en page, le format des repères de
/// règle, l'arrière-plan et le format de page ne s'atteignaient qu'en ouvrant le bon onglet et la
/// bonne section. Les entrées « … » y mènent désormais — le dock rouvert s'il est replié,
/// l'onglet choisi, la section ouverte, le focus dans son champ —, sans pas d'annulation. Les
/// pages ont leurs touches de Publisher en français : Ctrl+Maj+N, Ctrl+Maj+U, Ctrl+Pg.Suiv,
/// Ctrl+Pg.Préc, F5 et Ctrl+B, qui mènent au numéro de page de la barre d'état, devenu un champ.
/// Insertion ▸ Numéro de page, Date et Coordonnées posent un bloc à champ (C4).
///
/// Tout se joue sur la vraie fenêtre, aux gestes du banc (poste N1). Le dialogue de A8 a sa classe
/// (<see cref="InsererDesPagesTests"/>), le quart de tour de A9 la sienne
/// (<see cref="QuartDeTourTests"/>). La recette du poste —
/// <c>Scripts/recettes/N18-menus-vers-le-dock.sh</c> — joue les trois par leur trait.
/// </summary>
[Trait("poste", "N18")]
public class MenusVersLeDockTests
{
    // ------------------------------------------------------------------ outillage

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static TextFrame Texte(string nom = "Texte") =>
        new() { Name = nom, X = 60, Y = 60, Width = 220, Height = 90, Text = "Bonjour" };

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    /// <summary>Une entrée de menu, par son libellé : hors de l'arbre visuel tant que son menu est replié.</summary>
    private static MenuItem Menu(Window fenetre, string libelle) =>
        fenetre.GetLogicalDescendants().OfType<MenuItem>().SingleOrDefault(m => Equals(m.Header, libelle))
        ?? throw new InvalidOperationException($"le menu n'a pas d'entrée « {libelle} »");

    /// <summary>Joue une entrée de menu au pointeur, son chemin ouvert niveau par niveau.</summary>
    private static void Jouer(Window fenetre, params string[] chemin)
    {
        foreach (var libelle in chemin) Rendu.Cliquer(Menu(fenetre, libelle));
        Rendu.Battre(8);
    }

    private static Visual? LeFocus(Window fenetre) => fenetre.FocusManager?.GetFocusedElement() as Visual;

    private static bool Dans(Visual? element, Control conteneur) =>
        element is not null && (ReferenceEquals(element, conteneur) || conteneur.IsVisualAncestorOf(element));

    private static string? GesteAffiche(MenuItem entree) =>
        entree.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_InputGestureText").Text;

    private static Document TroisPages(params Frame[] blocs)
    {
        var document = Rendu.Feuille(blocs);
        document.Pages.Add(new Page { Name = "Page 2" });
        document.Pages.Add(new Page { Name = "Page 3" });
        return document;
    }

    // ------------------------------------------------------------- les entrées « … »

    /// <summary>
    /// La recette : chaque entrée « … » ouvre la bonne section, même dock replié, et n'empile aucun
    /// pas. Le dock part replié (Ctrl+F1), l'onglet Styles choisi, la section visée fermée : l'entrée
    /// rouvre le dock, choisit l'onglet, ouvre la section, et le focus tombe dans le champ visé —
    /// ou dans le premier champ de la section. Police, montée dans la barre contextuelle (I3), mène à
    /// la liste des polices de la barre. Le document ne change pas, et rien ne s'annule.
    /// </summary>
    [AvaloniaFact]
    public void Chaque_entree_points_de_suspension_ouvre_sa_section_meme_dock_replie_sans_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        var entrees = new (string[] Chemin, string? Onglet, string? Section, string? Champ, Action Fermer)[]
        {
            (["_Disposition", "Repères de m_ise en page…"], "Document", "ContenuMarges", "MargeGauche",
                () => { vm.SectionMargesOuverte = false; vm.SectionGrilleOuverte = false; }),
            (["_Disposition", "Re_pères de règle", "_Format…"], "Document", "ContenuReperesDeRegle", null,
                () => vm.SectionReperesOuverte = false),
            (["_Page", "Arrière-p_lan…"], "Document", "ContenuFondDePage", null, () => vm.SectionFondDePageOuverte = false),
            (["_Fichier", "_Mise en page…"], "Document", "ContenuFormat", "FormatDePage", () => vm.SectionFormatOuverte = false),
            (["_Texte", "Pa_ragraphe…"], "Propriétés", "ContenuParagraphe", null, () => vm.SectionParagrapheOuverte = false),
            (["_Disposition", "_Faire pivoter ou retourner", "Rotation _personnalisée…"], "Propriétés",
                "ContenuPositionEtTaille", "ChampAngle", () => vm.SectionPositionOuverte = false),
            (["_Texte", "_Police…"], null, null, "PoliceDuBloc", () => { }),
        };

        foreach (var (chemin, onglet, section, champ, fermer) in entrees)
        {
            var entree = string.Join(" ▸ ", chemin);
            fenetre.FocusManager!.ClearFocus();
            Rendu.Onglet(fenetre, "Styles");
            fermer();
            if (vm.DockVisible) vm.ReplierLesPanneauxCommand.Execute(null);
            Rendu.Battre();
            Assert.False(vm.DockVisible);

            Jouer(fenetre, chemin);

            var focus = LeFocus(fenetre);
            if (onglet is null)
            {
                Assert.True(Dans(focus, Trouver<Control>(Trouver<Control>(fenetre, "BarreContextuelle"), champ!)),
                    $"{entree} : le focus n'est pas à la liste des polices de la barre ({focus})");
                continue;
            }

            Assert.True(vm.DockVisible, $"{entree} : le dock n'est pas rouvert");
            var onglets = Trouver<TabControl>(fenetre, "OngletsDuDock");
            Assert.Equal(onglet, (onglets.SelectedItem as TabItem)?.Header);
            var contenu = Trouver<Control>(fenetre, section!);
            Assert.True(contenu.IsEffectivelyVisible, $"{entree} : la section {section} n'est pas ouverte");
            Assert.True(Dans(focus, contenu), $"{entree} : le focus n'est pas dans la section {section} ({focus})");
            if (champ is not null)
            {
                Assert.True(Dans(focus, Trouver<Control>(contenu, champ)), $"{entree} : le focus n'est pas au champ {champ} ({focus})");
            }

            fenetre.FocusManager.ClearFocus();
            Rendu.Battre();
            Assert.Equal(avant, Etat(vm));
            Assert.False(vm.CanUndo, $"{entree} : un pas d'annulation");
            Assert.False(vm.IsDirty, $"{entree} : le document est marqué modifié");
        }

        // Repères de mise en page ouvre la grille avec les marges : le dialogue Layout Guides de
        // Publisher a les deux, et les lignes de base.
        Assert.True(vm.SectionGrilleOuverte);
    }

    /// <summary>
    /// Police… et Paragraphe… sont grisés sans bloc de texte, comme chez Publisher (p. 24) — Police
    /// s'offre aussi pour un tableau, dont la barre règle la police ; Rotation personnalisée… veut un
    /// bloc seul ; les entrées du document s'offrent toujours.
    /// </summary>
    [AvaloniaFact]
    public void Police_Paragraphe_et_Rotation_personnalisee_attendent_le_bloc_qui_les_porte()
    {
        var texte = Texte();
        var carre = new ShapeFrame { Name = "Carré", X = 300, Y = 60, Width = 60, Height = 60 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(texte, carre));

        Assert.False(vm.PoliceCommand.CanExecute(null));
        Assert.False(vm.ParagrapheCommand.CanExecute(null));
        Assert.False(vm.RotationPersonnaliseeCommand.CanExecute(null));
        Assert.True(vm.ReperesDeMiseEnPageCommand.CanExecute(null));
        Assert.True(vm.MiseEnPageCommand.CanExecute(null));

        vm.SelectOnly(carre);
        Rendu.Battre();
        Assert.False(vm.PoliceCommand.CanExecute(null));
        Assert.False(vm.ParagrapheCommand.CanExecute(null));
        Assert.True(vm.RotationPersonnaliseeCommand.CanExecute(null));

        vm.SelectOnly(texte);
        Rendu.Battre();
        Assert.True(vm.PoliceCommand.CanExecute(null));
        Assert.True(vm.ParagrapheCommand.CanExecute(null));

        vm.SetSelection([texte, carre]);
        Rendu.Battre();
        Assert.False(vm.RotationPersonnaliseeCommand.CanExecute(null));

        // Au menu, l'entrée grisée suit la sélection.
        vm.SelectOnly(carre);
        Rendu.Battre();
        Rendu.Cliquer(Menu(fenetre, "_Texte"));
        Assert.False(Menu(fenetre, "_Police…").IsEffectivelyEnabled);
        Assert.False(Menu(fenetre, "Pa_ragraphe…").IsEffectivelyEnabled);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
    }

    // ------------------------------------------------------------- les pages au clavier

    /// <summary>
    /// La recette : F5 met le focus au numéro de page de la barre d'état, tout pris ; « 3 » puis
    /// Entrée mène à la page 3, et rend la planche. « 99 » est refusé, avec un message, et la page
    /// ne change pas ; Ctrl+B y mène aussi, sans armer l'outil Tableau ; Échap renonce. Aucun pas.
    /// </summary>
    [AvaloniaFact]
    public void Le_numero_de_page_de_la_barre_d_etat_mene_a_la_page_tapee()
    {
        var bloc = Texte();
        var document = TroisPages(bloc);
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var numero = Trouver<TextBox>(fenetre, "NumeroDePage");
        var total = Trouver<TextBlock>(fenetre, "TotalDesPages");
        Assert.Equal("1", numero.Text);
        Assert.Equal("sur 3", total.Text);
        var avant = Etat(vm);

        // La planche a le focus, comme après un clic sur la page.
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 400, 600));
        Rendu.Touche(fenetre, PhysicalKey.F5);
        Assert.True(numero.IsFocused, "F5 ne mène pas au numéro de page");
        Assert.Equal(0, numero.SelectionStart);
        Assert.Equal(1, numero.SelectionEnd);

        Rendu.Taper(fenetre, "3");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Same(document.Pages[2], vm.CurrentPage);
        Assert.False(numero.IsFocused, "Entrée ne rend pas la planche");
        Assert.Equal("3", numero.Text);

        Rendu.Touche(fenetre, PhysicalKey.F5);
        Rendu.Taper(fenetre, "99");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Same(document.Pages[2], vm.CurrentPage);
        Assert.Contains("« 99 » est refusé", vm.Status);
        Assert.Contains("3 pages", vm.Status);
        Assert.True(numero.IsFocused, "un numéro refusé rend la planche");
        Assert.Equal("3", numero.Text);

        Rendu.Taper(fenetre, "deux");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Contains("n'est pas un numéro de page", vm.Status);

        // Échap renonce, et rend la planche.
        Rendu.Taper(fenetre, "1");
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Same(document.Pages[2], vm.CurrentPage);
        Assert.False(numero.IsFocused);
        Assert.Equal("3", numero.Text);

        // Ctrl+B : « Atteindre la page » chez Publisher en français, et non l'outil Tableau.
        Rendu.Touche(fenetre, PhysicalKey.B, RawInputModifiers.Control);
        Assert.True(numero.IsFocused, "Ctrl+B ne mène pas au numéro de page");
        Assert.Equal(EditorTool.Select, vm.ActiveTool);
        Rendu.Taper(fenetre, "1");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Same(document.Pages[0], vm.CurrentPage);

        // La page changée ailleurs se lit dans le champ.
        vm.CurrentPage = document.Pages[1];
        Rendu.Battre();
        Assert.Equal("2", numero.Text);

        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// Ctrl+Pg.Suiv et Ctrl+Pg.Préc changent de page, au clavier comme au menu Page ; aux bouts,
    /// rien ne bouge, et la barre d'état le dit. Même en saisie : la saisie se referme, la frappe
    /// garde son pas. Les menus disent les touches en français.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_Pg_Suiv_et_Ctrl_Pg_Prec_changent_de_page()
    {
        var bloc = Texte();
        var document = TroisPages(bloc);
        var (fenetre, vm) = Rendu.Ouvrir(document);

        Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.Taper(fenetre, "Z");
        Rendu.Touche(fenetre, PhysicalKey.PageDown, RawInputModifiers.Control);
        Assert.Same(document.Pages[1], vm.CurrentPage);
        Assert.False(bloc.IsEditing, "la saisie reste ouverte sur une page qu'on a quittée");
        Assert.True(vm.CanUndo, "la frappe n'a pas son pas");

        Rendu.Touche(fenetre, PhysicalKey.PageDown, RawInputModifiers.Control);
        Assert.Same(document.Pages[2], vm.CurrentPage);
        Rendu.Touche(fenetre, PhysicalKey.PageDown, RawInputModifiers.Control);
        Assert.Same(document.Pages[2], vm.CurrentPage);
        Assert.Equal("Dernière page", vm.Status);

        Rendu.Touche(fenetre, PhysicalKey.PageUp, RawInputModifiers.Control);
        Assert.Same(document.Pages[1], vm.CurrentPage);

        Jouer(fenetre, "_Page", "Page p_récédente");
        Assert.Same(document.Pages[0], vm.CurrentPage);
        Rendu.Touche(fenetre, PhysicalKey.PageUp, RawInputModifiers.Control);
        Assert.Equal("Première page", vm.Status);
        Jouer(fenetre, "_Page", "Page s_uivante");
        Assert.Same(document.Pages[1], vm.CurrentPage);

        Rendu.Cliquer(Menu(fenetre, "_Page"));
        Assert.Equal("Ctrl+Maj+N", GesteAffiche(Menu(fenetre, "_Insérer des pages…")));
        Assert.Equal("Ctrl+Maj+U", GesteAffiche(Menu(fenetre, "_Dupliquer la page")));
        Assert.Equal("Ctrl+Pg.Suiv", GesteAffiche(Menu(fenetre, "Page s_uivante")));
        Assert.Equal("Ctrl+Pg.Préc", GesteAffiche(Menu(fenetre, "Page p_récédente")));
        Assert.Equal("F5", GesteAffiche(Menu(fenetre, "A_tteindre la page…")));
        Rendu.Touche(fenetre, PhysicalKey.Escape);
    }

    /// <summary>
    /// Ctrl+Maj+U duplique la page courante après elle, en un pas, même pendant la saisie d'un
    /// bloc : la saisie se referme d'abord, dans son pas, et une seule annulation retire la copie
    /// en laissant la frappe.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_Maj_U_duplique_la_page_en_un_pas_meme_en_saisie()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.Taper(fenetre, "Z");
        var frappe = bloc.Text;
        Assert.NotEqual("Bonjour", frappe);

        Rendu.Touche(fenetre, PhysicalKey.U, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(2, vm.Document.Pages.Count);
        Assert.Same(vm.Document.Pages[1], vm.CurrentPage);
        Assert.Single(vm.CurrentPage!.Frames);
        Assert.NotSame(bloc, vm.CurrentPage.Frames[0]);
        Assert.Equal(frappe, ((TextFrame)vm.CurrentPage.Frames[0]).Text);
        Assert.False(bloc.IsEditing);

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Single(vm.Document.Pages);
        Assert.Equal(frappe, ((TextFrame)vm.Document.Pages[0].Frames[0]).Text);
        Assert.True(vm.CanUndo, "la frappe s'est fondue dans la copie de la page");
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal("Bonjour", ((TextFrame)vm.Document.Pages[0].Frames[0]).Text);
    }

    /// <summary>
    /// Ctrl+Maj+N ouvre « Insérer des pages » — il proposait le catalogue des modèles, Maj comptée
    /// pour rien par OnKeyDown —, et Ctrl+N ouvre toujours le catalogue. Annuler n'insère rien.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_Maj_N_ouvre_Inserer_des_pages_et_non_plus_le_catalogue()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte()));
        var avant = Etat(vm);
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 400, 600));

        Rendu.Touche(fenetre, PhysicalKey.N, RawInputModifiers.Control | RawInputModifiers.Shift);
        Rendu.Battre(8);
        Assert.Empty(fenetre.OwnedWindows.OfType<TemplateChooserWindow>());
        var dialogue = Assert.Single(fenetre.OwnedWindows.OfType<InsererDesPages>());

        Rendu.Cliquer(Trouver<Button>(dialogue, "BoutonAnnuler"));
        Rendu.Battre(8);
        Assert.Empty(fenetre.OwnedWindows.OfType<InsererDesPages>());
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo);

        Rendu.Touche(fenetre, PhysicalKey.N, RawInputModifiers.Control);
        Rendu.Battre(8);
        Assert.Empty(fenetre.OwnedWindows.OfType<InsererDesPages>());
        var catalogue = Assert.Single(fenetre.OwnedWindows.OfType<TemplateChooserWindow>());
        catalogue.Close();
        Rendu.Battre(8);
    }

    // ------------------------------------------------------- Insertion ▸ un bloc à champ

    /// <summary>
    /// Insertion ▸ Numéro de page, Date et Coordonnées ▸ posent un bloc de texte qui porte son champ
    /// (C4), en un pas chacun, sans ouvrir de saisie : le texte est calculé, et le folio se lit.
    /// </summary>
    [AvaloniaFact]
    public void Insertion_pose_un_bloc_a_champ_en_un_pas()
    {
        var (fenetre, vm) = Rendu.Ouvrir(TroisPages());
        vm.CurrentPage = vm.Document.Pages[1];
        Rendu.Battre();

        foreach (var (chemin, sorte) in new (string[], FieldKind)[]
                 {
                     (["_Insertion", "_Numéro de page"], FieldKind.PageNumber),
                     (["_Insertion", "_Date"], FieldKind.Date),
                     (["_Insertion", "_Coordonnées", "_Société"], FieldKind.ContactOrganisation),
                 })
        {
            var avant = Etat(vm);

            Jouer(fenetre, chemin);

            var bloc = Assert.IsType<TextFrame>(Assert.Single(vm.CurrentPage!.Frames));
            Assert.Equal(sorte, bloc.Field);
            Assert.Same(bloc, vm.SingleSelected);
            Assert.False(bloc.IsEditing, "la saisie s'ouvre sur un bloc dont le texte est calculé");
            Rendu.UnSeulPas(vm, avant, string.Join(" ▸ ", chemin));
        }

        Jouer(fenetre, "_Insertion", "_Numéro de page");
        Rendu.UneImage();
        var folio = Assert.IsType<TextFrame>(Assert.Single(vm.CurrentPage!.Frames));
        Assert.Equal("2", FieldResolver.Resolve(folio.Field, folio.Text, vm.FieldContext));
    }
}
