using System;
using System.Collections.Generic;
using System.Linq;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>Une langue que le dock propose : son étiquette BCP 47, et son nom en français.</summary>
public sealed record LangueProposee(string Etiquette, string Nom)
{
    public override string ToString() => Nom;
}

/// <summary>
/// La langue du document au dock, onglet Document (poste N9). Dans son fichier partiel
/// (règle 10 de la phase I) ; le modèle de vue principal ne fait que l'annoncer quand le
/// document change, ou sa langue.
///
/// Le choix se fait en un pas d'annulation, comme le jeu de couleurs : le modèle de vue ouvre et
/// referme sa transaction lui-même, et deux langues choisies de suite font deux pas.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>La section repliable du dock, ouverte par défaut, comme ses voisines.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool _sectionLangueOuverte = true;

    /// <summary>
    /// Les langues proposées, la langue d'un document vierge en tête. Une courte liste et non
    /// toutes celles du monde : celles qu'un correcteur saura servir (N25). Les noms sont écrits
    /// ici, et non demandés à <c>CultureInfo</c>, qui les donnerait dans la langue de la machine
    /// — ou pas du tout, sous <c>LC_ALL=C</c>.
    /// </summary>
    public static IReadOnlyList<LangueProposee> LanguesConnues { get; } =
    [
        new(Langues.ParDefaut, "Français (France)"),
        new("fr-BE", "Français (Belgique)"),
        new("fr-CA", "Français (Canada)"),
        new("fr-CH", "Français (Suisse)"),
        new("en-US", "Anglais (États-Unis)"),
        new("en-GB", "Anglais (Royaume-Uni)"),
        new("de-DE", "Allemand (Allemagne)"),
        new("es-ES", "Espagnol (Espagne)"),
        new("it-IT", "Italien (Italie)"),
        new("nl-NL", "Néerlandais (Pays-Bas)"),
        new("pt-PT", "Portugais (Portugal)"),
        new("pt-BR", "Portugais (Brésil)"),
    ];

    /// <summary>
    /// Les langues de la liste du dock : les connues, et celle du document si elle n'en est
    /// pas — écrite telle quelle. Sans elle, la liste resterait sans sélection, et une liste
    /// sans sélection renvoie null à sa source.
    /// </summary>
    public IReadOnlyList<LangueProposee> LanguesProposees =>
        Connue(Document.Langue) is null ? [.. LanguesConnues, Inconnue(Document.Langue)] : LanguesConnues;

    /// <summary>
    /// La langue du document, telle que la liste la montre. La choisir l'écrit au document en
    /// un pas d'annulation ; null, ou la langue qu'il a déjà, ne fait rien.
    /// </summary>
    public LangueProposee? LangueDuDocument
    {
        get => Connue(Document.Langue) ?? Inconnue(Document.Langue);
        set
        {
            if (value is null || value.Etiquette == Document.Langue) return;

            BeginEdit();
            Document.Langue = value.Etiquette;
            CommitEdit();
            Status = $"Langue du document : {value.Nom}";
        }
    }

    /// <summary>Le document a changé de langue, ou on en a changé de document.</summary>
    private void AnnoncerLaLangue()
    {
        OnPropertyChanged(nameof(LanguesProposees));
        OnPropertyChanged(nameof(LangueDuDocument));
    }

    /// <summary>
    /// L'entrée de la liste pour une étiquette, la casse ignorée — BCP 47 ne la distingue pas.
    /// Une étiquette connue écrite autrement (« FR-fr ») est montrée comme connue, et le
    /// document la garde telle qu'elle est écrite tant qu'on n'en choisit pas une autre.
    /// </summary>
    private static LangueProposee? Connue(string etiquette) =>
        LanguesConnues.FirstOrDefault(l => string.Equals(l.Etiquette, etiquette, StringComparison.OrdinalIgnoreCase));

    private static LangueProposee Inconnue(string etiquette) => new(etiquette, etiquette);
}
