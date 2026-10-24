using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MyPublisher.Converters;

/// <summary>
/// La hauteur d'une vignette du panneau Pages (poste I5) : sa largeur, au rapport de la
/// page. Valeurs liées : la largeur et la hauteur de la page du document, qui changent en
/// place quand on change de format ; la largeur de la vignette en paramètre — 96 px dans
/// la maquette, cadre compris.
///
/// La hauteur se calcule sur la boîte entière, cadre compris, et non sur le dedans : la
/// page courante prend un cadre de 2 px au lieu de 1, et une vignette qui grandirait ou
/// rapetisserait d'un pixel quand on la choisit ferait glisser les suivantes.
/// </summary>
public sealed class HauteurDeVignette : IMultiValueConverter
{
    public static readonly HauteurDeVignette Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not double largeurDePage || values[1] is not double hauteurDePage ||
            largeurDePage <= 0 || hauteurDePage <= 0 ||
            !double.TryParse(parameter?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var largeur))
        {
            return AvaloniaProperty.UnsetValue;
        }

        return Math.Round(largeur * hauteurDePage / largeurDePage);
    }
}
