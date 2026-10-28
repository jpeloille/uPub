using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MyPublisher.Models;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Naviguer dans la planche (poste I6), à côté des gestes du fichier principal.
///
///   - La main — l'outil du rail, ou <c>Espace</c> tenu — fait glisser la planche : elle
///     ne change que le défilement du <c>ScrollViewer</c>, sans toucher à aucun bloc ni
///     ouvrir de pas d'annulation.
///   - La loupe zoome d'un pas au clic, en arrière sous <c>Alt</c> ; <c>Ctrl</c>+molette
///     fait de même, d'un pas plus fin. Dans les deux cas, le point visé reste sous le
///     pointeur, autant que le défilement le permet : une page plus petite que la zone
///     visible y est centrée, et ne défile pas.
///   - « Page entière » tient la page dans la zone visible, la marge de la planche
///     déduite — les règles sont déjà hors de la zone qui défile. C'est une commande, et
///     jamais l'effet d'une ouverture ni d'un redimensionnement (règle 5 de la phase).
///   - L'affichage au clavier (poste N4) : F9 et le champ du zoom zooment sans pointeur,
///     autour du centre de la sélection, ou du milieu de la zone visible ; le second F9
///     rend l'affichage d'avant, défilement compris ; « Objets sélectionnés » tient la
///     sélection dans la zone, comme « Page entière » y tient la page. Le modèle de vue
///     décide ; la planche calcule (<see cref="OnZoomDemande"/>).
///
/// Rien de tout cela ne vit dans PageRoot ni ne change le document.
/// </summary>
public partial class PageCanvas
{
    /// <summary>
    /// Le pas de la molette sous <c>Ctrl</c>, plus fin que celui de la loupe : un cran de
    /// molette est un geste plus petit qu'un clic.
    /// </summary>
    private const double PasDeLaMolette = 1.1;

    /// <summary>
    /// Ce que « Page entière » retire encore à une page qui dépassait : un millième de
    /// pixel, qui ne se voit pas, et qui la fait passer sous l'entier que l'arrondi visait.
    /// </summary>
    private const double SousLePixel = 1e-3;

    /// <summary>Taille des curseurs dessinés, en pixels : celle des icônes de la maquette.</summary>
    private const int TailleDUnCurseur = 24;

    /// <summary>
    /// La main fermée, que le jeu standard n'a pas, ni la maquette. Le tracé de la main
    /// ouverte de l'apparence, les doigts repliés : quatre phalanges au-dessus de la paume,
    /// le pouce et le poignet inchangés. Sur la même grille de 24.
    /// </summary>
    private const string TraceDeLaMainFermee =
        "M8 13v-2.5a1.5 1.5 0 0 1 3 0V12M11 11.5V9.5a1.5 1.5 0 0 1 3 0V12M14 12v-2a1.5 1.5 0 0 1 3 0v4" +
        "a7 7 0 0 1-7 7c-2.5 0-4-1.5-5.5-4L3 14a1.5 1.5 0 0 1 2.5-1.6L8 15";

    /// <summary>Le « + » et le « − » dans le verre de la loupe, centré en (10,5 ; 10,5).</summary>
    private const string TraceDuPlus = "M8 10.5h5M10.5 8v5";
    private const string TraceDuMoins = "M8 10.5h5";

    private Cursor? _curseurMainOuverte, _curseurMainFermee, _curseurLoupePlus, _curseurLoupeMoins;

    /// <summary>D'où part le glissé de la main : le pointeur dans la zone visible, et le défilement.</summary>
    private Point _departDeLaMain;
    private Vector _defilementDeDepart;

    private bool _mainTenue;

    /// <summary>
    /// <c>Espace</c> est tenu : la planche se conduit comme sous l'outil Main, quel que soit
    /// l'outil armé, jusqu'au relâchement de la touche. C'est la fenêtre qui le dit — elle
    /// seule sait si la touche irait à une saisie ou à un bouton qui a le focus.
    /// </summary>
    public bool MainTenue
    {
        get => _mainTenue;
        set
        {
            if (_mainTenue == value) return;
            _mainTenue = value;
            if (_mode == DragMode.None) InputLayer.Cursor = CurseurAuRepos(KeyModifiers.None);
        }
    }

    /// <summary>La main est l'outil du moment : armée au rail, ou tenue sous <c>Espace</c>.</summary>
    private bool MainArmee => MainTenue || _vm?.ActiveTool == EditorTool.Main;

    private bool LoupeArmee => !MainTenue && _vm?.ActiveTool == EditorTool.Zoom;

    /// <summary>
    /// Le curseur de la planche quand aucun geste n'est en cours et que le pointeur n'est sur
    /// rien de particulier : celui de l'outil. Les poignées et les blocs affinent ensuite,
    /// sauf sous la main et la loupe, qui ne les voient pas.
    /// </summary>
    private Cursor CurseurAuRepos(KeyModifiers modificateurs)
    {
        if (MainArmee) return _curseurMainOuverte ??= CurseurMain(fermee: false);
        if (LoupeArmee)
        {
            return modificateurs.HasFlag(KeyModifiers.Alt)
                ? _curseurLoupeMoins ??= CurseurLoupe(TraceDuMoins)
                : _curseurLoupePlus ??= CurseurLoupe(TraceDuPlus);
        }

        return _vm?.IsDrawing == true ? DrawCursor : ArrowCursor;
    }

    /// <summary>
    /// Pose le curseur de la main ou de la loupe, si l'une est armée, et le dit ; sinon,
    /// c'est à <see cref="UpdateCursor"/> de choisir d'après ce qui est sous le pointeur.
    /// </summary>
    private bool PoserLeCurseurDeNavigation(KeyModifiers modificateurs)
    {
        if (!MainArmee && !LoupeArmee) return false;

        InputLayer.Cursor = CurseurAuRepos(modificateurs);
        return true;
    }

    // ------------------------------------------------------------------ gestes

    /// <summary>
    /// La pression du pointeur sous la main ou la loupe. Elle passe avant tout le reste —
    /// poignées, repères, blocs — : ni l'une ni l'autre ne touche au document. Rend vrai si
    /// elle a pris le geste.
    /// </summary>
    private bool PrendreLaNavigation(PointerPressedEventArgs e)
    {
        if (MainArmee)
        {
            _mode = DragMode.Pan;
            _departDeLaMain = e.GetPosition(Scroller);
            _defilementDeDepart = Scroller.Offset;
            InputLayer.Cursor = _curseurMainFermee ??= CurseurMain(fermee: true);
            e.Pointer.Capture(InputLayer);
            e.Handled = true;
            return true;
        }

        if (LoupeArmee)
        {
            var pas = e.KeyModifiers.HasFlag(KeyModifiers.Alt)
                ? 1 / MainWindowViewModel.PasDeLaLoupe
                : MainWindowViewModel.PasDeLaLoupe;
            ZoomerAutourDe(e.GetPosition(Surface), _vm!.Zoom * pas);
            e.Handled = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Suit le pointeur : la planche avance avec lui, d'autant qu'il a bougé depuis la
    /// prise. Le chemin se mesure dans la zone visible, qui ne bouge pas, et non sur la
    /// page, qui défile sous lui. Le <c>ScrollViewer</c> borne le défilement.
    /// </summary>
    private void GlisserLaPlanche(PointerEventArgs e)
    {
        var chemin = e.GetPosition(Scroller) - _departDeLaMain;
        Scroller.Offset = _defilementDeDepart - chemin;
    }

    /// <summary>Le glissé fini, la main se rouvre — ou l'outil d'avant revient, Espace relâché.</summary>
    private void LacherLaPlanche() => InputLayer.Cursor = CurseurAuRepos(KeyModifiers.None);

    /// <summary>
    /// <c>Ctrl</c>+molette. Prise au passage descendant, avant que la zone qui défile ne
    /// consomme la molette pour elle : sans quoi, sur une page plus grande que la zone, la
    /// molette ferait défiler au lieu de zoomer.
    /// </summary>
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || _vm is null) return;

        ZoomerAutourDe(e.GetPosition(Surface), _vm.Zoom * (e.Delta.Y > 0 ? PasDeLaMolette : 1 / PasDeLaMolette));
        e.Handled = true;
    }

    /// <summary>
    /// Change le zoom en gardant sous le pointeur le point qu'il vise. Le point est lu en
    /// points de page, depuis le coin de PageRoot dans Surface — comme le zéro des règles,
    /// que l'arrondi de mise en page peut décaler d'un pixel (poste I4) — ; la mise en page
    /// passe au nouveau zoom ; puis le défilement ramène ce point où était le pointeur dans
    /// la zone visible.
    /// </summary>
    private void ZoomerAutourDe(Point pointeurDansSurface, double zoom)
    {
        if (_vm is null) return;

        var coin = PageRoot.TranslatePoint(default, Surface) ?? default;
        var vise = (pointeurDansSurface - coin) / Scale;
        var dansLaZone = (Surface.TranslatePoint(pointeurDansSurface, Stage) ?? default) - Scroller.Offset;
        Zoomer(vise, dansLaZone, zoom);
    }

    /// <summary>
    /// Pose le zoom, puis fait défiler la planche pour que le point de page <paramref name="vise"/>
    /// tombe en <paramref name="dansLaZone"/>, en pixels depuis le coin de la zone visible —
    /// autant que le défilement le permet : une page plus petite que la zone y est centrée.
    /// Le geste de la loupe et de la molette, et celui du clavier (N4).
    /// </summary>
    private void Zoomer(Vector vise, Point dansLaZone, double zoom)
    {
        if (_vm is null) return;

        _vm.SetZoom(zoom);
        UpdateLayout();

        var nouveauCoin = PageRoot.TranslatePoint(default, Surface) ?? default;
        var ici = Surface.TranslatePoint(nouveauCoin + vise * Scale, Stage) ?? default;
        Scroller.Offset = ici - dansLaZone;
    }

    // ------------------------------------------------------- l'affichage au clavier (N4)

    /// <summary>Le modèle de vue dont la planche sert les demandes d'affichage du poste N4.</summary>
    private MainWindowViewModel? _vmDeLAffichage;

    /// <summary>
    /// La planche sert les demandes d'affichage du modèle de vue qu'elle porte : F9, le
    /// champ du zoom, « Objets sélectionnés ». L'abonnement suit le contexte, comme celui que
    /// pose AttachViewModel au même changement — l'événement que la classe de base lève,
    /// et que le constructeur écoute, passe d'abord —, sans toucher au fichier principal de
    /// la planche, qu'un seul poste d'une salve retouche (règle 16 de la phase N).
    /// </summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vmDeLAffichage is not null)
        {
            _vmDeLAffichage.ZoomDemande -= OnZoomDemande;
            _vmDeLAffichage.ObjetsSelectionnesDemandes -= OnObjetsSelectionnesDemandes;
        }

        _vmDeLAffichage = DataContext as MainWindowViewModel;

        if (_vmDeLAffichage is not null)
        {
            _vmDeLAffichage.ZoomDemande += OnZoomDemande;
            _vmDeLAffichage.ObjetsSelectionnesDemandes += OnObjetsSelectionnesDemandes;
        }
    }

    /// <summary>
    /// Un zoom demandé sans pointeur. Le second F9 rend un affichage — son zoom, puis son
    /// défilement, tel qu'il était : la zone n'a pas changé de taille entre-temps, ou le
    /// défilement s'y borne. Sinon, le zoom change autour de ce que <see cref="PointARetenir"/>
    /// désigne. La planche dit d'où elle partait : c'est ce que le second F9 rendra.
    /// </summary>
    private void OnZoomDemande(object? sender, MainWindowViewModel.DemandeDeZoom demande)
    {
        if (_vm is null) return;

        demande.Servie = true;
        demande.DefilementDAvant = Scroller.Offset;

        if (demande.Defilement is { } defilement)
        {
            _vm.SetZoom(demande.Zoom);
            UpdateLayout();
            Scroller.Offset = defilement;
            return;
        }

        var (vise, dansLaZone) = PointARetenir();
        Zoomer(vise, dansLaZone, demande.Zoom);
    }

    /// <summary>
    /// Ce qui garde sa place quand le zoom change sans pointeur : le centre de la
    /// sélection, là où il est dans la zone visible ; hors de la vue, il vient au milieu de
    /// la zone, plutôt que de rester où l'on ne le voit pas ; sans sélection, ce que le
    /// milieu de la zone montre. Un bloc masqué (I8) n'en est pas : la planche ne le montre
    /// pas. Rend le point de page et sa place dans la zone, en pixels depuis son coin.
    /// </summary>
    private (Vector Vise, Point DansLaZone) PointARetenir()
    {
        var zone = Scroller.Viewport;
        var milieu = new Point(zone.Width / 2, zone.Height / 2);
        var coin = PageRoot.TranslatePoint(default, Surface) ?? default;

        if (_vm is not null &&
            Extent.Union(_vm.Selection.Where(bloc => bloc.Visible).Select(bloc => bloc.Extent)) is { } emprise)
        {
            var centre = new Vector(emprise.CenterX, emprise.CenterY);
            var dansLaZone = (Surface.TranslatePoint(coin + centre * Scale, Stage) ?? default) - Scroller.Offset;
            var enVue = dansLaZone.X >= 0 && dansLaZone.Y >= 0 && dansLaZone.X <= zone.Width && dansLaZone.Y <= zone.Height;
            return (centre, enVue ? dansLaZone : milieu);
        }

        var auMilieu = Stage.TranslatePoint(milieu + Scroller.Offset, Surface) ?? default;
        return ((auMilieu - coin) / Scale, milieu);
    }

    /// <summary>
    /// « Objets sélectionnés » : le plus grand zoom auquel l'emprise de la sélection et la
    /// marge de la planche tiennent dans la zone visible, dans les bornes du zoom, la
    /// sélection au milieu de la zone. Un trait n'a pas de hauteur : un côté compte au
    /// moins pour un point.
    /// </summary>
    private void OnObjetsSelectionnesDemandes(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        if (Extent.Union(_vm.Selection.Where(bloc => bloc.Visible).Select(bloc => bloc.Extent)) is not { } emprise) return;

        var marge = Feuille.Margin;
        var zone = Scroller.Viewport;
        var largeur = zone.Width - marge.Left - marge.Right;
        var hauteur = zone.Height - marge.Top - marge.Bottom;
        if (largeur <= 0 || hauteur <= 0) return;

        var echelle = Math.Min(largeur / Math.Max(emprise.Width, 1), hauteur / Math.Max(emprise.Height, 1));
        Zoomer(new Vector(emprise.CenterX, emprise.CenterY), new Point(zone.Width / 2, zone.Height / 2),
            echelle / Units.PointToDip);
    }

    /// <summary>
    /// « Page entière » : le plus grand zoom auquel la page et la marge de la planche
    /// tiennent dans la zone visible, dans les bornes de <see cref="MainWindowViewModel.SetZoom"/>.
    /// La place est comptée en pixels entiers, la zone pouvant tomber entre deux.
    ///
    /// Le calcul ne suffit pas : la mise en page arrondit la taille de PageRoot au point
    /// SUPÉRIEUR dans son propre repère, puis au pixel supérieur une fois mise à l'échelle —
    /// 841,89 pt font 842 pt, puis 667,09 px font 668 px là où 667 étaient visés, et la zone
    /// défilait d'un pixel. On mesure donc la feuille après la mise en page, et l'on
    /// réduit d'autant ce qui dépasse — une fois suffit, deux au plus.
    /// </summary>
    private void OnPageEntiereDemandee(object? sender, EventArgs e)
    {
        if (_vm is null) return;

        var marge = Feuille.Margin;
        var zone = Scroller.Bounds.Size;
        var largeur = Math.Floor(zone.Width - marge.Left - marge.Right);
        var hauteur = Math.Floor(zone.Height - marge.Top - marge.Bottom);
        if (largeur <= 0 || hauteur <= 0) return;

        var echelle = Math.Min(largeur / _vm.Document.PageWidth, hauteur / _vm.Document.PageHeight);
        for (var essai = 0; essai < 3; essai++)
        {
            _vm.SetZoom(echelle / Units.PointToDip);
            UpdateLayout();

            var tenue = Feuille.Bounds.Size;
            var depasse = Math.Max(tenue.Width / largeur, tenue.Height / hauteur);
            if (depasse <= 1) break;
            echelle = _vm.RenderScale / depasse - SousLePixel / Math.Max(_vm.Document.PageWidth, _vm.Document.PageHeight);
        }

        Scroller.Offset = default;
    }

    // ------------------------------------------------------------------ curseurs

    /// <summary>
    /// Le curseur de la main, ouverte ou fermée. Le jeu standard d'Avalonia n'a qu'une main,
    /// celle de la rotation, qui montre du doigt : l'outil dessine la sienne, au tracé de
    /// l'icône de l'apparence pour la main ouverte.
    /// </summary>
    private Cursor CurseurMain(bool fermee) =>
        Dessiner([fermee ? Geometry.Parse(TraceDeLaMainFermee) : TraceDeLIcone("IconeMain")], 1.7,
            new PixelPoint(12, 12));

    /// <summary>La loupe de l'apparence, son « + » ou son « − » dans le verre, le point chaud au centre du verre.</summary>
    private Cursor CurseurLoupe(string signe) =>
        Dessiner([TraceDeLIcone("IconeLoupe"), Geometry.Parse(signe)], 1.8, new PixelPoint(10, 10));

    /// <summary>
    /// Le tracé d'une icône de l'apparence : la géométrie de chacun de ses chemins, réunis.
    /// Une icône neuve se crée à chaque emploi (<c>x:Shared="False"</c>) : on la lit sans
    /// la poser nulle part.
    /// </summary>
    private Geometry TraceDeLIcone(string cle)
    {
        var chemins = this.TryFindResource(cle, ActualThemeVariant, out var icone) && icone is Viewbox { Child: Canvas toile }
            ? toile.Children.OfType<Path>().Select(chemin => chemin.Data).OfType<Geometry>().ToList()
            : [];

        var groupe = new GeometryGroup();
        foreach (var chemin in chemins) groupe.Children.Add(chemin.Clone());
        return groupe;
    }

    /// <summary>
    /// Un curseur dessiné : le tracé dans la couleur du texte, sur un liseré blanc qui le
    /// détache du gris de la planche comme du sombre d'une image. Les deux pinceaux sont
    /// ceux de l'apparence.
    /// </summary>
    private Cursor Dessiner(Geometry[] traces, double epaisseur, PixelPoint pointChaud)
    {
        var encre = this.TryFindResource("Texte", ActualThemeVariant, out var t) ? t as IBrush : null;
        var lisere = this.TryFindResource("Blanc", ActualThemeVariant, out var b) ? b as IBrush : null;

        var image = new RenderTargetBitmap(new PixelSize(TailleDUnCurseur, TailleDUnCurseur));
        using (var dessin = image.CreateDrawingContext())
        {
            foreach (var (pinceau, largeur) in new[] { (lisere, epaisseur + 2), (encre, epaisseur) })
            {
                if (pinceau is null) continue;
                var plume = new Pen(pinceau, largeur, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                foreach (var trace in traces) dessin.DrawGeometry(null, plume, trace);
            }
        }

        return new Cursor(image, pointChaud);
    }
}
