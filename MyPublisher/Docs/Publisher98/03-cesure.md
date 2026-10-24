# 03 — Césure, coupure de ligne et vérification orthographique

**Domaine** : point dur n° 1, côté coupure : où une ligne peut se couper, qui calcule les
points de césure d'un mot, qui décide de couper, et dans quelle langue.
**Sources** : `shared/proof/mshy32.dll` et `mshy_en.lex` (césure), `mssp232.dll` et ses
lexiques (orthographe), `pub5.inf` et `pub5.stf` (installation), chaînes de `mspub.exe`,
dialogues 105, 543 et 568 de `pub5int.dll`, aides ; données Unicode (UAX #14, `LineBreak`
18.0) et motifs de césure libres (hyph-utf8).
**Verdict** : `manquant` — MyPublisher n'a ni césure, ni langue dans le modèle, et sa coupure
de ligne a un défaut au milieu des mots.

Notations : voir [`00-index.md`](00-index.md#notations). Fiches voisines :
[`01-moteur-texte.md`](01-moteur-texte.md), [`02-typographie.md`](02-typographie.md).

## Ce que fait Publisher 98

Publisher 98 ne calcule pas ses césures. Il demande les points de coupure d'un mot au
césureur partagé d'Office (`mshy32.dll`, nom interne CHAPI, hérité de Word 6.0), qui travaille
sur un lexique compressé fourni par un tiers (`mshy_en.lex`). Il le trouve par le registre,
langue par langue. Un seul lexique anglais sert l'anglais américain, britannique et australien.

C'est Publisher qui décide de couper, avec deux réglages : une case par story, et la zone de
césure, seuil de tolérance du blanc mesuré du côté du bord irrégulier. Il n'offre ni limite de
coupures consécutives, ni exclusion des capitales, ni dictionnaire d'exceptions : l'aide se
contente de conseiller de ne pas couper plus de deux lignes de suite. Les traits automatiques ne
sont pas du texte ; les traits conditionnel et insécable et l'espace insécable en sont.

L'orthographe suit le même schéma (CSAPI, `mssp232.dll`, dictionnaire principal et
dictionnaire de l'utilisateur).

## Ce qui a été relevé

### Les composants

**C1 — Le césureur partagé.** `mshy32.dll` : version 1.30.3119, « Microsoft Houghton Mifflin
ICS DLL », nom interne CHAPI, nom d'origine `hyph32.dll`, daté du 18 mai 1995 ; il porte la
chaîne « Word for Windows 6.0 Hyphenation DLL ».
Preuve : `$X/pe/mshy32.dll/version.txt` ; chaîne à fichier+0x4800.
Commande : `strings -t x -n 5 $CD/shared/proof/mshy32.dll | grep 'Hyphenation DLL'`.
Confiance : haute.

**C2 — Six fonctions, aucune pour les exceptions.** *Constat noté incertain.* `HyphVer`,
`HyphInit`, `HyphTerminate`, `HyphOpenLang`, `HyphCloseLang`, `HyphCheck`, de base ordinale 200 ;
aucune fonction d'ajout, d'où l'on déduit qu'il n'y a pas d'exceptions de l'utilisateur.
Commande : `cat $X/pe/mshy32.dll/exports.txt`. Confiance : haute pour la liste, moyenne pour la
conclusion.

**C3 — Les fonctions sont vraisemblablement prises par ordinal.** En dehors des deux DLL qui les
exportent (`mshy32.dll` et `mssp232.dll`, seuls fichiers que rend la commande), aucun binaire du
CD ne nomme `HyphCheck`, `HyphInit`, `SpellCheck` ni `SpellInit`, alors que `mspub.exe` importe
`LoadLibraryA` et `GetProcAddress`.
Commande : `grep -rla -E 'HyphCheck|HyphInit|SpellCheck|SpellInit' $CD`. Confiance : moyenne.

**C4 — Le césureur travaille en caractères 8 bits.** *Constat noté incertain.* Il n'importe que
`KERNEL32` (dont `MultiByteToWideChar` et `GetStringTypeW`) et `CharToOemA`.
Commande : `cat $X/pe/mshy32.dll/imports.txt`. Confiance : moyenne.

**C5 — Le lexique de césure.** `mshy_en.lex` commence par un avis de droits Inso (0x000 à
0x152), puis un en-tête binaire en 0x180 et les chaînes « 960905 », « 02.91.002 », « english
pref disk hyphen » (0x1A0), « EN96_01.ICS » (0x1C0), « 2.01.005 » (0x1E0). Les données
commencent en 0x200, en trois zones dont un bloc compressé ; aucune chaîne n'y est lisible.
Commande : `xxd -s 0x180 -l 0x80 $CD/shared/proof/mshy_en.lex`. Confiance : haute.
L'avis revendique un secret de fabrique sur le contenu : l'étude s'est arrêtée à l'en-tête et à
la mesure d'entropie, et ne va pas plus loin.

**C6 — Un dictionnaire de mots plutôt que des motifs.** *Constat noté incertain.* La mention
« pref disk hyphen » et l'absence de motifs lisibles font penser à une liste de mots et de
points préférés, non à des motifs à la TeX. Confiance : basse.

**C7 — L'orthographe.** `mssp232.dll` (CSAPI, 1.63.5626, « Word for Windows 6.0 Spell DLL »)
exporte 16 fonctions `Spell*` : dictionnaire principal, dictionnaire de l'utilisateur avec mots
acceptés (`AddUdr`) et remplacements mémorisés (`AddChangeUdr`). Les lexiques sont compressés.
Commande : `cat $X/pe/mssp232.dll/exports.txt`. Confiance : haute.

**C8 — Un lexique pour trois pays.** `MSHY_EN.LEX` est inscrit pour 1033, 2057 et 3081.
Commande : `sed -n 232,246p $CD/pub5.stf`. Confiance : haute.

**C9 — L'annuaire par le registre.** `mspub.exe` cherche
`Proofing Tools\Hyphenation\%d\Normal` (UTF-16, +0x1E9DF0), `Spelling` (+0x1F02D0),
`Custom Dictionaries` (+0x1F0208) ; ses préférences sont `HotZone` (+0x209A90) et
`AutoHyphenate` (+0x209AA0).
Commande : `strings -el -t x -n 4 $CD/mspub.exe | grep -i -E 'proofing|hotzone|autohyph'`.
Confiance : haute.

**C10 — Le césureur de Publisher 97 est effacé.** *Constat noté incertain.* `HYPH.DAT` n'apparaît
que dans la section `[Remove MSPub 97]` de `pub5.inf` (ligne 89) ; la migration copie
`AutoHyphenate` et `HotZone` de `mspub.ini` vers le registre (`pub5.stf` lignes 981-982 ; une
seconde voie à la ligne 1050).
Commande : `sed -n 86,91p $CD/pub5.inf ; sed -n 981,982p $CD/pub5.stf`. Confiance : moyenne.

### Le comportement

**C11 — Trois commandes.** Le dialogue 105 n'a que la case « Automatically hyphenate this
story », la zone de césure et « Manual… » ; le dialogue 568 ajoute la césure des cadres neufs
et une zone par défaut. Aucun réglage de coupures consécutives, de capitales ni de longueur
minimale. Commande : `dlg 105 ; dlg 568`. Confiance : haute.

**C12 — La zone se mesure du côté du bord irrégulier** : depuis la marge droite pour un texte
au fer à gauche ou centré, depuis la gauche pour un texte au fer à droite ; en in, cm, pi ou pt.
Une zone plus petite donne plus de coupures et un bord plus régulier. Pour le texte justifié,
l'aide dit seulement que la césure réduit les blancs (sujets 2192, 2226).
Preuve : sujets 2224, 2277 ; `qmspub.hlp` 164-165. Commande : `sujet 2277 $M14`. Confiance : haute.

**C13 — Les traits automatiques ne sont pas des caractères** : on ne les efface qu'en coupant
la césure, Publisher ne déplace que ce qu'il a inséré, et ils disparaissent à l'export HTML.
Preuve : sujets 2280, 2281, 2304, 2426. Confiance : haute.

**C14 — Les caractères de l'auteur.** Trait conditionnel Ctrl+-, trait insécable Ctrl+Maj+-,
espace insécable Ctrl+Maj+Espace, espace insécable de chasse nulle Ctrl+Maj+0, saut de ligne
Maj+Entrée, saut de colonne Ctrl+Maj+Entrée, saut de cadre Ctrl+Entrée ; Ctrl+Maj+H bascule la
césure. Preuve : sujets 928, 2264, 2300. Confiance : haute.

**C15 — « Pas plus de deux lignes coupées de suite » n'est qu'un conseil**, à côté de « pas de
césure dans les titres » et « pas de trait en bas de colonne ». Preuve : sujet 2269 ; absence de
réglage dans les dialogues 105 et 568. Confiance : haute.

**C16 — La césure manuelle** (dialogue 543) propose un point ; l'auteur l'accepte, en choisit
un autre, ou renvoie le mot entier. Preuve : `qmspub.hlp` 157-159 ; sujet 2266. Confiance : haute.

Messages voisins, non commentés ici : 217:103.10 propose de césurer la story ; 217:102.18,
102.19 et 103.11 disent que le césureur ou le lexique manque, ce qui demande un repli explicite
quand les motifs d'une langue sont absents ; le sujet 2222 prévient qu'activer la césure change
la mise en page, donc les débordements.

**C17 — Line Services n'exporte pas de césure à lui.** *Constat noté incertain.* `msls2.dll`
n'a aucun export de césure ; il a une table de coupure (`LsSetBreaking`) et des recherches de
coupure. Le code public de la lignée WPF montre pourtant que le compositeur pilote la césure par
rappel au client (`Hyphenate`, `LsHyph`), y compris les césures qui changent l'orthographe
(`LsKysr` : ajouter, changer ou supprimer avant la coupure ; `LineServices.cs:132`, `514` et
suivantes). L'idée à retenir est donc : le compositeur demande les points au client et applique
le type de coupure.
Commande : `grep -i -E 'hyph|break' $X/pe/msls2.dll/exports.txt`. Confiance : moyenne.

### Ce que portent les documents

**C18 — La langue déclarée ne dit pas la langue écrite.** ATA 21, en anglais, est étiqueté fr-FR
sur ses 73 fragments ; les Fiches portent 298 fragments en-US et 31 fr-FR, ces derniers étant des
titres anglais en capitales. libmspub ne restitue aucun indicateur de césure.
Commande (depuis la racine du dépôt) :
`LD_LIBRARY_PATH=.outils/mspub/lib .outils/mspub/bin/pub2raw 'MyPublisher/Books/ATA 21 - Air conditioning.pub' | grep -o 'fo:language[^,]*' | sort | uniq -c`.
Confiance : haute.

**C24 — La zone par défaut de Publisher 98 n'est pas sur le CD.** Le défaut de Word documenté par
OOXML est de 0,25 po (18 pt) ; l'appliquer à Publisher reste une hypothèse.
Commande : `grep -rn -i hotzone $CD/pub5.inf $CD/pub5.stf $CD/admin.inf`. Confiance : basse.

### Les données libres

**C22 — Les motifs.** *Constat noté incertain, pour la liste de référence.* `hyph-fr.tex`
(hyph-utf8 V2.13) est sous licence MIT, 1 145 motifs ; `hyph-en-us.tex` est sous une licence
permissive de copie et de modification ; `hyph_fr.dic` de LibreOffice est sous LGPL 2.1 ou
ultérieure. Un essai de Liang aux bornes 2 et 3 sur un paragraphe du plan
(`MyPublisher/Docs/PLAN.md:4280-4286`) donne chan-tier, lo-gi-ciel, re-pous-ser, dé-fen-dable et
refuse « sau-ra ». Le découpage en mots de l'essai compte la ponctuation dans les bornes
(« pri-se, ») : la liste ne vaut référence qu'une fois ce découpage fixé.
Commande : `sed -n 1,46p $S/dl/hyph-fr-tex/hyph-fr.tex`. Confiance : haute pour les licences.

**C23 — UAX #14** (`LineBreak` 18.0) interdit la coupure avant « : », « ; » et « ? » même après
une espace (`0020 × 003A` dans `LineBreakTest.txt`). U+00A0, U+202F et U+2011 sont de classe GL,
U+00AD de classe HH, U+2060 de classe WJ.
Commande : `grep -E '^(00A0|202F|2011|00AD|2060|003A)' $S/dl/ucd/LineBreak.txt`. Confiance : haute.

## Principes à retenir, et ce qui ne vaut plus en 2026

| Principe | En 2026 | À ne pas reproduire |
|---|---|---|
| **Séparer « où peut-on couper » de « faut-il couper ».** Le césureur rend des points, le compositeur décide avec la zone et l'état de la ligne | C'est l'architecture de TeX, de libhyphen, d'InDesign | Une DLL 8 bits chargée par ordinal |
| **Les traits automatiques ne sont pas du texte.** Recalculés à chaque composition, jamais insérés dans le texte de l'auteur | Décisif pour une composition partagée par l'écran et le PDF | Le « tout ou rien » : offrir aussi « ne pas couper ce mot » (U+2060) |
| **Les traits de l'auteur sont des caractères** | U+00AD, U+2011, U+00A0, U+202F, U+2060, U+200B, avec les raccourcis de Publisher | Les codes de contrôle privés ; prévoir le repli quand la police n'a pas U+2010 ou U+202F ([`04-polices.md`](04-polices.md)) |
| **La zone de césure, seuil de tolérance du blanc** | Vaut pour un compositeur glouton, au fer à gauche ou à droite. Son extension au justifié est déduite, l'aide ne la mesure pas | La zone comme seul critère ; ajouter au plus deux coupures consécutives, une longueur minimale, pas de mot tout en capitales |
| **Césure par story, défaut par préférence** | Vaut ; le bloc tient lieu de story tant que le seuil D n'existe pas. La césure est inactive pour les documents ouverts ou importés, ce qui protège ceux dont la langue est mal étiquetée | — |
| **Des services linguistiques par langue** | Motifs embarqués comme ressources, indexés par étiquette BCP 47 | LCID, registre partagé, lexiques propriétaires, un lexique pour trois pays |
| **La langue est un attribut du texte** | Vaut au fragment, avec une langue de document | Prendre la langue du poste pour celle du texte |
| **La coupure se décide par classes de caractères** | UAX #14, généré depuis les données Unicode et éprouvé sur `LineBreakTest.txt` | Une table maison |
| **Composition ligne à ligne** | Défendable : stable à la frappe. Avec un façonneur, chaque fragment se mesure après la coupure, à cause des ligatures (« ef-ficace ») | — |
| **Deux sortes d'entrées dans le dictionnaire de l'utilisateur** : mot accepté, remplacement mémorisé | Vaut pour une future orthographe ; les exceptions de césure de l'utilisateur, absentes de CHAPI, sont à ajouter | — |

## État dans MyPublisher

- **Coupure.** Les mots sont découpés aux espaces U+0020 et aux tabulations
  (`MyPublisher.Core/Models/Compositeur.cs:421-442`, `:431`). Mais la découpe se fait fragment
  par fragment (`Compositeur.cs:146-149`, `:366-381`), et le renvoi à la ligne passe à toute
  frontière d'unité sans condition (`Compositeur.cs:262-263`) : un mot dont une partie change de
  style peut être coupé au milieu, sans trait (essai : `[abcdefg ef]` puis `[ficace]`). Un mot
  composé ne se coupe jamais à son trait d'union, et « : » peut commencer une ligne : ATA 21 a
  six « Note 1 : ».
- **Justification.** Seule U+0020 s'élargit ; U+00A0 et U+202F restent fixes
  (`Compositeur.cs:471-474`). La dernière ligne d'un paragraphe ne se justifie pas
  (`MyPublisher.Core/Services/PdfExporter.cs:630`).
- **Décision de coupure.** Elle lit `LargeurDeCoupure`, la plus large des deux mesures
  (`Compositeur.cs:81-99`, `:262-268` ; `MyPublisher.Core/Services/MetriquesPdf.cs:56-60`). Un
  export lancé sans écran décide sur la seule mesure PDF (`MetriquesPdf.cs:59`).
- **Langue.** Aucune propriété (`MyPublisher.Core/Models/StyleSpan.cs:27`,
  `MyPublisher.Core/Models/Frame.cs:469`) ; l'import ignore `fo:language` et `fo:country`. Les
  cultures employées sont fixes ou prises au poste : fr-FR pour les dates des champs
  (`MyPublisher.Core/Models/Field.cs:58`) et les nombres de l'export
  (`PdfExporter.cs:607-608`), la culture du poste pour le texte façonné de l'écran
  (`MyPublisher/Views/ControleDeBloc.cs:211-212`) et pour le catalogue de modèles
  (`MyPublisher.Core/Services/TemplateStore.cs:79`). La mesure de l'écran peut donc dépendre de
  la machine par les traits OpenType propres à une langue, contre le principe de `Field.cs:52-58`.
- **Correspondance avec le texte source.** Un morceau composé ne porte pas son décalage dans le
  texte source (`Compositeur.cs:8`). Dès qu'un trait est ajouté ou qu'un U+00AD est retiré, le
  texte composé n'est plus une sous-chaîne du texte, et ce qui remonte du composé vers le texte
  (sélection, recherche, `Recalage`) se décalerait. Trois rendus consomment les morceaux :
  l'écran, le PDF et la vignette (`MyPublisher/Views/TemplatePreview.cs:287`).
- **Saut de ligne Maj+Entrée** : il devient une fin de paragraphe (voir
  [`02-typographie.md`](02-typographie.md), constat C23, et le poste T1).

## Écart

| Sujet | Publisher 98 | MyPublisher | Écart |
|---|---|---|---|
| Coupure au milieu d'un mot | Jamais | À chaque frontière de fragment | Défaut, le plus grave de ce domaine |
| Occasions de coupure | Table de classes | Espaces et tabulations | Ni trait d'union, ni « / », et « : » en tête de ligne |
| Césure automatique | Par story, défaut pour les cadres neufs | Absente | Entier |
| Zone de césure | `HotZone`, en in, cm, pi ou pt | Absente | Entier |
| Trait conditionnel, trait insécable | Caractères saisis au clavier | U+00AD non traité, U+2011 insécable par accident | Partiel ; U+00AD serait rendu différemment par Avalonia et par PDFsharp |
| Espaces insécables | Caractères | U+00A0 et U+202F insécables par accident, rognés en bout de ligne (`Compositeur.cs:189`, `:198`, `:263`, `:266`) | Ni saisie, ni repli de police |
| Langue | Attribut du texte | Absente | Préalable de la césure |
| Français | Rien sur le CD (1033 seulement) | Rien | Voie propre : `hyph-fr.tex` et UAX #14 |
| Coupures consécutives, capitales | Conseils de l'aide | — | MyPublisher peut faire mieux à peu de frais |
| Orthographe | CSAPI | Absente | Hors de ce lot |

## Postes proposés

Codes du tableau unique de [`00-index.md`](00-index.md#postes-proposés).

- **T2 — Couper la ligne selon UAX #14, sur le paragraphe entier.** Les règles de paires
  portent sur le texte du paragraphe, à travers les fragments, et la coupure aux frontières de
  fragment disparaît. Recette : test pur sur le sous-ensemble latin de `LineBreakTest.txt`
  (18.0), nombre de cas écrit dans le test ; un mot à cheval sur deux fragments ne se coupe
  jamais ; « Note 1 : In case » ne laisse jamais « : » en tête de ligne ; « turbo-fan » se coupe
  après le trait ; « Brown‑Smith » (U+2011) ne se coupe pas ; `Scripts/comparer-mots.sh` rend
  zéro ligne coupée autrement sur l'exemple et les deux documents ATR. Sabotage : permettre la
  coupure avant la classe IS fait tomber le cas « Note 1 : ».
- **T8 — Les caractères de coupure de l'auteur.** Un morceau porte d'abord son décalage dans le
  texte source. Recette : « ef­ficace » (U+00AD) donne « ef- » / « ficace » dans un bloc étroit
  et « efficace » dans un bloc large, sans U+00AD dans aucun morceau ; « 20 °C » et « 150 kt »
  écrits avec U+00A0 ou U+202F ne se coupent pas ; `comparer-mots.sh` à zéro écart sur un
  document qui porte les quatre caractères, dans une police avec U+202F et une sans ; les
  raccourcis de Publisher insèrent le bon caractère ; sélection et recherche restent justes sur un
  mot coupé.
- **F2 — La langue du texte dans le modèle**, avec une langue de document. Recette : ATA 21
  importé porte ses 73 fragments fr-FR et un avertissement « une seule langue déclarée » ; les
  Fiches, 298 en-US et 31 fr-FR ; aller-retour d'enregistrement à l'identique ; migration
  testée propriété par propriété ; la langue d'un document neuf se choisit explicitement ou vient
  du modèle, jamais de la machine ; la langue du texte est celle que l'écran passe au façonnage.
  Sabotage : ignorer `fo:country` fait tomber le test des Fiches.
- **T13 — Césure automatique** (Liang, `hyph-fr.tex` et `hyph-en-us.tex`). Recette : sur un
  paragraphe anglais d'ATA 21, justifié dans 140 pt en Calibri 10, toute ligne finie par « - »
  l'est sur un point des motifs anglais ; « downstream » ne sort jamais en « downs-tream »
  (ce que donneraient les motifs français) ; jamais plus de deux lignes coupées de suite ; le
  blanc moyen des lignes justifiées baisse, valeur écrite dans le test ; une zone de 1 000 pt
  n'admet aucune coupure ; les titres en capitales ne se coupent pas ; ATA 21 importé ne se
  césure pas par défaut ; les exceptions du fichier de motifs s'appliquent, et l'une est éprouvée ;
  le découpage en mots est fixé (ponctuation ôtée, apostrophes ’ et ' traitées pareil,
  normalisation NFC). La décision se prend sur `LargeurDeCoupure`, ce qui garde le contrat de
  Q13 tant que le dossier D n'est pas tranché. La zone par défaut reste un réglage de
  l'application et n'entre pas dans le format tant qu'elle n'est pas établie. Sabotages :
  décider sur `Largeur`, ôter la limite de coupures consécutives, ignorer la zone.
- **T14 — Espaces typographiques françaises à la saisie.** Fine insécable avant « ; ! ? » et
  dans les guillemets, insécable avant « : ». Elles ne s'inventent jamais à l'import, et ne
  s'appliquent qu'à un texte dont la langue est vérifiée (avertissement de F2 levé par
  l'auteur) : sinon ATA 21, anglais étiqueté fr-FR, en recevrait à chaque retouche. Recette :
  « Note 1 : test » tapé dans un fragment français vérifié donne l'insécable, fragments recalés ;
  une frappe dans ATA 21 importé ne pose rien ; la commande sur un bloc s'annule en un pas.
- **T15 — Le PDF se lit et se cherche malgré la césure.** Recette : le flux de contenu porte un
  `ActualText` (ou un U+00AD selon ISO 32000) pour chaque mot coupé, contrôlé dans le flux et
  non par `pdftotext`, qui recolle parfois seul les mots coupés ; un sabotage qui retire le
  marquage fait tomber la recette ; l'aspect du PDF ne change pas.

Hors de ce lot : l'orthographe (Hunspell, dictionnaire libre), à décider après la césure.

## Questions ouvertes

1. La césure doit-elle être active par défaut dans les nouveaux blocs ? Le défaut de
   Publisher 98 n'est pas établi par le CD.
2. L'espace insécable U+00A0 s'élargit-elle dans une ligne justifiée (InDesign) ou reste-t-elle
   fixe (Word, et MyPublisher aujourd'hui) ?
3. Pour un document importé dont l'étiquette de langue est fausse, suffit-il d'avertir, ou
   faut-il proposer de corriger l'étiquette ?
4. La zone de césure par défaut des versions récentes de Publisher est-elle documentée par
   Microsoft ? Cela ferait passer l'hypothèse des 18 pt de basse à moyenne.
