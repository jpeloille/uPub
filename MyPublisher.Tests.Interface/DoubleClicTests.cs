using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Un double-clic ne déplace pas le bloc — le poste Q14.
///
/// Le défaut a été trouvé par Q1, sur ATA 21 importé, et c'est sur ce document-là qu'on
/// l'éprouve : un bloc posé à des coordonnées rondes, loin des repères, ne montre ni
/// l'une ni l'autre de ses deux causes.
///
/// Elles sont deux, et la mesure les a séparées. Le dernier chiffre de l'ordonnée
/// (107.15039999999999 devenu 107.1504) venait du panneau, à la sélection : le champ en
/// millimètres renvoyait au modèle la valeur qu'il venait de recevoir, et l'aller-retour
/// n'est pas l'identité. Le pas d'annulation venait du geste de déplacement : un
/// mouvement nul du pointeur passait par l'aimantation, qui attirait vers un repère
/// voisin un bloc qu'on n'avait fait que cliquer.
/// </summary>
public class DoubleClicTests
{
    private static Panel Planche(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");

    private static Point SurLaPage(Window fenetre, double x, double y)
    {
        var origine = Planche(fenetre).TranslatePoint(new Point(0, 0), fenetre) ?? new Point(0, 0);
        return new Point(origine.X + x, origine.Y + y);
    }

    /// <summary>
    /// Les blocs de texte de la première page qu'un clic en leur centre désigne
    /// eux-mêmes — ceux qu'un autre bloc recouvre en leur milieu ne se double-cliquent
    /// pas là.
    /// </summary>
    private static List<TextFrame> BlocsAtteints(Page page) =>
        page.Frames.OfType<TextFrame>()
            .Where(bloc => ReferenceEquals(bloc, page.Frames.LastOrDefault(
                f => f.HitTest(bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2))))
            .ToList();

    /// <summary>Le bloc où Q1 a vu le défaut, reconnu à son ordonnée telle que l'import la donne.</summary>
    private static TextFrame BlocDeQ1(IEnumerable<TextFrame> blocs) =>
        blocs.First(bloc => bloc.Y.ToString("R") == "107.15039999999999");

    /// <summary>
    /// Un clic tel qu'une vraie plate-forme peut le livrer : le bouton pressé, un
    /// mouvement qui ne va nulle part, le bouton relâché. Le mouvement nul n'est pas
    /// une fantaisie du banc : c'est lui qui passait par l'aimantation, et un banc qui
    /// ne l'émettrait pas ne verrait que la moitié du défaut.
    /// </summary>
    private static void Cliquer(Window fenetre, Point point)
    {
        fenetre.MouseDown(point, MouseButton.Left);
        fenetre.MouseMove(point, RawInputModifiers.LeftMouseButton);
        fenetre.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    [Trait("poste", "Q14")]
    public void Un_double_clic_ouvre_la_saisie_sans_rien_reecrire()
    {
        var document = Rendu.Ata21();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var blocs = BlocsAtteints(vm.CurrentPage!);

        // Le bloc de Q1 doit en être, et les autres aussi : c'est en eux que
        // l'aimantation mordait. Moins de blocs, et le test pourrait passer sur
        // ceux qui ne montrent rien.
        BlocDeQ1(blocs);
        Assert.True(blocs.Count >= 5, $"{blocs.Count} blocs atteints seulement");

        foreach (var bloc in blocs)
        {
            // Rien n'est sélectionné : le premier clic prend le bloc et arme le
            // déplacement, le second ouvre la saisie. C'est le geste entier.
            vm.SetSelection([]);
            Rendu.Battre();
            Assert.False(vm.CanUndo);
            Assert.False(vm.IsDirty);

            var avant = DocumentSerializer.ToJson(vm.Document);
            var centre = SurLaPage(fenetre, bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2);

            Cliquer(fenetre, centre);
            Cliquer(fenetre, centre);
            Rendu.Battre(8);

            Assert.True(bloc.IsEditing, $"le double-clic n'a pas ouvert la saisie de « {bloc.Name} »");

            // Au caractère près : c'est le dernier chiffre de l'ordonnée que le défaut
            // réécrivait, et une comparaison à un milliardième l'aurait laissé passer.
            Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
            Assert.False(vm.CanUndo, $"un double-clic sur « {bloc.Name} » a empilé un pas d'annulation");
            Assert.False(vm.IsDirty, $"un double-clic sur « {bloc.Name} » a marqué le document modifié");

            // Refermer la saisie sans avoir rien tapé n'empile rien non plus.
            fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Rendu.Battre();
            Assert.False(bloc.IsEditing);
            Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
            Assert.False(vm.CanUndo);
        }
    }

    [AvaloniaFact]
    [Trait("poste", "Q14")]
    public void Un_double_clic_garde_langle_dun_bloc_tourne_a_la_main()
    {
        // ATA 21 ne porte que des quarts de tour, que l'arrondi à quatre décimales du
        // champ de rotation rend intacts. Un angle posé à la poignée n'est jamais rond :
        // le champ le rendait arrondi au modèle, à la seule sélection. Et une abscisse
        // de 100 pt, la plus ronde qui soit, revenait 100.00000000000007 du champ en
        // millimètres.
        var bloc = new TextFrame
        {
            Name = "Tourné", X = 100, Y = 200, Width = 200, Height = 80,
            Rotation = 12.3456789, Text = "Un bloc tourné à la main."
        };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = DocumentSerializer.ToJson(vm.Document);

        var centre = SurLaPage(fenetre, bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2);
        Cliquer(fenetre, centre);
        Cliquer(fenetre, centre);
        Rendu.Battre(8);

        Assert.True(bloc.IsEditing, "le double-clic n'a pas ouvert la saisie");
        Assert.Equal(12.3456789, bloc.Rotation);
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
        Assert.False(vm.IsDirty);
    }

    [AvaloniaFact]
    [Trait("poste", "Q14")]
    public void Un_glisse_dun_point_empile_toujours_son_pas()
    {
        // La garde ne doit rien manger d'un vrai geste : à ce zoom, le plus petit qu'on
        // fasse au pointeur est d'un point. Alt écarte l'aimantation, qui pourrait
        // ramener le bloc sur un repère et rendre le geste nul pour une autre raison
        // que la garde.
        var document = Rendu.Ata21();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var bloc = BlocDeQ1(BlocsAtteints(vm.CurrentPage!));
        var depart = bloc.Y;

        var avant = DocumentSerializer.ToJson(vm.Document);
        var centre = SurLaPage(fenetre, bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2);
        var arrivee = new Point(centre.X, centre.Y + 1);

        fenetre.MouseDown(centre, MouseButton.Left);
        fenetre.MouseMove(arrivee, RawInputModifiers.LeftMouseButton | RawInputModifiers.Alt);
        fenetre.MouseUp(arrivee, MouseButton.Left, RawInputModifiers.Alt);
        Rendu.Battre();

        Assert.Equal(depart + 1, bloc.Y);
        Assert.True(vm.CanUndo, "un glissé d'un point n'a pas empilé de pas d'annulation");
        Assert.True(vm.IsDirty);

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
    }

    [AvaloniaFact]
    [Trait("poste", "Q14")]
    public void Revenir_au_point_de_prise_dans_le_meme_geste_ramene_le_bloc()
    {
        // La garde ne se contente pas de ne rien faire quand le pointeur est à son point
        // de prise : elle remet le bloc à son départ. Un geste qui part puis revient
        // exactement là où il a pris le bloc doit le rendre là où il était, et non le
        // laisser au dernier endroit où le pointeur l'a mené. Alt écarte l'aimantation.
        var bloc = new ShapeFrame { Name = "A", X = 100, Y = 100, Width = 100, Height = 100 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var prise = SurLaPage(fenetre, 150, 150);

        fenetre.MouseDown(prise, MouseButton.Left);
        fenetre.MouseMove(new Point(prise.X + 40, prise.Y + 30),
            RawInputModifiers.LeftMouseButton | RawInputModifiers.Alt);
        Rendu.Battre();
        Assert.Equal((140.0, 130.0), (bloc.X, bloc.Y));

        fenetre.MouseMove(prise, RawInputModifiers.LeftMouseButton | RawInputModifiers.Alt);
        fenetre.MouseUp(prise, MouseButton.Left, RawInputModifiers.Alt);
        Rendu.Battre();

        Assert.Equal((100.0, 100.0), (bloc.X, bloc.Y));
    }
}
