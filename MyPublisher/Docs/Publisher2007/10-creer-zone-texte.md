# 10 — Créer une zone de texte

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 33.
**Verdict** : `fait`

## Ce que fait Publisher 2007

La page s'ouvre sur cette phrase, qui résume toute la philosophie de l'outil :

> « You cannot type in Publisher until you create a text box. »

Rien ne s'écrit sur la page ; tout vit dans un bloc. C'est exactement le parti pris de
MyPublisher.

## Détail de l'IHM

1. Cliquer l'outil *Text Box* dans la barre d'objets.
2. Le curseur devient une croix noire pleine.
3. Glisser sur la page pour tracer le cadre à la taille voulue.
4. Relâcher : un cadre clair apparaît — et **il ne s'imprime pas**.

Deux gestes signalés en encadré :
- **rotation** par le point vert situé au-dessus du bloc, le curseur devenant une
  flèche circulaire ;
- **redimensionnement** par les poignées, curseur en double flèche.

## État dans MyPublisher

Tout y est, et le geste est même mieux fini que dans Publisher :

- Outil armé (`ViewModels/EditorTool.cs`), curseur croix (`Views/PageCanvas.axaml.cs:54`).
- Tracé direct sur la page (`DrawWithActiveTool`, `MainWindowViewModel.cs:407`).
- **Un clic sans glisser pose le bloc à sa taille par défaut** au lieu de créer un
  cadre dégénéré (`MinDrawnSize`, `:355` et `:413`) — Publisher ne fait pas ça.
- **L'outil se désarme après usage** (`:425`), pour qu'on puisse ajuster immédiatement
  ce qu'on vient de poser.
- Huit poignées de redimensionnement (`Views/PageCanvas.axaml.cs:57`), curseur adapté
  au coin saisi (`EdgeCursors`, `:43-49`).
- Le rectangle de marge est un repère d'écran uniquement : il est tracé dans
  `PageCanvas.axaml:47-52` et absent de `PdfExporter` — donc non imprimé, comme chez
  Publisher.
- Édition du texte au double-clic, directement sur la page.

## Écart / ce qu'il faudrait

Un seul manque, mais c'est le geste le plus attendu : **la poignée de rotation**.

La rotation existe bien dans le modèle (`Models/Frame.cs:44`), elle est prise en compte
par le test de survol (`HitTest`/`ToLocal`, `:60-78`), par le redimensionnement
(`ApplyResize`, `PageCanvas.axaml.cs:475`) et par l'export
(`Services/PdfExporter.cs:44-49`). Mais on ne peut la régler qu'au clavier, par le champ
numérique du panneau de propriétés (`Views/MainWindow.axaml:286`).

Ajouter une neuvième poignée au-dessus du bloc réutiliserait toute cette mécanique :
`_handles` est un tableau (`PageCanvas.axaml.cs:57`), `DragMode` une énumération à
étendre (`:31`), et l'angle se déduit de l'écart entre le centre du bloc et le pointeur.
C'est le meilleur rapport valeur/effort de tout ce corpus.
