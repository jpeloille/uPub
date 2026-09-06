using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private sealed record Snapshot(string Json, int PageIndex, IReadOnlyList<int> SelectionIndices);

    private readonly List<Snapshot> _undo = [];
    private readonly List<Snapshot> _redo = [];

    private Snapshot? _editBaseline;
    private int _editDepth;

    /// <summary>Décalage en cascade des blocs insérés, pour éviter la superposition parfaite.</summary>
    private int _insertOffset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageCountLabel))]
    [NotifyPropertyChangedFor(nameof(FieldContext))]
    private Document _document = Document.CreateDefault();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageNumber))]
    [NotifyPropertyChangedFor(nameof(FieldContext))]
    private Page? _currentPage;

    /// <summary>
    /// Blocs sélectionnés. Invariant tenu par <see cref="SetSelection"/> : n'y figurent
    /// que des blocs posés directement sur <see cref="CurrentPage"/>, jamais l'enfant
    /// d'un groupe — celui-ci ne se retouche qu'en dissociant. Les index d'instantané
    /// et les commandes de disposition en dépendent.
    /// </summary>
    public ObservableCollection<Frame> Selection { get; } = [];

    /// <summary>
    /// Dernier bloc désigné : c'est lui que met en évidence la liste des blocs. Nul
    /// exactement quand la sélection est vide, et toujours membre de celle-ci.
    /// </summary>
    [ObservableProperty] private Frame? _selected;

    /// <summary>
    /// Empêche l'ancre de reboucler sur la sélection : <see cref="SetSelection"/> écrit
    /// les deux, tandis que la liaison de la liste des blocs n'écrit que l'ancre — et
    /// doit alors, elle, remplacer la sélection entière.
    /// </summary>
    private bool _syncingSelection;

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

    /// <summary>
    /// Affichage des repères — marges et grille. Réglage de vue et non du document :
    /// il sert à juger la page telle qu'elle sera imprimée, or les repères ne
    /// s'impriment jamais.
    /// </summary>
    [ObservableProperty] private bool _showGuides = true;

    /// <summary>Affichage des règles graduées bordant la planche.</summary>
    [ObservableProperty] private bool _showRulers = true;

    /// <summary>
    /// Repère de l'alignement. Publisher pose explicitement la question sous le nom
    /// « Relative to Margin Guides » ; MyPublisher la posait sans le dire, en centrant
    /// par rapport à la page alors que l'ajustement voisin travaillait sur les marges.
    /// Par défaut la zone de composition : c'est elle qu'on compose.
    /// </summary>
    [ObservableProperty] private bool _alignToMargins = true;

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

    /// <param name="contacts">
    /// Jeux de coordonnées, ou <c>null</c> pour relire ceux de l'utilisateur. On les
    /// passe pour qu'un banc n'aille pas lire — ni écrire — la configuration réelle de
    /// la machine : un export dépendrait alors d'un fichier hors du dépôt, qu'aucun
    /// « git status » ne montrerait.
    /// </param>
    /// <param name="horloge">
    /// D'où vient la date des champs, ou <c>null</c> pour l'heure courante. Même raison :
    /// un document portant un champ de date change tous les jours.
    /// </param>
    public MainWindowViewModel(BusinessInfoStore? contacts = null, Func<DateTime>? horloge = null)
    {
        Contacts = contacts ?? BusinessInfoStore.Load();
        _horloge = horloge ?? (() => DateTime.Now);
        CurrentPage = Document.Pages.FirstOrDefault();
        FontFamilies = LoadSystemFonts();
        // L'initialiseur de champ ne passe pas par OnDocumentChanged : le premier
        // document s'abonne donc ici, et lui seul.
        Document.PropertyChanged += OnDocumentPropertyChanged;
        SyncPageSetup();
    }

    private readonly Func<DateTime> _horloge;

    /// <summary>
    /// Le document se remplace en bloc — ouverture, modèle, page neuve, annulation —
    /// et l'abonnement doit suivre l'instance courante. Sans le désabonnement, un
    /// document restauré laisserait derrière lui un abonné vivant ; sans le
    /// réabonnement, une largeur saisie après une annulation cesserait de mettre le
    /// sélecteur de format à jour.
    /// </summary>
    partial void OnDocumentChanged(Document? oldValue, Document newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= OnDocumentPropertyChanged;
        newValue.PropertyChanged += OnDocumentPropertyChanged;
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Les dimensions se saisissent aussi en clair, format libre compris : ce sont
        // elles qui font foi, et le sélecteur de format qui les suit.
        if (e.PropertyName is nameof(Models.Document.PageWidth) or nameof(Models.Document.PageHeight))
        {
            SyncPageSetup();
        }
    }

    /// <summary>
    /// Aligne les contrôles de format sur les dimensions réelles du document.
    ///
    /// Une sélection nulle est un état légitime et non une absence de réponse : elle
    /// dit « format libre », comme la vignette « Create custom page size… » du
    /// catalogue de Publisher. Ce qu'il ne faut surtout pas en conclure, c'est qu'il
    /// faille effacer les dimensions — d'où la sortie anticipée de
    /// <see cref="OnSelectedPageSizeChanged"/>.
    /// </summary>
    private void SyncPageSetup()
    {
        if (_syncingPageSetup) return;

        _syncingPageSetup = true;
        try
        {
            IsLandscape = Document.PageWidth > Document.PageHeight;
            SelectedPageSize = PageSize.Match(Document.PageWidth, Document.PageHeight);
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
        ApplyPageSetup(() =>
        {
            Document.PageWidth = IsLandscape ? value.Height : value.Width;
            Document.PageHeight = IsLandscape ? value.Width : value.Height;
        });
        CommitEdit();
    }

    partial void OnIsLandscapeChanged(bool value)
    {
        if (_syncingPageSetup) return;
        if (Document.PageWidth > Document.PageHeight == value) return;

        BeginEdit();
        ApplyPageSetup(() =>
            (Document.PageWidth, Document.PageHeight) = (Document.PageHeight, Document.PageWidth));
        CommitEdit();
    }

    /// <summary>
    /// Change les dimensions de la page sans que le sélecteur de format ne s'en mêle.
    ///
    /// Les deux dimensions s'écrivent l'une après l'autre : entre les deux, le document
    /// porte un format bâtard que personne n'a demandé — largeur d'A4 et hauteur de
    /// Legal — et le ré-accord du sélecteur rejouerait alors le gestionnaire qui vient
    /// de l'appeler. Le drapeau couvre donc l'écriture entière et non chaque ligne.
    /// </summary>
    private void ApplyPageSetup(Action change)
    {
        _syncingPageSetup = true;
        try
        {
            change();
        }
        finally
        {
            _syncingPageSetup = false;
        }
    }

    /// <summary>Déplace la sélection au clavier ; le pas fin sert au calage précis.</summary>
    public void NudgeSelection(double dx, double dy, bool fine)
    {
        if (Selection.Count == 0) return;

        var step = fine ? 0.5 : 3.0;
        BeginEdit();
        foreach (var frame in Selection)
        {
            frame.X += dx * step;
            frame.Y += dy * step;
        }

        CommitEdit();
    }

    public IReadOnlyList<string> FontFamilies { get; }

    public IReadOnlyList<PageSize> PageSizes => PageSize.All;

    public IReadOnlyList<TextAlign> TextAlignments { get; } =
        [TextAlign.Left, TextAlign.Center, TextAlign.Right, TextAlign.Justify];

    public IReadOnlyList<VerticalAlign> VerticalAlignments { get; } =
        [VerticalAlign.Top, VerticalAlign.Middle, VerticalAlign.Bottom];

    public IReadOnlyList<ImageFit> ImageFits { get; } =
        [ImageFit.Uniform, ImageFit.UniformToFill, ImageFit.Fill];

    public IReadOnlyList<StrokeStyle> StrokeStyles { get; } =
        [StrokeStyle.Solid, StrokeStyle.Dashed, StrokeStyle.Dotted];

    private static IReadOnlyList<FieldOption> OptionsWhere(Func<FieldKind, bool> suits) =>
        Enum.GetValues<FieldKind>().Where(suits)
            .Select(kind => new FieldOption(kind, FieldResolver.Label(kind))).ToList();

    /// <summary>Ce qu'un bloc de texte peut afficher ; le logo est réservé aux images.</summary>
    public IReadOnlyList<FieldOption> TextFieldOptions { get; } = OptionsWhere(FieldResolver.SuitsText);

    public IReadOnlyList<FieldOption> ImageFieldOptions { get; } = OptionsWhere(FieldResolver.SuitsImage);

    /// <summary>
    /// Les jeux de coordonnées de l'utilisateur, relus au démarrage. Ils ne sont pas
    /// dans le document : un même document affiche les coordonnées de qui l'ouvre.
    /// </summary>
    public BusinessInfoStore Contacts { get; private set; }

    /// <summary>
    /// De quoi résoudre les champs de la page affichée : où l'on est dans le document,
    /// quand, et pour le compte de qui. Il se renouvelle à chaque changement de page,
    /// de nombre de pages ou de jeu de coordonnées, et les champs de l'écran suivent.
    /// </summary>
    public FieldContext FieldContext =>
        new(CurrentPageNumber, Document.Pages.Count, _horloge(), Contacts.Active);

    /// <summary>Adopte les jeux qu'un dialogue vient de valider, et rafraîchit les champs.</summary>
    public void ApplyContacts(BusinessInfoStore contacts)
    {
        Contacts = contacts;
        OnPropertyChanged(nameof(Contacts));
        OnPropertyChanged(nameof(FieldContext));
        Status = $"Coordonnées : {Contacts.Active.DisplayName}";
    }

    /// <summary>Facteur points -> pixels écran, zoom inclus. Le seul endroit qui mélange les deux unités.</summary>
    public double RenderScale => Zoom * Units.PointToDip;

    public bool HasSelection => Selection.Count > 0;
    public bool HasSingleSelection => Selection.Count == 1;
    public bool HasMultipleSelection => Selection.Count > 1;

    /// <summary>
    /// Le bloc, quand il n'y en a qu'un. Le panneau de propriétés n'édite que ce cas :
    /// afficher les champs d'un seul bloc alors que plusieurs sont pris ferait modifier
    /// en silence celui qu'on ne regarde pas.
    /// </summary>
    public Frame? SingleSelected => Selection.Count == 1 ? Selection[0] : null;

    /// <summary>
    /// Ce que porte la sélection, en toutes lettres. On compte les blocs plutôt que de
    /// les nommer : un libellé bâti sur le nom d'un bloc se démentirait dès qu'on le
    /// renomme, faute d'être prévenu.
    /// </summary>
    public string SelectionLabel => Selection.Count switch
    {
        0 => "Aucun bloc sélectionné.",
        1 => "1 bloc sélectionné.",
        _ => $"{Selection.Count} blocs sélectionnés."
    };

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public string ZoomLabel => $"{Zoom * 100:0} %";

    public string CursorLabel =>
        $"X {Units.PointsToMm(CursorX):0.0} mm   Y {Units.PointsToMm(CursorY):0.0} mm";

    /// <summary>
    /// Ce que la barre d'état annonce du bloc sélectionné : son coin, et sa taille.
    ///
    /// À un seul bloc, ce sont ses X, Y, largeur et hauteur — exactement les quatre
    /// nombres que le panneau donne à saisir, pour qu'un chiffre lu en bas et un
    /// chiffre lu à droite ne puissent pas se contredire. À plusieurs, c'est
    /// l'empreinte de l'ensemble, faute d'un cadre commun à nommer.
    ///
    /// La distinction se voit sur un bloc tourné : son empreinte déborde son cadre, et
    /// c'est bien son cadre qu'on annonce quand il est seul.
    /// </summary>
    private Extent? SelectionBox => Selection.Count switch
    {
        0 => null,
        1 => new Extent(
            Selection[0].X, Selection[0].Y,
            Selection[0].X + Selection[0].Width, Selection[0].Y + Selection[0].Height),
        _ => Extent.Union(Selection.Select(frame => frame.Extent)),
    };

    public string FramePositionLabel => SelectionBox is not { } box
        ? string.Empty
        : $"X {Units.PointsToMm(box.Left):0.0}   Y {Units.PointsToMm(box.Top):0.0} mm";

    public string FrameSizeLabel => SelectionBox is not { } box
        ? string.Empty
        : $"{Units.PointsToMm(box.Width):0.0} × {Units.PointsToMm(box.Height):0.0} mm";

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

    // ---------------------------------------------------------------- sélection

    /// <summary>Sélectionne ce seul bloc, ou vide la sélection si le bloc est nul.</summary>
    public void SelectOnly(Frame? frame) => SetSelection(frame is null ? [] : [frame]);

    /// <summary>Ctrl+clic : ajoute le bloc à la sélection, ou l'en retire s'il y était.</summary>
    public void ToggleSelection(Frame frame)
    {
        var frames = Selection.ToList();
        if (!frames.Remove(frame)) frames.Add(frame);
        SetSelection(frames);
    }

    /// <summary>
    /// Unique porte d'entrée de la sélection : elle filtre sur la page courante, tient
    /// l'ancre à jour et prévient l'interface. Les blocs sont conservés dans l'ordre
    /// reçu, le dernier devenant l'ancre — celui que la liste des blocs met en évidence.
    /// </summary>
    public void SetSelection(IReadOnlyList<Frame> frames)
    {
        _syncingSelection = true;
        try
        {
            Selection.Clear();
            foreach (var frame in frames)
            {
                if (CurrentPage?.Frames.Contains(frame) == true) Selection.Add(frame);
            }

            Selected = Selection.Count > 0 ? Selection[^1] : null;
        }
        finally
        {
            _syncingSelection = false;
        }

        WatchSelectionGeometry();
        NotifySelection();
    }

    /// <summary>
    /// Blocs dont la barre d'état suit la géométrie.
    ///
    /// La liste double <see cref="Selection"/> au lieu de s'y fier : il faut se
    /// désabonner de ce qu'on quitte, et la collection a déjà été vidée quand on
    /// l'apprend. C'est le même dédoublement que celui du plan de travail, et pour la
    /// même raison.
    /// </summary>
    private readonly List<Frame> _watchedFrames = [];

    private void WatchSelectionGeometry()
    {
        foreach (var frame in _watchedFrames) frame.PropertyChanged -= OnSelectedFrameChanged;
        _watchedFrames.Clear();

        foreach (var frame in Selection)
        {
            _watchedFrames.Add(frame);
            frame.PropertyChanged += OnSelectedFrameChanged;
        }
    }

    /// <summary>
    /// Pendant un redimensionnement ou un déplacement, le plan de travail écrit la
    /// géométrie du bloc à chaque mouvement du pointeur et ne prévient personne
    /// d'autre. Sans cet abonnement, la barre d'état ne se mettrait à jour qu'au
    /// relâchement — or c'est pendant le geste qu'on la regarde, l'œil étant sur la
    /// page et non sur le panneau de droite.
    /// </summary>
    private void OnSelectedFrameChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Frame.X) or nameof(Frame.Y)
            or nameof(Frame.Width) or nameof(Frame.Height) or nameof(Frame.Rotation))
        {
            NotifyFrameGeometry();
        }
    }

    private void NotifyFrameGeometry()
    {
        OnPropertyChanged(nameof(FramePositionLabel));
        OnPropertyChanged(nameof(FrameSizeLabel));
    }

    /// <summary>La sélection dans l'ordre de superposition de la page, du fond vers l'avant.</summary>
    public List<Frame> SelectionInPageOrder() =>
        CurrentPage is null ? [] : CurrentPage.Frames.Where(Selection.Contains).ToList();

    [RelayCommand]
    private void SelectAll()
    {
        if (CurrentPage is null) return;

        SetSelection(CurrentPage.Frames.ToList());
        Status = SelectionLabel;
    }

    partial void OnSelectedChanged(Frame? value)
    {
        // Seule la liste des blocs écrit l'ancre directement : elle désigne alors un
        // bloc unique, qui remplace la sélection.
        if (!_syncingSelection) SelectOnly(value);
    }

    partial void OnCurrentPageChanged(Page? value)
    {
        // La sélection ne contient que des blocs de la page courante : changer de page
        // la vide, sans quoi les commandes de disposition travailleraient sur des blocs
        // qui ne sont plus à l'écran.
        if (!_syncingSelection) SetSelection([]);
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasSingleSelection));
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(SingleSelected));
        OnPropertyChanged(nameof(SelectionLabel));
        NotifyFrameGeometry();

        DeleteSelectedCommand.NotifyCanExecuteChanged();
        DuplicateSelectedCommand.NotifyCanExecuteChanged();
        BringToFrontCommand.NotifyCanExecuteChanged();
        BringForwardCommand.NotifyCanExecuteChanged();
        SendBackwardCommand.NotifyCanExecuteChanged();
        SendToBackCommand.NotifyCanExecuteChanged();
        AlignLeftCommand.NotifyCanExecuteChanged();
        AlignCentreHorizontallyCommand.NotifyCanExecuteChanged();
        AlignRightCommand.NotifyCanExecuteChanged();
        AlignTopCommand.NotifyCanExecuteChanged();
        AlignCentreVerticallyCommand.NotifyCanExecuteChanged();
        AlignBottomCommand.NotifyCanExecuteChanged();
        DistributeHorizontallyCommand.NotifyCanExecuteChanged();
        DistributeVerticallyCommand.NotifyCanExecuteChanged();
        FitToMarginsCommand.NotifyCanExecuteChanged();

        GroupSelectedCommand.NotifyCanExecuteChanged();
        UngroupSelectedCommand.NotifyCanExecuteChanged();

        OnPropertyChanged(nameof(CanDistribute));
        OnPropertyChanged(nameof(CanGroup));
        OnPropertyChanged(nameof(CanUngroup));
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
        CurrentPage is null
            ? []
            : Selection.Select(frame => CurrentPage.Frames.IndexOf(frame)).Where(i => i >= 0).ToList());

    private void Restore(Snapshot snapshot)
    {
        Document = DocumentSerializer.FromJson(snapshot.Json);

        // La page d'abord : changer de page vide la sélection, c'est donc elle qui
        // doit être rétablie en dernier.
        CurrentPage = snapshot.PageIndex >= 0 && snapshot.PageIndex < Document.Pages.Count
            ? Document.Pages[snapshot.PageIndex]
            : Document.Pages.FirstOrDefault();

        SetSelection(CurrentPage is null
            ? []
            : snapshot.SelectionIndices
                .Where(i => i >= 0 && i < CurrentPage.Frames.Count)
                .Select(i => CurrentPage.Frames[i])
                .ToList());

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
        SetSelection([]);
        FilePath = null;
        IsDirty = false;
        _undo.Clear();
        _redo.Clear();
        SyncPageSetup();
        NotifyPageList();
        NotifyHistory();
        Status = "Nouveau document";
    }

    public void LoadFrom(string path) =>
        Adopt(DocumentSerializer.Load(path), path, $"Ouvert : {Path.GetFileName(path)}");

    /// <summary>
    /// Démarre un document neuf à partir d'un modèle. Tout se passe comme à l'ouverture,
    /// sauf le chemin : il reste vide, si bien que « Enregistrer » propose « Enregistrer
    /// sous » et que le modèle ne peut pas être écrasé par mégarde. C'est toute la
    /// différence entre ouvrir un modèle et partir de lui.
    /// </summary>
    public void StartFromTemplate(string path)
    {
        var document = DocumentSerializer.Load(path);
        Adopt(document, null, $"Nouveau document d'après « {document.Title} »");
    }

    private void Adopt(Document document, string? path, string status)
    {
        Document = document;
        if (Document.Pages.Count == 0) Document.Pages.Add(new Page { Name = "Page 1" });
        CurrentPage = Document.Pages[0];
        SetSelection([]);
        FilePath = path;
        IsDirty = false;
        _undo.Clear();
        _redo.Clear();
        SyncPageSetup();
        NotifyPageList();
        NotifyHistory();
        Status = status;
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
        // La même horloge que celle des champs de l'écran : sans cela, un export lancé
        // à minuit passé porterait une autre date que ce que la page affiche.
        var report = PdfExporter.Export(Document, path, Contacts.Active, _horloge());
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
        EditorTool.Table => BuildTable(),
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "Outil sans gabarit de bloc.")
    };

    /// <summary>
    /// Une bande de trois cases sur toute la largeur de composition : c'est la forme
    /// que prennent seize des dix-neuf tableaux des fiches ATR. Sa taille n'est pas
    /// posée ici — ce sont les colonnes et la ligne qui la font, et poser la largeur
    /// avant elles reviendrait à les mesurer au rapport entre les deux.
    /// </summary>
    private TableFrame BuildTable()
    {
        var table = new TableFrame { Name = "Tableau" };
        var largeur = Document.MarginBoxWidth / 3;

        for (var colonne = 0; colonne < 3; colonne++) table.Columns.Add(new TableColumn(largeur));
        table.Rows.Add(new TableRow(26));

        for (var colonne = 0; colonne < 3; colonne++)
        {
            table.Cells.Add(new TableCell
            {
                Row = 0, Column = colonne, Stroke = "#FF6B7280", StrokeThickness = 1
            });
        }

        return table;
    }

    [RelayCommand]
    private void AddText() => InsertWithTool(EditorTool.Text);

    [RelayCommand]
    private void AddHeading() => InsertWithTool(EditorTool.Heading);

    [RelayCommand]
    private void AddRectangle() => InsertWithTool(EditorTool.Rectangle);

    [RelayCommand]
    private void AddEllipse() => InsertWithTool(EditorTool.Ellipse);

    [RelayCommand]
    private void AddTable() => InsertWithTool(EditorTool.Table);

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
        SelectOnly(frame);
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
        if (CurrentPage is null || Selection.Count == 0) return;

        var doomed = SelectionInPageOrder();

        BeginEdit();
        foreach (var frame in doomed) CurrentPage.Frames.Remove(frame);
        SetSelection([]);
        CommitEdit();
        Status = doomed.Count == 1 ? "Bloc supprimé" : $"{doomed.Count} blocs supprimés";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DuplicateSelected()
    {
        if (CurrentPage is null || Selection.Count == 0) return;

        var copies = new List<Frame>();

        BeginEdit();
        // Dans l'ordre de superposition : les copies se retrouvent empilées comme
        // les originaux, et non dans l'ordre où les blocs ont été désignés.
        foreach (var frame in SelectionInPageOrder())
        {
            var copy = frame.Clone();
            copy.X += 12;
            copy.Y += 12;
            CurrentPage.Frames.Add(copy);
            copies.Add(copy);
        }

        SetSelection(copies);
        CommitEdit();
        Status = copies.Count == 1 ? "Bloc dupliqué" : $"{copies.Count} blocs dupliqués";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void BringToFront() => MoveToEnd(front: true);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SendToBack() => MoveToEnd(front: false);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void BringForward() => StepOrder(1);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SendBackward() => StepOrder(-1);

    /// <summary>
    /// Remanie la pile de la page, et repose ensuite la sélection que le remaniement
    /// a fait tomber.
    ///
    /// La liste « Blocs (du fond vers l'avant) » est liée à la fois à la collection de
    /// la page et à l'ancre de sélection. Remuer la collection fait passer la sélection
    /// de la liste par zéro, et cette remise à zéro redescend dans le modèle de vue par
    /// la liaison à double sens : le bloc qu'on vient de porter au premier plan cesse
    /// d'être pris. Le défaut est ancien — il valait déjà pour le menu et la barre
    /// latérale, et il ne tient pas au moyen employé : déplacer perd la sélection tout
    /// autant que retirer puis réinsérer. Il ne devient gênant qu'une fois le geste
    /// atteignable au clavier, où l'on enchaîne les frappes.
    ///
    /// On repose la sélection après coup plutôt que d'empêcher la liste de la lâcher :
    /// c'est elle qui en décide, et rien ne distingue sa remise à zéro d'un clic dans
    /// le vide. Reposée avant la fermeture de la transaction, pour que l'instantané
    /// d'annulation garde la sélection qu'on voit et non une sélection vide.
    /// </summary>
    private void Restack(Action change)
    {
        var selection = Selection.ToList();

        BeginEdit();
        change();
        SetSelection(selection);
        CommitEdit();
    }

    /// <summary>
    /// Porte la sélection tout devant ou tout derrière. Les blocs pris ensemble
    /// conservent leur ordre relatif : un ensemble monté au premier plan garde
    /// l'aspect qu'il avait.
    ///
    /// Par déplacements plutôt que par retraits suivis d'insertions, comme le fait déjà
    /// <see cref="StepOrder"/> : un retrait sort le bloc de la page, et pendant ce
    /// temps-là toute relecture de la sélection l'écarterait, celle-ci ne gardant que
    /// des blocs de la page courante. Vers l'avant on parcourt dans l'ordre, chaque
    /// bloc allant se poser derrière le précédent ; vers l'arrière on parcourt à
    /// l'envers, chacun passant devant celui qu'on vient de placer.
    /// </summary>
    private void MoveToEnd(bool front)
    {
        if (CurrentPage is null || Selection.Count == 0) return;

        var frames = CurrentPage.Frames;
        var moving = SelectionInPageOrder();
        if (!front) moving.Reverse();

        Restack(() =>
        {
            foreach (var frame in moving)
            {
                var from = frames.IndexOf(frame);
                if (from >= 0) frames.Move(from, front ? frames.Count - 1 : 0);
            }
        });
    }

    /// <summary>
    /// Déplace un bloc dans la pile de la page — c'est ce que fait le glisser-déposer
    /// dans la liste des blocs. <paramref name="target"/> est le rang où le bloc doit
    /// se trouver une fois posé.
    ///
    /// La transaction est ouverte ici, et non par le panneau de propriétés : celui-ci
    /// n'encadre que ce qui est dans son défilement, et la liste des blocs est de
    /// l'autre côté de la fenêtre. Sans cela, réordonner ne serait pas annulable.
    /// </summary>
    public bool ReorderFrame(Frame frame, int target)
    {
        if (CurrentPage is null) return false;

        var frames = CurrentPage.Frames;
        var from = frames.IndexOf(frame);
        if (from < 0) return false;

        target = Math.Clamp(target, 0, frames.Count - 1);
        if (target == from) return false;

        Restack(() => frames.Move(from, target));
        Status = target > from ? "Bloc avancé" : "Bloc reculé";
        return true;
    }

    /// <summary>
    /// Avance ou recule la sélection d'un rang. On parcourt depuis le côté vers lequel
    /// on va, pour qu'un bloc déjà déplacé n'arrête pas le suivant ; un bloc buté sur un
    /// autre bloc sélectionné ne bouge pas, l'ensemble garde donc sa forme.
    ///
    /// Le parcours interroge une copie de la sélection et non la sélection elle-même :
    /// remuer la collection de la page la vide en cours de route (voir
    /// <see cref="Restack"/>), et la boucle cesserait alors de reconnaître les blocs
    /// qu'il lui reste à déplacer.
    /// </summary>
    private void StepOrder(int direction)
    {
        if (CurrentPage is null || Selection.Count == 0) return;

        var frames = CurrentPage.Frames;
        var moving = Selection.ToList();

        Restack(() =>
        {
            if (direction > 0)
            {
                for (var i = frames.Count - 2; i >= 0; i--)
                {
                    if (moving.Contains(frames[i]) && !moving.Contains(frames[i + 1])) frames.Move(i, i + 1);
                }
            }
            else
            {
                for (var i = 1; i < frames.Count; i++)
                {
                    if (moving.Contains(frames[i]) && !moving.Contains(frames[i - 1])) frames.Move(i, i - 1);
                }
            }
        });
    }

    // ---------------------------------------------------------------- groupes

    public bool CanGroup => Selection.Count > 1;

    public bool CanUngroup => Selection.Any(frame => frame is GroupFrame);

    /// <summary>
    /// Réunit les blocs sélectionnés. Le groupe prend l'empreinte de l'ensemble, les
    /// blocs y entrent en coordonnées relatives et dans leur ordre de superposition ;
    /// le groupe se pose au rang du plus avancé d'entre eux, pour que l'ensemble garde
    /// l'aspect qu'il avait.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGroup))]
    private void GroupSelected()
    {
        if (CurrentPage is null || Selection.Count < 2) return;

        var members = SelectionInPageOrder();
        var bounds = Extent.Union(members.Select(frame => frame.Extent)) ?? default;

        // Le rang visé se lit avant les retraits ; les blocs retirés étant tous au
        // niveau du plus avancé ou en dessous, il redescend d'autant.
        var target = CurrentPage.Frames.IndexOf(members[^1]) - (members.Count - 1);

        var group = new GroupFrame
        {
            Name = "Groupe",
            X = bounds.Left,
            Y = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height
        };

        BeginEdit();
        foreach (var frame in members)
        {
            CurrentPage.Frames.Remove(frame);
            frame.X -= bounds.Left;
            frame.Y -= bounds.Top;
            group.Children.Add(frame);
        }

        CurrentPage.Frames.Insert(Math.Clamp(target, 0, CurrentPage.Frames.Count), group);
        SelectOnly(group);
        CommitEdit();
        Status = $"Groupe de {group.Children.Count} blocs";
    }

    /// <summary>
    /// Défait les groupes sélectionnés. Chaque bloc retrouve sa place sur la page : son
    /// centre est celui que le repère du groupe lui donnait, et la rotation du groupe
    /// s'ajoute à la sienne. Un groupe tourné se dissocie donc sans que rien ne bouge.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUngroup))]
    private void UngroupSelected()
    {
        if (CurrentPage is null || !CanUngroup) return;

        var released = new List<Frame>();

        BeginEdit();
        foreach (var frame in SelectionInPageOrder())
        {
            if (frame is not GroupFrame group)
            {
                released.Add(frame);
                continue;
            }

            var index = CurrentPage.Frames.IndexOf(group);
            CurrentPage.Frames.Remove(group);

            var children = group.Children.ToList();
            group.Children.Clear();

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var (centreX, centreY) = group.ToPage(child.CenterX, child.CenterY);

                child.Rotation += group.Rotation;
                child.X = centreX - child.Width / 2;
                child.Y = centreY - child.Height / 2;

                CurrentPage.Frames.Insert(index + i, child);
                released.Add(child);
            }
        }

        SetSelection(released);
        CommitEdit();
        Status = "Groupe dissocié";
    }

    // ---------------------------------------------------------------- alignement

    /// <summary>
    /// Sur quoi les blocs s'alignent. Trois cas, et le premier tranche : si l'on aligne
    /// par rapport aux marges, c'est la zone de composition. Sinon, plusieurs blocs
    /// s'alignent entre eux — sur ce que la sélection occupe — et un bloc seul, qui n'a
    /// rien à quoi se comparer, s'aligne sur la page.
    /// </summary>
    private Extent AlignmentReference()
    {
        if (AlignToMargins)
        {
            return new Extent(
                Document.MarginLeft, Document.MarginTop,
                Document.PageWidth - Document.MarginRight,
                Document.PageHeight - Document.MarginBottom);
        }

        if (Selection.Count > 1)
        {
            return Extent.Union(Selection.Select(frame => frame.Extent)) ?? default;
        }

        return new Extent(0, 0, Document.PageWidth, Document.PageHeight);
    }

    /// <summary>
    /// Déplace chaque bloc de l'écart que dicte son empreinte face au repère. On
    /// raisonne sur l'empreinte et non sur X et Y : un bloc tourné s'aligne sur ce
    /// qu'il occupe, pas sur son cadre non tourné.
    /// </summary>
    private void AlignEach(Func<Extent, Extent, (double Dx, double Dy)> shift, string label)
    {
        if (Selection.Count == 0) return;

        var reference = AlignmentReference();

        BeginEdit();
        foreach (var frame in Selection)
        {
            var (dx, dy) = shift(frame.Extent, reference);
            frame.X += dx;
            frame.Y += dy;
        }

        CommitEdit();
        Status = label;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AlignLeft() => AlignEach((f, r) => (r.Left - f.Left, 0), "Alignés à gauche");

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AlignCentreHorizontally() =>
        AlignEach((f, r) => (r.CenterX - f.CenterX, 0), "Centrés horizontalement");

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AlignRight() => AlignEach((f, r) => (r.Right - f.Right, 0), "Alignés à droite");

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AlignTop() => AlignEach((f, r) => (0, r.Top - f.Top), "Alignés en haut");

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AlignCentreVertically() =>
        AlignEach((f, r) => (0, r.CenterY - f.CenterY), "Centrés verticalement");

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AlignBottom() => AlignEach((f, r) => (0, r.Bottom - f.Bottom), "Alignés en bas");

    /// <summary>Répartir demande trois blocs : deux extrêmes et au moins un à placer entre eux.</summary>
    public bool CanDistribute => Selection.Count > 2;

    [RelayCommand(CanExecute = nameof(CanDistribute))]
    private void DistributeHorizontally() => Distribute(horizontal: true);

    [RelayCommand(CanExecute = nameof(CanDistribute))]
    private void DistributeVertically() => Distribute(horizontal: false);

    /// <summary>
    /// Égalise les intervalles entre blocs, les deux extrêmes restant où ils sont. Ce
    /// sont bien les blancs qu'on égalise, et non les centres : des blocs de largeurs
    /// différentes à centres réguliers laissent des blancs irréguliers, ce qui est ce
    /// que l'œil voit.
    /// </summary>
    private void Distribute(bool horizontal)
    {
        if (Selection.Count < 3) return;

        var ordered = Selection
            .Select(frame => (Frame: frame, Extent: frame.Extent))
            .OrderBy(item => horizontal ? item.Extent.Left : item.Extent.Top)
            .ToList();

        var first = ordered[0].Extent;
        var last = ordered[^1].Extent;
        var span = horizontal ? last.Right - first.Left : last.Bottom - first.Top;
        var occupied = ordered.Sum(item => horizontal ? item.Extent.Width : item.Extent.Height);
        var gap = (span - occupied) / (ordered.Count - 1);

        BeginEdit();
        var cursor = horizontal ? first.Left : first.Top;
        foreach (var (frame, extent) in ordered)
        {
            if (horizontal) frame.X += cursor - extent.Left;
            else frame.Y += cursor - extent.Top;

            cursor += (horizontal ? extent.Width : extent.Height) + gap;
        }

        CommitEdit();
        Status = horizontal ? "Répartis horizontalement" : "Répartis verticalement";
    }

    /// <summary>Un bloc à lui seul dans la zone de composition : sans objet à plusieurs.</summary>
    [RelayCommand(CanExecute = nameof(HasSingleSelection))]
    private void FitToMargins()
    {
        if (SingleSelected is not { } frame) return;
        BeginEdit();
        frame.X = Document.MarginLeft;
        frame.Y = Document.MarginTop;
        frame.Width = Document.MarginBoxWidth;
        frame.Height = Document.MarginBoxHeight;
        CommitEdit();
    }

    // ---------------------------------------------------------------- repères libres

    /// <summary>
    /// Pose un repère au milieu de la page. C'est la variante au menu du glisser depuis
    /// la règle : même résultat, sans souris, et le repère se déplace ensuite pareil.
    /// </summary>
    [RelayCommand]
    private void AddVerticalRulerGuide() => AddRulerGuide(vertical: true);

    [RelayCommand]
    private void AddHorizontalRulerGuide() => AddRulerGuide(vertical: false);

    private void AddRulerGuide(bool vertical)
    {
        BeginEdit();
        var guides = vertical ? Document.VerticalRulerGuides : Document.HorizontalRulerGuides;
        guides.Add(new RulerGuide((vertical ? Document.PageWidth : Document.PageHeight) / 2));
        ShowGuides = true;
        CommitEdit();
        Status = vertical ? "Repère vertical posé" : "Repère horizontal posé";
    }

    [RelayCommand]
    private void RemoveRulerGuide(RulerGuide? guide)
    {
        if (guide is null) return;

        BeginEdit();
        if (!Document.VerticalRulerGuides.Remove(guide)) Document.HorizontalRulerGuides.Remove(guide);
        CommitEdit();
        Status = "Repère retiré";
    }

    [RelayCommand]
    private void ClearRulerGuides()
    {
        var total = Document.VerticalRulerGuides.Count + Document.HorizontalRulerGuides.Count;
        if (total == 0)
        {
            Status = "Aucun repère à effacer";
            return;
        }

        BeginEdit();
        Document.VerticalRulerGuides.Clear();
        Document.HorizontalRulerGuides.Clear();
        CommitEdit();
        Status = total == 1 ? "Repère effacé" : $"{total} repères effacés";
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
        SetSelection([]);
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
        SetSelection([]);
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
        SetSelection([]);
        RenumberPages();
        CommitEdit();
        NotifyPageList();
    }

    private void NotifyPageList()
    {
        OnPropertyChanged(nameof(PageCountLabel));
        OnPropertyChanged(nameof(FieldContext));
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
