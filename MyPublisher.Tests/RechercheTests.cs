using System.Linq;
using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Rechercher et remplacer — poste N20 —, éprouvé dans le noyau : les occurrences, la casse, le
/// mot entier, et le remplacement qui recale les fragments comme une frappe (Q1), le texte
/// remplacé prenant le style de son premier caractère. La barre et ses touches sont éprouvées sur
/// la fenêtre (<c>RechercherRemplacerTests</c>).
/// </summary>
[Trait("poste", "N20")]
public class RechercheTests
{
    private static Plage[] Occurrences(string texte, string cherche, bool casse = false, bool mot = false) =>
        Recherche.Occurrences(texte, new CriteresDeRecherche(cherche, casse, mot)).ToArray();

    [Fact]
    public void Les_occurrences_vont_de_gauche_a_droite_sans_se_chevaucher()
    {
        Assert.Equal(new[] { new Plage(0, 2), new Plage(2, 2) }, Occurrences("aaaaa", "aa"));
        Assert.Equal(new[] { new Plage(4, 8), new Plage(18, 8) }, Occurrences("Mon bulletin, ton Bulletin", "bulletin"));
        Assert.Empty(Occurrences("Mon bulletin", ""));
        Assert.Empty(Occurrences("", "bulletin"));
    }

    [Fact]
    public void Respecter_la_casse_ecarte_les_majuscules_et_les_accents_comptent_toujours()
    {
        Assert.Equal(new[] { new Plage(4, 8) }, Occurrences("Mon bulletin, ton Bulletin", "bulletin", casse: true));
        Assert.Equal(new[] { new Plage(0, 5) }, Occurrences("Élève, eleve", "élève"));
        Assert.Equal(new[] { new Plage(7, 5) }, Occurrences("Élève, eleve", "eleve"));
    }

    [Fact]
    public void Mot_entier_ne_trouve_ni_le_debut_ni_le_milieu_d_un_mot()
    {
        const string texte = "bulle, bulletin, la bulle; bulle2";
        Assert.Equal(4, Occurrences(texte, "bulle").Length);
        Assert.Equal(new[] { new Plage(0, 5), new Plage(20, 5) }, Occurrences(texte, "bulle", mot: true));
        // Après une occurrence refusée, la recherche reprend juste après son début.
        Assert.Equal(new[] { new Plage(4, 2) }, Occurrences("aaa aa", "aa", mot: true));
    }

    [Fact]
    public void Une_plage_est_une_occurrence_selon_les_memes_criteres()
    {
        var criteres = new CriteresDeRecherche("bulletin");
        Assert.True(Recherche.EstUneOccurrence("Le Bulletin", new Plage(3, 8), criteres));
        Assert.False(Recherche.EstUneOccurrence("Le Bulletin", new Plage(3, 8), criteres with { RespecterLaCasse = true }));
        Assert.False(Recherche.EstUneOccurrence("Le Bulletin", new Plage(2, 8), criteres));
        Assert.False(Recherche.EstUneOccurrence("Le Bulletins", new Plage(3, 8), criteres with { MotEntier = true }));
        Assert.False(Recherche.EstUneOccurrence("Le", new Plage(3, 8), criteres));
    }

    [Fact]
    public void La_suivante_part_de_la_position_donnee()
    {
        var criteres = new CriteresDeRecherche("le");
        Assert.Equal(new Plage(0, 2), Recherche.Suivante("le bleu le", criteres, 0));
        Assert.Equal(new Plage(4, 2), Recherche.Suivante("le bleu le", criteres, 1));
        Assert.Equal(new Plage(8, 2), Recherche.Suivante("le bleu le", criteres, 5));
        Assert.Null(Recherche.Suivante("le bleu le", criteres, 9));
    }

    /// <summary>
    /// Le texte remplacé prend le style de son PREMIER caractère, même quand le remplacement
    /// commence comme lui : la déduction de Q1 aurait lu « bulletin » → « bulletins » comme un
    /// « s » tapé au bout du mot, qui aurait pris le style du « n ».
    /// </summary>
    [Fact]
    public void Le_texte_remplace_prend_le_style_de_son_premier_caractere()
    {
        // « Le bulletin du mois », « bul » en gras, « letin » en italique.
        var bloc = new TextFrame { Text = "Le bulletin du mois" };
        var gras = new StyleSpan { Start = 3, Length = 3, Bold = true };
        var italique = new StyleSpan { Start = 6, Length = 5, Italic = true };
        var mois = new StyleSpan { Start = 15, Length = 4, Bold = true };
        bloc.Spans.Add(gras);
        bloc.Spans.Add(italique);
        bloc.Spans.Add(mois);

        Recherche.Remplacer(bloc, new Plage(3, 8), "bulletins");

        Assert.Equal("Le bulletins du mois", bloc.Text);
        // Le fragment du premier caractère remplacé recouvre tout le remplacement ; l'autre, vidé, part.
        Assert.Equal(2, bloc.Spans.Count);
        Assert.Equal((3, 9), (gras.Start, gras.Length));
        Assert.DoesNotContain(italique, bloc.Spans);
        // Ce qui suit se décale d'autant.
        Assert.Equal((16, 4), (mois.Start, mois.Length));
    }

    [Fact]
    public void Un_remplacement_efface_ou_raccourci_recale_ce_qui_suit_et_garde_les_paragraphes()
    {
        var bloc = new TextFrame { Text = "un deux\ntrois deux\nquatre" };
        var trois = new StyleSpan { Start = 8, Length = 5, Bold = true };
        bloc.Spans.Add(trois);
        var ecart = new ParagraphSpan { Start = 2, Length = 1 };
        bloc.Paragraphs.Add(ecart);

        foreach (var plage in Recherche.AReboursDe(bloc.Text, new CriteresDeRecherche("deux")))
        {
            Recherche.Remplacer(bloc, plage, "2");
        }

        Assert.Equal("un 2\ntrois 2\nquatre", bloc.Text);
        Assert.Equal((5, 5), (trois.Start, trois.Length));
        Assert.Equal((2, 1), (ecart.Start, ecart.Length));
    }

    [Fact]
    public void Une_case_se_remplace_comme_un_bloc()
    {
        var case_ = new TableCell { Text = "Air Production" };
        var gras = new StyleSpan { Start = 0, Length = 3, Bold = true };
        case_.Spans.Add(gras);

        Recherche.Remplacer(case_, new Plage(0, 3), "Gaz");

        Assert.Equal("Gaz Production", case_.Text);
        Assert.Equal((0, 3), (gras.Start, gras.Length));
    }

    /// <summary>Un remplacement qui contient ce qu'on cherche ne se retrouve pas : à rebours, chaque occurrence une fois.</summary>
    [Fact]
    public void Remplacer_a_rebours_ne_reprend_pas_ce_qu_il_vient_d_ecrire()
    {
        var bloc = new TextFrame { Text = "a a a" };
        foreach (var plage in Recherche.AReboursDe(bloc.Text, new CriteresDeRecherche("a")))
        {
            Recherche.Remplacer(bloc, plage, "aa");
        }

        Assert.Equal("aa aa aa", bloc.Text);
    }
}
