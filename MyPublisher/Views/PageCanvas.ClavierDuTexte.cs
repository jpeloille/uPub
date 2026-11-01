using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Le clavier suit les lignes composées (poste N29 ; fiche 23, a21), décidé par le propriétaire le
/// 31 octobre 2026 sur la mesure de N21.
///
/// Depuis la voie 2a (N21), la saisie montre le texte composé, mais le champ caché gardait la
/// navigation au clavier sur ses propres lignes, coupées à sa mesure, sans retraits ni fragments :
/// sur ATA 21, depuis le milieu de 33 lignes composées, <c>↓</c> tombait ailleurs que là où la
/// composition l'attend 21 fois, <c>↑</c> 23 fois, <c>Début</c> et <c>Fin</c> 4 fois chacune. La
/// planche prend désormais ces touches au champ, avant lui, et les calcule sur les lignes que la
/// composition pose (<see cref="GeometrieDuTexte"/>, N3) :
///
///   - <c>Début</c> et <c>Fin</c> mènent au début et au bout de la ligne composée du curseur ;
///     <c>Ctrl+Début</c> et <c>Ctrl+Fin</c> restent au champ, qui les fait sur le texte ;
///   - <c>↑</c> et <c>↓</c> mènent à la ligne composée du dessus ou du dessous, dans l'ordre du
///     texte — de la dernière ligne d'une colonne à la première de la suivante —, à la position la
///     plus proche de l'abscisse visée, qui se garde d'une flèche à l'autre et se perd au premier
///     autre déplacement ; <c>↑</c> sur la première ligne mène au début du texte, <c>↓</c> sur la
///     dernière à sa fin ;
///   - <c>Pg.Préc</c> et <c>Pg.Suiv</c> mènent à la première et à la dernière ligne du bloc, à
///     l'abscisse visée ;
///   - avec <c>Maj</c>, chacune étend la sélection depuis son ancre (N15).
///
/// Le champ reste la source de la frappe : la touche calculée y pose le curseur et la sélection
/// (<see cref="PoserLaSelection"/>), et ne lui laisse pas faire sa propre navigation. Le reste —
/// les flèches de côté, <c>Ctrl+←</c> et <c>Ctrl+→</c> le mot, l'effacement — reste au champ, qui le
/// fait sur le texte, indépendamment des lignes.
///
/// **L'affinité du curseur.** Une coupure douce donne la même position au bout d'une ligne et au
/// début de la suivante. La planche retient, avec l'abscisse visée, si le curseur est au bout de
/// la ligne qu'elle finit (<see cref="LigneVisee.EnFinDeLigne"/>) : c'est là que <c>Fin</c> le
/// dessine, et là qu'une flèche le pose quand elle arrive au bout d'une ligne renvoyée.
///
/// **La fenêtre de candidats** de la méthode d'entrée se pose au curseur dessiné. Avalonia la
/// place au rectangle que le client de la méthode d'entrée donne, dans le repère du champ qui a le
/// focus (<c>TextInputMethodManager.UpdateCursorRect</c>) ; celui du champ caché le prend sur ses
/// propres lignes. La planche enveloppe ce client (<see cref="ClientAuCurseurDessine"/>), qui
/// garde tout du champ sauf ce rectangle.
/// </summary>
public partial class PageCanvas
{
    /// <summary>
    /// Ce qu'une touche des lignes a laissé : le champ, son texte et sa sélection juste après, si
    /// le curseur est au bout de la ligne qu'une coupure douce finit, et l'abscisse visée par les
    /// flèches, dans la colonne de sa ligne — nulle après <c>Début</c> et <c>Fin</c>. Elle ne vaut
    /// que tant que le champ n'a pas bougé depuis (<see cref="LigneViseeTenue"/>).
    /// </summary>
    private sealed record LigneVisee(TextBox Zone, string Texte, int Ancre, int Curseur, bool EnFinDeLigne, double? Abscisse);

    private LigneVisee? _ligneVisee;

    /// <summary>Le rectangle du curseur que la méthode d'entrée lit, dans le repère du bloc, et la composition qui le porte.</summary>
    private (TextBox Zone, TexteCompose Compose, Rect Curseur)? _curseurDeLaMethode;

    /// <summary>Le client de la méthode d'entrée de chaque champ de la planche, enveloppé une fois.</summary>
    private readonly ConditionalWeakTable<TextBox, ClientAuCurseurDessine> _clientsDesChamps = new();

    /// <summary>
    /// Branche le clavier des lignes, au premier attachement de la planche (voir
    /// <c>OnAttachedToVisualTree</c>) : une pression perd l'abscisse visée, et le client de la
    /// méthode d'entrée du champ en saisie est enveloppé au passage montant de sa demande, après
    /// que le champ a donné le sien.
    /// </summary>
    private void BrancherLeClavierDesLignes()
    {
        AddHandler(PointerPressedEvent, (_, _) => _ligneVisee = null, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(TextInputMethodClientRequestedEvent, EnvelopperLeClientDuChamp, RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    /// <summary>
    /// Une touche du champ de la saisie, avant lui (appelée par <see cref="ToucheDeLaSaisie"/>) :
    /// <c>Début</c>, <c>Fin</c>, <c>↑</c>, <c>↓</c>, <c>Pg.Préc</c> ou <c>Pg.Suiv</c>, seule ou sous
    /// <c>Maj</c>, est calculée sur les lignes composées et rend vrai. Toute autre touche, sauf un
    /// modificateur seul, perd l'abscisse visée et reste au champ. Le pré-édit de la méthode
    /// d'entrée en cours, rien : le champ ne navigue pas non plus.
    /// </summary>
    private bool ToucheDesLignes(TextBox zone, KeyEventArgs e)
    {
        var maj = e.KeyModifiers == KeyModifiers.Shift;
        if ((e.KeyModifiers != KeyModifiers.None && !maj) ||
            e.Key is not (Key.Home or Key.End or Key.Up or Key.Down or Key.PageUp or Key.PageDown))
        {
            if (e.Key is not (Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt
                or Key.RightAlt or Key.LWin or Key.RWin))
            {
                _ligneVisee = null;
            }

            return false;
        }

        if (PreEditEnCours(zone) || _editing is not { } bloc || TexteComposeDe(bloc)?.Geometrie() is not { } geometrie)
        {
            return false;
        }

        var longueur = zone.Text?.Length ?? 0;
        var ancre = Math.Clamp(zone.SelectionStart, 0, longueur);
        var depuis = Math.Clamp(zone.SelectionEnd, 0, longueur);
        var tenue = LigneViseeTenue(zone);
        var finDeLigne = tenue?.EnFinDeLigne ?? false;
        var rang = geometrie.RangDuCurseur(depuis, finDeLigne);

        (int Position, bool EnFinDeLigne) arrivee;
        double? abscisse = null;
        switch (e.Key)
        {
            case Key.Home:
                arrivee = (geometrie.Lignes[rang].Ligne.Debut, false);
                break;
            case Key.End:
                arrivee = geometrie.FinDeLaLigne(rang);
                break;
            default:
                // L'abscisse visée se garde dans la colonne : la première ligne de la colonne
                // suivante est visée au même endroit de la sienne.
                abscisse = tenue?.Abscisse ?? geometrie.PointDe(depuis, finDeLigne).X - geometrie.DecalageDeColonne(rang);
                var cible = e.Key switch
                {
                    Key.Up => rang - 1,
                    Key.Down => rang + 1,
                    Key.PageUp => 0,
                    _ => geometrie.Lignes.Count - 1,
                };
                arrivee = cible < 0 ? (0, false)
                    : cible >= geometrie.Lignes.Count ? (longueur, false)
                    : geometrie.SurLaLigne(cible, abscisse.Value + geometrie.DecalageDeColonne(cible));
                break;
        }

        PoserLaSelection(zone, maj ? ancre : arrivee.Position, arrivee.Position);
        _ligneVisee = new LigneVisee(zone, zone.Text ?? "", zone.SelectionStart, zone.SelectionEnd, arrivee.EnFinDeLigne, abscisse);
        e.Handled = true;

        // L'affinité seule a pu changer — Fin au bout d'une ligne renvoyée où le curseur était déjà,
        // au début de la suivante : le champ n'en dit rien, les marques se reposent quand même.
        DemanderLesMarques();
        return true;
    }

    /// <summary>
    /// Ce que la dernière touche des lignes a laissé, si le champ n'a pas bougé depuis : ni frappe,
    /// ni clic, ni autre touche. Nul sinon — l'abscisse visée et l'affinité sont perdues.
    /// </summary>
    private LigneVisee? LigneViseeTenue(TextBox? zone)
    {
        if (_ligneVisee is not { } visee || zone is null || !ReferenceEquals(visee.Zone, zone)) return null;
        if (visee.Texte != (zone.Text ?? "") || visee.Ancre != zone.SelectionStart || visee.Curseur != zone.SelectionEnd)
        {
            _ligneVisee = null;
            return null;
        }

        return visee;
    }

    /// <summary>
    /// Le curseur de la sélection gardée est-il au bout de la ligne qu'une coupure douce finit ?
    /// C'est ce que <c>Fin</c>, ou une flèche arrivée au bout d'une ligne renvoyée, a laissé, tant
    /// que rien d'autre n'a bougé.
    /// </summary>
    private bool CurseurEnFinDeLigne(SelectionDuTexte selection) =>
        LigneViseeTenue(_zoneSuivie) is { EnFinDeLigne: true } visee &&
        EstLaSelectionDe(selection, visee.Zone.DataContext) && visee.Curseur == selection.Curseur;

    /// <summary>La méthode d'entrée compose-t-elle dans ce champ ? Le champ ne navigue pas pendant ce temps.</summary>
    private bool PreEditEnCours(TextBox zone)
    {
        var presentation = ReferenceEquals(zone, _zoneSuivie) && _presentationSuivie is not null
            ? _presentationSuivie
            : zone.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        return !string.IsNullOrEmpty(presentation?.PreeditText);
    }

    // ------------------------------------------------- la fenêtre de candidats

    /// <summary>
    /// Retient le curseur que la méthode d'entrée doit lire — dessiné, ou caché sous une sélection
    /// —, dans le repère du bloc, et le lui signale (appelée par <see cref="PoserLesMarquesDuTexte"/>).
    /// Rien hors saisie : le client rend alors le rectangle du champ.
    /// </summary>
    private void DireLeCurseurALaMethode(TexteCompose? compose, Rect? curseur)
    {
        var avant = _curseurDeLaMethode;
        _curseurDeLaMethode = _zoneSuivie is { } zone && compose is not null && curseur is { } rect
            ? (zone, compose, rect)
            : null;
        if (avant.Equals(_curseurDeLaMethode)) return;

        var zoneSignalee = _curseurDeLaMethode?.Zone ?? avant?.Zone;
        if (zoneSignalee is not null && _clientsDesChamps.TryGetValue(zoneSignalee, out var client)) client.Signaler();
    }

    /// <summary>
    /// Le rectangle du curseur dessiné, dans le repère du champ <paramref name="zone"/> — celui où
    /// le client du champ donne le sien —, rotation comprise par sa boîte englobante. Nul quand ce
    /// n'est pas le champ en saisie, ou que la composition n'est plus à l'écran.
    /// </summary>
    private Rect? CurseurDessineDans(TextBox zone)
    {
        if (_curseurDeLaMethode is not { } retenu || !ReferenceEquals(retenu.Zone, zone) ||
            !ReferenceEquals(_editing, zone.DataContext) || retenu.Compose.TransformToVisual(zone) is not { } transformation)
        {
            return null;
        }

        return retenu.Curseur.TransformToAABB(transformation);
    }

    /// <summary>
    /// La demande du client de la méthode d'entrée remonte du champ qui a le focus : pour un champ
    /// d'un bloc de texte de la planche, le client qu'il a donné est enveloppé, une fois par champ.
    /// </summary>
    private void EnvelopperLeClientDuChamp(object? sender, TextInputMethodClientRequestedEventArgs e)
    {
        if (e.Source is not TextBox zone || zone.DataContext is not TextFrame || !this.IsVisualAncestorOf(zone) ||
            e.Client is not { } client || client is ClientAuCurseurDessine)
        {
            return;
        }

        if (!_clientsDesChamps.TryGetValue(zone, out var enveloppe) || !ReferenceEquals(enveloppe.Champ, client))
        {
            enveloppe = new ClientAuCurseurDessine(client, () => CurseurDessineDans(zone));
            _clientsDesChamps.AddOrUpdate(zone, enveloppe);
        }

        e.Client = enveloppe;
    }
}

/// <summary>
/// Le client de la méthode d'entrée du champ de la saisie, enveloppé par la planche (poste N29) : il
/// rend tout ce que rend celui du champ — le texte autour du curseur, la sélection, le pré-édit, ses
/// avis —, sauf le rectangle du curseur, pris au curseur dessiné sur la composition quand il y en a
/// un. C'est là qu'Avalonia pose la fenêtre de candidats ; celui du champ caché la posait sur ses
/// propres lignes, que l'écran ne montre pas.
/// </summary>
internal sealed class ClientAuCurseurDessine : TextInputMethodClient
{
    private readonly Func<Rect?> _curseurDessine;

    public ClientAuCurseurDessine(TextInputMethodClient champ, Func<Rect?> curseurDessine)
    {
        Champ = champ;
        _curseurDessine = curseurDessine;
        champ.TextViewVisualChanged += (_, _) => RaiseTextViewVisualChanged();
        champ.CursorRectangleChanged += (_, _) => RaiseCursorRectangleChanged();
        champ.SurroundingTextChanged += (_, _) => RaiseSurroundingTextChanged();
        champ.SelectionChanged += (_, _) => RaiseSelectionChanged();
        champ.ResetRequested += (_, _) => RequestReset();
        champ.InputPaneActivationRequested += (_, _) => RaiseInputPaneActivationRequested();
    }

    /// <summary>Le client que le champ a donné.</summary>
    public TextInputMethodClient Champ { get; }

    public override Visual TextViewVisual => Champ.TextViewVisual;

    public override bool SupportsPreedit => Champ.SupportsPreedit;

    public override bool SupportsSurroundingText => Champ.SupportsSurroundingText;

    public override string SurroundingText => Champ.SurroundingText;

    /// <summary>Le curseur dessiné, dans le repère du champ ; à défaut, celui du champ.</summary>
    public override Rect CursorRectangle => _curseurDessine() ?? Champ.CursorRectangle;

    public override TextSelection Selection
    {
        get => Champ.Selection;
        set => Champ.Selection = value;
    }

    public override void SetPreeditText(string? preeditText) => Champ.SetPreeditText(preeditText);

    public override void SetPreeditText(string? preeditText, int? cursorPos) => Champ.SetPreeditText(preeditText, cursorPos);

    public override void ExecuteContextMenuAction(ContextMenuAction action) => Champ.ExecuteContextMenuAction(action);

    /// <summary>Le curseur dessiné a bougé : Avalonia relit le rectangle et déplace la fenêtre de candidats.</summary>
    public void Signaler() => RaiseCursorRectangleChanged();
}
