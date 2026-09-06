using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MyPublisher.Models;
using Xunit;
using Image = MyPublisher.Tests.Interface.Rendu.Image;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Les colonnes du bloc de texte, relevées sur ce que la fenêtre dessine.
///
/// Pourquoi ne pas s'en tenir à <c>CompositeurTests</c>, qui éprouve déjà le rangement
/// en colonnes : parce qu'il éprouve le calcul, et non son emploi. Un rendu qui
/// oublierait d'ajouter le pas de la colonne à l'abscisse d'origine, ou qui continuerait
/// à cumuler sa propre hauteur au lieu de lire celle de la ligne, passerait tous les
/// tests du noyau sans en déranger un seul — et empilerait les trois colonnes l'une sur
/// l'autre. On mesure donc l'encre elle-même, à deux pixels par point.
///
/// Les propriétés sont posées sur le bloc AVANT d'ouvrir la fenêtre. Ce n'est pas un
/// détail de commodité : le panneau TEXTE ne rafraîchit pas la page quand on change une
/// propriété typographique, défaut relevé en R2 et resté ouvert. Régler les colonnes
/// après coup mesurerait l'image d'avant.
/// </summary>
public class ColonnesTests
{
    private const double PixelsParPoint = Rendu.PixelsParPoint;

    /// <summary>
    /// Trois colonnes de 100 pt séparées de 10 : (320 − 2 × 10) / 3 = 100, et le pas
    /// d'un bord gauche au suivant vaut 110.
    ///
    /// Six paragraphes d'une ligne, 34 pt de hauteur, et une avance d'environ 14 pt à
    /// ce corps : deux lignes par colonne, avec six points de marge sous la seconde et
    /// huit au-dessus de ce qu'il faudrait pour une troisième. La rupture de colonne
    /// est un seuil et non une pente : un bloc dimensionné pour que la dernière ligne
    /// tienne « tout juste » basculerait d'une colonne à l'autre au moindre écart de
    /// métrique entre l'écran et le PDF, et le test serait rouge un jour sur deux sans
    /// qu'on sache pourquoi.
    /// </summary>
    private const double Pas = 110;
    private const double Colonne = 100;

    private static TextFrame Bloc() => new()
    {
        Name = "Flot", X = 40, Y = 40, Width = 320, Height = 34,
        FontFamily = "DejaVu Sans", FontSize = 12, LineHeight = 1,
        PaddingLeft = 0, PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0,
        Columns = 3, ColumnGap = 10,
        Text = string.Join("\n", Enumerable.Range(1, 6).Select(n => $"flot {n:00}")),
    };

    [AvaloniaFact]
    public void Le_texte_passe_dans_la_colonne_suivante_a_lecran()
    {
        var bloc = Bloc();
        var image = Rendu.Encre(Rendu.Ouvrir(Rendu.Feuille(bloc)).Fenetre);

        // Chacune des trois colonnes porte de l'encre. Une bande vide serait le symptôme
        // d'un écran qui compose tout dans la première : c'est ce qu'il faisait la veille
        // de ce poste.
        var boites = Enumerable.Range(0, 3)
            .Select(c => Boite(image, bloc.X + c * Pas, bloc.Y, Colonne, bloc.Height))
            .ToArray();

        for (var c = 0; c < 3; c++)
        {
            Assert.True(boites[c] is not null, $"la colonne {c + 1} est vide");
        }

        // Et elles sont bien décalées du pas, chacune au fer de sa colonne.
        for (var c = 0; c < 3; c++)
        {
            Assert.Equal(bloc.X + c * Pas, boites[c]!.Value.Gauche, tolerance: 1.5);
        }

        // Les trois repartent du haut du bloc : une colonne qui continuerait à cumuler
        // la hauteur de la précédente commencerait plus bas.
        Assert.Equal(boites[0]!.Value.Haut, boites[1]!.Value.Haut, tolerance: 1.0);
        Assert.Equal(boites[0]!.Value.Haut, boites[2]!.Value.Haut, tolerance: 1.0);
    }

    [AvaloniaFact]
    public void Une_seule_colonne_garde_tout_le_texte_au_fer_du_bloc()
    {
        // La non-régression, à l'écran : un bloc d'une seule colonne compose comme avant
        // ce poste, et sa gouttière — ici volontairement énorme — ne fait rien du tout.
        var bloc = Bloc();
        bloc.Columns = 1;
        bloc.ColumnGap = 60;

        var image = Rendu.Encre(Rendu.Ouvrir(Rendu.Feuille(bloc)).Fenetre);

        var tout = Boite(image, bloc.X, bloc.Y, bloc.Width, bloc.Height);
        var premiere = Boite(image, bloc.X, bloc.Y, Colonne, bloc.Height);

        Assert.NotNull(tout);
        Assert.NotNull(premiere);
        // Rien ne dépasse dans une seconde colonne qui n'existe pas : toute l'encre du
        // bloc tient dans la largeur de son premier « flot NN ».
        Assert.Equal(premiere!.Value.Droite, tout!.Value.Droite, tolerance: 1.0);
    }

    [AvaloniaFact]
    public void Le_pas_dune_colonne_a_lautre_se_lit_dans_lencre()
    {
        // La largeur d'encre du bloc entier vaut deux pas plus celle d'une colonne. La
        // mesure se cale sur elle-même : la chasse de « flot 01 » n'a pas à être connue,
        // et le test ne tombera pas le jour où la police du système change.
        var bloc = Bloc();
        var image = Rendu.Encre(Rendu.Ouvrir(Rendu.Feuille(bloc)).Fenetre);

        var tout = Boite(image, bloc.X, bloc.Y, bloc.Width, bloc.Height)!.Value;
        var premiere = Boite(image, bloc.X, bloc.Y, Colonne, bloc.Height)!.Value;

        Assert.Equal(2 * Pas + (premiere.Droite - premiere.Gauche),
            tout.Droite - tout.Gauche, tolerance: 1.5);
    }

    [AvaloniaFact]
    public void Le_panneau_refuse_un_nombre_de_colonnes_nul()
    {
        // Zéro colonne ne veut rien dire. La composition s'en défend — un bloc chargé
        // depuis un fichier absurde reste lisible — mais elle s'en défend en silence, et
        // un champ qui laisse saisir une valeur qu'on corrige derrière le dos de
        // l'utilisateur ment sur ce qu'il fait. C'est le pendant du minimum négatif que
        // R2 a dû poser sur le retrait de première ligne, dans l'autre sens.
        var bloc = Bloc();
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));

        vm.SelectOnly(bloc);
        Rendu.Battre();

        var champ = fenetre.GetVisualDescendants().OfType<NumericUpDown>()
            .First(c => c.Name == "NombreDeColonnes");

        Assert.True(champ.Minimum >= 1, "zéro colonne ne doit pas être saisissable");

        champ.Value = 2;
        Rendu.Battre();

        Assert.Equal(2, bloc.Columns);
    }

    // -------------------------------------------------------------- la mesure

    private readonly record struct Encre(double Gauche, double Haut, double Droite, double Bas);

    /// <summary>
    /// La boîte englobante de l'encre dans un rectangle de la page, en points, ou rien
    /// si le rectangle est vide. C'est la même grandeur que le <c>%@</c> d'ImageMagick
    /// que mesure la recette du poste, sur la même image et à la même échelle.
    /// </summary>
    private static Encre? Boite(Image image, double x, double y, double largeur, double hauteur)
    {
        var x0 = (int)Math.Floor(x * PixelsParPoint);
        var y0 = (int)Math.Floor(y * PixelsParPoint);
        var x1 = (int)Math.Ceiling((x + largeur) * PixelsParPoint);
        var y1 = (int)Math.Ceiling((y + hauteur) * PixelsParPoint);

        int gauche = int.MaxValue, haut = int.MaxValue, droite = int.MinValue, bas = int.MinValue;

        for (var py = y0; py < y1; py++)
        {
            for (var px = x0; px < x1; px++)
            {
                if (!Rendu.EstEncre(image, px, py)) continue;

                gauche = Math.Min(gauche, px);
                haut = Math.Min(haut, py);
                droite = Math.Max(droite, px);
                bas = Math.Max(bas, py);
            }
        }

        if (droite < gauche) return null;

        return new Encre(
            gauche / PixelsParPoint, haut / PixelsParPoint,
            (droite + 1) / PixelsParPoint, (bas + 1) / PixelsParPoint);
    }
}
