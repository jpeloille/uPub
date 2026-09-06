using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les gestes, joués sur la vraie couche de saisie de la vraie fenêtre.
///
/// C'est le harnais que les postes C2a, C2b et C3c ont monté puis jeté trois fois.
/// Sur ce poste — session Wayland, où une copie d'écran d'une fenêtre Avalonia sort
/// noire — c'est aussi le seul moyen de vérifier ce que l'application affiche.
/// </summary>
public class GestesTests
{
    /// <summary>
    /// Une fenêtre sur un document neuf, sans lire la configuration de la machine ni
    /// l'horloge : un banc qui dépend de l'un ou de l'autre ne prouve rien deux jours
    /// de suite.
    /// </summary>
    private static (Window Fenetre, MainWindowViewModel Vm) Ouvrir(Document document)
    {
        var vm = new MainWindowViewModel(
            contacts: new BusinessInfoStore(),
            horloge: () => new DateTime(2026, 9, 6));

        vm.Document = document;
        vm.CurrentPage = document.Pages[0];
        vm.FilePath = "banc.mypub";   // évite l'ouverture du catalogue au démarrage
        vm.Zoom = 1 / Units.PointToDip;

        var fenetre = new MainWindow { DataContext = vm, Width = 1400, Height = 900 };
        fenetre.Show();
        Battre();
        return (fenetre, vm);
    }

    private static void Battre(int tours = 4)
    {
        for (var i = 0; i < tours; i++) Dispatcher.UIThread.RunJobs();
    }

    /// <summary>La couche de saisie transparente posée sur la page.</summary>
    private static Control Saisie(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Control>().First(c => c.Name == "InputLayer");

    private static Panel Page(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");

    /// <summary>Un point de la page, en points typographiques, dans le repère fenêtre.</summary>
    private static Point SurLaPage(Window fenetre, double x, double y)
    {
        var page = Page(fenetre);
        var origine = page.TranslatePoint(new Point(0, 0), fenetre) ?? new Point(0, 0);
        return new Point(origine.X + x, origine.Y + y);
    }

    private static Document DocumentAvecUnBloc(out ShapeFrame bloc)
    {
        var document = Document.CreateDefault();
        bloc = new ShapeFrame
        {
            Name = "Carré", X = 100, Y = 100, Width = 60, Height = 60, Fill = new SolidFill("#FFCC0000"),
        };
        document.Pages[0].Frames.Add(bloc);
        return document;
    }

    [AvaloniaFact]
    public void La_fenetre_rend_reellement_des_pixels()
    {
        // Le premier test à écrire, et celui qui garde tous les autres : si le rendu
        // est vide, chaque comparaison d'images passerait sans rien prouver.
        var document = DocumentAvecUnBloc(out _);
        var (fenetre, _) = Ouvrir(document);

        using var image = new RenderTargetBitmap(
            new PixelSize((int)(Page(fenetre).Bounds.Width * 2), (int)(Page(fenetre).Bounds.Height * 2)),
            new Vector(192, 192));
        image.Render(Page(fenetre));

        Assert.True(image.PixelSize.Width > 100);
        Assert.NotEqual(0, EncreRouge(image));
    }

    [AvaloniaFact]
    public void Un_bloc_se_deplace_a_la_souris()
    {
        var document = DocumentAvecUnBloc(out var bloc);
        var (fenetre, vm) = Ouvrir(document);

        vm.SelectOnly(bloc);
        Battre();

        var saisie = Saisie(fenetre);
        fenetre.MouseDown(SurLaPage(fenetre, 130, 130), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 230, 190));
        fenetre.MouseUp(SurLaPage(fenetre, 230, 190), MouseButton.Left);
        Battre();

        Assert.NotNull(saisie);
        Assert.Equal(200, bloc.X, precision: 0);
        Assert.Equal(160, bloc.Y, precision: 0);
    }

    [AvaloniaFact]
    public void Un_deplacement_sannule()
    {
        // L'annulation compare deux JSON : un geste qui ne change rien n'empile aucun
        // pas. Celui-ci change quelque chose, il doit donc se défaire exactement.
        var document = DocumentAvecUnBloc(out var bloc);
        var (fenetre, vm) = Ouvrir(document);

        vm.SelectOnly(bloc);
        Battre();

        // Relevé après la sélection, et non avant : sélectionner fait passer la
        // position par le panneau de propriétés, donc par un aller-retour en
        // millimètres qui laisse 7·10⁻¹⁴ pt derrière lui. C'est un résidu du
        // correctif de C2a, mesuré et sans effet — il ne salit pas le document —
        // mais il n'a rien à voir avec ce que ce test éprouve.
        var avant = DocumentSerializer.ToJson(vm.Document);

        fenetre.MouseDown(SurLaPage(fenetre, 130, 130), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, 230, 190));
        fenetre.MouseUp(SurLaPage(fenetre, 230, 190), MouseButton.Left);
        Battre();

        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Battre();

        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void Un_bloc_lache_pres_dune_colonne_sy_aimante()
    {
        // La recette de C2a, rendue rejouable : « quatre blocs lâchés à des endroits
        // différents à moins de 4 pt du bord de la deuxième colonne s'y posent tous
        // exactement ». Ici, un seul bloc et un seul bord — le calcul lui-même est
        // éprouvé sans fenêtre, dans MyPublisher.Tests.
        var document = DocumentAvecUnBloc(out var bloc);
        document.ColumnCount = 3;
        document.ColumnSpacing = 12;
        var (fenetre, vm) = Ouvrir(document);

        var colonne = document.VerticalGuides.OrderBy(g => Math.Abs(g - 250)).First();

        vm.SelectOnly(bloc);
        Battre();

        // On lâche à deux points du bord de colonne : l'aimantation doit finir le geste.
        var visee = colonne + 2;
        fenetre.MouseDown(SurLaPage(fenetre, bloc.X + 30, bloc.Y + 30), MouseButton.Left);
        fenetre.MouseMove(SurLaPage(fenetre, visee + 30, bloc.Y + 30));
        fenetre.MouseUp(SurLaPage(fenetre, visee + 30, bloc.Y + 30), MouseButton.Left);
        Battre();

        Assert.Equal(colonne, bloc.X, precision: 6);
    }

    /// <summary>Une bande à trois cases, comme celles des fiches ATR.</summary>
    private static TableFrame Bande()
    {
        var table = new TableFrame { Name = "Bande", X = 100, Y = 200 };
        foreach (var largeur in new[] { 120.0, 80.0, 100.0 }) table.Columns.Add(new TableColumn(largeur));
        table.Rows.Add(new TableRow(30));

        for (var colonne = 0; colonne < 3; colonne++)
        {
            table.Cells.Add(new TableCell
            {
                Row = 0, Column = colonne, Text = $"case {colonne + 1}", Stroke = "#FF404040"
            });
        }

        return table;
    }

    /// <summary>Les cases affichées, de gauche à droite, avec leur position sur la page.</summary>
    private static List<(TexteCompose Case, Point Position)> Cases(Window fenetre)
    {
        var page = Page(fenetre);
        return fenetre.GetVisualDescendants().OfType<TexteCompose>()
            .Select(c => (Case: c, Position: c.TranslatePoint(new Point(0, 0), page) ?? default))
            .OrderBy(item => item.Position.X)
            .ToList();
    }

    [AvaloniaFact]
    public void Les_cases_dun_tableau_se_posent_aux_bords_de_colonnes()
    {
        // Le PDF est mesuré par la recette du poste ; ici c'est l'écran qu'on mesure,
        // dans l'arbre visuel, comme les recettes de C2a et C3c l'ont fait. Le zoom
        // est réglé pour qu'une unité de mise en page vaille un point : les positions
        // relevées sont donc directement celles du modèle.
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Add(Bande());
        var (fenetre, _) = Ouvrir(document);

        var cases = Cases(fenetre);

        Assert.Equal(3, cases.Count);
        Assert.Equal([100, 220, 300], cases.Select(c => Math.Round(c.Position.X)));
        Assert.All(cases, c => Assert.Equal(200, Math.Round(c.Position.Y)));
        Assert.Equal([120, 80, 100], cases.Select(c => Math.Round(c.Case.Bounds.Width)));
        Assert.Equal(["case 1", "case 2", "case 3"], cases.Select(c => c.Case.Contenu));
    }

    [AvaloniaFact]
    public void Une_case_modifiee_se_redessine_meme_apres_lecture_dun_fichier()
    {
        // Le piège silencieux de ce poste : le sérialiseur ne remplit pas les
        // collections d'un bloc, il les remplace. Un abonnement pris au constructeur
        // ne survivrait donc pas à l'ouverture d'un document, et le tableau cesserait
        // de se redessiner — sans erreur, et seulement sur les documents lus, jamais
        // sur ceux qu'on vient de composer. D'où le passage par le fichier.
        var neuf = Document.CreateDefault();
        neuf.Pages[0].Frames.Add(Bande());
        var document = DocumentSerializer.FromJson(DocumentSerializer.ToJson(neuf));

        var (fenetre, _) = Ouvrir(document);
        var table = (TableFrame)document.Pages[0].Frames[0];

        table.Cells[1].Text = "modifiée";
        table.Columns[0].Width = 200;
        Battre();

        var cases = Cases(fenetre);
        Assert.Equal("modifiée", cases[1].Case.Contenu);
        Assert.Equal(300, Math.Round(cases[1].Position.X));
        // Les colonnes font la taille du bloc : l'élargir de 80 pt élargit le tableau.
        Assert.Equal(380, table.Width);

        // La grille passe par un autre chemin — le panneau écrit le contour de toutes
        // les cases à la fois — et retombe sur le même abonnement.
        table.HasGrid = false;
        Battre();
        Assert.All(Cases(fenetre), c => Assert.Null(c.Case.Bloc!.Stroke));
    }

    [AvaloniaFact]
    public void Le_panneau_edite_les_cases_du_tableau_selectionne()
    {
        // Une case ne se choisit pas sur la page — il n'y a pas de sélection à
        // l'intérieur d'un bloc — mais dans le panneau, case par case. Ce test garde
        // ce chemin-là : un gabarit de propriétés qui ne se lie plus n'afficherait
        // rien du tout, sans erreur.
        var document = Document.CreateDefault();
        var table = Bande();
        document.Pages[0].Frames.Add(table);
        var (fenetre, vm) = Ouvrir(document);

        vm.SelectOnly(table);
        Battre();

        // Le sélecteur de couleur de la case porte lui aussi des champs de texte, et
        // il hérite du même contexte : on vise la classe, pas le type.
        var champs = fenetre.GetVisualDescendants().OfType<TextBox>()
            .Where(t => t.DataContext is TableCell && t.Classes.Contains("cellText"))
            .ToList();

        Assert.Equal(3, champs.Count);
        Assert.Equal(["case 1", "case 2", "case 3"], champs.Select(t => t.Text));

        champs[0].Text = "saisie au panneau";
        Battre();

        Assert.Equal("saisie au panneau", table.Cells[0].Text);
        Assert.Equal("saisie au panneau", Cases(fenetre)[0].Case.Contenu);
    }

    [AvaloniaFact]
    public void Inserer_un_tableau_pose_une_bande_a_trois_cases_et_sannule()
    {
        var document = Document.CreateDefault();
        var (_, vm) = Ouvrir(document);
        var avant = DocumentSerializer.ToJson(vm.Document);

        vm.AddTableCommand.Execute(null);
        Battre();

        var table = Assert.IsType<TableFrame>(vm.Document.Pages[0].Frames[0]);
        Assert.Equal(3, table.Columns.Count);
        Assert.Single(table.Rows);
        Assert.Equal(3, table.Cells.Count);
        // La largeur n'est pas posée à l'insertion : ce sont les colonnes qui la font.
        // À six décimales et non à l'identique : sélectionner le bloc fait passer sa
        // largeur par le panneau, donc par un aller-retour en millimètres qui laisse
        // 10⁻¹³ pt derrière lui — la dérive consignée au poste P3, sans effet ici.
        Assert.Equal(table.Columns.Sum(colonne => colonne.Width), table.Width, precision: 6);
        Assert.Same(table, vm.Selected);

        vm.UndoCommand.Execute(null);
        Battre();

        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
    }

    [AvaloniaFact]
    public void Un_tableau_trace_sur_la_page_mesure_ses_cases_au_cadre()
    {
        // L'outil arme, on trace, et le tableau prend la taille tracée : ses colonnes
        // et sa ligne s'y mesurent, comme les blocs d'un groupe redimensionné.
        var document = Document.CreateDefault();
        var (_, vm) = Ouvrir(document);

        vm.ActiveTool = EditorTool.Table;
        var trace = Assert.IsType<TableFrame>(vm.DrawWithActiveTool(50, 50, 300, 60));
        Battre();

        Assert.Equal(300, trace.Width, precision: 6);
        Assert.Equal(300, trace.Columns.Sum(colonne => colonne.Width), precision: 6);
        Assert.Equal(60, trace.Rows[0].Height, precision: 6);
        // L'outil se désarme tout seul, comme les autres.
        Assert.Equal(EditorTool.Select, vm.ActiveTool);
    }

    /// <summary>Compte les pixels franchement rouges — l'encre du bloc de test.</summary>
    private static int EncreRouge(RenderTargetBitmap image)
    {
        var largeur = image.PixelSize.Width;
        var hauteur = image.PixelSize.Height;
        var pixels = new byte[largeur * hauteur * 4];

        using var tampon = new WriteableBitmap(
            image.PixelSize, image.Dpi, Avalonia.Platform.PixelFormat.Bgra8888);

        using (var verrou = tampon.Lock())
        {
            image.CopyPixels(new PixelRect(0, 0, largeur, hauteur), verrou.Address,
                pixels.Length, verrou.RowBytes);
            System.Runtime.InteropServices.Marshal.Copy(verrou.Address, pixels, 0, pixels.Length);
        }

        var rouges = 0;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            // BGRA : rouge franc, peu de bleu et peu de vert.
            if (pixels[i + 2] > 150 && pixels[i] < 100 && pixels[i + 1] < 100) rouges++;
        }

        return rouges;
    }
}
