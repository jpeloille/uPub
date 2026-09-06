using System;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Ce que l'écran fait des remplissages — postes C5a et C5c.
///
/// Deux choses s'y éprouvent, et elles ne se remplacent pas. La première est que le fond
/// arrive à la bonne couleur au bon endroit, mesurée pixel par pixel : c'est ce qui
/// répond de l'accord avec l'export, dont la recette mesure les mêmes points sur le PDF.
/// La seconde est que la page se REPEINT quand on retouche le fond — par
/// <c>CaptureRenderedFrame</c>, parce que <c>RenderTargetBitmap.Render</c> redessine de
/// force et passerait au vert même si rien ne se rafraîchissait. Le défaut de
/// rafraîchissement de R1 avait vécu quatre postes faute de cette distinction.
/// </summary>
public class FondsTests
{
    /// <summary>La couleur d'un pixel de la page, en #AARRGGBB — l'écriture du modèle.</summary>
    private static string Teinte(Rendu.Image image, double x, double y)
    {
        var px = (int)Math.Round(x * Rendu.PixelsParPoint);
        var py = (int)Math.Round(y * Rendu.PixelsParPoint);
        var i = (py * image.Largeur + px) * 4;

        // Le tampon est en BGRA.
        return HexColor.Format(
            image.Pixels[i + 3], image.Pixels[i + 2], image.Pixels[i + 1], image.Pixels[i]);
    }

    [AvaloniaFact]
    public void Le_fond_de_page_couvre_la_page_entiere()
    {
        var document = Rendu.Feuille();
        document.Background = new SolidFill("#FF336699");
        var (fenetre, _) = Rendu.Ouvrir(document);

        var image = Rendu.Encre(fenetre);

        // Les quatre coins et le centre : un fond posé sur la zone de marges plutôt que
        // sur la page passerait un contrôle au centre seul.
        Assert.Equal("#FF336699", Teinte(image, 2, 2));
        Assert.Equal("#FF336699", Teinte(image, document.PageWidth - 2, 2));
        Assert.Equal("#FF336699", Teinte(image, 2, document.PageHeight - 2));
        Assert.Equal("#FF336699", Teinte(image, document.PageWidth / 2, document.PageHeight / 2));
    }

    [AvaloniaFact]
    public void Une_teinte_est_dessinee_a_la_couleur_que_le_modele_calcule()
    {
        // L'écran ne mélange rien lui-même : il pose la couleur que « TintFill.Uniforme »
        // a calculée, et le PDF pose la même. Un mélange écrit deux fois dériverait sans
        // qu'aucune comparaison des deux rendus ne le voie.
        var teinte = new TintFill("#FF804020", 50);
        var document = Rendu.Feuille(new ShapeFrame
        {
            X = 100, Y = 100, Width = 200, Height = 100, Fill = teinte, Stroke = null,
        });
        var (fenetre, _) = Rendu.Ouvrir(document);

        Assert.Equal(teinte.Uniforme, Teinte(Rendu.Encre(fenetre), 200, 150));
    }

    [AvaloniaFact]
    public void Un_degrade_va_bien_de_sa_premiere_couleur_a_la_seconde()
    {
        // Un dégradé horizontal, mesuré à ses deux bords. C'est ce que la recette mesure
        // au même endroit sur le PDF : si les deux extrémités étaient inversées, l'écran
        // et le PDF se tromperaient ensemble — mais pas ce test-ci, ni celui du noyau qui
        // épingle les extrémités.
        var document = Rendu.Feuille(new ShapeFrame
        {
            X = 100, Y = 100, Width = 200, Height = 100, Stroke = null,
            Fill = new GradientFill("#FFFF0000", "#FF0000FF", 0),
        });
        var (fenetre, _) = Rendu.Ouvrir(document);
        var image = Rendu.Encre(fenetre);

        var gauche = Teinte(image, 102, 150);
        var droite = Teinte(image, 298, 150);

        // Presque le rouge pur d'un côté, presque le bleu pur de l'autre : « presque »
        // parce qu'on mesure à deux points du bord et non sur le bord même.
        Assert.True(Rouge(gauche) > 200 && Bleu(gauche) < 60, $"bord gauche {gauche}");
        Assert.True(Bleu(droite) > 200 && Rouge(droite) < 60, $"bord droit {droite}");
    }

    private static int Rouge(string couleur) =>
        HexColor.TryParse(couleur, out _, out var r, out _, out _) ? r : -1;

    private static int Bleu(string couleur) =>
        HexColor.TryParse(couleur, out _, out _, out _, out var b) ? b : -1;

    // -------------------------------------------------------------- rafraîchissement

    [AvaloniaFact]
    public void Regler_le_fond_de_page_repeint_la_planche()
    {
        var document = Rendu.Feuille();
        var (fenetre, _) = Rendu.Ouvrir(document);
        var avant = Composee(fenetre);

        document.Background = new SolidFill("#FF336699");
        Rendu.Battre();

        Assert.NotEqual(avant, Composee(fenetre));
    }

    [AvaloniaFact]
    public void Regler_langle_dun_degrade_repeint_la_planche()
    {
        // Le piège de ce poste : régler l'angle modifie l'objet SUR PLACE, sans changer
        // l'instance que porte le bloc. « AffectsRender » n'y suffit donc pas.
        var degrade = new GradientFill("#FFFF0000", "#FF0000FF", 0);
        var document = Rendu.Feuille(new ShapeFrame
        {
            X = 100, Y = 100, Width = 200, Height = 100, Fill = degrade, Stroke = null,
        });
        var (fenetre, _) = Rendu.Ouvrir(document);
        var avant = Composee(fenetre);

        degrade.Angle = 90;
        Rendu.Battre();

        Assert.NotEqual(avant, Composee(fenetre));
    }

    [AvaloniaFact]
    public void Changer_la_couleur_dun_fond_uni_repeint_la_planche()
    {
        var uni = new SolidFill("#FFFF0000");
        var document = Rendu.Feuille(new ShapeFrame
        {
            X = 100, Y = 100, Width = 200, Height = 100, Fill = uni, Stroke = null,
        });
        var (fenetre, _) = Rendu.Ouvrir(document);
        var avant = Composee(fenetre);

        uni.Color = "#FF0000FF";
        Rendu.Battre();

        Assert.NotEqual(avant, Composee(fenetre));
    }

    /// <summary>
    /// L'image RÉELLEMENT composée de la fenêtre, et non un rendu forcé.
    /// <c>RenderTargetBitmap.Render</c> redessine l'arbre visuel à la demande, quel
    /// qu'ait été l'état d'invalidation : un test bâti dessus passe même quand rien ne
    /// se rafraîchit. Voir <see cref="RafraichissementTests"/>, qui l'a appris à ses
    /// dépens.
    /// </summary>
    private static byte[] Composee(Window fenetre)
    {
        var image = fenetre.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException("aucune image composée");

        using (image)
        {
            var pixels = new byte[image.PixelSize.Width * image.PixelSize.Height * 4];
            using var verrou = image.Lock();
            System.Runtime.InteropServices.Marshal.Copy(verrou.Address, pixels, 0, pixels.Length);
            return pixels;
        }
    }
}
