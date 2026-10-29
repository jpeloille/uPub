using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Le corps et la police : une liste, et une frappe — poste N19 (fiche 23, b6).
///
/// Avant le poste, le corps de la barre contextuelle était un champ numérique sans liste, et la
/// liste des polices ne se tapait pas. Publisher propose le corps dans une liste de tailles où
/// l'on tape aussi, et la police dans une liste où l'on tape son nom (p. 10, 48). Le corps garde
/// son champ (N14) et reçoit la liste standard du noyau (N10) ; la police se tape, et la liste
/// propose les familles qui commencent par la frappe, Entrée posant la première.
///
/// Tout se joue sur la vraie fenêtre, aux gestes du banc (<see cref="Rendu"/>, poste N1). La
/// recette du poste — <c>Scripts/recettes/N19-corps-et-police.sh</c> — joue ces tests par leur
/// trait.
/// </summary>
[Trait("poste", "N19")]
public class CorpsEtPoliceTests
{
    // ------------------------------------------------------------------ outillage

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static BarreContextuelle Barre(Window fenetre) => Trouver<BarreContextuelle>(fenetre, "BarreContextuelle");

    /// <summary>La liste des polices visible de la barre : une seule (règle 1).</summary>
    private static ComboBox Police(Window fenetre) =>
        Barre(fenetre).GetVisualDescendants().OfType<ComboBox>().Single(c => Equals(ToolTip.GetTip(c), "Police") && c.IsEffectivelyVisible);

    /// <summary>La zone où l'on tape le nom de la police.</summary>
    private static TextBox ZoneDe(ComboBox liste) =>
        liste.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "PART_EditableTextBox");

    /// <summary>La liste prise au pointeur, son texte tout sélectionné : ce qu'on tape le remplace.</summary>
    private static TextBox Prendre(ComboBox liste)
    {
        var zone = ZoneDe(liste);
        Rendu.Cliquer(zone);
        Assert.True(zone.IsFocused, "la liste des polices n'a pas pris le focus");
        zone.SelectAll();
        return zone;
    }

    /// <summary>Les libellés des entrées que la liste ouverte montre.</summary>
    private static List<string> Montrees(ComboBox liste) =>
        liste.GetRealizedContainers().OfType<ComboBoxItem>().Where(e => e.IsVisible)
            .Select(Libelle).ToList();

    private static string Libelle(ComboBoxItem entree) =>
        entree.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? "";

    /// <summary>L'entrée marquée, celle qu'Entrée poserait.</summary>
    private static string? Marquee(ComboBox liste) =>
        liste.GetRealizedContainers().OfType<ComboBoxItem>().Where(e => e.Classes.Contains("proposee")).Select(Libelle).SingleOrDefault();

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static string EnPoints(double corps) => corps.ToString("0.##", CultureInfo.CurrentCulture) + " pt";

    private static TextFrame Texte(string nom = "Bloc", double y = 60, string police = "DejaVu Sans") => new()
    {
        Name = nom, X = 60, Y = y, Width = 300, Height = 80, Text = "Texte",
        FontFamily = police, FontSize = 10.5, Foreground = "#FF000000",
    };

    private static TableFrame Tableau(double y)
    {
        var tableau = new TableFrame { Name = "Tableau", X = 60, Y = y, FontFamily = "DejaVu Sans", FontSize = 10 };
        tableau.Columns.Add(new TableColumn(100));
        tableau.Rows.Add(new TableRow(30));
        tableau.Cells.Add(new TableCell { Row = 0, Column = 0, Text = "x" });
        return tableau;
    }

    /// <summary>Tape lettre à lettre, comme la main : chaque lettre est une frappe.</summary>
    private static void TaperLettreALettre(Window fenetre, string texte, Action<string>? apresChaque = null)
    {
        for (var i = 0; i < texte.Length; i++)
        {
            Rendu.Taper(fenetre, texte[i].ToString());
            apresChaque?.Invoke(texte[..(i + 1)]);
        }
    }

    // ------------------------------------------------------------------ la police tapée

    // Repris le 30 octobre 2026, à la demande du propriétaire : un clic sur le nom de la police
    // ouvre la liste, comme avant que la police ne se tape. La zone éditable prenait le clic pour
    // son curseur, et seule la flèche ouvrait la liste.

    [AvaloniaFact]
    public void Un_clic_sur_le_nom_ouvre_la_liste_entiere_la_police_du_bloc_marquee_et_le_nom_tout_pris()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        var zone = ZoneDe(liste);
        Assert.False(liste.IsDropDownOpen);

        Rendu.Cliquer(zone);
        Assert.True(liste.IsDropDownOpen, "le clic sur le nom n'a pas ouvert la liste");
        Assert.Equal(vm.FontFamilies.Count, Montrees(liste).Count);
        Assert.Equal("DejaVu Sans", Marquee(liste));
        Assert.True(zone.IsFocused, "la zone n'a pas le focus");
        Assert.Equal("DejaVu Sans", zone.Text);
        Assert.Equal(0, Math.Min(zone.SelectionStart, zone.SelectionEnd));
        Assert.Equal(zone.Text!.Length, Math.Max(zone.SelectionStart, zone.SelectionEnd));
        Assert.Equal("DejaVu Sans", bloc.FontFamily);
        Assert.Equal(avant, Etat(vm));

        // La première lettre remplace tout le nom, et filtre.
        Rendu.Taper(fenetre, "L");
        Assert.Equal("L", zone.Text);
        var montrees = Montrees(liste);
        Assert.NotEmpty(montrees);
        Assert.All(montrees, f => Assert.StartsWith("L", f, StringComparison.CurrentCultureIgnoreCase));
        Assert.Equal("DejaVu Sans", bloc.FontFamily);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "ouvrir la liste au clic a laissé un pas");
    }

    [AvaloniaFact]
    public void Un_second_clic_sans_rien_taper_referme_la_liste_sans_rien_poser()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        var zone = ZoneDe(liste);

        Rendu.Cliquer(zone);
        Assert.True(liste.IsDropDownOpen);
        // ↓ marque une autre entrée : le clic qui referme ne la pose pas.
        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Rendu.Cliquer(zone);
        Assert.False(liste.IsDropDownOpen, "le second clic n'a pas refermé la liste");
        Assert.Equal("DejaVu Sans", bloc.FontFamily);
        Assert.Equal("DejaVu Sans", zone.Text);

        // Et un troisième la rouvre : le clic est une bascule.
        Rendu.Cliquer(zone);
        Assert.True(liste.IsDropDownOpen, "le troisième clic n'a pas rouvert la liste");
        Rendu.Cliquer(zone);
        Assert.False(liste.IsDropDownOpen);

        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "ouvrir et refermer la liste a laissé un pas");
    }

    [AvaloniaFact]
    public void La_fleche_ouvre_et_referme_la_liste_comme_avant()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        // La flèche du thème ne se vise pas : c'est le fond de la liste qui prend le clic, à sa place.
        var fleche = Rendu.Centre(liste.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "DropDownGlyph"));

        Rendu.Cliquer(fenetre, fleche);
        Assert.True(liste.IsDropDownOpen, "la flèche n'ouvre plus la liste");
        Assert.Equal(vm.FontFamilies.Count, Montrees(liste).Count);
        Rendu.Cliquer(fenetre, fleche);
        Assert.False(liste.IsDropDownOpen, "la flèche ne referme plus la liste");
        Assert.Equal("DejaVu Sans", bloc.FontFamily);
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void Un_clic_pendant_la_frappe_garde_la_frappe_et_la_liste()
    {
        var bloc = Texte(police: "Liberation Serif");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        var zone = ZoneDe(liste);

        Rendu.Cliquer(zone);
        Rendu.Taper(fenetre, "DejaVu");
        var proposees = Montrees(liste);
        Assert.True(proposees.Count >= 2, "la machine n'a pas les DejaVu");

        Rendu.Cliquer(zone);
        Assert.Equal("DejaVu", zone.Text);
        Assert.True(liste.IsDropDownOpen, "le clic pendant la frappe a refermé la liste");
        Assert.Equal(proposees, Montrees(liste));
        Assert.Equal(proposees[0], Marquee(liste));
        Assert.Equal("Liberation Serif", bloc.FontFamily);
        Assert.Equal(avant, Etat(vm));

        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(proposees[0], bloc.FontFamily);
        Rendu.UnSeulPas(vm, avant, "la police tapée après un clic");
    }

    [AvaloniaFact]
    public void Taper_le_debut_dune_police_la_propose_et_Entree_la_pose_en_un_pas()
    {
        // « Geo » propose Georgia, dit le plan. La machine du banc n'a pas Georgia : un autre bloc
        // du document l'emploie, et la liste la compte parmi ses entrées, « (absente) » (Q20).
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, Texte("Autre", 200, "Georgia")));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        var zone = Prendre(liste);

        Rendu.Taper(fenetre, "Geo");
        var attendues = vm.FontFamilies.Where(f => f.StartsWith("Geo", StringComparison.CurrentCultureIgnoreCase)).ToList();
        Assert.Contains("Georgia", attendues);
        Assert.True(liste.IsDropDownOpen, "la frappe n'a rien proposé");
        Assert.Equal(attendues.Select(f => vm.PolicesDeLaMachine.Contains(f) ? f : $"{f} (absente)"), Montrees(liste));
        Assert.Equal(attendues[0] == "Georgia" ? "Georgia (absente)" : attendues[0], Marquee(liste));
        Assert.Equal("Geo", zone.Text);
        Assert.Equal("DejaVu Sans", bloc.FontFamily);

        // Entrée pose la première proposition, en un pas, et rend la planche.
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(attendues[0], bloc.FontFamily);
        Assert.False(liste.IsDropDownOpen, "la liste est restée ouverte");
        Assert.False(Barre(fenetre).IsKeyboardFocusWithin, "Entrée n'a pas rendu la planche");
        Rendu.Touche(fenetre, PhysicalKey.T);
        Assert.Equal(EditorTool.Text, vm.ActiveTool);
        Rendu.UnSeulPas(vm, avant, "la police tapée");
    }

    [AvaloniaFact]
    public void Une_police_de_la_machine_se_tape_et_la_zone_montre_son_libelle()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        var zone = Prendre(liste);

        // En minuscules : la proposition ne regarde pas la casse.
        Rendu.Taper(fenetre, "dejavu se");
        Assert.Equal(["DejaVu Serif"], Montrees(liste));
        Assert.Equal("DejaVu Serif", Marquee(liste));
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal("DejaVu Serif", bloc.FontFamily);
        Assert.Equal("DejaVu Serif", zone.Text);
        Assert.Equal("DejaVu Serif", liste.SelectedItem);
        bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "DejaVu Serif tapée"));

        // Les entrées du jeu se tapent par leur libellé, et la zone le montre — non la référence
        // que le bloc porte.
        zone = Prendre(Police(fenetre));
        Rendu.Taper(fenetre, "Corps du");
        Assert.Single(Montrees(Police(fenetre)));
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal("scheme:body", bloc.FontFamily);
        Assert.StartsWith("Corps du jeu — ", zone.Text);
        Assert.DoesNotContain("scheme:", zone.Text);
    }

    [AvaloniaFact]
    public void La_frappe_ne_pose_rien_avant_Entree()
    {
        // La liste d'Avalonia prenait, à chaque lettre, l'entrée dont le texte était exactement
        // celui tapé : en tapant « DejaVu Sans Mono », DejaVu Sans partait au onzième caractère.
        var bloc = Texte(police: "Liberation Serif");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        Prendre(liste);

        TaperLettreALettre(fenetre, "DejaVu Sans Mono", tape =>
        {
            Assert.Equal("Liberation Serif", bloc.FontFamily);
            Assert.Equal("Liberation Serif", liste.SelectedItem);
            Assert.Equal(avant, Etat(vm));
        });

        // La proposition exacte passe devant : Entrée pose DejaVu Sans Mono, une fois.
        Assert.Equal("DejaVu Sans Mono", Marquee(liste));
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal("DejaVu Sans Mono", bloc.FontFamily);
        Rendu.UnSeulPas(vm, avant, "la police tapée lettre à lettre");
    }

    [AvaloniaFact]
    public void Les_fleches_promenent_la_proposition_et_rien_ne_part_avant_Entree()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        var zone = Prendre(liste);

        Rendu.Taper(fenetre, "DejaVu");
        var proposees = Montrees(liste);
        Assert.Equal(vm.PolicesDeLaMachine.Where(f => f.StartsWith("DejaVu", StringComparison.CurrentCultureIgnoreCase)), proposees);
        Assert.True(proposees.Count >= 3, "la machine n'a pas les trois DejaVu");
        Assert.Equal(proposees[0], Marquee(liste));

        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Assert.Equal(proposees[1], Marquee(liste));
        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Assert.Equal(proposees[2], Marquee(liste));
        Rendu.Touche(fenetre, PhysicalKey.ArrowUp);
        Assert.Equal(proposees[1], Marquee(liste));
        Assert.Equal("DejaVu", zone.Text);
        Assert.Equal(avant, Etat(vm));

        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(proposees[1], bloc.FontFamily);
        Rendu.UnSeulPas(vm, avant, "la police marquée aux flèches");
    }

    [AvaloniaFact]
    public void Une_police_que_la_machine_na_pas_est_refusee_et_dite()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        var zone = Prendre(liste);

        Rendu.Taper(fenetre, "Police Imaginaire");
        Assert.False(liste.IsDropDownOpen, "la liste propose ce que rien ne commence");
        Rendu.Touche(fenetre, PhysicalKey.Enter);

        Assert.Equal("DejaVu Sans", bloc.FontFamily);
        Assert.Equal("DejaVu Sans", zone.Text);
        Assert.Equal("« Police Imaginaire » n'est pas une police de cette machine : la police reste DejaVu Sans.", vm.Status);
        // Refusée, la liste garde le focus, tout pris, pour qu'on retape.
        Assert.True(zone.IsFocused, "un refus a rendu la planche");
        Assert.Equal(zone.Text.Length, Math.Abs(zone.SelectionEnd - zone.SelectionStart));
        Assert.Equal(avant, Etat(vm));

        Rendu.Taper(fenetre, "DejaVu Serif");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal("DejaVu Serif", bloc.FontFamily);
        Rendu.UnSeulPas(vm, avant, "la police retapée après un refus");
    }

    [AvaloniaFact]
    public void Echap_renonce_a_la_police_tapee_et_rend_la_planche()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        var zone = Prendre(liste);

        Rendu.Taper(fenetre, "DejaVu Se");
        Assert.True(liste.IsDropDownOpen);
        Rendu.Touche(fenetre, PhysicalKey.Escape);

        Assert.False(liste.IsDropDownOpen, "Échap a laissé la liste ouverte");
        Assert.Equal("DejaVu Sans", bloc.FontFamily);
        Assert.Equal("DejaVu Sans", zone.Text);
        Assert.False(Barre(fenetre).IsKeyboardFocusWithin, "Échap n'a pas rendu la planche");
        Rendu.Touche(fenetre, PhysicalKey.T);
        Assert.Equal(EditorTool.Text, vm.ActiveTool);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "Échap a laissé un pas d'annulation");
    }

    [AvaloniaFact]
    public void Quitter_la_liste_au_pointeur_renonce_a_la_frappe()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        var zone = Prendre(liste);

        Rendu.Taper(fenetre, "DejaVu Se");
        Assert.True(liste.IsDropDownOpen);
        var corps = Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc").GetVisualDescendants().OfType<TextBox>().First();

        // Un premier clic dans le corps referme la liste ouverte, et rien d'autre : c'est la liste
        // d'Avalonia, qui reprend alors le focus. La frappe attend toujours.
        Rendu.Cliquer(fenetre, Rendu.Centre(corps));
        Assert.False(liste.IsDropDownOpen, "le clic n'a pas refermé la liste");
        Assert.True(zone.IsKeyboardFocusWithin, "la liste refermée n'a pas gardé le focus");
        Assert.Equal("DejaVu Sans", bloc.FontFamily);

        // Le second quitte la liste : la police à moitié tapée ne part pas.
        Rendu.Cliquer(corps);
        Assert.True(corps.IsFocused, "le clic n'a pas mené au corps");
        Assert.Equal("DejaVu Sans", bloc.FontFamily);
        Assert.Equal("DejaVu Sans", zone.Text);
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "quitter la liste a laissé un pas");
    }

    [AvaloniaFact]
    public void Tab_pose_la_police_tapee_et_passe_au_corps()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        Prendre(Police(fenetre));

        Rendu.Taper(fenetre, "DejaVu Se");
        Rendu.Touche(fenetre, PhysicalKey.Tab);
        Assert.Equal("DejaVu Serif", bloc.FontFamily);
        Assert.True(Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc").IsKeyboardFocusWithin, "Tab n'a pas mené au corps");
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
        Rendu.UnSeulPas(vm, avant, "la police posée par Tab");
    }

    [AvaloniaFact]
    public void Une_police_absente_du_document_reste_proposee_absente()
    {
        // Q20 tient : la police d'un bloc que la machine n'a pas reste une entrée, libellée
        // « (absente) » ; la zone la montre ainsi, et la frappe la retrouve.
        var absent = Texte("Absent", 60, "Police Absente De Cette Machine");
        var present = Texte("Present", 200);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(absent, present));

        foreach (var bloc in new[] { absent, present, absent })
        {
            vm.SelectOnly(bloc);
            Rendu.Battre();
        }

        var liste = Police(fenetre);
        Assert.Equal("Police Absente De Cette Machine", liste.SelectedItem);
        Assert.Equal("Police Absente De Cette Machine (absente)", ZoneDe(liste).Text);
        Assert.Equal("Police Absente De Cette Machine", absent.FontFamily);
        Assert.False(vm.CanUndo, "la sélection a écrit dans le document");

        // Sur l'autre bloc, on la tape.
        vm.SelectOnly(present);
        Rendu.Battre();
        var avant = Etat(vm);
        liste = Police(fenetre);
        Prendre(liste);
        Rendu.Taper(fenetre, "Police Ab");
        Assert.Equal(["Police Absente De Cette Machine (absente)"], Montrees(liste));
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal("Police Absente De Cette Machine", present.FontFamily);
        Rendu.UnSeulPas(vm, avant, "la police absente retapée");
        Rendu.Encre(fenetre);
    }

    [AvaloniaFact]
    public void Une_police_choisie_au_pointeur_dans_la_liste_ouverte_fait_un_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        Prendre(liste);

        Rendu.Taper(fenetre, "DejaVu");
        var serif = liste.GetRealizedContainers().OfType<ComboBoxItem>().Single(e => Libelle(e) == "DejaVu Serif");
        Rendu.Cliquer(serif);
        Assert.Equal("DejaVu Serif", bloc.FontFamily);
        Assert.False(liste.IsDropDownOpen);
        Assert.Equal("DejaVu Serif", ZoneDe(liste).Text);
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
        Rendu.UnSeulPas(vm, avant, "la police choisie au pointeur");
        // La liste rouverte montre toutes les entrées : le filtre de la frappe est levé.
        liste = Police(fenetre);
        liste.IsDropDownOpen = true;
        Rendu.Battre();
        Assert.Equal(vm.FontFamilies.Count, Montrees(liste).Count);
        liste.IsDropDownOpen = false;
        Rendu.Battre();
    }

    [AvaloniaFact]
    public void Le_tableau_a_la_meme_police_tapee()
    {
        var tableau = Tableau(60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        vm.SelectOnly(tableau);
        Rendu.Battre();
        var avant = Etat(vm);
        var liste = Police(fenetre);
        Assert.IsType<ListeDesPolices>(liste);
        Prendre(liste);

        TaperLettreALettre(fenetre, "DejaVu Se", _ => Assert.Equal("DejaVu Sans", tableau.FontFamily));
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal("DejaVu Serif", tableau.FontFamily);
        Rendu.UnSeulPas(vm, avant, "la police tapée du tableau");
    }

    // ------------------------------------------------------------------ le corps

    [AvaloniaFact]
    public void La_liste_des_tailles_pose_14_pt_en_un_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        // Une liste, et une seule, à côté du corps (règle 1).
        var chevron = Barre(fenetre).GetVisualDescendants().OfType<Button>()
            .Single(b => Equals(ToolTip.GetTip(b), "Tailles du corps") && b.IsEffectivelyVisible);
        var tailles = Assert.IsType<MenuFlyout>(chevron.Flyout);
        Rendu.Cliquer(chevron);
        Assert.True(tailles.IsOpen, "le chevron n'ouvre pas les tailles");
        var entrees = tailles.Items.OfType<MenuItem>().ToList();
        Assert.Equal(CorpsStandard.Tailles.Select(t => t.ToString("0.##", CultureInfo.CurrentCulture)), entrees.Select(e => e.Header as string));
        Assert.Equal(CorpsStandard.Tailles.Select(t => t == 10.5), entrees.Select(e => e.IsChecked));

        Rendu.Cliquer(entrees.Single(e => (string?)e.Header == "14"));
        Assert.Equal(14, bloc.FontSize);
        Assert.False(tailles.IsOpen);
        Assert.Equal(EnPoints(14), Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc").Text);
        Assert.False(Barre(fenetre).IsKeyboardFocusWithin, "la liste refermée a laissé le focus à la barre");
        bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "14 pt de la liste"));

        // La taille que le bloc a déjà ne fait pas de pas.
        Rendu.Cliquer(chevron);
        Rendu.Cliquer(tailles.Items.OfType<MenuItem>().Single(e => (string?)e.Header == (10.5).ToString("0.##", CultureInfo.CurrentCulture)));
        Assert.Equal(10.5, bloc.FontSize);
        Assert.False(vm.CanUndo, "la même taille a fait un pas");
    }

    [AvaloniaFact]
    public void Le_tableau_a_la_meme_liste_des_tailles()
    {
        var tableau = Tableau(60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        vm.SelectOnly(tableau);
        Rendu.Battre();
        var avant = Etat(vm);

        var chevron = Barre(fenetre).GetVisualDescendants().OfType<Button>()
            .Single(b => Equals(ToolTip.GetTip(b), "Tailles du corps") && b.IsEffectivelyVisible);
        Rendu.Cliquer(chevron);
        var entrees = Assert.IsType<MenuFlyout>(chevron.Flyout).Items.OfType<MenuItem>().ToList();
        Assert.Equal(CorpsStandard.Tailles.Select(t => t == 10), entrees.Select(e => e.IsChecked));
        Rendu.Cliquer(entrees.Single(e => (string?)e.Header == "24"));
        Assert.Equal(24, tableau.FontSize);
        Rendu.UnSeulPas(vm, avant, "24 pt du tableau");
    }

    [AvaloniaFact]
    public void Dix_virgule_cinq_tape_donne_10_5_pt()
    {
        var bloc = Texte();
        bloc.FontSize = 12;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        var corps = Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc");
        var saisie = corps.GetVisualDescendants().OfType<TextBox>().First();
        Rendu.Cliquer(saisie);
        saisie.SelectAll();
        Rendu.Taper(fenetre, "10,5");
        Assert.Equal(12, bloc.FontSize);
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(10.5, bloc.FontSize);
        Assert.Equal(EnPoints(10.5), corps.Text);
        bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "10,5 tapé"));

        // ↑ fait un pas de 0,5 pt, comme avant le poste.
        saisie = Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc").GetVisualDescendants().OfType<TextBox>().First();
        Rendu.Cliquer(saisie);
        Rendu.Touche(fenetre, PhysicalKey.ArrowUp);
        Assert.Equal(12.5, bloc.FontSize);
    }

    [AvaloniaFact]
    public void Un_corps_tape_part_avant_la_taille_de_la_liste_chacun_son_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        var saisie = Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc").GetVisualDescendants().OfType<TextBox>().First();
        Rendu.Cliquer(saisie);
        saisie.SelectAll();
        Rendu.Taper(fenetre, "13");
        var chevron = Trouver<Button>(Barre(fenetre), "TaillesDuCorps");
        Rendu.Cliquer(chevron);
        // La liste qui s'ouvre fait quitter le champ, dont la sortie valide (N14).
        Assert.Equal(13, bloc.FontSize);
        Rendu.Cliquer(Assert.IsType<MenuFlyout>(chevron.Flyout).Items.OfType<MenuItem>().Single(e => (string?)e.Header == "18"));
        Assert.Equal(18, bloc.FontSize);

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(13, ((TextFrame)vm.SingleSelected!).FontSize);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "plus de deux pas");
    }

    [AvaloniaFact]
    public void Ouvrir_et_refermer_la_liste_ne_reecrit_pas_un_corps_hors_liste()
    {
        // La mémoire d'une case de Q14 vaut pour le corps : un corps que l'affichage arrondit,
        // la liste ouverte et refermée, le champ traversé, reste au chiffre près.
        var bloc = Texte();
        bloc.FontSize = 10.123456789;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        var chevron = Trouver<Button>(Barre(fenetre), "TaillesDuCorps");
        Rendu.Cliquer(chevron);
        var tailles = Assert.IsType<MenuFlyout>(chevron.Flyout);
        Assert.DoesNotContain(tailles.Items.OfType<MenuItem>(), e => e.IsChecked);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(tailles.IsOpen, "Échap n'a pas refermé la liste");

        var saisie = Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc").GetVisualDescendants().OfType<TextBox>().First();
        Rendu.Cliquer(saisie);
        Rendu.Touche(fenetre, PhysicalKey.Tab);
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
        Assert.Equal(10.123456789, bloc.FontSize);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "la liste ou le champ ont réécrit le corps");
    }
}
