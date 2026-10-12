using System;
using System.Collections.Generic;
using MyPublisher.Models;
using PdfSharp.Drawing;

namespace MyPublisher.Services;

/// <summary>
/// La règle à mesurer du PDF, prêtée au compositeur du noyau.
///
/// Elle vivait cachée dans <see cref="PdfExporter"/> jusqu'au poste Q13. Elle en sort
/// parce que l'écran en a besoin à son tour : la coupure d'une ligne se décide sur la
/// plus large des deux mesures, celle d'Avalonia et celle-ci, et l'écran doit donc
/// savoir ce que le PDF mesurerait (cf. <see cref="Compositeur.IMetriques.LargeurDeCoupure"/>).
/// </summary>
public sealed class MetriquesPdf : Compositeur.IMetriques
{
    private readonly XGraphics _gfx;
    private readonly Compositeur.IMetriques? _ecran;
    private readonly Dictionary<StyleEffectif, XFont> _polices = [];

    /// <param name="gfx">La surface où l'on dessine, ou un contexte de mesure.</param>
    /// <param name="ecran">
    /// La règle de l'écran, avec laquelle la coupure se décide ; <c>null</c> seulement
    /// là où il n'y a pas d'écran du tout — les tests du noyau, qui ne chargent pas
    /// Avalonia. Le paramètre n'a pas de valeur par défaut pour que chaque appelant le
    /// dise : un export qui l'oublierait recouperait à sa seule mesure, c'est-à-dire
    /// autrement que l'écran.
    /// </param>
    public MetriquesPdf(XGraphics gfx, Compositeur.IMetriques? ecran)
    {
        _gfx = gfx;
        _ecran = ecran;
    }

    /// <summary>
    /// Une règle sans page, pour mesurer ce que le PDF mesurerait sans rien dessiner.
    /// L'écran la consulte sans écran (<c>null</c>), puisqu'il est l'écran ; les tests
    /// de la coupure la construisent avec, pour composer comme l'export compose.
    ///
    /// La largeur que rend PDFsharp ne dépend que de la police et du texte, pas de la
    /// surface : un test du noyau le vérifie, parce que tout l'accord des deux rendus
    /// repose sur ce que la mesure hors page soit, au bit près, celle de la page.
    /// </summary>
    public static MetriquesPdf HorsPage(Compositeur.IMetriques? ecran)
    {
        FontconfigFontResolver.EnsureInstalled();
        return new MetriquesPdf(
            XGraphics.CreateMeasureContext(new XSize(1000, 1000), XGraphicsUnit.Point, XPageDirection.Downwards),
            ecran);
    }

    public double Largeur(string texte, StyleEffectif style) =>
        _gfx.MeasureString(texte, Police(style, _polices)).Width;

    public double LargeurDeCoupure(string texte, StyleEffectif style)
    {
        var propre = Largeur(texte, style);
        return _ecran is null ? propre : Math.Max(propre, _ecran.Largeur(texte, style));
    }

    public double Naturelle(StyleEffectif style) => Police(style, _polices).GetHeight();

    /// <summary>La police d'un style, telle que l'export la dessine et la mesure.</summary>
    internal static XFont Police(StyleEffectif style)
    {
        var variante = XFontStyleEx.Regular;
        if (style.Bold) variante |= XFontStyleEx.Bold;
        if (style.Italic) variante |= XFontStyleEx.Italic;
        return new XFont(style.FontFamily, style.FontSize, variante);
    }

    private static XFont Police(StyleEffectif style, Dictionary<StyleEffectif, XFont> cache)
    {
        if (cache.TryGetValue(style, out var connue)) return connue;
        return cache[style] = Police(style);
    }
}
