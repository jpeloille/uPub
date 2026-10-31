using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

/// <summary>
/// Un fragment de style : une portion du texte du bloc qui s'écarte du style commun.
///
/// Pourquoi des portions repérées par position plutôt que des morceaux de texte
/// autonomes : parce que le texte reste alors la seule source des caractères. Un
/// fichier <c>.mypub</c> se relit toujours à l'œil, la recherche continue de porter
/// sur <c>Text</c>, et un bloc dont on retirerait tous les fragments retombe
/// exactement sur ce qu'il était. C'est aussi la façon dont Publisher s'y prend —
/// ses pages FDPC décrivent des propriétés de caractère posées sur un flux de texte.
///
/// Chaque propriété est facultative : nulle veut dire « comme le bloc ». Un gras au
/// milieu d'une phrase ne coûte donc qu'une position, une longueur et un booléen, et
/// non la répétition de la police, du corps et de la couleur.
///
/// Ce que cela permet, et qui manquait : les deux documents ATR déposés le
/// 6 septembre 2026 portent 402 fragments de style, jusqu'à sept dans un seul
/// paragraphe. Tant que le bloc n'avait qu'une police, aucun des deux ne pouvait être
/// reproduit.
/// </summary>
public partial class StyleSpan : ObservableObject
{
    /// <summary>Position du premier caractère concerné, dans <c>TextFrame.Text</c>.</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(10)]
    private int _start;

    /// <summary>Nombre de caractères concernés.</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(20)]
    private int _length;

    [ObservableProperty]
    [property: JsonPropertyOrder(30)]
    private string? _fontFamily;

    [ObservableProperty]
    [property: JsonPropertyOrder(40)]
    private double? _fontSize;

    [ObservableProperty]
    [property: JsonPropertyOrder(50)]
    private bool? _bold;

    [ObservableProperty]
    [property: JsonPropertyOrder(60)]
    private bool? _italic;

    /// <summary>
    /// Le souligné de la portion (poste I7). Nul, comme le bloc ; <c>false</c> dans le
    /// fichier, explicitement aucun — un mot qu'on retire du souligné de son bloc.
    /// </summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(65)]
    private Souligne? _underline;

    [ObservableProperty]
    [property: JsonPropertyOrder(70)]
    private string? _foreground;

    /// <summary>
    /// Décalage de la ligne de base, en multiple du corps : positif pour un exposant,
    /// négatif pour un indice. Les fiches ATR s'en servent quatre fois.
    /// </summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(80)]
    private double _baselineShift;

    /// <summary>
    /// Les capitales de la portion (poste N24) : nulle, comme le bloc ;
    /// <c>Aucune</c> dans le fichier, explicitement aucune — un mot
    /// qu'on retire des capitales de son bloc. La composition dessine en capitales sans
    /// toucher au texte (<see cref="Compositeur"/>). Une clé additive : un fichier qui ne
    /// l'écrit pas se lit nul, et <c>FormatVersion</c> reste à 5 (règle 15).
    /// </summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(85)]
    private Capitales? _capitales;

    /// <summary>
    /// La langue de la portion, une étiquette BCP 47 (poste N9) ; nulle, celle du bloc. Elle
    /// n'entre pas dans <see cref="StyleEffectif"/> : la composition et les deux rendus ne la
    /// voient pas (<see cref="Langues"/>). L'import d'un <c>.pub</c> la lit sur chaque
    /// fragment de libmspub.
    /// </summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(90)]
    private string? _langue;

    public StyleSpan Clone() => new()
    {
        Start = Start, Length = Length, FontFamily = FontFamily, FontSize = FontSize,
        Bold = Bold, Italic = Italic, Underline = Underline, Foreground = Foreground,
        BaselineShift = BaselineShift, Capitales = Capitales, Langue = Langue,
    };
}

/// <summary>
/// Les capitales d'un texte (poste N24 ; fiche 23, b14) : <i>Majuscules</i> et <i>Petites
/// majuscules</i> de la barre Formatting de Publisher (p. 10). Elles ne changent pas le texte —
/// la recherche, la copie et l'orthographe le lisent tel qu'il est tapé — : la composition le
/// dessine en capitales, et fabrique les petites capitales en dessinant les minuscules en
/// capitales réduites (<see cref="Compositeur.ReductionDesPetitesCapitales"/>).
/// </summary>
public enum Capitales
{
    /// <summary>Le texte tel qu'il est tapé.</summary>
    Aucune,

    /// <summary>Tout en capitales, à la hauteur des capitales.</summary>
    Capitales,

    /// <summary>Les minuscules en capitales réduites ; les capitales tapées restent pleines.</summary>
    PetitesCapitales,
}

/// <summary>
/// Le style effectif d'une portion de texte : ce que le bloc dit, corrigé par le
/// fragment qui la recouvre. C'est ce que la composition mesure et ce que les deux
/// rendus dessinent — jamais le bloc ni le fragment pris séparément.
///
/// Le souligné y entre comme le gras (poste I7) : il ne change aucune mesure, mais deux
/// portions qui ne se soulignent pas pareil sont deux morceaux, et chaque rendu trace le
/// trait de chacun (<see cref="Compositeur.Soulignes"/>).
///
/// Les capitales y entrent de même (poste N24), hors des membres positionnels : un style
/// construit sans elles n'en a pas. Deux portions de capitales différentes sont deux morceaux,
/// que la composition dessine chacun à sa façon.
/// </summary>
public readonly record struct StyleEffectif(
    string FontFamily, double FontSize, bool Bold, bool Italic, Souligne Underline,
    string Foreground, double BaselineShift)
{
    /// <summary>Les capitales de la portion (poste N24) ; aucune par défaut.</summary>
    public Capitales Capitales { get; init; }

    /// <summary>
    /// Découpe le texte d'un bloc en portions de style homogène. Les fragments qui se
    /// chevauchent, débordent ou remontent le temps sont ramenés à quelque chose de
    /// lisible plutôt que refusés : un document ne doit pas devenir illisible parce
    /// qu'un import a mal compté les caractères.
    /// </summary>
    public static IReadOnlyList<(string Texte, StyleEffectif Style)> Decouper(
        string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun)
    {
        var morceaux = new List<(string, StyleEffectif)>();
        if (texte.Length == 0) return morceaux;

        // Un style par caractère, le commun d'abord, puis chaque fragment par-dessus :
        // c'est la façon la plus courte de traiter les chevauchements sans les
        // énumérer, et le dernier fragment posé l'emporte, comme dans un éditeur.
        var styles = new StyleEffectif[texte.Length];
        Array.Fill(styles, commun);

        foreach (var fragment in fragments)
        {
            var (debut, fin) = Etendue(fragment, texte.Length);

            for (var i = debut; i < fin; i++)
            {
                styles[i] = new StyleEffectif(
                    fragment.FontFamily ?? styles[i].FontFamily,
                    fragment.FontSize ?? styles[i].FontSize,
                    fragment.Bold ?? styles[i].Bold,
                    fragment.Italic ?? styles[i].Italic,
                    fragment.Underline ?? styles[i].Underline,
                    fragment.Foreground ?? styles[i].Foreground,
                    fragment.BaselineShift != 0 ? fragment.BaselineShift : styles[i].BaselineShift)
                {
                    Capitales = fragment.Capitales ?? styles[i].Capitales,
                };
            }
        }

        // Puis on recolle les caractères voisins qui partagent le même style.
        var debutMorceau = 0;
        for (var i = 1; i <= texte.Length; i++)
        {
            if (i < texte.Length && styles[i] == styles[debutMorceau]) continue;
            morceaux.Add((texte[debutMorceau..i], styles[debutMorceau]));
            debutMorceau = i;
        }

        return morceaux;
    }

    /// <summary>
    /// Les caractères qu'un fragment recouvre dans un texte de <paramref name="longueur"/>
    /// caractères : ses bornes ramenées dans le texte, la fin jamais avant le début. C'est là
    /// que <see cref="Decouper"/> le dessine, et là que <see cref="MiseEnForme"/> le coupe :
    /// les deux doivent le voir au même endroit (poste N2).
    ///
    /// La fin se calcule en long, comme celle d'un écart de paragraphe et comme au
    /// recalage. En entier, « Start = 2, Length = 2 147 483 647 » faisait déborder
    /// l'addition : le fragment, qui court jusqu'au bout du texte, ne recouvrait plus rien.
    /// </summary>
    internal static (int Debut, int Fin) Etendue(StyleSpan fragment, int longueur)
    {
        var debut = Math.Clamp(fragment.Start, 0, longueur);
        return (debut, (int)Math.Clamp((long)fragment.Start + fragment.Length, debut, longueur));
    }
}
