using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
using Xunit.Abstractions;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Puces et numérotation — le poste N23 (fiche 23, b12), sur la vraie fenêtre. La liste « Puces et
/// numéros » de la section Paragraphe du dock, où mène <i>Texte ▸ Puces et numéros…</i>, met en
/// liste les paragraphes visés — ceux de la sélection en saisie, le bloc hors saisie (R6) — ; la
/// marque se dessine au retrait de première ligne, à l'écran comme au PDF ; Entrée dans un élément
/// continue la liste, et dans un élément vide l'en sort. Le noyau a ses tests
/// (<c>MyPublisher.Tests.PucesEtNumerosTests</c>) ; la recette —
/// <c>Scripts/recettes/N23-puces-et-numeros.sh</c> — joue les deux par leur trait.
/// </summary>
[Trait("poste", "N23")]
public class PucesEtNumerosSurLaFenetreTests
{
    private readonly ITestOutputHelper _sortie;

    public PucesEtNumerosSurLaFenetreTests(ITestOutputHelper sortie) => _sortie = sortie;

    // ------------------------------------------------------------------ le banc du poste

    /// <summary>La précision que le plan demande à l'écran et au PDF.</summary>
    private const double Precision = 0.05;

    private const string TroisParagraphes = "Titre\nAlpha\nBeta";

    private static readonly ListeDeParagraphe Chiffres = ListeDeParagraphe.DesNumeros(FormatDeNumero.Chiffres);

    /// <summary>
    /// Trois paragraphes d'un mot, au fer à gauche, assez gros pour qu'un clic les vise : 16 pt, où
    /// « 1. » tient dans le retrait suspendu de 18 pt.
    /// </summary>
    private static TextFrame Bloc(string texte = TroisParagraphes) => new()
    {
        Name = "Liste", X = 100, Y = 100, Width = 360, Height = 200, Text = texte,
        FontFamily = "DejaVu Sans", FontSize = 16, Foreground = "#FF000000", Align = TextAlign.Left,
    };

    /// <summary>Le même, les numéros posés sur le deuxième paragraphe et ceux qui le suivent.</summary>
    private static TextFrame Numerote(string texte = TroisParagraphes)
    {
        var bloc = Bloc(texte);
        bloc.Paragraphs.Add(new ParagraphSpan
        {
            Start = 1, Length = Recalage.NombreDeParagraphes(texte) - 1, Liste = Chiffres,
            IndentLeft = Listes.RetraitSuspendu, IndentFirstLine = -Listes.RetraitSuspendu,
        });
        return bloc;
    }

    private static string Etat(MainWindowViewModel vm) => DocumentSerializer.ToJson(vm.Document);

    private static T Trouver<T>(Visual racine, string nom) where T : Control =>
        racine.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == nom)
        ?? throw new InvalidOperationException($"« {nom} » n'est pas dans la fenêtre");

    private static IReadOnlyList<ParagrapheEffectif> Effectifs(TextFrame bloc) =>
        ParagrapheEffectif.Resoudre(Recalage.NombreDeParagraphes(bloc.Text ?? ""), bloc.Paragraphs, bloc.Paragraph);

    private static string?[] Marques(TextFrame bloc) => Listes.Marques(Effectifs(bloc)).ToArray();

    private static TexteCompose Compose(Window fenetre, TextFrame bloc) =>
        fenetre.GetVisualDescendants().OfType<TexteCompose>().First(c => ReferenceEquals(c.Bloc, bloc) && c.IsEffectivelyVisible);

    private static GeometrieDuTexte Geometrie(Window fenetre, TextFrame bloc) =>
        Compose(fenetre, bloc).Geometrie() ?? throw new InvalidOperationException("pas de géométrie");

    /// <summary>
    /// Les marques que l'écran pose, ligne par ligne : leur texte, et l'abscisse de leur ancre sur la
    /// page — celle où le dessin pose le morceau —, lues sur la géométrie de la composition.
    /// </summary>
    private static List<(int Rang, string Texte, double X, double Y)> MarquesALEcran(Window fenetre, TextFrame bloc) =>
        Geometrie(fenetre, bloc).Lignes
            .SelectMany(posee => posee.Ligne.Morceaux.Select((m, i) => (m, i)).Where(p => p.m.EstUneMarque)
                .Select(p => (posee.Rang, p.m.Texte, bloc.X + posee.Origine + p.m.X + posee.Decalages[p.i],
                              bloc.Y + posee.LigneDeBase)))
            .ToList();

    /// <summary>La liste « Puces et numéros » du dock, la section Paragraphe ouverte.</summary>
    private static ComboBox ListeDuDock(Window fenetre, MainWindowViewModel vm)
    {
        vm.SectionParagrapheOuverte = true;
        Rendu.Battre();
        return Trouver<ComboBox>(fenetre, "ListeDuParagraphe");
    }

    /// <summary>Choisit une entrée de « Puces et numéros », comme on choisit dans une liste du dock, puis la quitte.</summary>
    private static void Choisir(Window fenetre, MainWindowViewModel vm, ChoixDeListe choix)
    {
        var liste = ListeDuDock(fenetre, vm);
        liste.Focus();
        Rendu.Battre();
        liste.SelectedItem = MainWindowViewModel.OptionsDeListe.Single(o => o.Choix == choix);
        Rendu.Battre();
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();
    }

    /// <summary>Pose le curseur avant le caractère <paramref name="indice"/> d'un clic sur la composition (N12, N21).</summary>
    private static TextBox CurseurAuClic(Window fenetre, MainWindowViewModel vm, TextFrame bloc, int indice)
    {
        var zone = Rendu.ZoneDeSaisie(fenetre, bloc) ?? Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.UneImage();
        Rendu.Cliquer(fenetre, Rendu.BordDuCaractere(zone, indice));
        Assert.True(bloc.IsEditing, "le clic dans le texte a refermé la saisie");
        Assert.Equal(new SelectionDuTexte(bloc, null, indice, indice, indice), vm.SelectionDuTexte);
        return zone;
    }

    /// <summary>Les mots de la première page du PDF, lus par « pdftotext -bbox » : leur bord gauche et leur ligne.</summary>
    private static Dictionary<string, (double XMin, double YMax)> MotsDuPdf(MainWindowViewModel vm)
    {
        var dossier = Path.Combine(Path.GetTempPath(), $"mypublisher-n23-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dossier);
        try
        {
            var pdf = Path.Combine(dossier, "liste.pdf");
            var html = Path.Combine(dossier, "liste.html");
            vm.ExportPdf(pdf);
            try
            {
                var demarrage = new ProcessStartInfo("pdftotext")
                {
                    ArgumentList = { "-bbox", "-f", "1", "-l", "1", pdf, html },
                    RedirectStandardError = true,
                };
                using var processus = Process.Start(demarrage)!;
                processus.WaitForExit(60_000);
                Assert.True(processus.ExitCode == 0, processus.StandardError.ReadToEnd());
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Assert.Fail("« pdftotext » est introuvable : sudo apt install poppler-utils.");
                throw;
            }

            return System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(html),
                    "<word xMin=\"([-\\d.]+)\" yMin=\"[-\\d.]+\" xMax=\"[-\\d.]+\" yMax=\"([-\\d.]+)\">([^<]*)</word>")
                .ToDictionary(m => m.Groups[3].Value,
                    m => (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                          double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
        }
        finally
        {
            Directory.Delete(dossier, recursive: true);
        }
    }

    // ------------------------------------------------------------------ la recette du plan

    /// <summary>
    /// La recette : trois paragraphes, la sélection du deuxième au troisième, « 1. 2. 3. » choisi au
    /// dock. Un seul écart, le retrait suspendu d'Office ; « 1. » et « 2. » au PDF au retrait de
    /// première ligne — le bord du texte —, « Alpha » et « Beta » au retrait gauche, 18 pt plus
    /// loin ; l'écran égal au PDF à 0,05 pt ; un pas d'annulation.
    /// </summary>
    [AvaloniaFact]
    public void Les_numeros_poses_sur_le_deuxieme_et_le_troisieme_au_pdf_comme_a_l_ecran_en_un_pas()
    {
        var bloc = Bloc();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.SelectionnerDuTexte(zone, 7, 14);
        Assert.Equal(new Plage(1, 2), vm.ParagraphesDeLaSelection);
        var avant = Etat(vm);

        Choisir(fenetre, vm, ChoixDeListe.Chiffres);

        var ecart = Assert.Single(bloc.Paragraphs);
        Assert.Equal((1, 2, Chiffres, 18.0, -18.0),
            (ecart.Start, ecart.Length, ecart.Liste, ecart.IndentLeft, ecart.IndentFirstLine));
        Assert.Null(bloc.Liste);
        Assert.Equal(new string?[] { null, "1.", "2." }, Marques(bloc));
        Assert.True(bloc.IsEditing, "la liste du dock a refermé la saisie");
        Assert.Equal(ChoixDeListe.Chiffres, vm.ListeVisee.Option?.Choix);
        Rendu.UneImage();

        // L'écran : la composition que la saisie montre (N21), lue sur sa géométrie.
        var bord = bloc.X + bloc.PaddingLeft;
        var marques = MarquesALEcran(fenetre, bloc);
        Assert.Equal([(1, "1."), (2, "2.")], marques.Select(m => (m.Rang, m.Texte)));
        var geometrie = Geometrie(fenetre, bloc);
        var alpha = bloc.X + geometrie.PointDe(6).X;
        var beta = bloc.X + geometrie.PointDe(12).X;
        _sortie.WriteLine($"écran : « 1. » à {marques[0].X:F4}, « 2. » à {marques[1].X:F4}, « Alpha » à {alpha:F4}, « Beta » à {beta:F4}");
        Assert.All(marques, m => Assert.InRange(m.X, bord - Precision, bord + Precision));
        Assert.InRange(alpha, bord + 18 - Precision, bord + 18 + Precision);
        Assert.InRange(beta, bord + 18 - Precision, bord + 18 + Precision);
        Assert.InRange(bloc.X + geometrie.PointDe(0).X, bord - Precision, bord + Precision);

        // Le PDF : les mots que pdftotext y lit, au même point que l'écran.
        var mots = MotsDuPdf(vm);
        _sortie.WriteLine($"PDF : « 1. » à {mots["1."].XMin:F4}, « 2. » à {mots["2."].XMin:F4}, " +
                          $"« Alpha » à {mots["Alpha"].XMin:F4}, « Beta » à {mots["Beta"].XMin:F4}");
        Assert.InRange(mots["1."].XMin, marques[0].X - Precision, marques[0].X + Precision);
        Assert.InRange(mots["2."].XMin, marques[1].X - Precision, marques[1].X + Precision);
        Assert.InRange(mots["Alpha"].XMin, alpha - Precision, alpha + Precision);
        Assert.InRange(mots["Beta"].XMin, beta - Precision, beta + Precision);
        Assert.InRange(mots["Titre"].XMin, bord - Precision, bord + Precision);
        Assert.False(mots.ContainsKey("3."), "le PDF numérote un paragraphe hors liste");
        // Chaque marque est sur la ligne de son élément.
        Assert.Equal(mots["Alpha"].YMax, mots["1."].YMax, 0.5);
        Assert.Equal(mots["Beta"].YMax, mots["2."].YMax, 0.5);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.IsType<TextFrame>(Rendu.UnSeulPas(vm, avant, "Puces et numéros, 1. 2. 3."));
        Assert.Empty(((TextFrame)vm.SingleSelected!).Paragraphs);
    }

    /// <summary>
    /// Entrée à la fin de l'élément 2 donne un élément « 3. » — le saut que le champ écrit, et
    /// l'écart que le recalage de Q1 allonge —, la saisie et le focus gardés ; la frappe qui suit
    /// s'y écrit ; le tout en un pas.
    /// </summary>
    [AvaloniaFact]
    public void Entree_a_la_fin_de_l_element_2_donne_un_element_3_en_un_pas()
    {
        var bloc = Numerote();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = CurseurAuClic(fenetre, vm, bloc, TroisParagraphes.Length);
        var avant = Etat(vm);

        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(TroisParagraphes + "\n", bloc.Text);
        Assert.Equal(new string?[] { null, "1.", "2.", "3." }, Marques(bloc));
        var ecart = Assert.Single(bloc.Paragraphs);
        Assert.Equal((1, 3), (ecart.Start, ecart.Length));
        Assert.True(zone.IsFocused, "Entrée a pris le focus à la zone de saisie");
        Rendu.UneImage();
        Assert.Equal([(1, "1."), (2, "2."), (3, "3.")], MarquesALEcran(fenetre, bloc).Select(m => (m.Rang, m.Texte)));

        Rendu.Taper(fenetre, "Gamma");
        Assert.Equal(TroisParagraphes + "\nGamma", bloc.Text);
        Assert.Equal(new string?[] { null, "1.", "2.", "3." }, Marques(bloc));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UnSeulPas(vm, avant, "Entrée dans un élément de liste");
    }

    /// <summary>
    /// Entrée dans un élément vide le sort de la liste au lieu d'insérer un saut : le texte ne
    /// change pas, le paragraphe n'a plus de marque, et le retrait suspendu que la liste lui avait
    /// donné est rendu. Une seconde Entrée, hors liste, écrit le saut. En un pas, avec la frappe.
    /// </summary>
    [AvaloniaFact]
    public void Entree_dans_l_element_vide_le_sort_de_la_liste()
    {
        var texte = TroisParagraphes + "\n";
        var bloc = Numerote(texte);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        Assert.Equal(new string?[] { null, "1.", "2.", "3." }, Marques(bloc));
        var zone = CurseurAuClic(fenetre, vm, bloc, texte.Length);
        var avant = Etat(vm);

        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(texte, bloc.Text);
        Assert.Equal(new string?[] { null, "1.", "2.", null }, Marques(bloc));
        var sorti = Effectifs(bloc)[3];
        Assert.Equal((0.0, 0.0), (sorti.IndentLeft, sorti.IndentFirstLine));
        var ecart = Assert.Single(bloc.Paragraphs);
        Assert.Equal((1, 2, Chiffres), (ecart.Start, ecart.Length, ecart.Liste));
        Assert.True(bloc.IsEditing && zone.IsFocused, "Entrée a refermé la saisie");
        Assert.Equal(texte.Length, zone.CaretIndex);
        Rendu.UneImage();
        Assert.Equal([(1, "1."), (2, "2.")], MarquesALEcran(fenetre, bloc).Select(m => (m.Rang, m.Texte)));

        // Hors liste, Entrée écrit le saut, comme avant le poste.
        Rendu.Touche(fenetre, PhysicalKey.Enter);
        Assert.Equal(texte + "\n", bloc.Text);
        Assert.Equal(new string?[] { null, "1.", "2.", null, null }, Marques(bloc));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UnSeulPas(vm, avant, "Entrée dans un élément vide");
    }

    // ------------------------------------------------------------------ le dock et le menu

    /// <summary>
    /// <i>Texte ▸ Puces et numéros…</i> mène au dock, même replié, à la liste « Puces et numéros »,
    /// sans pas ; l'entrée est grisée sans bloc de texte, comme <i>Paragraphe…</i>.
    /// </summary>
    [AvaloniaFact]
    public void Le_menu_Puces_et_numeros_mene_a_la_liste_du_dock_sans_pas()
    {
        var bloc = Bloc();
        var carre = new ShapeFrame { Name = "Carré", X = 500, Y = 400, Width = 60, Height = 60 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, carre));
        Assert.False(vm.PucesEtNumerosCommand.CanExecute(null));
        vm.SelectOnly(carre);
        Rendu.Battre();
        Assert.False(vm.PucesEtNumerosCommand.CanExecute(null));

        vm.SelectOnly(bloc);
        Rendu.Battre();
        Assert.True(vm.PucesEtNumerosCommand.CanExecute(null));
        vm.SectionParagrapheOuverte = false;
        vm.ReplierLesPanneauxCommand.Execute(null);
        Rendu.Battre();
        Assert.False(vm.DockVisible);
        var avant = Etat(vm);

        foreach (var libelle in new[] { "_Texte", "P_uces et numéros…" })
        {
            Rendu.Cliquer(fenetre.GetLogicalDescendants().OfType<MenuItem>().Single(m => Equals(m.Header, libelle)));
        }

        Rendu.Battre(8);
        Assert.True(vm.DockVisible, "le dock n'est pas rouvert");
        Assert.True(vm.SectionParagrapheOuverte, "la section Paragraphe n'est pas ouverte");
        var liste = Trouver<ComboBox>(fenetre, "ListeDuParagraphe");
        var focus = fenetre.FocusManager!.GetFocusedElement() as Visual;
        Assert.True(focus is not null && (ReferenceEquals(focus, liste) || liste.IsVisualAncestorOf(focus)),
            $"le focus n'est pas à « Puces et numéros » ({focus})");

        fenetre.FocusManager.ClearFocus();
        Rendu.Battre();
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "l'entrée « … » a empilé un pas");
    }

    /// <summary>
    /// Hors saisie, la liste se pose sur le bloc, comme les autres réglages du paragraphe (R6) :
    /// des puces sur chaque paragraphe, le retrait suspendu au bloc, ses écarts laissés ; un pas.
    /// Le champ de la puce se montre, et le premier numéro se cache.
    /// </summary>
    [AvaloniaFact]
    public void Hors_saisie_les_puces_se_posent_sur_le_bloc_en_un_pas()
    {
        var bloc = Bloc();
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 0, Length = 1, Align = TextAlign.Center });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        Assert.Equal(ChoixDeListe.Aucune, vm.ListeVisee.Option?.Choix);
        var avant = Etat(vm);

        Choisir(fenetre, vm, ChoixDeListe.Puces);

        Assert.Equal(ListeDeParagraphe.DesPuces(), bloc.Liste);
        Assert.Equal((18.0, -18.0), (bloc.IndentLeft, bloc.IndentFirstLine));
        Assert.Equal((0, 1, TextAlign.Center), (bloc.Paragraphs[0].Start, bloc.Paragraphs[0].Length, bloc.Paragraphs[0].Align));
        Assert.Equal(new string?[] { "•", "•", "•" }, Marques(bloc));
        Rendu.UneImage();
        Assert.Equal(["•", "•", "•"], MarquesALEcran(fenetre, bloc).Select(m => m.Texte));
        Assert.True(Trouver<TextBox>(fenetre, "PuceDuParagraphe").IsEffectivelyVisible, "la puce ne se montre pas");
        Assert.False(Trouver<ChampNumerique>(fenetre, "PremierNumero").IsEffectivelyVisible, "le premier numéro se montre pour des puces");

        Rendu.UnSeulPas(vm, avant, "Puces sur le bloc");
    }

    /// <summary>
    /// Le caractère de la puce se tape au dock : « → » remplace « • » sur les paragraphes visés, en
    /// un pas ; la marque suit à l'écran.
    /// </summary>
    [AvaloniaFact]
    public void La_puce_se_tape_au_dock()
    {
        var bloc = Bloc();
        bloc.Liste = ListeDeParagraphe.DesPuces();
        bloc.IndentLeft = 18;
        bloc.IndentFirstLine = -18;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        vm.SectionParagrapheOuverte = true;
        Rendu.Battre();
        var avant = Etat(vm);

        var puce = Trouver<TextBox>(fenetre, "PuceDuParagraphe");
        Assert.Equal("•", puce.Text);
        puce.Focus();
        Rendu.Battre();
        puce.SelectAll();
        Rendu.Taper(fenetre, "→");
        fenetre.FocusManager!.ClearFocus();
        Rendu.Battre();

        Assert.Equal(ListeDeParagraphe.DesPuces("→"), bloc.Liste);
        Rendu.UneImage();
        Assert.Equal(["→", "→", "→"], MarquesALEcran(fenetre, bloc).Select(m => m.Texte));
        Rendu.UnSeulPas(vm, avant, "Puce tapée au dock");
    }

    /// <summary>
    /// Le premier numéro, saisi au dock pendant la saisie, fait repartir la suite au paragraphe du
    /// curseur : « Beta » passe à « 5. », « Alpha » reste « 1. » ; un pas à lui (Q25).
    /// </summary>
    [AvaloniaFact]
    public void Le_premier_numero_saisi_au_dock_fait_repartir_la_suite_au_curseur()
    {
        var bloc = Numerote();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        CurseurAuClic(fenetre, vm, bloc, 14);
        Assert.Equal(new Plage(2, 1), vm.ParagraphesDeLaSelection);
        vm.SectionParagrapheOuverte = true;
        Rendu.Battre();
        var champ = Trouver<ChampNumerique>(fenetre, "PremierNumero");
        Assert.True(champ.IsEffectivelyVisible, "le premier numéro ne se montre pas");
        Assert.Equal((decimal?)2m, champ.Value);
        var avant = Etat(vm);

        Rendu.Saisir(fenetre, champ, "5");

        Assert.Equal(new string?[] { null, "1.", "5." }, Marques(bloc));
        Assert.True(bloc.IsEditing, "le premier numéro a refermé la saisie");
        Rendu.UneImage();
        Assert.Equal(["1.", "5."], MarquesALEcran(fenetre, bloc).Select(m => m.Texte));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UnSeulPas(vm, avant, "Premier numéro");
    }

    /// <summary>
    /// L'état mixte : une sélection qui va d'un élément numéroté à un élément à puces ne montre
    /// aucune entrée, ni la puce ni le premier numéro ; le curseur dans l'un montre le sien. Lire
    /// n'écrit rien.
    /// </summary>
    [AvaloniaFact]
    public void L_etat_mixte_ne_montre_aucune_entree()
    {
        var bloc = Numerote();
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 2, Length = 1, Liste = ListeDeParagraphe.DesPuces() });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        Assert.Equal(new string?[] { null, "1.", "•" }, Marques(bloc));
        var avant = Etat(vm);

        var zone = CurseurAuClic(fenetre, vm, bloc, 8);
        var liste = ListeDuDock(fenetre, vm);
        Assert.Equal(ChoixDeListe.Chiffres, (liste.SelectedItem as OptionDeListe)?.Choix);
        Assert.True(Trouver<ChampNumerique>(fenetre, "PremierNumero").IsEffectivelyVisible);

        Rendu.SelectionnerDuTexte(zone, 8, 14);
        Assert.Equal(new Plage(1, 2), vm.ParagraphesDeLaSelection);
        Assert.Null(liste.SelectedItem);
        Assert.Null(vm.ListeVisee.Option);
        Assert.False(Trouver<ChampNumerique>(fenetre, "PremierNumero").IsEffectivelyVisible);
        Assert.False(Trouver<TextBox>(fenetre, "PuceDuParagraphe").IsEffectivelyVisible);

        CurseurAuClic(fenetre, vm, bloc, 13);
        Assert.Equal(ChoixDeListe.Puces, (liste.SelectedItem as OptionDeListe)?.Choix);
        Assert.Equal("•", Trouver<TextBox>(fenetre, "PuceDuParagraphe").Text);

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal(avant, Etat(vm));
        Assert.False(vm.CanUndo, "lire la liste a écrit dans le document");
    }

    // ------------------------------------------------------------------ la marque sans position

    /// <summary>
    /// La marque n'a pas de position dans le texte (N3) : un clic sur elle pose le curseur au
    /// début de son paragraphe, et le point du début du paragraphe est après elle. Les caractères
    /// spéciaux (N22) n'y dessinent rien : autant de ¶ que de paragraphes, chacun à sa place.
    /// </summary>
    [AvaloniaFact]
    public void Un_clic_sur_la_marque_pose_le_curseur_au_debut_du_paragraphe()
    {
        var bloc = Numerote();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        CurseurAuClic(fenetre, vm, bloc, 2);
        Rendu.UneImage();

        var marque = MarquesALEcran(fenetre, bloc).Single(m => m.Texte == "2.");
        Rendu.Cliquer(fenetre, Rendu.SurLaPage(fenetre, marque.X + 4, marque.Y - 6));
        Assert.True(bloc.IsEditing);
        Assert.Equal(new SelectionDuTexte(bloc, null, 12, 12, 12), vm.SelectionDuTexte);

        var geometrie = Geometrie(fenetre, bloc);
        Assert.Equal(12, geometrie.IndexSous(new Point(marque.X - bloc.X + 4, marque.Y - bloc.Y - 6)));
        Assert.True(geometrie.PointDe(12).X > marque.X - bloc.X + 10, "le début du paragraphe n'est pas après sa marque");

        vm.CaracteresSpeciauxVisibles = true;
        Rendu.UneImage();
        var speciaux = fenetre.GetVisualDescendants().OfType<CaracteresSpeciaux>().Single();
        var caracteres = speciaux.Blocs.Single(b => ReferenceEquals(b.Compose.Bloc, bloc)).Caracteres;
        var fins = caracteres.Where(c => c.Sorte == CaractereSpecial.FinDeParagraphe).ToList();
        Assert.Equal(3, fins.Count);
        Assert.All(caracteres, c => Assert.InRange(c.Index, 0, TroisParagraphes.Length));
        Assert.All(fins, c => Assert.Equal(geometrie.PointDe(c.Index).X, c.Point.X, 0.01));
    }

    /// <summary>
    /// Le compte de I9 voit les mêmes lignes que la composition de la planche : les marques en
    /// font partie, un élément sans retrait suspendu poussant son texte au-delà d'elle.
    /// </summary>
    [AvaloniaFact]
    public void Le_compte_des_lignes_en_excedent_voit_les_lignes_de_la_planche()
    {
        var bloc = Bloc("Un deux trois quatre cinq six sept huit neuf dix onze douze treize");
        bloc.Width = 200;
        bloc.Height = 60;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var sans = vm.LignesEnExcedent;

        Choisir(fenetre, vm, ChoixDeListe.Romains);
        Rendu.UneImage();

        var lignes = Geometrie(fenetre, bloc).Lignes.Select(l => l.Ligne).ToList();
        var hauteur = bloc.Height - bloc.PaddingTop - bloc.PaddingBottom;
        var attendu = Compositeur.LignesEnExcedent(lignes, hauteur, bloc.PaddingBottom, bloc.VerticalAlign);
        _sortie.WriteLine($"lignes en excédent : {sans} sans liste, {vm.LignesEnExcedent} avec, {attendu} sur la planche");
        Assert.Equal("i.", lignes[0].Morceaux[0].Texte);
        Assert.Equal(attendu, vm.LignesEnExcedent);
        Assert.True(vm.LignesEnExcedent > 0);
    }
}
