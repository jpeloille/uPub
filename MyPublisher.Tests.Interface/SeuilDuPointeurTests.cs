using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
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

    // ------------------------------------------------------------------ Q22 : les poignées

    /// <summary>
    /// Les poignées que l'Overlay montre, rotation comprise, leur centre dans la fenêtre : lu
    /// dans l'arbre, et non recalculé — un bloc tourné les porte où sa rotation les mène.
    /// </summary>
    private static Point[] CentresDesPoignees(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Canvas>().Single(c => c.Name == "Overlay").Children
            .OfType<Shape>()
            .Where(s => s.IsVisible && s.Classes.Contains("poignee"))
            .Select(s => s.TranslatePoint(new Point(s.Width / 2, s.Height / 2), fenetre)!.Value)
            .ToArray();

    /// <summary>
    /// La recette du plan : une pression puis un relâchement sur chacune des huit poignées
    /// laissent le bloc identique et l'historique vide — avec le mouvement nul qu'une
    /// plate-forme peut livrer, et en tremblant d'un pixel, ce qui redimensionnait le bloc
    /// d'autant. Et la mesure du relevé de Q14, une pression à quatre pixels du coin sud-est.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q22")]
    public void Une_pression_sur_chacune_des_huit_poignees_laisse_le_bloc()
    {
        var bloc = new ShapeFrame { Name = "A", X = 100, Y = 100, Width = 100, Height = 100 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.UneImage();
        var avant = DocumentSerializer.ToJson(vm.Document);

        var poignees = (from fy in new[] { 0, 0.5, 1 }
                        from fx in new[] { 0, 0.5, 1 }
                        where fx != 0.5 || fy != 0.5
                        select (fx, fy)).ToList();
        Assert.Equal(8, poignees.Count);

        foreach (var (fx, fy) in poignees)
        {
            var poignee = Rendu.Poignee(fenetre, bloc, fx, fy);
            Rendu.Cliquer(fenetre, poignee, mouvementNul: true);
            RienNaChange(vm, avant, $"une pression sans mouvement sur la poignée ({fx}, {fy})");
            Rendu.Trembler(fenetre, poignee, new Vector(1, 1));
            RienNaChange(vm, avant, $"une pression qui tremble sur la poignée ({fx}, {fy})");
            Assert.Same(bloc, vm.SingleSelected);
        }

        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 204, 204), mouvementNul: true);
        Assert.Equal((100.0, 100.0), (bloc.Width, bloc.Height));
        RienNaChange(vm, avant, "une pression à quatre pixels du coin sud-est");
    }

    /// <summary>
    /// Un bloc tourné de 12,3456789°, pressé sans mouvement sur chacune de ses neuf poignées —
    /// la rotation comprise —, puis en tremblant d'un pixel : il ne bouge pas. La pression
    /// repassait par les cosinus du redimensionnement, et le bloc en ressortait décalé d'un
    /// arrondi, avec un pas empilé.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q22")]
    public void Un_bloc_tourne_presse_sur_chaque_poignee_ne_bouge_pas()
    {
        var bloc = new ShapeFrame { Name = "Tourné", X = 150, Y = 200, Width = 160, Height = 90, Rotation = 12.3456789 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.UneImage();
        var avant = DocumentSerializer.ToJson(vm.Document);

        var poignees = CentresDesPoignees(fenetre);
        Assert.Equal(9, poignees.Length);

        foreach (var poignee in poignees)
        {
            Rendu.Cliquer(fenetre, poignee, mouvementNul: true);
            RienNaChange(vm, avant, $"une pression sans mouvement en {poignee}");
            Rendu.Trembler(fenetre, poignee, new Vector(1, 0));
            RienNaChange(vm, avant, $"une pression qui tremble en {poignee}");
            Assert.Same(bloc, vm.SingleSelected);
        }
    }

    /// <summary>
    /// Maj arrondit l'angle au pas de 15° — sur un glissé. Pressée sous Maj, sans mouvement ou
    /// en tremblant, la poignée de rotation d'un bloc à 7° le ramenait à 0° ; elle le laisse à
    /// 7°. Mené au-delà du seuil puis ramené à un pixel de sa prise — l'angle à 6,2° environ —,
    /// le même geste arrondit encore, à 0°.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q22")]
    public void La_poignee_de_rotation_pressee_sous_Maj_laisse_langle()
    {
        var bloc = new ShapeFrame { Name = "À 7°", X = 150, Y = 200, Width = 160, Height = 90, Rotation = 7 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.UneImage();
        var avant = DocumentSerializer.ToJson(vm.Document);
        var disque = Rendu.PoigneeDeRotation(fenetre) ?? throw new System.InvalidOperationException("aucune poignée de rotation");
        var poignee = disque.TranslatePoint(new Point(disque.Width / 2, disque.Height / 2), fenetre)!.Value;

        Rendu.Cliquer(fenetre, poignee, RawInputModifiers.Shift, mouvementNul: true);
        Assert.Equal(7, bloc.Rotation);
        RienNaChange(vm, avant, "la poignée de rotation pressée sous Maj");

        Rendu.Trembler(fenetre, poignee, new Vector(1, 0), RawInputModifiers.Shift);
        Assert.Equal(7, bloc.Rotation);
        RienNaChange(vm, avant, "la poignée de rotation pressée sous Maj, en tremblant");

        Rendu.GlisserEtRamener(fenetre, poignee, poignee + new Vector(-1, 0), RawInputModifiers.Shift);
        Assert.Equal(0, bloc.Rotation);
        Rendu.UnSeulPas(vm, avant, "un glissé de la poignée de rotation sous Maj");
    }
}
