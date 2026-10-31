using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;
using Xunit.Abstractions;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Le clavier suit les lignes composées — le poste N29 (fiche 23, a21), décidé par le propriétaire
/// le 31 octobre 2026 sur la mesure de N21.
///
/// Depuis la voie 2a, la saisie montre le texte composé, mais le champ caché gardait la navigation
/// au clavier sur ses propres lignes, coupées à sa mesure : sur ATA 21, depuis le milieu de 33
/// lignes composées, <c>↓</c> tombait ailleurs que là où la composition l'attend 21 fois, <c>↑</c>
/// 23 fois, <c>Début</c> et <c>Fin</c> 4 fois chacune. Désormais la planche calcule ces touches
/// sur les lignes que la composition pose, garde l'abscisse visée d'une flèche à l'autre et
/// l'affinité du curseur au bout d'une ligne renvoyée, et pose la fenêtre de candidats de la
/// méthode d'entrée au curseur dessiné.
///
/// Les gestes sont ceux du banc (<see cref="Rendu"/>, poste N1). Ce qu'on attend est lu sur ce que
/// la composition dessine — ses morceaux posés, le bord de chacun de leurs caractères dans le
/// texte façonné — et sur ses lignes (N3), et non refait par le calcul de la planche : un test qui
/// referait ce calcul ne prouverait rien d'elle.
/// </summary>
public class ClavierSurLaCompositionTests(ITestOutputHelper sortie)
{
    // ------------------------------------------------------------------ le banc du poste

    private static TextFrame Texte(string texte, double largeur = 300, double hauteur = 160, double rotation = 0) => new()
    {
        Name = "Texte", X = 60, Y = 60, Width = largeur, Height = hauteur, Text = texte, Rotation = rotation,
        FontFamily = "DejaVu Sans", FontSize = 14, Foreground = "#FF000000",
    };

    private static TexteCompose Compose(Window fenetre, TextFrame bloc) =>
        fenetre.GetVisualDescendants().OfType<TexteCompose>().Single(c => ReferenceEquals(c.Bloc, bloc));

    private static GeometrieDuTexte Geometrie(Window fenetre, TextFrame bloc) =>
        Compose(fenetre, bloc).Geometrie() ?? throw new InvalidOperationException($"« {bloc.Name} » n'a pas de géométrie");

    private static MarquesDuTexte Marques(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<MarquesDuTexte>().Single(m => m.Name == "MarquesDeLaSaisie");

    private static (int, int) Selection(TextBox zone) => (zone.SelectionStart, zone.SelectionEnd);

    /// <summary>Le curseur dessiné, dans le repère du bloc : il doit y en avoir un.</summary>
    private static Rect Curseur(Window fenetre, string quoi) =>
        Marques(fenetre).CurseurPose ?? throw new InvalidOperationException($"{quoi} : aucun curseur dessiné");

    /// <summary>Un caractère tel que la composition le dessine : sa position, les bords de sa grappe, sa ligne.</summary>
    private readonly record struct Dessine(int Indice, double Gauche, double Droite, TexteCompose.LignePosee Ligne);

    /// <summary>
    /// Chaque caractère dessiné qui commence une grappe, lu sur le texte formaté de chaque morceau
    /// posé (<c>FormattedText.BuildHighlightGeometry</c>), comme la recette N21 le lit.
    /// </summary>
    private static List<Dessine> Dessines(TexteCompose compose)
    {
        var geometrie = compose.Geometrie() ?? throw new InvalidOperationException("pas de géométrie");
        var metriques = new MetriquesAvalonia();
        var poses = compose.Poser(metriques, metriques);
        var dessines = new List<Dessine>();

        foreach (var posee in geometrie.Lignes)
        {
            var morceaux = posee.Ligne.Morceaux.Where(m => m.Texte.Trim().Length > 0).ToList();
            var siens = poses.Where(p => p.Ligne == posee.Rang).ToList();
            Assert.Equal(morceaux.Count, siens.Count);

            for (var m = 0; m < morceaux.Count; m++)
            {
                Rect? precedente = null;
                for (var dedans = 0; dedans < morceaux[m].Texte.Length; dedans++)
                {
                    var boite = siens[m].Rendu.BuildHighlightGeometry(siens[m].Ancre, dedans, 1)?.Bounds;
                    if (boite is not { } b || b == precedente) continue;
                    precedente = b;
                    dessines.Add(new Dessine(morceaux[m].Debut + dedans, b.Left, b.Right, posee));
                }
            }
        }

        return dessines;
    }

    /// <summary>La bande d'une ligne posée, de son sommet au bas de son avance, ligne de base comprise.</summary>
    private static (double Haut, double Bas) Bande(TexteCompose.LignePosee ligne) =>
        (Math.Min(ligne.Haut, ligne.LigneDeBase), Math.Max(ligne.Haut + ligne.Ligne.Avance, ligne.LigneDeBase));

    /// <summary>Le curseur est-il dessiné sur cette ligne, et non sur une autre ?</summary>
    private static bool SurLaLigne(Rect curseur, TexteCompose.LignePosee ligne)
    {
        var (haut, bas) = Bande(ligne);
        return Math.Abs(curseur.Top - haut) < 0.01 && Math.Abs(curseur.Bottom - bas) < 0.01;
    }

    /// <summary>
    /// Le bord du dessin le plus proche de l'abscisse <paramref name="x"/> sur une ligne : le bord
    /// gauche de chacun de ses caractères dessinés, et le bord droit du dernier. Ce qu'une flèche
    /// verticale doit atteindre : le curseur dessiné ne peut en être plus loin que ce bord-là.
    /// </summary>
    private static double PlusPetitEcart(IEnumerable<Dessine> ligne, double x)
    {
        var siens = ligne.OrderBy(c => c.Indice).ToList();
        var bords = siens.Select(c => c.Gauche).Append(siens[^1].Droite);
        return bords.Min(b => Math.Abs(b - x));
    }

    /// <summary>Le point visé d'un caractère dessiné, dans la fenêtre : au tiers de sa largeur, à mi-hauteur de sa ligne.</summary>
    private static Point Vise(Window fenetre, TexteCompose compose, Dessine caractere) =>
        compose.TranslatePoint(new Point(caractere.Gauche + (caractere.Droite - caractere.Gauche) / 3,
            (caractere.Ligne.Haut + caractere.Ligne.LigneDeBase) / 2), fenetre) ?? throw new InvalidOperationException("hors de la fenêtre");

    /// <summary>Entre dans le texte d'un clic sur le caractère <paramref name="indice"/>, et rend le champ.</summary>
    private static TextBox EntrerSur(Window fenetre, TextFrame bloc, int indice)
    {
        var compose = Compose(fenetre, bloc);
        Rendu.Cliquer(fenetre, Vise(fenetre, compose, Dessines(compose).Single(c => c.Indice == indice)));
        Assert.True(bloc.IsEditing, "le clic n'a pas ouvert la saisie");
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
        Assert.Equal((indice, indice), Selection(zone));
        return zone;
    }

    /// <summary>Le caractère est-il atteint par un clic, son bloc au-dessus des autres en ce point ?</summary>
    private static bool Atteint(Page page, TextFrame bloc, Dessine caractere)
    {
        if (caractere.Droite - caractere.Gauche < 3) return false;
        var x = caractere.Gauche + (caractere.Droite - caractere.Gauche) / 3;
        var y = (caractere.Ligne.Haut + caractere.Ligne.LigneDeBase) / 2;
        const double bord = 8;
        if (x < bord || y < bord || x > bloc.Width - bord || y > bloc.Height - bord) return false;
        var (px, py) = bloc.ToPage(x, y);
        return ReferenceEquals(bloc, page.Frames.LastOrDefault(f => f.Visible && f.HitTest(px, py)));
    }

    // ------------------------------------------------------------------ la mesure de N21

    /// <summary>
    /// La recette : la mesure de N21 rejouée sur ATA 21. Sur chaque bloc non tourné qu'un clic au
    /// milieu ouvre, depuis le milieu de chacune de ses lignes composées — le caractère dessiné du
    /// milieu —, <c>Fin</c>, <c>Début</c>, <c>↓</c> et <c>↑</c> :
    ///
    ///   - <c>Fin</c> pose le curseur au bout de sa ligne composée, et le dessine sur elle, au-delà
    ///     de son dernier caractère dessiné ; <c>Début</c> à son début, au bord de son premier ;
    ///   - <c>↓</c> et <c>↑</c> le posent sur la ligne composée du dessous ou du dessus, dessiné sur
    ///     elle, au bord du dessin le plus proche de l'abscisse de départ.
    ///
    /// N21 comptait 4 écarts pour <c>Fin</c> et pour <c>Début</c>, 21 pour <c>↓</c>, 23 pour
    /// <c>↑</c> : il n'en reste aucun. Le document reste intact. Au juge : <c>↓</c> rendu au champ.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N29")]
    public void La_mesure_de_N21_sur_ATA_21_ne_laisse_aucun_ecart()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Ata21());
        var page = vm.CurrentPage!;
        var avant = DocumentSerializer.ToJson(vm.Document);
        var ecarts = new Dictionary<string, List<string>> { ["Fin"] = [], ["Début"] = [], ["↓"] = [], ["↑"] = [] };
        int blocs = 0, lignes = 0, changementsDeColonne = 0;

        foreach (var bloc in page.Frames.OfType<TextFrame>().Where(b => b.Visible && !b.HasField && b.Rotation == 0).ToList())
        {
            var compose = Compose(fenetre, bloc);
            var dessines = Dessines(compose);
            var atteints = dessines.Where(c => Atteint(page, bloc, c)).ToList();
            if (atteints.Count == 0) continue;

            Rendu.Cliquer(fenetre, Vise(fenetre, compose, atteints[atteints.Count / 2]));
            if (!bloc.IsEditing) continue;
            blocs++;

            var zone = Rendu.ZoneDeSaisie(fenetre, bloc)!;
            var geometrie = Geometrie(fenetre, bloc);
            var parLigne = dessines.GroupBy(c => c.Ligne.Rang).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Indice).ToList());

            foreach (var (rang, siens) in parLigne.OrderBy(p => p.Key))
            {
                lignes++;
                var ligne = geometrie.Lignes[rang];
                var milieu = siens[siens.Count / 2];

                foreach (var (nom, touche) in new[] { ("Fin", PhysicalKey.End), ("Début", PhysicalKey.Home), ("↓", PhysicalKey.ArrowDown), ("↑", PhysicalKey.ArrowUp) })
                {
                    zone.CaretIndex = milieu.Indice;
                    Rendu.Battre();
                    Rendu.Touche(fenetre, touche);
                    var arrivee = zone.CaretIndex;
                    var curseur = Curseur(fenetre, $"« {bloc.Name} », ligne {rang}, {nom}");
                    var ou = $"« {bloc.Name} », ligne {rang} depuis {milieu.Indice} : {nom} mène à {arrivee}";

                    switch (nom)
                    {
                        case "Fin":
                            if (arrivee != ligne.Ligne.Fin || !SurLaLigne(curseur, ligne) || curseur.X < siens[^1].Droite - 0.01)
                            {
                                ecarts[nom].Add($"{ou}, la ligne finit à {ligne.Ligne.Fin} ; curseur à {curseur.X:F2} pt");
                            }

                            break;
                        case "Début":
                            if (arrivee != ligne.Ligne.Debut || !SurLaLigne(curseur, ligne) ||
                                (siens[0].Indice == ligne.Ligne.Debut && Math.Abs(curseur.X - siens[0].Gauche) > 0.01))
                            {
                                ecarts[nom].Add($"{ou}, la ligne commence à {ligne.Ligne.Debut} ; curseur à {curseur.X:F2} pt");
                            }

                            break;
                        default:
                            var cible = rang + (nom == "↓" ? 1 : -1);
                            if (cible < 0 || cible >= geometrie.Lignes.Count) continue;
                            var visee = geometrie.Lignes[cible];
                            if (visee.Ligne.Colonne != ligne.Ligne.Colonne)
                            {
                                changementsDeColonne++;
                                continue;
                            }

                            var bon = parLigne.TryGetValue(cible, out var autres)
                                ? arrivee >= visee.Ligne.Debut && arrivee <= visee.Ligne.Fin &&
                                  Math.Abs(curseur.X - milieu.Gauche) <= PlusPetitEcart(autres, milieu.Gauche) + 0.01
                                : arrivee == visee.Ligne.Debut;
                            if (!bon || !SurLaLigne(curseur, visee))
                            {
                                ecarts[nom].Add($"{ou}, curseur à {curseur.X - milieu.Gauche:+0.00;-0.00} pt de l'abscisse visée, " +
                                                $"ligne attendue {visee.Ligne.Debut}–{visee.Ligne.Fin}");
                            }

                            break;
                    }
                }
            }

            Rendu.Touche(fenetre, PhysicalKey.Escape);
            vm.SetSelection([]);
            Rendu.UneImage();
        }

        foreach (var (nom, liste) in ecarts)
        {
            sortie.WriteLine($"{nom} : {liste.Count} écart(s)");
            foreach (var ecart in liste) sortie.WriteLine("  " + ecart);
        }

        sortie.WriteLine($"{blocs} blocs, {lignes} lignes composées, {changementsDeColonne} flèches d'une colonne à l'autre");
        Assert.True(blocs >= 5, $"{blocs} blocs d'ATA 21 ouverts seulement");
        Assert.True(lignes >= 30, $"{lignes} lignes composées seulement");
        Assert.All(ecarts, paire => Assert.True(paire.Value.Count == 0,
            $"{paire.Key} : {paire.Value.Count} écart(s), dont {paire.Value.FirstOrDefault()}"));
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
    }

    // ------------------------------------------------------------------ l'abscisse visée

    /// <summary>
    /// Trois paragraphes : un long, un court, un long en retrait de 40 pt. Depuis un caractère loin à droite du
    /// premier, <c>↓</c> bute au bout du court ; <c>↓</c> de nouveau retrouve l'abscisse de départ
    /// sur le troisième, et non celle du bout du court ; <c>↑ ↑</c> revient au caractère de départ.
    /// L'abscisse se perd au premier autre déplacement : après <c>←</c>, <c>↓</c> vise depuis le
    /// court. <c>Maj+↓</c> étend la sélection d'une ligne composée, depuis son ancre, et la
    /// sélection se surligne sur les lignes qu'elle prend ; <c>Maj+Fin</c> et <c>Maj+Début</c>
    /// gardent l'ancre d'une sélection faite au champ.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N29")]
    public void Bas_bas_haut_revient_a_l_abscisse_de_depart_et_Maj_bas_etend_d_une_ligne()
    {
        const string texte = "Un premier paragraphe assez long\nBref\nUn troisième paragraphe assez";
        var bloc = Texte(texte);
        // Le troisième paragraphe en retrait : le champ caché, qui ne le connaît pas, viserait ailleurs.
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 2, Length = 1, IndentLeft = 40 });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var compose = Compose(fenetre, bloc);
        var dessines = Dessines(compose);
        var geometrie = Geometrie(fenetre, bloc);
        Assert.Equal(3, geometrie.Lignes.Count);

        var depart = dessines.Single(c => c.Indice == 28);
        var zone = EntrerSur(fenetre, bloc, depart.Indice);

        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Assert.Equal(geometrie.Lignes[1].Ligne.Fin, zone.CaretIndex);

        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        var troisieme = dessines.Where(c => c.Ligne.Rang == 2).ToList();
        var curseur = Curseur(fenetre, "↓ ↓");
        Assert.True(SurLaLigne(curseur, geometrie.Lignes[2]), "↓ ↓ n'est pas sur la troisième ligne");
        Assert.True(Math.Abs(curseur.X - depart.Gauche) <= PlusPetitEcart(troisieme, depart.Gauche) + 0.01,
            $"↓ ↓ dessine le curseur à {curseur.X:F2} pt, l'abscisse de départ est {depart.Gauche:F2} pt");
        Assert.True(curseur.X > dessines.Where(c => c.Ligne.Rang == 1).Max(c => c.Droite) + 20,
            "↓ ↓ vise depuis le bout du paragraphe court, et non depuis le départ");

        Rendu.Touche(fenetre, PhysicalKey.ArrowUp);
        Assert.Equal(geometrie.Lignes[1].Ligne.Fin, zone.CaretIndex);
        Rendu.Touche(fenetre, PhysicalKey.ArrowUp);
        Assert.Equal((depart.Indice, depart.Indice), Selection(zone));
        Assert.True(Math.Abs(Curseur(fenetre, "↓ ↓ ↑ ↑").X - depart.Gauche) < 0.01);

        // Un autre déplacement perd l'abscisse : la flèche vise depuis le paragraphe court.
        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Rendu.Touche(fenetre, PhysicalKey.ArrowLeft);
        var court = Curseur(fenetre, "←");
        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Assert.True(SurLaLigne(Curseur(fenetre, "← ↓"), geometrie.Lignes[2]));
        Assert.True(Math.Abs(Curseur(fenetre, "← ↓").X - court.X) <= PlusPetitEcart(troisieme, court.X) + 0.01,
            "après ←, ↓ vise encore l'abscisse d'avant");

        // Maj+↓ : la sélection s'étend d'une ligne composée, l'ancre au départ.
        zone.CaretIndex = depart.Indice;
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.ArrowDown, RawInputModifiers.Shift);
        Assert.Equal(depart.Indice, zone.SelectionStart);
        Assert.Equal(geometrie.Lignes[1].Ligne.Fin, zone.SelectionEnd);
        Rendu.Touche(fenetre, PhysicalKey.ArrowDown, RawInputModifiers.Shift);
        Assert.Equal(depart.Indice, zone.SelectionStart);
        var fin = zone.SelectionEnd;
        Assert.InRange(fin, geometrie.Lignes[2].Ligne.Debut, geometrie.Lignes[2].Ligne.Fin);
        Assert.Equal(new SelectionDuTexte(bloc, null, depart.Indice, fin, fin), vm.SelectionDuTexte);
        Assert.Equal(3, Marques(fenetre).Plages.Count);
        Assert.Null(Marques(fenetre).CurseurPose);

        Rendu.Touche(fenetre, PhysicalKey.ArrowUp, RawInputModifiers.Shift);
        Assert.Equal((depart.Indice, geometrie.Lignes[1].Ligne.Fin), Selection(zone));

        // Maj+Fin et Maj+Début gardent l'ancre d'une sélection faite au champ, par Maj+→.
        zone.CaretIndex = depart.Indice - 4;
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        Rendu.Touche(fenetre, PhysicalKey.End, RawInputModifiers.Shift);
        Assert.Equal((depart.Indice - 4, geometrie.Lignes[0].Ligne.Fin), Selection(zone));
        Rendu.Touche(fenetre, PhysicalKey.Home, RawInputModifiers.Shift);
        Assert.Equal((depart.Indice - 4, 0), Selection(zone));
        Assert.Equal(texte, bloc.Text);
        Assert.False(vm.CanUndo);
    }

    // ------------------------------------------------------------------ la coupure douce

    /// <summary>
    /// Un paragraphe coupé en douceur : la fin de la première ligne et le début de la seconde sont
    /// la même position. <c>Fin</c> y pose le curseur après l'espace où la ligne se coupe — elle
    /// appartient à la ligne qu'elle finit — et le dessine au bout de la première ligne, non au
    /// début de la seconde ; <c>Fin</c> encore ne bouge rien. <c>↓</c> depuis là vise la seconde
    /// ligne à l'abscisse du bout de la première. <c>Début</c> depuis la seconde ligne la ramène à
    /// son début, dessiné sur elle. Une frappe au bout de la ligne entre au texte à cette position.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N29")]
    public void Fin_sur_une_ligne_coupee_en_douceur_dessine_le_curseur_au_bout_de_la_ligne()
    {
        var bloc = Texte("Une ligne qui se coupe en douceur au bout du bloc étroit, puis continue", largeur: 160);
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var geometrie = Geometrie(fenetre, bloc);
        Assert.True(geometrie.Lignes.Count >= 3, "le paragraphe ne se coupe pas");
        var premiere = geometrie.Lignes[0];
        var seconde = geometrie.Lignes[1];
        Assert.Equal(premiere.Ligne.Fin, seconde.Ligne.Debut);
        Assert.Equal(' ', bloc.Text![premiere.Ligne.Fin - 1]);

        var dessines = Dessines(Compose(fenetre, bloc));
        var zone = EntrerSur(fenetre, bloc, dessines.Where(c => c.Ligne.Rang == 0).ElementAt(3).Indice);

        Rendu.Touche(fenetre, PhysicalKey.End);
        Assert.Equal((premiere.Ligne.Fin, premiere.Ligne.Fin), Selection(zone));
        var curseur = Curseur(fenetre, "Fin");
        Assert.True(SurLaLigne(curseur, premiere), "Fin dessine le curseur au début de la ligne d'en dessous");
        var dernier = dessines.Where(c => c.Ligne.Rang == 0).Max(c => c.Droite);
        Assert.True(curseur.X > dernier, $"Fin dessine le curseur à {curseur.X:F2} pt, avant le bout de la ligne ({dernier:F2} pt)");

        Rendu.Touche(fenetre, PhysicalKey.End);
        Assert.Equal(curseur, Curseur(fenetre, "Fin Fin"));

        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Assert.True(SurLaLigne(Curseur(fenetre, "Fin ↓"), seconde));
        var secondeDessinee = dessines.Where(c => c.Ligne.Rang == 1).ToList();
        Assert.True(Math.Abs(Curseur(fenetre, "Fin ↓").X - curseur.X) <= PlusPetitEcart(secondeDessinee, curseur.X) + 0.01);

        Rendu.Touche(fenetre, PhysicalKey.Home);
        Assert.Equal((seconde.Ligne.Debut, seconde.Ligne.Debut), Selection(zone));
        Assert.True(SurLaLigne(Curseur(fenetre, "Début"), seconde), "Début dessine le curseur au bout de la ligne d'au-dessus");
        Assert.True(Math.Abs(Curseur(fenetre, "Début").X - secondeDessinee[0].Gauche) < 0.01);

        // Fin, puis une frappe : elle entre à la position du bout de la ligne.
        Rendu.Touche(fenetre, PhysicalKey.ArrowUp);
        Rendu.Touche(fenetre, PhysicalKey.End);
        Rendu.Taper(fenetre, "X");
        Assert.Equal('X', bloc.Text[premiere.Ligne.Fin]);
        Assert.True(bloc.IsEditing);
    }

    // ------------------------------------------------------------------ les colonnes, les bouts

    /// <summary>
    /// Un bloc à deux colonnes : <c>↓</c> sur la dernière ligne de la première colonne mène à la
    /// première de la seconde, dans l'ordre du texte, au même endroit de sa colonne ; <c>↑</c> en
    /// revient. <c>Pg.Suiv</c> mène à la dernière ligne du bloc, <c>Pg.Préc</c> à la première, à
    /// l'abscisse visée. <c>↑</c> sur la première ligne mène au début du texte, <c>↓</c> sur la
    /// dernière à sa fin ; <c>Ctrl+Début</c> et <c>Ctrl+Fin</c> restent au champ, aux bouts du texte.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N29")]
    public void Dans_un_bloc_a_deux_colonnes_bas_mene_de_la_derniere_ligne_d_une_colonne_a_la_premiere_de_la_suivante()
    {
        var texte = string.Join("\n", Enumerable.Range(1, 12).Select(i => $"Ligne numéro {i}"));
        var bloc = Texte(texte, largeur: 320, hauteur: 110);
        bloc.Columns = 2;
        bloc.ColumnGap = 20;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var geometrie = Geometrie(fenetre, bloc);
        var dessines = Dessines(Compose(fenetre, bloc));
        var derniere = geometrie.Lignes.Last(l => l.Ligne.Colonne == 0);
        var premiere = geometrie.Lignes.First(l => l.Ligne.Colonne == 1);
        Assert.Equal(derniere.Rang + 1, premiere.Rang);
        sortie.WriteLine($"{geometrie.Lignes.Count} lignes, la première colonne finit à la ligne {derniere.Rang}");

        var depart = dessines.Where(c => c.Ligne.Rang == derniere.Rang).ElementAt(4);
        var zone = EntrerSur(fenetre, bloc, depart.Indice);
        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Assert.InRange(zone.CaretIndex, premiere.Ligne.Debut, premiere.Ligne.Fin);
        var curseur = Curseur(fenetre, "↓");
        Assert.True(SurLaLigne(curseur, premiere), "↓ ne mène pas à la première ligne de la seconde colonne");
        var pas = dessines.First(c => c.Ligne.Rang == premiere.Rang).Gauche - dessines.First(c => c.Ligne.Rang == derniere.Rang).Gauche;
        Assert.True(pas > 100, $"la seconde colonne n'est qu'à {pas:F1} pt de la première");
        Assert.True(Math.Abs(curseur.X - pas - depart.Gauche) < 0.01,
            $"↓ dessine le curseur à {curseur.X - pas:F2} pt dans sa colonne, le départ était à {depart.Gauche:F2} pt dans la sienne");

        Rendu.Touche(fenetre, PhysicalKey.ArrowUp);
        Assert.Equal(depart.Indice, zone.CaretIndex);

        Rendu.Touche(fenetre, PhysicalKey.PageDown);
        var bas = geometrie.Lignes[^1];
        Assert.InRange(zone.CaretIndex, bas.Ligne.Debut, bas.Ligne.Fin);
        Assert.True(SurLaLigne(Curseur(fenetre, "Pg.Suiv"), bas));
        Rendu.Touche(fenetre, PhysicalKey.PageUp);
        Assert.Equal(geometrie.Lignes[0].Ligne.Debut + (depart.Indice - derniere.Ligne.Debut), zone.CaretIndex);
        Assert.True(SurLaLigne(Curseur(fenetre, "Pg.Préc"), geometrie.Lignes[0]));

        Rendu.Touche(fenetre, PhysicalKey.ArrowUp);
        Assert.Equal((0, 0), Selection(zone));
        Rendu.Touche(fenetre, PhysicalKey.PageDown);
        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Assert.Equal((texte.Length, texte.Length), Selection(zone));

        Rendu.Touche(fenetre, PhysicalKey.Home, RawInputModifiers.Control);
        Assert.Equal((0, 0), Selection(zone));
        Rendu.Touche(fenetre, PhysicalKey.End, RawInputModifiers.Control);
        Assert.Equal((texte.Length, texte.Length), Selection(zone));
        Rendu.Touche(fenetre, PhysicalKey.PageUp, RawInputModifiers.Shift);
        Assert.Equal(texte.Length, zone.SelectionStart);
        Assert.InRange(zone.SelectionEnd, 0, geometrie.Lignes[0].Ligne.Fin);
        Assert.True(bloc.IsEditing);
        Assert.False(vm.CanUndo);
    }

    // ------------------------------------------------------------------ le bloc tourné

    /// <summary>
    /// Un bloc tourné de 30° navigue dans son repère, comme à plat : la même suite de touches —
    /// <c>↓</c>, <c>↓</c>, <c>Fin</c>, <c>↑</c>, <c>Début</c>, <c>Pg.Suiv</c>, <c>Maj+↑</c> — mène aux mêmes
    /// positions, et le curseur se dessine au même endroit du bloc. Le champ caché, posé sur le
    /// bloc, s'y écarte : c'est la composition qui décide.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N29")]
    public void Un_bloc_tourne_de_30_degres_navigue_comme_a_plat()
    {
        const string texte = "Le premier paragraphe se coupe sur deux lignes au moins\nUn second\nEt un troisième paragraphe pour finir la page";
        var touches = new (PhysicalKey, RawInputModifiers)[]
        {
            (PhysicalKey.ArrowDown, RawInputModifiers.None), (PhysicalKey.ArrowDown, RawInputModifiers.None),
            (PhysicalKey.End, RawInputModifiers.None), (PhysicalKey.ArrowUp, RawInputModifiers.None),
            (PhysicalKey.Home, RawInputModifiers.None), (PhysicalKey.PageDown, RawInputModifiers.None),
            (PhysicalKey.ArrowUp, RawInputModifiers.Shift),
        };

        List<(int, int, Rect?)> Jouer(double rotation)
        {
            // Un retrait à gauche sur le premier paragraphe : le champ caché, qui ne le connaît
            // pas, ne coupe pas ses lignes aux mêmes endroits que la composition.
            var bloc = Texte(texte, largeur: 220, rotation: rotation);
            bloc.Paragraphs.Add(new ParagraphSpan { Start = 0, Length = 1, IndentLeft = 50 });
            var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(bloc));
            var geometrie = Geometrie(fenetre, bloc);
            var dessines = Dessines(Compose(fenetre, bloc));
            var zone = EntrerSur(fenetre, bloc, dessines.Single(c => c.Indice == 9).Indice);
            var rang = 0;
            var suite = new List<(int, int, Rect?)>();
            foreach (var (touche, avec) in touches)
            {
                Rendu.Touche(fenetre, touche, avec);
                var curseur = Marques(fenetre).CurseurPose;
                suite.Add((zone.SelectionStart, zone.SelectionEnd, curseur));
                if (avec != RawInputModifiers.None) continue;

                // Chaque touche, lue sur les lignes composées : la ligne où le curseur se dessine,
                // et la position où Fin et Début le posent.
                var ligne = geometrie.Lignes.Single(l => SurLaLigne(curseur ?? throw new InvalidOperationException($"{touche} : aucun curseur"), l));
                var attendu = touche switch
                {
                    PhysicalKey.ArrowDown => rang + 1,
                    PhysicalKey.ArrowUp => rang - 1,
                    PhysicalKey.PageDown => geometrie.Lignes.Count - 1,
                    _ => rang,
                };
                Assert.True(attendu == ligne.Rang, $"{rotation}°, {touche} : le curseur est sur la ligne {ligne.Rang}, et non sur la ligne {attendu}");
                if (touche == PhysicalKey.End) Assert.Equal(ligne.Ligne.Fin, zone.CaretIndex);
                if (touche == PhysicalKey.Home) Assert.Equal(ligne.Ligne.Debut, zone.CaretIndex);
                rang = ligne.Rang;
            }

            fenetre.Close();
            return suite;
        }

        var aPlat = Jouer(0);
        var tourne = Jouer(30);
        for (var i = 0; i < touches.Length; i++)
        {
            sortie.WriteLine($"{touches[i]} : à plat {aPlat[i]}, tourné {tourne[i]}");
        }

        Assert.Equal(aPlat, tourne);
        Assert.True(aPlat.Select(p => (p.Item1, p.Item2)).Distinct().Count() >= 5, "la suite de touches ne va pas assez loin");
    }

    // ------------------------------------------------------------------ la méthode d'entrée

    /// <summary>
    /// La fenêtre de candidats de la méthode d'entrée se pose au curseur dessiné. Avalonia la place
    /// au rectangle que le client du champ qui a le focus donne, dans le repère de ce champ
    /// (<c>TextInputMethodManager.UpdateCursorRect</c>) : le test le demande comme lui, en levant la
    /// demande du client sur le champ, et le mène dans la fenêtre comme lui. Sur un paragraphe en
    /// retrait de 60 pt — le champ caché n'a pas de retrait —, il tombe sur le curseur dessiné, à un
    /// centième de pixel, quand celui du champ en est loin ; il suit <c>↓</c> et <c>Fin</c>, et le
    /// client le signale. Le reste du client est celui du champ.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N29")]
    public void La_fenetre_de_candidats_se_pose_au_curseur_dessine()
    {
        var bloc = Texte("Premier paragraphe\nSecond paragraphe en retrait, assez long pour se couper en deux");
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 1, Length = 1, IndentLeft = 60 });
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var dessines = Dessines(Compose(fenetre, bloc));
        var zone = EntrerSur(fenetre, bloc, 24);
        var marques = Marques(fenetre);

        var demande = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        zone.RaiseEvent(demande);
        var client = demande.Client ?? throw new InvalidOperationException("le champ ne donne pas de client à la méthode d'entrée");
        var signalements = 0;
        client.CursorRectangleChanged += (_, _) => signalements++;

        // Le rectangle que le champ caché donnerait seul, sur ses propres lignes.
        Rect DuChamp() => Assert.IsType<ClientAuCurseurDessine>(client).Champ.CursorRectangle;

        void AuCurseurDessine(string quoi)
        {
            var dessine = marques.CurseurPose ?? throw new InvalidOperationException($"{quoi} : aucun curseur dessiné");
            var attendu = marques.TranslatePoint(dessine.TopLeft.Transform(marques.Transformation), fenetre)!.Value;
            var lu = zone.TranslatePoint(client.CursorRectangle.TopLeft, fenetre)!.Value;
            Assert.True(Point.Distance(attendu, lu) < 0.01, $"{quoi} : la méthode d'entrée lit le curseur en {lu}, il est dessiné en {attendu}");

            var champ = zone.TranslatePoint(DuChamp().TopLeft, fenetre)!.Value;
            sortie.WriteLine($"{quoi} : dessiné en {attendu}, lu en {lu}, le champ caché en {champ}");
        }

        AuCurseurDessine("le clic");
        var presentation = zone.GetVisualDescendants().OfType<TextPresenter>().Single();
        var champ = zone.TranslatePoint(DuChamp().TopLeft, fenetre)!.Value;
        var dessine = marques.TranslatePoint(marques.CurseurPose!.Value.TopLeft.Transform(marques.Transformation), fenetre)!.Value;
        Assert.True(Point.Distance(champ, dessine) > 20, "le curseur du champ caché est déjà sur le curseur dessiné : le test ne prouve rien");

        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Assert.True(signalements > 0, "↓ ne signale pas le curseur à la méthode d'entrée");
        AuCurseurDessine("↓");
        signalements = 0;
        Rendu.Touche(fenetre, PhysicalKey.End);
        Assert.True(signalements > 0, "Fin ne signale pas le curseur à la méthode d'entrée");
        AuCurseurDessine("Fin");

        Assert.True(client.SupportsPreedit);
        Assert.Same(presentation, client.TextViewVisual);
        Assert.False(string.IsNullOrEmpty(client.SurroundingText));
        Assert.True(dessines.Count > 0);
    }
}
