using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using MyPublisher.Models;
using MyPublisher.Services;

namespace MyPublisher.Views;

/// <summary>
/// Les suggestions du clic droit sur une faute (poste N25 ; fiche 23, a26). Dans le texte en
/// saisie, un clic droit sur un mot souligné — ou la touche Menu et <c>Maj+F10</c>, le curseur
/// dans le mot ou à son bout — ouvre le menu du texte (A12) avec, en tête, ce que le dictionnaire
/// propose à la place du mot, puis <i>Ignorer tout</i> et <i>Ajouter au dictionnaire</i>, comme
/// le menu de Publisher. Une suggestion remplace le mot en un pas, le curseur après lui.
///
/// Ces entrées ne sont pas écrites dans <c>MainWindow.axaml</c> : elles changent avec le mot. La
/// planche les pose en préparant le menu (<see cref="PoserLesSuggestions"/>) et les retire à la
/// préparation suivante ; elles portent la classe du texte en saisie, que
/// <c>MontrerLesEntrees</c> garde. Les fautes sont celles que la page souligne : la vérification
/// pendant la frappe fermée, le menu ne propose rien.
/// </summary>
public partial class PageCanvas
{
    /// <summary>La marque des entrées posées pour une faute, qu'on retire au menu suivant.</summary>
    private static readonly object MarqueDesSuggestions = new();

    /// <summary>
    /// Le caractère du texte en saisie sous le pointeur qui a demandé le menu, ou rien — au
    /// clavier, ou hors du texte : on lit alors le curseur.
    /// </summary>
    private int? _caractereSousLeMenu;

    /// <summary>
    /// Une demande du menu traverse la planche : on retient le caractère du texte en saisie sous le
    /// pointeur, lu sur la composition, pour y chercher une faute.
    /// </summary>
    private void RetenirLeCaractereSousLeMenu(ContextRequestedEventArgs e)
    {
        _caractereSousLeMenu = null;
        if (_editing is not { } bloc || TexteComposeDe(bloc) is not { } compose || !e.TryGetPosition(compose, out var point))
        {
            return;
        }

        if (compose.Geometrie() is { } geometrie) _caractereSousLeMenu = geometrie.CaractereSous(point);
    }

    /// <summary>
    /// Pose en tête du menu les suggestions de la faute visée, dans le texte en saisie : au
    /// pointeur, celle qui couvre le caractère sous lui ; au clavier, celle où est le curseur.
    /// Retire celles de la préparation d'avant.
    /// </summary>
    private void PoserLesSuggestions(ContextMenu menu, string contexte)
    {
        foreach (var entree in menu.Items.OfType<Control>().Where(e => ReferenceEquals(e.Tag, MarqueDesSuggestions)).ToList())
        {
            menu.Items.Remove(entree);
        }

        if (_vm is null || contexte != EnSaisie || _editing is not { } bloc || _zoneDeSaisie is not { } zone) return;

        var visee = _caractereSousLeMenu is { } caractere
            ? _vm.FauteEn(bloc, caractere, boutCompris: false)
            : _vm.FauteEn(bloc, zone.CaretIndex, boutCompris: true);
        if (visee is not { } faute) return;

        var rang = 0;
        void Poser(Control entree)
        {
            entree.Tag = MarqueDesSuggestions;
            entree.Classes.Add(EnSaisie);
            menu.Items.Insert(rang++, entree);
        }

        var suggestions = _vm.SuggestionsPour(faute);
        foreach (var suggestion in suggestions)
        {
            var entree = new MenuItem { Header = suggestion.Replace("_", "__"), FontWeight = FontWeight.SemiBold };
            entree.Click += (_, _) => Corriger(bloc, faute, suggestion);
            Poser(entree);
        }

        if (suggestions.Count == 0) Poser(new MenuItem { Header = "(Aucune suggestion)", IsEnabled = false });

        var ignorer = new MenuItem { Header = "_Ignorer tout" };
        ignorer.Click += (_, _) => _vm?.IgnorerToutLeMot(faute);
        Poser(ignorer);

        var ajouter = new MenuItem { Header = "Ajouter au _dictionnaire" };
        ajouter.Click += (_, _) => _vm?.AjouterLeMot(faute);
        Poser(ajouter);

        Poser(new Separator());
    }

    /// <summary>
    /// Une suggestion choisie : le mot fautif est remplacé, en un pas, et le curseur se pose après
    /// le mot remplacé, une fois que le champ a reçu le texte neuf.
    /// </summary>
    private void Corriger(TextFrame bloc, FauteDOrthographe faute, string par)
    {
        if (_vm?.CorrigerLaFaute(bloc, faute, par) is not { } apres) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_editing, bloc) || _zoneDeSaisie is not { } zone) return;

            var position = Math.Clamp(apres, 0, zone.Text?.Length ?? 0);
            PoserLaSelection(zone, position, position);
        }, DispatcherPriority.Background);
    }
}
