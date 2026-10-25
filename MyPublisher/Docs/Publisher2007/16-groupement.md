# 16 — Groupement et dissociation

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 46.
**Verdict** : `manquant`

## Ce que fait Publisher 2007

> « Grouping objects makes the object into one object. Use this if you always want
> captions to stay with graphics, etc. »

Le cas d'usage donné par le livre — la légende qui doit suivre l'image — est exactement
celui qu'on rencontre dès le premier document sérieux.

## Détail de l'IHM

1. **Ctrl + clic gauche** pour sélectionner plusieurs objets.
2. `Arrange/Group` — `Ctrl+Shift+G`.
3. `Arrange/Ungroup` pour dissocier — la capture lui donne la même touche, `Ctrl+Shift+G`
   (p. 44, 46) —, `Arrange/Regroup` pour reformer le dernier groupe. *Group* est grisé tant
   qu'un seul objet est sélectionné.

Le groupe se déplace, se redimensionne et se tourne comme un objet unique. Le livre
signale p. 58 la conséquence pratique : pour retoucher une partie d'un élément de la
bibliothèque de conception, il faut d'abord le dissocier, puis le regrouper.

## État dans MyPublisher

Absent, et l'obstacle est en amont : **la sélection est unique**. `Selected` est un
`Frame?` (`ViewModels/MainWindowViewModel.cs:52`), consommé partout — les commandes
de disposition, le panneau de propriétés (`SelectedText`, `SelectedShape`,
`SelectedImage`, `:170-172`), les poignées (`Views/PageCanvas.axaml.cs:186`).

## Écart / ce qu'il faudrait

Deux étages, et l'ordre compte :

**1. Sélection multiple d'abord.** `Selected` devient une collection ; les commandes de
disposition itèrent ; les poignées encadrent l'union des rectangles ; le panneau de
propriétés montre soit le bloc unique, soit ce qui est commun. C'est un remaniement
réel mais circonscrit, et il débloque à lui seul :
- le déplacement d'un ensemble d'un seul geste,
- la suppression et la duplication groupées,
- **l'alignement et la répartition** (voir
  [22-fonctions-hors-sommaire.md](22-fonctions-hors-sommaire.md)), aujourd'hui
  impossibles faute d'avoir deux blocs sélectionnés à la fois.

**2. Groupe persistant ensuite.** Un `GroupFrame : Frame` contenant des enfants en
coordonnées relatives. Le coût est ailleurs que dans le modèle : rendu et export
deviennent **récursifs**. Aujourd'hui les deux boucles sont plates —
`Services/PdfExporter.cs:41` et l'`ItemsControl` de `Views/PageCanvas.axaml:55`. La
transformation de rotation, elle, se compose déjà proprement
(`ToLocal`/`ToPage`, `Models/Frame.cs:67-90`), ce qui est de bon augure.

Faire (1) d'abord : c'est environ 80 % de l'usage réel pour une fraction du coût.
