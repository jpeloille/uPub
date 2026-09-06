using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MyPublisher.Converters;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Affiche le texte d'un bloc en passant par la composition du noyau.
///
/// Pourquoi remplacer <c>TextBlock</c>, qui faisait très bien l'affaire. Pour deux
/// raisons qui n'en font qu'une.
///
/// D'abord parce qu'un <c>TextBlock</c> ne change pas de police au milieu d'un
/// paragraphe, et que les documents réels le demandent : jusqu'à sept fragments de
/// style dans un seul paragraphe des fiches ATR.
///
/// Ensuite, et surtout, parce que tant que l'écran découpait ses lignes avec Avalonia
/// et l'export avec le noyau, rien ne garantissait qu'ils coupent aux mêmes endroits.
/// Ils s'accordaient sur les documents essayés ; c'était une observation, pas une
/// propriété. Ici les deux appellent <see cref="Compositeur.Composer"/> — la même
/// fonction, les mêmes lignes, par construction. C'est ce que le projet promet depuis
/// le début : « l'écran et l'export partagent exactement les mêmes données, sans
/// couche intermédiaire susceptible de diverger ».
///
/// La saisie, elle, reste au <c>TextBox</c> posé par-dessus : ce contrôle-ci ne fait
/// qu'afficher.
/// </summary>
public sealed class TexteCompose : Control
{
    public static readonly StyledProperty<TextFrame?> BlocProperty =
        AvaloniaProperty.Register<TexteCompose, TextFrame?>(nameof(Bloc));

    public static readonly StyledProperty<string?> ContenuProperty =
        AvaloniaProperty.Register<TexteCompose, string?>(nameof(Contenu));

    static TexteCompose()
    {
        AffectsRender<TexteCompose>(BlocProperty, ContenuProperty);
    }

    /// <summary>Le bloc, pour son style commun, ses fragments et ses marges.</summary>
    public TextFrame? Bloc
    {
        get => GetValue(BlocProperty);
        set => SetValue(BlocProperty, value);
    }

    /// <summary>
    /// Le texte à afficher, déjà passé par le résolveur de champs : un folio vu ici
    /// est le folio imprimé.
    /// </summary>
    public string? Contenu
    {
        get => GetValue(ContenuProperty);
        set => SetValue(ContenuProperty, value);
    }

    public override void Render(DrawingContext contexte)
    {
        if (Bloc is not { } bloc || string.IsNullOrEmpty(Contenu)) return;

        var largeur = Math.Max(1, Bounds.Width - bloc.PaddingLeft - bloc.PaddingRight);
        var hauteur = Math.Max(1, Bounds.Height - bloc.PaddingTop - bloc.PaddingBottom);
        var metriques = new MetriquesAvalonia();
        var commun = new StyleEffectif(
            bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic, bloc.Foreground, 0);

        // Les fragments repèrent des positions dans le texte saisi ; un champ résolu
        // n'a pas les mêmes caractères, et n'est jamais qu'un seul style. Les écarts de
        // paragraphe tombent pour la même raison — un champ n'a pas les mêmes
        // paragraphes. Les réglages du bloc, eux, valent toujours : ce sont des
        // propriétés du bloc, comme ses marges intérieures.
        var fragments = Contenu == bloc.Text ? bloc.Spans : (IReadOnlyList<StyleSpan>)[];
        var ecarts = Contenu == bloc.Text ? bloc.Paragraphs : (IReadOnlyList<ParagraphSpan>)[];

        var lignes = Compositeur.Composer(
            Contenu, fragments, commun, ecarts, bloc.Paragraph,
            largeur, hauteur, bloc.Colonnage, bloc.LineHeight, metriques);

        foreach (var ligne in lignes)
        {
            // Rien ne se cumule ici : la ligne porte son ordonnée dans sa colonne, et
            // l'espace avant le paragraphe y est déjà entré. C'est la composition qui a
            // décidé où passer d'une colonne à l'autre, et il ne faut pas deux endroits
            // pour en décider.
            if (ligne.Morceaux.Count > 0)
            {
                var justifiee = bloc.Align == TextAlign.Justify && !ligne.FinDeParagraphe;
                // Le retrait déplace le bord gauche de la ligne, gouttières comprises ;
                // « Disponible » est ce qui reste entre les deux retraits, dans sa
                // colonne. C'est cette largeur-là, et non celle du bloc, qui cale un
                // centrage ou un fer à droite.
                var gauche = bloc.PaddingLeft + ligne.Retrait;
                var origine = justifiee ? gauche : bloc.Align switch
                {
                    TextAlign.Center => gauche + (ligne.Disponible - ligne.Largeur) / 2,
                    TextAlign.Right => gauche + ligne.Disponible - ligne.Largeur,
                    _ => gauche,
                };

                // Le blanc se répartit entre les mots, pas entre les morceaux :
                // même calcul que l'export, et il vient du noyau.
                var decalages = justifiee
                    ? Compositeur.Decalages(ligne)
                    : new double[ligne.Morceaux.Count];

                // Tous les morceaux d'une ligne se calent sur la même ligne de base,
                // celle du plus grand style : c'est ce qui empêche un mot en gros
                // corps de flotter au-dessus de ses voisins.
                var montee = 0.0;
                foreach (var morceau in ligne.Morceaux)
                {
                    montee = Math.Max(montee, metriques.Montee(morceau.Style));
                }

                var ligneDeBase = bloc.PaddingTop + ligne.Haut
                                  + (ligne.Avance - ligne.Naturelle) / 2 + montee;

                for (var m = 0; m < ligne.Morceaux.Count; m++)
                {
                    var morceau = ligne.Morceaux[m];
                    if (morceau.Texte.Trim().Length == 0) continue;

                    var rendu = metriques.Composer(morceau.Texte, morceau.Style);
                    // FormattedText se pose par son coin haut-gauche ; on remonte donc
                    // de sa propre montée pour caler la ligne de base.
                    var y = ligneDeBase
                            - morceau.Style.BaselineShift * morceau.Style.FontSize
                            - rendu.Baseline;
                    contexte.DrawText(rendu, new Point(origine + morceau.X + decalages[m], y));
                }
            }
        }
    }

    /// <summary>La règle à mesurer d'Avalonia, prêtée au compositeur du noyau.</summary>
    private sealed class MetriquesAvalonia : Compositeur.IMetriques
    {
        private readonly Dictionary<StyleEffectif, Typeface> _polices = [];

        public double Largeur(string texte, StyleEffectif style) =>
            Composer(texte, style).WidthIncludingTrailingWhitespace;

        public double Naturelle(StyleEffectif style) =>
            InterligneConverter.HauteurNaturelle(
                style.FontFamily, style.FontSize, style.Bold, style.Italic);

        public double Montee(StyleEffectif style)
        {
            var metriques = Police(style).GlyphTypeface.Metrics;
            var em = (double)metriques.DesignEmHeight;
            return em > 0 ? -metriques.Ascent * style.FontSize / em : style.FontSize;
        }

        public FormattedText Composer(string texte, StyleEffectif style) =>
            new(texte, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Police(style), style.FontSize,
                Conv.ToBrush(style.Foreground) ?? Brushes.Black);

        private Typeface Police(StyleEffectif style)
        {
            if (_polices.TryGetValue(style, out var connue)) return connue;

            var typeface = new Typeface(
                new FontFamily(style.FontFamily),
                style.Italic ? FontStyle.Italic : FontStyle.Normal,
                style.Bold ? FontWeight.Bold : FontWeight.Normal);

            _polices[style] = typeface;
            return typeface;
        }
    }
}
