using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Plan de travail : sélection, déplacement, redimensionnement, rotation et saisie
/// de texte.
///
/// Toutes les interactions passent par une couche de saisie transparente placée
/// au-dessus de la page plutôt que par le test de survol des blocs eux-mêmes.
/// Cela évite que les contrôles internes (image, zone de texte) interceptent le
/// pointeur, et permet de tester les blocs dans l'ordre de superposition exact.
/// </summary>
public partial class PageCanvas : UserControl
{
    /// <summary>
    /// Poignées dans l'ordre horaire, à 45° d'intervalle : l'index sert aussi au choix
    /// du curseur. <see cref="Rotate"/> vient APRÈS les huit et non parmi elles :
    /// <see cref="UpdateCursor"/> fait tourner l'index avec le bloc, et cette
    /// arithmétique-là ne compte que sur les huit premières.
    /// </summary>
    private enum Anchor { NorthWest, North, NorthEast, East, SouthEast, South, SouthWest, West, Rotate }

    private enum DragMode { None, Move, Resize, Rotate, Create, Guide }

    private const double HandleSize = 9;      // pixels écran
    private const double HandleGrab = 7;      // rayon de préhension, pixels écran
    private const double GuideGrab = 4;       // un repère est un trait : on le saisit de près
    private const double SnapPixels = 6;      // tolérance d'aimantation, pixels écran

    /// <summary>
    /// Distance du bord haut à la poignée de rotation, en pixels écran. Hors de la
    /// zone de préhension des poignées d'angle (7 px de part et d'autre du bord) :
    /// deux poignées qui se recouvrent, c'est un geste qui part au hasard.
    /// </summary>
    private const double RotateHandleGap = 22;

    /// <summary>
    /// Pas d'aimantation angulaire sous Maj — celui de Publisher. Douze positions par
    /// tour couvrent tous les angles droits, les diagonales et les tiers de quart.
    /// </summary>
    private const double RotateSnapDegrees = 15;

    /// <summary>
    /// Taille minimale d'un bloc. Le panneau de propriétés saisit en millimètres :
    /// les deux plafonds doivent exprimer la même valeur, sinon un bloc réduit à la
    /// souris serait silencieusement redimensionné par le champ numérique. Elle est
    /// désormais déclarée une seule fois, dans le modèle.
    /// </summary>
    private static readonly double MinFrameSize = Snap.MinFrameSize;

    private static readonly Cursor[] EdgeCursors =
    [
        new(StandardCursorType.TopLeftCorner), new(StandardCursorType.TopSide),
        new(StandardCursorType.TopRightCorner), new(StandardCursorType.RightSide),
        new(StandardCursorType.BottomRightCorner), new(StandardCursorType.BottomSide),
        new(StandardCursorType.BottomLeftCorner), new(StandardCursorType.LeftSide)
    ];

    private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);

    /// <summary>
    /// Le jeu standard d'Avalonia n'a pas de curseur de rotation, et en dessiner un
    /// demanderait une image à embarquer pour un seul geste. La main dit « saisir »,
    /// ce qui est exactement ce qu'on fait de cette poignée, et ne se confond avec
    /// aucune des huit flèches de redimensionnement.
    /// </summary>
    private static readonly Cursor RotateCursor = new(StandardCursorType.Hand);

    private static readonly Cursor DrawCursor = new(StandardCursorType.Cross);
    private static readonly Cursor VerticalGuideCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor HorizontalGuideCursor = new(StandardCursorType.SizeNorthSouth);

    /// <summary>
    /// Un contour par bloc sélectionné, créés à la demande et gardés sous les poignées.
    /// </summary>
    private readonly List<Polygon> _outlines = [];

    /// <summary>
    /// Les neuf poignées : les huit du redimensionnement, puis celle de la rotation.
    /// Un seul tableau, indexé par <see cref="Anchor"/>, parce qu'un second tableau
    /// serait un second endroit à penser dans <see cref="RefreshOverlay"/> et dans
    /// <see cref="HitHandle"/> — et le jour où l'on n'en penserait qu'un, la poignée
    /// se dessinerait sans se laisser saisir.
    /// </summary>
    private readonly Shape[] _handles = new Shape[9];

    /// <summary>
    /// Tige reliant la poignée de rotation au bord haut du bloc. Sans elle, un disque
    /// flottant au-dessus du bloc ne se rattache visuellement à rien.
    /// </summary>
    private readonly Polyline _rotateStem;

    /// <summary>Cadre en pointillés affiché pendant le tracé d'un nouveau bloc.</summary>
    private readonly Rectangle _preview;

    private MainWindowViewModel? _vm;
    private readonly List<Frame> _tracked = [];
    private TextFrame? _editing;

    private DragMode _mode;
    private Anchor _anchor;
    private Point _startPointer;
    private double _startX, _startY, _startWidth, _startHeight, _startRotation;

    /// <summary>Position de départ de chaque bloc déplacé : le geste vaut pour l'ensemble.</summary>
    private readonly List<(Frame Frame, double X, double Y)> _moveStart = [];

    /// <summary>
    /// Empreinte de la sélection au début du déplacement. C'est elle qu'on aimante, et
    /// non chaque bloc : sinon un ensemble se disloquerait au premier repère rencontré.
    /// </summary>
    private Extent _moveExtent;

    /// <summary>Aucun bloc tourné dans la sélection : l'aimantation a des bords à offrir.</summary>
    private bool _moveSnappable;

    /// <summary>Repère en cours de pose ou de déplacement, et l'axe qu'il barre.</summary>
    private RulerGuide? _draggedGuide;
    private bool _draggedGuideIsVertical;

    public PageCanvas()
    {
        InitializeComponent();

        // La tige d'abord : posée avant les poignées, elle passe dessous, et le disque
        // reste saisissable sur toute sa surface.
        _rotateStem = new Polyline
        {
            Stroke = new SolidColorBrush(Color.Parse("#FF2D7DD2")),
            StrokeThickness = 1,
            IsVisible = false
        };
        Overlay.Children.Add(_rotateStem);

        for (var i = 0; i < _handles.Length; i++)
        {
            // La neuvième ne redimensionne pas : elle tourne. Un disque vert plutôt
            // qu'un carré blanc — un neuvième carré au-dessus du bloc se lirait comme
            // un bord à tirer, et c'est le vert que Publisher emploie pour ce geste.
            _handles[i] = (Anchor)i == Anchor.Rotate
                ? new Ellipse { Fill = new SolidColorBrush(Color.Parse("#FF2F9E5E")) }
                : new Rectangle { Fill = Brushes.White };

            _handles[i].Width = HandleSize;
            _handles[i].Height = HandleSize;
            _handles[i].Stroke = new SolidColorBrush(Color.Parse("#FF2D7DD2"));
            _handles[i].StrokeThickness = 1;
            _handles[i].IsVisible = false;

            Overlay.Children.Add(_handles[i]);
        }

        _preview = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.Parse("#FF2D7DD2")),
            StrokeThickness = 1,
            StrokeDashArray = new AvaloniaList<double> { 4, 3 },
            Fill = new SolidColorBrush(Color.Parse("#242D7DD2")),
            IsVisible = false
        };
        Overlay.Children.Add(_preview);

        // Les règles reçoivent les mêmes gestes que la planche : glisser depuis l'une
        // d'elles pose un repère, et la capture du pointeur fait que la suite du geste
        // leur revient même quand il passe au-dessus de la page.
        TopRuler.PointerPressed += OnRulerPressed;
        LeftRuler.PointerPressed += OnRulerPressed;
        TopRuler.PointerMoved += OnPointerMoved;
        LeftRuler.PointerMoved += OnPointerMoved;
        TopRuler.PointerReleased += OnPointerReleased;
        LeftRuler.PointerReleased += OnPointerReleased;
        TopRuler.Cursor = VerticalGuideCursor;
        LeftRuler.Cursor = HorizontalGuideCursor;

        InputLayer.PointerPressed += OnPointerPressed;
        InputLayer.PointerMoved += OnPointerMoved;
        InputLayer.PointerReleased += OnPointerReleased;
        InputLayer.DoubleTapped += OnDoubleTapped;
        Scroller.PointerWheelChanged += OnPointerWheelChanged;

        // En phase de tunnel : on quitte la saisie avant que le clic n'atteigne sa cible.
        AddHandler(PointerPressedEvent, OnTunnelPointerPressed, RoutingStrategies.Tunnel);

        Surface.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty) RefreshOverlay();
        };

        DataContextChanged += (_, _) => AttachViewModel();
        AttachViewModel();
    }

    /// <summary>Le raccourci Suppr ne doit pas effacer un bloc pendant la frappe.</summary>
    public bool IsEditingText => _editing is not null;

    private double Scale => _vm?.RenderScale ?? 1.0;

    // ---------------------------------------------------------------- liaison

    private void AttachViewModel()
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm.Selection.CollectionChanged -= OnSelectionChanged;
        }

        _vm = DataContext as MainWindowViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            // La collection n'est jamais remplacée : un seul abonnement suffit, et il
            // couvre aussi les changements qui ne déplacent pas l'ancre.
            _vm.Selection.CollectionChanged += OnSelectionChanged;
        }

        TrackFrames();
        RefreshOverlay();
    }

    private void OnSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        EndTextEdit();
        TrackFrames();
        RefreshOverlay();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.Selected):
                RefreshOverlay();
                break;
            case nameof(MainWindowViewModel.RenderScale):
            case nameof(MainWindowViewModel.CurrentPage):
            case nameof(MainWindowViewModel.Document):
                RefreshOverlay();
                break;
            case nameof(MainWindowViewModel.ActiveTool):
                // Changer d'outil pendant un tracé l'abandonne : sans cela, Échap
                // laisserait le cadre en pointillés affiché à l'écran.
                CancelCreate();

                // Un outil de tracé s'annonce par le curseur ; on quitte aussi la
                // saisie en cours, qui monopoliserait le pointeur.
                EndTextEdit();
                InputLayer.Cursor = _vm?.IsDrawing == true ? DrawCursor : ArrowCursor;
                break;
        }
    }

    /// <summary>
    /// Suit la géométrie de tous les blocs sélectionnés : pendant un déplacement
    /// d'ensemble, chacun d'eux bouge, et chaque contour doit suivre le sien.
    /// </summary>
    private void TrackFrames()
    {
        foreach (var frame in _tracked) frame.PropertyChanged -= OnFramePropertyChanged;
        _tracked.Clear();

        if (_vm is null) return;

        foreach (var frame in _vm.Selection)
        {
            _tracked.Add(frame);
            frame.PropertyChanged += OnFramePropertyChanged;
        }
    }

    private void OnFramePropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshOverlay();

    // ---------------------------------------------------------------- poignées

    private void RefreshOverlay()
    {
        var page = _vm?.CurrentPage;
        var frames = _vm is null || page is null
            ? []
            : _vm.Selection.Where(page.Frames.Contains).ToList();

        for (var i = 0; i < frames.Count; i++)
        {
            var outline = OutlineAt(i);
            outline.Points = Corners(frames[i]);
            outline.IsVisible = true;
        }

        for (var i = frames.Count; i < _outlines.Count; i++) _outlines[i].IsVisible = false;

        // Les poignées ne servent qu'à un bloc seul, celle de rotation comprise :
        // redimensionner — ou tourner — un ensemble demanderait de reprendre chacun de
        // ses blocs autour d'un pivot commun, ce que le groupe fait proprement. On
        // sélectionne, on groupe, puis on redimensionne ou l'on tourne.
        var single = frames.Count == 1 ? frames[0] : null;
        foreach (var handle in _handles) handle.IsVisible = single is not null;
        _rotateStem.IsVisible = single is not null;
        if (single is null) return;

        for (var i = 0; i < _handles.Length; i++)
        {
            var (lx, ly) = LocalAnchor(single, (Anchor)i);
            var point = SurfacePoint(single, lx, ly);
            Canvas.SetLeft(_handles[i], point.X - HandleSize / 2);
            Canvas.SetTop(_handles[i], point.Y - HandleSize / 2);
        }

        var (nx, ny) = LocalAnchor(single, Anchor.North);
        var (rx, ry) = LocalAnchor(single, Anchor.Rotate);
        _rotateStem.Points = [SurfacePoint(single, nx, ny), SurfacePoint(single, rx, ry)];
    }

    private List<Point> Corners(Frame frame) =>
    [
        SurfacePoint(frame, 0, 0),
        SurfacePoint(frame, frame.Width, 0),
        SurfacePoint(frame, frame.Width, frame.Height),
        SurfacePoint(frame, 0, frame.Height)
    ];

    /// <summary>
    /// Contour de rang donné, créé au besoin. Les contours s'insèrent en tête de la
    /// couche pour rester sous les poignées, qui doivent rester saisissables.
    /// </summary>
    private Polygon OutlineAt(int index)
    {
        while (_outlines.Count <= index)
        {
            var outline = new Polygon
            {
                Stroke = new SolidColorBrush(Color.Parse("#FF2D7DD2")),
                StrokeThickness = 1,
                Fill = null,
                IsVisible = false
            };

            _outlines.Add(outline);
            Overlay.Children.Insert(0, outline);
        }

        return _outlines[index];
    }

    /// <summary>Convertit un point du repère local d'un bloc vers les pixels écran de la surface.</summary>
    private Point SurfacePoint(Frame frame, double localX, double localY)
    {
        var (px, py) = frame.ToPage(localX, localY);
        return new Point(px * Scale, py * Scale);
    }

    /// <summary>
    /// Ancre d'une poignée dans le repère local du bloc. Celle de la rotation se pose
    /// au-dessus du bord haut, à distance constante à l'ÉCRAN et non sur la page : elle
    /// reste ainsi à la même portée de main quel que soit le zoom, comme les huit autres
    /// gardent leur taille. C'est ce qui oblige la méthode à connaître l'échelle, donc à
    /// ne plus être statique.
    /// </summary>
    private (double X, double Y) LocalAnchor(Frame frame, Anchor anchor) => anchor switch
    {
        Anchor.NorthWest => (0, 0),
        Anchor.North => (frame.Width / 2, 0),
        Anchor.NorthEast => (frame.Width, 0),
        Anchor.East => (frame.Width, frame.Height / 2),
        Anchor.SouthEast => (frame.Width, frame.Height),
        Anchor.South => (frame.Width / 2, frame.Height),
        Anchor.SouthWest => (0, frame.Height),
        Anchor.Rotate => (frame.Width / 2, -RotateHandleGap / Scale),
        _ => (0, frame.Height / 2)
    };

    private Anchor? HitHandle(Frame frame, Point surfacePoint)
    {
        for (var i = 0; i < _handles.Length; i++)
        {
            var (lx, ly) = LocalAnchor(frame, (Anchor)i);
            var centre = SurfacePoint(frame, lx, ly);
            if (Math.Abs(centre.X - surfacePoint.X) <= HandleGrab &&
                Math.Abs(centre.Y - surfacePoint.Y) <= HandleGrab)
            {
                return (Anchor)i;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------- pointeur

    private Point ToPageCoordinates(Point surfacePoint) =>
        new(surfacePoint.X / Scale, surfacePoint.Y / Scale);

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm?.CurrentPage is null) return;
        if (!e.GetCurrentPoint(Surface).Properties.IsLeftButtonPressed) return;

        Focus();

        var surfacePoint = e.GetPosition(Surface);
        var page = ToPageCoordinates(surfacePoint);

        // Un outil armé prend la main : on crée au lieu de sélectionner.
        if (_vm.IsDrawing)
        {
            _mode = DragMode.Create;
            _startPointer = page;
            UpdatePreview(page, page);
            e.Pointer.Capture(InputLayer);
            e.Handled = true;
            return;
        }

        if (_vm.SingleSelected is { } single && HitHandle(single, surfacePoint) is { } anchor)
        {
            // Même prise, mêmes repères de départ, même capture : la rotation n'est
            // qu'un second mode de ce geste-là. BeginEdit ici, CommitEdit au relâché,
            // comme le redimensionnement — c'est ce qui la rend annulable d'un pas.
            _mode = anchor == Anchor.Rotate ? DragMode.Rotate : DragMode.Resize;
            _anchor = anchor;
            RememberResize(single, page);
            _vm.BeginEdit();
            e.Pointer.Capture(InputLayer);
            e.Handled = true;
            return;
        }

        // Un repère passe avant les blocs : il est fin, on ne l'attrape qu'en le visant,
        // et c'est le seul moyen de le reprendre une fois posé.
        if (HitGuide(page) is var (guide, vertical) && guide is not null)
        {
            BeginGuideDrag(guide, vertical);
            e.Pointer.Capture(InputLayer);
            e.Handled = true;
            return;
        }

        // Ordre de superposition : le dernier bloc dessiné est le premier candidat.
        var hit = _vm.CurrentPage.Frames.LastOrDefault(f => f.HitTest(page.X, page.Y));
        var additive = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                       e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (hit is null)
        {
            // Cliquer à côté libère la sélection — sauf si l'on est en train de
            // l'étendre, geste où un clic manqué ne doit pas tout défaire.
            if (!additive) _vm.SetSelection([]);
            e.Handled = true;
            return;
        }

        if (additive) _vm.ToggleSelection(hit);
        else if (!_vm.Selection.Contains(hit)) _vm.SelectOnly(hit);

        // Un bloc déjà pris garde la sélection entière : c'est ce qui permet de saisir
        // un ensemble par n'importe lequel de ses blocs.
        if (_vm.Selection.Contains(hit))
        {
            _mode = DragMode.Move;
            RememberMove(page);
            _vm.BeginEdit();
            e.Pointer.Capture(InputLayer);
        }

        e.Handled = true;
    }

    private void RememberResize(Frame frame, Point page)
    {
        _startPointer = page;
        _startX = frame.X;
        _startY = frame.Y;
        _startWidth = frame.Width;
        _startHeight = frame.Height;
        _startRotation = frame.Rotation;
    }

    /// <summary>Retient d'où part chaque bloc de la sélection, et ce que l'ensemble occupe.</summary>
    private void RememberMove(Point page)
    {
        if (_vm is null) return;

        _startPointer = page;
        _moveStart.Clear();
        foreach (var frame in _vm.Selection) _moveStart.Add((frame, frame.X, frame.Y));

        _moveSnappable = _vm.Selection.All(frame => frame.Rotation == 0);
        _moveExtent = Extent.Union(_vm.Selection.Select(frame => frame.Extent)) ?? default;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_vm is null) return;

        var surfacePoint = e.GetPosition(Surface);
        var page = ToPageCoordinates(surfacePoint);

        _vm.CursorX = page.X;
        _vm.CursorY = page.Y;

        if (_mode == DragMode.None)
        {
            UpdateCursor(surfacePoint, page);
            return;
        }

        if (_mode == DragMode.Create)
        {
            UpdatePreview(_startPointer, page);
            return;
        }

        if (_mode == DragMode.Guide)
        {
            ApplyGuideMove(page, e.KeyModifiers);
            return;
        }

        if (_mode == DragMode.Move)
        {
            ApplyMove(page, e.KeyModifiers);
            return;
        }

        if (_vm.SingleSelected is not { } frame) return;

        // Maj sert aux deux gestes, et pas à la même chose : proportions conservées au
        // redimensionnement, pas de 15° à la rotation. C'est ce que fait Publisher.
        var maj = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (_mode == DragMode.Rotate) ApplyRotate(frame, page, maj);
        else ApplyResize(frame, page, maj);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Toujours relâcher la capture, y compris si le geste a été abandonné
        // entre-temps : sinon le pointeur resterait accroché au plan de travail.
        e.Pointer.Capture(null);

        if (_mode == DragMode.None) return;

        var mode = _mode;
        _mode = DragMode.None;

        if (mode == DragMode.Create)
        {
            FinishCreate(e.GetPosition(Surface));
            return;
        }

        if (mode == DragMode.Guide)
        {
            FinishGuideDrag();
            return;
        }

        _vm?.CommitEdit();
    }

    /// <summary>Abandonne un tracé en cours et efface son aperçu.</summary>
    private void CancelCreate()
    {
        if (_mode != DragMode.Create) return;

        _mode = DragMode.None;
        _preview.IsVisible = false;
    }

    /// <summary>Met à jour le cadre d'aperçu, exprimé en pixels écran.</summary>
    private void UpdatePreview(Point origin, Point current)
    {
        var scale = Scale;
        var left = Math.Min(origin.X, current.X) * scale;
        var top = Math.Min(origin.Y, current.Y) * scale;

        Canvas.SetLeft(_preview, left);
        Canvas.SetTop(_preview, top);
        _preview.Width = Math.Abs(current.X - origin.X) * scale;
        _preview.Height = Math.Abs(current.Y - origin.Y) * scale;
        _preview.IsVisible = true;
    }

    private void FinishCreate(Point surfacePoint)
    {
        _preview.IsVisible = false;

        if (_vm is null) return;

        var end = ToPageCoordinates(surfacePoint);
        var frame = _vm.DrawWithActiveTool(
            Math.Min(_startPointer.X, end.X),
            Math.Min(_startPointer.Y, end.Y),
            Math.Abs(end.X - _startPointer.X),
            Math.Abs(end.Y - _startPointer.Y));

        InputLayer.Cursor = ArrowCursor;

        // Un bloc de texte fraîchement tracé attend qu'on le remplisse.
        if (frame is TextFrame text) BeginTextEdit(text);
    }

    private void UpdateCursor(Point surfacePoint, Point page)
    {
        if (_vm?.IsDrawing == true)
        {
            InputLayer.Cursor = DrawCursor;
            return;
        }

        if (_vm?.SingleSelected is { } selected && HitHandle(selected, surfacePoint) is { } anchor)
        {
            // La neuvième poignée d'abord : elle n'est pas un bord, et l'arithmétique
            // ci-dessous — qui compte huit poignées — lui donnerait un curseur de
            // redimensionnement parfaitement valide, donc parfaitement trompeur.
            if (anchor == Anchor.Rotate)
            {
                InputLayer.Cursor = RotateCursor;
                return;
            }

            // La rotation fait tourner le sens du redimensionnement : on décale
            // l'index de poignée d'un cran par quart de tour de 45°.
            var index = (int)anchor + (int)Math.Round(selected.Rotation / 45.0);
            InputLayer.Cursor = EdgeCursors[((index % 8) + 8) % 8];
            return;
        }

        if (HitGuide(page) is var (guide, vertical) && guide is not null)
        {
            InputLayer.Cursor = vertical ? VerticalGuideCursor : HorizontalGuideCursor;
            return;
        }

        var over = _vm?.CurrentPage?.Frames.LastOrDefault(f => f.HitTest(page.X, page.Y));
        InputLayer.Cursor = over is null ? ArrowCursor : MoveCursor;
    }

    // ---------------------------------------------------------------- repères libres

    /// <summary>
    /// Le repère le plus proche du point, s'il est à portée. Les repères cachés ne se
    /// saisissent pas : on ne doit pas attraper ce qu'on ne voit pas.
    /// </summary>
    private (RulerGuide? Guide, bool Vertical) HitGuide(Point page)
    {
        if (_vm is null || !_vm.ShowGuides) return (null, false);

        var tolerance = GuideGrab / Scale;

        foreach (var guide in _vm.Document.VerticalRulerGuides)
        {
            if (Math.Abs(guide.Position - page.X) <= tolerance) return (guide, true);
        }

        foreach (var guide in _vm.Document.HorizontalRulerGuides)
        {
            if (Math.Abs(guide.Position - page.Y) <= tolerance) return (guide, false);
        }

        return (null, false);
    }

    /// <summary>Un geste parti d'une règle pose un repère neuf, qu'on traîne aussitôt.</summary>
    private void OnRulerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null || sender is not Ruler ruler) return;
        if (!e.GetCurrentPoint(ruler).Properties.IsLeftButtonPressed) return;

        Focus();

        // La poignée de rotation est la seule qui se pose DEHORS, au-dessus du bloc :
        // un bloc collé au haut de la page la met donc sur la règle, où elle se voit
        // sans se laisser prendre. Le geste y poserait un repère au lieu de tourner —
        // il ne ferait pas rien, il ferait autre chose. On la sert donc d'abord ; la
        // capture revient à la règle, qui renvoie déjà la suite du geste aux mêmes
        // gestionnaires.
        var surfacePoint = e.GetPosition(Surface);
        if (_vm.SingleSelected is { } single && HitHandle(single, surfacePoint) == Anchor.Rotate)
        {
            _mode = DragMode.Rotate;
            _anchor = Anchor.Rotate;
            RememberResize(single, ToPageCoordinates(surfacePoint));
            _vm.BeginEdit();
            e.Pointer.Capture(ruler);
            e.Handled = true;
            return;
        }

        var vertical = ruler.Orientation == Avalonia.Layout.Orientation.Horizontal;
        var page = ToPageCoordinates(surfacePoint);
        var guide = new RulerGuide(vertical ? page.X : page.Y);

        BeginGuideDrag(guide, vertical);
        (vertical ? _vm.Document.VerticalRulerGuides : _vm.Document.HorizontalRulerGuides).Add(guide);

        // Poser un repère n'a d'intérêt que si on les voit ; le geste les rallume.
        _vm.ShowGuides = true;

        e.Pointer.Capture(ruler);
        e.Handled = true;
    }

    private void BeginGuideDrag(RulerGuide guide, bool vertical)
    {
        _mode = DragMode.Guide;
        _draggedGuide = guide;
        _draggedGuideIsVertical = vertical;
        _vm?.BeginEdit();
    }

    /// <summary>
    /// Suit le pointeur, en s'aimantant sur les autres repères — marges, grille,
    /// colonnes — pour qu'on puisse poser un repère exactement sur un bord de colonne.
    /// Le repère traîné est écarté des candidats, sans quoi il s'accrocherait à
    /// lui-même et ne bougerait plus.
    /// </summary>
    private void ApplyGuideMove(Point page, KeyModifiers modifiers)
    {
        if (_draggedGuide is null || _vm is null) return;

        var target = _draggedGuideIsVertical ? page.X : page.Y;

        if (!modifiers.HasFlag(KeyModifiers.Alt))
        {
            var current = _draggedGuide.Position;
            var candidates = (_draggedGuideIsVertical
                    ? _vm.Document.VerticalGuides
                    : _vm.Document.HorizontalGuides)
                .Where(value => Math.Abs(value - current) > 1e-9)
                .ToList();

            target = SnapEdge(target, 0, SnapPixels / Scale, candidates);
        }

        _draggedGuide.Position = target;
    }

    /// <summary>
    /// Ferme le geste. Un repère lâché hors de la page est retiré : c'est le moyen de
    /// s'en débarrasser, en le ramenant d'où il vient.
    /// </summary>
    private void FinishGuideDrag()
    {
        if (_draggedGuide is not null && _vm is not null)
        {
            var limit = _draggedGuideIsVertical ? _vm.Document.PageWidth : _vm.Document.PageHeight;
            if (_draggedGuide.Position < 0 || _draggedGuide.Position > limit)
            {
                (_draggedGuideIsVertical
                    ? _vm.Document.VerticalRulerGuides
                    : _vm.Document.HorizontalRulerGuides).Remove(_draggedGuide);
                _vm.Status = "Repère retiré";
            }
        }

        _draggedGuide = null;
        _vm?.CommitEdit();
    }

    // ---------------------------------------------------------------- déplacement

    private void ApplyMove(Point page, KeyModifiers modifiers)
    {
        var dx = page.X - _startPointer.X;
        var dy = page.Y - _startPointer.Y;

        // Alt désactive l'aimantation ; un bloc tourné la désactive aussi, faute de
        // bords parallèles aux repères. On aimante l'empreinte de l'ensemble, puis on
        // reporte le même écart sur chaque bloc : la sélection garde sa forme.
        if (!modifiers.HasFlag(KeyModifiers.Alt) && _moveSnappable && _vm is not null)
        {
            var document = _vm.Document;
            var tolerance = SnapPixels / Scale;

            // Les repères sont ceux du document : bords, marges, milieu, et la grille de
            // composition quand il y en a une. Le tableau s'allonge, le geste ne change pas.
            dx = SnapEdge(_moveExtent.Left + dx, _moveExtent.Width, tolerance,
                document.VerticalGuides) - _moveExtent.Left;

            dy = SnapEdge(_moveExtent.Top + dy, _moveExtent.Height, tolerance,
                document.HorizontalGuides) - _moveExtent.Top;
        }

        foreach (var (frame, startX, startY) in _moveStart)
        {
            frame.X = startX + dx;
            frame.Y = startY + dy;
        }
    }

    /// <summary>
    /// Aimante l'un des trois bords du bloc sur le repère le plus proche. Le calcul
    /// est dans <see cref="Snap.Edge"/> : il est pur, et c'est lui l'oracle des
    /// positions exactes — il doit pouvoir être interrogé sans ouvrir de fenêtre.
    /// </summary>
    private static double SnapEdge(double position, double size, double tolerance, IReadOnlyList<double> guides)
        => Snap.Edge(position, size, tolerance, guides);

    // ---------------------------------------------------------------- redimensionnement

    private void ApplyResize(Frame frame, Point page, bool keepAspect)
    {
        var radians = _startRotation * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        // Position du pointeur dans le repère local du bloc tel qu'il était au
        // début du geste : on raisonne ensuite sur un rectangle non tourné.
        var dx = page.X - (_startX + _startWidth / 2);
        var dy = page.Y - (_startY + _startHeight / 2);
        var localX = dx * cos + dy * sin + _startWidth / 2;
        var localY = -dx * sin + dy * cos + _startHeight / 2;

        var movesLeft = _anchor is Anchor.NorthWest or Anchor.West or Anchor.SouthWest;
        var movesRight = _anchor is Anchor.NorthEast or Anchor.East or Anchor.SouthEast;
        var movesTop = _anchor is Anchor.NorthWest or Anchor.North or Anchor.NorthEast;
        var movesBottom = _anchor is Anchor.SouthWest or Anchor.South or Anchor.SouthEast;

        double left = 0, top = 0, right = _startWidth, bottom = _startHeight;
        if (movesLeft) left = Math.Min(localX, right - MinFrameSize);
        if (movesRight) right = Math.Max(localX, left + MinFrameSize);
        if (movesTop) top = Math.Min(localY, bottom - MinFrameSize);
        if (movesBottom) bottom = Math.Max(localY, top + MinFrameSize);

        var width = right - left;
        var height = bottom - top;

        // Maj sur une poignée d'angle : conservation des proportions d'origine.
        if (keepAspect && movesLeft != movesRight && movesTop != movesBottom &&
            _startWidth > 0 && _startHeight > 0)
        {
            var ratio = _startWidth / _startHeight;
            if (width / height > ratio) width = height * ratio;
            else height = width / ratio;

            if (movesLeft) left = right - width; else right = left + width;
            if (movesTop) top = bottom - height; else bottom = top + height;
        }

        // Le coin local (left, top) doit rester au même endroit sur la page ;
        // on en déduit la position du nouveau bloc, rotation comprise.
        var offsetX = left - _startWidth / 2;
        var offsetY = top - _startHeight / 2;
        var originX = _startX + _startWidth / 2 + (offsetX * cos - offsetY * sin);
        var originY = _startY + _startHeight / 2 + (offsetX * sin + offsetY * cos);

        var cornerX = -width / 2 * cos + height / 2 * sin;
        var cornerY = -width / 2 * sin - height / 2 * cos;

        frame.Width = width;
        frame.Height = height;
        frame.X = originX - width / 2 - cornerX;
        frame.Y = originY - height / 2 - cornerY;
    }

    // ---------------------------------------------------------------- rotation

    /// <summary>
    /// Jumeau de <see cref="ApplyResize"/> : mêmes repères de départ, même capture,
    /// même transaction d'annulation. Le bloc tourne autour de son centre — le pivot
    /// qu'emploient <see cref="Frame.ToPage"/> et l'export —, si bien que le geste
    /// n'écrit que <see cref="Frame.Rotation"/> : ni la position ni la taille ne bougent.
    ///
    /// Il suit l'ÉCART d'angle depuis la prise, et non l'angle absolu du pointeur. La
    /// poignée est au-dessus du bloc, à un quart de tour de son axe : viser l'angle
    /// absolu ferait sauter le bloc de 90° au premier pixel de glissement.
    /// </summary>
    private void ApplyRotate(Frame frame, Point page, bool snap)
    {
        var centreX = _startX + _startWidth / 2;
        var centreY = _startY + _startHeight / 2;

        // Sur le pivot, l'angle n'existe pas et bascule d'un demi-tour au moindre
        // tremblement : on garde alors la rotation acquise plutôt que d'en inventer une.
        var dx = page.X - centreX;
        var dy = page.Y - centreY;
        if (dx * dx + dy * dy < 1) return;

        // L'axe Y descend, comme en PDF : un angle qui croît tourne dans le sens des
        // aiguilles, ce qui est le sens de Rotation. Aucune négation à faire.
        var prise = Math.Atan2(_startPointer.Y - centreY, _startPointer.X - centreX);
        var angle = _startRotation + (Math.Atan2(dy, dx) - prise) * 180.0 / Math.PI;

        // L'aimantation porte sur l'angle ABSOLU et non sur l'écart parcouru : c'est ce
        // qui permet de remettre d'aplomb un bloc qui ne l'est pas — un pas relatif lui
        // conserverait son travers.
        if (snap) angle = Math.Round(angle / RotateSnapDegrees) * RotateSnapDegrees;

        // Ramené dans [0, 360[ : le champ du panneau s'arrête à ±360, et deux tours de
        // poignée dans le même sens le dépasseraient — la valeur serait alors ramenée en
        // silence à la sélection suivante. Publisher affiche 350°, pas −10°.
        frame.Rotation = ((angle % 360) + 360) % 360;
    }

    // ---------------------------------------------------------------- saisie de texte

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm?.SingleSelected is TextFrame text) BeginTextEdit(text);
    }

    private void BeginTextEdit(TextFrame frame)
    {
        if (_editing is not null) return;

        // Un bloc à champ affiche une valeur calculée : la saisir sur la page
        // reviendrait à modifier un texte qu'on ne voit pas.
        if (frame.HasField)
        {
            if (_vm is not null) _vm.Status = "Ce bloc affiche un champ ; son contenu se règle dans le panneau.";
            return;
        }

        _vm?.BeginEdit();
        _editing = frame;
        frame.IsEditing = true;
        InputLayer.IsHitTestVisible = false;
        RefreshOverlay();

        // Le TextBox n'existe qu'après le passage de mise en page suivant.
        Dispatcher.UIThread.Post(() =>
        {
            var box = Scaler.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(t => ReferenceEquals(t.DataContext, frame));
            box?.Focus();
            box?.SelectAll();
        }, DispatcherPriority.Background);
    }

    public void EndTextEdit()
    {
        if (_editing is null) return;

        _editing.IsEditing = false;
        _editing = null;
        InputLayer.IsHitTestVisible = true;
        _vm?.CommitEdit();
        RefreshOverlay();
    }

    private void OnTunnelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_editing is null) return;

        // Un clic dans la zone de saisie elle-même ne doit pas en sortir.
        if (e.Source is Visual visual &&
            visual.GetSelfAndVisualAncestors()
                .OfType<TextBox>()
                .Any(t => ReferenceEquals(t.DataContext, _editing)))
        {
            return;
        }

        EndTextEdit();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _editing is not null)
        {
            EndTextEdit();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || _vm is null) return;

        _vm.SetZoom(_vm.Zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1));
        e.Handled = true;
    }
}
