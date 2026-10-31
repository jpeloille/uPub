using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace MyPublisher.Models;

/// <summary>Ce qu'est la liste d'un paragraphe : aucune, des puces, ou des numéros (poste N23).</summary>
public enum SorteDeListe { Aucune, Puces, Numeros }

/// <summary>
/// La forme d'un numéro : « 1. », « a) » ou « i. » — les trois que la spécification de N23 nomme,
/// parmi celles du dialogue <i>Puces et numéros</i> de Publisher (p. 24).
/// </summary>
public enum FormatDeNumero { Chiffres, Lettres, Romains }

/// <summary>
/// La liste d'un paragraphe (poste N23 ; fiche 23, b12) : aucune, des puces — le caractère, « • »
/// par défaut —, ou des numéros, avec leur forme et leur premier numéro. Une propriété de
/// paragraphe, sur le bloc (<see cref="TextFrame.Liste"/>) et sur l'écart
/// (<see cref="ParagraphSpan.Liste"/>), comme le fer depuis R5 : une clé additive (règle 15), nulle
/// dans tous les documents écrits avant le poste, qui se composent comme avant.
///
/// <b>Une valeur, et non un objet qu'on retouche.</b> Un enregistrement immuable, égal à un autre
/// quand ses quatre réglages le sont : la mise en forme d'une plage (<see cref="MiseEnForme"/>)
/// compare les écarts pour les fusionner, et la numérotation compare les paragraphes voisins pour
/// les enchaîner. Le changer, c'est en poser un autre.
///
/// <b>La numérotation</b> (<see cref="Listes.Numeros"/>) : des paragraphes consécutifs qui portent
/// la même liste numérotée — les quatre réglages égaux — se numérotent de proche en proche, à
/// partir de <see cref="Premier"/>, ou de 1 sans lui ; un paragraphe hors de cette liste — sans
/// liste, à puces, d'une autre forme ou d'un autre premier numéro — interrompt la suite, et le
/// premier numéro d'un paragraphe la fait repartir. Entrée dans un élément continue donc la liste
/// sans rien de plus : le recalage de Q1 donne au paragraphe neuf l'écart de celui qu'il coupe.
/// </summary>
public sealed record ListeDeParagraphe
{
    /// <summary>La puce de Publisher et d'Office.</summary>
    public const string PuceParDefaut = "•";

    /// <summary>Aucune liste, des puces, ou des numéros.</summary>
    [JsonPropertyOrder(10)]
    public SorteDeListe Sorte { get; init; }

    /// <summary>Le caractère de la puce ; sans effet pour une liste numérotée.</summary>
    [JsonPropertyOrder(20)]
    public string Puce { get; init; } = PuceParDefaut;

    /// <summary>La forme du numéro ; sans effet pour une liste à puces.</summary>
    [JsonPropertyOrder(30)]
    public FormatDeNumero Format { get; init; }

    /// <summary>
    /// Le premier numéro de la suite, ou nul pour 1. Nul et 1 se numérotent de même, mais ne sont
    /// pas la même liste : c'est ce qui laisse repartir à 1 une suite qui suit une autre suite
    /// partie de 1 (<see cref="Listes.Recommencer"/>).
    /// </summary>
    [JsonPropertyOrder(40)]
    public int? Premier { get; init; }

    /// <summary>Le paragraphe est dans une liste, à puces ou numérotée.</summary>
    [JsonIgnore]
    public bool EstUneListe => Sorte != SorteDeListe.Aucune;

    /// <summary>Pas de liste : ce qu'un écart pose pour sortir son paragraphe d'une liste que le bloc porte.</summary>
    public static ListeDeParagraphe Aucune { get; } = new();

    /// <summary>Des puces, de ce caractère.</summary>
    public static ListeDeParagraphe DesPuces(string puce = PuceParDefaut) =>
        new() { Sorte = SorteDeListe.Puces, Puce = string.IsNullOrEmpty(puce) ? PuceParDefaut : puce };

    /// <summary>Des numéros, de cette forme, à partir de ce premier numéro.</summary>
    public static ListeDeParagraphe DesNumeros(FormatDeNumero format, int? premier = null) =>
        new() { Sorte = SorteDeListe.Numeros, Format = format, Premier = premier };
}

/// <summary>
/// Ce que la composition et l'interface font des listes (poste N23) : la marque de chaque
/// paragraphe, et la pose d'une liste sur des paragraphes, avec le retrait suspendu d'Office.
/// Tout est fonction du texte, des écarts et des réglages du bloc, sur le modèle de
/// <see cref="MiseEnForme"/>, et s'éprouve sans fenêtre.
/// </summary>
public static class Listes
{
    /// <summary>
    /// Le retrait suspendu qu'une liste donne à un paragraphe sans retrait : le texte à 18 pt, la
    /// marque pendue à 0 — « Indent list by 0.25" » du dialogue de Publisher, et le quart de pouce
    /// d'Office.
    /// </summary>
    public const double RetraitSuspendu = 18;

    /// <summary>
    /// La liste qui se dessine : nulle pour « aucune ». Une liste <see cref="SorteDeListe.Aucune"/>
    /// et l'absence de liste ne font qu'un, pour la composition comme pour la comparaison de deux
    /// paragraphes.
    /// </summary>
    public static ListeDeParagraphe? Effective(ListeDeParagraphe? liste) => liste is { EstUneListe: true } ? liste : null;

    /// <summary>
    /// Le numéro de chaque paragraphe, nul hors d'une liste numérotée. Des paragraphes consécutifs
    /// de la même liste se suivent ; tout autre paragraphe interrompt la suite, et la suivante
    /// repart à son premier numéro.
    /// </summary>
    public static IReadOnlyList<int?> Numeros(IReadOnlyList<ParagrapheEffectif> reglages)
    {
        var numeros = new int?[reglages.Count];
        ListeDeParagraphe? precedente = null;
        var numero = 0;

        for (var i = 0; i < reglages.Count; i++)
        {
            var liste = Effective(reglages[i].Liste);
            if (liste is not { Sorte: SorteDeListe.Numeros })
            {
                precedente = null;
                continue;
            }

            numero = liste == precedente ? numero + 1 : liste.Premier ?? 1;
            numeros[i] = numero;
            precedente = liste;
        }

        return numeros;
    }

    /// <summary>La marque de chaque paragraphe — « • », « 3. », « c) » — ou nulle hors liste.</summary>
    public static IReadOnlyList<string?> Marques(IReadOnlyList<ParagrapheEffectif> reglages)
    {
        var numeros = Numeros(reglages);
        var marques = new string?[reglages.Count];
        for (var i = 0; i < reglages.Count; i++)
        {
            marques[i] = Effective(reglages[i].Liste) switch
            {
                { Sorte: SorteDeListe.Puces } puces => string.IsNullOrEmpty(puces.Puce) ? ListeDeParagraphe.PuceParDefaut : puces.Puce,
                { Sorte: SorteDeListe.Numeros } liste => Marque(liste.Format, numeros[i] ?? 1),
                _ => null,
            };
        }

        return marques;
    }

    /// <summary>
    /// Le numéro <paramref name="numero"/> écrit dans la forme <paramref name="format"/> : « 3. »,
    /// « c) », « iii. ». Les lettres vont de « a » à « z », puis « aa », « bb »… comme Office,
    /// jusqu'à trente lettres ; les chiffres romains jusqu'à 3 999. Hors de ces bornes — zéro, un nombre négatif, au-delà —,
    /// le numéro s'écrit en chiffres, avec le signe de sa forme.
    /// </summary>
    public static string Marque(FormatDeNumero format, int numero) => format switch
    {
        FormatDeNumero.Lettres => (numero is >= 1 and <= LettresAuPlus ? Lettres(numero) : Chiffres(numero)) + ")",
        FormatDeNumero.Romains => (numero is >= 1 and <= 3999 ? Romains(numero) : Chiffres(numero)) + ".",
        _ => Chiffres(numero) + ".",
    };

    /// <summary>
    /// Le dernier numéro qui s'écrit en lettres : « zzzzzzzzzzzzzzzzzzzzzzzzzzzzzz », trente fois la
    /// même. Au-delà, des chiffres : un premier numéro aberrant, venu d'un fichier, ne doit pas
    /// écrire une marque de plusieurs millions de lettres.
    /// </summary>
    private const int LettresAuPlus = 26 * 30;

    private static string Chiffres(int numero) => numero.ToString(CultureInfo.InvariantCulture);

    private static string Lettres(int numero)
    {
        var lettre = (char)('a' + (numero - 1) % 26);
        return new string(lettre, (numero - 1) / 26 + 1);
    }

    private static string Romains(int numero)
    {
        ReadOnlySpan<int> valeurs = [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];
        string[] signes = ["m", "cm", "d", "cd", "c", "xc", "l", "xl", "x", "ix", "v", "iv", "i"];
        var romain = new StringBuilder();
        for (var i = 0; i < valeurs.Length; i++)
        {
            while (numero >= valeurs[i])
            {
                romain.Append(signes[i]);
                numero -= valeurs[i];
            }
        }

        return romain.ToString();
    }

    // ------------------------------------------------------------------ poser une liste

    /// <summary>
    /// Pose la liste <paramref name="liste"/> sur les paragraphes de la plage, en écarts
    /// (<see cref="MiseEnForme.Poser(string, IList{ParagraphSpan}, ParagrapheEffectif, Plage, ReglagesDeLaPlage)"/>).
    /// Un paragraphe qui entre dans une liste sans aucun retrait — ni à gauche, ni de première
    /// ligne — reçoit le retrait suspendu d'Office (<see cref="RetraitSuspendu"/>) ; un paragraphe
    /// qui en sort avec exactement ce retrait le rend. Les autres retraits ne bougent pas : celui
    /// qu'on a réglé à la main est gardé. Rend vrai si la collection a changé.
    /// </summary>
    public static bool Poser(
        string texte, IList<ParagraphSpan> ecarts, ParagrapheEffectif commun, Plage paragraphes, ListeDeParagraphe liste)
    {
        var nombre = Recalage.NombreDeParagraphes(texte);
        var debut = Math.Clamp(paragraphes.Start, 0, nombre - 1);
        var fin = Math.Clamp(paragraphes.Start + Math.Max(1, paragraphes.Length), debut + 1, nombre);
        var effectifs = ParagrapheEffectif.Resoudre(nombre, Lecture(ecarts), commun);

        var change = false;
        for (var i = debut; i < fin; i++)
        {
            var (gauche, premiere) = Retraits(effectifs[i], liste);
            change |= MiseEnForme.Poser(texte, ecarts, commun, new Plage(i, 1),
                new ReglagesDeLaPlage { Liste = liste, IndentLeft = gauche, IndentFirstLine = premiere });
        }

        return change;
    }

    /// <summary>
    /// Pose la liste sur le bloc entier, hors saisie : sa liste, et le retrait suspendu s'il n'a
    /// aucun retrait — ou le retrait rendu s'il sort d'une liste avec exactement celui-là. Ses
    /// écarts ne changent pas, comme pour les autres réglages du bloc (R6). Rend vrai si le bloc a
    /// changé.
    /// </summary>
    public static bool PoserSurLeBloc(TextFrame bloc, ListeDeParagraphe liste)
    {
        var avant = bloc.Paragraph;
        var (gauche, premiere) = Retraits(bloc.Paragraph, liste);
        bloc.Liste = Effective(liste);
        if (gauche is { } g) bloc.IndentLeft = g;
        if (premiere is { } p) bloc.IndentFirstLine = p;
        return bloc.Paragraph != avant;
    }

    /// <summary>
    /// Les retraits qu'une liste posée change sur un paragraphe : le retrait suspendu pour un
    /// paragraphe sans retrait qui entre dans une liste, zéro pour un paragraphe qui en sort avec
    /// le retrait suspendu ; nuls — rien à changer — sinon.
    /// </summary>
    private static (double? Gauche, double? Premiere) Retraits(ParagrapheEffectif reglage, ListeDeParagraphe liste)
    {
        var entre = liste.EstUneListe && Effective(reglage.Liste) is null;
        var sort = !liste.EstUneListe && Effective(reglage.Liste) is not null;

        if (entre && reglage.IndentLeft == 0 && reglage.IndentFirstLine == 0)
        {
            return (RetraitSuspendu, -RetraitSuspendu);
        }

        if (sort && reglage.IndentLeft == RetraitSuspendu && reglage.IndentFirstLine == -RetraitSuspendu)
        {
            return (0, 0);
        }

        return (null, null);
    }

    /// <summary>
    /// Fait repartir la numérotation au paragraphe <paramref name="paragraphe"/>, à
    /// <paramref name="premier"/> : sa liste reçoit ce premier numéro, et avec lui les paragraphes
    /// qui le suivaient dans la même suite — ils restent la suite du paragraphe, et non le
    /// commencement d'une autre. Repartir à 1 juste après une suite de même forme qui écrit son
    /// premier numéro, 1, se fait par la liste « sans premier numéro » : deux listes égales se
    /// suivraient sans repartir. Rien si le paragraphe n'est pas numéroté. Rend vrai si la
    /// collection a changé.
    /// </summary>
    public static bool Recommencer(
        string texte, IList<ParagraphSpan> ecarts, ParagrapheEffectif commun, int paragraphe, int premier)
    {
        var nombre = Recalage.NombreDeParagraphes(texte);
        if (paragraphe < 0 || paragraphe >= nombre) return false;

        var effectifs = ParagrapheEffectif.Resoudre(nombre, Lecture(ecarts), commun);
        if (Effective(effectifs[paragraphe].Liste) is not { Sorte: SorteDeListe.Numeros } liste) return false;

        var fin = paragraphe + 1;
        while (fin < nombre && Effective(effectifs[fin].Liste) == liste) fin++;

        var neuve = liste with { Premier = premier };
        if (paragraphe > 0 && Effective(effectifs[paragraphe - 1].Liste) == neuve && premier == 1)
        {
            neuve = liste with { Premier = null };
        }

        return MiseEnForme.Poser(texte, ecarts, commun, new Plage(paragraphe, fin - paragraphe),
            new ReglagesDeLaPlage { Liste = neuve });
    }

    private static IReadOnlyList<T> Lecture<T>(IList<T> liste) => liste as IReadOnlyList<T> ?? [.. liste];
}
