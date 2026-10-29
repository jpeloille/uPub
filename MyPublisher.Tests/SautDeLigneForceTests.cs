using System.Collections.Generic;
using System.Linq;
using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Le saut de ligne forcé — le poste N16 (fiche 23, a15), dans le noyau.
///
/// Publisher distingue la fin de paragraphe, Entrée, du saut de ligne forcé, Maj+Entrée : la
/// ligne est coupée, le paragraphe continue — son retrait de première ligne ne s'applique pas
/// à la ligne d'après, son espace après n'est qu'au bout du paragraphe, et le compte des
/// paragraphes ne change pas. Le saut forcé est un caractère, U+2028 (règle 15), que la
/// composition coupe sans fermer le paragraphe et que le recalage ne compte pas comme un saut.
/// </summary>
[Trait("poste", "N16")]
public class SautDeLigneForceTests
{
    /// <summary>Une police imaginaire à chasse fixe : un caractère, une unité par point de corps.</summary>
    private sealed class ChasseFixe : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => texte.Length * style.FontSize;
        public double LargeurDeCoupure(string texte, StyleEffectif style) => Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => style.FontSize;
    }

    private static readonly StyleEffectif Commun =
        new("Police", 1, Bold: false, Italic: false, Souligne.Aucun, "#FF000000", 0);

    /// <summary>Un paragraphe en retrait de première ligne, avec ses espaces avant et après.</summary>
    private static readonly ParagrapheEffectif EnRetrait = new(
        IndentLeft: 2, IndentRight: 0, IndentFirstLine: 5, SpaceBefore: 3, SpaceAfter: 7, TextAlign.Left);

    private const char Force = Compositeur.SautDeLigne;

    private static IReadOnlyList<LigneComposee> Composer(string texte, double largeur = 100,
                                                         params ParagraphSpan[] ecarts) =>
        Compositeur.Composer(texte, [], Commun, ecarts, EnRetrait, largeur, 1_000_000, new Colonnage(1, 0), 1,
            new ChasseFixe());

    [Fact]
    public void Le_saut_force_est_U_2028()
    {
        Assert.Equal('\u2028', Compositeur.SautDeLigne);
    }

    /// <summary>
    /// La recette : le saut forcé coupe la ligne sans créer de paragraphe. Deux lignes, un seul
    /// paragraphe ; la première ne le ferme pas ; le saut n'est dans aucune des deux — la
    /// première finit avant lui, la seconde commence après.
    /// </summary>
    [Fact]
    public void Le_saut_force_coupe_la_ligne_sans_fermer_le_paragraphe()
    {
        var texte = $"ab cd{Force}ef gh";
        var lignes = Composer(texte);

        Assert.Equal(2, lignes.Count);
        Assert.Equal(["ab cd", "ef gh"], lignes.Select(l => l.Texte));
        Assert.False(lignes[0].FinDeParagraphe);
        Assert.True(lignes[1].FinDeParagraphe);
        Assert.Equal((0, 5), (lignes[0].Debut, lignes[0].Fin));
        Assert.Equal((6, 11), (lignes[1].Debut, lignes[1].Fin));
        Assert.All(lignes, l => Assert.Equal(0, l.Paragraphe));
        Assert.Equal(6, lignes[1].Morceaux[0].Debut);

        Assert.Equal(1, Recalage.NombreDeParagraphes(texte));
        Assert.Equal(0, Recalage.ParagrapheDe(texte, texte.Length));
    }

    /// <summary>
    /// Le retrait de première ligne ne s'applique pas après le saut forcé, l'espace avant n'est
    /// qu'à la première ligne, l'espace après qu'à la dernière ; après un saut de paragraphe,
    /// le paragraphe suivant reprend tout cela.
    /// </summary>
    [Fact]
    public void Apres_un_saut_force_ni_retrait_de_premiere_ligne_ni_espace_de_paragraphe()
    {
        var force = Composer($"un{Force}deux");
        Assert.Equal([7.0, 2.0], force.Select(l => l.Retrait));
        Assert.Equal([3.0, 0.0], force.Select(l => l.EspaceAvant));
        Assert.Equal([0.0, 7.0], force.Select(l => l.EspaceApres));

        var paragraphe = Composer("un\ndeux");
        Assert.Equal([7.0, 7.0], paragraphe.Select(l => l.Retrait));
        Assert.Equal([3.0, 3.0], paragraphe.Select(l => l.EspaceAvant));
        Assert.Equal([7.0, 7.0], paragraphe.Select(l => l.EspaceApres));
        Assert.Equal([0, 1], paragraphe.Select(l => l.Paragraphe));
    }

    /// <summary>
    /// Les écarts de paragraphe comptent les paragraphes comme le recalage : un saut forcé
    /// dans le premier ne décale pas l'écart posé sur le deuxième.
    /// </summary>
    [Fact]
    public void Un_ecart_du_deuxieme_paragraphe_reste_au_deuxieme_apres_un_saut_force_dans_le_premier()
    {
        var centre = new ParagraphSpan { Start = 1, Length = 1, Align = TextAlign.Center };
        var lignes = Composer($"a{Force}b\nc", 100, centre);

        Assert.Equal(["a", "b", "c"], lignes.Select(l => l.Texte));
        Assert.Equal([TextAlign.Left, TextAlign.Left, TextAlign.Center], lignes.Select(l => l.Fer));
        Assert.Equal([0, 0, 1], lignes.Select(l => l.Paragraphe));
    }

    /// <summary>
    /// Les blancs avant le saut forcé sont rognés comme ceux d'une fin de ligne ; deux sauts
    /// forcés de suite font une ligne vide ; un saut forcé au bout du texte ouvre une ligne
    /// vide du même paragraphe.
    /// </summary>
    [Fact]
    public void Blancs_sauts_qui_se_suivent_et_saut_au_bout_du_texte()
    {
        var lignes = Composer($"ab  {Force}{Force}cd{Force}");
        Assert.Equal(["ab", "", "cd", ""], lignes.Select(l => l.Texte));
        Assert.Equal([(0, 4), (5, 5), (6, 8), (9, 9)], lignes.Select(l => (l.Debut, l.Fin)));
        Assert.Equal([false, false, false, true], lignes.Select(l => l.FinDeParagraphe));
        Assert.All(lignes, l => Assert.Equal(0, l.Paragraphe));
    }

    /// <summary>
    /// Un clic à droite d'une ligne coupée par un saut forcé pose le curseur avant le saut, au
    /// bout de ce que la ligne porte, et non au début de la suivante ; la position après le saut
    /// est à l'origine de la ligne suivante.
    /// </summary>
    [Fact]
    public void Le_curseur_a_droite_d_une_ligne_forcee_se_pose_avant_le_saut()
    {
        var texte = $"ab{Force}cd";
        var lignes = Composer(texte);
        var metriques = new ChasseFixe();
        var zeros = new double[lignes[0].Morceaux.Count];

        Assert.Equal(2, Compositeur.PositionLaPlusProche(lignes[0], zeros, 50, texte, Commun, metriques));
        Assert.Equal(0, Compositeur.Abscisse(lignes[1], new double[lignes[1].Morceaux.Count], 3, texte, Commun, metriques));
    }

    /// <summary>
    /// Le saut forcé tapé — U+2028 inséré — ne déplace aucun écart de paragraphe : il n'en
    /// crée pas. Un saut de paragraphe tapé au même endroit décale l'écart du suivant.
    /// </summary>
    [Fact]
    public void Taper_un_saut_force_ne_decale_aucun_ecart()
    {
        var deuxieme = new Plage(1, 1);
        var (_, ecarts) = Recalage.Recaler("un deux\ntrois", $"un{Force}deux\ntrois", [], [deuxieme]);
        Assert.Equal(deuxieme, ecarts.Single());

        (_, ecarts) = Recalage.Recaler("un deux\ntrois", "un\ndeux\ntrois", [], [deuxieme]);
        Assert.Equal(new Plage(2, 1), ecarts.Single());
    }

    /// <summary>
    /// Le saut forcé tapé prend le style de ce qui le précède, comme un caractère du paragraphe :
    /// il n'ouvre pas de paragraphe, où il prendrait celui de ce qui le suit.
    /// </summary>
    [Fact]
    public void Taper_un_saut_force_allonge_le_fragment_qui_le_precede()
    {
        var (fragments, _) = Recalage.Recaler("abc def", $"abc{Force}def", [new Plage(0, 3)], []);
        Assert.Equal(new Plage(0, 3), fragments.Single());

        (fragments, _) = Recalage.Recaler("abcdef", $"abc{Force}def", [new Plage(0, 3)], []);
        Assert.Equal(new Plage(0, 4), fragments.Single());
    }
}
