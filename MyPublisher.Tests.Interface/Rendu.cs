using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Converters;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Ouvrir la vraie fenêtre sur un document et relever l'encre qu'elle dessine.
///
/// Ces quelques lignes étaient dans <c>RetraitsTests</c>, qui les avait écrites en
/// premier ; elles en sont sorties au poste R3, qui en a besoin pour mesurer autre
/// chose. Ce n'est pas un rangement de commodité : c'est le piège du banc — <c>PageRoot</c>
/// est mesuré en points mais vit sous la mise à l'échelle du zoom — qu'on ne veut garder
/// qu'à un seul endroit. Deux copies, et la seconde oublie l'assertion.
/// </summary>
public static class Rendu
{
    /// <summary>Deux pixels par point : la même échelle que <c>pdftoppm -r 144</c>.</summary>
    public const double PixelsParPoint = 2;

    public sealed record Image(byte[] Pixels, int Largeur, int Hauteur);

    /// <summary>Une fenêtre sur un document, sans horloge ni configuration de machine.</summary>
    public static (Window Fenetre, MainWindowViewModel Vm) Ouvrir(Document document)
    {
        var vm = new MainWindowViewModel(
            contacts: new BusinessInfoStore(),
            horloge: () => new DateTime(2026, 9, 6));

        vm.Document = document;
        vm.CurrentPage = document.Pages[0];
        vm.FilePath = "banc.mypub";   // évite l'ouverture du catalogue au démarrage
        // À ce zoom, une unité de mise en page vaut exactement un point typographique.
        vm.Zoom = 1 / Units.PointToDip;
        // Les repères ne s'impriment pas ; ils n'ont rien à faire dans une mesure d'encre.
        vm.ShowGuides = false;

        var fenetre = new MainWindow { DataContext = vm, Width = 1400, Height = 900 };
        fenetre.Show();
        Battre();
        return (fenetre, vm);
    }

    public static void Battre(int tours = 4)
    {
        for (var i = 0; i < tours; i++) Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Un clic de la souris au centre d'un contrôle, dans la racine qui le porte : la
    /// fenêtre, menus ouverts compris — le banc les pose sur elle, en surimpression.
    ///
    /// Trois copies de ces lignes vivaient dans <c>DispositionTests</c>, <c>JeuxTests</c> et
    /// <c>BarreContextuelleTests</c>, avec le même défaut, que la suite entière faisait
    /// tomber trois fois sur quatorze au poste I3 : un clic dans un menu tout juste ouvert,
    /// ou sur un bouton que la barre venait d'échanger, passait à travers sans rien faire.
    /// Les bornes n'y étaient pour rien — la mise en page était faite, le centre visé juste.
    /// Ce qui retardait, c'est l'atteinte : Avalonia ne cherche pas le contrôle sous le
    /// pointeur dans l'arbre visuel, mais dans la dernière image que le compositeur a
    /// rendue. Or le compositeur ne prend un lot de changements qu'une fois le précédent
    /// rendu, et le banc sans tête ne rend qu'une image avant chaque geste, une seule.
    /// Mesuré le 25 octobre 2026 : au moment de chacun de ces clics, un lot attendait
    /// encore son image ; il en fallait une, parfois deux, pour que le contrôle soit sous
    /// son centre. Le menu des sortes du souligné n'était pas encore dans l'image, et la
    /// pression allait à la règle, dessous. Le minuteur de rendu, à soixante images par
    /// seconde, rattrapait souvent ce retard pendant un <see cref="Battre"/> — d'où un
    /// échec qui ne venait qu'en suite entière, où les gestes s'enchaînent en moins d'une
    /// image. L'application n'a pas ce défaut : son minuteur rend chaque lot à l'image
    /// suivante, bien avant qu'une main ait pu viser l'entrée d'un menu qui s'ouvre.
    ///
    /// Le clic attend donc, image après image comme l'écran les rendrait, que le contrôle
    /// soit celui que le pointeur atteint à son centre. S'il ne l'est jamais, c'est qu'autre
    /// chose le couvre, et le test le dit au lieu de cliquer sur cette autre chose.
    /// </summary>
    public static void Cliquer(Control controle)
    {
        controle.BringIntoView();
        var racine = controle.GetVisualRoot() as TopLevel ?? throw new InvalidOperationException("hors de l'arbre");
        var centre = Viser(controle, racine);
        racine.MouseDown(centre, MouseButton.Left);
        racine.MouseUp(centre, MouseButton.Left);
        Battre();
    }

    /// <summary>
    /// Le centre d'un contrôle, une fois que l'image rendue l'y montre. L'atteinte est
    /// cherchée comme la fenêtre la cherche quand un geste lui arrive
    /// (<c>TopLevel.HandleInput</c>) : sans écarter les contrôles grisés.
    /// </summary>
    private static Point Viser(Control controle, TopLevel racine)
    {
        for (var image = 0; ; image++)
        {
            Battre();
            var centre = controle.TranslatePoint(new Point(controle.Bounds.Width / 2, controle.Bounds.Height / 2), racine)
                         ?? throw new InvalidOperationException($"« {Nom(controle)} » n'est pas dans sa racine");
            var touche = racine.InputHitTest(centre, enabledElementsOnly: false) as Visual;
            if (touche is not null && (ReferenceEquals(touche, controle) || touche.GetVisualAncestors().Contains(controle)))
            {
                return centre;
            }

            // Une ou deux images rattrapent un menu qui s'ouvre ; dix laissent passer une
            // transition courte, pas un contrôle couvert.
            Assert.True(image < 10,
                $"un clic au centre de « {Nom(controle)} » ({centre}) tomberait sur « {(touche is null ? "rien" : Nom(touche))} »");
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    /// <summary>Ce qui désigne un contrôle dans un message : son nom, son libellé de menu ou sa bulle.</summary>
    private static string Nom(Visual visuel) => visuel switch
    {
        Control { Name: { Length: > 0 } nom } => nom,
        MenuItem { Header: { } entete } => $"{entete}",
        Control c when ToolTip.GetTip(c) is { } bulle => $"{bulle}",
        _ => visuel.GetType().Name,
    };

    /// <summary>
    /// Ouvre un onglet du dock — « Propriétés », « Styles », « Couleurs » ou « Document » —,
    /// comme un clic sur son titre. Depuis le poste I2, le dock a quatre onglets, et un
    /// contrôle d'un onglet non choisi n'est pas dans l'arbre visuel : un test qui cherche
    /// le format de page, un jeu ou le fond de page ouvre d'abord son onglet. Une aide
    /// commune, pour que tous l'ouvrent de la même façon et qu'un onglet renommé fasse
    /// échouer la recherche ici, en le disant, et non plus loin sur un contrôle introuvable.
    /// </summary>
    public static void Onglet(Window fenetre, string titre)
    {
        var onglets = fenetre.GetVisualDescendants().OfType<TabControl>().Single(t => t.Name == "OngletsDuDock");
        var onglet = onglets.Items.OfType<TabItem>().SingleOrDefault(t => Equals(t.Header, titre))
                     ?? throw new InvalidOperationException($"le dock n'a pas d'onglet « {titre} »");
        onglets.SelectedItem = onglet;
        Battre();
    }

    /// <summary>Une page seule, posée dans un document par ailleurs vierge.</summary>
    public static Document Feuille(params Frame[] blocs)
    {
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        foreach (var bloc in blocs) document.Pages[0].Frames.Add(bloc);
        return document;
    }

    public static string Racine()
    {
        var dossier = new DirectoryInfo(AppContext.BaseDirectory);
        while (dossier is not null && !File.Exists(Path.Combine(dossier.FullName, "MyPublisher.sln")))
        {
            dossier = dossier.Parent;
        }

        Assert.NotNull(dossier);
        return dossier.FullName;
    }

    /// <summary>
    /// ATA 21, importé comme l'application l'importe. Écrit d'abord dans
    /// <c>FrappeTests</c> au poste Q1, sorti ici au poste Q14, qui ouvre le même
    /// document pour y double-cliquer. Le lecteur est cherché là où la recette R5 le cherche : la
    /// variable d'environnement, puis le PATH, puis l'extraction locale de
    /// <c>Scripts/outils-pub.sh</c>. Introuvable, le test échoue en disant comment
    /// l'obtenir — il ne se déclare pas vert sur un document qu'il n'a pas ouvert.
    /// </summary>
    public static Document Ata21()
    {
        var racine = Racine();

        if (!PubReader.Disponible())
        {
            var local = Path.Combine(racine, ".outils", "mspub", "bin", "pub2raw");
            Assert.True(File.Exists(local),
                "pub2raw introuvable : « sudo apt install libmspub-tools », ou Scripts/outils-pub.sh.");

            Environment.SetEnvironmentVariable("MYPUBLISHER_PUB2RAW", local);
            var bibliotheques = Path.Combine(racine, ".outils", "mspub", "lib");
            var actuel = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
            Environment.SetEnvironmentVariable("LD_LIBRARY_PATH",
                string.IsNullOrEmpty(actuel) ? bibliotheques : $"{bibliotheques}:{actuel}");
        }

        return DocumentSerializer.Import(
            Path.Combine(racine, "MyPublisher", "Books", "ATA 21 - Air conditioning.pub")).Document;
    }

    /// <summary>
    /// La page rendue hors champ à deux pixels par point — la même échelle que
    /// <c>pdftoppm -r 144</c>, celle des recettes des postes.
    /// </summary>
    public static Image Encre(Window fenetre)
    {
        var page = fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");

        // Le piège du banc, gardé ici comme là-bas : PageRoot est mesuré en points mais
        // vit sous la mise à l'échelle du zoom. Le rendre sans avoir neutralisé
        // celle-ci dessinerait son contenu 96/72 trop grand dans une image à la bonne
        // taille, et rien ne le signalerait.
        var facteur = page.RenderTransform?.Value.M11 ?? 1;
        Assert.True(Math.Abs(facteur - 1) < 1e-6, $"la page est à l'échelle {facteur}, pas 1");

        var taille = new PixelSize(
            (int)Math.Ceiling(page.Bounds.Width * PixelsParPoint),
            (int)Math.Ceiling(page.Bounds.Height * PixelsParPoint));

        using var rendu = new RenderTargetBitmap(
            taille, new Vector(96 * PixelsParPoint, 96 * PixelsParPoint));
        rendu.Render(page);

        var pixels = new byte[taille.Width * taille.Height * 4];
        using var tampon = new WriteableBitmap(taille, rendu.Dpi, PixelFormat.Bgra8888);
        using (var verrou = tampon.Lock())
        {
            rendu.CopyPixels(new PixelRect(0, 0, taille.Width, taille.Height),
                verrou.Address, pixels.Length, verrou.RowBytes);
            System.Runtime.InteropServices.Marshal.Copy(verrou.Address, pixels, 0, pixels.Length);
        }

        return new Image(pixels, taille.Width, taille.Height);
    }

    /// <summary>
    /// L'image RÉELLEMENT composée de la fenêtre, celle que l'utilisateur voit.
    ///
    /// À ne pas confondre avec <see cref="Encre"/>, qui rend la page à la demande par
    /// <c>RenderTargetBitmap</c> : celui-ci redessine l'arbre visuel de force, quel
    /// qu'ait été l'état d'invalidation, et passe donc même quand rien ne se
    /// rafraîchit à l'écran. Il mesure le modèle, pas l'affichage. Un défaut
    /// d'affichage a survécu quatre postes faute de cette distinction.
    ///
    /// Les deux ont leur emploi : <see cref="Encre"/> mesure OÙ le texte se pose,
    /// celle-ci mesure SI l'écran a suivi.
    /// </summary>
    public static Image Composee(Window fenetre)
    {
        var image = fenetre.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException("aucune image composée");

        using (image)
        {
            var pixels = new byte[image.PixelSize.Width * image.PixelSize.Height * 4];
            using var verrou = image.Lock();
            System.Runtime.InteropServices.Marshal.Copy(verrou.Address, pixels, 0, pixels.Length);
            return new Image(pixels, image.PixelSize.Width, image.PixelSize.Height);
        }
    }

    /// <summary>
    /// Les pixels composés que couvre un contrôle donné, et rien d'autre.
    ///
    /// Comparer la fenêtre entière ne prouverait pas grand-chose d'une case de la barre
    /// d'état : la position du curseur, à deux cases de là, change à chaque mouvement
    /// de pointeur, et la différence viendrait d'elle. Le découpage est pris sur les
    /// limites du contrôle au moment de la mesure, si bien qu'une case qui se déplace
    /// sans changer de contenu rend les mêmes pixels.
    /// </summary>
    public static byte[] Bande(Window fenetre, Control controle)
    {
        var image = Composee(fenetre);
        var origine = controle.TranslatePoint(new Point(0, 0), fenetre)
                      ?? throw new InvalidOperationException(
                          $"« {controle.Name} » n'est pas dans la fenêtre");

        var x = (int)Math.Floor(origine.X);
        var y = (int)Math.Floor(origine.Y);
        var largeur = (int)Math.Floor(controle.Bounds.Width);
        var hauteur = (int)Math.Floor(controle.Bounds.Height);

        Assert.True(largeur > 0 && hauteur > 0,
            $"« {controle.Name} » n'occupe rien ({controle.Bounds}) : une bande vide serait égale à elle-même");
        Assert.True(x >= 0 && y >= 0 && x + largeur <= image.Largeur && y + hauteur <= image.Hauteur,
            $"« {controle.Name} » déborde de l'image composée ({image.Largeur}×{image.Hauteur})");

        var bande = new byte[largeur * hauteur * 4];
        for (var ligne = 0; ligne < hauteur; ligne++)
        {
            Array.Copy(image.Pixels, ((y + ligne) * image.Largeur + x) * 4,
                bande, ligne * largeur * 4, largeur * 4);
        }

        return bande;
    }

    /// <summary>
    /// Qui écoute cet objet par cet événement. Le champ d'un événement déclaré comme un
    /// champ est privé, et souvent dans un type de base — « ObservableObject »,
    /// « ObservableCollection » : on remonte donc la hiérarchie, « GetField » ne voyant
    /// pas les champs privés d'un type de base.
    ///
    /// Un abonné en trop ne se voit d'aucune autre façon depuis l'extérieur d'un
    /// événement. Écrit d'abord dans <c>FondsTests</c> au poste C5, il est sorti ici au
    /// poste Q2, qui compte les abonnés d'un autre contrôle : deux copies du même
    /// tâtonnement par réflexion, et la seconde finit par compter autre chose.
    /// La copie du noyau, elle, reste : les deux projets de tests ne partagent aucun
    /// assemblage.
    /// </summary>
    public static Delegate[] Abonnes(object source, string evenement = "PropertyChanged")
    {
        for (var type = source.GetType(); type is not null; type = type.BaseType)
        {
            var champ = type.GetField(evenement, System.Reflection.BindingFlags.Instance
                                                 | System.Reflection.BindingFlags.NonPublic
                                                 | System.Reflection.BindingFlags.DeclaredOnly);
            if (champ is null) continue;

            return champ.GetValue(source) is Delegate delegue ? delegue.GetInvocationList() : [];
        }

        // Un événement introuvable n'est pas un événement sans abonné : sans cette
        // exception, une faute de frappe dans son nom donnerait zéro, et zéro est
        // justement ce que les tests de fuite attendent.
        throw new InvalidOperationException(
            $"aucun champ d'événement « {evenement} » sur {source.GetType().Name}");
    }

    /// <summary>
    /// Un pixel franchement sombre. Le seuil écarte le lissage des bords, qui
    /// déplacerait le fer d'un pixel ou deux selon la lettre.
    /// </summary>
    public static bool EstEncre(Image image, int x, int y)
    {
        if (x < 0 || y < 0 || x >= image.Largeur || y >= image.Hauteur) return false;

        var i = (y * image.Largeur + x) * 4;
        return image.Pixels[i] < 128 && image.Pixels[i + 1] < 128 && image.Pixels[i + 2] < 128;
    }
}
