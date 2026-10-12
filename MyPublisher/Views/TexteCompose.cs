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

    public override void Render(DrawingContext contexte)
    {
        var metriques = new MetriquesAvalonia();
        foreach (var morceau in Poser(metriques, metriques))
        {
            contexte.DrawText(morceau.Rendu, morceau.Ancre);
        }
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
    public IReadOnlyList<MorceauPose> Poser(MetriquesAvalonia metriques, Compositeur.IMetriques composition)
    {
        var poses = new List<MorceauPose>();
        if (Bloc is not { } bloc) return poses;
        if (Mesurer(composition) is not { } mesure) return poses;

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
        }

        return poses;
    }
}
