using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using MyPublisher.Models;

namespace MyPublisher.Services;

/// <summary>
/// Met un document lu dans une version antérieure du format au niveau de la version
/// courante, en place, sur le JSON brut — avant que le modèle ne le voie. Chaque
/// version a son étape, jouée dans l'ordre : un fichier de la version 1 traverse
/// toutes les étapes, un fichier de la version courante n'en traverse aucune.
/// </summary>
public static class FormatMigrations
{
    public static void Apply(JsonObject root)
    {
        // Un fichier sans numéro date d'avant la numérotation : c'est la version 1.
        var version = root["FormatVersion"]?.GetValue<int>() ?? 1;

        if (version > Document.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Ce document est au format {version} ; cette version de MyPublisher lit jusqu'au format {Document.CurrentFormatVersion}.");
        }

        if (version < 2) ToVersion2(root);
        if (version < 3) ToVersion3(root);

        root["FormatVersion"] = Document.CurrentFormatVersion;
    }

    /// <summary>
    /// Version 2 : le fond du bloc de texte s'appelle « Fill », comme celui de la forme,
    /// depuis que fond et contour sont communs à tous les blocs (décision P0 du plan).
    /// Les autres propriétés nouvelles — contour, épaisseur, trait, arrondi sur les
    /// textes et les images — prennent leurs valeurs par défaut, qui signifient
    /// « rien » : un fichier migré s'affiche comme avant.
    /// </summary>
    private static void ToVersion2(JsonObject root)
    {
        foreach (var frame in AllFrames(root))
        {
            if (frame["$kind"]?.GetValue<string>() != "text") continue;
            if (!frame.ContainsKey("Background")) continue;

            var background = frame["Background"];
            frame.Remove("Background");
            frame["Fill"] = background;
        }
    }

    /// <summary>
    /// Version 3 : la marge intérieure unique du bloc de texte devient quatre marges,
    /// une par côté. Un fichier ancien les reçoit toutes égales à l'ancienne valeur : il
    /// s'affiche comme avant.
    /// </summary>
    private static void ToVersion3(JsonObject root)
    {
        foreach (var frame in AllFrames(root))
        {
            if (frame["$kind"]?.GetValue<string>() != "text") continue;
            if (!frame.ContainsKey("Padding")) continue;

            var padding = frame["Padding"]?.GetValue<double>() ?? 0;
            frame.Remove("Padding");
            frame["PaddingLeft"] = padding;
            frame["PaddingTop"] = padding;
            frame["PaddingRight"] = padding;
            frame["PaddingBottom"] = padding;
        }
    }

    /// <summary>Tous les blocs du document, ceux des groupes compris, à toute profondeur.</summary>
    private static IEnumerable<JsonObject> AllFrames(JsonObject root)
    {
        if (root["Pages"] is not JsonArray pages) yield break;

        foreach (var page in pages)
        {
            if (page?["Frames"] is not JsonArray frames) continue;
            foreach (var frame in Descend(frames)) yield return frame;
        }
    }

    private static IEnumerable<JsonObject> Descend(JsonArray frames)
    {
        foreach (var node in frames)
        {
            if (node is not JsonObject frame) continue;
            yield return frame;

            if (frame["Children"] is JsonArray children)
            {
                foreach (var child in Descend(children)) yield return child;
            }
        }
    }
}
