using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Dessine un trait et ses pointes de flèche, à l'écran — poste A10.
///
/// Le tracé est celui du modèle (<see cref="ShapeFrame.Tracer"/>), que l'export lit aussi :
/// même fût raccourci sous ses pointes, mêmes triangles, au même endroit. Le contrôle est
/// posé dans le panneau du bloc, qui porte la rotation : il dessine le trait couché, au
/// milieu de sa hauteur, comme l'export le dessine sous la même rotation.
///
/// Il ne tient pas le bloc : il reçoit par liaison les seules grandeurs qu'il dessine, et
/// redessine quand l'une change. Sans abonnement au modèle, il n'a rien à lâcher en
/// quittant l'arbre — c'est la fuite que le poste Q2 a refermée ailleurs. Sa longueur et sa
/// hauteur sont celles du panneau, donc du bloc.
/// </summary>
public sealed class TraitBloc : Control
{
    /// <summary>La couleur du trait, résolue dans le jeu ; nulle, rien ne se dessine.</summary>
    public static readonly StyledProperty<IBrush?> CouleurProperty =
        AvaloniaProperty.Register<TraitBloc, IBrush?>(nameof(Couleur));

    public static readonly StyledProperty<double> EpaisseurProperty =
        AvaloniaProperty.Register<TraitBloc, double>(nameof(Epaisseur), 1);

    public static readonly StyledProperty<StrokeStyle> MotifProperty =
        AvaloniaProperty.Register<TraitBloc, StrokeStyle>(nameof(Motif));

    public static readonly StyledProperty<bool> PointeAuDebutProperty =
        AvaloniaProperty.Register<TraitBloc, bool>(nameof(PointeAuDebut));

    public static readonly StyledProperty<bool> PointeALaFinProperty =
        AvaloniaProperty.Register<TraitBloc, bool>(nameof(PointeALaFin));

    static TraitBloc()
    {
        AffectsRender<TraitBloc>(CouleurProperty, EpaisseurProperty, MotifProperty,
            PointeAuDebutProperty, PointeALaFinProperty);
    }

    public IBrush? Couleur
    {
        get => GetValue(CouleurProperty);
        set => SetValue(CouleurProperty, value);
    }

    public double Epaisseur
    {
        get => GetValue(EpaisseurProperty);
        set => SetValue(EpaisseurProperty, value);
    }

    public StrokeStyle Motif
    {
        get => GetValue(MotifProperty);
        set => SetValue(MotifProperty, value);
    }

    public bool PointeAuDebut
    {
        get => GetValue(PointeAuDebutProperty);
        set => SetValue(PointeAuDebutProperty, value);
    }

    public bool PointeALaFin
    {
        get => GetValue(PointeALaFinProperty);
        set => SetValue(PointeALaFinProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        // Les mêmes conditions que le crayon de l'export : sans couleur ou sans épaisseur,
        // ni trait ni pointe.
        if (Couleur is not { } couleur || Epaisseur <= 0) return;

        // Les tirets comptent en multiples de l'épaisseur, des deux côtés : la table du
        // modèle sert telle quelle, comme pour le contour de tout bloc. Bouts carrés, comme
        // le crayon par défaut de PDFsharp.
        var motif = Frame.DashPattern(Motif) is { } tirets ? new DashStyle(tirets, 0) : null;
        var crayon = new Pen(couleur, Epaisseur, motif, PenLineCap.Flat);

        Tracer(context, ShapeFrame.Tracer(Bounds.Width, Bounds.Height, Epaisseur, PointeAuDebut, PointeALaFin),
            crayon, couleur, default);
    }

    /// <summary>
    /// Trace un dessin de trait, décalé de <paramref name="origine"/> : le coin du bloc,
    /// quand on dessine dans le repère de la page — c'est ce que font les vignettes de la
    /// page et des modèles. Les pointes sont pleines, de la couleur donnée.
    /// </summary>
    public static void Tracer(DrawingContext context, DessinDuTrait dessin, IPen crayon, IBrush pointes, Point origine)
    {
        context.DrawLine(crayon, origine + new Point(dessin.X1, dessin.Y1), origine + new Point(dessin.X2, dessin.Y2));

        foreach (var pointe in new[] { dessin.PointeAuDebut, dessin.PointeALaFin })
        {
            if (pointe is null) continue;

            var geometrie = new PolylineGeometry(
                pointe.Select(p => origine + new Point(p.X, p.Y)).ToList(), isFilled: true);
            context.DrawGeometry(pointes, null, geometrie);
        }
    }
}
