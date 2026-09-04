# 13 — Bibliothèque d'images (Clip Art)

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 39–40.
**Verdict** : `hors périmètre`

## Ce que fait Publisher 2007

`Insert/Picture/Clip Art` transforme le volet Office en moteur de recherche d'images :
on tape un mot-clé, on parcourt les vignettes, on clique — l'image arrive sur la page,
où elle se manipule comme n'importe quel bloc (« you can make it smaller and rotate it
just like a text box »).

## Détail de l'IHM

Volet **Clip Art** :
- `Search for:` — le mot-clé, avec un bouton **Go**.
- `Search in:` — les collections à interroger (locales et *Office Online*).
- `Results should be:` — filtre par type de média.
- Grille de résultats ; un clic insère.
- En pied de volet : *Organize clips…*, *Clip art on Office Online*,
  *Tips for finding clips*.

## État dans MyPublisher

Aucune bibliothèque. L'insertion passe exclusivement par un sélecteur de fichier
(`OnInsertImage`, `Views/MainWindow.axaml:133` → `AddImage`,
`ViewModels/MainWindowViewModel.cs:431`).

## Écart / ce qu'il faudrait

La partie en ligne est hors périmètre : elle suppose un service tiers, un catalogue à
maintenir et des questions de licence — pour un logiciel de PAO local, c'est une
dépendance disproportionnée.

L'idée locale, en revanche, reste bonne et peu coûteuse : **un dossier de ressources
indexé**, avec recherche par nom de fichier, présenté en vignettes. Cela rendrait
service à quiconque réutilise le même fonds d'images d'un document à l'autre — un
logo, des pictogrammes, un bandeau.

À rapprocher de la bibliothèque de blocs proposée en
[20-bibliotheque-conception.md](20-bibliotheque-conception.md) : même panneau, deux
contenus. Ce serait la bonne façon de les traiter ensemble plutôt que deux fois.
