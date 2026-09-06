using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyPublisher.Models;

/// <summary>
/// Un jeu de coordonnées — celles d'une personne, d'une société, d'une antenne. Saisi
/// une fois, réinjecté dans tous les blocs qui portent le champ correspondant.
///
/// Ces données ne sont pas dans le document : elles suivent l'utilisateur et non le
/// fichier. Un modèle envoyé à un collègue affiche donc les coordonnées de ce
/// collègue, ce qui est précisément l'intérêt.
/// </summary>
public partial class BusinessInfo : ObservableObject
{
    /// <summary>Nom du jeu, tel qu'on le choisit dans la liste ; plusieurs coexistent.</summary>
    [ObservableProperty] private string _setName = "Par défaut";

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _organisation = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _tagline = "";

    /// <summary>Chemin du logo, résolu par le champ correspondant sur un bloc image.</summary>
    [ObservableProperty] private string _logoPath = "";

    [JsonIgnore] public string DisplayName => string.IsNullOrWhiteSpace(SetName) ? "Sans nom" : SetName;

    public BusinessInfo Clone() => new()
    {
        SetName = SetName, Name = Name, Title = Title, Organisation = Organisation,
        Address = Address, Phone = Phone, Tagline = Tagline, LogoPath = LogoPath
    };
}
