using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace MyPublisher.Views;

/// <summary>
/// Les gestes des menus écrits en français, comme les bulles les écrivent depuis N10 :
/// « Ctrl+Maj+G », « Suppr », « Ctrl++ » (poste N13, repris le 29 octobre 2026).
///
/// La voie que le plan prévoyait, <c>AppBuilder.With(new KeyGestureFormatInfo(…))</c>, ne tient
/// pas sous Avalonia 11.3.9 : les options s'appliquent avant la plate-forme, qui lie la sienne
/// par-dessus. Le propriétaire a choisi de réécrire le texte affiché : à l'application du gabarit
/// de chaque <see cref="MenuItem"/>, le <c>PART_InputGestureText</c> du thème Fluent reçoit le
/// geste en valeur locale, qui l'emporte sur la liaison du gabarit (un style ne l'emporterait
/// pas), et le suit quand <see cref="MenuItem.InputGesture"/> change. Le thème n'est pas recopié.
///
/// Branché une fois pour le processus, par <c>App.Initialize</c> : l'application, l'hôte des
/// tests d'interface et le banc montent tous <c>App</c>.
/// </summary>
public static class GestesEnFrancais
{
    /// <summary>Le nom que le thème Fluent d'Avalonia 11.3.9 donne au texte du geste d'une entrée.</summary>
    public const string PartieDuGeste = "PART_InputGestureText";

    private static bool _branche;

    /// <summary>Le texte du geste de chaque entrée dont le gabarit est appliqué.</summary>
    private static readonly ConditionalWeakTable<MenuItem, TextBlock> Textes = new();

    /// <summary>Branche les deux gestionnaires de classe ; un second appel ne fait rien.</summary>
    public static void Brancher()
    {
        if (_branche) return;
        _branche = true;

        TemplatedControl.TemplateAppliedEvent.AddClassHandler<MenuItem>((entree, e) =>
        {
            if (e.NameScope.Find<TextBlock>(PartieDuGeste) is not { } texte) return;
            Textes.AddOrUpdate(entree, texte);
            Ecrire(entree);
        });
        MenuItem.InputGestureProperty.Changed.AddClassHandler<MenuItem>((entree, _) => Ecrire(entree));
    }

    private static void Ecrire(MenuItem entree)
    {
        if (Textes.TryGetValue(entree, out var texte)) texte.Text = entree.InputGesture is { } geste ? Texte(geste) : null;
    }

    /// <summary>Les touches qui ont un nom français, ou un signe, à l'écran comme au README.</summary>
    private static readonly Dictionary<Key, string> Noms = new()
    {
        [Key.Delete] = "Suppr",
        [Key.Back] = "Retour arrière",
        [Key.Insert] = "Inser",
        [Key.Escape] = "Échap",
        [Key.Space] = "Espace",
        [Key.Enter] = "Entrée",
        [Key.Tab] = "Tab",
        [Key.Home] = "Origine",
        [Key.End] = "Fin",
        [Key.PageUp] = "Pg.Préc",
        [Key.PageDown] = "Pg.Suiv",
        [Key.Left] = "Gauche",
        [Key.Right] = "Droite",
        [Key.Up] = "Haut",
        [Key.Down] = "Bas",
        // Le signe moins des bulles : « Zoom arrière (Ctrl+−) ». La touche « = » se dit « = »,
        // comme la fiche 24 l'écrit : « Ctrl+= » est l'indice, « Ctrl+Maj+= » l'exposant (poste
        // N24) ; « Ctrl++ » faisait taper Maj+= sur l'AZERTY, donc l'exposant.
        [Key.OemPlus] = "=",
        [Key.Add] = "+",
        [Key.OemMinus] = "−",
        [Key.Subtract] = "−",
        [Key.OemComma] = ",",
        [Key.OemPeriod] = ".",
        [Key.OemOpenBrackets] = "[",
        [Key.OemCloseBrackets] = "]",
    };

    /// <summary>
    /// Un geste tel que les bulles l'écrivent : les modificateurs dans l'ordre Ctrl, Maj, Alt,
    /// puis la touche, « Maj » pour Shift, les chiffres sans leur « D ».
    /// </summary>
    public static string Texte(KeyGesture geste)
    {
        var parties = new List<string>();
        if (geste.KeyModifiers.HasFlag(KeyModifiers.Control)) parties.Add("Ctrl");
        if (geste.KeyModifiers.HasFlag(KeyModifiers.Shift)) parties.Add("Maj");
        if (geste.KeyModifiers.HasFlag(KeyModifiers.Alt)) parties.Add("Alt");
        if (geste.KeyModifiers.HasFlag(KeyModifiers.Meta)) parties.Add("Super");
        parties.Add(NomDeLaTouche(geste.Key));
        return string.Join("+", parties);
    }

    private static string NomDeLaTouche(Key touche) => touche switch
    {
        _ when Noms.TryGetValue(touche, out var nom) => nom,
        >= Key.D0 and <= Key.D9 => ((char)('0' + (touche - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (touche - Key.NumPad0))).ToString(),
        _ => touche.ToString(),
    };
}
