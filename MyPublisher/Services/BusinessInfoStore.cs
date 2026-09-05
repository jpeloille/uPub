using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using MyPublisher.Models;

namespace MyPublisher.Services;

/// <summary>
/// Les jeux de coordonnées, rangés hors document — dans la configuration de
/// l'utilisateur, parce qu'ils le suivent d'un fichier à l'autre. Plusieurs jeux
/// coexistent, un seul est actif à la fois : c'est ce qui permet de composer tantôt
/// pour une société, tantôt pour une autre, sans retoucher les documents.
/// </summary>
public sealed class BusinessInfoStore
{
    public string ActiveSetName { get; set; } = "";

    public List<BusinessInfo> Sets { get; set; } = [];

    /// <summary>
    /// Le jeu actif, ou un jeu vide s'il n'y en a aucun : les champs de coordonnées
    /// rendent alors une chaîne vide plutôt que de faire échouer l'export.
    /// </summary>
    public BusinessInfo Active =>
        Sets.FirstOrDefault(set => set.SetName == ActiveSetName) ?? Sets.FirstOrDefault() ?? new BusinessInfo();

    public static string DefaultPath
    {
        get
        {
            var configuration = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(configuration))
            {
                configuration = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }

            return Path.Combine(configuration, "MyPublisher", "informations.json");
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Relit les jeux. Un fichier absent ou illisible rend un magasin vide : des
    /// coordonnées mal formées ne doivent pas empêcher d'ouvrir un document.
    /// </summary>
    public static BusinessInfoStore Load(string? path = null)
    {
        path ??= DefaultPath;

        try
        {
            if (!File.Exists(path)) return new BusinessInfoStore();
            return JsonSerializer.Deserialize<BusinessInfoStore>(File.ReadAllText(path), Options)
                   ?? new BusinessInfoStore();
        }
        catch (Exception)
        {
            return new BusinessInfoStore();
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;

        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }

    /// <summary>Copie indépendante, pour qu'un dialogue puisse être abandonné.</summary>
    public BusinessInfoStore Clone() => new()
    {
        ActiveSetName = ActiveSetName,
        Sets = Sets.Select(set => set.Clone()).ToList()
    };
}
