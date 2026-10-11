using System.Linq;
using System.Text;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Lire un fichier texte pour remplir un bloc — le poste B2, côté noyau : l'encodage
/// deviné, et les fins de ligne. Les octets sont écrits dans le test, sans fichier : ce
/// qu'on éprouve est le décodage, et le disque n'y ajoute rien.
///
/// Le branchement — le bloc rempli, le style, l'annulation — est éprouvé dans la vraie
/// fenêtre, par <c>ImportTexteTests</c>.
/// </summary>
[Trait("poste", "B2")]
public class TexteImporteTests
{
    private const string Accents = "Été à Nouméa : l'élève a reçu sa clé, où qu'il soit — « déjà » vu.";

    [Fact]
    public void Lutf8_sans_marque_se_lit_en_utf8()
    {
        var lu = TexteImporte.Decoder(new UTF8Encoding(false).GetBytes(Accents));

        Assert.Equal(Accents, lu.Texte);
        Assert.Equal("UTF-8", lu.Encodage);
    }

    [Fact]
    public void La_marque_utf8_est_retiree()
    {
        var octets = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(Accents)).ToArray();
        Assert.Equal(0xEF, octets[0]);

        var lu = TexteImporte.Decoder(octets);

        // Gardée, la marque deviendrait un caractère invisible en tête du bloc, que la
        // composition mesurerait et que la saisie laisserait effacer sans rien montrer.
        Assert.Equal(Accents, lu.Texte);
        Assert.Equal("UTF-8 avec BOM", lu.Encodage);
    }

    [Fact]
    public void Un_fichier_latin1_garde_ses_accents()
    {
        // « Été à Nouméa » en Latin-1 n'est pas de l'UTF-8 valide : É s'y écrit C9, qui
        // ouvre une séquence de deux octets que « t » ne continue pas.
        // Le tiret cadratin n'existe pas en Latin-1 ; les guillemets, si.
        var latin = Accents.Replace("—", "-");
        var octets = Encoding.Latin1.GetBytes(latin);
        Assert.Equal(latin, Encoding.Latin1.GetString(octets));

        var lu = TexteImporte.Decoder(octets);

        Assert.Equal(latin, lu.Texte);
        Assert.Equal("Latin-1", lu.Encodage);
        Assert.DoesNotContain('�', lu.Texte);
    }

    [Fact]
    public void Le_latin1_de_windows_garde_ses_apostrophes_et_son_euro()
    {
        // Les octets 80 à 9F : des caractères de commande en Latin-1 strict, que nul
        // texte n'emploie ; en Windows-1252, ce qu'un texte français écrit sous Windows
        // emploie partout. 92 « ’ », 9C « œ », 80 « € », 85 « … ».
        byte[] octets = [0x4C, 0x92, 0x9C, 0x75, 0x66, 0x20, 0x80, 0x85];

        var lu = TexteImporte.Decoder(octets);

        Assert.Equal("L’œuf €…", lu.Texte);
        Assert.Equal("Latin-1", lu.Encodage);
    }

    [Fact]
    public void Un_texte_ascii_se_lit_en_utf8()
    {
        // L'ASCII est de l'UTF-8 valide : c'est le cas le plus courant, et il ne doit
        // pas être annoncé comme Latin-1.
        var lu = TexteImporte.Decoder(Encoding.ASCII.GetBytes("Plain text"));

        Assert.Equal("Plain text", lu.Texte);
        Assert.Equal("UTF-8", lu.Encodage);
    }

    [Fact]
    public void Apres_la_marque_utf8_une_sequence_invalide_ne_fait_pas_tout_relire_en_latin1()
    {
        // La marque est une déclaration : on la croit. Relire en Latin-1 ferait
        // apparaître la marque elle-même en tête, sous la forme « ï»¿ ».
        byte[] octets = [0xEF, 0xBB, 0xBF, 0x61, 0xFF, 0x62];

        var lu = TexteImporte.Decoder(octets);

        Assert.Equal("a�b", lu.Texte);
        Assert.Equal("UTF-8 avec BOM", lu.Encodage);
    }

    [Fact]
    public void Les_fins_de_ligne_crlf_deviennent_un_seul_saut()
    {
        var lu = TexteImporte.Decoder(Encoding.UTF8.GetBytes("un\r\ndeux\r\n\r\ntrois"));

        Assert.Equal("un\ndeux\n\ntrois", lu.Texte);
        Assert.DoesNotContain('\r', lu.Texte);
    }

    [Fact]
    public void Un_retour_chariot_seul_est_un_saut()
    {
        // Les anciens Mac : « \r » seul. La composition le compte déjà pour un saut ;
        // la zone de saisie, elle, doit voir le même texte que la page.
        Assert.Equal("un\ndeux\ntrois", TexteImporte.Normaliser("un\rdeux\r\ntrois"));
    }

    [Fact]
    public void Le_saut_qui_clot_le_fichier_nouvre_pas_de_paragraphe()
    {
        Assert.Equal("une ligne", TexteImporte.Normaliser("une ligne\r\n"));
        Assert.Equal("une ligne", TexteImporte.Normaliser("une ligne\n"));

        // Un seul : une ligne vide voulue en fin de fichier en garde une.
        Assert.Equal("une ligne\n", TexteImporte.Normaliser("une ligne\n\n"));
    }

    [Fact]
    public void Un_fichier_vide_donne_un_texte_vide()
    {
        Assert.Equal("", TexteImporte.Decoder([]).Texte);
    }
}
