using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Models;

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
/// Dans le texte en saisie, le champ reçoit lui-même les clics (poste N5) : <c>Maj</c>+clic y
/// étend la sélection, et le double-clic y prend un mot — il n'ouvre plus rien. Un bloc neuf est
/// vide, le curseur dedans, qu'il vienne de l'outil ou du menu Insertion.
///
/// Ce qu'on voit après l'entrée dépend de la voie de R7 (N21) : jusque-là, le champ en texte brut.
/// Le curseur, lui, tombe au bon caractère dès ce poste ; le glissé suit le champ, qui est ce
/// que l'écran montre pendant la saisie.
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

    /// <summary>Le curseur sur un bloc : le I sur son texte, les quatre flèches sur son bord.</summary>
    private Cursor CurseurSur(Frame? bloc, Point page) =>
        bloc is null ? ArrowCursor : DansLeTexte(bloc, page) ? TexteCursor : MoveCursor;

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
        _mode = DragMode.Texte;
        e.Pointer.Capture(InputLayer);
        return true;
    }

    /// <summary>La position du texte sous le pointeur, sur la composition à l'écran.</summary>
    private int PositionVisee(TextFrame texte, PointerEventArgs e)
    {
        var compose = Scaler.GetVisualDescendants().OfType<TexteCompose>()
            .FirstOrDefault(controle => ReferenceEquals(controle.Bloc, texte) && controle.IsEffectivelyVisible);
        var longueur = texte.Text?.Length ?? 0;

        return compose?.Geometrie() is { } geometrie
            ? Math.Clamp(geometrie.IndexSous(e.GetPosition(compose)), 0, longueur)
            : longueur;
    }

    /// <summary>
    /// Le glissé dans le texte, passé le seuil : la sélection du champ suit le pointeur depuis
    /// l'ancre. La position est lue dans la mise en page du champ, comme le champ la lirait
    /// lui-même (<c>TextBox.OnPointerMoved</c>) : c'est lui que l'écran montre pendant la saisie.
    /// Tant que la mise en page ne l'a pas posé, le pointeur n'a rien à sélectionner.
    /// </summary>
    private void EtendreLaSelectionDuTexte(PointerEventArgs e)
    {
        if (_editing is null || _zoneDeSaisie is not { } zone) return;

        var presentation = zone.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presentation is null) return;

        var point = e.GetPosition(presentation);
        var atteint = presentation.TextLayout.HitTestPoint(point).CharacterHit;
        var position = Math.Clamp(atteint.FirstCharacterIndex + atteint.TrailingLength, 0, zone.Text?.Length ?? 0);
        PoserLaSelection(zone, Math.Clamp(_ancreDuTexte, 0, zone.Text?.Length ?? 0), position);
    }

    /// <summary>
    /// Le relâché d'une pression dans le texte. Un glissé qui a franchi le seuil finit là où le
    /// pointeur est lâché ; un clic qui a tremblé laisse le curseur où la pression l'a posé.
    /// Aucune transaction n'est à refermer : celle de la saisie suit le focus de sa zone (Q25).
    /// </summary>
    private void FinirLeGlisseDansLeTexte(PointerReleasedEventArgs e)
    {
        if (_aGlisse) EtendreLaSelectionDuTexte(e);
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
}
