using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using MyPublisher.Services;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType DocumentType =
        new("Document MyPublisher") { Patterns = ["*." + DocumentSerializer.Extension] };

    private static readonly FilePickerFileType PdfType =
        new("Document PDF") { Patterns = ["*.pdf"] };

    private static readonly FilePickerFileType ImageType =
        new("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"] };

    /// <summary>
    /// Le panneau de propriétés modifie le modèle par liaison directe : on encadre
    /// la saisie d'un champ par une transaction, ce qui rend chaque édition annulable.
    /// </summary>
    private bool _propertyEditOpen;

    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => AttachViewModel();
        AttachViewModel();

        // Le catalogue s'ouvre au démarrage, comme dans Publisher — sauf si un document
        // a été passé en argument : on ne demande pas ce qu'il faut ouvrir à quelqu'un
        // qui vient de le dire.
        Opened += async (_, _) =>
        {
            if (Vm?.FilePath is null) await ChooseTemplateAsync();
        };
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void AttachViewModel()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm = DataContext as MainWindowViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Changer de sélection reconstruit les champs du panneau : le contrôle qui
        // avait le focus quitte l'arbre visuel sans émettre LostFocus. Sans cette
        // remise à zéro, le verrou resterait fermé et plus aucune édition ne serait
        // annulable pour le reste de la session.
        if (e.PropertyName == nameof(MainWindowViewModel.Selected)) ClosePropertyEdit();
    }

    // ---------------------------------------------------------------- fichiers

    private async void OnNew(object? sender, RoutedEventArgs e) => await ChooseTemplateAsync();

    /// <summary>
    /// Propose le catalogue et donne suite au choix. Renoncer ne touche à rien : le
    /// document en cours reste ce qu'il est.
    /// </summary>
    private async Task ChooseTemplateAsync()
    {
        if (Vm is not { } vm) return;

        var dialog = new TemplateChooserWindow
        {
            DataContext = new TemplateChooserViewModel(TemplateStore.Load())
        };

        if (await dialog.ShowDialog<TemplateChoice?>(this) is not { } choice) return;

        if (choice.IsBlank)
        {
            vm.NewDocumentCommand.Execute(null);
            return;
        }

        try
        {
            vm.StartFromTemplate(choice.Path!);
        }
        catch (Exception ex)
        {
            vm.Status = $"Modèle illisible : {ex.Message}";
        }
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Ouvrir un document",
            AllowMultiple = false,
            FileTypeFilter = [DocumentType]
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;

        try
        {
            vm.LoadFrom(path);
        }
        catch (Exception ex)
        {
            vm.Status = $"Ouverture impossible : {ex.Message}";
        }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;

        if (vm.FilePath is null)
        {
            await SaveAsAsync(vm);
            return;
        }

        TrySave(vm, vm.FilePath);
    }

    private async void OnSaveAs(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) await SaveAsAsync(vm);
    }

    private async Task SaveAsAsync(MainWindowViewModel vm)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Enregistrer le document",
            SuggestedFileName = vm.Document.Title,
            DefaultExtension = DocumentSerializer.Extension,
            FileTypeChoices = [DocumentType]
        });

        if (file?.TryGetLocalPath() is { } path) TrySave(vm, path);
    }

    private static void TrySave(MainWindowViewModel vm, string path)
    {
        try
        {
            vm.SaveTo(path);
        }
        catch (Exception ex)
        {
            vm.Status = $"Enregistrement impossible : {ex.Message}";
        }
    }

    private async void OnExportPdf(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exporter en PDF",
            SuggestedFileName = vm.Document.Title,
            DefaultExtension = "pdf",
            FileTypeChoices = [PdfType]
        });

        if (file?.TryGetLocalPath() is not { } path) return;

        try
        {
            vm.ExportPdf(path);
        }
        catch (Exception ex)
        {
            vm.Status = $"Export impossible : {ex.Message}";
        }
    }

    private async void OnInsertImage(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Insérer une image",
            AllowMultiple = false,
            FileTypeFilter = [ImageType]
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) vm.AddImage(path);
    }

    private void OnQuit(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Ouvre les jeux de coordonnées. Le dialogue travaille sur une copie : il ne rend
    /// un magasin que si l'utilisateur a validé, et c'est seulement alors qu'on écrit.
    /// </summary>
    private async void OnEditBusinessInfo(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;

        var dialog = new BusinessInfoWindow { DataContext = new BusinessInfoViewModel(vm.Contacts) };
        if (await dialog.ShowDialog<BusinessInfoStore?>(this) is not { } saved) return;

        try
        {
            saved.Save();
            vm.ApplyContacts(saved);
        }
        catch (Exception ex)
        {
            vm.Status = $"Coordonnées non enregistrées : {ex.Message}";
        }
    }

    // ---------------------------------------------------------------- propriétés

    private void OnPropertyPanelGotFocus(object? sender, GotFocusEventArgs e)
    {
        // On referme d'abord une éventuelle transaction restée ouverte : chaque champ
        // compte ainsi pour une seule étape d'annulation, même si le passage de l'un
        // à l'autre n'a pas émis LostFocus.
        ClosePropertyEdit();

        _propertyEditOpen = true;
        Vm?.BeginEdit();
    }

    private void OnPropertyPanelLostFocus(object? sender, RoutedEventArgs e) => ClosePropertyEdit();

    private void ClosePropertyEdit()
    {
        if (!_propertyEditOpen) return;

        _propertyEditOpen = false;
        Vm?.CommitEdit();
    }

    // ---------------------------------------------------------------- clavier

    /// <summary>
    /// Un contrôle qui consomme les frappes : zone de texte, ou liste déroulante,
    /// qui cherche une entrée à la lettre. Les raccourcis d'outil à une lettre ne
    /// doivent pas les court-circuiter — taper « T » dans la liste des polices doit
    /// chercher une police, pas armer l'outil texte.
    /// </summary>
    private static bool ConsumesTyping(IInputElement? element) =>
        element is Visual visual &&
        visual.GetSelfAndVisualAncestors().Any(ancestor => ancestor is TextBox or ComboBox);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Vm is not { } vm)
        {
            base.OnKeyDown(e);
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.N: OnNew(this, e); e.Handled = true; return;
                case Key.O: OnOpen(this, e); e.Handled = true; return;
                case Key.S: OnSave(this, e); e.Handled = true; return;
                case Key.E: OnExportPdf(this, e); e.Handled = true; return;
            }
        }

        // Pendant une saisie, les touches appartiennent au contrôle qui a le focus.
        if (PageView.IsEditingText || ConsumesTyping(FocusManager?.GetFocusedElement()))
        {
            base.OnKeyDown(e);
            return;
        }

        // Raccourcis d'outils à une lettre, comme dans les logiciels de dessin.
        // Ils ne peuvent pas gêner la frappe : on a déjà rendu la main plus haut
        // dès qu'un champ de texte détient le focus.
        if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.V: vm.ActiveTool = EditorTool.Select; e.Handled = true; return;
                case Key.T: vm.ActiveTool = EditorTool.Text; e.Handled = true; return;
                case Key.H: vm.ActiveTool = EditorTool.Heading; e.Handled = true; return;
                case Key.R: vm.ActiveTool = EditorTool.Rectangle; e.Handled = true; return;
                case Key.E: vm.ActiveTool = EditorTool.Ellipse; e.Handled = true; return;
                // « B » comme bande : « T » appartient au texte.
                case Key.B: vm.ActiveTool = EditorTool.Table; e.Handled = true; return;
            }
        }

        var fine = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.Delete or Key.Back:
                vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                return;
            case Key.Left: vm.NudgeSelection(-1, 0, fine); e.Handled = true; return;
            case Key.Right: vm.NudgeSelection(1, 0, fine); e.Handled = true; return;
            case Key.Up: vm.NudgeSelection(0, -1, fine); e.Handled = true; return;
            case Key.Down: vm.NudgeSelection(0, 1, fine); e.Handled = true; return;
            case Key.Escape:
                // Échap désarme l'outil s'il y en a un, et ne libère la sélection qu'ensuite.
                if (vm.IsDrawing) vm.ActiveTool = EditorTool.Select;
                else vm.Selected = null;
                e.Handled = true;
                return;
        }

        base.OnKeyDown(e);
    }
}
