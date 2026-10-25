using System;
using System.Collections.Generic;
using System.Linq;
using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Le compte des lignes en excédent (poste I9), éprouvé comme la composition l'est dans
/// <c>CompositeurTests</c> : avec une règle factice, ici au corps 10 — une ligne fait dix
/// points de haut —, pour que le seuil d'un demi-point reste ce qu'il est sur une vraie
/// page, une fraction de ligne, et non la moitié d'une.
///
/// La fonction est pure, et c'est ce qui la fait servir à l'écran comme à l'export ; ce
/// que les vraies polices en font est éprouvé du côté de l'interface, contre le PDF.
/// </summary>
[Trait("poste", "I9")]
public class ExcedentTests
{
    private sealed class Corps10 : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => texte.Length;
        public double LargeurDeCoupure(string texte, StyleEffectif style) => Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => style.FontSize;
    }

    private static readonly StyleEffectif Commun =
        new("Police", 10, Bold: false, Italic: false, Souligne.Aucun, "#FF000000", 0);

    /// <summary>Un texte d'une ligne par paragraphe, de « l1 » à « l<paramref name="n"/> ».</summary>
    private static string Lignes(int n) => string.Join("\n", Enumerable.Range(1, n).Select(i => $"l{i}"));

    /// <summary>
    /// Compose pour une zone de texte haute de <paramref name="disponible"/>. La largeur
    /// est franche : aucune ligne ne se coupe, chaque paragraphe en fait une.
    /// </summary>
    private static IReadOnlyList<LigneComposee> Composer(
        string texte, double disponible, int colonnes = 1, double interligne = 1,
        ParagrapheEffectif paragraphe = default) =>
        Compositeur.Composer(texte, [], Commun, [], paragraphe,
            100, disponible, new Colonnage(colonnes, 4), interligne, new Corps10());

    private static int Excedent(
        IReadOnlyList<LigneComposee> lignes, double disponible, double margeBasse = 0,
        VerticalAlign fer = VerticalAlign.Top) =>
        Compositeur.LignesEnExcedent(lignes, disponible, margeBasse, fer);

    [Fact]
    public void Un_bloc_de_dix_lignes_qui_en_montre_six_en_compte_quatre()
    {
        var lignes = Composer(Lignes(10), 60);

        Assert.Equal(4, Excedent(lignes, 60));
        // Et le marqueur rouge de A2 parle du même bloc.
        Assert.True(Compositeur.Deborde(lignes, 60, 0, VerticalAlign.Top));
    }

    [Fact]
    public void Un_bloc_qui_ne_deborde_pas_compte_zero()
    {
        var lignes = Composer(Lignes(6), 60);

        Assert.Equal(0, Excedent(lignes, 60));
        Assert.False(Compositeur.Deborde(lignes, 60, 0, VerticalAlign.Top));
        Assert.Equal(0, Excedent([], 60));
    }

    [Fact]
    public void Une_ligne_coupee_par_le_bord_n_est_pas_montree()
    {
        // Six lignes et demie de place : la septième n'en montre que le haut, on ne la
        // lit pas. Publisher la mettrait en excédent tout entière.
        Assert.Equal(4, Excedent(Composer(Lignes(10), 65), 65));
    }

    [Fact]
    public void Un_demi_point_de_trop_ne_compte_pas_comme_au_marqueur()
    {
        // Le même seuil que A2 : une ligne comptée fait toujours poser le marqueur.
        var lignes = Composer(Lignes(7), 69.6);
        Assert.Equal(0, Excedent(lignes, 69.6));
        Assert.False(Compositeur.Deborde(lignes, 69.6, 0, VerticalAlign.Top));

        lignes = Composer(Lignes(7), 69.4);
        Assert.Equal(1, Excedent(lignes, 69.4));
        Assert.True(Compositeur.Deborde(lignes, 69.4, 0, VerticalAlign.Top));
    }

    [Fact]
    public void La_marge_interieure_basse_montre_ce_qui_ne_sort_pas_du_bloc()
    {
        // Six lignes dans une zone de 58 : 2 points dépassent de la zone, que 3 points de
        // marge basse reçoivent, et que 1 point ne reçoit pas (C6).
        var lignes = Composer(Lignes(6), 58);

        Assert.Equal(0, Excedent(lignes, 58, margeBasse: 3));
        Assert.Equal(1, Excedent(lignes, 58, margeBasse: 1));
    }

    [Theory]
    [InlineData(VerticalAlign.Top)]
    [InlineData(VerticalAlign.Middle)]
    [InlineData(VerticalAlign.Bottom)]
    public void Le_calage_vertical_ne_change_rien_a_un_texte_trop_haut(VerticalAlign fer)
    {
        // Un texte trop haut reste calé en haut (A7) : ce qui manque manque par le bas.
        Assert.Equal(4, Excedent(Composer(Lignes(10), 60), 60, fer: fer));
    }

    [Fact]
    public void Un_texte_cale_en_bas_qui_tient_ne_compte_rien_malgre_la_marge()
    {
        // Calé en bas, le texte descend jusqu'au bord de sa zone, pas au-delà.
        Assert.Equal(0, Excedent(Composer(Lignes(3), 60), 60, margeBasse: 0, fer: VerticalAlign.Bottom));
    }

    [Fact]
    public void Seule_la_derniere_colonne_laisse_couler_ce_qui_ne_tient_pas()
    {
        // Deux colonnes de trois lignes, huit lignes : la première colonne en prend trois,
        // la seconde cinq, dont deux sous le bord. Une mesure qui ne regarderait que
        // l'ordre des lignes en compterait cinq ; une qui additionnerait les colonnes,
        // toutes.
        var lignes = Composer(Lignes(8), 30, colonnes: 2);

        Assert.Equal([0, 0, 0, 1, 1, 1, 1, 1], lignes.Select(l => l.Colonne));
        Assert.Equal(2, Excedent(lignes, 30));

        // Six lignes y tiennent, trois et trois.
        Assert.Equal(0, Excedent(Composer(Lignes(6), 30, colonnes: 2), 30));
    }

    [Fact]
    public void Une_ligne_plus_haute_qu_une_colonne_n_y_est_montree_nulle_part()
    {
        // « l2 » au corps 40, dans des colonnes de 30 : sa ligne garde sa colonne — une
        // colonne vide ne se quitte pas —, mais elle en sort ; « l3 » la suit dans la
        // dernière colonne, sous le bord. Seule « l1 » se montre.
        var lignes = Compositeur.Composer("l1\nl2\nl3", [new StyleSpan { Start = 3, Length = 2, FontSize = 40 }],
            Commun, [], default, 100, 30, new Colonnage(2, 4), 1, new Corps10());

        Assert.Equal([0, 1, 1], lignes.Select(l => l.Colonne));
        Assert.Equal(2, Excedent(lignes, 30));
    }

    [Fact]
    public void Le_demi_interligne_sous_la_derniere_ligne_n_est_pas_de_l_encre()
    {
        // Interligne 1,5 : chaque ligne avance de 15 et se dessine au milieu, de 2,5 à
        // 12,5 sous son sommet. Trois lignes dans 43 : la troisième se dessine jusqu'à 42,5
        // et se montre, alors que son avance va jusqu'à 45. La mesure de A2 s'arrête au
        // bas de l'avance : le marqueur se pose, et aucune ligne ne manque.
        var lignes = Composer(Lignes(3), 43, interligne: 1.5);

        Assert.Equal(0, Excedent(lignes, 43));
        Assert.True(Compositeur.Deborde(lignes, 43, 0, VerticalAlign.Top));

        // Deux points plus bas, son encre est coupée : elle manque.
        Assert.Equal(1, Excedent(Composer(Lignes(3), 41, interligne: 1.5), 41));
    }

    [Fact]
    public void L_espace_apres_le_dernier_paragraphe_n_est_pas_une_ligne()
    {
        // Six lignes dans soixante points, et dix points d'espace après le paragraphe :
        // la mesure de A2 compte cet espace sous la dernière ligne, le compte des lignes
        // non. Le marqueur se pose, et zéro ne s'annonce pas.
        var paragraphe = new ParagrapheEffectif(0, 0, 0, 0, 10, TextAlign.Left);
        // Un seul paragraphe, coupé en six lignes à la largeur de trois caractères.
        var lignes = Compositeur.Composer("l1 l2 l3 l4 l5 l6", [], Commun, [], paragraphe,
            3, 60, new Colonnage(1, 0), 1, new Corps10());

        Assert.Equal(6, lignes.Count);
        Assert.True(Compositeur.Deborde(lignes, 60, 0, VerticalAlign.Top));
        Assert.Equal(0, Excedent(lignes, 60));
    }

    [Fact]
    public void Une_ligne_comptee_fait_toujours_poser_le_marqueur()
    {
        // Sur toutes les hauteurs, à trois interlignes et trois calages : le compte ne
        // signale jamais un bloc que le marqueur laisse en paix. Et à l'interligne 1, sans
        // espace après, l'avance est l'encre : le marqueur ne se pose alors jamais sur un
        // bloc dont aucune ligne ne manque.
        foreach (var interligne in new[] { 1.0, 1.15, 1.5 })
        foreach (var fer in Enum.GetValues<VerticalAlign>())
        for (var hauteur = 1.0; hauteur <= 120; hauteur += 0.25)
        {
            var lignes = Composer(Lignes(9), hauteur, interligne: interligne);
            var compte = Excedent(lignes, hauteur, fer: fer);
            var deborde = Compositeur.Deborde(lignes, hauteur, 0, fer);

            Assert.True(compte == 0 || deborde, $"interligne {interligne}, {fer}, hauteur {hauteur} : {compte} ligne(s) sans marqueur");
            if (interligne == 1)
                Assert.True(compte > 0 == deborde, $"{fer}, hauteur {hauteur} : {compte} ligne(s), marqueur {deborde}");
        }
    }
}
