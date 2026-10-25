namespace MyPublisher.ViewModels;

/// <summary>
/// Outil armé dans la barre latérale. <see cref="Select"/> manipule les blocs
/// existants ; les autres en créent un nouveau, tracé directement sur la page.
/// L'insertion d'image n'est pas un outil : elle passe par un sélecteur de fichier
/// et ne se trace donc pas.
///
/// <see cref="Main"/> et <see cref="Zoom"/> (poste I6) ne créent rien ni ne touchent aux
/// blocs : ils naviguent dans la planche. Ce ne sont pas des outils de tracé
/// (<see cref="MainWindowViewModel.IsDrawing"/>), mais ils s'arment et se désarment
/// comme eux, au rail et à <c>Échap</c>.
/// </summary>
public enum EditorTool
{
    Select,
    Text,
    Heading,
    Rectangle,
    Ellipse,
    /// <summary>Le trait (poste A10) : il se trace d'une extrémité à l'autre, et non en cadre.</summary>
    Line,
    Table,
    Main,
    Zoom
}
