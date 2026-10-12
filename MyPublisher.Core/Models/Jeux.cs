using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace MyPublisher.Models;

/// <summary>Les cinq emplacements d'un jeu de couleurs, dans l'ordre où Publisher les montre.</summary>
public enum EmplacementDeCouleur { Main, Accent1, Accent2, Accent3, Accent4 }

/// <summary>Les deux emplacements d'un jeu de polices.</summary>
public enum EmplacementDePolice { Heading, Body }

/// <summary>
/// Un jeu de couleurs : un nom et cinq couleurs, portés par le document — le « Color
/// scheme » de Publisher (fiche 01). La première, « principale », est celle du texte ;
/// les quatre autres sont des accents.
///
/// Un enregistrement et non une classe observable : on ne retouche pas une couleur d'un
/// jeu, on change de jeu. C'est ce qui fait que le remplacer suffit à prévenir l'écran,
/// sans abonnement à entretenir — le piège du remplissage modifié sur place, que C5c a
/// payé de deux fuites, n'a pas lieu d'être ici. L'égalité de valeur sert aussi le
/// sélecteur du panneau, qui reconnaît le jeu d'un document relu dans sa liste.
/// </summary>
public sealed record JeuDeCouleurs
{
    [JsonPropertyOrder(10)] public string Name { get; init; } = "";
    [JsonPropertyOrder(20)] public string Main { get; init; } = "#FF1A1A1A";
    [JsonPropertyOrder(30)] public string Accent1 { get; init; } = "#FF2E5C8A";
    [JsonPropertyOrder(40)] public string Accent2 { get; init; } = "#FF9DC3E6";
    [JsonPropertyOrder(50)] public string Accent3 { get; init; } = "#FFE8A33D";
    [JsonPropertyOrder(60)] public string Accent4 { get; init; } = "#FF6B7280";

    public string Couleur(EmplacementDeCouleur emplacement) => emplacement switch
    {
        EmplacementDeCouleur.Accent1 => Accent1,
        EmplacementDeCouleur.Accent2 => Accent2,
        EmplacementDeCouleur.Accent3 => Accent3,
        EmplacementDeCouleur.Accent4 => Accent4,
        _ => Main,
    };

    /// <summary>Les cinq couleurs dans l'ordre des emplacements, pour les pastilles du panneau.</summary>
    [JsonIgnore] public IReadOnlyList<string> Couleurs => [Main, Accent1, Accent2, Accent3, Accent4];

    public override string ToString() => Name;
}

/// <summary>
/// Un jeu de polices : une paire titre/corps nommée — le « Font scheme » de Publisher.
/// Même raison d'être un enregistrement que <see cref="JeuDeCouleurs"/>.
/// </summary>
public sealed record JeuDePolices
{
    [JsonPropertyOrder(10)] public string Name { get; init; } = "";
    [JsonPropertyOrder(20)] public string Heading { get; init; } = "DejaVu Sans";
    [JsonPropertyOrder(30)] public string Body { get; init; } = "DejaVu Sans";

    public string Police(EmplacementDePolice emplacement) =>
        emplacement == EmplacementDePolice.Heading ? Heading : Body;

    public override string ToString() => Name;
}

/// <summary>
/// La forme écrite d'une référence au jeu, là où le modèle attend une couleur ou une
/// police.
///
/// **Pourquoi dans la chaîne même.** Les couleurs du modèle sont des chaînes
/// <c>#AARRGGBB</c> en une douzaine d'endroits — fond uni, extrémités d'un dégradé,
/// base d'une teinte, contour, texte, fragment de style, case de tableau, grille, fond
/// de page. Une référence écrite à leur place les couvre toutes d'un coup, sans
/// propriété jumelle à tenir en accord avec la première. Une couleur saisie en
/// hexadécimal reste ce qu'elle est : libre.
///
/// Une référence s'écrit <c>scheme:accent1</c> ; elle peut porter sa propre opacité,
/// <c>scheme:accent1/80</c> — l'octet alpha en hexadécimal, comme dans <c>#AARRGGBB</c>
/// — parce que l'opacité d'un fond se règle sur le bloc et que Publisher la garde quand
/// on change de jeu. Une police s'écrit <c>scheme:heading</c> ou <c>scheme:body</c>.
///
/// Une valeur libre ne commence donc jamais par <c>scheme:</c>. Le format 4, qui ne
/// connaissait pas les références, n'interdisait rien de tel : c'est le travail de la
/// migration (<see cref="Liberer"/>) que de garder libre ce qui l'était.
/// </summary>
public static class ReferenceDuJeu
{
    public const string Prefixe = "scheme:";

    /// <summary>Ce qu'on met devant une valeur libre qui ressemblerait à une référence.</summary>
    public const string Echappement = "!";

    private static readonly (string Nom, EmplacementDeCouleur Emplacement)[] NomsDeCouleurs =
    [
        ("main", EmplacementDeCouleur.Main),
        ("accent1", EmplacementDeCouleur.Accent1),
        ("accent2", EmplacementDeCouleur.Accent2),
        ("accent3", EmplacementDeCouleur.Accent3),
        ("accent4", EmplacementDeCouleur.Accent4),
    ];

    public static bool EstReference(string? valeur) =>
        valeur is not null && valeur.StartsWith(Prefixe, StringComparison.Ordinal);

    /// <summary>La référence à un emplacement de couleur, avec ou sans opacité propre.</summary>
    public static string Couleur(EmplacementDeCouleur emplacement, byte? alpha = null)
    {
        var nom = NomsDeCouleurs.First(n => n.Emplacement == emplacement).Nom;
        return alpha is { } a ? $"{Prefixe}{nom}/{a:X2}" : Prefixe + nom;
    }

    public static string Police(EmplacementDePolice emplacement) =>
        Prefixe + (emplacement == EmplacementDePolice.Heading ? "heading" : "body");

    /// <summary>Lit une référence de couleur ; faux pour une couleur libre ou une référence illisible.</summary>
    public static bool TryCouleur(string? valeur, out EmplacementDeCouleur emplacement, out byte? alpha)
    {
        emplacement = EmplacementDeCouleur.Main;
        alpha = null;
        if (!EstReference(valeur)) return false;

        var corps = valeur![Prefixe.Length..];
        var barre = corps.IndexOf('/');
        var nom = barre < 0 ? corps : corps[..barre];

        var trouve = Array.FindIndex(NomsDeCouleurs, n => n.Nom == nom);
        if (trouve < 0) return false;

        if (barre >= 0)
        {
            if (!byte.TryParse(corps[(barre + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a))
            {
                return false;
            }

            alpha = a;
        }

        emplacement = NomsDeCouleurs[trouve].Emplacement;
        return true;
    }

    /// <summary>Lit une référence de police ; faux pour une police libre.</summary>
    public static bool TryPolice(string? valeur, out EmplacementDePolice emplacement)
    {
        emplacement = EmplacementDePolice.Body;
        switch (valeur)
        {
            case Prefixe + "heading":
                emplacement = EmplacementDePolice.Heading;
                return true;
            case Prefixe + "body":
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Garde libre une valeur qui ressemblerait à une référence, et laisse toute autre
    /// valeur intacte.
    ///
    /// Sert là où une valeur arrive d'ailleurs que du panneau : la migration d'un fichier
    /// de la version 4, et l'import d'un <c>.pub</c>, qui nomme ses polices comme il
    /// l'entend. Le préfixe ajouté ne change rien à ce qu'on voit : une telle couleur
    /// était illisible et le reste, une telle police était introuvable et le reste.
    /// </summary>
    public static string? Liberer(string? valeur) =>
        EstReference(valeur) ? Echappement + valeur : valeur;
}

/// <summary>
/// Le jeu de couleurs et le jeu de polices d'un document, et ce qu'ils font des
/// références : c'est ici, et nulle part ailleurs, qu'une référence devient une couleur
/// ou une police.
///
/// L'écran et l'export appellent les mêmes fonctions — <see cref="Couleur"/>,
/// <see cref="StyleCommun"/>, <see cref="Fragments"/> — comme ils appellent déjà la
/// même composition. Une résolution écrite deux fois pourrait se tromper d'un seul côté ;
/// une résolution écrite une fois se trompe des deux, et c'est un test du noyau qui la
/// garde.
/// </summary>
public sealed record Charte(JeuDeCouleurs Couleurs, JeuDePolices Polices)
{
    /// <summary>Les jeux par défaut, pour ce qui se dessine hors de tout document.</summary>
    public static Charte Defaut { get; } = new(JeuxPredefinis.CouleursParDefaut, JeuxPredefinis.PolicesParDefaut);

    /// <summary>
    /// La couleur à dessiner : celle du jeu pour une référence, la valeur elle-même pour
    /// une couleur libre. Une référence illisible rend null, et les deux rendus la
    /// traitent comme une couleur illisible : un fond ou un contour ne se dessine pas, un
    /// texte retombe au noir.
    /// </summary>
    public string? Couleur(string? valeur)
    {
        if (!ReferenceDuJeu.EstReference(valeur)) return valeur;
        if (!ReferenceDuJeu.TryCouleur(valeur, out var emplacement, out var alpha)) return null;

        var couleur = Couleurs.Couleur(emplacement);
        return alpha is { } a ? HexColor.WithAlpha(couleur, a) : couleur;
    }

    /// <summary>La police à employer : celle du jeu pour une référence, la valeur elle-même sinon.</summary>
    public string Police(string valeur) =>
        ReferenceDuJeu.TryPolice(valeur, out var emplacement) ? Polices.Police(emplacement) : valeur;

    /// <summary>Un style dont la police et la couleur sont résolues.</summary>
    public StyleEffectif Resoudre(StyleEffectif style) => style with
    {
        FontFamily = Police(style.FontFamily),
        Foreground = Couleur(style.Foreground) ?? style.Foreground,
    };

    /// <summary>Le style commun d'un bloc de texte, résolu : ce dont ses fragments s'écartent.</summary>
    public StyleEffectif StyleCommun(TextFrame bloc) =>
        Resoudre(new StyleEffectif(bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic, bloc.Foreground, 0));

    /// <summary>
    /// Les fragments d'un bloc, leurs références résolues.
    ///
    /// Un fragment ne dit rien de ce qu'il laisse au bloc — une propriété nulle veut dire
    /// « comme le bloc » — et une référence ne s'y résout qu'à elle-même : un fragment
    /// en <c>scheme:accent3</c> prend l'accent 3, quel que soit le bloc. Les fragments
    /// sans référence sont rendus tels quels, sans copie : c'est le cas de tous les
    /// documents importés.
    /// </summary>
    public IReadOnlyList<StyleSpan> Fragments(IReadOnlyList<StyleSpan> fragments)
    {
        if (!fragments.Any(f => ReferenceDuJeu.EstReference(f.FontFamily) || ReferenceDuJeu.EstReference(f.Foreground)))
        {
            return fragments;
        }

        return fragments.Select(f =>
        {
            var copie = f.Clone();
            if (copie.FontFamily is { } police) copie.FontFamily = Police(police);
            if (copie.Foreground is { } couleur) copie.Foreground = Couleur(couleur) ?? couleur;
            return copie;
        }).ToList();
    }
}

/// <summary>
/// Les jeux proposés au panneau. Une petite collection suffit : le poste demande qu'on
/// reste cohérent d'un bloc à l'autre, pas qu'on reproduise les soixante-dix jeux de
/// Publisher.
///
/// Le premier jeu de chaque liste est celui d'un document neuf, et il n'est pas choisi
/// au hasard : ses couleurs sont exactement celles que les blocs neufs portaient en dur
/// avant ce poste — texte <c>#FF1A1A1A</c>, contour de forme <c>#FF2E5C8A</c>, fond de
/// forme <c>#FF9DC3E6</c>, grille de tableau <c>#FF6B7280</c> — et ses polices celle
/// du bloc de texte, DejaVu Sans. Un bloc neuf qui fait désormais référence au jeu se
/// dessine donc comme avant.
///
/// Les polices sont celles qu'une installation Linux courante porte : un jeu dont la
/// police manque se substituerait à l'export, et le dirait (poste B5), mais ce serait
/// proposer au panneau un choix qu'on sait faux.
/// </summary>
public static class JeuxPredefinis
{
    public static JeuDeCouleurs CouleursParDefaut { get; } = new()
    {
        Name = "Ardoise",
        Main = "#FF1A1A1A", Accent1 = "#FF2E5C8A", Accent2 = "#FF9DC3E6",
        Accent3 = "#FFE8A33D", Accent4 = "#FF6B7280",
    };

    public static IReadOnlyList<JeuDeCouleurs> Couleurs { get; } =
    [
        CouleursParDefaut,
        // Les cinq couleurs que les quatre modèles de B1 avaient en commun avant ce poste :
        // ils l'ont adopté, et s'affichent exactement comme avant.
        new()
        {
            Name = "Marine",
            Main = "#FF1F2933", Accent1 = "#FF1F3B57", Accent2 = "#FFF0C674",
            Accent3 = "#FF6B7280", Accent4 = "#FFEDF2F8",
        },
        new()
        {
            Name = "Bordeaux",
            Main = "#FF2B1B17", Accent1 = "#FF7B2D26", Accent2 = "#FFE3B5A4",
            Accent3 = "#FFC9A227", Accent4 = "#FF5E6B5A",
        },
        new()
        {
            Name = "Forêt",
            Main = "#FF1F2A1F", Accent1 = "#FF2F6B3A", Accent2 = "#FFB8D8A8",
            Accent3 = "#FFD98E04", Accent4 = "#FF7A6A53",
        },
        new()
        {
            Name = "Océan",
            Main = "#FF102A43", Accent1 = "#FF0B7285", Accent2 = "#FF99E9F2",
            Accent3 = "#FFF08C00", Accent4 = "#FF495057",
        },
        new()
        {
            Name = "Agrumes",
            Main = "#FF333333", Accent1 = "#FFE8590C", Accent2 = "#FFFFD43B",
            Accent3 = "#FF2B8A3E", Accent4 = "#FF868E96",
        },
    ];

    public static JeuDePolices PolicesParDefaut { get; } = new()
    {
        Name = "Sobre", Heading = "DejaVu Sans", Body = "DejaVu Sans",
    };

    public static IReadOnlyList<JeuDePolices> Polices { get; } =
    [
        PolicesParDefaut,
        new() { Name = "Archives", Heading = "DejaVu Serif", Body = "DejaVu Serif" },
        new() { Name = "Édition", Heading = "Liberation Serif", Body = "Liberation Sans" },
        new() { Name = "Moderne", Heading = "Liberation Sans", Body = "Liberation Serif" },
        new() { Name = "Notes", Heading = "Noto Sans", Body = "Noto Serif" },
    ];

    public static JeuDeCouleurs? CouleursNommees(string nom) => Couleurs.FirstOrDefault(j => j.Name == nom);

    public static JeuDePolices? PolicesNommees(string nom) => Polices.FirstOrDefault(j => j.Name == nom);
}
