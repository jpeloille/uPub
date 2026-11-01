using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Taper dans le texte d'un groupe, et prendre un membre seul — le poste N26 (fiche 20 ; fiche 23,
/// a6 et c15). Le livre retape le texte d'un élément de la bibliothèque dans le groupe, sans le
/// dissocier, et prend un membre d'un clic pour en remplacer l'image (p. 58-60). La sélection de
/// MyPublisher ne voyait que le premier niveau, et la saisie exigeait un bloc de texte seul : il
/// fallait dissocier.
///
/// Les gestes sont ceux du banc (<see cref="Rendu"/>, poste N1). Les points visés dans un groupe
/// tourné sont menés par le modèle — le repère de chaque niveau, rotation comprise —, et ce que la
/// planche dessine est relu sur les contrôles qui le dessinent : les deux doivent tomber au même
/// endroit.
/// </summary>
public class SaisieDansUnGroupeTests
{
    // ------------------------------------------------------------------ le banc du poste

    /// <summary>Un demi-pixel au zoom 100 % : l'écart toléré entre le modèle et l'écran.</summary>
    private const double Seuil = 0.5;

    private static TextFrame Texte(string texte = "Bonjour le monde", double rotation = 0) => new()
    {
        Name = "Légende", X = 20, Y = 20, Width = 240, Height = 70, Text = texte, Rotation = rotation,
        FontFamily = "DejaVu Sans", FontSize = 14, Foreground = "#FF000000",
    };

    private static ImageFrame Image() => new() { Name = "Logo", X = 40, Y = 120, Width = 100, Height = 80 };

    /// <summary>Un groupe de 300 × 240 pt en (100, 120), tourné de <paramref name="rotation"/> degrés.</summary>
    private static GroupFrame Groupe(double rotation, params Frame[] blocs)
    {
        var groupe = new GroupFrame { Name = "Logo du club", X = 100, Y = 120, Width = 300, Height = 240, Rotation = rotation };
        foreach (var bloc in blocs) groupe.Children.Add(bloc);
        return groupe;
    }

    private static TexteCompose Compose(Window fenetre, TextFrame bloc) =>
        fenetre.GetVisualDescendants().OfType<TexteCompose>().Single(c => ReferenceEquals(c.Bloc, bloc));

    private static GeometrieDuTexte Geometrie(Window fenetre, TextFrame bloc) =>
        Compose(fenetre, bloc).Geometrie() ?? throw new InvalidOperationException($"« {bloc.Name} » n'a pas de géométrie");

    private static T Nomme<T>(Window fenetre, string nom) where T : Control =>
        fenetre.GetVisualDescendants().OfType<T>().Single(c => c.Name == nom);

    /// <summary>Les réglages que la barre montre : ceux du bloc qu'elle vise.</summary>
    private static object? BlocDeLaBarre(Window fenetre) => Nomme<GabaritsGardes>(fenetre, "ReglagesDuBloc").Bloc;

    /// <summary>Les sections du dock tirées des gabarits du bloc : chacune vise un bloc.</summary>
    private static List<GabaritsGardes> SectionsDuDock(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<GabaritsGardes>().Where(g => g.Name != "ReglagesDuBloc").ToList();

    /// <summary>
    /// Un point du repère du dernier bloc du chemin, dans la fenêtre, mené par le modèle : le repère
    /// de chaque groupe, rotation comprise (<see cref="MainWindowViewModel.VersLaPage"/>), puis la page.
    /// </summary>
    private static Point ParLeModele(Window fenetre, IReadOnlyList<Frame> chemin, double x, double y)
    {
        var (px, py) = MainWindowViewModel.VersLaPage(chemin, x, y);
        return Rendu.SurLaPage(fenetre, px, py);
    }

    /// <summary>Le point visé d'un caractère, dans le repère du bloc : au tiers de sa chasse, à mi-hauteur de sa ligne.</summary>
    private static Point Caractere(GeometrieDuTexte geometrie, int indice)
    {
        var bord = geometrie.CurseurDe(indice);
        var chasse = geometrie.CurseurDe(indice + 1).X - bord.X;
        return new Point(bord.X + chasse / 3, bord.Y + bord.Height / 2);
    }

    private static void Proche(Point attendu, Point lu, string quoi) =>
        Assert.True(Math.Abs(attendu.X - lu.X) < Seuil && Math.Abs(attendu.Y - lu.Y) < Seuil,
            $"{quoi} : {lu} à l'écran, {attendu} par le modèle");

    /// <summary>Le centre d'un bloc d'un groupe, dans la fenêtre, mené par le modèle.</summary>
    private static Point CentreDuMembre(Window fenetre, GroupFrame groupe, Frame membre) =>
        ParLeModele(fenetre, [groupe, membre], membre.Width / 2, membre.Height / 2);

    /// <summary>Un point du fond du groupe, hors de ses blocs, dans la fenêtre.</summary>
    private static Point FondDuGroupe(Window fenetre, GroupFrame groupe) =>
        ParLeModele(fenetre, [groupe], groupe.Width - 20, groupe.Height - 15);

    /// <summary>Le groupe pris d'un clic sur son fond, puis l'image d'un clic sur elle : deux points écartés, deux clics simples.</summary>
    private static void PrendreLImage(Window fenetre, MainWindowViewModel vm, GroupFrame groupe, ImageFrame image)
    {
        Rendu.Cliquer(fenetre, FondDuGroupe(fenetre, groupe));
        Assert.Same(groupe, Assert.Single(vm.Selection));
        Assert.Null(vm.MembreSeul);

        Rendu.Cliquer(fenetre, CentreDuMembre(fenetre, groupe, image));
        Assert.Same(image, vm.MembreSeul);
    }

    private static Polygon[] CadresVisibles(Window fenetre) =>
        Nomme<Canvas>(fenetre, "Overlay").Children.OfType<Polygon>().Where(p => p.IsVisible).ToArray();

    // ------------------------------------------------------------------ la recette

    /// <summary>
    /// La recette du plan : un clic sur le 3ᵉ caractère du texte d'un groupe ouvre sa saisie, le
    /// curseur à 2, le groupe restant sélectionné ; la frappe fait un pas, et le groupe garde ses
    /// enfants. Au juge : la saisie qui dissocie le groupe.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void Un_clic_sur_le_troisieme_caractere_d_un_groupe_ouvre_sa_saisie_le_groupe_restant_selectionne()
    {
        var texte = Texte();
        var image = Image();
        var groupe = Groupe(0, texte, image);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));
        var page = vm.CurrentPage!;
        var avant = DocumentSerializer.ToJson(vm.Document);

        var vise = Caractere(Geometrie(fenetre, texte), 2);
        Rendu.Cliquer(fenetre, ParLeModele(fenetre, [groupe, texte], vise.X, vise.Y));

        Assert.True(texte.IsEditing, "le clic dans le texte du groupe n'a pas ouvert sa saisie");
        Assert.Same(groupe, Assert.Single(vm.Selection));
        Assert.Same(groupe, vm.SingleSelected);
        Assert.Same(texte, vm.TexteEnSaisie);
        Assert.Same(texte, vm.BlocVise);
        var zone = Rendu.ZoneDeSaisie(fenetre, texte) ?? throw new InvalidOperationException("aucune zone de saisie");
        Assert.True(zone.IsFocused, "la zone de saisie du membre n'a pas le focus");
        Assert.Equal(2, zone.CaretIndex);
        Assert.Equal(2, vm.SelectionDuTexte?.Curseur);

        Rendu.Taper(fenetre, "X");
        Assert.Equal("BoXnjour le monde", texte.Text);
        Assert.Equal(new Frame[] { texte, image }, groupe.Children);
        Assert.Same(groupe, Assert.Single(page.Frames));
        Assert.Same(groupe, Assert.Single(vm.Selection));

        // Échap referme la saisie : le groupe reste pris, entier.
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(texte.IsEditing);
        Assert.Null(vm.TexteEnSaisie);
        Assert.Same(groupe, Assert.Single(vm.Selection));

        var repris = Assert.IsType<GroupFrame>(Rendu.UnSeulPas(vm, avant, "la frappe dans le texte d'un groupe"));
        Assert.Equal(2, repris.Children.Count);
        Assert.Equal("Bonjour le monde", Assert.IsType<TextFrame>(repris.Children[0]).Text);
    }

    /// <summary>
    /// Pendant la saisie d'un membre, la barre, le dock, la pastille, l'alerte d'excédent et les
    /// mises en forme visent le texte en saisie, et non le groupe, qui reste la sélection : le gras
    /// de R7 sur un mot, l'alignement de R6 et l'exposant de N24 touchent le membre, chacun en un pas,
    /// et rien ne se dissocie.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void La_barre_le_dock_et_les_mises_en_forme_suivent_le_texte_en_saisie_du_groupe()
    {
        var texte = Texte();
        var image = Image();
        var groupe = Groupe(0, texte, image);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));

        Rendu.Cliquer(fenetre, FondDuGroupe(fenetre, groupe));
        Assert.Same(groupe, BlocDeLaBarre(fenetre));
        Assert.Equal("Groupe", vm.Pastille);

        var mot = Caractere(Geometrie(fenetre, texte), 9);   // « le »
        Rendu.DoubleCliquer(fenetre, ParLeModele(fenetre, [groupe, texte], mot.X, mot.Y));
        Assert.Same(texte, vm.TexteEnSaisie);
        Assert.Equal((8, 10), (vm.SelectionDuTexte!.Debut, vm.SelectionDuTexte.Fin));

        Assert.Same(texte, BlocDeLaBarre(fenetre));
        Assert.All(SectionsDuDock(fenetre).Where(s => s.Bloc is not null), s => Assert.Same(texte, s.Bloc));
        Assert.Equal("Bloc de texte du groupe", vm.Pastille);
        Assert.Same(texte, vm.MiseEnFormeVisee.Bloc);
        Assert.Same(texte, vm.ParagrapheVise.Bloc);
        Assert.True(vm.MiseEnFormeVisee.EnSaisie);
        Assert.True(vm.BasculerGrasCommand.CanExecute(null));

        // Ctrl+G : le mot seul passe en gras, dans le membre, en un pas.
        var avant = DocumentSerializer.ToJson(vm.Document);
        Rendu.Touche(fenetre, PhysicalKey.G, RawInputModifiers.Control);
        var gras = Assert.Single(texte.Spans, f => f.Bold == true);
        Assert.Equal((8, 2), (gras.Start, gras.Length));
        Assert.Equal(new Frame[] { texte, image }, groupe.Children);
        Assert.Same(groupe, Assert.Single(vm.Selection));

        // Ctrl+E : le paragraphe du membre est centré (R6) ; Ctrl+Maj+= : le mot en exposant (N24).
        Rendu.Touche(fenetre, PhysicalKey.E, RawInputModifiers.Control);
        Assert.Equal(TextAlign.Center, ParagrapheEffectif.Resoudre(1, texte.Paragraphs, texte.Paragraph)[0].Align);
        vm.BasculerLExposantCommand.Execute(null);
        Assert.True(vm.MiseEnFormeVisee.Exposant, "le mot du membre n'est pas en exposant");
        Assert.True(texte.IsEditing, "la mise en forme a refermé la saisie du membre");

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Same(groupe, BlocDeLaBarre(fenetre));
        Assert.Same(groupe, Assert.Single(vm.Selection));
        Assert.Equal(new Frame[] { texte, image }, groupe.Children);

        // Trois pas : le gras, l'alignement, l'exposant.
        for (var pas = 0; pas < 3; pas++)
        {
            Assert.True(vm.CanUndo, $"il manque un pas ({pas} défaits)");
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
        }

        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
    }

    /// <summary>
    /// Les repères locaux et les rotations imbriquées : dans un groupe tourné de 30° qui porte un
    /// texte tourné de 20°, l'écran dessine le texte où le modèle le met ; un clic mené par le modèle
    /// sur le 6ᵉ caractère pose le curseur à 5 ; le curseur dessiné, les caractères spéciaux de N22
    /// et le trait ondulé de N25 tombent sur le texte, là où le modèle le place.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void Dans_un_groupe_tourne_le_curseur_les_marques_et_les_fautes_tombent_sur_le_texte()
    {
        var texte = Texte("We will recieve it", rotation: 20);
        var groupe = Groupe(30, texte, Image());
        var document = Rendu.Feuille(groupe);
        document.Langue = "en-US";
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.Orthographe = new Orthographe(System.IO.Path.Combine(Rendu.Racine(), "Scripts", "fixtures", "orthographe"));
        vm.CaracteresSpeciauxVisibles = true;
        Rendu.UneImage();
        var chemin = new Frame[] { groupe, texte };

        // L'écran dessine le texte où le modèle le met : coins et centre du bloc.
        var compose = Compose(fenetre, texte);
        foreach (var (x, y) in new[] { (0.0, 0.0), (texte.Width, 0), (texte.Width, texte.Height), (0, texte.Height), (120, 35) })
        {
            Proche(ParLeModele(fenetre, chemin, x, y), compose.TranslatePoint(new Point(x, y), fenetre)!.Value,
                $"le point ({x} ; {y}) du texte");
        }

        // Un clic sur le 6ᵉ caractère, mené par le modèle : la saisie s'ouvre, le curseur à 5.
        var geometrie = Geometrie(fenetre, texte);
        var vise = Caractere(geometrie, 5);
        Rendu.Cliquer(fenetre, ParLeModele(fenetre, chemin, vise.X, vise.Y));
        Assert.True(texte.IsEditing);
        Assert.Same(groupe, Assert.Single(vm.Selection));
        Assert.Equal(5, vm.SelectionDuTexte?.Curseur);

        // Le curseur dessiné : sur la composition, mené par la transformation du contrôle qui la dessine.
        var marques = Nomme<MarquesDuTexte>(fenetre, "MarquesDeLaSaisie");
        var curseur = marques.CurseurPose ?? throw new InvalidOperationException("aucun curseur dessiné");
        Assert.Equal(geometrie.CurseurDe(5), curseur);
        var pied = new Point(curseur.X, curseur.Bottom);
        Proche(ParLeModele(fenetre, chemin, pied.X, pied.Y),
            marques.TranslatePoint(pied.Transform(marques.Transformation), fenetre)!.Value, "le pied du curseur");

        // Les caractères spéciaux du membre (N22) : ¶ au bout du texte, où le modèle le met.
        var speciaux = fenetre.GetVisualDescendants().OfType<CaracteresSpeciaux>().Single();
        var siens = speciaux.Blocs.Single(b => ReferenceEquals(b.Compose.Bloc, texte));
        var finDeParagraphe = siens.Caracteres.Single(c => c.Sorte == CaractereSpecial.FinDeParagraphe);
        Proche(ParLeModele(fenetre, chemin, finDeParagraphe.Point.X, finDeParagraphe.Point.Y),
            speciaux.TranslatePoint(finDeParagraphe.Point.Transform(siens.Transformation), fenetre)!.Value,
            "le ¶ du texte du groupe");

        // Le trait ondulé de N25 sous « recieve » : de la place de son « r » à celle qui le suit.
        var fautes = fenetre.GetVisualDescendants().OfType<FautesSoulignees>().Single();
        var soulignes = fautes.Blocs.Single(b => ReferenceEquals(b.Compose.Bloc, texte));
        var trait = Assert.Single(soulignes.Traits);
        Assert.Equal("recieve", trait.Faute.Mot);
        var debut = geometrie.PointDe(8);
        Assert.True(Math.Abs(debut.X - trait.Gauche) < 0.01, $"le trait part de {trait.Gauche}, le « r » est en {debut.X}");
        Proche(ParLeModele(fenetre, chemin, trait.Gauche, debut.Y),
            fautes.TranslatePoint(new Point(trait.Gauche, debut.Y).Transform(soulignes.Transformation), fenetre)!.Value,
            "le départ du trait ondulé");

        // Le cadre du membre en saisie suit les deux rotations.
        var coin = ParLeModele(fenetre, chemin, -3.25, -3.25);
        var surface = Nomme<Panel>(fenetre, "Surface");
        Assert.Contains(CadresVisibles(fenetre), cadre => cadre.Points.Any(p =>
            surface.TranslatePoint(p, fenetre) is { } ici && Math.Abs(ici.X - coin.X) < 1 && Math.Abs(ici.Y - coin.Y) < 1));
    }

    // ------------------------------------------------------------------ prendre un membre seul

    /// <summary>
    /// La recette du plan : un clic sur l'image d'un groupe sélectionné la prend seule — la barre et
    /// le dock la visent, son cadre et ses poignées se dessinent —, et <c>Suppr</c> ne retire
    /// qu'elle, en un pas ; le groupe reste, et reste la sélection.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void Un_clic_sur_l_image_d_un_groupe_selectionne_la_prend_seule_et_Suppr_ne_retire_qu_elle()
    {
        var texte = Texte();
        var image = Image();
        var groupe = Groupe(0, texte, image);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));
        var page = vm.CurrentPage!;
        var avant = DocumentSerializer.ToJson(vm.Document);

        PrendreLImage(fenetre, vm, groupe, image);
        Assert.Same(groupe, Assert.Single(vm.Selection));
        Assert.Same(image, vm.BlocVise);
        Assert.Same(image, BlocDeLaBarre(fenetre));
        Assert.All(SectionsDuDock(fenetre).Where(s => s.Bloc is not null), s => Assert.Same(image, s.Bloc));
        Assert.Equal("Image du groupe", vm.Pastille);
        Assert.False(vm.PositionReglable);

        // Le cadre du groupe, et celui de l'image ; ses huit poignées, inertes, sur son cadre.
        Assert.Equal(2, CadresVisibles(fenetre).Length);
        var poignees = Nomme<Canvas>(fenetre, "Overlay").Children.OfType<Shape>()
            .Where(p => p.IsVisible && p.Classes.Contains("poignee")).ToList();
        Assert.Equal(8, poignees.Count);
        Assert.All(poignees, p => Assert.Contains("inerte", p.Classes));
        Assert.Null(Rendu.PoigneeDeRotation(fenetre));
        var surface = Nomme<Panel>(fenetre, "Surface");
        var coin = poignees.Select(p => surface.TranslatePoint(new Point(Canvas.GetLeft(p) + p.Width / 2, Canvas.GetTop(p) + p.Height / 2), fenetre)!.Value)
            .OrderBy(p => p.X + p.Y).First();
        Proche(ParLeModele(fenetre, [groupe, image], -3.25, -3.25), coin, "la poignée du coin de l'image");

        Rendu.Touche(fenetre, PhysicalKey.Delete);
        Assert.Equal(new Frame[] { texte }, groupe.Children);
        Assert.Same(groupe, Assert.Single(page.Frames));
        Assert.Same(groupe, Assert.Single(vm.Selection));
        Assert.Null(vm.MembreSeul);

        var repris = Assert.IsType<GroupFrame>(Rendu.UnSeulPas(vm, avant, "Suppr sur l'image du groupe"));
        Assert.Equal(2, repris.Children.Count);
    }

    /// <summary>
    /// Les flèches et l'alignement d'un membre pris seul restent dans son groupe : la flèche le
    /// déplace du pas de la planche dans le sens de l'écran, groupe tourné compris, chaque fois en un
    /// pas, et le retient au cadre du groupe ; aligner à droite le pose sur le bord droit du groupe,
    /// et non de la page. Le groupe ne bouge pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void Les_fleches_et_l_alignement_d_un_membre_pris_seul_restent_dans_son_groupe()
    {
        var image = Image();
        var groupe = Groupe(0, Texte(), image);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));
        PrendreLImage(fenetre, vm, groupe, image);

        var avant = DocumentSerializer.ToJson(vm.Document);
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        Assert.Equal((43.0, 120.0), (image.X, image.Y));
        Assert.Equal((100.0, 120.0), (groupe.X, groupe.Y));
        Assert.Same(image, vm.MembreSeul);
        var reprise = Assert.IsType<GroupFrame>(Rendu.UnSeulPas(vm, avant, "la flèche sur l'image du groupe"));
        image = Assert.IsType<ImageFrame>(reprise.Children[1]);
        groupe = reprise;

        // Aligner à droite : sur le bord droit du groupe.
        PrendreLImage(fenetre, vm, groupe, image);
        vm.AlignRightCommand.Execute(null);
        Assert.Equal(groupe.Width, image.X + image.Width, 6);
        Assert.Equal((100.0, 120.0), (groupe.X, groupe.Y));
        Assert.Same(groupe, Assert.Single(vm.CurrentPage!.Frames));

        // Au bord, la flèche ne le sort pas du groupe.
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        Assert.Equal(groupe.Width, image.X + image.Width, 6);
    }

    /// <summary>Dans un groupe tourné de 90°, la flèche droite mène le membre vers la droite de l'écran.</summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void Dans_un_groupe_tourne_la_fleche_mene_le_membre_dans_le_sens_de_l_ecran()
    {
        var image = Image();
        var groupe = Groupe(90, Texte(), image);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));
        PrendreLImage(fenetre, vm, groupe, image);

        var avant = MainWindowViewModel.VersLaPage([groupe, image], image.Width / 2, image.Height / 2);
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        var apres = MainWindowViewModel.VersLaPage([groupe, image], image.Width / 2, image.Height / 2);
        Assert.Equal(3, apres.X - avant.X, 6);
        Assert.Equal(0, apres.Y - avant.Y, 6);
    }

    /// <summary>
    /// Couper l'image prise seule d'un groupe tourné la retire du groupe, en un pas, et la met au
    /// presse-papiers telle qu'elle se voit sur la page : recollée, elle reprend sa place et son
    /// angle, celui du groupe compris.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void Couper_un_membre_pris_seul_le_retire_du_groupe_et_le_recolle_ou_il_se_voyait()
    {
        var texte = Texte();
        var image = Image();
        var groupe = Groupe(30, texte, image);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));
        var page = vm.CurrentPage!;
        PrendreLImage(fenetre, vm, groupe, image);
        var centre = MainWindowViewModel.VersLaPage([groupe, image], image.Width / 2, image.Height / 2);

        var avant = DocumentSerializer.ToJson(vm.Document);
        var contenu = vm.CouperLaSelection() ?? throw new InvalidOperationException("rien n'a été coupé");
        Assert.Equal(new Frame[] { texte }, groupe.Children);
        Assert.Same(groupe, Assert.Single(page.Frames));
        Assert.True(vm.CanUndo);

        Assert.True(vm.CollerDesBlocs(contenu.Blocs));
        var colle = Assert.IsType<ImageFrame>(page.Frames[^1]);
        Assert.Equal(30, colle.Rotation, 6);
        var (cx, cy) = colle.ToPage(colle.Width / 2, colle.Height / 2);
        Assert.Equal(centre.X, cx, 6);
        Assert.Equal(centre.Y, cy, 6);

        vm.UndoCommand.Execute(null);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
    }

    /// <summary>
    /// Un membre pris seul ne se redimensionne pas — c'est réservé à la dissociation : ses poignées
    /// ne se prennent pas, le dock refuse sa taille. <c>Échap</c> rend le groupe ; un clic hors du
    /// groupe rend la sélection ordinaire.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void Un_membre_pris_seul_ne_se_redimensionne_pas_et_un_clic_hors_du_groupe_rend_la_selection_ordinaire()
    {
        var image = Image();
        var groupe = Groupe(0, Texte(), image);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));
        PrendreLImage(fenetre, vm, groupe, image);

        // La poignée du coin bas droit, tirée : l'image garde sa taille.
        var poignee = ParLeModele(fenetre, [groupe, image], image.Width + 3.25, image.Height + 3.25);
        Rendu.Glisser(fenetre, poignee, poignee + new Vector(30, 20));
        Assert.Equal((100.0, 80.0), (image.Width, image.Height));

        // Le dock : la largeur saisie ne s'écrit pas.
        PrendreLImage(fenetre, vm, groupe, image);
        vm.LargeurDuBloc = 150;
        Assert.Equal(100, image.Width);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Null(vm.MembreSeul);
        Assert.Same(groupe, Assert.Single(vm.Selection));
        Assert.Same(groupe, vm.BlocVise);

        PrendreLImage(fenetre, vm, groupe, image);
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, 500, 600));
        Assert.Empty(vm.Selection);
        Assert.Null(vm.MembreSeul);
        Assert.Null(vm.BlocVise);
    }

    /// <summary>
    /// Un glissé parti d'un membre du groupe sélectionné déplace le groupe entier, comme avant, et
    /// le groupe redevient le bloc visé ; parti du membre pris seul, il déplace le membre dans son
    /// groupe, en un pas, et le groupe ne bouge pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void Un_glisse_deplace_le_groupe_et_depuis_le_membre_pris_seul_le_membre()
    {
        var texte = Texte();
        var image = Image();
        var groupe = Groupe(0, texte, image);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));
        PrendreLImage(fenetre, vm, groupe, image);

        // Loin du clic qui l'a prise : deux pressions au même point feraient un double-clic.
        var avant = DocumentSerializer.ToJson(vm.Document);
        var depart = CentreDuMembre(fenetre, groupe, image) + new Vector(20, 10);
        Rendu.Glisser(fenetre, depart, depart + new Vector(12, 6));
        Assert.Equal((52.0, 126.0), (image.X, image.Y));
        Assert.Equal((100.0, 120.0), (groupe.X, groupe.Y));
        Assert.Same(image, vm.MembreSeul);
        var repris = Assert.IsType<GroupFrame>(Rendu.UnSeulPas(vm, avant, "le glissé de l'image du groupe"));
        image = Assert.IsType<ImageFrame>(repris.Children[1]);
        groupe = repris;

        // Depuis un autre point du groupe : le groupe entier.
        PrendreLImage(fenetre, vm, groupe, image);
        var fond = FondDuGroupe(fenetre, groupe);
        Rendu.Glisser(fenetre, fond, fond + new Vector(12, 6));
        Assert.Equal((112.0, 126.0), (groupe.X, groupe.Y));
        Assert.Equal((40.0, 120.0), (image.X, image.Y));
        Assert.Null(vm.MembreSeul);
        Assert.Same(groupe, vm.BlocVise);
    }

    /// <summary>
    /// Un double-clic sur l'image d'un groupe la prend seule et ouvre sa section du dock ; dans un
    /// groupe qui en porte un autre, un second clic descend d'un niveau.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void Un_double_clic_ouvre_la_section_de_l_image_et_un_groupe_imbrique_se_traverse_d_un_niveau_par_clic()
    {
        var image = Image();
        var interieur = new GroupFrame { Name = "Intérieur", X = 20, Y = 100, Width = 200, Height = 120 };
        image.X = 10;
        image.Y = 10;
        interieur.Children.Add(image);
        interieur.Children.Add(new ShapeFrame { Name = "Cadre", X = 150, Y = 10, Width = 40, Height = 40 });
        var groupe = Groupe(0, Texte(), interieur);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));

        Rendu.Cliquer(fenetre, FondDuGroupe(fenetre, groupe));
        var surLImage = ParLeModele(fenetre, [groupe, interieur, image], image.Width / 2, image.Height / 2);
        Rendu.Cliquer(fenetre, surLImage);
        Assert.Same(interieur, vm.MembreSeul);
        Rendu.Cliquer(fenetre, surLImage + new Vector(10, 0));
        Assert.Same(image, vm.MembreSeul);
        Assert.Same(groupe, Assert.Single(vm.Selection));

        // Le double-clic : le premier clic prend le groupe intérieur, le second descend jusqu'à la
        // forme, la prend seule et ouvre sa section du dock.
        Rendu.Cliquer(fenetre, FondDuGroupe(fenetre, groupe));
        Assert.Null(vm.MembreSeul);
        vm.SectionFondOuverte = false;
        Rendu.DoubleCliquer(fenetre, ParLeModele(fenetre, [groupe, interieur], 170, 30));
        Assert.Same(interieur.Children[1], vm.MembreSeul);
        Assert.True(vm.SectionFondOuverte, "le double-clic sur la forme du groupe n'a pas ouvert sa section");
        Assert.Same(groupe, Assert.Single(vm.Selection));
    }

    /// <summary>
    /// La découpe de Q5 suit le membre qu'on règle : un filet de 8 pt posé sur l'image prise seule
    /// élargit la découpe du groupe à l'écran comme à l'export, et l'image retirée la ramène.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void La_decoupe_du_groupe_suit_le_filet_du_membre_regle_et_le_membre_retire()
    {
        var texte = Texte();
        var image = Image();
        var groupe = Groupe(0, texte, image);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));
        var liste = fenetre.GetVisualDescendants().OfType<ItemsControl>()
            .Single(i => i.Classes.Contains("frames") && ReferenceEquals(i.DataContext, groupe));

        Rect Decoupe() => Assert.IsType<RectangleGeometry>(liste.Clip).Rect;
        Rect Attendue()
        {
            var zone = Models.Decoupe.DesBlocs(groupe.Width, groupe.Height, groupe.Children);
            return new Rect(zone.Left, zone.Top, zone.Width, zone.Height);
        }

        Assert.Equal(new Rect(0, 0, 300, 240), Decoupe());

        PrendreLImage(fenetre, vm, groupe, image);
        vm.ContourDuBloc = vm.SortesDeContour[1];
        image.Stroke = "#FF000000";
        image.StrokeThickness = 8;
        Rendu.UneImage();
        Assert.Equal(Attendue(), Decoupe());
        Assert.Equal(new Rect(-4, -4, 308, 248), Decoupe());

        Rendu.Touche(fenetre, PhysicalKey.Delete);
        Assert.Equal(new Frame[] { texte }, groupe.Children);
        Rendu.UneImage();
        Assert.Equal(new Rect(0, 0, 300, 240), Decoupe());
    }

    /// <summary>
    /// Le texte d'un groupe en saisie, <c>Alt</c>+flèche déplace le membre dans son groupe ; un clic sur
    /// l'image du même groupe referme la saisie et prend l'image seule : le groupe reste la sélection
    /// d'un bout à l'autre.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N26")]
    public void De_la_saisie_d_un_membre_un_clic_sur_un_autre_le_prend_seul()
    {
        var texte = Texte();
        var image = Image();
        var groupe = Groupe(0, texte, image);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));

        var vise = Caractere(Geometrie(fenetre, texte), 4);
        Rendu.Cliquer(fenetre, ParLeModele(fenetre, [groupe, texte], vise.X, vise.Y));
        Assert.Same(texte, vm.TexteEnSaisie);

        // Alt+flèche (N6) mène le bloc dont le texte a le curseur : le membre, dans son groupe.
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
        Assert.Equal((23.0, 20.0), (texte.X, texte.Y));
        Assert.Equal((100.0, 120.0), (groupe.X, groupe.Y));
        Assert.True(texte.IsEditing, "Alt+flèche a refermé la saisie du membre");

        Rendu.Cliquer(fenetre, CentreDuMembre(fenetre, groupe, image));
        Assert.False(texte.IsEditing);
        Assert.Null(vm.TexteEnSaisie);
        Assert.Same(image, vm.MembreSeul);
        Assert.Same(groupe, Assert.Single(vm.Selection));
    }
}
