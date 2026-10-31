using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Rechercher et remplacer (poste N20 ; fiche 23, e23 ; décision 6 de la phase N). Le livre ne le
/// montre pas ; Publisher le fait par <i>Edition ▸ Rechercher</i> et <i>Remplacer</i>, sur
/// <c>Ctrl+F</c>, <c>F3</c>, <c>Maj+F4</c> et <c>Ctrl+H</c> dans l'aide en français (fiche 24). Un
/// fichier à lui, comme la règle 10 de la phase I le veut.
///
/// La barre de recherche (<c>Views/BarreDeRecherche.axaml</c>) est posée sur la planche, en haut à
/// droite ; elle se lie ici. Le parcours va de bloc en bloc dans l'ordre des pages, puis, sur la
/// page, de haut en bas et de gauche à droite : les blocs de texte visibles et sans champ, les
/// cases des tableaux, et ce que portent les groupes. Arrivé au bout du document, il reprend au
/// début, et le dit.
///
/// Le texte trouvé dans un bloc de texte est sélectionné en saisie (N12, N15) : le modèle de vue
/// prend le bloc, change de page s'il le faut, et demande à la planche d'ouvrir sa saisie la plage
/// sélectionnée (<see cref="TexteTrouve"/>), dessinée sur la composition (N21). Cherché depuis la
/// barre, le focus y reste, comme dans le volet de Publisher, et <c>Échap</c> le rend au texte
/// trouvé (décision du propriétaire, 31 octobre 2026). Trouvé dans un groupe, il sélectionne le groupe et le
/// dit, jusqu'à N26, qui entrera dans les groupes ; dans une case, il sélectionne le tableau et
/// nomme la case, qui se règle au dock — la page n'a pas de saisie de case.
///
/// Le remplacement passe par le noyau (<see cref="Recherche.Remplacer(TextFrame, Plage, string)"/>,
/// <see cref="Recalage"/>), sans passer par l'écran : le texte remplacé prend le style de son
/// premier caractère, la règle de Q1. <i>Remplacer</i> fait un pas ; <i>Remplacer tout</i> en fait
/// un seul, pour tout le document.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>La barre de recherche est ouverte.</summary>
    [ObservableProperty]
    private bool _rechercheVisible;

    /// <summary>Le texte cherché.</summary>
    [ObservableProperty]
    private string _texteCherche = "";

    /// <summary>Le texte qui remplace ce qu'on trouve ; vide, le remplacement efface.</summary>
    [ObservableProperty]
    private string _texteDeRemplacement = "";

    /// <summary><i>Respecter la casse</i> : « Bulletin » ne trouve plus « bulletin ».</summary>
    [ObservableProperty]
    private bool _respecterLaCasse;

    /// <summary><i>Mot entier</i> : « bulle » ne trouve plus le début de « bulletin ».</summary>
    [ObservableProperty]
    private bool _motEntier;

    /// <summary>
    /// La barre de recherche est demandée — <c>Ctrl+F</c>, <c>Ctrl+H</c>, <i>Edition ▸
    /// Rechercher…</i> — et doit prendre le focus : dans le texte cherché, ou, vrai, dans le texte
    /// de remplacement. La barre l'écoute.
    /// </summary>
    public event EventHandler<bool>? RechercheDemandee;

    /// <summary>
    /// Un texte est trouvé dans un bloc de texte de la page courante, que le modèle de vue a
    /// sélectionné seul : la planche ouvre sa saisie, la plage trouvée sélectionnée, le curseur à
    /// sa fin. La planche l'écoute (<c>PageCanvas.Saisie.cs</c>).
    /// </summary>
    public event EventHandler<SelectionDuTexte>? TexteTrouve;

    /// <summary>
    /// La barre se ferme sur une occurrence sélectionnée en saisie : la planche rend le focus au
    /// champ du bloc, sur la sélection que le modèle de vue garde (décision du propriétaire, 31
    /// octobre 2026).
    /// </summary>
    public event EventHandler<SelectionDuTexte>? SaisieRendue;

    /// <summary>Ce qu'on cherche, tel que la barre le dit.</summary>
    public CriteresDeRecherche CriteresDeRecherche => new(TexteCherche ?? "", RespecterLaCasse, MotEntier);

    /// <summary>
    /// Un texte où l'on cherche : celui d'un bloc de texte, ou d'une case. <see cref="Racine"/> est
    /// le bloc de la page qui le porte — le bloc lui-même, son tableau, ou le groupe où il est —,
    /// celui qu'on sélectionne quand on y trouve quelque chose.
    /// </summary>
    private sealed record Cible(Page Page, Frame Racine, TextFrame? Bloc, TableFrame? Tableau, TableCell? Case)
    {
        public string Texte => Case?.Text ?? Bloc?.Text ?? "";

        /// <summary>Un bloc de texte posé sur la page, hors de tout groupe : on y entre en saisie.</summary>
        public bool EnSaisie => Bloc is not null && ReferenceEquals(Bloc, Racine);
    }

    /// <summary>Une occurrence trouvée : le texte où elle est, et sa plage.</summary>
    private sealed record Occurrence(Cible Cible, Plage Plage);

    /// <summary>
    /// La dernière occurrence montrée hors de la saisie — dans un groupe ou une case —, d'où la
    /// recherche suivante repart et que <i>Remplacer</i> remplace, tant que son bloc reste
    /// sélectionné. Dans un bloc de texte, c'est la sélection du texte (N15) qui le dit.
    /// </summary>
    private Occurrence? _occurrenceMontree;

    /// <summary>
    /// Les textes du document, dans l'ordre du parcours : les pages dans leur ordre ; sur la page,
    /// les blocs visibles de haut en bas, puis de gauche à droite, à leur empreinte ; dans un
    /// tableau, ses cases ligne après ligne ; dans un groupe, ses blocs visibles de même.
    /// </summary>
    private List<Cible> Cibles()
    {
        var cibles = new List<Cible>();
        foreach (var page in Document.Pages)
        {
            foreach (var bloc in page.Frames.Where(bloc => bloc.Visible)
                         .OrderBy(bloc => bloc.Extent.Top).ThenBy(bloc => bloc.Extent.Left))
            {
                AjouterLesCibles(cibles, page, bloc, bloc);
            }
        }

        return cibles;
    }

    private static void AjouterLesCibles(List<Cible> cibles, Page page, Frame racine, Frame bloc)
    {
        switch (bloc)
        {
            case TextFrame { HasField: false } texte:
                cibles.Add(new Cible(page, racine, texte, null, null));
                break;
            case TableFrame tableau:
                foreach (var case_ in tableau.Cells.OrderBy(c => c.Row).ThenBy(c => c.Column))
                {
                    cibles.Add(new Cible(page, racine, null, tableau, case_));
                }

                break;
            case GroupFrame groupe:
                foreach (var enfant in groupe.Children.Where(enfant => enfant.Visible)
                             .OrderBy(enfant => enfant.Extent.Top).ThenBy(enfant => enfant.Extent.Left))
                {
                    AjouterLesCibles(cibles, page, racine, enfant);
                }

                break;
        }
    }

    // ------------------------------------------------------------------ les commandes

    /// <summary><c>Ctrl+F</c>, <i>Edition ▸ Rechercher…</i> : la barre, le focus au texte cherché.</summary>
    [RelayCommand]
    private void OuvrirLaRecherche() => DemanderLaRecherche(remplacer: false);

    /// <summary>
    /// <c>Ctrl+H</c>, <i>Edition ▸ Remplacer…</i> : la barre, le focus au texte de remplacement —
    /// au texte cherché tant qu'il est vide.
    /// </summary>
    [RelayCommand]
    private void OuvrirLeRemplacement() => DemanderLaRecherche(remplacer: !string.IsNullOrEmpty(TexteCherche));

    private void DemanderLaRecherche(bool remplacer)
    {
        // La barre repasse à la recherche si F7 l'avait mise à l'orthographe (N25).
        ModeOrthographe = false;
        RechercheVisible = true;
        RechercheDemandee?.Invoke(this, remplacer);
    }

    /// <summary>
    /// <c>Échap</c> dans la barre, et sa croix : la barre se ferme. Sur une occurrence sélectionnée
    /// en saisie, la planche rend le focus au champ du bloc, la sélection gardée, et l'on tape
    /// par-dessus (<see cref="SaisieRendue"/>) ; sinon, la barre rend la planche.
    /// </summary>
    [RelayCommand]
    private void FermerLaRecherche()
    {
        RechercheVisible = false;
        if (OccurrenceSelectionnee() is { Cible.EnSaisie: true } && SelectionDuTexte is { } selection)
        {
            SaisieRendue?.Invoke(this, selection);
        }
    }

    /// <summary>
    /// <c>F3</c>, <c>Maj+F4</c>, <i>Suivant</i>, <c>Entrée</c> dans la barre : l'occurrence suivante,
    /// sélectionnée. Sans rien à chercher, la barre s'ouvre, comme à <c>Ctrl+F</c>.
    /// </summary>
    [RelayCommand]
    private void RechercherLeSuivant()
    {
        if (!PretARechercher()) return;
        Suivante(null);
    }

    /// <summary>
    /// <i>Remplacer</i> : la sélection, si c'est une occurrence de ce qu'on cherche, est remplacée,
    /// en un pas ; puis l'occurrence suivante est sélectionnée. Une sélection qui n'en est pas une
    /// n'est pas remplacée : la recherche avance seulement, comme dans Publisher.
    /// </summary>
    [RelayCommand]
    private void Remplacer()
    {
        if (!PretARechercher()) return;

        if (OccurrenceSelectionnee() is not { } occurrence)
        {
            Suivante(null);
            return;
        }

        var par = TexteDeRemplacement ?? "";
        RemplacerUneOccurrence(occurrence, par);
        Suivante(occurrence with { Plage = new Plage(occurrence.Plage.Start, par.Length) });
    }

    /// <summary>
    /// <i>Remplacer tout</i> : chaque occurrence du document, en un seul pas. Les occurrences de
    /// chaque texte sont remplacées de la dernière à la première : celles qui restent ne bougent
    /// pas, et un remplacement qui contient ce qu'on cherche n'est pas repris.
    /// </summary>
    [RelayCommand]
    private void RemplacerTout()
    {
        if (!PretARechercher()) return;

        var criteres = CriteresDeRecherche;
        var par = TexteDeRemplacement ?? "";
        var cibles = Cibles();
        var nombre = 0;

        UnGesteUnPas(() =>
        {
            foreach (var cible in cibles)
            {
                foreach (var plage in Recherche.AReboursDe(cible.Texte, criteres))
                {
                    RemplacerLOccurrence(new Occurrence(cible, plage), par);
                    nombre++;
                }
            }
        });

        _occurrenceMontree = null;
        Status = nombre switch
        {
            0 => Introuvable,
            1 => $"1 remplacement : « {criteres.Cherche} » par « {par} ».",
            _ => $"{nombre} remplacements : « {criteres.Cherche} » par « {par} », en un pas.",
        };
    }

    private string Introuvable => $"« {TexteCherche} » est introuvable.";

    /// <summary>Il y a de quoi chercher ; sinon, la barre s'ouvre et le dit.</summary>
    private bool PretARechercher()
    {
        if (RechercheVisible && !ModeOrthographe && !string.IsNullOrEmpty(TexteCherche)) return true;

        DemanderLaRecherche(remplacer: false);
        if (string.IsNullOrEmpty(TexteCherche)) Status = "Tapez le texte à rechercher.";
        return false;
    }

    // ------------------------------------------------------------------ le parcours

    /// <summary>
    /// L'occurrence suivante, à partir de <paramref name="apres"/> — la fin d'un remplacement —,
    /// ou de là où l'on est : la sélection du texte en saisie, l'occurrence montrée dans un groupe
    /// ou une case, le début du bloc sélectionné, ou celui de la page courante. Au bout du
    /// document, la recherche reprend au début ; elle revient au texte d'où elle partait, et y
    /// retrouve ce qui précédait le départ.
    /// </summary>
    private bool Suivante(Occurrence? apres)
    {
        var criteres = CriteresDeRecherche;
        var cibles = Cibles();
        var (depart, depuis) = apres is null ? Depart(cibles) : (cibles.IndexOf(apres.Cible), apres.Plage.Start + apres.Plage.Length);
        if (depart < 0) (depart, depuis) = (0, 0);

        for (var pas = 0; pas <= cibles.Count; pas++)
        {
            var rang = (depart + pas) % cibles.Count;
            if (Recherche.Suivante(cibles[rang].Texte, criteres, pas == 0 ? depuis : 0) is { } plage)
            {
                Montrer(new Occurrence(cibles[rang], plage), reprise: depart + pas >= cibles.Count);
                return true;
            }
        }

        _occurrenceMontree = null;
        Status = Introuvable;
        return false;
    }

    /// <summary>D'où la recherche part : le texte, et la position dans ce texte.</summary>
    private (int Rang, int Depuis) Depart(List<Cible> cibles)
    {
        if (SelectionDuTexte is { Case: null, Bloc: TextFrame enSaisie } selection)
        {
            var rang = cibles.FindIndex(cible => cible.EnSaisie && ReferenceEquals(cible.Bloc, enSaisie));
            if (rang >= 0) return (rang, selection.Fin);
        }

        if (_occurrenceMontree is { } montree && SingleSelected is { } seul && ReferenceEquals(seul, montree.Cible.Racine))
        {
            var rang = cibles.IndexOf(montree.Cible);
            if (rang >= 0) return (rang, montree.Plage.Start + montree.Plage.Length);
        }

        if (SingleSelected is { } bloc)
        {
            var rang = cibles.FindIndex(cible => ReferenceEquals(cible.Racine, bloc));
            if (rang >= 0) return (rang, 0);
        }

        var page = CurrentPage is null ? 0 : Document.Pages.IndexOf(CurrentPage);
        var premier = cibles.FindIndex(cible => Document.Pages.IndexOf(cible.Page) >= page);
        return (Math.Max(0, premier), 0);
    }

    /// <summary>
    /// Montre l'occurrence : sa page devient la page courante, et son bloc la sélection. Dans un
    /// bloc de texte, la planche ouvre la saisie, la plage sélectionnée ; dans un groupe ou une
    /// case, la barre d'état dit où elle est.
    /// </summary>
    private void Montrer(Occurrence occurrence, bool reprise)
    {
        var cible = occurrence.Cible;
        if (!ReferenceEquals(CurrentPage, cible.Page)) CurrentPage = cible.Page;
        if (Selection.Count != 1 || !ReferenceEquals(Selection[0], cible.Racine)) SelectOnly(cible.Racine);

        var numero = Document.Pages.IndexOf(cible.Page) + 1;
        var trouve = $"« {TexteCherche} » trouvé page {numero}";
        var repris = reprise ? " — la recherche a repris au début du document" : "";

        if (cible.EnSaisie)
        {
            _occurrenceMontree = null;
            var fin = occurrence.Plage.Start + occurrence.Plage.Length;
            TexteTrouve?.Invoke(this, new SelectionDuTexte(cible.Bloc!, null, occurrence.Plage.Start, fin, fin));
            Status = $"{trouve}{repris}.";
            return;
        }

        _occurrenceMontree = occurrence;
        Status = cible.Racine is GroupFrame groupe
            ? $"{trouve}, dans le groupe « {groupe.Name} »{repris} : dissociez-le pour entrer dans son texte."
            : $"{trouve}, dans la case {cible.Case?.Label} du tableau « {cible.Tableau?.Name} »{repris} : la case se règle au dock.";
    }

    /// <summary>
    /// L'occurrence que <i>Remplacer</i> remplace : la sélection du texte en saisie, si elle en est
    /// une ; sinon l'occurrence montrée dans un groupe ou une case, si son bloc est toujours la
    /// sélection et que son texte n'a pas changé sous elle.
    /// </summary>
    private Occurrence? OccurrenceSelectionnee()
    {
        var criteres = CriteresDeRecherche;
        var cibles = Cibles();

        if (SelectionDuTexte is { Case: null, Bloc: TextFrame enSaisie, EstVide: false } selection)
        {
            var cible = cibles.Find(c => c.EnSaisie && ReferenceEquals(c.Bloc, enSaisie));
            return cible is not null && Recherche.EstUneOccurrence(cible.Texte, selection.Plage, criteres)
                ? new Occurrence(cible, selection.Plage)
                : null;
        }

        return _occurrenceMontree is { } montree && SingleSelected is { } seul &&
               ReferenceEquals(seul, montree.Cible.Racine) && cibles.Contains(montree.Cible) &&
               Recherche.EstUneOccurrence(montree.Cible.Texte, montree.Plage, criteres)
            ? montree
            : null;
    }

    /// <summary><i>Remplacer</i>, une occurrence : son propre pas, où que soit le focus.</summary>
    private void RemplacerUneOccurrence(Occurrence occurrence, string par) =>
        UnGesteUnPas(() => RemplacerLOccurrence(occurrence, par));

    /// <summary>Le remplacement, par le noyau, dans le bloc ou dans la case : sans pas à lui.</summary>
    private static void RemplacerLOccurrence(Occurrence occurrence, string par)
    {
        if (occurrence.Cible.Case is { } case_) Recherche.Remplacer(case_, occurrence.Plage, par);
        else if (occurrence.Cible.Bloc is { } bloc) Recherche.Remplacer(bloc, occurrence.Plage, par);
    }
}
