using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// « Insérer des pages » (A8, livré par le poste N18) — le dialogue que la fenêtre ouvre à
/// <i>Page ▸ Insérer des pages…</i> et à <c>Ctrl+Maj+N</c>. Il rend une
/// <see cref="DemandeDInsertion"/>, ou rien à Annuler ; c'est le modèle de vue qui insère, en un
/// pas (<see cref="MainWindowViewModel.InsererDesPages(DemandeDInsertion)"/>).
/// </summary>
public partial class InsererDesPages : Window
{
    public InsererDesPages()
    {
        InitializeComponent();
    }

    /// <param name="nombreDePages">Le nombre de pages du document : la page à copier s'y choisit.</param>
    /// <param name="pageCourante">Le numéro de la page courante, proposée à la copie.</param>
    public InsererDesPages(int nombreDePages, int pageCourante) : this()
    {
        var page = this.FindControl<ChampNumerique>("PageACopier")!;
        page.Maximum = Math.Max(1, nombreDePages);
        page.Value = Math.Clamp(pageCourante, 1, Math.Max(1, nombreDePages));
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>Ouvre le dialogue au-dessus de <paramref name="proprietaire"/> et attend la demande.</summary>
    public static Task<DemandeDInsertion?> Demander(Window proprietaire, int nombreDePages, int pageCourante) =>
        new InsererDesPages(nombreDePages, pageCourante).ShowDialog<DemandeDInsertion?>(proprietaire);

    /// <summary>
    /// OK : ce qui attend dans un champ part d'abord — un nombre tapé puis Entrée, qu'OK prend
    /// sans que le champ ait perdu le focus.
    /// </summary>
    private void OnOk(object? sender, RoutedEventArgs e)
    {
        var nombre = this.FindControl<ChampNumerique>("NombreDeNouvellesPages")!;
        var page = this.FindControl<ChampNumerique>("PageACopier")!;
        nombre.Valider();
        page.Valider();

        var contenu = this.FindControl<RadioButton>("DupliquerLesObjets")!.IsChecked == true ? ContenuDesPages.CopieDUnePage
            : this.FindControl<RadioButton>("UneZoneDeTexte")!.IsChecked == true ? ContenuDesPages.UneZoneDeTexte
            : ContenuDesPages.Vierges;

        Close(new DemandeDInsertion(
            (int)(nombre.Value ?? 1),
            this.FindControl<RadioButton>("AvantLaPage")!.IsChecked == true,
            contenu,
            (int)(page.Value ?? 1)));
    }

    private void OnAnnuler(object? sender, RoutedEventArgs e) => Close(null);
}
