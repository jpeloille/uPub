using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Importer un fichier texte dans un bloc — le poste B2, joué dans la vraie fenêtre.
///
/// Le sélecteur de fichier ne s'ouvre pas sans affichage : le geste est donc pris là où
/// la fenêtre le passe au modèle de vue, <see cref="MainWindowViewModel.ImporterTexte"/>,
/// avec un chemin — le partage que Q3 a fait pour <c>ChoisirImageDeFond</c>. Ce que la
/// fenêtre garde pour elle, le menu et le bouton du panneau, est éprouvé à part : qu'ils
/// soient là, et offerts au seul bloc qui peut recevoir le texte.
///
/// Le décodage lui-même est éprouvé dans le noyau (<c>TexteImporteTests</c>). Ici, c'est
/// ce que l'utilisateur voit : le texte dans le bloc, ses accents, le marqueur de
/// débordement de A2, et l'annulation.
/// </summary>
[Trait("poste", "B2")]
public class ImportTexteTests : IDisposable
{
    private readonly string _travail =
        Directory.CreateTempSubdirectory("mypublisher-import-texte-").FullName;

    public void Dispose() => Directory.Delete(_travail, recursive: true);

    // ------------------------------------------------------------------ le matériel

    /// <summary>
    /// Un bloc où tiennent quelques lignes et pas davantage : 240 × 160 pt, DejaVu Sans
    /// 11 pt à interligne 1, soit une douzaine de lignes. Trois mille signes en
    /// demandent plus de quatre-vingts.
    /// </summary>
    private static TextFrame Bloc(string texte = "Texte d'avant") => new()
    {
        Name = "Article", X = 40, Y = 40, Width = 240, Height = 160,
        FontFamily = "DejaVu Sans", FontSize = 11, LineHeight = 1,
        Foreground = "#FF000000",
        Text = texte,
    };

    /// <summary>
    /// Trois mille signes exactement, espaces et sauts compris, une fois les fins de
    /// ligne ramenées à un caractère : des paragraphes de quatre phrases accentuées.
    /// </summary>
    internal static string TroisMilleSignes()
    {
        string[] phrases =
        [
            "Le système de conditionnement d'air régule la température de la cabine.",
            "Chaque groupe reçoit l'air prélevé sur les moteurs, déjà comprimé.",
            "À l'arrêt, le groupe auxiliaire prend le relais sans que l'équipage intervienne.",
            "La vanne de régulation s'ouvre ou se ferme selon la consigne sélectionnée.",
        ];

        var texte = new StringBuilder();
        for (var i = 0; texte.Length < 3000; i++)
        {
            texte.Append(phrases[i % phrases.Length]);
            texte.Append(i % phrases.Length == phrases.Length - 1 ? '\n' : ' ');
        }

        // Coupé à trois mille, sans finir sur un blanc : la normalisation retirerait un
        // saut final, et le compte ne serait plus juste.
        var coupe = texte.ToString(0, 3000);
        return char.IsWhiteSpace(coupe[^1]) ? coupe[..^1] + "." : coupe;
    }

    /// <summary>
    /// Écrit le texte comme un éditeur de Windows l'aurait écrit : en Latin-1, en fins de
    /// ligne CRLF, et terminé par une fin de ligne.
    /// </summary>
    private string EcrireEnLatin1(string nom, string texte)
    {
        var chemin = Path.Combine(_travail, nom);
        File.WriteAllBytes(chemin, Encoding.Latin1.GetBytes(texte.Replace("\n", "\r\n") + "\r\n"));
        return chemin;
    }

    private string EcrireEnUtf8(string nom, string texte)
    {
        var chemin = Path.Combine(_travail, nom);
        File.WriteAllText(chemin, texte, new UTF8Encoding(false));
        return chemin;
    }

    private static StyleEffectif Commun(TextFrame bloc) =>
        new(bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic, bloc.Foreground, 0);

    /// <summary>De combien le texte composé dépasse le bloc, à la règle de l'écran.</summary>
    private static double Debordement(TextFrame bloc)
    {
        var hauteur = bloc.Height - bloc.PaddingTop - bloc.PaddingBottom;
        var lignes = Compositeur.Composer(
            bloc.Text, bloc.Spans, Commun(bloc), bloc.Paragraphs, bloc.Paragraph,
            bloc.Width - bloc.PaddingLeft - bloc.PaddingRight, hauteur,
            bloc.Colonnage, bloc.LineHeight, new MetriquesAvalonia());

        return Compositeur.Debordement(lignes, hauteur, bloc.PaddingBottom, bloc.VerticalAlign);
    }

    // ------------------------------------------------------------------ les tests

    [AvaloniaFact]
    public void Trois_mille_signes_en_latin1_arrivent_dans_le_bloc_et_debordent()
    {
        // Le geste de la recette du plan, sur le fichier le moins commode : Latin-1,
        // CRLF, une fin de ligne en fin de fichier.
        var texte = TroisMilleSignes();
        Assert.Equal(3000, texte.Length);
        var chemin = EcrireEnLatin1("article.txt", texte);
        Assert.Throws<DecoderFallbackException>(
            () => new UTF8Encoding(false, true).GetString(File.ReadAllBytes(chemin)));

        var bloc = Bloc();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.ShowGuides = true;
        Rendu.Battre();

        // Le témoin : avant l'import, le bloc tient et ne porte pas de marqueur.
        Assert.Equal(0, DebordementTests.Marqueur(Rendu.Encre(fenetre), bloc));
        Assert.True(Debordement(bloc) <= Compositeur.SeuilDebordement);

        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        Assert.True(vm.ImporterTexte(chemin), vm.Status);
        Rendu.Battre();

        // Le texte, au signe près : les accents lus, les CRLF ramenés à un saut, la fin
        // de ligne finale écartée.
        Assert.Equal(texte, bloc.Text);
        Assert.Contains("système", bloc.Text);
        Assert.DoesNotContain('\r', bloc.Text);
        Assert.Contains("Latin-1", vm.Status);
        Assert.Contains("3000 signes", vm.Status);

        // A2 le signale : la composition déborde, et le marqueur rouge paraît. La
        // sélection est levée d'abord, pour que rien d'autre ne se dessine au coin.
        Assert.True(Debordement(bloc) > Compositeur.SeuilDebordement,
            $"débordement de {Debordement(bloc):0.0} pt seulement");
        vm.SelectOnly(null);
        Rendu.Battre();
        Assert.True(DebordementTests.Marqueur(Rendu.Encre(fenetre), bloc) > 20,
            "le bloc rempli ne porte pas le marqueur de débordement");

        var importe = DocumentSerializer.ToJson(vm.Document);

        // Un pas, et un seul.
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, "l'import a empilé plus d'un pas d'annulation");

        // La recette exporte ces documents-là : elle relit le débordement dans
        // l'avertissement de l'export, et les accents dans le PDF.
        if (Environment.GetEnvironmentVariable("MYPUBLISHER_RECETTE_IMPORT") is { } dossier &&
            !string.IsNullOrWhiteSpace(dossier))
        {
            Directory.CreateDirectory(dossier);
            File.Copy(chemin, Path.Combine(dossier, "source.txt"), overwrite: true);
            File.WriteAllText(Path.Combine(dossier, "avant.mypub"), avant);
            File.WriteAllText(Path.Combine(dossier, "importe.mypub"), importe);
            File.WriteAllText(Path.Combine(dossier, "annule.mypub"), DocumentSerializer.ToJson(vm.Document));
        }
    }

    [AvaloniaFact]
    public void Lannulation_rend_le_bloc_davant_fragments_et_ecarts_compris()
    {
        // Un bloc riche : un titre gras d'un autre corps, et un paragraphe centré.
        // L'import les vide ; l'annulation doit les rendre au caractère près.
        var bloc = Bloc("Titre\nTexte d'avant");
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 5, Bold = true, FontSize = 18 });
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 1, Length = 1, Align = TextAlign.Center });

        var (_, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        Assert.False(vm.CanUndo);

        var avant = DocumentSerializer.ToJson(vm.Document);
        Assert.True(vm.ImporterTexte(EcrireEnUtf8("neuf.txt", "Nouveau texte\nsur deux lignes\n")));
        Rendu.Battre();
        var importe = DocumentSerializer.ToJson(vm.Document);

        Assert.Equal("Nouveau texte\nsur deux lignes", bloc.Text);

        vm.UndoCommand.Execute(null);
        Rendu.Battre();

        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo, "l'import a empilé plus d'un pas d'annulation");

        vm.RedoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(importe, DocumentSerializer.ToJson(vm.Document));
    }

    [AvaloniaFact]
    public void Le_texte_importe_repart_du_style_du_bloc()
    {
        // Le choix du poste : un fichier texte ne porte aucun style, il prend celui du
        // bloc. Le titre gras en 18 pt qui ouvrait le bloc ne déteint pas sur lui, et
        // aucun écart de paragraphe d'avant ne reste accroché à un indice.
        var bloc = Bloc("Titre\nTexte d'avant");
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 5, Bold = true, FontSize = 18 });
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 0, Length = 2, Align = TextAlign.Center });

        var (_, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        // « Titre » commun aux deux textes : posé par la seule saisie, sans vider les
        // fragments, ce début garderait son gras.
        Assert.True(vm.ImporterTexte(EcrireEnUtf8("titre.txt", "Titre neuf\net corps neuf")));

        Assert.Empty(bloc.Spans);
        Assert.Empty(bloc.Paragraphs);
        Assert.All(StyleEffectif.Decouper(bloc.Text, bloc.Spans, Commun(bloc)),
            morceau => Assert.Equal(Commun(bloc), morceau.Style));
    }

    [AvaloniaFact]
    public void Un_bloc_a_champ_ou_un_fichier_absent_ne_change_rien()
    {
        var bloc = Bloc();
        bloc.Field = FieldKind.PageNumber;
        var (_, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        var avant = DocumentSerializer.ToJson(vm.Document);

        // Sous un champ, le texte est caché et gardé pour le jour où l'on retire le
        // champ : on ne le remplace pas sans qu'on le voie.
        Assert.False(vm.ImporterTexte(EcrireEnUtf8("cache.txt", "texte caché")));
        Assert.Contains("champ", vm.Status);

        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));

        // Le champ retiré, l'import est permis ; mais un fichier qui ne se lit pas ne
        // pose rien, et le dit.
        bloc.Field = FieldKind.None;
        avant = DocumentSerializer.ToJson(vm.Document);
        Assert.False(vm.ImporterTexte(Path.Combine(_travail, "absent.txt")));
        Assert.StartsWith("Import impossible", vm.Status);

        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void Le_menu_et_le_panneau_offrent_limport_au_seul_bloc_de_texte()
    {
        var bloc = Bloc();
        var forme = new ShapeFrame { Name = "Forme", X = 300, Y = 40, Width = 80, Height = 80 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, forme));

        var menu = fenetre.FindControl<MenuItem>("MenuImporterTexte");
        Assert.NotNull(menu);
        Assert.Equal("Importer un te_xte…", menu.Header);

        Button? Bouton() => fenetre.GetVisualDescendants().OfType<Button>()
            .SingleOrDefault(b => b.Name == "BoutonImporterTexte" && b.IsEffectivelyVisible);

        vm.SelectOnly(null);
        Rendu.Battre();
        Assert.False(menu.IsEnabled, "rien n'est sélectionné, et le menu offre l'import");
        Assert.Null(Bouton());

        vm.SelectOnly(forme);
        Rendu.Battre();
        Assert.False(menu.IsEnabled, "une forme est sélectionnée, et le menu offre l'import");
        Assert.Null(Bouton());

        vm.SelectOnly(bloc);
        Rendu.Battre();
        Assert.True(menu.IsEnabled, "un bloc de texte est sélectionné, et le menu n'offre pas l'import");
        Assert.True(Bouton()?.IsEnabled, "le panneau du bloc de texte n'offre pas l'import");

        // Un champ calcule le texte : ni le menu ni le panneau ne l'offrent plus.
        bloc.Field = FieldKind.Date;
        Rendu.Battre();
        Assert.False(menu.IsEnabled, "le bloc porte un champ, et le menu offre l'import");
        Assert.False(Bouton()?.IsEnabled ?? false, "le bloc porte un champ, et le panneau offre l'import");
    }
}
