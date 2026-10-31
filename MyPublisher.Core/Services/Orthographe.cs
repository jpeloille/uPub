using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MyPublisher.Models;
using WeCantSpell.Hunspell;

namespace MyPublisher.Services;

/// <summary>
/// Un mot que le dictionnaire de sa langue ne connaît pas : sa place dans le texte, le mot tel
/// qu'il est écrit, et la langue où on l'a cherché (poste N25).
/// </summary>
public readonly record struct FauteDOrthographe(int Debut, int Longueur, string Mot, string Langue)
{
    /// <summary>La position qui suit le mot.</summary>
    public int Fin => Debut + Longueur;

    /// <summary>La plage du mot, au format des fragments.</summary>
    public Plage Plage => new(Debut, Longueur);
}

/// <summary>
/// La vérification orthographique (poste N25 ; fiche 23, a26 ; décision 6 de la phase N). Publisher
/// souligne les fautes pendant la frappe, les propose au clic droit et les parcourt par <c>F7</c> ;
/// ce service du noyau dit lesquelles, sans rien savoir de l'écran.
///
/// **Les dictionnaires sont ceux du système**, au format de Hunspell (<c>.dic</c> et <c>.aff</c>),
/// lus par WeCantSpell.Hunspell, un portage de Hunspell en code géré, sans bibliothèque native.
/// Un dictionnaire se charge à la première demande de sa langue, une fois. Une langue de N9 — une
/// étiquette BCP 47 — mène à son fichier par son nom : « fr-FR » cherche <c>fr_FR</c>, puis
/// <c>fr</c> ; « en-US », <c>en_US</c>, puis <c>en</c> (<see cref="NomsDeFichier"/>). Une langue
/// sans dictionnaire le dit une fois (<see cref="DictionnaireAbsent"/>), et rien n'y est souligné :
/// on ne signale pas comme fautif un texte qu'on ne sait pas lire.
///
/// **Mot par mot, avec un cache par mot.** Le texte se découpe en mots (<see cref="Mots"/>) : une
/// suite de lettres, de chiffres et de marques, qu'une apostrophe ou un trait d'union entre deux
/// lettres ne coupe pas — « l'avion », « porte-avions » —, et que Hunspell juge en entier, élision
/// et composés compris. Chaque mot n'est jugé qu'une fois par langue : un texte retapé ne coûte que
/// ses mots neufs. Comme Office par défaut, un mot qui porte un chiffre, ou tout en capitales, n'est
/// pas vérifié.
///
/// **Ce que l'utilisateur ajoute** : <see cref="IgnorerPartout"/>, le temps de la session ;
/// <see cref="Ajouter"/>, le dictionnaire de l'utilisateur, par langue, que le modèle de vue garde
/// dans les préférences (N11) et rend à chaque session.
///
/// Un service construit sans dossier n'a aucun dictionnaire : c'est celui des tests qui ne parlent
/// pas d'orthographe. Les tests du poste lisent le dictionnaire écrit pour eux, dans
/// <c>Scripts/fixtures/orthographe/</c>, jamais celui de la machine.
/// </summary>
public sealed class Orthographe
{
    /// <summary>Où le système range les dictionnaires de Hunspell (paquets <c>hunspell-fr</c>, <c>hunspell-en-us</c>…).</summary>
    public const string DossierDuSysteme = "/usr/share/hunspell";

    /// <summary>Le nombre de suggestions qu'on propose au plus, comme le menu de Publisher.</summary>
    public const int SuggestionsAuPlus = 6;

    private readonly IReadOnlyList<string> _dossiers;

    /// <summary>Les dictionnaires chargés, par langue ; <c>null</c> pour une langue qui n'en a pas.</summary>
    private readonly Dictionary<string, WordList?> _dictionnaires = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Le jugement de chaque mot déjà vu, par langue : juste ou non.</summary>
    private readonly Dictionary<(string Langue, string Mot), bool> _cache = [];

    /// <summary>Les mots que l'utilisateur a ajoutés, par langue.</summary>
    private readonly Dictionary<string, HashSet<string>> _ajoutes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Les mots ignorés partout, le temps de la session, par langue.</summary>
    private readonly Dictionary<string, HashSet<string>> _ignores = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="dossiers">Les dossiers où chercher les dictionnaires, dans l'ordre ; aucun, aucun dictionnaire.</param>
    public Orthographe(params string[] dossiers)
    {
        _dossiers = dossiers.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
    }

    /// <summary>Le service sur les dictionnaires du système, ceux de <see cref="DossierDuSysteme"/>.</summary>
    public static Orthographe DuSysteme() => new(DossierDuSysteme);

    /// <summary>Les dossiers où le service cherche ses dictionnaires.</summary>
    public IReadOnlyList<string> Dossiers => _dossiers;

    /// <summary>
    /// Une langue n'a pas de dictionnaire : levé une fois par langue, à la première demande. Rien
    /// n'est souligné dans cette langue.
    /// </summary>
    public event Action<string>? DictionnaireAbsent;

    /// <summary>
    /// Le nombre de fois qu'un mot a été soumis à un dictionnaire : un mot déjà jugé ne l'est
    /// plus, il est lu dans le cache. De quoi vérifier que le cache sert.
    /// </summary>
    public int MotsJuges { get; private set; }

    /// <summary>Les langues demandées qui n'ont pas de dictionnaire.</summary>
    public IReadOnlyList<string> LanguesSansDictionnaire =>
        _dictionnaires.Where(d => d.Value is null).Select(d => d.Key).ToList();

    // ------------------------------------------------------------------ les dictionnaires

    /// <summary>
    /// Les noms de fichier qu'une étiquette BCP 47 désigne, du plus précis au plus large :
    /// « fr-FR » → <c>fr_FR</c>, <c>fr</c> ; « sr-Latn-RS » → <c>sr_Latn_RS</c>, <c>sr_RS</c>,
    /// <c>sr</c>. La langue s'écrit en minuscules et la région en capitales, comme les fichiers.
    /// </summary>
    public static IReadOnlyList<string> NomsDeFichier(string langue)
    {
        var parties = (langue ?? "").Split('-', '_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parties.Length == 0) return [];

        var normees = parties.Select((p, i) => i == 0 ? p.ToLowerInvariant()
                : p.Length == 2 ? p.ToUpperInvariant()
                : p.Length == 4 ? char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()
                : p).ToList();

        var noms = new List<string> { string.Join('_', normees) };
        var region = normees.Skip(1).LastOrDefault(p => p.Length == 2 || p.All(char.IsDigit));
        if (region is not null) noms.Add($"{normees[0]}_{region}");
        noms.Add(normees[0]);
        return noms.Distinct().ToList();
    }

    /// <summary>Le fichier <c>.dic</c> de la langue, s'il y en a un avec son <c>.aff</c>, ou rien.</summary>
    public string? FichierDe(string langue)
    {
        foreach (var nom in NomsDeFichier(langue))
        {
            foreach (var dossier in _dossiers)
            {
                var dic = Path.Combine(dossier, nom + ".dic");
                if (File.Exists(dic) && File.Exists(Path.ChangeExtension(dic, ".aff"))) return dic;
            }
        }

        return null;
    }

    /// <summary>La langue a-t-elle un dictionnaire ? Le charge s'il ne l'est pas.</summary>
    public bool Connait(string langue) => Dictionnaire(langue) is not null;

    /// <summary>
    /// Le dictionnaire de la langue, chargé à la première demande ; <c>null</c> s'il n'y en a pas,
    /// ce que <see cref="DictionnaireAbsent"/> dit une fois. Un fichier illisible vaut un
    /// dictionnaire absent : il ne doit pas empêcher d'écrire.
    /// </summary>
    private WordList? Dictionnaire(string langue)
    {
        if (_dictionnaires.TryGetValue(langue, out var connu)) return connu;

        WordList? dictionnaire = null;
        if (FichierDe(langue) is { } fichier)
        {
            try
            {
                dictionnaire = WordList.CreateFromFiles(fichier);
            }
            catch (Exception)
            {
                dictionnaire = null;
            }
        }

        _dictionnaires[langue] = dictionnaire;
        if (dictionnaire is null)
        {
            DictionnaireAbsent?.Invoke(langue);
            return null;
        }

        if (_ajoutes.TryGetValue(langue, out var ajoutes))
        {
            foreach (var mot in ajoutes) dictionnaire.Add(mot);
        }

        return dictionnaire;
    }

    // ------------------------------------------------------------------ les mots

    /// <summary>
    /// Les mots du texte, de gauche à droite : une suite de lettres, de chiffres et de marques ;
    /// une apostrophe — droite ou typographique — ou un trait d'union entre deux lettres reste
    /// dans le mot. Tout le reste sépare : espaces, ponctuation, sauts.
    /// </summary>
    public static IEnumerable<Plage> Mots(string texte)
    {
        if (string.IsNullOrEmpty(texte)) yield break;

        var i = 0;
        while (i < texte.Length)
        {
            if (!EstDuMot(texte[i]))
            {
                i++;
                continue;
            }

            var debut = i;
            while (i < texte.Length)
            {
                if (EstDuMot(texte[i]) || EstUneMarque(texte[i]))
                {
                    i++;
                }
                else if (EstUnJoint(texte[i]) && i + 1 < texte.Length && char.IsLetter(texte[i + 1]) && char.IsLetter(texte[i - 1]))
                {
                    i++;
                }
                else
                {
                    break;
                }
            }

            yield return new Plage(debut, i - debut);
        }
    }

    private static bool EstDuMot(char c) => char.IsLetterOrDigit(c);

    private static bool EstUneMarque(char c) =>
        CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;

    private static bool EstUnJoint(char c) => c is '\'' or '’' or '-';

    /// <summary>
    /// Un mot qu'on ne vérifie pas, comme Office par défaut : il porte un chiffre — « A320 »,
    /// « 1er » —, ou il est tout en capitales — un sigle, « ATR ».
    /// </summary>
    public static bool HorsVerification(string mot)
    {
        if (mot.Any(char.IsDigit)) return true;

        var lettres = mot.Where(char.IsLetter).ToList();
        return lettres.Count >= 2 && lettres.All(char.IsUpper);
    }

    /// <summary>
    /// Le mot est-il juste dans cette langue ? Vrai pour un mot ignoré ou ajouté, pour un mot
    /// hors vérification, et dans une langue sans dictionnaire. Jugé une fois par langue.
    /// </summary>
    public bool EstJuste(string mot, string langue)
    {
        if (string.IsNullOrEmpty(mot) || HorsVerification(mot)) return true;
        if (_ignores.TryGetValue(langue, out var ignores) && ignores.Contains(mot)) return true;
        if (_cache.TryGetValue((langue, mot), out var juge)) return juge;
        if (Dictionnaire(langue) is not { } dictionnaire) return true;

        MotsJuges++;
        var juste = dictionnaire.Check(mot) ||
                    (_ajoutes.TryGetValue(langue, out var ajoutes) && ajoutes.Contains(mot));
        _cache[(langue, mot)] = juste;
        return juste;
    }

    /// <summary>
    /// Les fautes d'un texte, de gauche à droite. <paramref name="langueDe"/> donne la langue du
    /// caractère à une position : celle du premier caractère d'un mot est la sienne. Une abréviation
    /// que le dictionnaire écrit avec son point — « etc. » — n'est pas une faute.
    /// </summary>
    public IReadOnlyList<FauteDOrthographe> Fautes(string texte, Func<int, string> langueDe)
    {
        var fautes = new List<FauteDOrthographe>();
        foreach (var plage in Mots(texte))
        {
            var mot = texte.Substring(plage.Start, plage.Length);
            var langue = langueDe(plage.Start);
            if (EstJuste(mot, langue)) continue;

            var fin = plage.Start + plage.Length;
            if (fin < texte.Length && texte[fin] == '.' && EstJuste(mot + ".", langue)) continue;

            fautes.Add(new FauteDOrthographe(plage.Start, plage.Length, mot, langue));
        }

        return fautes;
    }

    /// <summary>Les fautes d'un texte tout entier dans une langue.</summary>
    public IReadOnlyList<FauteDOrthographe> Fautes(string texte, string langue) => Fautes(texte, _ => langue);

    /// <summary>
    /// Les fautes d'un bloc de texte, chaque mot dans la langue de son premier caractère (N9) : son
    /// fragment, sinon le bloc, sinon le document. Rien pour un bloc à champ, dont le texte est caché.
    /// </summary>
    public IReadOnlyList<FauteDOrthographe> Fautes(TextFrame bloc, Document document) =>
        bloc.HasField ? [] : Fautes(bloc.Text ?? "", position => Langues.DuCaractere(bloc, position, document));

    /// <summary>Les fautes d'une case de tableau, chaque mot dans la langue de son premier caractère.</summary>
    public IReadOnlyList<FauteDOrthographe> Fautes(TableCell cellule, Document document) =>
        Fautes(cellule.Text ?? "", position => Langues.DuCaractere(cellule, position, document));

    /// <summary>
    /// Ce que le dictionnaire propose à la place du mot, les plus proches d'abord,
    /// <see cref="SuggestionsAuPlus"/> au plus ; rien dans une langue sans dictionnaire.
    /// </summary>
    public IReadOnlyList<string> Suggestions(string mot, string langue)
    {
        if (string.IsNullOrEmpty(mot) || Dictionnaire(langue) is not { } dictionnaire) return [];

        return dictionnaire.Suggest(mot).Where(s => s != mot).Distinct().Take(SuggestionsAuPlus).ToList();
    }

    // ------------------------------------------------------------------ ce que l'utilisateur ajoute

    /// <summary>
    /// <i>Ignorer tout</i> : le mot n'est plus une faute dans cette langue, jusqu'à la fin de la
    /// session. Rien ne s'enregistre.
    /// </summary>
    public void IgnorerPartout(string mot, string langue)
    {
        if (string.IsNullOrEmpty(mot)) return;
        if (!_ignores.TryGetValue(langue, out var ignores)) _ignores[langue] = ignores = new HashSet<string>(StringComparer.Ordinal);
        ignores.Add(mot);
    }

    /// <summary>
    /// <i>Ajouter au dictionnaire</i> : le mot entre au dictionnaire de l'utilisateur pour cette
    /// langue — et dans le dictionnaire chargé, qui en accepte alors les formes capitalisées comme
    /// Hunspell les accepte. Le modèle de vue l'écrit dans les préférences.
    /// </summary>
    public void Ajouter(string mot, string langue)
    {
        if (string.IsNullOrWhiteSpace(mot)) return;
        if (!_ajoutes.TryGetValue(langue, out var ajoutes)) _ajoutes[langue] = ajoutes = new HashSet<string>(StringComparer.Ordinal);
        if (!ajoutes.Add(mot)) return;

        if (_dictionnaires.TryGetValue(langue, out var dictionnaire) && dictionnaire is not null) dictionnaire.Add(mot);
        foreach (var cle in _cache.Keys.Where(cle => string.Equals(cle.Langue, langue, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _cache.Remove(cle);
        }
    }
}
