# 23 — Les modalités de saisie

**Source** : *Microsoft Publisher 2007 Student Guide*, relu en entier le 25 octobre 2026 sous
l'angle de la saisie — captures à environ 100 dpi, texte courant par `pdftotext` —, et, pour les
touches, l'aide de Microsoft en français, *Raccourcis clavier dans Publisher*, consultée le même
jour.
**Verdict** : `mixte` — 158 modalités : 31 faites, 12 où MyPublisher fait mieux, 55 partielles,
60 manquantes, dont 4 que le plan écartait déjà.

## Pourquoi une fiche de plus

Les fiches 01 à 22 recensent des **fonctions** : ce que Publisher sait faire. Celle-ci recense
des **gestes** : comment la main fait entrer quelque chose dans le document — taper et
sélectionner du texte, mettre en forme une sélection, cliquer, glisser sous un modificateur,
frapper une touche, ouvrir un menu, remplir un champ.

Le changement d'angle renverse un verdict. La fiche 10 et le point 1 de l'index rangeaient
l'édition du texte parmi les acquis, « déjà meilleure que celle du modèle ». Pour la saisie, c'est
l'inverse : pendant l'édition, l'écran montre un champ en texte brut à la place du texte composé ;
on y entre au double-clic, et tout le texte est alors sélectionné ; la mise en forme ne vise que le
bloc entier. Le geste de **poser** un bloc reste meilleur que celui de Publisher ; celui de
**l'écrire**, non.

Le livre ne montre presque jamais la frappe elle-même : les pages 33 à 38 tracent une zone de
texte, y collent un texte, puis « surlignent » un passage pour le mettre en forme. Le reste —
où tombe le curseur au clic, Tab, Maj+Entrée, Ctrl+glisser — vient de Publisher et d'Office tels
qu'ils sont connus, et chaque ligne le dit.

Les écarts sont rangés en postes dans la phase N du plan ([`../PLAN.md`](../PLAN.md)) :
*La saisie de Publisher*, ouverte le 25 octobre 2026.

## Comment lire les tableaux

- « p. N » renvoie à une page du livre. **HR**, hors relevé, signale une connaissance de
  Publisher ou d'Office que le livre ne montre pas, à vérifier sur un Publisher 2007 français.
- L'état de MyPublisher est l'un de quatre mots : **fait**, **partiel**, **manquant**,
  **meilleur**. « (déduit) » signale ce qui est lu dans le code sans test ni essai à l'écran.
- La colonne *Poste* donne le poste du plan qui couvre la modalité ; « — » quand il n'y a rien à
  faire.
- Les références au code valent pour le commit `82a99ed` du 25 octobre 2026, branche
  `interface`. Abréviations : `PC` `Views/PageCanvas.axaml.cs`, `PCx` `Views/PageCanvas.axaml`,
  `MW` `Views/MainWindow.axaml`, `MWcs` `Views/MainWindow.axaml.cs`, `BC`
  `Views/BarreContextuelle.axaml`, `BCcs` son code, `VM` `ViewModels/MainWindowViewModel.cs`,
  `VMD` `ViewModels/MainWindowViewModel.Disposition.cs`, `DC`
  `Converters/DispositionConverters.cs`. `Av:` renvoie au code d'Avalonia 11.3.9, lu décompilé.

## (a) Saisie et édition du texte

| # | Publisher 2007 (source) | MyPublisher | Poste |
|---|---|---|---|
| a1 | Armer l'outil Text Box, glisser sur la page, curseur en croix (p. 33) | **meilleur** : outils `T` et `H`, bulle L × H ; un clic sans glisser pose la taille par défaut ; l'outil se désarme (PC:507-519, VM:978-1002) | — |
| a2 | Insert ▸ Text Box (p. 22, 39), qui arme l'outil (HR, à vérifier) | **partiel** : Insertion ▸ Bloc de texte pose 260 × 90 pt au point d'insertion suivant, sans ouvrir la saisie (MW:595 ; VM:951-972, 1127-1136) | N12 |
| a3 | Zone neuve vide, point d'insertion déjà dedans, on tape aussitôt (p. 33 : barre Formatting active ; la frappe est HR, sûre) | **partiel** : la saisie s'ouvre après le tracé (PC:760), mais sur « Saisissez votre texte. » ou « Titre », tout sélectionné (VM:889, 896 ; PC:1113). La première frappe remplace ce texte ; un clic ou une sortie sans frappe le garde | N12 |
| a4 | Un clic simple dans le texte pose le point d'insertion à l'endroit visé, sans mode à part (HR, sûr ; le livre dit seulement « Highlight text », p. 38, 53, 59) | **manquant** : double-clic seulement (PC:1077-1083), puis tout le texte est sélectionné (PC:1113) ; ni Entrée ni F2 n'ouvrent la saisie (MWcs:539-613) | N12 |
| a5 | Curseur en I sur le texte, quatre flèches sur le bord (HR ; p. 33 ne montre que ⇔ sur les poignées) | **manquant** : `SizeAll` sur toute la surface du bloc (PC:796) | N12 |
| a6 | Texte d'un groupe surligné puis retapé sans dissocier (p. 59-60) | **manquant** : la sélection ne voit que le premier niveau (PC:558) et la saisie exige un `TextFrame` seul (PC:1082) ; il faut dissocier (README:259-261) | N26 |
| a7 | Échap quitte le texte, le bloc reste sélectionné (HR, sûr) | **fait** (PC:1144-1151) ; un second Échap désélectionne (MWcs:603-609) | — |
| a8 | Un clic sur un autre objet quitte le texte et sélectionne cet objet (HR) | **partiel** (déduit) : le clic ferme la saisie (PC:1128-1142) mais n'atteint pas la couche de saisie, retirée du test de survol (PC:1103 ; PCx:554). Rien n'est sélectionné, il faut recliquer. Un clic sur une règle, lui, pose bien son repère | N5 |
| a9 | Poignées et point vert visibles autour d'un texte surligné (p. 53, 59) ; actifs (HR) | **partiel** (déduit) : dessinés, car `RefreshOverlay` ne regarde pas `_editing` (PC:334-386), mais inertes (PC:1103) | N5 |
| a10 | Sélectionner en glissant, Maj+clic, double-clic = un mot (HR) | **fait** par le TextBox natif (Av:TextBox.cs:1786-1826), mais sur ses lignes et non sur les lignes composées | N12 (le geste), N21 (sur les lignes composées) |
| a11 | Triple-clic = un paragraphe (HR) | **manquant** : le TextBox sélectionne tout (Av:TextBox.cs:1829-1832) | N21 |
| a12 | Flèches, Ctrl/Maj+flèches, Début/Fin, Pg préc./suiv., Ctrl+Retour arrière, Ctrl+Suppr (HR) | **fait** (natif, Av:TextBox.cs:1533-1719) | — |
| a13 | Ctrl+A sélectionne tout l'article, blocs chaînés compris (HR ; Publisher98/01-moteur-texte.md:132) | **manquant** : les liaisons de la fenêtre passent avant le contrôle (Av:KeyboardDevice.cs:164-194) et `SelectAll` n'a pas de condition (VM:598-608, MW:51). Tous les blocs sont sélectionnés et la saisie se ferme. Même chose dans les champs Contenu, Nom et numériques du dock | N6 |
| a14 | Annuler pendant la frappe sans quitter le texte (HR) | **partiel, avec un défaut** : Ctrl+Z referme la frappe en un pas puis l'annule, et la saisie se ferme (VM:712-726). **Avec un historique vide, la transaction se referme mais la saisie reste ouverte : la frappe suivante n'est dans aucun pas d'annulation et ne marque pas le document « modifié »** (VM:704-715 ; `EndTextEdit` appelle `CommitEdit` à profondeur 0, PC:1117-1126, VM:676-680). Confirmé à la lecture | Q24, Q25 |
| a15 | Entrée = paragraphe, Maj+Entrée = saut de ligne (HR ; Publisher98/02-typographie.md:123-127) | **partiel** : Entrée, Maj+Entrée et Ctrl+Entrée insèrent toutes « \n » (Av:TextBox.cs:1720-1727 ; Compositeur.cs:382-392) ; l'import change le saut forcé en paragraphe (PubImporter.cs:228) | N16 |
| a16 | Tab insère une tabulation (HR) | **manquant** : `AcceptsTab` n'est pas posé (PCx:279-302) ; Tab passe à la navigation du focus et la saisie reste ouverte, orpheline (déduit) | N16 |
| a17 | Ctrl+C/X/V ; Cut, Copy, Paste au clic droit (p. 34) ; « Copy and paste text into the text box » (p. 38, 50) | **partiel** : texte brut par le TextBox (Av:TextBox.cs:1409-1480) ; le texte collé prend le style de l'ancre (Recalage.cs:189-198). Au clic droit, le menu du thème Fluent (`DefaultTextBoxContextFlyout`, présent dans Avalonia.Themes.Fluent 11.3.9 ; `FluentTheme` chargé en App.axaml:20 ; pas vu à l'écran) | N8, A12 ; le collage mis en forme reste à E4 de l'étude de 98, non planifié |
| a18 | Coller du texte hors saisie crée un bloc (HR, à vérifier) | **manquant** : aucun presse-papiers hors TextBox (aucun `Clipboard` dans le dépôt) | N8 |
| a19 | Surligner puis taper par-dessus ; le texte tapé prend le format du premier caractère remplacé (p. 48, 53, 59 ; la règle est HR) | **fait** : natif, plus le recalage (`Recalage.Ancre`, Recalage.cs:189-198) | Q1 (livré) |
| a20 | Sélection en vidéo inverse sur le texte tel qu'il s'imprime (p. 53, 59, 61) | **partiel** : surlignage du TextBox, sur du texte brut (PCx:260-302) | N21 |
| a21 | Ce qu'on édite est ce qui s'imprime ; la barre affiche « Body Text 3 / Georgia / 10.1 » (p. 53) | **manquant** : le TextBox remplace `TexteCompose`. Il perd les fragments, les écarts de paragraphe, les retraits, l'interligne (1,25 sur un bloc neuf, non lié : Frame.cs:590-592), les colonnes, les taquets et la coupure du noyau | N7, puis N21 |
| a22 | Débordement signalé ; le texte coule d'un bloc chaîné au suivant (p. 36, 53) | **partiel** : le marqueur de A2 et « Texte en excédent » (I9) suivent la frappe ; pas de chaînage | seuil D |
| a23 | Insert ▸ Text File… (p. 22, 39) | **fait** : Texte ▸ Importer un texte… (MW:663-664) et dock | B2 (livré) |
| a24 | Insert ▸ Symbol…, actif seulement avec un point d'insertion (grisé p. 22, actif p. 39) | **manquant — écarté** | écarté |
| a25 | Champs dans le texte : Page Numbers…, Date and Time… (p. 22) | **partiel** : un bloc est soit du texte, soit un champ ; un bloc à champ refuse la saisie (PC:1094-1098) | non planifié (F14 de l'étude de 98) |
| a26 | Proofing Tools ▸, Look Up… (p. 34) | **manquant** | N25 ; Look Up écarté |
| a27 | Caractères spéciaux : View ▸ Special Characters, `Ctrl+Maj+Y` (p. 9), bouton ¶ (p. 24) | **manquant** | N22 |
| a28 | — (sans équivalent) | **meilleur** : saisie dans le champ Contenu du dock, recalée comme sur la page (MW:1201-1203) | — |

## (b) Mise en forme d'une sélection de texte et du paragraphe

| # | Publisher 2007 (source) | MyPublisher | Poste |
|---|---|---|---|
| b1 | Surligner, puis cliquer un outil de la barre Formatting (p. 38) | **manquant** : la barre, le menu Texte et les raccourcis agissent sur le bloc entier (VMD:257-264 ; README:543-546) | R7 |
| b2 | Ctrl+B, Ctrl+I, Ctrl+U (p. 10) | **partiel** : Ctrl+G, Ctrl+I, Ctrl+U sur le bloc (MW:70-74 ; VMD:243-250 ; Souligne.cs:44-63) ; Ctrl+B n'est lié à rien | N10 (les touches), R7 (la portée) |
| b3 | Souligné et ses sortes, au dialogue Font (HR) | **meilleur** à l'échelle du bloc : bouton S et chevron (double, épais, mots seuls) (BC:159-186) | I3, I7 (livrés) ; R7 (la portée) |
| b4 | Listes Font et Font Size et boutons qui reflètent le texte courant, vides sans texte (p. 33, 34, 36 → 38, 53) | **partiel** : remplis et cochés d'après le bloc (BC:128-149, 150-210) ; ni état de la sélection, ni état mixte | R7 |
| b5 | Corps décimal (9.2 p. 33, 10.1 p. 53, 5.9 p. 59) | **fait** : format « 0.## pt », pas de 0,5, minimum 1 (DC:85 ; BC:147-149) | — |
| b6 | Liste de tailles ; nom de police tapé dans la liste (p. 10 ; Size ▾ p. 48) | **manquant** : le corps n'a pas de liste ; la liste des polices n'est pas éditable (aucun `IsEditable`) | N19 |
| b7 | Ctrl+< et Ctrl+> (p. 10, lecture « petit ») ; Ctrl+] et Ctrl+[ pour ±1 pt (HR) | **manquant** | N10 |
| b8 | Ctrl+L, E, R, J (p. 10) | **partiel** : quatre alignements à la barre et au menu, sans raccourci (MW:652-659 ; BC:191-210) ; **Ctrl+E exporte en PDF**, y compris pendant une saisie (MWcs:554, traité avant le garde de la ligne 559) | N10 (les touches), R6 (la portée) |
| b9 | Alignement, retraits et espacement du paragraphe du curseur ou de la sélection (HR, implicite p. 10) | **manquant** : le bloc entier (MW:1119-1174 ; README:547-549) ; la composition sait déjà lire les écarts (Compositeur.cs:161-162, 217-229) | R6 |
| b10 | Line Spacing…, boutons Single, 1.5 et Double (p. 10) | **partiel** : multiple « 1,15 × », pas de 0,05 (BC:216-218) ; ni préréglages, ni points | N10, si la fiche 24 donne les préréglages ; l'interligne exact reste à T9 de l'étude de 98 |
| b11 | Decrease Indent, Increase Indent (p. 10) | **partiel** : retraits en mm au dock, pour le bloc entier (MW:1137-1159) ; pas de bouton ± | R6 |
| b12 | Puces, numérotation (p. 10 ; Format ▸ Bullets and Numbering…, p. 24) | **manquant** ; les listes importées tiennent par un retrait négatif | N23 |
| b13 | Couleur de police (p. 10, 33) | **partiel** : couleur du bloc, sélecteur du jeu (BC:222-237) | R7 |
| b14 | Small Caps, All Caps, Superscript, Subscript (p. 10, absents de la barre par défaut) | **manquant** : `BaselineShift` n'entre que par l'import | N24 |
| b15 | Outline, Emboss, Engrave ; Shadow Style, 3-D Style (p. 10) | **manquant** | A14 (contour et ombre) ; le reste écarté |
| b16 | Style nommé : liste Style, Styles… (p. 10, 24), « Body Text 3 » (p. 53) | **manquant** : non planifié (PLAN, phase I) | non planifié |
| b17 | Format ▸ Font…, Character Spacing…, Paragraph…, Tabs…, Drop Cap…, Horizontal Rules…, grisés sans texte (p. 24) | **partiel** : ce qui existe est à la barre et au dock, pour le bloc ; ni approche, ni crénage, ni échelle, ni taquets posés, ni lettrine, ni filets | N18 (les entrées vers le dock) ; approche, taquets posés, lettrine et filets non planifiés |
| b18 | Commandes de texte grisées faute de texte sous le curseur (p. 24 ; Symbol p. 39 et 41 ; Delete Text p. 34 et 36) | **partiel** : actives dès qu'un bloc portant du texte est sélectionné (`PorteDuTexte`, VMD:234), ce qui est cohérent avec la portée au bloc | R7 |
| b19 | Ctrl+Espace ; reproduire la mise en forme (HR, à vérifier) | **manquant** | R7 |
| b20 | AutoFit : Best Fit, Shrink, Do not autofit (p. 24, 37 ; le corps passe de 14 à 9) | **manquant** | seuil D |
| b21 | Alignement vertical et marges de la zone (p. 37) ; colonnes de la zone (HR) | **fait** au dock, en mm (MW:1049-1098) | A7, C6, R3 (livrés) |

## (c) Gestes sur les objets, à la souris

| # | Publisher 2007 (source) | MyPublisher | Poste |
|---|---|---|---|
| c1 | Un clic simple sélectionne (p. 44) | **fait** (PC:571-572) | — |
| c2 | Ctrl+clic ajoute à la sélection (p. 46) | **fait** ; Maj+clic aussi ; le clic bascule le bloc dans ou hors de la sélection (PC:559-571 ; VM:505-510) | — |
| c3 | Un clic droit sélectionne et ouvre le menu (p. 34) | **manquant** : seul le bouton gauche est lu (PC:497 ; règles, PC:828) | A12 |
| c4 | Glisser déplace, avec aimantation (HR ; Snap ▸ p. 16) | **fait** : l'emprise s'aimante aux repères, marges et grille ; Alt coupe ; rien si un bloc est tourné (PC:919-963) | — (A11 pour le reste) |
| c5 | Un clic qui tremble reste un clic (HR) | **manquant** : seule existe la garde du mouvement nul (PC:930-939) ; la liste des blocs a un seuil de 4 px (MWcs:380-471) | Q21 |
| c6 | Huit poignées, curseur ⇔ (p. 33) ; « diagonal handles » (p. 40, 50) | **fait** (PC:521-542, 975-1034 ; curseurs PC:89-95) | — |
| c7 | Une pression sans mouvement sur une poignée ne fait rien (HR) | **fait, probablement, depuis I4** : le redimensionnement suit le chemin du pointeur (PC:984-989) ; un chemin nul rend le cadre inchangé et `CommitEdit` n'empile rien (VM:676-697). Un arrondi flottant reste possible sur un bloc tourné | Q22 |
| c8 | Point vert : rotation, curseur en flèche circulaire (p. 33, 50) ; Maj = pas de 15° (HR) | **partiel** : la rotation et Maj sont faits (PC:1048-1073 ; A1) ; le curseur est une main (PC:106) | N17 (le curseur) |
| c9 | Maj sur un coin garde les proportions (HR) | **meilleur** : Maj inverse la case « Conserver les proportions » (PC:659-663 ; I6) | — |
| c10 | Ctrl sur une poignée redimensionne depuis le centre (HR) | **manquant** | N17 |
| c11 | Ctrl+glisser copie (HR) | **manquant** : Ctrl bascule la sélection puis déplace (PC:559-582) ; la seule copie est Ctrl+D, décalée de 12 pt (VM:1172-1194) | N17 |
| c12 | Maj+glisser contraint à l'horizontale ou à la verticale (HR) | **manquant** : `ApplyMove` ne lit qu'Alt (PC:944) | N17 |
| c13 | Maj au tracé : carré ou cercle ; Ctrl : depuis le centre (HR) | **manquant** pour les cadres (PC:714-761) ; **fait** pour le trait, que Maj tient à 0, 45 ou 90° (PageCanvas.Trait.cs:110, 154) | N17 |
| c14 | Rectangle de sélection tiré dans le vide (HR) | **manquant** : la pression dans le vide vide la sélection (PC:562-569) | N17 |
| c15 | Un clic sur un membre d'un groupe le prend seul (p. 58 : « If you wish to change the graphic, click it ») | **manquant** : le groupe entier est pris (PC:558) | N26 |
| c16 | Double-clic sur un objet qui n'est pas du texte : son dialogue (HR ; WordArt : Edit Text, HR) | **manquant** : rien (PC:1082) | A12 |
| c17 | Boutons Group et Ungroup sous la sélection (p. 41, 46) | **fait**, autrement : Grouper à la barre principale (MW:749-750), Dissocier à la barre contextuelle (BC:300) | C3, I3 (livrés) |
| c18 | Glisser depuis une règle pose un repère, sans modificateur (p. 20) | **fait**, et le geste rallume les repères (PC:825-852) | C2 (livré) |
| c19 | Repère : curseur ⇳, glisser (p. 20) | **meilleur** : aimanté aux autres repères sauf sous Alt ; retiré s'il est lâché hors de la page (PC:546-552, 872-915) | C2, I4 (livrés) |
| c20 | Objets posés sur le gris, disponibles depuis toutes les pages (p. 21, 23) | **manquant — écarté** (le plan renvoie à C7) ; blocs et outils s'arrêtent au bord de la page (PC:490-492, 513) | écarté (C7) |
| c21 | Copier-coller d'objets (p. 21 « copy it to the sidebar », p. 23) | **manquant** | N8 |
| c22 | Aimantation aux autres objets, réglable (HR ; Snap ▸ p. 16, 44) | **partiel** : aux repères seulement, au déplacement et aux extrémités de trait ; ni au redimensionnement (PC:975-1034), ni au tracé | A11 |
| c23 | Ctrl+molette (HR, à vérifier) | **fait** : ×1,1 autour du pointeur (PageCanvas.Navigation.cs:36, 171-177) | I6 (livré) |
| c24 | — (sans équivalent relevé) | **meilleur** : loupe, main, Espace tenu (I6) | — |
| c25 | Balise ⓘ du bloc de coordonnées : Edit…, Save to…, Update from…, Convert to Plain Text (p. 54) | **manquant** | A12 |
| c26 | Clic sur l'indicateur de débordement, curseur en cruche de chaînage (HR) | **manquant** | seuil D |
| c27 | Rognage aux poignées (HR ; barre Picture, p. 43) | **manquant** | A13 |
| c28 | — (sans équivalent) | **meilleur** : réordonner en glissant dans « Blocs de la page » (MWcs:380-471) | A4 (livré), Q11 |

## (d) Clavier, hors du texte

| # | Publisher 2007 (source) | MyPublisher | Poste |
|---|---|---|---|
| d1 | Flèches : déplacer ; Arrange ▸ Nudge ▸ (p. 16) ; pas réglable (HR) | **partiel** : 3 pt ; Maj donne 0,5 pt, un pas fin contraire à l'usage courant ; Ctrl et Alt ignorés ; pas d'aimantation ; un pas d'annulation par appui (MWcs:591-602 ; VM:262-275) | — (Pousser ▸ écarté) |
| d2 | Alt+flèches : un pixel (HR) | **manquant** : Alt+flèche avance de 3 pt | N6 |
| d3 | Tab et Maj+Tab : objet suivant ou précédent (HR) | **manquant** : c'est la navigation du focus d'Avalonia (aucun `TabIndex` ni `KeyboardNavigation`) | N13 |
| d4 | Suppr supprime (HR) | **fait** ; Retour arrière supprime aussi (MWcs:595-598), ce que le tableau du README ne dit pas (README:331-357) | — (N10 dit Retour arrière au README) |
| d5 | Échap désélectionne (HR) | **fait** : il désarme d'abord l'outil (MWcs:603-609) | — |
| d6 | Ctrl+A : tous les objets (HR) | **fait**, blocs visibles seulement (VM:598-608) | — (N6 en saisie) |
| d7 | Ctrl+C, X, V sur les objets (HR ; p. 21, 23) | **manquant** | N8 |
| d8 | Ctrl+Maj+G groupe, et la même touche dissocie (p. 16, 44, 46) | **partiel** : Ctrl+Maj+G groupe, Ctrl+Maj+H dissocie (MW:52-53) | N10 |
| d9 | Alt+F6, Alt+Maj+F6 (p. 44) | **partiel, à dessein** : Ctrl+Maj+F et Ctrl+Maj+B, parce qu'Alt+F6 est pris par les gestionnaires de fenêtres (MW:54-61) | A4 (livré), exception écrite |
| d10 | F9 zoome (p. 36 : « To zoom in, click the F9 key ») ; bascule entre 100 % et la vue d'avant (HR) | **manquant** | N4 |
| d11 | Page entière au clavier (HR : Ctrl+Maj+L, à vérifier) | **partiel** : bouton sans raccourci (MW:763-764) | N4 |
| d12 | Ctrl+F1 : volet Office (p. 9, 14) | **fait** : replier les panneaux (MW:77) | B7 (livré) |
| d13 | Ctrl+Maj+O pour les repères, Ctrl+F7 pour les lignes de base : deux bascules (p. 9, 15) | **partiel** : une seule bascule « Repères », sans raccourci (MW:674) | N4 |
| d14 | Ctrl+Maj+Y : caractères spéciaux (p. 9) | **manquant** (voir a27) | N22 |
| d15 | Ctrl+M : page maître (p. 9) | **manquant** : les pages maîtres ne sont pas planifiées | non planifié (pages maîtres) |
| d16 | Ctrl+Maj+N insère une page, Ctrl+Maj+U la duplique (p. 22, 39) | **manquant** : le menu Page n'a pas de raccourci (MW:667-671) | N18 |
| d17 | Ctrl+N, O, S (p. 61) | **fait**, actifs même pendant une saisie (MWcs:547-556) | — |
| d18 | Ctrl+P (p. 61) | **manquant** | B3 |
| d19 | Ctrl+F4 ferme, Alt+F4 quitte (p. 61) | **partiel** : Quitter n'a ni raccourci ni confirmation (MW:578) ; Alt+F4 ferme la fenêtre sans rien demander (aucun gestionnaire `Closing`) | N11 |
| d20 | Ctrl+Z, Ctrl+Y (HR) | **fait** (MW:47-49) | — (N10 ajoute Alt+Retour arrière et F4) |
| d21 | — (chez Publisher : à vérifier) | **meilleur** hors saisie : Ctrl+D copie à +12 pt (VM:1172-1194). **Pendant une saisie**, il duplique le bloc, ferme la saisie et fond la frappe et la copie en un seul pas (déduit de VM:1172-1194 et PC:272-277) | N6 |
| d22 | — (HR : taper sur un bloc de texte sélectionné écrirait dedans, à vérifier) | **meilleur** (invention de MyPublisher) : lettres seules V, T, H, R, E, L, B, Z et Espace tenu, hors saisie (MWcs:565-589) | — |
| d23 | Alt ou F10, puis la lettre soulignée ; le livre souligne partout (A**r**range, F**o**rmat, Pac**k**…) | **partiel** : lettres d'accès posées (MW:569-693), mais en double. Disposition : G ×4, D ×2, H ×2, L ×2. Insertion : T ×2. Texte : G ×2. Affichage : R ×3 | N13 |
| d24 | Ctrl+K (lien), Ctrl+Maj+E (bibliothèque) (p. 22, 39) | **manquant — écarté** pour le lien ; la bibliothèque relève de C7 | écarté (le lien) ; C7 (la bibliothèque) |

## (e) Menus et menu contextuel

| # | Publisher 2007 (source) | MyPublisher | Poste |
|---|---|---|---|
| e1 | Menu contextuel d'un bloc : Cut, Copy, Paste, Delete Text (grisé si le bloc est vide), Delete Object, Order ▸, Format Text Box…, Zoom ▸, Proofing Tools ▸, Change Text ▸… (p. 34, 36) | **manquant** : aucun `ContextMenu` ni `ContextFlyout` dans le dépôt | A12 |
| e2 | Menu contextuel dans le texte (HR) | **partiel** : celui du thème Fluent (Couper, Copier, Coller) | A12 |
| e3 | Menus File, Edit, View, Insert, Format, Tools, Table, Arrange, Window, Help (p. 3) | **partiel** : Fichier, Édition, Insertion, Disposition, Texte, Page, Affichage, Fenêtre (MW:569-694) | — (la maquette fait foi) ; N13 range les menus |
| e4 | Arrange ▸ Order ▸ (p. 44) | **fait**, mis à plat dans Disposition (MW:610-615) | N13 |
| e5 | Arrange ▸ Rotate or Flip ▸ (p. 16) | **manquant** | N18 (A9), C8 |
| e6 | Arrange ▸ Nudge ▸, Snap ▸ (p. 16) | **manquant** au menu | A11 (Snap) ; Pousser écarté |
| e7 | Arrange ▸ Align or Distribute ▸ (p. 16) | **meilleur** : six alignements, deux répartitions, « Par rapport aux marges », « Ajuster aux marges » (MW:617-634) | N13 |
| e8 | Arrange ▸ Layout Guides… (p. 16, 18) | **partiel** : dock Document (MW:1728-1814), sans entrée de menu | N18 |
| e9 | Arrange ▸ Ruler Guides ▸ Add Horizontal/Vertical (le repère arrive au milieu de la page, p. 20), Format…, Clear All | **fait** : posé au milieu (VM:1566-1580 ; MW:636-641) ; Format… se fait au dock (MW:1816-1866), sans entrée de menu | N18 |
| e10 | Arrange ▸ Group, Ungroup, Regroup (p. 16, 46, 58) | **partiel** : Regrouper manque | N13 |
| e11 | Arrange ▸ Text Wrapping ▸ (p. 16) | **manquant** | seuil D |
| e12 | Format ▸ Font… à Styles…, Background… (p. 24) | **partiel** : menu Texte (gras, italique, souligné, alignements, import) ; fond de page au dock (MW:1882-1900) | N18 |
| e13 | View ▸ Two-Page Spread, Master Page, Header and Footer, Rulers, Boundaries… (p. 9) | **partiel** : Affichage ▸ Repères, Règles (MW:674-675) | N4, N22 ; pages en vis-à-vis et page maître non planifiées |
| e14 | View ▸ Zoom ▸ ; liste « 74% » (p. 8, 9) | **partiel** : Zoom avant, Zoom arrière, Taille réelle (MW:677-679) ; raccourcis liés mais non affichés au menu | N4 |
| e15 | Insert ▸ Page… (dialogue), Duplicate Page (p. 22) | **partiel** : Ajouter, Dupliquer, Supprimer la page (MW:667-671) | N18 (A8) |
| e16 | Insert ▸ Page Numbers…, Date and Time… (p. 22) | **partiel** : les champs ne se choisissent qu'au dock | N18 |
| e17 | Insert ▸ Picture ▸ From File…, AutoShapes ▸ (p. 39) | **fait** pour l'image depuis un fichier, le rectangle, l'ellipse, le trait (MW:593-602) | — |
| e18 | Insert ▸ Business Information… (p. 22) | **partiel** : Édition ▸ Informations professionnelles… règle le jeu (MW:590) mais ne pose pas de bloc | N18 |
| e19 | File ▸ Page Setup, Print…, Print Preview, Pack and Go ▸, Publish as PDF…, fichiers récents 1 à 6, Properties (p. 61) ; Recent Publications au démarrage (p. 3, 51) | **partiel** : export PDF (**meilleur**, avec les avertissements de B5) ; Page Setup se fait au dock ; impression B3, aperçu B6, archive B4 ; **fichiers récents : manquants**, aucun « récent » dans le code | N11 (les récents) ; B3, B4, B6 |
| e20 | Commandes grisées selon le contexte (Group grisé avec un seul objet, p. 44 ; Delete Text…) | **fait** (`CanExecute` : VM:1339, 1385 ; VMD:243-253) | — |
| e21 | Raccourci affiché en face de chaque entrée (p. 9, 16, 22, 61) | **partiel** : les raccourcis du zoom ne sont pas affichés ; « Ctrl+Shift+G » aux menus, « Ctrl+Maj+G » dans les bulles (MW:605, 749) | N13 |
| e22 | Menu Help, champ « Type a question for help » (p. 3) | **manquant** | écarté |
| e23 | Edit ▸ Cut, Copy, Paste, Paste Special…, Find…, Replace…, Go to Page… (HR) | **partiel** : Annuler, Rétablir, Dupliquer, Supprimer, Tout sélectionner (MW:581-591) ; ni presse-papiers, ni recherche | N8, N20 ; Atteindre la page à N18 |

## (f) Champs, unités et dialogues

| # | Publisher 2007 (source) | MyPublisher | Poste |
|---|---|---|---|
| f1 | Le champ qui reçoit le focus est présélectionné, si bien qu'une frappe remplace sa valeur (« 12pt » p. 17, nom du jeu p. 55) | **fait** au clavier : un TextBox d'une ligne sélectionne tout quand Tab lui donne le focus (Av:TextBox.cs:1293-1300), champs numériques compris ; pas au clic | — |
| f2 | Toupies ▲▼, ou frappe d'un nombre (p. 19) | **partiel** : boutons de pas masqués (Apparence.axaml:535-538) ; ↑/↓ et la molette marchent probablement, sans test | N14 |
| f3 | Unité affichée dans la valeur : 12pt, 1", 0.2", 0.04", 6 pt (p. 17-19, 35, 37) | **fait** : mm, pt, °, %, × (DC:78-92) | — |
| f4 | Unité d'affichage réglable, Tools ▸ Options (HR) | **manquant** : mm fixes, par décision du propriétaire du 24 octobre 2026 (PLAN, phase I) ; aucun magasin de préférences hors des coordonnées | écarté : l'affichage reste en mm |
| f5 | Taper une autre unité, convertie : « 2 cm », « 1" », « 12 pt » (HR) | **manquant** : seul le suffixe propre au champ est retiré (DC:120), puis `decimal.Parse` (DC:128) | N14 |
| f6 | Séparateur décimal | **meilleur** : virgule et point, quelle que soit la culture (DC:123-124) | Q14 (livré) |
| f7 | Saisie illisible ou hors bornes : un message (HR, à vérifier) | **manquant** : refus silencieux. `NumericUpDown` avale l'erreur (Av:NumericUpDown.cs:1128-1140) et refuse hors bornes sans ramener à la borne (Av:NumericUpDown.cs:794-813, 1222-1240) ; aucun `ClipValueToMinMax` | N14 |
| f8 | La valeur part au document à OK (HR) | **partiel** : elle part à chaque frappe lisible qui tient dans les bornes (Av:NumericUpDown.cs:556-559, 687-693, 1045-1057). Taper « 210 » dans la largeur de page passe par 21 mm (déduit) ; un pas par champ (MWcs:356-375) | N14 |
| f9 | OK applique, Cancel renonce, un pas par dialogue (p. 17-37) | **partiel**, par conception : le dock applique tout de suite, un pas par champ ; rien ne permet de renoncer à un ensemble de réglages | — (le dock reste, décision du 25 octobre) |
| f10 | Entrée = OK, Échap = Cancel (HR) | **partiel** : Entrée valide le champ. Échap valide et quitte le champ dans la barre (BCcs:36-57), ne fait rien au dock (MWcs:559). Le catalogue et le dialogue des coordonnées ont un bouton par défaut mais pas d'`IsCancel` : Échap ne les ferme pas (TemplateChooserWindow.axaml:26 ; BusinessInfoWindow.axaml:26) | N14 |
| f11 | Tab et Maj+Tab passent d'un champ à l'autre (HR) | **partiel** : navigation par défaut. Dans la barre : Police, puis Corps, puis Interligne. Au dock : Tab s'arrête aussi sur les titres de section et les cases. Aucun test | N14 |
| f12 | Listes avec aperçu : jeu de couleurs en pastilles, jeu de polices dessiné dans ses fontes (p. 6-7, 52) | **fait** (MW:1623-1678) ; la liste des polices de la barre est dans la police de l'interface (BC:128-145) | C5 (livré) |
| f13 | Boutons radio et cases ; un champ actif seulement avec son option (p. 22, 26, 37) | **fait** (listes, cases, `IsEnabled` lié, ex. MW:1381) | — |
| f14 | Aperçu « Sample » avant OK (p. 17, 26-32) | **meilleur** : la page change elle-même, et cela s'annule | — |
| f15 | Liste de couleur : No Fill, couleurs du jeu, couleurs récentes, More Colors…, Fill Effects… (p. 35) | **partiel** : cinq pastilles du jeu et le `ColorPicker` (SelecteurDeCouleur.cs:110-131) ; « Aucun » est dans les listes Fond et Contour ; pas de couleurs récentes | — |
| f16 | Format Text Box, six onglets (p. 34-37) | **partiel** : les sections du dock. Manquent « Apply settings to new text boxes », la rotation du texte de 90°, « Continued on/from page » (seuil D), les onglets Layout et Web ; la bordure par côté est écartée | A12 (Format du bloc…) ; le reste non retenu |
| f17 | Layout Guides : Two-page master, Add center guide, aperçu (p. 18-19) | **partiel** : marges, grille et ligne de base au dock (MW:1728-1814) | N18 |
| f18 | Insert Page : nombre, avant ou après, contenu ; un champ grisé tant que son option n'est pas choisie (p. 22) | **manquant** | N18 (A8) |
| f19 | Fill Effects (p. 26-32) | **partiel** : gabarits du dock (MW:475-540) ; textures et motifs écartés | C5 (livré) |
| f20 | Catalogue de démarrage : un clic sélectionne, Create crée ; jeux de couleurs, de polices et de coordonnées, formats vierges, tout choisi avant la création (p. 4-7, 51-52) | **partiel** : vignettes, double-clic ou Entrée (TemplateChooserWindow.axaml.cs:29 ; TemplateChooserWindow.axaml:26) ; ni jeux ni formats à la création | B1 (livré) |
| f21 | Informations professionnelles : champs d'une ligne et multilignes, logo avec aperçu, Save (p. 55) | **partiel** : tout, sauf l'aperçu du logo (BusinessInfoWindow.axaml) | C4 (livré) |
| f22 | Sélecteur de fichier : double-clic ou Open, filtre, Insert ▾ (p. 30, 42) | **fait** (sélecteurs du système) ; les images sont toujours liées | A3 (livré) |
| f23 | Barre d'état : position du pointeur (p. 13, 21, 24, 47), position et taille de l'objet (p. 33, 50, 53) | **fait**, en mm (MW:793-802 ; VM:452-481) | A6 (livré) |
| f24 | Zoom tapé dans la liste (p. 8, 24) | **partiel** : curseur et boutons, pas de champ à saisir (MW:757-758, 822-825) | N4 |
| f25 | Demander avant de perdre des modifications (HR) | **manquant** : ni Nouveau, ni Ouvrir, ni Quitter, ni la fermeture de la fenêtre ne demandent rien | N11 |
| f26 | Edit WordArt Text (p. 48-49) | **manquant — écarté** (WordArt) | écarté |

## (g) Tableaux

Tout est hors relevé : le livre ne montre que le nom du menu Table (p. 3).

| # | Publisher 2007 (source) | MyPublisher | Poste |
|---|---|---|---|
| g1 | Insert ▸ Table : dialogue avec lignes, colonnes, format (HR) | **partiel** : l'outil `B` et Insertion ▸ Tableau posent une bande de trois cases (VM:924-946) ; lignes et colonnes au dock, de 1 à 40 (MW:1282-1288) | C9 (livré) |
| g2 | Clic dans une case, puis frappe sur la page (HR) | **manquant**, par un choix écrit (PLAN:2437-2439 : « Une case ne se choisit pas sur la page ») ; saisie au dock dans un champ de 54 px, en police d'interface (MW:1363-1366 ; Table.cs:82-96) | N27 |
| g3 | Tab et Maj+Tab de case en case ; Tab dans la dernière case ajoute une ligne ; Ctrl+Tab insère une tabulation (HR) | **manquant** | N27 |
| g4 | Flèches d'une case à l'autre ; glisser pour sélectionner des cases (HR) | **manquant** | N27 |
| g5 | Mise en forme d'une case ou de plusieurs (HR) | **partiel** : le tableau entier à la barre (BC:243-296) ; alignement et fond case par case au dock ; pas de souligné (Souligne.cs:47-51) | N28 |
| g6 | Insérer ou supprimer une ligne ou une colonne à un endroit choisi (HR) | **partiel** : compteurs du dock, qui ajoutent et retirent en fin seulement | N28 |
| g7 | Tirer une frontière de colonne ou de ligne (HR) | **manquant** : largeur et hauteur se règlent au dock (MW:1316-1350) | N28 |
| g8 | Fusionner, scinder des cases (HR) | **manquant**, limite assumée (README:565-567) | écarté (limite écrite au README) |

## Ce que la relecture du cours ajoute aux autres fiches

Relevé en relisant les 68 pages sous l'angle de la saisie. Rien de ce qui suit ne renverse une
fiche, sauf la 10 et la 20 ; les autres gagnent des détails.

- **01** — la galerie a un bandeau de catégories, des groupes de fabricants dépliables (⊞),
  « Filter Blank Sizes… » et un curseur de taille des vignettes. La consigne « Click Customize
  under Color scheme » est imprécise : la capture montre la liste ouverte par sa flèche.
- **02, 04** — l'outil de sélection est actif par défaut (en orange, p. 8). Sans sélection, la
  barre d'état donne la position du pointeur, en pouces (p. 13, 21, 47). La barre WordArt est en
  p. 50, non 49. La position lue p. 33 est incertaine : « 2.063 » ou « 2.963 in. ».
- **03** — la liste *Add or Remove Buttons ▸ Formatting* (p. 10) est le seul inventaire complet
  des commandes de mise en forme et de leurs touches : Bold `Ctrl+B`, Italic `Ctrl+I`, Underline
  `Ctrl+U`, Align Left `Ctrl+L`, Center `Ctrl+E`, Align Right `Ctrl+R`, Justify `Ctrl+J`, Decrease
  et Increase Font Size `Ctrl+<` et `Ctrl+>` ; et, absents par défaut, Small Caps, All Caps,
  Superscript, Subscript, Outline, Emboss, Engrave, Single, 1.5 et Double Spacing.
- **05** — Boundaries and Guides (`Ctrl+Shift+O`) et Baseline Guides (`Ctrl+F7`) sont deux
  bascules distinctes. Le champ qui reçoit le focus est présélectionné ; une valeur se règle à la
  toupie ou en tapant un nombre (p. 17-19). Le livre se contredit : 4 × 2 au dialogue, 3 × 3 sur
  la capture du résultat.
- **06** — le repère ajouté par le menu arrive au milieu de la page (p. 20) ; curseur ⇳.
- **07** — le geste nommé est le **copier-coller** (« copy it to the sidebar », p. 21 ; « Copy
  and paste », p. 23).
- **08** — le champ « Duplicate all objects on page: » reste grisé tant que son bouton radio
  n'est pas choisi (p. 22).
- **09** — la grille Tint/Shade compte 21 pastilles (7 × 3), non 18. OK reste grisé dans l'onglet
  Picture tant qu'aucune image n'est choisie ; Transparency et « Rotate fill effect with shape »
  sont grisés pour un fond de page ; Select Picture valide au double-clic.
- **10** — un second chemin crée une zone : *Insert ▸ Text Box* (p. 39, 41). La barre Formatting
  s'active dès le tracé (p. 33). **Le double-clic n'est pas le geste de Publisher** : le livre ne
  montre jamais comment on entre dans le texte, et Publisher y pose le point d'insertion au premier
  clic (HR).
- **11** — le clic droit sélectionne le bloc (poignées et point vert). « Delete Text » est grisé
  quand le bloc est vide (p. 34) et actif sinon (p. 36). La liste Fill Color se lit : No Fill, les
  couleurs du jeu, les couleurs récentes, More Colors…, Fill Effects….
- **12** — second chemin : *Format ▸ AutoFit Text ▸* (p. 24). Best Fit fait passer le corps de
  14 à 9 dans la barre (p. 36 puis 38). L'indicateur de débordement est visible p. 36. Les marges
  se saisissent dans des champs à flèches, en pouces.
- **14** — l'image s'insère au double-clic ou par le bouton partagé *Insert ▾* ; la barre Picture
  a ▾ et × dans son titre.
- **16** — *Ungroup* porte aussi `Ctrl+Shift+G` (p. 44, 46) : la même touche groupe et dissocie.
  *Group* est grisé avec un seul objet. Un bouton Group/Ungroup apparaît sous la sélection
  (p. 41, 46).
- **17** — la galerie de styles est lisible (p. 47) : 30 vignettes en 5 × 6. L'objet posé et sa
  barre sont p. 50 ; la p. 49 montre l'aperçu en direct. B, I, Font et Size s'y appliquent à tout
  le texte.
- **18** — « Highlight text and type over it » (p. 53, coquille du livre : « add ») est le geste
  de base. La capture p. 53 montre une sélection en vidéo inverse, et la barre qui reflète la
  sélection (corps décimal 10.1).
- **19** — la balise ⓘ du bloc de coordonnées a quatre entrées : Edit…, Save to…, Update from…,
  Convert to Plain Text (p. 54).
- **20** — **contradiction** : la fiche dit qu'il faut dissocier pour changer la taille *ou
  remplacer l'image*. Le livre ne fait dissocier que pour redimensionner (p. 58) ; l'image se
  remplace et le texte se retape dans le groupe (p. 58-60).
- **21** — *Finish* est disponible à chaque écran de l'assistant ; les emplacements indisponibles
  sont grisés ; la p. 68 montre la structure de l'archive (le `.pub`, les images renommées
  `image0` à `image6`, un manifeste `[Content_Types].xml`), utile pour B4.
- **22** — *Reroute Connectors* et non « Remove Connectors » (p. 16, 44). Le menu Insert porte
  `Ctrl+Shift+N`, `Ctrl+Shift+U`, `Ctrl+Shift+E` et `Ctrl+K` (p. 22, 39) ; *Symbol…* n'est actif
  qu'avec un point d'insertion. Le menu File porte New `Ctrl+N`, Open `Ctrl+O`, Close `Ctrl+F4`,
  Save `Ctrl+S`, Print `Ctrl+P`, Exit `Alt+F4` et six fichiers récents (p. 61). **Le menu Format
  (p. 24) n'est recensé par aucune fiche** : Font…, Character Spacing…, Paragraph…, Bullets and
  Numbering…, Horizontal Rules…, Tabs…, Drop Cap…, AutoFit Text ▸, Styles…, grisés sans texte
  sous le curseur. Le livre souligne partout les lettres d'accès des menus, signe de la
  navigation par Alt.

## Ce que la vérification du code a corrigé

Points où deux lectures se contredisaient, tranchés dans le code, dans Avalonia décompilé ou sur
une page du livre relue.

1. **La fenêtre gagne la touche partout.** `KeyboardDevice` essaie les liaisons de la fenêtre
   *avant* de prévenir le champ qui a le focus (Av:KeyboardDevice.cs:164-194), et une liaison ne
   rend la touche que si sa commande ne peut pas s'exécuter (Av:KeyBinding.cs:49-61). En saisie,
   `Ctrl+A` sélectionne donc les blocs, `Ctrl+D` duplique le bloc, `Ctrl+E` exporte — dans le
   champ Contenu et les champs numériques aussi (N6).
2. **Annuler ou Rétablir peut sortir la frappe de toute transaction**, dans trois cas : `Ctrl+Z`
   sans rien à annuler ; `Ctrl+Y` après n'importe quelle frappe, puisque `CommitEdit` vide la pile
   de rétablissement (VM:691) ; une annulation pendant qu'un champ du dock garde le focus. La
   frappe qui suit ne s'annule plus et ne marque pas le document « modifié » (Q24).
3. **Les touches de la p. 10 sont dans le livre**, et non hors relevé : `Ctrl+B`, `Ctrl+E`,
   `Ctrl+L`, `Ctrl+R`, `Ctrl+J`, `Ctrl+<`, `Ctrl+>`. F9 aussi, dans le texte de la p. 36.
4. **L'aide française dit ce que le livre anglais ne dit pas** : `Ctrl+G` est le gras, `Ctrl+B`
   « Atteindre la page » — la recette de R7, qui écrivait `Ctrl+B`, se trompait deux fois —,
   `Ctrl+Maj+F` ouvre la boîte Police, `Ctrl+Maj+H` la coupure de mots, `Ctrl+=` met en indice,
   `Alt`+flèches déplace l'objet dont le texte a le curseur, `Ctrl+Maj+G` groupe et dissocie.
5. **Q22 n'est pas réglé.** Une pression sans mouvement sur une poignée repasse par les cosinus
   d'un bloc tourné, et `Maj` arrondit l'angle sans le moindre mouvement : un bloc à 7° passe à 0°.
6. **Le banc sait plus qu'on ne le croyait** : Avalonia.Headless simule une touche AZERTY
   (`KeyPress(Key, modificateurs, PhysicalKey, symbole)`), et son horloge est un vrai chronomètre.
   Il ne sait pas le clic droit faute d'aide, ni le presse-papiers faute d'essai (N1).
7. **« _Édition » est injoignable au clavier** : `AccessKeyHandler` compare `Key.ToString()`, et
   aucune touche ne s'appelle « É ». Des lettres d'accès en double font tourner le focus d'une
   entrée à l'autre sans rien déclencher (N13).
8. **Les champs numériques avalent l'erreur et poussent chaque frappe** : une saisie illisible est
   refusée sans un mot (Av:NumericUpDown.cs:1128-1140), et taper « 210 » dans la largeur de la
   page passe par 21 mm (N14).
9. **Le texte d'un groupe se retape sans dissocier** (p. 58-60), contrairement à ce que disait la
   fiche 20 (N26).

## Reste à vérifier sur un Publisher 2007 français

Tout ce qui est marqué HR, en particulier : le clic simple dans le texte, le triple-clic,
`Ctrl+A` sur l'article ; Tab d'un objet à l'autre, le rectangle de sélection ; Ctrl+glisser, Maj
qui contraint, Ctrl depuis le centre ; le double-clic sur une image ou une forme ; le menu
contextuel du vide ; la place d'un objet collé ; le message d'une valeur refusée ; les libellés
français des sous-menus.

## Les postes

Les écarts sans poste sont rangés dans la phase N du plan, qui reprend aussi les postes déjà
ouverts qui sont des modalités de saisie (A8, A9, A11, A12, A13, Q21, Q22, R6, R7) :

| Poste | Ce qu'il apporte à la saisie |
|---|---|
| Q24, Q25 | Annuler ne perd plus la frappe ; un réglage fait pendant la frappe a son propre pas |
| N1 | Le banc des gestes : clic, clic droit, glissé, frappe, sélection de texte, presse-papiers |
| N2, N3 | Les préalables du texte riche : mettre en forme une plage, la position de chaque morceau composé |
| N4 | L'affichage au clavier : F9, page entière, zoom tapé, limites, lignes de base |
| N5, N6 | Sortir de la saisie d'un clic qui agit ; la touche laissée au champ |
| N7 | La mesure qui décide la voie de R7 |
| N8 | Couper, copier, coller des blocs |
| N9 | La langue du texte |
| N10 | Les raccourcis de Publisher en français (fiche 24) |
| N11 | Ne rien perdre en fermant ; les documents récents |
| N12 | Entrer dans le texte d'un clic, le curseur au point visé |
| N13 | Les menus de Publisher : lettres d'accès, gestes affichés, Tab d'un bloc à l'autre |
| N14 | Les champs : unités tapées, refus signalés, valeur à la validation, Échap |
| N15, N16 | La sélection du texte connue de l'application ; Tab et saut de ligne forcé |
| N17 | Ctrl+glisser, Maj qui contraint, le rectangle de sélection |
| N18 | Les menus qui mènent au dock ; les pages au clavier |
| N19 | Le corps en liste, la police tapée |
| N20 | Rechercher et remplacer |
| N21 | La saisie montre le texte tel qu'il s'imprime |
| N22 | Les caractères spéciaux |
| N23, N24 | Puces et numérotation ; exposant, indice, capitales |
| N25 | La vérification orthographique |
| N26, N27, N28 | Taper dans un groupe, dans une case ; lignes et colonnes à l'endroit choisi |
