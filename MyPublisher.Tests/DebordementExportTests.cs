using System;
using System.IO;
using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Ce que l'export DIT du débordement.
///
/// <c>CompositeurTests</c> éprouve la mesure ; ces tests-ci éprouvent qu'elle est
/// employée, et surtout que l'avertissement désigne un bloc et un seul. Une liste de
/// six lignes identiques ne vaut pas mieux que le silence qu'A2 vient supprimer —
/// c'est le cas des fiches ATR, dont l'import nomme « Bande » chacune des seize bandes
/// et « L1 · C2 » la deuxième case de chacune.
///
/// Les hauteurs sont volontairement franches — un bloc trop plein du double, un bloc
/// qui tient au tiers — parce que ces tests-ci mesurent avec les vraies polices de la
/// machine, et que ce n'est pas le nombre qu'ils éprouvent.
/// </summary>
public class DebordementExportTests
{
    private static TextFrame Bloc(string nom, double hauteur, string texte) => new()
    {
        Name = nom, X = 40, Y = 40, Width = 200, Height = hauteur,
        FontFamily = "DejaVu Sans", FontSize = 11, LineHeight = 1,
        PaddingLeft = 0, PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0,
        Text = texte,
    };

    private const string Huit = "une\ndeux\ntrois\nquatre\ncinq\nsix\nsept\nhuit";

    /// <summary>Exporte une page portant ces blocs et rend les avertissements.</summary>
    private static string[] Exporter(params Frame[] blocs)
    {
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        foreach (var bloc in blocs) document.Pages[0].Frames.Add(bloc);

        var chemin = Path.Combine(Path.GetTempPath(), $"debordement-{Guid.NewGuid():N}.pdf");
        try
        {
            return [.. PdfExporter.Export(document, chemin, date: new DateTime(2026, 9, 6)).Warnings];
        }
        finally
        {
            File.Delete(chemin);
        }
    }

    [Fact]
    public void Un_bloc_trop_plein_se_signale_a_lexport()
    {
        var avertissements = Exporter(Bloc("Consigne", 30, Huit));

        var ligne = Assert.Single(avertissements);
        Assert.StartsWith("Page 1 · Consigne : ", ligne);
        Assert.Contains("déborde du bloc", ligne);
    }

    [Fact]
    public void Un_bloc_qui_tient_ne_se_signale_pas()
    {
        Assert.Empty(Exporter(Bloc("Consigne", 200, "une\ndeux")));
    }

    [Fact]
    public void Ce_qui_ne_deborde_que_dans_la_marge_interieure_basse_ne_se_signale_pas()
    {
        // Le cas de deux des neuf blocs d'ATA 21 : le texte dépasse sa zone d'une
        // fraction de point, et la marge intérieure basse — 2,88 pt chez Publisher — la
        // reçoit. Rien n'est coupé. Ici la marge basse vaut la moitié du bloc : le texte
        // sort largement de sa zone et pas du bloc.
        var bloc = Bloc("Consigne", 200, "une\ndeux\ntrois\nquatre");
        bloc.PaddingBottom = 100;

        Assert.Empty(Exporter(bloc));
    }

    [Fact]
    public void Deux_blocs_de_meme_nom_donnent_deux_avertissements_distincts()
    {
        // Sans rang, ces deux-là écriraient deux fois la même ligne, et la seconde
        // n'apprendrait rien. Le rang ne s'écrit que lorsque le nom se répète : le
        // troisième bloc, nommé autrement, n'en porte pas.
        var avertissements = Exporter(
            Bloc("Bande", 30, Huit), Bloc("Bande", 30, Huit), Bloc("Consigne", 30, Huit));

        Assert.Equal(3, avertissements.Length);
        Assert.Equal(3, avertissements.Distinct().Count());
        Assert.Contains(avertissements, a => a.StartsWith("Page 1 · Bande n° 1 : "));
        Assert.Contains(avertissements, a => a.StartsWith("Page 1 · Bande n° 2 : "));
        Assert.Contains(avertissements, a => a.StartsWith("Page 1 · Consigne : "));
    }

    [Fact]
    public void Une_case_de_tableau_trop_pleine_se_signale_par_son_rang()
    {
        // Une case est un bloc de texte comme un autre, et l'export la traite comme tel.
        // Le chemin la situe : la page, la bande, puis la case.
        var tableau = new TableFrame { Name = "Bande", X = 40, Y = 40, FontSize = 11 };
        tableau.ColumnCount = 2;
        tableau.RowCount = 1;
        tableau.Rows[0].Height = 12;
        tableau.Cells.First(c => c.Column == 1).Text = Huit;

        var ligne = Assert.Single(Exporter(tableau));

        Assert.StartsWith("Page 1 · Bande · L1 · C2 : ", ligne);
    }

    [Fact]
    public void Un_bloc_sans_nom_se_distingue_de_son_voisin_sans_nom()
    {
        // « DisplayName » retombe sur le type quand le nom est vide : deux blocs
        // anonymes donnaient deux avertissements indiscernables, et c'est ce que le
        // poste A2 avait pour consigne d'améliorer s'il en souffrait. Il en souffrait.
        var avertissements = Exporter(Bloc("", 30, Huit), Bloc("", 30, Huit));

        Assert.Equal(2, avertissements.Distinct().Count());
    }
}
