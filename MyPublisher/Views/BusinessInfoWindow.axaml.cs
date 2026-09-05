using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Dialogue des jeux de coordonnées. Il rend le magasin à enregistrer, ou null si
/// l'utilisateur a renoncé : c'est la fenêtre appelante qui décide d'en faire quelque
/// chose, et rien n'est écrit sur le disque tant qu'elle ne l'a pas fait.
/// </summary>
public partial class BusinessInfoWindow : Window
{
    private static readonly FilePickerFileType ImageType =
        new("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"] };

    public BusinessInfoWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private BusinessInfoViewModel? Vm => DataContext as BusinessInfoViewModel;

    private void OnSave(object? sender, RoutedEventArgs e) => Close(Vm?.ToStore());

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private async void OnChooseLogo(object? sender, RoutedEventArgs e)
    {
        if (Vm?.Selected is not { } set) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choisir un logo",
            AllowMultiple = false,
            FileTypeFilter = [ImageType]
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) set.LogoPath = path;
    }

    private void OnRemoveLogo(object? sender, RoutedEventArgs e)
    {
        if (Vm?.Selected is { } set) set.LogoPath = "";
    }
}
