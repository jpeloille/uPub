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

    /// <summary>
    /// Les lignes que la page affiche pour ce bloc : la composition que
    /// <c>TexteCompose</c> fait, aux mêmes dimensions, et dont le sommet tombe dans le
    /// bloc — l'écran découpe au bloc depuis A2. Les lignes vides ne tracent rien.
    /// Chacune est rendue par l'ordonnée, en pixels, où son filet doit commencer : au
    /// milieu de sa ligne, à l'épaisseur de l'œil de la lettre.
    /// </summary>
    private static List<double> LignesDeLaPage(TextFrame bloc)
    {
        var largeur = Math.Max(1, bloc.Width - bloc.PaddingLeft - bloc.PaddingRight);
        var hauteur = Math.Max(1, bloc.Height - bloc.PaddingTop - bloc.PaddingBottom);
        var commun = new StyleEffectif(
            bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic, bloc.Foreground, 0);

        var lignes = Compositeur.Composer(
            bloc.Text, bloc.Spans, commun, bloc.Paragraphs, bloc.Paragraph,
            largeur, hauteur, bloc.Colonnage, bloc.LineHeight, new MetriquesAvalonia());
        var descente = Compositeur.Decalage(lignes, hauteur, bloc.VerticalAlign);
        var oeil = bloc.FontSize * 0.55;

        return lignes
            .Where(l => l.Morceaux.Count > 0 && bloc.PaddingTop + descente + l.Haut < bloc.Height)
            .Select(l => (bloc.Y + bloc.PaddingTop + descente + l.Haut + (l.Avance - oeil) / 2) * PixelsParPoint)
            .ToList();
    }

    /// <summary>La vignette d'un document, rendue à deux pixels par point.</summary>
    private static (byte[] Pixels, int Largeur, int Hauteur) Rendre(Document document)
    {
        var vignette = new TemplatePreview
        {
            Document = document,
            Width = document.PageWidth * PixelsParPoint,
            Height = document.PageHeight * PixelsParPoint,
            // Calée dans le coin : centrée, elle serait rendue avec son décalage dans la
            // fenêtre, et toutes les rangées glisseraient d'autant.
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
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
        Assert.True(taille.Width > 1000, $"la vignette n'a pas sa taille : {vignette.Bounds}");

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
        return (pixels, taille.Width, taille.Height);
    }

    /// <summary>
    /// Les filets d'un bloc dans la vignette : les suites de rangées de pixels où la
    /// couleur du texte du bloc paraît, comptées dans le rectangle du bloc seulement, et
    /// rendues par leur première rangée. Une rangée compte si trois pixels au moins y
    /// ont cette couleur — un filet en a des centaines, un bord lissé d'un autre bloc
    /// aucun.
    /// </summary>
    private static List<int> Filets((byte[] Pixels, int Largeur, int Hauteur) image, TextFrame bloc)
    {
        var couleur = Color.Parse(bloc.Foreground);
        var x0 = (int)Math.Ceiling(bloc.X * PixelsParPoint);
        var x1 = (int)Math.Floor((bloc.X + bloc.Width) * PixelsParPoint);
        var y0 = (int)Math.Ceiling(bloc.Y * PixelsParPoint);
        var y1 = (int)Math.Floor((bloc.Y + bloc.Height) * PixelsParPoint);

        var filets = new List<int>();
        var dansUnFilet = false;
        for (var y = y0; y < y1 && y < image.Hauteur; y++)
        {
            var touches = 0;
            for (var x = x0; x < x1 && x < image.Largeur; x++)
            {
                var i = (y * image.Largeur + x) * 4;
                if (Math.Abs(image.Pixels[i] - couleur.B) <= 8
                    && Math.Abs(image.Pixels[i + 1] - couleur.G) <= 8
                    && Math.Abs(image.Pixels[i + 2] - couleur.R) <= 8)
                {
                    touches++;
                }
            }

            var plein = touches >= 3;
            if (plein && !dansUnFilet) filets.Add(y);
            dansUnFilet = plein;
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
            var attendues = LignesDeLaPage(bloc);
            var vues = Filets(image, bloc);
            lignes += attendues.Count;
            if (vues.Count != attendues.Count)
            {
                ecarts.Add($"{bloc.DisplayName} : {vues.Count} filet(s), {attendues.Count} ligne(s) à la page");
                continue;
            }

            for (var i = 0; i < vues.Count; i++)
            {
                if (Math.Abs(vues[i] - attendues[i]) <= 2) continue;
                ecarts.Add($"{bloc.DisplayName}, ligne {i + 1} : filet à la rangée {vues[i]}, ligne attendue à {attendues[i]:0.0}");
                break;
            }
        }

        Assert.True(ecarts.Count == 0, string.Join("\n", ecarts));
        // Le pavé des trois colonnes compte à lui seul plusieurs dizaines de lignes : un
        // total de quelques unités voudrait dire qu'on a compté autre chose.
        Assert.True(lignes > 30, $"{lignes} ligne(s) seulement pour tout le bulletin");
    }
}
