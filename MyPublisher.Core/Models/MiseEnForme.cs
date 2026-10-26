using System;
using System.Collections.Generic;
using System.Linq;

namespace MyPublisher.Models;

/// <summary>
/// Le style d'une plage de caractères, propriété par propriété : ce que
/// <see cref="MiseEnForme"/> lit sur une plage, et ce qu'il y pose.
///
/// Une propriété nulle n'a rien à dire. À la lecture, la plage n'en a pas qu'une : c'est
/// l'état mixte, un champ vide dans la barre. À la pose, on n'y touche pas. Le même type
/// sert dans les deux sens, et ce n'est pas une économie : la mise en forme lue sur une
/// plage se pose telle quelle sur une autre, sans ce qu'elle a de mixte — ce que feront
/// <c>Ctrl+Maj+C</c> et <c>Ctrl+Maj+V</c> (R7).
///
/// Les noms sont ceux de <see cref="StyleSpan"/>. Le décalage de ligne de base, qui n'est
/// pas nullable sur le fragment — 0 y veut dire « comme le bloc » —, l'est ici : nul, il est
/// mixte ou laissé tel quel ; 0, la plage n'a pas de décalage.
/// </summary>
public readonly record struct StyleDeLaPlage
{
    public string? FontFamily { get; init; }
    public double? FontSize { get; init; }
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
    public Souligne? Underline { get; init; }
    public string? Foreground { get; init; }
    public double? BaselineShift { get; init; }
}

/// <summary>
/// Les réglages d'une suite de paragraphes, propriété par propriété : le pendant de
/// <see cref="StyleDeLaPlage"/> à l'échelle du paragraphe, avec les noms de
/// <see cref="ParagraphSpan"/>. Nul veut dire mixte à la lecture, intouché à la pose.
/// </summary>
public readonly record struct ReglagesDeLaPlage
{
    public double? IndentLeft { get; init; }
    public double? IndentRight { get; init; }
    public double? IndentFirstLine { get; init; }
    public double? SpaceBefore { get; init; }
    public double? SpaceAfter { get; init; }
    public TextAlign? Align { get; init; }
}

/// <summary>
/// Ce qu'un bouton d'état de la barre allume et éteint, et ce que <c>Ctrl+G</c>,
/// <c>Ctrl+I</c> et <c>Ctrl+U</c> basculent : les trois propriétés qui sont mises ou ne le
/// sont pas.
/// </summary>
public enum Bascule { Gras, Italique, Souligne }

/// <summary>
/// Met en forme une plage du texte : lit ce qu'elle a en commun, y pose une valeur, y
/// bascule le gras, l'italique ou le souligné, lui rend le style du bloc. Sur les fragments
/// de style (<see cref="StyleSpan"/>), et de même sur les écarts de paragraphe
/// (<see cref="ParagraphSpan"/>), à l'échelle du paragraphe.
///
/// Jusqu'au poste N2, les fragments n'étaient écrits que par la copie, l'import et le
/// recalage de Q1 : rien n'appliquait un style à une plage. R6 et R7 s'en serviront ; rien
/// de l'application ne l'appelle encore. Tout est fonction du texte, d'une collection et du
/// style du bloc — sur le modèle de <see cref="Recalage"/> —, si bien que tout s'éprouve
/// sans bloc ni fenêtre, et qu'une case de tableau se met en forme comme un bloc : ses
/// fragments, au style du tableau (<see cref="Commun(TableFrame)"/>). Une case n'a pas
/// d'écarts de paragraphe.
///
/// **Ce que fait une pose**, en une phrase : le texte de la plage prend la valeur, et rien
/// d'autre ne change à l'œil. Pour cela :
/// <list type="bullet">
/// <item>un fragment à cheval sur une borne y est coupé ; ses bouts hors de la plage sont
/// des copies de lui (<see cref="StyleSpan.Clone"/>), à sa place dans la liste ;</item>
/// <item>dans la plage, et là seulement, la règle « le dernier l'emporte » de
/// <see cref="StyleEffectif.Decouper"/> est tranchée : ce que les fragments y disent,
/// superposés dans leur ordre, est récrit en fragments qui ne se recouvrent plus, la valeur
/// posée, les trous comblés ;</item>
/// <item>une valeur égale à celle du bloc y devient nulle, et un fragment qui ne dit plus
/// rien disparaît ;</item>
/// <item>un bout qui dit la même chose que son voisin, de part et d'autre d'une borne, s'y
/// recolle.</item>
/// </list>
/// Hors de la plage, la liste ne change pas : les recouvrements qu'un import y a laissés y
/// restent, dans le même ordre, et le dernier y l'emporte toujours.
///
/// **Ce qu'on compare.** Pour écrire, les valeurs brutes : <c>scheme:accent1</c> n'est pas
/// la couleur qu'il désigne aujourd'hui. Un fragment qui y renvoie la suit quand on change
/// de jeu ; le confondre avec le bloc parce que les deux se ressemblent ce jour-là lui ferait
/// perdre sa référence. Pour lire l'état mixte, les valeurs résolues par la charte : deux
/// mots qui se dessinent de la même couleur ne sont pas « mixtes » dans la barre.
///
/// **L'écriture passe par la collection** : jamais par son remplacement (Q17), jamais par un
/// fragment modifié sur place. <c>ControleDeBloc</c> n'écoute que la collection, et un
/// fragment changé sous lui ne serait pas redessiné ; un fragment qui change est donc
/// remplacé par un autre, et ceux qui ne changent pas restent les mêmes objets. **Sans
/// changement réel, rien ne se fait** : une pose qui ne change rien à l'œil — le gras posé
/// sur du gras — laisse la collection telle qu'elle était, même désordonnée, et l'historique
/// n'empile pas un pas que l'annulation rendrait sans rien montrer.
/// </summary>
public static class MiseEnForme
{
    private static readonly SorteDesFragments LesFragments = new();
    private static readonly SorteDesEcarts LesEcarts = new();

    // -------------------------------------------------------------- le style du bloc

    /// <summary>
    /// Le style d'un bloc de texte tel qu'il s'écrit, avant la charte : ce dont ses
    /// fragments s'écartent, et ce à quoi une valeur posée se compare.
    /// <see cref="Charte.StyleCommun"/> en est la résolution.
    /// </summary>
    public static StyleEffectif Commun(TextFrame bloc) => new(
        bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic,
        bloc.Underline, bloc.Foreground, BaselineShift: 0);

    /// <summary>
    /// Le style des cases d'un tableau : celui du tableau, sans souligné. Le tableau n'en
    /// porte pas, et le bloc que <see cref="TableFrame.CellFrames"/> construit pour une case
    /// n'en a donc pas : les fragments d'une case s'écartent de ce style-ci.
    /// </summary>
    public static StyleEffectif Commun(TableFrame tableau) => new(
        tableau.FontFamily, tableau.FontSize, tableau.Bold, tableau.Italic,
        Souligne.Aucun, tableau.Foreground, BaselineShift: 0);

    // -------------------------------------------------------------- les caractères

    /// <summary>
    /// Ce que la plage a en commun, propriété par propriété ; nulle, la propriété est
    /// mixte.
    ///
    /// La police et la couleur se comparent résolues par <paramref name="charte"/> : une
    /// plage dont une moitié dit <c>scheme:accent1</c> et l'autre la couleur que l'accent
    /// désigne n'est pas mixte. La valeur rendue est la brute quand toute la plage la porte
    /// — une référence reste une référence, et la barre peut la montrer comme telle —, la
    /// résolue sinon.
    ///
    /// Une plage vide se lit au style que prendrait un texte tapé là, à la règle de
    /// <see cref="Recalage.Ancre"/> : celui du caractère qui précède, celui qui suit en tête
    /// de paragraphe, celui du bloc dans un texte vide. C'est ce qu'un traitement de texte
    /// montre quand rien n'est sélectionné, et une plage vide n'est jamais mixte.
    /// </summary>
    public static StyleDeLaPlage Lire(
        string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun, Plage plage, Charte charte)
    {
        var styles = Styles(texte, fragments, commun, plage);

        return new StyleDeLaPlage
        {
            FontFamily = Commune(styles, s => s.FontFamily, charte.Police),
            FontSize = Commune(styles, s => s.FontSize),
            Bold = Commune(styles, s => s.Bold),
            Italic = Commune(styles, s => s.Italic),
            Underline = Commune(styles, s => s.Underline),
            Foreground = Commune(styles, s => s.Foreground, couleur => charte.Couleur(couleur) ?? couleur),
            BaselineShift = Commune(styles, s => s.BaselineShift),
        };
    }

    /// <summary>
    /// L'état d'une bascule sur la plage : allumée partout, éteinte partout, ou nulle quand
    /// la plage est mixte — le <c>IsChecked</c> d'un bouton d'état. C'est ce que
    /// <see cref="Basculer(string, IList{StyleSpan}, StyleEffectif, Plage, Bascule)"/> lit
    /// pour décider, si bien que le bouton et la touche ne peuvent pas se contredire.
    ///
    /// Le souligné est allumé quelle qu'en soit la sorte : c'est la règle de <c>Ctrl+U</c>
    /// dans Publisher, que la commande du bloc suit déjà (I7). Une plage mêlant un pointillé
    /// et un trait simple est donc allumée, alors que <see cref="Lire"/> la dit mixte.
    /// </summary>
    public static bool? Allume(
        string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun, Plage plage, Bascule bascule)
    {
        Func<StyleEffectif, bool> allume = bascule switch
        {
            Bascule.Gras => style => style.Bold,
            Bascule.Italique => style => style.Italic,
            Bascule.Souligne => style => style.Underline.Actif,
            _ => throw new ArgumentOutOfRangeException(nameof(bascule)),
        };

        return Commune(Styles(texte, fragments, commun, plage), allume);
    }

    /// <summary>
    /// Pose sur la plage chaque propriété que <paramref name="style"/> renseigne ; les autres
    /// ne bougent pas. Rend vrai si la collection a changé.
    ///
    /// Une plage vide ne change rien : c'est à l'appelant de dire sur quoi porte un geste
    /// fait sans sélection (le mot du curseur, pour R7).
    /// </summary>
    public static bool Poser(
        string texte, IList<StyleSpan> fragments, StyleEffectif commun, Plage plage, StyleDeLaPlage style) =>
        LesFragments.Appliquer(fragments, texte.Length, plage,
            surcharges => surcharges.Poser(style).Normaliser(commun),
            surcharges => surcharges.Normaliser(commun));

    /// <summary>
    /// Si toute la plage porte la valeur, la retirer ; sinon, la poser partout. Une plage
    /// mixte prend donc la valeur, comme un bouton d'état qu'on presse sur une sélection où
    /// il ne s'allume qu'à moitié.
    ///
    /// Retirer, c'est poser la valeur éteinte — maigre, droit, pas de souligné —, et non
    /// rendre celle du bloc : sur un bloc gras, nul rendrait le gras qu'on vient d'ôter. Le
    /// souligné posé est le trait simple, le seul que Publisher pose au clavier ; les autres
    /// sortes se posent par <see cref="Poser(string, IList{StyleSpan}, StyleEffectif, Plage, StyleDeLaPlage)"/>.
    /// </summary>
    public static bool Basculer(
        string texte, IList<StyleSpan> fragments, StyleEffectif commun, Plage plage, Bascule bascule)
    {
        var allume = Allume(texte, Lecture(fragments), commun, plage, bascule) == true;

        var style = bascule switch
        {
            Bascule.Gras => new StyleDeLaPlage { Bold = !allume },
            Bascule.Italique => new StyleDeLaPlage { Italic = !allume },
            Bascule.Souligne => new StyleDeLaPlage { Underline = allume ? Souligne.Aucun : Souligne.Simple },
            _ => throw new ArgumentOutOfRangeException(nameof(bascule)),
        };

        return Poser(texte, fragments, commun, plage, style);
    }

    /// <summary>
    /// Rend à la plage le style du bloc — <c>Ctrl+Espace</c> : plus aucun fragment n'y dit
    /// quoi que ce soit, décalage de ligne de base compris. Ce qui dépasse de la plage garde
    /// le sien. Le style du bloc est demandé pour juger du changement : une plage qui ne
    /// s'en écarte qu'en apparence — un fragment qui répète le bloc — n'a rien à rendre.
    /// </summary>
    public static bool Retirer(string texte, IList<StyleSpan> fragments, StyleEffectif commun, Plage plage) =>
        LesFragments.Appliquer(fragments, texte.Length, plage,
            _ => default,
            surcharges => surcharges.Normaliser(commun));

    // -------------------------------------------------------------- les paragraphes

    /// <summary>
    /// Les paragraphes que touche une plage de caractères, comptés comme les écarts le
    /// sont (<see cref="Recalage.ParagrapheDe"/>) : du paragraphe de son premier caractère à
    /// celui de son dernier. Une plage vide — le curseur — touche le paragraphe où elle
    /// tombe. Une ligne sélectionnée saut compris ne touche pas le paragraphe d'après : le
    /// saut appartient au paragraphe qu'il clôt.
    /// </summary>
    public static Plage Paragraphes(string texte, Plage caracteres)
    {
        var (debut, fin) = Borner(caracteres, texte.Length);
        var premier = Recalage.ParagrapheDe(texte, debut);
        var dernier = fin > debut ? Recalage.ParagrapheDe(texte, fin - 1) : premier;
        return new Plage(premier, dernier - premier + 1);
    }

    /// <summary>
    /// Ce que les paragraphes de la plage ont en commun, réglage par réglage ; nul, le
    /// réglage est mixte — alors aucun bouton d'alignement n'est allumé (R6). Une plage vide
    /// se lit au paragraphe où elle tombe.
    /// </summary>
    public static ReglagesDeLaPlage Lire(
        string texte, IReadOnlyList<ParagraphSpan> ecarts, ParagrapheEffectif commun, Plage paragraphes)
    {
        var nombre = Recalage.NombreDeParagraphes(texte);
        var (debut, fin) = Borner(paragraphes, nombre);
        if (debut == fin) (debut, fin) = debut < nombre ? (debut, debut + 1) : (nombre - 1, nombre);

        var reglages = LesEcarts.Segments(ecarts, nombre, debut, fin)
            .Select(segment => segment.Valeurs.Sur(commun)).ToList();

        return new ReglagesDeLaPlage
        {
            IndentLeft = Commune(reglages, r => r.IndentLeft),
            IndentRight = Commune(reglages, r => r.IndentRight),
            IndentFirstLine = Commune(reglages, r => r.IndentFirstLine),
            SpaceBefore = Commune(reglages, r => r.SpaceBefore),
            SpaceAfter = Commune(reglages, r => r.SpaceAfter),
            Align = Commune(reglages, r => r.Align),
        };
    }

    /// <summary>
    /// Pose sur les paragraphes de la plage chaque réglage que <paramref name="reglages"/>
    /// renseigne, en écarts : deux écarts voisins identiques fusionnent, et un écart qui ne
    /// s'écarte plus de rien disparaît. Un paragraphe vide se règle comme un autre. Rend
    /// vrai si la collection a changé.
    /// </summary>
    public static bool Poser(
        string texte, IList<ParagraphSpan> ecarts, ParagrapheEffectif commun, Plage paragraphes,
        ReglagesDeLaPlage reglages) =>
        LesEcarts.Appliquer(ecarts, Recalage.NombreDeParagraphes(texte), paragraphes,
            surcharges => surcharges.Poser(reglages).Normaliser(commun),
            surcharges => surcharges.Normaliser(commun));

    /// <summary>
    /// Si tous les paragraphes de la plage ont le fer <paramref name="fer"/>, les rendre au
    /// fer à gauche ; sinon, le leur donner. C'est la bascule de Word pour <c>Ctrl+E</c>,
    /// <c>Ctrl+R</c> et <c>Ctrl+J</c> ; les boutons de la barre, eux, ne basculent pas — un
    /// fer est toujours allumé —, et posent (<see cref="Poser(string, IList{ParagraphSpan}, ParagrapheEffectif, Plage, ReglagesDeLaPlage)"/>).
    /// </summary>
    public static bool Basculer(
        string texte, IList<ParagraphSpan> ecarts, ParagrapheEffectif commun, Plage paragraphes, TextAlign fer)
    {
        var porte = Lire(texte, Lecture(ecarts), commun, paragraphes).Align == fer;
        return Poser(texte, ecarts, commun, paragraphes,
            new ReglagesDeLaPlage { Align = porte ? TextAlign.Left : fer });
    }

    /// <summary>
    /// Rend aux paragraphes de la plage les réglages du bloc : plus aucun écart n'y dit quoi
    /// que ce soit. Le pendant de <see cref="Retirer(string, IList{StyleSpan}, StyleEffectif, Plage)"/>.
    /// </summary>
    public static bool Retirer(
        string texte, IList<ParagraphSpan> ecarts, ParagrapheEffectif commun, Plage paragraphes) =>
        LesEcarts.Appliquer(ecarts, Recalage.NombreDeParagraphes(texte), paragraphes,
            _ => default,
            surcharges => surcharges.Normaliser(commun));

    // -------------------------------------------------------------- en commun

    /// <summary>
    /// Une plage ramenée à une étendue de <paramref name="etendue"/> caractères ou
    /// paragraphes : le début dans les bornes, la fin jamais avant le début. Une longueur
    /// négative fait une plage vide ; la fin se calcule en long, comme partout ailleurs.
    /// </summary>
    private static (int Debut, int Fin) Borner(Plage plage, int etendue)
    {
        var debut = Math.Clamp(plage.Start, 0, etendue);
        return (debut, (int)Math.Clamp((long)plage.Start + Math.Max(0, plage.Length), debut, etendue));
    }

    /// <summary>Les styles effectifs, bruts, de chaque segment de la plage ; celui de la frappe pour une plage vide.</summary>
    private static IReadOnlyList<StyleEffectif> Styles(
        string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun, Plage plage)
    {
        var (debut, fin) = Borner(plage, texte.Length);
        if (debut == fin)
        {
            var ancre = Recalage.Ancre(texte, new Edition(debut, 0, 1));
            if (ancre < 0) return [commun];
            (debut, fin) = (ancre, ancre + 1);
        }

        return LesFragments.Segments(fragments, texte.Length, debut, fin)
            .Select(segment => segment.Valeurs.Sur(commun)).ToList();
    }

    private static T? Commune<TStyle, T>(IReadOnlyList<TStyle> styles, Func<TStyle, T> valeur) where T : struct
    {
        var premiere = valeur(styles[0]);
        return styles.All(style => EqualityComparer<T>.Default.Equals(valeur(style), premiere)) ? premiere : null;
    }

    /// <summary>La brute si toute la plage la porte ; sinon la résolue, si toute la plage la dessine.</summary>
    private static string? Commune(
        IReadOnlyList<StyleEffectif> styles, Func<StyleEffectif, string> valeur, Func<string, string> resoudre)
    {
        var brute = valeur(styles[0]);
        if (styles.All(style => valeur(style) == brute)) return brute;

        var resolue = resoudre(brute);
        return styles.All(style => resoudre(valeur(style)) == resolue) ? resolue : null;
    }

    /// <summary>Une valeur qu'on écrit, ou nulle si c'est déjà celle du bloc.</summary>
    private static T? Sauf<T>(T? valeur, T bloc) where T : struct =>
        valeur is { } v && !EqualityComparer<T>.Default.Equals(v, bloc) ? v : null;

    private static string? Sauf(string? valeur, string bloc) =>
        valeur is not null && !string.Equals(valeur, bloc, StringComparison.Ordinal) ? valeur : null;

    private static IReadOnlyList<T> Lecture<T>(IList<T> liste) => liste as IReadOnlyList<T> ?? liste.ToList();

    // -------------------------------------------------------------- ce que disent les éléments

    /// <summary>
    /// Ce qu'un fragment dit, sans sa place : chaque propriété nulle laisse passer celle de
    /// dessous, et un décalage nul aussi — la règle de <see cref="StyleEffectif.Decouper"/>.
    ///
    /// Toutes les propriétés de style de <see cref="StyleSpan"/> y sont, et doivent y être :
    /// une propriété oubliée ici serait perdue dans toute plage qu'on met en forme, et deux
    /// fragments qui ne diffèrent que par elle seraient pris l'un pour l'autre. Un test le
    /// vérifie par réflexion ; un poste qui ajoute une propriété au fragment (N9, la langue ;
    /// N24, les capitales) l'ajoute ici.
    /// </summary>
    private readonly record struct Surcharges(
        string? FontFamily, double? FontSize, bool? Bold, bool? Italic, Souligne? Underline,
        string? Foreground, double BaselineShift)
    {
        public static Surcharges De(StyleSpan f) =>
            new(f.FontFamily, f.FontSize, f.Bold, f.Italic, f.Underline, f.Foreground, f.BaselineShift);

        public StyleSpan Fragment(int debut, int fin) => new()
        {
            Start = debut, Length = fin - debut, FontFamily = FontFamily, FontSize = FontSize, Bold = Bold,
            Italic = Italic, Underline = Underline, Foreground = Foreground, BaselineShift = BaselineShift,
        };

        /// <summary>Ce que disent ces surcharges recouvertes par <paramref name="dessus"/>, qui l'emporte.</summary>
        public Surcharges Puis(Surcharges dessus) => new(
            dessus.FontFamily ?? FontFamily, dessus.FontSize ?? FontSize, dessus.Bold ?? Bold,
            dessus.Italic ?? Italic, dessus.Underline ?? Underline, dessus.Foreground ?? Foreground,
            dessus.BaselineShift != 0 ? dessus.BaselineShift : BaselineShift);

        /// <summary>Le style d'un caractère que ces surcharges recouvrent, sur le style du bloc.</summary>
        public StyleEffectif Sur(StyleEffectif commun) => new(
            FontFamily ?? commun.FontFamily, FontSize ?? commun.FontSize, Bold ?? commun.Bold,
            Italic ?? commun.Italic, Underline ?? commun.Underline, Foreground ?? commun.Foreground,
            BaselineShift != 0 ? BaselineShift : commun.BaselineShift);

        public Surcharges Poser(StyleDeLaPlage style) => new(
            style.FontFamily ?? FontFamily, style.FontSize ?? FontSize, style.Bold ?? Bold,
            style.Italic ?? Italic, style.Underline ?? Underline, style.Foreground ?? Foreground,
            style.BaselineShift ?? BaselineShift);

        /// <summary>
        /// Nulle, toute valeur égale à celle du bloc — comparée brute. Ce qui reste dit
        /// exactement ce que le caractère a de différent du bloc, et deux caractères qui se
        /// dessinent pareil ont les mêmes surcharges normalisées.
        /// </summary>
        public Surcharges Normaliser(StyleEffectif commun) => new(
            Sauf(FontFamily, commun.FontFamily), Sauf(FontSize, commun.FontSize), Sauf(Bold, commun.Bold),
            Sauf(Italic, commun.Italic), Sauf(Underline, commun.Underline), Sauf(Foreground, commun.Foreground),
            BaselineShift.Equals(commun.BaselineShift) ? 0 : BaselineShift);
    }

    /// <summary>
    /// Ce qu'un écart de paragraphe dit, sans sa place : la règle de
    /// <see cref="ParagrapheEffectif.Resoudre"/>. Même exigence que pour
    /// <see cref="Surcharges"/> : toutes les propriétés de <see cref="ParagraphSpan"/> y
    /// sont (N23, les listes, l'y ajoutera).
    /// </summary>
    private readonly record struct SurchargesDeParagraphe(
        double? IndentLeft, double? IndentRight, double? IndentFirstLine, double? SpaceBefore,
        double? SpaceAfter, TextAlign? Align)
    {
        public static SurchargesDeParagraphe De(ParagraphSpan e) =>
            new(e.IndentLeft, e.IndentRight, e.IndentFirstLine, e.SpaceBefore, e.SpaceAfter, e.Align);

        public ParagraphSpan Ecart(int debut, int fin) => new()
        {
            Start = debut, Length = fin - debut, IndentLeft = IndentLeft, IndentRight = IndentRight,
            IndentFirstLine = IndentFirstLine, SpaceBefore = SpaceBefore, SpaceAfter = SpaceAfter, Align = Align,
        };

        public SurchargesDeParagraphe Puis(SurchargesDeParagraphe dessus) => new(
            dessus.IndentLeft ?? IndentLeft, dessus.IndentRight ?? IndentRight,
            dessus.IndentFirstLine ?? IndentFirstLine, dessus.SpaceBefore ?? SpaceBefore,
            dessus.SpaceAfter ?? SpaceAfter, dessus.Align ?? Align);

        public ParagrapheEffectif Sur(ParagrapheEffectif commun) => new(
            IndentLeft ?? commun.IndentLeft, IndentRight ?? commun.IndentRight,
            IndentFirstLine ?? commun.IndentFirstLine, SpaceBefore ?? commun.SpaceBefore,
            SpaceAfter ?? commun.SpaceAfter, Align ?? commun.Align);

        public SurchargesDeParagraphe Poser(ReglagesDeLaPlage reglages) => new(
            reglages.IndentLeft ?? IndentLeft, reglages.IndentRight ?? IndentRight,
            reglages.IndentFirstLine ?? IndentFirstLine, reglages.SpaceBefore ?? SpaceBefore,
            reglages.SpaceAfter ?? SpaceAfter, reglages.Align ?? Align);

        public SurchargesDeParagraphe Normaliser(ParagrapheEffectif commun) => new(
            Sauf(IndentLeft, commun.IndentLeft), Sauf(IndentRight, commun.IndentRight),
            Sauf(IndentFirstLine, commun.IndentFirstLine), Sauf(SpaceBefore, commun.SpaceBefore),
            Sauf(SpaceAfter, commun.SpaceAfter), Sauf(Align, commun.Align));
    }

    // -------------------------------------------------------------- la mise en forme, une fois

    /// <summary>
    /// Ce qui distingue un fragment de style d'un écart de paragraphe aux yeux de la mise en
    /// forme — où il tombe, ce qu'il dit, comment on en fait un autre. Le reste — la coupe,
    /// la superposition, le recollage, l'écriture — est le même, et s'écrit une fois.
    /// </summary>
    private abstract class Sorte<T, TValeurs> where T : class where TValeurs : struct, IEquatable<TValeurs>
    {
        /// <summary>Ce que l'élément recouvre, ramené à l'étendue : là où la composition le voit.</summary>
        protected abstract (int Debut, int Fin) Etendue(T element, int etendue);

        /// <summary>Sa place telle qu'elle est écrite, pour reconnaître un élément inchangé.</summary>
        protected abstract (int Start, int Length) Place(T element);

        protected abstract TValeurs Valeurs(T element);

        protected abstract TValeurs Puis(TValeurs dessous, TValeurs dessus);

        /// <summary>Un bout de <paramref name="modele"/> : sa copie, ramenée à [<paramref name="debut"/>, <paramref name="fin"/>).</summary>
        protected abstract T Morceau(T modele, int debut, int fin);

        protected abstract T Neuf(int debut, int fin, TValeurs valeurs);

        /// <summary>
        /// Les segments de [<paramref name="debut"/>, <paramref name="fin"/>) entre deux bornes
        /// d'éléments, chacun avec ce que les éléments qui le recouvrent y disent, superposés
        /// dans l'ordre de la liste : le dernier l'emporte.
        /// </summary>
        public List<(int Debut, int Fin, TValeurs Valeurs)> Segments(
            IReadOnlyList<T> elements, int etendue, int debut, int fin)
        {
            var etendues = elements.Select(element => Etendue(element, etendue)).ToArray();

            var coupures = new SortedSet<int> { debut, fin };
            foreach (var (d, f) in etendues)
            {
                if (debut < d && d < fin) coupures.Add(d);
                if (debut < f && f < fin) coupures.Add(f);
            }

            var segments = new List<(int, int, TValeurs)>(coupures.Count - 1);
            var precedente = debut;
            foreach (var coupure in coupures.Skip(1))
            {
                // Toute borne d'élément tombant dans la plage est une coupure : un élément
                // recouvre donc un segment entier, ou rien de lui.
                TValeurs valeurs = default;
                for (var i = 0; i < elements.Count; i++)
                {
                    if (etendues[i].Debut <= precedente && coupure <= etendues[i].Fin)
                    {
                        valeurs = Puis(valeurs, Valeurs(elements[i]));
                    }
                }

                segments.Add((precedente, coupure, valeurs));
                precedente = coupure;
            }

            return segments;
        }

        /// <summary>
        /// Met en forme la plage et porte le résultat sur la collection. Rend vrai si elle a
        /// changé.
        ///
        /// <paramref name="retouche"/> dit ce qu'un segment portera, normalisé, d'après ce
        /// qu'il porte ; <paramref name="normaliser"/> ramène ce qu'un élément dit à ce qu'il
        /// change au bloc. C'est par elle qu'on juge qu'un changement est réel, et que deux
        /// voisins disent la même chose.
        /// </summary>
        public bool Appliquer(
            IList<T> collection, int etendue, Plage plage,
            Func<TValeurs, TValeurs> retouche, Func<TValeurs, TValeurs> normaliser)
        {
            var cible = Retoucher(Lecture(collection), etendue, plage, retouche, normaliser);
            return cible is not null && Ecrire(collection, cible);
        }

        /// <summary>La liste d'après, ou null s'il n'y a rien à changer.</summary>
        private List<T>? Retoucher(
            IReadOnlyList<T> elements, int etendue, Plage plage,
            Func<TValeurs, TValeurs> retouche, Func<TValeurs, TValeurs> normaliser)
        {
            var (a, b) = Borner(plage, etendue);
            if (a >= b) return null;

            // 1. Ce que la plage portera, segment par segment, et les segments voisins qui
            //    porteront la même chose réunis. Rien ne change à l'œil si chaque segment
            //    porte déjà ce qu'on lui demande : on n'écrit rien, pas même une liste
            //    plus propre.
            var reel = false;
            var neufs = new List<(int Debut, int Fin, TValeurs Valeurs)>();
            foreach (var (debut, fin, avant) in Segments(elements, etendue, a, b))
            {
                var apres = retouche(avant);
                reel |= !normaliser(avant).Equals(apres);

                if (neufs.Count > 0 && neufs[^1].Valeurs.Equals(apres)) neufs[^1] = neufs[^1] with { Fin = fin };
                else neufs.Add((debut, fin, apres));
            }

            if (!reel) return null;
            neufs.RemoveAll(neuf => neuf.Valeurs.Equals(default));

            // 2. Les éléments, dans leur ordre. Un élément qui recouvre une partie de la
            //    plage n'en garde que ses bouts du dehors, à sa place : l'ordre décide de
            //    ce qui l'emporte là où plusieurs se recouvrent hors de la plage, et il ne
            //    doit pas changer. Un élément qui ne dit rien disparaît avec ses bouts.
            var sortie = new List<T>(elements.Count + neufs.Count + 1);
            var insertion = -1;
            foreach (var element in elements)
            {
                var (debut, fin) = Etendue(element, etendue);
                if (debut >= fin || debut >= b || fin <= a)
                {
                    sortie.Add(element);
                    continue;
                }

                var dit = !Valeurs(element).Equals(default);
                if (debut < a && dit) sortie.Add(Morceau(element, debut, a));
                if (insertion < 0) insertion = sortie.Count;
                if (fin > b && dit) sortie.Add(Morceau(element, b, fin));
            }

            // Les segments neufs ne recouvrent rien d'autre : leur rang ne change aucun
            // style. Ils vont là où était le premier élément touché, ou devant le premier
            // qui commence après la plage — une liste rangée le reste.
            if (insertion < 0)
            {
                insertion = sortie.FindIndex(element => Etendue(element, etendue).Debut >= b);
                if (insertion < 0) insertion = sortie.Count;
            }

            Recoller(sortie, neufs, ref insertion, etendue, a, b, normaliser);

            sortie.InsertRange(insertion, neufs.Select(neuf => Neuf(neuf.Debut, neuf.Fin, neuf.Valeurs)));
            return sortie;
        }

        /// <summary>
        /// Recolle aux bornes ce qui dit la même chose de part et d'autre.
        ///
        /// Un segment neuf qui touche une borne se fond dans l'élément qui finit — ou
        /// commence — à cette borne et change au bloc la même chose que lui : l'élément
        /// s'allonge sur le segment, que rien d'autre ne recouvre plus, et y dessine ce que
        /// le segment aurait dessiné. C'est ce qui rend, au gras rebasculé au milieu d'un
        /// fragment, le fragment d'avant la première bascule.
        ///
        /// Quand l'élément de gauche a pris toute la plage, il ne se recolle à celui de
        /// droite que s'ils se suivent dans la liste et disent exactement la même chose :
        /// allonger l'un sur l'autre changerait sinon ce qui l'emporte, hors de la plage,
        /// là où d'autres éléments les recouvrent. Les deux bouts d'un même fragment coupé
        /// remplissent toujours les deux conditions.
        /// </summary>
        private void Recoller(
            List<T> sortie, List<(int Debut, int Fin, TValeurs Valeurs)> neufs, ref int insertion,
            int etendue, int a, int b, Func<TValeurs, TValeurs> normaliser)
        {
            var gauche = -1;
            if (neufs.Count > 0 && neufs[0].Debut == a)
            {
                for (var j = sortie.Count - 1; j >= 0; j--)
                {
                    var (debut, fin) = Etendue(sortie[j], etendue);
                    if (fin != a || debut >= a || !normaliser(Valeurs(sortie[j])).Equals(neufs[0].Valeurs)) continue;

                    sortie[j] = Morceau(sortie[j], debut, neufs[0].Fin);
                    neufs.RemoveAt(0);
                    gauche = j;
                    break;
                }
            }

            if (neufs.Count > 0 && neufs[^1].Fin == b)
            {
                for (var k = 0; k < sortie.Count; k++)
                {
                    var (debut, fin) = Etendue(sortie[k], etendue);
                    if (debut != b || fin <= b || !normaliser(Valeurs(sortie[k])).Equals(neufs[^1].Valeurs)) continue;

                    sortie[k] = Morceau(sortie[k], neufs[^1].Debut, fin);
                    neufs.RemoveAt(neufs.Count - 1);
                    break;
                }
            }
            else if (gauche >= 0 && Etendue(sortie[gauche], etendue).Fin == b)
            {
                foreach (var k in new[] { gauche - 1, gauche + 1 })
                {
                    if (k < 0 || k >= sortie.Count) continue;
                    var (debut, fin) = Etendue(sortie[k], etendue);
                    if (debut != b || fin <= b || !Valeurs(sortie[k]).Equals(Valeurs(sortie[gauche]))) continue;

                    sortie[Math.Min(gauche, k)] = Morceau(sortie[gauche], Etendue(sortie[gauche], etendue).Debut, fin);
                    sortie.RemoveAt(Math.Max(gauche, k));
                    if (Math.Max(gauche, k) < insertion) insertion--;
                    break;
                }
            }
        }

        /// <summary>
        /// Porte la liste d'après sur la collection par ses propres opérations : ce qui ne
        /// change pas en tête et en queue est laissé, le milieu est remplacé élément par
        /// élément, puis allongé ou raccourci. Un élément inchangé reste le même objet.
        /// </summary>
        private bool Ecrire(IList<T> collection, IReadOnlyList<T> cible)
        {
            var tete = 0;
            while (tete < collection.Count && tete < cible.Count && Pareils(collection[tete], cible[tete])) tete++;

            var queue = 0;
            while (queue < collection.Count - tete && queue < cible.Count - tete &&
                   Pareils(collection[collection.Count - 1 - queue], cible[cible.Count - 1 - queue]))
            {
                queue++;
            }

            var anciens = collection.Count - tete - queue;
            var nouveaux = cible.Count - tete - queue;
            if (anciens == 0 && nouveaux == 0) return false;

            var remplaces = Math.Min(anciens, nouveaux);
            for (var i = 0; i < remplaces; i++)
            {
                if (!Pareils(collection[tete + i], cible[tete + i])) collection[tete + i] = cible[tete + i];
            }

            for (var i = remplaces; i < anciens; i++) collection.RemoveAt(tete + remplaces);
            for (var i = remplaces; i < nouveaux; i++) collection.Insert(tete + i, cible[tete + i]);
            return true;
        }

        private bool Pareils(T x, T y) =>
            ReferenceEquals(x, y) || (Place(x) == Place(y) && Valeurs(x).Equals(Valeurs(y)));
    }

    private sealed class SorteDesFragments : Sorte<StyleSpan, Surcharges>
    {
        protected override (int Debut, int Fin) Etendue(StyleSpan fragment, int longueur) =>
            StyleEffectif.Etendue(fragment, longueur);

        protected override (int Start, int Length) Place(StyleSpan fragment) => (fragment.Start, fragment.Length);

        protected override Surcharges Valeurs(StyleSpan fragment) => Surcharges.De(fragment);

        protected override Surcharges Puis(Surcharges dessous, Surcharges dessus) => dessous.Puis(dessus);

        protected override StyleSpan Morceau(StyleSpan modele, int debut, int fin)
        {
            var morceau = modele.Clone();
            morceau.Start = debut;
            morceau.Length = fin - debut;
            return morceau;
        }

        protected override StyleSpan Neuf(int debut, int fin, Surcharges valeurs) => valeurs.Fragment(debut, fin);
    }

    private sealed class SorteDesEcarts : Sorte<ParagraphSpan, SurchargesDeParagraphe>
    {
        protected override (int Debut, int Fin) Etendue(ParagraphSpan ecart, int nombre) =>
            ParagrapheEffectif.Etendue(ecart, nombre);

        protected override (int Start, int Length) Place(ParagraphSpan ecart) => (ecart.Start, ecart.Length);

        protected override SurchargesDeParagraphe Valeurs(ParagraphSpan ecart) => SurchargesDeParagraphe.De(ecart);

        protected override SurchargesDeParagraphe Puis(SurchargesDeParagraphe dessous, SurchargesDeParagraphe dessus) =>
            dessous.Puis(dessus);

        protected override ParagraphSpan Morceau(ParagraphSpan modele, int debut, int fin)
        {
            var morceau = modele.Clone();
            morceau.Start = debut;
            morceau.Length = fin - debut;
            return morceau;
        }

        protected override ParagraphSpan Neuf(int debut, int fin, SurchargesDeParagraphe valeurs) =>
            valeurs.Ecart(debut, fin);
    }
}
