using System;
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
        public double LargeurDeCoupure(string texte, StyleEffectif style) => Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => style.FontSize;
    }

    private static readonly StyleEffectif Commun =
        new("Police", 1, Bold: false, Italic: false, Souligne.Aucun, "#FF000000", 0);

    /// <summary>
    /// Un bloc d'une seule colonne et assez haut pour tout prendre : le cas de tous les
    /// documents écrits avant R3, celui sur lequel portent les tests qui précèdent la
    /// section « colonnes ».
    /// </summary>
    private static readonly Colonnage UneColonne = new(1, 0);

    private const double Haute = 1_000_000;

    private static IReadOnlyList<LigneComposee> Composer(
        string texte, double largeur, params StyleSpan[] fragments) =>
        Compositeur.Composer(texte, fragments, Commun, [], default,
            largeur, Haute, UneColonne, 1, new ChasseFixe());

    /// <summary>La même chose, avec des réglages de paragraphe plutôt que de style.</summary>
    private static IReadOnlyList<LigneComposee> Composer(
        string texte, double largeur, ParagrapheEffectif commun, params ParagraphSpan[] ecarts) =>
        Compositeur.Composer(texte, [], Commun, ecarts, commun,
            largeur, Haute, UneColonne, 1, new ChasseFixe());

    /// <summary>
    /// Un bloc en colonnes. La hauteur et la largeur sont toujours données distinctes
    /// dans les tests qui suivent : deux <c>double</c> voisins s'intervertissent en
    /// silence, et une hauteur prise pour une largeur donnerait un texte plausible.
    /// </summary>
    private static IReadOnlyList<LigneComposee> Composer(
        string texte, double largeur, double hauteur, Colonnage colonnes,
        ParagrapheEffectif commun = default, double interligne = 1) =>
        Compositeur.Composer(texte, [], Commun, [], commun,
            largeur, hauteur, colonnes, interligne, new ChasseFixe());

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

    // ------------------------------------------------- la règle de coupure (Q13)

    /// <summary>
    /// Une règle qui dessine à la chasse fixe, mais décide de ses coupures sur une mesure
    /// plus large : celle qu'un autre rendu donnerait. Le double, pour que l'écart se lise
    /// sans calcul.
    /// </summary>
    private sealed class CoupeLarge : Compositeur.IMetriques
    {
        private readonly ChasseFixe _chasse = new();
        public double Largeur(string texte, StyleEffectif style) => _chasse.Largeur(texte, style);
        public double LargeurDeCoupure(string texte, StyleEffectif style) => 2 * _chasse.Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => _chasse.Naturelle(style);
    }

    [Fact]
    [Trait("poste", "Q13")]
    public void La_coupure_se_decide_sur_la_mesure_de_coupure()
    {
        // « aa bb cc » à la chasse fixe tient en 8. À la mesure de coupure, double, « aa »
        // vaut 4 et « aa bb » 10 : sur 9 de large, la ligne se ferme après « aa ».
        var lignes = Compositeur.Composer("aa bb cc", [], Commun, [], default,
            9, Haute, UneColonne, 1, new CoupeLarge());

        Assert.Equal(new[] { "aa", "bb", "cc" }, lignes.Select(l => l.Texte).ToArray());
    }

    [Fact]
    [Trait("poste", "Q13")]
    public void Le_dessin_garde_sa_propre_mesure()
    {
        // La décision a lu la mesure double ; les morceaux, eux, sont posés à la chasse
        // fixe — c'est avec elle que le rendu dessine et justifie.
        var lignes = Compositeur.Composer("aa bb cc dd", [], Commun, [], default,
            12, Haute, UneColonne, 1, new CoupeLarge());

        Assert.Equal(new[] { "aa bb", "cc dd" }, lignes.Select(l => l.Texte).ToArray());
        Assert.Equal(5, lignes[0].Largeur, precision: 9);
        Assert.Equal(new[] { 0.0, 3.0 }, lignes[0].Morceaux.Select(m => m.X).ToArray());
        Assert.Equal(new[] { 3.0, 2.0 }, lignes[0].Morceaux.Select(m => m.Largeur).ToArray());
    }

    [Fact]
    [Trait("poste", "Q13")]
    public void Une_tabulation_avance_au_taquet_de_chaque_mesure()
    {
        // « a\tb » : le dessin pose « b » au taquet de sa propre largeur (36) ; la décision
        // compte le taquet de la sienne. Avec 30 unités de « a », la mesure de coupure
        // vaut 60 et son taquet 72 : « b » y passe la limite de 73 de deux unités et
        // descend, alors qu'au taquet du dessin (36) il aurait tenu.
        var a = new string('a', 30);
        var lignes = Compositeur.Composer($"{a}\tbb", [], Commun, [], default,
            73, Haute, UneColonne, 1, new CoupeLarge());

        Assert.Equal(2, lignes.Count);
        Assert.Equal("bb", lignes[1].Texte);
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


    // -------------------------------------------------------------- justification

    [Fact]
    public void Une_ligne_justifiee_va_dun_bord_a_lautre()
    {
        // Le trou que l'épreuve du juge a trouvé le 6 septembre 2026 : la comparaison
        // écran ↔ PDF ne pouvait pas l'attraper, puisque les deux rendus appellent le
        // même calcul et se trompent donc ensemble. Elle prouve l'accord, pas la
        // justesse — d'où ce test-ci.
        var ligne = Composer("un deux trois", 100)[0];
        var decalages = Compositeur.Decalages(ligne);

        var dernier = ligne.Morceaux[^1];
        var droite = dernier.X + decalages[^1] + dernier.Largeur;

        Assert.Equal(100, droite, precision: 9);
    }

    [Fact]
    public void Le_blanc_se_repartit_egalement_entre_les_mots()
    {
        // « un deux trois » : 13 unités d'encre, deux blancs à élargir. Pour aller
        // à 100, il manque 87, soit 43,5 par blanc.
        var ligne = Composer("un deux trois", 100)[0];
        var decalages = Compositeur.Decalages(ligne);

        Assert.Equal(0, decalages[0], precision: 9);
        Assert.Equal([0, 43.5, 87], decalages.Select(d => Math.Round(d, 9)).ToArray());
    }

    [Fact]
    public void Le_blanc_se_repartit_entre_les_mots_et_non_entre_les_morceaux()
    {
        // Le piège de R1 : « un deux trois » avec « deux » en gras fait quatre
        // morceaux — « un », « », « deux », « trois » — mais toujours deux blancs.
        // Compter les morceaux décalerait le texte à chaque changement de style.
        var ligne = Composer("un deux trois", 100,
            new StyleSpan { Start = 3, Length = 4, Bold = true })[0];
        var decalages = Compositeur.Decalages(ligne);

        Assert.True(ligne.Morceaux.Count > 3, "le fragment doit bien découper la ligne");
        Assert.Equal(87, decalages[^1], precision: 9);
        Assert.Equal(2, decalages.Distinct().Count(d => d > 0));
    }

    [Fact]
    public void Une_ligne_dun_seul_mot_ne_setire_pas()
    {
        var ligne = Composer("seul", 100)[0];

        Assert.All(Compositeur.Decalages(ligne), d => Assert.Equal(0, d));
    }

    [Fact]
    public void Une_ligne_deja_trop_large_ne_se_retrecit_pas()
    {
        // Un blanc négatif superposerait les mots ; on laisse déborder. La découpe
        // gloutonne ne produit pas d'elle-même une telle ligne — un mot trop large
        // reste seul sur la sienne — d'où le rétrécissement à la main.
        var ligne = Composer("un deux trois", 100)[0] with { Disponible = 1 };

        Assert.All(Compositeur.Decalages(ligne), d => Assert.Equal(0, d));
    }


    // -------------------------------------------------------------- tabulations

    [Fact]
    public void Une_tabulation_avance_jusquau_prochain_taquet()
    {
        // Taquets tous les 36 pt. « ab » fait 2 unités avec la règle factice, la
        // tabulation doit donc porter le mot suivant à 36.
        var ligne = Composer("ab\tcd", 200)[0];
        var apres = ligne.Morceaux.First(m => m.Texte == "cd");

        Assert.Equal(Compositeur.IntervalleTaquet, apres.X, precision: 9);
    }

    [Fact]
    public void Deux_tabulations_ne_valent_pas_une()
    {
        // Le piège : posée exactement sur un taquet, une tabulation qui avancerait
        // « jusqu'au prochain multiple » ne bougerait pas, et les deux consignes
        // d'une fiche se colleraient.
        var ligne = Composer("ab\t\tcd", 400)[0];
        var apres = ligne.Morceaux.First(m => m.Texte == "cd");

        Assert.Equal(2 * Compositeur.IntervalleTaquet, apres.X, precision: 9);
    }

    [Fact]
    public void Une_tabulation_en_debut_de_ligne_decale_le_premier_mot()
    {
        var ligne = Composer("\tab", 200)[0];

        Assert.Equal(Compositeur.IntervalleTaquet,
            ligne.Morceaux.First(m => m.Texte == "ab").X, precision: 9);
    }

    [Fact]
    public void Un_taquet_tombe_toujours_apres_la_position_courante()
    {
        Assert.Equal(36, Compositeur.ProchainTaquet(0), precision: 9);
        Assert.Equal(36, Compositeur.ProchainTaquet(1), precision: 9);
        Assert.Equal(72, Compositeur.ProchainTaquet(36), precision: 9);
        Assert.Equal(72, Compositeur.ProchainTaquet(71.9), precision: 9);
    }

    [Fact]
    public void Une_tabulation_dans_un_bloc_trop_etroit_ne_part_pas_en_cascade()
    {
        // Un taquet à 36 pt dans un bloc de 5 pt pousse forcément le mot suivant
        // au-delà du bord : il passe à la ligne, et c'est bien ce qu'on veut. Ce qu'on
        // ne veut pas, c'est qu'il recommence indéfiniment — la tabulation elle-même ne
        // provoque jamais de renvoi, sans quoi chaque ligne neuve en rouvrirait une.
        var lignes = Composer("a\tb", 5);

        Assert.Equal(2, lignes.Count);
        Assert.Equal("ab", string.Concat(lignes.Select(l => l.Texte)).Replace("\t", ""));
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
        Assert.Equal(5, Compositeur.Hauteur(lignes), precision: 9);
    }

    // ----------------------------------------------- retraits et espacement (R2)

    /// <summary>Un réglage de paragraphe écrit en clair, plutôt que cinq nombres.</summary>
    private static ParagrapheEffectif Reglage(
        double gauche = 0, double droite = 0, double premiere = 0,
        double avant = 0, double apres = 0, TextAlign fer = TextAlign.Left) =>
        new(gauche, droite, premiere, avant, apres, fer);

    [Fact]
    public void Sans_reglage_une_ligne_occupe_toute_la_largeur()
    {
        // Le cas de tous les documents écrits avant R2 : rien ne doit bouger.
        var lignes = Composer("un deux trois", 10);

        Assert.All(lignes, l => Assert.Equal(0, l.Retrait));
        Assert.All(lignes, l => Assert.Equal(10, l.Disponible));
        Assert.All(lignes, l => Assert.Equal(0, l.EspaceAvant + l.EspaceApres));
    }

    [Fact]
    public void Un_retrait_gauche_decale_toutes_les_lignes_du_paragraphe()
    {
        var lignes = Composer("un deux trois", 12, Reglage(gauche: 2));

        Assert.True(lignes.Count > 1, "il faut au moins deux lignes pour comparer");
        Assert.All(lignes, l => Assert.Equal(2, l.Retrait));
        Assert.All(lignes, l => Assert.Equal(10, l.Disponible));
    }

    [Fact]
    public void Un_retrait_de_premiere_ligne_ne_decale_que_la_premiere()
    {
        var lignes = Composer("un deux trois quatre", 12, Reglage(premiere: 3));

        Assert.Equal(3, lignes[0].Retrait, precision: 9);
        Assert.All(lignes.Skip(1), l => Assert.Equal(0, l.Retrait));
    }

    [Fact]
    public void Un_retrait_negatif_fait_pendre_la_premiere_ligne_a_gauche()
    {
        // C'est la liste à puces des trente-six paragraphes d'ATA 21 : la puce se pose
        // dans le creux, le texte qui suit s'aligne plus à droite.
        var lignes = Composer("un deux trois quatre", 12, Reglage(gauche: 4, premiere: -4));

        Assert.Equal(0, lignes[0].Retrait, precision: 9);
        Assert.All(lignes.Skip(1), l => Assert.Equal(4, l.Retrait));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(-5)]
    public void Le_bord_droit_ne_depend_pas_du_retrait_de_premiere_ligne(double premiere)
    {
        // Ce que « fo:text-indent » veut dire : la première ligne se déplace, le bord
        // droit ne bouge pas. Retirer le retrait des deux côtés donnerait un paragraphe
        // qui rentre à droite en même temps qu'à gauche — et la comparaison écran ↔ PDF
        // n'y verrait rien, puisque les deux rendus lisent la même mesure.
        var lignes = Composer("un deux trois quatre cinq six", 24,
            Reglage(gauche: 6, droite: 3, premiere: premiere));

        Assert.True(lignes.Count > 1, "il faut au moins deux lignes pour comparer");
        Assert.All(lignes, l => Assert.Equal(21, l.Retrait + l.Disponible, precision: 9));
    }

    [Fact]
    public void Un_retrait_coupe_les_lignes_plus_tot()
    {
        // Le retrait n'est pas qu'un décalage : il rétrécit la ligne, donc la découpe.
        Assert.Equal(["un deux", "trois"], Composer("un deux trois", 10).Select(l => l.Texte));

        var serre = Composer("un deux trois", 10, Reglage(gauche: 4));
        Assert.Equal(["un", "deux", "trois"], serre.Select(l => l.Texte));
    }

    [Fact]
    public void Une_ligne_justifiee_en_retrait_sarrete_au_retrait_de_droite()
    {
        // Le piège annoncé du poste : un retrait change la largeur cible d'une ligne
        // justifiée. La cible n'est plus la largeur du bloc mais ce qui reste entre les
        // deux retraits, et elle est portée par la ligne — les deux rendus ne peuvent
        // donc plus la calculer chacun de leur côté.
        var lignes = Composer("un deux trois quatre cinq", 20,
            Reglage(gauche: 6, droite: 4, premiere: -6));

        var premiere = lignes[0];
        var decalages = Compositeur.Decalages(premiere);
        var dernier = premiere.Morceaux[^1];

        Assert.False(premiere.FinDeParagraphe);
        Assert.Equal(0, premiere.Retrait, precision: 9);
        Assert.Equal(16, premiere.Disponible, precision: 9);
        Assert.Equal(16, dernier.X + decalages[^1] + dernier.Largeur, precision: 9);
    }

    [Fact]
    public void Lespace_avant_ne_porte_que_sur_la_premiere_ligne_du_paragraphe()
    {
        // Deux lignes dans le premier paragraphe, une dans le second : l'espace ne
        // s'ajoute qu'à l'entrée d'un paragraphe, pas à chaque renvoi à la ligne.
        var lignes = Composer("un deux trois\nsuite", 10, Reglage(avant: 5));

        Assert.Equal([5, 0, 5], lignes.Select(l => l.EspaceAvant));
    }

    [Fact]
    public void Lespace_apres_ne_porte_que_sur_la_derniere_ligne_du_paragraphe()
    {
        var lignes = Composer("un deux trois\nsuite", 10, Reglage(apres: 7));

        Assert.Equal([0, 7, 7], lignes.Select(l => l.EspaceApres));
    }

    [Fact]
    public void Lespacement_sajoute_a_la_hauteur_du_texte()
    {
        // Deux paragraphes d'une ligne, corps 1, interligne 1, cinq d'espace après
        // chacun : douze unités. Sans cela, la mesure de débordement mentirait dès
        // qu'un document se sert de l'espacement — les vingt-neuf paragraphes des
        // Fiches, par exemple.
        var lignes = Composer("un\ndeux", 100, Reglage(apres: 5));

        Assert.Equal(12, Compositeur.Hauteur(lignes), precision: 9);
    }

    [Fact]
    public void Un_ecart_ne_change_que_ses_paragraphes()
    {
        // Dans les documents réels, les paragraphes d'un même bloc n'ont pas tous le
        // même retrait : c'est toute la raison d'être des écarts.
        var lignes = Composer("a\nb\nc", 100, Reglage(),
            new ParagraphSpan { Start = 1, Length = 1, IndentLeft = 6 });

        Assert.Equal([0, 6, 0], lignes.Select(l => l.Retrait));
    }

    [Fact]
    public void Un_ecart_sans_longueur_porte_sur_son_seul_paragraphe()
    {
        var lignes = Composer("a\nb\nc", 100, Reglage(),
            new ParagraphSpan { Start = 1, IndentLeft = 6 });

        Assert.Equal([0, 6, 0], lignes.Select(l => l.Retrait));
    }

    [Fact]
    public void Un_ecart_nherite_que_de_ce_quil_ne_dit_pas()
    {
        // Il change le retrait gauche ; l'espace après reste celui du bloc.
        var lignes = Composer("a\nb", 100, Reglage(gauche: 2, apres: 4),
            new ParagraphSpan { Start = 0, IndentLeft = 9 });

        Assert.Equal(9, lignes[0].Retrait, precision: 9);
        Assert.Equal(4, lignes[0].EspaceApres, precision: 9);
        Assert.Equal(2, lignes[1].Retrait, precision: 9);
    }

    [Fact]
    public void Le_dernier_ecart_pose_lemporte()
    {
        var lignes = Composer("a\nb", 100, Reglage(),
            new ParagraphSpan { Start = 0, Length = 2, IndentLeft = 3 },
            new ParagraphSpan { Start = 1, Length = 1, IndentLeft = 8 });

        Assert.Equal([3, 8], lignes.Select(l => l.Retrait));
    }

    [Fact]
    public void Un_ecart_aux_bornes_absurdes_est_ignore_sans_bruit()
    {
        // Un import qui compte mal ses paragraphes doit donner un document laid,
        // jamais un document illisible — ni une addition qui déborde.
        var lignes = Composer("a\nb", 100, Reglage(),
            new ParagraphSpan { Start = -5, Length = 2, IndentLeft = 99 },
            new ParagraphSpan { Start = 100, Length = 3, IndentLeft = 99 },
            new ParagraphSpan { Start = int.MaxValue, Length = 5, IndentLeft = 99 });

        Assert.Equal(2, lignes.Count);
        Assert.All(lignes, l => Assert.Equal(0, l.Retrait));
    }

    [Fact]
    public void Un_retrait_plus_large_que_le_bloc_ne_fait_pas_boucler_la_decoupe()
    {
        var lignes = Composer("un deux trois", 10, Reglage(gauche: 40));

        Assert.Equal(["un", "deux", "trois"], lignes.Select(l => l.Texte));
        Assert.All(lignes, l => Assert.Equal(1, l.Disponible));
    }

    // ------------------------------------------------- colonnes du bloc (R3)

    /// <summary>
    /// Trois colonnes dans un bloc large de 34, gouttière 2 : (34 − 2×2) / 3 = 10 par
    /// colonne, et le pas d'un bord gauche au suivant vaut 12. Les nombres sont choisis
    /// pour tomber juste avec la règle à mesurer factice — un caractère, une unité — de
    /// sorte que chaque attente ci-dessous s'écrive et se relise à la main.
    ///
    /// La hauteur ne vaut jamais la largeur : intervertir les deux dans un des deux
    /// rendus donnerait un texte plausible, et c'est exactement le genre de faute qu'une
    /// comparaison écran ↔ PDF ne voit pas.
    /// </summary>
    private static readonly Colonnage Trois = new(3, 2);

    [Fact]
    public void Une_seule_colonne_compose_exactement_comme_avant()
    {
        // Le cas de tous les documents du dépôt, gouttière comprise : elle ne sert pas
        // à une colonne, et ne doit donc rien changer. C'est la promesse de
        // non-régression du poste, prise à sa racine.
        var sans = Composer("un deux trois quatre cinq", 10, 3, new Colonnage(1, 0));
        var avec = Composer("un deux trois quatre cinq", 10, 3, new Colonnage(1, 7));

        Assert.Equal(
            sans.Select(l => (l.Texte, l.Retrait, l.Disponible, l.Colonne, l.Haut)),
            avec.Select(l => (l.Texte, l.Retrait, l.Disponible, l.Colonne, l.Haut)));

        // Et le texte continue de descendre au-delà des trois unités de hauteur, comme
        // il le faisait avant ce poste : rien n'est tronqué.
        Assert.All(sans, l => Assert.Equal(0, l.Colonne));
        Assert.Equal([0, 1, 2, 3], sans.Select(l => l.Haut));
    }

    [Fact]
    public void La_largeur_disponible_est_celle_dune_colonne()
    {
        var lignes = Composer("un deux trois quatre", 34, 100, Trois);

        Assert.All(lignes, l => Assert.Equal(10, l.Disponible));
        // Dix unités, donc « un deux » (7) tient et « un deux trois » (13) non.
        Assert.Equal(["un deux", "trois", "quatre"], lignes.Select(l => l.Texte));
    }

    [Fact]
    public void Le_texte_remplit_une_colonne_puis_passe_a_la_suivante()
    {
        // Six lignes d'une unité chacune, deux unités de hauteur : deux lignes par
        // colonne. La sixième tombe dans la troisième colonne, la dernière.
        var lignes = Composer("a\nb\nc\nd\ne\nf", 34, 2, Trois);

        Assert.Equal([0, 0, 1, 1, 2, 2], lignes.Select(l => l.Colonne));
        Assert.Equal([0, 1, 0, 1, 0, 1], lignes.Select(l => l.Haut));
    }

    [Fact]
    public void La_colonne_porte_la_ligne_jusqua_sa_gouttiere()
    {
        // Le retrait de la ligne comprend le pas de sa colonne : 0, 12, 24. Un rendu
        // n'a donc rien à ajouter, et ne peut pas ajouter autre chose que l'autre.
        var lignes = Composer("a\nb\nc\nd\ne\nf", 34, 2, Trois);

        Assert.Equal([0, 0, 12, 12, 24, 24], lignes.Select(l => l.Retrait));
    }

    [Fact]
    public void Le_retrait_de_paragraphe_sajoute_au_pas_de_la_colonne()
    {
        // Le retrait est compté dans la colonne, puis la colonne le décale : 12 + 3.
        // L'ordre importe — décaler d'abord rognerait la largeur disponible du pas.
        var lignes = Composer("a\nb\nc\nd", 34, 2, Trois, Reglage(gauche: 3));

        Assert.Equal([3, 3, 15, 15], lignes.Select(l => l.Retrait));
        Assert.All(lignes, l => Assert.Equal(7, l.Disponible));
    }

    [Fact]
    public void Ce_qui_depasse_la_derniere_colonne_continue_vers_le_bas()
    {
        // Huit lignes pour trois colonnes de deux : les deux dernières débordent sous
        // la troisième colonne au lieu d'être tronquées. Tronquer livrerait un PDF
        // amputé sans le dire — c'est le poste A2, et il n'est pas écrit.
        var lignes = Composer("a\nb\nc\nd\ne\nf\ng\nh", 34, 2, Trois);

        Assert.Equal([0, 0, 1, 1, 2, 2, 2, 2], lignes.Select(l => l.Colonne));
        Assert.Equal([0, 1, 0, 1, 0, 1, 2, 3], lignes.Select(l => l.Haut));
    }

    [Fact]
    public void Une_ligne_plus_haute_que_la_colonne_garde_la_sienne()
    {
        // Sans garde-fou, une ligne qui ne tient nulle part traverserait toutes les
        // colonnes sans rien écrire dans aucune. Deux lignes, hauteur d'une demie :
        // chacune prend une colonne, aucune n'est sautée.
        var lignes = Composer("a\nb", 34, 0.5, Trois);

        Assert.Equal([0, 1], lignes.Select(l => l.Colonne));
        Assert.Equal([0, 0], lignes.Select(l => l.Haut));
    }

    [Fact]
    public void Une_hauteur_nulle_ne_disperse_pas_le_texte()
    {
        // Un bloc plus petit que ses marges intérieures rend une hauteur nulle ou
        // négative. Rompre à chaque ligne éparpillerait le texte sur toutes les
        // colonnes : on ne rompt pas du tout, et le bloc reste lisible.
        var lignes = Composer("a\nb\nc\nd", 34, 0, Trois);

        Assert.All(lignes, l => Assert.Equal(0, l.Colonne));
        Assert.Equal([0, 1, 2, 3], lignes.Select(l => l.Haut));
    }

    [Fact]
    public void Un_nombre_de_colonnes_absurde_vaut_une_colonne()
    {
        // Un import ou un fichier écrit à la main peut porter zéro, ou un nombre
        // négatif. Le document doit rester lisible.
        foreach (var nombre in new[] { 0, -3 })
        {
            var lignes = Composer("un deux trois", 10, 3, new Colonnage(nombre, 2));

            Assert.All(lignes, l => Assert.Equal(0, l.Colonne));
            Assert.All(lignes, l => Assert.Equal(10, l.Disponible));
        }
    }

    [Fact]
    public void Une_gouttiere_plus_large_que_le_bloc_ne_fait_pas_boucler_la_decoupe()
    {
        var lignes = Composer("un deux trois", 10, 100, new Colonnage(3, 40));

        Assert.Equal(["un", "deux", "trois"], lignes.Select(l => l.Texte));
        Assert.All(lignes, l => Assert.Equal(1, l.Disponible));
    }

    [Fact]
    public void Lespace_avant_sapplique_aussi_en_tete_de_colonne()
    {
        // Choisi sans mesure, comme l'espace avant du premier paragraphe du bloc en R2 :
        // le relevé des deux .pub ne porte aucun fo:margin-top. Ce qui compte est que
        // les deux cas soient tranchés une fois, et de la même façon.
        var lignes = Composer("a\nb\nc\nd", 34, 4, Trois, Reglage(avant: 1));

        Assert.Equal([0, 0, 1, 1], lignes.Select(l => l.Colonne));
        Assert.Equal([1, 3, 1, 3], lignes.Select(l => l.Haut));
    }

    [Fact]
    public void Une_ligne_coupee_par_une_colonne_reste_justifiable()
    {
        // Une ligne que la rupture de colonne sépare de la suite de son paragraphe
        // n'est pas la dernière de ce paragraphe : elle se justifie encore, et sur la
        // largeur de sa colonne. La croire finie la laisserait courte au bas d'une
        // colonne, seule ligne du bloc à ne pas atteindre le bord.
        var lignes = Composer("un deux trois quatre", 34, 1, Trois);

        Assert.Equal([0, 1, 2], lignes.Select(l => l.Colonne));
        Assert.Equal([false, false, true], lignes.Select(l => l.FinDeParagraphe));
        Assert.All(lignes, l => Assert.Equal(10, l.Disponible));
    }

    [Fact]
    public void La_hauteur_reclamee_est_celle_de_la_colonne_la_plus_remplie()
    {
        // Et non la somme des colonnes : un bloc de trois colonnes pleines réclame la
        // hauteur d'une seule. C'est ce que A2 aura à comparer à celle du bloc.
        var lignes = Composer("a\nb\nc\nd\ne", 34, 2, Trois);

        Assert.Equal(2, Compositeur.Hauteur(lignes), precision: 9);
    }

    [Fact]
    public void Linterligne_deplace_la_rupture_de_colonne()
    {
        // La rupture se décide sur l'avance, pas sur le corps : le même texte dans le
        // même bloc tient à deux lignes par colonne à interligne 1, et à une seule à
        // interligne 2. Un rendu qui appliquerait un autre interligne que celui de la
        // composition dessinerait des colonnes coupées ailleurs qu'il ne les remplit.
        var serre = Composer("a\nb\nc", 34, 2, Trois, interligne: 1);
        var large = Composer("a\nb\nc", 34, 2, Trois, interligne: 2);

        Assert.Equal([0, 0, 1], serre.Select(l => l.Colonne));
        Assert.Equal([0, 1, 2], large.Select(l => l.Colonne));
        Assert.All(large, l => Assert.Equal(2, l.Avance));
    }

    // --------------------------------- débordement et calage vertical (A2, A7)

    /// <summary>
    /// Un bloc dont la zone de texte fait <paramref name="disponible"/> de haut, avec
    /// <paramref name="margeBasse"/> sous elle jusqu'au bord du bloc. Les deux sont
    /// toujours donnés à part : c'est leur somme qui décide du débordement, et une
    /// mesure qui les confondrait signalerait un texte que rien ne coupe.
    /// </summary>
    private static IReadOnlyList<LigneComposee> Lignes(string texte, double disponible) =>
        Composer(texte, 100, disponible, UneColonne);

    [Fact]
    public void Un_texte_qui_tient_dans_son_bloc_ne_deborde_pas()
    {
        // Trois lignes d'une unité dans quatre unités de haut.
        Assert.Equal(0, Compositeur.Debordement(
            Lignes("a\nb\nc", 4), 4, margeBasse: 0, VerticalAlign.Top));
    }

    [Fact]
    public void Un_texte_trop_haut_deborde_de_ce_qui_depasse()
    {
        // Cinq lignes d'une unité dans trois unités de haut : deux dépassent.
        Assert.Equal(2, Compositeur.Debordement(
            Lignes("a\nb\nc\nd\ne", 3), 3, margeBasse: 0, VerticalAlign.Top), precision: 9);
    }

    [Fact]
    public void La_marge_interieure_basse_absorbe_ce_qui_ne_sort_pas_du_bloc()
    {
        // Le cas réel d'ATA 21 : deux de ses neuf blocs composent une fraction de point
        // de plus que leur zone de texte, et la marge intérieure basse — 2,88 pt chez
        // Publisher — la reçoit. Rien n'est coupé, rien ne doit être signalé : un
        // avertissement qui crie au loup ne se lit plus.
        var lignes = Lignes("a\nb\nc\nd", 3);

        Assert.Equal(1, Compositeur.Debordement(lignes, 3, margeBasse: 0, VerticalAlign.Top), precision: 9);
        Assert.Equal(0, Compositeur.Debordement(lignes, 3, margeBasse: 1, VerticalAlign.Top));
        Assert.False(Compositeur.Deborde(lignes, 3, margeBasse: 1, VerticalAlign.Top));
    }

    [Fact]
    public void Le_debordement_se_mesure_sur_la_colonne_la_plus_remplie()
    {
        // Cinq lignes en trois colonnes de deux unités : la première colonne en porte
        // deux, la dernière une. Le bloc ne déborde pas, alors que la somme des
        // colonnes ferait cinq fois sa hauteur — une mesure qui additionnerait les
        // colonnes signalerait tous les blocs en colonnes du monde.
        var lignes = Composer("a\nb\nc\nd\ne", 34, 2, Trois);

        Assert.Equal(0, Compositeur.Debordement(lignes, 2, margeBasse: 0, VerticalAlign.Top));
    }

    [Fact]
    public void Un_bloc_vide_ne_deborde_pas()
    {
        // Quel que soit le fer : un bloc sans ligne n'a rien à couper. Le calage, lui,
        // rend bien la moitié de la zone — il n'y a rien à y poser, et c'est sans
        // conséquence ; ce qui compte est qu'il ne fasse pas croire à un débordement.
        foreach (var fer in Enum.GetValues<VerticalAlign>())
        {
            Assert.Equal(0, Compositeur.Debordement([], 10, margeBasse: 0, fer));
        }
    }

    [Theory]
    [InlineData(VerticalAlign.Top, 0)]
    [InlineData(VerticalAlign.Middle, 3.5)]
    [InlineData(VerticalAlign.Bottom, 7)]
    public void Le_calage_vertical_descend_le_texte_dans_sa_zone(VerticalAlign fer, double attendu)
    {
        // Trois lignes d'une unité dans dix unités de haut : sept unités de reste, que
        // le milieu partage et que le bas prend en entier.
        Assert.Equal(attendu, Compositeur.Decalage(Lignes("a\nb\nc", 10), 10, fer), precision: 9);
    }

    [Theory]
    [InlineData(VerticalAlign.Top)]
    [InlineData(VerticalAlign.Middle)]
    [InlineData(VerticalAlign.Bottom)]
    public void Un_texte_plus_haut_que_sa_zone_reste_cale_en_haut(VerticalAlign fer)
    {
        // C'est le seul bloc des deux documents réels qui porte un calage vertical :
        // « draw:textarea-vertical-align: middle » sur une zone de 19,43 pt dont le
        // texte en compose 26,86. Centrer escamoterait le début, et c'est le début
        // qu'on lit ; caler en bas l'escamoterait deux fois plus.
        var lignes = Lignes("a\nb\nc\nd\ne", 3);

        Assert.Equal(0, Compositeur.Decalage(lignes, 3, fer));
        // Et le débordement ne dépend donc pas du calage : ce qui dépasse dépasse par
        // le bas, quel que soit le fer demandé.
        Assert.Equal(2, Compositeur.Debordement(lignes, 3, margeBasse: 0, fer), precision: 9);
    }

    [Fact]
    public void Le_calage_en_bas_fait_deborder_ce_qui_debordait_deja_par_le_haut()
    {
        // Un texte calé en bas descend jusqu'au bord de sa zone : il ne peut pas
        // déborder tant qu'il y tient, et la marge basse ne l'absorbe plus.
        var lignes = Lignes("a\nb\nc", 10);

        Assert.Equal(0, Compositeur.Debordement(lignes, 10, margeBasse: 2, VerticalAlign.Bottom));
        Assert.Equal(7, Compositeur.Decalage(lignes, 10, VerticalAlign.Bottom), precision: 9);
    }

    [Fact]
    public void Un_demi_point_de_debordement_ne_se_signale_pas()
    {
        // Le seuil n'absorbe pas une erreur d'arrondi — elle est de l'ordre du
        // millionième — mais le débordement qui ne coûte rien à voir, et il laisse aux
        // deux règles à mesurer, celle d'Avalonia et celle de PDFsharp, une fenêtre où
        // s'accorder.
        var lignes = Lignes("a\nb", 1);

        Assert.True(Compositeur.Debordement(lignes, 1, margeBasse: 0.6, VerticalAlign.Top) > 0);
        Assert.False(Compositeur.Deborde(lignes, 1, margeBasse: 0.6, VerticalAlign.Top));
        Assert.True(Compositeur.Deborde(lignes, 1, margeBasse: 0.4, VerticalAlign.Top));
    }
}
