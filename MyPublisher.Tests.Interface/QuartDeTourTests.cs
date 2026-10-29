using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Rotation par quart de tour — poste A9 (fiches 14, 22 ; fiche 23, e5), livré par N18. La
/// rotation, sa poignée et l'export tourné existaient depuis A1 : il manquait le geste rapide.
/// Deux commandes, +90° et −90°, au sous-menu <i>Faire pivoter ou retourner ▸</i> de Disposition,
/// comme Publisher (p. 16), et deux boutons au dock, sans icône.
///
/// La recette d'origine : 350° plus 90° donne 80°, 0° moins 90° donne 270°, chaque commande
/// s'annule en un pas, et une image tournée d'un quart de tour passe <c>comparer-rendus.sh</c> sous
/// la tolérance — le dernier test écrit le document là où <c>MYPUBLISHER_RECETTE_QUART</c> le
/// demande, et la recette N18 le mesure.
/// </summary>
[Trait("poste", "A9")]
public class QuartDeTourTests
{
    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static MenuItem Menu(Window fenetre, string libelle) =>
        fenetre.GetLogicalDescendants().OfType<MenuItem>().SingleOrDefault(m => Equals(m.Header, libelle))
        ?? throw new InvalidOperationException($"le menu n'a pas d'entrée « {libelle} »");

    private static ShapeFrame Carre(double angle) =>
        new() { Name = "Carré", X = 100, Y = 100, Width = 120, Height = 60, Rotation = angle, Fill = new SolidFill("#FFCC0000") };

    /// <summary>La recette : 350° plus 90° donne 80°, et 0° moins 90° donne 270°, ramenés comme la poignée les ramène.</summary>
    [AvaloniaFact]
    public void Un_quart_de_tour_se_ramene_entre_0_et_360()
    {
        Assert.Equal(80, MainWindowViewModel.QuartDeTour(350, 90));
        Assert.Equal(270, MainWindowViewModel.QuartDeTour(0, -90));
        Assert.Equal(0, MainWindowViewModel.QuartDeTour(270, 90));
        Assert.Equal(180, MainWindowViewModel.QuartDeTour(-90, -90));
    }

    /// <summary>
    /// Disposition ▸ Faire pivoter ou retourner ▸ Faire pivoter à droite de 90° : 350° devient 80°,
    /// en un pas ; à gauche, 0° devient 270°, en un pas. Le bloc tourne autour de son centre : son
    /// cadre ne bouge pas.
    /// </summary>
    [AvaloniaFact]
    public void Le_menu_Disposition_fait_pivoter_d_un_quart_de_tour_en_un_pas()
    {
        var bloc = Carre(350);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = Etat(vm);

        Rendu.Cliquer(Menu(fenetre, "_Disposition"));
        Rendu.Cliquer(Menu(fenetre, "_Faire pivoter ou retourner"));
        Rendu.Cliquer(Menu(fenetre, "Faire pivoter à _droite de 90°"));
        Rendu.Battre();
        Assert.Equal(80, bloc.Rotation);
        Assert.Equal((100.0, 100.0, 120.0, 60.0), (bloc.X, bloc.Y, bloc.Width, bloc.Height));
        var repris = Rendu.UnSeulPas(vm, avant, "pivoter à droite")!;
        Assert.Equal(350, repris.Rotation);

        repris.Rotation = 0;
        vm.BeginEdit();
        vm.CommitEdit();
        var zero = Etat(vm);
        Rendu.Cliquer(Menu(fenetre, "_Disposition"));
        Rendu.Cliquer(Menu(fenetre, "_Faire pivoter ou retourner"));
        Rendu.Cliquer(Menu(fenetre, "Faire pivoter à _gauche de 90°"));
        Rendu.Battre();
        Assert.Equal(270, ((Frame)vm.SingleSelected!).Rotation);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(zero, Etat(vm));
    }

    /// <summary>
    /// Les deux boutons du dock, à Position et taille, sans icône : −90° et +90°, chacun en un pas,
    /// même quand un champ du dock attend — « 40 » tapé dans Y fait son pas, le quart de tour le sien.
    /// Sans sélection, les commandes sont grisées.
    /// </summary>
    [AvaloniaFact]
    public void Les_boutons_du_dock_font_un_quart_de_tour_chacun_en_un_pas()
    {
        var bloc = Carre(0);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        Assert.False(vm.PivoterADroiteCommand.CanExecute(null));
        Assert.False(vm.PivoterAGaucheCommand.CanExecute(null));

        vm.SelectOnly(bloc);
        Rendu.Battre();
        var gauche = Trouver<Button>(fenetre, "BoutonPivoterAGauche");
        var droite = Trouver<Button>(fenetre, "BoutonPivoterADroite");
        Assert.Equal("−90°", gauche.Content);
        Assert.Equal("+90°", droite.Content);
        Assert.Null(gauche.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().FirstOrDefault());

        var avant = Etat(vm);
        Rendu.Cliquer(gauche);
        Assert.Equal(270, bloc.Rotation);
        var repris = (ShapeFrame)Rendu.UnSeulPas(vm, avant, "−90° au dock")!;

        // Un champ du dock qui attend : sa frappe part d'abord, dans son pas.
        var y = Trouver<ChampNumerique>(fenetre, "ChampY");
        var zone = y.GetVisualDescendants().OfType<TextBox>().First();
        Rendu.Cliquer(zone);
        Rendu.Touche(fenetre, PhysicalKey.A, RawInputModifiers.Control);
        Rendu.Taper(fenetre, "40");
        Rendu.Cliquer(Trouver<Button>(fenetre, "BoutonPivoterADroite"));
        Assert.Equal(90, repris.Rotation);
        Assert.Equal(Units.MmToPoints(40), repris.Y, precision: 6);

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        var defait = (Frame)vm.CurrentPage!.Frames[0];
        Assert.Equal(0, defait.Rotation);
        Assert.Equal(Units.MmToPoints(40), defait.Y, precision: 6);
        Assert.True(vm.CanUndo, "la frappe de Y s'est fondue dans le quart de tour");
    }

    /// <summary>
    /// Le geste de la recette : une image tournée d'un quart de tour à droite, au menu, puis le
    /// document écrit là où <c>MYPUBLISHER_RECETTE_QUART</c> le demande — un dossier, où la recette a
    /// posé <c>motif.png</c>. La recette N18 le passe à <c>comparer-rendus.sh</c>. Sans la variable,
    /// le geste se joue sur une image absente, et rien ne s'écrit.
    /// </summary>
    [AvaloniaFact]
    public void Le_quart_de_tour_de_la_recette_ecrit_son_document()
    {
        var dossier = Environment.GetEnvironmentVariable("MYPUBLISHER_RECETTE_QUART");
        var image = new ImageFrame
        {
            Name = "Motif", X = 150, Y = 200, Width = 240, Height = 120,
            Path = string.IsNullOrWhiteSpace(dossier) ? "motif-absent.png" : Path.Combine(dossier, "motif.png"),
        };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(image));
        vm.SelectOnly(image);
        Rendu.Battre();

        Rendu.Cliquer(Menu(fenetre, "_Disposition"));
        Rendu.Cliquer(Menu(fenetre, "_Faire pivoter ou retourner"));
        Rendu.Cliquer(Menu(fenetre, "Faire pivoter à _droite de 90°"));
        Rendu.Battre();
        Assert.Equal(90, image.Rotation);

        if (!string.IsNullOrWhiteSpace(dossier))
        {
            DocumentSerializer.Save(vm.Document, Path.Combine(dossier, "quart.mypub"));
        }
    }
}
