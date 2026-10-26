using System.Linq;
using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// La liste standard des corps — poste N10 —, éprouvée dans le noyau : les tailles d'Office, de 8
/// à 72 pt, que <c>Ctrl+Maj+&gt;</c> et <c>Ctrl+Maj+&lt;</c> parcourent, le pas de dix au-delà,
/// le point entier en deçà, et le point de <c>Ctrl+]</c> et <c>Ctrl+[</c>. Les touches elles-mêmes
/// sont éprouvées sur la fenêtre (<c>RaccourcisFrancaisTests</c>).
/// </summary>
[Trait("poste", "N10")]
public class CorpsStandardTests
{
    [Fact]
    public void La_liste_est_celle_d_Office_croissante_de_8_a_72()
    {
        Assert.Equal(new[] { 8, 9, 10, 10.5, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 }, CorpsStandard.Tailles);
        Assert.Equal(CorpsStandard.Tailles.OrderBy(t => t), CorpsStandard.Tailles);
    }

    [Fact]
    public void Chaque_taille_de_la_liste_mene_a_sa_voisine_et_en_revient()
    {
        var tailles = CorpsStandard.Tailles;
        for (var i = 0; i + 1 < tailles.Count; i++)
        {
            Assert.Equal(tailles[i + 1], CorpsStandard.Suivant(tailles[i]));
            Assert.Equal(tailles[i], CorpsStandard.Precedent(tailles[i + 1]));
        }
    }

    [Theory]
    [InlineData(13, 14, 12)]          // hors de la liste : ses deux voisines
    [InlineData(10.25, 10.5, 10)]
    [InlineData(72, 80, 48)]          // au bout : la dizaine suivante
    [InlineData(80, 90, 72)]
    [InlineData(75, 80, 72)]
    [InlineData(85, 90, 80)]
    [InlineData(8, 9, 7)]             // au début : le point entier précédent
    [InlineData(7.5, 8, 7)]
    [InlineData(5, 6, 4)]
    [InlineData(1, 2, 1)]             // jamais sous le minimum du champ Corps
    [InlineData(1.5, 2, 1)]
    public void Hors_de_la_liste_les_pas_sont_ceux_de_Word(double corps, double suivant, double precedent)
    {
        Assert.Equal(suivant, CorpsStandard.Suivant(corps));
        Assert.Equal(precedent, CorpsStandard.Precedent(corps));
    }

    [Theory]
    [InlineData(10.5, 1, 11.5)]
    [InlineData(10.5, -1, 9.5)]
    [InlineData(1.5, -1, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(72, 1, 73)]
    public void Ctrl_crochet_change_le_corps_d_un_point(double corps, int sens, double attendu) =>
        Assert.Equal(attendu, CorpsStandard.DUnPoint(corps, sens));

    /// <summary>Un corps lu d'un fichier, à l'arrondi près, compte pour la taille de la liste.</summary>
    [Fact]
    public void Un_corps_a_l_arrondi_pres_compte_pour_sa_taille()
    {
        Assert.Equal(12, CorpsStandard.Suivant(10.9999999));
        Assert.Equal(10.5, CorpsStandard.Precedent(11.0000001));
    }
}
