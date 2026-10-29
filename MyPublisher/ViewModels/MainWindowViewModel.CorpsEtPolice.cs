using CommunityToolkit.Mvvm.Input;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le poste N19 ajoute au modèle de vue (fiche 23, b6), dans un fichier à lui (règle 10) :
/// la commande que joue une taille de la liste du corps, dans la barre contextuelle. La liste est
/// celle du noyau, <see cref="Models.CorpsStandard.Tailles"/>, que N10 a posée pour
/// <c>Ctrl+Maj+&gt;</c> et <c>Ctrl+Maj+&lt;</c>. La police tapée, elle, ne demande rien ici : elle part
/// par la liaison de sa liste (<c>Views/ListeDesPolices.cs</c>).
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Une taille de la liste du corps : le bloc entier la prend, texte ou tableau, en un pas
    /// d'annulation où que soit le focus — comme <c>Ctrl+]</c> (N10). La taille qu'il a déjà ne
    /// fait pas de pas.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PorteDuTexte))]
    private void PoserLeCorps(double taille) => ChangerLeCorps(_ => taille);
}
