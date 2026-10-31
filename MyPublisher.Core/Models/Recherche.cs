using System;
using System.Collections.Generic;
using System.Linq;

namespace MyPublisher.Models;

/// <summary>
/// Ce qu'on cherche (poste N20, fiche 23, e23) : le texte, et les deux options de Publisher,
/// *Respecter la casse* et *Mot entier*.
/// </summary>
/// <param name="Cherche">Le texte cherché ; vide, il n'y a rien à trouver.</param>
/// <param name="RespecterLaCasse">« Bulletin » ne trouve pas « bulletin ».</param>
/// <param name="MotEntier">« bulle » ne trouve pas le début de « bulletin » : l'occurrence n'a, de part et
/// d'autre, ni lettre ni chiffre.</param>
public readonly record struct CriteresDeRecherche(string Cherche, bool RespecterLaCasse = false, bool MotEntier = false);

/// <summary>
/// Rechercher et remplacer dans le texte des blocs et des cases (poste N20). Tout est pur ici,
/// sauf les deux <see cref="Remplacer(TextFrame, Plage, string)"/>, qui écrivent le texte et
/// recalent ses fragments : le remplacement passe par le noyau, sans passer par l'écran.
///
/// La comparaison est ordinale : sans la casse, elle replie les majuscules sur les minuscules
/// caractère pour caractère (<see cref="StringComparison.OrdinalIgnoreCase"/>), si bien qu'une
/// occurrence a toujours la longueur du texte cherché, et que les accents comptent — « élève »
/// ne trouve pas « eleve », comme dans Publisher. Une comparaison selon la culture pourrait
/// trouver une occurrence d'une autre longueur, et la culture des tests n'est pas celle de
/// l'utilisateur (règle 8 de la phase I).
/// </summary>
public static class Recherche
{
    /// <summary>
    /// La première occurrence qui commence à <paramref name="depuis"/> ou après, ou rien. Les
    /// occurrences ne se chevauchent pas : celles que <see cref="Occurrences"/> rend, de gauche à
    /// droite, sont celles qu'on trouve en repartant chaque fois de la fin de la précédente.
    /// </summary>
    public static Plage? Suivante(string texte, CriteresDeRecherche criteres, int depuis)
    {
        var cherche = criteres.Cherche;
        if (string.IsNullOrEmpty(cherche) || string.IsNullOrEmpty(texte)) return null;

        var comparaison = criteres.RespecterLaCasse ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        for (var debut = Math.Clamp(depuis, 0, texte.Length); debut <= texte.Length - cherche.Length; debut++)
        {
            var trouve = texte.IndexOf(cherche, debut, comparaison);
            if (trouve < 0) return null;

            var plage = new Plage(trouve, cherche.Length);
            if (!criteres.MotEntier || EstUnMotEntier(texte, plage)) return plage;

            debut = trouve;
        }

        return null;
    }

    /// <summary>Toutes les occurrences, de gauche à droite, sans chevauchement.</summary>
    public static IReadOnlyList<Plage> Occurrences(string texte, CriteresDeRecherche criteres)
    {
        var occurrences = new List<Plage>();
        for (var depuis = 0; Suivante(texte, criteres, depuis) is { } plage; depuis = plage.Start + plage.Length)
        {
            occurrences.Add(plage);
        }

        return occurrences;
    }

    /// <summary>
    /// La plage est-elle une occurrence de ce qu'on cherche ? C'est ce que <i>Remplacer</i> lit
    /// avant de remplacer la sélection : une sélection qui n'en est pas une n'est pas remplacée.
    /// </summary>
    public static bool EstUneOccurrence(string texte, Plage plage, CriteresDeRecherche criteres)
    {
        var cherche = criteres.Cherche;
        if (string.IsNullOrEmpty(cherche) || plage.Length != cherche.Length || plage.Start < 0 ||
            plage.Start + plage.Length > texte.Length)
        {
            return false;
        }

        var comparaison = criteres.RespecterLaCasse ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return string.Compare(texte, plage.Start, cherche, 0, cherche.Length, comparaison) == 0 &&
               (!criteres.MotEntier || EstUnMotEntier(texte, plage));
    }

    /// <summary>Ni lettre ni chiffre juste avant l'occurrence, ni juste après.</summary>
    private static bool EstUnMotEntier(string texte, Plage plage)
    {
        var fin = plage.Start + plage.Length;
        return (plage.Start == 0 || !char.IsLetterOrDigit(texte[plage.Start - 1])) &&
               (fin >= texte.Length || !char.IsLetterOrDigit(texte[fin]));
    }

    /// <summary>
    /// Le texte où <paramref name="plage"/> est remplacée par <paramref name="par"/>, ses fragments
    /// et ses écarts de paragraphe recalés sur place, avant que le texte ne change (voir
    /// <see cref="Recalage.Appliquer(string, string, Edition, IList{StyleSpan}, IList{ParagraphSpan})"/>).
    /// L'édition est dite, et non déduite des deux textes : le texte remplacé prend le style de son
    /// premier caractère, la règle de Q1, même quand le remplacement commence ou finit comme lui.
    /// </summary>
    public static string Remplacer(string texte, Plage plage, string par, IList<StyleSpan> fragments,
                                   IList<ParagraphSpan>? ecarts)
    {
        var apres = string.Concat(texte.AsSpan(0, plage.Start), par, texte.AsSpan(plage.Start + plage.Length));
        Recalage.Appliquer(texte, apres, new Edition(plage.Start, plage.Length, par.Length), fragments, ecarts);
        return apres;
    }

    /// <summary>Remplace une occurrence dans un bloc de texte, ses fragments et ses écarts suivant.</summary>
    public static void Remplacer(TextFrame bloc, Plage plage, string par) =>
        bloc.Text = Remplacer(bloc.Text ?? "", plage, par, bloc.Spans, bloc.Paragraphs);

    /// <summary>Remplace une occurrence dans une case : une case n'a pas d'écarts de paragraphe.</summary>
    public static void Remplacer(TableCell case_, Plage plage, string par) =>
        case_.Text = Remplacer(case_.Text ?? "", plage, par, case_.Spans, null);

    /// <summary>
    /// Les occurrences d'un texte, de la dernière à la première : remplacées dans cet ordre, celles
    /// qui restent à faire ne bougent pas, et un remplacement qui contient ce qu'on cherche ne se
    /// retrouve pas.
    /// </summary>
    public static IEnumerable<Plage> AReboursDe(string texte, CriteresDeRecherche criteres) =>
        Occurrences(texte, criteres).Reverse();
}
