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

    /// <summary>
    /// La même teinte avec un autre octet alpha ; une chaîne illisible est rendue telle quelle.
    ///
    /// Une référence au jeu de couleurs garde sa référence et prend l'opacité avec elle
    /// (<c>scheme:accent1/80</c>) : régler l'opacité d'un fond ne doit pas le détacher du
    /// jeu. Opaque, elle retombe sur la forme courte, celle que pose une pastille.
    /// </summary>
    public static string WithAlpha(string value, byte alpha)
    {
        if (ReferenceDuJeu.TryCouleur(value, out var emplacement, out _))
        {
            return ReferenceDuJeu.Couleur(emplacement, alpha == 0xFF ? null : alpha);
        }

        return TryParse(value, out _, out var r, out var g, out var b) ? Format(alpha, r, g, b) : value;
    }

    /// <summary>
    /// L'octet alpha d'une couleur, ou null si elle est illisible. Une référence au jeu
    /// sans opacité propre est réputée opaque : c'est ce qu'elle rend avec les jeux
    /// livrés, dont aucune couleur n'est transparente.
    /// </summary>
    public static byte? Alpha(string? value)
    {
        if (ReferenceDuJeu.TryCouleur(value, out _, out var propre)) return propre ?? 0xFF;
        return TryParse(value, out var a, out _, out _, out _) ? a : null;
    }
}
