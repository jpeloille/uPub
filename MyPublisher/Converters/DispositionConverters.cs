using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using MyPublisher.Models;
using MyPublisher.ViewModels;

namespace MyPublisher.Converters;

/// <summary>
/// Les libellés français des réglages que le dock propose en liste. Le modèle garde ses
/// noms d'énumération, que le fichier écrit ; la maquette dit « Haut », « Plein »,
/// « Aucun ». Les éléments des listes restent les valeurs du modèle : seul leur gabarit
/// passe par ici, et un test qui choisit <c>VerticalAlign.Middle</c> le choisit encore.
/// </summary>
public static class Libelles
{
    public static string De(object? valeur) => valeur switch
    {
        TextAlign.Left => "Gauche",
        TextAlign.Center => "Centré",
        TextAlign.Right => "Droite",
        TextAlign.Justify => "Justifié",
        VerticalAlign.Top => "Haut",
        VerticalAlign.Middle => "Milieu",
        VerticalAlign.Bottom => "Bas",
        ImageFit.Uniform => "Contenue",
        ImageFit.UniformToFill => "Remplie",
        ImageFit.Fill => "Étirée",
        StrokeStyle.Solid => "Plein",
        StrokeStyle.Dashed => "Tirets",
        StrokeStyle.Dotted => "Points",
        FillKind.Solid => "Uni",
        FillKind.Gradient => "Dégradé",
        FillKind.Image => "Image",
        FillKind.Tint => "Teinte",
        null => "",
        _ => valeur.ToString() ?? "",
    };
}

/// <summary>Le libellé d'une valeur d'énumération, pour le gabarit d'une liste du dock.</summary>
public sealed class Libelle : IValueConverter
{
    public static readonly Libelle Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Libelles.De(value);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}

/// <summary>
/// Un nombre et son unité dans la même valeur — « 52,0 mm », « 0° », « 1,15 × » —, comme
/// la maquette les écrit dans ses champs. C'est la décision du 24 octobre 2026 : le dock
/// porte l'unité dans la valeur, et non plus dans le libellé (« Largeur (mm) »).
///
/// **Le texte, et lui seul.** Ce convertisseur est le <c>TextConverter</c> du champ
/// numérique : il passe entre la valeur du champ et le texte qu'il montre. La valeur, elle,
/// reste liée au modèle par les convertisseurs de Q14 et leur mémoire
/// (<see cref="PointsToMillimetres"/>, <see cref="DoubleToDecimal"/>) : l'arrondi
/// d'affichage ne remonte jamais au document. Et un texte qu'on n'a pas touché ne se relit
/// pas : le champ rend sa valeur telle quelle quand son texte est celui qu'il a écrit.
///
/// **Pourquoi pas un « FormatString ».** « 0.0 mm » s'affiche bien, mais se relit mal : le
/// champ d'Avalonia, faute de savoir lire l'unité, retire du texte tapé TOUT ce qui n'est
/// pas un chiffre — le séparateur décimal compris —, et « 52,5 mm » devenait 525.
///
/// **La saisie** accepte le nombre seul ou suivi de son unité, et la virgule comme le
/// point : sous la locale du système, l'un des deux est le séparateur décimal, et l'autre
/// ne peut pas y être un séparateur de milliers sans rendre « 4,2 » égal à 42. La culture
/// reste la culture courante (règle 8) : sous <c>LC_ALL=C</c>, c'est un point qui s'écrit.
/// </summary>
public sealed class Unite : IValueConverter
{
    /// <summary>Positions et tailles : la maquette les écrit au dixième, « 52,0 mm ».</summary>
    public static readonly Unite Millimetres = new("0.0", "mm");

    /// <summary>Les autres longueurs — gouttières, marges, retraits — sans zéros inutiles : « 4,2 mm », « 1 mm ».</summary>
    public static readonly Unite MillimetresCourts = new("0.##", "mm");

    /// <summary>Corps, filets, ligne de base : le point typographique.</summary>
    public static readonly Unite Points = new("0.##", "pt");

    public static readonly Unite Degres = new("0.#", "°", espace: false);

    /// <summary>L'interligne, en multiple de la hauteur naturelle (R4) : « 1,15 × ».</summary>
    public static readonly Unite Multiple = new("0.00", "×");

    public static readonly Unite Pourcent = new("0", "%");

    private readonly string _format;
    private readonly string _unite;
    private readonly bool _espace;

    private Unite(string format, string unite, bool espace = true)
    {
        _format = format;
        _unite = unite;
        _espace = espace;
    }

    // Le sens est celui du champ numérique, qui est l'inverse d'une liaison : il appelle
    // « ConvertBack » pour écrire sa valeur en texte, et « Convert » pour lire un texte.

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        decimal nombre => nombre.ToString(_format, culture) + (_espace ? " " : "") + _unite,
        double nombre => nombre.ToString(_format, culture) + (_espace ? " " : "") + _unite,
        _ => null,
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string texte) return null;

        texte = texte.Trim();
        if (texte.EndsWith(_unite, StringComparison.OrdinalIgnoreCase)) texte = texte[..^_unite.Length];
        texte = texte.Replace(" ", "").Replace("\u00A0", "").Replace("\u202F", "");

        var separateur = culture.NumberFormat.NumberDecimalSeparator;
        texte = separateur == "," ? texte.Replace('.', ',') : texte.Replace(',', '.');

        // Un texte qui ne se lit pas lève : le champ numérique attrape l'erreur et garde la
        // valeur d'avant, au lieu d'en recevoir une nulle qui effacerait la mesure.
        return decimal.Parse(texte, NumberStyles.Float, culture);
    }
}

/// <summary>
/// Le format de la page, pour la barre d'état : « A4 portrait », « A3 paysage », ou ses
/// dimensions en millimètres pour un format libre — c'est la même reconnaissance que le
/// sélecteur de format du dock (<see cref="PageSize.Match"/>). Valeurs liées : la largeur,
/// puis la hauteur du document.
/// </summary>
public sealed class FormatDePage : IMultiValueConverter
{
    public static readonly FormatDePage Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not double largeur || values[1] is not double hauteur) return "";

        if (PageSize.Match(largeur, hauteur) is { } format)
        {
            return $"{format.Name} {(largeur > hauteur ? "paysage" : "portrait")}";
        }

        return string.Format(culture, "{0:0.#} × {1:0.#} mm", Units.PointsToMm(largeur), Units.PointsToMm(hauteur));
    }
}

/// <summary>
/// Le curseur de zoom de la barre d'état, en échelle logarithmique : de 10 % à 800 %, le
/// milieu du rail est à 89 % et non à 405 %. Un pas du curseur multiplie le zoom, comme
/// les boutons + et −, au lieu de l'augmenter d'autant à 10 % qu'à 800 %. Les bornes sont
/// celles de <see cref="MainWindowViewModel.SetZoom"/>.
/// </summary>
public sealed class EchelleDuZoom : IValueConverter
{
    public static readonly EchelleDuZoom Instance = new();

    public static readonly double Minimum = Math.Log(MainWindowViewModel.ZoomMinimum);
    public static readonly double Maximum = Math.Log(MainWindowViewModel.ZoomMaximum);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double zoom && zoom > 0 ? Math.Log(zoom) : AvaloniaPropertyUnset();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double position
            ? Math.Clamp(Math.Exp(position), MainWindowViewModel.ZoomMinimum, MainWindowViewModel.ZoomMaximum)
            : BindingOperations.DoNothing;

    private static object AvaloniaPropertyUnset() => Avalonia.AvaloniaProperty.UnsetValue;
}

/// <summary>
/// L'initiale du type dans « Blocs de la page », comme la maquette : la touche de l'outil
/// qui trace le bloc — T, R, E, B pour le tableau — et I pour l'image, G pour un groupe.
/// Un titre est un bloc de texte du modèle : il porte le T.
/// </summary>
public sealed class InitialeDuType : IValueConverter
{
    public static readonly InitialeDuType Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TextFrame => "T",
        ImageFrame => "I",
        TableFrame => "B",
        GroupFrame => "G",
        ShapeFrame { IsEllipse: true } => "E",
        ShapeFrame => "R",
        _ => "",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}

/// <summary>
/// Le numéro d'une page dans le panneau Pages : son rang dans la liste, compté à partir de
/// un. Valeurs liées : la page, la liste, et son nombre d'entrées, qui ne sert qu'à faire
/// relire le rang quand une page est insérée ou retirée avant celle-ci.
/// </summary>
public sealed class NumeroDePage : IMultiValueConverter
{
    public static readonly NumeroDePage Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Count >= 2 && values[1] is System.Collections.IList liste && liste.IndexOf(values[0]) is >= 0 and var rang
            ? (rang + 1).ToString(culture)
            : "";
}

/// <summary>
/// La pile d'une page lue de l'avant vers le fond, pour « Blocs de la page » : toujours
/// la même vue pour la même page (<see cref="PileInversee.De"/>).
/// </summary>
public sealed class VueInversee : IValueConverter
{
    public static readonly VueInversee Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IList<Frame> pile ? PileInversee.De(pile) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
