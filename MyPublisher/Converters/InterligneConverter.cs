using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MyPublisher.Models;

namespace MyPublisher.Converters;

/// <summary>
/// L'avance d'une ligne à la suivante, pour l'écran.
///
/// Le modèle exprime l'interligne en multiple de la hauteur **naturelle** de la
/// police, et non du corps — sémantique retenue le 6 septembre 2026 sur la foi des
/// deux documents Publisher réels, qui ne portent aucun interligne et s'en tiennent
/// donc au défaut, lequel est la hauteur naturelle.
///
/// Avant cela, l'écran avançait de <c>FontSize × LineHeight</c> : à interligne 1 il
/// serrait le texte à 12,00 pt là où la police en demande 13,93, et l'export, qui
/// laissait faire PDFsharp, ne suivait pas du tout. Les deux rendus divergeaient sur
/// tout document portant plus d'une ligne.
///
/// La hauteur naturelle se lit dans les métriques de la police, comme le fait
/// <c>XFont.GetHeight()</c> côté PDF. C'est le seul endroit de la vue qui ait besoin
/// de ces métriques ; le calcul lui-même est celui du noyau
/// (<see cref="Compositeur.Avance"/>).
/// </summary>
public sealed class InterligneConverter : IMultiValueConverter
{
    public static readonly InterligneConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter,
                          CultureInfo culture)
    {
        // Ordre attendu : famille, corps, gras, italique, interligne.
        if (values.Count < 5 ||
            values[0] is not string famille ||
            values[1] is not double corps ||
            values[4] is not double multiple)
        {
            return double.NaN;   // NaN : Avalonia retombe sur la police, ce qui est le bon défaut.
        }

        var gras = values[2] as bool? ?? false;
        var italique = values[3] as bool? ?? false;

        var naturelle = HauteurNaturelle(famille, corps, gras, italique);
        return naturelle > 0 ? Compositeur.Avance(naturelle, multiple) : double.NaN;
    }

    /// <summary>
    /// Hauteur de ligne de la police elle-même, en points : montée, descente et
    /// interligne du dessinateur de caractères, ramenés au corps demandé.
    /// </summary>
    public static double HauteurNaturelle(string famille, double corps, bool gras, bool italique)
    {
        try
        {
            var typeface = new Typeface(
                new FontFamily(famille),
                italique ? FontStyle.Italic : FontStyle.Normal,
                gras ? FontWeight.Bold : FontWeight.Normal);

            var metriques = typeface.GlyphTypeface.Metrics;

            // La montée est comptée vers le haut, donc négative.
            var em = (double)metriques.DesignEmHeight;
            if (em <= 0) return 0;

            return (-metriques.Ascent + metriques.Descent + metriques.LineGap) * corps / em;
        }
        catch (Exception)
        {
            // Une famille absente ne doit pas empêcher d'afficher la page : on rend
            // zéro, l'appelant retombe sur le défaut d'Avalonia.
            return 0;
        }
    }
}
