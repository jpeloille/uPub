using System;
using System.Collections.Generic;

namespace MyPublisher.Models;

/// <summary>
/// La liste standard des corps, celle d'Office (poste N10) : la liste déroulante de la taille de
/// police de Publisher, de Word et des autres, de 8 à 72 pt. <c>Ctrl+Maj+&gt;</c> et
/// <c>Ctrl+Maj+&lt;</c> y prennent la taille suivante ou précédente — « Augmenter à la taille
/// suivante dans la zone Taille de police », dit l'aide de Publisher en français —, et
/// <c>Ctrl+]</c> et <c>Ctrl+[</c> changent le corps d'un point. N19, qui fait du corps une liste
/// éditable, la reprend.
///
/// Hors de la liste, le pas est celui de Word : au-delà de 72, la dizaine suivante ou
/// précédente ; en deçà de 8, le point entier. Le corps ne descend jamais sous
/// <see cref="Minimum"/>, le minimum du champ Corps de la barre et du dock. Fonctions pures,
/// sans Avalonia : le modèle de vue les appelle, les tests du noyau les éprouvent.
/// </summary>
public static class CorpsStandard
{
    /// <summary>Les tailles de la liste, en points, croissantes.</summary>
    public static IReadOnlyList<double> Tailles { get; } =
        [8, 9, 10, 10.5, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72];

    /// <summary>Le plus petit corps, celui que le champ Corps accepte.</summary>
    public const double Minimum = 1;

    /// <summary>Le pas de la liste au-delà de sa dernière taille, comme Word.</summary>
    private const double PasAuDela = 10;

    /// <summary>Ce qui sépare deux corps qu'on tient pour égaux : un corps lu d'un fichier, ou calculé.</summary>
    private const double Tolerance = 1e-6;

    /// <summary>
    /// La taille de la liste qui suit <paramref name="corps"/> : la première qui le dépasse ;
    /// au-delà de 72, la dizaine suivante (80, 90…) ; en deçà de 8, le point entier suivant.
    /// Un corps hors de la liste, 13 pt, passe à la taille qui le suit, 14 pt.
    /// </summary>
    public static double Suivant(double corps)
    {
        // En deçà de la liste, le point entier suivant, comme Precedent y descend.
        if (corps < Tailles[0] - Tolerance) return Math.Min(Tailles[0], Math.Floor(corps + Tolerance) + 1);

        foreach (var taille in Tailles)
        {
            if (taille > corps + Tolerance) return taille;
        }

        return (Math.Floor((corps + Tolerance) / PasAuDela) + 1) * PasAuDela;
    }

    /// <summary>
    /// La taille de la liste qui précède <paramref name="corps"/> : la dernière qui lui est
    /// inférieure ; au-delà de 72, la dizaine précédente tant qu'elle reste au-dessus de 72 ;
    /// en deçà de 8, le point entier précédent, jamais sous <see cref="Minimum"/>.
    /// </summary>
    public static double Precedent(double corps)
    {
        var derniere = Tailles[^1];
        if (corps > derniere + Tolerance)
        {
            var dizaine = (Math.Ceiling((corps - Tolerance) / PasAuDela) - 1) * PasAuDela;
            return Math.Max(dizaine, derniere);
        }

        for (var i = Tailles.Count - 1; i >= 0; i--)
        {
            if (Tailles[i] < corps - Tolerance) return Tailles[i];
        }

        return Math.Max(Minimum, Math.Ceiling(corps - Tolerance) - 1);
    }

    /// <summary>Le corps d'un point de plus, ou de moins (<c>Ctrl+]</c>, <c>Ctrl+[</c>), jamais sous <see cref="Minimum"/>.</summary>
    public static double DUnPoint(double corps, int sens) => Math.Max(Minimum, corps + Math.Sign(sens));
}
