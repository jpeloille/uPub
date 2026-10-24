# 02 — Réglages typographiques : caractère, paragraphe, cadre

**Domaine** : point dur n° 1, côté réglages : ce que l'utilisateur de Publisher 98 règle sur
le caractère, le paragraphe, la story et le cadre, avec quelles bornes et quels défauts.
**Sources** : 25 dialogues de `pub5int.dll` (Font 506, Character Spacing 537, Line Spacing
573, Indents and Lists 528, Tabs 538, Frame Columns and Margins 539-541, Drop Cap 578-579,
Text Style 551-556, Hyphenation 105, 543 et 568, Language 740, Line/Border 524-526, Custom
Rotate 576, Font Schemes 518) ; tables de chaînes de type 217 pour les listes ; bulles de
`qmspub.hlp` et sujets de `mspub.m14` pour les bornes et les défauts ; `pub2raw` et le code de
libmspub pour ce que le format garde, passés sur les documents du CD et sur les deux documents
ATR.
**Verdict** : `partiel` — fragments, écarts de paragraphe, colonnes, alignement vertical et
taquets par défaut sont faits ; le saut de ligne forcé est mal importé, la langue est perdue,
et il manque souligné, césure, interligne exact, approche et échelle.

Notations : voir [`00-index.md`](00-index.md#notations). Fiches voisines :
[`01-moteur-texte.md`](01-moteur-texte.md), [`03-cesure.md`](03-cesure.md) ; côté Publisher
2007, [`../Publisher2007/11-format-zone-texte.md`](../Publisher2007/11-format-zone-texte.md) et
[`../Publisher2007/12-ajustement-texte.md`](../Publisher2007/12-ajustement-texte.md).

## Ce que fait Publisher 98

Les réglages se rangent à cinq niveaux, que les dialogues rendent visibles :

| Niveau | Ce qui s'y règle | Dialogues |
|---|---|---|
| Caractère | police, style, corps, souligné, couleur, effets, échelle, approche, crénage, langue | 506, 537, 740 |
| Paragraphe | fer, retraits, liste, interligne, espace avant et après, taquets, lettrine, style | 528, 573, 538, 578-579, 551-556 |
| Story (chaîne de cadres) | césure automatique et zone | 105, 543 |
| Cadre | marges intérieures, colonnes, habillage, mentions de suite, ajustement, alignement vertical, rotation | 539-541, 576, menus *Copyfit* et *Align Text Vertically* |
| Publication et application | taquets par défaut, césure des cadres neufs | 538, 568 |

Les lignes sont composées par Line Services 2.50 ([`01-moteur-texte.md`](01-moteur-texte.md)) ;
l'aide décrit les réglages pour l'utilisateur et dit peu des algorithmes.

## Ce qui a été relevé

Pour reproduire : `dlg <n>` affiche un dialogue, `chaine '<clé>'` une chaîne de type 217,
`sujet <n> $M14` un sujet du livre, `sujet <n> $QHLP` une bulle de dialogue (voir les
[notations](00-index.md#notations)). Le décalage d'un dialogue dans `pub5int.dll` se donne
par `$PY $S/scripts/dlg_off.py $CD/pub5int.dll <n>`.

### Caractère

**C01 — Espacement des caractères, quatre groupes.** Le dialogue 537 (fichier+0xC93B4) réunit
*Scaling*, *Tracking*, *Kerning* et le crénage automatique par paires (« Kern text at: … and
above »). Commande : `dlg 537`. Confiance : haute.

**C02 — L'échelle horizontale va de 0,1 à 600 %** ; 200 % double la chasse. Preuve :
`qmspub.hlp` 407 (ID 1525) ; sujet 1169. Commande : `sujet 407 $QHLP`. Confiance : haute.

**C03 — L'approche a cinq préréglages** (Normal, Very Tight, Tight, Loose, Very Loose) et une
valeur libre de 0,1 à 600 %. Les pourcentages des préréglages ne figurent dans aucune
ressource. Preuve : table 217:63 (+0x163F0) ; bulles 408-409 ; sujet 1158.
Commande : `grep -P '^63\.\d+\t' $STR`. Confiance : haute.

**C04 — Le crénage manuel** offre Normal, Expand et Condense, en points, jusqu'à 600 pt, sur
la sélection ou sur le mot courant. La borne basse diffère selon l'aide : 0,1 (sujet 1160) ou
0 (bulle 411). Preuve : table 217:62 (+0x163B4). Commande : `sujet 410 $QHLP ; sujet 411 $QHLP`.
Confiance : haute.

**C05 — Le crénage automatique a un seuil de corps**, de 0,5 à 999,5 pt, 14 pt par défaut.
L'aide en donne deux raisons : un texte de moins de 14 pt « usually doesn't need to be
kerned », et abaisser le seuil ralentit Publisher. Preuve : bulle 412 (ID 1530) ; sujets 1156,
1170 à 1172. Commande : `sujet 412 $QHLP ; sujet 1172 $M14`. Confiance : haute.

**C12 — Dix-sept sortes de souligné, pas de barré.** Le souligné se choisit dans une liste de
dix-sept sortes. Le dialogue Font n'offre pas de barré : les contrôles *Strikeout* et *Script*
du gabarit commun de Windows sont placés hors de la vue. Preuve : table 217:66 (+0x16BB4) ;
dialogue 506 (+0xC2804), contrôles 1040 et 1041 à x = 5000, 1094 et 1140 à (1000, 967).
Commande : `dlg 506`. Confiance : moyenne.

**C13 — Les effets sont des cases sans paramètre** : ombre, contour, relief, gravure, petites
capitales, capitales. Exposant et indice ne sont stockés que comme un type ; les valeurs 50 %
et 67 % que l'on voit dans `pub2raw` viennent de libmspub, pas de Publisher. Preuve : dialogue
506, contrôles 6015 à 6026 ; libmspub `MSPUBCollector.cpp:1647-1658` et `1711-1717`.
Confiance : haute.

**C22 — libmspub ne lit ni l'approche ni le crénage manuel**, seulement l'échelle
(`SCALING_ID`, en dixièmes de pour cent). Leur absence dans les relevés ATR ne prouve donc rien.
Preuve : `MSPUBBlockID.h` (aucun identifiant d'approche ni de crénage). Confiance : haute.

### Paragraphe

**C06 — L'interligne a deux sens.** En « sp », il accueille le plus grand corps de la ligne,
plus l'espace demandé ; 1 sp par défaut. En in, cm, pt ou pi, il est fixe et l'espace s'ajoute
au-dessus de la ligne. Il est stocké par paragraphe, avec un drapeau de mode. L'export HTML écrit
les deux formes (`mspub.exe`, UTF-16, +0x1FFFEC et +0x200018). Preuve : sujet 2190 ; bulle 193
(ID 630). Commande : `sujet 2190 $M14 ; strings -t x -el -n 5 $CD/mspub.exe | grep line-height`.
Confiance : haute. La lecture du mode dans libmspub passe, pour les fichiers de 1998, par
`MSPUBParser2k` et non par `MSPUBParser.cpp`, l'analyseur des versions 2003 et suivantes.

**C07 — Un document du CD porte un interligne non standard** : `clrchk.jsp` a sept paragraphes
à 525 %, soit 5,25 sp. Commande (depuis la racine du dépôt) :
`LD_LIBRARY_PATH=.outils/mspub/lib .outils/mspub/bin/pub2raw $CD/clrchk.jsp | grep -c 'fo:line-height'`.
Confiance : haute.

**C08 — Retraits et listes.** Le dialogue 528 (+0xC6A5C) a trois modes : normal, puces, liste
numérotée ; six retraits préréglés (Original, Flush Left, 1st Line Indent, Hanging Indent,
Quotation, Custom) ; huit séparateurs de numéro ; six fers (gauche, centre, droite, justifié, et
deux modes extrême-orientaux, *Newspaper* et *Distributed*). Preuve : tables 217:51, 217:10,
217:52 ; bulles 177 à 190. Commande : `grep -P '^(51|52|10)\.\d+\t' $STR`. Confiance : haute.

**C09 — Le retrait suspendu des documents de 1997 vaut un quart de pouce** (marge gauche
0,25 in, première ligne −0,25 in) dans `infoshet.jsp` (24 paragraphes) et `cheklist.jsp` (13).
Confiance : moyenne (deux documents seulement).

**C10 — Taquets.** Position mesurée depuis le bord gauche **du cadre** ; alignements gauche,
centre, droite, décimal ; points de suite aucun, points, tirets, trait. Taquets par défaut
alignés à gauche tous les 0,5 pouce, réglables par publication. Preuve : dialogue 538
(+0xC9A30) ; bulles 10 à 22 ; sujet 2386. Commande : `sujet 10 $QHLP`. Confiance : haute.

**C11 — libmspub lit les taquets posés mais ne les émet pas** : un `.pub` lu par `pub2raw`
perd ses taquets. Preuve : `MSPUBBlockID.h:88` ; aucune occurrence de `tab-stop` dans
`MSPUBCollector.cpp`. Confiance : haute.

**C14 — Le style.** Le style Normal compose en Times New Roman 10 pt au fer à gauche. Un style
réunit les cinq dialogues (caractère, retraits et listes, interligne, espacement, taquets) et
s'applique par paragraphe ; nom de 31 caractères au plus ; les styles vivent dans la
publication. Preuve : sujets 2380, 2382 ; dialogues 552-553 ; bulle 114 (ID 380).
Confiance : haute.

**C16 — Les caractères de mise en forme.** Trait conditionnel Ctrl+-, trait insécable
Ctrl+Maj+-, espace insécable Ctrl+Maj+Espace, saut de ligne forcé Maj+Entrée, distinct de la
fin de paragraphe. Le même sujet donne aussi le saut de colonne (Ctrl+Maj+Entrée), le saut de
cadre (Ctrl+Entrée) et une espace insécable de chasse nulle (Ctrl+Maj+0). Preuve : sujet 928.
Commande : `sujet 928 $M14`. Confiance : haute.

**C17 — La lettrine** peut être tombante, montante, ou montée d'un nombre de lignes ; hauteur
en lignes ; 15 lettres et espaces au plus ; police, style et couleur propres. Preuve :
dialogues 578 (+0xCECD0) et 579 (+0xCEE2C) ; bulles 72 à 82. Confiance : haute.

Veuves, orphelines et lignes solidaires n'apparaissent nulle part dans l'aide
(`grep -c -i 'widow\|orphan' $M14` rend 0) : Publisher 98 ne les offre vraisemblablement pas.

### Story et cadre

**C15 — La césure se règle par story** : case automatique, zone de césure, césure manuelle mot
par mot ; zone mesurée depuis la marge droite, ou gauche pour un texte au fer à droite. Le
registre garde `HotZone` et `AutoHyphenate`. Détail dans [`03-cesure.md`](03-cesure.md).
Commande : `strings -t x -el -n 5 $CD/mspub.exe | grep -E ' (HotZone|AutoHyphenate)$'`.
Confiance : haute.

**C18 — L'ajustement (Copyfit)** : aucun, *Best Fit* (réduit ou agrandit) ou *Shrink Text On
Overflow*. Interdit aux cadres chaînés, aux cadres à colonnes et aux cases de tableau ; un
changement manuel de corps le désactive. Preuve : chaînes 217:99.64, 99.71 à 99.73 ; sujets
2099, 2150, 2151, 2153. Confiance : haute.

**C19 — L'alignement vertical** (haut, milieu, bas) porte sur le cadre entier et cesse d'agir
quand du texte déborde. Il fonctionne, lui, dans les cadres chaînés et à colonnes (sujet 2109) :
l'interdit de C18 ne vaut pas pour toutes les propriétés du cadre. Preuve : sujets 2109, 2154.
Confiance : haute.

**C20 — La rotation est celle du cadre** : de −360 à 360°, aimantée par 15° au glisser. Le
texte vertical n'existe que dans les éditions extrême-orientales. Preuve : dialogue 576
(+0xCEA4C) ; chaîne 217:103.2 ; sujet 2295. Confiance : moyenne.

**C21 — La justification de Line Services sait élargir et resserrer.** *Constat noté
incertain.* Les exports `LsSetExpansion`, `LsSetCompression`, `LsSqueezeSubline`,
`LsdnDistribute` existent ; mais les trois fonctions que Quill importe (`LsSetCompression`,
`LsSetExpansion`, `LsSetModWidthPairs`) règlent des tables de classes de caractères, le
mécanisme de la justification extrême-orientale. Elles ne prouvent pas que Publisher resserre
les blancs d'un texte occidental. Confiance : basse à moyenne.

### Ce que les documents réels emploient, et ce que l'import en fait

**C23 — Le saut de ligne forcé devient une fin de paragraphe, et décale les écarts.**
L'import écrit `\n` pour `insertLineBreak` (`MyPublisher.Core/Services/PubImporter.cs:228`), et
la composition lit `\n` comme une fin de paragraphe (`MyPublisher.Core/Models/Compositeur.cs:155`,
`:238`, `:373-386`). Le compteur de paragraphes de l'import, lui, n'avance que sur
`closeParagraph` (`PubImporter.cs:209`) : les écarts posés après le saut glissent d'un
paragraphe. ATA 21 contient un tel saut (« …through the mixing / chamber. ») dans son premier
bloc, qui compte huit paragraphes. Effet visible sur ce document : le dernier paragraphe du
bloc, « This butterfly valve has two functions… », perd sa marge gauche de 0,193 in et son
retrait de −0,2183 in, parce que l'écart d'indice 7 tombe sur l'ancien paragraphe 6.
Commande : `grep -n insertLineBreak "$S/etude/raw/ATA 21 - Air conditioning.txt"`.
Confiance : haute (mécanisme lu dans le code).

**C24 — Les étiquettes de langue des documents réels sont fausses.** Les Fiches portent 298
fragments en-US et 31 fr-FR, ATA 21 porte fr-FR sur ses 73 fragments ; or ces 104 fragments
étiquetés fr-FR sont tous du texte anglais. MyPublisher ne lit pas `fo:language`.
Commande : `grep -oE 'fo:language: [a-z]+' "$S/etude/raw/Fiches limitations et procédures d'urgences.txt" | sort | uniq -c`.
Confiance : haute.

**C25 — L'interligne de MyPublisher a le sens du sp**, multiple de la hauteur naturelle du
plus grand style de la ligne (`Compositeur.cs:134`, `:207-209`, `:599-600`). Il est réglé par
bloc, 1,25 par défaut (`MyPublisher.Core/Models/Frame.cs:559`), sans mode exact. L'import pose
`LineHeight = 1` (`PubImporter.cs:304-307`) et ne lit pas `fo:line-height`. Confiance : haute.

**C26 — L'écran crénèle, le PDF non** (`Compositeur.cs:85-86`). Sur ATA 21, l'écart de 2,96 pt
relevé par Q13 « reste sans crénage » (`MyPublisher/Docs/PLAN.md:3566-3570`) : il ne vient pas
du crénage. Sur l'exemple et les Fiches, l'écart résiduel est le crénage. Confiance : haute.

**C27 — Marges de cadre.** Les cadres des documents ATR, enregistrés par une version récente,
ont des marges de 0,04 in (2,88 pt) ; MyPublisher crée les siens à 4 pt (`Frame.cs:568-583`).
Le défaut propre à Publisher 98 n'est pas établi : les documents de 1997 du CD ont des marges à
zéro, réglées à la main. Confiance : moyenne.

## Principes à retenir, et ce qui ne vaut plus en 2026

**Une seule mesure décide de la coupure.** Line Services coupe sur une mesure de référence
puis présente ; seules `LsQueryLineDup` et `LsdnSetRigidDup`, que Quill importe, l'appuient
(confiance basse à moyenne). La recommandation qui en découle, façonner une fois et dessiner
les mêmes glyphes des deux côtés, est une option du seuil D, que le plan réserve au propriétaire
du projet (`PLAN.md:96`). Voir [`01-moteur-texte.md`](01-moteur-texte.md).

**Le crénage par paires appartient à la police, et le seuil appartient au document.** Les
versions de Publisher qui ont produit les fiches ATR gardent vraisemblablement le seuil de
14 pt (à relever). Crénager tout le texte donnerait alors d'autres chasses et d'autres coupures
que Publisher sur le Calibri de 10 pt d'ATA 21. Le seuil est donc une propriété du document,
14 pt à l'import, et un document créé dans MyPublisher peut crénager à tous les corps. PDFsharp
n'écrit pas de paires par `XGraphics.DrawString` : il faut écrire le flux à bas niveau ou poser
les glyphes un à un, ce qui rattache ce principe au seuil D.

**L'approche ajoute de l'espace, l'échelle déforme.** Trois réglages distincts : l'approche,
le crénage manuel entre deux caractères, la mise à l'échelle de la chasse. Ils valent (Tc, TJ,
Tz au PDF) ; l'unité de 2026 est le millième de cadratin, et une famille étroite dessinée vaut
mieux qu'une échelle. Les bornes de 0,1 à 600 % ne sont pas à reproduire.

**L'interligne a un mode relatif et un mode exact, par paragraphe.** Les deux modes valent : ce
sont ceux d'OpenDocument et de CSS, et l'exact est nécessaire pour une grille de lignes de base.
L'asymétrie de Publisher a une raison : en sp, l'espace ajouté sous la ligne garde la première
ligne collée au haut du cadre ; en exact, c'est l'interlignage classique, d'une ligne de base à la
suivante. Elle compte pour la fidélité : les sept étiquettes à 525 % de `clrchk.jsp`, dans des
cadres de 0,5 in, descendraient de plus de deux hauteurs de ligne avec la répartition de part et
d'autre qu'emploie l'export (`MyPublisher.Core/Services/PdfExporter.cs:620-625`). Le placement se
tranche donc par mode.

**Le style est un nom posé sur un paragraphe.** Il vaut, à condition de séparer styles de
paragraphe et de caractère et d'introduire l'héritage. Voir [`05-modele-document.md`](05-modele-document.md).

**La césure dépend de la langue.** Elle vaut avec des motifs libres et la langue portée par le
texte ; mais les étiquettes de langue des documents réels sont fausses (C24), si bien qu'une
langue de document doit pouvoir l'emporter. Voir [`03-cesure.md`](03-cesure.md).

**Justifier, c'est d'abord élargir les espaces dans des bornes.** Une compression des blancs
changerait les coupures par rapport à Publisher, donc la reproduction des 31 paragraphes
justifiés d'ATA 21. Elle se réserve aux blocs créés dans MyPublisher ou à un réglage, et ne se
présente pas comme un principe de Publisher 98 (C21).

**Les effets sont des booléens.** Petites capitales par la fonctionnalité OpenType `smcp`,
fabriquées seulement à défaut ; contour et ombre relèvent d'A14. Relief et gravure sont des
effets d'écran de 1997 : à lire à l'import, pas à proposer.

**L'exposant est un type, pas une valeur.** On demande d'abord `sups` et `subs` à la police, et
l'on ne réduit et décale qu'à défaut, avec des valeurs de repli écrites une fois.
`MyPublisher.Core/Models/StyleSpan.cs:59-65` stocke un décalage en multiple du corps, ce qui
suffit.

**La page appartient au cadre.** Marges intérieures, colonnes, habillage, alignement vertical,
ajustement, rotation et mentions de suite sont des propriétés du cadre ; MyPublisher a déjà ce
découpage (C6, R3, A7, A1). L'interdit de l'ajustement sur les cadres chaînés ne s'étend pas à
l'alignement vertical (C19).

## État dans MyPublisher

- **Fragments et écarts.** Les fragments (`MyPublisher.Core/Models/StyleSpan.cs:41-65`) et les
  écarts de paragraphe (`MyPublisher.Core/Models/ParagraphSpan.cs:59-104`) sont importés et
  rendus. Le fer par paragraphe est prêt dans le modèle (`ParagraphSpan.cs:104`) et lu par la
  composition (`Compositeur.cs:217-219`) ; aucun geste ne pose un écart (R6).
- **Saut de ligne forcé** : voir C23.
- **Interligne** : voir C25.
- **Crénage** : voir C26.
- **Taquets** : un pas fixe de 36 pt mesuré depuis le bord gauche **du texte**
  (`Compositeur.cs:399-419`), alors que Publisher mesure depuis le bord du cadre (C10). L'écart
  vaut la marge intérieure (2,88 pt sur les Fiches) et touche leurs 157 tabulations.
- **Montée de la ligne** au PDF : `PdfExporter.cs:685-699`.
- **Langue** : aucune propriété ; la seule culture fixée est fr-FR, pour les dates des champs
  (`MyPublisher.Core/Models/Field.cs:58`).
- **Souligné, approche, échelle, effets, lettrine, styles nommés** : absents.
- **Contour du bloc** sur les quatre côtés à la fois (`Frame.cs:172-186`) ; Publisher permet un
  filet par côté (dialogue 525), ce que le plan écarte (`PLAN.md:4304`).

## Écart

| Sujet | Publisher 98 | MyPublisher | Priorité |
|---|---|---|---|
| Saut de ligne forcé | Distinct du paragraphe | Fin de paragraphe, écarts décalés (C23) | haute : document réel touché |
| Coupure après un trait d'union | Oui (« spring-loaded » dans ATA 21) | Jamais (`Compositeur.cs:421-450`) | haute, voir T2 |
| Langue par fragment | Oui | Ni stockée ni importée | haute, préalable de la césure |
| Crénage | Paires au-dessus d'un seuil | Écran oui, PDF non | dossier D |
| Interligne | Par paragraphe, sp ou exact | Par bloc, multiple seul | moyenne : un document du CD |
| Souligné | 17 sortes | Absent | moyenne : R7 le promet sans propriété |
| Césure | Par story, zone | Absente | moyenne : 31 paragraphes justifiés d'ATA 21 |
| Approche, crénage manuel, échelle | 0,1 à 600 % ou pt | Absents | basse : libmspub ne transmet que l'échelle |
| Effets | Six cases | Absents (A14 spécifié) | basse |
| Taquets posés | Quatre alignements, points de suite | Pas fixe | basse : libmspub ne les transmet pas |
| Origine des taquets | Bord du cadre | Bord du texte | à vérifier sur les Fiches |
| Lettrine | Oui | Absente | basse : aucun document réel |
| Styles nommés | Oui | Absents | voir fiche 05 |
| Copier et appliquer une mise en forme | Ctrl+Maj+C et Ctrl+Maj+V (217:99.58-59) | Absent | basse, avec R7 |
| Guillemets typographiques | Option (sujets 1162, 385) | Absent | basse |

## Postes proposés

Codes du tableau unique de [`00-index.md`](00-index.md#postes-proposés).

- **T1 — Le saut de ligne forcé.** Le saut devient U+2028, que la composition traite comme une
  coupure sans fin de paragraphe. Recette : `Samples/recette-saut-de-ligne.mypub`, paragraphe
  justifié en retrait suspendu de −15,7 pt coupé par un U+2028, puis un paragraphe centré ; au
  PDF et à l'écran, le mot qui suit le saut s'aligne sur les lignes de suite du retrait, à
  0,05 pt, et le paragraphe centré reste centré. ATA 21 importé : le premier bloc contient un
  U+2028 et huit paragraphes, « chamber. » est aligné sur les lignes de suite, et « This
  butterfly valve… » retrouve sa marge de 0,193 in. Le poste tranche si la ligne qui précède le
  saut se justifie (Word l'étire par défaut, sauf l'option de compatibilité
  `doNotExpandShiftReturn`), apprend U+2028 à `Recalage` (Q1), à la zone de saisie et au PDF, et
  passe le format en version 6 pour qu'une version antérieure ne rende pas ce caractère sans le
  dire. Sabotage : rendre U+2028 comme `\n` fait tomber la recette.
- **T6 — Le souligné.** Le modèle porte style, simple ou double, épaisseur et « mots seuls »,
  comme OpenDocument, ce qui relit seize des dix-sept sortes que libmspub transmet par
  `fillUnderline` (`MSPUBCollector.cpp:254-330`). Recette : une ligne par sorte, dont une
  justifiée ; trait de l'épaisseur indiquée par la table `post` ; « mots seuls » ne laisse aucun
  trait sous les blancs ; écran et PDF à la même ordonnée à 0,1 pt ; un `.pub` à
  `style:text-underline-type: double` s'importe en double.
- **T9 — L'interligne par paragraphe, relatif ou exact.** Recette :
  `Samples/recette-interligne-exact.mypub`, trois paragraphes à 1 sp, à 18 pt exact avec un mot
  en 24 pt, et à 2 sp ; au PDF, 18,00 pt ± 0,05 entre deux lignes de base pour le deuxième, deux
  fois la hauteur naturelle pour le troisième ; écran et PDF à 0,05 pt ; `clrchk.jsp` s'importe
  avec sept écarts à 5,25 et ses étiquettes restent en haut de leur cadre ; migration testée.
- **T10 — Petites capitales, capitales, lecture des effets.** Recette : petites capitales en
  DejaVu Sans, qui n'a pas `smcp`, à 70 % de la hauteur des capitales, à 0,1 pt ;
  `fo:font-variant` et `fo:text-transform` lus à l'import ; `style:font-relief` ignoré avec un
  avertissement.
- **T11 — Taquets posés.** Recette : un tableau de consignes à trois taquets (gauche, décimal,
  droite avec points de suite) ; au PDF, virgules alignées à 0,05 pt, dernier mot calé, points de
  suite réguliers ; le poste tranche l'origine des taquets (bord du cadre comme Publisher, ou du
  texte).
- **T16 — Approche et échelle horizontale** (après le dossier D). Recette : un mot à +100 ‰
  s'élargit de 0,1 corps par caractère au PDF (Tc relevé dans le flux) ; un mot à 80 % mesure
  80 % de sa chasse à 0,05 pt (Tz) ; écran et PDF concordent.
- **F2 — La langue du texte**, avec une langue de document qui l'emporte (voir
  [`03-cesure.md`](03-cesure.md)).
- **T13 — La césure**, sur un texte anglais ([`03-cesure.md`](03-cesure.md)).
- **D2 — Un seul crénage pour les deux rendus**, avec le seuil de crénage comme propriété du
  document (dossier D, [`01-moteur-texte.md`](01-moteur-texte.md)). Sa recette ne vise pas
  l'écart de 2,96 pt d'ATA 21, qui ne vient pas du crénage (C26), mais l'exemple et les Fiches.

Compléments aux postes ouverts du plan, à verser dans leurs recettes quand ils seront repris :

- **R6** : les raccourcis Ctrl+L, E, R, J et Ctrl+Q (retirer l'écart du paragraphe) ; dans un
  bloc d'ATA 21 qui contient le saut forcé, centrer le paragraphe qui suit le saut et vérifier
  que c'est lui qui se centre. Dépend de T1.
- **R7** : sans sélection, la mise en forme s'applique au mot courant (option 421 du dialogue
  568) ; Ctrl+Espace retire la mise en forme de caractère ; le corps change par pas de 1 et de
  0,5 pt. Dépend de T6.

## Questions ouvertes

1. Quels sont les pourcentages des préréglages d'approche, la zone de césure, les marges de
   cadre et la gouttière par défaut de Publisher 98 ? Aucune ressource ni aucune aide ne les
   donne ; il faudrait un `.pub` créé pour chaque préréglage.
2. Où le `.pub` range-t-il l'approche et le crénage manuel ? La question ne compte que si l'on
   écrit un jour le `.pub` (F7 propose de ne pas le faire) ; elle se traite d'abord par
   l'observation de fichiers produits.
3. Les 157 tabulations des Fiches visent-elles des taquets posés, que libmspub ne transmet pas ?
4. Où se pose la première ligne de base dans le cadre, selon le mode d'interligne ? C'est ce qui
   décide de la fidélité verticale d'ATA 21 et de `clrchk.jsp`.
