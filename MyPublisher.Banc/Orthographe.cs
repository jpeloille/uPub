using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;

namespace MyPublisher.Banc;

/// <summary>
/// Le relevé des fautes d'un document (poste N25) : chaque bloc de texte — ceux des groupes
/// compris — et chaque case de tableau, page après page, passés au correcteur du noyau, chaque mot
/// dans sa langue (N9), ou tous dans celle que <c>--langue</c> impose.
///
///   MyPublisher.Banc orthographe &lt;doc&gt; [--langue en-US] [--dictionnaires &lt;dossier&gt;]
///
/// Le relevé se fait trois fois : sur un correcteur neuf, de nouveau sur le même — chaque mot lu
/// dans son cache —, puis sur un second correcteur neuf. Les trois doivent rendre les mêmes
/// fautes, aux mêmes places : c'est ce que la recette appelle « stable ». Sans dictionnaire pour une
/// langue, le relevé le dit et sort en 3 : il ne conclut rien d'un texte qu'il n'a pas lu.
/// </summary>
internal static class VerificationOrthographique
{
    /// <summary>Une faute relevée : où elle est, et le mot.</summary>
    private sealed record Relevee(int Page, string Texte, int Debut, string Mot, string Langue);

    public static int Lancer(string[] args)
    {
        var chemin = args.Length > 1 && !args[1].StartsWith('-') ? args[1] : null;
        if (chemin is null)
        {
            Console.Error.WriteLine("Usage : MyPublisher.Banc orthographe <doc> [--langue en-US] [--dictionnaires <dossier>]");
            return 2;
        }

        var langue = Option(args, "--langue");
        var dossier = Option(args, "--dictionnaires") ?? Services.Orthographe.DossierDuSysteme;
        var document = DocumentSerializer.Load(chemin);

        var premier = new Services.Orthographe(dossier);
        var absentes = new List<string>();
        premier.DictionnaireAbsent += absentes.Add;

        var froid = Relever(document, premier, langue, out var mots);
        if (absentes.Count > 0)
        {
            Console.WriteLine($"dictionnaire absent : {string.Join(", ", absentes)} (dans {dossier})");
            return 3;
        }

        var jugesAuFroid = premier.MotsJuges;
        var chaud = Relever(document, premier, langue, out _);
        var neuf = Relever(document, new Services.Orthographe(dossier), langue, out _);
        var stable = froid.SequenceEqual(chaud) && froid.SequenceEqual(neuf);

        Console.WriteLine($"mots : {mots}");
        Console.WriteLine($"fautes : {froid.Count}");
        Console.WriteLine($"distinctes : {froid.Select(f => f.Mot).Distinct(StringComparer.Ordinal).Count()}");
        Console.WriteLine("langues : " + string.Join(", ", froid.GroupBy(f => f.Langue).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key} {g.Count()}")));
        Console.WriteLine($"juges : {jugesAuFroid} au premier relevé, {premier.MotsJuges - jugesAuFroid} au second");
        Console.WriteLine($"stable : {(stable ? "oui" : "non")}");
        Console.WriteLine("premieres : " + string.Join(", ", froid.Select(f => f.Mot).Distinct(StringComparer.Ordinal).Take(12)));
        return stable ? 0 : 1;
    }

    /// <summary>Les fautes du document, dans l'ordre des pages et des blocs, et le nombre de mots lus.</summary>
    private static List<Relevee> Relever(Document document, Services.Orthographe correcteur, string? langue, out int mots)
    {
        var relevees = new List<Relevee>();
        mots = 0;
        for (var page = 0; page < document.Pages.Count; page++)
        {
            var numero = 0;
            foreach (var (nom, texte, langueDe) in Textes(document, document.Pages[page].Frames))
            {
                numero++;
                mots += Services.Orthographe.Mots(texte).Count();
                Func<int, string> laLangue = langue is null ? langueDe : _ => langue;
                relevees.AddRange(correcteur.Fautes(texte, laLangue)
                    .Select(f => new Relevee(page + 1, $"{numero}:{nom}", f.Debut, f.Mot, f.Langue)));
            }
        }

        return relevees;
    }

    /// <summary>Les textes d'une liste de blocs : les blocs de texte sans champ, les groupes, les cases.</summary>
    private static IEnumerable<(string Nom, string Texte, Func<int, string> Langue)> Textes(Document document, IEnumerable<Frame> blocs)
    {
        foreach (var bloc in blocs)
        {
            switch (bloc)
            {
                case TextFrame { HasField: false } texte:
                    yield return (texte.Name, texte.Text ?? "", p => Langues.DuCaractere(texte, p, document));
                    break;
                case TableFrame tableau:
                    foreach (var cellule in tableau.Cells.OrderBy(c => c.Row).ThenBy(c => c.Column))
                    {
                        yield return ($"{tableau.Name}/{cellule.Label}", cellule.Text ?? "",
                            p => Langues.DuCaractere(cellule, p, document));
                    }

                    break;
                case GroupFrame groupe:
                    foreach (var membre in Textes(document, groupe.Children)) yield return membre;
                    break;
            }
        }
    }

    private static string? Option(string[] args, string nom)
    {
        var i = Array.IndexOf(args, nom);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
