using System;
using System.Buffers.Binary;
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
/// Le souligné à l'écran et dans le modèle de vue — poste I7.
///
/// L'encre est mesurée contre la table <c>post</c> du fichier de police, lue ici à la
/// main comme la recette la lit : comparer l'écran à ce qu'Avalonia rend des métriques
/// prouverait leur accord, jamais leur justesse — un signe inversé dans la lecture de la
/// position passerait. La comparaison au PDF est celle de la recette
/// (<c>Scripts/recettes/I7-souligne.sh</c>), qui mesure les deux rendus de toutes les
/// sortes.
///
/// La commande n'a pas encore de bouton : le bouton S, <c>Ctrl+U</c> et le menu viennent
/// de I3. On l'éprouve donc par le modèle de vue, avec les deux règles de la phase qui la
/// concernent : un geste, un pas d'annulation, même au milieu d'une saisie.
/// </summary>
public class SouligneEcranTests
{
    private const string Texte = "MINE ET LE FILET";

    private static TextFrame Bloc(Souligne souligne) => new()
    {
        Name = "Souligné", X = 60, Y = 80, Width = 400, Height = 60, Text = Texte,
        FontFamily = "DejaVu Sans", FontSize = 24, Foreground = "#FF000000", LineHeight = 1,
        PaddingLeft = 0, PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0,
        Underline = souligne,
    };

    /// <summary>
    /// La montée (« hhea ») et la place du souligné (« post ») du fichier que fontconfig
    /// désigne pour la famille, en unités de l'em : ce que la police dit d'elle-même.
    /// </summary>
    private static (double Em, double Montee, double Haut, double Epaisseur) Police(string famille)
    {
        var donnees = File.ReadAllBytes(FontconfigFontResolver.Resoudre(famille, false, false).Fichier);
        var tables = new System.Collections.Generic.Dictionary<string, int>();
        var nombre = BinaryPrimitives.ReadUInt16BigEndian(donnees.AsSpan(4));
        for (var i = 0; i < nombre; i++)
        {
            var entree = donnees.AsSpan(12 + 16 * i);
            tables[System.Text.Encoding.ASCII.GetString(entree[..4])] =
                (int)BinaryPrimitives.ReadUInt32BigEndian(entree[8..]);
        }

        var em = BinaryPrimitives.ReadUInt16BigEndian(donnees.AsSpan(tables["head"] + 18));
        var montee = BinaryPrimitives.ReadInt16BigEndian(donnees.AsSpan(tables["hhea"] + 4));
        var position = BinaryPrimitives.ReadInt16BigEndian(donnees.AsSpan(tables["post"] + 8));
        var epaisseur = BinaryPrimitives.ReadInt16BigEndian(donnees.AsSpan(tables["post"] + 10));
        // La table donne le haut du trait, négatif sous la ligne de base.
        return (em, montee, -position, epaisseur);
    }

    /// <summary>La couverture d'encre d'un pixel, de 0 (blanc) à 1 (noir).</summary>
    private static double Encre(Rendu.Image image, int x, int y) =>
        x < 0 || y < 0 || x >= image.Largeur || y >= image.Hauteur
            ? 0
            : (255 - image.Pixels[(y * image.Largeur + x) * 4]) / 255.0;

    /// <summary>
    /// Le trait sous un point de la page, en points : le haut de son encre et son
    /// épaisseur, sommées sur la colonne entre deux ordonnées. Le lissage n'y change rien :
    /// une rangée à moitié couverte compte pour une demi-rangée.
    /// </summary>
    private static (double Haut, double Epaisseur) Trait(Rendu.Image image, double x, double haut, double bas)
    {
        var colonne = (int)(x * Rendu.PixelsParPoint);
        double somme = 0, moment = 0;
        for (var y = (int)(haut * Rendu.PixelsParPoint); y <= (int)(bas * Rendu.PixelsParPoint); y++)
        {
            var c = Encre(image, colonne, y);
            somme += c;
            moment += c * (y + 0.5);
        }

        if (somme == 0) return (double.NaN, 0);
        var epaisseur = somme / Rendu.PixelsParPoint;
        return (moment / somme / Rendu.PixelsParPoint - epaisseur / 2, epaisseur);
    }

    [AvaloniaFact]
    [Trait("poste", "I7")]
    public void Lecran_pose_le_trait_ou_la_table_post_le_met()
    {
        var bloc = Bloc(Souligne.Simple);
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var image = Rendu.Encre(fenetre);

        var (em, montee, haut, epaisseur) = Police("DejaVu Sans");
        var corps = bloc.FontSize;
        var ligneDeBase = bloc.Y + montee * corps / em;

        // Sous le blanc qui suit « MINE » : rien d'autre que le trait dans cette colonne.
        var x = bloc.X + 0.5 * new MetriquesAvalonia().Largeur("MINE ", Charte.Defaut.StyleCommun(bloc))
                + 0.5 * new MetriquesAvalonia().Largeur("MINE", Charte.Defaut.StyleCommun(bloc));
        var trait = Trait(image, x, ligneDeBase - 2, ligneDeBase + 6);

        Assert.True(Math.Abs(trait.Haut - (ligneDeBase + haut * corps / em)) < 0.25,
            $"le haut du trait est à {trait.Haut - ligneDeBase:0.00} pt sous la ligne de base, " +
            $"la table « post » le met à {haut * corps / em:0.00}");
        Assert.True(Math.Abs(trait.Epaisseur - epaisseur * corps / em) < 0.2,
            $"le trait a {trait.Epaisseur:0.00} pt d'épaisseur, la table « post » en donne {epaisseur * corps / em:0.00}");
    }

    [AvaloniaFact]
    [Trait("poste", "I7")]
    public void Lecran_trace_les_traits_que_le_controle_annonce_et_rien_sans_souligne()
    {
        var souligne = Bloc(new Souligne(TraitDeSouligne.Solid, @double: true));
        var nu = Bloc(Souligne.Aucun);
        nu.Y = 200;
        var (fenetre, _) = Rendu.Ouvrir(Rendu.Feuille(souligne, nu));
        var image = Rendu.Encre(fenetre);

        var controle = fenetre.GetVisualDescendants().OfType<TexteCompose>()
            .Single(c => ReferenceEquals(c.Bloc, souligne));
        var traits = controle.Souligner(new MetriquesAvalonia());
        Assert.Equal(2, traits.Count);

        var origine = controle.TranslatePoint(new Point(0, 0),
            fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot"))!.Value;
        var x = origine.X + (traits[0].Debut + traits[0].Fin) / 2;
        foreach (var t in traits)
        {
            var mesure = Trait(image, x, origine.Y + t.Axe - t.Epaisseur, origine.Y + t.Axe + t.Epaisseur);
            Assert.True(Math.Abs(mesure.Haut - (origine.Y + t.Axe - t.Epaisseur / 2)) < 0.25,
                $"un trait annoncé à {t.Axe:0.00} est tracé à {mesure.Haut - origine.Y + t.Epaisseur / 2:0.00}");
            Assert.True(Math.Abs(mesure.Epaisseur - t.Epaisseur) < 0.2);
        }

        // Le même texte sans souligné : pas une goutte d'encre sous sa ligne de base.
        var (em, montee, _, _) = Police("DejaVu Sans");
        var ligneDeBase = nu.Y + montee * nu.FontSize / em;
        Assert.Equal(0, Trait(image, x - souligne.X + nu.X, ligneDeBase + 0.3, ligneDeBase + 6).Epaisseur);
    }

    // ------------------------------------------------------------------ la commande

    private static (Window Fenetre, MainWindowViewModel Vm, TextFrame Bloc) Ouvrir(Souligne souligne)
    {
        var bloc = Bloc(souligne);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc, new ShapeFrame { X = 300, Y = 300 }));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        return (fenetre, vm, bloc);
    }

    [AvaloniaFact]
    [Trait("poste", "I7")]
    public void La_commande_souligne_le_bloc_et_lui_retire_son_souligne_en_un_pas_chacun()
    {
        var pointille = new Souligne(TraitDeSouligne.Dotted, thick: true);
        var (_, vm, bloc) = Ouvrir(Souligne.Aucun);
        var avant = DocumentSerializer.ToJson(vm.Document);

        vm.SoulignerCommand.Execute(null);
        Assert.Equal(Souligne.Simple, bloc.Underline);
        Assert.True(vm.IsDirty);

        // Une sorte quelconque se retire d'un geste, comme Ctrl+U le fait dans Publisher.
        bloc.Underline = pointille;
        vm.SoulignerCommand.Execute(null);
        Assert.Equal(Souligne.Aucun, bloc.Underline);

        // Deux gestes de la commande, deux pas ; le changement de sorte, fait à la main
        // hors de toute transaction, n'en a pas.
        vm.UndoCommand.Execute(null);
        Assert.Equal(pointille, ((TextFrame)vm.Document.Pages[0].Frames[0]).Underline);
        vm.UndoCommand.Execute(null);
        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
    }

    [AvaloniaFact]
    [Trait("poste", "I7")]
    public void Pendant_une_saisie_la_commande_a_son_pas_et_la_frappe_qui_suit_le_sien()
    {
        var (_, vm, bloc) = Ouvrir(Souligne.Aucun);

        // La saisie sur la page ouvre sa transaction à l'entrée et la ferme à la sortie
        // (PageCanvas.BeginTextEdit) : on la rejoue par le modèle de vue.
        vm.BeginEdit();
        bloc.Saisie = Texte + " UN";
        vm.SoulignerCommand.Execute(null);
        bloc.Saisie = Texte + " UN DEUX";
        vm.CommitEdit();

        Assert.Equal(Souligne.Simple, bloc.Underline);

        vm.UndoCommand.Execute(null);
        var b = (TextFrame)vm.Document.Pages[0].Frames[0];
        Assert.Equal((Texte + " UN", Souligne.Simple), (b.Text, b.Underline));

        vm.UndoCommand.Execute(null);
        b = (TextFrame)vm.Document.Pages[0].Frames[0];
        Assert.Equal((Texte + " UN", Souligne.Aucun), (b.Text, b.Underline));

        vm.UndoCommand.Execute(null);
        b = (TextFrame)vm.Document.Pages[0].Frames[0];
        Assert.Equal((Texte, Souligne.Aucun), (b.Text, b.Underline));
    }

    [AvaloniaFact]
    [Trait("poste", "I7")]
    public void Sans_bloc_de_texte_la_commande_ne_fait_rien_et_le_dit()
    {
        var (_, vm, _) = Ouvrir(Souligne.Aucun);
        vm.SelectOnly(vm.Document.Pages[0].Frames.OfType<ShapeFrame>().Single());
        var avant = DocumentSerializer.ToJson(vm.Document);

        vm.SoulignerCommand.Execute(null);

        Assert.Equal(avant, DocumentSerializer.ToJson(vm.Document));
        Assert.False(vm.CanUndo);
        Assert.Contains("bloc de texte", vm.Status);
    }

    [AvaloniaFact]
    [Trait("poste", "I7")]
    public void Une_copie_du_bloc_garde_son_souligne_et_celui_de_ses_fragments()
    {
        var (_, vm, bloc) = Ouvrir(new Souligne(TraitDeSouligne.Dash));
        bloc.Spans.Add(new StyleSpan { Start = 5, Length = 2, Underline = new Souligne(TraitDeSouligne.Wave) });

        vm.DuplicateSelectedCommand.Execute(null);

        var copie = Assert.IsType<TextFrame>(vm.Document.Pages[0].Frames[^1]);
        Assert.NotSame(bloc, copie);
        Assert.Equal(new Souligne(TraitDeSouligne.Dash), copie.Underline);
        Assert.Equal(new Souligne(TraitDeSouligne.Wave), copie.Spans.Single().Underline);
    }

    // ------------------------------------------------------------------ la frappe

    [AvaloniaFact]
    [Trait("poste", "I7")]
    public void Le_souligne_suit_la_frappe_sur_la_page_comme_le_gras()
    {
        var double_ = new Souligne(TraitDeSouligne.Solid, @double: true);
        var bloc = Bloc(Souligne.Aucun);
        // « FILET », souligné deux fois, en fin de texte.
        bloc.Spans.Add(new StyleSpan { Start = Texte.Length - 5, Length = 5, Underline = double_ });
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        // Entrer en saisie comme l'utilisateur : le bloc pris, puis un double-clic.
        Rendu.EntrerEnSaisie(fenetre, vm, bloc, dy: 10);

        // Au bout du texte, donc au bout du fragment : la frappe prend le style du
        // caractère qui la précède (Q1).
        fenetre.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.Control);
        fenetre.KeyTextInput("S");
        Rendu.Battre();
        fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Rendu.Battre();

        Assert.Equal(Texte + "S", bloc.Text);
        var fragment = Assert.Single(bloc.Spans);
        Assert.Equal((Texte.Length - 5, 6, double_), (fragment.Start, fragment.Length, fragment.Underline));

        // Et ce que l'écran trace : un trait double sous « FILETS », jusqu'à son « S ».
        var controle = fenetre.GetVisualDescendants().OfType<TexteCompose>().Single(c => ReferenceEquals(c.Bloc, bloc));
        var metriques = new MetriquesAvalonia();
        var traits = controle.Souligner(metriques);
        var filets = controle.Poser(metriques, metriques).Last();
        Assert.Equal("FILETS", filets.Texte);
        Assert.Equal(2, traits.Count);
        Assert.All(traits, t =>
        {
            Assert.Equal(filets.Ancre.X, t.Debut, 6);
            Assert.Equal(filets.Ancre.X + filets.Rendu.WidthIncludingTrailingWhitespace, t.Fin, 6);
        });
    }
}
