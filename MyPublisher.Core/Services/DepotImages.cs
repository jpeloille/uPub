using System;
using System.IO;
using System.Security.Cryptography;

namespace MyPublisher.Services;

/// <summary>
/// Où un import range les images qu'il tire d'un document fermé.
///
/// C'est une interface, et c'est ce qui garde <see cref="PubImporter"/> pur : la
/// traduction du relevé ne touche jamais au disque, elle demande un chemin. Les tests
/// lui en donnent un faux et s'exécutent en quelques millisecondes, comme le poste R5 en
/// a pris l'habitude ; l'application lui donne celui-ci.
/// </summary>
public interface IDepotImages
{
    /// <summary>
    /// Range une image et rend son chemin, ou <c>null</c> si elle n'a pas pu l'être —
    /// l'import le dira alors plutôt que de laisser un bloc au fond vide.
    /// </summary>
    string? Deposer(byte[] donnees, string typeMime);
}

/// <summary>
/// Le dépôt réel : un fichier par image, nommé d'après son contenu, dans les données de
/// l'utilisateur.
///
/// **Pourquoi hors du dossier du document.** Le modèle référence les images par chemin ;
/// il faut donc bien en écrire une quelque part. Les écrire à côté du <c>.pub</c>
/// modifierait un dossier que personne n'a demandé de toucher — et, très concrètement,
/// salirait <c>MyPublisher/Books/</c> à chaque passage de la recette, qui importe les
/// deux documents réels. Les images d'un document importé ne sont pas de la
/// configuration : elles vont dans les données de l'utilisateur
/// (<c>~/.local/share/MyPublisher/images</c>), là où les coordonnées professionnelles
/// vont dans sa configuration.
///
/// **Pourquoi nommées d'après leur contenu.** Réimporter le même <c>.pub</c> rend alors
/// exactement le même document, au caractère près : sans cela, deux imports du même
/// fichier différeraient par des noms d'images, et l'aller-retour que le contrôle P2
/// exige cesserait d'être vérifiable sur un document importé. Une image déjà déposée
/// n'est pas réécrite.
///
/// **Le prix, et il est réel** : déplacer un document ne déplace pas ses images, et une
/// purge de ce dossier les perd. L'export le dit — « Image de remplissage introuvable » —
/// plutôt que de laisser un aplat vide. C'est le même compromis que les images liées des
/// blocs image, et c'est le poste B4 (archive <c>.mypubz</c>) qui le résoudra.
/// </summary>
public sealed class DepotImages : IDepotImages
{
    private readonly string _dossier;

    public DepotImages(string? dossier = null) => _dossier = dossier ?? DefaultPath;

    public static string DefaultPath
    {
        get
        {
            var donnees = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(donnees))
            {
                donnees = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local", "share");
            }

            return Path.Combine(donnees, "MyPublisher", "images");
        }
    }

    public string? Deposer(byte[] donnees, string typeMime)
    {
        if (donnees.Length == 0) return null;

        try
        {
            Directory.CreateDirectory(_dossier);

            var empreinte = Convert.ToHexString(SHA256.HashData(donnees))[..16].ToLowerInvariant();
            var chemin = Path.Combine(_dossier, empreinte + Extension(typeMime));

            // Déjà là : le contenu fait le nom, deux fichiers de même nom ont donc le
            // même contenu. Réécrire ne changerait rien et casserait la date du fichier.
            if (!File.Exists(chemin)) File.WriteAllBytes(chemin, donnees);
            return chemin;
        }
        catch (Exception)
        {
            // Un disque plein ou un dossier interdit ne doit pas faire échouer tout
            // l'import : l'appelant en fera un avertissement, comme d'une image perdue.
            return null;
        }
    }

    /// <summary>
    /// L'extension d'après le type MIME que le relevé annonce. Inconnu, on écrit
    /// <c>.img</c> : le fichier reste lisible par son contenu, et la liste des images
    /// d'un document ne dépend pas de ce qu'on a su nommer.
    /// </summary>
    private static string Extension(string typeMime) => typeMime switch
    {
        "image/png" => ".png",
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        "image/tiff" => ".tif",
        _ => ".img",
    };
}
