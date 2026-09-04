using System;
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
/// Plan de travail : sélection, déplacement, redimensionnement et saisie de texte.
///
/// Toutes les interactions passent par une couche de saisie transparente placée
/// au-dessus de la page plutôt que par le test de survol des blocs eux-mêmes.
/// Cela évite que les contrôles internes (image, zone de texte) interceptent le
/// pointeur, et permet de tester les blocs dans l'ordre de superposition exact.
/// </summary>
public partial class PageCanvas : UserControl
{
    /// <summary>Poignées dans l'ordre horaire, à 45° d'intervalle : l'index sert aussi au choix du curseur.</summary>
    private enum Anchor { NorthWest, North, NorthEast, East, SouthEast, South, SouthWest, West }

    private enum DragMode { None, Move, Resize, Create }

    private const double HandleSize = 9;      // pixels écran
    private const double HandleGrab = 7;      // rayon de préhension, pixels écran
    private const double SnapPixels = 6;      // tolérance d'aimantation, pixels écran
    /// <summary>
    /// Taille minimale d'un bloc. Le panneau de propriétés saisit en millimètres :
    /// les deux plafonds doivent exprimer la même valeur, sinon un bloc réduit à la
    /// souris serait silencieusement redimensionné par le champ numérique.
    /// </summary>
    private static readonly double MinFrameSize = Units.MmToPoints(2);

    private static readonly Cursor[] EdgeCursors =
    [
        new(StandardCursorType.TopLeftCorner), new(StandardCursorType.TopSide),
        new(StandardCursorType.TopRightCorner), new(StandardCursorType.RightSide),
        new(StandardCursorType.BottomRightCorner), new(StandardCursorType.BottomSide),
        new(StandardCursorType.BottomLeftCorner), new(StandardCursorType.LeftSide)
    ];

    private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);

    private static readonly Cursor DrawCursor = new(StandardCursorType.Cross);

    private readonly Polygon _outline;
    private readonly Rectangle[] _handles = new Rectangle[8];

    /// <summary>Cadre en pointillés affiché pendant le tracé d'un nouveau bloc.</summary>
    private readonly Rectangle _preview;

    private MainWindowViewModel? _vm;
    private Frame? _tracked;
    private TextFrame? _editing;

    private DragMode _mode;
    private Anchor _anchor;
    private Point _startPointer;
    private double _startX, _startY, _startWidth, _startHeight, _startRotation;

    public PageCanvas()
    {
        InitializeComponent();

        _outline = new Polygon
        {
            Stroke = new SolidColorBrush(Color.Parse("#FF2D7DD2")),
            StrokeThickness = 1,
            Fill = null,
            IsVisible = false
        };
        Overlay.Children.Add(_outline);

        for (var i = 0; i < _handles.Length; i++)
        {
            _handles[i] = new Rectangle
            {
                Width = HandleSize,
                Height = HandleSize,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.Parse("#FF2D7DD2")),
                StrokeThickness = 1,
                IsVisible = false
            };
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
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm = DataContext as MainWindowViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnViewModelPropertyChanged;

        TrackFrame(_vm?.Selected);
        RefreshOverlay();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.Selected):
                EndTextEdit();
                TrackFrame(_vm?.Selected);
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

    /// <summary>Suit la géométrie du bloc sélectionné pour maintenir les poignées collées à lui.</summary>
    private void TrackFrame(Frame? frame)
    {
        if (ReferenceEquals(_tracked, frame)) return;

        if (_tracked is not null) _tracked.PropertyChanged -= OnFramePropertyChanged;
        _tracked = frame;
        if (_tracked is not null) _tracked.PropertyChanged += OnFramePropertyChanged;
    }

    private void OnFramePropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshOverlay();

    // ---------------------------------------------------------------- poignées

    private void RefreshOverlay()
    {
        var frame = _vm?.Selected;
        var visible = frame is not null && _vm?.CurrentPage?.Frames.Contains(frame) == true;

        _outline.IsVisible = visible;
        foreach (var handle in _handles) handle.IsVisible = visible;
        if (!visible || frame is null) return;

        var corners = new[]
        {
            SurfacePoint(frame, 0, 0),
            SurfacePoint(frame, frame.Width, 0),
            SurfacePoint(frame, frame.Width, frame.Height),
            SurfacePoint(frame, 0, frame.Height)
        };

        _outline.Points = corners.ToList();

        for (var i = 0; i < _handles.Length; i++)
        {
            var (lx, ly) = LocalAnchor(frame, (Anchor)i);
            var point = SurfacePoint(frame, lx, ly);
            Canvas.SetLeft(_handles[i], point.X - HandleSize / 2);
            Canvas.SetTop(_handles[i], point.Y - HandleSize / 2);
        }
    }

    /// <summary>Convertit un point du repère local d'un bloc vers les pixels écran de la surface.</summary>
    private Point SurfacePoint(Frame frame, double localX, double localY)
    {
        var (px, py) = frame.ToPage(localX, localY);
        return new Point(px * Scale, py * Scale);
    }

    private static (double X, double Y) LocalAnchor(Frame frame, Anchor anchor) => anchor switch
    {
        Anchor.NorthWest => (0, 0),
        Anchor.North => (frame.Width / 2, 0),
        Anchor.NorthEast => (frame.Width, 0),
        Anchor.East => (frame.Width, frame.Height / 2),
        Anchor.SouthEast => (frame.Width, frame.Height),
        Anchor.South => (frame.Width / 2, frame.Height),
        Anchor.SouthWest => (0, frame.Height),
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

        if (_vm.Selected is { } selected && HitHandle(selected, surfacePoint) is { } anchor)
        {
            _mode = DragMode.Resize;
            _anchor = anchor;
            Remember(selected, page);
            _vm.BeginEdit();
            e.Pointer.Capture(InputLayer);
            e.Handled = true;
            return;
        }

        // Ordre de superposition : le dernier bloc dessiné est le premier candidat.
        var hit = _vm.CurrentPage.Frames.LastOrDefault(f => f.HitTest(page.X, page.Y));
        _vm.Selected = hit;

        if (hit is not null)
        {
            _mode = DragMode.Move;
            Remember(hit, page);
            _vm.BeginEdit();
            e.Pointer.Capture(InputLayer);
        }

        e.Handled = true;
    }

    private void Remember(Frame frame, Point page)
    {
        _startPointer = page;
        _startX = frame.X;
        _startY = frame.Y;
        _startWidth = frame.Width;
        _startHeight = frame.Height;
        _startRotation = frame.Rotation;
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

        if (_vm.Selected is not { } frame) return;

        if (_mode == DragMode.Move) ApplyMove(frame, page, e.KeyModifiers);
        else ApplyResize(frame, page, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
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

        if (_vm?.Selected is { } selected && HitHandle(selected, surfacePoint) is { } anchor)
        {
            // La rotation fait tourner le sens du redimensionnement : on décale
            // l'index de poignée d'un cran par quart de tour de 45°.
            var index = (int)anchor + (int)Math.Round(selected.Rotation / 45.0);
            InputLayer.Cursor = EdgeCursors[((index % 8) + 8) % 8];
            return;
        }

        var over = _vm?.CurrentPage?.Frames.LastOrDefault(f => f.HitTest(page.X, page.Y));
        InputLayer.Cursor = over is null ? ArrowCursor : MoveCursor;
    }

    // ---------------------------------------------------------------- déplacement

    private void ApplyMove(Frame frame, Point page, KeyModifiers modifiers)
    {
        var x = _startX + (page.X - _startPointer.X);
        var y = _startY + (page.Y - _startPointer.Y);

        // Alt désactive l'aimantation ; la rotation la désactive aussi, faute de
        // bords parallèles aux repères.
        if (!modifiers.HasFlag(KeyModifiers.Alt) && frame.Rotation == 0 && _vm is not null)
        {
            var document = _vm.Document;
            var tolerance = SnapPixels / Scale;

            x = SnapEdge(x, frame.Width, tolerance,
                [0, document.MarginLeft, document.PageWidth / 2,
                 document.PageWidth - document.MarginRight, document.PageWidth]);

            y = SnapEdge(y, frame.Height, tolerance,
                [0, document.MarginTop, document.PageHeight / 2,
                 document.PageHeight - document.MarginBottom, document.PageHeight]);
        }

        frame.X = x;
        frame.Y = y;
    }

    /// <summary>
    /// Aimante l'un des trois bords du bloc (début, milieu, fin) sur le repère le
    /// plus proche, si l'écart reste sous la tolérance.
    /// </summary>
    private static double SnapEdge(double position, double size, double tolerance, double[] guides)
    {
        var best = position;
        var bestDistance = tolerance;

        foreach (var guide in guides)
        {
            foreach (var offset in new[] { 0.0, size / 2, size })
            {
                var candidate = guide - offset;
                var distance = Math.Abs(candidate - position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
        }

        return best;
    }

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

    // ---------------------------------------------------------------- saisie de texte

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm?.Selected is TextFrame text) BeginTextEdit(text);
    }

    private void BeginTextEdit(TextFrame frame)
    {
        if (_editing is not null) return;

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
