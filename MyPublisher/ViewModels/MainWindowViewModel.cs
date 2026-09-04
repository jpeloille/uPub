using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;
using MyPublisher.Services;

namespace MyPublisher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int MaxHistory = 60;

    /// <summary>Historique d'annulation par instantané JSON du document complet.</summary>
    private sealed record Snapshot(string Json, int PageIndex, int SelectionIndex);

    private readonly List<Snapshot> _undo = [];
    private readonly List<Snapshot> _redo = [];

    private Snapshot? _editBaseline;
    private int _editDepth;

    /// <summary>Décalage en cascade des blocs insérés, pour éviter la superposition parfaite.</summary>
    private int _insertOffset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageCountLabel))]
    private Document _document = Document.CreateDefault();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageNumber))]
    private Page? _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedText))]
    [NotifyPropertyChangedFor(nameof(SelectedShape))]
    [NotifyPropertyChangedFor(nameof(SelectedImage))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(BringToFrontCommand))]
    [NotifyCanExecuteChangedFor(nameof(BringForwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendBackwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendToBackCommand))]
    [NotifyCanExecuteChangedFor(nameof(AlignCentreHorizontallyCommand))]
    [NotifyCanExecuteChangedFor(nameof(AlignCentreVerticallyCommand))]
    [NotifyCanExecuteChangedFor(nameof(FitToMarginsCommand))]
    private Frame? _selected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RenderScale))]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    private double _zoom = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string? _filePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    [ObservableProperty] private string _status = "Prêt";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDrawing))]
    private EditorTool _activeTool = EditorTool.Select;

    [ObservableProperty] private PageSize? _selectedPageSize;

    [ObservableProperty] private bool _isLandscape;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CursorLabel))]
    private double _cursorX;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CursorLabel))]
    private double _cursorY;

    /// <summary>
    /// Évite que la synchronisation du format de page ne se reboucle sur elle-même :
    /// appliquer un format modifie le document, ce qui rejouerait le même code.
    /// </summary>
    private bool _syncingPageSetup;

    public MainWindowViewModel()
    {
        CurrentPage = Document.Pages.FirstOrDefault();
        FontFamilies = LoadSystemFonts();
        SyncPageSetup();
    }

    /// <summary>Aligne les contrôles de format sur les dimensions réelles du document.</summary>
    private void SyncPageSetup()
    {
        _syncingPageSetup = true;
        try
        {
            IsLandscape = Document.PageWidth > Document.PageHeight;

            var shortSide = Math.Min(Document.PageWidth, Document.PageHeight);
            var longSide = Math.Max(Document.PageWidth, Document.PageHeight);
            SelectedPageSize = PageSize.All.FirstOrDefault(size =>
                Math.Abs(size.Width - shortSide) < 1 && Math.Abs(size.Height - longSide) < 1);
        }
        finally
        {
            _syncingPageSetup = false;
        }
    }

    partial void OnActiveToolChanged(EditorTool value)
    {
        Status = value == EditorTool.Select
            ? "Prêt"
            : "Tracez le cadre sur la page — un simple clic pose le bloc à sa taille par défaut.";
    }

    partial void OnSelectedPageSizeChanged(PageSize? value)
    {
        if (_syncingPageSetup || value is null) return;

        BeginEdit();
        Document.PageWidth = IsLandscape ? value.Height : value.Width;
        Document.PageHeight = IsLandscape ? value.Width : value.Height;
        CommitEdit();
    }

    partial void OnIsLandscapeChanged(bool value)
    {
        if (_syncingPageSetup) return;
        if (Document.PageWidth > Document.PageHeight == value) return;

        BeginEdit();
        (Document.PageWidth, Document.PageHeight) = (Document.PageHeight, Document.PageWidth);
        CommitEdit();
    }

    /// <summary>Déplace la sélection au clavier ; le pas fin sert au calage précis.</summary>
    public void NudgeSelection(double dx, double dy, bool fine)
    {
        if (Selected is null) return;

        var step = fine ? 0.5 : 3.0;
        BeginEdit();
        Selected.X += dx * step;
        Selected.Y += dy * step;
        CommitEdit();
    }

    public IReadOnlyList<string> FontFamilies { get; }

    public IReadOnlyList<PageSize> PageSizes => PageSize.All;

    public IReadOnlyList<TextAlign> TextAlignments { get; } =
        [TextAlign.Left, TextAlign.Center, TextAlign.Right, TextAlign.Justify];

    public IReadOnlyList<ImageFit> ImageFits { get; } =
        [ImageFit.Uniform, ImageFit.UniformToFill, ImageFit.Fill];

    /// <summary>Facteur points -> pixels écran, zoom inclus. Le seul endroit qui mélange les deux unités.</summary>
    public double RenderScale => Zoom * Units.PointToDip;

    public bool HasSelection => Selected is not null;
    public TextFrame? SelectedText => Selected as TextFrame;
    public ShapeFrame? SelectedShape => Selected as ShapeFrame;
    public ImageFrame? SelectedImage => Selected as ImageFrame;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public string ZoomLabel => $"{Zoom * 100:0} %";

    public string CursorLabel =>
        $"X {Units.PointsToMm(CursorX):0.0} mm   Y {Units.PointsToMm(CursorY):0.0} mm";

    public string PageCountLabel => $"{Document.Pages.Count} page(s)";

    public int CurrentPageNumber =>
        CurrentPage is null ? 0 : Document.Pages.IndexOf(CurrentPage) + 1;

    public string WindowTitle
    {
        get
        {
            var name = FilePath is null ? "Sans titre" : Path.GetFileName(FilePath);
            return $"{(IsDirty ? "• " : "")}{name} — MyPublisher";
        }
    }

    // ---------------------------------------------------------------- historique

    /// <summary>
    /// Ouvre une transaction d'édition. Les appels s'imbriquent : seul le premier
    /// capture l'état de référence, ce qui permet d'envelopper indifféremment un
    /// glisser-déposer complet ou une simple commande.
    /// </summary>
    public void BeginEdit()
    {
        if (_editDepth++ == 0) _editBaseline = Capture();
    }

    /// <summary>Ferme la transaction et n'empile un pas d'annulation que si le document a réellement changé.</summary>
    public void CommitEdit()
    {
        _editDepth = Math.Max(0, _editDepth - 1);
        if (_editDepth > 0 || _editBaseline is null) return;

        var baseline = _editBaseline;
        _editBaseline = null;

        if (DocumentSerializer.ToJson(Document) == baseline.Json) return;

        _undo.Add(baseline);
        if (_undo.Count > MaxHistory) _undo.RemoveAt(0);
        _redo.Clear();
        IsDirty = true;
        NotifyHistory();
    }

    /// <summary>
    /// Referme une transaction laissée ouverte par un champ encore actif. Annuler
    /// pendant une saisie doit d'abord valider cette saisie, sinon l'instantané de
    /// référence serait comparé à un document déjà restauré.
    /// </summary>
    private void EndPendingEdit()
    {
        if (_editDepth == 0) return;

        _editDepth = 1;
        CommitEdit();
    }

    [RelayCommand]
    private void Undo()
    {
        EndPendingEdit();
        if (_undo.Count == 0) return;

        var current = Capture();
        var target = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(current);
        Restore(target);
        IsDirty = true;
        NotifyHistory();
        Status = "Annulé";
    }

    [RelayCommand]
    private void Redo()
    {
        EndPendingEdit();
        if (_redo.Count == 0) return;

        var current = Capture();
        var target = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(current);
        Restore(target);
        IsDirty = true;
        NotifyHistory();
        Status = "Rétabli";
    }

    private Snapshot Capture() => new(
        DocumentSerializer.ToJson(Document),
        CurrentPage is null ? -1 : Document.Pages.IndexOf(CurrentPage),
        CurrentPage is null || Selected is null ? -1 : CurrentPage.Frames.IndexOf(Selected));

    private void Restore(Snapshot snapshot)
    {
        Document = DocumentSerializer.FromJson(snapshot.Json);

        CurrentPage = snapshot.PageIndex >= 0 && snapshot.PageIndex < Document.Pages.Count
            ? Document.Pages[snapshot.PageIndex]
            : Document.Pages.FirstOrDefault();

        Selected = CurrentPage is not null &&
                   snapshot.SelectionIndex >= 0 &&
                   snapshot.SelectionIndex < CurrentPage.Frames.Count
            ? CurrentPage.Frames[snapshot.SelectionIndex]
            : null;

        SyncPageSetup();
        NotifyPageList();
    }

    private void NotifyHistory()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // ---------------------------------------------------------------- fichiers

    [RelayCommand]
    private void NewDocument()
    {
        Document = Document.CreateDefault();
        CurrentPage = Document.Pages[0];
        Selected = null;
        FilePath = null;
        IsDirty = false;
        _undo.Clear();
        _redo.Clear();
        SyncPageSetup();
        NotifyPageList();
        NotifyHistory();
        Status = "Nouveau document";
    }

    public void LoadFrom(string path)
    {
        Document = DocumentSerializer.Load(path);
        if (Document.Pages.Count == 0) Document.Pages.Add(new Page { Name = "Page 1" });
        CurrentPage = Document.Pages[0];
        Selected = null;
        FilePath = path;
        IsDirty = false;
        _undo.Clear();
        _redo.Clear();
        SyncPageSetup();
        NotifyPageList();
        NotifyHistory();
        Status = $"Ouvert : {Path.GetFileName(path)}";
    }

    public void SaveTo(string path)
    {
        DocumentSerializer.Save(Document, path);
        FilePath = path;
        IsDirty = false;
        Status = $"Enregistré : {Path.GetFileName(path)}";
    }

    public void ExportPdf(string path)
    {
        var report = PdfExporter.Export(Document, path);
        Status = report.Warnings.Count == 0
            ? $"PDF exporté : {Path.GetFileName(path)} ({report.PageCount} page(s))"
            : $"PDF exporté avec {report.Warnings.Count} avertissement(s) : {report.Warnings[0]}";
    }

    // ---------------------------------------------------------------- insertion

    /// <summary>En dessous de cette taille, un tracé est traité comme un simple clic.</summary>
    private const double MinDrawnSize = 8;

    /// <summary>Un outil de tracé est armé : le plan de travail crée au lieu de sélectionner.</summary>
    public bool IsDrawing => ActiveTool != EditorTool.Select;

    /// <summary>
    /// Gabarit d'un outil : apparence du bloc, et taille utilisée quand on pose
    /// l'outil d'un simple clic au lieu de tracer un cadre.
    /// </summary>
    private Frame BuildFrame(EditorTool tool) => tool switch
    {
        EditorTool.Text => new TextFrame
        {
            Name = "Texte", Width = 260, Height = 90, Text = "Saisissez votre texte."
        },
        EditorTool.Heading => new TextFrame
        {
            Name = "Titre", Width = Document.MarginBoxWidth, Height = 60,
            Text = "Titre", FontSize = 28, Bold = true
        },
        EditorTool.Rectangle => new ShapeFrame { Name = "Rectangle", Width = 200, Height = 120 },
        EditorTool.Ellipse => new ShapeFrame
        {
            Name = "Ellipse", Shape = ShapeKind.Ellipse, Width = 160, Height = 160
        },
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "Outil sans gabarit de bloc.")
    };

    [RelayCommand]
    private void AddText() => InsertWithTool(EditorTool.Text);

    [RelayCommand]
    private void AddHeading() => InsertWithTool(EditorTool.Heading);

    [RelayCommand]
    private void AddRectangle() => InsertWithTool(EditorTool.Rectangle);

    [RelayCommand]
    private void AddEllipse() => InsertWithTool(EditorTool.Ellipse);

    /// <summary>Insertion par le menu : le bloc est posé dans la zone de composition.</summary>
    private void InsertWithTool(EditorTool tool)
    {
        var frame = BuildFrame(tool);
        (frame.X, frame.Y) = NextInsertPoint(frame.Width, frame.Height);
        Insert(frame);
    }

    /// <summary>
    /// Création par tracé sur la page. Un cadre trop petit signale un clic simple :
    /// on retombe alors sur la taille par défaut de l'outil, comme le ferait un menu.
    /// </summary>
    public Frame? DrawWithActiveTool(double x, double y, double width, double height)
    {
        if (!IsDrawing || CurrentPage is null) return null;

        var frame = BuildFrame(ActiveTool);

        if (width >= MinDrawnSize && height >= MinDrawnSize)
        {
            frame.Width = width;
            frame.Height = height;
        }

        frame.X = x;
        frame.Y = y;

        // L'outil ne reste pas armé : on repasse à la sélection pour pouvoir ajuster
        // immédiatement le bloc qu'on vient de poser. Ce désarmement précède
        // l'insertion, sans quoi son message d'état écraserait celui du bloc ajouté.
        ActiveTool = EditorTool.Select;
        Insert(frame);
        return frame;
    }

    /// <summary>Insère une image en conservant ses proportions natives.</summary>
    public void AddImage(string path)
    {
        var width = 240.0;
        var height = 180.0;

        try
        {
            using var probe = new Avalonia.Media.Imaging.Bitmap(path);
            if (probe.PixelSize.Width > 0 && probe.PixelSize.Height > 0)
            {
                height = width * probe.PixelSize.Height / probe.PixelSize.Width;
            }
        }
        catch (Exception)
        {
            // Format illisible : on garde le gabarit par défaut, le bloc restera vide.
        }

        var (x, y) = NextInsertPoint(width, height);
        Insert(new ImageFrame
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Path = path, X = x, Y = y, Width = width, Height = height
        });
    }

    private void Insert(Frame frame)
    {
        if (CurrentPage is null) return;

        BeginEdit();
        CurrentPage.Frames.Add(frame);
        Selected = frame;
        CommitEdit();
        Status = $"{frame.Kind} ajouté";
    }

    /// <summary>
    /// Positionne le bloc dans la zone de composition, en décalant chaque insertion
    /// successive pour que les blocs ne se recouvrent pas exactement.
    /// </summary>
    private (double X, double Y) NextInsertPoint(double width, double height)
    {
        var step = _insertOffset % 8 * 12.0;
        _insertOffset++;

        var x = Document.MarginLeft + step;
        var y = Document.MarginTop + step;

        // On garde le bloc entièrement visible sur la page.
        x = Math.Min(x, Math.Max(0, Document.PageWidth - width));
        y = Math.Min(y, Math.Max(0, Document.PageHeight - height));
        return (x, y);
    }

    // ---------------------------------------------------------------- blocs

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteSelected()
    {
        if (CurrentPage is null || Selected is null) return;

        BeginEdit();
        CurrentPage.Frames.Remove(Selected);
        Selected = null;
        CommitEdit();
        Status = "Bloc supprimé";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DuplicateSelected()
    {
        if (CurrentPage is null || Selected is null) return;

        var copy = Selected.Clone();
        copy.X += 12;
        copy.Y += 12;

        BeginEdit();
        CurrentPage.Frames.Add(copy);
        Selected = copy;
        CommitEdit();
        Status = "Bloc dupliqué";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void BringToFront() => Reorder(int.MaxValue);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SendToBack() => Reorder(0);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void BringForward() => Reorder(IndexOfSelected() + 1);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SendBackward() => Reorder(IndexOfSelected() - 1);

    private int IndexOfSelected() =>
        CurrentPage is null || Selected is null ? -1 : CurrentPage.Frames.IndexOf(Selected);

    private void Reorder(int target)
    {
        if (CurrentPage is null || Selected is null) return;

        var from = CurrentPage.Frames.IndexOf(Selected);
        if (from < 0) return;

        var to = Math.Clamp(target, 0, CurrentPage.Frames.Count - 1);
        if (to == from) return;

        BeginEdit();
        CurrentPage.Frames.Move(from, to);
        CommitEdit();
    }

    /// <summary>Aligne le bloc sélectionné sur la zone de composition.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AlignCentreHorizontally()
    {
        if (Selected is null) return;
        BeginEdit();
        Selected.X = (Document.PageWidth - Selected.Width) / 2;
        CommitEdit();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AlignCentreVertically()
    {
        if (Selected is null) return;
        BeginEdit();
        Selected.Y = (Document.PageHeight - Selected.Height) / 2;
        CommitEdit();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void FitToMargins()
    {
        if (Selected is null) return;
        BeginEdit();
        Selected.X = Document.MarginLeft;
        Selected.Y = Document.MarginTop;
        Selected.Width = Document.MarginBoxWidth;
        Selected.Height = Document.MarginBoxHeight;
        CommitEdit();
    }

    // ---------------------------------------------------------------- pages

    [RelayCommand]
    private void AddPage()
    {
        BeginEdit();
        var index = CurrentPage is null ? Document.Pages.Count : Document.Pages.IndexOf(CurrentPage) + 1;
        var page = new Page { Name = $"Page {Document.Pages.Count + 1}" };
        Document.Pages.Insert(index, page);
        CurrentPage = page;
        Selected = null;
        RenumberPages();
        CommitEdit();
        NotifyPageList();
    }

    [RelayCommand]
    private void DuplicatePage()
    {
        if (CurrentPage is null) return;

        BeginEdit();
        var copy = new Page { Name = CurrentPage.Name + " (copie)" };
        foreach (var frame in CurrentPage.Frames) copy.Frames.Add(frame.Clone());
        Document.Pages.Insert(Document.Pages.IndexOf(CurrentPage) + 1, copy);
        CurrentPage = copy;
        Selected = null;
        CommitEdit();
        NotifyPageList();
    }

    public bool CanDeletePage => Document.Pages.Count > 1;

    [RelayCommand(CanExecute = nameof(CanDeletePage))]
    private void DeletePage()
    {
        if (CurrentPage is null || !CanDeletePage) return;

        BeginEdit();
        var index = Document.Pages.IndexOf(CurrentPage);
        Document.Pages.Remove(CurrentPage);
        CurrentPage = Document.Pages[Math.Clamp(index, 0, Document.Pages.Count - 1)];
        Selected = null;
        RenumberPages();
        CommitEdit();
        NotifyPageList();
    }

    private void NotifyPageList()
    {
        OnPropertyChanged(nameof(PageCountLabel));
        OnPropertyChanged(nameof(CanDeletePage));
        OnPropertyChanged(nameof(CurrentPageNumber));
        DeletePageCommand.NotifyCanExecuteChanged();
    }

    private void RenumberPages()
    {
        for (var i = 0; i < Document.Pages.Count; i++)
        {
            if (Document.Pages[i].Name.StartsWith("Page ", StringComparison.Ordinal))
            {
                Document.Pages[i].Name = $"Page {i + 1}";
            }
        }
    }

    // ---------------------------------------------------------------- affichage

    [RelayCommand]
    private void ZoomIn() => SetZoom(Zoom * 1.25);

    [RelayCommand]
    private void ZoomOut() => SetZoom(Zoom / 1.25);

    [RelayCommand]
    private void ZoomReset() => SetZoom(1.0);

    public void SetZoom(double value) => Zoom = Math.Clamp(value, 0.1, 8.0);

    private static IReadOnlyList<string> LoadSystemFonts()
    {
        try
        {
            return FontManager.Current.SystemFonts
                .Select(family => family.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            // Le concepteur visuel n'a pas toujours de gestionnaire de polices actif.
            return ["DejaVu Sans", "Liberation Serif", "Liberation Sans"];
        }
    }
}
