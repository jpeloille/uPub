using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
/// Les quatre sortes de remplissage, telles que le panneau les propose. Une énumération
/// en plus des quatre classes, parce qu'un sélecteur choisit une valeur et non un type :
/// c'est elle qui est liée au menu déroulant, et le bloc en tire l'instance.
/// </summary>
public enum FillKind { Solid, Gradient, Image, Tint }

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
[JsonDerivedType(typeof(TableFrame), "table")]
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
    /// Fond du bloc. Null signifie « aucun fond » : ce qui passe sous le bloc reste
    /// visible. Un fond blanc, lui, est un aplat opaque qui masque. Les deux ne se
    /// confondent pas, même posés sur une page blanche — la différence se voit dès qu'un
    /// bloc passe dessous, et elle est conservée à l'export. Commun à tous les blocs :
    /// une image ou un texte se remplit comme une forme.
    ///
    /// C'était une chaîne de couleur jusqu'au poste C5c ; c'est désormais un
    /// <see cref="FillBrush"/>, parce qu'un remplissage par image ne se dit pas en
    /// #AARRGGBB — et que c'est ainsi que les cinq images d'ATA 21 sont posées.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFill))]
    [NotifyPropertyChangedFor(nameof(FillKind))]
    [NotifyPropertyChangedFor(nameof(FillIsSolid))]
    [NotifyPropertyChangedFor(nameof(FillColor))]
    [NotifyPropertyChangedFor(nameof(FillOpacity))]
    [property: JsonPropertyOrder(70)]
    private FillBrush? _fill;

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
    /// Le dernier remplissage de chaque sorte, pour que changer de sorte puis revenir
    /// retrouve ses réglages au lieu d'un dégradé neuf. Même courtoisie que
    /// <see cref="_lastFill"/>, et même statut : jamais sérialisé.
    /// </summary>
    private readonly FillBrush?[] _lastOfKind = new FillBrush?[4];

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
            Fill = value ? new SolidFill(_lastFill) : null;
        }
    }

    /// <summary>
    /// La sorte de remplissage, telle que la choisit le menu déroulant du panneau.
    /// Changer de sorte remplace l'instance ; on retrouve celle qu'on avait si l'on
    /// revient. Sans fond, la sorte lue est « uni », qui est ce qu'un fond neuf sera.
    /// </summary>
    [JsonIgnore]
    public FillKind FillKind
    {
        get => FillBrush.SorteDe(Fill);
        set
        {
            if (value == FillKind && Fill is not null) return;

            if (Fill is { } ancien) _lastOfKind[(int)FillKind] = ancien;
            Fill = _lastOfKind[(int)value] ?? FillBrush.Neuf(value, _lastFill);
        }
    }

    /// <summary>
    /// Le fond est-il un aplat : c'est la seule sorte dont la couleur se règle sur le
    /// bloc lui-même, les trois autres ayant leurs propres champs. Le panneau s'en sert
    /// pour ne montrer que ce qui a un effet.
    /// </summary>
    [JsonIgnore] public bool FillIsSolid => Fill is SolidFill;

    /// <summary>
    /// Teinte présentée au sélecteur de couleur, toujours définie même quand le bloc
    /// n'a pas de fond. Le sélecteur renvoie vers la source la valeur qu'il vient d'en
    /// lire dès qu'il s'affiche : le brancher directement sur <see cref="Fill"/>
    /// donnerait un fond transparent parasite au premier bloc sélectionné, et la case
    /// « fond » se cocherait d'elle-même. On sépare donc la teinte de sa présence.
    ///
    /// Elle ne vaut que pour un fond UNI. Le sélecteur renvoyant ce qu'il lit dès qu'il
    /// s'affiche, un accesseur qui écrirait dans un fond d'une autre sorte remplacerait
    /// un dégradé par un aplat à la seule sélection du bloc — le même piège que
    /// ci-dessus, sous une autre forme. Le dégradé, l'image et la teinte se règlent
    /// chacun dans leurs propres champs.
    /// </summary>
    [JsonIgnore]
    public string FillColor
    {
        get => (Fill as SolidFill)?.Color ?? _lastFill;
        set
        {
            if (value == FillColor) return;

            _lastFill = value;
            // Choisir une teinte ne crée pas un fond : seule la case le fait.
            if (Fill is SolidFill uni) uni.Color = value;
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

    /// <summary>
    /// Le remplissage est un objet, et non plus une chaîne : régler la couleur d'un
    /// dégradé le modifie SUR PLACE, sans changer l'instance que porte le bloc. Rien
    /// n'en préviendrait alors ni le panneau ni la page. On s'abonne donc au
    /// remplissage, comme le tableau s'abonne à ses colonnes depuis C9, et on redit ce
    /// qui en dépend.
    ///
    /// Le désabonnement passe par la surcharge à deux paramètres : celle à un seul ne
    /// donne pas l'ancienne valeur, et le bloc continuerait d'entendre un remplissage
    /// qu'il ne porte plus.
    /// </summary>
    partial void OnFillChanging(FillBrush? oldValue, FillBrush? newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= SurRemplissage;
        if (newValue is not null) newValue.PropertyChanged += SurRemplissage;
    }

    partial void OnFillChanged(FillBrush? value)
    {
        if (value is SolidFill uni) _lastFill = uni.Color;
    }

    private void SurRemplissage(object? sender, PropertyChangedEventArgs e)
    {
        if (Fill is SolidFill uni) _lastFill = uni.Color;

        OnPropertyChanged(nameof(Fill));
        OnPropertyChanged(nameof(FillColor));
        OnPropertyChanged(nameof(FillOpacity));
        OnPropertyChanged(nameof(FillIsSolid));
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
        // En profondeur, et non par référence : le remplissage est un objet depuis C5c,
        // et deux blocs issus d'une duplication doivent pouvoir diverger. Une copie de
        // surface ferait qu'éclaircir le dégradé de l'un éclaircirait celui de l'autre.
        target.Fill = Fill?.Clone();
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
        new(IndentLeft, IndentRight, IndentFirstLine, SpaceBefore, SpaceAfter, Align);

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
        Fill = new SolidFill("#FF9DC3E6");
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

/// <summary>
/// Bande de cases : des colonnes, des lignes, et une cellule par croisement utile.
///
/// Ce que les documents réels demandent, et rien de plus. Les fiches ATR portent
/// dix-neuf objets tableau, dont seize de trois colonnes sur une seule ligne, aucune
/// cellule fusionnée, aucun tableau imbriqué (voir <c>Docs/Documents-reels.md</c>).
/// C'est un porte-cases, pas un tableur : il ne calcule rien, ne se scinde pas entre
/// deux pages et ne fusionne pas.
///
/// **Un tableau se rend comme un groupe.** Ses cases sont des blocs de texte calculés
/// — <see cref="CellFrames"/> — que l'écran et l'export dessinent par le même chemin
/// que n'importe quel bloc de texte. Aucun des deux rendus n'a une ligne de tableau à
/// lui : ils sont donc d'accord par construction, comme ils le sont pour un groupe, et
/// une case hérite gratuitement de la composition de R1, du fond et du contour de C1.
///
/// **Les colonnes et les lignes font la taille du bloc**, et non l'inverse : c'est la
/// seule règle qui rende vrai le chiffre affiché dans le panneau. Redimensionner le
/// bloc mesure les colonnes à la même aune (comme un groupe fait de ses blocs) ;
/// modifier une colonne fait suivre le bloc. Les deux sens passent par le même
/// verrou, sans quoi ils se relanceraient l'un l'autre indéfiniment.
/// </summary>
public partial class TableFrame : Frame
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CellFrames))]
    [property: JsonPropertyOrder(100)]
    private string _fontFamily = "DejaVu Sans";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CellFrames))]
    [property: JsonPropertyOrder(110)]
    private double _fontSize = 10;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CellFrames))]
    [property: JsonPropertyOrder(120)]
    private bool _bold;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CellFrames))]
    [property: JsonPropertyOrder(130)]
    private bool _italic;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CellFrames))]
    [property: JsonPropertyOrder(140)]
    private string _foreground = "#FF1A1A1A";

    /// <summary>
    /// Interligne en multiple de la hauteur naturelle de la police, comme sur un bloc
    /// de texte depuis R4. Une bande se compose serré : le défaut est la hauteur
    /// naturelle, sans rien y ajouter.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CellFrames))]
    [property: JsonPropertyOrder(150)]
    private double _lineHeight = 1;

    /// <summary>
    /// Marge intérieure des cases, en points, la même des quatre côtés. Une seule
    /// valeur là où le bloc de texte en a quatre depuis C6 : ce qu'on règle sur une
    /// bande, c'est l'air autour du texte, et le régler côté par côté sur chaque case
    /// d'un tableau demanderait quatre champs pour un besoin qu'aucun des deux
    /// documents réels n'exprime.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CellFrames))]
    [property: JsonPropertyOrder(160)]
    private double _cellPadding = 3;

    private ObservableCollection<TableColumn> _columns = [];
    private ObservableCollection<TableRow> _rows = [];
    private ObservableCollection<TableCell> _cells = [];

    /// <summary>
    /// Colonnes, de gauche à droite. Écrites après la géométrie du bloc (rang 40 et 50)
    /// et ce n'est pas indifférent : la largeur doit être relue avant elles, sinon la
    /// mise à l'échelle ci-dessous s'appliquerait à des colonnes déjà en place. C'est
    /// le même piège que celui des blocs d'un groupe, et la même parade.
    /// </summary>
    [JsonPropertyOrder(170)]
    public ObservableCollection<TableColumn> Columns
    {
        get => _columns;
        set => Adopter(ref _columns, value);
    }

    /// <summary>Lignes, de haut en bas.</summary>
    [JsonPropertyOrder(180)]
    public ObservableCollection<TableRow> Rows
    {
        get => _rows;
        set => Adopter(ref _rows, value);
    }

    /// <summary>
    /// Cases, rangées par ligne puis par colonne. Un croisement sans cellule est une
    /// case vide : rien n'y est tracé, pas même son contour.
    /// </summary>
    [JsonPropertyOrder(190)]
    public ObservableCollection<TableCell> Cells
    {
        get => _cells;
        set => Adopter(ref _cells, value);
    }

    public TableFrame()
    {
        Brancher(_columns, true);
        Brancher(_rows, true);
        Brancher(_cells, true);
    }

    [JsonIgnore] public override string Kind => "Tableau";

    // ------------------------------------------------------------------ disposition

    /// <summary>
    /// Les cases, en blocs de texte prêts à dessiner, en coordonnées relatives au coin
    /// supérieur gauche du tableau — comme les blocs d'un groupe.
    ///
    /// Recalculée à chaque lecture plutôt que gardée en cache : le modèle ne porte
    /// aucun état calculé, et une case qu'on vient de modifier doit valoir aussitôt.
    /// C'est le parti pris de la grille de composition (<c>Document.VerticalGridLines</c>),
    /// et il tient ici pour la même raison — une bande compte trois cases.
    ///
    /// Une cellule mal repérée n'est pas refusée mais ignorée, et la première posée sur
    /// un croisement l'emporte : un document ne doit pas devenir illisible parce qu'un
    /// import a mal compté ses lignes. C'est la règle que R1 s'est déjà donnée pour les
    /// fragments qui se chevauchent.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Frame> CellFrames
    {
        get
        {
            var abscisses = Bornes(Columns.Select(colonne => colonne.Width));
            var ordonnees = Bornes(Rows.Select(ligne => ligne.Height));

            var cases = new List<Frame>(Cells.Count);
            var occupes = new HashSet<(int, int)>();

            foreach (var cellule in Cells)
            {
                if (cellule.Row < 0 || cellule.Row >= Rows.Count) continue;
                if (cellule.Column < 0 || cellule.Column >= Columns.Count) continue;
                if (!occupes.Add((cellule.Row, cellule.Column))) continue;

                var bloc = new TextFrame
                {
                    Name = cellule.Label,
                    X = abscisses[cellule.Column],
                    Y = ordonnees[cellule.Row],
                    Width = Columns[cellule.Column].Width,
                    Height = Rows[cellule.Row].Height,
                    Text = cellule.Text,
                    FontFamily = FontFamily,
                    FontSize = FontSize,
                    Bold = Bold,
                    Italic = Italic,
                    Foreground = Foreground,
                    Align = cellule.Align,
                    LineHeight = LineHeight,
                    PaddingLeft = CellPadding,
                    PaddingTop = CellPadding,
                    PaddingRight = CellPadding,
                    PaddingBottom = CellPadding,
                    // Le remplissage est COPIÉ, là où les fragments ci-dessous sont
                    // prêtés — et la différence n'est pas de goût. Un bloc s'abonne à son
                    // remplissage depuis C5c, pour se redessiner quand on le modifie sur
                    // place ; ce bloc-ci ne vit que le temps d'un rendu, mais
                    // l'abonnement, lui, survivrait. Prêter le fond de la case y
                    // accrocherait un bloc jeté à chaque lecture de cette liste — vingt
                    // lectures, vingt-et-un abonnés, mesuré — et le panneau la relit à
                    // chaque notification. Copier coûte un petit objet par case et rend
                    // le bloc et son fond ramassables ensemble.
                    Fill = cellule.Fill?.Clone(),
                    Stroke = cellule.Stroke,
                    StrokeThickness = cellule.StrokeThickness,
                    // Les fragments, eux, sont prêtés : ils ne portent aucun abonnement.
                    Spans = cellule.Spans,
                };

                cases.Add(bloc);
            }

            return cases;
        }
    }

    /// <summary>Positions cumulées des bords, la première à zéro.</summary>
    private static double[] Bornes(IEnumerable<double> tailles)
    {
        var bornes = new List<double> { 0 };
        foreach (var taille in tailles) bornes.Add(bornes[^1] + taille);
        return [.. bornes];
    }

    // ------------------------------------------------------------------ nombre de cases

    /// <summary>
    /// Nombre de colonnes, tel que le règle le panneau. Réglage de structure et non
    /// valeur à écrire : ce sont les colonnes elles-mêmes qui sont dans le fichier.
    /// </summary>
    [JsonIgnore]
    public int ColumnCount
    {
        get => Columns.Count;
        set => Redimensionner(Math.Max(1, value), horizontal: true);
    }

    [JsonIgnore]
    public int RowCount
    {
        get => Rows.Count;
        set => Redimensionner(Math.Max(1, value), horizontal: false);
    }

    /// <summary>
    /// Ajoute ou retire des colonnes (ou des lignes) par la fin, en créant les cases
    /// qui manquent. Une case neuve reprend l'aspect de sa voisine : ajouter une
    /// colonne à une bande encadrée ne doit pas y ouvrir un trou sans contour.
    /// </summary>
    private void Redimensionner(int voulu, bool horizontal)
    {
        var actuel = horizontal ? Columns.Count : Rows.Count;
        if (voulu == actuel) return;

        _enLot = true;
        try
        {
            Poser(voulu, horizontal);
        }
        finally
        {
            _enLot = false;
        }

        // La source compte : « Recalculer » choisit d'accorder la largeur ou la
        // hauteur du bloc selon ce qui a bougé. Lui passer le tableau lui-même ne
        // désignerait ni l'une ni l'autre, et le bloc garderait sa taille d'avant.
        Recalculer(horizontal ? Columns : (object)Rows);
        OnPropertyChanged(horizontal ? nameof(ColumnCount) : nameof(RowCount));
    }

    private void Poser(int voulu, bool horizontal)
    {

        while ((horizontal ? Columns.Count : Rows.Count) > voulu)
        {
            var dernier = (horizontal ? Columns.Count : Rows.Count) - 1;
            if (horizontal) Columns.RemoveAt(dernier);
            else Rows.RemoveAt(dernier);

            foreach (var perdue in Cells.Where(c => (horizontal ? c.Column : c.Row) == dernier).ToList())
            {
                Cells.Remove(perdue);
            }
        }

        while ((horizontal ? Columns.Count : Rows.Count) < voulu)
        {
            var index = horizontal ? Columns.Count : Rows.Count;
            if (horizontal) Columns.Add(new TableColumn(TailleType(true)));
            else Rows.Add(new TableRow(TailleType(false)));

            var autres = horizontal ? Rows.Count : Columns.Count;
            for (var i = 0; i < autres; i++)
            {
                var ligne = horizontal ? i : index;
                var colonne = horizontal ? index : i;
                Cells.Insert(RangDe(ligne, colonne), Modeler(ligne, colonne));
            }
        }
    }

    /// <summary>Taille d'une colonne (ou d'une ligne) neuve : celle de ses voisines.</summary>
    private double TailleType(bool horizontal)
    {
        if (horizontal) return Columns.Count > 0 ? Columns.Average(c => c.Width) : 100;
        return Rows.Count > 0 ? Rows.Average(r => r.Height) : 24;
    }

    /// <summary>Où insérer une case pour que le fichier se lise ligne par ligne.</summary>
    private int RangDe(int ligne, int colonne)
    {
        for (var i = 0; i < Cells.Count; i++)
        {
            if (Cells[i].Row > ligne || (Cells[i].Row == ligne && Cells[i].Column > colonne)) return i;
        }

        return Cells.Count;
    }

    /// <summary>Une case neuve, habillée comme celles qui l'entourent.</summary>
    private TableCell Modeler(int ligne, int colonne)
    {
        var voisine = Cells.LastOrDefault(c => c.Row == ligne) ?? Cells.LastOrDefault();
        return new TableCell
        {
            Row = ligne,
            Column = colonne,
            Align = voisine?.Align ?? TextAlign.Left,
            // Le remplissage de la voisine est copié et non partagé : deux cases qui
            // portent le même objet changeraient de couleur ensemble.
            Fill = voisine?.Fill?.Clone(),
            Stroke = voisine?.Stroke,
            StrokeThickness = voisine?.StrokeThickness ?? 1,
        };
    }

    // ------------------------------------------------------------------ la grille

    private string _derniereGrille = "#FF6B7280";

    /// <summary>
    /// La grille, telle que la règle le panneau : le contour de toutes les cases d'un
    /// seul geste. Chaque case garde le sien dans le fichier — c'est ainsi que
    /// Publisher les décrit et que l'import de R5 les lira — mais personne ne veut
    /// cocher trois cases pour encadrer une bande.
    /// </summary>
    [JsonIgnore]
    public bool HasGrid
    {
        get => Cells.Any(cellule => cellule.Stroke is not null);
        set
        {
            if (value == HasGrid) return;

            foreach (var cellule in Cells) cellule.Stroke = value ? _derniereGrille : null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GridColor));
        }
    }

    [JsonIgnore]
    public string GridColor
    {
        get => Cells.FirstOrDefault(cellule => cellule.Stroke is not null)?.Stroke ?? _derniereGrille;
        set
        {
            if (value == GridColor) return;

            _derniereGrille = value;
            if (HasGrid)
            {
                foreach (var cellule in Cells) cellule.Stroke = value;
            }

            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public double GridThickness
    {
        get => Cells.Count > 0 ? Cells[0].StrokeThickness : 1;
        set
        {
            if (Math.Abs(value - GridThickness) < 1e-9) return;

            foreach (var cellule in Cells) cellule.StrokeThickness = value;
            OnPropertyChanged();
        }
    }

    // ------------------------------------------------------------------ accord des tailles

    /// <summary>
    /// Verrou du va-et-vient entre la taille du bloc et celle des cases. Sans lui,
    /// mesurer les colonnes réécrirait la largeur, qui remesurerait les colonnes.
    /// </summary>
    private bool _ajuste;

    /// <summary>
    /// Redimensionner le tableau mesure ses colonnes et ses lignes à la même aune —
    /// exactement ce que fait un groupe de ses blocs. Le corps du texte, lui, ne bouge
    /// pas : une bande élargie donne de l'air à ses cases, elle ne grossit pas.
    /// </summary>
    protected override void OnResized(double oldSize, double newSize, bool horizontal)
    {
        if (_ajuste || oldSize <= 0 || newSize <= 0) return;

        var facteur = newSize / oldSize;
        _ajuste = true;
        try
        {
            if (horizontal)
            {
                foreach (var colonne in Columns) colonne.Width *= facteur;
            }
            else
            {
                foreach (var ligne in Rows) ligne.Height *= facteur;
            }
        }
        finally
        {
            _ajuste = false;
        }

        OnPropertyChanged(nameof(CellFrames));
    }

    /// <summary>
    /// Une colonne, une ligne ou une case vient de changer : le bloc reprend la taille
    /// de ce qu'il contient, et les cases sont à redessiner.
    ///
    /// L'écart est comparé à un seuil et non à zéro. Mesurer trois colonnes au rapport
    /// d'un redimensionnement laisse leur somme à un ulp de la largeur demandée ; sans
    /// ce seuil, ouvrir puis réenregistrer un document déplacerait ce dernier chiffre
    /// et l'aller-retour cesserait d'être identique au caractère près — ce qu'exigent
    /// le contrôle P2 et la recette de ce poste. Un milliardième de point est cent fois
    /// plus fin que le plus petit réglage qu'offre le panneau.
    /// </summary>
    private void Recalculer(object? source)
    {
        // Pendant un redimensionnement en série, on laisse passer : le lot notifie une
        // seule fois à la fin.
        if (_enLot) return;

        if (!_ajuste)
        {
            _ajuste = true;
            try
            {
                if (ReferenceEquals(source, _columns) || source is TableColumn)
                {
                    Accorder(Columns.Sum(colonne => colonne.Width), horizontal: true);
                }
                else if (ReferenceEquals(source, _rows) || source is TableRow)
                {
                    Accorder(Rows.Sum(ligne => ligne.Height), horizontal: false);
                }
            }
            finally
            {
                _ajuste = false;
            }
        }

        OnPropertyChanged(nameof(CellFrames));
    }

    private void Accorder(double somme, bool horizontal)
    {
        if (horizontal)
        {
            if (Math.Abs(somme - Width) > 1e-9) Width = somme;
        }
        else
        {
            if (Math.Abs(somme - Height) > 1e-9) Height = somme;
        }
    }

    /// <summary>
    /// Le sérialiseur remplace une collection accessible en écriture au lieu de la
    /// remplir : un abonnement pris au constructeur ne survivrait pas à l'ouverture
    /// d'un document, et le tableau cesserait silencieusement de se redessiner.
    /// </summary>
    private void Adopter<T>(ref ObservableCollection<T> champ, ObservableCollection<T> neuve)
        where T : ObservableObject
    {
        if (ReferenceEquals(champ, neuve)) return;

        Brancher(champ, false);
        champ = neuve;
        Brancher(neuve, true);
        Recalculer(neuve);
    }

    private void Brancher<T>(ObservableCollection<T> collection, bool brancher)
        where T : ObservableObject
    {
        if (brancher) collection.CollectionChanged += SurCollection;
        else collection.CollectionChanged -= SurCollection;

        foreach (var membre in collection)
        {
            if (brancher) membre.PropertyChanged += SurMembre;
            else membre.PropertyChanged -= SurMembre;
        }
    }

    private void SurCollection(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var parti in e.OldItems?.OfType<ObservableObject>() ?? [])
        {
            parti.PropertyChanged -= SurMembre;
        }

        foreach (var venu in e.NewItems?.OfType<ObservableObject>() ?? [])
        {
            venu.PropertyChanged += SurMembre;
        }

        Recalculer(sender);
    }

    private void SurMembre(object? sender, PropertyChangedEventArgs e) => Recalculer(sender);

    /// <summary>
    /// Vrai pendant qu'on ajoute ou retire des lignes et des colonnes en série.
    ///
    /// Sans lui, chaque case insérée fait renaître toutes les autres : « CellFrames »
    /// reconstruit la grille entière à chaque lecture, et le panneau la relit à chaque
    /// notification. Mesuré par la relecture de C9 : passer un tableau à 40 × 40 —
    /// deux valeurs que le champ accepte — figeait l'application 145 secondes pour
    /// 1 600 cases, soit 1 673 reconstructions complètes. Une seule suffit.
    ///
    /// Le cas réel des documents à reprendre — une bande de trois cases — n'en
    /// souffrait pas. C'est le plafond offert par le panneau qui rendait le défaut
    /// atteignable en deux gestes.
    /// </summary>
    private bool _enLot;

    public override Frame Clone()
    {
        var copie = new TableFrame
        {
            FontFamily = FontFamily, FontSize = FontSize, Bold = Bold, Italic = Italic,
            Foreground = Foreground, LineHeight = LineHeight, CellPadding = CellPadding,
        };

        // Le verrou vaut pour tout le corps, et dans les deux sens : poser la taille du
        // bloc mesurerait des colonnes encore vides, et ajouter les colonnes réécrirait
        // la taille qu'on vient de copier. L'ordre des trois boucles n'a alors plus
        // d'importance — c'est ce qu'on veut d'une copie.
        copie._ajuste = true;
        try
        {
            CopyBaseTo(copie);
            foreach (var colonne in Columns) copie.Columns.Add(new TableColumn(colonne.Width));
            foreach (var ligne in Rows) copie.Rows.Add(new TableRow(ligne.Height));
            foreach (var cellule in Cells) copie.Cells.Add(cellule.Clone());
        }
        finally
        {
            copie._ajuste = false;
        }

        return copie;
    }
}
