using Avalonia;
using MyPublisher.Models;

namespace MyPublisher.Views;

/// <summary>
/// La charte du document affiché — son jeu de couleurs et son jeu de polices — posée une
/// fois sur la page et héritée par tout ce qui s'y dessine (poste C5b).
///
/// Pourquoi une propriété héritée plutôt qu'une liaison par contrôle. Un bloc ne sait
/// pas dans quel document il est, et c'est très bien ainsi ; ce sont donc les contrôles
/// qui le dessinent qui doivent connaître le jeu. Les fonds, les textes composés et les
/// marqueurs de débordement descendent tous de la page — à travers les groupes et les
/// cases des tableaux — et une valeur héritée les atteint tous sans qu'un gabarit puisse
/// l'oublier. Remplacer la charte du document — changer de jeu — la change pour tous
/// ces contrôles d'un coup, et chacun se redessine.
///
/// Hors d'une page, la propriété vaut null : c'est la charte par défaut qui s'applique.
/// </summary>
public static class JeuAffiche
{
    public static readonly AttachedProperty<Charte?> CharteProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, Charte?>("Charte", typeof(JeuAffiche), inherits: true);

    public static Charte? GetCharte(AvaloniaObject cible) => cible.GetValue(CharteProperty);

    public static void SetCharte(AvaloniaObject cible, Charte? valeur) => cible.SetValue(CharteProperty, valeur);

    /// <summary>La charte qui vaut pour un contrôle : la sienne, sinon celle d'un document neuf.</summary>
    public static Charte De(AvaloniaObject cible) => GetCharte(cible) ?? Charte.Defaut;
}
