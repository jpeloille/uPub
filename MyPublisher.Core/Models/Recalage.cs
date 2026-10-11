using System;
using System.Collections.Generic;
using System.Linq;

namespace MyPublisher.Models;

/// <summary>
/// Une modification du texte, réduite à un seul remplacement : à partir de
/// <paramref name="Debut"/>, <paramref name="Supprimes"/> caractères ôtés et
/// <paramref name="Inseres"/> caractères mis à leur place.
///
/// Pourquoi la déduire des deux textes plutôt que la recevoir de la zone de saisie :
/// parce qu'une liaison ne transmet que la valeur nouvelle. Une frappe, une
/// suppression, un collage, un « annuler » propre à la zone de saisie, tout arrive
/// sous la même forme — un texte qui en remplace un autre. Comparer les deux est le
/// seul moyen de les traiter tous, et de les traiter pareil.
/// </summary>
public readonly record struct Edition(int Debut, int Supprimes, int Inseres)
{
    /// <summary>
    /// Le plus petit remplacement qui mène de <paramref name="avant"/> à
    /// <paramref name="apres"/> : on retire le préfixe commun, puis le suffixe commun
    /// dans ce qui reste.
    ///
    /// La déduction a une ambiguïté, qu'il faut dire. Taper « d » devant « def » donne
    /// « ddef », et rien dans les deux textes ne distingue ce « d » tapé devant de celui
    /// qu'on aurait tapé derrière le premier. Le préfixe étant pris en premier et le plus
    /// long possible, l'insertion est toujours placée le plus à droite de ce qui est
    /// ambigu. Les caractères sont les mêmes ; seul peut différer le style que reçoit le
    /// caractère tapé, et seulement quand il répète son voisin à la frontière d'un
    /// fragment.
    /// </summary>
    public static Edition Deduire(string avant, string apres)
    {
        var borne = Math.Min(avant.Length, apres.Length);

        var prefixe = 0;
        while (prefixe < borne && avant[prefixe] == apres[prefixe]) prefixe++;

        // Une paire de substitution n'est pas coupée en deux : un emoji qui en remplace
        // un autre de la même famille partage souvent son premier demi-caractère, et
        // une position au milieu d'une paire ne désigne aucun caractère.
        // Reculer d'un cran ne coûte rien : le remplacement s'élargit d'un caractère
        // identique des deux côtés.
        if (prefixe > 0 && char.IsHighSurrogate(avant[prefixe - 1])) prefixe--;

        var suffixe = 0;
        while (suffixe < borne - prefixe &&
               avant[avant.Length - 1 - suffixe] == apres[apres.Length - 1 - suffixe])
        {
            suffixe++;
        }

        if (suffixe > 0 && char.IsLowSurrogate(avant[avant.Length - suffixe])) suffixe--;

        return new Edition(prefixe, avant.Length - prefixe - suffixe, apres.Length - prefixe - suffixe);
    }
}

/// <summary>
/// Une portion repérée par son début et sa longueur — en caractères pour un fragment
/// de style, en paragraphes pour un écart de paragraphe. C'est tout ce que le recalage
/// regarde : le style lui-même ne bouge pas, seule la portion qu'il recouvre.
/// </summary>
public readonly record struct Plage(int Start, int Length);

/// <summary>
/// Ce qu'une édition fait aux paragraphes : à partir du paragraphe
/// <paramref name="Paragraphe"/>, combien de sauts disparaissent et combien
/// apparaissent.
///
/// <paramref name="DepuisLeDebut"/> dit si la suppression part du tout début d'un
/// paragraphe. C'est la seule chose qui distingue « fusionner deux paragraphes » de
/// « supprimer des paragraphes entiers », et les deux ne gardent pas le même réglage
/// (voir <see cref="Recalage.Paragraphe"/>).
/// </summary>
public readonly record struct EditionDeParagraphes(
    int Paragraphe, int Supprimes, int Inseres, bool DepuisLeDebut);

/// <summary>
/// Recale les fragments de style et les écarts de paragraphe d'un bloc quand son texte
/// change.
///
/// Les fragments sont repérés par position de caractère et les écarts par indice de
/// paragraphe (voir <see cref="StyleSpan"/> et <see cref="ParagraphSpan"/>) : c'est ce
/// qui garde le texte seule source de ses caractères. Le prix de ce choix, c'est
/// qu'une position ne suit pas d'elle-même le texte qu'elle désigne. Jusqu'au poste Q1,
/// rien ne la faisait suivre : taper un mot en tête d'un bloc importé faisait glisser
/// tous ses gras d'autant, et un saut de ligne décalait tous ses retraits d'un
/// paragraphe — sans rien d'illisible, sans avertissement, et l'enregistrement fixait
/// le décalage.
///
/// Tout est pur ici, sauf <see cref="Appliquer"/>, qui porte le résultat sur les
/// collections du modèle : le calcul s'éprouve sans bloc, sans fenêtre et sans
/// fichier.
///
/// La règle, celle de Publisher et de Word : un caractère inséré prend le style du
/// caractère qui le PRÉCÈDE. Tapé à la fin d'un fragment, il l'allonge ; tapé à son
/// début, il le décale et reste au style du bloc — ou de ce qui précède. En tête de
/// texte, rien ne précède : le caractère prend le style du bloc. Un remplacement suit
/// la même règle que la frappe — supprimer, puis insérer — si bien que remplacer tout
/// le texte d'un bloc ne laisse aucun fragment derrière lui.
/// </summary>
public static class Recalage
{
    /// <summary>
    /// Le recalage complet, en valeurs : pour chaque fragment et chaque écart d'avant,
    /// sa plage d'après, ou <c>null</c> s'il ne recouvre plus rien. Les listes rendues
    /// sont parallèles à celles reçues.
    /// </summary>
    public static (IReadOnlyList<Plage?> Fragments, IReadOnlyList<Plage?> Ecarts) Recaler(
        string avant, string apres, IReadOnlyList<Plage> fragments, IReadOnlyList<Plage> ecarts)
    {
        var edition = Edition.Deduire(avant, apres);
        var paragraphes = Paragraphes(avant, apres, edition);
        var nombre = Sauts(apres, 0, apres.Length) + 1;
        var nombreAvant = Sauts(avant, 0, avant.Length) + 1;

        return (
            fragments.Select(f => Caractere(f, edition, avant.Length, apres.Length)).ToList(),
            ecarts.Select(e => Paragraphe(e, paragraphes, nombreAvant, nombre)).ToList());
    }

    /// <summary>
    /// Un fragment de style après l'édition, ou <c>null</c> s'il est vidé.
    ///
    /// La suppression d'abord : ce qui tombait dans la portion ôtée se replie sur son
    /// début, ce qui la suivait recule. Puis l'insertion, au point de l'édition : un
    /// fragment qui recouvre le caractère d'avant s'allonge de ce qu'on insère, un
    /// fragment qui commence au point ou après se décale, et les autres ne bougent pas.
    ///
    /// Un fragment qui débordait le texte avant l'édition est d'abord ramené dans ses
    /// bornes, comme la composition l'aurait fait en le dessinant (voir
    /// <see cref="StyleEffectif.Decouper"/>) : ce qui ne se voyait pas ne doit pas se
    /// mettre à se voir parce qu'on a tapé un caractère ailleurs.
    /// </summary>
    public static Plage? Caractere(Plage fragment, Edition edition, int longueurAvant, int longueurApres)
    {
        var debut = Math.Clamp(fragment.Start, 0, longueurAvant);
        // En long : « Start = 2 147 483 647, Length = 1 » ferait déborder l'addition.
        var fin = (int)Math.Clamp((long)fragment.Start + fragment.Length, debut, longueurAvant);

        var point = edition.Debut;
        int Replier(int x) =>
            x <= point ? x : x >= point + edition.Supprimes ? x - edition.Supprimes : point;

        debut = Replier(debut);
        fin = Replier(fin);

        if (debut < point && point <= fin)
        {
            // Le caractère qui précède l'insertion est dans le fragment : il l'allonge.
            fin += edition.Inseres;
        }
        else if (debut >= point)
        {
            // Le fragment commence au point ou plus loin : l'insertion le pousse.
            (debut, fin) = (debut + edition.Inseres, fin + edition.Inseres);
        }

        debut = Math.Clamp(debut, 0, longueurApres);
        fin = Math.Clamp(fin, debut, longueurApres);
        return fin > debut ? new Plage(debut, fin - debut) : null;
    }

    /// <summary>
    /// Ce que l'édition fait aux paragraphes : où elle tombe, et combien de sauts de
    /// ligne elle ôte et ajoute.
    ///
    /// Une insertion est d'abord poussée le plus à GAUCHE possible, là où
    /// <see cref="Edition.Deduire"/> l'a posée le plus à droite. Les deux places donnent
    /// le même texte ; elles ne donnent pas le même paragraphe. Le cas qui décide est le
    /// plus courant de tous : un retour à la ligne tapé au bout d'un paragraphe.
    /// « Un\nDeux » devient « Un\n\nDeux », et la déduction lit un saut tapé en tête de
    /// « Deux » — le paragraphe vide prendrait alors le réglage de « Deux ». Poussé à
    /// gauche, le saut est tapé au bout de « Un », et le paragraphe neuf continue
    /// celui qu'on vient d'écrire : c'est ainsi qu'une liste à puces se prolonge d'un
    /// élément. Le geste inverse — un retour tapé en tête d'un paragraphe — donne alors
    /// au paragraphe vide le réglage de celui d'au-dessus ; aucun paragraphe qui porte
    /// du texte ne change de réglage, ni dans un cas ni dans l'autre.
    ///
    /// Une suppression n'a pas besoin de ce détour : la règle de fusion de
    /// <see cref="Paragraphe"/> donne le même résultat aux deux lectures d'un saut
    /// effacé parmi plusieurs.
    /// </summary>
    public static EditionDeParagraphes Paragraphes(string avant, string apres, Edition edition)
    {
        var point = edition.Debut;
        if (edition.Supprimes == 0 && edition.Inseres > 0)
        {
            while (point > 0 && apres[point - 1] == apres[point + edition.Inseres - 1]) point--;
        }

        return new EditionDeParagraphes(
            Paragraphe: Sauts(avant, 0, point),
            Supprimes: Sauts(avant, point, point + edition.Supprimes),
            Inseres: Sauts(apres, point, point + edition.Inseres),
            DepuisLeDebut: point == 0 || EstSaut(avant, point - 1));
    }

    /// <summary>
    /// Un écart de paragraphe après l'édition, ou <c>null</c> s'il ne recouvre plus
    /// aucun paragraphe.
    ///
    /// Supprimer un saut de ligne fusionne deux paragraphes, et le paragraphe fusionné
    /// garde le réglage du PREMIER des deux : c'est ce que fait la touche d'effacement
    /// en tête d'une ligne, dans Publisher comme dans Word. Sauf quand le premier
    /// n'apporte rien à la fusion — la suppression part de son tout début : ce sont
    /// alors des paragraphes ENTIERS qu'on supprime, et celui qui reste garde le sien.
    /// Sans cette exception, supprimer une ligne sélectionnée de bout en bout, saut
    /// compris, donnerait au paragraphe suivant le retrait de la ligne effacée : le
    /// défaut même que ce poste répare, déplacé d'un cran.
    ///
    /// Insérer un saut de ligne coupe un paragraphe en deux, et les deux moitiés
    /// gardent son réglage : l'écart qui le recouvrait s'allonge, ceux qui le suivent
    /// se décalent. C'est ce qui fait qu'une liste à puces se continue d'un retour à la
    /// ligne.
    /// </summary>
    public static Plage? Paragraphe(Plage ecart, EditionDeParagraphes edition, int nombreAvant, int nombreApres)
    {
        var debut = Math.Clamp(ecart.Start, 0, nombreAvant);
        var fin = (int)Math.Clamp((long)ecart.Start + ecart.Length, debut, nombreAvant);

        var p = edition.Paragraphe;
        var supprimes = edition.Supprimes;

        // Une borne est une frontière entre deux paragraphes : la borne x passe juste
        // avant le paragraphe x. Celles qui tombent entre les paragraphes fusionnés se
        // replient sur la frontière qui reste — avant le paragraphe fusionné s'il garde
        // le réglage du dernier, après s'il garde celui du premier.
        int Replier(int x) =>
            x <= p ? x
            : x > p + supprimes ? x - supprimes
            : edition.DepuisLeDebut ? p : p + 1;

        debut = Replier(debut);
        fin = Replier(fin);

        if (debut <= p && p < fin)
        {
            fin += edition.Inseres;
        }
        else if (debut > p)
        {
            debut += edition.Inseres;
            fin += edition.Inseres;
        }

        debut = Math.Clamp(debut, 0, nombreApres);
        fin = Math.Clamp(fin, debut, nombreApres);
        return fin > debut ? new Plage(debut, fin - debut) : null;
    }

    /// <summary>
    /// Porte le recalage sur les collections d'un bloc ou d'une case : les plages
    /// changent sur place, les fragments vidés sont retirés. Les objets sont gardés et
    /// non remplacés — leur style ne bouge pas, et quiconque les observe continue de
    /// les observer.
    ///
    /// À appeler AVANT de changer le texte : les deux doivent passer ensemble d'un état
    /// cohérent à un autre, et c'est le changement du texte qui fait redessiner.
    /// </summary>
    public static void Appliquer(
        string avant, string apres, IList<StyleSpan> fragments, IList<ParagraphSpan>? ecarts)
    {
        if (avant == apres) return;

        var (nouveauxFragments, nouveauxEcarts) = Recaler(
            avant, apres,
            fragments.Select(f => new Plage(f.Start, f.Length)).ToList(),
            ecarts?.Select(e => new Plage(e.Start, e.Length)).ToList() ?? []);

        for (var i = fragments.Count - 1; i >= 0; i--)
        {
            if (nouveauxFragments[i] is { } plage)
            {
                fragments[i].Start = plage.Start;
                fragments[i].Length = plage.Length;
            }
            else
            {
                fragments.RemoveAt(i);
            }
        }

        if (ecarts is null) return;

        for (var i = ecarts.Count - 1; i >= 0; i--)
        {
            if (nouveauxEcarts[i] is { } plage)
            {
                ecarts[i].Start = plage.Start;
                ecarts[i].Length = plage.Length;
            }
            else
            {
                ecarts.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Les sauts de paragraphe entre <paramref name="debut"/> et <paramref name="fin"/>,
    /// comptés comme la composition les compte : « \n », « \r » seul, et « \r\n » pour
    /// un seul (voir <see cref="Compositeur"/>). Deux façons de compter les paragraphes
    /// donneraient deux indices pour le même, et l'écart se poserait à côté.
    /// </summary>
    private static int Sauts(string texte, int debut, int fin)
    {
        var nombre = 0;
        for (var i = Math.Max(0, debut); i < Math.Min(fin, texte.Length); i++)
        {
            if (EstSaut(texte, i)) nombre++;
        }

        return nombre;
    }

    /// <summary>
    /// Le caractère <paramref name="i"/> clôt-il un paragraphe ? Un « \r » suivi d'un
    /// « \n » ne le clôt pas : c'est le « \n » qui le fera.
    /// </summary>
    private static bool EstSaut(string texte, int i) =>
        texte[i] == '\n' || (texte[i] == '\r' && (i + 1 >= texte.Length || texte[i + 1] != '\n'));
}
