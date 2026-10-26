using System;
using CommunityToolkit.Mvvm.Input;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le poste N6 ajoute au modèle de vue : les raccourcis de la fenêtre laissent la touche
/// au champ. Un fichier à lui, comme la règle 10 de la phase I le veut.
///
/// Les liaisons de la fenêtre (<c>Window.KeyBindings</c>) passent avant le contrôle qui a le
/// focus, et ne lui rendent la touche que si leur commande ne peut pas s'exécuter. Sans
/// condition, <c>Ctrl+A</c> tapé dans la saisie d'un bloc sélectionnait tous les blocs et
/// refermait la saisie ; <c>Ctrl+D</c> dupliquait le bloc et fondait la frappe et la copie en un
/// seul pas ; de même dans les champs Contenu et Nom et dans les champs numériques du dock, où
/// <c>Ctrl+Maj+G</c>, <c>H</c>, <c>F</c> et <c>B</c> se fondaient aussi dans la saisie du champ
/// (relevé de la relecture de I3).
///
/// La règle : <b>tant qu'un champ de texte a le clavier, les raccourcis d'objet se taisent</b> —
/// tout sélectionner, dupliquer, grouper, dissocier, premier plan, arrière-plan, et demain le
/// presse-papiers de N8, qui lira <see cref="ConsumesTyping"/>. Leur touche revient au champ :
/// <c>Ctrl+A</c> y sélectionne le texte.
///
/// La garde est au clavier seul — décision du propriétaire, le 26 octobre 2026. Les six
/// liaisons ont des commandes à elles, ci-dessous, qui jouent la commande d'origine quand aucun
/// champ n'a le clavier ; les commandes d'origine gardent leur condition d'avant le poste. Les
/// menus et les boutons qui les portent agissent donc sur les blocs même pendant la frappe :
/// un menu qu'on ouvre prend de toute façon le focus au champ, et un bouton grisé pendant la
/// saisie ne se cliquait plus. Annuler et Rétablir ne changent pas de sens : c'est l'historique
/// du document qui annule (Q24).
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>Ce que la fenêtre répond quand on lui demande si un champ a le clavier.</summary>
    private Func<bool> _unChampALeClavier = () => false;

    /// <summary>
    /// Un contrôle qui consomme la frappe a le clavier : une zone de texte — la saisie d'un bloc
    /// sur la page, un champ du dock ou de la barre, numérique compris —, ou une liste
    /// déroulante, qui cherche une entrée à la lettre. C'est le prédicat qui fait déjà taire les
    /// outils à une lettre (<c>MainWindow.ConsumesTyping</c>) : la fenêtre le prête au modèle de
    /// vue, qui le lit à chaque touche. Sans fenêtre, faux : les raccourcis jouent comme avant.
    /// </summary>
    public bool ConsumesTyping => _unChampALeClavier();

    /// <summary>
    /// La fenêtre dit comment lire son focus — <c>null</c> quand elle lâche le modèle de vue.
    /// Une liaison lit la condition de sa commande au moment de la touche : rien n'est à notifier
    /// quand le focus change.
    /// </summary>
    public void SuivreLeClavier(Func<bool>? unChampALeClavier) =>
        _unChampALeClavier = unChampALeClavier ?? (() => false);

    // ------------------------------------------------------- les commandes du clavier

    [RelayCommand(CanExecute = nameof(PeutToutSelectionnerAuClavier))]
    private void ToucheToutSelectionner() => SelectAllCommand.Execute(null);

    [RelayCommand(CanExecute = nameof(PeutDupliquerAuClavier))]
    private void ToucheDupliquer() => DuplicateSelectedCommand.Execute(null);

    [RelayCommand(CanExecute = nameof(PeutGrouperAuClavier))]
    private void ToucheGrouper() => GroupSelectedCommand.Execute(null);

    [RelayCommand(CanExecute = nameof(PeutDissocierAuClavier))]
    private void ToucheDissocier() => UngroupSelectedCommand.Execute(null);

    [RelayCommand(CanExecute = nameof(PeutPorterAuPremierPlanAuClavier))]
    private void ToucheAuPremierPlan() => BringToFrontCommand.Execute(null);

    [RelayCommand(CanExecute = nameof(PeutPorterALArrierePlanAuClavier))]
    private void ToucheALArrierePlan() => SendToBackCommand.Execute(null);

    private bool PeutToutSelectionnerAuClavier() => !ConsumesTyping && SelectAllCommand.CanExecute(null);

    private bool PeutDupliquerAuClavier() => !ConsumesTyping && DuplicateSelectedCommand.CanExecute(null);

    private bool PeutGrouperAuClavier() => !ConsumesTyping && GroupSelectedCommand.CanExecute(null);

    private bool PeutDissocierAuClavier() => !ConsumesTyping && UngroupSelectedCommand.CanExecute(null);

    private bool PeutPorterAuPremierPlanAuClavier() => !ConsumesTyping && BringToFrontCommand.CanExecute(null);

    private bool PeutPorterALArrierePlanAuClavier() => !ConsumesTyping && SendToBackCommand.CanExecute(null);

    // ------------------------------------------------------------------ Alt+flèches

    /// <summary>
    /// <c>Alt</c>+flèche pendant la saisie d'un bloc : le bloc dont le texte a le curseur se
    /// déplace, comme l'aide de Publisher en français le dit, de 3 pt (<c>Maj</c> : 0,5 pt), et
    /// la saisie continue. Le déplacement fait son propre pas : la frappe d'avant est refermée
    /// dans le sien, et la transaction de la saisie rouverte aussitôt (<c>UnGesteUnPas</c>).
    /// </summary>
    public void DeplacerLeBlocEnSaisie(double dx, double dy, bool fine) =>
        UnGesteUnPas(() => NudgeSelection(dx, dy, fine));
}
