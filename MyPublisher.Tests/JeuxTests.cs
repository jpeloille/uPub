using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Le jeu de couleurs et le jeu de polices — poste C5b.
///
/// Ce qui s'éprouve ici, c'est la référence : un bloc qui prend une couleur du jeu la
/// suit quand on change de jeu, une couleur saisie en hexadécimal ne bouge pas, et un
/// document de la version 4 s'ouvre à l'identique. La résolution est un calcul du noyau
/// que l'écran et l'export appellent tous deux : une erreur ici serait commise des deux
/// côtés à la fois, et aucune comparaison des deux rendus ne la verrait.
/// </summary>
[Trait("poste", "C5b")]
public class JeuxTests
{
    private static readonly JeuDeCouleurs Ardoise = JeuxPredefinis.CouleursParDefaut;
    private static readonly JeuDeCouleurs Ocean = JeuxPredefinis.CouleursNommees("Océan")!;

    private static Charte Avec(JeuDeCouleurs couleurs, JeuDePolices? polices = null) =>
        new(couleurs, polices ?? JeuxPredefinis.PolicesParDefaut);

    // ------------------------------------------------------------------ la référence

    [Fact]
    public void Une_reference_prend_la_couleur_du_jeu_et_une_couleur_libre_la_sienne()
    {
        var accent = ReferenceDuJeu.Couleur(EmplacementDeCouleur.Accent1);

        Assert.Equal("scheme:accent1", accent);
        Assert.Equal(Ardoise.Accent1, Avec(Ardoise).Couleur(accent));
        Assert.Equal(Ocean.Accent1, Avec(Ocean).Couleur(accent));

        // Une couleur libre égale à l'accent 1 d'Ardoise n'est pas l'accent 1 : elle
        // reste ce qu'elle est quand le jeu change. C'est tout le poste en une ligne.
        Assert.Equal(Ardoise.Accent1, Avec(Ocean).Couleur(Ardoise.Accent1));
    }

    [Fact]
    public void Les_cinq_emplacements_se_resolvent_chacun_a_sa_couleur()
    {
        var charte = Avec(Ocean);
        var attendues = Ocean.Couleurs;

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(attendues[i], charte.Couleur(ReferenceDuJeu.Couleur((EmplacementDeCouleur)i)));
        }
    }

    [Fact]
    public void Une_reference_garde_son_opacite_propre()
    {
        var mi = ReferenceDuJeu.Couleur(EmplacementDeCouleur.Accent2, 0x80);

        Assert.Equal("scheme:accent2/80", mi);
        Assert.Equal(HexColor.WithAlpha(Ocean.Accent2, 0x80), Avec(Ocean).Couleur(mi));
    }

    [Fact]
    public void Une_reference_illisible_ne_se_dessine_pas()
    {
        // Comme une couleur illisible : les deux rendus ne dessinent pas ce qu'ils ne
        // savent pas lire, plutôt que d'inventer une couleur.
        Assert.Null(Avec(Ardoise).Couleur("scheme:accent9"));
        Assert.Null(Avec(Ardoise).Couleur("scheme:accent1/zz"));
    }

    [Fact]
    public void Une_police_du_jeu_suit_le_jeu_et_une_police_libre_reste()
    {
        var edition = JeuxPredefinis.PolicesNommees("Édition")!;
        var charte = Avec(Ardoise, edition);

        Assert.Equal(edition.Heading, charte.Police("scheme:heading"));
        Assert.Equal(edition.Body, charte.Police("scheme:body"));
        Assert.Equal("DejaVu Sans", charte.Police("DejaVu Sans"));
    }

    [Fact]
    public void Regler_lopacite_dun_fond_du_jeu_ne_le_detache_pas_du_jeu()
    {
        var bloc = new ShapeFrame { Fill = new SolidFill("scheme:accent1") };

        bloc.FillOpacity = 50;

        Assert.Equal("scheme:accent1/80", ((SolidFill)bloc.Fill!).Color);
        Assert.Equal(50, bloc.FillOpacity);

        // Ramenée à 100, la référence retrouve sa forme courte, celle d'une pastille.
        bloc.FillOpacity = 100;
        Assert.Equal("scheme:accent1", ((SolidFill)bloc.Fill!).Color);
    }

    // ---------------------------------------------------------- les remplissages C5c

    [Fact]
    public void Une_teinte_du_jeu_melange_la_couleur_du_jeu()
    {
        // La base est résolue AVANT le mélange : changer de jeu change la teinte.
        var teinte = new TintFill("scheme:accent1", 40);

        Assert.Equal(new TintFill(Ardoise.Accent1, 40).Uniforme, teinte.UniformeSelon(Avec(Ardoise)));
        Assert.Equal(new TintFill(Ocean.Accent1, 40).Uniforme, teinte.UniformeSelon(Avec(Ocean)));
    }

    [Fact]
    public void Un_aplat_du_jeu_rend_la_couleur_du_jeu()
    {
        Assert.Equal(Ocean.Accent3, new SolidFill("scheme:accent3").UniformeSelon(Avec(Ocean)));
        // Sans charte, le modèle se lit tel qu'il est écrit.
        Assert.Equal("scheme:accent3", new SolidFill("scheme:accent3").Uniforme);
    }

    // ---------------------------------------------------------------- le texte

    [Fact]
    public void Le_style_commun_dun_bloc_est_resolu()
    {
        var polices = JeuxPredefinis.PolicesNommees("Archives")!;
        var bloc = new TextFrame { FontFamily = "scheme:heading", Foreground = "scheme:main" };

        var style = Avec(Ocean, polices).StyleCommun(bloc);

        Assert.Equal(polices.Heading, style.FontFamily);
        Assert.Equal(Ocean.Main, style.Foreground);
    }

    [Fact]
    public void Un_fragment_qui_prend_une_couleur_du_jeu_la_suit()
    {
        var fragments = new[]
        {
            new StyleSpan { Start = 0, Length = 3, Foreground = "scheme:accent3" },
            new StyleSpan { Start = 4, Length = 3, Foreground = "#FF00AA00", FontFamily = "scheme:body" },
            new StyleSpan { Start = 8, Length = 2, Bold = true },
        };

        var resolus = Avec(Ocean).Fragments(fragments);

        Assert.Equal(Ocean.Accent3, resolus[0].Foreground);
        Assert.Equal("#FF00AA00", resolus[1].Foreground);
        Assert.Equal(JeuxPredefinis.PolicesParDefaut.Body, resolus[1].FontFamily);
        // Ce qu'un fragment laisse au bloc reste laissé au bloc.
        Assert.Null(resolus[2].Foreground);
        Assert.Null(resolus[2].FontFamily);
        // Et le modèle n'est pas touché : on résout une copie.
        Assert.Equal("scheme:accent3", fragments[0].Foreground);
    }

    [Fact]
    public void Des_fragments_sans_reference_ne_sont_pas_recopies()
    {
        // Le cas de tous les documents importés : 402 fragments dans les fiches ATR, qu'il
        // n'y a aucune raison de recopier à chaque dessin.
        var fragments = new[] { new StyleSpan { Start = 0, Length = 3, Foreground = "#FF102030" } };

        Assert.Same(fragments, Avec(Ocean).Fragments(fragments));
    }

    [Fact]
    public void La_composition_mesure_le_texte_dans_la_police_du_jeu()
    {
        // La police d'un bloc décide de ses coupures : la composition doit recevoir la
        // police du jeu et non « scheme:body », que personne ne sait mesurer.
        var bloc = new TextFrame { Text = "un deux", FontFamily = "scheme:body" };
        var charte = Avec(Ardoise, JeuxPredefinis.PolicesNommees("Édition")!);
        var mesure = new PoliceMesuree();

        Compositeur.Composer(bloc.Text, charte.Fragments(bloc.Spans), charte.StyleCommun(bloc),
            [], default, 500, 500, new Colonnage(1, 0), 1.25, mesure);

        Assert.Equal(["Liberation Sans"], mesure.Polices.Distinct());
    }

    private sealed class PoliceMesuree : Compositeur.IMetriques
    {
        public System.Collections.Generic.List<string> Polices { get; } = [];

        public double Largeur(string texte, StyleEffectif style)
        {
            Polices.Add(style.FontFamily);
            return texte.Length * 5;
        }

        public double Naturelle(StyleEffectif style) => 12;
    }

    // ---------------------------------------------------------------- le document

    [Fact]
    public void Un_document_neuf_porte_les_jeux_par_defaut()
    {
        var document = Document.CreateDefault();

        Assert.Equal(JeuxPredefinis.CouleursParDefaut, document.ColorScheme);
        Assert.Equal(JeuxPredefinis.PolicesParDefaut, document.FontScheme);
    }

    [Fact]
    public void Changer_de_jeu_change_la_charte_du_document()
    {
        var document = Document.CreateDefault();
        var avant = document.Charte;
        var notifiees = new System.Collections.Generic.List<string?>();
        document.PropertyChanged += (_, e) => notifiees.Add(e.PropertyName);

        document.ColorScheme = Ocean;

        Assert.NotEqual(avant, document.Charte);
        Assert.Equal(Ocean.Accent1, document.Charte.Couleur("scheme:accent1"));
        // C'est cette notification que l'écran suit : sans elle, la page garderait
        // l'ancien jeu jusqu'au prochain redessin.
        Assert.Contains(nameof(Document.Charte), notifiees);
    }

    [Fact]
    public void Un_jeu_nul_ecrit_dans_un_fichier_retombe_sur_le_jeu_par_defaut()
    {
        var document = DocumentSerializer.FromJson("""
            { "FormatVersion": 5, "ColorScheme": null, "FontScheme": null, "Pages": [] }
            """);

        Assert.Equal(JeuxPredefinis.CouleursParDefaut, document.ColorScheme);
        Assert.Equal(JeuxPredefinis.PolicesParDefaut, document.FontScheme);
    }

    [Fact]
    public void Les_jeux_et_les_references_font_laller_retour()
    {
        var document = Document.CreateDefault();
        document.ColorScheme = Ocean;
        document.FontScheme = JeuxPredefinis.PolicesNommees("Édition")!;
        document.Background = new GradientFill("scheme:accent2", "#FF000000", 45);
        document.Pages[0].Frames.Add(new TextFrame
        {
            FontFamily = "scheme:heading", Foreground = "scheme:main",
            Spans = [new StyleSpan { Start = 0, Length = 1, Foreground = "scheme:accent3/80" }],
        });

        var json = DocumentSerializer.ToJson(document);
        var relu = DocumentSerializer.FromJson(json);

        Assert.Equal(json, DocumentSerializer.ToJson(relu));
        Assert.Equal(Ocean, relu.ColorScheme);
        Assert.Equal(5, relu.FormatVersion);
        Assert.Equal("scheme:accent3/80", ((TextFrame)relu.Pages[0].Frames[0]).Spans[0].Foreground);
    }

    // -------------------------------------------------------------- la migration

    /// <summary>Un document de la version 4, couleurs et polices libres partout où il en porte.</summary>
    private const string Version4 = """
        {
          "FormatVersion": 4,
          "Background": { "$kind": "tint", "Color": "#FF2E5C8A", "Percent": 40 },
          "Pages": [ { "Frames": [
            { "$kind": "shape", "Fill": { "$kind": "solid", "Color": "#FF9DC3E6" }, "Stroke": "#FF2E5C8A" },
            { "$kind": "text", "FontFamily": "DejaVu Serif", "Foreground": "#FF1A1A1A",
              "Spans": [ { "Start": 0, "Length": 2, "FontFamily": "Liberation Sans", "Foreground": "#FFE8A33D" } ] },
            { "$kind": "group", "Children": [
              { "$kind": "shape", "Fill": { "$kind": "gradient", "From": "#FF2E5C8A", "To": "#FF6B7280", "Angle": 0 } } ] },
            { "$kind": "table", "FontFamily": "DejaVu Sans", "Foreground": "#FF1A1A1A",
              "Cells": [ { "Row": 0, "Column": 0, "Fill": { "$kind": "solid", "Color": "#FF6B7280" }, "Stroke": "#FF6B7280" } ] }
          ] } ]
        }
        """;

    [Fact]
    public void Un_document_de_la_version_4_garde_ses_couleurs_et_ses_polices_libres()
    {
        // Toutes ses couleurs sont celles d'Ardoise, le jeu par défaut — et c'est fait
        // exprès : la migration ne doit en rapprocher AUCUNE de l'accent qu'elle égale.
        // Sans quoi changer de jeu recolorerait un document qui n'a rien demandé.
        var avant = JsonNode.Parse(Version4)!.AsObject();
        var apres = JsonNode.Parse(Version4)!.AsObject();

        FormatMigrations.Apply(apres);

        apres["FormatVersion"] = 4;
        Assert.True(JsonNode.DeepEquals(avant, apres), apres.ToJsonString());
    }

    [Fact]
    public void Un_document_de_la_version_4_ne_change_pas_quand_on_change_de_jeu()
    {
        // La même chose, vue du rendu : chaque couleur et chaque police que les deux
        // rendus demanderont est la même sous deux jeux différents.
        var document = DocumentSerializer.FromJson(Version4);
        var sousArdoise = Demandees(document, Avec(Ardoise));
        var sousOcean = Demandees(document, Avec(Ocean, JeuxPredefinis.PolicesNommees("Édition")));

        Assert.Equal(sousArdoise, sousOcean);
        Assert.Contains("#FF2E5C8A", sousOcean);
    }

    /// <summary>Tout ce qu'un rendu demanderait au document, couleurs et polices, résolu par une charte.</summary>
    private static string[] Demandees(Document document, Charte charte)
    {
        var valeurs = new System.Collections.Generic.List<string?> { document.Background?.UniformeSelon(charte) };

        foreach (var bloc in document.Pages.SelectMany(p => p.Frames).SelectMany(Descendance))
        {
            valeurs.Add(bloc.Fill?.UniformeSelon(charte));
            if (bloc.Fill is GradientFill degrade)
            {
                valeurs.Add(charte.Couleur(degrade.From));
                valeurs.Add(charte.Couleur(degrade.To));
            }

            valeurs.Add(charte.Couleur(bloc.Stroke));
            if (bloc is not TextFrame texte) continue;

            var commun = charte.StyleCommun(texte);
            valeurs.Add(commun.FontFamily);
            valeurs.Add(commun.Foreground);
            foreach (var f in charte.Fragments(texte.Spans))
            {
                valeurs.Add(f.FontFamily);
                valeurs.Add(f.Foreground);
            }
        }

        return valeurs.Select(v => v ?? "∅").ToArray();
    }

    private static System.Collections.Generic.IEnumerable<Frame> Descendance(Frame bloc)
    {
        yield return bloc;
        var enfants = bloc switch
        {
            GroupFrame g => g.Children.AsEnumerable(),
            TableFrame t => t.CellFrames,
            _ => [],
        };
        foreach (var e in enfants.SelectMany(Descendance)) yield return e;
    }

    [Fact]
    public void Une_valeur_de_la_version_4_qui_ressemble_a_une_reference_reste_libre()
    {
        // La version 4 ne connaissait pas les références : une chaîne « scheme:… » y était
        // une couleur illisible ou une police introuvable. La version 5 la lirait comme
        // une référence et lui donnerait une couleur ; la migration la garde libre, où
        // qu'elle soit — fond, dégradé, contour, texte, fragment, case, fond de page.
        var racine = JsonNode.Parse("""
            {
              "FormatVersion": 4,
              "Background": { "$kind": "solid", "Color": "scheme:accent1" },
              "Pages": [ { "Frames": [
                { "$kind": "shape", "Fill": { "$kind": "gradient", "From": "scheme:main", "To": "#FF000000" },
                  "Stroke": "scheme:accent2" },
                { "$kind": "text", "FontFamily": "scheme:heading", "Foreground": "scheme:accent3", "Text": "scheme:accent4",
                  "Spans": [ { "Start": 0, "Length": 1, "FontFamily": "scheme:body", "Foreground": "scheme:accent4" } ] },
                { "$kind": "table", "Cells": [ { "Row": 0, "Column": 0, "Fill": { "$kind": "tint", "Color": "scheme:accent1" } } ] }
              ] } ]
            }
            """)!.AsObject();

        FormatMigrations.Apply(racine);
        var document = DocumentSerializer.FromJson(racine.ToJsonString());
        var charte = document.Charte;

        Assert.Equal("!scheme:accent1", ((SolidFill)document.Background!).Color);
        // Illisible avant la migration, illisible après : la page n'a pas plus de fond.
        Assert.False(HexColor.TryParse(
            document.Background!.UniformeSelon(charte), out _, out _, out _, out _));

        var forme = document.Pages[0].Frames[0];
        Assert.Equal("!scheme:main", ((GradientFill)forme.Fill!).From);
        Assert.Equal("!scheme:accent2", forme.Stroke);

        var texte = (TextFrame)document.Pages[0].Frames[1];
        Assert.Equal("!scheme:heading", texte.FontFamily);
        Assert.Equal("!scheme:accent3", texte.Foreground);
        Assert.Equal("!scheme:body", texte.Spans[0].FontFamily);
        Assert.Equal("!scheme:accent4", texte.Spans[0].Foreground);
        // Le texte n'est pas une couleur : il ne se touche pas.
        Assert.Equal("scheme:accent4", texte.Text);

        var tableau = (TableFrame)document.Pages[0].Frames[2];
        Assert.Equal("!scheme:accent1", ((TintFill)tableau.Cells[0].Fill!).Color);

        // Aucune ne se résout plus à une couleur du jeu.
        Assert.DoesNotContain(Ardoise.Couleurs, couleur => Demandees(document, charte).Contains(couleur));
    }

    [Fact]
    public void La_migration_de_la_version_4_est_un_point_fixe()
    {
        var racine = JsonNode.Parse(Version4.Replace("\"DejaVu Serif\"", "\"scheme:heading\""))!.AsObject();

        FormatMigrations.Apply(racine);
        var une = racine.ToJsonString();
        racine["FormatVersion"] = 4;
        FormatMigrations.Apply(racine);

        // Rejouée — ce que fait le contrôle P2 sur ses documents figés —, elle ne doit
        // pas empiler un second échappement sur le premier.
        Assert.Equal(une, racine.ToJsonString());
    }

    // ----------------------------------------------------------------- l'import

    [Fact]
    public void Limport_dun_pub_garde_ses_polices_libres()
    {
        // Un .pub ne connaît pas le jeu de MyPublisher. Une police dont le nom aurait la
        // forme d'une référence reste la police qu'il nomme.
        var document = PubImporter.Importer("""
            startDocument()
              startPage(svg:height: 11.6929in, svg:width: 8.2677in)
                startTextObject (svg:height: 1.0000in, svg:width: 2.0000in, svg:x: 0.5000in, svg:y: 1.5000in)
                  openParagraph (fo:text-align: left)
                    openSpan(fo:font-size: 0.1389in, style:font-name: scheme:heading, fo:color: #2e5c8a)
                      insertText (Bonjour)
                    closeSpan
                  closeParagraph
                endTextObject
            """).Document;

        var bloc = (TextFrame)document.Pages[0].Frames.Single();
        Assert.False(ReferenceDuJeu.EstReference(bloc.FontFamily), bloc.FontFamily);
        Assert.False(ReferenceDuJeu.EstReference(bloc.Foreground), bloc.Foreground);
        Assert.Equal("#FF2E5C8A", bloc.Foreground);
    }

    // ------------------------------------------------------------------ l'export

    [Fact]
    public void Lexport_controle_la_police_du_jeu_sous_son_vrai_nom()
    {
        // « scheme:body » n'est la police de personne : contrôlée telle quelle, elle
        // passerait pour introuvable sur toutes les machines. C'est la police que le jeu
        // désigne qui doit être contrôlée — et signalée si elle manque.
        var document = Document.CreateDefault();
        document.FontScheme = new JeuDePolices { Name = "Absente", Heading = "Police Inexistante C5b", Body = "DejaVu Sans" };
        document.Pages[0].Frames.Add(new TextFrame { Text = "Titre", FontFamily = "scheme:heading" });
        document.Pages[0].Frames.Add(new TextFrame { Text = "Corps", FontFamily = "scheme:body", Y = 200 });

        var chemin = Path.Combine(Path.GetTempPath(), $"c5b-{System.Guid.NewGuid():N}.pdf");
        try
        {
            var rapport = PdfExporter.Export(document, chemin);

            var polices = rapport.Warnings.Where(w => w.StartsWith("Police")).ToList();
            Assert.Contains(polices, w => w.Contains("Police Inexistante C5b"));
            Assert.DoesNotContain(polices, w => w.Contains("scheme:"));
        }
        finally
        {
            File.Delete(chemin);
        }
    }
}
