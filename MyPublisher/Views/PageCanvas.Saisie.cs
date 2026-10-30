using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Entrer dans le texte d'un clic, le curseur au point visé (poste N12, fiche 10 ; fiche 23, a2
/// à a5 et a10) — la décision 3 du propriétaire, à l'ouverture de la phase N.
///
/// Un bloc de texte ne s'ouvrait qu'au double-clic, et la saisie sélectionnait alors tout son
/// texte ; le curseur était la croix de déplacement sur toute sa surface. Publisher pose le point
/// d'insertion au premier clic, à l'endroit visé (p. 33). La planche distingue désormais deux
/// zones dans un bloc de texte visible et sans champ :
///
///   - **le texte**, l'intérieur du bloc moins une bande de bord : une pression y prend le bloc
///     seul et ouvre la saisie, le curseur sur le caractère visé, calculé sur la composition qui
///     était à l'écran (<see cref="TexteCompose.IndexSous"/>, poste N3) ; un glissé y sélectionne
///     du texte, depuis cette ancre, une fois franchi le seuil du glissé (Q21) ;
///   - **la bande de bord**, <see cref="BandeDeBord"/> pixels d'écran, au plus un quart du côté,
///     et pour un bloc sélectionné tout ce qui va jusqu'à son cadre de sélection : elle prend et
///     déplace le bloc comme avant, et tout objet qui n'est pas du texte avec elle.
///
/// <c>Ctrl</c>+clic et <c>Maj</c>+clic, eux, ajoutent ou retirent le bloc sans ouvrir la saisie.
/// Un bloc neuf est vide, le curseur dedans, qu'il vienne de l'outil ou du menu Insertion.
///
/// **La saisie montre le texte composé** (poste N21, la voie 2a que le propriétaire a choisie le
/// 26 octobre 2026 sur la mesure de N7 ; fiche 23, a10, a11, a20 et a21). Le champ en texte brut
/// remplaçait la composition pendant la saisie : 471 pt d'écart au plus entre son curseur et le
/// caractère composé. Désormais la composition reste à l'écran, et le champ, caché et hors du
/// test de survol, ne fait plus que la frappe, la méthode d'entrée, le presse-papiers et la
/// navigation au clavier. La couche de saisie garde tous les clics, texte en saisie compris
/// (<see cref="PressionDansLaSaisie"/>) : un clic y pose le curseur, <c>Maj</c>+clic étend la
/// sélection, le double-clic prend un mot, le triple-clic le paragraphe, et le glissé étend par
/// caractère, par mot ou par paragraphe — tout lu sur la composition (<see cref="GeometrieDuTexte"/>).
/// Le curseur, la sélection et le pré-édit se dessinent sur elle, dans l'<c>Overlay</c>
/// (<see cref="MarquesDuTexte"/>, <see cref="PoserLesMarquesDuTexte"/>).
///
/// La planche dit aussi au modèle de vue ce qui est sélectionné dans le texte en saisie (poste
/// N15 ; fiche 23, b1 et b4) : <see cref="MainWindowViewModel.SelectionDuTexte"/>, que R6, R7,
/// N20 et N21 liront. Voir <see cref="SuivreLaZoneDeSaisie"/>.
/// </summary>
public partial class PageCanvas
{
    /// <summary>
    /// La bande de bord d'un bloc de texte, en pixels d'écran : on y prend et déplace le bloc, et
    /// non son texte. Au plus un quart du côté, pour qu'un bloc étroit garde du texte où cliquer.
    /// </summary>
    private const double BandeDeBord = 5;

    private static readonly Cursor TexteCursor = new(StandardCursorType.Ibeam);

    /// <summary>
    /// La position du texte où la pression a ouvert la saisie : l'ancre de la sélection que le
    /// glissé étend.
    /// </summary>
    private int _ancreDuTexte;

    /// <summary>Ce que le glissé dans le texte étend : des caractères, des mots ou des paragraphes (N21).</summary>
    private enum UniteDuTexte { Caractere, Mot, Paragraphe }

    private UniteDuTexte _uniteDuGlisse;

    /// <summary>
    /// Le mot ou le paragraphe que le double ou le triple clic a pris : le glissé qui suit l'étend
    /// sans jamais le perdre, comme dans un traitement de texte (N21).
    /// </summary>
    private (int Debut, int Fin) _priseDuTexte;

    /// <summary>
    /// La géométrie du texte pendant un glissé, prise à la pression : le texte ne change pas
    /// sous un glissé, et la recomposer à chaque mouvement du pointeur coûterait jusqu'à 15 ms
    /// sur un bloc d'ATA 21 (relevé de N3).
    /// </summary>
    private GeometrieDuTexte? _geometrieDuGlisse;

    /// <summary>
    /// Le point de la page, en points, tombe-t-il dans le texte du bloc — son intérieur moins la
    /// bande de bord, lu dans le repère du bloc, rotation comprise ? Un bloc masqué ou à champ
    /// n'a pas de texte où entrer : tout y est bord.
    /// </summary>
    private bool DansLeTexte(Frame bloc, Point page)
    {
        if (bloc is not TextFrame { Visible: true, HasField: false } texte) return false;

        var (lx, ly) = texte.ToLocal(page.X, page.Y);
        var bandeX = Math.Min(BandeDeBord / Scale, texte.Width / 4);
        var bandeY = Math.Min(BandeDeBord / Scale, texte.Height / 4);
        return lx > bandeX && lx < texte.Width - bandeX && ly > bandeY && ly < texte.Height - bandeY;
    }

    /// <summary>
    /// Le bloc qu'une pression prend en ce point : <paramref name="dessous"/>, le bloc atteint,
    /// sauf si le point tombe entre un bloc sélectionné et son cadre de sélection — la bande de
    /// bord s'y étend, et le cadre, dessiné par-dessus tout, prend ce qu'il borde. Un trait n'a pas
    /// de cadre où le prendre : son survol est sa distance à l'encre.
    /// </summary>
    private Frame? BlocVise(Point page, Frame? dessous)
    {
        if (_vm?.CurrentPage is not { } feuille || !DansLaPage(page)) return dessous;

        var ecart = SelectionGap / Scale;
        var cadre = feuille.Frames.LastOrDefault(bloc =>
            bloc is { Visible: true, IsLine: false } && _vm.Selection.Contains(bloc) &&
            EntreLeBlocEtSonCadre(bloc, page, ecart));
        return cadre ?? dessous;
    }

    private static bool EntreLeBlocEtSonCadre(Frame bloc, Point page, double ecart)
    {
        var (lx, ly) = bloc.ToLocal(page.X, page.Y);
        var dansLeCadre = lx >= -ecart && ly >= -ecart && lx <= bloc.Width + ecart && ly <= bloc.Height + ecart;
        var dansLeBloc = lx >= 0 && ly >= 0 && lx <= bloc.Width && ly <= bloc.Height;
        return dansLeCadre && !dansLeBloc;
    }

    /// <summary>
    /// Le curseur sur un bloc : le I sur son texte, les quatre flèches sur son bord. Sur le bloc en
    /// saisie, le I partout où la couche garde le clic pour le texte, bord compris (N21) : c'est là
    /// qu'un clic pose le curseur.
    /// </summary>
    private Cursor CurseurSur(Frame? bloc, Point page) =>
        DansLeTexteEnSaisie(new Point(page.X * Scale, page.Y * Scale)) ? TexteCursor
        : bloc is null ? ArrowCursor : DansLeTexte(bloc, page) ? TexteCursor : MoveCursor;

    /// <summary>
    /// Une pression sans <c>Ctrl</c> ni <c>Maj</c> dans le texte d'un bloc : elle prend ce bloc
    /// seul, ouvre la saisie le curseur sur le caractère visé, et garde le geste, que le glissé
    /// changera en sélection de texte. Rend faux, sans rien faire, hors du texte.
    ///
    /// La position est lue sur la composition qui était à l'écran, par le contrôle même qui la
    /// dessinait : le point du pointeur y entre par la transformation de l'écran, mise en page et
    /// rotation comprises. <see cref="Frame.ToLocal"/>, calculé sur le modèle, s'en écarte d'un
    /// demi-point sur un bloc tourné (relevé de N3) : il dit seulement si le point est dans le
    /// texte ou sur le bord. Sans géométrie, le curseur va au bout du texte.
    /// </summary>
    private bool EntrerDansLeTexte(Frame bloc, Point page, PointerPressedEventArgs e)
    {
        if (_vm is null || !DansLeTexte(bloc, page) || bloc is not TextFrame texte) return false;

        var curseur = PositionVisee(texte, e);

        if (_vm.Selection.Count != 1 || !_vm.Selection.Contains(texte)) _vm.SelectOnly(texte);
        BeginTextEdit(texte, curseur);
        if (!ReferenceEquals(_editing, texte)) return true;

        _ancreDuTexte = curseur;
        _uniteDuGlisse = UniteDuTexte.Caractere;
        _geometrieDuGlisse = null;
        _mode = DragMode.Texte;
        e.Pointer.Capture(InputLayer);
        return true;
    }

    /// <summary>La position du texte sous le pointeur, sur la composition à l'écran.</summary>
    private int PositionVisee(TextFrame texte, PointerEventArgs e)
    {
        var compose = TexteComposeDe(texte);
        var longueur = texte.Text?.Length ?? 0;

        return compose?.Geometrie() is { } geometrie
            ? Math.Clamp(geometrie.IndexSous(e.GetPosition(compose)), 0, longueur)
            : longueur;
    }

    /// <summary>Le contrôle qui dessine la composition du bloc sur la page, s'il est à l'écran.</summary>
    private TexteCompose? TexteComposeDe(TextFrame bloc) =>
        Scaler.GetVisualDescendants().OfType<TexteCompose>()
            .FirstOrDefault(controle => ReferenceEquals(controle.Bloc, bloc) && controle.IsEffectivelyVisible);

    /// <summary>
    /// Le glissé dans le texte, passé le seuil : la sélection du champ suit le pointeur depuis
    /// l'ancre. La position est lue sur la composition, qui est ce que l'écran montre pendant la
    /// saisie (poste N21) : par caractère après un clic, par mot après un double-clic, par
    /// paragraphe après un triple-clic, la prise de départ toujours gardée. Le champ la lisait
    /// dans sa propre mise en page, tant qu'il était à l'écran (N12).
    /// </summary>
    private void EtendreLaSelectionDuTexte(PointerEventArgs e)
    {
        if (_editing is not { } bloc || _zoneDeSaisie is not { } zone || TexteComposeDe(bloc) is not { } compose) return;

        var geometrie = _geometrieDuGlisse ??= compose.Geometrie();
        if (geometrie is null) return;

        var texte = zone.Text ?? "";
        var point = e.GetPosition(compose);
        if (_uniteDuGlisse == UniteDuTexte.Caractere)
        {
            var position = Math.Clamp(geometrie.IndexSous(point), 0, texte.Length);
            PoserLaSelection(zone, Math.Clamp(_ancreDuTexte, 0, texte.Length), position);
            return;
        }

        var caractere = geometrie.CaractereSous(point);
        var atteinte = _uniteDuGlisse == UniteDuTexte.Mot ? MotDe(texte, caractere) : ParagrapheDe(texte, caractere);
        var (debut, fin) = _priseDuTexte;
        if (atteinte.Debut < debut) PoserLaSelection(zone, fin, atteinte.Debut);
        else PoserLaSelection(zone, debut, Math.Max(fin, atteinte.Fin));
    }

    /// <summary>
    /// Le relâché d'une pression dans le texte. Un glissé qui a franchi le seuil finit là où le
    /// pointeur est lâché ; un clic qui a tremblé laisse le curseur où la pression l'a posé.
    /// Aucune transaction n'est à refermer : celle de la saisie suit le focus de sa zone (Q25).
    /// </summary>
    private void FinirLeGlisseDansLeTexte(PointerReleasedEventArgs e)
    {
        if (_aGlisse) EtendreLaSelectionDuTexte(e);
        _geometrieDuGlisse = null;
    }

    /// <summary>
    /// Pose la sélection [<paramref name="ancre"/>, <paramref name="fin"/>) dans le champ, le
    /// curseur à sa fin, comme le champ le fait au pointeur : un curseur posé seul quand l'ancre
    /// et la fin se confondent.
    /// </summary>
    private static void PoserLaSelection(TextBox zone, int ancre, int fin)
    {
        // Le curseur d'abord : le poser réduit la sélection à lui (TextBox.OnCaretIndexChanged).
        // La fin ensuite, au cas où il y était déjà, et l'ancre en dernier.
        zone.CaretIndex = fin;
        zone.SelectionEnd = fin;
        zone.SelectionStart = ancre;
    }

    // ------------------------------------------------------- la sélection du texte (N15)

    /// <summary>
    /// Le champ de la saisie dont la planche suit la sélection : celui qui a pris le focus en
    /// dernier pour le bloc en saisie. Nul hors saisie.
    /// </summary>
    private TextBox? _zoneSuivie;

    /// <summary>L'écoute du focus est posée : elle ne l'est qu'une fois, au premier attachement.</summary>
    private bool _focusDesZonesEcoute;

    /// <summary>
    /// La planche écoute le focus que prennent ses champs, pour suivre la sélection du texte en
    /// saisie. L'écoute est posée ici, et non dans le constructeur, qu'un seul poste d'une salve
    /// retouche (règle 16 de la phase N) ; une fois, car la planche peut être détachée puis
    /// rattachée.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_focusDesZonesEcoute) return;

        _focusDesZonesEcoute = true;
        AddHandler(GotFocusEvent, (_, args) => SuivreLaZoneDeSaisie(args.Source as TextBox),
            RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, ToucheDeLaSaisie, RoutingStrategies.Tunnel);

        // La saisie sur le texte composé (poste N21). La couche ne laisse plus rien au champ,
        // caché : elle garde le clic dans le texte en saisie, que la pression ci-dessous prend
        // avant qu'OnPointerPressed ne referme la saisie. Ce que la couche laissait au champ
        // (LaisseAuTexteEnSaisie, poste N5) ne sert plus ; il reste dans le fichier principal de
        // la planche, qu'un autre poste de la salve retouche (règle 16).
        InputLayer.Laisse = null;
        AddHandler(PointerPressedEvent, PressionDansLaSaisie, RoutingStrategies.Tunnel);

        // Les marques suivent la mise en page — le zoom, la place et la taille du bloc —, et la
        // sélection que le modèle de vue garde.
        LayoutUpdated += (_, _) => SuivreLaMiseEnPage();
        DataContextChanged += (_, _) => SuivreLeModeleDesMarques();
        SuivreLeModeleDesMarques();

        // Le texte trouvé par la recherche (N20) : l'écoute suit le modèle de vue, comme celle
        // qu'AttachViewModel pose, sans toucher au fichier principal de la planche (règle 16).
        DataContextChanged += (_, _) => SuivreLaRecherche();
        SuivreLaRecherche();
    }

    // ------------------------------------------------------- le texte trouvé (N20)

    /// <summary>Le modèle de vue dont la planche écoute la recherche.</summary>
    private MainWindowViewModel? _vmDeLaRecherche;

    private void SuivreLaRecherche()
    {
        if (_vmDeLaRecherche is not null) _vmDeLaRecherche.TexteTrouve -= OnTexteTrouve;
        _vmDeLaRecherche = DataContext as MainWindowViewModel;
        if (_vmDeLaRecherche is not null) _vmDeLaRecherche.TexteTrouve += OnTexteTrouve;
    }

    /// <summary>
    /// La recherche a trouvé un texte dans un bloc de la page, que le modèle de vue a pris seul
    /// (poste N20) : la saisie s'ouvre — ou reste ouverte, sur le même bloc —, la plage trouvée
    /// sélectionnée, le curseur à sa fin, comme après un glissé (N12). Le champ prend le focus, et
    /// la sélection est dite au modèle de vue comme toute autre (N15). Après l'ouverture : le champ
    /// n'existe qu'au passage de mise en page qui suit, et <see cref="BeginTextEdit"/> y pose son
    /// curseur d'abord.
    /// </summary>
    private void OnTexteTrouve(object? sender, SelectionDuTexte trouve)
    {
        if (trouve.Bloc is not TextFrame bloc) return;

        if (!ReferenceEquals(_editing, bloc))
        {
            EndTextEdit();
            BeginTextEdit(bloc, trouve.Fin);
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_editing, bloc) || _zoneDeSaisie is not { } zone) return;

            zone.Focus();
            var longueur = zone.Text?.Length ?? 0;
            PoserLaSelection(zone, Math.Clamp(trouve.Debut, 0, longueur), Math.Clamp(trouve.Fin, 0, longueur));
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Ce que la planche prend au champ de la saisie, avant lui (poste N16 ; fiche 23, a15 et
    /// a16). Le champ met lui-même la tabulation au curseur (<c>AcceptsTab</c>), et Entrée y
    /// écrit le saut de paragraphe ; il écrivait aussi le saut de paragraphe sous <c>Maj</c> et
    /// <c>Ctrl</c>.
    ///
    ///   - <c>Maj+Entrée</c> pose le saut de ligne forcé, <see cref="Compositeur.SautDeLigne"/>,
    ///     à la place de la sélection, le curseur après lui (<see cref="PoserLeSautForce"/>) ;
    ///   - <c>Maj+Tab</c> ne fait rien. Sans <c>AcceptsTab</c>, la navigation emmenait le focus
    ///     au contrôle précédent et laissait la saisie ouverte, orpheline ; avec lui, le champ
    ///     écrit une tabulation sous <c>Maj</c> comme sans lui (mesuré). Ni l'un ni l'autre :
    ///     <c>Maj+Tab</c> reste dans le bloc, sans rien écrire.
    ///
    /// <c>Ctrl+Entrée</c> reste au champ, qui y écrit un saut de paragraphe. Dans le dock, Tab
    /// reste la navigation entre champs : seul le champ de la page est ici.
    /// </summary>
    private void ToucheDeLaSaisie(object? sender, KeyEventArgs e)
    {
        if (_editing is null || e.Source is not TextBox zone || !ReferenceEquals(zone.DataContext, _editing) ||
            !this.IsVisualAncestorOf(zone))
        {
            return;
        }

        if (e.KeyModifiers != KeyModifiers.Shift) return;

        switch (e.Key)
        {
            case Key.Enter:
                PoserLeSautForce(zone);
                e.Handled = true;
                break;
            case Key.Tab:
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Le saut de ligne forcé, posé dans le champ comme le champ y pose une frappe : à la place
    /// de la sélection, le curseur après lui. Le texte passe par la liaison du champ, donc par la
    /// saisie du bloc, qui recale fragments et écarts comme pour toute frappe (Q1) : il y est un
    /// caractère du paragraphe. Écrit, et non rejoué en frappe synthétisée : la planche ne
    /// fabrique aucun événement (recette N5).
    /// </summary>
    private static void PoserLeSautForce(TextBox zone)
    {
        var texte = zone.Text ?? "";
        var debut = Math.Clamp(Math.Min(zone.SelectionStart, zone.SelectionEnd), 0, texte.Length);
        var fin = Math.Clamp(Math.Max(zone.SelectionStart, zone.SelectionEnd), debut, texte.Length);

        zone.SetCurrentValue(TextBox.TextProperty, texte[..debut] + Compositeur.SautDeLigne + texte[fin..]);
        PoserLaSelection(zone, debut + 1, debut + 1);
    }

    /// <summary>
    /// Le champ du bloc en saisie prend le focus : la planche suit sa sélection, et la dit au
    /// modèle de vue (poste N15). Elle la suit tant que le champ a le focus — chaque pression,
    /// glissé, flèche ou frappe —, et la vide quand le champ disparaît, à la sortie de la saisie.
    ///
    /// Quand le focus va à la barre ou au dock, le champ oublie sa sélection : il la réduit à son
    /// curseur en perdant le focus (<c>TextBox.OnLostFocus</c>, <c>ClearSelection</c>). La
    /// planche ne le suit pas là : un champ sans focus ne dit plus rien, et la sélection que le
    /// modèle de vue garde est celle que l'utilisateur a faite, où le réglage de la barre doit
    /// s'appliquer (R7). Seul le texte changé ailleurs — au champ Contenu du dock, lié au même
    /// bloc — la ramène dans ses bornes.
    /// </summary>
    private void SuivreLaZoneDeSaisie(TextBox? zone)
    {
        if (zone is null || _editing is null || !ReferenceEquals(zone.DataContext, _editing) ||
            !this.IsVisualAncestorOf(zone))
        {
            return;
        }

        if (!ReferenceEquals(_zoneSuivie, zone))
        {
            if (_zoneSuivie is not null) _zoneSuivie.PropertyChanged -= OnZoneSuivieChangee;
            _zoneSuivie = zone;
            zone.PropertyChanged += OnZoneSuivieChangee;
        }

        SuivreLeTexteDesMarques(_editing, zone);
        DireLaSelectionDuTexte(zone);
        DemanderLesMarques();
    }

    private void OnZoneSuivieChangee(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not TextBox zone) return;

        // Le champ se cache : la saisie est refermée — Échap, un clic qui en sort, une autre
        // sélection, Annuler. La sélection du texte n'est plus.
        if (e.Property == IsVisibleProperty && !zone.IsVisible)
        {
            OublierLaSelectionDuTexte(zone);
            return;
        }

        // Le curseur ne se montre que quand le champ a le focus, comme celui d'un champ (N21).
        if (e.Property == IsFocusedProperty) DemanderLesMarques();

        if (e.Property != TextBox.SelectionStartProperty && e.Property != TextBox.SelectionEndProperty &&
            e.Property != TextBox.TextProperty)
        {
            return;
        }

        if (!zone.IsFocused)
        {
            if (e.Property == TextBox.TextProperty) BornerLaSelectionDuTexte(zone.Text?.Length ?? 0);
            return;
        }

        DireLaSelectionDuTexte(zone);
    }

    /// <summary>La sélection du champ, dite au modèle de vue pour le bloc en saisie.</summary>
    private void DireLaSelectionDuTexte(TextBox zone)
    {
        if (_vm is null || _editing is null || !ReferenceEquals(zone.DataContext, _editing) || !zone.IsVisible) return;

        _vm.SuivreLaSelectionDuTexte(SelectionDuTexte.De(_editing, null, zone.SelectionStart, zone.SelectionEnd,
            zone.Text?.Length ?? 0));
    }

    /// <summary>
    /// Le texte a changé hors du champ, qui n'a pas le focus : la sélection gardée revient dans
    /// les bornes du texte, sans rien dire de plus.
    /// </summary>
    private void BornerLaSelectionDuTexte(int longueur)
    {
        if (_vm?.SelectionDuTexte is not { } selection) return;

        _vm.SuivreLaSelectionDuTexte(SelectionDuTexte.De(selection.Bloc, selection.Case, selection.Ancre,
            selection.Curseur, longueur));
    }

    /// <summary>
    /// La saisie du bloc de <paramref name="zone"/> est refermée : la planche ne suit plus son
    /// champ, et la sélection du texte, si c'était la sienne, est vide.
    /// </summary>
    private void OublierLaSelectionDuTexte(TextBox zone)
    {
        zone.PropertyChanged -= OnZoneSuivieChangee;
        if (ReferenceEquals(_zoneSuivie, zone)) _zoneSuivie = null;
        if (_vm?.SelectionDuTexte is { } selection && ReferenceEquals(selection.Bloc, zone.DataContext))
        {
            _vm.SuivreLaSelectionDuTexte(null);
        }

        if (ReferenceEquals(_texteDesMarques, zone.DataContext)) SuivreLeTexteDesMarques(null, null);
        DemanderLesMarques();
    }

    // ------------------------------------------------- la saisie sur le texte composé (N21)

    /// <summary>
    /// Le point de la planche, en pixels d'écran (le repère de <c>Surface</c>), tombe-t-il sur le
    /// texte en saisie, là où la couche garde le clic pour lui ? Tout le bloc, bord compris, lu dans
    /// le repère du contrôle qui dessine sa composition — mise en page et rotation comprises —, sur
    /// la page, et hors des poignées, qui passent avant lui. C'est la zone que le champ prenait
    /// quand il était à l'écran (N5) : le cadre de sélection, autour, prend et déplace le bloc.
    /// </summary>
    private bool DansLeTexteEnSaisie(Point surfacePoint)
    {
        if (_editing is not { } bloc || !DansLaPage(ToPageCoordinates(surfacePoint)) ||
            HitHandle(bloc, surfacePoint) is not null || TexteComposeDe(bloc) is not { } compose)
        {
            return false;
        }

        return Surface.TranslatePoint(surfacePoint, compose) is { } dedans && new Rect(compose.Bounds.Size).Contains(dedans);
    }

    /// <summary>
    /// Une pression dans le texte en saisie (poste N21), prise au passage descendant, avant
    /// qu'<c>OnPointerPressed</c> ne referme la saisie (N5). Le champ, caché, ne la reçoit plus :
    /// la planche fait ce qu'il faisait, sur la composition.
    ///
    ///   - le bouton gauche pose le curseur au caractère visé ; avec <c>Maj</c>, il étend la
    ///     sélection depuis son ancre ; au deuxième clic, il prend le mot, au troisième le
    ///     paragraphe — le champ prenait tout le texte (fiche 23, a11). Le glissé qui suit étend
    ///     par la même unité ;
    ///   - le bouton droit garde la saisie et la sélection : le relâché lève la demande du menu
    ///     contextuel, qui remonte de la couche à la planche (A12) ;
    ///   - le bouton du milieu, comme hors du texte.
    ///
    /// Le compte des clics est celui que la plate-forme donne à la pression : rien n'est rejoué,
    /// rien n'est fabriqué (recette N5).
    /// </summary>
    private void PressionDansLaSaisie(object? sender, PointerPressedEventArgs e)
    {
        if (_editing is not { } bloc || _zoneDeSaisie is not { } zone || !ReferenceEquals(e.Source, InputLayer) ||
            TexteComposeDe(bloc) is not { } compose)
        {
            return;
        }

        var pointeur = e.GetCurrentPoint(Surface);
        if (!DansLeTexteEnSaisie(pointeur.Position)) return;

        switch (pointeur.Properties.PointerUpdateKind)
        {
            case PointerUpdateKind.RightButtonPressed:
                e.Handled = true;
                return;
            case PointerUpdateKind.LeftButtonPressed:
                break;
            default:
                return;
        }

        e.Handled = true;
        if (compose.Geometrie() is not { } geometrie) return;
        if (!zone.IsFocused) zone.Focus();

        var texte = zone.Text ?? "";
        var point = e.GetPosition(compose);
        var position = Math.Clamp(geometrie.IndexSous(point), 0, texte.Length);

        if (e.ClickCount >= 2)
        {
            var caractere = geometrie.CaractereSous(point);
            _uniteDuGlisse = e.ClickCount == 2 ? UniteDuTexte.Mot : UniteDuTexte.Paragraphe;
            _priseDuTexte = _uniteDuGlisse == UniteDuTexte.Mot ? MotDe(texte, caractere) : ParagrapheDe(texte, caractere);
            PoserLaSelection(zone, _priseDuTexte.Debut, _priseDuTexte.Fin);
        }
        else
        {
            // Maj étend depuis l'ancre de la sélection du champ : SelectionStart, le bout que le
            // geste ne déplace pas (N15).
            _ancreDuTexte = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
                ? Math.Clamp(zone.SelectionStart, 0, texte.Length)
                : position;
            _uniteDuGlisse = UniteDuTexte.Caractere;
            PoserLaSelection(zone, _ancreDuTexte, position);
        }

        _geometrieDuGlisse = geometrie;
        _priseALEcran = pointeur.Position;
        _aGlisse = false;
        _mode = DragMode.Texte;
        e.Pointer.Capture(InputLayer);
    }

    /// <summary>
    /// Le mot qui porte le caractère <paramref name="caractere"/> : la suite de lettres et de
    /// chiffres qui le contient ; une suite de blancs, si c'est un blanc ; le signe seul, si c'est
    /// une ponctuation. Un saut de paragraphe ou de ligne n'est pas un mot : le double-clic au bout
    /// d'une ligne prend le mot qui le précède. Le mot seul, sans le blanc qui le suit — le champ
    /// en prenait tantôt un, tantôt non (mesuré : « monde, » au clic sur la virgule, « entier »
    /// au clic sur la première espace qui suit, « entier  » sur la seconde).
    /// </summary>
    internal static (int Debut, int Fin) MotDe(string texte, int caractere)
    {
        if (texte.Length == 0) return (0, 0);

        var c = Math.Clamp(caractere, 0, texte.Length - 1);
        if (EstUnSaut(texte[c]))
        {
            if (c == 0 || EstUnSaut(texte[c - 1])) return (c, c);
            c--;
        }

        var sorte = SorteDe(texte[c]);
        if (sorte == SorteDeCaractere.Signe)
        {
            var fin = c + 1 < texte.Length && char.IsSurrogatePair(texte[c], texte[c + 1]) ? c + 2 : c + 1;
            return (c, fin);
        }

        var debut = c;
        while (debut > 0 && SorteDe(texte[debut - 1]) == sorte) debut--;
        var bout = c + 1;
        while (bout < texte.Length && SorteDe(texte[bout]) == sorte) bout++;
        return (debut, bout);
    }

    /// <summary>
    /// Le paragraphe qui porte le caractère <paramref name="caractere"/>, sans le saut qui le
    /// ferme : du caractère qui suit le saut précédent jusqu'au saut suivant. Un saut de ligne
    /// forcé (U+2028) est dans le paragraphe, comme le compte des paragraphes le veut (N16). Le
    /// saut lui-même appartient au paragraphe qu'il ferme. Le saut reste hors de la sélection : la
    /// frappe qui la remplace garde le paragraphe et son écart (Q1).
    /// </summary>
    internal static (int Debut, int Fin) ParagrapheDe(string texte, int caractere)
    {
        var c = Math.Clamp(caractere, 0, texte.Length);
        var debut = c;
        while (debut > 0 && texte[debut - 1] is not ('\n' or '\r')) debut--;
        var fin = c;
        while (fin < texte.Length && texte[fin] is not ('\n' or '\r')) fin++;
        return (debut, fin);
    }

    private enum SorteDeCaractere { Mot, Blanc, Signe }

    private static bool EstUnSaut(char c) => c is '\n' or '\r' || c == Compositeur.SautDeLigne;

    private static SorteDeCaractere SorteDe(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || char.IsSurrogate(c) || char.GetUnicodeCategory(c) is
            System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.SpacingCombiningMark
            ? SorteDeCaractere.Mot
            : char.IsWhiteSpace(c) && !EstUnSaut(c) ? SorteDeCaractere.Blanc : SorteDeCaractere.Signe;

    // ------------------------------------------------------------------ les marques (N21)

    /// <summary>Le bloc dont les marques suivent les changements : celui de la saisie suivie.</summary>
    private TextFrame? _texteDesMarques;

    /// <summary>Le présentateur du champ de la saisie suivie, dont le pré-édit se dessine.</summary>
    private TextPresenter? _presentationSuivie;

    /// <summary>Le modèle de vue dont les marques suivent la sélection du texte.</summary>
    private MainWindowViewModel? _modeleDesMarques;

    /// <summary>Une pose des marques attend : les demandes d'un même geste n'en font qu'une.</summary>
    private bool _marquesDemandees;

    /// <summary>
    /// Les marques suivent la sélection que le modèle de vue garde (N15), et non celle du champ :
    /// quand la barre prend le focus, le champ réduit la sienne à un curseur, et c'est la sélection
    /// gardée, celle que la barre mettra en forme (R7), qui reste surlignée.
    /// </summary>
    private void SuivreLeModeleDesMarques()
    {
        if (_modeleDesMarques is not null) _modeleDesMarques.PropertyChanged -= OnModeleDesMarquesChange;
        _modeleDesMarques = DataContext as MainWindowViewModel;
        if (_modeleDesMarques is not null) _modeleDesMarques.PropertyChanged += OnModeleDesMarquesChange;
        DemanderLesMarques();
    }

    private void OnModeleDesMarquesChange(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectionDuTexte)) DemanderLesMarques();
    }

    /// <summary>
    /// Les marques suivent le bloc en saisie — un réglage de la barre change sa composition sans
    /// toucher à la mise en page — et le pré-édit du champ. Nul, plus rien.
    /// </summary>
    private void SuivreLeTexteDesMarques(TextFrame? bloc, TextBox? zone)
    {
        if (!ReferenceEquals(_texteDesMarques, bloc))
        {
            if (_texteDesMarques is { } ancien)
            {
                ancien.PropertyChanged -= OnTexteDesMarquesChange;
                ancien.Spans.CollectionChanged -= OnTexteDesMarquesChange;
                ancien.Paragraphs.CollectionChanged -= OnTexteDesMarquesChange;
            }

            _texteDesMarques = bloc;
            if (bloc is not null)
            {
                bloc.PropertyChanged += OnTexteDesMarquesChange;
                bloc.Spans.CollectionChanged += OnTexteDesMarquesChange;
                bloc.Paragraphs.CollectionChanged += OnTexteDesMarquesChange;
            }
        }

        var presentation = zone?.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (ReferenceEquals(_presentationSuivie, presentation)) return;

        if (_presentationSuivie is not null) _presentationSuivie.PropertyChanged -= OnPresentationSuivieChangee;
        _presentationSuivie = presentation;
        if (presentation is not null) presentation.PropertyChanged += OnPresentationSuivieChangee;
    }

    private void OnTexteDesMarquesChange(object? sender, EventArgs e) => DemanderLesMarques();

    private void OnPresentationSuivieChangee(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextPresenter.PreeditTextProperty || e.Property == TextPresenter.PreeditTextCursorPositionProperty)
        {
            DemanderLesMarques();
        }
    }

    /// <summary>
    /// Une passe de mise en page vient de finir, n'importe où dans la fenêtre : les marques ne se
    /// reposent que si le texte en saisie a bougé à l'écran — zoom, place, taille, rotation —, ou
    /// si la saisie s'est refermée sans qu'elles le sachent. Le reste — la frappe, la sélection, un
    /// réglage du bloc — les demande lui-même (<see cref="DemanderLesMarques"/>) : recomposer le
    /// bloc à chaque passe, pour un champ du dock qui se remet en page, coûterait jusqu'à 15 ms
    /// sur ATA 21.
    /// </summary>
    private void SuivreLaMiseEnPage()
    {
        if (_editing is not { } bloc)
        {
            if (MarquesDeLaSaisie.Visibles) MarquesDeLaSaisie.Effacer();
            return;
        }

        if (!MarquesDeLaSaisie.Visibles || TexteComposeDe(bloc) is not { } compose ||
            compose.TransformToVisual(MarquesDeLaSaisie) != MarquesDeLaSaisie.Transformation)
        {
            PoserLesMarquesDuTexte();
        }
    }

    /// <summary>
    /// Demande une pose des marques, une fois le geste en cours fini : le texte tapé passe par la
    /// liaison du champ, puis par la saisie du bloc, avant que la composition ne le voie.
    /// </summary>
    private void DemanderLesMarques()
    {
        if (_marquesDemandees) return;

        _marquesDemandees = true;
        Dispatcher.UIThread.Post(() =>
        {
            _marquesDemandees = false;
            PoserLesMarquesDuTexte();
        });
    }

    /// <summary>
    /// Pose les marques de la saisie sur la composition (poste N21) : la sélection que le modèle
    /// de vue garde, surlignée ligne par ligne ; le curseur, quand elle est vide et que le champ a
    /// le focus ; le pré-édit de la méthode d'entrée au curseur. Tout est lu sur la géométrie du
    /// texte tel qu'il est dessiné (<see cref="GeometrieDuTexte"/>, N3), et mené dans l'<c>Overlay</c>
    /// par la transformation du contrôle même qui le dessine — zoom, place et rotation du bloc
    /// comprises. Hors saisie, rien.
    /// </summary>
    private void PoserLesMarquesDuTexte()
    {
        var marques = MarquesDeLaSaisie;
        var focus = _zoneSuivie is { IsFocused: true } zone && ReferenceEquals(zone.DataContext, _editing);

        // Un curseur seul, sans le focus, ne se montre pas : rien à composer.
        if (_editing is not { } bloc || _vm?.SelectionDuTexte is not { Case: null } selection ||
            !ReferenceEquals(selection.Bloc, bloc) || (selection.EstVide && !focus) ||
            TexteComposeDe(bloc) is not { } compose || compose.TransformToVisual(marques) is not { } transformation ||
            compose.Geometrie() is not { } geometrie)
        {
            marques.Effacer();
            return;
        }

        var preEdit = focus ? PreEditDe(geometrie, selection.Curseur) : null;
        var curseur = focus && selection.EstVide ? geometrie.CurseurDe(selection.Curseur) : (Rect?)null;
        if (preEdit is not null && curseur is { } avant)
        {
            // Le curseur suit le pré-édit, à la place que la méthode d'entrée lui donne dedans.
            var dedans = _presentationSuivie?.PreeditTextCursorPosition ?? preEdit.Chaine.Length;
            var decalage = dedans <= 0 ? 0 : geometrie.Metriques.Largeur(
                preEdit.Chaine[..Math.Min(dedans, preEdit.Chaine.Length)], geometrie.StyleDe(selection.Curseur));
            curseur = avant.Translate(new Vector(decalage, 0));
        }

        marques.Poser(transformation, geometrie.RectanglesDe(selection.Debut, selection.Fin), curseur, preEdit);
    }

    /// <summary>
    /// Le pré-édit de la méthode d'entrée, posé au curseur : mis en forme au style où il entrera,
    /// sur la ligne de base de la ligne du curseur, sur un fond qui couvre la composition sous lui
    /// — un pré-édit ne repousse pas le texte composé, qui ne le connaît pas encore.
    /// </summary>
    private PreEditPose? PreEditDe(GeometrieDuTexte geometrie, int curseur)
    {
        if (_presentationSuivie?.PreeditText is not { Length: > 0 } chaine) return null;

        var style = geometrie.StyleDe(curseur);
        var texte = new MetriquesAvalonia().Composer(chaine, style);
        var point = geometrie.PointDe(curseur);
        var bande = geometrie.CurseurDe(curseur);
        var origine = new Point(point.X, point.Y - texte.Baseline);
        var fond = new Rect(point.X, bande.Top, texte.WidthIncludingTrailingWhitespace, bande.Height);
        return new PreEditPose(texte, origine, fond, chaine);
    }
}
