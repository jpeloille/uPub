using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MyPublisher.Models;

namespace MyPublisher.Converters;

/// <summary>
/// Le modèle est en points, mais l'utilisateur compose en millimètres : la
/// conversion est bidirectionnelle et se fait au plus près du champ de saisie.
///
/// La valeur n'est pas arrondie au passage. Elle l'était, à deux décimales, et cela
/// suffisait à altérer le document : la liaison étant à double sens, la valeur
/// arrondie revenait aussitôt dans le modèle, si bien qu'une gouttière de 12 pt —
/// 4,2333 mm — devenait 11,99 pt à la seule ouverture du document, sans geste ni
/// pas d'annulation. L'affichage est arrondi là où il doit l'être, par le
/// « FormatString » du champ, qui ne touche pas à la valeur.
///
/// Ne pas arrondir ne suffisait pas encore, et c'est le poste Q14 qui l'a mesuré : le
/// champ renvoie au modèle la valeur qu'il vient de recevoir, dès qu'un bloc est
/// sélectionné, et l'aller-retour points → millimètres → points n'est pas l'identité
/// en virgule flottante. L'ordonnée d'un bloc d'ATA 21, 107.15039999999999, revenait
/// 107.1504 ; un bloc à 100 revenait 100.00000000000007, une page de 595,28 pt
/// 595.2800000000013. Rien ne se voyait, mais le document n'était plus le même au
/// caractère près, sans qu'on ait rien tapé. D'où la mémoire de <see cref="Affiches"/>.
/// </summary>
public sealed class PointsToMillimetres : IValueConverter
{
    public static readonly PointsToMillimetres Instance = new();

    private readonly Affiches _affiches = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double points ? _affiches.Retenir((decimal)Units.PointsToMm(points), points) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        decimal millimetres when _affiches.Retrouver(millimetres, out var points) => points,
        decimal millimetres => Units.MmToPoints((double)millimetres),
        double millimetres => Units.MmToPoints(millimetres),
        _ => AvaloniaProperty.UnsetValue
    };
}

/// <summary>
/// Les nombres qu'un champ du panneau affiche, et la valeur exacte du modèle dont chacun
/// a été tiré.
///
/// Un champ numérique renvoie au modèle la valeur qu'il vient de recevoir, dès que le
/// bloc ou la page qu'il montre est affiché — rien ne l'a tapée. Si la conversion vers
/// le champ perd quoi que ce soit, ce renvoi réécrit le document en silence : c'est le
/// défaut du poste Q14. Aucune arithmétique ne le rend exact — le décimal ne garde que
/// quinze chiffres d'un double, et l'arrondi d'affichage en garde moins encore —, si
/// bien que le convertisseur se souvient : un nombre qui lui revient tel qu'il l'a
/// donné rend la valeur dont il venait. Un nombre qu'il n'a jamais donné — une frappe,
/// une flèche du champ — est converti comme avant.
///
/// La mémoire est partagée par tous les champs d'un même convertisseur. Deux valeurs
/// du modèle qui s'affichent sous le même nombre se confondent donc : c'est la
/// dernière affichée qui revient.
/// </summary>
internal sealed class Affiches
{
    /// <summary>
    /// Au-delà, la mémoire repart de zéro. Elle ne sert qu'à reconnaître un nombre que
    /// le panneau affiche encore — une vingtaine de champs —, pas à tout garder d'une
    /// session.
    /// </summary>
    private const int Capacite = 256;

    private readonly Dictionary<decimal, double> _valeurs = [];

    public decimal Retenir(decimal affiche, double valeur)
    {
        if (_valeurs.Count >= Capacite) _valeurs.Clear();
        _valeurs[affiche] = valeur;
        return affiche;
    }

    public bool Retrouver(decimal affiche, out double valeur) => _valeurs.TryGetValue(affiche, out valeur);
}

/// <summary>Passerelle entre les couleurs stockées en #AARRGGBB et le sélecteur de couleur.</summary>
public sealed class HexToColor : IValueConverter
{
    public static readonly HexToColor Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text)) return Colors.Transparent;
        return Color.TryParse(text, out var color) ? color : Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Color color ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}" : AvaloniaProperty.UnsetValue;
}

/// <summary>
/// NumericUpDown expose des décimaux ; le modèle des doubles.
///
/// L'arrondi à quatre décimales reste, pour le champ ; il ne remonte plus dans le
/// modèle. Mesuré au poste Q14 : sélectionner un bloc tourné de 12.3456789° le
/// laissait à 12.3457°, et un angle posé à la poignée de rotation n'est jamais rond.
/// </summary>
public sealed class DoubleToDecimal : IValueConverter
{
    public static readonly DoubleToDecimal Instance = new();

    private readonly Affiches _affiches = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double number && !double.IsNaN(number) ? _affiches.Retenir((decimal)Math.Round(number, 4), number) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        decimal number when _affiches.Retrouver(number, out var exact) => exact,
        decimal number => (double)number,
        double number => number,
        _ => AvaloniaProperty.UnsetValue
    };
}

/// <summary>
/// Relie un bouton bascule à une valeur d'énumération : le bouton est enfoncé quand
/// l'énumération vaut le paramètre. Décocher ne fait rien — c'est l'activation d'un
/// autre outil qui relâche celui-ci, si bien qu'un outil reste toujours armé.
/// </summary>
public sealed class EnumToBool : IValueConverter
{
    public static readonly EnumToBool Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null &&
        string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null) return BindingOperations.DoNothing;

        var name = parameter.ToString();
        return name is not null && Enum.IsDefined(targetType, name)
            ? Enum.Parse(targetType, name)
            : BindingOperations.DoNothing;
    }
}

/// <summary>
/// Texte réellement affiché par un bloc : le littéral, ou la valeur du champ quand il
/// y en a un. Trois entrées — le texte, le champ, le contexte — parce que les deux
/// premières changent avec le bloc et la troisième avec la page courante. La résolution
/// elle-même est celle du modèle, la même que l'export appelle.
/// </summary>
public sealed class FieldToText : IMultiValueConverter
{
    public static readonly FieldToText Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // Pendant la mise en place des liaisons, une valeur peut manquer : on rend alors
        // le littéral s'il est là, plutôt qu'un texte calculé de travers.
        var literal = values.Count > 0 && values[0] is string text ? text : "";
        if (values.Count < 3 || values[1] is not FieldKind field || values[2] is not FieldContext context)
        {
            return literal;
        }

        return FieldResolver.Resolve(field, literal, context);
    }
}

/// <summary>
/// Image réellement affichée par un bloc : celle de son chemin, ou le logo du jeu de
/// coordonnées quand le bloc porte le champ correspondant. Même découpage que
/// <see cref="FieldToText"/>, et le même résolveur.
/// </summary>
public sealed class FieldToImage : IMultiValueConverter
{
    public static readonly FieldToImage Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var literal = values.Count > 0 && values[0] is string path ? path : "";
        var resolved = values.Count >= 3 && values[1] is FieldKind field && values[2] is FieldContext context
            ? FieldResolver.Resolve(field, literal, context)
            : literal;

        return string.IsNullOrWhiteSpace(resolved) ? null : BitmapCache.Get(resolved);
    }
}
