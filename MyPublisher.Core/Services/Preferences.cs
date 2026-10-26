using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyPublisher.Services;

/// <summary>
/// Les réglages de l'utilisateur, rangés hors du document — dans sa configuration, parce qu'ils
/// le suivent d'un document à l'autre et d'une session à la suivante (poste N11). Le poste y
/// range les six derniers documents ouverts ou enregistrés ; A11 y rangera l'aimantation, N25 le
/// dictionnaire de l'utilisateur. Ce que ce fichier ne connaît pas encore — une clé qu'une
/// version plus récente y aurait écrite — est relu et réécrit tel quel, sans être perdu.
///
/// Un magasin chargé sans chemin (<see cref="EnMemoire"/>) ne s'écrit jamais : c'est celui que
/// le modèle de vue prend quand on ne lui en donne pas, si bien qu'un banc ou un test qui
/// construit une fenêtre ne peut pas écrire dans la configuration réelle de la machine. Seul
/// <c>App.axaml.cs</c> charge le magasin de l'utilisateur ; les tests et les recettes en
/// chargent un dans un dossier temporaire, par paramètre ou par la variable
/// <see cref="VariableDuDossier"/>.
/// </summary>
public sealed class Preferences
{
    /// <summary>Le nombre de documents récents que garde le menu Fichier, comme Publisher (p. 61).</summary>
    public const int NombreDeRecents = 6;

    /// <summary>
    /// La variable d'environnement qui désigne le dossier des préférences, à la place de celui de
    /// l'utilisateur : la recette et les tests la pointent vers un dossier temporaire.
    /// </summary>
    public const string VariableDuDossier = "MYPUBLISHER_PREFERENCES";

    public const string NomDuFichier = "preferences.json";

    /// <summary>Les derniers documents ouverts ou enregistrés, chemins complets, le plus récent en tête.</summary>
    public List<string> DocumentsRecents { get; set; } = [];

    /// <summary>Les clés que cette version ne connaît pas : relues, puis réécrites telles quelles.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AutresReglages { get; set; }

    /// <summary>Le fichier où le magasin s'écrit, ou <c>null</c> pour un magasin en mémoire, qui ne s'écrit jamais.</summary>
    [JsonIgnore]
    public string? Chemin { get; private set; }

    /// <summary>Un magasin vide, qui ne s'écrit nulle part.</summary>
    public static Preferences EnMemoire() => new();

    /// <summary>
    /// Le dossier des préférences : celui que désigne <see cref="VariableDuDossier"/> si elle est
    /// posée, sinon <c>MyPublisher</c> dans la configuration de l'utilisateur — là où sont déjà
    /// ses coordonnées (<see cref="BusinessInfoStore"/>).
    /// </summary>
    public static string DossierParDefaut
    {
        get
        {
            if (Environment.GetEnvironmentVariable(VariableDuDossier) is { Length: > 0 } dossier) return dossier;

            var configuration = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(configuration))
            {
                configuration = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }

            return Path.Combine(configuration, "MyPublisher");
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Relit les préférences du dossier donné, ou de <see cref="DossierParDefaut"/>. Un fichier
    /// absent ou illisible rend un magasin vide, qui s'écrira au même endroit : des préférences
    /// mal formées ne doivent pas empêcher d'ouvrir l'application.
    /// </summary>
    public static Preferences Charger(string? dossier = null)
    {
        var chemin = Path.Combine(dossier ?? DossierParDefaut, NomDuFichier);
        Preferences preferences;

        try
        {
            preferences = File.Exists(chemin)
                ? JsonSerializer.Deserialize<Preferences>(File.ReadAllText(chemin), Options) ?? new Preferences()
                : new Preferences();
        }
        catch (Exception)
        {
            preferences = new Preferences();
        }

        preferences.Chemin = chemin;
        preferences.DocumentsRecents = Nettoyer(preferences.DocumentsRecents ?? []);
        return preferences;
    }

    /// <summary>
    /// Écrit le magasin, s'il a un chemin : dans un fichier voisin d'abord, puis à sa place, pour
    /// qu'une écriture interrompue ne laisse pas un fichier tronqué.
    /// </summary>
    public void Enregistrer()
    {
        if (Chemin is null) return;

        var dossier = Path.GetDirectoryName(Chemin);
        if (!string.IsNullOrEmpty(dossier)) Directory.CreateDirectory(dossier);

        var provisoire = Chemin + ".tmp";
        File.WriteAllText(provisoire, JsonSerializer.Serialize(this, Options));
        File.Move(provisoire, Chemin, overwrite: true);
    }

    /// <summary>
    /// Met un document en tête des récents : le chemin complet, une seule fois — ouvert de
    /// nouveau, il remonte au lieu de se doubler —, six au plus, le plus ancien sortant.
    /// </summary>
    public void RetenirLeDocument(string chemin)
    {
        var complet = Path.GetFullPath(chemin);
        DocumentsRecents = Nettoyer([complet, .. DocumentsRecents]);
    }

    /// <summary>Retire un document des récents — celui qu'on n'a pas pu rouvrir.</summary>
    /// <returns>Vrai s'il y était.</returns>
    public bool OublierLeDocument(string chemin)
    {
        var complet = Path.GetFullPath(chemin);
        return DocumentsRecents.RemoveAll(recent => recent == complet) > 0;
    }

    /// <summary>
    /// Les récents tels qu'on les garde : sans entrée vide, sans doublon — le premier venu
    /// l'emporte, c'est le plus récent —, six au plus. Les chemins se comparent tels quels,
    /// comme le système de fichiers de Linux les distingue.
    /// </summary>
    private static List<string> Nettoyer(IEnumerable<string?> chemins) =>
        chemins.Where(chemin => !string.IsNullOrWhiteSpace(chemin))
            .Select(chemin => chemin!)
            .Distinct(StringComparer.Ordinal)
            .Take(NombreDeRecents)
            .ToList();
}
