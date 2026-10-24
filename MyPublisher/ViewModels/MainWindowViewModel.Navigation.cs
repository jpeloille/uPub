using System;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le poste I6 — naviguer dans la planche, et garder les proportions — ajoute au
/// modèle de vue. Un fichier à lui, comme la règle 10 de la phase le veut.
///
/// La main, la loupe et « Page entière » sont des gestes de la planche, qui seule connaît
/// sa taille à l'écran et son défilement : ils vivent dans <c>PageCanvas.Navigation.cs</c>.
/// Ici, ce qui se décide sans elle : quel outil navigue, et comment un bloc garde son
/// rapport quand on saisit sa largeur ou sa hauteur au dock. Rien de cela n'entre dans le
/// document ; la case « Conserver les proportions » elle-même, posée par I2, est un état
/// de la session.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Le pas d'un clic de la loupe : celui des boutons « Zoom avant » et « Zoom arrière »
    /// de la barre principale. La molette garde le sien, plus fin.
    /// </summary>
    public const double PasDeLaLoupe = 1.25;

    /// <summary>
    /// Un outil de la planche est armé — la main ou la loupe. Il ne trace rien : la planche
    /// ne sélectionne ni ne crée de bloc tant qu'il l'est.
    /// </summary>
    public bool NavigueDansLaPlanche => ActiveTool is EditorTool.Main or EditorTool.Zoom;

    // ------------------------------------------------- conserver les proportions

    /// <summary>
    /// Le champ L du dock, en points. Il passait droit au bloc ; il passe désormais par
    /// ici, pour que la case « Conserver les proportions » puisse entraîner la hauteur.
    /// Décochée, il n'écrit que la largeur, comme avant. Le pas d'annulation reste celui
    /// du champ : le dock ouvre la transaction au focus et la ferme en le quittant, et la
    /// hauteur entraînée y entre avec la largeur.
    /// </summary>
    public double LargeurDuBloc
    {
        get => SingleSelected?.Width ?? 0;
        set => SaisirLaTaille(largeur: value, hauteur: null);
    }

    /// <summary>Le champ H du dock, en points : le jumeau de <see cref="LargeurDuBloc"/>.</summary>
    public double HauteurDuBloc
    {
        get => SingleSelected?.Height ?? 0;
        set => SaisirLaTaille(largeur: null, hauteur: value);
    }

    private void SaisirLaTaille(double? largeur, double? hauteur)
    {
        if (SingleSelected is not { } bloc) return;

        if (!ConserverLesProportions)
        {
            if (largeur is { } l) bloc.Width = l;
            if (hauteur is { } h) bloc.Height = h;
            return;
        }

        var (nouvelleLargeur, nouvelleHauteur) = AuMemeRapport(bloc.Width, bloc.Height, largeur, hauteur);
        bloc.Width = nouvelleLargeur;
        bloc.Height = nouvelleHauteur;
    }

    /// <summary>
    /// La taille d'un bloc dont on fixe un côté en gardant son rapport. Le côté qui suit
    /// ne descend pas sous la taille minimale d'un bloc : l'échelle remonte alors d'autant,
    /// et le côté saisi avec elle — le rapport passe avant la valeur tapée, puisque c'est
    /// ce que la case demande. Un bloc sans surface n'a pas de rapport à garder : la valeur
    /// saisie s'écrit seule.
    /// </summary>
    public static (double Largeur, double Hauteur) AuMemeRapport(
        double largeur, double hauteur, double? nouvelleLargeur, double? nouvelleHauteur)
    {
        if (largeur <= 0 || hauteur <= 0)
        {
            return (nouvelleLargeur ?? largeur, nouvelleHauteur ?? hauteur);
        }

        var echelle = nouvelleLargeur is { } l ? l / largeur
            : nouvelleHauteur is { } h ? h / hauteur
            : 1;
        echelle = Math.Max(echelle, Snap.MinFrameSize / Math.Min(largeur, hauteur));
        return (largeur * echelle, hauteur * echelle);
    }
}
