using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Platform;
using MyPublisher.Services;

namespace MyPublisher.Views;

/// <summary>
/// Les polices du texte des documents, résolues par MyPublisher et non par Avalonia
/// (poste Q13).
///
/// Avalonia 11.3.9 résout une face absente — l'italique de DejaVu Sans, que cette
/// machine n'a pas — en prenant, dans la famille, la face qu'il a DÉJÀ chargée dans le
/// processus : le gras si le gras est venu le premier, le romain sinon. La même ligne
/// de légende mesurait ainsi 262,83 pt dans un processus et 233,96 pt dans un autre, et
/// l'écran dessinait l'italique en gras. Une mesure qui dépend de l'histoire du
/// processus ne peut pas décider d'une coupure que l'écran et l'export doivent prendre
/// ensemble.
///
/// Cette collection ne consulte jamais ce qu'Avalonia a chargé avant. Pour une famille,
/// une graisse et une pente, elle demande à fontconfig le fichier — le même appel, et
/// le même cache, que l'export (<see cref="FontconfigFontResolver.Resoudre"/>) — et le
/// charge avec les simulations que fontconfig a décidées : penché s'il n'a qu'un
/// romain, épaissi s'il n'a pas de gras. Chaque variante est résolue une fois pour
/// toutes. L'écran et le PDF dessinent donc le même fichier, de la même façon.
///
/// Les familles s'y adressent par <see cref="Famille"/> : « fonts:MyPublisher#DejaVu
/// Sans ». Le chrome de l'application, lui, garde les polices du système.
/// </summary>
public sealed class PolicesDuDocument : FontCollectionBase
{
    public static readonly Uri Cle = new("fonts:MyPublisher", UriKind.Absolute);

    /// <summary>
    /// La variable qui fait comme si Avalonia n'avait plus la porte privée : la recette
    /// de Q13 et ses tests s'en servent pour éprouver le repli dans un vrai processus,
    /// sans fausser Avalonia. Elle fait chercher une méthode qui n'existe pas, si bien
    /// que c'est le vrai chemin de l'échec qui est parcouru.
    /// </summary>
    public const string VariableSansPorte = "MYPUBLISHER_SANS_PORTE_PRIVEE";

    /// <summary>Le nom de la porte privée d'Avalonia (cf. <see cref="ChercherLaPorte"/>).</summary>
    public const string NomDeLaPorte = "TryCreateGlyphTypeface";

    /// <summary>
    /// La porte, cherchée UNE fois par processus, au premier montage des polices —
    /// c'est-à-dire au démarrage de l'application, du banc, de l'hôte des tests et de
    /// l'export sans fenêtre. Jusqu'à la relecture de Q13, elle se cherchait à chaque
    /// police chargée, et son absence ne se découvrait qu'au premier texte : un export
    /// sans texte et sans erreur, une application qui levait au premier rendu.
    /// </summary>
    private static readonly Lazy<(MethodInfo? Methode, string? Motif)> Porte = new(() =>
        ChercherLaPorte(Environment.GetEnvironmentVariable(VariableSansPorte) == "1"
            ? NomDeLaPorte + "·absente"
            : NomDeLaPorte));

    /// <summary>
    /// Pourquoi la collection n'est pas montée, ou <c>null</c> si elle l'est. Quand la
    /// porte manque, les polices des documents retombent sur la résolution d'Avalonia —
    /// celle d'avant Q13, qui dépend de ce que le processus a chargé — plutôt que de
    /// faire lever chaque texte : l'application reste utilisable, et le dit. L'export
    /// sans fenêtre, lui, refuse de produire un PDF (<c>Program.MesureDeLEcran</c>).
    /// </summary>
    public static string? Indisponible => Porte.Value.Motif;

    /// <summary>
    /// Les gestionnaires qui ont déjà la collection. Un par processus pour
    /// l'application ; l'hôte des tests d'interface, lui, en monte un par session, et un
    /// simple drapeau croirait installée chez le nouveau la collection de l'ancien.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FontManager, object> Installees = new();

    /// <summary>L'avertissement global ne s'écrit qu'une fois par processus.</summary>
    private static int _averti;

    private readonly ConcurrentDictionary<(string Famille, bool Gras, bool Italique), Lazy<IGlyphTypeface?>> _faces = new();

    /// <summary>Le gestionnaire de la plate-forme, que <see cref="Initialize"/> reçoit.</summary>
    private IFontManagerImpl? _plateforme;

    /// <summary>
    /// La famille d'un document, adressée dans cette collection. La collection est
    /// installée au besoin : un hôte qui l'aurait oubliée ne doit pas mesurer dans le
    /// vide. Si la porte manque, c'est la famille du système, et l'avertissement global
    /// a été écrit au montage.
    /// </summary>
    public static FontFamily Famille(string nom)
    {
        Installer(FontManager.Current);
        return Indisponible is null ? new FontFamily($"{Cle}#{nom}") : new FontFamily(nom);
    }

    /// <summary>
    /// Ajoute la collection au gestionnaire de polices, une fois par gestionnaire. Les
    /// hôtes l'appellent au démarrage par <see cref="PolicesAppBuilder"/> ; la règle à
    /// mesurer l'appelle aussi avant sa première mesure, pour qu'aucun hôte oublié ne
    /// mesure dans le vide. C'est ici que la porte se cherche, et que son absence se dit.
    /// </summary>
    public static void Installer(FontManager gestionnaire)
    {
        if (Indisponible is { } motif)
        {
            if (System.Threading.Interlocked.Exchange(ref _averti, 1) == 0)
            {
                Console.Error.WriteLine(
                    $"Avertissement : {motif} Les polices des documents sont résolues par Avalonia, " +
                    "et l'écran peut couper ses lignes autrement que le PDF.");
            }

            return;
        }

        lock (Installees)
        {
            if (Installees.TryGetValue(gestionnaire, out _)) return;
            gestionnaire.AddFontCollection(new PolicesDuDocument());
            Installees.Add(gestionnaire, new object());
        }
    }

    /// <summary>
    /// <c>IFontManagerImpl.TryCreateGlyphTypeface(Stream, FontSimulations, out …)</c>,
    /// cherchée par réflexion.
    ///
    /// C'est la seule porte d'Avalonia 11.3.9 qui charge une police depuis des octets
    /// avec des simulations choisies par l'appelant ; l'assemblage de référence la cache
    /// (« [PrivateApi] »), l'assemblage d'exécution l'expose. Les deux portes publiques
    /// ne conviennent pas : la collection du système rabat une face absente sur son
    /// cache — c'est le défaut —, et <c>TryCreateSyntheticGlyphTypeface</c> rend, essayé
    /// ici, une face sans aucune simulation. Une version d'Avalonia qui la retirerait se
    /// verrait ici, au démarrage, avec le motif qu'on rend.
    /// </summary>
    public static (MethodInfo? Methode, string? Motif) ChercherLaPorte(string nom)
    {
        var methode = typeof(IFontManagerImpl).GetMethod(
            nom, [typeof(Stream), typeof(FontSimulations), typeof(IGlyphTypeface).MakeByRefType()]);

        return methode is not null && methode.ReturnType == typeof(bool)
            ? (methode, null)
            : (null, $"Avalonia n'a plus la méthode IFontManagerImpl.{nom}(Stream, FontSimulations, out IGlyphTypeface).");
    }

    public override Uri Key => Cle;

    public override int Count => 0;

    public override FontFamily this[int index] => throw new ArgumentOutOfRangeException(nameof(index));

    public override IEnumerator<FontFamily> GetEnumerator() => ((IEnumerable<FontFamily>)[]).GetEnumerator();

    public override void Initialize(IFontManagerImpl fontManager)
    {
        _plateforme = fontManager;
    }

    public override bool TryGetGlyphTypeface(
        string familyName, FontStyle style, FontWeight weight, FontStretch stretch,
        out IGlyphTypeface glyphTypeface)
    {
        // Le modèle ne connaît que deux graisses et deux pentes ; c'est en elles qu'on
        // range ce qu'on demande. Une demi-grasse demandée par ailleurs compte pour un
        // gras, une oblique pour une italique.
        var cle = (familyName, weight >= FontWeight.SemiBold, style != FontStyle.Normal);
        var face = _faces.GetOrAdd(cle, c => new Lazy<IGlyphTypeface?>(
            () => Charger(c.Famille, c.Gras, c.Italique) ?? Systeme(c.Famille, style, weight, stretch))).Value;

        glyphTypeface = face!;
        return face is not null;
    }

    /// <summary>
    /// Le dernier recours, quand le fichier choisi ne se charge pas — aucun fichier, ou un
    /// format que Skia refuse : la résolution d'Avalonia, plutôt qu'une page qui ne
    /// s'affiche plus. Une collection qui ne rend rien fait lever une exception à chaque
    /// texte formaté. Ce recours retrouve le défaut que la collection corrige, mais
    /// seulement pour une police que l'export, lui, ne saurait pas lire non plus.
    /// </summary>
    private static IGlyphTypeface? Systeme(string famille, FontStyle style, FontWeight weight, FontStretch stretch) =>
        FontManager.Current.SystemFonts.TryGetGlyphTypeface(famille, style, weight, stretch, out var face)
            ? face
            : null;

    private IGlyphTypeface? Charger(string famille, bool gras, bool italique)
    {
        var resolution = FontconfigFontResolver.Resoudre(famille, gras, italique);
        if (string.IsNullOrEmpty(resolution.Fichier)) return null;

        var simulations = FontSimulations.None;
        if (resolution.SimulerGras) simulations |= FontSimulations.Bold;
        if (resolution.SimulerItalique) simulations |= FontSimulations.Oblique;

        // Le fichier même que l'export emploie, et les simulations que fontconfig a
        // décidées — rien de ce qu'Avalonia aurait gardé en cache.
        var octets = FontconfigFontResolver.Octets(resolution.Fichier);
        return DepuisLeFichier(new MemoryStream(octets, writable: false), simulations);
    }

    /// <summary>
    /// Charge une face par la porte cherchée au démarrage. La collection n'est montée
    /// que si la porte existe ; si l'appel échoue malgré tout, la face retombe sur le
    /// dernier recours plutôt que de faire lever le rendu.
    /// </summary>
    private IGlyphTypeface? DepuisLeFichier(Stream flux, FontSimulations simulations)
    {
        if (Porte.Value.Methode is not { } methode || _plateforme is null) return null;

        try
        {
            object?[] arguments = [flux, simulations, null];
            return methode.Invoke(_plateforme, arguments) is true ? arguments[2] as IGlyphTypeface : null;
        }
        catch (TargetInvocationException)
        {
            return null;
        }
    }
}

/// <summary>
/// Monte les polices comme l'application les monte : Inter pour le chrome, et la
/// collection des documents. L'application, l'export sans fenêtre, le banc et l'hôte
/// des tests d'interface passent tous par ici, pour qu'aucun ne mesure autrement.
/// </summary>
public static class PolicesAppBuilder
{
    public static AppBuilder AvecLesPolicesDeMyPublisher(this AppBuilder builder) =>
        builder.WithInterFont()
            .ConfigureFonts(gestionnaire => PolicesDuDocument.Installer(gestionnaire));
}
