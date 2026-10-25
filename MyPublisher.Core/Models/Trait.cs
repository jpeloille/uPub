using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

/// <summary>
/// Le dessin d'un trait, en coordonnées locales au bloc — l'origine au coin supérieur
/// gauche de son cadre non tourné, comme tout ce qu'un bloc dessine. L'écran le pose dans
/// le panneau du bloc, qui tourne ; l'export le décale du coin du bloc, sous la même
/// rotation. Les deux lisent donc le même tracé, au même endroit, par construction.
///
/// Le fût s'arrête sous la pointe qui le coiffe, à mi-longueur de celle-ci : sans ce
/// retrait, son bout carré dépasserait de part et d'autre de la pointe dès que le trait
/// est épais, et la flèche aurait l'air émoussée.
/// </summary>
public sealed record DessinDuTrait(
    double X1, double Y1, double X2, double Y2,
    IReadOnlyList<(double X, double Y)>? PointeAuDebut,
    IReadOnlyList<(double X, double Y)>? PointeALaFin);

/// <summary>
/// Le bloc est-il un trait ? Posée sur tout bloc, pour que le dock — dont un même gabarit
/// sert à tous les blocs — puisse taire ce qui n'a pas de sens pour un trait : le fond,
/// l'arrondi, la largeur et la hauteur d'un cadre. Faux pour tout ce qui n'est pas une forme.
/// </summary>
public abstract partial class Frame
{
    [JsonIgnore] public virtual bool IsLine => false;
}

/// <summary>
/// L'objet trait, et la flèche — poste A10.
///
/// <b>Le trait se définit par ses deux extrémités, et non par un cadre.</b> C'est le piège
/// que le plan consignait : la taille minimale d'un bloc (<see cref="Snap.MinFrameSize"/>,
/// 2 mm) n'a pas de sens pour un trait horizontal, dont la hauteur est nulle. Le modèle ne
/// lui donne pourtant pas de clés à lui pour ses extrémités : un trait est un bloc couché,
/// long de sa largeur, sans hauteur, que sa rotation oriente — le fût court de
/// (0, H/2) à (L, H/2) dans le repère du bloc. Le milieu du trait est le centre du bloc,
/// autour duquel tout bloc tourne. Ainsi la rotation, l'export tourné, le déplacement, la
/// superposition, le groupement et l'aimantation d'un ensemble le servent sans rien
/// apprendre, et le fichier n'écrit que les deux pointes en plus de ce qu'une forme
/// écrivait déjà.
///
/// Ce qui le manipule passe par ses extrémités : l'outil Ligne, les deux poignées de la
/// planche, les champs du dock et l'import. Tous appellent <see cref="PoserLesExtremites"/>,
/// qui recalcule le bloc entier. Aucun ne passe par un redimensionnement de cadre, et la
/// taille minimale ne s'applique donc jamais à un trait.
/// </summary>
public partial class ShapeFrame
{
    /// <summary>
    /// Une pointe de flèche au début du trait — l'extrémité d'où on l'a tracé. Facultative
    /// à chaque bout, sans effet sur une autre figure. Aucune par défaut, le cas de toutes
    /// les formes écrites jusqu'ici : l'addition ne demande aucune migration, et
    /// <c>FormatVersion</c> ne change pas, au précédent de <see cref="TextFrame.Columns"/>.
    /// </summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(110)]
    private bool _startArrow;

    /// <summary>Une pointe de flèche à la fin du trait, là où le tracé s'est arrêté.</summary>
    [ObservableProperty]
    [property: JsonPropertyOrder(120)]
    private bool _endArrow;

    /// <summary>
    /// Jusqu'où, en points, le survol prend un trait : à cette distance au plus de son
    /// encre — de son axe, moins la demi-épaisseur. Un trait d'un quart de point ne se
    /// viserait pas sans elle ; un rectangle sans épaisseur, comme l'import le posait avant
    /// ce poste, ne se prenait qu'en tombant pile dessus.
    /// </summary>
    public const double ToleranceDeSurvol = 3;

    [JsonIgnore] public override bool IsLine => Shape == ShapeKind.Line;

    partial void OnShapeChanged(ShapeKind value) => OnPropertyChanged(nameof(IsLine));

    // ------------------------------------------------------------------ extrémités

    /// <summary>Le début du trait, en coordonnées page, rotation comprise.</summary>
    [JsonIgnore] public (double X, double Y) Debut => ToPage(0, Height / 2);

    /// <summary>La fin du trait, en coordonnées page, rotation comprise.</summary>
    [JsonIgnore] public (double X, double Y) Fin => ToPage(Width, Height / 2);

    /// <summary>
    /// Pose le trait de (<paramref name="x1"/>, <paramref name="y1"/>) à
    /// (<paramref name="x2"/>, <paramref name="y2"/>), en coordonnées page : la largeur du
    /// bloc prend la longueur, sa hauteur s'annule, sa rotation prend l'angle, et son
    /// centre le milieu des deux points. Le début reste le début : une flèche tracée de
    /// droite à gauche pointe à gauche, avec une rotation de 180°.
    ///
    /// Deux points confondus ne disent aucun angle : le trait garde alors le sien plutôt
    /// que d'en inventer un.
    /// </summary>
    public void PoserLesExtremites(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var longueur = Math.Sqrt(dx * dx + dy * dy);

        if (longueur > 0) Rotation = Angle(dx, dy);
        Height = 0;
        Width = longueur;
        X = (x1 + x2) / 2 - longueur / 2;
        Y = (y1 + y2) / 2;
    }

    /// <summary>
    /// L'angle d'un vecteur en degrés, sens horaire puisque l'axe Y descend, ramené dans
    /// [0, 360[ comme la poignée de rotation le ramène. Arrondi au milliardième de degré :
    /// un trait vertical tracé à la main vaut 90 et non 90,00000000000001, que le dock
    /// afficherait tel quel le jour où on lui demande plus de décimales.
    /// </summary>
    private static double Angle(double dx, double dy)
    {
        var degres = Math.Atan2(dy, dx) * 180 / Math.PI;
        degres = Math.Round(((degres % 360) + 360) % 360, 9);
        return degres >= 360 ? 0 : degres;
    }

    /// <summary>Les huit directions de <see cref="Contraindre"/>, exactes sur les axes.</summary>
    private static readonly (double X, double Y)[] Directions =
    [
        (1, 0), (Math.Sqrt(0.5), Math.Sqrt(0.5)), (0, 1), (-Math.Sqrt(0.5), Math.Sqrt(0.5)),
        (-1, 0), (-Math.Sqrt(0.5), -Math.Sqrt(0.5)), (0, -1), (Math.Sqrt(0.5), -Math.Sqrt(0.5)),
    ];

    /// <summary>
    /// Où mène le pointeur sous <c>Maj</c> : la direction la plus proche parmi 0, 45 et 90°
    /// — et leurs pendants dans les trois autres quadrants — autour de l'extrémité qui
    /// reste, à la distance que le pointeur couvre dans cette direction (sa projection).
    /// Un trait presque horizontal le devient sans changer de longueur apparente ; le
    /// pointeur, lui, peut s'écarter de l'axe sans le tirer avec lui. Les directions sont
    /// tenues dans une table et non recalculées : cos(90°) ne vaut pas zéro en virgule
    /// flottante, et un trait vertical aurait penché d'un millionième de point.
    /// </summary>
    public static (double X, double Y) Contraindre(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        if (dx == 0 && dy == 0) return (x2, y2);

        var huitieme = (int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4));
        var (ux, uy) = Directions[((huitieme % 8) + 8) % 8];
        var projection = dx * ux + dy * uy;
        return (x1 + projection * ux, y1 + projection * uy);
    }

    // ------------------------------------------------------------------ survol

    /// <summary>
    /// Un trait se prend à <see cref="ToleranceDeSurvol"/> points de son encre, et pas
    /// au-delà ; une autre forme, dans son cadre, comme tout bloc.
    /// </summary>
    public override bool HitTest(double px, double py) =>
        IsLine ? DistanceAuTrait(px, py) <= ToleranceDeSurvol + StrokeThickness / 2 : base.HitTest(px, py);

    /// <summary>
    /// La distance d'un point de la page à l'axe du trait — au segment, et non à la droite
    /// qui le porte : au-delà d'une extrémité, on compte jusqu'à elle.
    /// </summary>
    public double DistanceAuTrait(double px, double py)
    {
        var (lx, ly) = ToLocal(px, py);
        var dx = lx - Math.Clamp(lx, 0, Width);
        var dy = ly - Height / 2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ------------------------------------------------------------------ pointes

    /// <summary>
    /// La taille d'une pointe pour un trait d'épaisseur donnée : longue de trois fois
    /// l'épaisseur, large de la même longueur — la flèche moyenne d'Office, que Publisher
    /// pose par défaut. Un trait fin la recevrait minuscule : l'épaisseur y compte au moins
    /// pour 2 pt, si bien qu'un filet d'un demi-point porte encore une pointe de 6 pt.
    /// </summary>
    public static (double Longueur, double DemiLargeur) Pointe(double epaisseur)
    {
        var mesure = Math.Max(epaisseur, 2);
        return (3 * mesure, 1.5 * mesure);
    }

    /// <summary>Le tracé du trait et de ses pointes, en coordonnées locales au bloc.</summary>
    public DessinDuTrait Dessin() => Tracer(Width, Height, StrokeThickness, StartArrow, EndArrow);

    /// <summary>
    /// Le tracé d'un trait couché dans un cadre de la taille donnée, au milieu de sa
    /// hauteur. Une fonction de ses seules grandeurs, pour que l'écran, qui les reçoit par
    /// liaison, n'ait pas à tenir le bloc — ni à s'y abonner (poste Q2).
    /// </summary>
    public static DessinDuTrait Tracer(double largeur, double hauteur, double epaisseur, bool debut, bool fin)
    {
        var y = hauteur / 2;
        var (longueur, demi) = Pointe(epaisseur);
        // Jamais au-delà du milieu : deux pointes sur un trait plus court qu'elles se
        // chevauchent, et le fût, sous elles, ne s'inverse pas.
        var retrait = Math.Min(longueur / 2, largeur / 2);

        return new DessinDuTrait(
            debut ? retrait : 0, y,
            fin ? largeur - retrait : largeur, y,
            debut ? [(0, y), (longueur, y - demi), (longueur, y + demi)] : null,
            fin ? [(largeur, y), (largeur - longueur, y - demi), (largeur - longueur, y + demi)] : null);
    }

    /// <summary>
    /// Jusqu'où le trait dessine hors de son cadre, qui n'a pas de hauteur : la moitié de
    /// son épaisseur, ou la demi-largeur d'une pointe s'il en porte une — c'est ce que la
    /// découpe d'un groupe doit lui laisser (<see cref="Decoupe"/>).
    /// </summary>
    [JsonIgnore]
    public double Debord => StartArrow || EndArrow
        ? Math.Max(StrokeThickness / 2, Pointe(StrokeThickness).DemiLargeur)
        : StrokeThickness / 2;
}
