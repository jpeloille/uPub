using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace MyPublisher.Views;

/// <summary>
/// Les marques de la saisie, dessinées sur le texte composé (poste N21, voie 2a ; fiche 23,
/// a10, a11, a20 et a21) : la sélection, le curseur et le texte que la méthode d'entrée est en
/// train de composer — le pré-édit.
///
/// Pendant la saisie, l'écran montrait le champ en texte brut à la place de la composition ; il
/// montre désormais ce qui s'imprime. Le champ, caché, garde la frappe, la méthode d'entrée et le
/// presse-papiers ; ce contrôle dessine ce que le champ dessinait, mais à la place que la
/// composition donne à chaque caractère (<see cref="GeometrieDuTexte"/>). Il vit dans
/// l'<c>Overlay</c> de la planche, hors de <c>PageRoot</c>, que l'écran compare au PDF : rien de ce
/// qu'il dessine ne s'imprime, et la page ne change pas d'un pixel quand on entre dans un texte.
///
/// La planche lui donne tout, dans le repère du bloc non tourné, et la transformation qui mène
/// de ce repère au sien — mise à l'échelle, rotation et place du bloc comprises
/// (<see cref="Poser"/>). Il ne calcule rien : il dessine. Ses couleurs sont celles de
/// l'apparence, posées par le style de la planche (<c>PageCanvas.axaml</c>).
/// </summary>
public sealed class MarquesDuTexte : Control
{
    /// <summary>Le pinceau du curseur, et du soulignement du pré-édit.</summary>
    public static readonly StyledProperty<IBrush?> CurseurProperty =
        AvaloniaProperty.Register<MarquesDuTexte, IBrush?>(nameof(Curseur));

    /// <summary>La teinte posée sur le texte sélectionné.</summary>
    public static readonly StyledProperty<IBrush?> SelectionProperty =
        AvaloniaProperty.Register<MarquesDuTexte, IBrush?>(nameof(Selection));

    /// <summary>Le fond du pré-édit, qui couvre la composition le temps qu'il dure.</summary>
    public static readonly StyledProperty<IBrush?> FondDuPreEditProperty =
        AvaloniaProperty.Register<MarquesDuTexte, IBrush?>(nameof(FondDuPreEdit));

    static MarquesDuTexte()
    {
        AffectsRender<MarquesDuTexte>(CurseurProperty, SelectionProperty, FondDuPreEditProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MarquesDuTexte>(false);
    }

    /// <summary>
    /// Le curseur clignote comme celui d'un champ : allumé une demi-seconde, éteint autant. Il se
    /// rallume à chaque déplacement, pour qu'on le voie là où il arrive.
    /// </summary>
    private static readonly TimeSpan Clignotement = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherTimer _clignotement;

    public MarquesDuTexte()
    {
        _clignotement = new DispatcherTimer { Interval = Clignotement };
        _clignotement.Tick += (_, _) =>
        {
            CurseurAllume = !CurseurAllume;
            InvalidateVisual();
        };
    }

    public IBrush? Curseur
    {
        get => GetValue(CurseurProperty);
        set => SetValue(CurseurProperty, value);
    }

    public IBrush? Selection
    {
        get => GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    public IBrush? FondDuPreEdit
    {
        get => GetValue(FondDuPreEditProperty);
        set => SetValue(FondDuPreEditProperty, value);
    }

    /// <summary>Du repère du bloc non tourné au repère de ce contrôle, celui de l'<c>Overlay</c>.</summary>
    public Matrix Transformation { get; private set; } = Matrix.Identity;

    /// <summary>Les rectangles de la sélection, un par ligne, dans le repère du bloc ; vide sans sélection.</summary>
    public IReadOnlyList<Rect> Plages { get; private set; } = [];

    /// <summary>
    /// Le curseur, un rectangle de largeur nulle dans le repère du bloc ; nul quand il ne se
    /// montre pas — hors saisie, quand une sélection le remplace, quand le champ n'a pas le focus.
    /// </summary>
    public Rect? CurseurPose { get; private set; }

    /// <summary>Le pré-édit de la méthode d'entrée tel qu'il se pose, ou rien.</summary>
    public PreEditPose? PreEdit { get; private set; }

    /// <summary>La phase du clignotement : le curseur posé ne se dessine que quand il est allumé.</summary>
    public bool CurseurAllume { get; private set; } = true;

    /// <summary>
    /// Pose les marques de la saisie. <paramref name="transformation"/> mène du repère du bloc à
    /// celui de ce contrôle. Un curseur qui bouge se rallume.
    /// </summary>
    public void Poser(Matrix transformation, IReadOnlyList<Rect> plages, Rect? curseur, PreEditPose? preEdit)
    {
        var bouge = curseur != CurseurPose || transformation != Transformation ||
                    preEdit?.Chaine != PreEdit?.Chaine || preEdit?.Origine != PreEdit?.Origine;
        if (!bouge && plages.SequenceEqual(Plages) && (curseur is null || _clignotement.IsEnabled)) return;

        Transformation = transformation;
        Plages = plages;
        CurseurPose = curseur;
        PreEdit = preEdit;

        if (curseur is null)
        {
            _clignotement.Stop();
        }
        else if (bouge || !_clignotement.IsEnabled)
        {
            CurseurAllume = true;
            _clignotement.Stop();
            _clignotement.Start();
        }

        InvalidateVisual();
    }

    /// <summary>Rien à dessiner : la saisie est refermée, ou elle n'a pas encore sa sélection.</summary>
    public void Effacer()
    {
        if (CurseurPose is null && Plages.Count == 0 && PreEdit is null) return;

        Plages = [];
        CurseurPose = null;
        PreEdit = null;
        _clignotement.Stop();
        InvalidateVisual();
    }

    /// <summary>Les marques dessinent-elles quelque chose ? Ce que les tests lisent.</summary>
    public bool Visibles => CurseurPose is not null || Plages.Count > 0 || PreEdit is not null;

    public override void Render(DrawingContext contexte)
    {
        if (!Visibles) return;

        using (contexte.PushTransform(Transformation))
        {
            // La sélection, d'une teinte translucide : le texte composé reste lisible dessous.
            if (Selection is { } teinte)
            {
                foreach (var plage in Plages) contexte.FillRectangle(teinte, plage);
            }

            if (PreEdit is { } preEdit)
            {
                if (FondDuPreEdit is { } fond) contexte.FillRectangle(fond, preEdit.Fond);
                contexte.DrawText(preEdit.Texte, preEdit.Origine);
            }
        }

        // Le curseur et le soulignement du pré-édit, d'un pixel d'écran à tous les zooms : leurs
        // bouts passent par la transformation, le trait non.
        if (Curseur is not { } pinceau) return;
        var stylo = new Pen(pinceau, 1);

        if (PreEdit is { } edition)
        {
            var sous = edition.Fond.Bottom - 1 / Echelle();
            contexte.DrawLine(stylo,
                new Point(edition.Fond.Left, sous).Transform(Transformation),
                new Point(edition.Fond.Right, sous).Transform(Transformation));
        }

        if (CurseurPose is { } curseur && CurseurAllume)
        {
            contexte.DrawLine(stylo,
                curseur.TopLeft.Transform(Transformation), curseur.BottomLeft.Transform(Transformation));
        }
    }

    /// <summary>Combien de pixels vaut un point du bloc, rotation mise à part.</summary>
    private double Echelle()
    {
        var m = Transformation;
        var echelle = Math.Sqrt(m.M11 * m.M11 + m.M12 * m.M12);
        return echelle > 0 ? echelle : 1;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clignotement.Stop();
    }
}

/// <summary>
/// Le pré-édit posé (poste N21) : le texte que la méthode d'entrée compose, mis en forme au style
/// du caractère qui précède le curseur, son coin haut-gauche, et le fond qui couvre la composition
/// sous lui — tout dans le repère du bloc non tourné.
/// </summary>
public sealed record PreEditPose(FormattedText Texte, Point Origine, Rect Fond, string Chaine);
