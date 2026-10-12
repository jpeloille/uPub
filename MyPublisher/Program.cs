using Avalonia;
using System;
using System.Globalization;
using System.IO;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.Views;

namespace MyPublisher;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        if (TryRunHeadless(args, out var exitCode)) return exitCode;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    /// <summary>
    /// Mode sans interface, pour un script ou une chaîne d'intégration :
    /// « MyPublisher doc.mypub --export sortie.pdf » régénère un PDF,
    /// « MyPublisher doc.mypub --resave copie.mypub » relit le document et le réécrit
    /// sous un autre nom. La réécriture n'a d'intérêt que pour le contrôle : un
    /// document relu puis réécrit doit rendre exactement le même JSON.
    ///
    /// Deux options rendent l'export reproductible, et n'ont d'usage que là :
    /// « --date 2026-09-06 » fixe la date des champs, sans quoi un document qui en
    /// porte un produit un PDF différent chaque jour ; « --contacts fichier.json »
    /// désigne les coordonnées à employer, sans quoi l'export lit celles de
    /// l'utilisateur — un fichier hors du dépôt, qu'aucun « git status » ne montre.
    /// </summary>
    private static bool TryRunHeadless(string[] args, out int exitCode)
    {
        exitCode = 0;

        var export = Array.IndexOf(args, "--export");
        var resave = Array.IndexOf(args, "--resave");
        var flag = export >= 0 ? export : resave;
        if (flag < 0) return false;

        // Le document est le premier argument qui ne soit ni une option, ni la valeur
        // d'une option. Le repérer comme « celui qui précède le drapeau » marchait tant
        // qu'il n'y avait qu'un drapeau ; « --date 2026-09-06 --export x.pdf » aurait
        // pris la date pour le document, et ouvert un fichier inexistant.
        var source = Document(args);
        var target = flag + 1 < args.Length ? args[flag + 1] : null;

        if (source is null || target is null)
        {
            Console.Error.WriteLine("Usage : MyPublisher <document.mypub> --export <sortie.pdf>");
            Console.Error.WriteLine("        MyPublisher <document.mypub> --resave <copie.mypub>");
            Console.Error.WriteLine("        options : --date <AAAA-MM-JJ> --contacts <fichier.json>");
            exitCode = 2;
            return true;
        }

        if (!File.Exists(source))
        {
            Console.Error.WriteLine($"Document introuvable : {source}");
            exitCode = 1;
            return true;
        }

        try
        {
            var document = DocumentSerializer.Load(source);

            if (flag == export)
            {
                // Les mêmes coordonnées qu'à l'écran : un export scripté doit rendre
                // exactement ce que l'application aurait produit.
                var contacts = BusinessInfoStore.Load(Value(args, "--contacts")).Active;
                var date = Value(args, "--date") is { } jour
                    ? DateTime.Parse(jour, CultureInfo.InvariantCulture)
                    : (DateTime?)null;

                var report = PdfExporter.Export(document, target, MesureDeLEcran(), contacts, date);
                Console.WriteLine($"{report.PageCount} page(s) exportée(s) vers {target}");
                foreach (var warning in report.Warnings) Console.Error.WriteLine($"Avertissement : {warning}");
            }
            else
            {
                // « --resave » n'est pas un « enregistrer sous » : il réécrit le document
                // tel qu'il s'écrit CHEZ LUI. Les chemins d'images restent donc rapportés
                // au dossier de la source, et non à celui du fichier produit — lequel n'a
                // d'autre destin que d'être recopié par-dessus la source, ce qui est la
                // procédure de régénération des Samples et des Templates. Relativiser sur
                // /tmp graverait dans un modèle livré des chemins absolus propres à cette
                // machine, sans que rien ne le dise.
                DocumentSerializer.Save(document, target, Path.GetDirectoryName(Path.GetFullPath(source)));
                Console.WriteLine($"Document réécrit vers {target}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Échec : {ex.Message}");
            exitCode = 1;
        }

        return true;
    }

    /// <summary>
    /// La règle à mesurer de l'écran, pour un export sans fenêtre.
    ///
    /// L'export coupe ses lignes sur la plus large de sa mesure et de celle de l'écran
    /// (poste Q13) : sans elle, « --export » rendrait un PDF coupé autrement que ce que
    /// l'application aurait produit, et c'est le PDF de référence du contrôle P2 qui
    /// mentirait. On monte donc d'Avalonia ce qu'il faut pour mesurer un texte — Skia,
    /// ses polices et son façonnage —, sans plate-forme de fenêtrage : un export se
    /// lance aussi d'une session sans affichage. Les polices sont celles de
    /// <see cref="BuildAvaloniaApp"/>, la collection des documents comprise, pour que la mesure soit
    /// celle de l'application et non une voisine.
    ///
    /// Sans la porte privée d'Avalonia (<see cref="PolicesDuDocument.Indisponible"/>),
    /// l'export REFUSE : la mesure de l'écran dépendrait alors de ce que ce processus-ci
    /// a chargé, et non de ce que l'application aurait montré. Il échoue avant d'écrire,
    /// avec un code non nul — un PDF muet et vert est ce que la relecture de Q13 a trouvé.
    /// </summary>
    private static Compositeur.IMetriques MesureDeLEcran()
    {
        if (Application.Current is null)
        {
            AppBuilder.Configure<Application>()
                .UseStandardRuntimePlatformSubsystem()
                .UseSkia()
                .AvecLesPolicesDeMyPublisher()
                .UseWindowingSubsystem(() => { }, "aucun")
                .SetupWithoutStarting();
        }

        if (PolicesDuDocument.Indisponible is { } motif)
        {
            throw new InvalidOperationException(
                $"la mesure de l'écran est indisponible, aucun PDF n'est écrit. {motif}");
        }

        return new MetriquesAvalonia();
    }

    /// <summary>Valeur d'une option nommée, ou <c>null</c> si elle n'est pas donnée.</summary>
    private static string? Value(string[] args, string option)
    {
        var i = Array.IndexOf(args, option);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>Toutes les options qui consomment l'argument suivant.</summary>
    private static readonly string[] OptionsAvecValeur =
        ["--export", "--resave", "--date", "--contacts"];

    /// <summary>
    /// Le document à ouvrir : le premier argument libre, une fois les options et leurs
    /// valeurs écartées.
    /// </summary>
    private static string? Document(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (Array.IndexOf(OptionsAvecValeur, args[i]) >= 0) { i++; continue; }
            if (args[i].StartsWith('-')) continue;
            return args[i];
        }

        return null;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Inter pour le chrome, et la collection des polices des documents : le
            // banc, l'hôte des tests et l'export sans fenêtre montent les mêmes.
            .AvecLesPolicesDeMyPublisher()
            .LogToTrace();
}
