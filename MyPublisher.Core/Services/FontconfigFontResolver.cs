using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PdfSharp.Fonts;

namespace MyPublisher.Services;

/// <summary>
/// Ce qu'une demande de police a réellement obtenu : la famille employée, le fichier
/// qui la porte, et — quand ce n'est pas celle qu'on demandait — la raison de l'écart.
///
/// <see cref="Motif"/> nul veut dire que la machine a rendu la police demandée. C'est
/// le seul cas où il n'y a rien à dire à l'utilisateur.
/// </summary>
public sealed record ResolutionPolice(string Demandee, string Employee, string Fichier, string? Motif)
{
    /// <summary>La famille obtenue est celle demandée.</summary>
    public bool Exacte => Motif is null;
}

/// <summary>
/// PDFsharp ne connaît pas les polices Linux : sans résolveur, l'export échoue dès
/// qu'on demande autre chose qu'une police embarquée. On délègue à fontconfig
/// (fc-match), qui est la source de vérité du système et gère aussi les
/// substitutions quand la famille demandée n'existe pas.
///
/// Ces substitutions étaient muettes jusqu'au 6 septembre 2026, et ce n'était pas
/// qu'une question d'esthétique : 119 des 329 fragments des fiches ATR sont en
/// Wingdings ou Wingdings 3 — les flèches et les symboles des procédures d'urgence.
/// Une flèche « descendre » remplacée sans un mot par une lettre est une consigne
/// fausse. Le résolveur compare donc la famille demandée à celle obtenue, et
/// <see cref="Resoudre"/> rend l'écart à qui veut bien le dire.
/// </summary>
public sealed class FontconfigFontResolver : IFontResolver
{
    private static readonly ConcurrentDictionary<string, ResolutionPolice> Cache = new();
    private static readonly ConcurrentDictionary<string, byte[]> DataCache = new();
    private static readonly object InstallLock = new();
    private static bool _installed;

    /// <summary>Police de repli si fontconfig est absent ou muet.</summary>
    private const string Fallback = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";

    /// <summary>
    /// La famille que porte ce fichier-là, écrite ici plutôt que demandée à
    /// fontconfig : le repli sert précisément quand on ne peut pas l'interroger.
    /// </summary>
    private const string FamilleDeRepli = "DejaVu Sans";

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

    /// <summary>
    /// Ce que la machine rendra pour cette police-là. C'est le même chemin de code et
    /// le même cache que ce qu'emploie l'export : on ne peut donc pas annoncer une
    /// substitution et en dessiner une autre.
    /// </summary>
    public static ResolutionPolice Resoudre(string familyName, bool isBold, bool isItalic) =>
        Cache.GetOrAdd($"{familyName}|{isBold}|{isItalic}", _ => Match(familyName, isBold, isItalic));

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var resolution = Resoudre(familyName, isBold, isItalic);

        // Le chemin sert de nom de fonte : c'est lui que GetFont recevra ensuite.
        return string.IsNullOrEmpty(resolution.Fichier) ? null : new FontResolverInfo(resolution.Fichier);
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
    /// La famille demandée figure-t-elle parmi celles que fontconfig annonce ?
    ///
    /// fontconfig rend **tous** les noms d'une même famille, séparés par des virgules :
    /// « DejaVu Sans,DejaVu Sans Condensed » pour la condensée, « Lato,Lato Semibold »
    /// pour la demi-grasse, « Malgun Gothic,맑은 고딕 » pour un nom localisé. Comparer
    /// la chaîne entière annoncerait une substitution sur 81 polices pourtant
    /// installées, ici même — et un avertissement qui crie au loup sur des polices
    /// présentes est pire que le silence qu'il remplace : on apprend à ne plus le lire.
    /// </summary>
    public static bool Correspond(string famillesRendues, string demandee) =>
        Noms(famillesRendues).Any(nom => string.Equals(nom, demandee, StringComparison.OrdinalIgnoreCase));

    private static string[] Noms(string famillesRendues) =>
        famillesRendues.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Interroge fc-match. Les constantes numériques de fontconfig sont plus fiables
    /// que les noms de style, qui varient d'une fonderie à l'autre.
    ///
    /// On lui demande le fichier **et** la famille : fc-match rend toujours quelque
    /// chose, si bien que le fichier seul ne dit pas si l'on a obtenu ce qu'on
    /// demandait.
    /// </summary>
    private static ResolutionPolice Match(string famille, bool gras, bool italique)
    {
        var weight = gras ? 200 : 80;   // FC_WEIGHT_BOLD / FC_WEIGHT_REGULAR
        var slant = italique ? 100 : 0; // FC_SLANT_ITALIC / FC_SLANT_ROMAN
        var pattern = $"{famille}:weight={weight}:slant={slant}";

        string sortie;

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "fc-match",
                ArgumentList = { "-f", "%{file}\n%{family}", pattern },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });

            if (process is null) return Repli(famille, "fc-match indisponible");

            // La sortie d'erreur est redirigée pour ne pas salir celle de
            // l'application ; encore faut-il la vider. Un fc-match qui remplirait son
            // tampon d'erreur resterait bloqué en écriture pendant qu'on lit sa sortie
            // standard, et l'export avec lui — d'où la lecture asynchrone, avant celle
            // qui bloque.
            _ = process.StandardError.ReadToEndAsync();

            sortie = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
        }
        catch (Exception)
        {
            // fc-match absent : on ne fait pas échouer l'export pour autant.
            return Repli(famille, "fc-match indisponible");
        }

        var saut = sortie.IndexOf('\n');
        if (saut < 0) return Repli(famille, "fc-match n'a rien rendu");

        var fichier = sortie[..saut].Trim();
        var famillesRendues = sortie[(saut + 1)..].Trim();

        if (fichier.Length == 0 || !File.Exists(fichier)) return Repli(famille, "aucun fichier de police");

        // Une collection (.ttc) porte plusieurs fontes dans un même fichier ; PDFsharp
        // ne sait pas y choisir. La police est bien installée, et pourtant substituée :
        // le motif le dit, sans quoi l'avertissement serait incompréhensible.
        if (fichier.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
        {
            return Repli(famille, "collection .ttc, que l'export ne sait pas lire");
        }

        if (Correspond(famillesRendues, famille))
        {
            return new ResolutionPolice(famille, famille, fichier, null);
        }

        var noms = Noms(famillesRendues);
        return new ResolutionPolice(
            famille, noms.Length > 0 ? noms[0] : "", fichier, "absente de cette machine");
    }

    /// <summary>
    /// Le repli, et ce qu'il faut en dire. Quand c'est justement DejaVu Sans qu'on
    /// demandait, il n'y a aucune substitution à signaler : le repli rend alors la
    /// police demandée.
    /// </summary>
    private static ResolutionPolice Repli(string demandee, string motif)
    {
        if (!File.Exists(Fallback)) return new ResolutionPolice(demandee, "", "", motif);

        return string.Equals(demandee, FamilleDeRepli, StringComparison.OrdinalIgnoreCase)
            ? new ResolutionPolice(demandee, demandee, Fallback, null)
            : new ResolutionPolice(demandee, FamilleDeRepli, Fallback, motif);
    }
}
