using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

/// <summary>
/// Une ligne de repère tracée sur la planche. Les lignes de base portent moins de
/// trait que les séparations de colonnes : il y en a dix fois plus, et elles guident
/// sans devoir se voir.
/// </summary>
public sealed record GuideLine(double Position, bool IsBaseline);

/// <summary>
/// Repère libre, posé où l'utilisateur veut. C'est le seul mécanisme qui permette
/// d'aligner sur une position qu'il a choisie lui-même : la grille et les marges, elles,
/// sont des positions décidées d'avance. Porté par le document et non par la page, comme
/// dans Publisher — un repère sert à tenir un alignement d'une page à l'autre.
///
/// Une classe plutôt qu'un simple nombre : la position se modifie, à la souris comme au
/// champ numérique, et la liaison a besoin d'un objet à qui parler.
/// </summary>
public partial class RulerGuide : ObservableObject
{
    public RulerGuide()
    {
    }

    public RulerGuide(double position) => Position = position;

    [ObservableProperty] private double _position;
}

public partial class Page : ObservableObject
{
    [ObservableProperty]
    [property: JsonPropertyOrder(10)]
    private string _name = "Page";

    /// <summary>Blocs dans l'ordre de superposition : le dernier est au premier plan.</summary>
    [JsonPropertyOrder(100)]
    public ObservableCollection<Frame> Frames { get; set; } = [];
}

public partial class Document : ObservableObject
{
    /// <summary>
    /// Version du format que cette version du logiciel écrit. Un fichier plus ancien est
    /// migré à la lecture (<see cref="Services.FormatMigrations"/>) ; un fichier plus
    /// récent est refusé, plutôt que lu de travers.
    /// </summary>
    public const int CurrentFormatVersion = 4;

    /// <summary>Version du format de fichier, telle que lue puis migrée.</summary>
    [JsonPropertyOrder(10)]
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    [ObservableProperty]
    [property: JsonPropertyOrder(20)]
    private string _title = "Sans titre";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxWidth))]
    [property: JsonPropertyOrder(30)]
    [NotifyPropertyChangedFor(nameof(VerticalGridLines))]
    [NotifyPropertyChangedFor(nameof(VerticalGuides))]
    private double _pageWidth = PageSize.A4.Width;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxHeight))]
    [property: JsonPropertyOrder(40)]
    [NotifyPropertyChangedFor(nameof(HorizontalGridLines))]
    [NotifyPropertyChangedFor(nameof(HorizontalGuides))]
    private double _pageHeight = PageSize.A4.Height;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxWidth))]
    [property: JsonPropertyOrder(50)]
    [NotifyPropertyChangedFor(nameof(VerticalGridLines))]
    [NotifyPropertyChangedFor(nameof(VerticalGuides))]
    private double _marginLeft = Units.MmToPoints(20);
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxHeight))]
    [property: JsonPropertyOrder(60)]
    [NotifyPropertyChangedFor(nameof(HorizontalGridLines))]
    [NotifyPropertyChangedFor(nameof(HorizontalGuides))]
    private double _marginTop = Units.MmToPoints(20);
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxWidth))]
    [property: JsonPropertyOrder(70)]
    [NotifyPropertyChangedFor(nameof(VerticalGridLines))]
    [NotifyPropertyChangedFor(nameof(VerticalGuides))]
    private double _marginRight = Units.MmToPoints(20);
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxHeight))]
    [property: JsonPropertyOrder(80)]
    [NotifyPropertyChangedFor(nameof(HorizontalGridLines))]
    [NotifyPropertyChangedFor(nameof(HorizontalGuides))]
    private double _marginBottom = Units.MmToPoints(20);

    // -------------------------------------------------------------- fond de page

    /// <summary>
    /// Fond de la page, dessiné en tête de chaque page, sous tous les blocs. Null veut
    /// dire « page blanche », et c'est ce qu'écrivaient tous les documents jusqu'ici.
    ///
    /// Porté par le document et non par la page, comme les repères libres et pour la
    /// même raison : un fond change l'allure de la publication entière, et Publisher le
    /// règle une fois pour toutes dans <c>Format ▸ Background</c>.
    ///
    /// C'est un <see cref="FillBrush"/> et non une couleur : le fond de page est
    /// justement l'endroit où un dégradé sert. Le nom était réservé depuis la décision
    /// P0 — « Fill » pour les blocs, « Background » pour la page.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackground))]
    [NotifyPropertyChangedFor(nameof(BackgroundKind))]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    [property: JsonPropertyOrder(85)]
    private FillBrush? _background;

    /// <summary>La teinte retenue pendant qu'aucun fond n'est posé. Jamais sérialisée.</summary>
    private string _dernierFond = "#FFF2F4F7";

    private readonly FillBrush?[] _fondsRetenus = new FillBrush?[4];

    /// <summary>
    /// Présence d'un fond de page, telle que la manipule le panneau. Même mécanique que
    /// sur un bloc — voir <see cref="Frame.HasFill"/> — et pour les mêmes raisons.
    /// </summary>
    [JsonIgnore]
    public bool HasBackground
    {
        get => Background is not null;
        set
        {
            if (value == HasBackground) return;
            Background = value ? new SolidFill(_dernierFond) : null;
        }
    }

    [JsonIgnore]
    public FillKind BackgroundKind
    {
        get => FillBrush.SorteDe(Background);
        set
        {
            if (value == BackgroundKind && Background is not null) return;

            if (Background is { } ancien) _fondsRetenus[(int)BackgroundKind] = ancien;
            Background = _fondsRetenus[(int)value] ?? FillBrush.Neuf(value, _dernierFond);
        }
    }

    /// <summary>La couleur du fond de page quand il est uni ; voir <see cref="Frame.FillColor"/>.</summary>
    [JsonIgnore]
    public string BackgroundColor
    {
        get => (Background as SolidFill)?.Color ?? _dernierFond;
        set
        {
            if (value == BackgroundColor) return;

            _dernierFond = value;
            if (Background is SolidFill uni) uni.Color = value;
            else OnPropertyChanged();
        }
    }

    partial void OnBackgroundChanging(FillBrush? oldValue, FillBrush? newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= SurFond;
        if (newValue is not null) newValue.PropertyChanged += SurFond;
    }

    partial void OnBackgroundChanged(FillBrush? value)
    {
        if (value is SolidFill uni) _dernierFond = uni.Color;
    }

    /// <summary>
    /// Le fond se modifie sur place : sans cet abonnement, régler l'angle d'un dégradé
    /// de page ne redirait rien au document, et la planche ne se repeindrait pas.
    /// </summary>
    private void SurFond(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (Background is SolidFill uni) _dernierFond = uni.Color;

        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(BackgroundColor));
    }

    [JsonPropertyOrder(100)]
    // ------------------------------------------------------------------ grille

    /// <summary>Colonnes de composition dans la zone de marges ; 1 signifie « aucune grille ».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerticalGridLines))]
    [NotifyPropertyChangedFor(nameof(VerticalGuides))]
    [property: JsonPropertyOrder(90)]
    private int _columnCount = 1;

    /// <summary>Gouttière entre deux colonnes, en points.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerticalGridLines))]
    [NotifyPropertyChangedFor(nameof(VerticalGuides))]
    [property: JsonPropertyOrder(91)]
    private double _columnSpacing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HorizontalGridLines))]
    [NotifyPropertyChangedFor(nameof(HorizontalGuides))]
    [property: JsonPropertyOrder(92)]
    private int _rowCount = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HorizontalGridLines))]
    [NotifyPropertyChangedFor(nameof(HorizontalGuides))]
    [property: JsonPropertyOrder(93)]
    private double _rowSpacing;

    /// <summary>
    /// Pas de la grille de ligne de base, en points — et non en millimètres : elle se
    /// cale sur le corps du texte, qui est typographique. En dessous d'un point, le
    /// réglage n'a pas de sens et la grille est tenue pour absente.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HorizontalGridLines))]
    [NotifyPropertyChangedFor(nameof(HorizontalGuides))]
    [property: JsonPropertyOrder(94)]
    private double _baselineSpacing;

    /// <summary>Décalage de la première ligne de base sous la marge haute, en points.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HorizontalGridLines))]
    [NotifyPropertyChangedFor(nameof(HorizontalGuides))]
    [property: JsonPropertyOrder(95)]
    private double _baselineOffset;

    /// <summary>Repères libres verticaux, en abscisses.</summary>
    [JsonPropertyOrder(96)]
    public ObservableCollection<RulerGuide> VerticalRulerGuides { get; set; } = [];

    /// <summary>Repères libres horizontaux, en ordonnées.</summary>
    [JsonPropertyOrder(97)]
    public ObservableCollection<RulerGuide> HorizontalRulerGuides { get; set; } = [];

    [JsonPropertyOrder(100)]
    public ObservableCollection<Page> Pages { get; set; } = [];

    [JsonIgnore] public double MarginBoxWidth => PageWidth - MarginLeft - MarginRight;
    [JsonIgnore] public double MarginBoxHeight => PageHeight - MarginTop - MarginBottom;

    /// <summary>
    /// Bornes des bandes d'une grille : « count » bandes de largeur égale séparées par
    /// « spacing ». Rend le début et la fin de chaque bande. Un réglage impossible —
    /// des gouttières plus larges que la zone — ne rend rien plutôt que des repères faux.
    /// </summary>
    private static IEnumerable<double> Bands(double start, double length, int count, double spacing)
    {
        count = Math.Max(1, count);
        var band = (length - spacing * (count - 1)) / count;
        if (band <= 0) yield break;

        for (var i = 0; i < count; i++)
        {
            var origin = start + i * (band + spacing);
            yield return origin;
            yield return origin + band;
        }
    }

    /// <summary>Ordonnées des lignes de base, sous la marge haute et jusqu'à la marge basse.</summary>
    private IEnumerable<double> Baselines()
    {
        if (BaselineSpacing < 1) yield break;

        var limit = PageHeight - MarginBottom;
        for (var y = MarginTop + BaselineOffset; y <= limit; y += BaselineSpacing) yield return y;
    }

    /// <summary>
    /// Repères de colonnes à tracer : les séparations intérieures seulement. Les bornes
    /// extérieures se confondent avec la zone de marges, déjà dessinée.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<GuideLine> VerticalGridLines => ColumnCount <= 1
        ? []
        : Interior(Bands(MarginLeft, MarginBoxWidth, ColumnCount, ColumnSpacing))
            .Select(x => new GuideLine(x, false)).ToList();

    /// <summary>Séparations de lignes et grille de ligne de base, dans cet ordre de tracé.</summary>
    [JsonIgnore]
    public IReadOnlyList<GuideLine> HorizontalGridLines =>
    [
        .. RowCount <= 1
            ? []
            : Interior(Bands(MarginTop, MarginBoxHeight, RowCount, RowSpacing)).Select(y => new GuideLine(y, false)),
        .. Baselines().Select(y => new GuideLine(y, true))
    ];

    /// <summary>Abscisses sur lesquelles un bloc s'aimante : bords, marges, milieu, colonnes.</summary>
    /// <summary>
    /// Ces deux listes sont relues à chaque déplacement du pointeur plutôt que mises en
    /// cache : un repère qu'on vient de poser doit valoir aussitôt, sans qu'on ait à
    /// prévenir qui que ce soit du changement.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<double> VerticalGuides =>
    [
        0, MarginLeft, PageWidth / 2, PageWidth - MarginRight, PageWidth,
        .. Bands(MarginLeft, MarginBoxWidth, ColumnCount, ColumnSpacing),
        .. VerticalRulerGuides.Select(guide => guide.Position)
    ];

    [JsonIgnore]
    public IReadOnlyList<double> HorizontalGuides =>
    [
        0, MarginTop, PageHeight / 2, PageHeight - MarginBottom, PageHeight,
        .. Bands(MarginTop, MarginBoxHeight, RowCount, RowSpacing),
        .. Baselines(),
        .. HorizontalRulerGuides.Select(guide => guide.Position)
    ];

    /// <summary>
    /// Écarte les deux bornes extérieures et les doublons — sans gouttière, la fin d'une
    /// bande et le début de la suivante sont au même endroit.
    /// </summary>
    private static IEnumerable<double> Interior(IEnumerable<double> bounds)
    {
        var all = bounds.ToList();
        if (all.Count < 2) return [];

        return all.Skip(1).Take(all.Count - 2)
            .GroupBy(value => Math.Round(value, 4))
            .Select(group => group.First());
    }

    public static Document CreateDefault()
    {
        var doc = new Document();
        doc.Pages.Add(new Page { Name = "Page 1" });
        return doc;
    }
}
