using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MyPublisher.Models;

// Le banc d'interface atteint le cache d'images et son lecteur, pour éprouver l'ordre
// de l'empreinte et de la lecture (poste Q15).
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MyPublisher.Tests.Interface")]

namespace MyPublisher.Converters;

/// <summary>
/// Convertisseurs de liaison. Ils existent pour que le modèle reste indépendant
/// d'Avalonia : les couleurs y sont des chaînes, les alignements des énumérations
/// maison, et la traduction vers les types Avalonia se fait ici, dans la vue.
/// </summary>
public static class Conv
{
    public static readonly FuncValueConverter<string?, IBrush?> Brush =
        new(ToBrush);

    /// <summary>
    /// Une couleur du modèle en pinceau Avalonia ; null pour une couleur absente ou
    /// illisible. Publique parce que la vignette des modèles la dessine en code, sans
    /// passer par une liaison.
    /// </summary>
    public static IBrush? ToBrush(string? value) =>
        ToColor(value) is { } couleur ? new SolidColorBrush(couleur) : null;

    /// <summary>
    /// Une couleur du modèle en couleur Avalonia ; null pour une couleur absente ou
    /// illisible. Publique parce que le fond d'un bloc en a besoin telle quelle, pour
    /// poser les deux extrémités d'un dégradé.
    /// </summary>
    public static Color? ToColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return HexColor.TryParse(value, out var a, out var r, out var g, out var b)
            ? Color.FromArgb(a, r, g, b)
            : null;
    }

    /// <summary>
    /// L'image d'un chemin, par le cache ci-dessous. Publique pour le fond d'un bloc,
    /// qui la dessine en code plutôt que par une liaison — et qui doit passer par le
    /// même cache, sans quoi faire glisser un bloc au fond illustré relirait le fichier
    /// à chaque image de l'animation.
    /// </summary>
    public static Bitmap? Bitmap(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : BitmapCache.Get(path);

    public static readonly FuncValueConverter<bool, FontWeight> Weight =
        new(bold => bold ? FontWeight.Bold : FontWeight.Normal);

    public static readonly FuncValueConverter<bool, FontStyle> Slant =
        new(italic => italic ? FontStyle.Italic : FontStyle.Normal);

    public static readonly FuncValueConverter<TextAlign, TextAlignment> Align =
        new(align => align switch
        {
            TextAlign.Center => TextAlignment.Center,
            TextAlign.Right => TextAlignment.Right,
            TextAlign.Justify => TextAlignment.Justify,
            _ => TextAlignment.Left
        });

    public static readonly FuncValueConverter<ImageFit, Stretch> Fit =
        new(fit => fit switch
        {
            ImageFit.UniformToFill => Stretch.UniformToFill,
            ImageFit.Fill => Stretch.Fill,
            _ => Stretch.Uniform
        });

    /// <summary>
    /// Les transformations Avalonia ne sont pas dans l'arbre logique : leur DataContext
    /// n'est donc pas hérité et on ne peut pas y placer une liaison. On produit
    /// l'objet transformation complet depuis un convertisseur.
    /// </summary>
    public static readonly FuncValueConverter<double, ITransform?> Rotate =
        new(angle => angle == 0 ? null : new RotateTransform(angle));

    public static readonly FuncValueConverter<double, ITransform> Scale =
        new(factor => new ScaleTransform(factor, factor));

    public static readonly FuncValueConverter<string?, FontFamily> Font =
        // La collection des documents, comme le texte composé : la saisie ne doit pas
        // montrer l'italique en gras quand l'affichage le montre penché (poste Q13).
        new(name => string.IsNullOrWhiteSpace(name) ? FontFamily.Default : Views.PolicesDuDocument.Famille(name));

    public static readonly FuncValueConverter<Insets, Thickness> Pad =
        new(insets => new Thickness(insets.Left, insets.Top, insets.Right, insets.Bottom));

    /// <summary>Épaisseur d'un repère : une ligne de base se pose plus légèrement.</summary>
    public static readonly FuncValueConverter<bool, double> GuideWeight =
        new(isBaseline => isBaseline ? 0.2 : 0.4);

    /// <summary>Teinte d'un repère : le bleu des marges, pâli pour les lignes de base.</summary>
    public static readonly FuncValueConverter<bool, IBrush> GuideTint =
        new(isBaseline => new SolidColorBrush(Color.Parse(isBaseline ? "#FFD3E4F4" : "#FF7FB2E5")));

    /// <summary>Motif du trait, en multiples de l'épaisseur ; null laisse le trait continu.</summary>
    public static readonly FuncValueConverter<StrokeStyle, AvaloniaList<double>?> Dash =
        new(style => Frame.DashPattern(style) is { } pattern ? new AvaloniaList<double>(pattern) : null);

    /// <summary>
    /// La marge qui centre un filet sur le bord de son bloc : une demi-épaisseur, vers
    /// l'extérieur.
    ///
    /// Un <c>Rectangle</c> ou une <c>Ellipse</c> d'Avalonia RENTRENT leur trait dans leurs
    /// limites : leur géométrie est le cadre diminué d'une demi-épaisseur. Posés sur le
    /// cadre du bloc, ils traçaient donc un filet de 2 pt sur [bord, bord + 2] quand
    /// l'export, qui dit vrai (« centré sur le bord », <see cref="Frame.StrokeThickness"/>),
    /// le trace sur [bord − 1, bord + 1] — poste Q4. Élargir l'élément de la même
    /// demi-épaisseur ramène sa géométrie exactement sur le cadre, arrondi compris : les
    /// rayons s'appliquent à la géométrie, qui est désormais celle que trace l'export.
    /// </summary>
    public static readonly FuncValueConverter<double, Thickness> FiletCentre =
        new(epaisseur => new Thickness(-Math.Max(0, epaisseur) / 2));

    public static readonly FuncValueConverter<string?, Bitmap?> Image =
        new(path => string.IsNullOrWhiteSpace(path) ? null : BitmapCache.Get(path));

    /// <summary>Points -> millimètres, pour l'affichage dans la barre d'état.</summary>
    public static readonly FuncValueConverter<double, string> Millimetres =
        new(points => Units.PointsToMm(points).ToString("0.0", CultureInfo.CurrentCulture) + " mm");

    public static readonly FuncValueConverter<double, string> Percent =
        new(ratio => (ratio * 100).ToString("0", CultureInfo.CurrentCulture) + " %");

}

/// <summary>
/// Les images sont relues à chaque notification de liaison : sans cache, faire
/// glisser un bloc image relirait le fichier à chaque image de l'animation.
///
/// Le cache est indexé par chemin, et ce que le poste A3 a changé le laisse intact
/// pour une raison qu'il faut écrire : les chemins relatifs sont résolus **à
/// l'ouverture**, si bien que ce cache ne voit jamais que des chemins absolus. Si le
/// modèle avait gardé la forme relue du fichier, deux documents rangés dans deux
/// dossiers et portant chacun « images/logo.png » se seraient partagé une seule
/// entrée — la seconde aurait affiché l'image de la première.
///
/// Chaque image lue garde l'empreinte de son fichier — date de modification et
/// taille, prises avant la lecture —, et l'ouverture d'un document oublie celles dont
/// le fichier a changé depuis : poste Q15. L'empreinte se compare à l'ouverture, et
/// non à chaque lecture du cache, qui a lieu à chaque image d'un glissé.
/// </summary>
internal static class BitmapCache
{
    /// <summary>Ce qui dit qu'un fichier a changé : sa date de modification et sa taille.</summary>
    private readonly record struct Empreinte(DateTime Date, long Taille);

    /// <summary>Une image lue, ou nulle pour un échec, et l'empreinte de son fichier.</summary>
    private readonly record struct Entree(Bitmap? Image, Empreinte? Empreinte);

    private static readonly Dictionary<string, Entree> Cache = new();

    /// <summary>
    /// Ce qui lit une image sur le disque. Un point d'injection pour le seul banc
    /// d'interface : il lui permet de réécrire le fichier PENDANT la lecture, et
    /// d'éprouver ainsi que l'empreinte est prise avant elle (poste Q15).
    /// </summary>
    internal static Func<string, Bitmap> Lecteur { get; set; } = chemin => new Bitmap(chemin);

    public static Bitmap? Get(string path)
    {
        // Deux graphies du même fichier ne font qu'une entrée. Ce n'est pas seulement
        // une économie : « Invalidate » ne retrouverait pas une entrée rangée sous une
        // graphie différente de celle qu'on lui donne.
        var cle = Cle(path);

        lock (Cache)
        {
            if (Cache.TryGetValue(cle, out var cached)) return cached.Image;

            // L'empreinte AVANT la lecture : un fichier réécrit pendant qu'on le lit
            // laisse une empreinte périmée, et sera relu à la prochaine ouverture,
            // plutôt que l'inverse.
            var empreinte = Prendre(cle);

            Bitmap? bitmap = null;
            try
            {
                bitmap = Lecteur(cle);
            }
            catch (Exception)
            {
                // Fichier absent ou format non géré : le bloc reste vide, sans planter.
            }

            Cache[cle] = new Entree(bitmap, empreinte);
            return bitmap;
        }
    }

    public static void Invalidate(string path)
    {
        lock (Cache) Cache.Remove(Cle(path));
    }

    /// <summary>
    /// Oublie les images qu'on n'a pas pu lire, et elles seules — poste Q3.
    ///
    /// Un échec est mis en cache comme une réussite, et ce n'est pas un oubli : sans
    /// cela, un bloc dont l'image manque relirait le disque, et lèverait une exception, à
    /// chaque notification de liaison, donc à chaque image d'un glissé. Mais l'échec
    /// était retenu pour toute la durée du processus : remettre le fichier en place puis
    /// rouvrir le document laissait le bloc vide, sans rien dire.
    ///
    /// Appelé à l'ouverture d'un document, qui est le moment où l'on s'attend à ce que
    /// le disque soit relu. Les réussites restent : une image lue est juste tant que
    /// son fichier ne change pas, et la relire à chaque ouverture ne réparerait aucun
    /// défaut de ce poste. Une image MODIFIÉE sur le disque, elle, est l'affaire de
    /// <see cref="OublierLesRetouchees"/> (poste Q15).
    /// </summary>
    public static void OublierLesEchecs()
    {
        lock (Cache)
        {
            var echecs = new List<string>();
            foreach (var (cle, entree) in Cache)
            {
                if (entree.Image is null) echecs.Add(cle);
            }

            foreach (var cle in echecs) Cache.Remove(cle);
        }
    }

    /// <summary>
    /// Oublie les images dont le fichier a changé depuis qu'on les a lues, et elles
    /// seules — poste Q15. Le pendant de <see cref="OublierLesEchecs"/>, appelé au même
    /// endroit : l'ouverture d'un document.
    ///
    /// Une image retouchée sur le disque pendant la session restait périmée à l'écran,
    /// réouverture comprise, alors que l'export, qui lit le disque à chaque fois,
    /// montrait la nouvelle : un écart écran ↔ PDF. Une image dont le fichier n'a pas
    /// bougé reste, elle, la même instance — l'épingle de Q3 tient. Un fichier disparu
    /// change d'empreinte : l'image est oubliée, et le bloc se vide comme le PDF le
    /// dit déjà (« Image introuvable »).
    ///
    /// Pourquoi à l'ouverture et non à chaque lecture du cache : celle-ci a lieu à
    /// chaque notification de liaison, donc à chaque image d'un glissé, et interroger
    /// le disque là — un partage réseau, parfois — coûterait sur le geste le plus
    /// fréquent ce qu'on n'achète que rarement. L'annulation, de même, ne relit rien :
    /// annuler n'est pas rouvrir.
    /// </summary>
    public static void OublierLesRetouchees()
    {
        lock (Cache)
        {
            var retouchees = new List<string>();
            foreach (var (cle, entree) in Cache)
            {
                if (entree.Image is not null && Prendre(cle) != entree.Empreinte) retouchees.Add(cle);
            }

            foreach (var cle in retouchees) Cache.Remove(cle);
        }
    }

    /// <summary>
    /// L'empreinte d'un fichier, ou null s'il n'existe pas ou ne se lit pas. La date
    /// seule ne suffirait pas toujours : certains systèmes de fichiers la comptent à la
    /// seconde, et une retouche faite dans la même seconde que la lecture passerait.
    /// </summary>
    private static Empreinte? Prendre(string cle)
    {
        try
        {
            var fichier = new System.IO.FileInfo(cle);
            return fichier.Exists ? new Empreinte(fichier.LastWriteTimeUtc, fichier.Length) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Le chemin normalisé. Un chemin illisible — vide, caractères interdits — est laissé
    /// tel quel : il ne s'ouvrira pas, et c'est « Get » qui le dira, pas ce cache.
    /// </summary>
    private static string Cle(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }
}
