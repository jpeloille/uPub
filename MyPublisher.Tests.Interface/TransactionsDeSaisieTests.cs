using System;
using System.Collections.Generic;
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
/// Qui tient la transaction d'édition, et quand — les postes Q24 et Q25.
///
/// Une transaction s'ouvre pour un geste et se referme avec lui : la saisie sur la page, un
/// champ du dock ou de la barre qui a le focus, un glissé sur la planche, le sélecteur de
/// couleur de la barre. Elle n'empile son pas d'annulation qu'à sa fermeture. Un défaut trouvé
/// par la relecture du cours sous l'angle de la saisie (fiche 23, a14), éprouvé ici sur la
/// vraie fenêtre, au clavier : <c>Annuler</c> et <c>Rétablir</c> referment d'abord tout ce qui
/// est ouvert, puis rendaient la main s'il n'y avait rien à défaire. La saisie, ou le champ,
/// continuait alors hors de toute transaction : sa frappe ne s'annulait plus et ne marquait
/// pas le document — une fermeture l'aurait perdue sans question.
///
/// Q25 : la transaction de la saisie s'ouvrait au double-clic et ne se refermait qu'à la fin
/// de la saisie. Un champ de la barre ou du dock qui prenait le focus pendant la frappe y
/// emboîtait la sienne, et son réglage se fondait dans le pas de la frappe. Elle suit
/// désormais le focus de la zone de saisie.
///
/// Ses gestes sont ceux du banc (<see cref="Rendu"/>, poste N1).
/// </summary>
public class TransactionsDeSaisieTests
{
    // ------------------------------------------------------------------ l'outillage

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static void Quitter(Window fenetre)
    {
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
    }

    // ------------------------------------------------------------------ le banc du poste

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static TextFrame Texte() => new()
    {
        Name = "Bloc", X = 60, Y = 60, Width = 300, Height = 80, Text = "Texte",
        FontFamily = "DejaVu Sans", FontSize = 10.5, Foreground = "#FF000000",
    };

    /// <summary>Le bloc de la page après une annulation, qui relit le document : une autre instance.</summary>
    private static TextFrame Bloc(MainWindowViewModel vm) => Assert.IsType<TextFrame>(vm.CurrentPage!.Frames.Single());

    private static BarreContextuelle Barre(Window fenetre) => Trouver<BarreContextuelle>(fenetre, "BarreContextuelle");

    /// <summary>Le champ de saisie d'un champ numérique : c'est lui qui prend le focus.</summary>
    private static TextBox SaisieDe(NumericUpDown champ) => champ.GetVisualDescendants().OfType<TextBox>().First();

    /// <summary>
    /// Défait les pas un à un : chaque annulation rend l'état relevé juste avant le geste
    /// qu'elle défait, dans l'ordre inverse des gestes, et il n'en reste aucun après le
    /// premier état.
    /// </summary>
    private static void DefaitsUnAUn(MainWindowViewModel vm, IReadOnlyList<(string Quoi, string Etat)> etats)
    {
        for (var i = etats.Count - 1; i > 0; i--)
        {
            Assert.True(vm.CanUndo, $"il manque le pas de « {etats[i].Quoi} »");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.True(etats[i - 1].Etat == Etat(vm),
                $"annuler « {etats[i].Quoi} » ne rend pas l'état d'après « {etats[i - 1].Quoi} »");
        }

        Assert.False(vm.CanUndo, $"{etats.Count - 1} gestes, plus de {etats.Count - 1} pas");
    }

    // ============================================================ Q24 · Annuler, Rétablir

    /// <summary>
    /// La recette : <c>Ctrl+Z</c> sans rien à annuler, pendant la saisie. Il referme la
    /// transaction de la saisie, ne trouve rien à défaire — et la saisie, restée ouverte,
    /// doit rouvrir la sienne : la frappe suivante s'annule, et marque le document.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q24")]
    public void Ctrl_Z_sans_rien_a_annuler_laisse_la_frappe_suivante_dans_un_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);
        var titre = Trouver<TextBlock>(fenetre, "TitreDuDocument");

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Assert.False(vm.CanUndo);
        Rendu.Touche(fenetre, PhysicalKey.Z, RawInputModifiers.Control);
        Assert.True(bloc.IsEditing, "Ctrl+Z sans rien à annuler a refermé la saisie");
        Assert.True(zone.IsFocused, "Ctrl+Z a pris le focus à la saisie");

        fenetre.KeyTextInput("abc");
        Rendu.Battre();
        Assert.Equal("abc", bloc.Text);
        // Pendant la frappe déjà : le document est modifié, et Annuler ne se grise pas.
        Assert.True(vm.PeutAnnuler, "la frappe n'est dans aucune transaction : Annuler est grisé");
        Assert.Equal("banc.mypub — modifié", titre.Text);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(bloc.IsEditing);
        Assert.True(vm.CanUndo, "la frappe n'est dans aucun pas d'annulation");
        Assert.True(vm.IsDirty, "la frappe ne marque pas le document");
        Assert.StartsWith("• ", vm.WindowTitle);
        Assert.Equal("banc.mypub — modifié", titre.Text);

        // Ctrl+Z rend le texte d'avant la frappe, et seulement lui.
        Rendu.Touche(fenetre, PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal("Texte", Bloc(vm).Text);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// La recette : <c>Ctrl+Y</c> après une frappe, le cas le plus fréquent. Il referme la
    /// frappe en un pas — qui vide la pile de rétablissement —, la trouve vide, et rend la
    /// main sans rien rétablir : la saisie reste ouverte, et ce qu'on tape ensuite fait son
    /// propre pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q24")]
    public void Ctrl_Y_apres_une_frappe_garde_la_saisie_et_donne_son_pas_a_la_suite()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        fenetre.KeyTextInput("abc");
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.Y, RawInputModifiers.Control);
        Assert.True(bloc.IsEditing, "Ctrl+Y a refermé la saisie");
        Assert.True(zone.IsFocused, "Ctrl+Y a pris le focus à la saisie");
        Assert.False(vm.CanRedo);
        etats.Add(("abc", Etat(vm)));

        fenetre.KeyTextInput("d");
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal("abcd", bloc.Text);
        etats.Add(("d", Etat(vm)));

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>
    /// Le troisième cas, déduit à la lecture du code : un champ de la barre garde le focus
    /// pendant un <c>Ctrl+Z</c> qui ne trouve rien à annuler. La transaction ouverte à son
    /// focus se referme avec les autres, et le champ la croyait toujours ouverte : sa saisie
    /// ne faisait aucun pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q24")]
    public void Ctrl_Z_sans_rien_a_annuler_dans_un_champ_de_la_barre_laisse_sa_saisie_dans_un_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        var corps = SaisieDe(Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc"));
        corps.Focus();
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.Z, RawInputModifiers.Control);
        Assert.True(corps.IsFocused, "Ctrl+Z a pris le focus au champ");

        corps.SelectAll();
        fenetre.KeyTextInput("14");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(14, bloc.FontSize);
        Quitter(fenetre);

        Assert.True(vm.IsDirty, "le corps saisi ne marque pas le document");
        Rendu.UnSeulPas(vm, avant, "le corps saisi après Ctrl+Z");
    }

    /// <summary>
    /// La recette, dans le champ « Contenu » du dock : une frappe, puis <c>Ctrl+Y</c>, qui
    /// referme la frappe en un pas et ne trouve rien à rétablir. Le champ garde le focus : la
    /// seconde frappe fait le sien.
    ///
    /// Avec <c>Ctrl+Z</c>, qui défait la frappe, le champ « Contenu » perd le focus : relire
    /// le document vide la sélection en passant — changer de page la vide —, et les réglages
    /// du bloc se masquent un instant (Q23). La seconde frappe n'y arrive pas, et rien ne se
    /// perd ; le cas se joue sur un champ que la sélection ne masque pas, au test suivant.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q24")]
    public void Ctrl_Y_dans_le_champ_Contenu_laisse_la_frappe_suivante_dans_son_propre_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var etats = new List<(string, string)> { ("la sélection", Etat(vm)) };

        var contenu = Trouver<TextBox>(fenetre, "SaisieDuTexte");
        contenu.Focus();
        Rendu.Battre();
        contenu.CaretIndex = contenu.Text!.Length;
        fenetre.KeyTextInput("x");
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.Y, RawInputModifiers.Control);
        Assert.True(contenu.IsFocused, "Ctrl+Y a pris le focus au champ");
        Assert.Equal("Textex", bloc.Text);
        etats.Add(("x", Etat(vm)));

        contenu.CaretIndex = contenu.Text!.Length;
        fenetre.KeyTextInput("y");
        Rendu.Battre();
        Quitter(fenetre);
        Assert.Equal("Textexy", bloc.Text);
        etats.Add(("y", Etat(vm)));

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>
    /// <c>Ctrl+Z</c> qui défait une saisie, dans un champ que la sélection ne masque pas : une
    /// marge de l'onglet Document. L'annulation relit le document, le champ garde le focus et
    /// montre la marge rendue ; ce qu'on y saisit ensuite fait son pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q24")]
    public void Annuler_dans_une_marge_de_l_onglet_Document_laisse_la_saisie_suivante_dans_un_pas()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte()));
        Rendu.Onglet(fenetre, "Document");
        var avant = Etat(vm);
        var gauche = vm.Document.MarginLeft;

        var marge = SaisieDe(Trouver<NumericUpDown>(fenetre, "MargeGauche"));
        marge.Focus();
        Rendu.Battre();
        marge.SelectAll();
        fenetre.KeyTextInput("25");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(Units.MmToPoints(25), vm.Document.MarginLeft, precision: 9);

        Rendu.Touche(fenetre, PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal(gauche, vm.Document.MarginLeft, precision: 9);
        Assert.True(marge.IsFocused, "l'annulation a pris le focus au champ");

        marge.SelectAll();
        fenetre.KeyTextInput("30");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Quitter(fenetre);
        Assert.Equal(Units.MmToPoints(30), vm.Document.MarginLeft, precision: 9);

        Rendu.UnSeulPas(vm, avant, "la marge saisie après Ctrl+Z");
    }

    /// <summary>
    /// Le piège du poste : ne jamais rouvrir à l'aveugle. Une annulation qui défait la frappe
    /// relit le document et referme la saisie par le changement de sélection ; une boucle qui
    /// rouvrirait autant de transactions qu'il y en avait en laisserait une sans propriétaire,
    /// où chaque geste suivant fondrait son pas. Un glissé de 1 pt, Alt tenu pour écarter
    /// l'aimantation, empile encore le sien.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q24")]
    public void Apres_une_annulation_qui_referme_la_saisie_un_glisse_fait_son_propre_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        fenetre.KeyTextInput("abc");
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.Z, RawInputModifiers.Control);

        var relu = Bloc(vm);
        Assert.Equal(avant, Etat(vm));
        Assert.False(relu.IsEditing, "l'annulation n'a pas refermé la saisie");
        Assert.Null(Rendu.ZoneDeSaisie(fenetre, relu));
        Assert.Same(relu, vm.SingleSelected);
        Assert.False(vm.CanUndo);
        Assert.True(vm.CanRedo);

        var x = relu.X;
        var prise = Rendu.SurLaPage(fenetre, relu.X + 20, relu.Y + 20);
        Rendu.Glisser(fenetre, prise, prise + new Point(1, 0), RawInputModifiers.Alt);
        Assert.Equal(x + 1, relu.X, precision: 6);

        Rendu.UnSeulPas(vm, avant, "le glissé d'après l'annulation");
    }

    /// <summary>
    /// Un glissé est aussi le propriétaire d'une transaction, de la pression au relâché.
    /// <c>Ctrl+Z</c> pendant qu'on tient le bloc, sans rien à annuler, la refermait sans
    /// qu'il la rouvre : le déplacement qui suivait n'entrait dans aucun pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q24")]
    public void Ctrl_Z_pendant_un_glisse_sans_rien_a_annuler_laisse_le_deplacement_dans_un_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        var prise = Rendu.SurLaPage(fenetre, bloc.X + 20, bloc.Y + 20);
        var arrivee = prise + new Point(30, 0);
        fenetre.MouseDown(prise, MouseButton.Left, RawInputModifiers.Alt);
        Rendu.Battre();
        fenetre.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.LeftMouseButton);
        Rendu.Battre();
        fenetre.MouseMove(arrivee, RawInputModifiers.Alt | RawInputModifiers.LeftMouseButton);
        Rendu.Battre();
        fenetre.MouseUp(arrivee, MouseButton.Left, RawInputModifiers.Alt);
        Rendu.Battre();
        Assert.Equal(90, bloc.X, precision: 6);

        Assert.True(vm.IsDirty, "le déplacement ne marque pas le document");
        Rendu.UnSeulPas(vm, avant, "le glissé repris après Ctrl+Z");
    }

    /// <summary>
    /// Le sélecteur de couleur de la barre tient sa transaction de son ouverture à sa
    /// fermeture (I3). <c>Ctrl+Z</c> pendant qu'il est ouvert, sans rien à annuler, la
    /// refermait sans qu'il la rouvre : la pastille choisie ensuite ne faisait aucun pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q24")]
    public void Ctrl_Z_sous_le_selecteur_de_couleur_ouvert_laisse_la_couleur_dans_un_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        var vignette = Trouver<Button>(Barre(fenetre), "CouleurDuTexte");
        Rendu.Cliquer(vignette);
        var volet = Assert.IsType<Flyout>(vignette.Flyout);
        Assert.True(volet.IsOpen, "la vignette n'ouvre pas le sélecteur");
        Rendu.Touche(fenetre, PhysicalKey.Z, RawInputModifiers.Control);
        Assert.True(volet.IsOpen, "Ctrl+Z a refermé le sélecteur");

        var selecteur = Assert.IsType<SelecteurDeCouleur>(volet.Content);
        Rendu.Cliquer(selecteur.GetVisualDescendants().OfType<Button>().Single(b => b.Tag is EmplacementDeCouleur.Accent2));
        volet.Hide();
        Rendu.Battre();
        Assert.Equal("scheme:accent2", bloc.Foreground);

        Assert.True(vm.IsDirty, "la couleur ne marque pas le document");
        Rendu.UnSeulPas(vm, avant, "la couleur prise après Ctrl+Z");
    }

    /// <summary>
    /// La règle de Q14, vue du côté de la transaction : entrer en saisie et la quitter sans
    /// rien taper n'empile aucun pas et ne marque pas le document. Le bloc est pris avant le
    /// double-clic, si bien que les deux clics se suivent de près : le test ne dépend pas du
    /// délai du double-clic, que le premier clic d'un processus neuf sur ATA 21 peut dépasser
    /// (<c>DoubleClicTests</c>, joué à chaud par les recettes de Q14 et de Q23).
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q24")]
    public void Entrer_en_saisie_et_la_quitter_sans_rien_taper_ne_fait_aucun_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.PeutAnnuler, "entrer en saisie a ouvert une retouche");

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(bloc.IsEditing);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "entrer en saisie et en sortir a empilé un pas");
        Assert.False(vm.IsDirty, "entrer en saisie et en sortir a marqué le document");
    }

    // ===================================================== Q25 · un réglage pendant la frappe

    /// <summary>
    /// La recette : une frappe, le corps réglé à la barre, un clic dans le texte, une autre
    /// frappe. Trois gestes, trois pas, défaits un à un et dans l'ordre. La transaction de la
    /// saisie suit le focus de sa zone : elle se referme quand le champ de la barre le prend,
    /// et se rouvre quand le clic le lui rend — la saisie, elle, reste ouverte.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q25")]
    public void Un_corps_regle_a_la_barre_pendant_la_saisie_fait_son_propre_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        fenetre.KeyTextInput("abc");
        Rendu.Battre();
        etats.Add(("abc", Etat(vm)));

        var corps = SaisieDe(Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc"));
        Rendu.Cliquer(corps);
        Assert.True(corps.IsFocused, "le clic n'a pas donné le focus au corps");
        Assert.True(bloc.IsEditing, "un clic dans la barre a refermé la saisie");
        corps.SelectAll();
        fenetre.KeyTextInput("14");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(14, bloc.FontSize);
        etats.Add(("corps 14", Etat(vm)));

        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, bloc.X + bloc.Width - 20, bloc.Y + 10));
        Assert.True(zone.IsFocused, "le clic dans le texte ne lui a pas rendu le focus");
        zone.CaretIndex = zone.Text!.Length;
        fenetre.KeyTextInput("d");
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal("abcd", bloc.Text);
        etats.Add(("d", Etat(vm)));

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>De même avec un retrait de première ligne saisi au dock.</summary>
    [AvaloniaFact]
    [Trait("poste", "Q25")]
    public void Un_retrait_saisi_au_dock_pendant_la_saisie_fait_son_propre_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        fenetre.KeyTextInput("abc");
        Rendu.Battre();
        etats.Add(("abc", Etat(vm)));

        var retrait = SaisieDe(Trouver<NumericUpDown>(fenetre, "RetraitPremiereLigne"));
        Rendu.Cliquer(retrait);
        Assert.True(retrait.IsFocused, "le clic n'a pas donné le focus au retrait");
        Assert.True(bloc.IsEditing, "un clic dans le dock a refermé la saisie");
        retrait.SelectAll();
        fenetre.KeyTextInput("5");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(Units.MmToPoints(5), bloc.IndentFirstLine, precision: 9);
        etats.Add(("retrait de 5 mm", Etat(vm)));

        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, bloc.X + bloc.Width - 20, bloc.Y + 10));
        Assert.True(zone.IsFocused, "le clic dans le texte ne lui a pas rendu le focus");
        zone.CaretIndex = zone.Text!.Length;
        fenetre.KeyTextInput("d");
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal("abcd", bloc.Text);
        etats.Add(("d", Etat(vm)));

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>
    /// La couleur prise pendant la saisie fait son propre pas. Le sélecteur retire le focus
    /// en s'ouvrant, ce qui clôt la frappe d'abord ; il tient sa transaction ; refermé, il
    /// rend le focus à la zone de la page, dont la transaction se rouvre après la sienne.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q25")]
    public void La_couleur_prise_pendant_la_saisie_fait_son_propre_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        fenetre.KeyTextInput("abc");
        Rendu.Battre();
        etats.Add(("abc", Etat(vm)));

        var vignette = Trouver<Button>(Barre(fenetre), "CouleurDuTexte");
        Rendu.Cliquer(vignette);
        var volet = Assert.IsType<Flyout>(vignette.Flyout);
        Assert.True(volet.IsOpen, "la vignette n'ouvre pas le sélecteur");
        Assert.True(bloc.IsEditing, "ouvrir le sélecteur a refermé la saisie");
        var selecteur = Assert.IsType<SelecteurDeCouleur>(volet.Content);
        Rendu.Cliquer(selecteur.GetVisualDescendants().OfType<Button>().Single(b => b.Tag is EmplacementDeCouleur.Accent2));
        Assert.Equal("scheme:accent2", bloc.Foreground);
        volet.Hide();
        Rendu.Battre();
        etats.Add(("la couleur", Etat(vm)));

        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, bloc.X + bloc.Width - 20, bloc.Y + 10));
        Assert.True(zone.IsFocused, "le clic dans le texte ne lui a pas rendu le focus");
        zone.CaretIndex = zone.Text!.Length;
        fenetre.KeyTextInput("d");
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal("abcd", bloc.Text);
        etats.Add(("d", Etat(vm)));

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>
    /// Les boutons G, I et S de la barre ne prennent pas le focus : la zone de la page le
    /// garde, et sa transaction avec. Ils gardent <c>UnGesteUnPas</c>, qui la referme en un
    /// pas, fait le sien, puis la rouvre — la frappe qui suit a encore le sien.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q25")]
    public void Le_gras_clique_pendant_la_saisie_fait_son_propre_pas_sans_prendre_le_focus()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        fenetre.KeyTextInput("abc");
        Rendu.Battre();
        etats.Add(("abc", Etat(vm)));

        Rendu.Cliquer(Barre(fenetre).GetVisualDescendants().OfType<ToggleButton>()
            .Single(b => Equals(ToolTip.GetTip(b), "Gras (Ctrl+G)") && b.IsEffectivelyVisible));
        Assert.True(bloc.Bold);
        Assert.True(zone.IsFocused, "le bouton G a pris le focus à la saisie");
        etats.Add(("le gras", Etat(vm)));

        fenetre.KeyTextInput("d");
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal("abcd", bloc.Text);
        etats.Add(("d", Etat(vm)));

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>
    /// Q24 et Q25 ensemble : la saisie ouverte, le focus dans un champ de la barre, un
    /// <c>Ctrl+Z</c> sans rien à annuler. Seul le champ rouvre sa transaction : celle de la
    /// saisie suit le focus de sa zone, qui ne l'a pas. Rouverte parce que la saisie l'est
    /// toujours, elle emboîterait celle du champ, et le corps se fondrait dans la frappe qui
    /// reprend.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q25")]
    public void Ctrl_Z_sans_rien_a_annuler_dans_la_barre_pendant_la_saisie_ne_rouvre_que_le_champ()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        var corps = SaisieDe(Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc"));
        Rendu.Cliquer(corps);
        Assert.True(corps.IsFocused, "le clic n'a pas donné le focus au corps");
        Assert.False(vm.CanUndo);
        Rendu.Touche(fenetre, PhysicalKey.Z, RawInputModifiers.Control);
        Assert.True(bloc.IsEditing, "Ctrl+Z sans rien à annuler a refermé la saisie");
        Assert.True(corps.IsFocused, "Ctrl+Z a pris le focus au corps");

        corps.SelectAll();
        fenetre.KeyTextInput("14");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(14, bloc.FontSize);
        etats.Add(("corps 14", Etat(vm)));

        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, bloc.X + bloc.Width - 20, bloc.Y + 10));
        Assert.True(zone.IsFocused, "le clic dans le texte ne lui a pas rendu le focus");
        zone.CaretIndex = zone.Text!.Length;
        fenetre.KeyTextInput("d");
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal("Texted", bloc.Text);
        etats.Add(("d", Etat(vm)));

        DefaitsUnAUn(vm, etats);
    }
}
