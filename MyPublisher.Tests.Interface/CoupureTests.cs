using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Couper la ligne au même mot à l'écran et au PDF — le poste Q13.
///
/// Les deux rendus composent par la même fonction du noyau, mais chacun avec sa règle à
/// mesurer : Avalonia crénèle, PDFsharp non, et une police absente ne se remplace pas
/// pareil chez l'un et chez l'autre. Avant ce poste, la coupure se décidait sur la
/// mesure de chacun : la première colonne du document d'exemple passait « jamais » à la
/// ligne dans le PDF et pas à l'écran. Elle se décide désormais sur la plus large des
/// deux. Et la mesure de l'écran ne dépend plus de ce que le processus a chargé
/// avant : les polices des documents passent par <see cref="PolicesDuDocument"/>.
///
/// On compose ici avec les DEUX vraies règles — celle d'Avalonia, qui exige la
/// plate-forme, et celle de PDFsharp, telle que l'export la construit —, sur les trois
/// documents où le défaut a été cherché : l'exemple et les deux documents ATR.
/// </summary>
public class CoupureTests
{
    // ------------------------------------------------------------------ les documents

    private static string Racine()
    {
        var dossier = new DirectoryInfo(AppContext.BaseDirectory);
        while (dossier is not null && !File.Exists(Path.Combine(dossier.FullName, "MyPublisher.sln")))
        {
            dossier = dossier.Parent;
        }

        Assert.NotNull(dossier);
        return dossier.FullName;
    }

    /// <summary>
    /// Un document ATR, importé. Le lecteur est cherché comme le cherchent la recette R5
    /// et <c>FrappeTests</c> ; introuvable, le test échoue en disant comment l'obtenir.
    /// </summary>
    private static Document Importer(string nom)
    {
        var racine = Racine();

        if (!PubReader.Disponible())
        {
            var local = Path.Combine(racine, ".outils", "mspub", "bin", "pub2raw");
            Assert.True(File.Exists(local),
                "pub2raw introuvable : « sudo apt install libmspub-tools », ou Scripts/outils-pub.sh.");

            Environment.SetEnvironmentVariable("MYPUBLISHER_PUB2RAW", local);
            var bibliotheques = Path.Combine(racine, ".outils", "mspub", "lib");
            var actuel = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
            Environment.SetEnvironmentVariable("LD_LIBRARY_PATH",
                string.IsNullOrEmpty(actuel) ? bibliotheques : $"{bibliotheques}:{actuel}");
        }

        return DocumentSerializer.Import(Path.Combine(racine, "MyPublisher", "Books", nom)).Document;
    }

    private static Document Exemple() =>
        DocumentSerializer.Load(Path.Combine(Racine(), "Samples", "exemple-bulletin.mypub"));

    /// <summary>
    /// Les trois documents, chacun avec le nombre de blocs de texte qu'il porte : un
    /// parcours qui en perdrait — un groupe, une case de tableau — passerait vert sur
    /// moins qu'il ne prétend.
    /// </summary>
    private static IEnumerable<(string Nom, Document Document, int Blocs)> Documents() =>
    [
        ("exemple-bulletin", Exemple(), 15),
        ("ATA 21", Importer("ATA 21 - Air conditioning.pub"), 9),
        ("Fiches", Importer("Fiches limitations et procédures d'urgences.pub"), 33),
    ];

    /// <summary>Tous les blocs de texte, groupes et cases de tableau compris.</summary>
    private static IEnumerable<(TextFrame Bloc, string Nom, int Page)> Textes(Document document)
    {
        IEnumerable<(TextFrame, string)> Descendre(Frame frame, string chemin)
        {
            var repere = $"{chemin} · {frame.DisplayName}";
            if (frame is TextFrame texte && texte.Text.Length > 0) yield return (texte, repere);

            var enfants = frame switch
            {
                GroupFrame groupe => groupe.Children.AsEnumerable(),
                TableFrame tableau => tableau.CellFrames,
                _ => [],
            };

            foreach (var enfant in enfants)
            {
                foreach (var trouve in Descendre(enfant, repere)) yield return trouve;
            }
        }

        return document.Pages.SelectMany((page, rang) => page.Frames
            .SelectMany(f => Descendre(f, $"Page {rang + 1}"))
            .Select(t => (t.Item1, t.Item2, rang + 1)));
    }

    // ------------------------------------------------------------------ composer

    /// <summary>
    /// Une règle qui décide seule de sa coupure : ce que faisait le dépôt avant ce poste.
    /// Elle ne sert qu'à la contre-épreuve.
    /// </summary>
    private sealed class Seule(Compositeur.IMetriques regle) : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => regle.Largeur(texte, style);
        public double LargeurDeCoupure(string texte, StyleEffectif style) => regle.Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => regle.Naturelle(style);
    }

    /// <summary>Le bloc composé comme l'écran et l'export le composent, à la règle donnée.</summary>
    private static string[] Lignes(TextFrame bloc, Compositeur.IMetriques metriques)
    {
        var commun = new StyleEffectif(
            bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic, bloc.Foreground, 0);

        return Compositeur.Composer(
                bloc.Text, bloc.Spans, commun, bloc.Paragraphs, bloc.Paragraph,
                Math.Max(1, bloc.Width - bloc.PaddingLeft - bloc.PaddingRight),
                Math.Max(1, bloc.Height - bloc.PaddingTop - bloc.PaddingBottom),
                bloc.Colonnage, bloc.LineHeight, metriques)
            .Select(ligne => ligne.Texte)
            .ToArray();
    }

    /// <summary>Les lignes de chaque bloc qui ne coupent pas pareil sous les deux règles.</summary>
    private static List<string> Ecarts(
        Document document, string nom, Compositeur.IMetriques ecran, Compositeur.IMetriques pdf)
    {
        var ecarts = new List<string>();
        foreach (var (bloc, repere, _) in Textes(document))
        {
            var a = Lignes(bloc, ecran);
            var b = Lignes(bloc, pdf);
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var la = i < a.Length ? a[i] : "(rien)";
                var lb = i < b.Length ? b[i] : "(rien)";
                if (la == lb) continue;
                ecarts.Add($"{nom} · {repere}, ligne {i + 1} : écran « {la} », PDF « {lb} »");
                break;
            }
        }

        return ecarts;
    }

    // ------------------------------------------------------------------ les tests

    [AvaloniaFact]
    [Trait("poste", "Q13")]
    public void Les_deux_regles_coupent_aux_memes_mots_sur_les_trois_documents()
    {
        var ecran = new MetriquesAvalonia();
        // La règle du PDF telle que l'export la construit : sa mesure, et celle de
        // l'écran pour décider. Seule la surface diffère — un contexte de mesure au lieu
        // d'une page —, et MetriquesPdfTests prouve qu'elle ne change rien à la mesure.
        var pdf = MetriquesPdf.HorsPage(ecran);

        var ecarts = new List<string>();
        foreach (var (nom, document, attendus) in Documents())
        {
            Assert.Equal(attendus, Textes(document).Count());
            ecarts.AddRange(Ecarts(document, nom, ecran, pdf));
        }

        Assert.True(ecarts.Count == 0, string.Join("\n", ecarts));
    }

    [AvaloniaFact]
    [Trait("poste", "Q13")]
    public void Contre_epreuve_chaque_regle_decidant_seule_coupe_l_exemple_autrement()
    {
        // Sans elle, le test précédent pourrait passer parce que les trois documents ne
        // portent aucun mot à la limite. L'exemple en porte un : « jamais », dans la
        // première colonne, que l'écran garde sur la ligne et que le PDF renvoie.
        var ecran = new MetriquesAvalonia();
        var ecarts = Ecarts(Exemple(), "exemple-bulletin",
            new Seule(ecran), new Seule(MetriquesPdf.HorsPage(ecran: null)));

        Assert.Contains(ecarts, e => e.Contains("Colonne de gauche") && e.Contains("jamais"));
    }

    [AvaloniaFact]
    [Trait("poste", "Q13")]
    public void L_italique_absent_sort_penche_et_jamais_gras_quoi_que_le_systeme_ait_charge()
    {
        // DejaVu Serif n'a pas d'italique sur la machine du projet, et aucun texte de
        // document ne passe plus par la résolution d'Avalonia : ce test est seul à la
        // toucher pour cette famille. Il y charge le GRAS d'abord — l'ordre qui, avant
        // Q13, faisait sortir l'italique en gras —, comme le ferait n'importe quel
        // contrôle du chrome.
        const string Famille = "DejaVu Serif";
        const string Texte = "Les formes acceptent remplissage, contour, ";
        StyleEffectif Style(bool gras, bool italique) => new(Famille, 10.5, gras, italique, "#FF000000", 0);

        var systeme = new Typeface(new FontFamily(Famille), FontStyle.Normal, FontWeight.Bold).GlyphTypeface;
        Assert.Equal(FontWeight.Bold, systeme.Weight);

        var regle = new MetriquesAvalonia();
        var italique = regle.Largeur(Texte, Style(false, true));
        var romain = regle.Largeur(Texte, Style(false, false));
        var gras = regle.Largeur(Texte, Style(true, false));
        Assert.NotEqual(romain, gras, 3);

        // L'italique se mesure comme le romain qu'il penche, et non comme le gras.
        Assert.Equal(romain, italique, 9);

        // Et il se DESSINE ainsi : la face que la collection rend est le romain, penché.
        var face = new Typeface(PolicesDuDocument.Famille(Famille), FontStyle.Italic, FontWeight.Normal).GlyphTypeface;
        Assert.Equal(FontWeight.Normal, face.Weight);
        Assert.Equal(FontSimulations.Oblique, face.FontSimulations);

        // Le PDF penche le même fichier : fontconfig a décidé de la simulation pour les deux.
        Assert.True(FontconfigFontResolver.Resoudre(Famille, false, true).SimulerItalique);
    }

    /// <summary>Le banc, compilé avec la solution : c'est un AUTRE processus qu'on veut.</summary>
    private static string Banc()
    {
        var chemin = Path.Combine(Racine(), "MyPublisher.Banc", "bin", "Debug", "net10.0", "MyPublisher.Banc");
        Assert.True(File.Exists(chemin),
            $"banc introuvable ({chemin}) : compiler la solution, « dotnet test » ne compile pas le banc.");
        return chemin;
    }

    /// <summary>Les lignes de l'écran relevées par le banc, dans un processus neuf.</summary>
    private static string Relever(string document, string ordre)
    {
        var sortie = Path.Combine(Path.GetTempPath(), $"mypublisher-q13-{Guid.NewGuid():N}.json");
        try
        {
            var demarrage = new ProcessStartInfo(Banc())
            {
                ArgumentList = { "mots", document, "--sortie", sortie, "--charger-d-abord", ordre },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var processus = Process.Start(demarrage)!;
            var erreurs = processus.StandardError.ReadToEndAsync();
            var compte = processus.StandardOutput.ReadToEnd();
            Assert.True(processus.WaitForExit(120_000), "le banc n'a pas fini en deux minutes");
            Assert.True(processus.ExitCode == 0, $"le banc a échoué : {erreurs.Result}");
            Assert.Contains($"faces chargées d'abord : {ordre}", compte);
            return File.ReadAllText(sortie);
        }
        finally
        {
            File.Delete(sortie);
        }
    }

    [Fact]
    [Trait("poste", "Q13")]
    public void Un_processus_qui_charge_le_gras_d_abord_mesure_comme_celui_qui_charge_le_romain()
    {
        // Trois processus, trois ordres de chargement des faces — avant la fenêtre, par
        // la résolution d'Avalonia et par la règle à mesurer —, le même document : les
        // lignes de l'écran, leurs mots et leurs places doivent être identiques au bit
        // près. Avant Q13, la légende de l'exemple y coupait autrement selon l'ordre.
        var exemple = Path.Combine(Racine(), "Samples", "exemple-bulletin.mypub");

        var romain = Relever(exemple, "romain");
        Assert.Contains("Légende", romain);
        Assert.Equal(romain, Relever(exemple, "gras"));
        Assert.Equal(romain, Relever(exemple, "italique"));
    }
}
