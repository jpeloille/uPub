using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using MyPublisher.Models;
using MyPublisher.ViewModels;

namespace MyPublisher.Converters;

/// <summary>
/// Le souligné du bloc est-il la sorte que désigne une entrée du menu déroulant du bouton S
/// (poste I3) ? C'est la coche de l'entrée. Un souligné que le menu ne propose pas — un
/// pointillé importé d'un <c>.pub</c> — n'en coche aucune : le menu ne prétend pas le
/// connaître. Dans un seul sens : l'entrée choisie joue sa commande, qui fait son pas
/// d'annulation, et la coche suit le bloc.
/// </summary>
public sealed class SorteDuSouligne : IValueConverter
{
    public static readonly SorteDuSouligne Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Souligne souligne && parameter is SorteDeSouligne sorte &&
        souligne == MainWindowViewModel.SouligneDe(sorte);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
