using System;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using MyPublisher.Models;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Le trait sur la planche (poste A10) : le tracer à l'outil Ligne, et le reprendre par ses
/// deux extrémités. Ce qui s'ajoute aux gestes du fichier principal, dans un fichier à lui.
///
/// Un trait n'a pas de cadre à tirer. Les huit poignées d'un bloc y sont deux, posées sur
/// ses extrémités — celles de l'ouest et de l'est, que sa rotation oriente —, et la
/// poignée de rotation se tait : tirer une extrémité tourne déjà le trait autour de
/// l'autre. Aucun des deux gestes ne passe par le redimensionnement, ni donc par la taille
/// minimale d'un bloc, qu'un trait horizontal, sans hauteur, ne saurait respecter. Sous
/// <c>Maj</c>, le trait se tient à 0, 45 ou 90° autour de l'extrémité qui reste.
///
/// Le survol, lui, n'a rien à apprendre ici : il passe par <see cref="Frame.HitTest"/>, qui
/// prend un trait à trois points de son encre (<see cref="ShapeFrame.ToleranceDeSurvol"/>).
/// </summary>
public partial class PageCanvas
{
    /// <summary>
    /// Le trait qu'on trace, de la pression au relâché : le pointillé d'accent du cadre
    /// qu'un autre outil trace. Posé dans l'Overlay à sa première sortie, hors de PageRoot.
    /// </summary>
    private readonly Line _traitEnCours = new()
    {
        Classes = { "traitEnCours" },
        StrokeDashArray = new AvaloniaList<double> { 4, 3 },
        IsVisible = false,
        IsHitTestVisible = false,
    };

    /// <summary>L'extrémité qui reste pendant qu'on tire l'autre, en points de page.</summary>
    private (double X, double Y) _extremiteFixe;

    /// <summary>Où était l'extrémité tirée à la prise, en points de page.</summary>
    private (double X, double Y) _extremiteTiree;

    /// <summary>Tire-t-on le début du trait, ou sa fin ?</summary>
    private bool _tireLeDebut;

    /// <summary>L'outil Ligne est armé : le geste trace un trait, et non un cadre.</summary>
    private bool TraceUnTrait => _vm?.ActiveTool == EditorTool.Line;

    /// <summary>
    /// Les poignées qu'un bloc présente : toutes, sauf pour un trait, qui n'a que ses deux
    /// extrémités.
    /// </summary>
    private static bool PoigneeServie(Frame bloc, Anchor poignee) =>
        !bloc.IsLine || poignee is Anchor.West or Anchor.East;

    /// <summary>Où se pose la poignée d'une extrémité, dans le repère du bloc : sur elle.</summary>
    private static (double X, double Y)? AncreDExtremite(Frame bloc, Anchor poignee) =>
        bloc.IsLine && poignee is Anchor.West or Anchor.East
            ? (poignee == Anchor.West ? 0 : bloc.Width, bloc.Height / 2)
            : null;

    // ------------------------------------------------------------------ tracer

    /// <summary>
    /// L'aperçu du trait qu'on trace, de la prise au pointeur — contraint sous <c>Maj</c> —,
    /// projeté sur les règles et mesuré dans la bulle. Faux si l'outil n'est pas Ligne : le
    /// cadre d'aperçu des autres outils prend alors le relais.
    /// </summary>
    private bool MontrerLeTraitEnCours(Point origine, Point pointeur, KeyModifiers modificateurs)
    {
        if (!TraceUnTrait) return false;

        if (_traitEnCours.Parent is null) Overlay.Children.Add(_traitEnCours);

        var fin = BoutDuTrait(origine, pointeur, modificateurs);
        _traitEnCours.StartPoint = new Point(origine.X * Scale, origine.Y * Scale);
        _traitEnCours.EndPoint = new Point(fin.X * Scale, fin.Y * Scale);
        _traitEnCours.IsVisible = true;

        var emprise = new Extent(
            Math.Min(origine.X, fin.X), Math.Min(origine.Y, fin.Y),
            Math.Max(origine.X, fin.X), Math.Max(origine.Y, fin.Y));
        Projeter(emprise);
        MontrerLaBulleDuTrait(emprise, origine.X, origine.Y, fin.X, fin.Y);
        return true;
    }

    private void CacherLeTraitEnCours() => _traitEnCours.IsVisible = false;

    /// <summary>
    /// Pose le trait tracé, au relâché. Faux si l'outil n'est pas Ligne : le tracé d'un
    /// cadre suit alors son cours.
    /// </summary>
    private bool FinirLeTrait(Point origine, Point pointeur, KeyModifiers modificateurs)
    {
        if (!TraceUnTrait || _vm is null) return false;

        var fin = BoutDuTrait(origine, pointeur, modificateurs);
        _vm.TracerUnTrait(origine.X, origine.Y, fin.X, fin.Y);
        return true;
    }

    /// <summary>Le bout du trait sous le pointeur : contraint à 0, 45 ou 90° sous <c>Maj</c>.</summary>
    private static Point BoutDuTrait(Point origine, Point pointeur, KeyModifiers modificateurs)
    {
        if (!modificateurs.HasFlag(KeyModifiers.Shift)) return pointeur;

        var (x, y) = ShapeFrame.Contraindre(origine.X, origine.Y, pointeur.X, pointeur.Y);
        return new Point(x, y);
    }

    // ------------------------------------------------------------------ reprendre

    /// <summary>Retient, à la prise d'une poignée d'extrémité, l'extrémité qui reste.</summary>
    private void PrendreUneExtremite(ShapeFrame trait, Anchor poignee)
    {
        _tireLeDebut = poignee == Anchor.West;
        _extremiteFixe = _tireLeDebut ? trait.Fin : trait.Debut;
        _extremiteTiree = _tireLeDebut ? trait.Debut : trait.Fin;
    }

    /// <summary>
    /// L'extrémité suit le chemin du pointeur depuis la prise — le chemin, et non la
    /// position, comme une poignée de cadre (poste I4). Sous <c>Maj</c>, le trait se tient à
    /// 0, 45 ou 90° autour de l'autre extrémité ; sinon l'extrémité s'aimante aux repères,
    /// sauf sous <c>Alt</c>, comme un bloc qu'on déplace.
    ///
    /// Un pointeur qui n'a pas quitté son point de prise ne change rien : le bloc retrouve
    /// exactement ce qu'il était, et le geste n'empile aucun pas — la leçon du poste Q14.
    /// Reposer le trait sur les mêmes extrémités l'aurait fait glisser d'un ulp, assez pour
    /// que le document diffère et qu'un pas d'annulation vide s'empile.
    /// </summary>
    private void TirerUneExtremite(ShapeFrame trait, Point page, KeyModifiers modificateurs)
    {
        var dx = page.X - _startPointer.X;
        var dy = page.Y - _startPointer.Y;

        if (dx == 0 && dy == 0)
        {
            trait.Rotation = _startRotation;
            trait.Width = _startWidth;
            trait.Height = _startHeight;
            trait.X = _startX;
            trait.Y = _startY;
            return;
        }

        var (x, y) = (_extremiteTiree.X + dx, _extremiteTiree.Y + dy);

        if (modificateurs.HasFlag(KeyModifiers.Shift))
        {
            (x, y) = ShapeFrame.Contraindre(_extremiteFixe.X, _extremiteFixe.Y, x, y);
        }
        else if (!modificateurs.HasFlag(KeyModifiers.Alt) && _vm is not null)
        {
            var tolerance = SnapPixels / Scale;
            x = Snap.Edge(x, 0, tolerance, _vm.Document.VerticalGuides);
            y = Snap.Edge(y, 0, tolerance, _vm.Document.HorizontalGuides);
        }

        if (_tireLeDebut) trait.PoserLesExtremites(x, y, _extremiteFixe.X, _extremiteFixe.Y);
        else trait.PoserLesExtremites(_extremiteFixe.X, _extremiteFixe.Y, x, y);
    }

    // ------------------------------------------------------------------ la bulle

    /// <summary>
    /// La bulle d'un trait dit sa longueur et son angle, et non une largeur et une hauteur
    /// qu'il n'a pas : « L 50,8 mm · 45° », dans la culture courante. Centrée sous
    /// l'emprise, comme celle d'un cadre.
    /// </summary>
    private void MontrerLaBulleDuTrait(Extent emprise, double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        // Arrondi avant d'être ramené dans [0, 360[ : 359,6° se lit 0°, et non 360°.
        var angle = dx == 0 && dy == 0 ? 0 : (Math.Round(Math.Atan2(dy, dx) * 180 / Math.PI) % 360 + 360) % 360;
        _texteDeLaBulle.Text = string.Format(CultureInfo.CurrentCulture,
            "L {0:0.0} mm · {1:0}°", Units.PointsToMm(Math.Sqrt(dx * dx + dy * dy)), angle);

        _bulle.IsVisible = true;
        _bulle.InvalidateMeasure();
        _bulle.Measure(Size.Infinity);
        Canvas.SetLeft(_bulle, emprise.CenterX * Scale - _bulle.DesiredSize.Width / 2);
        Canvas.SetTop(_bulle, emprise.Bottom * Scale + EcartDeLaBulle);
    }
}
