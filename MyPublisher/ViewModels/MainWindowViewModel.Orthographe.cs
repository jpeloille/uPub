using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;
using MyPublisher.Services;

namespace MyPublisher.ViewModels;

/// <summary>
/// La vérification orthographique (poste N25 ; fiche 23, a26 ; décision 6 de la phase N). Un
/// fichier à lui, comme la règle 10 de la phase I le veut. Publisher souligne les fautes pendant la
/// frappe, les propose au clic droit et les parcourt par <c>F7</c> (<i>Outils ▸ Orthographe</i>) ;
/// le service du noyau, <see cref="Services.Orthographe"/>, dit lesquelles.
///
///   - <b>Le service</b> n'est donné que par l'application (<c>App.axaml.cs</c>), sur les
///     dictionnaires du système : un modèle de vue construit ailleurs — un test, le banc — n'en a
///     pas, et ne souligne rien, comme il ne lit pas les préférences de la personne (N11). Les
///     tests du poste lui en donnent un sur le dictionnaire de <c>Scripts/fixtures/orthographe/</c>.
///   - <b>« Vérifier pendant la frappe »</b> (<see cref="VerifierPendantLaFrappe"/>), une case du
///     menu Edition gardée dans les préférences : la planche souligne les fautes des blocs visibles
///     de la page, dans son <c>Overlay</c> (<c>PageCanvas.CaracteresSpeciaux.cs</c>).
///   - <b><c>F7</c></b> ouvre la barre de recherche de N20 en mode orthographe : la faute suivante,
///     dans l'ordre de lecture de N20 — les pages, puis de haut en bas et de gauche à droite —,
///     sélectionnée en saisie comme un texte trouvé ; ses suggestions ; <i>Ignorer</i>,
///     <i>Ignorer tout</i>, <i>Remplacer</i>, en un pas, et <i>Ajouter au dictionnaire</i>.
///   - <b>Le dictionnaire de l'utilisateur</b>, par langue, est dans les préférences : il survit
///     au redémarrage. <i>Ignorer tout</i> ne vaut que pour la session.
///   - <b>Un dictionnaire absent</b> le dit une fois, à la barre d'état, et rien n'est souligné dans
///     cette langue.
/// </summary>
public partial class MainWindowViewModel
{
    private Orthographe? _orthographe;

    /// <summary>Les langues dont l'absence de dictionnaire a été dite.</summary>
    private readonly HashSet<string> _languesSansDictionnaireDites = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Le correcteur, ou rien : sans lui, rien n'est vérifié ni souligné. L'application le donne
    /// sur les dictionnaires du système ; le dictionnaire de l'utilisateur, gardé dans les
    /// préférences, y entre aussitôt.
    /// </summary>
    public Orthographe? Orthographe
    {
        get => _orthographe;
        set
        {
            if (ReferenceEquals(_orthographe, value)) return;
            if (_orthographe is not null) _orthographe.DictionnaireAbsent -= DireLeDictionnaireAbsent;

            _orthographe = value;
            if (value is not null)
            {
                foreach (var (langue, mots) in Preferences.DictionnaireDeLUtilisateur)
                {
                    foreach (var mot in mots) value.Ajouter(mot, langue);
                }

                value.DictionnaireAbsent += DireLeDictionnaireAbsent;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(VerificationActive));
            OrthographeChangee();
        }
    }

    /// <summary>
    /// « Vérifier pendant la frappe » : les fautes se soulignent sur la page. Gardé dans les
    /// préférences, ouvert d'emblée. Fermé, <c>F7</c> vérifie encore.
    /// </summary>
    public bool VerifierPendantLaFrappe
    {
        get => Preferences.VerifierPendantLaFrappe;
        set
        {
            if (Preferences.VerifierPendantLaFrappe == value) return;

            Preferences.VerifierPendantLaFrappe = value;
            EnregistrerLesPreferences();
            OnPropertyChanged();
            OnPropertyChanged(nameof(VerificationActive));
        }
    }

    /// <summary>Les fautes se soulignent : il y a un correcteur, et la case est cochée.</summary>
    public bool VerificationActive => Orthographe is not null && VerifierPendantLaFrappe;

    /// <summary>
    /// Change chaque fois que ce qui est une faute change sans qu'aucun texte ne change : un mot
    /// ignoré ou ajouté, un correcteur donné. La planche l'écoute pour souligner de nouveau.
    /// </summary>
    [ObservableProperty]
    private int _versionDeLOrthographe;

    private void OrthographeChangee() => VersionDeLOrthographe++;

    /// <summary>La barre d'état le dit une fois par langue : rien n'y sera souligné.</summary>
    private void DireLeDictionnaireAbsent(string langue)
    {
        if (!_languesSansDictionnaireDites.Add(langue)) return;

        var paquet = Services.Orthographe.NomsDeFichier(langue).LastOrDefault() ?? langue;
        Status = $"Aucun dictionnaire pour « {langue} » : le texte dans cette langue n'est pas vérifié " +
                 $"(sudo apt install hunspell-{paquet}).";
    }

    // ------------------------------------------------------------------ la page

    /// <summary>
    /// Les fautes d'un bloc de texte, chaque mot dans sa langue (N9) ; rien quand la vérification
    /// pendant la frappe est fermée. La planche les souligne.
    /// </summary>
    public IReadOnlyList<FauteDOrthographe> FautesDuBloc(TextFrame bloc) =>
        VerificationActive ? Orthographe!.Fautes(bloc, Document) : [];

    /// <summary>
    /// La faute d'un bloc à la position du texte, pour le menu du clic droit : celle qui couvre le
    /// caractère <paramref name="position"/>, ou, <paramref name="boutCompris"/>, dont le curseur
    /// touche le bout. Rien quand la vérification pendant la frappe est fermée : sans trait
    /// ondulé, le menu ne propose rien.
    /// </summary>
    public FauteDOrthographe? FauteEn(TextFrame bloc, int position, bool boutCompris) =>
        FautesDuBloc(bloc).Cast<FauteDOrthographe?>()
            .FirstOrDefault(f => f!.Value.Debut <= position && (position < f.Value.Fin || boutCompris && position == f.Value.Fin));

    /// <summary>Ce que le dictionnaire propose à la place du mot fautif.</summary>
    public IReadOnlyList<string> SuggestionsPour(FauteDOrthographe faute) =>
        Orthographe?.Suggestions(faute.Mot, faute.Langue) ?? [];

    /// <summary>
    /// Une suggestion du clic droit : le mot fautif du bloc est remplacé, en un pas, s'il est
    /// toujours là ; le texte remplacé prend le style de son premier caractère (Q1, N20). Rend la
    /// position qui suit le mot remplacé, ou rien si le mot n'y est plus.
    /// </summary>
    public int? CorrigerLaFaute(TextFrame bloc, FauteDOrthographe faute, string par)
    {
        if (!EstEncoreLa(bloc.Text, faute)) return null;

        UnGesteUnPas(() => Recherche.Remplacer(bloc, faute.Plage, par));
        Status = $"« {faute.Mot} » remplacé par « {par} ».";
        return faute.Debut + par.Length;
    }

    /// <summary><i>Ignorer tout</i> : le mot n'est plus une faute dans sa langue, le temps de la session.</summary>
    public void IgnorerToutLeMot(FauteDOrthographe faute)
    {
        Orthographe?.IgnorerPartout(faute.Mot, faute.Langue);
        OrthographeChangee();
        Status = $"« {faute.Mot} » est ignoré jusqu'à la fin de la session.";
    }

    /// <summary>
    /// <i>Ajouter au dictionnaire</i> : le mot entre au dictionnaire de l'utilisateur pour sa
    /// langue, que les préférences gardent d'une session à l'autre.
    /// </summary>
    public void AjouterLeMot(FauteDOrthographe faute)
    {
        if (Preferences.AjouterAuDictionnaire(faute.Langue, faute.Mot)) EnregistrerLesPreferences();
        Orthographe?.Ajouter(faute.Mot, faute.Langue);
        OrthographeChangee();
        Status = $"« {faute.Mot} » ajouté au dictionnaire ({faute.Langue}).";
    }

    private static bool EstEncoreLa(string? texte, FauteDOrthographe faute) =>
        texte is not null && faute.Fin <= texte.Length && string.CompareOrdinal(texte, faute.Debut, faute.Mot, 0, faute.Longueur) == 0;

    // ------------------------------------------------------------------ F7, dans la barre de N20

    /// <summary>La barre de recherche montre la vérification orthographique, et non la recherche.</summary>
    [ObservableProperty]
    private bool _modeOrthographe;

    /// <summary>Le mot fautif montré par la barre ; vide quand la vérification est finie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UneFauteEstMontree))]
    private string _motFautif = "";

    /// <summary>La suggestion choisie dans la barre, que <i>Remplacer</i> écrit.</summary>
    [ObservableProperty]
    private string? _suggestionChoisie;

    /// <summary>Ce que le dictionnaire propose pour le mot montré.</summary>
    public ObservableCollection<string> SuggestionsDeLaFaute { get; } = [];

    /// <summary>Une faute est montrée : les boutons de la barre ont sur quoi jouer.</summary>
    public bool UneFauteEstMontree => !string.IsNullOrEmpty(MotFautif);

    /// <summary>La faute que la barre montre : le texte où elle est, et elle.</summary>
    private sealed record FauteMontree(Cible Cible, FauteDOrthographe Faute);

    private FauteMontree? _fauteMontree;

    /// <summary>
    /// <c>F7</c>, <i>Edition ▸ Vérifier l'orthographe…</i> : la barre de N20 s'ouvre en mode
    /// orthographe, sur la faute suivante, depuis la sélection du texte en saisie, ou le bloc
    /// sélectionné, ou le début de la page courante.
    /// </summary>
    [RelayCommand]
    private void VerifierLOrthographe()
    {
        if (Orthographe is null)
        {
            Status = "La vérification orthographique n'a pas de correcteur.";
            return;
        }

        ModeOrthographe = true;
        RechercheVisible = true;
        _fauteMontree = null;
        FauteSuivante(null);
        // La barre prend le focus une fois la faute montrée, la saisie ouverte : la sélection du
        // mot reste celle que le modèle de vue garde (N15), dessinée sur la composition (N21).
        OrthographeDemandee?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// <c>F7</c> : la barre, en mode orthographe, doit prendre le focus — dans la liste des
    /// suggestions, où <c>↑</c> et <c>↓</c> choisissent, <c>Entrée</c> remplace et <c>Échap</c>
    /// rend le mot. La barre l'écoute.
    /// </summary>
    public event EventHandler? OrthographeDemandee;

    /// <summary><i>Ignorer</i> : la faute reste, la vérification passe à la suivante.</summary>
    [RelayCommand]
    private void IgnorerLaFaute()
    {
        if (FauteCourante() is not { } montree)
        {
            FauteSuivante(null);
            return;
        }

        FauteSuivante((montree.Cible, montree.Faute.Fin));
    }

    /// <summary><i>Ignorer tout</i> : le mot n'est plus une faute de la session, puis la suivante.</summary>
    [RelayCommand]
    private void IgnorerToutesLesFautes()
    {
        if (FauteCourante() is not { } montree) return;

        IgnorerToutLeMot(montree.Faute);
        FauteSuivante((montree.Cible, montree.Faute.Fin));
    }

    /// <summary>
    /// <i>Remplacer</i> : le mot fautif est remplacé par la suggestion choisie, en un pas, puis la
    /// faute suivante est montrée. Sans suggestion, rien n'est remplacé, et la barre d'état le dit.
    /// </summary>
    [RelayCommand]
    private void RemplacerLaFaute()
    {
        if (FauteCourante() is not { } montree) return;

        var par = SuggestionChoisie ?? SuggestionsDeLaFaute.FirstOrDefault();
        if (string.IsNullOrEmpty(par))
        {
            Status = $"Aucune suggestion pour « {montree.Faute.Mot} » : corrigez-le dans le texte, ou ignorez-le.";
            return;
        }

        UnGesteUnPas(() => RemplacerLOccurrence(new Occurrence(montree.Cible, montree.Faute.Plage), par));
        var remplace = $"« {montree.Faute.Mot} » remplacé par « {par} »";
        FauteSuivante((montree.Cible, montree.Faute.Debut + par.Length));
        Status = $"{remplace}. {Status}";
    }

    /// <summary><i>Ajouter au dictionnaire</i> : le mot y entre pour sa langue, puis la faute suivante.</summary>
    [RelayCommand]
    private void AjouterAuDictionnaire()
    {
        if (FauteCourante() is not { } montree) return;

        AjouterLeMot(montree.Faute);
        FauteSuivante((montree.Cible, montree.Faute.Fin));
    }

    /// <summary>
    /// La faute que la barre montre, si son mot est toujours à sa place ; sinon rien — le texte a
    /// changé sous elle, et la vérification repartira de là où l'on est.
    /// </summary>
    private FauteMontree? FauteCourante() =>
        _fauteMontree is { } montree && Cibles().Contains(montree.Cible) && EstEncoreLa(montree.Cible.Texte, montree.Faute)
            ? montree
            : null;

    /// <summary>Les fautes d'un texte où l'on cherche, chaque mot dans sa langue.</summary>
    private IReadOnlyList<FauteDOrthographe> FautesDe(Cible cible) => Orthographe is not { } correcteur ? []
        : cible.Case is { } case_ ? correcteur.Fautes(case_, Document)
        : cible.Bloc is { } bloc ? correcteur.Fautes(bloc, Document)
        : [];

    /// <summary>
    /// La faute suivante, à partir de <paramref name="apres"/> — la fin d'une faute ignorée ou
    /// remplacée —, ou de là où l'on est : la sélection du texte en saisie — le mot du curseur
    /// compris —, le bloc sélectionné, ou la page courante. Au bout du document, la vérification
    /// reprend au début et revient au texte d'où elle partait. Sans faute, elle est finie, et le dit.
    /// </summary>
    private void FauteSuivante((Cible Cible, int Depuis)? apres)
    {
        var cibles = Cibles();
        int depart, depuis;
        if (apres is { } point)
        {
            (depart, depuis) = (cibles.IndexOf(point.Cible), point.Depuis);
        }
        else
        {
            (depart, depuis) = Depart(cibles);
            // Le curseur dans un mot le vérifie : la recherche partirait de la fin de la sélection.
            if (SelectionDuTexte is { Case: null, EstVide: true } curseur) depuis = Math.Max(0, curseur.Curseur - 1);
        }

        if (depart < 0) (depart, depuis) = (0, 0);

        for (var pas = 0; pas <= cibles.Count && cibles.Count > 0; pas++)
        {
            var rang = (depart + pas) % cibles.Count;
            var fautes = FautesDe(cibles[rang]);
            var faute = pas == 0 ? fautes.Cast<FauteDOrthographe?>().FirstOrDefault(f => f!.Value.Fin > depuis)
                : fautes.Count > 0 ? fautes[0] : (FauteDOrthographe?)null;
            if (faute is not { } trouvee) continue;

            MontrerLaFaute(new FauteMontree(cibles[rang], trouvee), reprise: depart + pas >= cibles.Count);
            return;
        }

        _fauteMontree = null;
        MotFautif = "";
        SuggestionsDeLaFaute.Clear();
        SuggestionChoisie = null;
        Status = "La vérification orthographique est terminée.";
    }

    /// <summary>
    /// Montre la faute : sa page, son bloc sélectionné ; dans un bloc de texte, le mot sélectionné
    /// en saisie, comme un texte trouvé (N20) ; dans un groupe ou une case, la barre d'état dit où.
    /// La barre montre le mot et ses suggestions, la première choisie.
    /// </summary>
    private void MontrerLaFaute(FauteMontree montree, bool reprise)
    {
        _fauteMontree = montree;
        var (cible, faute) = (montree.Cible, montree.Faute);

        SuggestionsDeLaFaute.Clear();
        foreach (var suggestion in SuggestionsPour(faute)) SuggestionsDeLaFaute.Add(suggestion);
        SuggestionChoisie = SuggestionsDeLaFaute.FirstOrDefault();
        MotFautif = faute.Mot;

        if (!ReferenceEquals(CurrentPage, cible.Page)) CurrentPage = cible.Page;
        if (Selection.Count != 1 || !ReferenceEquals(Selection[0], cible.Racine)) SelectOnly(cible.Racine);

        var numero = Document.Pages.IndexOf(cible.Page) + 1;
        var dit = $"« {faute.Mot} » n'est pas dans le dictionnaire ({faute.Langue}), page {numero}";
        var repris = reprise ? " — la vérification a repris au début du document" : "";

        if (cible.EnSaisie)
        {
            TexteTrouve?.Invoke(this, new SelectionDuTexte(cible.Bloc!, null, faute.Debut, faute.Fin, faute.Fin));
            Status = $"{dit}{repris}.";
            return;
        }

        Status = cible.Racine is GroupFrame groupe
            ? $"{dit}, dans le groupe « {groupe.Name} »{repris}."
            : $"{dit}, dans la case {cible.Case?.Label} du tableau « {cible.Tableau?.Name} »{repris}.";
    }

    /// <summary>
    /// La barre se ferme, ou repasse à la recherche : le mode orthographe s'achève. Fermée sur une
    /// faute sélectionnée en saisie, le focus revient au mot, comme pour un texte trouvé (N20).
    /// </summary>
    partial void OnRechercheVisibleChanged(bool value)
    {
        if (value || !ModeOrthographe) return;

        ModeOrthographe = false;
        if (FauteCourante() is { Cible.EnSaisie: true } && SelectionDuTexte is { } selection) SaisieRendue?.Invoke(this, selection);
        _fauteMontree = null;
    }
}
