using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Masquer un bloc — poste I8, le noyau. Un bloc masqué reste dans le document, et s'y
/// écrit masqué ; il n'est ni exporté ni nommé par la passe des polices, et la découpe
/// d'un groupe l'ignore. Ce que la planche et la liste en font est éprouvé par
/// <c>MasquerEcranTests</c>, et le PDF par la recette <c>I8-masquer</c>.
/// </summary>
public class MasquerTests
{
    private const string Absente = "Police Absente De Cette Machine";

    private static Document Document(params Frame[] blocs)
    {
        var document = new Document { Title = "Masquer" };
        document.Pages.Add(new Page { Name = "Page 1" });
        foreach (var bloc in blocs) document.Pages[0].Frames.Add(bloc);
        return document;
    }

    private static JsonNode Arbre(Document document) => JsonNode.Parse(DocumentSerializer.ToJson(document))!;

    // ------------------------------------------------------------------ le fichier

    [Theory]
    [Trait("poste", "I8")]
    [InlineData(typeof(TextFrame))]
    [InlineData(typeof(ImageFrame))]
    [InlineData(typeof(ShapeFrame))]
    [InlineData(typeof(GroupFrame))]
    [InlineData(typeof(TableFrame))]
    public void Un_bloc_est_visible_par_defaut_et_le_dit_dans_le_fichier(Type type)
    {
        var bloc = (Frame)Activator.CreateInstance(type)!;

        Assert.True(bloc.Visible);
        // Le sérialiseur écrit toutes les valeurs, défaut compris : c'est ce qui a fait
        // réenregistrer Samples et Templates.
        Assert.True(Arbre(Document(bloc))["Pages"]![0]!["Frames"]![0]!["Visible"]!.GetValue<bool>());
    }

    /// <summary>
    /// Un document écrit avant I8 n'a pas la clé : il s'ouvre visible partout, sans
    /// migration, et <c>FormatVersion</c> n'a pas bougé.
    /// </summary>
    [Fact]
    [Trait("poste", "I8")]
    public void Un_document_sans_la_cle_s_ouvre_visible_partout()
    {
        var groupe = new GroupFrame { Width = 100, Height = 100 };
        groupe.Children.Add(new TextFrame { Text = "Dedans" });
        var arbre = Arbre(Document(new ShapeFrame(), groupe));
        Retirer(arbre, "Visible");

        Assert.DoesNotContain("\"Visible\"", arbre.ToJsonString());
        var relu = DocumentSerializer.FromJson(arbre.ToJsonString());

        Assert.Equal(5, Models.Document.CurrentFormatVersion);
        Assert.All(relu.Pages[0].Frames, bloc => Assert.True(bloc.Visible));
        Assert.True(((GroupFrame)relu.Pages[0].Frames[1]).Children[0].Visible);
    }

    [Fact]
    [Trait("poste", "I8")]
    public void Visible_faux_fait_l_aller_retour_au_bloc_et_dans_un_groupe()
    {
        var groupe = new GroupFrame { Name = "Groupe", Width = 200, Height = 100 };
        groupe.Children.Add(new TextFrame { Name = "Montré", Text = "Montré", Width = 100, Height = 50 });
        groupe.Children.Add(new TextFrame { Name = "Caché", Text = "Caché", Y = 50, Width = 100, Height = 50, Visible = false });
        var document = Document(new ShapeFrame { Name = "Forme", Visible = false }, groupe);

        var json = DocumentSerializer.ToJson(document);
        Assert.Contains("\"Visible\": false", json);

        var relu = DocumentSerializer.FromJson(json);
        Assert.False(relu.Pages[0].Frames[0].Visible);
        Assert.True(relu.Pages[0].Frames[1].Visible);
        var enfants = ((GroupFrame)relu.Pages[0].Frames[1]).Children;
        Assert.True(enfants[0].Visible);
        Assert.False(enfants[1].Visible);

        // Relu puis réécrit, au caractère près.
        Assert.Equal(json, DocumentSerializer.ToJson(relu));
    }

    /// <summary>
    /// La recopie — dupliquer un bloc, une page — garde la propriété, au bloc comme à ce
    /// qu'un groupe porte. <c>CloneTests</c> le vérifie déjà par réflexion pour toute
    /// propriété écrite ; celui-ci le dit pour I8, dans les deux sens.
    /// </summary>
    [Fact]
    [Trait("poste", "I8")]
    public void Une_recopie_garde_la_visibilite_du_bloc_et_de_ce_que_porte_un_groupe()
    {
        var groupe = new GroupFrame { Width = 200, Height = 100, Visible = false };
        groupe.Children.Add(new ShapeFrame { Width = 100, Height = 100 });
        groupe.Children.Add(new ShapeFrame { X = 100, Width = 100, Height = 100, Visible = false });

        var copie = (GroupFrame)groupe.Clone();

        Assert.False(copie.Visible);
        Assert.True(copie.Children[0].Visible);
        Assert.False(copie.Children[1].Visible);
        Assert.False(new TextFrame { Visible = false }.Clone().Visible);
        Assert.True(new TextFrame().Clone().Visible);
    }

    /// <summary>
    /// Les cases d'un tableau ne voient pas la propriété : une cellule ne la porte ni dans
    /// le modèle ni dans le fichier, et les blocs de texte jetables qui la dessinent
    /// naissent visibles. C'est le tableau entier qui se masque.
    /// </summary>
    [Fact]
    [Trait("poste", "I8")]
    public void Les_cases_d_un_tableau_ne_voient_pas_la_propriete()
    {
        var tableau = new TableFrame { Visible = false };
        tableau.ColumnCount = 3;
        tableau.RowCount = 2;

        Assert.Null(typeof(TableCell).GetProperty(nameof(Frame.Visible)));
        Assert.All(tableau.CellFrames, cellule => Assert.True(cellule.Visible));

        var cellules = Arbre(Document(tableau))["Pages"]![0]!["Frames"]![0]!["Cells"]!.AsArray();
        Assert.NotEmpty(cellules);
        Assert.All(cellules, cellule => Assert.Null(cellule!["Visible"]));
    }

    // ------------------------------------------------------------------ l'export

    /// <summary>
    /// L'export passe le bloc masqué, et rien d'autre : la page qui le porte, masqué,
    /// s'écrit octet pour octet comme la page qui ne le porte pas. Un tracé laissé — son
    /// fond, son filet, son texte, ou la découpe d'un groupe élargie par son filet —
    /// changerait le flux. La contre-épreuve : visible, il le change.
    /// </summary>
    [Fact]
    [Trait("poste", "I8")]
    public void Un_bloc_masque_ne_laisse_rien_dans_le_flux_de_la_page()
    {
        Frame Fond() => new ShapeFrame { Name = "Fond", X = 40, Y = 40, Width = 300, Height = 200 };
        Frame Cache(bool visible) => new TextFrame
        {
            Name = "Caché", Text = "Escamoté", X = 60, Y = 60, Width = 200, Height = 40,
            Fill = new SolidFill("#FFFFCC00"), Stroke = "#FF000000", StrokeThickness = 2, Visible = visible,
        };
        GroupFrame Groupe(bool visible)
        {
            var groupe = new GroupFrame { Name = "Groupe", X = 40, Y = 300, Width = 200, Height = 100 };
            groupe.Children.Add(new ShapeFrame { Width = 100, Height = 100 });
            groupe.Children.Add(new ShapeFrame { X = 100, Width = 100, Height = 100, StrokeThickness = 12, Visible = visible });
            return groupe;
        }
        GroupFrame Seul()
        {
            var groupe = new GroupFrame { Name = "Groupe", X = 40, Y = 300, Width = 200, Height = 100 };
            groupe.Children.Add(new ShapeFrame { Width = 100, Height = 100 });
            return groupe;
        }

        var sans = Flux(Document(Fond(), Seul()));
        var masque = Flux(Document(Fond(), Cache(false), Groupe(false)));
        var montre = Flux(Document(Fond(), Cache(true), Groupe(true)));

        Assert.Equal(sans, masque);
        Assert.NotEqual(sans, montre);
    }

    /// <summary>
    /// Un groupe masqué emporte tout ce qu'il porte, même ce qui est visible dedans.
    /// </summary>
    [Fact]
    [Trait("poste", "I8")]
    public void Un_groupe_masque_n_exporte_aucun_de_ses_blocs()
    {
        var groupe = new GroupFrame { X = 40, Y = 40, Width = 200, Height = 100, Visible = false };
        groupe.Children.Add(new ShapeFrame { Width = 100, Height = 100 });
        groupe.Children.Add(new TextFrame { X = 100, Width = 100, Height = 100, Text = "Dedans" });

        Assert.Equal(Flux(Document()), Flux(Document(groupe)));
    }

    /// <summary>
    /// Q8 : l'export n'avertit que des polices qu'il dessine. Un bloc masqué n'en dessine
    /// aucune, ni le bloc de texte, ni le tableau, ni ce que porte un groupe masqué.
    /// </summary>
    [Fact]
    [Trait("poste", "I8")]
    public void L_export_n_avertit_pas_des_polices_d_un_bloc_masque()
    {
        var texte = new TextFrame { Text = "Escamoté", FontFamily = Absente, Width = 200, Height = 40, Visible = false };
        var tableau = new TableFrame { FontFamily = "Police Inexistante", Y = 100, Visible = false };
        tableau.ColumnCount = 2;
        tableau.RowCount = 1;
        tableau.Cells[0].Text = "Case";
        var groupe = new GroupFrame { Y = 200, Width = 200, Height = 40, Visible = false };
        groupe.Children.Add(new TextFrame { Text = "Dedans", FontFamily = "Autre Police Absente", Width = 200, Height = 40 });

        Assert.Empty(Exporter(Document(texte, tableau, groupe)));

        // La contre-épreuve : les mêmes, visibles, nomment leurs trois polices.
        texte.Visible = tableau.Visible = groupe.Visible = true;
        var avertissements = Exporter(Document(texte, tableau, groupe));
        Assert.Contains(avertissements, a => a.Contains(Absente));
        Assert.Contains(avertissements, a => a.Contains("Police Inexistante"));
        Assert.Contains(avertissements, a => a.Contains("Autre Police Absente"));
    }

    /// <summary>
    /// Rien d'un bloc masqué n'avertit : ni son image introuvable, ni son texte qui
    /// déborde. L'export ne le dessine pas ; il n'a rien à en dire.
    /// </summary>
    [Fact]
    [Trait("poste", "I8")]
    public void Un_bloc_masque_n_avertit_ni_d_une_image_ni_d_un_debordement()
    {
        var image = new ImageFrame { Path = "/nulle/part/image-absente.png", Visible = false };
        var deborde = new TextFrame
        {
            Text = string.Join(" ", Enumerable.Repeat("déborde", 200)), Width = 60, Height = 20, Visible = false,
        };

        Assert.Empty(Exporter(Document(image, deborde)));

        image.Visible = deborde.Visible = true;
        Assert.Equal(2, Exporter(Document(image, deborde)).Count);
    }

    // ------------------------------------------------------------------ la découpe

    /// <summary>
    /// La découpe d'un groupe s'élargit du plus large demi-filet qu'il porte (Q5) — des
    /// filets dessinés. Celui d'un bloc masqué ne l'est pas.
    /// </summary>
    [Fact]
    [Trait("poste", "I8")]
    public void Le_filet_d_un_bloc_masque_n_elargit_pas_la_decoupe()
    {
        var montre = new ShapeFrame { Width = 50, Height = 50, Stroke = "#FF000000", StrokeThickness = 2 };
        var cache = new ShapeFrame { Width = 50, Height = 50, Stroke = "#FF000000", StrokeThickness = 10, Visible = false };

        Assert.Equal(1, Decoupe.Marge([montre, cache]));
        cache.Visible = true;
        Assert.Equal(5, Decoupe.Marge([montre, cache]));
    }

    // ------------------------------------------------------------------ outils

    private static void Retirer(JsonNode noeud, string cle)
    {
        switch (noeud)
        {
            case JsonObject objet:
                objet.Remove(cle);
                foreach (var (_, enfant) in objet.ToList()) if (enfant is not null) Retirer(enfant, cle);
                break;
            case JsonArray tableau:
                foreach (var enfant in tableau) if (enfant is not null) Retirer(enfant, cle);
                break;
        }
    }

    private static System.Collections.Generic.IReadOnlyList<string> Exporter(Document document)
    {
        var chemin = Path.Combine(Path.GetTempPath(), $"i8-{Guid.NewGuid():N}.pdf");
        try
        {
            return PdfExporter.Export(document, chemin, ecran: null, date: new DateTime(2026, 9, 6)).Warnings.ToArray();
        }
        finally
        {
            if (File.Exists(chemin)) File.Delete(chemin);
        }
    }

    /// <summary>
    /// Le flux de dessin de la première page, décompressé — comme <c>FondsTests</c> le
    /// relit : PDFsharp relit son propre PDF.
    /// </summary>
    private static string Flux(Document document)
    {
        var chemin = Path.Combine(Path.GetTempPath(), $"i8-{Guid.NewGuid():N}.pdf");
        try
        {
            PdfExporter.Export(document, chemin, ecran: null, date: new DateTime(2026, 9, 6));
            using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(chemin, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify);
            var octets = pdf.Pages[0].Contents.Elements.GetDictionary(0)!.Stream.UnfilteredValue;
            return string.Concat(octets.Select(o => (char)o));
        }
        finally
        {
            if (File.Exists(chemin)) File.Delete(chemin);
        }
    }
}
