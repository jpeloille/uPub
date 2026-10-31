using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;
using Xunit.Abstractions;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Le panneau règle le paragraphe du curseur — poste R6, repris dans la phase N (fiche 23, b8, b9,
/// b11). Pendant la saisie d'un bloc de texte, l'alignement de la barre, du menu Texte et de
/// <c>Ctrl+L</c>, <c>Ctrl+E</c>, <c>Ctrl+R</c>, <c>Ctrl+J</c>, les retraits et les espacements du
/// dock s'appliquent aux paragraphes que touche la sélection, ou à celui du curseur, en écarts
/// <see cref="ParagraphSpan"/> ; hors saisie, ils restent réglages du bloc. Ce qu'ils montrent est
/// ce que les paragraphes visés ont en commun : mixtes, aucun bouton n'est allumé. Deux boutons du
/// dock, « Diminuer » et « Augmenter le retrait », vont de taquet en taquet, de 36 pt.
/// </summary>
[Trait("poste", "R6")]
public class ParagrapheDuCurseurTests
{
    private readonly ITestOutputHelper _sortie;

    public ParagrapheDuCurseurTests(ITestOutputHelper sortie) => _sortie = sortie;

    // ------------------------------------------------------------------ le banc du poste

    /// <summary>La précision que le plan demande à l'écran et au PDF.</summary>
    private const double Precision = 0.05;

    private const string TroisParagraphes = "Alpha\nMilieu\nOmega";

    /// <summary>Trois paragraphes d'un mot, au fer à gauche, assez gros pour qu'un clic les vise.</summary>
    private static TextFrame Trois(string texte = TroisParagraphes) => new()
    {
        Name = "Trois", X = 100, Y = 100, Width = 360, Height = 160, Text = texte,
        FontFamily = "DejaVu Sans", FontSize = 24, Foreground = "#FF000000", Align = TextAlign.Left,
    };

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    /// <summary>Le bouton d'alignement de la barre qui porte cette bulle.</summary>
    private static ToggleButton Alignement(Window fenetre, string bulle) =>
        Trouver<BarreContextuelle>(fenetre, "BarreContextuelle").GetVisualDescendants().OfType<ToggleButton>()
            .Single(b => Equals(ToolTip.GetTip(b), bulle) && b.IsEffectivelyVisible);

    private static readonly string[] Bulles =
        ["Aligner le texte à gauche", "Centrer le texte", "Aligner le texte à droite", "Justifier"];

    /// <summary>Les bulles des boutons d'alignement allumés.</summary>
    private static string[] Allumes(Window fenetre) =>
        Bulles.Where(b => Alignement(fenetre, b).IsChecked == true).ToArray();

    /// <summary>Un champ de la section Paragraphe du dock, la section ouverte.</summary>
    private static ChampNumerique Champ(Window fenetre, MainWindowViewModel vm, string nom)
    {
        vm.SectionParagrapheOuverte = true;
        Rendu.Battre();
        return Trouver<ChampNumerique>(fenetre, nom);
    }

    /// <summary>Les réglages effectifs de chaque paragraphe du bloc, comme la composition les lit.</summary>
    private static IReadOnlyList<ParagrapheEffectif> Effectifs(TextFrame bloc) =>
        ParagrapheEffectif.Resoudre(Recalage.NombreDeParagraphes(bloc.Text ?? ""), bloc.Paragraphs, bloc.Paragraph);

    private static TexteCompose Compose(Window fenetre, TextFrame bloc) =>
        fenetre.GetVisualDescendants().OfType<TexteCompose>().First(c => ReferenceEquals(c.Bloc, bloc) && c.IsEffectivelyVisible);

    /// <summary>
    /// Pose le curseur avant le caractère <paramref name="indice"/> d'un clic sur la composition :
    /// la saisie ouverte d'abord par un clic dans le texte (N12), puis un clic au bord du caractère
    /// (N21). Rend la zone de saisie.
    /// </summary>
    private static TextBox CurseurAuClic(Window fenetre, MainWindowViewModel vm, TextFrame bloc, int indice)
    {
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc) ?? Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.UneImage();
        Rendu.Cliquer(fenetre, Rendu.BordDuCaractere(zone, indice));
        Assert.True(bloc.IsEditing, "le clic dans le texte a refermé la saisie");
        Assert.Equal(new SelectionDuTexte(bloc, null, indice, indice, indice), vm.SelectionDuTexte);
        return zone;
    }

    /// <summary>Le bord gauche et le bord droit d'une plage du texte composé à l'écran, sur la page, en points.</summary>
    private static (double Gauche, double Droite) AlEcran(Window fenetre, TextFrame bloc, int debut, int fin)
    {
        var geometrie = Compose(fenetre, bloc).Geometrie() ?? throw new InvalidOperationException("pas de géométrie");
        return (bloc.X + geometrie.PointDe(debut).X, bloc.X + geometrie.PointDe(fin).X);
    }

    /// <summary>Les boîtes horizontales des mots de la première page du PDF, lues par « pdftotext -bbox ».</summary>
    private static Dictionary<string, (double XMin, double XMax)> MotsDuPdf(MainWindowViewModel vm)
    {
        var dossier = Path.Combine(Path.GetTempPath(), $"mypublisher-r6-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dossier);
        try
        {
            var pdf = Path.Combine(dossier, "paragraphe.pdf");
            var html = Path.Combine(dossier, "paragraphe.html");
            vm.ExportPdf(pdf);
            try
            {
                var demarrage = new ProcessStartInfo("pdftotext")
                {
                    ArgumentList = { "-bbox", "-f", "1", "-l", "1", pdf, html },
                    RedirectStandardError = true,
                };
                using var processus = Process.Start(demarrage)!;
                processus.WaitForExit(60_000);
                Assert.True(processus.ExitCode == 0, processus.StandardError.ReadToEnd());
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Assert.Fail("« pdftotext » est introuvable : sudo apt install poppler-utils.");
                throw;
            }

            return System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(html),
                    "<word xMin=\"([-\\d.]+)\" yMin=\"[-\\d.]+\" xMax=\"([-\\d.]+)\" yMax=\"[-\\d.]+\">([^<]*)</word>")
                .ToDictionary(m => m.Groups[3].Value,
                    m => (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                          double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
        }
        finally
        {
            Directory.Delete(dossier, recursive: true);
        }
    }

    // ------------------------------------------------------------------ la recette du plan

    /// <summary>
    /// La recette : le curseur posé au clic dans le deuxième de trois paragraphes, un clic sur
    /// « Centrer le texte » ; un seul écart <c>{Start 1, Length 1, Align Center}</c>, le bloc au
    /// fer à gauche ; l'écran et le PDF centrent ce paragraphe et lui seul, à 0,05 pt près ; la
    /// saisie reste ouverte, la zone garde le focus ; un pas d'annulation rend le bloc d'avant.
    /// </summary>
    [AvaloniaFact]
    public void Le_curseur_au_deuxieme_paragraphe_centre_lui_seul_a_l_ecran_et_au_pdf_en_un_pas()
    {
        var bloc = Trois();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        var zone = CurseurAuClic(fenetre, vm, bloc, 9);
        Assert.Equal(new Plage(1, 1), vm.ParagraphesDeLaSelection);
        Assert.Equal(["Aligner le texte à gauche"], Allumes(fenetre));

        Rendu.Cliquer(Alignement(fenetre, "Centrer le texte"));

        var ecart = Assert.Single(bloc.Paragraphs);
        Assert.Equal((1, 1, TextAlign.Center), (ecart.Start, ecart.Length, ecart.Align));
        Assert.Null(ecart.IndentLeft ?? ecart.IndentRight ?? ecart.IndentFirstLine ?? ecart.SpaceBefore ?? ecart.SpaceAfter);
        Assert.Equal(TextAlign.Left, bloc.Align);
        Assert.Equal(["Centrer le texte"], Allumes(fenetre));
        Assert.True(bloc.IsEditing, "le bouton a refermé la saisie");
        Assert.True(zone.IsFocused, "le bouton a pris le focus à la zone de saisie");
        Rendu.UneImage();

        // L'écran : la composition que la saisie montre (N21), lue sur sa géométrie.
        var gauche = bloc.X + bloc.PaddingLeft;
        var milieu = gauche + (bloc.Width - bloc.PaddingLeft - bloc.PaddingRight) / 2;
        var (g, d) = AlEcran(fenetre, bloc, 6, 12);
        _sortie.WriteLine($"écran : « Milieu » de {g:F4} à {d:F4}, milieu {(g + d) / 2:F4} pour {milieu:F4}");
        Assert.InRange((g + d) / 2, milieu - Precision, milieu + Precision);
        Assert.True(g > gauche + 20, "« Milieu » n'a pas quitté le fer à gauche");
        Assert.InRange(AlEcran(fenetre, bloc, 0, 5).Gauche, gauche - Precision, gauche + Precision);
        Assert.InRange(AlEcran(fenetre, bloc, 13, 18).Gauche, gauche - Precision, gauche + Precision);

        // Le PDF : les mots que pdftotext y lit. « Milieu » commence où l'écran le pose, à 0,05 pt :
        // la composition, commune aux deux, l'a centré. Sa fin est lue sur les chasses de la
        // police du PDF, qui ne façonne pas : 0,10 pt plus court que le morceau façonné de l'écran
        // (DejaVu Sans, 24 pt), l'écart des deux mesures que le README range dans ses limites ; le
        // milieu de l'encre du PDF est donc tenu à 0,05 pt plus la moitié de cet écart.
        var mots = MotsDuPdf(vm);
        var (xMin, xMax) = mots["Milieu"];
        var ecartDesMesures = Math.Abs((d - g) - (xMax - xMin));
        _sortie.WriteLine($"PDF : « Milieu » de {xMin:F4} à {xMax:F4}, milieu {(xMin + xMax) / 2:F4} ; " +
                          $"écart des deux mesures {ecartDesMesures:F4}");
        Assert.InRange(xMin, g - Precision, g + Precision);
        Assert.True(ecartDesMesures < 0.25, $"l'écran et le PDF mesurent « Milieu » à {ecartDesMesures:F4} pt l'un de l'autre");
        Assert.InRange((xMin + xMax) / 2, milieu - Precision - ecartDesMesures / 2, milieu + Precision + ecartDesMesures / 2);
        Assert.InRange(mots["Alpha"].XMin, gauche - Precision, gauche + Precision);
        Assert.InRange(mots["Omega"].XMin, gauche - Precision, gauche + Precision);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "Centrer le paragraphe du curseur"));
        Assert.Empty(((TextFrame)vm.SingleSelected!).Paragraphs);
    }

    /// <summary>
    /// Hors saisie, rien ne change : le bloc pris, un clic sur « Centrer le texte » centre le
    /// bloc, en un pas, et laisse ses écarts — un paragraphe à droite le reste ; un retrait saisi
    /// au dock est celui du bloc. Les boutons montrent le fer du bloc, non celui des paragraphes.
    /// </summary>
    [AvaloniaFact]
    public void Hors_saisie_le_panneau_regle_le_bloc_comme_avant()
    {
        var bloc = Trois();
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 2, Length = 1, Align = TextAlign.Right });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        Assert.False(vm.ParagrapheVise.EnSaisie);
        Assert.Equal(["Aligner le texte à gauche"], Allumes(fenetre));
        var avant = Etat(vm);

        Rendu.Cliquer(Alignement(fenetre, "Centrer le texte"));
        Assert.Equal(TextAlign.Center, bloc.Align);
        var ecart = Assert.Single(bloc.Paragraphs);
        Assert.Equal((2, 1, TextAlign.Right), (ecart.Start, ecart.Length, ecart.Align));
        Assert.Equal(["Centrer le texte"], Allumes(fenetre));
        bloc = (TextFrame)Rendu.UnSeulPas(vm, avant, "Centrer le bloc")!;

        Rendu.Saisir(fenetre, Champ(fenetre, vm, "RetraitAGauche"), "10");
        Assert.Equal(Units.MmToPoints(10), bloc.IndentLeft, precision: 9);
        Assert.Single(bloc.Paragraphs);
        Rendu.UnSeulPas(vm, avant, "Retrait du bloc");
    }

    /// <summary>
    /// L'état mixte n'allume aucun bouton : une sélection qui va d'un paragraphe au fer à gauche à
    /// un paragraphe centré n'en allume aucun, et laisse vide le champ d'un retrait où ils
    /// diffèrent ; le curseur dans l'un ou l'autre allume le sien, et le champ montre son retrait.
    /// Lire n'écrit rien.
    /// </summary>
    [AvaloniaFact]
    public void L_etat_mixte_n_allume_aucun_bouton_et_laisse_le_champ_vide()
    {
        var bloc = Trois();
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 1, Length = 1, Align = TextAlign.Center, IndentLeft = 20 });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        var zone = CurseurAuClic(fenetre, vm, bloc, 9);
        Assert.Equal(["Centrer le texte"], Allumes(fenetre));
        var retrait = Champ(fenetre, vm, "RetraitAGauche");
        Assert.Equal((decimal)Units.PointsToMm(20), retrait.Value!.Value, 6);

        Rendu.SelectionnerDuTexte(zone, 2, 9);
        Assert.Equal(new Plage(0, 2), vm.ParagraphesDeLaSelection);
        Assert.Empty(Allumes(fenetre));
        Assert.Null(vm.ParagrapheVise.Align);
        Assert.Null(retrait.Value);
        Assert.Null(vm.ParagrapheVise.IndentLeft);
        // Ce qu'ils ont en commun se montre : le retrait droit, nul des deux côtés.
        Assert.Equal((decimal?)0m, Champ(fenetre, vm, "RetraitADroite").Value);

        // Loin du point où le glissé a commencé : un clic au même endroit serait un double-clic.
        CurseurAuClic(fenetre, vm, bloc, 4);
        Assert.Equal(["Aligner le texte à gauche"], Allumes(fenetre));
        Assert.Equal((decimal?)0m, retrait.Value);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "lire les paragraphes a écrit dans le document");
    }

    /// <summary>
    /// Deux écarts voisins identiques fusionnent, et un écart qui ne s'écarte plus de rien
    /// disparaît : le deuxième paragraphe centré, puis le troisième, font un seul écart sur les
    /// deux ; la sélection des deux, alignée à gauche comme le bloc, n'en laisse aucun. Chaque
    /// geste fait son pas.
    /// </summary>
    [AvaloniaFact]
    public void Deux_ecarts_voisins_fusionnent_et_un_ecart_qui_ne_dit_plus_rien_disparait()
    {
        var bloc = Trois();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<string> { Etat(vm) };

        CurseurAuClic(fenetre, vm, bloc, 9);
        Rendu.Cliquer(Alignement(fenetre, "Centrer le texte"));
        etats.Add(Etat(vm));
        var zone = CurseurAuClic(fenetre, vm, bloc, 15);
        Rendu.Cliquer(Alignement(fenetre, "Centrer le texte"));
        etats.Add(Etat(vm));
        var ecart = Assert.Single(bloc.Paragraphs);
        Assert.Equal((1, 2, TextAlign.Center), (ecart.Start, ecart.Length, ecart.Align));

        Rendu.SelectionnerDuTexte(zone, 8, 15);
        Assert.Equal(new Plage(1, 2), vm.ParagraphesDeLaSelection);
        Assert.Equal(["Centrer le texte"], Allumes(fenetre));
        Rendu.Cliquer(Alignement(fenetre, "Aligner le texte à gauche"));
        Assert.Empty(bloc.Paragraphs);
        Assert.Equal(TextAlign.Left, bloc.Align);
        Assert.Equal(["Aligner le texte à gauche"], Allumes(fenetre));
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
    /// <c>Ctrl+E</c>, <c>Ctrl+R</c>, <c>Ctrl+J</c> et <c>Ctrl+L</c> en saisie alignent les
    /// paragraphes que touche la sélection — une ligne sélectionnée jusque dans la suivante les
    /// prend toutes deux —, et non le bloc ; la frappe garde son pas, la touche le sien, la saisie
    /// continue. Le menu Texte fait de même.
    /// </summary>
    [AvaloniaFact]
    public void Les_touches_et_le_menu_alignent_les_paragraphes_de_la_selection()
    {
        var bloc = Trois();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = CurseurAuClic(fenetre, vm, bloc, 0);
        Rendu.SelectionnerDuTexte(zone, 1, 8);
        Assert.Equal(new Plage(0, 2), vm.ParagraphesDeLaSelection);

        foreach (var (touche, fer) in new[]
                 {
                     (PhysicalKey.E, TextAlign.Center), (PhysicalKey.R, TextAlign.Right),
                     (PhysicalKey.J, TextAlign.Justify), (PhysicalKey.L, TextAlign.Left),
                 })
        {
            var avant = Etat(vm);
            Rendu.ToucheAzerty(fenetre, touche, RawInputModifiers.Control);
            Assert.True(zone.IsFocused, $"Ctrl+{touche} a pris le focus à la zone");
            Assert.True(bloc.IsEditing, $"Ctrl+{touche} a refermé la saisie");
            Assert.Equal(TextAlign.Left, bloc.Align);
            var effectifs = Effectifs(bloc);
            Assert.Equal([fer, fer, TextAlign.Left], effectifs.Select(e => e.Align));
            if (fer == TextAlign.Left) Assert.Empty(bloc.Paragraphs);
            else Assert.Equal((0, 2, fer), (bloc.Paragraphs.Single().Start, bloc.Paragraphs.Single().Length, bloc.Paragraphs.Single().Align));
            Assert.NotEqual(avant, Etat(vm));
        }

        // Le menu Texte, par sa commande : la même, sur la même sélection.
        vm.AlignerLeTexteCommand.Execute(TextAlign.Right);
        Rendu.Battre();
        Assert.Equal([TextAlign.Right, TextAlign.Right, TextAlign.Left], Effectifs(bloc).Select(e => e.Align));
        Assert.Equal("2 paragraphes alignés : Droite", vm.Status);

        // La frappe garde son pas, entre deux touches.
        zone.CaretIndex = bloc.Text!.Length;
        Rendu.Battre();
        Rendu.Taper(fenetre, "!");
        Assert.Equal(TroisParagraphes + "!", bloc.Text);
        var etat = Etat(vm);
        Rendu.ToucheAzerty(fenetre, PhysicalKey.E, RawInputModifiers.Control);
        Assert.Equal(TextAlign.Center, Effectifs(bloc)[2].Align);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(etat, Etat(vm));
    }

    /// <summary>
    /// La recette, en plus : un retrait saisi au dock pendant la saisie va au paragraphe du
    /// curseur, en écart, et fait un pas à lui (Q25) — la frappe d'avant a le sien, celle d'après
    /// aussi ; trois pas, défaits un à un et dans l'ordre. Le bloc garde ses réglages.
    /// </summary>
    [AvaloniaFact]
    public void Un_retrait_saisi_au_dock_va_au_paragraphe_du_curseur_et_fait_un_pas_a_lui()
    {
        var bloc = Trois();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<(string Quoi, string Etat)> { ("l'ouverture", Etat(vm)) };

        var zone = CurseurAuClic(fenetre, vm, bloc, 12);
        Rendu.Taper(fenetre, "s");
        Assert.Equal("Alpha\nMilieus\nOmega", bloc.Text);
        etats.Add(("s", Etat(vm)));

        var champ = Champ(fenetre, vm, "RetraitPremiereLigne");
        Rendu.Saisir(fenetre, champ, "5");
        Assert.True(bloc.IsEditing, "le champ du dock a refermé la saisie");
        var ecart = Assert.Single(bloc.Paragraphs);
        Assert.Equal((1, 1), (ecart.Start, ecart.Length));
        Assert.Equal(Units.MmToPoints(5), ecart.IndentFirstLine!.Value, precision: 9);
        Assert.Equal(0, bloc.IndentFirstLine);
        Assert.Equal(5m, champ.Value!.Value, 6);
        etats.Add(("retrait de 5 mm", Etat(vm)));

        CurseurAuClic(fenetre, vm, bloc, 13);
        Assert.True(zone.IsFocused, "le clic dans le texte ne lui a pas rendu le focus");
        Rendu.Taper(fenetre, "!");
        Assert.Equal("Alpha\nMilieus!\nOmega", bloc.Text);
        Assert.Equal(5m, champ.Value!.Value, 6);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        etats.Add(("!", Etat(vm)));

        for (var i = etats.Count - 1; i > 0; i--)
        {
            Assert.True(vm.CanUndo, $"il manque le pas de « {etats[i].Quoi} »");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.True(etats[i - 1].Etat == Etat(vm),
                $"annuler « {etats[i].Quoi} » ne rend pas l'état d'après « {etats[i - 1].Quoi} »");
        }

        Assert.False(vm.CanUndo, "plus de trois pas");
    }

    /// <summary>
    /// « Augmenter le retrait » et « Diminuer le retrait », au dock à côté des retraits : le
    /// retrait gauche va de taquet en taquet, de 36 pt, sans descendre sous zéro ; hors saisie sur
    /// le bloc, en saisie sur chaque paragraphe visé, chacun depuis le sien. Un pas par clic ; les
    /// boutons ne prennent pas le focus, la frappe continue au curseur.
    /// </summary>
    [AvaloniaFact]
    public void Diminuer_et_augmenter_le_retrait_vont_de_taquet_en_taquet()
    {
        var bloc = Trois();
        bloc.IndentLeft = 10;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        Champ(fenetre, vm, "RetraitAGauche");
        var augmenter = Trouver<Button>(fenetre, "AugmenterLeRetrait");
        var diminuer = Trouver<Button>(fenetre, "DiminuerLeRetrait");
        Assert.False(augmenter.Focusable || diminuer.Focusable, "un bouton de retrait prend le focus");

        var attendus = new List<double>();
        foreach (var (bouton, attendu) in new[] { (augmenter, 36.0), (augmenter, 72.0), (diminuer, 36.0), (diminuer, 0.0), (diminuer, 0.0) })
        {
            var avant = Etat(vm);
            Rendu.Cliquer(bouton);
            Assert.Equal(attendu, bloc.IndentLeft, precision: 9);
            Assert.Empty(bloc.Paragraphs);
            if (attendus.LastOrDefault(-1) != attendu) Assert.NotEqual(avant, Etat(vm));
            else Assert.Equal(avant, Etat(vm));
            attendus.Add(attendu);
        }

        // En saisie : 0, 10 et 40 pt, sélectionnés tous trois.
        bloc.IndentLeft = 0;
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 1, Length = 1, IndentLeft = 10 });
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 2, Length = 1, IndentLeft = 40 });
        var zone = CurseurAuClic(fenetre, vm, bloc, 0);
        Rendu.SelectionnerDuTexte(zone, 1, 16);
        Assert.Equal(new Plage(0, 3), vm.ParagrapheVise.Paragraphes);
        var etat = Etat(vm);

        Rendu.Cliquer(augmenter);
        Assert.Equal([36.0, 36.0, 72.0], Effectifs(bloc).Select(e => e.IndentLeft));
        Assert.Equal(0, bloc.IndentLeft);
        Assert.True(zone.IsFocused, "« Augmenter le retrait » a pris le focus à la zone");
        Assert.True(bloc.IsEditing);

        Rendu.Cliquer(diminuer);
        Rendu.Cliquer(diminuer);
        Assert.Equal([0.0, 0.0, 0.0], Effectifs(bloc).Select(e => e.IndentLeft));
        Assert.Empty(bloc.Paragraphs);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        vm.UndoCommand.Execute(null);
        vm.UndoCommand.Execute(null);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(etat, Etat(vm));
    }

    /// <summary>Le taquet suivant et le précédent, de 36 pt en 36 pt, au millième de point près.</summary>
    [Theory]
    [InlineData(0, 36, 0)]
    [InlineData(10, 36, 0)]
    [InlineData(36, 72, 0)]
    [InlineData(35.9999, 72, 0)]
    [InlineData(50, 72, 36)]
    [InlineData(72.0001, 108, 36)]
    [InlineData(-18, 0, -18)]
    public void Le_taquet_suivant_et_le_precedent(double retrait, double suivant, double precedent)
    {
        Assert.Equal(suivant, ParagrapheVise.AuTaquet(retrait, +1), precision: 9);
        Assert.Equal(precedent, ParagrapheVise.AuTaquet(retrait, -1), precision: 9);
    }

    /// <summary>
    /// La recette demandait que « les 16 paragraphes centrés des Fiches survivent à un aller-retour
    /// par l'interface ». Ils sont tous dans des cases de tableau — la colonne du milieu des bandes —,
    /// qui n'ont pas de paragraphes, et que l'import ramène au fer à gauche (voir la section R6 du
    /// plan, <i>Relevé, non corrigé</i>) : le panneau ne les voit pas. L'épreuve se joue donc sur
    /// les paragraphes justifiés d'ATA 21, l'autre document du constat de R6, qui sont dans des
    /// blocs de texte, mêlés au fer à gauche — 31 dans la source, 36 à l'import, qui donne le fer
    /// du bloc à des paragraphes à gauche sans écart (relevé aussi). Chaque bloc qui en porte
    /// s'ouvre en saisie d'un clic, le curseur passe dans chacun de ses paragraphes — la barre
    /// allume le fer du paragraphe et lui seul, le dock lit ses retraits —, puis la saisie se
    /// referme. Rien n'est écrit ; le document enregistré et relu les a tous.
    /// </summary>
    [AvaloniaFact]
    public void Les_paragraphes_justifies_d_ATA_21_survivent_a_un_aller_retour_par_l_interface()
    {
        var document = Rendu.Ata21();
        var justifies = Justifies(document);
        Assert.True(justifies >= 31, $"ATA 21 n'a que {justifies} paragraphes justifiés, 31 dans la source");
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.SectionParagrapheOuverte = true;
        var avant = Etat(vm);
        var vus = 0;

        foreach (var page in document.Pages)
        {
            vm.CurrentPage = page;
            Rendu.UneImage();
            foreach (var bloc in page.Frames.OfType<TextFrame>()
                         .Where(b => b.Visible && !b.HasField && Effectifs(b).Any(e => e.Align == TextAlign.Justify)).ToList())
            {
                var point = PointDansLeTexte(fenetre, page, bloc);
                Assert.True(point is not null, $"« {bloc.Name} » : aucun caractère qu'un clic atteigne");
                Rendu.Cliquer(fenetre, point!.Value);
                Assert.True(bloc.IsEditing, $"« {bloc.Name} » : le clic n'a pas ouvert la saisie");
                var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;

                var texte = bloc.Text ?? "";
                var effectifs = Effectifs(bloc);
                for (var p = 0; p < effectifs.Count; p++)
                {
                    zone.CaretIndex = DebutDuParagraphe(texte, p);
                    Rendu.Battre();
                    Assert.Equal(new Plage(p, 1), vm.ParagraphesDeLaSelection);
                    var allume = Allumes(fenetre);
                    Assert.True(allume.Length == 1 && allume[0] == Bulles[(int)effectifs[p].Align],
                        $"« {bloc.Name} », paragraphe {p} : la barre allume [{string.Join(", ", allume)}] pour {effectifs[p].Align}");
                    Assert.Equal(effectifs[p].IndentLeft, vm.ParagrapheVise.IndentLeft);
                    Assert.Equal(effectifs[p].IndentFirstLine, vm.ParagrapheVise.IndentFirstLine);
                    if (effectifs[p].Align == TextAlign.Justify) vus++;
                }

                Rendu.Touche(fenetre, PhysicalKey.Escape);
                vm.SetSelection([]);
                Rendu.Battre();
            }
        }

        _sortie.WriteLine($"{vus} paragraphes justifiés parcourus au curseur, sur {justifies}");
        Assert.Equal(justifies, vus);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "parcourir les paragraphes a écrit dans le document");
        Assert.Equal(justifies, Justifies(DocumentSerializer.FromJson(DocumentSerializer.ToJson(vm.Document))));
    }

    private static int Justifies(Document document) =>
        document.Pages.SelectMany(p => p.Frames).OfType<TextFrame>()
            .Sum(b => Effectifs(b).Count(e => e.Align == TextAlign.Justify));

    private static int DebutDuParagraphe(string texte, int paragraphe)
    {
        for (var i = 0; i <= texte.Length; i++)
        {
            if (Recalage.ParagrapheDe(texte, i) == paragraphe) return i;
        }

        throw new InvalidOperationException($"pas de paragraphe {paragraphe}");
    }

    /// <summary>
    /// Un point de la fenêtre sur un caractère dessiné du bloc, qu'un clic atteint : loin de la
    /// bande de bord, et sur le bloc le plus haut de la page à cet endroit. Lu sur la composition,
    /// au milieu de la bande de la ligne. Rien si aucun ne l'est.
    /// </summary>
    private static Point? PointDansLeTexte(Window fenetre, Page page, TextFrame bloc)
    {
        var geometrie = Compose(fenetre, bloc).Geometrie();
        if (geometrie is null) return null;
        var texte = bloc.Text ?? "";
        const double bord = 8;
        for (var i = 0; i < texte.Length; i++)
        {
            if (char.IsWhiteSpace(texte[i])) continue;
            var posee = geometrie.LigneDe(i);
            var x = geometrie.PointDe(i).X + 1;
            var y = (posee.Haut + posee.LigneDeBase) / 2;
            if (x < bord || y < bord || x > bloc.Width - bord || y > bloc.Height - bord) continue;
            var (px, py) = bloc.ToPage(x, y);
            if (!ReferenceEquals(bloc, page.Frames.LastOrDefault(f => f.Visible && f.HitTest(px, py)))) continue;
            return Rendu.SurLaPage(fenetre, px, py);
        }

        return null;
    }
}
