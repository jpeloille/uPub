# 08 — Impression professionnelle et sortie : du PostScript au PDF/X

**Domaine** : point dur n° 4, l'export PDF, le CMJN et le PDF/X : ce que Publisher 98 faisait
pour l'imprimeur, ce que MyPublisher et PDFsharp 6.2.1 savent faire, et l'ordre dans lequel
combler l'écart.
**Sources** : dialogues 500, 511, 512, 609 à 614 de `pub5int.dll` et leurs chaînes ; les deux
PPD génériques (`msgenbw.ppd`, `msgencol.ppd`), `mspub.inf`, `printer.inf` ; `cheklist.jsp` et
`infoshet.jsp` ; imports de `mspub.exe` ; aides. Côté MyPublisher : le code, deux exports mesurés
de l'exemple, la documentation XML de PDFsharp 6.2.1 et un essai CMJN rendu par Ghostscript.
**Verdict** : `manquant` — l'export écrit un PDF 1.7 en RVB seul, sans boîte de format fini, et
deux exports identiques ne donnent pas le même fichier.

Notations : voir [`00-index.md`](00-index.md#notations). Les essais de cette fiche (exports
mesurés, essai CMJN, rendu des plaques) sont dans `$W`, hors du dépôt. Fiches voisines :
[`04-polices.md`](04-polices.md) ; côté Publisher 2007,
[`../Publisher2007/21-pack-and-go.md`](../Publisher2007/21-pack-and-go.md) et
[`22-fonctions-hors-sommaire.md`](../Publisher2007/22-fonctions-hors-sommaire.md) (impression).

## Ce que fait Publisher 98

Publisher 98 distingue l'imprimante de bureau du « service d'impression ». Préparer la publication
pour un imprimeur est un **état du document**, choisi tôt par un assistant en trois étapes, qui
contraint les couleurs et la composition ; l'imprimante de bureau devient alors une imprimante
d'épreuve, et la publication reste composée pour l'imprimante finale.

Il ne sait pas faire de quadrichromie. Il sait faire du ton direct : le noir et au plus deux
encres, décrites par leur valeur RVB d'affichage et sans nom, avec des teintes en pourcentage
d'encre. Il fabrique lui-même les séparations, une feuille par plaque. Le noir et le petit texte
noir surimpriment, le reste défonce, et il ne fait aucun recouvrement. Les marques d'impression
vont hors de la zone imprimable, sur des formats « Extra » ; les fonds perdus sont des objets posés
sur le bord. Une fiche technique se remplit seule, à côté d'une liste de contrôle et du
vérificateur de conception. La correspondance des couleurs passe par ICM.

## Ce qui a été relevé

**P98-01 — Pas de séparation quadrichromie.** « Four-color (CMYK) separation process printing is
not available » ; le recours documenté est un EPS par page, sous Windows 95, séparé par l'imprimeur
dans un autre logiciel. Preuve : sujets 123, 130, 1460, 1466, 1470, 1519, 1546.
Commande : `for n in 123 130 1466 1546; do sujet $n $M14; done`. Confiance : haute.

**P98-02 — Trois types de travail en trois étapes.** Noir et gris ; couleur (imprimante de moins de
1 200 ppp) ; tons directs, « up to two additional colors » ; puis le choix du pilote ; puis les
formats Extra, les marques et la liste de contrôle. Preuve : dialogues 609, 610, 611 ; chaînes
1.175 à 1.178 ; `pub5int.dll` +0xD2768 ; `qmspub.hlp` 296 à 298. Commande : `dlg 609`.
Confiance : haute.

**P98-03 — Un ton direct est une valeur RVB, sans nom d'encre** ; l'encre se choisit chez
l'imprimeur. Preuve : chaînes 1.181 « Spot Color %d - %dR, %dG, %dB » et 1.29 ; dialogue 614
(TSL et RVB seulement) ; sujet 127 ; aucune occurrence de Pantone.
Commande : `chaine '1\.(29|181)' ; grep -ic pantone $STR`. Confiance : haute.

**P98-04 — En ton direct, des teintes et pas de nuances.** *Constat noté incertain.* Seules les
teintes (mélange au blanc) sont permises ; les objets d'une autre couleur prennent la teinte la plus
proche d'un ton ou d'un gris ; les jeux de couleurs sont désactivés. La conversion n'est pas
silencieuse : le dialogue 609 (contrôle 12307) et le message 198.33 préviennent. Elle est en
revanche irréversible pour les couleurs saisies. Preuve : sujets 156, 1538, 1543 ; chaînes 74.1,
198.33, 1.12 à 1.14. Confiance : moyenne.

**P98-05 — Publisher fait ses séparations** : une feuille par couleur, noir compris, plaques nommées
Composite, Black, Spot %d ; l'épreuve de séparation sort en gris sur toute imprimante. Preuve :
dialogues 511 et 512, contrôle 6354 « Print Color S&eparations » (+0xC1410) ; chaînes 1.90 à 1.92 ;
`qmspub.hlp` 258 ; sujets 106, 128.
Commande : `strings -t x -el -n 5 $CD/pub5int.dll | grep 'Print Color S&eparations'`.
Confiance : haute.

**P98-06 — La trame reste au RIP.** Les PPD génériques ne déclarent ni trame ni clé de séparation ;
la liste de contrôle demande à l'imprimeur ses réglages (film positif, émulsion dessus).
Commande : `grep -n -iE 'screen|colorsep|separation|Negative|Emulsion' $CD/msgen*.ppd`.
Confiance : moyenne.

**P98-07 — Le noir surimprime, le reste défonce.** Deux réglages : surimprimer les objets noirs, et
surimprimer le texte noir sous un corps donné, dont la valeur par défaut n'est pas connue. Aucun
recouvrement, laissé à l'imprimeur. Preuve : sujets 124, 132 ; dialogue 613 (+0xD3A24, +0xD3A74) ;
`qmspub.hlp` 311, 312. Commande : `sujet 132 $M14`. Confiance : haute.

**P98-08 — Formats Extra.** La flasheuse générique ajoute 72 pt par dimension (36 pt par côté) ;
l'aide donne Letter Extra à 9 × 11,5 pouces (+¼ pouce par côté), en désaccord avec le PPD
(684 × 864 pt). Commande : `tr -d '\r' < $CD/msgenbw.ppd | sed -n 36,112p`. Confiance : haute.

**P98-09 — Les marques** : traits de coupe, repères (ton direct seulement), nom du ton, date et
heure, plage de pages, nom de la publication ; posées hors de la zone imprimable, donc seulement si
la page est plus petite que la feuille. Le dialogue d'impression ordinaire a aussi ses traits de
coupe (dialogue 500, contrôle 6352). Preuve : dialogue 611, contrôle 12343 (+0xD3290) ;
`qmspub.hlp` 309 ; sujets 1531, 1820, 1858. Confiance : haute.

**P98-10 — Les fonds perdus sont des objets posés sur le bord**, imprimés sur format Extra avec la
case « Allow Bleeds » de « Create PostScript File » ; aucune largeur n'est paramétrée. Au bureau, on
obtient le même effet en réduisant la page (sujet 1235). Preuve : dialogue 512, contrôle 6357
(+0xC1460) ; sujets 1252, 1279. Confiance : moyenne.

**P98-11 — Les pilotes génériques** : PostScript niveau 1, couleur déclarée avec extension CMJN même
pour la flasheuse, 600 ppp fixes, zone imprimable pleine page pour la flasheuse, format libre
jusqu'à 2 448 × 3 132 pt. Preuve : `msgenbw.ppd` lignes 22-33, 80, 116-121 ; `msgencol.ppd`
lignes 23-34, 62-69 ; `mspub.inf` lignes 20-32. Confiance : haute.

**P98-12 — La composition dépend de l'imprimante cible** : changer d'imprimante repagine,
l'épreuve imite les coupures de la flasheuse, le document garde son imprimante. Les deux pilotes
génériques sont la parade de Publisher : ils évitent de recomposer pour la plupart des flasheuses
(sujet 1499). Preuve : sujets 1494, 1499 ; `qmspub.hlp` 242 ; chaînes 107.21 et 109.15 ; dialogue
511 « Compose On: » (6361). Commande : `chaine '107\.21|109\.15'`. Confiance : haute.

**P98-13 — La fiche technique se remplit seule** : fichier, formats de page et de feuille, nombre
de feuilles et de pages, type de travail, couleurs, marques, imprimante finale, résolution,
imprimante d'épreuve, 25 polices typées, 10 effets spéciaux ; **aucun champ pour les images**.
Preuve : `infoshet.jsp`, flux Quill +0x200, +0x61C, +0x758, +0x8F2 ; chaînes 7.10, 87.0 à 87.3,
1.186 à 1.193, 98.0. Confiance : haute.

**P98-14 — La correspondance des couleurs par ICM** : une case à l'impression, des profils écran
et imprimante requis, et le marquage des couleurs hors gamut ; `mspub.exe` importe
`CheckColorsInGamut`, `CreateColorSpaceA`, `SetICMMode`. Preuve : dialogues 500 et 512 (6353), 614
(12502) ; sujets 96, 98, 104, 119. Commande : `grep -n -iE 'icm|colorspace|gamut' $X/pe/mspub.exe/imports.txt`.
Confiance : moyenne.

**P98-15 — Le vérificateur de conception de l'imprimé.** *Constat noté incertain.* Il contrôle les
cadres vides, le débordement, les proportions, les objets hors page ou en zone non imprimable, les
objets recouverts, les doubles espaces, et des seuils de goût (plus de cinq effets, trois polices,
trois couleurs) ; ni polices absentes ni résolution d'image. C'est le dialogue 567 ; le 595 et les
chaînes 33.x sont ceux du site Web. Preuve : chaînes 31.0 à 31.8 (+0x10424) ; sujet 1501.
Commande : `grep -P '^31\.' $STR`. Confiance : moyenne.

Hors du chemin de l'imprimeur, mais à noter : les papiers spéciaux s'affichent sans s'imprimer
(« The pattern does not print », `qmspub.hlp` 8, ID 30) ; voir
[`07-assistants-ressources.md`](07-assistants-ressources.md). Le livret (dialogue 560) et le
tuilage (557) relèvent de l'imposition.

### MyPublisher et PDFsharp, mesurés

**MP-01 — L'export n'écrit que du DeviceRGB.** Toutes les couleurs passent par `XColor.FromArgb`
(`MyPublisher.Core/Services/PdfExporter.cs:810-815`) ; mesuré sur l'exemple : opérateurs `rg` (13),
`RG` (2), `gs` (15), aucun `k`, aucun espace nommé, aucun `OutputIntents`. PDFsharp écrit sur
chaque page un groupe de transparence en DeviceRGB.
Commande : `MyPublisher/bin/Debug/net10.0/MyPublisher Samples/exemple-bulletin.mypub --date 2026-09-06 --export $W/a.pdf && $PY $W/pdf-operateurs.py $W/a.pdf`.
Confiance : haute.

**MP-02 — Une page n'a que sa MediaBox** (`PdfExporter.cs:71-73`) ; ni TrimBox ni BleedBox
explicites, ni fond perdu dans le modèle (`MyPublisher.Core/Models/Document.cs:70`, `:76` ;
`README.md:478`). Commande : `pdfinfo -box $W/a.pdf`. Confiance : haute.

**MP-03 — Deux exports identiques diffèrent de 137 octets** : `CreationDate` à l'horloge, XMP
(dates, `DocumentID`, `InstanceID`), `/ID` du trailer et étiquettes de sous-ensemble des polices
tirées au hasard. L'export ne pose que le titre et le créateur (`PdfExporter.cs:63-65`). La ligne
de commande accepte `--date` (`MyPublisher/Program.cs:58`, lecture `:79-81`, appel `:83`), qui ne
fixe que la date des champs.
Commande : `cmp -l $W/a.pdf $W/b.pdf | wc -l ; pdffonts $W/a.pdf ; pdffonts $W/b.pdf`.
Confiance : haute. Le contrôle P2 ne compare que le nombre de pages et les avertissements
(`Scripts/controle.sh:83-95`, `:121-127`), ce qui explique que l'écart n'ait gêné personne.

**MP-05 — MyPublisher n'imprime pas et n'a pas d'aperçu** : B3 et B6 sont à 0 %
(`MyPublisher/Docs/PLAN.md:52`, `:55`) ; B3 n'a qu'une recette d'une ligne (`PLAN.md:1642-1648`),
B6 la sienne (`PLAN.md:1746-1748`). Confiance : haute.

**PS-01 — PDFsharp convertit en CMJN par une formule sans profil** (K = 1 − max(R, V, B)) quand le
mode couleur est CMJN : #1F3B57 donne C 0,644 M 0,322 J 0 N 0,659 ; un gris 128 donne N 0,498 seul.
Preuve : `PdfSharp.xml` lignes 21021, 22883 ; essai `$W/essai-cmyk`.
Commande : `(cd $W/essai-cmyk && dotnet run -- $W/essai-cmyk/sortie.pdf) && $PY $W/pdf-operateurs.py $W/essai-cmyk/sortie.pdf`.
Confiance : haute.

**PS-02 — PDFsharp sait la surimpression et les boîtes.** Surimpression d'un aplat
(`/op true /OPM 1`, expérimentale, en CMJN seulement), TrimBox et BleedBox ; Ghostscript confirme
que le cyan reste sous le noir surimprimé. Preuve : `PdfSharp.xml` lignes 6873, 7961, 24247-24317.
Commande : `gs -q -dNOPAUSE -dBATCH -sDEVICE=bitcmyk -dGrayValues=256 -r72 -sOutputFile=$W/sep/p.cmyk $W/essai-cmyk/sortie.pdf && $PY $W/plaque.py $W/sep/p.cmyk 200 100 75`.
Confiance : haute.

**PS-03 — PDFsharp n'écrit ni OutputIntent PDF/X, ni espace Separation, ni conversion ICC** ; sa
couche basse (ressources de page, ajout et remplacement du contenu, dictionnaires, clé
`OutputIntents` du catalogue) est publique. Le seul `OutputIntent` qu'il sait écrire est celui de
PDF/A, en sRGB. Preuve : `PdfSharp.xml` lignes 22458, 12033, 12261-12282, 24359, 24442.
Commande : `strings -n 4 ~/.nuget/packages/pdfsharp/6.2.1/lib/net8.0/PdfSharp.dll | grep -iE 'GTS_|srgb|Separation'`.
Confiance : haute.

**La teinte de MyPublisher** garde sa couleur de base et son pourcentage
(`MyPublisher.Core/Models/FillBrush.cs:268-277`) ; le dessin la mêle au blanc ou au noir en RVB
(`FillBrush.cs:290-304`). Un pourcentage de 100 ou moins est exactement la part de couleur dans le
mélange au blanc de Publisher (sujet 156) : il se traduit directement en pourcentage d'encre dès
que la base est un ton direct. Seule la nuance (au-delà de 100) n'a pas d'équivalent à une encre.

## Principes à retenir, et ce qui ne vaut plus en 2026

**La sortie professionnelle est un état du document, choisi tôt.** Une intention de sortie
attachée au document permet d'avertir pendant la mise en page (couleur RVB dans une sortie CMJN,
objet au bord sans fond perdu). À ne pas reproduire : la conversion irréversible des couleurs
saisies ; le modèle garde la couleur saisie, la sortie dit ce qu'elle en fait.

**Un ton direct se nomme.** Publisher dit où va la couleur, pas ce qu'elle est. En PDF, un ton
direct est un espace `Separation` qui nomme l'encre, avec un équivalent CMJN ou Lab pour
l'affichage ; la limite à deux tons et l'absence de nom étaient des compromis de 1997.

**La teinte d'un ton direct est un pourcentage d'encre** : une teinte à 60 % s'écrit `0.6 scn` sur
son espace. Le modèle de MyPublisher s'y prête déjà ; seule la base doit pouvoir être un ton nommé.

**Le noir surimprime, le reste défonce.** Règle de prépresse toujours courante : surimpression sur
les objets en noir seul, seuil de corps pour le texte, en sortie CMJN. Publisher la proposait en ton
direct parce qu'il n'avait pas de mode quadrichromie. En 2026, le RIP de l'imprimeur l'applique
souvent lui-même ; on la propose comme un réglage, après avoir demandé ce que l'imprimeur attend.

**Les séparations se vérifient sur épreuve, et se font au RIP.** L'idée de l'épreuve vaut ; séparer
dans l'application ne vaut plus, le PDF composite l'a remplacé. Ghostscript rend les plaques d'un
PDF (`bitcmyk`, `tiffsep`) : c'est l'outil de recette, pas la chaîne de production.

**Format fini, fond perdu et marques vivent hors de la page.** Les boîtes PDF le disent sans
ambiguïté : TrimBox au format fini, BleedBox au format fini plus le fond perdu, MediaBox avec la
marge des marques. Le fond perdu se stocke en points, comme tout le modèle
(`PdfExporter.cs:14-18`), et s'affiche en millimètres. Les marques qui doivent sortir sur toutes les
plaques s'écrivent dans la couleur de repérage (`Separation /All`).

**Livrer ce que l'imprimeur ne peut pas deviner.** La fiche technique de 1997 oubliait les images et
leur résolution ; en 2026, c'est un rapport de contrôle écrit à côté du PDF, et le PDF/X porte la
condition d'impression dans le fichier.

**Contrôler avant d'envoyer** : polices substituées, droits d'incorporation, débordement, résolution
effective des images, couleurs restées en RVB, objets arrêtés au bord. Les seuils de goût relèvent
du conseil, pas du contrôle technique.

**Ne pas composer pour l'imprimante cible.** C'était imposé par des métriques GDI dépendantes du
périphérique. MyPublisher n'y est pas tout à fait : la coupure se décide sur la plus large de deux
mesures, celle d'Avalonia et celle de PDFsharp, et chaque rendu dessine ensuite avec la sienne
(Q13) ; l'export dépend de la mesure d'écran du processus (`MyPublisher/ViewModels/MainWindowViewModel.cs:818-823`,
`Program.cs:83`). La mesure unique est l'objet du dossier D ([`01-moteur-texte.md`](01-moteur-texte.md)).

**Une sortie qui ne dépend d'aucun pilote.** L'idée a gagné : le PDF est cette sortie, et sous Linux
c'est aussi le format natif de CUPS pour imprimer.

**Faire correspondre écran et imprimé par profils.** Profil de sortie ICC, conversion par LittleCMS,
alerte de gamut. Cela heurte l'égalité écran ↔ PDF que le dépôt contrôle : un PDF converti en CMJN,
repassé en RVB par `pdftoppm`, s'écartera de l'écran au-delà de la tolérance, et la surimpression ne
se voit pas dans un rendu RVB sans simulation. Il faut soit une épreuve à l'écran par le même profil,
soit réserver la comparaison écran ↔ PDF à l'export RVB.

## État dans MyPublisher

- **Couleurs** : chaînes `#AARRGGBB` (`MyPublisher.Core/Models/HexColor.cs:5-30`) ; teinte décrite
  plus haut ; PDF en DeviceRGB (MP-01). Les dégradés sont des ombrages RVB.
- **Images** : posées telles quelles (`PdfExporter.cs:722-746`), sans mesure de leur résolution
  effective.
- **Format** : libre (`Document.cs:70`, `:76`), sans format fini distinct ni fond perdu (MP-02).
- **Polices** : incorporées en sous-ensemble ; une collection `.ttc` est remplacée par un substitut
  qui, lui, est incorporé (`MyPublisher.Core/Services/FontconfigFontResolver.cs:198-204`) : la
  conformité PDF/X n'en dépend pas, la fidélité oui, dès aujourd'hui. `fsType` n'est pas contrôlé
  ([`04-polices.md`](04-polices.md)).
- **Reproductibilité** : le contenu est déterministe (P3), le fichier ne l'est pas (MP-03).
- **Contrôles** : débordement (A2) et substitutions (B5, `PdfExporter.cs:115-150`) remontent dans
  la barre d'état.
- **Impression, aperçu** : absents (MP-05).

## Écart

| Sujet | MyPublisher | Écart |
|---|---|---|
| Imprimer | Absent | Le parcours s'arrête au PDF |
| Modèle de couleur | `#AARRGGBB`, teinte à base et pourcentage | Ni CMJN ni ton direct nommé |
| Couleur du PDF | DeviceRGB | Égal à Publisher 98, en dessous de ce qu'attend un imprimeur en 2026 |
| Format fini, fonds perdus | MediaBox seule | Pas de TrimBox ni de BleedBox ; un objet qui dépasse est rogné |
| Marques | Aucune | Ni traits de coupe, ni repères |
| Surimpression | Aucune | PDFsharp sait l'écrire en CMJN |
| Épreuve des plaques | Aucune | Ghostscript le permet |
| Reproductibilité du fichier | 137 octets diffèrent | Pas de comparaison possible à une référence |
| Contrôle et rapport | A2 et B5 | Ni résolution, ni couleurs, ni fond perdu, ni rapport remis avec le PDF |
| Profil de sortie | Aucun | LittleCMS est sur la machine |

## Postes proposés

Codes du tableau unique de [`00-index.md`](00-index.md#postes-proposés). L'ordre suit les documents
réels, qui sont des A4 bureautiques : S1 à S4 servent dès maintenant, S5 à S9 attendent la réponse à
la question 1.

- **S1 — Un PDF identique octet pour octet.** Recette : deux exports à la même `--date` sont
  identiques (`cmp`) ; une autre date diffère ; sabotages : `/ID` laissé au hasard, `CreationDate`
  reprise à l'horloge. À éprouver d'abord : fixer l'étiquette de sous-ensemble et le XMP sans
  réécrire le fichier après coup.
- **S3 — Imprimer par CUPS** (complète B3). Recette : sur une file CUPS qui écrit dans un fichier,
  Ctrl+P produit un PDF dont `pdftoppm` rend les mêmes pages que l'export ; la plage « 2-3 » n'en
  produit que deux. Le poste dit ce que CUPS couvre du livret et du tuilage.
- **S4 — Contrôle avant envoi, et rapport** (avec le vérificateur de
  [`05-modele-document.md`](05-modele-document.md)). Recette : un document de recette (image de
  72 ppp posée à 10 cm, police absente, bloc qui déborde, bloc vide, objet hors page, double espace)
  sort un constat nommé par règle, désignant le bloc par son identifiant ; le rapport donne la
  résolution effective de l'image à 1 ppp ; un document propre n'en sort aucun ; chaque correction
  est un pas d'annulation ; chaque sabotage d'une règle fait tomber la recette.
- **S2 — Respecter les droits d'incorporation** ([`04-polices.md`](04-polices.md)).
- **S5 — Format fini, fonds perdus, traits de coupe.** Recette : `pdfinfo -box` donne une TrimBox
  égale au format du document et une BleedBox plus grande de 3 mm par côté pour 3 mm de fond perdu ;
  un aplat débordant se voit dans la BleedBox et pas au-delà, à l'écran (zone de fond perdu montrée)
  comme au PDF ; aucun trait de coupe dans la BleedBox ; les traits sortent sur chaque plaque
  (`tiffsep`) ; un document de version 5 s'exporte comme avant (contrôle P2).
- **S6 — Couleurs d'impression dans le modèle** : CMJN et ton direct nommé comme base possible de
  toute couleur, et de la teinte. Recette : aller-retour JSON au caractère près ; migration testée
  propriété par propriété ; une teinte à 60 % d'un ton direct s'écrit `0.6 scn` sur son espace
  `Separation`, lu dans le flux.
- **S7 — PDF/X-4 en CMJN par profil**, écrit par la couche basse de PDFsharp, couleurs converties
  avant par LittleCMS. Le poste dit pourquoi il convertit tôt alors que PDF/X-4 admet le RVB géré,
  règle l'espace de fusion du groupe de transparence, l'en-tête de version et la clé
  `GTS_PDFXVersion` du XMP, et garde le noir sur la seule plaque noire (intention qui préserve K).
  Recette : `OutputIntents` `GTS_PDFX` avec le profil incorporé, TrimBox sur chaque page, aucun `rg`
  ni image DeviceRGB, `pdffonts` tout en `emb yes` ; rendu `bitcmyk` d'un aplat #1F3B57 à 1 % près
  de `transicc`, et non de la formule de PS-01 ; un texte noir #000000 ne sort que sur la plaque K ;
  un ton direct a sa plaque nommée sous `tiffsep`.
- **S8 — Surimpression du noir.** Recette : rendu `bitcmyk`, sous un texte noir de 10 pt sur un
  aplat cyan, la plaque cyan n'est pas trouée ; sous un titre de 36 pt avec un seuil de 12 pt, elle
  l'est ; sabotage : surimpression retirée.
- **S9 — Épreuve des plaques** (complète B6). Recette : celle de B6 (`PLAN.md:1746-1748`), plus une
  vue par plaque dont les valeurs coïncident avec le rendu Ghostscript de la recette S8.

## Questions ouvertes

1. Un imprimeur doit-il recevoir ces documents ? Si oui, quel PDF demande-t-il (PDF/X-4, X-1a,
   simple PDF haute définition), avec quel profil, et applique-t-il lui-même la surimpression du
   noir ? La réponse décide si S5 à S9 sont à planifier.
2. Quelle valeur par défaut Publisher 98 donnait-il au seuil de surimpression du texte noir ? Un
   `.pub` préparé en ton direct, lu par libmspub, pourrait la contenir ; la question ne justifie pas
   de désassembler quoi que ce soit.
3. PDFsharp 6.2.1 convertit-il les images RVB en mode CMJN ? Non éprouvé.
4. Les profils ECI et FOGRA peuvent-ils être proposés au téléchargement, ou l'utilisateur doit-il
   les fournir ?
5. Faut-il passer de PDFsharp 6.2.1 à une version plus récente avant ces postes ? L'historique
   officiel ne promet rien pour l'impression.
