using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que les menus de Publisher (phase N, poste N13) ajoutent au modèle de vue : « Regrouper »,
/// au menu Disposition, et le bloc voisin, que Tab et Maj+Tab prennent sur la planche. Un
/// fichier à lui, comme la règle 10 de la phase I le veut.
/// </summary>
public partial class MainWindowViewModel
{
    // ------------------------------------------------------------------ Regrouper

    /// <summary>
    /// Le dernier groupe dissous : le groupe d'avant, pour son nom, et les blocs qu'il a
    /// rendus à la page. Retenu par <see cref="UngroupSelected"/> ; des références, et non des
    /// rangs : un bloc déplacé, retouché ou passé devant un autre reste le même bloc.
    /// </summary>
    private (GroupFrame Groupe, Frame[] Membres)? _groupeDissous;

    /// <summary>Retient le groupe que <see cref="UngroupSelected"/> vient de dissoudre.</summary>
    private void RetenirLeGroupeDissous(GroupFrame groupe, IEnumerable<Frame> membres) =>
        _groupeDissous = (groupe, membres.ToArray());

    /// <summary>
    /// « Regrouper » s'offre tant que les blocs du dernier groupe dissous sont tous encore sur
    /// la page courante, à sa racine — aucun n'a été supprimé ni mis dans un autre groupe. Une
    /// annulation relit le document entier (<see cref="Restore"/>) : les blocs d'avant sont
    /// alors d'autres instances, et le groupe n'est plus à refaire — l'annulation de la
    /// dissociation l'a déjà refait.
    /// </summary>
    public bool PeutRegrouper =>
        _groupeDissous is { Membres.Length: > 1 } retenu && CurrentPage is { } page &&
        retenu.Membres.All(page.Frames.Contains);

    /// <summary>
    /// Refait le dernier groupe dissous, en un pas : Publisher, Arrange ▸ Regroup (le livre,
    /// p. 16 et p. 58). Les blocs se regroupent comme Grouper les grouperait — l'empreinte de
    /// l'ensemble, au rang du plus avancé — et le groupe reprend son nom. La sélection
    /// n'y entre pas : il suffit que les blocs soient là. Comme l'œil de I8, la commande
    /// referme d'abord, en un pas à elle, la transaction qu'un champ tiendrait ouverte, puis
    /// la rouvre : le menu se joue aussi pendant une frappe.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PeutRegrouper))]
    private void Regrouper()
    {
        if (!PeutRegrouper || _groupeDissous is not { } retenu) return;

        var ouvertes = _editDepth;
        EndPendingEdit();

        BeginEdit();
        SetSelection(retenu.Membres);
        GroupSelected();
        if (SingleSelected is GroupFrame groupe) groupe.Name = retenu.Groupe.Name;
        CommitEdit();

        for (var i = 0; i < ouvertes; i++) BeginEdit();

        _groupeDissous = null;
        RegrouperCommand.NotifyCanExecuteChanged();
        Status = $"Groupe refait, {retenu.Membres.Length} blocs";
    }

    // ------------------------------------------------------------ le bloc voisin

    /// <summary>
    /// Tab et Maj+Tab sur la planche : le bloc suivant, ou le précédent, dans l'ordre de la
    /// page — celui de la superposition, du fond vers l'avant, le même que Publisher parcourt.
    /// On part du bloc que la sélection désigne (son ancre) ; sans sélection, Tab prend le
    /// premier, Maj+Tab le dernier ; au bout, on repart de l'autre bout. Les blocs masqués
    /// (I8) sont sautés : la planche ne les montre pas. Aucun pas d'annulation : seule la
    /// sélection change. Rend <c>false</c> quand il n'y a rien à prendre.
    /// </summary>
    public bool SelectionnerLeBlocVoisin(bool enArriere)
    {
        if (CurrentPage is not { } page) return false;

        var blocs = page.Frames;
        var depart = Selected is { } ancre ? blocs.IndexOf(ancre) : -1;
        var pas = enArriere ? -1 : 1;
        if (depart < 0) depart = enArriere ? blocs.Count : -1;

        for (var i = 1; i <= blocs.Count; i++)
        {
            var rang = ((depart + pas * i) % blocs.Count + blocs.Count) % blocs.Count;
            if (!blocs[rang].Visible) continue;

            SelectOnly(blocs[rang]);
            Status = SelectionLabel;
            return true;
        }

        return false;
    }
}
