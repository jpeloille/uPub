using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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
/// Les styles suivent la frappe — le poste Q1, joué au clavier dans la vraie fenêtre.
///
/// Le calcul est éprouvé à part, en tests purs (<c>RecalageTests</c>). Ce qu'on éprouve
/// ici, c'est le BRANCHEMENT : que les trois zones où l'on tape — la saisie sur la page,
/// le champ « Contenu » du panneau, et le champ d'une case de tableau — passent bien
/// par le recalage. Une zone liée au texte brut taperait comme avant, et aucun test du
/// noyau ne le verrait.
///
/// Le document est le vrai : ATA 21, importé par pub2raw comme l'application l'importe.
/// C'est sur lui que le défaut a été trouvé, et un bloc fabriqué pour le test n'aurait
/// ni ses titres en gras d'un autre corps ni ses listes à puces en retrait.
/// </summary>
public class FrappeTests
{
    private const string Tape = "Nouveau ";

    // ------------------------------------------------------------------ le document

    /// <summary>Le premier bloc de texte de la page qui porte des fragments de style.</summary>
    private static TextFrame PremierBlocRiche(Document document) =>
        document.Pages[0].Frames.OfType<TextFrame>().First(bloc => bloc.Spans.Count > 0);

    // ------------------------------------------------------------------ ce qu'on relève

    private static StyleEffectif Commun(TextFrame bloc) =>
        new(bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic, bloc.Underline, bloc.Foreground, 0);

    /// <summary>Le style de chaque caractère du texte, tel que les fragments le donnent.</summary>
    private static List<StyleEffectif> StyleParCaractere(TextFrame bloc) =>
        StyleEffectif.Decouper(bloc.Text, bloc.Spans, Commun(bloc))
            .SelectMany(morceau => Enumerable.Repeat(morceau.Style, morceau.Texte.Length))
            .ToList();

    /// <summary>
    /// Les mots en gras du bloc, tels que la COMPOSITION les rend — la même fonction que
    /// l'écran et l'export appellent, avec la règle à mesurer de l'écran. C'est ce qui
    /// se dessine, et non ce que le fichier dit.
    /// </summary>
    private static List<string> MotsGrasComposes(TextFrame bloc)
    {
        var lignes = Compositeur.Composer(
            bloc.Text, bloc.Spans, Commun(bloc), bloc.Paragraphs, bloc.Paragraph,
            bloc.Width - bloc.PaddingLeft - bloc.PaddingRight,
            bloc.Height - bloc.PaddingTop - bloc.PaddingBottom,
            bloc.Colonnage, bloc.LineHeight, new MetriquesAvalonia());

        return lignes.SelectMany(ligne => ligne.Morceaux)
            .Where(morceau => morceau.Style.Bold)
            .SelectMany(morceau => morceau.Texte.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();
    }

    /// <summary>Le retrait de la première ligne de chaque paragraphe, par son début.</summary>
    private static Dictionary<string, double> RetraitsComposes(TextFrame bloc)
    {
        var lignes = Compositeur.Composer(
            bloc.Text, bloc.Spans, Commun(bloc), bloc.Paragraphs, bloc.Paragraph,
            bloc.Width - bloc.PaddingLeft - bloc.PaddingRight,
            bloc.Height - bloc.PaddingTop - bloc.PaddingBottom,
            bloc.Colonnage, bloc.LineHeight, new MetriquesAvalonia());

        var retraits = new Dictionary<string, double>();
        var debut = true;
        foreach (var ligne in lignes)
        {
            var texte = ligne.Texte.Trim();
            if (debut && texte.Length > 0) retraits.TryAdd(texte[..Math.Min(20, texte.Length)], ligne.Retrait);
            debut = ligne.FinDeParagraphe;
        }

        return retraits;
    }

    // ------------------------------------------------------------------ les gestes

    private static Panel Planche(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");

    private static Point SurLaPage(Window fenetre, double x, double y)
    {
        var origine = Planche(fenetre).TranslatePoint(new Point(0, 0), fenetre) ?? new Point(0, 0);
        return new Point(origine.X + x, origine.Y + y);
    }

    /// <summary>
    /// Entre en saisie sur la page par un double-clic, comme l'utilisateur. Le bloc est
    /// pris d'abord : le double-clic édite le bloc sélectionné.
    /// </summary>
    private static TextBox EntrerEnSaisie(Window fenetre, MainWindowViewModel vm, TextFrame bloc)
    {
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var point = SurLaPage(fenetre, bloc.X + 40, bloc.Y + 8);
        for (var clic = 0; clic < 2; clic++)
        {
            fenetre.MouseDown(point, MouseButton.Left);
            fenetre.MouseUp(point, MouseButton.Left);
        }

        Rendu.Battre(8);
        Assert.True(bloc.IsEditing, "le double-clic n'a pas ouvert la saisie sur la page");

        return Planche(fenetre).GetVisualDescendants().OfType<TextBox>()
            .Single(zone => ReferenceEquals(zone.DataContext, bloc) && zone.IsVisible);
    }

    /// <summary>
    /// Le geste du poste : entrer dans le bloc, aller en tête, taper un mot, sortir.
    /// L'entrée en saisie sélectionne tout le texte ; c'est Ctrl+Début qui y pose le
    /// curseur, comme on le ferait au clavier.
    /// </summary>
    private static void TaperEnTete(Window fenetre, MainWindowViewModel vm, TextFrame bloc)
    {
        EntrerEnSaisie(fenetre, vm, bloc);
        TaperEnTeteDejaEnSaisie(fenetre, bloc);
    }

    private static void TaperEnTeteDejaEnSaisie(Window fenetre, TextFrame bloc)
    {
        fenetre.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.Control);
        fenetre.KeyTextInput(Tape);
        Rendu.Battre();

        fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Rendu.Battre();
        Assert.False(bloc.IsEditing, "Échap n'a pas refermé la saisie");
    }

    // ------------------------------------------------------------------ les tests

    [AvaloniaFact]
    [Trait("poste", "Q1")]
    public void Taper_en_tete_dATA_21_garde_les_memes_mots_en_gras()
    {
        var document = Rendu.Ata21();
        var bloc = PremierBlocRiche(document);
        var (fenetre, vm) = Rendu.Ouvrir(document);

        var styles = StyleParCaractere(bloc);
        var gras = MotsGrasComposes(bloc);
        Assert.NotEmpty(gras);

        TaperEnTete(fenetre, vm, bloc);

        // La frappe est bien arrivée, et en tête.
        Assert.StartsWith(Tape, bloc.Text);

        // Par les fragments : chaque caractère d'avant a gardé son style, décalé de la
        // longueur du mot tapé. C'est plus fort que « les mêmes mots en gras » : le
        // corps, la police et l'italique doivent suivre aussi.
        var apres = StyleParCaractere(bloc);
        Assert.Equal(styles, apres.Skip(Tape.Length));

        // Le mot tapé en tête de texte prend le style du caractère qui le suit, comme
        // dans Publisher : celui du titre en gras, à son corps.
        Assert.NotEqual(Commun(bloc), styles[0]);
        Assert.All(apres.Take(Tape.Length), style => Assert.Equal(styles[0], style));

        // Par la composition : ce que l'écran et l'export dessinent en gras — les mêmes
        // mots, et le mot tapé devant.
        Assert.Equal([Tape.Trim(), .. gras], MotsGrasComposes(bloc));

        // La recette exporte ce document-là et relève les gras dans le flux du PDF.
        if (Environment.GetEnvironmentVariable("MYPUBLISHER_RECETTE_FRAPPE") is { } chemin &&
            !string.IsNullOrWhiteSpace(chemin))
        {
            DocumentSerializer.Save(vm.Document, chemin);
        }
    }

    [AvaloniaFact]
    [Trait("poste", "Q1")]
    public void Lannulation_rend_le_texte_et_les_fragments_davant()
    {
        var document = Rendu.Ata21();
        var bloc = PremierBlocRiche(document);
        var (fenetre, vm) = Rendu.Ouvrir(document);

        // L'état de référence est celui de l'ouverture, avant toute sélection. Il a
        // d'abord dû être pris une fois la saisie ouverte : sélectionner le bloc
        // réécrivait le dernier chiffre de son ordonnée, par l'aller-retour en
        // millimètres du panneau. Le poste Q14 l'a corrigé ; sélectionner, double-
        // cliquer puis taper ne laisse plus derrière soi que la frappe.
        var avant = DocumentSerializer.ToJson(vm.Document);
        TaperEnTete(fenetre, vm, bloc);
        var tape = DocumentSerializer.ToJson(vm.Document);
        Assert.NotEqual(avant, tape);

        vm.UndoCommand.Execute(null);
        Rendu.Battre();

        // Au caractère près, fragments et écarts compris : l'annulation relit un
        // instantané, et la relecture ne doit rien recaler.
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));

        vm.RedoCommand.Execute(null);
        Rendu.Battre();

        Assert.Equal(tape, DocumentSerializer.ToJson(vm.Document));
    }

    [AvaloniaFact]
    [Trait("poste", "Q1")]
    public void Un_saut_de_ligne_tape_garde_chaque_retrait_a_son_paragraphe()
    {
        var document = Rendu.Ata21();
        var bloc = PremierBlocRiche(document);
        var (fenetre, vm) = Rendu.Ouvrir(document);

        var retraits = RetraitsComposes(bloc);
        var ecarts = bloc.Paragraphs.Select(ecart => ecart.Start).ToList();
        var paragraphes = bloc.Text.Split('\n').Length;
        Assert.Contains(retraits.Values, retrait => retrait != 0);

        // Un retour à la ligne au bout du premier paragraphe — le titre « Air
        // Production » —, là où l'on ouvrirait un paragraphe neuf sous lui.
        var zone = EntrerEnSaisie(fenetre, vm, bloc);
        zone.CaretIndex = bloc.Text.IndexOf('\n');
        Rendu.Battre();
        fenetre.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Rendu.Battre();
        fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Rendu.Battre();

        Assert.Equal(paragraphes + 1, bloc.Text.Split('\n').Length);

        // Chaque écart a descendu d'un paragraphe, et chaque paragraphe se compose
        // avec le retrait qu'il avait.
        Assert.Equal(ecarts.Select(indice => indice + 1), bloc.Paragraphs.Select(ecart => ecart.Start));
        Assert.Equal(retraits, RetraitsComposes(bloc));
    }

    /// <summary>Le champ « Contenu » du panneau : lié au même bloc, mais hors de la planche.</summary>
    private static TextBox ChampDuPanneau(Window fenetre, TextFrame bloc)
    {
        var planche = Planche(fenetre);
        return fenetre.GetVisualDescendants().OfType<TextBox>()
            .Single(t => ReferenceEquals(t.DataContext, bloc) && t.AcceptsReturn &&
                         t.IsEffectivelyVisible && !t.GetVisualAncestors().Contains(planche));
    }

    [AvaloniaFact]
    [Trait("poste", "Q1")]
    public void Le_champ_du_panneau_recale_aussi()
    {
        var document = Rendu.Ata21();
        var bloc = PremierBlocRiche(document);
        var (fenetre, vm) = Rendu.Ouvrir(document);
        var gras = MotsGrasComposes(bloc);

        vm.SelectOnly(bloc);
        Rendu.Battre();

        var zone = ChampDuPanneau(fenetre, bloc);
        zone.Focus();
        zone.CaretIndex = 0;
        Rendu.Battre();
        fenetre.KeyTextInput(Tape);
        Rendu.Battre();

        Assert.StartsWith(Tape, bloc.Text);
        Assert.Equal([Tape.Trim(), .. gras], MotsGrasComposes(bloc));
    }

    [AvaloniaFact]
    [Trait("poste", "Q1")]
    public void Le_champ_du_panneau_suit_la_frappe_sur_la_page()
    {
        // Les deux zones se lient à « Saisie », et c'est « Text » que la saisie écrit :
        // sans l'avis que « Saisie » a changé avec lui, le panneau garderait le texte
        // d'avant la frappe sur la page. La frappe suivante dans le panneau réécrirait
        // alors le bloc avec ce texte périmé, et ce qu'on a tapé sur la page serait perdu.
        var document = Rendu.Ata21();
        var bloc = PremierBlocRiche(document);
        var (fenetre, vm) = Rendu.Ouvrir(document);

        TaperEnTete(fenetre, vm, bloc);
        var tape = bloc.Text;
        Assert.StartsWith(Tape, tape);

        var zone = ChampDuPanneau(fenetre, bloc);
        Assert.Equal(tape, zone.Text);

        zone.Focus();
        zone.CaretIndex = zone.Text!.Length;
        Rendu.Battre();
        fenetre.KeyTextInput("Z");
        Rendu.Battre();

        Assert.Equal(tape + "Z", bloc.Text);
    }

    [AvaloniaFact]
    [Trait("poste", "Q1")]
    public void Le_champ_dune_case_recale_ses_fragments()
    {
        // Une case prête ses fragments au bloc qui la dessine : c'est sur la case que
        // le recalage doit avoir lieu, et c'est le champ du panneau qui la modifie.
        var tableau = new TableFrame { Name = "Bande", X = 60, Y = 60 };
        tableau.RowCount = 1;
        tableau.ColumnCount = 1;
        var cellule = tableau.Cells[0];
        cellule.Text = "abc def";
        cellule.Spans.Add(new StyleSpan { Start = 4, Length = 3, Bold = true });

        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(tableau));
        vm.SelectOnly(tableau);
        Rendu.Battre();

        var zone = fenetre.GetVisualDescendants().OfType<TextBox>()
            .Single(t => ReferenceEquals(t.DataContext, cellule) && t.Classes.Contains("cellText"));
        zone.Focus();
        zone.CaretIndex = 0;
        Rendu.Battre();
        fenetre.KeyTextInput("XX");
        Rendu.Battre();

        Assert.Equal("XXabc def", cellule.Text);
        Assert.Equal((6, 3), (cellule.Spans[0].Start, cellule.Spans[0].Length));

        var dessine = (TextFrame)tableau.CellFrames[0];
        var grasDessine = StyleEffectif.Decouper(dessine.Text, dessine.Spans, Commun(dessine))
            .Where(morceau => morceau.Style.Bold).Select(morceau => morceau.Texte);
        Assert.Equal(["def"], grasDessine);

        // Le champ suit un texte écrit hors de lui, par « Text » — l'import, ou demain
        // un collage de tableau : sans quoi la frappe suivante le réécrirait.
        cellule.Text = "écrit ailleurs";
        Rendu.Battre();
        Assert.Equal("écrit ailleurs", zone.Text);
    }
}
