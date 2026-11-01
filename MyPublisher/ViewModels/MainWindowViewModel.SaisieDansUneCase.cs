using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Taper dans une case, sur la page (poste N27 ; fiche 23, g2 à g4). Un fichier à lui, comme la
/// règle 10 de la phase I le veut.
///
/// La case ne se choisissait pas sur la page (C9) : on ne la remplissait qu'au dock. La planche
/// l'ouvre désormais d'un clic, et la dit ici. <b>Le tableau reste la sélection</b>, et le bloc
/// visé (<see cref="BlocVise"/>) : la barre règle la police et le corps du tableau entier, le dock
/// garde sa saisie de case, liée au même texte que le champ de la page — ce qu'on tape d'un côté
/// se voit de l'autre. La sélection du texte en saisie porte la case
/// (<see cref="SelectionDuTexte.Case"/>), le tableau pour bloc : c'est la forme que N15 lui avait
/// donnée d'avance.
/// </summary>
public partial class MainWindowViewModel
{
    private TableCell? _caseEnSaisie;
    private TableFrame? _tableauDeLaCase;

    /// <summary>La case dont la saisie est ouverte sur la page ; nulle sinon. La planche la dit.</summary>
    public TableCell? CaseEnSaisie => _caseEnSaisie;

    /// <summary>Le tableau de la case en saisie ; nul hors d'elle.</summary>
    public TableFrame? TableauDeLaCase => _tableauDeLaCase;

    /// <summary>
    /// La planche ouvre la saisie d'une case, ou, nulle, la referme. La barre d'état nomme la case
    /// qu'on tape, comme la recherche la nommait.
    /// </summary>
    public void SuivreLaCaseEnSaisie(TableFrame? tableau, TableCell? case_)
    {
        if (case_ is null) tableau = null;
        if (ReferenceEquals(_caseEnSaisie, case_) && ReferenceEquals(_tableauDeLaCase, tableau)) return;

        _caseEnSaisie = case_;
        _tableauDeLaCase = tableau;
        OnPropertyChanged(nameof(CaseEnSaisie));
        OnPropertyChanged(nameof(TableauDeLaCase));
        if (case_ is not null) Status = $"Case {case_.Label} du tableau « {tableau?.DisplayName} »";
    }

    /// <summary>
    /// <c>Tab</c> dans la dernière case : une ligne de plus au bas du tableau, en un pas, ses cases
    /// habillées comme celles d'au-dessus. Rend la première case de la ligne neuve, où la saisie
    /// continue.
    /// </summary>
    public TableCell? AjouterUneLigneAuTableau(TableFrame tableau)
    {
        TableCell? premiere = null;
        UnGesteUnPas(() => premiere = tableau.AjouterUneLigne());
        return premiere;
    }
}
