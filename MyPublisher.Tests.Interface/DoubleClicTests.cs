using System;
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
/// Un double-clic ne déplace pas le bloc — le poste Q14. Depuis le poste N12, on entre dans le
/// texte d'un clic : c'est ce clic, et celui qui prend le bloc par son bord, qui ne doivent
/// rien réécrire.
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

    /// <summary>
    /// Les blocs de texte de la première page qu'un clic en leur centre désigne
    /// eux-mêmes — ceux qu'un autre bloc recouvre en leur milieu ne s'ouvrent pas là.
    /// </summary>
    private static List<TextFrame> BlocsAtteints(Page page) =>
        page.Frames.OfType<TextFrame>()
            .Where(bloc => ReferenceEquals(bloc, page.Frames.LastOrDefault(
                f => f.HitTest(bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2))))
            .ToList();

    /// <summary>Le bloc où Q1 a vu le défaut, reconnu à son ordonnée telle que l'import la donne.</summary>
    private static TextFrame BlocDeQ1(IEnumerable<TextFrame> blocs) =>
        blocs.First(bloc => bloc.Y.ToString("R") == "107.15039999999999");

    // Le geste entier, sans sélection préalable : le premier clic prend le bloc.
    //
    // Avalonia reconnaît un double-clic à l'horloge — deux pressions à moins de 500 ms —, et
    // le banc sans tête date chaque pression du moment où il la livre. Une pression sur un
    // bloc non sélectionné rendait la main une fois le panneau recomposé : 170 à 470 ms,
    // mesurés le 12 octobre 2026, et le test du bloc tourné tombait une fois sur deux. Les
    // deux tests sélectionnaient donc le bloc par le modèle de vue avant le geste. Le poste
    // Q23 a ôté la cause — les réglages du bloc sont construits une fois et gardés
    // (GabaritsGardes) — et rendu aux tests leur premier clic.
    //
    // Repris par le poste N12 (règle 14) : on entre dans le texte d'un clic, et le double-clic
    // n'y ouvre plus rien, il prend un mot. Le premier clic ouvre donc déjà la saisie ; ce que
    // Q14 voulait savoir — un clic qui ne bouge pas ne réécrit rien — se joue sur ce clic-là, et
    // sur le clic qui prend le bloc par sa bande de bord, celui qui arme le déplacement et
    // passait par l'aimantation. Le double-clic sur un mot, à l'horloge, est éprouvé par
    // ClicDansLeTexteTests, vingt fois par la recette N12.
    //
    // Le relevé d'avant est pris avant le premier clic : un panneau qui réécrirait le bloc
    // en l'affichant serait vu, et chaque clic passe par la pression, le mouvement nul et le
    // relâchement, où l'aimantation mordait.

    [AvaloniaFact]
    [Trait("poste", "Q14")]
    public void Un_clic_ouvre_la_saisie_sans_rien_reecrire()
    {
        var document = Rendu.Ata21();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var page = vm.CurrentPage!;
        var blocs = BlocsAtteints(page);

        // Le bloc de Q1 doit en être, et les autres aussi : c'est en eux que
        // l'aimantation mordait. Moins de blocs, et le test pourrait passer sur
        // ceux qui ne montrent rien.
        BlocDeQ1(blocs);
        Assert.True(blocs.Count >= 5, $"{blocs.Count} blocs atteints seulement");
        var parLeBord = 0;

        foreach (var bloc in blocs)
        {
            // Rien n'est sélectionné au relevé.
            vm.SetSelection([]);
            Rendu.Battre();
            Assert.False(vm.CanUndo);
            Assert.False(vm.IsDirty);
            var avant = DocumentSerializer.ToJson(vm.Document);

            // Par la bande de bord, le clic prend le bloc et arme le déplacement, sans saisie.
            if (Rendu.BordAtteint(fenetre, bloc) is { } bord)
            {
                Rendu.Cliquer(fenetre, bord, mouvementNul: true);
                Assert.Same(bloc, vm.SingleSelected);
                Assert.False(bloc.IsEditing, $"le bord de « {bloc.Name} » a ouvert la saisie");
                Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
                Assert.False(vm.CanUndo, $"un clic sur le bord de « {bloc.Name} » a empilé un pas d'annulation");
                Assert.False(vm.IsDirty, $"un clic sur le bord de « {bloc.Name} » a marqué le document modifié");
                parLeBord++;

                vm.SetSelection([]);
                Rendu.Battre();
            }

            // Dans son texte, le premier clic prend le bloc et ouvre la saisie.
            var centre = Rendu.SurLaPage(fenetre, bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2);
            Rendu.Cliquer(fenetre, centre, mouvementNul: true);
            Assert.Same(bloc, vm.SingleSelected);
            Assert.True(bloc.IsEditing, $"le clic n'a pas ouvert la saisie de « {bloc.Name} »");

            // Au caractère près : c'est le dernier chiffre de l'ordonnée que le défaut
            // réécrivait, et une comparaison à un milliardième l'aurait laissé passer.
            Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
            Assert.False(vm.CanUndo, $"un clic sur « {bloc.Name} » a empilé un pas d'annulation");
            Assert.False(vm.IsDirty, $"un clic sur « {bloc.Name} » a marqué le document modifié");

            // Refermer la saisie sans avoir rien tapé n'empile rien non plus.
            fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Rendu.Battre();
            Assert.False(bloc.IsEditing);
            Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
            Assert.False(vm.CanUndo);
        }

        Assert.True(parLeBord >= 5, $"{parLeBord} blocs pris par leur bord seulement");
    }

    [AvaloniaFact]
    [Trait("poste", "Q14")]
    public void Un_clic_garde_langle_dun_bloc_tourne_a_la_main()
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
        Assert.False(vm.HasSelection);

        var centre = Rendu.SurLaPage(fenetre, bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2);
        Rendu.Cliquer(fenetre, centre, mouvementNul: true);
        Assert.Same(bloc, vm.SingleSelected);
        Assert.True(bloc.IsEditing, "le clic n'a pas ouvert la saisie");
        Rendu.Battre(8);

        Assert.Equal(12.3456789, bloc.Rotation);
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
        Assert.False(vm.IsDirty);

        // Par la bande de bord, le bloc pris sans saisie garde son angle de même.
        fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        vm.SetSelection([]);
        Rendu.Battre();
        Rendu.Cliquer(fenetre, Rendu.Bord(fenetre, bloc), mouvementNul: true);
        Assert.Same(bloc, vm.SingleSelected);
        Assert.False(bloc.IsEditing);
        Assert.Equal(12.3456789, bloc.Rotation);
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    [Trait("poste", "Q14")]
    public void Un_glisse_dun_point_empile_toujours_son_pas()
    {
        // La garde ne doit rien manger d'un vrai geste : à ce zoom, le plus petit qu'on
        // fasse au pointeur est d'un point. Alt écarte l'aimantation, qui pourrait
        // ramener le bloc sur un repère et rendre le geste nul pour une autre raison
        // que la garde.
        //
        // Réécrit par le poste Q21 (règle 14) : un pointeur qui ne s'écarte que d'un pixel
        // de sa prise n'a pas glissé, il a tremblé, et le bloc ne bouge plus. Le glissé
        // d'un point se fait comme une main le fait, mené au-delà du seuil puis ramené.
        //
        // Réécrit par le poste N12 (règle 14) : le glissé prend le bloc par sa bande de bord.
        // Parti de son centre, il entrerait dans le texte et y sélectionnerait.
        var document = Rendu.Ata21();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var bloc = BlocDeQ1(BlocsAtteints(vm.CurrentPage!));
        var depart = bloc.Y;

        var avant = DocumentSerializer.ToJson(vm.Document);
        var prise = Rendu.BordAtteint(fenetre, bloc) ?? throw new InvalidOperationException("le bord du bloc de Q1 est couvert");
        var arrivee = new Point(prise.X, prise.Y + 1);

        Rendu.GlisserEtRamener(fenetre, prise, arrivee, RawInputModifiers.Alt);

        Assert.False(bloc.IsEditing, "le glissé par le bord a ouvert la saisie");
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
        var prise = Rendu.SurLaPage(fenetre, 150, 150);

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

    [AvaloniaFact]
    [Trait("poste", "Q14")]
    public void Une_valeur_tapee_ne_prend_pas_celle_quun_autre_bloc_a_affichee()
    {
        // Le convertisseur rend la valeur exacte d'un nombre qu'il vient d'afficher.
        // Une mémoire plus longue que ce renvoi prenait une frappe pour un affichage
        // ancien : A, tourné de 30.00001°, montre « 30 » ; on sélectionne B, on y tape
        // « 30 », et B recevait 30.00001.
        var a = new ShapeFrame { Name = "A", X = 50, Y = 50, Width = 100, Height = 100, Rotation = 30.00001 };
        var b = new ShapeFrame { Name = "B", X = 300, Y = 300, Width = 100, Height = 100 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b));

        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 100, 100), mouvementNul: true);
        Rendu.Battre();
        Assert.Same(a, vm.SingleSelected);
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 350, 350), mouvementNul: true);
        Rendu.Battre();
        Assert.Same(b, vm.SingleSelected);

        // Sélectionner ne réécrit ni l'un ni l'autre.
        Assert.Equal(30.00001, a.Rotation);
        Assert.Equal(0.0, b.Rotation);

        var rotation = fenetre.GetVisualDescendants().OfType<NumericUpDown>()
            .Single(n => n.IsEffectivelyVisible && n.Minimum == -360 && n.Maximum == 360);
        Assert.Equal(0m, rotation.Value);

        var saisie = rotation.GetVisualDescendants().OfType<TextBox>().First();
        saisie.Focus();
        saisie.SelectAll();
        fenetre.KeyTextInput("30");
        fenetre.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Rendu.Battre();

        Assert.Equal(30m, rotation.Value);
        Assert.Equal(30.0, b.Rotation);
        Assert.Equal(30.00001, a.Rotation);
    }
}
