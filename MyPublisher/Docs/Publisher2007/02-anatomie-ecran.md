# 02 — Anatomie de l'écran et palette d'objets

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 8 et 33.
**Verdict** : `partiel`

## Ce que fait Publisher 2007

L'écran se décompose en : barre de menus, barres d'outils horizontales, **volet Office**
à gauche, **barre d'objets** verticale sur le bord gauche, règles graduées sur les deux
bords, **indicateur de page** en bas à gauche (onglets numérotés), et barre d'état.

## Détail de l'IHM

- **Barre d'objets** (p. 33) : une palette verticale d'une douzaine d'icônes. La
  capture la montre mais **les icônes n'y portent pas de libellé** — le livre n'en
  nomme explicitement que la zone de texte (p. 33), le WordArt (p. 47) et l'objet de
  bibliothèque de conception (p. 56). L'inventaire habituel de Publisher 2007
  comprend en outre sélection, cadre d'image, trait, flèche, ovale, rectangle,
  tableau et AutoShapes ; *cette partie de la liste vient de l'interface Office 2007,
  pas d'un relevé de la capture.*
- **Barre d'état**, à droite (visible p. 33 et p. 43) : la position du bloc
  (« 2.000, 2.063 in. ») **et** sa taille (« 4.438 x 3.000 in. ») sont affichées en
  permanence, séparément.
- **Indicateur de page** : onglets numérotés cliquables, la page courante en surbrillance.
- Les barres d'outils sont contextuelles : la barre *Picture* n'apparaît qu'à la
  sélection d'une image (p. 43), la barre *WordArt* qu'à celle d'un WordArt (p. 49).

## État dans MyPublisher

*Repris le 24 octobre 2026, à la refonte de la fenêtre (phase I, poste I2).* La
disposition est celle de la maquette de `Maquettes/` : barre de menus, barre principale à
icônes, barre contextuelle, puis, de gauche à droite, le rail d'outils, le panneau Pages,
la planche et le dock à quatre onglets ; la barre d'état au pied.

- Panneau Pages (`Views/MainWindow.axaml`, `PanneauPages`) — équivalent plus lisible de
  l'indicateur de page ; chaque entrée est un contrôle à part (`EntreeDePage.axaml`), qui
  deviendra une vignette (poste I5).
- « Blocs de la page », au pied de l'onglet Propriétés du dock, **l'avant en tête** —
  Publisher n'a pas cet équivalent, c'est un avantage net.
- Barre d'état : message, position du curseur en mm (`CursorLabel`), position et taille du
  bloc (A6), « Page 1 sur 3 », format, zoom et son curseur.
- Règles graduées et repères libres depuis C2b ; la planche est refaite par le poste I4.

## Écart / ce qu'il faudrait

1. **Règles sur les deux bords.** Utile en soi, mais surtout prérequis du geste
   « glisser un repère depuis la règle » — voir [06-reperes-regles.md](06-reperes-regles.md).
2. **Taille du bloc dans la barre d'état.** Aujourd'hui elle n'est lisible que dans le
   panneau de droite ; pendant un redimensionnement, l'œil est sur la page.
   `CursorLabel` est le point d'accroche évident.
3. **Objets absents de la palette** : trait, flèche, tableau, AutoShapes. Le trait est
   le plus criant — un filet de séparation est un besoin de PAO élémentaire, et il ne
   demande qu'un `ShapeKind.Line` dans `Models/Frame.cs:11`, avec le tracé
   correspondant dans `PdfExporter.DrawShape` (`Services/PdfExporter.cs:74`).
