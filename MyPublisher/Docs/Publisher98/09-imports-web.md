# 09 — Imports de texte et d'images, publication Web

**Domaine** : point dur n° 5, côté échanges : comment Publisher 98 reçoit du texte et des images
venus d'ailleurs, comment il les rend, et comment il publie sur le Web ; ce que MyPublisher en tire
pour l'import DOCX, l'export SVG et image.
**Sources** : `shared/textconv` (neuf convertisseurs et une bibliothèque commune),
`shared/grphflt` (filtres d'images), `shared/quill/qrtf98.dll`, `pubmrg98.dll`, `wdbimp.dll`,
`msencode.dll`, `htmlenc.reg`, `webpost.exe` (listé sans exécution), chaînes de `mspub.exe` et
de `pub5int.dll`, dialogues, aides ; documentation publique.
**Verdict** : `partiel` — MyPublisher importe le texte brut (B2) et le `.pub` (R5), n'exporte que
le PDF, et possède déjà sans le nommer un format pivot.

Notations : voir [`00-index.md`](00-index.md#notations). Fiches voisines :
[`06-format-pub.md`](06-format-pub.md), [`02-typographie.md`](02-typographie.md) ; côté
Publisher 2007, [`../Publisher2007/14-image-fichier.md`](../Publisher2007/14-image-fichier.md) et
[`21-pack-and-go.md`](../Publisher2007/21-pack-and-go.md).

## Ce que fait Publisher 98

Publisher 98 ne lit lui-même aucun format de traitement de texte. Tout texte venu d'ailleurs entre
en RTF : un convertisseur partagé d'Office, trouvé par le registre, produit du RTF ; un composant
séparé du moteur de textes le lit dans une story. Le même chemin sert au presse-papiers, à l'export
du texte et à l'aller-retour avec Word. Les images suivent le même schéma, avec le métafichier
Windows pour pivot. Le publipostage lit ses sources par Jet.

La publication Web prend une « photographie » de la page. Elle a deux cibles : HTML 2.0+ par défaut,
qui reconstruit la page en tableaux calés par des images d'espacement, et HTML 4.0+, où chaque
objet est positionné en CSS. Dans la cible 2.0, tout ce qui n'est pas du texte pur devient une image
GIF ; dans la cible 4.0, aucun cadre de texte ne devient image (sujet 661). Le vérificateur de
conception prévient des pertes principales, avec un seuil de 20 Ko par image.

## Ce qui a été relevé

### Le texte : tout passe par RTF

**C01 — Les convertisseurs rendent du RTF.** Tous exportent `ForeignToRtf32` ; `RtfToForeign32`
(l'écriture) manque à `mswrd832.cnv` (Word 97) et `mswrd632.cnv` (Word 6/95), et figure dans
`wpft532`, `works332`, `works432`, `write32`, `wnwrd232`, `wrd6er32`. Publisher 98 lit donc Word 97
mais n'écrit le Word moderne que par un convertisseur d'export RTF.
Commande : `grep -v '^#' $X/pe/mswrd832.cnv/exports.txt ; grep -v '^#' $X/pe/wpft532.cnv/exports.txt`.
Confiance : haute.

**C02 — `mspub.exe` les cherche par le registre et les appelle par une interface étroite** :
valeurs `Name`, `Path`, `Extensions`, fonctions `InitConverter32`, `IsFormatCorrect32` (reconnaître
un fichier à son contenu), `ForeignToRtf32`, `RtfToForeign32`, `RegisterApp`, et un gabarit de
progression. Preuve : `mspub.exe`, UTF-16 +0x1F5D86 à +0x1F5ED8 ; racines `Text Converters\`
(+0x208C04) et `Graphics Filters\` (+0x208BA0).
Commande : `strings -t x -el -n 3 $CD/mspub.exe | grep -E 'ForeignToRtf32|IsFormatCorrect32|Text Converters'`.
Confiance : haute.

**C03 — L'installation les enregistre et leur impose le silence** : une clé par convertisseur sous
`Shared Tools\Text Converters\Import|Export\<nom>`, des sous-clés `NoDialogs`, et des options
imposées à WordPerfect. Preuve : `pub5.stf` lignes 377 à 462, options de WordPerfect aux lignes
460-462. Commande : `grep -n -a 'Text Converters' $CD/pub5.stf ; sed -n 455,462p $CD/pub5.stf`.
Confiance : haute.

**C04 — Le RTF est lu par un composant COM à part**, `qrtf98.dll` (« Quill RTF Converter », export
`QRtfIO_CreateInstance`), compagnon de `qs98.dll` et auto-enregistré ; aucun binaire ne le nomme.
Preuve : `pub5.stf` lignes 161 et 168. Commande : `grep -n -a -iE 'qrtf|quill' $CD/pub5.stf`.
Confiance : haute.

**C05 — Ce que Quill sait lire du RTF.** *Constat noté incertain.* `qrtf98.dll` porte une table de
761 mots de commande (de +0x15B04 à +0x17646). Elle n'a ni les listes de Word 97 (`listtable`,
`ls`, `ilvl`) ni ses formes (`shp`, `sn`, `sv`), ni les images JPEG et PNG ; elle a l'Unicode (`uc`,
`ud`, `upr`), des ajouts de Word 97 (`animtext`, `accnone`, `embo`, `impr`), et la direction
(`rtlch`, `ltrch`, `rtlpar`, `ltrpar`, `rtlrow`) avec la police associée par écriture (`hich`,
`loch`, `dbch`). C'est un RTF de Word 97 sans listes ni formes, plutôt qu'un RTF de Word 95.
Commande, bornée à la table (88 836 et 95 814 sont les deux décalages en décimal) :
`strings -t x -n 2 $CD/shared/quill/qrtf98.dll | while read o w; do echo "$((16#$o)) $w"; done | awk '$1>=88836 && $1<=95814 && $2 ~ /^[a-z]+$/' | wc -l`
rend 761 ; un filtre sur les seuls préfixes `15` à `17` en rendrait 766, la zone débordant de la
table. Confiance : moyenne.

**C06 — Les formats convertis** : texte brut, RTF, Word 2.0 à 97, Word Mac 4 à 5.1, Works 3 et 4,
Write 3.1, WordPerfect ; « Publisher does not convert HTML ». Preuve : sujet 2105.
Commande : `sujet 2105 $M14`. Confiance : haute.

**C07 — Insérer un fichier texte** insère au point d'insertion et propose de créer les blocs et
pages qui manquent (écoulement automatique) ; aucune borne de taille n'est déclarée. Accepté dans
une case de tableau. Preuve : sujet 2067 ; chaînes 217:28.3 et 104.29. Les bornes de l'écoulement
viennent des cadres : nombre de cadres sur la vie de la publication (217:114.69) et de cadres
chaînés (114.70). Commande : `chaine '28\.3|104\.29|114\.(69|70)'`. Confiance : haute.

**C08 — Les styles importés rejoignent la liste de la publication**, et un conflit de nom se
tranche par une question ; bornes : 255 styles, tableau de 128 × 128, objets non textuels d'une
story Word refusés. Preuve : chaînes 217:104.2, 114.77, 104.11, 106.31, 114.116, 107.12.
Confiance : haute.

**C09 — Le presse-papiers** déclare « Rich Text Format », « Publisher Object », « Publisher Cells »
et les formats OLE ; un collage spécial force le RTF. Preuve : `mspub.exe` UTF-16 +0x1EDE40 ;
sujet 2062. Confiance : haute.

**C10 — Le texte s'exporte**, la sélection ou tout le texte, vers un convertisseur d'export, en RTF
ou en texte brut ; un convertisseur en lecture seule entraîne un message qui propose le RTF.
Preuve : sujets 2322, 2323 ; chaîne 217:106.44. Confiance : haute. L'aller-retour avec Word passe par
la même voie (« Edit Story in Microsoft Word », sujets 1224 et 1282).

### Les images et les données

**C11 — Les filtres d'images suivent l'interface Aldus** (`GetFilterInfo`, `ImportGr`, `ExportGr`,
`RegisterPercentCallback`) et ramènent l'image à un métafichier Windows. Seuls GIF, JPEG et WPG
s'écrivent. Preuve : `$X/pe/*.flt/exports.txt` ; `pub5.stf`, valeurs 467 à 553 ; Dr. Dobb's, juillet
1992. Commande : `grep -n -a 'Graphics Filters' $CD/pub5.stf`. Confiance : moyenne.

**C12 — Le publipostage** lit ses sources par Jet 3.5 (texte, Excel, dBASE, FoxPro, Paradox) ; la
liste d'adresses de Publisher est un `.mdb` ; Works passe par `wdbimp.dll`, réduit à quatre
fonctions (ouvrir, noms de champs, enregistrements, fermer) ; une source Word doit être un tableau ;
255 champs au plus. Preuve : `pubmrg98.dll` +0x20B98, +0x21590, +0x27150 ;
`$X/pe/wdbimp.dll/exports.txt` ; chaînes 217:113.7, 113.14, 114.20. Confiance : haute.

### La publication Web

**C13 — La photographie de la page.** Le texte pur va en HTML, le reste en GIF. Dans la cible 2.0,
un texte devient image s'il a une bordure d'art, un fond ou un motif, s'il est tourné, s'il est dans
une case de tableau, s'il chevauche un objet, ou s'il est dans la région graphique d'autres objets
(le rectangle qui englobe des objets qui se chevauchent). Preuve : sujets 673, 674, 677, 683 à 704 ;
chaînes 217:1.174 et 109.27. Commande : `for n in 673 674 677; do sujet $n $M14; done`.
Confiance : haute.

**C14 — Deux cibles.** HTML 2.0+ par défaut : la page devient une grille de tableau, calée par une
ligne d'images d'un pixel de haut. HTML 4.0+ : chaque objet est un bloc en position absolue, en
pixels. Preuve : `mspub.exe`, gabarits UTF-16 à +0x1FF09C, +0x1FF034, +0x1FF248, +0x1FF2E8 ;
dialogue 714 ; sujets 661, 2441, 2442. Une seconde famille de gabarits se trouve de +0x2030A8 à
+0x203608. Commande : `strings -t x -el -n 5 $CD/mspub.exe | grep -E 'position: absolute'`.
Confiance : haute.

**C15 — Le texte en HTML.** *Constat noté incertain.* En cible 2.0, le texte passe par des balises de
police et de style ; en cible 4.0, une feuille CSS porte une règle par style, en points à une
décimale, avec une propriété propre qui garde le nom du style Publisher. Le crénage, l'approche, les
petites capitales et les effets se perdent, et une tabulation devient une espace, ce qu'une bulle
annonce au moment où cela se produit (217:198.37). Preuve : `mspub.exe` UTF-16 +0x1FFED8 à +0x200788
(propriété de nom de style à +0x200264) ; sujets 2423, 2440.
Commande : `strings -t x -el -n 5 $CD/mspub.exe | grep -c letter-spacing`. Confiance : moyenne.

**C16 — Le vérificateur Web** signale toute image de plus de 20 Ko (la texture de fond a la même
borne), une page qu'aucun lien ne relie à l'accueil, un objet qui déborde sur la zone de travail.
Preuve : sujets 608, 688, 689, 691, 698, 703, 704. Commande : `grep -n '20 kilobytes' $M14`.
Confiance : haute.

**C17 — Les encodages.** *Constat noté incertain.* `htmlenc.reg` associe un libellé à un jeu de
caractères et à une page de code : sept encodages Windows ou ISO, neuf asiatiques et l'UTF-8 par
`msencode.dll`. Publisher se replie sur l'UTF-8 et avertit d'un mélange de jeux de caractères.
Preuve : `htmlenc.reg` lignes 23 à 48 (lignes 39 à 48 pour les asiatiques et l'UTF-8) ;
chaînes 217:114.39, 114.41. Commande : `tr -d '\r' < $CD/htmlenc.reg | cat -n`. Confiance : moyenne.

**C18 — Largeurs et noms.** *Constat noté incertain.* Largeurs de page Web standard (640 px), large
(800 px) et libre ; noms de fichiers limités à A-Z, a-z, 0-9, « - », « _ », « $ », avec INDEX.HTML
par défaut ; les fragments HTML sont gardés sans contrôle, sauf les balises de structure. Preuve :
dialogues 598 et 102 ; chaînes 217:1.107-1.109, 110.3, 114.114, 86.* ; sujets 663, 676.
Confiance : moyenne. Les formulaires Web et la récupération de leurs réponses (chaînes 217:58.*,
60.*, 61.*) sont hors du périmètre de MyPublisher.

**C19 — L'envoi** passe par le Web Publishing Wizard 1.52, dont `webpost.exe` est l'auto-extracteur,
avec quatre fournisseurs (FTP, HTTP, FrontPage, CRS). Commande :
`LD_LIBRARY_PATH=$S/dl/cabextract/root/usr/lib/x86_64-linux-gnu $S/dl/cabextract/root/usr/bin/cabextract -l $CD/webpost.exe`.
Confiance : haute.

### MyPublisher

**C20 — L'import de texte brut (B2).** Décodage pur (UTF-8 strict, UTF-16 à marque, repli
Windows-1252) et borne de 128 Kio sur la **taille du fichier**, refusé avant d'être lu
(`MyPublisher.Core/Services/TexteImporte.cs:59-242`, `:97-104`). Le texte remplace tout le bloc et
repart de son style, fragments et écarts vidés, en un pas d'annulation
(`MyPublisher/ViewModels/MainWindowViewModel.cs:1052-1086`, `:1079-1081`). Confiance : haute.

**C21 — Un pivot existe déjà.** `PubImporter` traduit les appels librevenge, au vocabulaire
d'OpenDocument (`fo:`, `style:`), en fragments et écarts, dans les méthodes privées de sa classe
d'état (`MyPublisher.Core/Services/PubImporter.cs:156`, `:192-239`, `:282`, `:348-380`, `:382-405`,
`:421`). Il n'est pas réutilisable en l'état pour un ODT : `Points` ne lit que des pouces et rend 0
pour « 12pt », « 1.27cm » ou « 150% » (`PubImporter.cs:68-75`) ; le gras n'est vrai que pour la
valeur « bold » (`PubImporter.cs:400`), alors qu'OpenDocument admet aussi 600 et 700 ; l'exposant
lit le premier nombre de `style:text-position`, si bien que « super 58% » donne 0
(`PubImporter.cs:412-419`) ; « start » et « end » retombent au fer à gauche (`PubImporter.cs:357-363`).
Et la fin de texte prend des décisions propres au `.pub` (fer dominant promu au bloc, libération
des références de jeu). Confiance : haute.

**C22 — Le récit de MyPublisher est court** : ni soulignement, ni approche, ni lien, ni feuille de
styles nommés, ni chaînage (`MyPublisher.Core/Models/StyleSpan.cs:27-71`,
`MyPublisher.Core/Models/ParagraphSpan.cs:39-112`, `README.md:437-441`). Les images se limitent à
PNG, JPEG, BMP, GIF et WebP au sélecteur (`MyPublisher/Views/MainWindow.axaml.cs:26-27`), alors que
l'import d'un `.pub` peut écrire des `.tif` (`MyPublisher.Core/Services/DepotImages.cs:99-107`).
L'export est le PDF seul (`MyPublisher.Core/Services/PdfExporter.cs:40-43`). Le `.pub` s'ouvre par
son extension (`MyPublisher.Core/Services/DocumentSerializer.cs:115`). Confiance : haute.

## Principes à retenir, et ce qui ne vaut plus en 2026

**Un seul format pivot entre le monde et le moteur.** Neuf convertisseurs, le presse-papiers,
l'export du texte et l'aller-retour avec Word passent par RTF : le moteur n'a qu'un lecteur et qu'un
écrivain à tenir. Le principe vaut. Le pivot ne doit plus être RTF, figé et pensé en pages de code,
mais un modèle interne de récit alimenté par le vocabulaire d'OpenDocument. Un pivot sérialisé en
fichier se justifie quand il paie l'isolement d'un processus externe, comme `pub2raw` pour R5 ; il ne
se justifie pas entre deux modules d'un même processus.

**Des traducteurs étroits et isolés.** Un convertisseur est une DLL à sept fonctions, indépendante
du moteur. On garde la séparation lecteur / traducteur pur (`PubReader` / `PubImporter`) pour chaque
format ; une table statique dans le noyau remplace le registre.

**Reconnaître un fichier à son contenu.** DOCX et ODT sont tous deux des ZIP : on les distingue par
`[Content_Types].xml` ou le membre `mimetype`, non par l'extension.

**L'application décide, le traducteur ne questionne pas** : tout import se joue sans fenêtre, ce qui
le rend testable.

**Dire ce qui n'est pas passé.** Chaque perte a son message. C'est déjà la doctrine du dépôt
(`DocumentSerializer.cs:108-109`) ; chaque nouvel import la suit, propriétés non portées comptées.

**L'import respecte l'endroit et les styles d'accueil** : insertion au point d'insertion, styles
nommés versés dans la liste. Faute de styles nommés, un import les aplatit en fragments et le dit.

**Le surplus coule dans de nouveaux blocs** : hors de portée tant que MyPublisher ne chaîne pas ; le
débordement reste signalé (A2).

**Un format d'image interne unique.** Le principe vaut ; le WMF et le GIF à 256 couleurs non. L'export
d'images se tire du PDF lui-même, pour qu'il n'y ait qu'un rendu d'export.

**Photographier ce que HTML ne sait pas dire** était un compromis de navigateurs de 1997, que
Publisher 98 évitait déjà dans sa cible 4.0. Le texte mis en image nuit à l'accessibilité et à la
recherche. CSS, les polices incorporées et SVG savent tout dire.

**Deux cibles selon le lecteur** : sans objet en 2026. **Un encodage déclaré** : UTF-8 partout en
sortie ; la détection de B2 reste juste pour les fichiers anciens.

**Une source de données réduite à quatre fonctions** : le contrat de `wdbimp.dll` vaut le jour où un
publipostage sera planifié (CSV, ODS, XLSX).

## État dans MyPublisher

- Import : texte brut (B2, C20) et `.pub` (R5, par `pub2raw`, `MyPublisher.Core/Services/PubReader.cs:28-112`).
- Pivot de fait : C21.
- Récit et images : C22.
- Export : PDF seul ; ni texte, ni PNG, ni SVG.
- Presse-papiers : texte seul.
- Publication Web, publipostage : aucun.

## Écart

| Sujet | MyPublisher | Écart |
|---|---|---|
| Format pivot | Traducteur privé de `PubImporter` | Le pivot existe sans être partagé, et ne lit que des pouces |
| Import de traitement de texte | `.txt` seulement | Il manque ODT et DOCX, les équivalents de RTF et de Word 97 |
| Styles nommés à l'import | Pas de feuille de styles | Les styles d'un document importé ne peuvent qu'être aplatis |
| Tableaux à l'import | B2 refuse les cases | Un ODT ou un DOCX porte des tableaux, que le modèle sait représenter (C9) |
| Polices et couleurs de thème | Jeux de polices et de couleurs (C5b) | Un DOCX porte des polices et couleurs de thème qui ont un équivalent direct dans les jeux |
| Surplus d'un import | Débordement signalé | Dépend du chaînage |
| Insertion au curseur | B2 remplace le bloc | Publisher insère au point d'insertion |
| Coller, copier du texte mis en forme | Texte seul | Ni lecture ni écriture de `text/html` |
| Export du texte | Aucun | Équivalent de « Save All Text As File » |
| Images vectorielles en entrée | Images en points | Ni PDF, ni SVG ; le TIFF écrit par l'import n'est pas au sélecteur |
| Export d'images | PDF seul | Pas de PNG ni de SVG d'une page |
| Publication Web | Aucune | Non prioritaire pour des documents faits pour l'impression |

Pour un DOCX, l'import devra aussi absorber les espaces multiples, les fragments imbriqués, les
taquets nommés (MyPublisher n'a qu'une grille fixe), les listes et puces, les champs (folio, date),
les notes, les césures conditionnelles et les espaces insécables ; et ses polices habituelles
(Calibri, Aptos) sont absentes sous Linux, ce qui renvoie à la substitution (B5,
[`04-polices.md`](04-polices.md)).

## Postes proposés

Codes du tableau unique de [`00-index.md`](00-index.md#postes-proposés).

- **E1 — Le récit importé, une fois pour tous les formats.** Une classe pure du noyau (ouvrir un
  paragraphe, un fragment, du texte, une tabulation, un saut de ligne, avec des clés OpenDocument),
  un analyseur des unités OpenDocument (pt, cm, mm, in, %), une table des valeurs (graisses
  numériques, fers « start » et « end », positions d'exposant), et la séparation entre ce qui est
  traduction et ce qui est politique propre au `.pub`. `PubImporter` l'appelle. Recette : les deux
  `.pub` ATR importés après le poste donnent un `.mypub` identique octet pour octet (contrôle P2,
  fixtures de R5) ; tests purs sur des suites d'appels écrites à la main ; « 12pt », « 1.27cm »,
  « 150% » et « 700 » sont lus ; sabotages : inverser les marges gauche et droite, lire un
  pourcentage comme des points.
- **E2 — Importer un ODT dans le bloc choisi**, reconnu par son membre `mimetype` ; styles nommés
  résolus, parents compris, puis aplatis ; tableaux versés dans un bloc tableau ; ce que le modèle ne
  porte pas est averti et compté. Recette : un `.odt` écrit à la main en XML (gras, italique, deux
  corps, une couleur, un retrait négatif, un paragraphe justifié, un style hérité, un souligné, une
  image) donne le texte au signe près et les fragments et écarts attendus, contrôlés dans le modèle ;
  les avertissements attendus ; l'annulation rend le document d'avant octet pour octet. Sabotages :
  style parent ignoré, pourcentage lu comme des points.
- **E3 — Importer un DOCX, qui rend la même chose que l'ODT.** `w:sz` en demi-points, `w:ind` en
  vingtièmes de point, `w:jc`, `w:color`, `w:vertAlign`, `w:basedOn`, `w:docDefaults`, et les
  polices et couleurs de thème vers les jeux. Recette : la composition de la fixture d'E2, écrite en
  DOCX à la main, donne un bloc identique à celui de l'ODT ; un DOCX réel produit par Word s'importe
  sans exception (aucun n'est dans `MyPublisher/Books` : il faudra en fournir un). Sabotages :
  demi-points lus comme des points, `w:basedOn` ignoré. La voie (lecture XML directe, ou bibliothèque
  sous MIT) est à trancher.
- **E5 — Exporter une page en PNG et en SVG**, tirés du PDF produit par poppler (`pdftocairo`), avec
  la découverte d'outil de `PubReader`. Le SVG dessine le texte en contours, et l'export le dit avant.
  Recette : ATA 21 à 150 ppp donne deux fichiers de 1240 × 1754 px à un pixel près, identiques au
  rendu du même PDF par le même outil ; le SVG a un `viewBox` de 595,3 × 841,9 et se rastérise comme
  le PNG à l'anticrénelage près. La comparaison se fait au PDF dont l'image est tirée, pas à l'écran :
  l'écran et `pdftocairo` diffèrent déjà de 4,28 % et 3,35 % sur une page de texte
  (`MyPublisher/Docs/PLAN.md:3590-3597`).
- **E6 — Le PDF comme image vectorielle**, posé par PDFsharp (`XPdfForm`), avec un aperçu rastérisé
  à l'écran. À éprouver d'abord : que la page posée reste vectorielle et garde ses polices. Recette :
  un PDF d'une page (trait de 0,25 pt et un mot) posé dans un bloc image : dans le PDF exporté, le
  trait est un chemin et le mot se relit par `pdftotext` ; l'aperçu tient dans le bloc à 1 pt près.
- **E7 — Exporter le texte**, en UTF-8, bloc par bloc dans l'ordre des pages. Recette : le texte d'un
  bloc exporté puis réimporté par B2 rend le même texte au signe près, pour les fixtures de B2 et pour
  ATA 21.
- **E4 — Coller du texte mis en forme** : un sous-ensemble de `text/html` (paragraphes, sauts, gras,
  italique, exposant, indice, styles de police, de corps, de couleur, de fer, de marges, de retrait)
  traduit en propriétés OpenDocument pour E1, avec repli sur le texte brut ; la copie vers l'extérieur
  offre `text/html` et `text/plain`. Recette : des fragments HTML fixés, dont un copié de LibreOffice,
  donnent les fragments et écarts attendus ; un `<script>` ou une image n'injectent que le texte, avec
  un avertissement.
- **E8 — Works et WordPerfect, à la demande**, par `wps2raw` et `wpd2raw` (famille librevenge), dont
  la sortie a la forme de celle de `pub2raw`, extraits sans installation comme `Scripts/outils-pub.sh`.
  À ouvrir seulement si une archive réelle le demande.

**Ce qui n'est pas proposé.** L'export HTML : les documents ATR sont faits pour l'impression. S'il
vient, ce sera une page HTML par page, chaque bloc en position absolue, chaque ligne composée posée
telle que la composition l'a coupée, les polices incorporées et les formes en SVG : le texte reste du
texte. Le publipostage : aucun poste du plan ne le porte.

## Questions ouvertes

1. Le modèle aura-t-il une feuille de styles nommés ? Sans elle, E2 et E3 aplatissent les styles
   (voir [`05-modele-document.md`](05-modele-document.md), F10).
2. L'import doit-il insérer au curseur plutôt que remplacer le bloc ?
3. La borne de 128 Kio de B2, qui porte sur la taille du fichier, doit-elle valoir pour un DOCX ? Il
   vaudrait mieux la mesurer sur le texte extrait, puisque sa raison est le temps de rendu.
4. E5 et E6 : `pdftocairo` devient-il une dépendance d'exécution, comme `pub2raw` ?
5. Comment Publisher 98 décode-t-il un `.txt` (page de code ANSI du poste, chaîne 217:9.1) ? Le
   relevé ne l'établit pas, et la comparaison avec B2 reste donc sans source.
