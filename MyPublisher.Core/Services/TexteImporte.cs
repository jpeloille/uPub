using System;
using System.IO;
using System.Text;

namespace MyPublisher.Services;

/// <summary>
/// Un fichier texte lu pour remplir un bloc (poste B2) : ses caractères, et le nom de
/// l'encodage sous lequel on les a lus — la barre d'état le dit, pour qu'un fichier lu
/// de travers se voie à autre chose qu'à ses accents.
/// </summary>
public sealed record TexteLu(string Texte, string Encodage);

/// <summary>
/// Un fichier qu'on refuse de verser dans un bloc : vide, trop gros, ou qui n'est pas du
/// texte. Le message dit pourquoi, en clair ; la barre d'état le reprend tel quel.
/// </summary>
public sealed class TexteRefuseException(string message) : Exception(message);

/// <summary>
/// Lire un fichier texte comme un bloc l'attend. Tout est pur ici, sauf
/// <see cref="Lire"/>, qui ne fait que prendre les octets du disque : le décodage
/// s'éprouve sur des octets écrits dans le test, sans fichier.
///
/// Un fichier texte ne dit pas son encodage. On le devine, dans cet ordre :
/// <list type="number">
/// <item>une marque d'ordre d'octets le dit : EF BB BF pour l'UTF-8, FF FE et FE FF
/// pour l'UTF-16 petit- et gros-boutiste. Elle est retirée, et le reste se lit dans
/// l'encodage qu'elle annonce ;</item>
/// <item>sans marque, un fichier qui est de l'UTF-8 VALIDE se lit en UTF-8 — sauf s'il
/// y donne un caractère de commande C1 (U+0080 à U+009F), voir
/// <see cref="Utf8Vraisemblable"/>. Un texte Latin-1 qui porte un accent n'est presque
/// jamais de l'UTF-8 valide : « é » s'y écrit E9, qui ouvre en UTF-8 une séquence de
/// trois octets que la lettre suivante, ASCII, ne continue pas ;</item>
/// <item>sinon, c'est du Latin-1 — lu comme Windows-1252, voir
/// <see cref="Windows1252"/>.</item>
/// </list>
///
/// **Ce qui reste deviné de travers** : un fichier Latin-1 dont TOUS les octets
/// accentués forment par hasard des séquences UTF-8 valides se lit en UTF-8. Il faut
/// pour cela qu'aucun accent du fichier ne soit suivi d'une lettre ASCII ou d'un blanc
/// ordinaire : en pratique un texte court, en capitales, où une capitale accentuée
/// touche une ponctuation de la plage A0 à BF — « CAFÉ » suivi d'une espace
/// insécable s'écrit C9 A0, qui est « ɠ » en UTF-8. Un seul « é » ordinaire ailleurs
/// dans le fichier le fait relire en entier en Latin-1.
///
/// Puis le refus (<see cref="TexteRefuseException"/>) : un fichier vide, ou sans autre
/// texte qu'une fin de ligne, effacerait le bloc ; un fichier qui porte un caractère de
/// commande — hors tabulation, saut de ligne, retour chariot et saut de page — n'est pas
/// du texte, et ses octets nuls ou ses codes d'échappement entreraient dans le bloc,
/// puis dans le document enregistré. Un binaire et un fichier UTF-16 sans marque en
/// portent tous deux : l'UTF-16 écrit l'ASCII avec un octet nul sur deux.
///
/// Puis les fins de ligne : le bloc compte « \r\n » pour un seul saut, comme la
/// composition (voir <see cref="Models.Compositeur"/>), mais la zone de saisie, elle,
/// verrait deux caractères là où l'utilisateur n'en voit qu'un. Tout saut devient donc
/// « \n », celui de Windows comme le « \r » seul des anciens Mac.
/// </summary>
public static class TexteImporte
{
    private static readonly byte[] MarqueUtf8 = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// UTF-8 strict : une séquence invalide lève au lieu de devenir « � ». C'est ce qui
    /// permet de reconnaître un fichier qui n'en est pas.
    /// </summary>
    private static readonly UTF8Encoding Utf8Strict = new(encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>
    /// Le Latin-1 lu comme Windows-1252, son sur-ensemble. Les deux ne diffèrent que sur
    /// les octets 80 à 9F : des caractères de commande en Latin-1, que nul texte
    /// n'emploie ; « ’ », « œ », « € », « … » en Windows-1252, qu'un texte français
    /// écrit sous Windows emploie partout. C'est aussi ce que fait tout navigateur d'un
    /// document déclaré « iso-8859-1 » (WHATWG Encoding Standard). Un fichier Latin-1 se
    /// lit donc à l'identique, et un fichier Windows garde ses apostrophes.
    ///
    /// Le fournisseur est pris directement plutôt qu'inscrit auprès d'<see cref="Encoding"/> :
    /// l'inscription change l'état de tout le processus pour le seul besoin de cette
    /// classe.
    /// </summary>
    private static readonly Encoding Windows1252 =
        CodePagesEncodingProvider.Instance.GetEncoding(1252)
        ?? throw new InvalidOperationException("Windows-1252 est introuvable.");

    private static readonly byte[] MarqueUtf16Petit = [0xFF, 0xFE];
    private static readonly byte[] MarqueUtf16Gros = [0xFE, 0xFF];

    /// <summary>UTF-16 strict : un octet orphelin ou une demi-paire lèvent.</summary>
    private static readonly UnicodeEncoding Utf16PetitStrict =
        new(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);

    private static readonly UnicodeEncoding Utf16GrosStrict =
        new(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Le plus gros fichier qu'un bloc reçoive : 128 Kio, soit environ 125 000 signes d'un texte
    /// français en UTF-8 — une soixantaine de pages, bien plus qu'un bloc n'en montre.
    /// Au-delà, l'écran gèle : mesuré dans la fenêtre, le premier rendu après l'import
    /// prend 0,8 s à 128 Kio, 1,5 s à 256 Kio, 3,2 s à 512 Kio et 6,6 s à 1 Mio, et la
    /// relecture du poste a vu 10 Mo monter à près de 7 Go de mémoire. Le fichier est
    /// refusé avant d'être lu.
    /// </summary>
    public const long TailleMax = 128 * 1024;

    /// <summary>
    /// Lit le fichier désigné. Les erreurs du disque remontent telles quelles ; un
    /// fichier trop gros, vide ou qui n'est pas du texte lève
    /// <see cref="TexteRefuseException"/>.
    /// </summary>
    public static TexteLu Lire(string chemin)
    {
        using var flux = File.OpenRead(chemin);
        if (flux.Length > TailleMax)
        {
            throw new TexteRefuseException(
                $"le fichier fait {Kio(flux.Length)} Kio, et un bloc en reçoit au plus {Kio(TailleMax)} Kio.");
        }

        var octets = new byte[flux.Length];
        flux.ReadExactly(octets);
        return Decoder(octets);
    }

    private static long Kio(long octets) => (octets + 1023) / 1024;

    /// <summary>
    /// Les caractères que portent ces octets, et l'encodage qui les a donnés. Lève
    /// <see cref="TexteRefuseException"/> si ces octets ne font pas un texte à verser
    /// dans un bloc.
    /// </summary>
    public static TexteLu Decoder(ReadOnlySpan<byte> octets)
    {
        if (octets.IsEmpty) throw new TexteRefuseException("le fichier est vide.");

        string texte;
        string encodage;

        if (octets.StartsWith(MarqueUtf8))
        {
            // La marque est une déclaration explicite : on la croit, et une séquence
            // invalide derrière elle devient « � » plutôt que de faire relire tout le
            // fichier — marque comprise — en Latin-1.
            texte = Encoding.UTF8.GetString(octets[MarqueUtf8.Length..]);
            encodage = "UTF-8 avec BOM";
        }
        else if (octets.StartsWith(MarqueUtf16Petit) || octets.StartsWith(MarqueUtf16Gros))
        {
            var petit = octets[0] == 0xFF;
            try
            {
                texte = (petit ? Utf16PetitStrict : Utf16GrosStrict).GetString(octets[2..]);
            }
            catch (DecoderFallbackException)
            {
                throw new TexteRefuseException(
                    "le fichier s'annonce en UTF-16 mais n'en est pas : une moitié de caractère y reste seule.");
            }

            encodage = petit ? "UTF-16 LE" : "UTF-16 BE";
        }
        else if (Utf8Vraisemblable(octets) is { } utf8)
        {
            texte = utf8;
            encodage = "UTF-8";
        }
        else
        {
            texte = Windows1252.GetString(octets);
            encodage = "Latin-1";
        }

        texte = Normaliser(texte);
        Verifier(texte);
        return new TexteLu(texte, encodage);
    }

    /// <summary>
    /// Ces octets lus en UTF-8 strict, ou rien s'ils n'en sont pas. De l'UTF-8 valide qui
    /// donne un caractère de commande C1 (U+0080 à U+009F, écrits C2 80 à C2 9F) n'en est
    /// pas non plus : nul texte n'emploie ces caractères, alors que « Â » suivi de « ’ »,
    /// « € » ou « œ » s'écrit justement C2 92, C2 80, C2 9C en Windows-1252.
    /// </summary>
    private static string? Utf8Vraisemblable(ReadOnlySpan<byte> octets)
    {
        string texte;
        try
        {
            texte = Utf8Strict.GetString(octets);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        foreach (var c in texte)
        {
            if (c is >= '\u0080' and <= '\u009F') return null;
        }

        return texte;
    }

    /// <summary>
    /// Refuse un texte vide, ou qui porte un caractère de commande autre que la
    /// tabulation, le saut de ligne, le retour chariot et le saut de page.
    /// </summary>
    private static void Verifier(string texte)
    {
        if (texte.Length == 0)
        {
            throw new TexteRefuseException("le fichier ne contient aucun texte.");
        }

        for (var i = 0; i < texte.Length; i++)
        {
            var c = texte[i];
            if (!char.IsControl(c) || c is '\t' or '\n' or '\r' or '\f') continue;

            throw new TexteRefuseException(c == '\0'
                ? $"ce n'est pas un fichier texte : il porte un octet nul (signe {i + 1}). Un fichier binaire, ou de l'UTF-16 sans marque d'ordre d'octets ?"
                : $"ce n'est pas un fichier texte : il porte le caractère de commande U+{(int)c:X4} (signe {i + 1}).");
        }
    }

    /// <summary>
    /// Les fins de ligne ramenées à « \n », puis le dernier saut retiré s'il clôt le
    /// texte.
    ///
    /// Ce dernier saut n'est pas un paragraphe : un fichier texte finit d'ordinaire par
    /// une fin de ligne, que tout éditeur ajoute sans qu'on la voie. Gardée, elle
    /// ouvrirait un paragraphe vide en bas du bloc, qui compte dans sa hauteur — la
    /// « ligne fantôme » que la recette de A2 a trouvée dans l'import des <c>.pub</c>
    /// (poste R5), et qui faisait signaler des débordements qui n'existaient pas. Un
    /// seul saut est retiré : une ligne vide voulue, en fin de fichier, en laisse un.
    /// </summary>
    public static string Normaliser(string texte)
    {
        var lignes = texte.Replace("\r\n", "\n").Replace('\r', '\n');
        return lignes.EndsWith('\n') ? lignes[..^1] : lignes;
    }
}
