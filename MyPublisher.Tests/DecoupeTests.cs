using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// La découpe d'un groupe et d'un tableau — poste Q5. L'écran et l'export la lisent
/// tous deux ici ; ce qui s'éprouve en pixels l'est par la recette de Q5.
/// </summary>
public class DecoupeTests
{
    [Fact]
    [Trait("poste", "Q5")]
    public void Sans_filet_la_decoupe_est_le_cadre_du_groupe()
    {
        // Le cas du relevé de C9 : un texte de 400 pt dans un groupe de 120.
        var groupe = new GroupFrame { X = 100, Y = 100, Width = 120, Height = 80 };
        groupe.Children.Add(new TextFrame { X = 0, Y = 0, Width = 400, Height = 40, Stroke = null });

        Assert.Equal(new Extent(0, 0, 120, 80),
            Decoupe.DesBlocs(groupe.Width, groupe.Height, groupe.Children));
    }

    [Fact]
    [Trait("poste", "Q5")]
    public void La_decoupe_laisse_passer_le_filet_d_un_bloc_pose_sur_le_bord()
    {
        // Un filet de 4 pt sort de 2 pt de son bloc : grouper ajuste le groupe aux
        // cadres, filets non compris, et une découpe au cadre en rognerait la moitié.
        var groupe = new GroupFrame { Width = 120, Height = 80 };
        groupe.Children.Add(new ShapeFrame { Width = 120, Height = 40, Stroke = "#FF0000FF", StrokeThickness = 4 });
        groupe.Children.Add(new ShapeFrame { Y = 40, Width = 60, Height = 40, Stroke = "#FF0000FF", StrokeThickness = 1 });

        Assert.Equal(new Extent(-2, -2, 122, 82),
            Decoupe.DesBlocs(groupe.Width, groupe.Height, groupe.Children));
    }

    [Theory]
    [Trait("poste", "Q5")]
    [InlineData(null, 4)]          // aucun filet
    [InlineData("#000000FF", 4)]   // filet transparent : l'export ne le trace pas
    [InlineData("#FF0000FF", 0)]   // épaisseur nulle : idem
    public void Un_filet_que_l_export_ne_trace_pas_n_elargit_rien(string? couleur, double epaisseur)
    {
        var bloc = new ShapeFrame { Width = 10, Height = 10, Stroke = couleur, StrokeThickness = epaisseur };

        Assert.Equal(0, Decoupe.DemiFilet(bloc));
    }

    [Fact]
    [Trait("poste", "Q5")]
    public void Un_groupe_imbrique_compte_les_filets_de_ses_blocs()
    {
        var interieur = new GroupFrame { Width = 50, Height = 50 };
        interieur.Children.Add(new ShapeFrame { Width = 50, Height = 50, Stroke = "#FF000000", StrokeThickness = 6 });

        Assert.Equal(3, Decoupe.Marge([interieur]));
    }

    [Fact]
    [Trait("poste", "Q5")]
    public void Un_tableau_compte_les_filets_de_ses_cases()
    {
        var tableau = new TableFrame();
        tableau.Columns.Add(new TableColumn(100));
        tableau.Columns.Add(new TableColumn(60));
        tableau.Rows.Add(new TableRow(30));
        for (var colonne = 0; colonne < 2; colonne++)
        {
            tableau.Cells.Add(new TableCell { Row = 0, Column = colonne, Stroke = "#FF404040", StrokeThickness = 2 });
        }

        Assert.Equal(2, tableau.CellFrames.Count);

        Assert.All(tableau.CellFrames, bloc => Assert.Equal(1, Decoupe.DemiFilet(bloc)));
        Assert.Equal(1, Decoupe.Marge(Decoupe.Portes(tableau)));
    }
}
