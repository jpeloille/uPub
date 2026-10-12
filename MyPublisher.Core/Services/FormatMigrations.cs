using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        if (version < 4) ToVersion4(root);
        if (version < 5) ToVersion5(root);

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

    /// <summary>
    /// Version 4 : le remplissage n'est plus une couleur mais un objet discriminé, pour
    /// que le fichier sache dire un dégradé, une image ou une teinte. Une couleur ancienne
    /// devient un remplissage uni de cette couleur : le document s'affiche comme avant.
    ///
    /// La marche descend dans les groupes ET dans les cases des tableaux. Les cases ne
    /// sont pas des blocs — elles n'ont pas de <c>$kind</c> — et un parcours qui ne
    /// suivrait que les blocs laisserait derrière lui des fonds de case en chaîne, que le
    /// modèle refuserait de relire. C'est le premier poste où la distinction compte.
    /// </summary>
    private static void ToVersion4(JsonObject root)
    {
        foreach (var frame in AllFrames(root))
        {
            Envelopper(frame, "Fill");

            if (frame["Cells"] is not JsonArray cells) continue;
            foreach (var cell in cells.OfType<JsonObject>()) Envelopper(cell, "Fill");
        }
    }

    /// <summary>
    /// Remplace une couleur par le remplissage uni équivalent. Une valeur nulle ou déjà
    /// enveloppée est laissée telle quelle : la migration doit pouvoir se rejouer sans
    /// rien casser, c'est ce que le contrôle P2 exige en la demandant en point fixe.
    /// </summary>
    private static void Envelopper(JsonObject porteur, string nom)
    {
        if (porteur[nom] is not JsonValue valeur) return;
        if (valeur.GetValueKind() != JsonValueKind.String) return;

        porteur[nom] = new JsonObject
        {
            ["$kind"] = "solid",
            ["Color"] = valeur.GetValue<string>(),
        };
    }

    /// <summary>Les propriétés qui portent une couleur ou une police, où qu'elles soient.</summary>
    private static readonly HashSet<string> CouleursEtPolices =
        ["Color", "From", "To", "Stroke", "Foreground", "FontFamily"];

    /// <summary>
    /// Version 5 : le document porte un jeu de couleurs et un jeu de polices, et une
    /// couleur ou une police peut y faire référence — <c>scheme:accent1</c>,
    /// <c>scheme:heading</c> (poste C5b).
    ///
    /// Un document de la version 4 n'a aucune référence, et doit s'ouvrir à l'identique :
    /// ses couleurs et ses polices restent libres, toutes. La migration ne tente donc
    /// AUCUN rapprochement — une couleur saisie qui se trouverait égale à un accent du
    /// jeu par défaut ne devient pas cet accent : elle changerait de couleur au premier
    /// changement de jeu, sans que personne l'ait demandé. Les jeux eux-mêmes ne sont pas
    /// écrits ici : un fichier qui n'en a pas reçoit ceux du document neuf, qu'aucun de
    /// ses blocs ne consulte.
    ///
    /// Ce qu'elle fait, c'est garder libre une valeur que la version 4 tenait pour libre
    /// et que la version 5 lirait comme une référence : une chaîne qui commencerait par
    /// <c>scheme:</c>. Rien dans l'interface n'en écrivait, mais un fichier écrit à la
    /// main ou un import le pouvaient ; elle reçoit un préfixe
    /// (<see cref="ReferenceDuJeu.Liberer"/>) qui n'en change pas le rendu — une telle
    /// couleur était illisible et le reste, une telle police introuvable et le reste.
    ///
    /// La descente est générique, comme celle des chemins d'images : les couleurs vivent
    /// dans les remplissages, les fragments de style, les cases, le fond de page, et
    /// une marche dirigée en oublierait une le jour où un type composé apparaît.
    /// </summary>
    private static void ToVersion5(JsonNode? noeud)
    {
        switch (noeud)
        {
            case JsonObject objet:
                foreach (var (nom, valeur) in objet.ToList())
                {
                    if (CouleursEtPolices.Contains(nom)
                        && valeur is JsonValue texte
                        && texte.TryGetValue<string>(out var chaine)
                        && ReferenceDuJeu.EstReference(chaine))
                    {
                        objet[nom] = ReferenceDuJeu.Liberer(chaine);
                        continue;
                    }

                    ToVersion5(valeur);
                }

                break;

            case JsonArray tableau:
                foreach (var element in tableau) ToVersion5(element);
                break;
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
