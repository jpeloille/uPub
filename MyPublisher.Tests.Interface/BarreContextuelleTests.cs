using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyPublisher.Converters;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// La barre contextuelle — poste I3 : sous la barre principale, elle suit la sélection, et
/// reçoit la mise en forme du texte que le dock portait — la police, le corps, le gras,
/// l'italique, le souligné, les quatre alignements, l'interligne, la couleur du texte —,
/// qui quitte le dock (règle 1). Chaque geste y fait un pas d'annulation (règle 2), et la
/// planche ne bouge pas quand elle change (règle 3).
/// </summary>
[Trait("poste", "I3")]
public class BarreContextuelleTests
{
    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static BarreContextuelle Barre(Window fenetre) => Trouver<BarreContextuelle>(fenetre, "BarreContextuelle");

    /// <summary>Le bouton de la barre qui porte cette bulle d'aide, s'il se voit.</summary>
    private static T? Bouton<T>(Window fenetre, string bulle) where T : Control =>
        Barre(fenetre).GetVisualDescendants().OfType<T>()
            .SingleOrDefault(b => Equals(ToolTip.GetTip(b), bulle) && b.IsEffectivelyVisible);

    private static Point Origine(Window fenetre) =>
        Trouver<Panel>(fenetre, "PageRoot").TranslatePoint(new Point(0, 0), fenetre) ?? throw new InvalidOperationException();

    private static void Quitter(Window fenetre)
    {
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
    }

    private static TextFrame Texte(string nom, double x, double y, string texte = "Texte") => new()
    {
        Name = nom, X = x, Y = y, Width = 300, Height = 80, Text = texte,
        FontFamily = "DejaVu Sans", FontSize = 10.5, Foreground = "#FF000000",
    };

    /// <summary>Un tableau d'une case, comme les tests de Q20 le bâtissent.</summary>
    private static TableFrame Tableau(double x, double y)
    {
        var tableau = new TableFrame { X = x, Y = y, FontFamily = "DejaVu Sans" };
        tableau.Columns.Add(new TableColumn(100));
        tableau.Rows.Add(new TableRow(30));
        tableau.Cells.Add(new TableCell { Row = 0, Column = 0, Text = "x" });
        return tableau;
    }

    private static string EnPoints(double corps) => corps.ToString("0.##", CultureInfo.CurrentCulture) + " pt";

    // ----------------------------------------------------------- ce que la barre montre

    [AvaloniaFact]
    public void Un_bloc_de_texte_montre_sa_police_son_corps_et_son_alignement()
    {
        var bloc = Texte("Texte", 60, 60);
        bloc.Align = TextAlign.Justify;
        bloc.Bold = true;
        bloc.Foreground = "scheme:accent2";
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var police = Trouver<ComboBox>(Barre(fenetre), "PoliceDuBloc");
        Assert.Same(bloc, police.DataContext);
        Assert.Equal("DejaVu Sans", police.SelectedItem);
        var corps = Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc");
        Assert.Equal(EnPoints(10.5), corps.Text);
        Assert.Equal(Unite.Multiple.ConvertBack((decimal)bloc.LineHeight, typeof(string), null, CultureInfo.CurrentCulture),
            Trouver<NumericUpDown>(Barre(fenetre), "InterligneDuBloc").Text);

        // Les quatre alignements, la droite comprise que la maquette omet : seul le sien est coché.
        foreach (var (bulle, alignement) in new[]
                 {
                     ("Aligner le texte à gauche", TextAlign.Left), ("Centrer le texte", TextAlign.Center),
                     ("Aligner le texte à droite", TextAlign.Right), ("Justifier", TextAlign.Justify),
                 })
        {
            var bouton = Bouton<ToggleButton>(fenetre, bulle) ?? throw new InvalidOperationException(bulle);
            Assert.Equal(alignement == TextAlign.Justify, bouton.IsChecked);
        }

        Assert.True(Bouton<ToggleButton>(fenetre, "Gras (Ctrl+G)")!.IsChecked);
        Assert.False(Bouton<ToggleButton>(fenetre, "Italique (Ctrl+I)")!.IsChecked);
        Assert.False(Bouton<ToggleButton>(fenetre, "Souligné (Ctrl+U)")!.IsChecked);

        // La vignette de la couleur montre la couleur du jeu que le bloc désigne.
        var vignette = Trouver<Button>(Barre(fenetre), "CouleurDuTexte").GetVisualDescendants().OfType<Border>()
            .First(b => b.Background is ISolidColorBrush);
        Assert.Equal(Color.Parse(vm.Document.Charte.Couleur("scheme:accent2")!),
            ((ISolidColorBrush)vignette.Background!).Color);

        // Lire n'est pas écrire.
        Assert.False(vm.CanUndo, "montrer la mise en forme a écrit dans le document");
    }

    [AvaloniaFact]
    public void Un_clic_sur_centre_centre_le_bloc_a_l_ecran_et_au_pdf_en_un_pas()
    {
        var bloc = Texte("Centre", 100, 100, "Milieu");
        bloc.FontSize = 24;
        bloc.Width = 360;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);
        var milieu = bloc.X + bloc.Width / 2;
        Assert.True(MilieuDeLEncre(Rendu.Encre(fenetre), bloc) < milieu - 60, "le texte n'était pas à gauche");

        Rendu.Cliquer(Bouton<ToggleButton>(fenetre, "Centrer le texte")!);

        Assert.Equal(TextAlign.Center, bloc.Align);
        Assert.True(Bouton<ToggleButton>(fenetre, "Centrer le texte")!.IsChecked);
        Assert.False(Bouton<ToggleButton>(fenetre, "Aligner le texte à gauche")!.IsChecked);
        // L'écran : l'encre du mot se pose au milieu du bloc, au lissage près.
        Assert.InRange(MilieuDeLEncre(Rendu.Encre(fenetre), bloc), milieu - 2, milieu + 2);

        // Le PDF : le mot que pdftotext y lit est centré dans le bloc.
        var dossier = Path.Combine(Path.GetTempPath(), $"mypublisher-i3-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dossier);
        try
        {
            var pdf = Path.Combine(dossier, "centre.pdf");
            vm.ExportPdf(pdf);
            var (xMin, xMax) = MotDuPdf(pdf, "Milieu");
            Assert.InRange((xMin + xMax) / 2, milieu - 2, milieu + 2);
        }
        finally
        {
            Directory.Delete(dossier, recursive: true);
        }

        Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "Centrer le texte"));
    }

    /// <summary>L'abscisse, en points, du milieu de l'encre que le texte du bloc laisse sur la page.</summary>
    private static double MilieuDeLEncre(Rendu.Image image, Frame bloc)
    {
        var p = Rendu.PixelsParPoint;
        int gauche = int.MaxValue, droite = int.MinValue;
        for (var y = (int)(bloc.Y * p); y < (int)((bloc.Y + bloc.Height) * p); y++)
        {
            for (var x = (int)(bloc.X * p); x < (int)((bloc.X + bloc.Width) * p); x++)
            {
                if (!Rendu.EstEncre(image, x, y)) continue;
                gauche = Math.Min(gauche, x);
                droite = Math.Max(droite, x);
            }
        }

        Assert.True(gauche <= droite, "aucune encre dans le bloc");
        return (gauche + droite + 1) / 2.0 / p;
    }

    /// <summary>La boîte horizontale d'un mot de la première page du PDF, lue par « pdftotext -bbox ».</summary>
    private static (double XMin, double XMax) MotDuPdf(string pdf, string mot)
    {
        var html = Path.ChangeExtension(pdf, ".html");
        try
        {
            var demarrage = new ProcessStartInfo("pdftotext")
            {
                ArgumentList = { "-bbox", "-f", "1", "-l", "1", pdf, html },
                RedirectStandardError = true,
            };
            using var processus = Process.Start(demarrage)!;
            processus.WaitForExit(60_000);
            Assert.True(processus.ExitCode == 0, processus.StandardError.ReadToEnd());
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Assert.Fail("« pdftotext » est introuvable : sudo apt install poppler-utils.");
            throw;
        }

        var trouve = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(html),
            "<word xMin=\"([-\\d.]+)\" yMin=\"[-\\d.]+\" xMax=\"([-\\d.]+)\" yMax=\"[-\\d.]+\">" + mot + "</word>");
        Assert.True(trouve.Success, $"« {mot} » n'est pas dans le PDF");
        return (double.Parse(trouve.Groups[1].Value, CultureInfo.InvariantCulture),
            double.Parse(trouve.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    // ----------------------------------------------------------------- règle 2

    [AvaloniaFact]
    public void Chaque_controle_de_la_barre_fait_un_seul_pas()
    {
        var bloc = Texte("Texte", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        // La police : la liste a le focus le temps du choix.
        var police = Trouver<ComboBox>(Barre(fenetre), "PoliceDuBloc");
        police.Focus();
        police.SelectedItem = vm.FontFamilies.First(f => f != "DejaVu Sans" && !f.StartsWith("scheme:"));
        Rendu.Battre();
        Quitter(fenetre);
        Assert.NotEqual("DejaVu Sans", bloc.FontFamily);
        bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "police"));

        Rendu.Saisir(fenetre, Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc"), "14");
        Assert.Equal(14, bloc.FontSize);
        bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "corps"));

        Rendu.Saisir(fenetre, Trouver<NumericUpDown>(Barre(fenetre), "InterligneDuBloc"), "1.5");
        Assert.Equal(1.5, bloc.LineHeight, precision: 9);
        bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "interligne"));

        // La couleur : la vignette ouvre le sélecteur, dont une pastille écrit la référence.
        Rendu.Cliquer(Trouver<Button>(Barre(fenetre), "CouleurDuTexte"));
        var selecteur = SelecteurOuvert(fenetre);
        Assert.Same(bloc, selecteur.DataContext);
        Rendu.Cliquer(selecteur.GetVisualDescendants().OfType<Button>().Single(b => b.Tag is EmplacementDeCouleur.Accent1));
        Rendu.Cliquer(selecteur.GetVisualDescendants().OfType<Button>().Single(b => b.Tag is EmplacementDeCouleur.Accent3));
        Assert.Equal("scheme:accent3", bloc.Foreground);
        // Le passage par le sélecteur est un geste : son pas se fait à sa fermeture, et les
        // deux pastilles essayées n'en font qu'un.
        Assert.True(vm.PeutAnnuler, "Annuler s'est grisé pendant le choix de la couleur");
        Assert.IsType<Flyout>(Trouver<Button>(Barre(fenetre), "CouleurDuTexte").Flyout).Hide();
        Rendu.Battre();
        bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "couleur du texte"));

        // Les boutons : la commande du menu Texte, un pas chacun.
        foreach (var (bulle, verifier) in new (string, Func<TextFrame, bool>)[]
                 {
                     ("Gras (Ctrl+G)", b => b.Bold), ("Italique (Ctrl+I)", b => b.Italic),
                     ("Souligné (Ctrl+U)", b => b.Underline == Souligne.Simple),
                     ("Aligner le texte à droite", b => b.Align == TextAlign.Right),
                 })
        {
            Rendu.Cliquer(Bouton<ToggleButton>(fenetre, bulle)!);
            Assert.True(verifier(bloc), bulle);
            bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, bulle));
        }
    }

    [AvaloniaFact]
    public void Changer_de_bloc_sous_le_selecteur_ouvert_referme_son_pas()
    {
        // Le sélecteur de couleur tient sa transaction ouverte tant qu'il l'est. Un clic sur
        // un autre bloc le referme avec elle : la couleur fait son pas, et le geste suivant
        // le sien — une transaction restée ouverte les fondrait tous dans le premier.
        var a = Texte("A", 60, 60);
        var b = Texte("B", 60, 200);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b));
        vm.SelectOnly(a);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        Rendu.Cliquer(Trouver<Button>(Barre(fenetre), "CouleurDuTexte"));
        Rendu.Cliquer(SelecteurOuvert(fenetre).GetVisualDescendants().OfType<Button>().Single(x => x.Tag is EmplacementDeCouleur.Accent2));
        Assert.Equal("scheme:accent2", a.Foreground);

        var surB = Origine(fenetre) + new Point(b.X + 20, b.Y + 20);
        fenetre.MouseDown(surB, MouseButton.Left);
        fenetre.MouseUp(surB, MouseButton.Left);
        Rendu.Battre();
        if (!ReferenceEquals(vm.SingleSelected, b))
        {
            // Le premier clic n'a fait que refermer le sélecteur : le second prend le bloc.
            fenetre.MouseDown(surB, MouseButton.Left);
            fenetre.MouseUp(surB, MouseButton.Left);
            Rendu.Battre();
        }

        Assert.Same(b, vm.SingleSelected);
        Assert.False(Assert.IsType<Flyout>(Trouver<Button>(Barre(fenetre), "CouleurDuTexte").Flyout).IsOpen);
        fenetre.KeyPressQwerty(PhysicalKey.G, RawInputModifiers.Control);
        Rendu.Battre();
        Assert.True(b.Bold);

        TextFrame Bloc(string nom) => vm.CurrentPage!.Frames.OfType<TextFrame>().Single(x => x.Name == nom);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(("scheme:accent2", false), (Bloc("A").Foreground, Bloc("B").Bold));
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, "deux gestes, plus de deux pas");
    }

    [AvaloniaFact]
    public void Changer_de_bloc_sans_le_pointeur_referme_aussi_le_selecteur()
    {
        // Écrit à la relecture de Q23, le 26 octobre 2026. Le test précédent change de bloc
        // au pointeur, et le premier clic referme le volet de lui-même : il passait sans
        // l'écoute du bloc de « ReglagesDuBloc », que Q23 a reprise de « ContentControl ».
        // C'est elle qui le referme quand la sélection change sans clic — un raccourci, une
        // annulation, la liste des blocs. Sans elle, le volet restait ouvert sur le bloc
        // d'après, et la transaction de la couleur engloutissait le geste suivant.
        var a = Texte("A", 60, 60);
        var b = Texte("B", 60, 200);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b));
        vm.SelectOnly(a);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        Rendu.Cliquer(Trouver<Button>(Barre(fenetre), "CouleurDuTexte"));
        Rendu.Cliquer(SelecteurOuvert(fenetre).GetVisualDescendants().OfType<Button>().Single(x => x.Tag is EmplacementDeCouleur.Accent2));
        Assert.Equal("scheme:accent2", a.Foreground);

        vm.SelectOnly(b);
        Rendu.Battre();
        Assert.False(Assert.IsType<Flyout>(Trouver<Button>(Barre(fenetre), "CouleurDuTexte").Flyout).IsOpen,
            "le sélecteur est resté ouvert sur le bloc d'après");

        // Un geste du modèle de vue, qui ouvre et ferme sa propre transaction : il fait son pas.
        vm.BeginEdit();
        b.Bold = true;
        vm.CommitEdit();

        TextFrame Bloc(string nom) => vm.CurrentPage!.Frames.OfType<TextFrame>().Single(x => x.Name == nom);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(("scheme:accent2", false), (Bloc("A").Foreground, Bloc("B").Bold));
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, "deux gestes, plus de deux pas");
    }

    /// <summary>Le sélecteur de couleur que la vignette de la barre vient d'ouvrir, dans sa racine à lui.</summary>
    private static SelecteurDeCouleur SelecteurOuvert(Window fenetre)
    {
        var bouton = Trouver<Button>(Barre(fenetre), "CouleurDuTexte");
        var volet = Assert.IsType<Flyout>(bouton.Flyout);
        Assert.True(volet.IsOpen, "la vignette n'ouvre pas le sélecteur");
        return Assert.IsType<SelecteurDeCouleur>(volet.Content);
    }

    [AvaloniaFact]
    public void Deux_clics_font_deux_pas_et_un_bouton_coche_le_reste()
    {
        var bloc = Texte("Texte", 60, 60);
        bloc.Align = TextAlign.Center;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        // « Centrer » sur un texte centré : rien ne change, et le bouton reste coché — un
        // bouton à bascule ordinaire se serait décoché tout seul.
        var centre = Bouton<ToggleButton>(fenetre, "Centrer le texte")!;
        Rendu.Cliquer(centre);
        Assert.True(centre.IsChecked);
        Assert.Equal(TextAlign.Center, bloc.Align);
        Assert.False(vm.CanUndo, "réécrire l'alignement déjà là a fait un pas");

        // Deux clics sur G : deux pas, et le bouton suit le bloc à chacun.
        var gras = Bouton<ToggleButton>(fenetre, "Gras (Ctrl+G)")!;
        Rendu.Cliquer(gras);
        Assert.True(gras.IsChecked);
        Rendu.Cliquer(gras);
        Assert.False(gras.IsChecked);
        Assert.False(bloc.Bold);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
        Assert.True(bloc.Bold);
        Assert.True(Bouton<ToggleButton>(fenetre, "Gras (Ctrl+G)")!.IsChecked);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.False(vm.CanUndo, "deux clics, plus de deux pas");

        // Le bouton ne prend pas le focus : un clic laisse le curseur où il était.
        Assert.False(Bouton<ToggleButton>(fenetre, "Gras (Ctrl+G)")!.Focusable);
    }

    [AvaloniaFact]
    public void Le_souligne_a_son_bouton_ctrl_u_le_menu_texte_et_ses_sortes()
    {
        var bloc = Texte("Texte", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);
        var bouton = Bouton<ToggleButton>(fenetre, "Souligné (Ctrl+U)")!;

        // Ctrl+U : le trait simple, et le bouton coché.
        fenetre.KeyPressQwerty(PhysicalKey.U, RawInputModifiers.Control);
        Rendu.Battre();
        Assert.Equal(Souligne.Simple, bloc.Underline);
        Assert.True(bouton.IsChecked);
        bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "Ctrl+U"));
        Assert.False(Bouton<ToggleButton>(fenetre, "Souligné (Ctrl+U)")!.IsChecked);

        // Le menu Texte porte « Souligné » sur la même commande.
        var menu = fenetre.GetVisualDescendants().OfType<MenuItem>().Single(m => Equals(m.Header, "_Texte"));
        var entree = menu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "_Souligné"));
        Assert.Equal(KeyGesture.Parse("Ctrl+U"), entree.InputGesture);
        Assert.Same(vm.SoulignerCommand, entree.Command);

        // Les sortes : le chevron ouvre le menu, « Double » pose le double en un pas, et
        // l'entrée est cochée ; le bouton, lui, bascule — il retire le double d'un clic.
        var sortes = Assert.IsType<MenuFlyout>(Trouver<Button>(Barre(fenetre), "SortesDuSouligne").Flyout);
        Rendu.Cliquer(Trouver<Button>(Barre(fenetre), "SortesDuSouligne"));
        Assert.True(sortes.IsOpen, "le chevron n'ouvre pas les sortes");
        var entrees = sortes.Items.OfType<MenuItem>().ToList();
        Assert.Equal(new[] { "Trait simple", "Double", "Épais", "Mots seuls" }, entrees.Select(e => e.Header as string));
        Rendu.Cliquer(entrees[1]);
        Assert.Equal(new Souligne(TraitDeSouligne.Solid, @double: true), bloc.Underline);
        Assert.True(Bouton<ToggleButton>(fenetre, "Souligné (Ctrl+U)")!.IsChecked);
        Assert.True(vm.CanUndo);
        // Rouvert, le menu coche la sorte du bloc.
        Rendu.Cliquer(Trouver<Button>(Barre(fenetre), "SortesDuSouligne"));
        Assert.Equal(new bool?[] { false, true, false, false }, entrees.Select(e => (bool?)e.IsChecked));
        sortes.Hide();
        Rendu.Battre();

        Rendu.Cliquer(Bouton<ToggleButton>(fenetre, "Souligné (Ctrl+U)")!);
        Assert.Equal(Souligne.Aucun, bloc.Underline);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "souligné double"));

        // Les trois autres sortes, chacune la sienne.
        foreach (var (sorte, attendu) in new[]
                 {
                     (SorteDeSouligne.Epais, new Souligne(TraitDeSouligne.Solid, thick: true)),
                     (SorteDeSouligne.MotsSeuls, new Souligne(TraitDeSouligne.Solid, wordsOnly: true)),
                     (SorteDeSouligne.Simple, Souligne.Simple),
                 })
        {
            vm.SoulignerEnCommand.Execute(sorte);
            Rendu.Battre();
            Assert.Equal(attendu, bloc.Underline);
            bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, sorte.ToString()));
        }
    }

    [AvaloniaFact]
    public void Ctrl_U_pendant_une_saisie_de_la_barre_fait_son_propre_pas()
    {
        var bloc = Texte("Texte", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var saisie = Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc").GetVisualDescendants().OfType<TextBox>().First();
        saisie.Focus();
        Rendu.Battre();
        saisie.SelectAll();
        fenetre.KeyTextInput("14");
        fenetre.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Rendu.Battre();
        fenetre.KeyPressQwerty(PhysicalKey.U, RawInputModifiers.Control);
        Rendu.Battre();
        Assert.True(saisie.IsFocused);
        Assert.True(bloc.Underline.Actif);
        Quitter(fenetre);

        TextFrame Bloc() => Assert.IsType<TextFrame>(vm.CurrentPage!.Frames[0]);
        foreach (var (corps, souligne) in new[] { (14.0, false), (10.5, false) })
        {
            Assert.True(vm.CanUndo, $"il manque un pas avant {corps} pt");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal((corps, souligne), (Bloc().FontSize, Bloc().Underline.Actif));
        }

        Assert.False(vm.CanUndo, "deux gestes, plus de deux pas");
    }

    [AvaloniaFact]
    public void Sans_bloc_de_texte_le_souligne_ne_s_offre_pas()
    {
        // Le menu Texte n'offre rien sans bloc de texte : le gras et l'italique s'y grisent
        // sans un bloc qui porte du texte, les alignements sans un bloc de texte. Repris le
        // 26 octobre 2026, après relecture : « Souligné », qui y est entré avec la barre,
        // restait actif sur un tableau, une forme ou sans sélection, où il ne fait rien.
        var bloc = Texte("Texte", 60, 60);
        var tableau = Tableau(60, 300);
        var forme = new ShapeFrame { Name = "Forme", X = 300, Y = 60, Width = 40, Height = 40 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, tableau, forme));
        var menus = Trouver<Menu>(fenetre, "Menus");
        var menu = menus.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "_Texte"));
        var entree = menu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "_Souligné"));

        // Une entrée de menu fermé ne relit pas sa commande : le menu est ouvert pour la lire.
        bool Offert()
        {
            Rendu.Cliquer(menu);
            Assert.True(entree.IsEffectivelyVisible, "le menu Texte ne s'est pas ouvert");
            var offert = entree.IsEffectivelyEnabled;
            menus.Close();
            Rendu.Battre();
            return offert;
        }

        foreach (var (selection, quoi) in new (Frame?, string)[] { (tableau, "un tableau"), (forme, "une forme"), (null, "rien") })
        {
            vm.SelectOnly(selection);
            Rendu.Battre();
            Assert.False(vm.SoulignerCommand.CanExecute(null), $"« Souligné » s'offre sur {quoi}");
            Assert.False(Offert(), $"« Souligné » est actif au menu Texte sur {quoi}");
        }

        // Le bloc de texte repris, l'entrée se rallume, et le bouton S de la barre — gardé
        // d'une sélection à l'autre (Q23) — se réactive avec la commande.
        vm.SelectOnly(bloc);
        Rendu.Battre();
        Assert.True(Offert(), "« Souligné » reste grisé au menu Texte sur un bloc de texte");
        Rendu.Cliquer(Bouton<ToggleButton>(fenetre, "Souligné (Ctrl+U)")!);
        Assert.Equal(Souligne.Simple, bloc.Underline);
    }

    [AvaloniaFact]
    public void Une_commande_de_la_barre_fait_son_pas_au_clic()
    {
        // Aligner, Répartir et Dissocier sont des commandes, comme celles de la barre
        // principale : chaque clic fait son pas, aussitôt. Repris le 26 octobre 2026, après
        // relecture : le bouton prenait le focus, la fenêtre ouvrait à ce focus la transaction
        // de la barre (règle 2), et le pas de l'alignement attendait que le focus quitte la
        // barre — la flèche frappée entre-temps s'y fondait, et une annulation défaisait les
        // deux gestes à la fois.
        var a = new ShapeFrame { Name = "A", X = 300, Y = 60, Width = 40, Height = 40 };
        var b = new ShapeFrame { Name = "B", X = 360, Y = 80, Width = 40, Height = 40 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b));
        vm.SetSelection([a, b]);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);
        Button Commande(string bulle) =>
            Barre(fenetre).GetVisualDescendants().OfType<Button>().Single(x => Equals(ToolTip.GetTip(x), bulle) && x.IsEffectivelyVisible);
        ShapeFrame Bloc(string nom) => vm.CurrentPage!.Frames.OfType<ShapeFrame>().Single(x => x.Name == nom);

        Rendu.Cliquer(Commande("Aligner en haut"));
        var haut = a.Y;
        Assert.Equal(haut, b.Y);
        Assert.True(vm.CanUndo, "« Aligner en haut » n'a pas fait son pas au clic");
        fenetre.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Rendu.Battre();
        Assert.Equal(303, a.X);

        // Deux gestes, deux pas : la flèche s'annule seule, puis l'alignement.
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal((300.0, haut, haut), (Bloc("A").X, Bloc("A").Y, Bloc("B").Y));
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, "deux gestes, plus de deux pas");

        // Un champ du dock en pleine saisie — la marge gauche, à l'onglet Document — ne
        // laisse pas la commande se fondre dans son pas : il rend le focus avant qu'elle joue.
        vm.SetSelection([Bloc("A"), Bloc("B")]);
        Rendu.Onglet(fenetre, "Document");
        var marge = Trouver<NumericUpDown>(fenetre, "MargeGauche").GetVisualDescendants().OfType<TextBox>().First();
        marge.Focus();
        Rendu.Battre();
        marge.SelectAll();
        fenetre.KeyTextInput("25");
        fenetre.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Rendu.Battre();
        var margeSaisie = vm.Document.MarginLeft;
        Assert.NotEqual(Document.CreateDefault().MarginLeft, margeSaisie);
        Rendu.Cliquer(Commande("Aligner à gauche"));
        Assert.Equal(Bloc("A").X, Bloc("B").X, precision: 9);
        Assert.True(vm.CanUndo, "« Aligner à gauche » n'a pas fait son pas au clic");
        Quitter(fenetre);

        var aligne = Bloc("A").X;
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal((margeSaisie, 300.0, 360.0), (vm.Document.MarginLeft, Bloc("A").X, Bloc("B").X));
        Assert.NotEqual(300, aligne);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, "la marge et l'alignement, plus de deux pas");
    }

    // ----------------------------------------------------------------- règle 1

    [AvaloniaFact]
    public void Chaque_propriete_n_a_qu_un_controle_visible()
    {
        var bloc = Texte("Texte", 60, 60);
        var tableau = Tableau(60, 300);
        // Une couleur que rien d'autre ne porte : le sélecteur qui la montre est celui du texte.
        bloc.Foreground = "#FF123457";
        tableau.Foreground = "#FF123457";
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, tableau));

        foreach (var frame in new Frame[] { bloc, tableau })
        {
            vm.SelectOnly(frame);
            Rendu.Battre();
            var visibles = fenetre.GetVisualDescendants().OfType<Control>()
                .Where(c => c.IsEffectivelyVisible && ReferenceEquals(c.DataContext, frame)).ToList();
            var dock = Trouver<Border>(fenetre, "Dock");

            // La police (Q20, C5b), le corps et l'interligne : un champ chacun, dans la barre.
            var polices = visibles.OfType<ComboBox>().Where(c => ReferenceEquals(c.ItemsSource, vm.FontFamilies)).ToList();
            Assert.Single(polices);
            Assert.True(polices[0].GetVisualAncestors().Contains(Barre(fenetre)), "la police n'est pas dans la barre");
            var corps = (decimal)(frame is TextFrame t ? t.FontSize : ((TableFrame)frame).FontSize);
            var auDock = dock.GetVisualDescendants().OfType<NumericUpDown>()
                .Where(n => n.IsEffectivelyVisible && ReferenceEquals(n.DataContext, frame)).ToList();
            // Le dock garde des champs en points — l'épaisseur des filets d'un tableau —, mais
            // plus le corps ; et l'interligne était son seul champ en multiple.
            Assert.DoesNotContain(auDock, n => ReferenceEquals(n.TextConverter, Unite.Points) && n.Value == corps);
            Assert.DoesNotContain(auDock, n => ReferenceEquals(n.TextConverter, Unite.Multiple));
            Assert.Single(visibles.OfType<NumericUpDown>(), n => ReferenceEquals(n.TextConverter, Unite.Multiple));

            // Le gras, l'italique : un bouton chacun, dans la barre.
            foreach (var bulle in new[] { "Gras (Ctrl+G)", "Italique (Ctrl+I)" })
            {
                Assert.Single(fenetre.GetVisualDescendants().OfType<ToggleButton>(),
                    t => Equals(ToolTip.GetTip(t), bulle) && t.IsEffectivelyVisible);
            }

            // La couleur du texte : plus aucun sélecteur au dock ; celui de la barre, une fois ouvert, est seul.
            Assert.DoesNotContain(dock.GetVisualDescendants().OfType<SelecteurDeCouleur>(),
                s => s.IsEffectivelyVisible && s.Valeur == "#FF123457");
            var vignette = Barre(fenetre).GetVisualDescendants().OfType<Button>()
                .Single(b => Equals(ToolTip.GetTip(b), "Couleur du texte") && b.IsEffectivelyVisible);
            Rendu.Cliquer(vignette);
            var selecteur = Assert.IsType<SelecteurDeCouleur>(Assert.IsType<Flyout>(vignette.Flyout).Content);
            Assert.Same(frame, selecteur.DataContext);
            Assert.Equal(frame is TextFrame texte ? texte.Foreground : ((TableFrame)frame).Foreground, selecteur.Valeur);
            Assert.DoesNotContain(fenetre.GetVisualDescendants().OfType<SelecteurDeCouleur>(),
                s => ReferenceEquals(s.DataContext, frame) && s.Valeur == selecteur.Valeur && !ReferenceEquals(s, selecteur));
            ((Flyout)vignette.Flyout!).Hide();
            Rendu.Battre();

            // L'alignement d'un bloc de texte n'est plus une liste du dock.
            Assert.DoesNotContain(dock.GetVisualDescendants().OfType<ComboBox>(),
                c => c.IsEffectivelyVisible && ReferenceEquals(c.DataContext, frame)
                                            && ReferenceEquals(c.ItemsSource, vm.TextAlignments));
        }

        // L'angle (Q14) : un champ.
        vm.SelectOnly(bloc);
        Rendu.Battre();
        Assert.Single(fenetre.GetVisualDescendants().OfType<NumericUpDown>(),
            n => n.IsEffectivelyVisible && n.Minimum == -360 && n.Maximum == 360);
        Assert.False(vm.CanUndo, "parcourir la barre a écrit dans le document");
    }

    [AvaloniaFact]
    public void Un_tableau_a_sa_mise_en_forme_sans_alignement_ni_souligne()
    {
        var tableau = Tableau(60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        vm.SelectOnly(tableau);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        Assert.NotNull(Bouton<ToggleButton>(fenetre, "Gras (Ctrl+G)"));
        Assert.NotNull(Bouton<NumericUpDown>(fenetre, "Corps"));
        Assert.NotNull(Bouton<NumericUpDown>(fenetre, "Interligne"));
        Assert.Null(Bouton<ToggleButton>(fenetre, "Souligné (Ctrl+U)"));
        Assert.Null(Bouton<ToggleButton>(fenetre, "Centrer le texte"));

        Rendu.Cliquer(Bouton<ToggleButton>(fenetre, "Italique (Ctrl+I)")!);
        Assert.True(tableau.Italic);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.False(vm.CanUndo, "l'italique du tableau a fait plus d'un pas");
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
    }

    // ----------------------------------------------------------------- règle 3

    [AvaloniaFact]
    public void Passer_d_un_texte_a_une_image_change_la_barre_sans_deplacer_la_planche()
    {
        var texte = Texte("Texte", 60, 60);
        var image = new ImageFrame { Name = "Image", X = 60, Y = 220, Width = 120, Height = 90, Path = "/nulle/part.png" };
        var a = new ShapeFrame { Name = "A", X = 300, Y = 60, Width = 40, Height = 40 };
        var b = new ShapeFrame { Name = "B", X = 360, Y = 60, Width = 40, Height = 40 };
        var groupe = new GroupFrame { Name = "Groupe", X = 300, Y = 220, Width = 100, Height = 40 };
        groupe.Children.Add(new ShapeFrame { X = 300, Y = 220, Width = 40, Height = 40 });
        groupe.Children.Add(new ShapeFrame { X = 360, Y = 220, Width = 40, Height = 40 });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(texte, image, a, b, groupe));
        var origine = Origine(fenetre);
        var barre = Barre(fenetre);
        var pastille = Trouver<Border>(fenetre, "PastilleDeLaSelection").GetVisualDescendants().OfType<TextBlock>().Single();

        void Voir(string nature, bool police, bool commandes, bool dissocier)
        {
            Assert.Equal(nature, pastille.Text);
            Assert.Equal(police, Bouton<ComboBox>(fenetre, "Police") is not null);
            Assert.Equal(commandes, Trouver<StackPanel>(barre, "CommandesDeLaSelection").IsEffectivelyVisible);
            Assert.Equal(dissocier, Bouton<Button>(fenetre, "Dissocier (Ctrl+Maj+H)") is not null);
            Assert.Equal(origine, Origine(fenetre));
            Assert.Equal(41, barre.Bounds.Height);
        }

        // Au pointeur, du texte à l'image : la pression qui sélectionne, puis le relâchement.
        foreach (var (bloc, nature, police) in new (Frame, string, bool)[] { (texte, "Bloc de texte", true), (image, "Image", false) })
        {
            var point = origine + new Point(bloc.X + 20, bloc.Y + 20);
            fenetre.MouseDown(point, MouseButton.Left);
            Rendu.Battre();
            Assert.Same(bloc, vm.SingleSelected);
            Voir(nature, police, commandes: false, dissocier: false);
            fenetre.MouseUp(point, MouseButton.Left);
            Rendu.Battre();
            Voir(nature, police, commandes: false, dissocier: false);
        }

        vm.SetSelection([a, b]);
        Rendu.Battre();
        Voir("2 blocs", police: false, commandes: true, dissocier: false);
        // Aligner en haut, d'un clic dans la barre : les deux blocs ensemble, en un pas.
        b.Y = 80;
        Rendu.Cliquer(barre.GetVisualDescendants().OfType<Button>().Single(x => Equals(ToolTip.GetTip(x), "Aligner en haut")));
        Assert.Equal(a.Y, b.Y);

        vm.SelectOnly(groupe);
        Rendu.Battre();
        Voir("Groupe", police: false, commandes: false, dissocier: true);
        Rendu.Cliquer(Bouton<Button>(fenetre, "Dissocier (Ctrl+Maj+H)")!);
        Assert.DoesNotContain(groupe, vm.CurrentPage!.Frames);

        vm.SelectOnly(null);
        Rendu.Battre();
        Voir("Page", police: false, commandes: false, dissocier: false);
    }

    // ------------------------------------------------------------- les lettres seules

    [AvaloniaFact]
    public void T_dans_le_corps_s_ecrit_et_T_apres_Echap_arme_l_outil_texte()
    {
        var bloc = Texte("Texte", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        var champ = Trouver<NumericUpDown>(Barre(fenetre), "CorpsDuBloc");
        var saisie = champ.GetVisualDescendants().OfType<TextBox>().First();
        saisie.Focus();
        Rendu.Battre();
        saisie.SelectAll();
        fenetre.KeyTextInput("12");
        fenetre.KeyPressQwerty(PhysicalKey.T, RawInputModifiers.None);
        fenetre.KeyTextInput("T");
        Rendu.Battre();
        Assert.Equal("12T", saisie.Text);
        Assert.Equal(EditorTool.Select, vm.ActiveTool);

        // Échap quitte le champ ; « T » arme alors l'outil texte, et la sélection reste.
        saisie.SelectAll();
        fenetre.KeyTextInput("12");
        fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Rendu.Battre();
        Assert.False(saisie.IsFocused, "Échap n'a pas quitté le champ");
        Assert.Same(bloc, vm.SingleSelected);
        fenetre.KeyPressQwerty(PhysicalKey.T, RawInputModifiers.None);
        Rendu.Battre();
        Assert.Equal(EditorTool.Text, vm.ActiveTool);

        // Ce qu'on avait tapé est validé, en un pas.
        Assert.Equal(12, bloc.FontSize);
        Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "corps quitté par Échap"));
    }
}
