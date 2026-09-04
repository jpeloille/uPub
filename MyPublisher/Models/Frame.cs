using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

public enum TextAlign { Left, Center, Right, Justify }

public enum ImageFit { Uniform, UniformToFill, Fill }

public enum ShapeKind { Rectangle, Ellipse }

/// <summary>
/// Rectangle englobant en coordonnées page, bords parallèles à la page. C'est la forme
/// sous laquelle on compare des blocs entre eux — alignement, répartition, aimantation
/// d'un ensemble — parce qu'un bloc tourné occupe autre chose que son cadre.
/// </summary>
public readonly record struct Extent(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;
    public double CenterX => (Left + Right) / 2;
    public double CenterY => (Top + Bottom) / 2;

    /// <summary>Plus petit rectangle contenant tous les autres ; nul si la suite est vide.</summary>
    public static Extent? Union(IEnumerable<Extent> extents)
    {
        Extent? union = null;
        foreach (var extent in extents)
        {
            union = union is not { } current
                ? extent
                : new Extent(
                    Math.Min(current.Left, extent.Left), Math.Min(current.Top, extent.Top),
                    Math.Max(current.Right, extent.Right), Math.Max(current.Bottom, extent.Bottom));
        }

        return union;
    }
}

/// <summary>
/// Bloc positionné sur une page. Toutes les coordonnées sont en points, l'origine
/// est le coin supérieur gauche de la page, l'axe Y descend (comme en PDF).
/// Ce modèle ne référence aucun type Avalonia : il est partagé tel quel par le
/// rendu écran et par l'export PDF.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(TextFrame), "text")]
[JsonDerivedType(typeof(ImageFrame), "image")]
[JsonDerivedType(typeof(ShapeFrame), "shape")]
public abstract partial class Frame : ObservableObject
{
    // L'ordre d'écriture est explicite d'un bout à l'autre du modèle. Sans lui, le
    // sérialiseur le déduit des classes partielles qu'engendre [ObservableProperty] :
    // les propriétés propres au type sortaient avant les coordonnées, et le titre du
    // document après ses pages. Un format qui se relit se lit dans l'ordre où on
    // pense — le bloc d'abord, ses réglages ensuite. Les rangs vont de dix en dix :
    // remonter une propriété d'une classe à l'autre n'oblige pas à tout renuméroter.
    [ObservableProperty]
    [property: JsonPropertyOrder(10)]
    private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Right))]
    [property: JsonPropertyOrder(20)]
    private double _x;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bottom))]
    [property: JsonPropertyOrder(30)]
    private double _y;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Right))]
    [property: JsonPropertyOrder(40)]
    private double _width = 200;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bottom))]
    [property: JsonPropertyOrder(50)]
    private double _height = 100;

    /// <summary>Rotation en degrés, sens horaire, autour du centre du bloc.</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(60)]
    private double _rotation;

    [JsonIgnore] public double Right => X + Width;
    [JsonIgnore] public double Bottom => Y + Height;
    [JsonIgnore] public double CenterX => X + Width / 2;
    [JsonIgnore] public double CenterY => Y + Height / 2;

    /// <summary>
    /// Ce que le bloc occupe réellement sur la page, rotation comprise. Un bloc tourné
    /// déborde de son cadre : c'est cette empreinte-là qu'on aligne et qu'on aimante,
    /// pas les X et Y bruts.
    /// </summary>
    [JsonIgnore]
    public Extent Extent
    {
        get
        {
            if (Rotation == 0) return new Extent(X, Y, X + Width, Y + Height);

            var corners = new[]
            {
                ToPage(0, 0), ToPage(Width, 0), ToPage(Width, Height), ToPage(0, Height)
            };

            return new Extent(
                corners.Min(c => c.X), corners.Min(c => c.Y),
                corners.Max(c => c.X), corners.Max(c => c.Y));
        }
    }

    /// <summary>Libellé affiché dans le panneau des calques.</summary>
    [JsonIgnore] public abstract string Kind { get; }

    [JsonIgnore] public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Kind : Name;

    /// <summary>
    /// Teste si un point (en coordonnées document) tombe dans le bloc, en tenant
    /// compte de la rotation : on ramène le point dans le repère local du bloc.
    /// </summary>
    public bool HitTest(double px, double py)
    {
        var (lx, ly) = ToLocal(px, py);
        return lx >= 0 && ly >= 0 && lx <= Width && ly <= Height;
    }

    /// <summary>Convertit un point du repère page vers le repère local (non tourné) du bloc.</summary>
    public (double X, double Y) ToLocal(double px, double py)
    {
        if (Rotation == 0) return (px - X, py - Y);

        var rad = -Rotation * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var dx = px - CenterX;
        var dy = py - CenterY;
        return (dx * cos - dy * sin + Width / 2, dx * sin + dy * cos + Height / 2);
    }

    /// <summary>Convertit un point du repère local du bloc vers le repère page.</summary>
    public (double X, double Y) ToPage(double lx, double ly)
    {
        if (Rotation == 0) return (X + lx, Y + ly);

        var rad = Rotation * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var dx = lx - Width / 2;
        var dy = ly - Height / 2;
        return (dx * cos - dy * sin + CenterX, dx * sin + dy * cos + CenterY);
    }

    public abstract Frame Clone();

    protected void CopyBaseTo(Frame target)
    {
        target.Name = Name;
        target.X = X;
        target.Y = Y;
        target.Width = Width;
        target.Height = Height;
        target.Rotation = Rotation;
    }
}

public partial class TextFrame : Frame
{
    [ObservableProperty]
    [property: JsonPropertyOrder(100)]
    private string _text = "";

    [ObservableProperty]
    [property: JsonPropertyOrder(110)]
    private string _fontFamily = "DejaVu Sans";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AbsoluteLineHeight))]
    [property: JsonPropertyOrder(120)]
    private double _fontSize = 11;

    [ObservableProperty]
    [property: JsonPropertyOrder(130)]
    private bool _bold;

    [ObservableProperty]
    [property: JsonPropertyOrder(140)]
    private bool _italic;

    [ObservableProperty]
    [property: JsonPropertyOrder(150)]
    private string _foreground = "#FF1A1A1A";

    /// <summary>
    /// Fond du bloc, en #AARRGGBB. Null signifie « aucun fond » : ce qui passe sous le
    /// bloc reste visible. Un fond blanc, lui, est un aplat opaque qui masque. Les deux
    /// ne se confondent pas, même posés sur une page blanche — la différence se voit dès
    /// qu'un bloc passe dessous, et elle est conservée à l'export.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackground))]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    [property: JsonPropertyOrder(160)]
    private string? _background;

    /// <summary>
    /// Teinte retenue pendant qu'aucun fond n'est appliqué, pour que retirer puis
    /// remettre le fond retrouve la couleur choisie au lieu d'un blanc neuf.
    /// État transitoire d'interface : jamais sérialisé.
    /// </summary>
    private string _lastBackground = "#FFFFFFFF";

    [ObservableProperty]
    [property: JsonPropertyOrder(170)]
    private TextAlign _align = TextAlign.Left;

    /// <summary>Interligne exprimé en multiple du corps.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AbsoluteLineHeight))]
    [property: JsonPropertyOrder(180)]
    private double _lineHeight = 1.25;

    /// <summary>Marge intérieure du bloc, en points.</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(190)]
    private double _padding = 4;

    /// <summary>Édition en cours : état transitoire d'interface, jamais sérialisé.</summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private bool _isEditing;

    /// <summary>
    /// Présence d'un fond, telle que la manipule le panneau de propriétés. Décocher
    /// efface le fond sans perdre la teinte, recocher la restitue.
    /// </summary>
    [JsonIgnore]
    public bool HasBackground
    {
        get => Background is not null;
        set
        {
            if (value == HasBackground) return;
            Background = value ? _lastBackground : null;
        }
    }

    /// <summary>
    /// Teinte présentée au sélecteur de couleur, toujours définie même quand le bloc
    /// n'a pas de fond. Le sélecteur renvoie vers la source la valeur qu'il vient d'en
    /// lire dès qu'il s'affiche : le brancher directement sur <see cref="Background"/>
    /// donnerait un fond transparent parasite au premier bloc sélectionné, et la case
    /// « fond » se cocherait d'elle-même. On sépare donc la teinte de sa présence.
    /// </summary>
    [JsonIgnore]
    public string BackgroundColor
    {
        get => Background ?? _lastBackground;
        set
        {
            if (value == BackgroundColor) return;

            _lastBackground = value;
            // Choisir une teinte ne crée pas un fond : seule la case le fait.
            if (HasBackground) Background = value;
            else OnPropertyChanged();
        }
    }

    partial void OnBackgroundChanged(string? value)
    {
        if (value is not null) _lastBackground = value;
    }

    [JsonIgnore] public double AbsoluteLineHeight => FontSize * LineHeight;

    [JsonIgnore] public override string Kind => "Texte";

    public override Frame Clone()
    {
        var c = new TextFrame
        {
            Text = Text, FontFamily = FontFamily, FontSize = FontSize, Bold = Bold,
            Italic = Italic, Foreground = Foreground, Background = Background, Align = Align,
            LineHeight = LineHeight, Padding = Padding
        };
        CopyBaseTo(c);
        return c;
    }
}

public partial class ImageFrame : Frame
{
    [ObservableProperty]
    [property: JsonPropertyOrder(100)]
    private string _path = "";

    [ObservableProperty]
    [property: JsonPropertyOrder(110)]
    private ImageFit _fit = ImageFit.Uniform;

    [JsonIgnore] public override string Kind => "Image";

    public override Frame Clone()
    {
        var c = new ImageFrame { Path = Path, Fit = Fit };
        CopyBaseTo(c);
        return c;
    }
}

public partial class ShapeFrame : Frame
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRectangle))]
    [NotifyPropertyChangedFor(nameof(IsEllipse))]
    [property: JsonPropertyOrder(100)]
    private ShapeKind _shape = ShapeKind.Rectangle;

    [ObservableProperty]
    [property: JsonPropertyOrder(110)]
    private string? _fill = "#FF9DC3E6";

    [ObservableProperty]
    [property: JsonPropertyOrder(120)]
    private string? _stroke = "#FF2E5C8A";

    [ObservableProperty]
    [property: JsonPropertyOrder(130)]
    private double _strokeThickness = 1;

    [ObservableProperty]
    [property: JsonPropertyOrder(140)]
    private double _cornerRadius;

    [JsonIgnore] public bool IsRectangle => Shape == ShapeKind.Rectangle;
    [JsonIgnore] public bool IsEllipse => Shape == ShapeKind.Ellipse;

    [JsonIgnore] public override string Kind => Shape == ShapeKind.Ellipse ? "Ellipse" : "Rectangle";

    public override Frame Clone()
    {
        var c = new ShapeFrame
        {
            Shape = Shape, Fill = Fill, Stroke = Stroke,
            StrokeThickness = StrokeThickness, CornerRadius = CornerRadius
        };
        CopyBaseTo(c);
        return c;
    }
}
