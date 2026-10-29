using System;
using System.Collections.Generic;
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
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les menus de Publisher — poste N13 (fiche 23, d3, d23, e4, e7, e10, e21).
///
/// Avant le poste, les menus portaient des lettres d'accès en double — Disposition : G quatre
/// fois, D, H et L deux fois ; Insertion : T deux fois ; Texte : G deux fois —, qui font tourner
/// le focus d'une entrée à l'autre sans rien déclencher, et « _Édition » était injoignable : la
/// lettre d'accès se compare au nom de la touche, et aucune ne s'appelle « É ». Disposition
/// n'était pas rangé comme Publisher, qui n'avait pas son « Regrouper », et Tab, sur la planche,
/// promenait le focus d'Avalonia là où Publisher passe d'un objet à l'autre.
///
/// Repris le 29 octobre 2026, sur le choix du propriétaire : la lettre d'accès se cherche au seul
/// niveau ouvert, comme Windows (Avalonia 11.3.9 la cherchait sur la barre et sur chaque niveau
/// ouvert à la fois), et les menus écrivent leurs gestes en français, « Ctrl+Maj+G », comme les
/// bulles.
///
/// Tout se joue sur la vraie fenêtre, aux gestes du banc (poste N1) : les menus au clavier, Alt
/// relâché puis les lettres, comme une main ; Regrouper au pointeur, par le menu ; Tab sur la
/// planche et dans le dock. La recette du poste — <c>Scripts/recettes/N13-menus-de-publisher.sh</c>
/// — joue ces tests par leur trait.
/// </summary>
[Trait("poste", "N13")]
public class MenusDePublisherTests
{
    // ------------------------------------------------------------------ outillage

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static ShapeFrame Carre(string nom, double x, double y) =>
        new() { Name = nom, X = x, Y = y, Width = 60, Height = 60, Fill = new SolidFill("#FFCC0000") };

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    /// <summary>La barre de menus, et ses menus de premier niveau.</summary>
    private static Menu Barre(Window fenetre) => Trouver<Menu>(fenetre, "Menus");

    /// <summary>Une entrée de menu, par son libellé : hors de l'arbre visuel tant que son menu est replié.</summary>
    private static MenuItem Menu(Window fenetre, string libelle) =>
        fenetre.GetLogicalDescendants().OfType<MenuItem>().SingleOrDefault(m => Equals(m.Header, libelle))
        ?? throw new InvalidOperationException($"le menu n'a pas d'entrée « {libelle} »");

    /// <summary>
    /// La lettre d'accès d'un libellé, telle qu'Avalonia la lit : le caractère qui suit le premier
    /// « _ » seul — « __ » écrit un trait. <c>null</c> sans lettre d'accès.
    /// </summary>
    private static char? LettreDAcces(string libelle)
    {
        for (var i = 0; i < libelle.Length - 1; i++)
        {
            if (libelle[i] != '_') continue;
            if (libelle[i + 1] == '_') { i++; continue; }
            return libelle[i + 1];
        }

        return null;
    }

    private static string[] Pile(MainWindowViewModel vm) => vm.CurrentPage!.Frames.Select(bloc => bloc.Name).ToArray();

    // ------------------------------------------------------------- les lettres d'accès

    /// <summary>
    /// Chaque niveau de chaque menu, sous-menus et documents récents compris, a ses lettres
    /// d'accès à lui : une par entrée, sans doublon, aucune accentuée — une lettre accentuée ne
    /// se tape pas sous Alt, et une lettre en double fait tourner le focus sans rien déclencher.
    /// La barre elle-même aussi : « _Edition », sans accent.
    /// </summary>
    [AvaloniaFact]
    public void Chaque_niveau_de_menu_a_des_lettres_d_acces_uniques_et_sans_accent()
    {
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 60, 60)));

        var niveaux = new List<(string Niveau, IReadOnlyList<MenuItem> Entrees)>
        {
            ("la barre", Barre(fenetre).Items.OfType<MenuItem>().ToList()),
        };
        var aParcourir = new Queue<MenuItem>(niveaux[0].Entrees);
        while (aParcourir.Count > 0)
        {
            var menu = aParcourir.Dequeue();
            var entrees = menu.Items.OfType<MenuItem>().Where(e => e.IsVisible).ToList();
            if (entrees.Count == 0) continue;
            niveaux.Add(($"{menu.Header}", entrees));
            foreach (var entree in entrees) aParcourir.Enqueue(entree);
        }

        Assert.True(niveaux.Count >= 10, $"{niveaux.Count} niveaux de menu seulement");
        foreach (var (niveau, entrees) in niveaux)
        {
            var lettres = entrees.Select(e => (Libelle: $"{e.Header}", Lettre: LettreDAcces($"{e.Header}"))).ToList();
            foreach (var (libelle, lettre) in lettres)
            {
                Assert.True(lettre is { } l && (char.IsAsciiLetterOrDigit(l)),
                    $"« {libelle} », dans {niveau}, n'a pas de lettre d'accès sans accent");
            }

            var doublons = lettres.GroupBy(x => char.ToUpperInvariant(x.Lettre!.Value)).Where(g => g.Count() > 1)
                .Select(g => $"{g.Key} ({string.Join(", ", g.Select(x => x.Libelle))})").ToList();
            Assert.True(doublons.Count == 0, $"{niveau} : lettres d'accès en double — {string.Join(" ; ", doublons)}");
        }

        Assert.Equal(new[] { "_Fichier", "_Edition", "_Insertion", "_Disposition", "_Texte", "_Page", "_Affichage", "Fe_nêtre" },
            Barre(fenetre).Items.OfType<MenuItem>().Select(m => m.Header as string));
    }

    /// <summary>
    /// Alt relâché, puis E : le menu Edition s'ouvre — il était injoignable sous « _Édition », et
    /// E, lettre d'outil, armait l'ellipse sous la barre de menus ouverte ; de même Alt, puis T,
    /// ouvre Texte au lieu d'armer l'outil Texte. Alt tenu avec la lettre, de même.
    /// </summary>
    [AvaloniaFact]
    public void Alt_puis_E_ouvre_le_menu_Edition_sans_armer_d_outil()
    {
        var bloc = Carre("A", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        // Le clic donne le focus à la planche : sans élément qui ait le focus, Avalonia ne lit
        // pas Alt (AccessKeyHandler.IsFocusWithinOwner).
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 70, 70));
        Assert.Same(bloc, vm.Selected);

        foreach (var (lettre, menu) in new[] { (PhysicalKey.E, "_Edition"), (PhysicalKey.T, "_Texte") })
        {
            Rendu.Touche(fenetre, PhysicalKey.AltLeft, relacher: true);   // Alt seul, relâché : la barre prend la main
            Rendu.Touche(fenetre, lettre);
            Assert.True(Menu(fenetre, menu).IsSubMenuOpen, $"Alt puis {lettre} n'a pas ouvert « {menu} »");
            Assert.Equal(EditorTool.Select, vm.ActiveTool);
            Rendu.Touche(fenetre, PhysicalKey.Escape);
            Rendu.Touche(fenetre, PhysicalKey.Escape);
            Assert.False(Barre(fenetre).IsOpen, "Échap n'a pas refermé la barre de menus");
        }

        Rendu.Touche(fenetre, PhysicalKey.E, RawInputModifiers.Alt);
        Assert.True(Menu(fenetre, "_Edition").IsSubMenuOpen, "Alt+E n'a pas ouvert le menu Edition");
        Assert.Equal(EditorTool.Select, vm.ActiveTool);
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// La recette : Alt, D, O, P — Disposition, Ordre, Premier plan —, comme une main sur
    /// Publisher : le bloc du fond passe devant, en un pas, et les menus se referment. Avalonia
    /// 11.3.9 cherchait P sur la barre et sur chaque niveau ouvert — « _Page », « Re_pères de
    /// règle », « Premier _plan » — et ne faisait que promener le focus ; la lettre se cherche
    /// désormais au seul niveau ouvert (repris le 29 octobre 2026). Suppr, la barre ouverte, ne
    /// supprime pas le bloc ; Alt, D, O, L joue Arrière-plan de même.
    /// </summary>
    [AvaloniaFact]
    public void Alt_D_O_P_porte_le_bloc_au_premier_plan()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 60, 60), Carre("B", 90, 90), Carre("C", 120, 120)));
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 70, 70));
        Assert.Equal("A", vm.Selected?.Name);
        var avant = Etat(vm);

        Rendu.Touche(fenetre, PhysicalKey.AltLeft, relacher: true);   // Alt seul, relâché : la barre prend la main
        Rendu.Touche(fenetre, PhysicalKey.D);
        Assert.True(Menu(fenetre, "_Disposition").IsSubMenuOpen, "Alt puis D n'a pas ouvert le menu Disposition");
        Rendu.Touche(fenetre, PhysicalKey.Delete);
        Assert.Equal(avant, Etat(vm));
        Rendu.Touche(fenetre, PhysicalKey.O);
        Assert.True(Menu(fenetre, "_Ordre").IsSubMenuOpen, "O n'a pas ouvert le sous-menu Ordre");
        Rendu.Touche(fenetre, PhysicalKey.P);

        Assert.Equal(new[] { "B", "C", "A" }, Pile(vm));
        Assert.False(Barre(fenetre).IsOpen, "la barre de menus est restée ouverte");
        Assert.Equal("A", Rendu.UnSeulPas(vm, avant, "Alt, D, O, P")?.Name);

        // Alt, D, O, L : Arrière-plan, sans rivale non plus.
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 170, 170));
        Assert.Equal("C", vm.Selected?.Name);
        foreach (var lettre in new[] { PhysicalKey.D, PhysicalKey.O, PhysicalKey.L })
        {
            if (lettre == PhysicalKey.D) Rendu.Touche(fenetre, PhysicalKey.AltLeft, relacher: true);
            Rendu.Touche(fenetre, lettre);
        }

        Assert.Equal(new[] { "C", "A", "B" }, Pile(vm));
    }

    /// <summary>
    /// La recette : Edition ouvert, A joue Annuler — et non Affichage, dont la barre, toujours à
    /// l'écran, porte aussi le A.
    /// </summary>
    [AvaloniaFact]
    public void Edition_ouvert_A_annule_et_n_ouvre_pas_Affichage()
    {
        var bloc = Carre("A", 60, 60);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 70, 70));
        var avant = Etat(vm);
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        Assert.NotEqual(avant, Etat(vm));

        Rendu.Touche(fenetre, PhysicalKey.AltLeft, relacher: true);
        Rendu.Touche(fenetre, PhysicalKey.E);
        Rendu.Touche(fenetre, PhysicalKey.A);

        Assert.Equal(avant, Etat(vm));
        Assert.False(Menu(fenetre, "_Affichage").IsSubMenuOpen, "A a ouvert Affichage");
        Assert.False(Barre(fenetre).IsOpen, "la barre de menus est restée ouverte");
    }

    /// <summary>
    /// Deux entrées d'un même niveau sur une lettre : la touche passe le focus de l'une à l'autre,
    /// et ne joue rien, comme Windows. Une lettre que le niveau n'a pas est prise, et ne fait rien
    /// — ni outil armé, ni menu de la barre ouvert.
    /// </summary>
    [AvaloniaFact]
    public void Une_lettre_partagee_au_niveau_ouvert_promene_le_focus_sans_rien_jouer()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 60, 60), Carre("B", 200, 60)));
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 70, 70));
        vm.SelectAllCommand.Execute(null);
        var avant = Etat(vm);
        var jouee = 0;
        var gamma = new MenuItem { Header = "_Gamma", Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => jouee++) };
        Menu(fenetre, "_Disposition").Items.Add(gamma);

        Rendu.Touche(fenetre, PhysicalKey.AltLeft, relacher: true);
        Rendu.Touche(fenetre, PhysicalKey.D);
        Rendu.Touche(fenetre, PhysicalKey.G);
        Assert.True(Menu(fenetre, "_Grouper").IsFocused, "G n'a pas mené le focus à « _Grouper »");
        Rendu.Touche(fenetre, PhysicalKey.G);
        Assert.True(gamma.IsFocused, "le second G n'a pas mené le focus à « _Gamma »");
        Rendu.Touche(fenetre, PhysicalKey.G);
        Assert.True(Menu(fenetre, "_Grouper").IsFocused, "le troisième G n'est pas revenu à « _Grouper »");
        Assert.Equal(0, jouee);
        Assert.Equal(avant, Etat(vm));
        Assert.True(Menu(fenetre, "_Disposition").IsSubMenuOpen);

        // F, que Disposition n'a pas : ni Fichier ouvert, ni rien d'autre.
        Rendu.Touche(fenetre, PhysicalKey.F);
        Assert.False(Menu(fenetre, "_Fichier").IsSubMenuOpen, "F a ouvert Fichier depuis Disposition");
        Assert.True(Menu(fenetre, "_Disposition").IsSubMenuOpen);
        Assert.Equal(EditorTool.Select, vm.ActiveTool);
    }

    /// <summary>
    /// Les documents récents (N11), « _1 » à « _6 », se joignent au clavier : la lettre se compare
    /// au chiffre de la touche, que l'AZERTY donne sans Maj — « é » sur la touche du 2 — et
    /// qu'Avalonia lisait « D2 ».
    /// </summary>
    [AvaloniaFact]
    public void Alt_F_puis_le_chiffre_ouvre_un_document_recent()
    {
        var dossier = Directory.CreateTempSubdirectory("mypublisher-n13-");
        try
        {
            var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 60, 60)));
            var premier = Path.Combine(dossier.FullName, "premier.mypub");
            var second = Path.Combine(dossier.FullName, "second.mypub");
            vm.SaveTo(premier);
            vm.SaveTo(second);
            Rendu.Battre();
            Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 70, 70));

            Rendu.Touche(fenetre, PhysicalKey.AltLeft, relacher: true);
            Rendu.Touche(fenetre, PhysicalKey.F);
            Assert.True(Menu(fenetre, "_Fichier").IsSubMenuOpen, "Alt puis F n'a pas ouvert Fichier");
            Assert.Equal("_2 premier.mypub", Menu(fenetre, "_2 premier.mypub").Header);
            Rendu.ToucheAzerty(fenetre, PhysicalKey.Digit2);
            Rendu.Battre(8);

            Assert.Equal(premier, vm.FilePath);
            Assert.False(Barre(fenetre).IsOpen, "la barre de menus est restée ouverte");
        }
        finally
        {
            dossier.Delete(recursive: true);
        }
    }

    // ------------------------------------------------------------- les gestes en français

    /// <summary>Le texte que l'entrée montre pour son geste, une fois son gabarit appliqué.</summary>
    private static string? GesteAffiche(MenuItem entree) =>
        entree.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_InputGestureText").Text;

    /// <summary>
    /// La recette : le menu Disposition affiche « Ctrl+Maj+G », comme les bulles, et non
    /// « Ctrl+Shift+G » ; Edition « Suppr » et non « Delete », Affichage « Ctrl++ ». Le texte
    /// suit un geste qui change.
    /// </summary>
    [AvaloniaFact]
    public void Les_menus_ecrivent_les_gestes_en_francais()
    {
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 60, 60)));

        Rendu.Cliquer(Menu(fenetre, "_Disposition"));
        var grouper = Menu(fenetre, "_Grouper");
        Assert.Equal("Ctrl+Maj+G", GesteAffiche(grouper));
        Assert.Equal("Ctrl+Maj+G", GesteAffiche(Menu(fenetre, "_Dissocier")));
        Rendu.Cliquer(Menu(fenetre, "_Ordre"));
        Assert.Equal("Ctrl+Maj+F", GesteAffiche(Menu(fenetre, "Premier _plan")));

        grouper.InputGesture = KeyGesture.Parse("Ctrl+Alt+Shift+Delete");
        Rendu.Battre();
        Assert.Equal("Ctrl+Maj+Alt+Suppr", GesteAffiche(grouper));
        grouper.InputGesture = null;
        Rendu.Battre();
        Assert.True(string.IsNullOrEmpty(GesteAffiche(grouper)), "le geste retiré se montre encore");
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.Touche(fenetre, PhysicalKey.Escape);

        Rendu.Cliquer(Menu(fenetre, "_Edition"));
        Assert.Equal("Suppr", GesteAffiche(Menu(fenetre, "_Supprimer")));
        Assert.Equal("Ctrl+Z", GesteAffiche(Menu(fenetre, "_Annuler")));
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.Touche(fenetre, PhysicalKey.Escape);

        Rendu.Cliquer(Menu(fenetre, "_Affichage"));
        Assert.Equal("Ctrl++", GesteAffiche(Menu(fenetre, "Zoom _avant")));
        Assert.Equal("Ctrl+−", GesteAffiche(Menu(fenetre, "Zoom a_rrière")));
        Assert.Equal("Ctrl+0", GesteAffiche(Menu(fenetre, "_Taille réelle")));
        Assert.Equal("Ctrl+Maj+L", GesteAffiche(Menu(fenetre, "Page _entière")));
    }

    // ------------------------------------------------------------------ Disposition

    /// <summary>
    /// Disposition est rangé comme Publisher 2007 (le livre, p. 16) : les repères de règle, les
    /// trois commandes de groupe, « Ordre ▸ » et ses quatre ordres, « Aligner ou répartir ▸ » —
    /// les six alignements, les deux répartitions et leur repère —, puis « Ajuster aux marges ».
    /// Chaque entrée garde sa commande. N18 y a ajouté les repères de mise en page et le quart de
    /// tour, aux places de Publisher.
    /// </summary>
    [AvaloniaFact]
    public void Le_menu_Disposition_est_range_comme_Publisher()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 60, 60)));

        static string[] Libelles(MenuItem menu) => menu.Items.OfType<MenuItem>().Select(e => $"{e.Header}").ToArray();

        // Depuis N18 (règle 14) : « Repères de mise en page… » en tête, comme Layout Guides…, et
        // « Faire pivoter ou retourner ▸ » après l'alignement, comme Rotate or Flip ▸ (p. 16).
        Assert.Equal(new[]
            {
                "Repères de m_ise en page…", "Re_pères de règle", "_Grouper", "_Dissocier", "_Regrouper", "_Ordre",
                "_Aligner ou répartir", "_Faire pivoter ou retourner", "Ajuster aux _marges",
            },
            Libelles(Menu(fenetre, "_Disposition")));
        Assert.Equal(new[] { "Premier _plan", "Arrière-p_lan", "_Avancer", "_Reculer" }, Libelles(Menu(fenetre, "_Ordre")));
        Assert.Equal(new[]
        {
            "Aligner à _gauche", "_Centrer horizontalement", "Aligner à _droite", "Aligner en _haut", "Centrer _verticalement",
            "Aligner en _bas", "Répartir hori_zontalement", "Répartir vertica_lement", "Par rapport aux _marges",
        }, Libelles(Menu(fenetre, "_Aligner ou répartir")));

        Assert.Same(vm.BringToFrontCommand, Menu(fenetre, "Premier _plan").Command);
        Assert.Same(vm.SendToBackCommand, Menu(fenetre, "Arrière-p_lan").Command);
        Assert.Same(vm.AlignLeftCommand, Menu(fenetre, "Aligner à _gauche").Command);
        Assert.Same(vm.DistributeVerticallyCommand, Menu(fenetre, "Répartir vertica_lement").Command);
        Assert.Same(vm.RegrouperCommand, Menu(fenetre, "_Regrouper").Command);
        Assert.Equal(KeyGesture.Parse("Ctrl+Shift+F"), Menu(fenetre, "Premier _plan").InputGesture);
        Assert.Equal(KeyGesture.Parse("Ctrl+Shift+B"), Menu(fenetre, "Arrière-p_lan").InputGesture);

        // La case du repère, déplacée, se coche toujours dans les deux sens.
        Assert.True(vm.AlignToMargins);
        Rendu.Cliquer(Menu(fenetre, "_Disposition"));
        Rendu.Cliquer(Menu(fenetre, "_Aligner ou répartir"));
        Rendu.Cliquer(Menu(fenetre, "Par rapport aux _marges"));
        Assert.False(vm.AlignToMargins);
    }

    /// <summary>
    /// La recette : Regrouper refait le dernier groupe dissous, en un pas — même après qu'un de
    /// ses blocs a bougé, sans qu'on ait à les resélectionner —, et le groupe reprend son nom.
    /// Il ne s'offre ni avant toute dissociation, ni quand un des blocs a quitté la page.
    /// </summary>
    [AvaloniaFact]
    public void Regrouper_refait_le_dernier_groupe_dissous_en_un_pas()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 60, 60), Carre("B", 200, 60), Carre("C", 60, 300)));
        var page = vm.CurrentPage!;
        var regrouper = Menu(fenetre, "_Regrouper");
        Assert.False(vm.RegrouperCommand.CanExecute(null), "Regrouper s'offre avant toute dissociation");

        vm.SetSelection([page.Frames[0], page.Frames[1]]);
        vm.GroupSelectedCommand.Execute(null);
        var groupe = Assert.IsType<GroupFrame>(page.Frames.Single(bloc => bloc is GroupFrame));
        groupe.Name = "Logo";
        vm.UngroupSelectedCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(new[] { "A", "B", "C" }, Pile(vm));

        // Un des blocs bouge, et la sélection passe ailleurs : Regrouper s'en passe.
        vm.SelectOnly(page.Frames.Single(bloc => bloc.Name == "B"));
        vm.NudgeSelection(1, 0, false);
        vm.SelectOnly(page.Frames.Single(bloc => bloc.Name == "C"));
        Rendu.Battre();
        Assert.True(vm.RegrouperCommand.CanExecute(null), "Regrouper n'est pas offert après la dissociation");
        var avant = Etat(vm);

        // Au pointeur, par le menu : l'entrée ne lit sa commande que menu ouvert.
        Rendu.Cliquer(Menu(fenetre, "_Disposition"));
        Assert.True(regrouper.IsEffectivelyEnabled, "Regrouper est grisé au menu");
        Rendu.Cliquer(regrouper);

        var refait = Assert.IsType<GroupFrame>(page.Frames.Single(bloc => bloc is GroupFrame));
        Assert.Equal("Logo", refait.Name);
        Assert.Equal(new[] { "A", "B" }, refait.Children.Select(bloc => bloc.Name));
        Assert.Same(refait, vm.Selected);
        Assert.False(vm.RegrouperCommand.CanExecute(null), "Regrouper s'offre encore, le groupe refait");
        // Un pas, et un seul : une annulation rend la page d'avant, la frappe et la dissociation
        // d'avant restant derrière lui.
        var regroupee = Etat(vm);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, Etat(vm));
        vm.RedoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(regroupee, Etat(vm));

        // Dissous de nouveau, puis un de ses blocs supprimé : il n'y a plus de groupe à refaire.
        page = vm.CurrentPage!;
        vm.SelectOnly(page.Frames.Single(bloc => bloc is GroupFrame));
        vm.UngroupSelectedCommand.Execute(null);
        Assert.True(vm.RegrouperCommand.CanExecute(null));
        vm.SelectOnly(page.Frames.Single(bloc => bloc.Name == "A"));
        vm.DeleteSelectedCommand.Execute(null);
        Rendu.Battre();
        Assert.False(vm.RegrouperCommand.CanExecute(null), "Regrouper s'offre alors qu'un des blocs a quitté la page");
    }

    // ------------------------------------------------------------------ Tab

    /// <summary>
    /// La recette : sur la planche, Tab passe au bloc suivant dans l'ordre de la page, Maj+Tab au
    /// précédent, en sautant le bloc masqué, et repartent de l'autre bout ; sans rien empiler, et
    /// la planche garde le focus.
    /// </summary>
    [AvaloniaFact]
    public void Tab_et_Maj_Tab_passent_au_bloc_voisin_en_sautant_le_masque()
    {
        var masque = Carre("B", 200, 60);
        masque.Visible = false;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 60, 60), masque, Carre("C", 340, 60), Carre("D", 60, 300)));
        var planche = Trouver<Control>(fenetre, "PageView");

        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 90, 90));
        Assert.Equal("A", vm.Selected?.Name);
        Assert.True(planche.IsFocused, "le clic sur la page n'a pas donné le focus à la planche");

        var suite = new List<string?>();
        for (var i = 0; i < 3; i++)
        {
            Rendu.Touche(fenetre, PhysicalKey.Tab);
            suite.Add(vm.Selected?.Name);
        }

        Assert.Equal(new[] { "C", "D", "A" }, suite);

        suite.Clear();
        for (var i = 0; i < 3; i++)
        {
            Rendu.Touche(fenetre, PhysicalKey.Tab, RawInputModifiers.Shift);
            suite.Add(vm.Selected?.Name);
        }

        Assert.Equal(new[] { "D", "C", "A" }, suite);
        Assert.Single(vm.Selection);
        Assert.True(planche.IsFocused, "Tab a emmené le focus hors de la planche");
        Assert.False(vm.CanUndo, "Tab a fait un pas");

        // Sans sélection, Tab prend le premier bloc visible, Maj+Tab le dernier.
        vm.SelectOnly(null);
        Rendu.Touche(fenetre, PhysicalKey.Tab);
        Assert.Equal("A", vm.Selected?.Name);
        vm.SelectOnly(null);
        Rendu.Touche(fenetre, PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.Equal("D", vm.Selected?.Name);

        // Pris seul dans la liste, le bloc masqué sert de départ : Tab va au suivant visible.
        vm.SelectOnly(masque);
        planche.Focus();
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.Tab);
        Assert.Equal("C", vm.Selected?.Name);
    }

    /// <summary>
    /// La recette : dans le dock, Tab continue d'aller au champ suivant, et la sélection des
    /// blocs ne bouge pas.
    /// </summary>
    [AvaloniaFact]
    public void Tab_dans_le_dock_va_au_champ_suivant()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre("A", 60, 60), Carre("C", 340, 60)));
        vm.SelectOnly(vm.CurrentPage!.Frames[0]);
        Rendu.Battre();

        var nom = Trouver<TextBox>(fenetre, "ChampNom");
        nom.Focus();
        Rendu.Battre();
        Assert.True(nom.IsFocused);

        Rendu.Touche(fenetre, PhysicalKey.Tab);

        var focus = fenetre.FocusManager!.GetFocusedElement() as Visual;
        Assert.NotNull(focus);
        Assert.False(ReferenceEquals(focus, nom), "Tab est resté sur le champ Nom");
        var planche = Trouver<Control>(fenetre, "PageView");
        Assert.False(ReferenceEquals(focus, planche) || planche.IsVisualAncestorOf(focus!), "Tab a mené le focus sur la planche");
        Assert.Equal("A", vm.Selected?.Name);

        // Et revient, Maj+Tab, au champ Nom.
        Rendu.Touche(fenetre, PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.True(nom.IsFocused, "Maj+Tab n'est pas revenu au champ Nom");
        Assert.Equal("A", vm.Selected?.Name);
    }
}
