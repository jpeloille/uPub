using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Le seuil du pointeur — postes Q21 et Q22, sur la vraie fenêtre, au pointeur synthétisé.
///
/// Q14 avait gardé le bloc d'un clic sans mouvement ; un pointeur réel tremble. Un pixel de
/// mouvement pendant le clic, près d'une marge, faisait sauter le bloc de 3 pt par
/// l'aimantation et empilait un pas d'annulation (Q21) ; une pression sur une poignée
/// repassait par les cosinus d'un bloc tourné, et <c>Maj</c> ramenait à 0° un bloc à 7° sans
/// le moindre mouvement (Q22). La planche ne compte désormais un geste pour un glissé qu'une
/// fois le pointeur écarté de trois pixels d'écran de sa prise : en deçà, ni le bloc, ni ses
/// poignées, ni sa rotation, ni les extrémités d'un trait ne bougent, et rien ne passe par
/// l'aimantation. Au-delà, le geste est un glissé jusqu'au relâché, ramené ou non.
///
/// Les gestes sont ceux du banc : <see cref="Rendu.Trembler"/>, le clic d'une vraie main, et
/// <see cref="Rendu.GlisserEtRamener"/>, le glissé plus court que le seuil.
/// </summary>
public class SeuilDuPointeurTests
{
    /// <summary>
    /// Un bloc dont le bord gauche est à 3 pt de la marge de gauche : à la tolérance de
    /// l'aimantation, six pixels, le moindre mouvement l'y attirait.
    /// </summary>
    private static (ShapeFrame Bloc, Document Document) PresDeLaMarge()
    {
        var bloc = new ShapeFrame { Name = "Près de la marge", Y = 300, Width = 100, Height = 80 };
        var document = Rendu.Feuille(bloc);
        bloc.X = document.MarginLeft + 3;
        return (bloc, document);
    }

    private static void RienNaChange(MainWindowViewModel vm, string avant, string geste)
    {
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, $"{geste} a empilé un pas d'annulation");
        Assert.False(vm.IsDirty, $"{geste} a marqué le document modifié");
    }

    // ------------------------------------------------------------------ Q21 : le bloc

    /// <summary>
    /// La recette du plan : un clic qui bouge d'un pixel près d'une marge laisse le bloc à sa
    /// place et n'empile rien. Dans chaque direction, et en diagonale jusqu'à 2,8 px : un
    /// mouvement vertical aimantait aussi l'abscisse, l'aimantation portant sur les deux
    /// axes dès que le pointeur n'était plus exactement à sa prise. Le premier clic prend le
    /// bloc, les suivants le trouvent pris.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q21")]
    public void Un_clic_qui_tremble_pres_dune_marge_laisse_le_bloc_a_sa_place()
    {
        var (bloc, document) = PresDeLaMarge();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var avant = DocumentSerializer.ToJson(vm.Document);
        var centre = Rendu.Centre(fenetre, bloc);

        foreach (var ecart in new Vector[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(-2, 2) })
        {
            Rendu.Trembler(fenetre, centre, ecart);
            Assert.Same(bloc, vm.SingleSelected);
            RienNaChange(vm, avant, $"un clic qui tremble de {ecart}");
        }
    }

    /// <summary>
    /// Le seuil se compte en pixels d'écran, et non en points : la main tremble à l'écran. À
    /// 50 %, deux pixels font quatre points, et le bloc ne bouge pas ; à 200 %, quatre pixels
    /// n'en font que deux, et le bloc les suit. Alt écarte l'aimantation : seul le seuil peut
    /// retenir le bloc.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q21")]
    public void Le_seuil_se_compte_en_pixels_decran()
    {
        var bloc = new ShapeFrame { Name = "A", X = 100, Y = 100, Width = 100, Height = 100 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = DocumentSerializer.ToJson(vm.Document);

        vm.Zoom = 0.5 / Units.PointToDip;
        Rendu.UneImage();
        Rendu.Trembler(fenetre, Rendu.Centre(fenetre, bloc), new Vector(2, 0), RawInputModifiers.Alt);
        RienNaChange(vm, avant, "à 50 %, un tremblement de deux pixels");

        vm.Zoom = 2 / Units.PointToDip;
        Rendu.UneImage();
        var centre = Rendu.Centre(fenetre, bloc);
        Rendu.Glisser(fenetre, centre, centre + new Vector(4, 0), RawInputModifiers.Alt);
        Assert.Equal(102, bloc.X, precision: 9);
        Rendu.UnSeulPas(vm, avant, "à 200 %, un glissé de quatre pixels");
    }

    /// <summary>
    /// L'aimantation ne joue qu'au-delà du seuil, et elle y joue encore : un glissé d'un
    /// point, mené au-delà puis ramené, déplace le bloc d'un point et fait son pas, loin de
    /// tout repère, sans Alt.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q21")]
    public void Un_glisse_dun_point_mene_au_dela_du_seuil_puis_ramene_deplace_dun_point()
    {
        var bloc = new ShapeFrame { Name = "A", X = 150, Y = 150, Width = 60, Height = 60 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = DocumentSerializer.ToJson(vm.Document);
        var centre = Rendu.Centre(fenetre, bloc);

        Rendu.GlisserEtRamener(fenetre, centre, centre + new Vector(0, 1));

        Assert.Equal((150.0, 151.0), (bloc.X, bloc.Y));
        Rendu.UnSeulPas(vm, avant, "un glissé d'un point ramené");
    }

    /// <summary>
    /// Le glissé qui a franchi le seuil puis revient exactement à sa prise rend le bloc à son
    /// départ sans l'aimanter, et n'empile rien : c'est la garde du mouvement nul de Q14, que
    /// le seuil ne laisse plus atteindre que par ce chemin-là. Près de la marge, sans Alt : le
    /// bloc ramené au point de prise y était attiré sans elle.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q21")]
    public void Un_glisse_ramene_a_sa_prise_pres_dune_marge_rend_le_bloc_sans_laimanter()
    {
        var (bloc, document) = PresDeLaMarge();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);
        var centre = Rendu.Centre(fenetre, bloc);

        Rendu.GlisserEtRamener(fenetre, centre, centre);

        RienNaChange(vm, avant, "un glissé ramené à sa prise");
    }

    /// <summary>
    /// L'extrémité d'un trait, à 3 pt de la marge : un clic qui tremble d'un pixel sur sa
    /// poignée la laissait aimanter sur la marge, le trait allongé de 3 pt. La garde du
    /// mouvement nul d'A10 ne couvrait que le pointeur immobile.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q21")]
    public void Une_extremite_de_trait_qui_tremble_reste_ou_elle_est()
    {
        var trait = new ShapeFrame
        {
            Name = "Trait", Shape = ShapeKind.Line, Fill = null, Stroke = "#FF000000", StrokeThickness = 2,
        };
        var document = Rendu.Feuille(trait);
        trait.PoserLesExtremites(document.MarginLeft + 3, 300, 250, 300);
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.SelectOnly(trait);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        Rendu.Trembler(fenetre, Rendu.SurLaPage(fenetre, trait.Debut.X, trait.Debut.Y), new Vector(1, 0));

        Assert.Same(trait, vm.SingleSelected);
        RienNaChange(vm, avant, "un clic qui tremble sur l'extrémité d'un trait");
    }

    /// <summary>
    /// En saisie, le clic qui en sort sur un autre bloc le prend d'un seul geste (N5) ; s'il
    /// tremble, il ne le déplace pas : la frappe garde son pas, le clic n'en fait aucun.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q21")]
    public void Le_clic_qui_sort_de_la_saisie_et_tremble_ne_deplace_pas_lautre_bloc()
    {
        var texte = new TextFrame { Name = "Texte", X = 300, Y = 100, Width = 200, Height = 80, Text = "Un texte." };
        var (bloc, document) = PresDeLaMarge();
        document.Pages[0].Frames.Add(texte);
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var avant = DocumentSerializer.ToJson(vm.Document);

        Rendu.EntrerEnSaisie(fenetre, vm, texte);
        Rendu.Taper(fenetre, "abc");
        Rendu.Trembler(fenetre, Rendu.Centre(fenetre, bloc), new Vector(1, 0));

        Assert.False(texte.IsEditing, "le clic sur l'autre bloc n'a pas refermé la saisie");
        Assert.Same(bloc, vm.SingleSelected);
        Assert.Equal(document.MarginLeft + 3, bloc.X);
        Rendu.UnSeulPas(vm, avant, "la frappe, puis un clic qui tremble sur un autre bloc");
    }
}
