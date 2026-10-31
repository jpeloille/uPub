using System;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Converters;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Le paragraphe que règlent l'alignement, les retraits et les espacements (poste R6 ; fiche 23,
/// b8, b9, b11) : hors saisie, le bloc de texte seul, comme avant ; en saisie, les paragraphes que
/// touche la sélection du texte (<see cref="MainWindowViewModel.ParagraphesDeLaSelection"/>, N15),
/// ou celui du curseur.
///
/// Les quatre alignements de la barre, du menu Texte et de <c>Ctrl+L</c>, <c>Ctrl+E</c>,
/// <c>Ctrl+R</c>, <c>Ctrl+J</c> (N10), et les cinq réglages de R2 au dock, se lient à lui, sous les
/// noms du bloc : <c>{Binding Align}</c>, <c>{Binding IndentLeft}</c>… Les liaisons s'écrivent
/// comme avant — c'est leur contexte qui change —, si bien que les comptes de la recette I3
/// tiennent.
///
/// <b>Ce qu'il lit</b> : hors saisie, les réglages du bloc ; en saisie, ce que les paragraphes visés
/// ont en commun, écarts compris (<see cref="MiseEnForme.Lire(string, System.Collections.Generic.IReadOnlyList{ParagraphSpan}, ParagrapheEffectif, Plage)"/>).
/// Un réglage où ils diffèrent est nul : l'état mixte, aucun bouton d'alignement allumé, un champ
/// vide.
///
/// <b>Ce qu'il écrit</b> : hors saisie, le bloc ; en saisie, des écarts <see cref="ParagraphSpan"/>,
/// par <see cref="MiseEnForme.Poser(string, System.Collections.Generic.IList{ParagraphSpan}, ParagrapheEffectif, Plage, ReglagesDeLaPlage)"/> :
/// deux écarts voisins identiques fusionnent, un écart qui ne s'écarte plus de rien disparaît. Une
/// valeur nulle — un champ vidé, l'état mixte que le champ renvoie — n'écrit rien.
///
/// L'interligne n'y est pas : il reste un réglage du bloc, un écart avec Publisher, qui le règle
/// par paragraphe, écrit et assumé (phase N).
/// </summary>
public sealed class ParagrapheVise : ObservableObject
{
    /// <summary>Le taquet d'« Augmenter le retrait » et de « Diminuer le retrait » : un demi-pouce, comme Publisher.</summary>
    public const double Taquet = 36;

    private static readonly string[] Reglages =
    [
        nameof(Align), nameof(IndentLeft), nameof(IndentRight), nameof(IndentFirstLine),
        nameof(SpaceBefore), nameof(SpaceAfter), nameof(EnSaisie),
    ];

    private readonly MainWindowViewModel _vm;
    private TextFrame? _blocSuivi;

    /// <summary>
    /// Une écriture du panneau est en cours : la collection des écarts passe par plusieurs états,
    /// qu'aucun champ ne doit lire — il renverrait au modèle une valeur intermédiaire. Les avis
    /// attendent la fin de l'écriture, et partent une fois.
    /// </summary>
    private bool _enEcriture;

    internal ParagrapheVise(MainWindowViewModel vm)
    {
        _vm = vm;
        _vm.PropertyChanged += SurModeleDeVue;
        SuivreLeBloc();
    }

    /// <summary>Le bloc de texte seul que le panneau règle ; nul sans lui.</summary>
    public TextFrame? Bloc => _vm.SingleSelected as TextFrame;

    /// <summary>
    /// Les paragraphes visés en saisie — ceux de la sélection du texte, ou celui du curseur —, si
    /// la saisie est celle du bloc seul ; nuls hors saisie, et le panneau règle alors le bloc.
    /// </summary>
    public Plage? Paragraphes =>
        Bloc is { } bloc && _vm.SelectionDuTexte is { Case: null } selection && ReferenceEquals(selection.Bloc, bloc)
            ? _vm.ParagraphesDeLaSelection
            : null;

    /// <summary>Le panneau règle des paragraphes, et non le bloc entier.</summary>
    public bool EnSaisie => Paragraphes is not null;

    /// <summary>Ce que le panneau montre : les réglages du bloc, ou ce que les paragraphes visés ont en commun.</summary>
    public ReglagesDeLaPlage Lire()
    {
        if (Bloc is not { } bloc) return default;
        if (Paragraphes is { } paragraphes)
        {
            return MiseEnForme.Lire(bloc.Text ?? "", bloc.Paragraphs, bloc.Paragraph, paragraphes);
        }

        return new ReglagesDeLaPlage
        {
            IndentLeft = bloc.IndentLeft, IndentRight = bloc.IndentRight, IndentFirstLine = bloc.IndentFirstLine,
            SpaceBefore = bloc.SpaceBefore, SpaceAfter = bloc.SpaceAfter, Align = bloc.Align,
        };
    }

    /// <summary>Le fer ; nul quand les paragraphes visés en ont plusieurs — alors aucun bouton n'est allumé.</summary>
    public TextAlign? Align
    {
        get => Lire().Align;
        set { if (value is { } fer) Poser(new ReglagesDeLaPlage { Align = fer }); }
    }

    /// <summary>Le retrait gauche, en points ; nul s'il est mixte.</summary>
    public double? IndentLeft
    {
        get => Lire().IndentLeft;
        set { if (value is { } v) Poser(new ReglagesDeLaPlage { IndentLeft = v }); }
    }

    /// <summary>Le retrait droit, en points ; nul s'il est mixte.</summary>
    public double? IndentRight
    {
        get => Lire().IndentRight;
        set { if (value is { } v) Poser(new ReglagesDeLaPlage { IndentRight = v }); }
    }

    /// <summary>Le retrait de première ligne, en points ; nul s'il est mixte.</summary>
    public double? IndentFirstLine
    {
        get => Lire().IndentFirstLine;
        set { if (value is { } v) Poser(new ReglagesDeLaPlage { IndentFirstLine = v }); }
    }

    /// <summary>L'espace avant, en points ; nul s'il est mixte.</summary>
    public double? SpaceBefore
    {
        get => Lire().SpaceBefore;
        set { if (value is { } v) Poser(new ReglagesDeLaPlage { SpaceBefore = v }); }
    }

    /// <summary>L'espace après, en points ; nul s'il est mixte.</summary>
    public double? SpaceAfter
    {
        get => Lire().SpaceAfter;
        set { if (value is { } v) Poser(new ReglagesDeLaPlage { SpaceAfter = v }); }
    }

    /// <summary>
    /// Pose chaque réglage que <paramref name="reglages"/> renseigne : sur le bloc hors saisie, en
    /// écarts sur les paragraphes visés en saisie. Rend vrai si le document a changé. La
    /// transaction est celle de l'appelant : le champ du dock qui a le focus, ou le geste
    /// (<c>UnGesteUnPas</c>).
    /// </summary>
    internal bool Poser(ReglagesDeLaPlage reglages) => Ecrire(bloc =>
        Paragraphes is { } paragraphes
            ? MiseEnForme.Poser(bloc.Text ?? "", bloc.Paragraphs, bloc.Paragraph, paragraphes, reglages)
            : PoserSurLeBloc(bloc, reglages));

    /// <summary>
    /// « Augmenter le retrait » (<paramref name="sens"/> positif) ou « Diminuer le retrait » : le
    /// retrait gauche passe au taquet suivant ou précédent, de <see cref="Taquet"/> en
    /// <see cref="Taquet"/> points, sans descendre sous zéro. En saisie, chaque paragraphe visé
    /// part de son propre retrait, comme dans Publisher : trois paragraphes à 0, 10 et 40 pt
    /// passent à 36, 36 et 72.
    /// </summary>
    internal bool Decaler(int sens) => Ecrire(bloc =>
    {
        if (Paragraphes is not { } paragraphes)
        {
            var nouveau = AuTaquet(bloc.IndentLeft, sens);
            if (nouveau == bloc.IndentLeft) return false;
            bloc.IndentLeft = nouveau;
            return true;
        }

        var texte = bloc.Text ?? "";
        var effectifs = ParagrapheEffectif.Resoudre(Recalage.NombreDeParagraphes(texte), bloc.Paragraphs, bloc.Paragraph);
        var change = false;
        for (var i = paragraphes.Start; i < paragraphes.Start + paragraphes.Length && i < effectifs.Count; i++)
        {
            var nouveau = AuTaquet(effectifs[i].IndentLeft, sens);
            if (nouveau == effectifs[i].IndentLeft) continue;
            change |= MiseEnForme.Poser(texte, bloc.Paragraphs, bloc.Paragraph, new Plage(i, 1),
                new ReglagesDeLaPlage { IndentLeft = nouveau });
        }

        return change;
    });

    /// <summary>
    /// Le taquet suivant (sens positif) ou précédent d'un retrait. Un retrait déjà au taquet passe
    /// au suivant ; un retrait nul ou négatif ne diminue plus — c'est au retrait de première
    /// ligne de pendre, pas au paragraphe.
    /// </summary>
    public static double AuTaquet(double retrait, int sens)
    {
        // Un millième de point d'indulgence : 36 lu en millimètres et rendu en points n'est pas
        // toujours 36 au dernier bit.
        const double jeu = 1e-3;
        if (sens > 0) return (Math.Floor((retrait + jeu) / Taquet) + 1) * Taquet;
        if (retrait <= jeu) return retrait;
        return Math.Max(0, (Math.Ceiling((retrait - jeu) / Taquet) - 1) * Taquet);
    }

    private static bool PoserSurLeBloc(TextFrame bloc, ReglagesDeLaPlage reglages)
    {
        var avant = bloc.Paragraph;
        if (reglages.IndentLeft is { } gauche) bloc.IndentLeft = gauche;
        if (reglages.IndentRight is { } droite) bloc.IndentRight = droite;
        if (reglages.IndentFirstLine is { } premiere) bloc.IndentFirstLine = premiere;
        if (reglages.SpaceBefore is { } avantLui) bloc.SpaceBefore = avantLui;
        if (reglages.SpaceAfter is { } apres) bloc.SpaceAfter = apres;
        if (reglages.Align is { } fer) bloc.Align = fer;
        return bloc.Paragraph != avant;
    }

    /// <summary>
    /// Une écriture du panneau sur le bloc seul, ses avis tenus jusqu'à la fin. Interne depuis N23 :
    /// la liste du paragraphe (<see cref="ListeVisee"/>) écrit par elle.
    /// </summary>
    internal bool Ecrire(Func<TextFrame, bool> ecriture)
    {
        if (Bloc is not { } bloc || _enEcriture) return false;

        _enEcriture = true;
        try
        {
            return ecriture(bloc);
        }
        finally
        {
            _enEcriture = false;
            Notifier();
        }
    }

    // ------------------------------------------------------------------ ce qu'il suit

    /// <summary>
    /// Ce qui change ce que le panneau montre, côté modèle de vue : un autre bloc seul — une autre
    /// sélection, ou l'annulation qui relit le document —, et la sélection du texte, qui naît à
    /// l'entrée en saisie, suit le curseur et meurt à la sortie.
    /// </summary>
    private void SurModeleDeVue(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.SingleSelected):
                SuivreLeBloc();
                break;
            case nameof(MainWindowViewModel.SelectionDuTexte):
                Notifier();
                break;
        }
    }

    /// <summary>
    /// Suit le bloc seul : ses réglages, son texte — une frappe qui ajoute un paragraphe change
    /// ceux que vise le curseur — et ses écarts, comme <c>ControleDeBloc</c> les suit pour les
    /// dessiner.
    /// </summary>
    private void SuivreLeBloc()
    {
        if (_blocSuivi is not null)
        {
            _blocSuivi.PropertyChanged -= SurBlocRetouche;
            _blocSuivi.Paragraphs.CollectionChanged -= SurEcartsRetouches;
        }

        _blocSuivi = Bloc;

        if (_blocSuivi is not null)
        {
            _blocSuivi.PropertyChanged += SurBlocRetouche;
            _blocSuivi.Paragraphs.CollectionChanged += SurEcartsRetouches;
        }

        Notifier();
    }

    private void SurBlocRetouche(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TextFrame.Text) or nameof(TextFrame.Align) or nameof(TextFrame.IndentLeft)
                or nameof(TextFrame.IndentRight) or nameof(TextFrame.IndentFirstLine)
                or nameof(TextFrame.SpaceBefore) or nameof(TextFrame.SpaceAfter) or nameof(TextFrame.Liste):
                Notifier();
                break;
        }
    }

    private void SurEcartsRetouches(object? sender, NotifyCollectionChangedEventArgs e) => Notifier();

    private void Notifier()
    {
        if (_enEcriture) return;
        foreach (var reglage in Reglages) OnPropertyChanged(reglage);
    }
}

/// <summary>
/// Ce que le poste R6 ajoute au modèle de vue — le panneau règle le paragraphe du curseur. Un
/// fichier à lui, comme la règle 10 de la phase I le veut.
/// </summary>
public partial class MainWindowViewModel
{
    private ParagrapheVise? _paragrapheVise;

    /// <summary>
    /// Ce que règlent l'alignement, les retraits et les espacements : le bloc hors saisie, les
    /// paragraphes de la sélection en saisie. La barre et le dock s'y lient.
    /// </summary>
    public ParagrapheVise ParagrapheVise => _paragrapheVise ??= new ParagrapheVise(this);

    /// <summary>
    /// Les quatre alignements — la barre, le menu Texte, <c>Ctrl+L</c>, <c>Ctrl+E</c>,
    /// <c>Ctrl+R</c>, <c>Ctrl+J</c> — en un pas, où que soit le focus : sur le bloc hors saisie,
    /// sur les paragraphes visés en saisie. Ils posent, et ne basculent pas : <c>Ctrl+E</c> sur
    /// un paragraphe centré le laisse centré, comme le bouton de la barre.
    /// </summary>
    private void AlignerLesParagraphesVises(TextAlign alignement)
    {
        if (SingleSelected is not TextFrame) return;

        var vises = ParagrapheVise.Paragraphes;
        UnGesteUnPas(() => ParagrapheVise.Poser(new ReglagesDeLaPlage { Align = alignement }));
        Status = vises switch
        {
            null => $"Texte aligné : {Libelles.De(alignement)}",
            { Length: 1 } => $"Paragraphe aligné : {Libelles.De(alignement)}",
            { Length: var n } => $"{n} paragraphes alignés : {Libelles.De(alignement)}",
        };
    }

    /// <summary>« Diminuer le retrait », au dock à côté des retraits : le taquet précédent, en un pas.</summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void DiminuerLeRetrait() => DecalerLeRetrait(-1, "Retrait diminué");

    /// <summary>« Augmenter le retrait » : le taquet suivant, de 36 pt en 36 pt, en un pas.</summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void AugmenterLeRetrait() => DecalerLeRetrait(+1, "Retrait augmenté");

    private void DecalerLeRetrait(int sens, string message)
    {
        if (SingleSelected is not TextFrame) return;

        UnGesteUnPas(() => ParagrapheVise.Decaler(sens));
        Status = message;
    }
}
