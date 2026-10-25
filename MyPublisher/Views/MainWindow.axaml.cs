using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Models;
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

    private static readonly FilePickerFileType TextType =
        new("Texte") { Patterns = ["*.txt"] };

    /// <summary>
    /// Un texte rédigé ailleurs ne finit pas toujours en « .txt » — un « .md », un
    /// « .csv » : on laisse le choix.
    /// </summary>
    private static readonly FilePickerFileType AnyType =
        new("Tous les fichiers") { Patterns = ["*"] };

    /// <summary>
    /// Le dock modifie le modèle par liaison directe : on encadre la saisie d'un champ
    /// par une transaction, ce qui rend chaque édition annulable. Chaque onglet du dock
    /// et la barre contextuelle l'ouvrent et la ferment de la même façon (règle 2 de la
    /// phase I) ; « Blocs de la page », au pied du dock, en est exclu : son glissé ouvre
    /// son propre pas.
    /// </summary>
    private bool _propertyEditOpen;

    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();

        // En phase de tunnel : la liste consomme le clic pour sélectionner, et il faut
        // savoir d'où le geste part avant qu'elle ne le fasse. L'événement n'est pas
        // marqué traité — cliquer sur un bloc le sélectionne comme avant.
        ListeDesBlocs.AddHandler(PointerPressedEvent, OnBlockListPressed, RoutingStrategies.Tunnel);
        ListeDesBlocs.PointerMoved += OnBlockListMoved;
        ListeDesBlocs.PointerReleased += OnBlockListReleased;
        ListeDesBlocs.PointerCaptureLost += (_, _) => EndBlockDrag();

        // Changer d'onglet retire du dock le champ qui avait le focus, sans qu'il émette
        // toujours LostFocus : la transaction qu'il avait ouverte se referme ici. Seul le
        // changement d'onglet compte — l'événement monte aussi des listes du dock, et
        // refermer la transaction au choix d'une entrée laisserait les suivantes hors de
        // tout pas d'annulation.
        OngletsDuDock.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Source, OngletsDuDock)) ClosePropertyEdit();
        };

        // La fenêtre quittée Espace tenu ne verra pas son relâchement : la main se lâche ici (I6).
        Deactivated += (_, _) => PageView.MainTenue = false;

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
        // annulable pour le reste de la session. Replier le dock fait de même.
        if (e.PropertyName is nameof(MainWindowViewModel.Selected) or nameof(MainWindowViewModel.DockVisible))
        {
            ClosePropertyEdit();
        }

        // Le bloc choisi sur la planche se montre dans « Blocs de la page », qui n'en
        // montre que six : la liste ne le fait pas d'elle-même quand la sélection lui
        // arrive avant qu'elle ait construit ses lignes. Après la mise en page, donc.
        if (e.PropertyName == nameof(MainWindowViewModel.Selected) && _vm?.Selected is { } bloc)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(_vm?.Selected, bloc)) ListeDesBlocs.ScrollIntoView(bloc);
            }, DispatcherPriority.Loaded);
        }
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

    /// <summary>
    /// Choisit l'image d'un remplissage. Le bouton vit dans le gabarit du remplissage :
    /// c'est donc son contexte de données qui désigne l'objet à renseigner, et le même
    /// geste sert au fond d'un bloc comme à celui de la page. Le reste — pas
    /// d'annulation, cache d'images — est au modèle de vue, où un test peut l'atteindre
    /// sans dialogue de fichier.
    /// </summary>
    private async void OnChooseFillImage(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        if ((sender as Control)?.DataContext is not ImageFill fond) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Image de remplissage",
            AllowMultiple = false,
            FileTypeFilter = [ImageType]
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) vm.ChoisirImageDeFond(fond, path);
    }

    /// <summary>
    /// Remplit le bloc de texte sélectionné à partir d'un fichier (poste B2). Le menu et
    /// le panneau du bloc mènent ici ; seul le sélecteur de fichier est à la fenêtre, le
    /// reste — encodage, style, pas d'annulation — est au modèle de vue
    /// (<see cref="MainWindowViewModel.ImporterTexte"/>), où un test l'atteint.
    /// </summary>
    private async void OnImportText(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { PeutImporterTexte: true } vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importer un texte",
            AllowMultiple = false,
            FileTypeFilter = [TextType, AnyType]
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) ImporterTexte(path);
    }

    /// <summary>
    /// Ce que fait le geste une fois le fichier choisi : refermer la saisie sur la page et
    /// la transaction du panneau, puis passer le chemin au modèle de vue. Laissée
    /// ouverte, la saisie sur la page garderait sa zone à l'écran, et la frappe qui la
    /// précède partagerait le pas d'annulation de l'import. Publique pour qu'un test la
    /// joue sans le sélecteur de fichier, qui ne s'ouvre pas sans affichage.
    /// </summary>
    /// <returns>Vrai si le texte a été posé.</returns>
    public bool ImporterTexte(string chemin)
    {
        if (Vm is not { } vm) return false;

        PageView.EndTextEdit();
        ClosePropertyEdit();
        return vm.ImporterTexte(chemin);
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

    // ------------------------------------------------------- ordre des blocs à la souris

    /// <summary>Déplacement, en pixels, à partir duquel un clic devient un glissement.</summary>
    private const double DragThreshold = 4;

    private Frame? _draggedBlock;
    private Point _dragOrigin;
    private bool _dragging;

    /// <summary>Rang d'insertion visé, c'est-à-dire le rang <b>avant</b> lequel on dépose.</summary>
    private int _dropIndex = -1;

    /// <summary>
    /// Le geste est bâti sur les pointeurs et non sur <c>DragDrop.DoDragDrop</c>.
    ///
    /// Deux raisons, et la seconde suffirait. La plate-forme sans affichage n'inscrit
    /// aucune source de glisser-déposer : un geste bâti sur elle ne pourrait pas être
    /// joué par le banc, et ce poste serait éprouvé sur la parole. Et c'est déjà ainsi
    /// que la planche travaille — pointeur brut, ordre de superposition explicite —
    /// ce qui fait un idiome de moins à connaître.
    /// </summary>
    private void OnBlockListPressed(object? sender, PointerPressedEventArgs e)
    {
        EndBlockDrag();

        if (!e.GetCurrentPoint(ListeDesBlocs).Properties.IsLeftButtonPressed) return;
        if (e.Source is not Visual source) return;
        if (source.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()
            is not { DataContext: Frame frame }) return;

        _draggedBlock = frame;
        _dragOrigin = e.GetPosition(ListeDesBlocs);
    }

    private void OnBlockListMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedBlock is null) return;

        if (!e.GetCurrentPoint(ListeDesBlocs).Properties.IsLeftButtonPressed)
        {
            EndBlockDrag();
            return;
        }

        var point = e.GetPosition(ListeDesBlocs);

        // On ne s'engage qu'au-delà du seuil : sans lui, le tremblement d'un clic
        // ordinaire réordonnerait la pile à l'insu de celui qui sélectionne un bloc.
        if (!_dragging)
        {
            if (Math.Abs(point.Y - _dragOrigin.Y) < DragThreshold) return;

            _dragging = true;
            e.Pointer.Capture(ListeDesBlocs);
        }

        _dropIndex = DropIndexAt(point);
        ShowDropMark(_dropIndex);
    }

    private void OnBlockListReleased(object? sender, PointerReleasedEventArgs e)
    {
        // L'état est relevé avant de rendre la capture : la rendre ferme le geste.
        var frame = _draggedBlock;
        var dragging = _dragging;
        var index = _dropIndex;

        if (dragging) e.Pointer.Capture(null);
        EndBlockDrag();

        if (!dragging || frame is null || Vm is not { } vm) return;
        if (vm.CurrentPage is not { } page) return;
        var pile = PileInversee.De(page.Frames);
        if (pile.IndexOf(frame) is var depart && depart < 0) return;

        // Le rang visé est un point d'insertion, entre deux lignes de la liste. Retirer le
        // bloc de sa ligne fait remonter d'un cran toutes celles qui la suivent, ce point
        // compris : un dépôt plus bas dans la liste vaut donc une ligne de moins qu'il n'y
        // paraît. La liste montre la pile de l'avant vers le fond (poste I2) : la ligne
        // d'arrivée se lit ensuite au rang miroir de la page.
        if (index > depart) index--;

        if (vm.ReorderFrame(frame, pile.Miroir(index))) e.Handled = true;
    }

    private void EndBlockDrag()
    {
        _draggedBlock = null;
        _dragging = false;
        _dropIndex = -1;
        TraitDInsertion.IsVisible = false;
    }

    /// <summary>
    /// Les éléments réellement construits, avec leur rang et leur bande verticale dans
    /// la liste. Le rang est celui de la liste, et non la position parmi les éléments
    /// construits : la liste ne construit que ce qu'elle affiche, et compter les éléments
    /// présents désignerait le mauvais bloc dès qu'elle défile.
    /// </summary>
    private List<(int Index, double Top, double Height)> RealisedItems()
    {
        var items = new List<(int, double, double)>();

        for (var i = 0; i < ListeDesBlocs.ItemCount; i++)
        {
            if (ListeDesBlocs.ContainerFromIndex(i) is not { } container) continue;
            if (container.TranslatePoint(new Point(0, 0), ListeDesBlocs) is not { } origin) continue;

            items.Add((i, origin.Y, container.Bounds.Height));
        }

        return items;
    }

    /// <summary>
    /// Le rang d'insertion que désigne le pointeur : celui de l'élément dont on survole
    /// la moitié haute, le suivant si l'on est dans sa moitié basse.
    /// </summary>
    private int DropIndexAt(Point point)
    {
        var items = RealisedItems();
        if (items.Count == 0) return 0;

        foreach (var (index, top, height) in items)
        {
            if (point.Y < top + height / 2) return index;
            if (point.Y < top + height) return index + 1;
        }

        return items[^1].Index + 1;
    }

    private void ShowDropMark(int index)
    {
        var items = RealisedItems();
        if (items.Count == 0) return;

        var y = items.FirstOrDefault(item => item.Index == index) is { Height: > 0 } cible
            ? cible.Top
            : items[^1].Top + items[^1].Height;

        Canvas.SetLeft(TraitDInsertion, 0);
        Canvas.SetTop(TraitDInsertion, Math.Max(0, y - 1));
        TraitDInsertion.Width = ListeDesBlocs.Bounds.Width;
        TraitDInsertion.IsVisible = true;
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
                case Key.L: vm.ActiveTool = EditorTool.Line; e.Handled = true; return;
                // « B » comme bande : « T » appartient au texte.
                case Key.B: vm.ActiveTool = EditorTool.Table; e.Handled = true; return;
                case Key.Z: vm.ActiveTool = EditorTool.Zoom; e.Handled = true; return;
                // Espace tenu : la main, sans changer d'outil, jusqu'au relâchement (I6). Pas
                // quand un bouton a le focus — Espace l'actionnerait — ni pendant une saisie,
                // écartée plus haut.
                case Key.Space when FocusManager?.GetFocusedElement() is not Button:
                    PageView.MainTenue = true;
                    e.Handled = true;
                    return;
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
                // Échap désarme l'outil s'il y en a un — la main et la loupe comprises —, et ne
                // libère la sélection qu'ensuite.
                if (vm.ActiveTool != EditorTool.Select) vm.ActiveTool = EditorTool.Select;
                else vm.Selected = null;
                e.Handled = true;
                return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// Espace relâché rend l'outil d'avant (I6). Toujours, sans regarder le focus : une main
    /// restée tenue parce que le relâchement serait allé ailleurs ne se lâcherait plus.
    /// </summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space) PageView.MainTenue = false;
        base.OnKeyUp(e);
    }
}
