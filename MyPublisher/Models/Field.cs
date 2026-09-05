using System;
using System.Globalization;

namespace MyPublisher.Models;

/// <summary>
/// Ce qu'un bloc affiche à la place de son texte littéral. « None » est le cas
/// courant : le bloc dit ce qu'on y a saisi. Les autres valeurs font calculer le
/// contenu au moment où on l'affiche ou l'exporte — un folio se numérote donc seul,
/// et la même page dupliquée huit fois porte huit numéros différents.
/// </summary>
public enum FieldKind
{
    None,
    PageNumber,
    PageCount,
    Date
}

/// <summary>
/// Ce qu'il faut savoir pour résoudre un champ : où l'on est dans le document, et
/// quand. Un contexte par page à l'export, un pour la page courante à l'écran.
/// </summary>
public sealed record FieldContext(int PageNumber, int PageCount, DateTime Date);

/// <summary>
/// Résolution des champs. Fonction pure, appelée telle quelle par le rendu écran et
/// par l'export : c'est ce qui garantit que le folio du PDF est celui qu'on voyait.
/// </summary>
public static class FieldResolver
{
    /// <summary>
    /// Le français, quel que soit le poste. Un document est composé une fois et exporté
    /// n'importe où : si la date suivait la culture de la machine, le même fichier
    /// imprimerait « 5 septembre » chez l'un et « 5 September » chez l'autre. Le PDF
    /// étant le livrable, il ne doit dépendre que du document. C'est la même règle que
    /// pour la géométrie : une seule représentation, aucune dérive.
    /// </summary>
    private static readonly CultureInfo Language = CultureInfo.GetCultureInfo("fr-FR");

    public static string Resolve(FieldKind field, string literal, FieldContext context) => field switch
    {
        // Invariant pour les nombres : un folio s'écrit « 1000 », jamais « 1,000 ».
        FieldKind.PageNumber => context.PageNumber.ToString(CultureInfo.InvariantCulture),
        FieldKind.PageCount => context.PageCount.ToString(CultureInfo.InvariantCulture),
        FieldKind.Date => context.Date.ToString("d MMMM yyyy", Language),
        _ => literal
    };

    /// <summary>Libellé français du champ, pour la liste du panneau de propriétés.</summary>
    public static string Label(FieldKind field) => field switch
    {
        FieldKind.PageNumber => "Numéro de page",
        FieldKind.PageCount => "Nombre de pages",
        FieldKind.Date => "Date du jour",
        _ => "Aucun — texte saisi"
    };
}

/// <summary>Un champ et son libellé, tels que la liste du panneau les présente.</summary>
public sealed record FieldOption(FieldKind Kind, string Label);
