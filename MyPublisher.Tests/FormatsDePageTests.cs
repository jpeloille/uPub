using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Le catalogue des formats de page, et le format libre.
///
/// Ces tests épinglent le calcul lui-même, et non l'accord de deux rendus : une
/// comparaison écran ↔ PDF ne verrait rien d'un format faux, les deux le lisant sur le
/// même document. Ce qui peut casser ici n'est pas visible à l'œil — un format ajouté
/// qui se confond avec un autre à la tolérance d'un point, et le sélecteur affiche
/// alors le mauvais nom sans que rien ne bouge sur la page.
/// </summary>
public class FormatsDePageTests
{
    [Fact]
    public void Les_quatre_formats_ajoutes_mesurent_ce_que_le_catalogue_de_Publisher_annonce()
    {
        // Fiche 01, en pouces : B4 13,898 × 9,843 — B5 9,843 × 6,929 —
        // Executive 10,5 × 7,25 — Tabloid 17 × 11. Les deux ISO se disent mieux en
        // millimètres, les deux autres en pouces : c'est ainsi qu'ils sont définis.
        Assert.Equal(250, Units.PointsToMm(PageSize.B4.Width), precision: 1);
        Assert.Equal(353, Units.PointsToMm(PageSize.B4.Height), precision: 1);
        Assert.Equal(176, Units.PointsToMm(PageSize.B5.Width), precision: 1);
        Assert.Equal(250, Units.PointsToMm(PageSize.B5.Height), precision: 1);

        Assert.Equal(7.25 * 72, PageSize.Executive.Width, precision: 6);
        Assert.Equal(10.5 * 72, PageSize.Executive.Height, precision: 6);
        Assert.Equal(11 * 72, PageSize.Tabloid.Width, precision: 6);
        Assert.Equal(17 * 72, PageSize.Tabloid.Height, precision: 6);
    }

    [Fact]
    public void Chaque_format_du_catalogue_se_reconnait_dans_les_deux_orientations()
    {
        // Le vrai risque de ce poste : deux formats que la tolérance d'un point de
        // « Match » ne sépare plus. On interroge donc TOUT le catalogue plutôt que les
        // quatre entrées neuves — une collision se crée aussi bien en ajoutant un
        // format demain qu'aujourd'hui, et elle se lit comme un mauvais nom dans le
        // sélecteur, jamais comme une page de travers.
        foreach (var format in PageSize.All)
        {
            Assert.Same(format, PageSize.Match(format.Width, format.Height));
            Assert.Same(format, PageSize.Match(format.Height, format.Width));
        }
    }

    [Fact]
    public void Tout_format_est_declare_en_portrait()
    {
        // « Match » compare le petit côté au premier nombre : un format déclaré à
        // l'envers ne se reconnaîtrait dans aucune des deux orientations.
        Assert.All(PageSize.All, format => Assert.True(format.Width < format.Height));
    }

    [Fact]
    public void Un_format_libre_ne_correspond_a_aucune_entree()
    {
        // 100 × 200 mm, le format de la recette du poste.
        Assert.Null(PageSize.Match(Units.MmToPoints(100), Units.MmToPoints(200)));
        Assert.Null(PageSize.Match(Units.MmToPoints(200), Units.MmToPoints(100)));
    }

    [Fact]
    public void Un_format_libre_se_relit_au_nombre_pres()
    {
        // Le format libre n'ajoute aucune propriété au fichier : ce sont les deux
        // « double » que le document portait déjà. Ce test le dit une fois — sans quoi
        // on croirait, à lire le panneau, qu'il y a quelque chose de neuf à migrer.
        var document = Document.CreateDefault();
        document.PageWidth = Units.MmToPoints(100);
        document.PageHeight = Units.MmToPoints(200);

        var relu = DocumentSerializer.FromJson(DocumentSerializer.ToJson(document));

        Assert.Equal(document.PageWidth, relu.PageWidth);
        Assert.Equal(document.PageHeight, relu.PageHeight);
        Assert.Equal(Document.CurrentFormatVersion, relu.FormatVersion);
    }

    [Fact]
    public void Les_neuf_formats_du_catalogue_portent_des_noms_distincts()
    {
        Assert.Equal(9, PageSize.All.Count);
        Assert.Equal(PageSize.All.Count, PageSize.All.Select(format => format.Name).Distinct().Count());
    }
}
