using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;
using Xunit.Abstractions;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Entrer dans le texte d'un clic, le curseur au point visé — le poste N12 (fiche 10 ; fiche 23,
/// a2 à a5 et a10), sur la décision 3 du propriétaire.
///
/// Un bloc de texte ne s'ouvrait qu'au double-clic, puis tout son texte était sélectionné ; un
/// bloc neuf portait « Saisissez votre texte. », et le menu Insertion le posait sans ouvrir la
/// saisie ; le curseur était la croix de déplacement sur toute sa surface. Publisher pose le
/// point d'insertion au premier clic, à l'endroit visé. Désormais : une pression dans le texte
/// d'un bloc prend ce bloc seul et y pose le curseur, sur le caractère visé de la composition
/// qui était à l'écran ; un glissé y sélectionne du texte ; la bande de bord, cinq pixels
/// d'écran, prend et déplace le bloc ; Ctrl et Maj ajoutent ou retirent sans saisie ; le
/// double-clic prend un mot ; un bloc neuf est vide, le curseur dedans.
///
/// Ses gestes sont ceux du banc (<see cref="Rendu"/>, poste N1). Les points visés sont calculés
/// sur le modèle — la géométrie du texte (N3), puis <see cref="Frame.ToPage"/> —, et non relus
/// à l'écran : un test qui refait le calcul de la planche ne prouverait rien d'elle.
/// </summary>
public class ClicDansLeTexteTests(ITestOutputHelper sortie)
{
    // ------------------------------------------------------------------ le banc du poste

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static TextFrame Texte(string nom = "Texte", double x = 60, double y = 60, string texte = "Bonjour le monde",
                                   double rotation = 0) => new()
    {
        Name = nom, X = x, Y = y, Width = 300, Height = 80, Text = texte, Rotation = rotation,
        FontFamily = "DejaVu Sans", FontSize = 14, Foreground = "#FF000000",
    };

    private static ShapeFrame Forme() => new()
    {
        Name = "Forme", X = 100, Y = 400, Width = 120, Height = 100, Fill = new SolidFill("#FF336699"),
    };

    private static CoucheDeSaisie Couche(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<CoucheDeSaisie>().Single(c => c.Name == "InputLayer");

    /// <summary>La géométrie du texte du bloc, telle que la planche la compose (poste N3).</summary>
    private static GeometrieDuTexte Geometrie(Window fenetre, TextFrame bloc) =>
        fenetre.GetVisualDescendants().OfType<TexteCompose>().Single(c => ReferenceEquals(c.Bloc, bloc)).Geometrie()
        ?? throw new InvalidOperationException($"« {bloc.Name} » n'a pas de géométrie");

    /// <summary>
    /// Le point du caractère <paramref name="indice"/> de la composition, dans le repère du bloc
    /// non tourné : à la fraction <paramref name="fx"/> de sa largeur, à mi-chemin du sommet de
    /// sa ligne et de sa ligne de base.
    /// </summary>
    private static Point DansLeCaractere(GeometrieDuTexte geometrie, int indice, double fx)
    {
        var avant = geometrie.PointDe(indice);
        var apres = geometrie.PointDe(indice + 1);
        var ligne = geometrie.LigneDe(indice);
        Assert.Equal(ligne, geometrie.LigneDe(indice + 1));
        return new Point(avant.X + fx * (apres.X - avant.X), (ligne.Haut + ligne.LigneDeBase) / 2);
    }

    /// <summary>
    /// Le même point dans la fenêtre : passé à la page par <see cref="Frame.ToPage"/>, rotation
    /// comprise, puis à la fenêtre par <see cref="Rendu.SurLaPage"/>.
    /// </summary>
    private static Point SurLeCaractere(Window fenetre, TextFrame bloc, int indice, double fx = 0.3)
    {
        var local = DansLeCaractere(Geometrie(fenetre, bloc), indice, fx);
        var (x, y) = bloc.ToPage(local.X, local.Y);
        return Rendu.SurLaPage(fenetre, x, y);
    }

    /// <summary>Le curseur et la sélection de la zone : (début, fin).</summary>
    private static (int, int) Selection(TextBox zone) => (zone.SelectionStart, zone.SelectionEnd);

    /// <summary>Referme la saisie et vide la sélection, comme deux Échap.</summary>
    private static void Sortir(Window fenetre, MainWindowViewModel vm)
    {
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        vm.SetSelection([]);
        Rendu.UneImage();
    }

    // ------------------------------------------------------------------ le clic

    /// <summary>
    /// La recette : un clic sur le 5ᵉ caractère d'un bloc non sélectionné donne ce bloc seul, en
    /// saisie, le curseur à 4 — rien de sélectionné, la zone au focus, et le document intact.
    /// Un autre bloc était pris : le clic le lâche.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Un_clic_sur_le_cinquieme_caractere_d_un_bloc_non_selectionne_y_pose_le_curseur()
    {
        var bloc = Texte();
        var autre = Texte("Autre", 60, 200);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, autre));
        vm.SelectOnly(autre);
        Rendu.Battre();
        var avant = Etat(vm);

        Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, bloc, 4));

        Assert.Equal([bloc], vm.Selection);
        Assert.True(bloc.IsEditing, "le clic dans le texte n'a pas ouvert la saisie");
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
        Assert.True(zone.IsFocused, "la zone de saisie n'a pas le focus");
        Assert.Equal(4, zone.CaretIndex);
        Assert.Equal((4, 4), Selection(zone));
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "entrer dans le texte a empilé un pas");
    }

    /// <summary>
    /// La même recette sur un bloc tourné de 30° : le point entre dans le repère du bloc, et le
    /// curseur tombe sur le caractère visé — le 5ᵉ, puis le 12ᵉ, le « m » de « monde ».
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Sur_un_bloc_tourne_de_30_degres_le_clic_pose_le_curseur_au_caractere_vise()
    {
        var bloc = Texte(x: 120, y: 160, rotation: 30);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        foreach (var indice in new[] { 4, 11 })
        {
            Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, bloc, indice));

            Assert.Same(bloc, vm.SingleSelected);
            Assert.True(bloc.IsEditing, $"le clic sur le caractère {indice} du bloc tourné n'a pas ouvert la saisie");
            var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
            Assert.Equal((indice, indice), Selection(zone));
            Assert.Equal(30, bloc.Rotation);
            Sortir(fenetre, vm);
        }

        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// Sur ATA 21 importé, chaque bloc de texte qu'un clic atteint en son centre : un caractère
    /// repéré par N3 — le deuxième d'un morceau de la ligne du milieu, assez large pour être visé —
    /// reçoit le curseur. Le document reste intact au caractère près.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Sur_ATA_21_le_clic_pose_le_curseur_sur_le_caractere_que_N3_repere()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Ata21());
        var page = vm.CurrentPage!;
        var avant = Etat(vm);
        var joues = 0;

        foreach (var bloc in page.Frames.OfType<TextFrame>().Where(b => b.Visible && !b.HasField).ToList())
        {
            var geometrie = Geometrie(fenetre, bloc);
            var vise = Candidats(geometrie, bloc).FirstOrDefault(c => ReferenceEquals(bloc, page.Frames.LastOrDefault(
                f => f.Visible && f.HitTest(c.Page.X, c.Page.Y))));
            if (vise.Page == default) continue;

            Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, vise.Page.X, vise.Page.Y));
            Assert.Same(bloc, vm.SingleSelected);
            Assert.True(bloc.IsEditing, $"« {bloc.Name} » : le clic n'a pas ouvert la saisie");
            var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
            Assert.True((vise.Indice, vise.Indice) == Selection(zone),
                $"« {bloc.Name} » : le curseur est à {Selection(zone)}, le caractère visé à {vise.Indice} " +
                $"(« {bloc.Text!.Substring(vise.Indice, 1)} »)");
            sortie.WriteLine($"« {bloc.Name} » : curseur à {vise.Indice}, « {bloc.Text!.Substring(vise.Indice, 1)} »");
            joues++;

            Sortir(fenetre, vm);
            Assert.Equal(avant, Etat(vm));
        }

        Assert.True(joues >= 5, $"{joues} blocs d'ATA 21 joués seulement");
        Assert.False(vm.CanUndo);
        Assert.False(vm.IsDirty);
    }

    /// <summary>
    /// Les caractères d'un bloc qu'on peut viser sans ambiguïté, du milieu du bloc vers ses bords :
    /// le deuxième caractère de chaque morceau, s'il fait au moins quatre points de large, visé au
    /// tiers de sa largeur, hors de la bande de bord.
    /// </summary>
    private static IEnumerable<(int Indice, Point Page)> Candidats(GeometrieDuTexte geometrie, TextFrame bloc)
    {
        var milieu = bloc.Height / 2;
        foreach (var posee in geometrie.Lignes.OrderBy(l => Math.Abs(l.LigneDeBase - milieu)))
        {
            foreach (var morceau in posee.Ligne.Morceaux.Where(m => m.Texte.Length >= 3 && char.IsLetter(m.Texte[1])))
            {
                var indice = morceau.Debut + 1;
                if (geometrie.LigneDe(indice + 1) != posee) continue;
                var largeur = geometrie.PointDe(indice + 1).X - geometrie.PointDe(indice).X;
                if (largeur < 4) continue;

                var local = DansLeCaractere(geometrie, indice, 1.0 / 3);
                if (local.X < 6 || local.X > bloc.Width - 6 || local.Y < 6 || local.Y > bloc.Height - 6) continue;
                var (x, y) = bloc.ToPage(local.X, local.Y);
                yield return (indice, new Point(x, y));
            }
        }
    }

    // ------------------------------------------------------------------ le glissé

    /// <summary>
    /// La recette : un glissé du milieu du 3ᵉ au milieu du 7ᵉ caractère sélectionne [2, 7). La
    /// pression ouvre la saisie, l'ancre au début du 3ᵉ ; la sélection suit le pointeur dans le
    /// champ. Un milieu exact est à égale distance de deux positions : la pression vise juste
    /// avant le sien, le relâché juste après, comme une main qui couvre le 3ᵉ et le 7ᵉ.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Un_glisse_du_milieu_du_3e_au_milieu_du_7e_caractere_selectionne_de_2_a_7()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        Rendu.Glisser(fenetre, SurLeCaractere(fenetre, bloc, 2, 0.45), SurLeCaractere(fenetre, bloc, 6, 0.55));

        Assert.True(bloc.IsEditing);
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
        Assert.Equal((2, 7), Selection(zone));
        Assert.Equal("njour", zone.SelectedText);
        Assert.Equal(avant, Etat(vm));
        Assert.Equal(bloc.X, 60);

        // Le même glissé à rebours : l'ancre reste où la pression l'a posée.
        Sortir(fenetre, vm);
        Rendu.Glisser(fenetre, SurLeCaractere(fenetre, bloc, 6, 0.55), SurLeCaractere(fenetre, bloc, 2, 0.45));
        zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
        Assert.Equal((7, 2), Selection(zone));
        Assert.Equal("njour", zone.SelectedText);
    }

    /// <summary>
    /// Un glissé de moins de 3 px ne sélectionne rien (le seuil de Q21) : un clic qui tremble de
    /// deux pixels par-dessus la fin d'un caractère laisse le curseur où la pression l'a posé.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Un_glisse_de_moins_de_3_px_dans_le_texte_ne_selectionne_rien()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        Rendu.Trembler(fenetre, SurLeCaractere(fenetre, bloc, 4, 0.1), new Vector(2.5, 0));
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
        Assert.Equal((4, 4), Selection(zone));

        // Au-delà du seuil, le même geste sélectionne.
        Sortir(fenetre, vm);
        Rendu.Glisser(fenetre, SurLeCaractere(fenetre, bloc, 4, 0.1), SurLeCaractere(fenetre, bloc, 5, 0.9));
        zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
        Assert.Equal((4, 6), Selection(zone));
    }

    // ------------------------------------------------------------------ le bord

    /// <summary>
    /// La recette : un glissé de 1 pt par le bord, Alt tenu, déplace le bloc, en un pas, sans
    /// ouvrir la saisie — mené au-delà du seuil puis ramené (Q21).
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Un_glisse_d_un_point_par_le_bord_Alt_tenu_deplace_le_bloc_en_un_pas()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        foreach (var (fx, fy) in new[] { (0.0, 0.5), (1.0, 0.5), (0.5, 0.0), (0.5, 1.0) })
        {
            var prise = Rendu.Bord(fenetre, bloc, fx, fy);
            Rendu.GlisserEtRamener(fenetre, prise, prise + new Vector(0, 1), RawInputModifiers.Alt);

            Assert.False(bloc.IsEditing, $"le bord ({fx}, {fy}) a ouvert la saisie");
            Assert.Same(bloc, vm.SingleSelected);
            Assert.Equal((60.0, 61.0), (bloc.X, bloc.Y));
            bloc = Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, $"le glissé par le bord ({fx}, {fy})"));
            vm.SetSelection([]);
            Rendu.UneImage();
        }
    }

    /// <summary>
    /// Pour un bloc sélectionné, la bande de bord s'étend jusqu'au cadre de sélection, posé
    /// 3,25 px d'écran hors du bloc : une pression entre le bloc et son cadre le prend — à un
    /// quart de sa hauteur, loin des poignées, qui passent avant. Hors sélection, la même
    /// pression tombe à côté du bloc, et vide la sélection.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void La_bande_de_bord_d_un_bloc_selectionne_va_jusqu_a_son_cadre()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);
        var dehors = Rendu.SurLaPage(fenetre, bloc.X - 2, bloc.Y + 20);

        Rendu.Cliquer(fenetre, dehors);
        Assert.Empty(vm.Selection);

        vm.SelectOnly(bloc);
        Rendu.UneImage();
        Rendu.GlisserEtRamener(fenetre, dehors, dehors + new Vector(0, 1), RawInputModifiers.Alt);
        Assert.Same(bloc, vm.SingleSelected);
        Assert.False(bloc.IsEditing);
        Assert.Equal((60.0, 61.0), (bloc.X, bloc.Y));
        Rendu.UnSeulPas(vm, avant, "le glissé par le cadre");
    }

    /// <summary>
    /// Le curseur : le I sur le texte, les quatre flèches sur la bande de bord — celles d'une
    /// forme —, et sur le cadre d'un bloc sélectionné ; la flèche dans le vide.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Le_curseur_est_le_I_sur_le_texte_et_les_quatre_fleches_sur_le_bord()
    {
        var bloc = Texte();
        var forme = Forme();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, forme));
        var couche = Couche(fenetre);

        Cursor? Sur(Point point)
        {
            Rendu.Survoler(fenetre, point);
            Rendu.Battre();
            return couche.Cursor;
        }

        var deplacement = Sur(Rendu.Centre(fenetre, forme));
        var fleche = Sur(Rendu.SurLaPage(fenetre, 450, 600));
        var texte = Sur(SurLeCaractere(fenetre, bloc, 4));
        Assert.NotNull(texte);
        Assert.NotSame(deplacement, texte);
        Assert.NotSame(fleche, texte);
        Assert.Same(deplacement, Sur(Rendu.Bord(fenetre, bloc)));
        Assert.Same(deplacement, Sur(Rendu.Bord(fenetre, bloc, 0.5, 1)));
        Assert.Same(texte, Sur(Rendu.Centre(fenetre, bloc)));

        var dehors = Rendu.SurLaPage(fenetre, bloc.X - 2, bloc.Y + 20);
        Assert.Same(fleche, Sur(dehors));
        vm.SelectOnly(bloc);
        Rendu.UneImage();
        Assert.Same(deplacement, Sur(dehors));
    }

    // ------------------------------------------------------------------ Ctrl, Maj, le double-clic

    /// <summary>
    /// La recette : Ctrl+clic dans le texte d'un bloc non sélectionné l'ajoute à la sélection,
    /// sans saisie ; Maj+clic de même ; Ctrl+clic sur un bloc pris l'en retire.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Ctrl_clic_et_Maj_clic_dans_le_texte_ajoutent_le_bloc_sans_saisie()
    {
        var a = Texte("A", 60, 60);
        var b = Texte("B", 60, 200);
        var c = Texte("C", 60, 340);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(a, b, c));
        vm.SelectOnly(a);
        Rendu.Battre();

        Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, b, 4), RawInputModifiers.Control);
        Assert.Equal([a, b], vm.Selection.OrderBy(f => f.Name));
        Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, c, 4), RawInputModifiers.Shift);
        Assert.Equal([a, b, c], vm.Selection.OrderBy(f => f.Name));
        Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, b, 8), RawInputModifiers.Control);
        Assert.Equal([a, c], vm.Selection.OrderBy(f => f.Name));

        Assert.DoesNotContain(new[] { a, b, c }, bloc => bloc.IsEditing);
        Assert.False(fenetre.GetVisualDescendants().OfType<PageCanvas>().Single().IsEditingText);
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// Dans le texte en saisie, Maj+clic étend la sélection depuis le curseur : le champ reçoit
    /// lui-même ce clic (poste N5).
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Dans_le_texte_en_saisie_Maj_clic_etend_la_selection()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, bloc, 2, 0.1));
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
        Assert.Equal((2, 2), Selection(zone));

        Rendu.Cliquer(fenetre, Rendu.BordDuCaractere(zone, 10), RawInputModifiers.Shift);
        Assert.True(bloc.IsEditing);
        Assert.Equal((2, 10), Selection(zone));
        Assert.Equal("njour le", zone.SelectedText);
    }

    /// <summary>
    /// La recette : un double-clic sur un mot sélectionne le mot. Le premier clic ouvre la saisie,
    /// le second atteint le champ, qui prend le mot ; il n'ouvre rien de plus. La recette le joue
    /// vingt fois, chacune dans un processus neuf : Avalonia reconnaît un double-clic à l'horloge,
    /// deux pressions à moins de 500 ms, et la première d'un processus est la plus lente. Une
    /// entrée dans un autre bloc la précède, comme une main qui a déjà travaillé.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Un_double_clic_sur_un_mot_selectionne_le_mot()
    {
        var bloc = Texte();
        var autre = Texte("Autre", 60, 200, "Un autre bloc");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, autre));
        Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, autre, 4));
        Sortir(fenetre, vm);
        var avant = Etat(vm);

        Rendu.DoubleCliquer(fenetre, SurLeCaractere(fenetre, bloc, 13, 0.5));

        Assert.True(bloc.IsEditing, "le double-clic n'a pas ouvert la saisie");
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
        Assert.Equal("monde", zone.SelectedText);
        Assert.Equal((11, 16), Selection(zone));
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo);
    }

    // ------------------------------------------------------------------ les pas, les blocs neufs

    /// <summary>
    /// La recette : entrer puis sortir sans taper n'empile aucun pas ; taper en empile un.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Entrer_puis_sortir_sans_taper_n_empile_aucun_pas_et_taper_en_empile_un()
    {
        var bloc = Texte();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = Etat(vm);

        Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, bloc, 7, 0.1));
        Assert.True(bloc.IsEditing);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(bloc.IsEditing);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "entrer puis sortir sans taper a empilé un pas");
        Assert.False(vm.IsDirty);

        Rendu.Cliquer(fenetre, SurLeCaractere(fenetre, bloc, 7, 0.1));
        Rendu.Taper(fenetre, ",");
        Assert.Equal("Bonjour, le monde", bloc.Text);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UnSeulPas(vm, avant, "la frappe");
    }

    /// <summary>
    /// La recette : un bloc tracé à l'outil est vide, et la frappe y entre. De même pour l'outil
    /// Titre.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Un_bloc_trace_a_l_outil_est_vide_et_la_frappe_y_entre()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille());

        foreach (var (outil, y) in new[] { (EditorTool.Text, 100.0), (EditorTool.Heading, 300.0) })
        {
            vm.ActiveTool = outil;
            Rendu.Battre();
            Rendu.Glisser(fenetre, Rendu.SurLaPage(fenetre, 80, y), Rendu.SurLaPage(fenetre, 380, y + 90));

            var bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
            Assert.Equal("", bloc.Text);
            Assert.True(bloc.IsEditing, $"le bloc tracé à l'outil {outil} n'est pas en saisie");
            Assert.True(Rendu.ZoneDeSaisie(fenetre, bloc)!.IsFocused);

            Rendu.Taper(fenetre, "Salut");
            Assert.Equal("Salut", bloc.Text);
            Rendu.Touche(fenetre, PhysicalKey.Escape);
            vm.SetSelection([]);
            Rendu.UneImage();
        }
    }

    /// <summary>
    /// Insertion ▸ Bloc de texte ouvre la saisie comme l'outil : le bloc posé est vide, sa zone
    /// a le focus, et la frappe y entre. L'entrée est cherchée par sa commande, non par son
    /// libellé, que les menus de Publisher (N13) reprennent.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Insertion_Bloc_de_texte_ouvre_la_saisie_d_un_bloc_vide()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Forme()));
        var menus = fenetre.GetVisualDescendants().OfType<Menu>().Single(m => m.Name == "Menus");
        var insertion = menus.Items.OfType<MenuItem>()
            .Single(m => m.Items.OfType<MenuItem>().Any(e => ReferenceEquals(e.Command, vm.AddTextCommand)));

        Rendu.Cliquer(insertion);
        Rendu.Cliquer(insertion.Items.OfType<MenuItem>().Single(e => ReferenceEquals(e.Command, vm.AddTextCommand)));
        Rendu.UneImage();

        var bloc = Assert.IsType<TextFrame>(vm.SingleSelected);
        Assert.Equal("", bloc.Text);
        Assert.True(bloc.IsEditing, "Insertion ▸ Bloc de texte n'a pas ouvert la saisie");
        Assert.True(Rendu.ZoneDeSaisie(fenetre, bloc)!.IsFocused, "la zone du bloc inséré n'a pas le focus");

        Rendu.Taper(fenetre, "Salut");
        Assert.Equal("Salut", bloc.Text);
    }

    /// <summary>
    /// Un bloc à champ n'a pas de texte où entrer : toute sa surface est bord. Un clic en son
    /// centre le prend, sans saisie, et le pointeur y montre les quatre flèches.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N12")]
    public void Un_bloc_a_champ_se_prend_partout_sans_saisie()
    {
        var bloc = Texte();
        bloc.Field = FieldKind.PageNumber;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        Rendu.Cliquer(fenetre, Rendu.Centre(fenetre, bloc));
        Assert.Same(bloc, vm.SingleSelected);
        Assert.False(bloc.IsEditing);
    }
}
