using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using MyPublisher.ViewModels;
using MyPublisher.Views;

namespace MyPublisher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            var viewModel = new MainWindowViewModel();

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

            desktop.MainWindow = new MainWindow { DataContext = viewModel };
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