using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Converters;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;
using Image = MyPublisher.Tests.Interface.Rendu.Image;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les retraits et l'espacement de paragraphe, relevés sur ce que la fenêtre dessine.
///
/// Pourquoi ne pas s'en tenir à <c>CompositeurTests</c>, qui éprouve déjà le calcul :
/// parce qu'il éprouve le calcul, et non son emploi. Un rendu qui oublierait d'ajouter
/// <c>Retrait</c> à l'abscisse d'origine, ou <c>EspaceApres</c> à l'avance, passerait
/// tous les tests du noyau sans en déranger un seul. On mesure donc l'encre elle-même,
/// à deux pixels par point — la même échelle et la même grandeur que la recette du
/// poste mesure sur le PDF.
/// </summary>
public class RetraitsTests
{
    // La fenêtre, l'image et le seuil d'encre sont ceux de « Rendu » : une seule
    // définition pour les deux postes qui mesurent ce que la fenêtre dessine.
    private const double PixelsParPoint = Rendu.PixelsParPoint;
    private static Image Encre(Window fenetre) => Rendu.Encre(fenetre);
    private static bool EstEncre(Image image, int x, int y) => Rendu.EstEncre(image, x, y);
    private static (Window Fenetre, MainWindowViewModel Vm) Ouvrir(Document document) =>
        Rendu.Ouvrir(document);
    private static void Battre(int tours = 4) => Rendu.Battre(tours);

    /// <summary>
    /// Deux paragraphes de deux lignes chacun, dans un bloc trop étroit pour qu'ils
    /// tiennent sur une seule. Les deux paragraphes portent le même texte à un mot
    /// près et se coupent au même endroit : d'un paragraphe à l'autre, ce qui sépare
    /// deux mesures est le retrait et rien d'autre. D'une ligne à la suivante, il reste
    /// l'approche du premier glyphe — un pixel, mesuré — d'où la tolérance.
    /// </summary>
    private static TextFrame Bloc() => new()
    {
        Name = "Liste", X = 40, Y = 40, Width = 200, Height = 140,
        FontFamily = "DejaVu Sans", FontSize = 11, LineHeight = 1.25,
        PaddingLeft = 0, PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0,
        Text = "nnnn premiere ligne de liste, et sa suite\n"
             + "nnnn seconde ligne de liste, et sa suite",
    };

    private static Document Feuille(TextFrame bloc) => Rendu.Feuille(bloc);

    /// <summary>L'avance d'une ligne à la suivante, telle que l'écran la calcule.</summary>
    private static double Avance(TextFrame bloc) => Compositeur.Avance(
        InterligneConverter.HauteurNaturelle(bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic),
        bloc.LineHeight);

    [AvaloniaFact]
    public void Un_retrait_negatif_fait_pendre_la_premiere_ligne_a_lecran()
    {
        // Retrait gauche de 18 pt, et son opposé en première ligne : celle-ci repart au
        // fer du bloc, les suivantes rentrent. C'est la liste à puces des trente-six
        // paragraphes d'ATA 21 — sans elle, la structure du document disparaît.
        var bloc = Bloc();
        bloc.IndentLeft = 18;
        bloc.IndentFirstLine = -18;

        var image = Encre(Ouvrir(Feuille(bloc)).Fenetre);

        Assert.Equal(18 * PixelsParPoint, Fer(image, bloc, 1) - Fer(image, bloc, 0), tolerance: 3.0);
    }

    [AvaloniaFact]
    public void Sans_retrait_les_deux_lignes_partent_du_meme_fer()
    {
        // Le témoin, et il n'est pas décoratif : sans lui, la mesure ci-dessus pourrait
        // tenir à ce que les deux lignes ne commencent pas par la même lettre.
        var bloc = Bloc();
        var image = Encre(Ouvrir(Feuille(bloc)).Fenetre);

        Assert.Equal(0, Fer(image, bloc, 1) - Fer(image, bloc, 0), tolerance: 3.0);
    }

    [AvaloniaFact]
    public void Un_retrait_de_paragraphe_ne_deplace_que_son_paragraphe()
    {
        // L'écart repéré par indice de paragraphe, éprouvé à l'écran : le second
        // paragraphe rentre de 24 pt, le premier ne bouge pas. C'est le cas des
        // documents réels, où les paragraphes d'un même bloc n'ont pas tous le même
        // retrait.
        var bloc = Bloc();
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 1, Length = 1, IndentLeft = 24 });

        var image = Encre(Ouvrir(Feuille(bloc)).Fenetre);

        Assert.Equal(0, Fer(image, bloc, 1) - Fer(image, bloc, 0), tolerance: 3.0);
        Assert.Equal(24 * PixelsParPoint, Fer(image, bloc, 2) - Fer(image, bloc, 0), tolerance: 3.0);
        Assert.Equal(24 * PixelsParPoint, Fer(image, bloc, 3) - Fer(image, bloc, 0), tolerance: 3.0);
    }

    [AvaloniaFact]
    public void Lespace_apres_le_paragraphe_ecarte_les_deux_paragraphes_a_lecran()
    {
        // Deux paragraphes de deux lignes : l'espace ne s'ajoute qu'entre eux, et non
        // entre les deux lignes d'un même paragraphe. La troisième ligne descend donc
        // d'un espace, et la seconde ne bouge pas.
        var sans = Bloc();
        var imageSans = Encre(Ouvrir(Feuille(sans)).Fenetre);

        var avec = Bloc();
        avec.SpaceAfter = 10;
        var imageAvec = Encre(Ouvrir(Feuille(avec)).Fenetre);

        Assert.Equal(Haut(imageSans, sans, 1), Haut(imageAvec, avec, 1), tolerance: 3.0);
        Assert.Equal(
            Haut(imageSans, sans, 2) + 10 * PixelsParPoint, Haut(imageAvec, avec, 2), tolerance: 3.0);
    }

    [AvaloniaFact]
    public void Le_panneau_accepte_un_retrait_de_premiere_ligne_negatif()
    {
        // Tous les autres champs de mesure de ce panneau — marges, épaisseurs — ont un
        // minimum à zéro. Copié tel quel ici, il rendrait la liste à puces inatteignable
        // à la souris tout en la laissant marcher dans le fichier : un réglage offert
        // qui ne s'atteint pas est pire qu'un réglage absent.
        var bloc = Bloc();
        var (fenetre, vm) = Ouvrir(Feuille(bloc));

        vm.SelectOnly(bloc);
        Battre();

        var champ = fenetre.GetVisualDescendants().OfType<NumericUpDown>()
            .First(c => c.Name == "RetraitPremiereLigne");

        Assert.True(champ.Minimum < 0, "un minimum à zéro interdirait le retrait négatif");

        champ.Value = -18;
        Battre();

        Assert.Equal(-18, bloc.IndentFirstLine);
    }

    // -------------------------------------------------------------- la mesure

    /// <summary>
    /// Colonne du pixel encré le plus à gauche de la n-ième ligne du bloc, en pixels.
    ///
    /// La bande de balayage se déduit de l'avance de ligne, et la recherche se limite à
    /// la largeur du bloc : le bord de la page et l'ombre qui la borde sont sombres eux
    /// aussi, et une recherche partant de la colonne zéro les trouverait d'abord.
    /// </summary>
    private static double Fer(Image image, TextFrame bloc, int ligne)
    {
        var avance = Avance(bloc);
        var haut = (int)((bloc.Y + bloc.PaddingTop + ligne * avance + 1) * PixelsParPoint);
        var bas = (int)((bloc.Y + bloc.PaddingTop + (ligne + 1) * avance - 1) * PixelsParPoint);

        for (var x = (int)(bloc.X * PixelsParPoint); x < (bloc.X + bloc.Width) * PixelsParPoint; x++)
        {
            for (var y = Math.Max(0, haut); y <= Math.Min(image.Hauteur - 1, bas); y++)
            {
                if (EstEncre(image, x, y)) return x;
            }
        }

        throw new InvalidOperationException(
            $"Aucune encre sur la ligne {ligne} du bloc : le texte est-il rendu ?");
    }

    /// <summary>
    /// Ligne du pixel encré le plus haut au-dessous du sommet de la n-ième ligne, en
    /// pixels. C'est la mesure verticale que l'espacement déplace.
    /// </summary>
    private static double Haut(Image image, TextFrame bloc, int ligne)
    {
        var depart = (int)((bloc.Y + bloc.PaddingTop + ligne * Avance(bloc)) * PixelsParPoint);

        for (var y = Math.Max(0, depart); y < image.Hauteur; y++)
        {
            for (var x = (int)(bloc.X * PixelsParPoint); x < (bloc.X + bloc.Width) * PixelsParPoint; x++)
            {
                if (EstEncre(image, x, y)) return y;
            }
        }

        throw new InvalidOperationException(
            $"Aucune encre sous la ligne {ligne} du bloc : le texte est-il rendu ?");
    }

}
