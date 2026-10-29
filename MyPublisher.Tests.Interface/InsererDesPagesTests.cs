using System;
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

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Insérer des pages : nombre, position, contenu — poste A8 (fiche 08 ; fiche 23, f18), livré par
/// N18. <c>AddPage</c> insérait une page vierge après la page courante, et <c>DuplicatePage</c>
/// copiait la page courante ; Publisher demande dans un même dialogue un nombre de pages, une
/// position et un contenu — des pages vierges, une zone de texte par page, ou la copie des objets
/// d'une page désignée, dont le numéro reste grisé tant que son option n'est pas choisie (p. 22).
///
/// Le dialogue s'ouvre à <c>Ctrl+Maj+N</c>, comme chez Publisher, et se remplit aux gestes du banc
/// (poste N1) : la recette d'origine de A8, puis la zone de texte, puis le champ grisé.
/// </summary>
[Trait("poste", "A8")]
public class InsererDesPagesTests
{
    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    /// <summary>Le document de la recette : trois pages, la première portant trois blocs, la page 2 courante.</summary>
    private static (Window Fenetre, MainWindowViewModel Vm) TroisPages()
    {
        var document = Rendu.Feuille(
            new TextFrame { Name = "Titre", X = 60, Y = 60, Width = 300, Height = 60, Text = "Bulletin" },
            new ShapeFrame { Name = "Bandeau", X = 60, Y = 140, Width = 400, Height = 40 },
            new TextFrame { Name = "Texte", X = 60, Y = 200, Width = 300, Height = 200, Text = "Corps" });
        document.Pages[0].Name = "Page 1";
        document.Pages.Add(new Page { Name = "Page 2", Frames = { new ShapeFrame { Name = "Seul", X = 10, Y = 10, Width = 20, Height = 20 } } });
        document.Pages.Add(new Page { Name = "Page 3" });
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.CurrentPage = document.Pages[1];
        Rendu.Battre();
        return (fenetre, vm);
    }

    /// <summary><c>Ctrl+Maj+N</c>, la planche ayant le focus : le dialogue, seul au-dessus de la fenêtre.</summary>
    private static InsererDesPages Ouvrir(Window fenetre)
    {
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 500, 700));
        Rendu.Touche(fenetre, PhysicalKey.N, RawInputModifiers.Control | RawInputModifiers.Shift);
        Rendu.Battre(8);
        var dialogue = Assert.Single(fenetre.OwnedWindows.OfType<InsererDesPages>());
        Rendu.UneImage();
        return dialogue;
    }

    /// <summary>Tape un nombre dans un champ du dialogue : un clic, tout pris, la frappe.</summary>
    private static void Remplir(InsererDesPages dialogue, string nom, string texte)
    {
        var champ = Trouver<ChampNumerique>(dialogue, nom);
        Rendu.Cliquer(champ.GetVisualDescendants().OfType<TextBox>().First());
        Rendu.Touche(dialogue, PhysicalKey.A, RawInputModifiers.Control);
        Rendu.Taper(dialogue, texte);
    }

    private static void Valider(Window fenetre, InsererDesPages dialogue)
    {
        Rendu.Cliquer(Trouver<Button>(dialogue, "BoutonOk"));
        Rendu.Battre(8);
        Assert.Empty(fenetre.OwnedWindows.OfType<InsererDesPages>());
    }

    /// <summary>
    /// La recette de A8 : un document de 3 pages, la page 2 courante ; insérer 4 pages avant, copiées
    /// de la page 1. On trouve 7 pages ; les pages 2 à 5 portent autant de blocs que la page 1, en
    /// instances distinctes ; les noms sont renumérotés, et une seule annulation rend les 3 pages.
    /// </summary>
    [AvaloniaFact]
    public void Quatre_pages_avant_copiees_de_la_page_1_en_un_pas()
    {
        var (fenetre, vm) = TroisPages();
        var avant = Etat(vm);
        var premiere = vm.Document.Pages[0];

        var dialogue = Ouvrir(fenetre);
        Remplir(dialogue, "NombreDeNouvellesPages", "4");
        Rendu.Cliquer(Trouver<RadioButton>(dialogue, "AvantLaPage"));
        Rendu.Cliquer(Trouver<RadioButton>(dialogue, "DupliquerLesObjets"));
        Remplir(dialogue, "PageACopier", "1");
        Valider(fenetre, dialogue);

        var pages = vm.Document.Pages;
        Assert.Equal(7, pages.Count);
        Assert.Same(premiere, pages[0]);
        for (var i = 1; i <= 4; i++)
        {
            Assert.Equal(premiere.Frames.Count, pages[i].Frames.Count);
            Assert.Equal(premiere.Frames.Select(b => b.Name), pages[i].Frames.Select(b => b.Name));
            foreach (var bloc in pages[i].Frames)
            {
                Assert.DoesNotContain(pages.Where(p => !ReferenceEquals(p, pages[i])).SelectMany(p => p.Frames),
                    autre => ReferenceEquals(autre, bloc));
            }
        }

        Assert.Equal("Seul", Assert.Single(pages[5].Frames).Name);
        Assert.Equal(Enumerable.Range(1, 7).Select(n => $"Page {n}"), pages.Select(p => p.Name));
        Assert.Same(pages[1], vm.CurrentPage);
        Assert.Equal("2", Trouver<TextBox>(fenetre, "NumeroDePage").Text);
        Assert.Equal("sur 7", Trouver<TextBlock>(fenetre, "TotalDesPages").Text);

        Rendu.UnSeulPas(vm, avant, "insérer 4 pages");
        Assert.Equal(3, vm.Document.Pages.Count);
    }

    /// <summary>
    /// L'option « zone de texte » pose exactement un bloc de texte par page, égal à la zone des
    /// marges ; par défaut, après la page courante.
    /// </summary>
    [AvaloniaFact]
    public void Une_zone_de_texte_par_page_egale_a_la_zone_des_marges()
    {
        var (fenetre, vm) = TroisPages();
        var avant = Etat(vm);

        var dialogue = Ouvrir(fenetre);
        Remplir(dialogue, "NombreDeNouvellesPages", "2");
        Rendu.Cliquer(Trouver<RadioButton>(dialogue, "UneZoneDeTexte"));
        Valider(fenetre, dialogue);

        var document = vm.Document;
        Assert.Equal(5, document.Pages.Count);
        Assert.Equal("Seul", document.Pages[1].Frames.Single().Name);
        foreach (var page in document.Pages.Skip(2).Take(2))
        {
            var bloc = Assert.IsType<TextFrame>(Assert.Single(page.Frames));
            Assert.Equal(document.MarginLeft, bloc.X);
            Assert.Equal(document.MarginTop, bloc.Y);
            Assert.Equal(document.MarginBoxWidth, bloc.Width);
            Assert.Equal(document.MarginBoxHeight, bloc.Height);
        }

        Assert.Same(document.Pages[2], vm.CurrentPage);
        Rendu.UnSeulPas(vm, avant, "insérer 2 pages à zone de texte");
    }

    /// <summary>
    /// Le numéro de la page à copier est grisé tant que son option n'est pas choisie, comme chez
    /// Publisher (p. 22) ; il propose la page courante. Des pages vierges, l'option par défaut,
    /// n'ont aucun bloc ; Échap ferme le dialogue sans rien insérer.
    /// </summary>
    [AvaloniaFact]
    public void Le_numero_de_la_page_a_copier_reste_grise_tant_que_son_option_n_est_pas_choisie()
    {
        var (fenetre, vm) = TroisPages();
        var avant = Etat(vm);

        var dialogue = Ouvrir(fenetre);
        var page = Trouver<ChampNumerique>(dialogue, "PageACopier");
        Assert.False(page.IsEffectivelyEnabled);
        Assert.Equal(2m, page.Value);
        Assert.Equal(3m, page.Maximum);
        Assert.True(Trouver<RadioButton>(dialogue, "PagesVierges").IsChecked);
        Assert.True(Trouver<RadioButton>(dialogue, "ApresLaPage").IsChecked);

        Rendu.Cliquer(Trouver<RadioButton>(dialogue, "DupliquerLesObjets"));
        Assert.True(page.IsEffectivelyEnabled);
        Rendu.Cliquer(Trouver<RadioButton>(dialogue, "UneZoneDeTexte"));
        Assert.False(page.IsEffectivelyEnabled);

        Rendu.Touche(dialogue, PhysicalKey.Escape);
        Rendu.Battre(8);
        Assert.Empty(fenetre.OwnedWindows.OfType<InsererDesPages>());
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo);

        // Des pages vierges, après la page courante, au menu Page.
        Rendu.Cliquer(MenuPage(fenetre));
        Rendu.Cliquer(fenetre.GetVisualDescendants().OfType<MenuItem>().Single(m => Equals(m.Header, "_Insérer des pages…")));
        Rendu.Battre(8);
        dialogue = Assert.Single(fenetre.OwnedWindows.OfType<InsererDesPages>());
        Rendu.UneImage();
        Valider(fenetre, dialogue);
        Assert.Equal(4, vm.Document.Pages.Count);
        Assert.Empty(vm.Document.Pages[2].Frames);
        Assert.Equal("Page 3", vm.Document.Pages[2].Name);
        Assert.Equal("Page 4", vm.Document.Pages[3].Name);
        Rendu.UnSeulPas(vm, avant, "insérer une page vierge");
    }

    private static MenuItem MenuPage(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<MenuItem>().Single(m => Equals(m.Header, "_Page"));
}
