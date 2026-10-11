using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Poste Q2 : le texte composé se détache du bloc qu'il ne montre plus.
///
/// <see cref="ControleDeBloc"/> s'abonne au bloc et à ses deux collections d'écarts —
/// c'est ce qui fait que régler le corps redessine la page. Or un bloc vit bien plus
/// longtemps que les contrôles qui le dessinent : changer de page reconstruit tous les
/// gabarits, et chaque passage laissait un contrôle de plus accroché à chaque bloc de
/// texte. C'est la fuite que C5 a refermée sur <see cref="FondBloc"/>, un contrôle
/// plus loin, et que R1 avait ouverte ici.
///
/// Elle ne se voit d'aucune façon depuis l'extérieur d'un événement : rien ne se
/// dessine de travers, tout se redessine juste un peu plus souvent, et la mémoire
/// monte. On compte donc les abonnés par réflexion, comme pour le fond.
///
/// Ce que la mesure a appris, et qui contredit le plan : changer de page ne laissait
/// pas d'abonné. La liste des blocs de la page retire le contexte de données des
/// gabarits qu'elle démonte, la liaison du bloc retombe à null, et le contrôle se
/// désabonne en perdant son bloc. Le premier test, qui joue le geste réel, passait
/// donc avant le remède ; il reste, parce que c'est le geste que l'utilisateur fait et
/// que rien ne garantit que le conteneur se conduira toujours ainsi. C'est le second
/// qui éprouve le remède lui-même : un contrôle qui quitte l'arbre EN GARDANT son
/// bloc — une fenêtre fermée sur un document qui reste ouvert, un aperçu, une
/// vignette. Sans le remède, vingt passages y laissent vingt abonnés.
///
/// Ce qui est compté, précisément : les abonnés qui SONT des contrôles de bloc. Le
/// bloc en a d'autres — les liaisons d'Avalonia, le modèle de vue quand il est
/// sélectionné — qui ne sont pas de ce poste et dont le nombre ne dit rien de lui.
/// Et ce qui est attendu n'est pas « un » mais « autant que de contrôles qui le
/// montrent » : un bloc de texte affiché en a deux, le texte et le marqueur de
/// débordement, et un bloc hors de la page n'en a aucun.
/// </summary>
public class DetachementTests
{
    /// <summary>
    /// Deux pages. La première porte un bloc de texte muni d'un fragment et d'un écart
    /// de paragraphe, et un tableau dont une case porte un fragment : les cases sont
    /// des blocs de texte calculés à chaque lecture, qui EMPRUNTENT la collection de
    /// fragments de leur case — si le contrôle restait accroché à elle, ce serait la
    /// case, qui vit aussi longtemps que le document, qui retiendrait tout. La seconde
    /// page porte un autre bloc de texte, pour que le va-et-vient en démonte un à
    /// chaque fois.
    /// </summary>
    private static (Window Fenetre, MainWindowViewModel Vm, TextFrame Premier, TableCell Case, TextFrame Second) Ouvrir()
    {
        var premier = new TextFrame
        {
            Name = "Premier", X = 40, Y = 40, Width = 300, Height = 120,
            Text = "Un mot en gras.\nUn second paragraphe.",
            FontFamily = "DejaVu Sans", FontSize = 11,
        };
        premier.Spans.Add(new StyleSpan { Start = 7, Length = 7, Bold = true });
        premier.Paragraphs.Add(new ParagraphSpan { Start = 1, IndentLeft = 20 });

        var tableau = new TableFrame { Name = "Tableau", X = 40, Y = 200, FontFamily = "DejaVu Sans" };
        tableau.Columns.Add(new TableColumn(150));
        tableau.Columns.Add(new TableColumn(150));
        tableau.Rows.Add(new TableRow(30));
        var cellule = new TableCell { Row = 0, Column = 0, Text = "Intitulé" };
        cellule.Spans.Add(new StyleSpan { Start = 0, Length = 4, Bold = true });
        tableau.Cells.Add(cellule);
        tableau.Cells.Add(new TableCell { Row = 0, Column = 1, Text = "Valeur" });

        var second = new TextFrame
        {
            Name = "Second", X = 40, Y = 40, Width = 300, Height = 120,
            Text = "La page d'à côté.", FontFamily = "DejaVu Sans", FontSize = 11,
        };
        second.Spans.Add(new StyleSpan { Start = 3, Length = 4, Italic = true });
        second.Paragraphs.Add(new ParagraphSpan { Start = 0, SpaceAfter = 6 });

        var document = Rendu.Feuille(premier, tableau);
        document.Pages.Add(new Page { Name = "Page 2" });
        document.Pages[1].Frames.Add(second);

        var (fenetre, vm) = Rendu.Ouvrir(document);
        return (fenetre, vm, premier, cellule, second);
    }

    /// <summary>Les contrôles de bloc qui écoutent cet objet par cet événement.</summary>
    private static int Controles(object source, string evenement) =>
        Rendu.Abonnes(source, evenement).Count(abonne => abonne.Target is ControleDeBloc);

    /// <summary>Les contrôles de bloc que la fenêtre montre, pour un bloc donné.</summary>
    private static int Affiches(Window fenetre, Func<TextFrame, bool> montre) =>
        fenetre.GetVisualDescendants().OfType<ControleDeBloc>()
            .Count(controle => controle.Bloc is { } bloc && montre(bloc));

    [AvaloniaFact]
    [Trait("poste", "Q2")]
    public void Vingt_changements_de_page_ne_laissent_aucun_controle_accroche()
    {
        var (fenetre, vm, premier, cellule, second) = Ouvrir();
        var document = vm.Document;

        // Le témoin : sans lui, un rendu qui ne poserait aucun contrôle de bloc
        // passerait l'assertion d'égalité ci-dessous à zéro contre zéro.
        var montres = Affiches(fenetre, bloc => bloc == premier);
        Assert.Equal(2, montres);
        Assert.Equal(montres, Controles(premier, "PropertyChanged"));

        // Vingt changements de page : on revient sur la première, la seconde est
        // démontée. Deux battements du répartiteur par passage, comme dans
        // l'application, où l'utilisateur ne clique pas deux fois dans la même image.
        for (var i = 0; i < 20; i++)
        {
            vm.CurrentPage = document.Pages[(i + 1) % 2];
            Rendu.Battre(2);
        }

        Assert.Same(document.Pages[0], vm.CurrentPage);

        // Le bloc affiché : autant d'abonnés que de contrôles qui le montrent, sur le bloc
        // et sur chacune de ses deux collections.
        Assert.Equal(2, Affiches(fenetre, bloc => bloc == premier));
        Assert.Equal(2, Controles(premier, "PropertyChanged"));
        Assert.Equal(2, Controles(premier.Spans, "CollectionChanged"));
        Assert.Equal(2, Controles(premier.Paragraphs, "CollectionChanged"));

        // La case : ses fragments sont prêtés au bloc calculé qui la dessine. On compare
        // aux contrôles qui la montrent plutôt qu'à un nombre écrit en dur — c'est le
        // modèle qui décide combien de blocs une case devient.
        var surLaCase = Affiches(fenetre, bloc => ReferenceEquals(bloc.Spans, cellule.Spans));
        Assert.True(surLaCase > 0, "la case du tableau n'est montrée par aucun contrôle de bloc");
        Assert.Equal(surLaCase, Controles(cellule.Spans, "CollectionChanged"));

        // Le bloc de la page quittée : plus personne.
        Assert.Equal(0, Affiches(fenetre, bloc => bloc == second));
        Assert.Equal(0, Controles(second, "PropertyChanged"));
        Assert.Equal(0, Controles(second.Spans, "CollectionChanged"));
        Assert.Equal(0, Controles(second.Paragraphs, "CollectionChanged"));
    }

    [AvaloniaFact]
    [Trait("poste", "Q2")]
    public void Un_controle_qui_quitte_l_arbre_en_gardant_son_bloc_cesse_de_l_ecouter()
    {
        // Le cas que le conteneur ne couvre pas : le contrôle sort de l'arbre, et rien
        // ne lui retire son bloc. Il le reçoit en outre AVANT d'être attaché, comme un
        // gabarit qu'on construit — c'est donc l'attachement qui doit l'abonner.
        var bloc = new TextFrame { Text = "Un mot en gras.\nUn second paragraphe." };
        bloc.Spans.Add(new StyleSpan { Start = 7, Length = 7, Bold = true });
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 1, IndentLeft = 20 });

        var planche = new Panel();
        var fenetre = new Window { Content = planche, Width = 300, Height = 200 };
        fenetre.Show();

        for (var i = 0; i < 20; i++)
        {
            var controle = new TexteCompose { Bloc = bloc, Contenu = bloc.Text };
            Assert.Equal(0, Controles(bloc, "PropertyChanged"));

            planche.Children.Add(controle);
            Rendu.Battre(1);

            // Affiché : un abonné, et un seul, sur le bloc et sur ses deux collections.
            Assert.Equal(1, Controles(bloc, "PropertyChanged"));
            Assert.Equal(1, Controles(bloc.Spans, "CollectionChanged"));
            Assert.Equal(1, Controles(bloc.Paragraphs, "CollectionChanged"));

            planche.Children.Remove(controle);
            Rendu.Battre(1);

            Assert.Same(bloc, controle.Bloc);
        }

        Assert.Equal(0, Controles(bloc, "PropertyChanged"));
        Assert.Equal(0, Controles(bloc.Spans, "CollectionChanged"));
        Assert.Equal(0, Controles(bloc.Paragraphs, "CollectionChanged"));
    }

    [AvaloniaFact]
    [Trait("poste", "Q2")]
    public void Revenu_sur_sa_page_le_texte_se_redessine_encore_quand_on_le_regle()
    {
        // L'autre moitié du remède, et la plus facile à perdre : se détacher en
        // quittant l'arbre ne suffit pas, il faut se rattacher en y revenant. Un
        // contrôle reconstruit reçoit son bloc AVANT d'être attaché — c'est donc
        // l'attachement qui l'abonne, et un remède qui l'oublierait laisserait une
        // page revenue sourde à tous les réglages du panneau. La fuite serait
        // refermée, et le défaut de R1 rouvert.
        var (fenetre, vm, premier, _, _) = Ouvrir();

        vm.CurrentPage = vm.Document.Pages[1];
        Rendu.Battre(2);
        vm.CurrentPage = vm.Document.Pages[0];
        Rendu.Battre();

        var avant = Rendu.Composee(fenetre).Pixels;
        premier.FontSize = 19;
        Rendu.Battre();

        Assert.NotEqual(avant, Rendu.Composee(fenetre).Pixels);

        // Et la même chose par une collection, qui ne change aucune propriété du bloc.
        avant = Rendu.Composee(fenetre).Pixels;
        premier.Spans.Add(new StyleSpan { Start = 0, Length = 2, FontSize = 30 });
        Rendu.Battre();

        Assert.NotEqual(avant, Rendu.Composee(fenetre).Pixels);
    }
}
