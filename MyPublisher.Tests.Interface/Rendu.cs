using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
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
/// Ouvrir la vraie fenêtre sur un document, y jouer des gestes, et relever l'encre qu'elle
/// dessine.
///
/// Ces quelques lignes étaient dans <c>RetraitsTests</c>, qui les avait écrites en
/// premier ; elles en sont sorties au poste R3, qui en a besoin pour mesurer autre
/// chose. Ce n'est pas un rangement de commodité : c'est le piège du banc — <c>PageRoot</c>
/// est mesuré en points mais vit sous la mise à l'échelle du zoom — qu'on ne veut garder
/// qu'à un seul endroit. Deux copies, et la seconde oublie l'assertion.
///
/// Les gestes ont suivi au poste N1 : toutes les aides qui les synthétisent sont ici, et
/// nulle part ailleurs (voir « Les gestes »).
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

    #region Les gestes

    // ================================================================== les gestes
    //
    // Toutes les aides qui synthétisent un geste sont ici, et nulle part ailleurs (poste N1).
    // Elles vivaient recopiées de classe en classe : treize « SurLaPage », dont dix
    // retombaient en silence sur (0, 0) quand PageRoot manquait ; six « Cliquer », dont deux
    // sans l'attente d'image du clic ci-dessous ; trois « Glisser », dont deux qui ne
    // disaient pas le bouton tenu pendant le mouvement — or un TextBox n'étend une
    // sélection que si le mouvement le dit ; quatre « UnSeulPas » ; cinq entrées en saisie.
    // La phase N change le sens de ces gestes (N12 : on entre dans le texte d'un clic) :
    // elle le fera ici, une fois.
    //
    // Ce qu'est une aide de geste, au sens du contrôle de la recette N1
    // (Scripts/aides-de-geste.py) : un membre qui n'est pas un test et qui synthétise
    // lui-même une entrée — qui appelle MouseDown, MouseUp, MouseMove, MouseWheel,
    // KeyPress, KeyPressQwerty, KeyRelease, KeyReleaseQwerty, KeyTextInput ou DragDrop —,
    // ou qui porte le nom d'une aide de cette région. Une aide d'une classe qui compose
    // celles-ci (« Tracer », « TirerLaPoignee ») n'en est pas une : elle ne synthétise rien.
    // Un test, lui, peut jouer ses entrées en ligne quand c'est ce qu'il éprouve — le
    // mouvement sans bouton du banc, le glissé d'un point de Q14.

    /// <summary>
    /// Une image, comme l'écran la rendrait : ce qui attend, l'image, puis ce qu'elle a
    /// laissé à faire. Avalonia cherche le contrôle sous le pointeur dans la dernière image
    /// rendue (voir <see cref="Cliquer(Control)"/>) : un geste qui finit par une image laisse
    /// au suivant une fenêtre telle que la main la verrait.
    /// </summary>
    public static void UneImage()
    {
        Battre();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Battre();
    }

    // ------------------------------------------------------------------ où viser

    private static T Nomme<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static MainWindowViewModel Vm(Window fenetre) =>
        fenetre.DataContext as MainWindowViewModel
        ?? throw new InvalidOperationException("la fenêtre n'a pas de MainWindowViewModel");

    /// <summary>
    /// Où tombe un point de page — en points typographiques — dans la fenêtre, à
    /// l'échelle de la planche. Lu depuis le coin de PageRoot dans Surface, que la planche
    /// ne fait que mettre à l'échelle : de PageRoot droit à la fenêtre, à travers la mise
    /// à l'échelle, Avalonia peut se tromper (poste I4). À l'échelle du banc, un point y
    /// vaut un pixel, et le résultat est celui des copies qu'elle remplace.
    ///
    /// PageRoot absent, elle échoue en le disant : dix des copies retombaient sur (0, 0),
    /// et le geste partait au coin de la fenêtre sans que rien ne le dise.
    /// </summary>
    public static Point SurLaPage(Window fenetre, double x, double y)
    {
        var page = Nomme<Panel>(fenetre, "PageRoot");
        var surface = Nomme<Panel>(fenetre, "Surface");
        var echelle = Vm(fenetre).RenderScale;
        var coin = page.TranslatePoint(default, surface)
                   ?? throw new InvalidOperationException("PageRoot n'est pas sous Surface");
        return surface.TranslatePoint(new Point(coin.X + x * echelle, coin.Y + y * echelle), fenetre)
               ?? throw new InvalidOperationException("Surface n'est pas dans la fenêtre");
    }

    /// <summary>Le centre d'un bloc, dans la fenêtre — celui d'un bloc tourné est son pivot.</summary>
    public static Point Centre(Window fenetre, Frame bloc) =>
        SurLaPage(fenetre, bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2);

    /// <summary>
    /// Le centre d'un contrôle, dans la racine qui le porte, tel que l'arbre le place —
    /// sans attendre que l'image l'y montre : c'est <see cref="Cliquer(Control)"/> qui
    /// attend.
    /// </summary>
    public static Point Centre(Control controle)
    {
        var racine = controle.GetVisualRoot() as Visual ?? throw new InvalidOperationException("hors de l'arbre");
        return controle.TranslatePoint(new Point(controle.Bounds.Width / 2, controle.Bounds.Height / 2), racine)
               ?? throw new InvalidOperationException($"« {Nom(controle)} » n'est pas dans sa racine");
    }

    /// <summary>
    /// Un point du cadre d'un bloc droit, dans la fenêtre : <paramref name="fx"/> et
    /// <paramref name="fy"/> en fractions de sa largeur et de sa hauteur — (0, 0) le coin
    /// d'en haut à gauche, (1, 1) celui d'en bas à droite.
    /// </summary>
    public static Point Coin(Window fenetre, Frame bloc, double fx, double fy) =>
        SurLaPage(fenetre, bloc.X + fx * bloc.Width, bloc.Y + fy * bloc.Height);

    /// <summary>
    /// Où saisir une poignée de redimensionnement d'un bloc droit, dans la fenêtre :
    /// <paramref name="fx"/> et <paramref name="fy"/> valent 0, ½ ou 1, comme pour
    /// <see cref="Coin"/>. Les poignées sont posées sur le cadre de sélection, 3,25 px
    /// d'écran hors du bloc (poste I4), à toute échelle.
    /// </summary>
    public static Point Poignee(Window fenetre, Frame bloc, double fx, double fy)
    {
        var ecart = 3.25 / Vm(fenetre).RenderScale;
        var x = bloc.X + (fx == 0 ? -ecart : fx == 1 ? bloc.Width + ecart : bloc.Width / 2);
        var y = bloc.Y + (fy == 0 ? -ecart : fy == 1 ? bloc.Height + ecart : bloc.Height / 2);
        return SurLaPage(fenetre, x, y);
    }

    /// <summary>
    /// La poignée de rotation, relue dans la couche de poignées : c'est le seul disque
    /// qu'elle porte, les huit autres étant des carrés. Aucune sans sélection d'un bloc
    /// seul. On lit sa position plutôt que de la recalculer — un test qui refait le calcul
    /// du code ne prouve rien de lui.
    /// </summary>
    public static Ellipse? PoigneeDeRotation(Window fenetre) =>
        Nomme<Canvas>(fenetre, "Overlay").Children.OfType<Ellipse>().SingleOrDefault(e => e.IsVisible);

    /// <summary>Le centre de la poignée de rotation, en points de page.</summary>
    public static Point CentreDeLaRotation(Window fenetre)
    {
        var disque = PoigneeDeRotation(fenetre) ?? throw new InvalidOperationException("aucune poignée de rotation");
        return new Point(Canvas.GetLeft(disque) + disque.Width / 2, Canvas.GetTop(disque) + disque.Height / 2);
    }

    // ------------------------------------------------------------------ la souris

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
    /// Un clic en un point de la fenêtre — de la page, par <see cref="SurLaPage"/>, le
    /// plus souvent —, touches tenues, puis l'image qui le montre. Il n'attend rien avant :
    /// un point n'est pas un contrôle, il n'y a rien à voir arriver dessous.
    ///
    /// <paramref name="mouvementNul"/> glisse entre la pression et le relâchement un
    /// mouvement qui ne va nulle part, bouton tenu : c'est ainsi qu'une vraie plate-forme
    /// peut livrer un clic, et c'est ce mouvement-là qui passait par l'aimantation au poste
    /// Q14. Un banc qui ne l'émettrait pas ne verrait que la moitié du défaut.
    /// </summary>
    public static void Cliquer(TopLevel racine, Point point, RawInputModifiers touches = RawInputModifiers.None,
                               MouseButton bouton = MouseButton.Left, bool mouvementNul = false)
    {
        racine.MouseDown(point, bouton, touches);
        if (mouvementNul) racine.MouseMove(point, touches | Tenu(bouton));
        racine.MouseUp(point, bouton, touches);
        UneImage();
    }

    /// <summary>Le bouton tenu, tel qu'un mouvement le dit.</summary>
    private static RawInputModifiers Tenu(MouseButton bouton) => bouton switch
    {
        MouseButton.Left => RawInputModifiers.LeftMouseButton,
        MouseButton.Right => RawInputModifiers.RightMouseButton,
        MouseButton.Middle => RawInputModifiers.MiddleMouseButton,
        _ => throw new ArgumentOutOfRangeException(nameof(bouton), bouton, "ni gauche, ni droit, ni milieu"),
    };

    /// <summary>
    /// Un clic droit au centre d'un contrôle, attendu comme <see cref="Cliquer(Control)"/>,
    /// et le menu contextuel qu'il ouvre.
    ///
    /// Avalonia 11.3.9 ouvre le <c>ContextMenu</c> au relâché du bouton droit : le contrôle
    /// que le relâché atteint lève <c>ContextRequested</c> (<c>Control.OnPointerReleased</c>),
    /// qui remonte jusqu'au premier qui porte un menu. Sans affichage, la fenêtre ne crée pas de
    /// fenêtre surgissante (<c>HeadlessWindowImpl.CreatePopup</c> rend <c>null</c>) : le
    /// menu se pose sur elle, en surimpression, et <see cref="Cliquer(Control)"/> atteint
    /// ses entrées. Dix images au plus, comme pour un clic ; au-delà, aucun menu ne s'est
    /// ouvert, et le test le dit.
    /// </summary>
    public static ContextMenu CliquerDroit(Control controle)
    {
        controle.BringIntoView();
        var racine = controle.GetVisualRoot() as TopLevel ?? throw new InvalidOperationException("hors de l'arbre");
        var centre = Viser(controle, racine);
        racine.MouseDown(centre, MouseButton.Right);
        racine.MouseUp(centre, MouseButton.Right);
        return MenuOuvert(racine, $"un clic droit sur « {Nom(controle)} »");
    }

    /// <summary>
    /// Le menu contextuel ouvert dans cette racine, attendu image après image, dix au plus.
    /// <paramref name="geste"/> dit, dans l'échec, ce qui devait l'ouvrir.
    /// </summary>
    public static ContextMenu MenuOuvert(TopLevel racine, string geste)
    {
        for (var image = 0; ; image++)
        {
            Battre();
            var ouverts = racine.GetVisualDescendants().OfType<ContextMenu>().Where(m => m.IsOpen).ToList();
            Assert.True(ouverts.Count <= 1, $"{ouverts.Count} menus contextuels ouverts à la fois");
            if (ouverts.Count == 1) return ouverts[0];

            Assert.True(image < 10, $"{geste} n'a ouvert aucun menu contextuel en dix images");
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    /// <summary>
    /// Deux pressions au même point, sans rien entre elles, puis ce que la fenêtre a à
    /// faire. Avalonia reconnaît un double-clic à l'horloge et à la distance : deux
    /// pressions à moins de 500 ms, dans un carré de 4 px centré sur la première
    /// (<c>MouseDevice</c>, <c>DefaultPlatformSettings</c>) — le banc date chaque pression
    /// du moment où il la livre, c'est un vrai chronomètre.
    /// </summary>
    public static void DoubleCliquer(TopLevel racine, Point point)
    {
        for (var clic = 0; clic < 2; clic++)
        {
            racine.MouseDown(point, MouseButton.Left);
            racine.MouseUp(point, MouseButton.Left);
        }

        Battre(8);
    }

    /// <summary>
    /// Saisit en <paramref name="depart"/>, mène en <paramref name="arrivee"/> en
    /// <paramref name="pas"/> mouvements égaux, lâche. Le bouton gauche est toujours dit
    /// tenu pendant le mouvement, comme le dit une vraie plate-forme : un TextBox n'étend
    /// une sélection que si le mouvement le dit (<c>TextBox.OnPointerMoved</c>), et deux
    /// des trois copies l'oubliaient. Un glissé sans bouton n'est pas un glissé : le test
    /// qui veut le montrer le joue en ligne.
    ///
    /// Deux pas par défaut, comme une main : un geste qui ne verrait que son arrivée ne
    /// dirait pas s'il suit le chemin ou s'il saute. Un seul, là où les copies n'en
    /// faisaient qu'un.
    /// </summary>
    public static void Glisser(TopLevel racine, Point depart, Point arrivee,
                               RawInputModifiers touches = RawInputModifiers.None, int pas = 2)
    {
        Assert.True(pas >= 1, "un glissé fait au moins un mouvement");
        racine.MouseDown(depart, MouseButton.Left, touches);
        Battre();
        for (var i = 1; i <= pas; i++)
        {
            var ici = i == pas ? arrivee : depart + (arrivee - depart) * i / pas;
            racine.MouseMove(ici, touches | RawInputModifiers.LeftMouseButton);
        }

        Battre();
        racine.MouseUp(arrivee, MouseButton.Left, touches);
        Battre();
    }

    // ------------------------------------------------------------------ le clavier

    /// <summary>
    /// Une touche, nommée par sa place sur un clavier QWERTY — ce que livre
    /// <c>KeyPressQwerty</c> —, puis ce que la fenêtre a à faire. Pressée seulement, comme
    /// l'ont toujours fait les tests ; <paramref name="relacher"/> la relâche aussi, ce
    /// que demande la touche Menu, qu'Avalonia lit au relâché (<c>Control.OnKeyUp</c>).
    /// </summary>
    public static void Touche(TopLevel racine, PhysicalKey touche, RawInputModifiers touches = RawInputModifiers.None,
                              bool relacher = false)
    {
        racine.KeyPressQwerty(touche, touches);
        if (relacher) racine.KeyReleaseQwerty(touche, touches);
        Battre();
    }

    /// <summary>
    /// Ce qu'une touche d'un clavier AZERTY français livre : sa place, la touche qu'elle
    /// nomme et le caractère qu'elle porte. Les lettres seules et la rangée du bas : le
    /// reste est commun aux deux claviers, ou dépend de Maj.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<PhysicalKey, (Key Touche, string Symbole)> Azerty = new()
    {
        [PhysicalKey.Q] = (Key.A, "a"),
        [PhysicalKey.A] = (Key.Q, "q"),
        [PhysicalKey.W] = (Key.Z, "z"),
        [PhysicalKey.Z] = (Key.W, "w"),
        [PhysicalKey.Semicolon] = (Key.M, "m"),
        [PhysicalKey.M] = (Key.OemComma, ","),
        [PhysicalKey.Comma] = (Key.OemSemicolon, ";"),
        [PhysicalKey.Period] = (Key.Oem2, ":"),
        [PhysicalKey.Slash] = (Key.Oem8, "!"),
    };

    /// <summary>
    /// Une touche d'un clavier AZERTY, nommée par sa place : <c>PhysicalKey.Q</c> livre
    /// <c>Key.A</c> et « a ». Les liaisons de touches d'Avalonia comparent la touche, non
    /// sa place (<c>KeyGesture.Matches</c>) : Ctrl+A se joue donc sur la place de Q. Ce que
    /// le banc ne sait pas dire, c'est ce que X11 livre vraiment — pour « &lt; », par
    /// exemple, sur la touche que n'a pas un clavier QWERTY. Une place hors de la table est
    /// celle d'une touche commune aux deux claviers — les autres lettres, Entrée, Échap, les
    /// flèches, les touches de fonction — et part en QWERTY ; celles qui diffèrent sans être
    /// dans la table — la rangée des chiffres, les crochets, l'apostrophe — échouent plutôt
    /// que de livrer ce qu'un AZERTY ne livre pas.
    /// </summary>
    public static void ToucheAzerty(TopLevel racine, PhysicalKey touche, RawInputModifiers touches = RawInputModifiers.None,
                                    bool relacher = false)
    {
        if (!Azerty.TryGetValue(touche, out var livree))
        {
            if (touche is >= PhysicalKey.Digit0 and <= PhysicalKey.Digit9 or PhysicalKey.Minus or PhysicalKey.Equal
                or PhysicalKey.BracketLeft or PhysicalKey.BracketRight or PhysicalKey.Quote or PhysicalKey.Backquote
                or PhysicalKey.Backslash or PhysicalKey.IntlBackslash)
            {
                throw new ArgumentException($"le banc ne sait pas ce que la place {touche} livre sur un AZERTY", nameof(touche));
            }

            Touche(racine, touche, touches, relacher);
            return;
        }

        racine.KeyPress(livree.Touche, touches, touche, livree.Symbole);
        if (relacher) racine.KeyRelease(livree.Touche, touches, touche, livree.Symbole);
        Battre();
    }

    /// <summary>Du texte, tel que la frappe le livre au contrôle qui a le focus.</summary>
    public static void Taper(TopLevel racine, string texte)
    {
        racine.KeyTextInput(texte);
        Battre();
    }

    /// <summary>Tape dans un champ numérique, valide, et rend le focus — comme on quitte un champ.</summary>
    public static void Saisir(Window fenetre, NumericUpDown champ, string texte)
    {
        var saisie = champ.GetVisualDescendants().OfType<TextBox>().First();
        saisie.Focus();
        Battre();
        saisie.SelectAll();
        fenetre.KeyTextInput(texte);
        fenetre.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Battre();
        fenetre.FocusManager!.ClearFocus();
        Battre();
    }

    // ------------------------------------------------------------------ le texte

    /// <summary>
    /// La zone de saisie ouverte sur un bloc de texte, cherchée sous PageRoot : un
    /// <c>TextBox</c> visible dont le bloc est le contexte. <c>null</c> hors saisie.
    /// </summary>
    public static TextBox? ZoneDeSaisie(Window fenetre, TextFrame bloc) =>
        Nomme<Panel>(fenetre, "PageRoot").GetVisualDescendants().OfType<TextBox>()
            .SingleOrDefault(zone => ReferenceEquals(zone.DataContext, bloc) && zone.IsVisible);

    /// <summary>
    /// Entre en saisie dans un bloc comme l'utilisateur : le bloc pris, puis un
    /// double-clic à (<paramref name="dx"/>, <paramref name="dy"/>) de son coin. C'est le
    /// seul endroit que N12 changera, quand on entrera dans le texte d'un clic. Rend la
    /// zone de saisie, et échoue si la saisie ne s'est pas ouverte.
    /// </summary>
    public static TextBox EntrerEnSaisie(Window fenetre, MainWindowViewModel vm, TextFrame bloc, double dx = 40, double dy = 8)
    {
        vm.SelectOnly(bloc);
        Battre();

        DoubleCliquer(fenetre, SurLaPage(fenetre, bloc.X + dx, bloc.Y + dy));
        Assert.True(bloc.IsEditing, "le double-clic n'a pas ouvert la saisie sur la page");

        return ZoneDeSaisie(fenetre, bloc) ?? throw new InvalidOperationException("la saisie est ouverte, mais aucune zone ne se voit");
    }

    /// <summary>
    /// Le bord avant d'un caractère d'une zone de texte, à mi-hauteur de sa ligne, dans la
    /// racine qui la porte : là où une pression pose le curseur devant lui. Lu dans la mise
    /// en page du texte (<c>TextLayout.HitTestTextPosition</c>), non recalculé. L'indice
    /// qui suit le dernier caractère est le bout du texte.
    /// </summary>
    public static Point BordDuCaractere(TextBox zone, int indice)
    {
        var racine = zone.GetVisualRoot() as Visual ?? throw new InvalidOperationException("hors de l'arbre");
        var presentation = zone.GetVisualDescendants().OfType<TextPresenter>().Single();
        var case_ = presentation.TextLayout.HitTestTextPosition(indice);
        return presentation.TranslatePoint(new Point(case_.X, case_.Y + case_.Height / 2), racine)
               ?? throw new InvalidOperationException("la zone de texte n'est pas dans sa racine");
    }

    /// <summary>
    /// Sélectionne au pointeur les caractères [<paramref name="debut"/>, <paramref name="fin"/>)
    /// d'une zone de texte : un glissé, bouton tenu, du bord avant du caractère
    /// <paramref name="debut"/> au bord avant du caractère <paramref name="fin"/>
    /// (<see cref="BordDuCaractere"/>), une fois que l'image montre la zone. La pression y
    /// pose le curseur (<c>TextBox.OnPointerPressed</c>, qui demande le bouton gauche), le
    /// mouvement l'étend — si le bouton est dit tenu (<c>TextBox.OnPointerMoved</c>).
    /// </summary>
    public static void SelectionnerDuTexte(TextBox zone, int debut, int fin)
    {
        var racine = zone.GetVisualRoot() as TopLevel ?? throw new InvalidOperationException("hors de l'arbre");
        Viser(zone, racine);
        Glisser(racine, BordDuCaractere(zone, debut), BordDuCaractere(zone, fin));
    }

    /// <summary>
    /// Le texte que porte le presse-papiers. Sans affichage, c'est un magasin en mémoire
    /// (<c>HeadlessClipboardImplStub</c>), commun à toute la suite, formats propres
    /// compris ; le presse-papiers du système ne s'éprouve pas ici.
    /// </summary>
    public static string? TexteCopie(TopLevel racine) =>
        Attendre((racine.Clipboard ?? throw new InvalidOperationException("aucun presse-papiers")).TryGetTextAsync());

    // ------------------------------------------------------------------ l'annulation

    /// <summary>
    /// Le geste a fait exactement un pas : il y en a un, qui a changé le document, et une
    /// seule annulation rend le document d'avant, sans en laisser d'autre. Rend le bloc
    /// sélectionné après l'annulation : elle relit le document, les blocs d'avant sont
    /// d'autres instances, et le test reprend le sien dans la sélection qu'elle rétablit.
    /// </summary>
    public static Frame? UnSeulPas(MainWindowViewModel vm, string avant, string quoi)
    {
        Assert.True(vm.CanUndo, $"{quoi} : aucun pas d'annulation");
        Assert.NotEqual(avant, DocumentSerializer.ToJson(vm.Document));
        vm.UndoCommand.Execute(null);
        Battre();
        Assert.False(vm.CanUndo, $"{quoi} : plus d'un pas d'annulation");
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        return vm.SingleSelected;
    }

    #endregion

    /// <summary>
    /// Le résultat d'une tâche du fil de l'interface — le presse-papiers est asynchrone —,
    /// en faisant tourner la file tant qu'elle n'est pas finie.
    /// </summary>
    public static T Attendre<T>(System.Threading.Tasks.Task<T> tache)
    {
        Attendre((System.Threading.Tasks.Task)tache);
        return tache.GetAwaiter().GetResult();
    }

    public static void Attendre(System.Threading.Tasks.Task tache)
    {
        for (var tour = 0; !tache.IsCompleted; tour++)
        {
            Assert.True(tour < 100, "la tâche ne finit pas");
            Dispatcher.UIThread.RunJobs();
        }

        tache.GetAwaiter().GetResult();
    }
}
