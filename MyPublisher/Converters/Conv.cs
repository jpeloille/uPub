using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MyPublisher.Models;

namespace MyPublisher.Converters;

/// <summary>
/// Convertisseurs de liaison. Ils existent pour que le modèle reste indépendant
/// d'Avalonia : les couleurs y sont des chaînes, les alignements des énumérations
/// maison, et la traduction vers les types Avalonia se fait ici, dans la vue.
/// </summary>
public static class Conv
{
    public static readonly FuncValueConverter<string?, IBrush?> Brush =
        new(value => string.IsNullOrWhiteSpace(value) ? null : TryBrush(value));

    public static readonly FuncValueConverter<bool, FontWeight> Weight =
        new(bold => bold ? FontWeight.Bold : FontWeight.Normal);

    public static readonly FuncValueConverter<bool, FontStyle> Slant =
        new(italic => italic ? FontStyle.Italic : FontStyle.Normal);

    public static readonly FuncValueConverter<TextAlign, TextAlignment> Align =
        new(align => align switch
        {
            TextAlign.Center => TextAlignment.Center,
            TextAlign.Right => TextAlignment.Right,
            TextAlign.Justify => TextAlignment.Justify,
            _ => TextAlignment.Left
        });

    public static readonly FuncValueConverter<ImageFit, Stretch> Fit =
        new(fit => fit switch
        {
            ImageFit.UniformToFill => Stretch.UniformToFill,
            ImageFit.Fill => Stretch.Fill,
            _ => Stretch.Uniform
        });

    /// <summary>
    /// Les transformations Avalonia ne sont pas dans l'arbre logique : leur DataContext
    /// n'est donc pas hérité et on ne peut pas y placer une liaison. On produit
    /// l'objet transformation complet depuis un convertisseur.
    /// </summary>
    public static readonly FuncValueConverter<double, ITransform?> Rotate =
        new(angle => angle == 0 ? null : new RotateTransform(angle));

    public static readonly FuncValueConverter<double, ITransform> Scale =
        new(factor => new ScaleTransform(factor, factor));

    public static readonly FuncValueConverter<string?, FontFamily> Font =
        new(name => string.IsNullOrWhiteSpace(name) ? FontFamily.Default : new FontFamily(name));

    public static readonly FuncValueConverter<Insets, Thickness> Pad =
        new(insets => new Thickness(insets.Left, insets.Top, insets.Right, insets.Bottom));

    /// <summary>Épaisseur d'un repère : une ligne de base se pose plus légèrement.</summary>
    public static readonly FuncValueConverter<bool, double> GuideWeight =
        new(isBaseline => isBaseline ? 0.2 : 0.4);

    /// <summary>Teinte d'un repère : le bleu des marges, pâli pour les lignes de base.</summary>
    public static readonly FuncValueConverter<bool, IBrush> GuideTint =
        new(isBaseline => new SolidColorBrush(Color.Parse(isBaseline ? "#FFD3E4F4" : "#FF7FB2E5")));

    /// <summary>Motif du trait, en multiples de l'épaisseur ; null laisse le trait continu.</summary>
    public static readonly FuncValueConverter<StrokeStyle, AvaloniaList<double>?> Dash =
        new(style => Frame.DashPattern(style) is { } pattern ? new AvaloniaList<double>(pattern) : null);

    public static readonly FuncValueConverter<string?, Bitmap?> Image =
        new(path => string.IsNullOrWhiteSpace(path) ? null : BitmapCache.Get(path));

    /// <summary>Points -> millimètres, pour l'affichage dans la barre d'état.</summary>
    public static readonly FuncValueConverter<double, string> Millimetres =
        new(points => Units.PointsToMm(points).ToString("0.0", CultureInfo.CurrentCulture) + " mm");

    public static readonly FuncValueConverter<double, string> Percent =
        new(ratio => (ratio * 100).ToString("0", CultureInfo.CurrentCulture) + " %");

    private static IBrush? TryBrush(string value)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(value));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>
/// Les images sont relues à chaque notification de liaison : sans cache, faire
/// glisser un bloc image relirait le fichier à chaque image de l'animation.
/// </summary>
internal static class BitmapCache
{
    private static readonly Dictionary<string, Bitmap?> Cache = new();

    public static Bitmap? Get(string path)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var cached)) return cached;

            Bitmap? bitmap = null;
            try
            {
                bitmap = new Bitmap(path);
            }
            catch (Exception)
            {
                // Fichier absent ou format non géré : le bloc reste vide, sans planter.
            }

            Cache[path] = bitmap;
            return bitmap;
        }
    }

    public static void Invalidate(string path)
    {
        lock (Cache) Cache.Remove(path);
    }
}
