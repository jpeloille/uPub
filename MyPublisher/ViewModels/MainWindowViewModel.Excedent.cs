using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// L'alerte du dock, à la section « Cadre de texte » : « Texte en excédent : 4 lignes »
/// (poste I9). Le compte est celui du noyau, <see cref="Compositeur.LignesEnExcedent"/>,
/// sur les lignes que l'écran compose — mêmes polices, même contenu résolu, même charte
/// que le texte de la planche —, et l'export, composé à sa règle, a les mêmes lignes en
/// moins. Rien de cela n'est dans le document : c'est une mesure, pas une propriété.
///
/// Dans son fichier partiel (règle 10 de la phase I), et accroché sans toucher au reste du
/// modèle de vue : il écoute ce que le modèle de vue annonce de lui-même — la sélection, le
/// document, la page, le jeu de polices — et, entre deux annonces, le bloc qu'il compte.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>Le bloc dont on suit les retouches pour le compte.</summary>
    private TextFrame? _blocCompte;

    private int _lignesEnExcedent;

    /// <summary>
    /// Combien de lignes du bloc sélectionné ne se montrent pas ; zéro sans bloc de texte
    /// sélectionné, ou quand il n'y en a qu'un parmi plusieurs.
    /// </summary>
    public int LignesEnExcedent
    {
        get => _lignesEnExcedent;
        private set
        {
            if (!SetProperty(ref _lignesEnExcedent, value)) return;
            OnPropertyChanged(nameof(TexteEnExcedent));
            OnPropertyChanged(nameof(AlerteDExcedent));
        }
    }

    /// <summary>L'alerte ne se montre qu'à partir d'une ligne : zéro ne s'annonce pas.</summary>
    public bool TexteEnExcedent => LignesEnExcedent > 0;

    /// <summary>Le libellé de la maquette, au singulier pour une ligne.</summary>
    public string AlerteDExcedent => LignesEnExcedent == 1
        ? "Texte en excédent : 1 ligne"
        : $"Texte en excédent : {LignesEnExcedent} lignes";

    /// <summary>
    /// Ce que le modèle de vue annonce, et qui change le compte : un autre bloc
    /// sélectionné, un autre document — l'ouverture, l'annulation, qui le relit —, une autre
    /// page ou un autre nombre de pages, qui changent un folio (<see cref="FieldContext"/>),
    /// un autre jeu de polices, qui change la police d'un texte pris dans le jeu.
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(SingleSelected):
                SuivreLeBlocCompte();
                break;
            case nameof(Document) or nameof(FieldContext) or nameof(ChosenFontScheme):
                CompterLesLignesEnExcedent();
                break;
        }
    }

    /// <summary>
    /// Change de bloc suivi. Le bloc, ses fragments et ses écarts de paragraphe, comme
    /// <c>ControleDeBloc</c> les suit pour le dessiner : la frappe change le texte du bloc
    /// sans changer la sélection, et l'alerte suit la frappe.
    /// </summary>
    private void SuivreLeBlocCompte()
    {
        if (_blocCompte is not null)
        {
            _blocCompte.PropertyChanged -= SurBlocCompteRetouche;
            _blocCompte.Spans.CollectionChanged -= SurBlocCompteRetouche;
            _blocCompte.Paragraphs.CollectionChanged -= SurBlocCompteRetouche;
        }

        _blocCompte = SingleSelected as TextFrame;

        if (_blocCompte is not null)
        {
            _blocCompte.PropertyChanged += SurBlocCompteRetouche;
            _blocCompte.Spans.CollectionChanged += SurBlocCompteRetouche;
            _blocCompte.Paragraphs.CollectionChanged += SurBlocCompteRetouche;
        }

        CompterLesLignesEnExcedent();
    }

    private void SurBlocCompteRetouche(object? sender, PropertyChangedEventArgs e) =>
        CompterLesLignesEnExcedent();

    private void SurBlocCompteRetouche(object? sender, NotifyCollectionChangedEventArgs e) =>
        CompterLesLignesEnExcedent();

    /// <summary>
    /// Compose le bloc suivi comme la planche le compose (<c>ControleDeBloc.Mesurer</c>) :
    /// la place du modèle et non celle du contrôle, le contenu passé par le résolveur de
    /// champs, les fragments et les écarts tombés pour un champ, le style résolu par la
    /// charte du document. Puis compte, par le noyau, ce qui ne se montre pas.
    /// </summary>
    private void CompterLesLignesEnExcedent()
    {
        if (_blocCompte is not { } bloc)
        {
            LignesEnExcedent = 0;
            return;
        }

        var contenu = FieldResolver.Resolve(bloc.Field, bloc.Text, FieldContext);
        if (string.IsNullOrEmpty(contenu))
        {
            LignesEnExcedent = 0;
            return;
        }

        var largeur = System.Math.Max(1, bloc.Width - bloc.PaddingLeft - bloc.PaddingRight);
        var hauteur = System.Math.Max(1, bloc.Height - bloc.PaddingTop - bloc.PaddingBottom);

        var charte = Document.Charte;
        var fragments = contenu == bloc.Text ? charte.Fragments(bloc.Spans) : (IReadOnlyList<StyleSpan>)[];
        var ecarts = contenu == bloc.Text ? bloc.Paragraphs : (IReadOnlyList<ParagraphSpan>)[];

        var lignes = Compositeur.Composer(
            contenu, fragments, charte.StyleCommun(bloc), ecarts, bloc.Paragraph,
            largeur, hauteur, bloc.Colonnage, bloc.LineHeight, new Views.MetriquesAvalonia());

        LignesEnExcedent = Compositeur.LignesEnExcedent(lignes, hauteur, bloc.PaddingBottom, bloc.VerticalAlign);
    }
}
