using System;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le poste Q24 ajoute au modèle de vue : l'avis qu'Annuler et Rétablir lèvent après
/// avoir refermé les transactions ouvertes. Un fichier à lui, comme la règle 10 de la phase I
/// le veut.
///
/// Une transaction d'édition a un propriétaire, qui l'ouvre et la referme : la saisie sur la
/// page, le champ du dock ou de la barre qui a le focus, le glissé en cours sur la planche, le
/// sélecteur de couleur ouvert sous la barre.
/// Annuler et Rétablir les referment toutes d'abord (<c>EndPendingEdit</c>) : comparer
/// l'instantané de référence d'une saisie à un document déjà restauré ne voudrait rien dire.
/// Puis il peut n'y avoir rien à défaire — <c>Ctrl+Z</c> sur un historique vide, ou
/// <c>Ctrl+Y</c> après n'importe quelle frappe, dont la fermeture vide la pile de
/// rétablissement. La saisie restait alors ouverte, ou le champ gardait le focus, hors de
/// toute transaction : la frappe suivante ne s'annulait jamais, et ne marquait pas le
/// document, qu'une fermeture aurait perdu sans question.
///
/// Le modèle de vue ne rouvre rien lui-même : il ne sait pas qui est encore actif. Une
/// annulation qui relit le document referme la saisie par le changement de sélection ; rouvrir
/// autant de transactions qu'il y en avait en laisserait une sans propriétaire, où chaque geste
/// suivant fondrait son pas. Il lève l'avis, et chaque propriétaire encore actif rouvre la
/// sienne.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Annuler ou Rétablir vient de refermer toutes les transactions ouvertes, qu'il y ait eu
    /// ou non quelque chose à défaire. Le propriétaire encore actif rouvre la sienne, et lui
    /// seul : la saisie sur la page si elle l'est toujours, le champ s'il a toujours le focus,
    /// le glissé si le bouton est toujours tenu, le sélecteur de couleur s'il est toujours
    /// ouvert.
    /// </summary>
    public event EventHandler? TransactionsRefermees;

    private void AviserLesProprietaires() => TransactionsRefermees?.Invoke(this, EventArgs.Empty);
}
