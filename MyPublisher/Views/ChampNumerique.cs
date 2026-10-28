using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using MyPublisher.Converters;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Un champ numérique du dock et de la barre contextuelle, qui se tient comme un champ
/// d'Office (poste N14 ; fiche 23, f2, f5, f7, f8, f10, f11).
///
/// **Rien ne part à la frappe.** Le <c>NumericUpDown</c> d'Avalonia pousse sa valeur à chaque
/// frappe lisible (<c>OnTextChanged</c> → <c>SyncTextAndValueProperties</c>) : taper « 210 »
/// dans la largeur de la page l'écrivait trois fois, et passait par 2 mm puis 21 mm. Ici, la
/// valeur part quand on la valide — Entrée, Tab, la sortie du champ, ↑ et ↓, la molette — et
/// elle part une fois. La frappe ne fait que du texte.
///
/// **Un refus se dit.** Le champ d'Avalonia avalait une saisie illisible, ou hors de ses
/// bornes, et gardait le texte refusé sous une valeur qui n'avait pas bougé. Ici, le champ
/// remontre la valeur d'avant, et la barre d'état dit pourquoi, bornes comprises
/// (<see cref="MainWindowViewModel.Status"/>, le canal où le zoom tapé de N4 dit déjà ses
/// refus). Une saisie hors des bornes n'est pas ramenée à la borne : on ne sait pas ce qu'on
/// voulait dire en tapant 5 000 mm.
///
/// **Échap renonce** à ce qui n'est pas validé, comme Office, puis rend la main à la planche
/// (règle 13 de la phase N) : le champ perd le focus, et les lettres seules redeviennent des
/// outils. C'est l'inverse de ce que I3 avait choisi pour la barre, où Échap validait ; au
/// dock, il ne faisait rien.
///
/// La lecture est celle du convertisseur du champ (<see cref="Unite"/>), qui accepte d'autres
/// unités que la sienne : « 2 cm » dans X donne 20,0 mm. L'affichage, lui, reste en
/// millimètres (décision du 24 octobre 2026).
///
/// Il garde l'apparence d'un <c>NumericUpDown</c> : les styles « NumericUpDown.champ » de
/// l'apparence le prennent pour l'un d'eux, et les tests le trouvent comme tel.
/// </summary>
public class ChampNumerique : NumericUpDown
{
    protected override Type StyleKeyOverride => typeof(NumericUpDown);

    /// <summary>La zone de texte du gabarit : le champ d'Avalonia ne la montre pas.</summary>
    private TextBox? _zone;

    public ChampNumerique()
    {
        // Au passage descendant, avant la zone de texte : elle prend ↑ et ↓ pour son curseur dès
        // qu'il a où aller — tout le texte pris, après un refus, ↑ le ramenait au début au lieu
        // de faire un pas. C'est le geste du champ du zoom (N4).
        AddHandler(KeyDownEvent, SurFleche, RoutingStrategies.Tunnel);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _zone = e.NameScope.Find<TextBox>("PART_TextBox");
    }

    /// <summary>
    /// La frappe ne pousse rien. Le texte d'une valeur reçue — de la liaison, d'un pas — est
    /// écrit par le champ d'Avalonia lui-même, pendant sa synchronisation, où sa lecture du
    /// texte ne jouerait de toute façon pas : il n'y a rien à lui rendre ici.
    /// </summary>
    protected override void OnTextChanged(string? oldValue, string? newValue)
    {
    }

    /// <summary>
    /// Du texte tapé attend d'être validé : le champ ne montre pas ce que sa valeur écrirait.
    /// </summary>
    public bool EnAttente => IsInitialized && Text is not null && !string.Equals(Text, Ecrire(Value), StringComparison.Ordinal);

    /// <summary>
    /// Valide ce qui est tapé : la valeur part, une fois, et le champ l'écrit à sa façon. Une
    /// saisie illisible ou hors des bornes est refusée — le champ remontre la valeur d'avant, et
    /// la barre d'état le dit. Rend faux sur un refus ; vrai sinon, rien d'attendu compris.
    /// </summary>
    public bool Valider()
    {
        if (!EnAttente) return true;

        var saisie = (Text ?? "").Trim();
        if (!Lire(saisie, out var valeur))
        {
            Refuser($"« {saisie} » ne se lit pas{Aide()} : la valeur reste {Ecrire(Value)}.");
            return false;
        }

        if (valeur < Minimum || valeur > Maximum)
        {
            Refuser($"« {saisie} » est refusé, {Bornes()} : la valeur reste {Ecrire(Value)}.");
            return false;
        }

        // La même valeur ne repart pas : celle du modèle reste exacte, sans repasser par la
        // mémoire des convertisseurs de Q14.
        if (valeur == Value) Remontrer();
        else SetCurrentValue(ValueProperty, valeur);
        return true;
    }

    /// <summary>Ce qui est tapé est oublié : le champ remontre sa valeur.</summary>
    public void Abandonner()
    {
        if (EnAttente) Remontrer();
    }

    private void Refuser(string message)
    {
        Remontrer();
        if (TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel vm) vm.Status = message;
    }

    private void Remontrer()
    {
        var texte = Ecrire(Value);
        SetCurrentValue(TextProperty, texte);
        if (_zone is not null) _zone.Text = texte;
    }

    // ------------------------------------------------------------ les gestes qui valident

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            // Pris, Entrée continue son chemin : la barre contextuelle y rend la planche
            // (BarreContextuelle.SurTouche), le dock garde le champ. Refusé, le champ garde le
            // focus, tout pris, pour qu'on retape.
            case Key.Enter:
                if (!Valider())
                {
                    _zone?.SelectAll();
                    e.Handled = true;
                }
                return;
            case Key.Escape when e.KeyModifiers == KeyModifiers.None:
                Abandonner();
                TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
                e.Handled = true;
                return;
        }

        base.OnKeyDown(e);
    }

    private void SurFleche(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Up or Key.Down)) return;
        if (!AllowSpin || IsReadOnly) return;

        e.Handled = true;
        OnSpin(new SpinEventArgs(Spinner.SpinEvent, e.Key == Key.Up ? SpinDirection.Increase : SpinDirection.Decrease));
    }

    /// <summary>Tab, Maj+Tab, un clic ailleurs : la sortie du champ valide.</summary>
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        Valider();
        base.OnLostFocus(e);
    }

    /// <summary>
    /// ↑, ↓ et la molette font un pas à partir de ce qui est tapé, validé d'abord ; un refus
    /// arrête le geste, et le dit.
    /// </summary>
    protected override void OnSpin(SpinEventArgs e)
    {
        if (!Valider()) return;
        base.OnSpin(e);
    }

    // ------------------------------------------------------------ lire et écrire

    /// <summary>Le texte d'une valeur, tel que le champ d'Avalonia l'écrit (<c>ConvertValueToText</c>).</summary>
    private string? Ecrire(decimal? valeur)
    {
        if (TextConverter is { } convertisseur)
        {
            return convertisseur.ConvertBack(valeur, typeof(string), null, CultureInfo.CurrentCulture)?.ToString();
        }

        return FormatString.Contains("{0", StringComparison.Ordinal)
            ? string.Format(NumberFormat, FormatString, valeur)
            : valeur?.ToString(FormatString, NumberFormat);
    }

    /// <summary>Un texte lu comme le champ d'Avalonia le lit : par son convertisseur, sinon en nombre.</summary>
    private bool Lire(string saisie, out decimal valeur)
    {
        valeur = 0;
        if (saisie.Length == 0) return false;

        try
        {
            if (TextConverter is { } convertisseur)
            {
                if (convertisseur.Convert(saisie, typeof(decimal?), null, CultureInfo.CurrentCulture) is not decimal lu) return false;
                valeur = lu;
                return true;
            }

            return decimal.TryParse(saisie, ParsingNumberStyle, NumberFormat, out valeur);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidCastException)
        {
            return false;
        }
    }

    private string Bornes()
    {
        var minimum = Minimum != decimal.MinValue;
        var maximum = Maximum != decimal.MaxValue;
        return (minimum, maximum) switch
        {
            (true, true) => $"de {Ecrire(Minimum)} à {Ecrire(Maximum)}",
            (true, false) => $"au moins {Ecrire(Minimum)}",
            (false, true) => $"au plus {Ecrire(Maximum)}",
            _ => "hors des bornes",
        };
    }

    private string Aide() => TextConverter is Unite { EstUneLongueur: true } ? $" ({Unite.UnitesLues})" : "";
}
