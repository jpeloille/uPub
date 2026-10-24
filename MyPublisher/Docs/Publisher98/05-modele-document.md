# 05 — Modèle de document et fonctions de l'application

**Domaine** : point dur n° 2, le modèle de document : objets, pages et arrière-plans, styles,
jeux de couleurs, informations personnelles, champs, vérification, annulation.
**Sources** : tables de chaînes de `pub5int.dll` (217:88, 99, 19, 20, 27, 31 à 34, 101 à 114),
134 dialogues, accélérateurs ; chaînes et imports de `mspub.exe` et de `pubole98.dll` ; aides
`mspub.m14` et `qmspub.hlp` ; `clrscm.reg`, `pubwiz/*.des` ; code de libmspub. Rien n'a été
désassemblé.
**Verdict** : `partiel` — les objets et leurs propriétés communes sont là, et l'annulation a
plus de pas que celle de Publisher 98 ; il manque l'identifiant de bloc, la page maîtresse, les
styles nommés, la story et le champ dans le texte.

Notations : voir [`00-index.md`](00-index.md#notations). Fiches voisines :
[`06-format-pub.md`](06-format-pub.md), [`07-assistants-ressources.md`](07-assistants-ressources.md) ;
côté Publisher 2007, [`../Publisher2007/05-reperes-mise-en-page.md`](../Publisher2007/05-reperes-mise-en-page.md),
[`06-reperes-regles.md`](../Publisher2007/06-reperes-regles.md),
[`09-arriere-plan-page.md`](../Publisher2007/09-arriere-plan-page.md),
[`16-groupement.md`](../Publisher2007/16-groupement.md),
[`19-informations-professionnelles.md`](../Publisher2007/19-informations-professionnelles.md).

## Ce que fait Publisher 98

Chaque page a deux calques : un premier plan qui lui est propre, et un arrière-plan commun qui
tient lieu de page maîtresse. Il y a un arrière-plan, ou deux (gauche et droite) pour les pages
en vis-à-vis. « Ignorer l'arrière-plan » vaut pour une page entière. Les repères de mise en page
vivent sur l'arrière-plan ; les repères de règle posés au premier plan appartiennent à la page.

Les styles sont des styles de paragraphe nommés et plats, avec la mise en forme directe
par-dessus. Le jeu de couleurs a huit emplacements et s'applique par référence. Quatre ensembles
d'informations personnelles sont rangés chez l'utilisateur, et la publication retient celui
qu'elle emploie. Le numéro de page est une marque insérée dans le texte. Un vérificateur de
conception applique des règles sur le modèle.

L'annulation a vingt niveaux, chaque pas porte un libellé tiré d'un catalogue de 150 actions,
et elle couvre le document entier, réglages compris.

## Ce qui a été relevé

### L'annulation

**U1 — Vingt niveaux.** « You can undo up to 20 actions, one at a time ». Publisher 98 n'a
donc pas une annulation à un seul niveau. Preuve : sujets 933, 1568, 2045, concordants.
Commande : `for n in 933 1568 2045; do sujet $n $M14; done`. Confiance : haute.

**U2 — Chaque pas porte un libellé.** *Constat noté incertain.* « &Undo %s » et « &Redo %s »
(217:99.239, 99.242), « Can't &Undo » quand rien n'est annulable ; le `%s` vient de la table
217:88 (+0x18208, 150 chaînes). Ce que la table établit, c'est le libellé des pas ; qu'elle
serve aussi aux menus ou à l'aide n'est pas établi.
Commande : `chaine '99\.23[7-9]|99\.24[0-2]' ; grep -P '^88\.' $STR`. Confiance : haute pour les
libellés.

**U3 — L'annulation couvre les réglages du document** : repères (88.98, 88.82), mise en page
(88.104), jeu de couleurs (88.102), double page (88.89-90), papier spécial (88.110), actions
d'assistant (88.92-97). Commande : `chaine '88\.(8[29]|9[0-8]|10[24]|110)'`. Confiance : haute.

**U4 — On peut agir sans annulation** quand la mémoire manque, et certaines actions ne
s'annulent pas. Preuve : 217:101.1 « Proceed without Undo? », 101.20 ; sujet 1568.
Confiance : haute.

**U5 — Les objets OLE ont leur propre pas.** *Constat noté incertain.* 217:99.240-241 ;
`pubole98.dll` exporte `_MatUndoRedoObject@8` et `_MatNoUndoObject@4`, ce dernier importé par
`mspub.exe`. Commande : `grep -i undo $X/pe/pubole98.dll/exports.txt $X/pe/mspub.exe/imports.txt`.
Confiance : haute pour les noms, moyenne pour l'interprétation.

**U6 — Annuler une synchronisation la coupe durablement**, effet de bord voulu (sujet 1257).
Confiance : haute.

Ces faits ne disent pas comment l'annulation est faite : un libellé par pas, une borne en
nombre et la bascule « sans annulation » se concilient aussi bien avec des commandes inverses
qu'avec des instantanés. Rien ne permet de parler d'un patron Command.

### Les pages

**P1 — Deux calques par page.** « Every page of a publication has a background and a
foreground » ; Ctrl+M passe de l'un à l'autre (commande 93). Preuve : sujets 21 et 47 ;
chaînes 217:99.35-36. Commande : `sujet 47 $M14`. Confiance : haute.

**P2 — Un arrière-plan, ou deux.** *Constat noté incertain.* La case « Create Two Backgrounds
With Mirrored Guides » (dialogue 535) crée un arrière-plan gauche et un droit ; la barre d'état
connaît « Background Page », « Left Background Page », « Right Background Page »
(217:53.5-53.7). Aucun autre mécanisme n'apparaît. Preuve : sujets 11, 53, 56.
Confiance : haute pour la limite, moyenne pour l'absence d'autre mécanisme.

**P3 — Ignorer l'arrière-plan, tout ou rien.** La bascule vaut pour la page entière ; pour
masquer un seul objet, l'aide fait poser un cadre de texte vide et opaque qui le recouvre.
Preuve : 217:99.5, dialogue 550, ID d'aide 1000-1001 ; sujets 13, 15, 2370. Confiance : haute.

**P4 — Passer d'un à deux arrière-plans et retour.** À deux, Publisher copie le droit sur le
gauche ; revenu à un, le droit sert à toutes les pages, et les objets de gauche sont soit
masqués, soit remplacés (217:104.25 et 104.32, que les chaînes ne départagent pas).
Commande : `chaine '101\.8|104\.(25|32)'`. Confiance : moyenne.

**P5 — Le fond imprimé est toujours blanc** : une page colorée se fait par un rectangle envoyé
à l'arrière. Le réglage de couleur de fond n'existe que pour le Web. Preuve : sujets 160, 112,
116, 596. Confiance : haute.

**P6 — Repères communs et repères locaux.** Les repères de règle du premier plan sont propres
à la page (« Unlike the layout guides, they do not repeat on each page ») ; les repères de mise
en page vivent sur l'arrière-plan. Preuve : sujets 195, 510, 511 ; ID d'aide 952, 955.
Confiance : haute.

**P7 — Pas de grille de lignes de base.** Aucune occurrence de « baseline guide » ou
« baseline grid » dans l'aide. Commande : `grep -c -i 'baseline guide\|baseline grid' $M14`.
Confiance : moyenne.

**P8 — Insérer des pages** : nombre, position (avant, après, entre), contenu (vierge, un cadre
de texte par page, copie des objets d'une page). C'est la spécification du poste A8.
Preuve : dialogue 534 ; 217:37.0, 38.0, 39.0. Confiance : haute.

### Les objets

**O1 — Types d'objets** : cadres (texte, tableau, image, WordArt, OLE), objets dessinés (trait,
ovale, rectangle, forme personnalisée), groupe, objet de la Design Gallery, objets Web.
Preuve : 217:99.134-147, 99.203, 99.254, 99.268, 99.272, 99.338-352. Confiance : haute.

**O2 — Propriétés communes** : bordure (filet fin, 1, 2, 4, 8 pt, par côté pour un cadre),
BorderArt, remplissage (teintes, motifs, dégradés), opaque ou transparent (Ctrl+T), ombre
automatique, rotation, retournement, habillage. Preuve : dialogues 520, 522, 524, 525, 576 ;
sujets 48, 379. Confiance : haute.

**O3 — Rotation** de −360 à 360°, par boutons de 5°, comptée depuis l'orientation de création
et en sens anti-horaire. Un objet naît sans rotation : c'est un angle absolu, comme dans
MyPublisher, mais de sens contraire (`MyPublisher.Core/Models/Frame.cs:142`, sens horaire). Toute
correspondance d'angle, à l'import comme dans les fiches, inverse le signe.
Preuve : 217:103.2 ; ID d'aide 220, 221, 222, 225 ; sujet 68. Confiance : haute.

**O4 — L'habillage est une relation** : l'objet qui gêne dit comment (cadre entier, image
seule, contour irrégulier et marges), le cadre de texte dit s'il accepte d'être habillé.
Preuve : dialogues 539 et 541 ; ID d'aide 730-732. Confiance : haute.

**O5 — Dans un groupe**, on peut changer la couleur, la bordure et le texte d'un enfant sans
dissocier ; la profondeur d'imbrication est bornée. Preuve : sujet 534 ; 217:103.26, 108.1.
Confiance : haute.

**O6 — Les identifiants ne sont pas réutilisés** : le compteur de cadres de texte et de
tableaux ne revient pas en arrière « over the life of a publication ». Preuve : 217:114.69-70.
Confiance : moyenne. Le format le confirme ([`06-format-pub.md`](06-format-pub.md)).

### Styles, couleurs, informations, champs

**S1 — Des styles de paragraphe plats.** *Constat noté incertain.* Un style réunit caractère,
retraits et listes, interligne, espacement et taquets, sans « basé sur » ; Normal ne se
supprime pas ; supprimer un style rend ses paragraphes à Normal et garde la mise en forme
directe ; l'écart entre le texte et son style est détecté. Preuve : dialogues 552 et 556 ;
sujets 372, 2328, 2333 ; ID d'aide 750-751. Confiance : haute pour les comportements.

**S2 — 255 styles au plus, noms de 31 caractères au plus** ; la table 217:27 (+0xFAA8) contient
les noms des styles prédéfinis de Word. Commande : `chaine '103\.36|104\.11'`. Confiance : haute.

**C1 — Le jeu de couleurs** a huit emplacements (principale, accents 1 à 4, lien, lien visité,
accent 5) et s'applique par référence. `clrscm.reg` contient 61 jeux. Détail dans
[`07-assistants-ressources.md`](07-assistants-ressources.md). Confiance : moyenne pour le
décodage.

**I1 — Quatre ensembles d'informations personnelles** de huit composants, logo et jeu de
couleurs compris ; ils sont rangés chez l'utilisateur (registre `UserInfo`), et la publication
retient l'ensemble qu'elle emploie. Preuve : 217:65.0-65.3 ; sujet 902 ; `mspub.exe` UTF-16
+0x1F3DF0 et +0x1F3EC8 (`IncludeColorScheme`). Les chaînes voisines `UserID` et
`WebSiteContactInfo` laissent supposer un composant propre au Web. Confiance : moyenne.

**F1 — Le folio est une marque dans le texte.** *Constat noté incertain.* Le numéro de page est
une marque # insérée dans le texte d'un cadre ; le premier numéro est global, sans sections ;
les champs de publipostage s'insèrent au point d'insertion. Le format le montre : le bloc `TOKN`
du flux Quill désigne la position du « # » ([`06-format-pub.md`](06-format-pub.md)).
Preuve : sujets 1044, 2364, 2366, 2369 ; 217:101.10 ; dialogue 521. Confiance : moyenne.

**V1 — Le vérificateur de conception** applique neuf règles à l'imprimé : cadre vide, objet
recouvert, débordement, zone non imprimable, image déformée, plus de cinq effets, double espace,
plus de trois polices, plus de trois couleurs. Le Web en a d'autres. Ses corrections ont leur
propre bouton d'annulation. Preuve : tables 217:31 à 34 ; dialogue 567 (imprimé), 595 (Web).
Commande : `grep -P '^(31|33)\.' $STR`. Confiance : haute.

**G1 — La Design Gallery a une réserve propre à la publication** (« Your Objects »), importable
d'une autre publication. Preuve : sujets 476, 481, 486. Confiance : haute.

### L'import par libmspub

**L1 — L'arrière-plan d'un fichier Publisher 98 est perdu à l'import.** Les fichiers dont le
flux `Contents` commence par `E8 AC 22 00` sont lus par l'analyseur « 2000 », qui écarte les
formes dont la page parente n'est pas une page normale.
Preuve : libmspub `MSPUBDocument.cpp`, aiguillage sur 0x22 ; `MSPUBParser2k.cpp:525` (« ignore
non top level shapes ») et `:539-542`. Commande : `sed -n 535,545p <libmspub>/src/lib/MSPUBParser2k.cpp`.
Confiance : haute pour `cheklist.jsp`, où le pied de page de l'arrière-plan (58 caractères)
n'arrive pas à l'import ([`06-format-pub.md`](06-format-pub.md), C14 et C6) ; moyenne pour la
généralisation à tout arrière-plan. Pour les fichiers récents, le collecteur recopie au contraire les objets de
la maîtresse dans chaque page (`MSPUBCollector.cpp:1790-1800`) et n'émet pas une page sans objets
propres (`:1787-1788`).

## Principes à retenir, et ce qui ne vaut plus en 2026

| Principe | En 2026 | À ne pas reproduire |
|---|---|---|
| **Deux calques par page : premier plan et arrière-plan commun**, édité dans un mode à part | Vaut tel quel (InDesign, Scribus, Publisher depuis 2002) | La limite à deux arrière-plans, le masquage d'un objet par un cadre vide, la copie d'un arrière-plan sur l'autre |
| **L'histoire est séparée des cadres** | Condition du chaînage et de l'habillage | La borne de cadres « sur la vie de la publication » |
| **Styles de paragraphe, puis mise en forme directe**, Normal indélébile, écart détecté | Les deux couches valent ; il faut y ajouter l'héritage et des styles de caractère | Les bornes de 255 styles et 31 caractères |
| **Les couleurs du jeu s'appliquent par référence** | Vaut ; MyPublisher l'a (C5b) | La palette indexée, le fond figé au blanc |
| **Les données personnelles vivent chez l'utilisateur ; le document dit lesquelles** | La séparation vaut | La copie avec synchronisation, que l'annulation coupe ; un champ résolu au rendu fait mieux |
| **L'annulation a plusieurs niveaux, nommés, sur tout le document**, un pas par intention | Le libellé, la couverture et le grain valent | La borne de 20 et « agir sans annulation » ; une annulation qui coupe quelque chose (U6) |
| **Les identifiants ne sont jamais réutilisés** | Vaut ; il faut dire où vit le compteur et ce que l'annulation en fait | La borne finie |
| **Repères communs et repères locaux** | La distinction vaut | — |
| **L'habillage est une relation entre deux objets** | Vaut tel quel | — |
| **Le vérificateur est une liste de règles sur le modèle**, chaque constat avec sa suggestion | Vaut ; c'est le contrôle en amont du PDF/X | Les règles de goût (trois polices, trois couleurs) |

## État dans MyPublisher

- **Pages** : une page porte un nom et ses blocs (`MyPublisher.Core/Models/Document.cs:37-46`) ;
  le document n'a qu'un fond de page (`Document.cs:105-123`, `:130-143`) ; l'export dessine le
  fond puis les blocs (`MyPublisher.Core/Services/PdfExporter.cs:67-84`). Pas de page maîtresse.
  Le commentaire de `Document.cs:109-111` attribue à Publisher un réglage de fond « une fois pour
  toutes » ; en Publisher 98, le fond imprimé est blanc et ce réglage n'existe que pour le Web
  (P5).
- **Repères** : marges (`Document.cs:63-100`), grille (`Document.cs:244-288`) et repères libres
  (`Document.cs:292-297`) sont portés par le document. Le commentaire `Document.cs:20-21` et
  `MyPublisher/Docs/PLAN.md:1840` disent « comme dans Publisher » ; c'est inexact pour
  Publisher 98 (P6), et à vérifier pour Publisher 2007, la référence du dépôt
  (`README.md:488`), d'où vient l'attribution (`MyPublisher/Docs/Publisher2007/06-reperes-regles.md:31-32`).
- **Groupe** : un enfant ne se retouche pas sans dissocier (`MyPublisher.Core/Models/Frame.cs:799`),
  ce qui suit la fiche Publisher 2007 n° 20 ; Publisher 98 permettait la retouche (O5). Choix
  défendable, attribution à vérifier de la même façon.
- **Styles** : le style commun est porté par le bloc (`Frame.cs:515-559`), plus des fragments de
  mise en forme directe (`MyPublisher.Core/Models/StyleSpan.cs:27-72`). Les réglages du bloc sont
  non nullables et toujours écrits (`Frame.cs:515` police, `:519` corps, `:523` gras, `:559`
  interligne ; tableau `Frame.cs:877-882`) : une cascade « style, puis bloc, puis fragment » où le
  bloc l'emporterait toujours ne laisserait aucun effet au style.
- **Texte** : il appartient au bloc (`Frame.cs:481`, `:546`, `:643`).
- **Champs** : un champ remplace tout le texte du bloc (`Frame.cs:588-596` ;
  `MyPublisher.Core/Models/Field.cs:12-30`, `:60-76`).
- **Identifiants** : aucun, un nom libre (`Frame.cs:120`).
- **Annulation** : 60 pas (`MyPublisher/ViewModels/MainWindowViewModel.cs:18`) par instantanés
  JSON du document entier (`MainWindowViewModel.cs:21-26`, capture `:711-716`, restauration
  `:718-737`), rangés dans le ViewModel, sans libellé. La sélection est mémorisée par indices,
  pris avec l'instantané et réappliqués au même JSON : « réordonner puis annuler » rend déjà la
  bonne sélection. Ce n'est pas un défaut ; c'est hors de portée des tests du noyau.
- **Coordonnées** : le jeu actif est un réglage de l'application, et le document n'en retient
  aucun (`MyPublisher.Core/Services/BusinessInfoStore.cs:11-16`, `:30-43` ;
  `MyPublisher.Core/Models/BusinessInfo.cs:17-27`). C'est un choix écrit : un modèle envoyé à un
  collègue affiche les coordonnées du collègue (`BusinessInfo.cs:10-12`).
- **Vérification** : débordement (A2) et polices (B5) à l'export, sans liste de règles unique.
- **Langue** : aucune propriété (voir [`03-cesure.md`](03-cesure.md)).

## Écart

| Sujet | MyPublisher | Écart |
|---|---|---|
| Pages maîtresses | Fond de page unique | Structurel : format, deux rendus, champs, édition, import |
| Pages en vis-à-vis | Marges gauche et droite (`Document.cs:83-101`) | Ni maîtresse double, ni marges intérieure et extérieure, ni vue double |
| Styles nommés | Style du bloc et fragments | Structurel : cascade à fixer, propriétés du bloc à rendre facultatives |
| Story | Texte dans le bloc | Structurel, voir le dossier D |
| Champs | Sur le bloc entier | « Page # sur ## » impossible dans un bloc |
| Identifiants | Nom libre | Préalable des maîtresses, des chaînes, de la bibliothèque, du vérificateur |
| Annulation | Instantanés, sans libellé, dans le ViewModel | Non structurel : à ranger dans le noyau et à nommer |
| Repères de règle | Sur le document | Par page en Publisher 98 |
| Rotation | Sens horaire | Sens anti-horaire en Publisher 98 |
| Ombre d'objet | Absente (A14 ne traite que le caractère) | Propriété commune de Publisher 98 |
| Habillage | Absent | À poser avec le dossier D, des deux côtés de la relation |
| Vérificateur | A2 et B5 | Pas de liste de règles |
| Import de l'arrière-plan | Délégué à libmspub | Risque de perdre l'arrière-plan d'un fichier de 1998 sans le dire (L1), et de dupliquer la maîtresse d'un fichier récent |

## Postes proposés

Codes du tableau unique de [`00-index.md`](00-index.md#postes-proposés).

- **F3 — Un identifiant stable par bloc et par page**, compteur porté par le document, format
  en version 6. Recette : un fichier de version 5 reçoit des identifiants déterministes ;
  aller-retour JSON au caractère près ; une copie reçoit un identifiant neuf, aucun doublon après
  dix duplications ; un identifiant supprimé n'est pas réattribué ; le poste dit où vit le
  compteur et le teste dans la suite « créer, annuler, créer » ; annuler une suppression rend
  l'identifiant d'origine. Sabotage : un compteur recalculé au maximum présent fait tomber le test
  de non-réemploi.
- **F4 — L'historique dans le noyau, un libellé par pas.** Il garde les instantanés. Recette :
  61 transactions laissent 60 pas ; une transaction sans changement n'empile rien ; le libellé du
  sommet est celui de la dernière transaction ; deux transactions imbriquées font un pas au libellé
  de l'extérieure ; l'ouverture vide l'historique ; un script vérifie que chaque commande des
  menus a son libellé au catalogue.
- **F9 — Pages maîtresses.** Recette : huit pages, folio sur la maîtresse : `pdftotext` lit 1 à
  8 et le JSON ne contient le bloc qu'une fois ; la page 3, qui ignore la maîtresse, n'a pas de
  folio ; envoyer un bloc sur la maîtresse puis annuler le rend à sa page sous le même
  identifiant ; un fichier à fond de page donne un PDF identique ; un folio à deux chiffres
  compose pareil à l'écran et au PDF, les deux rendus résolvant les champs avec le même contexte
  de page (`PdfExporter.cs:70`) ; l'import reconnaît les objets de maîtresse que libmspub recopie
  dans chaque page d'un fichier récent.
- **F12 — Pages en vis-à-vis**, par une maîtresse en double page (gauche et droite) affectée
  aux pages, plutôt que par la copie d'un arrière-plan sur l'autre. Recette : marge intérieure
  25 mm et extérieure 15 mm, marge droite à 25 mm en page 2 et gauche en page 3 ; un folio à
  l'extérieur est trouvé par `pdftotext -bbox` à gauche des pages paires et à droite des impaires.
- **F13 — Repères de règle par page.** Recette : un repère posé en page 2 n'aimante pas en
  page 3 ; un repère posé sur la maîtresse aimante partout ; les repères d'un document existant
  migrent sur la maîtresse et la recette C2 garde ses positions aimantées. À défaut, corriger
  l'attribution de `Document.cs:20-21` et de `PLAN.md:1840` après vérification sur Publisher 2007.
- **F10 — Styles de paragraphe nommés.** Les propriétés de bloc deviennent facultatives (absence
  = héritée), ou les réglages de chaque bloc se versent dans un style engendré à la migration.
  Recette : migration sans changer un pixel sur l'exemple, ATA 21 et les Fiches ; changer le corps
  de « Titre » de 18 à 20 change les paragraphes Titre sauf celui qui porte un 14 pt direct ;
  supprimer « Titre » rend Normal et garde le 14 pt ; Normal ne se supprime pas ; un cycle
  « basé sur » est refusé à la lecture ; tout s'annule en un pas. Les styles nommés des documents
  ATR ne peuvent pas servir d'argument : libmspub les fond dans les propriétés effectives
  (`MSPUBCollector.cpp:1165-1170`, `:1547-1550`) avant que MyPublisher les voie.
- **F14 — Champs dans le texte.** Il ne dépend d'aucun autre poste ; s'il coïncide avec F3 ou F10,
  il partage leur montée de format plutôt que d'en ouvrir une. Recette : « Page # sur ## » donne « Page 3 sur 8 » en page 3
  par `pdftotext` ; un ancien bloc à champ de folio donne le même PDF ; la recherche ne trouve pas
  le caractère de marque.
- **F15 — Ombre d'objet.** Recette : bloc ombré, le flux PDF contient le contour deux fois au
  décalage fixé ; un groupe ombré n'ombre pas chaque enfant ; écran et PDF concordent.
- **G1 — Le document retient son jeu de coordonnées** : révision d'un choix écrit, voir
  [`07-assistants-ressources.md`](07-assistants-ressources.md).
- **S4 — Le vérificateur de conception**, avec le rapport remis avec le PDF, voir
  [`08-impression-pdf.md`](08-impression-pdf.md). Ses corrections sont des pas d'annulation
  libellés (F4).
- **D3 — La story séparée du bloc** (dossier D, [`01-moteur-texte.md`](01-moteur-texte.md)).

## Questions ouvertes

1. Revenu à un seul arrière-plan, Publisher 98 masque-t-il les objets de gauche ou les
   remplace-t-il par une copie de droite ? Les chaînes ne disent pas ce qui décide.
2. L'enregistrement vidait-il l'historique ? Un seul sujet le suggère (2289).
3. libmspub perd l'arrière-plan de `cheklist.jsp` (L1) ; reste à savoir si un arrière-plan fait
   d'objets autres que du texte se perd de même, ce qu'un `.pub` à arrière-plan dessiné
   éprouverait. L'import doit au moins avertir : F6 le fait par la génération du fichier, pas par
   l'objet perdu.
4. C7 : bibliothèque de l'utilisateur seulement, ou aussi une réserve dans le document comme
   « Your Objects » ?
5. Faut-il un rôle sur les blocs (titre, folio, logo), pour qu'un changement de modèle reprenne
   le contenu ? Voir le modèle paramétré, [`07-assistants-ressources.md`](07-assistants-ressources.md).
6. Un bloc de texte en miroir (C8) montre-t-il son texte à l'envers ? Le CD ne le tranche pas.
