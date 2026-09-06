using System.IO;
using System.Text.Json.Nodes;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Le format de fichier : l'aller-retour, et les migrations étape par étape.
///
/// « Scripts/controle.sh » vérifie déjà que les documents figés dans Scripts/fixtures/
/// se migrent en un point fixe. Il le vérifie **globalement** : si une étape de
/// migration cessait de faire son travail alors qu'une autre le compense, il ne
/// verrait rien. Ces tests-ci interrogent chaque étape séparément — et surtout, ils
/// s'exécutent en quelques millisecondes, là où le contrôle compile et exporte.
/// </summary>
public class FormatTests
{
    [Fact]
    public void Un_document_relu_et_reecrit_rend_le_meme_json()
    {
        // C'est l'étape 2 du contrôle, à ceci près qu'elle ne demande plus de
        // processus ni de fichier.
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Add(new TextFrame
        {
            Name = "Bloc", X = 10, Y = 20, Width = 100, Height = 50,
            Text = "L'unité, à 100 %", FontFamily = "DejaVu Sans", FontSize = 11,
        });

        var json = DocumentSerializer.ToJson(document);
        var relu = DocumentSerializer.FromJson(json);

        Assert.Equal(json, DocumentSerializer.ToJson(relu));
    }

    [Fact]
    public void Le_json_est_ecrit_en_clair()
    {
        // L'encodeur par défaut échappait tout caractère non-ASCII et l'apostrophe :
        // un document français sortait en « l'unité », contre la lisibilité
        // que le format revendique. Corrigé lors de P2 ; rien ne le retenait depuis.
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Add(new TextFrame { Text = "l'unité à 100 %" });

        var json = DocumentSerializer.ToJson(document);

        Assert.Contains("l'unité à 100 %", json);
        Assert.DoesNotContain("\\u00E9", json);
        Assert.DoesNotContain("\\u0027", json);
    }

    [Fact]
    public void La_version_1_renomme_Background_en_Fill()
    {
        var racine = JsonNode.Parse("""
            {
              "FormatVersion": 1,
              "Pages": [ { "Frames": [ { "$kind": "text", "Background": "#FF102030" } ] } ]
            }
            """)!.AsObject();

        FormatMigrations.Apply(racine);

        var bloc = racine["Pages"]![0]!["Frames"]![0]!;
        // La couleur se lit sous le remplissage uni que la version 4 a posé autour : les
        // deux étapes se composent, et c'est ce qu'un fichier de la version 1 traverse.
        Assert.Equal("#FF102030", bloc["Fill"]!["Color"]!.GetValue<string>());
        Assert.Null(bloc["Background"]);
        Assert.Equal(Document.CurrentFormatVersion, racine["FormatVersion"]!.GetValue<int>());
    }

    [Fact]
    public void La_version_2_eclate_la_marge_unique_en_quatre()
    {
        var racine = JsonNode.Parse("""
            {
              "FormatVersion": 2,
              "Pages": [ { "Frames": [ { "$kind": "text", "Padding": 7.5 } ] } ]
            }
            """)!.AsObject();

        FormatMigrations.Apply(racine);

        var bloc = racine["Pages"]![0]!["Frames"]![0]!;
        foreach (var cote in new[] { "PaddingLeft", "PaddingTop", "PaddingRight", "PaddingBottom" })
        {
            Assert.Equal(7.5, bloc[cote]!.GetValue<double>());
        }
    }

    [Fact]
    public void La_migration_descend_dans_les_groupes()
    {
        // Le piège : un groupe porte ses blocs en coordonnées relatives, et une
        // migration qui ne parcourrait que le premier niveau laisserait derrière
        // elle des blocs à l'ancienne forme, sans erreur.
        var racine = JsonNode.Parse("""
            {
              "FormatVersion": 1,
              "Pages": [ { "Frames": [
                { "$kind": "group", "Children": [
                  { "$kind": "text", "Background": "#FFAABBCC" } ] } ] } ]
            }
            """)!.AsObject();

        FormatMigrations.Apply(racine);

        var enfant = racine["Pages"]![0]!["Frames"]![0]!["Children"]![0]!;
        Assert.Equal("#FFAABBCC", enfant["Fill"]!["Color"]!.GetValue<string>());
    }

    [Fact]
    public void La_version_3_enveloppe_la_couleur_de_fond_dans_un_remplissage()
    {
        var racine = JsonNode.Parse("""
            {
              "FormatVersion": 3,
              "Pages": [ { "Frames": [ { "$kind": "shape", "Fill": "#FF9DC3E6" } ] } ]
            }
            """)!.AsObject();

        FormatMigrations.Apply(racine);

        var bloc = racine["Pages"]![0]!["Frames"]![0]!;
        Assert.Equal("solid", bloc["Fill"]!["$kind"]!.GetValue<string>());
        Assert.Equal("#FF9DC3E6", bloc["Fill"]!["Color"]!.GetValue<string>());
    }

    [Fact]
    public void La_migration_descend_dans_les_cases_dun_tableau()
    {
        // Le piège propre à cette étape-ci : une case n'est pas un bloc — elle n'a pas de
        // « $kind » — et un parcours qui ne suivrait que les blocs laisserait un fond de
        // case en chaîne derrière lui, que le modèle refuserait ensuite de relire.
        var racine = JsonNode.Parse("""
            {
              "FormatVersion": 3,
              "Pages": [ { "Frames": [
                { "$kind": "table", "Cells": [ { "Row": 0, "Column": 0, "Fill": "#FFEFF2F6" } ] } ] } ]
            }
            """)!.AsObject();

        FormatMigrations.Apply(racine);

        var case0 = racine["Pages"]![0]!["Frames"]![0]!["Cells"]![0]!;
        Assert.Equal("#FFEFF2F6", case0["Fill"]!["Color"]!.GetValue<string>());
    }

    [Fact]
    public void Un_fond_absent_le_reste_apres_migration()
    {
        // Enveloppé, un « null » deviendrait un aplat blanc : la page changerait
        // d'aspect à la seule ouverture, ce qu'aucune migration n'a le droit de faire.
        var racine = JsonNode.Parse("""
            {
              "FormatVersion": 3,
              "Pages": [ { "Frames": [ { "$kind": "text", "Fill": null } ] } ]
            }
            """)!.AsObject();

        FormatMigrations.Apply(racine);

        Assert.Null(racine["Pages"]![0]!["Frames"]![0]!["Fill"]);
    }

    [Fact]
    public void Un_fichier_plus_recent_est_refuse_plutot_que_lu_de_travers()
    {
        var racine = JsonNode.Parse(
            $$"""{ "FormatVersion": {{Document.CurrentFormatVersion + 1}}, "Pages": [] }""")!
            .AsObject();

        var erreur = Assert.Throws<InvalidDataException>(() => FormatMigrations.Apply(racine));

        Assert.Contains((Document.CurrentFormatVersion + 1).ToString(), erreur.Message);
    }
}
