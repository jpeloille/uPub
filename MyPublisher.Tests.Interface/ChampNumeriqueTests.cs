using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
/// Les champs — poste N14 (fiche 23, f2, f5, f7, f8, f10, f11).
///
/// Avant le poste, les champs numériques du dock et de la barre étaient des
/// <c>NumericUpDown</c> d'Avalonia : la valeur partait à chaque frappe lisible — taper « 210 »
/// dans la largeur de la page passait par 2 mm et par 21 mm —, une saisie illisible ou hors des
/// bornes était refusée en silence, une seule unité se lisait, la sienne, Échap validait dans
/// la barre et ne faisait rien au dock, le catalogue et le dialogue des coordonnées ne se
/// fermaient pas par Échap, et Tab s'arrêtait sur les titres de section du dock.
///
/// Tout se joue au clavier sur la vraie fenêtre, aux gestes du banc (<see cref="Rendu"/>, poste
/// N1). La recette du poste — <c>Scripts/recettes/N14-les-champs.sh</c> — joue ces tests par
/// leur trait.
/// </summary>
[Trait("poste", "N14")]
public class ChampNumeriqueTests
{
    // ------------------------------------------------------------------ outillage

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    /// <summary>Le champ de saisie d'un champ numérique : c'est lui qui prend le focus.</summary>
    private static TextBox SaisieDe(NumericUpDown champ) => champ.GetVisualDescendants().OfType<TextBox>().First();

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    private static string Mm(decimal valeur, string format = "0.0") => valeur.ToString(format, Culture) + " mm";

    private static TextFrame Texte() => new()
    {
        Name = "Bloc", X = 60, Y = 60, Width = 300, Height = 80, Text = "Texte",
        FontFamily = "DejaVu Sans", FontSize = 10.5, Foreground = "#FF000000",
    };

    /// <summary>
    /// Le champ pris au pointeur, son texte tout sélectionné : ce qu'on tape le remplace.
    /// Rend sa zone de saisie.
    /// </summary>
    private static TextBox Prendre(Window fenetre, NumericUpDown champ)
    {
        var saisie = SaisieDe(champ);
        Rendu.Cliquer(saisie);
        Assert.True(saisie.IsFocused, $"« {champ.Name ?? ToolTip.GetTip(champ)} » n'a pas pris le focus");
        saisie.SelectAll();
        return saisie;
    }

    private static void Quitter(Window fenetre)
    {
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
    }

    // ------------------------------------------------------------- les unités (f5)

    [AvaloniaFact]
    public void Une_autre_unite_se_tape_et_se_convertit_l_affichage_reste_en_millimetres()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        // « 2 cm » dans X donne 20,0 mm.
        var x = Trouver<NumericUpDown>(fenetre, "ChampX");
        Prendre(fenetre, x);
        Rendu.Taper(fenetre, "2 cm");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(Units.MmToPoints(20), bloc.X, precision: 9);
        Assert.Equal(Mm(20), x.Text);

        // « 1" » dans Y donne 25,4 mm ; « 2,5 cm », à la virgule, 25,0 mm dans la hauteur.
        var y = Trouver<NumericUpDown>(fenetre, "ChampY");
        Prendre(fenetre, y);
        Rendu.Taper(fenetre, "1\"");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(72, bloc.Y, precision: 9);
        Assert.Equal(Mm(25.4m), y.Text);

        var hauteur = Trouver<NumericUpDown>(fenetre, "ChampHauteur");
        Prendre(fenetre, hauteur);
        Rendu.Taper(fenetre, "2,5 cm");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(Units.MmToPoints(25), bloc.Height, precision: 9);
        Assert.Equal(Mm(25), hauteur.Text);

        // Le pica, douze points ; le pouce écrit « po » ou « in ».
        Prendre(fenetre, x);
        Rendu.Taper(fenetre, "6 pi");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(72, bloc.X, precision: 9);
        Prendre(fenetre, x);
        Rendu.Taper(fenetre, "2 po");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(144, bloc.X, precision: 9);
        Prendre(fenetre, x);
        Rendu.Taper(fenetre, "1in");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(72, bloc.X, precision: 9);
        Quitter(fenetre);

        // Un corps en millimètres : la barre l'écrit en points.
        var corps = Trouver<NumericUpDown>(fenetre, "CorpsDuBloc");
        Prendre(fenetre, corps);
        Rendu.Taper(fenetre, "4 mm");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(Units.MmToPoints(4), bloc.FontSize, precision: 9);
        Assert.Equal(Units.MmToPoints(4).ToString("0.##", Culture) + " pt", corps.Text);

        // « 12 pt » dans une marge de l'onglet Document donne 4,23 mm.
        Rendu.Onglet(fenetre, "Document");
        var marge = Trouver<NumericUpDown>(fenetre, "MargeGauche");
        Prendre(fenetre, marge);
        Rendu.Taper(fenetre, "12 pt");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Quitter(fenetre);
        Assert.Equal(12, vm.Document.MarginLeft, precision: 9);
        Assert.Equal(Mm(4.23m, "0.##"), marge.Text);
    }

    // ----------------------------------------------- la valeur part une fois (f8, f2)

    [AvaloniaFact]
    public void En_tapant_210_la_largeur_de_la_page_n_est_ecrite_qu_une_fois_en_un_pas()
    {
        var document = Rendu.Feuille(Texte());
        document.PageWidth = Units.MmToPoints(148);
        document.PageHeight = Units.MmToPoints(210);
        var (fenetre, vm) = Rendu.Ouvrir(document);
        Rendu.Onglet(fenetre, "Document");
        var avant = Etat(vm);

        var ecritures = 0;
        vm.Document.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Document.PageWidth)) ecritures++;
        };

        var largeur = Trouver<NumericUpDown>(fenetre, "LargeurDePage");
        var saisie = Prendre(fenetre, largeur);
        foreach (var chiffre in new[] { "2", "1", "0" })
        {
            Rendu.Taper(fenetre, chiffre);
            Assert.Equal(0, ecritures);
            Assert.Equal(Units.MmToPoints(148), vm.Document.PageWidth, precision: 9);
        }
        Assert.Equal("210", saisie.Text);

        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(1, ecritures);
        Assert.Equal(Units.MmToPoints(210), vm.Document.PageWidth, precision: 9);
        Assert.True(saisie.IsFocused, "Entrée au dock a quitté le champ");

        Quitter(fenetre);
        Assert.Equal(1, ecritures);
        Rendu.UnSeulPas(vm, avant, "la largeur tapée");
    }

    [AvaloniaFact]
    public void Tab_valide_et_les_fleches_font_un_pas_depuis_ce_qui_est_tape()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        // Tab : la valeur part, et le champ suivant prend le focus.
        var x = Trouver<NumericUpDown>(fenetre, "ChampX");
        Prendre(fenetre, x);
        Rendu.Taper(fenetre, "30");
        Assert.Equal(60, bloc.X);
        Rendu.Touche(fenetre, PhysicalKey.Tab);
        Assert.Equal(Units.MmToPoints(30), bloc.X, precision: 9);
        Assert.True(SaisieDe(Trouver<NumericUpDown>(fenetre, "ChampY")).IsFocused, "Tab n'a pas mené de X à Y");

        // ↑ part de ce qui est tapé : « 40 », puis un pas d'un millimètre.
        var y = Trouver<NumericUpDown>(fenetre, "ChampY");
        SaisieDe(y).SelectAll();
        Rendu.Taper(fenetre, "40");
        Rendu.Touche(fenetre, PhysicalKey.ArrowUp);
        Assert.Equal(Units.MmToPoints(41), bloc.Y, precision: 9);
        Assert.Equal(Mm(41), y.Text);
        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Assert.Equal(Units.MmToPoints(39), bloc.Y, precision: 9);

        // La molette, le champ ayant le focus, fait de même.
        var centre = Rendu.Centre(y);
        fenetre.MouseWheel(centre, new Vector(0, 1));
        Rendu.Battre();
        Assert.Equal(Units.MmToPoints(40), bloc.Y, precision: 9);

        // Un champ quitté : X et Y dans des pas à eux, que deux annulations défont.
        Quitter(fenetre);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(60, ((TextFrame)vm.SingleSelected!).Y);
        Assert.Equal(Units.MmToPoints(30), ((TextFrame)vm.SingleSelected!).X, precision: 9);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, Etat(vm));
    }

    // --------------------------------------------------------------- le refus (f7)

    [AvaloniaFact]
    public void Une_saisie_illisible_ou_hors_des_bornes_laisse_la_valeur_et_le_dit()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        // « trente » : la valeur reste, le champ la remontre, tout pris, et la barre d'état le dit.
        var x = Trouver<NumericUpDown>(fenetre, "ChampX");
        var affiche = x.Text;
        var saisie = Prendre(fenetre, x);
        Rendu.Taper(fenetre, "trente");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(60, bloc.X);
        Assert.Equal(affiche, x.Text);
        Assert.True(saisie.IsFocused, "le refus a quitté le champ");
        Assert.Equal(affiche, saisie.SelectedText);
        Assert.Contains("« trente » ne se lit pas", vm.Status);
        Assert.Contains("mm, cm, pt, pi ou po", vm.Status);
        Assert.Contains(affiche!, vm.Status);

        // ↑ fait encore un pas : le refus n'a pas éteint les flèches.
        Rendu.Touche(fenetre, PhysicalKey.ArrowUp);
        Assert.Equal(Units.MmToPoints(Units.PointsToMm(60) + 1), bloc.X, precision: 9);
        Quitter(fenetre);
        Rendu.UnSeulPas(vm, avant, "le pas de la flèche après le refus");

        // Hors des bornes : 5 000 mm de large n'est pas ramené à 2 000, et les bornes se disent.
        Rendu.Onglet(fenetre, "Document");
        var largeur = Trouver<NumericUpDown>(fenetre, "LargeurDePage");
        var page = vm.Document.PageWidth;
        var avantLaPage = Etat(vm);
        Prendre(fenetre, largeur);
        Rendu.Taper(fenetre, "5000");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(page, vm.Document.PageWidth);
        Assert.Contains("« 5000 » est refusé", vm.Status);
        Assert.Contains("de " + Mm(10, "0.##") + " à " + Mm(2000, "0.##"), vm.Status);

        // Refusé à la sortie du champ aussi : Tab ne fait rien partir.
        largeur = Trouver<NumericUpDown>(fenetre, "LargeurDePage");
        Prendre(fenetre, largeur);
        Rendu.Taper(fenetre, "2 m");
        Rendu.Touche(fenetre, PhysicalKey.Tab);
        Assert.Equal(page, vm.Document.PageWidth);
        Assert.Contains("« 2 m » ne se lit pas", vm.Status);
        Quitter(fenetre);
        Assert.Equal(avantLaPage, Etat(vm));
        Assert.False(vm.CanUndo, "un refus a laissé un pas d'annulation");
    }

    // -------------------------------------------------- Échap et Entrée (f10, règle 13)

    [AvaloniaFact]
    public void Echap_renonce_et_rend_la_planche_dans_la_barre_comme_au_dock()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        // Le corps : « 14 » tapé, puis Échap — la valeur d'avant, aucun pas, la planche reprend la
        // main : « T » arme l'outil texte.
        var corps = Trouver<NumericUpDown>(fenetre, "CorpsDuBloc");
        var affiche = corps.Text;
        var saisie = Prendre(fenetre, corps);
        Rendu.Taper(fenetre, "14");
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal(10.5, bloc.FontSize);
        Assert.Equal(affiche, corps.Text);
        Assert.False(saisie.IsKeyboardFocusWithin, "Échap n'a pas quitté le corps");
        Assert.Same(bloc, vm.SingleSelected);
        Rendu.Touche(fenetre, PhysicalKey.T);
        Assert.Equal(EditorTool.Text, vm.ActiveTool);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal(EditorTool.Select, vm.ActiveTool);

        // Au dock : X, « 99 », Échap — de même ; la flèche, ensuite, pousse le bloc.
        var x = Trouver<NumericUpDown>(fenetre, "ChampX");
        var afficheX = x.Text;
        saisie = Prendre(fenetre, x);
        Rendu.Taper(fenetre, "99");
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal(60, bloc.X);
        Assert.Equal(afficheX, x.Text);
        Assert.False(saisie.IsKeyboardFocusWithin, "Échap n'a pas quitté X");
        Assert.Same(bloc, vm.SingleSelected);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "Échap a laissé un pas d'annulation");

        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        Assert.True(bloc.X > 60, "la flèche n'a pas poussé le bloc");
    }

    [AvaloniaFact]
    public void Entree_dans_la_barre_valide_et_rend_la_planche_au_dock_garde_le_champ()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        var corps = Trouver<NumericUpDown>(fenetre, "CorpsDuBloc");
        var saisie = Prendre(fenetre, corps);
        Rendu.Taper(fenetre, "14");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(14, bloc.FontSize);
        Assert.False(saisie.IsKeyboardFocusWithin, "Entrée dans la barre n'a pas rendu la planche");
        Rendu.Touche(fenetre, PhysicalKey.T);
        Assert.Equal(EditorTool.Text, vm.ActiveTool);
        var apres = Rendu.UnSeulPas(vm, avant, "le corps validé par Entrée");
        Assert.IsType<TextFrame>(apres);

        // Au dock, Entrée valide et garde le champ, comme avant le poste.
        bloc = (TextFrame)vm.SingleSelected!;
        var x = Trouver<NumericUpDown>(fenetre, "ChampX");
        saisie = Prendre(fenetre, x);
        Rendu.Taper(fenetre, "30");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(Units.MmToPoints(30), bloc.X, precision: 9);
        Assert.True(saisie.IsFocused, "Entrée au dock a quitté le champ");
    }

    // ------------------------------------------------ ce qui attend ne se perd pas

    [AvaloniaFact]
    public void Ce_qui_attend_dans_un_champ_part_avant_Annuler_et_l_enregistrement()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        // « 40 » tapé dans Y, sans le valider, puis Ctrl+Z : la frappe fait son pas, que
        // l'annulation défait — comme quand la valeur partait à la frappe.
        var y = Trouver<NumericUpDown>(fenetre, "ChampY");
        Prendre(fenetre, y);
        Rendu.Taper(fenetre, "40");
        Rendu.Touche(fenetre, PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "la frappe annulée a laissé un pas");
        Assert.True(vm.CanRedo, "la frappe n'a pas fait son pas avant l'annulation");
        Quitter(fenetre);

        // « 45 » tapé, puis enregistrer : le fichier a la valeur tapée.
        var chemin = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"n14-{Guid.NewGuid():N}.mypub");
        try
        {
            y = Trouver<NumericUpDown>(fenetre, "ChampY");
            Prendre(fenetre, y);
            Rendu.Taper(fenetre, "45");
            vm.SaveTo(chemin);
            var relu = DocumentSerializer.Load(chemin);
            Assert.Equal(Units.MmToPoints(45), relu.Pages[0].Frames.Single().Y, precision: 9);
        }
        finally
        {
            System.IO.File.Delete(chemin);
        }
    }

    [AvaloniaFact]
    public void Fermer_le_document_pendant_qu_un_champ_attend_pose_la_question()
    {
        // Le piège du plan : ClosePropertyEdit valide le champ en attente avant de refermer sa
        // transaction. Sans cela, « 40 » tapé dans Y, puis Fermer, trouvait un document que rien
        // n'avait modifié, et le laissait partir sans rien demander — la frappe perdue.
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var principale = Assert.IsType<MainWindow>(fenetre);
        var questions = 0;
        principale.AvantDePerdre = _ =>
        {
            questions++;
            return System.Threading.Tasks.Task.FromResult(ReponseAvantDePerdre.Annuler);
        };
        vm.SelectOnly(bloc);
        Rendu.Battre();

        Prendre(fenetre, Trouver<NumericUpDown>(fenetre, "ChampY"));
        Rendu.Taper(fenetre, "40");
        Assert.False(vm.IsDirty);

        var part = Rendu.Attendre(principale.LaisserPartirLeDocument());
        Assert.False(part, "le document est parti sans qu'on le demande");
        Assert.Equal(1, questions);
        Assert.Equal(Units.MmToPoints(40), bloc.Y, precision: 9);
        Assert.True(vm.IsDirty, "la frappe validée n'a pas marqué le document");
        Assert.True(vm.CanUndo, "la frappe validée n'a pas fait son pas");
    }

    [AvaloniaFact]
    public void Ce_qui_attend_reste_au_bloc_qu_on_quitte_quand_la_selection_change()
    {
        // La sélection qui change sans que le champ perde le focus — une annulation, un geste
        // du modèle de vue — passe les champs gardés au bloc d'après (Q23) : ce qui attendait
        // dans X, dans L ou dans le corps part au bloc qu'on quitte, et à lui seul.
        foreach (var nom in new[] { "ChampX", "ChampLargeur", "CorpsDuBloc" })
        {
            var premier = Texte();
            var second = Texte();
            second.Name = "Second";
            second.Y = 300;
            var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(premier, second));
            vm.SelectOnly(premier);
            Rendu.Battre();
            var avantDuSecond = DocumentSerializer.ToJson(Rendu.Feuille(second));

            Prendre(fenetre, Trouver<NumericUpDown>(fenetre, nom));
            Rendu.Taper(fenetre, "44");
            vm.SelectOnly(second);
            Rendu.Battre();

            var attendu = nom == "CorpsDuBloc" ? 44 : Units.MmToPoints(44);
            var lu = nom switch { "ChampX" => premier.X, "ChampLargeur" => premier.Width, _ => premier.FontSize };
            Assert.True(Math.Abs(attendu - lu) < 1e-9, $"{nom} : le premier bloc a {lu}, et non {attendu}");
            Assert.Equal(avantDuSecond, DocumentSerializer.ToJson(Rendu.Feuille(second)));
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Retaper_ce_qui_est_affiche_ou_traverser_les_champs_ne_change_rien()
    {
        // La mémoire de Q14 garde la valeur exacte de ce que le champ affiche : traverser les
        // champs au clavier, ou y retaper le texte même, ne réécrit rien.
        var bloc = Texte();
        bloc.X = 107.15039999999999;
        bloc.Rotation = 12.3456789;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        var x = Trouver<NumericUpDown>(fenetre, "ChampX");
        var affiche = x.Text!;
        Prendre(fenetre, x);
        Rendu.Taper(fenetre, affiche);
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        for (var i = 0; i < 6; i++) Rendu.Touche(fenetre, PhysicalKey.Tab);
        Quitter(fenetre);

        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "traverser les champs a laissé un pas");
        Assert.False(vm.IsDirty, "traverser les champs a marqué le document");
    }

    // ------------------------------------------------------------------ Tab (f11)

    [AvaloniaFact]
    public void Tab_passe_d_un_champ_du_dock_a_l_autre_sans_s_arreter_sur_les_titres()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        Prendre(fenetre, Trouver<NumericUpDown>(fenetre, "ChampX"));
        var arrets = new System.Collections.Generic.List<IInputElement?>();
        for (var i = 0; i < 16; i++)
        {
            Rendu.Touche(fenetre, PhysicalKey.Tab);
            arrets.Add(fenetre.FocusManager!.GetFocusedElement());
        }

        Assert.DoesNotContain(arrets, a => a is ToggleButton bouton && bouton.Classes.Contains("titreSection"));

        // L'ordre de la section Position et taille : X, Y, L, H, puis l'angle.
        string? Nom(IInputElement? arret) => (arret as Visual)?.FindAncestorOfType<NumericUpDown>()?.Name;
        Assert.Equal(new[] { "ChampY", "ChampLargeur", "ChampHauteur", "ChampAngle" }, arrets.Take(4).Select(Nom).ToArray());

        // Et Tab passe, en arrière, d'un champ au précédent.
        Rendu.Touche(fenetre, PhysicalKey.Tab, RawInputModifiers.Shift);
        var retour = fenetre.FocusManager!.GetFocusedElement();
        Assert.Same(arrets[^2], retour);
    }

    // ------------------------------------------------------- les dialogues (f10)

    [AvaloniaFact]
    public void Echap_ferme_le_catalogue_et_le_dialogue_des_coordonnees()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte()));
        var avant = Etat(vm);

        // Le catalogue, ouvert par Ctrl+N : Échap y renonce, et le document reste.
        Rendu.Touche(fenetre, PhysicalKey.N, RawInputModifiers.Control);
        var catalogue = fenetre.OwnedWindows.OfType<TemplateChooserWindow>().SingleOrDefault();
        Assert.NotNull(catalogue);
        Rendu.Touche(catalogue, PhysicalKey.Escape);
        Assert.Empty(fenetre.OwnedWindows.OfType<TemplateChooserWindow>());
        Assert.Equal(avant, Etat(vm));

        // Le dialogue des coordonnées : Échap y renonce aussi, et ne rend rien à enregistrer.
        var coordonnees = new BusinessInfoWindow { DataContext = new BusinessInfoViewModel(new BusinessInfoStore()) };
        var rendu = coordonnees.ShowDialog<BusinessInfoStore?>(fenetre);
        Rendu.Battre();
        Assert.False(rendu.IsCompleted);
        Rendu.Touche(coordonnees, PhysicalKey.Escape);
        Assert.True(rendu.IsCompleted, "Échap n'a pas fermé le dialogue des coordonnées");
        Assert.Null(rendu.Result);
    }
}
