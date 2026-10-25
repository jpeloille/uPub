using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Converters;
using MyPublisher.Models;
using MyPublisher.Services;

namespace MyPublisher.ViewModels;

/// <summary>
/// Ce que la disposition de la fenêtre (phase I, poste I2, avec B7) ajoute au modèle de
/// vue. Un fichier à lui, comme la règle 10 de la phase le veut : I3, I6 et I7 ajoutent
/// les leurs sans toucher à celui-ci.
///
/// Rien ici n'entre dans le document. Les panneaux repliés, les sections fermées et la
/// case « Conserver les proportions » sont des états de la SESSION : ils ne passent ni
/// par l'historique ni par l'état « modifié ». Ce qui change le document — le gras, un
/// alignement, le fond, la marge commune — ouvre et ferme son pas d'annulation ici, comme
/// les commandes voisines.
/// </summary>
public partial class MainWindowViewModel
{
    // ------------------------------------------------------- B7 : replier les panneaux

    /// <summary>Le panneau Pages, entre le rail et la planche. Menu Fenêtre.</summary>
    [ObservableProperty] private bool _panneauPagesVisible = true;

    /// <summary>Le dock à quatre onglets, à droite de la planche. Menu Fenêtre.</summary>
    [ObservableProperty] private bool _dockVisible = true;

    /// <summary>
    /// Ce que montrait la fenêtre avant qu'on replie : un second <c>Ctrl+F1</c> le rend.
    /// Rendre « les deux » à chaque fois effacerait un choix — le dock seul, pour un écran
    /// étroit — que le premier repli n'avait pas à connaître.
    /// </summary>
    private (bool Pages, bool Dock)? _avantLeRepli;

    /// <summary>Vrai le temps que <see cref="ReplierLesPanneaux"/> pose les deux panneaux.</summary>
    private bool _repliEnCours;

    /// <summary>
    /// Replie les deux colonnes latérales pour rendre leur place à la planche, comme le
    /// <c>Ctrl+F1</c> de Publisher (fiche 04) ; les déplie si elles le sont déjà. Le rail
    /// d'outils reste : sans lui, plus aucun outil ne s'arme qu'au clavier.
    ///
    /// La règle est lue sur ce qu'on voit, et non sur un drapeau : deux panneaux masqués à
    /// la main au menu Fenêtre se déplient aussi au raccourci, faute d'un état d'avant à
    /// rendre.
    /// </summary>
    [RelayCommand]
    private void ReplierLesPanneaux()
    {
        _repliEnCours = true;
        try
        {
            if (PanneauPagesVisible || DockVisible)
            {
                _avantLeRepli = (PanneauPagesVisible, DockVisible);
                PanneauPagesVisible = false;
                DockVisible = false;
                Status = "Panneaux repliés — Ctrl+F1 les rend";
                return;
            }

            var (pages, dock) = _avantLeRepli ?? (true, true);
            _avantLeRepli = null;
            PanneauPagesVisible = pages;
            DockVisible = dock;
            Status = "Panneaux dépliés";
        }
        finally
        {
            _repliEnCours = false;
        }
    }

    // Un panneau montré ou masqué au menu Fenêtre périme l'état d'avant le repli : rouverts
    // puis masqués à la main, les deux panneaux se déplient tous deux au raccourci, au lieu
    // de rendre ce qu'un repli plus ancien avait retenu.
    partial void OnPanneauPagesVisibleChanged(bool value)
    {
        if (!_repliEnCours) _avantLeRepli = null;
    }

    partial void OnDockVisibleChanged(bool value)
    {
        if (!_repliEnCours) _avantLeRepli = null;
    }

    // ------------------------------------------------------------- points d'accroche

    /// <summary>
    /// La case « Conserver les proportions » de Position et taille. Le poste I2 la pose ;
    /// son geste — un coin tiré ou une largeur saisie qui gardent le rapport du bloc —
    /// vient de I6, qui lit cette valeur. État de la session et non du document, décochée
    /// au départ : c'est le comportement d'aujourd'hui, où seul <c>Maj</c> garde le
    /// rapport, et c'est à I6 de dire s'il change.
    /// </summary>
    [ObservableProperty] private bool _conserverLesProportions;

    private EventHandler? _pageEntiereDemandee;

    /// <summary>
    /// Le bouton « Page entière » de la barre principale. Le poste I2 le pose ; la page
    /// tenue dans la zone visible est un calcul de la planche, qui seule connaît sa taille
    /// à l'écran, ses règles et sa marge (I6). La planche s'abonne ici ; tant que personne
    /// ne l'écoute, le bouton est grisé plutôt que de ne rien faire en silence.
    /// </summary>
    public event EventHandler? PageEntiereDemandee
    {
        add
        {
            _pageEntiereDemandee += value;
            PageEntiereCommand.NotifyCanExecuteChanged();
        }
        remove
        {
            _pageEntiereDemandee -= value;
            PageEntiereCommand.NotifyCanExecuteChanged();
        }
    }

    private bool PeutAfficherLaPageEntiere() => _pageEntiereDemandee is not null;

    [RelayCommand(CanExecute = nameof(PeutAfficherLaPageEntiere))]
    private void PageEntiere() => _pageEntiereDemandee?.Invoke(this, EventArgs.Empty);

    // ------------------------------------------------------------------- le zoom

    /// <summary>
    /// Les bornes du zoom, que <see cref="SetZoom"/> tient et que le curseur de la barre
    /// d'état parcourt en échelle logarithmique. Une seule écriture pour les deux : un
    /// curseur aux bornes recopiées garderait les anciennes le jour où l'on les change.
    /// </summary>
    public const double ZoomMinimum = 0.1;

    public const double ZoomMaximum = 8.0;

    // ------------------------------------------------------------ barre de menus

    /// <summary>
    /// Le titre que la barre de menus centre, comme la maquette : le nom du fichier, et
    /// « modifié » tant qu'il n'est pas enregistré. La barre de titre du système garde le
    /// sien (<see cref="WindowTitle"/>), que le bureau affiche dans sa liste des fenêtres.
    /// </summary>
    public string TitreDuDocument =>
        (FilePath is null ? "Sans titre" : Path.GetFileName(FilePath)) +
        (IsDirty || RetoucheEnCours ? " — modifié" : "");

    // ---------------------------------------------------------- Annuler et Rétablir

    private bool _retoucheEnCours;

    /// <summary>
    /// Une transaction ouverte — un champ du dock qui a le focus, un geste sur la planche —
    /// a déjà changé le document, sans avoir encore empilé son pas : il ne l'empile qu'à sa
    /// fermeture, à la perte du focus. Le document est pourtant modifié, et Annuler le
    /// déferait (<c>EndPendingEdit</c>) : le bouton ne doit pas se griser entre les deux.
    /// Relevé à la première retouche du bloc choisi ou du document, et oublié à la
    /// fermeture de la transaction.
    /// </summary>
    public bool RetoucheEnCours
    {
        get => _retoucheEnCours;
        private set
        {
            if (!SetProperty(ref _retoucheEnCours, value)) return;
            OnPropertyChanged(nameof(PeutAnnuler));
            OnPropertyChanged(nameof(PeutRetablir));
            OnPropertyChanged(nameof(TitreDuDocument));
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    /// <summary>
    /// Le bouton Annuler de la barre principale : grisé quand l'historique est vide et que
    /// rien n'attend d'y entrer. Un bouton grisé ne prend ni le pointeur ni le focus — le
    /// clic ne refermerait même pas la saisie qu'il devait défaire.
    /// </summary>
    public bool PeutAnnuler => CanUndo || RetoucheEnCours;

    /// <summary>
    /// Le bouton Rétablir : une retouche en cours videra la pile de rétablissement dès
    /// qu'elle empilera son pas, ce que Rétablir ferait d'abord.
    /// </summary>
    public bool PeutRetablir => CanRedo && !RetoucheEnCours;

    /// <summary>
    /// Appelée à chaque retouche du document ou d'un bloc choisi. Une seule comparaison au
    /// document de référence par transaction, tant qu'elle n'a rien changé : une retouche
    /// qui écrit la valeur déjà là ne compte pas.
    /// </summary>
    private void RemarquerUneRetouche()
    {
        if (RetoucheEnCours || _editDepth == 0 || _editBaseline is null) return;
        if (DocumentSerializer.ToJson(Document) != _editBaseline.Json) RetoucheEnCours = true;
    }

    // -------------------------------------------------------- la pastille de la barre

    /// <summary>
    /// Ce que porte la sélection, dans la pastille qui ouvre la barre contextuelle :
    /// « Page » quand rien n'est pris — la barre garde sa hauteur sans sélection, et la
    /// planche ne bouge pas (règle 3) —, la nature du bloc quand il est seul, le nombre de
    /// blocs sinon. Un bloc de texte et un titre sont le même objet du modèle, que rien ne
    /// distingue après coup : la pastille dit « Bloc de texte » pour les deux.
    /// </summary>
    public string Pastille => Selection.Count switch
    {
        0 => "Page",
        1 => NatureDe(Selection[0]),
        _ => $"{Selection.Count} blocs",
    };

    /// <summary>La nature d'un bloc en toutes lettres, comme la pastille et l'outil la nomment.</summary>
    public static string NatureDe(Frame bloc) => bloc switch
    {
        TextFrame => "Bloc de texte",
        ImageFrame => "Image",
        TableFrame => "Tableau",
        GroupFrame => "Groupe",
        ShapeFrame { IsEllipse: true } => "Ellipse",
        ShapeFrame { IsLine: true } => "Trait",
        ShapeFrame => "Rectangle",
        _ => bloc.Kind,
    };

    // -------------------------------------------------------------- le menu Texte

    /// <summary>Un bloc seul qui porte du texte : texte, titre, ou tableau.</summary>
    public bool PorteDuTexte => SingleSelected is TextFrame or TableFrame;

    /// <summary>Un bloc de texte seul : l'alignement est le sien, un tableau aligne case par case.</summary>
    public bool EstUnBlocDeTexte => SingleSelected is TextFrame;

    /// <summary>
    /// Gras, <c>Ctrl+G</c> comme dans Publisher en français. Le bloc entier, faute d'une
    /// sélection dans le texte : c'est ce que fait la case du dock, en un pas d'annulation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PorteDuTexte))]
    private void BasculerGras() => Retoucher(texte => texte.Bold = !texte.Bold, tableau => tableau.Bold = !tableau.Bold,
        "Gras");

    /// <summary>Italique, <c>Ctrl+I</c> ; même règle que le gras.</summary>
    [RelayCommand(CanExecute = nameof(PorteDuTexte))]
    private void BasculerItalique() => Retoucher(texte => texte.Italic = !texte.Italic,
        tableau => tableau.Italic = !tableau.Italic, "Italique");

    /// <summary>Les quatre alignements du texte, au menu Texte.</summary>
    [RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]
    private void AlignerLeTexte(TextAlign alignement) =>
        Retoucher(texte => texte.Align = alignement, null, $"Texte aligné : {Libelles.De(alignement)}");

    private void Retoucher(Action<TextFrame> surTexte, Action<TableFrame>? surTableau, string message)
    {
        if (SingleSelected is TextFrame texte) UnGesteUnPas(() => surTexte(texte));
        else if (SingleSelected is TableFrame tableau && surTableau is not null) UnGesteUnPas(() => surTableau(tableau));
        else return;

        Status = message;
    }

    /// <summary>
    /// Un geste de la fenêtre — un raccourci, une entrée du menu Texte — fait son propre pas
    /// d'annulation, où que soit le focus (règle 2). Un raccourci joue sans retirer le
    /// focus au champ du dock, dont la transaction est ouverte : imbriqué dedans, le gras
    /// se fondrait dans le pas du champ, et une annulation déferait les deux. La saisie en
    /// cours est donc refermée d'abord — son pas avant celui du geste —, puis rouverte à la
    /// même profondeur : la suite de la frappe dans le même champ reste dans une
    /// transaction, que la perte du focus refermera.
    /// </summary>
    private void UnGesteUnPas(Action geste)
    {
        var ouvertes = _editDepth;
        if (ouvertes > 0) EndPendingEdit();

        BeginEdit();
        geste();
        CommitEdit();

        for (var i = 0; i < ouvertes; i++) BeginEdit();
    }

    // -------------------------------------------------------------- Fond et contour

    /// <summary>
    /// Le fond, en un seul choix comme dans la maquette : « Aucun », ou l'une des quatre
    /// sortes. Le modèle le porte en deux propriétés — la présence d'un fond et sa sorte —,
    /// qu'un seul contrôle règle désormais (règle 1 : un contrôle par propriété, et pas
    /// deux pour une seule chose à dire).
    /// </summary>
    public IReadOnlyList<OptionDeFond> SortesDeFond { get; } =
    [
        new(null, "Aucun"),
        new(FillKind.Solid, "Uni"),
        new(FillKind.Gradient, "Dégradé"),
        new(FillKind.Image, "Image"),
        new(FillKind.Tint, "Teinte"),
    ];

    /// <summary>Le contour, de même : « Aucun », ou l'un des trois traits.</summary>
    public IReadOnlyList<OptionDeContour> SortesDeContour { get; } =
    [
        new(null, "Aucun"),
        new(StrokeStyle.Solid, "Plein"),
        new(StrokeStyle.Dashed, "Tirets"),
        new(StrokeStyle.Dotted, "Points"),
    ];

    public OptionDeFond? FondDuBloc
    {
        get => SingleSelected is not { } bloc ? null
            : !bloc.HasFill ? SortesDeFond[0]
            : SortesDeFond[1 + (int)bloc.FillKind];
        set
        {
            // La liste renvoie null quand elle se vide — un bloc qui s'en va : ce n'est
            // pas un choix, et le bloc d'après ne doit pas perdre son fond.
            if (value is null || SingleSelected is not { } bloc || value == FondDuBloc) return;

            BeginEdit();
            if (value.Sorte is not { } sorte) bloc.HasFill = false;
            else
            {
                bloc.HasFill = true;
                bloc.FillKind = sorte;
            }

            CommitEdit();
        }
    }

    public OptionDeContour? ContourDuBloc
    {
        get => SingleSelected is not { } bloc ? null
            : !bloc.HasStroke ? SortesDeContour[0]
            : SortesDeContour[1 + (int)bloc.StrokeStyle];
        set
        {
            if (value is null || SingleSelected is not { } bloc || value == ContourDuBloc) return;

            BeginEdit();
            if (value.Trait is not { } trait) bloc.HasStroke = false;
            else
            {
                bloc.HasStroke = true;
                bloc.StrokeStyle = trait;
            }

            CommitEdit();
        }
    }

    // --------------------------------------------------------- les marges intérieures

    /// <summary>
    /// Le dock montre un champ quand les quatre marges intérieures du bloc sont égales,
    /// quatre sinon : le modèle garde ses quatre marges (C6), la vue n'en montre qu'une
    /// tant qu'une suffit à les dire. Décidé à la sélection, et non à chaque frappe : un
    /// champ qui se change en quatre sous le curseur perdrait le focus, et avec lui la
    /// fermeture de son pas d'annulation.
    /// </summary>
    public bool MargesInterieuresEgales { get; private set; }

    public bool MargesInterieuresDistinctes => !MargesInterieuresEgales;

    /// <summary>La marge commune, écrite sur les quatre côtés à la fois.</summary>
    public double MargeInterieure
    {
        get => (SingleSelected as TextFrame)?.PaddingLeft ?? 0;
        set
        {
            if (SingleSelected is not TextFrame bloc) return;
            if (bloc.PaddingLeft == value && bloc.PaddingRight == value &&
                bloc.PaddingTop == value && bloc.PaddingBottom == value) return;

            BeginEdit();
            bloc.PaddingLeft = value;
            bloc.PaddingRight = value;
            bloc.PaddingTop = value;
            bloc.PaddingBottom = value;
            CommitEdit();
        }
    }

    // ----------------------------------------------- sections repliables du dock

    // Ouvertes par défaut, et retenues le temps de la session : les sections d'un type de
    // bloc se recréent à chaque sélection, et c'est ici qu'elles retrouvent l'état où on
    // les a laissées. Une propriété par section, et non un dictionnaire : la liaison
    // compilée les vérifie à la compilation, et une faute de frappe dans un nom de section
    // ne passerait pas pour une section neuve.
    [ObservableProperty] private bool _sectionPositionOuverte = true;
    [ObservableProperty] private bool _sectionCadreOuverte = true;
    [ObservableProperty] private bool _sectionCaractereOuverte = true;
    [ObservableProperty] private bool _sectionParagrapheOuverte = true;
    [ObservableProperty] private bool _sectionContenuOuverte = true;
    [ObservableProperty] private bool _sectionTableauOuverte = true;
    [ObservableProperty] private bool _sectionImageOuverte = true;
    [ObservableProperty] private bool _sectionGroupeOuverte = true;
    [ObservableProperty] private bool _sectionFondOuverte = true;
    [ObservableProperty] private bool _sectionFormatOuverte = true;
    [ObservableProperty] private bool _sectionMargesOuverte = true;
    [ObservableProperty] private bool _sectionGrilleOuverte = true;
    [ObservableProperty] private bool _sectionReperesOuverte = true;
    [ObservableProperty] private bool _sectionFondDePageOuverte = true;

    // ------------------------------------------------------------ suivre la sélection

    /// <summary>Le bloc dont on suit les retouches pour le fond, le contour et la marge.</summary>
    private Frame? _blocSuivi;

    /// <summary>
    /// Appelée par <see cref="NotifySelection"/> : ce que la disposition tire de la
    /// sélection. Le fond, le contour et la marge commune se relisent aussi quand le bloc
    /// change sous le dock — une annulation, la frappe sur la page, un autre champ.
    /// </summary>
    private void NotifierLaDisposition()
    {
        if (_blocSuivi is not null) _blocSuivi.PropertyChanged -= SurBlocRetouche;
        _blocSuivi = SingleSelected;
        if (_blocSuivi is not null) _blocSuivi.PropertyChanged += SurBlocRetouche;

        MargesInterieuresEgales = SingleSelected is TextFrame t &&
                                  t.PaddingLeft == t.PaddingRight && t.PaddingLeft == t.PaddingTop &&
                                  t.PaddingLeft == t.PaddingBottom;

        OnPropertyChanged(nameof(Pastille));
        OnPropertyChanged(nameof(PorteDuTexte));
        OnPropertyChanged(nameof(EstUnBlocDeTexte));
        OnPropertyChanged(nameof(MargesInterieuresEgales));
        OnPropertyChanged(nameof(MargesInterieuresDistinctes));
        OnPropertyChanged(nameof(MargeInterieure));
        OnPropertyChanged(nameof(FondDuBloc));
        OnPropertyChanged(nameof(ContourDuBloc));
        BasculerGrasCommand.NotifyCanExecuteChanged();
        BasculerItaliqueCommand.NotifyCanExecuteChanged();
        AlignerLeTexteCommand.NotifyCanExecuteChanged();
    }

    private void SurBlocRetouche(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Frame.Fill) or nameof(Frame.HasFill) or nameof(Frame.FillKind):
                OnPropertyChanged(nameof(FondDuBloc));
                break;
            case nameof(Frame.Stroke) or nameof(Frame.HasStroke) or nameof(Frame.StrokeStyle):
                OnPropertyChanged(nameof(ContourDuBloc));
                break;
            case nameof(TextFrame.PaddingLeft):
                OnPropertyChanged(nameof(MargeInterieure));
                break;
        }
    }
}

/// <summary>Une entrée de la liste « Fond » : une sorte de remplissage, ou aucune.</summary>
public sealed record OptionDeFond(FillKind? Sorte, string Libelle);

/// <summary>Une entrée de la liste « Contour » : un trait, ou aucun.</summary>
public sealed record OptionDeContour(StrokeStyle? Trait, string Libelle);
