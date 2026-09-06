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

    /// <summary>
    /// Fond de la case ; null pour aucun fond. Un <see cref="FillBrush"/> comme celui
    /// d'un bloc, et non une chaîne de couleur : la case est rendue par un bloc de texte
    /// calculé, qui reçoit ce fond tel quel, et deux façons d'écrire un remplissage dans
    /// le même fichier rouvriraient exactement l'incohérence que la décision P0 a fermée.
    /// Le panneau, lui, n'y offre que l'aplat — une bande de trois cases se règle vite.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFill))]
    [NotifyPropertyChangedFor(nameof(FillColor))]
    [property: JsonPropertyOrder(60)]
    private FillBrush? _fill;

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
            Fill = value ? new SolidFill(_dernierFond) : null;
        }
    }

    /// <summary>
    /// La couleur du fond de la case, quand il est uni — la seule sorte que le panneau
    /// offre ici. Un fichier peut en porter une autre : le sélecteur lit alors la teinte
    /// retenue et n'écrit rien, plutôt que de remplacer en silence un dégradé par un
    /// aplat au premier affichage du panneau.
    /// </summary>
    [JsonIgnore]
    public string FillColor
    {
        get => (Fill as SolidFill)?.Color ?? _dernierFond;
        set
        {
            if (value == FillColor) return;

            _dernierFond = value;
            if (Fill is SolidFill uni) uni.Color = value;
            else OnPropertyChanged();
        }
    }

    /// <summary>
    /// Le remplissage se modifie sur place : sans cet abonnement, changer la couleur du
    /// fond d'une case ne redirait rien au tableau, qui ne redessinerait pas ses cases.
    /// </summary>
    partial void OnFillChanging(FillBrush? oldValue, FillBrush? newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= SurRemplissage;
        if (newValue is not null) newValue.PropertyChanged += SurRemplissage;
    }

    partial void OnFillChanged(FillBrush? value)
    {
        if (value is SolidFill uni) _dernierFond = uni.Color;
    }

    private void SurRemplissage(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (Fill is SolidFill uni) _dernierFond = uni.Color;

        OnPropertyChanged(nameof(Fill));
        OnPropertyChanged(nameof(FillColor));
    }

    /// <summary>Comment le panneau désigne la case. Un rang humain commence à un.</summary>
    [JsonIgnore] public string Label => $"L{Row + 1} · C{Column + 1}";

    public TableCell Clone()
    {
        var copie = new TableCell
        {
            Row = Row, Column = Column, Text = Text, Align = Align,
            // Le remplissage est copié en profondeur, comme les fragments ci-dessous.
            Fill = Fill?.Clone(), Stroke = Stroke, StrokeThickness = StrokeThickness,
        };

        // Les fragments sont copiés et non partagés : deux cases issues d'une
        // duplication doivent pouvoir diverger.
        foreach (var fragment in Spans) copie.Spans.Add(fragment.Clone());
        return copie;
    }
}
