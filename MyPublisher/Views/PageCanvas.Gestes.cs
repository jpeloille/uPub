using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Les modificateurs du glisser, et le rectangle de sélection (poste N17 ; fiche 23, c8, c10 à
/// c14). Ce que Publisher fait des touches tenues pendant un geste, et que la planche ne faisait
/// pas : <c>ApplyMove</c> ne lisait qu'<c>Alt</c> ; <c>Ctrl</c> et <c>Maj</c> basculaient le bloc
/// dans la sélection à la pression, et rien d'autre ; la seule copie était <c>Ctrl+D</c>, décalée
/// de 12 pt ; une pression dans le vide vidait la sélection ; le curseur de rotation était une
/// main.
///
///   - <b>Sur un bloc, <c>Ctrl</c> et <c>Maj</c> attendent le relâché</b> pour basculer le bloc :
///     un clic l'ajoute ou le retire, comme avant. Si le pointeur passe le seuil du glissé (Q21),
///     le bloc s'ajoute à la sélection et le geste devient un déplacement — sous <c>Ctrl</c>, de
///     copies posées à la place des originaux, qui restent ; sous <c>Maj</c>, tenu sur l'axe
///     dominant du chemin. Un seul pas d'annulation : celui que la pression a ouvert.
///   - <b>Au tracé</b>, <c>Maj</c> donne un carré ou un cercle, <c>Ctrl</c> trace depuis le
///     centre ; <b>sur une poignée</b>, <c>Ctrl</c> redimensionne depuis le centre.
///   - <b>Un rectangle de sélection</b>, tiré dans le vide ou depuis le gris, prend les blocs
///     visibles de la page dont l'emprise est tout entière dedans ; <c>Maj</c> ou <c>Ctrl</c>
///     l'ajoute à la sélection. Il se dessine dans l'<c>Overlay</c>, jamais dans <c>PageRoot</c>
///     (règle 4), aux pinceaux de l'apparence lus par le code.
///   - <b>Échap annule un glissé</b> en cours : le bloc revient à sa place, les copies s'en vont,
///     le repère retourne d'où il venait, et la transaction se referme sans pas.
///   - <b>Le curseur de rotation</b> est la flèche circulaire de l'icône de rotation de
///     l'apparence, dessinée comme la main et la loupe (PageCanvas.Navigation.cs).
/// </summary>
public partial class PageCanvas
{
    // ------------------------------------------------------------------ Ctrl et Maj sur un bloc

    /// <summary>
    /// Le bloc qu'une pression sous <c>Ctrl</c> ou <c>Maj</c> a pris, tant que le geste n'a pas
    /// glissé : le relâché le basculera dans la sélection. Nul dès que le geste a glissé.
    /// </summary>
    private Frame? _basculeEnAttente;

    /// <summary>La pression était sous <c>Ctrl</c> : le glissé copie au lieu de déplacer.</summary>
    private bool _copieAuGlisse;

    /// <summary>Les copies que le glissé déplace, et la sélection d'avant elles : ce qu'Échap défait.</summary>
    private readonly List<Frame> _copies = [];
    private readonly List<Frame> _originauxDesCopies = [];

    /// <summary>
    /// Une pression sous <c>Ctrl</c> ou <c>Maj</c> sur un bloc. Elle ne change rien à la
    /// sélection : elle retient le bloc, ouvre la transaction du geste et prend le pointeur. Le
    /// relâché basculera le bloc, si le pointeur n'a pas glissé ; sinon le glissé le prendra.
    /// </summary>
    private void PrendreSousUnModificateur(Frame bloc, Point page, PointerPressedEventArgs e)
    {
        if (_vm is null) return;

        _basculeEnAttente = bloc;
        _copieAuGlisse = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        _copies.Clear();
        _originauxDesCopies.Clear();

        _mode = DragMode.Move;
        RememberMove(page);
        _vm.BeginEdit();
        e.Pointer.Capture(InputLayer);
    }

    /// <summary>
    /// Le premier mouvement, passé le seuil, d'un geste parti sous <c>Ctrl</c> ou <c>Maj</c> : le
    /// bloc pris s'ajoute à la sélection s'il n'y était pas — on ne déplace pas ce qu'on retire —,
    /// puis, sous <c>Ctrl</c>, la sélection est copiée à sa place, et ce sont les copies que le
    /// geste emporte. Les départs se relisent sur ce que le geste déplace désormais.
    /// </summary>
    private void PrendreLeGlisseSousUnModificateur()
    {
        if (_vm?.CurrentPage is not { } feuille || _basculeEnAttente is not { } bloc) return;

        _basculeEnAttente = null;
        if (!_vm.Selection.Contains(bloc)) _vm.ToggleSelection(bloc);
        if (_copieAuGlisse) CopierLaSelectionSurPlace(feuille);
        RememberMove(_startPointer);
    }

    /// <summary>
    /// Pose une copie de chaque bloc visible de la sélection, à la même place, au-dessus de tout,
    /// dans l'ordre de superposition des originaux — comme <c>Ctrl+D</c>, sans le décalage —, et
    /// sélectionne les copies. Dans la transaction que la pression a ouverte : la copie et son
    /// déplacement font un seul pas.
    /// </summary>
    private void CopierLaSelectionSurPlace(Page feuille)
    {
        if (_vm is null) return;

        _originauxDesCopies.AddRange(_vm.Selection);
        foreach (var original in _vm.SelectionInPageOrder().Where(bloc => bloc.Visible))
        {
            var copie = original.Clone();
            feuille.Frames.Add(copie);
            _copies.Add(copie);
        }

        _vm.SetSelection(_copies.ToList());
        _vm.Status = _copies.Count == 1 ? "Bloc copié" : $"{_copies.Count} blocs copiés";
    }

    /// <summary>
    /// Le relâché d'un geste parti sous <c>Ctrl</c> ou <c>Maj</c> qui n'a pas glissé : le clic
    /// bascule le bloc, l'ajoute ou le retire, comme avant le poste.
    /// </summary>
    private void BasculerAuRelache()
    {
        _copies.Clear();
        _originauxDesCopies.Clear();
        if (_basculeEnAttente is not { } bloc) return;

        _basculeEnAttente = null;
        _vm?.ToggleSelection(bloc);
    }

    /// <summary>
    /// <c>Maj</c> pendant un déplacement : le chemin garde son axe dominant, l'autre est nul. À
    /// égalité, l'horizontale.
    /// </summary>
    private static (double Dx, double Dy) TenirLAxe(double dx, double dy) =>
        Math.Abs(dx) >= Math.Abs(dy) ? (dx, 0) : (0, dy);

    // ------------------------------------------------------------------ le tracé et les poignées

    /// <summary>
    /// Les deux coins opposés du cadre qu'on trace, de la pression au pointeur : sous
    /// <c>Maj</c>, un carré — le côté est le plus grand des deux chemins, chacun dans son sens —,
    /// qui fait un cercle à l'outil Ellipse ; sous <c>Ctrl</c>, la pression est le centre, et le
    /// cadre s'étend autant de l'autre côté. Les deux ensemble : un carré centré.
    /// </summary>
    private static (Point Coin, Point Oppose) CoinsDuTrace(Point pression, Point pointeur, KeyModifiers modificateurs)
    {
        var dx = pointeur.X - pression.X;
        var dy = pointeur.Y - pression.Y;

        if (modificateurs.HasFlag(KeyModifiers.Shift))
        {
            var cote = Math.Max(Math.Abs(dx), Math.Abs(dy));
            dx = dx < 0 ? -cote : cote;
            dy = dy < 0 ? -cote : cote;
        }

        var chemin = new Vector(dx, dy);
        return modificateurs.HasFlag(KeyModifiers.Control)
            ? (pression - chemin, pression + chemin)
            : (pression, pression + chemin);
    }

    /// <summary>
    /// <c>Ctrl</c> est tenu pendant qu'on tire une poignée : le bloc grandit ou rétrécit
    /// autour de son centre.
    /// </summary>
    private bool _depuisLeCentre;

    /// <summary>
    /// Les deux bords d'un côté du bloc, dans son repère, quand on le redimensionne depuis le
    /// centre : le bord tiré suit le chemin du pointeur, le bord opposé le suit en miroir. Sans
    /// bord tiré sur cet axe, le côté ne change pas. Il ne descend pas sous la taille minimale
    /// d'un bloc.
    /// </summary>
    private static (double Debut, double Fin) DepuisLeCentre(double chemin, double cote, bool tireLeDebut, bool tireLaFin)
    {
        if (!tireLeDebut && !tireLaFin) return (0, cote);

        var croit = Math.Max(tireLeDebut ? -chemin : chemin, (MinFrameSize - cote) / 2);
        return (-croit, cote + croit);
    }

    // ------------------------------------------------------------------ le rectangle de sélection

    /// <summary>Le rectangle de sélection, posé dans l'Overlay à sa première sortie.</summary>
    private Rectangle? _rectangleDeSelection;

    /// <summary>Le rectangle a été tiré sous <c>Maj</c> ou <c>Ctrl</c> : il ajoute à la sélection.</summary>
    private bool _rectangleAjoute;

    /// <summary>
    /// Une pression dans le vide de la page, ou sur le gris : le rectangle part d'ici. Il ne
    /// paraît qu'au-delà du seuil du glissé ; en deçà, la pression n'est qu'un clic dans le vide.
    /// </summary>
    private void CommencerLeRectangle(Point page, bool ajoute, PointerPressedEventArgs e)
    {
        _mode = DragMode.Rectangle;
        _startPointer = page;
        _rectangleAjoute = ajoute;
        e.Pointer.Capture(InputLayer);
    }

    /// <summary>Le rectangle, de la pression au pointeur, en points de page, dans quelque sens qu'on le tire.</summary>
    private Rect CadreDuRectangle(Point page) =>
        new(Math.Min(_startPointer.X, page.X), Math.Min(_startPointer.Y, page.Y),
            Math.Abs(page.X - _startPointer.X), Math.Abs(page.Y - _startPointer.Y));

    private void TirerLeRectangle(Point page)
    {
        var rectangle = RectangleDeSelection();
        var cadre = CadreDuRectangle(page);
        Canvas.SetLeft(rectangle, cadre.X * Scale);
        Canvas.SetTop(rectangle, cadre.Y * Scale);
        rectangle.Width = cadre.Width * Scale;
        rectangle.Height = cadre.Height * Scale;
        rectangle.IsVisible = true;
    }

    /// <summary>
    /// Le relâché du rectangle : les blocs visibles de la page dont l'emprise est tout entière
    /// dedans, dans l'ordre de la page, remplacent la sélection, ou s'y ajoutent sous <c>Maj</c>
    /// ou <c>Ctrl</c>. Une sélection de plusieurs blocs ne garde pas un bloc masqué, comme celle
    /// que <c>Ctrl</c>+clic étend. Un clic qui n'a pas glissé ne prend rien : il a déjà vidé la
    /// sélection à la pression, s'il le devait.
    /// </summary>
    private void FinirLeRectangle(Point page)
    {
        CacherLeRectangle();
        if (!_aGlisse || _vm?.CurrentPage is not { } feuille) return;

        var cadre = CadreDuRectangle(page);
        var pris = feuille.Frames.Where(bloc => bloc.Visible && ToutEntierDans(bloc.Extent, cadre)).ToList();

        var selection = _rectangleAjoute ? _vm.Selection.Concat(pris.Where(bloc => !_vm.Selection.Contains(bloc))).ToList() : pris;
        if (selection.Count > 1) selection.RemoveAll(bloc => !bloc.Visible);
        _vm.SetSelection(selection);
    }

    private static bool ToutEntierDans(Extent emprise, Rect cadre) =>
        emprise.Left >= cadre.Left && emprise.Right <= cadre.Right &&
        emprise.Top >= cadre.Top && emprise.Bottom <= cadre.Bottom;

    private void CacherLeRectangle()
    {
        if (_rectangleDeSelection is not null) _rectangleDeSelection.IsVisible = false;
    }

    /// <summary>
    /// Le rectangle de sélection : un pointillé d'accent sur l'accent à 14 %, comme le cadre
    /// qu'on trace. Les deux pinceaux sont ceux de l'apparence, lus ici.
    /// </summary>
    private Rectangle RectangleDeSelection()
    {
        if (_rectangleDeSelection is not null) return _rectangleDeSelection;

        _rectangleDeSelection = new Rectangle
        {
            Name = "RectangleDeSelection",
            Stroke = PinceauDeLApparence("Accent"),
            Fill = PinceauDeLApparence("Projection"),
            StrokeThickness = 1,
            StrokeDashArray = new AvaloniaList<double> { 4, 3 },
            IsHitTestVisible = false,
            IsVisible = false,
        };
        Overlay.Children.Add(_rectangleDeSelection);
        return _rectangleDeSelection;
    }

    private IBrush? PinceauDeLApparence(string cle) =>
        this.TryFindResource(cle, ActualThemeVariant, out var pinceau) ? pinceau as IBrush : null;

    // ------------------------------------------------------------------ Échap

    /// <summary>La place d'un repère à la prise, et s'il vient d'être tiré d'une règle.</summary>
    private double _departDuRepere;
    private bool _repereNeuf;

    /// <summary>
    /// Échap pendant un glissé : tout revient où le geste l'a pris — les blocs déplacés à leur
    /// départ, les copies retirées et les originaux resélectionnés, le bloc redimensionné ou
    /// tourné à sa géométrie, le repère à sa place ou retiré s'il venait d'une règle —, puis la
    /// transaction se referme, sans pas puisque rien n'a changé. Le rectangle de sélection
    /// s'efface. Le reste du geste, jusqu'au relâché, ne fait plus rien. Faux s'il n'y a pas de
    /// glissé à annuler : le tracé, la main et le glissé dans le texte gardent leur Échap.
    /// </summary>
    private bool AnnulerLeGlisse()
    {
        if (_vm is null) return false;

        switch (_mode)
        {
            case DragMode.Move:
                foreach (var (bloc, x, y) in _moveStart)
                {
                    bloc.X = x;
                    bloc.Y = y;
                }

                if (_copies.Count > 0 && _vm.CurrentPage is { } feuille)
                {
                    foreach (var copie in _copies) feuille.Frames.Remove(copie);
                    _vm.SetSelection(_originauxDesCopies.ToList());
                }

                _basculeEnAttente = null;
                _copies.Clear();
                _originauxDesCopies.Clear();
                break;

            case DragMode.Resize or DragMode.Rotate or DragMode.Extremite:
                if (_vm.SingleSelected is { } pris)
                {
                    pris.Rotation = _startRotation;
                    pris.Width = _startWidth;
                    pris.Height = _startHeight;
                    pris.X = _startX;
                    pris.Y = _startY;
                }
                break;

            case DragMode.Guide:
                if (_draggedGuide is { } repere)
                {
                    if (_repereNeuf)
                    {
                        (_draggedGuideIsVertical ? _vm.Document.VerticalRulerGuides : _vm.Document.HorizontalRulerGuides)
                            .Remove(repere);
                    }
                    else repere.Position = _departDuRepere;
                }

                _draggedGuide = null;
                break;

            case DragMode.Rectangle:
                CacherLeRectangle();
                _mode = DragMode.None;
                return true;

            default:
                return false;
        }

        _mode = DragMode.None;
        _vm.CommitEdit();
        RefreshOverlay();
        return true;
    }

    // ------------------------------------------------------------------ le curseur de rotation

    private Cursor? _curseurDeRotation;

    /// <summary>
    /// Le curseur de la poignée de rotation : la flèche circulaire de l'icône de rotation de
    /// l'apparence (fiche 23, c8 ; Publisher, p. 33 et 50), dessinée comme la main et la loupe,
    /// le point chaud au centre du cercle. Avant le poste, une main, faute de curseur de rotation
    /// dans le jeu standard.
    /// </summary>
    private Cursor CurseurDeRotation =>
        _curseurDeRotation ??= Dessiner([TraceDeLIcone("IconeRotation")], 1.8, new PixelPoint(12, 12));
}
