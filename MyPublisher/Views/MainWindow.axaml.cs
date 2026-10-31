using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.ViewModels;

namespace MyPublisher.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType DocumentType =
        new("Document MyPublisher") { Patterns = ["*." + DocumentSerializer.Extension] };

    private static readonly FilePickerFileType PdfType =
        new("Document PDF") { Patterns = ["*.pdf"] };

    private static readonly FilePickerFileType ImageType =
        new("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"] };

    private static readonly FilePickerFileType TextType =
        new("Texte") { Patterns = ["*.txt"] };

    /// <summary>
    /// Un texte rédigé ailleurs ne finit pas toujours en « .txt » — un « .md », un
    /// « .csv » : on laisse le choix.
    /// </summary>
    private static readonly FilePickerFileType AnyType =
        new("Tous les fichiers") { Patterns = ["*"] };

    /// <summary>
    /// Le dock modifie le modèle par liaison directe : on encadre la saisie d'un champ
    /// par une transaction, ce qui rend chaque édition annulable. Chaque onglet du dock
    /// et la barre contextuelle l'ouvrent et la ferment de la même façon (règle 2 de la
    /// phase I) ; « Blocs de la page », au pied du dock, en est exclu : son glissé ouvre
    /// son propre pas.
    /// </summary>
    private bool _propertyEditOpen;

    /// <summary>
    /// Le panneau — la barre ou un onglet du dock — dont un champ a pris le focus en dernier :
    /// celui qu'on interroge quand Annuler ou Rétablir a refermé sa transaction (poste Q24).
    /// </summary>
    private Control? _panneauDuChamp;

    /// <summary>
    /// Le champ numérique qui a pris le focus en dernier dans un panneau (poste N14). Sa valeur
    /// ne part plus à la frappe : ce qui y attend se valide avant que sa transaction ne se
    /// referme, alors même qu'il garde le focus — le document qu'on ferme, la sélection que le
    /// modèle de vue change.
    /// </summary>
    private ChampNumerique? _champNumerique;

    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();

        // En phase de tunnel : la liste consomme le clic pour sélectionner, et il faut
        // savoir d'où le geste part avant qu'elle ne le fasse. L'événement n'est pas
        // marqué traité — cliquer sur un bloc le sélectionne comme avant.
        ListeDesBlocs.AddHandler(PointerPressedEvent, OnBlockListPressed, RoutingStrategies.Tunnel);
        ListeDesBlocs.PointerMoved += OnBlockListMoved;
        ListeDesBlocs.PointerReleased += OnBlockListReleased;
        ListeDesBlocs.PointerCaptureLost += (_, _) => EndBlockDrag();

        // Changer d'onglet retire du dock le champ qui avait le focus, sans qu'il émette
        // toujours LostFocus : la transaction qu'il avait ouverte se referme ici. Seul le
        // changement d'onglet compte — l'événement monte aussi des listes du dock, et
        // refermer la transaction au choix d'une entrée laisserait les suivantes hors de
        // tout pas d'annulation.
        OngletsDuDock.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Source, OngletsDuDock)) ClosePropertyEdit();
        };

        // Le champ de texte qu'ouvrir la barre de menus prive du focus, retenu avant qu'il ne le
        // perde (poste N8) : en tunnel, avant que la pression ne donne le focus à l'entrée.
        AddHandler(PointerPressedEvent, OnPressionAvantLeMenu, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnToucheAvantLeMenu, RoutingStrategies.Tunnel, handledEventsToo: true);
        // De même pour le menu contextuel de la planche (poste A12), qui prend le focus en
        // s'ouvrant : retenu à sa demande, avant l'ouverture, quel que soit le geste qui l'ouvre.
        MenuDuBloc.Opening += (_, _) => RetenirLeChampSousLeMenu();

        // La fenêtre quittée Espace tenu ne verra pas son relâchement : la main se lâche ici (I6).
        Deactivated += (_, _) => PageView.MainTenue = false;

        // Alt+flèche pendant la saisie : avant la zone de texte, qui y déplacerait le curseur (N6).
        AddHandler(KeyDownEvent, DeplacerLeBlocEnSaisie, RoutingStrategies.Tunnel);

        DataContextChanged += (_, _) => AttachViewModel();
        AttachViewModel();

        // Le catalogue s'ouvre au démarrage, comme dans Publisher — sauf si un document
        // a été passé en argument : on ne demande pas ce qu'il faut ouvrir à quelqu'un
        // qui vient de le dire.
        Opened += async (_, _) =>
        {
            if (Vm?.FilePath is null) await ChooseTemplateAsync();
        };
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void AttachViewModel()
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm.TransactionsRefermees -= OnTransactionsRefermees;
            _vm.FermetureDemandee -= OnFermetureDemandee;
            _vm.ExportDemande -= OnExportDemande;
            _vm.DockDemandee -= OnDockDemandee;
            _vm.InsertionDePagesDemandee -= OnInsertionDePagesDemandee;
            _vm.ChampDeLaPageDemande -= OnChampDeLaPageDemande;
            _vm.SuivreLeClavier(null);
            _vm.SuivreLesChamps(null);
        }

        _vm = DataContext as MainWindowViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            _vm.TransactionsRefermees += OnTransactionsRefermees;
            _vm.FermetureDemandee += OnFermetureDemandee;
            _vm.ExportDemande += OnExportDemande;
            _vm.DockDemandee += OnDockDemandee;
            _vm.InsertionDePagesDemandee += OnInsertionDePagesDemandee;
            _vm.ChampDeLaPageDemande += OnChampDeLaPageDemande;
            _vm.SuivreLeClavier(() => ConsumesTyping(FocusManager?.GetFocusedElement()));
            _vm.SuivreLesChamps(ValiderLeChampNumerique);
        }

        ConstruireLesRecents();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Changer de sélection masque les champs du panneau, ou les passe au bloc d'après
        // (GabaritsGardes, Q23) : le contrôle qui avait le focus ne l'émet pas toujours en
        // le perdant. Sans cette remise à zéro, le verrou resterait fermé et plus aucune
        // édition ne serait annulable pour le reste de la session. Replier le dock fait de
        // même.
        if (e.PropertyName is nameof(MainWindowViewModel.Selected) or nameof(MainWindowViewModel.DockVisible))
        {
            ClosePropertyEdit();
        }

        if (e.PropertyName == nameof(MainWindowViewModel.DocumentsRecents)) ConstruireLesRecents();

        // Le bloc choisi sur la planche se montre dans « Blocs de la page », qui n'en
        // montre que six : la liste ne le fait pas d'elle-même quand la sélection lui
        // arrive avant qu'elle ait construit ses lignes. Après la mise en page, donc, et
        // seulement si sa ligne n'est pas déjà en vue : la liste défile alors par une passe
        // de mise en page entière, qui recycle et reconstruit chacune de ses lignes, même
        // quand elle n'a pas à bouger — un dixième de la pression, mesuré au poste Q23.
        if (e.PropertyName == nameof(MainWindowViewModel.Selected) && _vm?.Selected is { } bloc)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(_vm?.Selected, bloc) && !LigneEnVue(bloc)) ListeDesBlocs.ScrollIntoView(bloc);
            }, DispatcherPriority.Loaded);
        }
    }

    /// <summary>La ligne du bloc dans « Blocs de la page » est construite, et entière dans la liste.</summary>
    private bool LigneEnVue(Frame bloc)
    {
        if (ListeDesBlocs.ContainerFromItem(bloc) is not Control { IsVisible: true } ligne) return false;
        if (ligne.TranslatePoint(new Point(0, 0), ListeDesBlocs) is not { } haut) return false;
        return haut.Y >= 0 && haut.Y + ligne.Bounds.Height <= ListeDesBlocs.Bounds.Height;
    }

    // ---------------------------------------------------------------- fichiers

    private async void OnNew(object? sender, RoutedEventArgs e) => await ChooseTemplateAsync();

    /// <summary>
    /// Propose le catalogue et donne suite au choix. Renoncer ne touche à rien : le
    /// document en cours reste ce qu'il est. Le choix fait, et seulement alors, la question
    /// d'avant la perte est posée (poste N11) : Annuler y rend aussi le document intact.
    /// </summary>
    private async Task ChooseTemplateAsync()
    {
        if (Vm is not { } vm) return;

        var dialog = new TemplateChooserWindow
        {
            DataContext = new TemplateChooserViewModel(TemplateStore.Load())
        };

        if (await dialog.ShowDialog<TemplateChoice?>(this) is not { } choice) return;
        if (!await LaisserPartirLeDocument()) return;

        if (choice.IsBlank)
        {
            vm.NewDocumentCommand.Execute(null);
            return;
        }

        try
        {
            vm.StartFromTemplate(choice.Path!);
        }
        catch (Exception ex)
        {
            vm.Status = $"Modèle illisible : {ex.Message}";
        }
    }

    /// <summary>
    /// Le document choisi s'ouvre par <see cref="OuvrirLeDocument"/>, comme un document récent
    /// du menu Fichier : la question d'avant la perte vient une fois le fichier choisi (N11).
    /// </summary>
    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Ouvrir un document",
            AllowMultiple = false,
            FileTypeFilter = [DocumentType]
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;

        await OuvrirLeDocument(path);
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;

        if (vm.FilePath is null)
        {
            await SaveAsAsync(vm);
            return;
        }

        TrySave(vm, vm.FilePath);
    }

    private async void OnSaveAs(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) await SaveAsAsync(vm);
    }

    private async Task SaveAsAsync(MainWindowViewModel vm)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Enregistrer le document",
            SuggestedFileName = vm.Document.Title,
            DefaultExtension = DocumentSerializer.Extension,
            FileTypeChoices = [DocumentType]
        });

        if (file?.TryGetLocalPath() is { } path) TrySave(vm, path);
    }

    private static void TrySave(MainWindowViewModel vm, string path)
    {
        try
        {
            vm.SaveTo(path);
        }
        catch (Exception ex)
        {
            vm.Status = $"Enregistrement impossible : {ex.Message}";
        }
    }

    /// <summary>
    /// Le modèle de vue demande l'export (poste N10) : le menu Fichier, le bouton de la barre
    /// principale et <c>Ctrl+Maj+P</c> passent tous par sa commande, et la fenêtre fait ce que
    /// seule elle sait faire, demander le fichier.
    /// </summary>
    private async void OnExportDemande(object? sender, EventArgs e)
    {
        if (Vm is not { } vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exporter en PDF",
            SuggestedFileName = vm.Document.Title,
            DefaultExtension = "pdf",
            FileTypeChoices = [PdfType]
        });

        if (file?.TryGetLocalPath() is not { } path) return;

        try
        {
            vm.ExportPdf(path);
        }
        catch (Exception ex)
        {
            vm.Status = $"Export impossible : {ex.Message}";
        }
    }

    private async void OnInsertImage(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Insérer une image",
            AllowMultiple = false,
            FileTypeFilter = [ImageType]
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) vm.AddImage(path);
    }

    /// <summary>
    /// Choisit l'image d'un remplissage. Le bouton vit dans le gabarit du remplissage :
    /// c'est donc son contexte de données qui désigne l'objet à renseigner, et le même
    /// geste sert au fond d'un bloc comme à celui de la page. Le reste — pas
    /// d'annulation, cache d'images — est au modèle de vue, où un test peut l'atteindre
    /// sans dialogue de fichier.
    /// </summary>
    private async void OnChooseFillImage(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        if ((sender as Control)?.DataContext is not ImageFill fond) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Image de remplissage",
            AllowMultiple = false,
            FileTypeFilter = [ImageType]
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) vm.ChoisirImageDeFond(fond, path);
    }

    /// <summary>
    /// Remplit le bloc de texte sélectionné à partir d'un fichier (poste B2). Le menu et
    /// le panneau du bloc mènent ici ; seul le sélecteur de fichier est à la fenêtre, le
    /// reste — encodage, style, pas d'annulation — est au modèle de vue
    /// (<see cref="MainWindowViewModel.ImporterTexte"/>), où un test l'atteint.
    /// </summary>
    private async void OnImportText(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { PeutImporterTexte: true } vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importer un texte",
            AllowMultiple = false,
            FileTypeFilter = [TextType, AnyType]
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) ImporterTexte(path);
    }

    /// <summary>
    /// Ce que fait le geste une fois le fichier choisi : refermer la saisie sur la page et
    /// la transaction du panneau, puis passer le chemin au modèle de vue. Laissée
    /// ouverte, la saisie sur la page garderait sa zone à l'écran, et la frappe qui la
    /// précède partagerait le pas d'annulation de l'import. Publique pour qu'un test la
    /// joue sans le sélecteur de fichier, qui ne s'ouvre pas sans affichage.
    /// </summary>
    /// <returns>Vrai si le texte a été posé.</returns>
    public bool ImporterTexte(string chemin)
    {
        if (Vm is not { } vm) return false;

        PageView.EndTextEdit();
        ClosePropertyEdit();
        return vm.ImporterTexte(chemin);
    }

    private void OnQuit(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Ouvre les jeux de coordonnées. Le dialogue travaille sur une copie : il ne rend
    /// un magasin que si l'utilisateur a validé, et c'est seulement alors qu'on écrit.
    /// </summary>
    private async void OnEditBusinessInfo(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;

        var dialog = new BusinessInfoWindow { DataContext = new BusinessInfoViewModel(vm.Contacts) };
        if (await dialog.ShowDialog<BusinessInfoStore?>(this) is not { } saved) return;

        try
        {
            saved.Save();
            vm.ApplyContacts(saved);
        }
        catch (Exception ex)
        {
            vm.Status = $"Coordonnées non enregistrées : {ex.Message}";
        }
    }

    // ------------------------------------------------------ vers le dock (poste N18)

    /// <summary>
    /// Une entrée « … » des menus demande un réglage (<see cref="DockDemande"/>) : le modèle de vue
    /// a montré le dock et ouvert la section ; la fenêtre choisit l'onglet, puis, la mise en page
    /// faite — les gabarits gardés ne montrent la section d'une sorte de bloc qu'au passage
    /// suivant —, met le focus dans le champ visé, ou dans le premier champ de la section. Le
    /// focus ouvre la transaction du panneau, comme un clic ; rien n'ayant changé, aucun pas.
    /// </summary>
    private void OnDockDemandee(object? sender, DockDemande demande)
    {
        switch (demande.Destination)
        {
            case DestinationDuReglage.Proprietes:
                OngletsDuDock.SelectedItem = OngletProprietes;
                break;
            case DestinationDuReglage.Document:
                OngletsDuDock.SelectedItem = OngletDocument;
                break;
        }

        Control racine = demande.Destination == DestinationDuReglage.BarreContextuelle ? BarreContextuelle : Dock;
        MettreLeFocusAuReglage(racine, demande, essais: 3);
    }

    private void MettreLeFocusAuReglage(Control racine, DockDemande demande, int essais)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ChampDuReglage(racine, demande) is { } champ)
            {
                champ.BringIntoView();
                champ.Focus(NavigationMethod.Tab);
            }
            else if (essais > 1)
            {
                MettreLeFocusAuReglage(racine, demande, essais - 1);
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Le champ que vise la demande, s'il est à l'écran : nommé, ou le premier champ de la
    /// section — une zone de texte, un champ numérique, une liste, une case —, sinon le premier
    /// contrôle qui prend le focus, un bouton du dock.
    /// </summary>
    private static Control? ChampDuReglage(Control racine, DockDemande demande)
    {
        var dans = racine;
        if (demande.Section is { } section)
        {
            dans = racine.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(c => c.Name == section && c.IsEffectivelyVisible);
            if (dans is null) return null;
        }

        if (demande.Champ is { } nom)
        {
            var nomme = dans.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(c => c.Name == nom && c.IsEffectivelyVisible);
            // Un champ numérique prend la frappe par sa zone de texte.
            return nomme is NumericUpDown numerique
                ? numerique.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() ?? nomme
                : nomme;
        }

        var prenables = dans.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.IsEffectivelyEnabled && c.Focusable).ToList();
        return prenables.FirstOrDefault(c => c is TextBox or ComboBox or CheckBox) ?? prenables.FirstOrDefault();
    }

    /// <summary>
    /// <i>Page ▸ Insérer des pages…</i>, <c>Ctrl+Maj+N</c> : le dialogue de A8, puis l'insertion, en
    /// un pas, par le modèle de vue. Annuler n'insère rien.
    /// </summary>
    private async void OnInsertionDePagesDemandee(object? sender, EventArgs e)
    {
        if (Vm is not { } vm) return;

        if (await InsererDesPages.Demander(this, vm.Document.Pages.Count, vm.CurrentPageNumber) is { } demande)
        {
            vm.InsererDesPages(demande);
        }
    }

    /// <summary><c>F5</c>, <c>Ctrl+B</c>, <i>Page ▸ Atteindre la page…</i> : le numéro de page de la barre d'état.</summary>
    private void OnChampDeLaPageDemande(object? sender, EventArgs e) => PageSurPages.Prendre();

    // ---------------------------------------------------------------- propriétés

    private void OnPropertyPanelGotFocus(object? sender, GotFocusEventArgs e)
    {
        // On referme d'abord une éventuelle transaction restée ouverte : chaque champ
        // compte ainsi pour une seule étape d'annulation, même si le passage de l'un
        // à l'autre n'a pas émis LostFocus.
        ClosePropertyEdit();

        _propertyEditOpen = true;
        _panneauDuChamp = sender as Control;
        _champNumerique = (e.Source as Visual)?.FindAncestorOfType<ChampNumerique>(includeSelf: true);
        Vm?.BeginEdit();
    }

    private void OnPropertyPanelLostFocus(object? sender, RoutedEventArgs e) => ClosePropertyEdit();

    /// <summary>
    /// Annuler ou Rétablir a refermé la transaction du champ avec toutes les autres (poste
    /// Q24). Le champ qui a toujours le focus rouvre la sienne, et la suite de sa saisie fait
    /// son pas. Sans cela, il la croyait ouverte quand il n'y avait rien à défaire, et ce
    /// qu'on y tapait n'entrait dans aucun pas ; et quand l'annulation relit le document, un
    /// champ que la sélection ne masque pas — une marge de l'onglet Document — gardait le
    /// focus sans transaction. Les champs du bloc, eux, le perdent : la sélection se vide en
    /// passant, et leurs réglages se masquent un instant.
    /// </summary>
    private void OnTransactionsRefermees(object? sender, EventArgs e)
    {
        _propertyEditOpen = false;
        if (_panneauDuChamp is not { IsKeyboardFocusWithin: true }) return;

        _propertyEditOpen = true;
        Vm?.BeginEdit();
    }

    private void ClosePropertyEdit()
    {
        // Ce qui attend dans le champ part d'abord, dans la transaction qu'il avait ouverte
        // (N14) : « 40 » tapé dans Y, puis Fermer, marque le document, et la question se pose.
        ValiderLeChampNumerique();
        if (!_propertyEditOpen) return;

        _propertyEditOpen = false;
        Vm?.CommitEdit();
    }

    /// <summary>
    /// Valide ce qui attend dans le dernier champ numérique pris (N14) : avant que sa
    /// transaction ne se referme, et quand le modèle de vue referme les transactions ouvertes
    /// — Annuler, Rétablir, un raccourci qui fait son propre pas —, change la sélection,
    /// enregistre ou exporte : la valeur tapée y était au document quand elle partait à la
    /// frappe.
    /// </summary>
    private void ValiderLeChampNumerique() => _champNumerique?.Valider();

    // ------------------------------------------------------- ordre des blocs à la souris

    /// <summary>Déplacement, en pixels, à partir duquel un clic devient un glissement.</summary>
    private const double DragThreshold = 4;

    private Frame? _draggedBlock;
    private Point _dragOrigin;
    private bool _dragging;

    /// <summary>Rang d'insertion visé, c'est-à-dire le rang <b>avant</b> lequel on dépose.</summary>
    private int _dropIndex = -1;

    /// <summary>
    /// Le geste est bâti sur les pointeurs et non sur <c>DragDrop.DoDragDrop</c>.
    ///
    /// Deux raisons, et la seconde suffirait. La plate-forme sans affichage n'inscrit
    /// aucune source de glisser-déposer : un geste bâti sur elle ne pourrait pas être
    /// joué par le banc, et ce poste serait éprouvé sur la parole. Et c'est déjà ainsi
    /// que la planche travaille — pointeur brut, ordre de superposition explicite —
    /// ce qui fait un idiome de moins à connaître.
    /// </summary>
    private void OnBlockListPressed(object? sender, PointerPressedEventArgs e)
    {
        EndBlockDrag();

        if (!e.GetCurrentPoint(ListeDesBlocs).Properties.IsLeftButtonPressed) return;
        if (e.Source is not Visual source) return;
        // L'œil d'une ligne (poste I8) est un bouton, pas une prise : on ne tire pas un bloc
        // par son œil.
        if (source.GetSelfAndVisualAncestors().OfType<Button>().Any())
        {
            RendreLeFocusApresLOeil();
            return;
        }
        if (source.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()
            is not { DataContext: Frame frame }) return;

        _draggedBlock = frame;
        _dragOrigin = e.GetPosition(ListeDesBlocs);
    }

    /// <summary>
    /// L'œil ne prend pas le focus (poste I8) : la frappe en cours garde le sien. Le bouton
    /// n'est pas focalisable, mais la pression donne le focus au premier ancêtre qui l'est,
    /// sa ligne : le champ du dock ou la saisie sur la page le perdait, et la lettre suivante
    /// armait un outil au lieu de s'écrire (trouvé par la relecture du 26 octobre 2026). Le
    /// focus revient, la pression passée, au champ qui l'avait, s'il est encore à l'écran —
    /// il ne l'est plus quand l'œil vient de masquer le bloc qu'on tapait. Le champ du dock
    /// referme sa saisie en perdant le focus et la rouvre en le reprenant : le début de la
    /// frappe, l'œil et la suite ont chacun leur pas, comme le veut la règle 2.
    /// </summary>
    private void RendreLeFocusApresLOeil()
    {
        if (FocusManager?.GetFocusedElement() is not InputElement champ) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (champ.IsEffectivelyVisible && champ.IsAttachedToVisualTree()) champ.Focus();
        });
    }

    private void OnBlockListMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedBlock is null) return;

        if (!e.GetCurrentPoint(ListeDesBlocs).Properties.IsLeftButtonPressed)
        {
            EndBlockDrag();
            return;
        }

        var point = e.GetPosition(ListeDesBlocs);

        // On ne s'engage qu'au-delà du seuil : sans lui, le tremblement d'un clic
        // ordinaire réordonnerait la pile à l'insu de celui qui sélectionne un bloc.
        if (!_dragging)
        {
            if (Math.Abs(point.Y - _dragOrigin.Y) < DragThreshold) return;

            _dragging = true;
            e.Pointer.Capture(ListeDesBlocs);
        }

        _dropIndex = DropIndexAt(point);
        ShowDropMark(_dropIndex);
    }

    private void OnBlockListReleased(object? sender, PointerReleasedEventArgs e)
    {
        // L'état est relevé avant de rendre la capture : la rendre ferme le geste.
        var frame = _draggedBlock;
        var dragging = _dragging;
        var index = _dropIndex;

        if (dragging) e.Pointer.Capture(null);
        EndBlockDrag();

        if (!dragging || frame is null || Vm is not { } vm) return;
        if (vm.CurrentPage is not { } page) return;
        var pile = PileInversee.De(page.Frames);
        if (pile.IndexOf(frame) is var depart && depart < 0) return;

        // Le rang visé est un point d'insertion, entre deux lignes de la liste. Retirer le
        // bloc de sa ligne fait remonter d'un cran toutes celles qui la suivent, ce point
        // compris : un dépôt plus bas dans la liste vaut donc une ligne de moins qu'il n'y
        // paraît. La liste montre la pile de l'avant vers le fond (poste I2) : la ligne
        // d'arrivée se lit ensuite au rang miroir de la page.
        if (index > depart) index--;

        if (vm.ReorderFrame(frame, pile.Miroir(index))) e.Handled = true;
    }

    private void EndBlockDrag()
    {
        _draggedBlock = null;
        _dragging = false;
        _dropIndex = -1;
        TraitDInsertion.IsVisible = false;
    }

    /// <summary>
    /// Les éléments réellement construits, avec leur rang et leur bande verticale dans
    /// la liste. Le rang est celui de la liste, et non la position parmi les éléments
    /// construits : la liste ne construit que ce qu'elle affiche, et compter les éléments
    /// présents désignerait le mauvais bloc dès qu'elle défile.
    /// </summary>
    private List<(int Index, double Top, double Height)> RealisedItems()
    {
        var items = new List<(int, double, double)>();

        for (var i = 0; i < ListeDesBlocs.ItemCount; i++)
        {
            if (ListeDesBlocs.ContainerFromIndex(i) is not { } container) continue;
            if (container.TranslatePoint(new Point(0, 0), ListeDesBlocs) is not { } origin) continue;

            items.Add((i, origin.Y, container.Bounds.Height));
        }

        return items;
    }

    /// <summary>
    /// Le rang d'insertion que désigne le pointeur : celui de l'élément dont on survole
    /// la moitié haute, le suivant si l'on est dans sa moitié basse.
    /// </summary>
    private int DropIndexAt(Point point)
    {
        var items = RealisedItems();
        if (items.Count == 0) return 0;

        foreach (var (index, top, height) in items)
        {
            if (point.Y < top + height / 2) return index;
            if (point.Y < top + height) return index + 1;
        }

        return items[^1].Index + 1;
    }

    private void ShowDropMark(int index)
    {
        var items = RealisedItems();
        if (items.Count == 0) return;

        var y = items.FirstOrDefault(item => item.Index == index) is { Height: > 0 } cible
            ? cible.Top
            : items[^1].Top + items[^1].Height;

        Canvas.SetLeft(TraitDInsertion, 0);
        Canvas.SetTop(TraitDInsertion, Math.Max(0, y - 1));
        TraitDInsertion.Width = ListeDesBlocs.Bounds.Width;
        TraitDInsertion.IsVisible = true;
    }

    // ---------------------------------------------------------------- presse-papiers

    /// <summary>Le format propre des blocs (poste N8) : le JSON d'un document d'une page.</summary>
    private static readonly DataFormat<string> FormatDesBlocs =
        DataFormat.CreateStringApplicationFormat(MainWindowViewModel.FormatDesBlocs);

    /// <summary>
    /// La dernière écriture au presse-papiers, que le collage attend avant de lire : le
    /// presse-papiers est asynchrone, et un <c>Ctrl+V</c> qui suit de près un <c>Ctrl+C</c>
    /// lirait sinon ce qu'il y avait avant.
    /// </summary>
    private Task _ecritureDuPressePapiers = Task.CompletedTask;

    /// <summary>
    /// Le champ de texte que la barre de menus a privé du focus, et la sélection qu'il avait :
    /// une entrée de menu prend le focus à la pression, et un <c>TextBox</c> qui le perd vide
    /// sa sélection. Retenu à la pression sur la barre, ou à la touche qui l'ouvre, tant que le
    /// focus n'y est pas déjà ; nul si aucun champ de texte n'avait le focus.
    /// </summary>
    private (TextBox Champ, int Debut, int Fin)? _champSousLeMenu;

    private void OnPressionAvantLeMenu(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Contains(Menus)) RetenirLeChampSousLeMenu();
    }

    private void OnToucheAvantLeMenu(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftAlt or Key.RightAlt or Key.F10) RetenirLeChampSousLeMenu();
    }

    private void RetenirLeChampSousLeMenu()
    {
        // Le focus déjà dans les menus : c'est le champ d'avant qui compte, il reste retenu.
        if (FocusManager?.GetFocusedElement() is Visual focus && focus.GetSelfAndVisualAncestors().Any(v => v is MenuItem))
        {
            return;
        }

        _champSousLeMenu = ChampQuiALeFocus() is { } champ ? (champ, champ.SelectionStart, champ.SelectionEnd) : null;
    }

    /// <summary>Le champ de texte qui a le focus : une zone du dock ou de la barre, la saisie d'un champ numérique ou d'un bloc.</summary>
    private TextBox? ChampQuiALeFocus() =>
        FocusManager?.GetFocusedElement() is Visual focus
            ? focus.GetSelfAndVisualAncestors().OfType<TextBox>().FirstOrDefault()
            : null;

    /// <summary>
    /// Rend le geste du presse-papiers au champ de texte, s'il y en a un, et dit s'il l'a fait :
    /// au champ qui a le focus, sur-le-champ ; pour une entrée du menu, au champ que la barre de
    /// menus en a privé, ou à défaut à la zone de saisie ouverte sur la page.
    ///
    /// Ce dernier geste attend que le menu se soit refermé. Le menu reprend le focus en se
    /// refermant, après le clic sur son entrée, et le <c>TextBox</c> ne lit sa sélection qu'en
    /// différé (<c>TextBox.Copy</c>) : rendu pendant le clic, le champ l'aurait déjà perdue de
    /// nouveau. Il reprend donc le focus et sa sélection une fois le menu refermé, puis joue le
    /// geste — et l'on continue d'y taper, comme après le menu d'un traitement de texte.
    /// </summary>
    private bool RendreAuChamp(object? sender, Action<TextBox> geste)
    {
        if (ChampQuiALeFocus() is { } champ)
        {
            geste(champ);
            return true;
        }

        if (sender is not MenuItem) return false;

        TextBox? cible;
        int debut, fin;
        if (_champSousLeMenu is var (retenu, retenuDebut, retenuFin) && retenu.IsEffectivelyVisible &&
            ReferenceEquals(retenu.GetVisualRoot(), this))
        {
            (cible, debut, fin) = (retenu, retenuDebut, retenuFin);
        }
        else if (PageView.IsEditingText &&
                 PageView.GetVisualDescendants().OfType<TextBox>()
                     .FirstOrDefault(zone => zone.DataContext is TextFrame { IsEditing: true } && zone.IsVisible) is { } zone)
        {
            (cible, debut, fin) = (zone, zone.SelectionStart, zone.SelectionEnd);
        }
        else
        {
            return false;
        }

        _champSousLeMenu = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (!cible.IsEffectivelyVisible || !ReferenceEquals(cible.GetVisualRoot(), this)) return;
            cible.Focus();
            cible.SelectionStart = debut;
            cible.SelectionEnd = fin;
            geste(cible);
        }, DispatcherPriority.Background);
        return true;
    }

    /// <summary>
    /// Couper (poste N8) : <c>Ctrl+X</c> hors saisie, et l'entrée du menu Édition, qui rend le
    /// geste au champ de texte en saisie. Hors saisie, la sélection part au presse-papiers et
    /// quitte la page, en un pas.
    /// </summary>
    private void OnCouper(object? sender, RoutedEventArgs e)
    {
        if (RendreAuChamp(sender, champ => champ.Cut())) return;
        if (Vm?.CouperLaSelection() is { } contenu) Poser(contenu);
    }

    /// <summary>Copier (poste N8), sur le modèle de <see cref="OnCouper"/> ; le document ne change pas.</summary>
    private void OnCopier(object? sender, RoutedEventArgs e)
    {
        if (RendreAuChamp(sender, champ => champ.Copy())) return;
        if (Vm?.CopierLaSelection() is { } contenu) Poser(contenu);
    }

    /// <summary>
    /// Coller (poste N8). En saisie, le champ colle son texte. Hors saisie, des blocs au format
    /// propre se posent sur la page courante ; un texte venu d'ailleurs devient un bloc.
    /// </summary>
    private async void OnColler(object? sender, RoutedEventArgs e)
    {
        if (RendreAuChamp(sender, champ => champ.Paste())) return;

        if (Vm is not { } vm || Clipboard is not { } pressePapiers) return;

        try
        {
            await _ecritureDuPressePapiers;
            if (await pressePapiers.TryGetValueAsync(FormatDesBlocs) is { } blocs)
            {
                vm.CollerDesBlocs(blocs);
                return;
            }

            vm.CollerDuTexte(await pressePapiers.TryGetTextAsync());
        }
        catch (Exception ex)
        {
            vm.Status = $"Collage impossible : {ex.Message}";
        }
    }

    /// <summary>Pose au presse-papiers les blocs et leur texte brut, en une seule écriture.</summary>
    private async void Poser(MainWindowViewModel.ContenuDuPressePapiers contenu)
    {
        if (Clipboard is not { } pressePapiers) return;

        var element = new DataTransferItem();
        element.Set(FormatDesBlocs, contenu.Blocs);
        if (contenu.Texte.Length > 0) element.Set(DataFormat.Text, contenu.Texte);
        var transfert = new DataTransfer();
        transfert.Add(element);

        try
        {
            _ecritureDuPressePapiers = pressePapiers.SetDataAsync(transfert);
            await _ecritureDuPressePapiers;
        }
        catch (Exception ex)
        {
            _ecritureDuPressePapiers = Task.CompletedTask;
            if (Vm is { } vm) vm.Status = $"Presse-papiers indisponible : {ex.Message}";
        }
    }

    // ---------------------------------------------------------------- clavier

    /// <summary>
    /// Un contrôle qui consomme les frappes : zone de texte, ou liste déroulante,
    /// qui cherche une entrée à la lettre. Les raccourcis d'outil à une lettre ne
    /// doivent pas les court-circuiter — taper « T » dans la liste des polices doit
    /// chercher une police, pas armer l'outil texte. Depuis le poste N6, le modèle de vue
    /// le lit aussi (<see cref="MainWindowViewModel.ConsumesTyping"/>) : les raccourcis
    /// d'objet des liaisons de la fenêtre se taisent tant qu'il est vrai.
    /// </summary>
    private static bool ConsumesTyping(IInputElement? element) =>
        element is Visual visual &&
        visual.GetSelfAndVisualAncestors().Any(ancestor => ancestor is TextBox or ComboBox);

    /// <summary>
    /// Une frappe sous <c>Ctrl</c>, que <c>Maj</c> ou <c>Alt</c> accompagnent ou non, sans la touche
    /// système : celle que la table des raccourcis par caractère lit (poste N10).
    /// </summary>
    private static bool ParCaractere(KeyEventArgs e) =>
        (e.KeyModifiers & ~(KeyModifiers.Shift | KeyModifiers.Alt)) == KeyModifiers.Control;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Vm is not { } vm)
        {
            base.OnKeyDown(e);
            return;
        }

        // Les raccourcis qui sont des caractères — Ctrl+], Ctrl+[, Ctrl+Maj+> et Ctrl+Maj+< —,
        // lus sur le caractère produit, avant la garde de la saisie (poste N10) : une liaison
        // compare la touche, et sur un AZERTY « < » et « > » partagent une touche, « ] » et « [ »
        // demandent AltGr. Maj et Alt n'y comptent pas, puisqu'ils produisent le caractère —
        // AltGr est Ctrl+Alt sous Windows, et ne se voit pas sous X11. Aucun champ ne prend ces
        // touches : elles remontent jusqu'ici, et, que personne ne les prenne, la zone de saisie
        // en écrirait le caractère — la sonde l'a vu pour Ctrl+« < ».
        if (ParCaractere(e) && vm.JouerLeRaccourciParCaractere(e.KeySymbol))
        {
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                // Sans Maj : Ctrl+Maj+N insère des pages, par sa liaison (poste N18). Il proposait
                // le catalogue, Maj comptée pour rien.
                case Key.N when !e.KeyModifiers.HasFlag(KeyModifiers.Shift): OnNew(this, e); e.Handled = true; return;
                case Key.O: OnOpen(this, e); e.Handled = true; return;
                case Key.S: OnSave(this, e); e.Handled = true; return;
            }
        }

        // La barre de menus ouverte, une lettre est une lettre d'accès, cherchée au seul niveau
        // ouvert, comme Windows (poste N13, MainWindow.Menus.cs) : avant la garde de la saisie, que
        // la barre ouverte depuis un champ ne laisse pas la lettre au champ.
        if (JouerLaLettreDuMenu(e))
        {
            e.Handled = true;
            return;
        }

        // Pendant une saisie, les touches appartiennent au contrôle qui a le focus.
        if (PageView.IsEditingText || ConsumesTyping(FocusManager?.GetFocusedElement()))
        {
            base.OnKeyDown(e);
            return;
        }

        // La barre de menus ouverte — Alt relâché, ou un menu déroulé — a les touches, elle aussi :
        // ses lettres d'accès se lisent après cette méthode (AccessKeyHandler, à la fenêtre), et
        // « E », « T » ou « R » y armaient un outil au lieu d'ouvrir Edition, Texte ou Rectangle ;
        // Suppr y supprimait le bloc sous le menu (poste N13).
        if (Menus.IsOpen)
        {
            base.OnKeyDown(e);
            return;
        }

        // Le presse-papiers des blocs (poste N8) : ici, après la garde de la saisie, et jamais
        // en KeyBinding. Une liaison passerait avant le champ qui a le focus et lui volerait la
        // touche ; le TextBox, lui, marque toujours Ctrl+X, C et V comme traitées, si bien qu'en
        // saisie la fenêtre ne les voit jamais, et la garde ci-dessus couvre le reste.
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.X: OnCouper(this, e); e.Handled = true; return;
                case Key.C: OnCopier(this, e); e.Handled = true; return;
                case Key.V: OnColler(this, e); e.Handled = true; return;
                // Les secondes touches du presse-papiers de l'aide en français, que le propriétaire
                // a demandées le 28 octobre 2026 (poste N10) : Ctrl+Inser copie, et, plus bas,
                // Maj+Inser colle, Maj+Suppr coupe. En saisie et dans un champ, la garde les laisse
                // au TextBox, qui les tient de lui-même (PlatformHotkeyConfiguration d'Avalonia).
                case Key.Insert: OnCopier(this, e); e.Handled = true; return;
            }
        }

        // Maj+Suppr coupe, et ne supprime plus : sans ce cas, Suppr, lu plus bas sans regarder Maj,
        // emportait la sélection hors du presse-papiers.
        if (e.KeyModifiers == KeyModifiers.Shift)
        {
            switch (e.Key)
            {
                case Key.Insert: OnColler(this, e); e.Handled = true; return;
                case Key.Delete: OnCouper(this, e); e.Handled = true; return;
            }
        }

        // Raccourcis d'outils à une lettre, comme dans les logiciels de dessin.
        // Ils ne peuvent pas gêner la frappe : on a déjà rendu la main plus haut
        // dès qu'un champ de texte détient le focus.
        if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.V: vm.ActiveTool = EditorTool.Select; e.Handled = true; return;
                case Key.T: vm.ActiveTool = EditorTool.Text; e.Handled = true; return;
                case Key.H: vm.ActiveTool = EditorTool.Heading; e.Handled = true; return;
                case Key.R: vm.ActiveTool = EditorTool.Rectangle; e.Handled = true; return;
                case Key.E: vm.ActiveTool = EditorTool.Ellipse; e.Handled = true; return;
                case Key.L: vm.ActiveTool = EditorTool.Line; e.Handled = true; return;
                // « B » comme bande : « T » appartient au texte.
                case Key.B: vm.ActiveTool = EditorTool.Table; e.Handled = true; return;
                case Key.Z: vm.ActiveTool = EditorTool.Zoom; e.Handled = true; return;
                // Espace tenu : la main, sans changer d'outil, jusqu'au relâchement (I6). Pas
                // quand un bouton a le focus — Espace l'actionnerait — ni pendant une saisie,
                // écartée plus haut.
                case Key.Space when FocusManager?.GetFocusedElement() is not Button:
                    PageView.MainTenue = true;
                    e.Handled = true;
                    return;
            }
        }

        var fine = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.Delete or Key.Back:
                vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                return;
            case Key.Left: vm.NudgeSelection(-1, 0, fine); e.Handled = true; return;
            case Key.Right: vm.NudgeSelection(1, 0, fine); e.Handled = true; return;
            case Key.Up: vm.NudgeSelection(0, -1, fine); e.Handled = true; return;
            case Key.Down: vm.NudgeSelection(0, 1, fine); e.Handled = true; return;
            // Tab et Maj+Tab, la planche ayant le focus : le bloc suivant ou le précédent dans
            // l'ordre de la page, les masqués sautés, comme Publisher (poste N13). Ici, avant la
            // navigation du focus d'Avalonia, qui est un gestionnaire d'instance de la fenêtre et
            // ne voit pas une touche traitée. Ailleurs — le dock, la liste des blocs, la barre —,
            // Tab continue d'aller au champ suivant ; en saisie, la garde plus haut l'a rendu.
            case Key.Tab when (e.KeyModifiers & ~KeyModifiers.Shift) == KeyModifiers.None && LaPlancheALeFocus():
                vm.SelectionnerLeBlocVoisin(enArriere: fine);
                e.Handled = true;
                return;
            case Key.Escape:
                // Échap désarme l'outil s'il y en a un — la main et la loupe comprises —, et ne
                // libère la sélection qu'ensuite. Un membre pris seul rend d'abord son groupe (N26).
                if (vm.ActiveTool != EditorTool.Select) vm.ActiveTool = EditorTool.Select;
                else if (!vm.RendreLeGroupe()) vm.Selected = null;
                e.Handled = true;
                return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// La planche a le focus : la planche elle-même, ou ce qu'elle porte. C'est elle que le clic
    /// sur la page ou sur une règle rend au clavier (<c>PageCanvas.Focus()</c>).
    /// </summary>
    private bool LaPlancheALeFocus() =>
        FocusManager?.GetFocusedElement() is Visual focus &&
        (ReferenceEquals(focus, PageView) || PageView.IsVisualAncestorOf(focus));

    /// <summary>
    /// <c>Alt</c>+flèche pendant la saisie d'un bloc, la zone de saisie ayant le focus : le bloc
    /// se déplace, de 3 pt (<c>Maj</c> : 0,5 pt), dans un pas à lui, et la saisie continue — l'aide
    /// de Publisher en français : « déplacer l'objet dont le texte a le curseur » (poste N6). En
    /// tunnel, avant la zone, qui prend les flèches pour son curseur, <c>Alt</c> compris. Hors de
    /// la saisie, la flèche passe par <see cref="OnKeyDown"/>, comme avant.
    /// </summary>
    private void DeplacerLeBlocEnSaisie(object? sender, KeyEventArgs e)
    {
        if (Vm is not { } vm || !PageView.IsEditingText) return;
        if ((e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.Alt) return;
        if (FocusManager?.GetFocusedElement() is not Visual champ || !PageView.IsVisualAncestorOf(champ)) return;

        var fine = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.Left: vm.DeplacerLeBlocEnSaisie(-1, 0, fine); break;
            case Key.Right: vm.DeplacerLeBlocEnSaisie(1, 0, fine); break;
            case Key.Up: vm.DeplacerLeBlocEnSaisie(0, -1, fine); break;
            case Key.Down: vm.DeplacerLeBlocEnSaisie(0, 1, fine); break;
            default: return;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Espace relâché rend l'outil d'avant (I6). Toujours, sans regarder le focus : une main
    /// restée tenue parce que le relâchement serait allé ailleurs ne se lâcherait plus.
    /// </summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space) PageView.MainTenue = false;
        base.OnKeyUp(e);
    }
}
