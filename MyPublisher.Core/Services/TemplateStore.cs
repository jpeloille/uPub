using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MyPublisher.Models;

namespace MyPublisher.Services;

/// <summary>
/// Un modèle et de quoi le présenter. Le document est celui qu'on lit pour dessiner la
/// vignette ; c'est le chemin, et non lui, qui sert à démarrer — on relit le fichier au
/// moment de créer, pour que deux documents partis du même modèle ne partagent rien.
/// </summary>
public sealed record TemplateChoice(string Name, string Summary, Document? Document, string? Path)
{
    /// <summary>L'entrée « page blanche », toujours en tête du catalogue.</summary>
    public static TemplateChoice Blank { get; } =
        new("Document vierge", "A4 portrait, une page, sans contenu", null, null);

    public bool IsBlank => Path is null;
}

/// <summary>
/// Le catalogue de modèles. Un modèle n'est rien d'autre qu'un document : les fichiers
/// sont des « .mypub » ordinaires, lus par le même code que les documents de
/// l'utilisateur. Rien n'est à ajouter au format.
/// </summary>
public static class TemplateStore
{
    /// <summary>Les modèles livrés avec l'application, à côté du binaire.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Templates");

    /// <summary>
    /// Le catalogue, page blanche en tête. Un modèle illisible est passé sous silence
    /// plutôt que de faire échouer l'écran de choix : mieux vaut un catalogue amputé
    /// qu'une application qui ne démarre pas.
    /// </summary>
    public static IReadOnlyList<TemplateChoice> Load(string? folder = null)
    {
        var choices = new List<TemplateChoice> { TemplateChoice.Blank };
        folder ??= Folder;

        if (!Directory.Exists(folder)) return choices;

        foreach (var file in Directory.EnumerateFiles(folder, "*." + DocumentSerializer.Extension)
                     .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
        {
            try
            {
                var document = DocumentSerializer.Load(file);
                choices.Add(new TemplateChoice(
                    string.IsNullOrWhiteSpace(document.Title) ? Path.GetFileNameWithoutExtension(file) : document.Title,
                    Describe(document), document, file));
            }
            catch (Exception)
            {
                // Modèle abîmé : on l'ignore.
            }
        }

        return choices;
    }

    /// <summary>Ce qu'on lit sous le nom : format, sens, colonnes, nombre de pages.</summary>
    public static string Describe(Document document)
    {
        var format = PageSize.Match(document.PageWidth, document.PageHeight)?.Name
                     ?? $"{Units.PointsToMm(document.PageWidth):0} × {Units.PointsToMm(document.PageHeight):0} mm";

        var parts = new List<string>
        {
            $"{format} {(document.PageWidth > document.PageHeight ? "paysage" : "portrait")}",
            document.Pages.Count == 1 ? "une page" : $"{document.Pages.Count} pages"
        };

        if (document.ColumnCount > 1)
        {
            parts.Add($"{document.ColumnCount.ToString(CultureInfo.CurrentCulture)} colonnes");
        }

        return string.Join(" · ", parts);
    }
}
