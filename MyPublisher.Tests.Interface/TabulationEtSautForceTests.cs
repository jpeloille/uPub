using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
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
/// Tabulation et saut de ligne forcé — le poste N16 (fiche 23, a15 et a16), sur la fenêtre.
///
/// Tab n'était pas inséré — <c>AcceptsTab</c> n'était pas posé — : il emmenait le focus au
/// contrôle suivant et laissait la saisie ouverte, orpheline. Entrée, Maj+Entrée et Ctrl+Entrée
/// inséraient toutes un saut de paragraphe, et le champ écrivait <c>Environment.NewLine</c>,
/// « \r\n » sous Windows. Désormais : Tab met une tabulation au curseur, que la composition
/// avance au taquet de 36 pt ; Maj+Tab ne sort plus du bloc ; Maj+Entrée pose U+2028, le saut
/// de ligne forcé, qui coupe la ligne sans créer de paragraphe ; les deux champs de texte du bloc,
/// sur la page et au dock, écrivent « \n ». Dans le dock, Tab reste la navigation entre champs.
///
/// Les gestes sont ceux du banc (<see cref="Rendu"/>, poste N1).
/// </summary>
public class TabulationEtSautForceTests
{
    // ------------------------------------------------------------------ le banc du poste

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static TextFrame Texte(string texte = "Premier\nmot suivant") => new()
    {
        Name = "Texte", X = 60, Y = 60, Width = 300, Height = 120, Text = texte,
        FontFamily = "DejaVu Sans", FontSize = 14, Foreground = "#FF000000",
    };

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    /// <summary>La géométrie du texte du bloc, telle que la planche la compose (poste N3).</summary>
    private static GeometrieDuTexte Geometrie(Window fenetre, TextFrame bloc) =>
        fenetre.GetVisualDescendants().OfType<TexteCompose>().Single(c => ReferenceEquals(c.Bloc, bloc)).Geometrie()
        ?? throw new InvalidOperationException($"« {bloc.Name} » n'a pas de géométrie");

    /// <summary>Entre en saisie et pose le curseur à <paramref name="position"/>, comme une flèche l'y mènerait.</summary>
    private static TextBox EntrerA(Window fenetre, MainWindowViewModel vm, TextFrame bloc, int position)
    {
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc, 20, 8);
        zone.CaretIndex = position;
        Rendu.Battre();
        return zone;
    }

    /// <summary>La boîte horizontale d'un mot de la première page du PDF, lue par « pdftotext -bbox ».</summary>
    private static double DebutDuMotDuPdf(string pdf, string mot)
    {
        var html = Path.ChangeExtension(pdf, ".html");
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

        var trouve = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(html),
            "<word xMin=\"([-\\d.]+)\" yMin=\"[-\\d.]+\" xMax=\"[-\\d.]+\" yMax=\"[-\\d.]+\">" + mot + "</word>");
        Assert.True(trouve.Success, $"« {mot} » n'est pas dans le PDF");
        return double.Parse(trouve.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------------ la tabulation

    /// <summary>
    /// La recette : Tab dans le 2ᵉ paragraphe met « \t » au curseur ; le mot suivant tombe au
    /// taquet de 36 pt, à l'écran comme au PDF, à 0,05 pt près ; la saisie et le focus restent
    /// dans le champ ; la frappe fait un pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N16")]
    public void Tab_dans_le_deuxieme_paragraphe_met_une_tabulation_et_le_mot_suivant_au_taquet()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);
        var zone = EntrerA(fenetre, vm, bloc, 8);

        Rendu.Touche(fenetre, PhysicalKey.Tab);

        Assert.Equal("Premier\n\tmot suivant", bloc.Text);
        Assert.Equal(9, zone.CaretIndex);
        Assert.True(bloc.IsEditing, "Tab a refermé la saisie");
        Assert.True(zone.IsFocused, "Tab a emmené le focus hors du champ");
        Assert.Same(bloc, vm.SingleSelected);

        // L'écran : le « m » de « mot », là où la composition le pose, au taquet compté depuis le
        // bord gauche du texte — la marge du bloc.
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        var taquet = bloc.X + bloc.PaddingLeft + Compositeur.IntervalleTaquet;
        var ecran = bloc.X + Geometrie(fenetre, bloc).PointDe(9).X;
        Assert.InRange(ecran, taquet - 0.05, taquet + 0.05);

        // Le PDF : le mot que pdftotext y lit commence au même taquet.
        var dossier = Path.Combine(Path.GetTempPath(), $"mypublisher-n16-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dossier);
        try
        {
            var pdf = Path.Combine(dossier, "tabulation.pdf");
            vm.ExportPdf(pdf);
            Assert.InRange(DebutDuMotDuPdf(pdf, "mot"), taquet - 0.05, taquet + 0.05);
        }
        finally
        {
            Directory.Delete(dossier, recursive: true);
        }

        Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "la tabulation tapée"));
    }

    /// <summary>
    /// Maj+Tab, en saisie, ne sort plus du bloc : il n'écrit rien — le champ, qui accepte la
    /// tabulation, en écrirait une —, et la saisie et le focus restent dans le champ.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N16")]
    public void Maj_Tab_en_saisie_ne_sort_pas_du_bloc()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = EntrerA(fenetre, vm, bloc, 8);

        Rendu.Touche(fenetre, PhysicalKey.Tab, RawInputModifiers.Shift);

        Assert.Equal("Premier\nmot suivant", bloc.Text);
        Assert.True(bloc.IsEditing, "Maj+Tab a refermé la saisie");
        Assert.True(zone.IsFocused, "Maj+Tab a emmené le focus hors du champ");
        Assert.Equal(8, zone.CaretIndex);
    }

    /// <summary>
    /// Dans le dock, Tab reste la navigation entre champs : le champ Contenu n'accepte pas la
    /// tabulation, et le focus passe au contrôle suivant.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N16")]
    public void Dans_le_dock_Tab_reste_la_navigation_entre_champs()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        Rendu.Onglet(fenetre, "Propriétés");

        var contenu = Trouver<TextBox>(fenetre, "SaisieDuTexte");
        Assert.False(contenu.AcceptsTab);
        Rendu.Cliquer(contenu);
        Assert.True(contenu.IsFocused, "le clic n'a pas donné le focus au champ Contenu");

        Rendu.Touche(fenetre, PhysicalKey.Tab);

        Assert.Equal("Premier\nmot suivant", bloc.Text);
        Assert.False(contenu.IsFocused, "Tab est resté dans le champ Contenu");
    }

    // ------------------------------------------------------------------ Entrée et Maj+Entrée

    /// <summary>
    /// La recette : Maj+Entrée coupe la ligne sans créer de paragraphe — U+2028 au curseur, le
    /// compte des paragraphes inchangé, l'écart du paragraphe suivant à sa place —, et le
    /// retrait de première ligne ne s'applique pas à la ligne d'après ; la frappe fait un pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N16")]
    public void Maj_Entree_coupe_la_ligne_sans_creer_de_paragraphe()
    {
        var bloc = Texte("Un deux\ntrois");
        bloc.IndentFirstLine = 20;
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 1, Length = 1, Align = TextAlign.Center });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);
        var zone = EntrerA(fenetre, vm, bloc, 3);

        Rendu.Touche(fenetre, PhysicalKey.Enter, RawInputModifiers.Shift);

        Assert.Equal("Un \u2028deux\ntrois", bloc.Text);
        Assert.Equal(4, zone.CaretIndex);
        Assert.Equal(2, Recalage.NombreDeParagraphes(bloc.Text));
        var ecart = Assert.Single(bloc.Paragraphs);
        Assert.Equal((1, 1, TextAlign.Center), (ecart.Start, ecart.Length, ecart.Align));
        Assert.True(bloc.IsEditing);
        Rendu.Touche(fenetre, PhysicalKey.Escape);

        // La composition à l'écran : trois lignes, deux paragraphes ; la première en retrait, la
        // deuxième — après le saut forcé — au bord, la troisième centrée.
        var lignes = Geometrie(fenetre, bloc).Lignes;
        Assert.Equal(["Un", "deux", "trois"], lignes.Select(l => l.Ligne.Texte));
        Assert.Equal([0, 0, 1], lignes.Select(l => l.Ligne.Paragraphe));
        Assert.Equal(bloc.PaddingLeft + 20, lignes[0].Origine, precision: 6);
        Assert.Equal(bloc.PaddingLeft, lignes[1].Origine, precision: 6);
        Assert.Equal(TextAlign.Center, lignes[2].Ligne.Fer);

        Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "le saut forcé tapé"));
    }

    /// <summary>
    /// Maj+Entrée sur une sélection la remplace, comme une frappe : le saut forcé prend sa place,
    /// le curseur après lui, et le modèle de vue le suit (N15).
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N16")]
    public void Maj_Entree_sur_une_selection_la_remplace()
    {
        var bloc = Texte("Un deux trois");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = EntrerA(fenetre, vm, bloc, 2);
        zone.SelectionEnd = 8;
        Rendu.Battre();

        Rendu.Touche(fenetre, PhysicalKey.Enter, RawInputModifiers.Shift);

        Assert.Equal("Un\u2028trois", bloc.Text);
        Assert.Equal((3, 3), (zone.SelectionStart, zone.SelectionEnd));
        Assert.Equal((3, 3), (vm.SelectionDuTexte!.Debut, vm.SelectionDuTexte.Fin));
        Assert.True(zone.IsFocused);
    }

    /// <summary>
    /// Entrée, elle, crée un paragraphe, et le champ écrit « \n » — <c>NewLine</c> posé sur le
    /// champ de la page et sur le champ Contenu du dock, et non <c>Environment.NewLine</c>, qui
    /// vaut « \r\n » sous Windows. Le paragraphe neuf a son retrait de première ligne.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N16")]
    public void Entree_cree_un_paragraphe_et_les_deux_champs_ecrivent_un_saut_simple()
    {
        var bloc = Texte("Un deux");
        bloc.IndentFirstLine = 20;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = EntrerA(fenetre, vm, bloc, 3);
        Assert.Equal("\n", zone.NewLine);

        Rendu.Touche(fenetre, PhysicalKey.Enter);

        Assert.Equal("Un \ndeux", bloc.Text);
        Assert.Equal(2, Recalage.NombreDeParagraphes(bloc.Text));
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        var lignes = Geometrie(fenetre, bloc).Lignes;
        Assert.Equal([bloc.PaddingLeft + 20, bloc.PaddingLeft + 20], lignes.Select(l => Math.Round(l.Origine, 6)));

        Rendu.Onglet(fenetre, "Propriétés");
        Assert.Equal("\n", Trouver<TextBox>(fenetre, "SaisieDuTexte").NewLine);
    }
}
