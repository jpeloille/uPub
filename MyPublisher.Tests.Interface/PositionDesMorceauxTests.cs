using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Chaque morceau composé sait d'où il vient — le poste N3, côté vue.
///
/// <see cref="TexteCompose.Geometrie"/> passe d'une position du texte à un point du bloc
/// (<see cref="GeometrieDuTexte.PointDe"/>) et retour (<see cref="GeometrieDuTexte.IndexSous"/>) :
/// c'est sur elle que le clic simple de N12 posera le curseur, et que N7 mesurera l'écart
/// de la saisie. Les deux promesses se vérifient sur les documents réels, dans la vraie
/// fenêtre, et non sur des textes faits pour : le point d'un morceau est l'ancre où le dessin
/// l'a posé, au millionième de point ; et le point de chaque position ramène à elle-même.
///
/// Les documents : l'exemple, ATA 21 et les Fiches, ouverts par pub2raw, et
/// <c>recette-colonnes</c>, pour les colonnes. Chacun avec le nombre de contrôles de texte
/// qu'il porte : un parcours qui en perdrait passerait vert sur moins qu'il ne prétend.
/// </summary>
[Trait("poste", "N3")]
public class PositionDesMorceauxTests
{
    private const double Tolerance = 1e-6;

    private static Document Importer(string nom)
    {
        // Rendu.Ata21 cherche pub2raw là où la recette R5 le cherche, et le pose dans
        // l'environnement ; l'import qui suit le trouve.
        if (!PubReader.Disponible()) _ = Rendu.Ata21();
        return DocumentSerializer.Import(Path.Combine(Rendu.Racine(), "MyPublisher", "Books", nom)).Document;
    }

    private static Document Echantillon(string nom) =>
        DocumentSerializer.Load(Path.Combine(Rendu.Racine(), "Samples", nom));

    /// <summary>Le document, et le nombre de contrôles de texte qui ont une géométrie.</summary>
    private static (Document Document, int Blocs) Document(string nom) => nom switch
    {
        "exemple" => (Echantillon("exemple-bulletin.mypub"), 15),
        "ATA 21" => (Rendu.Ata21(), 9),
        "Fiches" => (Importer("Fiches limitations et procédures d'urgences.pub"), 69),
        "colonnes" => (Echantillon("recette-colonnes.mypub"), 3),
        _ => throw new ArgumentException(nom),
    };

    private static Panel Planche(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");

    /// <summary>Chaque contrôle de texte de chaque page, groupes et cases compris, avec sa géométrie.</summary>
    private static IEnumerable<(TexteCompose Controle, GeometrieDuTexte Geometrie, string Repere)> Textes(
        Window fenetre, MyPublisher.ViewModels.MainWindowViewModel vm)
    {
        for (var page = 0; page < vm.Document.Pages.Count; page++)
        {
            vm.CurrentPage = vm.Document.Pages[page];
            Rendu.Battre();
            var rang = 0;
            foreach (var controle in Planche(fenetre).GetVisualDescendants().OfType<TexteCompose>().ToList())
            {
                if (controle.Geometrie() is not { } geometrie) continue;
                yield return (controle, geometrie, $"page {page + 1}, bloc {rang++} « {controle.Bloc?.DisplayName} »");
            }
        }
    }

    /// <summary>Une position qui ne désigne aucun caractère : au milieu d'une paire, ou d'un « \r\n ».</summary>
    private static bool Interieure(string texte, int i) =>
        i > 0 && i < texte.Length &&
        (char.IsLowSurrogate(texte[i]) && char.IsHighSurrogate(texte[i - 1]) || texte[i] == '\n' && texte[i - 1] == '\r');

    [AvaloniaTheory]
    [InlineData("exemple")]
    [InlineData("ATA 21")]
    [InlineData("Fiches")]
    [InlineData("colonnes")]
    public void Le_point_d_un_morceau_est_l_ancre_ou_le_dessin_l_a_pose(string nom)
    {
        var (document, attendus) = Document(nom);
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var metriques = new MetriquesAvalonia();

        var blocs = 0;
        var morceaux = 0;
        foreach (var (controle, geometrie, repere) in Textes(fenetre, vm))
        {
            blocs++;
            // Ce que le dessin pose, dans l'ordre : les morceaux qui ont de l'encre.
            var dessines = geometrie.Lignes
                .SelectMany(l => l.Ligne.Morceaux.Where(m => m.Texte.Trim().Length > 0))
                .ToList();
            var poses = controle.Poser(metriques, metriques);
            Assert.True(dessines.Count == poses.Count, $"{nom}, {repere} : {dessines.Count} morceaux, {poses.Count} posés");

            for (var k = 0; k < poses.Count; k++)
            {
                Assert.Equal(poses[k].Texte, dessines[k].Texte);
                var point = geometrie.PointDe(dessines[k].Debut);
                Assert.True(Math.Abs(point.X - poses[k].Ancre.X) <= Tolerance,
                    $"{nom}, {repere}, « {dessines[k].Texte} » : PointDe {point.X}, ancre {poses[k].Ancre.X}");
                morceaux++;
            }
        }

        Assert.Equal(attendus, blocs);
        Assert.True(morceaux > 0);
        fenetre.Close();
    }

    [AvaloniaTheory]
    [InlineData("exemple")]
    [InlineData("ATA 21")]
    [InlineData("Fiches")]
    [InlineData("colonnes")]
    public void Le_point_de_chaque_position_ramene_a_elle(string nom)
    {
        var (document, attendus) = Document(nom);
        var (fenetre, vm) = Rendu.Ouvrir(document);

        var blocs = 0;
        var positions = 0;
        foreach (var (controle, geometrie, repere) in Textes(fenetre, vm))
        {
            blocs++;
            var texte = controle.Contenu ?? string.Empty;
            // L'exception que N3 déclare — un « \r\n » partagé entre deux fragments ne
            // compte plus qu'un paragraphe — ne touche aucun document réel : l'import retire
            // les « \r ».
            Assert.DoesNotContain('\r', texte);
            var fautes = new List<string>();
            for (var i = 0; i <= texte.Length; i++)
            {
                if (Interieure(texte, i)) continue;
                var point = geometrie.PointDe(i);
                var retour = geometrie.IndexSous(point);
                if (retour != i) fautes.Add($"{i} → ({point.X:F3}, {point.Y:F3}) → {retour}");
                positions++;
            }

            Assert.True(fautes.Count == 0, $"{nom}, {repere} : {fautes.Count} positions perdues, dont {string.Join(" ; ", fautes.Take(5))}");

            // Le contrôle répond comme sa géométrie : il la recompose à chaque appel.
            Assert.Equal(geometrie.PointDe(texte.Length / 2), controle.PointDe(texte.Length / 2));
            Assert.Equal(texte.Length / 2 - (Interieure(texte, texte.Length / 2) ? 1 : 0),
                controle.IndexSous(geometrie.PointDe(texte.Length / 2)));
        }

        Assert.Equal(attendus, blocs);
        Assert.True(positions > 0);
        fenetre.Close();
    }

    /// <summary>
    /// La mise en page arrondit au pixel la place et la taille du contrôle d'un bloc — une
    /// hauteur de 22,77 pt devient 23 —, et un bloc tourné tourne autour du centre de son
    /// contrôle, non de celui du modèle : le repère que <see cref="Frame.ToLocal"/> calcule
    /// sur le modèle s'écarte de celui de l'écran de moins d'un pixel, 0,75 pt au zoom 100 % —
    /// 0,5 pt mesuré sur ATA 21. Relevé au poste N3, non corrigé : c'est l'affaire de la
    /// saisie (N12, R7).
    /// </summary>
    private const double ArrondiDuPlacement = 0.75;

    /// <summary>
    /// Un bloc tourné : le point de la page passe dans le repère du bloc par
    /// <see cref="Frame.ToLocal"/>, comme le clic de N12 le fera, et désigne la position
    /// dont il vient — à l'arrondi du placement près, qui ne fait pas changer de caractère.
    /// ATA 21 porte deux blocs tournés.
    /// </summary>
    [AvaloniaFact]
    public void Un_bloc_tourne_entre_par_son_repere()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Ata21());

        var tournes = 0;
        foreach (var (controle, geometrie, repere) in Textes(fenetre, vm))
        {
            if (controle.Bloc is not { Rotation: not 0 } bloc || !vm.CurrentPage!.Frames.Contains(bloc)) continue;
            tournes++;

            var texte = controle.Contenu ?? string.Empty;
            for (var i = 0; i <= texte.Length; i++)
            {
                if (Interieure(texte, i)) continue;
                var local = geometrie.PointDe(i);
                var surLaPage = controle.TranslatePoint(local, Planche(fenetre));
                Assert.NotNull(surLaPage);

                var (x, y) = bloc.ToLocal(surLaPage.Value.X, surLaPage.Value.Y);
                Assert.True(Math.Abs(x - local.X) <= ArrondiDuPlacement && Math.Abs(y - local.Y) <= ArrondiDuPlacement,
                    $"{repere}, {i} : ({local.X}, {local.Y}) revient en ({x}, {y})");
                Assert.Equal(i, geometrie.IndexSous(new Point(x, y)));
            }
        }

        Assert.Equal(2, tournes);
        fenetre.Close();
    }

    /// <summary>
    /// Un bloc vide a une ligne, celle d'un paragraphe vide, calée comme le bloc le veut :
    /// le curseur s'y pose. Un bloc à champ n'a pas de géométrie — son contenu résolu n'est
    /// pas son texte.
    /// </summary>
    [AvaloniaFact]
    public void Un_bloc_vide_a_sa_ligne_et_un_champ_n_en_a_pas()
    {
        var vide = new TextFrame
        {
            Name = "Vide", X = 50, Y = 50, Width = 200, Height = 100, Text = string.Empty,
            VerticalAlign = VerticalAlign.Middle, Align = TextAlign.Center,
        };
        var folio = new TextFrame
        {
            Name = "Folio", X = 50, Y = 200, Width = 200, Height = 40, Text = "folio", Field = FieldKind.PageNumber,
        };
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(vide, folio));
        var controles = Planche(fenetre).GetVisualDescendants().OfType<TexteCompose>().ToList();

        var geometrie = controles.Single(c => c.Bloc == vide).Geometrie();
        Assert.NotNull(geometrie);
        var ligne = Assert.Single(geometrie.Lignes);
        Assert.Equal((0, 0), (ligne.Ligne.Debut, ligne.Ligne.Fin));

        // Centré : au milieu de la place entre les marges, et à mi-hauteur.
        var point = geometrie.PointDe(0);
        Assert.Equal(vide.PaddingLeft + (vide.Width - vide.PaddingLeft - vide.PaddingRight) / 2, point.X, 6);
        Assert.InRange(ligne.Haut, vide.PaddingTop + 1, vide.Height / 2);
        Assert.InRange(point.Y, ligne.Haut, ligne.Haut + ligne.Ligne.Avance);
        Assert.Equal(0, geometrie.IndexSous(new Point(0, 0)));
        Assert.Equal(0, geometrie.IndexSous(new Point(vide.Width, vide.Height)));

        var champ = controles.Single(c => c.Bloc == folio);
        Assert.Null(champ.Geometrie());
        Assert.Null(champ.PointDe(0));
        Assert.Null(champ.IndexSous(new Point(10, 10)));
        fenetre.Close();
    }
}
