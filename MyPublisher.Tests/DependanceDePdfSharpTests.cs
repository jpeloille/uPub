using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Text.Json;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// La faille de la dépendance de PdfSharp — le poste Q27.
///
/// PdfSharp 6.2.1 tire <c>System.Security.Cryptography.Pkcs</c> 8.0.1, que visent
/// GHSA-846f-f764-76mx (haute) et GHSA-2fmv-rv2c-59x7 (moyenne), sur la série 8.0 jusqu'à la
/// 8.0.1. Le noyau épingle la 8.0.2, correctif de la même série ; aucune version de PdfSharp
/// publiée ne la demande d'elle-même. Ces tests lisent ce que le processus a réellement reçu :
/// le relevé de dépendances que la compilation a écrit à côté du binaire, et l'assemblage
/// chargé. Ni réseau ni horloge — c'est ce que le juge peut éprouver hors ligne, quand la base
/// des vulnérabilités, elle, se lit par <c>Scripts/verifier-dependances.sh</c>.
/// </summary>
[Trait("poste", "Q27")]
public class DependanceDePdfSharpTests
{
    private const string Pkcs = "System.Security.Cryptography.Pkcs";

    /// <summary>La première version corrigée de chaque série que les deux avancées visent.</summary>
    private static readonly Version[] Corrigees = [new(8, 0, 2), new(9, 0, 21), new(10, 0, 13)];

    /// <summary>Le relevé de dépendances du processus (« MyPublisher.Tests.deps.json »).</summary>
    private static JsonElement Releve()
    {
        var chemin = Path.Combine(Path.GetDirectoryName(typeof(DependanceDePdfSharpTests).Assembly.Location)!,
            "MyPublisher.Tests.deps.json");
        Assert.True(File.Exists(chemin), $"le relevé de dépendances du processus est introuvable ({chemin})");
        return JsonDocument.Parse(File.ReadAllText(chemin)).RootElement;
    }

    /// <summary>
    /// La version d'un paquet au relevé, et la clé « nom/version » qui la porte. Le nom se lit sans
    /// la casse : PdfSharp s'y écrit « PDFsharp ».
    /// </summary>
    private static (Version Version, string Cle) Paquet(JsonElement releve, string nom)
    {
        var cles = releve.GetProperty("libraries").EnumerateObject()
            .Where(p => p.Name.StartsWith(nom + "/", StringComparison.OrdinalIgnoreCase)
                        && p.Value.GetProperty("type").GetString() == "package")
            .Select(p => p.Name).ToList();
        Assert.True(cles.Count == 1, $"{nom} figure {cles.Count} fois au relevé : {string.Join(", ", cles)}");
        return (Version.Parse(cles[0][(nom.Length + 1)..]), cles[0]);
    }

    private static bool EstCorrigee(Version version) =>
        Corrigees.Any(c => c.Major == version.Major && version >= c)
        || version.Major > Corrigees.Max(c => c.Major);

    /// <summary>
    /// La recette du plan, lue dans le binaire et non dans le <c>.csproj</c> : le relevé que la
    /// compilation a écrit porte une version corrigée de <c>Pkcs</c>, et l'assemblage que le
    /// processus charge est bien celui de ce paquet-là — son numéro de fichier est celui que le
    /// relevé lui attribue. Sans l'épinglage du noyau, la restauration résout la 8.0.1.
    /// </summary>
    [Fact]
    public void Le_processus_charge_une_version_corrigee_de_Pkcs()
    {
        var releve = Releve();
        var (version, cle) = Paquet(releve, Pkcs);
        Assert.True(EstCorrigee(version),
            $"{cle} : une version que GHSA-846f-f764-76mx et GHSA-2fmv-rv2c-59x7 visent — l'épinglage du noyau a-t-il disparu ?");

        var cible = releve.GetProperty("targets").EnumerateObject().First().Value.GetProperty(cle);
        var attendu = cible.GetProperty("runtime").EnumerateObject().Single().Value.GetProperty("fileVersion").GetString();
        var charge = typeof(SignedCms).Assembly;
        Assert.Equal(attendu, FileVersionInfo.GetVersionInfo(charge.Location).FileVersion);
    }

    /// <summary>
    /// Le plus petit pas : le moteur de l'export n'a pas bougé. PdfSharp reste en 6.2.1 — aucune
    /// version publiée après elle ne tire une version corrigée de <c>Pkcs</c> (6.2.2 à 6.5.0
    /// demandent toutes la 8.0.1), et en monter une aurait changé l'export pour rien. Qui monte
    /// PdfSharp exprès change ce test, et rejoue la comparaison des PDF de
    /// <c>Scripts/recettes/Q27-pkcs.sh</c>.
    /// </summary>
    [Fact]
    public void PdfSharp_reste_en_6_2_1()
    {
        var (pdfSharp, cle) = Paquet(Releve(), "PdfSharp");
        Assert.True(pdfSharp == new Version(6, 2, 1), $"{cle} : le moteur de l'export a changé de version.");
    }
}
