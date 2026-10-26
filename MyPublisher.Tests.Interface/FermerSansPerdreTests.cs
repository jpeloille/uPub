using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
/// Ne rien perdre en fermant, retrouver ses documents — poste N11 (fiche 23, d19, e19, f25).
///
/// Avant le poste, ni Nouveau, ni Ouvrir, ni Quitter, ni la fermeture de la fenêtre ne
/// demandaient rien : <c>Alt+F4</c> jetait les modifications sans un mot. Ils demandent
/// désormais, avant de perdre : Enregistrer, Ne pas enregistrer, Annuler ; Fermer
/// (<c>Ctrl+W</c>, <c>Ctrl+F4</c>) ferme le document et rend le catalogue des modèles ; les six
/// derniers documents sont au menu Fichier, numérotés, et survivent à un redémarrage.
///
/// La question n'est branchée que par <c>App.axaml.cs</c> : une fenêtre de test ne demande rien.
/// Ces tests la branchent comme l'application, par la même ligne (<see cref="Brancher"/>), et
/// jouent le vrai dialogue, au pointeur et au clavier, aux gestes du banc (<see cref="Rendu"/>,
/// poste N1). Les documents s'écrivent dans un dossier temporaire, les préférences aussi : rien
/// n'est écrit dans la configuration de la personne. La recette du poste —
/// <c>Scripts/recettes/N11-fermer-sans-perdre.sh</c> — joue ces tests par leur trait.
/// </summary>
[Trait("poste", "N11")]
public sealed class FermerSansPerdreTests : IDisposable
{
    private readonly string _dossier = Path.Combine(Path.GetTempPath(), "n11-" + Guid.NewGuid().ToString("N"));

    public FermerSansPerdreTests() => Directory.CreateDirectory(_dossier);

    public void Dispose() => Directory.Delete(_dossier, recursive: true);

    // ------------------------------------------------------------------ outillage

    /// <summary>La question branchée comme <c>App.axaml.cs</c> la branche : la même ligne.</summary>
    private static void Brancher(MainWindow fenetre) =>
        fenetre.AvantDePerdre = nom => QuestionAvantDePerdre.Poser(fenetre, nom);

    private static ShapeFrame Carre() =>
        new() { Name = "Carré", X = 100, Y = 100, Width = 60, Height = 60, Fill = new SolidFill("#FFCC0000") };

    private static TextFrame Texte() => new()
    {
        Name = "Bloc", X = 60, Y = 260, Width = 300, Height = 80, Text = "Texte",
        FontFamily = "DejaVu Sans", FontSize = 10.5, Foreground = "#FF000000",
    };

    private string Chemin(string nom) => Path.Combine(_dossier, nom);

    /// <summary>
    /// La fenêtre sur « principal.mypub », enregistré sur le disque, et un second document,
    /// « autre.mypub », ouvert avant lui : il est le deuxième des récents. La question branchée.
    /// </summary>
    private (MainWindow Fenetre, MainWindowViewModel Vm) Ouvrir(Preferences? preferences = null)
    {
        DocumentSerializer.Save(Rendu.Feuille(Texte()), Chemin("autre.mypub"));
        DocumentSerializer.Save(Rendu.Feuille(Carre(), Texte()), Chemin("principal.mypub"));

        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(), preferences);
        vm.LoadFrom(Chemin("autre.mypub"));
        vm.LoadFrom(Chemin("principal.mypub"));
        Rendu.Battre();

        var principale = Assert.IsType<MainWindow>(fenetre);
        Brancher(principale);
        return (principale, vm);
    }

    /// <summary>Le carré pris et poussé de 3 pt à la flèche : un pas, le document modifié.</summary>
    private static void Modifier(MainWindow fenetre, MainWindowViewModel vm)
    {
        vm.SelectOnly(vm.CurrentPage!.Frames.OfType<ShapeFrame>().Single());
        Rendu.Battre();
        Rendu.Touche(fenetre, PhysicalKey.ArrowRight);
        Assert.True(vm.IsDirty, "la flèche n'a pas modifié le document");
    }

    private static double XDuCarre(Document document) =>
        document.Pages[0].Frames.OfType<ShapeFrame>().Single().X;

    /// <summary>La question à l'écran, ou <c>null</c>, une fois que la fenêtre a fait ce qu'elle avait à faire.</summary>
    private static QuestionAvantDePerdre? LaQuestion(MainWindow fenetre)
    {
        Rendu.UneImage();
        return fenetre.OwnedWindows.OfType<QuestionAvantDePerdre>().SingleOrDefault();
    }

    private static TemplateChooserWindow? LeCatalogue(MainWindow fenetre)
    {
        Rendu.UneImage();
        return fenetre.OwnedWindows.OfType<TemplateChooserWindow>().SingleOrDefault();
    }

    private static Button Bouton(Window dialogue, string nom) =>
        dialogue.GetVisualDescendants().OfType<Button>().SingleOrDefault(b => b.Name == nom)
        ?? throw new InvalidOperationException($"le dialogue n'a pas de bouton « {nom} »");

    /// <summary>
    /// Une entrée du menu Fichier, le menu ouvert d'un clic : hors de l'arbre visuel tant que son
    /// menu est replié, elle est cherchée dans l'arbre logique.
    /// </summary>
    private static MenuItem EntreeDuMenuFichier(MainWindow fenetre, Func<MenuItem, bool> laquelle, string quoi)
    {
        var fichier = fenetre.GetLogicalDescendants().OfType<MenuItem>().Single(m => m.Name == "MenuFichier");
        if (!fichier.IsSubMenuOpen) Rendu.Cliquer(fichier);
        return fichier.Items.OfType<MenuItem>().SingleOrDefault(laquelle)
               ?? throw new InvalidOperationException($"le menu Fichier n'a pas d'entrée {quoi}");
    }

    private static string[] Recents(MainWindow fenetre) =>
        fenetre.GetLogicalDescendants().OfType<MenuItem>().Single(m => m.Name == "MenuFichier")
            .Items.OfType<MenuItem>().Where(m => m.Name?.StartsWith("MenuRecent") == true)
            .Select(m => $"{m.Header}").ToArray();

    // ------------------------------------------------------------------ les gestes qui perdent

    public enum Geste { Nouveau, Ouvrir, Fermer, Fenetre }

    /// <summary>
    /// Le geste, joué comme l'utilisateur : <c>Ctrl+N</c> puis « Créer » au catalogue, la page
    /// blanche choisie ; le deuxième document récent au menu Fichier ; <c>Ctrl+W</c> ; Quitter
    /// au menu Fichier, qui ferme la fenêtre comme <c>Alt+F4</c>.
    /// </summary>
    private static void Jouer(Geste geste, MainWindow fenetre)
    {
        switch (geste)
        {
            case Geste.Nouveau:
                Rendu.Touche(fenetre, PhysicalKey.N, RawInputModifiers.Control);
                var catalogue = LeCatalogue(fenetre) ?? throw new InvalidOperationException("Ctrl+N n'a pas ouvert le catalogue");
                Assert.True(Assert.IsType<TemplateChooserViewModel>(catalogue.DataContext).Selected?.IsBlank);
                Rendu.Touche(catalogue, PhysicalKey.Enter);
                break;
            case Geste.Ouvrir:
                Rendu.Cliquer(EntreeDuMenuFichier(fenetre, m => m.Name == "MenuRecent2", "« 2 »"));
                break;
            case Geste.Fermer:
                Rendu.Touche(fenetre, PhysicalKey.W, RawInputModifiers.Control);
                break;
            case Geste.Fenetre:
                Rendu.Cliquer(EntreeDuMenuFichier(fenetre, m => m.Name == "MenuQuitter", "« Quitter »"));
                break;
        }
    }

    /// <summary>Le geste est allé au bout : le document a été remplacé, fermé, ou la fenêtre.</summary>
    private void Abouti(Geste geste, MainWindow fenetre, MainWindowViewModel vm, bool fermee)
    {
        switch (geste)
        {
            case Geste.Nouveau:
                Assert.Null(vm.FilePath);
                Assert.Empty(vm.CurrentPage!.Frames);
                break;
            case Geste.Ouvrir:
                Assert.Equal(Chemin("autre.mypub"), vm.FilePath);
                break;
            case Geste.Fermer:
                Assert.Null(vm.FilePath);
                Assert.Empty(vm.CurrentPage!.Frames);
                var catalogue = LeCatalogue(fenetre);
                Assert.True(catalogue is not null, "Fermer n'a pas rendu le catalogue des modèles");
                catalogue.Close();
                Rendu.Battre();
                break;
            case Geste.Fenetre:
                Assert.True(fermee, "la fenêtre ne s'est pas fermée");
                return;
        }

        Assert.False(fermee, "la fenêtre s'est fermée");
        Assert.False(vm.IsDirty);
    }

    public static TheoryData<Geste> Gestes() => new() { Geste.Nouveau, Geste.Ouvrir, Geste.Fermer, Geste.Fenetre };

    /// <summary>Annuler laisse tout tel quel : la fenêtre, le document, ses modifications, le fichier.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Gestes))]
    public void Annuler_laisse_tout_tel_quel(Geste geste)
    {
        var (fenetre, vm) = Ouvrir();
        var fermee = false;
        fenetre.Closed += (_, _) => fermee = true;
        Modifier(fenetre, vm);
        var etat = DocumentSerializer.ToJson(vm.Document);
        var fichier = File.ReadAllText(Chemin("principal.mypub"));

        Jouer(geste, fenetre);
        var question = LaQuestion(fenetre);
        Assert.True(question is not null, $"{geste} sur un document modifié n'a rien demandé");
        Assert.Contains("«\u00A0principal.mypub\u00A0»", question.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Question").Text);
        Rendu.Cliquer(Bouton(question, "BoutonAnnuler"));

        Assert.Null(LaQuestion(fenetre));
        Assert.False(fermee, "Annuler a laissé la fenêtre se fermer");
        Assert.Null(LeCatalogue(fenetre));
        Assert.Equal(Chemin("principal.mypub"), vm.FilePath);
        Assert.True(vm.IsDirty, "Annuler a effacé la marque des modifications");
        Assert.Equal(etat, DocumentSerializer.ToJson(vm.Document));
        Assert.Equal(fichier, File.ReadAllText(Chemin("principal.mypub")));
        Assert.True(vm.CanUndo, "Annuler a vidé l'historique");
    }

    /// <summary>Ne pas enregistrer laisse le geste aller au bout, et le fichier tel qu'il était.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Gestes))]
    public void Ne_pas_enregistrer_ferme_sans_ecrire(Geste geste)
    {
        var (fenetre, vm) = Ouvrir();
        var fermee = false;
        fenetre.Closed += (_, _) => fermee = true;
        Modifier(fenetre, vm);
        var fichier = File.ReadAllText(Chemin("principal.mypub"));

        Jouer(geste, fenetre);
        var question = LaQuestion(fenetre);
        Assert.True(question is not null, $"{geste} sur un document modifié n'a rien demandé");
        Rendu.Cliquer(Bouton(question, "BoutonNePasEnregistrer"));

        Assert.Null(LaQuestion(fenetre));
        Abouti(geste, fenetre, vm, fermee);
        Assert.Equal(fichier, File.ReadAllText(Chemin("principal.mypub")));
    }

    /// <summary>Enregistrer écrit le document, puis laisse le geste aller au bout.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Gestes))]
    public void Enregistrer_ecrit_puis_ferme(Geste geste)
    {
        var (fenetre, vm) = Ouvrir();
        var fermee = false;
        fenetre.Closed += (_, _) => fermee = true;
        var avant = XDuCarre(vm.Document);
        Modifier(fenetre, vm);

        Jouer(geste, fenetre);
        var question = LaQuestion(fenetre);
        Assert.True(question is not null, $"{geste} sur un document modifié n'a rien demandé");
        Rendu.Cliquer(Bouton(question, "BoutonEnregistrer"));

        Assert.Null(LaQuestion(fenetre));
        Assert.Equal(avant + 3, XDuCarre(DocumentSerializer.Load(Chemin("principal.mypub"))), 6);
        Abouti(geste, fenetre, vm, fermee);
    }

    /// <summary>Un document intact ne fait rien demander : le geste va au bout, comme avant le poste.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Gestes))]
    public void Un_document_intact_ne_fait_rien_demander(Geste geste)
    {
        var (fenetre, vm) = Ouvrir();
        var fermee = false;
        fenetre.Closed += (_, _) => fermee = true;
        Assert.False(vm.IsDirty);

        Jouer(geste, fenetre);
        Assert.Null(LaQuestion(fenetre));
        Abouti(geste, fenetre, vm, fermee);
    }

    /// <summary>
    /// Le piège du poste, vu du juge : la fenêtre qui se ferme sur un document modifié — la
    /// fermeture du système, <c>Alt+F4</c> ou la case de la barre de titre, que joue
    /// <c>Close()</c> — demande, et reste ouverte tant qu'on n'a pas répondu.
    /// </summary>
    [AvaloniaFact]
    public void La_fenetre_fermee_sur_un_document_modifie_demande_et_attend()
    {
        var (fenetre, vm) = Ouvrir();
        var fermee = false;
        fenetre.Closed += (_, _) => fermee = true;
        Modifier(fenetre, vm);

        fenetre.Close();
        var question = LaQuestion(fenetre);
        Assert.True(question is not null, "la fenêtre s'est fermée sur un document modifié sans rien demander");
        Assert.False(fermee, "la fenêtre s'est fermée avant la réponse");

        // Une seconde fermeture pendant la question ne la pose pas deux fois, et ne ferme rien.
        fenetre.Close();
        Rendu.UneImage();
        Assert.Single(fenetre.OwnedWindows.OfType<QuestionAvantDePerdre>());
        Assert.False(fermee);

        Rendu.Cliquer(Bouton(question, "BoutonNePasEnregistrer"));
        Assert.True(fermee, "Ne pas enregistrer n'a pas fermé la fenêtre");
    }

    /// <summary>Fermer a ses deux touches, et son entrée au menu Fichier.</summary>
    [AvaloniaTheory]
    [InlineData("Ctrl+W")]
    [InlineData("Ctrl+F4")]
    [InlineData("menu")]
    public void Fermer_au_clavier_ou_au_menu_demande_puis_rend_le_catalogue(string comment)
    {
        var (fenetre, vm) = Ouvrir();
        Modifier(fenetre, vm);

        switch (comment)
        {
            case "Ctrl+W": Rendu.Touche(fenetre, PhysicalKey.W, RawInputModifiers.Control); break;
            case "Ctrl+F4": Rendu.Touche(fenetre, PhysicalKey.F4, RawInputModifiers.Control); break;
            default: Rendu.Cliquer(EntreeDuMenuFichier(fenetre, m => m.Name == "MenuFermer", "« Fermer »")); break;
        }

        var question = LaQuestion(fenetre);
        Assert.True(question is not null, $"Fermer par {comment} n'a rien demandé");
        Rendu.Cliquer(Bouton(question, "BoutonNePasEnregistrer"));

        var catalogue = LeCatalogue(fenetre);
        Assert.True(catalogue is not null, "Fermer n'a pas rendu le catalogue des modèles");
        Assert.Null(vm.FilePath);
        Assert.Empty(vm.CurrentPage!.Frames);
        Assert.False(vm.CanUndo, "le document fermé a laissé son historique");

        // Le catalogue abandonné laisse la page blanche sans titre.
        Rendu.Cliquer(catalogue.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Annuler")));
        Assert.Null(LeCatalogue(fenetre));
        Assert.Equal("Sans titre — MyPublisher", vm.WindowTitle);
    }

    /// <summary>
    /// Une saisie ouverte est d'abord refermée, pour que sa frappe compte : « abc » tapé dans un
    /// bloc, sans quitter la saisie, fait demander à <c>Ctrl+W</c>, et Enregistrer l'écrit.
    /// </summary>
    [AvaloniaFact]
    public void La_frappe_d_une_saisie_ouverte_compte()
    {
        var (fenetre, vm) = Ouvrir();
        var bloc = vm.CurrentPage!.Frames.OfType<TextFrame>().Single();
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.Touche(fenetre, PhysicalKey.End, RawInputModifiers.Control);
        Rendu.Taper(fenetre, "abc");
        Assert.True(bloc.IsEditing);
        Assert.Equal("Texteabc", zone.Text);

        Rendu.Touche(fenetre, PhysicalKey.W, RawInputModifiers.Control);
        var question = LaQuestion(fenetre);
        Assert.True(question is not null, "la frappe d'une saisie ouverte n'a pas fait demander");
        Assert.False(bloc.IsEditing, "la saisie est restée ouverte sous la question");
        Rendu.Cliquer(Bouton(question, "BoutonEnregistrer"));

        var relu = DocumentSerializer.Load(Chemin("principal.mypub"));
        Assert.Equal("Texteabc", relu.Pages[0].Frames.OfType<TextFrame>().Single().Text);
        LeCatalogue(fenetre)?.Close();
    }

    /// <summary>
    /// Le dialogue a un bouton par défaut et un bouton d'annulation : Entrée enregistre, Échap
    /// annule ; refermé par sa barre de titre, il annule aussi.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("Entrée")]
    [InlineData("Échap")]
    [InlineData("barre de titre")]
    public void Le_dialogue_a_un_bouton_par_defaut_et_un_bouton_d_annulation(string reponse)
    {
        var (fenetre, vm) = Ouvrir();
        Modifier(fenetre, vm);
        var avant = XDuCarre(vm.Document) - 3;

        Rendu.Touche(fenetre, PhysicalKey.W, RawInputModifiers.Control);
        var question = LaQuestion(fenetre) ?? throw new InvalidOperationException("Ctrl+W n'a rien demandé");
        Assert.True(Bouton(question, "BoutonEnregistrer").IsDefault);
        Assert.False(Bouton(question, "BoutonEnregistrer").IsCancel);
        Assert.True(Bouton(question, "BoutonAnnuler").IsCancel);
        Assert.False(Bouton(question, "BoutonAnnuler").IsDefault);
        Assert.False(Bouton(question, "BoutonNePasEnregistrer").IsDefault || Bouton(question, "BoutonNePasEnregistrer").IsCancel);

        switch (reponse)
        {
            case "Entrée": Rendu.Touche(question, PhysicalKey.Enter); break;
            case "Échap": Rendu.Touche(question, PhysicalKey.Escape); break;
            default: question.Close(); Rendu.Battre(); break;
        }

        Assert.Null(LaQuestion(fenetre));
        var relu = XDuCarre(DocumentSerializer.Load(Chemin("principal.mypub")));
        if (reponse == "Entrée")
        {
            Assert.Equal(avant + 3, relu, 6);
            Assert.Null(vm.FilePath);
            LeCatalogue(fenetre)?.Close();
        }
        else
        {
            Assert.Equal(avant, relu, 6);
            Assert.Equal(Chemin("principal.mypub"), vm.FilePath);
            Assert.True(vm.IsDirty);
            Assert.Null(LeCatalogue(fenetre));
        }
    }

    /// <summary>
    /// Enregistrer qui ne peut pas écrire ne perd rien : un document sans titre, dont
    /// « Enregistrer sous » est abandonné — la plate-forme sans affichage referme le sélecteur
    /// sans rien rendre —, ou un chemin où l'on ne peut pas écrire, laisse la fenêtre ouverte sur
    /// le document modifié.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Enregistrer_qui_echoue_ne_perd_rien(bool sansTitre)
    {
        var (fenetre, vm) = Ouvrir();
        var fermee = false;
        fenetre.Closed += (_, _) => fermee = true;
        Modifier(fenetre, vm);
        vm.FilePath = sansTitre ? null : Path.Combine(_dossier, "absent", "principal.mypub");
        var etat = DocumentSerializer.ToJson(vm.Document);

        fenetre.Close();
        var question = LaQuestion(fenetre) ?? throw new InvalidOperationException("la fermeture n'a rien demandé");
        Assert.Contains(sansTitre ? "«\u00A0Sans titre\u00A0»" : "«\u00A0principal.mypub\u00A0»",
            question.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Question").Text);
        Rendu.Cliquer(Bouton(question, "BoutonEnregistrer"));

        Assert.False(fermee, "un enregistrement qui n'a pas eu lieu a laissé la fenêtre se fermer");
        Assert.True(vm.IsDirty);
        Assert.Equal(etat, DocumentSerializer.ToJson(vm.Document));
        if (!sansTitre) Assert.StartsWith("Enregistrement impossible", vm.Status);
    }

    // ------------------------------------------------------------------ les documents récents

    /// <summary>
    /// Les documents ouverts ou enregistrés sont au menu Fichier, numérotés, le plus récent en
    /// tête, sans doublon, six au plus ; ils survivent à un redémarrage — un second magasin chargé
    /// du même dossier, une seconde fenêtre —, et l'un d'eux se rouvre d'un clic.
    /// </summary>
    [AvaloniaFact]
    public void Les_documents_recents_survivent_a_un_redemarrage()
    {
        var preferences = Path.Combine(_dossier, "preferences");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre()), Preferences.Charger(preferences));
        Assert.Empty(Recents(Assert.IsType<MainWindow>(fenetre)));

        // Sept documents enregistrés, puis le deuxième rouvert : il remonte en tête sans se
        // doubler, et le premier, le plus ancien, est sorti de la liste.
        for (var i = 1; i <= 7; i++) vm.SaveTo(Chemin($"doc_{i}.mypub"));
        vm.LoadFrom(Chemin("doc_2.mypub"));
        fenetre.Close();

        var (seconde, vm2) = Rendu.Ouvrir(Rendu.Feuille(), Preferences.Charger(preferences));
        var principale = Assert.IsType<MainWindow>(seconde);
        Assert.Equal(new[] { 2, 7, 6, 5, 4, 3 }.Select(i => Chemin($"doc_{i}.mypub")), vm2.DocumentsRecents);
        // Numérotés de 1 à 6 ; le « _ » du nom est doublé, pour ne pas devenir une touche d'accès.
        Assert.Equal(new[] { "_1 doc__2.mypub", "_2 doc__7.mypub", "_3 doc__6.mypub", "_4 doc__5.mypub",
                             "_5 doc__4.mypub", "_6 doc__3.mypub" }, Recents(principale));
        Assert.True(principale.GetLogicalDescendants().OfType<Separator>().Single(s => s.Name == "SeparateurDesRecents").IsVisible);

        Rendu.Cliquer(EntreeDuMenuFichier(principale, m => m.Name == "MenuRecent3", "« 3 »"));
        Assert.Equal(Chemin("doc_6.mypub"), vm2.FilePath);
        Assert.Equal("_1 doc__6.mypub", Recents(principale)[0]);
        Assert.Equal(6, Recents(principale).Length);
        Assert.Equal(vm2.DocumentsRecents, Preferences.Charger(preferences).DocumentsRecents);
    }

    /// <summary>
    /// Un document récent disparu du disque quitte la liste, sans question ni rien d'autre : on
    /// n'abandonne pas le document en cours pour un fichier qui n'est plus là.
    /// </summary>
    [AvaloniaFact]
    public void Un_document_recent_disparu_quitte_la_liste_sans_rien_demander()
    {
        var preferences = Path.Combine(_dossier, "preferences");
        var (fenetre, vm) = Ouvrir(Preferences.Charger(preferences));
        Modifier(fenetre, vm);
        File.Delete(Chemin("autre.mypub"));

        Jouer(Geste.Ouvrir, fenetre);
        Assert.Null(LaQuestion(fenetre));
        Assert.Equal(Chemin("principal.mypub"), vm.FilePath);
        Assert.True(vm.IsDirty);
        Assert.StartsWith("Introuvable", vm.Status);
        Assert.Equal(new[] { Chemin("principal.mypub") }, vm.DocumentsRecents);
        Assert.Equal(new[] { Chemin("principal.mypub") }, Preferences.Charger(preferences).DocumentsRecents);
        Assert.Equal(new[] { "_1 principal.mypub" }, Recents(fenetre));
    }

    /// <summary>
    /// Une fenêtre de test ne demande rien : la question n'est branchée que par l'application. La
    /// fenêtre modifiée se ferme, comme avant le poste — c'est ce dont <c>MasquerEcranTests</c> et
    /// les autres ont besoin.
    /// </summary>
    [AvaloniaFact]
    public void Sans_la_question_branchee_la_fenetre_ne_demande_rien()
    {
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(Carre()));
        var principale = Assert.IsType<MainWindow>(fenetre);
        Assert.Null(principale.AvantDePerdre);
        var fermee = false;
        fenetre.Closed += (_, _) => fermee = true;
        Modifier(principale, vm);

        fenetre.Close();
        Assert.Null(LaQuestion(principale));
        Assert.True(fermee);
    }
}

/// <summary>
/// Le filet des préférences (poste N11) : avant le premier test, la variable
/// <see cref="Preferences.VariableDuDossier"/> désigne un dossier temporaire, si personne ne l'a
/// posée. Un modèle de vue de test prend déjà un magasin en mémoire, qui n'écrit nulle part ; ceci
/// garde le cas d'un <see cref="Preferences.Charger"/> sans dossier : il n'écrirait pas non plus
/// dans la configuration de la personne.
/// </summary>
internal static class PreferencesDesTests
{
    [ModuleInitializer]
    internal static void HorsDeLaConfiguration()
    {
        if (Environment.GetEnvironmentVariable(Preferences.VariableDuDossier) is { Length: > 0 }) return;
        Environment.SetEnvironmentVariable(Preferences.VariableDuDossier,
            Path.Combine(Path.GetTempPath(), $"mypublisher-tests-preferences-{Environment.ProcessId}"));
    }
}
