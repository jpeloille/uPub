# 22 — Fonctions visibles dans les captures, absentes du sommaire

**Source** : captures du *Student Guide*, p. 18, 20, 22, 33, 34, 43, 44, 46, 61.
**Verdict** : `mixte` — voir chaque ligne.

Le livre est un guide de découverte : il ne documente qu'une fraction des menus qu'il
photographie. Or plusieurs commandes visibles dans ces captures pèsent plus lourd, pour
notre sujet, que celles qu'il explique. Cette fiche les recense.

## Menu Arrange (p. 18, 20, 44, 46)

Contenu du menu, relevé sur les captures p. 18, 20, 44 et 46 : *Layout Guides…*,
*Ruler Guides ▸*, *Send to Master Page*, *Group* `Ctrl+Shift+G`, *Ungroup*,
*Regroup*, *Order ▸*, *Snap ▸*, *Nudge ▸*, *Align or Distribute ▸*, *Rotate or Flip ▸*,
*Remove Connectors*, *Text Wrapping ▸*, *Edit Points*, *Change AutoShape ▸*.

**Précision de sourcing.** Ces intitulés de premier niveau sont bien lisibles sur les
captures. En revanche, **seuls deux sous-menus y sont déroulés** : *Ruler Guides* (p. 20)
et *Order* (p. 44). Les contenus des autres sous-menus donnés ci-dessous — Snap, Align
or Distribute, Rotate or Flip, Text Wrapping — proviennent de l'interface Office 2007
et non d'un relevé du livre. Ils sont donnés pour cadrer le besoin, pas comme
référence documentaire. La colonne « État MyPublisher », elle, est vérifiée dans le
code.

| Commande | État MyPublisher |
|---|---|
| **Send to Master Page** — pages maîtres | `manquant`. Listé dans les limites connues du README. Un contenu répété sur toutes les pages (filet, folio, logo). Se ramène à une `Page` marquée « maître », rendue sous chaque page et non modifiable depuis celles-ci. |
| **Snap ▸** — aimanter aux repères / aux objets / aux repères de règle, activable | `partiel`. L'aimantation est **toujours active** et ne se désactive qu'en maintenant `Alt` pendant le geste (`Views/PageCanvas.axaml.cs:427-429`). Un réglage persistant, et surtout l'aimantation **aux autres blocs** (aujourd'hui absente : `SnapEdge` ne reçoit que des repères de page, `:434-441`), sont les deux vrais manques. |
| **Nudge ▸** — déplacement fin | `fait`. Flèches à 3 pt, `Maj` à 0,5 pt (`NudgeSelection`, `ViewModels/MainWindowViewModel.cs:145-154`). |
| **Align or Distribute ▸** — aligner gauche/centre/droite, haut/milieu/bas ; répartir horizontalement/verticalement ; *Relative to Margin Guides* | `partiel`. Seuls le centrage horizontal et vertical existent (`MainWindowViewModel.cs:547-563`). **Incohérence à trancher** : ces deux commandes centrent par rapport à la **page** (`Document.PageWidth`), alors que `FitToMargins` juste en dessous (`:565-575`) travaille par rapport aux **marges**. Publisher, lui, propose explicitement le choix. La répartition suppose la sélection multiple ([16](16-groupement.md)). |
| **Rotate or Flip ▸** — rotation par pas de 90°, miroir horizontal / vertical | `partiel`. La rotation libre au degré près existe (`Models/Frame.cs:44`) et va plus loin que Publisher. Manquent le raccourci « +90° » et surtout le **miroir**, qui n'est pas une rotation et n'a aucun équivalent dans le modèle. |
| **Text Wrapping ▸** — habillage : carré, rapproché, au travers, haut et bas, aucun | `manquant`. **C'est le manque le plus visible d'une mise en page à blocs** : aujourd'hui, un texte et une image qui se recouvrent se recouvrent, point. Suppose un moteur de composition capable d'exclure des zones — donc le même chantier que le chaînage ([12](12-ajustement-texte.md)). À décider avec lui, pas séparément. |
| *Remove Connectors*, *Edit Points*, *Change AutoShape* | `hors périmètre`. Relèvent du dessin vectoriel, pas de la mise en page. |

## Menu Insert (p. 22)

*Page…*, *Duplicate Page*, *Section…*, *Page Numbers…*, *Date and Time…*, *Symbol…*,
*Design Gallery Object…*, *Add to Content Library…*, *Item from Content Library…*,
*Business Information…*, *Picture ▸*, *Text Box*, *Text File…*, *Object…*,
*Hyperlink…*, *Bookmark…*

- **Page Numbers…** et **Date and Time…** — `manquant`, traités en
  [08](08-insertion-pages.md) et [19](19-informations-professionnelles.md) : même
  mécanisme de champ.
- **Text File…** — importer un fichier texte dans un bloc. `manquant`, et peu coûteux :
  un sélecteur de fichier qui remplit `TextFrame.Text`. C'est le chemin naturel pour
  faire entrer du contenu rédigé ailleurs.
- **Content Library** — voir [20](20-bibliotheque-conception.md).
- **Symbol…**, **Object…**, **Hyperlink…**, **Bookmark…** — `hors périmètre` pour un
  export destiné à l'impression.

## Menu File (p. 61)

*Import Word Document…*, *Publish to the Web…*, **Publish as PDF or XPS…** (`fait` —
c'est `Ctrl+E`), *Convert to Web Publication…*, **Page Setup…**, *Print Setup…*,
**Print Preview**, **Print…**

- **Print…** — `manquant`, et c'est notable : MyPublisher exporte un PDF mais
  **n'imprime pas**. Pour un logiciel de PAO, c'est un trou dans le parcours. Le PDF
  produit peut servir d'intermédiaire, mais l'utilisateur ne devrait pas avoir à le
  savoir.
- **Print Preview** — `manquant`. Moins critique : le rendu écran est déjà fidèle,
  puisque écran et export partagent exactement le même modèle en points.

## Barre d'état (p. 33, 43)

Publisher affiche en permanence **la position et la taille** du bloc sélectionné, dans
deux zones distinctes. MyPublisher n'affiche que la position du curseur
(`CursorLabel`, `ViewModels/MainWindowViewModel.cs:179-180`). Voir
[02-anatomie-ecran.md](02-anatomie-ecran.md).
