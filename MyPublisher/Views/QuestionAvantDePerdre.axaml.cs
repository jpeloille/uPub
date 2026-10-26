using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MyPublisher.Views;

/// <summary>Ce qu'on répond à la question d'avant la perte (poste N11).</summary>
public enum ReponseAvantDePerdre
{
    /// <summary>Enregistrer le document, puis poursuivre.</summary>
    Enregistrer,

    /// <summary>Poursuivre sans enregistrer : les modifications sont perdues.</summary>
    NePasEnregistrer,

    /// <summary>Ne rien faire : le document reste tel qu'il est, ouvert.</summary>
    Annuler,
}

/// <summary>
/// « Enregistrer les modifications apportées à « nom » ? » — le dialogue que la fenêtre montre
/// avant de perdre des modifications, quand <c>App.axaml.cs</c> l'y a branché
/// (<see cref="MainWindow.AvantDePerdre"/>). Il rend la réponse ; c'est la fenêtre qui
/// enregistre, ou non, puis poursuit.
/// </summary>
public partial class QuestionAvantDePerdre : Window
{
    public QuestionAvantDePerdre()
    {
        InitializeComponent();
    }

    /// <param name="nom">Le nom du document, tel que la barre de titre le montre.</param>
    public QuestionAvantDePerdre(string nom) : this()
    {
        // Les guillemets et le point d'interrogation tiennent au nom par des espaces insécables.
        this.FindControl<TextBlock>("Question")!.Text = $"Enregistrer les modifications apportées à «\u00A0{nom}\u00A0»\u00A0?";
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Pose la question au-dessus de <paramref name="proprietaire"/> et attend la réponse. Le
    /// dialogue refermé par sa barre de titre répond Annuler.
    /// </summary>
    public static async Task<ReponseAvantDePerdre> Poser(Window proprietaire, string nom) =>
        await new QuestionAvantDePerdre(nom).ShowDialog<ReponseAvantDePerdre?>(proprietaire)
        ?? ReponseAvantDePerdre.Annuler;

    private void OnEnregistrer(object? sender, RoutedEventArgs e) => Close(ReponseAvantDePerdre.Enregistrer);

    private void OnNePasEnregistrer(object? sender, RoutedEventArgs e) => Close(ReponseAvantDePerdre.NePasEnregistrer);

    private void OnAnnuler(object? sender, RoutedEventArgs e) => Close(ReponseAvantDePerdre.Annuler);
}
