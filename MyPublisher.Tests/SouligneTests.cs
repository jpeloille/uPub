using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Le souligné — poste I7 —, éprouvé dans le noyau : sa forme dans le fichier, sa place
/// dans le style effectif, les étendues que la composition souligne, la géométrie de ses
/// traits, la frappe, la copie et l'import.
///
/// Ce qui se dessine n'est pas éprouvé ici, mais par les tests d'interface et par la
/// recette, qui mesurent l'encre de l'écran et du PDF contre la table <c>post</c> de la
/// police : une géométrie juste dans le noyau et oubliée par un rendu ne se verrait pas
/// d'ici.
/// </summary>
public class SouligneTests
{
    // ------------------------------------------------------------------ les sortes

    /// <summary>
    /// Les dix-sept sortes de Publisher, telles que libmspub les range dans les quatre
    /// réglages d'OpenDocument (<c>fillUnderline</c>, <c>MSPUBCollector.cpp:254-330</c>) :
    /// le trait, le type, l'épaisseur, le mode. La dix-septième, le tiret long épais, y
    /// arrive comme le tiret long ordinaire — libmspub ne la range pas parmi les épaisses.
    /// </summary>
    public static readonly (string Nom, string Releve, Souligne Attendu)[] SortesDeLibmspub =
    [
        ("Single", "solid, single, auto, continuous", Souligne.Simple),
        ("WordsOnly", "solid, single, auto, skip-white-space", new(TraitDeSouligne.Solid, wordsOnly: true)),
        ("Double", "solid, double, auto, continuous", new(TraitDeSouligne.Solid, @double: true)),
        ("Dotted", "dotted, single, auto, continuous", new(TraitDeSouligne.Dotted)),
        ("Thick", "solid, single, bold, continuous", new(TraitDeSouligne.Solid, thick: true)),
        ("Dash", "dash, single, auto, continuous", new(TraitDeSouligne.Dash)),
        ("DotDash", "dot-dash, single, auto, continuous", new(TraitDeSouligne.DotDash)),
        ("DotDotDash", "dot-dot-dash, single, auto, continuous", new(TraitDeSouligne.DotDotDash)),
        ("Wave", "wave, single, auto, continuous", new(TraitDeSouligne.Wave)),
        ("ThickWave", "wave, single, bold, continuous", new(TraitDeSouligne.Wave, thick: true)),
        ("ThickDot", "dotted, single, bold, continuous", new(TraitDeSouligne.Dotted, thick: true)),
        ("ThickDash", "dash, single, bold, continuous", new(TraitDeSouligne.Dash, thick: true)),
        ("ThickDotDash", "dot-dash, single, bold, continuous", new(TraitDeSouligne.DotDash, thick: true)),
        ("ThickDotDotDash", "dot-dot-dash, single, bold, continuous", new(TraitDeSouligne.DotDotDash, thick: true)),
        ("LongDash", "long-dash, single, auto, continuous", new(TraitDeSouligne.LongDash)),
        ("ThickLongDash", "long-dash, single, auto, continuous", new(TraitDeSouligne.LongDash)),
        ("DoubleWave", "wave, double, auto, continuous", new(TraitDeSouligne.Wave, @double: true)),
    ];

    public static TheoryData<string> NomsDesSortes() => [.. SortesDeLibmspub.Select(s => s.Nom)];

    private static Souligne Sorte(string nom) => SortesDeLibmspub.Single(s => s.Nom == nom).Attendu;

    // ------------------------------------------------------------------ le fichier

    private static Document Feuille(params Frame[] blocs)
    {
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        foreach (var bloc in blocs) document.Pages[0].Frames.Add(bloc);
        return document;
    }

    private static TextFrame Relire(Document document) =>
        (TextFrame)DocumentSerializer.FromJson(DocumentSerializer.ToJson(document)).Pages[0].Frames[0];

    [Fact]
    [Trait("poste", "I7")]
    public void Le_trait_simple_secrit_true_au_bloc_et_au_fragment_et_se_relit()
    {
        var bloc = new TextFrame { Text = "un deux trois", Underline = Souligne.Simple };
        bloc.Spans.Add(new StyleSpan { Start = 3, Length = 4, Underline = Souligne.Simple });

        var json = DocumentSerializer.ToJson(Feuille(bloc));

        // La forme courte, et elle seule : un objet pour le trait simple alourdirait tous
        // les documents qui ne soulignent qu'au clavier.
        Assert.Equal(2, json.Split("\"Underline\": true").Length - 1);

        var relu = Relire(Feuille(bloc));
        Assert.Equal(Souligne.Simple, relu.Underline);
        Assert.Equal(Souligne.Simple, relu.Spans[0].Underline);
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Sans_souligne_le_bloc_ecrit_false_et_le_fragment_null()
    {
        var bloc = new TextFrame { Text = "un deux" };
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 2, Bold = true });

        var json = DocumentSerializer.ToJson(Feuille(bloc));

        Assert.Contains("\"Underline\": false", json);
        Assert.Contains("\"Underline\": null", json);
        var relu = Relire(Feuille(bloc));
        Assert.Equal(Souligne.Aucun, relu.Underline);
        Assert.Null(relu.Spans[0].Underline);
    }

    [Theory]
    [Trait("poste", "I7")]
    [MemberData(nameof(NomsDesSortes))]
    public void Chaque_sorte_fait_laller_retour_au_bloc_et_au_fragment(string nom)
    {
        var sorte = Sorte(nom);
        var bloc = new TextFrame { Text = "un deux", Underline = sorte };
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 2, Underline = sorte });

        var document = Feuille(bloc);
        var json = DocumentSerializer.ToJson(document);
        var relu = Relire(document);

        Assert.Equal(sorte, relu.Underline);
        Assert.Equal(sorte, relu.Spans[0].Underline);
        // Au caractère près : relu puis réécrit, le fichier ne bouge plus.
        Assert.Equal(json, DocumentSerializer.ToJson(DocumentSerializer.FromJson(json)));
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Une_sorte_secrit_en_objet_avec_ses_quatre_reglages()
    {
        var bloc = new TextFrame { Text = "un", Underline = new Souligne(TraitDeSouligne.Wave, @double: true) };

        var json = DocumentSerializer.ToJson(Feuille(bloc));

        Assert.Contains("\"Line\": \"Wave\"", json);
        Assert.Contains("\"Double\": true", json);
        Assert.Contains("\"Thick\": false", json);
        Assert.Contains("\"WordsOnly\": false", json);
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Un_objet_sans_trait_vaut_le_trait_plein_et_un_trait_inconnu_est_refuse()
    {
        var options = new JsonSerializerOptions();

        Assert.Equal(new Souligne(TraitDeSouligne.Solid, @double: true),
            JsonSerializer.Deserialize<Souligne>("{\"Double\": true}", options));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Souligne>("{\"Line\": \"Zigzag\"}", options));
        // Un nombre n'est pas un nom, même quand il tomberait sur une valeur.
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Souligne>("{\"Line\": \"3\"}", options));
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Un_souligne_sans_trait_na_quune_valeur()
    {
        // Sans cela, deux « pas de souligné » ne seraient pas égaux, et la découpe du texte
        // couperait un mot entre deux styles que rien ne distingue.
        Assert.Equal(Souligne.Aucun, new Souligne(TraitDeSouligne.None, @double: true, thick: true, wordsOnly: true));
        Assert.Equal(Souligne.Aucun,
            JsonSerializer.Deserialize<Souligne>("{\"Line\": \"None\", \"Double\": true}"));
        Assert.Equal("false", JsonSerializer.Serialize(new Souligne(TraitDeSouligne.None, thick: true)));
    }

    // ------------------------------------------------------------------ le style effectif

    private static readonly StyleEffectif Commun =
        new("Police", 1, false, false, Souligne.Simple, "#FF000000", 0);

    private static Souligne[] SouligneParCaractere(string texte, params StyleSpan[] fragments) =>
        StyleEffectif.Decouper(texte, fragments, Commun)
            .SelectMany(m => Enumerable.Repeat(m.Style.Underline, m.Texte.Length)).ToArray();

    [Fact]
    [Trait("poste", "I7")]
    public void Le_fragment_dit_son_souligne_et_nul_laisse_celui_du_bloc()
    {
        var double_ = new Souligne(TraitDeSouligne.Solid, @double: true);
        var styles = SouligneParCaractere("aa bb cc dd",
            new StyleSpan { Start = 0, Length = 2, Underline = Souligne.Aucun },
            new StyleSpan { Start = 3, Length = 2, Underline = double_ },
            new StyleSpan { Start = 6, Length = 2, Bold = true });

        Assert.Equal(Souligne.Aucun, styles[0]);
        Assert.Equal(double_, styles[3]);
        // Un fragment qui ne dit rien du souligné garde celui du bloc.
        Assert.Equal(Souligne.Simple, styles[6]);
        Assert.Equal(Souligne.Simple, styles[9]);
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Le_style_commun_dun_bloc_porte_son_souligne()
    {
        var bloc = new TextFrame { Underline = new Souligne(TraitDeSouligne.Dotted) };

        Assert.Equal(new Souligne(TraitDeSouligne.Dotted), Charte.Defaut.StyleCommun(bloc).Underline);
    }

    // ------------------------------------------------------------------ les étendues

    /// <summary>Un caractère vaut une unité, comme dans les tests de la composition.</summary>
    private sealed class ChasseFixe : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => texte.Length * style.FontSize;
        public double LargeurDeCoupure(string texte, StyleEffectif style) => Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => style.FontSize;
    }

    private static (LigneComposee Ligne, IReadOnlyList<double> Decalages) PremiereLigne(
        string texte, double largeur, TextAlign fer, Souligne souligne, params StyleSpan[] fragments)
    {
        var commun = Commun with { Underline = souligne };
        var lignes = Compositeur.Composer(texte, fragments, commun, [],
            new ParagrapheEffectif(0, 0, 0, 0, 0, fer), largeur, 1000, new Colonnage(1, 0), 1, new ChasseFixe());
        var ligne = lignes[0];
        var justifiee = ligne.Fer == TextAlign.Justify && !ligne.FinDeParagraphe;
        return (ligne, justifiee ? Compositeur.Decalages(ligne) : new double[ligne.Morceaux.Count]);
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Un_souligne_continu_passe_sous_les_blancs_elargis_dune_ligne_justifiee()
    {
        // Sur 10, la première ligne « un deux » (7) s'étire de trois sous son seul blanc.
        var (ligne, decalages) = PremiereLigne("un deux trois quatre", 10, TextAlign.Justify, Souligne.Simple);
        Assert.Equal("un deux", ligne.Texte);
        Assert.True(decalages[^1] > 0, "la ligne n'est pas justifiée : le test ne prouverait rien");

        var etendues = Compositeur.Soulignes(ligne, decalages, new ChasseFixe());

        // Un seul trait, d'un bord à l'autre de la colonne — et non un par mot, avec un
        // trou de la largeur du blanc ajouté.
        var etendue = Assert.Single(etendues);
        Assert.Equal(0, etendue.Debut, 9);
        Assert.Equal(10, etendue.Fin, 9);
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Mots_seuls_ne_passe_sous_aucun_blanc_ni_sous_une_tabulation()
    {
        var motsSeuls = new Souligne(TraitDeSouligne.Solid, wordsOnly: true);
        var (ligne, decalages) = PremiereLigne("un  deux\ttrois", 100, TextAlign.Left, motsSeuls);

        var etendues = Compositeur.Soulignes(ligne, decalages, new ChasseFixe());

        Assert.Equal([(0.0, 2.0), (4.0, 8.0), (Compositeur.IntervalleTaquet, Compositeur.IntervalleTaquet + 5)],
            etendues.Select(e => (e.Debut, e.Fin)));
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Mots_seuls_laisse_nus_les_blancs_elargis()
    {
        var motsSeuls = new Souligne(TraitDeSouligne.Solid, wordsOnly: true);
        var (ligne, decalages) = PremiereLigne("un deux trois quatre", 10, TextAlign.Justify, motsSeuls);

        var etendues = Compositeur.Soulignes(ligne, decalages, new ChasseFixe());

        // « deux » commence après le blanc élargi de trois, et finit au bord de la colonne.
        Assert.Equal([(0.0, 2.0), (6.0, 10.0)], etendues.Select(e => (e.Debut, e.Fin)));
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Un_changement_de_style_coupe_le_trait_et_deux_mots_du_meme_style_nen_font_quun()
    {
        var (ligne, decalages) = PremiereLigne("un deux trois quatre", 100, TextAlign.Left, Souligne.Simple,
            new StyleSpan { Start = 8, Length = 5, Bold = true },
            new StyleSpan { Start = 14, Length = 6, Underline = Souligne.Aucun });

        var etendues = Compositeur.Soulignes(ligne, decalages, new ChasseFixe());

        // « un deux » d'un trait, l'espace qui suit compris ; « trois » en gras d'un autre,
        // puis le blanc qui le suit, qui n'est pas gras ; « quatre » n'est pas souligné.
        Assert.Equal([(0.0, 8.0, false), (8.0, 13.0, true), (13.0, 14.0, false)],
            etendues.Select(e => (e.Debut, e.Fin, e.Style.Bold)));
    }

    // ------------------------------------------------------------------ les traits

    [Fact]
    [Trait("poste", "I7")]
    public void Le_haut_du_premier_trait_reste_ou_la_police_le_met()
    {
        const double haut = 0.6, ep = 0.8;

        foreach (var (nom, _, sorte) in SortesDeLibmspub)
        {
            var traits = sorte.Traits(haut, ep);
            var premier = traits[0];
            var amplitude = sorte.Ondule ? premier.Epaisseur : 0;
            Assert.True(Math.Abs(premier.Axe - amplitude - premier.Epaisseur / 2 - haut) < 1e-9, nom);
            Assert.Equal(sorte.Thick ? 2 * ep : ep, premier.Epaisseur, 9);
            Assert.Equal(sorte.Double ? 2 : 1, traits.Count);
        }
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Le_second_trait_dun_double_est_a_une_epaisseur_du_premier()
    {
        var traits = new Souligne(TraitDeSouligne.Solid, @double: true).Traits(0.5, 1);
        Assert.Equal(1, traits[1].Axe - traits[0].Axe - traits[0].Epaisseur, 9);

        // Pour une vague, le blanc se compte entre le creux de la première et la crête de
        // la seconde.
        var vagues = new Souligne(TraitDeSouligne.Wave, @double: true).Traits(0.5, 1);
        Assert.Equal(1, (vagues[1].Axe - 1 - 0.5) - (vagues[0].Axe + 1 + 0.5), 9);
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Sans_souligne_rien_ne_se_trace()
    {
        Assert.Empty(Souligne.Aucun.Traits(0.5, 1));
    }

    [Fact]
    [Trait("poste", "I7")]
    public void La_vague_ondule_dune_epaisseur_et_finit_au_bout_du_trait()
    {
        var points = Souligne.Vague(10, 31, 5, 2);

        Assert.Equal((10.0, 5.0), points[0]);
        Assert.Equal(31, points[^1].X, 9);
        Assert.Equal(7, points.Max(p => p.Y), 9);
        Assert.Equal(3, points.Min(p => p.Y), 9);
        // Une période de six épaisseurs : la crête suivante douze points plus loin.
        Assert.Equal(points.First(p => p.Y >= 7 - 1e-9).X + 12, points.Last(p => p.Y >= 7 - 1e-9).X, 9);
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Une_police_sans_epaisseur_de_souligne_en_recoit_un_vingtieme_de_corps()
    {
        Assert.Equal((0.4, 1.0), Souligne.Mesure(0.4, 0, 20));
        Assert.Equal((0.4, 0.9), Souligne.Mesure(0.4, 0.9, 20));
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Chaque_trait_a_son_motif_et_le_plein_nen_a_pas()
    {
        Assert.Null(Souligne.Simple.Motif);
        Assert.Null(new Souligne(TraitDeSouligne.Wave).Motif);
        foreach (var trait in new[]
                 {
                     TraitDeSouligne.Dotted, TraitDeSouligne.Dash, TraitDeSouligne.LongDash,
                     TraitDeSouligne.DotDash, TraitDeSouligne.DotDotDash,
                 })
        {
            Assert.NotNull(new Souligne(trait).Motif);
        }

        // Cinq motifs, cinq dessins : deux sortes qui se dessineraient pareil ne se
        // reconnaîtraient pas.
        Assert.Equal(5, new[]
            {
                TraitDeSouligne.Dotted, TraitDeSouligne.Dash, TraitDeSouligne.LongDash,
                TraitDeSouligne.DotDash, TraitDeSouligne.DotDotDash,
            }
            .Select(t => string.Join(",", new Souligne(t).Motif!)).Distinct().Count());
    }

    // ------------------------------------------------------------------ la frappe, la copie

    [Fact]
    [Trait("poste", "I7")]
    public void Le_souligne_suit_la_frappe_comme_le_gras()
    {
        // Le recalage de Q1 déplace des plages : le souligné d'un fragment le suit sans
        // rien demander de plus. Une frappe au bout du fragment l'allonge, une frappe
        // devant le décale.
        var double_ = new Souligne(TraitDeSouligne.Solid, @double: true);
        var bloc = new TextFrame { Text = "abc def" };
        bloc.Spans.Add(new StyleSpan { Start = 4, Length = 3, Underline = double_ });

        bloc.Saisie = "XX abc def";
        bloc.Saisie = "XX abc defgh";

        var fragment = Assert.Single(bloc.Spans);
        Assert.Equal((7, 5), (fragment.Start, fragment.Length));
        Assert.Equal(double_, fragment.Underline);
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Une_copie_de_bloc_garde_son_souligne_et_celui_de_ses_fragments()
    {
        var bloc = new TextFrame { Text = "un deux", Underline = new Souligne(TraitDeSouligne.Dash, thick: true) };
        bloc.Spans.Add(new StyleSpan { Start = 3, Length = 4, Underline = new Souligne(TraitDeSouligne.Wave) });

        var copie = (TextFrame)bloc.Clone();

        Assert.Equal(bloc.Underline, copie.Underline);
        Assert.Equal(bloc.Spans[0].Underline, copie.Spans[0].Underline);
        // La charte copie les fragments qui font référence au jeu : le souligné y passe.
        bloc.Spans[0].Foreground = "scheme:accent1";
        Assert.Equal(new Souligne(TraitDeSouligne.Wave), Charte.Defaut.Fragments(bloc.Spans)[0].Underline);
    }

    // ------------------------------------------------------------------ l'import

    private static Document Importer(string releve) => PubImporter.Importer(releve).Document;

    private static string Fragment(string texte, string? souligne) =>
        $"""
                openSpan(fo:font-size: 0.1389in, style:font-name: Calibri{(souligne is null ? "" : ", " + souligne)})
                  insertText ({texte})
                closeSpan
        """;

    private static string Releve(params string[] fragments) => $"""
        startDocument()
          startPage(svg:height: 11.6929in, svg:width: 8.2677in)
            startTextObject (svg:height: 1.0000in, svg:width: 4.0000in, svg:x: 0.5000in, svg:y: 1.5000in)
              openParagraph (fo:text-align: left)
        {string.Join("\n", fragments)}
              closeParagraph
            endTextObject
          endPage
        """;

    /// <summary>Les quatre réglages dans l'ordre où libmspub les écrit.</summary>
    private static string Odf(string releve)
    {
        var r = releve.Split(", ");
        return $"style:text-underline-mode: {r[3]}, style:text-underline-style: {r[0]}, " +
               $"style:text-underline-type: {r[1]}, style:text-underline-width: {r[2]}";
    }

    [Theory]
    [Trait("poste", "I7")]
    [MemberData(nameof(NomsDesSortes))]
    public void Chaque_sorte_de_libmspub_simporte_en_la_sienne(string nom)
    {
        var (_, releve, attendu) = SortesDeLibmspub.Single(s => s.Nom == nom);

        // Un mot souligné au milieu d'un texte qui ne l'est pas : le bloc garde le style
        // le plus répandu, et le fragment dit sa sorte.
        var bloc = (TextFrame)Importer(Releve(
            Fragment("Le texte courant du bloc, ", null),
            Fragment("souligné", Odf(releve)),
            Fragment(" puis courant encore.", null))).Pages[0].Frames.Single();

        Assert.Equal(Souligne.Aucun, bloc.Underline);
        var styles = StyleEffectif.Decouper(bloc.Text, bloc.Spans, Charte.Defaut.StyleCommun(bloc));
        Assert.Equal(attendu, styles.Single(m => m.Texte == "souligné").Style.Underline);
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Les_dix_sept_sortes_de_libmspub_en_font_seize()
    {
        Assert.Equal(16, SortesDeLibmspub.Select(s => s.Attendu).Distinct().Count());
        Assert.Equal(Sorte("LongDash"), Sorte("ThickLongDash"));
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Un_bloc_souligne_de_bout_en_bout_porte_son_souligne_et_ses_fragments_le_lui_laissent()
    {
        var double_ = Odf("solid, double, auto, continuous");
        var bloc = (TextFrame)Importer(Releve(
            Fragment("Un long texte souligné deux fois, ", double_),
            $"""
                    openSpan(fo:font-size: 0.1389in, fo:font-weight: bold, style:font-name: Calibri, {double_})
                      insertText (gras)
                    closeSpan
            """,
            Fragment(" et la fin.", null))).Pages[0].Frames.Single();

        Assert.Equal(new Souligne(TraitDeSouligne.Solid, @double: true), bloc.Underline);
        // Le gras garde son fragment, mais il laisse au bloc le souligné qui est le sien :
        // souligner ou désouligner le bloc le touchera comme le reste.
        var gras = bloc.Spans.Single(f => f.Bold == true);
        Assert.Null(gras.Underline);
        // « et la fin. » n'est pas souligné, et le dit.
        var fin = bloc.Spans.Single(f => f.Length == " et la fin.".Length);
        Assert.Equal(Souligne.Aucun, fin.Underline);
    }

    [Fact]
    [Trait("poste", "I7")]
    public void Un_releve_sans_souligne_ne_souligne_rien()
    {
        var bloc = (TextFrame)Importer(Releve(Fragment("Rien ici.", null))).Pages[0].Frames.Single();

        Assert.Equal(Souligne.Aucun, bloc.Underline);
        Assert.All(bloc.Spans, f => Assert.Null(f.Underline));
    }
}
