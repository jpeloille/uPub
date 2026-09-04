using System.IO;
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
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string ToJson(Document document) => JsonSerializer.Serialize(document, Options);

    public static Document FromJson(string json) =>
        JsonSerializer.Deserialize<Document>(json, Options) ?? Document.CreateDefault();

    public static void Save(Document document, string path) =>
        File.WriteAllText(path, ToJson(document));

    public static Document Load(string path) => FromJson(File.ReadAllText(path));
}
