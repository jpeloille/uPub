using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;
using Xunit.Abstractions;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// L'import garde le fer des cases et des paragraphes à gauche — poste Q26, sur les deux
/// documents réels, confrontés à ce que <c>pub2raw</c> en relève : c'est la source qui fait foi,
/// et non un compte pris sur l'import lui-même.
///
/// Deux défauts, trouvés par R6 : les seize valeurs centrées des Fiches, dans des cases dont le
/// bloc de texte n'est pas ouvert, arrivaient au fer à gauche ; et ATA 21 comptait 36 paragraphes
/// justifiés à l'import pour 31 dans la source, un paragraphe au fer à gauche sans autre réglage
/// prenant le fer de son bloc justifié. Puis l'aller-retour que R6 n'avait pas pu jouer : les
/// cases centrées des Fiches, montrées à l'écran et au dock, gardent leur fer.
/// </summary>
[Trait("poste", "Q26")]
public class FerALImportDesDocumentsReelsTests
{
    private const string Ata21 = "ATA 21 - Air conditioning.pub";
    private const string Fiches = "Fiches limitations et procédures d'urgences.pub";

    private readonly ITestOutputHelper _sortie;

    public FerALImportDesDocumentsReelsTests(ITestOutputHelper sortie) => _sortie = sortie;

    /// <summary>Un paragraphe de la source : son fer, et les sauts de ligne forcés qu'il porte.</summary>
    private sealed record ParagrapheSource(TextAlign Fer, int Sauts);

    /// <summary>
    /// Ce que le relevé dit des fers : les paragraphes de chaque bloc de texte, dans l'ordre, et
    /// ceux de chaque case, rangés par tableau, ligne et colonne.
    /// </summary>
    private sealed record Source(
        List<List<ParagrapheSource>> Blocs,
        Dictionary<(int Tableau, int Ligne, int Colonne), List<ParagrapheSource>> Cases);

    private static TextAlign Fer(string ligne) =>
        Regex.Match(ligne, @"fo:text-align: (\w+)").Groups[1].Value switch
        {
            "center" => TextAlign.Center,
            "right" => TextAlign.Right,
            "justify" => TextAlign.Justify,
            _ => TextAlign.Left,
        };

    private static int Entier(string ligne, string cle) =>
        int.Parse(Regex.Match(ligne, cle + @": (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);

    private static Source Relever(string releve)
    {
        var source = new Source([], []);
        List<ParagrapheSource>? courant = null;
        var tableau = -1;
        foreach (var brute in releve.Split('\n'))
        {
            var ligne = brute.Trim();
            if (ligne.StartsWith("startTextObject", StringComparison.Ordinal))
            {
                courant = [];
                source.Blocs.Add(courant);
            }
            else if (ligne.StartsWith("startTableObject", StringComparison.Ordinal))
            {
                tableau++;
            }
            else if (ligne.StartsWith("openTableCell", StringComparison.Ordinal))
            {
                courant = [];
                source.Cases[(tableau, Entier(ligne, "librevenge:row"), Entier(ligne, "librevenge:column"))] = courant;
            }
            else if (ligne.StartsWith("openParagraph", StringComparison.Ordinal))
            {
                courant?.Add(new ParagrapheSource(Fer(ligne), 0));
            }
            else if (ligne.StartsWith("insertLineBreak", StringComparison.Ordinal) && courant is { Count: > 0 })
            {
                courant[^1] = courant[^1] with { Sauts = courant[^1].Sauts + 1 };
            }
        }

        return source;
    }

    /// <summary>Les fers que la composition lit, paragraphe du modèle par paragraphe du modèle.</summary>
    private static IReadOnlyList<ParagrapheEffectif> Effectifs(TextFrame bloc) =>
        ParagrapheEffectif.Resoudre(Recalage.NombreDeParagraphes(bloc.Text ?? ""), bloc.Paragraphs, bloc.Paragraph);

    /// <summary>
    /// Chaque paragraphe de chaque bloc de texte au fer que la source lui donne, et chaque case au
    /// fer de son premier paragraphe. L'import rend encore le saut de ligne forcé de Publisher par
    /// un paragraphe (T1 de l'étude de 98 l'en corrigera) : un paragraphe de la source est donc lu
    /// au paragraphe du modèle où il commence, après les sauts forcés de ceux qui le précèdent.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(Ata21, 21, 31, 0, 0)]
    [InlineData(Fiches, 120, 1, 0, 16)]
    public void Chaque_paragraphe_et_chaque_case_sont_au_fer_de_la_source(
        string nom, int aGauche, int justifies, int centres, int casesCentrees)
    {
        var releve = PubReader.Relever(Rendu.Livre(nom));
        var source = Relever(releve);
        var document = PubImporter.Importer(releve).Document;
        var blocs = document.Pages.SelectMany(p => p.Frames).OfType<TextFrame>().ToList();
        var tableaux = document.Pages.SelectMany(p => p.Frames).OfType<TableFrame>().ToList();
        Assert.Equal(source.Blocs.Count, blocs.Count);

        var lus = new List<TextAlign>();
        for (var b = 0; b < blocs.Count; b++)
        {
            var effectifs = Effectifs(blocs[b]);
            var paragraphes = source.Blocs[b];
            Assert.Equal(paragraphes.Count + paragraphes.Sum(p => p.Sauts), effectifs.Count);

            var rang = 0;
            for (var p = 0; p < paragraphes.Count; p++)
            {
                Assert.True(paragraphes[p].Fer == effectifs[rang].Align,
                    $"« {blocs[b].Name} », paragraphe {p} : {paragraphes[p].Fer} dans la source, {effectifs[rang].Align} à l'import");
                lus.Add(effectifs[rang].Align);
                rang += 1 + paragraphes[p].Sauts;
            }
        }

        var cases = new List<TextAlign>();
        foreach (var ((t, ligne, colonne), paragraphes) in source.Cases)
        {
            var cellule = tableaux[t].Cells.Single(c => c.Row == ligne && c.Column == colonne);
            var attendu = paragraphes.Count > 0 ? paragraphes[0].Fer : TextAlign.Left;
            Assert.True(attendu == cellule.Align,
                $"« {cellule.Text} » ({tableaux[t].Name}, {cellule.Label}) : {attendu} dans la source, {cellule.Align} à l'import");
            cases.Add(cellule.Align);
        }

        _sortie.WriteLine($"{nom} : {lus.Count} paragraphes de bloc — {lus.Count(f => f == TextAlign.Left)} à gauche, " +
                          $"{lus.Count(f => f == TextAlign.Justify)} justifiés, {lus.Count(f => f == TextAlign.Center)} centrés ; " +
                          $"{cases.Count} cases, dont {cases.Count(f => f == TextAlign.Center)} centrées");
        Assert.Equal(aGauche, lus.Count(f => f == TextAlign.Left));
        Assert.Equal(justifies, lus.Count(f => f == TextAlign.Justify));
        Assert.Equal(centres, lus.Count(f => f == TextAlign.Center));
        Assert.Equal(casesCentrees, cases.Count(f => f == TextAlign.Center));
    }

    // ------------------------------------------------------------------ l'aller-retour

    /// <summary>La précision que le plan demande à l'écran.</summary>
    private const double Precision = 0.05;

    private static IEnumerable<(Page Page, TableFrame Tableau, TableCell Case)> Centrees(Document document) =>
        from page in document.Pages
        from tableau in page.Frames.OfType<TableFrame>()
        from cellule in tableau.Cells
        where cellule.Align == TextAlign.Center
        select (page, tableau, cellule);

    /// <summary>Un point du tableau qu'un clic atteint : le tableau y est le bloc le plus haut de la page.</summary>
    private static (double X, double Y) PointDuTableau(Page page, TableFrame tableau)
    {
        for (var fy = 0.5; fy > 0.05; fy -= 0.1)
        {
            for (var fx = 0.05; fx < 1; fx += 0.05)
            {
                var (px, py) = (tableau.X + fx * tableau.Width, tableau.Y + fy * tableau.Height);
                if (ReferenceEquals(tableau, page.Frames.LastOrDefault(f => f.Visible && f.HitTest(px, py)))) return (px, py);
            }
        }

        throw new InvalidOperationException($"« {tableau.Name} » : aucun point qu'un clic atteigne");
    }

    /// <summary>
    /// Le milieu de l'encre de la première ligne d'une case à l'écran, et le milieu de sa zone de
    /// texte, dans le repère de la case : lus sur la composition que l'écran dessine, blancs
    /// pendants exclus.
    /// </summary>
    private static (double Encre, double Zone) MilieuALEcran(Window fenetre, TableFrame tableau, TableCell cellule)
    {
        var compose = fenetre.GetVisualDescendants().OfType<TexteCompose>()
            .Single(c => c.Bloc is TextFrame bloc && bloc.Text == cellule.Text && c.IsEffectivelyVisible &&
                         c.GetVisualAncestors().OfType<ItemsControl>().Any(i => ReferenceEquals(i.DataContext, tableau)));
        var bloc = (TextFrame)compose.Bloc!;
        Assert.Equal(TextAlign.Center, bloc.Align);
        var geometrie = compose.Geometrie() ?? throw new InvalidOperationException("pas de géométrie");

        var premiere = geometrie.Lignes[0];
        var texte = bloc.Text;
        var fin = premiere.Ligne.Debut;
        for (var i = premiere.Ligne.Debut; i < texte.Length && geometrie.LigneDe(i).Rang == premiere.Rang; i++)
        {
            if (!char.IsWhiteSpace(texte[i])) fin = i + 1;
        }

        var gauche = geometrie.PointDe(premiere.Ligne.Debut).X;
        var droite = geometrie.PointDe(fin, enFinDeLigne: true).X;
        return ((gauche + droite) / 2, (bloc.PaddingLeft + bloc.Width - bloc.PaddingRight) / 2);
    }

    /// <summary>
    /// La recette que R6 n'avait pas pu jouer : les seize cases centrées des Fiches passent par
    /// l'interface. Chaque bande est prise d'un clic sur la page ; l'écran centre la valeur dans sa
    /// case à 0,05 pt ; le dock montre « Centré » dans le choix d'alignement de la case, et le
    /// montrer ne lui écrit rien. Rien n'est défait ni écrit ; le document enregistré et relu les a
    /// toutes, centrées.
    /// </summary>
    [AvaloniaFact]
    public void Les_cases_centrees_des_Fiches_survivent_a_un_aller_retour_par_l_interface()
    {
        var document = DocumentSerializer.Import(Rendu.Livre(Fiches)).Document;
        var centrees = Centrees(document).ToList();
        Assert.Equal(16, centrees.Count);
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.SectionTableauOuverte = true;
        var avant = DocumentSerializer.ToJson(vm.Document);
        var vues = 0;

        foreach (var groupe in centrees.GroupBy(c => c.Page))
        {
            vm.CurrentPage = groupe.Key;
            Rendu.UneImage();
            foreach (var (page, tableau, cellule) in groupe)
            {
                var (px, py) = PointDuTableau(page, tableau);
                Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, px, py));
                Assert.True(ReferenceEquals(tableau, vm.Selected), $"« {cellule.Text} » : le clic n'a pas pris sa bande");
                Rendu.UneImage();

                var (encre, zone) = MilieuALEcran(fenetre, tableau, cellule);
                Assert.True(Math.Abs(encre - zone) <= Precision,
                    $"« {cellule.Text} » : l'écran pose le milieu de la ligne à {encre:0.0000} pt, celui de la case est à {zone:0.0000} pt");

                var choix = fenetre.GetVisualDescendants().OfType<ComboBox>()
                    .Single(c => ReferenceEquals(c.DataContext, cellule) && c.IsEffectivelyVisible);
                Assert.Equal(TextAlign.Center, choix.SelectedItem);
                Assert.Equal(TextAlign.Center, cellule.Align);
                _sortie.WriteLine($"page {document.Pages.IndexOf(page) + 1}, « {cellule.Text.Trim()} » : centrée, à {encre - zone:+0.0000;-0.0000} pt à l'écran");
                vues++;

                vm.SetSelection([]);
                Rendu.Battre();
            }
        }

        Assert.Equal(16, vues);
        Assert.False(vm.CanUndo, "montrer les cases a écrit dans le document");
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        var relu = DocumentSerializer.FromJson(DocumentSerializer.ToJson(vm.Document));
        Assert.Equal(centrees.Select(c => c.Case.Text), Centrees(relu).Select(c => c.Case.Text));
    }
}
