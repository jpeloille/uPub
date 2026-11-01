using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Taper dans une case, sur la page — le poste N27 (fiche 23, g2 à g4). Revient sur un choix écrit
/// en C9 : une case ne se remplissait qu'au dock. Publisher y tape au clic, et <c>Tab</c> passe de
/// case en case.
///
/// Les gestes sont ceux du banc (<see cref="Rendu"/>, poste N1). Une case se dessine par un bloc de
/// texte que le tableau reconstruit à chaque changement : le contrôle qui la dessine est retrouvé
/// par les fragments qu'il emprunte à la case, et ce que la planche y pose est relu sur lui.
/// </summary>
public class SaisieDansUneCaseTests
{
    // ------------------------------------------------------------------ le banc du poste

    /// <summary>Un demi-pixel au zoom 100 % : l'écart toléré entre le modèle et l'écran.</summary>
    private const double Seuil = 0.5;

    /// <summary>
    /// Une bande de trois cases de 120 pt, comme celles des fiches ATR, en (60, 80) : une ligne de
    /// <paramref name="hauteur"/> pt, chaque case encadrée, ou <paramref name="lignes"/> lignes.
    /// </summary>
    private static TableFrame Bande(int lignes = 1, double rotation = 0, double hauteur = 30, params string[] textes)
    {
        var tableau = new TableFrame
        {
            Name = "Bande", X = 60, Y = 80, Width = 360, Height = hauteur * lignes, Rotation = rotation,
            FontFamily = "DejaVu Sans", FontSize = 12, Foreground = "#FF000000",
        };
        for (var colonne = 0; colonne < 3; colonne++) tableau.Columns.Add(new TableColumn(120));
        for (var ligne = 0; ligne < lignes; ligne++) tableau.Rows.Add(new TableRow(hauteur));
        for (var ligne = 0; ligne < lignes; ligne++)
        {
            for (var colonne = 0; colonne < 3; colonne++)
            {
                var rang = ligne * 3 + colonne;
                tableau.Cells.Add(new TableCell
                {
                    Row = ligne, Column = colonne, Stroke = "#FF6B7280", StrokeThickness = 1,
                    Text = rang < textes.Length ? textes[rang] : "",
                });
            }
        }

        return tableau;
    }

    private static TableCell Case(TableFrame tableau, int ligne, int colonne) =>
        tableau.CaseEn(ligne, colonne) ?? throw new InvalidOperationException($"pas de case en {ligne}, {colonne}");

    /// <summary>Le contrôle qui dessine la case, retrouvé par les fragments qu'il lui emprunte.</summary>
    private static TexteCompose Compose(Window fenetre, TableCell case_) =>
        Nomme<Panel>(fenetre, "PageRoot").GetVisualDescendants().OfType<TexteCompose>()
            .Single(c => c.Bloc is { } bloc && ReferenceEquals(bloc.Spans, case_.Spans));

    private static GeometrieDuTexte Geometrie(Window fenetre, TableCell case_) =>
        Compose(fenetre, case_).Geometrie() ?? throw new InvalidOperationException($"la case {case_.Label} n'a pas de géométrie");

    private static T Nomme<T>(Window fenetre, string nom) where T : Control =>
        fenetre.GetVisualDescendants().OfType<T>().Single(c => c.Name == nom);

    /// <summary>
    /// Un point du repère d'une case, dans la fenêtre, mené par le modèle : le coin de la case dans
    /// le tableau, puis le repère du tableau, rotation comprise, puis ceux des groupes qui le portent.
    /// </summary>
    private static Point ParLeModele(Window fenetre, IReadOnlyList<Frame> chemin, TableCell case_, double x, double y)
    {
        var tableau = (TableFrame)chemin[^1];
        var cadre = tableau.CadreDe(case_)!.Value;
        var (px, py) = MainWindowViewModel.VersLaPage(chemin, cadre.X + x, cadre.Y + y);
        return Rendu.SurLaPage(fenetre, px, py);
    }

    /// <summary>Le point visé d'un caractère, dans le repère de la case : au tiers de sa chasse, à mi-hauteur de sa ligne.</summary>
    private static Point Caractere(GeometrieDuTexte geometrie, int indice)
    {
        var bord = geometrie.CurseurDe(indice);
        var chasse = geometrie.CurseurDe(indice + 1).X - bord.X;
        return new Point(bord.X + chasse / 3, bord.Y + bord.Height / 2);
    }

    private static void Proche(Point attendu, Point lu, string quoi) =>
        Assert.True(Math.Abs(attendu.X - lu.X) < Seuil && Math.Abs(attendu.Y - lu.Y) < Seuil,
            $"{quoi} : {lu} à l'écran, {attendu} par le modèle");

    /// <summary>Le champ du dock où la case se tape aussi (C9) : celui de la section Tableau, lié à la case.</summary>
    private static TextBox ChampDuDock(Window fenetre, TableCell case_) =>
        fenetre.GetVisualDescendants().OfType<TextBox>()
            .Single(champ => champ.Classes.Contains("cellText") && ReferenceEquals(champ.DataContext, case_));

    /// <summary>Les mots de la première page du PDF, lus par « pdftotext -bbox » : leur texte et leur boîte.</summary>
    private static List<(string Mot, double X0, double Y0, double X1, double Y1)> MotsDuPdf(string pdf)
    {
        var html = Path.ChangeExtension(pdf, ".html");
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

        static double Lire(Match m, string nom) => double.Parse(m.Groups[nom].Value, CultureInfo.InvariantCulture);
        return Regex.Matches(File.ReadAllText(html),
                "<word xMin=\"(?<x0>[-\\d.]+)\" yMin=\"(?<y0>[-\\d.]+)\" xMax=\"(?<x1>[-\\d.]+)\" yMax=\"(?<y1>[-\\d.]+)\">(?<mot>[^<]*)</word>")
            .Select(m => (m.Groups["mot"].Value, Lire(m, "x0"), Lire(m, "y0"), Lire(m, "x1"), Lire(m, "y1")))
            .ToList();
    }

    // ------------------------------------------------------------------ la recette

    /// <summary>
    /// La recette du plan : un clic dans la 2ᵉ case d'une bande y pose le curseur, le tableau restant
    /// la sélection et le bloc visé ; la frappe « 12 » s'écrit dans la case, se voit au dock, fait un
    /// pas ; le PDF porte « 12 » dans la case ; <c>Échap</c> rend le tableau.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N27")]
    public void Un_clic_dans_la_deuxieme_case_puis_12_le_PDF_porte_12_dans_la_case()
    {
        var tableau = Bande(textes: ["Vitesse", "", "kt"]);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        var avant = DocumentSerializer.ToJson(vm.Document);
        var deuxieme = Case(tableau, 0, 1);

        var zone = Rendu.EntrerDansLaCase(fenetre, vm, tableau, deuxieme);
        Assert.Same(tableau, Assert.Single(vm.Selection));
        Assert.Same(tableau, vm.BlocVise);
        Assert.Null(vm.TexteEnSaisie);
        Assert.True(Rendu.ZoneDeSaisie(fenetre, (TextFrame)zone.DataContext!) is null, "le champ de la case est dans PageRoot");
        Assert.Equal((tableau, deuxieme, 0, 0), (vm.SelectionDuTexte?.Bloc, vm.SelectionDuTexte?.Case,
            vm.SelectionDuTexte?.Debut, vm.SelectionDuTexte?.Fin));

        Rendu.Taper(fenetre, "12");
        Assert.Equal("12", deuxieme.Text);
        Assert.Equal(["Vitesse", "12", "kt"], tableau.Cells.Select(c => c.Text));
        Assert.Equal(2, zone.CaretIndex);
        Assert.Equal(2, vm.SelectionDuTexte?.Curseur);
        Assert.Equal("12", ChampDuDock(fenetre, deuxieme).Text);
        Assert.Equal("12", Compose(fenetre, deuxieme).Contenu);
        Assert.True(zone.IsFocused, "la frappe a fait perdre le focus au champ de la case");
        Assert.Same(tableau, Assert.Single(vm.Selection));

        // Le PDF porte « 12 » dans la deuxième case : entre ses bords, sur sa ligne.
        var dossier = Path.Combine(Path.GetTempPath(), $"mypublisher-n27-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dossier);
        try
        {
            var pdf = Path.Combine(dossier, "case.pdf");
            vm.ExportPdf(pdf);
            var mot = Assert.Single(MotsDuPdf(pdf), m => m.Mot == "12");
            Assert.InRange(mot.X0, tableau.X + 120, tableau.X + 240);
            Assert.InRange(mot.X1, tableau.X + 120, tableau.X + 240);
            Assert.InRange(mot.Y0, tableau.Y, tableau.Y + 30);
            Assert.InRange(mot.Y1, tableau.Y, tableau.Y + 30);
        }
        finally
        {
            Directory.Delete(dossier, recursive: true);
        }

        // Échap rend le tableau : la saisie se referme, le tableau reste pris.
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Null(vm.CaseEnSaisie);
        Assert.Null(vm.SelectionDuTexte);
        Assert.Null(Rendu.ZoneDeLaCase(fenetre));
        Assert.Same(tableau, Assert.Single(vm.Selection));

        var repris = Assert.IsType<TableFrame>(Rendu.UnSeulPas(vm, avant, "la frappe dans une case"));
        Assert.Equal(["Vitesse", "", "kt"], repris.Cells.Select(c => c.Text));
    }

    // ------------------------------------------------------------------ de case en case

    /// <summary>
    /// <c>Tab</c> passe à la case suivante, son texte sélectionné, et <c>Maj+Tab</c> à la précédente :
    /// la saisie reste dans le tableau, qui reste la sélection, le focus au champ. <c>Tab</c> dans la
    /// dernière case ajoute une ligne, en un pas, et mène à sa première case.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N27")]
    public void Tab_et_Maj_Tab_passent_de_case_en_case_et_Tab_dans_la_derniere_ajoute_une_ligne_en_un_pas()
    {
        var tableau = Bande(textes: ["Vitesse", "250", "kt"]);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        var (premiere, deuxieme, troisieme) = (Case(tableau, 0, 0), Case(tableau, 0, 1), Case(tableau, 0, 2));
        Rendu.EntrerDansLaCase(fenetre, vm, tableau, premiere);

        void Dans(TableCell case_, int debut, int fin, string quoi)
        {
            Assert.Same(case_, vm.CaseEnSaisie);
            Assert.Same(tableau, Assert.Single(vm.Selection));
            var zone = Rendu.ZoneDeLaCase(fenetre) ?? throw new InvalidOperationException($"{quoi} : aucune zone de saisie");
            Assert.True(zone.IsFocused, $"{quoi} : la zone de la case n'a pas le focus");
            Assert.Equal((case_, debut, fin), (vm.SelectionDuTexte?.Case, vm.SelectionDuTexte?.Debut, vm.SelectionDuTexte?.Fin));
        }

        Rendu.Touche(fenetre, PhysicalKey.Tab);
        Dans(deuxieme, 0, 3, "Tab");
        Assert.Equal(["Vitesse", "250", "kt"], tableau.Cells.Select(c => c.Text));

        Rendu.Touche(fenetre, PhysicalKey.Tab, RawInputModifiers.Shift);
        Dans(premiere, 0, 7, "Maj+Tab");

        // Maj+Tab dans la première case : rien, la saisie reste où elle est.
        Rendu.Touche(fenetre, PhysicalKey.Tab, RawInputModifiers.Shift);
        Dans(premiere, 0, 7, "Maj+Tab dans la première case");

        Rendu.Touche(fenetre, PhysicalKey.Tab);
        Rendu.Touche(fenetre, PhysicalKey.Tab);
        Dans(troisieme, 0, 2, "Tab, Tab");
        Assert.False(vm.CanUndo, "passer de case en case a fait un pas");

        // Tab dans la dernière case : une ligne de plus, en un pas, la saisie dans sa première case.
        var avant = DocumentSerializer.ToJson(vm.Document);
        Rendu.Touche(fenetre, PhysicalKey.Tab);
        Assert.Equal(2, tableau.Rows.Count);
        Assert.Equal(6, tableau.Cells.Count);
        Assert.Equal(60, tableau.Height, 6);
        var neuve = Case(tableau, 1, 0);
        Dans(neuve, 0, 0, "Tab dans la dernière case");
        Assert.Equal("#FF6B7280", neuve.Stroke);

        Rendu.Taper(fenetre, "Volets");
        Assert.Equal("Volets", neuve.Text);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Same(tableau, Assert.Single(vm.Selection));

        // Deux pas : la frappe dans la case neuve, puis la ligne.
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        var repris = Assert.IsType<TableFrame>(vm.SingleSelected);
        Assert.Equal(2, repris.Rows.Count);
        Assert.Equal("", repris.CaseEn(1, 0)?.Text);
        Rendu.UnSeulPas(vm, avant, "la ligne ajoutée par Tab");
    }

    /// <summary>
    /// Un pas par case : la frappe dans une case, <c>Tab</c>, la frappe dans la suivante — deux pas,
    /// que deux annulations défont l'un après l'autre. <c>Ctrl+Tab</c> met une tabulation dans la
    /// case, sans en sortir, dans le pas de la frappe.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N27")]
    public void Un_pas_par_case_et_Ctrl_Tab_met_une_tabulation()
    {
        var tableau = Bande(textes: ["Vitesse", "250", "kt"]);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        var depart = DocumentSerializer.ToJson(vm.Document);
        var (premiere, deuxieme) = (Case(tableau, 0, 0), Case(tableau, 0, 1));

        var zone = Rendu.EntrerDansLaCase(fenetre, vm, tableau, premiere, 80);
        Assert.Equal(7, zone.CaretIndex);
        Rendu.Taper(fenetre, " max");
        Rendu.Touche(fenetre, PhysicalKey.Tab, RawInputModifiers.Control);
        Assert.Equal("Vitesse max\t", premiere.Text);
        Assert.Same(premiere, vm.CaseEnSaisie);
        Assert.Equal(12, vm.SelectionDuTexte?.Curseur);
        var apresLaPremiere = DocumentSerializer.ToJson(vm.Document);

        Rendu.Touche(fenetre, PhysicalKey.Tab);
        Rendu.Taper(fenetre, "VNE");
        Assert.Equal("VNE", deuxieme.Text);
        Rendu.Touche(fenetre, PhysicalKey.Escape);

        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(apresLaPremiere, DocumentSerializer.ToJson(vm.Document));
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(depart, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, "plus de deux pas pour deux cases");
    }

    /// <summary>
    /// La saisie d'une case tient sa transaction comme celle d'un bloc (Q24, Q25) : <c>Ctrl+Z</c> sans
    /// rien à annuler garde la saisie et le focus, et la frappe suivante fait son pas ; <c>Ctrl+Z</c>
    /// après la frappe la défait, sans laisser de pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N27")]
    public void Ctrl_Z_dans_une_case_suit_la_transaction_de_la_saisie()
    {
        var tableau = Bande(textes: ["Vitesse", "", "kt"]);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        var avant = DocumentSerializer.ToJson(vm.Document);
        var deuxieme = Case(tableau, 0, 1);

        var zone = Rendu.EntrerDansLaCase(fenetre, vm, tableau, deuxieme);
        Rendu.Touche(fenetre, PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Same(deuxieme, vm.CaseEnSaisie);
        Assert.True(zone.IsFocused, "Ctrl+Z a pris le focus à la case");

        Rendu.Taper(fenetre, "12");
        Assert.Equal("12", deuxieme.Text);
        Assert.True(vm.PeutAnnuler, "la frappe dans la case n'est dans aucune transaction");

        Rendu.Touche(fenetre, PhysicalKey.Z, RawInputModifiers.Control);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, "Ctrl+Z après la frappe dans la case a laissé un pas");
    }

    /// <summary>
    /// Les flèches au bord du texte passent à la case voisine : <c>→</c> à la fin à la suivante, au
    /// début ; <c>←</c> au début à la précédente, à la fin ; <c>↓</c> sur la dernière ligne à la case
    /// du dessous, et <c>↑</c> sur la première à celle du dessus, à la même abscisse. Ailleurs, elles
    /// restent dans le texte.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N27")]
    public void Les_fleches_au_bord_du_texte_passent_a_la_case_voisine()
    {
        var tableau = Bande(2, textes: ["Vitesse", "250", "kt", "Volets", "180", "kt"]);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        var (l1c1, l1c2, l2c1) = (Case(tableau, 0, 0), Case(tableau, 0, 1), Case(tableau, 1, 0));

        // Au milieu du texte, → reste dans la case.
        var zone = Rendu.EntrerDansLaCase(fenetre, vm, tableau, l1c1, 80);
        Assert.Equal(7, zone.CaretIndex);
        Rendu.Touche(fenetre, PhysicalKey.ArrowLeft);
        Assert.Equal((l1c1, 6), (vm.CaseEnSaisie, vm.SelectionDuTexte?.Curseur));
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        Assert.Equal((l1c1, 7), (vm.CaseEnSaisie, vm.SelectionDuTexte?.Curseur));

        // → à la fin : la case suivante, au début.
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        Assert.Equal((l1c2, 0, 0), (vm.CaseEnSaisie, vm.SelectionDuTexte?.Debut, vm.SelectionDuTexte?.Fin));

        // ← au début : la précédente, à la fin.
        Rendu.Touche(fenetre, PhysicalKey.ArrowLeft);
        Assert.Equal((l1c1, 7, 7), (vm.CaseEnSaisie, vm.SelectionDuTexte?.Debut, vm.SelectionDuTexte?.Fin));

        // ↓ sur la dernière ligne : la case du dessous, à l'abscisse du curseur — après « Vitess ».
        Rendu.Touche(fenetre, PhysicalKey.ArrowLeft);
        var abscisse = Geometrie(fenetre, l1c1).PointDe(6).X;
        var attendue = Geometrie(fenetre, l2c1).SurLaLigne(0, abscisse).Position;
        Rendu.Touche(fenetre, PhysicalKey.ArrowDown);
        Assert.Equal((l2c1, attendue), (vm.CaseEnSaisie, vm.SelectionDuTexte?.Curseur));
        Assert.Equal(6, attendue);

        // ↑ sur la première ligne : la case du dessus.
        Rendu.Touche(fenetre, PhysicalKey.ArrowUp);
        Assert.Equal((l1c1, 6), (vm.CaseEnSaisie, vm.SelectionDuTexte?.Curseur));

        // ↑ dans la première ligne du tableau : la touche reste aux lignes composées (N29), au début du texte.
        Rendu.Touche(fenetre, PhysicalKey.ArrowUp);
        Assert.Equal((l1c1, 0), (vm.CaseEnSaisie, vm.SelectionDuTexte?.Curseur));
        Assert.Same(tableau, Assert.Single(vm.Selection));
        Assert.False(vm.CanUndo, "passer de case en case a fait un pas");
    }

    // ------------------------------------------------------------------ la page et le dock

    /// <summary>
    /// Le dock garde sa saisie de case (C9), et les deux ne se contredisent pas : ce qu'on tape sur
    /// la page se voit au dock, ce qu'on tape au dock se voit sur la page, la saisie de la case
    /// restant ouverte ; chacun fait son pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N27")]
    public void Ce_qu_on_tape_sur_la_page_se_voit_au_dock_et_inversement()
    {
        var tableau = Bande(textes: ["Vitesse", "", "kt"]);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        var deuxieme = Case(tableau, 0, 1);

        var zone = Rendu.EntrerDansLaCase(fenetre, vm, tableau, deuxieme);
        Rendu.Taper(fenetre, "250");
        var dock = ChampDuDock(fenetre, deuxieme);
        Assert.Equal("250", dock.Text);
        var apresLaPage = DocumentSerializer.ToJson(vm.Document);

        // Au dock : le texte suit sur la page, la saisie de la case reste ouverte.
        dock.Focus();
        Rendu.Battre();
        dock.CaretIndex = 3;
        Rendu.Taper(fenetre, " kt");
        Assert.Equal("250 kt", deuxieme.Text);
        Assert.Equal("250 kt", zone.Text);
        Assert.Equal("250 kt", Compose(fenetre, deuxieme).Contenu);
        Assert.Same(deuxieme, vm.CaseEnSaisie);
        Assert.Equal("250 kt", ((TextFrame)zone.DataContext!).Text);

        // Retour à la page : un clic dans la case y pose le curseur sur le texte venu du dock.
        var geometrie = Geometrie(fenetre, deuxieme);
        var vise = Caractere(geometrie, 4);
        Rendu.Cliquer(fenetre, ParLeModele(fenetre, [tableau], deuxieme, vise.X, vise.Y));
        zone = Rendu.ZoneDeLaCase(fenetre) ?? throw new InvalidOperationException("la saisie de la case s'est refermée");
        Assert.True(zone.IsFocused);
        Assert.Equal((deuxieme, 4), (vm.CaseEnSaisie, vm.SelectionDuTexte?.Curseur));
        Rendu.Taper(fenetre, "!");
        Assert.Equal("250 !kt", deuxieme.Text);
        Assert.Equal("250 !kt", dock.Text);
        Rendu.Touche(fenetre, PhysicalKey.Escape);

        // Trois pas : la page, le dock, la page.
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(apresLaPage, DocumentSerializer.ToJson(vm.Document));
    }

    /// <summary>
    /// Pendant qu'on tape dans une case, la barre et le dock visent le tableau, qui reste le bloc visé :
    /// le dock garde sa saisie de case (C9). Le clic droit dans la case ouvre le menu du texte (A12) —
    /// Couper, Copier, Coller, Format du bloc… —, la saisie et la sélection gardées : Copier, Couper
    /// et Coller jouent sur la case. <c>Ctrl+G</c> met le tableau entier en gras, en un pas, la saisie
    /// gardée : la mise en forme d'une case est à N28.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N27")]
    public void La_barre_le_dock_et_le_menu_du_texte_en_saisie_d_une_case()
    {
        var tableau = Bande(textes: ["Vitesse", "VNE 250", "kt"]);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        var deuxieme = Case(tableau, 0, 1);

        var zone = Rendu.EntrerDansLaCase(fenetre, vm, tableau, deuxieme);
        Assert.Same(tableau, Nomme<GabaritsGardes>(fenetre, "ReglagesDuBloc").Bloc);
        Assert.All(fenetre.GetVisualDescendants().OfType<GabaritsGardes>().Where(g => g.Bloc is not null),
            g => Assert.Same(tableau, g.Bloc));
        Assert.True(ChampDuDock(fenetre, deuxieme).IsEffectivelyVisible, "le dock ne montre plus la saisie de la case");

        Rendu.SelectionnerDuTexte(zone, 0, 3);
        Assert.Equal("VNE", zone.SelectedText);
        Assert.Equal((deuxieme, 0, 3), (vm.SelectionDuTexte?.Case, vm.SelectionDuTexte?.Debut, vm.SelectionDuTexte?.Fin));
        var avant = DocumentSerializer.ToJson(vm.Document);

        var menu = Rendu.CliquerDroit(fenetre, Rendu.BordDuCaractere(zone, 1));
        Assert.Same(deuxieme, vm.CaseEnSaisie);
        Assert.Equal(new[] { "Co_uper", "_Copier", "C_oller", "Format du _bloc…" },
            menu.Items.OfType<MenuItem>().Where(e => e.IsVisible).Select(e => $"{e.Header}"));
        Rendu.Cliquer(menu.Items.OfType<MenuItem>().Single(e => Equals(e.Header, "_Copier")));
        Rendu.Battre(8);
        Assert.Equal("VNE", Rendu.TexteCopie(fenetre));
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.True(zone.IsFocused, "la zone de la case n'a pas repris le focus");

        menu = Rendu.CliquerDroit(fenetre, Rendu.BordDuCaractere(zone, 1));
        Rendu.Cliquer(menu.Items.OfType<MenuItem>().Single(e => Equals(e.Header, "Co_uper")));
        Rendu.Battre(8);
        Assert.Equal(" 250", deuxieme.Text);
        Assert.Equal(" 250", ChampDuDock(fenetre, deuxieme).Text);

        menu = Rendu.CliquerDroit(fenetre, Rendu.BordDuCaractere(zone, 0));
        Rendu.Cliquer(menu.Items.OfType<MenuItem>().Single(e => Equals(e.Header, "C_oller")));
        Rendu.Battre(8);
        Assert.Equal("VNE 250", deuxieme.Text);
        Assert.Same(deuxieme, vm.CaseEnSaisie);

        // Ctrl+G : le tableau entier, en un pas, la saisie gardée.
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        avant = DocumentSerializer.ToJson(vm.Document);
        zone = Rendu.EntrerDansLaCase(fenetre, vm, tableau, deuxieme);
        Rendu.Touche(fenetre, PhysicalKey.G, RawInputModifiers.Control);
        Assert.True(tableau.Bold, "Ctrl+G dans une case n'a pas mis le tableau en gras");
        Assert.Empty(deuxieme.Spans);
        Assert.Same(deuxieme, vm.CaseEnSaisie);
        Rendu.Touche(fenetre, PhysicalKey.Escape);

        // Un pas : l'annuler rend le tableau maigre, et les pas de Couper et de Coller restent.
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.True(vm.CanUndo, "Couper et Coller n'ont pas fait leur pas");
    }

    // ------------------------------------------------------------------ tourné

    /// <summary>
    /// Un tableau tourné de 30° : un clic mené par le modèle sur le 3ᵉ caractère d'une case pose le
    /// curseur à 2 ; le curseur dessiné, la sélection, les caractères spéciaux de N22 et le trait
    /// ondulé de N25 — les cases sont dessinées par <see cref="TexteCompose"/>, et soulignées — tombent
    /// sur la case, là où le modèle la place. Le mot qu'on tape ne se souligne pas encore.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N27")]
    public void Dans_un_tableau_tourne_le_curseur_la_selection_les_marques_et_les_fautes_tombent_sur_la_case()
    {
        var tableau = Bande(rotation: 30, hauteur: 40, textes: ["Speed", "We recieve it", "kt"]);
        var document = Rendu.Feuille(tableau);
        document.Langue = "en-US";
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.Orthographe = new Orthographe(Path.Combine(Rendu.Racine(), "Scripts", "fixtures", "orthographe"));
        vm.CaracteresSpeciauxVisibles = true;
        Rendu.UneImage();
        var deuxieme = Case(tableau, 0, 1);
        var chemin = new Frame[] { tableau };

        // L'écran dessine la case où le modèle la met.
        var compose = Compose(fenetre, deuxieme);
        foreach (var (x, y) in new[] { (0.0, 0.0), (120.0, 0.0), (120.0, 40.0), (0.0, 40.0), (60.0, 20.0) })
        {
            Proche(ParLeModele(fenetre, chemin, deuxieme, x, y), compose.TranslatePoint(new Point(x, y), fenetre)!.Value,
                $"le point ({x} ; {y}) de la case");
        }

        // Le trait ondulé sous « recieve », avant la saisie : la case est soulignée.
        var fautes = fenetre.GetVisualDescendants().OfType<FautesSoulignees>().Single();
        var soulignes = fautes.Blocs.Single(b => ReferenceEquals(b.Compose.Bloc?.Spans, deuxieme.Spans));
        var trait = Assert.Single(soulignes.Traits);
        Assert.Equal("recieve", trait.Faute.Mot);
        var geometrie = Geometrie(fenetre, deuxieme);
        var debut = geometrie.PointDe(3);
        Assert.True(Math.Abs(debut.X - trait.Gauche) < 0.01, $"le trait part de {trait.Gauche}, le « r » est en {debut.X}");
        Proche(ParLeModele(fenetre, chemin, deuxieme, trait.Gauche, debut.Y),
            fautes.TranslatePoint(new Point(trait.Gauche, debut.Y).Transform(soulignes.Transformation), fenetre)!.Value,
            "le départ du trait ondulé");

        // Un clic sur le 3ᵉ caractère, mené par le modèle : la saisie de la case, le curseur à 2.
        var vise = Caractere(geometrie, 2);
        Rendu.Cliquer(fenetre, ParLeModele(fenetre, chemin, deuxieme, vise.X, vise.Y));
        Assert.Same(deuxieme, vm.CaseEnSaisie);
        Assert.Same(tableau, Assert.Single(vm.Selection));
        Assert.Equal(2, vm.SelectionDuTexte?.Curseur);

        var marques = Nomme<MarquesDuTexte>(fenetre, "MarquesDeLaSaisie");
        var curseur = marques.CurseurPose ?? throw new InvalidOperationException("aucun curseur dessiné");
        Assert.Equal(geometrie.CurseurDe(2), curseur);
        var pied = new Point(curseur.X, curseur.Bottom);
        Proche(ParLeModele(fenetre, chemin, deuxieme, pied.X, pied.Y),
            marques.TranslatePoint(pied.Transform(marques.Transformation), fenetre)!.Value, "le pied du curseur");

        // La frappe : la case se reconstruit, le curseur se repose sur la composition neuve.
        Rendu.Taper(fenetre, "X");
        Rendu.UneImage();
        Assert.Equal("WeX recieve it", deuxieme.Text);
        geometrie = Geometrie(fenetre, deuxieme);
        Assert.Equal(geometrie.CurseurDe(3), marques.CurseurPose);
        pied = new Point(geometrie.CurseurDe(3).X, geometrie.CurseurDe(3).Bottom);
        Proche(ParLeModele(fenetre, chemin, deuxieme, pied.X, pied.Y),
            marques.TranslatePoint(pied.Transform(marques.Transformation), fenetre)!.Value, "le pied du curseur après la frappe");

        // La sélection d'un glissé, du 4ᵉ au 11ᵉ caractère — « recieve » —, surlignée sur la case.
        Rendu.Glisser(fenetre, ParLeModele(fenetre, chemin, deuxieme, geometrie.CurseurDe(4).X, vise.Y),
            ParLeModele(fenetre, chemin, deuxieme, geometrie.CurseurDe(11).X, vise.Y));
        Assert.Equal((deuxieme, 4, 11), (vm.SelectionDuTexte?.Case, vm.SelectionDuTexte?.Debut, vm.SelectionDuTexte?.Fin));
        var plage = Assert.Single(marques.Plages);
        Assert.Equal(geometrie.RectanglesDe(4, 11).Single(), plage);

        // Les caractères spéciaux de la case (N22) : ¶ au bout du texte, où le modèle le met.
        var speciaux = fenetre.GetVisualDescendants().OfType<CaracteresSpeciaux>().Single();
        var siens = speciaux.Blocs.Single(b => ReferenceEquals(b.Compose.Bloc?.Spans, deuxieme.Spans));
        var finDeParagraphe = siens.Caracteres.Single(c => c.Sorte == CaractereSpecial.FinDeParagraphe);
        Proche(ParLeModele(fenetre, chemin, deuxieme, finDeParagraphe.Point.X, finDeParagraphe.Point.Y),
            speciaux.TranslatePoint(finDeParagraphe.Point.Transform(siens.Transformation), fenetre)!.Value,
            "le ¶ de la case");

        // Le mot qu'on tape, le curseur à son bout, ne se souligne pas encore ; quitté, il se souligne.
        Rendu.Touche(fenetre, PhysicalKey.End);
        Rendu.Taper(fenetre, " teh");
        Rendu.UneImage();
        soulignes = fautes.Blocs.Single(b => ReferenceEquals(b.Compose.Bloc?.Spans, deuxieme.Spans));
        Assert.DoesNotContain(soulignes.Traits, t => t.Faute.Mot == "teh");
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.UneImage();
        soulignes = fautes.Blocs.Single(b => ReferenceEquals(b.Compose.Bloc?.Spans, deuxieme.Spans));
        Assert.Contains(soulignes.Traits, t => t.Faute.Mot == "teh");
    }

    /// <summary>
    /// La recherche (N20) et la vérification orthographique (N25) parcouraient les cases sans y
    /// entrer : la barre d'état nommait la case. Elles y entrent désormais, comme dans un bloc — le
    /// tableau sélectionné, la plage trouvée sélectionnée en saisie dans la case —, et la barre
    /// d'état la nomme toujours. Remplacer, depuis la barre, remplace la plage sélectionnée, en un pas.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N27")]
    public void La_recherche_et_la_verification_entrent_dans_la_case()
    {
        var tableau = Bande(textes: ["The text", "We recieve it", "a word"]);
        var document = Rendu.Feuille(tableau);
        document.Langue = "en-US";
        var (fenetre, vm) = Rendu.Ouvrir(document);
        vm.Orthographe = new Orthographe(Path.Combine(Rendu.Racine(), "Scripts", "fixtures", "orthographe"));
        var deuxieme = Case(tableau, 0, 1);

        Rendu.Touche(fenetre, PhysicalKey.F7);
        Rendu.UneImage();
        Assert.Same(deuxieme, vm.CaseEnSaisie);
        Assert.Same(tableau, Assert.Single(vm.Selection));
        Assert.Equal((tableau, deuxieme, 3, 10),
            (vm.SelectionDuTexte?.Bloc, vm.SelectionDuTexte?.Case, vm.SelectionDuTexte?.Debut, vm.SelectionDuTexte?.Fin));
        Assert.Equal("recieve", vm.MotFautif);
        Assert.Contains($"dans la case {deuxieme.Label}", vm.Status);
        Assert.NotNull(Nomme<MarquesDuTexte>(fenetre, "MarquesDeLaSaisie").Plages.SingleOrDefault());

        var avant = DocumentSerializer.ToJson(vm.Document);
        vm.RemplacerLaFauteCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal("We receive it", deuxieme.Text);
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
    }

    /// <summary>
    /// Un tableau dans un groupe tourné, lui-même tourné : un clic dans une case traverse le groupe,
    /// le groupe reste la sélection, le tableau y est pris seul (N26) — la barre et le dock le visent
    /// —, et le curseur tombe là où le modèle le met, sous les deux rotations.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N27")]
    public void Dans_un_groupe_tourne_la_case_d_un_tableau_tourne_se_tape_le_groupe_restant_selectionne()
    {
        var tableau = Bande(rotation: 15, textes: ["Vitesse", "250", "kt"]);
        tableau.X = 10;
        tableau.Y = 60;
        var groupe = new GroupFrame { Name = "Encart", X = 80, Y = 120, Width = 380, Height = 160, Rotation = 20 };
        groupe.Children.Add(tableau);
        groupe.Children.Add(new ShapeFrame { Name = "Cadre", X = 0, Y = 0, Width = 40, Height = 40 });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(groupe));
        var deuxieme = Case(tableau, 0, 1);
        var chemin = new Frame[] { groupe, tableau };
        var avant = DocumentSerializer.ToJson(vm.Document);

        var geometrie = Geometrie(fenetre, deuxieme);
        var vise = Caractere(geometrie, 1);
        Rendu.Cliquer(fenetre, ParLeModele(fenetre, chemin, deuxieme, vise.X, vise.Y));
        Assert.Same(deuxieme, vm.CaseEnSaisie);
        Assert.Same(groupe, Assert.Single(vm.Selection));
        Assert.Same(tableau, vm.MembreSeul);
        Assert.Same(tableau, vm.BlocVise);
        Assert.Equal(1, vm.SelectionDuTexte?.Curseur);

        var marques = Nomme<MarquesDuTexte>(fenetre, "MarquesDeLaSaisie");
        var curseur = marques.CurseurPose ?? throw new InvalidOperationException("aucun curseur dessiné");
        var pied = new Point(curseur.X, curseur.Bottom);
        Proche(ParLeModele(fenetre, chemin, deuxieme, pied.X, pied.Y),
            marques.TranslatePoint(pied.Transform(marques.Transformation), fenetre)!.Value, "le pied du curseur");

        Rendu.Taper(fenetre, "0");
        Assert.Equal("2050", deuxieme.Text);
        Assert.Equal(2, groupe.Children.Count);
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.Null(vm.CaseEnSaisie);
        Assert.Same(groupe, Assert.Single(vm.Selection));

        var repris = Assert.IsType<GroupFrame>(Rendu.UnSeulPas(vm, avant, "la frappe dans la case d'un groupe"));
        Assert.Equal("250", Assert.IsType<TableFrame>(repris.Children[0]).CaseEn(0, 1)?.Text);
    }

    // ------------------------------------------------------------------ le bord

    /// <summary>
    /// Le bord du tableau le prend et le déplace, comme celui d'un bloc de texte (N12) : une pression
    /// sur sa bande de bord ne l'ouvre pas, un glissé parti de là le déplace d'un pas. Un clic dans une
    /// autre case pendant la saisie y passe, le tableau restant la sélection.
    /// </summary>
    [AvaloniaFact]
    [Trait("poste", "N27")]
    public void Le_bord_du_tableau_le_prend_et_le_deplace_et_un_clic_dans_une_autre_case_y_passe()
    {
        var tableau = Bande(textes: ["Vitesse", "250", "kt"]);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        var avant = DocumentSerializer.ToJson(vm.Document);

        // Alt : sans aimantation, le pas est celui du pointeur.
        Rendu.Glisser(fenetre, Rendu.Bord(fenetre, tableau, 0.5, 0), Rendu.SurLaPage(fenetre, 60 + 180 + 40, 80 + 2 + 30),
            RawInputModifiers.Alt);
        Assert.Equal(100, tableau.X, 6);
        Assert.Equal(110, tableau.Y, 6);
        Assert.Same(tableau, Assert.Single(vm.Selection));
        Assert.Null(vm.CaseEnSaisie);
        Assert.Null(Rendu.ZoneDeLaCase(fenetre));
        tableau = Assert.IsType<TableFrame>(Rendu.UnSeulPas(vm, avant, "le tableau tiré par son bord"));

        // Un clic sur le bord, ailleurs : le tableau pris, sans saisie.
        vm.SetSelection([]);
        Rendu.Cliquer(fenetre, Rendu.Bord(fenetre, tableau, 0, 0.5));
        Assert.Same(tableau, Assert.Single(vm.Selection));
        Assert.Null(vm.CaseEnSaisie);
        Assert.Null(Rendu.ZoneDeLaCase(fenetre));

        Rendu.EntrerDansLaCase(fenetre, vm, tableau, Case(tableau, 0, 0));
        var troisieme = Case(tableau, 0, 2);
        var vise = Caractere(Geometrie(fenetre, troisieme), 1);
        Rendu.Cliquer(fenetre, ParLeModele(fenetre, [tableau], troisieme, vise.X, vise.Y));
        Assert.Equal((troisieme, 1), (vm.CaseEnSaisie, vm.SelectionDuTexte?.Curseur));
        Assert.Same(tableau, Assert.Single(vm.Selection));
        Assert.True(Rendu.ZoneDeLaCase(fenetre)?.IsFocused);

        // Échap rend le tableau à la planche : Suppr l'ôte, comme un bloc dont on quitte le texte.
        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Rendu.Touche(fenetre, PhysicalKey.Delete);
        Assert.Empty(vm.CurrentPage!.Frames);
    }
}
