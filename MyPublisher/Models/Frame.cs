using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

public enum TextAlign { Left, Center, Right, Justify }

public enum ImageFit { Uniform, UniformToFill, Fill }

public enum ShapeKind { Rectangle, Ellipse }

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
    [ObservableProperty] private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Right))]
    private double _x;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bottom))]
    private double _y;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Right))]
    private double _width = 200;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bottom))]
    private double _height = 100;

    /// <summary>Rotation en degrés, sens horaire, autour du centre du bloc.</summary>
    [ObservableProperty] private double _rotation;

    [JsonIgnore] public double Right => X + Width;
    [JsonIgnore] public double Bottom => Y + Height;
    [JsonIgnore] public double CenterX => X + Width / 2;
    [JsonIgnore] public double CenterY => Y + Height / 2;

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
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _fontFamily = "DejaVu Sans";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AbsoluteLineHeight))]
    private double _fontSize = 11;

    [ObservableProperty] private bool _bold;
    [ObservableProperty] private bool _italic;
    [ObservableProperty] private string _foreground = "#FF1A1A1A";

    /// <summary>
    /// Fond du bloc, en #AARRGGBB. Null signifie « aucun fond » : ce qui passe sous le
    /// bloc reste visible. Un fond blanc, lui, est un aplat opaque qui masque. Les deux
    /// ne se confondent pas, même posés sur une page blanche — la différence se voit dès
    /// qu'un bloc passe dessous, et elle est conservée à l'export.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackground))]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    private string? _background;

    /// <summary>
    /// Teinte retenue pendant qu'aucun fond n'est appliqué, pour que retirer puis
    /// remettre le fond retrouve la couleur choisie au lieu d'un blanc neuf.
    /// État transitoire d'interface : jamais sérialisé.
    /// </summary>
    private string _lastBackground = "#FFFFFFFF";

    [ObservableProperty] private TextAlign _align = TextAlign.Left;

    /// <summary>Interligne exprimé en multiple du corps.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AbsoluteLineHeight))]
    private double _lineHeight = 1.25;

    /// <summary>Marge intérieure du bloc, en points.</summary>
    [ObservableProperty] private double _padding = 4;

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
    [ObservableProperty] private string _path = "";
    [ObservableProperty] private ImageFit _fit = ImageFit.Uniform;

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
    private ShapeKind _shape = ShapeKind.Rectangle;

    [ObservableProperty] private string? _fill = "#FF9DC3E6";
    [ObservableProperty] private string? _stroke = "#FF2E5C8A";
    [ObservableProperty] private double _strokeThickness = 1;
    [ObservableProperty] private double _cornerRadius;

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
