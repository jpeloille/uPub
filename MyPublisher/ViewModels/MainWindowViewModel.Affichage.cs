using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le poste N4 — l'affichage au clavier — ajoute au modèle de vue. Un fichier à lui,
/// comme la règle 10 de la phase I le veut.
///
/// Les gestes de Publisher en français (fiche 23, d10, d11, d13, e14, f24) :
///   - <c>F9</c> bascule entre l'affichage courant et la taille réelle, et le second
///     <c>F9</c> rend l'affichage d'avant ;
///   - <c>Ctrl+Maj+L</c> tient la page entière — la commande de I6, que la fenêtre lie ;
///   - <c>Ctrl+Maj+O</c> montre ou masque les limites et repères — la case « Repères »
///     d'aujourd'hui, <see cref="ShowGuides"/>, dont rien ne change —, <c>Ctrl+F7</c> les
///     seules lignes de base ;
///   - le zoom se tape dans la barre principale, ou se choisit dans la liste qu'elle
///     déroule (<c>Views/ChampDuZoom.axaml</c>).
///
/// Ce qui se décide sans la planche est ici : quel zoom, et quel affichage <c>F9</c>
/// rendra. Le calcul — quel point garde sa place dans la zone visible, quel zoom tient la
/// sélection — est à la planche (<c>PageCanvas.Navigation.cs</c>), qui seule connaît sa
/// taille à l'écran et son défilement : elle répond aux demandes qu'on lui fait ici.
/// Rien de tout cela n'entre dans le document ni dans l'historique, et rien ne zoome de
/// soi-même (règle 5) : chaque demande part d'une commande ou du champ.
/// </summary>
public partial class MainWindowViewModel
{
    // ------------------------------------------------------- limites et repères

    /// <summary>
    /// Repères de lignes de base (<c>Ctrl+F7</c>) : une bascule de la session, à part de
    /// « Limites et repères » (<see cref="ShowGuides"/>). Celle-ci masque tout ce qui ne
    /// s'imprime pas, lignes de base comprises, comme la case « Repères » le faisait ;
    /// celle-là ne masque que les lignes de base. Aucune des deux n'entre dans le document.
    /// </summary>
    [ObservableProperty] private bool _showBaselines = true;

    /// <summary><c>Ctrl+Maj+O</c> : la case « Limites et repères » du menu Affichage.</summary>
    [RelayCommand]
    private void BasculerLesReperes() => ShowGuides = !ShowGuides;

    /// <summary><c>Ctrl+F7</c> : la case « Repères de lignes de base » du menu Affichage.</summary>
    [RelayCommand]
    private void BasculerLesLignesDeBase() => ShowBaselines = !ShowBaselines;

    // ------------------------------------------------------- le zoom demandé

    /// <summary>La taille réelle : 100 %, un point de page pour un point à l'écran.</summary>
    public const double TailleReelle = 1.0;

    public const string PageEntiereLibelle = "Page entière";
    public const string ObjetsSelectionnesLibelle = "Objets sélectionnés";

    /// <summary>
    /// Ce que le champ du zoom déroule, dans l'ordre de Publisher : les deux commandes, puis
    /// les pourcentages de 400 à 10. Chaque entrée se lit comme une saisie : choisir
    /// « 150 % », c'est le taper.
    /// </summary>
    public IReadOnlyList<string> ZoomsProposes { get; } =
    [
        PageEntiereLibelle, ObjetsSelectionnesLibelle,
        "400 %", "300 %", "200 %", "150 %", "100 %", "75 %", "66 %", "50 %", "33 %", "25 %", "10 %",
    ];

    /// <summary>
    /// Une demande faite à la planche : le zoom voulu et, pour le second <c>F9</c>, le
    /// défilement à rendre avec lui. Sans défilement, la planche zoome autour du centre de
    /// la sélection — ou du milieu de la zone visible sans sélection —, qui garde sa place à
    /// l'écran. Elle dit en retour qu'elle a servi la demande, et d'où elle partait.
    /// </summary>
    public sealed class DemandeDeZoom(double zoom, Vector? defilement) : EventArgs
    {
        public double Zoom { get; } = zoom;

        public Vector? Defilement { get; } = defilement;

        /// <summary>La planche a répondu ; sans elle, le zoom se pose seul.</summary>
        public bool Servie { get; set; }

        /// <summary>Le défilement de la planche avant la demande, qu'elle remplit en répondant.</summary>
        public Vector DefilementDAvant { get; set; }
    }

    /// <summary>La planche s'y abonne (<c>PageCanvas.Navigation.cs</c>).</summary>
    public event EventHandler<DemandeDeZoom>? ZoomDemande;

    /// <summary>
    /// « Objets sélectionnés » : la planche tient la sélection dans la zone visible, comme
    /// « Page entière » y tient la page. Elle seule connaît la taille de la zone.
    /// </summary>
    public event EventHandler? ObjetsSelectionnesDemandes;

    private DemandeDeZoom DemanderLeZoom(double zoom, Vector? defilement = null)
    {
        var demande = new DemandeDeZoom(Math.Clamp(zoom, ZoomMinimum, ZoomMaximum), defilement);
        ZoomDemande?.Invoke(this, demande);
        if (!demande.Servie) SetZoom(demande.Zoom);
        return demande;
    }

    // ------------------------------------------------------- F9

    /// <summary>
    /// L'affichage que le prochain <c>F9</c> rendra : le zoom et le défilement d'avant la
    /// taille réelle. Il ne vaut que tant que le zoom reste celui que <c>F9</c> a posé : un
    /// autre geste qui le change — la molette, la loupe, un bouton, le champ, « Page
    /// entière » — l'oublie, et le <c>F9</c> qui suit repart de l'affichage courant.
    /// </summary>
    private (double Zoom, Vector Defilement)? _affichageDAvant;

    /// <summary>Vrai le temps que <c>F9</c> change le zoom : ce changement-là ne fait rien oublier.</summary>
    private bool _basculeEnCours;

    private bool EnTailleReelle => Math.Abs(Zoom - TailleReelle) < 1e-9;

    /// <summary>
    /// <c>F9</c>, comme dans Publisher : hors de la taille réelle, il y passe, autour du
    /// centre de la sélection — ou du milieu de la zone visible —, et retient l'affichage
    /// qu'il quitte ; en taille réelle, il rend cet affichage, zoom et défilement. En taille
    /// réelle sans rien à rendre, il le dit et ne fait rien.
    /// </summary>
    [RelayCommand]
    private void BasculerLaTailleReelle()
    {
        _basculeEnCours = true;
        try
        {
            if (!EnTailleReelle)
            {
                var zoom = Zoom;
                var demande = DemanderLeZoom(TailleReelle);
                _affichageDAvant = (zoom, demande.DefilementDAvant);
                return;
            }

            if (_affichageDAvant is { } avant)
            {
                _affichageDAvant = null;
                DemanderLeZoom(avant.Zoom, avant.Defilement);
                return;
            }

            Status = "Déjà en taille réelle.";
        }
        finally
        {
            _basculeEnCours = false;
        }
    }

    /// <summary>
    /// Un zoom changé par un autre geste que <c>F9</c> fait oublier l'affichage d'avant.
    /// Un écart sous le milliardième n'est pas un geste : le curseur de la barre d'état, lié
    /// dans les deux sens en échelle logarithmique, peut rendre un zoom à un bit près (I6).
    /// </summary>
    partial void OnZoomChanged(double oldValue, double newValue)
    {
        if (!_basculeEnCours && Math.Abs(newValue - oldValue) > 1e-9 * Math.Abs(newValue))
        {
            _affichageDAvant = null;
        }
    }

    // ------------------------------------------------------- le zoom tapé

    private static string Bornes => string.Format(CultureInfo.CurrentCulture, "{0:0} à {1:0} %",
        ZoomMinimum * 100, ZoomMaximum * 100);

    /// <summary>
    /// Ce qu'on tape — ou choisit — dans le champ du zoom de la barre principale : un
    /// pourcentage, avec ou sans « % », à la virgule comme au point, ou l'une des deux
    /// commandes de la liste. Un pourcentage illisible, ou hors des bornes du zoom, est
    /// refusé avec un message à la barre d'état, et le zoom ne bouge pas. Pris, le zoom
    /// change autour du centre de la sélection, comme sous <c>F9</c>. Rend vrai si la saisie
    /// est prise.
    /// </summary>
    public bool SaisirLeZoom(string? texte)
    {
        var saisie = (texte ?? "").Trim();

        if (string.Equals(saisie, PageEntiereLibelle, StringComparison.CurrentCultureIgnoreCase))
        {
            PageEntiereCommand.Execute(null);
            return true;
        }

        if (string.Equals(saisie, ObjetsSelectionnesLibelle, StringComparison.CurrentCultureIgnoreCase))
        {
            return ZoomerSurLaSelection();
        }

        if (!LirePourcentage(saisie, out var pourcent))
        {
            Status = $"« {saisie} » n'est pas un zoom : tapez un pourcentage, de {Bornes}.";
            return false;
        }

        if (pourcent < ZoomMinimum * 100 - 1e-9 || pourcent > ZoomMaximum * 100 + 1e-9)
        {
            Status = $"Le zoom va de {Bornes} : « {saisie} » est refusé.";
            return false;
        }

        DemanderLeZoom(pourcent / 100);
        return true;
    }

    /// <summary>
    /// Un pourcentage écrit comme on l'écrit : « 150 », « 150 % », « 150% », « 66,5 », avec
    /// des blancs — insécables compris — où l'on veut. La virgule et le point valent tous
    /// deux séparateur décimal, quelle que soit la culture, comme dans les champs du dock.
    /// Le champ du zoom s'en sert aussi pour situer un zoom libre dans sa liste.
    /// </summary>
    public static bool LirePourcentage(string saisie, out double pourcent)
    {
        var chiffres = new string(saisie.Where(c => !char.IsWhiteSpace(c) && c != '%').ToArray())
            .Replace(',', '.');
        return double.TryParse(chiffres, NumberStyles.Float, CultureInfo.InvariantCulture, out pourcent)
               && double.IsFinite(pourcent);
    }

    /// <summary>« Objets sélectionnés », au menu Affichage comme dans la liste du zoom.</summary>
    [RelayCommand]
    private void ObjetsSelectionnes() => ZoomerSurLaSelection();

    /// <summary>
    /// Un bloc masqué (I8) ne compte pas : la planche n'en montre rien, et zoomer sur lui
    /// tiendrait du vide dans la zone.
    /// </summary>
    private bool ZoomerSurLaSelection()
    {
        if (!Selection.Any(bloc => bloc.Visible))
        {
            Status = "Aucun bloc n'est sélectionné : « Objets sélectionnés » n'a rien à montrer.";
            return false;
        }

        ObjetsSelectionnesDemandes?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
