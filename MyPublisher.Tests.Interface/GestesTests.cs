using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les gestes, joués sur la vraie couche de saisie de la vraie fenêtre.
///
/// C'est le harnais que les postes C2a, C2b et C3c ont monté puis jeté trois fois.
/// Sur ce poste — session Wayland, où une copie d'écran d'une fenêtre Avalonia sort
/// noire — c'est aussi le seul moyen de vérifier ce que l'application affiche.
/// </summary>
public class GestesTests
{
    /// <summary>
    /// Une fenêtre sur un document neuf, sans lire la configuration de la machine ni
    /// l'horloge : un banc qui dépend de l'un ou de l'autre ne prouve rien deux jours
    /// de suite.
    /// </summary>
    private static (Window Fenetre, MainWindowViewModel Vm) Ouvrir(Document document)
    {
        var vm = new MainWindowViewModel(
            contacts: new BusinessInfoStore(),
            horloge: () => new DateTime(2026, 9, 6));

        vm.Document = document;
        vm.CurrentPage = document.Pages[0];
        vm.FilePath = "banc.mypub";   // évite l'ouverture du catalogue au démarrage
        vm.Zoom = 1 / Units.PointToDip;

        var fenetre = new MainWindow { DataContext = vm, Width = 1400, Height = 900 };
        fenetre.Show();
        Battre();
        return (fenetre, vm);
    }

    private static void Battre(int tours = 4)
    {
        for (var i = 0; i < tours; i++) Dispatcher.UIThread.RunJobs();
    }

    /// <summary>La couche de saisie transparente posée sur la page.</summary>
    private static Control Saisie(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Control>().First(c => c.Name == "InputLayer");

    private static Panel Page(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");

    /// <summary>Un point de la page, en points typographiques, dans le repère fenêtre.</summary>
    private static Point SurLaPage(Window fenetre, double x, double y)
    {
        var page = Page(fenetre);
        var origine = page.TranslatePoint(new Point(0, 0), fenetre) ?? new Point(0, 0);
        return new Point(origine.X + x, origine.Y + y);
    }

    private static Document DocumentAvecUnBloc(out ShapeFrame bloc)
    {
        var document = Document.CreateDefault();
        bloc = new ShapeFrame
        {
            Name = "Carré", X = 100, Y = 100, Width = 60, Height = 60, Fill = "#FFCC0000",
        };
        document.Pages[0].Frames.Add(bloc);
        return document;
    }

    [AvaloniaFact]
    public void La_fenetre_rend_reellement_des_pixels()
    {
        // Le premier test à écrire, et celui qui garde tous les autres : si le rendu
        // est vide, chaque comparaison d'images passerait sans rien prouver.
        var document = DocumentAvecUnBloc(out _);
        var (fenetre, _) = Ouvrir(document);

        using var image = new RenderTargetBitmap(
            new PixelSize((int)(Page(fenetre).Bounds.Width * 2), (int)(Page(fenetre).Bounds.Height * 2)),
            new Vector(192, 192));
        image.Render(Page(fenetre));

        Assert.True(image.PixelSize.Width > 100);
        Assert.NotEqual(0, EncreRouge(image));
    }

    [AvaloniaFact]
    public void Un_bloc_se_deplace_a_la_souris()
    {
        var document = DocumentAvecUnBloc(out var bloc);
        var (fenetre, vm) = Ouvrir(document);

        vm.SelectOnly(bloc);
        Battre();

        var saisie = Saisie(fenetre);
        fenetre.MouseDown(SurLaPage(fenetre, 130, 130), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 230, 190));
        fenetre.MouseUp(SurLaPage(fenetre, 230, 190), MouseButton.Left);
        Battre();

        Assert.NotNull(saisie);
        Assert.Equal(200, bloc.X, precision: 0);
        Assert.Equal(160, bloc.Y, precision: 0);
    }

    [AvaloniaFact]
    public void Un_deplacement_sannule()
    {
        // L'annulation compare deux JSON : un geste qui ne change rien n'empile aucun
        // pas. Celui-ci change quelque chose, il doit donc se défaire exactement.
        var document = DocumentAvecUnBloc(out var bloc);
        var (fenetre, vm) = Ouvrir(document);

        vm.SelectOnly(bloc);
        Battre();

        // Relevé après la sélection, et non avant : sélectionner fait passer la
        // position par le panneau de propriétés, donc par un aller-retour en
        // millimètres qui laisse 7·10⁻¹⁴ pt derrière lui. C'est un résidu du
        // correctif de C2a, mesuré et sans effet — il ne salit pas le document —
        // mais il n'a rien à voir avec ce que ce test éprouve.
        var avant = DocumentSerializer.ToJson(vm.Document);

        fenetre.MouseDown(SurLaPage(fenetre, 130, 130), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 230, 190));
        fenetre.MouseUp(SurLaPage(fenetre, 230, 190), MouseButton.Left);
        Battre();

        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Battre();

        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void Un_bloc_lache_pres_dune_colonne_sy_aimante()
    {
        // La recette de C2a, rendue rejouable : « quatre blocs lâchés à des endroits
        // différents à moins de 4 pt du bord de la deuxième colonne s'y posent tous
        // exactement ». Ici, un seul bloc et un seul bord — le calcul lui-même est
        // éprouvé sans fenêtre, dans MyPublisher.Tests.
        var document = DocumentAvecUnBloc(out var bloc);
        document.ColumnCount = 3;
        document.ColumnSpacing = 12;
        var (fenetre, vm) = Ouvrir(document);

        var colonne = document.VerticalGuides.OrderBy(g => Math.Abs(g - 250)).First();

        vm.SelectOnly(bloc);
        Battre();

        // On lâche à deux points du bord de colonne : l'aimantation doit finir le geste.
        var visee = colonne + 2;
        fenetre.MouseDown(SurLaPage(fenetre, bloc.X + 30, bloc.Y + 30), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, visee + 30, bloc.Y + 30));
        fenetre.MouseUp(SurLaPage(fenetre, visee + 30, bloc.Y + 30), MouseButton.Left);
        Battre();

        Assert.Equal(colonne, bloc.X, precision: 6);
    }

    /// <summary>Compte les pixels franchement rouges — l'encre du bloc de test.</summary>
    private static int EncreRouge(RenderTargetBitmap image)
    {
        var largeur = image.PixelSize.Width;
        var hauteur = image.PixelSize.Height;
        var pixels = new byte[largeur * hauteur * 4];

        using var tampon = new WriteableBitmap(
            image.PixelSize, image.Dpi, Avalonia.Platform.PixelFormat.Bgra8888);

        using (var verrou = tampon.Lock())
        {
            image.CopyPixels(new PixelRect(0, 0, largeur, hauteur), verrou.Address,
                pixels.Length, verrou.RowBytes);
            System.Runtime.InteropServices.Marshal.Copy(verrou.Address, pixels, 0, pixels.Length);
        }

        var rouges = 0;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            // BGRA : rouge franc, peu de bleu et peu de vert.
            if (pixels[i + 2] > 150 && pixels[i] < 100 && pixels[i + 1] < 100) rouges++;
        }

        return rouges;
    }
}
