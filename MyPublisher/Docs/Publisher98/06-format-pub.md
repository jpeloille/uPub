# 06 — Le format `.pub` de Publisher 98, et ce qu'il enseigne au `.mypub`

**Domaine** : points durs n° 3 (format de fichier) et n° 5 (import et export `.pub`) : comment
Publisher 98 range une publication, ce que libmspub en lit, ce que l'import de MyPublisher en
garde, et ce que le `.mypub` peut en retenir.
**Sources** : les cinq documents OLE du CD (`mspub.pub`, `zmspub.pub`, `cheklist.jsp`,
`clrchk.jsp`, `infoshet.jsp`), `pubwiz/*.poc` et `*.des`, `clrscm.reg`, `pub5.inf`, `pub5.stf`,
chaînes de `pubole98.dll` et de `pub5int.dll`, aides ; un document récent de comparaison
(`MyPublisher/Books/ATA 21 - Air conditioning.pub`, lu sans écriture) ; libmspub (source au
commit `66701376`, binaire 0.1.4 extrait par `Scripts/outils-pub.sh`). **Aucun désassemblage** :
la lecture des fichiers de données et le code public de libmspub ont suffi, si bien que
l'exception de décompilation pour l'interopérabilité (CPI art. L122-6-1, IV) n'a pas eu à être
invoquée.
**Verdict** : `partiel` — les documents de Publisher 98 s'ouvrent tous, aucun fidèlement : le
texte passe à 87 %, aucune forme.

Notations : voir [`00-index.md`](00-index.md#notations). Les scripts de cette fiche sont dans
`$S/etude/06-scripts/` (variable `$SC` ci-dessous) et ne lisent que des fichiers de données.
Fiches voisines : [`01-moteur-texte.md`](01-moteur-texte.md),
[`05-modele-document.md`](05-modele-document.md) ; côté Publisher 2007,
[`../Publisher2007/21-pack-and-go.md`](../Publisher2007/21-pack-and-go.md).

## Ce que fait Publisher 98

Le `.pub` de Publisher 98 est un stockage composé OLE (CFB). Chaque composant y a sa partie :
le flux `Contents` porte la mise en page et appartient à l'application ; le sous-stockage
`Quill/QuillSub`, avec son propre identifiant de classe, porte tout le texte et appartient au
moteur de textes ; `Objects` accueille les objets OLE ; `SummaryInformation` ne contient qu'une
vignette.

Le texte est séparé de la mise en page : un cadre désigne une story et son rang dans la chaîne,
et le texte chaîné n'est écrit qu'une fois. Les objets ont un identifiant de 16 bits, alloué à
partir de 0x100, croissant et jamais réutilisé, et un parent. Les longueurs sont des entiers en
EMU. L'enveloppe n'a pas changé de Publisher 95 aux versions récentes ; la version se lit dans le
contenu. Publisher refuse une version inconnue, demande avant de mettre un fichier ancien au
nouveau format, et peut garder une copie de secours.

## Ce qui a été relevé

### Le conteneur

**C2 — L'identité du fichier ne dit pas sa version.** Publisher 98 écrit encore le nom de
classe de Publisher 95 (« MSPublisher.3 », « Microsoft Publisher 3.0 »). Un document récent
(ATA 21) porte le même identifiant de classe 00021201, le même `CompObj` et le même `\x03Internal`.
La version se lit au troisième octet de `Contents` : 0x22 pour le CD, 0x2C pour ATA 21 ; libmspub
n'aiguille pas autrement (`MSPUBDocument.cpp:44-63`).
Preuve : `pubole98.dll`, UTF-16 à +0xED20 et +0xED3C.
Commande : `strings -t x -el -n 4 $CD/pubole98.dll | grep -i publisher ; $PY $SC/ole_more.py $CD/mspub.pub`.
Confiance : haute. Un second indice reste à exploiter : le `u16` en 0x04 de `Contents` vaut
0x012C dans les cinq fichiers de 1998 et 0x03E8 dans ATA 21.

**C3 — `SummaryInformation` ne contient qu'une vignette** : la propriété 17, au format
presse-papiers 3 (métafichier WMF), sans titre ni auteur. Commande : `$PY $SC/ole_more.py $CD/mspub.pub`.
Confiance : haute.

**C18 — Le cycle de vie du fichier.** Refus d'une autre version (217:100.26) ; mise à niveau à
l'enregistrement après demande, avec rappel que le fichier ne s'ouvrira plus dans l'ancienne
version et proposition d'« Enregistrer sous » (217:103.27) ; fin de prise en charge des fichiers
1.0 (217:114.38) ; copie de secours « Backup of » (217:1.95), qui a un enregistrement de retard
(sujet 1430) et double la place (sujet 1448).
Commande : `chaine '100\.26|103\.27|114\.38|1\.95'`. Confiance : haute.

### Le flux `Contents`

**C4 — La table des blocs et les identifiants.** *Constat noté incertain.* L'adresse de la table
est le `u32` en 0x16 (0x3AC) ; elle contient un `u16` *n* puis *n* entrées de 10 octets
(drapeaux, identifiant, parent, décalage). Les identifiants commencent à 0x100 et sont uniques ;
le champ de drapeaux de la première entrée vaut l'identifiant maximal. `clrchk.jsp` a 537 blocs
pour des identifiants jusqu'à 0x3A4 : les identifiants des objets supprimés ne sont pas
réutilisés. Lecture identique à `MSPUBParser2k.cpp:353-428`.
Commande : `$PY $SC/contents_index.py $CD/clrchk.jsp`. Confiance : haute pour les octets,
moyenne pour l'interprétation du compteur.

**C5 — Des entiers en EMU, origine au centre de la page.** 914 400 EMU par pouce ; la page
Lettre fait 7 772 400 × 10 058 400, l'A4 de `zmspub.pub` 7 560 000 × 10 692 000. Le cadre de
boîte (−3,25 ; 0,927) po est rendu par `pub2raw` en x = 1,0 po, y = 6,427 po. Les filets sont
codés sur un octet en quarts de point (0x81 pour le filet fin), la rotation en dixièmes de degré,
sens trigonométrique. Preuve : `MSPUBParser2k.cpp:482-487`, `:639-648`, `:80-95`, `:502-506`.
Commande : `$PY $SC/contents_index.py $CD/cheklist.jsp`. Confiance : haute.

**C6 — Le document, ses pages et la page des prototypes.** *Constat noté incertain.* Le bloc
document porte en +0x08 l'identifiant d'une page hors liste, faite d'un objet de taille nulle par
outil (vraisemblablement la mise en forme par défaut de chaque outil), et en +0xB2 et +0xBC la
liste des pages. L'entrée 1 est la page d'arrière-plan (repères de type 0x2C et pied de page) ;
les suivantes sont les pages du document dans l'ordre ; les dernières sont des pages spéciales de
rôle inconnu.
Commande : `$PY $SC/doc_pages.py $CD/cheklist.jsp ; $PY $SC/chunk_hex.py $CD/mspub.pub 0x10f`.
Confiance : moyenne.

**C7 — Où Publisher 98 range le fond et les filets.** Pour ses rectangles, le type de
remplissage est en +0x28 (2 = uni) et la couleur en +0x20 ; libmspub lit +0x2A et +0x22. Le
premier filet est en +0x2A ; libmspub le lit en +0x2C. Les 394 rectangles de `clrchk.jsp` ont
tous +0x28 = 2, et les couleurs lues en +0x20 (0x20666633, 0x20E0E0D6, 0x20CCCCCC) figurent telles
quelles dans `clrscm.reg` (jeu « Mist »). Côté libmspub : `MSPUBParser2k.cpp:577-585`, `:730-733`.
Commande : `$PY $SC/fill_tally.py $CD/clrchk.jsp ; grep -i 20666633 $CD/clrscm.reg`.
Confiance : moyenne. Que les décalages de libmspub soient justes pour Publisher 2000 n'est pas
démontré : aucun fichier 2000 n'a été lu.

**C8 — Une couleur est une référence typée** : un `u32` dont l'octet fort donne la nature
(0x00 ou 0x80 : indice dans une palette fixe de 56 couleurs ; 0x20 ou 0x90 : RVB ; 0xC0 ou 0xE0 :
case du jeu de couleurs). `clrscm.reg` code ses huit couleurs par jeu de la même façon.
Preuve : `MSPUBParser2k.cpp:97-247`. Commande : `head -c 1500 $CD/clrscm.reg`. Confiance : haute.

**C9 — Les cadres chaînés partagent une story.** Dans `infoshet.jsp`, les cadres 0x113 et 0x156
portent la même story n° 1 en +0x58 et un rang de 0 puis 1 en +0x5C ; ils diffèrent aussi en
+0x17 (01 dans le premier, 00 dans le dernier). Le bloc 0x115 (type 0x43) les cite dans l'ordre,
et le bloc 0x125 (type 0x4C) les cite aussi. Le texte n'est stocké qu'une fois.
Commande : `$PY $SC/chunk_hex.py $CD/infoshet.jsp 0x113,0x156 ; $PY $SC/refs.py $CD/infoshet.jsp 0x113,0x156`.
Confiance : moyenne (rôles déduits).

### Le flux Quill

**C10 — L'index `CHNKINK`.** En-tête de 0x18 octets, puis des blocs d'index chaînés par
décalage, aux entrées de 24 octets (nom, numéro, second nom, décalage, longueur). Blocs relevés :
`TEXT` (UTF-16LE, toutes les stories bout à bout), `STRS` (longueurs), `SYID` (numéros), `FDPC` et
`FDPP` (pages de 512 octets), `BTEC` et `BTEP` (tables de ces pages), deux `STSH` (feuille de
styles), `FONT` (noms), `TOKN` (champs : une entrée à la position du « # » de « Page # »), `TCD`
(fins de cases de tableau), `SGP`, `INK`, `MCLD`. Les données de `BTEP` valent 0x5E00 à 0x6800,
les décalages des six `FDPP`. Lecture conforme à `MSPUBParser.cpp:1006-1224`.
Commande : `$PY $SC/quill_index.py $CD/cheklist.jsp`. Confiance : haute.

**C11 — Une filiation Word.** *Constat noté incertain.* Pages de propriétés de 512 octets
désignées par une table de pages, tables à plages (`PLC`), feuille de styles `STSH` : ce sont les
structures du format binaire de Word 97, noms compris. Confiance : moyenne.

**C12 — Le stockage du texte a traversé les versions.** ATA 21 garde l'en-tête
`CHNKINK 04 00 07 00` et les mêmes blocs, plus `PL␠␠` (palette du texte). La mise en page, elle, a
été refondue à partir de 2002 (dessin Office, blocs étiquetés). Confiance : haute.

**C13 — Les `.poc` ne sont pas un sous-ensemble de `Contents`.** Après l'en-tête `POCPB5` et un
répertoire, ils se composent de morceaux préfixés par leur longueur, faits de blocs
« identifiant, type, données » selon la table de longueurs de libmspub pour Publisher 2002
(`MSPUBParser.cpp:210-251`), plus un type 0xF0 sans donnée. Texte UTF-16 et noms de police y sont
incorporés. 33 fichiers sur 61 se lisent de bout en bout ; les autres butent sur des types absents
de la table (0xE0, 0xE8, 0xF2). Commande : `$PY $SC/poc_walk.py $CD/pubwiz x`. Confiance : haute.

### Ce que libmspub et MyPublisher en font

**C14 — libmspub lit Publisher 98 par son analyseur « 2k »** (signature 0x22 avec un flux
Quill ; 0x22 sans Quill va à l'analyseur de Publisher 97 ; 0x2C à celui des versions récentes) et
retrouve 106 stories sur 110 (une 107e est vide), soit 87 % des caractères. Pertes : le pied de
page de l'arrière-plan et deux tableaux (1 778 caractères). Il perd aussi les fonds de toutes les
formes et le filet gauche (C7) : `clrchk.jsp.raw`, lignes 14 à 21, montre 394 côtés « none » à
0,0000 in et 1 182 côtés « solid » à 0,0139 in.
Preuve : `MSPUBDocument.cpp:129-139` ; `MSPUBParser2k.cpp:421-426` (type 0x0A, tableau, inconnu),
`:539-542` (pages non normales écartées).
Commande : `$PY $SC/quill_presence.py $S`. Confiance : haute.

**C16 — libmspub copie le texte entier d'une story chaînée dans chacun de ses cadres** : dans
`infoshet.jsp`, les deux cadres reçoivent les mêmes 1 040 caractères. Preuve : `addTextShape`,
`MSPUBParser2k.cpp:710-717` ; banc d'import `$S/etude/06-banc/Essai`. Confiance : haute.

**C17 — `PubImporter` ignore sans avertir les appels qu'il ne connaît pas.** Son aiguillage n'a
ni `drawPolyline` ni cas par défaut (`MyPublisher.Core/Services/PubImporter.cs:194-238`) : sur
Publisher 98, aucune forme ne passe (16 traits dans `cheklist.jsp`, 1 576 dans `clrchk.jsp`, qui
sont les quatre côtés de ses 394 rectangles), sans un avertissement. C'est contraire à la règle
générale de l'import : « l'importation dit ce qu'elle n'a pas su rendre »
(`MyPublisher.Core/Services/DocumentSerializer.cs:108-109`). Les documents ATR n'en souffrent pas :
pour un fichier récent, libmspub émet des polygones et des chemins, que l'import traite.
Commande : `grep -n 'case "' MyPublisher.Core/Services/PubImporter.cs`. Confiance : haute.

## Principes à retenir, et ce qui ne vaut plus en 2026

**Un conteneur, des parties nommées, une par composant.** Chaque composant lit sa partie seul.
L'idée vaut : c'est celle d'OPC et d'ODF, et c'est la forme de l'archive `.mypubz` (B4) : un ZIP,
le membre `mimetype` en premier et non compressé, puis `document.json`, `images/` et la liste des
polices. Le format CFB lui-même (secteurs de 512 octets, sans compression ni somme de contrôle)
est un compromis de 1997.

**Le texte à part de la mise en page.** Des stories, et des cadres qui en désignent une avec leur
rang ; le texte chaîné est écrit une fois, sa répartition se recalcule. C'est l'idée la plus
durable du format, la seule partie qui a survécu à la refonte de 2002. On n'enregistre ni glyphes
ni positions : aucune donnée dérivée dans le format canonique. La répartition recalculée n'est
compatible avec l'égalité écran ↔ PDF que si la coupure entre cadres est décidée par la même
composition et la même largeur de coupure pour les deux rendus ; sinon un mot changerait de cadre
d'un rendu à l'autre.

**Des identifiants stables et un arbre explicite.** Un compteur de document alloue des
identifiants croissants, jamais réutilisés, et chaque objet a un parent ; les renvois passent par
ces identifiants. Indispensable pour les chaînes, les maîtresses, les ancrages. Dans MyPublisher,
l'annulation restaure des instantanés complets (`MyPublisher/ViewModels/MainWindowViewModel.cs:711-737`) :
un compteur rangé dans le document recule avec une annulation, et la création suivante reprend le
même identifiant. Il faut le ranger hors de l'instantané, ou admettre le réemploi pour un objet dont
la création a été annulée, et le tester.

**Une structure qui se décrit elle-même.** Le `Contents` de 1997 repose sur des décalages fixes
par version ; libmspub lit Publisher 98 deux octets trop loin (C7). Quill et les `.poc` employaient
déjà des blocs étiquetés, que Publisher 2002 a généralisés. Le `.mypub` est déjà autodescriptif
(JSON, `$kind`) ; il garde la règle qu'un lecteur saute ce qu'il ne connaît pas.

**La version dans le contenu, la migration consentie.** Une version inconnue est refusée ; un
fichier ancien est mis à niveau à l'enregistrement, sur demande, avec un rappel de la
conséquence. `MyPublisher.Core/Services/FormatMigrations.cs:18-35` fait l'essentiel ; il manque
l'avis avant de réécrire un document migré. Comme `FormatMigrations.cs:34` écrase la version à la
lecture, le document en mémoire ne sait plus d'où il vient : la version d'origine doit être gardée
hors du modèle sérialisé.

**Le document porte ses défauts et ses fonds de page.** Une page des prototypes donne la mise en
forme par défaut de chaque outil ; une page d'arrière-plan porte ce qui se répète et les repères.
MyPublisher porte déjà au niveau du document les marges, la grille et les repères libres
(`MyPublisher.Core/Models/Document.cs:63-100`, `:244-297`) et le fond (`Document.cs:105-123`) ; il
lui manque les objets répétés et les défauts d'outil. En 2026, on les range dans des collections
nommées (maîtresses, défauts), et non dans des pages déguisées.

**Des unités entières pour la saisie.** L'EMU reste l'unité d'OOXML, parce que pouces, points et
millimètres s'y écrivent sans reste. Pour le `.mypub`, la leçon porte sur l'unité de saisie, pas
sur l'écriture : `System.Text.Json` écrit déjà les nombres sous leur forme la plus courte qui se
relit à l'identique (`DocumentSerializer.cs:45-48`), et arrondir à l'écriture créerait un écart. On
garde les points ; l'origine au centre de la page est à abandonner.

**Une couleur est une référence typée.** Palette fixe, RVB ou case du jeu de couleurs.
`scheme:accent1` existe déjà dans MyPublisher (`MyPublisher.Core/Models/Jeux.cs:77-82`) ; pour
PDF/X, il faudra le CMJN et les tons directs nommés ([`08-impression-pdf.md`](08-impression-pdf.md)).
La palette indexée est un compromis d'écran 8 bits.

**Incorporer, nommer, prévisualiser, garder une copie.** Les images sont incorporées, les polices
nommées sans être incorporées, la vignette est un métafichier, la copie de secours a un
enregistrement de retard. En 2026, on conserve les octets d'origine des images (nommées par
empreinte), on liste les polices, et l'on écrit de façon atomique. La vignette est un rendu : dans
une archive, c'est un cache jetable, hors de toute comparaison d'archives.

## État dans MyPublisher

- **Format** : JSON versionné, version 5 (`MyPublisher.Core/Models/Document.cs:55`), migrations en
  point fixe et refus d'une version plus récente (`Document.cs:50-55`,
  `MyPublisher.Core/Services/FormatMigrations.cs:18-35`).
- **Texte** dans le cadre (`MyPublisher.Core/Models/Frame.cs:481`, `:546`, `:643`) ; chaînage
  renvoyé au seuil D (`MyPublisher/Docs/PLAN.md:616`).
- **Identifiants** : aucun ; un bloc porte un nom et sa géométrie (`Frame.cs:117-140`), une page un
  nom et ses blocs (`Document.cs:37-46`).
- **Enregistrement** : écriture directe du fichier (`DocumentSerializer.cs:104-105`) ; migration
  silencieuse (`FormatMigrations.cs:34`).
- **Ressources** : images liées par chemin relatif (A3, `DocumentSerializer.cs:130-199`) ; archive
  B4 à 0 % (`PLAN.md:53`, spécifiée `:1650-1656`).
- **Import** : `DocumentSerializer.Load` aiguille les `.pub` vers l'import
  (`DocumentSerializer.cs:114-117`) ; `PubReader` lance `pub2raw`
  (`MyPublisher.Core/Services/PubReader.cs:28-112`) ; `PubImporter` traduit appel par appel
  (`PubImporter.cs:192-239`). Un chemin de deux points devient un trait simulé par un rectangle
  (`PubImporter.cs:665-690`, simulation `:679-690`), un chemin plus long est averti (`:670-674`).
  `startLayer` et `endLayer` sont ignorés, alors que le modèle a des groupes : 9 groupes dans
  `clrchk.jsp`, 4 dans ATA 21.
- **Génération du `.pub`** : l'import ne la regarde pas.
- **Export `.pub`** : aucun.

## Écart

| Sujet | MyPublisher | Écart |
|---|---|---|
| Stories et chaînage | Texte dans le cadre | Pas de chaînage sans changer le format ; un texte chaîné importé est doublé |
| Identifiants | Aucun | Rien ne peut désigner un bloc ou une page |
| Maîtresses et défauts d'outil | Fond, marges, grille, repères au niveau du document | Pas d'objets répétés ; les défauts ne voyagent pas avec le document |
| Enregistrement | Écriture directe, migration silencieuse | Un fichier peut être tronqué par une coupure ; pas de copie de secours ; un document ancien est réécrit sans avis |
| Transport | Images liées, B4 à faire | Un document envoyé seul est incomplet |
| Import Publisher 98 | Délégué à `pub2raw` | 87 % du texte, aucune forme, sans avertissement ; côté libmspub : fonds, filet gauche, tableaux, arrière-plan, texte chaîné doublé |
| Génération | Inconnue | L'utilisateur n'est pas prévenu qu'un fichier de 1998 ou 2000 ne sera lu qu'en partie |
| Format récent | Lu par libmspub | Non étudié ici : ATA 21 a des flux `Escher/EscherStm`, `Escher/EscherDelayStm`, `Envelope`, `VBA` et `\x05DocumentSummaryInformation`, que ce relevé ne fait que nommer |
| Export `.pub` | Aucun | Aucun écart à combler (F7) |

## Postes proposés

Codes du tableau unique de [`00-index.md`](00-index.md#postes-proposés).

- **F1 — L'import n'ignore plus rien en silence.** Un appel inconnu donne un avertissement qui le
  nomme ; quatre segments consécutifs qui ferment un rectangle redonnent un rectangle (ou le filet
  d'un cadre de texte quand ils l'entourent), plutôt que quatre blocs. Recette : tests purs sur des
  relevés écrits à la main (jamais un relevé du CD dans `MyPublisher.Tests`) : un `drawPolyline` à
  deux points donne un trait aux bonnes coordonnées ; quatre segments fermés donnent un rectangle ;
  un appel inventé donne un avertissement ; un filet fin (code 0x81, épaisseur 0) reçoit une
  épaisseur minimale rendue pareil à l'écran et au PDF. Facultatif, avec le CD : `clrchk.jsp` donne
  394 rectangles. Sabotage : retirer le cas par défaut fait tomber le test.
- **F6 — Dire la génération du `.pub` avant de l'importer**, trois cas : 0x22 sans Quill
  (Publisher 97), 0x22 avec Quill (98 ou 2000), 0x2C (2002 et suivants). Recette : des CFB fabriqués
  par le test déclenchent l'avertissement attendu ; avec le CD, les cinq fichiers avertissent,
  ATA 21 non. Dépend du choix d'un lecteur CFB (bibliothèque sous MPL 2.0, ou lecteur minimal).
- **F8 — Corriger libmspub en amont pour Publisher 98** : décalages du fond et du filet,
  tableaux. Recette : `pub2raw` recompilé rend 394 « draw:fill: solid » pour `clrchk.jsp` et aucun
  premier côté « none » ; les fichiers de test de libmspub ne changent pas. Le correctif s'appuie
  sur la seule lecture de fichiers de données ; un fichier Publisher 2000 témoin doit provenir
  d'une source licite (les fichiers de test de libmspub, par exemple), jamais d'une exécution du
  logiciel.
- **F3 — Identifiants stables** : voir [`05-modele-document.md`](05-modele-document.md).
- **D3 — Les stories sortent du cadre** : dossier D ([`01-moteur-texte.md`](01-moteur-texte.md)) ;
  le poste fixe le modèle de la chaîne et ne se fait donc pas avant la décision. Il dit aussi où
  vivent les champs en ligne (le `TOKN` de Quill) et la feuille de styles (`STSH`).
- **F5 — Enregistrer sans risque, et prévenir avant de migrer.** Recette : une écriture
  interrompue laisse l'original intact octet pour octet ; la copie de secours égale la version
  précédente ; un document de format 4 enregistré produit l'avis, un document au format courant
  non ; la version d'origine est gardée hors du modèle sérialisé.
- **F11 — Préciser B4.** Recette : celle de B4 (ouvrir ailleurs, images comprises), plus : une
  image partagée n'est stockée qu'une fois ; une archive tronquée donne un message et non une
  exception ; deux enregistrements donnent les mêmes membres, octet pour octet, vignette exceptée
  (cache jetable) et horodatages ZIP fixés.
- **F7 — Ne pas écrire de `.pub`.** Le format n'est connu qu'en lecture et en partie. Les sorties
  utiles sont PDF, PNG, SVG, et le texte (voir [`09-imports-web.md`](09-imports-web.md)). Décision
  à inscrire au plan.

## Questions ouvertes

1. Existe-t-il des documents Publisher 98 ou 2000 réels à reprendre ? Sans eux, F6 et F8 passent
   après les autres.
2. Le `u16` en 0x04 de `Contents` suffit-il à séparer 98 de 2000 ?
3. Le format de la chaîne côté `Contents` (+0x17, +0x5C, blocs 0x43 et 0x4C) est l'information
   d'interopérabilité dont l'import des chaînes aura besoin ; libmspub ne la lit pas. Elle se
   cherchera d'abord par l'observation de fichiers.
4. Les rôles de l'entrée 0 et des dernières pages de la liste, du bloc 0x41, de `MCLD`, `SGP` et
   `INK` ne sont pas établis.
5. Pourquoi 28 `.poc` sur 61 ne se lisent-ils pas, et quel lien unit les renvois `OPYID` des `.des`
   aux objets des `.poc` ? Voir [`07-assistants-ressources.md`](07-assistants-ressources.md).
6. Le format des documents réellement à reprendre (ATA 21, Publisher récent) reste à décrire pour
   le point dur n° 5 ; ce relevé ne couvre que celui de 1998.
