using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Le magasin des réglages de l'utilisateur — poste N11, <c>Services/Preferences.cs</c>, que A11
/// et N25 reprendront. Il garde les six derniers documents, sans doublon, le plus récent en
/// tête ; il se relit d'un redémarrage à l'autre ; un fichier absent ou illisible rend un magasin
/// vide ; une clé inconnue survit à la réécriture ; un magasin en mémoire n'écrit nulle part ; et
/// le dossier se désigne par paramètre ou par variable d'environnement. Chaque test travaille
/// dans un dossier temporaire : jamais dans la configuration de la personne.
/// </summary>
[Trait("poste", "N11")]
public sealed class PreferencesTests : IDisposable
{
    private readonly string _dossier = Path.Combine(Path.GetTempPath(), "n11-preferences-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dossier)) Directory.Delete(_dossier, recursive: true);
    }

    private string Document(int i) => Path.Combine(_dossier, $"doc-{i}.mypub");

    [Fact]
    public void Un_dossier_vide_rend_un_magasin_vide_qui_s_ecrira_la()
    {
        var preferences = Preferences.Charger(_dossier);

        Assert.Empty(preferences.DocumentsRecents);
        Assert.Equal(Path.Combine(_dossier, "preferences.json"), preferences.Chemin);
        Assert.False(File.Exists(preferences.Chemin), "charger a écrit");

        preferences.Enregistrer();
        Assert.True(File.Exists(preferences.Chemin));
    }

    [Fact]
    public void Les_recents_gardent_six_documents_sans_doublon_le_plus_recent_en_tete()
    {
        var preferences = Preferences.Charger(_dossier);
        for (var i = 1; i <= 8; i++) preferences.RetenirLeDocument(Document(i));
        preferences.RetenirLeDocument(Document(5));
        // Un chemin relatif ou détourné désigne le même fichier : il ne se double pas.
        preferences.RetenirLeDocument(Path.Combine(_dossier, "sous", "..", "doc-7.mypub"));

        Assert.Equal(new[] { 7, 5, 8, 6, 4, 3 }.Select(Document), preferences.DocumentsRecents);
        Assert.Equal(Preferences.NombreDeRecents, preferences.DocumentsRecents.Count);
    }

    [Fact]
    public void Les_recents_survivent_a_un_redemarrage()
    {
        var preferences = Preferences.Charger(_dossier);
        foreach (var i in new[] { 1, 2, 3 }) preferences.RetenirLeDocument(Document(i));
        preferences.Enregistrer();

        var relues = Preferences.Charger(_dossier);
        Assert.Equal(new[] { 3, 2, 1 }.Select(Document), relues.DocumentsRecents);

        Assert.True(relues.OublierLeDocument(Document(2)));
        Assert.False(relues.OublierLeDocument(Document(9)));
        relues.Enregistrer();
        Assert.Equal(new[] { 3, 1 }.Select(Document), Preferences.Charger(_dossier).DocumentsRecents);
    }

    [Fact]
    public void Un_fichier_illisible_rend_un_magasin_vide_et_un_fichier_retouche_est_nettoye()
    {
        Directory.CreateDirectory(_dossier);
        var chemin = Path.Combine(_dossier, Preferences.NomDuFichier);

        File.WriteAllText(chemin, "{ ce n'est pas du JSON");
        Assert.Empty(Preferences.Charger(_dossier).DocumentsRecents);

        File.WriteAllText(chemin, """{ "DocumentsRecents": null }""");
        Assert.Empty(Preferences.Charger(_dossier).DocumentsRecents);

        // Retouché à la main : un doublon, une entrée vide, huit entrées. Relu comme il se garde.
        var huit = Enumerable.Range(1, 8).Select(Document).Prepend(Document(1)).Append("").ToArray();
        File.WriteAllText(chemin, new JsonObject { ["DocumentsRecents"] = new JsonArray(huit.Select(c => (JsonNode)c!).ToArray()) }.ToJsonString());
        Assert.Equal(Enumerable.Range(1, 6).Select(Document), Preferences.Charger(_dossier).DocumentsRecents);
    }

    /// <summary>A11 et N25 y rangeront leurs réglages : une version qui ne les connaît pas ne les efface pas.</summary>
    [Fact]
    public void Une_cle_inconnue_survit_a_la_reecriture()
    {
        Directory.CreateDirectory(_dossier);
        var chemin = Path.Combine(_dossier, Preferences.NomDuFichier);
        File.WriteAllText(chemin, """{ "Aimantation": { "AuxObjets": true }, "DocumentsRecents": [] }""");

        var preferences = Preferences.Charger(_dossier);
        preferences.RetenirLeDocument(Document(1));
        preferences.Enregistrer();

        var relu = JsonNode.Parse(File.ReadAllText(chemin))!;
        Assert.True(relu["Aimantation"]!["AuxObjets"]!.GetValue<bool>());
        Assert.Equal(Document(1), relu["DocumentsRecents"]![0]!.GetValue<string>());
        Assert.False(File.Exists(chemin + ".tmp"), "le fichier provisoire est resté");
    }

    [Fact]
    public void Un_magasin_en_memoire_n_ecrit_nulle_part()
    {
        var preferences = Preferences.EnMemoire();
        preferences.RetenirLeDocument(Document(1));
        preferences.Enregistrer();

        Assert.Null(preferences.Chemin);
        Assert.Equal(new[] { Document(1) }, preferences.DocumentsRecents);
        Assert.False(Directory.Exists(_dossier));
    }

    /// <summary>
    /// La variable désigne le dossier, à la place de la configuration de la personne : c'est
    /// elle que la recette pose. Sans elle, le dossier est celui des coordonnées.
    /// </summary>
    [Fact]
    public void La_variable_d_environnement_designe_le_dossier()
    {
        var avant = Environment.GetEnvironmentVariable(Preferences.VariableDuDossier);
        try
        {
            Environment.SetEnvironmentVariable(Preferences.VariableDuDossier, _dossier);
            Assert.Equal(_dossier, Preferences.DossierParDefaut);
            Assert.Equal(Path.Combine(_dossier, "preferences.json"), Preferences.Charger().Chemin);

            Environment.SetEnvironmentVariable(Preferences.VariableDuDossier, null);
            Assert.Equal(Path.GetDirectoryName(BusinessInfoStore.DefaultPath), Preferences.DossierParDefaut);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Preferences.VariableDuDossier, avant);
        }
    }
}
