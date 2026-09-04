# 15 — Ordre de superposition

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 44–45.
**Verdict** : `fait`

## Ce que fait Publisher 2007

On sélectionne un objet, puis `Arrange/Order ▸` pour le faire monter ou descendre dans
la pile.

## Détail de l'IHM

Sous-menu **Order** :
- **Bring to Front** — `Alt+F6`
- **Send to Back** — `Alt+Shift+F6`
- **Bring Forward**
- **Send Backward**

Les deux premières seulement ont un raccourci. Aucun panneau ne montre la pile : on
travaille à l'aveugle, en essayant.

## État dans MyPublisher

Les quatre commandes existent, et l'ergonomie est meilleure que celle du modèle :

- `BringToFront`, `SendToBack`, `BringForward`, `SendBackward`
  (`ViewModels/MainWindowViewModel.cs:516-526`), toutes ramenées à un unique `Reorder`
  qui borne l'index (`:531-544`) — pas de cas limite à la première ni à la dernière
  place.
- Exposées au menu Disposition (`Views/MainWindow.axaml:140-143`) et dans la barre
  d'outils latérale.
- Désactivées automatiquement sans sélection (`CanExecute = nameof(HasSelection)`,
  déclaré en `:45-48`).
- **La pile est visible** : le panneau « Blocs (du fond vers l'avant) »
  (`Views/MainWindow.axaml:213-222`) montre l'ordre réel, alimenté par la collection
  `Page.Frames` dont le commentaire pose la convention — « le dernier est au premier
  plan » (`Models/Document.cs:11`).
- L'ordre est respecté partout : au test de survol, qui parcourt en sens inverse
  (`LastOrDefault`, `Views/PageCanvas.axaml.cs:285`), au rendu, et à l'export
  (`Services/PdfExporter.cs:41`).

## Écart / ce qu'il faudrait

Deux finitions, aucune structurelle :

1. **Raccourcis clavier.** Aucun `KeyBinding` n'est déclaré pour ces quatre commandes
   (`Views/MainWindow.axaml:22-32`). Ce sont pourtant des gestes répétés.
2. **Réordonner par glisser-déposer** dans la liste des blocs. La liste existe et
   affiche déjà le bon ordre ; c'est le geste que l'utilisateur va spontanément
   essayer en la voyant.
