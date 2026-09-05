using System.Collections.Generic;

namespace MyPublisher.Models;

/// <summary>
/// Toute la géométrie du document est exprimée en points PostScript (1/72 pouce),
/// l'unité native du PDF. Avalonia raisonne en pixels indépendants du périphérique
/// (1/96 pouce) : les conversions sont centralisées ici et nulle part ailleurs.
/// </summary>
public static class Units
{
    public const double PointsPerInch = 72.0;
    public const double DipsPerInch = 96.0;
    public const double MmPerInch = 25.4;

    /// <summary>Facteur points -> pixels Avalonia à 100 % de zoom.</summary>
    public const double PointToDip = DipsPerInch / PointsPerInch;

    public static double MmToPoints(double mm) => mm * PointsPerInch / MmPerInch;
    public static double PointsToMm(double points) => points * MmPerInch / PointsPerInch;
}

/// <summary>Format de page, en points.</summary>
public sealed record PageSize(string Name, double Width, double Height)
{
    public static readonly PageSize A3 = new("A3", 841.89, 1190.55);
    public static readonly PageSize A4 = new("A4", 595.28, 841.89);
    public static readonly PageSize A5 = new("A5", 419.53, 595.28);
    public static readonly PageSize Letter = new("Letter", 612.0, 792.0);
    public static readonly PageSize Legal = new("Legal", 612.0, 1008.0);

    public static IReadOnlyList<PageSize> All { get; } = [A3, A4, A5, Letter, Legal];

    /// <summary>
    /// Le format du catalogue qui correspond à ces dimensions, dans un sens ou dans
    /// l'autre ; nul pour un format libre. La tolérance d'un point absorbe les valeurs
    /// arrondies qu'on trouve dans les fichiers.
    /// </summary>
    public static PageSize? Match(double width, double height)
    {
        var shortSide = System.Math.Min(width, height);
        var longSide = System.Math.Max(width, height);
        foreach (var size in All)
        {
            if (System.Math.Abs(size.Width - shortSide) < 1 && System.Math.Abs(size.Height - longSide) < 1) return size;
        }

        return null;
    }

    public override string ToString() => $"{Name} ({Units.PointsToMm(Width):0} × {Units.PointsToMm(Height):0} mm)";
}
