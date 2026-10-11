using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.Views;
using Xunit;
using Image = MyPublisher.Tests.Interface.Rendu.Image;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Le débordement du texte (A2) et son calage vertical (A7), relevés sur ce que la
/// fenêtre dessine.
///
/// Pourquoi ne pas s'en tenir à <c>CompositeurTests</c>, qui éprouve déjà les deux
/// calculs : parce qu'il éprouve les calculs, et non leur emploi. Un écran qui
/// oublierait d'ajouter le calage à l'ordonnée de la ligne de base, ou qui ne
/// dessinerait jamais le marqueur, passerait tous les tests du noyau sans en déranger
/// un seul.
/// </summary>
public class DebordementTests
{
    private const double PixelsParPoint = Rendu.PixelsParPoint;

    /// <summary>
    /// Un bloc de 60 pt de haut, sans marge intérieure : trois lignes y tiennent à
    /// interligne 1, six n'y tiennent pas. Les marges sont mises à zéro parce que c'est
    /// leur somme avec la hauteur qui décide du débordement — les laisser à leur défaut
    /// de 4 pt rendrait chaque nombre de ce fichier plus difficile à relire qu'à
    /// vérifier.
    /// </summary>
    private static TextFrame Bloc(string texte, double hauteur = 60) => new()
    {
        Name = "Bloc", X = 40, Y = 40, Width = 200, Height = hauteur,
        FontFamily = "DejaVu Sans", FontSize = 11, LineHeight = 1,
        PaddingLeft = 0, PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0,
        Foreground = "#FF000000",
        Text = texte,
    };

    private const string Court = "une\ndeux";
    private const string Long = "une\ndeux\ntrois\nquatre\ncinq\nsix\nsept\nhuit";

    /// <summary>
    /// La fenêtre, repères ALLUMÉS : le marqueur de débordement se range sous le même
    /// interrupteur qu'eux, et <see cref="Rendu.Ouvrir"/> les éteint pour mesurer de
    /// l'encre. Un test du marqueur qui hériterait de ce réglage ne verrait jamais rien
    /// et passerait pour une raison qui n'est pas la sienne.
    /// </summary>
    private static (Window Fenetre, Image Vue) Montrer(params Frame[] blocs)
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(blocs));
        vm.ShowGuides = true;
        Rendu.Battre();
        return (fenetre, Rendu.Encre(fenetre));
    }

    /// <summary>
    /// Un pixel franchement rouge : le marqueur, et rien d'autre de cette page. Le seuil
    /// écarte le lissage de ses bords.
    /// </summary>
    private static bool EstRouge(Image image, int x, int y)
    {
        if (x < 0 || y < 0 || x >= image.Largeur || y >= image.Hauteur) return false;

        var i = (y * image.Largeur + x) * 4;   // BGRA
        return image.Pixels[i + 2] > 150 && image.Pixels[i + 1] < 110 && image.Pixels[i] < 110;
    }

    /// <summary>
    /// Combien de pixels rouges dans le voisinage du coin inférieur droit du bloc. Le
    /// poste B2 s'en sert aussi : un texte importé trop long doit faire paraître le même
    /// marqueur.
    /// </summary>
    internal static int Marqueur(Image image, Frame bloc)
    {
        var compte = 0;
        for (var y = (int)((bloc.Bottom - 16) * PixelsParPoint); y < (bloc.Bottom + 8) * PixelsParPoint; y++)
        {
            for (var x = (int)((bloc.Right - 16) * PixelsParPoint); x < (bloc.Right + 8) * PixelsParPoint; x++)
            {
                if (EstRouge(image, x, y)) compte++;
            }
        }

        return compte;
    }

    [AvaloniaFact]
    public void Le_marqueur_ne_parait_que_sur_le_bloc_qui_deborde()
    {
        // Deux blocs de même taille, l'un trop plein, l'autre non. Le témoin n'est pas
        // une politesse : sans lui, un marqueur dessiné sur TOUS les blocs passerait.
        var trop = Bloc(Long);
        var juste = Bloc(Court);
        juste.X = 300;

        var (_, vue) = Montrer(trop, juste);

        Assert.True(Marqueur(vue, trop) > 20, "le bloc qui déborde ne porte pas de marqueur");
        Assert.Equal(0, Marqueur(vue, juste));
    }

    [AvaloniaFact]
    public void Le_marqueur_parait_des_que_le_texte_ne_tient_plus()
    {
        // C'est l'image RÉELLEMENT composée qu'on compare, deux fois, et non le modèle :
        // « RenderTargetBitmap.Render » redessine de force et passerait même si rien ne
        // se rafraîchissait. Le marqueur doit suivre la frappe, pas la réouverture du
        // document.
        var bloc = Bloc(Court);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.ShowGuides = true;
        Rendu.Battre();

        var avant = Composee(fenetre);
        bloc.Text = Long;
        Rendu.Battre();

        Assert.NotEqual(avant, Composee(fenetre));
        Assert.True(Marqueur(Rendu.Encre(fenetre), bloc) > 20);
    }

    /// <summary>L'image réellement composée de la fenêtre — celle que l'utilisateur voit.</summary>
    private static byte[] Composee(Window fenetre)
    {
        using var image = fenetre.CaptureRenderedFrame()
                          ?? throw new InvalidOperationException("aucune image composée");

        var pixels = new byte[image.PixelSize.Width * image.PixelSize.Height * 4];
        using var verrou = image.Lock();
        System.Runtime.InteropServices.Marshal.Copy(verrou.Address, pixels, 0, pixels.Length);
        return pixels;
    }

    [AvaloniaFact]
    public void Les_reperes_eteints_effacent_le_marqueur()
    {
        // Il ne s'imprime pas : c'est ce qui permet de comparer l'écran au PDF sur une
        // page qui déborde, et c'est la seule raison pour laquelle il obéit à cet
        // interrupteur-là plutôt qu'à aucun.
        var bloc = Bloc(Long);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        vm.ShowGuides = false;
        Rendu.Battre();
        Assert.Equal(0, Marqueur(Rendu.Encre(fenetre), bloc));

        vm.ShowGuides = true;
        Rendu.Battre();
        Assert.True(Marqueur(Rendu.Encre(fenetre), bloc) > 20);
    }

    [AvaloniaFact]
    public void Le_texte_qui_deborde_est_rogne_au_bloc_a_lecran()
    {
        // L'écran découpe depuis toujours ; on l'épingle ici parce que c'est la
        // référence sur laquelle l'export vient de s'aligner. Si l'écran cessait de
        // rogner, la recette du poste comparerait deux rendus d'accord et tous deux
        // faux.
        var bloc = Bloc(Long);
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var vue = Rendu.Encre(fenetre);

        Assert.True(Encre(vue, bloc.Y + 4, bloc.Bottom - 2), "le bloc ne dessine rien");
        Assert.False(Encre(vue, bloc.Bottom + 1, bloc.Bottom + 40),
            "du texte est dessiné sous le bloc");
    }

    /// <summary>Y a-t-il de l'encre entre ces deux ordonnées, en points ?</summary>
    private static bool Encre(Image image, double hautPt, double basPt)
    {
        for (var y = (int)(hautPt * PixelsParPoint); y < basPt * PixelsParPoint; y++)
        {
            for (var x = 0; x < image.Largeur; x++)
            {
                if (Rendu.EstEncre(image, x, y)) return true;
            }
        }

        return false;
    }

    [AvaloniaFact]
    public void Le_calage_vertical_descend_le_texte_a_lecran()
    {
        // Deux lignes dans 120 pt de haut : le reste est assez grand pour que les trois
        // calages se distinguent sans ambiguïté, et pour qu'un demi-reste ne se confonde
        // pas avec un reste entier.
        //
        // On mesure par DIFFÉRENCE entre les trois calages, et non chacun contre une
        // ordonnée calculée : l'encre commence à la hauteur d'x du premier glyphe et non
        // au sommet de la ligne — « une » n'a aucun jambage montant, et ce décalage vaut
        // quatre points à ce corps-là. Il est le même pour les trois, et la différence
        // s'en débarrasse.
        var reste = 0.0;
        var sommets = new Dictionary<VerticalAlign, int>();

        foreach (var fer in Enum.GetValues<VerticalAlign>())
        {
            var bloc = Bloc(Court, hauteur: 120);
            bloc.VerticalAlign = fer;

            var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(bloc));
            sommets[fer] = SommetDeLEncre(Rendu.Encre(fenetre));

            var naturelle = new MetriquesAvalonia().Naturelle(
                new StyleEffectif(bloc.FontFamily, bloc.FontSize, false, false, bloc.Foreground, 0));
            reste = bloc.Height - 2 * Compositeur.Avance(naturelle, bloc.LineHeight);
        }

        // Un pixel, soit un demi-point : le sommet de l'encre se lit au pixel près.
        Assert.Equal(reste / 2 * PixelsParPoint,
            sommets[VerticalAlign.Middle] - sommets[VerticalAlign.Top], tolerance: 1);
        Assert.Equal(reste * PixelsParPoint,
            sommets[VerticalAlign.Bottom] - sommets[VerticalAlign.Top], tolerance: 1);
    }

    private static int SommetDeLEncre(Image image)
    {
        for (var y = 0; y < image.Hauteur; y++)
        {
            for (var x = 0; x < image.Largeur; x++)
            {
                if (Rendu.EstEncre(image, x, y)) return y;
            }
        }

        return -1;
    }

    [AvaloniaFact]
    public void Aucun_document_livre_ne_deborde()
    {
        // Le garde-fou du poste, et il a déjà servi : quatre blocs de
        // « exemple-bulletin.mypub » — le document de référence du contrôle P2 — et deux
        // de « recette-polices.mypub » débordaient sans que rien ne le dise, depuis que
        // R4 a fait de l'interligne un multiple de la hauteur naturelle et non du corps.
        // Personne ne l'avait vu parce que rien ne le disait ; c'est exactement ce que
        // ce poste corrige.
        //
        // Il mesure avec la règle d'AVALONIA. Celle de PDFsharp est éprouvée du côté de
        // la recette, qui exporte les mêmes fichiers et n'y attend aucun avertissement
        // de débordement : les deux règles ne rendent pas exactement la même hauteur, et
        // un document livré doit tenir sous les deux.
        var metriques = new MetriquesAvalonia();
        var fautifs = new List<string>();

        foreach (var chemin in Documents())
        {
            var document = DocumentSerializer.Load(chemin);
            foreach (var (bloc, nom) in Textes(document))
            {
                var largeur = Math.Max(1, bloc.Width - bloc.PaddingLeft - bloc.PaddingRight);
                var hauteur = Math.Max(1, bloc.Height - bloc.PaddingTop - bloc.PaddingBottom);
                var commun = new StyleEffectif(
                    bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic, bloc.Foreground, 0);

                var lignes = Compositeur.Composer(
                    bloc.Text, bloc.Spans, commun, bloc.Paragraphs, bloc.Paragraph,
                    largeur, hauteur, bloc.Colonnage, bloc.LineHeight, metriques);

                var trop = Compositeur.Debordement(
                    lignes, hauteur, bloc.PaddingBottom, bloc.VerticalAlign);

                if (trop > Compositeur.SeuilDebordement)
                {
                    fautifs.Add($"{Path.GetFileName(chemin)} · {nom} : {trop:0.0} pt");
                }
            }
        }

        Assert.Empty(fautifs);
    }

    /// <summary>Les blocs de texte du document, cases de tableau et groupes compris.</summary>
    private static IEnumerable<(TextFrame Bloc, string Nom)> Textes(Document document)
    {
        IEnumerable<(TextFrame, string)> Descendre(Frame frame, string chemin)
        {
            var repere = $"{chemin} · {frame.DisplayName}";
            if (frame is TextFrame texte && texte.Text.Length > 0) yield return (texte, repere);

            var enfants = frame switch
            {
                GroupFrame groupe => groupe.Children.AsEnumerable(),
                TableFrame tableau => tableau.CellFrames,
                _ => [],
            };

            foreach (var enfant in enfants)
            {
                foreach (var trouve in Descendre(enfant, repere)) yield return trouve;
            }
        }

        return document.Pages.SelectMany(
            (page, rang) => page.Frames.SelectMany(f => Descendre(f, $"Page {rang + 1}")));
    }

    /// <summary>
    /// Les documents que le dépôt livre : les exemples et les modèles. Le chemin remonte
    /// jusqu'à la solution plutôt que de compter les « .. » depuis le dossier de sortie,
    /// qui change avec la configuration de compilation.
    /// </summary>
    private static IEnumerable<string> Documents()
    {
        var racine = new DirectoryInfo(AppContext.BaseDirectory);
        while (racine is not null && !File.Exists(Path.Combine(racine.FullName, "MyPublisher.sln")))
        {
            racine = racine.Parent;
        }

        Assert.NotNull(racine);

        var documents = Directory.GetFiles(Path.Combine(racine.FullName, "Samples"), "*.mypub")
            .Concat(Directory.GetFiles(
                Path.Combine(racine.FullName, "MyPublisher", "Templates"), "*.mypub"))
            // Sauf le document de la recette de ce poste : il déborde exprès, c'est ce
            // qu'il montre. La recette l'écarte de la même façon et pour la même raison.
            .Where(c => Path.GetFileName(c) != "recette-debordement.mypub")
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        // Un chemin qui ne trouve rien ferait passer ce test en silence.
        Assert.True(documents.Count >= 10, $"{documents.Count} document(s) trouvé(s)");
        return documents;
    }
}
