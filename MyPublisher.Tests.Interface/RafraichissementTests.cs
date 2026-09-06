using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;
using MyPublisher.Views;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Un réglage du panneau doit se voir tout de suite.
///
/// Le défaut que ces tests attrapent est arrivé avec R1 et a vécu quatre postes : le
/// contrôle d'affichage déclarait <c>AffectsRender</c> sur la propriété qui porte le
/// bloc, or régler le corps ou le retrait ne remplace pas l'instance du bloc — la
/// liaison ne rendait rien de neuf, et rien ne se redessinait. Quinze réglages
/// s'enregistraient et s'exportaient correctement sans que la page bouge, jusqu'à ce
/// qu'on retouche le texte ou qu'on rouvre le document.
///
/// Il est resté invisible parce qu'aucune vérification ne comparait deux images de la
/// même page : les recettes comparaient l'écran au PDF, tous deux calculés à partir du
/// modèle, tous deux justes. C'est l'écran **déjà affiché** qui ne suivait pas.
/// </summary>
public class RafraichissementTests
{
    private static (Window Fenetre, MainWindowViewModel Vm, TextFrame Bloc) Ouvrir()
    {
        var document = Document.CreateDefault();
        var bloc = new TextFrame
        {
            Name = "Texte", X = 40, Y = 40, Width = 300, Height = 200,
            // Deux paragraphes : l'espace après le paragraphe n'a d'effet visible que
            // s'il y a quelque chose en dessous à repousser.
            Text = "Un premier paragraphe, assez long pour se couper en plusieurs lignes.\nUn second.",
            FontFamily = "DejaVu Sans", FontSize = 11, Foreground = "#FF000000",
        };
        document.Pages[0].Frames.Add(bloc);

        var vm = new MainWindowViewModel(
            contacts: new BusinessInfoStore(), horloge: () => new DateTime(2026, 9, 6))
        {
            Document = document,
            FilePath = "banc.mypub",
            Zoom = 1 / Units.PointToDip,
        };
        vm.CurrentPage = document.Pages[0];

        var fenetre = new MainWindow { DataContext = vm, Width = 1400, Height = 900 };
        fenetre.Show();
        Battre();
        return (fenetre, vm, bloc);
    }

    private static void Battre(int tours = 4)
    {
        for (var i = 0; i < tours; i++) Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// L'image RÉELLEMENT composée de la fenêtre.
    ///
    /// Le piège, et il a d'abord eu ma peau : « RenderTargetBitmap.Render » redessine
    /// l'arbre visuel de force, à la demande, quel qu'ait été l'état d'invalidation.
    /// Un test bâti dessus passe donc même quand rien ne se redessine à l'écran — il
    /// mesure le modèle, pas l'affichage. « CaptureRenderedFrame » rend la dernière
    /// image composée, celle que l'utilisateur voit : c'est la seule qui puisse
    /// attraper une invalidation manquante.
    /// </summary>
    /// <remarks>
    /// Le corps est passé dans <see cref="Rendu.Composee"/> au poste A6, qui en avait
    /// besoin pour éprouver la barre d'état. Le raisonnement ci-dessus est ce qui
    /// justifie qu'il n'y en ait qu'une copie : deux, et la seconde retombe sur
    /// « RenderTargetBitmap » sans que personne ne s'en aperçoive.
    /// </remarks>
    private static byte[] Image(Window fenetre) => Rendu.Composee(fenetre).Pixels;

    [AvaloniaTheory]
    // Les quinze réglages du panneau se ramènent à ces onze-là ; on en éprouve un par
    // famille, plus les deux collections d'écarts qui ne changent aucune propriété.
    [InlineData("FontSize")]
    [InlineData("Bold")]
    [InlineData("Italic")]
    [InlineData("Foreground")]
    [InlineData("Align")]
    [InlineData("LineHeight")]
    [InlineData("PaddingLeft")]
    [InlineData("IndentLeft")]
    [InlineData("IndentFirstLine")]
    [InlineData("SpaceAfter")]
    [InlineData("Columns")]
    public void Un_reglage_du_panneau_redessine_la_page(string reglage)
    {
        var (fenetre, _, bloc) = Ouvrir();
        var avant = Image(fenetre);

        switch (reglage)
        {
            case "FontSize": bloc.FontSize = 17; break;
            case "Bold": bloc.Bold = true; break;
            case "Italic": bloc.Italic = true; break;
            case "Foreground": bloc.Foreground = "#FFCC0000"; break;
            case "Align": bloc.Align = TextAlign.Right; break;
            case "LineHeight": bloc.LineHeight = 2.2; break;
            case "PaddingLeft": bloc.PaddingLeft = 40; break;
            case "IndentLeft": bloc.IndentLeft = 40; break;
            case "IndentFirstLine": bloc.IndentFirstLine = 40; break;
            case "SpaceAfter": bloc.SpaceAfter = 30; break;
            case "Columns": bloc.Columns = 3; break;
        }

        Battre();

        Assert.NotEqual(avant, Image(fenetre));
    }

    [AvaloniaFact]
    public void Ajouter_un_fragment_de_style_redessine_la_page()
    {
        // Aucune propriété du bloc ne change : c'est la collection qui bouge.
        var (fenetre, _, bloc) = Ouvrir();
        var avant = Image(fenetre);

        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 9, FontSize = 20, Bold = true });
        Battre();

        Assert.NotEqual(avant, Image(fenetre));
    }

    [AvaloniaFact]
    public void Ajouter_un_ecart_de_paragraphe_redessine_la_page()
    {
        var (fenetre, _, bloc) = Ouvrir();
        var avant = Image(fenetre);

        bloc.Paragraphs.Add(new ParagraphSpan { Start = 0, Length = 1, IndentLeft = 40 });
        Battre();

        Assert.NotEqual(avant, Image(fenetre));
    }

    [AvaloniaFact]
    public void Changer_de_bloc_cesse_de_suivre_le_precedent()
    {
        // L'abonnement doit se défaire, sinon un bloc retiré de la page continuerait à
        // faire redessiner un contrôle qui ne l'affiche plus.
        var (fenetre, vm, bloc) = Ouvrir();
        var autre = new TextFrame
        {
            Name = "Autre", X = 40, Y = 40, Width = 300, Height = 200, Text = "Autre texte",
        };

        vm.Document.Pages[0].Frames[0] = autre;
        Battre();

        var avant = Image(fenetre);
        bloc.FontSize = 40;          // l'ancien bloc, qui n'est plus affiché
        Battre();

        Assert.Equal(avant, Image(fenetre));
    }
}
