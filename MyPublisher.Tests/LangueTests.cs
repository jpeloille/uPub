using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// La langue du texte — poste N9 : <c>Document.Langue</c>, toujours là ;
/// <c>TextFrame.Langue</c> et <c>StyleSpan.Langue</c>, nulles pour « comme le bloc », puis
/// « comme le document ». L'import la lit sur chaque fragment du relevé de libmspub ;
/// l'enregistrement la garde ; un document vierge est en français quelle que soit la
/// machine ; la mise en forme d'une plage ne la perd pas ; la composition ne la voit pas.
///
/// Les relevés sont écrits à la main, comme ceux de <c>ImportPubTests</c>, dans la forme
/// que <c>pub2raw</c> leur donne : « fo:country: FR, …, fo:language: fr ». Les deux
/// documents réels sont lus par <c>LangueDuDocumentTests</c>, côté fenêtre, où le banc sait
/// trouver le lecteur.
/// </summary>
[Trait("poste", "N9")]
public class LangueTests
{
    private const string UnePage = """
        startDocument()
          startPage(svg:height: 11.6929in, svg:width: 8.2677in)
        """;

    /// <summary>Un bloc dont chaque paragraphe est un fragment, au style commun, dans la langue donnée.</summary>
    private static string Bloc(params (string Texte, string? Langue)[] fragments) =>
        "    startTextObject (svg:height: 1.0000in, svg:width: 4.0000in, svg:x: 0.5000in, svg:y: 0.5000in)\n" +
        string.Concat(fragments.Select(f =>
            "      openParagraph (fo:text-align: left)\n" +
            $"        openSpan(fo:color: #000000, {f.Langue}fo:font-size: 0.1528in, style:font-name: Calibri)\n" +
            $"          insertText ({f.Texte})\n" +
            "          insertText (\r)\n" +
            "        closeSpan\n" +
            "      closeParagraph\n")) +
        "    endTextObject\n";

    private const string Francais = "fo:country: FR, fo:language: fr, ";
    private const string Americain = "fo:country: US, fo:language: en, ";

    private static Document Importer(params string[] blocs) =>
        PubImporter.Importer(UnePage + "\n" + string.Concat(blocs)).Document;

    private static TextFrame Texte(Document document, int rang = 0) =>
        document.Pages[0].Frames.OfType<TextFrame>().ElementAt(rang);

    /// <summary>La langue de chaque caractère non blanc du bloc, en une chaîne par langue rencontrée.</summary>
    private static string[] Langues(Document document, TextFrame bloc) =>
        Enumerable.Range(0, bloc.Text.Length)
            .Where(i => !char.IsWhiteSpace(bloc.Text[i]))
            .Select(i => Models.Langues.DuCaractere(bloc, i, document))
            .Aggregate(new List<string>(), (suite, langue) =>
            {
                if (suite.Count == 0 || suite[^1] != langue) suite.Add(langue);
                return suite;
            })
            .ToArray();

    // ------------------------------------------------------------------ l'import

    [Fact]
    public void L_import_lit_la_langue_et_le_pays_de_chaque_fragment()
    {
        var document = Importer(Bloc(("Hello world", Americain), ("Bonjour", Francais), ("Goodbye", Americain)));
        var bloc = Texte(document);

        Assert.Equal("Hello world\nBonjour\nGoodbye", bloc.Text);
        Assert.Equal(new[] { "en-US", "fr-FR", "en-US" }, Langues(document, bloc));
    }

    [Fact]
    public void Un_fragment_qui_ne_differe_que_par_la_langue_reste_et_n_ecrit_qu_elle()
    {
        // Le titre français d'un bloc anglais, au même style que lui : sans la langue, le
        // fragment ne dirait rien, et l'import le retirait. Il reste, et ne dit qu'elle — il
        // ne fige ni la police ni le corps du bloc. C'est la garde du sabotage de N9.
        var document = Importer(Bloc(("FLIGHT MANAGEMENT", Francais), ("Check the valve position.", Americain)));
        var bloc = Texte(document);

        var francais = Assert.Single(bloc.Spans);
        Assert.Equal((0, "FLIGHT MANAGEMENT".Length), (francais.Start, francais.Length));
        Assert.Equal("fr-FR", francais.Langue);
        Assert.Null(francais.FontFamily);
        Assert.Null(francais.FontSize);
        Assert.Null(francais.Bold);
        Assert.Null(francais.Italic);
        Assert.Null(francais.Foreground);

        Assert.Equal(new[] { "fr-FR", "en-US" }, Langues(document, bloc));
    }

    [Fact]
    public void La_langue_la_plus_ecrite_va_au_bloc_puis_au_document()
    {
        var document = Importer(
            Bloc(("Un long paragraphe en français", Francais), ("Short", Americain)),
            Bloc(("A long English paragraph, longer than the French one", Americain)),
            Bloc(("Another English one, to tip the balance", Americain)));

        // Le document prend la langue la plus écrite ; un bloc qui la parle s'en remet à lui ;
        // un bloc qui en parle une autre la porte ; un fragment qui répète son bloc ne dit rien.
        Assert.Equal("en-US", document.Langue);
        Assert.Equal("fr-FR", Texte(document, 0).Langue);
        Assert.Null(Texte(document, 1).Langue);
        Assert.Null(Texte(document, 2).Langue);
        Assert.Equal("en-US", Assert.Single(Texte(document, 0).Spans).Langue);
        Assert.Empty(Texte(document, 1).Spans);

        Assert.Equal(new[] { "fr-FR", "en-US" }, Langues(document, Texte(document, 0)));
        Assert.Equal(new[] { "en-US" }, Langues(document, Texte(document, 1)));
    }

    [Theory]
    [InlineData("fo:country: FR, fo:language: fr, ", "fr-FR")]
    [InlineData("fo:language: fr, ", "fr")]
    [InlineData("fo:country: RS, fo:language: sr, fo:script: Latn, ", "sr-Latn-RS")]
    // Une langue que MyPublisher ne connaît pas reste écrite telle quelle.
    [InlineData("fo:country: QM, fo:language: qaa, ", "qaa-QM")]
    public void L_etiquette_se_compose_des_trois_cles_de_libmspub(string cles, string etiquette)
    {
        var document = Importer(Bloc(("Texte", cles)));

        Assert.Equal(etiquette, document.Langue);
        Assert.Equal(new[] { etiquette }, Langues(document, Texte(document)));
    }

    [Fact]
    public void Un_releve_sans_langue_laisse_le_document_en_francais_et_le_texte_sans_langue()
    {
        var document = Importer(Bloc(("Sans langue", null), ("Toujours rien", null)));
        var bloc = Texte(document);

        Assert.Equal(Models.Langues.ParDefaut, document.Langue);
        Assert.Null(bloc.Langue);
        Assert.Empty(bloc.Spans);
    }

    [Fact]
    public void Un_fragment_vide_pese_sa_langue_sans_rien_porter()
    {
        // Les fragments qui ne portent que la marque de paragraphe — vingt-trois dans ATA 21 —
        // ont une langue et pas un caractère. Un bloc qui n'a qu'eux prend la leur.
        var document = Importer(Bloc(("A long English paragraph", Americain)), Bloc(("", Francais)));

        Assert.Equal("en-US", document.Langue);
        Assert.Equal("fr-FR", Texte(document, 1).Langue);
        Assert.Empty(Texte(document, 1).Spans);
    }

    [Fact]
    public void Une_case_de_tableau_garde_la_langue_de_ses_fragments_et_rien_d_autre()
    {
        // Les Fiches écrivent leurs titres dans des cases, sans bloc de texte : l'import n'en
        // rend pas le style — la case prend celui du tableau —, mais il en garde la langue.
        var document = Importer(
            Bloc(("A long English paragraph, to make the document English", Americain)),
            """
                startTableObject(librevenge:table-columns: ((style:column-width: 2.0000in), (style:column-width: 2.0000in)), svg:height: 0.3652in, svg:width: 4.0000in, svg:x: 1.0000in, svg:y: 4.0000in)
                  openTableRow(librevenge:row-height: 0.3421in)
                    openTableCell(librevenge:column: 0, librevenge:row: 0)
                      openParagraph (fo:text-align: center)
                        openSpan(fo:color: #000000, fo:country: FR, fo:font-size: 0.1389in, fo:language: fr, fo:font-weight: bold, style:font-name: Verdana)
                          insertText (DC GEN FAULT)
                        closeSpan
                      closeParagraph
                    closeTableCell()
                    openTableCell(librevenge:column: 1, librevenge:row: 0)
                      openParagraph (fo:text-align: left)
                        openSpan(fo:color: #000000, fo:country: US, fo:font-size: 0.1389in, fo:language: en, style:font-name: Verdana)
                          insertText (Land ASAP)
                        closeSpan
                      closeParagraph
                    closeTableCell()
                  closeTableRow()
                endTableObject()

            """);
        var tableau = document.Pages[0].Frames.OfType<TableFrame>().Single();
        var francaise = tableau.Cells.Single(c => c.Column == 0);
        var anglaise = tableau.Cells.Single(c => c.Column == 1);

        Assert.Equal("en-US", document.Langue);
        var fragment = Assert.Single(francaise.Spans);
        Assert.Equal((0, "DC GEN FAULT".Length, "fr-FR"), (fragment.Start, fragment.Length, fragment.Langue));
        Assert.Null(fragment.Bold);
        Assert.Null(fragment.FontFamily);
        Assert.Equal("fr-FR", Models.Langues.DuCaractere(francaise, 3, document));
        // Ce qui répète la langue du document s'en remet à lui.
        Assert.Empty(anglaise.Spans);
        Assert.Equal("en-US", Models.Langues.DuCaractere(anglaise, 3, document));
    }

    // ------------------------------------------------------------------ le fichier

    [Fact]
    public void L_aller_retour_garde_chaque_langue()
    {
        var bloc = new TextFrame { Text = "Hello Bonjour", Langue = "en-GB" };
        bloc.Spans.Add(new StyleSpan { Start = 6, Length = 7, Langue = "fr-CA" });
        var tableau = new TableFrame { RowCount = 1, ColumnCount = 1 };
        tableau.Cells[0].Text = "Ciao";
        tableau.Cells[0].Spans.Add(new StyleSpan { Start = 0, Length = 4, Langue = "it-IT" });
        var document = Document.CreateDefault();
        document.Langue = "de-DE";
        document.Pages[0].Frames.Add(bloc);
        document.Pages[0].Frames.Add(tableau);

        var json = DocumentSerializer.ToJson(document);
        var relu = DocumentSerializer.FromJson(json);

        Assert.Equal(json, DocumentSerializer.ToJson(relu));
        Assert.Equal("de-DE", relu.Langue);
        var bloc2 = relu.Pages[0].Frames.OfType<TextFrame>().Single();
        Assert.Equal("en-GB", bloc2.Langue);
        Assert.Equal("fr-CA", bloc2.Spans.Single().Langue);
        Assert.Equal("it-IT", relu.Pages[0].Frames.OfType<TableFrame>().Single().Cells[0].Spans.Single().Langue);
        Assert.Equal(5, (int)JsonNode.Parse(json)!["FormatVersion"]!);
    }

    [Fact]
    public void Un_fichier_d_avant_N9_est_en_francais_et_son_texte_sans_langue()
    {
        var ancien = JsonNode.Parse(DocumentSerializer.ToJson(Rendu(new TextFrame { Text = "Texte" })))!.AsObject();
        ancien.Remove("Langue");
        var bloc = ancien["Pages"]![0]!["Frames"]![0]!.AsObject();
        bloc.Remove("Langue");

        var relu = DocumentSerializer.FromJson(ancien.ToJsonString());

        Assert.Equal("fr-FR", relu.Langue);
        Assert.Null(relu.Pages[0].Frames.OfType<TextFrame>().Single().Langue);
    }

    [Theory]
    [InlineData("null", "fr-FR")]
    [InlineData("\"\"", "fr-FR")]
    [InlineData("\"tlh-Latn\"", "tlh-Latn")]
    [InlineData("\"FR-fr\"", "FR-fr")]
    public void La_langue_du_document_n_est_jamais_vide_et_jamais_corrigee(string ecrite, string lue)
    {
        var json = DocumentSerializer.ToJson(Document.CreateDefault())
            .Replace("\"Langue\": \"fr-FR\"", $"\"Langue\": {ecrite}");

        Assert.Equal(lue, DocumentSerializer.FromJson(json).Langue);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("")]
    public void Un_document_vierge_est_en_francais_quelle_que_soit_la_culture(string culture)
    {
        var avant = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

            Assert.Equal("fr-FR", new Document().Langue);
            Assert.Equal("fr-FR", Document.CreateDefault().Langue);
            Assert.Null(new TextFrame().Langue);
            Assert.Null(new StyleSpan().Langue);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = avant;
        }
    }

    [Fact]
    public void Une_copie_garde_la_langue_du_bloc_et_de_ses_fragments()
    {
        var bloc = new TextFrame { Text = "Hello Bonjour", Langue = "en-US" };
        bloc.Spans.Add(new StyleSpan { Start = 6, Length = 7, Langue = "fr-FR" });

        var copie = (TextFrame)bloc.Clone();

        Assert.Equal("en-US", copie.Langue);
        Assert.Equal("fr-FR", copie.Spans.Single().Langue);
        Assert.Equal("fr-FR", bloc.Spans[0].Clone().Langue);
    }

    // ------------------------------------------------------------------ la frappe et la mise en forme

    private static readonly StyleEffectif Commun =
        new("DejaVu Sans", 11, Bold: false, Italic: false, Souligne.Aucun, "#FF1A1A1A", 0);

    private static ObservableCollection<StyleSpan> Francais0a7() =>
        [new StyleSpan { Start = 0, Length = 7, Langue = "fr-FR" }];

    [Fact]
    public void Ce_qu_on_tape_dans_un_mot_francais_est_francais()
    {
        var bloc = new TextFrame { Text = "Bonjour world", Langue = "en-US" };
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 7, Langue = "fr-FR" });
        var document = Rendu(bloc);

        bloc.Saisie = "Bonjoureee world";

        Assert.Equal(new[] { "fr-FR", "en-US" }, Langues(document, bloc));
        Assert.Equal("fr-FR", Models.Langues.DuCaractere(bloc, 9, document));
    }

    [Fact]
    public void Mettre_en_gras_une_plage_a_cheval_garde_la_langue_de_chaque_bout()
    {
        const string texte = "Bonjour world";
        var fragments = Francais0a7();

        Assert.True(MiseEnForme.Poser(texte, fragments, Commun, new Plage(4, 6), new StyleDeLaPlage { Bold = true }));

        var bloc = new TextFrame { Text = texte, Langue = "en-US" };
        foreach (var f in fragments) bloc.Spans.Add(f);
        var document = Rendu(bloc);
        Assert.Equal(new[] { "fr-FR", "en-US" }, Langues(document, bloc));
        Assert.Equal("fr-FR", Models.Langues.DuCaractere(bloc, 6, document));
        Assert.Equal("en-US", Models.Langues.DuCaractere(bloc, 7, document));

        // Et le gras est bien là où on l'a posé, ni plus ni moins.
        var gras = StyleEffectif.Decouper(texte, fragments, Commun)
            .SelectMany(m => Enumerable.Repeat(m.Style.Bold, m.Texte.Length)).ToArray();
        Assert.Equal(Enumerable.Range(0, 13).Select(i => i is >= 4 and < 10), gras);
    }

    [Fact]
    public void Ctrl_Espace_rend_le_style_du_bloc_et_garde_la_langue()
    {
        const string texte = "Bonjour world";
        ObservableCollection<StyleSpan> fragments =
            [new StyleSpan { Start = 0, Length = 7, Bold = true, Langue = "fr-FR" }];

        Assert.True(MiseEnForme.Retirer(texte, fragments, Commun, new Plage(0, 13)));

        var reste = Assert.Single(fragments);
        Assert.Equal((0, 7, "fr-FR"), (reste.Start, reste.Length, reste.Langue));
        Assert.Null(reste.Bold);

        // Plus rien à rendre : une seconde fois ne fait rien.
        Assert.False(MiseEnForme.Retirer(texte, fragments, Commun, new Plage(0, 13)));
    }

    [Fact]
    public void Deux_voisins_de_meme_dessin_et_de_langues_differentes_ne_se_recollent_pas()
    {
        const string texte = "Bonjour world";
        var fragments = Francais0a7();

        // Le gras posé sur tout le texte : le bout français et le bout sans langue se
        // dessinent pareil, mais ne disent pas la même chose.
        Assert.True(MiseEnForme.Poser(texte, fragments, Commun, new Plage(0, 13), new StyleDeLaPlage { Bold = true }));

        Assert.Equal(2, fragments.Count);
        Assert.Equal(new[] { "fr-FR", null }, fragments.OrderBy(f => f.Start).Select(f => f.Langue));
        Assert.All(fragments, f => Assert.True(f.Bold));
    }

    [Fact]
    public void Une_langue_se_pose_sur_une_plage_et_s_y_lit()
    {
        const string texte = "Hello Bonjour";
        var fragments = new ObservableCollection<StyleSpan>();

        Assert.True(MiseEnForme.Poser(texte, fragments, Commun, new Plage(6, 7), new StyleDeLaPlage { Langue = "fr-FR" }));

        Assert.Equal("fr-FR", MiseEnForme.Lire(texte, fragments, Commun, new Plage(6, 7), Charte.Defaut).Langue);
        // Mêlée, ou laissée au bloc, la plage n'a pas de langue à dire.
        Assert.Null(MiseEnForme.Lire(texte, fragments, Commun, new Plage(0, 13), Charte.Defaut).Langue);
        Assert.Null(MiseEnForme.Lire(texte, fragments, Commun, new Plage(0, 5), Charte.Defaut).Langue);
        // Reposée, elle ne change rien.
        Assert.False(MiseEnForme.Poser(texte, fragments, Commun, new Plage(6, 7), new StyleDeLaPlage { Langue = "fr-FR" }));
    }

    // ------------------------------------------------------------------ le rendu

    private sealed class ChasseFixe : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => texte.Length * style.FontSize;
        public double LargeurDeCoupure(string texte, StyleEffectif style) => Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => style.FontSize;
    }

    [Fact]
    public void La_langue_ne_change_rien_a_la_composition()
    {
        // Une frontière de langue au milieu d'un mot : si la langue comptait dans le style,
        // le mot ferait deux unités, et le renvoi pourrait les séparer.
        const string texte = "Le mot anticonstitutionnellement se coupe mal, et deux langues s'y mêlent.";
        IReadOnlyList<StyleSpan> sans = [new StyleSpan { Start = 0, Length = 10, Bold = true }];
        IReadOnlyList<StyleSpan> avec =
        [
            new StyleSpan { Start = 0, Length = 10, Bold = true, Langue = "fr-FR" },
            new StyleSpan { Start = 12, Length = 9, Langue = "en-US" },
            new StyleSpan { Start = 40, Length = 20, Langue = "de-DE" },
        ];

        foreach (var largeur in new[] { 14.0, 20, 33, 80, 400 })
        {
            Assert.Equal(Composer(texte, sans, largeur), Composer(texte, avec, largeur));
        }

        Assert.Equal(StyleEffectif.Decouper(texte, sans, Commun), StyleEffectif.Decouper(texte, avec, Commun));
    }

    private static string Composer(string texte, IReadOnlyList<StyleSpan> fragments, double largeur) =>
        string.Join("\n", Compositeur.Composer(texte, fragments, Commun with { FontSize = 1 }, [], default,
                largeur, 1_000_000, new Colonnage(1, 0), 1, new ChasseFixe())
            .Select(ligne => string.Join("|", ligne.Morceaux.Select(m => $"{m.Debut}:{m.Texte}:{m.Style}:{m.X}"))));

    private static Document Rendu(TextFrame bloc)
    {
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Add(bloc);
        return document;
    }
}
