using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// La vignette des modèles lit les lignes de la composition — le poste Q9.
///
/// Elle lisait <c>AbsoluteLineHeight</c>, survivant de l'interligne d'avant R4
/// (<c>FontSize × LineHeight</c>, sans la hauteur naturelle de la police), et comptait
/// ses filets à la louche. On la rend ici pour de vrai, à deux pixels par point, et l'on
/// compte ses filets bloc par bloc, au pixel.
/// </summary>
public class VignetteTests
{
    private const double PixelsParPoint = 2;

    /// <summary>Une ligne de la page, en pixels : là où son filet doit paraître.</summary>
    private readonly record struct Ligne(double Rangee, double Debut, double Fin, TextAlign Fer, bool Justifiee);

    /// <summary>Un filet de la vignette, en pixels : sa première rangée, son premier
    /// pixel et le pixel qui suit son dernier, lus sur sa rangée du milieu.</summary>
    private readonly record struct Filet(int Rangee, int Debut, int Fin);

    /// <summary>
    /// Les lignes que la page affiche pour ce bloc : la composition que
    /// <c>TexteCompose</c> fait, aux mêmes dimensions, et dont le sommet tombe dans le
    /// bloc — l'écran découpe au bloc depuis A2. Les lignes vides ne tracent rien.
    ///
    /// Chacune est rendue par l'ordonnée, en pixels, où son filet doit commencer — au
    /// milieu de sa ligne, à l'épaisseur de l'œil de la lettre —, et par les abscisses
    /// où la page la pose : l'origine que <c>TexteCompose</c> lui donne selon son fer, et
    /// sa cible — le bout de sa colonne si elle est justifiée, le bout de son texte
    /// sinon.
    /// </summary>
    private static List<Ligne> LignesDeLaPage(TextFrame bloc, Charte charte)
    {
        var largeur = Math.Max(1, bloc.Width - bloc.PaddingLeft - bloc.PaddingRight);
        var hauteur = Math.Max(1, bloc.Height - bloc.PaddingTop - bloc.PaddingBottom);
        // Le style et les fragments résolus par la charte du modèle, comme la page les
        // compose : les modèles prennent leurs polices dans leur jeu (poste C5b).
        var commun = charte.StyleCommun(bloc);

        var lignes = Compositeur.Composer(
            bloc.Text, charte.Fragments(bloc.Spans), commun, bloc.Paragraphs, bloc.Paragraph,
            largeur, hauteur, bloc.Colonnage, bloc.LineHeight, new MetriquesAvalonia());
        var descente = Compositeur.Decalage(lignes, hauteur, bloc.VerticalAlign);
        var oeil = bloc.FontSize * 0.55;

        return lignes
            .Where(l => l.Morceaux.Count > 0 && bloc.PaddingTop + descente + l.Haut < bloc.Height)
            .Select(l =>
            {
                var justifiee = l.Fer == TextAlign.Justify && !l.FinDeParagraphe;
                var gauche = bloc.X + bloc.PaddingLeft + l.Retrait;
                var origine = justifiee ? gauche : l.Fer switch
                {
                    TextAlign.Center => gauche + (l.Disponible - l.Largeur) / 2,
                    TextAlign.Right => gauche + l.Disponible - l.Largeur,
                    _ => gauche,
                };
                var fin = justifiee ? gauche + l.Disponible : origine + l.Largeur;
                return new Ligne(
                    (bloc.Y + bloc.PaddingTop + descente + l.Haut + (l.Avance - oeil) / 2) * PixelsParPoint,
                    origine * PixelsParPoint, fin * PixelsParPoint, l.Fer, justifiee);
            })
            .ToList();
    }

    /// <summary>La vignette d'un document, rendue à deux pixels par point.</summary>
    private static (byte[] Pixels, int Largeur, int Hauteur) Rendre(Document document) =>
        Rendre(document, new TemplatePreview());

    /// <summary>
    /// Pose le document dans cette vignette-ci et la rend : une même vignette peut ainsi
    /// montrer deux documents l'un après l'autre, comme le fait une liste qui recycle
    /// ses éléments.
    /// </summary>
    private static (byte[] Pixels, int Largeur, int Hauteur) Rendre(Document document, TemplatePreview vignette)
    {
        vignette.Document = document;
        vignette.Width = document.PageWidth * PixelsParPoint;
        vignette.Height = document.PageHeight * PixelsParPoint;
        // Calée dans le coin : centrée, elle serait rendue avec son décalage dans la
        // fenêtre, et toutes les rangées glisseraient d'autant.
        vignette.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        vignette.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        var fenetre = new Window
        {
            Content = vignette,
            Width = vignette.Width + 40,
            Height = vignette.Height + 40,
        };
        fenetre.Show();
        Rendu.Battre();

        Assert.Equal(new Point(0, 0), vignette.Bounds.Position);
        var taille = new PixelSize((int)Math.Ceiling(vignette.Bounds.Width), (int)Math.Ceiling(vignette.Bounds.Height));
        Assert.True(Math.Abs(taille.Width - document.PageWidth * PixelsParPoint) <= 1,
            $"la vignette n'a pas sa taille : {vignette.Bounds}");

        using var rendu = new RenderTargetBitmap(taille, new Vector(96, 96));
        rendu.Render(vignette);

        var pixels = new byte[taille.Width * taille.Height * 4];
        using var tampon = new WriteableBitmap(taille, rendu.Dpi, PixelFormat.Bgra8888);
        using (var verrou = tampon.Lock())
        {
            rendu.CopyPixels(new PixelRect(0, 0, taille.Width, taille.Height),
                verrou.Address, pixels.Length, verrou.RowBytes);
            System.Runtime.InteropServices.Marshal.Copy(verrou.Address, pixels, 0, pixels.Length);
        }

        if (Environment.GetEnvironmentVariable("MYPUBLISHER_RECETTE_VIGNETTE") is { Length: > 0 } sortie)
        {
            rendu.Save(sortie);
        }

        fenetre.Close();
        fenetre.Content = null;
        return (pixels, taille.Width, taille.Height);
    }

    /// <summary>Ce pixel a-t-il la couleur donnée, au lissage près ?</summary>
    private static bool Teinte((byte[] Pixels, int Largeur, int Hauteur) image, int x, int y, Color couleur)
    {
        var i = (y * image.Largeur + x) * 4;
        return Math.Abs(image.Pixels[i] - couleur.B) <= 8
               && Math.Abs(image.Pixels[i + 1] - couleur.G) <= 8
               && Math.Abs(image.Pixels[i + 2] - couleur.R) <= 8;
    }

    /// <summary>
    /// Les rangées de pixels, de <paramref name="y0"/> à <paramref name="y1"/> exclu et
    /// de <paramref name="x0"/> à <paramref name="x1"/> exclu, où la couleur paraît. Une
    /// rangée compte si trois pixels au moins y ont cette couleur — un filet en a des
    /// centaines, un bord lissé d'un autre bloc aucun.
    /// </summary>
    private static List<int> RangeesTeintees(
        (byte[] Pixels, int Largeur, int Hauteur) image, Color couleur, int x0, int x1, int y0, int y1)
    {
        var rangees = new List<int>();
        for (var y = Math.Max(0, y0); y < y1 && y < image.Hauteur; y++)
        {
            var touches = 0;
            for (var x = Math.Max(0, x0); x < x1 && x < image.Largeur; x++)
            {
                if (Teinte(image, x, y, couleur)) touches++;
            }

            if (touches >= 3) rangees.Add(y);
        }

        return rangees;
    }

    /// <summary>
    /// Les filets d'un bloc dans la vignette : les suites de rangées de pixels où la
    /// couleur du texte du bloc paraît, comptées dans le rectangle du bloc seulement.
    /// Chacun est rendu par sa première rangée, et par le premier et le dernier pixel de
    /// cette couleur sur sa rangée du milieu — loin du bord lissé du dessus et du
    /// dessous.
    /// </summary>
    private static List<Filet> Filets((byte[] Pixels, int Largeur, int Hauteur) image, TextFrame bloc, Charte charte)
    {
        var couleur = Color.Parse(charte.Couleur(bloc.Foreground)!);
        var x0 = (int)Math.Ceiling(bloc.X * PixelsParPoint);
        var x1 = (int)Math.Floor((bloc.X + bloc.Width) * PixelsParPoint);
        var y0 = (int)Math.Ceiling(bloc.Y * PixelsParPoint);
        var y1 = (int)Math.Floor((bloc.Y + bloc.Height) * PixelsParPoint);

        // Les rangées teintées, en suites contiguës : une suite, un filet.
        var suites = new List<(int Premiere, int Derniere)>();
        foreach (var y in RangeesTeintees(image, couleur, x0, x1, y0, y1))
        {
            if (suites.Count > 0 && suites[^1].Derniere == y - 1) suites[^1] = (suites[^1].Premiere, y);
            else suites.Add((y, y));
        }

        var filets = new List<Filet>();
        foreach (var (premiere, derniere) in suites)
        {
            var milieu = (premiere + derniere) / 2;
            int debut = -1, fin = -1;
            for (var x = x0; x < x1 && x < image.Largeur; x++)
            {
                if (!Teinte(image, x, milieu, couleur)) continue;
                if (debut < 0) debut = x;
                fin = x + 1;
            }

            filets.Add(new Filet(premiere, debut, fin));
        }

        return filets;
    }

    [Fact]
    [Trait("poste", "Q9")]
    public void La_propriete_AbsoluteLineHeight_n_existe_plus()
    {
        Assert.Null(typeof(TextFrame).GetProperty("AbsoluteLineHeight"));
    }

    [AvaloniaFact]
    [Trait("poste", "Q9")]
    public void La_vignette_du_bulletin_a_un_filet_par_ligne_de_la_page_a_sa_place()
    {
        var document = DocumentSerializer.Load(Path.Combine(TemplateStore.Folder, "bulletin.mypub"));
        var image = Rendre(document);

        // Les blocs de texte saisi, droits : un champ garde un filet unique dans la
        // vignette — son contenu n'est pas celui du catalogue —, et la vignette
        // n'en dit pas plus.
        var blocs = document.Pages[0].Frames.OfType<TextFrame>()
            .Where(b => b.Field == FieldKind.None && b.Text.Length > 0 && b.Rotation == 0)
            .ToList();
        // Titre, sous-titre, titre de une, chapeau, trois colonnes, légende : un parcours
        // qui en perdrait passerait vert sur moins qu'il ne prétend.
        Assert.Equal(8, blocs.Count);

        // Le nombre, puis la place : c'est l'espacement que l'ancienne vignette manquait
        // de 16 %, et un compte seul ne l'aurait pas vu tant que les filets tenaient
        // tous dans le bloc. Deux pixels de jeu, soit un point : le lissage peut
        // allumer la rangée du dessus.
        var ecarts = new List<string>();
        var lignes = 0;
        foreach (var bloc in blocs)
        {
            var attendues = LignesDeLaPage(bloc, document.Charte);
            var vues = Filets(image, bloc, document.Charte);
            lignes += attendues.Count;
            if (vues.Count != attendues.Count)
            {
                ecarts.Add($"{bloc.DisplayName} : {vues.Count} filet(s), {attendues.Count} ligne(s) à la page");
                continue;
            }

            for (var i = 0; i < vues.Count; i++)
            {
                if (Math.Abs(vues[i].Rangee - attendues[i].Rangee) <= 2) continue;
                ecarts.Add($"{bloc.DisplayName}, ligne {i + 1} : filet à la rangée {vues[i].Rangee}, ligne attendue à {attendues[i].Rangee:0.0}");
                break;
            }
        }

        Assert.True(ecarts.Count == 0, string.Join("\n", ecarts));
        // Le pavé des trois colonnes compte à lui seul plusieurs dizaines de lignes : un
        // total de quelques unités voudrait dire qu'on a compté autre chose.
        Assert.True(lignes > 30, $"{lignes} ligne(s) seulement pour tout le bulletin");
    }

    /// <summary>
    /// Le bulletin, où chaque fer paraît. Il justifie ses trois colonnes mais ne centre
    /// aucun texte saisi : on centre le titre de une et le chapeau, et l'on cale le
    /// sous-titre à droite. Ce n'est pas le catalogue tel quel, mais ce sont ses blocs,
    /// à leurs dimensions. Rendu avec ses blocs de texte saisi, droits.
    /// </summary>
    private static (Document Document, List<TextFrame> Blocs) BulletinATousLesFers()
    {
        var document = DocumentSerializer.Load(Path.Combine(TemplateStore.Folder, "bulletin.mypub"));
        var blocs = document.Pages[0].Frames.OfType<TextFrame>()
            .Where(b => b.Field == FieldKind.None && b.Text.Length > 0 && b.Rotation == 0)
            .ToList();
        foreach (var bloc in blocs)
        {
            if (bloc.DisplayName is "Titre de une" or "Chapeau") bloc.Align = TextAlign.Center;
            if (bloc.DisplayName == "Sous-titre") bloc.Align = TextAlign.Right;
        }

        return (document, blocs);
    }

    /// <summary>
    /// Compare chaque filet de ces blocs aux abscisses de sa ligne à la page, et rend
    /// les écarts, avec le nombre de lignes éprouvées par fer. Deux pixels de jeu, soit
    /// un point, à chaque bout : le lissage éteint ou allume le pixel d'une extrémité
    /// qui tombe entre deux.
    /// </summary>
    private static (List<string> Ecarts, int Justifiees, int Centrees, int ADroite) EcartsDAbscisse(
        (byte[] Pixels, int Largeur, int Hauteur) image, IEnumerable<TextFrame> blocs, Charte charte)
    {
        var ecarts = new List<string>();
        int centrees = 0, justifiees = 0, aDroite = 0;
        foreach (var bloc in blocs)
        {
            var attendues = LignesDeLaPage(bloc, charte);
            var vues = Filets(image, bloc, charte);
            if (vues.Count != attendues.Count)
            {
                ecarts.Add($"{bloc.DisplayName} : {vues.Count} filet(s), {attendues.Count} ligne(s) à la page");
                continue;
            }

            for (var i = 0; i < vues.Count; i++)
            {
                var (vue, attendue) = (vues[i], attendues[i]);
                if (attendue.Justifiee) justifiees++;
                else if (attendue.Fer == TextAlign.Center) centrees++;
                else if (attendue.Fer == TextAlign.Right) aDroite++;

                if (Math.Abs(vue.Debut - attendue.Debut) <= 2 && Math.Abs(vue.Fin - attendue.Fin) <= 2) continue;
                ecarts.Add($"{bloc.DisplayName}, ligne {i + 1} ({attendue.Fer}{(attendue.Justifiee ? ", justifiée" : "")}) : "
                           + $"filet de {vue.Debut} à {vue.Fin}, ligne de {attendue.Debut:0.0} à {attendue.Fin:0.0}");
            }
        }

        return (ecarts, justifiees, centrees, aDroite);
    }

    [AvaloniaFact]
    [Trait("poste", "Q9")]
    public void Les_filets_commencent_et_finissent_ou_la_page_pose_ses_lignes()
    {
        var (document, blocs) = BulletinATousLesFers();
        var (ecarts, justifiees, centrees, aDroite) = EcartsDAbscisse(Rendre(document), blocs, document.Charte);

        Assert.True(ecarts.Count == 0, string.Join("\n", ecarts));
        // Un fer qu'aucune ligne n'éprouverait passerait vert sans rien prouver.
        Assert.True(justifiees > 20, $"{justifiees} ligne(s) justifiée(s) seulement");
        Assert.True(centrees >= 2, $"{centrees} ligne(s) centrée(s) seulement");
        Assert.True(aDroite >= 1, $"{aDroite} ligne(s) au fer à droite seulement");
    }

    [AvaloniaFact]
    [Trait("poste", "Q9")]
    public void La_carte_de_visite_porte_un_filet_par_champ()
    {
        // La carte n'est faite que de champs : sans leur filet, sa vignette serait vide
        // de tout texte.
        var document = DocumentSerializer.Load(Path.Combine(TemplateStore.Folder, "carte-de-visite.mypub"));
        var champs = document.Pages[0].Frames.OfType<TextFrame>()
            .Where(b => b.Field != FieldKind.None)
            .ToList();
        // Société, devise, nom, fonction, adresse, téléphone.
        Assert.Equal(6, champs.Count);

        var image = Rendre(document);

        // Un filet, et un seul, par champ : de toute la largeur de sa zone de texte, au
        // milieu de sa première ligne, à l'avance de sa police. Deux pixels de jeu, comme
        // pour le texte saisi.
        var metriques = new MetriquesAvalonia();
        var ecarts = new List<string>();
        foreach (var champ in champs)
        {
            var vus = Filets(image, champ, document.Charte);
            if (vus.Count != 1)
            {
                ecarts.Add($"{champ.DisplayName} : {vus.Count} filet(s), un attendu");
                continue;
            }

            var avance = Compositeur.Avance(
                metriques.Naturelle(document.Charte.StyleCommun(champ)),
                champ.LineHeight);
            var rangee = (champ.Y + champ.PaddingTop + (avance - champ.FontSize * 0.55) / 2) * PixelsParPoint;
            var debut = (champ.X + champ.PaddingLeft) * PixelsParPoint;
            var fin = (champ.X + champ.Width - champ.PaddingRight) * PixelsParPoint;
            var vu = vus[0];
            if (Math.Abs(vu.Rangee - rangee) <= 2 && Math.Abs(vu.Debut - debut) <= 2 && Math.Abs(vu.Fin - fin) <= 2) continue;
            ecarts.Add($"{champ.DisplayName} : filet à la rangée {vu.Rangee}, de {vu.Debut} à {vu.Fin} ; "
                       + $"attendu à {rangee:0.0}, de {debut:0.0} à {fin:0.0}");
        }

        Assert.True(ecarts.Count == 0, string.Join("\n", ecarts));
    }

    [AvaloniaFact]
    [Trait("poste", "Q9")]
    public void Un_bloc_qui_deborde_ne_laisse_paraitre_aucun_filet_sous_son_cadre()
    {
        // La première colonne du bulletin, ramenée à cent points : son texte en compose
        // bien davantage, et rien d'autre n'est posé dessous jusqu'au folio.
        var document = DocumentSerializer.Load(Path.Combine(TemplateStore.Folder, "bulletin.mypub"));
        var colonne = document.Pages[0].Frames.OfType<TextFrame>().Single(b => b.DisplayName == "Colonne 1");
        var basOrigine = colonne.Y + colonne.Height;
        colonne.Height = 100;

        // Le débordement est réel : la composition pose des lignes sous le cadre.
        var hauteur = colonne.Height - colonne.PaddingTop - colonne.PaddingBottom;
        var lignes = Compositeur.Composer(
            colonne.Text, document.Charte.Fragments(colonne.Spans),
            document.Charte.StyleCommun(colonne),
            colonne.Paragraphs, colonne.Paragraph,
            colonne.Width - colonne.PaddingLeft - colonne.PaddingRight, hauteur,
            colonne.Colonnage, colonne.LineHeight, new MetriquesAvalonia());
        Assert.True(Compositeur.Hauteur(lignes) > hauteur + 20,
            $"la colonne ne déborde pas : {Compositeur.Hauteur(lignes):0.0} pt pour {hauteur:0.0}");

        var image = Rendre(document);

        // Sous le cadre, de la première rangée entièrement dessous jusqu'au bas d'origine
        // de la colonne : aucune rangée de la couleur du texte.
        var dessous = RangeesTeintees(image, Color.Parse(document.Charte.Couleur(colonne.Foreground)!),
            (int)Math.Floor(colonne.X * PixelsParPoint), (int)Math.Ceiling((colonne.X + colonne.Width) * PixelsParPoint),
            (int)Math.Ceiling((colonne.Y + colonne.Height) * PixelsParPoint), (int)Math.Floor(basOrigine * PixelsParPoint));
        Assert.True(dessous.Count == 0,
            $"{dessous.Count} rangée(s) de filet sous le cadre, la première à {dessous.FirstOrDefault()}");

        // Et dans le cadre, le texte paraît bien : la découpe n'a pas tout effacé.
        Assert.NotEmpty(Filets(image, colonne, document.Charte));
    }

    [AvaloniaFact]
    [Trait("poste", "Q9")]
    public void Une_vignette_qui_change_de_document_recompose_ses_filets()
    {
        // La vignette garde ses filets d'un rendu à l'autre. Deux documents de même
        // format, l'un après l'autre dans la même vignette : la taille ne change pas, et
        // seul le changement de document peut faire recomposer. Des filets gardés du
        // premier laisseraient le titre et le chapeau du second au fer à gauche.
        var vignette = new TemplatePreview();
        Rendre(DocumentSerializer.Load(Path.Combine(TemplateStore.Folder, "bulletin.mypub")), vignette);

        var (document, blocs) = BulletinATousLesFers();
        var (ecarts, _, centrees, _) = EcartsDAbscisse(Rendre(document, vignette), blocs, document.Charte);

        Assert.True(ecarts.Count == 0, string.Join("\n", ecarts));
        Assert.True(centrees >= 2, $"{centrees} ligne(s) centrée(s) seulement");
    }
}
