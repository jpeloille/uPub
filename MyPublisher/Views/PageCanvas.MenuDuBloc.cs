using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Le menu contextuel du bloc (poste A12, fiche 11 ; fiche 23, c3, c16, c25, e1, e2, f16). Le geste
/// réflexe d'un utilisateur d'Office : un clic droit sur un objet le sélectionne et ouvre un menu
/// qui commence par Couper, Copier, Coller et mène au dialogue de format (p. 34). Aucun menu
/// contextuel n'existait sur la planche ; la zone de saisie montrait celui du thème.
///
///   - <b>La pression du bouton droit</b> suit le chemin de celle du gauche jusqu'à la
///     sélection, puis s'arrête, sans glissé (<see cref="PrendreAuBoutonDroit"/>) : un bloc hors
///     de la sélection est pris seul, une sélection qui contient le bloc reste, un clic dans le
///     vide la vide. Elle sort aussi de la saisie, comme celle du gauche (N5).
///   - <b>Le menu est celui de la planche</b> (<c>MenuDuBloc</c>, écrit dans MainWindow.axaml à
///     côté des menus, dont il reprend les commandes) : Avalonia lève sa demande au relâché du
///     bouton droit, sur le contrôle atteint, et elle remonte jusqu'à la planche — depuis la
///     couche de saisie, ou depuis le texte en saisie, dont la zone n'a plus son menu à elle. La
///     touche Menu la lève sur le contrôle qui a le focus ; <c>Maj+F10</c>, qu'Avalonia ne lit pas,
///     est lu ici. Au clavier, le menu s'ouvre sous la sélection.
///   - <b>Il se tait sur les règles et les barres de défilement</b> : la demande qui en vient est
///     prise au passage descendant, avant que le menu ne l'entende.
///   - <b>Ses entrées suivent la sélection</b> : le texte en saisie garde Couper, Copier, Coller
///     et Format du bloc… ; des blocs pris, tout le menu, avec Supprimer le texte et Modifier le
///     texte ▸ pour un bloc de texte seul, et la balise du bloc de coordonnées ; le vide, Coller,
///     les repères de mise en page et l'arrière-plan.
///   - <b>Un double-clic sur une image ou une forme</b> ouvre sa section du dock, comme Format du
///     bloc… (décision 5).
/// </summary>
public partial class PageCanvas
{
    /// <summary>Où paraît chaque entrée du menu : les classes qu'elle porte dans MainWindow.axaml.</summary>
    private const string EnSaisie = "saisie", SurDesBlocs = "blocs", DansLeVide = "vide",
        SiTexte = "siTexte", SiCoordonnees = "siCoordonnees";

    /// <summary>
    /// La zone de saisie dont le menu ouvert garde la sélection : un <c>TextBox</c> qui perd le
    /// focus vide sa sélection, sauf quand son propre menu s'ouvre — et ce menu-ci n'est pas le
    /// sien. Nulle hors d'un menu ouvert dans le texte en saisie.
    /// </summary>
    private TextBox? _zoneSousLeMenu;

    /// <summary>Le menu attaché : celui dont la fermeture est suivie.</summary>
    private ContextMenu? _menuSuivi;

    /// <summary>Branche la demande du menu et sa fermeture ; appelé une fois, par le constructeur.</summary>
    private void BrancherLeMenuDuBloc()
    {
        // Au passage descendant : la planche voit la demande avant le menu, qui l'entend en
        // montant, et avant tout contrôle qu'elle traverse.
        AddHandler(ContextRequestedEvent, PreparerLeMenu, RoutingStrategies.Tunnel);

        // Le menu est posé après la construction de la planche, par la fenêtre qui l'écrit.
        PropertyChanged += (_, e) =>
        {
            if (e.Property != ContextMenuProperty) return;
            if (_menuSuivi is not null) _menuSuivi.Closed -= OnMenuDuBlocFerme;
            _menuSuivi = e.NewValue as ContextMenu;
            if (_menuSuivi is not null) _menuSuivi.Closed += OnMenuDuBlocFerme;
        };
    }

    /// <summary>
    /// La pression du bouton droit, arrivée à la sélection par le chemin du gauche : le bloc
    /// <paramref name="bloc"/> atteint, ou rien. Elle s'arrête là — ni glissé, ni saisie, ni
    /// rectangle — et le relâché ouvrira le menu.
    /// </summary>
    private void PrendreAuBoutonDroit(Frame? bloc)
    {
        if (_vm is null) return;

        if (bloc is null) _vm.SetSelection([]);
        else if (!_vm.Selection.Contains(bloc)) _vm.SelectOnly(bloc);
    }

    /// <summary>
    /// <c>Maj+F10</c> ouvre le menu sous la sélection, comme la touche Menu, la seule qu'Avalonia
    /// lise (<c>PlatformHotkeyConfiguration.OpenContextMenu</c>). La planche l'ouvre elle-même,
    /// préparé comme pour une demande : aucun événement n'est rejoué (N5). La fenêtre retient le
    /// champ sous le menu à la pression de <c>F10</c>, comme pour la barre de menus (N8).
    /// </summary>
    private bool OuvrirLeMenuAuClavier(KeyEventArgs e)
    {
        if (e.Key != Key.F10 || e.KeyModifiers != KeyModifiers.Shift || ContextMenu is not { } menu || menu.IsOpen) return false;
        _caractereSousLeMenu = null;
        if (!Preparer(menu, this, auClavier: true)) return false;

        // Ouvert sous la place que Preparer a posée ; la demande du pointeur garde la sienne.
        var auPointeur = menu.Placement;
        menu.Placement = PlacementMode.Bottom;
        menu.Open(this);
        menu.Placement = auPointeur;
        return true;
    }

    /// <summary>
    /// Une demande du menu traverse la planche. Venue d'une règle, d'une barre de défilement ou
    /// du coin entre les règles, elle s'arrête ici. Venue de la couche, de la planche ou du texte
    /// en saisie, elle prépare le menu — ses entrées, la disponibilité des neuves, sa place — et
    /// continue jusqu'à lui.
    /// </summary>
    private void PreparerLeMenu(object? sender, ContextRequestedEventArgs e)
    {
        if (_vm is null || ContextMenu is not { } menu) return;

        var source = e.Source as Visual;
        var dansLeTexte = _editing is not null && source is not null &&
                          source.GetSelfAndVisualAncestors().OfType<TextBox>().Any(t => ReferenceEquals(t.DataContext, _editing));
        if (!ReferenceEquals(source, InputLayer) && !ReferenceEquals(source, this) && !dansLeTexte)
        {
            e.Handled = true;
            return;
        }

        RetenirLeCaractereSousLeMenu(e);
        if (!Preparer(menu, source as Control ?? this, auClavier: !e.TryGetPosition(null, out _))) e.Handled = true;
    }

    /// <summary>
    /// Prépare le menu avant qu'il ne s'ouvre : ses entrées selon la sélection, la disponibilité
    /// des neuves, la sélection du texte en saisie gardée, sa place. Au pointeur, le menu s'ouvre
    /// sous lui ; au clavier, Avalonia le poserait sous la <paramref name="cible"/>, la planche
    /// entière : il se pose sous la sélection, lue dans le repère de la cible. Faux sans modèle.
    /// </summary>
    private bool Preparer(ContextMenu menu, Control cible, bool auClavier)
    {
        if (_vm is null) return false;

        var contexte = _editing is not null ? EnSaisie : _vm.HasSelection ? SurDesBlocs : DansLeVide;
        var texte = _vm.SingleSelected is TextFrame;
        var coordonnees = _vm.EstUnBlocDeCoordonnees;
        _vm.NotifierLeMenuDuBloc();
        // Les suggestions d'une faute, en tête du menu du texte (N25).
        PoserLesSuggestions(menu, contexte);
        MontrerLesEntrees(menu, contexte, texte, coordonnees);

        // Le texte en saisie garde sa sélection pendant que le menu a le focus : Copier la lira.
        if (contexte == EnSaisie && _zoneDeSaisie is { } zone)
        {
            zone.ClearSelectionOnLostFocus = false;
            _zoneSousLeMenu = zone;
        }

        menu.PlacementRect = auClavier ? PlaceAuClavier(cible) : null;
        return true;
    }

    /// <summary>
    /// Montre les entrées du <paramref name="contexte"/>, celles qu'une condition retient si elle
    /// tient, et les séparateurs qui séparent encore quelque chose.
    /// </summary>
    private static void MontrerLesEntrees(ContextMenu menu, string contexte, bool texte, bool coordonnees)
    {
        var entrees = menu.Items.OfType<Control>().ToList();
        foreach (var entree in entrees)
        {
            entree.IsVisible = entree.Classes.Contains(contexte) &&
                               (texte || !entree.Classes.Contains(SiTexte)) &&
                               (coordonnees || !entree.Classes.Contains(SiCoordonnees));
        }

        // Un séparateur en tête, en queue, ou à la suite d'un autre ne sépare rien.
        Control? precedent = null;
        foreach (var entree in entrees.Where(e => e.IsVisible))
        {
            if (entree is Separator && precedent is null or Separator) entree.IsVisible = false;
            else precedent = entree;
        }

        if (precedent is Separator) precedent.IsVisible = false;
    }

    /// <summary>
    /// Où se pose le menu ouvert au clavier, dans le repère de <paramref name="cible"/> : sous la
    /// sélection, son emprise sur la page courante ; sans rien de pris, au milieu de la planche
    /// visible.
    /// </summary>
    private Rect? PlaceAuClavier(Control cible)
    {
        var page = _vm?.CurrentPage;
        var emprise = _vm is null || page is null
            ? null
            : Extent.Union(_vm.Selection.Where(f => f.Visible && page.Frames.Contains(f)).Select(f => f.Extent));

        if (emprise is { } e &&
            Surface.TranslatePoint(new Point(e.Left * Scale, e.Top * Scale), cible) is { } haut &&
            Surface.TranslatePoint(new Point(e.Right * Scale, e.Bottom * Scale), cible) is { } bas)
        {
            return new Rect(haut, bas);
        }

        var milieu = Scroller.TranslatePoint(new Point(Scroller.Bounds.Width / 2, Scroller.Bounds.Height / 2), cible);
        return milieu is { } m ? new Rect(m, new Size(0, 0)) : null;
    }

    /// <summary>
    /// Le menu s'est refermé. La zone en saisie retrouve sa règle, et le focus revient où il
    /// était — à la zone en saisie, ou à la planche —, à moins qu'une entrée ne l'ait mené
    /// ailleurs : Format du bloc… le donne au dock.
    /// </summary>
    private void OnMenuDuBlocFerme(object? sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, _menuSuivi)) return;

        _zoneSousLeMenu?.ClearValue(TextBox.ClearSelectionOnLostFocusProperty);
        _zoneSousLeMenu = null;

        var focus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focus is not null && focus.IsAttachedToVisualTree() && !focus.GetSelfAndVisualAncestors().Any(v => v is MenuBase or MenuItem))
        {
            return;
        }

        if (_editing is not null && _zoneDeSaisie is { IsEffectivelyVisible: true } zone) zone.Focus();
        else Focus();
    }

    /// <summary>
    /// Un double-clic sur une image ou une forme ouvre sa section du dock, comme Format du bloc…
    /// (décision 5 ; fiche 23, c16) : le second clic ne déplace rien, et le bloc est pris seul.
    /// Vrai s'il l'a ouverte. Un texte garde son double-clic, qui y prend un mot (N12).
    /// </summary>
    private bool OuvrirSaSection(Frame bloc, PointerPressedEventArgs e)
    {
        if (_vm is null || e.ClickCount != 2 || bloc is not (ImageFrame or ShapeFrame)) return false;

        if (_vm.Selection.Count != 1 || !_vm.Selection.Contains(bloc)) _vm.SelectOnly(bloc);
        _vm.FormatDuBlocCommand.Execute(null);
        return true;
    }
}
