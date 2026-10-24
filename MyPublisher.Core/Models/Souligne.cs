using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyPublisher.Models;

/// <summary>
/// Le dessin du trait d'un souligné, sous les noms qu'OpenDocument lui donne
/// (<c>style:text-underline-style</c>) : c'est sous ces noms que libmspub le transmet,
/// et que l'import le lit. <see cref="None"/> veut dire « pas de souligné ».
/// </summary>
public enum TraitDeSouligne { None, Solid, Dotted, Dash, LongDash, DotDash, DotDotDash, Wave }

/// <summary>
/// Le souligné d'un texte (poste I7) : le dessin du trait, simple ou double, épais ou
/// non, et « mots seuls » — les quatre réglages d'OpenDocument
/// (<c>style:text-underline-style</c>, <c>-type</c>, <c>-width</c>, <c>-mode</c>).
///
/// Pourquoi une sorte, et non un drapeau. Publisher en a dix-sept, que libmspub
/// transmet par <c>fillUnderline</c> (<c>MSPUBCollector.cpp:254-330</c>) dans ces quatre
/// réglages. Un drapeau les ramènerait toutes à un trait simple ; ces quatre-là en
/// relisent seize. La dix-septième, le tiret long épais, libmspub l'écrit comme le tiret
/// long ordinaire (son épaisseur n'est pas dans la liste des épaisses) : elle arrive
/// déjà confondue, et le modèle, qui saurait l'écrire, ne peut que la relire ainsi.
///
/// **La forme dans le fichier** est une valeur unique, dont le booléen reste la forme
/// courte : <c>false</c> pour aucun souligné, <c>true</c> pour le trait simple — le seul
/// que Publisher pose au clavier, et le seul que pose la commande « Souligné » —, et un
/// objet pour toute autre sorte : <c>{ "Line": "Dotted", "Double": false, "Thick": true,
/// "WordsOnly": false }</c>. L'autre forme, un drapeau suivi de trois réglages, aurait
/// écrit quatre clés de plus sur chaque bloc et chaque fragment de tous les documents —
/// le sérialiseur écrit toutes les valeurs —, et elle aurait laissé écrire des réglages
/// sans souligné, que rien ne dessine. Ici, un document sans souligné ne gagne qu'une clé
/// par bloc et par fragment, et une sorte n'existe qu'avec son trait.
///
/// Un état sans trait n'a qu'une valeur : le constructeur oublie les trois réglages d'un
/// souligné qui n'a pas de trait. Sans cela, deux « pas de souligné » ne seraient pas
/// égaux, et la découpe du texte (<see cref="StyleEffectif.Decouper"/>) couperait un
/// mot entre deux styles que rien ne distingue à l'œil.
/// </summary>
[JsonConverter(typeof(SouligneJson))]
public readonly record struct Souligne
{
    public Souligne(TraitDeSouligne line, bool @double = false, bool thick = false, bool wordsOnly = false)
    {
        Line = line;
        var actif = line != TraitDeSouligne.None;
        Double = actif && @double;
        Thick = actif && thick;
        WordsOnly = actif && wordsOnly;
    }

    /// <summary>Le dessin du trait ; <see cref="TraitDeSouligne.None"/>, pas de souligné.</summary>
    public TraitDeSouligne Line { get; }

    /// <summary>Deux traits au lieu d'un (<c>style:text-underline-type: double</c>).</summary>
    public bool Double { get; }

    /// <summary>Un trait deux fois plus épais que celui de la police (<c>-width: bold</c>).</summary>
    public bool Thick { get; }

    /// <summary>Rien sous les blancs (<c>-mode: skip-white-space</c>).</summary>
    public bool WordsOnly { get; }

    public static Souligne Aucun => default;

    /// <summary>Le souligné de <c>Ctrl+U</c> : un trait plein, simple, de l'épaisseur de la police.</summary>
    public static Souligne Simple => new(TraitDeSouligne.Solid);

    public bool Actif => Line != TraitDeSouligne.None;

    /// <summary>
    /// Le motif du trait, en multiples de son épaisseur — Avalonia et PDFsharp multiplient
    /// tous deux leur motif par l'épaisseur du stylo, si bien que la même table sert des
    /// deux côtés, comme celle du contour (<see cref="Frame.DashPattern"/>). Nul pour un
    /// trait continu : plein, vague.
    ///
    /// Les longueurs ne sont écrites nulle part chez Publisher ni chez OpenDocument, qui
    /// ne nomment que les sortes. Elles sont choisies pour que chaque sorte se reconnaisse
    /// à l'œil sous un corps courant : le point aussi long que le trait est épais, le
    /// tiret quatre fois, le tiret long huit.
    /// </summary>
    public double[]? Motif => Line switch
    {
        TraitDeSouligne.Dotted => [1, 1],
        TraitDeSouligne.Dash => [4, 2],
        TraitDeSouligne.LongDash => [8, 3],
        TraitDeSouligne.DotDash => [4, 2, 1, 2],
        TraitDeSouligne.DotDotDash => [4, 2, 1, 2, 1, 2],
        _ => null,
    };

    /// <summary>La vague ondule autour de son axe : son tracé vient de <see cref="Vague"/>.</summary>
    public bool Ondule => Line == TraitDeSouligne.Wave;

    /// <summary>
    /// Les traits à tracer sous la ligne de base, pour la place et l'épaisseur que la
    /// police donne : <paramref name="haut"/> est la distance de la ligne de base au haut
    /// du trait, positive vers le bas, et <paramref name="epaisseur"/> l'épaisseur, en
    /// points (voir <see cref="Mesure"/>).
    ///
    /// **Le haut du premier trait reste où la police le met**, quelle que soit la sorte :
    /// un trait épais s'épaissit vers le bas, une vague descend de son amplitude, et le
    /// second trait d'un double se pose sous le premier, à une épaisseur de lui. C'est la
    /// seule règle qui garde le blanc entre le texte et son souligné, que la police a
    /// choisi, et qui ne fait jamais toucher les jambages par le trait.
    /// </summary>
    public IReadOnlyList<TraceDeSouligne> Traits(double haut, double epaisseur)
    {
        if (!Actif) return [];

        var trait = Thick ? 2 * epaisseur : epaisseur;
        // L'amplitude d'une vague est d'une épaisseur de part et d'autre de son axe : un
        // creux plus profond déborderait vers la ligne suivante, un plus plat ne se
        // distinguerait plus d'un trait plein au corps d'un texte courant.
        var amplitude = Ondule ? trait : 0;
        var premier = haut + amplitude + trait / 2;

        if (!Double) return [new TraceDeSouligne(premier, trait)];

        // Une épaisseur de blanc entre les deux traits, crêtes comprises pour la vague.
        return [new TraceDeSouligne(premier, trait), new TraceDeSouligne(premier + 2 * amplitude + 2 * trait, trait)];
    }

    /// <summary>
    /// La place et l'épaisseur du trait dans un corps donné, à partir de ce que la table
    /// <c>post</c> de la police en dit, ramené au corps par l'appelant : la distance de la
    /// ligne de base au <b>haut</b> du trait et son épaisseur.
    ///
    /// Le haut, et non l'axe : c'est ce que dit la spécification OpenType
    /// (<c>underlinePosition</c>, « the top of the underline »), et ce que lisent Skia et
    /// Chrome. FreeType, lui, ramène la valeur à l'axe en retranchant la demi-épaisseur ;
    /// lire l'axe là où la table donne le haut descendrait le trait d'une demi-épaisseur.
    ///
    /// Une police sans épaisseur de souligné — une table <c>post</c> vide — en reçoit une
    /// d'un vingtième de corps, l'ordre de grandeur des polices courantes (DejaVu 4,4 %,
    /// Liberation Sans 7,3 %, Noto 5 %). Une police sans place de souligné — zéro, ou un
    /// haut au-dessus de la ligne de base — en reçoit une d'un dixième de corps sous la
    /// ligne de base, celle des polices Noto : un haut nul poserait le trait sur la ligne
    /// de base, collé à la panse d'un « o ». Un rendu qui ne lit aucune métrique passe
    /// deux zéros, et reçoit les deux. La règle est ici, et non dans chaque rendu, pour
    /// qu'ils ne s'en inventent pas chacun une.
    /// </summary>
    public static (double Haut, double Epaisseur) Mesure(double haut, double epaisseur, double corps) =>
        (haut > 0 ? haut : corps / 10, epaisseur > 0 ? epaisseur : corps / 20);

    /// <summary>
    /// Le tracé d'une vague de <paramref name="debut"/> à <paramref name="fin"/>, autour
    /// de l'ordonnée <paramref name="axe"/>, pour un trait d'épaisseur
    /// <paramref name="epaisseur"/> : une sinusoïde d'une épaisseur d'amplitude et de six
    /// de période, échantillonnée douze fois par période.
    ///
    /// Les points viennent d'ici, et les deux rendus les relient tels quels : une courbe
    /// que chacun échantillonnerait à sa façon ne tomberait pas aux mêmes pixels, et la
    /// comparaison de l'écran au PDF ne verrait plus que ce désaccord-là. La phase part du
    /// début du trait : deux mots soulignés d'une vague ne se raccordent pas d'un style à
    /// l'autre, ce qu'aucun lecteur ne cherche.
    /// </summary>
    public static IReadOnlyList<(double X, double Y)> Vague(double debut, double fin, double axe, double epaisseur)
    {
        var points = new List<(double, double)>();
        if (fin <= debut || epaisseur <= 0) return points;

        var periode = 6 * epaisseur;
        var pas = periode / 12;
        var nombre = (int)Math.Ceiling((fin - debut) / pas);
        for (var i = 0; i <= nombre; i++)
        {
            var x = Math.Min(fin, debut + i * pas);
            points.Add((x, axe + epaisseur * Math.Sin(2 * Math.PI * (x - debut) / periode)));
        }

        return points;
    }
}

/// <summary>
/// Un trait de souligné à tracer : l'ordonnée de son axe sous la ligne de base, positive
/// vers le bas, et son épaisseur, en points. Un rendu le trace d'un stylo de cette
/// épaisseur, centré sur l'axe — droit, ou le long de <see cref="Souligne.Vague"/>.
/// </summary>
public readonly record struct TraceDeSouligne(double Axe, double Epaisseur);

/// <summary>
/// La forme d'un souligné dans le fichier : <c>false</c>, <c>true</c>, ou un objet pour
/// les autres sortes (voir <see cref="Souligne"/>).
///
/// L'objet s'écrit en entier, ses quatre réglages compris, comme le sérialiseur écrit
/// toutes les valeurs ; à la lecture, un réglage absent vaut son défaut, et un trait
/// absent vaut le trait plein — un objet dit qu'il y a un souligné. Un trait inconnu est
/// refusé comme une valeur d'énumération inconnue l'est partout ailleurs dans le format :
/// le lire « plein » ferait dessiner ce que le fichier ne dit pas.
/// </summary>
public sealed class SouligneJson : JsonConverter<Souligne>
{
    public override Souligne Read(ref Utf8JsonReader lecteur, Type type, JsonSerializerOptions options)
    {
        switch (lecteur.TokenType)
        {
            case JsonTokenType.True: return Souligne.Simple;
            case JsonTokenType.False: return Souligne.Aucun;
            case JsonTokenType.StartObject: break;
            default: throw new JsonException($"Souligné illisible : {lecteur.TokenType}.");
        }

        var trait = TraitDeSouligne.Solid;
        bool @double = false, epais = false, motsSeuls = false;

        while (lecteur.Read() && lecteur.TokenType != JsonTokenType.EndObject)
        {
            var cle = lecteur.GetString();
            lecteur.Read();
            switch (cle)
            {
                case "Line":
                    // Par son nom seul : « TryParse » prendrait aussi « 3 » ou « Solid, Dotted ».
                    var nom = lecteur.GetString();
                    if (nom is null || Array.IndexOf(Enum.GetNames<TraitDeSouligne>(), nom) < 0)
                    {
                        throw new JsonException($"Trait de souligné inconnu : « {nom} ».");
                    }

                    trait = Enum.Parse<TraitDeSouligne>(nom);
                    break;
                case "Double": @double = lecteur.GetBoolean(); break;
                case "Thick": epais = lecteur.GetBoolean(); break;
                case "WordsOnly": motsSeuls = lecteur.GetBoolean(); break;
                default: lecteur.Skip(); break;
            }
        }

        return new Souligne(trait, @double, epais, motsSeuls);
    }

    public override void Write(Utf8JsonWriter ecrivain, Souligne valeur, JsonSerializerOptions options)
    {
        if (!valeur.Actif) { ecrivain.WriteBooleanValue(false); return; }
        if (valeur == Souligne.Simple) { ecrivain.WriteBooleanValue(true); return; }

        ecrivain.WriteStartObject();
        ecrivain.WriteString("Line", valeur.Line.ToString());
        ecrivain.WriteBoolean("Double", valeur.Double);
        ecrivain.WriteBoolean("Thick", valeur.Thick);
        ecrivain.WriteBoolean("WordsOnly", valeur.WordsOnly);
        ecrivain.WriteEndObject();
    }
}
