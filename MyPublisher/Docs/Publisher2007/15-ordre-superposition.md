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
  (`ViewModels/MainWindowViewModel.cs`), ramenées à `MoveToEnd` et `StepOrder`, qui
  bornent le rang — pas de cas limite à la première ni à la dernière place — et reposent
  la sélection que le remaniement fait tomber.
- Exposées au menu Disposition (`Views/MainWindow.axaml`) ; premier plan et arrière-plan
  aussi dans la barre principale, au groupe « Ordre » (phase I, poste I2).
- **Raccourcis** sur les deux extrêmes, comme Publisher : `Ctrl+Maj+F` et `Ctrl+Maj+B`
  (poste A4), et non `Alt+F6`, que la plupart des gestionnaires de fenêtres Linux
  captent. Avancer et reculer d'un rang n'en ont pas, comme chez lui.
- Désactivées sans sélection (`CanExecute = nameof(HasSelection)`).
- **La pile est visible** : « Blocs de la page », au pied du dock, montre l'ordre réel,
  **l'avant en tête** depuis le poste I2 — une vue inversée (`ViewModels/PileInversee.cs`)
  de la collection `Page.Frames`, dont le commentaire pose la convention — « le dernier
  est au premier plan » (`Models/Document.cs`).
- **La pile se réordonne en y tirant un bloc** (poste A4) : un trait d'insertion suit le
  pointeur, et le dépôt fait un seul pas d'annulation (`Views/MainWindow.axaml.cs`, le
  rang de dépôt et son miroir dans la page depuis que la liste est à l'envers).
- L'ordre est respecté partout : au test de survol, qui parcourt la pile en sens inverse
  (`LastOrDefault`, `Views/PageCanvas.axaml.cs`), au rendu, et à l'export
  (`Services/PdfExporter.cs`).

## Écart / ce qu'il faudrait

*Repris le 25 octobre 2026, après la relecture du poste I2.* Les deux finitions que la
fiche demandait — les raccourcis et le glisser-déposer dans la liste — sont faites depuis
A4. Il reste un défaut consigné, sans rien de structurel : la liste ne défile pas pendant
le glissé, si bien qu'on ne dépose un bloc qu'entre les lignes qu'elle montre (Q11).
