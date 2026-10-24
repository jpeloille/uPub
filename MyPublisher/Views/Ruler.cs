using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Règle graduée de la planche. Elle est dessinée plutôt que composée : une graduation
/// fine sur toute la largeur de la planche fait des centaines de traits, et autant de
/// contrôles coûteraient cher pour un décor qui ne reçoit qu'un clic.
///
/// Les graduations sont en millimètres, comme les champs de position du panneau et la
/// barre d'état. Le zéro est le coin de la page, pas celui de la fenêtre : c'est la
/// page qu'on mesure.
///
/// Depuis le poste I4, la règle est FIXE, au bord de la zone visible, et non plus collée
/// à la page qu'elle suivait dans le défilement. Elle se gradue donc de part et d'autre
/// de la page — négative à gauche et au-dessus —, et c'est la planche qui lui dit, à
/// chaque mise en page et à chaque défilement, où tombe le zéro (<see cref="Origine"/>).
/// Elle porte aussi la projection de la sélection : une bande d'accent pâle sur
/// l'emprise du bloc, que la planche lui donne en points de page.
///
/// Ses couleurs sont celles de l'apparence, que la planche lui passe par un style : la
/// règle n'en écrit aucune.
/// </summary>
public class Ruler : Control
{
    /// <summary>
    /// Épaisseur de la règle, en pixels écran — elle ne suit pas le zoom : les 20 px de la
    /// maquette, et le filet d'un pixel qui la sépare de la planche.
    /// </summary>
    public const double Breadth = 21;

    /// <summary>Longueur des graduations principales et fines, comme la maquette.</summary>
    private const double TraitPrincipal = 9;
    private const double TraitFin = 4;

    /// <summary>
    /// Écart minimal entre deux graduations principales, en pixels. Au zoom de la
    /// maquette, dix millimètres en font 32 : c'est le pas qu'elle dessine.
    /// </summary>
    private const double EcartPrincipalMinimal = 25;

    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<Ruler, double>(nameof(Scale), 1.0);

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<Ruler, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public static readonly StyledProperty<double> OrigineProperty =
        AvaloniaProperty.Register<Ruler, double>(nameof(Origine));

    public static readonly StyledProperty<double> DebutProjeteProperty =
        AvaloniaProperty.Register<Ruler, double>(nameof(DebutProjete), double.NaN);

    public static readonly StyledProperty<double> FinProjeteeProperty =
        AvaloniaProperty.Register<Ruler, double>(nameof(FinProjetee), double.NaN);

    public static readonly StyledProperty<IBrush?> FondProperty =
        AvaloniaProperty.Register<Ruler, IBrush?>(nameof(Fond));

    public static readonly StyledProperty<IBrush?> BordProperty =
        AvaloniaProperty.Register<Ruler, IBrush?>(nameof(Bord));

    public static readonly StyledProperty<IBrush?> GraduationProperty =
        AvaloniaProperty.Register<Ruler, IBrush?>(nameof(Graduation));

    public static readonly StyledProperty<IBrush?> GraduationFineProperty =
        AvaloniaProperty.Register<Ruler, IBrush?>(nameof(GraduationFine));

    public static readonly StyledProperty<IBrush?> ChiffresProperty =
        AvaloniaProperty.Register<Ruler, IBrush?>(nameof(Chiffres));

    public static readonly StyledProperty<IBrush?> ProjectionProperty =
        AvaloniaProperty.Register<Ruler, IBrush?>(nameof(Projection));

    static Ruler()
    {
        AffectsMeasure<Ruler>(OrientationProperty);
        AffectsRender<Ruler>(ScaleProperty, OrientationProperty, OrigineProperty,
            DebutProjeteProperty, FinProjeteeProperty, FondProperty, BordProperty,
            GraduationProperty, GraduationFineProperty, ChiffresProperty, ProjectionProperty);
    }

    /// <summary>Facteur points → pixels écran, zoom compris.</summary>
    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Où tombe le zéro de la page, en pixels depuis le bord de départ de la règle — son
    /// bord gauche, ou son bord haut. Négatif quand la page a défilé au-delà.
    /// </summary>
    public double Origine
    {
        get => GetValue(OrigineProperty);
        set => SetValue(OrigineProperty, value);
    }

    /// <summary>
    /// Début et fin de l'emprise projetée, en points de page ; NaN quand il n'y a rien à
    /// projeter.
    /// </summary>
    public double DebutProjete
    {
        get => GetValue(DebutProjeteProperty);
        set => SetValue(DebutProjeteProperty, value);
    }

    public double FinProjetee
    {
        get => GetValue(FinProjeteeProperty);
        set => SetValue(FinProjeteeProperty, value);
    }

    public IBrush? Fond
    {
        get => GetValue(FondProperty);
        set => SetValue(FondProperty, value);
    }

    public IBrush? Bord
    {
        get => GetValue(BordProperty);
        set => SetValue(BordProperty, value);
    }

    public IBrush? Graduation
    {
        get => GetValue(GraduationProperty);
        set => SetValue(GraduationProperty, value);
    }

    public IBrush? GraduationFine
    {
        get => GetValue(GraduationFineProperty);
        set => SetValue(GraduationFineProperty, value);
    }

    public IBrush? Chiffres
    {
        get => GetValue(ChiffresProperty);
        set => SetValue(ChiffresProperty, value);
    }

    public IBrush? Projection
    {
        get => GetValue(ProjectionProperty);
        set => SetValue(ProjectionProperty, value);
    }

    private bool Horizontal => Orientation == Orientation.Horizontal;

    /// <summary>Sa seule épaisseur : la longueur, c'est la planche qui la lui donne.</summary>
    protected override Size MeasureOverride(Size availableSize) =>
        Horizontal ? new Size(0, Breadth) : new Size(Breadth, 0);

    /// <summary>Convertit une position de page, en points, vers un pixel de la règle.</summary>
    public double ToScreen(double points) => Origine + points * Scale;

    /// <summary>Et l'inverse : ce que le pointeur désigne, en points de page.</summary>
    public double ToPage(double screen) => Scale <= 0 ? 0 : (screen - Origine) / Scale;

    /// <summary>
    /// Le pas des graduations principales, en millimètres, et le nombre de graduations
    /// fines qui le partagent. Le plus petit pas de la suite 1, 2, 5, 10… qui laisse
    /// <see cref="EcartPrincipalMinimal"/> pixels entre deux traits : au zoom de la maquette,
    /// dix millimètres, partagés en quatre comme elle les partage. Un pas de cinq se
    /// partage en cinq, pour tomber sur des millimètres ronds.
    /// </summary>
    public static (double Principal, int Divisions) Pas(double pixelsParMillimetre)
    {
        foreach (var decade in new[] { 1.0, 10, 100, 1000 })
        {
            foreach (var (facteur, divisions) in new[] { (1.0, 4), (2.0, 4), (5.0, 5) })
            {
                var pas = facteur * decade;
                if (pas * pixelsParMillimetre >= EcartPrincipalMinimal) return (pas, divisions);
            }
        }

        return (10000, 4);
    }

    public override void Render(DrawingContext context)
    {
        var taille = Bounds.Size;
        var longueur = Horizontal ? taille.Width : taille.Height;
        var face = Breadth - 1;   // ce que la règle a de large, son filet ôté

        if (Fond is { } fond) context.FillRectangle(fond, new Rect(taille));

        // Le filet du bord jouxte la planche : c'est lui qui fait lire la règle comme
        // adossée à la zone de travail plutôt que posée à côté.
        if (Bord is { } bord)
        {
            context.FillRectangle(bord, Horizontal
                ? new Rect(0, face, taille.Width, 1)
                : new Rect(face, 0, 1, taille.Height));
        }

        var millimetre = Units.MmToPoints(1) * Scale;
        if (millimetre <= 0 || longueur <= 0) return;

        var (principal, divisions) = Pas(millimetre);
        var fin = principal / divisions;
        var principale = Graduation is { } g ? new Pen(g) : null;
        var fine = GraduationFine is { } gf ? new Pen(gf) : null;

        // Seulement ce qui se voit : à 800 %, une page A4 ferait des milliers de traits.
        var premier = (long)Math.Floor(-Origine / millimetre / fin);
        var dernier = (long)Math.Ceiling((longueur - Origine) / millimetre / fin);

        for (var k = premier; k <= dernier; k++)
        {
            var mm = k * fin;
            // Au milieu du pixel : un trait d'un pixel reste net au lieu de s'étaler sur deux.
            var at = Math.Floor(ToScreen(Units.MmToPoints(mm))) + 0.5;
            if (at < 0 || at > longueur) continue;

            var estPrincipale = k % divisions == 0;
            var crayon = estPrincipale ? principale : fine;
            var trait = estPrincipale ? TraitPrincipal : TraitFin;

            if (crayon is not null)
            {
                context.DrawLine(crayon,
                    Horizontal ? new Point(at, face - trait) : new Point(face - trait, at),
                    Horizontal ? new Point(at, face) : new Point(face, at));
            }

            // Un nombre une graduation principale sur deux, comme la maquette : la place
            // de l'écrire, et une règle qui ne se lit pas comme une colonne de chiffres.
            if (!estPrincipale || Chiffres is not { } encre || (k / divisions) % 2 != 0) continue;

            var texte = new FormattedText(
                Math.Round(mm).ToString(CultureInfo.CurrentCulture), CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 9, encre);

            if (Horizontal)
            {
                // Le nombre se pose contre son trait, du côté des valeurs qui croissent.
                context.DrawText(texte, new Point(at + 2.5, 1));
            }
            else
            {
                // Couché, et lu de bas en haut : une règle de vingt pixels ne loge pas
                // « −100 » debout. Il se pose sous son trait, comme à droite du sien
                // sur la règle du haut.
                using (context.PushTransform(Matrix.CreateRotation(-Math.PI / 2) *
                                             Matrix.CreateTranslation(1, at + 2.5 + texte.Width)))
                {
                    context.DrawText(texte, new Point(0, 0));
                }
            }
        }

        // La projection de la sélection, sur toute la face : elle passe sur les
        // graduations, comme dans la maquette, et l'accent à 14 % les laisse voir.
        if (Projection is { } bande && !double.IsNaN(DebutProjete) && !double.IsNaN(FinProjetee))
        {
            // Bornée au pixel, pour des bords nets : l'emprise tombe au pixel près.
            var debut = Math.Round(ToScreen(Math.Min(DebutProjete, FinProjetee)));
            var finBande = Math.Round(ToScreen(Math.Max(DebutProjete, FinProjetee)));
            debut = Math.Max(0, debut);
            finBande = Math.Min(longueur, finBande);
            if (finBande > debut)
            {
                context.FillRectangle(bande, Horizontal
                    ? new Rect(debut, 0, finBande - debut, face)
                    : new Rect(0, debut, face, finBande - debut));
            }
        }
    }
}
