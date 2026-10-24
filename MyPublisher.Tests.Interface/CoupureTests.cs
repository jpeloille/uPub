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

    /// <summary>
    /// Le bloc composé comme l'écran et l'export le composent, à la règle donnée : style
    /// et fragments résolus par la charte du document, comme les deux le font depuis C5b.
    /// </summary>
    private static string[] Lignes(TextFrame bloc, Charte charte, Compositeur.IMetriques metriques)
    {
        return Compositeur.Composer(
                bloc.Text, charte.Fragments(bloc.Spans), charte.StyleCommun(bloc), bloc.Paragraphs, bloc.Paragraph,
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
            var a = Lignes(bloc, document.Charte, ecran);
            var b = Lignes(bloc, document.Charte, pdf);
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

    // ------------------------------------------------------- l'écran plus large que le PDF

    /// <summary>
    /// Le document de la recette : deux pages de blocs aux largeurs en demi-points, où
    /// l'arrondi de mise en page faisait couper l'écran autrement (relecture de Q13), et
    /// une troisième où l'écran mesure PLUS LARGE que le PDF.
    /// </summary>
    private static Document Recette() =>
        DocumentSerializer.Load(Path.Combine(Racine(), "Samples", "recette-coupure.mypub"));

    /// <summary>Les blocs de la page 3, ceux dont la largeur tombe entre les deux mesures.</summary>
    private const string PlusLarge = "Écran plus large";

    private static string Normaliser(string texte) =>
        string.Join(" ", texte.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Les lignes que l'écran DESSINE, lues sur les vrais contrôles de la page.</summary>
    private static Dictionary<string, string[]> LignesDeLEcran(Avalonia.Controls.Window fenetre)
    {
        var regle = new MetriquesAvalonia();
        return Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(fenetre)
            .OfType<TexteCompose>()
            .Where(c => c.IsEffectivelyVisible && c.Bloc is { } b && b.Name.StartsWith(PlusLarge))
            .ToDictionary(
                c => c.Bloc!.Name,
                c => c.Poser(regle, regle)
                    .GroupBy(p => p.Ligne)
                    .OrderBy(g => g.Key)
                    .Select(g => Normaliser(string.Concat(g.Select(p => p.Texte))))
                    .ToArray());
    }

    /// <summary>
    /// Les lignes que le PDF imprime dans chaque bloc d'une page, lues par
    /// « pdftotext -bbox » : un mot appartient au bloc dont le cadre contient son centre,
    /// et une ligne réunit les mots de même sommet.
    /// </summary>
    private static Dictionary<string, string[]> LignesDuPdf(string pdf, int numero, Page page)
    {
        var html = Path.ChangeExtension(pdf, ".html");
        ProcessStartInfo demarrage;
        try
        {
            demarrage = new ProcessStartInfo("pdftotext")
            {
                ArgumentList = { "-bbox", "-f", $"{numero}", "-l", $"{numero}", pdf, html },
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

        var mots = System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(html),
                "<word xMin=\"([-\\d.]+)\" yMin=\"([-\\d.]+)\" xMax=\"([-\\d.]+)\" yMax=\"([-\\d.]+)\">(.*?)</word>")
            .Select(m => (
                X: double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                Y: double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                Xf: double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture),
                Yf: double.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture),
                Texte: System.Net.WebUtility.HtmlDecode(m.Groups[5].Value)))
            .ToList();
        File.Delete(html);

        return page.Frames.OfType<TextFrame>()
            .Where(b => b.Name.StartsWith(PlusLarge))
            .ToDictionary(
                b => b.Name,
                b => mots
                    .Where(m => b.X <= (m.X + m.Xf) / 2 && (m.X + m.Xf) / 2 <= b.X + b.Width
                                && b.Y <= (m.Y + m.Yf) / 2 && (m.Y + m.Yf) / 2 <= b.Y + b.Height)
                    .GroupBy(m => Math.Round(m.Y, 1))
                    .OrderBy(g => g.Key)
                    .Select(g => string.Join(" ", g.OrderBy(m => m.X).Select(m => m.Texte)))
                    .ToArray());
    }

    [AvaloniaFact]
    [Trait("poste", "Q13")]
    public void L_export_de_l_application_coupe_comme_l_ecran_quand_l_ecran_mesure_plus_large()
    {
        // Sur le corpus, la mesure du PDF est presque toujours la plus large : un export
        // qui oublierait celle de l'écran y passerait vert (relecture de Q13). Les blocs
        // de la page 3 sont en Noto Serif, où Avalonia crénèle « f) » PLUS LARGE que
        // PDFsharp — 16,10 pt contre 14,30 à 20 pt —, et leur largeur tombe à mi-chemin
        // des deux mesures de la ligne « Voir l'alinéa f) ». L'un d'eux tient sa police
        // du jeu du document (« scheme:heading ») : la règle doit la recevoir résolue.
        var document = Recette();

        // Vérification préalable : les largeurs de la page 3 sont calculées pour les
        // fichiers Noto Serif de cette machine. Une autre version de la police peut ne
        // plus les placer entre les deux mesures, et le test tomberait sans dire pourquoi.
        //
        // « Voir l'alinéa f) » se mesure comme le compositeur la cumule : mot par mot,
        // l'espace qui suit compris, le dernier mot sans elle. Le bloc doit tomber entre
        // la ligne que mesure le PDF seul et celle que mesure la règle de coupure — la
        // plus large des deux mesures, MOT PAR MOT —, et c'est « f) » que l'écran doit
        // mesurer plus large. En romain : 134,88 pt au PDF seul, 135,48 à l'écran seul —
        // sous les 135,78 du bloc, lui aussi —, 136,68 à la règle de coupure. C'est le
        // maximum pris mot par mot qui renvoie « f) », des deux côtés.
        var regleEcran = new MetriquesAvalonia();
        var reglePdf = MetriquesPdf.HorsPage(ecran: null);
        string[] mots = ["Voir ", "l'alinéa ", "f)"];
        foreach (var bloc in document.Pages[2].Frames.OfType<TextFrame>().Where(b => b.Name.StartsWith(PlusLarge)))
        {
            var style = document.Charte.StyleCommun(bloc);
            var utile = bloc.Width - bloc.PaddingLeft - bloc.PaddingRight;
            var pdfSeul = mots.Sum(m => reglePdf.Largeur(m, style));
            var coupure = mots.Sum(m => regleEcran.LargeurDeCoupure(m, style));
            var ecranF = regleEcran.Largeur("f)", style);
            var pdfF = reglePdf.Largeur("f)", style);
            Assert.True(pdfSeul < utile && utile < coupure && pdfF < ecranF,
                $"{bloc.Name} : la police de cette machine ne départage plus les deux mesures " +
                $"(PDF seul {pdfSeul:0.00} pt, largeur utile {utile:0.00} pt, règle de coupure {coupure:0.00} pt ; " +
                $"« f) » {pdfF:0.00} pt au PDF, {ecranF:0.00} pt à l'écran) : " +
                "recalculer les largeurs de la page 3 de recette-coupure.mypub.");
        }

        var (fenetre, vm) = Rendu.Ouvrir(document);
        var travail = Path.Combine(Path.GetTempPath(), $"mypublisher-q13-{Guid.NewGuid():N}");
        Directory.CreateDirectory(travail);
        try
        {
            vm.CurrentPage = document.Pages[2];
            Rendu.Battre();

            var ecran = LignesDeLEcran(fenetre);
            Assert.Equal(4, ecran.Count);
            // L'écran renvoie « f) » : c'est là que sa mesure dépasse celle du PDF.
            Assert.All(ecran.Values, lignes => Assert.Equal("Voir l'alinéa", lignes[0]));

            // L'export de l'application, par son modèle de vue — le geste du menu.
            var pdf = Path.Combine(travail, "application.pdf");
            vm.ExportPdf(pdf);
            var imprime = LignesDuPdf(pdf, 3, document.Pages[2]);
            foreach (var (nom, lignes) in ecran)
            {
                Assert.Equal(lignes, imprime[nom]);
            }

            // Contre-épreuve : sans la mesure de l'écran, le PDF garde « f) » sur la première
            // ligne. Sans elle, le test pourrait passer sur des blocs qu'aucune mesure ne
            // départage.
            var seul = Path.Combine(travail, "pdf-seul.pdf");
            PdfExporter.Export(document, seul, ecran: null, date: new DateTime(2026, 9, 6));
            Assert.All(LignesDuPdf(seul, 3, document.Pages[2]).Values,
                lignes => Assert.Equal("Voir l'alinéa f)", lignes[0]));
        }
        finally
        {
            fenetre.Close();
            Directory.Delete(travail, recursive: true);
        }
    }

    // ------------------------------------------------------------- la porte privée

    [Fact]
    [Trait("poste", "Q13")]
    public void La_porte_privee_se_trouve_et_son_absence_se_dit()
    {
        // Le chemin de ce processus-ci : la porte existe dans Avalonia 11.3.9.
        var (methode, motif) = PolicesDuDocument.ChercherLaPorte(PolicesDuDocument.NomDeLaPorte);
        Assert.NotNull(methode);
        Assert.Null(motif);

        // Une version qui l'aurait retirée : la recherche le dit, au lieu de lever.
        var (absente, pourquoi) = PolicesDuDocument.ChercherLaPorte("TryCreateGlyphTypefaceRetiree");
        Assert.Null(absente);
        Assert.Contains("TryCreateGlyphTypefaceRetiree", pourquoi);
    }

    private static (int Code, string Sortie, string Erreurs) Lancer(string binaire, params string[] arguments)
    {
        var demarrage = new ProcessStartInfo(binaire)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) demarrage.ArgumentList.Add(argument);
        // Comme un script le lance : sans affichage, et sans la porte privée.
        demarrage.Environment.Remove("DISPLAY");
        demarrage.Environment.Remove("WAYLAND_DISPLAY");
        demarrage.Environment[PolicesDuDocument.VariableSansPorte] = "1";

        using var processus = Process.Start(demarrage)!;
        var erreurs = processus.StandardError.ReadToEndAsync();
        var sortie = processus.StandardOutput.ReadToEnd();
        Assert.True(processus.WaitForExit(120_000), $"{Path.GetFileName(binaire)} n'a pas fini en deux minutes");
        return (processus.ExitCode, sortie, erreurs.Result);
    }

    [Fact]
    [Trait("poste", "Q13")]
    public void Sans_la_porte_privee_l_export_refuse_et_la_fenetre_se_replie()
    {
        // Avant la relecture, la porte se cherchait à chaque police : sans elle,
        // « --export » rendait un PDF SANS TEXTE avec un code nul, et l'application levait
        // au premier rendu. On la retire ici à un vrai processus, par la variable que
        // PolicesDuDocument lit au démarrage.
        var exemple = Path.Combine(Racine(), "Samples", "exemple-bulletin.mypub");
        var application = Path.Combine(Racine(), "MyPublisher", "bin", "Debug", "net10.0", "MyPublisher");
        Assert.True(File.Exists(application), $"application introuvable ({application}) : compiler la solution.");

        var travail = Path.Combine(Path.GetTempPath(), $"mypublisher-q13-{Guid.NewGuid():N}");
        Directory.CreateDirectory(travail);
        try
        {
            // L'export sans fenêtre refuse, avec un code non nul, et n'écrit rien.
            var pdf = Path.Combine(travail, "sans-porte.pdf");
            var (code, _, erreurs) = Lancer(application, exemple, "--export", pdf);
            Assert.NotEqual(0, code);
            Assert.Contains("la mesure de l'écran est indisponible", erreurs);
            Assert.False(File.Exists(pdf), "un PDF a été écrit sans la mesure de l'écran");

            // La vraie fenêtre, elle, se replie sur la résolution d'Avalonia : elle
            // s'ouvre, dessine ses lignes, et le dit une fois.
            var lignes = Path.Combine(travail, "lignes.json");
            (code, var compte, erreurs) = Lancer(Banc(), "mots", exemple, "--sortie", lignes);
            Assert.True(code == 0, $"le banc a échoué sans la porte : {erreurs}");
            Assert.Contains("Les polices des documents sont résolues par Avalonia", erreurs);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(erreurs, "Avertissement"));
            // Le compte dépend, sans la porte, de l'ordre où les faces ont été chargées
            // (voir « Mots.Ordres ») : la fenêtre d'avant le poste I2 montrait d'emblée le
            // jeu de polices dans son panneau, et en chargeait les faces avant la page —
            // 53 lignes. Le jeu est passé à l'onglet Styles, qui ne s'ouvre pas tout seul :
            // 55. Ce que le test garde, c'est que la fenêtre dessine ses lignes.
            Assert.Contains("55 ligne(s)", compte);
            Assert.Contains("Légende", File.ReadAllText(lignes));
        }
        finally
        {
            Directory.Delete(travail, recursive: true);
        }
    }
}
