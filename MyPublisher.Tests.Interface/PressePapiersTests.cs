using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Couper, copier, coller des blocs — le poste N8 (fiche 23, a17, a18, c21, d7).
///
/// Le livre fait entrer le contenu par copier-coller, et le menu contextuel de Publisher
/// commence par Couper, Copier, Coller. Hors du <c>TextBox</c>, MyPublisher n'avait aucune
/// commande de presse-papiers. Les touches sont prises par la fenêtre après la garde de la
/// saisie, jamais en liaison : en saisie, ou dans un champ, c'est le champ qui les reçoit.
///
/// Le presse-papiers est celui du banc sans affichage, un magasin en mémoire commun à toute la
/// suite (<c>HeadlessClipboardImplStub</c>) où un format propre fait l'aller-retour (N1) : aucun
/// faux n'est nécessaire. Chaque test pose d'abord ce qu'il y attend. Ses gestes sont ceux du
/// banc (<see cref="Rendu"/>, poste N1).
/// </summary>
public class PressePapiersTests
{
    // ------------------------------------------------------------------ l'outillage

    private static readonly DataFormat<string> FormatDesBlocs =
        DataFormat.CreateStringApplicationFormat(MainWindowViewModel.FormatDesBlocs);

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static IClipboard PressePapiers(Window fenetre) =>
        fenetre.Clipboard ?? throw new InvalidOperationException("aucun presse-papiers");

    private static string? BlocsCopies(Window fenetre) => Rendu.Attendre(PressePapiers(fenetre).TryGetValueAsync(FormatDesBlocs));

    private static TextFrame Texte(string nom, double x, double y, string texte) => new()
    {
        Name = nom, X = x, Y = y, Width = 220, Height = 60, Text = texte,
        FontFamily = "DejaVu Sans", FontSize = 12, Foreground = "#FF000000",
    };

    private static ShapeFrame Forme(double x, double y) => new()
    {
        Name = "Forme", X = x, Y = y, Width = 80, Height = 50,
        Fill = new SolidFill(ReferenceDuJeu.Couleur(EmplacementDeCouleur.Accent2)),
        Stroke = ReferenceDuJeu.Couleur(EmplacementDeCouleur.Accent1),
    };

    /// <summary>Un document de deux pages, la première portant <paramref name="blocs"/>.</summary>
    private static Document DeuxPages(params Frame[] blocs)
    {
        var document = Rendu.Feuille(blocs);
        document.Pages.Add(new Page { Name = "Page 2" });
        return document;
    }

    private static void Ctrl(Window fenetre, PhysicalKey touche)
    {
        Rendu.Touche(fenetre, touche, RawInputModifiers.Control);
        Rendu.Battre();
    }

    /// <summary>L'entrée du menu Édition, menu ouvert, comme une main la vise.</summary>
    private static void ParLeMenu(Window fenetre, string nom)
    {
        var edition = Trouver<Menu>(fenetre, "Menus").Items.OfType<MenuItem>().Single(m => Equals(m.Header, "_Edition"));
        Rendu.Cliquer(edition);
        Rendu.Cliquer(edition.Items.OfType<MenuItem>().Single(m => m.Name == nom));
        Rendu.Battre();
    }

    // ------------------------------------------------------------------ les blocs

    [AvaloniaFact]
    [Trait("poste", "N8")]
    public void Copier_puis_coller_sur_une_autre_page_donne_des_instances_distinctes_au_meme_endroit_en_un_pas()
    {
        var titre = Texte("Titre", 60, 40, "Le titre");
        var forme = Forme(300, 200);
        var corps = Texte("Corps", 60, 120, "Le corps");
        var (fenetre, vm) = Rendu.Ouvrir(DeuxPages(titre, forme, corps));
        try
        {
            Rendu.Attendre(PressePapiers(fenetre).SetTextAsync("rien encore"));

            // Désignés dans le désordre : la copie suit l'ordre de la page.
            vm.SetSelection([corps, titre, forme]);
            Rendu.Battre();
            var avant = Etat(vm);
            Ctrl(fenetre, PhysicalKey.C);

            Assert.Equal(avant, Etat(vm));
            Assert.False(vm.CanUndo, "copier a empilé un pas");
            var copie = DocumentSerializer.FromJson(BlocsCopies(fenetre) ?? throw new InvalidOperationException("aucun bloc au presse-papiers"));
            Assert.Equal(new[] { "Titre", "Forme", "Corps" }, copie.Pages.Single().Frames.Select(f => f.Name));
            // Le texte brut, pour les autres logiciels : les blocs de texte, dans l'ordre de la page.
            Assert.Equal("Le titre\n\nLe corps", Rendu.TexteCopie(fenetre));

            vm.CurrentPage = vm.Document.Pages[1];
            Rendu.Battre();
            var avantLeCollage = Etat(vm);
            Ctrl(fenetre, PhysicalKey.V);

            var colles = vm.CurrentPage!.Frames.ToList();
            Assert.Equal(new[] { "Titre", "Forme", "Corps" }, colles.Select(f => f.Name));
            foreach (var (colle, original) in colles.Zip(new Frame[] { titre, forme, corps }))
            {
                Assert.NotSame(original, colle);
                Assert.Equal((original.X, original.Y, original.Width, original.Height), (colle.X, colle.Y, colle.Width, colle.Height));
            }

            Assert.Equal(colles, vm.SelectionInPageOrder());
            Assert.Equal("Le titre", Assert.IsType<TextFrame>(colles[0]).Text);

            // Les instances divergent : retoucher le collé ne touche pas l'original.
            ((TextFrame)colles[0]).Text = "Retouché";
            Assert.Equal("Le titre", titre.Text);
            ((TextFrame)colles[0]).Text = "Le titre";

            Rendu.UnSeulPas(vm, avantLeCollage, "le collage sur une autre page");
            Assert.Empty(vm.CurrentPage!.Frames);
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    [Trait("poste", "N8")]
    public void Sur_la_meme_page_le_collage_tombe_12_pt_plus_loin_comme_Ctrl_D()
    {
        var forme = Forme(100, 100);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(forme));
        try
        {
            vm.SelectOnly(forme);
            Rendu.Battre();
            Ctrl(fenetre, PhysicalKey.C);

            var avant = Etat(vm);
            Ctrl(fenetre, PhysicalKey.V);
            var premier = Assert.Single(vm.Selection);
            Assert.NotSame(forme, premier);
            Assert.Equal((112.0, 112.0), (premier.X, premier.Y));
            Assert.Equal(2, vm.CurrentPage!.Frames.Count);

            // Un second collage ne retombe ni sur l'original ni sur le premier.
            Ctrl(fenetre, PhysicalKey.V);
            var second = Assert.Single(vm.Selection);
            Assert.Equal((124.0, 124.0), (second.X, second.Y));

            // Ctrl+D décale de même.
            vm.SelectOnly(vm.CurrentPage.Frames[0]);
            vm.DuplicateSelectedCommand.Execute(null);
            Rendu.Battre();
            var duplique = Assert.Single(vm.Selection);
            Assert.Equal((112.0, 112.0), (duplique.X, duplique.Y));

            // Chaque collage a fait son pas.
            vm.UndoCommand.Execute(null);
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Rendu.UnSeulPas(vm, avant, "le premier collage");
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    [Trait("poste", "N8")]
    public void Couper_fait_un_seul_pas_et_le_collage_rend_les_blocs_a_leur_place()
    {
        var titre = Texte("Titre", 60, 40, "Le titre");
        var forme = Forme(300, 200);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(titre, forme));
        try
        {
            Rendu.Attendre(PressePapiers(fenetre).SetTextAsync("rien encore"));
            vm.SetSelection([forme, titre]);
            Rendu.Battre();
            var avant = Etat(vm);

            Ctrl(fenetre, PhysicalKey.X);
            Assert.Empty(vm.CurrentPage!.Frames);
            Assert.Empty(vm.Selection);
            Assert.NotNull(BlocsCopies(fenetre));
            Assert.Equal("Le titre", Rendu.TexteCopie(fenetre));
            var coupe = Etat(vm);
            Rendu.UnSeulPas(vm, avant, "couper");
            vm.RedoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal(coupe, Etat(vm));

            // L'original n'est plus là : le collage le rend à sa place, et l'état d'avant.
            Ctrl(fenetre, PhysicalKey.V);
            Assert.Equal(avant, Etat(vm));
            Assert.Equal(new[] { "Titre", "Forme" }, vm.SelectionInPageOrder().Select(f => f.Name));
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// Le garde du juge : le collage pose des clones, jamais les blocs du contenu qu'on lui
    /// passe. Celui qui colle garde ce contenu et peut le coller de nouveau ; deux collages du
    /// même contenu donnent deux jeux d'instances, qui divergent ensuite.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N8")]
    public void Le_collage_pose_des_clones_et_non_les_blocs_du_contenu_copie()
    {
        var titre = Texte("Titre", 60, 40, "Le titre");
        var (fenetre, vm) = Rendu.Ouvrir(DeuxPages(titre));
        try
        {
            vm.SelectOnly(titre);
            var contenu = vm.CopierLaSelection() ?? throw new InvalidOperationException("rien de copié");
            var source = DocumentSerializer.FromJson(contenu.Blocs);
            var copie = Assert.IsType<TextFrame>(source.Pages.Single().Frames.Single());

            vm.CurrentPage = vm.Document.Pages[1];
            Assert.True(vm.Coller(source));
            Assert.True(vm.Coller(source));
            Rendu.Battre();

            var colles = vm.CurrentPage!.Frames.ToList();
            Assert.Equal(2, colles.Count);
            Assert.DoesNotContain(copie, colles);
            Assert.NotSame(colles[0], colles[1]);
            Assert.Equal((72.0, 52.0), (colles[1].X, colles[1].Y));

            ((TextFrame)colles[0]).Text = "Retouché";
            Assert.Equal("Le titre", copie.Text);
            Assert.Equal("Le titre", ((TextFrame)colles[1]).Text);
            Assert.Equal((60.0, 40.0), (copie.X, copie.Y));
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ le texte

    [AvaloniaFact]
    [Trait("poste", "N8")]
    public void En_saisie_Ctrl_C_met_le_texte_au_presse_papiers_sans_format_de_bloc()
    {
        var bloc = Texte("Bloc", 60, 60, "Bonjour le monde");
        bloc.Width = 300;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            // Des blocs au presse-papiers d'abord : la copie du texte doit les remplacer.
            vm.SelectOnly(bloc);
            Rendu.Battre();
            Ctrl(fenetre, PhysicalKey.C);
            Assert.NotNull(BlocsCopies(fenetre));

            var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
            var avant = Etat(vm);
            Rendu.SelectionnerDuTexte(zone, 8, 10);
            Assert.Equal("le", zone.SelectedText);

            Ctrl(fenetre, PhysicalKey.C);
            Assert.Equal("le", Rendu.TexteCopie(fenetre));
            Assert.Null(BlocsCopies(fenetre));
            Assert.Equal(avant, Etat(vm));
            Assert.True(bloc.IsEditing, "la copie a refermé la saisie");
            Assert.Single(vm.CurrentPage!.Frames);

            // Coller, en saisie, colle du texte dans la zone, pas un bloc.
            Rendu.SelectionnerDuTexte(zone, 0, 7);
            Ctrl(fenetre, PhysicalKey.V);
            Assert.Equal("le le monde", zone.Text);
            Assert.Single(vm.CurrentPage!.Frames);

            // Couper, de même : le texte part, le bloc reste.
            Rendu.SelectionnerDuTexte(zone, 3, 5);
            Ctrl(fenetre, PhysicalKey.X);
            Assert.Equal("le", Rendu.TexteCopie(fenetre));
            Assert.Equal("le  monde", zone.Text);
            Assert.Single(vm.CurrentPage!.Frames);
            Assert.True(bloc.IsEditing);
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    [Trait("poste", "N8")]
    public void Dans_un_champ_du_dock_Ctrl_C_et_Ctrl_V_restent_au_champ()
    {
        var bloc = Texte("Bloc", 60, 60, "Corps");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            vm.SelectOnly(bloc);
            Rendu.Battre();
            var nom = fenetre.GetVisualDescendants().OfType<TextBox>()
                .First(t => t.IsEffectivelyVisible && t.Text == "Bloc" && !t.GetVisualAncestors().Any(a => a.Name == "PageRoot"));
            nom.Focus();
            nom.SelectAll();
            Rendu.Battre();

            Ctrl(fenetre, PhysicalKey.C);
            Assert.Equal("Bloc", Rendu.TexteCopie(fenetre));
            Assert.Null(BlocsCopies(fenetre));

            Rendu.Attendre(PressePapiers(fenetre).SetTextAsync("Collé"));
            nom.SelectAll();
            Ctrl(fenetre, PhysicalKey.V);
            Assert.Equal("Collé", nom.Text);
            Assert.Single(vm.CurrentPage!.Frames);
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    [Trait("poste", "N8")]
    public void Un_texte_brut_colle_hors_saisie_devient_un_bloc_de_texte()
    {
        var forme = Forme(300, 300);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(forme));
        try
        {
            Rendu.Attendre(PressePapiers(fenetre).SetTextAsync("Venu d'ailleurs\r\nsur deux lignes"));
            vm.SelectOnly(forme);
            Rendu.Battre();
            var avant = Etat(vm);

            Ctrl(fenetre, PhysicalKey.V);
            Assert.Equal(2, vm.CurrentPage!.Frames.Count);
            var bloc = Assert.IsType<TextFrame>(Assert.Single(vm.Selection));
            Assert.Same(bloc, vm.CurrentPage.Frames[^1]);
            Assert.Equal("Venu d'ailleurs\nsur deux lignes", bloc.Text);
            // Comme le bloc du menu Insertion : dans la zone de composition, aux couleurs du jeu.
            Assert.Equal(ReferenceDuJeu.Couleur(EmplacementDeCouleur.Main), bloc.Foreground);
            Assert.InRange(bloc.X, vm.Document.MarginLeft, vm.Document.PageWidth);
            Rendu.UnSeulPas(vm, avant, "le collage d'un texte");

            // Un texte blanc ne pose rien.
            Rendu.Attendre(PressePapiers(fenetre).SetTextAsync("  \n "));
            Ctrl(fenetre, PhysicalKey.V);
            Assert.Single(vm.CurrentPage!.Frames);
            Assert.False(vm.CanUndo);
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ les pièges

    [AvaloniaFact]
    [Trait("poste", "N8")]
    public void Un_collage_d_une_version_plus_recente_du_format_est_refuse()
    {
        var forme = Forme(100, 100);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(forme));
        try
        {
            vm.SelectOnly(forme);
            var contenu = vm.CopierLaSelection()!;
            var plusRecent = contenu.Blocs.Replace(
                $"\"FormatVersion\": {Document.CurrentFormatVersion},", $"\"FormatVersion\": {Document.CurrentFormatVersion + 1},");
            Assert.NotEqual(contenu.Blocs, plusRecent);

            var element = new DataTransferItem();
            element.Set(FormatDesBlocs, plusRecent);
            element.Set(DataFormat.Text, "Un texte à côté");
            var transfert = new DataTransfer();
            transfert.Add(element);
            Rendu.Attendre(PressePapiers(fenetre).SetDataAsync(transfert));

            var avant = Etat(vm);
            Ctrl(fenetre, PhysicalKey.V);
            Assert.Equal(avant, Etat(vm));
            Assert.False(vm.CanUndo);
            Assert.StartsWith("Collage impossible", vm.Status);
            Assert.Contains($"format {Document.CurrentFormatVersion + 1}", vm.Status);
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    [Trait("poste", "N8")]
    public void Les_references_au_jeu_se_resolvent_dans_le_document_d_arrivee()
    {
        var forme = Forme(100, 100);
        var (fenetreA, vmA) = Rendu.Ouvrir(Rendu.Feuille(forme));
        var arrivee = Rendu.Feuille();
        arrivee.ColorScheme = JeuxPredefinis.CouleursNommees("Bordeaux")!;
        var (fenetreB, vmB) = Rendu.Ouvrir(arrivee);
        try
        {
            vmA.SelectOnly(forme);
            Rendu.Battre();
            Ctrl(fenetreA, PhysicalKey.C);

            Ctrl(fenetreB, PhysicalKey.V);
            var colle = Assert.IsType<ShapeFrame>(Assert.Single(vmB.CurrentPage!.Frames));
            Assert.Equal("scheme:accent1", colle.Stroke);
            Assert.Equal("#FF2E5C8A", vmA.Document.Charte.Couleur(forme.Stroke));
            Assert.Equal("#FF7B2D26", vmB.Document.Charte.Couleur(colle.Stroke));
            Assert.Equal("#FFE3B5A4", vmB.Document.Charte.Couleur(Assert.IsType<SolidFill>(colle.Fill).Color));
        }
        finally
        {
            fenetreA.Close();
            fenetreB.Close();
        }
    }

    [AvaloniaFact]
    [Trait("poste", "N8")]
    public void Les_chemins_d_images_se_rebasent_sur_le_document_d_arrivee()
    {
        var racine = Directory.CreateTempSubdirectory("mypublisher-n8-");
        try
        {
            var images = Directory.CreateDirectory(Path.Combine(racine.FullName, "images"));
            var png = Path.Combine(images.FullName, "trame.png");
            File.Copy(Path.Combine(Rendu.Racine(), "Samples", "recette-fonds-trame.png"), png);

            // Le document de départ, à la racine : l'image y est écrite « images/trame.png ».
            var depart = Rendu.Feuille(new ImageFrame { Name = "Image", X = 50, Y = 50, Width = 80, Height = 80, Path = png });
            var cheminA = Path.Combine(racine.FullName, "depart.mypub");
            DocumentSerializer.Save(depart, cheminA);
            Assert.Contains("\"Path\": \"images/trame.png\"", File.ReadAllText(cheminA));

            var (fenetreA, vmA) = Rendu.Ouvrir(Rendu.Feuille());
            var (fenetreB, vmB) = Rendu.Ouvrir(Rendu.Feuille());
            try
            {
                vmA.LoadFrom(cheminA);
                vmA.SelectOnly(vmA.CurrentPage!.Frames.Single());
                Rendu.Battre();
                Ctrl(fenetreA, PhysicalKey.C);
                // Au presse-papiers, le chemin du modèle : absolu, et non relatif au départ.
                Assert.Contains(png, BlocsCopies(fenetreA));

                Ctrl(fenetreB, PhysicalKey.V);
                var colle = Assert.IsType<ImageFrame>(Assert.Single(vmB.CurrentPage!.Frames));
                Assert.Equal(png, colle.Path);

                // Enregistré dans le dossier de l'image, le document d'arrivée l'écrit par rapport à lui.
                var cheminB = Path.Combine(images.FullName, "arrivee.mypub");
                vmB.SaveTo(cheminB);
                Assert.Contains("\"Path\": \"trame.png\"", File.ReadAllText(cheminB));
                Assert.Equal(png, Assert.IsType<ImageFrame>(DocumentSerializer.Load(cheminB).Pages[0].Frames.Single()).Path);
            }
            finally
            {
                fenetreA.Close();
                fenetreB.Close();
            }
        }
        finally
        {
            racine.Delete(recursive: true);
        }
    }

    // ------------------------------------------------------------------ le menu Édition

    [AvaloniaFact]
    [Trait("poste", "N8")]
    public void Le_menu_Edition_coupe_copie_et_colle_et_rend_le_geste_a_la_saisie()
    {
        var bloc = Texte("Bloc", 60, 60, "Bonjour le monde");
        bloc.Width = 300;
        var (fenetre, vm) = Rendu.Ouvrir(DeuxPages(bloc));
        try
        {
            var edition = Trouver<Menu>(fenetre, "Menus").Items.OfType<MenuItem>().Single(m => Equals(m.Header, "_Edition"));
            var entrees = edition.Items.OfType<MenuItem>().ToList();
            foreach (var (nom, geste) in new[] { ("MenuCouper", "Ctrl+X"), ("MenuCopier", "Ctrl+C"), ("MenuColler", "Ctrl+V") })
            {
                var entree = Assert.Single(entrees, e => e.Name == nom);
                Assert.Equal(KeyGesture.Parse(geste), entree.InputGesture);
            }

            // Hors saisie, les blocs : copier, puis coller sur l'autre page.
            vm.SelectOnly(bloc);
            Rendu.Battre();
            ParLeMenu(fenetre, "MenuCopier");
            Assert.NotNull(BlocsCopies(fenetre));
            vm.CurrentPage = vm.Document.Pages[1];
            Rendu.Battre();
            ParLeMenu(fenetre, "MenuColler");
            var colle = Assert.IsType<TextFrame>(Assert.Single(vm.CurrentPage!.Frames));
            Assert.NotSame(bloc, colle);
            Assert.Equal((60.0, 60.0), (colle.X, colle.Y));
            ParLeMenu(fenetre, "MenuCouper");
            Assert.Empty(vm.CurrentPage!.Frames);

            // En saisie, le menu rend le geste à la zone.
            vm.CurrentPage = vm.Document.Pages[0];
            Rendu.Battre();
            var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
            Rendu.SelectionnerDuTexte(zone, 8, 10);
            ParLeMenu(fenetre, "MenuCopier");
            Assert.Equal("le", Rendu.TexteCopie(fenetre));
            Assert.Null(BlocsCopies(fenetre));
            Assert.True(bloc.IsEditing, "le menu a refermé la saisie");
            // La zone a repris le focus et sa sélection : on continue d'y taper.
            Assert.True(zone.IsFocused, "la zone n'a pas repris le focus après le menu");
            Assert.Equal("le", zone.SelectedText);

            // Couper et Coller, de même, au texte de la zone et non aux blocs.
            ParLeMenu(fenetre, "MenuCouper");
            Assert.Equal("Bonjour  monde", zone.Text);
            Rendu.SelectionnerDuTexte(zone, 0, 7);
            ParLeMenu(fenetre, "MenuColler");
            Assert.Equal("le  monde", zone.Text);
            Assert.Single(vm.CurrentPage!.Frames);
            Assert.True(bloc.IsEditing);
        }
        finally
        {
            fenetre.Close();
        }
    }
}
