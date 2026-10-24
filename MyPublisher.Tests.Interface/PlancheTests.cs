using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// La planche de la maquette — poste I4 : le fond gris et l'ombre de la feuille, les
/// règles fixes graduées de part et d'autre de la page, la projection de la sélection,
/// les repères prolongés sur toute la planche, les marques de la sélection et la bulle
/// de dimensions.
///
/// Tout se mesure sur l'image que la fenêtre compose réellement, à l'échelle 1 — un
/// point de page y vaut un pixel —, sauf ce qui dépend de l'échelle : les règles, la
/// projection, les marges et les repères sont repris à l'échelle 2, où un point n'est plus
/// un pixel, et les repères à deux zooms où la page ne tombe pas sur un pixel entier.
/// Les couleurs attendues sont lues dans
/// l'apparence, non recopiées : que chaque clé porte la couleur de la maquette, c'est la
/// recette de I1 qui le vérifie contre la maquette elle-même. Ici, on vérifie que la
/// planche les pose là où la maquette les pose.
///
/// La recette du poste — <c>Scripts/recettes/I4-Q10-planche.sh</c> — joue ces tests par leur
/// trait, puis confronte l'écran au PDF sur l'exemple et sur ATA 21.
/// </summary>
[Trait("poste", "I4")]
public class PlancheTests
{
    // ------------------------------------------------------------------ outillage

    private static Color Couleur(string cle)
    {
        Assert.True(Application.Current!.TryGetResource(cle, ThemeVariant.Light, out var valeur),
            $"« {cle} » n'est pas dans l'apparence");
        return Assert.IsType<Color>(valeur);
    }

    /// <summary>
    /// L'image composée, lue pixel par pixel. Le banc sans tête la rend en RGBA, et non en
    /// BGRA comme <c>Rendu.Encre</c> : on lit son format plutôt que de le supposer.
    /// </summary>
    private sealed class Image
    {
        private readonly byte[] _pixels;
        private readonly bool _bgra;

        public int Largeur { get; }
        public int Hauteur { get; }

        public Image(Window fenetre)
        {
            // Un tic de rendu de plus avant l'image, comme les tests de l'apparence :
            // « CaptureRenderedFrame » n'avance l'horloge que d'un tic, et l'image composée
            // peut alors être celle d'avant le dernier changement. Mesuré : un repère tout
            // juste posé manquait à la première image une fois sur deux, et se trouvait,
            // entier, à la suivante — l'écran était juste, l'image en retard.
            Rendu.Battre();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Rendu.Battre();
            using var image = fenetre.CaptureRenderedFrame()
                              ?? throw new InvalidOperationException("aucune image composée");
            _bgra = image.Format == PixelFormat.Bgra8888;
            Assert.True(_bgra || image.Format == PixelFormat.Rgba8888, $"format inattendu : {image.Format}");

            Largeur = image.PixelSize.Width;
            Hauteur = image.PixelSize.Height;
            _pixels = new byte[Largeur * Hauteur * 4];
            using var verrou = image.Lock();
            for (var ligne = 0; ligne < Hauteur; ligne++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    verrou.Address + ligne * verrou.RowBytes, _pixels, ligne * Largeur * 4, Largeur * 4);
            }
        }

        public Color this[double x, double y]
        {
            get
            {
                var i = ((int)Math.Floor(y) * Largeur + (int)Math.Floor(x)) * 4;
                var (r, b) = _bgra ? (_pixels[i + 2], _pixels[i]) : (_pixels[i], _pixels[i + 2]);
                return Color.FromRgb(r, _pixels[i + 1], b);
            }
        }
    }

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static void Pareil(Color attendu, Color lu, string ou) =>
        Assert.True(Hex(attendu) == Hex(lu), $"{ou} : {Hex(lu)} lu, {Hex(attendu)} attendu");

    private static void Proche(Color attendu, Color lu, int ecart, string ou) =>
        Assert.True(Math.Abs(attendu.R - lu.R) <= ecart && Math.Abs(attendu.G - lu.G) <= ecart &&
                    Math.Abs(attendu.B - lu.B) <= ecart,
            $"{ou} : {Hex(lu)} lu, {Hex(attendu)} attendu à {ecart} près");

    private static (Window Fenetre, MainWindowViewModel Vm) Ouvrir(Document document, bool reperes = false)
    {
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.ShowGuides = reperes;
        Rendu.Battre();
        return (fenetre, vm);
    }

    /// <summary>
    /// La même fenêtre, à un autre zoom : <paramref name="echelle"/> pixels par point. Le
    /// zoom n'est jamais l'effet d'une ouverture (règle 5) : le test le pose, comme le
    /// ferait l'utilisateur.
    /// </summary>
    private static (Window Fenetre, MainWindowViewModel Vm) OuvrirA(double echelle, Document document, bool reperes = false)
    {
        var (fenetre, vm) = Ouvrir(document, reperes);
        vm.Zoom = echelle / Units.PointToDip;
        Rendu.Battre();
        Assert.Equal(echelle, vm.RenderScale, precision: 9);
        return (fenetre, vm);
    }

    /// <summary>Le point de page, dans la fenêtre, à n'importe quelle échelle.</summary>
    private static Point DansLaFenetreA(Window fenetre, double x, double y) =>
        Page(fenetre).TranslatePoint(new Point(x, y), fenetre) ?? throw new InvalidOperationException();

    /// <summary>La culture courante posée le temps d'un geste, puis rendue.</summary>
    private static void SousLaCulture(string nom, Action geste)
    {
        var (avant, avantUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(nom);
            geste();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (avant, avantUi);
        }
    }

    private static T Trouver<T>(Window fenetre, string nom) where T : Control =>
        fenetre.GetVisualDescendants().OfType<T>().First(c => c.Name == nom);

    private static Panel Page(Window fenetre) => Trouver<Panel>(fenetre, "PageRoot");

    /// <summary>L'origine de la page dans la fenêtre : un point de page y vaut un pixel.</summary>
    private static Point Origine(Window fenetre) =>
        Page(fenetre).TranslatePoint(new Point(0, 0), fenetre) ?? throw new InvalidOperationException();

    private static Point SurLaPage(Window fenetre, double x, double y)
    {
        var origine = Origine(fenetre);
        return new Point(origine.X + x, origine.Y + y);
    }

    /// <summary>Le zéro d'une règle, dans la fenêtre — celui qu'elle dessine.</summary>
    private static Point Zero(Window fenetre, string nom)
    {
        var regle = Trouver<Ruler>(fenetre, nom);
        var local = regle.Orientation == Avalonia.Layout.Orientation.Horizontal
            ? new Point(regle.Origine, regle.Bounds.Height / 2)
            : new Point(regle.Bounds.Width / 2, regle.Origine);
        return regle.TranslatePoint(local, fenetre) ?? throw new InvalidOperationException();
    }

    private static Rect DansLaFenetre(Window fenetre, Control controle)
    {
        var coin = controle.TranslatePoint(new Point(0, 0), fenetre) ?? throw new InvalidOperationException();
        return new Rect(coin, controle.Bounds.Size);
    }

    private static Border Bulle(Window fenetre) =>
        Trouver<Canvas>(fenetre, "Overlay").Children.OfType<Border>()
            .Single(b => b.Classes.Contains("bulleDeDimensions"));

    private static string Millimetres(double points) => $"{Units.PointsToMm(points):0.0}";

    // ------------------------------------------------------------------ fond et feuille

    [AvaloniaFact]
    public void La_planche_est_grise_et_la_page_a_le_blanc_du_papier_sous_une_ombre_double()
    {
        // Une page assez petite pour se voir en entier : l'ombre de la maquette est décalée
        // vers le bas, et c'est sous la page qu'elle se lit le mieux.
        var document = Rendu.Feuille();
        document.PageWidth = 300;
        document.PageHeight = 400;
        var (fenetre, _) = Ouvrir(document);
        var image = new Image(fenetre);
        var o = Origine(fenetre);
        var planche = Couleur("CouleurFondPlanche");

        // Le gris de la planche, loin de la feuille ; le blanc du papier, sur la page.
        Pareil(planche, image[o.X - 60, o.Y + 200], "planche, à gauche de la page");
        Pareil(planche, image[o.X + 150, o.Y - 40], "planche, au-dessus de la page");
        Pareil(Colors.White, image[o.X + 150, o.Y + 200], "page");
        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(Page(fenetre).Background).Color);

        // L'ombre : contre la feuille, plus sombre que la planche, et éteinte à 40 px.
        var contre = image[o.X - 1, o.Y + 200];
        Assert.True(contre.R < planche.R - 10 && contre.G < planche.G - 10 && contre.B < planche.B - 10,
            $"pas d'ombre contre la feuille : {Hex(contre)} sur une planche {Hex(planche)}");
        Pareil(planche, image[o.X - 40, o.Y + 200], "l'ombre s'éteint");

        // Décalée vers le bas, comme celle de la maquette (« 0 6px 20px ») : à la même
        // distance, l'ombre est plus sombre sous la page qu'au-dessus.
        var dessus = image[o.X + 150, o.Y - 4];
        var dessous = image[o.X + 150, o.Y + 400 + 4];
        Assert.True(dessous.R < dessus.R - 4,
            $"l'ombre n'est pas portée vers le bas : {Hex(dessous)} dessous, {Hex(dessus)} dessus");
    }

    // ------------------------------------------------------------------ règles

    [AvaloniaFact]
    public void Les_regles_restent_fixes_et_leur_zero_suit_la_page_quand_elle_defile()
    {
        // Un A4 à l'échelle 1 dépasse la zone visible : la page défile.
        var (fenetre, _) = Ouvrir(Rendu.Feuille());
        var haut = Trouver<Ruler>(fenetre, "TopRuler");
        var gauche = Trouver<Ruler>(fenetre, "LeftRuler");
        var defilement = Trouver<ScrollViewer>(fenetre, "Scroller");

        // Fixes au bord de la zone visible, sur toute sa largeur et toute sa hauteur.
        var zoneVisible = DansLaFenetre(fenetre, defilement);
        Assert.Equal(zoneVisible.Width, haut.Bounds.Width, precision: 6);
        Assert.Equal(zoneVisible.Height, gauche.Bounds.Height, precision: 6);
        Assert.Equal(zoneVisible.Left, DansLaFenetre(fenetre, haut).Left, precision: 6);
        Assert.Equal(zoneVisible.Top, DansLaFenetre(fenetre, gauche).Top, precision: 6);
        Assert.True(defilement.Extent.Height > defilement.Viewport.Height + 60,
            "la page tient dans la zone visible : il n'y a rien à faire défiler");

        var avant = (Haut: DansLaFenetre(fenetre, haut), Gauche: DansLaFenetre(fenetre, gauche), Page: Origine(fenetre));
        Assert.Equal(avant.Page.X, Zero(fenetre, "TopRuler").X, precision: 6);
        Assert.Equal(avant.Page.Y, Zero(fenetre, "LeftRuler").Y, precision: 6);

        defilement.Offset = new Vector(0, 60);
        Rendu.Battre();

        // Les règles n'ont pas bougé ; la page est montée de 60 px, et le zéro avec elle.
        Assert.Equal(avant.Haut, DansLaFenetre(fenetre, haut));
        Assert.Equal(avant.Gauche, DansLaFenetre(fenetre, gauche));
        Assert.Equal(avant.Page.Y - 60, Origine(fenetre).Y, precision: 6);
        Assert.Equal(Origine(fenetre).Y, Zero(fenetre, "LeftRuler").Y, precision: 6);
        Assert.Equal(Origine(fenetre).X, Zero(fenetre, "TopRuler").X, precision: 6);
    }

    [AvaloniaFact]
    public void Les_regles_se_graduent_de_part_et_d_autre_de_la_page()
    {
        var (fenetre, _) = Ouvrir(Rendu.Feuille());
        var image = new Image(fenetre);
        var graduation = Couleur("CouleurGraduation");

        // Les graduations principales, de part et d'autre du zéro : la règle du haut en
        // porte à gauche de la page — les valeurs négatives — comme sur elle.
        var regle = DansLaFenetre(fenetre, Trouver<Ruler>(fenetre, "TopRuler"));
        var zero = Zero(fenetre, "TopRuler").X;
        var ligne = regle.Top + Ruler.Breadth - 3;   // dans les traits, au pied de la face
        var traits = Enumerable.Range((int)regle.Left, (int)regle.Width)
            .Where(x => Hex(image[x, ligne]) == Hex(graduation)).ToList();

        Assert.Contains(traits, x => x < zero - 10);
        Assert.Contains(traits, x => x > zero + 10);
        Assert.Contains(traits, x => Math.Abs(x - Math.Floor(zero)) < 1);   // le zéro lui-même

        // La règle de gauche aussi, au-dessus de la page.
        regle = DansLaFenetre(fenetre, Trouver<Ruler>(fenetre, "LeftRuler"));
        zero = Zero(fenetre, "LeftRuler").Y;
        var colonne = regle.Left + Ruler.Breadth - 3;
        traits = Enumerable.Range((int)regle.Top, (int)regle.Height)
            .Where(y => Hex(image[colonne, y]) == Hex(graduation)).ToList();
        Assert.Contains(traits, y => y < zero - 10);
        Assert.Contains(traits, y => y > zero + 10);
    }

    [AvaloniaFact]
    public void Les_nombres_des_regles_sont_negatifs_avant_la_page_et_dans_la_police_de_l_interface()
    {
        var (fenetre, _) = Ouvrir(Rendu.Feuille());
        var haut = Trouver<Ruler>(fenetre, "TopRuler");

        // Le libellé d'une graduation : négatif à gauche de la page et au-dessus, dans la
        // culture courante — le signe de la culture, puis le même nombre qu'à droite.
        var moins = NumberFormatInfo.CurrentInfo.NegativeSign;
        Assert.Equal("10", Ruler.Libelle(10));
        Assert.Equal(moins + "10", Ruler.Libelle(-10));
        Assert.Equal(moins + "20", Ruler.Libelle(-20.4));

        // Et il s'écrit, à gauche du zéro : sur les dix rangées du haut de la règle, où ne
        // descend aucune graduation, tout ce qui n'est pas le fond de la règle est un
        // chiffre — et il est plus sombre que les graduations.
        var image = new Image(fenetre);
        var fond = Couleur("CouleurFondFenetre");
        var regle = DansLaFenetre(fenetre, haut);
        var zero = Zero(fenetre, "TopRuler").X;
        var encre = (from y in Enumerable.Range((int)regle.Top, 10)
                     from x in Enumerable.Range((int)regle.Left, (int)(zero - regle.Left) - 2)
                     select image[x, y]).Where(c => Hex(c) != Hex(fond)).ToList();
        Assert.True(encre.Count > 20, $"pas de nombre à gauche du zéro : {encre.Count} pixels d'encre");
        Assert.Contains(encre, c => c.G < Couleur("CouleurGraduation").G);

        // La police des nombres est celle que la règle hérite de la fenêtre — celle de la
        // bulle, Inter —, et non la police par défaut du système, qui change de machine en
        // machine.
        var bulle = Assert.IsType<TextBlock>(Bulle(fenetre).Child);
        Assert.Equal(bulle.FontFamily, haut.Police.FontFamily);
        Assert.Contains("Inter", haut.Police.FontFamily.Name);
        Assert.NotEqual(Typeface.Default.FontFamily, haut.Police.FontFamily);
    }

    [AvaloniaFact]
    public void A_l_echelle_2_les_regles_se_graduent_au_double()
    {
        // À l'échelle 1, un point vaut un pixel : une graduation posée sans l'échelle y
        // tombe juste. Ici, dix millimètres font deux fois 28,35 pixels, partagés en deux
        // pas de cinq.
        var (fenetre, vm) = OuvrirA(2, Rendu.Feuille());
        var image = new Image(fenetre);
        var graduation = Hex(Couleur("CouleurGraduation"));
        var haut = Trouver<Ruler>(fenetre, "TopRuler");
        Assert.Equal(2, haut.Scale, precision: 9);
        Assert.Equal((5.0, 5), Ruler.Pas(Units.MmToPoints(1) * 2));

        var regle = DansLaFenetre(fenetre, haut);
        var zero = Zero(fenetre, "TopRuler").X;
        Assert.Equal(DansLaFenetreA(fenetre, 0, 0).X, zero, precision: 6);
        var ligne = regle.Top + Ruler.Breadth - 3;
        int Colonne(double mm) => (int)Math.Floor(zero + Units.MmToPoints(mm) * 2 + 1e-3);

        // Entre le zéro et dix millimètres, trois graduations principales, et pas une de plus.
        var traits = Enumerable.Range(Colonne(0) - 1, Colonne(10) - Colonne(0) + 3)
            .Where(x => Hex(image[x, ligne]) == graduation).ToList();
        Assert.Equal([Colonne(0), Colonne(5), Colonne(10)], traits);
    }

    // ------------------------------------------------------------------ projection

    [AvaloniaFact]
    public void La_projection_dun_bloc_tourne_couvre_exactement_son_emprise_sur_chaque_regle() =>
        ProjeterUnBlocTourne(1, gauche: 150, haut: 220);

    /// <summary>
    /// La même mesure à l'échelle 2 : l'emprise, en points, fait deux fois plus de pixels
    /// sur la règle. Un bloc plus près du coin, pour que sa projection reste en vue.
    /// </summary>
    [AvaloniaFact]
    public void A_l_echelle_2_la_projection_couvre_encore_exactement_l_emprise() =>
        ProjeterUnBlocTourne(2, gauche: 60, haut: 90);

    private static void ProjeterUnBlocTourne(double echelle, double gauche, double haut)
    {
        // Tourné de 30° : son emprise déborde son cadre des deux côtés. Une projection qui
        // lirait X et la largeur tomberait à côté, de part et d'autre.
        var bloc = new ShapeFrame
        {
            Name = "Tourné", X = gauche, Y = haut, Width = 200, Height = 80, Rotation = 30,
            Fill = new SolidFill("#FFCC0000"), Stroke = null,
        };
        var (fenetre, vm) = OuvrirA(echelle, Rendu.Feuille(bloc));
        var emprise = bloc.Extent;
        Assert.True(emprise.Left < bloc.X - 5 && emprise.Right > bloc.X + bloc.Width + 5 &&
                    emprise.Top < bloc.Y - 30 && emprise.Bottom > bloc.Y + bloc.Height + 30,
            "l'emprise ne déborde pas le cadre");

        var sans = new Image(fenetre);
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avec = new Image(fenetre);

        // Ce que la sélection change sur chaque règle : la bande, et rien d'autre.
        foreach (var (nom, horizontale) in new[] { ("TopRuler", true), ("LeftRuler", false) })
        {
            var regle = DansLaFenetre(fenetre, Trouver<Ruler>(fenetre, nom));
            var zero = Zero(fenetre, nom);
            var changes = (from y in Enumerable.Range((int)regle.Top, (int)regle.Height)
                           from x in Enumerable.Range((int)regle.Left, (int)regle.Width)
                           where Hex(sans[x, y]) != Hex(avec[x, y])
                           select horizontale ? x : y).ToList();
            Assert.NotEmpty(changes);

            var (debut, fin) = horizontale
                ? (zero.X + emprise.Left * echelle, zero.X + emprise.Right * echelle)
                : (zero.Y + emprise.Top * echelle, zero.Y + emprise.Bottom * echelle);
            Assert.InRange(changes.Min(), debut - 1, debut + 1);
            Assert.InRange(changes.Max() + 1, fin - 1, fin + 1);
        }

        // La teinte : l'accent à 14 % sur le gris de la règle, comme la maquette l'écrit.
        var face = Couleur("CouleurFondFenetre");
        var accent = Couleur("CouleurAccent");
        var regleDuHaut = DansLaFenetre(fenetre, Trouver<Ruler>(fenetre, "TopRuler"));
        var milieu = Zero(fenetre, "TopRuler").X + (emprise.Left + emprise.Right) / 2 * echelle;
        var y0 = Enumerable.Range((int)regleDuHaut.Top, (int)regleDuHaut.Height)
            .First(y => Hex(sans[milieu, y]) == Hex(face));
        Proche(Color.FromRgb(
                (byte)Math.Round(accent.R * 0.14 + face.R * 0.86),
                (byte)Math.Round(accent.G * 0.14 + face.G * 0.86),
                (byte)Math.Round(accent.B * 0.14 + face.B * 0.86)),
            avec[milieu, y0], 2, "la bande de projection");
    }

    [AvaloniaFact]
    public void La_projection_suit_le_bloc_pendant_le_geste()
    {
        var bloc = new ShapeFrame { Name = "Pris", X = 100, Y = 120, Width = 80, Height = 60, Stroke = null };
        var (fenetre, vm) = Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var haut = Trouver<Ruler>(fenetre, "TopRuler");
        var gauche = Trouver<Ruler>(fenetre, "LeftRuler");
        Assert.Equal((100.0, 180.0), (haut.DebutProjete, haut.FinProjetee));

        // Mené de 50 et de 40 points, Alt tenu contre l'aimantation, et NON relâché.
        fenetre.MouseDown(SurLaPage(fenetre, 140, 150), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 190, 190), RawInputModifiers.LeftMouseButton | RawInputModifiers.Alt);
        Rendu.Battre();

        Assert.Equal((150.0, 230.0), (haut.DebutProjete, haut.FinProjetee));
        Assert.Equal((160.0, 220.0), (gauche.DebutProjete, gauche.FinProjetee));
        fenetre.MouseUp(SurLaPage(fenetre, 190, 190), MouseButton.Left);

        // Deux blocs pris ensemble : la projection couvre les deux, du plus à gauche au plus
        // à droite, du plus haut au plus bas — et non le premier seul.
        var autre = new ShapeFrame { Name = "Autre", X = 300, Y = 40, Width = 50, Height = 30, Stroke = null };
        vm.CurrentPage!.Frames.Add(autre);
        vm.SetSelection([bloc, autre]);
        Rendu.Battre();
        Assert.Equal((150.0, 350.0), (haut.DebutProjete, haut.FinProjetee));
        Assert.Equal((40.0, 220.0), (gauche.DebutProjete, gauche.FinProjetee));

        // Sans sélection, plus rien n'est projeté.
        vm.SetSelection([]);
        Rendu.Battre();
        Assert.True(double.IsNaN(haut.DebutProjete) && double.IsNaN(gauche.DebutProjete));
    }

    // ------------------------------------------------------------------ repères

    [AvaloniaFact]
    public void Un_repere_tire_de_la_regle_se_voit_a_gauche_de_la_page_et_lache_hors_de_la_page_disparait()
    {
        var (fenetre, vm) = Ouvrir(Rendu.Feuille(), reperes: true);
        var repere = Couleur("CouleurRepere");
        var planche = Couleur("CouleurFondPlanche");

        // Tiré de la règle du haut, mené sur la planche à 20 pt à gauche de la page — et
        // pas encore lâché.
        var zero = Zero(fenetre, "TopRuler");
        fenetre.MouseDown(new Point(zero.X + 300, zero.Y), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, -20, 300), RawInputModifiers.LeftMouseButton);
        Rendu.Battre();

        Assert.Equal(-20, Assert.Single(vm.Document.VerticalRulerGuides).Position, precision: 6);
        var image = new Image(fenetre);
        var o = Origine(fenetre);
        Pareil(repere, image[o.X - 20, o.Y + 300], "le repère, à gauche de la page");
        Pareil(repere, image[o.X - 20, o.Y - 10], "le repère, au-dessus de la page");

        // Lâché là, sur la planche hors de la page : il est retiré, et ne se voit plus.
        fenetre.MouseUp(SurLaPage(fenetre, -20, 300), MouseButton.Left);
        Rendu.Battre();

        Assert.Empty(vm.Document.VerticalRulerGuides);
        Assert.Equal("Repère retiré", vm.Status);
        Pareil(planche, new Image(fenetre)[o.X - 20, o.Y + 300], "la planche, le repère retiré");
    }

    [AvaloniaFact]
    public void Un_repere_lache_sur_la_planche_tout_pres_de_la_page_est_retire_quoique_aimante_au_bord()
    {
        var (fenetre, vm) = Ouvrir(Rendu.Feuille(), reperes: true);

        // Mené sur le gris, à 4 puis à 3 points de la page : à moins de six pixels, le bord
        // de la page l'aimante, et il y reste d'un mouvement à l'autre — il ne s'en décroche
        // pas un mouvement sur deux.
        var zero = Zero(fenetre, "TopRuler");
        fenetre.MouseDown(new Point(zero.X + 300, zero.Y), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, -4, 300), RawInputModifiers.LeftMouseButton);
        Assert.Equal(0, Assert.Single(vm.Document.VerticalRulerGuides).Position, precision: 9);
        fenetre.MouseMove(SurLaPage(fenetre, -3, 300), RawInputModifiers.LeftMouseButton);
        Assert.Equal(0, Assert.Single(vm.Document.VerticalRulerGuides).Position, precision: 9);

        // Lâché là, hors de la page : il est retiré, bien que l'aimantation l'ait posé sur
        // le bord. C'est le pointeur qui dit où on le lâche.
        fenetre.MouseUp(SurLaPage(fenetre, -3, 300), MouseButton.Left);
        Rendu.Battre();
        Assert.Empty(vm.Document.VerticalRulerGuides);
        Assert.Equal("Repère retiré", vm.Status);

        // Et lâché à 3 points DANS la page, il reste, aimanté au bord.
        fenetre.MouseDown(new Point(zero.X + 300, zero.Y), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 3, 300), RawInputModifiers.LeftMouseButton);
        fenetre.MouseUp(SurLaPage(fenetre, 3, 300), MouseButton.Left);
        Rendu.Battre();
        Assert.Equal(0, Assert.Single(vm.Document.VerticalRulerGuides).Position, precision: 9);
    }

    [AvaloniaFact]
    public void Un_repere_traverse_toute_la_planche_sans_entrer_dans_la_page()
    {
        // Une page qui tient dans la zone visible, planche comprise tout autour : le trait
        // se lit des deux côtés de la page, et non du seul côté d'où il part.
        var document = Rendu.Feuille();
        document.PageWidth = 300;
        document.PageHeight = 400;
        var (fenetre, vm) = Ouvrir(document, reperes: true);
        var sans = Rendu.Encre(fenetre);

        document.VerticalRulerGuides.Add(new RulerGuide(250));
        document.HorizontalRulerGuides.Add(new RulerGuide(300));
        Rendu.Battre();

        // D'un bord à l'autre de la planche, page comprise, d'un pixel de cyan.
        var image = new Image(fenetre);
        var o = Origine(fenetre);
        var repere = Couleur("CouleurRepere");
        Pareil(repere, image[o.X + 250, o.Y - 20], "repère vertical, sur la planche au-dessus de la page");
        Pareil(repere, image[o.X + 250, o.Y + 200], "repère vertical, sur la page");
        Pareil(repere, image[o.X + 250, o.Y + 400 + 20], "repère vertical, sur la planche sous la page");
        Pareil(repere, image[o.X - 30, o.Y + 300], "repère horizontal, sur la planche à gauche de la page");
        Pareil(repere, image[o.X + 150, o.Y + 300], "repère horizontal, sur la page");
        Pareil(repere, image[o.X + 300 + 20, o.Y + 300], "repère horizontal, sur la planche à droite de la page");
        Assert.NotEqual(Hex(repere), Hex(image[o.X + 251, o.Y + 200]));
        Assert.NotEqual(Hex(repere), Hex(image[o.X + 150, o.Y + 301]));

        // Et rien dans PageRoot, que l'écran compare au PDF (règle 4 de la phase) : la
        // page rendue seule est la même, au pixel, avec et sans les deux repères.
        Assert.True(sans.Pixels.SequenceEqual(Rendu.Encre(fenetre).Pixels),
            "un repère libre se dessine dans PageRoot");
        Assert.DoesNotContain(Page(fenetre).GetVisualDescendants(), v => v.DataContext is RulerGuide);
        Assert.True(vm.ShowGuides);
    }

    [AvaloniaFact]
    public void Les_marges_sont_en_pointille_rose_dun_pixel()
    {
        var (fenetre, _) = Ouvrir(Rendu.Feuille(), reperes: true);
        var image = new Image(fenetre);
        var o = Origine(fenetre);
        var rose = Hex(Couleur("CouleurMargeDePage"));

        // La marge gauche du document par défaut est à 20 mm : la colonne la plus rose
        // autour d'elle est le trait, et il est seul — un pixel de large.
        var marge = Units.MmToPoints(20);
        int Roses(int x) => Enumerable.Range((int)(o.Y + 100), 400).Count(y => Hex(image[o.X + x, y]) == rose);
        var colonnes = Enumerable.Range((int)marge - 4, 9).ToList();
        var trait = colonnes.MaxBy(Roses);

        // En pointillé : du rose et du blanc, en alternance, sur 400 pixels.
        Assert.InRange(Roses(trait), 160, 320);
        Assert.All(colonnes.Where(x => Math.Abs(x - trait) > 0), x => Assert.True(Roses(x) < 20,
            $"la colonne {x} porte {Roses(x)} pixels roses : la marge fait plus d'un pixel"));
    }

    [AvaloniaFact]
    public void A_l_echelle_2_la_marge_reste_d_un_pixel_et_le_repere_tombe_sur_sa_position()
    {
        // Un pixel d'écran à tous les zooms : à l'échelle 1, un trait d'un point aussi. Ici,
        // un point fait deux pixels, et un trait d'un point en ferait deux colonnes.
        var document = Rendu.Feuille();
        document.VerticalRulerGuides.Add(new RulerGuide(150));
        var (fenetre, _) = OuvrirA(2, document, reperes: true);
        var image = new Image(fenetre);
        var rose = Hex(Couleur("CouleurMargeDePage"));

        var marge = DansLaFenetreA(fenetre, Units.MmToPoints(20), 0).X;
        var haut = DansLaFenetreA(fenetre, 0, 60).Y;
        int Roses(int x) => Enumerable.Range((int)haut, 300).Count(y => Hex(image[x, y]) == rose);
        var colonnes = Enumerable.Range((int)marge - 6, 13).ToList();
        var trait = colonnes.MaxBy(Roses);
        Assert.InRange(Roses(trait), 120, 240);
        Assert.All(colonnes.Where(x => x != trait), x => Assert.True(Roses(x) < 15,
            $"la colonne {x} porte {Roses(x)} pixels roses : la marge fait plus d'un pixel à l'échelle 2"));

        // Le repère à 150 points : à 300 pixels de l'origine de la page, et sur un pixel.
        var repere = Hex(Couleur("CouleurRepere"));
        var colonne = Math.Floor(DansLaFenetreA(fenetre, 150, 0).X + 1e-3);
        Assert.Equal(repere, Hex(image[colonne, haut + 20]));
        Assert.NotEqual(repere, Hex(image[colonne - 1, haut + 20]));
        Assert.NotEqual(repere, Hex(image[colonne + 1, haut + 20]));
    }

    /// <summary>
    /// Des zooms où la page ne tombe pas sur un pixel entier — 30 % (une échelle de
    /// 0,39999999999999997) et 41 %, où PageRoot est décalé d'un pixel dans ce qui la
    /// porte —, ou bien où la position lue tombe juste sous l'entier : 64 % et « 100 % »
    /// atteints par les pas du zoom. Le repère vertical et l'horizontal marquent le bord du
    /// bloc posé sur eux — la colonne, la rangée où ce bord commence —, et le zéro des
    /// règles est l'origine de PageRoot.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(0.3)]
    [InlineData(0.4096)]
    [InlineData(0.6400000000000003)]
    [InlineData(1.0000000000000007)]
    public void Hors_de_l_echelle_1_le_repere_tombe_sur_le_bord_du_bloc_qu_il_marque(double zoom)
    {
        var bloc = new ShapeFrame
        {
            Name = "Marqué", X = 150, Y = 150, Width = 100, Height = 100,
            Fill = new SolidFill("#FFCC0000"), Stroke = null,
        };
        var document = Rendu.Feuille(bloc);
        document.VerticalRulerGuides.Add(new RulerGuide(150));
        document.HorizontalRulerGuides.Add(new RulerGuide(150));
        var (fenetre, vm) = Ouvrir(document);
        vm.Zoom = zoom;
        Rendu.Battre();

        // Le zéro des deux règles, à l'origine de la page — non de ce qui la porte.
        var o = Origine(fenetre);
        Assert.Equal(o.X, Zero(fenetre, "TopRuler").X, precision: 6);
        Assert.Equal(o.Y, Zero(fenetre, "LeftRuler").Y, precision: 6);

        // Le bord du bloc, repères éteints : la première colonne et la première rangée où le
        // rouge commence, même pâle.
        var sans = new Image(fenetre);
        var milieu = DansLaFenetreA(fenetre, 200, 200);
        var avant = DansLaFenetreA(fenetre, 120, 120);
        var colonne = Enumerable.Range((int)avant.X, (int)(milieu.X - avant.X)).First(x => sans[x, milieu.Y].G < 250);
        var rangee = Enumerable.Range((int)avant.Y, (int)(milieu.Y - avant.Y)).First(y => sans[milieu.X, y].G < 250);

        vm.ShowGuides = true;
        Rendu.Battre();
        var avec = new Image(fenetre);
        var repere = Hex(Couleur("CouleurRepere"));
        Assert.True(repere == Hex(avec[colonne, milieu.Y]),
            $"à {zoom:P0}, le repère vertical n'est pas sur la colonne {colonne} où commence le bloc : " +
            $"{Hex(avec[colonne, milieu.Y])}, cyan en {Enumerable.Range(colonne - 3, 7).FirstOrDefault(x => Hex(avec[x, milieu.Y]) == repere)}");
        Assert.True(repere == Hex(avec[milieu.X, rangee]),
            $"à {zoom:P0}, le repère horizontal n'est pas sur la rangée {rangee} où commence le bloc : " +
            $"cyan en {Enumerable.Range(rangee - 3, 7).FirstOrDefault(y => Hex(avec[milieu.X, y]) == repere)}");
    }

    // ------------------------------------------------------------------ marques

    /// <summary>
    /// Un bloc sans encre sur une page grise : le blanc des poignées et l'accent du cadre
    /// s'y lisent sans se confondre avec le papier.
    /// </summary>
    private static (Window Fenetre, MainWindowViewModel Vm, ShapeFrame Bloc, Color Fond) BlocSurPageGrise(double rotation = 0)
    {
        var bloc = new ShapeFrame
        {
            Name = "Repère", X = 100, Y = 150, Width = 200, Height = 100, Rotation = rotation,
            Fill = null, Stroke = null,
        };
        var document = Rendu.Feuille(bloc);
        document.Background = new SolidFill("#FF808080");
        var (fenetre, vm) = Ouvrir(document);
        vm.SelectOnly(bloc);
        Rendu.Battre();
        return (fenetre, vm, bloc, Color.FromRgb(0x80, 0x80, 0x80));
    }

    [AvaloniaFact]
    public void Le_cadre_de_selection_passe_a_4_px_du_bloc_en_un_trait_et_demi_d_accent()
    {
        var (fenetre, _, _, fond) = BlocSurPageGrise();
        var image = new Image(fenetre);
        var o = Origine(fenetre);
        var accent = Couleur("CouleurAccent");

        // Bord gauche du bloc en x = 100 : le trait couvre [96 ; 97,5], son pixel 96 est
        // plein, le 97 à moitié, et rien au-delà de part et d'autre. Lu à 20 pt sous le
        // haut du bloc, entre deux poignées.
        var y = o.Y + 170;
        Pareil(fond, image[o.X + 95, y], "à 5 px du bloc");
        Pareil(accent, image[o.X + 96, y], "à 4 px du bloc");
        Assert.NotEqual(Hex(accent), Hex(image[o.X + 97, y]));
        Assert.NotEqual(Hex(fond), Hex(image[o.X + 97, y]));
        Pareil(fond, image[o.X + 98, y], "à 2 px du bloc");

        // Même chose à droite et en bas.
        Pareil(accent, image[o.X + 303, y], "à 4 px du bord droit");
        Pareil(fond, image[o.X + 304, y], "à 5 px du bord droit");
        Pareil(accent, image[o.X + 170, o.Y + 253], "à 4 px du bord bas");
    }

    [AvaloniaFact]
    public void Le_cadre_de_selection_tourne_avec_le_bloc()
    {
        // Un quart de tour autour du centre (200 ; 200) : le bloc couché se dresse sur
        // [150 ; 250] × [100 ; 300], et le cadre le suit à 4 px. Lu entre les poignées.
        var (fenetre, _, _, fond) = BlocSurPageGrise(rotation: 90);
        var image = new Image(fenetre);
        var o = Origine(fenetre);
        var accent = Couleur("CouleurAccent");

        Pareil(accent, image[o.X + 146, o.Y + 160], "à 4 px du bord gauche du bloc tourné");
        Pareil(accent, image[o.X + 253, o.Y + 160], "à 4 px du bord droit du bloc tourné");
        Pareil(accent, image[o.X + 220, o.Y + 96], "à 4 px du haut du bloc tourné");
        Pareil(fond, image[o.X + 96, o.Y + 170], "là où passait le cadre du bloc couché");
    }

    [AvaloniaFact]
    public void Les_poignees_sont_blanches_bordees_d_accent_et_la_rotation_est_un_disque_au_bout_de_sa_tige()
    {
        var (fenetre, _, _, fond) = BlocSurPageGrise();
        var image = new Image(fenetre);
        var o = Origine(fenetre);
        var accent = Couleur("CouleurAccent");
        var blanc = Couleur("CouleurBlanc");

        // La poignée nord-ouest, à cheval sur le cadre en (96,75 ; 146,75) : 7 px de blanc
        // dans un filet d'accent, dix de côté.
        Pareil(blanc, image[o.X + 97, o.Y + 147], "le cœur de la poignée");
        Pareil(accent, image[o.X + 92, o.Y + 147], "le filet gauche de la poignée");
        Pareil(fond, image[o.X + 90, o.Y + 147], "à côté de la poignée");

        // La poignée de rotation : 26 px au-dessus du bloc, dans son axe — un disque blanc
        // bordé d'accent, et une tige d'accent qui descend jusqu'au cadre.
        var (cx, cy) = (o.X + 200, o.Y + 150 - 26);
        Pareil(blanc, image[cx, cy], "le cœur du disque de rotation");
        Proche(accent, image[cx, cy - 6], 24, "le bord du disque de rotation");
        Pareil(fond, image[cx, cy - 8], "au-dessus du disque");
        var tige = image[cx, o.Y + 150 - 14];
        Assert.True(tige.B - tige.R > 60, $"pas de tige d'accent sous le disque : {Hex(tige)}");
    }

    // ------------------------------------------------------------------ bulle

    [AvaloniaFact]
    public void Pendant_un_redimensionnement_la_bulle_et_la_barre_d_etat_disent_la_meme_taille()
    {
        // En français, comme la maquette : la virgule vient de la culture courante (règle 8
        // de la phase), et les tests tournent sous LC_ALL=C, où virgule et point ne se
        // distinguent pas. Posée le temps du test, elle fait voir une bulle qui écrirait
        // dans une autre culture que la barre.
        SousLaCulture("fr-FR", () =>
        {
            var bloc = new ShapeFrame { Name = "Tiré", X = 100, Y = 100, Width = 120, Height = 80, Stroke = null };
            var (fenetre, vm) = Ouvrir(Rendu.Feuille(bloc));
            vm.SelectOnly(bloc);
            Rendu.Battre();

            var bulle = Bulle(fenetre);
            Assert.False(bulle.IsVisible, "la bulle paraît sans geste");

            // Le coin sud-est, pris : la bulle paraît, et déjà centrée sous le bloc — et non
            // décalée de sa demi-largeur, mesurée quand elle était encore cachée.
            var o = Origine(fenetre);
            fenetre.MouseDown(SurLaPage(fenetre, 220, 180), MouseButton.Left);
            Rendu.Battre();
            Assert.True(bulle.IsEffectivelyVisible, "pas de bulle à la prise de la poignée");
            Assert.Equal(o.X + 160, DansLaFenetre(fenetre, bulle).Center.X, precision: 0);

            // Tiré de 30 et de 20 points, et NON relâché : c'est pendant le geste que les
            // deux se lisent.
            fenetre.MouseMove(SurLaPage(fenetre, 250, 200), RawInputModifiers.LeftMouseButton);
            Rendu.Battre();

            Assert.Equal((150.0, 100.0), (bloc.Width, bloc.Height));
            Assert.True(bulle.IsEffectivelyVisible, "pas de bulle pendant le redimensionnement");

            var texte = Assert.IsType<TextBlock>(bulle.Child).Text!;
            var barre = Trouver<TextBlock>(fenetre, "TailleDuBloc").Text!;

            Assert.Equal($"L {Millimetres(150)} mm · H {Millimetres(100)} mm", texte);
            Assert.Equal("L 52,9 mm · H 35,3 mm", texte);
            Assert.Equal(Nombres(barre), Nombres(texte));

            // Sous le bloc, et sous ses poignées ; centrée sur lui.
            var boite = DansLaFenetre(fenetre, bulle);
            Assert.True(boite.Top > o.Y + 200 + 8, $"la bulle ({boite}) n'est pas sous le bloc");
            Assert.Equal(o.X + 175, boite.Center.X, precision: 0);

            // Un fond sombre, le texte blanc : la couleur du texte de l'interface.
            Assert.Equal(Couleur("CouleurTexte"), Assert.IsAssignableFrom<ISolidColorBrush>(bulle.Background).Color);

            fenetre.MouseUp(SurLaPage(fenetre, 250, 200), MouseButton.Left);
            Rendu.Battre();
            Assert.False(bulle.IsVisible, "la bulle reste après le geste");
        });
    }

    /// <summary>
    /// Les nombres d'un texte, séparateur décimal compris : « 52,9 » n'est pas « 52.9 ».
    /// </summary>
    private static string[] Nombres(string s) => Regex.Matches(s, @"\d+(?:[.,]\d+)?").Select(m => m.Value).ToArray();

    [AvaloniaFact]
    public void Sur_un_bloc_tourne_la_bulle_dit_la_taille_du_bloc_et_non_celle_de_son_emprise()
    {
        // Tourné de 30°, le bloc s'étend plus loin que son cadre : la barre d'état dit sa
        // largeur et sa hauteur, la bulle aussi — l'emprise ne sert qu'à la placer.
        var bloc = new ShapeFrame { Name = "Tourné", X = 150, Y = 150, Width = 120, Height = 80, Rotation = 30, Stroke = null };
        var (fenetre, vm) = Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        // La poignée sud-est, là où elle se dessine : sur le cadre, 3,25 px hors du coin,
        // tournée avec le bloc. Menée de 10 px sans être lâchée.
        var (px, py) = bloc.ToPage(bloc.Width + 3.25, bloc.Height + 3.25);
        fenetre.MouseDown(SurLaPage(fenetre, px, py), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, px + 10, py + 10), RawInputModifiers.LeftMouseButton);
        Rendu.Battre();

        Assert.NotEqual(120, bloc.Width);
        var bulle = Bulle(fenetre);
        Assert.True(bulle.IsEffectivelyVisible, "pas de bulle pendant le redimensionnement");
        var texte = Assert.IsType<TextBlock>(bulle.Child).Text!;
        var barre = Trouver<TextBlock>(fenetre, "TailleDuBloc").Text!;
        Assert.Equal($"L {Millimetres(bloc.Width)} mm · H {Millimetres(bloc.Height)} mm", texte);
        Assert.Equal(Nombres(barre), Nombres(texte));
        Assert.NotEqual($"L {Millimetres(bloc.Extent.Width)} mm · H {Millimetres(bloc.Extent.Height)} mm", texte);

        fenetre.MouseUp(SurLaPage(fenetre, px + 10, py + 10), MouseButton.Left);
    }

    [AvaloniaFact]
    public void Pendant_un_trace_la_bulle_dit_la_taille_du_cadre()
    {
        var (fenetre, vm) = Ouvrir(Rendu.Feuille());

        fenetre.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.None);
        Rendu.Battre();
        Assert.True(vm.IsDrawing);

        fenetre.MouseDown(SurLaPage(fenetre, 300, 300), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 380, 360), RawInputModifiers.LeftMouseButton);
        Rendu.Battre();

        var bulle = Bulle(fenetre);
        Assert.True(bulle.IsEffectivelyVisible, "pas de bulle pendant le tracé");
        Assert.Equal($"L {Millimetres(80)} mm · H {Millimetres(60)} mm", Assert.IsType<TextBlock>(bulle.Child).Text);

        // Centrée sous le cadre dès le premier mouvement, sur la largeur de son nouveau
        // texte et non sur celle de l'ancien, « L 0,0 mm · H 0,0 mm ».
        Assert.Equal(Origine(fenetre).X + 340, DansLaFenetre(fenetre, bulle).Center.X, precision: 0);

        // Le cadre tracé se projette aussi sur les règles.
        Assert.Equal((300.0, 380.0), (Trouver<Ruler>(fenetre, "TopRuler").DebutProjete, Trouver<Ruler>(fenetre, "TopRuler").FinProjetee));

        fenetre.MouseUp(SurLaPage(fenetre, 380, 360), MouseButton.Left);
        Rendu.Battre();
        Assert.False(bulle.IsVisible);
    }

    // ------------------------------------------------------------------ la planche reçoit le pointeur

    [AvaloniaFact]
    public void Une_poignee_saisie_en_son_centre_ne_fait_pas_sauter_le_bord()
    {
        // La poignée est sur le cadre, 3,25 px hors du coin du bloc. Saisie là où elle se
        // dessine et menée de 10 px, elle allonge le bloc de 10 points — et non de 13,25,
        // ce que donnerait un bord qui rejoint le pointeur.
        var bloc = new ShapeFrame { Name = "Tiré", X = 100, Y = 100, Width = 120, Height = 80, Stroke = null };
        var (fenetre, vm) = Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var prise = SurLaPage(fenetre, 223.25, 183.25);
        fenetre.MouseDown(prise, MouseButton.Left);
        fenetre.MouseMove(new Point(prise.X + 10, prise.Y + 10), RawInputModifiers.LeftMouseButton);
        fenetre.MouseUp(new Point(prise.X + 10, prise.Y + 10), MouseButton.Left);
        Rendu.Battre();

        Assert.Equal(130, bloc.Width, precision: 6);
        Assert.Equal(90, bloc.Height, precision: 6);
        Assert.Equal((100.0, 100.0), (bloc.X, bloc.Y));
    }

    /// <summary>Le centre de la poignée de rotation, relu dans l'Overlay, en points de page.</summary>
    private static Point CentreDeLaRotation(Window fenetre)
    {
        var disque = Trouver<Canvas>(fenetre, "Overlay").Children.OfType<Ellipse>().Single(e => e.IsVisible);
        return new Point(Canvas.GetLeft(disque) + disque.Width / 2, Canvas.GetTop(disque) + disque.Height / 2);
    }

    [AvaloniaFact]
    public void Une_poignee_hors_de_la_page_se_saisit_sur_la_planche()
    {
        // Les trois cas que A1 avait laissés : la couche de saisie s'arrêtait au bord de la
        // page, et une poignée posée au-delà ne se prenait pas — ou tombait sur la règle,
        // qui posait un repère. Elle couvre désormais la planche entière. Une petite page,
        // qui tient tout entière dans la zone visible, planche comprise autour d'elle.
        Document Feuille(Frame bloc)
        {
            var document = Rendu.Feuille(bloc);
            document.PageWidth = 300;
            document.PageHeight = 400;
            return document;
        }

        // Un quart de tour dans le sens des aiguilles, sous Maj : la poignée est menée au
        // point qui fait un angle droit avec elle autour du centre du bloc.
        void Tourner(Window fenetre, Frame bloc)
        {
            var prise = CentreDeLaRotation(fenetre);
            var centre = new Point(bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2);
            var vise = new Point(centre.X - 2 * (prise.Y - centre.Y), centre.Y + 2 * (prise.X - centre.X));
            fenetre.MouseDown(SurLaPage(fenetre, prise.X, prise.Y), MouseButton.Left, RawInputModifiers.Shift);
            fenetre.MouseMove(SurLaPage(fenetre, vise.X, vise.Y), RawInputModifiers.LeftMouseButton | RawInputModifiers.Shift);
            fenetre.MouseUp(SurLaPage(fenetre, vise.X, vise.Y), MouseButton.Left, RawInputModifiers.Shift);
            Rendu.Battre();
        }

        // Un bloc collé au haut, règles masquées : sa poignée de rotation est sur le gris.
        var haut = new ShapeFrame { Name = "Haut", X = 50, Y = 5, Width = 200, Height = 60, Stroke = null };
        var (fenetre, vm) = Ouvrir(Feuille(haut));
        vm.ShowRulers = false;
        vm.SelectOnly(haut);
        Rendu.Battre();
        Assert.True(CentreDeLaRotation(fenetre).Y < 0, "la poignée devrait être au-dessus de la page");
        Tourner(fenetre, haut);
        Assert.Equal(90, haut.Rotation);

        // Un bloc retourné, collé au bas : sa poignée est sous la page.
        var bas = new ShapeFrame { Name = "Bas", X = 50, Y = 335, Width = 200, Height = 60, Rotation = 180, Stroke = null };
        (fenetre, vm) = Ouvrir(Feuille(bas));
        vm.SelectOnly(bas);
        Rendu.Battre();
        Assert.True(CentreDeLaRotation(fenetre).Y > 400, "la poignée devrait être sous la page");
        Tourner(fenetre, bas);
        Assert.Equal(270, bas.Rotation);

        // Un bloc posé à Y = 0 : sa poignée d'angle, sur le cadre, déborde de la page. Elle
        // redimensionne, et aucun repère ne se pose.
        var colle = new ShapeFrame { Name = "Collé", X = 50, Y = 0, Width = 100, Height = 60, Stroke = null };
        (fenetre, vm) = Ouvrir(Feuille(colle));
        vm.SelectOnly(colle);
        Rendu.Battre();
        fenetre.MouseDown(SurLaPage(fenetre, 46.75, -3.25), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 66.75, 16.75), RawInputModifiers.LeftMouseButton);
        fenetre.MouseUp(SurLaPage(fenetre, 66.75, 16.75), MouseButton.Left);
        Rendu.Battre();
        Assert.Equal((70.0, 20.0, 80.0, 40.0), (colle.X, colle.Y, colle.Width, colle.Height));
        Assert.Empty(vm.Document.VerticalRulerGuides);
        Assert.Empty(vm.Document.HorizontalRulerGuides);
    }

    [AvaloniaTheory]
    [InlineData(PhysicalKey.T)]
    [InlineData(PhysicalKey.R)]
    public void Un_outil_arme_ne_pose_rien_sur_la_planche_hors_de_la_page_et_reste_arme(PhysicalKey touche)
    {
        // La couche de saisie couvre la planche pour les poignées et les repères ; un outil
        // armé, lui, ne trace que sur la page. Un bloc posé sur le gris tomberait hors de
        // PageRoot, qui le rogne : invisible, et la frappe partirait dans un texte caché.
        var (fenetre, vm) = Ouvrir(Rendu.Feuille());
        fenetre.KeyPressQwerty(touche, RawInputModifiers.None);
        Rendu.Battre();
        Assert.True(vm.IsDrawing);
        var outil = vm.ActiveTool;

        foreach (var (x, y) in new[] { (-25.0, 300.0), (300.0, -20.0) })
        {
            fenetre.MouseDown(SurLaPage(fenetre, x, y), MouseButton.Left);
            fenetre.MouseUp(SurLaPage(fenetre, x, y), MouseButton.Left);
            Rendu.Battre();

            Assert.Empty(vm.CurrentPage!.Frames);
            Assert.Equal(outil, vm.ActiveTool);
        }

        // Sur la page, le même clic pose bien le bloc.
        fenetre.MouseDown(SurLaPage(fenetre, 100, 100), MouseButton.Left);
        fenetre.MouseUp(SurLaPage(fenetre, 100, 100), MouseButton.Left);
        Rendu.Battre();
        Assert.Single(vm.CurrentPage!.Frames);
    }

    [AvaloniaFact]
    public void Un_clic_sur_le_gris_ne_prend_pas_la_part_rognee_d_un_bloc_qui_deborde()
    {
        // Un fond perdu qui déborde à gauche de la page : sa part sur le gris est rognée par
        // PageRoot, rien ne s'y voit. Un clic là libère la sélection, comme ailleurs sur le
        // gris, et ne prend ni ne déplace le bloc.
        var fond = new ShapeFrame { Name = "Fond perdu", X = -60, Y = 100, Width = 160, Height = 80 };
        var pris = new ShapeFrame { Name = "Pris", X = 300, Y = 300, Width = 80, Height = 60 };
        var (fenetre, vm) = Ouvrir(Rendu.Feuille(fond, pris));
        vm.SelectOnly(pris);
        Rendu.Battre();

        fenetre.MouseDown(SurLaPage(fenetre, -30, 140), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 10, 140), RawInputModifiers.LeftMouseButton);
        fenetre.MouseUp(SurLaPage(fenetre, 10, 140), MouseButton.Left);
        Rendu.Battre();

        Assert.Empty(vm.Selection);
        Assert.Equal((-60.0, 100.0), (fond.X, fond.Y));

        // Sur la page, la part visible du même bloc se prend toujours.
        fenetre.MouseDown(SurLaPage(fenetre, 30, 140), MouseButton.Left);
        fenetre.MouseUp(SurLaPage(fenetre, 30, 140), MouseButton.Left);
        Rendu.Battre();
        Assert.Equal([fond], vm.Selection);
    }

    [AvaloniaFact]
    public void Un_clic_sur_la_planche_hors_de_la_page_libere_la_selection()
    {
        var bloc = new ShapeFrame { Name = "Pris", X = 100, Y = 100, Width = 80, Height = 60 };
        var (fenetre, vm) = Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var dehors = SurLaPage(fenetre, -25, 300);
        fenetre.MouseDown(dehors, MouseButton.Left);
        fenetre.MouseUp(dehors, MouseButton.Left);
        Rendu.Battre();

        Assert.Empty(vm.Selection);
        Assert.Equal((100.0, 100.0), (bloc.X, bloc.Y));
    }
}
