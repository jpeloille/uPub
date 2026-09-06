using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using PdfSharp.Fonts;

namespace MyPublisher.Services;

/// <summary>
/// PDFsharp ne connaît pas les polices Linux : sans résolveur, l'export échoue dès
/// qu'on demande autre chose qu'une police embarquée. On délègue à fontconfig
/// (fc-match), qui est la source de vérité du système et gère aussi les
/// substitutions quand la famille demandée n'existe pas.
/// </summary>
public sealed class FontconfigFontResolver : IFontResolver
{
    private static readonly ConcurrentDictionary<string, string> PathCache = new();
    private static readonly ConcurrentDictionary<string, byte[]> DataCache = new();
    private static readonly object InstallLock = new();
    private static bool _installed;

    /// <summary>Police de repli si fontconfig est absent ou muet.</summary>
    private const string Fallback = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";

    /// <summary>
    /// PDFsharp lève une exception si le résolveur global est réaffecté : on
    /// n'installe qu'une fois, quel que soit le nombre d'exports.
    /// </summary>
    public static void EnsureInstalled()
    {
        lock (InstallLock)
        {
            if (_installed) return;
            GlobalFontSettings.FontResolver = new FontconfigFontResolver();
            _installed = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var key = $"{familyName}|{isBold}|{isItalic}";
        var path = PathCache.GetOrAdd(key, _ => Match(familyName, isBold, isItalic));
        return string.IsNullOrEmpty(path) ? null : new FontResolverInfo(path);
    }

    public byte[]? GetFont(string faceName)
    {
        return DataCache.GetOrAdd(faceName, path =>
        {
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (IOException)
            {
                return File.Exists(Fallback) ? File.ReadAllBytes(Fallback) : [];
            }
        });
    }

    /// <summary>
    /// Interroge fc-match. Les constantes numériques de fontconfig sont plus fiables
    /// que les noms de style, qui varient d'une fonderie à l'autre.
    /// </summary>
    private static string Match(string familyName, bool isBold, bool isItalic)
    {
        var weight = isBold ? 200 : 80;   // FC_WEIGHT_BOLD / FC_WEIGHT_REGULAR
        var slant = isItalic ? 100 : 0;   // FC_SLANT_ITALIC / FC_SLANT_ROMAN
        var pattern = $"{familyName}:weight={weight}:slant={slant}";

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "fc-match",
                ArgumentList = { "-f", "%{file}", pattern },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });

            if (process is null) return FallbackIfPresent();

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            // Les .ttc (collections) ne sont pas gérées par PDFsharp : on retombe sur le repli.
            if (output.Length > 0 && File.Exists(output) &&
                !output.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
            {
                return output;
            }
        }
        catch (Exception)
        {
            // fc-match absent : on ne fait pas échouer l'export pour autant.
        }

        return FallbackIfPresent();
    }

    private static string FallbackIfPresent() => File.Exists(Fallback) ? Fallback : "";
}
