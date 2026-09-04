using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
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

    public static Document FromJson(string json) =>
        JsonSerializer.Deserialize<Document>(json, Options) ?? Document.CreateDefault();

    public static void Save(Document document, string path) =>
        File.WriteAllText(path, ToJson(document));

    public static Document Load(string path) => FromJson(File.ReadAllText(path));
}
