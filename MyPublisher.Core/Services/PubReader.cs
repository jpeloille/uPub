using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MyPublisher.Services;

/// <summary>
/// Fait lire un <c>.pub</c> par libmspub, et rend la suite d'appels de dessin qu'elle
/// en tire.
///
/// Pourquoi déléguer plutôt qu'écrire un lecteur. Le <c>.pub</c> est un format fermé :
/// un document composite OLE dont les formes vivent dans un flux propriétaire, les
/// styles dans des pages de propriétés à la façon de Word, et le texte dans un
/// troisième. Le rétro-concevoir serait un projet à lui seul, et libmspub le fait déjà
/// — c'est le lecteur qu'emploient LibreOffice et le reste du monde libre.
///
/// C'est le même choix que celui des polices, et pour la même raison : MyPublisher
/// délègue à <c>fc-match</c> ce que fontconfig sait mieux faire que lui. On délègue
/// ici à <c>pub2raw</c>, qui n'est pas un vidage de mise au point mais la sortie de
/// <c>RVNGRawDrawingGenerator</c> — la classe dont le rôle est précisément de
/// restituer les appels de l'interface de dessin, et que toute la famille libwpd
/// emploie. Son format est stable.
///
/// Installation : <c>apt install libmspub-tools</c>. La variable d'environnement
/// <c>MYPUBLISHER_PUB2RAW</c> permet d'en désigner un autre exemplaire.
/// </summary>
public static class PubReader
{
    /// <summary>Le programme à appeler, du PATH sauf indication contraire.</summary>
    public static string Programme =>
        Environment.GetEnvironmentVariable("MYPUBLISHER_PUB2RAW") is { Length: > 0 } choisi
            ? choisi
            : "pub2raw";

    /// <summary>
    /// Lit le document et rend le relevé d'appels. Lève si l'outil manque, en disant
    /// lequel et comment l'obtenir : un import qui échoue sans expliquer laisse
    /// l'utilisateur devant un format fermé et rien d'autre.
    /// </summary>
    public static string Relever(string chemin)
    {
        if (!File.Exists(chemin)) throw new FileNotFoundException("Document introuvable.", chemin);

        var demarrage = new ProcessStartInfo(Programme)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        demarrage.ArgumentList.Add(chemin);

        Process? processus;
        try
        {
            processus = Process.Start(demarrage);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"« {Programme} » est introuvable : MyPublisher délègue la lecture du format " +
                ".pub à libmspub. Installer « libmspub-tools », ou désigner le programme " +
                "par la variable MYPUBLISHER_PUB2RAW.", ex);
        }

        if (processus is null) throw new InvalidOperationException($"« {Programme} » n'a pas démarré.");

        using (processus)
        {
            // On lit les deux flux avant d'attendre : libmspub écrit ses avertissements
            // sur la sortie d'erreur, et un tampon plein bloquerait le processus.
            var releve = processus.StandardOutput.ReadToEnd();
            var erreurs = processus.StandardError.ReadToEnd();
            processus.WaitForExit();

            if (processus.ExitCode != 0 || releve.Length == 0)
            {
                throw new InvalidDataException(
                    $"Lecture impossible de « {Path.GetFileName(chemin)} » : " +
                    (erreurs.Length > 0 ? erreurs.Trim() : "le lecteur n'a rien rendu."));
            }

            return releve;
        }
    }

    /// <summary>Vrai si l'outil est disponible — de quoi désactiver proprement l'import.</summary>
    public static bool Disponible()
    {
        try
        {
            var demarrage = new ProcessStartInfo(Programme)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var processus = Process.Start(demarrage);
            if (processus is null) return false;
            processus.StandardOutput.ReadToEnd();
            processus.StandardError.ReadToEnd();
            processus.WaitForExit(5000);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
