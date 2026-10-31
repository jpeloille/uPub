using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les caractères spéciaux — le poste N22 (fiche 23, a27 et d14) : <i>Affichage ▸ Caractères
/// spéciaux</i> et <c>Ctrl+Maj+Y</c>, comme Publisher (le livre, p. 9 et 24). Une bascule de la
/// session, qui montre sur le texte de la page un point pour chaque espace, une flèche pour chaque
/// tabulation, ¶ à chaque fin de paragraphe et ↵ à chaque saut de ligne forcé.
///
/// Les marques se posent aux places de N3 (<see cref="TexteCompose.PointDe"/>), qu'on relit ici sur
/// la composition elle-même, et se dessinent dans l'<c>Overlay</c>, hors de <c>PageRoot</c> : l'encre
/// de la page ne change pas, bascule ouverte ou fermée, et rien n'entre au document ni à
/// l'historique. Les gestes sont ceux du banc (<see cref="Rendu"/>, poste N1).
/// </summary>
public class CaracteresSpeciauxTests
{
    // ------------------------------------------------------------------ le banc du poste

    /// <summary>La tolérance du plan : un centième de point.</summary>
    private const double Tolerance = 0.01;

    private static TextFrame Texte(string texte, string nom = "Texte", double x = 60, double y = 60,
                                   double largeur = 300, double hauteur = 160, double corps = 14, double rotation = 0) => new()
    {
        Name = nom, X = x, Y = y, Width = largeur, Height = hauteur, Text = texte, Rotation = rotation,
        FontFamily = "DejaVu Sans", FontSize = corps, Foreground = "#FF000000",
    };

    private static CaracteresSpeciaux Marques(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<CaracteresSpeciaux>().Single();

    private static TexteCompose Compose(Window fenetre, TextFrame bloc) =>
        fenetre.GetVisualDescendants().OfType<TexteCompose>().Single(c => ReferenceEquals(c.Bloc, bloc));

    private static MenuItem Menu(Window fenetre, string nom) =>
        fenetre.GetLogicalDescendants().OfType<MenuItem>().FirstOrDefault(m => m.Name == nom)
        ?? throw new InvalidOperationException($"le menu n'a pas d'entrée « {nom} »");

    /// <summary><c>Ctrl+Maj+Y</c>, pressé et relâché, puis ce que la planche a à faire.</summary>
    private static void Basculer(Window fenetre)
    {
        Rendu.Touche(fenetre, PhysicalKey.Y, RawInputModifiers.Control | RawInputModifiers.Shift, relacher: true);
        Rendu.UneImage();
    }

    /// <summary>Les caractères dessinés sur un bloc ; aucun s'il n'en montre pas.</summary>
    private static IReadOnlyList<CaracterePose> De(CaracteresSpeciaux marques, TextFrame bloc) =>
        marques.Blocs.SingleOrDefault(b => ReferenceEquals(b.Compose.Bloc, bloc))?.Caracteres ?? [];

    private static int Compte(IReadOnlyList<CaracterePose> caracteres, CaractereSpecial sorte) =>
        caracteres.Count(c => c.Sorte == sorte);

    /// <summary>Chaque marque est au point de N3 de son caractère, à un centième de point.</summary>
    private static void AuxPointsDeN3(TexteCompose compose, IReadOnlyList<CaracterePose> caracteres, string quoi)
    {
        foreach (var caractere in caracteres)
        {
            var attendu = compose.PointDe(caractere.Index) ?? throw new InvalidOperationException($"{quoi} : pas de géométrie");
            Assert.True(Point.Distance(attendu, caractere.Point) <= Tolerance,
                $"{quoi} : {caractere.Sorte} {caractere.Index} dessiné en {caractere.Point}, N3 le met en {attendu}");
        }
    }

    /// <summary>Le document n'a pas bougé : ni pas d'annulation, ni « modifié », ni transaction ouverte.</summary>
    private static void RienAuDocument(MainWindowViewModel vm, string avant, string quoi)
    {
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, $"{quoi} : un pas d'annulation est entré à l'historique");
        Assert.False(vm.IsDirty, $"{quoi} : le document est « modifié »");
    }

    private static Document Charger(string nom) =>
        DocumentSerializer.FromJson(File.ReadAllText(Path.Combine(Rendu.Racine(), "Samples", nom)));

    // ------------------------------------------------------------------ la bascule

    /// <summary>
    /// <c>Ctrl+Maj+Y</c> et l'entrée du menu Affichage basculent la session, que le menu montre
    /// cochée, avec sa touche ; rien n'entre au document ni à l'historique, et <c>Ctrl+Maj+Y</c>
    /// n'est pas lu comme <c>Ctrl+Y</c>, qui rétablirait. Fermée à l'ouverture, comme Publisher.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N22")]
    public void Ctrl_Maj_Y_et_le_menu_basculent_la_session_sans_rien_au_document()
    {
        var bloc = Texte("Bonjour le monde");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            var avant = DocumentSerializer.ToJson(vm.Document);
            var menu = Menu(fenetre, "MenuCaracteresSpeciaux");
            Assert.Equal(KeyGesture.Parse("Ctrl+Shift+Y"), menu.InputGesture);
            Assert.False(vm.CaracteresSpeciauxVisibles, "les caractères spéciaux se montrent à l'ouverture");
            Assert.False(menu.IsChecked);
            // Bascule jamais ouverte, la planche n'a rien posé : son arbre est celui d'avant le poste.
            Assert.Empty(fenetre.GetVisualDescendants().OfType<CaracteresSpeciaux>());

            Basculer(fenetre);
            var marques = Marques(fenetre);
            Assert.True(vm.CaracteresSpeciauxVisibles);
            Assert.True(menu.IsChecked);
            Assert.Equal(3, De(marques, bloc).Count);

            Basculer(fenetre);
            Assert.False(vm.CaracteresSpeciauxVisibles);
            Assert.False(menu.IsChecked);
            Assert.Empty(marques.Blocs);

            // L'entrée du menu, comme un clic : sa case est liée dans les deux sens.
            menu.IsChecked = true;
            Rendu.UneImage();
            Assert.True(vm.CaracteresSpeciauxVisibles);
            Assert.Equal(3, De(marques, bloc).Count);
            vm.BasculerLesCaracteresSpeciauxCommand.Execute(null);
            Rendu.UneImage();
            Assert.False(menu.IsChecked);

            RienAuDocument(vm, avant, "la bascule");
            Assert.Equal("banc.mypub", vm.FilePath);
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ la recette du plan

    /// <summary>
    /// La recette du plan, sur <c>recette-texte-riche.mypub</c> : autant de ¶ que de paragraphes dans
    /// chaque bloc, chacun au <c>PointDe</c> de son saut à 0,01 pt, une marque par espace ; l'encre de
    /// la page identique, bascule ouverte ou fermée ; et les marques hors de <c>PageRoot</c>, dans
    /// l'<c>Overlay</c>. C'est la garde du juge : une marque dessinée dans la page change son encre.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N22")]
    public void Sur_le_texte_riche_un_pied_de_mouche_par_paragraphe_et_l_encre_de_la_page_ne_change_pas()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Charger("recette-texte-riche.mypub"));
        try
        {
            Rendu.UneImage();
            var encreFermee = Rendu.Encre(fenetre);
            var avant = DocumentSerializer.ToJson(vm.Document);

            Basculer(fenetre);
            var marques = Marques(fenetre);
            var blocs = vm.CurrentPage!.Frames.OfType<TextFrame>().Where(b => b.Visible && !b.HasField).ToList();
            Assert.Equal(3, blocs.Count);
            Assert.Equal(blocs.Count, marques.Blocs.Count);

            foreach (var bloc in blocs)
            {
                var caracteres = De(marques, bloc);
                var texte = bloc.Text ?? "";
                Assert.Equal(Recalage.NombreDeParagraphes(texte), Compte(caracteres, CaractereSpecial.FinDeParagraphe));
                Assert.Equal(texte.Count(c => c == ' '), Compte(caracteres, CaractereSpecial.Espace));
                AuxPointsDeN3(Compose(fenetre, bloc), caracteres, bloc.Name ?? "?");
            }

            // L'encre de la page : au pixel, ouverte comme fermée.
            Assert.Equal(encreFermee.Pixels, Rendu.Encre(fenetre).Pixels);
            Basculer(fenetre);
            Assert.Equal(encreFermee.Pixels, Rendu.Encre(fenetre).Pixels);
            Basculer(fenetre);

            // Le contrôle qui dessine est dans l'Overlay, et rien de lui dans PageRoot.
            var page = fenetre.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "PageRoot");
            Assert.Equal("Overlay", (marques.GetVisualParent() as Control)?.Name);
            Assert.Empty(page.GetVisualDescendants().OfType<CaracteresSpeciaux>());
            Assert.Equal(encreFermee.Pixels, Rendu.Encre(fenetre).Pixels);

            RienAuDocument(vm, avant, "les caractères spéciaux");
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// La même chose sur les documents réels : chaque bloc de texte de l'exemple et d'ATA 21 montre
    /// autant de ¶ que de paragraphes, une marque par espace et par tabulation, un ↵ par saut forcé,
    /// toutes au point de N3.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N22")]
    public void Sur_l_exemple_et_ATA_21_chaque_marque_est_au_point_de_N3()
    {
        foreach (var (nom, document) in new[] { ("l'exemple", Charger("exemple-bulletin.mypub")), ("ATA 21", Rendu.Ata21()) })
        {
            var (fenetre, vm) = Rendu.Ouvrir(document);
            try
            {
                Basculer(fenetre);
                var marques = Marques(fenetre);
                var vus = 0;
                foreach (var compose in fenetre.GetVisualDescendants().OfType<TexteCompose>()
                             .Where(c => c.IsEffectivelyVisible && c.Geometrie() is not null))
                {
                    var bloc = compose.Bloc!;
                    var texte = bloc.Text ?? "";
                    var caracteres = De(marques, bloc);
                    Assert.Equal(Recalage.NombreDeParagraphes(texte), Compte(caracteres, CaractereSpecial.FinDeParagraphe));
                    Assert.Equal(texte.Count(c => c == ' '), Compte(caracteres, CaractereSpecial.Espace));
                    Assert.Equal(texte.Count(c => c == '\t'), Compte(caracteres, CaractereSpecial.Tabulation));
                    Assert.Equal(texte.Count(c => c == Compositeur.SautDeLigne), Compte(caracteres, CaractereSpecial.SautDeLigne));
                    AuxPointsDeN3(compose, caracteres, $"{nom}, « {bloc.Name} »");
                    vus++;
                }

                Assert.True(vus >= 3, $"{nom} : {vus} blocs de texte vus");
            }
            finally
            {
                fenetre.Close();
            }
        }
    }

    // ------------------------------------------------------------------ chaque sorte

    /// <summary>
    /// Un texte fait pour : deux espaces de suite, une tabulation, un saut forcé, un paragraphe vide,
    /// un « \r\n », une espace en fin de texte. Chaque espace et la tabulation occupent leur chasse,
    /// du point de N3 du caractère au point du suivant ; ↵ au saut forcé ; ¶ au saut d'un paragraphe
    /// vide, au « \r » d'un « \r\n » — une seule fois —, et au bout du texte. Un réglage du bloc les
    /// recompose : ils suivent le corps.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N22")]
    public void Espaces_tabulation_saut_force_paragraphe_vide_et_retour_chariot()
    {
        const string texte = "Un  deux\tTrois\u2028quatre\n\ncinq\r\nsix ";
        var bloc = Texte(texte);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            Basculer(fenetre);
            var marques = Marques(fenetre);
            var compose = Compose(fenetre, bloc);
            var caracteres = De(marques, bloc);

            int[] Index(CaractereSpecial sorte) => caracteres.Where(c => c.Sorte == sorte).Select(c => c.Index).ToArray();
            Assert.Equal(new[] { 2, 3, texte.Length - 1 }, Index(CaractereSpecial.Espace));
            Assert.Equal(new[] { texte.IndexOf('\t') }, Index(CaractereSpecial.Tabulation));
            Assert.Equal(new[] { texte.IndexOf('\u2028') }, Index(CaractereSpecial.SautDeLigne));
            var fins = Index(CaractereSpecial.FinDeParagraphe);
            Assert.Equal(new[] { texte.IndexOf('\n'), texte.IndexOf('\n') + 1, texte.IndexOf('\r'), texte.Length }, fins);
            Assert.Equal(Recalage.NombreDeParagraphes(texte), fins.Length);
            AuxPointsDeN3(compose, caracteres, "le texte fait pour");

            // La chasse : du point du caractère au point du suivant, sur la même ligne.
            foreach (var blanc in caracteres.Where(c => c.Sorte is CaractereSpecial.Espace or CaractereSpecial.Tabulation
                                                       && c.Index + 1 < texte.Length))
            {
                var suivant = compose.PointDe(blanc.Index + 1)!.Value;
                Assert.Equal(suivant.Y, blanc.Point.Y);
                Assert.True(Math.Abs(blanc.Point.X + blanc.Chasse - suivant.X) <= Tolerance,
                    $"{blanc.Sorte} {blanc.Index} : chasse {blanc.Chasse}, le caractère suivant à {suivant.X}");
                Assert.True(blanc.Chasse > 0);
            }

            var tabulation = caracteres.Single(c => c.Sorte == CaractereSpecial.Tabulation);
            Assert.True(tabulation.Chasse > caracteres.First(c => c.Sorte == CaractereSpecial.Espace).Chasse,
                "la tabulation n'avance pas plus qu'une espace jusqu'au taquet");
            Assert.All(caracteres, c => Assert.Equal(14.0, c.Taille));

            // Le corps change : les marques se recomposent, au nouveau corps et aux nouveaux points.
            bloc.FontSize = 20;
            Rendu.UneImage();
            caracteres = De(marques, bloc);
            Assert.All(caracteres, c => Assert.Equal(20.0, c.Taille));
            AuxPointsDeN3(compose, caracteres, "après le corps");
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// Une ligne renvoyée garde le blanc de tête du mot qu'elle renvoie (N3) : son espace se marque
    /// sur elle, à la chasse d'une espace, et non sur la ligne suivante. Sur une ligne justifiée,
    /// une espace élargie occupe toute sa chasse, jusqu'au mot qui la suit : son point est au milieu
    /// du blanc qu'on voit.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N22")]
    public void Une_espace_pendante_reste_sur_sa_ligne_et_une_espace_justifiee_prend_sa_chasse()
    {
        const string texte = "Une phrase assez longue pour passer plusieurs fois à la ligne dans un bloc étroit.";
        var pendante = Texte(texte, "Pendante", largeur: 120);
        var justifiee = Texte(texte, "Justifiee", y: 260, largeur: 120);
        justifiee.Align = TextAlign.Justify;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(pendante, justifiee));
        try
        {
            Basculer(fenetre);
            var marques = Marques(fenetre);

            // L'espace qui pend au bout de chaque ligne renvoyée.
            var geometrie = Compose(fenetre, pendante).Geometrie()!;
            var espaces = De(marques, pendante);
            var renvois = geometrie.Lignes.Take(geometrie.Lignes.Count - 1).ToList();
            Assert.True(renvois.Count >= 3, $"{renvois.Count} lignes renvoyées");
            var espace = geometrie.Metriques.Largeur(" ", geometrie.StyleDe(1));
            foreach (var ligne in renvois)
            {
                Assert.Equal(' ', texte[ligne.Ligne.Fin - 1]);
                var bout = espaces.Single(c => c.Index == ligne.Ligne.Fin - 1);
                Assert.Equal(ligne.LigneDeBase, bout.Point.Y);
                Assert.True(Math.Abs(bout.Chasse - espace) <= Tolerance,
                    $"l'espace pendante {bout.Index} a {bout.Chasse} de chasse, une espace en a {espace}");
            }

            // Sur la ligne justifiée, une espace élargie va jusqu'au mot qui la suit.
            var compose = Compose(fenetre, justifiee);
            var lignes = compose.Geometrie()!.Lignes;
            var elargies = 0;
            foreach (var blanc in De(marques, justifiee).Where(c => c.Sorte == CaractereSpecial.Espace))
            {
                var ligne = lignes.Single(l => l.Ligne.Debut <= blanc.Index && blanc.Index < l.Ligne.Fin);
                if (blanc.Index + 1 >= ligne.Ligne.Fin) continue;

                var suivant = compose.PointDe(blanc.Index + 1)!.Value;
                Assert.True(Math.Abs(blanc.Point.X + blanc.Chasse - suivant.X) <= Tolerance);
                if (blanc.Chasse > espace + 0.5) elargies++;
            }

            Assert.True(elargies >= 3, $"{elargies} espaces élargies par la justification");
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ en saisie

    /// <summary>
    /// En saisie, sur le texte composé (N21) : <c>Ctrl+Maj+Y</c> joue sans refermer la saisie ni
    /// toucher au texte, et les marques suivent la frappe — une espace tapée, un paragraphe ouvert
    /// par Entrée, un saut forcé par <c>Maj+Entrée</c> —, toujours au point de N3.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N22")]
    public void En_saisie_les_marques_suivent_la_frappe()
    {
        var bloc = Texte("Bonjour le monde");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        try
        {
            var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
            Basculer(fenetre);
            Assert.True(vm.CaracteresSpeciauxVisibles);
            Assert.True(bloc.IsEditing, "Ctrl+Maj+Y a refermé la saisie");
            Assert.True(zone.IsFocused, "Ctrl+Maj+Y a pris le focus au champ");
            Assert.Equal("Bonjour le monde", bloc.Text);

            var marques = Marques(fenetre);
            Assert.Equal(2, Compte(De(marques, bloc), CaractereSpecial.Espace));

            Rendu.Taper(fenetre, " x");
            Rendu.UneImage();
            Assert.Equal(3, Compte(De(marques, bloc), CaractereSpecial.Espace));

            Rendu.Touche(fenetre, PhysicalKey.Enter);
            Rendu.Touche(fenetre, PhysicalKey.Enter, RawInputModifiers.Shift);
            Rendu.UneImage();
            Assert.True(bloc.IsEditing);
            var caracteres = De(marques, bloc);
            Assert.Equal(2, Compte(caracteres, CaractereSpecial.FinDeParagraphe));
            Assert.Equal(1, Compte(caracteres, CaractereSpecial.SautDeLigne));
            AuxPointsDeN3(Compose(fenetre, bloc), caracteres, "en saisie");

            Basculer(fenetre);
            Assert.True(bloc.IsEditing);
            Assert.Empty(marques.Blocs);
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ ce qu'on voit

    /// <summary>
    /// Ce qui se dessine, lu sur l'image composée de la fenêtre : sur un bloc tourné de 30° à 200 %,
    /// le ¶ du bout du texte se dessine à l'accent, là où la transformation du bloc le mène, et rien
    /// ne s'y dessine bascule fermée. Un ¶ d'une ligne qui sort du bloc ne se dessine pas : le bloc
    /// découpe ses marques comme son texte.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N22")]
    public void Les_marques_se_dessinent_a_l_accent_tournees_a_l_echelle_et_decoupees_par_le_bloc()
    {
        var tourne = Texte("Mot", "Tourne", x: 80, y: 80, largeur: 160, hauteur: 60, corps: 24, rotation: 30);
        var coupe = Texte("Ligne un\nLigne deux\nLigne trois", "Coupe", x: 80, y: 260, largeur: 160, hauteur: 22);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tourne, coupe));
        try
        {
            vm.Zoom = 2 / Units.PointToDip;
            Rendu.UneImage();
            Basculer(fenetre);
            var marques = Marques(fenetre);

            // La boîte du signe d'une marque, dans la fenêtre : ses quatre coins menés par la
            // transformation de son bloc, puis de l'Overlay à la fenêtre.
            (Point Min, Point Max) Boite(CaracteresDuBloc bloc, CaracterePose marque)
            {
                var coins = new[]
                {
                    new Point(marque.Point.X, marque.Point.Y - marque.Taille * 0.8),
                    new Point(marque.Point.X + marque.Taille * 0.6, marque.Point.Y - marque.Taille * 0.8),
                    new Point(marque.Point.X, marque.Point.Y),
                    new Point(marque.Point.X + marque.Taille * 0.6, marque.Point.Y),
                }.Select(c => marques.TranslatePoint(c.Transform(bloc.Transformation), fenetre)!.Value).ToArray();
                return (new Point(coins.Min(c => c.X), coins.Min(c => c.Y)), new Point(coins.Max(c => c.X), coins.Max(c => c.Y)));
            }

            // Les pixels de l'accent — le bleu franc de l'apparence, lissé sur le blanc — dans une boîte.
            int Bleus(Rendu.Image image, (Point Min, Point Max) boite)
            {
                var n = 0;
                for (var y = (int)boite.Min.Y; y <= (int)boite.Max.Y; y++)
                for (var x = (int)boite.Min.X; x <= (int)boite.Max.X; x++)
                {
                    if (x < 0 || y < 0 || x >= image.Largeur || y >= image.Hauteur) continue;
                    var i = (y * image.Largeur + x) * 4;
                    // Le bleu et le rouge très écartés, quel que soit l'ordre des canaux de l'image :
                    // le noir du texte et le blanc de la page ont les trois canaux égaux.
                    if (Math.Abs(image.Pixels[i] - image.Pixels[i + 2]) > 60) n++;
                }

                return n;
            }

            var tournes = marques.Blocs.Single(b => ReferenceEquals(b.Compose.Bloc, tourne));
            var pied = tournes.Caracteres.Single(c => c.Sorte == CaractereSpecial.FinDeParagraphe);
            var boite = Boite(tournes, pied);
            var compose = Compose(fenetre, tourne);
            Assert.Equal(compose.TransformToVisual(marques), tournes.Transformation);

            var coupes = marques.Blocs.Single(b => ReferenceEquals(b.Compose.Bloc, coupe));
            var piedsCoupes = coupes.Caracteres.Where(c => c.Sorte == CaractereSpecial.FinDeParagraphe).ToList();
            Assert.Equal(3, piedsCoupes.Count);
            var premier = Boite(coupes, piedsCoupes[0]);
            var dernier = Boite(coupes, piedsCoupes[2]);

            Rendu.UneImage();
            var ouverte = Rendu.Composee(fenetre);
            Assert.True(Bleus(ouverte, boite) > 20, $"le ¶ du bloc tourné ne se voit pas ({Bleus(ouverte, boite)} pixels d'accent)");
            Assert.True(Bleus(ouverte, premier) > 20, "le ¶ de la première ligne du bloc coupé ne se voit pas");
            Assert.Equal(0, Bleus(ouverte, dernier));

            Basculer(fenetre);
            var fermee = Rendu.Composee(fenetre);
            Assert.Equal(0, Bleus(fermee, boite));
            Assert.Equal(0, Bleus(fermee, premier));
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// Les marques suivent les blocs que la page montre : une autre page montre les siens, un bloc
    /// masqué (I8) n'en montre pas, un bloc ajouté en montre. Bascule fermée, la planche ne suit plus
    /// rien : les blocs qu'elle écoutait n'ont plus d'abonné de plus qu'avant, et ceux de la page
    /// quittée non plus.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N22")]
    public void Les_marques_suivent_la_page_et_lachent_les_blocs_une_fois_fermees()
    {
        var premier = Texte("Page un", "Premier");
        var second = Texte("Page deux", "Second");
        var masque = Texte("Masque", "Masque", y: 260);
        masque.Visible = false;
        var document = Rendu.Feuille(premier);
        var page2 = new Page();
        page2.Frames.Add(second);
        page2.Frames.Add(masque);
        document.Pages.Add(page2);
        var (fenetre, vm) = Rendu.Ouvrir(document);
        try
        {
            // Ceux de la planche qui écoutent un bloc pour ses caractères spéciaux.
            int Ecoutes(TextFrame bloc) => Rendu.Abonnes(bloc).Count(d => d.Method.Name.Contains("Caracteres"));
            string?[] Montres(CaracteresSpeciaux marques) => marques.Blocs.Select(b => b.Compose.Bloc?.Name).ToArray();

            Assert.Equal(0, Ecoutes(premier));
            Basculer(fenetre);
            var marques = Marques(fenetre);
            Assert.Equal(new[] { "Premier" }, Montres(marques));
            Assert.Equal(1, Ecoutes(premier));

            vm.CurrentPage = page2;
            Rendu.UneImage();
            Assert.Equal(new[] { "Second" }, Montres(marques));
            Assert.Equal(0, Ecoutes(premier));
            Assert.Equal(0, Ecoutes(masque));

            var ajoute = Texte("Ajouté", "Ajoute", y: 400);
            page2.Frames.Add(ajoute);
            Rendu.UneImage();
            Assert.Equal(new[] { "Second", "Ajoute" }, Montres(marques));

            masque.Visible = true;
            Rendu.UneImage();
            Assert.Contains("Masque", Montres(marques));

            Basculer(fenetre);
            Assert.Empty(marques.Blocs);
            foreach (var bloc in new[] { premier, second, masque, ajoute }) Assert.Equal(0, Ecoutes(bloc));
        }
        finally
        {
            fenetre.Close();
        }
    }
}
