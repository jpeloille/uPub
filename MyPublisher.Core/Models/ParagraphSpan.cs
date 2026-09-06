using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

/// <summary>
/// Les retraits et l'espacement d'une suite de paragraphes, quand ils s'écartent de ce
/// que le bloc dit pour tous. C'est le pendant de <see cref="StyleSpan"/> à l'échelle
/// du paragraphe.
///
/// Pourquoi des INDICES DE PARAGRAPHE, là où <see cref="StyleSpan"/> repère des
/// positions de caractères. Trois raisons, et un prix.
///
/// Un retrait appartient au paragraphe entier : il n'en a pas de moitié. Repérer par
/// caractère permettrait d'écrire « les trente premiers caractères de ce paragraphe
/// sont en retrait », qui ne se dessine pas, et obligerait à inventer une règle pour le
/// paragraphe à demi couvert. Une règle inventée s'applique en silence.
///
/// Ensuite, un indice de paragraphe survit à la frappe ordinaire : taper un mot décale
/// toutes les positions de caractères qui le suivent, et ne décale aucun indice de
/// paragraphe. Seule l'entrée d'un saut de ligne les déplace, et c'est bien plus rare.
///
/// Enfin, c'est ce que la source donne : <c>pub2raw</c> énumère des paragraphes et pose
/// <c>fo:margin-left</c> sur chacun. L'import du poste R5 comptera ce qu'il lit, au
/// lieu de convertir en positions ce qu'il aurait ensuite à recompter.
///
/// Le prix, qu'il faut dire : le fichier porte désormais deux systèmes de repérage, et
/// R5 devra tenir les deux à jour dans le même import. Comme pour les fragments de
/// style, un écart aux bornes absurdes est ramené à ce qu'il peut couvrir plutôt que
/// refusé — un import qui compte mal doit donner un document laid, jamais un document
/// illisible.
///
/// Chaque propriété est facultative : nulle veut dire « comme le bloc ». Les
/// trente-six paragraphes en retrait négatif d'ATA 21 — ses listes à puces — ne coûtent
/// donc qu'un indice, une longueur et deux nombres.
/// </summary>
public partial class ParagraphSpan : ObservableObject
{
    /// <summary>Indice du premier paragraphe concerné ; le premier du bloc porte 0.</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(10)]
    private int _start;

    /// <summary>
    /// Nombre de paragraphes concernés. Un par défaut, et non zéro comme la longueur
    /// d'un fragment de style : un écart écrit sans longueur porte sur son seul
    /// paragraphe, ce qui est le cas courant, plutôt que sur aucun — un écart qui ne
    /// fait rien ne se remarque pas.
    /// </summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(20)]
    private int _length = 1;

    /// <summary>Retrait gauche du paragraphe, en points (<c>fo:margin-left</c>).</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(30)]
    private double? _indentLeft;

    /// <summary>Retrait droit, en points (<c>fo:margin-right</c>).</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(40)]
    private double? _indentRight;

    /// <summary>
    /// Retrait de la première ligne seule, en points (<c>fo:text-indent</c>), compté à
    /// partir du retrait gauche. Négatif, il fait pendre la première ligne à gauche des
    /// suivantes : c'est ainsi que Publisher compose ses listes à puces, et les cinq
    /// valeurs relevées sur ATA 21 sont toutes négatives.
    /// </summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(50)]
    private double? _indentFirstLine;

    /// <summary>
    /// Espace avant le paragraphe, en points (<c>fo:margin-top</c>).
    ///
    /// Il s'applique aussi au <b>premier</b> paragraphe du bloc, qui descend donc
    /// d'autant sous la marge intérieure haute. C'est ce que fait Publisher dans une
    /// zone de texte, là où Word l'escamote en tête de page. Choisi sans mesure, et
    /// c'est à dire : le relevé des deux <c>.pub</c> ne porte que <c>margin-bottom</c>,
    /// vingt-neuf fois, et pas un seul <c>margin-top</c>.
    /// </summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(60)]
    private double? _spaceBefore;

    /// <summary>Espace après le paragraphe, en points (<c>fo:margin-bottom</c>).</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(70)]
    private double? _spaceAfter;

    public ParagraphSpan Clone() => new()
    {
        Start = Start, Length = Length, IndentLeft = IndentLeft, IndentRight = IndentRight,
        IndentFirstLine = IndentFirstLine, SpaceBefore = SpaceBefore, SpaceAfter = SpaceAfter,
    };
}

/// <summary>
/// Les réglages effectifs d'un paragraphe : ce que le bloc dit, corrigé par l'écart qui
/// le recouvre. C'est ce que la composition consomme — jamais le bloc ni l'écart pris
/// séparément.
/// </summary>
public readonly record struct ParagrapheEffectif(
    double IndentLeft, double IndentRight, double IndentFirstLine,
    double SpaceBefore, double SpaceAfter)
{
    /// <summary>
    /// Les réglages de chacun des <paramref name="nombre"/> paragraphes du bloc. Les
    /// écarts qui se chevauchent, débordent ou remontent le temps sont ramenés à
    /// quelque chose de lisible plutôt que refusés, et le dernier posé l'emporte —
    /// même règle que pour les fragments de style, pour la même raison.
    /// </summary>
    public static IReadOnlyList<ParagrapheEffectif> Resoudre(
        int nombre, IReadOnlyList<ParagraphSpan> ecarts, ParagrapheEffectif commun)
    {
        var reglages = new ParagrapheEffectif[Math.Max(nombre, 1)];
        Array.Fill(reglages, commun);

        foreach (var ecart in ecarts)
        {
            var debut = Math.Clamp(ecart.Start, 0, reglages.Length);
            // Le total se calcule en long : un écart écrivant « Start = 2 147 483 647,
            // Length = 1 » ferait déborder l'addition, et la borne repartirait à
            // gauche du texte au lieu de le dépasser.
            var fin = (int)Math.Clamp((long)ecart.Start + ecart.Length, debut, reglages.Length);

            for (var i = debut; i < fin; i++)
            {
                reglages[i] = new ParagrapheEffectif(
                    ecart.IndentLeft ?? reglages[i].IndentLeft,
                    ecart.IndentRight ?? reglages[i].IndentRight,
                    ecart.IndentFirstLine ?? reglages[i].IndentFirstLine,
                    ecart.SpaceBefore ?? reglages[i].SpaceBefore,
                    ecart.SpaceAfter ?? reglages[i].SpaceAfter);
            }
        }

        return reglages;
    }
}
