using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Taper dans une case, sur la page (poste N27 ; fiche 23, g2 à g4). Revient sur un choix écrit en
/// C9 : « une case ne se choisit pas sur la page ». On n'y saisissait qu'au dock, dans un champ de
/// 54 px en police de l'interface ; Publisher y tape au clic, et <c>Tab</c> passe de case en case.
///
/// <b>Le choix.</b> Une case n'existe qu'à la lecture : <see cref="TableFrame.CellFrames"/>
/// reconstruit ses blocs à chaque changement, et la liste qui les dessine reconstruit leurs
/// contrôles — chaque frappe change le texte de la case, donc tous les contrôles du tableau. Le
/// champ caché de la voie 2a (N21), posé dans le gabarit du bloc de texte comme pour un bloc de la
/// page ou le membre d'un groupe (N26), serait détruit à la première frappe. La saisie d'une case
/// passe donc par <b>un champ unique hors de la page</b>, posé dans l'<c>Overlay</c> le temps de la
/// saisie (<see cref="OuvrirLaCase"/>) : caché comme l'autre, il garde la frappe, la méthode
/// d'entrée, le presse-papiers et la navigation dans le texte, et <b>écrit
/// <see cref="TableCell.Saisie"/></b>, comme le champ du dock — ce qu'on tape d'un côté se voit de
/// l'autre, par construction. Les cases n'ont pas à être stables : le parti de C9 tient.
///
/// La planche garde le reste de la voie 2a tel quel. Le texte en saisie est un bloc de texte
/// <i>témoin</i> de la case, pris dans <see cref="TableFrame.CellFrames"/> à l'ouverture : il porte
/// les fragments mêmes de la case — un bloc de cases les emprunte — et son texte, que la planche
/// tient à jour, dans les deux sens (<see cref="SurLaCase"/>, <see cref="SurLeTemoin"/>). Il ne se
/// dessine nulle part : le contrôle qui dessine la case est retrouvé par ses fragments
/// (<see cref="DessineLeTexte"/>), à chaque reconstruction, et c'est sur lui que le clic, le
/// curseur, la sélection, le pré-édit, les caractères spéciaux de N22 et le trait de N25 se lisent
/// et se posent, par sa transformation — tableau tourné, ou groupe tourné qui le porte, compris.
///
/// <b>Le tableau reste la sélection</b>, et le bloc visé : la barre règle le tableau entier, le dock
/// sa case. Dans un groupe, le tableau est pris seul (N26). La sélection du texte porte la case
/// (<see cref="SelectionDuTexte.Case"/>), le tableau pour bloc.
///
/// <b>Le clavier</b>, avant le champ (<see cref="ToucheDeLaCase"/>) : <c>Tab</c> passe à la case
/// suivante, son texte sélectionné, <c>Maj+Tab</c> à la précédente ; <c>Tab</c> dans la dernière
/// case ajoute une ligne, en un pas ; <c>Ctrl+Tab</c> met une tabulation ; <c>←</c> au début du texte
/// et <c>→</c> à sa fin passent à la case d'avant ou d'après, <c>↑</c> sur la première ligne et
/// <c>↓</c> sur la dernière à celle du dessus ou du dessous, à la même abscisse. Chaque case fait son
/// pas : passer de l'une à l'autre referme la saisie et en ouvre une autre. <c>Échap</c> rend le
/// tableau.
/// </summary>
public partial class PageCanvas
{
    /// <summary>
    /// La saisie d'une case : son tableau, la case, le bloc témoin qui la tient pour la planche, et
    /// le champ hors de la page qui la tape.
    /// </summary>
    private sealed record SaisieDeCase(TableFrame Tableau, TableCell Case, TextFrame Temoin, TextBox Champ);

    /// <summary>Le nom du champ de la case, que les tests et les recettes cherchent.</summary>
    public const string NomDuChampDeLaCase = "ChampDeLaCase";

    private SaisieDeCase? _saisieDeCase;

    // ------------------------------------------------------------------ reconnaître la case

    /// <summary>
    /// Le contrôle dessine-t-il le bloc en saisie <paramref name="bloc"/> ? Le sien, ou, pour le
    /// témoin d'une case, celui qui porte les fragments de la case : le bloc qu'il dessine change à
    /// chaque reconstruction du tableau, les fragments restent ceux de la case.
    /// </summary>
    private bool DessineLeTexte(TexteCompose controle, TextFrame bloc) =>
        ReferenceEquals(controle.Bloc, bloc) ||
        (_saisieDeCase is { } saisie && ReferenceEquals(bloc, saisie.Temoin) && controle.Bloc is { } dessine &&
         ReferenceEquals(dessine.Spans, saisie.Case.Spans));

    /// <summary>Le bloc que dessine un contrôle de la page est-il le texte en saisie — le bloc, ou la case ?</summary>
    private bool EstDessineEnSaisie(TextFrame dessine) =>
        ReferenceEquals(dessine, _editing) ||
        (_saisieDeCase is { } saisie && ReferenceEquals(dessine.Spans, saisie.Case.Spans));

    /// <summary>
    /// La sélection du texte est-elle celle du bloc en saisie <paramref name="bloc"/> ? Pour une
    /// case, elle porte la case ; pour un bloc, le bloc, sans case.
    /// </summary>
    private bool EstLaSelectionDe(SelectionDuTexte selection, object? bloc) =>
        _saisieDeCase is { } saisie && ReferenceEquals(bloc, saisie.Temoin)
            ? ReferenceEquals(selection.Case, saisie.Case)
            : selection.Case is null && ReferenceEquals(selection.Bloc, bloc);

    /// <summary>
    /// La sélection du texte en saisie, telle que le modèle de vue la garde (N15) : le tableau et sa
    /// case pour une case, le bloc sinon.
    /// </summary>
    private SelectionDuTexte SelectionDeLaSaisie(TextFrame bloc, int ancre, int curseur, int longueur) =>
        _saisieDeCase is { } saisie && ReferenceEquals(bloc, saisie.Temoin)
            ? SelectionDuTexte.De(saisie.Tableau, saisie.Case, ancre, curseur, longueur)
            : SelectionDuTexte.De(bloc, null, ancre, curseur, longueur);

    /// <summary>Le contrôle qui dessine la case sur la page, s'il est à l'écran.</summary>
    private TexteCompose? TexteComposeDeLaCase(TableCell case_) =>
        Scaler.GetVisualDescendants().OfType<TexteCompose>()
            .FirstOrDefault(controle => controle.Bloc is { } dessine && ReferenceEquals(dessine.Spans, case_.Spans) &&
                                        controle.IsEffectivelyVisible);

    // ------------------------------------------------------------------ le clic

    /// <summary>
    /// La case sous le point de la page, dans le tableau qui finit le <paramref name="chemin"/> :
    /// l'intérieur du tableau moins sa bande de bord, lu dans son repère à travers ceux des groupes,
    /// rotation comprise. Le bord prend et déplace le tableau, comme celui d'un bloc de texte (N12) ;
    /// un croisement sans case n'a pas de texte où entrer.
    /// </summary>
    private (TableFrame Tableau, TableCell Case)? CaseSousLeChemin(IReadOnlyList<Frame> chemin, Point page)
    {
        if (chemin[^1] is not TableFrame { Visible: true } tableau) return null;

        var (lx, ly) = MainWindowViewModel.VersLeBloc(chemin, page.X, page.Y);
        var bandeX = Math.Min(BandeDeBord / Scale, tableau.Width / 4);
        var bandeY = Math.Min(BandeDeBord / Scale, tableau.Height / 4);
        if (lx <= bandeX || lx >= tableau.Width - bandeX || ly <= bandeY || ly >= tableau.Height - bandeY) return null;

        return tableau.CaseSous(lx, ly) is { } case_ ? (tableau, case_) : null;
    }

    /// <summary>
    /// Une pression sans <c>Ctrl</c> ni <c>Maj</c> dans une case : elle prend le tableau — ou le groupe
    /// qui le porte, le tableau pris seul dedans —, ouvre la saisie de la case le curseur au
    /// caractère visé, lu sur la composition à l'écran, et garde le geste, que le glissé changera en
    /// sélection de texte. Faux hors d'une case.
    /// </summary>
    private bool EntrerDansLaCase(IReadOnlyList<Frame> chemin, Point page, PointerPressedEventArgs e)
    {
        if (_vm is null || CaseSousLeChemin(chemin, page) is not var (tableau, case_)) return false;

        var longueur = case_.Text?.Length ?? 0;
        var compose = TexteComposeDeLaCase(case_);
        var curseur = compose?.Geometrie() is { } geometrie
            ? Math.Clamp(geometrie.IndexSous(e.GetPosition(compose)), 0, longueur)
            : longueur;

        // Le tableau, ou le groupe qui le porte, pris seul s'il ne l'était pas.
        var racine = chemin[0];
        if (_vm.Selection is not [var seul] || !ReferenceEquals(seul, racine)) _vm.SelectOnly(racine);
        OuvrirLaCase(tableau, case_, curseur, curseur);
        if (!ReferenceEquals(_saisieDeCase?.Case, case_)) return true;

        _ancreDuTexte = curseur;
        _uniteDuGlisse = UniteDuTexte.Caractere;
        _geometrieDuGlisse = null;
        _mode = DragMode.Texte;
        e.Pointer.Capture(InputLayer);
        return true;
    }

    /// <summary>
    /// Un texte trouvé dans une case par la recherche (N20) ou la vérification (N25) : la saisie de la
    /// case s'ouvre — ou reste ouverte, sur la même case —, le curseur au bout de la plage, que la
    /// planche sélectionne ensuite comme dans un bloc. Rend le témoin de la case en saisie ; rien hors
    /// d'une case.
    /// </summary>
    private TextFrame? OuvrirLaCaseTrouvee(SelectionDuTexte trouve)
    {
        if (trouve is not { Case: { } case_, Bloc: TableFrame tableau }) return null;

        if (!ReferenceEquals(_saisieDeCase?.Case, case_))
        {
            EndTextEdit();
            OuvrirLaCase(tableau, case_, trouve.Fin, trouve.Fin);
        }

        return ReferenceEquals(_saisieDeCase?.Case, case_) ? _saisieDeCase.Temoin : null;
    }

    /// <summary>
    /// La sélection du champ de la case, dite au modèle de vue avec le tableau et la case (N15).
    /// Faux hors de la saisie d'une case.
    /// </summary>
    private bool DireLaSelectionDeLaCase(TextBox zone)
    {
        if (_vm is null || _saisieDeCase is not { } saisie || !ReferenceEquals(zone, saisie.Champ)) return false;

        _vm.SuivreLaSelectionDuTexte(SelectionDuTexte.De(saisie.Tableau, saisie.Case, zone.SelectionStart, zone.SelectionEnd,
            zone.Text?.Length ?? 0));
        return true;
    }

    // ------------------------------------------------------------------ ouvrir, refermer

    /// <summary>
    /// Ouvre la saisie de la case, la sélection de <paramref name="ancre"/> à <paramref name="curseur"/>.
    /// Le champ est posé dans l'<c>Overlay</c>, sur la case, et prend le focus tout de suite : la
    /// transaction de la saisie le suit (Q25), et une touche qui suit <c>Tab</c> tombe déjà dans la
    /// case suivante. Le tableau doit être la sélection, ou porté par elle.
    /// </summary>
    private void OuvrirLaCase(TableFrame tableau, TableCell case_, int ancre, int curseur)
    {
        if (_editing is not null || _vm is null || !tableau.Visible) return;
        if (tableau.CellFrames.OfType<TextFrame>().FirstOrDefault(f => ReferenceEquals(f.Spans, case_.Spans)) is not { } temoin)
        {
            return;
        }

        // Le tableau d'un groupe est pris seul dans le groupe : le dock et la barre le visent (N26).
        if (_vm.CheminDe(tableau) is { Count: > 1 }) _vm.PrendreLeMembre(tableau);

        // Les champs des saisies refermées, cachés, s'en vont.
        foreach (var ancien in Overlay.Children.OfType<TextBox>().Where(c => c.Name == NomDuChampDeLaCase && !c.IsVisible).ToList())
        {
            Overlay.Children.Remove(ancien);
        }

        _editing = temoin;
        temoin.IsEditing = true;

        // Caché et hors du test de survol, comme le champ du gabarit (N21) ; sans le menu du thème
        // (A12). Il écrit la saisie de la case, comme le champ du dock : la frappe y recale les
        // fragments de la case (Q1), et les deux champs se suivent.
        var champ = new TextBox
        {
            Name = NomDuChampDeLaCase,
            Opacity = 0,
            IsHitTestVisible = false,
            ContextFlyout = null,
            AcceptsReturn = true,
            AcceptsTab = true,
            NewLine = "\n",
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Top,
            DataContext = temoin,
        };
        champ.Classes.Add("frameEditor");
        champ.Bind(IsVisibleProperty, new Binding(nameof(TextFrame.IsEditing)));
        champ.Bind(TextBox.TextProperty, new Binding(nameof(TableCell.Saisie)) { Source = case_, Mode = BindingMode.TwoWay });
        PoserLeChampSurLaCase(champ, case_);
        Overlay.Children.Add(champ);

        _saisieDeCase = new SaisieDeCase(tableau, case_, temoin, champ);
        _zoneDeSaisie = champ;
        case_.PropertyChanged += SurLaCase;
        temoin.PropertyChanged += SurLeTemoin;
        tableau.Cells.CollectionChanged += SurLesCases;
        tableau.PropertyChanged += SurLeTableau;

        _vm.SuivreLaCaseEnSaisie(tableau, case_);
        RefreshOverlay();

        champ.ApplyTemplate();
        champ.Focus();
        var longueur = champ.Text?.Length ?? 0;
        PoserLaSelection(champ, Math.Clamp(ancre, 0, longueur), Math.Clamp(curseur, 0, longueur));
    }

    /// <summary>
    /// Le champ se pose sur la case, de sa taille à l'écran : la méthode d'entrée ouvre sa fenêtre
    /// près de lui, à défaut du curseur dessiné (N29).
    /// </summary>
    private void PoserLeChampSurLaCase(TextBox champ, TableCell case_)
    {
        if (TexteComposeDeLaCase(case_) is not { } compose || compose.TranslatePoint(default, Overlay) is not { } coin)
        {
            champ.Width = 1;
            return;
        }

        Canvas.SetLeft(champ, coin.X);
        Canvas.SetTop(champ, coin.Y);
        champ.Width = Math.Max(1, compose.Bounds.Width * Scale);
        champ.Height = Math.Max(1, compose.Bounds.Height * Scale);
    }

    /// <summary>
    /// La saisie de la case se referme (appelée par <see cref="EndTextEdit"/>) : la planche cesse de
    /// tenir son témoin, et le modèle de vue l'oublie. Le champ, caché, reste dans l'<c>Overlay</c>
    /// jusqu'à la saisie suivante : il s'y cache comme celui d'un bloc, le focus le quittant de même.
    /// </summary>
    private void OublierLaCase()
    {
        if (_saisieDeCase is not { } saisie) return;

        _saisieDeCase = null;
        saisie.Case.PropertyChanged -= SurLaCase;
        saisie.Temoin.PropertyChanged -= SurLeTemoin;
        saisie.Tableau.Cells.CollectionChanged -= SurLesCases;
        saisie.Tableau.PropertyChanged -= SurLeTableau;
        _vm?.SuivreLaCaseEnSaisie(null, null);
    }

    /// <summary>
    /// Le texte de la case a changé — au champ de la page, au dock, par la recherche — : le témoin le
    /// suit, sans recalage, les fragments étant ceux de la case, déjà recalés.
    /// </summary>
    private void SurLaCase(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TableCell.Text) || _saisieDeCase is not { } saisie) return;
        if (!string.Equals(saisie.Temoin.Text, saisie.Case.Text, StringComparison.Ordinal)) saisie.Temoin.Text = saisie.Case.Text;
    }

    /// <summary>
    /// Le texte du témoin a changé — une correction d'orthographe (N25) l'écrit, comme celui d'un
    /// bloc — : la case le prend, sans recalage, les fragments partagés l'ayant déjà été.
    /// </summary>
    private void SurLeTemoin(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TextFrame.Text) || _saisieDeCase is not { } saisie) return;
        if (!string.Equals(saisie.Case.Text, saisie.Temoin.Text, StringComparison.Ordinal)) saisie.Case.Text = saisie.Temoin.Text;
    }

    /// <summary>La case quitte son tableau — une ligne retirée au dock — : sa saisie se referme.</summary>
    private void SurLesCases(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_saisieDeCase is not { } saisie || saisie.Tableau.Cells.Contains(saisie.Case)) return;
        RefermerLaCasePlusTard(saisie);
    }

    /// <summary>Le tableau se masque : la saisie de sa case se referme, comme celle d'un bloc masqué (I8).</summary>
    private void SurLeTableau(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Frame.Visible) || _saisieDeCase is not { } saisie || saisie.Tableau.Visible) return;
        RefermerLaCasePlusTard(saisie);
    }

    /// <summary>Refermée une fois la commande jouée, pour qu'elle garde son pas, comme pour un bloc masqué.</summary>
    private void RefermerLaCasePlusTard(SaisieDeCase saisie) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_saisieDeCase, saisie)) EndTextEdit();
        });

    // ------------------------------------------------------------------ le clavier

    /// <summary>
    /// Une touche du champ de la case, avant lui (appelée par <see cref="ToucheDeLaSaisie"/>) : les
    /// passages de case en case et la tabulation sous <c>Ctrl</c>. Rend vrai si elle est prise. Le
    /// pré-édit de la méthode d'entrée en cours, rien.
    /// </summary>
    private bool ToucheDeLaCase(TextBox zone, KeyEventArgs e)
    {
        if (_saisieDeCase is not { } saisie || !ReferenceEquals(zone, saisie.Champ) || PreEditEnCours(zone)) return false;

        var texte = zone.Text ?? "";
        var debut = Math.Min(zone.SelectionStart, zone.SelectionEnd);
        var fin = Math.Max(zone.SelectionStart, zone.SelectionEnd);
        var seul = e.KeyModifiers == KeyModifiers.None;
        var vide = debut == fin;

        switch (e.Key)
        {
            case Key.Tab when seul:
                AllerALaCaseSuivante(saisie);
                break;
            case Key.Tab when e.KeyModifiers == KeyModifiers.Shift:
                if (saisie.Tableau.CaseVoisine(saisie.Case, -1) is { } precedente)
                {
                    PasserALaCase(saisie, precedente, 0, precedente.Text?.Length ?? 0);
                }

                break;
            case Key.Tab when e.KeyModifiers == KeyModifiers.Control:
                PoserDansLeChamp(zone, "\t");
                break;
            case Key.Left when seul && vide && fin == 0:
                if (saisie.Tableau.CaseVoisine(saisie.Case, -1) is not { } avant) return false;
                PasserALaCase(saisie, avant, avant.Text?.Length ?? 0, avant.Text?.Length ?? 0);
                break;
            case Key.Right when seul && vide && fin == texte.Length:
                if (saisie.Tableau.CaseVoisine(saisie.Case, 1) is not { } apres) return false;
                PasserALaCase(saisie, apres, 0, 0);
                break;
            case Key.Up or Key.Down when seul && vide:
                return PasserALaLigneVoisine(saisie, zone, e.Key == Key.Up ? -1 : 1);
            default:
                return false;
        }

        e.Handled = true;
        return true;
    }

    /// <summary>
    /// <c>Tab</c> : la case suivante, son texte sélectionné, comme Publisher et Word ; dans la
    /// dernière, une ligne de plus, en un pas à elle, et sa première case.
    /// </summary>
    private void AllerALaCaseSuivante(SaisieDeCase saisie)
    {
        if (saisie.Tableau.CaseVoisine(saisie.Case, 1) is { } suivante)
        {
            PasserALaCase(saisie, suivante, 0, suivante.Text?.Length ?? 0);
            return;
        }

        // La frappe de la case referme son pas avant que la ligne n'ajoute le sien.
        EndTextEdit();
        if (_vm?.AjouterUneLigneAuTableau(saisie.Tableau) is { } premiere) OuvrirLaCase(saisie.Tableau, premiere, 0, 0);
    }

    /// <summary>
    /// <c>↑</c> sur la première ligne composée de la case, <c>↓</c> sur la dernière : la case du
    /// dessus ou du dessous, dans la colonne, sur sa dernière ou sa première ligne, à l'abscisse du
    /// curseur. Ailleurs, ou au bord du tableau, la touche reste aux lignes composées (N29).
    /// </summary>
    private bool PasserALaLigneVoisine(SaisieDeCase saisie, TextBox zone, int sens)
    {
        if (TexteComposeDeLaCase(saisie.Case)?.Geometrie() is not { } ici ||
            saisie.Tableau.CaseDansLaColonne(saisie.Case, sens) is not { } voisine ||
            TexteComposeDeLaCase(voisine)?.Geometrie() is not { } la)
        {
            return false;
        }

        var curseur = Math.Clamp(zone.SelectionEnd, 0, zone.Text?.Length ?? 0);
        var enFinDeLigne = LigneViseeTenue(zone)?.EnFinDeLigne ?? false;
        var rang = ici.RangDuCurseur(curseur, enFinDeLigne);
        if (sens < 0 ? rang > 0 : rang < ici.Lignes.Count - 1) return false;

        var abscisse = ici.PointDe(curseur, enFinDeLigne).X;
        var (position, _) = la.SurLaLigne(sens < 0 ? la.Lignes.Count - 1 : 0, abscisse);
        PasserALaCase(saisie, voisine, position, position);
        return true;
    }

    /// <summary>
    /// Passe de la case en saisie à <paramref name="cible"/>, du même tableau : la saisie de l'une se
    /// referme, avec son pas, et celle de l'autre s'ouvre, la sélection posée.
    /// </summary>
    private void PasserALaCase(SaisieDeCase saisie, TableCell cible, int ancre, int curseur)
    {
        EndTextEdit();
        OuvrirLaCase(saisie.Tableau, cible, ancre, curseur);
    }

    /// <summary>
    /// Écrit <paramref name="texte"/> dans le champ à la place de la sélection, le curseur après lui,
    /// comme le champ écrit une frappe : par son texte, donc par la saisie de la case. Écrit, et non
    /// rejoué en frappe synthétisée (recette N5).
    /// </summary>
    private static void PoserDansLeChamp(TextBox zone, string texte)
    {
        var avant = zone.Text ?? "";
        var debut = Math.Clamp(Math.Min(zone.SelectionStart, zone.SelectionEnd), 0, avant.Length);
        var fin = Math.Clamp(Math.Max(zone.SelectionStart, zone.SelectionEnd), debut, avant.Length);

        zone.SetCurrentValue(TextBox.TextProperty, avant[..debut] + texte + avant[fin..]);
        PoserLaSelection(zone, debut + texte.Length, debut + texte.Length);
    }
}
