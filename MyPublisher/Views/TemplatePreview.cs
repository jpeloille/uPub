using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MyPublisher.Converters;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Vignette d'un modèle : la première page, dessinée en réduction.
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

    static TemplatePreview() => AffectsRender<TemplatePreview>(DocumentProperty);

    public Document? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    private static readonly IBrush Paper = Brushes.White;
    private static readonly IPen Edge = new Pen(new SolidColorBrush(Color.Parse("#FFCBD2DA")));
    private static readonly IBrush Placeholder = new SolidColorBrush(Color.Parse("#FFE4E8ED"));
    private static readonly IPen PlaceholderEdge = new Pen(new SolidColorBrush(Color.Parse("#FFB6BFCA")));
    private static readonly IBrush BlankHint = new SolidColorBrush(Color.Parse("#FFF6F7F9"));

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

        var page = Fit(box, document.PageWidth, document.PageHeight);
        context.FillRectangle(Paper, page);
        context.DrawRectangle(null, Edge, page);

        var scale = page.Width / document.PageWidth;

        using (context.PushClip(page))
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(page.X, page.Y)))
        {
            foreach (var frame in document.Pages[0].Frames) Draw(context, frame, scale);
        }
    }

    /// <summary>Le plus grand rectangle aux proportions de la page qui tienne dans la boîte.</summary>
    private static Rect Fit(Size box, double width, double height)
    {
        if (width <= 0 || height <= 0) return new Rect(box);

        var scale = Math.Min(box.Width / width, box.Height / height);
        var size = new Size(width * scale, height * scale);
        return new Rect(new Point((box.Width - size.Width) / 2, (box.Height - size.Height) / 2), size);
    }

    /// <summary>
    /// Dessine un bloc dans le repère de la page. Récursif comme le reste : un groupe
    /// translate le repère et redescend.
    /// </summary>
    private static void Draw(DrawingContext context, Frame frame, double scale)
    {
        using var _ = frame.Rotation == 0
            ? (IDisposable)new NoTransform()
            : context.PushTransform(
                Matrix.CreateTranslation(-frame.CenterX, -frame.CenterY) *
                Matrix.CreateRotation(frame.Rotation * Math.PI / 180) *
                Matrix.CreateTranslation(frame.CenterX, frame.CenterY));

        var rect = new Rect(frame.X, frame.Y, frame.Width, frame.Height);
        var fill = Conv.ToBrush(frame.Fill);
        var stroke = Conv.ToBrush(frame.Stroke);
        var pen = stroke is null || frame.StrokeThickness <= 0
            ? null
            : new Pen(stroke, Math.Max(frame.StrokeThickness, 0.5 / scale));

        switch (frame)
        {
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
                    foreach (var child in group.Children) Draw(context, child, scale);
                }

                return;

            case TableFrame table:
                // Comme un groupe : ses cases sont des blocs calculés, en coordonnées
                // relatives. Sans ce cas, un modèle portant un tableau montrerait une
                // vignette vide — le switch n'a pas de branche par défaut.
                if (fill is not null || pen is not null) context.DrawRectangle(fill, pen, rect);
                using (context.PushTransform(Matrix.CreateTranslation(frame.X, frame.Y)))
                {
                    foreach (var cellule in table.CellFrames) Draw(context, cellule, scale);
                }

                return;

            case TextFrame text:
                if (fill is not null || pen is not null) context.DrawRectangle(fill, pen, rect);
                DrawTextBars(context, text, scale);
                return;
        }
    }

    /// <summary>
    /// Le texte, en filets : un par ligne que le bloc peut recevoir, à sa hauteur
    /// d'interligne réelle, dans sa couleur. Le dernier est plus court, comme une fin
    /// de paragraphe — c'est ce détail qui fait lire un pavé comme du texte.
    /// </summary>
    private static void DrawTextBars(DrawingContext context, TextFrame text, double scale)
    {
        if (string.IsNullOrWhiteSpace(text.Text) && text.Field == FieldKind.None) return;

        var brush = Conv.ToBrush(text.Foreground) ?? Brushes.Black;
        var line = Math.Max(text.AbsoluteLineHeight, 1);
        var width = text.Width - text.PaddingLeft - text.PaddingRight;
        var height = text.Height - text.PaddingTop - text.PaddingBottom;
        if (width <= 0 || height <= 0) return;

        // Épaisseur du filet : celle de l'œil de la lettre, sans jamais descendre sous
        // le pixel, faute de quoi la vignette perdrait ses textes les plus fins.
        var weight = Math.Max(text.FontSize * 0.55, 1 / scale);
        var lines = Math.Max(1, (int)(height / line));

        // Un champ ou un titre tient sur une ligne : on ne lui en invente pas d'autres.
        if (text.Field != FieldKind.None) lines = 1;
        else lines = Math.Min(lines, EstimateLines(text, width));

        for (var i = 0; i < lines; i++)
        {
            var y = text.Y + text.PaddingTop + i * line;
            if (y + weight > text.Y + text.Height) break;

            var run = i == lines - 1 && lines > 1 ? width * 0.62 : width;
            var x = text.Align switch
            {
                TextAlign.Center => text.X + text.PaddingLeft + (width - run) / 2,
                TextAlign.Right => text.X + text.Width - text.PaddingRight - run,
                _ => text.X + text.PaddingLeft
            };

            context.FillRectangle(brush, new Rect(x, y, run, weight));
        }
    }

    /// <summary>
    /// Combien de lignes le texte occupe vraiment, estimé à la grosse : un caractère
    /// vaut à peu près la moitié du corps. Il ne s'agit pas de composer, seulement de
    /// ne pas remplir de filets un bloc qui ne porte que trois mots.
    /// </summary>
    private static int EstimateLines(TextFrame text, double width)
    {
        var perLine = Math.Max(1, width / Math.Max(text.FontSize * 0.5, 0.1));
        var total = 0;
        foreach (var paragraph in text.Text.Split('\n'))
        {
            total += Math.Max(1, (int)Math.Ceiling(paragraph.Length / perLine));
        }

        return Math.Max(1, total);
    }

    /// <summary>De quoi écrire « using » sans transformation à empiler.</summary>
    private sealed class NoTransform : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
