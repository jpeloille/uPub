using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    /// <summary>
    /// Le texte de la case, tel qu'il s'écrit dans le fichier. Comme sur un bloc de
    /// texte, l'écrire ne recale rien : la saisie passe par <see cref="Saisie"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Saisie))]
    [property: JsonPropertyOrder(30)]
    private string _text = "";

    /// <summary>
    /// Le texte, tel que le panneau le modifie : les fragments suivent la frappe (voir
    /// <see cref="TextFrame.Saisie"/>, dont c'est la jumelle).
    ///
    /// C'est ici, sur la case, et non sur le bloc calculé qui la dessine, que le
    /// recalage doit avoir lieu : <see cref="TableFrame.CellFrames"/> PRÊTE à ce bloc
    /// la collection de fragments de la case, et le reconstruit à chaque lecture. Un
    /// recalage déclenché par la construction de ce bloc déplacerait les fragments de
    /// la case à chaque redessin.
    /// </summary>
    [JsonIgnore]
    public string Saisie
    {
        get => Text;
        set
        {
            var nouveau = value ?? "";
            if (nouveau == Text) return;

            // Une case n'a pas d'écarts de paragraphe : son alignement vaut pour elle
            // entière.
            Recalage.Appliquer(Text, nouveau, Spans, null);
            Text = nouveau;
        }
    }

    /// <summary>
    /// Portions de texte qui s'écartent du style du tableau, comme sur un bloc de texte.
    /// Vide veut dire « d'un seul style ».
    ///
    /// Remplie et vidée, jamais remplacée après la construction (poste Q17) : le bloc
    /// que <c>TableFrame.CellFrames</c> construit pour la case EMPRUNTE cette
    /// collection, et le contrôle qui le dessine s'y abonne.
    /// </summary>
    [JsonPropertyOrder(40)]
    public ObservableCollection<StyleSpan> Spans { get; init; } = [];

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

/// <summary>
/// Taper dans une case, sur la page (poste N27 ; fiche 23, g2 à g4) : ce que la planche demande au
/// tableau pour y entrer d'un clic, et passer de case en case au clavier. Tout se lit sur les
/// colonnes, les lignes et les cases, comme <see cref="CellFrames"/> les dessine : une case mal
/// repérée n'existe pas, et la première posée sur un croisement l'emporte.
/// </summary>
public partial class TableFrame
{
    /// <summary>
    /// La case dessinée au croisement de la ligne et de la colonne — la première posée, comme
    /// <see cref="CellFrames"/> la retient —, ou rien : un croisement sans cellule est une case vide,
    /// où rien ne se tape.
    /// </summary>
    public TableCell? CaseEn(int ligne, int colonne) =>
        ligne < 0 || ligne >= Rows.Count || colonne < 0 || colonne >= Columns.Count
            ? null
            : Cells.FirstOrDefault(c => c.Row == ligne && c.Column == colonne);

    /// <summary>
    /// La case sous un point du repère du tableau, en points depuis son coin supérieur gauche :
    /// celle de la colonne et de la ligne qui le portent, bords compris du côté de la case
    /// suivante. Rien hors des colonnes et des lignes, ni sur un croisement vide.
    /// </summary>
    public TableCell? CaseSous(double x, double y)
    {
        var colonne = RangSous(Columns.Select(c => c.Width), x);
        var ligne = RangSous(Rows.Select(r => r.Height), y);
        return colonne is { } c && ligne is { } l ? CaseEn(l, c) : null;
    }

    private static int? RangSous(IEnumerable<double> tailles, double position)
    {
        if (position < 0) return null;

        var bord = 0.0;
        var rang = 0;
        foreach (var taille in tailles)
        {
            bord += taille;
            if (position < bord) return rang;
            rang++;
        }

        // Le bord extérieur appartient à la dernière.
        return rang > 0 && position <= bord ? rang - 1 : null;
    }

    /// <summary>
    /// Les cases dessinées, dans l'ordre de lecture : ligne après ligne, de gauche à droite. C'est
    /// l'ordre de <c>Tab</c> et des flèches de côté, et celui où la recherche les parcourt.
    /// </summary>
    public IReadOnlyList<TableCell> CasesDansLOrdre() =>
        Cells.Where(c => ReferenceEquals(CaseEn(c.Row, c.Column), c))
            .OrderBy(c => c.Row).ThenBy(c => c.Column)
            .ToList();

    /// <summary>
    /// La case qui suit <paramref name="case_"/> dans l'ordre de lecture — <paramref name="sens"/>
    /// vaut 1 —, ou qui la précède — -1 ; rien au bout du tableau.
    /// </summary>
    public TableCell? CaseVoisine(TableCell case_, int sens)
    {
        var ordre = CasesDansLOrdre();
        var rang = -1;
        for (var i = 0; i < ordre.Count; i++)
        {
            if (ReferenceEquals(ordre[i], case_)) rang = i;
        }

        var cible = rang + Math.Sign(sens);
        return rang < 0 || cible < 0 || cible >= ordre.Count ? null : ordre[cible];
    }

    /// <summary>
    /// La case au-dessus de <paramref name="case_"/> — <paramref name="sens"/> vaut -1 — ou
    /// au-dessous — 1 —, dans sa colonne : la plus proche, les croisements vides sautés ; rien au
    /// bord du tableau.
    /// </summary>
    public TableCell? CaseDansLaColonne(TableCell case_, int sens)
    {
        if (!ReferenceEquals(CaseEn(case_.Row, case_.Column), case_) || sens == 0) return null;

        for (var ligne = case_.Row + Math.Sign(sens); ligne >= 0 && ligne < Rows.Count; ligne += Math.Sign(sens))
        {
            if (CaseEn(ligne, case_.Column) is { } voisine) return voisine;
        }

        return null;
    }

    /// <summary>
    /// Ajoute une ligne au bas du tableau, ses cases habillées comme celles qui l'entourent — le
    /// compteur du dock fait la même chose —, et rend la première de ses cases : <c>Tab</c> dans
    /// la dernière case y mène.
    /// </summary>
    public TableCell? AjouterUneLigne()
    {
        RowCount = Rows.Count + 1;
        var ligne = Rows.Count - 1;
        return CasesDansLOrdre().FirstOrDefault(c => c.Row == ligne);
    }

    /// <summary>
    /// Le cadre de la case dans le repère du tableau, en points : son coin supérieur gauche, sa
    /// largeur et sa hauteur, tels que <see cref="CellFrames"/> les pose. Rien pour une case mal
    /// repérée.
    /// </summary>
    public (double X, double Y, double Largeur, double Hauteur)? CadreDe(TableCell case_)
    {
        if (case_.Row < 0 || case_.Row >= Rows.Count || case_.Column < 0 || case_.Column >= Columns.Count) return null;

        var x = Columns.Take(case_.Column).Sum(c => c.Width);
        var y = Rows.Take(case_.Row).Sum(r => r.Height);
        return (x, y, Columns[case_.Column].Width, Rows[case_.Row].Height);
    }
}
