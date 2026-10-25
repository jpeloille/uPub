using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;

namespace MyPublisher.Banc;

/// <summary>
/// Ce que le dock annonce de chaque bloc de texte : l'alerte « Texte en excédent : n
/// lignes » du poste I9, lue sur la vraie fenêtre, bloc sélectionné après bloc
/// sélectionné, comme on le ferait à la souris. C'est ce que la recette I9 confronte aux
/// lignes absentes du PDF.
///
/// Le banc ne compte rien lui-même : il lit le texte de l'alerte affichée, ou son
/// absence. Un compte juste qui ne s'afficherait pas, ou une alerte restée d'un bloc
/// précédent, s'y verraient.
/// </summary>
internal static class Excedent
{
    /// <summary>Un bloc de texte relevé : où il est, ce qu'il porte, ce que le dock en dit.</summary>
    private sealed record Bloc(
        int Page, string Nom, double X, double Y, double Largeur, double Hauteur, double Rotation,
        double MargeHaute, double MargeBasse, string Contenu, string? Alerte);

    public static int Lancer(string[] args)
    {
        var chemin = args.Length > 1 && !args[1].StartsWith('-') ? args[1] : null;
        var i = Array.IndexOf(args, "--sortie");
        var sortie = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        if (chemin is null || sortie is null)
        {
            Console.Error.WriteLine("Usage : MyPublisher.Banc excedent <doc.mypub> --sortie <blocs.json>");
            return 2;
        }

        Banc.Configurer().SetupWithoutStarting();
        var (fenetre, vm) = Banc.Ouvrir(chemin);

        var blocs = new List<Bloc>();
        for (var page = 1; page <= vm.Document.Pages.Count; page++)
        {
            vm.CurrentPage = vm.Document.Pages[page - 1];
            Banc.Battre();

            foreach (var bloc in vm.CurrentPage.Frames.OfType<TextFrame>().ToList())
            {
                vm.SelectOnly(bloc);
                Banc.Battre();

                var alertes = fenetre.GetVisualDescendants().OfType<Border>()
                    .Where(b => b.Name == "AlerteDExcedent" && b.IsEffectivelyVisible)
                    .ToList();
                if (alertes.Count > 1)
                    throw new InvalidOperationException($"{alertes.Count} alertes d'excédent affichées pour « {bloc.DisplayName} ».");

                var alerte = alertes.Count == 0
                    ? null
                    : string.Join(" ", alertes[0].GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));

                blocs.Add(new Bloc(
                    page, bloc.DisplayName, bloc.X, bloc.Y, bloc.Width, bloc.Height, bloc.Rotation,
                    bloc.PaddingTop, bloc.PaddingBottom,
                    FieldResolver.Resolve(bloc.Field, bloc.Text, vm.FieldContext), alerte));
            }
        }

        File.WriteAllText(sortie, JsonSerializer.Serialize(blocs,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine(
            $"{blocs.Count} bloc(s) de texte relevé(s), {blocs.Count(b => b.Alerte is not null)} en excédent, dans {sortie}");
        return 0;
    }
}
