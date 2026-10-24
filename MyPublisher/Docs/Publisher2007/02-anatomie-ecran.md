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
  l'indicateur de page ; chaque entrée est un contrôle à part (`EntreeDePage.axaml`) : la
  vignette de la page, de 96 px de large au rapport de la page, la page courante bordée de
  2 px d'accent, et son numéro dessous (poste I5). La vignette est un schéma tiré de la
  composition (`VignetteDePage`, dérivée de la vignette des modèles), et suit les
  retouches en place, bloc par bloc.
- « Blocs de la page », au pied de l'onglet Propriétés du dock, **l'avant en tête** —
  Publisher n'a pas cet équivalent, c'est un avantage net.
- Barre d'état : message, position du curseur en mm (`CursorLabel`), position et taille du
  bloc (A6), « Page 1 sur 3 », format, zoom et son curseur.
- Règles graduées et repères libres depuis C2b ; la planche est refaite par le poste I4.

## Écart / ce qu'il faudrait

*Repris le 25 octobre 2026, après la relecture du poste I2.* Deux des trois écarts que la
fiche relevait sont refermés : les règles graduées sur les deux bords, d'où l'on tire un
repère, depuis C2b — voir [06-reperes-regles.md](06-reperes-regles.md) — ; la taille du
bloc dans la barre d'état, à côté de sa position, suivie pendant le geste et non à sa
fin, depuis A6. Le tableau est au rail depuis C9. Restent :

1. **Le trait et la flèche**, absents du rail. Un filet de séparation est un besoin de
   PAO élémentaire ; c'est le poste A10, au lot 2 de la phase I. Le bouton Ligne, que la
   maquette range au rail, n'y apparaîtra qu'avec son geste : jusque-là, l'import d'un
   `.pub` simule le trait par un rectangle.
2. **La main et la loupe**, que la maquette range aussi au rail : le poste I6.
3. **Les formes automatiques** (AutoShapes) : ni planifiées, ni demandées par les deux
   documents réels.
