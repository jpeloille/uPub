using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que la barre contextuelle (phase I, poste I3) ajoute au modèle de vue. Un fichier à
/// lui, comme la règle 10 de la phase le veut : I5 et I6 partent en même temps et ajoutent
/// les leurs sans toucher à celui-ci.
///
/// Presque rien : la barre se lie au bloc sélectionné comme le dock s'y liait — la police,
/// le corps, l'interligne, la couleur du texte — et joue les commandes que le menu Texte
/// joue déjà — le gras, l'italique, les alignements (I2), le souligné (I7). Il ne lui
/// manque que les sortes du souligné, que son menu déroulant propose.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Le souligné du modèle qu'une sorte du menu déroulant du bouton S désigne : toujours
    /// un trait plein. Les quatre réglages du modèle (<see cref="Souligne"/>) composent bien
    /// d'autres sortes — un pointillé double, une vague —, qu'un <c>.pub</c> importé apporte
    /// et que le rendu dessine ; la barre n'offre que les quatre que le plan nomme, et qui
    /// se disent d'un mot.
    /// </summary>
    public static Souligne SouligneDe(SorteDeSouligne sorte) => sorte switch
    {
        SorteDeSouligne.Double => new Souligne(TraitDeSouligne.Solid, @double: true),
        SorteDeSouligne.Epais => new Souligne(TraitDeSouligne.Solid, thick: true),
        SorteDeSouligne.MotsSeuls => new Souligne(TraitDeSouligne.Solid, wordsOnly: true),
        _ => Souligne.Simple,
    };

    /// <summary>Le nom d'une sorte, au menu déroulant et dans la barre d'état.</summary>
    public static string LibelleDe(SorteDeSouligne sorte) => sorte switch
    {
        SorteDeSouligne.Double => "Double",
        SorteDeSouligne.Epais => "Épais",
        SorteDeSouligne.MotsSeuls => "Mots seuls",
        _ => "Trait simple",
    };

    /// <summary>
    /// Souligne le bloc de texte sélectionné de la sorte choisie au menu déroulant du
    /// bouton S, en un pas d'annulation, où que soit le focus — la même règle que
    /// « Souligné » (<see cref="Souligner"/>), dont le bouton lui-même et <c>Ctrl+U</c>
    /// restent la bascule : retirer le souligné quelle qu'en soit la sorte, ou poser le
    /// trait simple. Choisir la sorte que le bloc porte déjà ne fait pas de pas : ce n'est
    /// pas un changement.
    /// </summary>
    [RelayCommand]
    private void SoulignerEn(SorteDeSouligne sorte)
    {
        if (SingleSelected is not TextFrame bloc)
        {
            Status = "Souligné : sélectionnez un seul bloc de texte.";
            return;
        }

        var souligne = SouligneDe(sorte);
        if (bloc.Underline == souligne) return;

        UnGesteUnPas(() => bloc.Underline = souligne);
        Status = $"Souligné : {LibelleDe(sorte).ToLowerInvariant()}";
    }
}

/// <summary>Une entrée du menu déroulant du souligné.</summary>
public enum SorteDeSouligne { Simple, Double, Epais, MotsSeuls }
