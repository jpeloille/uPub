using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// La planche de la maquette (poste I4) : ce qui s'ajoute autour de la page, à côté des
/// gestes du fichier principal.
///
///   - les règles fixes, auxquelles on redit où tombe le zéro de la page à chaque mise
///     en page et à chaque défilement ;
///   - la projection de la sélection sur les deux règles ;
///   - les repères libres, prolongés sur toute la planche ;
///   - la bulle de dimensions, sous le bloc qu'on trace ou qu'on redimensionne.
///
/// Rien de tout cela ne vit dans PageRoot, que l'écran compare au PDF : les repères ont
/// leur couche au-dessus de la page, la bulle est dans l'Overlay, la projection dans les
/// règles (règle 4 de la phase). Et rien de tout cela ne change la mise en page : la
/// planche ne bouge pas quand la sélection change (règle 3).
/// </summary>
public partial class PageCanvas
{
    /// <summary>
    /// Écart entre le bas de l'emprise du bloc et le haut de la bulle, en pixels écran :
    /// sous le cadre de sélection et ses poignées, qui descendent à 8 px du bloc.
    /// </summary>
    private const double EcartDeLaBulle = 17;

    /// <summary>
    /// Ce qu'une position en pixels, passée par la mise à l'échelle, peut perdre sous
    /// l'entier qu'elle vaut : 127,9999995 pour 128 à 64 %, 199,99999… pour 200 à « 100 % »
    /// atteint par les pas du zoom, 625,99999936 pour 626 à 244 %. Ajouté avant l'arrondi
    /// au pixel, il garde le trait dans la colonne qui commence à sa position ; un millième
    /// de pixel ne se voit pas, de l'autre côté.
    /// </summary>
    private const double Rattrapage = 1e-3;

    /// <summary>Un trait par repère libre, chacun avec le sens qu'il barre.</summary>
    private readonly Dictionary<RulerGuide, (Rectangle Trait, bool Vertical)> _traitsDesReperes = [];

    /// <summary>Le document dont on suit les listes de repères.</summary>
    private Document? _documentDesReperes;

    private Border _bulle = null!;
    private TextBlock _texteDeLaBulle = null!;

    /// <summary>Appelée une fois par le constructeur, après les poignées.</summary>
    private void PoserLaPlanche()
    {
        // La bulle vient après les poignées : elle passe dessus quand un petit bloc la
        // rapproche d'elles.
        _texteDeLaBulle = new TextBlock();
        _bulle = new Border
        {
            Classes = { "bulleDeDimensions" },
            Child = _texteDeLaBulle,
            IsVisible = false,
        };
        Overlay.Children.Add(_bulle);

        // Le zéro des règles et l'étendue des repères dépendent de la place de la page
        // dans la planche : centrage, taille de la zone visible, défilement. Tous trois
        // passent par une mise en page, après laquelle on relit cette place. Le défilement
        // est écouté en plus : c'est lui que l'œil suit sur les règles.
        LayoutUpdated += (_, _) => CalerLaPlanche();
        Scroller.ScrollChanged += (_, _) => CalerLaPlanche();
    }

    /// <summary>
    /// Redit aux règles où tombe le zéro de la page, et prolonge les repères jusqu'aux
    /// bords de la planche. Idempotente : relancée sans que rien ait bougé, elle n'écrit
    /// rien — c'est ce qui lui permet d'écouter chaque mise en page sans en provoquer une.
    ///
    /// Le zéro est celui de PageRoot, et non de Surface qui le porte : sous la mise à
    /// l'échelle, dès que la largeur de la page en pixels n'est pas entière, l'arrondi de
    /// mise en page décale PageRoot d'un pixel dans Surface — mesuré à 30 %, 33 %, 41 % et
    /// 244 %. Lu sur Surface, le zéro tombait un pixel avant la page. Le coin de PageRoot
    /// est lu dans Surface, puis porté sur la règle : de PageRoot droit à la règle, qui
    /// n'est pas son ancêtre, Avalonia se trompe à travers la mise à l'échelle — 293,6 px
    /// au lieu de 281 à 30 %, 11 au lieu de 32 à l'échelle 2.
    /// </summary>
    private void CalerLaPlanche()
    {
        var page = PageRoot.TranslatePoint(default, Surface) ?? default;
        if (Surface.TranslatePoint(page, TopRuler) is { } haut) TopRuler.Origine = haut.X;
        if (Surface.TranslatePoint(page, LeftRuler) is { } gauche) LeftRuler.Origine = gauche.Y;
        PlacerLesReperes();
    }

    // ------------------------------------------------------------------ projection

    /// <summary>
    /// Projette une emprise, en points de page, sur les deux règles ; rien quand elle est
    /// nulle. C'est l'emprise et non le cadre : un bloc tourné s'étend plus loin que ses
    /// X et sa largeur.
    /// </summary>
    private void Projeter(Extent? emprise)
    {
        TopRuler.DebutProjete = emprise?.Left ?? double.NaN;
        TopRuler.FinProjetee = emprise?.Right ?? double.NaN;
        LeftRuler.DebutProjete = emprise?.Top ?? double.NaN;
        LeftRuler.FinProjetee = emprise?.Bottom ?? double.NaN;
    }

    // ------------------------------------------------------------------ bulle

    /// <summary>
    /// La bulle de dimensions, centrée sous l'emprise : la largeur et la hauteur du bloc
    /// — et non de son emprise —, au dixième de millimètre et dans la culture courante,
    /// comme la barre d'état qui les dit aussi.
    /// </summary>
    private void MontrerLaBulle(Extent emprise, double largeur, double hauteur)
    {
        _texteDeLaBulle.Text = string.Format(CultureInfo.CurrentCulture,
            "L {0:0.0} mm · H {1:0.0} mm", Units.PointsToMm(largeur), Units.PointsToMm(hauteur));

        // Mesurée tout de suite, pour la centrer : la mise en page ne passera qu'après.
        // Visible d'abord — Avalonia mesure à zéro un contrôle caché, et la bulle, à sa
        // première apparition, se posait toute à droite du bloc. Puis remesurée de force :
        // le texte changé n'invalide que lui-même, et la bulle déjà mesurée sous la même
        // contrainte rendait la largeur du texte d'avant.
        _bulle.IsVisible = true;
        _bulle.InvalidateMeasure();
        _bulle.Measure(Size.Infinity);
        Canvas.SetLeft(_bulle, emprise.CenterX * Scale - _bulle.DesiredSize.Width / 2);
        Canvas.SetTop(_bulle, emprise.Bottom * Scale + EcartDeLaBulle);
    }

    private void CacherLaBulle() => _bulle.IsVisible = false;

    // ------------------------------------------------------------------ repères libres

    /// <summary>
    /// Suit les deux listes de repères du document : un trait par repère, posé ou retiré
    /// avec lui. Les listes ne sont pas remplacées en cours de route (Q19 le dira) ; un
    /// autre document, si, et l'on suit alors les siennes.
    /// </summary>
    private void SuivreLesReperes()
    {
        if (_documentDesReperes is { } ancien)
        {
            ancien.VerticalRulerGuides.CollectionChanged -= OnReperesChanged;
            ancien.HorizontalRulerGuides.CollectionChanged -= OnReperesChanged;
        }

        _documentDesReperes = _vm?.Document;

        if (_documentDesReperes is { } document)
        {
            document.VerticalRulerGuides.CollectionChanged += OnReperesChanged;
            document.HorizontalRulerGuides.CollectionChanged += OnReperesChanged;
        }

        RetracerLesReperes();
    }

    private void OnReperesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RetracerLesReperes();

    private void OnRepereChanged(object? sender, PropertyChangedEventArgs e) => PlacerLesReperes();

    /// <summary>
    /// Refait un trait par repère. Les repères se comptent sur les doigts : tout refaire à
    /// chaque ajout ou retrait coûte moins qu'une comptabilité qui pourrait se tromper.
    /// </summary>
    private void RetracerLesReperes()
    {
        foreach (var repere in _traitsDesReperes.Keys) repere.PropertyChanged -= OnRepereChanged;
        _traitsDesReperes.Clear();
        CoucheDesReperes.Children.Clear();

        if (_documentDesReperes is { } document)
        {
            foreach (var repere in document.VerticalRulerGuides) Tracer(repere, vertical: true);
            foreach (var repere in document.HorizontalRulerGuides) Tracer(repere, vertical: false);
        }

        PlacerLesReperes();

        void Tracer(RulerGuide repere, bool vertical)
        {
            // Le repère est le contexte de son trait : c'est par lui qu'un test le retrouve.
            var trait = new Rectangle { Classes = { "repereLibre" }, DataContext = repere };
            if (!_traitsDesReperes.TryAdd(repere, (trait, vertical))) return;

            repere.PropertyChanged += OnRepereChanged;
            CoucheDesReperes.Children.Add(trait);
        }
    }

    /// <summary>
    /// Pose chaque trait : un pixel d'écran, celui qui contient la position du repère, et
    /// d'un bord à l'autre de la planche — la couche est à l'origine de la page, la planche
    /// commence donc avant elle.
    ///
    /// La place du trait est lue sur PageRoot, comme le zéro des règles, et non calculée
    /// d'après l'échelle : PageRoot peut être décalé d'un pixel dans Surface. Et la
    /// position lue tombe parfois juste sous l'entier, d'où le <see cref="Rattrapage"/>
    /// ajouté avant l'arrondi. Sans l'un ni l'autre, le repère tombait un pixel avant le
    /// bord du bloc aimanté sur lui.
    /// </summary>
    private void PlacerLesReperes()
    {
        if (_traitsDesReperes.Count == 0) return;

        var coin = Stage.TranslatePoint(default, Surface) ?? default;
        var etendue = Stage.Bounds.Size;

        foreach (var (repere, (trait, vertical)) in _traitsDesReperes)
        {
            var place = PageRoot.TranslatePoint(new Point(repere.Position, repere.Position), Surface) ?? default;
            if (vertical)
            {
                Canvas.SetLeft(trait, Math.Floor(place.X + Rattrapage));
                Canvas.SetTop(trait, coin.Y);
                trait.Width = 1;
                trait.Height = etendue.Height;
            }
            else
            {
                Canvas.SetLeft(trait, coin.X);
                Canvas.SetTop(trait, Math.Floor(place.Y + Rattrapage));
                trait.Width = etendue.Width;
                trait.Height = 1;
            }
        }
    }
}
