using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

public partial class Page : ObservableObject
{
    [ObservableProperty] private string _name = "Page";

    /// <summary>Blocs dans l'ordre de superposition : le dernier est au premier plan.</summary>
    public ObservableCollection<Frame> Frames { get; set; } = [];
}

public partial class Document : ObservableObject
{
    /// <summary>Version du format de fichier, pour pouvoir migrer plus tard.</summary>
    public int FormatVersion { get; set; } = 1;

    [ObservableProperty] private string _title = "Sans titre";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxWidth))]
    private double _pageWidth = PageSize.A4.Width;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxHeight))]
    private double _pageHeight = PageSize.A4.Height;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxWidth))]
    private double _marginLeft = Units.MmToPoints(20);
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxHeight))]
    private double _marginTop = Units.MmToPoints(20);
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxWidth))]
    private double _marginRight = Units.MmToPoints(20);
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginBoxHeight))]
    private double _marginBottom = Units.MmToPoints(20);

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
