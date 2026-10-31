using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le poste N22 — les caractères spéciaux — ajoute au modèle de vue. Un fichier à lui,
/// comme la règle 10 de la phase I le veut.
///
/// Publisher montre les espaces, les tabulations, les fins de paragraphe et les sauts de ligne
/// forcés par <i>Affichage ▸ Caractères spéciaux</i> et <c>Ctrl+Maj+Y</c> (le livre, p. 9 ;
/// fiche 23, a27 et d14). C'est une bascule de la session, comme « Repères de lignes de base »
/// (<see cref="ShowBaselines"/>) : rien n'entre au document ni à l'historique, rien ne
/// s'enregistre, et elle est fermée à l'ouverture, comme chez Publisher. La planche dessine les
/// marques dans son <c>Overlay</c>, hors de la page (<c>PageCanvas.CaracteresSpeciaux.cs</c>).
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>Les caractères spéciaux se montrent-ils sur la page ? Fermé d'emblée.</summary>
    [ObservableProperty] private bool _caracteresSpeciauxVisibles;

    /// <summary><c>Ctrl+Maj+Y</c> : la case « Caractères spéciaux » du menu Affichage.</summary>
    [RelayCommand]
    private void BasculerLesCaracteresSpeciaux() => CaracteresSpeciauxVisibles = !CaracteresSpeciauxVisibles;
}
