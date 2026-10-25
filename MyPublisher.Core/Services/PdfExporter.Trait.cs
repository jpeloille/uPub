using System.Linq;
using MyPublisher.Models;
using PdfSharp.Drawing;

namespace MyPublisher.Services;

/// <summary>
/// Le trait à l'export — poste A10. Un fichier à lui : I8 et I9 touchent
/// <c>PdfExporter.cs</c> en même temps, et ce qui est neuf vit à part.
/// </summary>
public static partial class PdfExporter
{
    /// <summary>
    /// Un trait se trace d'un seul coup de crayon — <c>m … l S</c> dans le flux —, et non
    /// en rectangle sans hauteur comme l'import le posait avant ce poste : le contour d'un
    /// tel rectangle passait deux fois sur le trait, et ses bouts carrés en dépassaient
    /// d'une demi-épaisseur. Les pointes sont des triangles pleins, à la couleur du trait.
    ///
    /// Le tracé est celui du modèle (<see cref="ShapeFrame.Dessin"/>), que l'écran lit
    /// aussi ; la rotation est déjà posée par l'appelant, comme pour tout bloc. Le crayon
    /// est celui du contour de tout bloc : même couleur résolue dans le jeu, même
    /// épaisseur, mêmes tirets, et rien de tracé sans contour.
    /// </summary>
    private static void DessinerLeTrait(XGraphics gfx, ShapeFrame trait, Charte charte)
    {
        var crayon = MakePen(trait, charte);
        if (crayon is null) return;

        var dessin = trait.Dessin();
        gfx.DrawLine(crayon, trait.X + dessin.X1, trait.Y + dessin.Y1, trait.X + dessin.X2, trait.Y + dessin.Y2);

        var pinceau = new XSolidBrush(crayon.Color);
        foreach (var pointe in new[] { dessin.PointeAuDebut, dessin.PointeALaFin })
        {
            if (pointe is null) continue;
            gfx.DrawPolygon(pinceau, pointe.Select(p => new XPoint(trait.X + p.X, trait.Y + p.Y)).ToArray(),
                XFillMode.Winding);
        }
    }
}
