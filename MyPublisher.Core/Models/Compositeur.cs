using System;
using System.Collections.Generic;

namespace MyPublisher.Models;

/// <summary>
/// Une ligne composée : son texte, la largeur qu'elle occupe, et si elle termine son
/// paragraphe.
///
/// Le drapeau n'est pas un détail : la dernière ligne d'un paragraphe justifié ne se
/// justifie pas. Sans lui, « fin. » se retrouverait étiré sur toute la largeur du
/// bloc.
/// </summary>
public readonly record struct LigneComposee(string Texte, double Largeur, bool FinDeParagraphe);

/// <summary>
/// Découpe un texte en lignes pour une largeur donnée.
///
/// Pourquoi ne pas laisser <c>XTextFormatter</c> le faire, comme jusqu'ici : parce
/// qu'il ne publie pas sa découpe et n'expose aucun réglage d'avance de ligne. Tant
/// qu'on lui déléguait la composition, l'interligne du modèle ne franchissait pas
/// l'export — mesuré au poste P3 : 12,00 pt à l'écran contre 13,93 pt au PDF, et
/// 18,00 contre 13,93 à interligne 1,5, c'est-à-dire aucun effet du tout.
///
/// La mesure est passée en paramètre plutôt que codée ici : le noyau ne connaît ni
/// Avalonia ni PDFsharp, et c'est ce qui permet aux deux rendus d'appeler la même
/// découpe avec leur propre règle à mesurer.
/// </summary>
public static class Compositeur
{
    /// <summary>
    /// Coupe le texte aux espaces, sans jamais dépasser <paramref name="largeur"/>.
    /// Les retours à la ligne saisis sont respectés ; un mot plus large que la boîte
    /// occupe sa ligne entière plutôt que d'être coupé au milieu — c'est ce que fait
    /// Publisher, et couper un mot à l'aveugle ferait pire que déborder.
    /// </summary>
    public static IReadOnlyList<LigneComposee> Composer(
        string texte, double largeur, Func<string, double> mesurer)
    {
        var lignes = new List<LigneComposee>();
        if (string.IsNullOrEmpty(texte)) return lignes;

        // Un texte venu d'ailleurs peut porter les trois conventions de fin de ligne.
        foreach (var paragraphe in texte.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (paragraphe.Length == 0)
            {
                // Une ligne vide est une ligne : elle occupe sa hauteur.
                lignes.Add(new LigneComposee(string.Empty, 0, FinDeParagraphe: true));
                continue;
            }

            var courante = string.Empty;

            foreach (var mot in Mots(paragraphe))
            {
                var essai = courante.Length == 0 ? mot : courante + mot;
                if (courante.Length > 0 && mesurer(essai.TrimEnd()) > largeur)
                {
                    var pleine = courante.TrimEnd();
                    lignes.Add(new LigneComposee(pleine, mesurer(pleine), FinDeParagraphe: false));
                    courante = mot.TrimStart();
                }
                else
                {
                    courante = essai;
                }
            }

            var reste = courante.TrimEnd();
            lignes.Add(new LigneComposee(reste, mesurer(reste), FinDeParagraphe: true));
        }

        return lignes;
    }

    /// <summary>
    /// Les mots d'un paragraphe, chacun avec les espaces qui le suivent : garder
    /// l'espace attaché évite d'avoir à le réinventer au moment de recoller, et de
    /// perdre les espaces multiples que l'auteur a voulus.
    /// </summary>
    private static IEnumerable<string> Mots(string paragraphe)
    {
        var debut = 0;

        while (debut < paragraphe.Length)
        {
            var fin = paragraphe.IndexOf(' ', debut);
            if (fin < 0) { yield return paragraphe[debut..]; yield break; }

            // On absorbe la suite d'espaces avec le mot qui précède.
            var apres = fin;
            while (apres < paragraphe.Length && paragraphe[apres] == ' ') apres++;

            yield return paragraphe[debut..apres];
            debut = apres;
        }
    }

    /// <summary>
    /// Hauteur occupée par un texte composé : le nombre de lignes multiplié par
    /// l'avance. C'est la mesure dont dépendent l'alignement vertical (A7) et
    /// l'avertissement de débordement (A2).
    /// </summary>
    public static double Hauteur(int lignes, double avance) => lignes * avance;

    /// <summary>
    /// Avance d'une ligne à la suivante. <paramref name="naturelle"/> est la hauteur
    /// de ligne de la police elle-même ; le multiple vient du modèle.
    ///
    /// C'est la sémantique retenue le 6 septembre 2026, et elle vient des documents
    /// réels : aucun des deux <c>.pub</c> déposés ne porte <c>fo:line-height</c>, ce
    /// qui veut dire qu'ils s'en tiennent au défaut de Publisher — la hauteur
    /// naturelle de la police. Un interligne « 1 » ne doit donc rien serrer, et
    /// « 1,25 » veut dire une fois et quart cette hauteur-là, jamais une fois et
    /// quart le corps.
    /// </summary>
    public static double Avance(double naturelle, double multiple) =>
        naturelle * (multiple > 0 ? multiple : 1);
}
