using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les raccourcis de la fenêtre laissent la touche au champ — poste N6 (fiche 23, a13, d2, d21).
///
/// Les liaisons de la fenêtre passent avant le contrôle qui a le focus, et ne lui rendent la
/// touche que si leur commande ne peut pas s'exécuter. Avant le poste, <c>Ctrl+A</c> tapé dans
/// la saisie d'un bloc sélectionnait tous les blocs et refermait la saisie, et <c>Ctrl+D</c>
/// dupliquait le bloc, la frappe et la copie ne faisant qu'un pas ; de même dans les champs du
/// dock, pour <c>Ctrl+Maj+G</c>, <c>H</c>, <c>F</c> et <c>B</c> (relevé de la relecture de I3).
/// Tant qu'un champ de texte a le clavier, ces raccourcis d'objet se taisent, et leur touche
/// revient au champ ; les menus et les boutons agissent sur les blocs comme avant (décision du
/// propriétaire, 26 octobre 2026). <c>Alt</c>+flèche, en saisie, déplace le bloc dans un pas à
/// lui. Hors des champs, tout se joue comme avant — les tests de A4 le tiennent.
///
/// Tout se joue au clavier sur la vraie fenêtre, aux gestes du banc (<see cref="Rendu"/>,
/// poste N1). La recette du poste — <c>Scripts/recettes/N6-touche-au-champ.sh</c> — joue ces
/// tests par leur trait.
/// </summary>
[Trait("poste", "N6")]
public class ToucheAuChampTests
{
    // ------------------------------------------------------------------ outillage

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    /// <summary>
    /// Une entrée de menu, par son libellé : hors de l'arbre visuel tant que son menu est
    /// replié, elle est dans l'arbre logique.
    /// </summary>
    private static MenuItem Menu(Window fenetre, string libelle) =>
        fenetre.GetLogicalDescendants().OfType<MenuItem>().SingleOrDefault(m => Equals(m.Header, libelle))
        ?? throw new InvalidOperationException($"le menu n'a pas d'entrée « {libelle} »");

    /// <summary>Le champ de saisie d'un champ numérique : c'est lui qui prend le focus.</summary>
    private static TextBox SaisieDe(NumericUpDown champ) => champ.GetVisualDescendants().OfType<TextBox>().First();

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static string[] Pile(MainWindowViewModel vm) => vm.CurrentPage!.Frames.Select(bloc => bloc.Name).ToArray();

    private static TextFrame Texte() => new()
    {
        Name = "Bloc", X = 60, Y = 60, Width = 300, Height = 80, Text = "Texte",
        FontFamily = "DejaVu Sans", FontSize = 10.5, Foreground = "#FF000000",
    };

    private static ShapeFrame Carre(string nom, double x, double y) =>
        new() { Name = nom, X = x, Y = y, Width = 60, Height = 60, Fill = new SolidFill("#FFCC0000") };

    private static void Quitter(Window fenetre)
    {
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
    }

    /// <summary>Le curseur au bout du champ, rien de sélectionné : Ctrl+A aura quelque chose à montrer.</summary>
    private static void AuBout(Window fenetre, TextBox champ)
    {
        Rendu.Touche(fenetre, PhysicalKey.End, RawInputModifiers.Control);
        Assert.True(string.IsNullOrEmpty(champ.SelectedText), $"« {champ.Text} » a encore du texte sélectionné");
    }

    /// <summary><c>Ctrl+A</c> a sélectionné tout le texte du champ.</summary>
    private static void ToutLeTexteSelectionne(TextBox champ, string quoi)
    {
        Assert.False(string.IsNullOrEmpty(champ.Text), $"{quoi} : le champ est vide, il n'y a rien à sélectionner");
        Assert.True(champ.IsFocused, $"{quoi} : Ctrl+A a pris le focus au champ");
        Assert.Equal(champ.Text, champ.SelectedText);
    }

    // ------------------------------------------------------------------ Ctrl+A

    /// <summary>
    /// La recette : en saisie, <c>Ctrl+A</c> sélectionne le texte, la saisie reste ouverte et la
    /// sélection des blocs ne change pas — l'aide de Publisher en français, « dans une zone de
    /// texte ». Échap refermant la saisie, il sélectionne de nouveau tous les blocs, comme avant.
    /// </summary>
    [AvaloniaFact]
    public void En_saisie_Ctrl_A_selectionne_le_texte_et_laisse_la_saisie_et_les_blocs()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, Carre("Carré", 400, 300)));

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        AuBout(fenetre, zone);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.Q, RawInputModifiers.Control);

        ToutLeTexteSelectionne(zone, "la saisie sur la page");
        Assert.True(bloc.IsEditing, "Ctrl+A a refermé la saisie");
        Assert.Same(bloc, Assert.Single(vm.Selection));
        Assert.Equal("Texte", bloc.Text);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(bloc.IsEditing);
        Assert.False(vm.CanUndo, "Ctrl+A en saisie a fait un pas");

        Rendu.ToucheAzerty(fenetre, PhysicalKey.Q, RawInputModifiers.Control);
        Assert.Equal(2, vm.Selection.Count);
    }

    /// <summary>
    /// La recette, de même dans les champs du dock : Contenu, Nom, un champ numérique (X), et une
    /// marge de l'onglet Document, que la sélection ne masque pas — le chemin que la relecture de
    /// Q24 avait mesuré : la liaison prenait la touche, sélectionnait les blocs en refermant la
    /// transaction du champ, et la marge saisie ensuite n'entrait dans aucun pas.
    /// </summary>
    [AvaloniaFact]
    public void Dans_les_champs_du_dock_Ctrl_A_selectionne_le_texte_du_champ()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, Carre("Carré", 400, 300)));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        var champs = new (string Quoi, TextBox Champ)[]
        {
            ("Contenu", Trouver<TextBox>(fenetre, "SaisieDuTexte")),
            ("Nom", Trouver<TextBox>(fenetre, "ChampNom")),
            ("X", SaisieDe(Trouver<NumericUpDown>(fenetre, "ChampX"))),
        };
        foreach (var (quoi, champ) in champs)
        {
            champ.Focus();
            Rendu.Battre();
            AuBout(fenetre, champ);
            Rendu.ToucheAzerty(fenetre, PhysicalKey.Q, RawInputModifiers.Control);

            ToutLeTexteSelectionne(champ, quoi);
            Assert.Same(bloc, Assert.Single(vm.Selection));
            Quitter(fenetre);
        }

        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "Ctrl+A dans un champ a fait un pas");

        // La marge : Ctrl+A, puis une valeur tapée par-dessus, fait son pas — un seul.
        Rendu.Onglet(fenetre, "Document");
        var marge = SaisieDe(Trouver<NumericUpDown>(fenetre, "MargeGauche"));
        marge.Focus();
        Rendu.Battre();
        AuBout(fenetre, marge);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.Q, RawInputModifiers.Control);
        ToutLeTexteSelectionne(marge, "la marge gauche");
        Assert.Same(bloc, Assert.Single(vm.Selection));

        Rendu.Taper(fenetre, "25");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Quitter(fenetre);
        Assert.Equal(Units.MmToPoints(25), vm.Document.MarginLeft, precision: 9);
        Assert.True(vm.IsDirty, "la marge saisie après Ctrl+A ne marque pas le document");
        Rendu.UnSeulPas(vm, avant, "la marge saisie après Ctrl+A");
    }

    // ------------------------------------------------------------------ les commandes d'objet

    /// <summary>
    /// La recette : en saisie, <c>Ctrl+D</c>, <c>Ctrl+Maj+F</c> et <c>Ctrl+Maj+B</c> ne font rien
    /// et n'empilent rien. Avant le poste, <c>Ctrl+D</c> dupliquait le bloc, refermait la saisie
    /// et fondait la frappe et la copie en un seul pas (fiche 23, d21) : ici la frappe a le sien,
    /// et lui seul.
    /// </summary>
    [AvaloniaFact]
    public void En_saisie_dupliquer_et_l_ordre_se_taisent_et_la_frappe_a_seule_son_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("Fond", 20, 20), bloc, Carre("Avant", 400, 300)));
        var avant = Etat(vm);

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        AuBout(fenetre, zone);
        Rendu.Taper(fenetre, " abc");

        Rendu.Touche(fenetre, PhysicalKey.D, RawInputModifiers.Control);
        Rendu.Touche(fenetre, PhysicalKey.F, RawInputModifiers.Control | RawInputModifiers.Shift);
        Rendu.Touche(fenetre, PhysicalKey.B, RawInputModifiers.Control | RawInputModifiers.Shift);

        Assert.Equal(new[] { "Fond", "Bloc", "Avant" }, Pile(vm));
        Assert.Same(bloc, Assert.Single(vm.Selection));
        Assert.True(bloc.IsEditing, "un raccourci d'objet a refermé la saisie");
        Assert.True(zone.IsFocused, "un raccourci d'objet a pris le focus à la saisie");
        Assert.Equal("Texte abc", bloc.Text);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(bloc.IsEditing);
        Rendu.UnSeulPas(vm, avant, "la frappe, et les raccourcis d'objet tapés pendant la saisie");
        Assert.Equal(new[] { "Fond", "Bloc", "Avant" }, Pile(vm));
    }

    /// <summary>
    /// La recette : <c>Ctrl+Maj+G</c> se tait aussi dans un champ — il demande plusieurs blocs, ou
    /// un groupe, et la saisie sur la page n'en a jamais : on le joue dans une marge de l'onglet
    /// Document, que la sélection ne masque pas. Ni groupe ni dissociation, et rien d'empilé ; le
    /// champ garde le focus. Depuis N10, la même touche groupe et dissocie, comme dans Publisher,
    /// et <c>Ctrl+Maj+H</c> est libéré : le test, écrit pour <c>H</c>, dissocie par <c>G</c>
    /// (règle 14).
    /// </summary>
    [AvaloniaFact]
    public void Dans_un_champ_grouper_et_dissocier_se_taisent()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 20, 20), Carre("B", 200, 20), Carre("C", 400, 20)));
        var page = vm.CurrentPage!;
        Rendu.Onglet(fenetre, "Document");
        var marge = SaisieDe(Trouver<NumericUpDown>(fenetre, "MargeGauche"));

        // Deux blocs pris, la marge au clavier : Ctrl+Maj+G ne groupe rien.
        vm.SelectOnly(page.Frames[0]);
        vm.ToggleSelection(page.Frames[1]);
        Rendu.Battre();
        Assert.True(vm.CanGroup);
        marge.Focus();
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.G, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(new[] { "A", "B", "C" }, Pile(vm));
        Assert.True(marge.IsFocused, "Ctrl+Maj+G a pris le focus au champ");
        Quitter(fenetre);
        Assert.False(vm.CanUndo, "Ctrl+Maj+G dans un champ a fait un pas");

        // Hors du champ, il groupe, comme avant ; le groupe pris, la marge au clavier :
        // Ctrl+Maj+G ne le dissocie pas.
        Rendu.Touche(fenetre, PhysicalKey.G, RawInputModifiers.Control | RawInputModifiers.Shift);
        var groupe = Assert.IsType<GroupFrame>(page.Frames[0]);
        Assert.Same(groupe, vm.Selected);
        var avant = Etat(vm);

        marge.Focus();
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.G, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Same(groupe, page.Frames[0]);
        Assert.True(marge.IsFocused, "Ctrl+Maj+G a pris le focus au champ");
        Quitter(fenetre);
        Assert.Equal(avant, Etat(vm));

        Rendu.Touche(fenetre, PhysicalKey.G, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(new[] { "A", "B", "C" }, Pile(vm));
    }

    /// <summary>
    /// La garde est au clavier seul — décision du propriétaire, le 26 octobre 2026. En saisie,
    /// <c>Ctrl+Maj+F</c> ne fait rien ; le bouton Premier plan de la barre principale, qui porte la
    /// commande d'origine, n'est pas grisé, et un clic sur lui porte le bloc au premier plan.
    /// </summary>
    [AvaloniaFact]
    public void En_saisie_le_bouton_Premier_plan_agit_et_Ctrl_Maj_F_se_tait()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, Carre("Carré", 400, 300)));
        var premier = fenetre.GetVisualDescendants().OfType<Button>()
            .Single(bouton => ToolTip.GetTip(bouton) is "Premier plan (Ctrl+Maj+F)");

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.Touche(fenetre, PhysicalKey.F, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(new[] { "Bloc", "Carré" }, Pile(vm));
        Assert.True(zone.IsFocused, "Ctrl+Maj+F a pris le focus à la saisie");
        Assert.False(vm.CanUndo, "Ctrl+Maj+F en saisie a fait un pas");

        Assert.True(premier.IsEffectivelyEnabled, "le bouton Premier plan est grisé pendant la saisie");
        Rendu.Cliquer(premier);
        Assert.Equal(new[] { "Carré", "Bloc" }, Pile(vm));
        Assert.Same(bloc, Assert.Single(vm.Selection));
    }

    /// <summary>
    /// Un menu qu'on ouvre prend le focus. Ouvert pendant la saisie, le menu Édition l'ôte à la
    /// zone, et « Tout sélectionner », qui porte la commande d'origine, y est allumé : choisi au
    /// menu, il prend les blocs, comme avant le poste. Seule la touche revient au champ.
    /// </summary>
    [AvaloniaFact]
    public void Un_menu_ouvert_pendant_la_saisie_agit_sur_les_blocs()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, Carre("Carré", 400, 300)));

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Assert.False(vm.ToucheToutSelectionnerCommand.CanExecute(null), "Ctrl+A prendrait la touche à la saisie");
        Assert.True(vm.SelectAllCommand.CanExecute(null), "Tout sélectionner, au menu, lit le focus");

        Rendu.Cliquer(Menu(fenetre, "_Edition"));
        var tout = Menu(fenetre, "_Tout sélectionner");
        Assert.False(zone.IsFocused, "le menu ouvert a laissé le focus à la zone de saisie");
        Assert.True(bloc.IsEditing);
        Assert.True(tout.IsEffectivelyEnabled, "« Tout sélectionner » grisé dans le menu ouvert");

        Rendu.Cliquer(tout);
        Assert.Equal(2, vm.Selection.Count);
    }

    /// <summary>
    /// Hors des champs, tout se joue comme avant : un champ quitté rend la touche aux liaisons.
    /// <c>Ctrl+D</c> duplique, <c>Ctrl+A</c> prend tous les blocs.
    /// </summary>
    [AvaloniaFact]
    public void Un_champ_quitte_rend_les_raccourcis_aux_blocs()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, Carre("Carré", 400, 300)));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var nom = Trouver<TextBox>(fenetre, "ChampNom");
        nom.Focus();
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.D, RawInputModifiers.Control);
        Assert.Equal(2, vm.CurrentPage!.Frames.Count);
        Quitter(fenetre);

        Rendu.Touche(fenetre, PhysicalKey.D, RawInputModifiers.Control);
        Assert.Equal(3, vm.CurrentPage!.Frames.Count);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.Q, RawInputModifiers.Control);
        Assert.Equal(3, vm.Selection.Count);
    }

    // ------------------------------------------------------------------ Alt+flèches

    /// <summary>
    /// La recette : <c>Alt+→</c>, en saisie, déplace le bloc de 3 pt, en un pas, et la saisie
    /// continue — l'aide de Publisher en français : « déplacer l'objet dont le texte a le
    /// curseur ». Le curseur ne bouge pas. Une frappe avant, une frappe après : trois pas,
    /// défaits un à un et dans l'ordre ; <c>Alt+Maj+↓</c> avance de 0,5 pt.
    /// </summary>
    [AvaloniaFact]
    public void En_saisie_Alt_fleche_deplace_le_bloc_en_un_pas_et_la_saisie_continue()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<(string Quoi, string Etat)> { ("l'ouverture", Etat(vm)) };

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        AuBout(fenetre, zone);
        Rendu.Taper(fenetre, " abc");
        etats.Add(("« abc »", Etat(vm)));

        var curseur = zone.CaretIndex;
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
        Assert.Equal(63, bloc.X, precision: 9);
        Assert.Equal(60, bloc.Y, precision: 9);
        Assert.True(bloc.IsEditing, "Alt+→ a refermé la saisie");
        Assert.True(zone.IsFocused, "Alt+→ a pris le focus à la saisie");
        Assert.Equal(curseur, zone.CaretIndex);
        etats.Add(("Alt+→", Etat(vm)));

        Rendu.Touche(fenetre, PhysicalKey.ArrowDown, RawInputModifiers.Alt | RawInputModifiers.Shift);
        Assert.Equal(60.5, bloc.Y, precision: 9);
        etats.Add(("Alt+Maj+↓", Etat(vm)));

        Rendu.Taper(fenetre, "d");
        Assert.Equal("Texte abcd", bloc.Text);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        etats.Add(("« d »", Etat(vm)));

        for (var i = etats.Count - 1; i > 0; i--)
        {
            Assert.True(vm.CanUndo, $"il manque le pas de « {etats[i].Quoi} »");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.True(etats[i - 1].Etat == Etat(vm),
                $"annuler {etats[i].Quoi} ne rend pas l'état d'après {etats[i - 1].Quoi}");
        }

        Assert.False(vm.CanUndo, $"{etats.Count - 1} gestes, plus de {etats.Count - 1} pas");
    }
}
