using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MyPublisher.Converters;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Vignette d'un modèle : la première page, dessinée en réduction. Le panneau Pages en
/// dérive la vignette de chaque page du document ouvert (<see cref="VignetteDePage"/>,
/// poste I5), qui choisit sa page, suit les retouches et ne se borde pas elle-même.
///
/// C'est un schéma et non un rendu fidèle, à dessein. À cent cinquante pixels de large,
/// un A4 réduit son corps de onze points à deux pixels : un rendu exact ne montrerait
/// qu'une bouillie grise. Ce qu'on choisit dans un catalogue, c'est une structure — où
/// sont les blocs, lesquels sont pleins, où le texte descend — et c'est cela qu'on
/// dessine. Le texte devient des filets, l'image un cadre barré, les formes gardent
/// leurs vraies couleurs.
///
/// C'est aussi ce qui évite un troisième moteur de rendu : l'écran et l'export partagent
/// le modèle sans couche intermédiaire, et une vignette fidèle aurait demandé de refaire
/// leur travail une fois de plus, avec le risque de diverger.
/// </summary>
public class TemplatePreview : Control
{
    public static readonly StyledProperty<Document?> DocumentProperty =
        AvaloniaProperty.Register<TemplatePreview, Document?>(nameof(Document));

    /// <summary>
    /// La page dessinée : nulle, la première, celle qu'un catalogue de modèles montre ;
    /// une autre pour la vignette d'une page du panneau Pages (poste I5).
    /// </summary>
    public static readonly StyledProperty<Page?> PageProperty =
        AvaloniaProperty.Register<TemplatePreview, Page?>(nameof(Page));

    static TemplatePreview() => AffectsRender<TemplatePreview>(DocumentProperty, PageProperty);

    public Document? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public Page? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    /// <summary>
    /// Le cadre gris autour de la feuille. Le panneau Pages borde ses vignettes lui-même,
    /// de l'accent pour la page courante : un second cadre doublerait le sien.
    /// </summary>
    protected virtual bool Encadree => true;

    /// <summary>
    /// Le fond de page du document, sous les blocs. Le catalogue ne le montre pas,
    /// comme avant I5 ; une page du document ouvert se montre avec le sien.
    /// </summary>
    protected virtual bool AvecLeFondDePage => false;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Les filets sont rangés par rang de bloc dans la page : ceux d'une autre page
        // ne vaudraient rien pour celle-ci.
        if (change.Property == PageProperty) Oublier();
    }

    /// <summary>
    /// Oublie les filets retenus : le prochain rendu recompose. Pour un appelant qui
    /// modifie en place le document qu'il montre — ce que le catalogue ne fait jamais, et
    /// ce que fait chaque retouche du document ouvert (poste I5).
    /// </summary>
    protected void Oublier()
    {
        _filetsDe = null;
        InvalidateVisual();
    }

    /// <summary>
    /// Oublie les filets d'un bloc posé sur la page dessinée, et de lui seul : un groupe
    /// avec tous ses enfants, un bloc de texte avec les siens. Les autres restent, et le
    /// prochain rendu ne recompose que lui. Un bloc que le dernier rendu n'a pas
    /// rencontré fait tout oublier.
    ///
    /// L'appelant garantit que la retouche ne change pas le NOMBRE de blocs de texte que
    /// le parcours rencontre sous ce bloc — sans quoi les rangs suivants glisseraient :
    /// un enfant ajouté à un groupe, une ligne ajoutée à un tableau font tout oublier
    /// (<see cref="Oublier()"/>).
    /// </summary>
    protected void Oublier(Frame bloc)
    {
        if (_filetsDe is not null && _rangsDe.TryGetValue(bloc, out var rangs))
        {
            for (var rang = rangs.Debut; rang < rangs.Fin && rang < _filets.Count; rang++) _filets[rang] = null;
            InvalidateVisual();
        }
        else
        {
            Oublier();
        }
    }

    private static readonly IBrush Paper = Brushes.White;
    private static readonly IPen Edge = new Pen(new SolidColorBrush(Color.Parse("#FFCBD2DA")));
    private static readonly IBrush Placeholder = new SolidColorBrush(Color.Parse("#FFE4E8ED"));
    private static readonly IPen PlaceholderEdge = new Pen(new SolidColorBrush(Color.Parse("#FFB6BFCA")));
    private static readonly IBrush BlankHint = new SolidColorBrush(Color.Parse("#FFF6F7F9"));

    /// <summary>
    /// Les filets de chaque bloc de texte, en coordonnées de la page, dans l'ordre où le
    /// rendu rencontre les blocs.
    ///
    /// Composer un bloc coûte : recomposée à chaque rendu, la vignette du bulletin
    /// prenait 2,5 à 2,7 ms par appel de <see cref="Render"/>, contre 0,06 ms en relisant
    /// ses filets — l'affiche 1,2 ms contre 0,03 (134 pixels de côté, moyenne de 500
    /// rendus, deux passes, poste chargé). On ne compose donc qu'au premier rendu, pour
    /// un document et une taille donnés.
    ///
    /// Le rang plutôt que le bloc sert de clé : les cases d'un tableau sont des blocs
    /// reconstruits à chaque lecture de <c>CellFrames</c>, et une clé par référence ne
    /// les retrouverait jamais. Le parcours, lui, ne change pas tant que le document ne
    /// change pas.
    ///
    /// Ce qui invalide la mémoire : un autre <see cref="Document"/>, ou une autre taille
    /// de contrôle — l'épaisseur des filets dépend de l'échelle. Ce qui ne l'invalide
    /// PAS : une modification du document en place. Le catalogue n'en fait aucune — ses
    /// documents sont lus sur le disque pour la vignette, et le modèle choisi est relu
    /// depuis son fichier —, mais un appelant qui éditerait le document affiché appelle
    /// <see cref="Oublier()"/>, comme le fait la vignette d'une page (poste I5). Changer de
    /// <see cref="Page"/> oublie de même. Un rang laissé vide (<see cref="Oublier(Frame)"/>)
    /// se recompose seul au rendu suivant.
    /// </summary>
    private readonly List<Rect[]?> _filets = [];
    private Document? _filetsDe;
    private Size _filetsA;
    private int _rang;

    /// <summary>
    /// Les rangs de filets de chaque bloc posé sur la page, du premier au suivant du
    /// dernier, tels que le dernier rendu les a parcourus : de quoi n'oublier qu'un bloc.
    /// </summary>
    private readonly Dictionary<Frame, (int Debut, int Fin)> _rangsDe = [];

    public override void Render(DrawingContext context)
    {
        var box = Bounds.Size;
        if (box.Width <= 0 || box.Height <= 0) return;

        if (Document is not { Pages.Count: > 0 } document)
        {
            // Page blanche : un rectangle vide, qui dit exactement ce qu'il propose.
            var empty = Fit(box, 595.28, 841.89);
            context.FillRectangle(BlankHint, empty);
            context.DrawRectangle(null, Edge, empty);
            return;
        }

        var page = Feuille(box, document);
        context.FillRectangle(Paper, page);
        if (AvecLeFondDePage && Conv.ToBrush(Aplat(document.Background, document.Charte)) is { } fond)
        {
            context.FillRectangle(fond, page);
        }

        if (Encadree) context.DrawRectangle(null, Edge, page);

        var scale = page.Width / document.PageWidth;
        // L'échelle verticale ne diffère de l'autre que pour une feuille étirée, et de
        // moins d'un pixel sur sa hauteur (voir Feuille).
        var scaleY = page.Height / document.PageHeight;
        var dessinee = Page ?? document.Pages[0];

        if (!ReferenceEquals(document, _filetsDe) || box != _filetsA)
        {
            _filets.Clear();
            _filetsDe = document;
            _filetsA = box;
        }

        _rang = 0;
        _rangsDe.Clear();

        using (context.PushClip(page))
        using (context.PushTransform(Matrix.CreateScale(scale, scaleY) * Matrix.CreateTranslation(page.X, page.Y)))
        {
            // Les couleurs prises dans le jeu se résolvent par la charte du modèle : sa
            // vignette montre ses couleurs à lui (poste C5b).
            var charte = document.Charte;
            foreach (var frame in dessinee.Frames)
            {
                var debut = _rang;
                Draw(context, frame, scale, charte);
                _rangsDe[frame] = (debut, _rang);
            }
        }
    }

    /// <summary>
    /// Où se dessine la feuille dans la boîte : le plus grand rectangle aux proportions de
    /// la page qui y tienne. La vignette d'une page prend la boîte entière : celle-ci est
    /// déjà au rapport de la page, à l'arrondi du pixel et au cadre près, et la feuille
    /// centrée y laisserait un filet blanc entre le cadre et un bandeau à fond perdu.
    /// </summary>
    protected virtual Rect Feuille(Size box, Document document) => Fit(box, document.PageWidth, document.PageHeight);

    /// <summary>Le plus grand rectangle aux proportions de la page qui tienne dans la boîte.</summary>
    private static Rect Fit(Size box, double width, double height)
    {
        if (width <= 0 || height <= 0) return new Rect(box);

        var scale = Math.Min(box.Width / width, box.Height / height);
        var size = new Size(width * scale, height * scale);
        return new Rect(new Point((box.Width - size.Width) / 2, (box.Height - size.Height) / 2), size);
    }

    /// <summary>
    /// L'aplat qui représente un remplissage dans une vignette.
    ///
    /// La vignette est un schéma et non un rendu — c'est l'écart que B1 a assumé, texte
    /// en filets et image en cadre barré — et il n'y a pas de raison de le rouvrir pour
    /// les remplissages riches : un dégradé de cent cinquante pixels de large ne se
    /// distingue pas de sa couleur de départ, et embarquer ici un troisième moteur de
    /// rendu serait la régression que tout l'équilibre du projet cherche à éviter. Un
    /// dégradé se donne donc par sa première couleur, une image par un gris neutre.
    /// </summary>
    private static string? Aplat(FillBrush? fill, Charte charte) => fill switch
    {
        GradientFill degrade => charte.Couleur(degrade.From),
        ImageFill => "#FFD5DAE1",
        _ => fill?.UniformeSelon(charte),
    };

    /// <summary>
    /// Dessine un bloc dans le repère de la page. Récursif comme le reste : un groupe
    /// translate le repère et redescend. Un bloc masqué n'est pas dessiné, ni rien de ce
    /// qu'il porte (poste I8) : la vignette montre la page telle qu'elle s'imprime.
    /// </summary>
    private void Draw(DrawingContext context, Frame frame, double scale, Charte charte)
    {
        if (!frame.Visible) return;

        using var _ = frame.Rotation == 0
            ? (IDisposable)new NoTransform()
            : context.PushTransform(
                Matrix.CreateTranslation(-frame.CenterX, -frame.CenterY) *
                Matrix.CreateRotation(frame.Rotation * Math.PI / 180) *
                Matrix.CreateTranslation(frame.CenterX, frame.CenterY));

        var rect = new Rect(frame.X, frame.Y, frame.Width, frame.Height);
        var fill = Conv.ToBrush(Aplat(frame.Fill, charte));
        var stroke = Conv.ToBrush(charte.Couleur(frame.Stroke));
        var pen = stroke is null || frame.StrokeThickness <= 0
            ? null
            : new Pen(stroke, Math.Max(frame.StrokeThickness, 0.5 / scale));

        switch (frame)
        {
            // Un trait (poste A10) : son tracé et ses pointes, par la fonction de la page.
            case ShapeFrame { IsLine: true } trait:
                if (pen is not null) TraitBloc.Tracer(context, trait.Dessin(), pen, pen.Brush!, rect.TopLeft);
                return;

            case ShapeFrame { Shape: ShapeKind.Ellipse }:
                context.DrawEllipse(fill, pen, rect.Center, rect.Width / 2, rect.Height / 2);
                return;

            case ShapeFrame:
                context.DrawRectangle(fill, pen, rect);
                return;

            case ImageFrame:
                context.DrawRectangle(fill ?? Placeholder, pen ?? PlaceholderEdge, rect);
                context.DrawLine(PlaceholderEdge, rect.TopLeft, rect.BottomRight);
                context.DrawLine(PlaceholderEdge, rect.TopRight, rect.BottomLeft);
                return;

            case GroupFrame group:
                if (fill is not null || pen is not null) context.DrawRectangle(fill, pen, rect);
                using (context.PushTransform(Matrix.CreateTranslation(frame.X, frame.Y)))
                {
                    foreach (var child in group.Children) Draw(context, child, scale, charte);
                }

                return;

            case TableFrame table:
                // Comme un groupe : ses cases sont des blocs calculés, en coordonnées
                // relatives. Sans ce cas, un modèle portant un tableau montrerait une
                // vignette vide — le switch n'a pas de branche par défaut.
                if (fill is not null || pen is not null) context.DrawRectangle(fill, pen, rect);
                using (context.PushTransform(Matrix.CreateTranslation(frame.X, frame.Y)))
                {
                    foreach (var cellule in table.CellFrames) Draw(context, cellule, scale, charte);
                }

                return;

            case TextFrame text:
                if (fill is not null || pen is not null) context.DrawRectangle(fill, pen, rect);
                DrawTextBars(context, text, scale, charte);
                return;
        }
    }

    /// <summary>
    /// Le texte, en filets : un par ligne que le bloc compose vraiment, à l'ordonnée et à
    /// la longueur que la composition lui donne, dans sa couleur. La dernière ligne d'un
    /// paragraphe est plus courte parce qu'elle l'est — c'est ce détail qui fait lire un
    /// pavé comme du texte.
    ///
    /// Les lignes viennent de <see cref="Compositeur"/>, la fonction que l'écran et
    /// l'export appellent, et non plus d'une estimation : jusqu'au poste Q9, la vignette
    /// espaçait ses filets de <c>FontSize × LineHeight</c> — l'interligne d'avant R4,
    /// qui ignore la hauteur naturelle de la police, 16 % plus petite que l'avance réelle
    /// en DejaVu Sans — et en comptait le nombre à la louche, un caractère valant un
    /// demi-corps. La vignette d'un modèle montrait donc un autre texte que la page
    /// qu'on allait ouvrir. Ce n'est toujours pas un rendu : les glyphes restent des
    /// filets. Mais ce sont les lignes de la page.
    /// </summary>
    private void DrawTextBars(DrawingContext context, TextFrame text, double scale, Charte charte)
    {
        var rang = _rang++;
        if (rang == _filets.Count) _filets.Add(null);

        var filets = _filets[rang] ??= Filets(text, scale, charte);
        if (filets.Length == 0) return;

        var brush = Conv.ToBrush(charte.Couleur(text.Foreground)) ?? Brushes.Black;

        // Découpé au bloc, comme la page l'est depuis A2 : une ligne qui sort du bloc
        // n'est pas imprimée, elle n'a pas à paraître dans la vignette.
        using var _ = context.PushClip(new Rect(text.X, text.Y, text.Width, text.Height));

        foreach (var filet in filets) context.FillRectangle(brush, filet);
    }

    /// <summary>Les rectangles des filets d'un bloc, en coordonnées de la page.</summary>
    private static Rect[] Filets(TextFrame text, double scale, Charte charte)
    {
        if (string.IsNullOrWhiteSpace(text.Text) && text.Field == FieldKind.None) return [];

        var width = text.Width - text.PaddingLeft - text.PaddingRight;
        var height = text.Height - text.PaddingTop - text.PaddingBottom;
        if (width <= 0 || height <= 0) return [];

        // Épaisseur du filet : celle de l'œil de la lettre, sans jamais descendre sous
        // le pixel, faute de quoi la vignette perdrait ses textes les plus fins.
        var weight = Math.Max(text.FontSize * 0.55, 1 / scale);
        var left = text.X + text.PaddingLeft;
        var top = text.Y + text.PaddingTop;

        var filets = new List<Rect>();
        foreach (var (x, y, run, advance) in Bars(text, width, height, charte))
        {
            filets.Add(new Rect(left + x, top + y + (advance - weight) / 2, run, weight));
        }

        return [.. filets];
    }

    /// <summary>
    /// Les filets d'un bloc, relatifs au coin de sa zone de texte : abscisse, sommet de
    /// la ligne, longueur, avance.
    /// </summary>
    private static IEnumerable<(double X, double Y, double Run, double Advance)> Bars(
        TextFrame text, double width, double height, Charte charte)
    {
        var metriques = new MetriquesAvalonia();
        // Le style commun et les fragments résolus par la charte du modèle, comme l'écran
        // et l'export : une police du jeu se mesure dans la police du jeu, et le nombre
        // de lignes en dépend (postes C5b et Q9).
        var commun = charte.StyleCommun(text);

        // Un champ tient sur une ligne, et son contenu n'est pas celui du catalogue — les
        // coordonnées de l'utilisateur, la date du jour où il ouvrira le document : on
        // lui garde un filet de toute la largeur, à l'avance de sa police.
        if (text.Field != FieldKind.None)
        {
            yield return (0, 0, width, Compositeur.Avance(metriques.Naturelle(commun), text.LineHeight));
            yield break;
        }

        var lignes = Compositeur.Composer(
            text.Text, charte.Fragments(text.Spans), commun, text.Paragraphs, text.Paragraph,
            width, height, text.Colonnage, text.LineHeight, metriques);
        var descente = Compositeur.Decalage(lignes, height, text.VerticalAlign);

        foreach (var ligne in lignes)
        {
            // Une ligne vide — un paragraphe vide — ne trace rien à l'écran non plus.
            if (ligne.Morceaux.Count == 0) continue;

            // Le fer et la cible sont ceux de la ligne, comme dans TexteCompose : une
            // ligne justifiée court jusqu'au bout de sa colonne, les autres s'arrêtent
            // où leur texte s'arrête.
            var justifiee = ligne.Fer == TextAlign.Justify && !ligne.FinDeParagraphe;
            var run = justifiee ? ligne.Disponible : ligne.Largeur;
            var x = ligne.Retrait + (justifiee ? 0 : ligne.Fer switch
            {
                TextAlign.Center => (ligne.Disponible - ligne.Largeur) / 2,
                TextAlign.Right => ligne.Disponible - ligne.Largeur,
                _ => 0,
            });

            yield return (x, descente + ligne.Haut, run, ligne.Avance);
        }
    }

    /// <summary>De quoi écrire « using » sans transformation à empiler.</summary>
    private sealed class NoTransform : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
