using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <param name="ecran">
    /// La règle à mesurer de l'écran. La coupure de chaque ligne se décide sur la plus
    /// large de sa mesure et de celle du PDF, ce qui fait couper l'export aux mêmes mots
    /// que l'écran (poste Q13). Le noyau ne connaît pas Avalonia : c'est l'application
    /// qui la passe. <c>null</c> veut dire « pas d'écran » et coupe à la seule mesure du
    /// PDF ; seuls les tests du noyau le font. Le paramètre n'a pas de valeur par
    /// défaut, pour qu'aucun appelant ne coupe autrement que l'écran sans l'avoir écrit.
    /// </param>
    /// <param name="date">
    /// Date que portent les champs, ou <c>null</c> pour lire l'horloge. Elle se passe
    /// pour qu'un export soit reproductible : sans elle, tout document portant un champ
    /// de date produit un PDF différent chaque jour, et aucune comparaison à une
    /// référence figée n'est possible.
    /// </param>
    public static ExportReport Export(
        Document document, string path, Compositeur.IMetriques? ecran,
        BusinessInfo? contact = null, DateTime? date = null)
    {
        FontconfigFontResolver.EnsureInstalled();

        var warnings = new List<string>();

        // Une seule lecture de l'horloge : toutes les pages d'un même export portent
        // la même date, même si le document est long à écrire.
        var now = date ?? DateTime.Now;

        // Avant les pages, et non pendant : la barre d'état ne montre que le premier
        // avertissement, et une flèche de procédure d'urgence remplacée par une lettre
        // passe avant une image manquante.
        AvertirSurLesPolices(document, warnings, now, contact ?? new BusinessInfo());

        // Les références au jeu de couleurs et de polices se résolvent ici, une fois pour
        // tout l'export, par la même charte que l'écran consulte.
        var charte = document.Charte;

        // Pas de « using » ici : PDFsharp invalide le document lors du Save et
        // lève une exception si on tente ensuite de le libérer.
        var pdf = new PdfDocument();
        pdf.Info.Title = document.Title;
        pdf.Info.Creator = "MyPublisher";

        for (var index = 0; index < document.Pages.Count; index++)
        {
            var page = document.Pages[index];
            var context = new FieldContext(index + 1, document.Pages.Count, now, contact ?? new BusinessInfo());
            var pdfPage = pdf.AddPage();
            pdfPage.Width = XUnit.FromPoint(document.PageWidth);
            pdfPage.Height = XUnit.FromPoint(document.PageHeight);

            using var gfx = XGraphics.FromPdfPage(pdfPage);

            // Le fond de page passe avant tout : c'est le seul remplissage qui n'est pas
            // celui d'un bloc, et il doit se retrouver sous le premier d'entre eux.
            DrawBackground(gfx, document, warnings, charte);

            // Une règle par page, parce qu'elle mesure sur la surface de la page.
            var metriques = new MetriquesPdf(gfx, ecran);
            DessinerTous(gfx, metriques, page.Frames, warnings, context, charte, $"Page {index + 1}");
        }

        var pageCount = pdf.PageCount;
        pdf.Save(path);
        return new ExportReport(pageCount, warnings);
    }

    /// <summary>
    /// Confronte les polices que le document demande à celles que la machine rendra,
    /// et signale les écarts.
    ///
    /// Pourquoi une passe sur le modèle plutôt qu'un relevé pris pendant le dessin :
    /// PDFsharp garde en cache la résolution d'une police pour toute la durée du
    /// processus et n'appelle donc le résolveur qu'une fois par famille. Un relevé pris
    /// au vol serait complet au premier export et vide au second — or l'application
    /// exporte plusieurs fois de suite, et c'est justement le deuxième export qu'on
    /// envoie à l'imprimeur. Cette passe-ci ne dépend que du document.
    ///
    /// Un avertissement par police et non par bloc : les fiches ATR portent 119
    /// fragments en Wingdings, qui feraient 119 lignes disant la même chose. C'est la
    /// police qui est substituée, pas le bloc — comme « Image introuvable » nomme le
    /// fichier et non le cadre qui l'affiche.
    ///
    /// Et seulement les polices que l'export dessine — poste Q8. La passe relisait
    /// l'héritage des fragments pour son compte : un fragment entièrement recouvert par
    /// un autre, ou un bloc dont tout le texte est recouvert, faisait avertir d'une
    /// police que rien ne dessine. Elle lit maintenant les styles composés, par la
    /// découpe même que la composition emploie, sur le contenu même que
    /// <see cref="DrawText"/> compose — d'où la date et les coordonnées, qui résolvent
    /// les champs page par page comme le dessin les résout.
    /// </summary>
    private static void AvertirSurLesPolices(
        Document document, List<string> warnings, DateTime date, BusinessInfo contact)
    {
        // La paire demandée/employée, et non le libellé : le romain et le gras d'une
        // même famille absente tombent souvent sur le même substitut, et ne valent
        // alors qu'un avertissement.
        var vues = new HashSet<(string, string)>();
        // Une police prise dans le jeu est contrôlée sous son vrai nom : « scheme:body »
        // n'est la police de personne, et passerait sinon pour une police introuvable.
        var charte = document.Charte;

        for (var index = 0; index < document.Pages.Count; index++)
        {
            var context = new FieldContext(index + 1, document.Pages.Count, date, contact);
            var blocs = document.Pages[index].Frames.SelectMany(Descendance).OfType<TextFrame>();

            foreach (var bloc in blocs)
            {
                var content = FieldResolver.Resolve(bloc.Field, bloc.Text, context);

                foreach (var style in StylesDessines(bloc, content, charte))
                {
                    if (string.IsNullOrWhiteSpace(style.FontFamily)) continue;

                    var resolution = FontconfigFontResolver.Resoudre(style.FontFamily, style.Bold, style.Italic);
                    if (resolution.Exacte) continue;
                    if (!vues.Add((resolution.Demandee, resolution.Employee))) continue;

                    warnings.Add(resolution.Employee.Length == 0
                        ? $"Police introuvable : « {resolution.Demandee} » — aucune police sur cette machine."
                        : $"Police substituée : « {resolution.Demandee} » demandée, "
                          + $"« {resolution.Employee} » employée ({resolution.Motif}).");
                }
            }
        }
    }

    /// <summary>
    /// Les styles qu'un bloc de texte dessine : ceux de la découpe que la composition
    /// fait elle-même (<see cref="StyleEffectif.Decouper"/>), sur le contenu résolu et
    /// les fragments qui s'y appliquent (<see cref="Fragments"/>), comme
    /// <see cref="DrawText"/>. Le fragment recouvert, celui qui tombe hors du texte, la
    /// police d'un bloc dont chaque caractère porte un fragment, les fragments d'un
    /// bloc qui affiche un champ : rien de tout cela n'est dessiné, et rien de tout cela
    /// n'avertit plus.
    ///
    /// Un morceau blanc — espaces, tabulations, sauts de ligne — ne compte pas : c'est la
    /// règle de <see cref="Tracer"/>, qui ne le dessine pas. Sa largeur se mesure
    /// pourtant dans sa police, substituée ou non ; c'est le seul effet d'une police
    /// que la passe tait.
    /// </summary>
    private static IEnumerable<StyleEffectif> StylesDessines(TextFrame text, string content, Charte charte)
    {
        foreach (var (morceau, style) in StyleEffectif.Decouper(
                     content, Fragments(text, content, charte), Commun(text, charte)))
        {
            if (!string.IsNullOrWhiteSpace(morceau)) yield return style;
        }
    }

    /// <summary>
    /// Le bloc et tout ce qu'il contient. Les deux types qui portent des enfants sont
    /// traités ici : le groupe, et le tableau, dont les cases sont des blocs de texte
    /// ordinaires. Oublier le tableau laisserait ses polices hors du contrôle — trouvé
    /// par la relecture de B5, avant même que C9 ne soit fusionné.
    ///
    /// Un bloc masqué n'y est pas, ni rien de ce qu'il porte (poste I8) : l'export ne le
    /// dessine pas, et la passe des polices ne nomme que ce qui est dessiné (Q8).
    /// </summary>
    private static IEnumerable<Frame> Descendance(Frame frame)
    {
        if (!frame.Visible) yield break;

        yield return frame;

        var enfants = frame switch
        {
            GroupFrame groupe => groupe.Children.AsEnumerable(),
            TableFrame tableau => tableau.CellFrames,
            _ => [],
        };

        foreach (var enfant in enfants)
        {
            foreach (var descendant in Descendance(enfant)) yield return descendant;
        }
    }

    /// <summary>
    /// Dessine une liste de blocs de même niveau, chacun avec la désignation sous
    /// laquelle un avertissement le nommera. La liste entière est nécessaire pour cela :
    /// c'est elle qui dit si un nom suffit à désigner.
    ///
    /// Un bloc masqué est passé (poste I8) : ni lui ni ce qu'il porte ne se dessine, et
    /// rien de lui n'avertit — ni débordement, ni image introuvable. Il garde son rang
    /// dans la liste : un homonyme visible reste désigné par le numéro qu'il a dans le
    /// document, que l'œil d'un voisin soit ouvert ou non.
    /// </summary>
    private static void DessinerTous(
        XGraphics gfx, MetriquesPdf metriques, IReadOnlyList<Frame> blocs, List<string> warnings,
        FieldContext context, Charte charte, string chemin)
    {
        for (var rang = 0; rang < blocs.Count; rang++)
        {
            if (!blocs[rang].Visible) continue;

            DrawFrame(gfx, metriques, blocs[rang], warnings, context, charte, Reperer(chemin, blocs, rang));
        }
    }

    /// <summary>
    /// Comment un avertissement désigne le bloc qu'il vise.
    ///
    /// Le format « &lt;nom du bloc&gt; : &lt;message&gt; » ne suffit plus depuis que le
    /// débordement se signale, et il ne suffisait déjà pas : <c>DisplayName</c> retombe
    /// sur le TYPE quand le nom est vide, si bien que deux blocs anonymes donnaient deux
    /// avertissements indiscernables. On préfixe donc par la page, puis par les blocs
    /// qui portent celui-ci : « Page 4 · Bande · L1 · C2 ».
    ///
    /// Cela ne suffit pas non plus. L'import de R5 nomme « Bande » chacune des seize
    /// bandes des fiches ATR et « L1 · C2 » la deuxième case de chacune : six d'entre
    /// elles débordent sur la seule page 4, et six lignes identiques ne désignent rien.
    /// D'où le rang, qui ne s'écrit **que** lorsque le nom se répète dans la même
    /// liste — un numéro posé sur un nom déjà unique ferait croire à une numérotation
    /// que le document ne porte pas.
    /// </summary>
    private static string Reperer(string chemin, IReadOnlyList<Frame> fratrie, int rang)
    {
        var nom = fratrie[rang].DisplayName;

        var homonymes = 0;
        var precedents = 0;
        for (var i = 0; i < fratrie.Count; i++)
        {
            if (fratrie[i].DisplayName != nom) continue;
            homonymes++;
            if (i < rang) precedents++;
        }

        return homonymes > 1 ? $"{chemin} · {nom} n° {precedents + 1}" : $"{chemin} · {nom}";
    }

    /// <summary>
    /// Dessine un bloc dans le repère courant. Récursif : un groupe translate le repère
    /// et redescend, ses enfants étant en coordonnées relatives. La rotation s'applique
    /// avant la translation, si bien que celle d'un groupe se compose avec celle de
    /// chacun de ses blocs sans qu'aucun calcul n'ait à les combiner.
    /// </summary>
    private static void DrawFrame(
        XGraphics gfx, MetriquesPdf metriques, Frame frame, List<string> warnings, FieldContext context,
        Charte charte, string repere)
    {
        var state = gfx.Save();
        if (frame.Rotation != 0)
        {
            gfx.TranslateTransform(frame.CenterX, frame.CenterY);
            gfx.RotateTransform(frame.Rotation);
            gfx.TranslateTransform(-frame.CenterX, -frame.CenterY);
        }

        // Fond dessous, contenu, contour dessus : un filet posé sur une image passe sur
        // son bord et non sous elle — c'est aussi l'ordre de l'écran. Une forme n'a pas
        // de contenu, et suit donc le même chemin sans cas particulier : depuis C5c, le
        // fond peut être une image, qu'un tracé « fond et contour d'un trait » ne
        // saurait pas poser.
        DrawFill(gfx, frame, warnings, charte);

        // Seul le contenu est sous surveillance : une image illisible se signale et
        // laisse son cadre en place, comme une image manquante laisse le sien.
        try
        {
            switch (frame)
            {
                case TextFrame text: DrawText(gfx, metriques, text, context, charte, warnings, repere); break;
                case ImageFrame image: DrawImage(gfx, image, warnings, context, repere); break;
                case GroupFrame group: DrawChildren(gfx, metriques, group, warnings, context, charte, repere); break;
                case TableFrame table: DrawTable(gfx, metriques, table, warnings, context, charte, repere); break;
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"{repere} : {ex.Message}");
        }

        DrawStroke(gfx, frame, charte);

        gfx.Restore(state);
    }

    private static void DrawChildren(
        XGraphics gfx, MetriquesPdf metriques, GroupFrame group, List<string> warnings, FieldContext context,
        Charte charte, string repere) =>
        DessinerPortes(gfx, metriques, group, group.Children, warnings, context, charte, repere);

    /// <summary>
    /// Les blocs d'un groupe ou les cases d'un tableau, dans le repère de leur porteur et
    /// découpés à lui.
    ///
    /// La translation et la découpe ne valent que pour eux : le contour du porteur, tracé
    /// ensuite, est en coordonnées de page comme son fond, et ne doit pas être rogné.
    ///
    /// La découpe est celle de l'écran, lue au même endroit (<see cref="Decoupe"/>) :
    /// jusqu'au poste Q5, un bloc plus large que son groupe s'arrêtait au bord du groupe
    /// à l'écran et courait jusqu'au bout dans le PDF.
    /// </summary>
    private static void DessinerPortes(
        XGraphics gfx, MetriquesPdf metriques, Frame porteur, IReadOnlyList<Frame> blocs, List<string> warnings,
        FieldContext context, Charte charte, string repere)
    {
        var state = gfx.Save();
        try
        {
            gfx.TranslateTransform(porteur.X, porteur.Y);
            var zone = Decoupe.DesBlocs(porteur.Width, porteur.Height, blocs);
            gfx.IntersectClip(new XRect(zone.Left, zone.Top, zone.Width, zone.Height));
            DessinerTous(gfx, metriques, blocs, warnings, context, charte, repere);
        }
        finally
        {
            gfx.Restore(state);
        }
    }

    /// <summary>
    /// Un tableau se dessine comme un groupe, et pour la même raison : ses cases sont
    /// des blocs de texte calculés à partir de ses colonnes, de ses lignes et de ses
    /// cellules, en coordonnées relatives. L'export n'a donc aucun code de tableau qui
    /// lui soit propre — il redescend dans celui du bloc de texte, exactement comme
    /// l'écran. Les deux rendus sont d'accord par construction, sans qu'il faille les
    /// comparer pour s'en assurer.
    /// </summary>
    private static void DrawTable(
        XGraphics gfx, MetriquesPdf metriques, TableFrame table, List<string> warnings, FieldContext context,
        Charte charte, string repere) =>
        DessinerPortes(gfx, metriques, table, table.CellFrames, warnings, context, charte, repere);

    /// <summary>
    /// La zone qu'un remplissage doit couvrir, et sa forme. Un bloc, une case ou la page
    /// entière s'y ramènent, ce qui permet au fond de page d'emprunter exactement le
    /// chemin de celui d'un bloc.
    /// </summary>
    private readonly record struct Pourtour(XRect Rect, bool Ellipse, double Arrondi);

    private static Pourtour Contour(Frame frame) => new(
        new XRect(frame.X, frame.Y, frame.Width, frame.Height),
        frame is ShapeFrame { Shape: ShapeKind.Ellipse },
        frame.CornerRadius);

    /// <summary>
    /// Le fond de page : le même remplissage que celui d'un bloc, posé sur la page
    /// entière avant que rien d'autre ne soit tracé.
    /// </summary>
    private static void DrawBackground(XGraphics gfx, Document document, List<string> warnings, Charte charte) =>
        RemplirOuSignaler(gfx, document.Background,
            new Pourtour(new XRect(0, 0, document.PageWidth, document.PageHeight), false, 0),
            warnings, "Fond de page", charte);

    /// <summary>
    /// Le fond couvre le bloc entier, marge intérieure comprise, et se dessine même sans
    /// contenu : un bloc vide muni d'un fond est un aplat voulu. Un fond absent, comme un
    /// alpha nul, ne pose rien du tout et laisse voir ce qui est dessous.
    /// </summary>
    private static void DrawFill(XGraphics gfx, Frame frame, List<string> warnings, Charte charte) =>
        RemplirOuSignaler(gfx, frame.Fill, Contour(frame), warnings, frame.DisplayName, charte);

    /// <summary>
    /// Pose un remplissage dans un pourtour. Trois des quatre sortes se ramènent à un
    /// pinceau ; l'image, elle, se dessine — PDFsharp n'a pas de pinceau d'image — et
    /// c'est le pourtour qui lui sert alors de découpe.
    /// </summary>
    /// <summary>
    /// Pose un remplissage, et ne laisse jamais son échec emporter l'export.
    ///
    /// Depuis C5c, un remplissage peut ouvrir un fichier — et un fichier illisible levait
    /// jusqu'ici hors de toute garde : l'export s'arrêtait, aucun PDF n'était écrit,
    /// alors que la MÊME image posée en bloc image ne valait qu'un avertissement, et que
    /// l'écran, lui, encaissait. Trouvé par la relecture de C5. Le remède est celui que
    /// le reste du fichier applique déjà : signaler, et laisser le bloc en place.
    /// </summary>
    private static void RemplirOuSignaler(
        XGraphics gfx, FillBrush? fill, Pourtour pourtour, List<string> warnings, string nom, Charte charte)
    {
        try
        {
            Remplir(gfx, fill, pourtour, warnings, nom, charte);
        }
        catch (Exception ex)
        {
            warnings.Add($"{nom} : fond non dessiné — {ex.Message}");
        }
    }

    private static void Remplir(
        XGraphics gfx, FillBrush? fill, Pourtour pourtour, List<string> warnings, string nom, Charte charte)
    {
        if (fill is ImageFill image)
        {
            RemplirParImage(gfx, image, pourtour, warnings, nom);
            return;
        }

        Tracer(gfx, pourtour, Pinceau(fill, pourtour.Rect, charte), null);
    }

    private static void DrawStroke(XGraphics gfx, Frame frame, Charte charte) =>
        Tracer(gfx, Contour(frame), null, MakePen(frame, charte));

    /// <summary>
    /// Trace le pourtour avec le fond et le contour qu'on lui passe : une ellipse pour la
    /// forme qui en est une, sinon un rectangle, arrondi s'il l'est.
    /// </summary>
    private static void Tracer(XGraphics gfx, Pourtour pourtour, XBrush? brush, XPen? pen)
    {
        if (brush is null && pen is null) return;

        var rect = pourtour.Rect;

        if (pourtour.Ellipse)
        {
            if (brush is not null && pen is not null) gfx.DrawEllipse(pen, brush, rect);
            else if (brush is not null) gfx.DrawEllipse(brush, rect);
            else gfx.DrawEllipse(pen!, rect);
            return;
        }

        if (pourtour.Arrondi > 0)
        {
            // PDFsharp attend le diamètre des coins, pas le rayon.
            var corner = new XSize(pourtour.Arrondi * 2, pourtour.Arrondi * 2);
            if (brush is not null && pen is not null) gfx.DrawRoundedRectangle(pen, brush, rect, corner);
            else if (brush is not null) gfx.DrawRoundedRectangle(brush, rect, corner);
            else gfx.DrawRoundedRectangle(pen!, rect, corner);
            return;
        }

        if (brush is not null && pen is not null) gfx.DrawRectangle(pen, brush, rect);
        else if (brush is not null) gfx.DrawRectangle(brush, rect);
        else gfx.DrawRectangle(pen!, rect);
    }

    /// <summary>Le pourtour en chemin, pour servir de découpe à un remplissage par image.</summary>
    private static XGraphicsPath Chemin(Pourtour pourtour)
    {
        var chemin = new XGraphicsPath();
        var rect = pourtour.Rect;

        if (pourtour.Ellipse) chemin.AddEllipse(rect);
        else if (pourtour.Arrondi > 0)
        {
            chemin.AddRoundedRectangle(
                rect.X, rect.Y, rect.Width, rect.Height, pourtour.Arrondi * 2, pourtour.Arrondi * 2);
        }
        else chemin.AddRectangle(rect);

        return chemin;
    }

    /// <summary>
    /// Un remplissage par image : découpe au pourtour, quart de tour éventuel, puis
    /// l'image posée dans le cadre que le modèle a calculé.
    ///
    /// La découpe précède la rotation, et ce n'est pas indifférent : c'est le bloc qui
    /// découpe, et lui ne tourne pas. L'inverse rognerait l'image sur un rectangle
    /// couché.
    ///
    /// Une image absente se signale, comme celle d'un bloc image : ranger les images
    /// hors du document rend ce cas atteignable sans que le document ait bougé.
    /// </summary>
    private static void RemplirParImage(
        XGraphics gfx, ImageFill fill, Pourtour pourtour, List<string> warnings, string nom)
    {
        if (string.IsNullOrWhiteSpace(fill.Path)) return;

        if (!File.Exists(fill.Path))
        {
            warnings.Add($"Image de remplissage introuvable : {fill.Path}");
            return;
        }

        using var image = XImage.FromFile(fill.Path);
        var rect = pourtour.Rect;
        var (x, y, largeur, hauteur) = fill.Cadre(
            rect.Width, rect.Height, image.PixelWidth, image.PixelHeight);

        var state = gfx.Save();
        try
        {
            gfx.IntersectClip(Chemin(pourtour));

            if (fill.Rotation != 0)
            {
                gfx.TranslateTransform(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
                gfx.RotateTransform(fill.Rotation);
                gfx.TranslateTransform(-(rect.X + rect.Width / 2), -(rect.Y + rect.Height / 2));
            }

            gfx.DrawImage(image, new XRect(rect.X + x, rect.Y + y, largeur, hauteur));
        }
        finally
        {
            // La découpe ne doit pas survivre : le contour tracé ensuite serait rogné
            // au bloc, à la moitié de son épaisseur.
            gfx.Restore(state);
        }
    }

    private static XPen? MakePen(Frame frame, Charte charte)
    {
        var pen = ParsePen(charte.Couleur(frame.Stroke), frame.StrokeThickness);
        if (pen is not null && Frame.DashPattern(frame.StrokeStyle) is { } pattern)
        {
            // PDFsharp multiplie le motif par l'épaisseur, comme le fait Avalonia :
            // la table du modèle sert telle quelle des deux côtés.
            pen.DashPattern = pattern;
        }

        return pen;
    }

    /// <summary>
    /// Dessine le texte d'un bloc, et dit ce qu'il en coupe.
    ///
    /// **Le texte est découpé au bloc**, comme l'écran le fait depuis toujours
    /// (<c>ClipToBounds</c> dans <c>PageCanvas.axaml</c>). Ce n'est pas une addition
    /// mais un retour : jusqu'à R1, <c>XTextFormatter</c> s'arrêtait au bord de la
    /// boîte, et la composition partagée — qui pose chaque ligne à l'ordonnée qu'on lui
    /// donne — a emporté cette limite sans que rien ne la remplace. Depuis, un bloc trop
    /// plein débordait sur ses voisins dans le PDF et pas à l'écran. Publisher ne dessine
    /// pas le texte hors de sa zone ; les deux rendus s'accordent désormais là-dessus.
    ///
    /// C'est le BLOC qu'on découpe et non la zone de texte : une première ligne en
    /// retrait négatif pend dans la marge intérieure gauche et doit y rester visible.
    /// C'est exactement ce que fait la paire d'ATA 21 — <c>margin-left 0,1930in</c> avec
    /// <c>text-indent −0,2183in</c> pose la ligne à 1,82 pt à gauche de la zone de
    /// texte, que les 2,88 pt de <c>fo:padding-left</c> laissent bien à l'intérieur du
    /// bloc.
    /// </summary>
    private static void DrawText(
        XGraphics gfx, MetriquesPdf metriques, TextFrame text, FieldContext context, Charte charte,
        List<string> warnings, string repere)
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

        // La composition est celle du noyau ; la règle à mesurer est d'ici, et la
        // coupure se décide sur la plus large d'elle et de celle de l'écran. C'est ce
        // qui permet à l'écran d'obtenir exactement les mêmes lignes (poste Q13).
        //
        // Un bloc portant un champ résolu n'a pas les mêmes caractères que son texte
        // saisi : les fragments de style, qui repèrent des positions dans le texte,
        // ne s'y appliquent pas. Un folio ou une date sont d'un seul style, et c'est
        // bien ainsi. Les écarts de paragraphe tombent de même — un champ n'a pas les
        // mêmes paragraphes — alors que les réglages du bloc, eux, valent toujours.
        //
        // Le style commun et les fragments arrivent résolus par la charte du document —
        // la même fonction que l'écran appelle : une police du jeu se MESURE dans la
        // police du jeu, et la coupure des lignes en dépend.
        var fragments = Fragments(text, content, charte);
        var ecarts = content == text.Text ? text.Paragraphs : (IReadOnlyList<ParagraphSpan>)[];

        var commun = Commun(text, charte);
        var lignes = Compositeur.Composer(
            content, fragments, commun, ecarts, text.Paragraph,
            box.Width, box.Height, text.Colonnage, text.LineHeight, metriques);
        if (lignes.Count == 0) return;

        // Le calage vertical et la mesure du débordement viennent du noyau, comme la
        // découpe en lignes : l'écran lit les mêmes deux fonctions, avec sa règle.
        var descente = Compositeur.Decalage(lignes, box.Height, text.VerticalAlign);
        var debordement = Compositeur.Debordement(
            lignes, box.Height, text.PaddingBottom, text.VerticalAlign);

        if (debordement > Compositeur.SeuilDebordement)
        {
            warnings.Add(
                $"{repere} : le texte déborde du bloc de " +
                $"{debordement.ToString("0.#", Nombres)} pt — ce qui dépasse n'est pas imprimé.");
        }

        // Le découpage est posé ici et retiré ici, quoi qu'il arrive : le contour du
        // bloc se trace APRÈS le contenu, et un découpage qui lui survivrait le
        // réduirait à la moitié de son épaisseur. C'est la parade que « DrawImage »
        // s'était déjà donnée pour le cadrage « remplir ».
        var decoupe = gfx.Save();
        try
        {
            gfx.IntersectClip(new XRect(text.X, text.Y, text.Width, text.Height));
            Tracer(gfx, lignes, box, descente, metriques);
        }
        finally
        {
            gfx.Restore(decoupe);
        }
    }

    /// <summary>Les nombres d'un avertissement : « 36,9 pt », comme la date est en français.</summary>
    private static readonly System.Globalization.CultureInfo Nombres =
        System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

    private static void Tracer(
        XGraphics gfx, IReadOnlyList<LigneComposee> lignes, XRect box, double descente,
        MetriquesPdf metriques)
    {
        foreach (var ligne in lignes)
        {
            // Rien ne se cumule ici : la ligne porte son ordonnée dans sa colonne, et
            // l'espace avant le paragraphe y est déjà entré. Cumuler de ce côté et de
            // celui de l'écran remettrait en jeu, à chaque ligne, l'accord des deux.
            //
            // L'interligne supplémentaire se répartit de part et d'autre de la ligne et
            // non entièrement en dessous : convention typographique, celle de CSS et celle
            // d'Avalonia. Sans ce demi-interligne, l'écran et le PDF décalaient de 2 pt à
            // interligne 1,25 — assez pour que les deux rendus ne se superposent plus.
            var ligneDeBase = box.Top + descente + ligne.Haut
                              + (ligne.Avance - ligne.Naturelle) / 2 + Montee(ligne, metriques);

            if (ligne.Morceaux.Count > 0)
            {
                // Le fer vient de la LIGNE et non du bloc : il appartient au paragraphe.
                var justifiee = ligne.Fer == TextAlign.Justify && !ligne.FinDeParagraphe;
                // Le retrait déplace le bord gauche de la ligne, gouttières comprises ;
                // « Disponible » est ce qui reste entre les deux retraits, dans sa
                // colonne. C'est cette largeur-là, et non celle du bloc, qui cale un
                // centrage ou un fer à droite.
                var gauche = box.Left + ligne.Retrait;
                var origine = justifiee ? gauche : ligne.Fer switch
                {
                    TextAlign.Center => gauche + (ligne.Disponible - ligne.Largeur) / 2,
                    TextAlign.Right => gauche + ligne.Disponible - ligne.Largeur,
                    _ => gauche,
                };

                // Justifier : répartir le blanc qui manque entre les morceaux. La
                // dernière ligne d'un paragraphe ne se justifie pas, sinon « fin. »
                // s'étirerait sur toute la largeur du bloc.
                // Le blanc se répartit entre les mots, pas entre les morceaux : le
                // calcul est celui du noyau, partagé avec l'écran.
                var decalages = justifiee
                    ? Compositeur.Decalages(ligne)
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

                // Le souligné, sur les étendues que le noyau calcule pour l'écran aussi :
                // continu sous les blancs élargis d'une ligne justifiée, absent sous les
                // blancs pour « mots seuls » (poste I7).
                foreach (var etendue in Compositeur.Soulignes(ligne, decalages, metriques))
                {
                    Souligner(gfx, etendue, origine,
                        ligneDeBase - etendue.Style.BaselineShift * etendue.Style.FontSize);
                }
            }
        }
    }

    /// <summary>
    /// Trace le souligné d'une étendue sous sa ligne de base, à la place et à l'épaisseur
    /// que la table <c>post</c> de sa police donne. PDFsharp rend la position avec le signe
    /// du fichier — négative sous la ligne de base — et l'écran, par Avalonia, la rend
    /// comptée vers le bas : chacun la ramène à la distance du haut du trait, positive vers
    /// le bas, avant d'appeler la même géométrie (<see cref="Souligne.Traits"/>).
    ///
    /// Le stylo est celui de l'écran : bouts plats, jointures rondes, motif en multiples de
    /// l'épaisseur. La vague se trace par les points mêmes que l'écran relie.
    /// </summary>
    private static void Souligner(XGraphics gfx, EtendueSoulignee etendue, double origine, double ligneDeBase)
    {
        var style = etendue.Style;
        var metriques = Police(style).Metrics;
        var em = (double)metriques.UnitsPerEm;
        var (haut, epaisseur) = em > 0
            ? Souligne.Mesure(
                -metriques.UnderlinePosition * style.FontSize / em,
                metriques.UnderlineThickness * style.FontSize / em,
                style.FontSize)
            : Souligne.Mesure(0, 0, style.FontSize);

        var couleur = ParseColor(style.Foreground) ?? XColors.Black;
        var debut = origine + etendue.Debut;
        var fin = origine + etendue.Fin;

        foreach (var trace in style.Underline.Traits(haut, epaisseur))
        {
            var stylo = new XPen(couleur, trace.Epaisseur)
            {
                LineCap = XLineCap.Flat,
                LineJoin = XLineJoin.Round,
            };
            if (style.Underline.Motif is { } motif) stylo.DashPattern = motif;

            var axe = ligneDeBase + trace.Axe;
            if (!style.Underline.Ondule)
            {
                gfx.DrawLine(stylo, debut, axe, fin, axe);
                continue;
            }

            var points = Souligne.Vague(debut, fin, axe, trace.Epaisseur);
            if (points.Count < 2) continue;
            gfx.DrawLines(stylo, points.Select(p => new XPoint(p.X, p.Y)).ToArray());
        }
    }

    /// <summary>
    /// Les fragments qui s'appliquent au contenu dessiné : ceux du bloc s'il montre son
    /// texte saisi, aucun s'il montre un champ résolu. Une seule règle pour le dessin et
    /// pour la passe des polices, qui doivent parler du même texte. Ils arrivent résolus
    /// par la charte (poste C5b) : une référence au jeu y est déjà la police et la couleur
    /// qu'elle désigne.
    /// </summary>
    private static IReadOnlyList<StyleSpan> Fragments(TextFrame text, string content, Charte charte) =>
        content == text.Text ? charte.Fragments(text.Spans) : [];

    /// <summary>Le style du bloc, celui dont les fragments s'écartent, résolu par la charte.</summary>
    private static StyleEffectif Commun(TextFrame text, Charte charte) => charte.StyleCommun(text);

    /// <summary>La police d'un style : celle que mesure <see cref="MetriquesPdf"/>, par le même code.</summary>
    private static XFont Police(StyleEffectif style) => MetriquesPdf.Police(style);

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

    private static void DrawImage(
        XGraphics gfx, ImageFrame frame, List<string> warnings, FieldContext context, string repere)
    {
        // Un bloc à champ tire son chemin du jeu de coordonnées ; les autres du leur.
        var source = FieldResolver.Resolve(frame.Field, frame.Path, context);

        if (string.IsNullOrWhiteSpace(source))
        {
            // Un bloc image sans chemin est un emplacement qui attend son image — c'est
            // ainsi que les modèles réservent leur place — et non une image perdue. Il
            // ne se signale que s'il attendait un logo qu'on n'a pas renseigné.
            if (frame.HasField) warnings.Add($"{repere} : aucun logo dans le jeu de coordonnées.");
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

    /// <summary>
    /// Le rectangle de destination selon le mode de cadrage. Le calcul lui-même est celui
    /// du noyau, partagé avec le remplissage par image de C5c : deux copies de cette
    /// formule ne divergeraient que sur une image dont le rapport ne serait pas celui de
    /// son cadre, ce qui est exactement le cas intéressant.
    /// </summary>
    private static XRect FitRect(ImageFrame frame, double pixelWidth, double pixelHeight)
    {
        var (width, height) = Cadrage.Poser(frame.Fit, frame.Width, frame.Height, pixelWidth, pixelHeight);
        return new XRect(
            frame.X + (frame.Width - width) / 2,
            frame.Y + (frame.Height - height) / 2,
            width, height);
    }

    /// <summary>
    /// Le pinceau d'un remplissage, pour les trois sortes qui en ont un. Un dégradé lit
    /// ses extrémités dans le modèle — l'écran lit les mêmes — et une teinte lit la
    /// couleur qu'il a calculée. L'export ne mélange ni ne projette rien lui-même.
    /// </summary>
    /// <remarks>
    /// Le PDF ne sait pas donner une transparence propre à chaque extrémité d'un dégradé :
    /// PDFsharp écrit un ombrage axial, dont l'alpha est celui de la page. Un dégradé
    /// semi-transparent sort donc opaque — écart assumé, consigné plutôt que tu.
    /// </remarks>
    private static XBrush? Pinceau(FillBrush? fill, XRect rect, Charte charte) => fill switch
    {
        GradientFill degrade => Degrade(degrade, rect, charte),
        // Une teinte est un aplat : c'est le modèle qui a fait le mélange, sur la couleur
        // du jeu quand sa base y fait référence.
        not null when fill.UniformeSelon(charte) is { } couleur => ParseBrush(couleur),
        _ => null,
    };

    private static XBrush? Degrade(GradientFill fill, XRect rect, Charte charte)
    {
        var depart = ParseColor(charte.Couleur(fill.From));
        var arrivee = ParseColor(charte.Couleur(fill.To));
        if (depart is null && arrivee is null) return null;

        var (x0, y0, x1, y1) = fill.Extremites(rect.Width, rect.Height);
        return new XLinearGradientBrush(
            new XPoint(rect.X + x0, rect.Y + y0),
            new XPoint(rect.X + x1, rect.Y + y1),
            depart ?? arrivee!.Value,
            arrivee ?? depart!.Value);
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
