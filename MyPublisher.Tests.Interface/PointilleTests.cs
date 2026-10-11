using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Le pointillé gris d'un bloc image — poste Q16.
///
/// « Repère visible tant qu'aucune image n'est chargée », disait <c>PageCanvas.axaml</c> ;
/// il était en réalité toujours dessiné. Une image cadrée sans déformation le laissait
/// voir sur le bord du bloc, et il n'obéissait pas à l'interrupteur des repères : il
/// entrait dans toute comparaison de l'écran au PDF. Trouvé par la recette de C1.
///
/// On le mesure à l'encre : les pixels gris du pointillé (#B0B0B0) relevés sur le pourtour
/// du bloc. Ce gris-là n'est porté par aucun autre repère — ceux de la page sont bleus ou
/// verts — ni par l'image, qui est magenta.
/// </summary>
public class PointilleTests
{
    // Un bloc en largeur, une image carrée cadrée sans déformation : l'image laisse le
    // pourtour du bloc nu à gauche et à droite, là où le pointillé se voyait.
    private const double X = 200, Y = 300, Largeur = 240, Hauteur = 120;

    [AvaloniaFact]
    [Trait("poste", "Q16")]
    public void Une_image_chargee_ne_montre_pas_le_pointille_reperes_eteints()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Bloc(ImageCarree())));
        Assert.False(vm.ShowGuides);

        // La mesure ne vaut que si l'image est bien là : sans elle, un bloc vide
        // repères éteints ne montre pas non plus le pointillé, et le test passerait
        // pour une autre raison que la sienne.
        var encre = Rendu.Encre(fenetre);
        Assert.True(Magenta(encre, X + Largeur / 2, Y + Hauteur / 2), "l'image n'est pas dessinée");

        Assert.Equal(0, Pointille(encre));
    }

    [AvaloniaFact]
    [Trait("poste", "Q16")]
    public void Une_image_chargee_ne_montre_pas_le_pointille_reperes_allumes()
    {
        // Le pointillé marque une place qui attend son image : l'image posée, il n'a plus
        // rien à marquer, même quand les repères sont allumés.
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Bloc(ImageCarree())));
        vm.ShowGuides = true;
        Rendu.Battre();

        var encre = Rendu.Encre(fenetre);
        Assert.True(Magenta(encre, X + Largeur / 2, Y + Hauteur / 2), "l'image n'est pas dessinée");
        Assert.Equal(0, Pointille(encre));
    }

    [AvaloniaFact]
    [Trait("poste", "Q16")]
    public void Un_bloc_sans_image_montre_le_pointille_reperes_allumes_et_le_cache_eteints()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Bloc("")));

        vm.ShowGuides = true;
        Rendu.Battre();
        var allumes = Pointille(Rendu.Encre(fenetre));

        vm.ShowGuides = false;
        Rendu.Battre();
        var eteints = Pointille(Rendu.Encre(fenetre));

        // Le pourtour fait 720 pt, soit 1 440 pixels ; le motif en noircit 4 sur 7.
        // Un seuil à 400 dit « le pointillé est là » sans dépendre du lissage.
        Assert.True(allumes > 400, $"repères allumés, {allumes} pixels de pointillé seulement");
        Assert.Equal(0, eteints);
    }

    private static ImageFrame Bloc(string chemin) => new()
    {
        Name = "Photo", X = X, Y = Y, Width = Largeur, Height = Hauteur,
        Path = chemin, Fit = ImageFit.Uniform, Stroke = null,
    };

    /// <summary>Les pixels du gris du pointillé, sur une bande de 2 pt de part et d'autre du pourtour.</summary>
    private static int Pointille(Rendu.Image encre)
    {
        var px = Rendu.PixelsParPoint;
        var (gauche, haut) = ((int)(X * px), (int)(Y * px));
        var (droite, bas) = ((int)((X + Largeur) * px), (int)((Y + Hauteur) * px));
        const int bande = 4;

        var compte = 0;
        for (var y = haut - bande; y < bas + bande; y++)
        {
            for (var x = gauche - bande; x < droite + bande; x++)
            {
                var surLePourtour = x < gauche + bande || x >= droite - bande
                                    || y < haut + bande || y >= bas - bande;
                if (surLePourtour && Gris(encre, x, y)) compte++;
            }
        }

        return compte;
    }

    /// <summary>Un gris neutre proche de #B0B0B0, lissage compris.</summary>
    private static bool Gris(Rendu.Image encre, int x, int y)
    {
        var i = (y * encre.Largeur + x) * 4;
        int b = encre.Pixels[i], g = encre.Pixels[i + 1], r = encre.Pixels[i + 2];
        return Math.Abs(r - g) <= 6 && Math.Abs(g - b) <= 6 && r is >= 150 and <= 215;
    }

    private static bool Magenta(Rendu.Image encre, double x, double y)
    {
        var i = ((int)(y * Rendu.PixelsParPoint) * encre.Largeur + (int)(x * Rendu.PixelsParPoint)) * 4;
        return encre.Pixels[i + 2] > 200 && encre.Pixels[i + 1] < 60 && encre.Pixels[i] > 200;
    }

    /// <summary>Un PNG carré, magenta, dans un dossier à soi : le cache d'images est commun.</summary>
    private static string ImageCarree()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "mypublisher-q16-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        var chemin = Path.Combine(dossier, "carre.png");

        const int cote = 8;
        using var bitmap = new WriteableBitmap(
            new PixelSize(cote, cote), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        var ligne = new byte[cote * 4];
        for (var x = 0; x < cote; x++)
        {
            ligne[x * 4] = 0xFF;       // bleu
            ligne[x * 4 + 1] = 0x00;   // vert
            ligne[x * 4 + 2] = 0xFF;   // rouge
            ligne[x * 4 + 3] = 0xFF;   // opaque
        }

        using (var verrou = bitmap.Lock())
        {
            for (var y = 0; y < cote; y++) Marshal.Copy(ligne, 0, verrou.Address + y * verrou.RowBytes, ligne.Length);
        }

        bitmap.Save(chemin);
        return chemin;
    }
}
