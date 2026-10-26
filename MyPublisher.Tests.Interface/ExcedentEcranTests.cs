using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
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
/// L'alerte du dock — « Texte en excédent : 4 lignes », section « Cadre de texte » —,
/// jouée sur la vraie fenêtre (poste I9). Le compte est éprouvé en tests purs
/// (<c>ExcedentTests</c>) ; ce qu'on éprouve ici, c'est qu'il est celui qu'on lit, sur
/// le bloc sélectionné, qu'il suit la frappe sans attendre un changement de sélection, et
/// qu'il a les couleurs et l'icône de la maquette.
///
/// <c>Scripts/recettes/I9-excedent.sh</c> joue ces tests par leur trait, puis confronte le
/// dock aux lignes absentes du PDF.
/// </summary>
[Trait("poste", "I9")]
public class ExcedentEcranTests
{
    // ------------------------------------------------------------------ outillage

    private static Document RecetteDebordement() =>
        DocumentSerializer.Load(System.IO.Path.Combine(Rendu.Racine(), "Samples", "recette-debordement.mypub"));

    /// <summary>L'alerte affichée, s'il y en a une — et jamais plus d'une.</summary>
    private static Border? Alerte(Window fenetre)
    {
        var alertes = fenetre.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Name == "AlerteDExcedent" && b.IsEffectivelyVisible)
            .ToList();
        Assert.True(alertes.Count <= 1, $"{alertes.Count} alertes d'excédent affichées");
        return alertes.SingleOrDefault();
    }

    /// <summary>Le texte de l'alerte affichée, ou null quand aucune ne l'est.</summary>
    private static string? TexteDeLAlerte(Window fenetre) =>
        Alerte(fenetre) is { } alerte
            ? alerte.GetVisualDescendants().OfType<TextBlock>().Single().Text
            : null;

    /// <summary>
    /// Le compte d'un bloc composé à la règle de l'EXPORT — celle de PDFsharp, coupant sur
    /// la plus large de sa mesure et de celle de l'écran, comme l'export la monte — par la
    /// fonction même que le dock appelle.
    /// </summary>
    private static int CompteALExport(TextFrame bloc, Charte charte)
    {
        var largeur = Math.Max(1, bloc.Width - bloc.PaddingLeft - bloc.PaddingRight);
        var hauteur = Math.Max(1, bloc.Height - bloc.PaddingTop - bloc.PaddingBottom);
        var lignes = Compositeur.Composer(
            bloc.Text, charte.Fragments(bloc.Spans), charte.StyleCommun(bloc), bloc.Paragraphs, bloc.Paragraph,
            largeur, hauteur, bloc.Colonnage, bloc.LineHeight, MetriquesPdf.HorsPage(new MetriquesAvalonia()));
        return Compositeur.LignesEnExcedent(lignes, hauteur, bloc.PaddingBottom, bloc.VerticalAlign);
    }

    private static TextFrame Bloc(string texte, double hauteur = 60) => new()
    {
        Name = "Consigne", X = 60, Y = 60, Width = 160, Height = hauteur,
        FontFamily = "DejaVu Sans", FontSize = 11, LineHeight = 1,
        PaddingLeft = 0, PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0,
        Text = texte,
    };

    private static string Lignes(int n) => string.Join("\n", Enumerable.Range(1, n).Select(i => $"Ligne {i}"));

    // ------------------------------------------------------------------ les tests

    [AvaloniaFact]
    public void Le_dock_dit_le_compte_de_chaque_bloc_et_l_export_compte_pareil()
    {
        var document = RecetteDebordement();
        var (fenetre, vm) = Rendu.Ouvrir(document);

        // Le document de A2 : quatre blocs de huit lignes qui en montrent quatre — dont un
        // calé au milieu, un tourné de 20° —, un qui ne dépasse sa marge basse que d'un
        // point et quart et perd sa dernière ligne, et six qui tiennent, dont celui que sa
        // marge basse sauve.
        var attendus = new (string Nom, string? Alerte)[]
        {
            ("Trop plein", "Texte en excédent : 4 lignes"),
            ("Temoin qui tient", null),
            ("Calage haut", null),
            ("Calage milieu", null),
            ("Calage bas", null),
            ("Milieu qui deborde", "Texte en excédent : 4 lignes"),
            ("Haut qui deborde", "Texte en excédent : 4 lignes"),
            ("Retrait pendant", null),
            ("Trop plein pivote", "Texte en excédent : 4 lignes"),
            ("Marge basse absorbe", null),
            ("Marge basse depassee", "Texte en excédent : 1 ligne"),
        };

        var blocs = document.Pages[0].Frames.OfType<TextFrame>().ToList();
        Assert.Equal(attendus.Select(a => a.Nom), blocs.Select(b => b.Name));

        foreach (var (bloc, (nom, alerte)) in blocs.Zip(attendus))
        {
            vm.SelectOnly(bloc);
            Rendu.Battre();

            Assert.Equal(alerte, TexteDeLAlerte(fenetre));
            // L'écran et l'export, chacun à sa règle, perdent les mêmes lignes.
            Assert.Equal(vm.LignesEnExcedent, CompteALExport(bloc, document.Charte));
            Assert.Equal(alerte is null ? 0 : int.Parse(alerte.Split(' ')[4]), vm.LignesEnExcedent);
        }

        // Une mesure, pas une retouche : ni pas d'annulation, ni document modifié.
        Assert.False(vm.CanUndo);
        Assert.False(vm.IsDirty);
    }

    [AvaloniaFact]
    public void Sans_bloc_de_texte_seul_l_alerte_se_tait()
    {
        var trop = Bloc(Lignes(8));
        var autre = Bloc(Lignes(8));
        autre.X = 300;
        var image = new ImageFrame { X = 60, Y = 300, Width = 100, Height = 100 };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(trop, autre, image));

        vm.SelectOnly(trop);
        Rendu.Battre();
        Assert.Equal("Texte en excédent : 4 lignes", TexteDeLAlerte(fenetre));

        // Deux blocs : le dock n'en montre aucun, l'alerte non plus.
        vm.SetSelection([trop, autre]);
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));
        Assert.Equal(0, vm.LignesEnExcedent);

        vm.SelectOnly(image);
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));

        vm.SelectOnly(null);
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));
        Assert.False(vm.TexteEnExcedent);
    }

    [AvaloniaFact]
    public void L_alerte_suit_la_frappe_sans_changement_de_selection()
    {
        // Quatre lignes de 12,8 pt dans 60 : tout tient.
        var bloc = Bloc(Lignes(4));
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        vm.SelectOnly(bloc);
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));

        // L'utilisateur ouvre la saisie sur la page, comme il le fait (Rendu.EntrerEnSaisie).
        Rendu.EntrerEnSaisie(fenetre, vm, bloc);

        // En fin de texte, une cinquième ligne : elle sort du bloc, l'alerte paraît.
        fenetre.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.Control);
        fenetre.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        fenetre.KeyTextInput("Ligne 5");
        Rendu.Battre();

        Assert.Same(bloc, vm.SingleSelected);
        Assert.True(bloc.IsEditing, "la frappe a refermé la saisie");
        Assert.Equal("Texte en excédent : 1 ligne", TexteDeLAlerte(fenetre));

        fenetre.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        fenetre.KeyTextInput("Ligne 6");
        Rendu.Battre();
        Assert.Equal("Texte en excédent : 2 lignes", TexteDeLAlerte(fenetre));

        // Effacer la dernière ligne la rend au bloc.
        for (var i = 0; i < "\nLigne 6".Length; i++)
        {
            fenetre.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        }

        Rendu.Battre();
        Assert.Equal("Texte en excédent : 1 ligne", TexteDeLAlerte(fenetre));

        fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Rendu.Battre();

        // L'annulation relit le document : le bloc rétabli tient de nouveau, et l'alerte
        // suit le bloc que la sélection désigne désormais.
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal(Lignes(4), ((TextFrame)vm.SingleSelected!).Text);
        Assert.Null(TexteDeLAlerte(fenetre));
    }

    [AvaloniaFact]
    public void L_alerte_suit_les_reglages_du_bloc()
    {
        // Huit lignes, quatre montrées.
        var bloc = Bloc(Lignes(8));
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        Assert.Equal("Texte en excédent : 4 lignes", TexteDeLAlerte(fenetre));

        // Deux colonnes : la première en prend quatre, la seconde quatre (R3).
        bloc.Columns = 2;
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));

        // Une marge basse de 12 pt ôte à la zone de texte la place d'une ligne : la première
        // colonne n'en prend plus que trois, la seconde en reçoit cinq. La quatrième se
        // montre encore, dans la marge — c'est le bloc qui montre, pas la zone (C6) —, la
        // cinquième non.
        bloc.PaddingBottom = 12;
        Rendu.Battre();
        Assert.Equal("Texte en excédent : 1 ligne", TexteDeLAlerte(fenetre));

        // Calé en bas, le texte trop haut reste calé en haut (A7) : rien ne change.
        bloc.VerticalAlign = VerticalAlign.Bottom;
        Rendu.Battre();
        Assert.Equal("Texte en excédent : 1 ligne", TexteDeLAlerte(fenetre));

        // Tourné, le bloc se compte dans son repère.
        bloc.Rotation = 30;
        Rendu.Battre();
        Assert.Equal("Texte en excédent : 1 ligne", TexteDeLAlerte(fenetre));

        // La même zone de texte, haute de 48, avec deux points de marge seulement : la
        // quatrième ligne de la seconde colonne passe sous le bord du bloc.
        bloc.PaddingBottom = 2;
        bloc.Height = 50;
        Rendu.Battre();
        Assert.Equal("Texte en excédent : 2 lignes", TexteDeLAlerte(fenetre));

        // Plus haut, tout tient.
        bloc.Height = 200;
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));
    }

    // Les trois tests qui suivent ont été écrits à la relecture du 26 octobre 2026 : le
    // compte du dock compose le bloc de son côté, et rien ne vérifiait qu'il le compose
    // avec ce que la planche lui donne — ses fragments, ses écarts de paragraphe, le jeu de
    // polices du document, le contenu résolu d'un champ —, ni qu'il le recompose quand l'un
    // d'eux change sans que la sélection change. Neuf sabotages du juge passaient.

    [AvaloniaFact]
    public void L_alerte_suit_les_fragments_et_les_ecarts_du_bloc()
    {
        // Quatre lignes de 12,8 pt dans 60 : tout tient.
        var bloc = Bloc(Lignes(4));
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));

        // La première ligne en corps 40 : haute de 46,6 pt, elle pousse la troisième et la
        // quatrième hors du bloc. C'est un fragment de style, pas un réglage du bloc.
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = "Ligne 1".Length, FontSize = 40 });
        Rendu.Battre();
        Assert.Equal("Texte en excédent : 2 lignes", TexteDeLAlerte(fenetre));
        Assert.Equal(CompteALExport(bloc, Charte.Defaut), vm.LignesEnExcedent);

        bloc.Spans.Clear();
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));

        // Trente points après le premier paragraphe : la troisième ligne commence à 55,6 et
        // finit hors du bloc, la quatrième aussi.
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 0, SpaceAfter = 30 });
        Rendu.Battre();
        Assert.Equal("Texte en excédent : 2 lignes", TexteDeLAlerte(fenetre));
        Assert.Equal(CompteALExport(bloc, Charte.Defaut), vm.LignesEnExcedent);

        bloc.Paragraphs.Clear();
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));
    }

    [AvaloniaFact]
    public void L_alerte_suit_le_jeu_de_polices_et_son_annulation()
    {
        // Un texte dans la police de corps du jeu. « Sobre », le jeu par défaut, le compose en
        // DejaVu Sans ; « Édition », en Liberation Sans, plus étroite : le même texte y prend
        // moins de lignes, et en perd moins.
        var bloc = Bloc(TexteCourant);
        bloc.FontFamily = ReferenceDuJeu.Police(EmplacementDePolice.Body);
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.SelectOnly(bloc);
        Rendu.Battre();

        var sobre = vm.LignesEnExcedent;
        Assert.True(sobre > 0, "le texte tient déjà dans « Sobre » : le test ne prouve rien");
        Assert.Equal(CompteALExport(bloc, vm.Document.Charte), sobre);

        vm.ChosenFontScheme = JeuxPredefinis.PolicesNommees("Édition");
        Rendu.Battre();
        var edition = vm.LignesEnExcedent;
        Assert.Same(bloc, vm.SingleSelected);
        Assert.NotEqual(sobre, edition);
        Assert.Equal(CompteALExport(bloc, vm.Document.Charte), edition);
        Assert.Equal(edition == 0 ? null : edition == 1 ? "Texte en excédent : 1 ligne" : $"Texte en excédent : {edition} lignes",
            TexteDeLAlerte(fenetre));

        // L'annulation relit le document, et son jeu d'avant : le compte revient.
        vm.UndoCommand.Execute(null);
        Rendu.Battre();
        Assert.Equal("Sobre", vm.Document.FontScheme.Name);
        Assert.Equal(sobre, vm.LignesEnExcedent);
    }

    [AvaloniaFact]
    public void Un_champ_se_compte_par_ce_qu_il_affiche_et_suit_les_coordonnees()
    {
        // Le texte saisi de ce bloc déborde ; mais il porte le numéro de page, et la planche
        // n'en montre que « 1 ».
        var numero = Bloc(Lignes(8));
        numero.Field = FieldKind.PageNumber;
        var adresse = Bloc("");
        adresse.X = 300;
        adresse.Field = FieldKind.ContactAddress;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(numero, adresse));

        vm.SelectOnly(numero);
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));
        Assert.Equal(0, vm.LignesEnExcedent);

        // L'adresse vient du jeu de coordonnées : vide, rien ne déborde ; une adresse de huit
        // lignes, adoptée sans changer la sélection, en perd quatre.
        vm.SelectOnly(adresse);
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));

        vm.ApplyContacts(new BusinessInfoStore { Sets = [new BusinessInfo { Address = Lignes(8) }] });
        Rendu.Battre();
        Assert.Same(adresse, vm.SingleSelected);
        Assert.Equal("Texte en excédent : 4 lignes", TexteDeLAlerte(fenetre));
    }

    /// <summary>Un paragraphe courant, assez long pour que la largeur de la police décide des lignes.</summary>
    private const string TexteCourant =
        "Le compte du dock suit le jeu de polices du document : on le change sans toucher au bloc, " +
        "et les mots se répartissent autrement sur les lignes, que la police soit large ou étroite.";

    [AvaloniaFact]
    public void Le_bloc_quitte_ne_retient_pas_le_modele_de_vue()
    {
        var premier = Bloc(Lignes(8));
        var second = Bloc(Lignes(2));
        second.X = 300;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(premier, second));

        bool EcouteParLeModele(TextFrame bloc) =>
            Rendu.Abonnes(bloc).Any(d => ReferenceEquals(d.Target, vm))
            || Rendu.Abonnes(bloc.Spans, "CollectionChanged").Any(d => ReferenceEquals(d.Target, vm))
            || Rendu.Abonnes(bloc.Paragraphs, "CollectionChanged").Any(d => ReferenceEquals(d.Target, vm));

        vm.SelectOnly(premier);
        Rendu.Battre();
        Assert.True(EcouteParLeModele(premier));

        vm.SelectOnly(second);
        Rendu.Battre();
        Assert.False(EcouteParLeModele(premier), "le bloc quitté est encore écouté par le compte");
        Assert.Null(TexteDeLAlerte(fenetre));

        // Retouché hors de la sélection, il ne fait plus rien dire au dock.
        premier.Text = Lignes(20);
        Rendu.Battre();
        Assert.Null(TexteDeLAlerte(fenetre));
    }

    [AvaloniaFact]
    public void L_alerte_a_les_couleurs_et_l_icone_de_la_maquette_sans_bouger_la_planche()
    {
        var trop = Bloc(Lignes(8));
        var tient = Bloc(Lignes(2));
        tient.X = 300;
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(trop, tient));

        vm.SelectOnly(tient);
        Rendu.Battre();
        var planche = fenetre.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PageRoot");
        var avant = planche.TranslatePoint(new Point(0, 0), fenetre);

        vm.SelectOnly(trop);
        Rendu.Battre();
        var alerte = Alerte(fenetre);
        Assert.NotNull(alerte);

        // La planche ne bouge pas quand la sélection change (règle 3) : l'alerte est au dock.
        Assert.Equal(avant, planche.TranslatePoint(new Point(0, 0), fenetre));

        // Fond #FDF1E8 à rayon 6, texte et icône #8A2E07, corps 11,5 : la maquette.
        Assert.Equal(Color.Parse("#FDF1E8"), Assert.IsAssignableFrom<ISolidColorBrush>(alerte.Background).Color);
        Assert.Equal(new CornerRadius(6), alerte.CornerRadius);
        var texte = alerte.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.Equal(Color.Parse("#8A2E07"), Assert.IsAssignableFrom<ISolidColorBrush>(texte.Foreground).Color);
        Assert.Equal(11.5, texte.FontSize);

        // L'icône d'alerte de l'apparence : le cercle et le point d'exclamation, au trait
        // de la couleur du texte.
        var traits = alerte.GetVisualDescendants().OfType<Path>().ToList();
        Assert.Equal(2, traits.Count);
        Assert.All(traits, t =>
            Assert.Equal(Color.Parse("#8A2E07"), Assert.IsAssignableFrom<ISolidColorBrush>(t.Stroke).Color));

        // Et c'est bien ce qui se dessine : le fond de l'alerte couvre sa bande — l'image
        // composée est en RGBA.
        var bande = Rendu.Bande(fenetre, alerte);
        var fond = 0;
        for (var i = 0; i < bande.Length; i += 4)
        {
            if (bande[i] == 0xFD && bande[i + 1] == 0xF1 && bande[i + 2] == 0xE8) fond++;
        }

        Assert.True(fond > bande.Length / 4 / 2, $"le fond de l'alerte ne couvre que {fond} pixels de sa bande");
    }
}
