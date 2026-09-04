using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

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
    public const int CurrentFormatVersion = 3;

    /// <summary>Version du format de fichier, telle que lue puis migrée.</summary>
    [JsonPropertyOrder(10)]
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    [ObservableProperty]
    [property: JsonPropertyOrder(20)]
    private string _title = "Sans titre";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxWidth))]
    [property: JsonPropertyOrder(30)]
    private double _pageWidth = PageSize.A4.Width;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxHeight))]
    [property: JsonPropertyOrder(40)]
    private double _pageHeight = PageSize.A4.Height;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxWidth))]
    [property: JsonPropertyOrder(50)]
    private double _marginLeft = Units.MmToPoints(20);
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxHeight))]
    [property: JsonPropertyOrder(60)]
    private double _marginTop = Units.MmToPoints(20);
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxWidth))]
    [property: JsonPropertyOrder(70)]
    private double _marginRight = Units.MmToPoints(20);
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxHeight))]
    [property: JsonPropertyOrder(80)]
    private double _marginBottom = Units.MmToPoints(20);

    [JsonPropertyOrder(100)]
    public ObservableCollection<Page> Pages { get; set; } = [];

    [JsonIgnore] public double MarginBoxWidth => PageWidth - MarginLeft - MarginRight;
    [JsonIgnore] public double MarginBoxHeight => PageHeight - MarginTop - MarginBottom;

    public static Document CreateDefault()
    {
        var doc = new Document();
        doc.Pages.Add(new Page { Name = "Page 1" });
        return doc;
    }
}
