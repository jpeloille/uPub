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
/// </summary>
public sealed class PointsToMillimetres : IValueConverter
{
    public static readonly PointsToMillimetres Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double points ? (decimal)Units.PointsToMm(points) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        decimal millimetres => Units.MmToPoints((double)millimetres),
        double millimetres => Units.MmToPoints(millimetres),
        _ => AvaloniaProperty.UnsetValue
    };
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

/// <summary>NumericUpDown expose des décimaux ; le modèle des doubles.</summary>
public sealed class DoubleToDecimal : IValueConverter
{
    public static readonly DoubleToDecimal Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double number && !double.IsNaN(number) ? (decimal)Math.Round(number, 4) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
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
