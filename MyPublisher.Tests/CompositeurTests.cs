using System.Linq;
using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// La découpe en lignes, éprouvée avec une règle à mesurer factice : un caractère
/// vaut une unité. Cela suffit à décrire tout ce qu'on attend d'elle, et cela
/// n'oblige ni à ouvrir une fenêtre ni à charger une police.
/// </summary>
public class CompositeurTests
{
    private static double Caracteres(string s) => s.Length;

    private static string[] Composer(string texte, double largeur) =>
        Compositeur.Composer(texte, largeur, Caracteres).Select(l => l.Texte).ToArray();

    [Fact]
    public void Un_texte_court_tient_sur_une_ligne()
    {
        Assert.Equal(["un deux"], Composer("un deux", 20));
    }

    [Fact]
    public void La_coupe_se_fait_aux_espaces()
    {
        Assert.Equal(["un deux", "trois"], Composer("un deux trois", 10));
    }

    [Fact]
    public void Les_retours_a_la_ligne_saisis_sont_respectes()
    {
        Assert.Equal(["un", "deux"], Composer("un\ndeux", 100));
    }

    [Theory]
    [InlineData("un\r\ndeux")]
    [InlineData("un\rdeux")]
    public void Les_trois_conventions_de_fin_de_ligne_se_valent(string texte)
    {
        // Un texte importé peut porter n'importe laquelle des trois ; aucune ne doit
        // laisser un caractère de contrôle dans la ligne composée.
        Assert.Equal(["un", "deux"], Composer(texte, 100));
    }

    [Fact]
    public void Une_ligne_vide_reste_une_ligne()
    {
        // Elle occupe sa hauteur : la supprimer resserrerait le texte et ferait
        // mentir la mesure de débordement.
        Assert.Equal(["un", "", "deux"], Composer("un\n\ndeux", 100));
    }

    [Fact]
    public void Un_mot_plus_large_que_la_boite_garde_sa_ligne()
    {
        // Le couper à l'aveugle ferait pire que déborder : Publisher ne le fait pas.
        Assert.Equal(["anticonstitutionnellement", "fin"], Composer("anticonstitutionnellement fin", 10));
    }

    [Fact]
    public void Aucune_ligne_ne_se_termine_par_une_espace()
    {
        var lignes = Compositeur.Composer("un deux trois quatre", 10, Caracteres);

        Assert.All(lignes, l => Assert.Equal(l.Texte.TrimEnd(), l.Texte));
    }

    [Fact]
    public void Un_texte_vide_ne_donne_aucune_ligne()
    {
        Assert.Empty(Compositeur.Composer("", 100, Caracteres));
    }

    [Theory]
    [InlineData(1.0, 13.93)]
    [InlineData(1.25, 17.4125)]
    [InlineData(1.5, 20.895)]
    public void Linterligne_est_un_multiple_de_la_hauteur_naturelle(double multiple, double attendu)
    {
        // 13,93 pt est la hauteur naturelle de DejaVu Sans à 12 pt, mesurée au poste
        // P3. Le multiple porte sur elle, jamais sur le corps : c'est ce que veut
        // dire « interligne 1,25 » dans Publisher comme ailleurs.
        Assert.Equal(attendu, Compositeur.Avance(13.93, multiple), precision: 4);
    }

    [Fact]
    public void Un_interligne_absurde_ne_fait_pas_disparaitre_le_texte()
    {
        // Un zéro empilerait toutes les lignes au même endroit ; on retombe sur 1.
        Assert.Equal(13.93, Compositeur.Avance(13.93, 0), precision: 4);
    }
}
