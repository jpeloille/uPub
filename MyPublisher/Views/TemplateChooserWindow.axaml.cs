using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MyPublisher.Services;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Écran de choix du modèle. Il rend le choix retenu, ou null si l'on renonce — la
/// fenêtre appelante décide alors quoi en faire, et n'ouvre rien qu'on n'ait demandé.
/// </summary>
public partial class TemplateChooserWindow : Window
{
    public TemplateChooserWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private TemplateChooserViewModel? Vm => DataContext as TemplateChooserViewModel;

    private void OnCreate(object? sender, RoutedEventArgs e) => Close(Vm?.Selected);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void OnChoiceDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: TemplateChoice choice }) Close(choice);
    }
}
