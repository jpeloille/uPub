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

    public StyleSpan Clone() => new()
    {
        Start = Start, Length = Length, FontFamily = FontFamily, FontSize = FontSize,
        Bold = Bold, Italic = Italic, Underline = Underline, Foreground = Foreground,
        BaselineShift = BaselineShift,
    };
}

/// <summary>
/// Le style effectif d'une portion de texte : ce que le bloc dit, corrigé par le
/// fragment qui la recouvre. C'est ce que la composition mesure et ce que les deux
/// rendus dessinent — jamais le bloc ni le fragment pris séparément.
///
/// Le souligné y entre comme le gras (poste I7) : il ne change aucune mesure, mais deux
/// portions qui ne se soulignent pas pareil sont deux morceaux, et chaque rendu trace le
/// trait de chacun (<see cref="Compositeur.Soulignes"/>).
/// </summary>
public readonly record struct StyleEffectif(
    string FontFamily, double FontSize, bool Bold, bool Italic, Souligne Underline,
    string Foreground, double BaselineShift)
{
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
            var debut = Math.Clamp(fragment.Start, 0, texte.Length);
            var fin = Math.Clamp(fragment.Start + fragment.Length, debut, texte.Length);

            for (var i = debut; i < fin; i++)
            {
                styles[i] = new StyleEffectif(
                    fragment.FontFamily ?? styles[i].FontFamily,
                    fragment.FontSize ?? styles[i].FontSize,
                    fragment.Bold ?? styles[i].Bold,
                    fragment.Italic ?? styles[i].Italic,
                    fragment.Underline ?? styles[i].Underline,
                    fragment.Foreground ?? styles[i].Foreground,
                    fragment.BaselineShift != 0 ? fragment.BaselineShift : styles[i].BaselineShift);
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
}
