using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le menu contextuel du bloc (poste A12, fiche 11 ; fiche 23, c3, c16, c25, e1, e2, f16)
/// ajoute au modèle de vue. Un fichier à lui, comme la règle 10 de la phase I le veut.
///
/// Le menu reprend les commandes des menus, chaque entrée liée à la même commande que son
/// pendant : rien de leur logique n'est écrit ici. Ne s'y ajoutent que les trois entrées qui
/// n'ont pas de pendant aux menus :
///
///   - <b>Format du bloc…</b>, le chemin de Publisher vers son dialogue <i>Format</i> (décision 5) :
///     la section du dock propre à la sorte du bloc, par la <see cref="DockDemande"/> de N18 ;
///     le double-clic sur une image ou une forme y mène aussi ;
///   - <b>Supprimer le texte</b> (<i>Delete Text</i>, p. 34) : tout le texte du bloc, en un pas,
///     comme une sélection entière effacée à la frappe ;
///   - <b>Convertir en texte</b>, sur un bloc de coordonnées (la balise de Publisher, p. 54,
///     <i>Convert to Plain Text</i>) : le champ cède la place à ce qu'il affiche, en un pas.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// <i>Format du bloc…</i> : la section du dock propre à la sorte du bloc seul — le cadre d'un
    /// texte, l'image, le trait, le groupe, le tableau ; pour un rectangle ou une ellipse, qui
    /// n'ont pas d'autre section que la leur, le fond et le contour. Rien n'entre dans le
    /// document : aucun pas.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSingleSelection))]
    private void FormatDuBloc()
    {
        switch (SingleSelected)
        {
            case TextFrame:
                AllerAuDock(SectionDuBloc("ContenuCadreDeTexte"), () => SectionCadreOuverte = true);
                break;
            case ImageFrame:
                AllerAuDock(SectionDuBloc("ContenuImage"), () => SectionImageOuverte = true);
                break;
            case ShapeFrame { IsLine: true }:
                AllerAuDock(SectionDuBloc("ContenuTrait"), () => SectionTraitOuverte = true);
                break;
            case ShapeFrame:
                AllerAuDock(SectionDuBloc("ContenuFondEtContour"), () => SectionFondOuverte = true);
                break;
            case GroupFrame:
                AllerAuDock(SectionDuBloc("ContenuGroupe"), () => SectionGroupeOuverte = true);
                break;
            case TableFrame:
                AllerAuDock(SectionDuBloc("ContenuTableau"), () => SectionTableauOuverte = true);
                break;
        }
    }

    /// <summary>Une section de l'onglet Propriétés, le focus à son premier champ.</summary>
    private static DockDemande SectionDuBloc(string section) => new(DestinationDuReglage.Proprietes, section, null);

    /// <summary>Un bloc de texte seul, sans champ, qui a du texte : celui qu'on peut vider.</summary>
    public bool PeutSupprimerLeTexte => SingleSelected is TextFrame { HasField: false, Text.Length: > 0 };

    /// <summary>
    /// <i>Supprimer le texte</i> : le bloc garde sa place, son cadre et son style ; son texte
    /// part, en un pas, par <see cref="TextFrame.Saisie"/>, comme une sélection entière effacée à
    /// la frappe — les fragments et les écarts de paragraphe se recalent sur le texte vide.
    /// Grisée sur un bloc vide, et sur un bloc à champ, dont le texte est caché.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PeutSupprimerLeTexte))]
    private void SupprimerLeTexte()
    {
        if (SingleSelected is not TextFrame { HasField: false } bloc || string.IsNullOrEmpty(bloc.Text)) return;

        UnGesteUnPas(() => bloc.Saisie = "");
        Status = $"« {bloc.Name} » : texte supprimé";
    }

    /// <summary>Le bloc seul est un bloc de coordonnées : un bloc de texte qui porte l'un des six champs de texte du jeu.</summary>
    public bool EstUnBlocDeCoordonnees =>
        SingleSelected is TextFrame { Field: >= FieldKind.ContactName and <= FieldKind.ContactTagline };

    /// <summary>
    /// <i>Convertir en texte</i> : le champ du bloc de coordonnées cède la place au texte qu'il
    /// affiche, en un pas ; le texte caché sous le champ est remplacé. Le bloc ne suit plus le jeu
    /// de coordonnées, et se saisit comme un autre.
    /// </summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeCoordonnees))]
    private void ConvertirEnTexte()
    {
        if (!EstUnBlocDeCoordonnees || SingleSelected is not TextFrame bloc) return;

        var affiche = FieldResolver.Resolve(bloc.Field, bloc.Text, FieldContext);
        UnGesteUnPas(() =>
        {
            bloc.Field = FieldKind.None;
            bloc.Spans.Clear();
            bloc.Paragraphs.Clear();
            bloc.Saisie = affiche;
        });
        Status = $"« {bloc.Name} » : converti en texte";
    }

    /// <summary>
    /// Le menu s'ouvre : la planche demande la disponibilité de ses entrées neuves, qui dépend de
    /// la sélection et aussi du texte et du champ du bloc, que la sélection ne notifie pas.
    /// </summary>
    public void NotifierLeMenuDuBloc()
    {
        FormatDuBlocCommand.NotifyCanExecuteChanged();
        SupprimerLeTexteCommand.NotifyCanExecuteChanged();
        ConvertirEnTexteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PeutSupprimerLeTexte));
        OnPropertyChanged(nameof(EstUnBlocDeCoordonnees));
    }
}
