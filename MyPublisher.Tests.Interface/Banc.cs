using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using MyPublisher;
using MyPublisher.Views;
using MyPublisher.Tests.Interface;

[assembly: AvaloniaTestApplication(typeof(Hote))]

namespace MyPublisher.Tests.Interface;

/// <summary>
/// L'hôte des tests d'interface : la vraie application, sur une plate-forme sans
/// affichage mais avec un vrai moteur de rendu.
///
/// <c>UseHeadlessDrawing = false</c> est le point décisif. Sans lui, Avalonia accepte
/// de « rendre » sans jamais produire un pixel : les tests passeraient sur des images
/// vides, ce qui est exactement le piège que le banc doit fermer.
/// </summary>
public static class Hote
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            // Les polices comme l'application les monte (poste Q13).
            .AvecLesPolicesDeMyPublisher();
}
