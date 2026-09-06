using System.Collections.Generic;
using System.Linq;
using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// La composition, éprouvée avec une règle à mesurer factice : un caractère vaut une
/// unité, et la hauteur naturelle vaut le corps. Cela suffit à décrire tout ce qu'on
/// attend d'elle, et cela n'oblige ni à ouvrir une fenêtre ni à charger une police.
/// </summary>
public class CompositeurTests
{
    /// <summary>Une police imaginaire à chasse fixe : un caractère, une unité.</summary>
    private sealed class ChasseFixe : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => texte.Length * style.FontSize;
        public double Naturelle(StyleEffectif style) => style.FontSize;
    }

    private static readonly StyleEffectif Commun =
        new("Police", 1, Bold: false, Italic: false, "#FF000000", 0);

    private static IReadOnlyList<LigneComposee> Composer(
        string texte, double largeur, params StyleSpan[] fragments) =>
        Compositeur.Composer(texte, fragments, Commun, largeur, new ChasseFixe());

    private static string[] Textes(string texte, double largeur, params StyleSpan[] fragments) =>
        Composer(texte, largeur, fragments).Select(l => l.Texte).ToArray();

    // ------------------------------------------------------------------ découpe

    [Fact]
    public void Un_texte_court_tient_sur_une_ligne()
    {
        Assert.Equal(["un deux"], Textes("un deux", 20));
    }

    [Fact]
    public void La_coupe_se_fait_aux_espaces()
    {
        Assert.Equal(["un deux", "trois"], Textes("un deux trois", 10));
    }

    [Theory]
    [InlineData("un\ndeux")]
    [InlineData("un\r\ndeux")]
    [InlineData("un\rdeux")]
    public void Les_trois_conventions_de_fin_de_ligne_se_valent(string texte)
    {
        Assert.Equal(["un", "deux"], Textes(texte, 100));
    }

    [Fact]
    public void Une_ligne_vide_reste_une_ligne()
    {
        // Elle occupe sa hauteur : la supprimer resserrerait le texte et ferait
        // mentir la mesure de débordement.
        Assert.Equal(["un", "", "deux"], Textes("un\n\ndeux", 100));
    }

    [Fact]
    public void Un_mot_plus_large_que_la_boite_garde_sa_ligne()
    {
        Assert.Equal(["anticonstitutionnellement", "fin"],
            Textes("anticonstitutionnellement fin", 10));
    }

    [Fact]
    public void Aucune_ligne_ne_se_termine_par_une_espace()
    {
        Assert.All(Composer("un deux trois quatre", 10),
            l => Assert.Equal(l.Texte.TrimEnd(), l.Texte));
    }

    [Fact]
    public void Un_texte_vide_ne_donne_aucune_ligne()
    {
        Assert.Empty(Composer("", 100));
    }

    [Fact]
    public void La_derniere_ligne_dun_paragraphe_est_marquee()
    {
        var lignes = Composer("un deux trois", 10);

        Assert.False(lignes[0].FinDeParagraphe);   // « un deux », coupée par la largeur
        Assert.True(lignes[^1].FinDeParagraphe);   // « trois », fin du paragraphe
    }

    // ------------------------------------------------------- fragments de style

    [Fact]
    public void Sans_fragment_toute_la_ligne_est_du_style_du_bloc()
    {
        var ligne = Composer("un deux", 100)[0];

        Assert.All(ligne.Morceaux, m => Assert.Equal(Commun, m.Style));
    }

    [Fact]
    public void Un_fragment_change_le_style_de_sa_portion_seulement()
    {
        // « deux » en gras, au milieu d'un texte qui ne l'est pas.
        var ligne = Composer("un deux trois", 100,
            new StyleSpan { Start = 3, Length = 4, Bold = true })[0];

        var gras = ligne.Morceaux.Where(m => m.Style.Bold).Select(m => m.Texte).ToArray();
        Assert.Equal(["deux"], gras);
        Assert.DoesNotContain(ligne.Morceaux, m => m.Texte.Contains("trois") && m.Style.Bold);
    }

    [Fact]
    public void Un_fragment_nherite_que_de_ce_quil_ne_dit_pas()
    {
        // Il change le corps ; la police et la couleur restent celles du bloc.
        var ligne = Composer("un deux", 100,
            new StyleSpan { Start = 0, Length = 2, FontSize = 3 })[0];

        var premier = ligne.Morceaux[0];
        Assert.Equal(3, premier.Style.FontSize);
        Assert.Equal(Commun.FontFamily, premier.Style.FontFamily);
        Assert.Equal(Commun.Foreground, premier.Style.Foreground);
    }

    [Fact]
    public void La_ligne_prend_la_hauteur_de_son_plus_grand_style()
    {
        // Un mot en gros corps ouvre la ligne où il se trouve, et seulement là.
        var lignes = Composer("petit GROS\npetit", 100,
            new StyleSpan { Start = 6, Length = 4, FontSize = 4 });

        Assert.Equal(4, lignes[0].Naturelle, precision: 9);
        Assert.Equal(1, lignes[1].Naturelle, precision: 9);
    }

    [Fact]
    public void Un_fragment_qui_deborde_du_texte_ne_casse_rien()
    {
        // Un import peut mal compter les caractères : le document doit rester lisible.
        var ligne = Composer("court", 100,
            new StyleSpan { Start = 3, Length = 9999, Bold = true })[0];

        Assert.Equal("court", ligne.Texte);
    }

    [Fact]
    public void Un_fragment_aux_bornes_absurdes_est_ignore_sans_bruit()
    {
        var ligne = Composer("court", 100,
            new StyleSpan { Start = -5, Length = 2 },
            new StyleSpan { Start = 100, Length = 3 })[0];

        Assert.Equal("court", ligne.Texte);
    }

    [Fact]
    public void Le_dernier_fragment_pose_lemporte()
    {
        // Deux fragments qui se recouvrent : le second gagne, comme dans un éditeur.
        var ligne = Composer("abcdef", 100,
            new StyleSpan { Start = 0, Length = 6, Bold = true },
            new StyleSpan { Start = 2, Length = 2, Bold = false })[0];

        var milieu = ligne.Morceaux.First(m => m.Texte.StartsWith("cd"));
        Assert.False(milieu.Style.Bold);
    }

    [Fact]
    public void Les_morceaux_voisins_de_meme_style_sont_recolles()
    {
        // Deux fragments identiques collés ne doivent pas produire deux morceaux :
        // le rendu en ferait deux appels de dessin, et la justification compterait
        // un blanc de trop.
        var ligne = Composer("abcdef", 100,
            new StyleSpan { Start = 0, Length = 3, Bold = true },
            new StyleSpan { Start = 3, Length = 3, Bold = true })[0];

        Assert.Single(ligne.Morceaux);
    }

    // ---------------------------------------------------------------- interligne

    [Theory]
    [InlineData(1.0, 13.93)]
    [InlineData(1.25, 17.4125)]
    [InlineData(1.5, 20.895)]
    public void Linterligne_est_un_multiple_de_la_hauteur_naturelle(double multiple, double attendu)
    {
        // 13,93 pt est la hauteur naturelle de DejaVu Sans à 12 pt, mesurée au poste
        // P3. Le multiple porte sur elle, jamais sur le corps.
        Assert.Equal(attendu, Compositeur.Avance(13.93, multiple), precision: 4);
    }

    [Fact]
    public void Un_interligne_absurde_ne_fait_pas_disparaitre_le_texte()
    {
        Assert.Equal(13.93, Compositeur.Avance(13.93, 0), precision: 4);
    }

    [Fact]
    public void La_hauteur_totale_suit_la_hauteur_de_chaque_ligne()
    {
        var lignes = Composer("petit GROS\npetit", 100,
            new StyleSpan { Start = 6, Length = 4, FontSize = 4 });

        // Une ligne à 4, une ligne à 1, interligne 1 : cinq unités.
        Assert.Equal(5, Compositeur.Hauteur(lignes, 1), precision: 9);
    }
}
