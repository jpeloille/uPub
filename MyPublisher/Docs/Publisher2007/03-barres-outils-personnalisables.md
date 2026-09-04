# 03 — Barres d'outils personnalisables

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 9–13.
**Verdict** : `hors périmètre`

## Ce que fait Publisher 2007

`View/Toolbars` affiche ou masque les barres d'outils. La flèche en bout de barre ouvre
« Add or Remove Buttons » pour choisir les boutons visibles, bouton par bouton. Enfin
`View/Toolbars/Customize` permet de créer une barre nommée (« Favorites ») et d'y
glisser-déposer n'importe quelle commande de l'application.

## État dans MyPublisher

La barre d'outils est fixe : cinq boutons d'outil de tracé puis des actions immédiates
sur le bloc sélectionné (`Views/MainWindow.axaml`, barre latérale).

## Écart / ce qu'il faudrait

Rien. Cette personnalisation est un héritage des interfaces Office d'avant le ruban,
et Microsoft l'a lui-même abandonnée dans Publisher 2010. Une barre courte et pensée
vaut mieux qu'une barre configurable : c'est déjà le parti pris du projet, et le README
l'assume explicitement (icônes tracées sur une grille commune de 24 unités pour que
l'ensemble « fasse système »).

Ce qui mérite en revanche d'être repris de ces pages : **l'idée de barres
contextuelles**, qui apparaissent selon le type d'objet sélectionné. MyPublisher le fait
déjà, mais dans le panneau de propriétés (`MainWindow.axaml:297` et suivants, via un
`ContentControl` qui commute sur le type du bloc). C'est la même idée, mieux placée.
