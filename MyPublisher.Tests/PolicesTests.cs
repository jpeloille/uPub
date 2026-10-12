using System;
using System.IO;
using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Les substitutions de polices — poste B5.
///
/// Ce qui est éprouvé ici tient en une phrase : une police absente doit se dire, une
/// police présente doit se taire. Le second point n'est pas le moins important. Un
/// avertissement qui crie au loup sur des polices installées s'apprend à ne plus être
/// lu, et le jour où une flèche de procédure d'urgence est vraiment remplacée par une
/// lettre, personne ne regarde.
/// </summary>
public class PolicesTests
{
    /// <summary>
    /// Une famille qu'aucune machine ne porte. Ce n'est pas une supposition
    /// commode : c'est le seul moyen d'éprouver la substitution sans dépendre de ce
    /// qui est installé là où les tests tournent.
    /// </summary>
    private const string Absente = "Police Absente De Cette Machine";

    /// <summary>La seule police que tout le dépôt suppose présente : c'est celle du repli.</summary>
    private const string Presente = "DejaVu Sans";

    // ------------------------------------------------- la comparaison des familles

    [Theory]
    // fontconfig rend tous les noms de la famille, séparés par des virgules. Ces
    // trois-là sont relevés sur la machine de développement, « fc-list -f %{family} ».
    [InlineData("DejaVu Sans,DejaVu Sans Condensed", "DejaVu Sans Condensed")]
    [InlineData("Lato,Lato Semibold", "Lato Semibold")]
    [InlineData("Malgun Gothic,맑은 고딕", "Malgun Gothic")]
    [InlineData("Wingdings", "Wingdings")]
    // Les fonderies ne s'accordent pas sur la casse ; fontconfig, lui, s'en moque.
    [InlineData("Wingdings 3", "wingdings 3")]
    public void Une_famille_annoncee_par_fontconfig_est_reconnue(string rendues, string demandee)
    {
        Assert.True(FontconfigFontResolver.Correspond(rendues, demandee));
    }

    [Theory]
    [InlineData("Noto Sans", "Verdana Pro Cond SemiBold")]
    [InlineData("DejaVu Sans,DejaVu Sans Condensed", "DejaVu Serif")]
    [InlineData("", "Wingdings")]
    public void Une_famille_que_fontconfig_n_annonce_pas_est_une_substitution(string rendues, string demandee)
    {
        Assert.False(FontconfigFontResolver.Correspond(rendues, demandee));
    }

    // ------------------------------------------------------------- la résolution

    [Fact]
    public void Une_police_installee_se_resout_sans_ecart()
    {
        Assert.True(FontconfigFontResolver.Resoudre(Presente, false, false).Exacte);
    }

    [Fact]
    public void Le_gras_d_une_police_installee_reste_la_meme_famille()
    {
        // Le gras se demande à fontconfig par un poids, et rend un autre fichier :
        // c'est la famille qu'on compare, pas le fichier.
        Assert.True(FontconfigFontResolver.Resoudre(Presente, true, false).Exacte);
    }

    [Fact]
    public void Une_famille_aux_noms_multiples_se_resout_sans_ecart()
    {
        // Les cas ci-dessus éprouvent « Correspond » sur des chaînes figées, ce qui
        // laisse passer une régression où « Match » cesserait de l'appeler pour
        // comparer les chaînes lui-même : les polices du dépôt n'ont qu'un nom, et
        // rien ne s'en apercevrait. Celui-ci va donc chercher sur la machine une
        // famille que fontconfig annonce sous plusieurs noms, et traverse tout le
        // chemin de résolution.
        var famille = FamilleAuxNomsMultiples();

        // Machine sans police à noms multiples : il n'y a rien à éprouver ici, et
        // inventer un échec ne dirait rien de vrai. La recette du poste fait la même
        // réserve, et le dit.
        if (famille is null) return;

        Assert.True(FontconfigFontResolver.Resoudre(famille, false, false).Exacte, famille);
    }

    [Fact]
    public void Une_police_absente_nomme_celle_qui_la_remplace()
    {
        var resolution = FontconfigFontResolver.Resoudre(Absente, false, false);

        Assert.False(resolution.Exacte);
        Assert.Equal(Absente, resolution.Demandee);
        Assert.NotEqual("", resolution.Employee);
        Assert.NotEqual(Absente, resolution.Employee);
        Assert.NotNull(resolution.Motif);
    }

    // ------------------------------------------------------------ l'avertissement

    [Fact]
    public void L_export_nomme_la_police_demandee_et_celle_employee()
    {
        var employee = FontconfigFontResolver.Resoudre(Absente, false, false).Employee;
        var avertissements = Exporter(Document(Bloc(Absente)));

        var ligne = Assert.Single(avertissements);
        Assert.Contains(Absente, ligne);
        Assert.Contains(employee, ligne);
    }

    [Fact]
    public void Un_document_dont_les_polices_existent_n_avertit_de_rien()
    {
        Assert.Empty(Exporter(Document(Bloc(Presente))));
    }

    [Fact]
    public void Trois_blocs_dans_la_meme_police_absente_ne_font_qu_un_avertissement()
    {
        // Sur les fiches ATR, 119 fragments sont en Wingdings : un avertissement par
        // bloc en ferait une liste que personne ne lit.
        var avertissements = Exporter(Document(Bloc(Absente), Bloc(Absente), Bloc(Absente)));

        Assert.Single(avertissements);
    }

    [Fact]
    public void Un_fragment_dans_une_police_absente_se_signale_aussi()
    {
        // C'est le cas réel : le bloc est en Calibri, et seule la flèche est en
        // Wingdings. Ne regarder que la police du bloc laisserait passer 119
        // fragments sur 329.
        var bloc = Bloc(Presente);
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 3, FontFamily = Absente });

        var ligne = Assert.Single(Exporter(Document(bloc)));
        Assert.Contains(Absente, ligne);
    }

    [Fact]
    public void Un_bloc_range_dans_un_groupe_est_examine_lui_aussi()
    {
        var groupe = new GroupFrame { X = 20, Y = 20, Width = 200, Height = 80 };
        groupe.Children.Add(Bloc(Absente));

        Assert.Single(Exporter(Document(groupe)));
    }

    [Fact]
    public void L_avertissement_de_police_precede_celui_d_une_image_manquante()
    {
        // La barre d'état ne montre que le premier : c'est la police qui doit s'y
        // trouver, une image manquante se voyant à l'œil sur la page.
        var document = Document(
            Bloc(Absente),
            new ImageFrame { X = 20, Y = 200, Width = 100, Height = 100, Path = "/nulle/part/x.png" });

        var avertissements = Exporter(document);

        Assert.Equal(2, avertissements.Count);
        Assert.StartsWith("Police substituée", avertissements[0]);
    }

    [Fact]
    public void Deux_exports_de_suite_disent_la_meme_chose()
    {
        // PDFsharp garde en cache la résolution d'une police pour toute la durée du
        // processus : un relevé pris pendant le dessin serait complet au premier
        // export et vide au second.
        var document = Document(Bloc(Absente));

        Assert.Equal(Exporter(document), Exporter(document));
        Assert.Single(Exporter(document));
    }

    // ------------------------------------------------------------------ outillage

    /// <summary>
    /// Une police installée que fontconfig annonce sous plusieurs noms — « DejaVu
    /// Sans,DejaVu Sans Condensed » —, rendue par le second de ces noms ; ou
    /// <c>null</c> si la machine n'en porte aucune.
    ///
    /// On la cherche au lieu de l'écrire en dur : celles qui existent ici ne sont pas
    /// celles qui existeront ailleurs, et un test qui exige une police précise échoue
    /// pour une raison qui n'est pas celle qu'il éprouve.
    /// </summary>
    private static string? FamilleAuxNomsMultiples()
    {
        foreach (var ligne in Fontconfig("fc-list", "-f", "%{family}\n").Split('\n'))
        {
            var noms = ligne.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (noms.Length < 2) continue;

            // On redemande au système, sans passer par le code sous test : la police
            // doit vraiment être là, et dans un fichier que l'export sait lire — une
            // collection .ttc est installée et pourtant substituée.
            var rendu = Fontconfig("fc-match", "-f", "%{family}\n%{file}", $"{noms[1]}:weight=80:slant=0");
            var lignes = rendu.Split('\n');
            if (lignes.Length < 2) continue;
            if (lignes[1].Trim().EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)) continue;

            var rendues = lignes[0].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (rendues.Contains(noms[1])) return noms[1];
        }

        return null;
    }

    private static string Fontconfig(string outil, params string[] arguments)
    {
        try
        {
            var demarrage = new System.Diagnostics.ProcessStartInfo
            {
                FileName = outil, RedirectStandardOutput = true, UseShellExecute = false
            };
            foreach (var argument in arguments) demarrage.ArgumentList.Add(argument);

            using var process = System.Diagnostics.Process.Start(demarrage);
            if (process is null) return "";

            var sortie = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return sortie;
        }
        catch (Exception)
        {
            // Machine sans fontconfig : l'appelant traitera cela comme « aucune police ».
            return "";
        }
    }

    private static TextFrame Bloc(string police) => new()
    {
        X = 20, Y = 20, Width = 200, Height = 60,
        Text = "Descendre.", FontFamily = police, FontSize = 11
    };

    private static Document Document(params Frame[] blocs)
    {
        var document = new Document { Title = "Polices" };
        document.Pages.Add(new Page { Name = "Page 1" });
        foreach (var bloc in blocs) document.Pages[0].Frames.Add(bloc);
        return document;
    }

    private static System.Collections.Generic.IReadOnlyList<string> Exporter(Document document)
    {
        var chemin = Path.Combine(Path.GetTempPath(), $"b5-{Guid.NewGuid():N}.pdf");
        try
        {
            return PdfExporter.Export(document, chemin, ecran: null).Warnings.ToArray();
        }
        finally
        {
            if (File.Exists(chemin)) File.Delete(chemin);
        }
    }
}
