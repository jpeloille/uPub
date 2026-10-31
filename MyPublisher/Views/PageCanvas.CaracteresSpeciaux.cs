using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Les caractères spéciaux sur la page (poste N22 ; fiche 23, a27 et d14) : <i>Affichage ▸
/// Caractères spéciaux</i> et <c>Ctrl+Maj+Y</c>, comme Publisher (le livre, p. 9 et 24). Quand la
/// bascule de la session est ouverte (<see cref="MainWindowViewModel.CaracteresSpeciauxVisibles"/>),
/// chaque bloc de texte visible de la page montre un point pour chaque espace, une flèche pour chaque
/// tabulation, ¶ à chaque fin de paragraphe et ↵ à chaque saut de ligne forcé.
///
/// Les places viennent de la géométrie du texte tel qu'il est dessiné (<see cref="GeometrieDuTexte"/>,
/// poste N3) : la fin d'un paragraphe est au <see cref="GeometrieDuTexte.PointDe"/> de son saut,
/// une espace occupe sa chasse sur sa ligne, blanc élargi d'une ligne justifiée et blanc pendant au
/// bout d'une ligne renvoyée compris. Pendant la saisie, c'est la même composition, qui reste à
/// l'écran depuis la voie 2a (poste N21) : les marques suivent la frappe.
///
/// Rien n'en va dans <c>PageRoot</c> (règle 4) : le contrôle qui les dessine,
/// <see cref="CaracteresSpeciaux"/>, est posé dans l'<c>Overlay</c>, sous les marques de la saisie
/// et les poignées, et chaque bloc y est mené par la transformation des contrôles mêmes qui le
/// dessinent — zoom, place, rotation —, la page et le bloc découpant ce qui sort d'eux. L'encre de la
/// page et le PDF ne changent pas.
///
/// **Ce qu'il en coûte.** Composer un bloc coûte jusqu'à 15 ms sur ATA 21 (N3) : chaque bloc garde
/// ses caractères, et ne les recompose que quand il change — son texte, ses fragments, ses écarts,
/// une propriété, son contenu, la charte de la page. Une passe de mise en page ne fait que relire la
/// place des blocs à l'écran. Bascule fermée, rien n'est suivi ni calculé.
///
/// Le branchement se fait au premier chargement de la planche (<see cref="OnLoaded"/>), et non dans
/// son constructeur, qu'un seul poste d'une salve retouche (règle 16 de la phase N).
/// </summary>
public partial class PageCanvas
{
    /// <summary>Ce qu'on sait d'un bloc dont on montre les caractères.</summary>
    private sealed class SuiviDesCaracteres
    {
        /// <summary>Le bloc qu'on écoute, celui que le contrôle dessinait quand on s'y est abonné.</summary>
        public TextFrame? Bloc;

        /// <summary>Les caractères sont à recomposer.</summary>
        public bool Perime = true;

        public IReadOnlyList<CaracterePose> Caracteres = [];

        public IReadOnlyList<CoucheDeDecoupe> Couches = [];
    }

    /// <summary>
    /// Le contrôle qui dessine les caractères, posé dans l'<c>Overlay</c> à la première ouverture de
    /// la bascule (<see cref="PoserLesCaracteresDansLOverlay"/>).
    /// </summary>
    private readonly CaracteresSpeciaux _caracteresSpeciaux = new() { Name = "CaracteresSpeciaux" };

    /// <summary>Les blocs suivis, dans l'ordre de la page.</summary>
    private readonly List<TexteCompose> _ordreDesCaracteres = [];

    private readonly Dictionary<TexteCompose, SuiviDesCaracteres> _caracteresSuivis = [];

    /// <summary>Le branchement est fait : il ne se fait qu'une fois, la planche pouvant être rechargée.</summary>
    private bool _caracteresBranches;

    /// <summary>Une recomposition est demandée, et pas encore faite.</summary>
    private bool _caracteresDemandes;

    private MainWindowViewModel? _vmDesCaracteres;

    private bool CaracteresVisibles => _vmDesCaracteres?.CaracteresSpeciauxVisibles == true;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_caracteresBranches) return;
        _caracteresBranches = true;

        // Un changement de jeu change la police, donc la coupure, des textes qui y font
        // référence, sans toucher aux blocs (poste C5b) : tout se recompose.
        PageRoot.PropertyChanged += (_, args) =>
        {
            if (args.Property == JeuAffiche.CharteProperty) ToutRecomposer();
        };

        LayoutUpdated += (_, _) => SuivreLesCaracteres();
        DataContextChanged += (_, _) => SuivreLeModeleDesCaracteres();
        SuivreLeModeleDesCaracteres();
    }

    private void SuivreLeModeleDesCaracteres()
    {
        if (_vmDesCaracteres is not null) _vmDesCaracteres.PropertyChanged -= OnModeleDesCaracteres;
        _vmDesCaracteres = DataContext as MainWindowViewModel;
        if (_vmDesCaracteres is not null) _vmDesCaracteres.PropertyChanged += OnModeleDesCaracteres;
        ToutRecomposer();
    }

    private void OnModeleDesCaracteres(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CaracteresSpeciauxVisibles)) ToutRecomposer();
    }

    /// <summary>
    /// La bascule a changé, ou la charte : bascule fermée, on lâche tout et on efface ; ouverte, tout
    /// est à recomposer.
    /// </summary>
    private void ToutRecomposer()
    {
        if (!CaracteresVisibles)
        {
            foreach (var compose in _ordreDesCaracteres.ToList()) LacherLesCaracteres(compose);
            _caracteresSpeciaux.Effacer();
            return;
        }

        PoserLesCaracteresDansLOverlay();
        foreach (var suivi in _caracteresSuivis.Values) suivi.Perime = true;
        SuivreLesCaracteres();
        DemanderLesCaracteres();
    }

    /// <summary>
    /// Pose le contrôle des caractères dans l'<c>Overlay</c>, sous les marques de la saisie et les
    /// poignées, sur toute la planche — une fois, à la première ouverture de la bascule, et non au
    /// chargement : bascule jamais ouverte, l'arbre de la planche est celui d'avant le poste. Posé au
    /// chargement de la planche, même vide et sans taille, il faisait tomber six tests d'autres postes
    /// qui lisent l'image composée de la fenêtre (vignettes, dock, alerte, rafraîchissement) quand ils
    /// tournent ensemble — mesuré en alternant la base et la copie, six passages chacune : la base
    /// passe toujours, la copie jamais ; posé à la première ouverture, ils passent six fois sur six.
    /// La cause n'est pas élucidée.
    /// </summary>
    private void PoserLesCaracteresDansLOverlay()
    {
        if (_caracteresSpeciaux.Parent is not null) return;

        _caracteresSpeciaux.Bind(WidthProperty, new Binding("Bounds.Width") { Source = Overlay });
        _caracteresSpeciaux.Bind(HeightProperty, new Binding("Bounds.Height") { Source = Overlay });
        Overlay.Children.Insert(0, _caracteresSpeciaux);
    }

    /// <summary>
    /// Une passe de mise en page vient de finir : les blocs de la page ont pu venir ou s'en aller —
    /// une autre page, un bloc ajouté, masqué ou retiré —, ou bouger à l'écran — zoom, place, taille.
    /// On relit les blocs visibles et leur place ; seuls ceux qui sont nouveaux se recomposent.
    /// </summary>
    private void SuivreLesCaracteres()
    {
        if (!CaracteresVisibles) return;

        var presents = PageRoot.GetVisualDescendants().OfType<TexteCompose>()
            .Where(compose => compose.Bloc is not null && compose.IsEffectivelyVisible)
            .ToList();
        var change = !presents.SequenceEqual(_ordreDesCaracteres);

        foreach (var compose in _ordreDesCaracteres.Except(presents).ToList()) LacherLesCaracteres(compose);
        _ordreDesCaracteres.Clear();
        _ordreDesCaracteres.AddRange(presents);

        foreach (var compose in presents)
        {
            if (!_caracteresSuivis.TryGetValue(compose, out var suivi))
            {
                suivi = new SuiviDesCaracteres();
                _caracteresSuivis[compose] = suivi;
                compose.PropertyChanged += OnComposeDesCaracteres;
            }

            if (!ReferenceEquals(suivi.Bloc, compose.Bloc))
            {
                EcouterLeBlocDesCaracteres(suivi, compose.Bloc);
                suivi.Perime = true;
            }

            var couches = CouchesDe(compose);
            if (!couches.SequenceEqual(suivi.Couches))
            {
                suivi.Couches = couches;
                change = true;
            }
        }

        if (_caracteresSuivis.Values.Any(suivi => suivi.Perime)) DemanderLesCaracteres();
        else if (change) PublierLesCaracteres();
    }

    /// <summary>
    /// Demande une recomposition des blocs périmés, une fois le geste en cours fini : le texte tapé
    /// passe par la liaison du champ, puis par la saisie du bloc, avant que la composition ne le voie.
    /// </summary>
    private void DemanderLesCaracteres()
    {
        if (_caracteresDemandes) return;

        _caracteresDemandes = true;
        Dispatcher.UIThread.Post(() =>
        {
            _caracteresDemandes = false;
            RecomposerLesCaracteres();
        });
    }

    private void RecomposerLesCaracteres()
    {
        if (!CaracteresVisibles) return;

        foreach (var compose in _ordreDesCaracteres)
        {
            var suivi = _caracteresSuivis[compose];
            if (!suivi.Perime) continue;

            suivi.Caracteres = CaracteresDe(compose);
            suivi.Couches = CouchesDe(compose);
            suivi.Perime = false;
        }

        PublierLesCaracteres();
    }

    private void PublierLesCaracteres() =>
        _caracteresSpeciaux.Poser(_ordreDesCaracteres
            .Select(compose => (Compose: compose, Suivi: _caracteresSuivis[compose]))
            .Where(bloc => bloc.Suivi.Caracteres.Count > 0 && bloc.Suivi.Couches.Count > 0)
            .Select(bloc => new CaracteresDuBloc(bloc.Compose, bloc.Suivi.Couches, bloc.Suivi.Caracteres))
            .ToList());

    /// <summary>
    /// Les caractères spéciaux d'un bloc, lus sur la géométrie de sa composition. Chaque caractère
    /// du texte est sur une ligne et une seule, sauts exceptés (N3) : une espace ou une tabulation
    /// se marque sur la sienne, de son bord à celui du caractère qui la suit sur cette même ligne ;
    /// un saut se marque au bout de la ligne qu'il ferme, à son <see cref="GeometrieDuTexte.PointDe"/>
    /// — ¶ pour une fin de paragraphe, ↵ pour un saut de ligne forcé —, et le bout du texte ferme le
    /// dernier paragraphe. Rien pour un bloc à champ, qui n'a pas de géométrie.
    /// </summary>
    private static IReadOnlyList<CaracterePose> CaracteresDe(TexteCompose compose)
    {
        if (compose.Geometrie() is not { } geometrie) return [];

        var texte = compose.Contenu ?? string.Empty;
        var caracteres = new List<CaracterePose>();
        var lignes = geometrie.Lignes;
        for (var rang = 0; rang < lignes.Count; rang++)
        {
            var posee = lignes[rang];
            var ligne = posee.Ligne;
            for (var i = ligne.Debut; i < ligne.Fin && i < texte.Length; i++)
            {
                CaractereSpecial? blanc = texte[i] switch
                {
                    ' ' => CaractereSpecial.Espace,
                    '\t' => CaractereSpecial.Tabulation,
                    _ => null,
                };
                if (blanc is not { } sorte) continue;

                var gauche = geometrie.AbscisseSur(posee, i);
                var droite = geometrie.AbscisseSur(posee, i + 1);
                caracteres.Add(new CaracterePose(sorte, i, new Point(gauche, posee.LigneDeBase),
                    droite - gauche, geometrie.StyleDe(i + 1).FontSize));
            }

            CaractereSpecial? fin = ligne.Fin < texte.Length
                ? texte[ligne.Fin] switch
                {
                    '\n' or '\r' => CaractereSpecial.FinDeParagraphe,
                    Compositeur.SautDeLigne => CaractereSpecial.SautDeLigne,
                    _ => null,
                }
                : rang == lignes.Count - 1 ? CaractereSpecial.FinDeParagraphe : null;
            if (fin is not { } marque) continue;

            caracteres.Add(new CaracterePose(marque, ligne.Fin,
                new Point(geometrie.AbscisseSur(posee, ligne.Fin), posee.LigneDeBase), 0,
                geometrie.StyleDe(ligne.Fin).FontSize));
        }

        return caracteres;
    }

    /// <summary>
    /// Les niveaux de découpe de l'<c>Overlay</c> au bloc : la page, chaque contrôle entre elle et
    /// le bloc qui découpe ce qu'il porte — le panneau qui découpe le texte du bloc, un groupe qui
    /// rogne —, puis le contrôle de la composition, chacun avec la transformation qui le mène dans
    /// le repère du précédent. Rien s'il n'est pas à l'écran.
    /// </summary>
    private IReadOnlyList<CoucheDeDecoupe> CouchesDe(TexteCompose compose)
    {
        var chaine = new List<Visual> { compose };
        for (var parent = compose.GetVisualParent(); parent is not null; parent = parent.GetVisualParent())
        {
            if (parent.ClipToBounds) chaine.Add(parent);
            if (ReferenceEquals(parent, PageRoot)) break;
        }

        chaine.Reverse();
        var couches = new List<CoucheDeDecoupe>(chaine.Count);
        Visual precedent = _caracteresSpeciaux;
        foreach (var niveau in chaine)
        {
            if (niveau.TransformToVisual(precedent) is not { } transformation) return [];

            couches.Add(new CoucheDeDecoupe(transformation, new Rect(niveau.Bounds.Size)));
            precedent = niveau;
        }

        return couches;
    }

    // ------------------------------------------------------- ce qu'on écoute

    private void EcouterLeBlocDesCaracteres(SuiviDesCaracteres suivi, TextFrame? bloc)
    {
        if (suivi.Bloc is { } ancien)
        {
            ancien.PropertyChanged -= OnBlocDesCaracteres;
            ancien.Spans.CollectionChanged -= OnEcartsDesCaracteres;
            ancien.Paragraphs.CollectionChanged -= OnEcartsDesCaracteres;
        }

        suivi.Bloc = bloc;
        if (bloc is null) return;

        bloc.PropertyChanged += OnBlocDesCaracteres;
        bloc.Spans.CollectionChanged += OnEcartsDesCaracteres;
        bloc.Paragraphs.CollectionChanged += OnEcartsDesCaracteres;
    }

    private void LacherLesCaracteres(TexteCompose compose)
    {
        if (_caracteresSuivis.Remove(compose, out var suivi)) EcouterLeBlocDesCaracteres(suivi, null);
        compose.PropertyChanged -= OnComposeDesCaracteres;
        _ordreDesCaracteres.Remove(compose);
    }

    /// <summary>
    /// Un bloc suivi a changé — son texte, une propriété : ses caractères sont à recomposer. Sa place
    /// sur la page n'y change rien, et la passe de mise en page qui suit la relit : un glissé ne
    /// recompose pas le bloc à chaque mouvement.
    /// </summary>
    private void OnBlocDesCaracteres(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Frame.X) or nameof(Frame.Y) or nameof(Frame.Right) or nameof(Frame.Bottom)) return;
        Perimer(suivi => ReferenceEquals(suivi.Bloc, sender));
    }

    /// <summary>Un fragment ou un écart de paragraphe d'un bloc suivi a changé.</summary>
    private void OnEcartsDesCaracteres(object? sender, NotifyCollectionChangedEventArgs e) =>
        Perimer(suivi => suivi.Bloc is { } bloc && (ReferenceEquals(bloc.Spans, sender) || ReferenceEquals(bloc.Paragraphs, sender)));

    /// <summary>Le contrôle d'un bloc suivi dessine un autre contenu — un champ résolu autrement —, ou un autre bloc.</summary>
    private void OnComposeDesCaracteres(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ControleDeBloc.ContenuProperty && e.Property != ControleDeBloc.BlocProperty) return;
        if (sender is TexteCompose compose && _caracteresSuivis.TryGetValue(compose, out var suivi))
        {
            suivi.Perime = true;
            DemanderLesCaracteres();
        }
    }

    private void Perimer(Func<SuiviDesCaracteres, bool> concerne)
    {
        var aucun = true;
        foreach (var suivi in _caracteresSuivis.Values.Where(concerne))
        {
            suivi.Perime = true;
            aucun = false;
        }

        if (!aucun) DemanderLesCaracteres();
    }
}
