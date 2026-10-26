using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

/// <summary>
/// Ne rien perdre en fermant, retrouver ses documents — poste N11 (fiche 23, d19, e19, f25).
///
/// Nouveau, Ouvrir, Fermer (<c>Ctrl+W</c>, <c>Ctrl+F4</c>), Quitter et la fermeture de la
/// fenêtre (<see cref="OnClosing"/>) jetaient les modifications sans un mot. Ils passent tous par
/// <see cref="LaisserPartirLeDocument"/>, qui referme d'abord la saisie ouverte — sa frappe
/// compte —, puis demande, si le document est modifié : Enregistrer, Ne pas enregistrer,
/// Annuler. Fermer rend ensuite le catalogue des modèles, comme Publisher rend son écran
/// d'accueil. Les six derniers documents ouverts ou enregistrés sont au menu Fichier,
/// numérotés.
///
/// La question n'est posée que si <see cref="AvantDePerdre"/> est branchée, et seul
/// <c>App.axaml.cs</c> la branche : les tests ferment des fenêtres modifiées (par exemple
/// <c>MasquerEcranTests</c>), et une fenêtre qui se brancherait elle-même les bloquerait sur un
/// dialogue que personne ne regarde.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// La question d'avant la perte : rend la réponse pour le document nommé. Nulle, rien n'est
    /// demandé et tout se poursuit, comme avant le poste — c'est l'état d'une fenêtre de test.
    /// </summary>
    public Func<string, Task<ReponseAvantDePerdre>>? AvantDePerdre { get; set; }

    /// <summary>La question a eu sa réponse et l'on ferme : la fermeture qui suit ne la repose pas.</summary>
    private bool _fermetureAccordee;

    /// <summary>La question est à l'écran : un second geste ne la pose pas une seconde fois.</summary>
    private bool _questionPosee;

    /// <summary>Les entrées des documents récents, posées au menu Fichier avant leur séparateur.</summary>
    private readonly List<MenuItem> _entreesRecentes = [];

    // ------------------------------------------------------------- la question

    /// <summary>
    /// Ce que fait chaque geste qui va remplacer ou fermer le document : la saisie ouverte sur
    /// la page et le champ du dock ou de la barre sont refermés, pour que leur frappe compte ;
    /// puis, si le document est modifié et que la question est branchée, elle est posée.
    /// Enregistrer écrit le document — par « Enregistrer sous » s'il n'a pas de chemin — et ne
    /// laisse poursuivre que si l'écriture a réussi ; Ne pas enregistrer laisse poursuivre ;
    /// Annuler, ou le dialogue refermé, laisse tout tel quel. Publique pour qu'un test la joue
    /// sans le sélecteur de fichiers, qui ne s'ouvre pas sans affichage.
    /// </summary>
    /// <returns>Vrai si le geste peut poursuivre et perdre le document en cours.</returns>
    public async Task<bool> LaisserPartirLeDocument()
    {
        if (Vm is not { } vm) return true;

        PageView.EndTextEdit();
        ClosePropertyEdit();

        if (!vm.IsDirty || AvantDePerdre is not { } question) return true;
        if (_questionPosee) return false;

        ReponseAvantDePerdre reponse;
        _questionPosee = true;
        try
        {
            reponse = await question(vm.FilePath is null ? "Sans titre" : Path.GetFileName(vm.FilePath));
        }
        finally
        {
            _questionPosee = false;
        }

        switch (reponse)
        {
            case ReponseAvantDePerdre.NePasEnregistrer:
                return true;
            case ReponseAvantDePerdre.Enregistrer:
                if (vm.FilePath is null) await SaveAsAsync(vm);
                else TrySave(vm, vm.FilePath);
                // Une écriture refusée, ou « Enregistrer sous » abandonné, laisse le document
                // modifié : on ne le perd pas pour autant.
                if (!vm.IsDirty) return true;
                break;
        }

        RouvrirLeChamp();
        return false;
    }

    /// <summary>
    /// Le champ du dock ou de la barre qui a toujours le focus, une fois la question refermée
    /// sans suite, rouvre la transaction que <see cref="LaisserPartirLeDocument"/> lui avait
    /// refermée : sans quoi la suite de sa saisie n'entrerait dans aucun pas (Q24).
    /// </summary>
    private void RouvrirLeChamp()
    {
        if (_propertyEditOpen || _panneauDuChamp is not { IsKeyboardFocusWithin: true }) return;

        _propertyEditOpen = true;
        Vm?.BeginEdit();
    }

    /// <summary>
    /// La fenêtre qui se ferme — Quitter, <c>Alt+F4</c>, la case de la barre de titre — sur un
    /// document modifié : la fermeture est suspendue le temps de la question, puis reprise si la
    /// réponse le permet. Sans question branchée, elle se fait comme avant.
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_fermetureAccordee && AvantDePerdre is not null && Vm is { } vm)
        {
            PageView.EndTextEdit();
            ClosePropertyEdit();
            if (_questionPosee || vm.IsDirty)
            {
                e.Cancel = true;
                if (!_questionPosee) _ = FermerApresLaQuestion();
            }
        }

        base.OnClosing(e);
    }

    private async Task FermerApresLaQuestion()
    {
        if (!await LaisserPartirLeDocument()) return;

        _fermetureAccordee = true;
        Close();
    }

    // ------------------------------------------------------------------ Fermer

    private async void OnFermetureDemandee(object? sender, EventArgs e) => await FermerLeDocument();

    /// <summary>
    /// Fermer : la question s'il le faut, puis le document fermé et le catalogue des modèles. Le
    /// catalogue abandonné laisse la page blanche sans titre où Fermer a laissé la fenêtre.
    /// </summary>
    public async Task FermerLeDocument()
    {
        if (Vm is not { } vm) return;
        if (!await LaisserPartirLeDocument()) return;

        vm.Fermer();
        await ChooseTemplateAsync();
    }

    // ---------------------------------------------------------------- Ouvrir

    /// <summary>
    /// Ouvre un document choisi — au sélecteur de fichiers ou parmi les récents —, après la
    /// question d'avant la perte. Publique pour qu'un test la joue sans le sélecteur.
    /// </summary>
    /// <returns>Vrai si le document est ouvert.</returns>
    public async Task<bool> OuvrirLeDocument(string chemin)
    {
        if (Vm is not { } vm) return false;
        if (!await LaisserPartirLeDocument()) return false;

        try
        {
            vm.LoadFrom(chemin);
            return true;
        }
        catch (Exception ex)
        {
            vm.Status = $"Ouverture impossible : {ex.Message}";
            return false;
        }
    }

    // ------------------------------------------------------- les documents récents

    /// <summary>
    /// Pose au menu Fichier les documents récents du modèle de vue, numérotés de 1 à 6, avant
    /// leur séparateur, et ne montre celui-ci qu'avec eux. Le libellé est le nom du fichier, la
    /// bulle son chemin ; un « _ » du nom est doublé pour ne pas devenir une touche d'accès.
    /// </summary>
    private void ConstruireLesRecents()
    {
        foreach (var entree in _entreesRecentes) MenuFichier.Items.Remove(entree);
        _entreesRecentes.Clear();

        var recents = Vm?.DocumentsRecents ?? [];
        var rang = MenuFichier.Items.IndexOf(SeparateurDesRecents);
        for (var i = 0; i < recents.Count; i++)
        {
            var chemin = recents[i];
            var entree = new MenuItem
            {
                Name = $"MenuRecent{i + 1}",
                Header = $"_{i + 1} {Path.GetFileName(chemin).Replace("_", "__")}",
                Tag = chemin,
            };
            ToolTip.SetTip(entree, chemin);
            entree.Click += OnDocumentRecent;
            MenuFichier.Items.Insert(rang + i, entree);
            _entreesRecentes.Add(entree);
        }

        SeparateurDesRecents.IsVisible = recents.Count > 0;
    }

    /// <summary>
    /// Un document récent choisi au menu. Disparu du disque, il quitte la liste et rien d'autre
    /// ne change : on ne demande pas d'abandonner le document en cours pour un fichier qui
    /// n'est plus là.
    /// </summary>
    private async void OnDocumentRecent(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string chemin } || Vm is not { } vm) return;

        if (!File.Exists(chemin))
        {
            vm.OublierLeDocument(chemin);
            vm.Status = $"Introuvable : {chemin}";
            return;
        }

        await OuvrirLeDocument(chemin);
    }
}
