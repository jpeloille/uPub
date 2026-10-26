using System;
using System.Collections.Generic;
using System.Linq;
using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Chaque morceau composé sait d'où il vient — le poste N3.
///
/// La composition rendait des morceaux sans position : <c>Morceau(Texte, Style, X,
/// Largeur)</c>. Or le curseur, le clic et la sélection passent d'un point de la ligne à un
/// caractère du texte, et ce que la ligne montre n'est pas le texte : les blancs de fin de
/// ligne sont rognés, la tête blanche d'un mot renvoyé aussi, une tabulation n'a pas de
/// glyphe, et un saut n'est pas dans la ligne. Le morceau porte désormais la position de son
/// premier caractère, la ligne sa plage et son paragraphe.
///
/// Ce que les tests tiennent pour vrai, sur des textes faits pour cela : chaque morceau est
/// le texte à sa position ; les morceaux d'une ligne se suivent ; après le dernier, jusqu'au
/// bout de la ligne, il n'y a que du blanc ; les lignes se suivent et couvrent tout le texte,
/// sauts exceptés ; le rang de paragraphe est celui que compte <see cref="Recalage"/>. Puis
/// les deux fonctions pures de la géométrie : l'abscisse d'une position, et la position la
/// plus proche d'une abscisse.
/// </summary>
[Trait("poste", "N3")]
public class OrigineDesMorceauxTests
{
    /// <summary>Une police imaginaire à chasse fixe : un caractère, une unité par point de corps.</summary>
    private sealed class ChasseFixe : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => texte.Length * style.FontSize;
        public double LargeurDeCoupure(string texte, StyleEffectif style) => Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => style.FontSize;
    }

    /// <summary>
    /// Une police imaginaire où chaque caractère a sa chasse, et le gras la sienne : une
    /// abscisse fausse d'un caractère, ou mesurée dans le mauvais style, ne retombe pas
    /// par hasard sur la bonne. Une demi-paire de substitution ne mesure rien, une paire
    /// mesure pour un caractère.
    /// </summary>
    private sealed class ChasseVariable : Compositeur.IMetriques
    {
        public static double Chasse(char c, StyleEffectif style) =>
            char.IsLowSurrogate(c) ? 0 : (1 + c % 5 * 0.25) * (style.Bold ? 1.5 : 1) * style.FontSize;

        public double Largeur(string texte, StyleEffectif style) => texte.Sum(c => Chasse(c, style));
        public double LargeurDeCoupure(string texte, StyleEffectif style) => Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => style.FontSize;
    }

    private static readonly StyleEffectif Commun =
        new("Police", 1, Bold: false, Italic: false, Souligne.Aucun, "#FF000000", 0);

    private static readonly Colonnage UneColonne = new(1, 0);

    private const double Haute = 1_000_000;

    private static IReadOnlyList<LigneComposee> Composer(
        string texte, double largeur, Compositeur.IMetriques metriques, params StyleSpan[] fragments) =>
        Compositeur.Composer(texte, fragments, Commun, [], default,
            largeur, Haute, UneColonne, 1, metriques);

    private static IReadOnlyList<LigneComposee> Composer(string texte, double largeur, params StyleSpan[] fragments) =>
        Composer(texte, largeur, new ChasseFixe(), fragments);

    private static StyleSpan Gras(int debut, int longueur) => new() { Start = debut, Length = longueur, Bold = true };

    // ------------------------------------------------------------------ les textes faits pour

    /// <summary>
    /// Les textes de la recette : espaces multiples, tabulations, renvois, paragraphes vides,
    /// « \r\n », « \r » seul, paires de substitution, insécables, blancs en tête et en fin de
    /// paragraphe. Chacun est composé à toutes les largeurs de 1 à 40 — un renvoi à chaque
    /// place —, avec et sans fragments.
    /// </summary>
    public static TheoryData<string> Textes() =>
    [
        "un deux trois quatre cinq six sept huit neuf dix",
        "un  deux   trois    quatre     cinq      six",
        "a\tb\t\tc d\te\t",
        "\t\t\tanticonstitutionnellement fin",
        "   trois blancs en tête, puis du texte",
        "un\n\ndeux\n",
        "\n",
        "\n\n\n",
        "x",
        "fin de ligne   \nsuite  \n  et blancs  ",
        "un\r\ndeux\r\n\r\ntrois\r\n",
        "un\rdeux\r\rtrois\r",
        "\r\n\n\r\r\n",
        "a😀b c😀d 😀😀 e😀",
        "aaaa \u00A0bbb cc\u00A0 dd \u00A0\u00A0ee",
        "PF \t CAPT\nDC GEN 1 \t RESET\n\tDC 1(2) FAULT procedure \t APPLY",
    ];

    /// <summary>
    /// Des fragments qui coupent le texte tous les trois caractères : au milieu des mots,
    /// des suites d'espaces, d'une paire de substitution et d'un « \r\n ». La découpe en
    /// produit des unités faites de blancs seuls, qu'un renvoi rogne en tête.
    /// </summary>
    private static StyleSpan[] TousLesTrois(string texte) =>
        Enumerable.Range(0, texte.Length / 6 + 1).Select(i => Gras(i * 6, 3)).ToArray();

    [Theory]
    [MemberData(nameof(Textes))]
    public void Chaque_morceau_est_le_texte_a_sa_position_et_les_lignes_couvrent_le_texte(string texte)
    {
        for (var largeur = 1; largeur <= 40; largeur++)
        {
            Verifier(texte, Composer(texte, largeur), $"largeur {largeur}");
            Verifier(texte, Composer(texte, largeur, TousLesTrois(texte)), $"largeur {largeur}, fragments");
            Verifier(texte, Composer(texte, largeur, new ChasseVariable(), TousLesTrois(texte)),
                $"largeur {largeur}, fragments, chasse variable");
        }
    }

    [Theory]
    [MemberData(nameof(Textes))]
    public void En_colonnes_les_lignes_couvrent_encore_le_texte(string texte)
    {
        foreach (var colonnes in new[] { new Colonnage(2, 1), new Colonnage(3, 0.5) })
        {
            for (var hauteur = 1; hauteur <= 4; hauteur++)
            {
                var lignes = Compositeur.Composer(texte, TousLesTrois(texte), Commun, [], default,
                    30, hauteur, colonnes, 1, new ChasseFixe());
                Verifier(texte, lignes, $"{colonnes.Nombre} colonnes, hauteur {hauteur}");
                if (texte.Length > 0 && hauteur == 1 && lignes.Count > 1)
                {
                    Assert.Contains(lignes, l => l.Colonne > 0);
                }
            }
        }
    }

    /// <summary>
    /// Ce que le poste promet de chaque composition, écrit une fois. Le message dit le texte
    /// et la configuration : une seule largeur fautive parmi quarante se retrouve.
    /// </summary>
    private static void Verifier(string texte, IReadOnlyList<LigneComposee> lignes, string cas)
    {
        string Ou(int k) => $"« {Visible(texte)} », {cas}, ligne {k}";

        if (texte.Length == 0)
        {
            Assert.Empty(lignes);
            return;
        }

        Assert.NotEmpty(lignes);
        Assert.True(lignes[0].Debut == 0, $"{Ou(0)} : la première ligne commence à {lignes[0].Debut}");
        Assert.True(lignes[^1].Fin == texte.Length, $"{Ou(lignes.Count - 1)} : la dernière ligne finit à {lignes[^1].Fin}, le texte à {texte.Length}");
        Assert.True(lignes[^1].FinDeParagraphe, $"{Ou(lignes.Count - 1)} : la dernière ligne ne ferme pas son paragraphe");

        for (var k = 0; k < lignes.Count; k++)
        {
            var ligne = lignes[k];
            Assert.True(ligne.Debut <= ligne.Fin, $"{Ou(k)} : plage à l'envers [{ligne.Debut}, {ligne.Fin})");

            // Les morceaux : chacun est le texte à sa position, et ils se suivent depuis le
            // début de la ligne, sans trou ni chevauchement.
            var suite = ligne.Debut;
            foreach (var morceau in ligne.Morceaux)
            {
                Assert.True(morceau.Debut == suite,
                    $"{Ou(k)} : « {Visible(morceau.Texte)} » commence à {morceau.Debut}, {suite} attendu");
                Assert.True(morceau.Debut + morceau.Texte.Length <= texte.Length
                            && texte.Substring(morceau.Debut, morceau.Texte.Length) == morceau.Texte,
                    $"{Ou(k)} : « {Visible(morceau.Texte)} » n'est pas le texte à la position {morceau.Debut}");
                suite = morceau.Debut + morceau.Texte.Length;
            }

            // Après le dernier morceau, jusqu'au bout de la ligne : ce que la composition a
            // rogné, et rien que du blanc.
            Assert.True(suite <= ligne.Fin, $"{Ou(k)} : le dernier morceau finit à {suite}, après la ligne ({ligne.Fin})");
            var rogne = texte[suite..ligne.Fin];
            Assert.True(rogne.All(char.IsWhiteSpace), $"{Ou(k)} : « {Visible(rogne)} » rogné en fin de ligne n'est pas du blanc");

            // Le paragraphe : celui que compte le recalage de la frappe.
            var attendu = Recalage.Paragraphes(texte, texte, new Edition(ligne.Debut, 0, 0)).Paragraphe;
            Assert.True(ligne.Paragraphe == attendu, $"{Ou(k)} : paragraphe {ligne.Paragraphe}, {attendu} pour Recalage");

            if (k + 1 == lignes.Count) continue;
            var suivante = lignes[k + 1];
            if (ligne.FinDeParagraphe)
            {
                // Entre deux paragraphes, un saut et un seul : « \n », « \r » ou « \r\n ».
                var saut = texte[ligne.Fin..suivante.Debut];
                Assert.True(saut is "\n" or "\r" or "\r\n", $"{Ou(k)} : « {Visible(saut)} » entre deux paragraphes");
                Assert.True(suivante.Paragraphe == ligne.Paragraphe + 1, $"{Ou(k)} : le paragraphe suivant n'est pas le suivant");
            }
            else
            {
                Assert.True(suivante.Debut == ligne.Fin, $"{Ou(k)} : la ligne renvoyée finit à {ligne.Fin}, la suivante commence à {suivante.Debut}");
                Assert.True(suivante.Paragraphe == ligne.Paragraphe, $"{Ou(k)} : un renvoi change de paragraphe");
            }
        }

        var paragraphes = Recalage.Paragraphes(texte, texte, new Edition(texte.Length, 0, 0)).Paragraphe + 1;
        Assert.True(lignes[^1].Paragraphe + 1 == paragraphes,
            $"« {Visible(texte)} », {cas} : {lignes[^1].Paragraphe + 1} paragraphes composés, {paragraphes} pour Recalage");
    }

    private static string Visible(string texte) =>
        texte.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\u00A0", "\\u00A0");

    // ------------------------------------------------------------------ les positions, une à une

    [Fact]
    public void Une_ligne_renvoyee_garde_son_blanc_de_fin_et_la_suivante_commence_au_mot()
    {
        var lignes = Composer("un deux trois", 7);

        Assert.Equal(["un deux", "trois"], lignes.Select(l => l.Texte));
        Assert.Equal((0, 8), (lignes[0].Debut, lignes[0].Fin));
        Assert.Equal((8, 13), (lignes[1].Debut, lignes[1].Fin));
        Assert.Equal([0, 3], lignes[0].Morceaux.Select(m => m.Debut));
        Assert.Equal("deux", lignes[0].Morceaux[1].Texte);
        Assert.Equal(8, lignes[1].Morceaux[0].Debut);
    }

    /// <summary>
    /// Le cas que le juge sabote : un mot renvoyé dont la tête est un blanc que la découpe ne
    /// sépare pas — une insécable. Le renvoi la rogne ; le morceau commence après elle, et
    /// elle reste au bout de la ligne qu'il quitte.
    /// </summary>
    [Fact]
    public void Un_mot_renvoye_commence_apres_son_blanc_de_tete()
    {
        var lignes = Composer("aaaa \u00A0bbb", 6);

        Assert.Equal(["aaaa", "bbb"], lignes.Select(l => l.Texte));
        Assert.Equal((0, 6), (lignes[0].Debut, lignes[0].Fin));
        Assert.Equal(6, lignes[1].Morceaux.Single().Debut);
        Assert.Equal((6, 9), (lignes[1].Debut, lignes[1].Fin));
    }

    /// <summary>
    /// Une unité faite de blancs seuls — deux espaces qu'un changement de style isole — renvoyée
    /// derrière un mot trop large : le renvoi la vide, et le morceau vide qui ouvre la ligne
    /// suivante se pose après eux.
    /// </summary>
    [Fact]
    public void Une_suite_de_blancs_renvoyee_ouvre_la_ligne_apres_elle()
    {
        var lignes = Composer("abcdefghij  k", 5, Gras(10, 3));

        Assert.Equal(["abcdefghij", "k"], lignes.Select(l => l.Texte));
        Assert.Equal((0, 12), (lignes[0].Debut, lignes[0].Fin));
        Assert.Equal(["", "k"], lignes[1].Morceaux.Select(m => m.Texte));
        Assert.Equal([12, 12], lignes[1].Morceaux.Select(m => m.Debut));
    }

    [Fact]
    public void Une_tabulation_est_un_morceau_a_sa_place()
    {
        var ligne = Composer("a\tb", 100).Single();

        Assert.Equal(["a", "\t", "b"], ligne.Morceaux.Select(m => m.Texte));
        Assert.Equal([0, 1, 2], ligne.Morceaux.Select(m => m.Debut));
        Assert.Equal(Compositeur.IntervalleTaquet, ligne.Morceaux[2].X);
    }

    [Theory]
    [InlineData("un\r\ndeux", 4)]
    [InlineData("un\ndeux", 3)]
    [InlineData("un\rdeux", 3)]
    public void Le_saut_n_est_dans_aucune_ligne(string texte, int debutDeDeux)
    {
        var lignes = Composer(texte, 100);

        Assert.Equal(2, lignes.Count);
        Assert.Equal((0, 2, 0), (lignes[0].Debut, lignes[0].Fin, lignes[0].Paragraphe));
        Assert.Equal((debutDeDeux, texte.Length, 1), (lignes[1].Debut, lignes[1].Fin, lignes[1].Paragraphe));
    }

    [Fact]
    public void Un_paragraphe_vide_est_une_ligne_de_plage_vide()
    {
        var lignes = Composer("un\n\ndeux\n", 100);

        Assert.Equal(
            [(0, 2, 0), (3, 3, 1), (4, 8, 2), (9, 9, 3)],
            lignes.Select(l => (l.Debut, l.Fin, l.Paragraphe)));
        Assert.Empty(lignes[1].Morceaux);
    }

    /// <summary>
    /// L'exception que le poste déclare : un « \r\n » que deux fragments se partagent comptait
    /// deux paragraphes dans la composition, un seul dans le recalage de la frappe. Il n'en
    /// compte plus qu'un : la ligne vide qui s'intercalait disparaît, et l'écart de paragraphe
    /// posé sur le deuxième paragraphe va à « deux », comme le recalage le pose.
    /// </summary>
    [Fact]
    public void Un_retour_chariot_que_deux_fragments_se_partagent_ne_fait_qu_un_paragraphe()
    {
        const string texte = "un\r\ndeux";
        var centre = new ParagraphSpan { Start = 1, Length = 1, Align = TextAlign.Center };

        var lignes = Compositeur.Composer(texte, [Gras(0, 3)], Commun, [centre], default,
            100, Haute, UneColonne, 1, new ChasseFixe());

        Assert.Equal(["un", "deux"], lignes.Select(l => l.Texte));
        Assert.Equal([0, 1], lignes.Select(l => l.Paragraphe));
        Assert.Equal(TextAlign.Center, lignes[1].Fer);
        Assert.Equal(TextAlign.Left, lignes[0].Fer);
        Assert.Equal(1, Recalage.Paragraphes(texte, texte, new Edition(texte.Length, 0, 0)).Paragraphe);
    }

    [Fact]
    public void Une_paire_de_substitution_reste_dans_son_morceau()
    {
        var lignes = Composer("a😀 b", 100);

        Assert.Equal(["a😀 ", "b"], lignes.Single().Morceaux.Select(m => m.Texte));
        Assert.Equal([0, 4], lignes.Single().Morceaux.Select(m => m.Debut));
    }

    [Fact]
    public void La_sortie_garde_ses_textes_ses_abscisses_et_ses_largeurs()
    {
        // Le contrat d'avant le poste, sur un texte qui passe par les chemins de la découpe :
        // deux espaces, une tabulation, un mot renvoyé, un blanc de fin rogné, un saut
        // « \r\n ». Les positions s'ajoutent ; rien de ce qu'on dessine ne bouge.
        var lignes = Composer("un  deux\ttrois quatre\r\ncinq six", 45);

        Assert.Equal(["un  deux\ttrois", "quatre", "cinq six"], lignes.Select(l => l.Texte));
        Assert.Equal([0.0, 4.0, 8.0, 36.0], lignes[0].Morceaux.Select(m => m.X));
        Assert.Equal([4.0, 4.0, 28.0, 5.0], lignes[0].Morceaux.Select(m => m.Largeur));
        Assert.Equal(41.0, lignes[0].Largeur);
        Assert.Equal([0, 4, 8, 9], lignes[0].Morceaux.Select(m => m.Debut));
        Assert.Equal((15, 21), (lignes[1].Debut, lignes[1].Fin));
        Assert.Equal((23, 31), (lignes[2].Debut, lignes[2].Fin));
    }

    // ------------------------------------------------------------------ la géométrie d'une ligne

    private static IReadOnlyList<double> DecalagesDe(LigneComposee ligne) =>
        ligne.Fer == TextAlign.Justify && !ligne.FinDeParagraphe
            ? Compositeur.Decalages(ligne)
            : new double[ligne.Morceaux.Count];

    private static bool DansUnePaire(string texte, int position) =>
        position > 0 && position < texte.Length
                     && char.IsLowSurrogate(texte[position]) && char.IsHighSurrogate(texte[position - 1]);

    /// <summary>
    /// Pour chaque ligne : l'abscisse d'un morceau est celle où le rendu le pose ; les
    /// abscisses des positions croissent strictement d'un bout à l'autre de la ligne,
    /// tabulations et blancs pendants compris ; et la position la plus proche de l'abscisse
    /// d'une position, c'est elle.
    /// </summary>
    [Theory]
    [MemberData(nameof(Textes))]
    public void L_abscisse_d_une_position_et_la_position_d_une_abscisse_se_repondent(string texte)
    {
        var metriques = new ChasseVariable();
        foreach (var fer in new[] { TextAlign.Left, TextAlign.Justify })
        {
            for (var largeur = 3; largeur <= 40; largeur += 3)
            {
                var lignes = Compositeur.Composer(texte, TousLesTrois(texte), Commun, [],
                    default(ParagrapheEffectif) with { Align = fer }, largeur, Haute, UneColonne, 1, metriques);

                foreach (var ligne in lignes)
                {
                    var decalages = DecalagesDe(ligne);
                    var cas = $"« {Visible(texte)} », {fer}, largeur {largeur}, ligne [{ligne.Debut}, {ligne.Fin})";

                    for (var m = 0; m < ligne.Morceaux.Count; m++)
                    {
                        var morceau = ligne.Morceaux[m];
                        if (morceau.Texte.Length == 0) continue;
                        Assert.True(
                            Compositeur.Abscisse(ligne, decalages, morceau.Debut, texte, Commun, metriques)
                            == morceau.X + decalages[m],
                            $"{cas} : « {Visible(morceau.Texte)} » n'est pas à son abscisse");
                    }

                    var precedente = double.NegativeInfinity;
                    for (var p = ligne.Debut; p <= ligne.Fin; p++)
                    {
                        if (DansUnePaire(texte, p)) continue;
                        var x = Compositeur.Abscisse(ligne, decalages, p, texte, Commun, metriques);
                        Assert.True(x > precedente, $"{cas} : la position {p} ({x}) n'est pas après la précédente ({precedente})");
                        precedente = x;

                        var retrouvee = Compositeur.PositionLaPlusProche(ligne, decalages, x, texte, Commun, metriques);
                        Assert.True(retrouvee == p, $"{cas} : l'abscisse {x} de la position {p} rend {retrouvee}");
                    }
                }
            }
        }
    }

    [Fact]
    public void Une_abscisse_entre_deux_caracteres_va_au_plus_proche()
    {
        var ligne = Composer("abcd", 100).Single();
        var zeros = new double[ligne.Morceaux.Count];
        int Sous(double x) => Compositeur.PositionLaPlusProche(ligne, zeros, x, "abcd", Commun, new ChasseFixe());

        Assert.Equal(1, Sous(1.4));
        Assert.Equal(2, Sous(1.6));
        Assert.Equal(0, Sous(-50));
        Assert.Equal(4, Sous(50));
    }

    [Fact]
    public void Une_tabulation_avance_le_point_jusqu_au_taquet()
    {
        var ligne = Composer("a\tb", 100).Single();
        var zeros = new double[ligne.Morceaux.Count];
        double X(int p) => Compositeur.Abscisse(ligne, zeros, p, "a\tb", Commun, new ChasseFixe());

        Assert.Equal([0.0, 1.0, Compositeur.IntervalleTaquet, Compositeur.IntervalleTaquet + 1],
            Enumerable.Range(0, 4).Select(X));
    }

    /// <summary>
    /// Une ligne justifiée élargit ses blancs : le point qui suit un blanc élargi est au début
    /// du mot suivant, là où le rendu le pose, et non au bout du blanc tel qu'il se mesure.
    /// </summary>
    [Fact]
    public void Le_point_qui_suit_un_blanc_elargi_est_au_mot_suivant()
    {
        const string texte = "un deux trois quatre";
        var lignes = Compositeur.Composer(texte, [], Commun, [],
            default(ParagrapheEffectif) with { Align = TextAlign.Justify }, 10, Haute, UneColonne, 1, new ChasseFixe());
        var ligne = lignes[0];
        var decalages = Compositeur.Decalages(ligne);

        Assert.Equal("un deux", ligne.Texte);
        Assert.Equal(3.0, decalages[1]);
        Assert.Equal(2.0, Compositeur.Abscisse(ligne, decalages, 2, texte, Commun, new ChasseFixe()));
        Assert.Equal(6.0, Compositeur.Abscisse(ligne, decalages, 3, texte, Commun, new ChasseFixe()));
    }

    /// <summary>
    /// Les blancs que la composition rogne au bout d'une ligne pendent après son dernier
    /// morceau : ils ne comptent pas dans sa largeur, mais chacun a sa place, dans le style du
    /// morceau qu'ils prolongent, et une tabulation pendante avance jusqu'au taquet.
    /// </summary>
    [Fact]
    public void Les_blancs_rognes_pendent_au_bout_de_la_ligne()
    {
        const string texte = "abc \t\tdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz";
        var lignes = Composer(texte, 50);
        var ligne = lignes[0];
        var zeros = new double[ligne.Morceaux.Count];
        double X(int p) => Compositeur.Abscisse(ligne, zeros, p, texte, Commun, new ChasseFixe());

        Assert.Equal("abc", ligne.Texte);
        Assert.Equal(3.0, ligne.Largeur);
        Assert.Equal((0, 6), (ligne.Debut, ligne.Fin));
        Assert.Equal([3.0, 4.0, 36.0, 72.0], new[] { 3, 4, 5, 6 }.Select(X));
    }

    [Fact]
    public void Une_ligne_sans_morceau_mesure_ses_blancs_au_style_du_bloc()
    {
        const string texte = "   \nx";
        var gros = Commun with { FontSize = 2 };
        var ligne = Compositeur.Composer(texte, [], gros, [], default, 100, Haute, UneColonne, 1, new ChasseFixe())[0];

        Assert.Empty(ligne.Morceaux);
        Assert.Equal((0, 3), (ligne.Debut, ligne.Fin));
        Assert.Equal([0.0, 2.0, 4.0, 6.0],
            Enumerable.Range(0, 4).Select(p => Compositeur.Abscisse(ligne, [], p, texte, gros, new ChasseFixe())));
    }

    [Fact]
    public void Une_position_dans_une_paire_de_substitution_se_ramene_a_son_debut()
    {
        const string texte = "a😀b";
        var ligne = Composer(texte, 100, new ChasseVariable()).Single();
        var zeros = new double[ligne.Morceaux.Count];
        var metriques = new ChasseVariable();

        Assert.Equal(
            Compositeur.Abscisse(ligne, zeros, 1, texte, Commun, metriques),
            Compositeur.Abscisse(ligne, zeros, 2, texte, Commun, metriques));

        var bout = Compositeur.Abscisse(ligne, zeros, 4, texte, Commun, metriques);
        for (var x = -1.0; x <= bout + 1; x += 0.05)
        {
            Assert.NotEqual(2, Compositeur.PositionLaPlusProche(ligne, zeros, x, texte, Commun, metriques));
        }
    }

    // ------------------------------------------------------------------ le texte vide

    /// <summary>
    /// Un texte vide ne compose aucune ligne, et c'est la sortie d'avant le poste. Mais le
    /// curseur s'y pose : la ligne qu'il aurait est celle d'un paragraphe vide, avec son
    /// retrait, son fer, son espace avant et la hauteur naturelle du style du bloc.
    /// </summary>
    [Fact]
    public void Un_texte_vide_a_la_ligne_d_un_paragraphe_vide()
    {
        var gros = Commun with { FontSize = 3 };
        var reglage = new ParagrapheEffectif(IndentLeft: 4, IndentRight: 1, IndentFirstLine: 2, SpaceBefore: 5, SpaceAfter: 6, TextAlign.Center);
        var ecart = new ParagraphSpan { Start = 0, Length = 1, IndentLeft = 7 };
        var colonnes = new Colonnage(2, 3);

        var vide = Compositeur.LigneDuTexteVide(gros, [ecart], reglage, 40, 20, colonnes, 1.5, new ChasseFixe());
        var paragraphe = Compositeur.Composer("\n", [], gros, [ecart], reglage, 40, 20, colonnes, 1.5, new ChasseFixe())[0];

        Assert.Empty(Compositeur.Composer("", [], gros, [ecart], reglage, 40, 20, colonnes, 1.5, new ChasseFixe()));
        Assert.Empty(vide.Morceaux);
        Assert.Equal(
            (paragraphe.Largeur, paragraphe.Naturelle, paragraphe.FinDeParagraphe, paragraphe.Fer, paragraphe.Retrait,
                paragraphe.Disponible, paragraphe.EspaceAvant, paragraphe.EspaceApres, paragraphe.Colonne, paragraphe.Haut,
                paragraphe.Avance, paragraphe.Debut, paragraphe.Fin, paragraphe.Paragraphe),
            (vide.Largeur, vide.Naturelle, vide.FinDeParagraphe, vide.Fer, vide.Retrait,
                vide.Disponible, vide.EspaceAvant, vide.EspaceApres, vide.Colonne, vide.Haut,
                vide.Avance, vide.Debut, vide.Fin, vide.Paragraphe));
        Assert.Equal(9.0, vide.Retrait);
        Assert.Equal(TextAlign.Center, vide.Fer);
        Assert.Equal(0.0, Compositeur.Abscisse(vide, [], 0, "", gros, new ChasseFixe()));
        Assert.Equal(0, Compositeur.PositionLaPlusProche(vide, [], 12, "", gros, new ChasseFixe()));
    }
}
