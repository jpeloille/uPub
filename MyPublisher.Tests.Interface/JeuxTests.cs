using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Le jeu de couleurs et le jeu de polices à l'écran — poste C5b.
///
/// Trois choses s'y éprouvent. Que la page dessine une référence dans la couleur du jeu,
/// partout où une couleur se pose — fond uni, dégradé, teinte, contour, texte, fragment,
/// case, fond de page — et qu'elle se REDESSINE quand on change de jeu, sans toucher aux
/// couleurs libres. Que le panneau propose le jeu et écrive une référence. Et que le
/// panneau n'écrive rien qu'on ne lui ait demandé : le sélecteur de couleur renvoie ce
/// qu'il lit, et sélectionner un bloc ne doit pas le détacher du jeu.
/// </summary>
[Trait("poste", "C5b")]
public class JeuxTests
{
    private static readonly JeuDeCouleurs Ardoise = JeuxPredefinis.CouleursParDefaut;
    private static readonly JeuDeCouleurs Ocean = JeuxPredefinis.CouleursNommees("Océan")!;

    /// <summary>La couleur d'un pixel de la page, en #AARRGGBB — l'écriture du modèle.</summary>
    private static string Teinte(Rendu.Image image, double x, double y)
    {
        var px = (int)Math.Round(x * Rendu.PixelsParPoint);
        var py = (int)Math.Round(y * Rendu.PixelsParPoint);
        var i = (py * image.Largeur + px) * 4;

        // Le tampon est en BGRA.
        return HexColor.Format(
            image.Pixels[i + 3], image.Pixels[i + 2], image.Pixels[i + 1], image.Pixels[i]);
    }

    /// <summary>Combien de pixels d'une zone portent exactement cette couleur.</summary>
    private static int Compter(Rendu.Image image, double x, double y, double l, double h, string couleur)
    {
        var n = 0;
        for (var py = (int)(y * Rendu.PixelsParPoint); py < (int)((y + h) * Rendu.PixelsParPoint); py++)
        for (var px = (int)(x * Rendu.PixelsParPoint); px < (int)((x + l) * Rendu.PixelsParPoint); px++)
        {
            if (Teinte(image, px / Rendu.PixelsParPoint, py / Rendu.PixelsParPoint) == couleur) n++;
        }

        return n;
    }

    private static ShapeFrame Forme(double x, double y, FillBrush? fond, string? contour = null, double epaisseur = 1) => new()
    {
        X = x, Y = y, Width = 80, Height = 60, Fill = fond, Stroke = contour, StrokeThickness = epaisseur,
    };

    // ------------------------------------------------------------------ la page

    [AvaloniaFact]
    public void Changer_de_jeu_recolore_les_blocs_qui_y_font_reference_et_eux_seuls()
    {
        // Le bloc libre porte EXACTEMENT l'accent 1 d'Ardoise : avant le changement, les
        // deux blocs sont indiscernables à l'écran. Seule la référence les distingue.
        var reference = Forme(40, 40, new SolidFill("scheme:accent1"));
        var libre = Forme(160, 40, new SolidFill(Ardoise.Accent1));
        var texte = new TextFrame
        {
            X = 40, Y = 140, Width = 300, Height = 80, FontSize = 48, Bold = true,
            Text = "████", Foreground = "scheme:accent1",
        };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(reference, libre, texte));

        var avant = Rendu.Encre(fenetre);
        Assert.Equal(Ardoise.Accent1, Teinte(avant, 80, 70));
        Assert.Equal(Ardoise.Accent1, Teinte(avant, 200, 70));
        var (gx, gy) = Premier(avant, 40, 140, 300, 80, Ardoise.Accent1);

        // Ce que la fenêtre montre, et non ce qu'un rendu forcé dessinerait : c'est ainsi
        // qu'on sait que l'écran a suivi de lui-même. Lu au point même, parce que le
        // panneau change aussi en changeant de jeu — une image entière différerait de
        // toute façon.
        Assert.Equal(Ardoise.Accent1, Affichee(fenetre, 80, 70));
        Assert.Equal(Ardoise.Accent1, Affichee(fenetre, gx, gy));

        vm.ChosenColorScheme = Ocean;
        Rendu.Battre();

        Assert.Equal(Ocean.Accent1, Affichee(fenetre, 80, 70));
        Assert.Equal(Ocean.Accent1, Affichee(fenetre, gx, gy));
        Assert.Equal(Ardoise.Accent1, Affichee(fenetre, 200, 70));

        var apres = Rendu.Encre(fenetre);
        Assert.Equal(Ocean.Accent1, Teinte(apres, 80, 70));
        Assert.Equal(Ardoise.Accent1, Teinte(apres, 200, 70));
    }

    /// <summary>Le premier point d'une zone de la page qui porte cette couleur, en points.</summary>
    private static (double X, double Y) Premier(Rendu.Image image, double x, double y, double l, double h, string couleur)
    {
        // Pris au milieu d'un carré de 3 × 3 pixels de la même couleur : un pixel de bord
        // d'un glyphe, lissé, ne se relirait pas pareil d'un rendu à l'autre.
        for (var py = y + 1; py < y + h - 1; py += 0.5)
        for (var px = x + 1; px < x + l - 1; px += 0.5)
        {
            var plein = true;
            for (var d = -1.0; d <= 1 && plein; d += 0.5)
            for (var e = -1.0; e <= 1 && plein; e += 0.5)
            {
                plein = Teinte(image, px + d, py + e) == couleur;
            }

            if (plein) return (px, py);
        }

        throw new InvalidOperationException($"aucun point de couleur {couleur} dans la zone");
    }

    /// <summary>La couleur que la fenêtre affiche en un point de la page, en #AARRGGBB.</summary>
    private static string Affichee(Window fenetre, double x, double y)
    {
        var page = fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");
        var point = page.TranslatePoint(new Point(x, y), fenetre)
                    ?? throw new InvalidOperationException("la page n'est pas dans la fenêtre");
        var image = Rendu.Composee(fenetre);
        var i = ((int)point.Y * image.Largeur + (int)point.X) * 4;

        // L'image composée est en RGBA, à la différence du rendu hors champ (BGRA).
        return HexColor.Format(
            image.Pixels[i + 3], image.Pixels[i], image.Pixels[i + 1], image.Pixels[i + 2]);
    }

    [AvaloniaFact]
    public void Les_remplissages_de_C5c_prennent_le_jeu()
    {
        var degrade = Forme(40, 40, new GradientFill("scheme:accent1", "scheme:accent3", 0));
        var teinte = Forme(160, 40, new TintFill("scheme:accent1", 40));
        var document = Rendu.Feuille(degrade, teinte);
        document.Background = new SolidFill("scheme:accent2");
        document.ColorScheme = Ocean;
        var (fenetre, _) = Rendu.Ouvrir(document);

        var image = Rendu.Encre(fenetre);

        // Le dégradé tout près de ses deux bords : presque l'accent 1, presque l'accent 3.
        AssertProche(Ocean.Accent1, Teinte(image, 41, 70), 12);
        AssertProche(Ocean.Accent3, Teinte(image, 119, 70), 12);
        // La teinte mélange la couleur du jeu, pas sa référence.
        Assert.Equal(new TintFill(Ocean.Accent1, 40).Uniforme, Teinte(image, 200, 70));
        // Le fond de page, hors de tout bloc.
        Assert.Equal(Ocean.Accent2, Teinte(image, 400, 400));
    }

    [AvaloniaFact]
    public void Un_contour_un_texte_et_un_fragment_du_jeu_suivent_le_jeu()
    {
        var contour = Forme(40, 40, null, "scheme:accent3", 8);
        var texte = new TextFrame
        {
            X = 40, Y = 140, Width = 400, Height = 80, FontSize = 48, Bold = true,
            Text = "████ ████", Foreground = "scheme:accent1",
            Spans = [new StyleSpan { Start = 5, Length = 4, Foreground = "scheme:accent4" }],
        };
        var document = Rendu.Feuille(contour, texte);
        var (fenetre, vm) = Rendu.Ouvrir(document);

        vm.ChosenColorScheme = Ocean;
        Rendu.Battre();
        var image = Rendu.Encre(fenetre);

        // Le contour, au milieu du côté gauche, dans son épaisseur (rentrée d'une
        // demi-épaisseur à l'écran : c'est Q4, et ce n'est pas d'ici).
        Assert.Equal(Ocean.Accent3, Teinte(image, 42, 70));
        // Le texte et son fragment : des pavés pleins dans chacune des deux couleurs.
        Assert.True(Compter(image, 40, 140, 400, 80, Ocean.Accent1) > 400, "le texte n'est pas dans l'accent 1 du jeu");
        Assert.True(Compter(image, 40, 140, 400, 80, Ocean.Accent4) > 400, "le fragment n'est pas dans l'accent 4 du jeu");
        Assert.Equal(0, Compter(image, 40, 140, 400, 80, Ardoise.Accent1));
    }

    [AvaloniaFact]
    public void Une_case_de_tableau_prend_le_jeu()
    {
        var tableau = new TableFrame { X = 40, Y = 40 };
        tableau.Columns.Add(new TableColumn(100));
        tableau.Rows.Add(new TableRow(40));
        tableau.Cells.Add(new TableCell { Row = 0, Column = 0, Fill = new SolidFill("scheme:accent4") });
        var document = Rendu.Feuille(tableau);
        document.ColorScheme = Ocean;
        var (fenetre, _) = Rendu.Ouvrir(document);

        Assert.Equal(Ocean.Accent4, Teinte(Rendu.Encre(fenetre), 90, 60));
    }

    [AvaloniaFact]
    public void Une_police_du_jeu_change_la_composition()
    {
        // Le titre est mesuré dans la police du jeu : passer d'une sans empattement à
        // une police à empattements change l'encre que le mot laisse sur la page.
        var titre = new TextFrame
        {
            X = 40, Y = 40, Width = 500, Height = 80, FontSize = 40,
            Text = "Graphisme", FontFamily = "scheme:heading", Foreground = "#FF000000",
        };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(titre));
        var avant = Rendu.Encre(fenetre);

        vm.ChosenFontScheme = JeuxPredefinis.PolicesNommees("Archives");
        Rendu.Battre();

        Assert.NotEqual(avant.Pixels, Rendu.Encre(fenetre).Pixels);
    }

    // ---------------------------------------------------------------- le panneau

    [AvaloniaFact]
    public void Selectionner_un_bloc_ne_le_detache_pas_du_jeu()
    {
        // Le piège du sélecteur de couleur, qui renvoie ce qu'il lit dès qu'il s'affiche.
        // Branché sans précaution, il lirait l'accent résolu et le renverrait en hexa :
        // le bloc quitterait le jeu à la seule sélection.
        var forme = Forme(40, 40, new SolidFill("scheme:accent1"), "scheme:accent2/80");
        var degrade = Forme(160, 40, new GradientFill("scheme:main", "scheme:accent3", 0));
        var teinte = Forme(280, 40, new TintFill("scheme:accent4", 40));
        var texte = new TextFrame { X = 40, Y = 140, Text = "Texte", Foreground = "scheme:accent2" };
        var document = Rendu.Feuille(forme, degrade, teinte, texte);
        document.Background = new SolidFill("scheme:main");
        var (_, vm) = Rendu.Ouvrir(document);

        foreach (var bloc in new Frame[] { forme, degrade, teinte, texte, forme })
        {
            vm.Selected = bloc;
            Rendu.Battre(8);
        }

        Assert.Equal("scheme:accent1", ((SolidFill)forme.Fill!).Color);
        Assert.Equal("scheme:accent2/80", forme.Stroke);
        Assert.Equal("scheme:main", ((GradientFill)degrade.Fill!).From);
        Assert.Equal("scheme:accent3", ((GradientFill)degrade.Fill!).To);
        Assert.Equal("scheme:accent4", ((TintFill)teinte.Fill!).Color);
        Assert.Equal("scheme:accent2", texte.Foreground);
        Assert.Equal("scheme:main", ((SolidFill)document.Background!).Color);
        Assert.False(vm.CanUndo, "la sélection a empilé un pas d'annulation : elle a écrit dans le document");
    }

    [AvaloniaFact]
    public void Une_pastille_du_panneau_fait_reference_au_jeu()
    {
        var forme = Forme(40, 40, new SolidFill("#FF123456"), "#FF654321");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(forme));
        vm.Selected = forme;
        Rendu.Battre();

        // Le sélecteur du fond est celui qui montre la couleur du fond.
        var selecteur = fenetre.GetVisualDescendants().OfType<SelecteurDeCouleur>()
            .Single(s => ReferenceEquals(s.DataContext, forme) && s.Valeur == "#FF123456");
        var pastille = selecteur.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Tag is EmplacementDeCouleur.Accent3);

        Cliquer(fenetre, pastille);

        Assert.Equal("scheme:accent3", ((SolidFill)forme.Fill!).Color);
        Assert.Equal("#FF654321", forme.Stroke);
        Assert.Equal(Ardoise.Accent3, Teinte(Rendu.Encre(fenetre), 80, 70));

        vm.ChosenColorScheme = Ocean;
        Rendu.Battre();
        Assert.Equal(Ocean.Accent3, Teinte(Rendu.Encre(fenetre), 80, 70));
    }

    [AvaloniaFact]
    public void Une_couleur_choisie_au_selecteur_libre_reste_libre()
    {
        var forme = Forme(40, 40, new SolidFill("scheme:accent1"));
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(forme));
        vm.Selected = forme;
        Rendu.Battre();

        var selecteur = fenetre.GetVisualDescendants().OfType<SelecteurDeCouleur>()
            .Single(s => ReferenceEquals(s.DataContext, forme) && s.Valeur == "scheme:accent1");
        var libre = selecteur.GetVisualDescendants().OfType<ColorPicker>().Single();

        // Le sélecteur libre montre la couleur qu'on voit, référence résolue…
        Assert.Equal(Color.Parse(Ardoise.Accent1), libre.Color);

        // … et une couleur qu'on y choisit est écrite en hexadécimal : elle ne suivra pas.
        libre.Color = Color.Parse("#FF00AA55");
        Rendu.Battre();

        Assert.Equal("#FF00AA55", ((SolidFill)forme.Fill!).Color);
        vm.ChosenColorScheme = Ocean;
        Rendu.Battre();
        Assert.Equal("#FF00AA55", ((SolidFill)forme.Fill!).Color);
    }

    [AvaloniaFact]
    public void Changer_de_jeu_au_panneau_se_fait_et_sannule()
    {
        var forme = Forme(40, 40, new SolidFill("scheme:accent1"));
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(forme));

        var liste = fenetre.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "JeuDeCouleurs");
        Assert.Equal(JeuxPredefinis.Couleurs.Count, liste.ItemCount);
        Assert.Equal(Ardoise, liste.SelectedItem);

        liste.SelectedItem = Ocean;
        Rendu.Battre();

        Assert.Equal(Ocean, vm.Document.ColorScheme);
        Assert.Equal(Ocean.Accent1, Teinte(Rendu.Encre(fenetre), 80, 70));

        vm.UndoCommand.Execute(null);
        Rendu.Battre();

        Assert.Equal(Ardoise, vm.Document.ColorScheme);
        Assert.Equal(Ardoise, liste.SelectedItem);
        Assert.Equal(Ardoise.Accent1, Teinte(Rendu.Encre(fenetre), 80, 70));
    }

    [AvaloniaFact]
    public void Un_document_au_jeu_inconnu_garde_son_jeu()
    {
        // Un jeu absent de la liste laisse la liste sans sélection, et une liste sans
        // sélection renvoie null à sa source : le document ne doit pas le perdre.
        var maison = new JeuDeCouleurs { Name = "Maison", Accent1 = "#FF00FF00" };
        var document = Rendu.Feuille(Forme(40, 40, new SolidFill("scheme:accent1")));
        document.ColorScheme = maison;
        var (fenetre, vm) = Rendu.Ouvrir(document);
        Rendu.Battre();

        Assert.Equal(maison, vm.Document.ColorScheme);
        Assert.Equal("#FF00FF00", Teinte(Rendu.Encre(fenetre), 80, 70));
    }

    [AvaloniaFact]
    public void La_liste_des_polices_propose_le_titre_et_le_corps_du_jeu()
    {
        var texte = new TextFrame { X = 40, Y = 40, Text = "Texte", FontFamily = "DejaVu Sans" };
        var document = Rendu.Feuille(texte);
        document.FontScheme = JeuxPredefinis.PolicesNommees("Édition")!;
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.Selected = texte;
        Rendu.Battre();

        Assert.Equal(["scheme:heading", "scheme:body"], vm.FontFamilies.Take(2));

        var liste = fenetre.GetVisualDescendants().OfType<ComboBox>()
            .Single(c => ReferenceEquals(c.DataContext, texte) && ReferenceEquals(c.ItemsSource, vm.FontFamilies));
        liste.SelectedItem = "scheme:heading";
        Rendu.Battre();

        Assert.Equal("scheme:heading", texte.FontFamily);
        // La liste dit quelle police la référence désigne aujourd'hui.
        Assert.Contains(liste.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text == "Titre du jeu — Liberation Serif");
    }

    [AvaloniaFact]
    public void Les_blocs_neufs_prennent_leurs_couleurs_et_leurs_polices_dans_le_jeu()
    {
        var (_, vm) = Rendu.Ouvrir(Rendu.Feuille());

        vm.AddRectangleCommand.Execute(null);
        var forme = (ShapeFrame)vm.CurrentPage!.Frames[^1];
        vm.AddHeadingCommand.Execute(null);
        var titre = (TextFrame)vm.CurrentPage.Frames[^1];
        vm.AddTextCommand.Execute(null);
        var texte = (TextFrame)vm.CurrentPage.Frames[^1];

        Assert.Equal("scheme:accent2", ((SolidFill)forme.Fill!).Color);
        Assert.Equal("scheme:accent1", forme.Stroke);
        Assert.Equal("scheme:heading", titre.FontFamily);
        Assert.Equal("scheme:body", texte.FontFamily);
        Assert.Equal("scheme:main", texte.Foreground);

        // Et le jeu par défaut leur rend exactement l'allure qu'ils avaient avant ce poste.
        var charte = vm.Document.Charte;
        Assert.Equal("#FF9DC3E6", forme.Fill.UniformeSelon(charte));
        Assert.Equal("#FF2E5C8A", charte.Couleur(forme.Stroke));
        Assert.Equal("DejaVu Sans", charte.Police(texte.FontFamily));
        Assert.Equal("#FF1A1A1A", charte.Couleur(texte.Foreground));
    }

    // ------------------------------------------------------------------ outils

    private static void Cliquer(Window fenetre, Control cible)
    {
        cible.BringIntoView();
        Rendu.Battre();

        var centre = cible.TranslatePoint(new Point(cible.Bounds.Width / 2, cible.Bounds.Height / 2), fenetre)
                     ?? throw new InvalidOperationException("la cible n'est pas dans la fenêtre");
        fenetre.MouseDown(centre, MouseButton.Left);
        fenetre.MouseUp(centre, MouseButton.Left);
        Rendu.Battre();
    }

    private static void AssertProche(string attendue, string obtenue, int seuil)
    {
        HexColor.TryParse(attendue, out _, out var r1, out var g1, out var b1);
        HexColor.TryParse(obtenue, out _, out var r2, out var g2, out var b2);
        var ecart = new[] { Math.Abs(r1 - r2), Math.Abs(g1 - g2), Math.Abs(b1 - b2) }.Max();
        Assert.True(ecart <= seuil, $"{obtenue} au lieu de {attendue} (écart {ecart})");
    }
}
