using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MyPublisher.Models;

namespace MyPublisher.Converters;

/// <summary>
/// Une couleur du modèle en pinceau, référence au jeu résolue (poste C5b). Deux entrées :
/// la couleur, et la charte du document. La résolution est celle du noyau — la même que
/// l'export appelle.
///
/// C'est ce qui remplace <see cref="Conv.Brush"/> partout où une couleur de bloc se peint
/// par liaison : les contours, et le texte pendant la saisie. Sans elle, un contour pris
/// dans le jeu ne se dessinerait pas à l'écran — « scheme:main » n'est pas une couleur
/// qu'Avalonia sache lire.
/// </summary>
public sealed class CouleurDuJeu : IMultiValueConverter
{
    public static readonly CouleurDuJeu Pinceau = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var couleur = values.Count > 0 ? values[0] as string : null;
        var charte = values.Count > 1 ? values[1] as Charte : null;
        return Conv.ToBrush((charte ?? Charte.Defaut).Couleur(couleur));
    }
}

/// <summary>La police d'un bloc, référence au jeu résolue : pour le champ de saisie posé sur la page.</summary>
public sealed class PoliceDuJeu : IMultiValueConverter
{
    public static readonly PoliceDuJeu Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var nom = values.Count > 0 ? values[0] as string : null;
        var charte = values.Count > 1 ? values[1] as Charte : null;
        if (string.IsNullOrWhiteSpace(nom)) return FontFamily.Default;

        // La collection des documents, comme le texte composé : la saisie ne doit pas
        // montrer l'italique en gras quand l'affichage le montre penché (poste Q13).
        return Views.PolicesDuDocument.Famille((charte ?? Charte.Defaut).Police(nom));
    }
}

/// <summary>
/// Une liaison qui ne renvoie jamais null à sa source. Une liste déroulante sans
/// sélection rend null, et ce null n'est jamais un choix de l'utilisateur : il ne peut
/// en faire qu'en choisissant une entrée. C'est le second garde-fou du poste Q20.
/// </summary>
public sealed class JamaisNul : IValueConverter
{
    public static readonly JamaisNul Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value ?? BindingOperations.DoNothing;
}

/// <summary>
/// Ce que la liste des polices affiche d'une entrée : son nom, ou, pour les deux
/// premières entrées, l'emplacement du jeu et la police qu'il désigne aujourd'hui —
/// « Titre du jeu — DejaVu Serif ». Le libellé suit le jeu ; la valeur choisie, elle,
/// reste la référence.
/// </summary>
public sealed class LibelleDePolice : IMultiValueConverter
{
    public static readonly LibelleDePolice Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var valeur = values.Count > 0 ? values[0] as string : null;
        var jeu = values.Count > 1 ? values[1] as JeuDePolices : null;
        var machine = values.Count > 2 ? values[2] as IReadOnlyList<string> : null;

        if (!ReferenceDuJeu.TryPolice(valeur, out var emplacement))
        {
            // Une police que la machine n'a pas reste proposée — c'est celle du bloc —, et
            // le libellé dit qu'elle manque (poste Q20).
            return valeur is not null && machine is not null && !machine.Contains(valeur)
                ? $"{valeur} (absente)"
                : valeur ?? "";
        }

        var nom = emplacement == EmplacementDePolice.Heading ? "Titre du jeu" : "Corps du jeu";
        return jeu is null ? nom : $"{nom} — {jeu.Police(emplacement)}";
    }
}
