# 04 — Polices : inventaire, crénage, ligatures, droits d'incorporation

**Domaine** : point dur n° 1 (mesure et dessin des glyphes) et n° 4 (incorporation au PDF) :
ce que le CD livre comme polices, ce que Publisher 98 en fait, et les écarts que MyPublisher a
entre l'écran et le PDF à cause d'elles.
**Sources** : `fonts/` (179 TrueType), `fonts.mfm`, `rulerfnt.fon`, `pub5.inf`, `pub5.stf`,
`qs98.dll` ; aides ; tables des polices lues par fontTools ; une sonde PDFsharp 6.2.1 et
HarfBuzz 8.3.1 qui lit les polices depuis le CD sans les copier ; code d'Avalonia 11.3.9 et de
PDFsharp 6.2.1 (MIT).
**Verdict** : `partiel` — la résolution et la substitution sont tenues (Q13, B5), mais trois
écarts entre l'écran et le PDF restent cachés par DejaVu Sans, et `fsType` n'est pas contrôlé.

Notations : voir [`00-index.md`](00-index.md#notations). Fiches voisines :
[`01-moteur-texte.md`](01-moteur-texte.md), [`08-impression-pdf.md`](08-impression-pdf.md) ;
côté Publisher 2007, [`../Publisher2007/21-pack-and-go.md`](../Publisher2007/21-pack-and-go.md).

## Ce que fait Publisher 98

Publisher 98 livre ses polices et compte dessus : ses assistants les nomment, et un message
prévient quand il en manque une. Il mesure par GDI, avec les métriques de l'imprimante de la
publication. Il crénèle par la table `kern` au-dessus d'un seuil de corps, ne forme aucune
ligature, et n'incorpore aucune police dans le `.pub` : le fichier ne porte que des noms, et la
substitution est laissée à Windows ou au pilote PostScript. Deux polices privées servent à
l'interface : une police bitmap pour les règles, et une TrueType pour les signes d'affichage.

## Ce qui a été relevé

L'inventaire complet est hors du dépôt, une ligne par fichier et 66 colonnes :
`$S/etude/04-polices-inventaire.csv`. Il se régénère par
`$PY $S/scripts/inventaire_polices.py $CD/fonts $CD/pub5.inf <csv> <json>` et se résume par
`$PY $S/scripts/stats_polices.py $S/etude/04-polices-inventaire.json`. Il reprend des chaînes de
la table `name` (mentions de droits et de marques) : il ne doit pas entrer dans le dépôt tel
quel. En voici les mesures.

| Mesure | Valeur | Remarque |
|---|---|---|
| Fichiers TrueType de `fonts/` | 179, 106 familles au sens de `name` 1 | `valupack/*.cab` en porte 30 autres, dont 21 identiques, 6 en autre version et 3 absentes de `fonts/` |
| Groupes d'installation | 118 « polices des assistants », 61 « supplémentaires » | `pub5.inf` lignes 1616 et 1736 |
| Unités par cadratin | 2 048 pour 176, 1 000 pour 3 | — |
| Familles à quatre styles | 19 | modèle « gras, italique » de GDI ; une autre graisse est une famille à part |
| `GSUB` / `GPOS` | 0 / 0 | polices de l'ère GDI |
| `kern` non vide | 99 (format 0) | médiane 472 paires, maximum 1 000 ; Arial 909, Times New Roman 867 |
| Glyphes `fi`, `fl` | 173, dont 84 atteignables par U+FB01 et U+FB02 | aucune substitution automatique possible |
| `hdmx`, `LTSH`, `VDMX` | 131, 126, 123 | caches au pixel pour GDI |
| `fsType` | 60 installables, 86 éditables, 33 prévisualisation et impression, 0 restreinte | aucun bit 0x100 ni 0x200 |
| Français (œ Œ Ÿ « » ’ … — U+00A0) | 175 | les quatre polices de symboles exceptées |
| U+202F, U+2009, U+2011 | 0 | — |
| € | 12 | Arial, Courier New, Times New Roman en 2.40 |
| `USE_TYPO_METRICS` | 0 | — |
| Total `usWin*` > total `hhea` | 16 | jusqu'à 0,231 em (Wide Latin) |
| `hhea.ascender` ≠ `usWinAscent` | 59 | de −0,15 à +0,23 em |
| `usWinAscent` < `head.yMax` ou `usWinDescent` < −`yMin` | 111 | Arial : 1 854 contre 2 124 |

**C1 — Deux groupes, et des assistants qui dépendent de polices précises.** L'installation
type pose le groupe 674 « Fonts used by Publisher's Wizards », pas le groupe 806. Le message
217:114.74 (+0x3DFC8) dit qu'un assistant ne trouve pas une police de sa conception.
Commande : `awk -F'\t' '$1==8 || $1==674 || $1==806' $CD/pub5.stf | cut -c1-300`.
Confiance : haute.

**C2 — Ni `GSUB` ni `GPOS` ; des paires `kern`.** Voir le tableau. La sonde HarfBuzz rend
« fi » en deux glyphes en Arial, en un seul en DejaVu Sans, qui porte `liga`.
Confiance : haute.

**C3 — Aucune police n'interdit l'incorporation.** Lecture selon la spécification OpenType :
pour une table `OS/2` de version inférieure à 3, c'est le bit le moins restrictif qui vaut.
Wingdings vaut 0 et Wingdings 3 vaut 14 : les deux polices de symboles des fiches ATR
s'incorporent. Aucun fichier du CD n'exercerait une règle de refus. Confiance : haute.

**C4 — Couverture du français**, voir le tableau. Latin étendu A est complet dans 49 polices ;
68 polices n'ont, en plus de Latin-1, que le jeu Mac Roman (ı Œ œ Š š Ÿ, sans Ž ni ž, et 21 à 31
signes comme ∂ ∑ π √ ≤ fi fl), ce qui explique U+FB01 dans 84 fichiers. Confiance : haute.

**C5 — Métriques verticales**, voir le tableau. Sous GDI, seuls les `usWin*` comptaient pour la
hauteur de ligne ; `hhea` était la métrique du Macintosh. Ce n'est pas un défaut de police, c'est
la source classique des interlignes qui diffèrent d'un logiciel à l'autre. Confiance : haute.

**C6 — Crénage, approche, échelle.** Crénage automatique par paires au-dessus d'un seuil de
0,5 à 999,5 pt, 14 pt par défaut ; l'aide le justifie d'abord parce qu'un texte plus petit n'a
d'ordinaire pas besoin d'être créné, puis par la lenteur (sujets 1171 et 1172). Crénage manuel
jusqu'à 600 pt, approche à cinq préréglages ou de 0,1 à 600 %, échelle de 0,1 à 600 %.
Preuve : `qmspub.hlp` 412 (ID 1530) ; sujets 1156, 1158, 1160, 1169 ; dialogue 537 ; tables
217:62 et 217:63. Commande : `sujet 412 $QHLP`. Confiance : haute.

**C7 — Les paires viennent de GDI**, par `GetKerningPairsA` (`$X/pe/qs98.dll/imports.txt:125`),
donc de la seule table `kern`. Confiance : moyenne.

**C8 — Pas de ligatures.** Aucune mention dans les ressources, les chaînes ni l'aide, et aucune
police n'a de `GSUB`. Commande : `grep -r -i -l ligature $X`. Confiance : moyenne.

**C9 — Pas d'incorporation dans le `.pub`.** L'aide demande que l'imprimeur ait les mêmes
polices ; la substitution est le fait de Windows ou du pilote (« Always Use TrueType Fonts »).
Le bloc `FONT` du flux Quill ne porte que des noms (`cheklist.jsp`, entrée à 0x1BA, données à
0x76E6, 156 octets). Les versions récentes incorporent en EOT : libmspub appelle
`defineEmbeddedFont` avec le type `application/vnd.ms-fontobject` (`MSPUBCollector.cpp:1849-1855`).
Preuve : sujets 1508, 1528, 1537 ; libmspub `MSPUBParser.cpp:594-657`. Confiance : haute.

**C10 — La mise en page dépend de l'imprimante de la publication** : en changer modifie la
mise en page, « especially the fonts ». Preuve : chaînes 217:106.19 à 106.21 (+0x2AF34) et
217:109.15. Commande : `chaine '106\.19|109\.15'`. Confiance : haute. Corollaire : sous GDI, les
chasses étaient arrondies à la résolution de cette imprimante (300 ou 600 ppp), et les coupures
des documents d'origine en dépendent ; MyPublisher ne peut pas reproduire cet arrondi.

**C11 — `fonts.mfm` sert le pilote PostScript.** Archive Adobe de 69 PFM, installée avec le
pilote et les deux PPD génériques (13 polices résidentes chacun) ; elle permet au pilote de
substituer une police résidente sans télécharger de contour. Preuve : en-tête à bit de poids fort
en 0 ; `pub5.stf` objet 177 ; `msgenbw.ppd:17`.
Commande : `head -c 58 $CD/fonts.mfm | python3 -c "import sys;print(bytes(b&0x7f for b in sys.stdin.buffer.read()))"`.
Confiance : moyenne pour le rôle.

**C12 — `rulerfnt.fon`, la police des règles** : bitmap FNT 2.0 de 7 pt à 96 ppp, 7 pixels de
haut, caractères `%` à `9` ; `mspub.exe` la nomme (UTF-16, 0x1F5A88) et prévoit « Small Fonts »
en repli (0x1F5AB8). Commande : `$PY $S/scripts/fnt.py $CD/rulerfnt.fon`. Confiance : haute.

**C13 — MSVisi, la police des signes d'affichage.** `qs98.dll` embarque une TrueType privée
(ressource RT_FONT 8193, 13 672 octets à +0x6A1B0), installée par
`CreateScalableFontResourceA` et `AddFontResourceA` (`$X/pe/qs98.dll/imports.txt:146-147`).
Voir [`01-moteur-texte.md`](01-moteur-texte.md), C19. Confiance : moyenne.

**C14 — Défauts et vérification.** *Constat noté incertain.* Police par défaut Times New Roman
10 pt ; le vérificateur de conception signale plus de trois polices ; un dialogue 518 « Font
Schemes » existe sans être exposé. Preuve : sujets 384 et 2382 ; chaîne 217:31.7 (+0x10424) ;
`$X/pe/pub5int.dll/dialogues.txt:371`. Confiance : moyenne.

## Ce que les mesures disent de MyPublisher

Les trois écarts qui suivent ne se voient pas sur la machine du projet, parce que DejaVu Sans a
des métriques sans contradiction. Ils sont mesurés par la sonde
(`cd /tmp && dotnet $S/sonde-polices/out/sonde-polices.dll $CD/fonts bkant.ttf calistb.ttf stencil.ttf`)
ou établis par la lecture du code.

**M1 — La hauteur et la montée ne viennent pas des mêmes champs.** À l'écran, la hauteur
naturelle et la montée viennent de `hhea` (`MyPublisher/Converters/InterligneConverter.cs:66-72`,
`MyPublisher/Views/ControleDeBloc.cs:200-209`, et la correction par la montée propre d'Avalonia
dans `MyPublisher/Views/TexteCompose.cs:122-124`) ; au PDF, de `max(hhea, usWin)` et de
`usWinAscent` (`MyPublisher.Core/Services/MetriquesPdf.cs:62`,
`MyPublisher.Core/Services/PdfExporter.cs:624-625` et `:685-699`). À 12 pt, Stencil fait
12,000 pt à l'écran et 14,221 pt au PDF ; la montée de Calisto MT Bold vaut 9,012 contre
11,039 pt. Côté Avalonia, la valeur est calculée d'après son code (11.3.9), non exécutée.
Confiance : moyenne. Quand les `sTypo*` sont nuls (Arial Narrow Bold), PDFsharp prend la montée
dans `hhea` (1 916) là où GDI prenait `usWinAscent` (1 910) : la règle de PDFsharp n'est donc pas
exactement celle de GDI.

**M2 — Le crénage et les ligatures.** PDFsharp ne crénèle pas : « AVATAR To » en Arial 12 pt
mesure 65,350 pt, contre 60,240 pt façonné par HarfBuzz (−7,8 %). HarfBuzz forme la ligature
« fi » de DejaVu Sans ; PDFsharp dessine deux glyphes. L'écran crénèle
(`MyPublisher.Core/Models/Compositeur.cs:85-86`) et forme vraisemblablement les ligatures
(voir [`01-moteur-texte.md`](01-moteur-texte.md)). Commande :
`cd /tmp && dotnet $S/sonde-polices/out/sonde-polices.dll /usr/share/fonts/truetype/dejavu DejaVuSans.ttf`.
Confiance : haute.

**M3 — Un caractère absent sort en `.notdef` au PDF.** Pour une police sans U+202F, PDFsharp
mesure et dessine le `.notdef` (9,000 pt en Arial 12 pt) ; HarfBuzz prend l'espace à
demi-chasse (1,664 pt). C'est le même cas pour €, absent de 167 fichiers sur 179. DejaVu Sans et
Liberation Sans ont U+202F. Commande :
`cd /tmp && ESPACES=1 dotnet $S/sonde-polices/out/sonde-polices.dll $CD/fonts arial.ttf times.ttf gara.ttf`.
Confiance : haute.

**M4 — `fsType` n'est contrôlé par personne.** PDFsharp incorpore toujours un sous-ensemble
(`MetriquesPdf.cs:65-71`, `XFont` sans options) ; il lit `fsType` sans s'en servir. Aucune
occurrence dans le dépôt. Commande :
`grep -rn -i fsType --include=*.cs /home/jpeloille/RiderProjects/uPub | grep -v /obj/`.
Confiance : haute.

## Principes à retenir, et ce qui ne vaut plus en 2026

**Une seule référence de mesure.** Publisher calcule la mise en page avec les métriques du
périphérique final, et l'écran s'y aligne. Le principe vaut ; la source change : ce sont les
tables de la police, indépendantes de tout périphérique.

**La hauteur de ligne selon la règle de Windows**, pour reprendre des documents Publisher :
sans `USE_TYPO_METRICS`, hauteur `max(usWinAscent + usWinDescent, total hhea)` et montée
`usWinAscent`, des deux côtés. La raison n'est pas que `usWin*` couvre tous les glyphes : 111
fichiers du CD ont des glyphes qui dépassent `usWinAscent` ou `usWinDescent`. La raison est la
fidélité aux documents composés sous Windows. Le rognage des glyphes à `usWinAscent`, lui, n'est
pas à reproduire. La fonction qui calcule ces valeurs choisit entre la règle de GDI et celle de
PDFsharp pour les `sTypo*` nuls, et le dit.

**Le crénage par paires vient de la police ; le seuil est une propriété du document.** Le
crénage par GPOS (puis `kern` à défaut) vaut. Le seuil de 14 pt n'est pas un simple
anachronisme : si les versions de Publisher qui ont produit les fiches ATR le gardent, leurs
corps de 8 à 12 pt n'étaient pas crénés, et crénager toujours éloignerait MyPublisher des
documents à reprendre. Il se relève (fiche Publisher 2007), s'importe s'il existe et se porte
dans le document.

**Pas de ligatures en 1997, et en 2026 un défaut qui suit la fidélité.** Les ligatures standard
sont attendues aujourd'hui, à condition que le texte reste extractible. Mais le défaut doit
suivre les documents repris, non une préférence : Word les laisse coupées par défaut, et l'on ne
sait pas ce que faisait la version de Publisher des fiches ATR.

**Une substitution déplace les coupures.** Publisher substitue sans rien dire. B5 le dit déjà ;
il reste à dire si le substitut est métriquement compatible (Liberation pour Arial, Times New
Roman et Courier New ; Carlito pour Calibri). Carlito a `hhea` égal à `usWin*`, U+202F et une
table GPOS : les ATR rendues en Carlito ne sont pas touchées par M1.

**Ne pas incorporer dans le document, incorporer dans le PDF.** Le `.pub` ne garde que des
noms ; c'est un héritage du PostScript. Le PDF incorpore toujours, et PDF/X l'exige. L'archive
`.mypubz` garde la liste des polices, pas leurs fichiers (licences).

**Respecter `fsType`.** Chaque police déclare ce qu'elle permet ; l'aide de 1997 n'en dit rien.
On refuse le restreint et le « bitmap seulement », en le disant, et l'on incorpore en entier une
police marquée 0x100.

**Les caches au pixel** (`hdmx`, `LTSH`, `VDMX`) ne servent qu'à un rendu à la grille de
pixels : on ne les lit jamais pour mesurer.

**Une police privée pour l'interface** : l'idée vaut si « afficher les caractères spéciaux »
arrive, avec une police dessinée pour le projet ou des tracés ; l'installer dans le système ne
vaut plus.

**Le jeu de caractères de 1997 est court.** Aucune police n'a U+202F : le français de 2026 a
besoin d'un remplacement explicite, dans le noyau, de tout caractère absent de la police. Garder
le caractère d'origine à l'extraction demande un `ActualText` : PDFsharp associe un seul code
Unicode à chaque glyphe dans sa table `ToUnicode`, si bien qu'une U+202F dessinée avec le glyphe
d'espace ressortirait U+0020.

## État dans MyPublisher

- **Résolution et substitution** : fontconfig choisit le fichier pour l'écran comme pour le
  PDF depuis Q13 ; une collection `.ttc` retombe sur un substitut, avec avertissement
  (`MyPublisher.Core/Services/FontconfigFontResolver.cs:198-204`), et l'export nomme chaque
  substitution (`PdfExporter.cs:115-150`, B5).
- **Mesures verticales** : M1. **Crénage et ligatures** : M2. **Caractère absent** : M3.
  **Droits d'incorporation** : M4.
- **Approche, échelle, crénage manuel** : absents du style résolu
  (`MyPublisher.Core/Models/StyleSpan.cs:79-81`) et de l'import
  (`MyPublisher.Core/Services/PubImporter.cs:398-403`). libmspub émet pourtant `fo:text-scale`,
  `fo:font-variant`, `fo:text-transform`, `style:text-outline`, `fo:text-shadow` et
  `style:font-relief` (`MSPUBCollector.cpp:1648-1666`) : pour l'échelle et les effets, la perte
  vient de MyPublisher, pas de la source. L'approche et le crénage manuel, eux, ne sont pas
  transmis par libmspub.
- **Polices incorporées des `.pub` récents** : `MyPublisher.Core/Services/PubReader.cs` ne
  relève aucun `defineEmbeddedFont`.

## Écart

| Sujet | Publisher 98 | MyPublisher | Écart |
|---|---|---|---|
| Hauteur de ligne | `usWin*` (GDI) | Écran `hhea`, PDF `max(hhea, usWin)` | 16 polices du CD divergent, jusqu'à 2,8 pt par ligne à 12 pt |
| Montée | `usWinAscent` | Écran `hhea`, PDF `usWinAscent` | 59 polices, jusqu'à 2,8 pt |
| Crénage | `kern` au-dessus de 14 pt | Écran toujours, PDF jamais | −7,8 % sur « AVATAR To » en Arial |
| Ligatures | Aucune | Écran vraisemblablement, PDF non | Dessins différents avec la police par défaut |
| Caractère absent | Substitution du système | `.notdef` de 9 pt au PDF | Une boîte au lieu d'un blanc, et une coupure décidée sur sa largeur |
| Substitution | Silencieuse | Dite (B5) | En avance, mais sans dire si le substitut garde les chasses |
| `fsType` | Ignoré | Ignoré | Rien n'empêche d'incorporer une police restreinte |
| Polices incorporées (EOT) | Sans objet en 5.0 | Non relevées | À éprouver sur un `.pub` récent |

## Postes proposés

Codes du tableau unique de [`00-index.md`](00-index.md#postes-proposés).

- **T3 — La hauteur et la montée d'une ligne viennent d'une seule mesure.** Recette : test pur
  sur des valeurs `hhea` et `OS/2` écrites en dur (cas DejaVu Sans, cas `usWin` plus haut que
  `hhea`, cas de montées différentes, cas `USE_TYPO_METRICS`, cas `sTypo*` nuls) ; test
  d'intégration avec une copie de DejaVu Sans réécrite à la volée (`usWinAscent` + 20 %, nom
  changé) dans un `FONTCONFIG_FILE` temporaire : lignes de base de l'écran et du PDF à 0,05 pt,
  relevées dans les positions calculées par le noyau, et `Scripts/comparer-mots.sh` sans écart de
  ligne de base de plus de 1 pt. Sabotage : rendre à l'écran ses métriques `hhea`.
- **T4 — Mesurer et dessiner les caractères que la police n'a pas.** Règle de repli dans le
  noyau, commune aux deux rendus : U+202F à demi-espace, U+2009 au cinquième de cadratin, U+2011
  en trait d'union insécable, et pour tout autre caractère absent (€ d'abord) une police de
  secours choisie par le noyau, ou un avertissement comme B5. Recette : test pur paramétré par la
  présence du caractère ; écran et PDF de même largeur ; le flux de contenu porte un
  `ActualText` pour l'espace remplacée ; sabotage : retirer le repli.
- **T7 — Dire si le substitut garde les coupures.** Recette : table courte de paires
  métriquement compatibles ; l'export d'un texte en Arial sur une machine qui n'a que Liberation
  Sans dit « chasses identiques » ; Wingdings garde l'alerte actuelle ; test sauté si le
  substitut manque ; sabotage : vider la table.
- **I2 — Respecter les droits d'incorporation à l'export.** Recette : test pur
  `Incorporation(fsType, versionOS2)` sur 0, 2, 4, 6, 8, 14, 0x100, 0x102 et 0x200, avec la règle
  du moins restrictif ; une police libre réécrite à `fsType` = 2 n'est pas incorporée, et le
  rapport le dit ; le poste tranche le sort du texte refusé (refuser l'export, vectoriser,
  substituer en le disant).
- **T5 — Essai : l'écran forme-t-il des ligatures ?** Un mot « fi » en DejaVu Sans est rendu à
  l'écran et au PDF ; on compte les glyphes des deux côtés. Le résultat est consigné ; s'il y a
  écart, il est versé au dossier D, et l'on n'en tire pas de conclusion avant.
- **D2** (dossier D) porte le crénage et les ligatures partagés. La voie « couper le crénage à
  l'écran » n'est pas reproposée : elle a été essayée pendant Q13 (`MyPublisher/Docs/PLAN.md:3568-3569`,
  `:3592`) et le propriétaire en a retenu une autre (`PLAN.md:3599-3606`). Sa recette compare des
  positions calculées par le noyau : `pdftotext -bbox` rend des mots et non des glyphes, et
  `Scripts/comparer-rendus.sh` ne mesure pas la coupure (`PLAN.md:3588-3596`).
- **T12 — Banc local des polices de Publisher**, hors dépôt. Un `fonts.conf` ajoute le dossier
  `fonts` du CD sans le copier ; un script lance `Scripts/comparer-mots.sh` sur des recettes en
  Arial, Book Antiqua, Stencil, Calisto MT Bold et Wingdings 3 ; il échoue avant T3 (écarts de
  ligne de base de 2,03 et 2,17 pt) et passe après ; il s'arrête si le CD n'est pas monté ; rien
  de ce qu'il produit n'est versionné, la licence ne couvrant qu'un ordinateur.

## Questions ouvertes

1. Calibri, police des fiches ATR, a-t-elle `hhea` égal à `usWin*` ? Carlito oui ; Calibri reste
   à relever sur une machine qui l'a.
2. Quelle métrique les versions récentes de Publisher prennent-elles pour « 1 sp » ? Une page
   imprimée en PDF par Publisher et mesurée trancherait.
3. Une police refusée par `fsType` : refuser l'export, vectoriser le texte, ou substituer et le
   dire ?
4. Le blob EOT que libmspub transmet pour un `.pub` récent est-il compressé en MTX ?
5. Comment libmspub transmet-il un caractère Wingdings : code U+F0xx, ou Windows-1252 avec la
   seule police pour le distinguer ?

Les copies faites pour l'étude hors du CD (la police MSVisi tirée de `qs98.dll`, les polices
tirées des cabinets `valupack`) restent dans le dossier de l'étude et sont à effacer après
lecture.
