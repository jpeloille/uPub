using System.Linq;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// L'import garde le fer des cases et des paragraphes à gauche (poste Q26), éprouvé sur des
/// relevés écrits à la main, au modèle de ceux que <c>pub2raw</c> donne des deux documents réels.
///
/// Deux défauts, trouvés par R6 : une case dont le bloc de texte n'est pas ouvert dans la source
/// recevait son texte sans le fer de son paragraphe — les seize valeurs centrées des Fiches
/// arrivaient au fer à gauche — ; et un paragraphe au fer à gauche sans autre réglage, n'ayant pas
/// d'écart, prenait le fer du bloc — dans un bloc justifié en majorité, il passait justifié. Les
/// documents réels eux-mêmes sont confrontés à leur relevé par
/// <c>MyPublisher.Tests.Interface/FerALImportDesDocumentsReelsTests</c> et par la recette.
/// </summary>
[Trait("poste", "Q26")]
public class FerALImportTests
{
    private static Document Importer(string releve) => PubImporter.Importer(releve).Document;

    private const string UnePage = """
        startDocument()
          startPage(svg:height: 11.6929in, svg:width: 8.2677in)
        """;

    private static string Paragraphe(string reglages, string texte) => $"""
                  openParagraph ({reglages})
                    openSpan(fo:font-size: 0.1389in, style:font-name: Calibri)
                      insertText ({texte})
                    closeSpan
                  closeParagraph
        """;

    private static string Bloc(params string[] paragraphes) => $"""
                startTextObject (svg:height: 2.0000in, svg:width: 4.0000in, svg:x: 0.0000in, svg:y: 0.0000in)
        {string.Join("\n", paragraphes)}
                endTextObject
        """;

    /// <summary>Les fers que la composition lit, paragraphe par paragraphe.</summary>
    private static TextAlign[] Fers(TextFrame bloc) =>
        ParagrapheEffectif.Resoudre(Recalage.NombreDeParagraphes(bloc.Text), bloc.Paragraphs, bloc.Paragraph)
            .Select(p => p.Align).ToArray();

    /// <summary>
    /// Une bande des Fiches, telle que libmspub la relève : trois cases sans bloc de texte, dont
    /// les paragraphes sont ouverts dans la case même, et la valeur du milieu centrée.
    /// </summary>
    [Fact]
    public void Une_case_sans_bloc_de_texte_prend_le_fer_de_son_paragraphe()
    {
        var document = Importer($"""
            {UnePage}
                startTableObject(librevenge:table-columns: ((style:column-width: 0.4860in), (style:column-width: 2.2286in), (style:column-width: 0.5220in)), svg:height: 0.2565in, svg:width: 3.2365in, svg:x: 0.9843in, svg:y: 0.8169in)
                  openTableRow(librevenge:row-height: 0.2295in)
                    openTableCell(librevenge:column: 0, librevenge:row: 0)
                      openParagraph (fo:text-align: left)
                      closeParagraph
                    closeTableCell()
                    openTableCell(librevenge:column: 1, librevenge:row: 0)
                      openParagraph (fo:text-align: center)
                        openSpan(fo:font-size: 0.1389in, style:font-name: Verdana Pro Cond SemiBold)
                          insertText (DC GEN 1+2 FAULT)
                        closeSpan
                      closeParagraph
                    closeTableCell()
                    openTableCell(librevenge:column: 2, librevenge:row: 0)
                      openParagraph (fo:text-align: right)
                      closeParagraph
                    closeTableCell()
                  closeTableRow()
                endTableObject()
            """);

        var tableau = Assert.IsType<TableFrame>(document.Pages[0].Frames.Single());
        var milieu = tableau.Cells.Single(c => c.Column == 1);
        Assert.Equal("DC GEN 1+2 FAULT", milieu.Text);
        Assert.Equal(TextAlign.Center, milieu.Align);
        Assert.Equal(TextAlign.Left, tableau.Cells.Single(c => c.Column == 0).Align);
        // Une case vide garde le fer de son paragraphe : on y tapera au bon endroit.
        Assert.Equal(TextAlign.Right, tableau.Cells.Single(c => c.Column == 2).Align);
        // Et le bloc de la case, que l'écran et l'export dessinent, centre la valeur.
        Assert.Equal(TextAlign.Center, tableau.CellFrames.OfType<TextFrame>().Single(b => b.Text == "DC GEN 1+2 FAULT").Align);
    }

    /// <summary>Une case n'a qu'un fer : celui de son premier paragraphe. Sans paragraphe, le fer à gauche.</summary>
    [Fact]
    public void Une_case_a_plusieurs_paragraphes_prend_le_fer_du_premier()
    {
        var document = Importer($"""
            {UnePage}
                startTableObject(librevenge:table-columns: ((style:column-width: 2.0000in), (style:column-width: 2.0000in)), svg:height: 0.5000in, svg:width: 4.0000in, svg:x: 1.0000in, svg:y: 1.0000in)
                  openTableRow(librevenge:row-height: 0.5000in)
                    openTableCell(librevenge:column: 0, librevenge:row: 0)
            {Paragraphe("fo:text-align: justify", "Premier")}
            {Paragraphe("fo:text-align: center", "Second")}
            {Paragraphe("fo:text-align: center", "Troisième")}
                    closeTableCell()
                    openTableCell(librevenge:column: 1, librevenge:row: 0)
                    closeTableCell()
                  closeTableRow()
                endTableObject()
            """);

        var tableau = Assert.IsType<TableFrame>(document.Pages[0].Frames.Single());
        Assert.Equal(TextAlign.Justify, tableau.Cells.Single(c => c.Column == 0).Align);
        Assert.Equal(TextAlign.Left, tableau.Cells.Single(c => c.Column == 1).Align);
    }

    /// <summary>
    /// Le premier bloc d'ATA 21 : un intitulé au fer à gauche sans autre réglage, puis des
    /// paragraphes justifiés. Le bloc est justifié, et l'intitulé garde son fer, par un écart
    /// qui ne dit que lui.
    /// </summary>
    [Fact]
    public void Un_paragraphe_a_gauche_dans_un_bloc_justifie_garde_son_fer()
    {
        var document = Importer($"""
            {UnePage}
            {Bloc(
                Paragraphe("fo:text-align: left", "Air Production"),
                Paragraphe("fo:margin-left: 0.1930in, fo:text-align: justify, fo:text-indent: -0.2183in", "Two packs."),
                Paragraphe("fo:margin-left: 0.1930in, fo:text-align: justify, fo:text-indent: -0.2183in", "The left pack."),
                Paragraphe("fo:text-align: left", "Note"),
                Paragraphe("fo:text-align: justify", "The right pack."))}
            """);

        var bloc = Assert.IsType<TextFrame>(document.Pages[0].Frames.Single());
        Assert.Equal(TextAlign.Justify, bloc.Align);
        Assert.Equal([TextAlign.Left, TextAlign.Justify, TextAlign.Justify, TextAlign.Left, TextAlign.Justify], Fers(bloc));

        // L'écart de l'intitulé ne dit que son fer : rien d'autre ne s'écarte du bloc.
        var intitule = bloc.Paragraphs.Single(p => p.Start == 0);
        Assert.Equal(1, intitule.Length);
        Assert.Equal(TextAlign.Left, intitule.Align);
        Assert.True(intitule.IndentLeft is null or 0 && intitule.IndentFirstLine is null or 0 &&
                    intitule.SpaceBefore is null or 0 && intitule.SpaceAfter is null or 0);

        // Les écarts sont rangés par paragraphe, comme l'interface les pose.
        Assert.Equal(bloc.Paragraphs.Select(p => p.Start).Order(), bloc.Paragraphs.Select(p => p.Start));
    }

    /// <summary>
    /// Le fer du bloc est le plus répandu de TOUS ses paragraphes, et non des seuls qui portent un
    /// écart : quatre paragraphes ordinaires et un centré font un bloc au fer à gauche, sans écart
    /// pour les quatre — le centré n'impose pas son fer à ceux qui n'en disent rien.
    /// </summary>
    [Fact]
    public void Le_fer_du_bloc_est_celui_de_la_plupart_de_ses_paragraphes()
    {
        var document = Importer($"""
            {UnePage}
            {Bloc(
                Paragraphe("fo:text-align: left", "Un"),
                Paragraphe("fo:text-align: center", "Titre"),
                Paragraphe("fo:text-align: left", "Deux"),
                Paragraphe("fo:text-align: left", "Trois"),
                Paragraphe("fo:text-align: left", "Quatre"))}
            """);

        var bloc = Assert.IsType<TextFrame>(document.Pages[0].Frames.Single());
        Assert.Equal(TextAlign.Left, bloc.Align);
        var centre = Assert.Single(bloc.Paragraphs);
        Assert.Equal((1, TextAlign.Center), (centre.Start, centre.Align!.Value));
        Assert.Equal([TextAlign.Left, TextAlign.Center, TextAlign.Left, TextAlign.Left, TextAlign.Left], Fers(bloc));
    }

    /// <summary>
    /// Ce qui n'était pas faux ne bouge pas : un bloc au fer à gauche sans réglage n'a toujours
    /// aucun écart, et un paragraphe qui porte un retrait et le fer du bloc garde son écart tel quel.
    /// </summary>
    [Fact]
    public void Un_bloc_sans_fer_divergent_ne_recoit_aucun_ecart_de_plus()
    {
        var document = Importer($"""
            {UnePage}
            {Bloc(
                Paragraphe("fo:text-align: left", "Un"),
                Paragraphe("fo:margin-left: 0.5000in, fo:text-align: left", "Retrait"),
                Paragraphe("fo:text-align: left", "Deux"))}
            """);

        var bloc = Assert.IsType<TextFrame>(document.Pages[0].Frames.Single());
        Assert.Equal(TextAlign.Left, bloc.Align);
        var retrait = Assert.Single(bloc.Paragraphs);
        Assert.Equal(1, retrait.Start);
        Assert.Equal(36, retrait.IndentLeft!.Value, precision: 6);
    }
}
