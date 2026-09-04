# 07 — Zone de travail hors page (« side bar »)

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 21.
**Verdict** : `manquant`

## Ce que fait Publisher 2007

La zone grise autour de la page n'est pas un décor : c'est une **surface de travail**.
On y dépose des éléments — le livre prend l'exemple d'un logo — et ils restent
accessibles depuis **toutes les pages** du document. Ils ne s'impriment pas.

Le livre donne la règle exacte, avec un avertissement en gros : l'élément doit être
**entièrement** hors de la page. S'il « touche le blanc », il appartient à la page
courante et cesse d'être disponible partout ailleurs.

C'est une primitive de PAO à part entière : brouillon, réserve d'éléments, et
réutilisation inter-pages, le tout sans boîte de dialogue.

## État dans MyPublisher

L'inverse, par construction :

- La planche est un `Border` blanc exactement à la taille de la page, et `PageRoot`
  a `ClipToBounds="True"` (`Views/PageCanvas.axaml:30-44`) — un bloc hors page est
  invisible.
- La couche de saisie couvre la page seule (`InputLayer`, `:130`) : rien n'est
  cliquable au-delà.
- `NextInsertPoint` ramène de force tout bloc inséré à l'intérieur de la page
  (`ViewModels/MainWindowViewModel.cs:480-483`).
- Les blocs appartiennent à une `Page` (`Models/Document.cs:12`), pas au document.

## Écart / ce qu'il faudrait

Trois changements, dans cet ordre de difficulté croissante :

1. **Agrandir la surface** : étendre `Stage` au-delà de la page et retirer le rognage.
   La page reste dessinée en blanc avec son ombre, le reste devient utilisable.
2. **Ne pas exporter hors page** : `PdfExporter` (`Services/PdfExporter.cs:41`) doit
   ignorer les blocs entièrement hors du rectangle de page. Un test de recouvrement.
3. **Stocker hors page au niveau document** : une collection `Frames` sur `Document`
   à côté de `Pages`, rendue sur toutes les pages. C'est le vrai coût, car le rendu et
   l'export itèrent aujourd'hui sur `CurrentPage.Frames` uniquement.

Alternative moins coûteuse et sans doute plus moderne : une **bibliothèque de blocs
nommés** — on sélectionne, on enregistre sous un nom, on repose ailleurs. C'est ce que
Publisher lui-même a fini par ajouter sous le nom *Content Library* (visible au menu
Insert, p. 22). Voir [20-bibliotheque-conception.md](20-bibliotheque-conception.md).
