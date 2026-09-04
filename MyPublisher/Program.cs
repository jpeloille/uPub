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
        if (TryRunHeadlessExport(args, out var exitCode)) return exitCode;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    /// <summary>
    /// Mode sans interface : « MyPublisher doc.mypub --export sortie.pdf ».
    /// Permet de régénérer un PDF depuis un script ou une chaîne d'intégration.
    /// </summary>
    private static bool TryRunHeadlessExport(string[] args, out int exitCode)
    {
        exitCode = 0;
        var flag = Array.IndexOf(args, "--export");
        if (flag < 0) return false;

        if (flag == 0 || flag + 1 >= args.Length)
        {
            Console.Error.WriteLine("Usage : MyPublisher <document.mypub> --export <sortie.pdf>");
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
            var report = PdfExporter.Export(DocumentSerializer.Load(source), target);
            Console.WriteLine($"{report.PageCount} page(s) exportée(s) vers {target}");
            foreach (var warning in report.Warnings) Console.Error.WriteLine($"Avertissement : {warning}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Échec de l'export : {ex.Message}");
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
