using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Affiche le texte d'un bloc en passant par la composition du noyau.
///
/// Pourquoi remplacer <c>TextBlock</c>, qui faisait très bien l'affaire. Pour deux
/// raisons qui n'en font qu'une.
///
/// D'abord parce qu'un <c>TextBlock</c> ne change pas de police au milieu d'un
/// paragraphe, et que les documents réels le demandent : jusqu'à sept fragments de
/// style dans un seul paragraphe des fiches ATR.
///
/// Ensuite, et surtout, parce que tant que l'écran découpait ses lignes avec Avalonia
/// et l'export avec le noyau, rien ne garantissait qu'ils coupent aux mêmes endroits.
/// Ils s'accordaient sur les documents essayés ; c'était une observation, pas une
/// propriété. Ici les deux appellent <see cref="Compositeur.Composer"/> — la même
/// fonction, les mêmes lignes, par construction. C'est ce que le projet promet depuis
/// le début : « l'écran et l'export partagent exactement les mêmes données, sans
/// couche intermédiaire susceptible de diverger ».
///
/// La saisie, elle, reste au <c>TextBox</c> posé par-dessus : ce contrôle-ci ne fait
/// qu'afficher. Le bloc, le contenu et l'abonnement viennent de
/// <see cref="ControleDeBloc"/>.
/// </summary>
public sealed class TexteCompose : ControleDeBloc
{
    /// <summary>
    /// Un morceau posé : son texte, le texte formaté qui le dessine, son point d'ancrage (coin haut-gauche du
    /// texte formaté, comme <c>DrawText</c> le veut), le rang de sa ligne, et le cadre
    /// de cette ligne — son sommet, son avance, son bord gauche et la largeur dont elle
    /// dispose —, tous dans le repère du contrôle.
    /// </summary>
    public readonly record struct MorceauPose(
        string Texte, FormattedText Rendu, Point Ancre, int Ligne,
        double HautDeLigne, double Avance, double Gauche, double Disponible);

    /// <summary>
    /// Un trait de souligné posé (poste I7) : de <paramref name="Debut"/> à
    /// <paramref name="Fin"/> sur son axe <paramref name="Axe"/>, d'un stylo
    /// <paramref name="Epaisseur"/>, dans le repère du contrôle ; avec la sorte qui en
    /// donne le motif ou la vague, et la couleur du texte qu'il souligne.
    /// </summary>
    public readonly record struct TraitPose(
        double Debut, double Fin, double Axe, double Epaisseur, Souligne Sorte, string Couleur, int Ligne);

    /// <summary>
    /// Une ligne posée (poste N3), telle que <see cref="Disposer"/> la pose pour le dessin,
    /// lignes vides comprises — un paragraphe vide, une ligne de blancs, le texte vide :
    /// rien ne s'y dessine, mais le curseur s'y pose. <paramref name="Ligne"/> est la ligne
    /// composée, avec sa plage dans le texte ; <paramref name="Rang"/> son rang dans le
    /// bloc ; <paramref name="Origine"/> l'abscisse où se pose son premier morceau, retrait,
    /// colonne et fer compris ; <paramref name="Haut"/> son sommet et
    /// <paramref name="LigneDeBase"/> sa ligne de base, calage vertical compris ;
    /// <paramref name="Decalages"/> ce que la justification ajoute à chaque morceau, des
    /// zéros si elle n'est pas justifiée. Tout est dans le repère du contrôle, qui est celui
    /// du bloc non tourné.
    /// </summary>
    public readonly record struct LignePosee(
        LigneComposee Ligne, int Rang, double Origine, double Haut, double LigneDeBase,
        IReadOnlyList<double> Decalages);

    public override void Render(DrawingContext contexte)
    {
        var metriques = new MetriquesAvalonia();
        var (poses, traits, _, _) = Disposer(metriques, metriques);
        foreach (var morceau in poses)
        {
            contexte.DrawText(morceau.Rendu, morceau.Ancre);
        }

        // Le souligné après le texte, comme à l'export : il passe sur les jambages, comme
        // dans Publisher, qui ne l'interrompt pas sous un « p ».
        foreach (var trait in traits) Tracer(contexte, trait);
    }

    /// <summary>
    /// Trace un trait de souligné : droit, pointillé ou en vague, d'un stylo à bouts
    /// plats et à jointures rondes, le même qu'à l'export. Le motif et les points de la
    /// vague viennent du noyau (<see cref="Souligne.Motif"/>, <see cref="Souligne.Vague"/>),
    /// et non d'ici : l'écran et le PDF tracent ainsi la même chose.
    /// </summary>
    private static void Tracer(DrawingContext contexte, TraitPose trait)
    {
        var pinceau = Converters.Conv.ToBrush(trait.Couleur) ?? Brushes.Black;
        var motif = trait.Sorte.Motif;
        var stylo = new Pen(pinceau, trait.Epaisseur,
            motif is null ? null : new DashStyle(motif, 0), PenLineCap.Flat, PenLineJoin.Round);

        if (!trait.Sorte.Ondule)
        {
            contexte.DrawLine(stylo, new Point(trait.Debut, trait.Axe), new Point(trait.Fin, trait.Axe));
            return;
        }

        var points = Souligne.Vague(trait.Debut, trait.Fin, trait.Axe, trait.Epaisseur);
        if (points.Count < 2) return;

        var vague = new StreamGeometry();
        using (var trace = vague.Open())
        {
            trace.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: false);
            for (var i = 1; i < points.Count; i++) trace.LineTo(new Point(points[i].X, points[i].Y));
            trace.EndFigure(isClosed: false);
        }

        contexte.DrawGeometry(null, stylo, vague);
    }

    /// <summary>
    /// Ce que <see cref="Render"/> dessine, morceau par morceau. Le banc le lit pour
    /// confronter l'écran au PDF mot à mot (<c>Scripts/comparer-mots.sh</c>) : il lit
    /// donc exactement ce qui se dessine, et non une composition refaite à côté.
    ///
    /// <paramref name="composition"/> est la règle avec laquelle on compose ; elle est
    /// <paramref name="metriques"/> au dessin. Le banc peut en passer une autre — une
    /// règle qui coupe à sa seule mesure — pour la contre-épreuve de la recette Q13.
    /// </summary>
    public IReadOnlyList<MorceauPose> Poser(MetriquesAvalonia metriques, Compositeur.IMetriques composition) =>
        Disposer(metriques, composition).Poses;

    /// <summary>
    /// Les traits de souligné que <see cref="Render"/> trace, dans le repère du
    /// contrôle : ce que les tests mesurent, comme le banc lit <see cref="Poser"/>.
    /// </summary>
    public IReadOnlyList<TraitPose> Souligner(MetriquesAvalonia metriques) =>
        Disposer(metriques, metriques).Traits;

    /// <summary>
    /// La géométrie du texte tel qu'il est à l'écran (poste N3) : les lignes que
    /// <see cref="Render"/> pose, et de quoi passer d'une position du texte à un point du
    /// bloc et retour. Elle est prise de la passe même du dessin, et non d'un calcul à côté.
    ///
    /// Elle compose le bloc à chaque appel : qui demande beaucoup de points la garde, au lieu
    /// d'appeler <see cref="PointDe"/> en boucle. Rien pour un bloc sans bloc, ni pour un
    /// bloc à champ — son <see cref="ControleDeBloc.Contenu"/>, résolu, n'est pas son texte,
    /// et une position dans l'un ne désigne rien dans l'autre.
    /// </summary>
    public GeometrieDuTexte? Geometrie()
    {
        if (Bloc is not { } bloc || bloc.HasField || (Contenu ?? string.Empty) != (bloc.Text ?? string.Empty))
        {
            return null;
        }

        var metriques = new MetriquesAvalonia();
        var disposition = Disposer(metriques, metriques, texteVideCompris: true);
        if (disposition.Mesure is not { } mesure) return null;

        return new GeometrieDuTexte(
            disposition.Lignes, mesure.Commun, Contenu ?? string.Empty,
            bloc.PaddingLeft, mesure.Largeur, bloc.Colonnage, metriques);
    }

    /// <summary>
    /// Le point d'insertion placé avant le caractère <paramref name="index"/>, sur la ligne
    /// de base de sa ligne, dans le repère du bloc non tourné (poste N3) — voir
    /// <see cref="GeometrieDuTexte.PointDe"/>. Rien pour un bloc à champ.
    /// </summary>
    public Point? PointDe(int index) => Geometrie()?.PointDe(index);

    /// <summary>
    /// La position du texte la plus proche d'un point du repère du bloc non tourné — un
    /// point de la page y entre par <see cref="Frame.ToLocal"/> (poste N3) ; voir
    /// <see cref="GeometrieDuTexte.IndexSous"/>. Rien pour un bloc à champ.
    /// </summary>
    public int? IndexSous(Point point) => Geometrie()?.IndexSous(point);

    /// <summary>
    /// Les morceaux et les traits de souligné, d'une seule composition : le souligné se
    /// pose sur les lignes mêmes que le texte, et composer deux fois coûterait deux fois
    /// à chaque redessin pour le risque de ne pas tomber pareil. Les lignes posées en
    /// sortent aussi, lignes vides comprises (poste N3) : la géométrie lit ce que le dessin
    /// a posé. <paramref name="texteVideCompris"/> donne au texte vide la ligne qu'il
    /// aurait, où le curseur se pose ; le dessin n'en a que faire.
    /// </summary>
    private (List<MorceauPose> Poses, List<TraitPose> Traits, List<LignePosee> Lignes, TexteMesure? Mesure) Disposer(
        MetriquesAvalonia metriques, Compositeur.IMetriques composition, bool texteVideCompris = false)
    {
        var poses = new List<MorceauPose>();
        var traits = new List<TraitPose>();
        var posees = new List<LignePosee>();
        if (Bloc is not { } bloc) return (poses, traits, posees, null);
        if (Mesurer(composition, texteVideCompris) is not { } mesure) return (poses, traits, posees, null);

        // Le calage vertical vient du noyau, comme la découpe en lignes : l'export lit
        // la même fonction. Il est borné à zéro là-bas comme ici, si bien qu'un texte
        // trop haut se cale en haut des deux côtés.
        var descente = Compositeur.Decalage(mesure.Lignes, mesure.Hauteur, bloc.VerticalAlign);

        for (var rang = 0; rang < mesure.Lignes.Count; rang++)
        {
            var ligne = mesure.Lignes[rang];

            // Rien ne se cumule ici : la ligne porte son ordonnée dans sa colonne, et
            // l'espace avant le paragraphe y est déjà entré. C'est la composition qui a
            // décidé où passer d'une colonne à l'autre, et il ne faut pas deux endroits
            // pour en décider.
            //
            // Une ligne sans morceau ne dessine rien, mais elle se pose : le curseur s'y
            // met (poste N3).

            // Le fer vient de la LIGNE et non du bloc : il appartient au paragraphe.
            var justifiee = ligne.Fer == TextAlign.Justify && !ligne.FinDeParagraphe;
            // Le retrait déplace le bord gauche de la ligne, gouttières comprises ;
            // « Disponible » est ce qui reste entre les deux retraits, dans sa
            // colonne. C'est cette largeur-là, et non celle du bloc, qui cale un
            // centrage ou un fer à droite.
            var gauche = bloc.PaddingLeft + ligne.Retrait;
            var origine = justifiee ? gauche : ligne.Fer switch
            {
                TextAlign.Center => gauche + (ligne.Disponible - ligne.Largeur) / 2,
                TextAlign.Right => gauche + ligne.Disponible - ligne.Largeur,
                _ => gauche,
            };

            // Le blanc se répartit entre les mots, pas entre les morceaux :
            // même calcul que l'export, et il vient du noyau.
            var decalages = justifiee
                ? Compositeur.Decalages(ligne)
                : new double[ligne.Morceaux.Count];

            // Tous les morceaux d'une ligne se calent sur la même ligne de base,
            // celle du plus grand style : c'est ce qui empêche un mot en gros
            // corps de flotter au-dessus de ses voisins. Une ligne sans morceau a la
            // hauteur naturelle du style commun : elle en prend la montée.
            var montee = ligne.Morceaux.Count == 0 ? metriques.Montee(mesure.Commun) : 0.0;
            foreach (var morceau in ligne.Morceaux)
            {
                montee = Math.Max(montee, metriques.Montee(morceau.Style));
            }

            var haut = bloc.PaddingTop + descente + ligne.Haut;
            var ligneDeBase = haut + (ligne.Avance - ligne.Naturelle) / 2 + montee;
            posees.Add(new LignePosee(ligne, rang, origine, haut, ligneDeBase, decalages));

            for (var m = 0; m < ligne.Morceaux.Count; m++)
            {
                var morceau = ligne.Morceaux[m];
                if (morceau.Texte.Trim().Length == 0) continue;

                var rendu = metriques.Composer(morceau.Texte, morceau.Style);
                // FormattedText se pose par son coin haut-gauche ; on remonte donc
                // de sa propre montée pour caler la ligne de base.
                var y = ligneDeBase
                        - morceau.Style.BaselineShift * morceau.Style.FontSize
                        - rendu.Baseline;
                poses.Add(new MorceauPose(
                    morceau.Texte, rendu, new Point(origine + morceau.X + decalages[m], y), rang,
                    haut, ligne.Avance, gauche, ligne.Disponible));
            }

            // Le souligné se pose sous la ligne de base de chaque morceau — décalée pour un
            // exposant, comme son texte —, à la place et à l'épaisseur que la police donne.
            // Les étendues viennent du noyau, que l'export appelle de même.
            foreach (var etendue in Compositeur.Soulignes(ligne, decalages, metriques))
            {
                var style = etendue.Style;
                var (position, epaisseur) = metriques.Souligne(style);
                var baseDuMorceau = ligneDeBase - style.BaselineShift * style.FontSize;
                foreach (var trace in style.Underline.Traits(position, epaisseur))
                {
                    traits.Add(new TraitPose(
                        origine + etendue.Debut, origine + etendue.Fin, baseDuMorceau + trace.Axe, trace.Epaisseur,
                        style.Underline, style.Foreground, rang));
                }
            }
        }

        return (poses, traits, posees, mesure);
    }
}

/// <summary>
/// La géométrie d'un texte posé (poste N3) : ses lignes posées, et le passage d'une position
/// du texte à un point du bloc, et retour — le curseur, le clic, la sélection. Tout est dans
/// le repère du bloc non tourné, colonnes, retraits, fer, justification et calage vertical
/// compris ; l'abscisse dans la ligne vient du noyau (<see cref="Compositeur.Abscisse"/>,
/// <see cref="Compositeur.PositionLaPlusProche"/>), mesurée à la règle même du dessin.
/// </summary>
public sealed class GeometrieDuTexte
{
    private readonly StyleEffectif _commun;
    private readonly string _texte;
    private readonly Compositeur.IMetriques _metriques;
    private readonly double _gauche;
    private readonly double _largeurDeColonne;
    private readonly double _pas;

    /// <summary>L'étendue de chaque colonne qui porte du texte, calculée au premier clic.</summary>
    private List<(int Colonne, double Gauche, double Droite)>? _colonnes;

    internal GeometrieDuTexte(
        IReadOnlyList<TexteCompose.LignePosee> lignes, StyleEffectif commun, string texte,
        double paddingLeft, double largeur, Colonnage colonnes, Compositeur.IMetriques metriques)
    {
        Lignes = lignes;
        _commun = commun;
        _texte = texte;
        _metriques = metriques;
        _gauche = paddingLeft;
        _largeurDeColonne = colonnes.Largeur(largeur);
        _pas = colonnes.Pas(largeur);
    }

    /// <summary>Les lignes posées, dans l'ordre du texte ; jamais aucune : un texte vide en a une.</summary>
    public IReadOnlyList<TexteCompose.LignePosee> Lignes { get; }

    /// <summary>
    /// La ligne qui porte la position <paramref name="index"/>, ramenée dans le texte : la
    /// dernière qui commence avant elle ou sur elle. Une position où une ligne est renvoyée
    /// est le début de la suivante ; une position dans un « \r\n » appartient à la ligne
    /// qu'il ferme.
    /// </summary>
    public TexteCompose.LignePosee LigneDe(int index)
    {
        index = Math.Clamp(index, 0, _texte.Length);
        var trouvee = Lignes[0];
        foreach (var ligne in Lignes)
        {
            if (ligne.Ligne.Debut > index) break;
            trouvee = ligne;
        }

        return trouvee;
    }

    /// <summary>
    /// Le point d'insertion placé avant le caractère <paramref name="index"/> : son
    /// abscisse, et la ligne de base de sa ligne (<see cref="LigneDe"/>). Une position hors
    /// du texte s'y ramène ; une position au milieu d'une paire de substitution, au début de
    /// la paire.
    /// </summary>
    public Point PointDe(int index)
    {
        var posee = LigneDe(index);
        var x = Compositeur.Abscisse(
            posee.Ligne, posee.Decalages, Math.Clamp(index, 0, _texte.Length), _texte, _commun, _metriques);
        return new Point(posee.Origine + x, posee.LigneDeBase);
    }

    /// <summary>
    /// La position du texte la plus proche de <paramref name="point"/> : la colonne la plus
    /// proche parmi celles qui portent du texte, puis, dans cette colonne, la ligne la plus
    /// proche, puis, sur cette ligne, la position la plus proche de l'abscisse
    /// (<see cref="Compositeur.PositionLaPlusProche"/>). Un point à égale distance de deux
    /// colonnes ou de deux lignes va à la première. La bande d'une ligne va de son sommet au
    /// bas de son avance, et contient sa ligne de base même quand un interligne serré l'en
    /// fait sortir ; l'étendue d'une colonne contient ce que ses lignes dépassent, de leur
    /// fer ou de leurs blancs pendants. Si bien que <c>IndexSous(PointDe(i)) == i</c> pour
    /// toute position qui désigne un caractère.
    /// </summary>
    public int IndexSous(Point point)
    {
        // La colonne : la plus proche du point en abscisse.
        var colonne = -1;
        var meilleure = double.PositiveInfinity;
        foreach (var (rang, gauche, droite) in _colonnes ??= Colonnes())
        {
            var distance = Distance(point.X, gauche, droite);
            if (distance < meilleure)
            {
                meilleure = distance;
                colonne = rang;
            }
        }

        // La ligne : la plus proche du point en ordonnée, dans cette colonne.
        var choisie = Lignes[0];
        meilleure = double.PositiveInfinity;
        foreach (var posee in Lignes)
        {
            if (posee.Ligne.Colonne != colonne) continue;
            var haut = Math.Min(posee.Haut, posee.LigneDeBase);
            var bas = Math.Max(posee.Haut + posee.Ligne.Avance, posee.LigneDeBase);
            var distance = Distance(point.Y, haut, bas);
            if (distance < meilleure)
            {
                meilleure = distance;
                choisie = posee;
            }
        }

        return Compositeur.PositionLaPlusProche(
            choisie.Ligne, choisie.Decalages, point.X - choisie.Origine, _texte, _commun, _metriques);
    }

    /// <summary>
    /// L'étendue de chaque colonne qui porte du texte : sa place dans le bloc, et ce que ses
    /// lignes en dépassent.
    /// </summary>
    private List<(int Colonne, double Gauche, double Droite)> Colonnes()
    {
        var colonnes = new List<(int, double, double)>();
        foreach (var groupe in Lignes.GroupBy(l => l.Ligne.Colonne))
        {
            var debut = _gauche + groupe.Key * _pas;
            var fin = debut + _largeurDeColonne;
            foreach (var posee in groupe)
            {
                var (gauche, droite) = Etendue(posee);
                debut = Math.Min(debut, gauche);
                fin = Math.Max(fin, droite);
            }

            colonnes.Add((groupe.Key, debut, fin));
        }

        return colonnes;
    }

    /// <summary>
    /// De quoi la ligne s'étend en abscisse : sa place entre ses retraits, et ce qu'elle en
    /// dépasse — un fer qui la décale à gauche, des blancs pendants à droite.
    /// </summary>
    private (double Gauche, double Droite) Etendue(TexteCompose.LignePosee posee)
    {
        var bord = _gauche + posee.Ligne.Retrait;
        var bout = posee.Origine + Compositeur.Abscisse(
            posee.Ligne, posee.Decalages, posee.Ligne.Fin, _texte, _commun, _metriques);
        return (Math.Min(bord, posee.Origine), Math.Max(bord + posee.Ligne.Disponible, bout));
    }

    private static double Distance(double valeur, double debut, double fin) =>
        valeur < debut ? debut - valeur : valeur > fin ? valeur - fin : 0;
}
