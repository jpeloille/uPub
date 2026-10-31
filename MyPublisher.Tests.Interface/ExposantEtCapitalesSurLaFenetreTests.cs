using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;
using Xunit.Abstractions;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Exposant, indice et capitales — le poste N24 (fiche 23, b14), sur la vraie fenêtre. Sur la
/// sélection du texte, le mot du curseur, ou le bloc entier hors saisie (R7) : <c>Ctrl+Maj+=</c>
/// l'exposant, <c>Ctrl+=</c> l'indice — le décalage et le corps de ceux d'Office, que l'import lit —,
/// <c>Ctrl+Maj+K</c> les petites majuscules, et le menu Texte comme le menu du texte pour les quatre.
/// La composition dessine les capitales sans toucher au texte, à l'écran comme au PDF.
/// </summary>
[Trait("poste", "N24")]
public class ExposantEtCapitalesSurLaFenetreTests
{
    private readonly ITestOutputHelper _sortie;

    public ExposantEtCapitalesSurLaFenetreTests(ITestOutputHelper sortie) => _sortie = sortie;

    // ------------------------------------------------------------------ le banc du poste

    /// <summary>La précision que le plan demande au PDF et à l'écran, en points.</summary>
    private const double Precision = 0.05;

    private const double Corps = 24;

    private static TextFrame Bloc(string texte) => new()
    {
        Name = "Formule", X = 100, Y = 100, Width = 360, Height = 120, Text = texte,
        FontFamily = "DejaVu Sans", FontSize = Corps, Foreground = "#FF000000", Align = TextAlign.Left,
    };

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    /// <summary>Le style que la composition donne à chaque caractère du bloc : corps et décalage.</summary>
    private static (double Corps, double Decalage)[] Styles(TextFrame bloc) =>
        StyleEffectif.Decouper(bloc.Text ?? "", bloc.Spans, MiseEnForme.Commun(bloc))
            .SelectMany(m => Enumerable.Repeat((Math.Round(m.Style.FontSize, 4), m.Style.BaselineShift), m.Texte.Length))
            .ToArray();

    private static TexteCompose Compose(Window fenetre, TextFrame bloc) =>
        fenetre.GetVisualDescendants().OfType<TexteCompose>().First(c => ReferenceEquals(c.Bloc, bloc) && c.IsEffectivelyVisible);

    /// <summary>
    /// Les morceaux que l'écran pose : leur texte, leur corps, et leur ligne de base sur la page —
    /// celle de la ligne, décalée du morceau —, lus sur la géométrie de la composition.
    /// </summary>
    private static List<(string Texte, double Corps, double LigneDeBase)> MorceauxALEcran(Window fenetre, TextFrame bloc) =>
        (Compose(fenetre, bloc).Geometrie() ?? throw new InvalidOperationException("pas de géométrie")).Lignes
            .SelectMany(posee => posee.Ligne.Morceaux.Select(m =>
                (m.Texte, m.Style.FontSize, bloc.Y + posee.LigneDeBase - m.Style.BaselineShift * m.Style.FontSize)))
            .ToList();

    /// <summary>
    /// Ce que le PDF dessine sur sa première page : chaque chaîne, son corps (<c>Tf</c>) et le point où
    /// elle se pose (<c>Td</c>, cumulé depuis <c>BT</c>), l'ordonnée comptée du haut de la page, comme
    /// à l'écran. Le flux est celui de PDFsharp, décompressé, lu opérateur par opérateur ; les fontes
    /// sont en WinAnsi, une chaîne est le texte même.
    /// </summary>
    private List<(string Texte, double Corps, double X, double Y)> DessinsDuPdf(MainWindowViewModel vm)
    {
        var chemin = Path.Combine(Path.GetTempPath(), $"mypublisher-n24-{Guid.NewGuid():N}.pdf");
        try
        {
            vm.ExportPdf(chemin);
            using var pdf = PdfReader.Open(chemin, PdfDocumentOpenMode.Modify);
            var page = pdf.Pages[0];
            var hauteur = page.Height.Point;
            var flux = string.Concat(Enumerable.Range(0, page.Contents.Elements.Count)
                .SelectMany(i => page.Contents.Elements.GetDictionary(i)!.Stream.UnfilteredValue)
                .Select(o => (char)o));

            var dessins = new List<(string, double, double, double)>();
            double corps = 0, x = 0, y = 0;
            foreach (Match m in Regex.Matches(flux,
                         @"\bBT\b|/[^\s/\[\]()<>]+\s+(?<tf>[-\d.]+)\s+Tf|(?<tx>[-\d.]+)\s+(?<ty>[-\d.]+)\s+Td|\((?<s>(?:\\.|[^\\)])*)\)\s*Tj"))
            {
                if (m.Value == "BT") (x, y) = (0, 0);
                else if (m.Groups["tf"].Success) corps = Nombre(m.Groups["tf"].Value);
                else if (m.Groups["tx"].Success) (x, y) = (x + Nombre(m.Groups["tx"].Value), y + Nombre(m.Groups["ty"].Value));
                else dessins.Add((Regex.Replace(m.Groups["s"].Value, @"\\(.)", "$1"), corps, x, hauteur - y));
            }

            foreach (var (t, c, dx, dy) in dessins) _sortie.WriteLine($"PDF : « {t} » {c} pt à ({dx:F4} ; {dy:F4})");
            return dessins;
        }
        finally
        {
            if (File.Exists(chemin)) File.Delete(chemin);
        }
    }

    private static double Nombre(string texte) => double.Parse(texte, CultureInfo.InvariantCulture);

    /// <summary>
    /// La hauteur d'encre d'une capitale de DejaVu Sans à un corps : le contour de son glyphe, tel
    /// qu'Avalonia le trace (<c>FormattedText.BuildGeometry</c>). « H » et « E » n'ont ni jambage ni
    /// débord : leur encre est la hauteur des capitales.
    /// </summary>
    private static double HauteurDesCapitales(char capitale, double corps)
    {
        var texte = new FormattedText(capitale.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("DejaVu Sans"), corps, Brushes.Black);
        return (texte.BuildGeometry(new Point(0, 0)) ?? throw new InvalidOperationException("pas de contour")).Bounds.Height;
    }

    private static MenuItem Menu(Window fenetre, string libelle) =>
        fenetre.GetLogicalDescendants().OfType<MenuItem>().SingleOrDefault(m => Equals(m.Header, libelle))
        ?? throw new InvalidOperationException($"le menu n'a pas d'entrée « {libelle} »");

    private static MenuItem Entree(ItemsControl menu, string libelle) =>
        menu.Items.OfType<MenuItem>().SingleOrDefault(e => Equals(e.Header, libelle))
        ?? throw new InvalidOperationException($"le menu n'a pas d'entrée « {libelle} »");

    private static string? GesteAffiche(MenuItem entree) =>
        entree.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_InputGestureText").Text;

    private static void Exposant(Window fenetre) =>
        Rendu.ToucheAzerty(fenetre, PhysicalKey.Equal, RawInputModifiers.Control | RawInputModifiers.Shift);

    private static void Indice(Window fenetre) => Rendu.ToucheAzerty(fenetre, PhysicalKey.Equal, RawInputModifiers.Control);

    // ------------------------------------------------------------------ la recette du plan

    /// <summary>
    /// La recette : « x2 », le « 2 » sélectionné au pointeur, <c>Ctrl+Maj+=</c>. Un seul fragment,
    /// le décalage d'Office — la moitié du corps — et son corps — 67 % ; la saisie ouverte, la zone au
    /// focus ; au PDF, le « 2 » au corps de 16,08 pt, sa ligne de base 8,04 pt au-dessus de celle du
    /// « x », et l'un et l'autre où l'écran les pose, à 0,05 pt ; un pas d'annulation.
    /// </summary>
    [AvaloniaFact]
    public void Le_2_de_x2_en_exposant_au_pdf_a_la_ligne_de_base_et_au_corps_attendus_en_un_pas()
    {
        var bloc = Bloc("x2");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.SelectionnerDuTexte(zone, 1, 2);
        Assert.Equal(false, vm.MiseEnFormeVisee.Exposant);

        Exposant(fenetre);

        var fragment = Assert.Single(bloc.Spans);
        Assert.Equal((1, 1, 0.5), (fragment.Start, fragment.Length, fragment.BaselineShift));
        Assert.Equal(Corps * 0.67, fragment.FontSize!.Value, 9);
        Assert.Equal("x2", bloc.Text);
        Assert.True(bloc.IsEditing, "Ctrl+Maj+= a refermé la saisie");
        Assert.True(zone.IsFocused, "Ctrl+Maj+= a pris le focus à la zone de saisie");
        Assert.Equal(new SelectionDuTexte(bloc, null, 1, 2, 2), vm.SelectionDuTexte);
        Assert.Equal(true, vm.MiseEnFormeVisee.Exposant);
        Assert.Equal("Exposant", vm.Status);
        Rendu.UneImage();

        // L'écran : la ligne de base de chaque morceau, sur la page.
        var ecran = MorceauxALEcran(fenetre, bloc);
        Assert.Equal(new[] { "x", "2" }, ecran.Select(m => m.Texte));
        Assert.Equal(16.08, ecran[1].Corps, 9);
        Assert.Equal(8.04, ecran[0].LigneDeBase - ecran[1].LigneDeBase, 9);

        // Le PDF : le « 2 » au corps et à la ligne de base attendus, où l'écran le pose.
        var pdf = DessinsDuPdf(vm).Where(d => d.Texte is "x" or "2").ToList();
        Assert.Equal(new[] { ("x", 24.0), ("2", 16.08) }, pdf.Select(d => (d.Texte, d.Corps)));
        _sortie.WriteLine($"écran : « x » sur {ecran[0].LigneDeBase:F4}, « 2 » sur {ecran[1].LigneDeBase:F4}");
        Assert.Equal(8.04, pdf[0].Y - pdf[1].Y, 2);
        Assert.InRange(pdf[0].Y, ecran[0].LigneDeBase - Precision, ecran[0].LigneDeBase + Precision);
        Assert.InRange(pdf[1].Y, ecran[1].LigneDeBase - Precision, ecran[1].LigneDeBase + Precision);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UnSeulPas(vm, avant, "Ctrl+Maj+= sur le 2");
    }

    /// <summary>
    /// <c>Ctrl+=</c> met en indice, et la même touche l'en retire : le bloc retrouve son corps et
    /// aucun fragment ne reste ; un pas chacune. Un indice passé en exposant garde son corps.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_egal_met_en_indice_et_l_en_retire_un_pas_chacun()
    {
        var bloc = Bloc("H2O");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.SelectionnerDuTexte(zone, 1, 2);
        var avant = Etat(vm);

        Indice(fenetre);
        Assert.Equal(new[] { (24.0, 0.0), (16.08, -0.5), (24.0, 0.0) }, Styles(bloc));
        Assert.Equal(true, vm.MiseEnFormeVisee.Indice);
        Assert.Equal("Indice", vm.Status);
        var enIndice = Etat(vm);

        // L'indice passe en exposant sans perdre de corps une seconde fois.
        Exposant(fenetre);
        Assert.Equal(new[] { (24.0, 0.0), (16.08, 0.5), (24.0, 0.0) }, Styles(bloc));
        Indice(fenetre);
        Assert.Equal(enIndice, Etat(vm));

        Indice(fenetre);
        Assert.Empty(bloc.Spans);
        Assert.Equal(Corps, bloc.FontSize);
        Assert.Equal(false, vm.MiseEnFormeVisee.Indice);
        Assert.Equal("Indice retiré", vm.Status);
        Assert.Equal(avant, Etat(vm));
        Assert.True(bloc.IsEditing);

        // Quatre gestes, quatre pas.
        for (var pas = 0; pas < 4; pas++)
        {
            Assert.True(vm.CanUndo, $"il manque le pas {4 - pas}");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
        }

        Assert.False(vm.CanUndo, "plus de quatre pas pour quatre gestes");
        Assert.Equal(avant, Etat(vm));
    }

    /// <summary>
    /// Une sélection en partie en exposant passe toute en exposant, et ce qui l'était garde son
    /// corps : 67 % une fois, jamais deux ; l'état mixte se lit nul, comme le bouton à moitié allumé
    /// de R7.
    /// </summary>
    [AvaloniaFact]
    public void Une_selection_mixte_passe_toute_en_exposant_sans_reduire_deux_fois()
    {
        var bloc = Bloc("abcd");
        bloc.Spans.Add(new StyleSpan { Start = 1, Length = 1, FontSize = Corps * 0.67, BaselineShift = 0.5 });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.SelectionnerDuTexte(zone, 0, 3);
        Assert.Null(vm.MiseEnFormeVisee.Exposant);
        var avant = Etat(vm);

        Exposant(fenetre);

        Assert.Equal(new[] { (16.08, 0.5), (16.08, 0.5), (16.08, 0.5), (24.0, 0.0) }, Styles(bloc));
        Assert.Equal(true, vm.MiseEnFormeVisee.Exposant);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UnSeulPas(vm, avant, "Ctrl+Maj+= sur une sélection mixte");
    }

    // ------------------------------------------------------------------ les capitales

    /// <summary>
    /// <c>Ctrl+Maj+K</c> hors saisie : le bloc prend les petites majuscules, sans fragment, et son
    /// texte ne change pas. Au PDF comme à l'écran, « H » au corps plein et « ELLO » au corps réduit :
    /// les petites capitales à 0,1 pt de 70 % de la hauteur des capitales, lue dans la police. La même
    /// touche les retire ; un pas chacune.
    /// </summary>
    [AvaloniaFact]
    public void Les_petites_majuscules_a_70_pour_cent_de_la_hauteur_des_capitales_au_pdf_et_a_l_ecran()
    {
        var bloc = Bloc("Hello");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        Rendu.ToucheAzerty(fenetre, PhysicalKey.K, RawInputModifiers.Control | RawInputModifiers.Shift);

        Assert.Equal(Capitales.PetitesCapitales, bloc.Capitales);
        Assert.Empty(bloc.Spans);
        Assert.Equal("Hello", bloc.Text);
        Assert.Equal(Capitales.PetitesCapitales, vm.MiseEnFormeVisee.Capitales);
        Assert.Equal("Petites majuscules", vm.Status);
        Rendu.UneImage();

        var ecran = MorceauxALEcran(fenetre, bloc);
        Assert.Equal(new[] { ("H", 24.0), ("ELLO", 16.8) }, ecran.Select(m => (m.Texte, Math.Round(m.Corps, 9))));

        var pdf = DessinsDuPdf(vm).Where(d => d.Texte is "H" or "ELLO").ToList();

        Assert.Equal(new[] { "H", "ELLO" }, pdf.Select(d => d.Texte));
        var pleine = HauteurDesCapitales('H', pdf[0].Corps);
        var petite = HauteurDesCapitales('E', pdf[1].Corps);
        Assert.InRange(pleine, 0.6 * Corps, 0.8 * Corps);
        _sortie.WriteLine($"hauteur des capitales {pleine:F4} pt, des petites {petite:F4} pt, 70 % : {0.7 * pleine:F4} pt");
        Assert.InRange(petite, 0.7 * pleine - 0.1, 0.7 * pleine + 0.1);
        Assert.Equal(pdf[0].Y, pdf[1].Y, 3);
        Assert.InRange(pdf[0].Y, ecran[0].LigneDeBase - Precision, ecran[0].LigneDeBase + Precision);

        var pose = Etat(vm);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.K, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(Capitales.Aucune, bloc.Capitales);
        Assert.Equal("Petites majuscules retirées", vm.Status);
        Assert.Equal(avant, Etat(vm));

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(pose, Etat(vm));
        Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "Ctrl+Maj+K hors saisie"));
    }

    /// <summary>
    /// Le menu Texte porte les quatre commandes sous <i>Exposant et majuscules</i>, aux touches de la
    /// fiche 24 écrites en français ; <i>Majuscules</i> y met tout le bloc en capitales, le texte
    /// tapé intact — la copie le lit tel quel. En saisie, le clic droit dans le texte mène aux mêmes
    /// commandes, sur la sélection : le mot du curseur passe en indice.
    /// </summary>
    [AvaloniaFact]
    public void Le_menu_Texte_et_le_menu_du_texte_portent_les_quatre_commandes()
    {
        var bloc = Bloc("Bonjour le monde");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        Rendu.Cliquer(Menu(fenetre, "_Texte"));
        Rendu.Cliquer(Menu(fenetre, "Exposant et _majuscules"));
        Assert.Equal("Ctrl+Maj+=", GesteAffiche(Menu(fenetre, "_Exposant")));
        Assert.Equal("Ctrl+=", GesteAffiche(Menu(fenetre, "_Indice")));
        Assert.Equal("Ctrl+Maj+K", GesteAffiche(Menu(fenetre, "_Petites majuscules")));
        Assert.True(string.IsNullOrEmpty(GesteAffiche(Menu(fenetre, "_Majuscules"))));
        Rendu.Cliquer(Menu(fenetre, "_Majuscules"));
        Rendu.Battre(8);

        Assert.Equal(Capitales.Capitales, bloc.Capitales);
        Assert.Equal("Bonjour le monde", bloc.Text);
        Rendu.UneImage();
        Assert.Equal("BONJOUR LE MONDE", string.Concat(MorceauxALEcran(fenetre, bloc).Select(m => m.Texte)));
        Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "Texte ▸ Majuscules"));
        bloc = (TextFrame)vm.SingleSelected!;

        // En saisie : le curseur dans « monde », le clic droit, Indice.
        bloc.Capitales = Capitales.Capitales;
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.Cliquer(fenetre, Rendu.BordDuCaractere(zone, 13));
        Assert.Equal(new SelectionDuTexte(bloc, null, 13, 13, 13), vm.SelectionDuTexte);
        var menu = Rendu.CliquerDroit(fenetre, Rendu.BordDuCaractere(zone, 13));
        Assert.True(bloc.IsEditing, "le clic droit dans le texte a refermé la saisie");
        var sousMenu = Entree(menu, "E_xposant et majuscules");
        Assert.True(sousMenu.IsVisible);
        Rendu.Cliquer(sousMenu);
        Rendu.Cliquer(Entree(sousMenu, "_Indice"));
        Rendu.Battre(8);

        var indice = Assert.Single(bloc.Spans);
        Assert.Equal((11, 5, -0.5), (indice.Start, indice.Length, indice.BaselineShift));
        Assert.True(bloc.IsEditing);

        // La copie lit le texte tapé, et non les capitales dessinées.
        Rendu.SelectionnerDuTexte(zone, 0, 7);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.C, RawInputModifiers.Control);
        Assert.Equal("Bonjour", Rendu.TexteCopie(fenetre));
    }

    // ------------------------------------------------------------------ le zoom

    /// <summary>
    /// <c>Ctrl+=</c> ne zoome plus (règle 12) : sans bloc de texte, la touche ne fait rien, ni
    /// <c>Ctrl+Maj+=</c> ; le zoom avant n'a plus de touche, et le « + » du pavé numérique, qu'Avalonia
    /// confond avec « = » en comparant un geste, ne zoome pas non plus. <c>Ctrl+−</c> garde le zoom
    /// arrière.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_egal_ne_zoome_plus_et_le_zoom_garde_ses_autres_touches()
    {
        var forme = new ShapeFrame { Name = "Carré", X = 60, Y = 60, Width = 80, Height = 80 };
        var bloc = Bloc("H2O");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(forme, bloc));
        vm.SelectOnly(forme);
        Rendu.Battre();
        vm.SetZoom(1.25);
        var avant = Etat(vm);

        Indice(fenetre);
        Exposant(fenetre);
        Rendu.Touche(fenetre, PhysicalKey.NumPadAdd, RawInputModifiers.Control);
        Assert.Equal(1.25, vm.Zoom, 6);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.BasculerLIndiceCommand.CanExecute(null));

        Rendu.ToucheAzerty(fenetre, PhysicalKey.Minus, RawInputModifiers.Control);
        Assert.True(vm.Zoom < 1.25, "Ctrl+− ne zoome plus en arrière");

        // Sur un bloc de texte, le « + » du pavé est l'indice, comme « = » : Avalonia 11.3.9 les confond.
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var zoom = vm.Zoom;
        Rendu.Touche(fenetre, PhysicalKey.NumPadAdd, RawInputModifiers.Control);
        Assert.Equal(zoom, vm.Zoom, 6);
        Assert.Equal(-0.5, Assert.Single(bloc.Spans).BaselineShift);
    }
}
