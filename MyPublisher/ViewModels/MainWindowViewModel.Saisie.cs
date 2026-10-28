using System;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce qu'entrer dans le texte d'un clic (poste N12) ajoute au modèle de vue : la demande
/// d'ouvrir la saisie d'un bloc qu'il vient de poser. Un fichier à lui, comme la règle 10 de
/// la phase I le veut.
///
/// Un bloc de texte tracé à l'outil s'ouvre en saisie, vide, le curseur dedans : c'est la
/// planche qui le trace, et qui l'ouvre. Celui que pose le menu Insertion était posé sans
/// saisie, et il fallait double-cliquer dedans. Publisher y met le point d'insertion
/// (p. 33) ; le modèle de vue ne connaît pas la saisie, qui est l'affaire de la planche, et la
/// lui demande.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Un bloc de texte vient d'être posé hors de la planche — par le menu Insertion — et
    /// demande la saisie, le curseur dedans. La planche l'écoute.
    /// </summary>
    public event EventHandler<TextFrame>? SaisieDemandee;

    private void DemanderLaSaisie(TextFrame bloc) => SaisieDemandee?.Invoke(this, bloc);
}
