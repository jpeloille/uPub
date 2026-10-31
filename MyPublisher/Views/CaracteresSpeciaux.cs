using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MyPublisher.Views;

/// <summary>Ce que <see cref="CaracteresSpeciaux"/> marque : les quatre caractères que Publisher montre.</summary>
public enum CaractereSpecial
{
    /// <summary>Une espace : un point, au milieu de sa chasse.</summary>
    Espace,

    /// <summary>Une tabulation : une flèche, au milieu de ce qu'elle avance.</summary>
    Tabulation,

    /// <summary>Une fin de paragraphe — un saut, ou le bout du texte : ¶, juste après le dernier caractère.</summary>
    FinDeParagraphe,

    /// <summary>Un saut de ligne forcé, U+2028 (poste N16) : ↵, juste après le dernier caractère de sa ligne.</summary>
    SautDeLigne,
}

/// <summary>
/// Un caractère spécial posé, dans le repère du bloc non tourné : sa sorte, sa position dans le
/// texte, son point d'insertion sur la ligne de base de sa ligne — celui que
/// <see cref="GeometrieDuTexte.PointDe"/> donne (poste N3) —, la chasse qu'il occupe sur sa ligne
/// (nulle pour une fin), et le corps du texte qui le porte.
/// </summary>
public readonly record struct CaracterePose(CaractereSpecial Sorte, int Index, Point Point, double Chasse, double Taille);

/// <summary>
/// Un niveau de découpe entre l'<c>Overlay</c> et un bloc de texte : la transformation qui mène du
/// repère d'un contrôle à celui du niveau d'au-dessus, et le rectangle de ce contrôle qui découpe ce
/// qu'il porte — la page, le bloc, un groupe qui rogne (Q5).
/// </summary>
public readonly record struct CoucheDeDecoupe(Matrix Transformation, Rect Decoupe);

/// <summary>
/// Les caractères spéciaux d'un bloc de texte : le contrôle qui dessine sa composition, les niveaux
/// de découpe qui mènent de l'<c>Overlay</c> à lui, du plus haut — la page — au contrôle lui-même,
/// et ses caractères posés.
/// </summary>
public sealed record CaracteresDuBloc(
    TexteCompose Compose, IReadOnlyList<CoucheDeDecoupe> Couches, IReadOnlyList<CaracterePose> Caracteres)
{
    /// <summary>Du repère du bloc non tourné à celui de l'<c>Overlay</c> : les niveaux, composés.</summary>
    public Matrix Transformation
    {
        get
        {
            var transformation = Matrix.Identity;
            foreach (var couche in Couches) transformation = couche.Transformation * transformation;
            return transformation;
        }
    }
}

/// <summary>
/// Les caractères spéciaux, dessinés sur le texte composé (poste N22 ; fiche 23, a27 et d14) :
/// <i>Affichage ▸ Caractères spéciaux</i>, <c>Ctrl+Maj+Y</c>. Un point pour chaque espace, une
/// flèche pour chaque tabulation, ¶ à chaque fin de paragraphe et ↵ à chaque saut de ligne forcé,
/// aux places que la géométrie du texte donne (<see cref="GeometrieDuTexte"/>, poste N3), au corps
/// du texte qui les porte.
///
/// Il vit dans l'<c>Overlay</c> de la planche, hors de <c>PageRoot</c>, que l'écran compare au PDF
/// (règle 4) : rien de ce qu'il dessine ne s'imprime, ni ne change l'encre de la page. La planche
/// lui donne tout (<c>PageCanvas.CaracteresSpeciaux.cs</c>) : pour chaque bloc, les caractères dans
/// le repère du bloc non tourné, et les niveaux de découpe qui y mènent — zoom, place et rotation
/// comprises, la page et le bloc découpant ce qui en sort, comme ils découpent le texte. Il ne
/// calcule rien : il dessine. Sa couleur est celle de l'apparence, posée par le style de la planche
/// (<c>PageCanvas.axaml</c>).
/// </summary>
public sealed class CaracteresSpeciaux : Control
{
    /// <summary>Le pinceau des marques.</summary>
    public static readonly StyledProperty<IBrush?> EncreProperty =
        AvaloniaProperty.Register<CaracteresSpeciaux, IBrush?>(nameof(Encre));

    static CaracteresSpeciaux()
    {
        AffectsRender<CaracteresSpeciaux>(EncreProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<CaracteresSpeciaux>(false);
    }

    /// <summary>Le signe de chaque sorte, tel que Publisher le montre.</summary>
    public static string Signe(CaractereSpecial sorte) => sorte switch
    {
        CaractereSpecial.Espace => "\u00B7",
        CaractereSpecial.Tabulation => "\u2192",
        CaractereSpecial.FinDeParagraphe => "\u00B6",
        _ => "\u21B5",
    };

    /// <summary>Les signes mis en forme, par sorte et par corps : un texte en a peu de différents.</summary>
    private readonly Dictionary<(CaractereSpecial, double), FormattedText> _signes = [];

    public IBrush? Encre
    {
        get => GetValue(EncreProperty);
        set => SetValue(EncreProperty, value);
    }

    /// <summary>Les blocs et leurs caractères, dans l'ordre de la page ; vide quand la bascule est fermée.</summary>
    public IReadOnlyList<CaracteresDuBloc> Blocs { get; private set; } = [];

    /// <summary>Pose les caractères à dessiner.</summary>
    public void Poser(IReadOnlyList<CaracteresDuBloc> blocs)
    {
        Blocs = blocs;
        InvalidateVisual();
    }

    /// <summary>Rien à dessiner : la bascule est fermée.</summary>
    public void Effacer()
    {
        if (Blocs.Count == 0) return;

        Blocs = [];
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EncreProperty) _signes.Clear();
    }

    public override void Render(DrawingContext contexte)
    {
        if (Encre is not { } encre) return;

        var etats = new List<DrawingContext.PushedState>();
        foreach (var bloc in Blocs)
        {
            // Du plus haut niveau au bloc : chaque niveau se pose dans le repère du précédent, et
            // découpe ce qui sort de lui, comme il découpe le texte qu'il porte.
            foreach (var couche in bloc.Couches)
            {
                etats.Add(contexte.PushTransform(couche.Transformation));
                etats.Add(contexte.PushClip(couche.Decoupe));
            }

            foreach (var caractere in bloc.Caracteres)
            {
                if (caractere.Taille <= 0) continue;

                var signe = SigneMisEnForme(caractere.Sorte, caractere.Taille, encre);
                // Une espace et une tabulation montrent leur signe au milieu de ce qu'elles
                // occupent ; une fin, juste après le dernier caractère de sa ligne.
                var x = caractere.Sorte is CaractereSpecial.Espace or CaractereSpecial.Tabulation
                    ? caractere.Point.X + (caractere.Chasse - signe.Width) / 2
                    : caractere.Point.X;
                contexte.DrawText(signe, new Point(x, caractere.Point.Y - signe.Baseline));
            }

            for (var i = etats.Count - 1; i >= 0; i--) etats[i].Dispose();
            etats.Clear();
        }
    }

    private FormattedText SigneMisEnForme(CaractereSpecial sorte, double taille, IBrush encre)
    {
        if (_signes.TryGetValue((sorte, taille), out var connu)) return connu;

        var signe = new FormattedText(Signe(sorte), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), taille, encre);
        _signes[(sorte, taille)] = signe;
        return signe;
    }
}
