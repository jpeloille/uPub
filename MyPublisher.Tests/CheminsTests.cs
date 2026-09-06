using System;
using System.IO;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Poste A3 — les chemins d'images relatifs.
///
/// La règle tient en une phrase : à l'écriture, un chemin d'image devient relatif
/// quand le fichier se trouve <b>sous</b> le dossier du document, et reste absolu
/// sinon ; à la lecture, les deux formes se résolvent. Le modèle, lui, ne porte que
/// des chemins absolus — la résolution a lieu à l'ouverture, une fois pour toutes.
///
/// Ces tests épinglent le calcul lui-même. La recette éprouve le geste — déplacer un
/// dossier — mais elle ne peut pas atteindre les cas de bord : le dossier voisin au nom
/// voisin, le chemin qui remonte par « .. », l'aller-retour sans dossier de l'historique
/// d'annulation. Ce sont ceux-là qui se cassent en silence.
/// </summary>
public class CheminsTests : IDisposable
{
    // Un dossier propre par test : deux tests qui partageraient le même chemin
    // partageraient aussi le cache d'images du processus.
    private readonly string _travail =
        Directory.CreateTempSubdirectory("mypublisher-chemins-").FullName;

    public void Dispose() => Directory.Delete(_travail, recursive: true);

    private static Document Document_avec_image(string chemin)
    {
        var document = Models.Document.CreateDefault();
        document.Pages[0].Frames.Add(new ImageFrame
        {
            Name = "Photo", X = 10, Y = 10, Width = 100, Height = 80, Path = chemin,
        });
        return document;
    }

    private static ImageFrame Image(Document document, int page = 0, int rang = 0) =>
        (ImageFrame)document.Pages[page].Frames[rang];

    // ------------------------------------------------------------------- l'écriture

    [Fact]
    public void Une_image_sous_le_dossier_s_ecrit_en_relatif()
    {
        var json = DocumentSerializer.ToJson(
            Document_avec_image(Path.Combine(_travail, "images", "photo.png")), _travail);

        Assert.Contains("\"Path\": \"images/photo.png\"", json);
        Assert.DoesNotContain(_travail, json);
    }

    [Fact]
    public void Une_image_hors_du_dossier_garde_son_chemin_absolu()
    {
        var ailleurs = Path.Combine(Path.GetTempPath(), "hors-du-document", "photo.png");

        var json = DocumentSerializer.ToJson(Document_avec_image(ailleurs), _travail);

        Assert.Contains($"\"Path\": \"{ailleurs}\"", json);
    }

    [Fact]
    public void Un_dossier_voisin_au_nom_voisin_n_est_pas_dedans()
    {
        // Le piège du préfixe de chaîne : « /…/notice » et « /…/notice-images » se
        // ressemblent, et « StartsWith » les confondrait. L'image sortirait alors
        // écrite « ../notice-images/x.png », qui se casse au premier déplacement —
        // exactement le défaut que ce poste répare.
        var voisin = _travail + "-images";
        var image = Path.Combine(voisin, "x.png");

        var json = DocumentSerializer.ToJson(Document_avec_image(image), _travail);

        Assert.Contains($"\"Path\": \"{image}\"", json);
        Assert.DoesNotContain("..", json);
    }

    [Fact]
    public void Un_bloc_image_sans_chemin_reste_sans_chemin()
    {
        // Les quatre modèles livrés réservent leur place ainsi. « GetFullPath("") » lève.
        var json = DocumentSerializer.ToJson(Document_avec_image(""), _travail);

        Assert.Contains("\"Path\": \"\"", json);
    }

    [Fact]
    public void Les_images_des_groupes_et_des_autres_pages_sont_traitees()
    {
        var document = Models.Document.CreateDefault();
        document.Pages.Add(new Page { Name = "Page 2" });
        document.Pages[1].Frames.Add(new GroupFrame
        {
            Name = "Groupe",
            Children =
            {
                new ImageFrame { Path = Path.Combine(_travail, "dans-le-groupe.png") },
            },
        });

        var json = DocumentSerializer.ToJson(document, _travail);

        Assert.Contains("\"Path\": \"dans-le-groupe.png\"", json);
    }

    // -------------------------------------------------------------------- la lecture

    [Fact]
    public void Un_chemin_relatif_se_resout_sur_le_dossier_du_document()
    {
        var json = DocumentSerializer.ToJson(
            Document_avec_image(Path.Combine(_travail, "images", "photo.png")), _travail);

        var relu = DocumentSerializer.FromJson(json, _travail);

        Assert.Equal(Path.Combine(_travail, "images", "photo.png"), Image(relu).Path);
    }

    [Fact]
    public void Le_dossier_deplace_retrouve_ses_images()
    {
        // Le geste que le poste existe pour permettre, joué sur de vrais fichiers.
        var depart = Path.Combine(_travail, "notice");
        var arrivee = Path.Combine(_travail, "archives", "notice");
        Directory.CreateDirectory(depart);
        File.WriteAllText(Path.Combine(depart, "photo.png"), "ce n'est pas un PNG, c'est un témoin");

        var document = Path.Combine(depart, "notice.mypub");
        DocumentSerializer.Save(Document_avec_image(Path.Combine(depart, "photo.png")), document);

        Directory.CreateDirectory(Path.Combine(_travail, "archives"));
        Directory.Move(depart, arrivee);

        var relu = DocumentSerializer.Load(Path.Combine(arrivee, "notice.mypub"));

        Assert.Equal(Path.Combine(arrivee, "photo.png"), Image(relu).Path);
        Assert.True(File.Exists(Image(relu).Path));
    }

    [Fact]
    public void Un_document_ancien_a_chemin_absolu_s_ouvre_comme_avant()
    {
        // Tout ce qui a été enregistré jusqu'ici porte des chemins absolus. Ils ne
        // doivent ni bouger ni se résoudre sur le dossier du document.
        var ailleurs = Path.Combine(Path.GetTempPath(), "ancien", "photo.png");
        var json = DocumentSerializer.ToJson(Document_avec_image(ailleurs));

        var relu = DocumentSerializer.FromJson(json, _travail);

        Assert.Equal(ailleurs, Image(relu).Path);
    }

    // ----------------------------------------------------------------- l'aller-retour

    [Fact]
    public void Le_document_relu_dans_son_dossier_se_reecrit_a_l_identique()
    {
        // L'exigence du contrôle de non-régression, tenue sur un document qui porte
        // les deux formes de chemin à la fois.
        var document = Document_avec_image(Path.Combine(_travail, "images", "dedans.png"));
        document.Pages[0].Frames.Add(new ImageFrame
        {
            Path = Path.Combine(Path.GetTempPath(), "ailleurs", "dehors.png"),
        });

        var json = DocumentSerializer.ToJson(document, _travail);
        var relu = DocumentSerializer.FromJson(json, _travail);

        Assert.Equal(json, DocumentSerializer.ToJson(relu, _travail));
    }

    [Fact]
    public void Enregistrer_ailleurs_rend_le_chemin_absolu()
    {
        // « Enregistrer sous » dans un autre dossier : l'image ne suit pas, elle est
        // donc bien hors du nouveau dossier et son chemin doit redevenir absolu.
        var origine = Path.Combine(_travail, "origine");
        var destination = Path.Combine(_travail, "destination");
        Directory.CreateDirectory(origine);
        Directory.CreateDirectory(destination);

        var image = Path.Combine(origine, "photo.png");
        DocumentSerializer.Save(Document_avec_image(image), Path.Combine(origine, "d.mypub"));
        var relu = DocumentSerializer.Load(Path.Combine(origine, "d.mypub"));

        DocumentSerializer.Save(relu, Path.Combine(destination, "d.mypub"));

        Assert.Contains(image, File.ReadAllText(Path.Combine(destination, "d.mypub")));
        Assert.Equal(image, Image(DocumentSerializer.Load(Path.Combine(destination, "d.mypub"))).Path);
    }

    // ------------------------------------------------------- l'historique d'annulation

    [Theory]
    // Les trois formes qu'un chemin peut prendre en mémoire. La troisième ne s'obtient
    // pas par « Load », mais un document fabriqué à la main ou déjà en mémoire l'a.
    [InlineData("/ailleurs/photo.png")]
    [InlineData("/travail/notice/photo.png")]
    [InlineData("images/photo.png")]
    public void Sans_dossier_l_aller_retour_ne_touche_pas_au_chemin(string chemin)
    {
        // C'est l'invariant de l'historique d'annulation, et il vaut d'être nommé :
        // « MainWindowViewModel.Capture » sérialise le document à chaque geste et
        // « Restore » le relit, tous deux SANS dossier. Si cet aller-retour
        // transformait les chemins, annuler un déplacement de bloc déplacerait une
        // image — un geste sans rapport avec les images en changerait une.
        var json = DocumentSerializer.ToJson(Document_avec_image(chemin));
        var relu = DocumentSerializer.FromJson(json);

        Assert.Equal(chemin, Image(relu).Path);
        Assert.Equal(json, DocumentSerializer.ToJson(relu));
    }
}
