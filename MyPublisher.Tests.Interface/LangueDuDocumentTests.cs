using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// La langue du texte, côté application — poste N9 : les deux documents réels importés
/// gardent la langue de chaque fragment, comparée au relevé de libmspub lui-même ; le dock
/// Document règle la langue du document en un pas d'annulation, sans rien changer à la
/// page ; un document neuf est en français, ou dans la langue de son modèle.
///
/// Les gestes sont ceux du banc (<see cref="Rendu"/>, poste N1). Les tests purs — le format,
/// l'import sur des relevés écrits à la main, la mise en forme, la composition — sont dans
/// <c>LangueTests</c>, du projet du noyau ; la recette, <c>Scripts/recettes/N9-langue.sh</c>,
/// joue les deux par leur trait.
/// </summary>
[Trait("poste", "N9")]
public class LangueDuDocumentTests
{
    // ------------------------------------------------------------------ les documents réels

    /// <summary>
    /// Un fragment du relevé de libmspub : où il va — un bloc de la page, ou une case d'un
    /// tableau —, son texte tel que l'import le pose, et sa langue déclarée.
    /// </summary>
    private sealed record FragmentSource(string Cible, string Texte, string? Langue);

    private static Dictionary<string, string> Cles(string ligne) =>
        ligne[(ligne.IndexOf('(') + 1)..ligne.LastIndexOf(')')]
            .Split(", ").Select(c => c.Split(": ", 2)).Where(c => c.Length == 2)
            .ToDictionary(c => c[0], c => c[1]);

    /// <summary>
    /// Les fragments du relevé, lus sans passer par l'import : leur texte comme
    /// <see cref="PubImporter"/> l'écrit — sans le « \r » final de chaque paragraphe, la
    /// tabulation et le saut forcé en caractères —, et l'étiquette des clés
    /// <c>fo:language</c>, <c>fo:script</c> et <c>fo:country</c>. Un fragment ouvert dans une
    /// case de tableau va à la case : les Fiches y écrivent leurs titres, sans bloc de texte.
    /// </summary>
    private static List<FragmentSource> Fragments(string releve)
    {
        var fragments = new List<FragmentSource>();
        var bloc = -1;
        var tableau = -1;
        string? cible = null;
        string? langue = null;
        System.Text.StringBuilder? texte = null;

        foreach (var ligne in releve.Split('\n').Select(l => l.Trim()))
        {
            var appel = ligne.Split('(')[0].Trim();
            switch (appel)
            {
                case "startTableObject": tableau++; break;
                case "openTableCell":
                    var position = Cles(ligne);
                    cible = $"tableau {tableau} case {position["librevenge:row"]},{position["librevenge:column"]}";
                    break;
                case "closeTableCell": cible = null; break;
                case "startTextObject": if (cible is null) bloc++; break;
                case "openSpan":
                    var cles = Cles(ligne);
                    langue = cles.TryGetValue("fo:language", out var l)
                        ? string.Join("-", new[] { l, cles.GetValueOrDefault("fo:script"), cles.GetValueOrDefault("fo:country") }
                            .Where(p => !string.IsNullOrEmpty(p)))
                        : null;
                    texte = new System.Text.StringBuilder();
                    break;
                case "insertText" when texte is not null:
                    texte.Append(ligne[(ligne.IndexOf('(') + 1)..ligne.LastIndexOf(')')].Replace("\r", ""));
                    break;
                case "insertTab" when texte is not null: texte.Append('\t'); break;
                case "insertLineBreak" when texte is not null: texte.Append('\n'); break;
                case "closeSpan" when texte is not null:
                    fragments.Add(new FragmentSource(cible ?? $"bloc {bloc}", texte.ToString(), langue));
                    texte = null;
                    break;
            }
        }

        return fragments;
    }

    [AvaloniaTheory]
    [InlineData("ATA 21 - Air conditioning.pub", "fr-FR:73", "fr-FR:50", "fr-FR")]
    [InlineData("Fiches limitations et procédures d'urgences.pub", "en-US:298 fr-FR:31", "en-US:294 fr-FR:17", "en-US")]
    public void Un_document_reel_importe_garde_la_langue_de_chaque_fragment(
        string nom, string declarees, string portees, string langueDuDocument)
    {
        var releve = PubReader.Relever(Rendu.Livre(nom));
        var source = Fragments(releve);
        var document = PubImporter.Importer(releve).Document;
        var blocs = document.Pages.SelectMany(page => page.Frames).OfType<TextFrame>().ToList();
        var tableaux = document.Pages.SelectMany(page => page.Frames).OfType<TableFrame>().ToList();

        // Le texte d'une cible, et la langue de chacun de ses caractères.
        (string Texte, Func<int, string> Langue) Cible(string nom)
        {
            var mots = nom.Split(' ');
            if (mots[0] == "bloc")
            {
                var bloc = blocs[int.Parse(mots[1], CultureInfo.InvariantCulture)];
                return (bloc.Text, i => Langues.DuCaractere(bloc, i, document));
            }

            var rangee = mots[3].Split(',').Select(n => int.Parse(n, CultureInfo.InvariantCulture)).ToArray();
            var cellule = tableaux[int.Parse(mots[1], CultureInfo.InvariantCulture)].Cells
                .Single(c => c.Row == rangee[0] && c.Column == rangee[1]);
            return (cellule.Text, i => Langues.DuCaractere(cellule, i, document));
        }

        // Ce que libmspub déclare : une langue par fragment, toutes comptées.
        Assert.Equal(declarees, Compte(source.Select(f => f.Langue)));

        // Ce que l'import en garde : chaque fragment qui porte un caractère se retrouve, à sa
        // place dans le texte de son bloc, chaque caractère dans la langue déclarée. Les
        // fragments vides — la seule marque de paragraphe — n'ont rien à porter.
        var curseurs = new Dictionary<string, int>();
        var retrouvees = new List<string>();
        foreach (var fragment in source.Where(f => f.Texte.Length > 0))
        {
            var (texte, langue) = Cible(fragment.Cible);
            var debut = texte.IndexOf(fragment.Texte, curseurs.GetValueOrDefault(fragment.Cible), StringComparison.Ordinal);
            Assert.True(debut >= 0, $"« {fragment.Texte} » n'est pas dans {fragment.Cible}");
            curseurs[fragment.Cible] = debut + fragment.Texte.Length;

            var langues = Enumerable.Range(debut, fragment.Texte.Length).Select(langue).Distinct().ToList();
            Assert.True(langues.SequenceEqual([fragment.Langue!]),
                $"« {fragment.Texte} », déclaré {fragment.Langue}, est importé en {string.Join(", ", langues)}");
            retrouvees.Add(langues[0]);
        }

        Assert.Equal(portees, Compte(retrouvees));
        Assert.Equal(langueDuDocument, document.Langue);
    }

    private static string Compte(IEnumerable<string?> langues) =>
        string.Join(" ", langues.GroupBy(l => l ?? "aucune").OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}:{g.Count()}"));

    // ------------------------------------------------------------------ le dock

    private static ComboBox Liste(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "LangueDuDocument");

    private static LangueProposee? Montree(Window fenetre) => Liste(fenetre).SelectedItem as LangueProposee;

    private static Document UnTexte() => Rendu.Feuille(new TextFrame
    {
        Name = "Texte", X = 40, Y = 40, Width = 300, Height = 80, FontSize = 18,
        Text = "Bonjour, hello, guten Tag.",
    });

    [AvaloniaFact]
    public void Le_dock_change_la_langue_du_document_en_un_pas_sans_rien_changer_a_la_page()
    {
        var (fenetre, vm) = Rendu.Ouvrir(UnTexte());
        Rendu.Onglet(fenetre, "Document");
        var avant = DocumentSerializer.ToJson(vm.Document);
        var encre = Rendu.Encre(fenetre);
        Assert.Equal("fr-FR", Montree(fenetre)?.Etiquette);
        Assert.Equal("Français (France)", Montree(fenetre)?.Nom);

        // Un clic déroule la liste ; un clic sur une langue la choisit.
        var liste = Liste(fenetre);
        Rendu.Cliquer(liste);
        Assert.True(liste.IsDropDownOpen, "un clic sur la liste des langues ne la déroule pas");
        var index = MainWindowViewModel.LanguesConnues.ToList().FindIndex(l => l.Etiquette == "en-GB");
        var entree = liste.ContainerFromIndex(index) as ComboBoxItem
                     ?? throw new InvalidOperationException("la liste déroulée n'a pas d'entrée « en-GB »");
        Rendu.Cliquer(entree);

        Assert.False(liste.IsDropDownOpen);
        Assert.Equal("en-GB", vm.Document.Langue);
        Assert.Equal("Anglais (Royaume-Uni)", Montree(fenetre)?.Nom);
        Assert.True(vm.IsDirty, "changer la langue ne marque pas le document");

        // La langue ne se dessine pas : la page est la même au pixel près.
        Assert.Equal(encre.Pixels, Rendu.Encre(fenetre).Pixels);

        // Un pas, que l'annulation rend, la liste avec.
        Rendu.UnSeulPas(vm, avant, "choisir la langue au dock");
        Assert.Equal("fr-FR", vm.Document.Langue);
        Assert.Equal("fr-FR", Montree(fenetre)?.Etiquette);
    }

    [AvaloniaFact]
    public void Deux_langues_choisies_de_suite_font_deux_pas()
    {
        var (fenetre, vm) = Rendu.Ouvrir(UnTexte());
        Rendu.Onglet(fenetre, "Document");
        var liste = Liste(fenetre);

        foreach (var etiquette in new[] { "de-DE", "it-IT" })
        {
            Rendu.Cliquer(liste);
            var index = MainWindowViewModel.LanguesConnues.ToList().FindIndex(l => l.Etiquette == etiquette);
            Rendu.Cliquer((ComboBoxItem)liste.ContainerFromIndex(index)!);
        }

        Assert.Equal("it-IT", vm.Document.Langue);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal("de-DE", vm.Document.Langue);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal("fr-FR", vm.Document.Langue);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void Une_langue_inconnue_se_montre_telle_quelle_et_ne_se_perd_pas()
    {
        var document = UnTexte();
        document.Langue = "tlh-Latn";
        var (fenetre, vm) = Rendu.Ouvrir(document);
        Rendu.Onglet(fenetre, "Document");

        Assert.Equal(new LangueProposee("tlh-Latn", "tlh-Latn"), Montree(fenetre));
        Assert.Equal(MainWindowViewModel.LanguesConnues.Count + 1, Liste(fenetre).ItemCount);
        Assert.Equal("tlh-Latn", vm.Document.Langue);
        Assert.False(vm.CanUndo);
        Assert.False(vm.IsDirty);
    }

    // ------------------------------------------------------------------ le document neuf

    [AvaloniaFact]
    public void Un_document_neuf_est_en_francais_ou_dans_la_langue_de_son_modele()
    {
        var (fenetre, vm) = Rendu.Ouvrir(UnTexte());
        Rendu.Onglet(fenetre, "Document");
        var culture = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        var modele = Path.Combine(Path.GetTempPath(), $"modele-n9-{Guid.NewGuid():N}.mypub");
        try
        {
            // La machine parle anglais : le document vierge reste en français.
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            vm.Document.Langue = "de-DE";
            vm.NewDocumentCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal("fr-FR", vm.Document.Langue);
            Assert.Equal("fr-FR", Montree(fenetre)?.Etiquette);

            // Un modèle en anglais britannique fait des documents en anglais britannique.
            var anglais = Document.CreateDefault();
            anglais.Title = "Modèle anglais";
            anglais.Langue = "en-GB";
            DocumentSerializer.Save(anglais, modele);
            vm.StartFromTemplate(modele);
            Rendu.Battre();
            Assert.Equal("en-GB", vm.Document.Langue);
            Assert.Equal("Anglais (Royaume-Uni)", Montree(fenetre)?.Nom);

            // Les modèles livrés sont réenregistrés : chacun écrit sa langue, le français.
            foreach (var livre in Directory.GetFiles(Path.Combine(Rendu.Racine(), "MyPublisher", "Templates"), "*.mypub"))
            {
                Assert.Contains("\"Langue\": \"fr-FR\"", File.ReadAllText(livre));
                vm.StartFromTemplate(livre);
                Rendu.Battre();
                Assert.Equal("fr-FR", vm.Document.Langue);
            }
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = culture;
            File.Delete(modele);
        }
    }
}
