using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;

namespace MyPublisher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Les gestes des menus en français, « Ctrl+Maj+G », comme les bulles (poste N13) : ici,
        // que l'application, l'hôte des tests et le banc, qui montent tous App, les voient.
        GestesEnFrancais.Brancher();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // La sonde du clavier (N10), qui ne s'éveille que si sa variable nomme un fichier.
            SondeDuClavier.BrancherSiDemandee();

            // Les préférences de l'utilisateur — les documents récents (poste N11) : chargées
            // ici, et seulement ici. Un modèle de vue construit ailleurs, par un test ou par le
            // banc, en prend un magasin en mémoire, qui n'écrit nulle part.
            var viewModel = new MainWindowViewModel(preferences: Preferences.Charger());

            // « MyPublisher document.mypub » ouvre directement le document, ce qui
            // permet aussi d'associer l'extension au gestionnaire de fichiers.
            var startupFile = desktop.Args?.FirstOrDefault(arg =>
                !arg.StartsWith('-') && File.Exists(arg));

            if (startupFile is not null)
            {
                try
                {
                    viewModel.LoadFrom(startupFile);
                }
                catch (Exception ex)
                {
                    viewModel.Status = $"Ouverture impossible : {ex.Message}";
                }
            }

            var fenetre = new MainWindow { DataContext = viewModel };

            // La question avant de perdre des modifications (poste N11) : branchée ici, jamais
            // par la fenêtre elle-même. Les tests ferment des fenêtres modifiées, et une
            // question que personne ne regarde les bloquerait.
            fenetre.AvantDePerdre = nom => QuestionAvantDePerdre.Poser(fenetre, nom);

            desktop.MainWindow = fenetre;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}