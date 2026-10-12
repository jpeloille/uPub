using System.IO;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Ce que l'export fait quand quelque chose se passe mal.
///
/// La règle du dépôt est constante : signaler et continuer. Une image manquante laisse
/// son cadre, une police absente est remplacée et dite, un texte trop long est coupé et
/// chiffré. Un export qui s'arrête sans rien écrire ne laisse, lui, aucun recours.
/// </summary>
public class ExportTests
{
    /// <summary>Un fichier qui porte l'extension d'une image sans en être une.</summary>
    private static string FausseImage()
    {
        var chemin = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
        File.WriteAllText(chemin, "ceci n'est pas une image");
        return chemin;
    }

    [Fact]
    public void Une_image_illisible_en_fond_de_bloc_narrete_pas_lexport()
    {
        // Trouvé par la relecture de C5c : depuis que le remplissage peut ouvrir un
        // fichier, un fichier illisible levait hors de toute garde. Aucun PDF n'était
        // écrit — alors que la MÊME image posée en bloc image ne valait qu'un
        // avertissement, et que l'écran l'encaissait sans broncher.
        var image = FausseImage();
        var sortie = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");

        try
        {
            var document = Document.CreateDefault();
            document.Pages[0].Frames.Add(new ShapeFrame
            {
                Name = "Aplat", X = 10, Y = 10, Width = 100, Height = 50,
                Fill = new ImageFill { Path = image },
            });

            var rapport = PdfExporter.Export(document, sortie, ecran: null);

            Assert.Equal(1, rapport.PageCount);
            Assert.True(File.Exists(sortie), "aucun PDF écrit");
            Assert.Contains(rapport.Warnings, a => a.Contains("Aplat"));
        }
        finally
        {
            File.Delete(image);
            if (File.Exists(sortie)) File.Delete(sortie);
        }
    }

    [Fact]
    public void Une_image_illisible_en_fond_de_page_narrete_pas_lexport()
    {
        // Même chemin, sans bloc pour le porter : le fond de page se dessine avant tout
        // et n'avait aucune garde non plus.
        var image = FausseImage();
        var sortie = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");

        try
        {
            var document = Document.CreateDefault();
            document.Background = new ImageFill { Path = image };

            var rapport = PdfExporter.Export(document, sortie, ecran: null);

            Assert.True(File.Exists(sortie), "aucun PDF écrit");
            Assert.Contains(rapport.Warnings, a => a.Contains("Fond de page"));
        }
        finally
        {
            File.Delete(image);
            if (File.Exists(sortie)) File.Delete(sortie);
        }
    }
}
