using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.Views;
using Xunit;
using Mesure = MyPublisher.Banc.Saisie;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// La mesure de la saisie — le poste N7 : l'écart, en points, entre le point d'insertion que
/// le champ de saisie dessine et celui du caractère composé, que la commande « saisie » du
/// banc relève sur le corpus (<c>Scripts/mesurer-la-saisie.sh</c>). Ces tests éprouvent la
/// commande elle-même, et non une copie de sa mesure.
///
/// Ce qu'ils tiennent : l'étalonnage donne ses trois écarts connus, dans les deux
/// configurations ; la saisie que le banc ouvre est celle du double-clic, au millième de point ;
/// mesurer ne change ni le document, ni l'historique, ni le champ, et ne laisse aucune
/// transaction ouverte ; l'écart ne dépend pas du zoom ; un bloc tourné se mesure dans son
/// repère ; les positions sautent l'intérieur d'une paire et d'un « \r\n » ; le corpus compte
/// les blocs que le plan annonce. Le corpus lui-même n'est pas un test : ce qu'il donne est une
/// décision.
/// </summary>
[Trait("poste", "N7")]
public class MesureDeLaSaisieTests
{
    private static Document Echantillon(string nom) =>
        DocumentSerializer.Load(Path.Combine(Rendu.Racine(), "Samples", nom));

    private static Document Fiches()
    {
        if (!PubReader.Disponible()) _ = Rendu.Ata21();
        return DocumentSerializer.Import(Path.Combine(
            Rendu.Racine(), "MyPublisher", "Books", "Fiches limitations et procédures d'urgences.pub")).Document;
    }

    /// <summary>L'écart le plus grand des positions d'un paragraphe, son saut compris.</summary>
    private static double[] ParParagraphe(Mesure.Bloc bloc)
    {
        var bornes = bloc.Texte.Split('\n');
        var resultat = new double[bornes.Length];
        var debut = 0;
        for (var p = 0; p < bornes.Length; p++)
        {
            var fin = debut + bornes[p].Length;
            resultat[p] = bloc.Positions.Where(x => x.Indice >= debut && x.Indice <= fin).Max(x => x.Ecart);
            debut = fin + 1;
        }

        return resultat;
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void L_etalonnage_donne_ses_trois_ecarts(bool auMieux)
    {
        var (fenetre, vm) = Rendu.Ouvrir(Echantillon("recette-saisie.mypub"));
        var releve = Mesure.Mesurer(fenetre, vm, auMieux, "recette-saisie.mypub");
        Assert.Equal(3, releve.Blocs.Count);
        var (seul, apres, fragment) = (releve.Blocs[0], releve.Blocs[1], releve.Blocs[2]);

        // Un seul style, sans retrait, à interligne 1 : le champ tombe sur la composition.
        Assert.True(seul.Maximum < 0.1, $"un seul style : {seul.Maximum} pt");
        Assert.Empty(seul.Causes);

        // Six points après chaque paragraphe : rien au premier, six au deuxième, douze au troisième.
        var paragraphes = ParParagraphe(apres);
        Assert.Equal(3, paragraphes.Length);
        Assert.True(paragraphes[0] < 0.1, $"six points après, 1er paragraphe : {paragraphes[0]} pt");
        Assert.InRange(paragraphes[1], 5.75, 6.25);
        Assert.InRange(paragraphes[2], 11.75, 12.25);
        Assert.Equal(["espaces du bloc"], apres.Causes);
        Assert.All(apres.Positions.Where(p => p.Ecart > Mesure.Seuil), p => Assert.Equal("espaces de paragraphe", p.Motif));

        // Un fragment de seize points dans dix : la ligne de base descend, le champ ne la suit pas.
        Assert.True(fragment.Maximum > Mesure.Seuil, $"fragment de seize points : {fragment.Maximum} pt");
        Assert.Contains("corps", fragment.Causes);
        Assert.True(Math.Abs(fragment.Positions[0].Dy) > 1, $"la ligne de base du fragment : {fragment.Positions[0].Dy} pt");
        fenetre.Close();
    }

    [AvaloniaFact]
    public void La_saisie_du_banc_est_celle_du_double_clic()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Echantillon("recette-saisie.mypub"));
        foreach (var bloc in vm.Document.Pages[0].Frames.OfType<TextFrame>().ToList())
        {
            var mesure = Mesure.MesurerLeBloc(fenetre, bloc, 1, auMieux: false);

            var controle = MyPublisher.Banc.Banc.Page(fenetre).GetVisualDescendants().OfType<TexteCompose>()
                .Single(c => ReferenceEquals(c.Bloc, bloc));
            var geometrie = controle.Geometrie()!;
            var origine = controle.Bounds.Position;
            var indices = mesure.Positions.Select(p => p.Indice).ToList();
            var composes = indices.Select(geometrie.PointDe).ToList();

            var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
            var champ = Mesure.PointsDuChamp(zone, zone.GetVisualParent()!, origine, indices);
            for (var n = 0; n < indices.Count; n++)
            {
                Assert.True(Math.Abs(champ[n].Point.X - composes[n].X - mesure.Positions[n].Dx) < 1e-3 &&
                            Math.Abs(champ[n].Point.Y - composes[n].Y - mesure.Positions[n].Dy) < 1e-3,
                    $"« {bloc.Name} », position {indices[n]} : au double-clic ({champ[n].Point.X - composes[n].X}, " +
                    $"{champ[n].Point.Y - composes[n].Y}), au banc ({mesure.Positions[n].Dx}, {mesure.Positions[n].Dy})");
            }

            Rendu.Touche(fenetre, PhysicalKey.Escape);
            Assert.False(bloc.IsEditing);
        }

        fenetre.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mesurer_ne_change_rien(bool auMieux)
    {
        var (fenetre, vm) = Rendu.Ouvrir(Echantillon("exemple-bulletin.mypub"));
        var avant = DocumentSerializer.ToJson(vm.Document);
        var page = vm.CurrentPage;

        var releve = Mesure.Mesurer(fenetre, vm, auMieux, "exemple-bulletin.mypub");
        Rendu.Battre();

        Assert.Equal(15, releve.Blocs.Count);
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, "la mesure a laissé un pas d'annulation");
        Assert.False(vm.IsDirty, "la mesure a marqué le document modifié");
        Assert.Same(page, vm.CurrentPage);
        Assert.DoesNotContain(vm.Document.Pages.SelectMany(p => p.Frames).OfType<TextFrame>(), b => b.IsEditing);
        Assert.False(fenetre.GetVisualDescendants().OfType<PageCanvas>().Single().IsEditingText);

        // Le champ est rendu à ses valeurs : ni interligne posé, ni arrondi retiré.
        foreach (var zone in MyPublisher.Banc.Banc.Page(fenetre).GetVisualDescendants().OfType<TextBox>())
        {
            Assert.False(zone.IsSet(TextBox.LineHeightProperty), $"« {zone.DataContext} » garde l'interligne de la mesure");
            Assert.All(zone.GetSelfAndVisualDescendants().OfType<Layoutable>(), partie => Assert.True(partie.UseLayoutRounding));
        }

        // Aucune transaction laissée ouverte : un geste qui suit fait son pas.
        var bloc = vm.Document.Pages[0].Frames.OfType<TextFrame>().First();
        vm.BeginEdit();
        bloc.FontSize += 1;
        vm.CommitEdit();
        Assert.True(vm.CanUndo, "une transaction est restée ouverte : le geste suivant n'a pas fait son pas");
        fenetre.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void L_ecart_ne_depend_pas_du_zoom(bool auMieux)
    {
        var (fenetre, vm) = Rendu.Ouvrir(Echantillon("exemple-bulletin.mypub"));
        var banc = Mesure.Mesurer(fenetre, vm, auMieux, "exemple-bulletin.mypub");
        vm.Zoom = 2;
        Rendu.Battre();
        var zoom2 = Mesure.Mesurer(fenetre, vm, auMieux, "exemple-bulletin.mypub");

        Assert.Equal(1, banc.Echelle, 6);
        Assert.Equal(2 * Units.PointToDip, zoom2.Echelle, 6);
        Assert.Equal(banc.Blocs.Count, zoom2.Blocs.Count);
        for (var b = 0; b < banc.Blocs.Count; b++)
        {
            var (x, y) = (banc.Blocs[b], zoom2.Blocs[b]);
            Assert.Equal(x.Positions.Count, y.Positions.Count);
            for (var n = 0; n < x.Positions.Count; n++)
            {
                Assert.True(Math.Abs(x.Positions[n].Dx - y.Positions[n].Dx) <= 0.01 &&
                            Math.Abs(x.Positions[n].Dy - y.Positions[n].Dy) <= 0.01,
                    $"« {x.Nom} », position {x.Positions[n].Indice} : {x.Positions[n]} au zoom du banc, {y.Positions[n]} au zoom 2");
            }
        }

        fenetre.Close();
    }

    [AvaloniaFact]
    public void Un_bloc_tourne_se_mesure_dans_son_repere()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Echantillon("exemple-bulletin.mypub"));
        vm.CurrentPage = vm.Document.Pages[1];
        Rendu.Battre();
        var bloc = vm.CurrentPage.Frames.OfType<TextFrame>().Single(b => b.Rotation != 0);

        var mesure = Mesure.MesurerLeBloc(fenetre, bloc, 2, auMieux: true);

        // Tourné de huit degrés, le bloc mesuré dans la page s'écarterait de plusieurs points dès
        // le premier caractère ; dans son repère, le champ y tombe sur la composition.
        Assert.True(Math.Abs(mesure.Positions[0].Dx) < 0.01 && Math.Abs(mesure.Positions[0].Dy) < 0.01,
            $"« {bloc.Name} », tourné de {bloc.Rotation}° : {mesure.Positions[0]}");
        Assert.True(mesure.Maximum < Mesure.Seuil, $"« {bloc.Name} » : {mesure.Maximum} pt");
        fenetre.Close();
    }

    [Fact]
    public void Les_positions_sautent_l_interieur_d_une_paire_et_d_un_retour()
    {
        // a \r \n b 😀 c : ni entre « \r » et « \n », ni entre les deux moitiés de la paire.
        Assert.Equal(new[] { 0, 1, 3, 4, 6, 7 }, Mesure.Positions("a\r\nb\U0001F600c").ToArray());
        Assert.Equal(new[] { 0 }, Mesure.Positions("").ToArray());
        // Le 95ᵉ centile, au rang le plus proche : sur vingt valeurs, la dix-neuvième.
        Assert.Equal(19, Mesure.Centile(Enumerable.Range(1, 20).Select(x => (double)x), 0.95));
    }

    [AvaloniaTheory]
    [InlineData("exemple", 15, 1, 0)]
    [InlineData("ATA 21", 9, 2, 0)]
    [InlineData("Fiches", 18, 0, 51)]
    [InlineData("coupure", 74, 0, 0)]
    public void Le_corpus_a_les_blocs_que_le_plan_annonce(string nom, int blocs, int tournes, int ecartes)
    {
        var document = nom switch
        {
            "exemple" => Echantillon("exemple-bulletin.mypub"),
            "ATA 21" => Rendu.Ata21(),
            "Fiches" => Fiches(),
            _ => Echantillon("recette-coupure.mypub"),
        };
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var releve = Mesure.Mesurer(fenetre, vm, auMieux: false, nom);

        Assert.Equal(blocs, releve.Blocs.Count);
        Assert.Equal(tournes, releve.Blocs.Count(b => b.Rotation % 360 != 0));
        Assert.Equal(ecartes, releve.Ecartes.Count);
        Assert.All(releve.Blocs, b => Assert.Equal(Mesure.Positions(b.Texte).Count(), b.Positions.Count));
        fenetre.Close();
    }
}
