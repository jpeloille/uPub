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
    /// </summary>
    private void CalerLaPlanche()
    {
        if (Surface.TranslatePoint(default, TopRuler) is { } haut) TopRuler.Origine = haut.X;
        if (Surface.TranslatePoint(default, LeftRuler) is { } gauche) LeftRuler.Origine = gauche.Y;
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
        _bulle.Measure(Size.Infinity);
        Canvas.SetLeft(_bulle, emprise.CenterX * Scale - _bulle.DesiredSize.Width / 2);
        Canvas.SetTop(_bulle, emprise.Bottom * Scale + EcartDeLaBulle);
        _bulle.IsVisible = true;
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
    /// </summary>
    private void PlacerLesReperes()
    {
        if (_traitsDesReperes.Count == 0) return;

        var coin = Stage.TranslatePoint(default, Surface) ?? default;
        var etendue = Stage.Bounds.Size;

        foreach (var (repere, (trait, vertical)) in _traitsDesReperes)
        {
            var position = Math.Floor(repere.Position * Scale);
            if (vertical)
            {
                Canvas.SetLeft(trait, position);
                Canvas.SetTop(trait, coin.Y);
                trait.Width = 1;
                trait.Height = etendue.Height;
            }
            else
            {
                Canvas.SetLeft(trait, coin.X);
                Canvas.SetTop(trait, position);
                trait.Width = etendue.Width;
                trait.Height = 1;
            }
        }
    }
}
