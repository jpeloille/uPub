using System;
using Avalonia.Controls.Primitives;

namespace MyPublisher.Views;

/// <summary>
/// Un bouton de la barre contextuelle qui MONTRE un état du bloc sans en tenir un à lui :
/// le gras, l'italique, le souligné, l'alignement du texte (poste I3). Il se coche par sa
/// liaison au bloc, et son clic joue sa commande — rien d'autre.
///
/// **Pourquoi pas un simple bouton à bascule.** Un <c>ToggleButton</c> inverse sa coche au
/// clic avant de jouer sa commande. Lié au bloc, cela tient tant que la commande inverse
/// aussi la propriété ; mais un clic sur « Centrer » quand le texte est déjà centré décoche
/// le bouton, sa commande écrit la valeur déjà là, le bloc ne prévient de rien, et le
/// bouton reste décoché sur un texte centré. Celui-ci ne s'inverse jamais lui-même : la
/// coche est celle du bloc, relue à chaque changement du bloc, et nulle part ailleurs.
///
/// **Pourquoi une commande et pas la liaison à double sens du dock.** La commande fait son
/// propre pas d'annulation (<c>UnGesteUnPas</c>), où que soit le focus : deux clics sur G
/// sont deux pas, et un clic pendant la saisie du corps ne se fond pas dans la transaction
/// du champ (règle 2 de la phase). Le bouton ne prend pas le focus pour autant
/// (<c>Focusable="False"</c> dans la barre) : un bouton de barre d'outils laisse le
/// curseur où il était — dans le champ, ou dans le texte qu'on tape sur la page.
///
/// Il garde l'apparence d'un <c>ToggleButton</c> : les styles « ToggleButton.ib » de
/// l'apparence, son état coché plein d'accent compris, le prennent pour l'un d'eux.
/// </summary>
public class BoutonDEtat : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(ToggleButton);

    protected override void Toggle()
    {
    }
}
