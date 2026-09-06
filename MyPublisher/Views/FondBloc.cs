using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using MyPublisher.Converters;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Dessine un remplissage : celui d'un bloc, celui d'une case, ou celui de la page.
///
/// Pourquoi un contrôle dessiné plutôt qu'un <c>Rectangle</c> muni d'un pinceau, comme
/// jusqu'au poste C5c. Parce que trois des quatre sortes de remplissage se ramènent à un
/// pinceau et que la quatrième ne s'y ramène pas : une image que Publisher fait pivoter
/// dans son cadre — une des cinq d'ATA 21 — demande de tourner le repère avant de poser
/// l'image, ce qu'un <c>ImageBrush</c> ne sait pas faire sans qu'on lui compose à la main
/// une rotation et une mise à l'échelle qui dépendent toutes deux de la taille du bloc.
///
/// Ce qui est dessiné ici l'est donc exactement comme <c>PdfExporter.Remplir</c> le
/// dessine : même découpe, même quart de tour, même cadre — et surtout, mêmes calculs,
/// pris dans le modèle. L'écran ne projette pas un dégradé et l'export non plus : les
/// deux lisent <see cref="GradientFill.Extremites"/>. C'est ce que le dépôt exige de tout
/// ce qui est dessiné deux fois.
///
/// Un seul contrôle pour les cinq gabarits de bloc et pour la page : c'est ce qui garantit
/// qu'un fond de page dégradé et un bloc dégradé se rendent pareil.
/// </summary>
public sealed class FondBloc : Control
{
    public static readonly StyledProperty<FillBrush?> FondProperty =
        AvaloniaProperty.Register<FondBloc, FillBrush?>(nameof(Fond));

    /// <summary>Rayon des angles, en points ; sans effet sur une ellipse.</summary>
    public static readonly StyledProperty<double> ArrondiProperty =
        AvaloniaProperty.Register<FondBloc, double>(nameof(Arrondi));

    public static readonly StyledProperty<bool> EllipseProperty =
        AvaloniaProperty.Register<FondBloc, bool>(nameof(Ellipse));

    static FondBloc()
    {
        AffectsRender<FondBloc>(FondProperty, ArrondiProperty, EllipseProperty);
        FondProperty.Changed.AddClassHandler<FondBloc>((c, e) => c.Suivre(e));
    }

    /// <summary>
    /// On s'abonne au remplissage lui-même, et pas seulement à la propriété qui le porte.
    ///
    /// C'est le défaut que la relecture du 6 septembre 2026 a mis quatre postes à trouver,
    /// sous une autre forme : « AffectsRender » ne se déclenche que si la VALEUR change.
    /// Or régler l'angle d'un dégradé modifie l'objet sur place — l'instance reste la
    /// même, la liaison ne rend rien de neuf, et la page ne se repeindrait pas.
    /// </summary>
    private void Suivre(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is FillBrush ancien) ancien.PropertyChanged -= SurChangement;
        // Hors de l'arbre visuel, on ne s'abonne pas : c'est l'attachement qui le fera.
        if (e.NewValue is FillBrush nouveau && this.GetVisualRoot() is not null)
        {
            nouveau.PropertyChanged += SurChangement;
        }
    }

    /// <summary>
    /// L'abonnement ne vaut que tant que le contrôle est affiché.
    ///
    /// Un remplissage vit bien plus longtemps que le contrôle qui le dessine : changer de
    /// page reconstruit tous les gabarits, et un contrôle qui ne se désabonnerait pas
    /// resterait accroché au bloc qu'il n'affiche plus. C'est exactement la fuite trouvée
    /// au même poste sur les cases d'un tableau, et il n'y avait pas de raison de la
    /// laisser ici. Mesuré : vingt passages, vingt abonnés.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Fond is not { } fond) return;

        // Retirer avant d'ajouter : deux attachements sans détachement entre eux ne
        // doivent pas compter double.
        fond.PropertyChanged -= SurChangement;
        fond.PropertyChanged += SurChangement;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Fond is { } fond) fond.PropertyChanged -= SurChangement;
    }

    private void SurChangement(object? _, System.ComponentModel.PropertyChangedEventArgs __) =>
        InvalidateVisual();

    public FillBrush? Fond
    {
        get => GetValue(FondProperty);
        set => SetValue(FondProperty, value);
    }

    public double Arrondi
    {
        get => GetValue(ArrondiProperty);
        set => SetValue(ArrondiProperty, value);
    }

    public bool Ellipse
    {
        get => GetValue(EllipseProperty);
        set => SetValue(EllipseProperty, value);
    }

    public override void Render(DrawingContext contexte)
    {
        if (Fond is not { } fond) return;

        var largeur = Bounds.Width;
        var hauteur = Bounds.Height;
        if (largeur <= 0 || hauteur <= 0) return;

        if (fond is ImageFill image)
        {
            RemplirParImage(contexte, image, largeur, hauteur);
            return;
        }

        var pinceau = Pinceau(fond, largeur, hauteur);
        if (pinceau is null) return;

        var rect = new Rect(0, 0, largeur, hauteur);
        if (Ellipse) contexte.DrawEllipse(pinceau, null, rect.Center, largeur / 2, hauteur / 2);
        else contexte.DrawRectangle(pinceau, null, rect, Arrondi, Arrondi);
    }

    /// <summary>
    /// Le pinceau des trois sortes qui en ont un. Les extrémités du dégradé viennent du
    /// modèle, en points ; Avalonia les veut en unités absolues du repère du contrôle,
    /// ce qui est exactement la même chose ici — une unité vaut un point sous la mise à
    /// l'échelle du zoom.
    /// </summary>
    private static IBrush? Pinceau(FillBrush fond, double largeur, double hauteur)
    {
        if (fond is not GradientFill degrade) return Conv.ToBrush(fond.Uniforme);

        var depart = Conv.ToColor(degrade.From);
        var arrivee = Conv.ToColor(degrade.To);
        if (depart is null && arrivee is null) return null;

        var (x0, y0, x1, y1) = degrade.Extremites(largeur, hauteur);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(x0, y0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(x1, y1, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(depart ?? arrivee!.Value, 0),
                new GradientStop(arrivee ?? depart!.Value, 1),
            },
        };
    }

    /// <summary>
    /// L'image, découpée au pourtour puis posée dans le cadre que le modèle a calculé.
    /// La découpe précède la rotation, comme à l'export : c'est le bloc qui découpe, et
    /// lui ne tourne pas.
    /// </summary>
    private void RemplirParImage(DrawingContext contexte, ImageFill fond, double largeur, double hauteur)
    {
        if (string.IsNullOrWhiteSpace(fond.Path)) return;
        if (Conv.Bitmap(fond.Path) is not { } bitmap) return;

        var rect = new Rect(0, 0, largeur, hauteur);
        var pourtour = Ellipse
            ? new EllipseGeometry(rect)
            : (Geometry)new RectangleGeometry(rect, Arrondi, Arrondi);

        using var decoupe = contexte.PushGeometryClip(pourtour);
        using var tour = fond.Rotation == 0
            ? (IDisposable)new SansTransformation()
            : contexte.PushTransform(
                Matrix.CreateTranslation(-largeur / 2, -hauteur / 2) *
                Matrix.CreateRotation(fond.Rotation * Math.PI / 180) *
                Matrix.CreateTranslation(largeur / 2, hauteur / 2));

        var (x, y, l, h) = fond.Cadre(largeur, hauteur, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        contexte.DrawImage(bitmap, new Rect(x, y, l, h));
    }

    /// <summary>De quoi écrire « using » sans transformation, comme le fait la vignette.</summary>
    private sealed class SansTransformation : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
