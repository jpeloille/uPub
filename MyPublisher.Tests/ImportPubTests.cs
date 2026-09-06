using System.Collections.Generic;
using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// L'import d'un <c>.pub</c>, éprouvé sur des relevés écrits à la main.
///
/// C'est possible parce que <see cref="PubImporter"/> est pur : il prend le texte du
/// relevé et rend un document, sans toucher au disque ni lancer de processus. Ces
/// tests ne demandent donc pas que libmspub soit installé, et s'exécutent en quelques
/// millisecondes. L'appel réel à l'outil est éprouvé par la recette du poste, sur les
/// deux documents ATR du dépôt.
/// </summary>
public class ImportPubTests
{
    private static Document Importer(string releve) => PubImporter.Importer(releve).Document;

    private const string UnePage = """
        startDocument()
          startPage(svg:height: 11.6929in, svg:width: 8.2677in)
        """;

    [Fact]
    public void La_page_prend_le_format_du_document_dorigine()
    {
        var document = Importer(UnePage);

        // 8,2677 pouces = 595,3 pt : de l'A4, au centième de point près.
        Assert.Equal(595.27, document.PageWidth, precision: 1);
        Assert.Equal(841.89, document.PageHeight, precision: 1);
        Assert.Single(document.Pages);
    }

    [Fact]
    public void Chaque_page_du_releve_devient_une_page()
    {
        var document = Importer($"{UnePage}\n  endPage\n  startPage(svg:height: 11.6929in, svg:width: 8.2677in)\n  endPage");

        Assert.Equal(2, document.Pages.Count);
    }

    [Fact]
    public void Un_bloc_de_texte_reprend_sa_geometrie_et_ses_marges()
    {
        var document = Importer($"""
            {UnePage}
                startTextObject (fo:padding-bottom: 0.0400in, fo:padding-left: 0.0500in, fo:padding-right: 0.0600in, fo:padding-top: 0.0700in, svg:height: 1.0000in, svg:width: 2.0000in, svg:x: 0.5000in, svg:y: 1.5000in)
                  openParagraph (fo:text-align: left)
                    openSpan(fo:font-size: 0.1389in, style:font-name: Calibri)
                      insertText (Bonjour)
                    closeSpan
                  closeParagraph
                endTextObject
            """);

        var bloc = Assert.IsType<TextFrame>(document.Pages[0].Frames.Single());
        Assert.Equal(36, bloc.X, precision: 6);
        Assert.Equal(108, bloc.Y, precision: 6);
        Assert.Equal(144, bloc.Width, precision: 6);
        Assert.Equal(72, bloc.Height, precision: 6);
        Assert.Equal(3.6, bloc.PaddingLeft, precision: 6);
        Assert.Equal(5.04, bloc.PaddingTop, precision: 6);
        Assert.Equal("Bonjour", bloc.Text);
    }

    [Fact]
    public void Les_cles_du_releve_portent_elles_memes_un_deux_points()
    {
        // Le défaut qui a fait échouer le premier import : couper « svg:width: 2.0000in »
        // au premier deux-points donne la clé « svg » et la valeur « width: 2.0000in ».
        // Tous les arguments étaient alors perdus en silence, et les blocs importés
        // naissaient à l'origine avec une taille nulle.
        var document = Importer($"""
            {UnePage}
                startTextObject (svg:height: 1.0000in, svg:width: 2.0000in, svg:x: 0.5000in, svg:y: 1.5000in)
                  openParagraph ()
                    openSpan()
                      insertText (x)
                    closeSpan
                  closeParagraph
                endTextObject
            """);

        var bloc = Assert.IsType<TextFrame>(document.Pages[0].Frames.Single());
        Assert.NotEqual(0, bloc.Width);
        Assert.NotEqual(0, bloc.X);
    }

    [Fact]
    public void Le_style_dominant_devient_celui_du_bloc()
    {
        // Publisher ne pose rien sur la zone de texte : tout est dans les fragments.
        // Sans cette remontée, le bloc garderait la police par défaut de MyPublisher
        // alors que pas un caractère ne s'en sert.
        var document = Importer($"""
            {UnePage}
                startTextObject (svg:height: 1.0000in, svg:width: 4.0000in, svg:x: 0.0000in, svg:y: 0.0000in)
                  openParagraph ()
                    openSpan(fo:font-size: 0.1389in, fo:font-weight: bold, style:font-name: Calibri)
                      insertText (Titre)
                    closeSpan
                  closeParagraph
                  openParagraph ()
                    openSpan(fo:font-size: 0.1111in, style:font-name: Calibri)
                      insertText (Un corps de texte nettement plus long que le titre.)
                    closeSpan
                  closeParagraph
                endTextObject
            """);

        var bloc = Assert.IsType<TextFrame>(document.Pages[0].Frames.Single());
        Assert.Equal("Calibri", bloc.FontFamily);
        Assert.Equal(7.9992, bloc.FontSize, precision: 4);   // 0,1111 pouce
        Assert.False(bloc.Bold);
        // Le titre reste une exception ; le corps n'en est plus une.
        var exception = Assert.Single(bloc.Spans);
        Assert.True(exception.Bold);
    }

    [Fact]
    public void Un_paragraphe_porte_son_retrait_et_son_fer()
    {
        var document = Importer($"""
            {UnePage}
                startTextObject (svg:height: 1.0000in, svg:width: 4.0000in, svg:x: 0.0000in, svg:y: 0.0000in)
                  openParagraph (fo:margin-left: 0.1930in, fo:text-align: justify, fo:text-indent: -0.2183in)
                    openSpan(fo:font-size: 0.1389in, style:font-name: Calibri)
                      insertText (Une puce.)
                    closeSpan
                  closeParagraph
                endTextObject
            """);

        var bloc = Assert.IsType<TextFrame>(document.Pages[0].Frames.Single());
        var ecart = Assert.Single(bloc.Paragraphs);
        Assert.Equal(13.896, ecart.IndentLeft!.Value, precision: 3);
        Assert.Equal(-15.718, ecart.IndentFirstLine!.Value, precision: 3);
        Assert.Equal(TextAlign.Justify, ecart.Align);
    }

    [Fact]
    public void Un_paragraphe_ordinaire_ne_laisse_aucun_ecart()
    {
        // Un écart qui ne dit rien alourdirait le fichier et ferait croire à une
        // exception là où il n'y en a pas.
        var document = Importer($"""
            {UnePage}
                startTextObject (svg:height: 1.0000in, svg:width: 4.0000in, svg:x: 0.0000in, svg:y: 0.0000in)
                  openParagraph (fo:text-align: left)
                    openSpan()
                      insertText (Rien de particulier.)
                    closeSpan
                  closeParagraph
                endTextObject
            """);

        Assert.Empty(Assert.IsType<TextFrame>(document.Pages[0].Frames.Single()).Paragraphs);
    }

    [Fact]
    public void Lindice_et_lexposant_se_lisent_dans_la_position_du_texte()
    {
        var document = Importer($"""
            {UnePage}
                startTextObject (svg:height: 1.0000in, svg:width: 4.0000in, svg:x: 0.0000in, svg:y: 0.0000in)
                  openParagraph ()
                    openSpan(fo:font-size: 0.1389in)
                      insertText (H)
                    closeSpan
                    openSpan(fo:font-size: 0.0930in, style:text-position: -50% 67%)
                      insertText (2)
                    closeSpan
                    openSpan(fo:font-size: 0.1389in)
                      insertText (O)
                    closeSpan
                  closeParagraph
                endTextObject
            """);

        var bloc = Assert.IsType<TextFrame>(document.Pages[0].Frames.Single());
        Assert.Equal("H2O", bloc.Text);
        var indice = bloc.Spans.Single(f => f.BaselineShift != 0);
        Assert.Equal(-0.5, indice.BaselineShift, precision: 6);
        Assert.Equal(1, indice.Start);
        Assert.Equal(1, indice.Length);
    }

    [Fact]
    public void Une_forme_sans_fond_nen_recoit_pas_un_par_defaut()
    {
        // Une forme naît avec un fond bleu, ce qu'on veut à la souris et jamais à la
        // lecture d'un fichier : le premier import a rendu un document tout bleu.
        var document = Importer($"""
            {UnePage}
                setStyle(draw:fill: none, draw:stroke: solid, svg:stroke-color: #000000, svg:stroke-width: 0.0139in)
                  drawPolygon (svg:points: ((svg:x: 0.5000in, svg:y: 1.0000in), (svg:x: 2.5000in, svg:y: 1.0000in), (svg:x: 2.5000in, svg:y: 2.0000in), (svg:x: 0.5000in, svg:y: 2.0000in), (svg:x: 0.5000in, svg:y: 1.0000in)))
            """);

        var forme = Assert.IsType<ShapeFrame>(document.Pages[0].Frames.Single());
        Assert.Null(forme.Fill);
        Assert.Equal("#FF000000", forme.Stroke);
        Assert.Equal(36, forme.X, precision: 6);
        Assert.Equal(144, forme.Width, precision: 6);
        Assert.Equal(72, forme.Height, precision: 6);
    }

    /// <summary>
    /// Un dépôt d'images qui ne touche à rien : il retient ce qu'on lui donne et rend un
    /// chemin. C'est ce qui permet d'éprouver le remplissage par image sans écrire un
    /// octet, et sans que <see cref="PubImporter"/> cesse d'être pur.
    /// </summary>
    private sealed class DepotEnMemoire : IDepotImages
    {
        public List<(byte[] Donnees, string Mime)> Recues { get; } = [];

        public string? Deposer(byte[] donnees, string typeMime)
        {
            Recues.Add((donnees, typeMime));
            return $"/tmp/faux-{Recues.Count}.png";
        }
    }

    /// <summary>Un PNG de 1 × 1 pixel, en base64 — de quoi éprouver le décodage.</summary>
    private const string PngMinuscule =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    [Fact]
    public void Une_forme_remplie_par_une_image_la_porte()
    {
        // C'est ce que ce poste existe pour rendre : les cinq PNG d'ATA 21 ne sont pas
        // des blocs image, ce sont des remplissages de forme.
        var depot = new DepotEnMemoire();
        var rapport = PubImporter.Importer($"""
            {UnePage}
                setStyle(draw:fill: bitmap, draw:fill-image: {PngMinuscule}, draw:fill-image-ref-point: top-left, draw:stroke: none, librevenge:mime-type: image/png, style:repeat: stretch)
                  drawPolygon (svg:points: ((svg:x: 1.0000in, svg:y: 1.0000in), (svg:x: 3.0000in, svg:y: 1.0000in), (svg:x: 3.0000in, svg:y: 2.0000in), (svg:x: 1.0000in, svg:y: 2.0000in), (svg:x: 1.0000in, svg:y: 1.0000in)))
            """, depot: depot);

        var forme = Assert.IsType<ShapeFrame>(rapport.Document.Pages[0].Frames.Single());
        var fond = Assert.IsType<ImageFill>(forme.Fill);
        Assert.Equal("/tmp/faux-1.png", fond.Path);
        // « style:repeat: stretch » étire l'image sur la forme : c'est le cadrage « Remplir ».
        Assert.Equal(ImageFit.Fill, fond.Fit);
        Assert.Equal(0, fond.Rotation);
        Assert.Empty(rapport.Warnings);

        // L'image est bien passée au dépôt, décodée, avec son type.
        var (donnees, mime) = Assert.Single(depot.Recues);
        Assert.Equal("image/png", mime);
        Assert.Equal(0x89, donnees[0]);
        Assert.Equal("PNG", System.Text.Encoding.ASCII.GetString(donnees, 1, 3));
    }

    [Fact]
    public void Une_image_que_Publisher_fait_pivoter_garde_son_quart_de_tour()
    {
        // Une des cinq images d'ATA 21 porte « librevenge:rotate: 90 » : son PNG est
        // couché et son cadre debout. Perdre la rotation la rendrait couchée ET écrasée
        // d'un facteur trois.
        var rapport = PubImporter.Importer($"""
            {UnePage}
                setStyle(draw:fill: bitmap, draw:fill-image: {PngMinuscule}, draw:stroke: none, librevenge:mime-type: image/png, librevenge:rotate: 90, style:repeat: stretch)
                  drawPolygon (svg:points: ((svg:x: 1.0000in, svg:y: 1.0000in), (svg:x: 3.0000in, svg:y: 1.0000in), (svg:x: 3.0000in, svg:y: 2.0000in), (svg:x: 1.0000in, svg:y: 2.0000in), (svg:x: 1.0000in, svg:y: 1.0000in)))
            """, depot: new DepotEnMemoire());

        var forme = Assert.IsType<ShapeFrame>(rapport.Document.Pages[0].Frames.Single());
        Assert.Equal(90, Assert.IsType<ImageFill>(forme.Fill).Rotation);
    }

    [Fact]
    public void Une_image_sans_depot_ou_ne_se_deposant_pas_est_dite()
    {
        // Sans dépôt, l'import n'a nulle part où écrire l'image : il le dit plutôt que de
        // rendre une forme au fond vide dont personne ne saurait qu'elle en avait un.
        var rapport = PubImporter.Importer($"""
            {UnePage}
                setStyle(draw:fill: bitmap, draw:fill-image: {PngMinuscule}, draw:stroke: none, librevenge:mime-type: image/png, style:repeat: stretch)
                  drawPolygon (svg:points: ((svg:x: 1.0000in, svg:y: 1.0000in), (svg:x: 3.0000in, svg:y: 1.0000in), (svg:x: 3.0000in, svg:y: 2.0000in), (svg:x: 1.0000in, svg:y: 2.0000in), (svg:x: 1.0000in, svg:y: 1.0000in)))
            """);

        var forme = Assert.IsType<ShapeFrame>(rapport.Document.Pages[0].Frames.Single());
        Assert.Null(forme.Fill);
        Assert.Contains(rapport.Warnings, a => a.Contains("Remplissage par image perdu"));
    }

    [Fact]
    public void Une_couleur_unie_reste_un_remplissage_uni()
    {
        var document = Importer($"""
            {UnePage}
                setStyle(draw:fill: solid, draw:fill-color: #336699, draw:stroke: none)
                  drawPolygon (svg:points: ((svg:x: 1.0000in, svg:y: 1.0000in), (svg:x: 3.0000in, svg:y: 1.0000in), (svg:x: 3.0000in, svg:y: 2.0000in), (svg:x: 1.0000in, svg:y: 2.0000in), (svg:x: 1.0000in, svg:y: 1.0000in)))
            """);

        var forme = Assert.IsType<ShapeFrame>(document.Pages[0].Frames.Single());
        Assert.Equal("#FF336699", Assert.IsType<SolidFill>(forme.Fill).Color);
    }

    [Fact]
    public void Un_trait_en_pointilles_garde_ses_tirets()
    {
        var document = Importer($"""
            {UnePage}
                setStyle(draw:fill: none, draw:stroke: dash, svg:stroke-color: #000000, svg:stroke-width: 0.0104in)
                  drawPath (svg:d: ((librevenge:path-action: M, svg:x: 0.0000in, svg:y: 2.0000in), (librevenge:path-action: L, svg:x: 8.0000in, svg:y: 2.0000in)))
            """);

        var trait = Assert.IsType<ShapeFrame>(document.Pages[0].Frames.Single());
        Assert.Equal(StrokeStyle.Dashed, trait.StrokeStyle);
        Assert.Equal(576, trait.Width, precision: 6);
        Assert.Equal(0, trait.Height, precision: 6);
    }

    [Fact]
    public void Une_bande_de_trois_cases_devient_un_tableau()
    {
        var document = Importer($"""
            {UnePage}
                startTableObject(librevenge:table-columns: ((style:column-width: 2.0000in), (style:column-width: 3.0000in), (style:column-width: 1.0000in)), svg:height: 0.5000in, svg:width: 6.0000in, svg:x: 1.0000in, svg:y: 4.0000in)
                  openTableRow(librevenge:row-height: 0.5000in)
                    openTableCell(librevenge:column: 0, librevenge:row: 0)
                      openParagraph ()
                        openSpan()
                          insertText (Gauche)
                        closeSpan
                      closeParagraph
                    closeTableCell
                    openTableCell(librevenge:column: 1, librevenge:row: 0)
                    closeTableCell
                    openTableCell(librevenge:column: 2, librevenge:row: 0)
                      openParagraph ()
                        openSpan()
                          insertText (Droite)
                        closeSpan
                      closeParagraph
                    closeTableCell
                  closeTableRow
                endTableObject()
            """);

        var tableau = Assert.IsType<TableFrame>(document.Pages[0].Frames.Single());
        Assert.Equal(3, tableau.ColumnCount);
        Assert.Equal(1, tableau.RowCount);
        Assert.Equal(144, tableau.Columns[0].Width, precision: 6);
        Assert.Equal(216, tableau.Columns[1].Width, precision: 6);
        Assert.Equal("Gauche", tableau.Cells.Single(c => c.Column == 0).Text);
        Assert.Equal("", tableau.Cells.Single(c => c.Column == 1).Text);
        Assert.Equal("Droite", tableau.Cells.Single(c => c.Column == 2).Text);
    }

    [Fact]
    public void Une_tabulation_traverse_limport()
    {
        // Les fiches de procédures en portent 157 : les perdre collerait les consignes.
        var rapport = PubImporter.Importer($"""
            {UnePage}
                startTextObject (svg:height: 1.0000in, svg:width: 4.0000in, svg:x: 0.0000in, svg:y: 0.0000in)
                  openParagraph ()
                    openSpan()
                      insertText (ALT)
                    closeSpan
                    insertTab
                    openSpan()
                      insertText (MAINTAIN)
                    closeSpan
                  closeParagraph
                endTextObject
            """);

        var bloc = Assert.IsType<TextFrame>(rapport.Document.Pages[0].Frames.Single());
        Assert.Equal("ALT\tMAINTAIN", bloc.Text);
        Assert.Contains(rapport.Warnings, a => a.Contains("tabulation"));
    }

    [Fact]
    public void Ce_que_le_modele_ne_sait_pas_porter_est_dit()
    {
        // Un import muet sur ce qu'il perd est pire qu'un import qui échoue.
        //
        // Ce test portait sur l'alignement vertical, puis sur le remplissage par image :
        // les postes A7 et C5c les ont l'un après l'autre fait entrer dans le modèle, et
        // l'avertissement a disparu — comme il devait. Il porte maintenant sur un chemin
        // à plus de deux points, que le modèle ramène à un trait entre ses extrémités.
        var rapport = PubImporter.Importer($"""
            {UnePage}
                setStyle(draw:fill: none, draw:stroke: solid, svg:stroke-color: #000000)
                drawPath (svg:d: ((librevenge:path-action: M, svg:x: 0.0000in, svg:y: 0.0000in), (librevenge:path-action: L, svg:x: 1.0000in, svg:y: 1.0000in), (librevenge:path-action: L, svg:x: 2.0000in, svg:y: 0.0000in)))
            """);

        Assert.Contains(rapport.Warnings, a => a.Contains("Chemin"));
    }

    [Theory]
    [InlineData("middle", VerticalAlign.Middle)]
    [InlineData("bottom", VerticalAlign.Bottom)]
    [InlineData(null, VerticalAlign.Top)]
    public void Le_calage_vertical_traverse_limport(string? valeur, VerticalAlign attendu)
    {
        // Le relevé des fiches de limitations porte « draw:textarea-vertical-align:
        // middle » une fois, sur un bandeau de titre. L'import le disait perdu jusqu'au
        // poste A7 ; le modèle le porte désormais, et l'avertissement n'a plus lieu
        // d'être. L'absence de la clé vaut « en haut », le défaut de Publisher.
        var calage = valeur is null ? "" : $"draw:textarea-vertical-align: {valeur}, ";
        var rapport = PubImporter.Importer($"""
            {UnePage}
                startTextObject ({calage}svg:height: 1.0000in, svg:width: 4.0000in, svg:x: 0.0000in, svg:y: 0.0000in)
                  openParagraph ()
                    openSpan()
                      insertText (x)
                    closeSpan
                  closeParagraph
                endTextObject
            """);

        var bloc = Assert.IsType<TextFrame>(rapport.Document.Pages[0].Frames.Single());
        Assert.Equal(attendu, bloc.VerticalAlign);
        Assert.DoesNotContain(rapport.Warnings, a => a.Contains("Alignement vertical"));
    }

    [Fact]
    public void Un_releve_vide_rend_un_document_ouvrable()
    {
        // Un document sans page ne s'affiche pas ; mieux vaut une page blanche qu'un
        // plantage à l'ouverture.
        var document = Importer("startDocument()\nendDocument()");

        Assert.Single(document.Pages);
    }
}
