using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les raccourcis de Publisher en français — poste N10 (fiche 23, b2, b7, b8, b10, d8, d20 ; fiche
/// 24, l'aide de Microsoft relue mot pour mot).
///
/// Avant le poste, <c>Ctrl+E</c> exportait en PDF là où Publisher centre, même pendant une saisie,
/// et <c>Ctrl+Maj+H</c> dissociait là où Publisher dissocie par <c>Ctrl+Maj+G</c>. Le poste lie
/// les alignements (<c>Ctrl+L</c>, <c>E</c>, <c>R</c>, <c>J</c>), le corps (<c>Ctrl+]</c> et
/// <c>Ctrl+[</c> d'un point, <c>Ctrl+Maj+&gt;</c> et <c>Ctrl+Maj+&lt;</c> dans la liste
/// standard), l'export sur <c>Ctrl+Maj+P</c>, <c>Ctrl+Maj+G</c> qui groupe et dissocie,
/// <c>Alt+Retour arrière</c> et <c>F4</c> qui annulent et rétablissent, <c>Ctrl+Maj+X</c> qui
/// supprime l'objet même en saisie.
///
/// Les touches qui sont des caractères se jouent comme la sonde du clavier les a relevées sur
/// l'AZERTY du propriétaire (<see cref="Rendu.ToucheAzerty"/>) : « &lt; » et « &gt; » sur la même
/// touche, « ] » et « [ » sous AltGr. Tout se joue au clavier sur la vraie fenêtre, aux gestes
/// du banc (poste N1), hors saisie et en saisie. La recette du poste —
/// <c>Scripts/recettes/N10-raccourcis-francais.sh</c> — joue ces tests par leur trait.
/// </summary>
[Trait("poste", "N10")]
public class RaccourcisFrancaisTests
{
    // ------------------------------------------------------------------ outillage

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static TextFrame Texte(string texte = "Texte") => new()
    {
        Name = "Bloc", X = 60, Y = 60, Width = 300, Height = 80, Text = texte,
        FontFamily = "DejaVu Sans", FontSize = 10.5, Foreground = "#FF000000",
    };

    private static ShapeFrame Carre(string nom, double x, double y) =>
        new() { Name = nom, X = x, Y = y, Width = 60, Height = 60, Fill = new SolidFill("#FFCC0000") };

    /// <summary>
    /// Le bloc de ce nom sur la page courante. Annuler relit le document entier
    /// (<c>Restore</c>) : un bloc retenu avant ne l'est plus après.
    /// </summary>
    private static T Le<T>(MainWindowViewModel vm, string nom) where T : Frame =>
        Assert.IsType<T>(vm.CurrentPage!.Frames.Single(bloc => bloc.Name == nom));

    /// <summary>Une entrée de menu, par son libellé : hors de l'arbre visuel tant que son menu est replié.</summary>
    private static MenuItem Menu(Window fenetre, string libelle) =>
        fenetre.GetLogicalDescendants().OfType<MenuItem>().SingleOrDefault(m => Equals(m.Header, libelle))
        ?? throw new InvalidOperationException($"le menu n'a pas d'entrée « {libelle} »");

    /// <summary>Compte les demandes d'export que le modèle de vue lève.</summary>
    private sealed class Exports
    {
        public int Nombre { get; private set; }

        public Exports(MainWindowViewModel vm) => vm.ExportDemande += (_, _) => Nombre++;
    }

    /// <summary>
    /// En saisie, une frappe puis un geste : deux pas, défaits un à un et dans l'ordre — le geste,
    /// puis la frappe. La saisie est refermée d'abord par Échap.
    /// </summary>
    private static void DeuxPas(Window fenetre, MainWindowViewModel vm, TextFrame bloc, string avant, string tape,
                                string quoi)
    {
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(bloc.IsEditing);
        var apres = Etat(vm);

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(tape, Le<TextFrame>(vm, bloc.Name).Text);
        Assert.NotEqual(apres, Etat(vm));
        Assert.True(vm.CanUndo, $"{quoi} : la frappe et le geste n'ont fait qu'un pas");

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.False(vm.CanUndo, $"{quoi} : plus de deux pas");
        Assert.Equal(avant, Etat(vm));
    }

    // ------------------------------------------------------------- Ctrl+E et l'export

    /// <summary>
    /// La recette : <c>Ctrl+E</c> centre le texte en un pas et n'exporte plus — ni sur un bloc de
    /// texte, ni sur une forme, ni sans sélection, où la touche, qu'aucune liaison ne prend, va
    /// jusqu'à <c>OnKeyDown</c>, qui l'exportait.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_E_centre_le_texte_en_un_pas_et_n_exporte_plus()
    {
        var bloc = Texte();
        var forme = Carre("Forme", 60, 300);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, forme));
        var exports = new Exports(vm);

        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.E, RawInputModifiers.Control);
        Assert.Equal(TextAlign.Center, bloc.Align);
        Assert.Equal(0, exports.Nombre);
        Rendu.UnSeulPas(vm, avant, "Ctrl+E");

        foreach (var (selection, quoi) in new (Frame?, string)[] { (forme, "sur une forme"), (null, "sans sélection") })
        {
            vm.SelectOnly(selection);
            Rendu.Battre();
            avant = Etat(vm);
            Rendu.ToucheAzerty(fenetre, PhysicalKey.E, RawInputModifiers.Control);
            Assert.Equal(0, exports.Nombre);
            Assert.Equal(avant, Etat(vm));
            Assert.False(vm.CanUndo, $"Ctrl+E {quoi} a fait un pas");
        }
    }

    /// <summary>
    /// <c>Ctrl+Maj+P</c> exporte : la commande du modèle de vue lève la demande, que la fenêtre
    /// suit — hors saisie, et en saisie, sans la refermer ni rien changer au document. Le menu
    /// Fichier et le bouton de la barre principale portent la même commande, et le menu montre
    /// la touche.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_Maj_P_exporte_meme_en_saisie()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var exports = new Exports(vm);
        var avant = Etat(vm);

        Rendu.ToucheAzerty(fenetre, PhysicalKey.P, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(1, exports.Nombre);

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.P, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(2, exports.Nombre);
        Assert.True(bloc.IsEditing, "Ctrl+Maj+P a refermé la saisie");
        Assert.True(zone.IsFocused, "Ctrl+Maj+P a pris le focus à la zone de saisie");
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "l'export a fait un pas");

        var entree = Menu(fenetre, "Exporter en _PDF…");
        Assert.Equal(KeyGesture.Parse("Ctrl+Shift+P"), entree.InputGesture);
        Assert.Same(vm.ExporterEnPdfCommand, entree.Command);
        var bouton = fenetre.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "BoutonExporter");
        Assert.Same(vm.ExporterEnPdfCommand, bouton.Command);
        Assert.Equal("Exporter en PDF (Ctrl+Maj+P)", ToolTip.GetTip(bouton));
    }

    // ---------------------------------------------------------------- les alignements

    /// <summary>
    /// <c>Ctrl+L</c>, <c>Ctrl+E</c>, <c>Ctrl+R</c> et <c>Ctrl+J</c> alignent le texte du bloc, en un
    /// pas ; en saisie, la frappe d'avant garde le sien, et la saisie continue. Le menu Texte
    /// montre les quatre touches.
    /// </summary>
    [AvaloniaFact]
    public void Les_alignements_au_clavier_hors_saisie_et_en_saisie()
    {
        var touches = new (PhysicalKey Touche, TextAlign Alignement, string Libelle)[]
        {
            (PhysicalKey.L, TextAlign.Left, "_Aligner le texte à gauche"),
            (PhysicalKey.E, TextAlign.Center, "_Centrer le texte"),
            (PhysicalKey.R, TextAlign.Right, "Aligner le texte à _droite"),
            (PhysicalKey.J, TextAlign.Justify, "_Justifier"),
        };

        foreach (var (touche, alignement, libelle) in touches)
        {
            var bloc = Texte();
            // Un alignement de départ qui n'est pas celui de la touche.
            bloc.Align = alignement == TextAlign.Right ? TextAlign.Left : TextAlign.Right;
            var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
            vm.SelectOnly(bloc);
            Rendu.Battre();
            var avant = Etat(vm);

            Rendu.ToucheAzerty(fenetre, touche, RawInputModifiers.Control);
            Assert.Equal(alignement, bloc.Align);
            Rendu.UnSeulPas(vm, avant, $"Ctrl+{touche}");

            bloc = Le<TextFrame>(vm, "Bloc");
            var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
            Rendu.Taper(fenetre, "abc");
            Rendu.ToucheAzerty(fenetre, touche, RawInputModifiers.Control);
            Assert.Equal(alignement, bloc.Align);
            Assert.True(bloc.IsEditing, $"Ctrl+{touche} a refermé la saisie");
            Assert.True(zone.IsFocused, $"Ctrl+{touche} a pris le focus à la zone de saisie");
            Assert.Equal("abc", bloc.Text);
            DeuxPas(fenetre, vm, bloc, avant, "abc", $"Ctrl+{touche} en saisie");

            Assert.Equal(KeyGesture.Parse($"Ctrl+{touche}"), Menu(fenetre, libelle).InputGesture);
        }
    }

    // ------------------------------------------------------------------------ le corps

    /// <summary>
    /// La table des raccourcis par caractère, aux frappes que la sonde a relevées sur l'AZERTY du
    /// propriétaire : AltGr + « ) » donne « ] », le corps d'un point de plus ; AltGr + « ( »
    /// donne « [ », d'un point de moins ; « &lt; » et Maj + « &lt; », sur la touche que n'a pas un
    /// QWERTY, la taille précédente et la suivante de la liste standard. Chacune sous <c>Ctrl</c>,
    /// en un pas, hors saisie puis en saisie ; et la même table répond à un QWERTY, où « ] » a sa
    /// touche et « &gt; » demande Maj sur la touche du point.
    /// </summary>
    [AvaloniaFact]
    public void Le_corps_se_lit_sur_le_caractere_de_l_AZERTY_comme_du_QWERTY()
    {
        var frappes = new (string Quoi, Action<Window> Frapper, double Corps)[]
        {
            ("Ctrl+AltGr+) (« ] »)", f => Rendu.ToucheAzerty(f, PhysicalKey.Minus, RawInputModifiers.Control, altGr: true), 11.5),
            ("Ctrl+AltGr+( (« [ »)", f => Rendu.ToucheAzerty(f, PhysicalKey.Digit5, RawInputModifiers.Control, altGr: true), 9.5),
            ("Ctrl+Maj+« < » (« > »)", f => Rendu.ToucheAzerty(f, PhysicalKey.IntlBackslash,
                RawInputModifiers.Control | RawInputModifiers.Shift), 11),
            ("Ctrl+« < »", f => Rendu.ToucheAzerty(f, PhysicalKey.IntlBackslash, RawInputModifiers.Control), 10),
            ("Ctrl+] en QWERTY", f => Rendu.Touche(f, PhysicalKey.BracketRight, RawInputModifiers.Control), 11.5),
            ("Ctrl+[ en QWERTY", f => Rendu.Touche(f, PhysicalKey.BracketLeft, RawInputModifiers.Control), 9.5),
        };

        foreach (var (quoi, frapper, corps) in frappes)
        {
            var bloc = Texte();
            var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
            vm.SelectOnly(bloc);
            Rendu.Battre();
            var avant = Etat(vm);

            frapper(fenetre);
            Assert.Equal(corps, bloc.FontSize);
            Rendu.UnSeulPas(vm, avant, quoi);

            bloc = Le<TextFrame>(vm, "Bloc");
            var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
            Rendu.Taper(fenetre, "abc");
            frapper(fenetre);
            Assert.Equal(corps, bloc.FontSize);
            Assert.True(bloc.IsEditing, $"{quoi} a refermé la saisie");
            Assert.True(zone.IsFocused, $"{quoi} a pris le focus à la zone de saisie");
            Assert.Equal("abc", bloc.Text);
            DeuxPas(fenetre, vm, bloc, avant, "abc", $"{quoi} en saisie");
        }
    }

    /// <summary>
    /// La liste standard, au clavier : de 10,5 pt, la taille suivante est 11, puis 12, 14 ; de 72,
    /// 80 ; de 8, la précédente est 7, au point entier. Un tableau change de corps comme un bloc de
    /// texte ; une forme n'en a pas, et la touche n'y fait rien.
    /// </summary>
    [AvaloniaFact]
    public void Les_tailles_suivantes_suivent_la_liste_standard_d_Office()
    {
        var bloc = Texte();
        var tableau = new TableFrame { Name = "Tableau", X = 60, Y = 300, FontFamily = "DejaVu Sans", FontSize = 72 };
        var forme = Carre("Forme", 400, 300);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, tableau, forme));

        void Suivante(Window f) => Rendu.ToucheAzerty(f, PhysicalKey.IntlBackslash, RawInputModifiers.Control | RawInputModifiers.Shift);
        void Precedente(Window f) => Rendu.ToucheAzerty(f, PhysicalKey.IntlBackslash, RawInputModifiers.Control);

        vm.SelectOnly(bloc);
        Rendu.Battre();
        foreach (var attendu in new[] { 11.0, 12, 14 })
        {
            Suivante(fenetre);
            Assert.Equal(attendu, bloc.FontSize);
        }

        bloc.FontSize = 8;
        Precedente(fenetre);
        Assert.Equal(7, bloc.FontSize);

        vm.SelectOnly(tableau);
        Rendu.Battre();
        Suivante(fenetre);
        Assert.Equal(80, tableau.FontSize);
        Precedente(fenetre);
        Precedente(fenetre);
        Assert.Equal(48, tableau.FontSize);

        vm.SelectOnly(forme);
        Rendu.Battre();
        var avant = Etat(vm);
        Suivante(fenetre);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.Minus, RawInputModifiers.Control, altGr: true);
        Assert.Equal(avant, Etat(vm));
    }

    // ------------------------------------------------------------- grouper, supprimer

    /// <summary>
    /// <c>Ctrl+Maj+G</c> groupe deux blocs, puis la même touche dissocie le groupe, chaque fois en un
    /// pas ; <c>Ctrl+Maj+H</c>, libéré, ne fait plus rien. Le menu Disposition montre
    /// <c>Ctrl+Maj+G</c> à Grouper comme à Dissocier, et l'infobulle de la barre le dit.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_Maj_G_groupe_puis_dissocie_et_Ctrl_Maj_H_est_libre()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 20, 20), Carre("B", 200, 20)));
        var page = vm.CurrentPage!;
        vm.SetSelection([page.Frames[0], page.Frames[1]]);
        Rendu.Battre();
        var avant = Etat(vm);

        Rendu.ToucheAzerty(fenetre, PhysicalKey.G, RawInputModifiers.Control | RawInputModifiers.Shift);
        var groupe = Assert.IsType<GroupFrame>(Assert.Single(page.Frames));
        Assert.Same(groupe, vm.Selected);
        var groupee = Etat(vm);

        Rendu.ToucheAzerty(fenetre, PhysicalKey.H, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(groupee, Etat(vm));

        Rendu.ToucheAzerty(fenetre, PhysicalKey.G, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(new[] { "A", "B" }, vm.CurrentPage!.Frames.Select(bloc => bloc.Name));

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(groupee, Etat(vm));
        Rendu.UnSeulPas(vm, avant, "Ctrl+Maj+G, deux fois");

        Assert.Equal(KeyGesture.Parse("Ctrl+Shift+G"), Menu(fenetre, "_Grouper").InputGesture);
        Assert.Equal(KeyGesture.Parse("Ctrl+Shift+G"), Menu(fenetre, "_Dissocier").InputGesture);
    }

    /// <summary>
    /// <c>Ctrl+Maj+X</c> supprime l'objet sélectionné, hors saisie et en saisie : là, le bloc dont
    /// le texte a le curseur s'en va, et la saisie avec lui ; la frappe d'avant garde son pas.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_Maj_X_supprime_le_bloc_meme_en_saisie()
    {
        var bloc = Texte();
        var forme = Carre("Forme", 60, 300);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, forme));
        var page = vm.CurrentPage!;

        vm.SelectOnly(forme);
        Rendu.Battre();
        var avant = Etat(vm);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.X, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.DoesNotContain(forme, page.Frames);
        Rendu.UnSeulPas(vm, avant, "Ctrl+Maj+X");

        bloc = Le<TextFrame>(vm, "Bloc");
        Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.Taper(fenetre, "abc");
        Rendu.ToucheAzerty(fenetre, PhysicalKey.X, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(new[] { "Forme" }, vm.CurrentPage!.Frames.Select(b => b.Name));
        Assert.False(bloc.IsEditing, "la saisie du bloc supprimé reste ouverte");

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal("abc", Le<TextFrame>(vm, "Bloc").Text);
        Assert.True(vm.CanUndo, "la frappe et la suppression n'ont fait qu'un pas");
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.False(vm.CanUndo, "plus de deux pas");
        Assert.Equal(avant, Etat(vm));
    }

    // -------------------------------------------------------------- annuler, rétablir

    /// <summary>
    /// <c>Alt+Retour arrière</c> annule et <c>F4</c> rétablit, hors saisie et en saisie. Sans rien à
    /// annuler, <c>Alt+Retour arrière</c> ne va pas plus loin : <c>OnKeyDown</c>, qui lit Retour
    /// arrière sans regarder <c>Alt</c>, aurait supprimé le bloc sélectionné.
    /// </summary>
    [AvaloniaFact]
    public void Alt_Retour_arriere_annule_et_F4_retablit()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        Rendu.Touche(fenetre, PhysicalKey.Backspace, RawInputModifiers.Alt);
        Assert.Contains(bloc, vm.CurrentPage!.Frames);
        Assert.Same(bloc, vm.SingleSelected);

        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        Assert.Equal(63, bloc.X);
        Rendu.Touche(fenetre, PhysicalKey.Backspace, RawInputModifiers.Alt);
        Assert.Equal(60, Le<TextFrame>(vm, "Bloc").X);
        Rendu.Touche(fenetre, PhysicalKey.F4);
        Assert.Equal(63, Le<TextFrame>(vm, "Bloc").X);

        // En saisie, l'annulation garde la saisie ouverte, et le texte du même bloc (Q24).
        bloc = Le<TextFrame>(vm, "Bloc");
        Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.Taper(fenetre, "abc");
        Rendu.Touche(fenetre, PhysicalKey.Backspace, RawInputModifiers.Alt);
        Assert.Equal("Texte", Le<TextFrame>(vm, "Bloc").Text);
        Rendu.Touche(fenetre, PhysicalKey.F4);
        Assert.Equal("abc", Le<TextFrame>(vm, "Bloc").Text);
    }

    // ------------------------------------------------------------- les autres triplets

    /// <summary>
    /// Ce que la sonde a relevé des touches voisines, rejoué : la touche « à/0 » livre <c>D0</c>
    /// sans <c>Maj</c>, si bien que <c>Ctrl</c> + « à » est la taille réelle — et non
    /// <c>Ctrl+Maj</c>+« à », comme le plan le croyait ; <c>Ctrl</c> + « = » zoome en avant et
    /// <c>Ctrl</c> + « ) » en arrière — la sonde les a vus pris —, <c>Ctrl+Maj</c> + « = » ne fait
    /// rien — la sonde l'a vu passer.
    /// </summary>
    [AvaloniaFact]
    public void Les_touches_voisines_de_la_sonde_et_Ctrl_0()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte()));

        vm.SetZoom(2);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.Digit0, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(2, vm.Zoom);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.Digit0, RawInputModifiers.Control);
        Assert.Equal(1, vm.Zoom);

        Rendu.ToucheAzerty(fenetre, PhysicalKey.Equal, RawInputModifiers.Control);
        Assert.Equal(1.25, vm.Zoom, 6);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.Equal, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(1.25, vm.Zoom, 6);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.Minus, RawInputModifiers.Control);
        Assert.Equal(1, vm.Zoom, 6);
    }

    // ------------------------------------- les secondes touches du presse-papiers (28 octobre)

    private static readonly DataFormat<string> FormatDesBlocs =
        DataFormat.CreateStringApplicationFormat(MainWindowViewModel.FormatDesBlocs);

    private static IClipboard PressePapiers(Window fenetre) =>
        fenetre.Clipboard ?? throw new InvalidOperationException("aucun presse-papiers");

    private static string? BlocsCopies(Window fenetre) =>
        Rendu.Attendre(PressePapiers(fenetre).TryGetValueAsync(FormatDesBlocs));

    /// <summary>
    /// Hors saisie, <c>Ctrl+Inser</c> copie les blocs, <c>Maj+Suppr</c> les coupe — en un pas, au
    /// presse-papiers, et non plus supprimés —, <c>Maj+Inser</c> les colle à leur place : ce que
    /// font <c>Ctrl+C</c>, <c>Ctrl+X</c> et <c>Ctrl+V</c> (N8). Les secondes touches de l'aide en
    /// français, que le propriétaire a demandées le 28 octobre 2026.
    /// </summary>
    [AvaloniaFact]
    public void Hors_saisie_Ctrl_Inser_copie_Maj_Suppr_coupe_et_Maj_Inser_colle_les_blocs()
    {
        var bloc = Texte("Le titre");
        var forme = Carre("Forme", 60, 300);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, forme));
        try
        {
            Rendu.Attendre(PressePapiers(fenetre).SetTextAsync("rien encore"));
            vm.SelectOnly(bloc);
            Rendu.Battre();
            var avant = Etat(vm);

            Rendu.Touche(fenetre, PhysicalKey.Insert, RawInputModifiers.Control);
            Assert.NotNull(BlocsCopies(fenetre));
            Assert.Equal("Le titre", Rendu.TexteCopie(fenetre));
            Assert.Equal(avant, Etat(vm));
            Assert.False(vm.CanUndo, "Ctrl+Inser a fait un pas");

            Rendu.Attendre(PressePapiers(fenetre).SetTextAsync("rien encore"));
            Rendu.Touche(fenetre, PhysicalKey.Delete, RawInputModifiers.Shift);
            Assert.Equal(new[] { "Forme" }, vm.CurrentPage!.Frames.Select(b => b.Name));
            Assert.NotNull(BlocsCopies(fenetre));
            Assert.Equal("Le titre", Rendu.TexteCopie(fenetre));
            var coupe = Etat(vm);
            Rendu.UnSeulPas(vm, avant, "Maj+Suppr");
            vm.RedoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal(coupe, Etat(vm));

            // L'original n'est plus là : le collage le rend à sa place, au-dessus de la forme, en un pas.
            Rendu.Touche(fenetre, PhysicalKey.Insert, RawInputModifiers.Shift);
            var colle = Le<TextFrame>(vm, "Bloc");
            Assert.Equal(("Le titre", 60.0, 60.0), (colle.Text, colle.X, colle.Y));
            Assert.Same(colle, vm.SingleSelected);
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal(coupe, Etat(vm));
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// En saisie, les trois touches agissent sur le texte, comme <c>Ctrl+C</c>, <c>V</c> et <c>X</c> :
    /// la zone de saisie les tient d'Avalonia, la fenêtre ne les voit pas. Le texte va au
    /// presse-papiers sans format de bloc, la saisie reste ouverte, et aucun bloc n'est copié,
    /// coupé ni collé.
    /// </summary>
    [AvaloniaFact]
    public void En_saisie_les_secondes_touches_du_presse_papiers_agissent_sur_le_texte()
    {
        var bloc = Texte("Bonjour le monde");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            // Des blocs au presse-papiers d'abord : la copie du texte doit les remplacer.
            vm.SelectOnly(bloc);
            Rendu.Battre();
            Rendu.Touche(fenetre, PhysicalKey.Insert, RawInputModifiers.Control);
            Assert.NotNull(BlocsCopies(fenetre));

            var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
            Rendu.SelectionnerDuTexte(zone, 8, 10);
            Rendu.Touche(fenetre, PhysicalKey.Insert, RawInputModifiers.Control);
            Assert.Equal("le", Rendu.TexteCopie(fenetre));
            Assert.Null(BlocsCopies(fenetre));

            Rendu.SelectionnerDuTexte(zone, 0, 7);
            Rendu.Touche(fenetre, PhysicalKey.Insert, RawInputModifiers.Shift);
            Assert.Equal("le le monde", zone.Text);

            Rendu.SelectionnerDuTexte(zone, 3, 5);
            Rendu.Touche(fenetre, PhysicalKey.Delete, RawInputModifiers.Shift);
            Assert.Equal("le", Rendu.TexteCopie(fenetre));
            Assert.Equal("le  monde", zone.Text);

            Assert.Single(vm.CurrentPage!.Frames);
            Assert.True(bloc.IsEditing, "une seconde touche du presse-papiers a refermé la saisie");
            Assert.Null(BlocsCopies(fenetre));
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// Dans un champ du dock, la touche reste au champ, comme le veut la garde de N6 : le Nom se
    /// copie, se coupe et se colle, et aucun bloc ne bouge.
    /// </summary>
    [AvaloniaFact]
    public void Dans_un_champ_du_dock_les_secondes_touches_restent_au_champ()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            vm.SelectOnly(bloc);
            Rendu.Battre();
            var nom = fenetre.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "ChampNom");
            nom.Focus();
            nom.SelectAll();
            Rendu.Battre();

            Rendu.Touche(fenetre, PhysicalKey.Insert, RawInputModifiers.Control);
            Assert.Equal("Bloc", Rendu.TexteCopie(fenetre));
            Assert.Null(BlocsCopies(fenetre));

            Rendu.Attendre(PressePapiers(fenetre).SetTextAsync("Collé"));
            nom.SelectAll();
            Rendu.Touche(fenetre, PhysicalKey.Insert, RawInputModifiers.Shift);
            Assert.Equal("Collé", nom.Text);

            nom.SelectAll();
            Rendu.Touche(fenetre, PhysicalKey.Delete, RawInputModifiers.Shift);
            Assert.Equal("", nom.Text ?? "");
            Assert.Equal("Collé", Rendu.TexteCopie(fenetre));
            Assert.True(nom.IsFocused, "une seconde touche du presse-papiers a pris le focus au champ");
            Assert.Single(vm.CurrentPage!.Frames);
            Assert.Null(BlocsCopies(fenetre));
        }
        finally
        {
            fenetre.Close();
        }
    }
}
