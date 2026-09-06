# Plan de développement

> **À lire avant ce plan** : [`Documents-reels.md`](Documents-reels.md), relevé des deux
> documents ATR réellement composés sous Publisher. Les fiches disent ce que Publisher
> *sait faire* ; ces documents-là disent ce dont on *se sert*. Ils ajoutent quatre postes
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

*Dernière mise à jour : 6 septembre 2026.*

| Poste | Intitulé | Avancement | Où ça en est |
|---|---|:---:|---|
| **P0** | Nom du remplissage | **100 %** | `Fill` retenu et daté ; renommage fait en C1 |
| **P1** | Dépôt sous git | **100 %** | Branche `main`, commit initial `cc098e0` |
| **P2** | Contrôle de non-régression | **100 %** | `Scripts/controle.sh`, référence dans `Scripts/reference.txt` |
| **P3** | Banc de vérification | en cours | Noyau séparé, banc de rendu, comparaison écran ↔ PDF, export déterministe, 20 tests, `Scripts/verifier.sh`. Reste : les recettes des postes clos, et le bilan |
| **A1** | Poignée de rotation | 0 % | La rotation est déjà partout ; il manque la neuvième poignée |
| **A2** | Signaler le débordement du texte | 0 % | Le poste le plus important de la phase : on livre encore un PDF amputé sans le dire |
| **A3** | Chemins d'images relatifs | 0 % | — |
| **A4** | Superposition : raccourcis et glisser-déposer | 0 % | Les quatre commandes existent, et portent la sélection multiple depuis C3a |
| **A5** | Formats de page | 0 % | Le modèle accepte déjà des dimensions libres |
| **A6** | Taille du bloc dans la barre d'état | 0 % | — |
| **A7** | Alignement vertical du texte | 0 % | — |
| **B1** | Modèles | **100 %** | Catalogue au démarrage et sur `Ctrl+N` ; quatre modèles composés dans `Templates/` |
| **B2** | Importer un fichier texte | 0 % | — |
| **B3** | Imprimer | 0 % | — |
| **B4** | Archive `.mypubz` | 0 % | — |
| **B5** | Avertir sur les substitutions de polices | **100 %** | L'export nomme la police demandée et celle employée ; recette dans `Scripts/recettes/` |
| **C1** | Fond et bordure au niveau de `Frame` | **100 %** | Fond, contour, trait, arrondi et opacité sur tout bloc ; `FormatVersion` 2, migration à l'ouverture |
| **C2** | Repères | **100 %** | C2a grille de composition · C2b règles graduées et repères libres |
| **C3** | Sélection multiple, puis groupement | **100 %** | C3a sélection · C3b alignement et répartition · C3c groupe persistant |
| **C4** | Champs | **100 %** | Folio, nombre de pages, date, coordonnées et logo ; les coordonnées attendent B1 pour servir vraiment |
| **C5** | Remplissage riche et cohérence visuelle | 0 % | C5a fond de page · C5b jeux de couleurs · C5c dégradés — C5a et C5c débloqués par C1 |
| **C6** | Marges intérieures par côté | **100 %** | Quatre marges sur le bloc de texte ; `FormatVersion` 3 |
| **C7** | Bibliothèque de blocs | 0 % | Débloqué depuis C3a |
| **R1** | Texte riche : fragments de style | **100 %** | `Spans` sur le bloc ; l'écran et l'export passent par la même composition ; recette dans `Scripts/recettes/` |
| **R2** | Retraits et espacement de paragraphe | 0 % | Retrait négatif (listes à puces), retrait gauche, espace après |
| **R3** | Colonnes à l'intérieur d'un bloc | 0 % | Posées sur 25 des 27 blocs de texte réels. Autre chose que C2a |
| **R4** | Interligne : une seule mesure | **100 %** | Multiple de la hauteur naturelle ; écran et PDF concordent au pixel |
| **R5** | **Ouvrir un `.pub`** | 0 % | Exigé le 6 septembre 2026. Lecture par `pub2raw` ; ce que l'import restitue mesure l'avancement de R1–R4 et C9 |
| **D** | Moteur de composition | — | Décision non ouverte ; à rouvrir quand A, B et C sont livrées |

**Par phase** — P : 3 sur 3 · A : 0 sur 7 · B : 2 sur 5 · C : 5 sur 7 · D : non planifié.

Soit **10 postes sur 22**. C'est un compte de postes et non d'effort : C3 pesait à lui
seul plus que toute la phase A, et les préalables ne produisaient aucune fonctionnalité.
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

Sept postes qui n'inventent aucune structure : la mécanique est déjà écrite, il manque
le geste, le message ou le champ. Aucun ne touche au format de fichier autrement qu'en
ajoutant une propriété facultative. Ils peuvent se faire dans n'importe quel ordre, et
même avant P1 — mais autant commencer par P1.

### A1 — Poignée de rotation *(fiche 10)*

La rotation existe partout — modèle, test de survol, redimensionnement, export — et ne
se règle qu'au champ numérique. Ajouter une neuvième poignée au-dessus du bloc, sur le
même tableau de poignées et le même mode de glisser que le redimensionnement.

*Recette* : tourner un bloc à la souris, l'exporter, l'angle est le même à l'écran et
dans le PDF.

### A2 — Signaler le débordement du texte *(fiche 12)*

Aujourd'hui un texte trop long est coupé sans un mot, à l'écran comme à l'export. Deux
gestes : un marqueur visuel sur le bloc concerné, et un avertissement dans le rapport
d'export, qui remonte déjà jusqu'à la barre d'état.

*Recette* : un bloc au texte trop long se signale à l'écran ; son export mentionne le
bloc par son nom.

C'est le poste le plus important de la phase A : il n'ajoute aucune fonction, il
supprime un risque — livrer un PDF amputé sans le savoir.

### A3 — Chemins d'images relatifs *(fiche 21)*

À l'enregistrement, écrire le chemin en relatif quand le fichier image se trouve sous le
dossier du document ; en absolu sinon. À l'ouverture, résoudre les deux.

*Recette* : déplacer un dossier contenant un `.mypub` et ses images ne casse plus rien.

### A4 — Ordre de superposition : raccourcis et glisser-déposer *(fiche 15)*

Les quatre commandes existent et sont saines ; il leur manque des raccourcis clavier, et
la liste « Blocs (du fond vers l'avant) » invite à un glisser-déposer qui n'existe pas.

*Recette* : réordonner un bloc au clavier, puis en le tirant dans la liste.

### A5 — Formats de page *(fiche 01)*

Deux champs largeur/hauteur libres dans le panneau DOCUMENT — le modèle les accepte déjà,
ce sont des `double`. Et quatre formats à ajouter à la liste : B4, B5, Executive, Tabloid.

*Recette* : composer sur un format 100 × 200 mm et l'exporter à cette taille exacte.

### A6 — Taille du bloc dans la barre d'état *(fiche 02)*

Publisher affiche en permanence position **et** taille du bloc sélectionné. MyPublisher
n'affiche que la position du curseur. Pendant un redimensionnement, l'œil est sur la
page, pas sur le panneau de droite.

*Recette* : la taille se lit sans quitter la page des yeux pendant le geste.

### A7 — Alignement vertical du texte *(fiche 12)*

Trois valeurs — haut, milieu, bas. Propriété nouvelle et facultative, donc sans
migration. Immédiat à l'écran, calculable à l'export à partir de la hauteur mesurée.

*Recette* : un titre centré verticalement dans son bloc l'est aussi dans le PDF.

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
| Volet Office commutable | 04 | Réponse au manque de place d'un écran de 2007. Deux panneaux permanents sont plus lisibles. Retenu seulement : pouvoir les replier. |
| Bibliothèque d'images en ligne | 13 | Service tiers, catalogue à maintenir, licences. Disproportionné pour un logiciel local. La variante locale rejoint C7. |
| WordArt | 17 | Glyphes à vectoriser des deux côtés de la chaîne, pour une esthétique datée. Le besoin réel — un titre qui se voit — se traite en propriétés ordinaires : contour, ombre, dégradé (C5c). |
| Retouche d'image (luminosité, contraste, nuances de gris) | 14 | Traitement pixel, coûteux à exporter proprement. Relève de l'éditeur d'images en amont. |
| Zone de travail hors page | 07 | Couverte autrement, et mieux, par C7. |
| Textures et motifs de remplissage | 09 | Ressources à embarquer, esthétique datée. |
| Pages maîtres | 22 | Non écarté sur le fond — simplement non planifié. À reconsidérer avec C4, dont il partage la logique de contenu répété. |

---

## Récapitulatif

Ce que chaque phase contient et ce qui la bloque. L'avancement, lui, se lit dans le
[tableau de suivi](#suivi) en tête : deux tableaux qui portent la même information
finissent toujours par se contredire.

| Phase | Postes | Nature | Bloquée par |
|---|---|---|---|
| **P** | P0 décision de nommage · P1 git · P2 contrôle | préalables | — |
| **A** | A1 rotation · A2 débordement · A3 chemins relatifs · A4 superposition · A5 formats · A6 barre d'état · A7 alignement vertical | finitions | — |
| **B** | B1 modèles · B2 import texte · B3 impression · B4 archive · B5 polices | usage | — |
| **C** | C1 remplissage · C2 repères · C3 sélection multiple · C4 champs · C5 couleurs · C6 marges · C7 bibliothèque | structure | P1, P2 |
| **D** | moteur de composition | décision | A, B, C |

Trois postes à faire en premier si l'on ne devait en retenir que trois : **A2**
(ne plus livrer un PDF amputé en silence), **B1** (les modèles, qui rendent visible tout
le reste), **P0** (trancher le nom pendant que cela ne coûte encore rien). Le troisième
est fait depuis le 5 septembre 2026 ; les deux autres restent en tête de liste.
