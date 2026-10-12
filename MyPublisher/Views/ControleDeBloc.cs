using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyPublisher.Converters;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Ce qu'un contrôle a besoin de savoir d'un bloc de texte pour le dessiner : ses
/// lignes composées et la zone qu'il leur laisse.
/// </summary>
public readonly record struct TexteMesure(
    IReadOnlyList<LigneComposee> Lignes, double Largeur, double Hauteur);

/// <summary>
/// Base des contrôles qui regardent un bloc de texte : le texte lui-même
/// (<see cref="TexteCompose"/>) et le marqueur de débordement
/// (<see cref="MarqueurDebordement"/>).
///
/// Elle porte le bloc, le contenu résolu, et surtout l'abonnement. « AffectsRender »
/// ne se déclenche que si la VALEUR de la propriété change : régler le corps ou un
/// retrait depuis le panneau ne remplace pas l'instance du bloc, et la page ne se
/// redessinait pas. C'est le défaut que R1 avait introduit et qui a survécu quatre
/// postes. Il ne doit pas revenir par une seconde copie du même code — d'où cette
/// base commune plutôt qu'un abonnement recopié dans chaque contrôle.
/// </summary>
public abstract class ControleDeBloc : Control
{
    public static readonly StyledProperty<TextFrame?> BlocProperty =
        AvaloniaProperty.Register<ControleDeBloc, TextFrame?>(nameof(Bloc));

    public static readonly StyledProperty<string?> ContenuProperty =
        AvaloniaProperty.Register<ControleDeBloc, string?>(nameof(Contenu));

    static ControleDeBloc()
    {
        AffectsRender<ControleDeBloc>(BlocProperty, ContenuProperty);
        BlocProperty.Changed.AddClassHandler<ControleDeBloc>((c, e) => c.Suivre(e));
        // Un changement de jeu ne touche pas le bloc : c'est la charte héritée qui change,
        // et un texte qui y fait référence change de couleur, ou de police et donc de
        // coupure (poste C5b).
        JeuAffiche.CharteProperty.Changed.AddClassHandler<ControleDeBloc>((c, _) => c.InvalidateVisual());
    }

    /// <summary>Le bloc, pour son style commun, ses fragments et ses marges.</summary>
    public TextFrame? Bloc
    {
        get => GetValue(BlocProperty);
        set => SetValue(BlocProperty, value);
    }

    /// <summary>
    /// Le texte à afficher, déjà passé par le résolveur de champs : un folio vu ici
    /// est le folio imprimé.
    /// </summary>
    public string? Contenu
    {
        get => GetValue(ContenuProperty);
        set => SetValue(ContenuProperty, value);
    }

    /// <summary>
    /// On s'abonne au bloc et à ses deux collections d'écarts — ajouter un fragment de
    /// style ne change aucune propriété du bloc. S'abonner aux collections une fois
    /// suffit : elles ne se remplacent pas après la construction du bloc (poste Q17).
    /// </summary>
    private void Suivre(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is TextFrame ancien) Desabonner(ancien);

        // Hors de l'arbre visuel, on ne s'abonne pas : c'est l'attachement qui le fera.
        if (e.NewValue is TextFrame nouveau && this.GetVisualRoot() is not null) Abonner(nouveau);
    }

    /// <summary>
    /// L'abonnement ne vaut que tant que le contrôle est affiché — poste Q2, le remède
    /// que C5 avait appliqué à <see cref="FondBloc"/>.
    ///
    /// Un bloc vit bien plus longtemps que le contrôle qui le dessine, et c'est le bloc
    /// qui tient l'abonnement : un contrôle sorti de l'arbre sans s'être désabonné reste
    /// retenu par le bloc, avec tout ce qu'il retient lui-même, et se fait redessiner à
    /// chaque frappe pour une page que personne ne regarde.
    ///
    /// Mesuré au poste Q2, et il faut l'écrire parce que le plan disait autre chose :
    /// changer de page ne laissait PAS d'abonné. La liste des blocs de la page — un
    /// « ItemsControl » — retire le contexte de données des gabarits qu'elle démonte ;
    /// la liaison du bloc retombe alors à null, et le changement de valeur ci-dessus
    /// désabonne. La fuite ne frappait qu'un contrôle qui quitte l'arbre en gardant son
    /// bloc — une fenêtre qu'on ferme sur un document qui reste ouvert, un aperçu, une
    /// vignette : les postes B6 et C7 en poseront. Le remède ne dépend plus de ce que
    /// fait le conteneur.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Bloc is not { } bloc) return;

        // Retirer avant d'ajouter : deux attachements sans détachement entre eux ne
        // doivent pas compter double.
        Desabonner(bloc);
        Abonner(bloc);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Bloc is { } bloc) Desabonner(bloc);
    }

    private void Abonner(TextFrame bloc)
    {
        bloc.PropertyChanged += SurChangement;
        bloc.Spans.CollectionChanged += SurCollection;
        bloc.Paragraphs.CollectionChanged += SurCollection;
    }

    private void Desabonner(TextFrame bloc)
    {
        bloc.PropertyChanged -= SurChangement;
        bloc.Spans.CollectionChanged -= SurCollection;
        bloc.Paragraphs.CollectionChanged -= SurCollection;
    }

    private void SurChangement(object? _, System.ComponentModel.PropertyChangedEventArgs __) =>
        InvalidateVisual();

    private void SurCollection(object? _, NotifyCollectionChangedEventArgs __) =>
        InvalidateVisual();

    /// <summary>
    /// Compose le bloc pour la place dont ce contrôle dispose. Les deux contrôles
    /// passent par ici : le texte tracé et le marqueur qui dit qu'il déborde doivent
    /// parler de la même composition, sinon l'un signalerait ce que l'autre ne montre
    /// pas.
    /// </summary>
    protected TexteMesure? Mesurer(Compositeur.IMetriques metriques)
    {
        if (Bloc is not { } bloc || string.IsNullOrEmpty(Contenu)) return null;

        var largeur = Math.Max(1, Bounds.Width - bloc.PaddingLeft - bloc.PaddingRight);
        var hauteur = Math.Max(1, Bounds.Height - bloc.PaddingTop - bloc.PaddingBottom);

        // Le style et les fragments arrivent résolus par la charte du document, la même
        // fonction que l'export appelle : une police du jeu se mesure dans la police du
        // jeu, ce qui décide des coupures.
        var charte = JeuAffiche.De(this);
        var commun = charte.StyleCommun(bloc);

        // Les fragments repèrent des positions dans le texte saisi ; un champ résolu
        // n'a pas les mêmes caractères, et n'est jamais qu'un seul style. Les écarts de
        // paragraphe tombent pour la même raison — un champ n'a pas les mêmes
        // paragraphes. Les réglages du bloc, eux, valent toujours : ce sont des
        // propriétés du bloc, comme ses marges intérieures.
        var fragments = Contenu == bloc.Text ? charte.Fragments(bloc.Spans) : (IReadOnlyList<StyleSpan>)[];
        var ecarts = Contenu == bloc.Text ? bloc.Paragraphs : (IReadOnlyList<ParagraphSpan>)[];

        var lignes = Compositeur.Composer(
            Contenu, fragments, commun, ecarts, bloc.Paragraph,
            largeur, hauteur, bloc.Colonnage, bloc.LineHeight, metriques);

        return new TexteMesure(lignes, largeur, hauteur);
    }
}

/// <summary>La règle à mesurer d'Avalonia, prêtée au compositeur du noyau.</summary>
public sealed class MetriquesAvalonia : Compositeur.IMetriques
{
    private readonly Dictionary<StyleEffectif, Typeface> _polices = [];

    public double Largeur(string texte, StyleEffectif style) =>
        Composer(texte, style).WidthIncludingTrailingWhitespace;

    public double Naturelle(StyleEffectif style) =>
        InterligneConverter.HauteurNaturelle(
            style.FontFamily, style.FontSize, style.Bold, style.Italic);

    public double Montee(StyleEffectif style)
    {
        var metriques = Police(style).GlyphTypeface.Metrics;
        var em = (double)metriques.DesignEmHeight;
        return em > 0 ? -metriques.Ascent * style.FontSize / em : style.FontSize;
    }

    public FormattedText Composer(string texte, StyleEffectif style) =>
        new(texte, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Police(style), style.FontSize,
            Conv.ToBrush(style.Foreground) ?? Brushes.Black);

    private Typeface Police(StyleEffectif style)
    {
        if (_polices.TryGetValue(style, out var connue)) return connue;

        var typeface = new Typeface(
            new FontFamily(style.FontFamily),
            style.Italic ? FontStyle.Italic : FontStyle.Normal,
            style.Bold ? FontWeight.Bold : FontWeight.Normal);

        _polices[style] = typeface;
        return typeface;
    }
}
