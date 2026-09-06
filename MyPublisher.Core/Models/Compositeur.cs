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
/// bord gauche de la zone de texte, **gouttières comprises** : une ligne de la deuxième
/// colonne porte déjà le pas qui l'y amène. <paramref name="Disponible"/> est ce qui lui
/// reste jusqu'au bord droit de **sa** colonne. Les deux sont calculés ici, une fois, et
/// non dans chacun des deux rendus : la cible d'une ligne justifiée, d'un fer à droite ou
/// d'un centrage est cette largeur-là et non celle du bloc. C'est le point où l'écran et
/// l'export auraient divergé le plus facilement, et sans que rien ne le signale — ils se
/// seraient trompés ensemble.
///
/// <paramref name="EspaceAvant"/> et <paramref name="EspaceApres"/> ne portent que sur
/// la première et la dernière ligne du paragraphe : les lignes du milieu valent zéro,
/// si bien qu'un rendu peut les ajouter sans se demander où il en est.
///
/// <paramref name="Colonne"/>, <paramref name="Haut"/> et <paramref name="Avance"/>
/// achèvent de poser la ligne : sa colonne, l'ordonnée de son sommet **dans cette
/// colonne** — espace avant déjà consommé — et de combien la suivante descend. Les trois
/// viennent d'ici pour la même raison que le retrait : le passage d'une colonne à l'autre
/// se décide sur une hauteur cumulée, et deux rendus qui la cumuleraient chacun de son
/// côté finiraient par ne pas couper au même endroit. Un rendu n'a donc plus rien à
/// additionner ; il lit.
/// </summary>
public readonly record struct LigneComposee(
    IReadOnlyList<Morceau> Morceaux, double Largeur, double Naturelle, bool FinDeParagraphe,
    TextAlign Fer,
    double Retrait, double Disponible, double EspaceAvant, double EspaceApres,
    int Colonne, double Haut, double Avance)
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
    /// Compose un bloc : découpe le texte selon ses fragments de style, répartit les
    /// morceaux en lignes qui tiennent dans une colonne, retraits de paragraphe déduits,
    /// puis range ces lignes dans les colonnes du bloc.
    ///
    /// Les cinq premiers paramètres sont ce que le bloc dit — le style commun et ses
    /// écarts, les réglages de paragraphe communs et les leurs — et les quatre suivants
    /// ce que sa géométrie impose. Aucun n'est facultatif : un paramètre à valeur par
    /// défaut serait exactement le moyen pour un des deux rendus de garder l'ancien
    /// comportement sans que rien ne l'annonce, et c'est ce que R1 a été écrit pour
    /// empêcher.
    ///
    /// <paramref name="hauteur"/> et <paramref name="interligne"/> n'étaient pas
    /// nécessaires tant qu'un bloc n'avait qu'une colonne : ils le deviennent parce que
    /// le passage à la colonne suivante est une décision verticale, et qu'on ne la prend
    /// qu'en sachant de combien chaque ligne descend.
    ///
    /// Ce qui dépasse la **dernière** colonne continue vers le bas, hors du bloc, comme
    /// un texte trop long le fait aujourd'hui dans un bloc d'une seule colonne. Rien
    /// n'est tronqué : couper ici livrerait un PDF amputé sans le dire, ce que le poste
    /// A2 est justement chargé d'empêcher, et il n'est pas écrit. La règle « une seule
    /// colonne » devient ainsi un cas particulier de la règle générale, et non un chemin
    /// de code à part — c'est ce qui garantit qu'aucun document existant ne bouge. Ce
    /// qu'il faudra à A2 est déjà là : la dernière ligne porte son <c>Haut</c> et son
    /// <c>Avance</c> dans sa colonne, et <see cref="Hauteur"/> en fait la somme.
    /// </summary>
    public static IReadOnlyList<LigneComposee> Composer(
        string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun,
        IReadOnlyList<ParagraphSpan> ecarts, ParagrapheEffectif communParagraphe,
        double largeur, double hauteur, Colonnage colonnes, double interligne,
        IMetriques metriques)
    {
        var lignes = new List<LigneComposee>();
        if (string.IsNullOrEmpty(texte)) return lignes;

        // Le texte est composé une fois pour la largeur d'UNE colonne : toutes ont la
        // même, la découpe ne dépend donc pas de celle où la ligne atterrira. C'est ce
        // qui permet de ranger les lignes en colonnes dans une seconde passe, sans
        // toucher au renvoi à la ligne.
        var largeurColonne = colonnes.Largeur(largeur);

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
        var disponible = largeurColonne;
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
            disponible = Math.Max(1, largeurColonne - retrait - reglage.IndentRight);
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

            // Colonne, hauteur et avance restent à zéro : c'est la seconde passe qui les
            // pose, une fois qu'on sait combien chaque ligne mesure.
            lignes.Add(new LigneComposee(
                [.. courante], largeurCourante, naturelle, finDeParagraphe,
                // Le fer appartient au paragraphe et non au bloc : ATA 21 mêle
                // justifié et fer à gauche dans le même cadre.
                reglage.Align,
                retrait, disponible,
                premiereLigne ? reglage.SpaceBefore : 0,
                finDeParagraphe ? reglage.SpaceAfter : 0,
                Colonne: 0, Haut: 0, Avance: 0));

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

            // Une tabulation n'a pas de largeur propre : elle vaut la distance qui
            // reste jusqu'au prochain taquet, et dépend donc d'où on en est sur la
            // ligne. On la calcule ici, où cette position est connue.
            var tabulation = mot.Texte == "\t";
            var l = tabulation
                ? ProchainTaquet(largeurCourante) - largeurCourante
                : metriques.Largeur(mot.Texte, mot.Style);

            // On ne renvoie à la ligne que si quelque chose y est déjà : un mot plus
            // large que la boîte garde sa ligne plutôt que d'être coupé au milieu.
            if (!tabulation && courante.Count > 0 &&
                largeurCourante + metriques.Largeur(mot.Texte.TrimEnd(), mot.Style) > disponible)
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

        Ranger(lignes, largeur, hauteur, colonnes, interligne);
        return lignes;
    }

    /// <summary>
    /// Range les lignes composées dans les colonnes du bloc : chacune reçoit sa colonne,
    /// l'ordonnée de son sommet dans cette colonne, son avance, et le pas qui la porte
    /// jusqu'à sa gouttière.
    ///
    /// La règle est celle qu'on lit : on remplit une colonne tant que la ligne suivante y
    /// tient, et on passe à la suivante sinon — quitte à couper un paragraphe en deux,
    /// puisque c'est ce que veut dire « le texte coule ». Trois garde-fous, chacun contre
    /// une faute qui ne se verrait pas :
    ///
    /// une colonne vide ne se quitte jamais, sinon une ligne plus haute que la colonne
    /// traverserait le bloc entier en n'écrivant nulle part ; une hauteur nulle ou
    /// négative — un bloc plus petit que ses marges — ne fait rompre aucune colonne,
    /// sinon chaque ligne partirait dans la sienne ; et la dernière colonne ne rompt
    /// pas, ce qui rend au bloc d'une seule colonne exactement le comportement qu'il
    /// avait avant ce poste, sans chemin de code séparé.
    /// </summary>
    private static void Ranger(
        List<LigneComposee> lignes, double largeur, double hauteur, Colonnage colonnes,
        double interligne)
    {
        var derniere = Math.Max(1, colonnes.Nombre) - 1;
        var pas = colonnes.Pas(largeur);

        var colonne = 0;
        var haut = 0.0;

        for (var i = 0; i < lignes.Count; i++)
        {
            var ligne = lignes[i];
            var avance = Avance(ligne.Naturelle, interligne);

            if (colonne < derniere && haut > 0 && hauteur > 0
                && haut + ligne.EspaceAvant + avance > hauteur)
            {
                colonne++;
                haut = 0;
            }

            // L'espace avant s'applique même en tête de colonne, comme il s'applique au
            // premier paragraphe du bloc. Choisi sans mesure — le relevé des deux .pub ne
            // porte aucun fo:margin-top —, mais choisi une fois pour les deux cas plutôt
            // que deux fois différemment.
            haut += ligne.EspaceAvant;

            lignes[i] = ligne with
            {
                Colonne = colonne,
                Haut = haut,
                Avance = avance,
                Retrait = ligne.Retrait + colonne * pas,
            };

            haut += avance + ligne.EspaceApres;
        }
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
    /// <summary>
    /// Intervalle des taquets par défaut, en points : un demi-pouce, comme Publisher.
    ///
    /// MyPublisher n'offre pas de taquets réglables, et n'en a pas besoin pour l'usage
    /// qu'on en fait : une tabulation avance jusqu'au prochain multiple de cet
    /// intervalle, mesuré depuis le bord gauche du texte. C'est ce que fait Publisher
    /// tant qu'aucun taquet n'a été posé à la main.
    ///
    /// Ce n'est pas un raffinement : les fiches de procédures d'urgence à reprendre
    /// portent 157 tabulations dans 98 de leurs 169 paragraphes. Les rendre par des
    /// espaces désalignerait les colonnes de consignes ; les perdre les collerait.
    /// </summary>
    public const double IntervalleTaquet = 36;

    /// <summary>La prochaine position de taquet strictement après <paramref name="x"/>.</summary>
    public static double ProchainTaquet(double x)
    {
        var suivant = (Math.Floor(x / IntervalleTaquet) + 1) * IntervalleTaquet;
        // Un taquet avance toujours d'au moins un peu : sans cela, une tabulation posée
        // exactement sur un taquet ne ferait rien et deux tabulations vaudraient une.
        return suivant > x ? suivant : x + IntervalleTaquet;
    }

    private static IEnumerable<string> Mots(string segment)
    {
        var debut = 0;

        while (debut < segment.Length)
        {
            // La tabulation se détache du mot qui la suit : sa largeur se calcule au
            // moment de la poser, pas au moment de la mesurer.
            if (segment[debut] == '\t') { yield return "\t"; debut++; continue; }

            var fin = segment.IndexOfAny([' ', '\t'], debut);
            if (fin < 0) { yield return segment[debut..]; yield break; }

            if (segment[fin] == '\t') { yield return segment[debut..fin]; debut = fin; continue; }

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
    /// Hauteur qu'un texte composé réclame, colonnes comprises : celle de la colonne la
    /// plus remplie, et non la somme de toutes. L'avance d'une ligne dépend de sa propre
    /// hauteur naturelle — un paragraphe qui contient un mot en gros corps s'ouvre à cet
    /// endroit-là et pas ailleurs — et l'espace avant comme après le paragraphe s'y
    /// ajoute, comme il s'ajoute au rendu : sinon la mesure de débordement mentirait dès
    /// qu'un document se sert de l'espacement.
    /// </summary>
    /// <remarks>
    /// L'interligne n'est plus un paramètre depuis R3, pour la raison qui avait ôté sa
    /// largeur à <see cref="Decalages"/> : la composition l'a déjà appliqué pour décider
    /// des ruptures de colonne, et un appelant qui en aurait passé un autre ici aurait
    /// mesuré un texte que personne ne dessine.
    /// </remarks>
    public static double Hauteur(IReadOnlyList<LigneComposee> lignes) =>
        lignes.Count == 0 ? 0 : lignes.Max(l => l.Haut + l.Avance + l.EspaceApres);

    /// <summary>
    /// De combien le texte descend sous le sommet de sa zone, selon l'alignement
    /// vertical du bloc.
    ///
    /// Jamais négatif, et c'est tout le poste A7 : un texte plus haut que sa zone se
    /// cale en haut, quel que soit l'alignement demandé. Centrer un texte qui déborde
    /// en escamoterait le début — or c'est le début qu'on lit, et ce qui manque à la
    /// fin se voit, tandis que ce qui manque au début ne se voit pas. Le seul bloc des
    /// deux documents réels qui porte <c>draw:textarea-vertical-align: middle</c> est
    /// justement dans ce cas : 26,86 pt composés dans 19,43 pt de haut.
    ///
    /// Le calage porte sur la colonne la plus remplie, comme <see cref="Hauteur"/> :
    /// deux colonnes inégalement remplies descendent ensemble, elles ne se centrent pas
    /// chacune pour son compte.
    /// </summary>
    public static double Decalage(
        IReadOnlyList<LigneComposee> lignes, double disponible, VerticalAlign fer)
    {
        var reste = disponible - Hauteur(lignes);
        if (reste <= 0) return 0;

        return fer switch
        {
            VerticalAlign.Middle => reste / 2,
            VerticalAlign.Bottom => reste,
            _ => 0,
        };
    }

    /// <summary>
    /// De combien le texte composé sort du BLOC, en points ; zéro s'il y tient.
    ///
    /// C'est le bloc qu'on mesure, et non la zone de texte. Depuis que l'export découpe
    /// au bloc comme l'écran le fait déjà, ce qui sort du bloc est exactement ce qui est
    /// amputé, et rien d'autre ne l'est. Un texte qui ne dépasse que dans la marge
    /// intérieure basse reste entier et lisible : le signaler serait crier au loup, et
    /// un avertissement qui crie au loup ne se lit plus — c'est la leçon de B5. Deux des
    /// neuf blocs d'ATA 21 sont dans ce cas, à 1,5 pt près.
    ///
    /// <paramref name="disponible"/> est la hauteur de la zone de texte, celle-là même
    /// qu'a reçue <see cref="Composer"/> ; <paramref name="margeBasse"/> ce qui reste
    /// entre elle et le bord du bloc.
    /// </summary>
    public static double Debordement(
        IReadOnlyList<LigneComposee> lignes, double disponible, double margeBasse,
        VerticalAlign fer) =>
        Math.Max(0, Decalage(lignes, disponible, fer) + Hauteur(lignes) - disponible - margeBasse);

    /// <summary>
    /// En deçà de quoi un débordement ne se signale pas, en points : un demi-point, la
    /// hauteur d'une queue de « p ».
    ///
    /// Le seuil n'est pas là pour absorber une erreur d'arrondi — elle est de l'ordre du
    /// millionième — mais parce que les deux rendus ne mesurent pas avec la même règle :
    /// le marqueur de l'écran sort des métriques d'Avalonia, l'avertissement de l'export
    /// de celles de PDFsharp. Un bloc au ras de son bord peut donc être signalé d'un côté
    /// et pas de l'autre, et aucun seuil ne referme cette fenêtre — seul un document qui
    /// se donne de l'air la referme. Ce qu'on écarte ici, c'est le débordement qui ne
    /// coûte rien à voir.
    /// </summary>
    public const double SeuilDebordement = 0.5;

    /// <summary>Le texte sort-il de son bloc au point que cela mérite d'être dit.</summary>
    public static bool Deborde(
        IReadOnlyList<LigneComposee> lignes, double disponible, double margeBasse,
        VerticalAlign fer) =>
        Debordement(lignes, disponible, margeBasse, fer) > SeuilDebordement;

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
