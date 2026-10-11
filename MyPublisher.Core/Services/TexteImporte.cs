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
/// Lire un fichier texte comme un bloc l'attend. Tout est pur ici, sauf
/// <see cref="Lire"/>, qui ne fait que prendre les octets du disque : le décodage
/// s'éprouve sur des octets écrits dans le test, sans fichier.
///
/// Un fichier texte ne dit pas son encodage. On le devine, dans cet ordre :
/// <list type="number">
/// <item>une marque d'ordre d'octets UTF-8 (EF BB BF) le dit : elle est retirée, et le
/// reste se lit en UTF-8 ;</item>
/// <item>sans marque, un fichier qui est de l'UTF-8 VALIDE se lit en UTF-8. Un texte
/// Latin-1 qui porte un accent ne l'est presque jamais : « é » s'y écrit E9, qui ouvre
/// en UTF-8 une séquence de trois octets que la lettre suivante, ASCII, ne continue
/// pas ;</item>
/// <item>sinon, c'est du Latin-1 — lu comme Windows-1252, voir
/// <see cref="Windows1252"/>.</item>
/// </list>
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

    /// <summary>Lit le fichier désigné ; les erreurs du disque remontent telles quelles.</summary>
    public static TexteLu Lire(string chemin) => Decoder(File.ReadAllBytes(chemin));

    /// <summary>Les caractères que portent ces octets, et l'encodage qui les a donnés.</summary>
    public static TexteLu Decoder(ReadOnlySpan<byte> octets)
    {
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
        else
        {
            try
            {
                texte = Utf8Strict.GetString(octets);
                encodage = "UTF-8";
            }
            catch (DecoderFallbackException)
            {
                texte = Windows1252.GetString(octets);
                encodage = "Latin-1";
            }
        }

        return new TexteLu(Normaliser(texte), encodage);
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
