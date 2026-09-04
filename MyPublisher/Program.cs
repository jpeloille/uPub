using Avalonia;
using System;
using System.IO;
using MyPublisher.Services;

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
    /// </summary>
    private static bool TryRunHeadless(string[] args, out int exitCode)
    {
        exitCode = 0;

        var export = Array.IndexOf(args, "--export");
        var resave = Array.IndexOf(args, "--resave");
        var flag = export >= 0 ? export : resave;
        if (flag < 0) return false;

        if (flag == 0 || flag + 1 >= args.Length)
        {
            Console.Error.WriteLine("Usage : MyPublisher <document.mypub> --export <sortie.pdf>");
            Console.Error.WriteLine("        MyPublisher <document.mypub> --resave <copie.mypub>");
            exitCode = 2;
            return true;
        }

        var source = args[flag - 1];
        var target = args[flag + 1];

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
                var report = PdfExporter.Export(document, target);
                Console.WriteLine($"{report.PageCount} page(s) exportée(s) vers {target}");
                foreach (var warning in report.Warnings) Console.Error.WriteLine($"Avertissement : {warning}");
            }
            else
            {
                DocumentSerializer.Save(document, target);
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

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
