using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que l'objet trait (poste A10) ajoute au modèle de vue. Un fichier à lui, comme la
/// règle 10 de la phase le veut : I3, I8 et I9 partent en même temps et touchent la
/// fenêtre, le modèle et le dock.
///
/// Le trait se définit par ses deux extrémités (<see cref="ShapeFrame.PoserLesExtremites"/>) :
/// l'outil Ligne les reçoit de la planche, « Trait » au menu Insertion le pose à sa longueur
/// par défaut, et le dock les donne à saisir — à la place de X, Y, L et H, qui décrivent un
/// cadre qu'un trait n'a pas. Le champ H, borné à la taille minimale d'un bloc, aurait
/// d'ailleurs donné deux millimètres de haut au premier trait qu'on sélectionne.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// La longueur d'un trait posé d'un simple clic ou par le menu : deux pouces, à plat. Un
    /// tracé plus court que <see cref="MinDrawnSize"/> compte pour un clic, comme le cadre
    /// trop petit d'un autre outil.
    /// </summary>
    public const double LongueurDuTraitParDefaut = 144;

    /// <summary>
    /// Un trait neuf : sans fond, un contour d'un point dans la couleur principale du jeu —
    /// celle du texte —, par référence, comme les autres blocs neufs (C5b). Sans pointe :
    /// c'est le dock qui en fait une flèche.
    /// </summary>
    private ShapeFrame NouveauTrait()
    {
        var trait = new ShapeFrame
        {
            Name = "Trait", Shape = ShapeKind.Line, Fill = null,
            Stroke = ReferenceDuJeu.Couleur(EmplacementDeCouleur.Main), StrokeThickness = 1,
        };
        trait.PoserLesExtremites(0, 0, LongueurDuTraitParDefaut, 0);
        return trait;
    }

    /// <summary>« Trait », au menu Insertion : posé à plat dans la zone de composition.</summary>
    [RelayCommand]
    private void AddLine() => InsertWithTool(EditorTool.Line);

    /// <summary>
    /// Le trait que l'outil Ligne vient de tracer, de (<paramref name="x1"/>,
    /// <paramref name="y1"/>) à (<paramref name="x2"/>, <paramref name="y2"/>), en points de
    /// page ; la planche a déjà contraint l'angle sous <c>Maj</c>. Un tracé trop court pose
    /// le trait à sa longueur par défaut, à plat depuis le point de départ. L'outil se
    /// désarme, comme les autres, et le trait posé est sélectionné, en un pas d'annulation.
    /// </summary>
    public Frame? TracerUnTrait(double x1, double y1, double x2, double y2)
    {
        if (ActiveTool != EditorTool.Line || CurrentPage is null) return null;

        var trait = NouveauTrait();
        if (Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1)) < MinDrawnSize)
        {
            (x2, y2) = (x1 + LongueurDuTraitParDefaut, y1);
        }

        trait.PoserLesExtremites(x1, y1, x2, y2);

        // Désarmé avant l'insertion, dont le message d'état est le dernier mot.
        ActiveTool = EditorTool.Select;
        Insert(trait);
        return trait;
    }

    // ------------------------------------------------------------------ le dock

    /// <summary>Le trait seul sélectionné, s'il y en a un.</summary>
    public ShapeFrame? TraitSeul => SingleSelected is ShapeFrame { IsLine: true } trait ? trait : null;

    /// <summary>
    /// L'abscisse du début du trait, en points de page, telle que le dock la saisit. Les
    /// quatre champs passent par ici et non par le bloc, faute de propriété du bloc à lier :
    /// un trait ne range pas ses extrémités, il les calcule. Écrire l'une repose le trait
    /// entier, l'autre extrémité restant où elle est ; le pas d'annulation est celui du
    /// champ, que le dock ouvre au focus et ferme en le quittant (règle 2).
    /// </summary>
    public double DebutDuTraitX
    {
        get => TraitSeul?.Debut.X ?? 0;
        set => Reposer(debut: (value, null), fin: (null, null));
    }

    public double DebutDuTraitY
    {
        get => TraitSeul?.Debut.Y ?? 0;
        set => Reposer(debut: (null, value), fin: (null, null));
    }

    public double FinDuTraitX
    {
        get => TraitSeul?.Fin.X ?? 0;
        set => Reposer(debut: (null, null), fin: (value, null));
    }

    public double FinDuTraitY
    {
        get => TraitSeul?.Fin.Y ?? 0;
        set => Reposer(debut: (null, null), fin: (null, value));
    }

    private void Reposer((double? X, double? Y) debut, (double? X, double? Y) fin)
    {
        if (TraitSeul is not { } trait) return;

        var (x1, y1) = trait.Debut;
        var (x2, y2) = trait.Fin;

        // Une valeur inchangée ne repose rien : le champ renvoie ce qu'il vient d'afficher
        // quand on le quitte, et reposer le trait le ferait glisser d'un ulp à chaque fois.
        var nouveau = (debut.X ?? x1, debut.Y ?? y1, fin.X ?? x2, fin.Y ?? y2);
        if (nouveau == (x1, y1, x2, y2)) return;

        trait.PoserLesExtremites(nouveau.Item1, nouveau.Item2, nouveau.Item3, nouveau.Item4);
    }

    /// <summary>Les quatre champs suivent le trait, pendant un geste comme après.</summary>
    private void NotifierLesExtremites()
    {
        OnPropertyChanged(nameof(TraitSeul));
        OnPropertyChanged(nameof(DebutDuTraitX));
        OnPropertyChanged(nameof(DebutDuTraitY));
        OnPropertyChanged(nameof(FinDuTraitX));
        OnPropertyChanged(nameof(FinDuTraitY));
    }

    /// <summary>La section « Trait » du dock, ouverte ou repliée le temps de la session.</summary>
    [ObservableProperty] private bool _sectionTraitOuverte = true;
}
