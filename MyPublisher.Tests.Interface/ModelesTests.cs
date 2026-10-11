using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les modèles (B1), joués sur la vraie fenêtre : « Ctrl+N, choisir « dépliant »,
/// modifier un texte, enregistrer — le fichier d'origine est intact ».
///
/// La recette de B1 avait été jouée à la main le 5 septembre 2026, puis jetée. Ce qui
/// la rend rejouable ici, c'est qu'on frappe les mêmes touches que l'utilisateur : le
/// catalogue s'ouvre par <c>Ctrl+N</c> et non par un appel de méthode, et l'enregistrement
/// part de <c>Ctrl+S</c>, qui est précisément le geste qui écraserait le modèle si le
/// document en avait gardé le chemin.
///
/// Les modèles éprouvés au <c>Ctrl+S</c> sont des COPIES rangées dans un dossier de
/// travail, jamais ceux du répertoire de sortie : un sabotage qui rendrait au document
/// le chemin de son modèle écraserait sinon le fichier que les tests suivants
/// liraient, et la copie de compilation, plus ancienne que lui, ne le remplacerait pas.
/// </summary>
public sealed class ModelesTests : IDisposable
{
    private readonly string _travail =
        Path.Combine(Path.GetTempPath(), "modeles-" + Guid.NewGuid().ToString("N"));

    public ModelesTests() => Directory.CreateDirectory(_travail);

    public void Dispose() => Directory.Delete(_travail, recursive: true);

    /// <summary>Les quatre modèles livrés, tels que le catalogue les trouve.</summary>
    public static TheoryData<string> Modeles() =>
        new() { "affiche.mypub", "bulletin.mypub", "carte-de-visite.mypub", "depliant-trois-volets.mypub" };

    private const string Retouche = "Retouché par la recette B1";

    [AvaloniaFact]
    [Trait("poste", "B1")]
    public void Ctrl_N_ouvre_le_catalogue_et_part_du_depliant_sans_son_chemin()
    {
        // Un document déjà ouvert, avec un chemin : c'est l'état d'où l'on part le plus
        // souvent, et celui où un chemin oublié par la création se verrait le moins.
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille());

        fenetre.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control);
        Rendu.Battre();

        var catalogue = fenetre.OwnedWindows.OfType<TemplateChooserWindow>().SingleOrDefault();
        Assert.NotNull(catalogue);

        var choix = Assert.IsType<TemplateChooserViewModel>(catalogue.DataContext);
        // La page blanche en tête, puis les quatre modèles livrés : un catalogue qui en
        // perdrait un en silence — modèle illisible, passé sous silence par conception —
        // se verrait ici et nulle part ailleurs.
        Assert.True(choix.Choices[0].IsBlank);
        Assert.Equal(
            new[] { "Affiche A3", "Bulletin trois colonnes", "Carte de visite", "Dépliant trois volets" },
            choix.Choices.Skip(1).Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());

        // Choisir, puis « Créer » — le bouton par défaut, donc Entrée.
        choix.Selected = choix.Choices.Single(c => c.Name == "Dépliant trois volets");
        Rendu.Battre();
        catalogue.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Rendu.Battre();

        Assert.Empty(fenetre.OwnedWindows.OfType<TemplateChooserWindow>());
        Assert.Equal("Dépliant trois volets", vm.Document.Title);
        // Tout le poste est là : le document est chargé, son chemin ne l'est pas.
        Assert.Null(vm.FilePath);
        Assert.False(vm.IsDirty);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Modeles))]
    [Trait("poste", "B1")]
    public void Un_modele_retouche_puis_enregistre_reste_intact_octet_pour_octet(string fichier)
    {
        var modele = Path.Combine(_travail, fichier);
        File.Copy(Path.Combine(TemplateStore.Folder, fichier), modele);
        var avant = File.ReadAllBytes(modele);

        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille());
        vm.StartFromTemplate(modele);
        Rendu.Battre();

        Assert.Null(vm.FilePath);

        // Le premier bloc de texte du document : le titre dans trois modèles, le nom de
        // la société sur la carte de visite, dont tous les textes sont des champs. Le
        // texte saisi sous un champ s'enregistre tout de même — il reparaît quand on
        // retire le champ —, si bien que la retouche se lit dans le fichier dans les
        // quatre cas.
        var bloc = vm.Document.Pages
            .SelectMany(page => page.Frames)
            .OfType<TextFrame>()
            .First();
        bloc.Text = Retouche;
        Rendu.Battre();

        // « Enregistrer » : le geste qui écraserait le modèle si le document en avait
        // gardé le chemin. Sans chemin, il propose « Enregistrer sous », que la
        // plate-forme sans affichage referme sans rien rendre.
        fenetre.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);
        Rendu.Battre();

        Assert.Equal(avant, File.ReadAllBytes(modele));
        Assert.Null(vm.FilePath);

        // « Enregistrer sous », ailleurs : la retouche part dans la copie, et dans elle
        // seule.
        var copie = Path.Combine(_travail, "copie-" + fichier);
        vm.SaveTo(copie);

        Assert.Equal(avant, File.ReadAllBytes(modele));
        Assert.Equal(copie, vm.FilePath);
        Assert.Contains(Retouche, File.ReadAllText(copie));
        Assert.DoesNotContain(Retouche, File.ReadAllText(modele));
    }
}
