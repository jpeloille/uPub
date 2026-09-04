# 14 — Insertion d'image et barre d'outils Image

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 41–43.
**Verdict** : `partiel`

## Ce que fait Publisher 2007

`Insert/Picture/From File` ouvre un sélecteur ; l'image arrive dans un cadre. Dès
qu'elle est sélectionnée, une **barre d'outils Picture flottante** apparaît près d'elle
et regroupe toutes les retouches.

## Détail de l'IHM

La capture p. 43 montre la barre **Picture** flottante — une bande d'une quinzaine
d'icônes, **sans libellé lisible** ; le livre se contente de « Use the Picture Toolbar
to format the picture » et n'en détaille aucun bouton.

*Rappel de son contenu d'après l'interface Office 2007, à titre indicatif et non
comme relevé de la capture* : insérer une image, couleur (automatique / nuances de
gris / noir et blanc / filigrane), contraste + et −, luminosité + et −, rogner,
faire pivoter, style de trait, compresser les images, habillage du texte, format de
l'image, définir une couleur transparente, réinitialiser l'image.

Deux caractéristiques d'interaction méritent d'être relevées : la barre est
**contextuelle** (elle n'existe que pour une image sélectionnée) et **flottante**
(elle vient à l'objet).

## État dans MyPublisher

Solide sur les fondamentaux, nu sur la retouche :

- `ImageFrame` porte un chemin et un mode de cadrage sur trois valeurs — Uniform,
  UniformToFill, Fill (`Models/Frame.cs:9` et `:187-200`).
- `AddImage` lit les dimensions natives du fichier pour poser le bloc **aux bonnes
  proportions** dès l'insertion (`ViewModels/MainWindowViewModel.cs:431-455`), avec
  repli propre si le format est illisible.
- L'export respecte les trois modes et **rogne au bloc** en mode UniformToFill, par
  `IntersectClip` (`Services/PdfExporter.cs:156-165`), la géométrie étant calculée par
  `FitRect` (`:163-180`).
- Une image absente ne fait pas échouer l'export : elle produit un avertissement
  (`:138-142`).
- À l'écran, un cadre en pointillés matérialise le bloc tant qu'aucune image n'est
  chargée (`Views/PageCanvas.axaml:95`).
- Chemin **absolu**, image liée et non embarquée (README) —
  voir [21-pack-and-go.md](21-pack-and-go.md).

## Écart / ce qu'il faudrait

1. **Rognage manuel** — choisir la zone de l'image à montrer, indépendamment du cadre.
   Le plus utile de la liste, et à moitié fait : l'export sait déjà découper
   (`IntersectClip`). Il manque un rectangle source dans `ImageFrame` et le geste de
   poignées correspondant.
2. **Rotation par pas de 90°** — la rotation libre existe (`Models/Frame.cs:44`) ; un
   bouton « +90° » couvre le cas courant du cliché en portrait.
3. **Habillage du texte** — voir [22-fonctions-hors-sommaire.md](22-fonctions-hors-sommaire.md).
4. **Luminosité, contraste, nuances de gris, couleur transparente** — traitement pixel,
   coûteux à faire proprement et à exporter. Faible priorité pour de la PAO : ces
   réglages relèvent de l'éditeur d'images en amont.
5. **Barre contextuelle flottante** — le panneau de propriétés joue déjà ce rôle
   (`Views/MainWindow.axaml:297` et suivants, qui commute sur le type du bloc). Ne pas
   dupliquer.
