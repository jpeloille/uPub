using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
/// La vérification orthographique sur la vraie fenêtre — le poste N25 (fiche 23, a26 ; décision 6
/// de la phase N). Publisher souligne les fautes pendant la frappe, les propose au clic droit et
/// les parcourt par <c>F7</c>. Ici : le trait ondulé dans l'<c>Overlay</c>, aux abscisses de N3,
/// jamais dans <c>PageRoot</c> ni au PDF ; <c>F7</c> et la barre de N20 en mode orthographe ; les
/// suggestions en tête du menu du texte (A12) ; le dictionnaire de l'utilisateur dans les
/// préférences (N11) ; la case « Vérifier pendant la frappe ».
///
/// Le correcteur lit le petit dictionnaire écrit pour les tests, dans
/// <c>Scripts/fixtures/orthographe/</c> — jamais ceux du système. Les gestes sont ceux du banc
/// (<see cref="Rendu"/>, poste N1).
/// </summary>
public class VerificationOrthographiqueTests
{
    // ------------------------------------------------------------------ le banc du poste

    /// <summary>La tolérance des places de N3 : un centième de point.</summary>
    private const double Tolerance = 0.01;

    private static string Dictionnaires() => Path.Combine(Rendu.Racine(), "Scripts", "fixtures", "orthographe");

    private static TextFrame Texte(string texte, string nom = "Texte", double x = 60, double y = 60,
                                   double largeur = 320, double hauteur = 120, double rotation = 0) => new()
    {
        Name = nom, X = x, Y = y, Width = largeur, Height = hauteur, Text = texte, Rotation = rotation,
        FontFamily = "DejaVu Sans", FontSize = 14, Foreground = "#FF000000",
    };

    /// <summary>Une page seule, en anglais, la langue du dictionnaire de test.</summary>
    private static Document EnAnglais(params Frame[] blocs)
    {
        var document = Rendu.Feuille(blocs);
        document.Langue = "en-US";
        return document;
    }

    /// <summary>La fenêtre sur le document, avec le correcteur du dictionnaire de test.</summary>
    private static (Window Fenetre, MainWindowViewModel Vm) Ouvrir(Document document, Preferences? preferences = null)
    {
        var (fenetre, vm) = Rendu.Ouvrir(document, preferences);
        vm.Orthographe = new Orthographe(Dictionnaires());
        Rendu.UneImage();
        return (fenetre, vm);
    }

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static FautesSoulignees Fautes(Window fenetre) => fenetre.GetVisualDescendants().OfType<FautesSoulignees>().Single();

    private static TexteCompose Compose(Window fenetre, TextFrame bloc) =>
        fenetre.GetVisualDescendants().OfType<TexteCompose>().Single(c => ReferenceEquals(c.Bloc, bloc));

    /// <summary>Les mots soulignés d'un bloc, dans l'ordre ; aucun s'il n'en montre pas.</summary>
    private static string[] Soulignes(Window fenetre, TextFrame bloc) =>
        Fautes(fenetre).Blocs.SingleOrDefault(b => ReferenceEquals(b.Compose.Bloc, bloc))?.Traits
            .Select(t => t.Faute.Mot).ToArray() ?? [];

    private static BarreDeRecherche Barre(Window fenetre) => Trouver<BarreDeRecherche>(fenetre, "BarreDeRecherche");

    private static Button Bouton(Window fenetre, string nom) => Trouver<Button>(Barre(fenetre), nom);

    /// <summary><c>F7</c>, pressé, puis ce que la fenêtre et la planche ont à faire.</summary>
    private static void F7(Window fenetre)
    {
        Rendu.Touche(fenetre, PhysicalKey.F7);
        Rendu.UneImage();
    }

    /// <summary>La faute montrée : le mot sélectionné en saisie (N15), dans quel bloc, et ce que la barre en dit.</summary>
    private static (string Mot, TextFrame Bloc) Montree(MainWindowViewModel vm)
    {
        var selection = vm.SelectionDuTexte ?? throw new InvalidOperationException("rien n'est sélectionné en saisie");
        var bloc = Assert.IsType<TextFrame>(selection.Bloc);
        Assert.True(bloc.IsEditing, $"« {bloc.Name} » n'est pas en saisie");
        var mot = bloc.Text![selection.Debut..selection.Fin];
        Assert.Equal(mot, vm.MotFautif);
        Assert.True(vm.ModeOrthographe && vm.RechercheVisible, "la barre n'est pas en mode orthographe");
        return (mot, bloc);
    }

    /// <summary>Le PDF exporté, ses dates et son identifiant retirés : ce qui reste ne dépend que de la page.</summary>
    private static string Pdf(MainWindowViewModel vm)
    {
        var chemin = Path.Combine(Path.GetTempPath(), $"n25-{Guid.NewGuid():N}.pdf");
        try
        {
            vm.ExportPdf(chemin);
            var brut = Encoding.Latin1.GetString(File.ReadAllBytes(chemin));
            brut = Regex.Replace(brut, @"/(CreationDate|ModDate)\s*\([^)]*\)", "/$1()");
            // Le préfixe d'un sous-ensemble de police est tiré au hasard à chaque export.
            brut = Regex.Replace(brut, @"/[A-Z]{6}\+", "/XXXXXX+");
            // Les métadonnées XMP portent un identifiant et des dates de plus.
            brut = Regex.Replace(brut, @"uuid:[0-9a-fA-F-]{36}", "uuid:");
            brut = Regex.Replace(brut, @"\d{4}-\d\d-\d\dT[0-9:.+\-Z]+", "date");
            return Regex.Replace(brut, @"/ID\s*\[[^\]]*\]", "/ID[]");
        }
        finally
        {
            File.Delete(chemin);
        }
    }

    // ------------------------------------------------------------------ la recette

    /// <summary>
    /// La recette du plan : avec le dictionnaire de test, « recieve » est souligné et « receive »
    /// non. Le trait est sous le mot, de la place de N3 de son premier caractère à celle qui le
    /// suit, sur la ligne de base de sa ligne ; au rouge de l'apparence ; dans l'<c>Overlay</c>.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N25")]
    public void Recieve_est_souligne_et_receive_non()
    {
        var bloc = Texte("We will recieve the letter, and receive it.");
        var (fenetre, vm) = Ouvrir(EnAnglais(bloc));
        try
        {
            var fautes = Fautes(fenetre);
            var trait = Assert.Single(Assert.Single(fautes.Blocs).Traits);
            Assert.Equal("recieve", trait.Faute.Mot);
            Assert.Equal(new[] { "recieve" }, Soulignes(fenetre, bloc));

            var compose = Compose(fenetre, bloc);
            var debut = compose.PointDe(8)!.Value;
            var fin = compose.PointDe(15)!.Value;
            Assert.True(Math.Abs(debut.X - trait.Gauche) <= Tolerance, $"le trait part de {trait.Gauche}, N3 met « r » en {debut.X}");
            Assert.True(Math.Abs(fin.X - trait.Droite) <= Tolerance, $"le trait finit en {trait.Droite}, N3 met l'espace en {fin.X}");
            Assert.True(Math.Abs(debut.Y - trait.LigneDeBase) <= Tolerance);
            Assert.True(FautesSoulignees.Axe(trait, 1) > trait.LigneDeBase, "le trait n'est pas sous la ligne de base");

            Assert.Equal("Overlay", (fautes.GetVisualParent() as Control)?.Name);
            var page = fenetre.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "PageRoot");
            Assert.Empty(page.GetVisualDescendants().OfType<FautesSoulignees>());
            Assert.Same(fenetre.FindResource("Aucun"), fautes.Encre);
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// <c>Rendu.Encre</c> de la page et le PDF sont les mêmes, vérification ouverte ou fermée, et
    /// sans correcteur : le trait ne s'imprime pas et n'entre pas dans ce que l'écran compare au PDF.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N25")]
    public void L_encre_de_la_page_et_le_PDF_ne_changent_pas_verification_ouverte_ou_fermee()
    {
        var bloc = Texte("We will recieve the lettr, and receive it.");
        var (fenetre, vm) = Rendu.Ouvrir(EnAnglais(bloc));
        try
        {
            Rendu.UneImage();
            var encreSans = Rendu.Encre(fenetre);
            var pdfSans = Pdf(vm);

            vm.Orthographe = new Orthographe(Dictionnaires());
            Rendu.UneImage();
            Assert.Equal(new[] { "recieve", "lettr" }, Soulignes(fenetre, bloc));
            Assert.Equal(encreSans.Pixels, Rendu.Encre(fenetre).Pixels);
            Assert.Equal(pdfSans, Pdf(vm));

            vm.VerifierPendantLaFrappe = false;
            Rendu.UneImage();
            Assert.Empty(Soulignes(fenetre, bloc));
            Assert.Equal(encreSans.Pixels, Rendu.Encre(fenetre).Pixels);
            Assert.Equal(pdfSans, Pdf(vm));
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// <c>F7</c> parcourt les fautes dans l'ordre de lecture de N20 : les pages dans leur ordre ; sur la
    /// page, de haut en bas puis de gauche à droite ; dans un texte, de gauche à droite. Chaque faute
    /// est sélectionnée en saisie, la page suivant, avec ses suggestions ; au bout, la vérification
    /// reprend au début et le dit. <c>Échap</c> ferme la barre et rend le focus au mot ;
    /// <c>Ctrl+F</c> la rouvre en recherche.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N25")]
    public void F7_parcourt_les_fautes_dans_l_ordre_de_lecture()
    {
        var gauche = Texte("the frst page and a wrng word", "Gauche", x: 40, y: 60, largeur: 240);
        var droite = Texte("thrd block", "Droite", x: 320, y: 60, largeur: 200);
        var bas = Texte("secnd block", "Bas", x: 40, y: 300, largeur: 240);
        var document = EnAnglais(bas, droite, gauche);
        var page2 = new Page();
        var suite = Texte("a lettr", "Suite");
        page2.Frames.Add(suite);
        document.Pages.Add(page2);
        var (fenetre, vm) = Ouvrir(document);
        try
        {
            F7(fenetre);
            var (mot, bloc) = Montree(vm);
            Assert.Equal(("frst", gauche), (mot, bloc));
            Assert.Contains("first", vm.SuggestionsDeLaFaute);
            Assert.Equal(vm.SuggestionsDeLaFaute[0], vm.SuggestionChoisie);
            Assert.True(Trouver<Grid>(Barre(fenetre), "Orthographe").IsEffectivelyVisible);
            Assert.False(Trouver<Grid>(Barre(fenetre), "Recherche").IsEffectivelyVisible);

            var vus = new List<(string, string?)> { (mot, bloc.Name) };
            for (var i = 0; i < 4; i++)
            {
                Rendu.Cliquer(Bouton(fenetre, "BoutonIgnorer"));
                Rendu.UneImage();
                var (suivant, ou) = Montree(vm);
                vus.Add((suivant, ou.Name));
            }

            Assert.Equal(new[] { ("frst", "Gauche"), ("wrng", "Gauche"), ("thrd", "Droite"), ("secnd", "Bas"), ("lettr", "Suite") },
                vus.Select(v => (v.Item1, v.Item2!)).ToArray());
            Assert.Same(page2, vm.CurrentPage);

            Rendu.Cliquer(Bouton(fenetre, "BoutonIgnorer"));
            Rendu.UneImage();
            Assert.Equal(("frst", gauche), Montree(vm));
            Assert.Same(document.Pages[0], vm.CurrentPage);
            Assert.Contains("repris au début", vm.Status);

            // Échap : la barre se ferme, le focus va au mot sélectionné, où l'on tape par-dessus.
            Rendu.Touche(fenetre, PhysicalKey.Escape);
            Rendu.UneImage();
            Assert.False(vm.RechercheVisible);
            Assert.False(vm.ModeOrthographe);
            var zone = Rendu.ZoneDeSaisie(fenetre, gauche)!;
            Assert.True(zone.IsFocused, "le mot n'a pas repris le focus");
            Assert.Equal((4, 8), (Math.Min(zone.SelectionStart, zone.SelectionEnd), Math.Max(zone.SelectionStart, zone.SelectionEnd)));

            // Ctrl+F rend la barre à la recherche.
            F7(fenetre);
            Assert.True(vm.ModeOrthographe);
            Rendu.Touche(fenetre, PhysicalKey.F, RawInputModifiers.Control);
            Rendu.UneImage();
            Assert.False(vm.ModeOrthographe);
            Assert.True(Trouver<TextBox>(Barre(fenetre), "TexteCherche").IsEffectivelyVisible);
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// <i>Remplacer</i> fait un pas : le mot fautif prend la suggestion choisie — la première,
    /// d'emblée —, puis la faute suivante est montrée. <c>F7</c> donne le focus à la liste des
    /// suggestions, où <c>Entrée</c> remplace ; le bouton fait de même. Une seule annulation rend le
    /// texte d'avant chaque remplacement.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N25")]
    public void Remplacer_fait_un_pas_et_passe_a_la_faute_suivante()
    {
        var bloc = Texte("We will recieve the lettr");
        var (fenetre, vm) = Ouvrir(EnAnglais(bloc));
        try
        {
            var avant = DocumentSerializer.ToJson(vm.Document);
            F7(fenetre);
            Assert.Equal("recieve", Montree(vm).Mot);
            Assert.Equal("receive", vm.SuggestionChoisie);

            // Le focus est dans la liste des suggestions : Entrée y remplace.
            var focus = fenetre.FocusManager!.GetFocusedElement() as Control;
            Assert.Equal("Suggestions", focus?.Name);
            Rendu.Touche(fenetre, PhysicalKey.Enter);
            Rendu.UneImage();
            Assert.Equal("We will receive the lettr", bloc.Text);
            Assert.Equal("lettr", Montree(vm).Mot);
            Assert.Equal(new[] { "lettr" }, Soulignes(fenetre, bloc));

            // La suggestion choisie dans la liste, et non forcément la première.
            var choisie = vm.SuggestionsDeLaFaute.Last();
            vm.SuggestionChoisie = choisie;
            Rendu.Cliquer(Bouton(fenetre, "BoutonRemplacerLaFaute"));
            Rendu.UneImage();
            Assert.Equal("We will receive the " + choisie, bloc.Text);
            Assert.Equal("", vm.MotFautif);
            Assert.Contains("terminée", vm.Status);

            fenetre.FocusManager!.ClearFocus();
            Rendu.UneImage();
            vm.UndoCommand.Execute(null);
            Rendu.Battre();
            Assert.Equal("We will receive the lettr", ((TextFrame)vm.Document.Pages[0].Frames[0]).Text);
            Rendu.UnSeulPas(vm, avant, "Remplacer");
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// <i>Ajouter au dictionnaire</i> écrit le mot dans les préférences, pour sa langue, et il survit
    /// à un redémarrage ; <i>Ignorer tout</i> ne vaut que pour la session.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N25")]
    public void Un_mot_ajoute_au_dictionnaire_survit_a_un_redemarrage_et_ignorer_tout_non()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "n25-preferences-" + Guid.NewGuid().ToString("N"));
        try
        {
            var bloc = Texte("frobnicate and frobnicate, then blorp blorp");
            var (fenetre, vm) = Ouvrir(EnAnglais(bloc), Preferences.Charger(dossier));
            try
            {
                Assert.Equal(new[] { "frobnicate", "frobnicate", "blorp", "blorp" }, Soulignes(fenetre, bloc));

                F7(fenetre);
                Assert.Equal("frobnicate", Montree(vm).Mot);
                Rendu.Cliquer(Bouton(fenetre, "BoutonAjouterAuDictionnaire"));
                Rendu.UneImage();
                Assert.Equal("blorp", Montree(vm).Mot);
                Assert.Equal(new[] { "blorp", "blorp" }, Soulignes(fenetre, bloc));

                Rendu.Cliquer(Bouton(fenetre, "BoutonIgnorerTout"));
                Rendu.UneImage();
                Assert.Equal("", vm.MotFautif);
                Assert.Empty(Soulignes(fenetre, bloc));
            }
            finally
            {
                fenetre.Close();
            }

            var ecrit = JsonNode.Parse(File.ReadAllText(Path.Combine(dossier, Preferences.NomDuFichier)))!;
            Assert.Equal("frobnicate", ecrit["DictionnaireDeLUtilisateur"]!["en-US"]![0]!.GetValue<string>());

            // Le redémarrage : des préférences relues, un correcteur neuf.
            var relu = Texte("frobnicate and frobnicate, then blorp blorp");
            var (apres, _) = Ouvrir(EnAnglais(relu), Preferences.Charger(dossier));
            try
            {
                Assert.Equal(new[] { "blorp", "blorp" }, Soulignes(apres, relu));
            }
            finally
            {
                apres.Close();
            }
        }
        finally
        {
            if (Directory.Exists(dossier)) Directory.Delete(dossier, recursive: true);
        }
    }

    /// <summary>
    /// Au clic droit sur une faute en saisie, les suggestions viennent en tête du menu du texte
    /// (A12), puis <i>Ignorer tout</i> et <i>Ajouter au dictionnaire</i>, avant Couper ; une suggestion
    /// remplace le mot en un pas, le curseur après lui. Sur un mot juste, le menu du texte est celui
    /// d'A12.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N25")]
    public void Au_clic_droit_sur_une_faute_ses_suggestions_viennent_en_tete_du_menu()
    {
        var bloc = Texte("We will recieve the letter");
        var (fenetre, vm) = Ouvrir(EnAnglais(bloc));
        try
        {
            var avant = DocumentSerializer.ToJson(vm.Document);
            var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc, dx: 200);

            string?[] Entrees(ContextMenu menu) =>
                menu.Items.OfType<Control>().Where(e => e.IsVisible).Select(e => e is MenuItem m ? m.Header?.ToString() : "—").ToArray();

            // Sur un mot juste, le menu du texte d'A12, sans suggestion.
            var juste = Rendu.CliquerDroit(fenetre, Rendu.BordDuCaractere(zone, 22));
            Assert.Equal("Co_uper", Entrees(juste)[0]);
            juste.Close();
            Rendu.UneImage();

            var menu = Rendu.CliquerDroit(fenetre, Rendu.BordDuCaractere(zone, 11));
            var entrees = Entrees(menu);
            Assert.Equal("receive", entrees[0]);
            var couper = Array.IndexOf(entrees, "Co_uper");
            Assert.Equal(new[] { "_Ignorer tout", "Ajouter au _dictionnaire", "—" }, entrees[(couper - 3)..couper]);
            Assert.True(bloc.IsEditing, "le clic droit a refermé la saisie");

            Rendu.Cliquer(menu.Items.OfType<MenuItem>().First(m => m.IsVisible));
            Rendu.UneImage();
            Assert.Equal("We will receive the letter", bloc.Text);
            Assert.True(bloc.IsEditing);
            Assert.Equal(15, zone.CaretIndex);
            Assert.Empty(Soulignes(fenetre, bloc));

            fenetre.FocusManager!.ClearFocus();
            Rendu.UneImage();
            Rendu.UnSeulPas(vm, avant, "une suggestion du menu");
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// Le mot qu'on tape, le curseur à son bout, n'est souligné qu'une fois quitté — comme dans
    /// Publisher, qui attend l'espace. La case « Vérifier pendant la frappe », au menu Edition, efface
    /// tout et s'enregistre.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N25")]
    public void Le_mot_qu_on_tape_se_souligne_une_fois_quitte_et_la_case_ferme_tout()
    {
        var bloc = Texte("the letter");
        var (fenetre, vm) = Ouvrir(EnAnglais(bloc));
        try
        {
            Rendu.EntrerEnSaisie(fenetre, vm, bloc, dx: 300);
            Assert.Equal(10, vm.SelectionDuTexte!.Curseur);

            Rendu.Taper(fenetre, " wrdo");
            Rendu.UneImage();
            Assert.Equal("the letter wrdo", bloc.Text);
            Assert.Empty(Soulignes(fenetre, bloc));

            Rendu.Taper(fenetre, " ");
            Rendu.UneImage();
            Assert.Equal(new[] { "wrdo" }, Soulignes(fenetre, bloc));

            var entree = fenetre.GetLogicalDescendants().OfType<MenuItem>().Single(m => m.Name == "MenuVerifierPendantLaFrappe");
            Assert.True(entree.IsChecked);
            vm.VerifierPendantLaFrappe = false;
            Rendu.UneImage();
            Assert.False(entree.IsChecked);
            Assert.Empty(Fautes(fenetre).Blocs);
            Assert.False(vm.Preferences.VerifierPendantLaFrappe);

            vm.VerifierPendantLaFrappe = true;
            Rendu.UneImage();
            Assert.Equal(new[] { "wrdo" }, Soulignes(fenetre, bloc));
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// Le piège du plan : le soulignement d'une faute dans un bloc tourné tourne avec lui. Le trait
    /// est mené par la transformation du contrôle qui dessine le bloc — zoom et rotation compris — et
    /// se voit, rouge, sous le mot ; une ligne qui sort du bloc ne montre pas le sien.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N25")]
    public void Le_trait_d_un_bloc_tourne_tourne_avec_lui()
    {
        var tourne = Texte("a wrng word", "Tourne", x: 100, y: 100, largeur: 200, hauteur: 60, rotation: 30);
        var coupe = Texte("the line\nthe lne", "Coupe", x: 100, y: 300, largeur: 200, hauteur: 22);
        var (fenetre, vm) = Ouvrir(EnAnglais(tourne, coupe));
        try
        {
            vm.Zoom = 2 / Units.PointToDip;
            Rendu.UneImage();
            var fautes = Fautes(fenetre);
            var bloc = fautes.Blocs.Single(b => ReferenceEquals(b.Compose.Bloc, tourne));
            Assert.Equal(Compose(fenetre, tourne).TransformToVisual(fautes), bloc.Transformation);

            var trait = Assert.Single(bloc.Traits);
            var milieu = new Point((trait.Gauche + trait.Droite) / 2, FautesSoulignees.Axe(trait, 0.5))
                .Transform(bloc.Transformation);
            var auMilieu = fautes.TranslatePoint(milieu, fenetre)!.Value;

            // Les pixels du rouge de l'apparence autour d'un point de la fenêtre : le rouge et le bleu
            // très écartés, quel que soit l'ordre des canaux ; le noir du texte et le blanc de la page
            // ont les trois canaux égaux.
            int Rouges(Rendu.Image image, Point point)
            {
                var n = 0;
                for (var y = (int)point.Y - 4; y <= (int)point.Y + 4; y++)
                for (var x = (int)point.X - 4; x <= (int)point.X + 4; x++)
                {
                    if (x < 0 || y < 0 || x >= image.Largeur || y >= image.Hauteur) continue;
                    var i = (y * image.Largeur + x) * 4;
                    if (Math.Abs(image.Pixels[i] - image.Pixels[i + 2]) > 60) n++;
                }

                return n;
            }

            Rendu.UneImage();
            var image = Rendu.Composee(fenetre);
            Assert.True(Rouges(image, auMilieu) > 3, $"le trait du bloc tourné ne se voit pas en {auMilieu}");

            // La seconde ligne du bloc coupé sort de lui : sa faute a son trait, que le bloc découpe.
            var coupes = fautes.Blocs.Single(b => ReferenceEquals(b.Compose.Bloc, coupe));
            var cache = Assert.Single(coupes.Traits);
            Assert.Equal("lne", cache.Faute.Mot);
            var dehors = fautes.TranslatePoint(new Point((cache.Gauche + cache.Droite) / 2, FautesSoulignees.Axe(cache, 0.5))
                .Transform(coupes.Transformation), fenetre)!.Value;
            Assert.Equal(0, Rouges(image, dehors));

            vm.VerifierPendantLaFrappe = false;
            Rendu.UneImage();
            Assert.Equal(0, Rouges(Rendu.Composee(fenetre), auMilieu));
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// Chaque mot se vérifie dans sa langue (N9) : le fragment, sinon le bloc, sinon le document ; la
    /// langue du document changée, la page se souligne de nouveau. Un dictionnaire absent le dit une
    /// fois, à la barre d'état, et rien n'est souligné dans cette langue.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N25")]
    public void Chaque_mot_dans_sa_langue_et_un_dictionnaire_absent_le_dit_une_fois()
    {
        var bloc = Texte("le texte recieve le mott");
        bloc.Spans.Add(new StyleSpan { Start = 9, Length = 7, Langue = "en-US" });
        var document = Rendu.Feuille(bloc);
        Assert.Equal("fr-FR", document.Langue);
        var (fenetre, vm) = Ouvrir(document);
        try
        {
            Assert.Equal(new[] { "recieve", "mott" }, Soulignes(fenetre, bloc));

            vm.Document.Langue = "en-US";
            Rendu.UneImage();
            Assert.Equal(new[] { "le", "texte", "recieve", "le", "mott" }, Soulignes(fenetre, bloc));

            vm.Document.Langue = "de-DE";
            Rendu.UneImage();
            Assert.Equal(new[] { "recieve" }, Soulignes(fenetre, bloc));
            Assert.Contains("Aucun dictionnaire pour « de-DE »", vm.Status);
            Assert.Contains("hunspell-de", vm.Status);

            vm.Status = "rien";
            bloc.Text += " noch";
            Rendu.UneImage();
            Assert.Equal("rien", vm.Status);
            Assert.Equal(new[] { "recieve" }, Soulignes(fenetre, bloc));
        }
        finally
        {
            fenetre.Close();
        }
    }

    /// <summary>
    /// Sans correcteur — la fenêtre d'un test, le banc —, rien n'est vérifié : le contrôle des fautes
    /// n'entre pas dans l'<c>Overlay</c>, et <c>F7</c> le dit.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N25")]
    public void Sans_correcteur_rien_n_est_souligne_et_F7_le_dit()
    {
        var bloc = Texte("We will recieve the letter");
        var (fenetre, vm) = Rendu.Ouvrir(EnAnglais(bloc));
        try
        {
            Rendu.UneImage();
            Assert.False(vm.VerificationActive);
            Assert.Empty(fenetre.GetVisualDescendants().OfType<FautesSoulignees>());

            F7(fenetre);
            Assert.False(vm.RechercheVisible);
            Assert.Contains("pas de correcteur", vm.Status);
        }
        finally
        {
            fenetre.Close();
        }
    }
}
