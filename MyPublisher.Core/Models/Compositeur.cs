using System;
using System.Collections.Generic;
using System.Linq;

namespace MyPublisher.Models;

/// <summary>
/// Un morceau de ligne d'un seul style, avec sa largeur et son abscisse, et la position de
/// son premier caractère dans le texte composé : <c>texte.Substring(Debut, Texte.Length)</c>
/// est son <paramref name="Texte"/> (poste N3). C'est ce qui permet de passer d'un point de
/// la ligne à un caractère du texte, et l'inverse — le curseur, le clic, la sélection.
/// </summary>
public readonly record struct Morceau(string Texte, StyleEffectif Style, double X, double Largeur, int Debut);

/// <summary>
/// Une portion soulignée d'une ligne : de <paramref name="Debut"/> à <paramref name="Fin"/>,
/// depuis l'origine de la ligne, sous le style <paramref name="Style"/> (poste I7). Voir
/// <see cref="Compositeur.Soulignes"/>.
/// </summary>
public readonly record struct EtendueSoulignee(double Debut, double Fin, StyleEffectif Style);

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
///
/// <paramref name="Debut"/> et <paramref name="Fin"/> sont la plage du texte que la ligne
/// couvre, et <paramref name="Paragraphe"/> le rang de son paragraphe (poste N3). La plage
/// compte les blancs que la composition a rognés — ceux de la fin de la ligne, et le blanc
/// de tête d'un mot renvoyé, qui restent au bout de la ligne qu'il quitte —, mais pas le
/// saut qui ferme un paragraphe : les lignes se suivent et couvrent tout le texte, sauts
/// exceptés. Une ligne renvoyée finit là où commence la suivante ; un paragraphe vide est
/// une ligne de plage vide. Le rang de paragraphe est celui que <see cref="Recalage"/>
/// compte : un « \r\n » n'ouvre qu'un paragraphe, même quand deux fragments se le
/// partagent.
/// </summary>
public readonly record struct LigneComposee(
    IReadOnlyList<Morceau> Morceaux, double Largeur, double Naturelle, bool FinDeParagraphe,
    TextAlign Fer,
    double Retrait, double Disponible, double EspaceAvant, double EspaceApres,
    int Colonne, double Haut, double Avance,
    int Debut, int Fin, int Paragraphe)
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
        /// <summary>
        /// Largeur d'un texte dans un style donné, en points, telle que CE rendu la
        /// dessine. C'est elle qui pose les morceaux et cale le fer de la ligne.
        /// </summary>
        double Largeur(string texte, StyleEffectif style);

        /// <summary>
        /// Largeur sur laquelle se DÉCIDE le renvoi à la ligne, en points. Elle doit
        /// être la même, au bit près, pour l'écran et pour l'export : c'est ce qui les
        /// fait couper aux mêmes mots (poste Q13).
        ///
        /// Les deux règles ne mesurent pas pareil. Avalonia crénèle (« PDF. » y est plus
        /// étroit d'un dixième de point), PDFsharp non ; et une police absente ne se
        /// remplace pas par la même chez l'un et chez l'autre — sur la machine du
        /// projet, l'italique de DejaVu Sans, qu'aucun fichier ne porte, sort en gras à
        /// l'écran et en romain au PDF, plus large de 12 % d'un côté. Chacun rend donc
        /// ici la plus large des deux mesures, la sienne et celle de l'autre : le
        /// maximum ne dépend pas de l'ordre de ses termes, et la ligne qui tient sous
        /// cette mesure tient sous les deux. Le prix : un mot qui tiendrait de justesse
        /// d'un côté passe à la ligne des deux.
        ///
        /// Pas de valeur par défaut qui rendrait <see cref="Largeur"/> : c'est
        /// exactement ce que faisait le dépôt avant Q13, et une règle qui l'oublierait
        /// recouperait à sa façon sans que rien ne le dise.
        /// </summary>
        double LargeurDeCoupure(string texte, StyleEffectif style);

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
        // Un texte vide ne compose aucune ligne : rien ne s'y dessine, rien n'y déborde.
        if (string.IsNullOrEmpty(texte)) return new List<LigneComposee>();

        return Composition(texte, fragments, commun, ecarts, communParagraphe,
            largeur, hauteur, colonnes, interligne, metriques);
    }

    /// <summary>
    /// La ligne qu'aurait un texte vide : celle d'un paragraphe vide, le premier du bloc,
    /// avec son retrait, son fer, son espace avant et la hauteur naturelle du style
    /// commun. <see cref="Composer"/> n'en rend aucune, et c'est voulu — un bloc vide ne
    /// déborde pas —, mais le curseur s'y pose (poste N3). Elle sort de la même
    /// composition que les autres lignes, et non d'un calcul à part qui pourrait s'en
    /// écarter.
    /// </summary>
    public static LigneComposee LigneDuTexteVide(
        StyleEffectif commun, IReadOnlyList<ParagraphSpan> ecarts, ParagrapheEffectif communParagraphe,
        double largeur, double hauteur, Colonnage colonnes, double interligne, IMetriques metriques) =>
        Composition(string.Empty, [], commun, ecarts, communParagraphe,
            largeur, hauteur, colonnes, interligne, metriques)[0];

    /// <summary>
    /// La composition elle-même, texte vide compris : elle rend alors la seule ligne d'un
    /// paragraphe vide.
    /// </summary>
    private static List<LigneComposee> Composition(
        string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun,
        IReadOnlyList<ParagraphSpan> ecarts, ParagrapheEffectif communParagraphe,
        double largeur, double hauteur, Colonnage colonnes, double interligne,
        IMetriques metriques)
    {
        var lignes = new List<LigneComposee>();

        // Le texte est composé une fois pour la largeur d'UNE colonne : toutes ont la
        // même, la découpe ne dépend donc pas de celle où la ligne atterrira. C'est ce
        // qui permet de ranger les lignes en colonnes dans une seconde passe, sans
        // toucher au renvoi à la ligne.
        var largeurColonne = colonnes.Largeur(largeur);

        // Un mot porte son style ; c'est l'unité que la découpe déplace d'une ligne
        // à l'autre. Un mot que deux fragments se partagent devient deux unités, que
        // le renvoi peut séparer comme deux mots.
        var mots = Decouper(texte, fragments, commun);

        // Le compte des paragraphes sort de la découpe elle-même et non d'un second
        // parcours du texte : c'est elle qui décide où ils commencent, et les deux ne
        // doivent pas pouvoir se contredire.
        var reglages = ParagrapheEffectif.Resoudre(
            mots.Count(u => u.FinDeLigne) + 1, ecarts, communParagraphe);

        var courante = new List<Morceau>();
        var largeurCourante = 0.0;
        // La même ligne mesurée à la règle de coupure : c'est sur elle, et sur elle
        // seule, que se décide le renvoi. Les deux cumuls avancent ensemble ; celui-ci
        // ne sert à rien d'autre, et ne se rogne pas en fin de ligne puisqu'il repart
        // de zéro à chaque ligne.
        var coupureCourante = 0.0;

        var paragraphe = 0;
        var premiereLigne = true;
        var retrait = 0.0;
        var disponible = largeurColonne;
        // Où commence, dans le texte, la ligne en cours (poste N3).
        var debutDeLigne = 0;
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

        // Ferme la ligne en cours sur la position <fin> du texte ; la suivante commence à
        // <suivante> — après le saut qui ferme un paragraphe, ou là même où un mot est
        // renvoyé.
        void Fermer(bool finDeParagraphe, int fin, int suivante)
        {
            // Les blancs de fin de ligne ne comptent ni dans la largeur ni à l'écran. Ils
            // restent dans la plage de la ligne : le curseur s'y pose.
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
                Colonne: 0, Haut: 0, Avance: 0,
                Debut: debutDeLigne, Fin: fin, Paragraphe: paragraphe));

            courante.Clear();
            largeurCourante = 0;
            coupureCourante = 0;
            debutDeLigne = suivante;

            // Fermer sur une fin de paragraphe ouvre le suivant ; fermer sur un renvoi
            // à la ligne reste dans le même paragraphe, mais n'est plus sa première.
            if (finDeParagraphe) paragraphe++;
            premiereLigne = finDeParagraphe;
            Cadrer();
        }

        foreach (var mot in mots)
        {
            // Le saut n'est dans aucune ligne : celle-ci finit avant lui, la suivante
            // commence après.
            if (mot.FinDeLigne)
            {
                Fermer(finDeParagraphe: true, fin: mot.Debut, suivante: mot.Debut + mot.Longueur);
                continue;
            }

            // Une tabulation n'a pas de largeur propre : elle vaut la distance qui
            // reste jusqu'au prochain taquet, et dépend donc d'où on en est sur la
            // ligne. On la calcule ici, où cette position est connue.
            //
            // Le taquet se calcule deux fois, une par cumul : le dessin le prend sur sa
            // propre largeur, la décision sur la sienne. Comme la mesure de coupure n'est
            // jamais plus étroite que celle du dessin, le taquet de la décision n'est
            // jamais en deçà, et une ligne qui tient pour elle tient pour le dessin.
            var tabulation = mot.Texte == "\t";
            var l = tabulation
                ? ProchainTaquet(largeurCourante) - largeurCourante
                : metriques.Largeur(mot.Texte, mot.Style);
            var c = tabulation
                ? ProchainTaquet(coupureCourante) - coupureCourante
                : metriques.LargeurDeCoupure(mot.Texte, mot.Style);

            // On ne renvoie à la ligne que si quelque chose y est déjà : un mot plus
            // large que la boîte garde sa ligne plutôt que d'être coupé au milieu.
            //
            // La décision ne lit que la règle de coupure, commune aux deux rendus. Lire
            // ici la largeur du dessin, c'est laisser l'écran et le PDF couper chacun à
            // sa mesure : c'était le défaut du poste Q13.
            if (!tabulation && courante.Count > 0 &&
                coupureCourante + metriques.LargeurDeCoupure(mot.Texte.TrimEnd(), mot.Style) > disponible)
            {
                var debut = mot.Texte.TrimStart();
                // Le renvoi rogne la tête blanche du mot — une insécable, des espaces
                // qu'un changement de style isole. Elle reste au bout de la ligne qu'il
                // quitte, comme les blancs de fin : le morceau commence après elle.
                var renvoi = mot.Debut + (mot.Texte.Length - debut.Length);
                Fermer(finDeParagraphe: false, fin: renvoi, suivante: renvoi);
                l = metriques.Largeur(debut, mot.Style);
                c = metriques.LargeurDeCoupure(debut, mot.Style);
                courante.Add(new Morceau(debut, mot.Style, largeurCourante, l, renvoi));
            }
            else
            {
                courante.Add(new Morceau(mot.Texte, mot.Style, largeurCourante, l, mot.Debut));
            }

            largeurCourante += l;
            coupureCourante += c;
        }

        Fermer(finDeParagraphe: true, fin: texte.Length, suivante: texte.Length);

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

    /// <summary>
    /// Une unité de la découpe : un mot avec les espaces qui le suivent, une tabulation, ou
    /// une fin de ligne ; sa place dans le texte d'origine, et sa longueur — celle du saut
    /// pour une fin de ligne, un ou deux caractères (poste N3).
    /// </summary>
    private readonly record struct Unite(string Texte, StyleEffectif Style, bool FinDeLigne, int Debut, int Longueur);

    /// <summary>
    /// Le texte réduit à une suite d'unités : des mots avec les espaces qui les
    /// suivent, des marques de fin de ligne, et jamais deux styles dans la même unité.
    ///
    /// Le texte se lit tel qu'il est, sans normaliser ses fins de ligne : les fragments y
    /// repèrent leurs positions, et chaque unité garde la sienne (poste N3). Un saut est un
    /// « \n », un « \r » seul, ou un « \r\n » pour un seul — la règle de
    /// <see cref="Recalage"/>, qui compte les paragraphes de la frappe. La découpe d'avant
    /// N3 normalisait chaque morceau de style à part : un « \r\n » que deux fragments se
    /// partageaient y faisait deux paragraphes, et l'écart du deuxième se posait sur une
    /// ligne vide. Aucun document du dépôt n'en porte — l'import retire les « \r ».
    /// </summary>
    private static List<Unite> Decouper(
        string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun)
    {
        var unites = new List<Unite>();

        // Les morceaux de style se suivent et couvrent le texte : chacun commence où
        // finit le précédent.
        var debutDuMorceau = 0;
        foreach (var (morceau, style) in StyleEffectif.Decouper(texte, fragments, commun))
        {
            var finDuMorceau = debutDuMorceau + morceau.Length;
            var segment = debutDuMorceau;

            for (var i = debutDuMorceau; i < finDuMorceau; i++)
            {
                if (texte[i] is not ('\n' or '\r')) continue;

                foreach (var (mot, debut) in Mots(texte, segment, i))
                {
                    unites.Add(new Unite(mot, style, FinDeLigne: false, debut, mot.Length));
                }

                segment = i + 1;

                // Le « \r » d'un « \r\n » ne ferme rien, même au bout d'un fragment : c'est
                // le « \n » qui ferme, et le saut couvre les deux.
                if (texte[i] == '\r' && i + 1 < texte.Length && texte[i + 1] == '\n') continue;
                var saut = texte[i] == '\n' && i > 0 && texte[i - 1] == '\r' ? i - 1 : i;
                unites.Add(new Unite(string.Empty, style, FinDeLigne: true, saut, i + 1 - saut));
            }

            foreach (var (mot, debut) in Mots(texte, segment, finDuMorceau))
            {
                unites.Add(new Unite(mot, style, FinDeLigne: false, debut, mot.Length));
            }

            debutDuMorceau = finDuMorceau;
        }

        return unites;
    }

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

    /// <summary>
    /// Les mots du segment [<paramref name="debut"/>, <paramref name="fin"/>) du texte,
    /// chacun avec les espaces qui le suivent et sa position : garder l'espace attaché
    /// évite de le réinventer au moment de recoller, et de perdre les espaces multiples
    /// que l'auteur a voulus.
    /// </summary>
    private static IEnumerable<(string Mot, int Debut)> Mots(string texte, int debut, int fin)
    {
        while (debut < fin)
        {
            // La tabulation se détache du mot qui la suit : sa largeur se calcule au
            // moment de la poser, pas au moment de la mesurer.
            if (texte[debut] == '\t') { yield return ("\t", debut); debut++; continue; }

            var blanc = texte.IndexOfAny([' ', '\t'], debut, fin - debut);
            if (blanc < 0) { yield return (texte[debut..fin], debut); yield break; }

            if (texte[blanc] == '\t') { yield return (texte[debut..blanc], debut); debut = blanc; continue; }

            var apres = blanc;
            while (apres < fin && texte[apres] == ' ') apres++;

            yield return (texte[debut..apres], debut);
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
    /// Les étendues soulignées d'une ligne posée : où chaque trait commence et finit,
    /// depuis l'origine de la ligne — l'abscisse où le rendu pose son premier morceau —,
    /// et le style qui le porte, dont le rendu tire la couleur, la police, donc la place
    /// et l'épaisseur du trait (<see cref="Souligne.Traits"/>). Poste I7.
    ///
    /// Le calcul est ici pour la raison qui y a mis <see cref="Decalages"/> : il est facile
    /// de le faire faussement, et deux rendus qui le feraient chacun se tromperaient
    /// ensemble, sans qu'une comparaison de l'écran au PDF le voie. Trois pièges :
    ///
    /// **Une ligne justifiée élargit ses blancs**, et un souligné continu passe dessous :
    /// un trait ne s'arrête pas au bout de son morceau mais au début du suivant, là où le
    /// blanc élargi finit. Sinon chaque mot d'une ligne justifiée aurait son trait, avec un
    /// trou de la largeur du blanc ajouté — et c'est exactement le dessin de « mots
    /// seuls », que rien ne distinguerait plus.
    ///
    /// **« Mots seuls » ne passe sous aucun blanc** : le trait couvre le mot sans les
    /// espaces qui le suivent, et ni un morceau blanc ni une tabulation n'en reçoivent.
    /// Une insécable est un blanc comme un autre (<see cref="char.IsWhiteSpace(char)"/>) :
    /// elle reste nue, qu'elle tombe au milieu d'un morceau ou au bout d'un fragment. Le
    /// mot se mesure à la règle du rendu, celle qui l'a posé.
    ///
    /// **Deux morceaux voisins du même style ne font qu'un trait.** Un trait par mot
    /// ferait repartir à chaque mot le motif d'un pointillé, et doublerait le lissage à
    /// chaque raccord. Un changement de style coupe le trait : la police — donc la place et
    /// l'épaisseur — ou la couleur changent avec lui.
    ///
    /// <paramref name="decalages"/> est ce que <see cref="Decalages"/> rend pour une ligne
    /// justifiée, et des zéros sinon : le rendu le passe tel qu'il l'a employé pour poser
    /// ses morceaux.
    /// </summary>
    public static IReadOnlyList<EtendueSoulignee> Soulignes(
        LigneComposee ligne, IReadOnlyList<double> decalages, IMetriques metriques)
    {
        var etendues = new List<EtendueSoulignee>();

        for (var m = 0; m < ligne.Morceaux.Count; m++)
        {
            var morceau = ligne.Morceaux[m];
            var souligne = morceau.Style.Underline;
            if (!souligne.Actif) continue;

            var debut = morceau.X + decalages[m];

            if (souligne.WordsOnly)
            {
                // Un morceau est un mot suivi de ses espaces (voir « Mots »), mais une
                // insécable n'y coupe pas : « Prix : » est un seul morceau. Un trait par
                // suite de caractères qui ne sont pas des blancs, donc : sans cela,
                // l'insécable serait soulignée au milieu d'un morceau et nue au bout d'un
                // fragment, selon une frontière que rien ne montre.
                var texte = morceau.Texte;
                double Bord(int k) => k == texte.Length ? morceau.Largeur
                    : k == 0 ? 0 : metriques.Largeur(texte[..k], morceau.Style);
                for (var i = 0; i < texte.Length;)
                {
                    if (char.IsWhiteSpace(texte[i])) { i++; continue; }
                    var j = i;
                    while (j < texte.Length && !char.IsWhiteSpace(texte[j])) j++;
                    etendues.Add(new EtendueSoulignee(debut + Bord(i), debut + Bord(j), morceau.Style));
                    i = j;
                }

                continue;
            }

            var fin = m + 1 < ligne.Morceaux.Count
                ? ligne.Morceaux[m + 1].X + decalages[m + 1]
                : debut + morceau.Largeur;

            if (etendues.Count > 0 && etendues[^1].Style == morceau.Style && etendues[^1].Fin == debut)
            {
                etendues[^1] = etendues[^1] with { Fin = fin };
            }
            else
            {
                etendues.Add(new EtendueSoulignee(debut, fin, morceau.Style));
            }
        }

        return etendues;
    }

    /// <summary>
    /// L'abscisse du point d'insertion placé avant le caractère <paramref name="position"/>
    /// du texte, sur une ligne posée : comptée depuis l'origine de la ligne — l'abscisse où
    /// le rendu pose son premier morceau, fer appliqué —, comme <see cref="Soulignes"/>
    /// (poste N3). C'est le curseur, le clic, la sélection : la vue y ajoute l'origine et la
    /// ligne de base qu'elle a posées.
    ///
    /// Trois pièges, chacun réglé ici une fois pour toutes plutôt que dans chaque appelant :
    ///
    /// **Une tabulation n'a pas de glyphe** : elle avance jusqu'au taquet. Le point qui la
    /// suit est au début du morceau suivant, là où la composition l'a posé, et non à la
    /// chasse d'un caractère de tabulation.
    ///
    /// **Une ligne justifiée élargit ses blancs** : le point qui suit un blanc élargi est au
    /// début du mot suivant, décalage compris. À l'intérieur d'un morceau, rien n'est élargi
    /// — un morceau est un mot suivi de ses espaces, et le blanc élargi est le dernier.
    ///
    /// **Les blancs rognés pendent** au bout de la ligne : ceux de sa fin, et la tête
    /// blanche d'un mot renvoyé. Ils ne comptent pas dans sa largeur et ne se dessinent pas,
    /// mais chacun a sa place après le dernier morceau, mesurée au style de celui-ci — qu'ils
    /// prolongent —, une tabulation pendante avançant au taquet. Une ligne sans morceau — un
    /// paragraphe vide, une ligne de blancs — n'a que des blancs pendants : ils se mesurent
    /// au style <paramref name="commun"/>, celui que la composition lui donne pour sa
    /// hauteur.
    ///
    /// Une position est ramenée dans la plage de la ligne ; une position au milieu d'une
    /// paire de substitution ne désigne aucun caractère et se ramène au début de la paire.
    /// <paramref name="decalages"/> est ce que <see cref="Decalages"/> rend pour une ligne
    /// justifiée, et des zéros sinon, tels que le rendu les a employés.
    /// </summary>
    public static double Abscisse(
        LigneComposee ligne, IReadOnlyList<double> decalages, int position,
        string texte, StyleEffectif commun, IMetriques metriques)
    {
        position = Math.Clamp(position, ligne.Debut, ligne.Fin);
        if (position > ligne.Debut && DansUnePaire(texte, position)) position--;

        var morceaux = ligne.Morceaux;
        for (var m = 0; m < morceaux.Count; m++)
        {
            var morceau = morceaux[m];
            var dedans = position - morceau.Debut;
            if (dedans >= morceau.Texte.Length) continue;

            // Le début d'un morceau est son abscisse, telle que le rendu la pose ; un point
            // plus loin dans le morceau, la chasse de ce qui le précède, à la règle du rendu.
            var avant = dedans <= 0 ? 0 : metriques.Largeur(morceau.Texte[..dedans], morceau.Style);
            return morceau.X + decalages[m] + avant;
        }

        if (morceaux.Count == 0)
        {
            return Pendants(texte, ligne.Debut, position, commun, depart: 0, metriques);
        }

        var dernier = morceaux[^1];
        var bout = dernier.X + dernier.Largeur;
        return decalages[^1] + bout
               + Pendants(texte, dernier.Debut + dernier.Texte.Length, position, dernier.Style, bout, metriques);
    }

    /// <summary>
    /// La position de la ligne la plus proche d'une abscisse comptée depuis l'origine de la
    /// ligne — l'inverse de <see cref="Abscisse"/>, qui croît d'une position à la suivante :
    /// une abscisse avant la ligne rend son début, après elle sa fin, et entre deux
    /// positions la plus proche, la première à égalité. Les positions sont celles de la
    /// plage de la ligne, bornes comprises — sa fin aussi pour une ligne renvoyée, où elle
    /// est le début de la suivante : un clic à droite d'une ligne pose le point au bout de
    /// ce qu'elle porte. Jamais le milieu d'une paire de substitution.
    /// </summary>
    public static int PositionLaPlusProche(
        LigneComposee ligne, IReadOnlyList<double> decalages, double abscisse,
        string texte, StyleEffectif commun, IMetriques metriques)
    {
        double X(int p) => Abscisse(ligne, decalages, p, texte, commun, metriques);

        var bas = ligne.Debut;
        var haut = ligne.Fin;
        if (abscisse <= X(bas)) return bas;
        if (abscisse >= X(haut)) return haut;

        // X(bas) < abscisse <= X(haut) : on resserre jusqu'à deux positions voisines.
        while (haut - bas > 1)
        {
            var milieu = bas + (haut - bas) / 2;
            if (X(milieu) < abscisse) bas = milieu;
            else haut = milieu;
        }

        // « haut » n'est jamais au milieu d'une paire : son abscisse est celle du début de
        // la paire, qui est sous l'abscisse cherchée. « bas » peut l'être : on prend le
        // début de la paire, à la même abscisse.
        if (bas > ligne.Debut && DansUnePaire(texte, bas)) bas--;
        return abscisse - X(bas) <= X(haut) - abscisse ? bas : haut;
    }

    /// <summary>
    /// La chasse des blancs pendants de <paramref name="depuis"/> à
    /// <paramref name="jusqua"/>, posés à partir de l'abscisse <paramref name="depart"/>
    /// de la ligne non justifiée — celle d'où la composition compte ses taquets.
    /// </summary>
    private static double Pendants(
        string texte, int depuis, int jusqua, StyleEffectif style, double depart, IMetriques metriques)
    {
        var x = depart;
        var suite = depuis;
        for (var i = depuis; i < jusqua; i++)
        {
            if (texte[i] != '\t') continue;
            if (suite < i) x += metriques.Largeur(texte[suite..i], style);
            x = ProchainTaquet(x);
            suite = i + 1;
        }

        if (suite < jusqua) x += metriques.Largeur(texte[suite..jusqua], style);
        return x - depart;
    }

    /// <summary>La position tombe-t-elle entre les deux moitiés d'une paire de substitution ?</summary>
    private static bool DansUnePaire(string texte, int position) =>
        position > 0 && position < texte.Length
                     && char.IsLowSurrogate(texte[position]) && char.IsHighSurrogate(texte[position - 1]);

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
    /// Combien de lignes composées le bloc ne montre pas : celles qui ne tiennent pas
    /// entières dans le BLOC. C'est le « Texte en excédent : 4 lignes » du dock (poste
    /// I9) ; <see cref="Debordement"/> dit de combien de points, ceci dit combien de
    /// lignes. Une fonction pure : l'écran l'appelle sur les lignes qu'il compose avec sa
    /// règle, et une composition faite à la règle de l'export donne le même compte, parce
    /// que les deux coupent aux mêmes mots (Q13) et posent les lignes aux mêmes
    /// ordonnées (R4). C'est le compte des lignes absentes du PDF.
    ///
    /// Une ligne est montrée quand ce qu'on en dessine reste dans le bloc : sa hauteur
    /// naturelle, que les deux rendus posent au milieu de son avance — l'interligne
    /// supplémentaire se partage de part et d'autre. Une ligne coupée par le bord n'est
    /// pas montrée : on n'en lit que le haut, et Publisher la mettrait en excédent tout
    /// entière. Pour le reste, les mesures de <see cref="Debordement"/>, et pas d'autres :
    ///
    /// le BLOC et non la zone de texte — ce qui ne dépasse que dans la marge intérieure
    /// basse reste entier et lisible (C6) ; le calage vertical de <see cref="Decalage"/>,
    /// qui ne descend jamais un texte trop haut (A7) ; le même seuil d'un demi-point, si
    /// bien qu'une ligne comptée fait toujours poser le marqueur rouge. Les colonnes (R3)
    /// n'ont rien à demander : chaque ligne porte son sommet dans sa colonne, et seule la
    /// dernière laisse couler ce qui ne tient pas — sauf une ligne plus haute qu'une
    /// colonne entière, qui n'y est montrée nulle part et compte. Un bloc tourné se
    /// compte dans son propre repère, où il est droit.
    ///
    /// L'inverse ne vaut pas toujours : <see cref="Debordement"/> mesure jusqu'au bas de
    /// l'avance de la dernière ligne, espace après le paragraphe compris, qui ne sont pas
    /// de l'encre. Un bloc dont seuls ce demi-interligne ou cet espace sortent porte le
    /// marqueur et zéro ligne en excédent ; zéro ne s'annonce pas.
    ///
    /// L'encre se compte dans l'avance de la ligne, jamais au-delà. À un interligne
    /// inférieur à 1 — la barre descend à 0,5 —, la hauteur naturelle déborde l'avance en
    /// haut et en bas, et ce débord est le chevauchement des lignes d'un titre serré, pas
    /// une ligne perdue : la première ligne n'est pas comptée pour ce qui sort au-dessus
    /// du bloc, la dernière ne l'est pas pour ses jambages sous le bord. Compté, il faisait
    /// annoncer une ligne en excédent sur un bloc que le marqueur laissait en paix
    /// (relecture du 26 octobre 2026).
    /// </summary>
    public static int LignesEnExcedent(
        IReadOnlyList<LigneComposee> lignes, double disponible, double margeBasse,
        VerticalAlign fer)
    {
        var descente = Decalage(lignes, disponible, fer);
        var bord = disponible + margeBasse + SeuilDebordement;
        return lignes.Count(l => descente + l.Haut + (l.Avance + Math.Min(l.Naturelle, l.Avance)) / 2 > bord);
    }

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
