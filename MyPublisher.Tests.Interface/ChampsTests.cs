using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les champs (C4), du côté de l'écran : « un folio posé sur un modèle se numérote seul
/// sur huit pages, à l'écran et dans le PDF ». Le PDF est relu par la recette, avec
/// <c>pdftotext</c> ; ce qui demande la fenêtre est ici.
///
/// Ce qu'on relève, c'est le contenu que <see cref="TexteCompose"/> reçoit pour le
/// dessiner — le texte déjà passé par le résolveur, avec le contexte de la page
/// affichée —, et non les pixels. Le folio se calcule à partir de la page COURANTE de la
/// fenêtre, et non d'un rang que le bloc porterait : un bloc ignore sur quelle page il
/// est. C'est donc en changeant de page qu'on l'éprouve — par le modèle de vue
/// (<c>CurrentPage</c>), et non par un geste de l'utilisateur.
/// </summary>
public class ChampsTests
{
    private const int Pages = 8;

    private static TextFrame Champ(string nom, FieldKind champ, double x, double y) => new()
    {
        Name = nom, X = x, Y = y, Width = 60, Height = 24,
        FontFamily = "DejaVu Sans", FontSize = 11, LineHeight = 1,
        Text = "?", Field = champ,
    };

    /// <summary>
    /// Huit pages identiques, comme un modèle dupliqué : un folio posé seul, et un
    /// second dans un groupe avec le nombre de pages. Le groupe compte parce que ses
    /// blocs se dessinent par un autre chemin — un gabarit qui descend dans ses
    /// enfants — et que rien n'assure qu'il leur passe le contexte de la page.
    /// </summary>
    private static Document Huit()
    {
        var document = Document.CreateDefault();
        document.Pages.Clear();

        for (var i = 0; i < Pages; i++)
        {
            var groupe = new GroupFrame { Name = "Pied", X = 60, Y = 760, Width = 200, Height = 24 };
            groupe.Children.Add(Champ("Folio du groupe", FieldKind.PageNumber, 0, 0));
            groupe.Children.Add(Champ("Nombre de pages", FieldKind.PageCount, 100, 0));

            document.Pages.Add(new Page
            {
                Name = $"Page {i + 1}",
                Frames = { Champ("Folio", FieldKind.PageNumber, 480, 760), groupe },
            });
        }

        return document;
    }

    /// <summary>Ce que chaque bloc de texte de la planche affiche, par nom de bloc.</summary>
    private static Dictionary<string, string?> Affiche(Window fenetre) =>
        fenetre.GetVisualDescendants()
            .OfType<TexteCompose>()
            .Where(controle => controle.Bloc is not null)
            .ToDictionary(controle => controle.Bloc!.Name, controle => controle.Contenu);

    [AvaloniaFact]
    [Trait("poste", "C4")]
    public void Le_folio_se_numerote_seul_sur_huit_pages_a_lecran()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Huit());

        for (var i = 0; i < Pages; i++)
        {
            vm.CurrentPage = vm.Document.Pages[i];
            Rendu.Battre();

            var affiche = Affiche(fenetre);
            var attendu = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Trois blocs, et pas davantage : la planche ne montre que la page courante.
            // Un relevé qui verrait les blocs de plusieurs pages à la fois trouverait
            // toujours le bon numéro quelque part.
            Assert.Equal(3, affiche.Count);
            Assert.Equal(attendu, affiche["Folio"]);
            Assert.Equal(attendu, affiche["Folio du groupe"]);
            Assert.Equal("8", affiche["Nombre de pages"]);
        }
    }

    [AvaloniaFact]
    [Trait("poste", "C4")]
    public void Le_texte_saisi_sous_le_champ_reparait_quand_on_le_retire()
    {
        // C4a l'a promis : le texte saisi reste sous le champ. Retirer le champ, c'est
        // retrouver ce qu'on avait écrit, et non un folio figé.
        var document = Huit();
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.CurrentPage = document.Pages[2];
        Rendu.Battre();

        Assert.Equal("3", Affiche(fenetre)["Folio"]);

        var folio = document.Pages[2].Frames.OfType<TextFrame>().Single(b => b.Name == "Folio");
        folio.Field = FieldKind.None;
        Rendu.Battre();

        Assert.Equal("?", Affiche(fenetre)["Folio"]);
    }
}
