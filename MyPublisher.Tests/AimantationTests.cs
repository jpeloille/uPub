using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// L'aimantation, éprouvée directement.
///
/// La recette de C2a disait : « quatre blocs lâchés à des endroits différents à moins
/// de 4 pt du bord de la deuxième colonne s'y posent tous exactement ». Elle a été
/// jouée en synthétisant des pointeurs sur la vraie couche de saisie, puis jetée. Ce
/// qu'elle éprouvait tient ici en quelques assertions — et le plan le disait
/// lui-même : « une position posée à la souris ne vaut que ce que vaut le pointeur ;
/// ce qui est exact, ce sont les positions aimantées ».
/// </summary>
public class AimantationTests
{
    private static readonly double[] Colonnes = [56.69, 187.09, 218.19, 348.59];

    [Theory]
    // Quatre positions différentes, toutes à moins de 4 pt du bord de la deuxième
    // colonne : elles doivent toutes se poser exactement dessus.
    [InlineData(215.0)]
    [InlineData(219.5)]
    [InlineData(221.0)]
    [InlineData(218.19)]
    public void Un_bord_proche_dune_colonne_sy_pose_exactement(double depart)
    {
        var pose = Snap.Edge(depart, size: 100, tolerance: 4, Colonnes);

        Assert.Equal(218.19, pose, precision: 10);
    }

    [Fact]
    public void Hors_tolerance_rien_ne_bouge()
    {
        var depart = 218.19 + 5;

        Assert.Equal(depart, Snap.Edge(depart, size: 100, tolerance: 4, Colonnes), precision: 10);
    }

    [Fact]
    public void Sans_repere_rien_ne_bouge()
    {
        Assert.Equal(123.45, Snap.Edge(123.45, size: 50, tolerance: 4, []), precision: 10);
    }

    [Fact]
    public void Le_bord_de_fin_saimante_aussi()
    {
        // Le bloc fait 100 pt de large ; son bord droit tombe à 2 pt de la colonne
        // à 187,09. C'est donc l'origine qui doit reculer, pas le bord gauche qui
        // doit s'accrocher.
        var depart = 187.09 - 100 + 2;

        var pose = Snap.Edge(depart, size: 100, tolerance: 4, Colonnes);

        Assert.Equal(187.09 - 100, pose, precision: 10);
    }

    [Fact]
    public void Le_milieu_saimante_aussi()
    {
        var depart = 218.19 - 50 + 1;

        var pose = Snap.Edge(depart, size: 100, tolerance: 4, Colonnes);

        Assert.Equal(218.19 - 50, pose, precision: 10);
    }

    [Fact]
    public void Entre_deux_reperes_cest_le_plus_proche_qui_gagne()
    {
        // À 187,09 + 1 : le bord gauche est à 1 pt de la troisième colonne et à
        // 30 pt de la quatrième.
        var pose = Snap.Edge(188.09, size: 10, tolerance: 40, Colonnes);

        Assert.Equal(187.09, pose, precision: 10);
    }

    [Fact]
    public void La_taille_minimale_dun_bloc_est_de_deux_millimetres()
    {
        // Elle était déclarée deux fois — dans la vue et, implicitement, dans le
        // panneau de propriétés — sans que rien n'impose l'accord.
        Assert.Equal(Units.MmToPoints(2), Snap.MinFrameSize, precision: 10);
    }
}
