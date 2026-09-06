using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Le bloc tableau — poste C9. Ce que ces tests gardent, c'est la règle qui tient tout
/// le reste : <b>les colonnes et les lignes font la taille du bloc</b>. Elle a deux
/// sens, et chacun peut casser sans bruit — un tableau redimensionné dont les cases ne
/// suivent pas, ou une colonne élargie qui déborde de son bloc.
/// </summary>
public class TableauTests
{
    /// <summary>Une bande à trois cases, comme seize des dix-neuf tableaux des fiches ATR.</summary>
    private static TableFrame Bande(params double[] largeurs)
    {
        var table = new TableFrame { Name = "Bande", X = 40, Y = 60 };
        foreach (var largeur in largeurs) table.Columns.Add(new TableColumn(largeur));
        table.Rows.Add(new TableRow(30));

        for (var colonne = 0; colonne < largeurs.Length; colonne++)
        {
            table.Cells.Add(new TableCell
            {
                Row = 0, Column = colonne, Text = $"case {colonne + 1}",
                Stroke = "#FF404040", StrokeThickness = 1
            });
        }

        return table;
    }

    [Fact]
    public void Les_cases_se_posent_aux_bords_de_colonnes()
    {
        var table = Bande(100, 60, 140);

        var cases = table.CellFrames;

        Assert.Equal(3, cases.Count);
        Assert.Equal([0, 100, 160], cases.Select(c => c.X));
        Assert.Equal([100, 60, 140], cases.Select(c => c.Width));
        Assert.All(cases, c => Assert.Equal(0, c.Y));
        Assert.All(cases, c => Assert.Equal(30, c.Height));
    }

    [Fact]
    public void Les_colonnes_et_les_lignes_font_la_taille_du_bloc()
    {
        // Le bloc n'a pas de taille propre : c'est ce qui rend vrai le chiffre affiché
        // dans le panneau. Sans cela, une colonne élargie déborderait de son cadre.
        var table = Bande(100, 60, 140);

        Assert.Equal(300, table.Width);
        Assert.Equal(30, table.Height);

        table.Columns[1].Width = 160;
        Assert.Equal(400, table.Width);

        table.Rows[0].Height = 44;
        Assert.Equal(44, table.Height);
    }

    [Fact]
    public void Redimensionner_le_tableau_mesure_ses_colonnes_a_la_meme_aune()
    {
        // Ce qu'un groupe fait de ses blocs, le tableau le fait de ses colonnes : c'est
        // ce qui permet de le redimensionner à la poignée, et de le mettre à l'échelle
        // quand il est pris dans un groupe.
        var table = Bande(100, 60, 140);

        table.Width = 600;

        Assert.Equal([200, 120, 280], table.Columns.Select(c => c.Width));
        Assert.Equal(600, table.Width);

        table.Height = 60;
        Assert.Equal(60, table.Rows[0].Height);
    }

    [Fact]
    public void Un_tableau_reduit_a_rien_ne_dechire_pas_ses_colonnes()
    {
        // Le même garde-fou que celui des groupes : un facteur nul ou négatif rendrait
        // des largeurs nulles, et une largeur nulle se propage ensuite en NaN.
        var table = Bande(100, 60, 140);

        table.Width = 0;

        Assert.Equal([100, 60, 140], table.Columns.Select(c => c.Width));
    }

    [Fact]
    public void Ajouter_une_colonne_cree_une_case_par_ligne_et_reprend_son_habillage()
    {
        var table = Bande(100, 100);
        table.Rows.Add(new TableRow(30));
        table.Cells.Add(new TableCell { Row = 1, Column = 0, Stroke = "#FF404040" });
        table.Cells.Add(new TableCell { Row = 1, Column = 1, Stroke = "#FF404040" });

        table.ColumnCount = 3;

        Assert.Equal(6, table.Cells.Count);
        Assert.Equal(6, table.CellFrames.Count);
        // Une case neuve sans contour ouvrirait un trou dans une bande encadrée.
        Assert.All(table.Cells, cellule => Assert.Equal("#FF404040", cellule.Stroke));
        // Rangées ligne par ligne : le fichier se lit dans l'ordre où on pense.
        Assert.Equal([(0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2)],
            table.Cells.Select(c => (c.Row, c.Column)));
    }

    [Fact]
    public void Ajouter_une_ligne_cree_une_case_par_colonne_et_reprend_son_habillage()
    {
        // Le chemin qu'on suit en composant une fiche : une bande encadrée, puis une
        // ligne de plus. Les cases neuves n'ont pas de voisine sur leur propre ligne —
        // elle n'existe pas encore — et vont donc chercher celle d'à côté.
        var table = Bande(100, 60, 140);

        table.RowCount = 2;

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(6, table.Cells.Count);
        Assert.Equal(60, table.Height);
        Assert.All(table.Cells, cellule => Assert.Equal("#FF404040", cellule.Stroke));
        Assert.Equal([(0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2)],
            table.Cells.Select(c => (c.Row, c.Column)));
        // La ligne neuve reprend la hauteur de celles qui l'entourent.
        Assert.Equal(30, table.Rows[1].Height);
    }

    [Fact]
    public void Retirer_une_colonne_emporte_ses_cases()
    {
        var table = Bande(100, 60, 140);

        table.ColumnCount = 2;

        Assert.Equal(2, table.Columns.Count);
        Assert.Equal(2, table.Cells.Count);
        Assert.Equal(160, table.Width);
        Assert.DoesNotContain(table.Cells, cellule => cellule.Column == 2);
    }

    [Fact]
    public void Une_cellule_mal_reperee_est_ignoree_plutot_que_refusee()
    {
        // Même parti pris que les fragments de style de R1 : un document ne doit pas
        // devenir illisible parce qu'un import a mal compté ses lignes.
        var table = Bande(100, 100);
        table.Cells.Add(new TableCell { Row = 0, Column = 7, Text = "hors grille" });
        table.Cells.Add(new TableCell { Row = -1, Column = 0, Text = "avant la première ligne" });
        table.Cells.Add(new TableCell { Row = 0, Column = 0, Text = "doublon" });

        var cases = table.CellFrames;

        Assert.Equal(2, cases.Count);
        // La première posée sur un croisement l'emporte, comme dans un éditeur.
        Assert.Equal("case 1", ((TextFrame)cases[0]).Text);
    }

    [Fact]
    public void Le_style_du_tableau_habille_ses_cases()
    {
        var table = Bande(100, 100);
        table.FontFamily = "DejaVu Serif";
        table.FontSize = 13;
        table.Bold = true;
        table.CellPadding = 5;
        table.Cells[1].Align = TextAlign.Right;

        var cases = table.CellFrames.Cast<TextFrame>().ToList();

        Assert.All(cases, c => Assert.Equal("DejaVu Serif", c.FontFamily));
        Assert.All(cases, c => Assert.Equal(13, c.FontSize));
        Assert.All(cases, c => Assert.True(c.Bold));
        Assert.All(cases, c => Assert.Equal(new Insets(5, 5, 5, 5), c.Padding));
        Assert.Equal(TextAlign.Left, cases[0].Align);
        Assert.Equal(TextAlign.Right, cases[1].Align);
    }

    [Fact]
    public void La_grille_habille_toutes_les_cases_dun_seul_geste()
    {
        var table = Bande(100, 100);

        table.HasGrid = false;
        Assert.All(table.Cells, cellule => Assert.Null(cellule.Stroke));

        table.HasGrid = true;
        table.GridColor = "#FF112233";
        table.GridThickness = 2;

        Assert.All(table.Cells, cellule => Assert.Equal("#FF112233", cellule.Stroke));
        Assert.All(table.Cells, cellule => Assert.Equal(2, cellule.StrokeThickness));
    }

    [Fact]
    public void Un_document_a_tableau_se_relit_a_lidentique()
    {
        var document = Document.CreateDefault();
        var table = Bande(100, 60, 140);
        table.Cells[1].Spans.Add(new StyleSpan { Start = 0, Length = 4, Bold = true });
        table.Cells[2].Fill = new SolidFill("#FFEFF2F6");
        document.Pages[0].Frames.Add(table);

        var json = DocumentSerializer.ToJson(document);
        var relu = DocumentSerializer.FromJson(json);

        Assert.Equal(json, DocumentSerializer.ToJson(relu));

        var repris = Assert.IsType<TableFrame>(relu.Pages[0].Frames[0]);
        Assert.Equal(300, repris.Width);
        Assert.Equal(3, repris.Cells.Count);
        Assert.Single(repris.Cells[1].Spans);
    }

    [Fact]
    public void La_largeur_du_bloc_sécrit_avant_ses_colonnes()
    {
        // Le piège des groupes, mot pour mot : relire les colonnes avant la largeur les
        // mettrait à l'échelle du rapport entre la taille par défaut d'un bloc et la
        // leur. L'ordre d'écriture est ce qui garantit qu'on ne le rencontre jamais sur
        // un fichier écrit par l'application.
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Add(Bande(100, 60, 140));

        var json = DocumentSerializer.ToJson(document);

        Assert.True(json.IndexOf("\"Width\"", System.StringComparison.Ordinal)
                    < json.IndexOf("\"Columns\"", System.StringComparison.Ordinal));
        Assert.True(json.IndexOf("\"Height\"", System.StringComparison.Ordinal)
                    < json.IndexOf("\"Rows\"", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Une_copie_de_tableau_emporte_ses_cases_sans_les_partager()
    {
        var table = Bande(100, 60, 140);
        table.Cells[0].Spans.Add(new StyleSpan { Start = 0, Length = 4, Bold = true });

        var copie = Assert.IsType<TableFrame>(table.Clone());

        Assert.Equal(300, copie.Width);
        Assert.Equal([100, 60, 140], copie.Columns.Select(c => c.Width));
        Assert.Equal(3, copie.Cells.Count);

        // Une copie de surface partagerait les cases : retoucher la copie modifierait
        // l'original, et le tableau dupliqué changerait avec lui.
        Assert.NotSame(table.Cells[0], copie.Cells[0]);
        Assert.NotSame(table.Columns[0], copie.Columns[0]);
        Assert.NotSame(table.Cells[0].Spans[0], copie.Cells[0].Spans[0]);

        copie.Cells[0].Text = "autre chose";
        copie.Columns[0].Width = 500;
        Assert.Equal("case 1", table.Cells[0].Text);
        Assert.Equal(300, table.Width);
    }

    [Fact]
    public void Agrandir_un_tableau_ne_reconstruit_la_grille_quune_fois()
    {
        // Trouvé par la relecture de C9 : chaque case insérée faisait renaître toutes
        // les autres, et le panneau les relisait à chaque notification. Passer à
        // 40 × 40 — deux valeurs que le champ accepte — figeait l'application deux
        // minutes et demie pour 1 600 cases. Ce n'est pas une question de vitesse mais
        // de nombre : on compte les notifications, pas les millisecondes, pour que le
        // test dise la même chose sur une machine lente.
        var tableau = new TableFrame { X = 0, Y = 0, Width = 300, Height = 100 };
        tableau.ColumnCount = 3;
        tableau.RowCount = 1;

        var reconstructions = 0;
        tableau.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TableFrame.CellFrames)) reconstructions++;
        };

        tableau.ColumnCount = 20;

        Assert.Equal(20, tableau.ColumnCount);
        Assert.Equal(20, tableau.CellFrames.Count);
        // Une notification pour les vingt colonnes, et non une par case.
        Assert.Equal(1, reconstructions);
    }

    [Fact]
    public void Un_tableau_de_quarante_sur_quarante_reste_praticable()
    {
        // Le plafond que le panneau offre, poussé à bout. Mille six cents cases, et le
        // compte de reconstructions doit rester celui de deux gestes.
        var tableau = new TableFrame { X = 0, Y = 0, Width = 300, Height = 100 };

        var reconstructions = 0;
        tableau.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TableFrame.CellFrames)) reconstructions++;
        };

        tableau.RowCount = 40;
        tableau.ColumnCount = 40;

        Assert.Equal(1600, tableau.CellFrames.Count);
        Assert.Equal(2, reconstructions);
    }

}
