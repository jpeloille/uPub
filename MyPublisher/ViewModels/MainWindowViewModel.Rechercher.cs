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
/// trouvé (décision du propriétaire, 31 octobre 2026). Trouvé dans un groupe, il sélectionne le groupe et
/// ouvre la saisie du bloc qui le porte, comme dans un bloc de la page (poste N26) ; dans une case, il
/// sélectionne le tableau et ouvre la saisie de la case, depuis le poste N27, qui tape dans les cases
/// sur la page.
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

        /// <summary>
        /// Un bloc de texte, posé sur la page ou membre d'un groupe, ou une case : on y entre en
        /// saisie, le groupe restant la sélection depuis le poste N26, le tableau depuis le poste N27.
        /// </summary>
        public bool EnSaisie => Bloc is not null || Case is not null;

        /// <summary>La plage [<paramref name="debut"/>, <paramref name="fin"/>) de ce texte, sélectionnée en saisie, le curseur à sa fin.</summary>
        public SelectionDuTexte Selection(int debut, int fin) => new(Bloc ?? (Frame)Tableau!, Case, debut, fin, fin);

        /// <summary>Cette cible est-elle le texte de la sélection du texte en saisie ?</summary>
        public bool Porte(SelectionDuTexte selection) =>
            selection.Case is { } case_ ? ReferenceEquals(Case, case_) : Case is null && ReferenceEquals(Bloc, selection.Bloc);
    }

    /// <summary>Une occurrence trouvée : le texte où elle est, et sa plage.</summary>
    private sealed record Occurrence(Cible Cible, Plage Plage);

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
    /// ou de là où l'on est : la sélection du texte en saisie, l'occurrence montrée dans une case,
    /// le début du bloc sélectionné, ou celui de la page courante. Au bout du
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

        Status = Introuvable;
        return false;
    }

    /// <summary>D'où la recherche part : le texte, et la position dans ce texte.</summary>
    private (int Rang, int Depuis) Depart(List<Cible> cibles)
    {
        if (SelectionDuTexte is { } selection)
        {
            var rang = cibles.FindIndex(cible => cible.EnSaisie && cible.Porte(selection));
            if (rang >= 0) return (rang, selection.Fin);
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
    /// Montre l'occurrence : sa page devient la page courante, et son bloc la sélection. La planche
    /// ouvre la saisie, la plage sélectionnée — dans un groupe aussi, le groupe sélectionné, depuis
    /// le poste N26 ; dans une case, le tableau sélectionné, depuis le poste N27 —, et la barre d'état
    /// nomme la case.
    /// </summary>
    private void Montrer(Occurrence occurrence, bool reprise)
    {
        var cible = occurrence.Cible;
        if (!ReferenceEquals(CurrentPage, cible.Page)) CurrentPage = cible.Page;
        if (Selection.Count != 1 || !ReferenceEquals(Selection[0], cible.Racine)) SelectOnly(cible.Racine);

        var numero = Document.Pages.IndexOf(cible.Page) + 1;
        var trouve = $"« {TexteCherche} » trouvé page {numero}";
        var repris = reprise ? " — la recherche a repris au début du document" : "";

        var fin = occurrence.Plage.Start + occurrence.Plage.Length;
        TexteTrouve?.Invoke(this, cible.Selection(occurrence.Plage.Start, fin));
        Status = cible.Case is null
            ? $"{trouve}{repris}."
            : $"{trouve}, dans la case {cible.Case.Label} du tableau « {cible.Tableau?.Name} »{repris}.";
    }

    /// <summary>
    /// L'occurrence que <i>Remplacer</i> remplace : la sélection du texte en saisie — d'un bloc ou
    /// d'une case —, si elle en est une.
    /// </summary>
    private Occurrence? OccurrenceSelectionnee()
    {
        if (SelectionDuTexte is not { EstVide: false } selection) return null;

        var cible = Cibles().Find(c => c.EnSaisie && c.Porte(selection));
        return cible is not null && Recherche.EstUneOccurrence(cible.Texte, selection.Plage, CriteresDeRecherche)
            ? new Occurrence(cible, selection.Plage)
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
