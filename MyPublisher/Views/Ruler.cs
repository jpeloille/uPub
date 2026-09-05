using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Règle graduée bordant la planche. Elle est dessinée plutôt que composée : une
/// graduation au millimètre sur une page A4 fait près de trois cents traits, et autant
/// de contrôles coûteraient cher pour un décor qui ne reçoit qu'un clic.
///
/// Les graduations sont en millimètres, comme les champs de position du panneau et la
/// barre d'état. Le zéro est le coin de la page, pas celui de la fenêtre : c'est la
/// page qu'on mesure.
/// </summary>
public class Ruler : Control
{
    /// <summary>Épaisseur de la règle, en pixels écran — elle ne suit pas le zoom.</summary>
    public const double Breadth = 18;

    public static readonly StyledProperty<double> PageLengthProperty =
        AvaloniaProperty.Register<Ruler, double>(nameof(PageLength), 595.28);

    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<Ruler, double>(nameof(Scale), 1.0);

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<Ruler, Orientation>(nameof(Orientation), Orientation.Horizontal);

    static Ruler()
    {
        AffectsMeasure<Ruler>(PageLengthProperty, ScaleProperty, OrientationProperty);
        AffectsRender<Ruler>(PageLengthProperty, ScaleProperty, OrientationProperty);
    }

    /// <summary>Longueur de la page mesurée par cette règle, en points.</summary>
    public double PageLength
    {
        get => GetValue(PageLengthProperty);
        set => SetValue(PageLengthProperty, value);
    }

    /// <summary>Facteur points → pixels écran, zoom compris.</summary>
    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    private static readonly IBrush Face = new SolidColorBrush(Color.Parse("#FFF3F4F6"));
    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#FF5A6472"));
    private static readonly IPen Edge = new Pen(new SolidColorBrush(Color.Parse("#FFD9DDE3")));
    private static readonly IPen Graduation = new Pen(new SolidColorBrush(Color.Parse("#FF8A929E")), 1);

    private bool Horizontal => Orientation == Orientation.Horizontal;

    protected override Size MeasureOverride(Size availableSize)
    {
        var length = Math.Max(0, PageLength * Scale);
        return Horizontal ? new Size(length, Breadth) : new Size(Breadth, length);
    }

    /// <summary>Convertit une position de page, en points, vers un pixel de la règle.</summary>
    public double ToScreen(double points) => points * Scale;

    /// <summary>Et l'inverse : ce que le pointeur désigne, en points de page.</summary>
    public double ToPage(double screen) => Scale <= 0 ? 0 : screen / Scale;

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Face, new Rect(size));

        // Le filet du bord jouxte la page : c'est lui qui fait lire la règle comme
        // adossée à la planche plutôt que posée à côté.
        context.DrawLine(Edge,
            Horizontal ? new Point(0, size.Height - 0.5) : new Point(size.Width - 0.5, 0),
            Horizontal ? new Point(size.Width, size.Height - 0.5) : new Point(size.Width - 0.5, size.Height));

        var millimetre = Units.MmToPoints(1) * Scale;
        if (millimetre <= 0) return;

        // Sous trois pixels, la graduation au millimètre devient une bouillie : on passe
        // au pas de cinq, puis de dix. La règle reste lisible à tous les zooms.
        var step = millimetre >= 3 ? 1 : millimetre * 5 >= 3 ? 5 : 10;

        // Un nombre tous les dix millimètres tant qu'il y a la place de l'écrire.
        var labelStep = millimetre * 10 >= 26 ? 10 : millimetre * 50 >= 26 ? 50 : 100;

        var lengthMm = Units.PointsToMm(PageLength);
        for (var mm = 0; mm <= lengthMm + 0.001; mm += step)
        {
            var at = Math.Round(ToScreen(Units.MmToPoints(mm))) + 0.5;
            var labelled = mm % labelStep == 0;
            var tick = labelled ? Breadth * 0.55 : mm % (step * 5) == 0 ? Breadth * 0.38 : Breadth * 0.22;

            context.DrawLine(Graduation,
                Horizontal ? new Point(at, size.Height - tick) : new Point(size.Width - tick, at),
                Horizontal ? new Point(at, size.Height) : new Point(size.Width, at));

            if (!labelled || mm == 0) continue;

            var text = new FormattedText(
                mm.ToString(CultureInfo.CurrentCulture), CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 9, Ink);

            // Le nombre se pose contre son trait, du côté où il reste de la place.
            context.DrawText(text, Horizontal
                ? new Point(at + 2, 1)
                : new Point(1, at + 2));
        }
    }
}
