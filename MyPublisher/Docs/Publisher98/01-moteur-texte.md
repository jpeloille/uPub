# 01 — Architecture du moteur de texte : Quill et Line Services

**Domaine** : point dur n° 1, la mise en page du texte, vue par son architecture : qui
possède le texte, qui coupe les lignes, qui mesure, comment le texte passe d'un cadre à
l'autre.
**Sources** : CD de Publisher 98 (`mspub.exe`, `shared/quill/qs98.dll`, `msls2.dll`,
`qrtf98.dll`, `pub5int.dll`, aides, documents `.jsp`) ; littérature publique (blog de Murray
Sargent, code de WPF sous licence MIT, brevets, libmspub sous MPL 2.0).
**Verdict** : `partiel` — MyPublisher a une composition commune à l'écran et au PDF, mais par
bloc, sans story, sans façonnage, avec deux mesures réconciliées par un maximum.

Notations et cadre : voir [`00-index.md`](00-index.md#notations). Fiches voisines :
[`02-typographie.md`](02-typographie.md), [`03-cesure.md`](03-cesure.md),
[`04-polices.md`](04-polices.md) ; côté Publisher 2007,
[`../Publisher2007/12-ajustement-texte.md`](../Publisher2007/12-ajustement-texte.md)
(chaînage, statut `manquant`) et [`../Publisher2007/22-fonctions-hors-sommaire.md`](../Publisher2007/22-fonctions-hors-sommaire.md)
(habillage).

## Ce que fait Publisher 98

Publisher 98 ne compose pas son texte lui-même. Le travail passe par trois étages, chacun
dans son binaire :

- `mspub.exe` pose les pages, les cadres et les objets. Il charge le moteur de textes à la
  demande et ne parle jamais au compositeur de ligne.
- **Quill** (`qs98.dll`) tient les *stories* : le stockage du texte, l'édition (curseur,
  sélection, glisser-déposer, saisie par méthode d'entrée), la répartition du texte dans les
  cadres. Il mesure les caractères par GDI.
- **Line Services 2.50** (`msls2.dll`) compose une ligne à la fois, sur demande de Quill. Il
  n'importe aucune fonction du système : il ne peut ni lire une police, ni mesurer, ni
  dessiner, et rappelle son client pour tout.

Le texte d'une publication est rangé hors des cadres. Un seul flux UTF-16 réunit toutes les
stories, découpé par une table de longueurs et une table d'identifiants ; un cadre ne porte
qu'un numéro de story. Le chaînage, le débordement, l'écoulement automatique et les mentions
« suite page n » découlent de ce modèle, et l'aide les décrit règle par règle.

Il n'y a ni façonnage (pas de substitution de glyphes, donc pas de ligatures), ni texte
bidirectionnel. Le crénage automatique ne s'applique qu'au-dessus d'un seuil de corps. Line
Services sépare un périphérique de référence, sur lequel on mesure et on coupe, d'un
périphérique de présentation, sur lequel on dessine ; Publisher 98 prend l'imprimante comme
référence et repagine quand elle change.

## Ce qui a été relevé

### Répartition des rôles

**C1 — `mspub.exe` charge Quill dynamiquement et ignore Line Services.** Ses 16 DLL importées
ne comprennent ni `qs98` ni `msls2` ; il nomme `QS98.DLL` en UTF-16 et importe `LoadLibraryA`
et `GetProcAddress`.
Preuve : `mspub.exe`, chaîne UTF-16 à fichier+0x203CA0 ; `$X/pe/mspub.exe/imports.txt`.
Commande : `strings -el -t x -n 5 $CD/mspub.exe | grep -i qs98`. Confiance : haute.

**C2 — Quill est un gestionnaire de stories dont les boîtes de texte sont des vues.**
`qs98.dll` (« Microsoft Quill 98 », 98.00.2403, nom interne `QuillStories`) exporte
`QSManager_GetInstance`, `QSTextBox_CreateInstance` et les quatre points d'entrée COM.
Preuve : `$X/pe/qs98.dll/version.txt`, `exports.txt` ; chaînes `QuillStoriesManager`
(+0x68234) et « Quill96 Story Group Class » (UTF-16, +0x681A4).
Commande : `strings -t x -n 6 $CD/shared/quill/qs98.dll | grep QuillStoriesManager`.
Confiance : haute.

**C3 — Quill pilote Line Services par 20 ordinaux** : contexte, `LsSetDoc`, création,
affichage et interrogation de ligne, `LsSetBreaking`, `LsSetCompression`, `LsSetExpansion`,
`LsSetModWidthPairs`, objets ruby, tatenakayoko et « Hih ».
Preuve : `$X/pe/qs98.dll/imports.txt`, section `msls2.dll`, ordinaux résolus par la table
d'export du CD. Commande : `sed -n '/^msls2.dll/,$p' $X/pe/qs98.dll/imports.txt`.
Confiance : haute.

**C4 — Line Services est un compositeur pur, à rappels.** `msls2.dll` (2.50.19.0, 3 décembre
1997) n'a aucune table d'importation. Ses 66 exports se rangent en familles : contexte,
ligne, sous-ligne, coupure en arrière, en avant ou forcée, justification par tables de
paires, nœuds, taquets résolus après coup, interrogation position ↔ point en coordonnées
u/v, objets incrustés. Sargent le dit de l'extérieur : le moteur ne fait jamais d'appel au
système.
Preuve : `$X/pe/msls2.dll/exports.txt` et `imports.txt`. Commande :
`cat $X/pe/msls2.dll/exports.txt $X/pe/msls2.dll/imports.txt`. Confiance : haute.

**C5 — Le lecteur RTF de Quill est un composant COM que rien ne nomme.** `qrtf98.dll`
(« Quill RTF Converter ») n'est cité par aucun binaire du CD, mais l'installation
l'auto-enregistre comme serveur COM avec `qs98.dll`. Sa table de mots-clés RTF connaît `upr`,
`ud`, `uc`, `rtlch`, `rtlpar`, `hyphauto`, `hyphhotz`, `kerning`, `expndtw`, `charscalex`,
`cols`.
Preuve : `pub5.stf` lignes 167-168 (`SelfReg`) ; `pub5.inf` lignes 945-948 ; mots-clés à
+0x15BE8 (`upr`), +0x15F34 (`rtlch`), +0x16830 (`hyphauto`), +0x1727C (`charscalex`).
Commande : `grep -n 'qs98\|qrtf98' $CD/pub5.stf`. Confiance : moyenne (pour l'accès par
`CoCreateInstance`, qui est déduit).

**C6 — Quill mesure par GDI, sur un contexte de périphérique.** Il importe `GetCharWidthW`,
`GetTextExtentPointW`, `GetOutlineTextMetricsA` et `GetKerningPairsA`, qui ne lit que la
table `kern` héritée. Il assure aussi l'édition : caret, capture, `DoDragDrop`, presse-papiers,
saisie par `ImeShare.dll`.
Preuve : `$X/pe/qs98.dll/imports.txt` ; chaîne `ImeShare.dll` à +0x683C0.
Commande : `grep -n 'GetKerningPairs\|GetCharWidthW\|DoDragDrop' $X/pe/qs98.dll/imports.txt`.
Confiance : haute.

### Le texte est une story

**C7 — Un flux unique, découpé en stories, et des cadres qui ne portent qu'un numéro.**
*Constat noté incertain par la vérification, à cause d'une référence fausse, corrigée ici.*
Le flux `Quill/QuillSub/CONTENTS` contient un bloc `TEXT` en UTF-16, découpé en stories par
`STRS` (longueurs) et `SYID` (identifiants). La mise en forme est rangée en pages de 512
octets (`FDPC`, `FDPP`) indexées par `BTEC` et `BTEP`, à la manière des pages de propriétés
du format binaire de Word. Dans `cheklist.jsp`, 15 stories dont les longueurs font
exactement les 11 565 caractères de `TEXT`. Le numéro de story d'un cadre se lit à +0x58 de
son enregistrement : les fichiers qui ont un flux Quill passent dans libmspub par
`MSPUBParser2k` (`MSPUBDocument.cpp:131-138`, `MSPUBParser2k.cpp:720-723`), et non par
l'analyseur de Publisher 97. `mspub.exe` remet ce sous-stockage à Quill par
`_LpstgQuillFromMd@4` (import, +0x20DAA0).
Commande : `$PY $W/quill.py $CD/cheklist.jsp $CD/infoshet.jsp $CD/clrchk.jsp`.
Confiance : haute pour la structure, à confirmer pour l'emplacement exact du numéro (voir la
fiche [`06-format-pub.md`](06-format-pub.md)).

**C8 — Une story peut être partagée par deux cadres.** Dans `infoshet.jsp`, les cadres 0x113
et 0x156 portent la même story n° 1 à +0x58 et un rang de 0 puis 1 à +0x5C ; un bloc de type
0x43 les cite dans cet ordre. Le texte n'est écrit qu'une fois. C'est la preuve, sur un
fichier du CD, que la chaîne de cadres repose sur la story.
Commande : `$PY $S/etude/06-scripts/chunk_hex.py $CD/infoshet.jsp 0x113,0x156`.
Confiance : moyenne (rôles déduits des octets).

### Les règles de la chaîne

**C9 — Ce qu'on peut connecter.** On ne verse que dans un cadre de texte vide, ni dans
soi-même, ni dans un cadre de la même chaîne. Un cadre d'une autre chaîne perd son lien amont
et commence une story nouvelle, après confirmation. Une chaîne ne s'étend pas sur le premier
plan et l'arrière-plan, et le nombre de cadres connectés par publication est borné.
Preuve : `pub5int.dll`, chaînes 217:101.15 à 101.19, 102.9, 114.70, 114.71.
Commande : `chaine '101\.1[5-9]|102\.9|114\.7[01]'`. Confiance : haute.

**C10 — Ce que font les opérations sur la chaîne.** Déconnecter renvoie la partie détachée
au débordement du cadre sélectionné. Supprimer un cadre au milieu fait passer son texte au
suivant ; supprimer le dernier le renvoie au débordement du précédent. Agrandir un cadre peut
vider les suivants. Copier une partie de la chaîne copie des cadres vides. Ctrl+A sélectionne
la story entière ; Ctrl+Entrée envoie au cadre suivant, Ctrl+Maj+Entrée à la colonne
suivante.
Preuve : `mspub.m14`, sujets 2095, 2108, 2143, 2137, 2148, 1365.
Commande : `for n in 2095 2108 2143 2137 2148 1365; do sujet $n $M14; done`.
Confiance : haute.

**C11 — Le débordement est conservé, invisible et signalé.** Le texte qui ne tient pas va
dans une zone de débordement qui ne s'imprime pas ; un indicateur « Text in Overflow » et le
vérificateur de conception la signalent ; un texte en débordement ne s'aligne pas
verticalement.
Preuve : sujets 2034 et 453 ; `qmspub.hlp` 589 ; chaînes 217:56.0, 217:114.109 ; dialogues
567 et 595 (« Text in overflow area »). Commande : `sujet 2034 $M14`. Confiance : haute.

**C12 — L'écoulement automatique.** Quand un texte importé ne tient pas, Publisher propose
de l'écouler, demande cadre par cadre (Oui, Non, Annuler), puis propose de créer cadres et
pages. « Text Frame Connecting » et « Autoflow » sont des libellés d'annulation : chaque
opération sur la chaîne est un pas.
Preuve : sujet 2111 ; chaînes 217:102.28, 103.16, 104.29, 88.21, 88.38.
Commande : `chaine '102\.28|103\.16|104\.29|88\.(21|38)'`. Confiance : haute.

**C13 — Les mentions de suite sont calculées.** « Continued on page… » et « Continued from
page… » sont deux cases du dialogue des colonnes et marges (539, contrôles 816 et 817), avec
leurs styles propres et un numéro de page qui se met à jour seul. Elles forment des régions
du cadre où l'on peut poser un folio mais pas une lettrine.
Preuve : dialogue 539 ; chaînes 217:1.62 à 1.65, 101.10, 107.18 ; `qmspub.hlp` 196-197 ; sujets
2227, 2229, 2375. Commande : `chaine '1\.6[2-5]|101\.10|107\.18'`. Confiance : haute.

**C14 — La césure est une propriété de la story.** « Automatically hyphenate this story » :
cocher la case dans un cadre chaîné césure toute la chaîne. Les options fixent le défaut des
nouveaux cadres et une zone de césure mesurée depuis la marge droite (depuis la gauche pour un
texte au fer à droite).
Preuve : dialogue 105 (contrôles 14200 et 8506), dialogue 568 (460, 461) ; sujets 2220, 2224,
2277 ; `qmspub.hlp` 164-165. Commande : `dlg 105`. Confiance : haute.

**C15 — L'ajustement automatique est refusé sur une chaîne et sur un cadre à colonnes.** Il
ne descend pas sous 0,1 pt et se coupe si l'on change le corps à la main.
Preuve : chaînes 217:114.110 à 114.112 ; sujets 2099, 2151, 2152.
Commande : `chaine '114\.11[0-2]'`. Confiance : haute.

### Mesure, crénage, interligne

**C16 — Référence et présentation.** Les exports `LsSetDoc`, `LsQueryLineDup`,
`LsdnSetRigidDup` et `LsdnGetDup` distinguent des unités de présentation ; la déclaration
publique d'un descendant de Line Services dans WPF (`LineServices.cs:967-973` et
`1471-1476`, `LoSetDoc` avec `isReferencePresentationEqual`) le dit en clair. Publisher 98
« repaginate[s] text » quand les propriétés de l'imprimante changent.
Preuve : chaînes 217:109.15 et 217:103.32. Commande : `chaine '103\.32|109\.15'`.
Confiance : moyenne pour le lien avec `LsSetDoc`, haute pour le comportement.

**C17 — Le crénage automatique ne s'applique qu'au-dessus d'un seuil.** *Constat noté
incertain par la vérification, pour sa lecture.* Le seuil va de 0,5 à 999,5 pt, 14 pt par
défaut. L'aide donne deux raisons, dans cet ordre : un texte de moins de 14 pt « usually
doesn't need to be kerned », et abaisser le seuil ralentit Publisher (sujet 1172). C'est donc
d'abord un choix typographique, dont la lenteur est la conséquence, et non une simple
économie de calcul.
Preuve : `qmspub.hlp` 412 (ID d'aide 1530), 407, 408 ; sujets 1156, 1170 à 1172 ; dialogue 537.
Commande : `sujet 412 $QHLP ; sujet 1172 $M14`. Confiance : haute pour les valeurs.

**C18 — L'interligne en « sp ».** La ligne accueille son plus grand corps, plus un
supplément ajouté sous la ligne ; 1 sp par défaut. En unités absolues, l'espace s'ajoute
au-dessus. Preuve : sujet 2190 ; `qmspub.hlp` 193 (ID 630). Commande : `sujet 2190 $M14`.
Confiance : haute.

**C19 — Une police privée pour les caractères invisibles.** Quill embarque une TrueType
« MSVisi » (Monotype, 1.01) dont les glyphes s'appellent `UF000` à `UF00F`, `Zerowidthspace`,
`ZerowidthNBSP`, `Dotbelow` : c'est l'affichage des signes de mise en forme. Le contexte de la
lignée WPF déclare de même un jeu de caractères `wchVisi*` (`LineServices.cs:857`).
Preuve : `qs98.dll`, ressource RT_FONT 8193, 13 672 octets à +0x6A1B0. Commande :
`grep -n 8193 $X/pe/qs98.dll/types-ressources.txt`. Confiance : moyenne. La copie extraite
pour la lecture ne doit ni entrer dans le dépôt ni servir à MyPublisher.

**C20 — Ni bidi, ni façonnage.** `msls2.dll` 2.50 n'a pas d'objet « inversé » (Sargent le date
de LineServices 3.0) ni de rappel de glyphes, et l'aide n'a aucun sujet sur l'écriture de
droite à gauche.
Commande : `grep -i reverse $X/pe/msls2.dll/exports.txt ; grep -n -i 'right-to-left\|hebrew\|arabic' $M14`.
Confiance : moyenne.

**C21 — La version livrée.** Le billet de Sargent de 2006 dit que LineServices 2.0 a été
livré avec Greetings Workshop (1997) puis avec Publisher (1998) ; sa révision de 2021
renumérote les versions. Le CD donne raison au texte de 2006 : « Microsoft® Line Services »
2.50 est dans Publisher 98.
Commande : `cat $X/pe/msls2.dll/version.txt`. Confiance : haute.

Hors de ce relevé, sans suite : une table de bornes de plages Unicode est commune à `qs98.dll`
(vers +0x62E80) et à `qrtf98.dll` (vers +0xF8E0). Son rôle (classes de coupure ou choix de jeu
de caractères) n'est pas établi, confiance basse ; le chercher demanderait de suivre du code,
ce que le cadre exclut pour ce domaine.

## Principes à retenir, et ce qui ne vaut plus en 2026

| Principe | Chez Publisher 98 | En 2026 | À ne pas reproduire |
|---|---|---|---|
| **La story est indépendante des cadres.** Un cadre est une vue sur une portion de story ; supprimer ou déconnecter un cadre ne détruit pas le texte | Flux `TEXT`/`STRS`/`SYID` ; cadre réduit à un numéro (C7, C8) ; règles de C9 et C10 | C'est la base de toute PAO | La borne du nombre de cadres connectés et l'interdit d'une chaîne entre premier plan et arrière-plan (217:114.70-71) |
| **Le formatage de ligne est séparé du flux.** Le compositeur reçoit une position et une largeur, rend une ligne et l'endroit où reprendre ; choisir le cadre, la colonne et la largeur de chaque ligne revient à l'étage supérieur | Line Services pour la ligne, Quill pour les cadres | Vaut ; c'est ce qui rend l'habillage possible (largeur par ligne) | Voir la réserve sur l'optimisation du paragraphe, plus bas |
| **Le compositeur demande, le client fournit.** Il ne possède ni texte ni police ; il demande les runs, les largeurs, les taquets, les occasions de césure, puis demande au client de dessiner | `msls2.dll` sans importation (C4) ; interface publique dans WPF (`LineServices.cs:813-960`) | Vaut : c'est ce qui rend le compositeur testable sans écran ni PDF | La mesure par GDI, avec hinting et résolution du périphérique ; on mesure en unités de police, par un façonneur |
| **Une référence, une présentation.** On coupe sur une mesure, on dessine ailleurs | C16 | Vaut, avec une référence abstraite et identique pour tous les rendus, comme WPF (`TextFormatterContext.cs:137-153`) | Prendre l'imprimante comme référence et repaginer quand elle change |
| **Les objets incrustés sont des greffons du compositeur** | Ruby, tatenakayoko, « Hih » (C3) | Vaut pour les champs, les mentions de suite, les images dans le texte | — |
| **La coupure se décide par classes de caractères** | `LsSetBreaking` et voisins (C3) | Sous la forme normalisée d'UAX #14, plus les règles locales | Des tables propres à l'application |
| **La justification procède par priorités** : les espaces dans des bornes d'abord, puis des moyens plus sévères | `cJustPriorityLim` du contexte ; brevets US 6 288 726 et US 6 321 243 (1997, expirés le 27 juin 2017) | Vaut | — |
| **Le crénage par paires vient de la police** | `GetKerningPairsA`, seuil de 14 pt (C17) | Par GPOS, appliqué par le façonneur | La seule table `kern`. Le seuil, lui, est à garder comme propriété du document pour la fidélité des `.pub` repris, et non à supprimer d'office |
| **Unicode** | UTF-16 dans le fichier, API GDI en W | Étendu aux grappes de graphèmes et à la normalisation | L'absence de façonnage et de bidi (C20) |
| **La césure est un service externe, portée par la story, avec une zone** | C14 ; `mshy32.dll` trouvé par le registre | Vaut pour sa place dans l'architecture | Le lexique propriétaire ; on prend des motifs libres et la langue portée par le texte ([`03-cesure.md`](03-cesure.md)) |
| **Chaque opération sur la chaîne est un pas d'annulation** | C12 | Vaut. MyPublisher annule par instantanés du document entier (`MyPublisher/ViewModels/MainWindowViewModel.cs:18`, `:21-26`), ce qui suffit pour la chaîne | — |
| **Le débordement est conservé et vérifiable** | C11 | Vaut ; MyPublisher l'a (A2) | Le refus de l'ajustement sur les chaînes et les colonnes (C15) |
| **Les mentions de suite sont calculées** | C13 | Vaut, comme champ | Des régions de cadre à part ; un paragraphe généré, d'un style dédié, suffit |
| **Stories paginées sur disque** (confiance basse : déduit d'un nom de fichier) | Gabarit de fichier temporaire `~Qil%04d.tmp` dans `qs98.dll` (+0x68266, `strings -t x -n 4 $CD/shared/quill/qs98.dll \| grep Qil`) ; la mémoire manque aussi pendant l'écoulement (217:103.17) | Ne vaut plus : contrainte de mémoire de 1997 | — |

**Réserve sur l'optimisation du paragraphe.** Couper le paragraphe entier par pénalités
(Knuth et Plass, public depuis 1981) est abordable en 2026. Mais la technique qui consiste à
garder une coupure ligne à ligne pendant la frappe et à différer l'optimisation est celle du
brevet US 7 596 752 (« Delaying optimal paragraph layout during editing », déposé en 2005),
que Google Patents donne en vigueur jusqu'au 29 septembre 2027. Le brevet voisin US 7 770 111
porte sur la mise en page optimisée elle-même ; il est donné expiré. Ces deux états sont à
faire vérifier avant tout poste qui optimise le paragraphe ; d'ici là, MyPublisher garde son
compositeur glouton.

## État dans MyPublisher

Références relues au commit `469b861`.

- **Le texte appartient au bloc.** `TextFrame` porte son texte (`MyPublisher.Core/Models/Frame.cs:481`),
  ses fragments (`Frame.cs:546`) et ses écarts de paragraphe (`Frame.cs:643`) ; une case de
  tableau a les siens (`MyPublisher.Core/Models/Table.cs:107`). Un bloc n'a pas d'identifiant,
  seulement un nom libre (`Frame.cs:120`), ce que le plan relève déjà (`MyPublisher/Docs/PLAN.md:4266`).
- **Chaque bloc est composé à part, par chaque rendu.** `Compositeur.Composer` reçoit le texte,
  les fragments, la largeur, la hauteur, le colonnage, l'interligne et une règle de mesure
  (`MyPublisher.Core/Models/Compositeur.cs:131-135`). L'écran l'appelle
  (`MyPublisher/Views/ControleDeBloc.cs:171`), la vignette des modèles aussi
  (`MyPublisher/Views/TemplatePreview.cs:279`), l'export de son côté
  (`MyPublisher.Core/Services/PdfExporter.cs:572`). Rien n'est stocké.
- **Le flux s'arrête au bloc.** La dernière colonne ne rompt pas et ce qui dépasse continue
  hors du bloc (`Compositeur.cs:316`, `:327-332`). Le débordement est calculé
  (`PdfExporter.cs:580-581`) et signalé à l'export (`PdfExporter.cs:583-588`) comme à l'écran
  (`MyPublisher/Views/MarqueurDebordement.cs:42`) : c'est A2, le seul équivalent de la zone de
  débordement.
- **La coupure se fait aux espaces et aux tabulations** (`Compositeur.cs:421-442`), et aussi,
  à tort, à chaque frontière de fragment : un mot dont une partie est en gras est découpé en
  deux unités (`Compositeur.cs:146-149`, `:366-381`) que le renvoi à la ligne sépare sans
  condition (`Compositeur.cs:262-263`). Un essai le montre : « ef » en gras dans « efficace »
  donne les lignes `[abcdefg ef]` et `[ficace]`. Le commentaire de `Compositeur.cs:146-148`
  parle d'un drapeau que la structure `Unite` (`Compositeur.cs:352`) ne porte pas. Il n'y a ni
  UAX #14 ni césure.
- **Deux mesures, réconciliées par un maximum.** PDFsharp mesure sans crénage
  (`MyPublisher.Core/Services/MetriquesPdf.cs:53-60`), Avalonia avec crénage et en imposant le
  sens gauche-droite (`ControleDeBloc.cs:194-198`, `:211-214`) ; la coupure suit la plus large
  (`Compositeur.cs:85-86`, `:262-263`). C'est la solution que le propriétaire a retenue et fait
  livrer dans Q13 (`PLAN.md:3599-3606`, `PLAN.md:85`), pas une solution provisoire. La mesure se
  fait mot par mot (`Compositeur.cs:251-254`).
- **L'écran forme vraisemblablement déjà des ligatures que le PDF n'a pas.** L'écran dessine
  des `FormattedText` par morceau (`MyPublisher/Views/TexteCompose.cs:48`), façonnés par
  HarfBuzz avec ses traits par défaut, et DejaVu Sans porte `liga`. Seule la coupure est
  protégée, par le maximum. C'est à établir par un essai (poste T5).
- **Le style de caractère est court** : famille, corps, gras, italique, couleur, décalage de
  ligne de base. Les champs du fragment sont à `MyPublisher.Core/Models/StyleSpan.cs:32-65`, le
  style résolu à `StyleSpan.cs:79-81`. Ni approche, ni crénage, ni échelle, ni langue.
- **Les briques sont sur la machine, hors du noyau.** HarfBuzzSharp 8.3.1.1 entre dans
  l'application par Avalonia.Desktop 11.3.9 (`MyPublisher/MyPublisher.csproj:28`) et
  Avalonia.Skia ; on le voit après restauration par
  `dotnet list MyPublisher/MyPublisher.csproj package --include-transitive`. Avalonia expose `GlyphRun`
  (construit à partir d'indices et d'avances) et `DrawGlyphRun`. La collection
  `PolicesDuDocument` charge déjà dans Avalonia le fichier que fontconfig désigne pour l'export
  (`MyPublisher/Views/PolicesDuDocument.cs:36`), avec les simulations décidées par fontconfig
  (`PolicesDuDocument.cs:29`). Le noyau ne référence que CommunityToolkit.Mvvm et PDFsharp
  6.2.1 (`MyPublisher.Core/MyPublisher.Core.csproj:16-22`), qui n'a pas d'API publique de
  dessin par indice de glyphe.
- **La recomposition est coûteuse** : 170 à 470 ms pour recomposer un panneau (Q23,
  `PLAN.md:95`). Une chaîne de plusieurs pages ne se tiendrait pas sans cache par paragraphe.

## Écart

| Sujet | Publisher 98 | MyPublisher | Écart |
|---|---|---|---|
| Propriétaire du texte | La story, à part, reliée par numéro | Le bloc | Ni story ni identifiant de bloc, deux préalables de toute chaîne |
| Flux | Story → chaîne de cadres → colonnes | Bloc → colonnes ; le reste sort du bloc | Ni chaîne, ni zone de débordement à rendre, ni écoulement, ni mentions de suite |
| Mesure | GDI sur l'imprimante | Deux règles, réconciliées par un maximum | Deux mesures au lieu d'une ; un mot qui tiendrait de justesse passe à la ligne |
| Façonnage | Aucun | Aucun dans le noyau ; Avalonia façonne à l'écran | Ni ligatures ni GPOS au PDF ; écart probable à l'écran (T5) |
| Coupure | Classes de paires, césure, zone | Espaces, tabulations, et frontières de fragment par erreur | Pas d'UAX #14, pas de césure, et une coupure au milieu des mots |
| Bidi | Absent | Absent, sens imposé | Égal ; l'architecture doit garder l'ordre logique |
| Justification | Priorités, compression, expansion | Espaces entre mots, sans bornes (`Compositeur.cs:462-501`) | Ni bornes ni recours |
| Propriétés de story | Césure, langue | — | À créer au niveau de la story |
| Recomposition | Incrémentale | Tout, à chaque rendu | Pas de cache |

## Postes proposés

Les codes sont ceux du tableau unique de [`00-index.md`](00-index.md#postes-proposés). Les
postes D forment un **dossier de décision** pour le propriétaire du projet, et non des postes
ouverts : le plan veut que l'ajustement, le chaînage et l'habillage soient décidés ensemble
(`PLAN.md:4263-4264`), et D2 défait une décision livrée de Q13.

- **T2** — Couper selon UAX #14, sur le paragraphe entier (défaut de la coupure au milieu des
  mots compris).
- **T5** — Essai : établir si l'écran forme des ligatures que le PDF n'a pas.
- **F3** — Un identifiant stable sur chaque bloc et chaque page.
- **D1** — Un façonneur unique dans le noyau. Recette : pour un même fichier de police et une
  même version de HarfBuzz, avances identiques au bit près dans trois processus et trois ordres
  de chargement ; « AV » et « To » mesurent moins que la somme de leurs lettres ; « fi » rend
  une grappe de deux caractères avec `liga` et deux glyphes sans ; le noyau ne référence ni
  Avalonia ni Skia. Le poste dit comment l'écran et PDFsharp appliquent le même gras ou le
  même penché synthétiques, quelle police de secours le noyau choisit pour un caractère
  absent, et il façonne par run (paragraphe et style) et non par mot. L'égalité entre machines
  suppose des polices fournies avec le projet ; sur une même machine, elle est garantie.
- **D2** — L'écran et le PDF dessinent les glyphes du noyau. Recette : `LargeurDeCoupure`
  disparaît ; `Scripts/comparer-mots.sh` sur l'exemple, ATA 21, les fiches ATR et
  `recette-coupure.mypub` : même mot sur la même ligne, abscisse à moins de 0,01 pt ; un essai
  écrit établit que l'écran garde les abscisses fractionnaires d'un `GlyphRun` ; la voie PDF
  (une grappe par `DrawString`, glyphes par indice en étendant PDFsharp, ou `SKDocument`, qui ne
  sait faire ni CMJN ni PDF/X) est choisie après un essai qui vérifie que `pdftotext -bbox`
  reconstitue encore les mots.
- **D3** — La story, séparée du bloc ; la migration décide d'abord si une case de tableau est
  une story.
- **D4** — Le flux d'une story dans une chaîne de blocs, aux règles C9 et C10.
- **D5** — Les mentions de suite calculées.
- **D6** — L'écoulement automatique à l'import.
- **D7** — La justification par priorités.
- **D8** — Le bidi réservé dans l'architecture (priorité basse).

L'ajustement automatique et l'habillage, les deux autres expressions du seuil D, se posent sur
la même interface : l'ajustement est une recherche du corps sur le formatage de ligne,
l'habillage une largeur de ligne que le bloc rend selon l'ordonnée.

## Questions ouvertes

1. Un cadre chaîné importé d'un `.pub` reçoit-il le texte entier de sa story ? Oui : le banc de
   la fiche 06 l'a observé sur `infoshet.jsp`, où les deux cadres reçoivent les mêmes 1 040
   caractères ([`06-format-pub.md`](06-format-pub.md), C16), parce que le collecteur de libmspub
   remplit chaque forme de la story entière (`MSPUBCollector.cpp:652-663`). L'import double donc
   le texte sans le dire, et aucun poste ne l'avertit avant D3. Reste ouvert le format de la chaîne
   côté `Contents` (rang à +0x5C, bloc 0x43, octet +0x17), l'information dont l'import des chaînes
   aura besoin.
2. Avalonia et Skia conservent-ils les abscisses fractionnaires d'un `GlyphRun` explicite à
   toutes les échelles d'affichage ? C'est la condition de D2.
3. La langue doit-elle être portée par la story ou par le fragment ? Publisher 98 met la langue
   sur le texte (sujet 1867) et la césure sur la story.
4. Que désigne l'objet « Hih » de Line Services 2.50 ? Le sens n'est pas établi.
