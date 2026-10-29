using System;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// La sélection du texte en saisie (poste N15 ; fiche 23, b1 et b4) : le bloc, ou la case d'un
/// tableau, et la plage [<see cref="Debut"/>, <see cref="Fin"/>) de son texte, le curseur à l'un
/// de ses deux bouts. Le début ne passe jamais la fin : un glissé à rebours donne la même plage
/// qu'à l'endroit, et c'est le curseur qui dit le sens — il est au début, l'ancre à la fin.
/// Une sélection vide est le curseur seul.
///
/// C'est ce que R6, R7, N20 et N22 lisent du texte en saisie : R6 les paragraphes que la plage
/// touche (<see cref="MainWindowViewModel.ParagraphesDeLaSelection"/>), R7 la plage à mettre en
/// forme, ou le mot du curseur quand elle est vide, N20 la plage où se pose un texte trouvé, et
/// la voie 2a (N21) le curseur et l'ancre qu'elle dessinera sur la composition. Les positions
/// sont celles du texte du modèle (<see cref="TextFrame.Text"/>), comme celles des fragments.
/// </summary>
/// <param name="Bloc">Le bloc en saisie : un bloc de texte, ou le tableau de la case.</param>
/// <param name="Case">La case du tableau dont le texte est en saisie ; nulle pour un bloc de texte.</param>
/// <param name="Debut">Le premier caractère sélectionné, ou le curseur.</param>
/// <param name="Fin">Le caractère qui suit le dernier sélectionné ; égale au début sans sélection.</param>
/// <param name="Curseur">Le curseur : le début ou la fin, le bout que le geste déplace.</param>
public sealed record SelectionDuTexte(Frame Bloc, TableCell? Case, int Debut, int Fin, int Curseur)
{
    /// <summary>Le bout que le geste ne déplace pas : l'autre que le curseur.</summary>
    public int Ancre => Curseur == Debut ? Fin : Debut;

    /// <summary>Rien n'est sélectionné : il n'y a que le curseur.</summary>
    public bool EstVide => Debut == Fin;

    /// <summary>La plage sélectionnée, au format des fragments.</summary>
    public Plage Plage => new(Debut, Fin - Debut);

    /// <summary>Le texte où la sélection est prise : celui de la case, ou celui du bloc.</summary>
    public string Texte => Case?.Text ?? (Bloc as TextFrame)?.Text ?? "";

    /// <summary>
    /// La sélection d'un champ de texte, telle qu'il la tient : de l'<paramref name="ancre"/>, où
    /// le geste a commencé, au <paramref name="curseur"/>, le bout qu'il déplace — avant l'ancre
    /// dans un glissé à rebours. C'est <c>SelectionStart</c> et <c>SelectionEnd</c> d'un
    /// <c>TextBox</c> : son <c>CaretIndex</c> ne suit pas <c>Maj+→</c> (mesuré). Le tout est
    /// ramené au texte, de <paramref name="longueur"/> caractères.
    /// </summary>
    public static SelectionDuTexte De(Frame bloc, TableCell? case_, int ancre, int curseur, int longueur)
    {
        var a = Math.Clamp(ancre, 0, longueur);
        var c = Math.Clamp(curseur, 0, longueur);
        return new SelectionDuTexte(bloc, case_, Math.Min(a, c), Math.Max(a, c), c);
    }
}

/// <summary>
/// Ce que la saisie du texte ajoute au modèle de vue. Un fichier à lui, comme la règle 10 de la
/// phase I le veut.
///
/// Entrer dans le texte d'un clic (poste N12) : la demande d'ouvrir la saisie d'un bloc qu'il
/// vient de poser. Un bloc de texte tracé à l'outil s'ouvre en saisie, vide, le curseur dedans :
/// c'est la planche qui le trace, et qui l'ouvre. Celui que pose le menu Insertion était posé
/// sans saisie, et il fallait double-cliquer dedans. Publisher y met le point d'insertion
/// (p. 33) ; le modèle de vue ne connaît pas la saisie, qui est l'affaire de la planche, et la
/// lui demande.
///
/// L'application connaît la sélection du texte (poste N15) : la planche, qui tient le champ de
/// la saisie, dit au modèle de vue ce qui y est sélectionné, à chaque geste, et le vide à la
/// sortie de la saisie. Elle seule le dit ; le modèle de vue le garde quand le focus va à la
/// barre ou au dock — le champ, lui, oublie sa sélection en perdant le focus.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Un bloc de texte vient d'être posé hors de la planche — par le menu Insertion — et
    /// demande la saisie, le curseur dedans. La planche l'écoute.
    /// </summary>
    public event EventHandler<TextFrame>? SaisieDemandee;

    private void DemanderLaSaisie(TextFrame bloc) => SaisieDemandee?.Invoke(this, bloc);

    private SelectionDuTexte? _selectionDuTexte;

    /// <summary>
    /// La sélection du texte en saisie ; nulle hors saisie. La planche la tient
    /// (<see cref="SuivreLaSelectionDuTexte"/>) : elle suit le champ tant qu'il a le focus, et
    /// survit quand le focus va à la barre, où un réglage la lira.
    /// </summary>
    public SelectionDuTexte? SelectionDuTexte => _selectionDuTexte;

    /// <summary>
    /// Les paragraphes que la sélection touche — le premier, et leur nombre —, comptés comme les
    /// écarts de paragraphe le sont (<see cref="Recalage.ParagrapheDe"/>, par
    /// <see cref="MiseEnForme.Paragraphes"/>) : du paragraphe de son premier caractère à celui de
    /// son dernier ; le curseur seul touche le paragraphe où il est. Nuls hors saisie, et pour une
    /// case de tableau, qui n'a pas de paragraphes.
    /// </summary>
    public Plage? ParagraphesDeLaSelection =>
        _selectionDuTexte is { Case: null, Bloc: TextFrame bloc } selection
            ? MiseEnForme.Paragraphes(bloc.Text ?? "", selection.Plage)
            : null;

    /// <summary>
    /// La planche dit ce qui est sélectionné dans le texte en saisie, ou, nul, que la saisie est
    /// refermée. Sans changement, rien n'est notifié.
    /// </summary>
    public void SuivreLaSelectionDuTexte(SelectionDuTexte? selection)
    {
        if (Equals(_selectionDuTexte, selection)) return;

        _selectionDuTexte = selection;
        OnPropertyChanged(nameof(SelectionDuTexte));
        OnPropertyChanged(nameof(ParagraphesDeLaSelection));
    }
}
