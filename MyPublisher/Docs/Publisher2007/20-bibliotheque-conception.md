# 20 — Bibliothèque de conception (Design Gallery)

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 56–60.
**Verdict** : `manquant`

## Ce que fait Publisher 2007

Un catalogue d'éléments composés, prêts à poser : logos, encadrés, exergues, ours,
en-têtes. Chacun est un assemblage de blocs qu'on dépose d'un clic puis qu'on adapte.

## Détail de l'IHM

Dialogue **Design Gallery**, trois volets :

- **Catégories** (colonne gauche) : Accent Box, Accessory Bar, Advertisements,
  Attention Getters, Barbells, Borders, Boxes, Calendars, Checkerboards, Coupons, Dots,
  Linear Accent, **Logos**, Marquee, Mastheads, Phone Tear-Off, Picture Captions,
  Pull Quotes, Punctuation, Reply Forms, Sidebars, Tables of Contents.
- **Designs** de la catégorie, nommés et illustrés (pour Logos : Center Focus,
  Crossed Corner, Extension, Font Focus, Foundation Bar, Open Oval, Perpendicular,
  Right Vertical, Suspended Rectangle, Top Bar), avec un curseur de zoom sur les
  vignettes.
- **Options** propres au design retenu (colonne droite) : pour un logo, *Graphic*
  `Include` / `None`, et *Lines of text* `1` / `2` / `3`.
- Bouton **Insert Object**.

Point important pour nous (p. 58) : **l'élément inséré est un groupe**. Pour changer sa
taille ou remplacer son image, il faut le dissocier (`Arrange/Ungroup`), agir, puis le
regrouper (`Arrange/Regroup`).

## État dans MyPublisher

Absent. Aucune notion d'élément composé ni de bibliothèque.

## Écart / ce qu'il faudrait

Tel quel, cette fonction dépend de [16-groupement.md](16-groupement.md) et suppose de
dessiner des dizaines d'éléments : coût élevé, résultat daté.

**Version dégradée, nettement plus utile** : une **bibliothèque de blocs réutilisables**
alimentée par l'utilisateur. On sélectionne un ou plusieurs blocs, on les enregistre
sous un nom, on les repose dans n'importe quel document. C'est exactement la
*Content Library* que Publisher a fini par ajouter à côté de la Design Gallery — elle
est visible dans les captures du livre, au menu Insert (p. 22 : *Add to Content
Library…*, *Item from Content Library…* `Ctrl+Shift+E`) et au menu contextuel d'un bloc
(p. 34), sans que le livre l'explique jamais.

Cette version a trois avantages :
- elle ne demande pas de contenu à dessiner, l'utilisateur l'apporte ;
- elle réutilise la sérialisation existante — un élément de bibliothèque est un
  fragment de `.mypub`, et `Frame.Clone()` (`Models/Frame.cs:92`) fait déjà la copie ;
- elle rend la zone de travail hors page de [07-zone-de-travail.md](07-zone-de-travail.md)
  presque superflue, en couvrant le même besoin — garder un logo sous la main — de
  façon persistante et inter-documents.

À traiter dans le même panneau que la bibliothèque d'images de
[13-clip-art.md](13-clip-art.md).
