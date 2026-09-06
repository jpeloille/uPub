using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Les remplissages non unis et le fond de page — postes C5a et C5c.
///
/// Ce que ces tests gardent est ce qu'aucune comparaison écran ↔ PDF ne peut garder :
/// **les calculs eux-mêmes**. L'écran et l'export lisent tous deux
/// <see cref="GradientFill.Extremites"/> et <see cref="TintFill.Uniforme"/> ; une erreur
/// dans l'un ou l'autre serait donc commise des deux côtés, les deux images
/// concorderaient, et la comparaison passerait au vert sur un dégradé tracé à l'envers.
/// C'est la leçon écrite dans PLAN.md après la relecture du 6 septembre 2026, et c'est
/// pour cela que ces valeurs-ci sont épinglées à la main.
/// </summary>
public class FondsTests
{
    // ------------------------------------------------------------------ le dégradé

    [Theory]
    // Zéro degré : de gauche à droite, à mi-hauteur. L'axe couvre toute la largeur.
    [InlineData(0, 0, 50, 200, 50)]
    // Quatre-vingt-dix degrés : de haut en bas — l'axe Y du modèle descend, comme en PDF.
    // Un signe inversé ici retournerait tous les dégradés du document sans rien casser
    // d'autre.
    [InlineData(90, 100, 0, 100, 100)]
    [InlineData(180, 200, 50, 0, 50)]
    [InlineData(270, 100, 100, 100, 0)]
    public void Les_extremites_du_degrade_suivent_son_angle(
        double angle, double x0, double y0, double x1, double y1)
    {
        var degrade = new GradientFill("#FFFFFFFF", "#FF000000", angle);

        var (ax0, ay0, ax1, ay1) = degrade.Extremites(200, 100);

        Assert.Equal(x0, ax0, precision: 6);
        Assert.Equal(y0, ay0, precision: 6);
        Assert.Equal(x1, ax1, precision: 6);
        Assert.Equal(y1, ay1, precision: 6);
    }

    [Fact]
    public void Un_degrade_en_diagonale_couvre_les_coins()
    {
        // Sur un carré à 45°, l'axe va d'un coin à l'autre : la première couleur commence
        // exactement au coin supérieur gauche, et non au milieu d'un bord. C'est ce que
        // la projection des coins achète — sans elle, un dégradé diagonal sortirait
        // délavé aux deux extrémités, ce qu'aucun test de couleur au centre ne verrait.
        var degrade = new GradientFill("#FFFFFFFF", "#FF000000", 45);

        var (x0, y0, x1, y1) = degrade.Extremites(100, 100);

        Assert.Equal(0, x0, precision: 6);
        Assert.Equal(0, y0, precision: 6);
        Assert.Equal(100, x1, precision: 6);
        Assert.Equal(100, y1, precision: 6);
    }

    // ------------------------------------------------------------------ la teinte

    [Theory]
    // Cent pour cent, c'est la couleur de base elle-même.
    [InlineData(100, "#FF804020")]
    // En dessous, on va vers le blanc ; à zéro, on y est.
    [InlineData(50, "#FFC0A090")]
    [InlineData(0, "#FFFFFFFF")]
    // Au-dessus, vers le noir ; à deux cents, on y est.
    [InlineData(150, "#FF402010")]
    [InlineData(200, "#FF000000")]
    public void La_teinte_se_calcule_depuis_sa_couleur_de_base(double taux, string attendue)
    {
        Assert.Equal(attendue, new TintFill("#FF804020", taux).Uniforme);
    }

    [Fact]
    public void Une_teinte_garde_la_transparence_de_sa_base()
    {
        // Éclaircir une couleur ne la rend pas opaque : l'octet alpha traverse le calcul.
        Assert.StartsWith("#80", new TintFill("#80336699", 40).Uniforme);
    }

    // ------------------------------------------------------------------ l'image

    [Fact]
    public void Une_image_etiree_occupe_tout_le_bloc()
    {
        var fond = new ImageFill("/tmp/x.png", ImageFit.Fill);

        var (x, y, largeur, hauteur) = fond.Cadre(200, 100, 1000, 250);

        Assert.Equal(0, x, precision: 6);
        Assert.Equal(0, y, precision: 6);
        Assert.Equal(200, largeur, precision: 6);
        Assert.Equal(100, hauteur, precision: 6);
    }

    [Fact]
    public void Une_image_cadree_sans_deformation_est_centree()
    {
        var fond = new ImageFill("/tmp/x.png", ImageFit.Uniform);

        var (x, y, largeur, hauteur) = fond.Cadre(200, 100, 1000, 250);

        // 1000 × 250 dans 200 × 100 : le rapport limitant est la largeur, l'image fait
        // donc 200 × 50 et se pose à 25 pt du haut.
        Assert.Equal(200, largeur, precision: 6);
        Assert.Equal(50, hauteur, precision: 6);
        Assert.Equal(0, x, precision: 6);
        Assert.Equal(25, y, precision: 6);
    }

    [Fact]
    public void Un_quart_de_tour_echange_la_largeur_et_la_hauteur_du_cadre()
    {
        // Le cas réel d'ATA 21, aux mesures près : un PNG couché de 1062 × 617 dans un
        // cadre debout de 219 × 376 pt, avec « librevenge:rotate: 90 ». Sans l'échange,
        // l'image est étirée sur un cadre dont le rapport est l'inverse du sien — couchée
        // ET écrasée d'un facteur trois. C'est une des cinq images du document.
        var fond = new ImageFill("/tmp/x.png", ImageFit.Fill, 90);

        var (x, y, largeur, hauteur) = fond.Cadre(219, 376, 1062, 617);

        Assert.Equal(376, largeur, precision: 6);
        Assert.Equal(219, hauteur, precision: 6);
        // Le rectangle reste centré sur le centre du bloc : c'est autour de lui que le
        // rendu fait tourner le repère.
        Assert.Equal(219 / 2.0, x + largeur / 2, precision: 6);
        Assert.Equal(376 / 2.0, y + hauteur / 2, precision: 6);
    }

    [Fact]
    public void Sans_rotation_le_cadre_ne_sechange_pas()
    {
        var fond = new ImageFill("/tmp/x.png", ImageFit.Fill);

        var (_, _, largeur, hauteur) = fond.Cadre(219, 376, 1062, 617);

        Assert.Equal(219, largeur, precision: 6);
        Assert.Equal(376, hauteur, precision: 6);
    }

    // ------------------------------------------------------------------ le format

    [Theory]
    [InlineData("solid")]
    [InlineData("gradient")]
    [InlineData("image")]
    [InlineData("tint")]
    public void Chaque_sorte_de_remplissage_se_relit_telle_quelle(string sorte)
    {
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Add(new ShapeFrame { Fill = Exemple(sorte) });
        document.Background = Exemple(sorte);

        var json = DocumentSerializer.ToJson(document);
        var relu = DocumentSerializer.FromJson(json);

        Assert.Contains($"\"$kind\": \"{sorte}\"", json);
        Assert.Equal(json, DocumentSerializer.ToJson(relu));
    }

    private static FillBrush Exemple(string sorte) => sorte switch
    {
        "gradient" => new GradientFill("#FFAABBCC", "#FF102030", 30),
        "image" => new ImageFill("/tmp/fond.png", ImageFit.UniformToFill, 90),
        "tint" => new TintFill("#FF336699", 40),
        _ => new SolidFill("#FF123456"),
    };

    [Fact]
    public void Le_fond_de_page_se_lit_et_se_reecrit()
    {
        var document = Document.CreateDefault();
        document.Background = new GradientFill("#FFFFFFFF", "#FF336699", 90);

        var relu = DocumentSerializer.FromJson(DocumentSerializer.ToJson(document));

        var fond = Assert.IsType<GradientFill>(relu.Background);
        Assert.Equal("#FF336699", fond.To);
        Assert.Equal(90, fond.Angle);
    }

    [Fact]
    public void Un_document_sans_fond_de_page_nen_recoit_pas_un()
    {
        // Le défaut doit rester « page blanche » : c'est ce qui dispense de toucher aux
        // documents existants autrement que par l'enveloppe du remplissage.
        Assert.Null(Document.CreateDefault().Background);
    }

    // ------------------------------------------------------------------ le panneau

    [Fact]
    public void Changer_de_sorte_puis_revenir_retrouve_ses_reglages()
    {
        var bloc = new ShapeFrame { Fill = new GradientFill("#FFAABBCC", "#FF102030", 30) };

        bloc.FillKind = FillKind.Solid;
        bloc.FillKind = FillKind.Gradient;

        Assert.Equal(30, Assert.IsType<GradientFill>(bloc.Fill).Angle);
    }

    [Fact]
    public void Le_selecteur_de_couleur_ne_remplace_pas_un_degrade_par_un_aplat()
    {
        // Le piège que le commentaire de « FillColor » décrit depuis C1, sous une forme
        // neuve : le sélecteur renvoie vers la source la valeur qu'il vient d'en lire dès
        // qu'il s'affiche. Un accesseur qui écrirait alors dans le fond effacerait le
        // dégradé à la seule sélection du bloc.
        var bloc = new ShapeFrame { Fill = new GradientFill("#FFAABBCC", "#FF102030", 30) };

        var lue = bloc.FillColor;
        bloc.FillColor = lue;
        bloc.FillColor = "#FF00FF00";

        Assert.IsType<GradientFill>(bloc.Fill);
    }

    [Fact]
    public void Modifier_le_remplissage_sur_place_se_redit_au_bloc()
    {
        // Régler l'angle d'un dégradé ne remplace pas l'instance que porte le bloc :
        // sans abonnement, ni le panneau ni la page n'en sauraient rien. C'est le
        // défaut de rafraîchissement que la relecture a mis quatre postes à trouver,
        // sous une autre forme.
        var bloc = new ShapeFrame { Fill = new SolidFill("#FFAABBCC") };
        var dits = new System.Collections.Generic.List<string>();
        bloc.PropertyChanged += (_, e) => dits.Add(e.PropertyName ?? "");

        ((SolidFill)bloc.Fill!).Color = "#FF00FF00";

        Assert.Contains("Fill", dits);
        Assert.Equal("#FF00FF00", bloc.FillColor);
    }

    [Fact]
    public void Un_remplissage_remplace_cesse_de_parler_au_bloc()
    {
        // Sans désabonnement, le bloc continuerait d'entendre un remplissage qu'il ne
        // porte plus, et se croirait à repeindre à chaque retouche d'un objet abandonné.
        var bloc = new ShapeFrame { Fill = new SolidFill("#FFAABBCC") };
        var ancien = (SolidFill)bloc.Fill!;
        bloc.Fill = new SolidFill("#FF102030");

        var dits = new System.Collections.Generic.List<string>();
        bloc.PropertyChanged += (_, e) => dits.Add(e.PropertyName ?? "");
        ancien.Color = "#FF00FF00";

        Assert.Empty(dits);
    }

    [Fact]
    public void Relire_les_cases_dun_tableau_nempile_pas_dabonnes_sur_leur_fond()
    {
        // Le piège que l'abonnement ci-dessus a créé : « CellFrames » reconstruit ses
        // blocs à chaque lecture, et chaque bloc neuf s'abonnait au fond de sa case —
        // qui, lui, vit longtemps. Les blocs jetés restaient donc accrochés à lui, par
        // centaines, et chaque retouche du fond notifiait des blocs que personne ne
        // dessine plus. Un tableau de C9 se relit à chaque notification du panneau.
        // On mesure la RÉTENTION et non un compte d'appels : un abonné en trop ne se voit
        // pas depuis l'extérieur d'un événement, mais il tient son objet en vie.
        var tableau = new TableFrame();
        tableau.RowCount = 1;
        tableau.ColumnCount = 3;
        tableau.Cells[0].Fill = new SolidFill("#FFEFF2F6");

        for (var i = 0; i < 20; i++) _ = tableau.CellFrames;

        Assert.Equal(1, Abonnes(tableau.Cells[0].Fill!));
    }

    /// <summary>
    /// Combien d'objets écoutent cet objet. On lit le champ de l'événement par réflexion :
    /// un abonné en trop ne se voit d'aucune autre façon depuis l'extérieur, et c'est
    /// précisément ce qui rend ce genre de fuite silencieuse.
    /// </summary>
    private static int Abonnes(object observable)
    {
        // Le champ de l'événement est privé dans « ObservableObject » : on remonte la
        // hiérarchie, « GetField » ne voyant pas les champs privés d'un type de base.
        for (var type = observable.GetType(); type is not null; type = type.BaseType)
        {
            var champ = type.GetField("PropertyChanged", System.Reflection.BindingFlags.Instance
                                                         | System.Reflection.BindingFlags.NonPublic
                                                         | System.Reflection.BindingFlags.DeclaredOnly);
            if (champ?.GetValue(observable) is System.Delegate delegue)
            {
                return delegue.GetInvocationList().Length;
            }
        }

        return 0;
    }

    // ------------------------------------------------------------------ l'export

    [Fact]
    public void Le_fond_de_page_arrive_dans_le_pdf()
    {
        var document = Document.CreateDefault();
        document.Background = new SolidFill("#FF336699");

        var chemin = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fond-{System.Guid.NewGuid():N}.pdf");
        try
        {
            var rapport = PdfExporter.Export(document, chemin);

            Assert.Empty(rapport.Warnings);
            // Le flux de la page, décompressé : #336699 s'y écrit « 0.2 0.4 0.6 rg »,
            // suivi du rectangle de la page entière. Sur un document sans aucun bloc,
            // cela ne peut venir que du fond de page — et le chercher dans le flux
            // plutôt que dans une image évite d'appeler un outil externe depuis un test
            // du noyau, qui doit rester à la milliseconde.
            var flux = Flux(chemin);
            Assert.Contains("0.2 0.4 0.6 rg", flux);
            Assert.Contains("595.28", flux);
        }
        finally
        {
            if (System.IO.File.Exists(chemin)) System.IO.File.Delete(chemin);
        }
    }

    [Fact]
    public void Une_image_de_remplissage_introuvable_se_signale()
    {
        // Ranger les images hors du document rend ce cas atteignable sans que le document
        // ait bougé : il ne doit pas se traduire par un aplat vide et silencieux.
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Add(new ShapeFrame
        {
            Name = "Bandeau",
            Fill = new ImageFill("/introuvable/absolument/nulle-part.png"),
        });

        var chemin = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fond-{System.Guid.NewGuid():N}.pdf");
        try
        {
            var rapport = PdfExporter.Export(document, chemin);

            Assert.Contains(rapport.Warnings, a => a.Contains("Image de remplissage introuvable"));
        }
        finally
        {
            if (System.IO.File.Exists(chemin)) System.IO.File.Delete(chemin);
        }
    }

    /// <summary>
    /// Le flux de dessin de la première page, décompressé. PDFsharp relit son propre
    /// PDF : c'est ce qui permet d'y chercher un ordre de tracé sans rien installer.
    /// </summary>
    private static string Flux(string chemin)
    {
        using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(chemin, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify);
        var octets = pdf.Pages[0].Contents.Elements.GetDictionary(0)!.Stream.UnfilteredValue;
        return string.Concat(octets.Select(o => (char)o));
    }
}
