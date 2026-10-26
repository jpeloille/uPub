using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Naviguer dans la planche, et garder les proportions — poste I6 : la main, l'outil ou
/// <c>Espace</c> tenu, qui fait défiler la planche sans toucher à rien ; la loupe et
/// <c>Ctrl</c>+molette, qui zooment autour du point visé ; « Page entière », qui tient la
/// page dans la zone visible ; et la case « Conserver les proportions », que le coin tiré
/// et la saisie de L ou de H suivent.
///
/// Tout se joue au pointeur et au clavier sur la vraie fenêtre, comme l'utilisateur le
/// ferait, et se mesure dans le repère de la fenêtre. La recette du poste —
/// <c>Scripts/recettes/I6-navigation.sh</c> — joue ces tests par leur trait.
/// </summary>
[Trait("poste", "I6")]
public class NavigationTests
{
    // ------------------------------------------------------------------ outillage

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static ScrollViewer Defilement(Window fenetre) => Trouver<ScrollViewer>(fenetre, "Scroller");

    /// <summary>Le milieu de la zone qui défile, dans la fenêtre.</summary>
    private static Point MilieuDeLaZone(Window fenetre)
    {
        var zone = Defilement(fenetre);
        return zone.TranslatePoint(new Point(zone.Bounds.Width / 2, zone.Bounds.Height / 2), fenetre)
               ?? throw new InvalidOperationException();
    }

    private static TextFrame Texte(double x, double y, double largeur, double hauteur) =>
        new() { Name = "Texte", X = x, Y = y, Width = largeur, Height = hauteur, Text = "Texte" };

    // ------------------------------------------------------------------ la main

    [AvaloniaFact]
    public void Espace_tenu_et_un_glisse_de_100_px_font_defiler_la_planche_de_100_px_sans_rien_toucher()
    {
        var bloc = Texte(60, 60, 470, 700);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            // Au double de la taille réelle, la page déborde de la zone dans les deux sens.
            vm.Zoom = 2 / Units.PointToDip;
            Rendu.Battre();
            var zone = Defilement(fenetre);
            Assert.True(zone.Extent.Width > zone.Viewport.Width + 200 && zone.Extent.Height > zone.Viewport.Height + 200,
                "la page ne déborde pas assez de la zone pour la mesure");
            zone.Offset = new Vector(150, 150);
            Rendu.Battre();

            var avant = DocumentSerializer.ToJson(vm.Document);
            var depart = MilieuDeLaZone(fenetre);
            Assert.Same(bloc, vm.CurrentPage!.Frames.Last(f => f.HitTest(
                (depart.X - Rendu.SurLaPage(fenetre, 0, 0).X) / vm.RenderScale,
                (depart.Y - Rendu.SurLaPage(fenetre, 0, 0).Y) / vm.RenderScale)));

            // Le glissé part du bloc : sans la main, il le déplacerait.
            fenetre.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Rendu.Battre();
            Rendu.Glisser(fenetre, depart, depart + new Vector(-100, -60));
            fenetre.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Rendu.Battre();

            Assert.Equal(250, zone.Offset.X, precision: 6);
            Assert.Equal(210, zone.Offset.Y, precision: 6);
            Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
            Assert.False(vm.CanUndo, "la main a empilé un pas d'annulation");
            Assert.False(vm.IsDirty, "la main a rendu le document « modifié »");
            Assert.Empty(vm.Selection);

            // Espace relâché, un glissé reprend le bloc. Ailleurs qu'au point de départ :
            // deux pressions au même point, sans que l'horloge du banc avance, font un
            // double-clic, qui ouvrirait la saisie du bloc.
            var prise = MilieuDeLaZone(fenetre) + new Vector(30, 30);
            Rendu.Glisser(fenetre, prise, prise + new Vector(-20, 0));
            Assert.Equal(250, zone.Offset.X, precision: 6);
            Assert.Same(bloc, vm.SingleSelected);
            Assert.Equal(60 - 20 / vm.RenderScale, bloc.X, precision: 6);
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void L_outil_Main_du_rail_fait_defiler_la_planche_main_ouverte_puis_fermee()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte(60, 60, 400, 600)));
        try
        {
            vm.Zoom = 2 / Units.PointToDip;
            Rendu.Battre();
            var zone = Defilement(fenetre);
            var couche = Trouver<Border>(fenetre, "InputLayer");
            var auRepos = couche.Cursor;

            Rendu.Cliquer(Trouver<Avalonia.Controls.Primitives.ToggleButton>(fenetre, "OutilMain"));
            Assert.Equal(EditorTool.Main, vm.ActiveTool);
            Assert.False(vm.IsDrawing, "la main passe pour un outil de tracé");
            var ouverte = couche.Cursor;
            Assert.NotNull(ouverte);
            Assert.NotSame(auRepos, ouverte);

            // Pendant le glissé, la main se ferme ; lâchée, elle se rouvre.
            var depart = MilieuDeLaZone(fenetre);
            fenetre.MouseDown(depart, MouseButton.Left);
            fenetre.MouseMove(depart + new Vector(0, -80));
            Rendu.Battre();
            var fermee = couche.Cursor;
            Assert.NotSame(ouverte, fermee);
            fenetre.MouseUp(depart + new Vector(0, -80), MouseButton.Left);
            Rendu.Battre();
            Assert.Same(ouverte, couche.Cursor);

            Assert.Equal(80, zone.Offset.Y, precision: 6);
            Assert.False(vm.CanUndo);
            Assert.Empty(vm.Selection);

            // Un double-clic sous la main n'ouvre pas la saisie du bloc.
            vm.SelectOnly(vm.CurrentPage!.Frames[0]);
            Rendu.Battre();
            var point = MilieuDeLaZone(fenetre);
            Rendu.DoubleCliquer(fenetre, point);
            var planche = Trouver<PageCanvas>(fenetre, "PageView");
            Assert.False(planche.IsEditingText, "la main a ouvert la saisie");

            // Échap la désarme, comme un outil de tracé, sans libérer la sélection ; et le
            // même double-clic, alors, ouvre bien la saisie.
            fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Rendu.Battre();
            Assert.Equal(EditorTool.Select, vm.ActiveTool);
            Assert.NotNull(vm.SingleSelected);
            Rendu.DoubleCliquer(fenetre, point);
            Assert.True(planche.IsEditingText, "le double-clic de contrôle n'a pas ouvert la saisie");
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Espace_ne_tient_pas_la_main_pendant_une_saisie_ni_sur_un_bouton()
    {
        var bloc = Texte(60, 60, 200, 80);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            var planche = Trouver<PageCanvas>(fenetre, "PageView");
            vm.SelectOnly(bloc);
            Rendu.Battre();

            // Dans le champ du nom, l'espace s'écrit.
            var nom = Trouver<TextBox>(fenetre, "ChampNom");
            nom.Focus();
            Rendu.Battre();
            fenetre.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Rendu.Battre();
            Assert.False(planche.MainTenue, "Espace tient la main pendant une saisie");
            fenetre.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            fenetre.FocusManager!.ClearFocus();
            Rendu.Battre();

            // Sur un bouton qui a le focus, Espace l'actionne et ne tient pas la main.
            var bouton = Trouver<Button>(fenetre, "BoutonPageEntiere");
            bouton.Focus();
            Rendu.Battre();
            fenetre.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Rendu.Battre();
            Assert.False(planche.MainTenue, "Espace tient la main alors qu'un bouton a le focus");
            fenetre.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Rendu.Battre();

            // La planche ayant le focus, Espace la tient, et le relâchement la lâche.
            planche.Focus();
            Rendu.Battre();
            fenetre.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Rendu.Battre();
            Assert.True(planche.MainTenue);
            fenetre.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Rendu.Battre();
            Assert.False(planche.MainTenue);
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ la loupe

    /// <summary>
    /// Le point de page visé est-il toujours sous le pointeur ? À 1 px près, comme le dit
    /// la recette du plan.
    /// </summary>
    private static void SousLePointeur(Window fenetre, MainWindowViewModel vm, Point pointeur, Point vise, string quoi)
    {
        var ici = Rendu.SurLaPage(fenetre, vise.X, vise.Y);
        Assert.True(Math.Abs(ici.X - pointeur.X) <= 1 && Math.Abs(ici.Y - pointeur.Y) <= 1,
            $"{quoi} : le point visé est passé de {pointeur} à {ici}");
    }

    [AvaloniaFact]
    public void Un_clic_de_la_loupe_zoome_en_gardant_sous_le_pointeur_le_point_vise()
    {
        var bloc = Texte(200, 300, 200, 150);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            // Une page qui déborde de la zone dans les deux sens : plus petite, elle y serait
            // centrée, et ne défilerait pas pour ramener le point sous le pointeur.
            vm.Zoom = 1.5 / Units.PointToDip;
            Rendu.Battre();
            var avant = DocumentSerializer.ToJson(vm.Document);
            Trouver<PageCanvas>(fenetre, "PageView").Focus();
            fenetre.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.None);
            Rendu.Battre();
            Assert.Equal(EditorTool.Zoom, vm.ActiveTool);

            // Sur le bloc : la loupe ne le prend pas.
            var pointeur = Rendu.SurLaPage(fenetre, 300, 400);
            var vise = Rendu.SousLaFenetre(fenetre, pointeur);
            var zoom = vm.Zoom;
            fenetre.MouseDown(pointeur, MouseButton.Left);
            fenetre.MouseUp(pointeur, MouseButton.Left);
            Rendu.Battre();
            Assert.Equal(zoom * MainWindowViewModel.PasDeLaLoupe, vm.Zoom, precision: 9);
            SousLePointeur(fenetre, vm, pointeur, vise, "clic");
            Assert.Empty(vm.Selection);

            // Un second clic, ailleurs dans la zone : la page déborde, le point suit encore.
            pointeur = MilieuDeLaZone(fenetre) + new Vector(120, 90);
            vise = Rendu.SousLaFenetre(fenetre, pointeur);
            fenetre.MouseDown(pointeur, MouseButton.Left);
            fenetre.MouseUp(pointeur, MouseButton.Left);
            Rendu.Battre();
            Assert.Equal(zoom * MainWindowViewModel.PasDeLaLoupe * MainWindowViewModel.PasDeLaLoupe, vm.Zoom, precision: 9);
            SousLePointeur(fenetre, vm, pointeur, vise, "second clic");

            // Alt + clic zoome en arrière, autour du point visé lui aussi.
            pointeur = MilieuDeLaZone(fenetre) + new Vector(-60, -40);
            vise = Rendu.SousLaFenetre(fenetre, pointeur);
            fenetre.MouseDown(pointeur, MouseButton.Left, RawInputModifiers.Alt);
            fenetre.MouseUp(pointeur, MouseButton.Left, RawInputModifiers.Alt);
            Rendu.Battre();
            Assert.Equal(zoom * MainWindowViewModel.PasDeLaLoupe, vm.Zoom, precision: 9);
            SousLePointeur(fenetre, vm, pointeur, vise, "Alt + clic");

            Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
            Assert.False(vm.CanUndo, "la loupe a empilé un pas d'annulation");
            Assert.False(vm.IsDirty, "la loupe a rendu le document « modifié »");
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_et_la_molette_zooment_autour_du_point_vise_meme_quand_la_page_deborde()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte(60, 60, 200, 80)));
        try
        {
            // La page déborde : sans la prise au passage descendant, la molette ferait
            // défiler la zone au lieu de zoomer.
            vm.Zoom = 1.5 / Units.PointToDip;
            Rendu.Battre();
            var zone = Defilement(fenetre);
            zone.Offset = new Vector(100, 200);
            Rendu.Battre();

            var pointeur = MilieuDeLaZone(fenetre) + new Vector(80, -50);
            var vise = Rendu.SousLaFenetre(fenetre, pointeur);
            var zoom = vm.Zoom;
            fenetre.MouseWheel(pointeur, new Vector(0, 1), RawInputModifiers.Control);
            Rendu.Battre();
            Assert.True(vm.Zoom > zoom * 1.05, $"Ctrl + molette n'a pas zoomé en avant ({zoom} → {vm.Zoom})");
            SousLePointeur(fenetre, vm, pointeur, vise, "molette en avant");

            zoom = vm.Zoom;
            fenetre.MouseWheel(pointeur, new Vector(0, -1), RawInputModifiers.Control);
            Rendu.Battre();
            Assert.True(vm.Zoom < zoom / 1.05, $"Ctrl + molette n'a pas zoomé en arrière ({zoom} → {vm.Zoom})");
            SousLePointeur(fenetre, vm, pointeur, vise, "molette en arrière");

            // Sans Ctrl, la molette fait défiler, et ne zoome pas.
            zoom = vm.Zoom;
            var defilement = zone.Offset;
            fenetre.MouseWheel(pointeur, new Vector(0, -1));
            Rendu.Battre();
            Assert.Equal(zoom, vm.Zoom);
            Assert.NotEqual(defilement, zone.Offset);
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ page entière

    [AvaloniaFact]
    public void Page_entiere_tient_un_A4_et_un_paysage_dans_la_zone_un_cote_touchant_a_la_marge_pres()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte(60, 60, 200, 80)));
        try
        {
            var bouton = Trouver<Button>(fenetre, "BoutonPageEntiere");
            Assert.True(bouton.IsEffectivelyEnabled, "« Page entière » reste grisé : personne ne l'écoute");
            var zone = Defilement(fenetre);
            var page = Trouver<Panel>(fenetre, "PageRoot");
            var marge = Trouver<Border>(fenetre, "Feuille").Margin;

            void Tient(string quoi)
            {
                var coin = page.TranslatePoint(default, zone) ?? throw new InvalidOperationException();
                var taille = new Size(vm.Document.PageWidth * vm.RenderScale, vm.Document.PageHeight * vm.RenderScale);
                var libreX = zone.Viewport.Width - marge.Left - marge.Right;
                var libreY = zone.Viewport.Height - marge.Top - marge.Bottom;

                Assert.True(taille.Width <= libreX + 0.5 && taille.Height <= libreY + 0.5,
                    $"{quoi} : la page de {taille} ne tient pas dans {libreX} × {libreY}");
                Assert.True(Math.Abs(taille.Width - libreX) <= 1 || Math.Abs(taille.Height - libreY) <= 1,
                    $"{quoi} : aucun côté de la page de {taille} ne touche la zone de {libreX} × {libreY}");
                Assert.True(zone.Extent.Width <= zone.Viewport.Width && zone.Extent.Height <= zone.Viewport.Height,
                    $"{quoi} : la zone défile encore ({zone.Extent} pour {zone.Viewport})");
                Assert.True(coin.X >= marge.Left - 1 && coin.Y >= marge.Top - 1,
                    $"{quoi} : la page commence en {coin}, dans la marge de la planche");
            }

            // Un A4 : à l'échelle 1, il déborde en hauteur.
            Assert.True(zone.Extent.Height > zone.Viewport.Height);
            Rendu.Cliquer(bouton);
            Tient("A4 portrait");
            Assert.True(vm.Zoom < 1 / Units.PointToDip);

            // Le même A4 en paysage : c'est alors la largeur qui borne, ou la hauteur — la
            // zone est plus large que haute, la page aussi.
            vm.IsLandscape = true;
            Rendu.Battre();
            var avant = DocumentSerializer.ToJson(vm.Document);
            var pas = vm.CanUndo;
            Rendu.Cliquer(bouton);
            Tient("A4 paysage");

            // Ni l'un ni l'autre ne touche au document.
            Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
            Assert.Equal(pas, vm.CanUndo);

            // Une page minuscule : le zoom s'arrête à sa borne, et la page ne remplit pas.
            vm.Document.PageWidth = Units.MmToPoints(10);
            vm.Document.PageHeight = Units.MmToPoints(10);
            Rendu.Battre();
            // Au neuvième chiffre : le curseur du zoom de la barre d'état, lié dans les deux
            // sens en échelle logarithmique, rend 7,999999999999998 pour 8.
            Rendu.Cliquer(bouton);
            Assert.Equal(MainWindowViewModel.ZoomMaximum, vm.Zoom, precision: 9);
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Aucun_zoom_d_office_ni_a_l_ouverture_ni_quand_la_fenetre_change_de_taille()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte(60, 60, 200, 80)));
        try
        {
            Assert.Equal(1 / Units.PointToDip, vm.Zoom);
            fenetre.Width = 900;
            fenetre.Height = 600;
            Rendu.Battre();
            Assert.Equal(1 / Units.PointToDip, vm.Zoom);

            vm.Document = Rendu.Feuille(Texte(10, 10, 50, 50));
            vm.CurrentPage = vm.Document.Pages[0];
            Rendu.Battre();
            Assert.Equal(1 / Units.PointToDip, vm.Zoom);
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ les proportions

    /// <summary>Tire une poignée d'un bloc droit, de <paramref name="chemin"/> pixels (voir <see cref="Rendu.Poignee"/>).</summary>
    private static void TirerLaPoignee(Window fenetre, Frame bloc,
                                       double fx, double fy, Vector chemin, RawInputModifiers modificateurs = RawInputModifiers.None)
    {
        var prise = Rendu.Poignee(fenetre, bloc, fx, fy);
        Rendu.Glisser(fenetre, prise, prise + chemin, modificateurs);
    }

    [AvaloniaFact]
    public void Case_cochee_un_coin_tire_de_20_mm_garde_le_rapport_en_un_pas_et_la_case_ne_modifie_rien()
    {
        var bloc = Texte(100, 100, 240, 120);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            vm.SelectOnly(bloc);
            Rendu.Battre();

            // La case, cochée au pointeur : un état de la session, ni pas ni « modifié ».
            Rendu.Cliquer(Trouver<CheckBox>(fenetre, "ConserverLesProportions"));
            Assert.True(vm.ConserverLesProportions);
            Assert.False(vm.IsDirty, "la case a rendu le document « modifié »");
            Assert.False(vm.CanUndo, "la case a empilé un pas d'annulation");

            var avant = DocumentSerializer.ToJson(vm.Document);
            var rapport = bloc.Width / bloc.Height;
            var vingt = Units.MmToPoints(20) * vm.RenderScale;
            TirerLaPoignee(fenetre, bloc, 1, 1, new Vector(vingt, vingt / 3));
            Assert.True(bloc.Width > 240, "le coin n'a pas agrandi le bloc");
            Assert.Equal(rapport, bloc.Width / bloc.Height, precision: 2);
            Assert.True(Math.Abs(rapport - bloc.Width / bloc.Height) <= 0.01);
            Rendu.UnSeulPas(vm, avant, "coin tiré, case cochée");

            // Le coin d'en haut à gauche aussi, en rétrécissant.
            bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
            TirerLaPoignee(fenetre, bloc, 0, 0, new Vector(vingt, vingt / 4));
            Assert.True(Math.Abs(rapport - bloc.Width / bloc.Height) <= 0.01, $"rapport {bloc.Width / bloc.Height} au lieu de {rapport}");
            Rendu.UnSeulPas(vm, avant, "coin haut gauche, case cochée");

            // Une poignée de côté ne garde pas le rapport, case cochée ou non.
            bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
            TirerLaPoignee(fenetre, bloc, 1, 0.5, new Vector(vingt, 0));
            Assert.Equal(120, bloc.Height, precision: 9);
            Assert.Equal(240 + vingt / vm.RenderScale, bloc.Width, precision: 6);
            Rendu.UnSeulPas(vm, avant, "côté tiré, case cochée");

            // Maj pendant le geste fait l'inverse de la case : cochée, le coin est libre.
            bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
            TirerLaPoignee(fenetre, bloc, 1, 1, new Vector(vingt, 0), RawInputModifiers.Shift);
            Assert.Equal(120, bloc.Height, precision: 9);
            Assert.Equal(240 + vingt / vm.RenderScale, bloc.Width, precision: 6);
            Rendu.UnSeulPas(vm, avant, "coin tiré sous Maj, case cochée");

            // Décochée, le coin est libre, et Maj garde le rapport, comme avant le poste.
            Rendu.Cliquer(Trouver<CheckBox>(fenetre, "ConserverLesProportions"));
            Assert.False(vm.ConserverLesProportions);
            bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
            TirerLaPoignee(fenetre, bloc, 1, 1, new Vector(vingt, 0));
            Assert.Equal(120, bloc.Height, precision: 9);
            Rendu.UnSeulPas(vm, avant, "coin tiré, case décochée");
            // Un autre coin que le précédent : deux pressions au même point, sans que
            // l'horloge du banc avance, font un double-clic, qui ouvrirait la saisie.
            bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
            TirerLaPoignee(fenetre, bloc, 1, 0, new Vector(vingt, -vingt / 3), RawInputModifiers.Shift);
            Assert.True(bloc.Width > 240, "le coin n'a pas agrandi le bloc");
            Assert.True(Math.Abs(rapport - bloc.Width / bloc.Height) <= 0.01);
            Rendu.UnSeulPas(vm, avant, "coin tiré sous Maj, case décochée");
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Case_cochee_la_saisie_de_L_ou_de_H_garde_le_rapport_en_un_pas()
    {
        var bloc = Texte(100, 100, Units.MmToPoints(80), Units.MmToPoints(40));
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            vm.SelectOnly(bloc);
            Rendu.Battre();
            var avant = DocumentSerializer.ToJson(vm.Document);

            // Décochée : L n'écrit que la largeur, comme avant.
            Rendu.Saisir(fenetre, Trouver<NumericUpDown>(fenetre, "ChampLargeur"), "100");
            Assert.Equal(Units.MmToPoints(100), bloc.Width, precision: 6);
            Assert.Equal(Units.MmToPoints(40), bloc.Height, precision: 6);
            Rendu.UnSeulPas(vm, avant, "L, case décochée");

            // La case ne change ni l'historique ni l'état « modifié », que l'annulation
            // vient de poser.
            var modifie = vm.IsDirty;
            vm.ConserverLesProportions = true;
            Rendu.Battre();
            Assert.Equal(modifie, vm.IsDirty);
            Assert.False(vm.CanUndo);

            // Cochée : L entraîne H, et le champ H le montre.
            bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
            Rendu.Saisir(fenetre, Trouver<NumericUpDown>(fenetre, "ChampLargeur"), "100");
            Assert.Equal(Units.MmToPoints(100), bloc.Width, precision: 6);
            Assert.Equal(Units.MmToPoints(50), bloc.Height, precision: 6);
            Assert.Equal(50m, Math.Round(Trouver<NumericUpDown>(fenetre, "ChampHauteur").Value ?? 0, 6));
            Rendu.UnSeulPas(vm, avant, "L, case cochée");

            // H entraîne L.
            bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
            Rendu.Saisir(fenetre, Trouver<NumericUpDown>(fenetre, "ChampHauteur"), "20");
            Assert.Equal(Units.MmToPoints(20), bloc.Height, precision: 6);
            Assert.Equal(Units.MmToPoints(40), bloc.Width, precision: 6);
            Rendu.UnSeulPas(vm, avant, "H, case cochée");

            // Le côté qui suit ne descend pas sous la taille minimale d'un bloc.
            Assert.Equal((Snap.MinFrameSize * 2, Snap.MinFrameSize),
                MainWindowViewModel.AuMemeRapport(80, 40, 2, null));
        }
        finally
        {
            fenetre.Close();
        }
    }
}
