using System;
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
///
/// Les chemins d'images s'écrivent en relatif quand le fichier se trouve sous le
/// dossier du document, en absolu sinon — voir <see cref="Relativiser"/>. Le dossier
/// de référence est donc un paramètre de l'écriture comme de la lecture : sans lui,
/// une image ne saurait ni s'écrire ni se retrouver. Il est facultatif, et son absence
/// a un sens précis : les chemins passent alors tels quels, ni relativisés ni résolus.
/// C'est ce qui rend l'aller-retour de l'historique d'annulation neutre.
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

    /// <summary>
    /// Écrit le document. <paramref name="dossier"/> est celui où le fichier va être
    /// rangé : les images qui s'y trouvent y sont référencées en relatif. Null signifie
    /// « aucun dossier connu » — les chemins sortent tels qu'ils sont en mémoire.
    ///
    /// On passe par l'arbre JSON plutôt que par le modèle : réécrire un chemin sur le
    /// bloc lui-même notifierait la liaison, ferait relire l'image à l'écran et, pire,
    /// laisserait le document modifié si l'écriture échouait ensuite. L'arbre est une
    /// copie ; le modèle ne bouge pas. Vérifié : à dossier absent, il rend exactement le
    /// même texte que la sérialisation directe, dérives d'arrondi comprises.
    /// </summary>
    public static string ToJson(Document document, string? dossier = null)
    {
        var arbre = JsonSerializer.SerializeToNode(document, Options)!;

        if (Racine(dossier) is { } racine)
        {
            ReecrireChemins(arbre, chemin => Relativiser(chemin, racine));
        }

        return arbre.ToJsonString(Options);
    }

    /// <summary>
    /// Relit un document, de n'importe quelle version du format jusqu'à la courante. On
    /// passe par l'arbre JSON plutôt que par le modèle : une propriété renommée n'a plus
    /// de propriété où atterrir, la migration doit donc se faire avant.
    ///
    /// <paramref name="dossier"/> est celui d'où vient le fichier : les chemins relatifs
    /// s'y résolvent, et le modèle ne porte plus ensuite que des chemins absolus. Rien
    /// en aval — la liaison de l'écran, l'export, le cache d'images — n'a donc à savoir
    /// qu'un chemin relatif existe.
    /// </summary>
    public static Document FromJson(string json, string? dossier = null)
    {
        if (JsonNode.Parse(json) is not JsonObject root)
        {
            throw new InvalidDataException("Le document n'est pas un objet JSON.");
        }

        FormatMigrations.Apply(root);

        // Après la migration : elle travaille sur la forme qu'avait le fichier, et un
        // chemin résolu d'avance lui serait passé sous une forme qu'elle n'a jamais vue.
        if (Racine(dossier) is { } racine)
        {
            ReecrireChemins(root, chemin => Resoudre(chemin, racine));
        }

        return JsonSerializer.Deserialize<Document>(root, Options) ?? Document.CreateDefault();
    }

    /// <summary>
    /// Enregistre le document. Le dossier de référence des chemins d'images est celui du
    /// fichier écrit — c'est la seule règle qui rende le fichier descriptible tout seul.
    /// <paramref name="dossier"/> permet d'en désigner un autre ; un seul appelant s'en
    /// sert, la réécriture sans interface, et il dit pourquoi.
    ///
    /// <b>C'est le seul endroit du dépôt qui écrive un .mypub sur le disque</b>, et c'est
    /// ce qui rend sans danger le dossier facultatif de <see cref="ToJson"/> : les deux
    /// appels qui n'en passent pas — l'instantané et la comparaison de l'historique
    /// d'annulation — ne touchent aucun fichier. Un second écrivain, l'archive du poste
    /// B4 par exemple, devra passer le sien, sans quoi il graverait des chemins absolus
    /// sans que rien n'échoue.
    /// </summary>
    public static void Save(Document document, string path, string? dossier = null) =>
        File.WriteAllText(path, ToJson(document, dossier ?? Path.GetDirectoryName(Path.GetFullPath(path))));

    /// <summary>
    /// Ouvre un document. Un fichier <c>.pub</c> est importé plutôt que lu : c'est le
    /// même geste pour qui ouvre, et l'importation dit ce qu'elle n'a pas su rendre.
    ///
    /// L'aiguillage est ici plutôt que chez l'appelant pour que tout le monde en
    /// profite d'un coup — la fenêtre, le mode sans interface et le banc.
    /// </summary>
    public static Document Load(string path) =>
        path.EndsWith(".pub", StringComparison.OrdinalIgnoreCase)
            ? Import(path).Document
            : FromJson(File.ReadAllText(path), Path.GetDirectoryName(Path.GetFullPath(path)));

    /// <summary>
    /// Importe un <c>.pub</c>, avec le relevé de ce qui s'est perdu en route.
    ///
    /// Le dépôt d'images est fourni ici et non par l'appelant : c'est le seul endroit qui
    /// sache qu'on lit un vrai fichier, et <see cref="PubImporter"/> doit rester pur.
    /// Le paramètre est ouvert pour que les tests et la recette puissent ranger ailleurs.
    /// </summary>
    public static ImportReport Import(string path, IDepotImages? depot = null) =>
        PubImporter.Importer(
            PubReader.Relever(path), Path.GetFileNameWithoutExtension(path), depot ?? new DepotImages());

    // ------------------------------------------------------------- chemins d'images

    /// <summary>Le dossier de référence, normalisé ; null quand il n'y en a pas.</summary>
    private static string? Racine(string? dossier) =>
        string.IsNullOrWhiteSpace(dossier) ? null : Path.GetFullPath(dossier);

    /// <summary>
    /// Le chemin tel qu'il s'écrit dans un document rangé dans <paramref name="racine"/> :
    /// relatif si le fichier est <b>sous</b> ce dossier, inchangé sinon.
    ///
    /// Le « sous » se mesure par <c>GetRelativePath</c> et non par un préfixe de chaîne :
    /// le dossier <c>/travail/notice</c> et l'image <c>/travail/notice-images/x.png</c>
    /// partagent le préfixe sans que la seconde soit dans le premier. Un résultat qui
    /// remonte par <c>..</c> est refusé pour la même raison qui fait tout ce poste : il
    /// se casserait au premier déplacement du dossier.
    /// </summary>
    private static string Relativiser(string chemin, string racine)
    {
        if (!Path.IsPathRooted(chemin)) return chemin;

        var relatif = Path.GetRelativePath(racine, Path.GetFullPath(chemin));

        return Path.IsPathRooted(relatif)
               || relatif == ".."
               || relatif.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? chemin
            : relatif;
    }

    /// <summary>
    /// Le chemin d'un fichier écrit dans <paramref name="racine"/>, ramené à l'absolu.
    /// Un chemin déjà absolu n'est pas normalisé : il ressortira à l'écriture tel qu'il
    /// est entré, et l'aller-retour reste identique au caractère près.
    /// </summary>
    private static string Resoudre(string chemin, string racine) =>
        Path.IsPathRooted(chemin) ? chemin : Path.GetFullPath(Path.Combine(racine, chemin));

    /// <summary>
    /// Applique une réécriture au chemin de chaque bloc image de l'arbre.
    ///
    /// La descente est générique — tout objet, où qu'il soit — plutôt que dirigée vers
    /// <c>Pages[].Frames[]</c> et <c>Children[]</c> : un bloc image logé dans un type
    /// composé qui n'existe pas encore serait sinon oublié en silence, ce qu'aucun test
    /// d'aujourd'hui ne pourrait voir.
    /// </summary>
    private static void ReecrireChemins(JsonNode? noeud, Func<string, string> reecriture)
    {
        switch (noeud)
        {
            case JsonObject objet:
                // Un bloc image sans chemin est un emplacement qui attend son image —
                // c'est ainsi que les quatre modèles livrés réservent leur place. Il n'y
                // a rien à relativiser, et « GetFullPath("") » lève.
                if (objet["$kind"] is JsonValue genre
                    && genre.TryGetValue<string>(out var kind) && kind == "image"
                    && objet["Path"] is JsonValue valeur
                    && valeur.TryGetValue<string>(out var chemin)
                    && !string.IsNullOrWhiteSpace(chemin))
                {
                    objet["Path"] = reecriture(chemin);
                }

                foreach (var (_, enfant) in objet) ReecrireChemins(enfant, reecriture);
                break;

            case JsonArray tableau:
                foreach (var element in tableau) ReecrireChemins(element, reecriture);
                break;
        }
    }
}
