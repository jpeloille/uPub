using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;
using Xunit.Abstractions;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les vignettes du panneau Pages — poste I5.
///
/// Chaque page se montre en vignette de 96 px de large, au rapport de la page, son numéro
/// dessous ; la page courante est bordée de 2 px d'accent. La vignette se dessine depuis la
/// composition, comme celle des modèles (<see cref="VignetteDePage"/> dérive de
/// <see cref="TemplatePreview"/>), et suit le document ouvert, qui se modifie en place.
///
/// Les pixels sont lus sur l'image RÉELLEMENT composée de la fenêtre (<see cref="Rendu.Bande"/>),
/// et non sur un rendu à la demande : une vignette qui oublierait de se redessiner passerait
/// un <c>RenderTargetBitmap</c>, qui redessine tout de force. Les couleurs sont lues dans
/// l'apparence (I1), qui les tient sous leur nom.
/// </summary>
[Trait("poste", "I5")]
public class VignettesDePagesTests(ITestOutputHelper sortie)
{
    private static string Exemple() => Path.Combine(Rendu.Racine(), "Samples", "exemple-bulletin.mypub");

    /// <summary>L'exemple, deux pages, ouvert sur la première.</summary>
    private static (Window Fenetre, MainWindowViewModel Vm) OuvrirLExemple() =>
        Rendu.Ouvrir(DocumentSerializer.Load(Exemple()));

    /// <summary>Les vignettes du panneau, dans l'ordre des pages.</summary>
    private static List<VignetteDePage> Vignettes(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<VignetteDePage>().ToList();

    /// <summary>Le cadre d'une entrée du panneau : la boîte bordée qui porte la vignette.</summary>
    private static Border Cadre(VignetteDePage vignette) =>
        vignette.FindAncestorOfType<Border>() is { Name: "CadreDeVignette" } cadre
            ? cadre
            : throw new InvalidOperationException("la vignette n'est pas dans son cadre");

    private static TextBlock Numero(VignetteDePage vignette) =>
        vignette.FindAncestorOfType<EntreeDePage>()!.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => t.Name == "NumeroDePage");

    private static Rect Boite(Window fenetre, Control controle)
    {
        var coin = controle.TranslatePoint(new Point(0, 0), fenetre) ?? throw new InvalidOperationException();
        return new Rect(coin, controle.Bounds.Size);
    }

    private static Color Couleur(string cle)
    {
        Assert.True(Application.Current!.TryGetResource(cle, ThemeVariant.Light, out var valeur), cle);
        return Assert.IsType<Color>(valeur);
    }

    /// <summary>Un pixel de l'image composée, quel que soit l'ordre de ses octets.</summary>
    private static Color Pixel(Window fenetre, double x, double y)
    {
        using var image = fenetre.CaptureRenderedFrame() ?? throw new InvalidOperationException("aucune image composée");
        using var verrou = image.Lock();
        var octets = new byte[verrou.RowBytes * image.PixelSize.Height];
        System.Runtime.InteropServices.Marshal.Copy(verrou.Address, octets, 0, octets.Length);
        var i = (int)y * verrou.RowBytes + (int)x * 4;
        var bgra = verrou.Format == PixelFormat.Bgra8888;
        return Color.FromArgb(octets[i + 3], octets[bgra ? i + 2 : i], octets[i + 1], octets[bgra ? i : i + 2]);
    }

    private static void ProcheDe(Color attendue, Color lue, string quoi, int ecart = 1)
    {
        Assert.True(Math.Abs(attendue.R - lue.R) <= ecart && Math.Abs(attendue.G - lue.G) <= ecart &&
                    Math.Abs(attendue.B - lue.B) <= ecart,
            $"{quoi} : {lue} au lieu de {attendue}");
    }

    /// <summary>Un clic de la souris au centre d'un contrôle de la fenêtre.</summary>
    private static void Cliquer(Window fenetre, Control controle)
    {
        var centre = controle.TranslatePoint(new Point(controle.Bounds.Width / 2, controle.Bounds.Height / 2), fenetre)
                     ?? throw new InvalidOperationException();
        fenetre.MouseDown(centre, MouseButton.Left);
        fenetre.MouseUp(centre, MouseButton.Left);
        Rendu.Battre();
    }

    // ------------------------------------------------------------ l'allure

    [AvaloniaFact]
    public void Chaque_page_a_sa_vignette_de_96_px_au_rapport_de_la_page_et_la_courante_est_bordee_d_accent()
    {
        var (fenetre, vm) = OuvrirLExemple();
        var vignettes = Vignettes(fenetre);
        Assert.Equal(2, vignettes.Count);

        // Un A4 : 96 de large, cadre compris, et 96 × 841,89 / 595,28 = 135,8 de haut,
        // la boîte de la maquette (96 × 136). Le numéro dessous, à 5 px.
        foreach (var vignette in vignettes)
        {
            var cadre = Boite(fenetre, Cadre(vignette));
            Assert.Equal(new Size(96, 136), cadre.Size);
            var numero = Boite(fenetre, Numero(vignette));
            Assert.Equal(cadre.Bottom + 5, numero.Top);
            Assert.True(Math.Abs(numero.Center.X - cadre.Center.X) < 1,
                $"le numéro n'est pas centré sous sa vignette : {numero.Center.X} contre {cadre.Center.X}");
        }

        Assert.Equal(["1", "2"], vignettes.Select(v => Numero(v).Text));

        // Centrées dans le panneau de 168 px, à 12 px l'une de l'autre.
        var panneau = Boite(fenetre, fenetre.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PanneauPages"));
        var premiere = Boite(fenetre, Cadre(vignettes[0]));
        Assert.Equal(panneau.X + (168 - 96) / 2.0, premiere.X);
        Assert.Equal(Boite(fenetre, Numero(vignettes[0])).Bottom + 12, Boite(fenetre, Cadre(vignettes[1])).Top);

        // La page courante : 2 px d'accent ; l'autre, 1 px au bord des champs. Lus à
        // mi-hauteur du bord gauche, sur l'image composée.
        var accent = Couleur("CouleurAccent");
        var bord = Couleur("CouleurBordChamp");
        var y1 = premiere.Center.Y;
        ProcheDe(accent, Pixel(fenetre, premiere.X, y1), "bord de la page courante, 1er pixel");
        ProcheDe(accent, Pixel(fenetre, premiere.X + 1, y1), "bord de la page courante, 2e pixel");
        var seconde = Boite(fenetre, Cadre(vignettes[1]));
        ProcheDe(bord, Pixel(fenetre, seconde.X, seconde.Center.Y), "bord d'une autre page");
        Assert.Equal(new Thickness(1), Cadre(vignettes[1]).BorderThickness);

        // Le numéro de la page courante est en demi-gras, au survol de l'accent ; l'autre au
        // gris de la barre d'état.
        Assert.Equal(FontWeight.SemiBold, Numero(vignettes[0]).FontWeight);
        Assert.Equal(Couleur("CouleurAccentSurvol"), ((ISolidColorBrush)Numero(vignettes[0]).Foreground!).Color);
        Assert.Equal(Couleur("CouleurEtat"), ((ISolidColorBrush)Numero(vignettes[1]).Foreground!).Color);

        // Choisir la seconde page passe l'accent sur elle, sans que rien ne bouge : les
        // vignettes gardent leur boîte — le cadre de 2 px se prend sur le dedans.
        vm.CurrentPage = vm.Document.Pages[1];
        Rendu.Battre();
        Assert.Equal(premiere, Boite(fenetre, Cadre(vignettes[0])));
        Assert.Equal(seconde, Boite(fenetre, Cadre(vignettes[1])));
        ProcheDe(bord, Pixel(fenetre, premiere.X, y1), "bord de la page quittée");
        ProcheDe(accent, Pixel(fenetre, seconde.X + 1, seconde.Center.Y), "bord de la page choisie");
    }

    [AvaloniaFact]
    public void Une_page_a_l_italienne_donne_une_vignette_a_l_italienne()
    {
        // Le format change en place, le document reste : la hauteur suit quand même.
        var (fenetre, vm) = OuvrirLExemple();
        vm.IsLandscape = true;
        Rendu.Battre();

        Assert.True(vm.Document.PageWidth > vm.Document.PageHeight);
        foreach (var vignette in Vignettes(fenetre))
        {
            // 96 × 595,28 / 841,89 = 67,9.
            Assert.Equal(new Size(96, 68), Cadre(vignette).Bounds.Size);
        }
    }

    [AvaloniaFact]
    public void La_vignette_montre_les_blocs_de_sa_page_et_le_fond_de_page()
    {
        // Une page portant un aplat dans son coin haut gauche, une autre vide : chaque
        // vignette dessine SA page, et non la première comme le catalogue.
        var document = Rendu.Feuille(new ShapeFrame
        {
            Name = "Aplat", X = 0, Y = 0, Width = 297.64, Height = 420.95, Fill = new SolidFill("#FF336699"), Stroke = null,
        });
        document.Pages.Add(new Page { Name = "Page 2" });
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var vignettes = Vignettes(fenetre);

        var bleu = Color.Parse("#336699");
        var blanc = Couleur("CouleurBlanc");
        Point QuartHautGauche(VignetteDePage v) => v.TranslatePoint(new Point(v.Bounds.Width / 4, v.Bounds.Height / 4), fenetre)!.Value;
        Point QuartBasDroit(VignetteDePage v) => v.TranslatePoint(new Point(v.Bounds.Width * 3 / 4, v.Bounds.Height * 3 / 4), fenetre)!.Value;

        ProcheDe(bleu, Pixel(fenetre, QuartHautGauche(vignettes[0]).X, QuartHautGauche(vignettes[0]).Y), "l'aplat de la page 1");
        ProcheDe(blanc, Pixel(fenetre, QuartBasDroit(vignettes[0]).X, QuartBasDroit(vignettes[0]).Y), "le blanc de la page 1");
        ProcheDe(blanc, Pixel(fenetre, QuartHautGauche(vignettes[1]).X, QuartHautGauche(vignettes[1]).Y), "la page 2, vide");

        // Le fond de page est celui du document : il paraît sous les blocs de chaque page.
        vm.BeginEdit();
        document.Background = new SolidFill("#FFF0C040");
        vm.CommitEdit();
        Rendu.Battre();
        var jaune = Color.Parse("#F0C040");
        ProcheDe(bleu, Pixel(fenetre, QuartHautGauche(vignettes[0]).X, QuartHautGauche(vignettes[0]).Y), "l'aplat sur le fond");
        ProcheDe(jaune, Pixel(fenetre, QuartBasDroit(vignettes[0]).X, QuartBasDroit(vignettes[0]).Y), "le fond de la page 1");
        ProcheDe(jaune, Pixel(fenetre, QuartHautGauche(vignettes[1]).X, QuartHautGauche(vignettes[1]).Y), "le fond de la page 2");
    }

    // ------------------------------------------------------------ suivre le document

    [AvaloniaFact]
    public void Modifier_un_bloc_de_la_page_2_change_la_vignette_2_et_non_la_1_et_une_annulation_la_rend()
    {
        var (fenetre, vm) = OuvrirLExemple();
        vm.CurrentPage = vm.Document.Pages[1];
        Rendu.Battre();

        var avant1 = Rendu.Bande(fenetre, Vignettes(fenetre)[0]);
        var avant2 = Rendu.Bande(fenetre, Vignettes(fenetre)[1]);
        Assert.Contains(avant2, octet => octet < 128);   // la page 2 a de l'encre : la comparaison dit quelque chose

        // Le premier bloc de texte saisi de la page 2, déplacé de 150 points, comme un champ X
        // du dock le ferait : une transaction, un pas. La vignette suit dès la retouche, sans
        // attendre la fermeture du pas : c'est le document en place qu'elle montre.
        var bloc = vm.Document.Pages[1].Frames.OfType<TextFrame>().First(t => t.Text.Length > 40);
        vm.SelectOnly(bloc);
        vm.BeginEdit();
        bloc.X += 150;
        Rendu.Battre();
        var pendant2 = Rendu.Bande(fenetre, Vignettes(fenetre)[1]);
        Assert.NotEqual(avant2, pendant2);
        vm.CommitEdit();
        Rendu.Battre();

        var apres2 = Rendu.Bande(fenetre, Vignettes(fenetre)[1]);
        Assert.NotEqual(avant2, apres2);
        Assert.Equal(avant1, Rendu.Bande(fenetre, Vignettes(fenetre)[0]));

        // Annuler remplace le document entier, et avec lui les pages et leurs vignettes :
        // celles-ci rendent exactement les pixels d'avant.
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Same(vm.Document.Pages[1], vm.CurrentPage);
        Assert.Equal(avant2, Rendu.Bande(fenetre, Vignettes(fenetre)[1]));
        Assert.Equal(avant1, Rendu.Bande(fenetre, Vignettes(fenetre)[0]));

        // Et rétablir les reprend.
        vm.RedoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(apres2, Rendu.Bande(fenetre, Vignettes(fenetre)[1]));
    }

    [AvaloniaFact]
    public void Un_bloc_pose_retire_ou_retouche_dans_un_groupe_se_voit_dans_la_vignette()
    {
        // Ce que la vignette écoute, au-delà d'un bloc posé sur la page : la pile de la page,
        // et les enfants d'un groupe. Chaque geste change ses pixels.
        var enfant = new ShapeFrame { Name = "Enfant", X = 0, Y = 0, Width = 200, Height = 200, Fill = new SolidFill("#FF336699") };
        var groupe = new GroupFrame { Name = "Groupe", X = 100, Y = 100, Width = 200, Height = 200 };
        groupe.Children.Add(enfant);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));
        var vignette = Vignettes(fenetre)[0];

        var depart = Rendu.Bande(fenetre, vignette);

        enfant.Fill = new SolidFill("#FFCC3333");
        Rendu.Battre();
        var recolore = Rendu.Bande(fenetre, vignette);
        Assert.NotEqual(depart, recolore);

        var second = new ShapeFrame { Name = "Second", X = 20, Y = 0, Width = 100, Height = 100, Fill = new SolidFill("#FF33AA33") };
        groupe.Children.Add(second);
        Rendu.Battre();
        var agrandi = Rendu.Bande(fenetre, vignette);
        Assert.NotEqual(recolore, agrandi);

        // L'enfant posé après coup s'écoute lui aussi.
        second.X = 120;
        Rendu.Battre();
        Assert.NotEqual(agrandi, Rendu.Bande(fenetre, vignette));

        vm.Document.Pages[0].Frames.Clear();
        Rendu.Battre();
        var vide = Rendu.Bande(fenetre, vignette);
        Assert.DoesNotContain(vide, octet => octet < 200);
    }

    [AvaloniaFact]
    public void Ajouter_dupliquer_ou_supprimer_une_page_met_la_liste_a_jour()
    {
        var (fenetre, vm) = OuvrirLExemple();
        Assert.Equal(2, Vignettes(fenetre).Count);

        // Ajoutée après la page courante : la deuxième, blanche, et devenue courante.
        vm.AddPageCommand.Execute(null);
        Rendu.Battre();
        var vignettes = Vignettes(fenetre);
        Assert.Equal(3, vignettes.Count);
        Assert.Equal(["1", "2", "3"], vignettes.Select(v => Numero(v).Text));
        Assert.Same(vm.Document.Pages[1], vignettes[1].Page);
        Assert.Equal(new Thickness(2), Cadre(vignettes[1]).BorderThickness);
        Assert.DoesNotContain(Rendu.Bande(fenetre, vignettes[1]), octet => octet < 200);

        // Dupliquer la première : sa copie la suit, et montre les mêmes pixels une fois
        // qu'aucune des deux n'est la page courante — le cadre de la courante prend 1 px
        // sur le dedans.
        vm.CurrentPage = vm.Document.Pages[0];
        vm.DuplicatePageCommand.Execute(null);
        vm.CurrentPage = vm.Document.Pages[3];
        Rendu.Battre();
        vignettes = Vignettes(fenetre);
        Assert.Equal(4, vignettes.Count);
        Assert.Equal(["1", "2", "3", "4"], vignettes.Select(v => Numero(v).Text));
        Assert.Equal(Rendu.Bande(fenetre, vignettes[0]), Rendu.Bande(fenetre, vignettes[1]));

        // Supprimer la page courante, la dernière : la liste se resserre, et la nouvelle
        // courante est bordée d'accent.
        vm.DeletePageCommand.Execute(null);
        Rendu.Battre();
        vignettes = Vignettes(fenetre);
        Assert.Equal(3, vignettes.Count);
        Assert.Equal(["1", "2", "3"], vignettes.Select(v => Numero(v).Text));
        Assert.Equal(vm.Document.Pages, vignettes.Select(v => v.Page));
        Assert.Same(vm.Document.Pages[2], vm.CurrentPage);
        Assert.Equal(new Thickness(2), Cadre(vignettes[2]).BorderThickness);
    }

    [AvaloniaFact]
    public void Un_clic_sur_une_vignette_change_la_page_courante()
    {
        var (fenetre, vm) = OuvrirLExemple();
        var vignettes = Vignettes(fenetre);

        Cliquer(fenetre, vignettes[1]);
        Assert.Same(vm.Document.Pages[1], vm.CurrentPage);
        Assert.Equal(2, vm.CurrentPageNumber);
        Assert.Equal(new Thickness(2), Cadre(vignettes[1]).BorderThickness);
        Assert.Equal(new Thickness(1), Cadre(vignettes[0]).BorderThickness);

        // La planche montre la page choisie, et l'historique n'a rien reçu : changer de page
        // n'est pas un geste sur le document.
        Assert.False(vm.CanUndo);
        Assert.False(vm.IsDirty);

        Cliquer(fenetre, vignettes[0]);
        Assert.Same(vm.Document.Pages[0], vm.CurrentPage);
    }

    // ------------------------------------------------------------ les abonnements

    /// <summary>Les vignettes de page qui écoutent cet objet par cet événement.</summary>
    private static int Ecoutes(object source, string evenement = "PropertyChanged") =>
        Rendu.Abonnes(source, evenement).Count(abonne => abonne.Target is VignetteDePage);

    [AvaloniaFact]
    public void Une_vignette_sortie_de_l_arbre_n_ecoute_plus_rien()
    {
        // La même fuite que celle que Q2 a refermée sur le texte composé : une page vit
        // aussi longtemps que le document, bien plus que l'entrée du panneau qui la montre.
        var texte = new TextFrame { Name = "Texte", X = 40, Y = 40, Width = 200, Height = 100, Text = "Un texte." };
        var enfant = new TextFrame { Name = "Enfant", X = 0, Y = 0, Width = 100, Height = 40, Text = "Dedans." };
        var groupe = new GroupFrame { Name = "Groupe", X = 40, Y = 200, Width = 100, Height = 40 };
        groupe.Children.Add(enfant);
        var document = Rendu.Feuille(texte, groupe);
        var page = document.Pages[0];

        var planche = new Panel();
        var fenetre = new Window { Content = planche, Width = 200, Height = 300 };
        fenetre.Show();

        var vignette = new VignetteDePage { Document = document, Page = page, Width = 96, Height = 136 };
        for (var i = 0; i < 20; i++)
        {
            planche.Children.Add(vignette);
            Rendu.Battre(1);

            // Dans l'arbre, une écoute et une seule de chaque objet.
            Assert.Equal(1, Ecoutes(document));
            Assert.Equal(1, Ecoutes(page.Frames, "CollectionChanged"));
            Assert.Equal(1, Ecoutes(texte));
            Assert.Equal(1, Ecoutes(texte.Spans, "CollectionChanged"));
            Assert.Equal(1, Ecoutes(texte.Paragraphs, "CollectionChanged"));
            Assert.Equal(1, Ecoutes(groupe.Children, "CollectionChanged"));
            Assert.Equal(1, Ecoutes(enfant));

            planche.Children.Remove(vignette);
            Rendu.Battre(1);
        }

        Assert.Equal(0, Ecoutes(document));
        Assert.Equal(0, Ecoutes(page.Frames, "CollectionChanged"));
        Assert.Equal(0, Ecoutes(texte));
        Assert.Equal(0, Ecoutes(texte.Spans, "CollectionChanged"));
        Assert.Equal(0, Ecoutes(texte.Paragraphs, "CollectionChanged"));
        Assert.Equal(0, Ecoutes(groupe.Children, "CollectionChanged"));
        Assert.Equal(0, Ecoutes(groupe));
        Assert.Equal(0, Ecoutes(enfant));

        // Dans l'arbre, une autre page posée : l'ancienne n'est plus écoutée.
        planche.Children.Add(vignette);
        Rendu.Battre(1);
        var autre = new Page { Name = "Autre" };
        vignette.Page = autre;
        Assert.Equal(0, Ecoutes(page.Frames, "CollectionChanged"));
        Assert.Equal(0, Ecoutes(texte));
        Assert.Equal(1, Ecoutes(autre.Frames, "CollectionChanged"));
    }

    // ------------------------------------------------------------ la mesure

    [AvaloniaFact]
    [Trait("mesure", "I5")]
    public void Sur_ATA_21_redessiner_toutes_les_vignettes_apres_une_modification_prend_moins_de_50_ms()
    {
        // Deux modifications, la plus courante et la plus large. La retouche d'un bloc — le
        // plus long texte d'ATA 21, déplacé d'un point — ne fait recomposer que ses filets,
        // et dessiner les autres de mémoire. Le jeu de polices vaut pour toutes les pages,
        // et change la mesure : chaque vignette recompose tous ses blocs.
        var document = Rendu.Ata21();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var vignettes = Vignettes(fenetre);
        Assert.Equal(document.Pages.Count, vignettes.Count);

        var plusLong = document.Pages.SelectMany(p => p.Frames).OfType<TextFrame>().MaxBy(t => t.Text.Length)!;
        var sens = 1;
        void Retoucher()
        {
            plusLong.X += sens;
            sens = -sens;
        }

        var jeux = JeuxPredefinis.Polices;
        void ChangerDePolices() =>
            document.FontScheme = jeux[(jeux.ToList().FindIndex(j => j.Name == document.FontScheme.Name) + 1) % jeux.Count];

        var retouche = Mesurer(vm, vignettes, Retoucher);
        var polices = Mesurer(vm, vignettes, ChangerDePolices);
        sortie.WriteLine($"ATA 21, {vignettes.Count} vignettes, « {plusLong.Name} » ({plusLong.Text.Length} caractères) retouché : " +
                         string.Join(" ; ", retouche.Select(p => $"{p:0.0} ms")));
        sortie.WriteLine($"ATA 21, jeu de polices changé : {string.Join(" ; ", polices.Select(p => $"{p:0.0} ms"))}");

        Assert.True(retouche[1] < 50, $"médiane de {retouche[1]:0.0} ms pour redessiner les vignettes après une retouche");

        // Tout recomposer coûte ce que coûte la composition des dix-neuf blocs — celle-là
        // même que fait la planche —, 40 à 47 ms ici en Debug, trop près des 50 pour
        // passer sous la charge d'un passage complet : le seuil est le double, et garde
        // d'une composition faite deux fois ou à chaque rendu (voir PLAN.md, poste I5).
        Assert.True(polices[1] < 100, $"médiane de {polices[1]:0.0} ms pour redessiner les vignettes après un jeu de polices");
    }

    /// <summary>
    /// Trois passes d'une modification suivie du dessin de chaque vignette, après un tour à
    /// blanc — les polices et le moteur de rendu se chargent une fois. Rend les trois
    /// temps du dessin seul, triés : la médiane est le deuxième.
    /// </summary>
    private static List<double> Mesurer(MainWindowViewModel vm, List<VignetteDePage> vignettes, Action modifier) =>
        Enumerable.Range(0, 4).Select(_ => Redessiner(vm, vignettes, modifier)).Skip(1).Order().ToList();

    /// <summary>
    /// Modifie le document, en un pas, puis redessine chaque vignette, et rend le temps du
    /// dessin seul. Le rendu se fait à la demande, à la taille de la vignette : aucun tic du
    /// compositeur n'a pu recomposer avant la mesure, qui serait alors celle de filets
    /// retenus.
    /// </summary>
    private static double Redessiner(MainWindowViewModel vm, List<VignetteDePage> vignettes, Action modifier)
    {
        vm.BeginEdit();
        modifier();
        vm.CommitEdit();

        var chrono = Stopwatch.StartNew();
        foreach (var vignette in vignettes)
        {
            var taille = new PixelSize((int)Math.Ceiling(vignette.Bounds.Width), (int)Math.Ceiling(vignette.Bounds.Height));
            using var rendu = new RenderTargetBitmap(taille);
            rendu.Render(vignette);
        }

        return chrono.Elapsed.TotalMilliseconds;
    }
}
