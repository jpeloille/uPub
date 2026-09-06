using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
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

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Ouvrir la vraie fenêtre sur un document et relever l'encre qu'elle dessine.
///
/// Ces quelques lignes étaient dans <c>RetraitsTests</c>, qui les avait écrites en
/// premier ; elles en sont sorties au poste R3, qui en a besoin pour mesurer autre
/// chose. Ce n'est pas un rangement de commodité : c'est le piège du banc — <c>PageRoot</c>
/// est mesuré en points mais vit sous la mise à l'échelle du zoom — qu'on ne veut garder
/// qu'à un seul endroit. Deux copies, et la seconde oublie l'assertion.
/// </summary>
public static class Rendu
{
    /// <summary>Deux pixels par point : la même échelle que <c>pdftoppm -r 144</c>.</summary>
    public const double PixelsParPoint = 2;

    public sealed record Image(byte[] Pixels, int Largeur, int Hauteur);

    /// <summary>Une fenêtre sur un document, sans horloge ni configuration de machine.</summary>
    public static (Window Fenetre, MainWindowViewModel Vm) Ouvrir(Document document)
    {
        var vm = new MainWindowViewModel(
            contacts: new BusinessInfoStore(),
            horloge: () => new DateTime(2026, 9, 6));

        vm.Document = document;
        vm.CurrentPage = document.Pages[0];
        vm.FilePath = "banc.mypub";   // évite l'ouverture du catalogue au démarrage
        // À ce zoom, une unité de mise en page vaut exactement un point typographique.
        vm.Zoom = 1 / Units.PointToDip;
        // Les repères ne s'impriment pas ; ils n'ont rien à faire dans une mesure d'encre.
        vm.ShowGuides = false;

        var fenetre = new MainWindow { DataContext = vm, Width = 1400, Height = 900 };
        fenetre.Show();
        Battre();
        return (fenetre, vm);
    }

    public static void Battre(int tours = 4)
    {
        for (var i = 0; i < tours; i++) Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Une page seule, posée dans un document par ailleurs vierge.</summary>
    public static Document Feuille(params Frame[] blocs)
    {
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        foreach (var bloc in blocs) document.Pages[0].Frames.Add(bloc);
        return document;
    }

    /// <summary>
    /// La page rendue hors champ à deux pixels par point — la même échelle que
    /// <c>pdftoppm -r 144</c>, celle des recettes des postes.
    /// </summary>
    public static Image Encre(Window fenetre)
    {
        var page = fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");

        // Le piège du banc, gardé ici comme là-bas : PageRoot est mesuré en points mais
        // vit sous la mise à l'échelle du zoom. Le rendre sans avoir neutralisé
        // celle-ci dessinerait son contenu 96/72 trop grand dans une image à la bonne
        // taille, et rien ne le signalerait.
        var facteur = page.RenderTransform?.Value.M11 ?? 1;
        Assert.True(Math.Abs(facteur - 1) < 1e-6, $"la page est à l'échelle {facteur}, pas 1");

        var taille = new PixelSize(
            (int)Math.Ceiling(page.Bounds.Width * PixelsParPoint),
            (int)Math.Ceiling(page.Bounds.Height * PixelsParPoint));

        using var rendu = new RenderTargetBitmap(
            taille, new Vector(96 * PixelsParPoint, 96 * PixelsParPoint));
        rendu.Render(page);

        var pixels = new byte[taille.Width * taille.Height * 4];
        using var tampon = new WriteableBitmap(taille, rendu.Dpi, PixelFormat.Bgra8888);
        using (var verrou = tampon.Lock())
        {
            rendu.CopyPixels(new PixelRect(0, 0, taille.Width, taille.Height),
                verrou.Address, pixels.Length, verrou.RowBytes);
            System.Runtime.InteropServices.Marshal.Copy(verrou.Address, pixels, 0, pixels.Length);
        }

        return new Image(pixels, taille.Width, taille.Height);
    }

    /// <summary>
    /// Un pixel franchement sombre. Le seuil écarte le lissage des bords, qui
    /// déplacerait le fer d'un pixel ou deux selon la lettre.
    /// </summary>
    public static bool EstEncre(Image image, int x, int y)
    {
        if (x < 0 || y < 0 || x >= image.Largeur || y >= image.Hauteur) return false;

        var i = (y * image.Largeur + x) * 4;
        return image.Pixels[i] < 128 && image.Pixels[i + 1] < 128 && image.Pixels[i + 2] < 128;
    }
}
