using System.Collections.Generic;

namespace MyPublisher.Models;

/// <summary>
/// La langue du texte (poste N9) : une étiquette BCP 47 — « fr-FR », « en-US » — que le
/// document porte toujours, qu'un bloc de texte et un fragment de style peuvent changer.
/// Nulle sur le bloc, elle est celle du document ; nulle sur le fragment, celle du bloc.
///
/// Elle ne sert qu'au correcteur (N25), qui doit savoir en quelle langue est chaque mot.
/// Elle n'a <b>aucun effet sur le rendu</b>, et par construction : elle n'entre pas dans
/// <see cref="StyleEffectif"/>, ce que la composition mesure et ce que les deux rendus
/// dessinent. Deux mots de même style et de langues différentes restent donc un seul
/// morceau ; si la langue y entrait, une frontière de langue au milieu d'un mot en ferait deux
/// unités, que le renvoi à la ligne pourrait séparer.
///
/// Une étiquette n'est jamais vérifiée ni corrigée : une langue inconnue reste écrite telle
/// quelle, comme l'import la lit.
/// </summary>
public static class Langues
{
    /// <summary>
    /// La langue d'un document vierge, et de tout document qui n'en écrit pas — ceux d'avant
    /// N9. Jamais la langue de la machine : la culture reste la culture courante (règle 8 de la
    /// phase I), mais la langue du document n'en dépend pas, et un même fichier ne se lit pas
    /// autrement sous <c>LC_ALL=C</c>.
    /// </summary>
    public const string ParDefaut = "fr-FR";

    /// <summary>
    /// La langue qu'écrivent les fragments au caractère <paramref name="position"/> d'un texte
    /// de <paramref name="longueur"/> caractères, ou null s'ils s'en remettent au bloc. Le
    /// dernier fragment qui en écrit une l'emporte : la règle de
    /// <see cref="StyleEffectif.Decouper"/>, et la même étendue.
    /// </summary>
    public static string? DesFragments(IReadOnlyList<StyleSpan> fragments, int longueur, int position)
    {
        string? langue = null;
        foreach (var fragment in fragments)
        {
            if (fragment.Langue is null) continue;
            var (debut, fin) = StyleEffectif.Etendue(fragment, longueur);
            if (debut <= position && position < fin) langue = fragment.Langue;
        }

        return langue;
    }

    /// <summary>La langue du bloc : la sienne, sinon celle du document.</summary>
    public static string DuBloc(TextFrame bloc, Document document) => bloc.Langue ?? document.Langue;

    /// <summary>
    /// La langue du caractère <paramref name="position"/> d'un bloc : celle de ses fragments,
    /// sinon celle du bloc, sinon celle du document.
    /// </summary>
    public static string DuCaractere(TextFrame bloc, int position, Document document) =>
        DesFragments(bloc.Spans, bloc.Text.Length, position) ?? DuBloc(bloc, document);

    /// <summary>
    /// La langue du caractère <paramref name="position"/> d'une case de tableau : celle de ses
    /// fragments, sinon celle du document — ni la case ni le tableau n'en portent.
    /// </summary>
    public static string DuCaractere(TableCell cellule, int position, Document document) =>
        DesFragments(cellule.Spans, cellule.Text.Length, position) ?? document.Langue;
}
