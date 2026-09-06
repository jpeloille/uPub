using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Écrit le PDF. Les coordonnées sont passées par l'appelant et non lues ici : elles
    /// appartiennent à l'utilisateur, pas au document, et l'export ne doit pas dépendre
    /// d'un réglage qu'il irait chercher lui-même.
    /// </summary>
    /// <param name="date">
    /// Date que portent les champs, ou <c>null</c> pour lire l'horloge. Elle se passe
    /// pour qu'un export soit reproductible : sans elle, tout document portant un champ
    /// de date produit un PDF différent chaque jour, et aucune comparaison à une
    /// référence figée n'est possible.
    /// </param>
    public static ExportReport Export(
        Document document, string path, BusinessInfo? contact = null, DateTime? date = null)
    {
        FontconfigFontResolver.EnsureInstalled();

        var warnings = new List<string>();

        // Pas de « using » ici : PDFsharp invalide le document lors du Save et
        // lève une exception si on tente ensuite de le libérer.
        var pdf = new PdfDocument();
        pdf.Info.Title = document.Title;
        pdf.Info.Creator = "MyPublisher";

        // Une seule lecture de l'horloge : toutes les pages d'un même export portent
        // la même date, même si le document est long à écrire.
        var now = date ?? DateTime.Now;

        for (var index = 0; index < document.Pages.Count; index++)
        {
            var page = document.Pages[index];
            var context = new FieldContext(index + 1, document.Pages.Count, now, contact ?? new BusinessInfo());
            var pdfPage = pdf.AddPage();
            pdfPage.Width = XUnit.FromPoint(document.PageWidth);
            pdfPage.Height = XUnit.FromPoint(document.PageHeight);

            using var gfx = XGraphics.FromPdfPage(pdfPage);

            foreach (var frame in page.Frames) DrawFrame(gfx, frame, warnings, context);
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
    private static void DrawFrame(XGraphics gfx, Frame frame, List<string> warnings, FieldContext context)
    {
        var state = gfx.Save();
        if (frame.Rotation != 0)
        {
            gfx.TranslateTransform(frame.CenterX, frame.CenterY);
            gfx.RotateTransform(frame.Rotation);
            gfx.TranslateTransform(-frame.CenterX, -frame.CenterY);
        }

        if (frame is ShapeFrame shape)
        {
            DrawShape(gfx, shape);
        }
        else
        {
            // Fond dessous, contenu, contour dessus : un filet posé sur une image
            // passe sur son bord et non sous elle — c'est aussi l'ordre de l'écran.
            DrawFill(gfx, frame);

            // Seul le contenu est sous surveillance : une image illisible se signale et
            // laisse son cadre en place, comme une image manquante laisse le sien.
            try
            {
                switch (frame)
                {
                    case TextFrame text: DrawText(gfx, text, context); break;
                    case ImageFrame image: DrawImage(gfx, image, warnings, context); break;
                    case GroupFrame group: DrawChildren(gfx, group, warnings, context); break;
                    case TableFrame table: DrawTable(gfx, table, warnings, context); break;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"{frame.DisplayName} : {ex.Message}");
            }

            DrawStroke(gfx, frame);
        }

        gfx.Restore(state);
    }

    private static void DrawChildren(XGraphics gfx, GroupFrame group, List<string> warnings, FieldContext context)
    {
        // La translation ne vaut que pour les enfants : le contour du groupe, tracé
        // ensuite, est en coordonnées de page comme son fond.
        var state = gfx.Save();
        gfx.TranslateTransform(group.X, group.Y);
        foreach (var child in group.Children) DrawFrame(gfx, child, warnings, context);
        gfx.Restore(state);
    }

    /// <summary>
    /// Un tableau se dessine comme un groupe, et pour la même raison : ses cases sont
    /// des blocs de texte calculés à partir de ses colonnes, de ses lignes et de ses
    /// cellules, en coordonnées relatives. L'export n'a donc aucun code de tableau qui
    /// lui soit propre — il redescend dans celui du bloc de texte, exactement comme
    /// l'écran. Les deux rendus sont d'accord par construction, sans qu'il faille les
    /// comparer pour s'en assurer.
    /// </summary>
    private static void DrawTable(XGraphics gfx, TableFrame table, List<string> warnings, FieldContext context)
    {
        var state = gfx.Save();
        gfx.TranslateTransform(table.X, table.Y);
        foreach (var cell in table.CellFrames) DrawFrame(gfx, cell, warnings, context);
        gfx.Restore(state);
    }

    /// <summary>Une forme n'a pas de contenu : fond et contour se tracent d'un seul trait.</summary>
    private static void DrawShape(XGraphics gfx, ShapeFrame shape) =>
        DrawOutline(gfx, shape, ParseBrush(shape.Fill), MakePen(shape));

    /// <summary>
    /// Le fond couvre le bloc entier, marge intérieure comprise, et se dessine même sans
    /// contenu : un bloc vide muni d'un fond est un aplat voulu. ParseBrush rend null pour
    /// un fond absent comme pour un alpha nul, si bien qu'un bloc sans fond ne pose rien
    /// du tout et laisse voir ce qui est dessous.
    /// </summary>
    private static void DrawFill(XGraphics gfx, Frame frame) =>
        DrawOutline(gfx, frame, ParseBrush(frame.Fill), null);

    private static void DrawStroke(XGraphics gfx, Frame frame) =>
        DrawOutline(gfx, frame, null, MakePen(frame));

    /// <summary>
    /// Trace le pourtour du bloc avec le fond et le contour qu'on lui passe : une ellipse
    /// pour la forme qui en est une, sinon un rectangle, arrondi s'il l'est.
    /// </summary>
    private static void DrawOutline(XGraphics gfx, Frame frame, XBrush? brush, XPen? pen)
    {
        if (brush is null && pen is null) return;

        var rect = new XRect(frame.X, frame.Y, frame.Width, frame.Height);

        if (frame is ShapeFrame { Shape: ShapeKind.Ellipse })
        {
            if (brush is not null && pen is not null) gfx.DrawEllipse(pen, brush, rect);
            else if (brush is not null) gfx.DrawEllipse(brush, rect);
            else gfx.DrawEllipse(pen!, rect);
            return;
        }

        if (frame.CornerRadius > 0)
        {
            // PDFsharp attend le diamètre des coins, pas le rayon.
            var corner = new XSize(frame.CornerRadius * 2, frame.CornerRadius * 2);
            if (brush is not null && pen is not null) gfx.DrawRoundedRectangle(pen, brush, rect, corner);
            else if (brush is not null) gfx.DrawRoundedRectangle(brush, rect, corner);
            else gfx.DrawRoundedRectangle(pen!, rect, corner);
            return;
        }

        if (brush is not null && pen is not null) gfx.DrawRectangle(pen, brush, rect);
        else if (brush is not null) gfx.DrawRectangle(brush, rect);
        else gfx.DrawRectangle(pen!, rect);
    }

    private static XPen? MakePen(Frame frame)
    {
        var pen = ParsePen(frame.Stroke, frame.StrokeThickness);
        if (pen is not null && Frame.DashPattern(frame.StrokeStyle) is { } pattern)
        {
            // PDFsharp multiplie le motif par l'épaisseur, comme le fait Avalonia :
            // la table du modèle sert telle quelle des deux côtés.
            pen.DashPattern = pattern;
        }

        return pen;
    }

    private static void DrawText(XGraphics gfx, TextFrame text, FieldContext context)
    {
        // Le contenu passe par le résolveur de champs, celui-là même que l'écran
        // appelle : un folio vu à l'écran est le folio imprimé.
        var content = FieldResolver.Resolve(text.Field, text.Text, context);
        if (string.IsNullOrEmpty(content)) return;

        var box = new XRect(
            text.X + text.PaddingLeft,
            text.Y + text.PaddingTop,
            Math.Max(1, text.Width - text.PaddingLeft - text.PaddingRight),
            Math.Max(1, text.Height - text.PaddingTop - text.PaddingBottom));

        // La composition est celle du noyau ; seule la règle à mesurer est d'ici.
        // C'est ce qui permet à l'écran d'obtenir exactement les mêmes lignes.
        //
        // Un bloc portant un champ résolu n'a pas les mêmes caractères que son texte
        // saisi : les fragments de style, qui repèrent des positions dans le texte,
        // ne s'y appliquent pas. Un folio ou une date sont d'un seul style, et c'est
        // bien ainsi.
        var fragments = content == text.Text ? text.Spans : (IReadOnlyList<StyleSpan>)[];

        var metriques = new MetriquesPdf(gfx);
        var commun = Commun(text);
        var lignes = Compositeur.Composer(content, fragments, commun, box.Width, metriques);
        if (lignes.Count == 0) return;

        // L'interligne supplémentaire se répartit de part et d'autre de la ligne et
        // non entièrement en dessous : convention typographique, celle de CSS et celle
        // d'Avalonia. Sans ce demi-interligne, l'écran et le PDF décalaient de 2 pt à
        // interligne 1,25 — assez pour que les deux rendus ne se superposent plus.
        var haut = box.Top;

        foreach (var ligne in lignes)
        {
            var avance = Compositeur.Avance(ligne.Naturelle, text.LineHeight);
            var ligneDeBase = haut + (avance - ligne.Naturelle) / 2 + Montee(ligne, metriques);

            if (ligne.Morceaux.Count > 0)
            {
                var justifiee = text.Align == TextAlign.Justify && !ligne.FinDeParagraphe;
                var origine = justifiee ? box.Left : text.Align switch
                {
                    TextAlign.Center => box.Left + (box.Width - ligne.Largeur) / 2,
                    TextAlign.Right => box.Right - ligne.Largeur,
                    _ => box.Left,
                };

                // Justifier : répartir le blanc qui manque entre les morceaux. La
                // dernière ligne d'un paragraphe ne se justifie pas, sinon « fin. »
                // s'étirerait sur toute la largeur du bloc.
                // Le blanc se répartit entre les mots, pas entre les morceaux : le
                // calcul est celui du noyau, partagé avec l'écran.
                var decalages = justifiee
                    ? Compositeur.Decalages(ligne, box.Width)
                    : new double[ligne.Morceaux.Count];

                for (var m = 0; m < ligne.Morceaux.Count; m++)
                {
                    var morceau = ligne.Morceaux[m];
                    if (morceau.Texte.Trim().Length == 0) continue;

                    var police = Police(morceau.Style);
                    var pinceau = ParseBrush(morceau.Style.Foreground) ?? XBrushes.Black;
                    // Un exposant ou un indice décale la ligne de base, vers le haut
                    // pour un exposant : l'axe du PDF descend, d'où le moins.
                    var y = ligneDeBase - morceau.Style.BaselineShift * morceau.Style.FontSize;
                    gfx.DrawString(morceau.Texte, police, pinceau,
                        new XPoint(origine + morceau.X + decalages[m], y));
                }
            }

            haut += avance;
        }
    }

    /// <summary>Le style du bloc, celui dont les fragments s'écartent.</summary>
    private static StyleEffectif Commun(TextFrame text) =>
        new(text.FontFamily, text.FontSize, text.Bold, text.Italic, text.Foreground, 0);

    private static XFont Police(StyleEffectif style)
    {
        var variante = XFontStyleEx.Regular;
        if (style.Bold) variante |= XFontStyleEx.Bold;
        if (style.Italic) variante |= XFontStyleEx.Italic;
        return new XFont(style.FontFamily, style.FontSize, variante);
    }

    /// <summary>
    /// Montée de la ligne : celle du plus grand style qu'elle porte. C'est ce qui cale
    /// les morceaux sur une même ligne de base, quelle que soit leur taille.
    /// </summary>
    private static double Montee(LigneComposee ligne, MetriquesPdf metriques)
    {
        var montee = 0.0;
        foreach (var morceau in ligne.Morceaux)
        {
            var police = Police(morceau.Style);
            montee = Math.Max(montee, police.Metrics.Ascent * police.Size / police.Metrics.UnitsPerEm);
        }

        return montee;
    }

    /// <summary>La règle à mesurer du PDF, prêtée au compositeur du noyau.</summary>
    private sealed class MetriquesPdf(XGraphics gfx) : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) =>
            gfx.MeasureString(texte, Police(style)).Width;

        public double Naturelle(StyleEffectif style) => Police(style).GetHeight();
    }

    private static void DrawImage(XGraphics gfx, ImageFrame frame, List<string> warnings, FieldContext context)
    {
        // Un bloc à champ tire son chemin du jeu de coordonnées ; les autres du leur.
        var source = FieldResolver.Resolve(frame.Field, frame.Path, context);

        if (string.IsNullOrWhiteSpace(source))
        {
            // Un bloc image sans chemin est un emplacement qui attend son image — c'est
            // ainsi que les modèles réservent leur place — et non une image perdue. Il
            // ne se signale que s'il attendait un logo qu'on n'a pas renseigné.
            if (frame.HasField) warnings.Add($"{frame.DisplayName} : aucun logo dans le jeu de coordonnées.");
            return;
        }

        if (!File.Exists(source))
        {
            warnings.Add($"Image introuvable : {source}");
            return;
        }

        using var image = XImage.FromFile(source);
        var target = FitRect(frame, image.PixelWidth, image.PixelHeight);

        // Un cadrage en « remplir » déborde du bloc : on découpe au bloc.
        if (frame.Fit == ImageFit.UniformToFill)
        {
            var clip = new XGraphicsPath();
            clip.AddRectangle(new XRect(frame.X, frame.Y, frame.Width, frame.Height));
            var state = gfx.Save();
            try
            {
                gfx.IntersectClip(clip);
                gfx.DrawImage(image, target);
            }
            finally
            {
                // Le découpage ne doit pas survivre à un échec : le contour tracé
                // ensuite serait rogné au bloc, à la moitié de son épaisseur.
                gfx.Restore(state);
            }

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

    /// <summary>Une couleur du modèle ; null pour une couleur absente, illisible ou entièrement transparente.</summary>
    private static XColor? ParseColor(string? value)
    {
        if (!HexColor.TryParse(value, out var a, out var r, out var g, out var b) || a == 0) return null;
        return XColor.FromArgb(a, r, g, b);
    }
}
