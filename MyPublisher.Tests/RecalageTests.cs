using System;
using System.Collections.Generic;
using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Le recalage des fragments et des écarts de paragraphe quand le texte change — le
/// poste Q1.
///
/// Les premiers cas sont ceux que la recette de PLAN.md écrit, chiffre pour chiffre.
/// Les suivants sont les bornes, puis les pièges du branchement : tout ce qui écrit le
/// texte d'un bloc sans le modifier — lecture d'un fichier, copie, cases d'un tableau —
/// ne doit RIEN recaler.
/// </summary>
public class RecalageTests
{
    private static readonly Plage Def = new(4, 3);

    /// <summary>Le fragment unique recalé, ou null s'il a disparu.</summary>
    private static Plage? Fragment(string avant, string apres, Plage fragment) =>
        Recalage.Recaler(avant, apres, [fragment], []).Fragments.Single();

    private static Plage? Ecart(string avant, string apres, Plage ecart) =>
        Recalage.Recaler(avant, apres, [], [ecart]).Ecarts.Single();

    // ------------------------------------------------------------- la recette de PLAN.md

    [Fact]
    public void Inserer_en_tete_decale_le_fragment()
    {
        Assert.Equal(new Plage(6, 3), Fragment("abc def", "XXabc def", Def));
    }

    [Fact]
    public void Inserer_dans_le_fragment_lallonge()
    {
        Assert.Equal(new Plage(4, 5), Fragment("abc def", "abc dXXef", Def));
    }

    [Fact]
    public void Supprimer_a_cheval_retaille_le_fragment()
    {
        // « c d » ôté : il reste « ef » du gras, qui commence là où « c » était.
        Assert.Equal(new Plage(2, 2), Fragment("abc def", "abef", Def));
    }

    [Fact]
    public void Supprimer_tout_le_fragment_le_retire()
    {
        Assert.Null(Fragment("abc def", "abc ", Def));
    }

    [Fact]
    public void Un_saut_de_ligne_dans_le_premier_paragraphe_decale_lecart_du_troisieme()
    {
        Assert.Equal(new Plage(3, 1), Ecart("Un\nDeux\nTrois", "U\nn\nDeux\nTrois", new Plage(2, 1)));
    }

    // ------------------------------------------------------- la règle du caractère d'avant

    [Fact]
    public void Inserer_a_la_fin_du_fragment_lallonge()
    {
        // Le caractère d'avant est le « f » gras : le « s » tapé derrière l'est aussi.
        Assert.Equal(new Plage(4, 4), Fragment("abc def", "abc defs", Def));
    }

    [Fact]
    public void Inserer_au_debut_du_fragment_le_decale()
    {
        // Le caractère d'avant est l'espace, au style du bloc.
        Assert.Equal(new Plage(6, 3), Fragment("abc def", "abc XXdef", Def));
    }

    [Fact]
    public void En_tete_de_texte_rien_ne_precede_et_le_fragment_se_decale()
    {
        Assert.Equal(new Plage(2, 3), Fragment("abc def", "XXabc def", new Plage(0, 3)));
    }

    [Fact]
    public void Un_fragment_avant_ledition_ne_bouge_pas()
    {
        Assert.Equal(new Plage(0, 3), Fragment("abc def", "abc dXef", new Plage(0, 3)));
        Assert.Equal(new Plage(0, 3), Fragment("abc def", "abc de", new Plage(0, 3)));
    }

    [Fact]
    public void Une_suppression_avant_le_fragment_le_ramene()
    {
        Assert.Equal(new Plage(2, 3), Fragment("abc def", "c def", Def));
    }

    [Fact]
    public void Remplacer_au_milieu_du_fragment_le_garde()
    {
        // « e » remplacé par « XY » : le caractère d'avant, « d », est gras.
        Assert.Equal(new Plage(4, 4), Fragment("abc def", "abc dXYf", Def));
    }

    [Fact]
    public void Remplacer_tout_le_fragment_le_retire()
    {
        // Le mot gras sélectionné et retapé prend le style de ce qui le précède : la
        // règle est la même que pour la frappe, et c'est elle que le poste a fixée.
        Assert.Null(Fragment("abc def", "abc XYZ", Def));
    }

    [Fact]
    public void Remplacer_tout_le_texte_ne_laisse_aucun_fragment()
    {
        var fragments = new[] { new Plage(0, 3), Def, new Plage(2, 4) };

        var (recales, _) = Recalage.Recaler("abc def", "tout autre chose, plus long", fragments, []);

        Assert.All(recales, Assert.Null);
    }

    [Fact]
    public void Un_fragment_hors_bornes_y_est_ramene()
    {
        // Un import qui a mal compté peut écrire un fragment qui déborde. La composition
        // le ramenait déjà à ce qu'il couvre ; le recalage fait de même, et ne laisse
        // jamais un fragment au-delà du texte.
        Assert.Equal(new Plage(4, 4), Fragment("abc def", "abc defX", new Plage(4, 50)));
        Assert.Null(Fragment("abc def", "Xabc def", new Plage(40, 3)));
        Assert.Equal(new Plage(4, 3), Fragment("abc def", "abc deX", new Plage(4, int.MaxValue)));
    }

    [Fact]
    public void Aucune_edition_ne_laisse_un_fragment_hors_bornes()
    {
        // Le balayage plutôt que trois exemples : toutes les éditions d'un caractère,
        // toutes les suppressions et tous les remplacements d'un texte court, sur tous
        // les fragments qu'il peut porter.
        const string texte = "ab\ncd e";
        var fragments = new List<Plage>();
        for (var debut = 0; debut <= texte.Length; debut++)
        for (var longueur = 0; longueur <= texte.Length - debut + 1; longueur++)
            fragments.Add(new Plage(debut, longueur));

        foreach (var apres in Variantes(texte))
        {
            var (recales, ecarts) = Recalage.Recaler(texte, apres, fragments, fragments);
            var paragraphes = apres.Count(c => c == '\n') + 1;

            foreach (var plage in recales.OfType<Plage>())
            {
                Assert.True(plage.Length > 0 && plage.Start >= 0 && plage.Start + plage.Length <= apres.Length,
                    $"« {texte} » → « {apres} » : fragment {plage} hors de 0..{apres.Length}");
            }

            foreach (var plage in ecarts.OfType<Plage>())
            {
                Assert.True(plage.Length > 0 && plage.Start >= 0 && plage.Start + plage.Length <= paragraphes,
                    $"« {texte} » → « {apres} » : écart {plage} hors de 0..{paragraphes}");
            }
        }
    }

    private static IEnumerable<string> Variantes(string texte)
    {
        for (var debut = 0; debut <= texte.Length; debut++)
        {
            foreach (var insere in new[] { "", "Z", "\n", "Z\nZ" })
            {
                for (var supprimes = 0; supprimes <= texte.Length - debut; supprimes++)
                {
                    yield return texte[..debut] + insere + texte[(debut + supprimes)..];
                }
            }
        }

        yield return "";
    }

    // ---------------------------------------------------------------------- la déduction

    [Fact]
    public void Ledition_se_deduit_du_prefixe_et_du_suffixe_communs()
    {
        Assert.Equal(new Edition(4, 0, 2), Edition.Deduire("abc def", "abc XXdef"));
        Assert.Equal(new Edition(2, 3, 0), Edition.Deduire("abc def", "abef"));
        Assert.Equal(new Edition(4, 3, 3), Edition.Deduire("abc def", "abc XYZ"));
        Assert.Equal(new Edition(0, 7, 0), Edition.Deduire("abc def", ""));
        Assert.Equal(new Edition(7, 0, 0), Edition.Deduire("abc def", "abc def"));
    }

    [Fact]
    public void Lambiguite_place_linsertion_le_plus_a_droite()
    {
        // « d » tapé devant « def » ou derrière son « d » : les deux textes sont les
        // mêmes. La déduction choisit la droite — c'est écrit, et voici où.
        Assert.Equal(new Edition(5, 0, 1), Edition.Deduire("abc def", "abc ddef"));
    }

    [Fact]
    public void Une_paire_de_substitution_nest_pas_coupee()
    {
        // 😀 et 😃 partagent leur premier demi-caractère.
        var edition = Edition.Deduire("a\U0001F600b", "a\U0001F603b");

        Assert.Equal(new Edition(1, 2, 2), edition);
    }

    // ---------------------------------------------------------------------- paragraphes

    [Fact]
    public void Taper_sans_saut_de_ligne_ne_bouge_aucun_ecart()
    {
        Assert.Equal(new Plage(1, 1), Ecart("Un\nDeux\nTrois", "Un\nDeuxième\nTrois", new Plage(1, 1)));
    }

    [Fact]
    public void Couper_un_paragraphe_donne_son_reglage_aux_deux_moities()
    {
        // Un retour à la ligne au milieu d'un élément de liste : la suite reste en liste.
        Assert.Equal(new Plage(1, 2), Ecart("Un\nDeux\nTrois", "Un\nDe\nux\nTrois", new Plage(1, 1)));
    }

    [Fact]
    public void Un_saut_tape_au_bout_dun_paragraphe_le_continue()
    {
        // « Deux » est un élément de liste, « Trois » n'en est pas. Un retour à la ligne
        // au bout de « Deux » ouvre un élément de liste de plus — et non un paragraphe
        // au réglage de « Trois », ce que la seule déduction aurait donné.
        const string avant = "Un\nDeux\nTrois", apres = "Un\nDeux\n\nTrois";

        Assert.Equal(new Plage(1, 2), Ecart(avant, apres, new Plage(1, 1)));
        Assert.Equal(new Plage(3, 1), Ecart(avant, apres, new Plage(2, 1)));
    }

    [Fact]
    public void Un_paragraphe_fusionne_garde_lecart_du_premier()
    {
        // Effacement en tête de « Deux » : il rejoint « Un », qui garde son réglage, et
        // celui de « Deux » disparaît avec lui.
        const string avant = "Un\nDeux\nTrois", apres = "UnDeux\nTrois";

        Assert.Equal(new Plage(0, 1), Ecart(avant, apres, new Plage(0, 1)));
        Assert.Null(Ecart(avant, apres, new Plage(1, 1)));
        Assert.Equal(new Plage(1, 1), Ecart(avant, apres, new Plage(2, 1)));
    }

    [Fact]
    public void Supprimer_une_ligne_entiere_laisse_la_suivante_a_son_reglage()
    {
        // « Deux\n » sélectionné de bout en bout et effacé : c'est « Deux » qui part, et
        // « Trois » garde le sien. Fusionner « au premier » ici donnerait à « Trois » le
        // retrait de la ligne effacée.
        const string avant = "Un\nDeux\nTrois", apres = "Un\nTrois";

        Assert.Equal(new Plage(0, 1), Ecart(avant, apres, new Plage(0, 1)));
        Assert.Null(Ecart(avant, apres, new Plage(1, 1)));
        Assert.Equal(new Plage(1, 1), Ecart(avant, apres, new Plage(2, 1)));
    }

    [Fact]
    public void Un_ecart_sur_plusieurs_paragraphes_se_raccourcit_dune_fusion()
    {
        Assert.Equal(new Plage(0, 2), Ecart("Un\nDeux\nTrois", "UnDeux\nTrois", new Plage(0, 3)));
    }

    [Fact]
    public void Un_retour_chariot_suivi_dun_saut_compte_pour_un_seul_paragraphe()
    {
        // La composition compte « \r\n » pour un saut : le recalage aussi, sans quoi
        // l'écart se poserait un paragraphe trop loin.
        Assert.Equal(new Plage(2, 1), Ecart("Un\r\nDeux\r\nTrois", "Un\r\nDeuxième\r\nTrois", new Plage(2, 1)));
        Assert.Equal(new Plage(3, 1), Ecart("Un\r\nDeux\r\nTrois", "\nUn\r\nDeux\r\nTrois", new Plage(2, 1)));
    }

    // ---------------------------------------------------------------- sur le modèle

    private static TextFrame Bloc()
    {
        var bloc = new TextFrame { Text = "abc def\nsuite" };
        bloc.Spans.Add(new StyleSpan { Start = 4, Length = 3, Bold = true });
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 3, Italic = true });
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 1, Length = 1, IndentLeft = 12 });
        return bloc;
    }

    [Fact]
    public void La_saisie_recale_les_fragments_et_les_ecarts_du_bloc()
    {
        var bloc = Bloc();
        var gras = bloc.Spans[0];

        bloc.Saisie = "Mot\nabc def\nsuite";

        Assert.Equal("Mot\nabc def\nsuite", bloc.Text);
        // L'objet est gardé, sa plage a changé : qui l'observait l'observe encore.
        Assert.Same(gras, bloc.Spans[0]);
        Assert.Equal((8, 3), (gras.Start, gras.Length));
        Assert.Equal("def", bloc.Text.Substring(gras.Start, gras.Length));
        Assert.Equal(2, bloc.Paragraphs[0].Start);
    }

    [Fact]
    public void La_saisie_retire_les_fragments_vides()
    {
        var bloc = Bloc();

        bloc.Saisie = "abc \nsuite";

        Assert.Single(bloc.Spans);
        Assert.True(bloc.Spans[0].Italic);
    }

    [Fact]
    public void Une_case_de_tableau_recale_ses_fragments_a_la_saisie()
    {
        var cellule = new TableCell { Text = "abc def" };
        cellule.Spans.Add(new StyleSpan { Start = 4, Length = 3, Bold = true });

        cellule.Saisie = "XXabc def";

        Assert.Equal((6, 3), (cellule.Spans[0].Start, cellule.Spans[0].Length));
    }

    // ------------------------------------------------- ce qui écrit le texte sans l'éditer

    [Fact]
    public void Ecrire_le_texte_directement_ne_recale_rien()
    {
        // Le contrat sur lequel tiennent la lecture, l'import, la copie et l'annulation.
        var bloc = Bloc();

        bloc.Text = "XXXXXXXXXXXXXXXXXXXX";

        Assert.Equal((4, 3), (bloc.Spans[0].Start, bloc.Spans[0].Length));
        Assert.Equal(1, bloc.Paragraphs[0].Start);
    }

    [Fact]
    public void Un_fichier_qui_donne_les_fragments_avant_le_texte_se_relit_intact()
    {
        // Le sérialiseur pose les propriétés dans l'ordre du FICHIER, et un fichier
        // écrit à la main peut donner les collections d'abord. Un recalage déclenché par
        // le texte posé ensuite pousserait tout de la longueur du texte.
        const string json = """
            {
              "FormatVersion": 4,
              "Title": "Ordre",
              "Pages": [ { "Name": "Page 1", "Frames": [ {
                "$kind": "text", "Name": "Bloc", "X": 10, "Y": 10, "Width": 200, "Height": 100,
                "Spans": [ { "Start": 4, "Length": 3, "Bold": true } ],
                "Paragraphs": [ { "Start": 1, "Length": 1, "IndentLeft": 12 } ],
                "Text": "abc def\nsuite"
              } ] } ]
            }
            """;

        var bloc = (TextFrame)DocumentSerializer.FromJson(json).Pages[0].Frames[0];

        Assert.Equal((4, 3), (bloc.Spans[0].Start, bloc.Spans[0].Length));
        Assert.Equal(1, bloc.Paragraphs[0].Start);
    }

    [Fact]
    public void Un_aller_retour_par_le_fichier_rend_les_memes_fragments()
    {
        // C'est par ce chemin que l'annulation rend l'état d'avant.
        var bloc = Bloc();
        bloc.Saisie = "Mot abc def\nsuite";
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        document.Pages[0].Frames.Add(bloc);

        var json = DocumentSerializer.ToJson(document);

        Assert.Equal(json, DocumentSerializer.ToJson(DocumentSerializer.FromJson(json)));
    }

    [Fact]
    public void Une_copie_ne_recale_rien()
    {
        var bloc = Bloc();

        var copie = (TextFrame)bloc.Clone();

        Assert.Equal((4, 3), (copie.Spans[0].Start, copie.Spans[0].Length));
        Assert.Equal(1, copie.Paragraphs[0].Start);
    }

    [Fact]
    public void Lire_les_cases_dun_tableau_ne_deplace_pas_leurs_fragments()
    {
        // « CellFrames » prête les fragments de la case au bloc qu'il calcule, et le
        // recalcule à chaque lecture. Si la construction de ce bloc recalait, chaque
        // redessin déplacerait les gras de la case.
        var tableau = new TableFrame();
        tableau.RowCount = 1;
        tableau.ColumnCount = 1;
        tableau.Cells[0].Text = "abc def";
        tableau.Cells[0].Spans.Add(new StyleSpan { Start = 4, Length = 3, Bold = true });

        for (var i = 0; i < 3; i++) _ = tableau.CellFrames;
        var bloc = (TextFrame)tableau.CellFrames[0];

        Assert.Same(tableau.Cells[0].Spans, bloc.Spans);
        Assert.Equal((4, 3), (tableau.Cells[0].Spans[0].Start, tableau.Cells[0].Spans[0].Length));
    }

    [Fact]
    public void Limport_dun_releve_ne_recale_rien()
    {
        // L'import pose le texte puis les fragments ; quel que soit l'ordre, le gras
        // doit rester sur le mot que la source met en gras.
        var document = PubImporter.Importer("""
            startDocument()
              startPage(svg:height: 11.6929in, svg:width: 8.2677in)
                startTextObject (svg:height: 1.0000in, svg:width: 2.0000in, svg:x: 0.5000in, svg:y: 1.5000in)
                  openParagraph (fo:text-align: left)
                    openSpan(fo:font-size: 0.1389in, style:font-name: Calibri)
                      insertText (Un mot )
                    closeSpan
                    openSpan(fo:font-size: 0.1389in, fo:font-weight: bold, style:font-name: Calibri)
                      insertText (gras)
                    closeSpan
                  closeParagraph
                endTextObject
            """).Document;

        var bloc = (TextFrame)document.Pages[0].Frames.Single();
        var gras = StyleEffectif.Decouper(bloc.Text, bloc.Spans,
                new StyleEffectif(bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic, bloc.Foreground, 0))
            .Where(m => m.Style.Bold).Select(m => m.Texte);

        Assert.Equal(["gras"], gras);
    }
}
