using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MyPublisher.Models;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;

namespace MyPublisher.Services;

public sealed record ExportReport(int PageCount, IReadOnlyList<string> Warnings);

/// <summary>
/// Export PDF. Le modèle étant déjà en points avec l'origine en haut à gauche et
/// l'axe Y descendant, il se transpose tel quel dans le repère PDFsharp : aucune
/// mise à l'échelle, donc aucune dérive d'arrondi.
/// </summary>
public static class PdfExporter
{
    public static ExportReport Export(Document document, string path)
    {
        FontconfigFontResolver.EnsureInstalled();

        var warnings = new List<string>();

        // Pas de « using » ici : PDFsharp invalide le document lors du Save et
        // lève une exception si on tente ensuite de le libérer.
        var pdf = new PdfDocument();
        pdf.Info.Title = document.Title;
        pdf.Info.Creator = "MyPublisher";

        foreach (var page in document.Pages)
        {
            var pdfPage = pdf.AddPage();
            pdfPage.Width = XUnit.FromPoint(document.PageWidth);
            pdfPage.Height = XUnit.FromPoint(document.PageHeight);

            using var gfx = XGraphics.FromPdfPage(pdfPage);

            foreach (var frame in page.Frames) DrawFrame(gfx, frame, warnings);
        }

        var pageCount = pdf.PageCount;
        pdf.Save(path);
        return new ExportReport(pageCount, warnings);
    }

    /// <summary>
    /// Dessine un bloc dans le repère courant. Récursif : un groupe translate le repère
    /// et redescend, ses enfants étant en coordonnées relatives. La rotation s'applique
    /// avant la translation, si bien que celle d'un groupe se compose avec celle de
    /// chacun de ses blocs sans qu'aucun calcul n'ait à les combiner.
    /// </summary>
    private static void DrawFrame(XGraphics gfx, Frame frame, List<string> warnings)
    {
        var state = gfx.Save();
        if (frame.Rotation != 0)
        {
            gfx.TranslateTransform(frame.CenterX, frame.CenterY);
            gfx.RotateTransform(frame.Rotation);
            gfx.TranslateTransform(-frame.CenterX, -frame.CenterY);
        }

        try
        {
            switch (frame)
            {
                case ShapeFrame shape: DrawShape(gfx, shape); break;
                case TextFrame text: DrawText(gfx, text); break;
                case ImageFrame image: DrawImage(gfx, image, warnings); break;
                case GroupFrame group: DrawGroup(gfx, group, warnings); break;
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"{frame.DisplayName} : {ex.Message}");
        }

        gfx.Restore(state);
    }

    private static void DrawGroup(XGraphics gfx, GroupFrame group, List<string> warnings)
    {
        gfx.TranslateTransform(group.X, group.Y);
        foreach (var child in group.Children) DrawFrame(gfx, child, warnings);
    }

    private static void DrawShape(XGraphics gfx, ShapeFrame shape)
    {
        var rect = new XRect(shape.X, shape.Y, shape.Width, shape.Height);
        var brush = ParseBrush(shape.Fill);
        var pen = ParsePen(shape.Stroke, shape.StrokeThickness);

        if (brush is null && pen is null) return;

        if (shape.Shape == ShapeKind.Ellipse)
        {
            if (brush is not null && pen is not null) gfx.DrawEllipse(pen, brush, rect);
            else if (brush is not null) gfx.DrawEllipse(brush, rect);
            else gfx.DrawEllipse(pen!, rect);
            return;
        }

        if (shape.CornerRadius > 0)
        {
            // PDFsharp attend le diamètre des coins, pas le rayon.
            var corner = new XSize(shape.CornerRadius * 2, shape.CornerRadius * 2);
            if (brush is not null && pen is not null) gfx.DrawRoundedRectangle(pen, brush, rect, corner);
            else if (brush is not null) gfx.DrawRoundedRectangle(brush, rect, corner);
            else gfx.DrawRoundedRectangle(pen!, rect, corner);
            return;
        }

        if (brush is not null && pen is not null) gfx.DrawRectangle(pen, brush, rect);
        else if (brush is not null) gfx.DrawRectangle(brush, rect);
        else gfx.DrawRectangle(pen!, rect);
    }

    private static void DrawText(XGraphics gfx, TextFrame text)
    {
        // Le fond couvre le bloc entier, marge intérieure comprise, et se dessine même
        // sans texte : un bloc vide muni d'un fond est un aplat voulu. ParseBrush rend
        // null pour un fond absent comme pour un alpha nul, si bien qu'un bloc sans fond
        // ne pose rien du tout et laisse voir ce qui est dessous.
        var background = ParseBrush(text.Background);
        if (background is not null)
            gfx.DrawRectangle(background, new XRect(text.X, text.Y, text.Width, text.Height));

        if (string.IsNullOrEmpty(text.Text)) return;

        var style = XFontStyleEx.Regular;
        if (text.Bold) style |= XFontStyleEx.Bold;
        if (text.Italic) style |= XFontStyleEx.Italic;

        var font = new XFont(text.FontFamily, text.FontSize, style);
        var brush = ParseBrush(text.Foreground) ?? XBrushes.Black;

        var box = new XRect(
            text.X + text.Padding,
            text.Y + text.Padding,
            Math.Max(1, text.Width - 2 * text.Padding),
            Math.Max(1, text.Height - 2 * text.Padding));

        var formatter = new XTextFormatter(gfx)
        {
            Alignment = text.Align switch
            {
                TextAlign.Center => XParagraphAlignment.Center,
                TextAlign.Right => XParagraphAlignment.Right,
                TextAlign.Justify => XParagraphAlignment.Justify,
                _ => XParagraphAlignment.Left
            }
        };

        formatter.DrawString(text.Text, font, brush, box, XStringFormats.TopLeft);
    }

    private static void DrawImage(XGraphics gfx, ImageFrame frame, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(frame.Path) || !File.Exists(frame.Path))
        {
            warnings.Add($"Image introuvable : {frame.Path}");
            return;
        }

        using var image = XImage.FromFile(frame.Path);
        var target = FitRect(frame, image.PixelWidth, image.PixelHeight);

        // Un cadrage en « remplir » déborde du bloc : on découpe au bloc.
        if (frame.Fit == ImageFit.UniformToFill)
        {
            var clip = new XGraphicsPath();
            clip.AddRectangle(new XRect(frame.X, frame.Y, frame.Width, frame.Height));
            var state = gfx.Save();
            gfx.IntersectClip(clip);
            gfx.DrawImage(image, target);
            gfx.Restore(state);
            return;
        }

        gfx.DrawImage(image, target);
    }

    /// <summary>Calcule le rectangle de destination selon le mode de cadrage.</summary>
    private static XRect FitRect(ImageFrame frame, double pixelWidth, double pixelHeight)
    {
        if (frame.Fit == ImageFit.Fill || pixelWidth <= 0 || pixelHeight <= 0)
            return new XRect(frame.X, frame.Y, frame.Width, frame.Height);

        var scaleX = frame.Width / pixelWidth;
        var scaleY = frame.Height / pixelHeight;
        var scale = frame.Fit == ImageFit.UniformToFill
            ? Math.Max(scaleX, scaleY)
            : Math.Min(scaleX, scaleY);

        var width = pixelWidth * scale;
        var height = pixelHeight * scale;
        return new XRect(
            frame.X + (frame.Width - width) / 2,
            frame.Y + (frame.Height - height) / 2,
            width, height);
    }

    private static XBrush? ParseBrush(string? color)
    {
        var parsed = ParseColor(color);
        return parsed is null ? null : new XSolidBrush(parsed.Value);
    }

    private static XPen? ParsePen(string? color, double thickness)
    {
        if (thickness <= 0) return null;
        var parsed = ParseColor(color);
        return parsed is null ? null : new XPen(parsed.Value, thickness);
    }

    /// <summary>Analyse une couleur au format #AARRGGBB ou #RRGGBB.</summary>
    private static XColor? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var hex = value.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        if (hex.Length != 8 ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return null;
        }

        var a = (byte)((packed >> 24) & 0xFF);
        if (a == 0) return null;

        return XColor.FromArgb(a, (byte)((packed >> 16) & 0xFF), (byte)((packed >> 8) & 0xFF), (byte)(packed & 0xFF));
    }
}
