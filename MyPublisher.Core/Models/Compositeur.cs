using System;
using System.Collections.Generic;
using System.Linq;

namespace MyPublisher.Models;

/// <summary>Un morceau de ligne d'un seul style, avec sa largeur et son abscisse.</summary>
public readonly record struct Morceau(string Texte, StyleEffectif Style, double X, double Largeur);

/// <summary>
/// Une ligne composée : ses morceaux, la largeur qu'elle occupe, la hauteur naturelle
/// qu'elle réclame, si elle termine son paragraphe, et le cadre que son paragraphe lui
/// donne.
///
/// La hauteur est celle du plus grand style de la ligne : une ligne portant un mot en
/// 18 pt au milieu d'un texte en 10 pt doit laisser passer le 18 pt.
///
/// Le drapeau de fin de paragraphe n'est pas un détail : la dernière ligne d'un
/// paragraphe justifié ne se justifie pas, sinon « fin. » s'étirerait sur toute la
/// largeur du bloc.
///
/// <paramref name="Retrait"/> est l'abscisse où la ligne commence, comptée depuis le
/// bord gauche de la zone de texte ; <paramref name="Disponible"/> ce qui lui reste
/// jusqu'au bord droit. Les deux sont calculés ici, une fois, et non dans chacun des
/// deux rendus : la cible d'une ligne justifiée, d'un fer à droite ou d'un centrage
/// est cette largeur-là et non celle du bloc. C'est le point où l'écran et l'export
/// auraient divergé le plus facilement, et sans que rien ne le signale — ils se
/// seraient trompés ensemble.
///
/// <paramref name="EspaceAvant"/> et <paramref name="EspaceApres"/> ne portent que sur
/// la première et la dernière ligne du paragraphe : les lignes du milieu valent zéro,
/// si bien qu'un rendu peut les ajouter sans se demander où il en est.
/// </summary>
public readonly record struct LigneComposee(
    IReadOnlyList<Morceau> Morceaux, double Largeur, double Naturelle, bool FinDeParagraphe,
    double Retrait, double Disponible, double EspaceAvant, double EspaceApres)
{
    public string Texte => string.Concat(Morceaux.Select(m => m.Texte));
}

/// <summary>
/// Découpe un texte en lignes pour une largeur donnée, en tenant compte des fragments
/// de style qui le traversent.
///
/// Pourquoi ne pas laisser <c>XTextFormatter</c> le faire, comme jusqu'au 6 septembre
/// 2026 : parce qu'il ne publie pas sa découpe, n'expose aucune avance de ligne, et ne
/// sait pas changer de police au milieu d'un paragraphe. Les trois manques se
/// tiennent : c'est la même composition qui les résout.
///
/// Le noyau ne connaît ni Avalonia ni PDFsharp. La mesure et la hauteur naturelle des
/// polices lui sont donc passées en paramètres — et c'est précisément ce qui permet à
/// l'écran et à l'export d'appeler **la même** découpe, chacun avec sa règle à
/// mesurer, au lieu d'en avoir chacun une.
/// </summary>
public static class Compositeur
{
    /// <summary>
    /// Ce que la composition a besoin de savoir d'une police, et que le noyau ne peut
    /// pas calculer lui-même.
    /// </summary>
    public interface IMetriques
    {
        /// <summary>Largeur d'un texte dans un style donné, en points.</summary>
        double Largeur(string texte, StyleEffectif style);

        /// <summary>Hauteur de ligne naturelle de la police, en points.</summary>
        double Naturelle(StyleEffectif style);
    }

    /// <summary>
    /// Compose un bloc : découpe le texte selon ses fragments de style, puis répartit
    /// les morceaux en lignes qui tiennent dans <paramref name="largeur"/>, retraits de
    /// paragraphe déduits.
    ///
    /// Les cinq paramètres de contenu sont ce que le bloc dit — le style commun et ses
    /// écarts, les réglages de paragraphe communs et les leurs — et les deux derniers
    /// ce que la page impose. Aucun n'est facultatif : un paramètre à valeur par défaut
    /// serait exactement le moyen pour un des deux rendus de garder l'ancien
    /// comportement sans que rien ne l'annonce, et c'est ce que R1 a été écrit pour
    /// empêcher.
    /// </summary>
    public static IReadOnlyList<LigneComposee> Composer(
        string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun,
        IReadOnlyList<ParagraphSpan> ecarts, ParagrapheEffectif communParagraphe,
        double largeur, IMetriques metriques)
    {
        var lignes = new List<LigneComposee>();
        if (string.IsNullOrEmpty(texte)) return lignes;

        // Un mot porte son style ; c'est l'unité que la découpe déplace d'une ligne
        // à l'autre. Un mot que deux fragments se partagent devient deux unités
        // insécables — d'où le drapeau.
        var mots = Decouper(texte, fragments, commun, metriques);

        // Le compte des paragraphes sort de la découpe elle-même et non d'un second
        // parcours du texte : c'est elle qui décide où ils commencent, et les deux ne
        // doivent pas pouvoir se contredire.
        var reglages = ParagrapheEffectif.Resoudre(
            mots.Count(u => u.FinDeLigne) + 1, ecarts, communParagraphe);

        var courante = new List<Morceau>();
        var largeurCourante = 0.0;

        var paragraphe = 0;
        var premiereLigne = true;
        var retrait = 0.0;
        var disponible = largeur;
        Cadrer();

        // Le cadre de la ligne à venir. Il change deux fois : au passage à la ligne,
        // parce que la première ligne d'un paragraphe n'a pas le même retrait que les
        // suivantes, et au changement de paragraphe.
        void Cadrer()
        {
            var reglage = Reglage();
            retrait = reglage.IndentLeft + (premiereLigne ? reglage.IndentFirstLine : 0);
            // Une largeur nulle ou négative — un retrait plus large que le bloc — ferait
            // renvoyer à la ligne un mot qui n'y tiendrait pas davantage. On laisse un
            // point plutôt que de refuser de composer.
            disponible = Math.Max(1, largeur - retrait - reglage.IndentRight);
        }

        ParagrapheEffectif Reglage() => reglages[Math.Min(paragraphe, reglages.Count - 1)];

        void Fermer(bool finDeParagraphe)
        {
            // Les blancs de fin de ligne ne comptent ni dans la largeur ni à l'écran.
            while (courante.Count > 0 && courante[^1].Texte.Trim().Length == 0)
            {
                largeurCourante -= courante[^1].Largeur;
                courante.RemoveAt(courante.Count - 1);
            }

            if (courante.Count > 0)
            {
                var dernier = courante[^1];
                var rogne = dernier.Texte.TrimEnd();
                if (rogne.Length != dernier.Texte.Length)
                {
                    var largeurRognee = metriques.Largeur(rogne, dernier.Style);
                    largeurCourante += largeurRognee - dernier.Largeur;
                    courante[^1] = dernier with { Texte = rogne, Largeur = largeurRognee };
                }
            }

            var naturelle = courante.Count == 0
                ? metriques.Naturelle(commun)
                : courante.Max(m => metriques.Naturelle(m.Style));

            var reglage = Reglage();

            lignes.Add(new LigneComposee(
                [.. courante], largeurCourante, naturelle, finDeParagraphe,
                retrait, disponible,
                premiereLigne ? reglage.SpaceBefore : 0,
                finDeParagraphe ? reglage.SpaceAfter : 0));

            courante.Clear();
            largeurCourante = 0;

            // Fermer sur une fin de paragraphe ouvre le suivant ; fermer sur un renvoi
            // à la ligne reste dans le même paragraphe, mais n'est plus sa première.
            if (finDeParagraphe) paragraphe++;
            premiereLigne = finDeParagraphe;
            Cadrer();
        }

        foreach (var mot in mots)
        {
            if (mot.FinDeLigne) { Fermer(finDeParagraphe: true); continue; }

            var l = metriques.Largeur(mot.Texte, mot.Style);

            // On ne renvoie à la ligne que si quelque chose y est déjà : un mot plus
            // large que la boîte garde sa ligne plutôt que d'être coupé au milieu.
            if (courante.Count > 0 && largeurCourante + metriques.Largeur(mot.Texte.TrimEnd(), mot.Style) > disponible)
            {
                Fermer(finDeParagraphe: false);
                var debut = mot.Texte.TrimStart();
                l = metriques.Largeur(debut, mot.Style);
                courante.Add(new Morceau(debut, mot.Style, largeurCourante, l));
            }
            else
            {
                courante.Add(new Morceau(mot.Texte, mot.Style, largeurCourante, l));
            }

            largeurCourante += l;
        }

        Fermer(finDeParagraphe: true);

        // Chaque morceau porte son abscisse relative au début de la ligne ; on la
        // recalcule ici plutôt que dans la boucle, où la ligne pouvait encore bouger.
        for (var i = 0; i < lignes.Count; i++)
        {
            var x = 0.0;
            var morceaux = lignes[i].Morceaux.Select(m => { var p = m with { X = x }; x += m.Largeur; return p; }).ToArray();
            lignes[i] = lignes[i] with { Morceaux = morceaux };
        }

        return lignes;
    }

    private readonly record struct Unite(string Texte, StyleEffectif Style, bool FinDeLigne);

    /// <summary>
    /// Le texte réduit à une suite d'unités : des mots avec les espaces qui les
    /// suivent, des marques de fin de ligne, et jamais deux styles dans la même unité.
    /// </summary>
    private static List<Unite> Decouper(
        string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun,
        IMetriques metriques)
    {
        var unites = new List<Unite>();
        var normalise = texte.Replace("\r\n", "\n").Replace('\r', '\n');

        // Les fragments repèrent des positions dans le texte d'origine ; la
        // normalisation des fins de ligne peut les décaler. On découpe donc le texte
        // d'origine, puis on traite les retours à la ligne dans les morceaux obtenus.
        foreach (var (morceau, style) in StyleEffectif.Decouper(texte, fragments, commun))
        {
            var propre = morceau.Replace("\r\n", "\n").Replace('\r', '\n');
            var debut = 0;

            while (debut <= propre.Length)
            {
                var saut = propre.IndexOf('\n', debut);
                var segment = saut < 0 ? propre[debut..] : propre[debut..saut];

                foreach (var mot in Mots(segment))
                {
                    unites.Add(new Unite(mot, style, FinDeLigne: false));
                }

                if (saut < 0) break;
                unites.Add(new Unite(string.Empty, style, FinDeLigne: true));
                debut = saut + 1;
            }
        }

        _ = normalise;
        return unites;
    }

    /// <summary>
    /// Les mots d'un segment, chacun avec les espaces qui le suivent : garder l'espace
    /// attaché évite de le réinventer au moment de recoller, et de perdre les espaces
    /// multiples que l'auteur a voulus.
    /// </summary>
    private static IEnumerable<string> Mots(string segment)
    {
        var debut = 0;

        while (debut < segment.Length)
        {
            var fin = segment.IndexOf(' ', debut);
            if (fin < 0) { yield return segment[debut..]; yield break; }

            var apres = fin;
            while (apres < segment.Length && segment[apres] == ' ') apres++;

            yield return segment[debut..apres];
            debut = apres;
        }
    }

    /// <summary>
    /// Décalage supplémentaire à appliquer à chaque morceau pour justifier la ligne,
    /// dans l'ordre des morceaux.
    ///
    /// Le calcul est ici, et pas dans les deux rendus, parce qu'il est facile de le
    /// faire faussement : le blanc se répartit entre les **mots**, et un morceau n'est
    /// pas un mot. « un <b>deux</b> trois » compte trois morceaux mais deux blancs à
    /// élargir ; distribuer par morceau décalerait le texte d'un tiers de blanc à
    /// chaque changement de style, sans que rien ne le signale.
    /// </summary>
    /// <remarks>
    /// La cible n'est plus un paramètre depuis R2 : c'est <c>ligne.Disponible</c>, que
    /// la composition a calculé. Un rendu qui aurait passé la largeur du bloc au lieu
    /// de celle de la ligne aurait étiré chaque paragraphe en retrait jusque sous son
    /// voisin — et l'autre rendu aurait fait la même erreur, donc la comparaison
    /// écran ↔ PDF n'aurait rien vu. Ôter le paramètre rend la faute impossible plutôt
    /// que détectable.
    /// </remarks>
    public static IReadOnlyList<double> Decalages(LigneComposee ligne)
    {
        var decalages = new double[ligne.Morceaux.Count];
        if (ligne.Morceaux.Count == 0) return decalages;

        var texte = ligne.Texte;
        var blancs = 0;

        // Fin d'une suite d'espaces, suivie d'autre chose : voilà un blanc à élargir.
        for (var i = 0; i < texte.Length - 1; i++)
        {
            if (texte[i] == ' ' && texte[i + 1] != ' ') blancs++;
        }

        var manque = ligne.Disponible - ligne.Largeur;
        if (blancs == 0 || manque <= 0) return decalages;

        var part = manque / blancs;
        var position = 0;
        var cumul = 0.0;

        for (var m = 0; m < ligne.Morceaux.Count; m++)
        {
            decalages[m] = cumul;

            var morceau = ligne.Morceaux[m].Texte;
            for (var i = 0; i < morceau.Length; i++)
            {
                var absolu = position + i;
                if (absolu < texte.Length - 1 && texte[absolu] == ' ' && texte[absolu + 1] != ' ')
                {
                    cumul += part;
                }
            }

            position += morceau.Length;
        }

        return decalages;
    }

    /// <summary>
    /// Hauteur totale d'un texte composé. L'avance d'une ligne dépend de sa propre
    /// hauteur naturelle : un paragraphe qui contient un mot en gros corps s'ouvre
    /// à cet endroit-là et pas ailleurs. L'espace avant et après le paragraphe s'y
    /// ajoute, comme il s'ajoute au rendu — c'est la même somme, sinon la mesure de
    /// débordement mentirait dès qu'un document se sert de l'espacement.
    /// </summary>
    public static double Hauteur(IReadOnlyList<LigneComposee> lignes, double multiple) =>
        lignes.Sum(l => l.EspaceAvant + Avance(l.Naturelle, multiple) + l.EspaceApres);

    /// <summary>
    /// Avance d'une ligne à la suivante. <paramref name="naturelle"/> est la hauteur
    /// de ligne de la police elle-même ; le multiple vient du modèle.
    ///
    /// C'est la sémantique retenue le 6 septembre 2026, et elle vient des documents
    /// réels : aucun des deux <c>.pub</c> déposés ne porte <c>fo:line-height</c>, ce
    /// qui veut dire qu'ils s'en tiennent au défaut de Publisher — la hauteur
    /// naturelle de la police. Un interligne « 1 » ne doit donc rien serrer, et
    /// « 1,25 » veut dire une fois et quart cette hauteur-là, jamais une fois et
    /// quart le corps.
    /// </summary>
    public static double Avance(double naturelle, double multiple) =>
        naturelle * (multiple > 0 ? multiple : 1);
}
