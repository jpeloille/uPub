using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

/// <summary>Une colonne, réduite à sa largeur en points.</summary>
public partial class TableColumn : ObservableObject
{
    public TableColumn()
    {
    }

    public TableColumn(double width) => Width = width;

    [ObservableProperty]
    [property: JsonPropertyOrder(10)]
    private double _width = 100;
}

/// <summary>Une ligne, réduite à sa hauteur en points.</summary>
public partial class TableRow : ObservableObject
{
    public TableRow()
    {
    }

    public TableRow(double height) => Height = height;

    [ObservableProperty]
    [property: JsonPropertyOrder(10)]
    private double _height = 24;
}

/// <summary>
/// Une case du tableau, repérée par sa ligne et sa colonne.
///
/// Repérée plutôt qu'imbriquée dans sa ligne : les fiches ATR portent 51 cellules dont
/// seize seulement contiennent du texte, et un repérage explicite permet de n'écrire
/// que celles qui disent quelque chose. Il donne aussi au panneau de propriétés de quoi
/// nommer la case qu'on modifie — « L1 · C2 » — ce qu'une imbrication ne fournit pas.
///
/// Le contenu est un texte et ses fragments de style, exactement comme celui d'un bloc
/// de texte depuis R1 : une case qui doit porter un mot en gras n'a besoin d'aucun
/// mécanisme neuf. Le reste — police, corps, couleur, interligne — appartient au
/// tableau, parce qu'une bande de trois cases se règle d'un seul geste.
/// </summary>
public partial class TableCell : ObservableObject
{
    /// <summary>Ligne de la case, à partir de zéro.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    [property: JsonPropertyOrder(10)]
    private int _row;

    /// <summary>Colonne de la case, à partir de zéro.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    [property: JsonPropertyOrder(20)]
    private int _column;

    [ObservableProperty]
    [property: JsonPropertyOrder(30)]
    private string _text = "";

    /// <summary>
    /// Portions de texte qui s'écartent du style du tableau, comme sur un bloc de texte.
    /// Vide veut dire « d'un seul style ».
    /// </summary>
    [JsonPropertyOrder(40)]
    public ObservableCollection<StyleSpan> Spans { get; set; } = [];

    /// <summary>
    /// Alignement propre à la case : dans une bande à trois cases, l'intitulé est à
    /// gauche et la valeur au centre. C'est le seul réglage typographique que la case
    /// porte elle-même, parce que c'est le seul qui diffère d'une case à l'autre.
    /// </summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(50)]
    private TextAlign _align = TextAlign.Left;

    /// <summary>Fond de la case, en #AARRGGBB ; null pour aucun fond.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFill))]
    [NotifyPropertyChangedFor(nameof(FillColor))]
    [property: JsonPropertyOrder(60)]
    private string? _fill;

    /// <summary>
    /// Contour de la case : c'est lui qui dessine la grille. Chaque case porte le sien
    /// plutôt que le tableau une grille d'ensemble, parce que Publisher fait ainsi et
    /// que l'import du poste R5 lira des bordures posées case par case. Le panneau, lui,
    /// les règle toutes ensemble — voir <see cref="TableFrame.GridColor"/>.
    /// </summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(70)]
    private string? _stroke;

    [ObservableProperty]
    [property: JsonPropertyOrder(80)]
    private double _strokeThickness = 1;

    /// <summary>Teinte retenue pendant qu'aucun fond n'est appliqué. Jamais sérialisée.</summary>
    private string _dernierFond = "#FFEFF2F6";

    /// <summary>
    /// Présence d'un fond, telle que la manipule le panneau. Même mécanique que sur le
    /// bloc, et pour la même raison : un sélecteur de couleur branché directement sur
    /// une propriété annulable la renseigne dès qu'il s'affiche.
    /// </summary>
    [JsonIgnore]
    public bool HasFill
    {
        get => Fill is not null;
        set
        {
            if (value == HasFill) return;
            Fill = value ? _dernierFond : null;
        }
    }

    [JsonIgnore]
    public string FillColor
    {
        get => Fill ?? _dernierFond;
        set
        {
            if (value == FillColor) return;

            _dernierFond = value;
            if (HasFill) Fill = value;
            else OnPropertyChanged();
        }
    }

    partial void OnFillChanged(string? value)
    {
        if (value is not null) _dernierFond = value;
    }

    /// <summary>Comment le panneau désigne la case. Un rang humain commence à un.</summary>
    [JsonIgnore] public string Label => $"L{Row + 1} · C{Column + 1}";

    public TableCell Clone()
    {
        var copie = new TableCell
        {
            Row = Row, Column = Column, Text = Text, Align = Align,
            Fill = Fill, Stroke = Stroke, StrokeThickness = StrokeThickness,
        };

        // Les fragments sont copiés et non partagés : deux cases issues d'une
        // duplication doivent pouvoir diverger.
        foreach (var fragment in Spans) copie.Spans.Add(fragment.Clone());
        return copie;
    }
}
