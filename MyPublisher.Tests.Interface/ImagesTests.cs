using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MyPublisher.Converters;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Poste A3, vu de l'écran : un dossier déplacé affiche encore ses images.
///
/// Ce que ces tests ajoutent aux tests du noyau, qui éprouvent déjà le calcul des
/// chemins : que l'image arrive jusqu'aux pixels. Entre le chemin résolu et l'affichage
/// il reste le cache d'images de <c>Conv.cs</c>, statique, indexé par chemin, qui garde
/// ses réussites pour la vie du processus et, depuis le poste Q3, oublie ses échecs à
/// chaque ouverture de document — le quatrième test. La résolution ayant lieu à l'ouverture, ce cache ne voit
/// que des chemins absolus — mais c'est là une affirmation sur du code, et le second
/// test en fait une mesure : deux documents rangés dans deux dossiers, portant chacun
/// une image nommée « photo.png », doivent afficher chacun la sienne.
///
/// Les couleurs sont franches et introuvables ailleurs dans la fenêtre — magenta et
/// cyan purs — ce qui permet de compter les pixels d'une image sans avoir à situer le
/// bloc dans la fenêtre.
///
/// L'image relevée est celle que la fenêtre a RÉELLEMENT composée
/// (<c>CaptureRenderedFrame</c>) et non un rendu forcé : « RenderTargetBitmap.Render »
/// redessine à la demande et passerait là où l'écran ne s'est pas rafraîchi.
/// </summary>
public class ImagesTests : IDisposable
{
    private const uint Magenta = 0xFFFF00FF;
    private const uint Cyan = 0xFF00FFFF;

    private readonly string _travail =
        Directory.CreateTempSubdirectory("mypublisher-images-").FullName;

    private Window? _ouverte;

    public void Dispose() => Directory.Delete(_travail, recursive: true);

    // -------------------------------------------------------------------- les tests

    [AvaloniaFact]
    public void Un_dossier_deplace_affiche_encore_son_image()
    {
        // Le geste que le poste existe pour permettre : « j'ai déplacé mon dossier de
        // travail ». Le dossier est déplacé pour de bon — l'ancien chemin n'existe
        // plus — et non recopié : un chemin absolu survivrait à une copie, il ne
        // survit pas à celle-ci.
        var depart = Composer("notice", "photo.png", Magenta);
        Assert.True(Compter(Ouvrir(depart), Magenta) > 1000, "l'image ne s'affiche pas avant le déplacement");

        var arrivee = Path.Combine(_travail, "archives", "notice");
        Directory.CreateDirectory(Path.Combine(_travail, "archives"));
        Directory.Move(Path.Combine(_travail, "notice"), arrivee);

        var pixels = Compter(Ouvrir(Path.Combine(arrivee, "notice.mypub")), Magenta);

        Assert.True(pixels > 1000, $"le dossier déplacé n'affiche plus son image ({pixels} pixels)");
    }

    [AvaloniaFact]
    public void Deux_dossiers_portant_le_meme_nom_d_image_affichent_chacun_le_sien()
    {
        // Le cache d'images vit toute la durée du processus : c'est donc bien dans une
        // seule et même exécution que les deux documents s'ouvrent. S'il était indexé
        // par la forme relue — « photo.png » — le second document afficherait l'image
        // du premier, et rien ne le dirait.
        var premier = Composer("bulletin-mai", "photo.png", Magenta);
        var second = Composer("bulletin-juin", "photo.png", Cyan);

        var image = Compter(Ouvrir(premier), Magenta);
        var autre = Ouvrir(second);

        Assert.True(image > 1000, "le premier document n'affiche pas son image");
        Assert.True(Compter(autre, Cyan) > 1000, "le second document n'affiche pas la sienne");
        Assert.Equal(0, Compter(autre, Magenta));
    }

    [AvaloniaFact]
    public void Annuler_un_geste_ne_deplace_pas_une_image()
    {
        // Le piège du poste. « Capture » sérialise le document à chaque geste et
        // « Restore » le relit, tous deux sans dossier de référence : si cet
        // aller-retour relativisait ou résolvait, annuler un simple déplacement de
        // bloc changerait le chemin d'une image — et l'image disparaîtrait de la page
        // sans qu'aucun geste ne l'ait touchée.
        var document = Composer("notice", "photo.png", Magenta);
        var (fenetre, vm) = Fenetre(DocumentSerializer.Load(document));

        // Le modèle de vue connaît le dossier du document, comme dans l'application dès
        // qu'un fichier est ouvert. Sans cela le piège ne pourrait pas se refermer : un
        // instantané qui relativiserait n'aurait rien sur quoi le faire, et ce test
        // passerait pour une raison qui n'est pas la bonne.
        vm.FilePath = document;
        var chemin = ((ImageFrame)vm.Document.Pages[0].Frames[0]).Path;

        vm.BeginEdit();
        vm.Document.Pages[0].Frames[1].X += 40;   // un geste sur l'autre bloc
        vm.CommitEdit();
        Rendu.Battre();

        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();

        // L'annulation reconstruit le document : c'est le bloc de l'instant, et non
        // celui d'avant, qu'il faut relire.
        Assert.Equal(chemin, ((ImageFrame)vm.Document.Pages[0].Frames[0]).Path);
        Assert.True(Compter(fenetre, Magenta) > 1000, "l'image a disparu à l'annulation");
    }

    [AvaloniaFact]
    [Trait("poste", "Q3")]
    public void Une_image_remise_en_place_reparait_a_la_reouverture()
    {
        // Le défaut du poste Q3, joué comme on le vit : on ouvre un document, ses images
        // manquent ; on les remet en place ; on rouvre — par le même geste que le menu,
        // dans la même fenêtre, le même processus et donc le même cache. Avant le
        // poste, l'échec de la première lecture était retenu pour toujours et les blocs
        // restaient vides sans rien dire.
        //
        // Deux images, deux chemins vers le cache : le bloc image passe par la liaison,
        // le fond illustré d'une forme par « Conv.Bitmap », que le fond dessine en code.
        // Le cyan est l'image du bloc parce qu'il n'est pas symétrique — la leçon de A3.
        var dossier = Path.Combine(_travail, "rapport");
        Directory.CreateDirectory(dossier);
        var photo = Path.Combine(dossier, "photo.png");
        var trame = Path.Combine(dossier, "trame.png");

        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        document.Pages[0].Frames.Add(new ImageFrame
        {
            Name = "Photo", X = 60, Y = 60, Width = 200, Height = 160, Path = photo, Fit = ImageFit.Fill,
        });
        document.Pages[0].Frames.Add(new ShapeFrame
        {
            Name = "Trame", X = 60, Y = 300, Width = 200, Height = 160, Stroke = null,
            Fill = new ImageFill(trame),
        });
        var chemin = Path.Combine(dossier, "rapport.mypub");
        DocumentSerializer.Save(document, chemin);

        // La fenêtre s'ouvre sur un document vierge, puis ouvre le fichier comme le
        // menu le fait : c'est l'ouverture qui est éprouvée, pas l'affectation d'un
        // document au modèle de vue, que l'annulation emprunte aussi.
        var (fenetre, vm) = Fenetre(Document.CreateDefault());
        vm.LoadFrom(chemin);
        Rendu.Battre(8);

        // Le témoin que les images manquaient bien, et que les blocs étaient affichés.
        Assert.Equal(2, vm.CurrentPage!.Frames.Count);
        Assert.Equal(0, Compter(fenetre, Cyan));
        Assert.Equal(0, Compter(fenetre, Magenta));

        EcrirePng(photo, Cyan);
        EcrirePng(trame, Magenta);
        vm.LoadFrom(chemin);
        Rendu.Battre(8);

        var bloc = Compter(fenetre, Cyan);
        var fond = Compter(fenetre, Magenta);
        Assert.True(bloc > 1000, $"l'image du bloc ne reparaît pas à la réouverture ({bloc} pixels)");
        Assert.True(fond > 1000, $"le fond illustré ne reparaît pas à la réouverture ({fond} pixels)");

        // Et seulement les échecs : une image lue n'est pas relue à l'ouverture suivante.
        // Vider tout le cache aurait réparé le défaut aussi bien, au prix de relire et de
        // redécoder chaque image de chaque document rouvert ; c'est le choix du poste, et
        // il est épinglé ici pour qu'on ne le défasse pas sans le savoir.
        var lue = Conv.Bitmap(photo);
        Assert.NotNull(lue);
        vm.LoadFrom(chemin);
        Rendu.Battre(8);
        Assert.Same(lue, Conv.Bitmap(photo));
    }

    // ------------------------------------------------------------------- l'outillage

    /// <summary>
    /// Un dossier de travail : une image d'une couleur donnée et un document qui la
    /// montre, enregistré à côté d'elle — donc écrit en chemin relatif. Le second bloc
    /// est là pour qu'un geste puisse porter sur autre chose que l'image.
    /// </summary>
    private string Composer(string dossier, string image, uint couleur)
    {
        var racine = Path.Combine(_travail, dossier);
        Directory.CreateDirectory(racine);
        EcrirePng(Path.Combine(racine, image), couleur);

        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        document.Pages[0].Frames.Add(new ImageFrame
        {
            Name = "Photo", X = 60, Y = 60, Width = 200, Height = 160,
            Path = Path.Combine(racine, image), Fit = ImageFit.Fill,
        });
        document.Pages[0].Frames.Add(new ShapeFrame { Name = "Repère", X = 60, Y = 300, Width = 60, Height = 60 });

        var chemin = Path.Combine(racine, dossier + ".mypub");
        DocumentSerializer.Save(document, chemin);

        // La forme écrite fait partie de ce qui est éprouvé : sans elle, le document
        // survivrait au déplacement pour la mauvaise raison.
        Assert.Contains($"\"Path\": \"{image}\"", File.ReadAllText(chemin));
        return chemin;
    }

    private Window Ouvrir(string document) => Fenetre(DocumentSerializer.Load(document)).Fenetre;

    /// <summary>
    /// Une fenêtre à la fois, comme dans l'application : la précédente se ferme. Ce
    /// n'est pas de la propreté, c'est le geste qu'on reproduit — on ferme le document,
    /// on déplace le dossier, on rouvre.
    /// </summary>
    private (Window Fenetre, MainWindowViewModel Vm) Fenetre(Document document)
    {
        _ouverte?.Close();
        Rendu.Battre();

        var ouverture = Rendu.Ouvrir(document);
        Rendu.Battre(8);
        _ouverte = ouverture.Fenetre;
        return ouverture;
    }

    /// <summary>
    /// Combien de pixels de cette couleur exacte la fenêtre a composés. On compte sur
    /// toute la fenêtre plutôt que sur le bloc : une couleur qui ne se trouve nulle
    /// part ailleurs dans l'interface se repère sans avoir à la situer.
    /// </summary>
    private static int Compter(Window fenetre, uint couleur)
    {
        using var image = fenetre.CaptureRenderedFrame()
                          ?? throw new InvalidOperationException("aucune image composée");

        var pixels = new byte[image.PixelSize.Width * image.PixelSize.Height * 4];
        using (var verrou = image.Lock())
        {
            Marshal.Copy(verrou.Address, pixels, 0, pixels.Length);
        }

        // L'ordre des octets se demande à l'image plutôt qu'il ne se suppose : la trame
        // composée est en RGBA et non en BGRA. Le magenta, dont le rouge et le bleu
        // sont égaux, passait le test dans les deux sens et ne l'aurait jamais dit ;
        // c'est le cyan qui l'a révélé. Une couleur symétrique est un mauvais témoin.
        var attendu = Octets(couleur, image.Format ?? PixelFormat.Bgra8888);

        var compte = 0;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i] == attendu[0] && pixels[i + 1] == attendu[1] && pixels[i + 2] == attendu[2]) compte++;
        }

        return compte;
    }

    /// <summary>Une couleur #AARRGGBB dans l'ordre d'octets d'un format donné.</summary>
    private static byte[] Octets(uint couleur, PixelFormat format)
    {
        var b = (byte)(couleur & 0xFF);
        var g = (byte)((couleur >> 8) & 0xFF);
        var r = (byte)((couleur >> 16) & 0xFF);
        var a = (byte)(couleur >> 24);

        return format == PixelFormat.Rgba8888 ? [r, g, b, a] : [b, g, r, a];
    }

    /// <summary>Un PNG d'un seul aplat, écrit sur le disque comme le ferait n'importe quel outil.</summary>
    private static void EcrirePng(string chemin, uint couleur)
    {
        const int cote = 8;
        using var bitmap = new WriteableBitmap(
            new PixelSize(cote, cote), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

        var octets = Octets(couleur, PixelFormat.Bgra8888);
        var ligne = new byte[cote * 4];
        for (var x = 0; x < cote; x++) Array.Copy(octets, 0, ligne, x * 4, 4);

        using (var verrou = bitmap.Lock())
        {
            for (var y = 0; y < cote; y++)
            {
                Marshal.Copy(ligne, 0, verrou.Address + y * verrou.RowBytes, ligne.Length);
            }
        }

        bitmap.Save(chemin);
    }
}
