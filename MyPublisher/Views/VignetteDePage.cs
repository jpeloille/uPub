using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// La vignette d'une page du panneau Pages (phase I, poste I5) : la page, dessinée en
/// réduction comme celle d'un modèle — un schéma tiré de la composition, filets et cadres
/// barrés, et non des contrôles de bloc —, mais qui suit le document ouvert.
///
/// <see cref="TemplatePreview"/> fait le dessin et garde ses filets d'un rendu à l'autre ;
/// ce qu'il ne savait pas faire, et que la vignette d'une page ajoute :
/// - dessiner une autre page que la première (<see cref="TemplatePreview.Page"/>) ;
/// - oublier ses filets quand la page se modifie en place. Le document ouvert se retouche
///   sans être remplacé, et le catalogue ne le faisait jamais. La vignette écoute donc ce
///   que la planche écoute — chaque bloc, ses fragments et ses écarts de paragraphe, les
///   enfants d'un groupe — et le document, dont le format, les jeux et le fond de page
///   valent pour toutes les pages ;
/// - se passer du cadre gris : l'entrée du panneau la borde elle-même, de l'accent pour la
///   page courante (<c>EntreeDePage.axaml</c>) ; et prendre toute sa boîte, déjà au rapport
///   de la page ;
/// - montrer le fond de page, qui est celui du document.
///
/// Ce qu'elle écoute est sa page seule : retoucher un bloc de la page 2 ne fait recomposer
/// que la vignette 2, et dans celle-ci que les filets de ce bloc — sur ATA 21, 14 ms pour
/// le plus long texte au lieu de 40 pour toute la composition. Une annulation, elle,
/// remplace le document entier — et avec lui les pages, donc les entrées du panneau et
/// leurs vignettes, qui se dessinent à neuf.
///
/// Les abonnements se prennent à l'entrée dans l'arbre visuel et se rendent à la sortie,
/// comme ceux de <see cref="ControleDeBloc"/> (poste Q2) : une page vit aussi longtemps
/// que le document, bien plus que l'entrée qui la montre.
/// </summary>
public sealed class VignetteDePage : TemplatePreview
{
    protected override bool Encadree => false;

    protected override bool AvecLeFondDePage => true;

    protected override Rect Feuille(Size box, Document document) => new(box);

    /// <summary>Ce qu'on écoute, pour le rendre exactement : le document, la page, ses blocs.</summary>
    private Document? _documentEcoute;

    private Page? _pageEcoutee;

    /// <summary>
    /// Chaque bloc écouté — et chaque collection d'écarts d'un bloc de texte —, avec le bloc
    /// posé sur la page qui le porte : lui-même, ou le groupe où il est. C'est ce bloc-là
    /// dont la vignette oublie les filets quand il change, et lui seul.
    /// </summary>
    private readonly Dictionary<object, Frame> _blocDeLaPage = [];

    private bool _dansLArbre;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _dansLArbre = true;
        Ecouter();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _dansLArbre = false;
        Lacher();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Une entrée recyclée sur une autre page, ou un autre document posé : on lâche
        // l'ancien pour écouter le nouveau — hors de l'arbre, on n'écoute rien.
        if ((change.Property == DocumentProperty || change.Property == PageProperty) && _dansLArbre)
        {
            Ecouter();
        }
    }

    /// <summary>Lâche ce qu'on écoutait, puis écoute le document et la page de maintenant.</summary>
    private void Ecouter()
    {
        Lacher();

        _documentEcoute = Document;
        _pageEcoutee = Page;
        if (_documentEcoute is not null) _documentEcoute.PropertyChanged += SurDocument;
        if (_pageEcoutee is null) return;

        _pageEcoutee.Frames.CollectionChanged += SurPile;
        EcouterLesBlocs();
    }

    private void Lacher()
    {
        if (_documentEcoute is not null) _documentEcoute.PropertyChanged -= SurDocument;
        if (_pageEcoutee is not null) _pageEcoutee.Frames.CollectionChanged -= SurPile;
        _documentEcoute = null;
        _pageEcoutee = null;
        LacherLesBlocs();
    }

    private void EcouterLesBlocs()
    {
        if (_pageEcoutee is null) return;

        foreach (var bloc in _pageEcoutee.Frames) EcouterLeBloc(bloc, bloc);
    }

    /// <summary>
    /// Un bloc, et sous un groupe ses enfants. Les cases d'un tableau ne s'écoutent pas une
    /// à une : ce sont des blocs reconstruits à chaque lecture, et le tableau dit lui-même
    /// qu'elles ont changé (<c>CellFrames</c>).
    /// </summary>
    private void EcouterLeBloc(Frame bloc, Frame deLaPage)
    {
        _blocDeLaPage[bloc] = deLaPage;
        bloc.PropertyChanged += SurBloc;

        switch (bloc)
        {
            case TextFrame texte:
                _blocDeLaPage[texte.Spans] = deLaPage;
                _blocDeLaPage[texte.Paragraphs] = deLaPage;
                texte.Spans.CollectionChanged += SurEcarts;
                texte.Paragraphs.CollectionChanged += SurEcarts;
                break;
            case GroupFrame groupe:
                groupe.Children.CollectionChanged += SurPile;
                foreach (var enfant in groupe.Children) EcouterLeBloc(enfant, deLaPage);
                break;
        }
    }

    private void LacherLesBlocs()
    {
        foreach (var ecoute in _blocDeLaPage.Keys)
        {
            switch (ecoute)
            {
                case Frame bloc:
                    bloc.PropertyChanged -= SurBloc;
                    if (bloc is GroupFrame groupe) groupe.Children.CollectionChanged -= SurPile;
                    break;
                case INotifyCollectionChanged ecarts:
                    ecarts.CollectionChanged -= SurEcarts;
                    break;
            }
        }

        _blocDeLaPage.Clear();
    }

    /// <summary>
    /// Un bloc posé, retiré ou déplacé dans la pile — de la page ou d'un groupe : on
    /// réécoute toute la page, ce qui est plus sûr que de suivre le détail des indices, et
    /// ne coûte que des abonnements. Les rangs des filets ont pu glisser : tout s'oublie.
    /// </summary>
    private void SurPile(object? sender, NotifyCollectionChangedEventArgs e)
    {
        LacherLesBlocs();
        EcouterLesBlocs();
        Oublier();
    }

    /// <summary>
    /// Un bloc retouché : seuls ses filets se recomposent — ceux du groupe qui le porte,
    /// s'il en a un. Sur une grande page, la frappe dans un bloc ne recompose pas les
    /// autres à chaque lettre. Un tableau fait exception : une ligne ou une colonne de plus
    /// change le nombre de ses cases, et les rangs des blocs suivants avec lui.
    /// </summary>
    private void SurBloc(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is TableFrame || sender is null || !_blocDeLaPage.TryGetValue(sender, out var deLaPage)) Oublier();
        else Oublier(deLaPage);
    }

    private void SurEcarts(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not null && _blocDeLaPage.TryGetValue(sender, out var deLaPage)) Oublier(deLaPage);
        else Oublier();
    }

    /// <summary>
    /// Le document vaut pour toutes les pages. Le jeu de polices change la mesure, donc les
    /// lignes, et le format l'échelle, donc l'épaisseur des filets : tout se recompose. Le
    /// reste — le fond de page, le jeu de couleurs, que le rendu relit à chaque fois — ne
    /// demande qu'un nouveau dessin.
    /// </summary>
    private void SurDocument(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Models.Document.FontScheme) or nameof(Models.Document.PageWidth)
            or nameof(Models.Document.PageHeight))
        {
            Oublier();
        }
        else
        {
            InvalidateVisual();
        }
    }
}
