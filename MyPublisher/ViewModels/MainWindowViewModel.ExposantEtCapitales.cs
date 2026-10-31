using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Exposant, indice et capitales sur les caractères visés (poste N24 ; fiche 23, b14) : ce que la
/// <see cref="MiseEnFormeVisee"/> de R7 ajoute pour eux — la sélection du texte ou le mot du curseur
/// en saisie, tous les caractères du bloc hors saisie —, par les mêmes écritures, en fragments.
/// </summary>
public sealed partial class MiseEnFormeVisee
{
    /// <summary>
    /// L'exposant et l'indice d'Office, tels que l'import les écrit (<c>PubImporter</c>, poste N24) :
    /// libmspub rend ceux de Publisher par « ±50% 67% » — la ligne de base décalée de la moitié du
    /// corps du caractère, le corps ramené à 67 %.
    /// </summary>
    public const double DecalageDOffice = 0.5;

    /// <summary>Le corps d'un exposant ou d'un indice, en fraction du corps du caractère.</summary>
    public const double CorpsDOffice = 0.67;

    /// <summary>Tous les caractères visés sont en exposant ; nul s'ils le sont en partie.</summary>
    public bool? Exposant => Decale(+1);

    /// <summary>Tous les caractères visés sont en indice ; nul s'ils le sont en partie.</summary>
    public bool? Indice => Decale(-1);

    /// <summary>Les capitales des caractères visés ; nulles si elles sont mixtes.</summary>
    public Capitales? Capitales => Lire().Capitales;

    private bool? Decale(int sens)
    {
        if (Bloc is not { } bloc || Caracteres is not { } plage) return null;

        bool? commun = null;
        foreach (var morceau in Morceaux(bloc, plage))
        {
            var decale = Math.Sign(morceau.Decalage) == sens;
            if (commun is { } c && c != decale) return null;
            commun = decale;
        }

        // Une plage vide se lit au style de la frappe, comme le gras (N2).
        return commun ?? Math.Sign(Lire().BaselineShift ?? 0) == sens;
    }

    /// <summary>
    /// <c>Ctrl+Maj+=</c> et <c>Ctrl+=</c> : si tous les caractères visés sont déjà en exposant
    /// (<paramref name="sens"/> positif) ou en indice (négatif), les rendre à la ligne de base, le
    /// corps divisé de ce qu'il avait perdu ; sinon, les y mettre tous — ceux qui ne l'étaient pas
    /// prennent 67 % de leur corps, chacun depuis le sien, ceux qui l'étaient en sens contraire
    /// gardent le leur. Le décalage va aux fragments seuls, même hors saisie : il n'est pas une
    /// propriété du bloc (R7).
    /// </summary>
    internal bool BasculerLeDecalage(int sens) => Ecrire((bloc, plage) =>
    {
        var texte = bloc.Text ?? "";
        var morceaux = Morceaux(bloc, plage);
        if (morceaux.Count == 0) return false;

        var retirer = morceaux.TrueForAll(m => Math.Sign(m.Decalage) == sens);
        var change = false;
        foreach (var (debut, fin, corps, decalage) in morceaux)
        {
            StyleDeLaPlage style;
            if (retirer)
            {
                // Le corps d'avant, au centième de point : 6,7 pt ramènent 10 pt, et non
                // 10,000000000000002 — qui ne se confondrait plus avec le corps du bloc.
                style = new StyleDeLaPlage { BaselineShift = 0, FontSize = Math.Round(corps / CorpsDOffice, 2) };
            }
            else if (Math.Sign(decalage) == sens)
            {
                continue;
            }
            else
            {
                style = new StyleDeLaPlage
                {
                    BaselineShift = sens * DecalageDOffice,
                    FontSize = decalage == 0 ? corps * CorpsDOffice : corps,
                };
            }

            change |= MiseEnForme.Poser(texte, bloc.Spans, MiseEnForme.Commun(bloc), new Plage(debut, fin - debut), style);
        }

        return change;
    });

    /// <summary>
    /// <i>Majuscules</i> et <i>Petites majuscules</i> (<c>Ctrl+Maj+K</c>) : si tous les caractères
    /// visés portent déjà <paramref name="sorte"/>, les rendre au texte tel qu'il est tapé ; sinon,
    /// la leur donner. Hors saisie, ou sur une sélection de tout le texte, le bloc la prend, et ses
    /// fragments qui en disaient une autre la perdent.
    /// </summary>
    internal bool BasculerLesCapitales(Capitales sorte) => Ecrire((bloc, plage) =>
    {
        var pose = new StyleDeLaPlage
        {
            Capitales = Lire().Capitales == sorte ? Models.Capitales.Aucune : sorte,
        };
        return TousLesCaracteres(bloc, plage)
            ? PoserSurLeBloc(bloc, pose)
            : MiseEnForme.Poser(bloc.Text ?? "", bloc.Spans, MiseEnForme.Commun(bloc), plage, pose);
    });

    /// <summary>
    /// Les morceaux de la plage d'un même corps et d'un même décalage : début, fin, corps et
    /// décalage, dans l'ordre du texte, lus sur le découpage de la composition
    /// (<see cref="StyleEffectif.Decouper"/>). Une plage vide n'en a pas.
    /// </summary>
    private static List<(int Debut, int Fin, double Corps, double Decalage)> Morceaux(TextFrame bloc, Plage plage)
    {
        var morceaux = new List<(int Debut, int Fin, double Corps, double Decalage)>();
        var finDeLaPlage = plage.Start + Math.Max(0, plage.Length);
        var position = 0;
        foreach (var (morceau, style) in StyleEffectif.Decouper(bloc.Text ?? "", bloc.Spans, MiseEnForme.Commun(bloc)))
        {
            var debut = Math.Max(position, plage.Start);
            var fin = Math.Min(position + morceau.Length, finDeLaPlage);
            position += morceau.Length;
            if (debut >= fin) continue;

            if (morceaux.Count > 0 && morceaux[^1].Fin == debut && morceaux[^1].Corps == style.FontSize &&
                morceaux[^1].Decalage == style.BaselineShift)
            {
                morceaux[^1] = morceaux[^1] with { Fin = fin };
            }
            else
            {
                morceaux.Add((debut, fin, style.FontSize, style.BaselineShift));
            }
        }

        return morceaux;
    }
}

/// <summary>
/// Ce que le poste N24 ajoute au modèle de vue — l'exposant, l'indice et les capitales, au clavier
/// de Publisher en français (fiche 24) et au menu Texte. Un fichier à lui, comme la règle 10 de la
/// phase I le veut.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// <c>Ctrl+Maj+=</c>, <i>Texte ▸ Exposant et majuscules ▸ Exposant</i> : met les caractères visés
    /// en exposant, ou les rend à la ligne de base s'ils y sont tous, en un pas.
    /// </summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void BasculerLExposant() => BasculerLeDecalage(+1, "Exposant");

    /// <summary><c>Ctrl+=</c>, <i>Indice</i> : de même, sous la ligne de base (règle 12 : le zoom avant y renonce).</summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void BasculerLIndice() => BasculerLeDecalage(-1, "Indice");

    /// <summary><c>Ctrl+Maj+K</c>, <i>Petites majuscules</i> : les petites capitales, ou le texte tel qu'il est tapé.</summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void BasculerLesPetitesCapitales() =>
        BasculerLesCapitales(Capitales.PetitesCapitales, "Petites majuscules");

    /// <summary><i>Majuscules</i> : tout en capitales, ou le texte tel qu'il est tapé. Pas de touche : la fiche 24 n'en relève pas.</summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void BasculerLesCapitales() => BasculerLesCapitales(Capitales.Capitales, "Majuscules");

    private void BasculerLeDecalage(int sens, string nom) =>
        MettreEnFormeLeTexte(visee => visee.BasculerLeDecalage(sens), _ =>
            (sens > 0 ? MiseEnFormeVisee.Exposant : MiseEnFormeVisee.Indice) == true ? nom : $"{nom} retiré");

    private void BasculerLesCapitales(Capitales sorte, string nom) =>
        MettreEnFormeLeTexte(visee => visee.BasculerLesCapitales(sorte), _ =>
            MiseEnFormeVisee.Capitales == sorte ? nom : $"{nom} retirées");

    /// <summary>La disponibilité des quatre commandes, qui suit la sélection : un bloc de texte seul.</summary>
    private void NotifierExposantEtCapitales()
    {
        BasculerLExposantCommand.NotifyCanExecuteChanged();
        BasculerLIndiceCommand.NotifyCanExecuteChanged();
        BasculerLesPetitesCapitalesCommand.NotifyCanExecuteChanged();
        BasculerLesCapitalesCommand.NotifyCanExecuteChanged();
    }
}
