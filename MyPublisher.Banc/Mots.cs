using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.Views;

namespace MyPublisher.Banc;

/// <summary>
/// Les lignes que l'écran dessine, mot par mot, en coordonnées de page — ce que
/// <c>Scripts/comparer-mots.sh</c> confronte aux mots du PDF (poste Q13).
///
/// Elles sont lues sur les vrais contrôles de la vraie fenêtre, par
/// <see cref="TexteCompose.Poser"/>, la fonction même que le rendu appelle : le banc
/// ne recompose rien à côté.
/// </summary>
internal static class Mots
{
    /// <summary>
    /// Les ordres de chargement des faces, chacun choisi pour un état de la résolution
    /// d'Avalonia d'avant Q13. Sondé le 12 octobre 2026 sur DejaVu Serif, qui n'a pas
    /// d'italique : l'italique demandé sort en GRAS penché quand le gras est la seule
    /// face de la famille déjà chargée, en romain penché dès que le romain l'est, ou
    /// que rien ne l'est. Charger « gras, gras italique, romain, italique » donne donc
    /// le romain : il faut demander l'italique entre le gras et le romain.
    ///
    /// « romain » demande l'italique après le seul romain, « gras » après le seul gras,
    /// « italique » avant toute autre face.
    /// </summary>
    private static readonly Dictionary<string, (bool Gras, bool Italique)[]> Ordres = new()
    {
        ["romain"] = [(false, false), (false, true), (true, false), (true, true)],
        ["gras"] = [(true, false), (false, true), (true, true), (false, false)],
        ["italique"] = [(false, true), (true, true), (true, false), (false, false)],
    };

    private sealed record Mot(string Texte, double X, double Y, double Largeur);

    private sealed record Ligne(
        string Texte, double Haut, double Bas, double Gauche, double Droite, List<Mot> Mots);

    /// <summary>Un bloc relevé, avec le rectangle qu'il couvre sur la page — tourné compris.</summary>
    private sealed record Bloc(
        string Nom, int Page, bool Compare, string? Motif, List<Ligne> Lignes,
        double X, double Y, double Largeur, double Hauteur);

    public static int Lancer(string[] args)
    {
        var chemin = args.Length > 1 && !args[1].StartsWith('-') ? args[1] : null;
        var sortie = Option(args, "--sortie");
        if (chemin is null || sortie is null)
        {
            Console.Error.WriteLine(
                "Usage : MyPublisher.Banc mots <doc> --sortie <lignes.json> [--charger-d-abord romain|gras|italique] [--coupure-seule]");
            return 2;
        }

        var ordre = Option(args, "--charger-d-abord")
                    ?? Environment.GetEnvironmentVariable("MYPUBLISHER_CHARGER_D_ABORD");
        var coupureSeule = args.Contains("--coupure-seule")
                           || Environment.GetEnvironmentVariable("MYPUBLISHER_COUPURE_SEULE") == "1";

        Banc.Configurer().SetupWithoutStarting();

        if (!string.IsNullOrEmpty(ordre)) Charger(DocumentSerializer.Load(chemin), ordre);

        var (fenetre, vm) = Banc.Ouvrir(chemin);
        vm.ShowGuides = false;
        vm.Selection.Clear();

        var metriques = new MetriquesAvalonia();
        Compositeur.IMetriques composition = coupureSeule ? new Seule(metriques) : metriques;

        var blocs = new List<Bloc>();
        for (var page = 1; page <= vm.Document.Pages.Count; page++)
        {
            vm.CurrentPage = vm.Document.Pages[page - 1];
            Banc.Battre();
            var planche = Banc.Page(fenetre);

            foreach (var controle in planche.GetVisualDescendants().OfType<TexteCompose>())
            {
                if (controle.Bloc is not { } bloc || !controle.IsEffectivelyVisible) continue;

                var nom = bloc.DisplayName;
                var repere = controle.TransformToVisual(planche);
                var cadre = new Rect(controle.Bounds.Size).TransformToAABB(repere ?? Matrix.Identity);
                if (repere is not { } m)
                {
                    blocs.Add(new Bloc(nom, page, false, "hors de la planche", [], 0, 0, 0, 0));
                    continue;
                }

                // Un bloc tourné n'a pas de lignes horizontales : pdftotext les lit
                // autrement, et la confrontation ne saurait pas les apparier. On le dit
                // plutôt que de le comparer de travers.
                if (Math.Abs(m.M12) > 1e-9 || Math.Abs(m.M21) > 1e-9
                    || Math.Abs(m.M11 - 1) > 1e-9 || Math.Abs(m.M22 - 1) > 1e-9)
                {
                    blocs.Add(new Bloc(nom, page, false, "tourné", [], cadre.X, cadre.Y, cadre.Width, cadre.Height));
                    continue;
                }

                var lignes = controle.Poser(metriques, composition)
                    .GroupBy(p => p.Ligne)
                    .OrderBy(g => g.Key)
                    .Select(g =>
                    {
                        var premier = g.First();
                        var haut = premier.HautDeLigne + m.M32;
                        var gauche = premier.Gauche + m.M31;
                        return new Ligne(
                            string.Join(" ", string.Concat(g.Select(p => p.Texte))
                                .Split(' ', StringSplitOptions.RemoveEmptyEntries)),
                            haut, haut + premier.Avance, gauche, gauche + premier.Disponible,
                            Assembler(g, m.M31, m.M32));
                    })
                    .ToList();

                blocs.Add(new Bloc(nom, page, true, null, lignes, cadre.X, cadre.Y, cadre.Width, cadre.Height));
            }
        }

        // En clair : les noms de blocs portent des accents, et ce relevé se lit aussi à l'œil.
        File.WriteAllText(sortie, JsonSerializer.Serialize(blocs,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine(
            $"{blocs.Count(b => b.Compare)} bloc(s) relevé(s), {blocs.Sum(b => b.Lignes.Count)} ligne(s), " +
            $"{blocs.Count(b => !b.Compare)} écarté(s), dans {sortie}" +
            (string.IsNullOrEmpty(ordre) ? "" : $" (faces chargées d'abord : {ordre})") +
            (coupureSeule ? " — CONTRE-ÉPREUVE : l'écran coupe à sa seule mesure" : ""));
        return 0;
    }

    /// <summary>
    /// Les mots d'une ligne, tels qu'on les lit : un morceau qui touche le précédent
    /// sans blanc entre eux — un mot que deux styles se partagent — s'y recolle ; une
    /// tabulation, qui ne se dessine pas mais avance, sépare.
    /// </summary>
    private static List<Mot> Assembler(IEnumerable<TexteCompose.MorceauPose> poses, double dx, double dy)
    {
        var mots = new List<Mot>();
        TexteCompose.MorceauPose? precedent = null;
        foreach (var pose in poses)
        {
            var colle = precedent is { } p
                        && p.Texte.Length > 0 && !char.IsWhiteSpace(p.Texte[^1])
                        && Math.Abs(p.Ancre.X + p.Rendu.WidthIncludingTrailingWhitespace - pose.Ancre.X) < 0.01;
            var texte = pose.Texte.Trim();
            var largeur = pose.Rendu.Width;
            if (colle && mots.Count > 0)
            {
                var dernier = mots[^1];
                mots[^1] = dernier with
                {
                    Texte = dernier.Texte + texte,
                    Largeur = pose.Ancre.X + dx + largeur - dernier.X,
                };
            }
            else
            {
                mots.Add(new Mot(texte, pose.Ancre.X + dx, pose.Ancre.Y + dy, largeur));
            }

            precedent = pose;
        }

        return mots;
    }

    /// <summary>
    /// Charge les faces de chaque famille du document dans l'ordre demandé, avant que
    /// la fenêtre ne s'ouvre : par la résolution d'Avalonia — ce que ferait le chrome,
    /// ou tout contrôle qui affiche du texte dans cette famille — puis par la règle à
    /// mesurer. Le résultat ne doit pas en dépendre.
    /// </summary>
    private static void Charger(Document document, string ordre)
    {
        if (!Ordres.TryGetValue(ordre, out var faces))
            throw new ArgumentException($"Ordre inconnu : {ordre} (romain, gras, italique).");

        var familles = document.Pages
            .SelectMany(p => Tous(p.Frames))
            .OfType<TextFrame>()
            .SelectMany(t => t.Spans.Select(s => s.FontFamily).Prepend(t.FontFamily))
            .OfType<string>()
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var regle = new MetriquesAvalonia();
        foreach (var famille in familles)
        {
            foreach (var (gras, italique) in faces)
            {
                _ = new Typeface(new FontFamily(famille),
                    italique ? FontStyle.Italic : FontStyle.Normal,
                    gras ? FontWeight.Bold : FontWeight.Normal).GlyphTypeface;
                regle.Largeur("Hamburgefonstiv", new StyleEffectif(famille, 10, gras, italique, Souligne.Aucun, "#FF000000", 0));
            }
        }
    }

    private static IEnumerable<Frame> Tous(IEnumerable<Frame> blocs) =>
        blocs.SelectMany(b => b switch
        {
            GroupFrame g => Tous(g.Children).Prepend(b),
            TableFrame t => Tous(t.CellFrames).Prepend(b),
            _ => [b],
        });

    /// <summary>
    /// La règle de l'écran qui décide seule de sa coupure — ce que faisait le dépôt
    /// avant Q13. Elle ne sert qu'à la contre-épreuve : comparer-mots.sh doit la voir.
    /// </summary>
    private sealed class Seule(MetriquesAvalonia regle) : Compositeur.IMetriques
    {
        public double Largeur(string texte, StyleEffectif style) => regle.Largeur(texte, style);
        public double LargeurDeCoupure(string texte, StyleEffectif style) => regle.Largeur(texte, style);
        public double Naturelle(StyleEffectif style) => regle.Naturelle(style);
    }

    private static string? Option(string[] args, string nom)
    {
        var i = Array.IndexOf(args, nom);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
