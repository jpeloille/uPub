using System;
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
/// point de page y vaut un pixel —, et les couleurs attendues sont lues dans
/// l'apparence, non recopiées : que chaque clé porte la couleur de la maquette, c'est la
/// recette de I1 qui le vérifie contre la maquette elle-même. Ici, on vérifie que la
/// planche les pose là où la maquette les pose.
///
/// La recette du poste — <c>Scripts/recettes/I4-planche.sh</c> — joue ces tests par leur
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

    // ------------------------------------------------------------------ projection

    [AvaloniaFact]
    public void La_projection_dun_bloc_tourne_couvre_exactement_son_emprise_sur_chaque_regle()
    {
        // Tourné de 30° : son emprise déborde son cadre des deux côtés. Une projection qui
        // lirait X et la largeur tomberait à côté, de part et d'autre.
        var bloc = new ShapeFrame
        {
            Name = "Tourné", X = 150, Y = 220, Width = 200, Height = 80, Rotation = 30,
            Fill = new SolidFill("#FFCC0000"), Stroke = null,
        };
        var (fenetre, vm) = Ouvrir(Rendu.Feuille(bloc));
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
                ? (zero.X + emprise.Left, zero.X + emprise.Right)
                : (zero.Y + emprise.Top, zero.Y + emprise.Bottom);
            Assert.InRange(changes.Min(), debut - 1, debut + 1);
            Assert.InRange(changes.Max() + 1, fin - 1, fin + 1);
        }

        // La teinte : l'accent à 14 % sur le gris de la règle, comme la maquette l'écrit.
        var face = Couleur("CouleurFondFenetre");
        var accent = Couleur("CouleurAccent");
        var regleDuHaut = DansLaFenetre(fenetre, Trouver<Ruler>(fenetre, "TopRuler"));
        var milieu = Zero(fenetre, "TopRuler").X + (emprise.Left + emprise.Right) / 2;
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
    public void Un_repere_traverse_toute_la_planche_sans_entrer_dans_la_page()
    {
        var document = Rendu.Feuille();
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
        Pareil(repere, image[o.X + 250, o.Y + 500], "repère vertical, sur la page");
        Pareil(repere, image[o.X - 30, o.Y + 300], "repère horizontal, sur la planche à gauche de la page");
        Pareil(repere, image[o.X + 400, o.Y + 300], "repère horizontal, sur la page");
        Assert.NotEqual(Hex(repere), Hex(image[o.X + 251, o.Y + 500]));
        Assert.NotEqual(Hex(repere), Hex(image[o.X + 400, o.Y + 301]));

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
        var bloc = new ShapeFrame { Name = "Tiré", X = 100, Y = 100, Width = 120, Height = 80, Stroke = null };
        var (fenetre, vm) = Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var bulle = Bulle(fenetre);
        Assert.False(bulle.IsVisible, "la bulle paraît sans geste");

        // Le coin sud-est, tiré de 30 et de 20 points, et NON relâché : c'est pendant le
        // geste que les deux se lisent.
        fenetre.MouseDown(SurLaPage(fenetre, 220, 180), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 250, 200), RawInputModifiers.LeftMouseButton);
        Rendu.Battre();

        Assert.Equal((150.0, 100.0), (bloc.Width, bloc.Height));
        Assert.True(bulle.IsEffectivelyVisible, "pas de bulle pendant le redimensionnement");

        var texte = Assert.IsType<TextBlock>(bulle.Child).Text!;
        var barre = Trouver<TextBlock>(fenetre, "TailleDuBloc").Text!;
        string[] Nombres(string s) => Regex.Matches(s, @"\d+(?:[.,]\d+)?").Select(m => m.Value).ToArray();

        Assert.Equal($"L {Millimetres(150)} mm · H {Millimetres(100)} mm", texte);
        Assert.Equal(Nombres(barre), Nombres(texte));

        // Sous le bloc, et sous ses poignées ; centrée sur lui.
        var o = Origine(fenetre);
        var boite = DansLaFenetre(fenetre, bulle);
        Assert.True(boite.Top > o.Y + 200 + 8, $"la bulle ({boite}) n'est pas sous le bloc");
        Assert.Equal(o.X + 175, boite.Center.X, precision: 0);

        // Un fond sombre, le texte blanc : la couleur du texte de l'interface.
        Assert.Equal(Couleur("CouleurTexte"), Assert.IsAssignableFrom<ISolidColorBrush>(bulle.Background).Color);

        fenetre.MouseUp(SurLaPage(fenetre, 250, 200), MouseButton.Left);
        Rendu.Battre();
        Assert.False(bulle.IsVisible, "la bulle reste après le geste");
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
