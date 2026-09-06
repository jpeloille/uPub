using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Le petit A rouge : ce bloc contient plus de texte qu'il n'en montre.
///
/// Publisher pose ce marqueur au coin d'une zone de texte en débordement, et c'est la
/// moitié visible du poste A2 — l'autre étant l'avertissement de l'export. Sans lui,
/// on compose une page entière sans savoir qu'un paragraphe est coupé, et on ne
/// l'apprend qu'en relisant le PDF, si on le relit.
///
/// Il est le frère de <see cref="TexteCompose"/> et non un morceau de lui : le texte
/// est découpé au bloc (<c>ClipToBounds</c>), un marqueur dessiné dedans le serait
/// aussi, et il doit pouvoir mordre le bord. Les deux lisent la même composition, par
/// <see cref="ControleDeBloc.Mesurer"/>, et la même règle de débordement, par
/// <see cref="Compositeur.Deborde"/> — celle-là même que l'export applique.
///
/// Il ne s'imprime pas, comme les repères : c'est un signe adressé à qui compose. On
/// le range donc sous le même interrupteur qu'eux (*Affichage ▸ Repères*), ce qui
/// s'écarte de Publisher, qui le montre toujours. Deux raisons : il n'a pas plus de
/// sens qu'un repère sur une page qu'on regarde pour la juger, et c'est ce qui permet
/// de comparer l'écran au PDF — sans quoi toute page portant un débordement afficherait
/// une tache que le PDF n'a pas, et la comparaison ne prouverait plus rien.
/// </summary>
public sealed class MarqueurDebordement : ControleDeBloc
{
    /// <summary>Côté du marqueur en points, réduit pour ne pas dépasser un petit bloc.</summary>
    private const double Cote = 12;

    /// <summary>Le rouge des marques d'alerte, celui de Publisher.</summary>
    private static readonly Color Rouge = Color.FromRgb(0xD3, 0x2F, 0x2F);

    public override void Render(DrawingContext contexte)
    {
        if (Bloc is not { } bloc) return;
        if (Mesurer(new MetriquesAvalonia()) is not { } mesure) return;
        if (!Compositeur.Deborde(mesure.Lignes, mesure.Hauteur, bloc.PaddingBottom, bloc.VerticalAlign))
        {
            return;
        }

        // Une case de tableau fait dix points de haut : un marqueur de douze la
        // couvrirait tout entière. Il se réduit plutôt que de mentir sur ce qu'il
        // désigne, sans descendre sous ce qui reste lisible.
        var cote = Math.Clamp(Math.Min(Bounds.Width, Bounds.Height), 6, Cote);

        // Posé au coin inférieur droit, mordant sur le bord : le débordement se produit
        // par le bas, et un marqueur entièrement à l'extérieur se poserait sur le bloc
        // voisin.
        var boite = new Rect(
            Bounds.Width - cote * 0.75, Bounds.Height - cote * 0.75, cote, cote);

        contexte.DrawRectangle(
            new SolidColorBrush(Colors.White),
            new Pen(new SolidColorBrush(Rouge), cote / 12),
            boite, cote / 6, cote / 6);

        var lettre = new FormattedText(
            "A", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
            cote * 0.72, new SolidColorBrush(Rouge));

        contexte.DrawText(lettre, new Point(
            boite.X + (boite.Width - lettre.Width) / 2,
            boite.Y + (boite.Height - lettre.Height) / 2));
    }
}
