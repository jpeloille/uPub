using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Exposant, indice et capitales — le poste N24 (fiche 23, b14), dans le noyau.
///
/// Une clé <c>Capitales</c> — aucune, capitales, petites capitales — sur le bloc et sur le fragment,
/// additive : <c>FormatVersion</c> reste 5. La composition dessine en capitales sans toucher au
/// texte, caractère pour caractère, si bien que les positions de N3 tiennent, et fabrique les petites
/// capitales en dessinant les minuscules en capitales à 70 % du corps. L'import lit les capitales de
/// libmspub, et la taille relative de l'exposant et de l'indice.
/// </summary>
[Trait("poste", "N24")]
public class ExposantEtCapitalesTests
{
    /// <summary>Une police imaginaire à chasse fixe : un caractère, une unité par point de corps.</summary>
    private sealed class ChasseFixe : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => texte.Length * style.FontSize;
        public double LargeurDeCoupure(string texte, StyleEffectif style) => Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => style.FontSize;
    }

    private static readonly StyleEffectif Commun =
        new("Police", 10, Bold: false, Italic: false, Souligne.Aucun, "#FF000000", 0);

    private static readonly ParagrapheEffectif SansRetrait = new(0, 0, 0, 0, 0, TextAlign.Left);

    private static IReadOnlyList<LigneComposee> Composer(
        string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif? commun = null, double largeur = 10_000) =>
        Compositeur.Composer(texte, fragments, commun ?? Commun, [], SansRetrait, largeur, 1_000_000,
            new Colonnage(1, 0), 1, new ChasseFixe());

    private static IEnumerable<Morceau> Morceaux(IReadOnlyList<LigneComposee> lignes) => lignes.SelectMany(l => l.Morceaux);

    // ------------------------------------------------------------------ la composition

    /// <summary>
    /// Sous des capitales, la composition dessine les capitales, et le texte ne change pas : chaque
    /// morceau a la longueur de sa plage, et sa plage, mise en capitales, est son texte (N3). « ß »
    /// n'a pas de capitale d'un seul caractère : il reste tel quel, et le mot qui le suit garde sa
    /// place.
    /// </summary>
    [Fact]
    public void Les_capitales_se_dessinent_sans_toucher_au_texte_et_les_positions_tiennent()
    {
        const string texte = "Straße été\nfin";
        var lignes = Composer(texte, [], Commun with { Capitales = Capitales.Capitales });

        Assert.Equal(new[] { "STRAßE ÉTÉ", "FIN" }, lignes.Select(l => l.Texte).ToArray());
        foreach (var morceau in Morceaux(lignes))
        {
            Assert.Equal(texte.Substring(morceau.Debut, morceau.Texte.Length).ToUpperInvariant(), morceau.Texte);
        }

        Assert.Equal((0, 10, 11, 14), (lignes[0].Debut, lignes[0].Fin, lignes[1].Debut, lignes[1].Fin));
        // Le texte d'origine n'a pas changé, et le curseur se pose sur ses positions.
        var apres = Compositeur.Abscisse(lignes[0], new double[lignes[0].Morceaux.Count], 7, texte, Commun, new ChasseFixe());
        Assert.Equal(70, apres, 6);
    }

    /// <summary>
    /// Sans capitales, rien ne change : la même composition, morceau pour morceau, qu'un style
    /// construit sans elles — le cas de tous les documents écrits avant N24.
    /// </summary>
    [Fact]
    public void Un_texte_sans_capitales_se_compose_comme_avant()
    {
        const string texte = "Le Texte, tel qu'il est tapé";
        var fragments = new List<StyleSpan> { new() { Start = 3, Length = 5, Bold = true } };

        var sans = Composer(texte, fragments);
        var aucune = Composer(texte, fragments, Commun with { Capitales = Capitales.Aucune });

        Assert.Equal(sans.SelectMany(l => l.Morceaux), aucune.SelectMany(l => l.Morceaux));
        Assert.Equal(texte, sans.Single().Texte);
    }

    /// <summary>
    /// Les petites capitales : les minuscules en capitales à 70 % du corps, les capitales tapées et
    /// les chiffres au corps plein ; une espace suit le caractère qui la précède. La ligne de base
    /// d'un exposant reste au même décalage, à 70 % du corps.
    /// </summary>
    [Fact]
    public void Les_petites_capitales_dessinent_les_minuscules_en_capitales_a_70_pour_cent_du_corps()
    {
        const string texte = "Air 21 conditioning";
        var lignes = Composer(texte, [], Commun with { Capitales = Capitales.PetitesCapitales, BaselineShift = 0.5 });

        var morceaux = Morceaux(lignes).Select(m => (m.Texte, m.Style.FontSize, m.Debut)).ToArray();
        Assert.Equal(new[]
        {
            ("A", 10.0, 0), ("IR ", 7.0, 1), ("21 ", 10.0, 4), ("CONDITIONING", 7.0, 7),
        }, morceaux);
        foreach (var morceau in Morceaux(lignes))
        {
            // Le décalage, multiple du corps, est rapporté au corps réduit : la ligne de base ne bouge pas.
            Assert.Equal(5.0, morceau.Style.BaselineShift * morceau.Style.FontSize, 9);
        }

        Assert.Equal(Compositeur.ReductionDesPetitesCapitales, 7.0 / 10.0, 12);
    }

    /// <summary>
    /// Un mot en petites capitales est deux portions — « A », « IR » —, mais un seul mot pour le
    /// renvoi : il passe à la ligne entier, et non coupé après sa capitale.
    /// </summary>
    [Fact]
    public void Un_mot_en_petites_capitales_ne_se_coupe_pas_entre_ses_deux_tailles()
    {
        // « Le » (10 + 7) + espace (7) = 24 ; « Avion » = 10 + 4 × 7 = 38 : 62 ne tient pas dans 40.
        const string texte = "Le Avion";
        var lignes = Composer(texte, [], Commun with { Capitales = Capitales.PetitesCapitales }, largeur: 40);

        Assert.Equal(new[] { "LE", "AVION" }, lignes.Select(l => l.Texte).ToArray());
        Assert.Equal(new[] { "A", "VION" }, lignes[1].Morceaux.Select(m => m.Texte).ToArray());

        // Et sans petites capitales, le même texte se coupe au même blanc.
        Assert.Equal(new[] { "Le", "Avion" }, Composer(texte, [], largeur: 40).Select(l => l.Texte).ToArray());
    }

    /// <summary>Un fragment en capitales dans un bloc qui n'en a pas, et un fragment qui les retire d'un bloc qui en a.</summary>
    [Fact]
    public void Le_fragment_donne_ou_retire_les_capitales_du_bloc()
    {
        const string texte = "un deux trois";
        var fragments = new List<StyleSpan>
        {
            new() { Start = 3, Length = 4, Capitales = Capitales.Capitales },
        };
        Assert.Equal("un DEUX trois", string.Concat(Composer(texte, fragments).Select(l => l.Texte)));

        var retires = new List<StyleSpan> { new() { Start = 3, Length = 4, Capitales = Capitales.Aucune } };
        Assert.Equal("UN deux TROIS",
            string.Concat(Composer(texte, retires, Commun with { Capitales = Capitales.Capitales }).Select(l => l.Texte)));
    }

    // ------------------------------------------------------------------ la mise en forme

    /// <summary>
    /// <c>MiseEnForme</c> lit, pose et retire les capitales comme le reste : un fragment, la valeur
    /// du bloc normalisée, l'état mixte nul, et <c>Ctrl+Espace</c> les retire.
    /// </summary>
    [Fact]
    public void La_mise_en_forme_lit_pose_et_retire_les_capitales()
    {
        const string texte = "abc def";
        var fragments = new ObservableCollection<StyleSpan>();

        Assert.True(MiseEnForme.Poser(texte, fragments, Commun, new Plage(4, 3),
            new StyleDeLaPlage { Capitales = Capitales.PetitesCapitales }));
        var fragment = Assert.Single(fragments);
        Assert.Equal((4, 3, Capitales.PetitesCapitales), (fragment.Start, fragment.Length, fragment.Capitales));

        Assert.Equal(Capitales.PetitesCapitales, MiseEnForme.Lire(texte, fragments, Commun, new Plage(4, 3), Charte.Defaut).Capitales);
        Assert.Null(MiseEnForme.Lire(texte, fragments, Commun, new Plage(0, 7), Charte.Defaut).Capitales);
        Assert.Equal(Capitales.Aucune, MiseEnForme.Lire(texte, fragments, Commun, new Plage(0, 3), Charte.Defaut).Capitales);

        // La valeur du bloc ne s'écrit pas : le fragment disparaît.
        Assert.True(MiseEnForme.Poser(texte, fragments, Commun, new Plage(4, 3), new StyleDeLaPlage { Capitales = Capitales.Aucune }));
        Assert.Empty(fragments);

        Assert.True(MiseEnForme.Poser(texte, fragments, Commun, new Plage(0, 7), new StyleDeLaPlage { Capitales = Capitales.Capitales }));
        Assert.True(MiseEnForme.Retirer(texte, fragments, Commun, new Plage(0, 7)));
        Assert.Empty(fragments);
    }

    // ------------------------------------------------------------------ le fichier

    /// <summary>
    /// La clé est additive (règle 15) : un document qui ne l'écrit pas se lit sans capitales, et
    /// <c>FormatVersion</c> reste 5 ; écrite, elle survit à l'aller-retour, sur le bloc et le
    /// fragment, et à la copie.
    /// </summary>
    [Fact]
    public void La_cle_Capitales_est_additive_et_survit_a_l_aller_retour()
    {
        var bloc = new TextFrame { Text = "Titre courant", Capitales = Capitales.PetitesCapitales };
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 5, Capitales = Capitales.Capitales, BaselineShift = 0.5, FontSize = 6.7 });
        var document = new Document();
        document.Pages.Add(new Page());
        document.Pages[0].Frames.Add(bloc);

        var json = DocumentSerializer.ToJson(document);
        Assert.Contains("\"FormatVersion\": 5", json);
        Assert.Contains("\"Capitales\": \"PetitesCapitales\"", json);
        Assert.Contains("\"Capitales\": \"Capitales\"", json);

        var relu = Assert.IsType<TextFrame>(DocumentSerializer.FromJson(json).Pages[0].Frames.Single());
        Assert.Equal(Capitales.PetitesCapitales, relu.Capitales);
        Assert.Equal((Capitales.Capitales, 0.5, 6.7), (relu.Spans[0].Capitales, relu.Spans[0].BaselineShift, relu.Spans[0].FontSize));

        var copie = Assert.IsType<TextFrame>(relu.Clone());
        Assert.Equal((Capitales.PetitesCapitales, Capitales.Capitales), (copie.Capitales, copie.Spans[0].Capitales));

        // Sans la clé : aucune, et un fragment qui s'en remet au bloc.
        var sans = json.Replace("\"Capitales\": \"PetitesCapitales\",", "").Replace("\"Capitales\": \"Capitales\",", "");
        Assert.DoesNotContain("Capitales", sans);
        var ancien = Assert.IsType<TextFrame>(DocumentSerializer.FromJson(sans).Pages[0].Frames.Single());
        Assert.Equal(Capitales.Aucune, ancien.Capitales);
        Assert.Null(ancien.Spans[0].Capitales);
    }

    // ------------------------------------------------------------------ l'import

    private const string UnePage = """
        startDocument()
          startPage(svg:height: 11.6929in, svg:width: 8.2677in)
        """;

    /// <summary>
    /// L'import lit ce que libmspub écrit : <c>fo:text-transform: uppercase</c> pour les majuscules
    /// de Publisher, <c>fo:font-variant: small-caps</c> pour ses petites majuscules, et la taille
    /// relative de l'exposant (« 50% 67% ») et de l'indice (« -50% 67% »), qui s'applique au corps —
    /// libmspub écrit celui du texte, comme les quatre indices des Fiches le montrent.
    /// </summary>
    [Fact]
    public void L_import_lit_les_capitales_et_le_corps_de_l_exposant_et_de_l_indice()
    {
        var document = PubImporter.Importer($"""
            {UnePage}
                startTextObject (svg:height: 1.0000in, svg:width: 4.0000in, svg:x: 0.0000in, svg:y: 0.0000in)
                  openParagraph ()
                    openSpan(fo:font-size: 0.1389in)
                      insertText (Vitesse V)
                    closeSpan
                    openSpan(fo:font-size: 0.1389in, style:text-position: -50% 67%)
                      insertText (1)
                    closeSpan
                    openSpan(fo:font-size: 0.1389in)
                      insertText ( et x)
                    closeSpan
                    openSpan(fo:font-size: 0.1389in, style:text-position: 50% 67%)
                      insertText (2)
                    closeSpan
                    openSpan(fo:font-size: 0.1389in, fo:text-transform: uppercase)
                      insertText ( atr)
                    closeSpan
                    openSpan(fo:font-size: 0.1389in, fo:font-variant: small-caps)
                      insertText ( Nota)
                    closeSpan
                  closeParagraph
                endTextObject
            """).Document;

        var bloc = Assert.IsType<TextFrame>(document.Pages[0].Frames.Single());
        Assert.Equal("Vitesse V1 et x2 atr Nota", bloc.Text);
        Assert.Equal(10, bloc.FontSize, 2);
        Assert.Equal(Capitales.Aucune, bloc.Capitales);

        var indice = bloc.Spans.Single(f => f.BaselineShift < 0);
        Assert.Equal((9, 1, -0.5), (indice.Start, indice.Length, indice.BaselineShift));
        Assert.Equal(10 * 0.67, indice.FontSize!.Value, 2);
        var exposant = bloc.Spans.Single(f => f.BaselineShift > 0);
        Assert.Equal((15, 1, 0.5), (exposant.Start, exposant.Length, exposant.BaselineShift));
        Assert.Equal(10 * 0.67, exposant.FontSize!.Value, 2);

        Assert.Equal((16, 4), bloc.Spans.Where(f => f.Capitales == Capitales.Capitales).Select(f => (f.Start, f.Length)).Single());
        Assert.Equal((20, 5), bloc.Spans.Where(f => f.Capitales == Capitales.PetitesCapitales).Select(f => (f.Start, f.Length)).Single());
    }
}
