using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace MyPublisher.Views;

/// <summary>
/// Les lettres d'accès des menus, cherchées au seul niveau ouvert, comme Windows — poste N13,
/// repris le 29 octobre 2026 sur le choix du propriétaire.
///
/// Avalonia 11.3.9 ne range pas les lettres par niveau (<c>AccessKeyHandler.ProcessKey</c>) :
/// toute lettre visible compte, la barre — qui reste à l'écran — et chaque niveau ouvert. Deux
/// cibles, et la touche ne fait que promener le focus : Disposition puis Ordre ouverts, P désignait
/// « _Page », « Re_pères de règle » et « Premier _plan », et ne jouait rien. Il compare aussi la
/// lettre au nom de la touche, si bien que « _1 », le premier document récent (N11), attendait
/// « D1 ».
///
/// La barre de menus ouverte, <see cref="JouerLaLettreDuMenu"/> prend la frappe avant lui : la
/// lettre — le caractère produit, ou le chiffre de la rangée des chiffres, que l'AZERTY donne sans
/// Maj — se cherche parmi les seules entrées du niveau ouvert le plus profond, ou de la barre si
/// aucun menu n'est déroulé. Une entrée trouvée seule s'active : son sous-menu s'ouvre, sa
/// commande se joue et les menus se referment, comme au clic. Plusieurs, le focus passe de l'une
/// à l'autre. Aucune, la frappe est prise et rien ne se fait — elle n'arme pas d'outil.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// La barre de menus ouverte, joue la lettre d'accès de la frappe au niveau ouvert, et dit si
    /// la frappe était une lettre ou un chiffre, sans <c>Ctrl</c> : la fenêtre la tient alors pour
    /// prise. Toute autre touche — flèches, Entrée, Échap — revient aux menus.
    /// </summary>
    private bool JouerLaLettreDuMenu(KeyEventArgs e)
    {
        if (!Menus.IsOpen || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0) return false;
        if (LettreDeLaFrappe(e) is not { } lettre) return false;

        var trouvees = EntreesDuNiveauOuvert()
            .Where(entree => entree.Header is string entete && LettreDAcces(entete) == lettre)
            .ToList();

        if (trouvees.Count > 1)
        {
            // Comme Windows : la lettre partagée promène le focus, sans rien jouer.
            var focus = trouvees.FindIndex(entree => entree.IsFocused);
            trouvees[(focus + 1) % trouvees.Count].Focus(NavigationMethod.Tab);
            return true;
        }

        if (trouvees.Count == 1) Activer(trouvees[0]);
        return true;
    }

    /// <summary>
    /// Les entrées du niveau ouvert le plus profond : on descend, depuis la barre, d'entrée
    /// déroulée en entrée déroulée.
    /// </summary>
    private List<MenuItem> EntreesDuNiveauOuvert()
    {
        IEnumerable<MenuItem> niveau = Menus.Items.OfType<MenuItem>();
        while (niveau.FirstOrDefault(entree => entree.IsSubMenuOpen) is { } ouverte)
        {
            niveau = ouverte.Items.OfType<MenuItem>();
        }

        return niveau.Where(entree => entree.IsVisible).ToList();
    }

    /// <summary>
    /// Ce que ferait le clic, comme <c>DefaultMenuInteractionHandler.Click</c> et <c>Open</c> le
    /// font : un sous-menu s'ouvre, le focus sur sa première entrée ; une entrée se coche si elle
    /// est une case, lève son clic — qui joue sa commande —, et les menus se referment. Une entrée
    /// grisée reçoit le focus et ne joue rien.
    /// </summary>
    private void Activer(MenuItem entree)
    {
        if (!entree.IsEffectivelyEnabled)
        {
            entree.Focus(NavigationMethod.Tab);
            return;
        }

        if (entree.ItemCount > 0)
        {
            entree.Open();
            entree.Focus(NavigationMethod.Tab);
            // Le sous-menu ne se montre qu'à la mise en page suivante : sa première entrée ne
            // prend le focus qu'alors.
            Dispatcher.UIThread.Post(() =>
            {
                if (!entree.IsSubMenuOpen) return;
                entree.Items.OfType<MenuItem>().FirstOrDefault(m => m.IsVisible && m.IsEffectivelyEnabled)
                    ?.Focus(NavigationMethod.Tab);
            }, DispatcherPriority.Loaded);
            return;
        }

        if (entree.ToggleType == MenuItemToggleType.CheckBox) entree.IsChecked = !entree.IsChecked;
        else if (entree.ToggleType == MenuItemToggleType.Radio && !entree.IsChecked) entree.IsChecked = true;

        entree.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        if (!entree.StaysOpenOnClick) Menus.Close();
    }

    /// <summary>
    /// La lettre que la frappe désigne, en capitale : le chiffre d'une touche de la rangée des
    /// chiffres ou du pavé — un AZERTY y donne « & » ou « é » sans Maj, et la rangée livre la
    /// touche de sa place (relevé de la sonde, N10) —, sinon le caractère produit, sinon la lettre
    /// de la touche. <c>null</c> pour tout ce qui n'est ni lettre ni chiffre.
    /// </summary>
    private static char? LettreDeLaFrappe(KeyEventArgs e)
    {
        if (e.Key is >= Key.D0 and <= Key.D9) return (char)('0' + (e.Key - Key.D0));
        if (e.Key is >= Key.NumPad0 and <= Key.NumPad9) return (char)('0' + (e.Key - Key.NumPad0));
        if (e.KeySymbol is { Length: 1 } symbole && char.IsLetterOrDigit(symbole[0])) return char.ToUpperInvariant(symbole[0]);
        if (e.Key is >= Key.A and <= Key.Z) return (char)('A' + (e.Key - Key.A));
        return null;
    }

    /// <summary>
    /// La lettre d'accès d'un libellé, en capitale, comme Avalonia la lit : le caractère qui suit
    /// le premier « _ » seul — « __ » écrit un trait. <c>null</c> sans lettre d'accès.
    /// </summary>
    internal static char? LettreDAcces(string entete)
    {
        for (var i = 0; i < entete.Length - 1; i++)
        {
            if (entete[i] != '_') continue;
            if (entete[i + 1] == '_') { i++; continue; }
            return char.ToUpperInvariant(entete[i + 1]);
        }

        return null;
    }
}
