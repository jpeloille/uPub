using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Le souligné que la barre montre : la sorte commune aux caractères visés — nulle quand ils en
/// mêlent plusieurs —, et le bouton S, allumé quelle qu'en soit la sorte, éteint, ou nul quand une
/// partie seulement est soulignée (<see cref="MiseEnForme.Allume"/>). Le bouton se lie à
/// <c>Underline.Actif</c>, comme il se liait au souligné du bloc (I3) : la liaison s'écrit comme
/// avant, et les comptes de la recette I3 tiennent.
/// </summary>
public sealed record SouligneVise(Souligne? Sorte, bool? Actif);

/// <summary>
/// Les caractères que règlent la police, le corps, le gras, l'italique, le souligné et la couleur
/// (poste R7 ; fiche 23, b1, b4, b13, b18, b19) — le pendant de <see cref="ParagrapheVise"/> (R6) à
/// l'échelle du caractère.
///
/// <b>Ce qu'il vise.</b> En saisie, la sélection du texte (<see cref="MainWindowViewModel.SelectionDuTexte"/>,
/// N15) ; sans sélection, le mot du curseur (<see cref="MotDuCurseur"/>), comme Publisher 98 (option
/// 421 du dialogue 568) et Word ; hors d'un mot, le curseur seul, qui se lit au style que prendrait la
/// frappe et ne se met pas en forme. Hors saisie, <b>le bloc veut dire tous ses caractères, fragments
/// compris</b> : un réglage pose la valeur sur le bloc et la retire des fragments qui en disaient une
/// autre — <c>Ctrl+G</c> atteint le gras qu'un import a écrit dans un fragment.
///
/// <b>Ce qu'il lit</b> : ce que les caractères visés ont en commun, par
/// <see cref="MiseEnForme.Lire(string, IReadOnlyList{StyleSpan}, StyleEffectif, Plage, Charte)"/> (N2) ;
/// une propriété où ils diffèrent est nulle — un champ vide, un bouton à <c>IsChecked = null</c>.
///
/// <b>Ce qu'il écrit</b> : par <see cref="MiseEnForme.Poser(string, IList{StyleSpan}, StyleEffectif, Plage, StyleDeLaPlage)"/>,
/// en fragments, coupés, recollés et retirés quand ils ne disent plus rien. Une valeur nulle — le
/// champ vidé, l'état mixte que la liste renvoie — n'écrit rien ; la valeur qu'il montre non plus :
/// une liaison qui renvoie ce qu'elle vient de lire ne doit rien changer, et, hors saisie, poser la
/// police lue sur tout le bloc remplacerait par la police qu'elle désigne une référence au jeu que
/// le bloc porte.
///
/// La barre se lie à lui sous les noms du bloc — <c>{Binding FontFamily}</c>, <c>{Binding Bold}</c>… —
/// en le prenant pour contexte, comme les alignements prennent le <see cref="ParagrapheVise"/> ; un
/// tableau garde sa mise en forme à lui, case par case dans le dock, la page n'ayant pas de saisie
/// de case (N27).
/// </summary>
public sealed class MiseEnFormeVisee : ObservableObject
{
    private static readonly string[] Proprietes =
    [
        nameof(FontFamily), nameof(FontSize), nameof(Bold), nameof(Italic), nameof(Underline),
        nameof(Foreground), nameof(EnSaisie),
    ];

    private readonly MainWindowViewModel _vm;
    private TextFrame? _blocSuivi;

    /// <summary>
    /// Une écriture est en cours : la collection des fragments passe par plusieurs états, qu'aucun
    /// champ ne doit lire et renvoyer. Les avis attendent la fin de l'écriture, et partent une fois.
    /// </summary>
    private bool _enEcriture;

    internal MiseEnFormeVisee(MainWindowViewModel vm)
    {
        _vm = vm;
        _vm.PropertyChanged += SurModeleDeVue;
        SuivreLeBloc();
    }

    /// <summary>Le bloc de texte seul que la barre met en forme ; nul sans lui.</summary>
    public TextFrame? Bloc => _vm.SingleSelected as TextFrame;

    /// <summary>
    /// La sélection du texte en saisie, si c'est celle du bloc seul ; nulle hors saisie — la barre
    /// règle alors tous les caractères du bloc.
    /// </summary>
    private SelectionDuTexte? Selection =>
        Bloc is { } bloc && _vm.SelectionDuTexte is { Case: null } selection && ReferenceEquals(selection.Bloc, bloc)
            ? selection
            : null;

    /// <summary>La barre règle les caractères de la saisie, et non le bloc entier.</summary>
    public bool EnSaisie => Selection is not null;

    /// <summary>
    /// Les caractères visés : en saisie, la sélection, ou le mot du curseur, ou, hors d'un mot, le
    /// curseur seul (une plage vide) ; hors saisie, tout le texte du bloc. Nulle sans bloc de texte.
    /// </summary>
    public Plage? Caracteres
    {
        get
        {
            if (Bloc is not { } bloc) return null;
            var texte = bloc.Text ?? "";
            if (Selection is not { } selection) return new Plage(0, texte.Length);
            if (!selection.EstVide) return selection.Plage;
            return MotDuCurseur(texte, selection.Curseur) ?? new Plage(selection.Curseur, 0);
        }
    }

    /// <summary>Ce que la barre montre : ce que les caractères visés ont en commun.</summary>
    public StyleDeLaPlage Lire() =>
        Bloc is { } bloc && Caracteres is { } plage
            ? MiseEnForme.Lire(bloc.Text ?? "", bloc.Spans, MiseEnForme.Commun(bloc), plage, _vm.Document.Charte)
            : default;

    /// <summary>L'état d'une bascule sur les caractères visés : le <c>IsChecked</c> du bouton.</summary>
    public bool? Allume(Bascule bascule) =>
        Bloc is { } bloc && Caracteres is { } plage
            ? MiseEnForme.Allume(bloc.Text ?? "", bloc.Spans, MiseEnForme.Commun(bloc), plage, bascule)
            : null;

    /// <summary>La police ; nulle si les caractères visés en mêlent plusieurs.</summary>
    public string? FontFamily
    {
        get => Lire().FontFamily;
        set
        {
            if (value is not null && value != FontFamily) Poser(new StyleDeLaPlage { FontFamily = value });
        }
    }

    /// <summary>Le corps, en points ; nul s'il est mixte.</summary>
    public double? FontSize
    {
        get => Lire().FontSize;
        set
        {
            if (value is { } corps && corps != FontSize) Poser(new StyleDeLaPlage { FontSize = corps });
        }
    }

    /// <summary>Le gras : allumé, éteint, ou nul quand une partie seulement est grasse.</summary>
    public bool? Bold => Allume(Bascule.Gras);

    /// <summary>L'italique, de même.</summary>
    public bool? Italic => Allume(Bascule.Italique);

    /// <summary>Le souligné : sa sorte, si elle est commune, et le bouton S.</summary>
    public SouligneVise Underline => new(Lire().Underline, Allume(Bascule.Souligne));

    /// <summary>La couleur du texte ; nulle si elle est mixte, et la vignette reste blanche.</summary>
    public string? Foreground
    {
        get => Lire().Foreground;
        set
        {
            if (value is not null && value != Foreground) Poser(new StyleDeLaPlage { Foreground = value });
        }
    }

    // ------------------------------------------------------------------ écrire

    /// <summary>
    /// Pose sur les caractères visés chaque propriété que <paramref name="style"/> renseigne. Hors
    /// saisie, le bloc la prend, et les fragments qui en disaient une autre la perdent. En saisie,
    /// sans sélection ni mot au curseur, rien ne change. Rend vrai si le document a changé. La
    /// transaction est celle de l'appelant : le champ de la barre qui a le focus, le sélecteur de
    /// couleur ouvert, ou le geste (<c>UnGesteUnPas</c>). Une sélection de tout le texte vaut le
    /// bloc, comme hors saisie.
    /// </summary>
    internal bool Poser(StyleDeLaPlage style) => Ecrire((bloc, plage) =>
        TousLesCaracteres(bloc, plage)
            ? PoserSurLeBloc(bloc, style)
            : MiseEnForme.Poser(bloc.Text ?? "", bloc.Spans, MiseEnForme.Commun(bloc), plage, style));

    /// <summary>
    /// <c>Ctrl+G</c>, <c>Ctrl+I</c>, <c>Ctrl+U</c> : si tous les caractères visés portent la valeur,
    /// la retirer ; sinon, la poser partout — une sélection mixte prend le gras. Retirer pose la
    /// valeur éteinte (N2). Hors saisie, sur le bloc et tous ses fragments.
    /// </summary>
    internal bool Basculer(Bascule bascule) => Ecrire((bloc, plage) =>
    {
        var texte = bloc.Text ?? "";
        if (!TousLesCaracteres(bloc, plage)) return MiseEnForme.Basculer(texte, bloc.Spans, MiseEnForme.Commun(bloc), plage, bascule);

        var allume = MiseEnForme.Allume(texte, bloc.Spans, MiseEnForme.Commun(bloc), plage, bascule) == true;
        return PoserSurLeBloc(bloc, bascule switch
        {
            Bascule.Gras => new StyleDeLaPlage { Bold = !allume },
            Bascule.Italique => new StyleDeLaPlage { Italic = !allume },
            _ => new StyleDeLaPlage { Underline = allume ? Souligne.Aucun : Souligne.Simple },
        });
    });

    /// <summary>
    /// <c>Ctrl+Espace</c> : rend aux caractères visés le style du bloc — plus aucun fragment n'y dit
    /// rien, la langue mise à part (N9). Hors saisie, à tous les caractères du bloc.
    /// </summary>
    internal bool Retirer() => Ecrire((bloc, plage) =>
        MiseEnForme.Retirer(bloc.Text ?? "", bloc.Spans, MiseEnForme.Commun(bloc), plage));

    /// <summary>
    /// Le corps de chaque caractère visé passe à <paramref name="nouveau"/> du sien : <c>Ctrl+]</c> sur
    /// une sélection de 10 et de 14 pt donne 11 et 15, comme Word. Hors saisie, le bloc et chacun de
    /// ses fragments. Une taille de la liste (N19) est une fonction constante.
    /// </summary>
    internal bool ChangerLeCorps(Func<double, double> nouveau) => Ecrire((bloc, plage) =>
    {
        var texte = bloc.Text ?? "";
        // Les corps d'avant, par morceau : ce qui suit les change un à un.
        var morceaux = Corps(texte, bloc.Spans, MiseEnForme.Commun(bloc), plage);

        var change = false;
        if (TousLesCaracteres(bloc, plage))
        {
            var duBloc = nouveau(bloc.FontSize);
            if (duBloc != bloc.FontSize)
            {
                bloc.FontSize = duBloc;
                change = true;
            }
        }

        foreach (var (debut, fin, corps) in morceaux)
        {
            change |= MiseEnForme.Poser(texte, bloc.Spans, MiseEnForme.Commun(bloc), new Plage(debut, fin - debut),
                new StyleDeLaPlage { FontSize = nouveau(corps) });
        }

        return change;
    });

    /// <summary>
    /// Les morceaux de la plage d'un même corps : début, fin et corps, dans l'ordre du texte, lus sur
    /// le découpage même de la composition (<see cref="StyleEffectif.Decouper"/>).
    /// </summary>
    private static List<(int Debut, int Fin, double Corps)> Corps(
        string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun, Plage plage)
    {
        var morceaux = new List<(int Debut, int Fin, double Corps)>();
        var debutDeLaPlage = plage.Start;
        var finDeLaPlage = plage.Start + Math.Max(0, plage.Length);
        var position = 0;
        foreach (var (morceau, style) in StyleEffectif.Decouper(texte, fragments, commun))
        {
            var debut = Math.Max(position, debutDeLaPlage);
            var fin = Math.Min(position + morceau.Length, finDeLaPlage);
            position += morceau.Length;
            if (debut >= fin) continue;

            if (morceaux.Count > 0 && morceaux[^1].Fin == debut && morceaux[^1].Corps == style.FontSize)
            {
                morceaux[^1] = morceaux[^1] with { Fin = fin };
            }
            else
            {
                morceaux.Add((debut, fin, style.FontSize));
            }
        }

        return morceaux;
    }

    /// <summary>
    /// Les caractères visés sont tous ceux du bloc : hors saisie, ou, en saisie, une sélection de
    /// tout le texte (<c>Ctrl+A</c>). Un réglage va alors au bloc, et non à un fragment qui le
    /// couvrirait entier : le même texte, les mêmes caractères, le même document.
    /// </summary>
    private bool TousLesCaracteres(TextFrame bloc, Plage plage) =>
        !EnSaisie || (plage.Start == 0 && plage.Length > 0 && plage.Length == (bloc.Text ?? "").Length);

    /// <summary>
    /// Tous les caractères : le bloc prend chaque valeur, puis tous ses caractères — les fragments qui en
    /// disaient une autre la perdent, et un fragment qui ne dit plus rien disparaît. Le décalage de
    /// ligne de base n'est pas une propriété du bloc : il va aux fragments seuls.
    /// </summary>
    private bool PoserSurLeBloc(TextFrame bloc, StyleDeLaPlage style)
    {
        var avant = MiseEnForme.Commun(bloc);
        if (style.FontFamily is { } police) bloc.FontFamily = police;
        if (style.FontSize is { } corps) bloc.FontSize = corps;
        if (style.Bold is { } gras) bloc.Bold = gras;
        if (style.Italic is { } italique) bloc.Italic = italique;
        if (style.Underline is { } souligne) bloc.Underline = souligne;
        if (style.Foreground is { } couleur) bloc.Foreground = couleur;
        var commun = MiseEnForme.Commun(bloc);

        var texte = bloc.Text ?? "";
        var fragments = MiseEnForme.Poser(texte, bloc.Spans, commun, new Plage(0, texte.Length), style);
        return fragments || commun != avant;
    }

    private bool Ecrire(Func<TextFrame, Plage, bool> ecriture)
    {
        if (Bloc is not { } bloc || Caracteres is not { } plage || _enEcriture) return false;

        _enEcriture = true;
        try
        {
            return ecriture(bloc, plage);
        }
        finally
        {
            _enEcriture = false;
            Notifier();
        }
    }

    // ------------------------------------------------------------------ le mot du curseur

    /// <summary>
    /// Le mot où tombe le curseur : la suite de lettres et de chiffres qu'il coupe, avec une lettre
    /// de chaque côté. Au bord d'un mot — devant sa première lettre, après sa dernière —, dans un
    /// blanc ou contre une ponctuation, il n'y en a pas : Word met alors en forme la frappe qui
    /// suit, et MyPublisher, qui n'a pas de style en attente, ne met rien en forme. C'est une autre
    /// règle que celle du double-clic (N21), qui prend aussi un blanc ou un signe : on ne met pas
    /// une espace en gras parce que le curseur la touche.
    /// </summary>
    public static Plage? MotDuCurseur(string texte, int curseur)
    {
        if (curseur <= 0 || curseur >= texte.Length) return null;
        if (!DansUnMot(texte[curseur - 1]) || !DansUnMot(texte[curseur])) return null;

        var debut = curseur - 1;
        while (debut > 0 && DansUnMot(texte[debut - 1])) debut--;
        var fin = curseur + 1;
        while (fin < texte.Length && DansUnMot(texte[fin])) fin++;
        return new Plage(debut, fin - debut);
    }

    /// <summary>Une lettre, un chiffre, le souligné bas, une moitié de paire, un accent combinant.</summary>
    private static bool DansUnMot(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || char.IsSurrogate(c) ||
        char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;

    // ------------------------------------------------------------------ ce qu'il suit

    /// <summary>
    /// Côté modèle de vue : un autre bloc seul — une autre sélection, l'annulation qui relit le
    /// document —, la sélection du texte, qui suit le curseur, et le jeu du document, qui résout la
    /// police et la couleur que la barre compare.
    /// </summary>
    private void SurModeleDeVue(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.SingleSelected):
                SuivreLeBloc();
                break;
            case nameof(MainWindowViewModel.SelectionDuTexte) or nameof(MainWindowViewModel.ChosenColorScheme)
                or nameof(MainWindowViewModel.ChosenFontScheme):
                Notifier();
                break;
        }
    }

    /// <summary>Suit le bloc seul : son style, son texte — la plage visée en dépend — et ses fragments.</summary>
    private void SuivreLeBloc()
    {
        if (_blocSuivi is not null)
        {
            _blocSuivi.PropertyChanged -= SurBlocRetouche;
            _blocSuivi.Spans.CollectionChanged -= SurFragmentsRetouches;
        }

        _blocSuivi = Bloc;

        if (_blocSuivi is not null)
        {
            _blocSuivi.PropertyChanged += SurBlocRetouche;
            _blocSuivi.Spans.CollectionChanged += SurFragmentsRetouches;
        }

        Notifier();
    }

    private void SurBlocRetouche(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TextFrame.Text) or nameof(TextFrame.FontFamily) or nameof(TextFrame.FontSize)
                or nameof(TextFrame.Bold) or nameof(TextFrame.Italic) or nameof(TextFrame.Underline)
                or nameof(TextFrame.Foreground):
                Notifier();
                break;
        }
    }

    private void SurFragmentsRetouches(object? sender, NotifyCollectionChangedEventArgs e) => Notifier();

    private void Notifier()
    {
        if (_enEcriture) return;
        foreach (var propriete in Proprietes) OnPropertyChanged(propriete);
    }
}

/// <summary>
/// Ce que le poste R7 ajoute au modèle de vue — éditer le texte riche à la souris. Un fichier à
/// lui, comme la règle 10 de la phase I le veut.
/// </summary>
public partial class MainWindowViewModel
{
    private MiseEnFormeVisee? _miseEnFormeVisee;

    /// <summary>
    /// Les caractères que règlent la police, le corps, le gras, l'italique, le souligné et la
    /// couleur : la sélection du texte, ou le mot du curseur, en saisie ; tout le bloc hors saisie.
    /// La barre s'y lie.
    /// </summary>
    public MiseEnFormeVisee MiseEnFormeVisee => _miseEnFormeVisee ??= new MiseEnFormeVisee(this);

    /// <summary>
    /// La mise en forme que <c>Ctrl+Maj+C</c> a copiée, et que <c>Ctrl+Maj+V</c> pose : ce que les
    /// caractères visés avaient en commun, sans ce qu'ils avaient de mixte, et sans la langue.
    /// L'état de la session, hors du document et de l'historique.
    /// </summary>
    public StyleDeLaPlage? MiseEnFormeCopiee { get; private set; }

    /// <summary>
    /// Met en forme les caractères visés d'un bloc de texte, en un pas d'annulation, où que soit le
    /// focus (règle 2) : la frappe en cours garde son pas, et la saisie continue. Faux si le bloc
    /// seul n'est pas un bloc de texte : l'appelant fait alors ce qu'il faisait — un tableau garde
    /// son gras de tableau.
    /// </summary>
    private bool MettreEnFormeLeTexte(Func<MiseEnFormeVisee, bool> geste, Func<bool, string> message)
    {
        if (SingleSelected is not TextFrame) return false;

        var visee = MiseEnFormeVisee;
        if (visee.EnSaisie && visee.Caracteres is { Length: 0 })
        {
            Status = "Rien à mettre en forme : sélectionnez du texte, ou placez le curseur dans un mot.";
            return true;
        }

        var change = false;
        UnGesteUnPas(() => change = geste(visee));
        Status = message(change);
        return true;
    }

    /// <summary>Ce que la barre d'état dit d'une bascule : « Gras » ou « Gras retiré », dans la saisie « … : 3 caractères ».</summary>
    private string DireLaBascule(Bascule bascule, string nom)
    {
        var allume = MiseEnFormeVisee.Allume(bascule) == true;
        var portee = MiseEnFormeVisee is { EnSaisie: true, Caracteres: { Length: var n } } ? $" : {n} caractère{(n > 1 ? "s" : "")}" : "";
        return (allume ? nom : $"{nom} retiré") + portee;
    }

    /// <summary>
    /// <c>Ctrl+G</c>, <c>Ctrl+I</c>, <c>Ctrl+U</c> et leurs boutons, sur un bloc de texte : la
    /// bascule des caractères visés. Faux pour un autre bloc.
    /// </summary>
    private bool BasculerLeTexte(Bascule bascule, string nom) =>
        MettreEnFormeLeTexte(visee => visee.Basculer(bascule), _ => DireLaBascule(bascule, nom));

    /// <summary>
    /// <c>Ctrl+Espace</c> : rend aux caractères visés le style du bloc (fiche 24, « Appliquer à la
    /// mise en forme des caractères le style de texte actuel »).
    /// </summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void RetirerLaMiseEnForme() =>
        MettreEnFormeLeTexte(visee => visee.Retirer(),
            change => change ? "Mise en forme des caractères retirée" : "Aucune mise en forme des caractères à retirer");

    /// <summary>
    /// <c>Ctrl+Maj+C</c> : copie la mise en forme des caractères visés — en saisie, la sélection, le
    /// mot du curseur, ou le caractère qui le précède ; hors saisie, ce que tout le bloc a en
    /// commun. Rien n'entre au document.
    /// </summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void CopierLaMiseEnForme()
    {
        if (SingleSelected is not TextFrame) return;

        MiseEnFormeCopiee = MiseEnFormeVisee.Lire() with { Langue = null };
        Status = "Mise en forme copiée";
    }

    /// <summary>
    /// <c>Ctrl+Maj+V</c> : pose la mise en forme copiée sur les caractères visés, en un pas. Sans
    /// rien de copié, rien ne change, et la barre d'état le dit.
    /// </summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void CollerLaMiseEnForme()
    {
        if (SingleSelected is not TextFrame) return;
        if (MiseEnFormeCopiee is not { } copiee)
        {
            Status = "Aucune mise en forme copiée : Ctrl+Maj+C la copie.";
            return;
        }

        MettreEnFormeLeTexte(visee => visee.Poser(copiee),
            change => change ? "Mise en forme appliquée" : "La mise en forme est déjà celle-là");
    }
}
