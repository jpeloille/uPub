using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;
using MyPublisher.Services;

namespace MyPublisher.ViewModels;

/// <summary>
/// Le dialogue des coordonnées. Il travaille sur une copie : abandonner le laisse sans
/// trace, valider remplace les jeux de l'application et les écrit sur le disque.
/// </summary>
public partial class BusinessInfoViewModel : ViewModelBase
{
    public BusinessInfoViewModel(BusinessInfoStore store)
    {
        var copy = store.Clone();
        Sets = new ObservableCollection<BusinessInfo>(copy.Sets);

        // Un premier jeu vide plutôt qu'une liste vide : le dialogue s'ouvre sur des
        // champs à remplir, et non sur un écran qui demanderait d'abord de créer.
        if (Sets.Count == 0) Sets.Add(new BusinessInfo());

        Selected = Sets.FirstOrDefault(set => set.SetName == copy.ActiveSetName) ?? Sets[0];
    }

    public ObservableCollection<BusinessInfo> Sets { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSetCommand))]
    private BusinessInfo? _selected;

    /// <summary>Le dernier jeu ne se supprime pas : il faut bien des coordonnées à afficher.</summary>
    public bool CanRemoveSet => Selected is not null && Sets.Count > 1;

    [RelayCommand]
    private void AddSet()
    {
        var set = new BusinessInfo { SetName = $"Jeu {Sets.Count + 1}" };
        Sets.Add(set);
        Selected = set;
        RemoveSetCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSet))]
    private void RemoveSet()
    {
        if (Selected is not { } set || Sets.Count <= 1) return;

        var index = Sets.IndexOf(set);
        Sets.Remove(set);
        Selected = Sets[System.Math.Clamp(index, 0, Sets.Count - 1)];
        RemoveSetCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Les jeux tels qu'ils seront enregistrés, le jeu affiché devenant l'actif.</summary>
    public BusinessInfoStore ToStore() => new()
    {
        Sets = Sets.ToList(),
        ActiveSetName = Selected?.SetName ?? ""
    };
}
