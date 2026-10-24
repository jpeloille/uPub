using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// La pile d'une page lue de l'avant vers le fond : ce que montre « Blocs de la page »
/// depuis la phase I (poste I2), l'avant en tête comme dans la maquette et dans
/// Publisher. Le modèle, lui, ne change pas : <see cref="Page.Frames"/> va toujours du
/// fond vers l'avant, le dernier bloc étant dessiné par-dessus les autres — c'est l'ordre
/// du rendu, de l'export et du test de survol.
///
/// **Une vue, et non une copie.** La liste suit la page qu'on modifie en place —
/// insertion, suppression, premier plan, glissé dans la liste elle-même — et chaque
/// changement de la collection est retraduit ici, élément pour élément, au rang miroir.
/// Une copie retenue à l'ouverture de la page manquerait le premier bloc tracé ; une
/// remise à zéro à chaque changement ferait perdre à la liste sa sélection et son
/// défilement, qu'un glissé vers le bas de la liste ne supporte pas.
///
/// **Une seule vue par pile**, tenue par <see cref="De"/>. La vue s'abonne à la
/// collection de la page, qui la garde donc en vie aussi longtemps qu'elle-même : en
/// créer une neuve à chaque retour sur une page empilerait les abonnés sur la même
/// collection — c'est la fuite que le poste Q2 a fermée pour le texte composé. La
/// table faible les attache l'une à l'autre sans retenir ni l'une ni l'autre.
/// </summary>
public sealed class PileInversee : IList<Frame>, IList, IReadOnlyList<Frame>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private static readonly ConditionalWeakTable<IList<Frame>, PileInversee> Vues = new();

    private readonly IList<Frame> _pile;

    private PileInversee(IList<Frame> pile)
    {
        _pile = pile;
        if (pile is INotifyCollectionChanged observable) observable.CollectionChanged += SurChangement;
    }

    /// <summary>La vue inversée de cette pile — toujours la même pour la même pile.</summary>
    public static PileInversee De(IList<Frame> pile) => Vues.GetValue(pile, p => new PileInversee(p));

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Count => _pile.Count;

    /// <summary>Le rang dans la page du bloc montré à ce rang de la liste, et réciproquement.</summary>
    public int Miroir(int rang) => _pile.Count - 1 - rang;

    public Frame this[int index]
    {
        get => _pile[Miroir(index)];
        set => throw new NotSupportedException("La liste montre la page ; elle ne la modifie pas.");
    }

    public int IndexOf(Frame item)
    {
        var rang = _pile.IndexOf(item);
        return rang < 0 ? -1 : Miroir(rang);
    }

    public bool Contains(Frame item) => _pile.Contains(item);

    public IEnumerator<Frame> GetEnumerator()
    {
        for (var i = _pile.Count - 1; i >= 0; i--) yield return _pile[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void CopyTo(Frame[] array, int arrayIndex)
    {
        foreach (var bloc in this) array[arrayIndex++] = bloc;
    }

    /// <summary>
    /// Chaque changement de la page, au rang miroir. Les rangs se calculent sur le compte
    /// d'APRÈS le changement, que la collection a déjà : un bloc ajouté au rang n d'une
    /// page qui en compte maintenant c se montre au rang c − 1 − n ; un bloc retiré du
    /// rang n d'une page qui en comptait c + 1, au rang c − n. Un déplacement garde le
    /// compte.
    /// </summary>
    private void SurChangement(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var compte = _pile.Count;
        NotifyCollectionChangedEventArgs traduit = e.Action switch
        {
            NotifyCollectionChangedAction.Add when e.NewItems is { Count: 1 } =>
                new(NotifyCollectionChangedAction.Add, e.NewItems[0], compte - 1 - e.NewStartingIndex),
            NotifyCollectionChangedAction.Remove when e.OldItems is { Count: 1 } =>
                new(NotifyCollectionChangedAction.Remove, e.OldItems[0], compte - e.OldStartingIndex),
            NotifyCollectionChangedAction.Move when e.NewItems is { Count: 1 } =>
                new(NotifyCollectionChangedAction.Move, e.NewItems[0],
                    compte - 1 - e.NewStartingIndex, compte - 1 - e.OldStartingIndex),
            NotifyCollectionChangedAction.Replace when e.NewItems is { Count: 1 } && e.OldItems is { Count: 1 } =>
                new(NotifyCollectionChangedAction.Replace, e.NewItems[0], e.OldItems[0], compte - 1 - e.NewStartingIndex),
            // Une remise à zéro, ou un changement de plusieurs blocs à la fois, que
            // « ObservableCollection » n'émet pas mais qu'une autre collection pourrait :
            // la liste se relit en entier plutôt que de recevoir des rangs faux.
            _ => new(NotifyCollectionChangedAction.Reset),
        };

        if (e.Action != NotifyCollectionChangedAction.Move && e.Action != NotifyCollectionChangedAction.Replace)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        CollectionChanged?.Invoke(this, traduit);
    }

    // La liste est en lecture seule : on ne réordonne pas la page par elle, mais par
    // les commandes du modèle de vue, qui ouvrent le pas d'annulation.
    public bool IsReadOnly => true;
    public void Add(Frame item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public void Insert(int index, Frame item) => throw new NotSupportedException();
    public bool Remove(Frame item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();

    bool IList.IsFixedSize => false;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object? IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
    int IList.Add(object? value) => throw new NotSupportedException();
    bool IList.Contains(object? value) => value is Frame bloc && Contains(bloc);
    int IList.IndexOf(object? value) => value is Frame bloc ? IndexOf(bloc) : -1;
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void ICollection.CopyTo(Array array, int index)
    {
        foreach (var bloc in this) array.SetValue(bloc, index++);
    }
}
