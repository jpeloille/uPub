using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Le champ du zoom de la barre principale (poste N4). Voir l'en-tête de
/// <c>ChampDuZoom.axaml</c>.
///
/// Il montre le zoom tant qu'on n'y tape pas, et ne le change qu'à Entrée ou au choix
/// d'une entrée de sa liste — jamais à la frappe : « 15 », en route vers « 150 », n'est pas
/// un zoom qu'on a demandé. Ce qu'on y tape se lit dans le modèle de vue
/// (<see cref="MainWindowViewModel.SaisirLeZoom"/>), qui refuse avec un message ce qui
/// n'est pas un zoom. Échap, ou le focus perdu, y renonce ; Échap et Entrée rendent la
/// planche, comme Échap le fait d'un champ de la barre contextuelle : le champ perd le
/// focus, et les lettres seules redeviennent des outils.
/// </summary>
public partial class ChampDuZoom : UserControl
{
    private MainWindowViewModel? _vm;

    /// <summary>
    /// Le clic qui donne le focus au champ : son relâchement prend tout le texte, qu'une
    /// frappe remplace. Pris à la pression, il serait défait par la zone de texte, qui pose
    /// son curseur au point du clic.
    /// </summary>
    private bool _toutPrendreAuRelachement;

    public ChampDuZoom()
    {
        InitializeComponent();

        // Au passage descendant : la zone de texte prendrait les flèches pour son curseur.
        SaisieDuZoom.AddHandler(KeyDownEvent, SurTouche, RoutingStrategies.Tunnel);
        SaisieDuZoom.AddHandler(PointerPressedEvent, SurPression, RoutingStrategies.Tunnel);
        SaisieDuZoom.AddHandler(PointerReleasedEvent, SurRelachement, RoutingStrategies.Bubble, handledEventsToo: true);
        SaisieDuZoom.GotFocus += SurFocusPris;
        SaisieDuZoom.LostFocus += (_, _) => Renoncer(rendreLaPlanche: false);
        SaisieDuZoom.TextChanged += (_, _) => MarquerLaCourante();

        DataContextChanged += (_, _) => Suivre();
        Suivre();
    }

    private void Suivre()
    {
        if (_vm is not null) _vm.PropertyChanged -= SurModeleDeVue;
        _vm = DataContext as MainWindowViewModel;
        if (_vm is not null) _vm.PropertyChanged += SurModeleDeVue;
        Afficher();
    }

    /// <summary>
    /// Le zoom changé ailleurs — la molette, F9, un bouton — se lit dans le champ ; pas
    /// pendant qu'on y tape, où il effacerait la frappe.
    /// </summary>
    private void SurModeleDeVue(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.ZoomLabel) && !SaisieDuZoom.IsFocused) Afficher();
    }

    private void Afficher() => SaisieDuZoom.Text = _vm?.ZoomLabel ?? "";

    // ------------------------------------------------------------ le pointeur

    private void SurPression(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(SaisieDuZoom).Properties.IsLeftButtonPressed) return;

        // Passé descendant : la zone de texte n'a pas encore pris le focus.
        _toutPrendreAuRelachement = !SaisieDuZoom.IsFocused;
        Deplier(true);
    }

    private void SurRelachement(object? sender, PointerReleasedEventArgs e)
    {
        if (!_toutPrendreAuRelachement) return;

        _toutPrendreAuRelachement = false;
        SaisieDuZoom.SelectAll();
    }

    private void SurFocusPris(object? sender, GotFocusEventArgs e)
    {
        // Au clic, c'est le relâchement qui prend tout ; autrement — Tab, le code —, ici.
        if (e.NavigationMethod != NavigationMethod.Pointer) SaisieDuZoom.SelectAll();
    }

    // ------------------------------------------------------------ le clavier

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
            case Key.Down or Key.Up when e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                Deplier(!ListeDuZoom.IsOpen);
                e.Handled = true;
                break;
            case Key.Down:
                Parcourir(1);
                e.Handled = true;
                break;
            case Key.Up:
                Parcourir(-1);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Entrée : la saisie part au modèle de vue. Prise, la liste se replie et la planche
    /// reprend la main ; refusée, la barre d'état dit pourquoi, et le champ montre le zoom,
    /// qui n'a pas bougé, tout pris pour qu'on retape.
    /// </summary>
    private void Valider()
    {
        if (_vm is null) return;

        if (_vm.SaisirLeZoom(SaisieDuZoom.Text))
        {
            Deplier(false);
            RendreLaPlanche();
            return;
        }

        Afficher();
        SaisieDuZoom.SelectAll();
    }

    /// <summary>Ce qu'on a tapé est oublié : le champ remontre le zoom.</summary>
    private void Renoncer(bool rendreLaPlanche)
    {
        _toutPrendreAuRelachement = false;
        Afficher();
        Deplier(false);
        if (rendreLaPlanche) RendreLaPlanche();
    }

    /// <summary>
    /// Le champ perd le focus, et le clavier revient à la fenêtre : les lettres seules y
    /// arment les outils, les flèches y poussent la sélection. C'est le geste de la barre
    /// contextuelle (<c>BarreContextuelle.SurTouche</c>).
    /// </summary>
    private void RendreLaPlanche() => TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();

    // ------------------------------------------------------------ la liste

    private void Deplier(bool ouvrir)
    {
        if (ListeDuZoom.IsOpen == ouvrir) return;
        ListeDuZoom.IsOpen = ouvrir;
    }

    private void SurEntreeChoisie(object? sender, RoutedEventArgs e)
    {
        if (_vm is null || (sender as Button)?.Content is not string entree) return;

        _vm.SaisirLeZoom(entree);
        Deplier(false);
        RendreLaPlanche();
    }

    /// <summary>
    /// Les flèches parcourent la liste : le champ montre l'entrée voisine, que la liste marque,
    /// et Entrée la prend. D'un zoom qui n'y est pas — « 86 % » —, la flèche du bas mène au
    /// premier pourcentage plus petit, celle du haut au premier plus grand, comme si la liste
    /// l'avait porté à sa place.
    /// </summary>
    private void Parcourir(int pas)
    {
        if (_vm is null) return;

        var entrees = _vm.ZoomsProposes;
        var texte = SaisieDuZoom.Text?.Trim() ?? "";
        var ici = -1;
        for (var i = 0; i < entrees.Count; i++)
        {
            if (string.Equals(entrees[i], texte, StringComparison.CurrentCultureIgnoreCase)) ici = i;
        }

        int suivant;
        if (ici >= 0)
        {
            suivant = Math.Clamp(ici + pas, 0, entrees.Count - 1);
        }
        else if (MainWindowViewModel.LirePourcentage(texte, out var pourcent))
        {
            // Les pourcentages vont décroissant : en bas, le premier plus petit ; en haut, le
            // dernier plus grand.
            var places = Enumerable.Range(0, entrees.Count)
                .Where(i => MainWindowViewModel.LirePourcentage(entrees[i], out var valeur) &&
                            (pas > 0 ? valeur < pourcent : valeur > pourcent))
                .ToList();
            if (places.Count == 0) return;
            suivant = pas > 0 ? places[0] : places[^1];
        }
        else
        {
            suivant = pas > 0 ? 0 : entrees.Count - 1;
        }

        SaisieDuZoom.Text = entrees[suivant];
        SaisieDuZoom.SelectAll();
        Deplier(true);
    }

    /// <summary>Une entrée qui paraît — la liste se déroule — se marque si le champ la montre.</summary>
    private void SurEntreeMontree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Button entree) Marquer(entree);
    }

    private void MarquerLaCourante()
    {
        foreach (var entree in EntreesDuZoom.GetVisualDescendants().OfType<Button>()) Marquer(entree);
    }

    private void Marquer(Button entree) =>
        entree.Classes.Set("courante",
            string.Equals(entree.Content as string, SaisieDuZoom.Text?.Trim(), StringComparison.CurrentCultureIgnoreCase));
}
