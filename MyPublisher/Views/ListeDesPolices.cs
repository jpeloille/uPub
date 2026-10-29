using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using MyPublisher.Converters;
using MyPublisher.Models;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// La liste des polices de la barre contextuelle, où l'on tape le nom de la police (poste N19 ;
/// fiche 23, b6) : « DejaVu Se » propose DejaVu Serif, et Entrée la pose.
///
/// **Une liste éditable d'Avalonia, dont on retient la frappe.** La <c>ComboBox</c> d'Avalonia
/// 11.3.9 s'édite (<c>IsEditable</c>, la zone <c>PART_EditableTextBox</c> du thème Fluent), mais
/// elle cherche, à chaque lettre, l'entrée dont le texte est exactement celui qui est tapé, et la
/// prend — sinon, elle n'en prend aucune (<c>ComboBox.TextChanged</c>, lu décompilé, et éprouvé
/// au banc) : taper « DejaVu Sans Mono » posait DejaVu Sans au onzième caractère, puis rien, puis
/// DejaVu Sans Mono ; et la liste ne proposait rien. Ici, la frappe ne fait que du texte et des
/// propositions — la liste s'ouvre sur les seules entrées dont le libellé commence par ce qui est
/// tapé, la première marquée — ; la police part à Entrée ou à Tab, ou par un choix dans la liste,
/// et elle part une fois. ↑ et ↓ promènent la marque parmi les propositions ; Échap renonce et rend
/// la main à la planche, comme un champ numérique (N14, règle 13) ; quitter la liste autrement
/// renonce aussi : on ne pose pas une police qu'on n'a pas confirmée.
///
/// **Un refus se dit.** Ce qui est tapé et ne commence aucune entrée — une police que la machine n'a pas,
/// et que le document n'emploie pas — est refusé : la liste remontre la police du bloc, et la barre
/// d'état dit pourquoi (<see cref="MainWindowViewModel.Status"/>, comme N14). Une police du document
/// que la machine n'a pas reste une entrée (Q20) : on la retrouve en tapant son nom, « (absente) ».
///
/// La zone montre le libellé de l'entrée, celui de la liste (<see cref="LibelleDePolice"/>) : « Corps
/// du jeu — DejaVu Sans », et non la référence <c>scheme:body</c> que le bloc porte. La sélection, elle,
/// reste la valeur que la liaison écrit au bloc — avec la garde de Q20, <see cref="JamaisNul"/>.
///
/// Elle garde l'apparence d'une <c>ComboBox</c> : les styles « ComboBox.champ » la prennent pour
/// l'une d'elles, et les tests la trouvent comme telle (Q20, règle 1).
/// </summary>
public class ListeDesPolices : ComboBox
{
    protected override Type StyleKeyOverride => typeof(ComboBox);

    /// <summary>Le jeu de polices du document, pour le libellé des deux premières entrées.</summary>
    public static readonly StyledProperty<JeuDePolices?> JeuProperty =
        AvaloniaProperty.Register<ListeDesPolices, JeuDePolices?>(nameof(Jeu));

    /// <summary>Les polices de la machine, pour dire lesquelles manquent.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> MachineProperty =
        AvaloniaProperty.Register<ListeDesPolices, IReadOnlyList<string>?>(nameof(Machine));

    public JeuDePolices? Jeu
    {
        get => GetValue(JeuProperty);
        set => SetValue(JeuProperty, value);
    }

    public IReadOnlyList<string>? Machine
    {
        get => GetValue(MachineProperty);
        set => SetValue(MachineProperty, value);
    }

    /// <summary>Vrai le temps que la liste écrit elle-même sa zone : ce n'est pas de la frappe.</summary>
    private bool _ecrit;

    /// <summary>Ce qui est tapé et attend d'être validé ; <c>null</c> quand la zone montre la police du bloc.</summary>
    private string? _saisie;

    /// <summary>Les indices des entrées proposées, dans l'ordre de la liste.</summary>
    private List<int> _propositions = [];

    /// <summary>L'indice de l'entrée marquée, celle qu'Entrée pose ; -1 sans marque.</summary>
    private int _marquee = -1;

    public ListeDesPolices()
    {
        IsEditable = true;
        // Une pile simple, et non la pile virtualisée du thème : les entrées écartées par la
        // frappe se masquent, et une pile virtualisée estime mal des entrées sans hauteur.
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel());
        ContainerPrepared += (_, e) => Preparer(e.Container, e.Index);
        // Au passage descendant, avant la zone de texte, qui prend ↑ et ↓ pour son curseur.
        AddHandler(KeyDownEvent, SurTouche, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => Abandonner();
    }

    /// <summary>Du texte tapé attend d'être validé.</summary>
    public bool EnAttente => _saisie is not null;

    /// <summary>La zone où l'on tape, celle du thème.</summary>
    private TextBox? _zone;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _zone = e.NameScope.Find<TextBox>("PART_EditableTextBox");
    }

    // ------------------------------------------------------------ la frappe

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == TextProperty)
        {
            // La recherche de la liste d'Avalonia — l'entrée de même texte prise à chaque lettre —
            // ne joue pas : c'est elle qui posait la police pendant la frappe. Rien d'autre, dans
            // la chaîne de la liste, ne regarde ce texte.
            if (!_ecrit) Proposer(change.GetNewValue<string?>() ?? "");
            return;
        }

        var aEcrire = change.Property == SelectedItemProperty || change.Property == IsEditableProperty
                      || change.Property == ItemsSourceProperty;
        _ecrit |= aEcrire;
        try
        {
            base.OnPropertyChanged(change);
        }
        finally
        {
            if (aEcrire) _ecrit = false;
        }

        if (change.Property == SelectedItemProperty)
        {
            // Une entrée prise — par la liaison, au pointeur, à Entrée — : la zone la montre, et ce
            // qui était tapé est oublié.
            Oublier();
            Remontrer();
        }
        else if (aEcrire || change.Property == JeuProperty || change.Property == MachineProperty)
        {
            if (!EnAttente) Remontrer();
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && !change.GetNewValue<bool>())
        {
            // Quitter la liste sans Entrée ni Tab renonce : un clic ailleurs ne pose pas la
            // police à moitié tapée.
            Abandonner();
        }
        else if (change.Property == IsDropDownOpenProperty && !change.GetNewValue<bool>() && !EnAttente)
        {
            Filtrer(null);
        }
    }

    /// <summary>La frappe : les entrées dont le libellé commence par ce qui est tapé, la première marquée.</summary>
    private void Proposer(string texte)
    {
        _saisie = texte;
        _propositions = texte.Length == 0 ? [] : Commencant(texte).ToList();
        var exacte = _propositions.FindIndex(i => string.Equals(Libelle(i), texte, StringComparison.CurrentCultureIgnoreCase));
        _marquee = _propositions.Count == 0 ? -1 : _propositions[Math.Max(0, exacte)];

        Filtrer(texte.Length == 0 ? null : _propositions);
        Ouvrir(_propositions.Count > 0);
        Marquer();
    }

    private IEnumerable<int> Commencant(string texte)
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (Libelle(i).StartsWith(texte, StringComparison.CurrentCultureIgnoreCase)) yield return i;
        }
    }

    // ------------------------------------------------------------ les touches

    private void SurTouche(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;

        switch (e.Key)
        {
            // Entrée pose l'entrée marquée, puis continue son chemin : la barre y rend la planche
            // (BarreContextuelle.SurTouche). Refusée, la liste garde le focus, tout pris.
            case Key.Enter when e.KeyModifiers == KeyModifiers.None:
                if (!Valider())
                {
                    ToutPrendre();
                    e.Handled = true;
                }
                return;

            // Tab valide aussi, puis passe au champ suivant ; un refus le retient.
            case Key.Tab:
                if (!Valider())
                {
                    ToutPrendre();
                    e.Handled = true;
                }
                return;

            // Échap renonce à ce qui est tapé et rend la planche. Sans rien de tapé, la liste
            // d'Avalonia se referme, et l'Échap suivant quitte la barre (BarreContextuelle.SurTouche).
            case Key.Escape when e.KeyModifiers == KeyModifiers.None && EnAttente:
                Abandonner();
                Ouvrir(false);
                TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
                e.Handled = true;
                return;

            // ↑ et ↓ promènent la marque, la liste ouverte — et l'ouvrent, sinon : rien ne part
            // avant Entrée.
            case Key.Up or Key.Down when e.KeyModifiers == KeyModifiers.None:
                Promener(e.Key == Key.Down ? +1 : -1);
                e.Handled = true;
                return;
        }
    }

    private void Promener(int sens)
    {
        if (!IsDropDownOpen)
        {
            if (!EnAttente) _marquee = SelectedIndex;
            Ouvrir(true);
            Marquer();
            return;
        }

        var candidates = EnAttente ? _propositions : Enumerable.Range(0, ItemCount).ToList();
        if (candidates.Count == 0) return;

        var place = candidates.IndexOf(_marquee);
        place = place < 0 ? (sens > 0 ? 0 : candidates.Count - 1) : Math.Clamp(place + sens, 0, candidates.Count - 1);
        _marquee = candidates[place];
        Marquer();
    }

    // ------------------------------------------------------------ valider, renoncer

    /// <summary>
    /// Pose l'entrée marquée, une fois — ou refuse ce qui est tapé, et le dit. Vrai si rien n'est
    /// refusé, rien d'attendu compris.
    /// </summary>
    public bool Valider()
    {
        if (!EnAttente)
        {
            // La liste ouverte sans rien taper, une entrée marquée aux flèches : Entrée la prend.
            if (IsDropDownOpen && _marquee >= 0 && _marquee < ItemCount) Poser(_marquee);
            return true;
        }

        var saisie = _saisie!.Trim();
        if (saisie.Length == 0)
        {
            Abandonner();
            return true;
        }

        if (_marquee < 0)
        {
            var avant = Libelle(SelectedIndex);
            Abandonner();
            if (TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel vm)
            {
                vm.Status = $"« {saisie} » n'est pas une police de cette machine : la police reste {avant}.";
            }

            return false;
        }

        Poser(_marquee);
        return true;
    }

    /// <summary>Ce qui est tapé est oublié : la zone remontre la police du bloc.</summary>
    public void Abandonner()
    {
        if (!EnAttente) return;

        Oublier();
        Remontrer();
        Filtrer(null);
    }

    private void Poser(int indice)
    {
        Oublier();
        Ouvrir(false);
        Filtrer(null);
        // La liaison écrit la police au bloc ; une entrée déjà prise ne repart pas, et la zone,
        // qui montrait la frappe, remontre son libellé.
        if (indice == SelectedIndex) Remontrer();
        else SetCurrentValue(SelectedIndexProperty, indice);
    }

    private void Oublier()
    {
        _saisie = null;
        _propositions = [];
        _marquee = -1;
        Marquer();
    }

    private void Remontrer()
    {
        _ecrit = true;
        try
        {
            SetCurrentValue(TextProperty, SelectedIndex < 0 ? "" : Libelle(SelectedIndex));
        }
        finally
        {
            _ecrit = false;
        }
    }

    private void ToutPrendre() => _zone?.SelectAll();

    /// <summary>
    /// Ouvre ou ferme la liste sans déranger la frappe. La liste d'Avalonia, en se refermant, se
    /// redonne le focus, et sa zone prend alors tout son texte (<c>ComboBox.PopupClosed</c>,
    /// <c>OnGotFocus</c>) : la lettre suivante aurait remplacé tout ce qui était tapé. Le curseur
    /// revient où il était.
    /// </summary>
    private void Ouvrir(bool ouverte)
    {
        if (IsDropDownOpen == ouverte) return;

        var curseur = _zone?.CaretIndex;
        SetCurrentValue(IsDropDownOpenProperty, ouverte);
        if (!ouverte && EnAttente && _zone is not null && curseur is { } c)
        {
            _zone.ClearSelection();
            _zone.CaretIndex = c;
        }
    }

    // ------------------------------------------------------------ la liste

    /// <summary>Le libellé d'une entrée, celui que la liste affiche.</summary>
    private string Libelle(int indice) =>
        indice < 0 || indice >= ItemCount ? "" :
        LibelleDePolice.Instance.Convert([Items[indice], Jeu, Machine], typeof(string), null, CultureInfo.CurrentCulture) as string ?? "";

    /// <summary>Les entrées gardées, ou toutes (<c>null</c>) : les autres se masquent.</summary>
    private HashSet<int>? _gardees;

    private void Filtrer(IEnumerable<int>? gardees)
    {
        _gardees = gardees?.ToHashSet();
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is { } entree) entree.IsVisible = _gardees?.Contains(i) ?? true;
        }
    }

    private void Marquer()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is not { } entree) continue;
            entree.Classes.Set("proposee", i == _marquee);
            if (i == _marquee) entree.BringIntoView();
        }
    }

    /// <summary>
    /// Une entrée de la liste ne prend pas le focus : la liste ouverte le volait à la zone, où la
    /// frappe continue (<c>ComboBox.TryFocusSelectedItem</c>). Le pointeur la choisit quand même.
    /// </summary>
    private void Preparer(Control entree, int indice)
    {
        entree.Focusable = false;
        entree.IsVisible = _gardees?.Contains(indice) ?? true;
        entree.Classes.Set("proposee", indice == _marquee);
    }
}
