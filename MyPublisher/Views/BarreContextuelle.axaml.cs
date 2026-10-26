using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// La barre contextuelle (phase I). Le poste I2 y a posé la pastille ; le poste I3 la
/// remplit. Voir l'en-tête de <c>BarreContextuelle.axaml</c>.
/// </summary>
public partial class BarreContextuelle : UserControl
{
    /// <summary>Le sélecteur de couleur ouvert sous sa vignette, le temps de sa transaction.</summary>
    private FlyoutBase? _couleurOuverte;

    public BarreContextuelle()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, SurTouche, RoutingStrategies.Bubble);

        // Le bloc change sous un sélecteur ouvert — un clic sur la planche, une annulation :
        // le sélecteur se referme avec sa transaction, avant que le gabarit qui le portait
        // ne passe au bloc d'après, ou ne se masque (Q23).
        ReglagesDuBloc.PropertyChanged += (_, e) =>
        {
            if (e.Property == GabaritsGardes.BlocProperty) FermerLaCouleur();
        };
        DetachedFromVisualTree += (_, _) => FermerLaCouleur();

        DataContextChanged += (_, _) => EcouterLAvis();
        EcouterLAvis();
    }

    private MainWindowViewModel? _vm;

    private void EcouterLAvis()
    {
        if (_vm is not null) _vm.TransactionsRefermees -= OnTransactionsRefermees;
        _vm = DataContext as MainWindowViewModel;
        if (_vm is not null) _vm.TransactionsRefermees += OnTransactionsRefermees;
    }

    /// <summary>
    /// Annuler ou Rétablir a refermé la transaction du sélecteur de couleur avec les autres
    /// (poste Q24). Encore ouvert — il n'y avait rien à défaire —, il rouvre la sienne, que sa
    /// fermeture refermera : la pastille choisie ensuite fait son pas. Une annulation qui relit
    /// le document l'a refermé avec le bloc, et rien ne se rouvre pour lui.
    /// </summary>
    private void OnTransactionsRefermees(object? sender, EventArgs e)
    {
        if (_couleurOuverte is not null) _vm?.BeginEdit();
    }

    /// <summary>
    /// <c>Échap</c> dans un champ de la barre rend la main à la planche : le champ perd le
    /// focus, ce qu'on y a tapé est validé comme à toute sortie de champ, et la transaction
    /// d'édition se referme avec lui en un pas d'annulation (règle 2). Les lettres seules
    /// redeviennent alors des outils — <c>T</c> arme l'outil texte —, alors que pendant la
    /// saisie elles s'écrivent dans le champ (règle 9 ; c'est la fenêtre qui les lui laisse,
    /// <c>ConsumesTyping</c>).
    ///
    /// Sans cela, rien ne sortait du champ au clavier : la fenêtre laisse passer toutes les
    /// touches d'un champ qui a le focus, <c>Échap</c> compris, et la lettre suivante
    /// s'écrivait encore dans le corps. Une liste ouverte se referme d'abord : elle traite
    /// son <c>Échap</c>, qui n'arrive pas jusqu'ici, et le suivant quitte le champ.
    /// </summary>
    private void SurTouche(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None) return;
        if (TopLevel.GetTopLevel(this)?.FocusManager is not { } focus) return;
        if (focus.GetFocusedElement() is not Control champ || !champ.GetSelfAndVisualAncestors().Contains(this)) return;

        focus.ClearFocus();
        e.Handled = true;
    }

    // ------------------------------------------------------------ la couleur du texte

    /// <summary>
    /// Le sélecteur de couleur s'ouvre dans sa propre racine, hors de la barre : le focus
    /// qu'il prend n'arrive pas jusqu'à elle, et la transaction que la fenêtre ouvre au focus
    /// de la barre (règle 2) ne l'encadrait pas — la pastille choisie changeait le bloc sans
    /// laisser de pas d'annulation. La transaction s'ouvre donc ici à l'ouverture du
    /// sélecteur, et se referme à sa fermeture : un passage par le sélecteur est un geste,
    /// et un pas.
    ///
    /// Un champ de la barre qui avait le focus le rend d'abord : sa saisie fait son pas à
    /// elle, au lieu de se fondre dans celui de la couleur.
    /// </summary>
    private void SurCouleurOuverte(object? sender, EventArgs e)
    {
        if (_couleurOuverte is not null || DataContext is not MainWindowViewModel vm) return;

        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        _couleurOuverte = sender as FlyoutBase;
        vm.BeginEdit();
    }

    /// <summary>
    /// La transaction de la couleur se referme dès que le volet commence à se refermer, avant
    /// qu'il ne rende le focus au dernier élément qui l'avait (poste Q25). Pendant une saisie
    /// sur la page, c'est la zone de saisie : sa transaction se rouvre à ce focus, et, celle de
    /// la couleur encore ouverte, s'y emboîtait — la pastille et la frappe qui suivait se
    /// fondaient en un pas.
    /// </summary>
    private void SurCouleurQuiSeFerme(object? sender, CancelEventArgs e) => RefermerLaTransactionDeLaCouleur(sender);

    private void SurCouleurFermee(object? sender, EventArgs e)
    {
        if (sender is not FlyoutBase) return;

        // Le volet refermé rend le focus au dernier champ qui l'avait dans la fenêtre. Un champ
        // de la barre est gardé d'une sélection à l'autre depuis Q23 : il le reprenait, et la
        // transaction que la fenêtre ouvre à son focus engloutissait celle de la couleur, qui
        // ne faisait plus son pas. Il le rend aussitôt, comme à l'ouverture du sélecteur. La
        // zone de saisie de la page, elle, le garde : la frappe continue (Q25).
        if (TopLevel.GetTopLevel(this)?.FocusManager is { } focus &&
            focus.GetFocusedElement() is Control champ && champ.GetSelfAndVisualAncestors().Contains(this))
        {
            focus.ClearFocus();
        }

        // Un volet refermé sans avoir dit qu'il se refermait, ou par FermerLaCouleur, ne laisse
        // pas sa transaction ouverte.
        RefermerLaTransactionDeLaCouleur(sender);
    }

    private void RefermerLaTransactionDeLaCouleur(object? volet)
    {
        if (!ReferenceEquals(volet, _couleurOuverte)) return;

        _couleurOuverte = null;
        (DataContext as MainWindowViewModel)?.CommitEdit();
    }

    private void FermerLaCouleur()
    {
        if (_couleurOuverte is not { } volet) return;

        volet.Hide();
        // Un sélecteur dont la vignette a déjà quitté l'arbre peut se refermer sans le dire :
        // la transaction ne reste pas ouverte pour autant.
        SurCouleurFermee(volet, EventArgs.Empty);
    }
}
