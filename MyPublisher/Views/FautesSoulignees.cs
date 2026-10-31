using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MyPublisher.Models;
using MyPublisher.Services;

namespace MyPublisher.Views;

/// <summary>
/// Le trait d'une faute sur une ligne, dans le repère du bloc non tourné : la faute, la ligne qu'il
/// souligne, de l'abscisse <see cref="Gauche"/> à <see cref="Droite"/> — celles de N3 —, la ligne de
/// base de cette ligne et le corps du texte. Une faute coupée en fin de ligne en a deux.
/// </summary>
public readonly record struct TraitDeFaute(FauteDOrthographe Faute, int Ligne, double Gauche, double Droite,
                                           double LigneDeBase, double Taille);

/// <summary>
/// Les fautes soulignées d'un bloc de texte : le contrôle qui dessine sa composition, les niveaux de
/// découpe qui mènent de l'<c>Overlay</c> à lui — ceux des caractères spéciaux (N22) —, et ses traits.
/// </summary>
public sealed record FautesDuBloc(TexteCompose Compose, IReadOnlyList<CoucheDeDecoupe> Couches, IReadOnlyList<TraitDeFaute> Traits)
{
    /// <summary>Du repère du bloc non tourné à celui de l'<c>Overlay</c> : les niveaux, composés.</summary>
    public Matrix Transformation
    {
        get
        {
            var transformation = Matrix.Identity;
            foreach (var couche in Couches) transformation = couche.Transformation * transformation;
            return transformation;
        }
    }
}

/// <summary>
/// Les fautes d'orthographe soulignées d'un trait ondulé sur le texte de la page (poste N25 ;
/// fiche 23, a26), comme Publisher les souligne pendant la frappe.
///
/// Il vit dans l'<c>Overlay</c> de la planche, hors de <c>PageRoot</c>, que l'écran compare au PDF
/// (règle 4) : rien de ce qu'il dessine ne s'imprime, ni ne change l'encre de la page. La planche
/// lui donne tout (<c>PageCanvas.CaracteresSpeciaux.cs</c>, qui suit les blocs de la page pour les
/// caractères spéciaux et pour lui) : pour chaque bloc, ses traits dans le repère du bloc non
/// tourné, aux abscisses de N3, et les niveaux de découpe qui y mènent — un bloc tourné tourne son
/// trait avec lui, la page et le bloc découpent ce qui sort d'eux. Il ne calcule rien : il dessine.
///
/// Le trait garde sa taille à l'écran à tous les zooms — un pixel d'épaisseur, autant
/// d'amplitude, six de période, la vague de I7 (<see cref="Souligne.Vague"/>) —, sous la ligne de
/// base, à un cinquième du corps. Sa couleur est celle de l'apparence, posée par le style de la
/// planche (<c>PageCanvas.axaml</c>).
/// </summary>
public sealed class FautesSoulignees : Control
{
    /// <summary>Le pinceau du trait ondulé.</summary>
    public static readonly StyledProperty<IBrush?> EncreProperty =
        AvaloniaProperty.Register<FautesSoulignees, IBrush?>(nameof(Encre));

    /// <summary>L'épaisseur du trait et l'amplitude de la vague, en pixels d'écran.</summary>
    public const double Epaisseur = 1;

    static FautesSoulignees()
    {
        AffectsRender<FautesSoulignees>(EncreProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<FautesSoulignees>(false);
    }

    public IBrush? Encre
    {
        get => GetValue(EncreProperty);
        set => SetValue(EncreProperty, value);
    }

    /// <summary>Les blocs et leurs fautes, dans l'ordre de la page ; vide quand rien n'est vérifié.</summary>
    public IReadOnlyList<FautesDuBloc> Blocs { get; private set; } = [];

    /// <summary>Pose les fautes à souligner.</summary>
    public void Poser(IReadOnlyList<FautesDuBloc> blocs)
    {
        if (blocs.Count == 0 && Blocs.Count == 0) return;

        Blocs = blocs;
        InvalidateVisual();
    }

    /// <summary>Rien à souligner : la vérification est fermée.</summary>
    public void Effacer() => Poser([]);

    /// <summary>
    /// L'ordonnée de l'axe de la vague, dans le repère du bloc : sous la ligne de base, à un
    /// cinquième du corps, et au moins à deux épaisseurs, pour ne pas toucher le pied des lettres.
    /// </summary>
    public static double Axe(TraitDeFaute trait, double epaisseur) =>
        trait.LigneDeBase + Math.Max(trait.Taille / 5, 2 * epaisseur);

    public override void Render(DrawingContext contexte)
    {
        if (Encre is not { } encre) return;

        var etats = new List<DrawingContext.PushedState>();
        foreach (var bloc in Blocs)
        {
            foreach (var couche in bloc.Couches)
            {
                etats.Add(contexte.PushTransform(couche.Transformation));
                etats.Add(contexte.PushClip(couche.Decoupe));
            }

            // Le repère du bloc est mis à l'échelle du zoom : l'épaisseur d'un pixel s'y divise par elle.
            var m = bloc.Transformation;
            var echelle = Math.Sqrt(m.M11 * m.M11 + m.M12 * m.M12);
            var epaisseur = Epaisseur / (echelle > 0 ? echelle : 1);
            var stylo = new Pen(encre, epaisseur);

            foreach (var trait in bloc.Traits)
            {
                var points = Souligne.Vague(trait.Gauche, trait.Droite, Axe(trait, epaisseur), epaisseur);
                if (points.Count < 2) continue;

                var vague = new StreamGeometry();
                using (var trace = vague.Open())
                {
                    trace.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: false);
                    for (var i = 1; i < points.Count; i++) trace.LineTo(new Point(points[i].X, points[i].Y));
                    trace.EndFigure(isClosed: false);
                }

                contexte.DrawGeometry(null, stylo, vague);
            }

            for (var i = etats.Count - 1; i >= 0; i--) etats[i].Dispose();
            etats.Clear();
        }
    }
}
