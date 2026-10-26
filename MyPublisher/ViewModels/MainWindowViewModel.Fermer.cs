using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Services;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le poste N11 ajoute au modèle de vue (fiche 23, d19, e19, f25) : Fermer, et les six
/// derniers documents. Un fichier à lui, comme la règle 10 de la phase I le veut.
///
/// La question qu'on pose avant de perdre des modifications n'est pas ici : elle est à la
/// fenêtre, qui seule sait montrer un dialogue, et elle n'y est branchée que par
/// <c>App.axaml.cs</c> (<c>MainWindow.AvantDePerdre</c>) — les tests ferment des fenêtres
/// modifiées sans rien demander. Le modèle de vue dit seulement si le document est modifié
/// (<see cref="IsDirty"/>, que Q24 rend vrai), et ferme ce qu'on lui dit de fermer.
/// </summary>
public partial class MainWindowViewModel
{
    /// <summary>
    /// Les réglages de l'utilisateur, hors du document. Un magasin en mémoire si on n'en donne
    /// pas au constructeur : un banc ou un test n'écrit jamais dans la configuration de la
    /// machine.
    /// </summary>
    public Preferences Preferences { get; }

    /// <summary>Les derniers documents ouverts ou enregistrés, le plus récent en tête : le menu Fichier les numérote.</summary>
    public IReadOnlyList<string> DocumentsRecents => Preferences.DocumentsRecents;

    /// <summary>
    /// Fermer, demandé au menu ou au clavier (<c>Ctrl+W</c>, <c>Ctrl+F4</c>) : la fenêtre pose la
    /// question s'il le faut, puis ferme le document et rend le catalogue des modèles, comme
    /// Publisher rend son écran d'accueil.
    /// </summary>
    public event EventHandler? FermetureDemandee;

    [RelayCommand]
    private void FermerLeDocument() => FermetureDemandee?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Ferme le document : il reste une page blanche sans titre, que le catalogue remplacera si
    /// l'on y choisit un modèle. MyPublisher n'a pas d'état sans document ; c'est le plus près de
    /// l'écran d'accueil de Publisher.
    /// </summary>
    public void Fermer()
    {
        NewDocumentCommand.Execute(null);
        Status = "Document fermé";
    }

    /// <summary>Met le document en tête des récents, et l'écrit dans les préférences.</summary>
    private void RetenirLeDocument(string chemin)
    {
        Preferences.RetenirLeDocument(chemin);
        EnregistrerLesPreferences();
    }

    /// <summary>Retire un document des récents — celui qu'on n'a pas pu rouvrir.</summary>
    public void OublierLeDocument(string chemin)
    {
        if (Preferences.OublierLeDocument(chemin)) EnregistrerLesPreferences();
    }

    /// <summary>
    /// Des préférences qui ne s'écrivent pas — un dossier en lecture seule — ne font échouer ni
    /// l'ouverture, ni l'enregistrement du document : les récents ne survivront pas à la
    /// session, rien de plus.
    /// </summary>
    private void EnregistrerLesPreferences()
    {
        try
        {
            Preferences.Enregistrer();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        OnPropertyChanged(nameof(DocumentsRecents));
    }
}
