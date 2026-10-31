using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>Ce que la liste « Puces et numéros » du dock propose (poste N23).</summary>
public enum ChoixDeListe { Aucune, Puces, Chiffres, Lettres, Romains }

/// <summary>Une entrée de la liste « Puces et numéros » : le choix, et ce qu'elle en montre.</summary>
public sealed record OptionDeListe(ChoixDeListe Choix, string Libelle);

/// <summary>
/// La liste des paragraphes que règle le dock (poste N23 ; fiche 23, b12) : le pendant, pour les
/// puces et les numéros, de <see cref="ParagrapheVise"/>, dont elle prend la cible — hors saisie,
/// le bloc de texte seul ; en saisie, les paragraphes que touche la sélection du texte, ou celui du
/// curseur — et les avis. Elle écrit par lui (<see cref="ParagrapheVise.Ecrire"/>), dans la
/// transaction du champ du dock qui a le focus.
///
/// <b>Ce qu'elle montre</b> : la sorte de liste que les paragraphes visés ont en commun — aucune,
/// des puces, ou des numéros d'une forme —, nulle quand ils diffèrent ; la puce qu'ils ont en
/// commun ; le numéro du premier paragraphe visé.
///
/// <b>Ce qu'elle pose</b> (<see cref="Listes"/>) : la liste choisie, sans premier numéro, et le
/// retrait suspendu d'Office à un paragraphe qui entre dans une liste sans aucun retrait ; « Aucune »
/// rend ce retrait-là. Le premier numéro fait repartir la suite au premier paragraphe visé
/// (<see cref="Listes.Recommencer"/>). Hors saisie, sur le bloc, ses écarts laissés (R6).
/// </summary>
public sealed class ListeVisee : ObservableObject
{
    private static readonly string[] Reglages =
        [nameof(Option), nameof(Puce), nameof(PremierNumero), nameof(EnPuces), nameof(Numerotee)];

    private readonly ParagrapheVise _vise;

    internal ListeVisee(ParagrapheVise vise)
    {
        _vise = vise;
        // Le ParagrapheVise avise de chacun de ses réglages, « EnSaisie » le dernier : une fois
        // par changement — le bloc, son texte, ses écarts, sa liste, la sélection du texte.
        _vise.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ParagrapheVise.EnSaisie)) Notifier();
        };
    }

    /// <summary>Les listes des paragraphes visés, telles que la composition les lit ; vide sans bloc.</summary>
    private IReadOnlyList<ListeDeParagraphe?> Visees()
    {
        if (_vise.Bloc is not { } bloc) return [];
        if (_vise.Paragraphes is not { } paragraphes) return [Listes.Effective(bloc.Liste)];

        var effectifs = Effectifs(bloc);
        var debut = Math.Clamp(paragraphes.Start, 0, effectifs.Count - 1);
        var fin = Math.Clamp(paragraphes.Start + Math.Max(1, paragraphes.Length), debut + 1, effectifs.Count);
        return effectifs.Skip(debut).Take(fin - debut).Select(e => e.Liste).ToList();
    }

    private static IReadOnlyList<ParagrapheEffectif> Effectifs(TextFrame bloc) =>
        ParagrapheEffectif.Resoudre(Recalage.NombreDeParagraphes(bloc.Text ?? ""), bloc.Paragraphs, bloc.Paragraph);

    private static ChoixDeListe Choix(ListeDeParagraphe? liste) => Listes.Effective(liste) switch
    {
        { Sorte: SorteDeListe.Puces } => ChoixDeListe.Puces,
        { Sorte: SorteDeListe.Numeros, Format: FormatDeNumero.Lettres } => ChoixDeListe.Lettres,
        { Sorte: SorteDeListe.Numeros, Format: FormatDeNumero.Romains } => ChoixDeListe.Romains,
        { Sorte: SorteDeListe.Numeros } => ChoixDeListe.Chiffres,
        _ => ChoixDeListe.Aucune,
    };

    /// <summary>Le choix commun aux paragraphes visés ; nul quand ils diffèrent, ou sans bloc.</summary>
    private ChoixDeListe? ChoixCommun()
    {
        var choix = Visees().Select(Choix).Distinct().ToList();
        return choix.Count == 1 ? choix[0] : null;
    }

    /// <summary>
    /// L'entrée de « Puces et numéros » : aucune, des puces, « 1. 2. 3. », « a) b) c) »,
    /// « i. ii. iii. » ; nulle quand les paragraphes visés diffèrent. La choisir pose la liste,
    /// sans premier numéro, et laisse tels quels les paragraphes qui l'ont déjà.
    /// </summary>
    public OptionDeListe? Option
    {
        get => ChoixCommun() is { } choix ? MainWindowViewModel.OptionsDeListe.Single(o => o.Choix == choix) : null;
        set
        {
            if (value is null || value.Choix == ChoixCommun()) return;

            var liste = value.Choix switch
            {
                ChoixDeListe.Puces => ListeDeParagraphe.DesPuces(Puce ?? ListeDeParagraphe.PuceParDefaut),
                ChoixDeListe.Chiffres => ListeDeParagraphe.DesNumeros(FormatDeNumero.Chiffres),
                ChoixDeListe.Lettres => ListeDeParagraphe.DesNumeros(FormatDeNumero.Lettres),
                ChoixDeListe.Romains => ListeDeParagraphe.DesNumeros(FormatDeNumero.Romains),
                _ => ListeDeParagraphe.Aucune,
            };
            Poser(liste);
        }
    }

    /// <summary>La puce que les paragraphes visés ont en commun ; nulle hors puces, ou quand elles diffèrent.</summary>
    public string? Puce
    {
        get
        {
            var puces = Visees().Select(l => l is { Sorte: SorteDeListe.Puces } p ? p.Puce : null).Distinct().ToList();
            return puces is [{ } puce] ? puce : null;
        }
        set
        {
            if (string.IsNullOrEmpty(value) || !EnPuces) return;
            // Un caractère, paire de substitution ou accent combinant compris : le dernier tapé, si
            // le champ porte encore l'ancien devant lui.
            var caractere = value[StringInfo.ParseCombiningCharacters(value)[^1]..];
            if (caractere == Puce) return;
            Poser(ListeDeParagraphe.DesPuces(caractere));
        }
    }

    /// <summary>
    /// Le numéro du premier paragraphe visé, s'il est numéroté — hors saisie, le premier numéro du
    /// bloc. Le changer fait repartir la suite à ce paragraphe, et la suite avec lui.
    /// </summary>
    public int? PremierNumero
    {
        get
        {
            if (_vise.Bloc is not { } bloc) return null;
            if (_vise.Paragraphes is not { } paragraphes)
            {
                return Listes.Effective(bloc.Liste) is { Sorte: SorteDeListe.Numeros } liste ? liste.Premier ?? 1 : null;
            }

            var numeros = Listes.Numeros(Effectifs(bloc));
            return numeros[Math.Clamp(paragraphes.Start, 0, numeros.Count - 1)];
        }
        set
        {
            if (value is not { } premier || premier == PremierNumero) return;

            _vise.Ecrire(bloc =>
            {
                if (_vise.Paragraphes is { } paragraphes)
                {
                    return Listes.Recommencer(bloc.Text ?? "", bloc.Paragraphs, bloc.Paragraph, paragraphes.Start, premier);
                }

                if (Listes.Effective(bloc.Liste) is not { Sorte: SorteDeListe.Numeros } liste) return false;
                bloc.Liste = liste with { Premier = premier };
                return true;
            });
        }
    }

    /// <summary>Les paragraphes visés sont tous à puces : le champ de la puce se montre.</summary>
    public bool EnPuces => ChoixCommun() == ChoixDeListe.Puces;

    /// <summary>Les paragraphes visés sont tous numérotés d'une même forme : le premier numéro se montre.</summary>
    public bool Numerotee => ChoixCommun() is ChoixDeListe.Chiffres or ChoixDeListe.Lettres or ChoixDeListe.Romains;

    /// <summary>Pose une liste sur les paragraphes visés, ou sur le bloc hors saisie.</summary>
    internal bool Poser(ListeDeParagraphe liste) => _vise.Ecrire(bloc =>
        _vise.Paragraphes is { } paragraphes
            ? Listes.Poser(bloc.Text ?? "", bloc.Paragraphs, bloc.Paragraph, paragraphes, liste)
            : Listes.PoserSurLeBloc(bloc, liste));

    private void Notifier()
    {
        foreach (var reglage in Reglages) OnPropertyChanged(reglage);
    }
}

/// <summary>
/// Ce que le poste N23 — puces et numérotation — ajoute au modèle de vue. Un fichier à lui, comme
/// la règle 10 de la phase I le veut.
/// </summary>
public partial class MainWindowViewModel
{
    private ListeVisee? _listeVisee;

    /// <summary>Les entrées de « Puces et numéros », au dock.</summary>
    public static IReadOnlyList<OptionDeListe> OptionsDeListe { get; } =
    [
        new(ChoixDeListe.Aucune, "Aucune"),
        new(ChoixDeListe.Puces, "• Puces"),
        new(ChoixDeListe.Chiffres, "1. 2. 3."),
        new(ChoixDeListe.Lettres, "a) b) c)"),
        new(ChoixDeListe.Romains, "i. ii. iii."),
    ];

    /// <summary>La liste des paragraphes que règle le dock : ceux du ParagrapheVise.</summary>
    public ListeVisee ListeVisee => _listeVisee ??= new ListeVisee(ParagrapheVise);

    /// <summary>
    /// <i>Texte ▸ Puces et numéros…</i> — <i>Format ▸ Bullets and Numbering…</i> de Publisher
    /// (p. 24) : la liste « Puces et numéros » de la section Paragraphe du dock (N18), qui n'a pas
    /// de bouton dans la maquette. Grisée sans bloc de texte, comme Paragraphe….
    /// </summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void PucesEtNumeros() => AllerAuDock(
        new DockDemande(DestinationDuReglage.Proprietes, "ContenuParagraphe", "ListeDuParagraphe"),
        () => SectionParagrapheOuverte = true);
}
