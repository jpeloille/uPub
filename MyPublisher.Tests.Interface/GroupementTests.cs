using System;
using System.IO;
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
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// La recette de C3, rendue rejouable : « sélectionner quatre blocs, les aligner sur
/// leur bord gauche, les grouper, déplacer le groupe, l'exporter — la géométrie tient ».
///
/// Les trois temps du poste y passent, chacun par son geste : la sélection multiple
/// (C3a) au clic et au Maj-clic, l'alignement (C3b) par la commande du menu, le
/// groupement (C3c) au raccourci, et le déplacement à la souris. Ce qui ne se joue pas
/// ici, c'est l'export : le test écrit ses deux documents — groupé, puis déplacé — là
/// où <c>MYPUBLISHER_RECETTE_GROUPE</c> le demande, et
/// <c>Scripts/recettes/C3-selection-groupe.sh</c> les exporte et mesure chaque mot
/// dans le PDF.
///
/// <c>GroupeTests</c> éprouve déjà le calcul du groupe — mise à l'échelle, passage d'un
/// repère à l'autre. Rien, jusqu'ici, n'éprouvait la commande d'alignement ni celle du
/// groupement : on pouvait les casser toutes deux sans qu'un seul test tombe.
/// </summary>
public class GroupementTests
{
    /// <summary>
    /// Quatre blocs de texte d'un mot, à quatre abscisses et quatre largeurs
    /// différentes. Le plus à gauche est à 150 pt : c'est là que l'alignement doit les
    /// amener tous. Les mots sont distincts pour que la recette les retrouve chacun
    /// dans le PDF.
    /// </summary>
    private static readonly (string Mot, double X, double Y, double Largeur)[] Blocs =
    [
        ("Alpha", 190, 150, 160),
        ("Bravo", 150, 230, 120),
        ("Charlie", 230, 310, 200),
        ("Delta", 170, 390, 140),
    ];

    /// <summary>Le déplacement que la recette nomme.</summary>
    private const double Dx = 30, Dy = 20;

    /// <summary>
    /// Chaque bloc porte un fond : quatre mots seuls ne pèsent que quelques milliers de
    /// pixels, et un groupe que l'écran poserait ailleurs que le PDF passerait sous le
    /// seuil de la comparaison d'images. Quatre aplats, eux, ne passeraient pas.
    /// </summary>
    private static Document Quatre()
    {
        var blocs = Blocs.Select(b => (Frame)new TextFrame
        {
            Name = b.Mot, Text = b.Mot, X = b.X, Y = b.Y, Width = b.Largeur, Height = 40,
            Fill = new SolidFill("#FFB0C4DE"),
        });
        return Rendu.Feuille(blocs.ToArray());
    }

    [AvaloniaFact]
    [Trait("poste", "C3")]
    public void Quatre_blocs_selectionnes_alignes_groupes_puis_deplaces()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Quatre());
        var page = vm.CurrentPage!;
        var blocs = page.Frames.ToList();

        // C3a — un clic, puis trois Maj-clics. Chacun vise le milieu de son bloc.
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, Blocs[0].X + 20, Blocs[0].Y + 20));
        foreach (var b in Blocs.Skip(1)) Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, b.X + 20, b.Y + 20), RawInputModifiers.Shift);

        Assert.Equal(4, vm.Selection.Count);
        Assert.All(blocs, bloc => Assert.Contains(bloc, vm.Selection));
        // Saisir un ensemble pour l'étendre n'est pas le déplacer : aucun pas empilé.
        Assert.False(vm.CanUndo);

        // C3b — aligner à gauche, les blocs ENTRE EUX : la case « par rapport aux
        // marges » décochée, comme dans le menu. Le bord commun est celui du plus à
        // gauche, et lui seul ne bouge pas.
        var gauche = Blocs.Min(b => b.X);
        vm.AlignToMargins = false;
        vm.AlignLeftCommand.Execute(null);
        Rendu.Battre();

        Assert.All(blocs, bloc => Assert.Equal(gauche, bloc.X, precision: 9));
        // À un milliardième près et non à l'identique : le bloc sélectionné passe par
        // le panneau, donc par l'aller-retour en millimètres consigné au poste P3, qui
        // laisse 10⁻¹³ pt derrière lui.
        Assert.All(blocs.Zip(Blocs), paire => Assert.Equal(paire.Second.Y, paire.First.Y, precision: 9));
        Assert.Equal(4, vm.Selection.Count);

        // C3c — grouper, au raccourci : le geste passe par le filtre de touches de la
        // fenêtre, que la commande seule ne verrait pas.
        fenetre.KeyPressQwerty(PhysicalKey.G, RawInputModifiers.Control | RawInputModifiers.Shift);
        Rendu.Battre();

        var groupe = Assert.IsType<GroupFrame>(Assert.Single(page.Frames));
        Assert.Equal(blocs, groupe.Children);
        Assert.Same(groupe, vm.Selected);

        // Grouper ne déplace rien : chaque bloc est à la même place sur la page.
        foreach (var (bloc, b) in blocs.Zip(Blocs))
        {
            var (x, y) = groupe.ToPage(bloc.X, bloc.Y);
            Assert.Equal(gauche, x, precision: 9);
            Assert.Equal(b.Y, y, precision: 9);
        }

        Ecrire(vm, "groupe.mypub");

        // Déplacer le groupe à la souris, en le saisissant par l'un de ses blocs. Les
        // bords de l'ensemble, avant comme après, restent loin de tout repère de la
        // page : seul le pointeur décide, et il se déplace d'un nombre entier de points.
        var avant = (groupe.X, groupe.Y);
        var prise = (X: gauche + 40, Y: Blocs[0].Y + 20);
        fenetre.MouseDown(Rendu.SurLaPage(fenetre, prise.X, prise.Y), MouseButton.Left);
        fenetre.MouseMove(Rendu.SurLaPage(fenetre, prise.X + Dx, prise.Y + Dy));
        fenetre.MouseUp(Rendu.SurLaPage(fenetre, prise.X + Dx, prise.Y + Dy), MouseButton.Left);
        Rendu.Battre();

        Assert.Equal(avant.X + Dx, groupe.X, precision: 9);
        Assert.Equal(avant.Y + Dy, groupe.Y, precision: 9);

        // Les blocs suivent sans que leurs coordonnées relatives changent.
        foreach (var (bloc, b) in blocs.Zip(Blocs))
        {
            var (x, y) = groupe.ToPage(bloc.X, bloc.Y);
            Assert.Equal(gauche + Dx, x, precision: 9);
            Assert.Equal(b.Y + Dy, y, precision: 9);
        }

        // La sélection ne s'écrit pas, mais elle se dessine : la recette compare
        // l'écran au PDF, et y trouverait le contour bleu du groupe.
        vm.Selection.Clear();
        Rendu.Battre();
        Ecrire(vm, "deplace.mypub");

        // Puis tout se défait, un pas par geste, et dans l'ordre inverse. L'annulation
        // rend un document relu de son instantané : on relit donc la page à chaque pas
        // plutôt que de se fier aux blocs d'avant.
        //
        // Le déplacement : un pas, et le groupe revient où il était — toujours groupé.
        Annuler(vm);
        var rendu = Assert.IsType<GroupFrame>(Assert.Single(vm.CurrentPage!.Frames));
        Assert.Equal(avant.X, rendu.X, precision: 9);
        Assert.Equal(avant.Y, rendu.Y, precision: 9);

        // Le groupement : un pas, et les quatre blocs reviennent, alignés.
        Annuler(vm);
        Assert.Equal(Blocs.Select(b => b.Mot), vm.CurrentPage!.Frames.Select(f => f.Name));
        Assert.All(vm.CurrentPage.Frames, bloc => Assert.Equal(gauche, bloc.X, precision: 9));

        // L'alignement : un pas, et chacun retrouve son abscisse d'origine. Une
        // commande d'alignement qui n'ouvrirait pas sa transaction laisserait les blocs
        // alignés ici, sans rien à annuler.
        Annuler(vm);
        foreach (var (bloc, b) in vm.CurrentPage!.Frames.Zip(Blocs))
        {
            Assert.Equal(b.X, bloc.X, precision: 9);
            Assert.Equal(b.Y, bloc.Y, precision: 9);
        }

        // Et rien de plus : la sélection au clic n'avait empilé aucun pas.
        Assert.False(vm.CanUndo);
    }

    private static void Annuler(MainWindowViewModel vm)
    {
        Assert.True(vm.CanUndo, "plus rien à annuler, alors qu'un geste reste à défaire");
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
    }

    /// <summary>
    /// Écrit le document là où la recette le demande ; ne fait rien hors de la recette.
    /// Le chemin est un DOSSIER : le test y dépose ses deux états, avant et après le
    /// déplacement, que la recette exporte l'un et l'autre.
    /// </summary>
    private static void Ecrire(MainWindowViewModel vm, string nom)
    {
        if (Environment.GetEnvironmentVariable("MYPUBLISHER_RECETTE_GROUPE") is { } dossier &&
            !string.IsNullOrWhiteSpace(dossier))
        {
            DocumentSerializer.Save(vm.Document, Path.Combine(dossier, nom));
        }
    }
}
