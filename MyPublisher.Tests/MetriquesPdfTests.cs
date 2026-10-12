using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// La règle à mesurer du PDF, hors de l'export — poste Q13.
///
/// L'écran consulte cette règle sans page pour décider de ses coupures, et l'export la
/// consulte sur sa page. Tout l'accord des deux rendus repose sur ce que les deux
/// mesures soient la même, au bit près : un écart d'un millionième suffit à faire
/// passer un mot à la ligne d'un côté et pas de l'autre.
/// </summary>
public class MetriquesPdfTests
{
    private static readonly string[] Textes =
        ["puis du PDF. Le document n'est donc jamais", "jamais ", "AVATAR Te", "l'œil — 3,5 pt", "épaisseur, "];

    private static readonly StyleEffectif[] Styles =
    [
        new("DejaVu Sans", 10.5, false, false, "#FF000000", 0),
        new("DejaVu Sans", 10.5, true, false, "#FF000000", 0),
        new("DejaVu Sans", 14, false, true, "#FF000000", 0),
        new("Noto Sans", 9, false, false, "#FF000000", 0),
    ];

    [Fact]
    [Trait("poste", "Q13")]
    public void Une_face_absente_se_simule_sur_le_fichier_que_fontconfig_a_choisi()
    {
        // DejaVu Sans n'a pas d'italique sur la machine du projet : fontconfig rend le
        // romain, avec une matrice de biais. L'export le penche donc — il le dessinait
        // droit avant Q13 —, et l'écran lit le même drapeau. Noto Sans, qui a son
        // italique, ne se simule pas : la simulation n'est pas posée à l'aveugle.
        var dejavu = FontconfigFontResolver.Resoudre("DejaVu Sans", false, true);
        Assert.EndsWith("DejaVuSans.ttf", dejavu.Fichier);
        Assert.True(dejavu.SimulerItalique);
        Assert.False(dejavu.SimulerGras);

        var noto = FontconfigFontResolver.Resoudre("Noto Sans", false, true);
        Assert.False(noto.SimulerItalique);

        var pourPdf = new FontconfigFontResolver().ResolveTypeface("DejaVu Sans", false, true);
        Assert.NotNull(pourPdf);
        Assert.True(pourPdf.MustSimulateItalic);
        Assert.False(pourPdf.MustSimulateBold);

        // Le gras de DejaVu Sans existe : rien à simuler.
        Assert.False(FontconfigFontResolver.Resoudre("DejaVu Sans", true, false).SimulerGras);
    }

    [Fact]
    [Trait("poste", "Q13")]
    public void La_mesure_hors_page_est_celle_de_la_page()
    {
        FontconfigFontResolver.EnsureInstalled();
        var pdf = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(pdf.AddPage());

        var page = new MetriquesPdf(gfx, ecran: null);
        var horsPage = MetriquesPdf.HorsPage(ecran: null);

        foreach (var style in Styles)
        {
            foreach (var texte in Textes)
            {
                Assert.Equal(page.Largeur(texte, style), horsPage.Largeur(texte, style));
            }

            Assert.Equal(page.Naturelle(style), horsPage.Naturelle(style));
        }
    }

    /// <summary>Une « autre » règle qui mesure tout plus large d'un point, ou plus étroit.</summary>
    private sealed class Decalee(Compositeur.IMetriques base_, double ecart) : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => base_.Largeur(texte, style) + ecart;
        public double LargeurDeCoupure(string texte, StyleEffectif style) => Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => base_.Naturelle(style);
    }

    [Fact]
    [Trait("poste", "Q13")]
    public void La_coupure_prend_la_plus_large_des_deux_mesures_et_le_dessin_la_sienne()
    {
        var seule = MetriquesPdf.HorsPage(ecran: null);
        var style = Styles[0];
        const string Texte = "jamais ";
        var propre = seule.Largeur(Texte, style);

        var ecranPlusLarge = MetriquesPdf.HorsPage(new Decalee(seule, 1));
        var ecranPlusEtroit = MetriquesPdf.HorsPage(new Decalee(seule, -1));

        // La décision lit la plus large ; le dessin reste à sa mesure, dans les deux cas.
        Assert.Equal(propre + 1, ecranPlusLarge.LargeurDeCoupure(Texte, style));
        Assert.Equal(propre, ecranPlusEtroit.LargeurDeCoupure(Texte, style));
        Assert.Equal(propre, ecranPlusLarge.Largeur(Texte, style));

        // Sans écran — les tests du noyau seuls —, la règle coupe à sa propre mesure.
        Assert.Equal(propre, seule.LargeurDeCoupure(Texte, style));
    }

    [Fact]
    [Trait("poste", "Q13")]
    public void Les_deux_rendus_coupent_pareil_quand_leurs_mesures_divergent()
    {
        // Deux règles qui ne mesurent pas pareil — l'une d'un point plus large par mot —
        // et qui se consultent l'une l'autre, comme l'écran et l'export : elles doivent
        // composer les mêmes lignes, alors que chacune décidant seule ne le fait pas.
        var pdfSeul = MetriquesPdf.HorsPage(ecran: null);
        var ecranSeul = new Decalee(pdfSeul, 1);
        var pdf = MetriquesPdf.HorsPage(ecranSeul);
        var ecran = new Croisee(ecranSeul, pdfSeul);

        const string Texte = "puis du PDF. Le document n'est donc jamais converti d'une unité vers une autre.";
        var style = Styles[0];
        // Une largeur où « jamais » tient pour le PDF seul, et pas pour l'écran seul.
        var largeur = pdfSeul.Largeur("puis du PDF. Le document n'est donc jamais", style) + 3;

        string[] Lignes(Compositeur.IMetriques m) =>
            Compositeur.Composer(Texte, [], style, [], default, largeur, 1000, new Colonnage(1, 0), 1, m)
                .Select(l => l.Texte).ToArray();

        Assert.NotEqual(Lignes(pdfSeul), Lignes(ecranSeul));
        Assert.Equal(Lignes(pdf), Lignes(ecran));
    }

    /// <summary>L'écran du test précédent : sa mesure, et celle du PDF pour décider.</summary>
    private sealed class Croisee(Compositeur.IMetriques propre, Compositeur.IMetriques autre) : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => propre.Largeur(texte, style);
        public double LargeurDeCoupure(string texte, StyleEffectif style) =>
            System.Math.Max(propre.Largeur(texte, style), autre.Largeur(texte, style));
        public double Naturelle(StyleEffectif style) => propre.Naturelle(style);
    }
}
