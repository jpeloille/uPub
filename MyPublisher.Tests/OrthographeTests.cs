using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// La vérification orthographique — poste N25, <c>Services/Orthographe.cs</c> : le service du
/// noyau qui rend les fautes d'un texte dans une langue, mot par mot, avec un cache par mot ; et
/// ce que les préférences (N11) en gardent — « Vérifier pendant la frappe » et le dictionnaire de
/// l'utilisateur, par langue.
///
/// Les dictionnaires sont ceux qui sont écrits pour ces tests, dans
/// <c>Scripts/fixtures/orthographe/</c> : jamais ceux du système, qu'une machine a et qu'une
/// autre n'a pas.
/// </summary>
[Trait("poste", "N25")]
public sealed class OrthographeTests : IDisposable
{
    private readonly string _dossier = Path.Combine(Path.GetTempPath(), "n25-orthographe-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dossier)) Directory.Delete(_dossier, recursive: true);
    }

    /// <summary>Le dossier des dictionnaires de test, à la racine du dépôt.</summary>
    private static string Dictionnaires()
    {
        var dossier = new DirectoryInfo(AppContext.BaseDirectory);
        while (dossier is not null && !File.Exists(Path.Combine(dossier.FullName, "MyPublisher.sln"))) dossier = dossier.Parent;
        Assert.NotNull(dossier);
        return Path.Combine(dossier.FullName, "Scripts", "fixtures", "orthographe");
    }

    private static Orthographe Service() => new(Dictionnaires());

    private static string[] MotsDe(string texte) =>
        Orthographe.Mots(texte).Select(p => texte.Substring(p.Start, p.Length)).ToArray();

    [Fact]
    public void Une_langue_mene_a_son_fichier_puis_a_sa_langue_seule()
    {
        Assert.Equal(new[] { "fr_FR", "fr" }, Orthographe.NomsDeFichier("fr-FR"));
        Assert.Equal(new[] { "en_US", "en" }, Orthographe.NomsDeFichier("en-US"));
        Assert.Equal(new[] { "en_US", "en" }, Orthographe.NomsDeFichier("EN-us"));
        Assert.Equal(new[] { "sr_Latn_RS", "sr_RS", "sr" }, Orthographe.NomsDeFichier("sr-Latn-RS"));
        Assert.Equal(new[] { "de" }, Orthographe.NomsDeFichier("de"));
        Assert.Empty(Orthographe.NomsDeFichier(""));

        var service = Service();
        Assert.Equal(Path.Combine(Dictionnaires(), "en_US.dic"), service.FichierDe("en-US"));
        // Pas de fr_FR parmi les dictionnaires de test : « fr-FR » se replie sur « fr ».
        Assert.Equal(Path.Combine(Dictionnaires(), "fr.dic"), service.FichierDe("fr-FR"));
        Assert.Null(service.FichierDe("de-DE"));
    }

    [Fact]
    public void Les_mots_gardent_l_apostrophe_et_le_trait_d_union_entre_deux_lettres()
    {
        Assert.Equal(new[] { "l'avion", "porte-avions", "aujourd’hui", "A320", "fin" },
            MotsDe("l'avion, porte-avions — aujourd’hui ; A320 fin."));
        Assert.Equal(new[] { "mot", "suivant" }, MotsDe("« mot- » -suivant'"));
        Assert.Equal(new[] { "un", "deux", "trois" }, MotsDe("un\tdeux\u2028trois\n"));
        Assert.Empty(MotsDe(""));
    }

    /// <summary>La recette du plan : « recieve » est une faute, « receive » non.</summary>
    [Fact]
    public void Recieve_est_une_faute_et_receive_non()
    {
        var service = Service();
        const string texte = "We will recieve the letter, and receive it.";

        var fautes = service.Fautes(texte, "en-US");

        var faute = Assert.Single(fautes);
        Assert.Equal(new FauteDOrthographe(8, 7, "recieve", "en-US"), faute);
        Assert.Equal("recieve", texte[faute.Debut..faute.Fin]);
        Assert.True(service.EstJuste("receive", "en-US"));
        Assert.True(service.EstJuste("Receive", "en-US"), "la capitale du début de phrase");
        Assert.True(service.EstJuste("received", "en-US"), "les affixes du dictionnaire");
        Assert.Equal("receive", service.Suggestions("recieve", "en-US").First());
    }

    [Fact]
    public void Un_sigle_et_un_mot_a_chiffre_ne_sont_pas_verifies()
    {
        var service = Service();

        Assert.Empty(service.Fautes("ATR A320 1st", "en-US"));
        Assert.Single(service.Fautes("Atr", "en-US"));
    }

    [Fact]
    public void Chaque_mot_n_est_juge_qu_une_fois_par_langue()
    {
        var service = Service();

        service.Fautes("the letter the letter the", "en-US");
        Assert.Equal(2, service.MotsJuges);

        service.Fautes("the letter, then the letter", "en-US");
        Assert.Equal(3, service.MotsJuges);

        // Dans une autre langue, le même mot se juge de nouveau.
        service.Fautes("le", "fr-FR");
        service.Fautes("le", "en-US");
        Assert.Equal(5, service.MotsJuges);
    }

    /// <summary>
    /// Un dictionnaire absent le dit une fois, et rien n'est souligné dans cette langue — ni
    /// ailleurs, la langue voisine restant vérifiée.
    /// </summary>
    [Fact]
    public void Un_dictionnaire_absent_le_dit_une_fois_et_rien_n_est_souligne()
    {
        var service = Service();
        var absentes = new List<string>();
        service.DictionnaireAbsent += absentes.Add;

        Assert.Empty(service.Fautes("Ein falsches Wortt", "de-DE"));
        Assert.Empty(service.Fautes("Noch ein Wortt", "de-DE"));
        Assert.False(service.Connait("de-DE"));
        Assert.Single(service.Fautes("wortt", "en-US"));

        Assert.Equal(new[] { "de-DE" }, absentes);
        Assert.Equal(new[] { "de-DE" }, service.LanguesSansDictionnaire);
        Assert.Empty(service.Suggestions("Wortt", "de-DE"));

        // Un service sans dossier n'a aucun dictionnaire : celui des tests qui ne parlent pas d'orthographe.
        Assert.Empty(new Orthographe().Fautes("recieve", "en-US"));
    }

    /// <summary>Chaque mot d'un bloc est jugé dans la langue de son premier caractère : fragment, bloc, document (N9).</summary>
    [Fact]
    public void Chaque_mot_d_un_bloc_est_juge_dans_sa_langue()
    {
        var document = Document.CreateDefault();
        Assert.Equal("fr-FR", document.Langue);
        var bloc = new TextFrame { Text = "le texte recieve the mott" };
        bloc.Spans.Add(new StyleSpan { Start = 9, Length = 11, Langue = "en-US" });

        var fautes = Orthographe.Mots(bloc.Text).ToList();
        Assert.Equal(5, fautes.Count);

        var service = Service();
        Assert.Equal(new[] { ("recieve", "en-US"), ("mott", "fr-FR") },
            service.Fautes(bloc, document).Select(f => (f.Mot, f.Langue)).ToArray());

        bloc.Langue = "en-US";
        Assert.Equal(new[] { "le", "texte", "recieve", "mott" }, service.Fautes(bloc, document).Select(f => f.Mot).ToArray());

        // Un bloc à champ cache son texte : rien à vérifier.
        bloc.Field = FieldKind.Date;
        Assert.Empty(service.Fautes(bloc, document));

        var cellule = new TableCell { Text = "un avionn" };
        Assert.Equal("avionn", Assert.Single(service.Fautes(cellule, document)).Mot);
    }

    [Fact]
    public void Ignorer_tout_vaut_pour_la_session_et_ajouter_pour_la_langue()
    {
        var service = Service();
        Assert.Single(service.Fautes("frobnicate", "en-US"));

        service.IgnorerPartout("frobnicate", "en-US");
        Assert.Empty(service.Fautes("frobnicate", "en-US"));
        Assert.Single(service.Fautes("frobnicate", "fr-FR"));
        Assert.Single(new Orthographe(Dictionnaires()).Fautes("frobnicate", "en-US"));

        // Ajouté avant le chargement du dictionnaire, comme les préférences le rendent au démarrage.
        var neuf = Service();
        neuf.Ajouter("Nouméa", "fr-FR");
        Assert.Empty(neuf.Fautes("Nouméa", "fr-FR"));
        Assert.Single(neuf.Fautes("Nouméa", "en-US"));

        // Ajouté après qu'un jugement l'a mis au cache : le cache de la langue se vide.
        Assert.Single(neuf.Fautes("Koné", "fr-FR"));
        neuf.Ajouter("Koné", "fr-FR");
        Assert.Empty(neuf.Fautes("Koné", "fr-FR"));
    }

    // ------------------------------------------------------------------ les préférences

    [Fact]
    public void Le_dictionnaire_de_l_utilisateur_et_la_case_survivent_a_un_redemarrage()
    {
        var preferences = Preferences.Charger(_dossier);
        Assert.True(preferences.VerifierPendantLaFrappe, "ouvert d'emblée, comme dans Publisher");
        Assert.Empty(preferences.DictionnaireDeLUtilisateur);

        Assert.True(preferences.AjouterAuDictionnaire("fr-FR", "Nouméa"));
        Assert.True(preferences.AjouterAuDictionnaire("fr-FR", "Koné"));
        Assert.False(preferences.AjouterAuDictionnaire("fr-FR", "Koné"));
        Assert.True(preferences.AjouterAuDictionnaire("en-US", "frobnicate"));
        preferences.VerifierPendantLaFrappe = false;
        preferences.Enregistrer();

        var relu = Preferences.Charger(_dossier);
        Assert.False(relu.VerifierPendantLaFrappe);
        Assert.Equal(new[] { "Koné", "Nouméa" }, relu.DictionnaireDeLUtilisateur["fr-FR"]);
        Assert.Equal(new[] { "frobnicate" }, relu.DictionnaireDeLUtilisateur["en-US"]);
    }

    [Fact]
    public void Un_dictionnaire_mal_ecrit_se_nettoie_et_une_cle_inconnue_survit()
    {
        Directory.CreateDirectory(_dossier);
        var chemin = Path.Combine(_dossier, Preferences.NomDuFichier);
        File.WriteAllText(chemin, """
            { "DictionnaireDeLUtilisateur": { "fr-FR": ["b", "a", "b", " "], "en-US": null },
              "Aimantation": { "AuxObjets": true } }
            """);

        var preferences = Preferences.Charger(_dossier);
        Assert.True(preferences.VerifierPendantLaFrappe);
        Assert.Equal(new[] { "a", "b" }, preferences.DictionnaireDeLUtilisateur["fr-FR"]);
        Assert.False(preferences.DictionnaireDeLUtilisateur.ContainsKey("en-US"));

        preferences.Enregistrer();
        var relu = JsonNode.Parse(File.ReadAllText(chemin))!;
        Assert.True(relu["Aimantation"]!["AuxObjets"]!.GetValue<bool>());
    }
}
