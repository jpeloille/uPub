using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// L'objet trait, et la flèche — poste A10, dans le noyau : le trait se définit par ses deux
/// extrémités, se relit au caractère près, se prend à trois points de son encre, se tient à
/// 0, 45 ou 90° sous <c>Maj</c>, et se trace d'un coup de crayon dans le PDF. La planche, le
/// dock et l'outil Ligne sont éprouvés par <c>MyPublisher.Tests.Interface/TraitTests.cs</c>,
/// et le tout par la recette <c>Scripts/recettes/A10-trait.sh</c>.
/// </summary>
public class TraitTests
{
    private static ShapeFrame Trait(double x1, double y1, double x2, double y2)
    {
        var trait = new ShapeFrame { Name = "Trait", Shape = ShapeKind.Line, Fill = null, Stroke = "#FF000000" };
        trait.PoserLesExtremites(x1, y1, x2, y2);
        return trait;
    }

    // ------------------------------------------------------------------ extrémités

    [Fact]
    public void Un_trait_horizontal_na_pas_de_hauteur_et_garde_ses_extremites()
    {
        // Le piège du plan : la taille minimale d'un bloc vaut 2 mm, et un trait horizontal
        // a une hauteur nulle. Rien ne la lui impose.
        var trait = Trait(100, 200, 300, 200);

        Assert.Equal(0, trait.Height);
        Assert.Equal(200, trait.Width);
        Assert.Equal(0, trait.Rotation);
        Assert.Equal((100.0, 200.0), trait.Debut);
        Assert.Equal((300.0, 200.0), trait.Fin);
        Assert.True(trait.Height < Snap.MinFrameSize);
    }

    [Theory]
    [InlineData(100, 100, 100, 300, 90)]
    [InlineData(300, 100, 100, 100, 180)]
    [InlineData(100, 300, 100, 100, 270)]
    [InlineData(100, 100, 200, 200, 45)]
    [InlineData(200, 100, 100, 200, 135)]
    public void La_rotation_du_trait_est_son_angle_et_le_debut_reste_le_debut(
        double x1, double y1, double x2, double y2, double angle)
    {
        var trait = Trait(x1, y1, x2, y2);

        Assert.Equal(angle, trait.Rotation);
        Assert.Equal(x1, trait.Debut.X, 9);
        Assert.Equal(y1, trait.Debut.Y, 9);
        Assert.Equal(x2, trait.Fin.X, 9);
        Assert.Equal(y2, trait.Fin.Y, 9);
        Assert.Equal(0, trait.Height);
    }

    [Fact]
    public void Deux_points_confondus_laissent_au_trait_son_angle()
    {
        var trait = Trait(0, 0, 0, 100);
        trait.PoserLesExtremites(50, 50, 50, 50);

        Assert.Equal(90, trait.Rotation);
        Assert.Equal(0, trait.Width);
        Assert.False(double.IsNaN(trait.X) || double.IsNaN(trait.Y));
    }

    [Theory]
    [InlineData(80, 3, 80, 0)]          // presque horizontal : horizontal, même longueur
    [InlineData(-80, -3, -80, 0)]
    [InlineData(3, 80, 0, 80)]          // presque vertical : vertical
    [InlineData(3, -80, 0, -80)]
    [InlineData(70, 60, 65, 65)]        // presque diagonal : la projection sur 45°
    [InlineData(-70, 60, -65, 65)]
    public void Sous_Maj_le_trait_se_tient_a_0_45_ou_90_degres(double dx, double dy, double attenduX, double attenduY)
    {
        var (x, y) = ShapeFrame.Contraindre(100, 100, 100 + dx, 100 + dy);

        Assert.Equal(100 + attenduX, x, 9);
        Assert.Equal(100 + attenduY, y, 9);
    }

    [Fact]
    public void Sous_Maj_un_trait_vertical_ne_penche_pas_dun_ulp()
    {
        // cos(90°) ne vaut pas zéro en virgule flottante : la direction est tenue par table.
        var (x, _) = ShapeFrame.Contraindre(123.25, 40, 124, 500);
        Assert.Equal(123.25, x);
    }

    // ------------------------------------------------------------------ survol

    [Fact]
    public void Le_survol_repond_a_moins_de_trois_points_de_lencre_et_plus_au_dela()
    {
        var trait = Trait(100, 200, 300, 200);
        trait.StrokeThickness = 1;
        var bord = ShapeFrame.ToleranceDeSurvol + trait.StrokeThickness / 2;

        Assert.True(trait.HitTest(200, 200));
        Assert.True(trait.HitTest(200, 200 + bord - 0.1));
        Assert.True(trait.HitTest(200, 200 - bord + 0.1));
        Assert.False(trait.HitTest(200, 200 + bord + 0.1));
        Assert.False(trait.HitTest(200, 200 - bord - 0.1));

        // Au-delà d'une extrémité, on compte jusqu'à elle, et non jusqu'à la droite.
        Assert.True(trait.HitTest(300 + bord - 0.1, 200));
        Assert.False(trait.HitTest(300 + bord + 0.1, 200));
        Assert.False(trait.HitTest(302.5, 202.5));
    }

    [Fact]
    public void Le_survol_suit_un_trait_tourne()
    {
        var trait = Trait(100, 100, 300, 300);
        trait.StrokeThickness = 1;

        Assert.True(trait.HitTest(200 + 2, 200 - 2));     // 2,8 pt de l'axe
        Assert.False(trait.HitTest(200 + 3, 200 - 3));    // 4,2 pt
        // Le cadre d'un bloc tourné n'y est pour rien : le coin de l'emprise est loin du trait.
        Assert.False(trait.HitTest(110, 290));
    }

    [Fact]
    public void Un_rectangle_se_prend_toujours_dans_son_cadre()
    {
        var forme = new ShapeFrame { X = 10, Y = 10, Width = 100, Height = 50 };

        Assert.True(forme.HitTest(60, 35));
        Assert.False(forme.HitTest(60, 63));
    }

    // ------------------------------------------------------------------ fichier

    [Fact]
    public void Shape_Line_fait_laller_retour_json_au_caractere_pres()
    {
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        var fleche = Trait(72.5, 100.25, 300.125, 412.75);
        fleche.StartArrow = true;
        fleche.StrokeThickness = 2.5;
        fleche.StrokeStyle = StrokeStyle.Dashed;
        document.Pages[0].Frames.Add(fleche);
        document.Pages[0].Frames.Add(Trait(500, 700, 40, 700));

        var json = DocumentSerializer.ToJson(document);
        Assert.Contains("\"Shape\": \"Line\"", json);
        Assert.Contains("\"StartArrow\": true", json);
        Assert.Contains("\"EndArrow\": false", json);

        var relu = DocumentSerializer.FromJson(json);
        Assert.Equal(json, DocumentSerializer.ToJson(relu));

        var trait = Assert.IsType<ShapeFrame>(relu.Pages[0].Frames[0]);
        Assert.True(trait.IsLine);
        Assert.True(trait.StartArrow);
        Assert.False(trait.EndArrow);
        Assert.Equal(72.5, trait.Debut.X, 9);
        Assert.Equal(412.75, trait.Fin.Y, 9);
        Assert.Equal(180, ((ShapeFrame)relu.Pages[0].Frames[1]).Rotation);
    }

    [Fact]
    public void Un_trait_ecrit_a_la_main_se_lit()
    {
        var document = DocumentSerializer.FromJson("""
            { "FormatVersion": 5, "Pages": [ { "Name": "Page 1", "Frames": [
              { "$kind": "shape", "Name": "Flèche", "X": 10, "Y": 20, "Width": 100, "Height": 0,
                "Stroke": "#FF000000", "Shape": "Line", "EndArrow": true } ] } ] }
            """);

        var trait = Assert.IsType<ShapeFrame>(document.Pages[0].Frames.Single());
        Assert.Equal(ShapeKind.Line, trait.Shape);
        Assert.Equal("Trait", trait.Kind);
        Assert.True(trait.EndArrow);
        Assert.False(trait.StartArrow);
        Assert.Equal((110.0, 20.0), trait.Fin);
    }

    [Fact]
    public void Une_forme_ecrite_avant_le_trait_se_lit_sans_pointe()
    {
        var document = DocumentSerializer.FromJson("""
            { "FormatVersion": 5, "Pages": [ { "Name": "Page 1", "Frames": [
              { "$kind": "shape", "X": 10, "Y": 20, "Width": 100, "Height": 50, "Shape": "Ellipse" } ] } ] }
            """);

        var forme = Assert.IsType<ShapeFrame>(document.Pages[0].Frames.Single());
        Assert.False(forme.IsLine || forme.StartArrow || forme.EndArrow);
    }

    // ------------------------------------------------------------------ pointes

    [Fact]
    public void Le_fut_sarrete_sous_ses_pointes_et_pas_ailleurs()
    {
        var trait = Trait(0, 0, 100, 0);
        trait.StrokeThickness = 4;
        var (longueur, demi) = ShapeFrame.Pointe(4);

        Assert.Equal((12.0, 6.0), (longueur, demi));
        Assert.Equal(new DessinDuTrait(0, 0, 100, 0, null, null), trait.Dessin() with { PointeAuDebut = null, PointeALaFin = null });
        Assert.Null(trait.Dessin().PointeAuDebut);

        trait.StartArrow = true;
        trait.EndArrow = true;
        var dessin = trait.Dessin();
        Assert.Equal(6, dessin.X1);
        Assert.Equal(94, dessin.X2);
        Assert.Equal(new[] { (0.0, 0.0), (12.0, -6.0), (12.0, 6.0) }, dessin.PointeAuDebut);
        Assert.Equal(new[] { (100.0, 0.0), (88.0, -6.0), (88.0, 6.0) }, dessin.PointeALaFin);

        // Un filet fin reçoit une pointe qu'on voit : l'épaisseur y compte pour 2 pt au moins.
        Assert.Equal((6.0, 3.0), ShapeFrame.Pointe(0.5));
    }

    [Fact]
    public void Un_trait_plus_court_que_ses_pointes_ne_sinverse_pas()
    {
        var trait = Trait(0, 0, 4, 0);
        trait.StartArrow = true;
        trait.EndArrow = true;

        var dessin = trait.Dessin();
        Assert.Equal(2, dessin.X1);
        Assert.Equal(2, dessin.X2);
    }

    [Fact]
    public void La_decoupe_dun_groupe_laisse_aux_pointes_leur_largeur()
    {
        var trait = Trait(0, 10, 100, 10);
        trait.StrokeThickness = 1;
        Assert.Equal(0.5, Decoupe.DemiFilet(trait));

        trait.EndArrow = true;
        Assert.Equal(3, Decoupe.DemiFilet(trait));
    }

    // ------------------------------------------------------------------ export

    [Fact]
    public void Au_pdf_le_trait_est_un_trace_m_l_S_et_non_un_rectangle()
    {
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        document.Pages[0].Frames.Add(Trait(100, 200, 300, 200));

        var flux = Flux(document);

        // Un coup de crayon, de (100, 200) à (300, 200) — l'axe Y du PDF monte depuis le bas
        // de la page : 841,89 − 200.
        Assert.Matches(@"100 641\.89 m\s+300 641\.89 l\s+S", flux);
        Assert.DoesNotMatch(@"\bre\b", flux);
        Assert.DoesNotMatch(@"\bf\b", flux);
    }

    [Fact]
    public void Au_pdf_chaque_pointe_est_un_triangle_plein()
    {
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        var fleche = Trait(100, 200, 300, 200);
        fleche.StartArrow = true;
        fleche.EndArrow = true;
        document.Pages[0].Frames.Add(fleche);

        var flux = Flux(document);

        // Le fût, raccourci de trois points à chaque bout (une demi-pointe de 6 pt)…
        Assert.Matches(@"103 641\.89 m\s+297 641\.89 l\s+S", flux);
        // … et deux triangles, chacun la pointe sur son extrémité, remplis.
        Assert.Equal(2, Regex.Matches(flux, @"m\s+[-\d.]+ [-\d.]+ l\s+[-\d.]+ [-\d.]+ l\s+h\s+f").Count);
        Assert.Matches(@"100 641\.89 m\s+106 644\.89 l\s+106 638\.89 l\s+h\s+f", flux);
        Assert.Matches(@"300 641\.89 m\s+294 644\.89 l\s+294 638\.89 l\s+h\s+f", flux);
    }

    [Fact]
    public void Un_trait_sans_contour_ne_dessine_rien()
    {
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        var trait = Trait(100, 200, 300, 200);
        trait.Stroke = null;
        trait.EndArrow = true;
        document.Pages[0].Frames.Add(trait);

        var flux = Flux(document);

        Assert.DoesNotMatch(@"\b(S|f)\b", flux);
    }

    /// <summary>
    /// Le flux de la première page exportée, décompressé : PDFsharp relit son propre PDF,
    /// comme <c>FondsTests</c> le fait pour le fond de page.
    /// </summary>
    private static string Flux(Document document)
    {
        var chemin = Path.Combine(Path.GetTempPath(), $"trait-{Guid.NewGuid():N}.pdf");
        try
        {
            var rapport = PdfExporter.Export(document, chemin, ecran: null);
            Assert.Empty(rapport.Warnings);

            using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(chemin, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify);
            var octets = pdf.Pages[0].Contents.Elements.GetDictionary(0)!.Stream.UnfilteredValue;
            return string.Concat(octets.Select(o => (char)o));
        }
        finally
        {
            if (File.Exists(chemin)) File.Delete(chemin);
        }
    }
}
