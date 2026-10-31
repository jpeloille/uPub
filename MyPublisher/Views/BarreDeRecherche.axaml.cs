using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// La barre de recherche (poste N20). Voir l'en-tête de <c>BarreDeRecherche.axaml</c>, et
/// <c>MainWindowViewModel.Rechercher.cs</c>, où vit ce qu'elle fait.
///
/// Elle tient ici ses touches et son focus : <c>Ctrl+F</c> et <c>Ctrl+H</c> la font prendre le
/// focus, au texte cherché ou au texte de remplacement, tout le texte pris ; <c>Entrée</c> dans
/// l'un ou l'autre cherche le suivant, le focus restant dans la barre ; <c>Échap</c> la ferme —
/// avant la fenêtre, pour laquelle <c>Échap</c> désélectionnerait — et rend le focus au texte
/// trouvé s'il en est un de sélectionné, la planche sinon (règle 13 de la phase N).
/// </summary>
public partial class BarreDeRecherche : UserControl
{
    private MainWindowViewModel? _vm;

    public BarreDeRecherche()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, SurTouche, RoutingStrategies.Tunnel);

        // Fermée, la barre ne garde pas le focus : un champ caché qui l'aurait laisserait les
        // lettres seules s'y écrire, invisibles, au lieu d'armer les outils.
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && !IsVisible) RendreLaPlanche();
        };

        DataContextChanged += (_, _) => Ecouter();
        Ecouter();
    }

    private void Ecouter()
    {
        if (_vm is not null) _vm.RechercheDemandee -= OnRechercheDemandee;
        _vm = DataContext as MainWindowViewModel;
        if (_vm is not null) _vm.RechercheDemandee += OnRechercheDemandee;
    }

    /// <summary>
    /// La barre est demandée : le champ prend le focus, tout son texte pris, une fois que la mise
    /// en page l'a posé — la barre vient peut-être de paraître.
    /// </summary>
    private void OnRechercheDemandee(object? sender, bool remplacer)
    {
        var champ = remplacer ? TexteDeRemplacement : TexteCherche;
        Dispatcher.UIThread.Post(() =>
        {
            if (!champ.IsEffectivelyVisible) return;
            champ.Focus();
            champ.SelectAll();
        }, DispatcherPriority.Background);
    }

    /// <summary><c>Entrée</c> cherche le suivant ; <c>Échap</c> ferme la barre et rend la planche.</summary>
    private void SurTouche(object? sender, KeyEventArgs e)
    {
        if (_vm is null || e.KeyModifiers != KeyModifiers.None) return;

        switch (e.Key)
        {
            case Key.Escape:
                _vm.FermerLaRechercheCommand.Execute(null);
                RendreLaPlanche();
                e.Handled = true;
                break;
            case Key.Enter when e.Source is TextBox:
                _vm.RechercherLeSuivantCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Le focus quitte la barre, s'il y était : les touches reviennent à la planche.</summary>
    private void RendreLaPlanche()
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager is { } focus &&
            focus.GetFocusedElement() is Visual champ && champ.GetSelfAndVisualAncestors().Contains(this))
        {
            focus.ClearFocus();
        }
    }
}
