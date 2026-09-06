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
    public override void Render(DrawingContext contexte)
    {
        if (Bloc is not { } bloc) return;

        var metriques = new MetriquesAvalonia();
        if (Mesurer(metriques) is not { } mesure) return;

        // Le calage vertical vient du noyau, comme la découpe en lignes : l'export lit
        // la même fonction. Il est borné à zéro là-bas comme ici, si bien qu'un texte
        // trop haut se cale en haut des deux côtés.
        var descente = Compositeur.Decalage(mesure.Lignes, mesure.Hauteur, bloc.VerticalAlign);

        foreach (var ligne in mesure.Lignes)
        {
            // Rien ne se cumule ici : la ligne porte son ordonnée dans sa colonne, et
            // l'espace avant le paragraphe y est déjà entré. C'est la composition qui a
            // décidé où passer d'une colonne à l'autre, et il ne faut pas deux endroits
            // pour en décider.
            if (ligne.Morceaux.Count > 0)
            {
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
                    montee = System.Math.Max(montee, metriques.Montee(morceau.Style));
                }

                var ligneDeBase = bloc.PaddingTop + descente + ligne.Haut
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
}
