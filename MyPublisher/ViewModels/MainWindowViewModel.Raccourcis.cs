using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le poste N10 ajoute au modèle de vue : les raccourcis de Publisher en français, relevés
/// sur l'aide de Microsoft et écrits dans la fiche 24 (<c>Docs/Publisher2007/24-raccourcis-clavier-francais.md</c>).
/// Un fichier à lui, comme la règle 10 de la phase I le veut.
///
/// Les lettres et les touches nommées restent des liaisons de la fenêtre (<c>Window.KeyBindings</c>) :
/// <c>Ctrl+E</c>, <c>L</c>, <c>R</c> et <c>J</c> alignent le texte, <c>Ctrl+Maj+P</c> exporte,
/// <c>Ctrl+Maj+G</c> groupe ou dissocie, <c>Ctrl+Maj+X</c> supprime l'objet, <c>Alt+Retour arrière</c>
/// et <c>F4</c> annulent et rétablissent. Les raccourcis qui sont des caractères — « ] », « [ »,
/// « &gt; », « &lt; » — passent par une table, <see cref="RaccourcisParCaractere"/>, que la fenêtre
/// lit sur le caractère que la frappe produit : une liaison compare la touche, jamais le
/// caractère, et sur un AZERTY « &lt; » et « &gt; » partagent une touche, « ] » et « [ » demandent
/// AltGr. La sonde du clavier (<c>Views/SondeDuClavier.cs</c>) l'a relevé sur la machine du
/// propriétaire.
/// </summary>
public partial class MainWindowViewModel
{
    // ------------------------------------------------------- les raccourcis par caractère

    private IReadOnlyDictionary<string, IRelayCommand>? _raccourcisParCaractere;

    /// <summary>
    /// La table des raccourcis lus sur le caractère, sous <c>Ctrl</c> : <c>Maj</c> et AltGr n'y
    /// comptent pas, puisqu'ils servent à produire le caractère — « &gt; » demande <c>Maj</c> sur
    /// un QWERTY comme sur un AZERTY, « ] » demande AltGr sur un AZERTY. Chaque geste n'y est
    /// qu'une fois, et nulle part ailleurs : la recette de A4 la confronte aux liaisons et au
    /// README.
    /// </summary>
    public IReadOnlyDictionary<string, IRelayCommand> RaccourcisParCaractere => _raccourcisParCaractere ??=
        new Dictionary<string, IRelayCommand>
        {
            ["]"] = CorpsPlusUnPointCommand,
            ["["] = CorpsMoinsUnPointCommand,
            [">"] = CorpsSuivantCommand,
            ["<"] = CorpsPrecedentCommand,
        };

    /// <summary>
    /// Joue le raccourci que la table donne au caractère, s'il y en a un et que sa commande peut
    /// s'exécuter. Vrai si la touche est prise : sinon, elle continue son chemin, comme une
    /// liaison dont la commande ne peut pas s'exécuter la rend au champ.
    /// </summary>
    public bool JouerLeRaccourciParCaractere(string? caractere)
    {
        if (caractere is null || !RaccourcisParCaractere.TryGetValue(caractere, out var commande)) return false;
        if (!commande.CanExecute(null)) return false;

        commande.Execute(null);
        return true;
    }

    // ------------------------------------------------------------------------ le corps

    /// <summary><c>Ctrl+]</c> : le corps d'un point de plus.</summary>
    [RelayCommand(CanExecute = nameof(PorteDuTexte))]
    private void CorpsPlusUnPoint() => ChangerLeCorps(corps => CorpsStandard.DUnPoint(corps, +1));

    /// <summary><c>Ctrl+[</c> : le corps d'un point de moins.</summary>
    [RelayCommand(CanExecute = nameof(PorteDuTexte))]
    private void CorpsMoinsUnPoint() => ChangerLeCorps(corps => CorpsStandard.DUnPoint(corps, -1));

    /// <summary><c>Ctrl+Maj+&gt;</c> : la taille suivante de la liste standard.</summary>
    [RelayCommand(CanExecute = nameof(PorteDuTexte))]
    private void CorpsSuivant() => ChangerLeCorps(CorpsStandard.Suivant);

    /// <summary><c>Ctrl+Maj+&lt;</c> : la taille précédente de la liste standard.</summary>
    [RelayCommand(CanExecute = nameof(PorteDuTexte))]
    private void CorpsPrecedent() => ChangerLeCorps(CorpsStandard.Precedent);

    /// <summary>
    /// Le corps, en un pas d'annulation, où que soit le focus (règle 2) : sur un bloc de texte, celui
    /// des caractères visés (poste R7, <c>MiseEnFormeVisee</c>) — la sélection ou le mot du curseur
    /// en saisie, tous les caractères du bloc hors saisie, chacun depuis le sien ; sur un tableau, le
    /// tableau, comme le gras.
    /// </summary>
    private void ChangerLeCorps(Func<double, double> nouveau)
    {
        switch (BlocVise)
        {
            case TextFrame:
                MettreEnFormeLeTexte(visee => visee.ChangerLeCorps(nouveau),
                    _ => MiseEnFormeVisee.FontSize is { } corps ? $"Corps : {corps} pt" : "Corps changé");
                break;
            case TableFrame tableau:
                var duTableau = nouveau(tableau.FontSize);
                Retoucher(_ => { }, t => t.FontSize = duTableau, $"Corps : {duTableau} pt");
                break;
        }
    }

    // -------------------------------------------------------------------------- l'export

    private EventHandler? _exportDemande;

    /// <summary>
    /// Exporter en PDF, <c>Ctrl+Maj+P</c> : le choix du fichier est une boîte de la fenêtre, qui
    /// s'abonne ici, comme la planche à <see cref="PageEntiereDemandee"/>. Tant que personne
    /// n'écoute, la commande ne peut pas s'exécuter.
    /// </summary>
    public event EventHandler? ExportDemande
    {
        add
        {
            _exportDemande += value;
            ExporterEnPdfCommand.NotifyCanExecuteChanged();
        }
        remove
        {
            _exportDemande -= value;
            ExporterEnPdfCommand.NotifyCanExecuteChanged();
        }
    }

    private bool PeutExporterEnPdf() => _exportDemande is not null;

    /// <summary>Le menu Fichier, le bouton de la barre principale et <c>Ctrl+Maj+P</c>, même en saisie.</summary>
    [RelayCommand(CanExecute = nameof(PeutExporterEnPdf))]
    private void ExporterEnPdf() => _exportDemande?.Invoke(this, EventArgs.Empty);

    // ---------------------------------------------------------------- grouper, supprimer

    /// <summary>
    /// <c>Ctrl+Maj+G</c> groupe les blocs pris, ou dissocie le groupe pris : « Grouper les objets
    /// sélectionnés ou dissocier les objets groupés », dit l'aide ; le livre le montre aussi
    /// (p. 44, 46). Par les commandes du clavier de N6 : tant qu'un champ a le clavier, la touche
    /// se tait et revient au champ.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PeutGrouperOuDissocierAuClavier))]
    private void ToucheGrouperOuDissocier()
    {
        if (ToucheGrouperCommand.CanExecute(null)) ToucheGrouperCommand.Execute(null);
        else ToucheDissocierCommand.Execute(null);
    }

    private bool PeutGrouperOuDissocierAuClavier() =>
        ToucheGrouperCommand.CanExecute(null) || ToucheDissocierCommand.CanExecute(null);

    /// <summary>
    /// <c>Ctrl+Maj+X</c> : supprimer l'objet sélectionné, même en saisie — l'aide le met à côté de
    /// <c>Suppr</c>, qui, lui, efface du texte dans un champ. La frappe en cours est refermée
    /// d'abord, dans son pas ; la suppression fait le sien, et referme la saisie du bloc qu'elle
    /// emporte.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ToucheSupprimerLObjet()
    {
        EndPendingEdit();
        DeleteSelectedCommand.Execute(null);
    }
}
