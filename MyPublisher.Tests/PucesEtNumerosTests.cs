using System.Collections.Generic;
using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Puces et numérotation — le poste N23 (fiche 23, b12), dans le noyau.
///
/// Une propriété de paragraphe, <c>Liste</c>, sur le bloc et sur l'écart : aucune, des puces, ou des
/// numéros avec leur forme et leur premier numéro. La composition pose la marque en tête de la
/// première ligne, dans le retrait de première ligne, au style du premier caractère : un morceau
/// sans position dans le texte. Les paragraphes consécutifs d'une même liste se numérotent de proche
/// en proche ; un paragraphe hors de la liste interrompt la suite ; le premier numéro la fait
/// repartir.
/// </summary>
[Trait("poste", "N23")]
public class PucesEtNumerosTests
{
    /// <summary>Une police imaginaire à chasse fixe : un caractère, une unité par point de corps.</summary>
    private sealed class ChasseFixe : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => texte.Length * style.FontSize;
        public double LargeurDeCoupure(string texte, StyleEffectif style) => Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => style.FontSize;
    }

    private static readonly StyleEffectif Commun =
        new("Police", 2, Bold: false, Italic: false, Souligne.Aucun, "#FF000000", 0);

    private static readonly ParagrapheEffectif SansRetrait = new(0, 0, 0, 0, 0, TextAlign.Left);

    private static readonly ListeDeParagraphe Chiffres = ListeDeParagraphe.DesNumeros(FormatDeNumero.Chiffres);

    private static IReadOnlyList<LigneComposee> Composer(
        string texte, IReadOnlyList<ParagraphSpan> ecarts, ParagrapheEffectif? commun = null, double largeur = 200,
        IReadOnlyList<StyleSpan>? fragments = null) =>
        Compositeur.Composer(texte, fragments ?? [], Commun, ecarts, commun ?? SansRetrait, largeur, 1_000_000,
            new Colonnage(1, 0), 1, new ChasseFixe());

    private static IReadOnlyList<ParagrapheEffectif> Effectifs(string texte, IReadOnlyList<ParagraphSpan> ecarts,
                                                               ParagrapheEffectif? commun = null) =>
        ParagrapheEffectif.Resoudre(Recalage.NombreDeParagraphes(texte), ecarts, commun ?? SansRetrait);

    private static string?[] Marques(string texte, IReadOnlyList<ParagraphSpan> ecarts, ParagrapheEffectif? commun = null) =>
        Listes.Marques(Effectifs(texte, ecarts, commun)).ToArray();

    /// <summary>La marque de chaque ligne qui en porte une, et son abscisse, sa largeur.</summary>
    private static Morceau? MarqueDe(LigneComposee ligne) =>
        ligne.Morceaux.Where(m => m.EstUneMarque).Select(m => (Morceau?)m).SingleOrDefault();

    // ------------------------------------------------------------------ la numérotation

    /// <summary>
    /// La recette : trois paragraphes, les numéros posés sur le deuxième et le troisième ; « 1. »
    /// et « 2. », sur la première ligne de chacun, en un seul écart, avec le retrait suspendu
    /// d'Office — le texte à 18 pt, la marque pendue à 0.
    /// </summary>
    [Fact]
    public void Les_numeros_poses_sur_le_deuxieme_et_le_troisieme_donnent_1_et_2_au_retrait_de_premiere_ligne()
    {
        const string texte = "Titre\nAlpha\nBeta";
        var ecarts = new List<ParagraphSpan>();

        Assert.True(Listes.Poser(texte, ecarts, SansRetrait, new Plage(1, 2), Chiffres));

        var ecart = Assert.Single(ecarts);
        Assert.Equal((1, 2, Chiffres, 18.0, -18.0), (ecart.Start, ecart.Length, ecart.Liste, ecart.IndentLeft, ecart.IndentFirstLine));
        Assert.Equal(new string?[] { null, "1.", "2." }, Marques(texte, ecarts));

        var lignes = Composer(texte, ecarts);
        Assert.Equal(3, lignes.Count);
        Assert.Null(MarqueDe(lignes[0]));
        Assert.Equal(0, lignes[0].Retrait);

        foreach (var (ligne, marque) in new[] { (lignes[1], "1."), (lignes[2], "2.") })
        {
            var m = MarqueDe(ligne) ?? throw new Xunit.Sdk.XunitException("pas de marque");
            Assert.Equal((marque, Morceau.SansPosition, 0.0, 18.0), (m.Texte, m.Debut, m.X, m.Largeur));
            // La ligne commence au retrait de première ligne — gauche 18, première −18 — ; le texte au
            // retrait gauche.
            Assert.Equal(0, ligne.Retrait);
            Assert.True(ligne.Morceaux[0].EstUneMarque);
            Assert.Equal(18, ligne.Morceaux[1].X);
        }

        // Les plages des lignes ne voient pas la marque : elle n'est pas dans le texte (N3).
        Assert.Equal((6, 11), (lignes[1].Debut, lignes[1].Fin));
        Assert.Equal("1.Alpha", lignes[1].Texte);
        Assert.All(lignes.SelectMany(l => l.Morceaux).Where(m => !m.EstUneMarque),
            m => Assert.Equal(m.Texte, texte.Substring(m.Debut, m.Texte.Length)));
    }

    [Fact]
    public void Un_paragraphe_hors_liste_interrompt_la_suite_et_le_premier_numero_la_fait_repartir()
    {
        const string texte = "a\nb\nc\nd\ne\nf\ng";
        var cinq = Chiffres with { Premier = 5 };
        var ecarts = new List<ParagraphSpan>
        {
            new() { Start = 0, Length = 2, Liste = Chiffres },
            // c : hors liste — la suite s'interrompt.
            new() { Start = 3, Length = 1, Liste = Chiffres },
            // e et f repartent à 5, g à 1 dans une autre forme.
            new() { Start = 4, Length = 2, Liste = cinq },
            new() { Start = 6, Length = 1, Liste = ListeDeParagraphe.DesNumeros(FormatDeNumero.Lettres) },
        };

        Assert.Equal([1, 2, null, 1, 5, 6, 1], Listes.Numeros(Effectifs(texte, ecarts)));
        Assert.Equal(new string?[] { "1.", "2.", null, "1.", "5.", "6.", "a)" }, Marques(texte, ecarts));

        // Des puces n'ont pas de numéro, et interrompent la suite.
        ecarts.Add(new ParagraphSpan { Start = 2, Length = 1, Liste = ListeDeParagraphe.DesPuces("–") });
        Assert.Equal(new string?[] { "1.", "2.", "–", "1.", "5.", "6.", "a)" }, Marques(texte, ecarts));
    }

    [Fact]
    public void Une_liste_du_bloc_numerote_tous_ses_paragraphes_et_un_ecart_aucune_en_sort_un()
    {
        const string texte = "a\nb\nc\nd";
        var bloc = SansRetrait with { Liste = Chiffres with { Premier = 3 } };

        Assert.Equal(new string?[] { "3.", "4.", "5.", "6." }, Marques(texte, [], bloc));

        var ecarts = new List<ParagraphSpan> { new() { Start = 1, Length = 1, Liste = ListeDeParagraphe.Aucune } };
        Assert.Equal(new string?[] { "3.", null, "3.", "4." }, Marques(texte, ecarts, bloc));
        Assert.Null(Effectifs(texte, ecarts, bloc)[1].Liste);
    }

    [Theory]
    [InlineData(FormatDeNumero.Chiffres, 1, "1.")]
    [InlineData(FormatDeNumero.Chiffres, 12, "12.")]
    [InlineData(FormatDeNumero.Lettres, 1, "a)")]
    [InlineData(FormatDeNumero.Lettres, 26, "z)")]
    [InlineData(FormatDeNumero.Lettres, 27, "aa)")]
    [InlineData(FormatDeNumero.Lettres, 53, "aaa)")]
    [InlineData(FormatDeNumero.Romains, 4, "iv.")]
    [InlineData(FormatDeNumero.Romains, 9, "ix.")]
    [InlineData(FormatDeNumero.Romains, 1994, "mcmxciv.")]
    [InlineData(FormatDeNumero.Romains, 4000, "4000.")]
    [InlineData(FormatDeNumero.Lettres, 0, "0)")]
    [InlineData(FormatDeNumero.Lettres, 2_000_000_000, "2000000000)")]
    public void Les_trois_formes_ecrivent_leur_numero(FormatDeNumero format, int numero, string marque)
    {
        Assert.Equal(marque, Listes.Marque(format, numero));
    }

    // ------------------------------------------------------------------ la marque dans la ligne

    /// <summary>
    /// La marque n'a pas de position : le point d'insertion du début du paragraphe est après elle,
    /// au retrait gauche, et un clic sur elle rend le début du paragraphe (N3). Une marque plus
    /// large que le creux pousse le texte d'une espace au-delà d'elle.
    /// </summary>
    [Fact]
    public void La_marque_n_a_pas_de_position_le_curseur_et_le_clic_la_sautent()
    {
        const string texte = "Un\nDeux";
        var ecarts = new List<ParagraphSpan> { new() { Start = 1, Length = 1, Liste = Chiffres, IndentLeft = 18, IndentFirstLine = -18 } };
        var lignes = Composer(texte, ecarts);
        var ligne = lignes[1];
        var zeros = new double[ligne.Morceaux.Count];
        var metriques = new ChasseFixe();

        Assert.Equal(18, Compositeur.Abscisse(ligne, zeros, 3, texte, Commun, metriques));
        Assert.Equal(20, Compositeur.Abscisse(ligne, zeros, 4, texte, Commun, metriques));
        Assert.Equal(3, Compositeur.PositionLaPlusProche(ligne, zeros, 1, texte, Commun, metriques));
        Assert.Equal(3, Compositeur.PositionLaPlusProche(ligne, zeros, 17, texte, Commun, metriques));
        Assert.Equal(7, Compositeur.PositionLaPlusProche(ligne, zeros, 500, texte, Commun, metriques));

        // Sans creux : la marque, puis la chasse d'une espace.
        ecarts[0].IndentFirstLine = 0;
        ligne = Composer(texte, ecarts)[1];
        Assert.Equal(18, ligne.Retrait);
        Assert.Equal(4 + 2, MarqueDe(ligne)!.Value.Largeur);
        Assert.Equal(6, Compositeur.Abscisse(ligne, zeros, 3, texte, Commun, metriques));
    }

    /// <summary>
    /// Un élément vide porte sa marque, au style du saut qui le ferme, et le curseur s'y pose après
    /// elle ; le paragraphe vide du bout du texte aussi. Un texte vide n'en dessine aucune.
    /// </summary>
    [Fact]
    public void Un_element_vide_porte_sa_marque_et_le_curseur_s_y_pose_apres_elle()
    {
        const string texte = "Un\n\n";
        var bloc = SansRetrait with { IndentLeft = 18, IndentFirstLine = -18, Liste = Chiffres };
        var lignes = Composer(texte, [], bloc);

        Assert.Equal(["1.", "2.", "3."], lignes.Select(l => MarqueDe(l)?.Texte));
        var vide = lignes[1];
        Assert.Equal((3, 3), (vide.Debut, vide.Fin));
        Assert.Equal(18, Compositeur.Abscisse(vide, new double[vide.Morceaux.Count], 3, texte, Commun, new ChasseFixe()));
        Assert.Equal(18, Compositeur.Abscisse(lignes[2], new double[1], 4, texte, Commun, new ChasseFixe()));

        Assert.Empty(Composer("", [], bloc));
        Assert.Null(MarqueDe(Compositeur.LigneDuTexteVide(Commun, [], bloc, 200, 100, new Colonnage(1, 0), 1, new ChasseFixe())));
    }

    /// <summary>La marque prend le style du premier caractère du paragraphe, et ne se souligne pas.</summary>
    [Fact]
    public void La_marque_prend_le_style_du_premier_caractere_et_ne_se_souligne_pas()
    {
        const string texte = "Un\nGras";
        var fragments = new List<StyleSpan> { new() { Start = 3, Length = 4, FontSize = 4, Bold = true, Underline = Souligne.Simple } };
        var ecarts = new List<ParagraphSpan> { new() { Start = 1, Length = 1, Liste = ListeDeParagraphe.DesPuces() } };
        var ligne = Composer(texte, ecarts, fragments: fragments)[1];

        var marque = MarqueDe(ligne)!.Value;
        Assert.Equal(("•", 4.0, true), (marque.Texte, marque.Style.FontSize, marque.Style.Bold));
        var souligne = Assert.Single(Compositeur.Soulignes(ligne, new double[ligne.Morceaux.Count], new ChasseFixe()));
        Assert.Equal(ligne.Morceaux[1].X, souligne.Debut);
    }

    /// <summary>
    /// Une ligne qui ne porte que la marque est vide pour le renvoi : un mot plus large que la
    /// colonne reste avec sa marque, au lieu de la laisser seule sur sa ligne.
    /// </summary>
    [Fact]
    public void Un_mot_trop_large_reste_avec_sa_marque()
    {
        const string texte = "Anticonstitutionnellement court";
        var bloc = SansRetrait with { IndentLeft = 18, IndentFirstLine = -18, Liste = ListeDeParagraphe.DesPuces() };
        var lignes = Composer(texte, [], bloc, largeur: 40);

        Assert.Equal("•Anticonstitutionnellement", lignes[0].Texte);
        Assert.Equal("court", lignes[1].Texte);
        Assert.Null(MarqueDe(lignes[1]));
    }

    // ------------------------------------------------------------------ la frappe et la pose

    /// <summary>
    /// Entrée au bout d'un élément continue la liste, par la règle de Q1 : le paragraphe neuf
    /// prend l'écart de celui qu'il coupe, et le numéro suivant ; les éléments d'après se
    /// renumérotent.
    /// </summary>
    [Fact]
    public void Un_saut_tape_au_bout_d_un_element_donne_l_element_suivant()
    {
        const string avant = "Titre\nAlpha\nBeta";
        var ecarts = new List<ParagraphSpan>();
        Listes.Poser(avant, ecarts, SansRetrait, new Plage(1, 2), Chiffres);

        const string apres = "Titre\nAlpha\n\nBeta";
        Recalage.Appliquer(avant, apres, [], ecarts);

        Assert.Equal(new string?[] { null, "1.", "2.", "3." }, Marques(apres, ecarts));
        Assert.Equal((1, 3), (Assert.Single(ecarts).Start, ecarts[0].Length));
    }

    /// <summary>
    /// Poser une liste donne le retrait suspendu d'Office à un paragraphe sans retrait, et garde
    /// un retrait réglé à la main ; l'ôter rend le retrait suspendu, et lui seul. Deux poses
    /// voisines fusionnent en un écart ; ôter la liste d'un bloc qui n'en a pas ne laisse rien.
    /// </summary>
    [Fact]
    public void Poser_une_liste_donne_le_retrait_suspendu_et_l_oter_le_rend()
    {
        const string texte = "a\nb\nc";
        var ecarts = new List<ParagraphSpan> { new() { Start = 2, Length = 1, IndentLeft = 30 } };

        Assert.True(Listes.Poser(texte, ecarts, SansRetrait, new Plage(0, 3), ListeDeParagraphe.DesPuces()));
        var effectifs = Effectifs(texte, ecarts);
        Assert.Equal([(18.0, -18.0), (18.0, -18.0), (30.0, 0.0)], effectifs.Select(e => (e.IndentLeft, e.IndentFirstLine)));
        Assert.All(effectifs, e => Assert.Equal(ListeDeParagraphe.DesPuces(), e.Liste));
        Assert.Equal(2, ecarts.Count);

        Assert.True(Listes.Poser(texte, ecarts, SansRetrait, new Plage(0, 3), ListeDeParagraphe.Aucune));
        Assert.Equal(new[] { new ParagraphSpan { Start = 2, Length = 1, IndentLeft = 30 } }.Select(Dire), ecarts.Select(Dire));

        Assert.False(Listes.Poser(texte, ecarts, SansRetrait, new Plage(0, 1), ListeDeParagraphe.Aucune));
    }

    private static string Dire(ParagraphSpan e) =>
        $"{e.Start}+{e.Length} {e.IndentLeft} {e.IndentFirstLine} {e.Liste}";

    /// <summary>
    /// Le premier numéro fait repartir la suite au paragraphe, et la suite avec lui ; repartir à 1
    /// juste après une suite qui écrit son 1 passe par la liste sans premier numéro.
    /// </summary>
    [Fact]
    public void Le_premier_numero_fait_repartir_la_suite_au_paragraphe()
    {
        const string texte = "a\nb\nc\nd";
        var ecarts = new List<ParagraphSpan>();
        Listes.Poser(texte, ecarts, SansRetrait, new Plage(0, 4), Chiffres with { Premier = 1 });
        Assert.Equal([1, 2, 3, 4], Listes.Numeros(Effectifs(texte, ecarts)));

        Assert.True(Listes.Recommencer(texte, ecarts, SansRetrait, 2, 7));
        Assert.Equal([1, 2, 7, 8], Listes.Numeros(Effectifs(texte, ecarts)));

        Assert.True(Listes.Recommencer(texte, ecarts, SansRetrait, 2, 1));
        Assert.Equal([1, 2, 1, 2], Listes.Numeros(Effectifs(texte, ecarts)));

        Assert.False(Listes.Recommencer("a", [], SansRetrait, 0, 3));
    }

    // ------------------------------------------------------------------ le fichier

    /// <summary>
    /// La clé est additive : un document sans liste écrit des listes nulles, et se relit tel quel ;
    /// une liste du bloc et une liste d'écart font l'aller-retour, et la copie d'un bloc les garde.
    /// </summary>
    [Fact]
    public void Les_listes_font_l_aller_retour_json_et_la_copie()
    {
        var bloc = new TextFrame
        {
            Text = "a\nb\nc", Liste = ListeDeParagraphe.DesNumeros(FormatDeNumero.Romains, 4),
            IndentLeft = 18, IndentFirstLine = -18,
        };
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 1, Length = 1, Liste = ListeDeParagraphe.DesPuces("→") });
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Add(bloc);

        var json = DocumentSerializer.ToJson(document);
        Assert.Contains("\"Sorte\": \"Numeros\"", json);
        Assert.Contains("\"Format\": \"Romains\"", json);
        var relu = (TextFrame)DocumentSerializer.FromJson(json).Pages[0].Frames[0];
        Assert.Equal(bloc.Liste, relu.Liste);
        Assert.Equal(bloc.Paragraphs[0].Liste, relu.Paragraphs[0].Liste);
        Assert.Equal(json, DocumentSerializer.ToJson(DocumentSerializer.FromJson(json)));
        // Les puces du milieu interrompent la suite : le troisième repart au premier numéro.
        Assert.Equal(new string?[] { "iv.", "→", "iv." }, Marques(relu.Text, relu.Paragraphs, relu.Paragraph));

        var copie = (TextFrame)bloc.Clone();
        Assert.Equal(bloc.Liste, copie.Liste);
        Assert.Equal(bloc.Paragraphs[0].Liste, copie.Paragraphs[0].Liste);

        var sans = Document.CreateDefault();
        sans.Pages[0].Frames.Add(new TextFrame { Text = "a" });
        var jsonSans = DocumentSerializer.ToJson(sans);
        Assert.Contains("\"Liste\": null", jsonSans);
        Assert.Null(((TextFrame)DocumentSerializer.FromJson(jsonSans).Pages[0].Frames[0]).Liste);
    }

    /// <summary>Un bloc ne court pas sur le suivant : chaque bloc numérote les siens depuis le début.</summary>
    [Fact]
    public void Une_liste_ne_court_pas_d_un_bloc_a_l_autre()
    {
        var bloc = SansRetrait with { Liste = Chiffres };
        Assert.Equal(new string?[] { "1.", "2." }, Marques("a\nb", [], bloc));
        Assert.Equal(new string?[] { "1." }, Marques("c", [], bloc));
    }
}
