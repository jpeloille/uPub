using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;
using MyPublisher.Views;

namespace MyPublisher.Banc;

/// <summary>
/// Ouvre la vraie fenêtre de MyPublisher sans affichage, et rend la page telle que
/// l'écran la dessine.
///
/// C'est le remplaçant permanent d'un outillage qui a été monté puis jeté à chaque
/// poste : les recettes de C1, C2a, C2b, C3c et C6 ont toutes été jouées avec un
/// harnais de ce genre. Toutes se rejouent désormais, sous <c>Scripts/recettes/</c> :
/// C1 et C6 rendent leur page par ce banc, C2 et C3 jouent leurs gestes dans
/// <c>MyPublisher.Tests.Interface</c>. Sur ce poste — session Wayland, où une copie d'écran d'une fenêtre
/// Avalonia sort noire — c'est aussi le seul moyen de voir ce que l'application
/// affiche.
/// </summary>
public static class Banc
{
    /// <summary>
    /// Monte la plate-forme. <c>UseHeadlessDrawing = false</c> est le point décisif :
    /// sans lui, Avalonia accepte de « rendre » sans jamais produire un pixel, et la
    /// comparaison écran/PDF porterait sur une image vide sans qu'on le voie.
    /// </summary>
    public static AppBuilder Configurer() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>
    /// Ouvre un document dans une vraie fenêtre. Le chemin est passé au modèle de vue
    /// avant l'ouverture : la fenêtre propose son catalogue de modèles quand aucun
    /// document n'est chargé, et une boîte modale n'a rien à faire dans un banc.
    /// </summary>
    public static (Window Fenetre, MainWindowViewModel Vm) Ouvrir(string document)
    {
        var vm = new MainWindowViewModel();
        vm.LoadFrom(document);

        // RenderScale vaut Zoom × 96/72. À ce zoom-là il vaut exactement 1 : une unité
        // de mise en page est alors un point typographique, comme l'annonce le README,
        // et la page se rend sans transformation à compenser. C'est la même mécanique
        // que celle qu'utilise l'utilisateur, réglée sur une valeur qui se mesure.
        vm.Zoom = 1 / Units.PointToDip;

        var fenetre = new MainWindow { DataContext = vm, Width = 1400, Height = 900 };
        fenetre.Show();
        Battre();
        return (fenetre, vm);
    }

    /// <summary>
    /// Laisse la boucle d'interface traiter ce qui est en attente — liaisons, mise en
    /// page, rendu. À appeler après chaque geste : sans cela on mesure l'état d'avant.
    /// </summary>
    public static void Battre(int tours = 4)
    {
        for (var i = 0; i < tours; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// Le panneau qui porte la page, à l'intérieur de la mise à l'échelle. Ses
    /// dimensions y valent exactement le format de page en points typographiques :
    /// c'est ce qui permet de rendre à un nombre entier de pixels par point, et donc
    /// de comparer au PDF sans facteur de conversion.
    /// </summary>
    public static Panel Page(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Panel>().FirstOrDefault(p => p.Name == "PageRoot")
        ?? throw new InvalidOperationException(
            "PageRoot introuvable : la planche a-t-elle été renommée dans PageCanvas.axaml ?");

    /// <summary>
    /// Rend la page hors champ à l'échelle demandée. À deux pixels par point, l'image
    /// est directement comparable à « pdftoppm -r 144 » : 144 points par pouce sur
    /// 72 points typographiques, soit les mêmes deux pixels.
    /// </summary>
    public static RenderTargetBitmap Rendre(Panel page, double pixelsParPoint)
    {
        // Le piège, et il est silencieux. « PageRoot » est mesuré en points, mais il
        // vit sous le LayoutTransformControl qui applique le zoom : rendre le panneau
        // directement dessine son contenu à RenderScale fois sa taille, tandis que
        // Bounds, lui, reste en points. On obtient alors une image à la bonne taille
        // dont le contenu déborde d'un facteur 96/72 — un bandeau de 118 pt sort à
        // 157 pt — et rien ne le signale.
        //
        // On neutralise donc la transformation avant de rendre, plutôt que de la
        // compenser : c'est la seule façon d'être sûr qu'une unité vaut un point,
        // ce que promet le commentaire de PageCanvas.axaml. On vérifie ici que
        // l'appelant l'a bien fait — le contrôle porte sur la matrice, car le
        // LayoutTransformControl pose une transformation même à l'identité.
        var facteur = page.RenderTransform?.Value.M11 ?? 1;
        if (Math.Abs(facteur - 1) > 1e-6)
            throw new InvalidOperationException(
                $"La page est mise à l'échelle {facteur:0.####} au lieu de 1. Régler le zoom " +
                "à 1 / Units.PointToDip avant de rendre, pour que RenderScale vaille 1.");

        // La page est mesurée en points ; sa taille en pixels s'arrondit vers le haut
        // pour ne jamais rogner un bord.
        var largeur = (int)Math.Ceiling(page.Bounds.Width * pixelsParPoint);
        var hauteur = (int)Math.Ceiling(page.Bounds.Height * pixelsParPoint);
        if (largeur <= 0 || hauteur <= 0)
            throw new InvalidOperationException(
                $"La page n'a pas de taille ({page.Bounds}). La mise en page a-t-elle été jouée ?");

        // 96 points par pouce est l'unité d'Avalonia : la multiplier donne l'échelle.
        var cible = new RenderTargetBitmap(
            new PixelSize(largeur, hauteur),
            new Vector(96 * pixelsParPoint, 96 * pixelsParPoint));
        cible.Render(page);
        return cible;
    }
}
