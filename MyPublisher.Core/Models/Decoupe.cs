using System;
using System.Collections.Generic;

namespace MyPublisher.Models;

/// <summary>
/// Où un bloc qui en porte d'autres — un groupe, un tableau — cesse de les dessiner.
///
/// L'écran découpait déjà les blocs d'un groupe à son bord : le thème d'Avalonia pose
/// <c>ClipToBounds</c> sur la liste qui les porte. L'export, lui, ne découpait rien, si
/// bien qu'un bloc plus large que son groupe s'arrêtait au bord du groupe à l'écran et
/// courait jusqu'au bout dans le PDF (relevé en C9, poste Q5). Le PLAN tranche dans le
/// sens de A2 : <b>l'export découpe comme l'écran</b>. Les deux lisent désormais la zone
/// ici, au lieu que l'écran la tienne d'un réglage de thème.
///
/// La zone n'est pas exactement le cadre du groupe. Un filet est centré sur le bord de
/// son bloc (<see cref="Frame.StrokeThickness"/>) : il en sort d'une demi-épaisseur. Or
/// grouper ajuste le groupe aux cadres de ses blocs, filets non compris — tout bloc
/// filé qui touche le bord du groupe y perdrait donc la moitié extérieure de son filet,
/// et ce sont les blocs filés en bord de bande que portent les fiches ATR. La découpe
/// s'élargit donc, de chaque côté, du plus large demi-filet qu'elle porte. Un bloc sans
/// filet qui dépasse s'arrête alors à cette distance du bord, et non au bord lui-même :
/// c'est le prix de la règle, nul quand aucun bloc du groupe n'est filé.
/// </summary>
public static class Decoupe
{
    /// <summary>
    /// La zone de découpe, en coordonnées relatives au porteur (son coin haut-gauche est
    /// l'origine), pour un porteur de la taille donnée et les blocs qu'il porte.
    /// </summary>
    public static Extent DesBlocs(double largeur, double hauteur, IEnumerable<Frame> blocs)
    {
        var marge = Marge(blocs);
        return new Extent(-marge, -marge, largeur + marge, hauteur + marge);
    }

    /// <summary>
    /// Le plus large demi-filet des blocs donnés et de tout ce qu'ils portent : jusqu'où,
    /// au plus, l'un d'eux dessine hors de son cadre.
    /// </summary>
    public static double Marge(IEnumerable<Frame> blocs)
    {
        var marge = 0.0;
        foreach (var bloc in blocs)
        {
            marge = Math.Max(marge, DemiFilet(bloc));
            marge = Math.Max(marge, Marge(Portes(bloc)));
        }

        return marge;
    }

    /// <summary>
    /// Ce dont le filet d'un bloc sort de son cadre : la moitié de son épaisseur, s'il
    /// est tracé. Un filet absent, transparent ou d'épaisseur nulle n'en sort pas — c'est
    /// la règle même de l'export, qui ne pose alors aucun crayon.
    /// </summary>
    public static double DemiFilet(Frame bloc)
    {
        if (bloc.StrokeThickness <= 0) return 0;
        if (!HexColor.TryParse(bloc.Stroke, out var alpha, out _, out _, out _) || alpha == 0) return 0;
        return bloc.StrokeThickness / 2;
    }

    /// <summary>Les blocs qu'un bloc porte : ceux d'un groupe, les cases d'un tableau.</summary>
    public static IEnumerable<Frame> Portes(Frame bloc) => bloc switch
    {
        GroupFrame groupe => groupe.Children,
        TableFrame tableau => tableau.CellFrames,
        _ => [],
    };
}
