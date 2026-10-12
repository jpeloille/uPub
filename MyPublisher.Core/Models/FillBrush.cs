using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

/// <summary>
/// Le remplissage d'un bloc ou d'une page : uni, dégradé de deux couleurs, image, ou
/// teinte calculée depuis une couleur de base.
///
/// Discriminé par <c>$kind</c>, exactement comme les blocs le sont depuis C3c : le
/// mécanisme de sérialisation polymorphe était déjà en place et éprouvé, ce poste ne
/// fait que s'en servir une seconde fois.
///
/// **Pourquoi un type et non une chaîne.** Jusqu'au 6 septembre 2026, <c>Fill</c> était
/// une couleur <c>#AARRGGBB</c>. Les cinq images d'ATA 21 ne sont pas des blocs image :
/// ce sont des remplissages de forme (<c>draw:fill: bitmap</c>), et une chaîne de
/// couleur ne peut pas les porter. L'import les perdait, en le disant.
///
/// **Le nom.** La propriété s'appelle <c>Fill</c> depuis la décision P0 ; une classe du
/// même nom rendrait tout accès statique ambigu. Le pinceau est ce que les deux rendus
/// en font — <c>XBrush</c> d'un côté, <c>IBrush</c> de l'autre — d'où <c>FillBrush</c>.
///
/// **Deux calculs vivent ici et non dans les rendus** : les extrémités d'un dégradé et
/// la couleur d'une teinte. C'est la règle du dépôt, et la relecture l'a redite : une
/// comparaison écran ↔ PDF ne peut pas attraper une erreur partagée, mais elle ne peut
/// pas non plus attraper une erreur qui n'existe qu'une fois. Un rendu ne calcule rien,
/// il lit — et un test du noyau épingle le calcul lui-même.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(SolidFill), "solid")]
[JsonDerivedType(typeof(GradientFill), "gradient")]
[JsonDerivedType(typeof(ImageFill), "image")]
[JsonDerivedType(typeof(TintFill), "tint")]
public abstract partial class FillBrush : ObservableObject
{
    /// <summary>
    /// La couleur unie qui vaut pour ce remplissage, ou null s'il n'en a pas d'unique.
    ///
    /// Un uni rend la sienne, une teinte la couleur calculée ; un dégradé et une image
    /// n'en ont pas. C'est ce que lisent les endroits qui ne savent dessiner qu'un
    /// aplat — la vignette du catalogue de modèles, qui assume depuis B1 d'être un
    /// schéma et non un rendu.
    ///
    /// Sans jeu de couleurs, une référence au jeu reste écrite telle quelle : c'est la
    /// lecture du modèle, pas celle d'un rendu. Les rendus lisent
    /// <see cref="UniformeSelon"/>.
    /// </summary>
    [JsonIgnore] public string? Uniforme => UniformeSelon(null);

    /// <summary>
    /// La couleur unie de ce remplissage dans un document donné : une référence au jeu
    /// y devient la couleur du jeu — celle d'un aplat comme la base d'une teinte, qu'on
    /// mélange APRÈS l'avoir résolue (poste C5b). Null ne résout rien.
    /// </summary>
    public abstract string? UniformeSelon(Charte? charte);

    public abstract FillBrush Clone();

    /// <summary>La sorte d'un remplissage ; un fond absent est réputé uni, ce qu'il deviendra.</summary>
    public static FillKind SorteDe(FillBrush? fill) => fill switch
    {
        GradientFill => FillKind.Gradient,
        ImageFill => FillKind.Image,
        TintFill => FillKind.Tint,
        _ => FillKind.Solid,
    };

    /// <summary>
    /// Un remplissage neuf de la sorte demandée, bâti sur une couleur déjà choisie.
    ///
    /// Ici et non chez chacun des trois porteurs de fond — le bloc, la case, la page :
    /// trois copies de ce choix dériveraient, et c'est le genre d'écart qui ne se voit
    /// que le jour où l'on compare deux panneaux côte à côte.
    /// </summary>
    public static FillBrush Neuf(FillKind sorte, string couleur) => sorte switch
    {
        FillKind.Gradient => new GradientFill(couleur, "#FF1A1A1A"),
        FillKind.Image => new ImageFill(""),
        FillKind.Tint => new TintFill(couleur),
        _ => new SolidFill(couleur),
    };
}

/// <summary>Un aplat, en #AARRGGBB. C'est ce que toute la version 3 du format écrivait.</summary>
public sealed partial class SolidFill : FillBrush
{
    public SolidFill()
    {
    }

    public SolidFill(string color) => Color = color;

    [ObservableProperty]
    [property: JsonPropertyOrder(10)]
    private string _color = "#FFFFFFFF";

    public override string? UniformeSelon(Charte? charte) => charte is null ? Color : charte.Couleur(Color);

    public override FillBrush Clone() => new SolidFill(Color);
}

/// <summary>
/// Un dégradé de deux couleurs, le long d'un axe.
///
/// Les six styles d'ombrage de Publisher — horizontal, vertical, deux diagonales, depuis
/// un coin, depuis le centre — se ramènent à un angle pour les quatre premiers ; les deux
/// derniers sont radiaux et ne sont pas repris. L'angle est en degrés, zéro pour un
/// dégradé de gauche à droite, et il tourne dans le sens des aiguilles d'une montre —
/// l'axe Y du modèle descend, comme en PDF, donc 90° va de haut en bas.
/// </summary>
public sealed partial class GradientFill : FillBrush
{
    public GradientFill()
    {
    }

    public GradientFill(string from, string to, double angle = 90)
    {
        From = from;
        To = to;
        Angle = angle;
    }

    [ObservableProperty]
    [property: JsonPropertyOrder(10)]
    private string _from = "#FFFFFFFF";

    [ObservableProperty]
    [property: JsonPropertyOrder(20)]
    private string _to = "#FF000000";

    [ObservableProperty]
    [property: JsonPropertyOrder(30)]
    private double _angle = 90;

    /// <summary>Un dégradé n'a pas de couleur unique : la vignette prendra sa couleur de départ.</summary>
    public override string? UniformeSelon(Charte? charte) => null;

    /// <summary>
    /// Les deux extrémités de l'axe, dans le repère du bloc, en points.
    ///
    /// L'axe passe par le centre ; ses extrémités sont posées de façon que le rectangle
    /// entier soit couvert, en projetant ses coins sur la direction du dégradé. C'est la
    /// convention de CSS, et c'est aussi la seule qui rende un dégradé à 45° complet dans
    /// les coins plutôt que délavé.
    ///
    /// Calculé ici, une fois, parce que l'écran et l'export auraient chacun eu à le
    /// faire : Avalonia veut deux points relatifs, PDFsharp deux points absolus, et une
    /// différence de signe entre les deux ne se verrait sur aucune comparaison d'images —
    /// elles se seraient trompées ensemble, ou l'une aurait dessiné à l'envers sans que
    /// le pourcentage de pixels différents ne le dise clairement.
    /// </summary>
    public (double X0, double Y0, double X1, double Y1) Extremites(double largeur, double hauteur)
    {
        var radians = Angle * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        var cx = largeur / 2;
        var cy = hauteur / 2;
        // Demi-longueur de la projection du rectangle sur l'axe : c'est elle qui garantit
        // que la première couleur commence exactement au coin le plus en amont.
        var demi = (Math.Abs(largeur * cos) + Math.Abs(hauteur * sin)) / 2;

        return (cx - demi * cos, cy - demi * sin, cx + demi * cos, cy + demi * sin);
    }

    public override FillBrush Clone() => new GradientFill(From, To, Angle);
}

/// <summary>
/// Une image posée dans le bloc — le remplissage par image de Publisher, et non un bloc
/// image.
///
/// C'est ce dont les documents à reprendre se servent : les cinq PNG d'ATA 21 sont des
/// remplissages de forme (<c>draw:fill: bitmap</c>), pas des blocs image.
///
/// <see cref="Fit"/> est le cadrage déjà modélisé pour les blocs image, et il dit la même
/// chose que la case « Lock picture aspect ratio » du dialogue *Fill Effects*.
///
/// <see cref="Rotation"/> est un quart de tour, et ce n'est pas un ornement. Sur les cinq
/// images d'ATA 21, quatre ont exactement le rapport de leur cadre — <c>stretch</c> ne les
/// déforme donc pas — mais la cinquième porte <c>librevenge:rotate: 90</c> : son PNG fait
/// 1062 × 617 (rapport 1,721) dans un cadre de 3,04 × 5,23 in (rapport 0,582), soit
/// exactement l'inverse. Sans la rotation, elle arrive couchée ET écrasée d'un facteur
/// trois. Une image sur cinq, dans le seul document que ce poste existe pour servir.
/// </summary>
public sealed partial class ImageFill : FillBrush
{
    public ImageFill()
    {
    }

    public ImageFill(string path, ImageFit fit = ImageFit.Fill, double rotation = 0)
    {
        Path = path;
        Fit = fit;
        Rotation = rotation;
    }

    /// <summary>Chemin du fichier image, absolu — comme celui d'un bloc image.</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(10)]
    private string _path = "";

    [ObservableProperty]
    [property: JsonPropertyOrder(20)]
    private ImageFit _fit = ImageFit.Fill;

    /// <summary>Rotation de l'image dans son cadre, en degrés, sens horaire.</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(30)]
    private double _rotation;

    public override string? UniformeSelon(Charte? charte) => null;

    /// <summary>Vrai quand la rotation échange la largeur et la hauteur du cadre.</summary>
    [JsonIgnore] public bool QuartDeTour => Math.Abs(Math.Cos(Rotation * Math.PI / 180)) < 0.5;

    /// <summary>
    /// Où poser l'image dans le bloc, <b>avant</b> la rotation : le rendu tourne le repère
    /// autour du centre du bloc, puis dessine dans ce rectangle-là.
    ///
    /// Un quart de tour échange la largeur et la hauteur du cadre autour de ce même
    /// centre : c'est ce qui fait qu'une image couchée remplit un cadre debout sans être
    /// écrasée.
    /// </summary>
    public (double X, double Y, double Largeur, double Hauteur) Cadre(
        double largeur, double hauteur, double pixelsX, double pixelsY)
    {
        var (zoneL, zoneH) = QuartDeTour ? (hauteur, largeur) : (largeur, hauteur);
        var (posee, poseeH) = Cadrage.Poser(Fit, zoneL, zoneH, pixelsX, pixelsY);

        // Le rectangle est centré sur le centre du bloc, quel que soit le quart de tour.
        return ((largeur - posee) / 2, (hauteur - poseeH) / 2, posee, poseeH);
    }

    public override FillBrush Clone() => new ImageFill(Path, Fit, Rotation);
}

/// <summary>
/// Une teinte calculée depuis une couleur de base : l'onglet *Tint* du dialogue
/// *Fill Effects*, qui dérive dix-huit pastilles d'une seule couleur.
///
/// <see cref="Percent"/> vaut 100 pour la couleur de base elle-même, descend vers le
/// blanc — c'est la teinte — et monte vers le noir — c'est la nuance : 0 est blanc,
/// 200 est noir. Une seule échelle continue, monotone, sans valeur ambiguë.
///
/// *Choisi sans certitude* : Publisher étiquette ses pastilles en couverture d'encre et
/// tient deux échelles séparées, « 60 % Tint » et « 60 % Shade ». Le côté clair
/// correspond donc exactement — 60 ici est 60 % de teinte — et le côté sombre est son
/// miroir plutôt que l'étiquette de Publisher. Aucun des deux documents réels ne porte
/// de teinte : le choix est écrit ici pour être discuté, non pour être mesuré.
/// </summary>
public sealed partial class TintFill : FillBrush
{
    public TintFill()
    {
    }

    public TintFill(string color, double percent = 60)
    {
        Color = color;
        Percent = percent;
    }

    /// <summary>Couleur de base, dont la teinte est dérivée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Uniforme))]
    [property: JsonPropertyOrder(10)]
    private string _color = "#FF2E5C8A";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Uniforme))]
    [property: JsonPropertyOrder(20)]
    private double _percent = 60;

    /// <summary>
    /// La couleur effective. Le calcul est ici et non dans les deux rendus, pour la même
    /// raison que les extrémités du dégradé : deux mélanges écrits séparément dériveraient
    /// d'un ton sans que rien ne le signale.
    ///
    /// L'octet alpha de la base est conservé : éclaircir une couleur ne la rend pas opaque.
    ///
    /// Une base prise dans le jeu de couleurs est résolue AVANT le mélange : changer de
    /// jeu change la teinte, et ce qui est mélangé est la couleur du jeu, pas sa
    /// référence.
    /// </summary>
    public override string? UniformeSelon(Charte? charte)
    {
        var baseResolue = charte is null ? Color : charte.Couleur(Color);
        if (!HexColor.TryParse(baseResolue, out var a, out var r, out var g, out var b)) return baseResolue;

        var taux = Math.Clamp(Percent, 0, 200);
        // Vers le blanc en dessous de 100, vers le noir au-dessus : une seule
        // interpolation, dont seule la cible change.
        var (cible, part) = taux <= 100 ? (255.0, 1 - taux / 100) : (0.0, taux / 100 - 1);

        return HexColor.Format(a, Melanger(r, cible, part), Melanger(g, cible, part), Melanger(b, cible, part));
    }

    private static byte Melanger(byte source, double cible, double part) =>
        (byte)Math.Round(Math.Clamp(source + (cible - source) * part, 0, 255));

    public override FillBrush Clone() => new TintFill(Color, Percent);
}

/// <summary>
/// Le cadrage d'une image dans une zone : la taille qu'elle y prend selon le mode.
///
/// Sortie du seul export, où elle vivait pour les blocs image, parce que le remplissage
/// par image en a besoin du même. Deux copies de cette formule seraient deux occasions de
/// diverger, et l'écart ne se verrait que sur une image dont le rapport ne serait pas
/// celui de son cadre — soit une des cinq d'ATA 21.
/// </summary>
public static class Cadrage
{
    public static (double Largeur, double Hauteur) Poser(
        ImageFit fit, double zoneL, double zoneH, double pixelsX, double pixelsY)
    {
        if (fit == ImageFit.Fill || pixelsX <= 0 || pixelsY <= 0) return (zoneL, zoneH);

        var rapportX = zoneL / pixelsX;
        var rapportY = zoneH / pixelsY;
        var rapport = fit == ImageFit.UniformToFill
            ? Math.Max(rapportX, rapportY)
            : Math.Min(rapportX, rapportY);

        return (pixelsX * rapport, pixelsY * rapport);
    }
}
