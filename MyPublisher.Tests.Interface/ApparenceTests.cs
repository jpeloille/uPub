using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.LogicalTree;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// L'apparence de l'interface — poste I1 : la palette, les classes et les icônes de la
/// maquette, déclarées dans <c>Views/Apparence.axaml</c> et chargées par <c>App.axaml</c>.
/// Pas « charte » : le mot nomme déjà le jeu de couleurs et de polices du document, que
/// <c>JeuxTests</c> éprouve.
///
/// Le poste la pose, les suivants la lisent. Ces tests la mesurent sur des contrôles
/// construits pour l'occasion, dans une fenêtre à l'échelle 1, et vérifient qu'elle ne
/// touche pas aux vues d'avant la phase — sa première promesse, puisqu'elle est chargée
/// pour toute l'application. La fenêtre principale n'en est plus depuis le poste I2.
///
/// Les couleurs attendues sont lues dans l'apparence et non recopiées. Ce n'est pas un
/// cercle : que chaque clé porte la couleur de la maquette, et que chaque style cite la
/// bonne clé, c'est la recette qui le vérifie, sur les sources et contre la maquette
/// elle-même (<c>Scripts/recettes/I1-apparence.sh</c>). Ici, on vérifie que l'écran les
/// applique.
/// </summary>
[Trait("poste", "I1")]
public class ApparenceTests
{
    /// <summary>
    /// L'apparence telle qu'App.axaml l'a chargée. Le compilateur XAML remplace l'inclusion
    /// d'un fichier du projet par le jeu de styles lui-même : on la reconnaît donc à sa
    /// première couleur, et non au nom de son fichier.
    /// </summary>
    private static Styles Apparence() =>
        Application.Current!.Styles.OfType<Styles>().Single(s => s.Resources.ContainsKey("CouleurAccent"));

    private static IReadOnlyList<string> Icones() =>
        Apparence().Resources.Keys.OfType<string>().Where(c => c.StartsWith("Icone")).Order().ToList();

    private static object Ressource(string cle)
    {
        Assert.True(Application.Current!.TryGetResource(cle, ThemeVariant.Light, out var valeur),
            $"« {cle} » n'est pas dans l'apparence");
        return valeur!;
    }

    private static Color Couleur(string cle) => Assert.IsType<Color>(Ressource(cle));

    private static Control Icone(string cle) => Assert.IsAssignableFrom<Control>(Ressource(cle));

    /// <summary>
    /// L'image composée de la fenêtre, dans l'ordre de ses octets. Le banc sans tête la
    /// rend en RGBA — et non en BGRA comme <c>Rendu.Encre</c> : on lit son format plutôt
    /// que de le supposer, une couleur aux canaux permutés passant pour une autre.
    /// </summary>
    private static (Rendu.Image Image, bool Bgra) Composer(Window fenetre)
    {
        using var image = fenetre.CaptureRenderedFrame()
                          ?? throw new InvalidOperationException("aucune image composée");
        var bgra = image.Format == PixelFormat.Bgra8888;
        Assert.True(bgra || image.Format == PixelFormat.Rgba8888, $"format inattendu : {image.Format}");

        var pixels = new byte[image.PixelSize.Width * image.PixelSize.Height * 4];
        using var verrou = image.Lock();
        System.Runtime.InteropServices.Marshal.Copy(verrou.Address, pixels, 0, pixels.Length);
        return (new Rendu.Image(pixels, image.PixelSize.Width, image.PixelSize.Height), bgra);
    }

    private static Color Pixel((Rendu.Image Image, bool Bgra) rendu, int x, int y)
    {
        var (image, bgra) = rendu;
        var i = (y * image.Largeur + x) * 4;
        var (r, b) = bgra ? (image.Pixels[i + 2], image.Pixels[i]) : (image.Pixels[i], image.Pixels[i + 2]);
        return Color.FromArgb(image.Pixels[i + 3], r, image.Pixels[i + 1], b);
    }

    private static IEnumerable<(int X, int Y)> Zone(int x, int y, int largeur, int hauteur) =>
        from py in Enumerable.Range(y, hauteur)
        from px in Enumerable.Range(x, largeur)
        select (px, py);

    /// <summary>Une fenêtre blanche qui porte ces contrôles aux positions données.</summary>
    private static Window Poser(int largeur, int hauteur, params (Control Controle, int X, int Y)[] controles)
    {
        var toile = new Canvas { Background = Brushes.White };
        foreach (var (controle, x, y) in controles)
        {
            Canvas.SetLeft(controle, x);
            Canvas.SetTop(controle, y);
            toile.Children.Add(controle);
        }

        var fenetre = new Window { Width = largeur, Height = hauteur, Content = toile };
        fenetre.Show();
        Rendu.Battre();
        return fenetre;
    }

    [AvaloniaFact]
    public void Un_bouton_icone_rend_30_sur_30_a_l_echelle_1_et_actif_il_est_plein_d_accent()
    {
        // Deux icônes pleines et non à trait : un trait de 1,3 px ne couvre jamais tout à
        // fait un pixel, et l'on veut ici la couleur exacte que l'icône reçoit.
        var repos = new Button { Classes = { "ib" }, Content = Icone("IconeSelection") };
        var actif = new ToggleButton { Classes = { "ib" }, IsChecked = true, Content = Icone("IconeBlocDeTexte") };
        var fenetre = Poser(120, 60, (repos, 10, 10), (actif, 60, 10));
        try
        {
            // L'échelle 1 d'abord : à une autre, 30 unités ne font pas 30 pixels, et la
            // mesure ci-dessous ne prouverait rien de la taille.
            Assert.Equal(1.0, fenetre.RenderScaling);
            Assert.Equal(new Size(30, 30), repos.Bounds.Size);
            Assert.Equal(new Size(30, 30), actif.Bounds.Size);
            // Le rayon de la maquette, exactement. Les pixels du coin, plus bas, ne
            // distinguent pas un rayon de 5 d'un rayon de 6 — l'un et l'autre laissent le
            // pixel d'angle au fond et adoucissent son voisin —, ni un rayon de 6 d'un rond
            // de 15, dont la boîte et l'aire tiennent encore dans leurs bornes.
            Assert.Equal(new CornerRadius(6), repos.CornerRadius);
            Assert.Equal(new CornerRadius(6), actif.CornerRadius);

            var image = Composer(fenetre);
            var accent = Couleur("CouleurAccent");
            var blanc = Couleur("CouleurBlanc");
            var commande = Couleur("CouleurCommande");

            // Actif : l'accent remplit exactement 30 × 30 pixels, au coin arrondi près.
            var pleins = Zone(0, 0, image.Image.Largeur, image.Image.Hauteur)
                .Where(p => Pixel(image, p.X, p.Y) == accent).ToList();
            Assert.NotEmpty(pleins);
            Assert.Equal((60, 10), (pleins.Min(p => p.X), pleins.Min(p => p.Y)));
            Assert.Equal((30, 30), (pleins.Max(p => p.X) - 59, pleins.Max(p => p.Y) - 9));
            Assert.True(pleins.Count > 600, $"l'accent ne couvre que {pleins.Count} pixels sur 900");
            // Et ce rayon est dessiné : le pixel d'angle et son voisin en diagonale restent
            // hors de l'accent plein, ce qu'un rayon de 4 ou moins ne ferait plus pour le
            // second.
            Assert.NotEqual(accent, Pixel(image, 60, 10));
            Assert.NotEqual(accent, Pixel(image, 61, 11));
            // L'icône de l'outil actif est blanche : elle suit la couleur du bouton.
            Assert.True(Zone(60, 10, 30, 30).Count(p => Pixel(image, p.X, p.Y) == blanc) > 20,
                "l'icône du bouton actif n'est pas blanche");

            // Au repos : pas un pixel d'accent, ni le fond gris de Fluent ; l'icône à la
            // couleur des commandes.
            Assert.DoesNotContain(Zone(10, 10, 30, 30), p => Pixel(image, p.X, p.Y) == accent);
            Assert.Equal(blanc, Pixel(image, 10, 10));
            Assert.Equal(blanc, Pixel(image, 12, 37));
            Assert.True(Zone(10, 10, 30, 30).Count(p => Pixel(image, p.X, p.Y) == commande) > 20,
                "l'icône du bouton au repos n'est pas à la couleur des commandes");
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Un_bouton_icone_survole_presse_ou_grise_suit_la_maquette()
    {
        // La maquette dessine le survol (« .ib:hover », un gris) et rien d'autre : pressé, un
        // bouton y est survolé ; actif, il reste plein d'accent même survolé, « .ib.on »
        // venant après « .ib:hover ». Le grisé, qu'elle ne dessine pas et que I2 demande pour
        // Annuler et Rétablir, efface le bouton sans lui donner de fond. Fluent a, pour
        // chacun de ces états, son propre gris ou son propre bleu : c'est eux qu'on chasse.
        var repos = new Button { Classes = { "ib" }, Content = Icone("IconeSelection") };
        var actif = new ToggleButton { Classes = { "ib" }, IsChecked = true, Content = Icone("IconeBlocDeTexte") };
        var grise = new Button { Classes = { "ib" }, IsEnabled = false, Content = Icone("IconeSelection") };
        var fenetre = Poser(170, 60, (repos, 10, 10), (actif, 60, 10), (grise, 110, 10));
        try
        {
            var accent = Couleur("CouleurAccent");
            var survol = Couleur("CouleurSurvol");
            var commande = Couleur("CouleurCommande");
            var blanc = Couleur("CouleurBlanc");

            // Un geste, puis l'image. « CaptureRenderedFrame » n'avance l'horloge du rendu que
            // d'un tic, et cela ne suffit pas toujours : relâché puis quitté, le bouton pressé
            // restait gris sur l'image alors que son fond était déjà transparent — une image
            // en retard d'un tic. Un tic de plus, et l'image suit l'état qu'on vient de poser ;
            // sans lui, la mesure de l'état pressé pouvait lire celle du survol d'avant.
            (Rendu.Image Image, bool Bgra) Apres(Action geste)
            {
                geste();
                Rendu.Battre();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Rendu.Battre();
                return Composer(fenetre);
            }

            // Survolé : le fond passe au gris de la maquette d'un bord à l'autre — (12, 25)
            // et (37, 25) sont à deux pixels des bords, hors des coins et de l'icône —, et
            // l'icône garde la couleur des commandes.
            var image = Apres(() => fenetre.MouseMove(new Point(25, 25)));
            Assert.True(repos.IsPointerOver, "le pointeur synthétique ne survole pas le bouton");
            Assert.Equal(survol, Pixel(image, 12, 25));
            Assert.Equal(survol, Pixel(image, 37, 25));
            Assert.True(Zone(10, 10, 30, 30).Count(p => Pixel(image, p.X, p.Y) == commande) > 20,
                "l'icône du bouton survolé n'est plus à la couleur des commandes");

            // Pressé : le même gris, et non celui, plus sombre, de Fluent.
            image = Apres(() => fenetre.MouseDown(new Point(25, 25), MouseButton.Left));
            Assert.True(repos.IsPressed, "le pointeur synthétique ne presse pas le bouton");
            Assert.Equal(survol, Pixel(image, 12, 25));
            fenetre.MouseUp(new Point(25, 25), MouseButton.Left);

            // Actif et survolé : plein d'accent, l'icône blanche ; le bouton quitté revient
            // au fond, quoiqu'il garde le focus que la pression lui a donné.
            image = Apres(() => fenetre.MouseMove(new Point(75, 25)));
            Assert.True(actif.IsPointerOver, "le pointeur synthétique ne survole pas le bouton actif");
            Assert.Equal(accent, Pixel(image, 62, 25));
            Assert.True(Zone(60, 10, 30, 30).Count(p => Pixel(image, p.X, p.Y) == blanc) > 20,
                "l'icône du bouton actif survolé n'est plus blanche");
            Assert.Equal(blanc, Pixel(image, 12, 25));

            // Grisé, le pointeur dessus : aucun fond — ni le gris du survol, ni celui de
            // Fluent —, et l'icône à 40 % : son pixel le plus sombre est le gris des commandes
            // mêlé au blanc dans cette proportion, à l'arrondi près.
            image = Apres(() => fenetre.MouseMove(new Point(125, 25)));
            Assert.Equal(blanc, Pixel(image, 112, 25));
            Assert.Equal(blanc, Pixel(image, 137, 25));
            var sombre = Zone(110, 10, 30, 30).Select(p => Pixel(image, p.X, p.Y)).MinBy(c => c.R + c.G + c.B);
            byte Mele(byte c, byte fond) => (byte)Math.Round(0.4 * c + 0.6 * fond);
            var voulu = Color.FromRgb(Mele(commande.R, blanc.R), Mele(commande.G, blanc.G), Mele(commande.B, blanc.B));
            Assert.True(Math.Abs(sombre.R - voulu.R) <= 2 && Math.Abs(sombre.G - voulu.G) <= 2 &&
                        Math.Abs(sombre.B - voulu.B) <= 2,
                $"le bouton grisé dessine son icône en {sombre}, et non en {voulu} — le gris des commandes à 40 %");
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Chaque_icone_se_cree_a_chaque_emploi_et_se_dessine_dans_sa_boite()
    {
        var cles = Icones();
        Assert.NotEmpty(cles);

        // L'accent pour couleur courante : il se distingue du gris fixe de l'œil et de la
        // rotation, que la maquette n'écrit pas en « currentColor ».
        var accent = new SolidColorBrush(Couleur("CouleurAccent"));
        var poses = new List<(Control, int, int)>();
        var tailles = new Dictionary<string, (int X, int Y, int Taille, bool Courante)>();
        for (var i = 0; i < cles.Count; i++)
        {
            var cle = cles[i];
            var icone = Icone(cle);

            // Une icône partagée n'aurait qu'un parent : employée deux fois, elle
            // quitterait sa première place sans un mot. « x:Shared="False" » en fait un
            // exemplaire neuf à chaque emploi.
            Assert.NotSame(icone, Icone(cle));

            var boite = Assert.IsType<Viewbox>(icone);
            Assert.True(boite.Width is >= 12 and <= 18 && boite.Width == boite.Height,
                $"{cle} : boîte de {boite.Width} × {boite.Height}");
            var traits = boite.GetLogicalDescendants().OfType<Avalonia.Controls.Shapes.Path>().ToList();
            Assert.NotEmpty(traits);

            var cadre = new Border { Width = 40, Height = 40, Child = icone };
            TextElement.SetForeground(cadre, accent);
            var (x, y) = (10 + i % 8 * 50, 10 + i / 8 * 50);
            poses.Add((cadre, x, y));
            tailles[cle] = (x + (40 - (int)boite.Width) / 2, y + (40 - (int)boite.Height) / 2, (int)boite.Width,
                traits.Any(t => t.Classes.Contains("iconeTrait") || t.Classes.Contains("iconePlein")));
        }

        var fenetre = Poser(420, 10 + (cles.Count + 7) / 8 * 50, poses.ToArray());
        try
        {
            var image = Composer(fenetre);
            bool Encre(Color c) => Math.Min(c.R, Math.Min(c.G, c.B)) < 200;
            bool Bleue(Color c) => c.B - c.R > 60;

            foreach (var cle in cles)
            {
                var (x, y, taille, courante) = tailles[cle];
                var cellule = Zone(x - 5, y - 5, taille + 10, taille + 10)
                    .Select(p => (p.X, p.Y, Couleur: Pixel(image, p.X, p.Y)))
                    .Where(p => Encre(p.Couleur)).ToList();

                Assert.True(cellule.Count > 10, $"{cle} ne dessine presque rien ({cellule.Count} pixels)");
                // Rien hors de sa boîte : la grille de 24 est rognée comme le svg la rogne.
                Assert.All(cellule, p => Assert.True(
                    p.X >= x && p.X < x + taille && p.Y >= y && p.Y < y + taille,
                    $"{cle} dessine en ({p.X}, {p.Y}), hors de sa boîte de {taille} px en ({x}, {y})"));
                // La couleur courante, sauf pour les icônes que la maquette colore elle-même.
                Assert.True(cellule.Any(p => Bleue(p.Couleur)) == courante,
                    courante ? $"{cle} ne suit pas la couleur courante" : $"{cle} devait garder sa couleur");
            }
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Champ_libelle_separateur_et_titre_de_section_ont_les_mesures_de_la_maquette()
    {
        var texte = new TextBox { Classes = { "champ" }, Text = "10,5 pt", Width = 58 };
        var liste = new ComboBox { Classes = { "champ" }, Width = 150, ItemsSource = new[] { "Inter" }, SelectedIndex = 0 };
        var nombre = new NumericUpDown { Classes = { "champ" }, Width = 90, Value = 52 };
        var libelle = new TextBlock { Classes = { "libelle" }, Text = "Interligne" };
        var filet = new Border { Classes = { "separateur" } };
        var section = new ToggleButton { Classes = { "titreSection" }, IsChecked = true, Width = 292, Content = "Position et taille" };
        var fenetre = Poser(420, 140,
            (texte, 10, 10), (liste, 80, 10), (nombre, 240, 10), (libelle, 10, 50), (filet, 100, 46),
            (section, 10, 80));
        try
        {
            var image = Composer(fenetre);
            var bord = Couleur("CouleurBordChamp");

            // Champ : 26 px, rayon 5, corps 12 ; Fluent en donnerait 32 de haut, et 64 de
            // large au moins à la zone de texte.
            foreach (var (champ, x) in new (TemplatedControl, int)[] { (texte, 10), (liste, 80), (nombre, 240) })
            {
                Assert.Equal(26, champ.Bounds.Height);
                Assert.Equal(12, champ.FontSize);
                Assert.Equal(new CornerRadius(5), champ.CornerRadius);
                Assert.Equal(bord, Pixel(image, x + (int)champ.Bounds.Width / 2, 10));
                Assert.Equal(bord, Pixel(image, x + (int)champ.Bounds.Width / 2, 35));
            }
            Assert.Equal(58, texte.Bounds.Width);

            Assert.Equal(11, libelle.FontSize);
            Assert.Equal(Couleur("CouleurLibelle"), Assert.IsAssignableFrom<ISolidColorBrush>(libelle.Foreground).Color);

            // Le séparateur, écarté de 6 de chaque côté : posé en 100, il se dessine en 106.
            Assert.Equal(new Rect(106, 46, 1, 20), filet.Bounds);
            Assert.Equal(Couleur("CouleurFiletFort"), Pixel(image, 106, 56));

            // Titre de section : corps 12 demi-gras, filet au-dessus ; ouvert (coché), son
            // fond reste celui du dock — Fluent le peindrait d'accent.
            Assert.Equal(12, section.FontSize);
            Assert.Equal(FontWeight.SemiBold, section.FontWeight);
            Assert.Equal(Couleur("CouleurFilet"), Pixel(image, 10 + 200, 80));
            Assert.Equal(Colors.White, Pixel(image, 10 + 250, 80 + (int)section.Bounds.Height / 2));
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Aucune_cle_de_l_apparence_ne_masque_une_ressource_du_theme()
    {
        // Les modèles de Fluent cherchent leurs pinceaux par leur nom, de proche en proche
        // jusqu'à l'application, où l'apparence est lue APRÈS le thème : une de ses clés
        // qui porterait le nom d'une des leurs repeindrait tous les contrôles de la fenêtre.
        var themes = Application.Current!.Styles.Where(s => !ReferenceEquals(s, Apparence())).ToList();
        Assert.Contains(themes, s => s is FluentTheme);

        foreach (var cle in Apparence().Resources.Keys)
        foreach (var theme in themes)
        foreach (var variante in new[] { ThemeVariant.Light, ThemeVariant.Dark, ThemeVariant.Default })
        {
            Assert.False(theme.TryGetResource(cle, variante, out _),
                $"« {cle} » est aussi une ressource de {theme.GetType().Name}");
        }
    }

    [AvaloniaFact]
    public void L_apparence_ne_change_pas_un_pixel_des_vues_d_avant_la_phase()
    {
        // Écrit d'abord sur la fenêtre principale, quand aucune vue ne se servait de
        // l'apparence. Depuis le poste I2, la fenêtre est de la phase : elle cite
        // l'apparence, et ne s'ouvre plus sans elle. La promesse reste — un style sans
        // classe rhabillerait ce qui n'en demande rien — et se mesure sur ce qui ne porte
        // aucune de ses classes : la planche, refaite par I4, ouverte seule sur l'exemple,
        // un bloc de texte sélectionné ; et les deux dialogues, coordonnées et catalogue des
        // modèles, avec leurs champs, leurs listes et leurs boutons.
        Rendu.Image Composer(Func<Window> ouvrir)
        {
            var fenetre = ouvrir();
            fenetre.Show();
            try
            {
                Rendu.Battre(8);
                return Rendu.Composee(fenetre);
            }
            finally
            {
                fenetre.Close();
            }
        }

        Window Planche()
        {
            var document = DocumentSerializer.Load(Path.Combine(Rendu.Racine(), "Samples", "exemple-bulletin.mypub"));
            var vm = new ViewModels.MainWindowViewModel(contacts: new BusinessInfoStore(),
                horloge: () => new DateTime(2026, 9, 6))
            {
                Document = document,
                CurrentPage = document.Pages[0],
                FilePath = "banc.mypub",
                Zoom = 1 / Models.Units.PointToDip,
            };
            vm.SelectOnly(document.Pages[0].Frames.OfType<Models.TextFrame>().First());
            return new Window { Width = 900, Height = 700, DataContext = vm, Content = new Views.PageCanvas() };
        }

        Window Coordonnees() => new Views.BusinessInfoWindow
        {
            Width = 700, Height = 600,
            DataContext = new ViewModels.BusinessInfoViewModel(new BusinessInfoStore()),
        };

        Window Catalogue() => new Views.TemplateChooserWindow
        {
            Width = 800, Height = 600,
            DataContext = new ViewModels.TemplateChooserViewModel(TemplateStore.Load()),
        };

        // Sans ses STYLES, et non sans elle : ses couleurs restent. Depuis le poste I4, la
        // planche est une vue de la phase et cite les pinceaux de l'apparence — retirée
        // tout entière, l'apparence emporterait ce que la planche lit, et elle ne
        // s'ouvrirait plus. Ce que ce test garde n'a pas changé : aucun style de
        // l'apparence ne rhabille ce qui ne porte pas sa classe. La planche ne porte
        // aucune classe ; elle reste donc mesurée, avec les deux dialogues. Une clé qui
        // masquerait une ressource du thème est l'affaire du test précédent.
        var apparence = Apparence();
        var regles = apparence.ToList();

        foreach (var (nom, vue) in new (string, Func<Window>)[]
                 { ("la planche", Planche), ("les coordonnées", Coordonnees), ("le catalogue", Catalogue) })
        {
            var avec = Composer(vue);
            Rendu.Image sans;
            apparence.Clear();
            try
            {
                sans = Composer(vue);
            }
            finally
            {
                apparence.AddRange(regles);
            }
            var encore = Composer(vue);

            // D'abord que la mesure soit sûre : deux ouvertures avec l'apparence rendent la
            // même image. Sans cela, l'égalité qui suit ne prouverait rien, et l'écart rien
            // non plus.
            Assert.True(avec.Pixels.SequenceEqual(encore.Pixels), $"{nom} : deux ouvertures identiques ne rendent pas la même image");
            Assert.Equal((avec.Largeur, avec.Hauteur), (sans.Largeur, sans.Hauteur));

            var ecarts = Enumerable.Range(0, avec.Pixels.Length / 4)
                .Where(i => !avec.Pixels.AsSpan(i * 4, 4).SequenceEqual(sans.Pixels.AsSpan(i * 4, 4)))
                .ToList();
            Assert.True(ecarts.Count == 0,
                $"l'apparence change {ecarts.Count} pixels dans {nom}, le premier en " +
                $"({ecarts.FirstOrDefault() % avec.Largeur}, {ecarts.FirstOrDefault() / avec.Largeur})");
        }
    }
}
