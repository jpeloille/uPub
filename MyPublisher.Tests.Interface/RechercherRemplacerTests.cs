using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Rechercher et remplacer — le poste N20 (fiche 23, e23 ; décision 6 de la phase N).
///
/// Publisher le fait par <i>Edition ▸ Rechercher</i> et <i>Remplacer</i>, sur <c>Ctrl+F</c>,
/// <c>F3</c>, <c>Maj+F4</c> et <c>Ctrl+H</c>. MyPublisher pose une barre sur la planche, en haut à
/// droite : le texte cherché, le texte de remplacement, <i>Respecter la casse</i>, <i>Mot
/// entier</i>, Suivant, Remplacer, Remplacer tout. Le texte trouvé dans un bloc est sélectionné en
/// saisie (N12, N15), la page courante suivant ; dans un groupe, le groupe est sélectionné et la
/// barre d'état le dit ; dans une case, le tableau. <i>Remplacer tout</i> fait un seul pas, et le
/// texte remplacé garde ses fragments, par le noyau.
///
/// Les gestes sont ceux du banc (<see cref="Rendu"/>, poste N1).
/// </summary>
public class RechercherRemplacerTests
{
    // ------------------------------------------------------------------ le banc du poste

    private static TextFrame Texte(string nom, double x, double y, string texte) => new()
    {
        Name = nom, X = x, Y = y, Width = 300, Height = 80, Text = texte,
        FontFamily = "DejaVu Sans", FontSize = 14, Foreground = "#FF000000",
    };

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static BarreDeRecherche Barre(Window fenetre) => Trouver<BarreDeRecherche>(fenetre, "BarreDeRecherche");

    private static TextBox Champ(Window fenetre, string nom) => Trouver<TextBox>(Barre(fenetre), nom);

    private static Document Exemple() =>
        DocumentSerializer.Load(Path.Combine(Rendu.Racine(), "Samples", "exemple-bulletin.mypub"));

    /// <summary><c>Ctrl+F</c>, puis le texte cherché tapé dans la barre, qui a le focus.</summary>
    private static void Chercher(Window fenetre, string texte)
    {
        Rendu.Touche(fenetre, PhysicalKey.F, RawInputModifiers.Control);
        var champ = Champ(fenetre, "TexteCherche");
        Assert.True(champ.IsFocused, "Ctrl+F n'a pas donné le focus au texte cherché");
        Rendu.Taper(fenetre, texte);
        Assert.Equal(texte, champ.Text);
    }

    /// <summary>Les marques de la saisie, que la planche pose sur la composition (N21).</summary>
    private static MarquesDuTexte Marques(Window fenetre) => Trouver<MarquesDuTexte>(fenetre, "MarquesDeLaSaisie");

    /// <summary>
    /// Le texte sélectionné en saisie, tel que le modèle de vue le connaît (N15) et que les marques
    /// le dessinent sur la composition (N21). Le focus est où il doit être : dans la barre de
    /// recherche quand on y cherchait (décision du propriétaire, 31 octobre 2026), le champ du bloc
    /// n'ayant plus de sélection à lui ; dans le champ du bloc, <paramref name="focusAuTexte"/>, et sa
    /// sélection est la même.
    /// </summary>
    private static (TextFrame Bloc, int Debut, int Fin) EnSaisie(Window fenetre, MainWindowViewModel vm, bool focusAuTexte = false)
    {
        var selection = vm.SelectionDuTexte ?? throw new InvalidOperationException("rien n'est sélectionné en saisie");
        var bloc = Assert.IsType<TextFrame>(selection.Bloc);
        Assert.True(bloc.IsEditing, $"« {bloc.Name} » n'est pas en saisie");
        Assert.Same(bloc, vm.SingleSelected);
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc) ?? throw new InvalidOperationException("aucune zone de saisie ne se voit");
        if (focusAuTexte)
        {
            Assert.True(zone.IsFocused, "la zone de saisie n'a pas le focus");
            Assert.Equal((selection.Debut, selection.Fin), (Math.Min(zone.SelectionStart, zone.SelectionEnd),
                Math.Max(zone.SelectionStart, zone.SelectionEnd)));
        }
        else
        {
            Assert.False(zone.IsFocused, "le texte trouvé a pris le focus à la barre");
            var focus = fenetre.FocusManager!.GetFocusedElement() as Visual;
            Assert.True(focus is not null && Barre(fenetre).IsVisualAncestorOf(focus), "le focus n'est plus dans la barre");
        }

        if (!selection.EstVide) Assert.NotEmpty(Marques(fenetre).Plages);
        return (bloc, selection.Debut, selection.Fin);
    }

    /// <summary>
    /// Les occurrences d'une recherche faite sur le texte des blocs, sans l'application : les
    /// blocs de texte visibles et sans champ, les cases des tableaux, ce que portent les groupes.
    /// </summary>
    private static int Compte(Document document, string cherche)
    {
        var motif = new Regex(Regex.Escape(cherche), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var compte = 0;

        void Compter(Frame bloc)
        {
            if (!bloc.Visible) return;
            switch (bloc)
            {
                case TextFrame { HasField: false } texte:
                    compte += motif.Matches(texte.Text ?? "").Count;
                    break;
                case TableFrame tableau:
                    compte += tableau.Cells.Sum(c => motif.Matches(c.Text ?? "").Count);
                    break;
                case GroupFrame groupe:
                    foreach (var enfant in groupe.Children) Compter(enfant);
                    break;
            }
        }

        foreach (var page in document.Pages)
        {
            foreach (var bloc in page.Frames) Compter(bloc);
        }

        return compte;
    }

    // ------------------------------------------------------------------ la recette

    /// <summary>
    /// Sur l'exemple, « texte » est sur les deux pages : chaque <c>F3</c> le trouve, sélectionné en
    /// saisie à la bonne plage, la page courante suivant, puis repart du début du document. Le
    /// premier se cherche par <c>Entrée</c> dans la barre ; les autres par <c>F3</c>, le focus dans
    /// le texte trouvé.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void F3_trouve_chaque_occurrence_selectionnee_en_saisie_la_page_suivant()
    {
        var document = Exemple();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var page1 = document.Pages[0];
        var page2 = document.Pages[1];
        var legende = page1.Frames.OfType<TextFrame>().Single(b => b.Name == "Légende");
        var colonneA = page2.Frames.OfType<TextFrame>().Single(b => b.Name == "Colonne A");
        var avant = DocumentSerializer.ToJson(document);

        Chercher(fenetre, "texte");
        Rendu.Touche(fenetre, PhysicalKey.Enter);

        var premier = legende.Text.IndexOf("texte", StringComparison.OrdinalIgnoreCase);
        Assert.Same(page1, vm.CurrentPage);
        Assert.Equal((legende, premier, premier + 5), EnSaisie(fenetre, vm));
        Assert.Contains("page 1", vm.Status);

        Rendu.Touche(fenetre, PhysicalKey.F3);
        var second = colonneA.Text.IndexOf("texte", StringComparison.OrdinalIgnoreCase);
        Assert.Same(page2, vm.CurrentPage);
        Assert.Equal((colonneA, second, second + 5), EnSaisie(fenetre, vm));
        Assert.Contains("page 2", vm.Status);

        // Maj+F4, la seconde touche : le bout du document, et la recherche repart du début.
        Rendu.Touche(fenetre, PhysicalKey.F4, RawInputModifiers.Shift);
        Assert.Same(page1, vm.CurrentPage);
        Assert.Equal((legende, premier, premier + 5), EnSaisie(fenetre, vm));
        Assert.Contains("repris au début", vm.Status);

        // Chercher n'a rien changé, et n'a empilé aucun pas.
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
    }

    /// <summary>
    /// « bulletin » sur l'exemple, cherché depuis la page 2 : il n'est qu'une fois dans le texte, au
    /// sous-titre de la page 1 — le titre, « Le Petit Composeur », ne le porte pas. <c>F3</c> l'y
    /// trouve, sa casse ignorée, la page courante suivant ; et encore à chaque <c>F3</c>.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void Bulletin_est_trouve_depuis_la_page_2_au_sous_titre_de_la_page_1()
    {
        var document = Exemple();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.CurrentPage = document.Pages[1];
        Rendu.Battre();
        var sousTitre = document.Pages[0].Frames.OfType<TextFrame>().Single(b => b.Name == "Sous-titre");
        Assert.Equal(1, Compte(document, "bulletin"));

        Chercher(fenetre, "bulletin");
        for (var fois = 0; fois < 3; fois++)
        {
            Rendu.Touche(fenetre, PhysicalKey.F3);
            Assert.Same(document.Pages[0], vm.CurrentPage);
            Assert.Equal((sousTitre, 0, 8), EnSaisie(fenetre, vm));
            Assert.Equal("Bulletin", vm.SelectionDuTexte!.Texte[..8]);
        }
    }

    /// <summary>
    /// <i>Remplacer tout</i> fait UN pas pour tout le document, quel que soit le nombre
    /// d'occurrences et de blocs ; une annulation rend le document. Les fragments sont gardés : un
    /// mot en gras remplacé reste gras, et ce qui le suit garde son style. La garde du juge :
    /// <i>Remplacer tout</i> qui empilerait un pas par occurrence.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void Remplacer_tout_fait_un_seul_pas_et_garde_les_fragments()
    {
        var titre = Texte("Titre", 60, 40, "Le bulletin du mois");
        var gras = new StyleSpan { Start = 3, Length = 8, Bold = true };
        var mois = new StyleSpan { Start = 15, Length = 4, Italic = true };
        titre.Spans.Add(gras);
        titre.Spans.Add(mois);
        var corps = Texte("Corps", 60, 160, "Un bulletin, deux bulletins.");
        var document = Rendu.Feuille(titre, corps);
        var page2 = new Page();
        page2.Frames.Add(Texte("Suite", 60, 40, "Fin du BULLETIN."));
        document.Pages.Add(page2);
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var avant = DocumentSerializer.ToJson(vm.Document);

        Chercher(fenetre, "bulletin");
        Rendu.Touche(fenetre, PhysicalKey.H, RawInputModifiers.Control);
        var remplacement = Champ(fenetre, "TexteDeRemplacement");
        Assert.True(remplacement.IsFocused, "Ctrl+H n'a pas donné le focus au texte de remplacement");
        Rendu.Taper(fenetre, "journal");

        Rendu.Cliquer(Trouver<Button>(Barre(fenetre), "BoutonRemplacerTout"));

        Assert.Equal("Le journal du mois", titre.Text);
        Assert.Equal("Un journal, deux journals.", corps.Text);
        Assert.Equal("Fin du journal.", ((TextFrame)page2.Frames[0]).Text);
        Assert.Equal((3, 7, true), (gras.Start, gras.Length, gras.Bold));
        Assert.Equal((14, 4, true), (mois.Start, mois.Length, mois.Italic));
        Assert.Contains("4 remplacements", vm.Status);

        Rendu.UnSeulPas(vm, avant, "Remplacer tout");
    }

    /// <summary>
    /// Sur ATA 21, le compte des occurrences que <c>F3</c> parcourt avant de repartir du début égale
    /// celui d'une recherche sur le texte des blocs ; puis <i>Remplacer tout</i>, à la casse, en
    /// remplace autant, en un pas, et le gras du titre « Air Production » reste au texte qui le
    /// remplace.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void Sur_ATA_21_le_compte_egale_celui_d_une_recherche_sur_le_texte_des_blocs()
    {
        var document = Rendu.Ata21();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var attendu = Compte(document, "air");
        Assert.True(attendu > 5, $"ATA 21 n'a que {attendu} « air »");

        Chercher(fenetre, "air");
        var trouves = 0;
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        while (!vm.Status.Contains("repris au début"))
        {
            Assert.StartsWith("« air » trouvé", vm.Status);
            trouves++;
            Assert.True(trouves <= attendu, $"F3 trouve plus de « air » ({trouves}) que le texte n'en a ({attendu})");
            Rendu.Touche(fenetre, PhysicalKey.F3);
        }

        Assert.Equal(attendu, trouves);

        // Le titre en gras : son premier caractère porte le gras, que le remplacement garde.
        var titre = document.Pages.SelectMany(p => p.Frames).OfType<TextFrame>()
            .First(b => b.Text.Contains("Air Production") &&
                        b.Spans.Any(s => s.Bold == true && s.Start <= b.Text.IndexOf("Air Production", StringComparison.Ordinal) &&
                                         b.Text.IndexOf("Air Production", StringComparison.Ordinal) < s.Start + s.Length));
        var ou = titre.Text.IndexOf("Air Production", StringComparison.Ordinal);
        var avant = DocumentSerializer.ToJson(vm.Document);
        var majuscules = Textes(document).Sum(texte => Regex.Matches(texte, "Air").Count);

        Trouver<CheckBox>(Barre(fenetre), "RespecterLaCasse").IsChecked = true;
        vm.TexteCherche = "Air";
        vm.TexteDeRemplacement = "Gaz";
        Rendu.Cliquer(Trouver<Button>(Barre(fenetre), "BoutonRemplacerTout"));

        Assert.Contains($"{majuscules} remplacements", vm.Status);
        Assert.Equal("Gaz Production", titre.Text.Substring(ou, 14));
        Assert.Contains(titre.Spans, s => s.Bold == true && s.Start <= ou && ou + 3 <= s.Start + s.Length);
        Rendu.UnSeulPas(vm, avant, "Remplacer tout sur ATA 21");
    }

    /// <summary>Le texte des blocs où l'on cherche, pour compter à la casse.</summary>
    private static IEnumerable<string> Textes(Document document)
    {
        IEnumerable<string> De(Frame bloc) => !bloc.Visible ? [] : bloc switch
        {
            TextFrame { HasField: false } texte => [texte.Text ?? ""],
            TableFrame tableau => tableau.Cells.Select(c => c.Text ?? ""),
            GroupFrame groupe => groupe.Children.SelectMany(De),
            _ => [],
        };

        return document.Pages.SelectMany(p => p.Frames).SelectMany(De);
    }

    /// <summary>
    /// <i>Respecter la casse</i> et <i>Mot entier</i> : « Bulletin » à la casse ne trouve que le
    /// sien ; « bulletin » en mot entier saute « bulletins ». Le parcours va de haut en bas, puis de
    /// gauche à droite : le bloc de droite, posé plus haut, passe avant celui de gauche.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void La_casse_et_le_mot_entier_et_l_ordre_de_la_page()
    {
        var bas = Texte("Bas", 60, 300, "Le Bulletin et les bulletins.");
        var haut = Texte("Haut", 380, 100, "Un bulletin.");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bas, haut));

        Chercher(fenetre, "bulletin");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal((haut, 3, 11), EnSaisie(fenetre, vm));
        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.Equal((bas, 3, 11), EnSaisie(fenetre, vm));
        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.Equal((bas, 19, 27), EnSaisie(fenetre, vm));

        // Mot entier : « bulletins » est sauté, et l'on revient en haut.
        Trouver<CheckBox>(Barre(fenetre), "MotEntier").IsChecked = true;
        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.Equal((haut, 3, 11), EnSaisie(fenetre, vm));
        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.Equal((bas, 3, 11), EnSaisie(fenetre, vm));
        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.Equal((haut, 3, 11), EnSaisie(fenetre, vm));

        // À la casse, « Bulletin » ne trouve que le sien, et « BULLETIN » rien.
        vm.MotEntier = false;
        vm.RespecterLaCasse = true;
        vm.TexteCherche = "Bulletin";
        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.Equal((bas, 3, 11), EnSaisie(fenetre, vm));
        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.Equal((bas, 3, 11), EnSaisie(fenetre, vm));
        vm.TexteCherche = "BULLETIN";
        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.Equal("« BULLETIN » est introuvable.", vm.Status);
    }

    /// <summary>
    /// <i>Remplacer</i> remplace la sélection quand elle est une occurrence, en un pas, puis
    /// sélectionne la suivante ; une sélection qui n'en est pas une n'est pas remplacée, et la
    /// recherche avance seulement.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void Remplacer_fait_un_pas_puis_trouve_la_suivante()
    {
        var bloc = Texte("Texte", 60, 60, "un bulletin, deux bulletin");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        Chercher(fenetre, "bulletin");
        var remplacer = Trouver<Button>(Barre(fenetre), "BoutonRemplacer");
        vm.TexteDeRemplacement = "journal";

        // Rien n'est encore sélectionné : Remplacer trouve d'abord, sans rien remplacer.
        Rendu.Cliquer(remplacer);
        Assert.Equal((bloc, 3, 11), EnSaisie(fenetre, vm));
        Assert.Equal("un bulletin, deux bulletin", bloc.Text);
        Assert.False(vm.CanUndo);

        var avant = DocumentSerializer.ToJson(vm.Document);
        Rendu.Cliquer(remplacer);
        Assert.Equal("un journal, deux bulletin", bloc.Text);
        Assert.Equal((bloc, 17, 25), EnSaisie(fenetre, vm));

        // Un pas, et un seul : l'annuler rend le texte.
        Rendu.UnSeulPas(vm, avant, "Remplacer");
    }

    /// <summary>
    /// Un texte trouvé dans un groupe sélectionne le groupe et ouvre la saisie du bloc qui le porte,
    /// la plage sélectionnée — depuis N26, qui entre dans les groupes (règle 14 : le groupe était
    /// sélectionné, la barre d'état le disait) ; dans une case, il sélectionne le tableau et ouvre la
    /// saisie de la case, la plage sélectionnée, et la barre d'état nomme la case — depuis N27, qui
    /// tape dans les cases (règle 14 : le tableau était sélectionné, sans saisie). <i>Remplacer</i> y
    /// remplace l'occurrence sélectionnée, en un pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void Dans_un_groupe_ou_une_case_le_bloc_est_selectionne_et_la_barre_d_etat_le_dit()
    {
        var groupe = new GroupFrame { Name = "Encart", X = 60, Y = 60, Width = 300, Height = 80 };
        groupe.Children.Add(Texte("Légende", 0, 0, "Le bulletin"));
        var tableau = new TableFrame { Name = "Bande", X = 60, Y = 300, Width = 300, Height = 26 };
        tableau.Columns.Add(new TableColumn(150));
        tableau.Columns.Add(new TableColumn(150));
        tableau.Rows.Add(new TableRow(26));
        tableau.Cells.Add(new TableCell { Row = 0, Column = 0, Text = "Numéro" });
        tableau.Cells.Add(new TableCell { Row = 0, Column = 1, Text = "bulletin 3" });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe, tableau));

        Chercher(fenetre, "bulletin");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Same(groupe, vm.SingleSelected);
        var legende = groupe.Children[0];
        Assert.Same(legende, vm.TexteEnSaisie);
        Assert.Equal((legende, 3, 11), (vm.SelectionDuTexte?.Bloc, vm.SelectionDuTexte?.Debut, vm.SelectionDuTexte?.Fin));
        Assert.DoesNotContain("dissociez", vm.Status);

        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.Same(tableau, vm.SingleSelected);
        Assert.Same(tableau.Cells[1], vm.CaseEnSaisie);
        Assert.Equal((tableau, tableau.Cells[1], 0, 8),
            (vm.SelectionDuTexte?.Bloc, vm.SelectionDuTexte?.Case, vm.SelectionDuTexte?.Debut, vm.SelectionDuTexte?.Fin));
        Assert.Contains($"dans la case {tableau.Cells[1].Label} du tableau « Bande »", vm.Status);

        vm.TexteDeRemplacement = "journal";
        var avant = DocumentSerializer.ToJson(vm.Document);
        Rendu.Cliquer(Trouver<Button>(Barre(fenetre), "BoutonRemplacer"));
        Assert.Equal("journal 3", tableau.Cells[1].Text);
        Assert.Same(groupe, vm.SingleSelected);
        Rendu.UnSeulPas(vm, avant, "Remplacer dans une case");
    }

    /// <summary>
    /// La barre ne prend pas les lettres seules des outils : « T » s'écrit dans le texte cherché ;
    /// <c>Échap</c> la ferme et rend la planche, et « T » arme alors l'outil texte. Sans rien à
    /// chercher, <c>F3</c> ouvre la barre, comme <c>Ctrl+F</c>.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void Echap_ferme_la_barre_et_rend_les_lettres_aux_outils()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte("Texte", 60, 60, "Bonjour")));
        Assert.False(Barre(fenetre).IsVisible);

        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.True(Barre(fenetre).IsVisible);
        Assert.True(Champ(fenetre, "TexteCherche").IsFocused);
        Assert.Equal("Tapez le texte à rechercher.", vm.Status);

        Rendu.ToucheAzerty(fenetre, PhysicalKey.T);
        Rendu.Taper(fenetre, "T");
        Assert.Equal("T", vm.TexteCherche);
        Assert.Equal(EditorTool.Select, vm.ActiveTool);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(vm.RechercheVisible);
        Assert.False(Barre(fenetre).IsVisible);
        Assert.False(Champ(fenetre, "TexteCherche").IsFocused);

        Rendu.ToucheAzerty(fenetre, PhysicalKey.T);
        Assert.Equal(EditorTool.Text, vm.ActiveTool);
    }

    /// <summary>
    /// La barre est posée sur la planche, en haut à droite, sans la déplacer (règle 3) et hors de
    /// <c>PageRoot</c> (règle 4) ; le menu Edition l'ouvre, ses entrées affichant leurs touches.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void La_barre_se_pose_sur_la_planche_sans_la_deplacer()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Texte("Texte", 60, 60, "Bonjour")));
        var page = Trouver<Panel>(fenetre, "PageRoot");
        var planche = Trouver<PageCanvas>(fenetre, "PageView");
        var avant = page.TranslatePoint(new Point(0, 0), fenetre);

        vm.OuvrirLaRechercheCommand.Execute(null);
        Rendu.Battre();
        var barre = Barre(fenetre);
        Assert.True(barre.IsVisible);
        Assert.Equal(avant, page.TranslatePoint(new Point(0, 0), fenetre));
        Assert.False(page.IsVisualAncestorOf(barre));

        // En haut à droite de la planche, sous la règle du haut.
        var coin = barre.TranslatePoint(new Point(barre.Bounds.Width, 0), planche)!.Value;
        Assert.InRange(planche.Bounds.Width - coin.X, 0, 40);
        Assert.InRange(coin.Y, Ruler.Breadth, Ruler.Breadth + 20);

        // Le menu Edition : Rechercher… et Remplacer…, sur leurs commandes.
        var entrees = fenetre.GetVisualDescendants().OfType<Menu>().First().Items.OfType<MenuItem>()
            .Single(m => Equals(m.Header, "_Edition")).Items.OfType<MenuItem>().ToList();
        Assert.Contains(entrees, m => Equals(m.Header, "Rec_hercher…") && ReferenceEquals(m.Command, vm.OuvrirLaRechercheCommand));
        Assert.Contains(entrees, m => Equals(m.Header, "Re_mplacer…") && ReferenceEquals(m.Command, vm.OuvrirLeRemplacementCommand));
    }

    /// <summary>
    /// <c>F3</c> joue aussi le focus dans le texte en saisie, que la zone ne prend pas ; et la
    /// recherche part du curseur : un clic plus loin que la première occurrence trouve la seconde.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void La_recherche_part_du_curseur_en_saisie()
    {
        var bloc = Texte("Texte", 60, 60, "ab ab ab");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.TexteCherche = "ab";
        vm.RechercheVisible = true;
        Rendu.Battre();

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        zone.CaretIndex = 4;
        Rendu.Battre();
        Assert.Equal(4, vm.SelectionDuTexte!.Fin);

        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.Equal((bloc, 6, 8), EnSaisie(fenetre, vm, focusAuTexte: true));
        Rendu.Touche(fenetre, PhysicalKey.F3);
        Assert.Equal((bloc, 0, 2), EnSaisie(fenetre, vm, focusAuTexte: true));
        Assert.Contains("repris au début", vm.Status);
    }

    // ------------------------------------------- repris le 31 octobre 2026 : le focus reste à la barre

    /// <summary>
    /// Décision du propriétaire, 31 octobre 2026 : le texte trouvé ne prend plus le focus, qui reste
    /// dans la barre, comme dans le volet de Publisher. Une seconde <c>Entrée</c> cherche le
    /// suivant, d'un bloc à l'autre, sans rien écrire ; la sélection est dessinée sur la composition
    /// pendant que la barre a le focus ; aucune transaction ne reste ouverte — une commande qui
    /// suit fait son pas. La garde du juge : le texte trouvé qui reprend le focus.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void Une_seconde_Entree_cherche_le_suivant_la_selection_dessinee_le_focus_dans_la_barre()
    {
        var premier = Texte("Premier", 60, 60, "Un bulletin.");
        var second = Texte("Second", 60, 200, "Le bulletin du mois.");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(premier, second));
        var avant = DocumentSerializer.ToJson(vm.Document);

        Chercher(fenetre, "bulletin");
        var champ = Champ(fenetre, "TexteCherche");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal((premier, 3, 11), EnSaisie(fenetre, vm));
        Assert.True(champ.IsFocused);
        var marques = Marques(fenetre).Plages;
        Assert.Single(marques);
        Assert.InRange(marques[0].Width, 20, 120);

        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal((second, 3, 11), EnSaisie(fenetre, vm));
        Assert.True(champ.IsFocused);
        Assert.Equal("bulletin", champ.Text);

        // Rien n'est écrit, rien n'est empilé.
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);

        // Aucune transaction ouverte : la commande qui suit fait son pas, aussitôt.
        vm.DuplicateSelectedCommand.Execute(null);
        Rendu.Battre();
        Assert.True(vm.CanUndo, "une transaction restée ouverte a avalé le pas de la commande");
    }

    /// <summary>
    /// <c>Échap</c> ferme la barre sur une occurrence sélectionnée : le focus va au champ du bloc,
    /// la sélection gardée, et une lettre remplace l'occurrence, en un pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N20")]
    public void Echap_rend_le_focus_au_texte_trouve_et_une_lettre_le_remplace_en_un_pas()
    {
        var bloc = Texte("Texte", 60, 60, "un bulletin ici");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var avant = DocumentSerializer.ToJson(vm.Document);

        Chercher(fenetre, "bulletin");
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal((bloc, 3, 11), EnSaisie(fenetre, vm));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(vm.RechercheVisible);
        Assert.Equal((bloc, 3, 11), EnSaisie(fenetre, vm, focusAuTexte: true));
        Assert.NotEmpty(Marques(fenetre).Plages);
        Assert.False(vm.CanUndo);

        Rendu.Taper(fenetre, "x");
        Assert.Equal("un x ici", bloc.Text);
        Assert.Equal(EditorTool.Select, vm.ActiveTool);

        // Échap, dans le texte, referme la saisie et sa transaction : la frappe a fait un pas, un seul.
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(bloc.IsEditing);
        Rendu.UnSeulPas(vm, avant, "la lettre tapée après Échap");
    }
}
