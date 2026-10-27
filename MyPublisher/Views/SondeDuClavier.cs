using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MyPublisher.Views;

/// <summary>
/// La sonde du clavier (poste N10) : ce qu'une frappe livre vraiment à l'application, sur
/// la machine où elle tourne. Les liaisons d'Avalonia comparent la touche (<c>Key</c>) et
/// les modificateurs exacts, jamais le caractère : sur un AZERTY, « &lt; » et « &gt; »
/// partagent une touche, « ] » et « [ » demandent AltGr, et ce que X11 en fait sous Ctrl
/// ne se devine pas. La sonde l'écrit, frappe par frappe, pour que les tests rejouent ce
/// qui a été relevé et non ce qu'on suppose.
///
/// Elle ne s'éveille que si la variable <see cref="Variable"/> nomme un fichier :
/// <c>MYPUBLISHER_SONDE_CLAVIER=/chemin/releve.tsv</c>. Chaque ligne y dit l'événement, la
/// touche, sa place, le caractère produit (et ses points de code), les modificateurs, si
/// l'application l'a prise, et l'élément qui avait le focus. Elle écoute toutes les
/// fenêtres — le catalogue et les boîtes comprises —, et n'en traite aucune.
/// </summary>
public static class SondeDuClavier
{
    public const string Variable = "MYPUBLISHER_SONDE_CLAVIER";

    private static StreamWriter? _sortie;
    private static int _rang;

    /// <summary>Branche la sonde si la variable la demande ; sinon, ne fait rien.</summary>
    public static void BrancherSiDemandee()
    {
        var chemin = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(chemin) || _sortie is not null) return;

        Brancher(chemin);
    }

    /// <summary>Branche la sonde vers <paramref name="chemin"/>, qu'elle remplace.</summary>
    public static void Brancher(string chemin)
    {
        _sortie = new StreamWriter(chemin, append: false, new UTF8Encoding(false)) { AutoFlush = true };
        _rang = 0;

        _sortie.WriteLine($"# Sonde du clavier de MyPublisher (N10), {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _sortie.WriteLine($"# Avalonia {typeof(Application).Assembly.GetName().Version}" +
                          $" ; XDG_SESSION_TYPE={Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")}" +
                          $" ; XDG_CURRENT_DESKTOP={Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")}" +
                          $" ; DISPLAY={Environment.GetEnvironmentVariable("DISPLAY")}" +
                          $" ; WAYLAND_DISPLAY={Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")}");
        _sortie.WriteLine("rang\tévénement\tKey\tPhysicalKey\tKeySymbol\tpoints\tKeyModifiers\tprise\tfocus");

        // Un seul gestionnaire par événement : ceux d'une classe sont appelés une fois la route
        // parcourue, si bien que « prise » dit si l'application a fait quelque chose de la frappe.
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>((_, e) => Ecrire("KeyDown", e), RoutingStrategies.Tunnel,
            handledEventsToo: true);
        InputElement.TextInputEvent.AddClassHandler<TopLevel>((fenetre, e) => EcrireLeTexte(fenetre, e),
            RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private static void Ecrire(string evenement, KeyEventArgs e)
    {
        if (_sortie is null) return;

        // Le rang suit les pressions ; le texte qu'une pression produit garde le sien.
        _rang++;

        _sortie.WriteLine(string.Join('\t',
            _rang.ToString(CultureInfo.InvariantCulture),
            evenement,
            e.Key,
            e.PhysicalKey,
            Montrer(e.KeySymbol),
            Points(e.KeySymbol),
            e.KeyModifiers,
            e.Handled ? "oui" : "non",
            Focus(e.Source)));
    }

    private static void EcrireLeTexte(TopLevel fenetre, TextInputEventArgs e)
    {
        _sortie?.WriteLine(string.Join('\t',
            _rang.ToString(CultureInfo.InvariantCulture),
            "TextInput",
            "",
            "",
            Montrer(e.Text),
            Points(e.Text),
            "",
            e.Handled ? "oui" : "non",
            Focus(fenetre.FocusManager?.GetFocusedElement())));
    }

    /// <summary>Le caractère entre guillemets, ses contrôles rendus visibles ; « - » s'il n'y en a pas.</summary>
    private static string Montrer(string? texte) => texte is null
        ? "-"
        : "«" + string.Concat(texte.Select(c => char.IsControl(c) ? $"\\x{(int)c:X2}" : c.ToString())) + "»";

    /// <summary>Les points de code du caractère, qui lèvent l'ambiguïté qu'un œil laisse.</summary>
    private static string Points(string? texte) => texte is null
        ? "-"
        : string.Join(' ', texte.EnumerateRunes().Select(r => $"U+{r.Value:X4}"));

    private static string Focus(object? element) => element switch
    {
        null => "-",
        Control { Name: { Length: > 0 } nom } c => $"{c.GetType().Name}#{nom}",
        _ => element.GetType().Name,
    };
}
