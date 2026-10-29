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

    /// <summary>
    /// Le geste en cours. <see cref="Pan"/> est celui de la main (poste I6) : il fait
    /// défiler la planche et ne touche à rien d'autre. <see cref="Extremite"/> tire un bout
    /// de trait (poste A10, PageCanvas.Trait.cs). <see cref="Texte"/> est la pression qui a
    /// ouvert la saisie dans le texte d'un bloc : son glissé sélectionne du texte (poste N12,
    /// PageCanvas.Saisie.cs). <see cref="Rectangle"/> est le rectangle de sélection, tiré dans
    /// le vide ou depuis le gris (poste N17, PageCanvas.Gestes.cs).
    /// </summary>
    private enum DragMode { None, Move, Resize, Rotate, Create, Guide, Pan, Extremite, Texte, Rectangle }

    /// <summary>
    /// Côté d'une poignée carrée, en pixels écran : les 7 px de blanc de la maquette dans
    /// un filet de 1,5 px, qu'un Rectangle d'Avalonia rentre dans ses limites (poste I4).
    /// </summary>
    private const double HandleSize = 10;
    private const double HandleGrab = 7;      // rayon de préhension, pixels écran
    private const double GuideGrab = 4;       // un repère est un trait : on le saisit de près
    private const double SnapPixels = 6;      // tolérance d'aimantation, pixels écran

    /// <summary>
    /// Le seuil du glissé, en pixels d'écran (postes Q21 et Q22) : tant que le pointeur reste
    /// à moins de trois pixels de sa prise, le geste n'est qu'un clic qui tremble, et il ne
    /// touche à rien — ni au bloc, ni à ses poignées, ni à sa rotation, ni aux extrémités d'un
    /// trait ; l'aimantation ne joue qu'au-delà. Une fois franchi, le seuil ne compte plus : un
    /// glissé mené plus loin, puis ramené à un point de sa prise, déplace d'un point.
    /// </summary>
    private const double SeuilDuGlisse = 3;

    /// <summary>
    /// Diamètre de la poignée de rotation : un disque de 9 px de blanc dans le même filet
    /// de 1,5 px que les poignées carrées.
    /// </summary>
    private const double RotateHandleSize = 12;

    /// <summary>
    /// Où passe le cadre de sélection, en pixels écran hors du bloc : son bord extérieur
    /// est à 4 px, comme la maquette le pose, et son trait de 1,5 px est centré sur cette
    /// ligne-ci. Les poignées s'y posent, à cheval sur le cadre et non sur le bloc.
    /// </summary>
    private const double SelectionGap = 3.25;

    /// <summary>
    /// Distance du bord haut du bloc à la poignée de rotation, en pixels écran — celle de
    /// la maquette, 22 px au-dessus du cadre. Hors de la zone de préhension des poignées
    /// d'angle (7 px de part et d'autre du cadre) : deux poignées qui se recouvrent,
    /// c'est un geste qui part au hasard.
    /// </summary>
    private const double RotateHandleGap = 26;

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

    /// <summary>Où le pointeur a pris, en pixels d'écran (le repère de Surface).</summary>
    private Point _priseALEcran;

    /// <summary>
    /// Le pointeur a-t-il quitté sa prise d'au moins <see cref="SeuilDuGlisse"/> depuis la
    /// pression ? Faux, le geste n'est encore qu'un clic.
    /// </summary>
    private bool _aGlisse;
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

    /// <summary>
    /// Où le pointeur mène le repère, avant l'aimantation : c'est sur cette cible que se
    /// décide le retrait au relâché. Aimantée, la position d'un repère lâché sur le gris à
    /// moins de six pixels de la page revenait sur le bord, et le repère restait.
    /// </summary>
    private double _cibleDuRepere;

    public PageCanvas()
    {
        InitializeComponent();

        // Les marques sont habillées par leur classe, dans PageCanvas.axaml : les couleurs
        // de l'apparence ne s'écrivent pas ici (poste I4).
        //
        // La tige d'abord : posée avant les poignées, elle passe dessous, et le disque
        // reste saisissable sur toute sa surface.
        _rotateStem = new Polyline { Classes = { "tigeDeRotation" }, IsVisible = false };
        Overlay.Children.Add(_rotateStem);

        for (var i = 0; i < _handles.Length; i++)
        {
            // La neuvième ne redimensionne pas : elle tourne. Un disque plutôt qu'un
            // carré — un neuvième carré au-dessus du bloc se lirait comme un bord à tirer.
            // Blanc bordé d'accent comme les huit autres, au bout de sa tige : c'est la
            // forme, et non plus le vert, qui dit le geste, comme dans la maquette.
            var rotation = (Anchor)i == Anchor.Rotate;
            _handles[i] = rotation ? new Ellipse() : new Rectangle();
            _handles[i].Classes.Add("poignee");
            _handles[i].Width = rotation ? RotateHandleSize : HandleSize;
            _handles[i].Height = rotation ? RotateHandleSize : HandleSize;
            _handles[i].IsVisible = false;

            Overlay.Children.Add(_handles[i]);
        }

        _preview = new Rectangle
        {
            Classes = { "traceEnCours" },
            StrokeDashArray = new AvaloniaList<double> { 4, 3 },
            IsVisible = false
        };
        Overlay.Children.Add(_preview);

        PoserLaPlanche();

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
        // Le double-clic n'ouvre plus la saisie : le premier clic l'a ouverte, et le second
        // atteint le champ, qui prend le mot (poste N12).
        // Pendant la saisie, la couche laisse le texte à son champ, et à lui seul (poste N5).
        InputLayer.Laisse = LaisseAuTexteEnSaisie;
        // Au passage descendant : la zone qui défile consommerait la molette avant nous
        // (poste I6, voir PageCanvas.Navigation.cs).
        Scroller.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);

        // En phase de tunnel : on quitte la saisie avant que le clic n'atteigne sa cible — une
        // règle, une barre de défilement. La couche de saisie, elle, la quitte elle-même.
        AddHandler(PointerPressedEvent, OnTunnelPointerPressed, RoutingStrategies.Tunnel);

        // La transaction de la saisie suit le focus de sa zone (poste Q25) : un champ de la
        // barre ou du dock qui le prend pendant la frappe y fait son propre pas.
        AddHandler(GotFocusEvent, (_, e) => SuivreLeFocusDeLaSaisie(e, focus: true), RoutingStrategies.Bubble);
        AddHandler(LostFocusEvent, (_, e) => SuivreLeFocusDeLaSaisie(e, focus: false), RoutingStrategies.Bubble);

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
            _vm.PageEntiereDemandee -= OnPageEntiereDemandee;
            _vm.TransactionsRefermees -= OnTransactionsRefermees;
            _vm.SaisieDemandee -= OnSaisieDemandee;
        }

        _vm = DataContext as MainWindowViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            // La collection n'est jamais remplacée : un seul abonnement suffit, et il
            // couvre aussi les changements qui ne déplacent pas l'ancre.
            _vm.Selection.CollectionChanged += OnSelectionChanged;

            // « Page entière » est un calcul de la planche, qui seule connaît sa taille à
            // l'écran : le bouton reste grisé tant qu'elle ne l'écoute pas (poste I6).
            _vm.PageEntiereDemandee += OnPageEntiereDemandee;

            // Annuler et Rétablir referment la saisie et le glissé avec le reste (Q24).
            _vm.TransactionsRefermees += OnTransactionsRefermees;

            // Insertion ▸ Bloc de texte ouvre la saisie du bloc posé, comme l'outil (N12).
            _vm.SaisieDemandee += OnSaisieDemandee;
        }

        TrackFrames();
        SuivreLesReperes();
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
            case nameof(MainWindowViewModel.Document):
                // Un autre document, d'autres repères : les traits de la planche suivent
                // les listes du nouveau.
                SuivreLesReperes();
                RefreshOverlay();
                break;
            case nameof(MainWindowViewModel.RenderScale):
                PlacerLesReperes();
                RefreshOverlay();
                break;
            case nameof(MainWindowViewModel.CurrentPage):
                RefreshOverlay();
                break;
            case nameof(MainWindowViewModel.ActiveTool):
                // Changer d'outil pendant un tracé l'abandonne : sans cela, Échap
                // laisserait le cadre en pointillés affiché à l'écran.
                CancelCreate();

                // Un outil de tracé s'annonce par le curseur — la main et la loupe aussi ;
                // on quitte aussi la saisie en cours, qui monopoliserait le pointeur.
                EndTextEdit();
                InputLayer.Cursor = CurseurAuRepos(KeyModifiers.None);
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

    private void OnFramePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Un bloc masqué ne se saisit pas sur la page (poste I8) : fermer l'œil du bloc qu'on
        // tape referme la saisie, comme un clic hors du bloc. Fermée tout de suite, elle
        // refermerait la transaction de l'œil, au milieu de sa commande ; elle l'est donc
        // une fois la commande jouée, et le pas de l'œil reste le sien. Trouvé par la
        // relecture du 26 octobre 2026 : le bloc restait en saisie, invisible, Échap sans
        // effet, et le clic suivant sur la planche ne servait qu'à en sortir.
        if (e.PropertyName == nameof(Frame.Visible) && sender is TextFrame { Visible: false } masque &&
            ReferenceEquals(masque, _editing))
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(_editing, masque) && !masque.Visible) EndTextEdit();
            });
        }

        RefreshOverlay();
    }

    /// <summary>
    /// Annuler ou Rétablir a refermé les transactions de la planche avec les autres (poste
    /// Q24). Chacun de ses deux propriétaires encore actif rouvre la sienne, et lui seul.
    ///
    /// La saisie, si elle est toujours ouverte et que sa zone a le focus — sa transaction suit
    /// ce focus (Q25) : c'est qu'il n'y avait rien à défaire, <c>Ctrl+Z</c> sur un historique
    /// vide, <c>Ctrl+Y</c> après une frappe. Une annulation qui relit le document l'a refermée
    /// par le changement de sélection, et rien ne se rouvre pour elle : une transaction
    /// rouverte à l'aveugle resterait sans propriétaire, et chaque geste suivant y fondrait son
    /// pas.
    ///
    /// Le glissé, si le bouton est toujours tenu : son relâché refermera ce qu'il rouvre. Après
    /// une annulation qui a relu le document, il tire des blocs que la page n'a plus, et ne
    /// change rien.
    /// </summary>
    private void OnTransactionsRefermees(object? sender, EventArgs e)
    {
        _saisieEnTransaction = false;
        if (ZoneDeLaSaisieAuFocus()) OuvrirLaTransactionDeLaSaisie();
        if (_mode is DragMode.Move or DragMode.Resize or DragMode.Rotate or DragMode.Guide or DragMode.Extremite)
        {
            _vm?.BeginEdit();
        }
    }

    // ---------------------------------------------------------------- poignées

    private void RefreshOverlay()
    {
        var page = _vm?.CurrentPage;
        // Un bloc masqué (poste I8) se sélectionne dans la liste, mais la planche n'en
        // montre rien : ni cadre, ni poignées, ni projection. Ce qu'on ne voit pas ne se
        // saisit pas, et une poignée tirée retoucherait un bloc que rien ne dessine.
        var frames = _vm is null || page is null
            ? []
            : _vm.Selection.Where(frame => frame.Visible && page.Frames.Contains(frame)).ToList();

        for (var i = 0; i < frames.Count; i++)
        {
            var outline = OutlineAt(i);
            outline.Points = Corners(frames[i]);
            outline.IsVisible = true;
        }

        for (var i = frames.Count; i < _outlines.Count; i++) _outlines[i].IsVisible = false;

        // La projection sur les règles suit la sélection, et le geste avec elle : chaque
        // mouvement d'un bloc pris repasse par ici. Un tracé en cours projette son cadre.
        if (_mode != DragMode.Create) Projeter(Extent.Union(frames.Select(frame => frame.Extent)));

        // Les poignées ne servent qu'à un bloc seul, celle de rotation comprise :
        // redimensionner — ou tourner — un ensemble demanderait de reprendre chacun de
        // ses blocs autour d'un pivot commun, ce que le groupe fait proprement. On
        // sélectionne, on groupe, puis on redimensionne ou l'on tourne.
        var single = frames.Count == 1 ? frames[0] : null;
        // Un trait n'en a que deux, sur ses extrémités, et pas de rotation (poste A10).
        for (var i = 0; i < _handles.Length; i++) _handles[i].IsVisible = single is not null && PoigneeServie(single, (Anchor)i);
        _rotateStem.IsVisible = single is not null && !single.IsLine;

        // La bulle de dimensions ne parle que pendant un redimensionnement — et pendant un
        // tracé, que UpdatePreview sert. Elle dit la taille du bloc, comme la barre d'état.
        if (single is not null && _mode == DragMode.Resize) MontrerLaBulle(single.Extent, single.Width, single.Height);
        else if (single is ShapeFrame trait && _mode == DragMode.Extremite)
            MontrerLaBulleDuTrait(trait.Extent, trait.Debut.X, trait.Debut.Y, trait.Fin.X, trait.Fin.Y);
        else if (_mode != DragMode.Create) CacherLaBulle();

        if (single is null) return;

        for (var i = 0; i < _handles.Length; i++)
        {
            var (lx, ly) = LocalAnchor(single, (Anchor)i);
            var point = SurfacePoint(single, lx, ly);
            Canvas.SetLeft(_handles[i], point.X - _handles[i].Width / 2);
            Canvas.SetTop(_handles[i], point.Y - _handles[i].Height / 2);
        }

        var (nx, ny) = LocalAnchor(single, Anchor.North);
        var (rx, ry) = LocalAnchor(single, Anchor.Rotate);
        _rotateStem.Points = [SurfacePoint(single, nx, ny), SurfacePoint(single, rx, ry)];
    }

    /// <summary>
    /// Les quatre coins du cadre de sélection : ceux du bloc, écartés de
    /// <see cref="SelectionGap"/> pixels dans son propre repère — le cadre tourne avec lui.
    /// </summary>
    private List<Point> Corners(Frame frame)
    {
        var ecart = SelectionGap / Scale;
        return
        [
            SurfacePoint(frame, -ecart, -ecart),
            SurfacePoint(frame, frame.Width + ecart, -ecart),
            SurfacePoint(frame, frame.Width + ecart, frame.Height + ecart),
            SurfacePoint(frame, -ecart, frame.Height + ecart)
        ];
    }

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
                Classes = { "cadreDeSelection" },
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
    /// ne plus être statique. Les huit autres se posent sur le cadre de sélection, écarté
    /// du bloc d'une distance d'écran elle aussi (poste I4).
    /// </summary>
    private (double X, double Y) LocalAnchor(Frame frame, Anchor anchor)
    {
        if (AncreDExtremite(frame, anchor) is { } extremite) return extremite;

        var e = SelectionGap / Scale;
        return anchor switch
        {
            Anchor.NorthWest => (-e, -e),
            Anchor.North => (frame.Width / 2, -e),
            Anchor.NorthEast => (frame.Width + e, -e),
            Anchor.East => (frame.Width + e, frame.Height / 2),
            Anchor.SouthEast => (frame.Width + e, frame.Height + e),
            Anchor.South => (frame.Width / 2, frame.Height + e),
            Anchor.SouthWest => (-e, frame.Height + e),
            Anchor.Rotate => (frame.Width / 2, -RotateHandleGap / Scale),
            _ => (-e, frame.Height / 2)
        };
    }

    private Anchor? HitHandle(Frame frame, Point surfacePoint)
    {
        for (var i = 0; i < _handles.Length; i++)
        {
            if (!PoigneeServie(frame, (Anchor)i)) continue;

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

    /// <summary>
    /// Le point, en points de page, tombe-t-il sur la page, bords compris ? La couche de
    /// saisie couvre toute la planche depuis le poste I4, pour les poignées et les repères
    /// qui en sortent ; les blocs et les outils, eux, s'arrêtent au bord de la page,
    /// comme avant. Ce que PageRoot rogne ne se voit pas : ni un bloc qui déborde ne se
    /// prend sur le gris, ni un outil armé n'y pose un bloc.
    /// </summary>
    private bool DansLaPage(Point page) =>
        _vm is { } vm && page.X >= 0 && page.Y >= 0 &&
        page.X <= vm.Document.PageWidth && page.Y <= vm.Document.PageHeight;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Un clic hors du texte en saisie la referme, puis fait ce qu'il ferait hors saisie :
        // il prend une poignée, un repère, un autre bloc, ou vide la sélection — d'un seul
        // geste, comme Publisher (poste N5, fiche 23, a8 et a9). Rien n'est rejoué. Avant le
        // choix du bouton : la pression du bouton droit sort aussi de la saisie, et c'est ici
        // que le menu contextuel la prendra (A12).
        if (_editing is not null) EndTextEdit();

        if (_vm?.CurrentPage is null) return;
        if (!e.GetCurrentPoint(Surface).Properties.IsLeftButtonPressed) return;

        Focus();

        // La main et la loupe passent avant tout : elles ne touchent à aucun bloc (I6), et
        // n'ouvrent la saisie d'aucun texte.
        if (PrendreLaNavigation(e)) return;

        var surfacePoint = e.GetPosition(Surface);
        var page = ToPageCoordinates(surfacePoint);
        _priseALEcran = surfacePoint;
        _aGlisse = false;

        // Un outil armé prend la main : on crée au lieu de sélectionner. Sur le gris, il ne
        // se passe rien, et l'outil reste armé : le bloc tomberait hors de la page, où
        // PageRoot le rognerait tout entier.
        if (_vm.IsDrawing)
        {
            e.Handled = true;
            if (!DansLaPage(page)) return;

            _mode = DragMode.Create;
            _startPointer = page;
            if (!MontrerLeTraitEnCours(page, page, e.KeyModifiers)) UpdatePreview(page, page);
            e.Pointer.Capture(InputLayer);
            return;
        }

        if (_vm.SingleSelected is { Visible: true } single && HitHandle(single, surfacePoint) is { } anchor)
        {
            // Même prise, mêmes repères de départ, même capture : la rotation n'est
            // qu'un second mode de ce geste-là. BeginEdit ici, CommitEdit au relâché,
            // comme le redimensionnement — c'est ce qui la rend annulable d'un pas.
            _mode = anchor == Anchor.Rotate ? DragMode.Rotate : DragMode.Resize;
            _anchor = anchor;
            RememberResize(single, page);
            if (single is ShapeFrame { IsLine: true } trait)
            {
                _mode = DragMode.Extremite;
                PrendreUneExtremite(trait, anchor);
            }
            _vm.BeginEdit();
            e.Pointer.Capture(InputLayer);
            e.Handled = true;

            // La bulle de dimensions paraît dès la prise, pas au premier mouvement.
            RefreshOverlay();
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

        // Ordre de superposition : le dernier bloc dessiné est le premier candidat. Sur la
        // page seulement : le gris libère la sélection, même là où passe, rognée, la part
        // d'un bloc qui déborde. Un bloc masqué n'est pas candidat (poste I8) : un clic à
        // sa place prend le bloc d'en dessous.
        var hit = DansLaPage(page) ? _vm.CurrentPage.Frames.LastOrDefault(f => f.Visible && f.HitTest(page.X, page.Y)) : null;
        // La bande de bord d'un bloc sélectionné s'étend jusqu'à son cadre (poste N12).
        hit = BlocVise(page, hit);
        var additive = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                       e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (hit is null)
        {
            // Cliquer à côté libère la sélection — sauf si l'on est en train de
            // l'étendre, geste où un clic manqué ne doit pas tout défaire. La pression tire
            // aussi un rectangle de sélection, que Ctrl et Maj font ajouter (poste N17).
            if (!additive) _vm.SetSelection([]);
            CommencerLeRectangle(page, additive, e);
            e.Handled = true;
            return;
        }

        // Dans le texte d'un bloc, sans Ctrl ni Maj, la pression entre dans le texte, le
        // curseur au point visé ; sur sa bande de bord, elle prend le bloc comme ci-dessous
        // (poste N12, PageCanvas.Saisie.cs). Ctrl et Maj ajoutent ou retirent, sans saisie.
        if (!additive && EntrerDansLeTexte(hit, page, e))
        {
            e.Handled = true;
            return;
        }

        // Ctrl et Maj ne basculent plus le bloc à la pression : ils attendent le relâché, et
        // le glissé, s'il vient, copie sous Ctrl ou tient l'axe sous Maj (poste N17,
        // PageCanvas.Gestes.cs).
        if (additive)
        {
            PrendreSousUnModificateur(hit, page, e);
            e.Handled = true;
            return;
        }

        if (!_vm.Selection.Contains(hit)) _vm.SelectOnly(hit);

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
        // Les blocs visibles seuls : un bloc masqué ne suit pas la main, et son empreinte
        // ne compte pas dans celle qu'on aimante (poste I8).
        var visibles = _vm.Selection.Where(frame => frame.Visible).ToList();
        foreach (var frame in visibles) _moveStart.Add((frame, frame.X, frame.Y));

        _moveSnappable = visibles.All(frame => frame.Rotation == 0);
        _moveExtent = Extent.Union(visibles.Select(frame => frame.Extent)) ?? default;
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
            if (!PoserLeCurseurDeNavigation(e.KeyModifiers)) UpdateCursor(surfacePoint, page);
            return;
        }

        if (_mode == DragMode.Pan)
        {
            GlisserLaPlanche(e);
            return;
        }

        if (_mode == DragMode.Create)
        {
            // Maj trace un carré ou un cercle, Ctrl depuis le centre (poste N17).
            if (!MontrerLeTraitEnCours(_startPointer, page, e.KeyModifiers))
            {
                var (coin, oppose) = CoinsDuTrace(_startPointer, page, e.KeyModifiers);
                UpdatePreview(coin, oppose);
            }
            return;
        }

        if (_mode == DragMode.Guide)
        {
            ApplyGuideMove(page, e.KeyModifiers);
            return;
        }

        // Un pointeur qui tremble sur sa prise n'a pas encore glissé : le bloc, ses poignées,
        // sa rotation et les extrémités d'un trait l'ignorent, et rien ne passe par
        // l'aimantation ni par les cosinus d'un bloc tourné (postes Q21 et Q22). Un pixel de
        // mouvement près d'une marge faisait sauter le bloc de 3 pt ; une pression sans
        // mouvement sur une poignée décalait un bloc tourné, et Maj ramenait à 0° un bloc à 7°.
        if (!AGlisse(surfacePoint)) return;

        // Le glissé dans le texte sélectionne du texte, une fois le seuil franchi (N12).
        if (_mode == DragMode.Texte)
        {
            EtendreLaSelectionDuTexte(e);
            return;
        }

        // Le rectangle de sélection ne paraît qu'au-delà du seuil : en deçà, c'est un clic (N17).
        if (_mode == DragMode.Rectangle)
        {
            TirerLeRectangle(page);
            return;
        }

        if (_mode == DragMode.Move)
        {
            // Sous Ctrl ou Maj, le glissé décide au seuil de ce que la pression prenait : le
            // bloc s'ajoute, et Ctrl le copie avec le reste de la sélection (N17).
            PrendreLeGlisseSousUnModificateur();
            ApplyMove(page, e.KeyModifiers);
            return;
        }

        if (_vm.SingleSelected is not { } frame) return;

        // Maj sert aux deux gestes, et pas à la même chose : proportions conservées au
        // redimensionnement, pas de 15° à la rotation. C'est ce que fait Publisher. Au
        // redimensionnement, Maj fait l'inverse de la case « Conserver les proportions »
        // du dock (poste I6) : cochée, le coin garde le rapport, et Maj le libère.
        var maj = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        // Ctrl sur une poignée redimensionne depuis le centre, comme Publisher (poste N17).
        _depuisLeCentre = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (_mode == DragMode.Rotate) ApplyRotate(frame, page, maj);
        else if (_mode == DragMode.Extremite && frame is ShapeFrame trait) TirerUneExtremite(trait, page, e.KeyModifiers);
        else ApplyResize(frame, page, maj != _vm.ConserverLesProportions);
    }

    /// <summary>
    /// Le pointeur a-t-il glissé depuis la pression ? Il glisse dès qu'il s'est trouvé une fois
    /// à <see cref="SeuilDuGlisse"/> pixels d'écran ou plus de sa prise — distance mesurée à
    /// vol d'oiseau —, et le reste jusqu'au relâché.
    /// </summary>
    private bool AGlisse(Point surfacePoint)
    {
        if (_aGlisse) return true;

        var ecart = surfacePoint - _priseALEcran;
        _aGlisse = ecart.X * ecart.X + ecart.Y * ecart.Y >= SeuilDuGlisse * SeuilDuGlisse;
        return _aGlisse;
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
            FinishCreate(e.GetPosition(Surface), e.KeyModifiers);
            return;
        }

        if (mode == DragMode.Guide)
        {
            FinishGuideDrag();
            return;
        }

        // La main n'a ouvert aucun pas d'annulation : elle n'en ferme pas.
        if (mode == DragMode.Pan)
        {
            LacherLaPlanche();
            return;
        }

        // La pression dans le texte non plus : la saisie tient sa transaction au focus (Q25).
        if (mode == DragMode.Texte)
        {
            FinirLeGlisseDansLeTexte(e);
            return;
        }

        // Le rectangle de sélection ne change que la sélection (poste N17).
        if (mode == DragMode.Rectangle)
        {
            FinirLeRectangle(ToPageCoordinates(e.GetPosition(Surface)));
            return;
        }

        _vm?.CommitEdit();

        // Un Ctrl+clic ou un Maj+clic qui n'a pas glissé bascule le bloc au relâché (N17).
        BasculerAuRelache();

        // Le geste fini, la bulle se retire ; la projection, elle, reste sur la sélection.
        RefreshOverlay();
    }

    /// <summary>Abandonne un tracé en cours et efface son aperçu.</summary>
    private void CancelCreate()
    {
        if (_mode != DragMode.Create) return;

        _mode = DragMode.None;
        _preview.IsVisible = false;
        CacherLeTraitEnCours();
        RefreshOverlay();
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

        // Le cadre qu'on trace se projette sur les règles et dit sa taille dans la bulle,
        // comme un bloc qu'on redimensionne : c'en sera un au relâché.
        var cadre = new Extent(
            Math.Min(origin.X, current.X), Math.Min(origin.Y, current.Y),
            Math.Max(origin.X, current.X), Math.Max(origin.Y, current.Y));
        Projeter(cadre);
        MontrerLaBulle(cadre, cadre.Width, cadre.Height);
    }

    private void FinishCreate(Point surfacePoint, KeyModifiers modifiers)
    {
        _preview.IsVisible = false;
        CacherLeTraitEnCours();
        CacherLaBulle();

        if (_vm is null) return;

        var end = ToPageCoordinates(surfacePoint);

        // Un trait se pose par ses deux extrémités (poste A10).
        if (FinirLeTrait(_startPointer, end, modifiers))
        {
            InputLayer.Cursor = ArrowCursor;
            return;
        }
        // Maj pose un carré ou un cercle, Ctrl un cadre centré sur la pression (poste N17).
        var (coin, oppose) = CoinsDuTrace(_startPointer, end, modifiers);
        var frame = _vm.DrawWithActiveTool(
            Math.Min(coin.X, oppose.X),
            Math.Min(coin.Y, oppose.Y),
            Math.Abs(oppose.X - coin.X),
            Math.Abs(oppose.Y - coin.Y));

        InputLayer.Cursor = ArrowCursor;

        // Un bloc de texte fraîchement tracé est vide, le curseur dedans (N12).
        if (frame is TextFrame text) BeginTextEdit(text, 0);
    }

    private void UpdateCursor(Point surfacePoint, Point page)
    {
        if (_vm?.IsDrawing == true)
        {
            InputLayer.Cursor = DansLaPage(page) ? DrawCursor : ArrowCursor;
            return;
        }

        if (_vm?.SingleSelected is { Visible: true } selected && HitHandle(selected, surfacePoint) is { } anchor)
        {
            // La neuvième poignée d'abord : elle n'est pas un bord, et l'arithmétique
            // ci-dessous — qui compte huit poignées — lui donnerait un curseur de
            // redimensionnement parfaitement valide, donc parfaitement trompeur.
            if (anchor == Anchor.Rotate)
            {
                InputLayer.Cursor = CurseurDeRotation;
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

        var over = DansLaPage(page) ? _vm?.CurrentPage?.Frames.LastOrDefault(f => f.Visible && f.HitTest(page.X, page.Y)) : null;
        // Le I sur le texte, les quatre flèches sur le bord, cadre d'un bloc pris compris (N12).
        InputLayer.Cursor = CurseurSur(BlocVise(page, over), page);
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

        // La règle ne sert plus la poignée de rotation avant de poser un repère, comme
        // A1 le lui avait appris. Elle bordait alors la page, et la poignée d'un bloc
        // collé au haut s'y dessinait, visible et imprenable. Depuis I4, la règle est fixe
        // hors de la zone qui défile : une poignée ne s'y dessine plus jamais — elle tombe
        // sur la planche, que la couche de saisie couvre, ou elle est rognée avec ce qui a
        // défilé. Servie d'ici, elle ferait tourner un bloc qu'on ne voit pas sous un clic
        // qui voulait un repère.
        var surfacePoint = e.GetPosition(Surface);
        var vertical = ruler.Orientation == Avalonia.Layout.Orientation.Horizontal;
        var page = ToPageCoordinates(surfacePoint);
        var guide = new RulerGuide(vertical ? page.X : page.Y);

        BeginGuideDrag(guide, vertical);
        _repereNeuf = true;
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
        _cibleDuRepere = guide.Position;
        // Ce qu'Échap rend : la place du repère, ou rien s'il vient d'une règle (poste N17).
        _departDuRepere = guide.Position;
        _repereNeuf = false;
        _vm?.BeginEdit();
    }

    /// <summary>
    /// Suit le pointeur, en s'aimantant sur les autres repères — marges, grille,
    /// colonnes — pour qu'on puisse poser un repère exactement sur un bord de colonne.
    /// Le repère traîné est écarté des candidats, sans quoi il s'accrocherait à
    /// lui-même et ne bougerait plus : lui seul, une occurrence de sa position. Écarter
    /// cette valeur partout écartait avec lui le bord ou la marge où il venait de
    /// s'aimanter, et le repère, au mouvement suivant, s'en décrochait — un mouvement
    /// sur deux, le long d'un bord.
    /// </summary>
    private void ApplyGuideMove(Point page, KeyModifiers modifiers)
    {
        if (_draggedGuide is null || _vm is null) return;

        var target = _draggedGuideIsVertical ? page.X : page.Y;
        _cibleDuRepere = target;

        if (!modifiers.HasFlag(KeyModifiers.Alt))
        {
            var candidates = (_draggedGuideIsVertical
                    ? _vm.Document.VerticalGuides
                    : _vm.Document.HorizontalGuides)
                .ToList();
            var lui = candidates.LastIndexOf(_draggedGuide.Position);
            if (lui >= 0) candidates.RemoveAt(lui);

            target = SnapEdge(target, 0, SnapPixels / Scale, candidates);
        }

        _draggedGuide.Position = target;
    }

    /// <summary>
    /// Ferme le geste. Un repère lâché hors de la page est retiré : c'est le moyen de
    /// s'en débarrasser, en le ramenant d'où il vient. Hors de la page, c'est là où le
    /// pointeur l'a mené, et non là où l'aimantation l'a mis.
    /// </summary>
    private void FinishGuideDrag()
    {
        if (_draggedGuide is not null && _vm is not null)
        {
            var limit = _draggedGuideIsVertical ? _vm.Document.PageWidth : _vm.Document.PageHeight;
            if (_cibleDuRepere < 0 || _cibleDuRepere > limit)
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

        // Un pointeur qui n'a pas quitté son point de prise ne déplace rien. Sans cette
        // garde, l'aimantation ci-dessous attire vers un repère voisin un bloc qu'on n'a
        // fait que cliquer : un mouvement nul, que la plate-forme peut signaler entre
        // les deux clics d'un double-clic, déplaçait alors le bloc et empilait un pas
        // d'annulation (poste Q14). Mesuré sur ATA 21 : trois des huit blocs qu'un clic
        // atteint en leur centre.
        //
        // Le seuil du glissé (Q21) arrête désormais ce mouvement-là avant qu'il n'arrive
        // ici. La garde sert le glissé qui a franchi le seuil puis revient exactement à son
        // point de prise : elle rend chaque bloc à son départ, sans l'aimanter.
        if (dx == 0 && dy == 0)
        {
            foreach (var (frame, startX, startY) in _moveStart)
            {
                frame.X = startX;
                frame.Y = startY;
            }

            return;
        }

        // Maj tient l'axe dominant du chemin : l'autre ne bouge pas, et l'aimantation ne l'y
        // fait pas bouger non plus (poste N17).
        var axeTenu = modifiers.HasFlag(KeyModifiers.Shift);
        if (axeTenu) (dx, dy) = TenirLAxe(dx, dy);

        // Alt désactive l'aimantation ; un bloc tourné la désactive aussi, faute de
        // bords parallèles aux repères. On aimante l'empreinte de l'ensemble, puis on
        // reporte le même écart sur chaque bloc : la sélection garde sa forme.
        if (!modifiers.HasFlag(KeyModifiers.Alt) && _moveSnappable && _vm is not null)
        {
            var document = _vm.Document;
            var tolerance = SnapPixels / Scale;

            // Les repères sont ceux du document : bords, marges, milieu, et la grille de
            // composition quand il y en a une. Le tableau s'allonge, le geste ne change pas.
            if (!axeTenu || dx != 0)
            {
                dx = SnapEdge(_moveExtent.Left + dx, _moveExtent.Width, tolerance,
                    document.VerticalGuides) - _moveExtent.Left;
            }

            if (!axeTenu || dy != 0)
            {
                dy = SnapEdge(_moveExtent.Top + dy, _moveExtent.Height, tolerance,
                    document.HorizontalGuides) - _moveExtent.Top;
            }
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

        // Le chemin du pointeur depuis la prise, dans le repère local du bloc tel qu'il
        // était au début du geste : on raisonne ensuite sur un rectangle non tourné.
        //
        // Le chemin, et non la position : la poignée est posée sur le cadre de sélection,
        // quelques pixels hors du bloc (poste I4). Le bord qui suivrait le pointeur
        // sauterait jusqu'à lui au premier pixel du geste. Pris exactement sur le coin du
        // bloc, les deux calculs donnent le même bord.
        var dx = page.X - _startPointer.X;
        var dy = page.Y - _startPointer.Y;
        var localX = dx * cos + dy * sin;
        var localY = -dx * sin + dy * cos;

        var movesLeft = _anchor is Anchor.NorthWest or Anchor.West or Anchor.SouthWest;
        var movesRight = _anchor is Anchor.NorthEast or Anchor.East or Anchor.SouthEast;
        var movesTop = _anchor is Anchor.NorthWest or Anchor.North or Anchor.NorthEast;
        var movesBottom = _anchor is Anchor.SouthWest or Anchor.South or Anchor.SouthEast;

        double left = 0, top = 0, right = _startWidth, bottom = _startHeight;
        if (_depuisLeCentre)
        {
            // Sous Ctrl, le bord opposé suit en miroir : le centre ne bouge pas (poste N17).
            (left, right) = DepuisLeCentre(localX, _startWidth, movesLeft, movesRight);
            (top, bottom) = DepuisLeCentre(localY, _startHeight, movesTop, movesBottom);
        }
        else
        {
            if (movesLeft) left = Math.Min(localX, right - MinFrameSize);
            if (movesRight) right = Math.Max(_startWidth + localX, left + MinFrameSize);
            if (movesTop) top = Math.Min(localY, bottom - MinFrameSize);
            if (movesBottom) bottom = Math.Max(_startHeight + localY, top + MinFrameSize);
        }

        var width = right - left;
        var height = bottom - top;

        // Sur une poignée d'angle, Maj ou la case du dock : conservation des proportions
        // d'origine. Les poignées de côté ne les gardent jamais, comme dans Publisher.
        if (keepAspect && movesLeft != movesRight && movesTop != movesBottom &&
            _startWidth > 0 && _startHeight > 0)
        {
            var ratio = _startWidth / _startHeight;
            if (width / height > ratio) width = height * ratio;
            else height = width / ratio;

            if (_depuisLeCentre)
            {
                left = (_startWidth - width) / 2;
                right = left + width;
                top = (_startHeight - height) / 2;
                bottom = top + height;
            }
            else
            {
                if (movesLeft) left = right - width; else right = left + width;
                if (movesTop) top = bottom - height; else bottom = top + height;
            }
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

    /// <summary>
    /// Le modèle de vue a posé un bloc de texte par le menu Insertion : on y entre, vide, le
    /// curseur dedans, comme dans un bloc tracé à l'outil (poste N12).
    /// </summary>
    private void OnSaisieDemandee(object? sender, TextFrame bloc)
    {
        EndTextEdit();
        BeginTextEdit(bloc, bloc.Text?.Length ?? 0);
    }

    /// <summary>
    /// Ouvre la saisie du bloc, le curseur à la position <paramref name="curseur"/> : celle que
    /// la pression a visée, ou le début d'un bloc neuf. Rien n'est sélectionné (poste N12) ;
    /// avant, la saisie s'ouvrait au double-clic et sélectionnait tout le texte.
    /// </summary>
    private void BeginTextEdit(TextFrame frame, int curseur)
    {
        if (_editing is not null) return;

        // Un bloc masqué ne se saisit pas sur la page, où il n'est pas (poste I8).
        if (!frame.Visible) return;

        // Un bloc à champ affiche une valeur calculée : la saisir sur la page
        // reviendrait à modifier un texte qu'on ne voit pas.
        if (frame.HasField)
        {
            if (_vm is not null) _vm.Status = "Ce bloc affiche un champ ; son contenu se règle dans le panneau.";
            return;
        }

        // La transaction ne s'ouvre pas ici : elle s'ouvrira quand la zone prendra le focus, et
        // se refermera chaque fois qu'elle le perdra (poste Q25, SuivreLeFocusDeLaSaisie).
        // La couche de saisie reste atteignable : elle laisse au champ le seul texte en saisie,
        // dès qu'il existe (poste N5, LaisseAuTexteEnSaisie).
        _editing = frame;
        frame.IsEditing = true;
        RefreshOverlay();

        // Le TextBox n'existe qu'après le passage de mise en page suivant.
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_editing, frame)) return;

            var box = Scaler.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(t => ReferenceEquals(t.DataContext, frame));
            _zoneDeSaisie = box;
            if (box is null) return;

            box.Focus();
            var position = Math.Clamp(curseur, 0, box.Text?.Length ?? 0);
            PoserLaSelection(box, position, position);
        }, DispatcherPriority.Background);
    }

    public void EndTextEdit()
    {
        if (_editing is null) return;

        FermerLaTransactionDeLaSaisie();
        _editing.IsEditing = false;
        _editing = null;
        _zoneDeSaisie = null;
        RefreshOverlay();
    }

    /// <summary>Le champ du texte en saisie, une fois que la mise en page l'a posé.</summary>
    private TextBox? _zoneDeSaisie;

    /// <summary>
    /// Ce que la couche de saisie laisse au champ du texte en saisie, le point lu dans son
    /// repère à elle (poste N5) : le texte, et rien d'autre. Ce que <c>PageRoot</c> rogne d'un
    /// bloc qui déborde de la page ne se voit pas, et ne se tape pas : la couche le garde, comme
    /// elle garde les poignées du bloc, qui débordent un peu sur son texte et passent avant lui.
    /// </summary>
    private bool LaisseAuTexteEnSaisie(Point surLaCouche)
    {
        if (_editing is null || _zoneDeSaisie is not { IsEffectivelyVisible: true } zone) return false;

        if (InputLayer.TranslatePoint(surLaCouche, zone) is not { } dansLaZone ||
            !new Rect(zone.Bounds.Size).Contains(dansLaZone))
        {
            return false;
        }

        if (InputLayer.TranslatePoint(surLaCouche, Surface) is not { } surfacePoint) return false;
        return DansLaPage(ToPageCoordinates(surfacePoint)) && HitHandle(_editing, surfacePoint) is null;
    }

    /// <summary>
    /// La saisie tient sa transaction d'édition (poste Q25). Elle suit le focus de sa zone sur
    /// la page — ouverte quand la zone le prend, refermée quand elle le perd, et à la fin de la
    /// saisie —, et non plus la saisie elle-même, du clic qui l'ouvre à Échap.
    ///
    /// Ouverte de bout en bout, elle avalait celle d'un champ de la barre ou du dock qui prenait
    /// le focus pendant la frappe : celle-ci s'y emboîtait, et le corps changé entre deux mots
    /// se fondait dans le pas de la frappe (règle 2 de la phase I). Désormais le champ ouvre la
    /// sienne quand la zone a déjà refermé la sienne — le focus quitte l'une avant d'arriver à
    /// l'autre —, et la frappe qui reprend, d'un clic dans le texte, en rouvre une. La saisie,
    /// elle, reste ouverte quand on clique dans la barre ou le dock, comme avant.
    ///
    /// Les boutons G, I et S ne prennent pas le focus : la zone le garde, et ils font leur pas
    /// par <c>UnGesteUnPas</c>, qui referme la transaction de la saisie, empile le sien et la
    /// rouvre aussitôt.
    /// </summary>
    private bool _saisieEnTransaction;

    private void SuivreLeFocusDeLaSaisie(RoutedEventArgs e, bool focus)
    {
        if (_editing is null || e.Source is not TextBox zone || !ReferenceEquals(zone.DataContext, _editing)) return;

        if (focus) OuvrirLaTransactionDeLaSaisie();
        else FermerLaTransactionDeLaSaisie();
    }

    private void OuvrirLaTransactionDeLaSaisie()
    {
        if (_saisieEnTransaction || _editing is null) return;

        _saisieEnTransaction = true;
        _vm?.BeginEdit();
    }

    private void FermerLaTransactionDeLaSaisie()
    {
        if (!_saisieEnTransaction) return;

        _saisieEnTransaction = false;
        _vm?.CommitEdit();
    }

    /// <summary>
    /// La zone de saisie du bloc en cours d'édition a le focus. Elle seule : le champ Contenu du
    /// dock est lié au même bloc, mais hors de la planche.
    /// </summary>
    private bool ZoneDeLaSaisieAuFocus() =>
        _editing is not null &&
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox zone &&
        ReferenceEquals(zone.DataContext, _editing) && this.IsVisualAncestorOf(zone);

    private void OnTunnelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_editing is null) return;

        // La pression qui atteint la couche de saisie y referme elle-même la saisie, puis agit
        // (poste N5, OnPointerPressed) : la refermer ici l'y précéderait sans rien changer.
        if (ReferenceEquals(e.Source, InputLayer)) return;

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
        // Échap pendant un glissé l'annule : le bloc revient à sa place, sans pas (poste N17).
        if (e.Key == Key.Escape && AnnulerLeGlisse())
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _editing is not null)
        {
            EndTextEdit();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
