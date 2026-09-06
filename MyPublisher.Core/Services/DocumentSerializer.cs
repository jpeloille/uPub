using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MyPublisher.Models;

namespace MyPublisher.Services;

/// <summary>
/// Lecture / écriture du format .mypub : du JSON lisible, avec un discriminant
/// "$kind" sur chaque bloc pour retrouver le bon type dérivé.
/// </summary>
public static class DocumentSerializer
{
    public const string Extension = "mypub";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,

        // L'encodeur par défaut échappe tout ce qui n'est pas ASCII, plus l'apostrophe :
        // un texte français ressort en « l\u0027unit\u00E9 », illisible dans un format
        // dont l'intérêt est justement de se relire. Il n'y a ici aucun rendu HTML à
        // protéger — le JSON part dans un fichier — donc on écrit les caractères tels quels.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ToJson(Document document) => JsonSerializer.Serialize(document, Options);

    /// <summary>
    /// Relit un document, de n'importe quelle version du format jusqu'à la courante. On
    /// passe par l'arbre JSON plutôt que par le modèle : une propriété renommée n'a plus
    /// de propriété où atterrir, la migration doit donc se faire avant.
    /// </summary>
    public static Document FromJson(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject root)
        {
            throw new InvalidDataException("Le document n'est pas un objet JSON.");
        }

        FormatMigrations.Apply(root);
        return JsonSerializer.Deserialize<Document>(root, Options) ?? Document.CreateDefault();
    }

    public static void Save(Document document, string path) =>
        File.WriteAllText(path, ToJson(document));

    public static Document Load(string path) => FromJson(File.ReadAllText(path));
}
