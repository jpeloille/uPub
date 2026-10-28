using System;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le poste N14 ajoute au modèle de vue : le champ numérique qui attend d'être validé.
/// Un fichier à lui, comme la règle 10 de la phase I le veut.
///
/// Depuis N14, la valeur d'un champ du dock ou de la barre ne part plus à chaque frappe, mais
/// quand on la valide (<c>ChampNumerique</c>). Ce qui la lisait au document pendant la frappe la
/// trouverait absente : Annuler et Rétablir, qui referment d'abord les transactions ouvertes
/// (<c>EndPendingEdit</c>, Q24), un raccourci qui fait son propre pas (<c>UnGesteUnPas</c>),
/// l'enregistrement et l'export. Et un changement de sélection qui ne passe pas par la perte du
/// focus la ferait partir au bloc d'après : les champs gardés passent au bloc suivant (Q23), et
/// la largeur ou la hauteur, liées au modèle de vue, y relisaient la leur en effaçant la frappe.
/// Le modèle de vue ne connaît pas les champs ; la fenêtre lui prête de quoi valider celui qui
/// attend, comme elle lui prête son focus (N6), et il la demande avant de lire le document ou
/// de changer la sélection (<c>SetSelection</c>).
/// </summary>
public partial class MainWindowViewModel
{
    private Action? _validerLeChamp;

    /// <summary>La fenêtre prête — ou reprend, avec <c>null</c> — de quoi valider le champ qui attend.</summary>
    public void SuivreLesChamps(Action? valider) => _validerLeChamp = valider;

    /// <summary>Ce qui attend dans un champ part au document, comme au moment où on le quitte.</summary>
    private void ValiderLeChampEnAttente() => _validerLeChamp?.Invoke();
}
