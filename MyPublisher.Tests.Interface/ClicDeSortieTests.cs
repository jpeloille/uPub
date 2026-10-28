using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Quitter la saisie d'un clic qui agit — le poste N5 (fiche 23, a8 et a9).
///
/// Pendant la saisie d'un texte sur la page, la couche de saisie se retirait du test de survol
/// pour laisser le pointeur au champ. Le clic suivant hors du texte refermait bien la saisie,
/// au passage descendant, mais la pression allait ensuite à la page : rien n'était pris, il
/// fallait recliquer, et les poignées dessinées autour du bloc en saisie ne se tiraient pas.
/// Publisher, lui, prend l'objet cliqué d'un seul geste.
///
/// La couche reste désormais atteignable partout, sauf sur le texte en saisie
/// (<see cref="CoucheDeSaisie"/>) : le clic hors du texte referme la saisie, puis fait ce
/// qu'il ferait hors saisie — il prend une poignée, un repère, un autre bloc, ou vide la
/// sélection. La frappe garde son pas, le geste fait le sien s'il en fait un, la sélection
/// aucun.
///
/// Ses gestes sont ceux du banc (<see cref="Rendu"/>, poste N1).
/// </summary>
public class ClicDeSortieTests
{
    // ------------------------------------------------------------------ le banc du poste

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static TextFrame Texte(string nom = "Texte", double x = 60, double y = 60) => new()
    {
        Name = nom, X = x, Y = y, Width = 300, Height = 80, Text = "Bonjour",
        FontFamily = "DejaVu Sans", FontSize = 10.5, Foreground = "#FF000000",
    };

    private static ShapeFrame Forme() => new()
    {
        Name = "Forme", X = 100, Y = 300, Width = 120, Height = 100, Fill = new SolidFill("#FF336699"),
    };

    private static CoucheDeSaisie Couche(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<CoucheDeSaisie>().Single(c => c.Name == "InputLayer");

    /// <summary>
    /// Entre en saisie, va au bout du texte et tape « abc » : la frappe dont on vérifie
    /// qu'elle garde son pas quand un clic referme la saisie.
    /// </summary>
    private static TextBox TaperDans(Window fenetre, MainWindowViewModel vm, TextFrame bloc)
    {
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc, dx: bloc.Width / 2, dy: bloc.Height / 2);
        Rendu.Touche(fenetre, PhysicalKey.End, RawInputModifiers.Control);
        Rendu.Taper(fenetre, "abc");
        Assert.Equal("Bonjourabc", bloc.Text);
        return zone;
    }

    /// <summary>
    /// Défait les pas un à un : chaque annulation rend l'état relevé juste avant le geste
    /// qu'elle défait, et il n'en reste aucun après le premier état.
    /// </summary>
    private static void DefaitsUnAUn(MainWindowViewModel vm, IReadOnlyList<(string Quoi, string Etat)> etats)
    {
        for (var i = etats.Count - 1; i > 0; i--)
        {
            Assert.True(vm.CanUndo, $"il manque le pas de « {etats[i].Quoi} »");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.True(etats[i - 1].Etat == Etat(vm),
                $"annuler « {etats[i].Quoi} » ne rend pas l'état d'après « {etats[i - 1].Quoi} »");
        }

        Assert.False(vm.CanUndo, $"{etats.Count - 1} gestes, plus de {etats.Count - 1} pas");
    }

    // ------------------------------------------------------------------ la recette du plan

    /// <summary>
    /// La recette : en saisie, un clic sur un autre bloc le sélectionne en un seul geste. La
    /// frappe fait un pas, la sélection aucun. Sur le code d'avant, le clic refermait la saisie
    /// et ne prenait rien : le bloc en saisie restait seul sélectionné.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N5")]
    public void En_saisie_un_clic_sur_un_autre_bloc_le_selectionne_d_un_seul_geste()
    {
        var bloc = Texte();
        var forme = Forme();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, forme));
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        TaperDans(fenetre, vm, bloc);
        etats.Add(("abc", Etat(vm)));

        Rendu.Cliquer(fenetre, Rendu.Centre(fenetre, forme));

        Assert.False(bloc.IsEditing, "le clic sur un autre bloc a laissé la saisie ouverte");
        Assert.Null(Rendu.ZoneDeSaisie(fenetre, bloc));
        Assert.Equal([forme], vm.Selection);
        Assert.Equal("Bonjourabc", bloc.Text);
        Assert.Equal(etats[^1].Item2, Etat(vm));

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>
    /// Le même clic, sur un autre bloc de texte, puis un glissé : le bloc pris se déplace dans
    /// le même geste — la pression qui sort de la saisie est celle qui le saisit. Deux pas :
    /// la frappe, puis le déplacement.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N5")]
    public void En_saisie_un_autre_bloc_se_tire_des_la_pression_qui_sort_de_la_saisie()
    {
        var bloc = Texte();
        var voisin = Texte("Voisin", 60, 300);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, voisin));
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        TaperDans(fenetre, vm, bloc);
        etats.Add(("abc", Etat(vm)));

        // Par la bande de bord du voisin : une pression dans son texte y entrerait (N12, règle 14).
        var depart = Rendu.Bord(fenetre, voisin);
        Rendu.Glisser(fenetre, depart, depart + new Vector(37, 23), RawInputModifiers.Alt);

        Assert.False(bloc.IsEditing);
        Assert.False(voisin.IsEditing, "la pression qui sort de la saisie a ouvert celle du voisin");
        Assert.Equal([voisin], vm.Selection);
        Assert.Equal((97.0, 323.0), (voisin.X, voisin.Y));
        etats.Add(("le déplacement", Etat(vm)));

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>
    /// La recette : une poignée du bloc en saisie se tire. Un pas pour la frappe, un pour le
    /// redimensionnement. Sur le code d'avant, les poignées étaient dessinées et inertes : la
    /// pression allait à la page, et le bloc gardait sa largeur.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N5")]
    public void En_saisie_une_poignee_du_bloc_se_tire()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        TaperDans(fenetre, vm, bloc);
        etats.Add(("abc", Etat(vm)));

        var poignee = Rendu.Poignee(fenetre, bloc, 1, 0.5);
        Rendu.Glisser(fenetre, poignee, poignee + new Vector(50, 0), RawInputModifiers.Alt);

        Assert.False(bloc.IsEditing, "tirer la poignée a laissé la saisie ouverte");
        Assert.Equal([bloc], vm.Selection);
        Assert.Equal(350, bloc.Width, precision: 6);
        Assert.Equal((60.0, 80.0), (bloc.X, bloc.Height));
        Assert.Equal("Bonjourabc", bloc.Text);
        etats.Add(("le redimensionnement", Etat(vm)));

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>
    /// La recette : un clic dans le texte en saisie y déplace le curseur et laisse la saisie
    /// ouverte — la couche le laisse à son champ. À un zoom qui n'est pas 1 et la planche
    /// décalée de la fenêtre par les barres, au coin du texte : c'est ce qui mesure le repère
    /// du point que le compositeur passe à la couche. Lu dans celui de la fenêtre, le coin du
    /// texte tomberait hors de lui, et la saisie se refermerait.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N5")]
    public void En_saisie_un_clic_dans_le_texte_y_pose_le_curseur_et_laisse_la_saisie_ouverte()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.Zoom = 2 / Units.PointToDip;
        Rendu.UneImage();

        var zone = TaperDans(fenetre, vm, bloc);
        Assert.NotEqual(new Point(0, 0), Couche(fenetre).TranslatePoint(default, fenetre));

        // Au coin du texte, loin de la poignée du coin, qui passe avant lui.
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, bloc.X + bloc.Width - 12, bloc.Y + bloc.Height - 8));
        Assert.True(bloc.IsEditing, "un clic au coin du texte a refermé la saisie");
        Assert.True(zone.IsFocused, "le clic dans le texte ne lui a pas laissé le focus");

        Rendu.Cliquer(fenetre, Rendu.BordDuCaractere(zone, 3));
        Assert.True(bloc.IsEditing, "un clic dans le texte a refermé la saisie");
        Assert.Equal(3, zone.CaretIndex);
        Assert.Equal(zone.SelectionStart, zone.SelectionEnd);

        Rendu.Taper(fenetre, "X");
        Assert.Equal("BonXjourabc", bloc.Text);
    }

    /// <summary>
    /// La recette : en saisie, un clic dans le vide désélectionne. La frappe garde son pas,
    /// la sélection vidée n'en fait aucun.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N5")]
    public void En_saisie_un_clic_dans_le_vide_deselectionne()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        TaperDans(fenetre, vm, bloc);
        etats.Add(("abc", Etat(vm)));

        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 400, 500));

        Assert.False(bloc.IsEditing, "un clic dans le vide a laissé la saisie ouverte");
        Assert.Empty(vm.Selection);
        Assert.Equal(etats[^1].Item2, Etat(vm));

        DefaitsUnAUn(vm, etats);
    }

    // ------------------------------------------------------------------ ce qui l'entoure

    /// <summary>
    /// En saisie, un repère libre se reprend d'un seul geste, comme hors saisie : il passe
    /// avant les blocs. La frappe, puis le repère : deux pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N5")]
    public void En_saisie_un_repere_se_reprend_d_un_seul_geste()
    {
        var bloc = Texte();
        var document = Rendu.Feuille(bloc);
        document.VerticalRulerGuides.Add(new RulerGuide(450));
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.ShowGuides = true;
        Rendu.UneImage();
        var repere = document.VerticalRulerGuides[0];
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        TaperDans(fenetre, vm, bloc);
        etats.Add(("abc", Etat(vm)));

        Rendu.Glisser(fenetre, Rendu.SurLaPage(fenetre, 450, 500), Rendu.SurLaPage(fenetre, 487, 500),
                      RawInputModifiers.Alt);

        Assert.False(bloc.IsEditing, "reprendre un repère a laissé la saisie ouverte");
        Assert.Same(repere, Assert.Single(vm.Document.VerticalRulerGuides));
        Assert.Equal(487, repere.Position, precision: 6);
        Assert.Equal([bloc], vm.Selection);
        etats.Add(("le repère", Etat(vm)));

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>
    /// Le gestionnaire descendant reste, pour les règles, que la couche ne couvre pas : en
    /// saisie, un glissé depuis la règle du haut referme la saisie et pose un repère neuf.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N5")]
    public void En_saisie_un_glisse_depuis_la_regle_referme_la_saisie_et_pose_un_repere()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.ShowGuides = true;
        Rendu.UneImage();
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };

        TaperDans(fenetre, vm, bloc);
        etats.Add(("abc", Etat(vm)));

        var regle = fenetre.GetVisualDescendants().OfType<Ruler>().Single(r => r.Name == "TopRuler");
        var depart = regle.TranslatePoint(new Point(regle.Origine + 420, regle.Bounds.Height / 2), fenetre)!.Value;
        Rendu.Glisser(fenetre, depart, Rendu.SurLaPage(fenetre, 433, 500), RawInputModifiers.Alt);

        Assert.False(bloc.IsEditing, "la pression sur la règle a laissé la saisie ouverte");
        Assert.Equal(433, Assert.Single(vm.Document.VerticalRulerGuides).Position, precision: 6);
        etats.Add(("le repère", Etat(vm)));

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>
    /// Un bloc tourné : la couche laisse au champ son texte tel qu'il est tourné, et non le
    /// rectangle droit qui l'enveloppe. Un clic dans le texte, loin du centre, laisse la saisie
    /// ouverte ; un clic dans le coin du rectangle droit que le texte tourné ne couvre plus la
    /// referme, et vide la sélection.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N5")]
    public void En_saisie_la_couche_laisse_au_champ_le_texte_tourne_et_lui_seul()
    {
        var bloc = Texte();
        bloc.Rotation = 30;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        TaperDans(fenetre, vm, bloc);

        var (dansX, dansY) = bloc.ToPage(bloc.Width - 20, bloc.Height - 15);
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, dansX, dansY));
        Assert.True(bloc.IsEditing, "un clic dans le texte tourné a refermé la saisie");

        // Le coin d'en haut à droite du bloc droit : hors du bloc tourné, loin de ses poignées.
        Assert.False(bloc.HitTest(bloc.X + bloc.Width - 10, bloc.Y + 5));
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, bloc.X + bloc.Width - 10, bloc.Y + 5));
        Assert.False(bloc.IsEditing, "un clic hors du texte tourné a laissé la saisie ouverte");
        Assert.Empty(vm.Selection);
    }

    /// <summary>
    /// La pression du bouton droit hors du texte sort aussi de la saisie, et elle atteint la
    /// couche de saisie, où le menu contextuel la prendra (A12) : sur le code d'avant, elle
    /// allait à la page. La frappe garde son pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N5")]
    public void En_saisie_la_pression_du_bouton_droit_sort_de_la_saisie_et_atteint_la_couche()
    {
        var bloc = Texte();
        var forme = Forme();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, forme));
        var etats = new List<(string, string)> { ("l'ouverture", Etat(vm)) };
        var recues = new List<MouseButton>();
        Couche(fenetre).AddHandler(InputElement.PointerPressedEvent,
            (_, e) => recues.Add(e.GetCurrentPoint(null).Properties.PointerUpdateKind switch
            {
                PointerUpdateKind.RightButtonPressed => MouseButton.Right,
                PointerUpdateKind.LeftButtonPressed => MouseButton.Left,
                _ => MouseButton.None,
            }),
            RoutingStrategies.Bubble, handledEventsToo: true);

        TaperDans(fenetre, vm, bloc);
        etats.Add(("abc", Etat(vm)));
        recues.Clear();

        Rendu.Cliquer(fenetre, Rendu.Centre(fenetre, forme), bouton: MouseButton.Right);

        Assert.False(bloc.IsEditing, "la pression du bouton droit a laissé la saisie ouverte");
        Assert.Equal([MouseButton.Right], recues);

        DefaitsUnAUn(vm, etats);
    }

    /// <summary>
    /// Hors du texte, la couche reprend aussi le survol : le pointeur qui passe sur un autre
    /// bloc pendant la saisie annonce qu'on peut le déplacer, comme hors saisie.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N5")]
    public void En_saisie_le_survol_d_un_autre_bloc_montre_le_curseur_de_deplacement()
    {
        var bloc = Texte();
        var forme = Forme();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, forme));
        var couche = Couche(fenetre);

        Rendu.Survoler(fenetre, Rendu.Centre(fenetre, forme));
        Rendu.Battre();
        var deplacement = couche.Cursor;
        Rendu.Survoler(fenetre, Rendu.SurLaPage(fenetre, 400, 500));
        Rendu.Battre();
        Assert.NotSame(deplacement, couche.Cursor);

        TaperDans(fenetre, vm, bloc);
        Rendu.Survoler(fenetre, Rendu.Centre(fenetre, forme));
        Rendu.Battre();
        Assert.True(bloc.IsEditing);
        Assert.Same(deplacement, couche.Cursor);
    }
}
