using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Converters;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;
using Xunit.Abstractions;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Sélectionner un bloc ne fige pas la fenêtre — le poste Q23.
///
/// Trouvé à la fusion de Q14 : une pression sur un bloc non sélectionné rendait la main
/// après 170 à 470 ms sur ATA 21, contre quelques millisecondes sur un bloc déjà pris. La
/// mesure, reprise sur l'interface de la phase I, a nommé la cause : les trois sections du
/// dock et les réglages de la barre contextuelle étaient des <c>ContentControl</c> liés au
/// bloc seul. Une sélection vide leur donnait <c>null</c>, ils jetaient leurs champs, et la
/// pression suivante les reconstruisait tous — mille visuels, deux douzaines de champs
/// numériques, leurs styles et leurs gabarits de contrôle, dans la pression même. Ils sont
/// désormais construits une fois par sorte de bloc, et gardés (<c>GabaritsGardes</c>).
///
/// Deux sortes de tests. Le premier est la recette du plan, au chronomètre : il dit ce que
/// la main ressent, mais une machine chargée peut le faire mentir dans les deux sens. Les
/// autres comptent ce qui se construit et ce qui se garde : ils ne dépendent pas de la
/// charge, et c'est eux qui voient le retour de la cause.
/// </summary>
public class PressionTests(ITestOutputHelper sortie)
{
    /// <summary>
    /// Le seuil de la recette, en millisecondes. Voir le test qui le lit pour la marge qu'il
    /// laisse à la charge de la machine.
    /// </summary>
    private const double Seuil = 100;

    private static Panel Planche(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");

    private static Point SurLaPage(Window fenetre, double x, double y)
    {
        var origine = Planche(fenetre).TranslatePoint(new Point(0, 0), fenetre) ?? new Point(0, 0);
        return new Point(origine.X + x, origine.Y + y);
    }

    private static Point Centre(Window fenetre, Frame bloc) =>
        SurLaPage(fenetre, bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2);

    /// <summary>
    /// Les blocs de la page qu'une pression en leur centre désigne eux-mêmes, de toutes
    /// sortes : sur ATA 21, sept blocs de texte et un cadre.
    /// </summary>
    private static List<Frame> BlocsAtteints(Page page) =>
        page.Frames
            .Where(bloc => bloc.Visible && ReferenceEquals(bloc, page.Frames.LastOrDefault(
                f => f.Visible && f.HitTest(bloc.X + bloc.Width / 2, bloc.Y + bloc.Height / 2))))
            .ToList();

    /// <summary>Une image, comme l'écran la rendrait, et ce qu'elle a laissé à faire.</summary>
    private static void Image()
    {
        Rendu.Battre();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Rendu.Battre();
    }

    /// <summary>Un clic au pointeur, à cet endroit de la page.</summary>
    private static void Cliquer(Window fenetre, Point point)
    {
        fenetre.MouseDown(point, MouseButton.Left);
        fenetre.MouseUp(point, MouseButton.Left);
        Image();
    }

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().Single(c => c.Name == nom);

    // ------------------------------------------------------------------ la recette

    /// <summary>
    /// La recette du plan, au pointeur synthétisé, sur ATA 21 : chaque bloc qu'une pression
    /// atteint en son centre, pressé alors que rien n'est sélectionné — le cas qui
    /// reconstruisait tout —, trois passes, et la médiane des trois sous 100 ms.
    ///
    /// Ce que le chronomètre compte : la pression, puis tout ce que la fenêtre a encore à
    /// faire — la mise en page, ce que la pression a posté, et l'image qui montre le bloc
    /// pris. Le banc sans tête rend cette image sur le fil de l'interface, où l'application
    /// ne paie que la remise au compositeur : la mesure est plus sévère que la main.
    ///
    /// **La marge contre la charge de la machine.** Mesuré le 25 octobre 2026, charge
    /// moyenne 1,2 à 2 : avant la correction, 107 à 136 ms de médiane par bloc de texte ;
    /// après, 30 à 60 ms, la première passe d'un processus neuf comprise — le seuil laisse
    /// donc un facteur de deux au moins. Trois choses le gardent : la médiane, qui écarte
    /// une passe que la machine a ralentie ; une collecte forcée avant chaque pression, parce
    /// que le banc alloue une image de 1400 × 900 à chaque rendu et en signale la mémoire au
    /// ramasse-miettes, qui déclencherait au hasard des pressions des collectes que
    /// l'application ne fait pas ; et un seuil qui ne prétend pas mesurer mieux que la
    /// charge le permet. Le retour de la cause, lui, ne dépend pas de la charge : les tests
    /// qui suivent le voient en comptant.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q23")]
    public void Une_pression_sur_un_bloc_non_selectionne_rend_la_main_en_moins_de_100_ms()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Ata21());
        var blocs = BlocsAtteints(vm.CurrentPage!);
        Assert.True(blocs.Count >= 8, $"{blocs.Count} blocs atteints seulement");
        Assert.Contains(blocs, bloc => bloc is TextFrame);
        Assert.Contains(blocs, bloc => bloc is not TextFrame);

        var mesures = blocs.ToDictionary(bloc => bloc, _ => new List<double>());
        for (var passe = 0; passe < 3; passe++)
        {
            foreach (var bloc in blocs)
            {
                vm.SetSelection([]);
                Image();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Deux pressions de suite ne tombent jamais au même endroit : un bloc n'est
                // pressé de nouveau qu'après les sept autres, et Avalonia n'y voit pas un
                // double-clic, qui ouvrirait la saisie.
                var centre = Centre(fenetre, bloc);
                var chrono = Stopwatch.StartNew();
                fenetre.MouseDown(centre, MouseButton.Left);
                Image();
                chrono.Stop();

                Assert.Same(bloc, vm.SingleSelected);
                mesures[bloc].Add(chrono.Elapsed.TotalMilliseconds);

                fenetre.MouseUp(centre, MouseButton.Left);
                Rendu.Battre();
                Assert.False(vm.CanUndo, $"une pression sur « {bloc.Name} » a empilé un pas d'annulation");
            }
        }

        string Ms(double valeur) => valeur.ToString("0", CultureInfo.InvariantCulture);
        var medianes = mesures.Select(m => (Bloc: m.Key, Mediane: m.Value.OrderBy(x => x).ElementAt(1), Passes: m.Value)).ToList();
        foreach (var (bloc, mediane, passes) in medianes)
        {
            sortie.WriteLine($"{bloc.GetType().Name,-10} {bloc.Name,-30} médiane {Ms(mediane),4} ms   ({string.Join(" / ", passes.Select(Ms))})");
        }

        var lents = medianes.Where(m => m.Mediane >= Seuil).ToList();
        Assert.True(lents.Count == 0,
            "une pression sur un bloc non sélectionné rend la main en 100 ms ou plus : " +
            string.Join(" ; ", lents.Select(m => $"« {m.Bloc.Name} » {Ms(m.Mediane)} ms ({string.Join(" / ", m.Passes.Select(Ms))})")));
    }

    // ------------------------------------------------------------------ la cause

    /// <summary>
    /// Ce que le chronomètre ne garde pas contre la charge : les champs du dock et de la
    /// barre sont construits à la première sélection d'un bloc de leur sorte, puis gardés —
    /// une sélection vide les masque, un bloc d'une autre sorte aussi, et le bloc suivant
    /// les retrouve, les mêmes, liés à lui. Une pression ne construit plus rien.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q23")]
    public void Les_reglages_du_bloc_sont_construits_une_fois_et_gardes()
    {
        var a = new TextFrame { Name = "A", X = 60, Y = 60, Width = 200, Height = 80, Text = "Premier bloc." };
        var cadre = new ShapeFrame { Name = "Cadre", X = 60, Y = 220, Width = 120, Height = 80 };
        var b = new TextFrame { Name = "B", X = 300, Y = 60, Width = 180, Height = 100, Text = "Second bloc." };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, cadre, b));
        var vide = SurLaPage(fenetre, 400, 500);

        // Un champ de chaque réglage gardé : « Position et taille », les sections du type,
        // « Fond et contour », la barre contextuelle.
        string[] noms = ["ChampX", "NombreDeColonnes", "SorteDeFond", "PoliceDuBloc"];
        Control[] Champs() =>
        [
            Trouver<NumericUpDown>(fenetre, "ChampX"), Trouver<NumericUpDown>(fenetre, "NombreDeColonnes"),
            Trouver<ComboBox>(fenetre, "SorteDeFond"), Trouver<ComboBox>(fenetre, "PoliceDuBloc"),
        ];

        Cliquer(fenetre, Centre(fenetre, a));
        Assert.Same(a, vm.SingleSelected);
        var premiers = Champs();
        Assert.All(premiers, champ => Assert.True(champ.IsEffectivelyVisible));
        Assert.All(premiers, champ => Assert.Same(a, champ.DataContext));

        // Rien de sélectionné : les champs restent, masqués.
        Cliquer(fenetre, vide);
        Assert.Null(vm.SingleSelected);
        Assert.All(Champs(), champ => Assert.False(champ.IsEffectivelyVisible, "un champ du bloc se montre sans bloc"));

        // Un cadre : la position et le fond le suivent, les sections du texte et la barre se taisent.
        Cliquer(fenetre, Centre(fenetre, cadre));
        Assert.Same(cadre, vm.SingleSelected);
        var auCadre = Champs();
        Assert.True(auCadre[0].IsEffectivelyVisible && auCadre[2].IsEffectivelyVisible);
        Assert.False(auCadre[1].IsEffectivelyVisible || auCadre[3].IsEffectivelyVisible,
            "les réglages du texte se montrent pour un cadre");
        Assert.True(Trouver<CheckBox>(fenetre, "PointeAuDebut").IsEffectivelyVisible);

        // Puis rien, puis l'autre bloc de texte : ce sont les mêmes champs, liés à lui, et la
        // pression n'a rien construit.
        Cliquer(fenetre, vide);
        var visuels = fenetre.GetVisualDescendants().Count();
        Cliquer(fenetre, Centre(fenetre, b));
        Assert.Same(b, vm.SingleSelected);
        var crees = fenetre.GetVisualDescendants().Count() - visuels;
        sortie.WriteLine($"visuels de la fenêtre : {visuels} avant la pression, {visuels + crees} après");

        var seconds = Champs();
        for (var i = 0; i < noms.Length; i++)
        {
            Assert.True(ReferenceEquals(premiers[i], seconds[i]), $"« {noms[i]} » a été reconstruit d'une sélection à l'autre");
            Assert.True(seconds[i].IsEffectivelyVisible, $"« {noms[i]} » ne se montre pas pour le second bloc");
            Assert.Same(b, seconds[i].DataContext);
        }

        Assert.Equal(Units.PointsToMm(b.X), (double)((NumericUpDown)seconds[0]).Value!.Value, precision: 6);
        Assert.True(crees < 50, $"une pression sur un bloc non sélectionné a ajouté {crees} visuels à la fenêtre");
        Assert.False(vm.CanUndo, "sélectionner a écrit dans le document");
    }

    /// <summary>
    /// Un champ masqué ne garde pas le focus. Il quittait l'arbre, avant, et la frappe
    /// n'allait plus nulle part ; masqué en le gardant, il la recevrait, et l'écrirait dans
    /// le bloc qu'on ne regarde plus. La sélection se vide ici sans le pointeur, comme une
    /// annulation ou un changement de page la vident.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q23")]
    public void Un_champ_masque_rend_le_focus()
    {
        var a = new TextFrame { Name = "A", X = 60, Y = 60, Width = 200, Height = 80, Text = "Un bloc." };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a));
        Cliquer(fenetre, Centre(fenetre, a));

        var nom = Trouver<TextBox>(fenetre, "ChampNom");
        nom.Focus();
        Rendu.Battre();
        Assert.True(nom.IsFocused);

        vm.SetSelection([]);
        Rendu.Battre();
        Assert.False(nom.IsEffectivelyVisible);
        Assert.False(nom.IsFocused, "le champ masqué a gardé le focus");

        fenetre.KeyTextInput("!!");
        Rendu.Battre();
        Assert.Equal("A", a.Name);
        Assert.False(vm.CanUndo);
        Assert.False(vm.IsDirty);
    }

    /// <summary>
    /// « Blocs de la page » montre le bloc pris sur la planche, mais ne défile plus que si
    /// sa ligne n'est pas en vue : la liste défilait par une passe de mise en page entière,
    /// qui reconstruisait chacune de ses lignes, à chaque pression, même sans avoir à bouger.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "Q23")]
    public void La_liste_des_blocs_ne_defile_que_pour_montrer_le_bloc_pris()
    {
        // Dix blocs : la liste en montre six, du premier plan au fond.
        var blocs = Enumerable.Range(0, 10)
            .Select(i => (Frame)new ShapeFrame { Name = $"Bloc {i}", X = 40 + i % 5 * 100, Y = 60 + i / 5 * 120, Width = 80, Height = 80 })
            .ToArray();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(blocs));
        var liste = Trouver<ListBox>(fenetre, "ListeDesBlocs");
        var defilement = liste.GetVisualDescendants().OfType<ScrollViewer>().First();

        bool EnVue(Frame bloc) =>
            liste.ContainerFromItem(bloc) is Control { IsVisible: true } ligne &&
            ligne.TranslatePoint(new Point(0, 0), liste) is { } haut &&
            haut.Y >= 0 && haut.Y + ligne.Bounds.Height <= liste.Bounds.Height;

        // Le bloc du fond est la dernière ligne, hors de vue : la liste défile jusqu'à lui.
        Assert.False(EnVue(blocs[0]));
        Cliquer(fenetre, Centre(fenetre, blocs[0]));
        Assert.Same(blocs[0], vm.SingleSelected);
        Assert.True(EnVue(blocs[0]), "la ligne du bloc pris sur la planche n'est pas en vue");

        // Un bloc dont la ligne est déjà en vue : la liste ne bouge pas.
        var decalage = defilement.Offset;
        var voisin = blocs.Skip(1).First(EnVue);
        Cliquer(fenetre, Centre(fenetre, voisin));
        Assert.Same(voisin, vm.SingleSelected);
        Assert.Equal(decalage, defilement.Offset);
        Assert.True(EnVue(voisin));
    }
}
