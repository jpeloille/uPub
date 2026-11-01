using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Taper dans le texte d'un groupe, et prendre un membre seul (poste N26 ; fiche 20 ; fiche 23, a6
/// et c15). Le livre retape le texte d'un élément de la bibliothèque dans le groupe, sans le
/// dissocier, et prend un membre d'un clic pour en remplacer l'image (p. 58-60). La planche ne
/// voyait que les blocs de la page : un clic sur un groupe le prenait entier.
///
///   - <b>Un clic dans le texte d'un membre</b> traverse le groupe — ses blocs, puis le repère de
///     chaque niveau, rotation comprise (<see cref="CheminSous"/>) — et ouvre la saisie de ce
///     membre, le curseur au point visé, qu'il soit sélectionné ou non. <b>Le groupe reste la
///     sélection</b> ; le texte en saisie est un autre état, que le modèle de vue garde
///     (<see cref="MainWindowViewModel.TexteEnSaisie"/>) et que la barre, le dock, R6, R7 et N24
///     suivent. Le curseur, la sélection, les caractères spéciaux et les fautes se posent sur la
///     composition par la transformation du contrôle qui la dessine : les rotations imbriquées
///     y sont.
///   - <b>Un clic sur un membre d'un groupe déjà sélectionné le prend seul</b>, au relâché
///     (<see cref="MainWindowViewModel.MembreSeul"/>) : le groupe garde son cadre, le membre a le
///     sien et ses poignées — dessinées sans se laisser prendre, son redimensionnement étant
///     réservé à la dissociation, comme dans Publisher. Un glissé parti d'un autre point du groupe
///     le déplace entier, comme avant ; parti du membre pris seul, il déplace le membre dans son
///     groupe, retenu à son cadre. Un second clic sur un groupe membre pris seul prend le bloc
///     qu'il porte sous le pointeur ; un double-clic sur une image ou une forme du groupe ouvre sa
///     section du dock. Un clic hors du groupe rend la sélection ordinaire ; <c>Échap</c> rend le
///     groupe.
///   - <b>La découpe de Q5</b> suit les membres qu'on règle : elle est liée au nombre des blocs du
///     groupe et à une révision que la planche avance quand le filet, la rotation ou la
///     visibilité d'un membre visé change (<see cref="RevisionDeLaDecoupeProperty"/>).
/// </summary>
public partial class PageCanvas
{
    /// <summary>
    /// La révision de la découpe d'un groupe, portée par la liste qui dessine ses blocs : la liaison
    /// de sa découpe la lit, et se recalcule quand la planche l'avance. La découpe ne suivait que
    /// le groupe et sa taille (Q5, « Laissé tel quel ») : aucun geste ne réglait un bloc groupé.
    /// </summary>
    public static readonly AttachedProperty<int> RevisionDeLaDecoupeProperty =
        AvaloniaProperty.RegisterAttached<PageCanvas, Control, int>("RevisionDeLaDecoupe");

    public static int GetRevisionDeLaDecoupe(Control controle) => controle.GetValue(RevisionDeLaDecoupeProperty);

    public static void SetRevisionDeLaDecoupe(Control controle, int revision) =>
        controle.SetValue(RevisionDeLaDecoupeProperty, revision);

    /// <summary>Le membre qu'une pression sur un membre du groupe sélectionné prendra seul au relâché.</summary>
    private Frame? _membreAuRelache;

    /// <summary>La pression attend son relâché pour prendre <see cref="_membreAuRelache"/> — ou rendre le groupe.</summary>
    private bool _prendreAuRelache;

    /// <summary>Le chemin du membre pris seul que le glissé déplace dans son groupe ; nul sinon.</summary>
    private IReadOnlyList<Frame>? _cheminDuMembreDeplace;

    // ------------------------------------------------------------------ traverser le groupe

    /// <summary>
    /// Le chemin de ce qui est sous le point de la page, depuis <paramref name="bloc"/>, un bloc de la
    /// page que le point atteint : dans un groupe, le bloc visible le plus en avant que le point
    /// atteint, lu dans le repère du groupe, et ainsi de suite tant que c'est un groupe. Un bloc
    /// qui n'est pas un groupe est son propre chemin.
    /// </summary>
    private static List<Frame> CheminSous(Frame bloc, Point page)
    {
        var chemin = new List<Frame> { bloc };
        var (x, y) = (page.X, page.Y);
        while (chemin[^1] is GroupFrame groupe)
        {
            (x, y) = groupe.ToLocal(x, y);
            var (px, py) = (x, y);
            if (groupe.Children.LastOrDefault(membre => membre.Visible && membre.HitTest(px, py)) is not { } dessous) break;
            chemin.Add(dessous);
        }

        return chemin;
    }

    /// <summary>
    /// Le membre qu'un clic en ce point prend dans le groupe sélectionné : le bloc du groupe sous le
    /// pointeur ; si c'est déjà le membre pris seul et qu'il est un groupe, le bloc qu'il porte sous
    /// le pointeur — on descend d'un niveau à chaque clic. Nul sur le fond du groupe.
    /// </summary>
    private Frame? MembreAPrendre(GroupFrame groupe, Point page)
    {
        var chemin = CheminSous(groupe, page);
        if (chemin.Count < 2) return null;

        var pris = _vm?.MembreSeul;
        var rang = pris is null ? -1 : chemin.FindIndex(bloc => ReferenceEquals(bloc, pris));
        if (rang < 1) return chemin[1];
        return pris is GroupFrame && rang + 1 < chemin.Count ? chemin[rang + 1] : chemin[rang];
    }

    // ------------------------------------------------------------------ la pression

    /// <summary>
    /// Une pression du bouton gauche, sans <c>Ctrl</c> ni <c>Maj</c>, sur le groupe sélectionné seul,
    /// hors du texte de ses membres. Sur le membre pris seul, elle le déplace dans son groupe. Ailleurs
    /// dans le groupe, elle prend le groupe comme avant — un glissé le déplace entier —, et le
    /// relâché, s'il n'a pas glissé, prend seul le membre visé, ou rend le groupe sur son fond. Un
    /// double-clic sur une image ou une forme du groupe la prend seule et ouvre sa section du dock.
    /// Faux hors de ce cas : la pression suit son chemin d'avant.
    /// </summary>
    private bool PrendreDansLeGroupe(Frame bloc, Point page, PointerPressedEventArgs e)
    {
        if (_vm is null || bloc is not GroupFrame groupe || _vm.Selection.Count != 1 ||
            !ReferenceEquals(_vm.Selection[0], groupe))
        {
            return false;
        }

        var membre = MembreAPrendre(groupe, page);

        if (e.ClickCount == 2 && membre is ImageFrame or ShapeFrame)
        {
            _vm.PrendreLeMembre(membre);
            _vm.FormatDuBlocCommand.Execute(null);
            return true;
        }

        if (membre is not null && ReferenceEquals(membre, _vm.MembreSeul) && _vm.CheminDe(membre) is { Count: > 1 } chemin)
        {
            _mode = DragMode.Move;
            RetenirLeMembre(chemin, page);
            _vm.BeginEdit();
            e.Pointer.Capture(InputLayer);
            return true;
        }

        _mode = DragMode.Move;
        RememberMove(page);
        _membreAuRelache = membre;
        _prendreAuRelache = true;
        _vm.BeginEdit();
        e.Pointer.Capture(InputLayer);
        return true;
    }

    /// <summary>
    /// Le bouton droit sur le groupe dont un membre est pris seul : il prend le membre sous le
    /// pointeur, que le menu visera — ou rend le groupe, sur son fond. Sans membre pris seul, le
    /// groupe reste pris entier, comme avant.
    /// </summary>
    private void PrendreAuBoutonDroitDansLeGroupe(Frame? bloc, Point page)
    {
        if (_vm?.MembreSeul is null || bloc is not GroupFrame groupe || _vm.Selection.Count != 1 ||
            !ReferenceEquals(_vm.Selection[0], groupe))
        {
            return;
        }

        _vm.PrendreLeMembre(MembreAPrendre(groupe, page));
    }

    /// <summary>
    /// Le glissé d'un groupe pris par l'un de ses membres a passé le seuil : c'est le groupe qui
    /// bouge, et c'est lui que la barre et le dock visent de nouveau.
    /// </summary>
    private void LacherLeMembreAuGlisse()
    {
        if (!_prendreAuRelache) return;

        _prendreAuRelache = false;
        _membreAuRelache = null;
        _vm?.PrendreLeMembre(null);
    }

    /// <summary>Le relâché d'une pression sur le groupe sélectionné qui n'a pas glissé : le membre visé est pris seul.</summary>
    private void PrendreAuRelache()
    {
        _cheminDuMembreDeplace = null;
        if (!_prendreAuRelache) return;

        _prendreAuRelache = false;
        var membre = _membreAuRelache;
        _membreAuRelache = null;
        _vm?.PrendreLeMembre(membre);
    }

    // ------------------------------------------------------------------ le glissé d'un membre

    /// <summary>Retient d'où part le membre pris seul que le glissé déplace dans son groupe.</summary>
    private void RetenirLeMembre(IReadOnlyList<Frame> chemin, Point page)
    {
        var membre = chemin[^1];
        _startPointer = page;
        _moveStart.Clear();
        _moveStart.Add((membre, membre.X, membre.Y));
        _moveExtent = membre.Extent;
        _moveSnappable = false;
        _cheminDuMembreDeplace = chemin;
    }

    /// <summary>
    /// Le membre pris seul suit le pointeur dans son groupe : le chemin parcouru sur la page, mené
    /// dans le repère du groupe — ses rotations retirées —, retenu à son cadre (la découpe de Q5 le
    /// rognerait au-delà). <c>Maj</c> tient l'axe ; rien ne s'aimante aux repères de la page.
    /// </summary>
    private void DeplacerLeMembre(IReadOnlyList<Frame> chemin, Point page, KeyModifiers modificateurs)
    {
        if (_moveStart.Count != 1) return;

        var dx = page.X - _startPointer.X;
        var dy = page.Y - _startPointer.Y;
        if (modificateurs.HasFlag(KeyModifiers.Shift)) (dx, dy) = TenirLAxe(dx, dy);

        var groupe = chemin[^2];
        var (lx, ly) = MainWindowViewModel.DansLeGroupe(chemin, dx, dy);
        (lx, ly) = MainWindowViewModel.BorneAuGroupe(_moveExtent, groupe.Width, groupe.Height, lx, ly);

        var (membre, x, y) = _moveStart[0];
        membre.X = x + lx;
        membre.Y = y + ly;
    }

    // ------------------------------------------------------------------ ce qu'on dessine

    /// <summary>
    /// Le membre visé que la planche encadre : le membre pris seul, ou le texte en saisie d'un
    /// groupe, avec son chemin — visible, lui et ses groupes, sur la page courante. Nul sinon.
    /// </summary>
    private (Frame Membre, IReadOnlyList<Frame> Chemin)? MembreEncadre()
    {
        if (_vm?.MembreVise is not { } membre || _vm.CheminDe(membre) is not { Count: > 1 } chemin ||
            chemin.Any(bloc => !bloc.Visible))
        {
            return null;
        }

        return (membre, chemin);
    }

    /// <summary>Un point du repère du dernier bloc du chemin, en pixels de la surface.</summary>
    private Point SurLaSurface(IReadOnlyList<Frame> chemin, double x, double y)
    {
        var (px, py) = MainWindowViewModel.VersLaPage(chemin, x, y);
        return new Point(px * Scale, py * Scale);
    }

    /// <summary>Les quatre coins du cadre de sélection du dernier bloc du chemin, tournés avec lui et ses groupes.</summary>
    private List<Point> CoinsDuChemin(IReadOnlyList<Frame> chemin)
    {
        var bloc = chemin[^1];
        var ecart = SelectionGap / Scale;
        return
        [
            SurLaSurface(chemin, -ecart, -ecart),
            SurLaSurface(chemin, bloc.Width + ecart, -ecart),
            SurLaSurface(chemin, bloc.Width + ecart, bloc.Height + ecart),
            SurLaSurface(chemin, -ecart, bloc.Height + ecart)
        ];
    }

    /// <summary>
    /// Les poignées du membre pris seul : les huit carrés posés sur son cadre — deux sur les bouts
    /// d'un trait —, inertes, sans tige ni disque de rotation. Elles disent ce qui est pris ; le
    /// redimensionner reste réservé à la dissociation (<see cref="HitHandle"/> ne les sert pas).
    /// </summary>
    private void PoserLesPoigneesDuMembre(IReadOnlyList<Frame> chemin)
    {
        var membre = chemin[^1];
        for (var i = 0; i < _handles.Length; i++)
        {
            var poignee = (Anchor)i;
            var montree = poignee != Anchor.Rotate && PoigneeServie(membre, poignee);
            _handles[i].IsVisible = montree;
            _handles[i].Classes.Set("inerte", montree);
            if (!montree) continue;

            var (lx, ly) = LocalAnchor(membre, poignee);
            var point = SurLaSurface(chemin, lx, ly);
            Canvas.SetLeft(_handles[i], point.X - _handles[i].Width / 2);
            Canvas.SetTop(_handles[i], point.Y - _handles[i].Height / 2);
        }

        _rotateStem.IsVisible = false;
    }

    // ------------------------------------------------------------------ ce qu'on suit

    /// <summary>
    /// Les blocs que la planche suit en plus de la sélection : le texte en saisie et le membre pris
    /// seul, avec les groupes qui les portent — leur cadre suit leur place, et la découpe leur filet.
    /// </summary>
    private IEnumerable<Frame> MembresSuivis()
    {
        if (_vm is null) yield break;

        foreach (var membre in new Frame?[] { _editing, _vm.MembreSeul })
        {
            if (membre is null || _vm.CheminDe(membre) is not { Count: > 1 } chemin) continue;
            foreach (var bloc in chemin.Skip(1)) yield return bloc;
        }
    }

    /// <summary>
    /// Un membre suivi change ce que la découpe de son groupe compte — son filet, sa rotation, sa
    /// visibilité : la découpe de chaque groupe qui le porte se recalcule, comme l'export la
    /// recalcule (Q5).
    /// </summary>
    private void SuivreLaDecoupe(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Frame membre || _vm?.CurrentPage is not { } page || page.Frames.Contains(membre)) return;
        if (e.PropertyName is not (nameof(Frame.Stroke) or nameof(Frame.StrokeThickness) or nameof(Frame.HasStroke)
            or nameof(Frame.Visible) or nameof(Frame.Rotation)))
        {
            return;
        }

        if (_vm.CheminDe(membre) is not { Count: > 1 } chemin) return;

        var groupes = chemin.Take(chemin.Count - 1).ToList();
        foreach (var liste in Scaler.GetVisualDescendants().OfType<ItemsControl>())
        {
            if (liste.Classes.Contains("frames") && groupes.Any(groupe => ReferenceEquals(liste.DataContext, groupe)))
            {
                SetRevisionDeLaDecoupe(liste, GetRevisionDeLaDecoupe(liste) + 1);
            }
        }
    }
}
