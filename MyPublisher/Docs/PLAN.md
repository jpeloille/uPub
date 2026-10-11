# Plan de développement

> **À lire avant ce plan** : [`Documents-reels.md`](Documents-reels.md), relevé des deux
> documents ATR réellement composés sous Publisher. Les fiches disent ce que Publisher
> *sait faire* ; ces documents-là disent ce dont on *se sert*. Ils ajoutent cinq postes
> que ce plan ne prévoyait pas — dont le texte riche, sans lequel aucun des deux ne se
> reproduit — et ils déclassent le seuil D.

Couche d'exécution posée sur le relevé fonctionnel de
[`Publisher2007/00-index.md`](Publisher2007/00-index.md) : mêmes priorités, mais
regroupées en phases, avec les dépendances explicites et un critère de recette par
poste. Les fiches sont citées par numéro (`fiche 12`) et non par ligne de code — le
code bouge, les fiches non.

Établi le 30 août 2026, sur l'état du dépôt à cette date : `TextFrame.Background`
vient d'atterrir, `ImageFrame` n'a ni fond ni bordure, `ShapeFrame` garde `Fill`.

---

## Suivi

Où en est chaque poste. Une seule règle pour la colonne d'avancement : un poste ne passe
à 100 % que lorsque **sa recette est jouée et que le contrôle P2 passe**. Entre les deux,
on écrit ce qui est fait plutôt qu'un pourcentage inventé — un poste à moitié écrit n'est
pas un poste à moitié livré. La colonne se met à jour dans le commit qui clôt le poste.

*Dernière mise à jour : 11 octobre 2026 (étape 1 : les postes spécifiés).*

| Poste | Intitulé | Avancement | Où ça en est |
|---|---|:---:|---|
| **P0** | Nom du remplissage | **100 %** | `Fill` retenu et daté ; renommage fait en C1 |
| **P1** | Dépôt sous git | **100 %** | Branche `main`, commit initial `cc098e0` |
| **P2** | Contrôle de non-régression | **100 %** | `Scripts/controle.sh`, référence dans `Scripts/reference.txt` |
| **P3** | Banc de vérification | en cours | Noyau séparé, banc de rendu, comparaison écran ↔ PDF, export déterministe, 20 tests, `Scripts/verifier.sh`, bilan dans `Scripts/bilan.sh`. Reste : les recettes de B1, C1, C2, C3, C4, C6 et R4, clos sans recette rejouable |
| **A1** | Poignée de rotation | **100 %** | Neuvième poignée au-dessus du bloc, aimantée sur 15° sous Maj ; recette dans `Scripts/recettes/` |
| **A2** | Signaler le débordement du texte | **100 %** | Marqueur rouge à l'écran, avertissement à l'export, et l'export rogne au bloc comme l'écran |
| **A3** | Chemins d'images relatifs | **100 %** | Relatif sous le dossier du document, absolu ailleurs ; résolu à l'ouverture. `FormatVersion` reste à 3 |
| **A4** | Superposition : raccourcis et glisser-déposer | **100 %** | `Ctrl+Maj+F` / `Ctrl+Maj+B`, et la liste des blocs se réordonne en y tirant un bloc ; les quatre commandes ne perdent plus leur sélection |
| **A5** | Formats de page | **100 %** | B4, B5, Executive, Tabloid au catalogue, et largeur/hauteur libres au panneau. `FormatVersion` reste à 3 |
| **A6** | Taille du bloc dans la barre d'état | **100 %** | Position **et** taille du bloc, suivies pendant le geste et non seulement à sa fin |
| **A7** | Alignement vertical du texte | **100 %** | Haut, milieu, bas sur le bloc de texte ; le calage est borné à zéro. `FormatVersion` reste à 3 |
| **A8** | Insérer des pages : nombre, position, contenu | 0 % | Spécifié le 11 octobre 2026 |
| **A9** | Rotation par quart de tour | 0 % | Spécifié le 11 octobre 2026 |
| **A10** | Objet trait, et flèche | 0 % | Spécifié le 11 octobre 2026 ; l'import simule le trait par un rectangle |
| **A11** | Aimantation réglable, et aux autres blocs | 0 % | Spécifié le 11 octobre 2026 |
| **A12** | Menu contextuel du bloc | 0 % | Spécifié le 11 octobre 2026 |
| **A13** | Rognage manuel de l'image | 0 % | Spécifié le 11 octobre 2026 |
| **A14** | Contour et ombre du caractère | 0 % | Spécifié le 11 octobre 2026 ; PDFsharp à éprouver d'abord |
| **A15** | Sections : numérotation qui repart | 0 % | Spécifié le 11 octobre 2026 |
| **B1** | Modèles | **100 %** | Catalogue au démarrage et sur `Ctrl+N` ; quatre modèles composés dans `Templates/` ; recette dans `Scripts/recettes/`, qui a remis le nom de la carte de visite à la taille de son texte |
| **B2** | Importer un fichier texte | 0 % | — |
| **B3** | Imprimer | 0 % | — |
| **B4** | Archive `.mypubz` | 0 % | — |
| **B5** | Avertir sur les substitutions de polices | **100 %** | L'export nomme la police demandée et celle employée ; recette dans `Scripts/recettes/` |
| **B6** | Aperçu avant impression | 0 % | Spécifié le 11 octobre 2026 |
| **B7** | Replier les panneaux | 0 % | Spécifié le 11 octobre 2026 |
| **C1** | Fond et bordure au niveau de `Frame` | **100 %** | Fond, contour, trait, arrondi et opacité sur tout bloc ; `FormatVersion` 2, migration à l'ouverture ; recette dans `Scripts/recettes/` |
| **C2** | Repères | **100 %** | C2a grille de composition · C2b règles graduées et repères libres ; recette dans `Scripts/recettes/` |
| **C3** | Sélection multiple, puis groupement | **100 %** | C3a sélection · C3b alignement et répartition · C3c groupe persistant ; recette dans `Scripts/recettes/` |
| **C4** | Champs | **100 %** | Folio, nombre de pages, date, coordonnées et logo ; les coordonnées servent depuis B1 ; recette dans `Scripts/recettes/` |
| **C5** | Remplissage riche et cohérence visuelle | C5a et C5c faits | Fond de page, et remplissage uni, dégradé, image ou teinte sur tout bloc ; `FormatVersion` 4. Les cinq images d'ATA 21 sont portées. **C5b — jeux de couleurs et de polices — reste ouvert** |
| **C6** | Marges intérieures par côté | **100 %** | Quatre marges sur le bloc de texte ; `FormatVersion` 3 ; recette dans `Scripts/recettes/` |
| **C7** | Bibliothèque de blocs | 0 % | Débloqué depuis C3a |
| **C8** | Miroir horizontal et vertical | 0 % | Spécifié le 11 octobre 2026 |
| **C9** | Le bloc tableau | **100 %** | Bande de cases : colonnes, lignes, contenu, fond et contour par case. `FormatVersion` reste à 3 |
| **R1** | Texte riche : fragments de style | **100 %** | `Spans` sur le bloc ; l'écran et l'export passent par la même composition ; recette dans `Scripts/recettes/` |
| **R2** | Retraits et espacement de paragraphe | **100 %** | Cinq réglages sur le bloc, des écarts par indice de paragraphe ; la ligne composée porte son retrait et sa largeur |
| **R3** | Colonnes à l'intérieur d'un bloc | **100 %** | Nombre et gouttière sur le bloc ; la ligne composée porte sa colonne et son ordonnée. Autre chose que C2a |
| **R4** | Interligne : une seule mesure | **100 %** | Multiple de la hauteur naturelle ; écran et PDF concordent à 0,05 pt près ; recette dans `Scripts/recettes/` |
| **R5** | **Ouvrir un `.pub`** | **100 %** | Lecture déléguée à libmspub. Les deux documents ATR s'ouvrent objet pour objet, sans perdre un style |
| **R6** | Alignement par paragraphe | 0 % | Spécifié le 11 octobre 2026 |
| **R7** | Éditer le texte riche à la souris | 0 % | Spécifié le 11 octobre 2026 ; attend Q1 |
| **Q1** | Les styles suivent la frappe | 0 % | **Défaut de données** : taper dans un bloc importé décale ses gras |
| **Q2** | Le texte composé se détache de son bloc | **100 %** | `ControleDeBloc` suit l'attachement, comme `FondBloc`, et lâche l'ancien bloc quand on lui en donne un autre. Mesuré : le changement de page ne fuyait pas ; la fuite ne frappait qu'un contrôle sorti de l'arbre avec son bloc. Recette dans `Scripts/recettes/` |
| **Q3** | Le cache d'images oublie ses échecs | **100 %** | L'ouverture d'un document (fichier ou modèle) oublie les échecs du cache, et eux seuls ; insérer une image ou la choisir pour un fond relit le fichier. L'annulation ne relit pas le disque. Recette dans `Scripts/recettes/` |
| **Q4** | Un filet au même endroit à l'écran et au PDF | 0 % | Relevé en A2 et à la relecture du 7 septembre |
| **Q5** | Un groupe rogne pareil à l'écran et au PDF | 0 % | Relevé en C9 |
| **Q6** | Mesurer la découpe d'un remplissage par image | 0 % | Relevé à la relecture du 7 septembre |
| **Q7** | La recette de A6 sur le cas réel | 0 % | Relevé à la relecture du 7 septembre |
| **Q8** | N'avertir que des polices dessinées | 0 % | Relevé à la relecture du 6 septembre |
| **Q9** | Retirer `AbsoluteLineHeight` | 0 % | Relevé en A2 : survivant de l'ancienne sémantique de l'interligne |
| **Q10** | Saisir une poignée hors de la page | 0 % | Relevé en A1 |
| **Q11** | La liste des blocs défile pendant le glissé | 0 % | Relevé en A4 |
| **Q12** | Dépendance vulnérable | **100 %** | `Tmds.DBus.Protocol` relevé à 0.21.3 (correctif rétroporté) par une référence directe dans `MyPublisher` ; Avalonia reste en 11.3.9. Recette dans `Scripts/recettes/`, qui lit la base des vulnérabilités et le prouve sur un projet témoin |
| **Q13** | Couper la ligne au même mot à l'écran et au PDF | 0 % | Trouvé le 11 octobre 2026 : le document d'exemple coupe une ligne différemment |
| **D** | Moteur de composition | — | Décision non ouverte ; à rouvrir quand A, B et C sont livrées |

**Par phase** — P : 3 sur 4 · A : 7 sur 15 · B : 2 sur 7 · C : 6 sur 9 · R : 5 sur 7 ·
Q : 0 sur 13 · D : non planifié.

Soit **23 postes sur 55**. Le compte a doublé le 11 octobre 2026 sans que rien ne
recule : il porte désormais les onze postes de l'audit des fiches (A8 à A15, B6, B7,
C8), deux postes que les documents réels demandaient (R6, R7), et la phase Q, qui
recense les défauts que les postes et les relectures ont **trouvés et consignés sans les
corriger**. Ces derniers étaient écrits, mais dispersés dans la prose de leurs postes :
rien ne les comptait, et `Scripts/bilan.sh` ne lit que ce tableau.
C'est un compte de postes et non d'effort : C3 pesait à lui seul plus que toute la
phase A, et les préalables ne produisaient aucune fonctionnalité.
Le tableau dit ce qui est clos, pas ce qui reste à peiner.

---

## P — Préalables

Deux choses à faire **avant** la phase C, pas après. Elles ne produisent aucune
fonctionnalité ; elles rendent le reste réversible.

### P1 — Mettre le dépôt sous git

Le projet n'est pas versionné (`git init` n'a jamais été fait). La phase C remonte des
propriétés de classe en classe et rend le rendu récursif : ce sont exactement les
changements qu'on veut pouvoir défaire. Un plan qui les programme sans historique est
un plan sans annulation — alors même que l'application, elle, en offre 60 pas.

*Recette* : `git log` montre un commit initial contenant l'état actuel.

*Fait le 5 septembre 2026* — dépôt initialisé (`main`), commit initial `cc098e0`,
52 fichiers. Les sorties de compilation et les réglages Rider sont écartés par
`.gitignore` ; les deux guides PDF de `Books/` sont suivis.

### P2 — Un contrôle de non-régression minimal

Il n'y a pas de projet de test. Sans aller jusqu'à une stratégie de test, il faut au
moins un garde-fou sur la chaîne qui compte :

1. ouvrir `Samples/exemple-bulletin.mypub`,
2. l'enregistrer sous un autre nom, comparer les deux JSON — ils doivent être identiques,
3. l'exporter en PDF, relever le nombre de pages et la liste des avertissements.

Le troisième point se formule comme un **écart** et non comme une absence : on compare
au relevé de référence pris sur l'état actuel. Un contrôle qui exige zéro avertissement
tomberait dès qu'un document de référence porte une image liée manquante — et un garde-fou
qui échoue sur du code intact ne sert plus à rien. Le document d'exemple actuel ne
contient d'ailleurs aucune image (15 blocs de texte, 8 formes) : la référence est
aujourd'hui vide, elle ne le restera pas forcément.

Trois étapes scriptables en une trentaine de lignes, à passer avant et après chaque
poste de la phase C. C'est le filet, pas la ceinture.

*Recette* : le script tourne sur l'état actuel et produit un relevé de référence ; il
sort en code 0 tant que le relevé ne change pas.

*Fait le 5 septembre 2026* — `Scripts/controle.sh`, référence dans
`Scripts/reference.txt` (2 pages, aucun avertissement). L'étape 2 a demandé un point
d'entrée sans interface, `--resave`, et vérifié son utilité dès le premier passage :

- l'encodeur JSON par défaut échappait tout caractère non-ASCII et l'apostrophe — un
  document français enregistré par l'application sortait en `l\u0027unit\u00E9`, contre
  la lisibilité que le format revendique. Corrigé dans `DocumentSerializer`.
- `Samples/exemple-bulletin.mypub` avait été écrit à la main et non par le sérialiseur.
  Il est régénéré par lui, à données égales, pour que l'étape 2 reste l'égalité stricte
  demandée ici plutôt qu'un écart toléré. L'ordre d'écriture est depuis déclaré dans le
  modèle, de dix en dix, ce qui rend au fichier l'ordre lisible qu'il avait à la main.

Le contrôle a été éprouvé en le faisant échouer : JSON écarté du sérialiseur, compte de
pages différent, bloc image au chemin inexistant.

### P3 — Un banc de vérification permanent

P2 tient la chaîne d'écriture, et rien d'autre. Or les recettes de C1, C2a, C2b, C3c et
C6 ont toutes été jouées avec un outillage monté puis jeté : les mots
`RenderTargetBitmap`, `pdftoppm` et « pointeur synthétisé » ne se trouvent nulle part
ailleurs que dans la prose de ce fichier. Aucune de ces recettes n'est rejouable, et
rien n'arrêterait quelqu'un qui casserait l'aimantation, la composition des rotations
d'un groupe ou la fidélité écran/export.

Sept pièces : le noyau séparé de l'application (`MyPublisher.Core`), l'export rendu
déterministe, les fonctions pures sorties de la vue, un banc qui rend l'écran hors champ
(`MyPublisher.Banc`), la comparaison écran ↔ PDF (`Scripts/comparer-rendus.sh`), une
recette exécutable par poste — **y compris pour les postes déjà clos** — et un bilan
machine qui dit ce qui reste ouvert.

*Recette* : saboter volontairement l'aimantation, la mise à l'échelle des groupes, une
propriété dans un `Clone()` et une étape de migration — chaque sabotage doit être
attrapé.

*Recette jouée le 6 septembre 2026*, et elle est désormais un script :
`Scripts/eprouver-le-juge.sh`. Sept sabotages, sept attrapés. **Elle a servi dès le
premier passage** : rien n'attrapait un groupe qui cesse de mettre ses blocs à
l'échelle. C'était précisément la recette de C3c — jouée à la main en comparant des PDF
au pixel près, puis jetée. Comblée par `GroupeTests`, qui éprouve aussi qu'un groupe
réduit à zéro ne déchire pas ses blocs en `NaN`, et que passer du repère de la page à
celui du groupe puis revenir rend exactement le même point, rotation comprise.

L'exhaustivité de `Clone()` est éprouvée **par réflexion** plutôt qu'en énumérant les
propriétés à la main — énumérer reproduirait exactement l'oubli qu'on cherche à
empêcher. Toute propriété qui s'écrit dans le fichier doit survivre à une copie ; celle
qu'on ajoutera demain est donc couverte le jour où elle est écrite.

*Fait en partie le 6 septembre 2026* — le noyau, le banc et la comparaison.
`Models` et `Services` ne référençaient déjà aucun type Avalonia : le découpage n'a
déplacé aucune ligne, il a matérialisé une séparation que le code respectait. Le banc
ouvre la vraie fenêtre sans affichage, sur Skia, et rend la page à deux pixels par
point — la même échelle que `pdftoppm -r 144`, sans facteur de conversion.

Deux pièges silencieux, tous deux gardés désormais : `PageRoot` est mesuré en points
mais vit sous la mise à l'échelle du zoom, si bien que le rendre directement dessinait
son contenu 4/3 trop grand dans une image à la bonne taille — un bandeau de 118 pt
sortait à 157 pt sans que rien ne le signale ; et un écart **nul** est refusé au même
titre qu'un écart énorme, puisqu'il voudrait dire qu'on compare une image à elle-même.

*Bilan écrit le 11 octobre 2026* — `Scripts/bilan.sh` lit le tableau de suivi et
cherche, pour chaque poste, la recette qui le rejoue ; `Scripts/verifier.sh` en imprime
la conclusion à la fin de chaque passage. Le premier passage compte **sept postes clos
sans recette rejouable** : B1, C1, C2, C3, C4, C6 et R4. Ce sont, à B1 et R4 près, ceux
que le premier paragraphe de cette section nommait déjà. Certains sont tenus en partie
par les tests — `GroupeTests` pour C3c, `AimantationTests` pour C2 — mais un test qui
éprouve un calcul n'est pas une recette qui rejoue le geste du poste. P3 reste donc
ouvert sur cette seule dette.

Le même jour, le banc est passé à .NET 10 et le dépôt a quitté le partage réseau pour
un disque local (`~/src/MyPublisher`, le partage restant le dépôt distant). Sur la
machine où .NET 9 n'était plus installé, **aucun** test ne se lançait ; sur le partage
CIFS, aucun script ni le binaire compilé ne s'exécutait, faute de bit d'exécution. Les
304 tests et le contrôle repassent tels quels sous .NET 10, sans une ligne de code
changée.

#### Les sept recettes qui manquent

Chacune reprend le critère que son poste avait écrit, et le rend rejouable. Le nom du
script commence par le code du poste : c'est ainsi que `Scripts/bilan.sh` le reconnaît.
Là où le geste a été joué au pointeur synthétisé, la recette appelle les tests
d'interface qui le jouent déjà — filtre `dotnet test --filter`, comme celles de A4 et
A6 — plutôt que de le réécrire en shell.

| Script | Ce qu'il mesure |
|---|---|
| `B1-modeles.sh` | Les quatre modèles s'ouvrent en copie, sans chemin ; le fichier du modèle reste identique octet pour octet après une retouche enregistrée ailleurs ; les quatre s'exportent **sans un avertissement** |
| `C1-fond-et-filet.sh` | Une image encadrée d'un filet de 2 pt, sans forme superposée : l'écran et le PDF concordent (`comparer-rendus.sh`), et la couleur du filet se relève sur le bord |
| `C2-reperes.sh` | Les tests d'aimantation et de repères au pointeur synthétisé passent ; le même document exporté avec la grille la plus dense et sans grille donne deux images identiques au pixel près, avec et sans repères libres |
| `C3-selection-groupe.sh` | Quatre blocs alignés à gauche ont le même X ; groupés, déplacés de (30, 20) pt puis exportés, chacun se retrouve exactement déplacé de (30, 20) dans le PDF (`pdftotext -bbox`) |
| `C4-champs.sh` | Un folio sur huit pages se lit 1 à 8 page par page dans le PDF (`pdftotext`), posé seul **et** posé dans un groupe ; les coordonnées du jeu actif, et de lui seul, apparaissent dans le PDF d'un modèle de B1, chacun de trois jeux actif à son tour |
| `C6-marges.sh` | 12 pt de marge à gauche seulement : le fer gauche est décalé de 12 pt, le fer droit ne bouge pas ; 20 pt en haut seulement : le texte descend de 20 pt ; 12 pt à droite seulement : le fer droit recule de 12 pt — à l'écran comme au PDF, à 0,05 pt près |
| `R4-interligne.sh` | Un bloc de neuf lignes à l'interligne 1,0 puis 1,5 : l'avance mesurée entre deux lignes est la même à l'écran et au PDF, et vaut le multiple de la hauteur naturelle |

Chaque recette reçoit au moins un sabotage dans `Scripts/eprouver-le-juge.sh`. Une
recette qu'on n'a jamais vue échouer ne prouve rien. P3 passe à 100 % quand le bilan
ne nomme plus aucun poste clos sans recette.

#### C1, C2 et C3 rejouées

*Fait le 11 octobre 2026* — trois recettes, et le bilan ne nomme plus C1, C2 ni C3.

- `C1-fond-et-filet.sh` fabrique sa page : une image carrée cadrée pour remplir son
  bloc sans se déformer (`UniformToFill`, le seul cadrage que l'export découpe), une
  image en hauteur qui laisse voir le fond jaune de son bloc, un bloc de texte à fond
  et à filet. Sur l'écran (le banc) et sur le PDF, au milieu de chacun des douze côtés,
  le pixel juste à l'intérieur du bord porte la couleur du filet — c'est ce qui prouve
  qu'il passe **sur** le contenu —, le filet fait exactement 2 pt (quatre pixels : 1,5
  ou 2,5 pt tombent), et le PDF le centre sur le bord au quart de point. Les fonds et
  les images se relèvent à leur place, la photo ne déborde pas de son bloc, puis
  `comparer-rendus.sh` passe.
- `C2-reperes.sh` appelle sept tests au pointeur synthétisé (`ReperesTests`, trait
  `poste=C2`) : les quatre blocs lâchés près d'un bord de colonne de la recette de C2a,
  Alt qui désarme, un repère tiré de la règle du haut, aimanté sur une colonne, repris,
  retiré hors page, un repère horizontal tiré de la règle de gauche, aimanté sur un
  bord de ligne puis retiré, et un bloc posé exactement sur un repère à 333,3 pt —
  chaque geste annulable. Puis le document d'exemple, exporté sans grille, avec la grille la plus
  dense que le panneau laisse régler (24 colonnes, 24 lignes, une ligne de base par
  point) et avec des repères libres en plus : zéro pixel d'écart, page par page. La
  contre-épreuve rend les mêmes documents à l'écran, repères allumés, et exige qu'ils
  y diffèrent — sans quoi l'égalité des PDF pourrait tenir à une grille perdue.
- `C3-selection-groupe.sh` joue le geste entier (`GroupementTests`, trait `poste=C3`) :
  un clic et trois Maj-clics, « aligner à gauche » entre eux, Ctrl+Maj+G, puis le
  groupe glissé de (30, 20) pt. Le test écrit le document groupé puis déplacé ; la
  recette les exporte et mesure chaque mot par `pdftotext -bbox` : un même fer à
  154 pt, puis exactement (30,000 ; 20,000) pt de décalage pour chacun. Le test défait
  ensuite les trois gestes, un pas chacun, jusqu'aux abscisses d'origine.

Un sabotage par recette dans `eprouver-le-juge.sh` : le filet d'une image rangé sous
elle à l'écran, les repères libres retirés de la table d'aimantation, « aligner à
gauche » qui aligne sur le bord droit. **Les deux derniers ne tombaient sous aucun
test** : ni la commande d'alignement, ni celle du groupement, ni le geste d'un repère
libre n'étaient éprouvés nulle part. La relecture en a trouvé trois autres que rien
n'attrapait, et que les recettes attrapent depuis : la découpe d'une image
`UniformToFill` qui survit au filet du PDF et le rogne de moitié, la règle de gauche
qui ne pose plus de repère, l'alignement qui n'ouvre plus de pas d'annulation. Ils
sont au juge aussi.

*Ce que la recette de C1 a mesuré de Q4.* Sur les douze côtés, sans exception, l'écran
rentre le filet de 2 pt d'exactement 1,00 pt — une demi-épaisseur — là où le PDF le
centre. La recette l'exige ainsi : ce que C1 promet (la couleur sur le bord, côté
intérieur, et l'épaisseur) est demandé des deux côtés sans indulgence, puisque les deux
filets se recouvrent sur la moitié intérieure ; la position est demandée au quart de
point au PDF, qui suit la documentation ; à l'écran, l'écart au PDF est imprimé à
chaque passage et borné à ce que Q4 explique, de 0 à une demi-épaisseur vers
l'intérieur. La tolérance d'images (1,2 %) est calculée sur ce que Q4 coûte —
2 120 pt de pourtour décalés d'un point, 0,84 % de la page — et non relevée puis
arrondie. Q4 réparé, l'écart imprimé tombera à 0 : c'est sa recette, pas celle-ci, qui
resserrera alors l'exigence.

*Choisi sans certitude.* La « grille la plus dense » est celle que le panneau autorise
aujourd'hui, et non les 77 repères de la recette d'origine, qui n'en disait pas le
réglage. L'alignement de C3 est joué « par rapport aux marges » décoché, les blocs
entre eux : c'est le cas où le bord visé dépend de la sélection, donc le seul où un
mauvais bord se voit ; l'alignement sur les marges, réglage par défaut, n'est pas joué.

*Trouvé et laissé tel quel.* Le pointillé gris d'un bloc image — « repère visible tant
qu'aucune image n'est chargée », dit `PageCanvas.axaml` — est en réalité toujours
dessiné : une image cadrée sans déformation le laisse voir sur le bord du bloc, et il
n'obéit pas à l'interrupteur des repères. Il entre donc dans toute comparaison écran ↔
PDF (0,06 % de la page pour un bloc seul).

#### B1, C4, C6 et R4 rejouées

*Fait le 11 octobre 2026* — `B1-modeles.sh`, `C4-champs.sh`,
`C6-marges.sh` et `R4-interligne.sh`, chacune avec son sabotage à la fin de
`Scripts/eprouver-le-juge.sh`. Les gestes que le shell ne sait pas jouer sont dans deux
fichiers de tests d'interface, appelés par filtre et comptés comme ceux de A4 :
`ModelesTests` (`poste=B1`) frappe `Ctrl+N`, choisit le dépliant, part de lui sans son
chemin ; puis, pour chacun des quatre modèles, `Ctrl+N`, le choix dans le catalogue,
Entrée, une retouche et `Ctrl+S`. Le modèle éprouvé est donc celui que le catalogue lit,
dans le répertoire de sortie : chaque test le remet en état en sortant, parce qu'un
sabotage qui rendrait au document le chemin de son modèle l'écraserait, et que la
compilation suivante ne le remplacerait pas, la source étant plus ancienne que lui. Le
`Ctrl+S` n'est pas une seconde garantie : le test vérifie juste avant que le document
n'a pas de chemin, et sans chemin `Ctrl+S` passe par « Enregistrer sous », que la
plate-forme sans affichage referme. `ChampsTests` (`poste=C4`) change la page courante
par le modèle de vue — et non par un geste — et relit le contenu résolu que chaque bloc
reçoit pour cette page, non les pixels dessinés.

Ce que les recettes mesurent, et contre quoi. Le folio de C4 se lit au PDF page par page
**à sa place**, posé seul et dans un groupe — trouver « 1 » à « 8 » quelque part dans le
texte passerait sur un folio qui suivrait l'ordre de lecture —, la date sous une locale
anglaise, et les coordonnées d'un fichier à trois jeux : la carte de visite s'exporte
trois fois, chaque jeu actif à son tour, et seuls ses six champs doivent paraître, aucun
des douze autres, son logo relevé au pixel à sa couleur. Un code qui prendrait le premier
jeu, ou le dernier, tombe sur l'un des trois exports. C6 pose six blocs : quatre pour la
marge gauche, dont deux calés à droite — c'est le fer droit qui distingue quatre marges
d'une marge unique appliquée des deux côtés —, un avec 20 pt en haut seulement, comme la
recette jouée à la main le 5 septembre, et un calé à droite avec 12 pt à droite. La
marge du bas n'est pas éprouvée : sous un calage en haut, elle ne déplace rien. R4 ne lit pas la hauteur naturelle dans le code qu'il éprouve, mais dans la
table `hhea` du fichier de police que fontconfig désigne — 13,96875 pt pour DejaVu Sans
12 —, puisque les deux rendus appellent le même `Compositeur.Avance` et que les comparer
entre eux prouverait leur accord, jamais leur justesse. Mesuré au PDF : 13,9688 et
20,9531 pt, pour une tolérance de 0,05 pt. Pour C6, des deux côtés : 12,000 pt de
déplacement du fer gauche et 0,000 pt du fer droit sous la marge gauche, 20,000 pt du
haut sous la marge du haut, −12,000 pt du fer droit sous la marge droite.

Les deux recettes à l'encre mesurent à **quatre** pixels par point, au centre de l'encre
pondéré par la noirceur et non à son bord : le plan demande 0,05 pt, soit un dixième de
pixel à l'échelle ordinaire. À deux pixels par point, le calage des glyphes sur la grille
coûte jusqu'à 0,06 pt sur l'avance de neuf lignes, et R4 échouerait sur du code juste.

Les sabotages ont été joués un à un contre les tests, le contrôle et **chaque** recette
avant d'être écrits. Ceux de C4 (le folio du PDF figé à 1) et de C6 (l'export retire la
marge gauche à la place de la droite) passent tout le reste et ne tombent que sur leur
recette. Celui de R4 — le défaut d'origine, l'écran qui avance du corps — passe les 311
tests et tombe sur les recettes de R2, R3 et R4. Celui de B1 tombe sur les tests
`poste=B1`.

La relecture en a trouvé deux que rien ne voyait : la carte de visite imprimant le
**dernier** jeu de coordonnées au lieu de l'actif (le fichier de la recette n'avait que
deux jeux, l'actif en second), et l'écran ignorant la marge du **haut** (la recette
n'éprouvait que la marge gauche). Les recettes de C4 et de C6 ont été reprises comme dit
plus haut ; chacun de ces deux sabotages a été joué à la main contre sa recette, qui
tombe, puis ajouté au juge.

**La recette de B1 a trouvé un défaut, corrigé dans un commit à part.** La carte de
visite s'exportait avec un avertissement dès qu'on y mettait un nom : le bloc « Nom »,
16 pt de haut en gras 12 pt à l'interligne 1,25, réclame 17,46 pt depuis R4. C'est la
retombée qu'A2 avait corrigée sur les documents d'exemple, et qu'elle ne pouvait pas voir
ici : le test des documents livrés mesure le texte *saisi*, vide sous un champ. « Les
quatre modèles s'exportent sans un avertissement » était donc faux depuis le 6 septembre.
Le bloc ne porte qu'une ligne : son interligne passe à 1, ce qui ne déplace aucun bloc.
Le rendu de la carte change pourtant : le demi-interligne que la composition place
au-dessus de la première ligne disparaît, et le nom **remonte de 1,75 pt** dans son bloc
(1,746 pt relevés par `pdftotext -bbox`). Sa boîte, qui chevauchait de 0,4 pt celle de
« Fonction », posée juste dessous, s'en écarte désormais de 1,4 pt. Le message du commit
du correctif ne le dit pas ; c'est ici qu'il est consigné.

*Choisi sans certitude* — corriger le modèle plutôt que de le signaler seulement : sans
cela la recette restait rouge, et la relâcher était exclu ; le correctif est isolé pour
pouvoir être repris. Le « jeu enregistré » de C4 est un fichier écrit par la recette au
format du dialogue des coordonnées, et non passé par ce dialogue. La concordance des
positions écran ↔ PDF est tenue à 0,5 pt, « au pixel » à deux pixels par point ;
l'avance et les marges, à 0,05 pt.

*Trouvé et laissé tel quel* — le banc n'est compilé ni par `dotnet test` ni par
`controle.sh`. Sur une copie neuve, A1 et A2 échouent faute de binaire au premier passage
de `verifier.sh` ; sous l'épreuve du juge, elles mesurent un banc **périmé**, compilé
avant le sabotage, jusqu'à ce que A4 recompile la solution. Les quatre nouvelles recettes
compilent elles-mêmes. Le texte de l'écran est posé 0,2 à 0,3 pt plus bas que celui du
PDF, quel que soit l'interligne — sous le pixel. Le bloc « Société » de la carte de
visite tient tout juste dans ses 16 pt. Des compilations avortent par intermittence sur
« Internal CLR error (0x80131506) » — quatre fois dans la journée, pendant que d'autres
copies de travail compilaient sur la même machine ; sous l'épreuve du juge, un tel échec
compte le sabotage pour attrapé, et c'est arrivé une fois à celui de R4, rejoué depuis à
part. Corrigé en revanche, parce qu'il empêchait de le voir : le juge ne savait nommer
aucune recette parmi ceux qui attrapent — il les cherchait en minuscules —, et disait
« attrapé par la compilation ».

*Réparé à la fusion de la salve.* `verifier.sh` compile d'abord toute la solution, banc
compris. Un plantage du compilateur y est rejoué une fois, puis signalé par le code de
sortie 3, que le juge ne compte plus comme une prise. Le juge nomme désormais **tous**
les tests et toutes les recettes en échec, et chaque sabotage de recette nomme la
recette qui doit l'attraper : attrapé par une autre seulement, il compte comme un trou.

#### Ce que le banc a trouvé le jour où il a servi

**L'interligne n'est pas appliqué à l'export.** `AbsoluteLineHeight`
(`Models/Frame.cs`) n'est lu que par l'écran et par les vignettes du catalogue ;
`PdfExporter.DrawText` ne le lit jamais et laisse `XTextFormatter` avancer à la hauteur
naturelle de la police. Mesuré sur un bloc de neuf lignes en DejaVu Sans 12 pt, par
différence entre deux et neuf lignes :

| Interligne | Avance à l'écran | Avance au PDF |
|---|---|---|
| 1,0 | 12,00 pt | 13,93 pt |
| 1,5 | 18,00 pt | 13,93 pt |

L'écran suit `FontSize × LineHeight` ; le PDF ne suit rien. Un réglage offert dans le
panneau ne franchit donc pas l'export — et le README annonce l'interligne parmi ce que
l'application sait faire.

**Tranché le 6 septembre 2026, par les documents réels** (voir
[`Documents-reels.md`](Documents-reels.md)) : aucun des deux `.pub` déposés ne porte
`fo:line-height`. La propriété existe dans libmspub et serait émise si Publisher stockait
un interligne particulier — les deux documents s'en tiennent donc au défaut de Publisher,
qui est la **hauteur naturelle de la police**. **C'est l'export qui a raison, et l'écran
qui a tort.** `LineHeight` doit devenir un multiple de la hauteur naturelle et non du
corps : à 1, l'écran ne doit rien imposer ; au-delà, il multiplie cette hauteur-là, et
l'export applique le même multiple à la même mesure.

**En revanche, les deux moteurs coupent aux mêmes mots.** Un paragraphe de sept lignes
dans un bloc étroit se brise exactement aux mêmes endroits des deux côtés, à deux pixels
près sur la largeur d'encre. C'est plus favorable que ce que le seuil D redoutait, et
cela réduit le risque de A7 : une fois l'interligne honoré, la comparaison écran ↔ PDF
devient un contrôle utilisable même sur une page de texte.

**Conséquence sur l'ordonnancement** : ces trois choses — interligne honoré, débordement
signalé, alignement vertical — demandent la même passe de composition dans
`PdfExporter`, et le dépôt tient à ce qu'il n'y ait qu'une seule mesure. Elles forment
un seul train, l'interligne en tête parce que c'est le seul des trois dont la recette
échoue déjà.

**Relevé de départ** : formes seules, écran contre PDF, **0,38 %** de pixels différents,
entièrement sur le lissage des bords. Avec le texte, l'écart monte à 8 % — et c'est
l'interligne, pas le banc.

**Une dérive mesurée, et sans conséquence.** Sélectionner un bloc fait passer sa
position par le panneau de propriétés, donc par un aller-retour points ↔ millimètres qui
rend 100,00000000000007 pour 100. C'est un résidu du correctif de C2a — sept
centièmes de millième de milliardième de point. Vérifié : il ne salit pas le document,
n'empile aucun pas d'annulation, et l'annulation d'un déplacement rend exactement l'état
d'avant le geste. Consigné parce qu'un test d'égalité stricte du JSON bute dessus, et
qu'il vaut mieux l'avoir écrit une fois que le redécouvrir à chaque poste.

*Fait le 6 septembre 2026, second temps* — l'export est devenu reproductible :
`--date` fixe la date des champs, `--contacts` désigne les coordonnées, et le modèle de
vue reçoit les deux plutôt que de lire l'horloge et le `~/.config` de la machine. Au
passage, le document à ouvrir n'est plus « l'argument qui précède le drapeau » mais le
premier argument libre : `--date 2026-09-06 --export x.pdf` aurait sinon pris la date
pour le document. L'aimantation et la taille minimale d'un bloc sont sorties de la vue
vers `Models/Snap.cs` — la seconde y était déclarée deux fois, sans que rien n'impose
l'accord. Vingt tests, dont quatre qui jouent de vrais gestes sur la vraie couche de
saisie, et `Scripts/verifier.sh` qui enchaîne tout ce qui doit passer.


### Ce que la relecture adversariale du 6 septembre 2026 a trouvé

Quatre postes livrés par quatre agents, quatre relecteurs lancés contre eux. Ce qu'ils
ont trouvé, et ce qui en a été fait.

**Corrigé aussitôt**

- **Le panneau ne redessinait plus rien, depuis R1.** `TexteCompose` déclarait
  `AffectsRender` sur la propriété qui porte le bloc — or régler le corps ou un retrait
  ne remplace pas l'instance du bloc, la liaison ne rendait donc rien de neuf. Quinze
  réglages s'enregistraient et s'exportaient correctement sans que la page bouge. Le
  contrôle s'abonne désormais au bloc et à ses deux collections d'écarts.

  *Ce qui l'avait laissé passer quatre postes durant* : aucune vérification ne comparait
  **deux images de la même page**. Les recettes comparaient l'écran au PDF, tous deux
  recalculés depuis le modèle, tous deux justes — c'est l'écran déjà affiché qui ne
  suivait pas. Et le premier test écrit pour l'attraper ne l'attrapait pas non plus :
  `RenderTargetBitmap.Render` redessine de force, quel qu'ait été l'état
  d'invalidation. Il a fallu `CaptureRenderedFrame`, qui rend l'image réellement
  composée. Onze réglages sont désormais éprouvés ainsi, et le correctif a été retiré
  pour vérifier qu'ils échouent sans lui : huit sur onze tombent.

- **La passe des polices ne descendait pas dans les tableaux.** Trouvé par la relecture
  de B5 avant même que C9 ne soit fusionné, et devenu réel à la fusion : les polices des
  cases échappaient au contrôle. `Descendance` traite maintenant les deux types qui
  portent des enfants.

- **Un tableau de 40 × 40 figeait l'application 145 secondes.** Chaque case insérée
  faisait renaître toutes les autres — 1 673 reconstructions pour 1 600 cases — et le
  panneau les relisait à chaque fois. Un verrou de lot ne notifie plus qu'une fois par
  redimensionnement. Deux tests comptent les reconstructions plutôt que les
  millisecondes, pour dire la même chose sur une machine lente.

**Consigné, non corrigé**

- **L'écran découpe le texte au bloc, l'export non.** ***Tranché et fait le
  6 septembre 2026, au poste A2*** : c'est l'export qui découpe, et au bloc. Une
  précision que la vérification a apportée, et qui corrige ce qui suit : la paire réelle
  d'ATA 21 (13,9 et −15,7 pt) sort bien de 1,82 pt de sa **zone de texte**, mais les
  2,88 pt de `fo:padding-left` la laissent à 1,06 pt **à l'intérieur du bloc** — elle
  n'était donc rognée nulle part. La divergence réelle de ces documents est verticale, et
  elle vaut jusqu'à 36,95 pt sur un bloc d'ATA 21.

- **Le seuil de rupture de colonne est évalué deux fois, sur deux mesures.** R3 a
  partagé le *code* qui décide où rompre, pas la *mesure* : l'écran interroge Avalonia,
  l'export PDFsharp. Une ligne qui tombe à un centième de point du bas d'une colonne
  peut partir en colonne 1 d'un côté et en colonne 2 de l'autre, et tout le reste du
  texte se décale. Le message de livraison de R3 présentait ce risque comme supprimé :
  il ne l'est pas. Le supprimer vraiment demande **une seule source de mesure**, ce qui
  est la question du seuil D.

- **Certains avertissements de police portent sur des polices jamais dessinées** : la
  passe relit l'héritage des fragments pour son compte au lieu de lire les styles
  composés. Un fragment entièrement recouvert par un autre est signalé quand même.



### Ce que la relecture adversariale du 7 septembre 2026 a trouvé

Cinq postes livrés par cinq agents, cinq relecteurs lancés contre eux. Deux des
trouvailles portaient sur **l'import du poste R5**, et étaient les plus lourdes.

**Corrigé aussitôt**

- **La marque de paragraphe de Publisher fabriquait une ligne vide fantôme.** libmspub
  rend le terminateur de paragraphe comme du texte — un `insertText (\r)` à la fin de
  chaque paragraphe — et l'import le gardait. À l'intérieur d'un bloc, il se collait au
  saut de ligne suivant en `\r\n` que la composition normalise : le défaut ne se voyait
  pas. Mais celui du **dernier** paragraphe n'avait rien derrière lui et devenait une
  ligne comptée dans la hauteur. Les **vingt-sept blocs** des deux documents finissaient
  ainsi. Conséquence mesurée : **cinq débordements signalés sur ATA 21, tous faux** —
  il n'en reste aucun — et vingt-deux sur les fiches, ramenés à douze. Quatre blocs
  portaient un marqueur rouge alors que rien n'y était coupé.

  *Ce que cela emportait avec soi* : la recette de A2 avait été bâtie sur ces
  débordements-là, et le poste A7 justifiait sa borne de calage par un bloc dont les
  « 26,86 pt composés » comptaient la ligne fantôme. La recette affirme désormais
  l'inverse — qu'aucun bloc d'ATA 21 ne déborde — et c'est ce contrôle-là qui tomberait
  si la ligne revenait.

- **Deux blocs d'ATA 21 sont tournés d'un quart de tour, et l'import les posait droits.**
  Le relevé écrit `librevenge:rotate: 90.0000in` — des degrés affublés d'une unité de
  longueur, que le convertisseur de points aurait lus 6 480. La rotation est portée, et
  la recette la compte.

- **Un remplissage par image illisible arrêtait tout l'export.** Depuis C5c, un
  remplissage peut ouvrir un fichier, et il le faisait hors de toute garde : aucun PDF
  n'était écrit, alors que la même image posée en bloc image ne valait qu'un
  avertissement et que l'écran l'encaissait. La règle du dépôt est constante — signaler
  et continuer — et le fond la suit désormais.

- **Un bloc de texte vide reste un bloc.** Retombée du premier correctif : un bloc des
  fiches ne portait que sa marque de paragraphe, et se retrouvait vide donc écarté. La
  source en compte dix-huit ; l'import en rendait dix-sept.

**Consigné, non corrigé**

- La découpe d'un remplissage par image n'est mesurée nulle part : le document de
  recette de C5 ne porte que des rectangles à angles droits, et un sabotage de la
  découpe traverse toute la vérification.
- La recette de A6 emploie une fabrique de blocs qui évite le cas réel — même défaut
  que celui relevé sur R2 la veille, et la consigne le nommait pourtant.
- L'écran rentre son filet dans le bloc là où l'export le centre sur le bord.
- Le seuil de rupture de colonne reste évalué sur deux mesures (voir plus haut).


### P0 — Une décision à prendre tout de suite

**Le nom du remplissage.** `ShapeFrame.Fill` et `TextFrame.Background` désignent la même
chose sous deux noms, et `ImageFrame` n'a ni l'un ni l'autre. La convergence est
programmée en C1 — mais **la décision de nommage doit être prise maintenant**, parce
que son coût est le seul du plan qui augmente avec le temps :

- aujourd'hui, un seul fichier porte la propriété (`Samples/exemple-bulletin.mypub`,
  8 occurrences de `Fill`, aucune de `Background`) : renommer, c'est un `sed` ;
- dès que des `.mypub` circulent, c'est un chemin de migration à écrire et à tenir.

Recommandation : retenir **`Fill`** pour tous les blocs — c'est le terme du domaine
(Publisher dit *Fill*, réserve *Background* à la page), c'est le nom déjà majoritaire
dans les fichiers existants, et cela laisse `Background` libre pour le fond de page
qui arrive en C5. `FormatVersion` (`Models/Document.cs`) passe à 2 le jour où le
renommage est fait.

*Recette* : la décision est actée ici, en une ligne, avec sa date.

**Décision, 5 septembre 2026 : `Fill`, pour tous les blocs** — la recommandation
ci-dessus est retenue. Le renommage lui-même reste au poste C1, et `FormatVersion`
passera à 2 ce jour-là. La normalisation du document d'exemple y a depuis ajouté
`"Background": null` sur 15 blocs de texte : le renommage reste un `sed`.
*Renommage fait en C1, le même jour — par une migration à l'ouverture plutôt qu'un
`sed`, parce que des `.mypub` de la version 1 existent désormais hors du dépôt.*

---

## Graphe de dépendances

Ce qui commande quoi. À lire de gauche à droite : la colonne de gauche doit exister
avant la colonne de droite.

| Ce qu'il faut d'abord | Ce que ça débloque |
|---|---|
| **Sélection multiple** (C3) | groupement, alignement multiple, répartition, bibliothèque de blocs (C7) |
| **Remplissage au niveau `Frame`** (C1) | fond de page, dégradés, teintes, image de fond (C5) |
| **Notion de champ** (C4) | numéros de page, date, informations professionnelles |
| **Règles graduées** (C2a) | repères de règle posés à la souris (C2b) |
| **Modèles** (B1) | jeux de couleurs et de polices (C5b), informations professionnelles (C4) |
| **Mesure du texte** (seuil D) | ajustement automatique, chaînage, habillage |
| **P1 + P2** | toute la phase C |

Deux remarques que le graphe rend visibles :

- **La sélection multiple est le nœud le plus rentable** : à elle seule elle ouvre
  quatre fonctions que les fiches comptaient séparément.
- **Les modèles ne dépendent de rien.** C'est pourquoi la phase B passe avant la
  phase C alors qu'elle apporte plus : rien ne l'en empêche.

---

## Phase A — Finitions à mécanique existante

Postes qui n'inventent aucune structure : la mécanique est déjà écrite, il manque
le geste, le message ou le champ. Ils étaient sept ; l'audit des fiches en a ajouté
huit, A8 à A15, dont deux — A10 et A14 — touchent au rendu plus que les autres. Aucun ne touche au format de fichier autrement qu'en
ajoutant une propriété facultative. Ils peuvent se faire dans n'importe quel ordre, et
même avant P1 — mais autant commencer par P1.

### A1 — Poignée de rotation *(fiche 10)*

La rotation existe partout — modèle, test de survol, redimensionnement, export — et ne
se règle qu'au champ numérique. Ajouter une neuvième poignée au-dessus du bloc, sur le
même tableau de poignées et le même mode de glisser que le redimensionnement.

*Recette* : tourner un bloc à la souris, l'exporter, l'angle est le même à l'écran et
dans le PDF.

*Fait le 6 septembre 2026.* Une neuvième poignée, posée au-dessus du bord haut du bloc,
sur le même tableau que les huit autres et sur le même mode de glisser que le
redimensionnement — `BeginEdit` à la prise, `CommitEdit` au relâché, donc annulable d'un
seul pas comme un déplacement. Le poste tient dans un fichier, `Views/PageCanvas.axaml.cs` ;
il n'ajoute aucune propriété au modèle, `FormatVersion` reste à 3 et aucun document du
dépôt n'a eu à être régénéré.

**Le geste suit l'écart d'angle depuis la prise, et non l'angle absolu du pointeur.** La
poignée n'est pas sur l'axe du bloc mais à un quart de tour de lui : viser l'angle absolu
ferait sauter le bloc de 90° au premier pixel de glissement. Le pivot est le centre —
celui qu'emploient déjà `Frame.ToPage` et l'export —, si bien que tourner n'écrit que
`Rotation` : ni la position ni la taille ne bougent.

**Maj aimante sur 15°, le pas de Publisher, et l'aimantation porte sur l'angle absolu.**
C'est la seule décision du poste qui ne se déduisait pas. Un pas appliqué à l'écart
parcouru conserverait son travers à un bloc déjà de guingois ; appliqué à l'angle, il
permet de redresser un bloc importé — et c'est ce qu'on attend d'un pas de rotation.
Douze positions par tour couvrent les angles droits, les diagonales et les tiers de
quart. **Maj change donc de sens d'un geste à l'autre** : elle conserve les proportions
sur une poignée d'angle et aimante l'angle sur celle-ci. C'est une divergence assumée,
et c'est celle de Publisher.

L'angle est ramené dans [0, 360[. Le champ du panneau s'arrête à ±360 : deux tours de
poignée dans le même sens le dépasseraient, et la valeur serait ramenée en silence à la
sélection suivante. Publisher affiche 350°, pas −10°.

*Recette jouée*, et elle est un script : `Scripts/recettes/A1-poignee-rotation.sh`. Elle
part d'un vrai pointeur synthétisé sur la vraie couche de saisie — onze vérifications
dans `MyPublisher.Tests.Interface/RotationTests.cs` — puis mesure ce que le geste a
produit : **90°, l'angle des trois objets tournés d'ATA 21** (`librevenge:rotate: 90`
dans le relevé de `pub2raw` ; les fiches de limitations n'en portent aucun). Le PDF et
l'écran sont ensuite rendus à deux pixels par point et leur **boîte d'encre mesurée** :
120 × 400 px posée en (340, 60) des deux côtés, soit le bloc de 200 × 60 pt debout,
tourné autour de son centre. La position absolue de cette boîte n'est pas décorative :
elle mesure le pivot, un quart de tour autour du coin supérieur gauche donnant la même
boîte 130 pt plus loin.

**Ce que la mesure d'encre ne peut pas voir, et ce qui s'en charge.** La boîte d'un bloc
tourné de +90° est exactement celle d'un bloc tourné de −90°, et l'écran comme le PDF
lisent la même propriété : ni la mesure ni la comparaison d'images n'attraperaient une
inversion de sens. C'est la valeur **signée** écrite dans le document, et un test qui
tourne dans l'autre sens et exige 270°, qui l'épinglent.

La recette a été éprouvée en la faisant échouer, cinq fois : sens inversé, aimantation
retirée, pivot placé au coin supérieur gauche, geste de rotation aiguillé vers le
redimensionnement — et, pour vérifier que la mesure d'encre n'est pas décorative, la
rotation retirée de `PdfExporter`. Cinq sabotages, cinq attrapés ; le cinquième par la
seule mesure de boîte, les quatre autres par les tests d'interface. Le binaire a été
touché à chaque restauration : `cp -a` rendrait au fichier sa date d'origine et MSBuild
laisserait vivre le binaire saboté — le piège consigné au poste R3.

#### Trouvé au passage, et corrigé à moitié — la moitié qui nuisait

**Un bloc collé au haut de la page mettait sa poignée sur la règle graduée.** C'est la
seule poignée qui se pose hors du bloc, donc la seule qui puisse sortir de la planche :
la couche de saisie s'arrête au bord de la page, et le clic tombait sur `Ruler`, qui
**posait un repère au lieu de tourner**. Il ne faisait pas rien, il faisait autre chose.
Les deux règles servent désormais la poignée de rotation avant de poser un repère ; la
capture leur revient, et elles renvoient déjà la suite du geste aux mêmes gestionnaires.
Mesuré avant correction sur un bloc posé à 5 pt du haut : rotation 0°, un repère
vertical de trop ; après : 90°, aucun repère.

**Cela ne vaut que règles affichées, et il faut le dire.** *Affichage ▸ Règles* décoché,
`Ruler` n'est plus dans l'arbre visuel : le clic tombe alors sur le plan de travail, que
personne n'écoute, et **ne fait rien**. Mesuré, comme le reste : rotation 0°, aucun
repère. C'est le cas ci-dessous, moins nuisible que celui qui est corrigé — se tromper
de geste vaut moins bien que n'en faire aucun — mais il n'est pas supprimé. Le supprimer
vraiment demanderait d'étendre la couche de saisie au-delà de la page, donc de toucher
`PageCanvas.axaml`, hors du fichier de ce poste.

#### Choisi sans certitude

**La poignée est à 22 px du bord haut, en pixels d'écran et non en points.** Elle garde
donc la même portée de main à tous les zooms, comme les huit autres gardent leur taille.
Vingt-deux plutôt que quinze pour que sa zone de préhension (7 px) ne touche jamais celle
des poignées d'angle : deux poignées qui se recouvrent, c'est un geste qui part au
hasard. Aucune fiche ne donne la distance de Publisher.

**Un disque vert, et une tige qui le relie au bloc.** Le vert est celui que Publisher
emploie pour ce geste — c'est aussi celui des repères libres dans cette application, mais
un disque de neuf pixels sur un bloc sélectionné ne se confond pas avec un trait qui
barre la page. Un neuvième carré, lui, se serait lu comme un bord à tirer.

**Le curseur est la main.** Le jeu standard d'Avalonia n'a pas de curseur de rotation, et
en embarquer une image pour un seul geste coûterait plus que ça ne dit.

#### Trouvé et laissé tel quel

**Une poignée qui sort de la page là où il n'y a pas de règle n'est pas saisissable.**
Deux cas : un bloc tourné de 180° et collé au bas de la page met sa poignée sous la
planche, et un bloc collé au haut la met hors de portée dès que les règles sont
masquées. Le clic ne fait alors rien — et non pas autre chose, contrairement au cas
corrigé ci-dessus. Le champ du panneau reste. Le traiter demanderait d'étendre la couche
de saisie au-delà de la page, donc de toucher `PageCanvas.axaml`, hors du fichier de ce
poste.

**Le curseur ne change pas au-dessus de la poignée quand elle est sur la règle.**
`UpdateCursor` n'écrit que sur la couche de saisie ; les règles portent le leur, posé une
fois pour toutes. On y voit donc la flèche de déplacement de repère alors qu'on
s'apprête à tourner. Le geste, lui, est le bon.

**Une poignée d'angle déborde de sept pixels au-dessus d'un bloc posé à Y = 0.** Sa zone
de préhension sort alors de la planche par le haut et retombe sur la règle, qui pose un
repère. Le défaut est antérieur à ce poste et vaut pour les huit poignées ; il n'est pas
corrigé ici parce que servir les huit depuis la règle changerait le geste de C2b — un
clic sur la règle près du bord d'un bloc cesserait de poser un repère.

### A2 — Signaler le débordement du texte *(fiche 12)*

Aujourd'hui un texte trop long est coupé sans un mot, à l'écran comme à l'export. Deux
gestes : un marqueur visuel sur le bloc concerné, et un avertissement dans le rapport
d'export, qui remonte déjà jusqu'à la barre d'état.

*Recette* : un bloc au texte trop long se signale à l'écran ; son export mentionne le
bloc par son nom.

C'est le poste le plus important de la phase A : il n'ajoute aucune fonction, il
supprime un risque — livrer un PDF amputé sans le savoir.

*Fait le 6 septembre 2026*, avec **A7** et le **rognage à l'export**. Les trois ne font
qu'un : ils demandent tous la hauteur composée d'un bloc, que `Compositeur` sait rendre
depuis R3, et les traiter séparément aurait produit trois mesures là où le dépôt tient à
n'en avoir qu'une.

#### Le rognage : un retour, et non une addition

`PdfExporter.DrawText` pose désormais un chemin de découpe sur le bloc, exactement là où
`PageCanvas.axaml` pose `ClipToBounds`. Ce n'est pas une fonction neuve : la fiche 12
relève qu'**avant R1, `XTextFormatter.DrawString` s'arrêtait au bord de la boîte**. La
composition partagée, qui pose chaque ligne à l'ordonnée qu'on lui donne, a emporté cette
limite sans que rien ne la remplace — et depuis, un bloc trop plein débordait sur ses
voisins dans le PDF et pas à l'écran. Publisher ne dessine pas le texte hors de sa zone ;
les deux rendus s'accordent de nouveau là-dessus.

C'est le **bloc** qu'on découpe, et non la zone de texte, parce que c'est ce que l'écran
fait : une première ligne en retrait négatif pend dans la marge intérieure gauche et doit
y rester visible.

#### Ce que la vérification a corrigé de l'énoncé

La consigne de ce poste, comme la note de R2 plus bas, présentait la paire d'ATA 21 —
`fo:margin-left 0.1930in` avec `fo:text-indent −0.2183in` — comme un cas où l'écran rogne
et le PDF non. **Ce n'est pas le cas, et l'arithmétique le dit** : 13,896 − 15,718 =
−1,822 pt, mais `fo:padding-left` vaut `0.0400in`, soit 2,88 pt. La première ligne tombe
donc à **+1,058 pt à l'intérieur du bloc**. Vérifié sur les neuf blocs d'ATA 21 et sur les
dix-huit des fiches : aucun ne sort de son bloc par la gauche ni par la droite, d'un
millième de point.

La conclusion de l'énoncé tient — c'est bien l'export qui doit rogner — mais le cas qui la
démontrait n'est pas celui-là. La divergence réelle d'ATA 21 est **verticale**, et elle
est bien plus grande : « Air is cooled » compose 36,95 pt de plus que son bloc, « Overheat
Protection » 8,65 pt. C'est donc là que la recette mesure, où le signal vaut 74 pixels à
deux pixels par point et non trois.

#### Le critère du débordement est le bloc, pas la zone de texte

Une fois la découpe posée au bloc, ce qui sort du bloc est exactement ce qui est amputé —
et rien d'autre ne l'est. Un texte qui ne dépasse que dans sa marge intérieure basse reste
entier et lisible : le signaler serait crier au loup, et c'est la leçon que B5 a payée
cher. **Deux des neuf blocs d'ATA 21 sont dans ce cas**, à 1,5 pt près ; la recette exige
le silence sur eux, nommément.

Le seuil est un demi-point, et il n'est pas là pour absorber une erreur d'arrondi — elle
est de l'ordre du millionième. Il est là parce que le marqueur de l'écran sort des
métriques d'Avalonia et l'avertissement de l'export de celles de PDFsharp : un bloc au ras
de son bord peut être signalé d'un côté et pas de l'autre. Aucun seuil ne referme cette
fenêtre ; seul un document qui se donne de l'air la referme.

#### A7 : le calage est borné, et c'est tout le poste

Trois valeurs sur le bloc de texte — haut, milieu, bas —, une propriété facultative, et
`FormatVersion` **reste à 3**. L'import de R5 les lit (`draw:textarea-vertical-align`) et
son avertissement disparaît.

Le seul bloc des deux documents réels qui porte ce réglage compose **26,86 pt dans 19,43
pt de haut**. Le calage est donc borné à zéro : un texte plus haut que sa zone se cale en
haut, quel que soit le fer demandé. Centrer y escamoterait le début du texte, et ce qui
manque à la fin se voit quand ce qui manque au début ne se voit pas. Une recette sur les
valeurs réelles n'éprouve donc *que* cette borne — d'où le document propre au poste, qui
éprouve les trois calages sur un bloc où ils se distinguent.

*Choisi sans certitude* : c'est bien cette borne, et non la troncature par le haut, que
fait Publisher — mais aucune fiche ne le dit et le relevé ne montre qu'une occurrence.

#### La désignation d'un avertissement

Le format reste « &lt;désignation&gt; : &lt;message&gt; » ; la désignation seule s'allonge
de ce qu'il faut pour désigner. La page d'abord, puis les blocs qui portent celui-ci, puis
un rang **lorsque le nom se répète dans la même liste** :

```
Page 2 · ATA 21 - Air conditioning : le texte déborde du bloc de 4,5 pt — …
Page 4 · Bande n° 9 · L1 · C2 : le texte déborde du bloc de 9,8 pt — …
```

Sans cela, deux blocs anonymes donnaient deux lignes identiques, et l'import de R5, qui
nomme « Bande » chacune des seize bandes des fiches ATR et « L1 · C2 » la deuxième case de
chacune, en donnait six sur la seule page 4. Le rang ne s'écrit que là où il apprend
quelque chose : posé sur un nom déjà unique, il ferait croire à une numérotation que le
document ne porte pas.

#### Le marqueur, et pourquoi il obéit aux repères

Un petit A rouge au coin inférieur droit du bloc, comme Publisher. Il est le frère du
contrôle qui dessine le texte et non un morceau de lui — le texte est découpé au bloc, un
marqueur dessiné dedans le serait aussi, et il doit mordre le bord. Les deux lisent la
même composition et la même règle de débordement, celle que l'export applique.

**Il s'efface avec les repères** (*Affichage ▸ Repères*), ce qui s'écarte de Publisher,
qui le montre toujours. Deux raisons, et la seconde est décisive : il n'a pas plus de sens
qu'un repère sur une page qu'on regarde pour la juger ; et sans cet interrupteur, toute
page portant un débordement afficherait une tache que le PDF n'a pas, si bien que la
comparaison écran ↔ PDF cesserait de prouver quoi que ce soit — précisément sur les pages
que ce poste concerne.

#### Ce que le poste a trouvé

**Le dépôt livrait des PDF amputés, et le document de référence du contrôle P2 le
premier.** Quatre blocs de `Samples/exemple-bulletin.mypub` — dont « Colonne de gauche »,
43,56 pt, presque trois lignes de corps — et deux de `Samples/recette-polices.mypub`
perdaient du texte à l'export sans un mot. Ce n'est pas une négligence de composition :
c'est **la retombée de R4**, qui a fait de l'interligne un multiple de la hauteur
naturelle et non du corps. Les blocs avaient été dimensionnés sous l'ancienne sémantique,
où `LineHeight 1,4` valait `1,4 × corps` ; ils réclament depuis 16 % de plus. Personne ne
l'avait vu parce que rien ne le disait — ce qui est exactement la raison d'être d'A2. Les
documents sont remis à la taille de leur texte, et un test les garde désormais tous.

**Les deux règles à mesurer ne donnent pas le même nombre de lignes.** Le bloc « Rappel »
d'`exemple-bulletin` — DejaVu Sans *italique* 14 pt sur 441,9 pt de large — se compose en
**deux lignes sous PDFsharp et en trois sous Avalonia**. L'écart n'est pas de quelques
centièmes : c'est une ligne entière, soit 23,55 pt, et il fait que l'écran signalait un
débordement que l'export ne voyait pas. C'est le défaut déjà consigné à R3 — « le seuil de
rupture de colonne est évalué deux fois, sur deux mesures » — pris ici sur un cas où il se
voit à l'œil nu. Il n'est pas corrigeable dans ce poste : le supprimer demande une seule
source de mesure, ce qui est la question du seuil D. Le bloc a été agrandi de façon à
tenir sous les deux.

#### Recette

`Scripts/recettes/A2-A7-debordement-et-rognage.sh`, sur **ATA 21 d'abord** — importé,
exporté, et mesuré aux valeurs qui y sont — puis sur `Samples/recette-debordement.mypub`
pour ce que le réel ne porte pas.

Le rognage se mesure sur des **images** et jamais sur `pdftotext` : un chemin de découpe
PDF cache le texte, il ne le retire pas du flux. `pdftotext` le trouve encore — et c'est
tant mieux, la recette de R5 compte dessus pour affirmer que le texte est passé.

Mesuré : zéro pixel d'encre sous « Overheat Protection » des deux côtés, contre 5 455
pixels dans le bas du bloc — la mesure n'est pas prise à vide ; les trois calages
verticaux à 160,002, 207,198 et 254,393 pt, au centième ; le calage au milieu d'un bloc
trop petit qui rend exactement la même ordonnée que son jumeau calé en haut ; la première
ligne pendante à 57,748 pt et les suivantes à 73,466, aux valeurs d'ATA 21, avec l'encre
qui reste **hors de sa zone de texte** et dans le bloc ; et la moitié basse du filet de
2 pt d'un bloc trop plein, qui prouve que le découpage du texte n'a pas survécu au texte.

Un bloc **pivoté de 20°** porte le même débordement : le découpage est posé dans le repère
courant, donc il tourne avec le bloc, et une découpe restée en coordonnées de page ne se
verrait sur aucun autre document du dépôt.

**Les nombres d'ATA 21 sont ceux que Calibri donne**, et Calibri est présente sur la
machine qui a écrit cette recette — le poste B5 l'avait déjà relevé. Ailleurs, fontconfig
rend autre chose, le texte ne compose plus à la même hauteur, et ces nombres-là ne veulent
plus rien dire ; la recette le voit à l'avertissement de substitution que l'export émet
déjà et passe son chemin en le disant. Ce serait un filet troué si le reste en dépendait :
la règle de la marge intérieure basse — celle qui vaut à ATA 21 son silence — est donc
éprouvée une seconde fois sur le document de la recette, en DejaVu Sans, sur une paire de
blocs qui ne diffèrent que de trois points de hauteur. Le passage à vide a été joué : la
recette reste verte, et n'éprouve alors plus rien qu'elle ne prouve ailleurs.

La recette a été éprouvée en la faisant échouer, sept fois : le découpage retiré, le
découpage posé sur la zone de texte au lieu du bloc, le découpage survivant au texte, le
calage vertical oublié à l'export, l'avertissement supprimé, le calage non borné, et le
débordement mesuré sur la zone au lieu du bloc. Les tests l'ont été de même sur cinq
autres : le calage oublié à l'écran, le marqueur jamais dessiné, le rang retiré de la
désignation, le calage perdu à l'import, et une propriété oubliée dans `Clone()`. **Douze
sabotages, douze attrapés.**

#### Trouvé au passage, et laissé tel quel

**L'écran rentre son filet dans le bloc, l'export le centre sur le bord.** Mesuré sur un
filet de 2 pt : il occupe 115,0–117,0 pt à l'écran et 115,5–117,5 pt au PDF, pour un bord
de bloc à 116,69. Avalonia décale la géométrie d'une demi-épaisseur, PDFsharp non — et la
documentation de `Frame.StrokeThickness`, « centrée sur le bord du bloc », n'est donc vraie
que d'un côté. Antérieur à ce poste, visible seulement sur les filets épais, hors
périmètre.

**Le débordement horizontal est rogné et n'est pas signalé.** Un mot plus large que sa
colonne, ou un retrait de première ligne assez négatif pour sortir du bloc, est désormais
coupé des deux côtés sans un mot — A2 ne mesure que la hauteur, comme le marqueur de
Publisher. Aucun des vingt-quatre `.pub` de la machine ne produit ce cas ; il n'en est pas
moins muet.

**Un tableau ne porte pas de calage vertical.** Ses cases naissent calées en haut, et rien
ne le règle : `TableFrame` a une police, un corps et une marge, pas de fer vertical. Le
relevé n'en demande aucun.

**`TextFrame.AbsoluteLineHeight` vaut encore `FontSize × LineHeight`.** C'est l'ancienne
sémantique, celle que R4 a remplacée par un multiple de la hauteur naturelle. Aucun des
deux rendus ne la lit — ils passent tous deux par `Compositeur.Avance` —, mais les
vignettes du catalogue (`TemplatePreview`) s'en servent encore, et elle ment du même écart
de 16 % qui a fait déborder le document de référence. B1 assume déjà que la vignette est
un schéma et non un rendu, si bien que rien ne se voit ; c'est la même forme que la
découverte sur le filet — une propriété survivante d'une sémantique changée ailleurs.

### A3 — Chemins d'images relatifs *(fiche 21)*

À l'enregistrement, écrire le chemin en relatif quand le fichier image se trouve sous le
dossier du document ; en absolu sinon. À l'ouverture, résoudre les deux.

*Recette* : déplacer un dossier contenant un `.mypub` et ses images ne casse plus rien.

*Fait le 6 septembre 2026.* La règle est celle du plan, à la lettre : relatif sous le
dossier du document, absolu ailleurs, et les deux se relisent. Un chemin qui remonterait
par `..` est écarté et reste absolu — il se casserait au premier déplacement, ce qui est
exactement ce que le poste répare. Le « sous le dossier » se mesure par
`Path.GetRelativePath` et non par un préfixe de chaîne : `…/notice` et
`…/notice-images` partagent le préfixe sans que le second soit dans le premier.

**La résolution a lieu à l'ouverture, une fois pour toutes.** C'est le choix qui commande
tout le reste. Passé `FromJson`, le modèle ne porte que des chemins absolus : ni la
liaison de l'écran, ni l'export, ni le cache d'images n'ont à savoir qu'un chemin relatif
existe, et aucun d'eux n'a changé d'une ligne. Le poste tient donc entier dans
`DocumentSerializer`, et il n'ajoute **aucune propriété au modèle** — ni `Clone()`, ni le
test d'exhaustivité par réflexion, ni `NotifySelection()` n'entrent en jeu.

**Le dossier est un paramètre de l'écriture comme de la lecture, et son absence a un
sens précis** : les chemins passent alors tels quels, ni relativisés ni résolus. C'est ce
qui règle le piège du poste. `MainWindowViewModel.Capture` sérialise le document à chaque
geste et `Restore` le relit — tous deux **sans dossier**, donc l'aller-retour est
l'identité sur les chemins, quelle que soit leur forme. Un geste annulé ne peut pas
déplacer une image. La demi-mesure serait mortelle : un instantané qui relativiserait
sans que la restauration résolve rendrait le chemin relatif *en mémoire*, où il se
résoudrait ensuite sur le dossier courant du processus — l'image disparaîtrait de la page
sans qu'aucun geste ne l'ait touchée. C'est le cinquième sabotage ci-dessous, et il est
attrapé.

Ce dossier facultatif appelle un garde-fou, et c'est un invariant plutôt qu'un test :
**`DocumentSerializer.Save` est le seul endroit du dépôt qui écrive un `.mypub` sur le
disque**. Les deux seuls appels qui ne passent pas de dossier — l'instantané de
l'historique et sa comparaison — ne touchent aucun fichier. Un second écrivain, l'archive
du poste B4 par exemple, devra passer le sien : sans quoi il graverait des chemins
absolus sans que rien n'échoue, le contrôle ne portant sur aucun document à image. La
consigne est écrite là où on la lira, sur `Save`.

**La réécriture n'est pas un « enregistrer sous ».** `--resave` rapporte les chemins au
dossier de la **source** et non à celui du fichier produit. Ce n'est pas une commodité :
c'est la procédure de régénération des `Samples/` et des `Templates/` — `--resave /tmp/x`
puis recopie par-dessus l'original — et sous une sémantique d'« enregistrer sous » elle
graverait dans un modèle livré le chemin absolu de la machine qui l'a régénéré, en
silence. Le vrai « enregistrer sous », lui, relativise bien sur le nouveau dossier ; il
est éprouvé par un test du noyau, l'application n'ayant pas de mode sans interface pour
ce geste-là. Conséquence assumée : un modèle qui porterait une image relative, ouvert en
copie et enregistré ailleurs, écrit cette image en absolu — ce qui est juste, elle vit
bien ailleurs.

**Le cache d'images de `Conv.cs` n'a pas eu à suivre, et c'est le résultat qui compte.**
Il est statique, indexé par chemin, et son `Invalidate` n'est appelé par personne : si le
modèle avait gardé la forme relue du fichier, deux documents rangés dans deux dossiers et
portant chacun `images/logo.png` se seraient partagé une seule entrée, le second
affichant l'image du premier. La résolution à l'ouverture ferme ce cas ; la clé est en
outre normalisée, sans quoi `Invalidate` ne retrouverait pas une entrée rangée sous une
autre graphie. Ce raisonnement est une affirmation sur du code : un test d'interface en
fait une mesure, avec deux documents et deux images du même nom dans un seul processus.

`FormatVersion` **reste à 3**, et c'est le seul endroit où l'on aurait pu choisir
autrement. Un fichier ancien ne porte que des chemins absolus et s'ouvre inchangé : rien
à migrer. Mais un fichier neuf portant un chemin relatif, ouvert par un binaire
antérieur, verrait ce chemin résolu sur le dossier courant du processus et perdrait le
lien **sans rien dire**, là où un numéro plus grand l'aurait fait refuser avec son
numéro. Un seul chemin y mène — enregistrer avec cette version, puis rouvrir avec une
plus ancienne, c'est-à-dire régresser de binaire — et c'est ce qui rend le choix
défendable plutôt que seulement permis. L'asymétrie est réelle ; elle est écrite ici
plutôt que tue.

*Recette jouée*, et elle est un script : `Scripts/recettes/A3-chemins-images.sh`. Un
atelier avec un `.mypub`, une image sous son dossier et une image au-dehors ; le dossier
est déplacé pour de bon — `mv`, l'ancien chemin n'existe plus — et le document s'ouvre
encore. Ce qui est mesuré n'est pas l'absence d'un avertissement mais **la couleur des
deux aplats relevée dans la page rendue**, au centre de chaque bloc : un export qui ne se
plaint de rien peut très bien avoir dessiné un trou. La forme écrite est vérifiée des
deux côtés — `images/plan.png` d'une part, le chemin absolu de l'autre — et la recette
finit par une contre-épreuve : déplacer le dossier de l'image du dehors doit la casser.
Sans elle, le passage précédent pourrait tenir sur un export qui ne dit jamais rien.

La recette a été éprouvée en la faisant échouer, six fois : l'écriture qui ne relativise
plus, la lecture qui ne résout plus, `--resave` rendu à la sémantique d'« enregistrer
sous », le garde-fou du `..` retiré, l'instantané d'annulation qui relativise sans que la
restauration résolve, et le cache indexé sur le nom du fichier. **Six sabotages, six
attrapés** — quatre par la recette, trois par les tests du noyau, quatre par ceux de
l'interface. Deux ne sont attrapés que par les tests d'interface, et ce sont les deux
qu'on ne voit pas venir : l'annulation et le cache. Un seul n'est attrapé que par la
recette, `--resave` — le contrôle de non-régression, lui, ne le verrait pas, aucun
document du dépôt ne portant d'image.

#### Ce que le poste a trouvé

**Les documents ATR ne portent aucune image liée**, et il faut le dire plutôt que de
laisser croire que la recette a évité le sujet. Les cinq PNG d'ATA 21 sont des
remplissages de forme (`draw:fill: bitmap`) que l'import de R5 ne reprend pas — le relevé
le dit, le README le répète. Il n'y a donc pas de valeur réelle à reprendre ici comme
R2 et R3 ont pu le faire : ce que la recette reproduit est le **geste** réel que la
fiche 21 désigne comme le cas de casse le plus fréquent, « j'ai déplacé mon dossier de
travail », joué littéralement.

**Une couleur symétrique est un mauvais témoin.** Le premier test d'interface relevait le
magenta, dont le rouge et le bleu sont égaux : il passait alors même que la lecture des
pixels inversait les deux voies. C'est le cyan qui l'a dit — la trame composée par
`CaptureRenderedFrame` est en RGBA et non en BGRA. Une heure perdue, et une leçon qui
vaut au-delà de ce poste : un témoin invariant par l'erreur qu'on cherche ne mesure rien.

**L'écriture passe désormais par l'arbre JSON** plutôt que par la sérialisation directe —
réécrire un chemin sur le bloc lui-même notifierait la liaison et ferait relire l'image à
l'écran, et laisserait le document modifié si l'écriture échouait ensuite. Vérifié avant
de bâtir dessus : à dossier absent, l'arbre rend **exactement** le même texte que la
sérialisation directe, dérives d'arrondi et caractères non-ASCII compris. Les dix
`.mypub` livrés — six exemples, quatre modèles — se réécrivent au caractère près, et
aucun n'a eu à être régénéré : le poste n'ajoute aucune propriété au fichier.

#### Trouvé et laissé tel quel

**Le cache d'images retient aussi les échecs, et pour toujours.** Une image absente à
l'ouverture est mémorisée comme telle pour la durée du processus : la remettre en place
puis rouvrir le document laisse le bloc vide, et rien ne le dit. `Invalidate` existe
précisément pour cela et personne ne l'appelle. Hors périmètre — c'est un défaut
antérieur au poste, que le poste ne touche pas — mais il attend son correctif, et
l'ouverture d'un document en serait le bon endroit.

**Un `.mypubz` reste nécessaire** : ce poste traite le dossier déplacé, pas le document
envoyé seul. C'est le poste B4, et la fiche 21 les distinguait déjà.

### A4 — Ordre de superposition : raccourcis et glisser-déposer *(fiche 15)*

Les quatre commandes existent et sont saines ; il leur manque des raccourcis clavier, et
la liste « Blocs (du fond vers l'avant) » invite à un glisser-déposer qui n'existe pas.

*Recette* : réordonner un bloc au clavier, puis en le tirant dans la liste.

*Fait le 6 septembre 2026.* `Ctrl+Maj+F` et `Ctrl+Maj+B` portent la sélection au premier
plan et à l'arrière-plan, et le menu Disposition les affiche. **Deux raccourcis et non
quatre** : la fiche 15 relève que Publisher n'en donne qu'aux deux extrêmes, et avancer
ou reculer d'un rang n'en a pas — on n'en invente pas. Publisher 2007 les posait sur
`Alt+F6` et `Alt+Maj+F6` ; ce sont les combinaisons modernes qui sont reprises, la
plupart des gestionnaires de fenêtres Linux captant `Alt+F6` avant l'application. Un
raccourci qui n'arrive jamais jusqu'à son destinataire ne vaut pas mieux qu'aucun.

La liste des blocs se réordonne en y tirant un bloc, un trait bleu marquant où il se
posera. **Le geste est bâti sur les pointeurs et non sur `DragDrop.DoDragDrop`** : la
plate-forme sans affichage n'inscrit aucune source de glisser-déposer — vérifié dans
`Avalonia.Headless` 11.3.9, qui offre bien la réception d'un dépôt mais pas son émission
— si bien qu'un geste bâti sur elle ne serait jouable par aucun banc, et ce poste
n'aurait été éprouvé que sur parole. C'est au demeurant l'idiome de la planche, qui
n'utilise pas davantage le survol des contrôles.

#### Ce que le poste a trouvé

**Les quatre commandes de superposition perdaient leur sélection**, et le défaut est
antérieur au poste. La liste des blocs est liée à la fois à la collection de la page et
à l'ancre de sélection ; remuer la collection fait passer la sélection de la liste par
zéro, et la liaison à double sens redescend cette remise à zéro dans le modèle de vue.
Le bloc qu'on vient de porter au premier plan cesse donc d'être pris.

Deux choses méritent d'être dites. D'abord, **le moyen n'y est pour rien** :
`ObservableCollection.Move` perd la sélection exactement comme un retrait suivi d'une
insertion. C'était l'hypothèse de départ, et elle est fausse — mesurée sur
`BringForward`, qui employait déjà `Move` et perdait la sélection tout autant. Ensuite,
le défaut ne devient gênant qu'une fois le geste au clavier : au menu on reclique sans y
penser, au raccourci on enchaîne les frappes et la deuxième ne fait plus rien. La
sélection est donc reposée après le remaniement et **avant** la fermeture de la
transaction, pour que l'instantané d'annulation garde celle qu'on voit et non une
sélection vide.

Au passage, `StepOrder` interroge désormais une copie de la sélection : la boucle
d'origine consultait `Selection` à chaque tour, alors que le premier déplacement venait
de la vider.

**Le seuil de glissement ne fait pas ce qu'on croyait, et c'est le sabotage qui l'a
dit.** Ramené à zéro, il ne change rien à l'ordre de la pile : le rang d'insertion est
un point *entre* deux blocs, et un pointeur qui tremble de deux pixels rend le rang dont
il est parti — le déplacement est alors sans objet. Ce que le seuil tient, c'est le
geste : sans lui, le moindre frémissement capture le pointeur et allume le trait
d'insertion sous le doigt de quelqu'un qui ne fait que cliquer. Le test a été réécrit
pour vérifier cela, qui est vrai, plutôt que ce qu'on lui prêtait.

### A5 — Formats de page *(fiche 01)*

Deux champs largeur/hauteur libres dans le panneau DOCUMENT — le modèle les accepte déjà,
ce sont des `double`. Et quatre formats à ajouter à la liste : B4, B5, Executive, Tabloid.

*Recette* : composer sur un format 100 × 200 mm et l'exporter à cette taille exacte.

*Fait le 6 septembre 2026.* Neuf formats au catalogue au lieu de cinq — B4 et B5 aux
dimensions ISO, Executive et Tabloid en pouces exacts — et deux champs en millimètres
dans le panneau DOCUMENT. `FormatVersion` reste à 3 : aucune propriété n'est née, ce sont
les deux `double` que le document portait déjà.

**Le sélecteur suit les dimensions, jamais l'inverse.** `PageSize.Match` rend `null`
devant une taille qu'aucune entrée ne reconnaît, et c'est un état légitime — la vignette
« Create custom page size… » du catalogue de Publisher — et non une absence de réponse.
Ce qu'il ne faut surtout pas en conclure, c'est qu'il faille effacer les dimensions.

Pour que le sélecteur se vide quand on saisit une taille libre, le modèle de vue
s'abonne désormais aux dimensions du document. Le point délicat n'est pas l'abonnement
mais sa durée de vie : **le document se remplace en bloc** — ouverture, modèle, page
neuve, annulation — et un abonnement pris une fois pour toutes laisserait le sélecteur
muet dès la première annulation, tout en gardant l'ancien document en vie.

**Poser un format écrit deux dimensions l'une après l'autre**, et le document porte
entre les deux une largeur de B5 avec une hauteur de format libre. Ré-accorder le
sélecteur sur cet état-là rejouerait le gestionnaire qui vient de l'appeler. Le verrou
couvre donc l'écriture entière et non chaque ligne ; ce qui le garde est un test qui
compte les pas d'annulation — un geste, un pas.

*Recette jouée*, `Scripts/recettes/A5-formats-de-page.sh`, sur
`Samples/recette-formats.mypub`, une page de 100 × 200 mm. Le PDF **mesure**
283,465 × 566,929 pt, relu deux fois par deux outils — `pdfinfo` lit la boîte déclarée,
`pdftotext -bbox` la redit à sa façon —, le contenu y est posé à 44,000 pt du bord
gauche (les 40 pt du bloc et ses 4 pt de marge intérieure), l'écran rend la même page à
la même taille, et les quatre formats ajoutés sortent aux dimensions de leur norme, au
centième de point.

### A6 — Taille du bloc dans la barre d'état *(fiche 02)*

Publisher affiche en permanence position **et** taille du bloc sélectionné. MyPublisher
n'affiche que la position du curseur. Pendant un redimensionnement, l'œil est sur la
page, pas sur le panneau de droite.

*Recette* : la taille se lit sans quitter la page des yeux pendant le geste.

*Fait le 6 septembre 2026.* Deux cases de plus dans la barre d'état, la position du bloc
et sa taille, séparées comme la fiche 02 les relève. Elles ne s'affichent qu'à la
sélection, et les quatre cases de droite sont désormais de largeur fixe : elles se
rafraîchissent à chaque mouvement de pointeur, et une barre dont les cases se déplacent
à chaque chiffre ne se lit plus.

**À un bloc, c'est son cadre ; à plusieurs, l'empreinte de l'ensemble.** Choix
délibéré : les quatre nombres annoncés pour un bloc seul sont exactement ceux que le
panneau donne à saisir, si bien qu'un chiffre lu en bas et un chiffre lu à droite ne
peuvent pas se contredire. La différence se voit sur un bloc tourné, dont l'empreinte
déborde le cadre — et c'est bien le cadre qu'on annonce quand il est seul.

**La mesure suit le geste et non sa fin.** Le plan de travail écrit la géométrie du bloc
à chaque mouvement du pointeur sans prévenir personne d'autre : le modèle de vue s'abonne
donc aux blocs sélectionnés. Sans cela, la barre ne se mettrait à jour qu'au
relâchement — or c'est pendant qu'on la regarde.

*Recette jouée*, `Scripts/recettes/A6-taille-dans-la-barre.sh`. Ce poste n'a rien qu'un
fichier livré puisse porter : la recette appelle les tests d'interface par leur poste et
vérifie qu'ils ont été joués, un filtre qui ne désigne plus rien sortant en 0.

Ce que ces tests éprouvent, et pourquoi ils ne sont pas une mise en scène : le bloc est
sélectionné **avant** la première image, si bien que la case affiche déjà une taille et
que seule une taille qui change peut en remuer les pixels ; l'image est prise par
`CaptureRenderedFrame` et non par `RenderTargetBitmap.Render`, qui redessine de force et
passerait même si plus rien ne se rafraîchissait ; et le découpage porte sur la seule
case de la taille, non sur la barre entière — la position du curseur, deux cases plus
loin, change à chaque mouvement de pointeur, et une comparaison large aurait vu sa
différence à elle.

### A4, A5, A6 — ce que les sabotages ont appris

*6 septembre 2026.* Les trois recettes ont été éprouvées en les faisant échouer, seize
fois. **Douze sabotages attrapés tels que les recettes étaient écrites, quatre qui ont
demandé de réparer le contrôle avant qu'il ne les voie** — et ce sont ces quatre-là qui
ont servi. Le compte est donné dans ce sens plutôt que dans l'autre : une recette qui
laisse passer son propre défaut n'est pas verte, elle est aveugle, et l'écrire douze sur
quatorze reviendrait à s'en dispenser.

| Sabotage | Attrapé par |
|---|---|
| `Ctrl+Maj+F` retiré des `KeyBindings` | recette A4, lecture des sources |
| `Ctrl+Maj+B` déclaré deux fois | recette A4, lecture des sources |
| la ligne du README retirée | recette A4 — **au second essai** |
| le rang d'insertion toujours nul | recette A4, tests de geste |
| la sélection non reposée après remaniement | recette A4, trois tests |
| le seuil de glissement à zéro | recette A4 — **au second essai** |
| le trait d'insertion jamais allumé | recette A4 — **assertion ajoutée après coup** |
| le trait d'insertion toujours en tête de liste | recette A4, même assertion |
| l'export ignorant la hauteur de page | recette A5, quatre mesures sur cinq formats |
| le sélecteur reposant l'A4 devant un format libre | recette A5, mesure de l'écran |
| choisir un format ne faisant plus rien | recette A5, tests du panneau |
| B5 mal mesuré au catalogue | `FormatsDePageTests`, puis recette A5 — **après correction du test** |
| l'abonnement à la géométrie du bloc coupé | recette A6, comparaison de pixels |
| l'abonnement jamais pris | recette A6 |
| la ligne manquante dans `NotifySelection` | recette A6 — **après réécriture du test** |
| les cases de la barre renommées | recette A6 |

**Le premier raté : une recette qui cherchait au mauvais endroit.** Le contrôle des
raccourcis confrontait les gestes déclarés au README *entier*. Retirer la ligne du
tableau ne le faisait pas tomber — les deux raccourcis étant aussi cités dans la prose
d'à côté. Il cherche désormais dans la seule section « Raccourcis », qui est l'endroit
où l'on va chercher un raccourci. Un contrôle qu'on n'a jamais vu échouer ne prouve rien,
et celui-là aurait passé le poste sans rien garder.

**Le second raté : une propriété calculée rend la bonne valeur même quand personne n'a
été prévenu.** Le test de la barre d'état à plusieurs blocs interrogeait
`vm.FrameSizeLabel` ; retirer la ligne de `NotifySelection` — celle-là même dont le
dépôt dit qu'elle s'oublie — ne le faisait pas tomber, puisque lire une propriété
calculée la recalcule. Le test lit maintenant la **case** et non le modèle de vue.

Ce raté-là en a découvert un autre, qu'il faut consigner : à un seul bloc, la barre se
rafraîchit **par accident** même sans cette ligne. Sélectionner un bloc fait passer sa
position par le panneau de propriétés, donc par l'aller-retour points ↔ millimètres qui
laisse 7·10⁻¹⁴ pt derrière lui — la dérive mesurée au poste P3, réputée sans effet. Elle
en a un : elle notifie. C'est le cas à plusieurs blocs, où le panneau ne montre aucun
champ, qui garde réellement la ligne de `NotifySelection`.

**Ce que le sabotage du seuil de glissement a appris** est écrit au poste A4 : ce n'est
pas lui qui empêche un clic de réordonner la pile, c'est l'arithmétique du rang
d'insertion. Le test disait le contraire ; il a été réécrit sur ce que le seuil fait
vraiment.

**Le trait d'insertion n'était éprouvé par rien.** Le test du glissé mesurait l'ordre
obtenu et l'annulation, sans jamais regarder pendant le geste ; le vider entièrement
laissait la recette verte. Or c'est le seul retour que le geste donne — sans lui, on
tire à l'aveugle. Le test l'exige désormais allumé **et à sa place**, entre les deux
mouvements de pointeur, puis éteint au relâchement ; les deux sabotages
correspondants tombent.

**Les recettes compilent, elles ne se contentent pas de ce qui traîne dans `bin/`.**
La première écriture lançait les tests avec `--no-build` et se bornait à vérifier que
la bibliothèque existait. C'est exactement le piège que R3 a relevé — un binaire périmé
survit à une modification des sources — reposé dans un script dont la raison d'être est
d'être rejouable seul, y compris par quelqu'un qui vient de saboter un fichier. Elles
compilent maintenant elles-mêmes et s'arrêtent sur une compilation cassée, comme
`Scripts/controle.sh` le fait depuis P2. Vérifié en sabotant une source et en lançant la
recette **sans** compiler : elle tombe.

**Une correction de comptage, à déclarer.** La ligne « par phase » annonçait « R : 4 sur
5 » alors que les cinq lignes de la phase R portent 100 % depuis la livraison de R5 ;
le total de 15 en héritait. Corrigé à 5 et à 19, la présente livraison comprise.

#### Trouvé, hors périmètre, non corrigé

- **Les bornes du format libre sont celles du champ, pas du format.** Le panneau plafonne
  à 10 et 2 000 mm ; un `.mypub` écrit à la main ou importé peut porter n'importe quel
  nombre, et rien ne le ramène. C'est le même parti que le plafond de vingt colonnes de
  R3 — un garde-fou de saisie. **Vérifié plutôt que supposé** : ce sont les premiers
  `Minimum` et `Maximum` du panneau posés sur une liaison à double sens vers la
  géométrie, et C2a a déjà vu un champ altérer en silence la valeur qu'il affichait. Un
  document de 3 000 mm et un de 5 mm s'ouvrent sans que leur page ne bouge d'un
  milliardième de point ; deux cas de test le gardent, faute de quoi un `.pub` de
  bannière — Publisher monte à 240 pouces — serait redimensionné à la seule ouverture.
- **Le glissé dans la liste ne fait pas défiler celle-ci.** Le rang d'insertion se lit
  sur les éléments réellement construits, et la liste ne construit que ce qu'elle
  affiche : sur une page comptant plus de blocs que la liste n'en montre, on ne peut pas
  tirer le quarantième jusqu'en tête. Le geste reste juste, il est seulement borné à la
  bande visible. Les raccourcis, eux, ne connaissent pas cette limite.
- **Les deux cases neuves de la barre d'état disparaissent sans sélection**, ce qui
  décale les cases voisines au moment où l'on sélectionne. Publisher les laisse vides.
  Retenu ainsi pour ne pas montrer deux cases muettes en permanence ; à revoir si le
  décalage gêne.

### A7 — Alignement vertical du texte *(fiche 12)*

Trois valeurs — haut, milieu, bas. Propriété nouvelle et facultative, donc sans
migration. Immédiat à l'écran, calculable à l'export à partir de la hauteur mesurée.

*Recette* : un titre centré verticalement dans son bloc l'est aussi dans le PDF.

*Fait le 6 septembre 2026*, **avec A2 et le rognage à l'export** : les trois demandent la
même mesure, et le compte rendu est [au poste A2](#a2--signaler-le-débordement-du-texte-fiche-12).

### A8 à A15 — ce que l'audit des fiches avait trouvé

*Reconstitué le 11 octobre 2026.* L'audit du 6 septembre avait nommé onze postes sans en
écrire la liste, et elle s'est perdue avec les transcriptions de la machine où il a été
fait. Un seul numéro a survécu, dans un commentaire de `PubImporter` : **A10, l'objet
trait**. Les autres ont été retrouvés en reprenant les 22 fiches une à une, puis en
classant chaque écart : couvert par un poste, écarté, renvoyé au seuil D, ou sans poste.
Les écarts sans poste se regroupent d'eux-mêmes en onze postes. L'attribution des
numéros, hors A10, est donc refaite et non retrouvée.

#### A8 — Insérer des pages : nombre, position, contenu *(fiche 08)*

`AddPage` insère une page vierge après la page courante, et `DuplicatePage` copie la
page courante. Publisher demande dans un même dialogue un **nombre** de pages, une
**position** (avant ou après) et un **contenu** : page vierge, une zone de texte par
page, ou copie des objets d'une page désignée.

*Recette* : document de 3 pages, page 2 courante ; insérer 4 pages *avant*, copiées de la
page 1. On doit trouver 7 pages, et les pages 2 à 5 portent autant de blocs que la
page 1, en instances distinctes. Les noms sont renumérotés, et une seule annulation rend
les 3 pages. L'option « zone de texte » pose exactement un bloc de texte, égal à la zone
des marges. Effort : faible.

#### A9 — Rotation par quart de tour *(fiches 14, 22)*

Deux commandes, +90° et −90°, au menu Disposition et au panneau. La rotation, sa poignée
et l'export tourné existent depuis A1 : il manque le geste rapide.

*Recette* : 350° plus 90° donne 80°, et 0° moins 90° donne 270°. Une image tournée d'un
quart de tour passe `comparer-rendus.sh` sous la tolérance, et chaque commande
s'annule en un pas. Effort : faible.

#### A10 — Objet trait, et flèche *(fiche 02)*

`ShapeKind` ne connaît que le rectangle et l'ellipse. L'import des « Fiches
limitations » rend ses cinq traits par des rectangles sans épaisseur, ce qui se voit
juste mais se manipule mal. Il faut un `ShapeKind.Line`, une pointe de flèche
facultative à chaque bout, et un survol qui tolère quelques points autour du trait.

*Piège* : `Snap.MinFrameSize` vaut 2 mm, alors qu'un trait horizontal a une hauteur
nulle. Le trait se définit donc par ses deux extrémités, et non par un cadre.

*Recette* :
- `"Shape": "Line"` fait l'aller-retour JSON au caractère près ;
- dans le flux PDF, le bloc est un tracé (`m … l S`) et non un rectangle rempli ;
- le survol répond à moins de 3 pt du trait, et plus au-delà ;
- l'import des Fiches rend 5 traits et 0 rectangle de substitution, et la recette R5
  est ajustée en conséquence.

Effort : moyen.

#### A11 — Aimantation réglable, et aux autres blocs *(fiche 22)*

Seul `Alt` coupe aujourd'hui l'aimantation, et seulement le temps d'un geste. Publisher
offre un réglage durable, et l'aimantation aux autres objets. `Snap.Edge` est une
fonction pure ; il suffit de lui donner, en plus des repères, les bords et les milieux
des blocs non sélectionnés.

*Recette* (test pur) : un bloc A en x = 100, de 50 de large ; un bloc B lâché en x = 148
se cale à 150 quand l'aimantation aux objets est active, et reste à 148 sinon. Avec
l'aimantation coupée, rien ne se cale, pas même sur la marge. Le réglage survit à la
relecture des préférences. Effort : moyen.

#### A12 — Menu contextuel du bloc *(fiche 11)*

Un clic droit sur un bloc le sélectionne et ouvre un menu qui reprend les commandes
d'Édition et de Disposition. Aucun `ContextMenu` n'existe dans les vues. Le poste est à
faire **après** A9 et B2, pour reprendre leurs commandes.

*Recette* :
- un clic droit synthétisé au centre d'un bloc le sélectionne et ouvre le menu ;
- un clic droit hors de tout bloc vide la sélection ;
- un script vérifie que chaque commande du menu est liée à la même commande que son
  pendant dans les menus Édition et Disposition.

Effort : faible.

#### A13 — Rognage manuel de l'image *(fiche 14)*

Un rognage facultatif sur les quatre côtés d'un `ImageFrame`, réglé au panneau puis par
un mode de poignées. La découpe existe déjà pour le remplissage par image (C5c) : les
deux doivent partager le même calcul, dans le noyau.

*Recette* : une image de 200 × 100 px rognée de 50 % à gauche. À l'écran comme au PDF,
le rendu est la moitié droite de la source (écart ImageMagick sous un seuil). Un
fichier sans rognage reste identique octet pour octet. Effort : moyen.

#### A14 — Contour et ombre du caractère *(fiche 17)*

C'est ce que « Ce que ce plan ne fait pas » promettait à la place de WordArt : « un
titre qui se voit », par le contour, l'ombre et le dégradé. La ligne renvoyait à C5c,
qui n'a fait que des fonds de bloc : le texte, lui, n'a rien reçu. Le poste porte le
contour (couleur, épaisseur) et l'ombre (décalage, couleur) sur le bloc de texte.

*À éprouver d'abord* : que PDFsharp sache écrire un texte en mode contour. Faute de
quoi, il faudrait tracer les glyphes un par un, et le poste change de taille.

*Recette* : avec contour, le flux PDF porte un mode de rendu `1 Tr` ou `2 Tr` ; avec
ombre, le texte est écrit deux fois, au décalage demandé ; `comparer-rendus.sh` passe
sous la tolérance. Effort : moyen, voire lourd si PDFsharp ne suit pas.

#### A15 — Sections : numérotation qui repart *(fiches 08, 22)*

Un début de section, facultatif, sur `Page`, avec son premier numéro. Le folio de C4
compte alors depuis la dernière section ouverte.

*Recette* : 8 pages, une section ouverte en page 3 et numérotée à partir de 1.
`pdftotext`, page par page, lit 1, 2, 1, 2, 3, 4, 5, 6 ; un document sans section se
numérote comme avant. Effort : faible.

---

## Phase B — Démarrage et transport

La phase A polit ce qui existe. La phase B change ce que le logiciel *est* pour qui
l'ouvre. Aucun de ces postes ne touche au modèle de données.

### B1 — Modèles *(fiche 18)* — **le poste le plus rentable du plan**

Un modèle n'est rien d'autre qu'un document : le format `.mypub` suffit déjà, et
`Samples/exemple-bulletin.mypub` prouve qu'il porte un document réaliste. Il manque
trois choses, aucune structurelle :

1. un dossier `Templates/` livré avec l'application ;
2. un écran de choix au démarrage et sur `Ctrl+N` — grille de vignettes, plus l'entrée
   « document vierge » ; les vignettes se génèrent en rendant la première page ;
3. l'ouverture **en copie** : charger le document mais laisser le chemin de fichier à
   vide, pour que `Ctrl+S` propose *Enregistrer sous* et n'écrase jamais le modèle.

Puis une poignée de modèles réellement composés : bulletin, dépliant trois volets,
carte de visite, affiche.

*Recette* : `Ctrl+N`, choisir « dépliant », modifier un texte, enregistrer — le fichier
d'origine est intact.

*Fait le 5 septembre 2026.* Les trois pièces annoncées, et rien de plus : un dossier
`Templates/` livré à côté du binaire, un catalogue au démarrage et sur `Ctrl+N`, et
l'ouverture en copie — le document est chargé sans son chemin, si bien qu'« Enregistrer »
propose « Enregistrer sous ». Aucune ligne n'a été ajoutée au modèle de données : un
modèle est un `.mypub` ordinaire, lu par le même code que les documents de l'utilisateur.

Quatre modèles composés : bulletin trois colonnes sur deux pages, dépliant trois volets,
carte de visite, affiche A3. Ils se servent de ce que les postes précédents ont apporté —
grille et lignes de base (C2a), repères de pli (C2b), folio et date (C4a), coordonnées et
logo (C4b), groupes (C3c), fond et contour (C1). C'est la contrepartie annoncée : la
moitié « coordonnées » de C4 sert enfin à quelque chose, et une carte de visite dont les
neuf blocs sont des champs se remplit toute seule.

*Écart assumé* : les vignettes sont un schéma et non un rendu. À cent cinquante pixels de
large, un A4 réduit son corps de onze points à deux pixels — un rendu fidèle ne montrerait
qu'une bouillie grise. Ce qu'on choisit dans un catalogue, c'est une structure ; c'est
donc elle qu'on dessine, le texte en filets et l'image en cadre barré. Cela évite au
passage un troisième moteur de rendu, alors que tout l'équilibre du projet tient à ce que
l'écran et l'export partagent le modèle sans couche intermédiaire.

Recette jouée : partir du dépliant, vérifier qu'aucun chemin n'est repris, modifier le
titre de couverture, enregistrer ailleurs — le fichier du modèle est intact octet pour
octet, et la copie porte bien la modification. Les quatre modèles s'exportent sans un
avertissement.

*Trouvé au passage*, et corrigé avant : l'application suivait la variante de thème du
système alors que sa palette est écrite en clair ; sur un bureau sombre, champs et menus
sortaient sombres au milieu de panneaux clairs. Corrigé aussi, dans ce poste : un bloc
image sans chemin est un emplacement qui attend son image — c'est ainsi qu'un modèle
réserve sa place — et non une image perdue ; il ne se signale donc plus à l'export.

C'est aussi le seul moyen de rendre visibles les fonctions **déjà présentes** :
l'aimantation, les modes de cadrage d'image, la justification. Un utilisateur qui
ouvre une page blanche n'en découvre aucune.

### B2 — Importer un fichier texte *(fiche 22)*

Un sélecteur de fichier qui remplit le texte du bloc. C'est le chemin naturel pour faire
entrer du contenu rédigé ailleurs — et aujourd'hui il n'y en a aucun, hors copier-coller.

*Recette* : un `.txt` de 3 000 signes arrive dans un bloc, et A2 signale qu'il déborde.

### B3 — Imprimer *(fiche 22)*

MyPublisher exporte un PDF mais n'imprime pas. Pour un logiciel de PAO, c'est un trou
dans le parcours : le PDF peut servir d'intermédiaire, mais l'utilisateur ne devrait pas
avoir à le savoir.

*Recette* : `Ctrl+P` sort le document sur une imprimante système.

### B4 — Archive `.mypubz` *(fiche 21)*

Un zip contenant le JSON, un dossier `images/` et la liste des polices employées. Le
sérialiseur actuel fait 32 lignes ; une classe voisine suffit, le choix se fait au filtre
du sélecteur d'enregistrement. A3 traite le cas courant ; B4 traite le cas du transport.

*Recette* : ouvrir sur une autre machine une archive produite ici, images comprises.

### B5 — Avertir sur les substitutions de polices *(fiche 21)*

La substitution est aujourd'hui totalement muette : le résolveur accepte le fichier que
lui rend `fc-match` sans vérifier qu'il correspond à la famille demandée, et retombe
silencieusement sur DejaVu Sans dans trois cas — `fc-match` absent, police introuvable,
ou fichier `.ttc` que PDFsharp ne gère pas. Il a tout ce qu'il faut pour signaler
l'écart ; il ne le remonte à personne.

*Recette* : exporter un document demandant une police absente produit un avertissement
nommant la police demandée et celle réellement employée.

*Fait le 6 septembre 2026.* `fc-match` est désormais interrogé sur le fichier **et** la
famille, et l'export confronte ce qu'il obtient à ce que le document demandait :

```
Police substituée : « Verdana Pro Cond SemiBold » demandée, « Noto Sans » employée
                    (absente de cette machine).
Police substituée : « Noto Sans CJK JP » demandée, « DejaVu Sans » employée
                    (collection .ttc, que l'export ne sait pas lire).
```

Le motif entre parenthèses n'est pas décoratif : sans lui, la seconde ligne serait
incompréhensible — la police est installée, et pourtant remplacée.

**Un avertissement par police, et non par bloc.** Les fiches ATR portent 119 fragments
en Wingdings ; un avertissement par bloc en ferait une liste que personne ne lit. C'est
la police qui est substituée, pas le bloc — comme « Image introuvable » nomme le fichier
et non le cadre qui l'affiche. Le dédoublonnage porte sur la paire demandée/employée :
le romain et le gras d'une même famille absente tombant sur le même substitut ne
valent qu'une ligne, mais s'ils divergent, les deux se disent.

**Le relevé se prend sur le document, pas pendant le dessin.** PDFsharp garde en cache
la résolution d'une police pour toute la durée du processus et n'appelle donc le
résolveur qu'une fois par famille : un relevé pris au vol serait complet au premier
export et **vide au second** — or l'application exporte plusieurs fois de suite, et
c'est le dernier PDF qu'on envoie. La passe lit les polices sur le modèle : celle du
bloc, et celle de chacun de ses fragments, groupes traversés. Elle ne peut donc pas
davantage être cassée par un remaniement de `DrawText`.

#### Ce que le poste a trouvé

**fontconfig rend *tous* les noms d'une famille, séparés par des virgules.** « DejaVu
Sans Condensed » lui vaut la réponse `DejaVu Sans,DejaVu Sans Condensed`, « Lato
Semibold » vaut `Lato,Lato Semibold`, et une police coréenne vaut `Malgun Gothic,맑은
고딕`. Comparer la chaîne entière au nom demandé — ce que la première écriture faisait —
annonçait une substitution sur **81 polices installées de cette machine**. C'est le seul
défaut qui aurait tué le poste : un avertissement qui crie au loup sur des polices
présentes s'apprend à ne plus être lu, et le jour où une flèche de procédure d'urgence
est vraiment remplacée, personne ne regarde. La comparaison cherche donc le nom demandé
**parmi** les noms rendus.

Ce cas-là ne s'éprouve qu'avec une police à noms multiples réellement installée. La
recette et le test vont donc en chercher une sur la machine qui les joue, plutôt que
d'en écrire une en dur qui n'y serait pas — **et passent leur chemin s'il n'y en a
aucune**. C'est la seule réserve du poste, et elle est écrite ici plutôt que tue : sur
cette machine-ci les 81 familles concernées font que le contrôle tourne toujours.

**Un interblocage possible, corrigé au passage** : `RedirectStandardError` était activé
sans que la sortie d'erreur soit jamais lue, puis `ReadToEnd()` bloquait sur la sortie
standard. Un `fc-match` qui aurait rempli son tampon d'erreur serait resté bloqué en
écriture, et l'export avec lui. La sortie d'erreur est maintenant vidée en parallèle.

**Ce qui reste muet** : l'écran. Avalonia substitue de son côté, par son propre
gestionnaire de polices, et le dire à l'affichage demanderait une seconde mesure, prise
ailleurs, qui pourrait diverger de celle-ci. Ce que l'utilisateur voit aujourd'hui,
c'est l'avertissement de l'export, remonté dans la barre d'état à `Ctrl+E` — et il y
passe **en tête**, la barre ne montrant que le premier.

**Vérifié sur les polices des documents réels** : Calibri, Wingdings et Wingdings 3 se
résolvent exactement et ne disent rien ; *Verdana Pro Cond SemiBold* se signale. Le
relevé de `Scripts/reference.txt` n'a pas bougé — les documents du dépôt n'emploient que
DejaVu Sans et DejaVu Serif.

*Recette jouée*, et éprouvée en la faisant échouer quatre fois : résolveur qui accepte
tout ce que `fc-match` rend, comparaison par égalité stricte des chaînes, fragments de
style ignorés, dédoublonnage retiré. Quatre sabotages, quatre attrapés.

Un cinquième a servi : la comparaison de chaînes réécrite **dans** la résolution,
`Correspond` restant juste mais n'étant plus appelée. Les tests sur chaînes figées ne
voyaient rien passer — un test qui traverse tout le chemin de résolution avec une
famille à noms multiples cherchée sur la machine a été ajouté pour cela.

### B6 — Aperçu avant impression *(fiche 22)*

Une fenêtre qui montre les pages telles qu'elles sortiront, sans repère, sans marqueur
de débordement et sans sélection. Le banc sait déjà rendre une page hors champ : c'est
le même rendu, montré au lieu d'être comparé. À faire avec B3 ou juste après.

*Recette* : chaque page de l'aperçu, comparée au `pdftoppm` de l'export par la logique
de `comparer-rendus.sh`, reste sous la tolérance. Aucun pixel n'a la couleur des
repères ni celle du marqueur rouge. Effort : faible à moyen.

### B7 — Replier les panneaux *(fiche 04)*

La seule chose que « Ce que ce plan ne fait pas » retenait du volet Office, et
qu'aucun poste ne portait : masquer les deux colonnes latérales pour rendre leur place
à la planche, sur `Ctrl+F1` comme dans Publisher.

*Recette* :
- après `Ctrl+F1`, la planche gagne au moins la largeur des deux panneaux, et ceux-ci
  sont invisibles ; un second `Ctrl+F1` rend l'état d'avant ;
- le raccourci figure au README et n'en double aucun autre.

À vérifier d'abord : que le bureau ne capte pas `Ctrl+F1`, comme il captait `Alt+F6`
en A4. Effort : faible.

---

## Phase C — Refactorings structurels

**Gate : P1 et P2 doivent être faits.** Ces postes déplacent des propriétés entre
classes, changent le format de fichier et rendent le rendu récursif. Ordre conseillé,
les dépendances étant respectées.

### C1 — Fond et bordure au niveau de `Frame` *(fiches 09, 11)*

Applique la décision P0. Remonter le remplissage, le contour, son épaisseur et l'arrondi
de `ShapeFrame` vers `Frame`, sous le nom retenu. Effet : les blocs de texte gagnent une
bordure, les images gagnent fond **et** bordure. Côté export, la fonction qui trace les
formes sait déjà faire les deux — il suffit de l'appeler avant le texte et l'image,
plutôt que de dessiner le rectangle à la main comme le fait aujourd'hui le rendu du texte.

Ajouter dans la foulée : transparence exposée (l'octet alpha la porte déjà, elle n'est
juste offerte nulle part) et traits en pointillés.

*Recette* : encadrer une image d'un filet de 2 pt sans superposer de forme ; l'export
est identique à l'écran. Le contrôle P2 passe.

*Fait le 5 septembre 2026.* Fond, contour, épaisseur, trait (continu, tirets, points)
et arrondi sont sur `Frame` ; l'opacité du fond est offerte en clair dans le panneau,
en plus du sélecteur. À l'écran comme à l'export, le fond passe sous le contenu et le
contour dessus — un filet sur une image passe sur son bord, pas sous elle — et le
contour se trace même quand le contenu échoue : une image illisible garde son cadre.
Les motifs de trait sont une seule table, en multiples de l'épaisseur, que les deux
rendus lisent ; le PDF en porte la trace (`3 w [12 6] 0 d`).

`FormatVersion` passe à 2. Un fichier de la version 1 — C3c y avait déjà ajouté le
discriminant `group` sans changer de numéro, une addition ne cassant rien — est migré
à l'ouverture, groupes compris ; un fichier plus récent est refusé avec son numéro. Le
contrôle P2 a gagné une quatrième étape : la version 1 du document d'exemple, figée
dans `Scripts/fixtures/`, doit s'ouvrir et se migrer en un point fixe.

Vérifié en rendant la même page des deux côtés — l'écran hors champ, par
`RenderTargetBitmap`, l'export par `pdftoppm` — à deux pixels par point : mêmes tirets,
mêmes points, même filet de 2 pt, mêmes arrondis ; 2,4 % des pixels diffèrent, tous
sur le lissage des bords, le texte et le repère de marge propre à l'écran.

### C2 — Repères *(fiches 05, 06, 02)*

**C2a — Grille de composition.** Colonnes, lignes, et grille de ligne de base réglée en
points. Le mécanisme d'aimantation prend déjà un tableau de repères en paramètre : toute
la mécanique consiste à l'allonger. C'est ce qui fait passer l'aimantation d'un confort
(centrer) à un outil de composition (un bulletin en trois colonnes).

**C2b — Règles et repères libres.** Règles graduées sur les deux bords, puis repères
posés au menu ou glissés depuis la règle, déplaçables, sérialisés, non imprimés. C'est
le seul mécanisme qui permette d'aligner sur une position choisie par l'utilisateur —
tant qu'il manque, l'aimantation ne sert qu'aux positions décidées d'avance.

*Recette* : composer trois colonnes, y aligner quatre blocs sans les positionner au
chiffre ; les repères n'apparaissent pas dans le PDF.

*C2a fait le 5 septembre 2026.* Six réglages sur le document — colonnes, gouttière,
lignes, espacement, pas et décalage de ligne de base — et quatre listes calculées : ce
qui se trace, et ce sur quoi on s'aimante. Le pas des lignes de base se règle en points
et non en millimètres, comme la fiche 05 y insiste : il se cale sur le corps du texte.
`SnapEdge` reçoit désormais les repères du document au lieu d'un tableau de cinq écrit
sur place ; le geste, lui, n'a pas changé. `FormatVersion` reste à 3, six propriétés
facultatives dont les défauts signifient « aucune grille ».

Recette jouée à la souris, pointeur synthétisé sur la vraie couche de saisie : quatre
blocs lâchés à des endroits différents à moins de 4 pt du bord de la deuxième colonne
s'y posent tous exactement, et `Alt` désarme bien l'aimantation. Les repères tracés sont
relus dans l'arbre visuel. Pour le PDF, le même document exporté avec la grille la plus
dense qu'on puisse régler — 77 repères — et sans grille donne deux images identiques au
pixel près.

*C2b fait le 5 septembre 2026.* Deux règles graduées en millimètres bordent la planche
et défilent avec elle — une graduation tombe donc exactement en face de la position
qu'elle annonce, sans avoir à suivre le défilement à la main. Publisher les fixe au bord
de la fenêtre ; les adosser à la page coûte moins cher et ne se trompe jamais.

Les repères libres sont portés par le document et non par la page, comme dans Publisher :
un repère sert à tenir un alignement d'une page à l'autre. On les pose en glissant depuis
une règle ou par le menu, on les déplace sur la page, on les retire en les ramenant
au-delà du bord. Ils s'aimantent eux-mêmes sur la grille, ce qui permet d'en poser un
exactement sur un bord de colonne, et leur position exacte se saisit dans le panneau —
c'est l'équivalent du dialogue « Format Ruler Guides », au même endroit que le reste des
réglages du document.

Recette jouée à la souris, pointeur synthétisé sur les vraies règles et la vraie couche
de saisie : poser, aimanter, désarmer par Alt, reprendre, retirer hors page, et un bloc
qui vient se poser exactement sur un repère choisi. Le tout est annulable. Deux exports
du même document, avec et sans repères, donnent deux images identiques au pixel près.

*À noter* : une position posée à la souris ne vaut que ce que vaut le pointeur —
Avalonia rend des positions en pixels entiers, soit 0,75 pt à 100 % de zoom. Ce qui est
exact, ce sont les positions aimantées, qui sortent de la table des repères.

*Trouvé au passage lors de C2a*, hors périmètre du poste mais corrigé : le convertisseur
millimètres arrondissait à deux décimales, et la liaison étant à double sens, la valeur
arrondie revenait dans le modèle. Une gouttière de 12 pt devenait 11,99 pt à la seule
ouverture du document, sans geste ni pas d'annulation — et il en allait de même de
toute position ou taille ne tombant pas sur la grille du centième de millimètre.
L'affichage est arrondi par le champ, qui ne touche pas à la valeur.

### C3 — Sélection multiple, puis groupement *(fiches 16, 22)*

**C3a — Sélection multiple.** La sélection est aujourd'hui unique, et c'est l'obstacle
en amont de tout le reste : le bloc sélectionné est un champ simple consommé partout —
commandes de disposition, panneau de propriétés, poignées. Le faire devenir une
collection est un remaniement réel mais circonscrit, et il ouvre à lui seul :
déplacement d'ensemble, suppression et duplication groupées, **alignement et
répartition**.

**C3b — Alignement et répartition.** Aligner à gauche/centre/droite, haut/milieu/bas ;
répartir horizontalement et verticalement. Au passage, **trancher une incohérence
existante** : le centrage actuel travaille par rapport à la *page*, alors que
l'ajustement aux marges juste à côté travaille par rapport aux *marges*. Publisher
propose explicitement le choix ; il faut soit le proposer, soit s'aligner sur les marges.

**C3c — Groupe persistant.** Un bloc « groupe » contenant des enfants en coordonnées
relatives. Le coût n'est pas dans le modèle : le rendu et l'export deviennent
**récursifs**, alors que leurs deux boucles sont aujourd'hui plates. La composition des
rotations, elle, est déjà écrite proprement.

Faire C3a et C3b d'abord : environ 80 % de l'usage pour une fraction du coût.

*Recette* : sélectionner quatre blocs, les aligner sur leur bord gauche, les grouper,
déplacer le groupe, l'exporter — la géométrie tient.

*Fait le 5 septembre 2026*, en trois temps comme prévu ci-dessus.

- **C3a** — `Selection` est une collection ; `Selected` n'en est plus que l'ancre.
  Déplacement, suppression, duplication, ordre de superposition et annulation portent
  sur l'ensemble. Le panneau de propriétés reste muet à plusieurs blocs, et les
  poignées ne s'affichent que sur un bloc seul : redimensionner un ensemble, c'est le
  grouper.
- **C3b** — six alignements et deux répartitions. L'incohérence signalée est tranchée
  par la case *Par rapport aux marges*, cochée par défaut : « centrer horizontalement »
  sur un bloc seul travaille désormais sur la zone de composition et non sur la page.
  La répartition égalise les blancs, pas les centres.
- **C3c** — `GroupFrame` porte ses blocs en coordonnées relatives ; rendu et export
  sont devenus récursifs. Redimensionner un groupe met ses blocs à l'échelle, sans
  toucher au corps du texte. Dissocier compose la rotation du groupe avec celle de
  chaque bloc, si bien qu'un groupe tourné se défait sans que rien ne bouge.

Vérifié en comparant les rendus : le PDF d'un groupe est identique au pixel près à
celui des mêmes blocs posés à plat, grouper puis dissocier rend exactement le document
de départ, et un groupe élargi de moitié donne la même image que les blocs mis à
l'échelle à la main. Les positions à l'écran ont été relevées dans l'arbre visuel :
elles concordent avec l'export.

### C4 — Champs *(fiches 08, 19)*

Un champ facultatif sur le bloc de texte : s'il est renseigné, le texte affiché est
calculé au lieu d'être littéral. Le bloc reste un bloc de texte ordinaire, avec sa
police et son alignement — **aucun type nouveau**.

Trois usages pour un seul investissement :
- **numéros de page** — le manque le plus gênant au-delà de deux pages ;
- **date** ;
- **informations professionnelles** : un jeu de coordonnées nommé, stocké hors document
  (les coordonnées suivent l'utilisateur, pas le fichier), avec plusieurs jeux
  possibles. N'a d'intérêt qu'avec les modèles de B1 — d'où l'ordre.

*Recette* : un folio posé sur un modèle se numérote seul sur huit pages, à l'écran et
dans le PDF.

*Fait le 5 septembre 2026*, en deux temps.

- **C4a** — le mécanisme, et ses trois usages immédiats : numéro de page, nombre de
  pages, date. Un `Field` facultatif sur le bloc de texte, résolu par une fonction pure
  que l'écran et l'export appellent tous deux ; le contexte vient du contrôle, un bloc
  ignorant sur quelle page il se trouve. Le texte saisi reste sous le champ et se
  retrouve intact quand on le retire.
- **C4b** — les informations professionnelles : des jeux nommés rangés dans
  `~/.config/MyPublisher/`, pas dans le document, avec le logo sur un bloc image.

La date est écrite en français quel que soit le poste et les nombres sans séparateur de
milliers : le PDF est le livrable, il ne doit pas changer de sens selon qui l'exporte.
`FormatVersion` reste à 3, une propriété facultative se lisant sans migration.

Recette jouée sur un document ordinaire de huit pages, les modèles de B1 n'existant pas
encore : c'est le folio qu'elle éprouve, et il se numérote seul de 1 à 8 à l'écran comme
dans le PDF — relu par `pdftotext` —, y compris posé dans un groupe. **La moitié
« coordonnées » du poste, elle, attend bien B1 pour servir à quelque chose** : le plan
le disait, et c'est toujours vrai. Ce qui est acquis, c'est qu'elle n'aura rien à
attendre le jour où les modèles arriveront.

### C5 — Remplissage riche et cohérence visuelle *(fiches 09, 01)*

**C5a — Fond de page.** Une propriété sur le document, un rectangle plein en tête de
chaque page à l'export. Effet immédiat sur l'allure, coût dérisoire.

**C5b — Jeu de couleurs et jeu de polices.** Une palette nommée de cinq couleurs et une
paire titre/corps, portées par le document et proposées dans les sélecteurs. C'est ce
qui manque le plus pour qu'un utilisateur reste cohérent d'un bloc à l'autre : aujourd'hui
les couleurs se saisissent en hexadécimal, une par une.

**C5c — Remplissages non unis.** Dégradé deux couleurs (l'export sait faire), image de
fond (réutilise le cadrage d'image déjà écrit), teinte (purement calculatoire depuis une
couleur de base). Le type de remplissage devient discriminé, exactement comme les blocs
le sont déjà — le mécanisme de sérialisation polymorphe est en place et éprouvé.

Textures et motifs : écartés, ressources à embarquer pour une esthétique datée.

*Recette* : un document composé avec un jeu de couleurs, un fond de page dégradé, et un
export fidèle.

*C5a et C5c faits le 6 septembre 2026. C5b reste ouvert.*

`Fill` n'est plus une couleur mais un type discriminé par `$kind` — `solid`, `gradient`,
`image`, `tint` — exactement comme les blocs le sont depuis C3c. Le fond de page arrive
avec, sous le nom que la décision P0 lui réservait : `Document.Background`, du même type,
dessiné en tête de chaque page par le même chemin que le fond d'un bloc. Une seule
propriété nouvelle sur le document, aucune sur les blocs.

**`FormatVersion` passe à 4.** Une couleur ancienne devient un remplissage uni de cette
couleur ; un fond absent le reste, sans quoi une page changerait d'aspect à la seule
ouverture. La migration descend dans les groupes **et dans les cases des tableaux** — une
case n'est pas un bloc, elle n'a pas de `$kind`, et un parcours qui n'aurait suivi que les
blocs aurait laissé des fonds de case en chaîne que le modèle refuse de relire. C'est le
premier poste où la distinction compte. La version 3 du document d'exemple rejoint les
deux précédentes dans `Scripts/fixtures/`.

#### Ce que les documents réels demandaient

**Les cinq PNG d'ATA 21 sont portés.** Ce ne sont pas des blocs image : ce sont des
remplissages de forme (`draw:fill: bitmap`), et l'import les perdait en le disant. Le
relevé les donne en base64 — jusqu'à 142 000 caractères pour une seule — alors que le
modèle référence les images par chemin.

**Où l'image est écrite, et pourquoi.** Dans les données de l'utilisateur,
`~/.local/share/MyPublisher/images/`, sous un nom tiré de son contenu. Trois raisons, et
un prix qu'il faut dire :

- les écrire à côté du `.pub` modifierait un dossier que personne n'a demandé de toucher —
  et, très concrètement, salirait `MyPublisher/Books/` à chaque passage de la recette ;
- les images d'un document ne sont pas de la configuration : elles vont dans les données,
  là où les coordonnées professionnelles vont dans `~/.config` ;
- nommer d'après le contenu rend l'import **idempotent** : réimporter le même `.pub` rend
  exactement le même document, au caractère près, ce sans quoi l'aller-retour qu'exige le
  contrôle P2 cesserait d'être vérifiable sur un document importé.

Le prix : déplacer un document ne déplace pas ses images, et une purge de ce dossier les
perd. L'export le dit — « Image de remplissage introuvable » — plutôt que de laisser un
aplat vide. C'est le compromis des images liées, et c'est le poste **B4** qui le résoudra.

**`PubImporter` reste pur**, ce qui n'allait pas de soi : il ne touche pas au disque, il
demande un chemin à un `IDepotImages` qu'on lui prête. Les tests lui en donnent un faux et
s'exécutent en quelques millisecondes, comme le poste R5 en avait pris l'habitude ; sans
dépôt, l'import dit qu'il a perdu l'image plutôt que de l'écrire de son propre chef.

**Une des cinq images tourne, et c'est 20 % du poste.** Elle porte
`librevenge:rotate: 90` : son PNG fait 1062 × 617, rapport 1,721, dans un cadre de
3,04 × 5,23 in, rapport 0,582 — exactement l'inverse. Sans le quart de tour elle arrive
couchée **et** écrasée d'un facteur trois, au milieu d'un schéma de ventilation.
`ImageFill` le porte donc, et les deux rendus l'appliquent : découpe au bloc d'abord — le
bloc, lui, ne tourne pas — puis rotation autour du centre, puis l'image posée dans le cadre
que le modèle a calculé.

#### Deux calculs qui vivent dans le noyau

Les extrémités d'un dégradé et la couleur d'une teinte. Aucun des deux rendus n'en fait
sa part : ils lisent. Ce n'est pas une élégance — c'est la leçon écrite plus haut, prise
par le bon bout. Une comparaison écran ↔ PDF ne peut pas attraper une erreur **partagée**,
mais elle ne peut pas non plus attraper une erreur qui **n'existe qu'une fois** : les deux
rendus s'accordent, et se trompent ensemble. Ce sont donc des tests du noyau qui épinglent
les valeurs, et la démonstration est faite plus bas.

L'angle d'un dégradé est en degrés, zéro de gauche à droite, croissant dans le sens des
aiguilles — l'axe Y descend, comme en PDF. Les extrémités sont posées en projetant les
coins du rectangle sur l'axe, convention de CSS : c'est la seule qui rende un dégradé à 45°
complet dans les coins plutôt que délavé. Les quatre ombrages de Publisher se ramènent à
un angle ; les deux radiaux — depuis un coin, depuis le centre — ne sont pas repris.

#### La recette, et le sabotage qu'elle a laissé passer

`Scripts/recettes/C5-fonds.sh`, en deux temps. Le premier porte sur ATA 21 lui-même : les
cinq remplissages par image arrivent, les cinq fichiers sont écrits, un seul quart de tour,
et `pdfimages -list` donne la définition posée de chaque image en largeur et en hauteur —
elles ne sont égales que si l'image n'a pas été étirée de travers. Le second mesure neuf
couleurs au pixel sur `Samples/recette-fonds.mypub`, dans le PDF puis à l'écran, aux mêmes
points, avec la comparaison d'ensemble en filet.

Éprouvée en sabotant sept fois le code, et **six seulement ont été attrapés du premier
coup**. Le septième — l'export cessant d'appliquer le quart de tour — est passé au vert :
une image non tournée garde une définition uniforme, elle est simplement rognée. Le compte
de définitions mesurait « pas déformée », pas « tournée ». La recette mesure donc
maintenant jusqu'où l'encre monte et descend dans le cadre de l'image tournée : 4,5 pt du
bord avec le quart de tour, 86 sans — la bande centrale de 219 pt sur 376. Et les deux
rendus doivent s'accorder à 3 pt près, ce qui prend en défaut une rotation oubliée d'un
seul côté.

**Le sabotage qui vaut d'être raconté** est celui des extrémités du dégradé, échangées
dans le noyau. Les deux rendus tracent alors tous les dégradés du document à l'envers — et
la comparaison écran ↔ PDF reste à **0,08 %**, exactement le chiffre du code sain. Elle
prouve l'accord, jamais la justesse. Ce sont les couleurs mesurées et cinq tests du noyau
qui l'attrapent.

Les six autres : la copie qui partage le remplissage au lieu de le cloner, l'étape de
migration retirée, l'import qui perd la rotation, l'export qui ignore le fond de page,
l'écran qui l'ignore, et la migration qui ne descend pas dans les cases d'un tableau.

#### Trois pièges rencontrés

**Le sélecteur de couleur pouvait effacer un dégradé.** `FillColor` existe depuis C1 parce
qu'un `ColorPicker` renvoie vers sa source la valeur qu'il vient d'en lire, dès qu'il
s'affiche. Le même piège revient sous une forme neuve : si l'accesseur écrivait dans un
fond d'une autre sorte, sélectionner un bloc au fond dégradé le remplacerait par un aplat.
`FillColor` ne vaut donc que pour l'aplat, et le panneau ne montre le sélecteur que dans
ce cas ; le dégradé, l'image et la teinte ont leurs propres champs. Un test le garde.

**Un remplissage se modifie sur place.** Régler l'angle d'un dégradé ne remplace pas
l'instance que porte le bloc : ni `AffectsRender` ni `NotifyPropertyChangedFor` n'en
sauraient rien, et la page ne se repeindrait pas — c'est le défaut de rafraîchissement de
R1, dans un habit neuf. Le bloc, la case et le document s'abonnent donc à leur remplissage,
et le contrôle d'affichage aussi. Le désabonnement passe par la surcharge à deux
paramètres d'`OnFillChanging` : celle à un seul ne donne pas l'ancienne valeur, et le bloc
continuerait d'entendre un remplissage qu'il ne porte plus.

**Cet abonnement a ouvert deux fuites, l'une et l'autre refermées.** La seconde est du
côté de l'écran : `FondBloc` s'abonnait au remplissage qu'on lui donnait et ne s'en
détachait que si on lui en donnait un autre. Un contrôle qui sort de l'arbre en gardant
son fond reste donc accroché à un remplissage qui, lui, survit. Ce qui a été mesuré, c'est
ce cas : un contrôle posé vingt fois à la main dans un panneau puis retiré laisse vingt
abonnés — le test qui garde le remède, relu au poste Q2 en retirant le désabonnement :
« Expected: 0, Actual: 20 ». On croyait alors que changer de page produisait la même
chose ; le poste Q2 l'a mesuré, et ce n'est pas le cas : la liste des blocs retire le
contexte de données des gabarits qu'elle démonte, le fond retombe à null et le contrôle se
désabonne — vingt changements de page, aucun abonné de trop. L'abonnement suit désormais
l'attachement à l'arbre visuel, ce qui ne dépend plus de la conduite du conteneur.
`TexteCompose` portait le même trou depuis R1, sur le bloc au lieu du remplissage ; il a
été refermé au poste Q2.

La première : `TableFrame.CellFrames`
recalcule ses blocs de texte à chaque lecture et leur *prêtait* le fond de leur case,
comme il leur prête leurs fragments. Chaque bloc jeté restait alors accroché au fond, qui
lui survit : vingt lectures, vingt-et-un abonnés — mesuré. Le panneau relisant cette liste
à chaque notification, la pile montait sans fin, et toucher au fond d'une case notifiait
des centaines de blocs que plus personne ne dessine. Le fond est donc copié là où les
fragments restent prêtés, ce qui rend le bloc et son fond ramassables ensemble. La fuite
ne se voit d'aucune façon depuis l'extérieur d'un événement : le test qui la garde compte
les abonnés par réflexion. Une première écriture, par référence faible, ne prouvait rien —
en Debug les variables locales restent vivantes jusqu'au bout de la méthode, et le test
échouait pareil sans aucun abonné.

**Le test d'exhaustivité de `Clone()` allait cesser de couvrir le fond.** Il découvre les
propriétés par réflexion et leur donne une valeur ; sa fabrique rend `null` pour tout type
qu'elle ne sait pas distinguer. `Fill` était une chaîne, donc couvert ; devenu un objet, il
serait retombé dans ce cas sans qu'aucun test ne change de couleur — exactement le trou que
les collections avaient laissé depuis R1. La fabrique sait désormais fabriquer un
remplissage, et trois tests vérifient en plus que la copie est profonde : deux blocs issus
d'une duplication doivent pouvoir diverger, et une case neuve ne partage pas le fond de sa
voisine.

#### Trouvé, non corrigé — hors périmètre

**`TexteCompose` ne se détache jamais du bloc qu'il écoute.** Il s'y abonne depuis la
correction de R1 — c'est ce qui fait que régler le corps redessine la page — mais rien ne
défait cet abonnement quand le contrôle quitte l'arbre visuel. Changer de page laisse donc
un contrôle de plus accroché à chaque bloc de texte. C'est exactement la fuite que ce
poste vient de refermer sur `FondBloc`, à l'endroit d'à côté, et le remède est le même :
suivre l'attachement. Non corrigé parce que ce contrôle appartient à R1 et qu'un poste ne
retouche pas le rendu du texte pour un fond.

**La recette de ce poste demande `pub2raw`**, comme celle de R5, sa moitié utile portant
sur ATA 21. Un arbre de travail neuf n'a pas de `.outils/` — le dossier n'est pas suivi —
et la recette échoue alors en le disant, plutôt que de se déclarer verte sur un contrôle
qu'elle n'a pas fait.

#### Écrit ailleurs, et assumé

**Le fond de la vignette d'un modèle reste un aplat.** `TemplatePreview` donne un dégradé
par sa première couleur et une image par un gris neutre. C'est l'écart que B1 a assumé —
« les vignettes sont un schéma et non un rendu » — et l'ouvrir ici reviendrait à embarquer
un troisième moteur de rendu, ce que tout l'équilibre du projet cherche à éviter.

**Un dégradé semi-transparent sort opaque du PDF.** PDFsharp écrit un ombrage axial, dont
l'alpha est celui de la page ; le format ne donne pas de transparence propre à chaque
extrémité sans un groupe de transparence, que l'export n'écrit pas. L'écran, lui, l'honore.
Consigné plutôt que tu.

**La forme ne se trace plus d'un seul trait.** Fond et contour étaient dessinés en un
appel ; ils sont désormais séparés, comme pour les autres blocs, parce qu'un remplissage
par image ne se pose pas avec un pinceau. Le rendu est le même — remplir puis contourner
est ce que fait l'opérateur combiné — et la forme hérite au passage des quatre sortes de
fond. Rien ne couvrait ce changement de chemin : le document de recette n'avait aucune
forme munie d'un contour, et le contrôle P2 compare un compte de pages, pas des pixels.
L'ellipse y a donc gagné un filet de 3 pt et l'aplat un filet de 2 pt avec un angle
arrondi de 10 — trois points de mesure de plus, et une vérification que le fond respecte
l'arrondi, le coin devant laisser passer le fond de page. Retirer le contour des formes à
l'export met les trois au rouge.

#### Choisi sans certitude

**L'échelle de la teinte.** 100 est la couleur de base, 0 le blanc, 200 le noir : une seule
échelle continue, monotone, sans valeur ambiguë. Publisher étiquette ses dix-huit pastilles
en couverture d'encre et tient deux échelles séparées, « 60 % Tint » et « 60 % Shade ». Le
côté clair correspond donc exactement — 60 ici est bien 60 % de teinte — et le côté sombre
est son miroir plutôt que l'étiquette de Publisher. Aucun des deux documents réels ne porte
de teinte : le choix est écrit pour être discuté, non pour être mesuré.

**Une mosaïque est rendue sans répétition.** `style:repeat` vaut `stretch` sur les cinq
images d'ATA 21, ce qui est le cadrage « Remplir ». Toute autre valeur est cadrée sans
déformation, avec un avertissement : le modèle ne sait pas mettre une image en mosaïque, et
l'inventer ici sortirait du poste.

**Le fond de page est porté par le document et non par la page**, comme les repères libres
et pour la même raison : c'est ainsi que Publisher le règle, et un fond qui changerait
d'une page à l'autre est une page maître — poste non planifié.

### C6 — Marges intérieures par côté *(fiche 12)*

Remplacer la marge intérieure unique et symétrique par quatre valeurs. Mécanique, sans
piège, hormis la migration de format — à grouper avec C1 pour ne payer la migration
qu'une fois.

*Recette* : un texte décalé de 12 pt à gauche seulement, à l'écran comme au PDF.

*Fait le 5 septembre 2026*, après C1 et non avec lui : la mécanique de migration
posée par C1 rendait le groupage sans objet, le poste n'a coûté qu'un numéro de
version et son étape. `PaddingLeft`, `PaddingTop`, `PaddingRight`, `PaddingBottom`
sur le bloc de texte, et `Padding` d'un seul tenant pour la vue et l'export — la
liaison XAML n'a pas changé, seul son convertisseur. `FormatVersion` passe à 3 ; un
fichier ancien reçoit quatre marges égales à son unique et s'affiche comme avant. Le
contrôle relit désormais une version passée par numéro de format : la version 2 de
l'exemple rejoint la version 1 dans `Scripts/fixtures/`.

Vérifié en mesurant l'encre : trois blocs du même texte, sans marge, avec 12 pt à
gauche seulement, avec 20 pt en haut seulement, rendus des deux côtés à deux pixels
par point — les décalages mesurés sont ceux demandés, à gauche sans bouger en haut, et
l'écran et l'export concordent.

### C7 — Bibliothèque de blocs *(fiche 20)*

Dépend de C3a. On sélectionne un ou plusieurs blocs, on les enregistre sous un nom, on
les repose dans n'importe quel document. Version utile et bien moins chère de la
*Design Gallery* : elle ne demande aucun contenu à dessiner, l'utilisateur l'apporte.
Elle réutilise la sérialisation existante — un élément est un fragment de `.mypub` — et
la copie de bloc est déjà écrite.

Elle rend au passage la zone de travail hors page (fiche 07) presque superflue : même
besoin — garder un logo sous la main — mais persistant et inter-documents. À traiter
dans le même panneau qu'une éventuelle bibliothèque d'images locale (fiche 13).

*Recette* : enregistrer un bloc-titre composé, ouvrir un autre document, le reposer.

### C8 — Miroir horizontal et vertical *(fiche 22)*

La notion n'existe pas dans le modèle. Elle traverse le cœur géométrique — survol,
poignées, redimensionnement, composition avec la rotation, groupes — et les deux rendus :
c'est pourquoi elle relève de la phase C et non de la phase A. Une question reste à
trancher avant d'écrire une ligne, et les fiches ne la tranchent pas : un bloc de texte
en miroir montre-t-il son texte à l'envers, ou seulement son cadre ?

Dépend de C3c, A1 et A9.

*Recette* :
- test pur : sur des blocs tirés au hasard, rotation et miroir compris, passer du
  repère de la page à celui du bloc puis revenir rend le même point ;
- un groupe en miroir porte son bloc de gauche à droite ;
- une image en miroir horizontal : la première colonne de son rendu est la dernière de
  la source, à l'écran comme au PDF ;
- `comparer-rendus.sh` passe sous la tolérance.

Effort : moyen à lourd.

### C9 — Le bloc tableau

Poste que ce plan ne portait pas : il vient de
[`Documents-reels.md`](Documents-reels.md), où les fiches ATR comptent dix-neuf objets
tableau. Le relevé a été rectifié le 6 septembre 2026, et la rectification change tout :
**seize de ces dix-neuf tableaux ont trois colonnes et une seule ligne**, trois n'ont
qu'une case, aucune cellule n'est fusionnée, aucun tableau n'est imbriqué. Ce sont des
bandes à trois cases, et non des tableaux de données. Sur les 362 insertions de texte du
document, seize sont dans un tableau : l'essentiel du contenu vit dans les blocs de
texte ordinaires.

Ce que le poste doit donc livrer est un porte-cases : des colonnes, des lignes, et une
cellule qui porte un contenu de texte, son fond et son contour. Rien qui calcule, rien
qui se scinde entre deux pages, rien qui fusionne.

*Recette* : un document portant une bande de trois cases se relit et se réécrit au
caractère près, s'exporte, et l'écran concorde avec le PDF.

*Fait le 6 septembre 2026.* `TableFrame`, discriminant `table`. `FormatVersion` **reste
à 3** : un type de bloc neuf n'oblige à migrer aucun fichier ancien, exactement comme
C3c avait ajouté `group` sans changer de numéro.

**Un tableau se rend comme un groupe.** C'est le choix qui commande tout le reste. Ses
cases ne sont pas dessinées : elles sont *calculées* en blocs de texte, en coordonnées
relatives, à partir des colonnes, des lignes et des cellules. L'écran les affiche par le
gabarit du bloc de texte, l'export les trace par sa fonction de bloc de texte. Aucun des
deux rendus n'a une ligne de tableau qui lui soit propre, et ils sont donc d'accord par
construction — comme ils le sont déjà pour un groupe. Une case hérite au passage de tout
ce que les postes précédents ont apporté au texte : la composition partagée de R1, ses
fragments de style, l'interligne de R4, le fond et le contour de C1.

**Les colonnes et les lignes font la taille du bloc**, et non l'inverse. C'était la
question à trancher, parce que le bloc porte déjà une largeur et une hauteur : trois
mécanismes pouvaient les accorder, et deux suffisent. Redimensionner le tableau mesure
ses colonnes à la même aune — ce qu'un groupe fait de ses blocs, donc le tirer à la
poignée et le mettre à l'échelle dans un groupe marchent sans une ligne de plus. Élargir
une colonne élargit le bloc. Les deux sens passent par un même verrou, sans quoi ils se
relanceraient l'un l'autre. Le prix de ce choix est qu'aucun chiffre du panneau ne peut
mentir : la largeur qu'on lit sur une colonne est celle qu'on mesure sur la page.

Un détail qui n'en est pas un : l'accord des deux tailles se fait à **un milliardième de
point près**, et non à l'identique. Mesurer trois colonnes au rapport d'un
redimensionnement laisse leur somme à un ulp de la largeur demandée ; sans ce seuil,
ouvrir puis réenregistrer un document déplacerait ce dernier chiffre, et l'aller-retour
cesserait d'être identique au caractère près. C'est la même dérive que celle consignée
au poste P3, prise cette fois avant qu'elle ne nuise.

**Ce que le panneau règle** : lignes et colonnes, la largeur de chacune, la police et le
corps du tableau, la marge intérieure des cases, la grille — et, case par case, son
texte, son alignement et son fond. Une case ne se choisit pas sur la page : il n'y a pas
de sélection à l'intérieur d'un bloc, et une bande en compte trois. La grille, elle, se
règle pour toutes les cases à la fois alors que chacune porte son contour dans le
fichier : c'est ainsi que Publisher les décrit et que l'import de R5 les lira, mais
personne ne veut cocher trois cases pour encadrer une bande.

*Recette jouée*, `Scripts/recettes/C9-tableau.sh` : le document de six bandes se réécrit
au caractère près, son texte se retrouve dans le PDF, et surtout **les cases y sont
mesurées** par `pdftotext -bbox` — origine de colonne, bord intérieur, fer à droite sur
la somme des largeurs, hauteur de ligne — au centième de point.

La mesure a remplacé une comparaison d'images qui ne prouvait pas grand-chose ici : sur
une page de tableaux, aux trois quarts blanche, le lissage des glyphes et des filets
coûte à lui seul 2,40 %, et décaler tous les tableaux de 2 pt à l'export ne fait monter
l'écart qu'à 3,04 %. Le même décalage déplace chaque mesure de 2,00 pt, contre une
tolérance de 0,05 pt. La comparaison d'images reste, en filet, pour ce qu'une mesure de
position ne voit pas : une couleur perdue, un fond oublié, un filet disparu.

*Trouvé au passage*, et non corrigé — hors périmètre. Deux choses.

`TexteCompose` ne se redessine que lorsque la **référence** de son bloc change
(`AffectsRender` porte sur `Bloc` et `Contenu`). Modifier le corps ou la police d'un
bloc de texte dans le panneau ne repeint donc pas l'écran tant qu'aucune autre liaison
ne le force. Les tableaux n'en souffrent pas — leurs cases sont recalculées et changent
donc de référence à chaque retouche — mais les blocs de texte, si.

Un bloc plus large que le groupe qui le porte est **découpé à l'écran et pas au PDF**.
Vérifié sur un groupe de 120 pt contenant un rectangle et un texte de 400 pt, sans
aucun tableau : l'écran s'arrête au bord du groupe, le PDF va jusqu'au bout. Le cas ne
s'obtient pas au geste — grouper ajuste le groupe à ses blocs — mais un fichier écrit à
la main ou un import le produira. Il ne touche pas les tableaux : leur largeur est
toujours celle de leurs colonnes, y compris après relecture d'un fichier qui dirait
autre chose.

*Écart assumé* : le contenu d'une case est découpé au bloc à l'écran (`ClipToBounds`) et
ne l'est pas au PDF. Un texte trop long pour sa case sortira du cadre dans le PDF et pas
sur la page. C'est le poste A2 — signaler le débordement — et il vaut déjà pour les
blocs de texte ordinaires.

---

## Phase R — Ce que les documents réels ont ajouté

Cinq postes qu'aucune fiche n'annonçait, tous établis sur les deux `.pub` ATR déposés le
6 septembre 2026 — voir [`Documents-reels.md`](Documents-reels.md), qui les décrit et dit
pourquoi ils passent devant le reste. R1 et R4 sont clos ; ce qu'ils ont trouvé est dans
leurs commits et dans le relevé.

### R2 — Retraits et espacement de paragraphe

*Fait le 6 septembre 2026.* Le bloc de texte gagne cinq réglages en points — retrait
gauche, retrait droit, retrait de première ligne, espace avant, espace après — et une
collection d'écarts, `Paragraphs`, qui les redit autrement pour une suite de paragraphes.
Trente-six paragraphes d'ATA 21 portent un retrait de première ligne **négatif** : c'est
ainsi que Publisher compose une liste à puces, la puce se posant dans le creux. Sans lui,
elles retombent au fer à gauche et la structure du document disparaît.

**Le repérage était le choix à faire, et il diverge de R1.** Un fragment de style repère
des positions de caractères ; un écart de paragraphe repère des **indices de paragraphe**.
Trois raisons : un retrait appartient au paragraphe entier et n'en a pas de moitié, si
bien qu'un repérage par caractère obligerait à inventer une règle pour le paragraphe à
demi couvert — et une règle inventée s'applique en silence ; un indice survit à la frappe
ordinaire, là où taper un mot décale toutes les positions de caractères qui suivent ; et
c'est ce que la source donne, `pub2raw` énumérant des paragraphes. Le prix est réel et il
est dit dans le code : le fichier porte désormais deux systèmes de repérage, et l'import
de R5 devra tenir les deux dans la même passe.

**La ligne composée porte désormais son cadre** — son retrait et la largeur qui lui
reste. Ce n'est pas une commodité de rendu. La cible d'une ligne justifiée n'est plus la
largeur du bloc mais celle-là, et `Compositeur.Decalages` a **perdu son paramètre de
largeur** : il lit celle de la ligne. Un rendu ne peut donc plus se tromper de cible tout
seul. C'est le piège que le poste annonçait, et il est de ceux que la comparaison écran ↔
PDF ne voit pas : les deux rendus appelant le même calcul, ils se seraient trompés
ensemble.

`FormatVersion` reste à 3. Cinq réglages à zéro et une collection vide composent
exactement comme avant, et c'est ce qui dispense de migration — le même mécanisme qu'a
employé R1.

*Recette jouée*, et elle est un script : `Scripts/recettes/R2-retraits.sh`. Les retraits
sont **mesurés** dans le PDF par `pdftotext -bbox-layout`, qui rend la boîte de chaque
ligne à l'origine du crayon : 18,000 pt de retrait négatif, 24,000 pt de retrait de
paragraphe à gauche, 24,015 pt à droite — le bord droit d'une ligne justifiée rentre
d'autant, ce qui est le point délicat — et 12,000 pt d'espace après, à un quart de point
près. L'écran est ensuite mesuré de la même façon, encre contre encre, bande par bande :
les quatre grandeurs y sont identiques à celles du PDF, à moins d'un pixel.

**Le pourcentage de pixels différents ne suffisait pas, et c'est un enseignement.** Sur
ce document, l'accord écran ↔ PDF donne 2,94 % — le seul lissage des glyphes d'une page
de texte — tandis qu'une divergence de retrait simulée, l'écran composant sans retraits
et le PDF avec, n'en donne que 3,93 %. Le filet global ne sépare pas les deux. Il reste
dans la recette pour ce qu'il attrape — un bloc qui cesserait d'être dessiné — mais ce
n'est pas lui qui éprouve le poste.

La recette a été éprouvée en la faisant échouer, quatre fois : l'écran ignorant le
retrait, le retrait de première ligne appliqué à toutes les lignes, l'export oubliant
l'espace après, et la justification visant la largeur du bloc. Quatre sabotages, quatre
attrapés. Les tests d'interface l'ont été de même, sur les deux sabotages qui ne touchent
que l'écran.

*Trouvé au passage, et comblé.* Le test d'exhaustivité de `Clone()` par réflexion **ne
couvre pas les collections** : la fabrique de valeurs rend `null` pour elles, si bien
qu'elles restent vides des deux côtés et que l'égalité des deux JSON passe sans rien
prouver. `Spans` était dans ce cas depuis R1. Un test propre aux deux collections du bloc
de texte comble le trou, et vérifie au passage que les éléments sont copiés et non
partagés.

#### Trois choses trouvées et laissées telles quelles

**Le panneau TEXTE ne rafraîchit plus la page — et c'est R1, pas R2.** Mesuré : on change
`IndentLeft` sur un bloc, la fenêtre rend une image identique ; on change `FontSize`,
elle rend encore la même ; on change `Text`, elle change. La raison est unique et elle
vaut pour les dix propriétés typographiques du panneau, pas seulement pour les cinq de ce
poste : `TexteCompose` déclare `AffectsRender` sur `Bloc` et `Contenu`, or `Bloc` est
lié au bloc lui-même — l'instance ne change pas quand une de ses propriétés change, donc
rien n'invalide le visuel. Seul `Contenu`, calculé par liaison à partir de `Text` et de
`Field`, en produit une nouvelle valeur. Avant R1 le texte était un `TextBlock` dont
chaque propriété était liée séparément, et le rafraîchissement suivait. Hors périmètre de
R2, et à corriger en un seul endroit pour toutes : c'est le bloc qu'il faut écouter.

**L'écran découpe le texte au bloc, l'export ne le découpe pas.** `PageCanvas.axaml` pose
`ClipToBounds` sur le contenu du bloc de texte ; `PdfExporter` dessine sans découpe.
Tant que le texte reste dans son cadre, les deux concordent. Une première ligne qui pend
au-delà du bord gauche, elle, est rognée à l'écran et entière au PDF — et c'est
exactement ce que la vraie paire d'ATA 21 produit : `margin-left 0.1930in` avec
`text-indent −0.2183in` place la première ligne à **−1,8 pt**, dehors. R5 importera
trente-six paragraphes de cette sorte.

> *Rectifié au poste A2, le 6 septembre 2026, en le mesurant.* La dernière phrase est
> fausse, et c'est la marge intérieure qui la rend fausse : `fo:padding-left` vaut
> `0.0400in` sur les neuf blocs d'ATA 21, soit 2,88 pt, si bien que la première ligne
> tombe à 1,06 pt **dans** le bloc. Elle sort de sa zone de texte, que rien ne découpe,
> et non du bloc, qui seul l'est. Le constat général était juste, l'exemple non — et
> c'est l'exemple qui aurait servi de recette.

**La recette éprouve donc le mécanisme, pas encore les valeurs réelles.** Elle mesure
18 pt de retrait négatif, 24 pt de retrait de paragraphe et 12 pt d'espace après, tous
choisis pour que le texte reste dans son cadre et que la découpe ci-dessus ne se voie
pas. Les valeurs relevées sur les documents — −15,7 pt et 13,9 pt — se composeront
correctement, mais leur concordance écran ↔ PDF ne sera acquise qu'une fois la découpe
tranchée : soit l'écran cesse de rogner, soit l'export rogne à son tour.

*Laissé de côté, et à noter pour R5* : `fo:text-align` varie **par paragraphe** dans
ATA 21 — 31 justifiés contre 21 au fer à gauche — alors que l'alignement reste une
propriété du bloc. `ParagraphSpan` le porterait pour presque rien, mais le poste énumère
quatre propriétés et s'y tient.

*Choisi sans certitude* : l'espace avant s'applique aussi au premier paragraphe du bloc,
qui descend donc sous sa marge intérieure haute. C'est ce que fait Publisher dans une
zone de texte ; le relevé ne tranche pas, n'ayant aucun `fo:margin-top`.

### R3 — Colonnes à l'intérieur d'un bloc

*Fait le 6 septembre 2026.* Le bloc de texte gagne un nombre de colonnes — une par
défaut — et une gouttière. Le texte remplit la première colonne jusqu'au bas du bloc,
puis passe à la suivante, quitte à couper un paragraphe en deux : c'est ce que veut dire
« le texte coule ». La largeur qui sert à la découpe n'est plus celle du bloc mais celle
d'une colonne, `(largeur − (n − 1) × gouttière) / n`.

**Ce n'est pas C2a**, et le panneau le dit maintenant en toutes lettres : C2a pose des
colonnes sur la **page**, comme repères d'aimantation ; celles-ci sont **dans un bloc**,
et rien n'est partagé entre les deux — ni propriété, ni calcul.

**La composition range elle-même les lignes en colonnes**, en une seconde passe qui ne
touche pas au renvoi à la ligne : toutes les colonnes ayant la même largeur, la découpe
ne dépend pas de celle où la ligne atterrira. La ligne composée porte désormais sa
colonne, l'ordonnée de son sommet **dans cette colonne** et son avance ; `Composer` a
reçu pour cela la hauteur du bloc et l'interligne, et `Hauteur` a **perdu** son
paramètre d'interligne.

C'est le geste de R2 poussé d'un cran, et pour la même raison. Jusqu'ici chaque rendu
cumulait sa propre hauteur de ligne en ligne ; c'était sans conséquence tant qu'une
divergence de métrique ne produisait qu'une dérive continue de quelques centièmes de
point. **Avec les colonnes, cette dérive devient une falaise** : une ligne posée près
d'un bord de colonne tomberait dans la colonne 1 à l'écran et dans la colonne 2 au PDF,
pour un écart de métrique bien inférieur au point. Un seul endroit décide donc, et les
deux rendus lisent. Aucun des deux n'additionne plus rien.

**Ce qui dépasse la dernière colonne continue vers le bas**, hors du bloc, exactement
comme un texte trop long le fait aujourd'hui dans un bloc d'une seule colonne. Rien
n'est tronqué, et c'est délibéré : couper ici livrerait un PDF amputé sans le dire, ce
que le poste **A2** est justement chargé d'empêcher — et A2 n'est pas écrit. La règle
« une seule colonne » devient ainsi un cas particulier de la règle générale et non un
chemin de code à part, ce qui est la vraie garantie qu'aucun document existant ne bouge.
A2 trouvera d'ailleurs son compte tout fait : la dernière ligne porte son `Haut` et son
`Avance` dans sa colonne, et `Compositeur.Hauteur` rend la hauteur de la colonne la plus
remplie — non la somme des colonnes.

`FormatVersion` reste à 3. Une colonne et une gouttière inerte composent exactement
comme avant, et c'est ce qui dispense de migration — le même mécanisme qu'ont employé
R1 et R2.

**La non-régression a été mesurée avant de toucher au code**, et c'est la seule façon de
la mesurer : les sept documents du dépôt ont été exportés et rendus à l'écran *avant* le
poste, puis à nouveau *après*, une fois les fichiers régénérés par le sérialiseur.
**Vingt images, écran et PDF, toutes pages : zéro pixel de différence.** Comparer les
JSON n'aurait rien prouvé — ils changent forcément, le sérialiseur écrivant les deux
propriétés neuves ; ce sont les images qu'il fallait confronter.

Ce que ce zéro dit, et ce qu'il ne dit pas : les vingt images ne portent que des blocs
d'une seule colonne — c'est précisément ce qui en fait le bon témoin de non-régression,
et c'est aussi pourquoi il ne prouve rien sur les colonnes elles-mêmes. Celles-ci
reposent sur la recette et sur `ColonnesTests`, et sur rien d'autre.

*Décidé, et non subi* : **réduire un groupe rétrécit les colonnes d'un bloc de texte sans
toucher à sa gouttière.** Le groupe met à l'échelle la géométrie, pas la typographie —
c'est déjà ce que C3c fait du corps du texte. Ce sont donc les colonnes qui se resserrent,
pas ce qui les sépare, et un bloc écrasé au-dessous de ses gouttières garde des colonnes
d'un point plutôt que des colonnes négatives. Deux tests dans `GroupeTests`.

*Recette jouée*, et elle est un script : `Scripts/recettes/R3-colonnes.sh`, sur
`Samples/recette-colonnes.mypub` — trois colonnes portant douze paragraphes numérotés,
deux colonnes justifiées, et un témoin à une seule colonne muni d'une gouttière de 40 pt
qui ne doit rien faire.

Les colonnes sont **mesurées** dans le PDF par `pdftotext -bbox`, qui rend la boîte de
chaque **mot**. Le mot et non la ligne, contrairement à la recette de R2 : `-bbox-layout`
regroupe en lignes selon sa propre analyse de mise en page, or ce qu'on veut vérifier est
précisément qu'il y a plusieurs colonnes à la même hauteur. Mesuré : 169,000 pt d'un
bord de colonne au suivant, trois sommets identiques au centième de point, le flot dans
l'ordre — `01 02 03 04` puis `05 06 07 08` puis `09 10 11 12` —, et 241,488 pt de bord
droit justifié pour une colonne large de 241,5. Ce dernier nombre est le piège du poste,
le même qu'en R2 : une ligne justifiée dont la cible serait restée la largeur du bloc
s'étirerait par-dessus la gouttière et sous sa voisine. L'écran est ensuite mesuré de la
même façon, encre contre encre, bande par bande : **les six bandes concordent au pixel**,
sauf un pixel de sommet sur le bloc justifié.

La recette a été éprouvée en la faisant échouer, huit fois : l'écran ignorant les
colonnes, l'export les ignorant, la ligne oubliant le pas de sa colonne, la
justification visant la largeur du bloc, l'écran ne repartant pas du haut de la colonne,
le débordement partant dans une quatrième colonne inexistante, et `Clone()` oubliant le
nombre de colonnes. **Huit sabotages, huit attrapés** — quatre par la recette, deux par
les tests d'interface, deux par ceux du noyau.

*Trouvé au passage, et il faut le dire.* Le sabotage se restaurait par `cp -a`, qui rend
au fichier sa date d'origine : MSBuild le croit inchangé, ne recompile pas, et le
**binaire saboté survit à la restauration**. Un premier passage a ainsi rapporté un
sabotage « non attrapé » qui l'était, et un autre « attrapé » par un test qui ne le
regardait pas. Toute vérification qui écrit du code puis le remet doit toucher le
fichier ; `Scripts/eprouver-le-juge.sh`, qui restaure par `git checkout`, ne court pas ce
risque — `git` écrit une date neuve.

#### Ce que le relevé dit des colonnes, et ce que les fichiers disent

Le relevé (`Documents-reels.md`) présente R3 ainsi : « `fo:column-gap` est posé sur les
neuf blocs de texte d'ATA 21 et sur seize des Fiches ». C'est exact. Deux points
méritent cependant d'être ajoutés, l'un et l'autre vérifiés en relançant `pub2raw` :

**`style:column-width` n'appartient pas aux zones de texte.** Les dix-sept occurrences
des Fiches sont toutes dans un `startTableObject`, sous `librevenge:table-columns` :

```
startTableObject(librevenge:table-columns: ((style:column-width: 0.4860in),
  (style:column-width: 2.2286in), (style:column-width: 0.5220in)), svg:height: ...)
```

Ce sont les colonnes des **tableaux** — les seize bandes à trois cases du poste C9 — et
non celles d'un bloc de texte. Le rapprochement du relevé était une lecture rapide.

**`fo:column-count` n'apparaît nulle part.** La chaîne existe dans libmspub 0.1.4 — elle
est dans la bibliothèque, à côté de `fo:column-gap` — mais elle n'est émise par aucun des
**vingt-quatre** `.pub` présents sur la machine, les deux documents de référence
compris, alors que `fo:column-gap` l'est sur chaque zone de texte, toujours à la même
valeur : 0,0787 in pour ATA 21, soit 2 mm exactement, et 0,0800 in pour les Fiches. Ce
sont les deux écritures du **défaut** de Publisher, selon que l'unité du document est
métrique ou impériale.

La lecture la plus probable est donc que **ces documents sont tous à une seule colonne**,
et que la gouttière n'y est qu'un réglage par défaut qui ne sert à rien. Une seconde
lecture ne peut pas être écartée sans un `.pub` à deux colonnes sous la main : que
libmspub 0.1.4 ne restitue tout simplement pas le compte. Les deux mènent au même endroit
pour R5 — l'import devra lire `fo:column-count` s'il le rencontre, et ne pas conclure des
colonnes de la seule présence d'une gouttière.

Ce qui change, c'est ce que le poste **prouve**. Il ne reproduit pas une mise en page que
ces documents demanderaient : il donne à Publisher une fonction qu'il a, que le format
doit savoir relire, et qui manquait. Et il rend à la gouttière son sens : celle des
documents réels, 5,67 pt, est désormais la valeur par défaut d'un bloc neuf.

`Documents-reels.md` n'a pas été corrigé : le poste s'en tient à son périmètre, et ce
fichier fait foi. La rectification est ici, avec sa preuve.

#### Choisi sans certitude

**La gouttière par défaut vaut 5,67 pt** — les 2 mm d'ATA 21. Zéro aurait aussi bien fait
l'affaire pour la non-régression, une gouttière ne servant à rien à une colonne ; mais
elle aurait fait se toucher les colonnes le jour où l'on passe le nombre à deux, et ce
n'est pas ce que Publisher fait.

**L'espace avant s'applique aussi en tête de colonne**, comme R2 l'a fait appliquer au
premier paragraphe du bloc. Le relevé ne tranche pas davantage ici qu'il ne tranchait
là — il ne porte aucun `fo:margin-top`. Ce qui compte est que les deux cas soient
tranchés une fois et de la même façon, plutôt que deux fois différemment.

**Le panneau plafonne à vingt colonnes.** C'est un garde-fou du champ et non du format :
le fichier en accepte davantage, la composition ramène toute largeur de colonne à un
point minimum, et au-delà de vingt une colonne est plus étroite qu'un mot. Aucune fiche
ne donne la limite de Publisher ; le nombre est donc choisi, pas relevé.

#### Trouvé et laissé tel quel

**La vignette d'un modèle ne montre pas les colonnes.** `TemplatePreview` trace le texte
en filets pleine largeur, quel que soit le nombre de colonnes du bloc. C'est l'écart que
B1 a assumé et écrit — « les vignettes sont un schéma et non un rendu » — et il n'y a pas
de raison de le rouvrir pour ce poste.

**Le panneau TEXTE ne rafraîchit toujours pas la page**, colonnes comprises : régler le
nombre de colonnes ne redessine rien tant qu'on ne retouche pas le texte. C'est le défaut
que R2 a relevé et daté de R1, à corriger en un seul endroit pour les douze propriétés à
la fois. Les tests d'interface de ce poste posent donc les colonnes **avant** d'ouvrir la
fenêtre, comme ceux de R2 posaient les retraits.

*Au passage, et c'est un déplacement et non une correction* : l'ouverture d'une fenêtre
sur un document et le relevé de son encre sont sortis de `RetraitsTests` vers
`MyPublisher.Tests.Interface/Rendu.cs`, R3 en ayant besoin pour mesurer autre chose. Le
piège du banc — `PageRoot` mesuré en points mais vivant sous la mise à l'échelle du
zoom — n'est ainsi gardé qu'à un seul endroit. Deux copies, et la seconde oublie
l'assertion.

### R6 — Le panneau règle le paragraphe du curseur

*Spécifié le 11 octobre 2026.* Le README range parmi les limites que « l'alignement
reste une propriété du bloc ». **Ce n'est plus vrai du modèle.** Depuis R5,
`ParagraphSpan` porte un `Align`, l'import le remplit et la composition le lit. Les
documents réels en tirent parti : 31 paragraphes justifiés dans ATA 21, et 16 centrés
dans les Fiches. Ce qui manque est le geste : le panneau agit sur le bloc entier, et
rien ne crée ni ne modifie un écart de paragraphe depuis l'interface. Il en va de même
des cinq réglages de R2.

Le poste : pendant l'édition d'un bloc de texte, l'alignement, les retraits et les
espacements du panneau s'appliquent aux paragraphes que touche la sélection, ou au
paragraphe du curseur, sous forme d'écarts `ParagraphSpan`. Hors édition, ils restent
réglages du bloc, comme aujourd'hui. Deux écarts voisins identiques fusionnent, et un
écart qui ne s'écarte plus de rien disparaît.

Dépend de Q1 : tant qu'un saut de ligne tapé ne décale pas les indices de paragraphe,
un écart posé à la main glisse sur le paragraphe voisin à la frappe suivante.

*Recette* :
- tests d'interface : curseur dans le 2e de trois paragraphes, clic sur « centré » ;
  un seul écart `{Start 1, Length 1, Align Center}` ;
- l'écran et le PDF centrent ce paragraphe et lui seul, à 0,05 pt près ;
- un pas d'annulation rend le bloc d'avant ;
- les 16 paragraphes centrés des Fiches survivent à un aller-retour par l'interface.

Effort : moyen.

### R7 — Éditer le texte riche à la souris

*Spécifié le 11 octobre 2026.* Le pendant de R6 à l'échelle du caractère, et la limite
qui pèse le plus sur la reprise des documents réels. Les 402 fragments des deux
documents ATR s'importent, se rendent et s'exportent, mais on ne peut pas mettre trois
mots en gras. Il faut appliquer la police, le corps, le gras, l'italique, le souligné
et la couleur à la sélection, en découpant, en fusionnant et en retirant des
`StyleSpan`.

**Une question à trancher avant d'écrire du code.** Pendant l'édition, l'écran montre
le `TextBox` en texte brut, et `TexteCompose` est masqué. Éditer du texte riche dans
une vue qui n'en montre rien n'a pas de sens. Deux voies :
- **garder le `TextBox` pour le curseur et la sélection seulement, transparent, et
  laisser `TexteCompose` dessiner dessous**. C'est la plus sobre. Le risque : que le
  curseur ne tombe pas où le texte composé est dessiné, puisque les deux mesures
  diffèrent ;
- **écrire un éditeur sur `Compositeur`**, avec le curseur, la sélection et la saisie
  calculés sur les lignes composées. C'est plus juste, et beaucoup plus lourd. Cette
  voie rejoint le seuil D.

Le poste commence par mesurer l'écart de la première voie sur les documents réels :
l'écart, en points, entre la position du curseur du `TextBox` et celle du caractère
composé. Si l'écart reste sous le pixel, on prend la première voie. Sinon, on revient
vers le propriétaire du projet pour décider.

Dépend de Q1.

*Recette* :
- tests d'interface : sélection des caractères 4 à 7 au pointeur synthétisé, puis
  `Ctrl+B`. On obtient un seul fragment `{Start 4, Length 3, Bold}`, et le PDF montre
  ces trois caractères seuls dans la fonte grasse (relevé du flux) ;
- `Ctrl+B` sur une sélection déjà grasse retire le gras, et le fragment disparaît s'il
  ne porte plus rien ;
- un pas d'annulation par commande.

Effort : lourd.

---

## Phase Q — Dettes consignées

*Ouverte le 11 octobre 2026.* Les postes et les relectures adversariales ont trouvé
des défauts qu'ils ont **écrits sans les corriger**, avec une raison chaque fois juste :
ce n'était pas leur périmètre. Écrits, ils étaient pourtant dispersés dans la prose de
douze sections, et rien ne les comptait. Cette phase les rassemble, chacun avec la
recette qui le prouvera corrigé. Les sections d'origine restent ce qu'elles sont : le
récit de ce qui a été trouvé, et quand.

Un défaut n'entre ici que s'il est **vérifié encore présent** dans le code du
11 octobre 2026. Plusieurs défauts consignés ont été corrigés depuis par une relecture :
le rafraîchissement du panneau, la rotation à l'import. Ils n'y figurent donc pas.

Ne sont pas repris non plus les écarts écrits comme des **choix** : les bornes du format
libre, les cases de la barre d'état qui disparaissent sans sélection, l'échelle de la
teinte, le dégradé semi-transparent qui sort opaque, et le débordement horizontal muet,
qui suit Publisher. Les rouvrir est une décision, pas une réparation.

### Q1 — Les styles suivent la frappe

**Le seul défaut de la liste qui abîme des données.** Les fragments de style se
repèrent par position de caractère, et les écarts de paragraphe par indice de
paragraphe. Aucune ligne du dépôt ne les met à jour quand le texte change : les seules
écritures de `Spans` sont dans `Clone()` et dans l'import. Quand on tape un mot en tête
d'un bloc importé, tous ses gras glissent d'autant de caractères vers la gauche du
texte. Un saut de ligne tapé décale de même tous les retraits, et
l'alignement, d'un paragraphe. Le document reste lisible, l'avertissement ne dit rien,
et l'enregistrement fixe le décalage.

Cela ne se voyait pas : aucun test ne tape dans un bloc qui porte des fragments, et
aucune recette n'enregistre un document réel après l'avoir retouché.

Le poste : à chaque modification du texte, calculer l'insertion ou la suppression par
rapport au texte d'avant, puis décaler, allonger, raccourcir ou retirer les fragments
et les écarts concernés. Un caractère inséré au contact d'un fragment prend le style du
caractère qui le précède, comme dans Publisher et Word. Ce calcul est une fonction pure
du noyau.

*Recette* :
- test pur : sur « abc def », gras sur « def » `{4, 3}`, insérer « XX » en 0 donne
  `{6, 3}` ; insérer en 5 donne `{4, 5}` ; supprimer « c d » retaille le fragment, et
  supprimer tout « def » le retire ;
- un saut de ligne tapé dans le 1er de trois paragraphes décale de 1 l'écart du 3e ;
- tests d'interface : ouvrir ATA 21, taper un mot en tête du premier bloc ; le PDF rend
  les mêmes mots en gras qu'avant la frappe (relevé du flux) ;
- sabotage : neutraliser le décalage fait tomber les trois points.

Effort : moyen. **À faire en premier** : R6 et R7 en dépendent, et chaque jour sans lui
est un jour où l'on peut abîmer un document importé.

### Q2 — Le texte composé se détache de son bloc

`ControleDeBloc` s'abonne au bloc et à ses deux collections, mais ne se désabonne
jamais quand il quitte l'arbre visuel. Chaque changement de page laisse donc un
contrôle de plus accroché à chaque bloc de texte. C'est la fuite que C5 a refermée sur
`FondBloc`, et le remède est le même : suivre l'attachement.

*Recette* : après vingt changements de page, chaque bloc de texte n'a plus qu'un seul
abonné, compté par réflexion comme pour `FondBloc`. Effort : faible.

*Fait le 11 octobre 2026.* `ControleDeBloc` suit désormais l'attachement, exactement
comme `FondBloc` : il ne s'abonne pas hors de l'arbre visuel, l'attachement l'abonne —
en retirant d'abord, pour qu'un double attachement ne compte pas double — et le
détachement le désabonne, bloc et deux collections. Le texte composé et le marqueur de
débordement en héritent tous deux, puisqu'ils partagent cette base depuis A2.

**Le poste a trouvé que son énoncé était faux, et il faut le dire avant tout le reste.**
Changer de page ne laissait aucun abonné. Mesuré sur le code d'avant le remède : vingt
changements de page, et le bloc affiché garde ses deux contrôles — texte et marqueur —,
celui de la page quittée n'en a plus aucun. La liste des blocs de la page est un
`ItemsControl` ; il retire le contexte de données des gabarits qu'il démonte, la
liaison `Bloc="{Binding}"` retombe à null, et l'ancien `Suivre` se désabonnait en
perdant son bloc. Il en va de même pour `FondBloc`, dont le fond arrive par une liaison
de style — vérifié ici en lui retirant son remède : vingt changements de page, aucun
abonné de trop. Le « vingt passages, vingt abonnés » de C5 est celui du test qui le
garde, où le contrôle est posé à la main dans un panneau ; le passage de C5 a été
récrit pour le dire. La fuite est
réelle, mais elle ne frappe qu'un contrôle qui quitte l'arbre **en gardant son bloc** :
une fenêtre fermée sur un document resté ouvert, un aperçu, une vignette. Aucun n'existe
encore ; B6 et C7 en poseront. Le remède ne dépend donc plus de la conduite du
conteneur, et c'est ce qu'il achète.

D'où quatre tests, sous `poste=Q2`, dans `DetachementTests`. Le premier joue le geste du
plan — vingt changements de page sur un document qui porte un bloc à fragments et à
écarts, un tableau dont une case porte un fragment, et une seconde page — et compte par
réflexion les abonnés qui sont des contrôles de bloc : autant que de contrôles qui
montrent le bloc, sur le bloc et sur chacune de ses collections, et zéro pour le bloc
de la page quittée. **Il passait avant le remède**, et il est gardé pour ce qu'il est :
le geste que l'utilisateur fait. On l'a vu tomber en retirant les deux désabonnements à
la fois. Le second est celui qui éprouve le remède : un contrôle qui reçoit son bloc
avant d'être attaché, puis entre et sort vingt fois de l'arbre en le gardant — un
abonné et un seul pendant qu'il est affiché, aucun après. Le troisième garde l'autre
moitié : revenue sur sa page, le texte se redessine encore quand on règle le corps ou
qu'on ajoute un fragment. Un remède qui se détache sans se rattacher refermerait la
fuite et rouvrirait le défaut de R1 ; ce sabotage-là fait tomber les trois tests, parce
qu'un gabarit reçoit son bloc avant d'être attaché. Le quatrième, ajouté après la
relecture, garde le chemin de désabonnement qui existait déjà : un contrôle qui reste
affiché et reçoit un autre bloc — un présentateur qui recycle son gabarit le fait —
lâche l'ancien, bloc et deux collections, et n'écoute que le nouveau. Sans lui, retirer
ce désabonnement ne cassait aucun test.

Le tableau a sa raison d'être dans le premier test : ses cases sont des blocs calculés
à chaque lecture qui **empruntent** la collection de fragments de leur case — et le
contrôle s'y abonne. Le commentaire de `CellFrames` disait des fragments prêtés qu'« ils
ne portent aucun abonnement » ; c'était vrai du bloc, pas du contrôle qui le dessine, et
le commentaire le dit maintenant. Un contrôle resté accroché à cette collection serait
retenu par la case, qui vit aussi longtemps que le document.

Le compteur d'abonnés par réflexion est sorti de `FondsTests` vers `Rendu.Abonnes`, qui
sait lire n'importe quel événement — `CollectionChanged` en plus de
`PropertyChanged` — et qui lève sur un nom d'événement introuvable plutôt que de
rendre zéro, ce que les tests de fuite attendent justement.

*Recette jouée* : `Scripts/recettes/Q2-Q3-fuite-et-cache.sh`, commune aux deux postes.
Elle joue les tests ci-dessus par leur trait, en vérifie le compte, et lit d'abord une
règle dans les sources : tout contrôle de `Views/` qui s'abonne par `+=` dans un
gestionnaire de changement de propriété surcharge `OnAttachedToVisualTree` et
`OnDetachedFromVisualTree`. C'est la deuxième fois que la même fuite se présente ; la
troisième se fera arrêter là. La règle est une heuristique et la recette le dit : elle
prouve qu'on y a pensé, les tests prouvent qu'on l'a bien fait. Deux sabotages au juge :
le détachement rendu muet, que seul le deuxième test voit ; le désabonnement de l'ancien
bloc retiré, que seul le quatrième voit.

*Choisi sans certitude* : ce qui est compté. Le plan disait « un seul abonné » ; un
bloc de texte affiché en a deux qui sont des contrôles de bloc — le texte et le
marqueur — et d'autres qui n'en sont pas, les liaisons d'Avalonia. Le test compte les
premiers et les compare au nombre de contrôles qui montrent le bloc, plutôt qu'à un
nombre écrit en dur.

*Laissé tel quel* : `Spans` et `Paragraphs` ont un accesseur public en écriture. Un
code qui remplacerait la collection d'un bloc affiché laisserait le contrôle abonné à
l'ancienne et sourd à la nouvelle. Rien dans le dépôt ne le fait hors de `Clone()` et de
l'import, qui portent sur des blocs neufs ; le poste ne l'a donc pas couvert.

### Q3 — Le cache d'images oublie ses échecs

Une image absente à l'ouverture est retenue comme absente pour toute la durée du
processus : la remettre en place puis rouvrir le document laisse le bloc vide, sans
rien signaler. `Conv.Invalidate` existe, mais personne ne l'appelle. L'ouverture d'un
document est le bon endroit pour vider le cache.

*Recette* : ouvrir un document dont l'image manque, remettre le fichier, rouvrir ; la
couleur de l'image se relève dans la page rendue. Effort : faible.

*Fait le 11 octobre 2026.* `BitmapCache.OublierLesEchecs` retire du cache les chemins
dont la lecture a échoué, et eux seuls ; le modèle de vue l'appelle au début
d'`Adopt`, par où passent l'ouverture d'un fichier et le départ d'un modèle. Pas dans
l'accesseur de `Document`, que l'annulation emprunte aussi : annuler un geste n'est pas
rouvrir, et n'a pas à relire le disque. Les trois cas sont tenus par des tests.

**Pourquoi oublier les échecs plutôt que vider le cache, ou que relire les images du
document.** Un échec est mis en cache à dessein : sans cela, un bloc dont l'image manque
relirait le disque et lèverait une exception à chaque notification de liaison, donc à
chaque image d'un glissé. Il suffit qu'il cesse de l'être pour la vie du processus. Les
réussites, elles, sont justes tant que leur fichier ne bouge pas, et les relire à chaque
ouverture ne réparerait aucun défaut de ce poste. Oublier tous les échecs plutôt que
ceux du seul document a un avantage de plus : le logo des coordonnées professionnelles,
qui passe par le même cache sans être un chemin du document, en profite sans qu'il ait
fallu parcourir quoi que ce soit — blocs, groupes, cases, fond de page.

**Rouvrir n'est pas la seule façon de redemander une image.** La réinsérer (`AddImage`)
ou la choisir pour un remplissage passe par le même trou : le fichier vient d'être
désigné, `AddImage` vient même de le lire pour en prendre les proportions, mais la
liaison passe par le cache, qui rend l'échec retenu à l'ouverture. La relecture l'a
mesuré : bloc réinséré vide, 0 pixel. Ces deux gestes ont un chemin à donner, et
`Invalidate` le reçoit : `AddImage` l'appelle après la sonde, et le choix d'une image de
remplissage, passé de la fenêtre au modèle de vue (`ChoisirImageDeFond`) pour qu'un test
l'atteigne sans dialogue de fichier, l'appelle avant d'écrire le chemin. Choisir de
nouveau, pour le même remplissage, l'image qu'il désignait déjà ne change aucune
propriété et ne redessine donc rien : c'est la réouverture qui couvre ce cas-là.

*Recette jouée* : la même que Q2, sous `poste=Q3`. Un test d'interface ouvre par
`LoadFrom` — le geste du menu, et non une affectation au modèle de vue — un document
dont les deux images manquent : un bloc image, qui passe par la liaison, et une forme
au fond illustré, que `FondBloc` dessine par `Conv.Bitmap`. Deux chemins vers le cache,
deux couleurs, cyan pour le bloc parce qu'il n'est pas symétrique. Aucune ne se voit ;
les fichiers sont remis ; le document est rouvert dans la même fenêtre, le même
processus et donc le même cache ; les deux couleurs se relèvent dans l'image composée.
Puis une troisième ouverture rend **la même instance** de l'image déjà lue : c'est
l'épingle du « seulement ». Entre la remise en place des fichiers et la réouverture, un
geste annulé reconstruit tous les blocs : les deux images restent absentes, et c'est
l'épingle de « l'annulation n'est pas une réouverture ». Le même test est joué deux
fois, par `LoadFrom` et par `StartFromTemplate`. Un troisième test réinsère l'image
remise en place, et en donne une autre à un remplissage par `ChoisirImageDeFond` : les
deux couleurs paraissent.

Six sabotages au juge, et ils ne se recouvrent pas : l'appel retiré d'`Adopt` fait
tomber la réouverture ; `Cache.Clear()` à la place de l'oubli sélectif répare le défaut
tout aussi bien — et c'est pourquoi seule l'épingle le voit ; l'oubli réservé à
l'ouverture d'un fichier fait tomber la variante du modèle ; l'oubli placé dans
l'accesseur de `Document` fait tomber l'épingle de l'annulation ; l'`Invalidate` retiré
d'`AddImage`, puis celui de `ChoisirImageDeFond`, font tomber le troisième test.

*Laissé tel quel* : une image **modifiée** sur le disque pendant que le processus vit
reste périmée à l'écran, rouverture comprise, alors que l'export, qui lit le disque à
chaque fois, montre la nouvelle. C'est un écart écran ↔ PDF, mais ce n'est pas celui
du poste, et le corriger demanderait de dater les entrées du cache plutôt que de les
oublier.

### Q4 — Un filet au même endroit à l'écran et au PDF

Mesuré en A2 sur un filet de 2 pt : l'écran le rentre dans le bloc, alors que l'export
le centre sur le bord. La documentation de `Frame.StrokeThickness` dit « centré sur le
bord » : c'est donc l'écran qui a tort.

*Recette* : sur un filet de 4 pt, les bords relevés à l'écran et au PDF concordent à
0,25 pt près. Effort : faible.

### Q5 — Un groupe rogne pareil à l'écran et au PDF

Un bloc qui dépasse de son groupe est coupé à l'écran et pas au PDF. Le geste ne
produit pas ce cas, mais un fichier écrit à la main ou un import le peut. Il faut
trancher dans le sens de A2 : **l'export rogne au groupe, comme l'écran**.

*Recette* : groupe de 120 pt portant un texte de 400 pt ; rien n'est dessiné au-delà
du groupe dans le PDF, et `comparer-rendus.sh` passe. Effort : faible.

### Q6 — Mesurer la découpe d'un remplissage par image

Le document de recette de C5 ne porte que des rectangles à angles droits : un sabotage
de la découpe d'un remplissage par image traverse toute la vérification.

*Recette* : ajouter au document de C5 un bloc arrondi rempli d'une image, relever la
couleur du fond de page dans le coin, et ajouter à `eprouver-le-juge.sh` le sabotage
qui doit tomber. Effort : faible.

### Q7 — La recette de A6 sur le cas réel

La recette de A6 fabrique ses blocs par un chemin qui évite celui de l'application :
c'est le défaut relevé sur R2, et que la consigne nommait pourtant.

*Recette* : réécrite sur la fabrique de l'application (outil armé, tracé synthétisé),
et le sabotage qu'elle laissait passer tombe. Effort : faible.

### Q8 — N'avertir que des polices dessinées

La passe des polices relit l'héritage des fragments pour son compte, au lieu de lire
les styles composés. Un fragment entièrement recouvert par un autre produit donc un
avertissement pour une police que rien ne dessine.

*Recette* : un bloc en Noto Sans, dont tout le texte est recouvert par un fragment en
DejaVu Sans, avec un fragment intermédiaire « Police Inexistante » lui-même recouvert ;
l'export n'avertit de rien. Effort : faible.

### Q9 — Retirer `AbsoluteLineHeight`

C'est le survivant de l'ancienne sémantique de l'interligne (`FontSize × LineHeight`),
que R4 a remplacée. Seule la vignette des modèles la lit encore, et se trompe ainsi de
16 %. Elle doit lire l'avance de `Compositeur`, ou la propriété disparaître.

*Recette* : la propriété n'existe plus, et la vignette du bulletin a autant de lignes
que son rendu réel. Effort : faible.

### Q10 — Saisir une poignée hors de la page

La couche de saisie s'arrête au bord de la page. Une poignée de rotation qui en sort,
sur un bloc tourné de 180° collé en bas ou sur un bloc collé en haut règles masquées,
ne se saisit pas. Une poignée d'angle sur un bloc posé à Y = 0 tombe sur la règle, qui
pose un repère. Il faut étendre la couche de saisie à la zone de travail, en gardant à
la règle son geste de C2b.

*Recette* : les deux cas de A1, au pointeur synthétisé, tournent le bloc ; la poignée
d'angle redimensionne sans poser de repère ; un clic sur la règle loin de tout bloc
pose toujours un repère. Effort : moyen.

### Q11 — La liste des blocs défile pendant le glissé

La liste ne construit que les éléments visibles : on ne peut pas tirer le quarantième
bloc jusqu'en tête.

*Recette* : sur une page de 60 blocs, un glissé synthétisé maintenu en haut de la liste
la fait défiler, et le bloc se pose au rang 0. Effort : faible.

### Q12 — Dépendance vulnérable

La compilation signale `Tmds.DBus.Protocol` 0.21.2, tiré par Avalonia 11.3.9 et
affecté d'une vulnérabilité de gravité haute (GHSA-xrw6-gwf8-vvr9).

*Recette* : `dotnet list package --vulnerable --include-transitive` ne rapporte rien,
et `Scripts/verifier.sh` passe. Effort : faible, si une version corrigée existe.

*Fait le 11 octobre 2026* — une ligne dans `MyPublisher/MyPublisher.csproj` : une
référence directe à `Tmds.DBus.Protocol` **0.21.3**. Avalonia n'a pas bougé.

**La version corrigée existe sur la branche même qu'emploie Avalonia.** L'avancée
GHSA-xrw6-gwf8-vvr9, publiée le 8 avril 2026, déclare corrigées la 0.92.0 et, pour
`Tmds.DBus.Protocol` seulement, un rétroportage en 0.21.3 sur la branche `release/0.21`.
Sa notice NuGet porte les mêmes cadres et les mêmes dépendances que la 0.21.2 ; seuls
le numéro et la branche diffèrent. Avalonia 11.3.9 demande « au moins 0.21.2 » : la
0.21.3 tient dans sa contrainte sans rien forcer, et c'était le plus petit pas possible.
La montée d'Avalonia dans la série 11.3.x n'a donc pas été tentée, et aucune recette
au pixel n'avait de raison de bouger — aucune n'a bougé.

**Un seul projet tire le paquet, pas trois.** `dotnet list` le signalait dans
`MyPublisher`, `MyPublisher.Banc` et `MyPublisher.Tests.Interface`, mais
`dotnet nuget why` montre que les deux derniers ne le reçoivent que par leur référence à
`MyPublisher` (Avalonia.Desktop → X11 → FreeDesktop). La référence directe va donc dans
ce seul projet, et la règle « la dépendance la plus proche l'emporte » la transmet aux
deux autres. La base ne signalait rien d'autre sur les cinq projets.

**La recette lit la base, puis prouve qu'elle l'a lue.** `Scripts/recettes/Q12-dependances.sh`
lit le relevé JSON plutôt que la phrase « has no vulnerable packages », qui se dit aussi
bien d'une base saine que d'une base vide ; elle échoue sur tout problème signalé, sur
tout projet manquant au relevé, et nommément sur l'absence de réseau. Elle fait ensuite
tirer la 0.21.2 à un projet jetable et exige qu'elle y soit signalée : sans cette
contre-épreuve, un outil qui ne sait plus rien signaler passerait pour un dépôt sain.
Enfin elle compare ce que la compilation a copié à ce que la restauration a résolu, paquet
par paquet : le premier essai l'a montré utile, sur un binaire compilé avant la
correction qui embarquait encore la 0.21.2. Le sabotage ajouté au juge remet la 0.21.2
dans le projet.

*Choisi sans certitude.* La recette dépend du réseau, ou d'un cache NuGet récent :
c'est la nature d'une base qui change sans nous, et la recette tombera le jour où une
avancée nouvelle touchera un paquet d'aujourd'hui. C'est ce qu'on veut apprendre, mais
c'est aussi la première recette dont le rouge peut ne rien devoir au dépôt.

*Laissé tel quel.* La référence directe est une ligne qu'aucun code n'appelle, et qu'on
serait tenté de retirer au premier ménage. Elle pourra partir quand une version
d'Avalonia demandera d'elle-même une version corrigée ; la recette dira si elle part
trop tôt.

### Q13 — Couper la ligne au même mot à l'écran et au PDF

*Trouvé le 11 octobre 2026*, en vérifiant ce que disaient les en-têtes de
`verifier.sh` et de `comparer-rendus.sh`. Ils annoncent que la comparaison écran ↔ PDF
échoue sur une page de texte « parce que l'interligne n'est pas appliqué à l'export ».
R4 a corrigé l'interligne, et l'écart sur la page 1 du document d'exemple est passé de
8 % à 4,87 %. Il reste pourtant au-dessus du seuil de 3 %, et pas seulement à cause du
lissage des glyphes. **Dans la première colonne, l'écran et le PDF ne coupent pas la
même ligne au même mot** : les deux versions se superposent sur l'image des écarts.

R1 a fait partager aux deux rendus le *code* qui coupe les lignes (`Compositeur`), mais
pas la *mesure* des mots : l'écran interroge Avalonia (`MetriquesAvalonia`), l'export
interroge PDFsharp (`MetriquesPdf`). Un mot qui tient au centième de point d'un côté
passe à la ligne de l'autre, et tout le reste du bloc se décale. C'est le défaut que la
relecture du 6 septembre avait relevé pour les colonnes, et il vaut aussi pour les
lignes. Le document d'exemple le produit tel quel.

Le remède sûr est le seuil D : une seule source de mesure. Il existe un remède d'attente,
moins cher, que ce poste doit éprouver : faire **décider la coupure sur la plus large
des deux mesures**. Les deux rendus coupent alors aux mêmes mots, et chacun dessine
ensuite avec sa propre mesure, en justifiant ou non à l'intérieur de la ligne. Le
prix : un mot qui tiendrait de justesse passe à la ligne des deux côtés.

*Recette* :
- test pur : sur les blocs de `Samples/exemple-bulletin.mypub` et des deux documents
  ATR, composer avec les deux métriques donne les mêmes coupures, ligne par ligne ;
- `comparer-rendus.sh` passe sous le seuil sur les deux pages du document d'exemple.
  Quand c'est fait, `verifier.sh` peut l'appeler en bloc.

Effort : moyen. **Si la voie d'attente ne suffit pas, le poste devient la question du
seuil D**, et l'on s'arrête pour la poser au propriétaire du projet.

---

## Ordre d'exécution

*Arrêté le 11 octobre 2026.* Les postes partent par **salves** de quatre à huit, chacun
dans sa copie de travail, et sont fusionnés après relecture adversariale. Une salve ne
réunit que des postes qui ne touchent pas les mêmes fichiers ; quand deux postes se
touchent, le second attend la salve suivante.

| Salve | Postes | Pourquoi ensemble |
|---|---|---|
| **1** | Q1 · les sept recettes de P3 · Q2 · Q3 · Q12 | La dette qui abîme des données, le banc complet, et trois réparations courtes |
| **2** | Q4 à Q9 · Q13 · B2 · C5b | Les écarts écran ↔ PDF, et les deux postes d'usage qui ne demandent rien |
| **3** | R6 · A8 · A9 · A15 · B7 · B4 | Le paragraphe au panneau, et des postes courts, indépendants les uns des autres |
| **4** | R7 · A12 · C7 · B3 + B6 | Le texte riche à la souris, et ce qui reprend les commandes des salves précédentes |
| **5** | A10 · A11 · A13 · Q10 · Q11 | La géométrie et la couche de saisie |
| **6** | A14 · C8 | Les deux postes à risque, chacun précédé de son essai |
| **D** | Note de décision | Rédigée après la salve 6 ; **la décision revient au propriétaire du projet** |

Trois points où l'on s'arrête pour demander, plutôt que de décider seul : le choix de
voie au début de R7, toute rupture de `FormatVersion` sans migration, et la recette de
B3, qui demande une vraie imprimante.

---

## Seuil D — Le moteur de composition

**Non planifié à ce stade. Décision à rouvrir quand A, B et C sont livrées.**

Trois fonctions du relevé n'existent que si le texte est *mesuré* avant d'être posé :

- **l'ajustement automatique** — réduire le corps jusqu'à ce que le texte tienne
  (fiche 12) ;
- **le chaînage entre blocs** — couper au bloc et reporter le reste dans le suivant
  d'une chaîne, avec les mentions de renvoi (fiche 12, limite n°1 du README) ;
- **l'habillage du texte** autour d'une image — composer en excluant des zones
  (fiche 22).

Ce sont trois expressions du même chantier, et il faut les décider ensemble, jamais
séparément.

**Ce que la décision engage.** Une chaîne de blocs suppose un identifiant sur les blocs,
qu'ils n'ont pas. Une mesure fiable suppose de trancher entre deux moteurs qui ne
mesurent pas à l'identique — celui de l'écran et celui du PDF — alors que tout
l'équilibre actuel du projet tient à ce que les deux partagent exactement les mêmes
données sans couche intermédiaire. Mesurer deux fois, c'est risquer de diverger ;
mesurer une fois et stocker, c'est introduire un état calculé dans un modèle qui n'en a
aucun.

*Précisé le 11 octobre 2026.* Depuis R1, les deux rendus partagent le **code** qui
coupe les lignes (`Compositeur`), mais pas encore la **mesure** des mots : chacun
interroge sa bibliothèque. Le risque de divergence n'est donc plus théorique. Q13 l'a
mesuré sur le document d'exemple, et propose une voie d'attente qu'il faut éprouver
avant de rouvrir cette décision.

**Ce qui est en jeu.** C'est le seul chantier lourd du corpus, et c'est aussi celui qui
sépare un poseur de blocs d'un logiciel de mise en page : sans lui, aucun article ne
court sur deux colonnes ni sur deux pages. Le repousser est une position défendable —
mais elle doit être prise, pas subie par défaut.

**Quand rouvrir.** Après C. À ce moment-là, C3 aura livré la sélection multiple, C2 la
grille, C4 les champs : le contexte technique sera plus favorable, et surtout on saura,
par l'usage des modèles de B1, si le besoin est réel.

---

## Ce que ce plan ne fait pas

Écarté délibérément, avec la raison :

| Fonction | Fiche | Raison |
|---|---|---|
| Barres d'outils personnalisables | 03 | Héritage d'Office 2007, abandonné par Publisher lui-même en 2010. Une barre courte et pensée vaut mieux. |
| Volet Office commutable | 04 | Réponse au manque de place d'un écran de 2007. Deux panneaux permanents sont plus lisibles. Retenu seulement : pouvoir les replier (B7). |
| Bibliothèque d'images en ligne | 13 | Service tiers, catalogue à maintenir, licences. Disproportionné pour un logiciel local. La variante locale rejoint C7. |
| WordArt | 17 | Glyphes à vectoriser des deux côtés de la chaîne, pour une esthétique datée. Le besoin réel — un titre qui se voit — se traite en propriétés ordinaires : contour et ombre du caractère (A14) ; le dégradé de C5c ne porte que sur les fonds. |
| Retouche d'image (luminosité, contraste, nuances de gris, couleur transparente) | 14 | Traitement pixel, coûteux à exporter proprement. Relève de l'éditeur d'images en amont. |
| Zone de travail hors page | 07 | Couverte autrement, et mieux, par C7. |
| Textures et motifs de remplissage | 09 | Ressources à embarquer, esthétique datée. |
| Bordure par côté, BorderArt | 11 | Écarté par la fiche : un filet par côté se pose avec des traits (A10), et les bordures illustrées sont datées. |
| Barre Image flottante | 14 | Ses commandes vont au panneau et au menu contextuel (A12) ; une barre de plus les dupliquerait. |
| Connecteurs, points d'édition, changer de forme automatique | 22 | Dessin vectoriel : relève d'un éditeur de dessin, pas d'un poseur de blocs. |
| Symbole, objet incorporé, lien hypertexte, signet | 22 | Hors du document imprimé que MyPublisher produit. Un symbole se tape ou se colle. |
| Embarquer les polices | 21 | Licences. B4 écrit la liste des polices employées, et B5 avertit de leur substitution. |
| Bibliothèque de conception (*Design Gallery*) | 20 | Contenu à dessiner et à maintenir. C7 en offre l'usage, avec le contenu de l'utilisateur. |
| Pages maîtres | 22 | Non écarté sur le fond — simplement non planifié. À reconsidérer avec C4, dont il partage la logique de contenu répété. |

---

## Récapitulatif

Ce que chaque phase contient et ce qui la bloque. L'avancement, lui, se lit dans le
[tableau de suivi](#suivi) en tête : deux tableaux qui portent la même information
finissent toujours par se contredire.

| Phase | Postes | Nature | Bloquée par |
|---|---|---|---|
| **P** | P0 décision de nommage · P1 git · P2 contrôle · P3 banc | préalables | — |
| **A** | A1 rotation · A2 débordement · A3 chemins relatifs · A4 superposition · A5 formats · A6 barre d'état · A7 alignement vertical · A8 insertion de pages · A9 quart de tour · A10 trait · A11 aimantation · A12 menu contextuel · A13 rognage · A14 contour du caractère · A15 sections | finitions | — |
| **B** | B1 modèles · B2 import texte · B3 impression · B4 archive · B5 polices · B6 aperçu · B7 panneaux repliables | usage | — |
| **C** | C1 remplissage · C2 repères · C3 sélection multiple · C4 champs · C5 couleurs · C6 marges · C7 bibliothèque · C8 miroir · C9 tableau | structure | P1, P2 |
| **R** | R1 texte riche · R2 retraits · R3 colonnes · R4 interligne · R5 import `.pub` · R6 paragraphe au panneau · R7 texte riche à la souris | documents réels | R6 et R7 : Q1 |
| **Q** | Q1 à Q13 | dettes consignées | — |
| **D** | moteur de composition | décision | Q13, puis A, B, C |

Trois postes à faire en premier si l'on ne devait en retenir que trois, au
11 octobre 2026 : **Q1** (ne plus abîmer un document importé en tapant dedans), les
**recettes de P3** (que tout ce qui est clos se rejoue), et **Q13** (que l'écran et le
PDF coupent leurs lignes aux mêmes mots). Les trois que ce paragraphe nommait le
30 août — A2, B1, P0 — sont livrés. L'ordre complet est dans
[Ordre d'exécution](#ordre-dexécution).