# Publisher 98 — ce que l'original enseigne, et ce qu'il demande à MyPublisher

Étude du CD d'origine de **Microsoft Publisher 98** (version 5.0, anglais, décembre 1997),
conduite pour éclairer les parties critiques de MyPublisher : la mise en page du texte, le
modèle de document, le format de fichier, l'export PDF, l'import du `.pub`. Les fiches de
[`../Publisher2007/`](../Publisher2007/00-index.md) disent ce que Publisher *offre* à
l'utilisateur, d'après un guide ; celles-ci disent comment Publisher 98 *est construit*, d'après
ses fichiers, et ce qui en vaut encore en 2026.

Chaque fiche suit le même plan : ce que fait Publisher 98 ; ce qui a été relevé, preuves à l'appui
(fichier et décalage, ressource, export, chaîne, sujet d'aide, et la commande qui le reproduit) ;
les principes à retenir et ce qui ne vaut plus ; l'état de MyPublisher, avec des références
`fichier:ligne` relues au commit `469b861` ; l'écart ; les postes proposés.

## Provenance et méthode

Le CD est monté en lecture seule sur `/run/media/jpeloille/PUB_50_CD`. Rien n'y a été écrit, et
aucun de ses binaires n'a été exécuté. Ont été lus :

- les **métadonnées des exécutables** : en-têtes PE, informations de version, tables d'export et
  d'import, ressources (tables de chaînes du type 217 propre à Publisher, 134 dialogues, menus,
  accélérateurs, polices incorporées), chaînes ASCII et UTF-16 ;
- les **aides**, décompilées en texte sujet par sujet (`mspub.m14`, `qmspub.hlp` et les autres) ;
- les **fichiers de données** : documents OLE (`mspub.pub`, `zmspub.pub`, `cheklist.jsp`,
  `clrchk.jsp`, `infoshet.jsp`), questionnaires et contenus des assistants (`pubwiz/*.des`,
  `*.poc`), BorderArt (`pubbrd/*.bdr`), papiers (`pubspapr/*.ini`), jeux de couleurs
  (`clrscm.reg`), registres (`agent98.reg`, `htmlenc.reg`), catalogue (`pub50.ctg`), PPD, manifestes
  d'installation (`pub5.inf`, `pub5.stf`), polices (tables lues par fontTools) ;
- la **littérature publique** : billets de Murray Sargent sur Line Services, code de WPF (MIT),
  code de libmspub (MPL 2.0), brevets, documentation de Microsoft, spécifications Unicode et
  OpenType, documentation de PDFsharp et de Ghostscript.

Chaque domaine a été relevé par un lecteur, puis contre-vérifié par un second, qui a rejoué les
preuves, corrigé les références et contesté certaines conclusions. Les fiches ne reprennent que les
constats non réfutés ; elles signalent ceux que la vérification a laissés **incertains**, appliquent
ses corrections de références, et intègrent ses objections. Les rapports détaillés et les
extractions restent hors du dépôt, dans le dossier de l'étude.

**Cadre juridique.** Le contrat de licence interdit la rétro-ingénierie « sauf dans la mesure où la
loi applicable le permet ». Le Code de la propriété intellectuelle (art. L122-6-1) permet à
l'utilisateur légitime d'observer, d'étudier et de tester le logiciel pour déterminer les idées et
principes à la base de ses éléments (III), et ne permet de décompiler que pour l'interopérabilité,
quand l'information n'est pas déjà accessible (IV). Cette étude s'en tient au III : elle a lu des
données, des métadonnées, des ressources et des textes d'aide, et n'a **désassemblé aucun code**, pas
même sur le chemin de lecture du `.pub`, où les fichiers de données et le code public de libmspub ont
suffi. Aucune source fuitée n'a été consultée. Le contenu des lexiques de césure et d'orthographe,
qui porte un avis de secret de fabrique, n'a pas été exploré au-delà de leur en-tête. Rien du CD
n'entre dans le dépôt — ni police, ni dictionnaire, ni image, ni modèle, ni texte d'aide — et les
citations sont courtes. Les copies faites pour la lecture hors du CD (la police MSVisi tirée de
`qs98.dll`, les polices tirées des cabinets `valupack`) sont à effacer après l'étude.

## Notations

Les commandes des fiches supposent :

```bash
CD=/run/media/jpeloille/PUB_50_CD
S=/tmp/claude-1000/-home-jpeloille-RiderProjects-uPub/d08b06cd-d5ce-4268-9d5b-41e9f9c11691/scratchpad
X=$S/extrait                      # métadonnées PE, ressources, aides en texte
W=$S/etude/work                   # scripts et essais de l'étude
PY="$S/tools/.venv/bin/python -I" # pefile, olefile, fontTools ; toujours -I
STR=$X/pe/pub5int.dll/chaines-217.txt
DLG=$X/pe/pub5int.dll/dialogues.txt
M14=$X/aide/mspub.m14/sujets.txt
QHLP=$X/aide/qmspub.hlp/sujets.txt
sujet() { awk -v n=$1 '/^SUJET [0-9]+ :/{p=($2==n)} p' $2; }   # sujet <n> $M14
chaine() { grep -P "^($1)\t" $STR; }                           # chaine '109\.15'
dlg() { awk -v d=$1 '$1=="DIALOGUE"{p=($2==d)} p' $DLG; }      # dlg 105
```

`$S` est le dossier de l'étude : il n'est pas versionné et disparaît avec la session. Les
extractions se régénèrent par les scripts de `$S/scripts/` (`pe_extract.py`, `res217.py`,
`resolve_ord.py`) et par `helpdeco` pour les aides ; leur mode d'emploi est dans
`$X/pe/INDEX.md` et `$X/aide/INDEX.md`. Une chaîne de type 217 se désigne par
`table.index` (« 217:109.15 » est l'entrée 15 de la table 109). Les commandes qui appellent
`pub2raw` se lancent depuis la racine du dépôt, où `Scripts/outils-pub.sh` l'a extrait dans
`.outils/mspub`.

Confiance : **haute** = lu tel quel ; **moyenne** = déduit d'indices concordants ; **basse** =
hypothèse plausible, à confirmer.

## Les fiches

| # | Domaine | Point dur | Verdict pour MyPublisher |
|---|---|---|---|
| [01](01-moteur-texte.md) | Architecture du moteur de texte : Quill et Line Services | 1 | `partiel` |
| [02](02-typographie.md) | Réglages typographiques : caractère, paragraphe, cadre | 1 | `partiel` |
| [03](03-cesure.md) | Césure, coupure de ligne et vérification orthographique | 1 | `manquant` |
| [04](04-polices.md) | Polices : inventaire, crénage, ligatures, droits d'incorporation | 1, 4 | `partiel` |
| [05](05-modele-document.md) | Modèle de document et fonctions de l'application | 2 | `partiel` |
| [06](06-format-pub.md) | Le format `.pub` de Publisher 98 | 3, 5 | `partiel` |
| [07](07-assistants-ressources.md) | Assistants, Design Gallery, jeux de couleurs, BorderArt, papiers | 2 | `partiel` |
| [08](08-impression-pdf.md) | Impression professionnelle et sortie : du PostScript au PDF/X | 4 | `manquant` |
| [09](09-imports-web.md) | Imports de texte et d'images, publication Web | 5 | `partiel` |

## Les cinq points durs, vus depuis Publisher 98

### 1. La mise en page du texte

**Ce que Publisher 98 enseigne.** Il ne compose pas son texte lui-même. Trois étages se partagent
le travail : l'application pose les pages et les cadres ; le moteur de textes (Quill) tient les
stories, l'édition et la répartition dans les cadres ; Line Services compose une ligne à la fois,
sans jamais appeler le système, en demandant à son client les runs, les largeurs, les occasions de
coupure, puis en lui demandant de dessiner. Le texte est rangé hors des cadres, en stories ; la
chaîne, le débordement, l'écoulement et les mentions de suite en découlent, et l'aide en donne les
règles une à une. La coupure se décide sur une mesure de référence. La césure est un service externe,
portée par la story, avec une zone ; le crénage par paires vient de la police, au-dessus d'un seuil
de corps.

**Ce qui est dépassé.** La mesure par GDI, sur l'imprimante de la publication, qui fait repaginer
quand l'imprimante change. L'absence de façonnage (pas de ligatures) et de texte bidirectionnel. Le
lexique de césure propriétaire, et la langue confondue avec celle du poste. Le seuil de crénage n'est
pas tout à fait dépassé : l'aide le justifie d'abord par la typographie, et les documents ATR ont
vraisemblablement été composés avec lui ; il devient une propriété du document plutôt qu'un défaut
supprimé.

**Ce que cela implique pour MyPublisher.** MyPublisher compose bloc par bloc, coupe aux espaces — et,
par défaut, au milieu d'un mot à chaque changement de style —, et réconcilie deux mesures par un
maximum, solution que le propriétaire a retenue pour Q13. L'étude ajoute trois écarts entre l'écran
et le PDF que DejaVu Sans cache (hauteur et montée de ligne, ligatures vraisemblables à l'écran,
caractère absent rendu en boîte au PDF). Les défauts se corrigent sans attendre (T1, T2, T3, T4), et
chacun rapproche la mesure du noyau. Le reste — une story au niveau du document, un façonneur unique,
des glyphes posés par les deux rendus, la chaîne — forme le **dossier D** proposé plus bas.

### 2. Le modèle de document

**Ce que Publisher 98 enseigne.** Deux calques par page, premier plan et arrière-plan commun, qui
tient lieu de page maîtresse. Des styles de paragraphe nommés, plats, sous la mise en forme directe.
Un jeu de couleurs appliqué par référence, à huit rôles. Des informations personnelles rangées chez
l'utilisateur, la publication retenant lesquelles. Un folio inséré dans le texte comme une marque. Des
identifiants d'objets jamais réutilisés. Une annulation à vingt niveaux, chaque pas nommé d'après un
catalogue de 150 actions, qui couvre le document entier, réglages et assistants compris. Rien ne dit,
en revanche, que cette annulation repose sur un patron Command : un libellé par pas se concilie aussi
bien avec des instantanés.

**Ce qui est dépassé.** Les bornes (vingt pas, 255 styles, deux arrière-plans), le masquage d'un
objet de l'arrière-plan par un cadre vide posé dessus, la synchronisation des coordonnées par copie,
les styles sans héritage.

**Ce que cela implique pour MyPublisher.** Son annulation, par instantanés du document entier, a déjà
plus de pas ; il lui manque d'être rangée dans le noyau et de nommer ses pas (F4). Le préalable de
presque tout le reste est un **identifiant stable** sur chaque bloc et chaque page (F3), qui demande
une version 6 du format. Viennent ensuite les pages maîtresses (F9), les styles nommés (F10), qui
obligent à rendre facultatives des propriétés de bloc aujourd'hui toujours écrites, et le champ dans
le texte (F14). La story séparée du bloc (D3) appartient au dossier D, parce qu'elle fixe le modèle
de la chaîne.

### 3. Le format de fichier

**Ce que Publisher 98 enseigne.** Un conteneur à parties nommées, une par composant : la mise en
page à l'application, le texte au moteur de textes, les objets incorporés à OLE, la vignette au
système. Le texte à part de la mise en page, et écrit une seule fois même quand il court sur plusieurs
cadres. Des identifiants et un parent pour chaque objet. Des longueurs entières. La version dans le
contenu, un refus net des versions inconnues, une migration demandée avant d'être faite, une copie de
secours. Le stockage du texte a traversé sans rupture la refonte de la mise en page en 2002.

**Ce qui est dépassé.** Le format CFB (secteurs de 512 octets, sans compression ni contrôle), les
structures lues à décalages fixes selon la version (libmspub lit Publisher 98 deux octets trop loin),
les identifiants sur 16 bits, l'origine au centre de la page, la page de prototypes déguisée en page.

**Ce que cela implique pour MyPublisher.** Le `.mypub` est déjà du JSON versionné, autodescriptif,
migré en point fixe : il n'a rien à emprunter à la forme du `.pub`. Il lui manque l'écriture atomique
et l'avis avant de réécrire un document migré (F5), et l'archive `.mypubz` prend la forme d'OPC et
d'ODF : un ZIP, le membre `mimetype` en premier, la vignette comme cache jetable (F11). Rien de dérivé
de la composition n'entre dans le format.

### 4. L'export PDF, le CMJN et le PDF/X

**Ce que Publisher 98 enseigne.** La sortie professionnelle est un état du document, choisi tôt, qui
contraint les couleurs. Publisher 98 ne savait pas faire de quadrichromie ; il savait le ton direct,
au plus deux encres sans nom, avec des teintes en pourcentage d'encre, et fabriquait ses séparations.
Le noir surimprimait, le reste défonçait. Les marques et les fonds perdus vivaient hors de la page.
Une fiche technique se remplissait seule pour l'imprimeur, et un vérificateur contrôlait le document
avant l'envoi.

**Ce qui est dépassé.** Les séparations faites dans l'application, la composition pour l'imprimante
cible, le PostScript et l'EPS comme formats d'échange, le ton direct décrit par du RVB, la fiche
technique qui oublie les images.

**Ce que cela implique pour MyPublisher.** Son export écrit un PDF 1.7 en RVB seul, sans TrimBox ni
BleedBox, et deux exports identiques diffèrent de 137 octets. PDFsharp 6.2.1 sait le CMJN (par une
formule sans profil), la surimpression et les boîtes ; il n'écrit ni profil de sortie PDF/X, ni ton
direct, mais sa couche basse est publique, et LittleCMS fera la conversion. Ghostscript sert d'outil
de recette pour les plaques. Les documents réels sont des A4 bureautiques : un PDF reproductible (S1),
le respect des droits d'incorporation (S2), l'impression (S3) et un contrôle avant envoi (S4) servent
tout de suite ; le format fini, les couleurs d'impression et le PDF/X-4 (S5 à S9) attendent de savoir
si un imprimeur doit recevoir ces documents, et ce qu'il demande.

### 5. L'import et l'export `.pub`

**Ce que Publisher 98 enseigne.** Le format se lit par l'observation des fichiers : l'étude a décrit
la table des blocs, les unités, le codage des couleurs, la chaîne de cadres et l'index du texte sans
désassembler quoi que ce soit. libmspub lit Publisher 98 par son analyseur de Publisher 2000 et en
perd une partie : fonds et filet gauche des formes (décalés de deux octets), tableaux, page
d'arrière-plan ; il copie le texte d'une story chaînée dans chacun de ses cadres. Pour les échanges de
texte, Publisher 98 faisait tout passer par un pivot unique, le RTF.

**Ce qui est dépassé.** Le RTF comme pivot, les convertisseurs découverts par le registre, la
publication Web qui met en image tout ce qui n'est pas du texte pur.

**Ce que cela implique pour MyPublisher.** L'import ignore sans le dire les traits que libmspub émet
pour Publisher 98 : aucune forme ne passe, contre la règle de l'import (F1). L'import doit dire la
génération du fichier (F6), et les pertes de libmspub se corrigent en amont (F8). **Écrire un `.pub`
n'est pas souhaitable** (F7) : le format n'est connu qu'en partie et en lecture. Les sorties utiles
sont le PDF, le PNG et le SVG tirés du PDF (E5), et le texte (E7). Pour l'entrée, MyPublisher a déjà
un pivot sans le nommer — la traduction du vocabulaire OpenDocument que fait `PubImporter` — ; il faut
l'extraire et lui apprendre les unités d'OpenDocument (E1), puis importer l'ODT (E2) et le DOCX (E3),
dont la recette est de rendre ce que rend l'ODT du même document. Le format des documents réellement
à reprendre, le `.pub` récent d'ATA 21, n'a pas été étudié ici au-delà de ses flux.

## Postes proposés

Un tableau unique, dédoublonné entre les fiches. Les codes sont proposés, préfixés par domaine, sans
recouvrir ceux du plan : **T** texte et polices, **F** format et modèle de document, **S** sortie : impression
et export PDF, **E** échanges de contenu, **G** gabarits et ressources de conception, **D** dossier de
décision du moteur de composition. La colonne *Vague* donne l'ordre des dépendances : un poste ne
dépend que de postes des vagues précédentes, de postes livrés du plan, ou de postes ouverts du plan
qu'il nomme (A8, A10, B4, B6, C7, R6, R7) ; ces derniers ne sont pas classés ici, et le poste attend
leur livraison. Les recettes sont résumées ; leur forme complète, avec les sabotages, est dans la
fiche citée.

| Vague | Poste | Intitulé | Dépend de | Recette | Fiche |
|:---:|---|---|---|---|---|
| 1 | **T1** | Le saut de ligne forcé | — | U+2028 coupe sans fermer le paragraphe ; dans le premier bloc d'ATA 21 (huit paragraphes), « chamber. » s'aligne sur les lignes de suite et « This butterfly valve… » retrouve sa marge de 0,193 in ; format 6 | 02 |
| 1 | **F1** | L'import `.pub` n'ignore plus rien en silence | — ; le trait reste simulé tant qu'A10 n'est pas livré | Tests purs sur relevés écrits à la main : trait à deux points, rectangle reconstitué de quatre segments, appel inventé averti par son nom ; avec le CD, `clrchk.jsp` donne 394 rectangles | 06 |
| 1 | **T2** | Couper selon UAX #14, sur le paragraphe entier | — | Sous-ensemble latin de `LineBreakTest.txt` ; un mot à cheval sur deux fragments ne se coupe jamais ; « : » jamais en tête de ligne ; « turbo-fan » coupé après le trait ; `comparer-mots.sh` à zéro écart sur les documents de référence | 03 |
| 1 | **T3** | La hauteur et la montée d'une ligne viennent d'une seule mesure | — | Test pur sur valeurs `hhea`/`OS/2` écrites en dur, `sTypo*` nuls compris ; DejaVu Sans réécrite à `usWinAscent` + 20 % : lignes de base écran et PDF à 0,05 pt | 04 |
| 1 | **T4** | Les caractères absents de la police | — | Repli commun aux deux rendus (U+202F, U+2009, U+2011, € et le reste) ; même largeur à l'écran et au PDF ; `ActualText` dans le flux | 04 |
| 1 | **T5** | Essai : l'écran forme-t-il des ligatures que le PDF n'a pas ? | — | « fi » en DejaVu Sans : nombre de glyphes relevé des deux côtés et consigné ; versé au dossier D | 01, 04 |
| 1 | **T7** | Dire si le substitut garde les coupures | B5 | Table de paires métriquement compatibles ; Arial rendu en Liberation Sans annoncé « chasses identiques » ; table vidée, la recette tombe | 04 |
| 1 | **F2** | La langue du texte, et une langue de document | — | ATA 21 : 73 fragments fr-FR et l'avertissement « une seule langue déclarée » ; Fiches 298 en-US et 31 fr-FR ; aller-retour à l'identique ; la langue d'un document neuf ne vient jamais de la machine | 02, 03 |
| 1 | **F3** | Un identifiant stable par bloc et par page | — | Format 6 ; identifiants déterministes à la migration ; copie à identifiant neuf ; pas de réemploi ; la suite « créer, annuler, créer » est testée selon la règle retenue pour le compteur | 05, 06 |
| 1 | **F4** | L'historique dans le noyau, un libellé par pas | — | 61 transactions laissent 60 pas ; transaction vide sans pas ; transactions imbriquées en un pas ; chaque commande des menus a son libellé | 05 |
| 1 | **F5** | Enregistrer sans risque, prévenir avant de migrer | — | Écriture interrompue : original intact ; copie de secours égale à la version précédente ; avis pour un document de format 4, pas pour un document courant | 06 |
| 1 | **F6** | Dire la génération du `.pub` | Choix d'un lecteur CFB | Trois cas (0x22 sans Quill, 0x22 avec Quill, 0x2C) sur des CFB fabriqués ; les cinq fichiers du CD avertissent, ATA 21 non | 06 |
| 1 | **F7** | Ne pas écrire de `.pub` | Décision | Inscrite au plan, parmi « ce que ce plan ne fait pas » | 06 |
| 1 | **F14** | Champs dans le texte | — ; même montée de format que F3 et F10 si elles coïncident | « Page # sur ## » rend « Page 3 sur 8 » ; ancien bloc à champ de folio au même PDF | 05 |
| 1 | **F15** | Ombre d'objet | — | Le flux PDF porte le contour deux fois au décalage fixé ; un groupe ombré n'ombre pas chaque enfant | 05 |
| 1 | **S1** | Un PDF identique octet pour octet | — | Deux exports à la même `--date` identiques par `cmp` ; une autre date diffère ; `/ID` au hasard ou date à l'horloge font tomber la recette | 08 |
| 1 | **S2** | Respecter les droits d'incorporation | B5 | Test pur sur neuf valeurs de `fsType` ; une police libre réécrite à 2 n'est pas incorporée, et le rapport le dit | 04 |
| 1 | **S3** | Imprimer par CUPS (complète B3) | — | Sur une file qui écrit dans un fichier, Ctrl+P rend les mêmes pages que l'export ; la plage « 2-3 » en donne deux | 08 |
| 1 | **E1** | Le récit importé, et les unités d'OpenDocument | — | Les `.pub` ATR importés donnent un `.mypub` identique octet pour octet ; « 12pt », « 1.27cm », « 150% », « 700 » sont lus | 09 |
| 1 | **E5** | Une page en PNG et en SVG, tirée du PDF | — | ATA 21 à 150 ppp : 1240 × 1754 px ; `viewBox` de 595,3 × 841,9 ; comparaison au PDF source, non à l'écran | 09 |
| 1 | **E6** | Le PDF comme image vectorielle | — | Dans le PDF exporté, le trait posé reste un chemin et le mot se relit par `pdftotext` | 09 |
| 1 | **E7** | Exporter le texte | — | Exporté puis réimporté par B2, le texte d'un bloc revient au signe près | 09 |
| 1 | **G1** | Le document retient son jeu de coordonnées (révision de C4) | Décision du propriétaire | Une carte composée avec B rouverte avec A actif porte B à l'écran, au PDF et en ligne de commande ; B absent : refus ou choix explicite | 05, 07 |
| 1 | **G3** | Jeu de couleurs propre au document, rapprochement des couleurs libres | C5 | Accent 2 retouché : références suivies, couleurs libres immobiles, jeu livré intact ; « Rapprocher » à seuil nul sans un pixel de différence | 07 |
| 1 | **G4** | Texte d'attente calibré | B1, A2 | N mots annoncés tiennent, N + 10 % débordent ; N baisse avec le corps ; même N à l'écran et à l'export | 07 |
| 1 | **G8** | Papier pré-imprimé | C5 | Visible à l'écran ; `pdfimages -list` vide et PDF identique au document sans papier | 07 |
| 2 | **T8** | Les caractères de coupure de l'auteur | T2 | U+00AD visible seulement en fin de ligne ; « 20 °C » en U+00A0 insécable ; chaque morceau porte son décalage dans le texte source | 03 |
| 2 | **T9** | L'interligne par paragraphe, relatif ou exact | T1 | 18,00 pt ± 0,05 en exact avec un mot de 24 pt ; `clrchk.jsp` à 5,25 sp, étiquettes en haut de leur cadre ; placement tranché par mode | 02 |
| 2 | **T10** | Petites capitales, capitales, lecture des effets | R7 | Petites capitales fabriquées à 70 % des capitales, à 0,1 pt ; effets lus à l'import, relief averti | 02 |
| 2 | **T11** | Taquets posés | R6 | Virgules décimales alignées à 0,05 pt ; points de suite réguliers ; origine des taquets tranchée | 02 |
| 2 | **T12** | Banc local des polices du CD, hors dépôt | T3 pour passer | Recettes en Arial, Book Antiqua, Stencil, Calisto MT Bold, Wingdings 3 : échec avant T3, succès après ; rien de versionné | 04 |
| 2 | **F8** | Corriger libmspub en amont | Fichier Publisher 2000 témoin d'origine licite | `clrchk.jsp` : 394 fonds pleins et aucun premier côté « none » ; tests de libmspub inchangés | 06 |
| 2 | **F9** | Pages maîtresses | F3 | Folio sur la maîtresse : 1 à 8 au PDF, un seul bloc dans le JSON ; page qui ignore la maîtresse sans folio ; folio à deux chiffres identique à l'écran et au PDF | 05 |
| 2 | **F10** | Styles de paragraphe nommés | F4 | Migration sans un pixel de différence ; « Titre » 18 → 20 pt sauf le 14 pt direct ; Normal indélébile ; cycle « basé sur » refusé | 05 |
| 2 | **F11** | Préciser B4 : l'archive `.mypubz` | B4 | Image partagée stockée une fois ; archive tronquée : message ; membres identiques d'un enregistrement à l'autre, vignette exceptée | 06 |
| 2 | **S4** | Vérificateur de conception et rapport de contrôle | F3 | Un constat par règle, désigné par identifiant ; résolution effective d'une image à 1 ppp ; document propre sans constat ; corrections annulables | 05, 08 |
| 2 | **S5** | Format fini, fonds perdus, traits de coupe | S1 | TrimBox au format, BleedBox à +3 mm ; aplat visible jusqu'à la BleedBox, à l'écran comme au PDF ; traits sur chaque plaque | 08 |
| 2 | **S6** | Couleurs d'impression dans le modèle | S1 | Aller-retour CMJN et ton direct ; teinte à 60 % d'un ton écrite `0.6 scn` sur son `Separation` | 08 |
| 2 | **E2** | Importer un ODT | E1 | Fixture `.odt` écrite à la main : texte, fragments, écarts, tableau, avertissements attendus ; annulation à l'identique | 09 |
| 2 | **E4** | Coller et copier du texte mis en forme | E1 | Fragments HTML fixés, dont un de LibreOffice, rendus en fragments et écarts ; script et image réduits au texte, avec avertissement | 09 |
| 2 | **E8** | Works et WordPerfect, à la demande | E1 | Un `.wps` de Works 4 rendu par E1, avertissements comptés | 09 |
| 2 | **G2** | Le jeu de coordonnées porte un jeu de couleurs | G1 | Carte de visite neuve aux couleurs d'« Océan » sur ses 14 références ; document ancien sans un pixel de différence | 07 |
| 2 | **G5** | Modèle paramétré | F3, F4, C3 | « Logo : non » retire le logo en un pas ; « oui » le rend à sa place, texte saisi gardé ; réponse relue après enregistrement | 07 |
| 3 | **T13** | Césure automatique | T2, T8, F2 | Sur l'anglais d'ATA 21 : coupures conformes aux motifs anglais, jamais « downs-tream », deux coupures consécutives au plus, blanc moyen en baisse ; inactive à l'import | 03 |
| 3 | **T14** | Espaces typographiques françaises à la saisie | T8, F2, Q1 | Insécable posée dans un fragment français vérifié ; rien dans ATA 21 importé, ni à l'import ni à la frappe | 03 |
| 3 | **F12** | Pages en vis-à-vis, par une maîtresse double | F9 | Marges intérieure et extérieure en miroir ; folio extérieur à gauche des paires, à droite des impaires | 05 |
| 3 | **F13** | Repères de règle par page | F9 | Repère de la page 2 sans effet en page 3 ; repère de la maîtresse partout ; recette C2 inchangée | 05 |
| 3 | **E3** | Importer un DOCX | E2 | Le DOCX de la fixture d'E2 donne un bloc identique à celui de l'ODT ; un DOCX réel s'importe sans exception | 09 |
| 3 | **S7** | PDF/X-4 en CMJN par profil | S5, S6 | `OutputIntents` `GTS_PDFX`, aucun `rg` ; aplat à 1 % de `transicc` ; texte noir sur la seule plaque K ; ton direct sur sa plaque nommée | 08 |
| 3 | **G6** | Bibliothèque de blocs fidèle au jeu | C7 | Bloc rangé depuis Marine, posé dans Océan : prend l'accent et la police du second ; couleur libre inchangée | 07 |
| 3 | **G7** | Bordure à motif, à rediscuter | Révision de l'exclusion du plan | Seulement si un document réel le demande ; motifs propres au projet, deux modes de remplissage éprouvés | 07 |
| 4 | **T15** | Le PDF se lit malgré la césure | T13 | `ActualText` contrôlé dans le flux de contenu pour chaque mot coupé ; sabotage du marquage attrapé | 03 |
| 4 | **S8** | Surimpression du noir | S7 | Plaque cyan intacte sous un texte noir de 10 pt, trouée sous un titre de 36 pt avec un seuil de 12 pt | 08 |
| 4 | **S9** | Épreuve des plaques (complète B6) | B6, S7 | Vue par plaque égale au rendu Ghostscript de la recette d'S8 | 08 |
| D | **D1** | Un façonneur unique (HarfBuzz) dans le noyau | T5 ; décision D | Avances identiques au bit près sur trois processus et trois ordres de chargement, même fichier et même version ; « AV » et « To » crénés ; « fi » en une grappe avec `liga` ; noyau sans Avalonia ni Skia | 01 |
| D | **D2** | L'écran et le PDF dessinent les glyphes du noyau ; crénage et ligatures partagés, seuil de crénage du document | D1 | `LargeurDeCoupure` retirée ; même mot à moins de 0,01 pt sur l'exemple, ATA 21, les fiches ATR ; abscisses fractionnaires gardées à l'écran ; voie PDF choisie par essai, mots extraits intacts | 01, 02, 04 |
| D | **D3** | La story, séparée du bloc | F3 ; décision D | Migration une story par bloc, cases de tableau tranchées d'abord ; recettes du plan inchangées ; deux cadres d'un même texte importé donnent une seule story | 01, 05, 06 |
| D | **D4** | Le flux d'une story dans une chaîne de blocs | D2, D3 | Deux blocs chaînés sur deux pages : coupures identiques à l'écran et au PDF ; déconnexion et suppression selon l'aide ; refus des liaisons interdites ; chaque opération annulable à l'identique | 01 |
| D | **D5** | Les mentions de suite calculées | D4, F14 | « (suite page 3) » se met à jour quand le bloc suivant passe en page 4, à l'écran et au PDF | 01 |
| D | **D6** | L'écoulement automatique à l'import | D4, A8 | Un texte de vingt pages crée pages et blocs en un pas ; refusé, il reste en débordement signalé | 01 |
| D | **D7** | La justification par priorités | D1 | Aucune espace au-delà de sa borne tant qu'une approche bornée suffit ; ATA 21 ne change pas de coupure | 01 |
| D | **D8** | Le bidi réservé dans l'architecture | D1, D2 | Cas retenus de `BidiCharacterTest.txt` : coupure en ordre logique, dessin dans l'ordre visuel, mêmes glyphes des deux côtés | 01 |
| D | **T16** | Approche et échelle horizontale | D2 | Tc et Tz relevés dans le flux ; un mot à 80 % mesure 80 % de sa chasse à 0,05 pt ; écran et PDF concordent | 02 |

Soit 63 lignes, dont neuf forment le dossier D. Trois sont des décisions plutôt que des développements
(F7, G1, G7). Quatre ne sont pas des postes nouveaux mais des précisions de postes ouverts du plan,
à verser dans leur recette quand ils seront repris plutôt qu'à ouvrir à part : S3 pour B3, F11 pour
B4, S9 pour B6, G6 pour C7 ; il reste donc 59 postes nouveaux. Trois touchent des postes livrés sans
les doubler : G1 révise un choix écrit de C4, F13 déplace les repères de C2 sans changer sa recette,
et S1 étend au fichier le « reproductible » de P3, qui ne portait que sur le contenu
(`PLAN.md:459-461`). Le souligné, qui portait ici le code T6, est versé au poste I7 du plan,
spécifié le 24 octobre 2026 ; ce qu'il ajoute à sa recette est dans la fiche 02, avec les
compléments à R6 et R7.

### Rouvrir le poste D

**Ce que l'étude apporte à la question.** Le plan pose que l'ajustement, le chaînage et l'habillage
sont trois expressions d'un même chantier, à décider ensemble (`MyPublisher/Docs/PLAN.md:4263-4264`),
et que la décision se rouvre quand les phases A, B et C sont livrées (`PLAN.md:96`). Publisher 98
montre comment ce chantier était découpé : une story indépendante des cadres, un compositeur de ligne
qui demande tout à son client, une seule mesure de référence. L'étude montre aussi ce que la double
mesure de MyPublisher cache encore : la hauteur et la montée de ligne, qui ne viennent pas des mêmes
champs des deux côtés ; les ligatures, que l'écran forme vraisemblablement ; le caractère absent, qui
sort en boîte au PDF ; et la coupure au milieu des mots. La césure (T13) et la coupure UAX #14 (T2)
demandent elles-mêmes de mesurer des runs entiers, à travers les fragments. Chaque correction faite
aujourd'hui doit l'être deux fois, une par bibliothèque, puis réconciliée. HarfBuzzSharp est déjà dans
l'application, et `PolicesDuDocument` charge déjà dans Avalonia le fichier que fontconfig désigne pour
l'export (`MyPublisher/Views/PolicesDuDocument.cs:36`).

**Ce qui retient.** Le propriétaire a écarté, dans Q13, la voie qui consistait à franchir le seuil D
pour la seule coupure (`PLAN.md:3599-3606`) : D2 défait une décision livrée et ne peut être qu'un choix
du propriétaire. La condition de réouverture n'est pas remplie (A : 7 sur 15, B : 3 sur 7, C : 7 sur 9).
Le coût de la recomposition (170 à 470 ms pour un panneau, Q23) rend un cache par paragraphe
nécessaire avant toute chaîne de plusieurs pages. Et l'optimisation du paragraphe pendant la frappe
touche un brevet que Google Patents donne en vigueur jusqu'au 29 septembre 2027 (US 7 596 752), à faire
vérifier avant tout poste qui s'en approcherait.

**Proposition.** Rouvrir la **décision** maintenant, sans toucher au code livré, sous la forme d'une
note de décision précédée de trois essais courts, chacun consigné avec sa mesure :

1. T5 : l'écran forme-t-il des ligatures que le PDF n'a pas ? Si oui, l'égalité écran ↔ PDF est déjà
   rompue sur la police par défaut, et la voie de Q13 a une limite de plus.
2. Un `GlyphRun` construit à partir d'indices et d'avances fournis garde-t-il ses abscisses
   fractionnaires à toutes les échelles d'affichage ? C'est la condition de D2.
3. Quelle voie PDF : une grappe par `DrawString`, sans ligatures, ou des glyphes par indice écrits par
   la couche basse de PDFsharp ? Le critère est double : les positions du noyau sont respectées, et
   `pdftotext -bbox` reconstitue encore les mots, faute de quoi `comparer-mots.sh` ne servirait plus de
   recette. `SKDocument` est écarté d'avance, faute de CMJN et de PDF/X.

À cela s'ajoute une mesure : le temps de façonnage d'ATA 21 par HarfBuzz dans le noyau, à comparer aux
170 à 470 ms de Q23. La note réunit ensuite les trois expressions du seuil — le chaînage (D3, D4), et
l'ajustement et l'habillage, qui se posent sur la même interface (recherche du corps sur le formatage de
ligne ; largeur de ligne rendue par le bloc selon l'ordonnée) — et la décision revient au propriétaire.

**Pendant ce temps.** Les postes de la vague 1 ne dépendent pas de la décision et vont dans son sens :
T2, T3 et T4 rangent dans le noyau des règles (coupure, mesure verticale, repli des caractères) que
l'écran et le PDF liraient ensemble ; F3 pose l'identifiant que toute chaîne demandera. Si la décision
est de garder la double mesure, ils restent justes ; si elle est d'ouvrir D, ils en sont les premières
pierres.

## À part

- **Fidélité ou préférence de 2026.** Plusieurs principes de 1997 paraissent dépassés (crénage
  au-dessus de 14 pt, ligatures absentes, interligne asymétrique, justification sans compression).
  Quand un document réel a été composé ainsi, la fidélité l'emporte : le réglage devient une propriété
  du document, avec la valeur de Publisher à l'import, et la préférence de 2026 s'applique aux documents
  créés dans MyPublisher.
- **Les étiquettes de langue des documents réels sont fausses** : ATA 21, en anglais, est étiqueté
  fr-FR, comme les 31 titres « français » des Fiches. C'est un fait à verser à
  [`../Documents-reels.md`](../Documents-reels.md), dont dépendent F2, T13 et T14.
- **Attributions à Publisher dans le code.** Plusieurs commentaires disent « comme dans Publisher »
  pour des choix qui ne sont pas ceux de Publisher 98 (repères libres, retouche d'un enfant de groupe,
  fond de page). Le dépôt prend Publisher 2007 pour référence : ces attributions sont à vérifier sur
  cette version avant d'être corrigées (fiche 05).
- **Non étudié** : le format du `.pub` récent (flux de dessin Office, enveloppe, macros), qui est celui
  des documents à reprendre ; le contenu des `.poc` au-delà de leur répertoire et d'une grammaire
  partielle ; l'orthographe. Le CD porte aussi des domaines qu'aucune fiche n'a ouverts : WordArt
  (`shared/wordart`, aides `wrdart32.hlp` et `qwrdrt32.hlp`), la galerie d'images
  (`shared/artgalry`, `clipart`), Microsoft Draw 98 (`msdraw8.exe` et son aide), les textures de
  fond Web (`bgrounds`, 210 GIF, groupe 316 de `pub5.stf`), le second catalogue `pub50sp.ctg`
  (même signature « BYROND » que `pub50.ctg`, conceptions pour papiers spéciaux), l'acquisition
  TWAIN, et le publipostage au-delà de ses sources (fiche 09, C12). Le plan écarte WordArt et la
  bibliothèque d'images en ligne (`PLAN.md:4299-4300`) ; les autres restent à relever si un
  document réel les emploie.
