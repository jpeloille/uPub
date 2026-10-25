using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que masquer un bloc (phase I, poste I8) ajoute au modèle de vue : la commande de
/// l'œil de « Blocs de la page », et les blocs que l'alignement voit. Un fichier à lui,
/// comme la règle 10 de la phase le veut.
///
/// La visibilité est une propriété du document (<see cref="Frame.Visible"/>), et non de la
/// vue : la basculer passe par l'historique comme toute retouche, et le bloc masqué
/// s'enregistre masqué. L'œil se lit sur le bloc lui-même, qui prévient de son
/// changement ; le modèle de vue n'en tient pas de copie.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Masque le bloc, ou le montre s'il l'était : le clic sur l'œil de sa ligne. Le bloc
    /// est désigné par la ligne et non par la sélection — on ferme l'œil d'un bloc sans le
    /// sélectionner —, et seul un bloc de la page courante se bascule : c'est la seule que
    /// la liste montre.
    ///
    /// **Un geste, un pas d'annulation** (règle 2 de la phase), même pendant une saisie :
    /// le bouton ne prend pas le focus, et un champ du dock ou la saisie sur la page peut
    /// tenir sa transaction ouverte. Comme le souligné (I7), la commande la referme d'abord
    /// en un pas à elle, empile le sien, puis la rouvre.
    ///
    /// **La sélection reste cohérente.** Un bloc masqué reste sélectionné s'il l'était
    /// seul : il s'y sélectionne, et le dock le montre. Il quitte une sélection multiple,
    /// qui ne porte que ce que la planche saisit — sans quoi un glissé ou Suppr
    /// emporterait, à l'insu de qui les fait, un bloc que rien ne montre.
    /// </summary>
    [RelayCommand]
    private void BasculerLaVisibilite(Frame? bloc)
    {
        if (bloc is null || CurrentPage?.Frames.Contains(bloc) != true) return;

        var ouvertes = _editDepth;
        EndPendingEdit();

        BeginEdit();
        bloc.Visible = !bloc.Visible;
        CommitEdit();

        for (var i = 0; i < ouvertes; i++) BeginEdit();

        if (!bloc.Visible && Selection.Count > 1 && Selection.Contains(bloc))
        {
            SetSelection(Selection.Where(frame => !ReferenceEquals(frame, bloc)).ToList());
        }

        Status = bloc.Visible ? $"« {bloc.DisplayName} » affiché" : $"« {bloc.DisplayName} » masqué";
    }

    /// <summary>
    /// Les blocs sélectionnés que l'alignement et la répartition voient : les visibles
    /// seuls. Un bloc masqué ne s'aligne pas, et ne sert pas non plus de repère aux autres.
    /// </summary>
    private List<Frame> BlocsAlignables => Selection.Where(frame => frame.Visible).ToList();
}
