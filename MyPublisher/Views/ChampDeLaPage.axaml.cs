using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Le numéro de page de la barre d'état, devenu un champ (poste N18). Voir l'en-tête de
/// <c>ChampDeLaPage.axaml</c>.
///
/// Il montre la page courante tant qu'on n'y tape pas, et ne change de page qu'à Entrée — jamais
/// à la frappe : « 1 », en route vers « 12 », n'est pas une page qu'on a demandée. Ce qu'on y
/// tape se lit dans le modèle de vue (<see cref="MainWindowViewModel.AtteindreLaPage(string?)"/>),
/// qui refuse avec un message ce qui n'est pas une page du document. C'est le geste du champ du
/// zoom (<c>ChampDuZoom</c>, N4), sans liste.
/// </summary>
public partial class ChampDeLaPage : UserControl
{
    private MainWindowViewModel? _vm;

    /// <summary>Le clic qui donne le focus : son relâchement prend tout le numéro, qu'une frappe remplace.</summary>
    private bool _toutPrendreAuRelachement;

    public ChampDeLaPage()
    {
        InitializeComponent();

        NumeroDePage.AddHandler(KeyDownEvent, SurTouche, RoutingStrategies.Tunnel);
        NumeroDePage.AddHandler(PointerPressedEvent, (_, _) => _toutPrendreAuRelachement = !NumeroDePage.IsFocused,
            RoutingStrategies.Tunnel);
        NumeroDePage.AddHandler(PointerReleasedEvent, SurRelachement, RoutingStrategies.Bubble, handledEventsToo: true);
        NumeroDePage.GotFocus += SurFocusPris;
        NumeroDePage.LostFocus += (_, _) => Renoncer(rendreLaPlanche: false);

        DataContextChanged += (_, _) => Suivre();
        Suivre();
    }

    /// <summary>
    /// <c>F5</c>, <c>Ctrl+B</c> ou <i>Page ▸ Atteindre la page…</i> : le focus au numéro, tout pris,
    /// prêt pour la frappe.
    /// </summary>
    public void Prendre()
    {
        NumeroDePage.Focus(NavigationMethod.Tab);
        NumeroDePage.SelectAll();
    }

    private void Suivre()
    {
        if (_vm is not null) _vm.PropertyChanged -= SurModeleDeVue;
        _vm = DataContext as MainWindowViewModel;
        if (_vm is not null) _vm.PropertyChanged += SurModeleDeVue;
        Afficher();
    }

    /// <summary>
    /// La page changée ailleurs — une vignette, Ctrl+Pg.Suiv, une page ajoutée — se lit dans le
    /// champ ; pas pendant qu'on y tape, où elle effacerait la frappe. Le total suit toujours.
    /// </summary>
    private void SurModeleDeVue(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.CurrentPageNumber) or nameof(MainWindowViewModel.CurrentPage)
                or nameof(MainWindowViewModel.PageCountLabel) or nameof(MainWindowViewModel.Document):
                if (!NumeroDePage.IsFocused) Afficher();
                else AfficherLeTotal();
                break;
        }
    }

    private void Afficher()
    {
        NumeroDePage.Text = _vm?.CurrentPageNumber.ToString(CultureInfo.CurrentCulture) ?? "";
        AfficherLeTotal();
    }

    private void AfficherLeTotal() =>
        TotalDesPages.Text = $"sur {(_vm?.Document.Pages.Count ?? 0).ToString(CultureInfo.CurrentCulture)}";

    private void SurRelachement(object? sender, PointerReleasedEventArgs e)
    {
        if (!_toutPrendreAuRelachement) return;

        _toutPrendreAuRelachement = false;
        NumeroDePage.SelectAll();
    }

    private void SurFocusPris(object? sender, GotFocusEventArgs e)
    {
        if (e.NavigationMethod != NavigationMethod.Pointer) NumeroDePage.SelectAll();
    }

    private void SurTouche(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Valider();
                e.Handled = true;
                break;
            case Key.Escape:
                Renoncer(rendreLaPlanche: true);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Entrée : la page atteinte, la planche reprend la main ; refusée, la barre d'état dit
    /// pourquoi, et le champ remontre la page courante, tout prise, pour qu'on retape.
    /// </summary>
    private void Valider()
    {
        if (_vm is null) return;

        if (_vm.AtteindreLaPage(NumeroDePage.Text))
        {
            Afficher();
            RendreLaPlanche();
            return;
        }

        Afficher();
        NumeroDePage.SelectAll();
    }

    private void Renoncer(bool rendreLaPlanche)
    {
        _toutPrendreAuRelachement = false;
        Afficher();
        if (rendreLaPlanche) RendreLaPlanche();
    }

    /// <summary>Le champ perd le focus, et les lettres seules redeviennent des outils (<c>ChampDuZoom</c>).</summary>
    private void RendreLaPlanche() => TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
}
