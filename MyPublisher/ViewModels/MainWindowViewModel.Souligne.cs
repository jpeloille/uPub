using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le souligné (phase I, poste I7) ajoute au modèle de vue : la commande
/// « Souligné ». Un fichier à lui, comme la règle 10 de la phase le veut : I7 part en même
/// temps que I2 et I4, qui réécrivent la fenêtre et la planche, et ne touche à aucun de
/// leurs fichiers ni au modèle de vue commun.
///
/// La commande est posée ici sans bouton : le bouton S de la barre contextuelle,
/// <c>Ctrl+U</c> et « Souligné » au menu Texte viennent de I3, qui les lie à
/// <see cref="SoulignerCommand"/>. L'état du bouton se lit sur le bloc lui-même
/// (<c>TextFrame.Underline.Actif</c>), qui prévient de son changement : le modèle de vue
/// n'en tient pas de copie, qu'il faudrait réaccorder à chaque changement de sélection
/// depuis un fichier qui n'est pas celui-ci.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Souligne le bloc de texte sélectionné d'un trait simple, ou lui retire son
    /// souligné s'il en a un, quelle qu'en soit la sorte : c'est la bascule de
    /// <c>Ctrl+U</c> dans Publisher, qui ne pose au clavier que le trait simple.
    ///
    /// **C'est le souligné du bloc qui bascule**, comme le gras du panneau : il n'y a pas
    /// encore de sélection dans le texte (R7). Un fragment qui dit son propre souligné le
    /// garde — un mot souligné en pointillé reste pointillé sous un bloc qu'on souligne,
    /// et redevient le seul souligné quand on retire celui du bloc. Les fragments d'un
    /// <c>.pub</c> importé ne le figent pas : l'import leur laisse le souligné du bloc
    /// quand c'est le leur (<c>PubImporter</c>), si bien que souligner un bloc importé le
    /// souligne entier.
    ///
    /// **Un geste, un pas d'annulation** (règle 2 de la phase), même pendant une saisie.
    /// <c>Ctrl+U</c> se frappe le plus souvent en tapant, quand la saisie sur la page tient
    /// sa transaction ouverte : la commande s'y emboîterait sans rien empiler, et une seule
    /// annulation défairait à la fois la frappe et le souligné. Elle referme donc d'abord
    /// cette transaction en un pas à elle, empile le sien, puis la rouvre, pour que ce
    /// qu'on tape ensuite ait encore son pas quand la saisie se refermera.
    ///
    /// Sans bloc de texte seul sélectionné, la commande ne fait rien et le dit : une image,
    /// une forme, un groupe ou un tableau ne portent pas de souligné.
    /// </summary>
    [RelayCommand]
    private void Souligner()
    {
        if (SingleSelected is not TextFrame bloc)
        {
            Status = "Souligné : sélectionnez un seul bloc de texte.";
            return;
        }

        var ouvertes = _editDepth;
        EndPendingEdit();

        BeginEdit();
        bloc.Underline = bloc.Underline.Actif ? Souligne.Aucun : Souligne.Simple;
        CommitEdit();

        for (var i = 0; i < ouvertes; i++) BeginEdit();

        Status = bloc.Underline.Actif ? "Souligné" : "Souligné retiré";
    }
}
