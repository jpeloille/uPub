using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;

namespace MyPublisher.Views;

/// <summary>
/// La couche de saisie de la planche, « InputLayer » : transparente, posée sur toute la
/// planche, elle reçoit les gestes du pointeur — prendre, tirer, tracer, poser un repère.
///
/// Elle se laissait autrefois éteindre pendant la saisie d'un texte sur la page, pour que le
/// pointeur atteigne le champ dessous : le clic suivant hors du texte refermait bien la
/// saisie, mais il allait ensuite à la page, et rien n'était pris — il fallait recliquer, et
/// les poignées dessinées autour du bloc en saisie ne se tiraient pas (fiche 23, a8 et a9).
/// Elle reste désormais atteignable partout, sauf là où <see cref="Laisse"/> lui dit de
/// laisser passer le pointeur : sur le texte en saisie, que la planche lui désigne (poste N5).
/// Depuis la voie 2a (poste N21), la planche ne lui fait plus rien laisser : le champ de la
/// saisie est caché, et c'est elle qui prend le clic sur le texte en saisie, lu sur la
/// composition.
///
/// Elle dérive de <see cref="Border"/>, que les tests cherchent sous le nom « InputLayer »
/// (règle 6), et s'habille comme lui.
/// </summary>
public class CoucheDeSaisie : Border, ICustomHitTest
{
    protected override Type StyleKeyOverride => typeof(Border);

    /// <summary>
    /// Le point, dans le repère de la couche, tombe-t-il sur ce qu'elle laisse au contrôle
    /// dessous ? <c>null</c>, elle ne laisse rien.
    /// </summary>
    public Func<Point, bool>? Laisse { get; set; }

    /// <summary>
    /// Le compositeur d'Avalonia 11 passe le point dans le repère de la couche, et non dans
    /// celui de la fenêtre que dit sa documentation : c'est ce que mesure le test d'un clic
    /// au coin du texte en saisie, la planche décalée de la fenêtre par les barres et à un
    /// zoom qui n'est pas 1.
    /// </summary>
    public bool HitTest(Point point) =>
        new Rect(Bounds.Size).Contains(point) && Laisse?.Invoke(point) != true;
}
