using System;
using System.Globalization;
using System.Linq;

namespace MyPublisher.Banc;

/// <summary>
/// Point d'entrée du banc, pour que les scripts de recette aient une commande à
/// appeler plutôt qu'un test à filtrer.
///
///   MyPublisher.Banc rendre &lt;doc.mypub&gt; --sortie &lt;image.png&gt;
///                          [--page N] [--pixels-par-point 2] [--sans-reperes]
///   MyPublisher.Banc mots &lt;doc&gt; --sortie &lt;lignes.json&gt;
///                          [--charger-d-abord romain|gras|italique] [--coupure-seule]
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--aide" or "-h")
        {
            Aide();
            return 2;
        }

        try
        {
            return args[0] switch
            {
                "rendre" => Rendre(args),
                "mots" => Mots.Lancer(args),
                _ => Inconnu(args[0]),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Échec : {ex.Message}");
            return 1;
        }
    }

    private static int Rendre(string[] args)
    {
        var document = args.Length > 1 && !args[1].StartsWith('-') ? args[1] : null;
        var sortie = Option(args, "--sortie");
        if (document is null || sortie is null)
        {
            Aide();
            return 2;
        }

        var page = int.Parse(Option(args, "--page") ?? "1", CultureInfo.InvariantCulture);
        var echelle = double.Parse(
            Option(args, "--pixels-par-point") ?? "2", CultureInfo.InvariantCulture);
        var sansReperes = args.Contains("--sans-reperes");

        Banc.Configurer().SetupWithoutStarting();

        var (fenetre, vm) = Banc.Ouvrir(document);

        // Les repères ne s'impriment jamais : pour comparer l'écran au PDF, il faut
        // pouvoir les éteindre, sinon l'écart mesuré est celui qu'on a demandé.
        if (sansReperes)
        {
            vm.ShowGuides = false;
            vm.Selection.Clear();
        }

        if (page < 1 || page > vm.Document.Pages.Count)
            throw new ArgumentOutOfRangeException(
                nameof(page), $"Page {page} demandée, le document en a {vm.Document.Pages.Count}.");

        vm.CurrentPage = vm.Document.Pages[page - 1];
        Banc.Battre();

        using var image = Banc.Rendre(Banc.Page(fenetre), echelle);
        image.Save(sortie);

        Console.WriteLine(
            $"{image.PixelSize.Width}x{image.PixelSize.Height} pixels écrits dans {sortie} " +
            $"({echelle.ToString(CultureInfo.InvariantCulture)} px/pt, page {page})");
        return 0;
    }

    private static string? Option(string[] args, string nom)
    {
        var i = Array.IndexOf(args, nom);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int Inconnu(string verbe)
    {
        Console.Error.WriteLine($"Commande inconnue : {verbe}");
        Aide();
        return 2;
    }

    private static void Aide()
    {
        Console.Error.WriteLine(
            "Usage : MyPublisher.Banc rendre <doc.mypub> --sortie <image.png>");
        Console.Error.WriteLine(
            "                              [--page N] [--pixels-par-point 2] [--sans-reperes]");
        Console.Error.WriteLine(
            "        MyPublisher.Banc mots <doc> --sortie <lignes.json>");
        Console.Error.WriteLine(
            "                              [--charger-d-abord romain|gras|italique] [--coupure-seule]");
    }
}
