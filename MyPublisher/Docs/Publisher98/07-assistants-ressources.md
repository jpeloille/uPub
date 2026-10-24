# 07 — Assistants, Design Gallery, jeux de couleurs, BorderArt, papiers

**Domaine** : point dur n° 2, côté ressources de conception : comment Publisher 98 aide à
composer (assistants, conceptions, jeux de couleurs, informations personnelles, ornements,
papiers pré-imprimés), et ce que cela dit des modèles de MyPublisher.
**Sources** : `pubwiz/*.des` (37) et `*.poc` (61), `agent98.reg`, `pub50.ctg`, `clrscm.reg`,
`clrchk.jsp`, `infoshet.jsp`, `cheklist.jsp`, `pubbrd/*.bdr` (15), `pubspapr/papers.ini` et
`zpapers.ini`, `morph98.dll` et `mor5int.dll`, chaînes et dialogues de `pub5int.dll`, aides ;
libmspub.
**Verdict** : `partiel` — la couleur par rôle et le jeu de polices existent, mieux écrits que
chez Publisher 98 ; il manque les modèles paramétrés, la réserve de contenu, les blocs étiquetés,
et le jeu de coordonnées retenu par le document.

Notations : voir [`00-index.md`](00-index.md#notations). Les scripts de cette fiche sont dans
`$S/scripts/s07/` et leurs sorties dans `$S/sorties07/`. Fiches voisines :
[`05-modele-document.md`](05-modele-document.md) ; côté Publisher 2007,
[`../Publisher2007/01-nouveau-document.md`](../Publisher2007/01-nouveau-document.md),
[`18-modeles.md`](../Publisher2007/18-modeles.md),
[`19-informations-professionnelles.md`](../Publisher2007/19-informations-professionnelles.md),
[`20-bibliotheque-conception.md`](../Publisher2007/20-bibliotheque-conception.md).

## Ce que fait Publisher 98

Publisher 98 ne livre pas des modèles qu'on copie. Il livre une chaîne de paramétrage à quatre
étages, chacun dans son fichier :

| Étage | Fichier | Rôle |
|---|---|---|
| Catalogue | `pub50.ctg` | Arbre « type de publication, sous-type », et familles de conception déclinées sur plusieurs types |
| Questionnaire | `pubwiz/*.des` | Les questions posées, sous forme déclarative : pages, choix, valeurs, conditions |
| Contenu | `pubwiz/*.poc` | Les conceptions, variantes comprises, avec texte d'attente et polices |
| Moteur | `morph98.dll`, `mor5int.dll` | Appliquer une réponse ; changer de conception sans perdre le contenu |

Trois ressources transverses paramètrent toutes les conceptions : le jeu de couleurs, le jeu
d'informations personnelles et, pour le papier du commerce, les papiers spéciaux. La BorderArt
est une bibliothèque d'ornements. Une conception est ainsi une fonction de quelques paramètres
(variante, couleurs, coordonnées, format, colonnes, présence d'un logo), et ces paramètres
restent modifiables après la création, sans perte.

## Ce qui a été relevé

### Les assistants

**K1 — Un petit langage parenthésé.** Les 37 `.des` suivent une grammaire stricte :
`WIZARD` > `PAGE` > (`STATIC`, `CUSTOM`, `PUSHBUTTON`, `CHECKBOX`, `RADIOGROUP` > `RADIOBUTTON`,
`COMBOBOX` > `ITEM`), avec des attributs `NOM=valeur`. Effectifs : 160 pages, 332 `STATIC`,
70 groupes pour 227 boutons radio, 57 `CUSTOM`, 57 boutons, 6 cases, 2 listes. Le vocabulaire
compte 28 mots, dont `ID`, `OPF` et `OID`. Commande : `$PY $S/scripts/s07/des_parse.py $CD/pubwiz`.
Confiance : haute.

**K3 — Ce que fait une réponse.** `OPYID` désigne une propriété de la publication et `VALUE` la
valeur posée ; `DIRECT` désigne une commande immédiate (réinitialiser la conception, choisir l'un
des quatre jeux d'informations personnelles, insérer un composant, une page) ; `CUSTOM ID=1` est
le choix du jeu de couleurs, `ID=2` la galerie des conceptions ; `MCAPTION` est le libellé en
unités métriques. Ces numéros n'ont de sens que dans le code du moteur : la séparation entre
questionnaire et moteur ne vaut que pour la présentation. Commande :
`$PY $S/scripts/s07/des_map.py $CD/pubwiz`. Confiance : moyenne.

**K4 — Une description, deux modes.** Le même fichier décrit le mode pas à pas et le volet qui
suit « Terminer » : `HIDEON=1` cache dans le volet, `HIDEON=2` en mode pas à pas. Les réponses
restent modifiables après la création. Preuve : `news98.des` ; sujet 416. Commande :
`sujet 416 $M14`. Confiance : moyenne.

**K5 — Changer sans perdre.** Le moteur demande confirmation quand une réponse exige une autre
variante (« Do you want to change to a design that does not include a logo? »), prévient d'un
changement de mise en page (217:1.4), borne certaines réponses (bannière d'au moins 3 pieds, dix
liens au plus), et range ce qui ne tient plus dans « Extra Content » au lieu de le détruire.
Preuve : `mor5int.dll`, table 217:1 (+0x1F08) et noms en 217:1001 (+0xAD0) ; `pub5int.dll`
217:2.7, 88.114-115 ; sujet 481. Commande :
`sed -n '/^\[217:1\]/,/^1.132/p' $X/pe/mor5int.dll/chaines-217.txt`. Confiance : haute. Que chaque
action d'assistant soit un pas d'annulation se déduit des libellés 88.92 à 88.97 « Wizard
Action » : déduit, confiance haute.

**K6 — Le registre des types.** *Constat noté incertain.* `agent98.reg` déclare 118 types
d'assistants, chacun avec son questionnaire, son contenu et des sous-contenus nommés ; 60 sont des
« Morph Groups » et 58 portent une étiquette (logo, titre, coupon…) que l'assistant sait retrouver
dans une page et remplacer. 38 des 75 questionnaires déclarés sont absents du CD (versions
japonaises, fichiers de test, Web).
Commande : `iconv -f cp1252 -t utf-8 $CD/agent98.reg | tr -d '\r' | grep -c '"MG"'`.
Confiance : moyenne.

**K7 — Les `.poc` et le texte d'attente.** En-tête de 12 octets (`POCPB5` puis `FD 01 00 05 06 01`),
répertoire d'entrées de 20 octets (1 à 757 entrées, 3 060 en tout). Le texte d'attente annonce sa
capacité (« This story can fit 175-225 words. »). Le format des morceaux est décrit dans
[`06-format-pub.md`](06-format-pub.md).
Commande : `strings -el -n 6 $CD/pubwiz/news98.poc | grep fit`. Confiance : haute.

**K16 — `pub50.ctg` est l'arbre du dialogue de démarrage**, et non le catalogue des images :
types, sous-types et familles de conception (« Master Sets »), chaque famille déclinée en carte,
en-tête, enveloppe, dépliant. Signature `FF 'BYROND' FE 07 00 01 00`.
Commande : `strings -el -t x -n 4 $CD/pub50.ctg | awk '/ Master Sets$/,/ Mastheads$/'`.
Confiance : haute. Un second catalogue, `pub50sp.ctg` (« Paper Catalog File », `pub5.stf`
ligne 355), a la même signature et range les conceptions pour papiers spéciaux (« Special Paper
Price List »…) ; il n'a pas été étudié au-delà de ses premières chaînes
(`strings -el -n 4 $CD/pub50sp.ctg | head`).

**K17 — Deux documents pour l'imprimeur.** `infoshet.jsp` est une fiche à champs entre accolades
(`{filename}`, `{page_size}`, `{font}` ×25, `{specialfx}` ×10…) que Publisher remplit ;
`cheklist.jsp` est la liste des questions à poser à l'imprimeur. Voir
[`08-impression-pdf.md`](08-impression-pdf.md). Confiance : haute.

### Les jeux

**K8 — Pas de jeu de polices exposé.** *Constat noté incertain.* Les conceptions figent leurs
familles (Gill Sans MT, Garamond, Franklin Gothic…), que le CD livre. Le dialogue 518 « Font
Schemes » existe pourtant dans les ressources (+0xD62CE, « Choose a font scheme: », liste 6751),
sans menu ni aide pour l'atteindre : c'est une ébauche. MyPublisher a repris le jeu de polices de
Publisher 2007, sa référence (`MyPublisher.Core/Models/Jeux.cs:51`). Commande : `dlg 518`.
Confiance : moyenne.

**K9 — Les jeux de couleurs.** `clrscm.reg` contient 61 jeux, chacun écrit `nom|0|8 couleurs`. Une
couleur est un RVB littéral (octet fort 0x20, ordre `0x00BBGGRR`) ou un indice dans la palette de
base de 56 couleurs. Preuve : libmspub `MSPUBParser2k.cpp:97-110` et `112-231` ; noms de la palette
en 217:19 (+0xE000). Commande : `$PY $S/scripts/s07/clrscm.py $CD/clrscm.reg`. Confiance : haute.

**K10 — Huit rôles**, dans l'ordre : principale, accents 1 à 4, lien, lien visité, accent 5. Les
cinq premiers sont ceux de la charte imprimable `clrchk.jsp`. L'accent 5 est blanc dans les 61
jeux et se comporte en couleur de papier ; l'accent 4 vaut #CCCCCC dans 56 jeux. Preuve : 217:20
(+0xE3FC). Confiance : moyenne.

**K11 — Le comportement des jeux.** Un seul jeu personnalisé par publication, dérivé d'un jeu
standard, qui peut contenir des teintes ; seuls les objets en couleurs du jeu changent ; une
commande rapproche les couleurs libres de la couleur du jeu la plus proche ; les jeux sont
indisponibles en impression en ton direct ; un jeu peut faire partie des informations
personnelles. Preuve : sujets 144, 150, 152, 164, 166, 168, 894 ; 217:74.1, 114.56, 114.61,
88.111. Commande : `chaine '74\.1|114\.(56|61)'`. Confiance : haute.

**K12 — Les informations personnelles** : quatre jeux aux noms imposés, de huit composants (nom,
fonction, organisation, adresse, devise, téléphones et courriel, logo, jeu de couleurs). La
publication retient son jeu ; retoucher un composant dans la page l'écrit dans le jeu
(« Propagate Personal Information »). Preuve : sujets 892, 902 ; 217:64, 65, 56.34-56.44, 88.109.
Confiance : haute.

### Ornements et papiers

**K13 — Le format `.bdr` de la BorderArt.** « XT », version 1, nombre *N* de bordures, *N* noms de
42 octets, puis *N*+1 enregistrements de 22 octets : taille par défaut en twips, longueur d'un
motif des côtés haut et bas, longueur d'un motif des côtés gauche et droit, huit décalages vers
des métafichiers WMF placés en ordre horaire depuis le coin haut-gauche ; le dernier enregistrement
borne la fin du fichier. 164 bordures en 15 fichiers, dont 89 à une seule image.
Commande : `$PY $S/scripts/s07/bdr4.py $CD/pubbrd/*.bdr`. Confiance : haute.

**K14 — Tailles et étirement.** *Constat noté incertain.* La taille par défaut est en twips (80,
100, 120, 200 et 360 pour cinq bordures que `mor5int.dll` dimensionne à 4, 5, 6, 10 et 18 pt ;
217:1.121 à 1.130), et la longueur de motif vaut la taille multipliée par le rapport de l'image de
côté. La lecture de la valeur 20 000 twips (12 bordures) comme un côté étiré reste une déduction.
Le sens d'« étirer » se lit dans libmspub (`MSPUBCollector.cpp:836-866`) et dans le sujet 70 :
« Stretch pictures to fit » donne un nombre entier de motifs, arrondi au plus proche, déformés
pour tomber juste ; « Don't stretch » arrondit par défaut et répartit le reste en espacement.
L'ordre des huit positions et la recoloration par la couleur de trait y sont aussi
(`MSPUBCollector.cpp:906-1024`). Confiance : moyenne.

**K15 — Les papiers pré-imprimés.** `papers.ini` et `zpapers.ini`, 51 entrées chacun, une section
par format de feuille ; chaque ligne donne l'aperçu recto et verso, les cotes, la position, les
gabarits et la référence du fournisseur. L'image s'affiche mais ne s'imprime pas (217:106.0) ; les
gabarits WMF viennent de Publisher 97, et l'installation les efface (`pub5.stf:130`,
`RemoveSection "Remove Papers 97"`, section `pub5.inf:496`).
Commande : `iconv -f cp1252 -t utf-8 $CD/pubspapr/papers.ini | head`. Confiance : haute.

## Principes à retenir, et ce qui ne vaut plus en 2026

**Une conception est une fonction de paramètres nommés.** La publication garde ses réponses, et
l'assistant se rejoue à tout moment. Vaut. À ne pas reproduire : les identifiants numériques
opaques (`OPYID`, `DIRECT`) et leur sens enfoui dans le code. La leçon est que les **actions**
doivent elles aussi être des données nommées (« poser telle propriété sur tel bloc étiqueté »),
sans quoi un tiers ne peut pas écrire un modèle et l'on reproduit le couplage de 1997.

**Le questionnaire est une donnée.** Les questions vivent dans un fichier lisible ; le programme
n'en connaît que le vocabulaire. Vaut, avec un schéma validé. Une grandeur porte son unité et
l'interface la formate, plutôt qu'un libellé métrique en double.

**Changer sans perdre.** Ce qui ne tient plus va dans une réserve ; chaque action d'assistant
s'annule. Vaut. Le dialogue de confirmation devient superflu quand la réserve et l'annulation
existent.

**La couleur s'écrit par rôle.** Changer de jeu recolore les objets en couleurs du jeu et eux
seuls ; les couleurs libres se rapprochent du jeu sur demande ; un jeu personnalisé se dérive d'un
jeu standard. MyPublisher l'a déjà (C5b) ; OOXML en a fait ses couleurs de thème. À ne pas
reproduire : la palette indexée, le RVB sans profil, les jeux coupés en ton direct ; un rôle peut
désigner un ton direct ou un CMJN et rester un rôle sous PDF/X.

**L'identité suit l'utilisateur, la publication dit laquelle.** Vaut. À ne pas reproduire : quatre
jeux aux noms imposés, le registre, et la retouche dans la page écrite en silence dans le jeu ; une
commande explicite « Enregistrer dans le jeu » est préférable.

**Une identité graphique se décline par familles** (carte, en-tête, enveloppe, dépliant). Vaut :
c'est la charte graphique, peu coûteuse dès que couleur et typographie sont des rôles.

**Le texte d'attente informe** : il dit à quoi sert le bloc et combien de mots il tient. Vaut, et
se fait mieux : calculé par la composition réelle plutôt qu'écrit à la main pour un corps.

**Un ornement de bord est un motif à huit positions** (quatre coins, quatre côtés, une taille de
coin, des côtés répétés ou étirés, une recoloration). Le principe vaut, mais le plan écarte la
BorderArt (`MyPublisher/Docs/PLAN.md:4304`, « les bordures illustrées sont datées ») : il n'y a lieu
d'y revenir que si un document réel le demande. À ne pas reproduire : le WMF, les décalages sur 16
bits qui morcellent la collection, le contour limité au rectangle.

**Un support pré-imprimé se montre sans s'imprimer.** Vaut : une image de repérage visible à
l'écran, jamais écrite au PDF (PDF/X-1a et X-3 excluent les calques optionnels). C'est un cas voulu
où l'écran diffère de la sortie, à confronter à la règle d'égalité écran ↔ PDF du dépôt.

**Une seule unité de travail.** Publisher 98 mêle EMU, twips, pouces et centimètres écrits en
clair. Toute ressource nouvelle se convertit en points à la lecture.

## État dans MyPublisher

- **Jeux de couleurs et de polices** : cinq rôles par référence dans la chaîne (`scheme:accent1/80`),
  et un jeu de polices, portés par le document (`MyPublisher.Core/Models/Jeux.cs:10-64`,
  `:86-170`, `:183-242` ; `MyPublisher.Core/Models/Document.cs:202-241`) ; six jeux livrés
  (`Jeux.cs:260-323`). Le commentaire de `Jeux.cs:245-247` parle de « soixante-dix jeux de
  Publisher » ; Publisher 98 en a 61.
- **Retoucher un jeu** : l'enregistrement est immuable pour que le remplacement prévienne l'écran
  (`Jeux.cs:20-24`) ; ce n'est pas une règle de produit. Le document porte la copie complète des
  valeurs (`Document.cs:217`) et le sélecteur reconnaît un jeu par égalité de valeur : un jeu
  personnalisé est un nouvel enregistrement. Il ne manque que l'interface.
- **Coordonnées** : sept composants, sans jeu de couleurs (`MyPublisher.Core/Models/BusinessInfo.cs:17-27`,
  `MyPublisher.Core/Models/Field.cs:19-29`). Le document emploie le jeu actif de l'application
  (`MyPublisher/ViewModels/MainWindowViewModel.cs:399`, `:823`), y compris à l'export en ligne de
  commande (`MyPublisher/Program.cs:78`). C'est un choix écrit : les coordonnées suivent
  l'utilisateur, pas le fichier, et un modèle envoyé à un collègue montre les siennes
  (`BusinessInfo.cs:10-12`, `MyPublisher.Core/Services/BusinessInfoStore.cs:11-16`).
- **Édition des coordonnées dans la page** : impossible, le texte est calculé
  (`Field.cs:60-76`) ; le texte saisi reste sous le champ (`MyPublisher/Docs/PLAN.md:1931-1933`).
- **Modèles** : le catalogue se charge par `MyPublisher.Core/Services/TemplateStore.cs:24-63`, et
  l'on démarre d'une copie relue sans chemin (`MainWindowViewModel.cs:774-778`), pour que deux
  documents partis du même modèle ne partagent rien (`TemplateStore.cs:10-14`). Pas de paramètres.
- **Texte d'attente** : il explique le geste (`MyPublisher/Templates/bulletin.mypub:270`), sans
  capacité.
- **Contour** : plein, tireté ou pointillé (`MyPublisher.Core/Models/Frame.cs:26`, `:167-181`).
- **Bibliothèque de blocs** : C7 à 0 %.

## Écart

| Sujet | MyPublisher | Écart |
|---|---|---|
| Modèles paramétrés | Copie sans paramètres | Changer de variante oblige à recomposer |
| Réserve de contenu | Absente | Nécessaire avec les variantes |
| Blocs étiquetés | Groupes sans étiquette | Un logo ou un titre ne se reconnaît pas comme tel |
| Jeu de coordonnées | Celui de l'application | Choix écrit, à réviser ou non (G1) |
| Jeu de couleurs dans l'identité | Absent | Une société ne transporte pas ses couleurs |
| Jeu de couleurs personnalisé | Le modèle le permet, l'interface non | Petit |
| Rapprocher les couleurs libres | Absent | Les `.pub` importés restent en couleurs libres |
| Jeu de polices | Présent | MyPublisher suit Publisher 2007 ; Publisher 98 n'en expose pas |
| Texte d'attente calibré | Absent | Peu coûteux avec la composition |
| BorderArt | Absente | Écartée par le plan |
| Papier pré-imprimé | Absent ; le fond de page s'imprime | Pas de calque de repérage |

L'import d'un `.pub` perd ce que ce domaine porte : réponses d'assistant, jeu de couleurs nommé,
jeu d'informations personnelles. libmspub dessine la BorderArt en images, tuile par tuile. Les
indices 0xC0 et 0xE0, que libmspub décrit comme « index into user palette »
(`MSPUBParser2k.cpp:234-246`), désignent peut-être le jeu de la publication ; si c'est le cas,
l'import pourrait produire des références `scheme:` au lieu de couleurs libres (confiance basse).

## Postes proposés

Codes du tableau unique de [`00-index.md`](00-index.md#postes-proposés).

- **G1 — Le document retient son jeu de coordonnées.** C'est une **révision** de C4, à arbitrer
  par le propriétaire : l'argument vaut pour un document fini (une carte composée pour la société B
  ne doit pas s'imprimer avec les coordonnées de A), pas pour un modèle. Recette : deux jeux A et B,
  carte composée avec B actif, enregistrée, A rendu actif, rouverte : l'écran et le PDF (relu par
  `pdftotext`) portent B, y compris par l'export en ligne de commande ; B renommé dans le magasin :
  l'export refuse ou demande un choix explicite, plutôt que d'imprimer A. La recette de C4
  (`PLAN.md:242`) est à reprendre.
- **G2 — Le jeu de coordonnées porte un jeu de couleurs.** Recette : jeu A réglé sur « Océan » ;
  une carte de visite neuve prend les valeurs d'Océan sur les 14 références `scheme:` de
  `MyPublisher/Templates/carte-de-visite.mypub` ; un document enregistré avant le réglage se rouvre
  sans un pixel de différence.
- **G3 — Jeu de couleurs propre au document, et rapprochement des couleurs libres.** Recette :
  sur `Samples/recette-jeux.mypub`, personnaliser l'accent 2 : les références suivent à l'écran et au
  PDF, les points libres ne bougent pas, le jeu livré reste intact, le jeu propre survit à
  l'enregistrement ; sur un `.pub` ATR importé, « Rapprocher » à seuil nul convertit exactement les
  couleurs égales à celles du jeu, sans un pixel de différence, en un pas d'annulation.
- **G4 — Texte d'attente calibré.** Recette : bloc de 80 × 100 mm en corps 10, la commande annonce
  N mots ; N mots du texte de référence ne débordent pas (A2 muet), N + 10 % débordent ; en corps 12,
  N diminue ; N est le même à l'écran et à l'export.
- **G5 — Modèle paramétré.** Des questions nommées, des actions nommées qui posent des valeurs sur
  des blocs étiquetés, des réponses gardées, une réserve pour ce qui est retiré. Recette : carte de
  visite avec la question « logo : oui / non » ; « non » retire le logo et décale le nom, en un pas
  d'annulation ; après saisie d'un texte, « oui » rend le logo à sa position exacte et garde le
  texte ; la réponse est relue après enregistrement ; un modèle sans questions s'ouvre sans un pixel
  de différence.
- **G6 — Bibliothèque de blocs fidèle au jeu** (précision de C7). Recette : un bloc-titre en
  `scheme:accent1` et `scheme:heading`, rangé depuis un document Marine et posé dans un document
  Océan, prend l'accent 1 et la police de titre du second, à l'écran comme au PDF ; une couleur libre
  du même bloc ne change pas. Le plan écarte la Design Gallery livrée (`PLAN.md:4309`) ; la réserve
  propre à la publication reste une question (fiche 05).
- **G7 — Bordure à motif**, à rediscuter seulement si un document réel le demande, le plan l'ayant
  écartée. Si le poste s'ouvre : motifs vectoriels propres au projet, un même analyseur pour l'écran
  et le PDF, au plus huit objets PDF quelle que soit la taille du bloc, et les deux modes de remplissage
  de K14, éprouvés chacun.
- **G8 — Papier pré-imprimé.** Recette : l'écran montre le papier ; `pdfimages -list` sur le PDF
  ne liste aucune image, et le PDF ne diffère d'aucun pixel du même document sans papier.

## Ce qui ne doit pas entrer dans le dépôt

Les palettes de `clrscm.reg` ne servent pas à définir les jeux livrés par MyPublisher ; aucun motif
de `.bdr`, aperçu de papier ou contenu de `.poc` n'entre dans `Templates` ni dans `Samples`. La
table des 56 couleurs et l'algorithme de pose de la BorderArt de libmspub sont sous MPL 2.0 : on en
décrit le principe avec nos mots, et l'on redérive les valeurs plutôt que de reprendre le code.

## Questions ouvertes

1. Sens exact de `DISABLEON`, des conditions négatives et de `PAGEID` : il faudrait le code pour le
   savoir, et le mécanisme proposé n'en a pas besoin.
2. Sens du champ « 0 » qui suit le nom dans `clrscm.reg`, constant sur les 61 jeux, et rôle exact
   de l'accent 5.
3. Le cinquième champ du répertoire des `.poc` varie dans 14 fichiers sur 61 : son rôle est à
   revoir.
4. Faut-il, comme Publisher 98, écrire dans le jeu de coordonnées quand on retouche un composant
   dans la page ? La proposition est non par défaut, avec une commande explicite.
