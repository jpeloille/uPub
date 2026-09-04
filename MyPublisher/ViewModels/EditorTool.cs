namespace MyPublisher.ViewModels;

/// <summary>
/// Outil armé dans la barre latérale. <see cref="Select"/> manipule les blocs
/// existants ; les autres en créent un nouveau, tracé directement sur la page.
/// L'insertion d'image n'est pas un outil : elle passe par un sélecteur de fichier
/// et ne se trace donc pas.
/// </summary>
public enum EditorTool
{
    Select,
    Text,
    Heading,
    Rectangle,
    Ellipse
}
