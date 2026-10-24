using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
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
/// qu'afficher. Le bloc, le contenu et l'abonnement viennent de
/// <see cref="ControleDeBloc"/>.
/// </summary>
public sealed class TexteCompose : ControleDeBloc
{
    /// <summary>
    /// Un morceau posé : son texte, le texte formaté qui le dessine, son point d'ancrage (coin haut-gauche du
    /// texte formaté, comme <c>DrawText</c> le veut), le rang de sa ligne, et le cadre
    /// de cette ligne — son sommet, son avance, son bord gauche et la largeur dont elle
    /// dispose —, tous dans le repère du contrôle.
    /// </summary>
    public readonly record struct MorceauPose(
        string Texte, FormattedText Rendu, Point Ancre, int Ligne,
        double HautDeLigne, double Avance, double Gauche, double Disponible);

    /// <summary>
    /// Un trait de souligné posé (poste I7) : de <paramref name="Debut"/> à
    /// <paramref name="Fin"/> sur son axe <paramref name="Axe"/>, d'un stylo
    /// <paramref name="Epaisseur"/>, dans le repère du contrôle ; avec la sorte qui en
    /// donne le motif ou la vague, et la couleur du texte qu'il souligne.
    /// </summary>
    public readonly record struct TraitPose(
        double Debut, double Fin, double Axe, double Epaisseur, Souligne Sorte, string Couleur, int Ligne);

    public override void Render(DrawingContext contexte)
    {
        var metriques = new MetriquesAvalonia();
        var (poses, traits) = Disposer(metriques, metriques);
        foreach (var morceau in poses)
        {
            contexte.DrawText(morceau.Rendu, morceau.Ancre);
        }

        // Le souligné après le texte, comme à l'export : il passe sur les jambages, comme
        // dans Publisher, qui ne l'interrompt pas sous un « p ».
        foreach (var trait in traits) Tracer(contexte, trait);
    }

    /// <summary>
    /// Trace un trait de souligné : droit, pointillé ou en vague, d'un stylo à bouts
    /// plats et à jointures rondes, le même qu'à l'export. Le motif et les points de la
    /// vague viennent du noyau (<see cref="Souligne.Motif"/>, <see cref="Souligne.Vague"/>),
    /// et non d'ici : l'écran et le PDF tracent ainsi la même chose.
    /// </summary>
    private static void Tracer(DrawingContext contexte, TraitPose trait)
    {
        var pinceau = Converters.Conv.ToBrush(trait.Couleur) ?? Brushes.Black;
        var motif = trait.Sorte.Motif;
        var stylo = new Pen(pinceau, trait.Epaisseur,
            motif is null ? null : new DashStyle(motif, 0), PenLineCap.Flat, PenLineJoin.Round);

        if (!trait.Sorte.Ondule)
        {
            contexte.DrawLine(stylo, new Point(trait.Debut, trait.Axe), new Point(trait.Fin, trait.Axe));
            return;
        }

        var points = Souligne.Vague(trait.Debut, trait.Fin, trait.Axe, trait.Epaisseur);
        if (points.Count < 2) return;

        var vague = new StreamGeometry();
        using (var trace = vague.Open())
        {
            trace.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: false);
            for (var i = 1; i < points.Count; i++) trace.LineTo(new Point(points[i].X, points[i].Y));
            trace.EndFigure(isClosed: false);
        }

        contexte.DrawGeometry(null, stylo, vague);
    }

    /// <summary>
    /// Ce que <see cref="Render"/> dessine, morceau par morceau. Le banc le lit pour
    /// confronter l'écran au PDF mot à mot (<c>Scripts/comparer-mots.sh</c>) : il lit
    /// donc exactement ce qui se dessine, et non une composition refaite à côté.
    ///
    /// <paramref name="composition"/> est la règle avec laquelle on compose ; elle est
    /// <paramref name="metriques"/> au dessin. Le banc peut en passer une autre — une
    /// règle qui coupe à sa seule mesure — pour la contre-épreuve de la recette Q13.
    /// </summary>
    public IReadOnlyList<MorceauPose> Poser(MetriquesAvalonia metriques, Compositeur.IMetriques composition) =>
        Disposer(metriques, composition).Poses;

    /// <summary>
    /// Les traits de souligné que <see cref="Render"/> trace, dans le repère du
    /// contrôle : ce que les tests mesurent, comme le banc lit <see cref="Poser"/>.
    /// </summary>
    public IReadOnlyList<TraitPose> Souligner(MetriquesAvalonia metriques) =>
        Disposer(metriques, metriques).Traits;

    /// <summary>
    /// Les morceaux et les traits de souligné, d'une seule composition : le souligné se
    /// pose sur les lignes mêmes que le texte, et composer deux fois coûterait deux fois
    /// à chaque redessin pour le risque de ne pas tomber pareil.
    /// </summary>
    private (List<MorceauPose> Poses, List<TraitPose> Traits) Disposer(
        MetriquesAvalonia metriques, Compositeur.IMetriques composition)
    {
        var poses = new List<MorceauPose>();
        var traits = new List<TraitPose>();
        if (Bloc is not { } bloc) return (poses, traits);
        if (Mesurer(composition) is not { } mesure) return (poses, traits);

        // Le calage vertical vient du noyau, comme la découpe en lignes : l'export lit
        // la même fonction. Il est borné à zéro là-bas comme ici, si bien qu'un texte
        // trop haut se cale en haut des deux côtés.
        var descente = Compositeur.Decalage(mesure.Lignes, mesure.Hauteur, bloc.VerticalAlign);

        for (var rang = 0; rang < mesure.Lignes.Count; rang++)
        {
            var ligne = mesure.Lignes[rang];

            // Rien ne se cumule ici : la ligne porte son ordonnée dans sa colonne, et
            // l'espace avant le paragraphe y est déjà entré. C'est la composition qui a
            // décidé où passer d'une colonne à l'autre, et il ne faut pas deux endroits
            // pour en décider.
            if (ligne.Morceaux.Count == 0) continue;

            // Le fer vient de la LIGNE et non du bloc : il appartient au paragraphe.
            var justifiee = ligne.Fer == TextAlign.Justify && !ligne.FinDeParagraphe;
            // Le retrait déplace le bord gauche de la ligne, gouttières comprises ;
            // « Disponible » est ce qui reste entre les deux retraits, dans sa
            // colonne. C'est cette largeur-là, et non celle du bloc, qui cale un
            // centrage ou un fer à droite.
            var gauche = bloc.PaddingLeft + ligne.Retrait;
            var origine = justifiee ? gauche : ligne.Fer switch
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

            var haut = bloc.PaddingTop + descente + ligne.Haut;
            var ligneDeBase = haut + (ligne.Avance - ligne.Naturelle) / 2 + montee;

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
                poses.Add(new MorceauPose(
                    morceau.Texte, rendu, new Point(origine + morceau.X + decalages[m], y), rang,
                    haut, ligne.Avance, gauche, ligne.Disponible));
            }

            // Le souligné se pose sous la ligne de base de chaque morceau — décalée pour un
            // exposant, comme son texte —, à la place et à l'épaisseur que la police donne.
            // Les étendues viennent du noyau, que l'export appelle de même.
            foreach (var etendue in Compositeur.Soulignes(ligne, decalages, metriques))
            {
                var style = etendue.Style;
                var (position, epaisseur) = metriques.Souligne(style);
                var baseDuMorceau = ligneDeBase - style.BaselineShift * style.FontSize;
                foreach (var trace in style.Underline.Traits(position, epaisseur))
                {
                    traits.Add(new TraitPose(
                        origine + etendue.Debut, origine + etendue.Fin, baseDuMorceau + trace.Axe, trace.Epaisseur,
                        style.Underline, style.Foreground, rang));
                }
            }
        }

        return (poses, traits);
    }
}
