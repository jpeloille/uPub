using System;
using System.Collections.Generic;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Taper dans le texte d'un groupe, et prendre un membre seul (poste N26 ; fiche 20 ; fiche 23, a6
/// et c15). Le livre retape le texte d'un élément de la bibliothèque <b>dans le groupe</b>, sans le
/// dissocier, et prend un membre d'un clic pour en remplacer l'image (p. 58-60) ; seul le
/// redimensionnement d'un membre y demande de dissocier. La sélection de MyPublisher ne voit que le
/// premier niveau de la page, et la saisie exigeait un bloc de texte seul : il fallait dissocier.
///
/// La sélection ne change pas de modèle : elle reste faite des blocs de la page. Deux états s'y
/// ajoutent, que la planche tient et dit ici :
///
///   - <see cref="TexteEnSaisie"/>, le bloc de texte dont la saisie est ouverte sur la page — un
///     bloc de la page, ou le membre d'un groupe, le groupe restant alors la sélection ;
///   - <see cref="MembreSeul"/>, le membre d'un groupe sélectionné qu'un clic a pris seul.
///
/// <see cref="BlocVise"/> dit lequel la barre, le dock, la mise en forme des caractères (R7, N24)
/// et celle des paragraphes (R6) règlent : le texte en saisie, sinon le membre pris seul, sinon le
/// bloc seul de la sélection — le même que <see cref="SingleSelected"/> tant qu'aucun membre n'est
/// visé. La suppression, les flèches et l'alignement d'un membre visé restent dans son groupe :
/// <see cref="SupprimerLeMembreVise"/>, <see cref="DeplacerLeMembreVise"/> et
/// <see cref="AlignerLeMembreVise"/>, que <c>DeleteSelected</c>, <c>NudgeSelection</c> et
/// <c>AlignEach</c> consultent d'abord. Le redimensionner reste réservé à la dissociation, comme
/// dans Publisher : ses poignées sont dessinées sans se laisser prendre, et « Position et taille »
/// ne se règle pas (<see cref="PositionReglable"/>).
/// </summary>
public partial class MainWindowViewModel
{
    private TextFrame? _texteEnSaisie;
    private Frame? _membreSeul;

    /// <summary>Le bloc visé tel qu'il a été annoncé : un avis ne part que s'il change.</summary>
    private Frame? _blocViseAnnonce;

    /// <summary>
    /// Le bloc de texte dont la saisie est ouverte sur la page, nul hors saisie : un bloc de la page
    /// — c'est alors le bloc seul de la sélection —, ou le membre d'un groupe, qui reste la sélection.
    /// La planche le dit (<see cref="SuivreLeTexteEnSaisie"/>).
    /// </summary>
    public TextFrame? TexteEnSaisie => _texteEnSaisie;

    /// <summary>
    /// Le membre d'un groupe sélectionné qu'un clic a pris seul : ses poignées, le dock et la barre le
    /// visent. Nul sans lui ; une autre sélection l'oublie.
    /// </summary>
    public Frame? MembreSeul => _membreSeul;

    /// <summary>
    /// Ce que la barre, le dock et les mises en forme règlent : le texte en saisie, le membre pris
    /// seul, ou le bloc seul de la sélection.
    /// </summary>
    public Frame? BlocVise => _texteEnSaisie ?? _membreSeul ?? SingleSelected;

    /// <summary>Le bloc visé quand c'est le membre d'un groupe de la page ; nul sinon.</summary>
    public Frame? MembreVise =>
        BlocVise is { } bloc && CurrentPage is { } page && !page.Frames.Contains(bloc) && CheminDe(bloc) is { Count: > 1 }
            ? bloc
            : null;

    /// <summary>
    /// « Position et taille » se règle au dock : pas pour un membre visé, qu'on ne redimensionne
    /// qu'après avoir dissocié son groupe.
    /// </summary>
    public bool PositionReglable => MembreVise is null;

    /// <summary>
    /// Le chemin d'un bloc depuis la page : le bloc de la page qui le porte, les groupes traversés,
    /// puis lui. Nul s'il n'est pas sur la page courante.
    /// </summary>
    public IReadOnlyList<Frame>? CheminDe(Frame bloc)
    {
        if (CurrentPage is not { } page) return null;

        var chemin = new List<Frame>();
        return Chercher(page.Frames) ? chemin : null;

        bool Chercher(IEnumerable<Frame> blocs)
        {
            foreach (var candidat in blocs)
            {
                chemin.Add(candidat);
                if (ReferenceEquals(candidat, bloc)) return true;
                if (candidat is GroupFrame groupe && Chercher(groupe.Children)) return true;
                chemin.RemoveAt(chemin.Count - 1);
            }

            return false;
        }
    }

    /// <summary>
    /// Un point du repère du dernier bloc du <paramref name="chemin"/>, mené sur la page : par le
    /// repère de chaque groupe traversé, rotation comprise, du plus profond au bloc de la page.
    /// </summary>
    public static (double X, double Y) VersLaPage(IReadOnlyList<Frame> chemin, double x, double y)
    {
        for (var i = chemin.Count - 1; i >= 0; i--) (x, y) = chemin[i].ToPage(x, y);
        return (x, y);
    }

    /// <summary>L'inverse de <see cref="VersLaPage"/> : un point de la page, dans le repère du dernier bloc du chemin.</summary>
    public static (double X, double Y) VersLeBloc(IReadOnlyList<Frame> chemin, double x, double y)
    {
        foreach (var bloc in chemin) (x, y) = bloc.ToLocal(x, y);
        return (x, y);
    }

    /// <summary>
    /// Un déplacement sur la page, dans le repère du groupe qui porte le dernier bloc du chemin : les
    /// rotations des groupes traversés retirées. C'est là que se comptent sa place et ses bords.
    /// </summary>
    public static (double Dx, double Dy) DansLeGroupe(IReadOnlyList<Frame> chemin, double dx, double dy)
    {
        var rotation = 0.0;
        for (var i = 0; i < chemin.Count - 1; i++) rotation += chemin[i].Rotation;
        if (rotation % 360 == 0) return (dx, dy);

        var radians = -rotation * Math.PI / 180.0;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        return (dx * cos - dy * sin, dx * sin + dy * cos);
    }

    /// <summary>
    /// Le déplacement d'un membre retenu à son groupe : son emprise, <paramref name="depart"/>, ne
    /// sort pas du cadre du groupe, de taille <paramref name="largeur"/> × <paramref name="hauteur"/> —
    /// la découpe de Q5 rognerait ce qui en sortirait —, et un membre qui en sortait déjà n'en sort
    /// pas davantage.
    /// </summary>
    public static (double Dx, double Dy) BorneAuGroupe(Extent depart, double largeur, double hauteur, double dx, double dy) =>
        (Borner(dx, depart.Left, depart.Right, largeur), Borner(dy, depart.Top, depart.Bottom, hauteur));

    private static double Borner(double ecart, double debut, double fin, double cote) =>
        ecart < 0 ? Math.Max(ecart, -Math.Max(debut, 0)) : Math.Min(ecart, Math.Max(cote - fin, 0));

    // ------------------------------------------------------------------ ce que la planche dit

    /// <summary>
    /// La planche ouvre la saisie d'un bloc de texte, ou, nul, la referme. Entrer dans le texte d'un
    /// membre le vise à la place du membre pris seul, qui est oublié.
    /// </summary>
    public void SuivreLeTexteEnSaisie(TextFrame? bloc)
    {
        if (ReferenceEquals(_texteEnSaisie, bloc)) return;

        _texteEnSaisie = bloc;
        if (bloc is not null) _membreSeul = null;
        OnPropertyChanged(nameof(TexteEnSaisie));

        // Pendant un changement de sélection, l'avis de la sélection suit, et dira le bloc visé.
        if (!_syncingSelection) NotifierLeBlocVise(parLaSelection: false);
    }

    /// <summary>
    /// Un clic prend seul ce membre du groupe sélectionné, ou, nul, rend le groupe. Un bloc qui n'est
    /// pas membre du groupe sélectionné seul ne se prend pas.
    /// </summary>
    public void PrendreLeMembre(Frame? membre)
    {
        if (membre is not null &&
            (Selection.Count != 1 || CheminDe(membre) is not { Count: > 1 } chemin || !ReferenceEquals(chemin[0], Selection[0])))
        {
            membre = null;
        }

        if (ReferenceEquals(_membreSeul, membre)) return;

        _membreSeul = membre;
        OnPropertyChanged(nameof(MembreSeul));
        NotifierLeBlocVise(parLaSelection: false);
        Status = membre is null
            ? SelectionLabel
            : $"{NatureDe(membre)} du groupe pris seul : il se règle dans le groupe ; dissociez pour le redimensionner.";
    }

    /// <summary>
    /// <c>Échap</c> sur un membre pris seul rend son groupe, comme on remonte d'un niveau. Faux sans
    /// membre pris seul.
    /// </summary>
    public bool RendreLeGroupe()
    {
        if (_membreSeul is null) return false;

        PrendreLeMembre(null);
        return true;
    }

    /// <summary>
    /// La sélection a changé (<see cref="SingleSelected"/> vient d'être annoncé) : le membre pris
    /// seul ne survit qu'à une sélection qui est toujours son groupe, seul. Le bloc visé est annoncé
    /// chaque fois, comme le bloc seul l'était : ceux qui le suivent se recalent.
    /// </summary>
    private void SuivreLeMembreApresLaSelection()
    {
        if (_membreSeul is { } membre &&
            (Selection.Count != 1 || CheminDe(membre) is not { Count: > 1 } chemin || !ReferenceEquals(chemin[0], Selection[0])))
        {
            _membreSeul = null;
            OnPropertyChanged(nameof(MembreSeul));
        }

        NotifierLeBlocVise(parLaSelection: true);
    }

    /// <summary>
    /// Annonce le bloc visé. Par la sélection, l'avis part toujours, et le reste — la disposition, la
    /// géométrie, les entrées vers le dock — suit dans <c>NotifySelection</c>. Hors d'elle, rien ne
    /// part s'il n'a pas changé : la saisie d'un bloc de la page le laisse où il était.
    /// </summary>
    private void NotifierLeBlocVise(bool parLaSelection)
    {
        var vise = BlocVise;
        var change = !ReferenceEquals(vise, _blocViseAnnonce);
        _blocViseAnnonce = vise;
        if (!change && !parLaSelection) return;

        OnPropertyChanged(nameof(BlocVise));
        OnPropertyChanged(nameof(MembreVise));
        OnPropertyChanged(nameof(PositionReglable));
        if (parLaSelection) return;

        NotifierLaDisposition();
        NotifyFrameGeometry();
        NotifierLesEntreesVersLeDock();
    }

    // ------------------------------------------------------------------ dans le groupe

    /// <summary>
    /// <c>Suppr</c>, Édition ▸ Supprimer, <c>Ctrl+Maj+X</c> sur un membre visé : il quitte son groupe,
    /// et lui seul, en un pas. Un groupe qu'il laisse vide s'en va avec lui. Le groupe de la page
    /// reste la sélection ; vidé, il quitte la page. Faux sans membre visé.
    /// </summary>
    private bool SupprimerLeMembreVise()
    {
        if (CurrentPage is not { } page || MembreVise is not { } membre || CheminDe(membre) is not { Count: > 1 } chemin)
        {
            return false;
        }

        BeginEdit();
        ((GroupFrame)chemin[^2]).Children.Remove(membre);
        for (var i = chemin.Count - 2; i >= 1 && ((GroupFrame)chemin[i]).Children.Count == 0; i--)
        {
            ((GroupFrame)chemin[i - 1]).Children.Remove(chemin[i]);
        }

        var racine = (GroupFrame)chemin[0];
        if (racine.Children.Count == 0) page.Frames.Remove(racine);

        // Une sélection posée de nouveau : la saisie du membre se referme, le membre pris seul s'oublie.
        SetSelection(page.Frames.Contains(racine) ? [racine] : []);
        CommitEdit();
        Status = $"{NatureDe(membre)} retiré du groupe";
        return true;
    }

    /// <summary>
    /// Les flèches sur un membre visé : il se déplace dans son groupe, du pas de la planche, dans le
    /// sens de la flèche à l'écran — les rotations des groupes traversées —, retenu au cadre du
    /// groupe. Faux sans membre visé.
    /// </summary>
    private bool DeplacerLeMembreVise(double dx, double dy, bool fine)
    {
        if (MembreVise is not { } membre || CheminDe(membre) is not { Count: > 1 } chemin) return false;

        var pas = fine ? 0.5 : 3.0;
        var groupe = chemin[^2];
        var (lx, ly) = DansLeGroupe(chemin, dx * pas, dy * pas);
        (lx, ly) = BorneAuGroupe(membre.Extent, groupe.Width, groupe.Height, lx, ly);
        if (Math.Abs(lx) < 1e-9 && Math.Abs(ly) < 1e-9)
        {
            Status = "Le bloc est au bord de son groupe.";
            return true;
        }

        BeginEdit();
        membre.X += lx;
        membre.Y += ly;
        CommitEdit();
        return true;
    }

    /// <summary>
    /// Une copie du dernier bloc du chemin, posée sur la page là où il se voit : son centre mené par
    /// les repères de ses groupes, leurs rotations ajoutées à la sienne — comme la dissociation le
    /// pose. Copier ou couper un membre visé le met ainsi au presse-papiers.
    /// </summary>
    private static Frame CopieSurLaPage(IReadOnlyList<Frame> chemin)
    {
        var membre = chemin[^1];
        var copie = membre.Clone();
        var (cx, cy) = VersLaPage(chemin, membre.Width / 2, membre.Height / 2);
        var rotation = 0.0;
        foreach (var bloc in chemin) rotation += bloc.Rotation;

        copie.Rotation = ((rotation % 360) + 360) % 360;
        copie.X = cx - membre.Width / 2;
        copie.Y = cy - membre.Height / 2;
        return copie;
    }

    /// <summary>
    /// Aligner un membre visé : sur le cadre de son groupe, et non sur la page — il y reste. Faux sans
    /// membre visé.
    /// </summary>
    private bool AlignerLeMembreVise(Func<Extent, Extent, (double Dx, double Dy)> decalage, string libelle)
    {
        if (MembreVise is not { } membre || CheminDe(membre) is not { Count: > 1 } chemin) return false;

        if (!membre.Visible)
        {
            Status = "Un bloc masqué ne s'aligne pas.";
            return true;
        }

        var groupe = chemin[^2];
        var (dx, dy) = decalage(membre.Extent, new Extent(0, 0, groupe.Width, groupe.Height));
        BeginEdit();
        membre.X += dx;
        membre.Y += dy;
        CommitEdit();
        Status = $"{libelle} dans le groupe";
        return true;
    }
}
