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
        public double Naturelle(StyleEffectif style) => style.FontSize;
    }

    private static readonly StyleEffectif Commun =
        new("Police", 1, Bold: false, Italic: false, "#FF000000", 0);

    private static IReadOnlyList<LigneComposee> Composer(
        string texte, double largeur, params StyleSpan[] fragments) =>
        Compositeur.Composer(texte, fragments, Commun, [], default, largeur, new ChasseFixe());

    /// <summary>La même chose, avec des réglages de paragraphe plutôt que de style.</summary>
    private static IReadOnlyList<LigneComposee> Composer(
        string texte, double largeur, ParagrapheEffectif commun, params ParagraphSpan[] ecarts) =>
        Compositeur.Composer(texte, [], Commun, ecarts, commun, largeur, new ChasseFixe());

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

    // ----------------------------------------------- retraits et espacement (R2)

    /// <summary>Un réglage de paragraphe écrit en clair, plutôt que cinq nombres.</summary>
    private static ParagrapheEffectif Reglage(
        double gauche = 0, double droite = 0, double premiere = 0,
        double avant = 0, double apres = 0) => new(gauche, droite, premiere, avant, apres);

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

        Assert.Equal(12, Compositeur.Hauteur(lignes, 1), precision: 9);
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
}
