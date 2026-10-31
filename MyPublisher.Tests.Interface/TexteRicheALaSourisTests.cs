using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyPublisher.Converters;
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
/// Éditer le texte riche à la souris — poste R7, repris dans la phase N (fiche 23, b1, b4, b13,
/// b18, b19), en voie 2a. En saisie, la police, le corps, le gras, l'italique, le souligné et ses
/// sortes, la couleur, <c>Ctrl+Espace</c>, <c>Ctrl+Maj+C</c> et <c>Ctrl+Maj+V</c> visent la
/// sélection du texte, ou le mot du curseur, et l'écrivent en fragments ; la barre montre ce que les
/// caractères visés ont en commun, et rien quand c'est mixte. Hors saisie, le bloc veut dire tous ses
/// caractères, fragments compris.
/// </summary>
[Trait("poste", "R7")]
public class TexteRicheALaSourisTests
{
    private readonly ITestOutputHelper _sortie;

    public TexteRicheALaSourisTests(ITestOutputHelper sortie) => _sortie = sortie;

    // ------------------------------------------------------------------ le banc du poste

    private const string Phrase = "abc def ghi";

    /// <summary>Une phrase de trois mots, assez grosse pour qu'un glissé vise chaque caractère.</summary>
    private static TextFrame Bloc(string texte = Phrase) => new()
    {
        Name = "Phrase", X = 100, Y = 100, Width = 360, Height = 120, Text = texte,
        FontFamily = "DejaVu Sans", FontSize = 24, Foreground = "#FF000000", Align = TextAlign.Left,
    };

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static BarreContextuelle Barre(Window fenetre) => Trouver<BarreContextuelle>(fenetre, "BarreContextuelle");

    /// <summary>Le bouton à état de la barre qui porte cette bulle.</summary>
    private static ToggleButton Bouton(Window fenetre, string bulle) =>
        Barre(fenetre).GetVisualDescendants().OfType<ToggleButton>()
            .Single(b => Equals(ToolTip.GetTip(b), bulle) && b.IsEffectivelyVisible);

    private static ToggleButton Gras(Window fenetre) => Bouton(fenetre, "Gras (Ctrl+G)");

    private static ComboBox Police(Window fenetre) => Trouver<ComboBox>(Barre(fenetre), "PoliceDuBloc");

    private static NumericUpDown Corps(Window fenetre) => Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc");

    /// <summary>Les fragments, tels qu'un test les compare : place et style, la langue mise à part.</summary>
    private static string Fragments(TextFrame bloc) => string.Join(" ; ", bloc.Spans.Select(f =>
        $"{f.Start}+{f.Length}" + (f.FontFamily is { } p ? $" {p}" : "") + (f.FontSize is { } c ? $" {c}pt" : "") +
        (f.Bold is { } g ? (g ? " gras" : " maigre") : "") + (f.Italic is { } i ? (i ? " italique" : " droit") : "") +
        (f.Underline is { } s ? $" souligné:{s.Line}{(s.Double ? ",double" : "")}" : "") +
        (f.Foreground is { } r ? $" {r}" : "") + (f.BaselineShift != 0 ? $" décalé:{f.BaselineShift}" : "")));

    /// <summary>Le style que la composition donne à chaque caractère du bloc.</summary>
    private static StyleEffectif[] StylesDesCaracteres(TextFrame bloc)
    {
        var styles = new List<StyleEffectif>();
        foreach (var (morceau, style) in StyleEffectif.Decouper(bloc.Text ?? "", bloc.Spans, MiseEnForme.Commun(bloc)))
        {
            styles.AddRange(Enumerable.Repeat(style, morceau.Length));
        }

        return styles.ToArray();
    }

    /// <summary>
    /// Entre dans le texte d'un clic, puis sélectionne au pointeur [<paramref name="debut"/>,
    /// <paramref name="fin"/>) : la recette du plan, « sélection des caractères 4 à 7 au pointeur
    /// synthétisé ».
    /// </summary>
    private static TextBox Selectionner(Window fenetre, MainWindowViewModel vm, TextFrame bloc, int debut, int fin)
    {
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc) ?? Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.SelectionnerDuTexte(zone, debut, fin);
        Assert.Equal(new SelectionDuTexte(bloc, null, debut, fin, fin), vm.SelectionDuTexte);
        return zone;
    }

    /// <summary>Le curseur posé au clic avant le caractère <paramref name="indice"/> (N12, N21).</summary>
    private static TextBox CurseurAuClic(Window fenetre, MainWindowViewModel vm, TextFrame bloc, int indice)
    {
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc) ?? Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.UneImage();
        Rendu.Cliquer(fenetre, Rendu.BordDuCaractere(zone, indice));
        Assert.Equal(new SelectionDuTexte(bloc, null, indice, indice, indice), vm.SelectionDuTexte);
        return zone;
    }

    /// <summary>
    /// Le texte que le PDF dessine, morceau par morceau, avec la fonte de chacun : le flux de la
    /// première page, décompressé par PDFsharp, lu opérateur par opérateur — <c>Tf</c> choisit la
    /// fonte, <c>Tj</c> et <c>TJ</c> dessinent ; la fonte est nommée par sa <c>/BaseFont</c>, prise
    /// dans les ressources de la page. Les fontes sont en WinAnsi : une chaîne est le texte même.
    /// </summary>
    private List<(string Fonte, string Texte)> TexteDuPdf(MainWindowViewModel vm)
    {
        var chemin = Path.Combine(Path.GetTempPath(), $"mypublisher-r7-{Guid.NewGuid():N}.pdf");
        try
        {
            vm.ExportPdf(chemin);
            using var pdf = PdfReader.Open(chemin, PdfDocumentOpenMode.Modify);
            var page = pdf.Pages[0];
            var ressources = page.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!;
            var fontes = ressources.Elements.Keys.ToDictionary(cle => cle.TrimStart('/'),
                cle => ressources.Elements.GetDictionary(cle)!.Elements.GetName("/BaseFont"));

            var flux = string.Concat(Enumerable.Range(0, page.Contents.Elements.Count)
                .SelectMany(i => page.Contents.Elements.GetDictionary(i)!.Stream.UnfilteredValue)
                .Select(o => (char)o));

            var dessins = new List<(string, string)>();
            var fonte = "";
            foreach (Match m in Regex.Matches(flux,
                         @"/(?<f>[^\s/\[\]()<>]+)\s+[-\d.]+\s+Tf|\((?<s>(?:\\.|[^\\)])*)\)\s*Tj|\[(?<a>[^\]]*)\]\s*TJ"))
            {
                if (m.Groups["f"].Success) fonte = fontes.GetValueOrDefault(m.Groups["f"].Value, m.Groups["f"].Value);
                else if (m.Groups["s"].Success) dessins.Add((fonte, Chaine(m.Groups["s"].Value)));
                else
                {
                    dessins.Add((fonte, string.Concat(Regex.Matches(m.Groups["a"].Value, @"\((?<s>(?:\\.|[^\\)])*)\)")
                        .Select(c => Chaine(c.Groups["s"].Value)))));
                }
            }

            foreach (var (f, t) in dessins) _sortie.WriteLine($"PDF : {f} « {t} »");
            return dessins;
        }
        finally
        {
            if (File.Exists(chemin)) File.Delete(chemin);
        }
    }

    /// <summary>Une chaîne littérale du PDF, ses échappements rendus.</summary>
    private static string Chaine(string litterale) =>
        Regex.Replace(litterale, @"\\(.)", m => m.Groups[1].Value switch
        {
            "n" => "\n", "r" => "\r", "t" => "\t", var autre => autre,
        });

    // ------------------------------------------------------------------ la recette du plan

    /// <summary>
    /// La recette : les caractères 4 à 7 sélectionnés au pointeur, puis <c>Ctrl+G</c>. Un seul
    /// fragment <c>{Start 4, Length 3, Bold}</c>, rien d'autre au bloc ; la saisie reste ouverte, la
    /// zone garde le focus — la frappe continue —, le bouton G s'allume ; le PDF dessine ces trois
    /// caractères, et eux seuls, dans la fonte grasse (relevé du flux) ; un pas d'annulation rend le
    /// bloc d'avant.
    /// </summary>
    [AvaloniaFact]
    public void Trois_caracteres_selectionnes_au_pointeur_passent_seuls_en_gras_a_ctrl_g_en_un_pas()
    {
        var bloc = Bloc();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        var zone = Selectionner(fenetre, vm, bloc, 4, 7);
        Assert.Equal(false, Gras(fenetre).IsChecked);

        Rendu.ToucheAzerty(fenetre, PhysicalKey.G, RawInputModifiers.Control);

        var fragment = Assert.Single(bloc.Spans);
        Assert.Equal("4+3 gras", Fragments(bloc));
        Assert.Equal((4, 3, (bool?)true), (fragment.Start, fragment.Length, fragment.Bold));
        Assert.False(bloc.Bold);
        Assert.True(bloc.IsEditing, "Ctrl+G a refermé la saisie");
        Assert.True(zone.IsFocused, "Ctrl+G a pris le focus à la zone de saisie : la frappe ne continue pas");
        Assert.Equal(true, Gras(fenetre).IsChecked);
        Assert.Equal("Gras : 3 caractères", vm.Status);
        Assert.Equal(new SelectionDuTexte(bloc, null, 4, 7, 7), vm.SelectionDuTexte);

        // Le PDF : « def » seul dans la fonte grasse.
        var dessins = TexteDuPdf(vm);
        var gras = dessins.Where(d => d.Fonte.Contains("Bold", StringComparison.Ordinal)).Select(d => d.Texte).ToList();
        Assert.Equal("def", string.Concat(gras).Trim());
        Assert.Equal(3, string.Concat(gras).Count(c => !char.IsWhiteSpace(c)));
        var maigres = string.Concat(dessins.Where(d => !d.Fonte.Contains("Bold", StringComparison.Ordinal)).Select(d => d.Texte));
        Assert.Equal("abcghi", string.Concat(maigres.Where(c => !char.IsWhiteSpace(c))));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        var rendu = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "Ctrl+G sur « def »"));
        Assert.Empty(rendu.Spans);
    }

    /// <summary>
    /// La recette : <c>Ctrl+G</c> sur une sélection déjà grasse retire le gras, et le fragment
    /// disparaît, ne portant plus rien ; un pas d'annulation par commande, défaits un à un.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_G_sur_une_selection_deja_grasse_retire_le_gras_et_le_fragment_disparait()
    {
        var bloc = Bloc();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<string> { Etat(vm) };

        var zone = Selectionner(fenetre, vm, bloc, 4, 7);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.G, RawInputModifiers.Control);
        Assert.Equal("4+3 gras", Fragments(bloc));
        etats.Add(Etat(vm));

        Rendu.ToucheAzerty(fenetre, PhysicalKey.G, RawInputModifiers.Control);
        Assert.Empty(bloc.Spans);
        Assert.False(bloc.Bold);
        Assert.Equal(false, Gras(fenetre).IsChecked);
        Assert.Equal("Gras retiré : 3 caractères", vm.Status);
        Assert.True(zone.IsFocused);
        etats.Add(Etat(vm));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        for (var i = etats.Count - 1; i > 0; i--)
        {
            Assert.True(vm.CanUndo, $"il manque le pas {i}");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal(etats[i - 1], Etat(vm));
        }

        Assert.False(vm.CanUndo, "plus d'un pas par commande");
    }

    /// <summary>
    /// La recette, en plus : sur une sélection mixte, <c>Ctrl+G</c> met le gras partout. Avant, le
    /// bouton G est à moitié allumé — <c>IsChecked</c> nul, l'état <c>:indeterminate</c>, sur la
    /// teinte de la projection — ; après, il est allumé, et un seul fragment couvre la sélection.
    /// </summary>
    [AvaloniaFact]
    public void Sur_une_selection_mixte_ctrl_g_met_le_gras_partout()
    {
        var bloc = Bloc();
        bloc.Spans.Add(new StyleSpan { Start = 4, Length = 3, Bold = true });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        Selectionner(fenetre, vm, bloc, 2, 9);
        var g = Gras(fenetre);
        Assert.Null(g.IsChecked);
        Assert.Contains(":indeterminate", g.Classes);
        var fond = g.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter").Background;
        var projection = (ISolidColorBrush)Application.Current!.FindResource("Projection")!;
        Assert.Equal((projection.Color, projection.Opacity), (((ISolidColorBrush)fond!).Color, fond.Opacity));
        Assert.False(vm.CanUndo, "lire la sélection mixte a écrit dans le document");

        Rendu.ToucheAzerty(fenetre, PhysicalKey.G, RawInputModifiers.Control);
        Assert.Equal("2+7 gras", Fragments(bloc));
        Assert.Equal(true, g.IsChecked);
        Assert.DoesNotContain(":indeterminate", g.Classes);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UnSeulPas(vm, avant, "Ctrl+G sur la sélection mixte");
    }

    /// <summary>
    /// La recette, en plus : le corps 14 posé à la barre fait un pas. Tapé dans le champ, il va aux
    /// caractères sélectionnés — la sélection survit au focus pris par le champ (N15) — et non au
    /// bloc ; une taille de la liste, 18, en fait un autre. Le champ montre le corps de la sélection,
    /// et la liste le coche.
    /// </summary>
    [AvaloniaFact]
    public void Le_corps_14_pose_a_la_barre_va_a_la_selection_en_un_pas()
    {
        var bloc = Bloc();
        bloc.FontSize = 10.5;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<string> { Etat(vm) };

        Selectionner(fenetre, vm, bloc, 4, 7);
        Rendu.Saisir(fenetre, Corps(fenetre), "14");
        Assert.Equal("4+3 14pt", Fragments(bloc));
        Assert.Equal(10.5, bloc.FontSize);
        Assert.True(bloc.IsEditing, "le champ du corps a refermé la saisie");
        Assert.Equal(new SelectionDuTexte(bloc, null, 4, 7, 7), vm.SelectionDuTexte);
        Assert.Equal(14m, Corps(fenetre).Value);
        etats.Add(Etat(vm));

        var chevron = Trouver<Button>(Barre(fenetre), "TaillesDuCorps");
        var tailles = Assert.IsType<MenuFlyout>(chevron.Flyout);
        Rendu.Cliquer(chevron);
        Assert.Equal(["14"], tailles.Items.OfType<MenuItem>().Where(e => e.IsChecked).Select(e => (string)e.Header!));
        Rendu.Cliquer(tailles.Items.OfType<MenuItem>().Single(e => (string?)e.Header == "18"));
        Assert.Equal("4+3 18pt", Fragments(bloc));
        Assert.Equal(10.5, bloc.FontSize);
        etats.Add(Etat(vm));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        for (var i = etats.Count - 1; i > 0; i--)
        {
            Assert.True(vm.CanUndo, $"il manque le pas {i}");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal(etats[i - 1], Etat(vm));
        }

        Assert.False(vm.CanUndo, "plus d'un pas par geste");
    }

    /// <summary>
    /// Sans sélection, la mise en forme va au mot du curseur (étude de 98, option 421 ; Word) :
    /// <c>Ctrl+I</c> le curseur dans « def » met « def » en italique, et la barre le montre. Au bout
    /// d'un mot, rien n'est mis en forme — il n'y a pas de style en attente —, aucun pas, et la barre
    /// d'état le dit.
    /// </summary>
    [AvaloniaFact]
    public void Sans_selection_le_mot_du_curseur_prend_la_mise_en_forme()
    {
        var bloc = Bloc();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        var zone = CurseurAuClic(fenetre, vm, bloc, 5);
        Assert.Equal(new Plage(4, 3), vm.MiseEnFormeVisee.Caracteres);
        Rendu.Touche(fenetre, PhysicalKey.I, RawInputModifiers.Control);
        Assert.Equal("4+3 italique", Fragments(bloc));
        Assert.Equal(true, Bouton(fenetre, "Italique (Ctrl+I)").IsChecked);
        Assert.True(zone.IsFocused);
        var apres = Etat(vm);

        // Loin du point du clic d'avant : au même endroit, ce serait un double-clic.
        CurseurAuClic(fenetre, vm, bloc, 11);
        Assert.Equal(new Plage(11, 0), vm.MiseEnFormeVisee.Caracteres);
        Rendu.Touche(fenetre, PhysicalKey.I, RawInputModifiers.Control);
        Assert.Equal("4+3 italique", Fragments(bloc));
        Assert.Equal(apres, Etat(vm));
        Assert.StartsWith("Rien à mettre en forme", vm.Status);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UnSeulPas(vm, avant, "Ctrl+I au mot du curseur");
    }

    /// <summary>Le mot du curseur : une lettre de chaque côté, la suite de lettres et de chiffres qu'il coupe.</summary>
    [Theory]
    [InlineData("abc def ghi", 5, 4, 3)]
    [InlineData("abc def ghi", 1, 0, 3)]
    [InlineData("abc def ghi", 10, 8, 3)]
    [InlineData("abc def ghi", 4, -1, 0)]
    [InlineData("abc def ghi", 7, -1, 0)]
    [InlineData("abc def ghi", 0, -1, 0)]
    [InlineData("abc def ghi", 11, -1, 0)]
    [InlineData("l'été, déjà", 4, 2, 3)]
    [InlineData("ATA-21 x", 5, 4, 2)]
    [InlineData("", 0, -1, 0)]
    public void Le_mot_du_curseur(string texte, int curseur, int debut, int longueur)
    {
        var mot = MiseEnFormeVisee.MotDuCurseur(texte, curseur);
        Assert.Equal(debut < 0 ? null : new Plage(debut, longueur), mot);
    }

    // ------------------------------------------------------------------ hors saisie

    /// <summary>
    /// Hors saisie, le bloc veut dire tous ses caractères, fragments compris : un mot gras par un
    /// fragment importé allume le G à moitié ; <c>Ctrl+G</c> met tout le bloc en gras et le fragment
    /// ne dit plus rien — il disparaît —, puis le retire de tout le bloc. Un fragment qui dit autre
    /// chose garde le reste : son corps. Un pas chacun.
    /// </summary>
    [AvaloniaFact]
    public void Hors_saisie_le_bloc_veut_dire_tous_ses_caracteres_fragments_compris()
    {
        var bloc = Bloc();
        bloc.Spans.Add(new StyleSpan { Start = 4, Length = 3, Bold = true });
        bloc.Spans.Add(new StyleSpan { Start = 8, Length = 3, Bold = false, FontSize = 30 });
        bloc.Bold = false;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var etats = new List<string> { Etat(vm) };
        Assert.False(vm.MiseEnFormeVisee.EnSaisie);
        Assert.Null(Gras(fenetre).IsChecked);
        Assert.Null(Corps(fenetre).Value);

        Rendu.ToucheAzerty(fenetre, PhysicalKey.G, RawInputModifiers.Control);
        Assert.True(bloc.Bold);
        Assert.Equal("8+3 30pt", Fragments(bloc));
        Assert.All(StylesDesCaracteres(bloc), s => Assert.True(s.Bold));
        Assert.Equal(true, Gras(fenetre).IsChecked);
        etats.Add(Etat(vm));

        Rendu.ToucheAzerty(fenetre, PhysicalKey.G, RawInputModifiers.Control);
        Assert.False(bloc.Bold);
        Assert.Equal("8+3 30pt", Fragments(bloc));
        Assert.All(StylesDesCaracteres(bloc), s => Assert.False(s.Bold));
        etats.Add(Etat(vm));

        // Le corps, hors saisie : chaque caractère depuis le sien — 24 et 30 donnent 25 et 31.
        Rendu.ToucheAzerty(fenetre, PhysicalKey.Minus, RawInputModifiers.Control, altGr: true);
        Assert.Equal(25, bloc.FontSize);
        Assert.Equal("8+3 31pt", Fragments(bloc));
        etats.Add(Etat(vm));

        for (var i = etats.Count - 1; i > 0; i--)
        {
            Assert.True(vm.CanUndo, $"il manque le pas {i}");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal(etats[i - 1], Etat(vm));
        }

        Assert.False(vm.CanUndo, "plus d'un pas par geste");
    }

    /// <summary>
    /// Le gras qu'un import a écrit dans un fragment s'atteint : sur chaque bloc d'ATA 21 dont un
    /// fragment dit le gras, <c>Ctrl+G</c> hors saisie met tous les caractères en gras — aucun
    /// fragment ne dit plus le maigre —, puis les remet tous en maigre — aucun ne dit plus le gras ;
    /// deux pas, qui rendent le document d'ouverture.
    /// </summary>
    [AvaloniaFact]
    public void Le_gras_ecrit_dans_les_fragments_d_ATA_21_s_atteint_hors_saisie()
    {
        var document = Rendu.Ata21();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var avant = Etat(vm);
        var vus = 0;

        foreach (var page in document.Pages)
        {
            vm.CurrentPage = page;
            Rendu.Battre();
            foreach (var bloc in page.Frames.OfType<TextFrame>().Where(b => b.Spans.Any(f => f.Bold is not null)).ToList())
            {
                vm.SelectOnly(bloc);
                Rendu.Battre();
                var styles = StylesDesCaracteres(bloc);
                var mixte = styles.Any(s => s.Bold) && styles.Any(s => !s.Bold);
                if (mixte) Assert.Null(Gras(fenetre).IsChecked);

                _sortie.WriteLine($"« {bloc.Name} » : {bloc.Text?.Length} caractères ; {Fragments(bloc)}");
                Rendu.ToucheAzerty(fenetre, PhysicalKey.G, RawInputModifiers.Control);
                var gras = vm.SingleSelected as TextFrame ?? bloc;
                _sortie.WriteLine($"   Ctrl+G : {Fragments(gras)}");
                Assert.True(gras.Bold);
                Assert.All(StylesDesCaracteres(gras), s => Assert.True(s.Bold, $"« {gras.Name} » : un caractère reste maigre"));
                // Un fragment qui dit le gras comme le bloc ne change rien à l'œil : la mise en forme
                // le laisse (N2, « sans changement réel, rien ne se fait ») ; aucun ne dit le maigre.
                Assert.DoesNotContain(gras.Spans, f => f.Bold == false);

                Rendu.ToucheAzerty(fenetre, PhysicalKey.G, RawInputModifiers.Control);
                _sortie.WriteLine($"   Ctrl+G : {Fragments(gras)}");
                Assert.False(gras.Bold);
                Assert.All(StylesDesCaracteres(gras), s => Assert.False(s.Bold, $"« {gras.Name} » : un caractère reste gras"));
                Assert.DoesNotContain(gras.Spans, f => f.Bold == true);
                vus++;
            }
        }

        _sortie.WriteLine($"{vus} blocs d'ATA 21 dont un fragment dit le gras");
        Assert.True(vus > 0, "ATA 21 n'a aucun fragment qui dise le gras");
        for (var i = 0; i < 2 * vus; i++)
        {
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
        }

        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "plus de deux pas par bloc");
    }

    // ------------------------------------------------------------------ la barre

    /// <summary>
    /// La barre montre l'état de la sélection (fiche 23, b4) : sur une sélection qui mêle deux
    /// polices, deux corps et deux couleurs, la liste des polices et le champ du corps sont vides, la
    /// vignette de la couleur n'en montre aucune ; une sélection d'un seul style les montre. Lire
    /// n'écrit rien : la garde de Q20 tient face à la police mixte — la liste vide ne renvoie rien —,
    /// et le document est intact au caractère près.
    /// </summary>
    [AvaloniaFact]
    public void La_barre_montre_l_etat_de_la_selection_et_n_ecrit_rien()
    {
        var bloc = Bloc();
        bloc.Spans.Add(new StyleSpan { Start = 4, Length = 3, FontFamily = "DejaVu Serif", FontSize = 30, Foreground = "#FFCC0000" });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        var zone = Selectionner(fenetre, vm, bloc, 2, 9);
        Assert.Null(Police(fenetre).SelectedItem);
        Assert.Null(Corps(fenetre).Value);
        Assert.Null(vm.MiseEnFormeVisee.Foreground);
        var vignette = Trouver<Button>(Barre(fenetre), "CouleurDuTexte").GetVisualDescendants().OfType<Border>()
            .First(b => b.CornerRadius == new CornerRadius(3));
        Assert.Null(vignette.Background);

        Rendu.SelectionnerDuTexte(zone, 4, 7);
        Assert.Equal("DejaVu Serif", Police(fenetre).SelectedItem);
        Assert.Equal(30m, Corps(fenetre).Value);
        Assert.Equal(Color.Parse("#FFCC0000"), ((ISolidColorBrush)vignette.Background!).Color);

        Rendu.SelectionnerDuTexte(zone, 0, 3);
        Assert.Equal("DejaVu Sans", Police(fenetre).SelectedItem);
        Assert.Equal(24m, Corps(fenetre).Value);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "lire la sélection a écrit dans le document");
    }

    /// <summary>
    /// La police tapée dans la barre (N19) et la couleur prise au sélecteur vont à la sélection, un
    /// pas chacune ; la saisie reste ouverte.
    /// </summary>
    [AvaloniaFact]
    public void La_police_et_la_couleur_vont_a_la_selection()
    {
        var bloc = Bloc();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<string> { Etat(vm) };

        Selectionner(fenetre, vm, bloc, 4, 7);
        var police = Police(fenetre);
        var zoneDeLaPolice = police.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "PART_EditableTextBox");
        Rendu.Cliquer(zoneDeLaPolice);
        zoneDeLaPolice.SelectAll();
        Rendu.Taper(fenetre, "DejaVu Serif");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal("4+3 DejaVu Serif", Fragments(bloc));
        Assert.Equal("DejaVu Sans", bloc.FontFamily);
        Assert.True(bloc.IsEditing);
        etats.Add(Etat(vm));

        var couleur = Trouver<Button>(Barre(fenetre), "CouleurDuTexte");
        Rendu.Cliquer(couleur);
        var volet = Assert.IsType<Flyout>(couleur.Flyout);
        var selecteur = Assert.IsType<SelecteurDeCouleur>(volet.Content);
        Rendu.Cliquer(selecteur.GetVisualDescendants().OfType<Button>().Single(b => b.Tag is EmplacementDeCouleur.Accent2));
        volet.Hide();
        Rendu.Battre();
        Assert.Equal("4+3 DejaVu Serif scheme:accent2", Fragments(bloc));
        Assert.Equal("#FF000000", bloc.Foreground);
        etats.Add(Etat(vm));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        for (var i = etats.Count - 1; i > 0; i--)
        {
            Assert.True(vm.CanUndo, $"il manque le pas {i}");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal(etats[i - 1], Etat(vm));
        }

        Assert.False(vm.CanUndo, "plus d'un pas par geste");
    }

    /// <summary>
    /// <c>Ctrl+U</c> souligne la sélection d'un trait simple, et le chevron des sortes la souligne
    /// double ; le bouton S s'allume, la sorte de la sélection est cochée. Un pas chacun.
    /// </summary>
    [AvaloniaFact]
    public void Le_souligne_et_ses_sortes_vont_a_la_selection()
    {
        var bloc = Bloc();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<string> { Etat(vm) };

        Selectionner(fenetre, vm, bloc, 4, 7);
        Rendu.Touche(fenetre, PhysicalKey.U, RawInputModifiers.Control);
        Assert.Equal("4+3 souligné:Solid", Fragments(bloc));
        Assert.Equal(Souligne.Aucun, bloc.Underline);
        Assert.Equal(true, Bouton(fenetre, "Souligné (Ctrl+U)").IsChecked);
        etats.Add(Etat(vm));

        var chevron = Trouver<Button>(Barre(fenetre), "SortesDuSouligne");
        var sortes = Assert.IsType<MenuFlyout>(chevron.Flyout);
        Rendu.Cliquer(chevron);
        Assert.Equal(["Trait simple"], sortes.Items.OfType<MenuItem>().Where(e => e.IsChecked).Select(e => (string)e.Header!));
        Rendu.Cliquer(sortes.Items.OfType<MenuItem>().Single(e => (string?)e.Header == "Double"));
        Assert.Equal("4+3 souligné:Solid,double", Fragments(bloc));
        etats.Add(Etat(vm));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        for (var i = etats.Count - 1; i > 0; i--)
        {
            Assert.True(vm.CanUndo, $"il manque le pas {i}");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal(etats[i - 1], Etat(vm));
        }

        Assert.False(vm.CanUndo, "plus d'un pas par geste");
    }

    // ------------------------------------------------------------------ les touches de la fiche 24

    /// <summary>
    /// <c>Ctrl+Espace</c> rend aux caractères visés le style du bloc (fiche 24) : en saisie, la
    /// sélection seule perd ses fragments, la langue restant ; hors saisie, tout le bloc. Un pas
    /// chacun ; sans rien à retirer, aucun.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_espace_rend_aux_caracteres_le_style_du_bloc()
    {
        var bloc = Bloc();
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 11, Italic = true, Langue = "en-GB" });
        bloc.Spans.Add(new StyleSpan { Start = 4, Length = 7, Bold = true, FontSize = 30 });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<string> { Etat(vm) };

        Selectionner(fenetre, vm, bloc, 4, 7);
        Rendu.Touche(fenetre, PhysicalKey.Space, RawInputModifiers.Control);
        var styles = StylesDesCaracteres(bloc);
        Assert.All(styles[4..7], s => Assert.Equal((false, false, 24.0), (s.Bold, s.Italic, s.FontSize)));
        Assert.All(styles[..4], s => Assert.True(s.Italic));
        Assert.All(styles[7..], s => Assert.Equal((true, true, 30.0), (s.Bold, s.Italic, s.FontSize)));
        Assert.All(Enumerable.Range(0, 11), i => Assert.Equal("en-GB", Langues.DuCaractere(bloc, i, vm.Document)));
        Assert.Equal("Mise en forme des caractères retirée", vm.Status);
        etats.Add(Etat(vm));

        Rendu.Touche(fenetre, PhysicalKey.Space, RawInputModifiers.Control);
        Assert.Equal(etats[^1], Etat(vm));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        vm.SelectOnly(bloc);
        Rendu.Battre();
        Assert.False(vm.MiseEnFormeVisee.EnSaisie);
        Rendu.Touche(fenetre, PhysicalKey.Space, RawInputModifiers.Control);
        Assert.All(StylesDesCaracteres(bloc), s => Assert.Equal(MiseEnForme.Commun(bloc), s));
        Assert.DoesNotContain(bloc.Spans, f => f.Bold is not null || f.Italic is not null || f.FontSize is not null);
        etats.Add(Etat(vm));

        for (var i = etats.Count - 1; i > 0; i--)
        {
            Assert.True(vm.CanUndo, $"il manque le pas {i}");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal(etats[i - 1], Etat(vm));
        }

        Assert.False(vm.CanUndo, "plus d'un pas par geste");
    }

    /// <summary>
    /// <c>Ctrl+Maj+C</c> copie la mise en forme du mot du curseur, <c>Ctrl+Maj+V</c> la pose sur une
    /// autre sélection, en un pas ; le menu Texte les offre aussi. Sans rien de copié, rien ne
    /// change, et la barre d'état le dit.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_maj_c_et_ctrl_maj_v_reproduisent_la_mise_en_forme()
    {
        var bloc = Bloc();
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 3, Bold = true, FontSize = 30, Foreground = "scheme:accent1" });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        Selectionner(fenetre, vm, bloc, 8, 11);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.V, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.StartsWith("Aucune mise en forme copiée", vm.Status);
        Assert.Equal(avant, Etat(vm));

        CurseurAuClic(fenetre, vm, bloc, 1);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.C, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal("Mise en forme copiée", vm.Status);
        Assert.Equal(avant, Etat(vm));

        var zone = Selectionner(fenetre, vm, bloc, 8, 11);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.V, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal("0+3 30pt gras scheme:accent1 ; 8+3 30pt gras scheme:accent1", Fragments(bloc));
        Assert.True(zone.IsFocused);
        Assert.Equal(new SelectionDuTexte(bloc, null, 8, 11, 11), vm.SelectionDuTexte);

        // Le menu Texte : les trois entrées, sur les mêmes commandes.
        var menu = fenetre.GetVisualDescendants().OfType<MenuItem>().Single(m => (string?)m.Header == "_Texte");
        Assert.Contains(menu.Items.OfType<MenuItem>(), m => ReferenceEquals(m.Command, vm.RetirerLaMiseEnFormeCommand));
        Assert.Contains(menu.Items.OfType<MenuItem>(), m => ReferenceEquals(m.Command, vm.CopierLaMiseEnFormeCommand));
        Assert.Contains(menu.Items.OfType<MenuItem>(), m => ReferenceEquals(m.Command, vm.CollerLaMiseEnFormeCommand));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UnSeulPas(vm, avant, "Ctrl+Maj+V");
    }

    /// <summary>
    /// Les touches du corps de N10 sur une sélection mixte : chaque caractère depuis le sien —
    /// <c>Ctrl+]</c> sur 24 et 30 pt donne 25 et 31 —, en un pas ; la barre dit le corps mixte.
    /// </summary>
    [AvaloniaFact]
    public void Les_touches_du_corps_changent_chaque_caractere_du_sien()
    {
        var bloc = Bloc();
        bloc.Spans.Add(new StyleSpan { Start = 4, Length = 3, FontSize = 30 });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        Selectionner(fenetre, vm, bloc, 2, 9);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.Minus, RawInputModifiers.Control, altGr: true);
        Assert.Equal([24, 24, 25, 25, 31, 31, 31, 25, 25, 24, 24], StylesDesCaracteres(bloc).Select(s => s.FontSize));
        Assert.Equal(24, bloc.FontSize);
        Assert.Null(Corps(fenetre).Value);
        Assert.Equal("Corps changé", vm.Status);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UnSeulPas(vm, avant, "Ctrl+] sur la sélection mixte");
    }

    // ------------------------------------------------------------------ les pièges

    /// <summary>
    /// Le piège de Q14 : la mémoire d'une case face à une valeur nulle. Le champ du corps a montré
    /// 10,123456789 pt, arrondi à 10,1235 ; la sélection mixte lui donne rien. La case se vide : un
    /// 10,1235 tapé ensuite est 10,1235, et non la valeur exacte d'un affichage d'avant.
    /// </summary>
    [Fact]
    public void La_memoire_du_corps_se_vide_quand_le_champ_ne_montre_rien()
    {
        var convertisseur = new DoubleToDecimal();
        Assert.Equal(10.1235m, convertisseur.Convert(10.123456789, typeof(decimal?), null, CultureInfo.InvariantCulture));
        Assert.Null(convertisseur.Convert(null, typeof(decimal?), null, CultureInfo.InvariantCulture));
        Assert.Equal(10.1235, convertisseur.ConvertBack(10.1235m, typeof(double?), null, CultureInfo.InvariantCulture));

        // Sans la valeur nulle entre les deux, le nombre affiché rend la valeur exacte (Q14).
        Assert.Equal(10.1235m, convertisseur.Convert(10.123456789, typeof(decimal?), null, CultureInfo.InvariantCulture));
        Assert.Equal(10.123456789, convertisseur.ConvertBack(10.1235m, typeof(double?), null, CultureInfo.InvariantCulture));
    }
}
