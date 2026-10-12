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
    /// Les gestionnaires qui ont déjà la collection. Un par processus pour
    /// l'application ; l'hôte des tests d'interface, lui, en monte un par session, et un
    /// simple drapeau croirait installée chez le nouveau la collection de l'ancien.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FontManager, object> Installees = new();

    private readonly ConcurrentDictionary<(string Famille, bool Gras, bool Italique), Lazy<IGlyphTypeface?>> _faces = new();

    /// <summary>
    /// La famille d'un document, adressée dans cette collection. La collection est
    /// installée au besoin : un hôte qui l'aurait oubliée ne doit pas mesurer dans le
    /// vide, ni retomber en silence sur la résolution d'Avalonia.
    /// </summary>
    public static FontFamily Famille(string nom)
    {
        Installer(FontManager.Current);
        return new FontFamily($"{Cle}#{nom}");
    }

    /// <summary>
    /// Ajoute la collection au gestionnaire de polices, une fois par gestionnaire. Les
    /// hôtes l'appellent au démarrage par <see cref="PolicesAppBuilder"/> ; la règle à
    /// mesurer l'appelle aussi avant sa première mesure, pour qu'aucun hôte oublié ne
    /// mesure dans le vide.
    /// </summary>
    public static void Installer(FontManager gestionnaire)
    {
        lock (Installees)
        {
            if (Installees.TryGetValue(gestionnaire, out _)) return;
            gestionnaire.AddFontCollection(new PolicesDuDocument());
            Installees.Add(gestionnaire, new object());
        }
    }

    public override Uri Key => Cle;

    public override int Count => 0;

    public override FontFamily this[int index] => throw new ArgumentOutOfRangeException(nameof(index));

    public override IEnumerator<FontFamily> GetEnumerator() => ((IEnumerable<FontFamily>)[]).GetEnumerator();

    public override void Initialize(IFontManagerImpl fontManager)
    {
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

    private static IGlyphTypeface? Charger(string famille, bool gras, bool italique)
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
    /// <c>IFontManagerImpl.TryCreateGlyphTypeface(Stream, FontSimulations, out …)</c>,
    /// appelé par réflexion, sur le gestionnaire de la plate-forme
    /// (<c>FontManager.PlatformImpl</c>, caché de même).
    ///
    /// C'est la seule porte d'Avalonia 11.3.9 qui charge une police depuis des octets
    /// avec des simulations choisies par l'appelant ; l'assemblage de référence la cache
    /// (« [PrivateApi] »), l'assemblage d'exécution l'expose. Les deux portes publiques
    /// ne conviennent pas : la collection du système rabat une face absente sur son
    /// cache — c'est le défaut —, et <c>TryCreateSyntheticGlyphTypeface</c> rend, essayé
    /// ici, une face sans aucune simulation. Si une version d'Avalonia retire cette
    /// méthode, l'exception le dira à la première mesure, et les tests du poste Q13
    /// tomberont ; on ne retombe pas en silence sur la résolution d'Avalonia.
    /// </summary>
    private static IGlyphTypeface? DepuisLeFichier(Stream flux, FontSimulations simulations)
    {
        var plateforme = typeof(FontManager)
                             .GetProperty("PlatformImpl", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
                             .GetValue(FontManager.Current)
                         ?? throw new MissingMemberException("FontManager.PlatformImpl est introuvable.");
        var methode = typeof(IFontManagerImpl).GetMethod(
                          "TryCreateGlyphTypeface",
                          [typeof(Stream), typeof(FontSimulations), typeof(IGlyphTypeface).MakeByRefType()])
                      ?? throw new MissingMethodException(
                          "IFontManagerImpl.TryCreateGlyphTypeface(Stream, FontSimulations) est introuvable.");

        object?[] arguments = [flux, simulations, null];
        return methode.Invoke(plateforme, arguments) is true ? arguments[2] as IGlyphTypeface : null;
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
