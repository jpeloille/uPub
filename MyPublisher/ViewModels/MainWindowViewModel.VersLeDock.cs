using System;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que le poste N18 — les menus vers le dock, et les pages, avec A8 et A9 — ajoute au modèle
/// de vue. Un fichier à lui, comme la règle 10 de la phase I le veut.
///
/// **Le chemin vers le dock** (décision 5 de la phase N). Publisher règle un objet par ses
/// dialogues « Format… » ; MyPublisher les a dans le dock, où aucune entrée de menu ne menait.
/// Une entrée « … » ouvre ici la section qu'elle vise et lève une <see cref="DockDemande"/>, que
/// la fenêtre traite : rouvrir le dock s'il est replié, choisir l'onglet, mettre le focus dans le
/// champ, une fois la mise en page faite. Rien n'entre dans le document : aucun pas d'annulation.
/// Aucun dialogue OK/Annuler ne double un champ du dock (règle 1).
///
/// **Les pages** : le dialogue « Insérer des pages » de A8 (<c>Ctrl+Maj+N</c>), qui insère en un
/// pas ; « Dupliquer la page » au clavier (<c>Ctrl+Maj+U</c>) ; la page suivante et la précédente
/// (<c>Ctrl+Pg.Suiv</c>, <c>Ctrl+Pg.Préc</c>) ; « Atteindre la page » (<c>F5</c>, <c>Ctrl+B</c>), qui
/// met le focus sur le numéro de page de la barre d'état, devenu un champ.
///
/// **Le quart de tour** de A9 : +90° et −90°, au menu Disposition et au dock, en un pas.
///
/// **Insertion ▸ Numéro de page, Date, Coordonnées** : un bloc de texte qui porte son champ (C4).
/// </summary>
public partial class MainWindowViewModel
{
    // ---------------------------------------------------------------- le chemin vers le dock

    /// <summary>
    /// Une entrée « … » demande le réglage qu'elle vise. La fenêtre s'abonne ; tant que personne
    /// n'écoute, l'entrée ouvre la section et ne fait rien d'autre.
    /// </summary>
    public event EventHandler<DockDemande>? DockDemandee;

    /// <summary>
    /// Ouvre ce que l'entrée vise et le demande à la fenêtre. Le dock est montré — il le serait
    /// au menu Fenêtre de même —, et la section ouverte avant que la fenêtre ne la cherche : la
    /// section se vise après la mise en page, quand les gabarits gardés ont montré la sienne.
    /// </summary>
    private void AllerAuDock(DockDemande demande, Action ouvrirLaSection)
    {
        if (demande.Destination != DestinationDuReglage.BarreContextuelle) DockVisible = true;
        ouvrirLaSection();
        DockDemandee?.Invoke(this, demande);
    }

    /// <summary>
    /// <i>Disposition ▸ Repères de mise en page…</i> — le dialogue <i>Layout Guides</i> de Publisher,
    /// marges et grille, lignes de base comprises (fiche 05) : l'onglet Document, Marges et Grille.
    /// </summary>
    [RelayCommand]
    private void ReperesDeMiseEnPage() => AllerAuDock(
        new DockDemande(DestinationDuReglage.Document, "ContenuMarges", "MargeGauche"),
        () =>
        {
            SectionMargesOuverte = true;
            SectionGrilleOuverte = true;
        });

    /// <summary><i>Disposition ▸ Repères de règle ▸ Format…</i> : la section Repères de règle.</summary>
    [RelayCommand]
    private void FormatDesReperes() => AllerAuDock(
        new DockDemande(DestinationDuReglage.Document, "ContenuReperesDeRegle", null),
        () => SectionReperesOuverte = true);

    /// <summary><i>Page ▸ Arrière-plan…</i> : le fond de page, à l'onglet Document.</summary>
    [RelayCommand]
    private void ArrierePlan() => AllerAuDock(
        new DockDemande(DestinationDuReglage.Document, "ContenuFondDePage", null),
        () => SectionFondDePageOuverte = true);

    /// <summary><i>Fichier ▸ Mise en page…</i> : le format de la page, à l'onglet Document.</summary>
    [RelayCommand]
    private void MiseEnPage() => AllerAuDock(
        new DockDemande(DestinationDuReglage.Document, "ContenuFormat", "FormatDePage"),
        () => SectionFormatOuverte = true);

    /// <summary>
    /// <i>Texte ▸ Police…</i> : la police, le corps et la graisse sont montés dans la barre
    /// contextuelle (I3) et ont quitté le dock (règle 1) ; l'entrée mène à la liste des polices
    /// de la barre. Grisée sans bloc qui porte du texte, comme chez Publisher (p. 24).
    /// </summary>
    [RelayCommand(CanExecute = nameof(PorteDuTexte))]
    private void Police() => AllerAuDock(
        new DockDemande(DestinationDuReglage.BarreContextuelle, null, "PoliceDuBloc"), () => { });

    /// <summary><i>Texte ▸ Paragraphe…</i> : la section Paragraphe du bloc de texte.</summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void Paragraphe() => AllerAuDock(
        new DockDemande(DestinationDuReglage.Proprietes, "ContenuParagraphe", null),
        () => SectionParagrapheOuverte = true);

    /// <summary>
    /// <i>Disposition ▸ Faire pivoter ou retourner ▸ Rotation personnalisée…</i> : le champ de
    /// l'angle, à Position et taille. Un bloc seul : à plusieurs, le dock ne montre aucun champ.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSingleSelection))]
    private void RotationPersonnalisee() => AllerAuDock(
        new DockDemande(DestinationDuReglage.Proprietes, "ContenuPositionEtTaille", "ChampAngle"),
        () => SectionPositionOuverte = true);

    /// <summary>La disponibilité des entrées qui dépendent de la sélection, et du quart de tour.</summary>
    private void NotifierLesEntreesVersLeDock()
    {
        PoliceCommand.NotifyCanExecuteChanged();
        ParagrapheCommand.NotifyCanExecuteChanged();
        RotationPersonnaliseeCommand.NotifyCanExecuteChanged();
        PivoterAGaucheCommand.NotifyCanExecuteChanged();
        PivoterADroiteCommand.NotifyCanExecuteChanged();
    }

    // ------------------------------------------------------------------ A9 : le quart de tour

    /// <summary>
    /// L'angle d'un quart de tour, ramené entre 0 et 360° comme la poignée le ramène : 350° plus
    /// 90° donne 80°, 0° moins 90° donne 270°.
    /// </summary>
    public static double QuartDeTour(double angle, double pas) => ((angle + pas) % 360 + 360) % 360;

    /// <summary><i>Faire pivoter à gauche de 90°</i> : −90°, dans le sens inverse des aiguilles.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void PivoterAGauche() => Pivoter(-90);

    /// <summary><i>Faire pivoter à droite de 90°</i> : +90°, dans le sens des aiguilles.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void PivoterADroite() => Pivoter(90);

    /// <summary>
    /// Chaque bloc pris tourne d'un quart de tour autour de son centre, en un pas d'annulation
    /// où que soit le focus : un champ du dock qui tiendrait sa transaction ouverte la voit
    /// refermée d'abord, dans son pas, puis rouverte (<c>UnGesteUnPas</c>).
    /// </summary>
    private void Pivoter(double pas)
    {
        if (Selection.Count == 0) return;

        var blocs = Selection.ToList();
        UnGesteUnPas(() =>
        {
            foreach (var bloc in blocs) bloc.Rotation = QuartDeTour(bloc.Rotation, pas);
        });
        Status = blocs.Count == 1
            ? $"Rotation : {blocs[0].Rotation.ToString("0.#", CultureInfo.CurrentCulture)}°"
            : $"{blocs.Count} blocs tournés de {(pas > 0 ? "+" : "−")}90°";
    }

    // ------------------------------------------------------------- A8 : insérer des pages

    private EventHandler? _insertionDePagesDemandee;

    /// <summary>
    /// <i>Page ▸ Insérer des pages…</i>, <c>Ctrl+Maj+N</c> : le dialogue est une fenêtre, que la
    /// fenêtre principale ouvre ; elle s'abonne ici, comme à l'export. Tant que personne n'écoute,
    /// la commande ne peut pas s'exécuter.
    /// </summary>
    public event EventHandler? InsertionDePagesDemandee
    {
        add
        {
            _insertionDePagesDemandee += value;
            InsererDesPagesCommand.NotifyCanExecuteChanged();
        }
        remove
        {
            _insertionDePagesDemandee -= value;
            InsererDesPagesCommand.NotifyCanExecuteChanged();
        }
    }

    private bool PeutInsererDesPages() => _insertionDePagesDemandee is not null;

    [RelayCommand(CanExecute = nameof(PeutInsererDesPages))]
    private void InsererDesPages() => _insertionDePagesDemandee?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Insère ce que le dialogue demande, en un seul pas : <see cref="DemandeDInsertion.Nombre"/>
    /// pages avant ou après la page courante, vierges, portant chacune un bloc de texte égal à la
    /// zone des marges, ou portant chacune une copie des blocs de la page désignée — des instances
    /// distinctes. Les noms « Page n » sont renumérotés ; la première page insérée devient la page
    /// courante, comme après « Ajouter une page ».
    /// </summary>
    public void InsererDesPages(DemandeDInsertion demande)
    {
        if (CurrentPage is null || demande.Nombre < 1) return;

        var modele = demande.Contenu == ContenuDesPages.CopieDUnePage
            ? Document.Pages[Math.Clamp(demande.PageACopier, 1, Document.Pages.Count) - 1]
            : null;

        // La saisie d'un bloc se referme d'abord, dans son pas : changer de page vide la
        // sélection, et la refermer pendant le geste fermerait sa transaction à lui.
        SetSelection([]);
        var rang = Document.Pages.IndexOf(CurrentPage) + (demande.Avant ? 0 : 1);
        UnGesteUnPas(() =>
        {
            Page? premiere = null;
            for (var i = 0; i < demande.Nombre; i++)
            {
                var page = new Page { Name = $"Page {rang + i + 1}" };
                if (modele is not null)
                {
                    foreach (var bloc in modele.Frames) page.Frames.Add(bloc.Clone());
                }
                else if (demande.Contenu == ContenuDesPages.UneZoneDeTexte)
                {
                    page.Frames.Add(ZoneDeTexteDesMarges());
                }

                Document.Pages.Insert(rang + i, page);
                premiere ??= page;
            }

            CurrentPage = premiere;
            RenumberPages();
        });
        NotifyPageList();

        Status = demande.Nombre == 1 ? "1 page insérée" : $"{demande.Nombre} pages insérées";
    }

    /// <summary>Le bloc de texte de l'option « une zone de texte par page » : la zone des marges.</summary>
    private TextFrame ZoneDeTexteDesMarges()
    {
        var bloc = (TextFrame)BuildFrame(EditorTool.Text);
        bloc.X = Document.MarginLeft;
        bloc.Y = Document.MarginTop;
        bloc.Width = Document.MarginBoxWidth;
        bloc.Height = Document.MarginBoxHeight;
        return bloc;
    }

    /// <summary>
    /// <i>Page ▸ Dupliquer la page</i>, <c>Ctrl+Maj+U</c> : la page courante copiée après elle. Le
    /// clavier joue sans retirer le focus : la saisie d'un bloc se referme d'abord, et la
    /// transaction d'un champ est refermée puis rouverte autour du pas de la copie.
    /// </summary>
    [RelayCommand]
    private void DupliquerLaPage()
    {
        if (CurrentPage is null) return;

        SetSelection([]);
        UnGesteUnPas(() => DuplicatePageCommand.Execute(null));
        Status = $"« {CurrentPage?.Name} » ajoutée";
    }

    // ------------------------------------------------------------- les pages au clavier

    /// <summary><c>Ctrl+Pg.Suiv</c> : la page suivante. À la dernière, rien ne bouge, et la barre d'état le dit.</summary>
    [RelayCommand]
    private void PageSuivante() => AllerALaPageVoisine(+1);

    /// <summary><c>Ctrl+Pg.Préc</c> : la page précédente.</summary>
    [RelayCommand]
    private void PagePrecedente() => AllerALaPageVoisine(-1);

    private void AllerALaPageVoisine(int pas)
    {
        if (CurrentPage is null) return;

        var rang = Document.Pages.IndexOf(CurrentPage) + pas;
        if (rang < 0 || rang >= Document.Pages.Count)
        {
            Status = pas > 0 ? "Dernière page" : "Première page";
            return;
        }

        CurrentPage = Document.Pages[rang];
        Status = $"Page {rang + 1} sur {Document.Pages.Count}";
    }

    private EventHandler? _champDeLaPageDemande;

    /// <summary>
    /// <i>Page ▸ Atteindre la page…</i>, <c>F5</c> ou <c>Ctrl+B</c> : le focus va au numéro de page
    /// de la barre d'état, qui est un champ. La fenêtre s'abonne ; sans elle, la commande ne peut
    /// pas s'exécuter.
    /// </summary>
    public event EventHandler? ChampDeLaPageDemande
    {
        add
        {
            _champDeLaPageDemande += value;
            AtteindreLaPageCommand.NotifyCanExecuteChanged();
        }
        remove
        {
            _champDeLaPageDemande -= value;
            AtteindreLaPageCommand.NotifyCanExecuteChanged();
        }
    }

    private bool PeutAtteindreLaPage() => _champDeLaPageDemande is not null;

    [RelayCommand(CanExecute = nameof(PeutAtteindreLaPage))]
    private void AtteindreLaPage() => _champDeLaPageDemande?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Ce qu'on tape dans le numéro de page de la barre d'état, à <c>Entrée</c> : un numéro de page
    /// du document y mène ; le reste est refusé, et la barre d'état dit pourquoi. Vrai si la page
    /// est atteinte. Aucun pas d'annulation : changer de page ne change pas le document.
    /// </summary>
    public bool AtteindreLaPage(string? saisie)
    {
        var texte = saisie?.Trim() ?? "";
        if (!int.TryParse(texte, NumberStyles.Integer, CultureInfo.CurrentCulture, out var numero))
        {
            Status = $"« {texte} » n'est pas un numéro de page.";
            return false;
        }

        if (numero < 1 || numero > Document.Pages.Count)
        {
            Status = Document.Pages.Count == 1
                ? $"« {texte} » est refusé : le document n'a qu'une page."
                : $"« {texte} » est refusé : le document a {Document.Pages.Count} pages, de 1 à {Document.Pages.Count}.";
            return false;
        }

        CurrentPage = Document.Pages[numero - 1];
        Status = $"Page {numero} sur {Document.Pages.Count}";
        return true;
    }

    // ------------------------------------------------------- Insertion ▸ un bloc à champ (C4)

    /// <summary>
    /// <i>Insertion ▸ Numéro de page, Date, Coordonnées ▸</i> : un bloc de texte qui porte le champ,
    /// posé dans la zone de composition, en un pas, comme Publisher (p. 22). Le texte saisi reste
    /// vide sous le champ ; la saisie ne s'ouvre pas, le texte étant calculé.
    /// </summary>
    [RelayCommand]
    private void InsererUnChamp(FieldKind sorte)
    {
        if (!FieldResolver.SuitsText(sorte) || sorte == FieldKind.None) return;

        var bloc = (TextFrame)BuildFrame(EditorTool.Text);
        bloc.Name = NomDuChamp(sorte);
        bloc.Field = sorte;
        bloc.Width = sorte is FieldKind.PageNumber or FieldKind.PageCount ? 60 : 200;
        bloc.Height = sorte == FieldKind.ContactAddress ? 60 : 30;
        (bloc.X, bloc.Y) = NextInsertPoint(bloc.Width, bloc.Height);
        Insert(bloc);
        Status = $"{FieldResolver.Label(sorte)} : bloc ajouté";
    }

    /// <summary>Le nom du bloc, tel que « Blocs de la page » le montre.</summary>
    private static string NomDuChamp(FieldKind sorte) => sorte switch
    {
        FieldKind.PageNumber => "Numéro de page",
        FieldKind.PageCount => "Nombre de pages",
        FieldKind.Date => "Date",
        _ => FieldResolver.Label(sorte).Replace("Coordonnées — ", "", StringComparison.Ordinal) is { Length: > 0 } nom
            ? char.ToUpper(nom[0], CultureInfo.CurrentCulture) + nom[1..]
            : "Coordonnée",
    };
}

/// <summary>Où mène une entrée « … » des menus : un onglet du dock, ou la barre contextuelle.</summary>
public enum DestinationDuReglage { BarreContextuelle, Proprietes, Document }

/// <summary>
/// Ce qu'une entrée « … » demande à la fenêtre (poste N18) : la destination ; la section, par le
/// nom de son contenu (<c>ContenuMarges</c>) ; le champ qui prend le focus, par son nom, ou, sans
/// nom, le premier champ de la section.
/// </summary>
public sealed record DockDemande(DestinationDuReglage Destination, string? Section, string? Champ);

/// <summary>Ce que portent les pages que le dialogue « Insérer des pages » insère (A8).</summary>
public enum ContenuDesPages { Vierges, UneZoneDeTexte, CopieDUnePage }

/// <summary>
/// Ce que le dialogue « Insérer des pages » demande (A8, fiche 08) : combien, avant ou après la
/// page courante, et quoi — <see cref="PageACopier"/>, numéro de page à partir de 1, ne compte que
/// pour la copie.
/// </summary>
public sealed record DemandeDInsertion(int Nombre, bool Avant, ContenuDesPages Contenu, int PageACopier);
