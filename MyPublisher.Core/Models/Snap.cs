using System;
using System.Collections.Generic;

namespace MyPublisher.Models;

/// <summary>
/// L'aimantation, et la seule chose qui la borne.
///
/// Ce calcul vivait dans <c>Views/PageCanvas.axaml.cs</c>, en <c>private static</c> :
/// il était déjà pur — trois nombres et une liste de nombres — mais aucun test ne
/// pouvait l'atteindre, si bien que les recettes de C2a et C2b ont dû l'éprouver en
/// synthétisant des pointeurs sur la vraie couche de saisie. Le plan le disait
/// lui-même : « ce qui est exact, ce sont les positions aimantées, qui sortent de la
/// table des repères ». L'oracle est ici ; c'est donc ici qu'il doit pouvoir être
/// interrogé.
/// </summary>
public static class Snap
{
    /// <summary>
    /// Taille minimale d'un bloc.
    ///
    /// Elle était déclarée dans la vue, alors que c'est un invariant du modèle — et
    /// le commentaire qui l'accompagnait reconnaissait que le panneau de propriétés
    /// devait exprimer la même valeur sans que rien ne l'impose. Deux sources de
    /// vérité, aucune vérification : elle n'en a plus qu'une.
    /// </summary>
    public static readonly double MinFrameSize = Units.MmToPoints(2);

    /// <summary>
    /// Aimante l'un des trois bords du bloc (début, milieu, fin) sur le repère le
    /// plus proche, si l'écart reste sous la tolérance.
    /// </summary>
    public static double Edge(double position, double size, double tolerance,
                             IReadOnlyList<double> guides)
    {
        var best = position;
        var bestDistance = tolerance;

        foreach (var guide in guides)
        {
            foreach (var offset in new[] { 0.0, size / 2, size })
            {
                var candidate = guide - offset;
                var distance = Math.Abs(candidate - position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
        }

        return best;
    }
}
