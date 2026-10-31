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
/// <see cref="SoulignerCommand"/>. L'état du bouton se lisait sur le bloc lui-même
/// (<c>TextFrame.Underline.Actif</c>) ; depuis R7, sur les caractères visés
/// (<see cref="MiseEnFormeVisee.Underline"/>), sous le même nom.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Souligne d'un trait simple les caractères visés d'un bloc de texte, ou leur retire leur
    /// souligné s'ils en ont un, quelle qu'en soit la sorte : c'est la bascule de <c>Ctrl+U</c>
    /// dans Publisher, qui ne pose au clavier que le trait simple.
    ///
    /// **Les caractères visés** (repris par le poste R7, <c>MiseEnFormeVisee</c>) : en saisie, la
    /// sélection du texte, ou le mot du curseur ; hors saisie, tous les caractères du bloc,
    /// fragments compris — le bloc prend le souligné, et un mot souligné en pointillé par son
    /// fragment le perd. Avant R7, c'était le souligné du bloc seul qui basculait, et le fragment
    /// gardait le sien.
    ///
    /// **Un geste, un pas d'annulation** (règle 2 de la phase), même pendant une saisie.
    /// <c>Ctrl+U</c> se frappe le plus souvent en tapant, quand la saisie sur la page tient
    /// sa transaction ouverte : la commande s'y emboîterait sans rien empiler, et une seule
    /// annulation défairait à la fois la frappe et le souligné. Elle referme donc d'abord
    /// cette transaction en un pas à elle, empile le sien, puis la rouvre (<c>UnGesteUnPas</c>),
    /// pour que ce qu'on tape ensuite ait encore son pas quand la saisie se refermera.
    ///
    /// Sans bloc de texte seul sélectionné, la commande ne fait rien et le dit : une image,
    /// une forme, un groupe ou un tableau ne portent pas de souligné. Elle ne s'offre pas
    /// non plus (repris le 26 octobre 2026, après la relecture de I3) : au menu Texte, où I3
    /// l'a posée, elle se grise comme les alignements, et <c>Ctrl+U</c> s'y tait comme
    /// <c>Ctrl+G</c> sur une image ; la sélection le lui redit (<c>NotifierLaDisposition</c>).
    /// </summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void Souligner()
    {
        if (!BasculerLeTexte(Bascule.Souligne, "Souligné"))
        {
            Status = "Souligné : sélectionnez un seul bloc de texte.";
        }
    }
}
