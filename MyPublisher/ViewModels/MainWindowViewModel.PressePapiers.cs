using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MyPublisher.Models;
using MyPublisher.Services;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le poste N8 ajoute au modèle de vue : couper, copier et coller des blocs. Un
/// fichier à lui, comme la règle 10 de la phase I le veut.
///
/// Le modèle de vue ne parle pas au presse-papiers, qui est à la fenêtre et asynchrone : il
/// rend ce qu'il faut y mettre (<see cref="ContenuDuPressePapiers"/>) et reçoit ce qu'on en
/// a lu. La fenêtre décide aussi qui reçoit le geste : en saisie, ou dans n'importe quel champ
/// de texte, c'est le champ, et le modèle de vue n'en sait rien.
///
/// <b>Le format.</b> <see cref="FormatDesBlocs"/> porte le JSON d'un document d'une seule page
/// où sont posés des clones de la sélection, dans l'ordre de la page — l'écriture même du
/// fichier, si bien qu'un collage relit ce qu'un fichier relit, version du format comprise.
/// Il est écrit sans dossier : les chemins d'images y sont ceux du modèle, absolus depuis A3,
/// et c'est l'enregistrement du document d'arrivée qui les rapporte à son dossier à lui. Les
/// références au jeu (<c>scheme:accent1</c>) y restent des références : elles se résolvent
/// dans le document d'arrivée, comme tout ce qui s'y dessine.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// L'identifiant du format propre des blocs. Avalonia 11.3 n'admet dans un identifiant de
    /// format d'application que des lettres ASCII, des chiffres, le point et le tiret (N1).
    /// </summary>
    public const string FormatDesBlocs = "mypublisher-blocs";

    /// <summary>Le décalage d'un collage qui tomberait sur son original, le même que celui de <c>Ctrl+D</c>.</summary>
    private const double DecalageDuCollage = 12;

    /// <summary>
    /// Ce qui part au presse-papiers : les blocs, au format propre, et leur texte brut, pour
    /// les autres logiciels.
    /// </summary>
    public sealed record ContenuDuPressePapiers(string Blocs, string Texte);

    /// <summary>
    /// Copie la sélection : des clones, dans l'ordre de la page, posés sur la page unique d'un
    /// document porteur. Le document ne change pas ; rien n'entre à l'historique.
    /// </summary>
    /// <returns>Le contenu à poser au presse-papiers ; null sans sélection.</returns>
    public ContenuDuPressePapiers? CopierLaSelection()
    {
        if (CurrentPage is null || Selection.Count == 0) return null;

        var blocs = SelectionInPageOrder();
        var page = new Page { Name = "Presse-papiers" };
        foreach (var bloc in blocs) page.Frames.Add(bloc.Clone());

        var porteur = new Document { Title = "Presse-papiers" };
        porteur.Pages.Add(page);

        Status = blocs.Count == 1 ? "Bloc copié" : $"{blocs.Count} blocs copiés";
        return new ContenuDuPressePapiers(
            DocumentSerializer.ToJson(porteur),
            string.Join("\n\n", blocs.Select(TexteBrut).Where(texte => texte.Length > 0)));
    }

    /// <summary>Coupe la sélection : la copie, puis la suppression, en un seul pas.</summary>
    public ContenuDuPressePapiers? CouperLaSelection()
    {
        if (CopierLaSelection() is not { } contenu) return null;

        var combien = Selection.Count;
        DeleteSelected();
        Status = combien == 1 ? "Bloc coupé" : $"{combien} blocs coupés";
        return contenu;
    }

    /// <summary>
    /// Colle des blocs lus au format propre. Un collage d'une version plus récente du format
    /// est refusé, comme l'ouverture d'un tel fichier, et le document ne change pas.
    /// </summary>
    /// <returns>Vrai si des blocs ont été posés.</returns>
    public bool CollerDesBlocs(string json)
    {
        Document source;
        try
        {
            source = DocumentSerializer.FromJson(json);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException
                                       or NotSupportedException or FormatException)
        {
            Status = $"Collage impossible : {ex.Message}";
            return false;
        }

        return Coller(source);
    }

    /// <summary>
    /// Pose sur la page courante les blocs de la première page de <paramref name="source"/>,
    /// en un pas, et les sélectionne. Au même endroit, ou 12 pt plus loin si l'original y est
    /// encore, comme <c>Ctrl+D</c> — et 12 pt plus loin encore à chaque collage qui suit,
    /// tant que la place est prise.
    ///
    /// Ce sont des clones qui entrent dans la page, jamais les blocs de la source : celui qui
    /// la passe la garde, et peut la coller de nouveau. Deux collages du même contenu donnent
    /// deux jeux d'instances, qui divergent ensuite.
    /// </summary>
    public bool Coller(Document source)
    {
        if (CurrentPage is null) return false;

        var blocs = source.Pages.FirstOrDefault()?.Frames.ToList() ?? [];
        if (blocs.Count == 0) return false;

        var decalage = DecalagePourColler(CurrentPage, blocs);
        var poses = new List<Frame>();

        BeginEdit();
        foreach (var bloc in blocs)
        {
            var pose = bloc.Clone();
            pose.X += decalage;
            pose.Y += decalage;
            CurrentPage.Frames.Add(pose);
            poses.Add(pose);
        }

        SetSelection(poses);
        CommitEdit();

        Status = poses.Count == 1 ? "Bloc collé" : $"{poses.Count} blocs collés";
        return true;
    }

    /// <summary>
    /// Colle un texte venu d'ailleurs, hors saisie : il devient un bloc de texte neuf, posé
    /// comme le pose le menu Insertion, en un pas. Les fins de ligne de Windows et de l'ancien
    /// Mac deviennent celles du modèle.
    /// </summary>
    /// <returns>Vrai si le bloc a été posé ; un texte vide ou blanc ne pose rien.</returns>
    public bool CollerDuTexte(string? texte)
    {
        if (CurrentPage is null || string.IsNullOrWhiteSpace(texte)) return false;

        var bloc = (TextFrame)BuildFrame(EditorTool.Text);
        bloc.Text = texte.Replace("\r\n", "\n").Replace('\r', '\n');
        (bloc.X, bloc.Y) = NextInsertPoint(bloc.Width, bloc.Height);
        Insert(bloc);
        Status = "Texte collé dans un bloc neuf";
        return true;
    }

    /// <summary>
    /// Le décalage du collage : nul si aucun bloc collé ne retombe exactement sur un bloc de la
    /// page de même sorte et de même taille — l'original, ou un collage précédent —, sinon le
    /// premier multiple de 12 pt où la place est libre.
    /// </summary>
    private static double DecalagePourColler(Page page, IReadOnlyList<Frame> blocs)
    {
        for (var rang = 0; rang < 100; rang++)
        {
            var decalage = rang * DecalageDuCollage;
            if (!blocs.Any(bloc => page.Frames.Any(present => Recouvre(present, bloc, decalage)))) return decalage;
        }

        return 0;
    }

    private static bool Recouvre(Frame present, Frame bloc, double decalage) =>
        present.GetType() == bloc.GetType()
        && Math.Abs(present.X - (bloc.X + decalage)) < 0.01
        && Math.Abs(present.Y - (bloc.Y + decalage)) < 0.01
        && Math.Abs(present.Width - bloc.Width) < 0.01
        && Math.Abs(present.Height - bloc.Height) < 0.01;

    /// <summary>
    /// Le texte d'un bloc tel qu'un autre logiciel le reçoit : celui qu'on lit sur la page,
    /// champs résolus ; les cases d'un tableau séparées par des tabulations, ligne par ligne ;
    /// les blocs d'un groupe à la suite. Une forme ou une image n'en ont pas.
    /// </summary>
    private string TexteBrut(Frame bloc) => bloc switch
    {
        TextFrame texte => FieldResolver.Resolve(texte.Field, texte.Text, FieldContext),
        TableFrame tableau => string.Join("\n", Enumerable.Range(0, tableau.Rows.Count).Select(ligne =>
            string.Join("\t", tableau.Cells.Where(c => c.Row == ligne).OrderBy(c => c.Column).Select(c => c.Text)))),
        GroupFrame groupe => string.Join("\n\n", groupe.Children.Select(TexteBrut).Where(t => t.Length > 0)),
        _ => "",
    };
}
