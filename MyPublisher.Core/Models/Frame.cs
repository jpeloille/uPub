using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

public enum TextAlign { Left, Center, Right, Justify }

public enum ImageFit { Uniform, UniformToFill, Fill }

public enum ShapeKind { Rectangle, Ellipse }

/// <summary>Continu, tirets ou points. Les motifs sont en multiples de l'épaisseur.</summary>
public enum StrokeStyle { Solid, Dashed, Dotted }

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
/// Marges intérieures d'un bloc de texte, côté par côté, en points. Le modèle les
/// stocke une à une pour que le panneau les lie séparément ; la vue et l'export les
/// consomment d'un seul tenant, sous cette forme.
/// </summary>
public readonly record struct Insets(double Left, double Top, double Right, double Bottom);

/// <summary>
/// Le découpage d'un bloc de texte en colonnes : leur nombre et la gouttière qui les
/// sépare, en points. Un nombre et une distance dans le même type parce qu'ils ne se
/// lisent jamais l'un sans l'autre, et parce que deux <c>double</c> voisins dans une
/// liste de paramètres se laissent intervertir sans que rien ne le dise.
///
/// C'est le pendant, pour le bloc, de <c>fo:column-count</c> et <c>fo:column-gap</c>.
/// À ne pas confondre avec les colonnes du document (poste C2a), qui sont des repères
/// d'aimantation posés sur la page : ici le texte coule d'une colonne à l'autre à
/// l'intérieur d'un seul bloc.
/// </summary>
public readonly record struct Colonnage(int Nombre, double Gouttiere)
{
    /// <summary>
    /// Largeur d'une colonne dans un bloc large de <paramref name="largeur"/>. Une
    /// gouttière plus large que le bloc rendrait une largeur négative : on laisse un
    /// point plutôt que de refuser de composer, même règle qu'un retrait trop grand.
    /// </summary>
    public double Largeur(double largeur) =>
        Math.Max(1, (largeur - (Math.Max(1, Nombre) - 1) * Gouttiere) / Math.Max(1, Nombre));

    /// <summary>D'un bord gauche de colonne au suivant.</summary>
    public double Pas(double largeur) => Largeur(largeur) + Gouttiere;
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
[JsonDerivedType(typeof(GroupFrame), "group")]
public abstract partial class Frame : ObservableObject
{
    // L'ordre d'écriture est explicite d'un bout à l'autre du modèle. Sans lui, le
    // sérialiseur le déduit des classes partielles qu'engendre [ObservableProperty] :
    // les propriétés propres au type sortaient avant les coordonnées, et le titre du
    // document après ses pages. Un format qui se relit se lit dans l'ordre où on
    // pense — le bloc d'abord, ses réglages ensuite. Les rangs vont de dix en dix :
    // remonter une propriété d'une classe à l'autre n'oblige pas à tout renuméroter.
    // Le fond et le contour, remontés de la forme vers le bloc, tiennent de 70 à 90 ;
    // les propriétés propres à chaque type commencent à cent.
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

    /// <summary>
    /// Fond du bloc, en #AARRGGBB. Null signifie « aucun fond » : ce qui passe sous le
    /// bloc reste visible. Un fond blanc, lui, est un aplat opaque qui masque. Les deux
    /// ne se confondent pas, même posés sur une page blanche — la différence se voit dès
    /// qu'un bloc passe dessous, et elle est conservée à l'export. Commun à tous les
    /// blocs : une image ou un texte se remplit comme une forme.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFill))]
    [NotifyPropertyChangedFor(nameof(FillColor))]
    [NotifyPropertyChangedFor(nameof(FillOpacity))]
    [property: JsonPropertyOrder(70)]
    private string? _fill;

    /// <summary>Contour du bloc, en #AARRGGBB ; null pour aucun contour.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStroke))]
    [NotifyPropertyChangedFor(nameof(StrokeColor))]
    [property: JsonPropertyOrder(75)]
    private string? _stroke;

    /// <summary>Épaisseur du contour en points, centrée sur le bord du bloc.</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(80)]
    private double _strokeThickness = 1;

    [ObservableProperty]
    [property: JsonPropertyOrder(85)]
    private StrokeStyle _strokeStyle = StrokeStyle.Solid;

    /// <summary>Rayon des angles en points ; sans effet sur une ellipse.</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(90)]
    private double _cornerRadius;

    /// <summary>
    /// Teintes retenues pendant qu'aucun fond ou contour n'est appliqué, pour que
    /// retirer puis remettre retrouve la couleur choisie au lieu d'une valeur neuve.
    /// État transitoire d'interface : jamais sérialisé.
    /// </summary>
    private string _lastFill = "#FFFFFFFF";
    private string _lastStroke = "#FF1A1A1A";

    /// <summary>
    /// Présence d'un fond, telle que la manipule le panneau de propriétés. Décocher
    /// efface le fond sans perdre la teinte, recocher la restitue.
    /// </summary>
    [JsonIgnore]
    public bool HasFill
    {
        get => Fill is not null;
        set
        {
            if (value == HasFill) return;
            Fill = value ? _lastFill : null;
        }
    }

    /// <summary>
    /// Teinte présentée au sélecteur de couleur, toujours définie même quand le bloc
    /// n'a pas de fond. Le sélecteur renvoie vers la source la valeur qu'il vient d'en
    /// lire dès qu'il s'affiche : le brancher directement sur <see cref="Fill"/>
    /// donnerait un fond transparent parasite au premier bloc sélectionné, et la case
    /// « fond » se cocherait d'elle-même. On sépare donc la teinte de sa présence.
    /// </summary>
    [JsonIgnore]
    public string FillColor
    {
        get => Fill ?? _lastFill;
        set
        {
            if (value == FillColor) return;

            _lastFill = value;
            // Choisir une teinte ne crée pas un fond : seule la case le fait.
            if (HasFill) Fill = value;
            else
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(FillOpacity));
            }
        }
    }

    /// <summary>
    /// Opacité du fond en pour cent. Elle ne fait que lire et réécrire l'octet alpha de
    /// la teinte : c'est le même réglage que celui du sélecteur, offert là où on le
    /// cherche.
    /// </summary>
    [JsonIgnore]
    public double FillOpacity
    {
        get => HexColor.TryParse(FillColor, out var a, out _, out _, out _) ? Math.Round(a * 100.0 / 255) : 100;
        set
        {
            var alpha = (byte)Math.Round(Math.Clamp(value, 0, 100) * 255 / 100);
            FillColor = HexColor.WithAlpha(FillColor, alpha);
        }
    }

    [JsonIgnore]
    public bool HasStroke
    {
        get => Stroke is not null;
        set
        {
            if (value == HasStroke) return;
            Stroke = value ? _lastStroke : null;
        }
    }

    [JsonIgnore]
    public string StrokeColor
    {
        get => Stroke ?? _lastStroke;
        set
        {
            if (value == StrokeColor) return;

            _lastStroke = value;
            if (HasStroke) Stroke = value;
            else OnPropertyChanged();
        }
    }

    partial void OnFillChanged(string? value)
    {
        if (value is not null) _lastFill = value;
    }

    partial void OnStrokeChanged(string? value)
    {
        if (value is not null) _lastStroke = value;
    }

    /// <summary>
    /// Motif du trait en multiples de l'épaisseur, ou null pour un trait continu. Écran
    /// et export lisent la même table : les deux comptent en multiples de l'épaisseur,
    /// un tiret de quatre fois l'épaisseur est donc le même des deux côtés.
    /// </summary>
    public static double[]? DashPattern(StrokeStyle style) => style switch
    {
        StrokeStyle.Dashed => [4, 2],
        StrokeStyle.Dotted => [1, 2],
        _ => null
    };

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

    partial void OnWidthChanged(double oldValue, double newValue) =>
        OnResized(oldValue, newValue, horizontal: true);

    partial void OnHeightChanged(double oldValue, double newValue) =>
        OnResized(oldValue, newValue, horizontal: false);

    /// <summary>
    /// Appelé quand le bloc change de taille. Un bloc ordinaire n'en fait rien ; un
    /// groupe s'en sert pour suivre avec les blocs qu'il contient.
    /// </summary>
    protected virtual void OnResized(double oldSize, double newSize, bool horizontal)
    {
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
        target.Fill = Fill;
        target.Stroke = Stroke;
        target.StrokeThickness = StrokeThickness;
        target.StrokeStyle = StrokeStyle;
        target.CornerRadius = CornerRadius;
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
    /// Portions de texte qui s'écartent du style du bloc. Vide — le cas de tous les
    /// documents écrits jusqu'ici — veut dire que le bloc est d'un seul style, et rien
    /// ne change pour lui : c'est pourquoi cette addition ne demande aucune migration
    /// et laisse <c>FormatVersion</c> à 3.
    /// </summary>
    [JsonPropertyOrder(160)]
    public ObservableCollection<StyleSpan> Spans { get; set; } = [];

    [ObservableProperty]
    [property: JsonPropertyOrder(170)]
    private TextAlign _align = TextAlign.Left;

    /// <summary>Interligne exprimé en multiple du corps.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AbsoluteLineHeight))]
    [property: JsonPropertyOrder(180)]
    private double _lineHeight = 1.25;

    /// <summary>
    /// Marges intérieures du bloc, en points, une par côté. Quatre valeurs et non une :
    /// un texte qui doit respirer à gauche d'un filet n'a pas à s'écarter aussi du haut.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Padding))]
    [property: JsonPropertyOrder(190)]
    private double _paddingLeft = 4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Padding))]
    [property: JsonPropertyOrder(200)]
    private double _paddingTop = 4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Padding))]
    [property: JsonPropertyOrder(210)]
    private double _paddingRight = 4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Padding))]
    [property: JsonPropertyOrder(220)]
    private double _paddingBottom = 4;

    /// <summary>Les quatre marges d'un seul tenant, pour la vue et l'export.</summary>
    [JsonIgnore] public Insets Padding => new(PaddingLeft, PaddingTop, PaddingRight, PaddingBottom);

    /// <summary>
    /// Champ éventuel. Renseigné, il fait calculer le texte affiché au lieu de le lire
    /// dans <see cref="Text"/> — qui garde pour autant sa valeur, pour la retrouver
    /// intacte le jour où l'on retire le champ.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasField))]
    [property: JsonPropertyOrder(230)]
    private FieldKind _field = FieldKind.None;

    /// <summary>
    /// Retraits et espacement valant pour tous les paragraphes du bloc, en points. Zéro
    /// partout est le cas de tous les documents écrits jusqu'ici, et compose exactement
    /// comme avant : c'est pourquoi cette addition ne demande aucune migration et laisse
    /// <c>FormatVersion</c> à 3.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Paragraph))]
    [property: JsonPropertyOrder(240)]
    private double _indentLeft;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Paragraph))]
    [property: JsonPropertyOrder(250)]
    private double _indentRight;

    /// <summary>
    /// Retrait de la première ligne, compté à partir du retrait gauche. Négatif, la
    /// première ligne pend à gauche des suivantes — la puce se pose dans ce creux.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Paragraph))]
    [property: JsonPropertyOrder(260)]
    private double _indentFirstLine;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Paragraph))]
    [property: JsonPropertyOrder(270)]
    private double _spaceBefore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Paragraph))]
    [property: JsonPropertyOrder(280)]
    private double _spaceAfter;

    /// <summary>
    /// Paragraphes qui s'écartent des réglages ci-dessus, repérés par indice. Vide veut
    /// dire que le bloc est d'un seul tenant — et dans les documents réels, il ne l'est
    /// pas : un même bloc d'ATA 21 mêle des paragraphes au fer à gauche et des listes à
    /// puces en retrait négatif.
    /// </summary>
    [JsonPropertyOrder(290)]
    public ObservableCollection<ParagraphSpan> Paragraphs { get; set; } = [];

    /// <summary>
    /// Nombre de colonnes à l'intérieur du bloc (<c>fo:column-count</c>). Une seule est
    /// le cas de tous les documents écrits jusqu'ici, et compose exactement comme avant :
    /// c'est pourquoi cette addition ne demande aucune migration et laisse
    /// <c>FormatVersion</c> à 3.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Colonnage))]
    [property: JsonPropertyOrder(300)]
    private int _columns = 1;

    /// <summary>
    /// Gouttière entre deux colonnes, en points (<c>fo:column-gap</c>). Sans effet tant
    /// que le bloc n'a qu'une colonne.
    ///
    /// La valeur par défaut est celle que portent les neuf blocs d'ATA 21 : 0,0787 in,
    /// soit 2 mm exactement, soit 5,67 pt — le défaut métrique de Publisher, qu'il pose
    /// sur toute zone de texte qu'on crée. Zéro aurait aussi bien fait l'affaire pour la
    /// non-régression, la gouttière ne servant pas à une colonne ; mais elle aurait fait
    /// se toucher les colonnes le jour où l'on passe le nombre à deux.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Colonnage))]
    [property: JsonPropertyOrder(310)]
    private double _columnGap = 5.67;

    /// <summary>
    /// Les cinq réglages d'un seul tenant, pour la composition — comme
    /// <see cref="Padding"/> pour les marges.
    /// </summary>
    [JsonIgnore]
    public ParagrapheEffectif Paragraph =>
        new(IndentLeft, IndentRight, IndentFirstLine, SpaceBefore, SpaceAfter);

    /// <summary>Le découpage en colonnes d'un seul tenant, pour la composition.</summary>
    [JsonIgnore] public Colonnage Colonnage => new(Columns, ColumnGap);

    /// <summary>Le texte de ce bloc est calculé : il ne se saisit pas sur la page.</summary>
    [JsonIgnore] public bool HasField => Field != FieldKind.None;

    /// <summary>Édition en cours : état transitoire d'interface, jamais sérialisé.</summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private bool _isEditing;

    [JsonIgnore] public double AbsoluteLineHeight => FontSize * LineHeight;

    [JsonIgnore] public override string Kind => "Texte";

    public override Frame Clone()
    {
        var c = new TextFrame
        {
            Text = Text, FontFamily = FontFamily, FontSize = FontSize, Bold = Bold,
            Italic = Italic, Foreground = Foreground, Align = Align,
            LineHeight = LineHeight, PaddingLeft = PaddingLeft, PaddingTop = PaddingTop,
            PaddingRight = PaddingRight, PaddingBottom = PaddingBottom, Field = Field,
            IndentLeft = IndentLeft, IndentRight = IndentRight,
            IndentFirstLine = IndentFirstLine, SpaceBefore = SpaceBefore, SpaceAfter = SpaceAfter,
            Columns = Columns, ColumnGap = ColumnGap
        };
        // Les fragments comme les écarts de paragraphe sont copiés et non partagés :
        // deux blocs issus d'une duplication doivent pouvoir diverger.
        foreach (var span in Spans) c.Spans.Add(span.Clone());
        foreach (var paragraphe in Paragraphs) c.Paragraphs.Add(paragraphe.Clone());
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

    /// <summary>
    /// Champ éventuel — le logo du jeu de coordonnées actif. Renseigné, il fournit le
    /// chemin de l'image à la place de <see cref="Path"/>, qui garde sa valeur.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasField))]
    [property: JsonPropertyOrder(120)]
    private FieldKind _field = FieldKind.None;

    [JsonIgnore] public bool HasField => Field != FieldKind.None;

    [JsonIgnore] public override string Kind => "Image";

    public override Frame Clone()
    {
        var c = new ImageFrame { Path = Path, Fit = Fit, Field = Field };
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

    /// <summary>
    /// Fond et contour sont ceux de tout bloc ; une forme naît simplement avec les deux,
    /// là où un texte ou une image naissent nus.
    /// </summary>
    public ShapeFrame()
    {
        Fill = "#FF9DC3E6";
        Stroke = "#FF2E5C8A";
    }

    [JsonIgnore] public bool IsRectangle => Shape == ShapeKind.Rectangle;
    [JsonIgnore] public bool IsEllipse => Shape == ShapeKind.Ellipse;

    [JsonIgnore] public override string Kind => Shape == ShapeKind.Ellipse ? "Ellipse" : "Rectangle";

    public override Frame Clone()
    {
        var c = new ShapeFrame { Shape = Shape };
        CopyBaseTo(c);
        return c;
    }
}

/// <summary>
/// Groupe de blocs manipulé comme un bloc unique : il se déplace, se tourne et se
/// redimensionne d'une pièce, et c'est ce qui permet à une légende de suivre son image.
///
/// Les enfants sont en coordonnées relatives au coin supérieur gauche du groupe. Rendu
/// et export composent les deux repères ; le groupe n'a donc aucune position absolue à
/// tenir à jour quand il bouge, et une rotation se compose naturellement avec celle de
/// chaque enfant.
///
/// Un enfant ne se sélectionne pas : comme dans Publisher, on dissocie pour retoucher.
/// </summary>
public partial class GroupFrame : Frame
{
    /// <summary>
    /// Blocs du groupe, du fond vers l'avant. Sérialisés en dernier (rang 100) et ce
    /// n'est pas indifférent : la taille du groupe doit être relue avant eux, sinon la
    /// mise à l'échelle ci-dessous s'appliquerait à des enfants déjà en place.
    /// </summary>
    [JsonPropertyOrder(100)]
    public ObservableCollection<Frame> Children { get; set; } = [];

    [JsonIgnore] public override string Kind => "Groupe";

    /// <summary>
    /// Redimensionner le groupe mesure ses blocs à la même aune. Le corps du texte, lui,
    /// ne change pas : un bloc de texte élargi recompose son texte, il ne le grossit pas.
    /// C'est le choix qui garde la mise en page lisible plutôt que photographique.
    /// </summary>
    protected override void OnResized(double oldSize, double newSize, bool horizontal)
    {
        if (oldSize <= 0 || newSize <= 0) return;

        var factor = newSize / oldSize;
        foreach (var child in Children)
        {
            if (horizontal)
            {
                child.X *= factor;
                child.Width *= factor;
            }
            else
            {
                child.Y *= factor;
                child.Height *= factor;
            }
        }
    }

    public override Frame Clone()
    {
        var c = new GroupFrame();

        // La géométrie d'abord : poser la taille du groupe met ses enfants à l'échelle.
        // Copier les enfants avant reviendrait à les mesurer au rapport entre la taille
        // par défaut d'un bloc et celle du groupe.
        CopyBaseTo(c);
        foreach (var child in Children) c.Children.Add(child.Clone());
        return c;
    }
}
