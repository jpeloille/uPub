using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using MyPublisher.Converters;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// Le sélecteur de couleur du panneau : les cinq pastilles du jeu de couleurs du
/// document, puis le sélecteur libre (poste C5b).
///
/// Une pastille ne copie pas sa couleur : elle écrit une <b>référence</b>
/// (<c>scheme:accent1</c>), comme Publisher, et le bloc suivra le jeu quand on en
/// changera. Une couleur choisie au sélecteur libre est écrite en hexadécimal, et reste
/// libre.
///
/// **Pourquoi un contrôle et non une liaison au <c>ColorPicker</c>.** Parce que le piège
/// que C1 et C5c ont chacun payé revient ici sous une forme neuve : le sélecteur renvoie
/// vers sa source la couleur qu'il vient d'en lire. Branché sur une référence, il lirait
/// la couleur du jeu et la renverrait en hexadécimal — et sélectionner un bloc le
/// détacherait du jeu, sans geste. Ce contrôle sait ce qu'il a affiché : une couleur
/// qui revient égale à celle qu'il vient de poser n'est pas un choix, et n'est pas écrite.
/// Il est seul à parler au sélecteur, si bien que la règle tient en un endroit pour les
/// dix couleurs du panneau.
/// </summary>
public sealed class SelecteurDeCouleur : UserControl
{
    public static readonly StyledProperty<string?> ValeurProperty =
        AvaloniaProperty.Register<SelecteurDeCouleur, string?>(
            nameof(Valeur), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<JeuDeCouleurs?> JeuProperty =
        AvaloniaProperty.Register<SelecteurDeCouleur, JeuDeCouleurs?>(nameof(Jeu));

    /// <summary>
    /// La couleur du sélecteur libre, liée à double sens à celui-ci. Une liaison entre ce
    /// contrôle et l'enfant qu'il a créé plutôt qu'un abonnement à son événement : les
    /// deux vivent et meurent ensemble, il n'y a rien à détacher.
    /// </summary>
    private static readonly StyledProperty<Color> CouleurLibreProperty =
        AvaloniaProperty.Register<SelecteurDeCouleur, Color>("CouleurLibre");

    private static readonly string[] Noms = ["Principale", "Accent 1", "Accent 2", "Accent 3", "Accent 4"];

    private static readonly IBrush Liseré = new SolidColorBrush(Color.Parse("#FFB6BFCA"));
    private static readonly IBrush Retenue = new SolidColorBrush(Color.Parse("#FF1A1A1A"));

    private readonly ColorPicker _libre;
    private readonly Border[] _pastilles = new Border[5];
    private readonly TextBlock _legende;

    /// <summary>La dernière couleur posée sur le sélecteur libre par ce contrôle lui-même.</summary>
    private Color? _posee;

    static SelecteurDeCouleur()
    {
        ValeurProperty.Changed.AddClassHandler<SelecteurDeCouleur>((c, _) => c.Actualiser());
        JeuProperty.Changed.AddClassHandler<SelecteurDeCouleur>((c, _) => c.Actualiser());
        CouleurLibreProperty.Changed.AddClassHandler<SelecteurDeCouleur>(
            (c, e) => c.SurCouleurLibre(e.GetNewValue<Color>()));
    }

    public SelecteurDeCouleur()
    {
        var rangee = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        for (var i = 0; i < _pastilles.Length; i++)
        {
            var emplacement = (EmplacementDeCouleur)i;
            _pastilles[i] = new Border { Width = 22, Height = 16, BorderBrush = Liseré, BorderThickness = new Thickness(1) };

            var bouton = new Button
            {
                Content = _pastilles[i],
                Padding = new Thickness(2),
                Tag = emplacement,
                Classes = { "pastille" },
            };
            ToolTip.SetTip(bouton, Noms[i]);
            bouton.Click += (_, _) => Choisir(emplacement);
            rangee.Children.Add(bouton);
        }

        _libre = new ColorPicker { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
        _libre[!!ColorView.ColorProperty] = this[!!CouleurLibreProperty];

        _legende = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#FF8A929E")) };

        Content = new StackPanel { Children = { rangee, _legende, _libre } };
        Actualiser();
    }

    /// <summary>La couleur du modèle : #AARRGGBB libre, ou référence au jeu.</summary>
    public string? Valeur
    {
        get => GetValue(ValeurProperty);
        set => SetValue(ValeurProperty, value);
    }

    /// <summary>Le jeu de couleurs du document, dont les pastilles montrent les cinq couleurs.</summary>
    public JeuDeCouleurs? Jeu
    {
        get => GetValue(JeuProperty);
        set => SetValue(JeuProperty, value);
    }

    /// <summary>
    /// Une pastille écrit la référence. L'opacité propre de la couleur courante est
    /// gardée : prendre l'accent 1 pour un fond à 50 % donne un accent 1 à 50 %.
    /// </summary>
    private void Choisir(EmplacementDeCouleur emplacement)
    {
        var alpha = HexColor.Alpha(Valeur);
        Valeur = ReferenceDuJeu.Couleur(emplacement, alpha is { } a && a != 0xFF ? a : null);
    }

    private void SurCouleurLibre(Color couleur)
    {
        // L'écho de ce que ce contrôle vient de poser n'est pas un choix : c'est le piège
        // du sélecteur qui renvoie ce qu'il lit. Écrit, il détacherait du jeu le bloc
        // qu'on n'a fait que sélectionner.
        if (_posee == couleur) return;

        var hexa = HexColor.Format(couleur.A, couleur.R, couleur.G, couleur.B);
        if (hexa == Valeur) return;

        Valeur = hexa;
    }

    private void Actualiser()
    {
        var jeu = Jeu ?? JeuxPredefinis.CouleursParDefaut;
        var charte = new Charte(jeu, JeuxPredefinis.PolicesParDefaut);
        var reference = ReferenceDuJeu.TryCouleur(Valeur, out var emplacement, out _);

        for (var i = 0; i < _pastilles.Length; i++)
        {
            _pastilles[i].Background = Conv.ToBrush(jeu.Couleur((EmplacementDeCouleur)i));
            var retenue = reference && (int)emplacement == i;
            _pastilles[i].BorderBrush = retenue ? Retenue : Liseré;
            _pastilles[i].BorderThickness = new Thickness(retenue ? 2 : 1);
        }

        _legende.Text = reference ? $"Couleur du jeu : {Noms[(int)emplacement]}" : "Couleur libre";

        // Le sélecteur libre montre la couleur qu'on voit sur la page, référence résolue.
        // Une couleur illisible ne lui est pas posée : il garderait sinon un transparent
        // qu'il finirait par renvoyer.
        if (Conv.ToColor(charte.Couleur(Valeur)) is not { } affichee) return;

        _posee = affichee;
        SetValue(CouleurLibreProperty, affichee);
    }
}
