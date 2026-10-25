using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Le geste de rotation, joué sur la vraie couche de saisie de la vraie fenêtre —
/// même harnais que <see cref="GestesTests"/>.
///
/// La rotation existait partout — modèle, test de survol, redimensionnement, export —
/// et ne se réglait qu'au champ numérique du panneau. Ce qui est éprouvé ici, c'est le
/// geste : l'angle obtenu, son SENS, son aimantation sous Maj, son annulation, et le
/// fait que la page se redessine pendant qu'on tourne.
///
/// Le sens mérite son propre test. Une comparaison écran ↔ PDF ne l'attraperait pas —
/// les deux rendus lisent la même propriété et tourneraient de travers ensemble — et
/// la boîte d'encre d'un bloc tourné de +90° est exactement celle d'un bloc tourné de
/// −90°. Seule la valeur signée, obtenue par un geste dans chaque sens, épingle le
/// calcul lui-même.
/// </summary>
public class RotationTests
{
    /// <summary>
    /// Le bloc de mesure : 200 × 60 pt en (100, 100), donc de centre (200, 130) — sauf
    /// là où un test le déplace exprès, pour mettre sa poignée hors de la planche. Sans
    /// contour, pour que la boîte d'encre soit exactement celle du bloc — la recette la
    /// mesure au pixel dans les deux rendus.
    /// </summary>
    private static ShapeFrame Bloc() => new()
    {
        Name = "Bandeau", X = 100, Y = 100, Width = 200, Height = 60,
        Fill = new SolidFill("#FFCC0000"), Stroke = null,
    };

    private static (Window Fenetre, MainWindowViewModel Vm, ShapeFrame Bloc) Ouvrir()
    {
        var bloc = Bloc();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        vm.SelectOnly(bloc);
        Rendu.Battre();
        return (fenetre, vm, bloc);
    }

    private static Panel Page(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");

    /// <summary>Un point de la page, en points typographiques, dans le repère fenêtre.</summary>
    private static Point SurLaPage(Window fenetre, double x, double y)
    {
        var origine = Page(fenetre).TranslatePoint(new Point(0, 0), fenetre) ?? new Point(0, 0);
        return new Point(origine.X + x, origine.Y + y);
    }

    /// <summary>
    /// La poignée de rotation, relue dans la couche de poignées : c'est le seul disque
    /// qu'elle porte, les huit autres étant des carrés. On lit sa position plutôt que
    /// de la recalculer — un test qui refait le calcul du code ne prouve rien de lui.
    /// </summary>
    private static Ellipse? Poignee(Window fenetre) =>
        fenetre.GetVisualDescendants()
            .OfType<Canvas>().First(c => c.Name == "Overlay")
            .Children.OfType<Ellipse>()
            .FirstOrDefault(e => e.IsVisible);

    /// <summary>Centre de la poignée, en coordonnées de page.</summary>
    private static Point CentrePoignee(Window fenetre)
    {
        var poignee = Poignee(fenetre) ?? throw new InvalidOperationException("aucune poignée de rotation");
        return new Point(
            Canvas.GetLeft(poignee) + poignee.Width / 2,
            Canvas.GetTop(poignee) + poignee.Height / 2);
    }

    /// <summary>
    /// Saisit la poignée de rotation et amène le pointeur à <paramref name="vise"/>,
    /// exprimé en coordonnées de page. Le point de destination est pris LOIN du centre :
    /// c'est sa direction qui fait l'angle, et un pointeur rendu au pixel entier
    /// (0,75 pt à 100 %) pèse d'autant moins qu'il est loin du pivot.
    /// </summary>
    private static void Tourner(Window fenetre, Point vise, bool maj = false)
    {
        var prise = CentrePoignee(fenetre);
        var touches = maj ? RawInputModifiers.Shift : RawInputModifiers.None;

        fenetre.MouseDown(SurLaPage(fenetre, prise.X, prise.Y), MouseButton.Left, touches);
        fenetre.MouseMove(SurLaPage(fenetre, vise.X, vise.Y), touches);
        fenetre.MouseUp(SurLaPage(fenetre, vise.X, vise.Y), MouseButton.Left, touches);
        Rendu.Battre();
    }

    [AvaloniaFact]
    public void La_poignee_de_rotation_se_pose_au_dessus_du_bloc()
    {
        var (fenetre, _, bloc) = Ouvrir();

        // Au-dessus du bord haut, dans l'axe du bloc, à distance d'écran constante.
        var centre = CentrePoignee(fenetre);
        Assert.Equal(bloc.X + bloc.Width / 2, centre.X, precision: 6);
        Assert.True(centre.Y < bloc.Y, $"la poignée est à {centre.Y}, le bord haut à {bloc.Y}");
        Assert.InRange(bloc.Y - centre.Y, 15, 30);
    }

    [AvaloniaFact]
    public void Une_tige_relie_la_poignee_au_bloc()
    {
        // Le README promet « un disque au bout d'une tige » ; rien ne prouvait que
        // la tige se dessine. Le Polyline est arrangé en (0, 0) avec des points en
        // coordonnées absolues, comme les contours de sélection — un motif qui marche,
        // mais « qui marche » est exactement ce qu'on croyait de l'invalidation avant
        // R1. On regarde donc l'image, et non l'arbre visuel : la boîte du Polyline
        // s'arrête à l'ordonnée nulle, elle ne dit rien de ce qui est peint au-dessus.
        var (fenetre, _, bloc) = Ouvrir();
        var poignee = CentrePoignee(fenetre);

        // Entre le bas du disque et le haut du bloc, il ne peut y avoir que la tige.
        var encre = EncreDansLaBande(fenetre, bloc.X + bloc.Width / 2, poignee.Y + 6, bloc.Y - 2);

        Assert.True(encre > 8, $"la tige ne laisse que {encre} pixels entre le disque et le bloc");
    }

    [AvaloniaFact]
    public void La_poignee_de_rotation_ne_parait_que_sur_une_selection_dun_seul_bloc()
    {
        var premier = Bloc();
        var second = new ShapeFrame { Name = "Autre", X = 100, Y = 300, Width = 80, Height = 40 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(premier, second));

        Rendu.Battre();
        Assert.Null(Poignee(fenetre));          // rien de sélectionné

        vm.SelectOnly(premier);
        Rendu.Battre();
        Assert.NotNull(Poignee(fenetre));

        vm.SetSelection([premier, second]);
        Rendu.Battre();
        Assert.Null(Poignee(fenetre));          // deux blocs : on groupe pour tourner
    }

    [AvaloniaFact]
    public void Un_bloc_tourne_a_la_souris()
    {
        var (fenetre, _, bloc) = Ouvrir();

        // La poignée est au-dessus du centre ; l'amener à sa droite, à la même hauteur,
        // c'est un quart de tour dans le sens des aiguilles.
        Tourner(fenetre, new Point(bloc.X + bloc.Width / 2 + 200, bloc.Y + bloc.Height / 2));

        // Mesuré exact sur cette machine — le banc ne rabote pas la position du
        // pointeur. La marge est pour celles qui l'arrondissent au pixel, ce que C2b a
        // relevé : « une position posée à la souris ne vaut que ce que vaut le
        // pointeur ». L'exactitude, elle, se joue sous Maj.
        Assert.InRange(bloc.Rotation, 89, 91);

        // Tourner ne déplace ni ne redimensionne : le pivot est le centre du bloc, et
        // le geste n'écrit que la rotation.
        Assert.Equal(100, bloc.X, precision: 6);
        Assert.Equal(100, bloc.Y, precision: 6);
        Assert.Equal(200, bloc.Width, precision: 6);
        Assert.Equal(60, bloc.Height, precision: 6);
    }

    [AvaloniaFact]
    public void Un_geste_dans_lautre_sens_tourne_dans_lautre_sens()
    {
        // Le test qui épingle le SENS. Sans lui, une inversion de signe passerait
        // toutes les autres vérifications du poste : la boîte d'un bloc à +90° est
        // celle d'un bloc à −90°, et l'écran et le PDF se tromperaient ensemble.
        var (fenetre, _, bloc) = Ouvrir();

        Tourner(fenetre, new Point(bloc.X + bloc.Width / 2 - 200, bloc.Y + bloc.Height / 2), maj: true);

        Assert.Equal(270, bloc.Rotation);
    }

    [AvaloniaFact]
    public void Un_bloc_colle_au_haut_de_la_page_se_tourne_quand_meme()
    {
        // La poignée de rotation est la seule qui se pose hors du bloc, donc la seule
        // qui puisse sortir de la page. Un bloc posé à 5 pt du haut la mettait sur la
        // règle graduée, qui bordait alors la page : elle s'y voyait sans se laisser
        // prendre, et le geste posait un repère au lieu de tourner.
        //
        // Depuis le poste I4, la règle est fixe au bord de la zone visible, et la
        // poignée tombe sur la planche grise, que la couche de saisie couvre : elle se
        // prend là, règles affichées ou non (PlancheTests). Le test garde son nom et sa
        // promesse — le bloc tourne, aucun repère ne se pose.
        var bloc = Bloc();
        bloc.Y = 5;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        Assert.True(CentrePoignee(fenetre).Y < 0, "la poignée devrait être au-dessus de la page");

        Tourner(fenetre, new Point(bloc.X + bloc.Width / 2 + 200, bloc.Y + bloc.Height / 2), maj: true);

        Assert.Equal(90, bloc.Rotation);
        Assert.Empty(vm.Document.VerticalRulerGuides);
        Assert.Empty(vm.Document.HorizontalRulerGuides);
    }

    [AvaloniaFact]
    public void Maj_aimante_la_rotation_sur_le_pas_de_quinze_degres()
    {
        var (fenetre, _, bloc) = Ouvrir();

        // Visé de travers, à deux degrés du quart de tour : sans aimantation on
        // obtiendrait 88°. 90° est l'angle des trois objets tournés d'ATA 21, le seul
        // des deux documents ATR qui en porte.
        var centreX = bloc.X + bloc.Width / 2;
        var centreY = bloc.Y + bloc.Height / 2;
        Tourner(fenetre, new Point(centreX + 200, centreY - 7), maj: true);

        Assert.Equal(90, bloc.Rotation);
    }

    [AvaloniaFact]
    public void Laimantation_porte_sur_langle_et_non_sur_lecart_parcouru()
    {
        // La différence se voit sur un bloc déjà de travers : 7° plus un quart de tour
        // de vingt-huit degrés font 35°, qui s'aimantent sur 30. Un pas appliqué à
        // l'ÉCART rendrait 7 + 30 = 37° et lui conserverait son travers — or ce qu'on
        // veut d'un pas de rotation, c'est justement pouvoir redresser un bloc importé.
        var bloc = Bloc();
        bloc.Rotation = 7;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var centre = new Point(bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2);
        var prise = CentrePoignee(fenetre);
        var vise = Math.Atan2(prise.Y - centre.Y, prise.X - centre.X) + 28 * Math.PI / 180;

        Tourner(fenetre, new Point(
            centre.X + 120 * Math.Cos(vise), centre.Y + 120 * Math.Sin(vise)), maj: true);

        Assert.Equal(30, bloc.Rotation);
    }

    [AvaloniaFact]
    public void Une_rotation_sannule()
    {
        var (fenetre, vm, bloc) = Ouvrir();

        // Relevé après la sélection : elle fait passer la géométrie par le panneau de
        // propriétés, donc par un aller-retour en millimètres qui laisse un résidu
        // derrière lui — la dérive consignée au poste P3, sans rapport avec ce geste.
        var avant = DocumentSerializer.ToJson(vm.Document);

        Tourner(fenetre, new Point(bloc.X + bloc.Width / 2 + 200, bloc.Y + bloc.Height / 2), maj: true);
        Assert.Equal(90, bloc.Rotation);

        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();

        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void Un_bloc_tourne_a_la_souris_se_redessine()
    {
        // « RenderTargetBitmap.Render » ne vaudrait rien ici : il redessine de force et
        // passerait même si rien ne se rafraîchissait. CaptureRenderedFrame rend l'image
        // réellement composée — la seule qui puisse attraper une invalidation manquante.
        //
        // Et l'on mesure l'ENCRE ROUGE, celle du bloc, plutôt que de comparer deux
        // images entières : les poignées et le contour de sélection bougent eux aussi,
        // si bien que deux images différentes ne prouveraient rien du bloc.
        var (fenetre, _, bloc) = Ouvrir();
        var (largeurAvant, hauteurAvant) = BoiteRouge(fenetre);

        Assert.True(largeurAvant > hauteurAvant * 2,
            $"le bandeau est couché : {largeurAvant} × {hauteurAvant}");

        Tourner(fenetre, new Point(bloc.X + bloc.Width / 2 + 200, bloc.Y + bloc.Height / 2), maj: true);

        Assert.Equal(90, bloc.Rotation);

        // Le quart de tour transpose la boîte : ce qui était couché est debout.
        var (largeur, hauteur) = BoiteRouge(fenetre);
        Assert.InRange(largeur, hauteurAvant - 2, hauteurAvant + 2);
        Assert.InRange(hauteur, largeurAvant - 2, largeurAvant + 2);
    }

    [AvaloniaFact]
    public void La_poignee_suit_le_bloc_quelle_a_tourne()
    {
        // Un quart de tour amène le haut du bloc à sa droite : la poignée doit y aller
        // aussi, sans quoi le second geste partirait d'un point qui n'est plus le sien.
        var (fenetre, _, bloc) = Ouvrir();
        var ecart = bloc.Y + bloc.Height / 2 - CentrePoignee(fenetre).Y;

        Tourner(fenetre, new Point(bloc.X + bloc.Width / 2 + 200, bloc.Y + bloc.Height / 2), maj: true);

        var centre = CentrePoignee(fenetre);
        Assert.Equal(bloc.X + bloc.Width / 2 + ecart, centre.X, precision: 6);
        Assert.Equal(bloc.Y + bloc.Height / 2, centre.Y, precision: 6);
    }

    /// <summary>
    /// Le geste de la recette : un quart de tour sous Maj, puis le document écrit là où
    /// <c>MYPUBLISHER_RECETTE_ROTATION</c> le demande. C'est ce fichier-là que
    /// <c>Scripts/recettes/A1-poignee-rotation.sh</c> exporte et mesure — l'écran et le
    /// PDF y sont confrontés sur un angle obtenu à la souris, et non posé au champ.
    /// </summary>
    [AvaloniaFact]
    public void Le_geste_de_la_recette_ecrit_son_document()
    {
        var (fenetre, vm, bloc) = Ouvrir();

        Tourner(fenetre, new Point(bloc.X + bloc.Width / 2 + 200, bloc.Y + bloc.Height / 2), maj: true);

        Assert.Equal(90, bloc.Rotation);

        // La sélection ne s'écrit pas dans le document, mais elle en dessine le contour
        // et les poignées : le rendu de la recette la trouverait sur l'image.
        vm.Selection.Clear();
        Rendu.Battre();

        if (Environment.GetEnvironmentVariable("MYPUBLISHER_RECETTE_ROTATION") is { } chemin &&
            !string.IsNullOrWhiteSpace(chemin))
        {
            DocumentSerializer.Save(vm.Document, chemin);
        }
    }

    /// <summary>
    /// L'image RÉELLEMENT composée par la fenêtre, avec de quoi la lire : le pas d'une
    /// ligne, et l'indice de la composante rouge.
    ///
    /// Elle sort en RGBA, là où « RenderTargetBitmap » rend du BGRA. Lire l'un pour
    /// l'autre ne fait échouer aucune lecture : on ne trouve simplement plus une seule
    /// trace de rouge. On lit donc le format au lieu de le supposer.
    /// </summary>
    private sealed record Composee(byte[] Octets, int Pas, int Largeur, int Hauteur, int Rouge);

    private static Composee Image(Window fenetre)
    {
        var image = fenetre.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException("aucune image composée");

        using (image)
        {
            using var verrou = image.Lock();
            var octets = new byte[verrou.RowBytes * image.PixelSize.Height];
            System.Runtime.InteropServices.Marshal.Copy(verrou.Address, octets, 0, octets.Length);

            return new Composee(octets, verrou.RowBytes,
                image.PixelSize.Width, image.PixelSize.Height,
                verrou.Format == PixelFormat.Bgra8888 ? 2 : 0);
        }
    }

    /// <summary>
    /// Boîte de l'encre rouge — celle du bloc — dans l'image composée. Le rouge écarte
    /// tout ce que la sélection dessine par-dessus : contour bleu, poignées blanches,
    /// disque de rotation et tige.
    ///
    /// La recherche s'en tient à la planche. Depuis la refonte de la fenêtre (poste I2),
    /// le dock montre « Fond et contour » sans défiler, et son sélecteur de couleur peint
    /// le fond du bloc — du même rouge — à côté de la planche.
    /// </summary>
    private static (int Largeur, int Hauteur) BoiteRouge(Window fenetre)
    {
        var image = Image(fenetre);
        var bleu = 2 - image.Rouge;

        var planche = fenetre.GetVisualDescendants().OfType<Control>().First(c => c.Name == "PageView");
        var coin = planche.TranslatePoint(new Point(0, 0), fenetre) ?? new Point(0, 0);
        var (x0, y0) = ((int)coin.X, (int)coin.Y);
        var x1 = Math.Min(image.Largeur, x0 + (int)planche.Bounds.Width);
        var y1 = Math.Min(image.Hauteur, y0 + (int)planche.Bounds.Height);

        int gauche = int.MaxValue, haut = int.MaxValue, droite = -1, bas = -1;

        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                // Rouge franc, peu de vert et peu de bleu.
                var i = y * image.Pas + x * 4;
                if (image.Octets[i + image.Rouge] <= 150 ||
                    image.Octets[i + bleu] >= 100 ||
                    image.Octets[i + 1] >= 100) continue;

                gauche = Math.Min(gauche, x);
                haut = Math.Min(haut, y);
                droite = Math.Max(droite, x);
                bas = Math.Max(bas, y);
            }
        }

        Assert.True(droite >= 0, "aucune encre rouge dans l'image composée");

        // En pixels de l'image composée, et non en points : seules deux mesures prises
        // de la même façon sont comparées, l'une avant le geste, l'autre après.
        return (droite - gauche + 1, bas - haut + 1);
    }

    /// <summary>
    /// Pixels non blancs dans une bande verticale étroite, décrite en coordonnées de
    /// page. Au zoom du banc, une unité de l'image composée vaut un point.
    /// </summary>
    private static int EncreDansLaBande(Window fenetre, double x, double haut, double bas)
    {
        var origine = Page(fenetre).TranslatePoint(new Point(0, 0), fenetre) ?? new Point(0, 0);
        var image = Image(fenetre);
        var trouves = 0;

        for (var ligne = (int)(origine.Y + haut); ligne < (int)(origine.Y + bas); ligne++)
        {
            for (var colonne = (int)(origine.X + x) - 3; colonne <= (int)(origine.X + x) + 3; colonne++)
            {
                if (ligne < 0 || colonne < 0 || ligne >= image.Hauteur || colonne >= image.Largeur) continue;

                var i = ligne * image.Pas + colonne * 4;
                if (image.Octets[i] > 200 && image.Octets[i + 1] > 200 && image.Octets[i + 2] > 200) continue;
                trouves++;
            }
        }

        return trouves;
    }
}
