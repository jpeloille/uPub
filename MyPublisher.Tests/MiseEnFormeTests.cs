using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// La mise en forme d'une plage, dans le noyau — le poste N2.
///
/// Les premiers cas sont ceux que la recette de PLAN.md écrit, chiffre pour chiffre. Les
/// suivants sont les bornes que le poste nomme, puis les pièges : l'écriture par la
/// collection, la normalisation, la charte, et deux balayages qui éprouvent la règle
/// d'ensemble — hors de la plage, rien ne change à l'œil.
/// </summary>
[Trait("poste", "N2")]
public class MiseEnFormeTests
{
    /// <summary>Un fragment en valeurs, pour comparer une collection d'un coup d'œil.</summary>
    private sealed record F(
        int Start, int Length, string? FontFamily = null, double? FontSize = null, bool? Bold = null,
        bool? Italic = null, Souligne? Underline = null, string? Foreground = null, double BaselineShift = 0)
    {
        public static F De(StyleSpan s) =>
            new(s.Start, s.Length, s.FontFamily, s.FontSize, s.Bold, s.Italic, s.Underline, s.Foreground, s.BaselineShift);

        public StyleSpan Fragment() => new()
        {
            Start = Start, Length = Length, FontFamily = FontFamily, FontSize = FontSize, Bold = Bold,
            Italic = Italic, Underline = Underline, Foreground = Foreground, BaselineShift = BaselineShift,
        };
    }

    /// <summary>Un écart de paragraphe en valeurs.</summary>
    private sealed record E(
        int Start, int Length, double? IndentLeft = null, double? IndentRight = null, double? IndentFirstLine = null,
        double? SpaceBefore = null, double? SpaceAfter = null, TextAlign? Align = null)
    {
        public static E De(ParagraphSpan p) =>
            new(p.Start, p.Length, p.IndentLeft, p.IndentRight, p.IndentFirstLine, p.SpaceBefore, p.SpaceAfter, p.Align);

        public ParagraphSpan Ecart() => new()
        {
            Start = Start, Length = Length, IndentLeft = IndentLeft, IndentRight = IndentRight,
            IndentFirstLine = IndentFirstLine, SpaceBefore = SpaceBefore, SpaceAfter = SpaceAfter, Align = Align,
        };
    }

    private static List<F> Valeurs(IEnumerable<StyleSpan> fragments) => fragments.Select(F.De).ToList();

    private static List<E> Valeurs(IEnumerable<ParagraphSpan> ecarts) => ecarts.Select(E.De).ToList();

    private static ObservableCollection<StyleSpan> Fragments(params F[] fragments) =>
        new(fragments.Select(f => f.Fragment()));

    private static ObservableCollection<ParagraphSpan> Ecarts(params E[] ecarts) => new(ecarts.Select(e => e.Ecart()));

    /// <summary>Le style d'un bloc neuf, avant la charte : DejaVu Sans 11, maigre, droit, sans souligné.</summary>
    private static readonly StyleEffectif Bloc =
        new("DejaVu Sans", 11, Bold: false, Italic: false, Souligne.Aucun, "#FF1A1A1A", 0);

    /// <summary>Ce que <see cref="MiseEnForme.Lire"/> rend d'une plage au style du bloc : tout, rien de mixte.</summary>
    private static readonly StyleDeLaPlage StyleDuBloc = new()
    {
        FontFamily = "DejaVu Sans", FontSize = 11, Bold = false, Italic = false, Underline = Souligne.Aucun,
        Foreground = "#FF1A1A1A", BaselineShift = 0,
    };

    private static readonly ParagrapheEffectif Paragraphe = new(0, 0, 0, 0, 0, TextAlign.Left);

    /// <summary>Le style effectif de chaque caractère, tel que la composition le découpe.</summary>
    private static StyleEffectif[] Styles(string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun) =>
        StyleEffectif.Decouper(texte, fragments, commun).SelectMany(m => Enumerable.Repeat(m.Style, m.Texte.Length))
            .ToArray();

    // ------------------------------------------------------------- la recette de PLAN.md

    [Fact]
    public void Le_gras_pose_sur_def_donne_un_seul_fragment_et_rebascule_sans_rien_laisser()
    {
        const string texte = "abc def";
        var fragments = Fragments();

        Assert.True(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(4, 3), new StyleDeLaPlage { Bold = true }));
        Assert.Equal([new F(4, 3, Bold: true)], Valeurs(fragments));

        Assert.True(MiseEnForme.Basculer(texte, fragments, Bloc, new Plage(4, 3), Bascule.Gras));
        Assert.Empty(fragments);

        // Et la bascule seule fait l'aller et le retour.
        Assert.True(MiseEnForme.Basculer(texte, fragments, Bloc, new Plage(4, 3), Bascule.Gras));
        Assert.Equal([new F(4, 3, Bold: true)], Valeurs(fragments));
        Assert.True(MiseEnForme.Basculer(texte, fragments, Bloc, new Plage(4, 3), Bascule.Gras));
        Assert.Empty(fragments);
    }

    [Fact]
    public void Une_plage_a_cheval_sur_deux_fragments_importes_qui_se_recouvrent_garde_les_styles_hors_de_la_plage()
    {
        // Deux fragments d'import, chacun avec toutes ses valeurs écrites, qui se
        // recouvrent sur « cd ef » : le second l'emporte, comme dans « Decouper ».
        const string texte = "ab cd ef gh ij";
        var fragments = Fragments(
            new F(0, 8, "Calibri", 10, Bold: true, Italic: false, Foreground: "#FF000000"),
            new F(3, 8, "Calibri", 12, Bold: false, Italic: true, Foreground: "#FFCC0000"));
        var avant = Styles(texte, fragments, Bloc);

        // La plage [6, 9) : « ef » des deux, puis l'espace du second seul.
        Assert.True(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(6, 3), new StyleDeLaPlage { Underline = Souligne.Simple }));

        var apres = Styles(texte, fragments, Bloc);
        for (var i = 0; i < texte.Length; i++)
        {
            var attendu = i is >= 6 and < 9 ? avant[i] with { Underline = Souligne.Simple } : avant[i];
            Assert.True(attendu == apres[i], $"caractère {i} « {texte[i]} » : {apres[i]} au lieu de {attendu}");
        }
    }

    [Fact]
    public void Un_gras_explicite_retire_sur_un_bloc_gras_devient_Bold_false()
    {
        // « Retirer » le gras, c'est poser « maigre », pas « comme le bloc » : sur un bloc
        // gras, nul rendrait le gras qu'on vient d'ôter.
        const string texte = "abc def";
        var gras = Bloc with { Bold = true };
        var fragments = Fragments(new F(4, 3, Bold: true));

        Assert.True(MiseEnForme.Basculer(texte, fragments, gras, new Plage(4, 3), Bascule.Gras));

        Assert.Equal([new F(4, 3, Bold: false)], Valeurs(fragments));
    }

    [Fact]
    public void Le_centrage_pose_sur_le_deuxieme_paragraphe_puis_sur_le_troisieme_fusionne()
    {
        const string texte = "Un\nDeux\nTrois";
        var ecarts = Ecarts();
        var centre = new ReglagesDeLaPlage { Align = TextAlign.Center };

        Assert.True(MiseEnForme.Poser(texte, ecarts, Paragraphe, new Plage(1, 1), centre));
        Assert.Equal([new E(1, 1, Align: TextAlign.Center)], Valeurs(ecarts));

        Assert.True(MiseEnForme.Poser(texte, ecarts, Paragraphe, new Plage(2, 1), centre));
        Assert.Equal([new E(1, 2, Align: TextAlign.Center)], Valeurs(ecarts));
    }

    // ------------------------------------------------------------- les bornes

    [Fact]
    public void Une_plage_vide_ne_change_rien_et_se_lit_au_style_de_la_frappe()
    {
        // a0 b1 c2 ␣3 d4 e5 f6 ⏎7 G8 h9 i10 : « def » en gras, « Ghi » en italique.
        const string texte = "abc def\nGhi";
        var fragments = Fragments(new F(4, 3, Bold: true), new F(8, 3, Italic: true));
        var notifications = 0;
        fragments.CollectionChanged += (_, _) => notifications++;

        Assert.False(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(5, 0), new StyleDeLaPlage { FontSize = 20 }));
        Assert.False(MiseEnForme.Basculer(texte, fragments, Bloc, new Plage(5, 0), Bascule.Gras));
        Assert.False(MiseEnForme.Retirer(texte, fragments, Bloc, new Plage(5, 0)));
        Assert.Equal(0, notifications);

        // Le style que prendrait la frappe (Recalage.Ancre) : le caractère qui précède — « d »
        // gras, « f » gras au bout du mot, l'espace devant lui —, celui qui suit en tête de
        // paragraphe — « G » italique —, le bloc dans un texte vide.
        Assert.Equal(true, Lu(5).Bold);
        Assert.Equal(true, Lu(7).Bold);
        Assert.Equal(false, Lu(4).Bold);
        Assert.Equal(true, Lu(8).Italic);
        Assert.True(MiseEnForme.Allume(texte, fragments, Bloc, new Plage(5, 0), Bascule.Gras));
        Assert.Equal(StyleDuBloc, MiseEnForme.Lire("", new List<StyleSpan>(), Bloc, new Plage(0, 0), Charte.Defaut));

        StyleDeLaPlage Lu(int position) =>
            MiseEnForme.Lire(texte, fragments, Bloc, new Plage(position, 0), Charte.Defaut);
    }

    [Fact]
    public void Une_plage_a_la_fin_du_texte_coupe_un_fragment_qui_deborde_et_se_recolle()
    {
        const string texte = "abc def";

        // Un fragment d'import qui déborde du texte : la composition ne le dessine que
        // jusqu'au bout. Coupé à la borne de la plage, il ne garde que ce qu'elle voyait.
        var deborde = Fragments(new F(4, 50, Italic: true));
        Assert.True(MiseEnForme.Poser(texte, deborde, Bloc, new Plage(5, 2), new StyleDeLaPlage { Bold = true }));
        Assert.Equal([new F(4, 1, Italic: true), new F(5, 2, Bold: true, Italic: true)], Valeurs(deborde));

        // Une plage qui finit au bout du texte se recolle au fragment qui la précède.
        var gras = Fragments(new F(0, 5, Bold: true));
        Assert.True(MiseEnForme.Poser(texte, gras, Bloc, new Plage(5, 2), new StyleDeLaPlage { Bold = true }));
        Assert.Equal([new F(0, 7, Bold: true)], Valeurs(gras));

        // Au-delà du texte, rien : une plage qui en sort y est ramenée, vide.
        Assert.False(MiseEnForme.Poser(texte, gras, Bloc, new Plage(7, 5), new StyleDeLaPlage { Italic = true }));
        Assert.False(MiseEnForme.Poser(texte, gras, Bloc, new Plage(40, 3), new StyleDeLaPlage { Italic = true }));
        Assert.False(MiseEnForme.Poser(texte, gras, Bloc, new Plage(2, -3), new StyleDeLaPlage { Italic = true }));
        // Et une plage qui en sort ne met en forme que ce qu'elle a du texte.
        Assert.True(MiseEnForme.Poser(texte, gras, Bloc, new Plage(5, int.MaxValue), new StyleDeLaPlage { Italic = true }));
        Assert.Equal([new F(0, 5, Bold: true), new F(5, 2, Bold: true, Italic: true)], Valeurs(gras));
    }

    [Fact]
    public void Une_plage_qui_couvre_tout_le_texte_tranche_les_recouvrements_partout()
    {
        const string texte = "ab cd ef gh ij";
        var fragments = Fragments(
            new F(0, 8, "Calibri", 10, Bold: true, Italic: false, Foreground: "#FF000000"),
            new F(3, 8, "Calibri", 12, Bold: false, Italic: true, Foreground: "#FFCC0000"));
        var avant = Styles(texte, fragments, Bloc);

        Assert.True(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(0, texte.Length), new StyleDeLaPlage { FontSize = 9 }));

        // Plus aucun recouvrement, le corps partout, et ce que les fragments répétaient du
        // bloc — le maigre, le droit — n'y est plus écrit.
        Assert.Equal(
            [
                new F(0, 3, "Calibri", 9, Bold: true, Foreground: "#FF000000"),
                new F(3, 8, "Calibri", 9, Italic: true, Foreground: "#FFCC0000"),
                new F(11, 3, FontSize: 9),
            ],
            Valeurs(fragments));
        Assert.Equal(avant.Select(s => s with { FontSize = 9 }), Styles(texte, fragments, Bloc));
    }

    [Fact]
    public void Un_fragment_qui_chevauche_une_borne_y_est_coupe_et_ceux_d_a_cote_ne_bougent_pas()
    {
        // a0 b1 c2 ␣3 d4 e5 f6 ␣7 g8 h9 i10 : « c d » en italique, « ghi » en gras.
        const string texte = "abc def ghi";
        var fragments = Fragments(new F(2, 4, Italic: true), new F(8, 3, Bold: true));
        var italique = fragments[0];
        var gras = fragments[1];

        Assert.True(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(4, 3), new StyleDeLaPlage { Bold = true }));

        // L'italique est coupé à la borne : « c » et l'espace le gardent seul, « de » est gras
        // et italique, « f » gras. « ghi », que la plage ne touche pas, reste le même objet.
        Assert.Equal(
            [new F(2, 2, Italic: true), new F(4, 2, Bold: true, Italic: true), new F(6, 1, Bold: true), new F(8, 3, Bold: true)],
            Valeurs(fragments));
        Assert.Same(gras, fragments[3]);
        Assert.DoesNotContain(italique, fragments);
    }

    [Fact]
    public void Des_fragments_qui_se_recouvrent_hors_de_la_plage_y_gardent_leur_ordre()
    {
        // Le second l'emporte sur [3, 6), que la plage ne touche pas : ses bouts du dehors
        // restent après ceux du premier, et il l'emporte encore.
        const string texte = "ab cd ef gh ij";
        var fragments = Fragments(
            new F(0, 8, "Calibri", 10, Bold: true, Italic: false, Foreground: "#FF000000"),
            new F(3, 8, "Calibri", 12, Bold: false, Italic: true, Foreground: "#FFCC0000"));

        Assert.True(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(6, 3), new StyleDeLaPlage { Underline = Souligne.Simple }));

        Assert.Equal(
            [
                new F(0, 6, "Calibri", 10, Bold: true, Italic: false, Foreground: "#FF000000"),
                new F(6, 3, "Calibri", 12, Italic: true, Underline: Souligne.Simple, Foreground: "#FFCC0000"),
                new F(3, 3, "Calibri", 12, Bold: false, Italic: true, Foreground: "#FFCC0000"),
                new F(9, 2, "Calibri", 12, Bold: false, Italic: true, Foreground: "#FFCC0000"),
            ],
            Valeurs(fragments));
    }

    [Fact]
    public void Une_valeur_egale_a_celle_du_bloc_devient_nulle_et_un_fragment_vide_disparait()
    {
        const string texte = "abc def";
        var fragments = Fragments(new F(4, 3, FontSize: 14, Bold: true));

        Assert.True(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(4, 3), new StyleDeLaPlage { FontSize = 11 }));
        Assert.Equal([new F(4, 3, Bold: true)], Valeurs(fragments));

        Assert.True(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(4, 3), new StyleDeLaPlage { Bold = false }));
        Assert.Empty(fragments);

        // Ce qu'un import répète du bloc disparaît aussi de la plage touchée, et de là
        // seulement : le dehors garde ce que le fichier disait.
        var importe = Fragments(new F(0, 7, "DejaVu Sans", 11, Bold: true, Italic: false, Foreground: "#FF1A1A1A"));
        Assert.True(MiseEnForme.Poser(texte, importe, Bloc, new Plage(4, 3), new StyleDeLaPlage { Italic = true }));
        Assert.Equal(
            [new F(0, 4, "DejaVu Sans", 11, Bold: true, Italic: false, Foreground: "#FF1A1A1A"), new F(4, 3, Bold: true, Italic: true)],
            Valeurs(importe));
    }

    [Fact]
    public void Une_couleur_du_jeu_se_compare_brute_pour_ecrire()
    {
        const string texte = "abc def";
        var fragments = Fragments();

        // Sur un bloc qui renvoie au jeu, la couleur que l'emplacement désigne aujourd'hui
        // n'est pas la sienne : elle reste écrite, et ne suivra pas un changement de jeu.
        var duJeu = Bloc with { Foreground = "scheme:main" };
        Assert.True(MiseEnForme.Poser(texte, fragments, duJeu, new Plage(4, 3), new StyleDeLaPlage { Foreground = "#FF1A1A1A" }));
        Assert.Equal([new F(4, 3, Foreground: "#FF1A1A1A")], Valeurs(fragments));

        // La référence du bloc, elle, est la valeur du bloc.
        Assert.True(MiseEnForme.Poser(texte, fragments, duJeu, new Plage(4, 3), new StyleDeLaPlage { Foreground = "scheme:main" }));
        Assert.Empty(fragments);

        // Sur un bloc à la couleur libre que l'accent 1 désigne aujourd'hui, la référence s'écrit.
        var libre = Bloc with { Foreground = "#FF2E5C8A" };
        Assert.True(MiseEnForme.Poser(texte, fragments, libre, new Plage(4, 3), new StyleDeLaPlage { Foreground = "scheme:accent1" }));
        Assert.Equal([new F(4, 3, Foreground: "scheme:accent1")], Valeurs(fragments));
        Assert.False(MiseEnForme.Poser(texte, fragments, libre, new Plage(4, 3), new StyleDeLaPlage { Foreground = "scheme:accent1" }));
    }

    [Fact]
    public void Une_couleur_et_une_police_du_jeu_se_comparent_resolues_pour_lire()
    {
        const string texte = "abc def";
        var ocean = new Charte(JeuxPredefinis.CouleursNommees("Océan")!, JeuxPredefinis.PolicesParDefaut);
        var archives = new Charte(JeuxPredefinis.CouleursParDefaut, JeuxPredefinis.PolicesNommees("Archives")!);

        // « def » renvoie à la couleur principale du jeu, qu'Ardoise fait #FF1A1A1A — celle
        // du bloc : rien n'est mixte, et la couleur rendue est la résolue. Sous Océan, si.
        var couleurs = Fragments(new F(4, 3, Foreground: "scheme:main"));
        Assert.Equal("#FF1A1A1A", MiseEnForme.Lire(texte, couleurs, Bloc, new Plage(0, 7), Charte.Defaut).Foreground);
        Assert.Null(MiseEnForme.Lire(texte, couleurs, Bloc, new Plage(0, 7), ocean).Foreground);
        // La référence seule reste une référence.
        Assert.Equal("scheme:main", MiseEnForme.Lire(texte, couleurs, Bloc, new Plage(4, 3), ocean).Foreground);

        // De même la police du titre du jeu, DejaVu Sans sous Sobre, DejaVu Serif sous Archives.
        var polices = Fragments(new F(0, 3, FontFamily: "scheme:heading"));
        Assert.Equal("DejaVu Sans", MiseEnForme.Lire(texte, polices, Bloc, new Plage(0, 7), Charte.Defaut).FontFamily);
        Assert.Null(MiseEnForme.Lire(texte, polices, Bloc, new Plage(0, 7), archives).FontFamily);
        Assert.Equal("scheme:heading", MiseEnForme.Lire(texte, polices, Bloc, new Plage(0, 3), archives).FontFamily);
    }

    [Fact]
    public void Le_decalage_de_ligne_de_base_vaut_zero_comme_le_bloc()
    {
        // x0 2₁ ␣2 e3 t4 ␣5 H6 2₇ O8
        const string texte = "x2 et H2O";
        var fragments = Fragments();

        Assert.True(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(1, 1), new StyleDeLaPlage { BaselineShift = 0.33 }));
        Assert.Equal([new F(1, 1, BaselineShift: 0.33)], Valeurs(fragments));
        Assert.Equal(0.33, MiseEnForme.Lire(texte, fragments, Bloc, new Plage(1, 1), Charte.Defaut).BaselineShift);
        Assert.Null(MiseEnForme.Lire(texte, fragments, Bloc, new Plage(0, 2), Charte.Defaut).BaselineShift);

        // Zéro, c'est « comme le bloc » : il ne reste rien.
        Assert.True(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(1, 1), new StyleDeLaPlage { BaselineShift = 0 }));
        Assert.Empty(fragments);

        // Un indice gras : le décalage ôté laisse le gras, Ctrl+Espace ôte tout.
        var indice = Fragments(new F(7, 1, Bold: true, BaselineShift: -0.2));
        Assert.True(MiseEnForme.Poser(texte, indice, Bloc, new Plage(7, 1), new StyleDeLaPlage { BaselineShift = 0 }));
        Assert.Equal([new F(7, 1, Bold: true)], Valeurs(indice));

        indice = Fragments(new F(7, 1, Bold: true, BaselineShift: -0.2));
        Assert.True(MiseEnForme.Retirer(texte, indice, Bloc, new Plage(0, texte.Length)));
        Assert.Empty(indice);

        // Un fragment qui recouvre un exposant sans dire de décalage ne l'annule pas : c'est
        // la règle de « Decouper », et la plage mise en forme la garde.
        var recouverts = Fragments(new F(0, 5, BaselineShift: 0.33), new F(2, 3, Bold: true));
        Assert.True(MiseEnForme.Poser(texte, recouverts, Bloc, new Plage(2, 3), new StyleDeLaPlage { Italic = true }));
        Assert.Equal([new F(0, 2, BaselineShift: 0.33), new F(2, 3, Bold: true, Italic: true, BaselineShift: 0.33)], Valeurs(recouverts));
    }

    [Fact]
    public void Le_souligne_se_pose_dans_sa_sorte_et_se_bascule_quelle_qu_elle_soit()
    {
        const string texte = "abc def ghi";
        var pointille = new Souligne(TraitDeSouligne.Dotted);
        var fragments = Fragments();

        Assert.True(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(4, 3), new StyleDeLaPlage { Underline = pointille }));
        Assert.Equal([new F(4, 3, Underline: pointille)], Valeurs(fragments));

        // « ␣ghi » au trait simple : « def ghi » est souligné partout, de deux sortes. La
        // lecture la dit mixte, la bascule allumée — et elle éteint tout, comme Ctrl+U.
        Assert.True(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(7, 4), new StyleDeLaPlage { Underline = Souligne.Simple }));
        Assert.Null(MiseEnForme.Lire(texte, fragments, Bloc, new Plage(4, 7), Charte.Defaut).Underline);
        Assert.True(MiseEnForme.Allume(texte, fragments, Bloc, new Plage(4, 7), Bascule.Souligne));
        Assert.True(MiseEnForme.Basculer(texte, fragments, Bloc, new Plage(4, 7), Bascule.Souligne));
        Assert.Empty(fragments);

        // Rebasculée, la plage prend le trait simple, le seul que Publisher pose au clavier.
        Assert.True(MiseEnForme.Basculer(texte, fragments, Bloc, new Plage(4, 7), Bascule.Souligne));
        Assert.Equal([new F(4, 7, Underline: Souligne.Simple)], Valeurs(fragments));

        // Une plage qui n'est soulignée qu'en partie est mixte pour les deux.
        Assert.Null(MiseEnForme.Allume(texte, fragments, Bloc, new Plage(0, 7), Bascule.Souligne));
    }

    [Fact]
    public void Sous_un_bloc_souligne_le_souligne_retire_s_ecrit_aucun()
    {
        // « Aucun » sur le fragment, et non nul : un mot qu'on retire du souligné de son
        // bloc (I7). Nul lui rendrait la vague du bloc.
        var souligne = Bloc with { Underline = new Souligne(TraitDeSouligne.Wave) };
        var fragments = Fragments();

        Assert.True(MiseEnForme.Basculer("abc def", fragments, souligne, new Plage(4, 3), Bascule.Souligne));
        Assert.Equal([new F(4, 3, Underline: Souligne.Aucun)], Valeurs(fragments));
    }

    // ------------------------------------------------------------- les paragraphes

    [Fact]
    public void Un_paragraphe_vide_se_regle_comme_un_autre()
    {
        // U0 n1 ⏎2 ⏎3 T4 r5 o6 i7 s8 ⏎9 : « Un », un vide, « Trois », un vide au bout.
        const string texte = "Un\n\nTrois\n";
        Assert.Equal(4, Recalage.NombreDeParagraphes(texte));
        var ecarts = Ecarts();
        var retrait = new ReglagesDeLaPlage { IndentLeft = 18 };

        Assert.True(MiseEnForme.Poser(texte, ecarts, Paragraphe, new Plage(1, 1), retrait));
        Assert.True(MiseEnForme.Poser(texte, ecarts, Paragraphe, new Plage(3, 1), retrait));
        Assert.Equal([new E(1, 1, IndentLeft: 18), new E(3, 1, IndentLeft: 18)], Valeurs(ecarts));

        // Le curseur dans le paragraphe vide du milieu le désigne, et celui du bout le dernier.
        Assert.Equal(new Plage(1, 1), MiseEnForme.Paragraphes(texte, new Plage(3, 0)));
        Assert.Equal(new Plage(3, 1), MiseEnForme.Paragraphes(texte, new Plage(texte.Length, 0)));

        Assert.Equal(18, MiseEnForme.Lire(texte, ecarts, Paragraphe, new Plage(1, 1)).IndentLeft);
        Assert.Equal(18, MiseEnForme.Lire(texte, ecarts, Paragraphe, new Plage(3, 0)).IndentLeft);
        Assert.Null(MiseEnForme.Lire(texte, ecarts, Paragraphe, new Plage(0, 2)).IndentLeft);

        // Remplir le trou entre les deux les fond en un seul écart.
        Assert.True(MiseEnForme.Poser(texte, ecarts, Paragraphe, new Plage(2, 1), retrait));
        Assert.Equal([new E(1, 3, IndentLeft: 18)], Valeurs(ecarts));

        Assert.True(MiseEnForme.Retirer(texte, ecarts, Paragraphe, new Plage(0, 4)));
        Assert.Empty(ecarts);
    }

    [Fact]
    public void Les_reglages_se_lisent_communs_ou_mixtes_et_l_alignement_se_bascule()
    {
        const string texte = "Un\nDeux\nTrois";
        var justifie = Paragraphe with { Align = TextAlign.Justify };
        var ecarts = Ecarts(new E(0, 2, SpaceAfter: 6, Align: TextAlign.Center), new E(1, 1, IndentFirstLine: -12));

        Assert.Equal(
            new ReglagesDeLaPlage
            {
                IndentLeft = 0, IndentRight = 0, IndentFirstLine = null, SpaceBefore = 0, SpaceAfter = 6,
                Align = TextAlign.Center,
            },
            MiseEnForme.Lire(texte, ecarts, justifie, new Plage(0, 2)));
        Assert.Null(MiseEnForme.Lire(texte, ecarts, justifie, new Plage(0, 3)).Align);

        // Les deux premiers sont centrés : la bascule du centrage les rend au fer à gauche,
        // qui n'est pas celui du bloc, et s'écrit donc.
        Assert.True(MiseEnForme.Basculer(texte, ecarts, justifie, new Plage(0, 2), TextAlign.Center));
        Assert.Equal(TextAlign.Left, MiseEnForme.Lire(texte, ecarts, justifie, new Plage(0, 2)).Align);
        Assert.Equal(
            [new E(0, 1, SpaceAfter: 6, Align: TextAlign.Left), new E(1, 1, IndentFirstLine: -12, SpaceAfter: 6, Align: TextAlign.Left)],
            Valeurs(ecarts));

        // Sur une plage mixte, la bascule pose le centrage partout.
        Assert.True(MiseEnForme.Basculer(texte, ecarts, justifie, new Plage(0, 3), TextAlign.Center));
        Assert.Equal(TextAlign.Center, MiseEnForme.Lire(texte, ecarts, justifie, new Plage(0, 3)).Align);
    }

    [Fact]
    public void ParagrapheDe_compte_les_sauts_comme_le_recalage()
    {
        // U0 n1 ␍2 ⏎3 D4 e5 u6 x7 ␍8 T9 r10 o11 i12 s13 ⏎14 Q15 …
        const string texte = "Un\r\nDeux\rTrois\nQuatre";

        Assert.Equal(0, Recalage.ParagrapheDe(texte, 0));
        Assert.Equal(0, Recalage.ParagrapheDe(texte, 2));   // le « \r » d'un « \r\n » ne clôt rien
        Assert.Equal(0, Recalage.ParagrapheDe(texte, 3));   // son « \n », qui clôt, est au paragraphe clos
        Assert.Equal(1, Recalage.ParagrapheDe(texte, 4));
        Assert.Equal(1, Recalage.ParagrapheDe(texte, 8));   // un « \r » seul clôt
        Assert.Equal(2, Recalage.ParagrapheDe(texte, 9));
        Assert.Equal(3, Recalage.ParagrapheDe(texte, 15));
        Assert.Equal(3, Recalage.ParagrapheDe(texte, 99));
        Assert.Equal(0, Recalage.ParagrapheDe(texte, -5));
        Assert.Equal(4, Recalage.NombreDeParagraphes(texte));
        Assert.Equal(1, Recalage.NombreDeParagraphes(""));

        // Et l'écart que le recalage décale est celui que ce compte désigne.
        var (_, ecarts) = Recalage.Recaler(texte, "\n" + texte, [], [new Plage(Recalage.ParagrapheDe(texte, 9), 1)]);
        Assert.Equal(new Plage(Recalage.ParagrapheDe("\n" + texte, 10), 1), ecarts.Single());
    }

    [Fact]
    public void Les_paragraphes_d_une_selection_vont_de_son_premier_caractere_a_son_dernier()
    {
        // U0 n1 ⏎2 D3 e4 u5 x6 ⏎7 T8 r9 o10 i11 s12
        const string texte = "Un\nDeux\nTrois";

        Assert.Equal(new Plage(1, 1), MiseEnForme.Paragraphes(texte, new Plage(4, 0)));   // le curseur
        Assert.Equal(new Plage(1, 1), MiseEnForme.Paragraphes(texte, new Plage(3, 5)));   // « Deux⏎ », saut compris
        Assert.Equal(new Plage(1, 2), MiseEnForme.Paragraphes(texte, new Plage(3, 6)));   // et le « T » de « Trois »
        Assert.Equal(new Plage(0, 2), MiseEnForme.Paragraphes(texte, new Plage(2, 2)));   // du saut de « Un » à « D »
        Assert.Equal(new Plage(0, 3), MiseEnForme.Paragraphes(texte, new Plage(0, texte.Length)));
        Assert.Equal(new Plage(2, 1), MiseEnForme.Paragraphes(texte, new Plage(40, 3)));
        Assert.Equal(new Plage(0, 1), MiseEnForme.Paragraphes("", new Plage(0, 0)));
    }

    // ------------------------------------------------------------- le modèle

    [Fact]
    public void Une_case_de_tableau_se_met_en_forme_au_style_du_tableau()
    {
        var tableau = new TableFrame { Bold = true, FontSize = 9, Foreground = "scheme:accent2" };
        tableau.RowCount = 1;
        tableau.ColumnCount = 1;
        var cellule = tableau.Cells[0];
        cellule.Text = "abc def";
        var commun = MiseEnForme.Commun(tableau);

        // Le style des cases est celui du bloc que le tableau construit pour chacune.
        var dessinee = (TextFrame)tableau.CellFrames[0];
        Assert.Equal(commun, MiseEnForme.Commun(dessinee));

        // Le gras du tableau se retire d'un mot ; la case prête ses fragments au bloc qui la dessine.
        Assert.True(MiseEnForme.Basculer(cellule.Text, cellule.Spans, commun, new Plage(4, 3), Bascule.Gras));
        Assert.Equal([new F(4, 3, Bold: false)], Valeurs(cellule.Spans));
        Assert.Same(cellule.Spans, ((TextFrame)tableau.CellFrames[0]).Spans);
    }

    [Fact]
    public void Le_style_du_bloc_est_celui_que_la_charte_resout()
    {
        // Deux écritures du même style commun, l'une brute ici, l'autre résolue dans la charte :
        // elles doivent dire la même chose, souligné compris.
        var bloc = new TextFrame
        {
            FontFamily = "scheme:heading", FontSize = 17, Bold = true, Italic = true,
            Underline = new Souligne(TraitDeSouligne.DotDash, thick: true), Foreground = "scheme:accent3",
        };

        Assert.Equal(Charte.Defaut.StyleCommun(bloc), Charte.Defaut.Resoudre(MiseEnForme.Commun(bloc)));
    }

    /// <summary>Le nombre de changements qu'ont annoncés les fragments eux-mêmes, après coup.</summary>
    private static Func<int> Ecouter(IEnumerable<StyleSpan> fragments)
    {
        var modifies = 0;
        foreach (var fragment in fragments) fragment.PropertyChanged += (_, _) => modifies++;
        return () => modifies;
    }

    [Fact]
    public void L_ecriture_passe_par_la_collection_et_ne_modifie_aucun_fragment_en_place()
    {
        const string texte = "abc def ghi";
        var fragments = Fragments(new F(0, 3, Italic: true), new F(4, 7, Bold: true));
        var italique = fragments[0];
        var collection = 0;
        fragments.CollectionChanged += (_, _) => collection++;

        // Le gras retiré au milieu : le fragment gras est remplacé par ses deux bouts.
        var modifies = Ecouter(fragments);
        Assert.True(MiseEnForme.Basculer(texte, fragments, Bloc, new Plage(5, 2), Bascule.Gras));
        Assert.Equal([new F(0, 3, Italic: true), new F(4, 1, Bold: true), new F(7, 4, Bold: true)], Valeurs(fragments));
        Assert.Equal(0, modifies());
        Assert.Same(italique, fragments[0]);
        Assert.Equal(2, collection);

        // Rebasculé : le fragment d'avant revient, en un seul.
        modifies = Ecouter(fragments);
        Assert.True(MiseEnForme.Basculer(texte, fragments, Bloc, new Plage(5, 2), Bascule.Gras));
        Assert.Equal([new F(0, 3, Italic: true), new F(4, 7, Bold: true)], Valeurs(fragments));
        Assert.Equal(0, modifies());
        Assert.Same(italique, fragments[0]);

        // Sans changement réel, rien n'est annoncé.
        collection = 0;
        Assert.False(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(4, 7), new StyleDeLaPlage { Bold = true }));
        Assert.Equal(0, collection);
    }

    [Fact]
    public void Sans_changement_reel_rien_ne_bouge_meme_une_liste_desordonnee()
    {
        // Deux fragments qui se recouvrent, l'un répétant le corps du bloc : un désordre
        // qu'une pose réelle trancherait, et qu'une pose sans effet laisse.
        const string texte = "abc def";
        var fragments = Fragments(new F(2, 5, FontSize: 11, Bold: true), new F(0, 4, Bold: true));
        var objets = fragments.ToList();
        var notifications = 0;
        fragments.CollectionChanged += (_, _) => notifications++;

        Assert.False(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(0, 7), new StyleDeLaPlage { Bold = true }));
        Assert.False(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(1, 3), new StyleDeLaPlage { FontSize = 11 }));
        Assert.False(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(0, 7), new StyleDeLaPlage { Italic = false }));
        Assert.False(MiseEnForme.Poser(texte, fragments, Bloc, new Plage(0, 7), new StyleDeLaPlage()));
        Assert.Equal(0, notifications);
        Assert.Equal(objets, fragments);

        // Un fragment qui ne fait que répéter le bloc n'a rien à rendre à Ctrl+Espace.
        var repete = Fragments(new F(0, 7, "DejaVu Sans", 11, Bold: false));
        Assert.False(MiseEnForme.Retirer(texte, repete, Bloc, new Plage(0, 7)));
        Assert.Single(repete);

        // De même pour les écarts : centrer ce qui l'est déjà ne fait rien.
        var ecarts = Ecarts(new E(0, 3, Align: TextAlign.Center), new E(1, 1, Align: TextAlign.Center));
        Assert.False(MiseEnForme.Poser("Un\nDeux\nTrois", ecarts, Paragraphe, new Plage(1, 2),
            new ReglagesDeLaPlage { Align = TextAlign.Center }));
        Assert.Equal(2, ecarts.Count);
    }

    // ------------------------------------------------------------- l'exhaustivité

    /// <summary>Les propriétés de style d'un élément : toutes, sauf sa place.</summary>
    private static string[] ProprietesDeStyle(Type type) =>
        type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                           System.Reflection.BindingFlags.DeclaredOnly)
            .Select(p => p.Name).Where(nom => nom is not ("Start" or "Length")).Order().ToArray();

    private static string[] Proprietes(Type type) =>
        type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name).Where(nom => nom != "EqualityContract").Order().ToArray();

    [Fact]
    public void Toutes_les_proprietes_du_fragment_et_de_l_ecart_sont_mises_en_forme()
    {
        // Une propriété ajoutée au fragment (la langue de N9, les capitales de N24) et
        // oubliée ici serait perdue dans toute plage qu'on met en forme.
        var surcharges = typeof(MiseEnForme).GetNestedType("Surcharges", System.Reflection.BindingFlags.NonPublic)!;
        var deParagraphe = typeof(MiseEnForme).GetNestedType("SurchargesDeParagraphe", System.Reflection.BindingFlags.NonPublic)!;

        Assert.Equal(ProprietesDeStyle(typeof(StyleSpan)), Proprietes(surcharges));
        Assert.Equal(ProprietesDeStyle(typeof(StyleSpan)), Proprietes(typeof(StyleDeLaPlage)));
        Assert.Equal(ProprietesDeStyle(typeof(StyleSpan)), Proprietes(typeof(StyleEffectif)));
        Assert.Equal(ProprietesDeStyle(typeof(ParagraphSpan)), Proprietes(deParagraphe));
        Assert.Equal(ProprietesDeStyle(typeof(ParagraphSpan)), Proprietes(typeof(ReglagesDeLaPlage)));
        Assert.Equal(ProprietesDeStyle(typeof(ParagraphSpan)), Proprietes(typeof(ParagrapheEffectif)));
    }

    // ------------------------------------------------------------- les balayages

    private static readonly string?[] Polices = [null, "DejaVu Sans", "Calibri", "scheme:heading"];
    private static readonly double?[] Corps = [null, 11, 9, 14];
    private static readonly bool?[] Drapeaux = [null, false, true];
    private static readonly Souligne?[] Soulignes = [null, Souligne.Aucun, Souligne.Simple, new Souligne(TraitDeSouligne.Wave)];
    private static readonly string?[] Couleurs = [null, "#FF1A1A1A", "#FFCC0000", "scheme:accent1"];
    private static readonly double[] Decalages = [0, 0, 0.33, -0.2];

    private static T Un<T>(Random hasard, T[] valeurs) => valeurs[hasard.Next(valeurs.Length)];

    private static StyleSpan FragmentAuHasard(Random hasard, int longueur) => new()
    {
        Start = hasard.Next(-2, longueur + 2), Length = hasard.Next(-1, longueur + 3),
        FontFamily = Un(hasard, Polices), FontSize = Un(hasard, Corps), Bold = Un(hasard, Drapeaux),
        Italic = Un(hasard, Drapeaux), Underline = Un(hasard, Soulignes), Foreground = Un(hasard, Couleurs),
        BaselineShift = Un(hasard, Decalages),
    };

    [Fact]
    public void Balayage_des_fragments_hors_de_la_plage_rien_ne_change_a_l_oeil()
    {
        // Des listes d'import au hasard — recouvrements, débords, longueurs négatives —, une
        // plage et une pose au hasard : chaque caractère dessine ce qu'il dessinait, la
        // plage y ajoutant la valeur posée ; et la même pose, rejouée, ne fait plus rien.
        var hasard = new Random(2026_10_26);
        const string texte = "Le gras\nsur l'aile droite";

        for (var essai = 0; essai < 2000; essai++)
        {
            var commun = new StyleEffectif(
                "DejaVu Sans", 11, Bold: hasard.Next(2) == 0, Italic: false,
                hasard.Next(3) == 0 ? Souligne.Simple : Souligne.Aucun, "#FF1A1A1A", 0);
            var fragments = new ObservableCollection<StyleSpan>(
                Enumerable.Range(0, hasard.Next(5)).Select(_ => FragmentAuHasard(hasard, texte.Length)));
            var avant = Styles(texte, fragments, commun);
            var plage = new Plage(hasard.Next(-1, texte.Length + 1), hasard.Next(-1, texte.Length + 1));
            var retirer = hasard.Next(6) == 0;
            var pose = new StyleDeLaPlage
            {
                FontFamily = hasard.Next(3) == 0 ? Un(hasard, Polices) : null,
                FontSize = hasard.Next(3) == 0 ? Un(hasard, Corps) : null,
                Bold = hasard.Next(3) == 0 ? Un(hasard, Drapeaux) : null,
                Italic = hasard.Next(3) == 0 ? Un(hasard, Drapeaux) : null,
                Underline = hasard.Next(3) == 0 ? Un(hasard, Soulignes) : null,
                Foreground = hasard.Next(3) == 0 ? Un(hasard, Couleurs) : null,
                BaselineShift = hasard.Next(3) == 0 ? Un(hasard, Decalages) : null,
            };

            var change = retirer
                ? MiseEnForme.Retirer(texte, fragments, commun, plage)
                : MiseEnForme.Poser(texte, fragments, commun, plage, pose);

            var apres = Styles(texte, fragments, commun);
            var debut = Math.Clamp(plage.Start, 0, texte.Length);
            var fin = Math.Clamp(plage.Start + Math.Max(0, plage.Length), debut, texte.Length);
            var visible = false;
            for (var i = 0; i < texte.Length; i++)
            {
                var attendu = i < debut || i >= fin ? avant[i]
                    : retirer ? commun
                    : avant[i] with
                    {
                        FontFamily = pose.FontFamily ?? avant[i].FontFamily, FontSize = pose.FontSize ?? avant[i].FontSize,
                        Bold = pose.Bold ?? avant[i].Bold, Italic = pose.Italic ?? avant[i].Italic,
                        Underline = pose.Underline ?? avant[i].Underline,
                        Foreground = pose.Foreground ?? avant[i].Foreground,
                        BaselineShift = pose.BaselineShift ?? avant[i].BaselineShift,
                    };
                visible |= attendu != avant[i];
                Assert.True(attendu == apres[i], $"essai {essai}, caractère {i} : {apres[i]} au lieu de {attendu}");
            }

            // Un changement à l'œil écrit toujours ; une liste qui change sans rien montrer ne
            // peut venir que d'un fragment qui répétait le bloc dans la plage.
            if (visible) Assert.True(change, $"essai {essai} : la pose n'a rien écrit");

            // Rejouée, la même pose ne trouve plus rien à faire.
            var rejouee = retirer
                ? MiseEnForme.Retirer(texte, fragments, commun, plage)
                : MiseEnForme.Poser(texte, fragments, commun, plage, pose);
            Assert.False(rejouee, $"essai {essai} : la pose rejouée a encore écrit");
        }
    }

    [Fact]
    public void Balayage_des_ecarts_hors_de_la_plage_rien_ne_change()
    {
        var hasard = new Random(2026_10_25);
        const string texte = "Un\n\nTrois\nQuatre\r\nCinq\n";
        var nombre = Recalage.NombreDeParagraphes(texte);
        double?[] mesures = [null, 0, 6, 18];
        TextAlign?[] fers = [null, TextAlign.Left, TextAlign.Center, TextAlign.Justify];

        for (var essai = 0; essai < 2000; essai++)
        {
            var commun = Paragraphe with { Align = hasard.Next(2) == 0 ? TextAlign.Left : TextAlign.Justify };
            var ecarts = new ObservableCollection<ParagraphSpan>(Enumerable.Range(0, hasard.Next(5)).Select(_ =>
                new ParagraphSpan
                {
                    Start = hasard.Next(-1, nombre + 1), Length = hasard.Next(-1, nombre + 2),
                    IndentLeft = Un(hasard, mesures), IndentRight = Un(hasard, mesures),
                    IndentFirstLine = Un(hasard, mesures), SpaceBefore = Un(hasard, mesures),
                    SpaceAfter = Un(hasard, mesures), Align = Un(hasard, fers),
                }));
            var avant = ParagrapheEffectif.Resoudre(nombre, ecarts, commun);
            var plage = new Plage(hasard.Next(-1, nombre + 1), hasard.Next(-1, nombre + 1));
            var retirer = hasard.Next(6) == 0;
            var pose = new ReglagesDeLaPlage
            {
                IndentLeft = hasard.Next(2) == 0 ? Un(hasard, mesures) : null,
                SpaceAfter = hasard.Next(2) == 0 ? Un(hasard, mesures) : null,
                Align = hasard.Next(2) == 0 ? Un(hasard, fers) : null,
            };

            _ = retirer
                ? MiseEnForme.Retirer(texte, ecarts, commun, plage)
                : MiseEnForme.Poser(texte, ecarts, commun, plage, pose);

            var apres = ParagrapheEffectif.Resoudre(nombre, ecarts, commun);
            var debut = Math.Clamp(plage.Start, 0, nombre);
            var fin = Math.Clamp(plage.Start + Math.Max(0, plage.Length), debut, nombre);
            for (var i = 0; i < nombre; i++)
            {
                var attendu = i < debut || i >= fin ? avant[i]
                    : retirer ? commun
                    : avant[i] with
                    {
                        IndentLeft = pose.IndentLeft ?? avant[i].IndentLeft,
                        SpaceAfter = pose.SpaceAfter ?? avant[i].SpaceAfter, Align = pose.Align ?? avant[i].Align,
                    };
                Assert.True(attendu == apres[i], $"essai {essai}, paragraphe {i} : {apres[i]} au lieu de {attendu}");
            }

            var rejouee = retirer
                ? MiseEnForme.Retirer(texte, ecarts, commun, plage)
                : MiseEnForme.Poser(texte, ecarts, commun, plage, pose);
            Assert.False(rejouee, $"essai {essai} : la pose rejouée a encore écrit");
        }
    }
}
