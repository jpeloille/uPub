using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// La recette de C2, rendue rejouable : la grille de composition (C2a) et les repères
/// libres (C2b), éprouvés au pointeur synthétisé sur les vraies règles et la vraie
/// couche de saisie — même harnais que <see cref="GestesTests"/>.
///
/// Les deux recettes d'origine ont été jouées ainsi, puis le harnais a été jeté. Ce
/// qu'elles disaient, et que ces tests redisent geste pour geste :
///
///   · C2a — « quatre blocs lâchés à des endroits différents à moins de 4 pt du bord de
///     la deuxième colonne s'y posent tous exactement, et Alt désarme bien
///     l'aimantation » ;
///   · C2b — « poser, aimanter, désarmer par Alt, reprendre, retirer hors page, et un
///     bloc qui vient se poser exactement sur un repère choisi. Le tout est annulable ».
///
/// Le calcul de l'aimantation est éprouvé sans fenêtre dans <c>AimantationTests</c> ;
/// ce qui ne s'éprouve qu'ici, c'est que le GESTE l'appelle avec les bons repères —
/// ceux du document, grille et repères libres compris. Un repère libre qui cesserait
/// d'entrer dans la table ne casserait aucun calcul : il cesserait juste de servir.
///
/// La partie imprimée de la recette — les repères ne paraissent pas au PDF — est dans
/// <c>Scripts/recettes/C2-reperes.sh</c>, qui appelle ces tests par leur trait.
/// </summary>
public class ReperesTests
{
    /// <summary>
    /// Trois colonnes à gouttière de 12 pt dans la zone de marges d'un A4 : la
    /// composition de bulletin que la recette de C2a nommait.
    /// </summary>
    private static Document TroisColonnes(params Frame[] blocs)
    {
        var document = Rendu.Feuille(blocs);
        document.ColumnCount = 3;
        document.ColumnSpacing = 12;
        return document;
    }

    /// <summary>
    /// La fenêtre, repères ALLUMÉS : un repère éteint ne se saisit pas — c'est voulu, on
    /// n'attrape pas ce qu'on ne voit pas — et <see cref="Rendu.Ouvrir"/> les éteint
    /// pour mesurer l'encre.
    /// </summary>
    private static (Window Fenetre, MainWindowViewModel Vm) Ouvrir(Document document)
    {
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.ShowGuides = true;
        Rendu.Battre();
        return (fenetre, vm);
    }

    private static Panel Page(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");

    /// <summary>Un point de la page, en points typographiques, dans le repère fenêtre.</summary>
    private static Point SurLaPage(Window fenetre, double x, double y)
    {
        var origine = Page(fenetre).TranslatePoint(new Point(0, 0), fenetre) ?? new Point(0, 0);
        return new Point(origine.X + x, origine.Y + y);
    }

    /// <summary>
    /// Un point de la règle du haut, à l'abscisse de page donnée, à mi-hauteur de la
    /// règle. La règle borde la page et défile avec elle : son origine est celle de la
    /// page, ce que le test vérifie au passage plutôt que de le supposer.
    /// </summary>
    private static Point SurLaRegleDuHaut(Window fenetre, double x)
    {
        var regle = fenetre.GetVisualDescendants().OfType<Control>().First(c => c.Name == "TopRuler");
        Assert.True(regle.IsVisible, "la règle du haut est cachée : on ne peut pas en tirer un repère");

        var origine = regle.TranslatePoint(new Point(0, regle.Bounds.Height / 2), fenetre)
                      ?? throw new InvalidOperationException("la règle n'est pas dans la fenêtre");
        Assert.Equal(SurLaPage(fenetre, 0, 0).X, origine.X, precision: 6);
        return new Point(origine.X + x, origine.Y);
    }

    /// <summary>Saisit en <paramref name="de"/>, mène en <paramref name="vers"/>, lâche.</summary>
    private static void Glisser(Window fenetre, Point de, Point vers, bool alt = false)
    {
        var touches = alt ? RawInputModifiers.Alt : RawInputModifiers.None;
        fenetre.MouseDown(de, MouseButton.Left, touches);
        fenetre.MouseMove(vers, touches);
        fenetre.MouseUp(vers, MouseButton.Left, touches);
        Rendu.Battre();
    }

    private static ShapeFrame Bloc(string nom, double x, double y) => new()
    {
        Name = nom, X = x, Y = y, Width = 60, Height = 40, Fill = new SolidFill("#FFCC0000"), Stroke = null,
    };

    /// <summary>Le bord gauche de la deuxième colonne, lu dans la table des repères.</summary>
    private static double BordDeLaDeuxiemeColonne(Document document)
    {
        // Les bandes vont par paires (début, fin) : la deuxième colonne commence au
        // troisième nombre. On le relit plutôt que de le recalculer — un test qui refait
        // le calcul du code ne prouve rien de lui.
        var bords = document.VerticalGridLines.Select(ligne => ligne.Position).Order().ToList();
        Assert.Equal(4, bords.Count);
        return bords[1];
    }

    [AvaloniaFact]
    [Trait("poste", "C2")]
    public void Quatre_blocs_laches_pres_dune_colonne_sy_posent_exactement()
    {
        // Quatre blocs, quatre écarts différents — à gauche et à droite du bord, de un
        // à près de quatre points — et quatre hauteurs loin de tout repère horizontal,
        // pour que seule l'aimantation verticale ait à jouer.
        var blocs = new[] { Bloc("Un", 80, 120), Bloc("Deux", 80, 220), Bloc("Trois", 80, 300), Bloc("Quatre", 80, 480) };
        var document = TroisColonnes(blocs);
        var (fenetre, vm) = Ouvrir(document);
        var bord = BordDeLaDeuxiemeColonne(document);

        foreach (var (bloc, ecart) in blocs.Zip(new[] { -3.5, -1.0, 2.0, 3.75 }))
        {
            vm.SelectOnly(bloc);
            Rendu.Battre();

            var prise = new Point(bloc.X + 30, bloc.Y + 20);
            var dx = bord + ecart - bloc.X;
            Glisser(fenetre, SurLaPage(fenetre, prise.X, prise.Y), SurLaPage(fenetre, prise.X + dx, prise.Y));
        }

        // Exactement, et non à un pixel près : c'est la table des repères qui pose le
        // bloc, pas le pointeur.
        Assert.All(blocs, bloc => Assert.Equal(bord, bloc.X, precision: 9));
        Assert.Equal([120.0, 220, 300, 480], blocs.Select(bloc => Math.Round(bloc.Y, 9)));
    }

    [AvaloniaFact]
    [Trait("poste", "C2")]
    public void Alt_desarme_laimantation_des_blocs()
    {
        var bloc = Bloc("Libre", 80, 120);
        var document = TroisColonnes(bloc);
        var (fenetre, vm) = Ouvrir(document);
        var bord = BordDeLaDeuxiemeColonne(document);

        vm.SelectOnly(bloc);
        Rendu.Battre();

        // Le même lâcher que ci-dessus, Alt tenu : le bloc reste où on l'a mis, à deux
        // points du bord. Si l'aimantation l'emportait, il serait SUR le bord.
        var vise = Math.Round(bord) + 2;
        Glisser(fenetre, SurLaPage(fenetre, 110, 140), SurLaPage(fenetre, 110 + vise - 80, 140), alt: true);

        Assert.Equal(vise, bloc.X, precision: 6);
        Assert.True(Math.Abs(bloc.X - bord) > 1, $"le bloc s'est aimanté malgré Alt ({bloc.X} contre {bord})");
    }

    [AvaloniaFact]
    [Trait("poste", "C2")]
    public void Un_repere_tire_de_la_regle_se_pose_et_sannule()
    {
        var document = TroisColonnes();
        var (fenetre, vm) = Ouvrir(document);
        vm.ShowGuides = false;   // le geste doit les rallumer de lui-même
        Rendu.Battre();

        // 333 pt : loin de tout bord de colonne, de la marge et du milieu de page.
        Glisser(fenetre, SurLaRegleDuHaut(fenetre, 200), SurLaPage(fenetre, 333, 400));

        var repere = Assert.Single(document.VerticalRulerGuides);
        Assert.Equal(333, repere.Position, precision: 6);
        Assert.Empty(document.HorizontalRulerGuides);
        Assert.True(vm.ShowGuides, "poser un repère doit rallumer les repères");

        // Le repère se trace : son trait est dans l'arbre visuel, visible, et là où il a
        // été posé — relu sur la page et non dans le modèle.
        var trait = fenetre.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>()
            .Single(r => ReferenceEquals(r.DataContext, repere));
        Assert.True(trait.IsEffectivelyVisible, "le repère posé ne se voit pas");
        Assert.Equal(333, trait.TranslatePoint(new Point(0, 0), Page(fenetre))!.Value.X, precision: 3);

        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Empty(vm.Document.VerticalRulerGuides);
    }

    [AvaloniaFact]
    [Trait("poste", "C2")]
    public void Un_repere_saimante_sur_un_bord_de_colonne_sauf_sous_alt()
    {
        var document = TroisColonnes();
        var (fenetre, _) = Ouvrir(document);
        var bord = BordDeLaDeuxiemeColonne(document);

        // Lâché à trois points du bord : il s'y pose exactement. C'est ce qui permet de
        // poser un repère SUR une colonne sans le saisir au chiffre.
        Glisser(fenetre, SurLaRegleDuHaut(fenetre, 200), SurLaPage(fenetre, bord + 3, 400));
        Assert.Equal(bord, Assert.Single(document.VerticalRulerGuides).Position, precision: 9);

        // Le même lâcher sous Alt : il reste où on l'a mis.
        var vise = Math.Round(bord) - 3;
        Glisser(fenetre, SurLaRegleDuHaut(fenetre, 450), SurLaPage(fenetre, vise, 400), alt: true);
        Assert.Equal(2, document.VerticalRulerGuides.Count);
        Assert.Equal(vise, document.VerticalRulerGuides[1].Position, precision: 6);
    }

    [AvaloniaFact]
    [Trait("poste", "C2")]
    public void Un_repere_se_reprend_puis_se_retire_hors_de_la_page()
    {
        var document = TroisColonnes();
        document.VerticalRulerGuides.Add(new RulerGuide(250));
        var (fenetre, vm) = Ouvrir(document);
        var repere = document.VerticalRulerGuides[0];

        // Reprendre : on le saisit sur la page, là où aucun bloc ne s'interpose.
        Glisser(fenetre, SurLaPage(fenetre, 250, 400), SurLaPage(fenetre, 350, 400));
        Assert.Same(repere, Assert.Single(document.VerticalRulerGuides));
        Assert.Equal(350, repere.Position, precision: 6);

        // Retirer : le ramener au-delà du bord de la page, d'où il venait.
        Glisser(fenetre, SurLaPage(fenetre, 350, 400), SurLaPage(fenetre, -15, 400));
        Assert.Empty(document.VerticalRulerGuides);
        Assert.Equal("Repère retiré", vm.Status);

        // Deux pas d'annulation, deux gestes : le retrait se défait, puis la reprise.
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(350, Assert.Single(vm.Document.VerticalRulerGuides).Position, precision: 6);

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(250, Assert.Single(vm.Document.VerticalRulerGuides).Position, precision: 6);
    }

    [AvaloniaFact]
    [Trait("poste", "C2")]
    public void Un_bloc_se_pose_exactement_sur_un_repere_choisi()
    {
        // Un repère à une position que rien d'autre ne propose — ni marge, ni colonne,
        // ni milieu —, et qu'aucun pointeur ne viserait au chiffre : c'est le cas que
        // seuls les repères libres servent. 333,3 pt et 612,7 pt.
        var bloc = Bloc("Posé", 80, 120);
        var document = Rendu.Feuille(bloc);
        document.VerticalRulerGuides.Add(new RulerGuide(333.3));
        document.HorizontalRulerGuides.Add(new RulerGuide(612.7));
        var (fenetre, vm) = Ouvrir(document);

        vm.SelectOnly(bloc);
        Rendu.Battre();

        // Lâché à trois points du repère vertical par son bord gauche, et à deux points
        // de l'horizontal par son bord BAS : les deux axes s'aimantent chacun de leur côté.
        Glisser(fenetre, SurLaPage(fenetre, 110, 140),
            SurLaPage(fenetre, 110 + (336 - 80), 140 + (615 - 160)));

        Assert.Equal(333.3, bloc.X, precision: 9);
        Assert.Equal(612.7, bloc.Y + bloc.Height, precision: 9);
    }
}
