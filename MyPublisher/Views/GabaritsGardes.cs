using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace MyPublisher.Views;

/// <summary>
/// Les réglages d'un bloc, tirés d'un gabarit par sorte de bloc, construits une fois et
/// gardés (poste Q23). Le dock en a trois — « Position et taille », les sections propres
/// au type, « Fond et contour » —, la barre contextuelle un, <c>ReglagesDuBloc</c>.
///
/// **Pourquoi pas un <c>ContentControl</c>.** C'est ce qu'ils étaient, liés au bloc seul,
/// et c'était la cause de Q23. Un <c>ContentControl</c> ne garde son contenu que d'un bloc
/// au suivant tiré du même gabarit. Une sélection vide lui donne <c>null</c> : il jette ses
/// champs, et la sélection suivante les reconstruit tous, dans la pression du pointeur
/// même — sur un bloc de texte, mille visuels, deux douzaines de champs numériques, une
/// vingtaine de champs de texte, leurs styles et leurs gabarits de contrôle. Mesuré le
/// 25 octobre 2026 sur ATA 21 : 47 à 55 ms de la pression sur un bloc non sélectionné, au
/// processus chaud, contre moins d'une milliseconde sur un bloc déjà pris ; 110 à 135 ms
/// au processus froid, dont les tests sont faits. Passer d'un bloc de texte à un cadre
/// les jetait de même.
///
/// Ici, chaque gabarit est construit à la première sélection d'un bloc de sa sorte, puis
/// gardé dans l'arbre, masqué tant qu'aucun bloc de sa sorte n'est seul sélectionné. Une
/// sélection ne fait plus que montrer le bon et lui donner le bloc : ses liaisons se
/// relisent, rien ne se construit. Le gabarit retenu est le premier qui accepte le bloc,
/// dans l'ordre où ils sont écrits, comme un <c>ContentControl</c> le choisirait.
///
/// **Le gabarit masqué garde son dernier bloc.** Il ne prend pas d'entrée, et lui ôter son
/// bloc coûterait à la désélection ce qu'on vient d'ôter à la sélection. Un champ qui avait
/// le focus le rend quand on le masque — Avalonia le reprend à un contrôle qui cesse d'être
/// visible —, comme il le rendait en quittant l'arbre : la frappe suivante ne va pas
/// s'écrire dans un bloc qu'on ne regarde plus (<c>PressionTests</c> le garde).
/// </summary>
public sealed class GabaritsGardes : Panel
{
    public static readonly StyledProperty<object?> BlocProperty =
        AvaloniaProperty.Register<GabaritsGardes, object?>(nameof(Bloc));

    /// <summary>Le bloc dont on montre les réglages, ou rien.</summary>
    public object? Bloc
    {
        get => GetValue(BlocProperty);
        set => SetValue(BlocProperty, value);
    }

    /// <summary>Chaque gabarit déjà construit, et ce qu'il a donné.</summary>
    private readonly Dictionary<IDataTemplate, Control> _construits = [];

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BlocProperty) Montrer(change.NewValue);
    }

    private void Montrer(object? bloc)
    {
        var gabarit = bloc is null ? null : DataTemplates.FirstOrDefault(g => g.Match(bloc));
        Control? montre = null;
        if (gabarit is not null && !_construits.TryGetValue(gabarit, out montre))
        {
            montre = gabarit.Build(bloc);
            if (montre is not null)
            {
                montre.DataContext = bloc;
                _construits[gabarit] = montre;
                Children.Add(montre);
            }
        }

        foreach (var reglages in _construits.Values)
        {
            if (!ReferenceEquals(reglages, montre)) reglages.IsVisible = false;
        }

        if (montre is null) return;
        montre.DataContext = bloc;
        montre.IsVisible = true;
    }
}
