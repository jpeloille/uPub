using System.Globalization;

namespace MyPublisher.Models;

/// <summary>
/// Les couleurs du modèle sont des chaînes #AARRGGBB, pour qu'il ne dépende d'aucun
/// type graphique. Ce qui doit les lire ou les recomposer — l'export, l'opacité du
/// panneau — passe par ici, afin qu'une seule analyse fasse foi.
/// </summary>
public static class HexColor
{
    /// <summary>Analyse #AARRGGBB ou #RRGGBB ; ce dernier est réputé opaque.</summary>
    public static bool TryParse(string? value, out byte a, out byte r, out byte g, out byte b)
    {
        a = r = g = b = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var hex = value.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        if (hex.Length != 8 ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return false;
        }

        a = (byte)(packed >> 24);
        r = (byte)(packed >> 16);
        g = (byte)(packed >> 8);
        b = (byte)packed;
        return true;
    }

    public static string Format(byte a, byte r, byte g, byte b) => $"#{a:X2}{r:X2}{g:X2}{b:X2}";

    /// <summary>La même teinte avec un autre octet alpha ; une chaîne illisible est rendue telle quelle.</summary>
    public static string WithAlpha(string value, byte alpha) =>
        TryParse(value, out _, out var r, out var g, out var b) ? Format(alpha, r, g, b) : value;
}
