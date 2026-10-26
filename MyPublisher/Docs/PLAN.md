# Plan de développement

> **À lire avant ce plan** : [`Documents-reels.md`](Documents-reels.md), relevé des deux
> documents ATR réellement composés sous Publisher. Les fiches disent ce que Publisher
> *sait faire* ; ces documents-là disent ce dont on *se sert*. Ils ajoutent cinq postes
> que ce plan ne prévoyait pas — dont le texte riche, sans lequel aucun des deux ne se
> reproduit — et ils déclassent le seuil D.

Couche d'exécution posée sur le relevé fonctionnel de
[`Publisher2007/00-index.md`](Publisher2007/00-index.md) : mêmes priorités, mais
regroupées en phases, avec les dépendances explicites et un critère de recette par
poste. Les fiches sont citées par numéro (`fiche 12`) et non par ligne de code — le
code bouge, les fiches non.

Établi le 30 août 2026, sur l'état du dépôt à cette date : `TextFrame.Background`
vient d'atterrir, `ImageFrame` n'a ni fond ni bordure, `ShapeFrame` garde `Fill`.

---

## Suivi

Où en est chaque poste. Une seule règle pour la colonne d'avancement : un poste ne passe
à 100 % que lorsque **sa recette est jouée et que le contrôle P2 passe**. Entre les deux,
on écrit ce qui est fait plutôt qu'un pourcentage inventé — un poste à moitié écrit n'est
pas un poste à moitié livré. La colonne se met à jour dans le commit qui clôt le poste.

*Dernière mise à jour : 26 octobre 2026 (salve Saisie 1 livrée : Q24 et Q25, Annuler et Rétablir ne laissent plus de frappe hors de toute transaction, et un réglage fait pendant la saisie a son propre pas ; N1, le banc des gestes ; N2, la mise en forme d'une plage dans le noyau ; N3, la position de chaque morceau composé ; N4, l'affichage au clavier. Plus tôt le même jour : phase N ouverte, la saisie de Publisher : N1 à N28, et Q24 et Q25 en phase Q ; A8, A9, A11, A12, A13, Q21, Q22, R6 et R7 repris par elle). Le 25 octobre 2026 : I2 livré avec B7 : la disposition de la fenêtre ; I4 livré : la planche, et Q10 avec elle ; I6 livré : naviguer dans la planche, et garder les proportions ; I7 livré : le souligné et ses sortes, le modèle seul ; I5 livré : les vignettes de pages ; I8 livré : masquer un bloc ; I9 livré : les lignes en excédent au dock ; A10 livré : le trait et la flèche, avec l'outil Ligne ; I3 livré : la barre contextuelle ; Q23 livré : sélectionner un bloc ne fige plus la fenêtre ; relecture et juge après fusion pour I3, I5, I6, I8, I9, A10 et Q23).*

| Poste | Intitulé | Avancement | Où ça en est |
|---|---|:---:|---|
| **P0** | Nom du remplissage | **100 %** | `Fill` retenu et daté ; renommage fait en C1 |
| **P1** | Dépôt sous git | **100 %** | Branche `main`, commit initial `cc098e0` |
| **P2** | Contrôle de non-régression | **100 %** | `Scripts/controle.sh`, référence dans `Scripts/reference.txt` |
| **P3** | Banc de vérification | **100 %** | Noyau séparé, banc de rendu, comparaison écran ↔ PDF, export déterministe, `Scripts/verifier.sh`, bilan dans `Scripts/bilan.sh`. Les sept recettes qui manquaient sont écrites, chacune éprouvée par son sabotage ; le bilan ne nomme plus aucun poste clos sans recette |
| **A1** | Poignée de rotation | **100 %** | Neuvième poignée au-dessus du bloc, aimantée sur 15° sous Maj ; recette dans `Scripts/recettes/` |
| **A2** | Signaler le débordement du texte | **100 %** | Marqueur rouge à l'écran, avertissement à l'export, et l'export rogne au bloc comme l'écran |
| **A3** | Chemins d'images relatifs | **100 %** | Relatif sous le dossier du document, absolu ailleurs ; résolu à l'ouverture. `FormatVersion` reste à 3 |
| **A4** | Superposition : raccourcis et glisser-déposer | **100 %** | `Ctrl+Maj+F` / `Ctrl+Maj+B`, et la liste des blocs se réordonne en y tirant un bloc ; les quatre commandes ne perdent plus leur sélection |
| **A5** | Formats de page | **100 %** | B4, B5, Executive, Tabloid au catalogue, et largeur/hauteur libres au panneau. `FormatVersion` reste à 3 |
| **A6** | Taille du bloc dans la barre d'état | **100 %** | Position **et** taille du bloc, suivies pendant le geste et non seulement à sa fin |
| **A7** | Alignement vertical du texte | **100 %** | Haut, milieu, bas sur le bloc de texte ; le calage est borné à zéro. `FormatVersion` reste à 3 |
| **A8** | Insérer des pages : nombre, position, contenu | 0 % | Spécifié le 11 octobre 2026 ; livré avec N18 (phase N, salve Saisie 5) |
| **A9** | Rotation par quart de tour | 0 % | Spécifié le 11 octobre 2026 ; livré avec N18 (phase N, salve Saisie 5) |
| **A10** | Objet trait, et flèche | **100 %** | `ShapeKind.Line` : un trait est une forme couchée, sans hauteur, longue de sa largeur, que sa rotation oriente autour de son milieu ; il se définit par ses deux extrémités (`PoserLesExtremites`), et la taille minimale d'un bloc ne s'y applique jamais. `StartArrow` et `EndArrow`, une pointe pleine facultative à chaque bout ; `FormatVersion` ne change pas, Samples et Templates réenregistrés. L'outil Ligne (`L`) du rail, entre Ellipse et la loupe, trace d'une extrémité à l'autre, `Maj` à 0, 45 ou 90°, la bulle disant longueur et angle ; *Insertion ▸ Trait*. Deux poignées sur les extrémités, sans rotation ; survol à 3 pt de l'encre. Au dock, X1, Y1, X2, Y2 à la place du cadre, les pointes, et le contour pour l'épaisseur et la couleur. Un coup de crayon `m … l S` au PDF, au même endroit qu'à l'écran ; les vignettes le dessinent. L'import des Fiches rend 5 traits et 0 rectangle de substitution, R5 compte les traits à part. L'icône du poste sert. Treize tests d'interface, vingt-huit du noyau ; relu et jugé le 26 octobre 2026 : un groupe étiré met désormais ses traits à l'échelle par leurs extrémités, et non par leur largeur ; treize sabotages au juge. Recette dans `Scripts/recettes/` |
| **A11** | Aimantation réglable, et aux autres blocs | 0 % | Spécifié le 11 octobre 2026 ; repris en phase N (salve Saisie 13) : au redimensionnement et au tracé, préférences de N11 |
| **A12** | Menu contextuel du bloc | 0 % | Spécifié le 11 octobre 2026 ; repris et élargi en phase N (salve Saisie 6) : presse-papiers, Format du bloc… vers le dock, double-clic sur une image ou une forme |
| **A13** | Rognage manuel de l'image | 0 % | Spécifié le 11 octobre 2026 ; repris en phase N (salve Saisie 14) : Rogner au menu contextuel |
| **A14** | Contour et ombre du caractère | 0 % | Spécifié le 11 octobre 2026 ; PDFsharp à éprouver d'abord |
| **A15** | Sections : numérotation qui repart | 0 % | Spécifié le 11 octobre 2026 |
| **B1** | Modèles | **100 %** | Catalogue au démarrage et sur `Ctrl+N` ; quatre modèles composés dans `Templates/` ; recette dans `Scripts/recettes/`, qui a remis le nom de la carte de visite à la taille de son texte |
| **B2** | Importer un fichier texte | **100 %** | Menu « Importer un texte… » sur le bloc choisi, en un pas d'annulation ; UTF-8 strict, UTF-16 à marque, repli Windows-1252 ; vide, binaire, au-delà de 128 Kio ou illisible : refusé avec un message, bloc intact. Recette dans `Scripts/recettes/` |
| **B3** | Imprimer | 0 % | — |
| **B4** | Archive `.mypubz` | 0 % | — |
| **B5** | Avertir sur les substitutions de polices | **100 %** | L'export nomme la police demandée et celle employée ; recette dans `Scripts/recettes/` |
| **B6** | Aperçu avant impression | 0 % | Spécifié le 11 octobre 2026 |
| **B7** | Replier les panneaux | **100 %** | Livré avec I2 : `Ctrl+F1`, ou *Fenêtre ▸ Replier les panneaux*, masque le panneau Pages et le dock, et le second les rend tels qu'ils étaient ; le menu Fenêtre masque l'un ou l'autre, et ce geste périme l'état d'avant le repli. Le rail reste. Recette `I2-B7-disposition` dans `Scripts/recettes/` |
| **C1** | Fond et bordure au niveau de `Frame` | **100 %** | Fond, contour, trait, arrondi et opacité sur tout bloc ; `FormatVersion` 2, migration à l'ouverture ; recette dans `Scripts/recettes/` |
| **C2** | Repères | **100 %** | C2a grille de composition · C2b règles graduées et repères libres ; recette dans `Scripts/recettes/` |
| **C3** | Sélection multiple, puis groupement | **100 %** | C3a sélection · C3b alignement et répartition · C3c groupe persistant ; recette dans `Scripts/recettes/` |
| **C4** | Champs | **100 %** | Folio, nombre de pages, date, coordonnées et logo ; les coordonnées servent depuis B1 ; recette dans `Scripts/recettes/` |
| **C5** | Remplissage riche et cohérence visuelle | **100 %** | Fond de page, et remplissage uni, dégradé, image ou teinte sur tout bloc ; les cinq images d'ATA 21 sont portées. C5b : couleurs et polices du jeu par référence (`scheme:accent1/80`, `scheme:heading`), résolues dans le noyau pour l'écran et le PDF ; six jeux de couleurs et cinq de polices, modèles sur « Marine ». `FormatVersion` 5, migration testée propriété par propriété. Recettes dans `Scripts/recettes/` |
| **C6** | Marges intérieures par côté | **100 %** | Quatre marges sur le bloc de texte ; `FormatVersion` 3 ; recette dans `Scripts/recettes/` |
| **C7** | Bibliothèque de blocs | 0 % | Débloqué depuis C3a |
| **C8** | Miroir horizontal et vertical | 0 % | Spécifié le 11 octobre 2026 |
| **C9** | Le bloc tableau | **100 %** | Bande de cases : colonnes, lignes, contenu, fond et contour par case. `FormatVersion` reste à 3 |
| **R1** | Texte riche : fragments de style | **100 %** | `Spans` sur le bloc ; l'écran et l'export passent par la même composition ; recette dans `Scripts/recettes/` |
| **R2** | Retraits et espacement de paragraphe | **100 %** | Cinq réglages sur le bloc, des écarts par indice de paragraphe ; la ligne composée porte son retrait et sa largeur |
| **R3** | Colonnes à l'intérieur d'un bloc | **100 %** | Nombre et gouttière sur le bloc ; la ligne composée porte sa colonne et son ordonnée. Autre chose que C2a |
| **R4** | Interligne : une seule mesure | **100 %** | Multiple de la hauteur naturelle ; écran et PDF concordent à 0,05 pt près ; recette dans `Scripts/recettes/` |
| **R5** | **Ouvrir un `.pub`** | **100 %** | Lecture déléguée à libmspub. Les deux documents ATR s'ouvrent objet pour objet, sans perdre un style |
| **R6** | Alignement par paragraphe | 0 % | Spécifié le 11 octobre 2026 ; repris en phase N (salve Saisie 7), sur N2, N15 et N21 |
| **R7** | Éditer le texte riche à la souris | 0 % | Spécifié le 11 octobre 2026 ; repris en phase N (salve Saisie 8) : la mesure est N7, le gras `Ctrl+G` ; attend la décision de voie |
| **Q1** | Les styles suivent la frappe | **100 %** | Fragments et écarts recalés à la saisie par une fonction pure du noyau (`Recalage`), à la règle de Publisher : le style qui précède, celui qui suit en tête de paragraphe, le premier remplacé pour une sélection retapée. Seules les zones de saisie s'y lient ; lecture, import, copie et annulation ne recalent rien. `FormatVersion` reste à 4 ; recette dans `Scripts/recettes/` |
| **Q2** | Le texte composé se détache de son bloc | **100 %** | `ControleDeBloc` suit l'attachement, comme `FondBloc`, et lâche l'ancien bloc quand on lui en donne un autre. Mesuré : le changement de page ne fuyait pas ; la fuite ne frappait qu'un contrôle sorti de l'arbre avec son bloc. Recette dans `Scripts/recettes/` |
| **Q3** | Le cache d'images oublie ses échecs | **100 %** | L'ouverture d'un document (fichier ou modèle) oublie les échecs du cache, et eux seuls ; insérer une image ou la choisir pour un fond relit le fichier. L'annulation ne relit pas le disque. Recette dans `Scripts/recettes/` |
| **Q4** | Un filet au même endroit à l'écran et au PDF | **100 %** | L'écran élargit l'élément du filet d'une demi-épaisseur, sans arrondi au pixel de mise en page : centré sur le bord comme à l'export, de 0,5 à 5 pt (écart ≤ 0,01 pt mesuré à 8 px/pt). Les recettes de C1 et C5 resserrées en conséquence. Recette dans `Scripts/recettes/` |
| **Q5** | Un groupe rogne pareil à l'écran et au PDF | **100 %** | Découpe calculée dans le noyau (`Decoupe`), lue par l'écran et l'export, groupes et tableaux, tournés et imbriqués ; elle déborde du plus large demi-filet des blocs portés (√2 fois pour un bloc tourné). Recette dans `Scripts/recettes/` |
| **Q6** | Mesurer la découpe d'un remplissage par image | **100 %** | Bloc arrondi rempli d'une image dans le document de C5 ; ses coins sont relevés au PDF et à l'écran, et les deux sabotages de la découpe tombent ; recette `C5-Q6-fonds` dans `Scripts/recettes/` |
| **Q7** | La recette de A6 sur le cas réel | **100 %** | La recette trace ses blocs à l'outil, au pointeur, comme la main ; renommée `A6-Q7-taille-dans-la-barre`, dans `Scripts/recettes/` |
| **Q8** | N'avertir que des polices dessinées | **100 %** | La passe des polices lit les styles composés (`StyleEffectif.Decouper`) sur le contenu que `DrawText` compose, cases de tableau comprises ; un fragment recouvert, blanc, hors du texte, ou celui d'un champ n'avertit plus. Une seule règle `Fragments` pour la passe et le dessin. Recette dans `Scripts/recettes/` |
| **Q9** | Retirer `AbsoluteLineHeight` | **100 %** | La vignette des modèles lit les lignes de la composition ; repris après relecture : fer, découpe et filet de champ éprouvés, filets gardés en mémoire ; recette dans `Scripts/recettes/` |
| **Q10** | Saisir une poignée hors de la page | **100 %** | Absorbé par I4 : la couche de saisie couvre la planche ; la rotation d'un bloc collé en haut, règles masquées, ou retourné en bas, et la poignée d'angle d'un bloc à Y = 0 se saisissent sur le gris, sans poser de repère, et la règle garde son geste. Recette `I4-Q10-planche` dans `Scripts/recettes/` |
| **Q11** | La liste des blocs défile pendant le glissé | 0 % | Relevé en A4 |
| **Q12** | Dépendance vulnérable | **100 %** | `Tmds.DBus.Protocol` relevé à 0.21.3 (correctif rétroporté) par une référence directe dans `MyPublisher` ; Avalonia reste en 11.3.9. Recette à part, `Scripts/verifier-dependances.sh`, parce qu'elle demande le réseau : elle lit la base des vulnérabilités et le prouve sur un projet témoin |
| **Q13** | Couper la ligne au même mot à l'écran et au PDF | **100 %** | Polices de l'écran résolues par fontconfig, variante par variante (l'italique absent penché des deux côtés) ; coupure sur la plus large des deux mesures, sur la taille du modèle ; 0 ligne coupée autrement sur l'exemple, ATA 21, les fiches et `recette-coupure.mypub`, dans trois ordres de chargement ; outil `comparer-mots.sh`. Recette dans `Scripts/recettes/` |
| **Q14** | Un double-clic ne déplace pas le bloc | **100 %** | Un mouvement nul ne passe plus par l'aimantation ; les champs en millimètres et d'angle rendent au modèle la valeur exacte qu'ils ont affichée (mémoire d'une case). Ouvre Q21 à Q23. Recette dans `Scripts/recettes/` |
| **Q15** | Une image modifiée sur le disque se relit | **100 %** | Chaque image du cache garde l'empreinte de son fichier (date et taille, prise avant la lecture) ; l'ouverture oublie les retouchées, une image intacte reste la même instance, l'annulation ne relit rien. Recette dans `Scripts/recettes/` |
| **Q16** | Le pointillé d'un bloc image obéit aux repères | **100 %** | Dessiné seulement repères allumés et sans image affichée ; recette dans `Scripts/recettes/` |
| **Q17** | Le texte composé suit une collection remplacée | **100 %** | Accesseur fermé (`init`) sur `TextFrame.Spans`, `TextFrame.Paragraphs` et `TableCell.Spans` : le remplacement ne compile plus (CS8852), le sérialiseur les pose encore. Recette dans `Scripts/recettes/` |
| **Q18** | Paquets marqués « Legacy » | 0 % | Trouvé par Q12 : `Avalonia.Diagnostics` et xunit 2 ont des remplaçants ; aucun n'est vulnérable |
| **Q19** | Les autres collections ne se remplacent plus | 0 % | Trouvé par Q17 : `Page.Frames`, `GroupFrame.Children`, `Document.Pages` et les listes de repères ont un setter public que des `ItemsControl` lient sans l'écouter |
| **Q20** | La liste des polices garde la police du bloc | **100 %** | Trouvé par la relecture de C5b. La liste propose les polices du document absentes de la machine, libellées « (absente) », et la liaison ne renvoie jamais null au bloc. Recette dans `Scripts/recettes/` |
| **Q21** | Un tremblement ne déplace pas le bloc | 0 % | Trouvé par la relecture de Q14 : un pixel de mouvement pendant le clic, près d'une marge, fait sauter le bloc de 3 pt par l'aimantation. Repris en phase N (salve Saisie 3, avec Q22) |
| **Q22** | Un clic sur une poignée ne redimensionne pas | 0 % | Trouvé par la relecture de Q14 : une pression sans mouvement près d'un coin fait passer le bloc de 100 à 96 pt et empile un pas d'annulation. Repris en phase N (salve Saisie 3) : toujours présent sur un bloc tourné, et Maj arrondit l'angle sans mouvement |
| **Q23** | Sélectionner un bloc ne fige pas la fenêtre | **100 %** | La cause, mesurée : les trois sections du dock et les réglages de la barre contextuelle étaient des `ContentControl` liés au bloc seul, qu'une sélection vide vidait et que la pression suivante reconstruisait — mille visuels, vingt-quatre champs numériques —, dans la pression même ; la liste des polices n'y était pour rien. Ils sont construits une fois par sorte de bloc et gardés, masqués sans bloc de leur sorte (`Views/GabaritsGardes.cs`) : sur ATA 21, une pression sur un bloc non sélectionné rend la main en 30 à 52 ms au lieu de 107 à 136, l'image comprise, et n'ajoute plus qu'un visuel à la fenêtre. « Blocs de la page » ne défile plus que si la ligne du bloc n'est pas en vue ; le sélecteur de couleur refermé ne rend plus le focus à un champ de la barre, qui engloutissait son pas. Les deux tests du double-clic reprennent le geste entier et passent vingt fois de suite, chacun dans un processus neuf. Quatre tests, et un de la barre ; relu et jugé le 26 octobre 2026 : aucun défaut de l'application ; le chronomètre tombe sous une charge de 18 sur vingt cœurs sans être mou, et le juge ne le prend plus pour garde-fou ; un test du sélecteur de couleur refermé sans clic ; cinq sabotages au lieu d'un. Recette dans `Scripts/recettes/` |
| **Q24** | Annuler ou Rétablir ne laisse plus une saisie hors de toute transaction | **100 %** | `Undo` et `Redo` referment ce qui est ouvert, comme avant, puis lèvent un avis sur chaque chemin, rien à défaire compris (`MainWindowViewModel.Transactions.cs`), sans rien rouvrir eux-mêmes : chaque propriétaire encore actif rouvre sa propre transaction, et lui seul — la saisie sur la page si elle l'est toujours, le champ du dock ou de la barre s'il a toujours le focus, et deux que le plan ne nommait pas, le glissé dont le bouton est tenu et le sélecteur de couleur resté ouvert. Une annulation qui relit le document referme la saisie, et rien ne se rouvre pour elle. Les trois cas du plan se montraient, le déduit compris : un champ de la barre sous un `Ctrl+Z` sans rien à annuler, une marge de l'onglet Document sous une vraie annulation ; les champs du bloc, eux, perdent le focus à une annulation, la sélection se vidant en passant. Neuf tests, dont sept tombaient sur le code d'avant ; le test du double-clic de Q14 n'est plus joué à froid par la recette, où il tombait sous la charge de la machine, avant Q24 comme après ; relecture et juge après fusion. Recette dans `Scripts/recettes/` |
| **Q25** | Un réglage fait pendant une saisie a son propre pas | **100 %** | Confirmé d'abord par un test : la transaction de la saisie s'ouvrait au double-clic et ne se refermait qu'à Échap, et le corps réglé à la barre, le retrait saisi au dock ou la couleur prise au sélecteur pendant la frappe se fondaient dans son pas. Elle suit désormais le focus de la zone de saisie sur la page — ouverte quand la zone le prend, refermée quand elle le perd, et à `EndTextEdit` ; la saisie reste ouverte quand on clique dans la barre ou le dock, et les boutons G, I et S, qui ne prennent pas le focus, gardent `UnGesteUnPas`. La réponse de la planche à l'avis de Q24 suit le même focus : sous un `Ctrl+Z` sans rien à annuler, seul le champ qui a le focus rouvre sa transaction. Le sélecteur de couleur referme la sienne dès que son volet commence à se refermer, avant de rendre le focus à la zone, qui rouvrait la sienne dedans. Cinq tests, dont quatre tombaient sur le code d'avant ; relecture et juge après fusion. Recette dans `Scripts/recettes/` |
| **I1** | Apparence de l'interface | **100 %** | `Views/Apparence.axaml` — pas « charte », qui nomme le jeu du document —, chargée par `App.axaml` : les 26 couleurs de l'interface de la maquette, une fois chacune et sous leur nom ; cinq classes aux tailles et aux couleurs de la feuille de style de la maquette, dont le bouton-icône de 30 × 30 plein d'accent quand il est actif ; 35 icônes au tracé de la maquette, à la couleur courante, une par emploi. L'écran d'aujourd'hui ne change pas d'un pixel. La recette tient la liste des vues de la phase et le poste de chaque icône, que les postes suivants complètent ; une vue qui cite l'apparence ou écrit en dur une de ses couleurs sans être dans la liste la fait échouer. Recette dans `Scripts/recettes/` |
| **I2** | Disposition de la fenêtre | **100 %** | La fenêtre de la maquette, de haut en bas. Barre de menus : la marque, le titre du document centré, deux menus neufs — Texte (gras `Ctrl+G`, italique `Ctrl+I`, les quatre alignements, « Importer un te_xte… ») et Fenêtre (les deux panneaux, et B7). Barre principale à icônes : Annuler et Rétablir grisés sur un historique vide, mais pas pendant une saisie qui a déjà changé le bloc ; « Page entière » posé pour I6. La place de la barre contextuelle, de hauteur constante, et sa pastille, dans son propre fichier pour I3. Rail d'outils, sans Main, Zoom ni Ligne avant leur geste. Panneau Pages, dont l'entrée est un contrôle à part pour I5. Dock à quatre onglets, sections repliables retenues le temps de la session, longueurs en millimètres, vignettes de Fond et contour, « Blocs de la page » l'avant en tête. Barre d'état : la page, le format, le zoom en échelle logarithmique. Chaque champ, chaque onglet et chaque geste de la fenêtre fait un seul pas d'annulation ; les vingt-six icônes du poste servent. Repris après relecture : quatorze défauts, tous refermés ; jugé le 26 octobre 2026, sur la fenêtre de I3, I5 et I6 : vingt-cinq sabotages, vingt-cinq pris par leur garde-fou. Recette dans `Scripts/recettes/` |
| **I3** | Barre contextuelle | **100 %** | La barre suit la sélection, à la suite de la pastille de I2. Pour un bloc de texte : la police avec la garde de Q20, le corps, le gras, l'italique, le souligné — bouton S, `Ctrl+U` et « Souligné » au menu Texte sur la commande de I7, qui retire le souligné quelle qu'en soit la sorte ou pose le trait simple, et un chevron qui propose le double, l'épais et « mots seuls » —, les quatre alignements, la droite comprise, l'interligne en multiple, et la couleur du texte, dont la vignette ouvre le sélecteur du jeu ; un tableau a la même mise en forme, sans l'alignement ni le souligné ; un groupe se dissocie, plusieurs blocs s'alignent et se répartissent ; une image, une forme ou la page n'ont que la pastille. Ce que la barre porte a quitté le dock : la section Caractère, l'alignement et l'interligne de Paragraphe, la mise en forme du tableau. Chaque bouton joue la commande du menu Texte sans prendre le focus et ne se coche que par le bloc, si bien que chaque clic fait son pas, même pendant une saisie ; le sélecteur de couleur, ouvert hors de la barre, tient sa propre transaction ; `Échap` quitte un champ de la barre et rend les lettres seules aux outils. Un clic sur « Centrer le texte » centre le bloc à l'écran et au PDF en un pas. Treize tests ; relu et jugé le 26 octobre 2026 : les commandes de la barre — Aligner, Répartir, Dissocier — ne prennent plus le focus et font leur pas au clic, au lieu de l'attendre et d'y fondre la flèche suivante ; « Souligné » ne s'offre plus sans bloc de texte ; neuf sabotages au juge, le poste n'en avait aucun. Repris après le contrôle indépendant : deux de ces tests tombaient trois fois sur quatorze en suite entière — un clic dans un menu tout juste ouvert, ou sur un bouton que la barre venait d'échanger, cherchait sa cible dans la dernière image rendue, où elle n'était pas encore ; le banc, pas le produit. Le clic au centre d'un contrôle n'a plus qu'une aide, `Rendu.Cliquer`, pour les trois classes qui en avaient chacune une copie : elle attend que l'image montre le contrôle sous son centre, et échoue en nommant ce qui le couvre. Recette dans `Scripts/recettes/` |
| **I4** | Planche : règles fixes, projection, bulle de dimensions | **100 %** | La planche de la maquette autour d'une page qui ne change pas d'un pixel : fond `#DCDFE4` et ombre double de la feuille ; règles fixes au bord de la zone visible, graduées de part et d'autre de la page — négatives avant elle, en Inter —, leur zéro lu sur `PageRoot` à tout zoom ; projection de l'emprise de la sélection en accent à 14 %, suivie pendant le geste ; repères libres tracés sur toute la planche, retirés dès qu'on les lâche hors de la page ; marges roses d'un pixel, cadre à 4 px qui tourne avec le bloc, poignées blanches bordées d'accent ; bulle de dimensions au tracé et au redimensionnement, d'accord avec la barre d'état. La couche de saisie couvre la planche — Q10 absorbé —, mais blocs et outils s'arrêtent au bord de la page. Repris après relecture, mesuré aussi à l'échelle 2 et à quatre zooms non entiers. Recette dans `Scripts/recettes/` |
| **I5** | Vignettes de pages | **100 %** | Chaque page du panneau Pages en vignette de 96 px au rapport de la page, son numéro dessous, la page courante bordée de 2 px d'accent ; un clic y mène, sans rien mettre à l'historique. `Views/VignetteDePage.cs` dérive de la vignette des modèles : un schéma tiré de la composition, sans contrôle de bloc, qui dessine sa page — et non la première — avec le fond de page. Elle suit le document qui se modifie en place : elle écoute sa page bloc par bloc, comme la planche, et ne recompose que les filets du bloc retouché ; une annulation rend ses pixels. Sur ATA 21, redessiner les vignettes prend 14 ms après une retouche du plus long texte, 43 ms quand le jeu de polices fait tout recomposer. Les sept cibles du juge dans `TemplatePreview.cs` restent, `DetachementTests` et `VignetteTests` passent inchangés. Dix tests ; recette dans `Scripts/recettes/`. Relu et jugé le 26 octobre 2026 : un Ctrl + clic sur la vignette courante ne laisse plus la fenêtre sans page courante ; la recette ne lit plus les cibles du juge sous le juge, où elle échouait sous chaque sabotage et le rendait aveugle à ceux qui ne nomment pas leur garde-fou ; cinq sabotages au juge, le poste n'en avait aucun |
| **I6** | Main, zoom, page entière, proportions | **100 %** | La main du rail, ou `Espace` tenu hors saisie et hors bouton, fait glisser la planche — son seul défilement, sans bloc touché ni pas d'annulation —, main ouverte puis fermée, deux curseurs dessinés aux pinceaux de l'apparence. La loupe (`Z`) zoome d'un pas au clic, en arrière sous `Alt`, le point visé restant sous le pointeur ; `Ctrl`+molette de même, prise avant que la zone qui défile ne la consomme. « Page entière » tient la page dans la zone, marge de la planche déduite, mesurée après la mise en page, qui arrondit au pixel supérieur ; jamais d'office. « Conserver les proportions », état de la session : cochée, un coin tiré et la saisie de L ou de H — qui passent désormais par le modèle de vue — gardent le rapport du bloc, les côtés non, et `Maj` pendant le glissé fait l'inverse. Les deux icônes du poste servent. Dix tests ; relu et jugé le 26 octobre 2026 : une prise de la main suivie d'un clic au même point ne fait plus un double-clic qui ouvrait la saisie du bloc ; sept sabotages au juge, le poste n'en avait aucun. Recette dans `Scripts/recettes/` |
| **I7** | Souligné | **100 %** | Le souligné porte sa sorte — plein, pointillé, tiret, tiret long, tiret-point, tiret-point-point ou vague ; simple ou double, épais ou non, « mots seuls » — au bloc, au fragment, où `null` laisse celui du bloc, et dans le style effectif. Il s'écrit `false`, `true` pour le trait simple, ou en objet `{ Line, Double, Thick, WordsOnly }` ; `FormatVersion` ne change pas, Samples et Templates sont réenregistrés. Il se trace à l'écran et au PDF à la place et à l'épaisseur de la table `post`, sous la ligne de base de son morceau, exposant compris, et à la couleur de son texte ; seize des dix-sept sortes de libmspub s'importent, le double en double. La commande `SoulignerCommand` est dans `MainWindowViewModel.Souligne.cs`, sans bouton : le bouton S, `Ctrl+U` et le menu viennent de I3. Recette dans `Scripts/recettes/`, vingt-six sabotages au juge |
| **I8** | Masquer un bloc | **100 %** | L'œil de « Blocs de la page », à la place que I2 avait réservée : un clic masque le bloc, ou le montre, en un pas d'annulation, même pendant une saisie, sans le sélectionner ni prendre le focus ; le document est « modifié ». `Visible` sur tout bloc, `true` par défaut ; `FormatVersion` ne change pas, Samples et Templates sont réenregistrés — un « Visible » de plus par bloc, rien d'autre. Masqué, le bloc n'est dessiné ni dans `PageRoot` ni au PDF, ni ce que porte un groupe masqué ; rien de lui n'avertit, ni sa police (Q8), ni son image, ni son débordement ; son filet n'élargit plus la découpe d'un groupe. Un clic à sa place prend le bloc d'en dessous ; l'alignement et la répartition ne le voient pas, « Tout sélectionner » non plus. Il reste dans la liste, l'œil fermé, et s'y sélectionne, sans cadre ni poignée sur la planche ; masqué, il quitte une sélection multiple. Une recopie le garde, dissocier un groupe masqué laisse ses blocs masqués ; les cases d'un tableau ne portent pas la propriété. La vignette de la page (I5) ne le dessine pas. L'icône du poste sert. Vingt-cinq tests ; relecture et juge après fusion. Recette dans `Scripts/recettes/` |
| **I9** | Compter les lignes en excédent | **100 %** | `Compositeur.LignesEnExcedent`, fonction pure du noyau : les lignes dont l'encre — la hauteur naturelle, au milieu de l'avance — sort du bloc, au calage vertical (A7), au bord du bloc et non de la zone (C6), au seuil du marqueur de A2 ; les colonnes et les blocs tournés sans chemin à part. Le dock l'affiche à la section « Cadre de texte », dans la place que I2 avait réservée : « Texte en excédent : 4 lignes » au fond, à la couleur et à l'icône d'alerte de l'apparence, au singulier pour une ligne, rien à zéro ; il suit la frappe et chaque réglage du bloc sans changement de sélection, et ne touche pas au document. Le modèle de vue le porte dans `MainWindowViewModel.Excedent.cs`. Aucune clé sérialisée ; l'avertissement d'export et le relevé de P2 inchangés. Sur `recette-debordement.mypub` et sur une variante en colonnes, marges, interligne 1,5 et rotation, le compte du dock est celui des lignes absentes du PDF, bloc par bloc, lu par la commande `excedent` du banc. Vingt-deux tests, treize purs et neuf sur la fenêtre ; recette dans `Scripts/recettes/`. Relu et jugé le 26 octobre 2026 : à un interligne serré, le compte ne prend plus pour perdue une ligne que le marqueur laisse en paix ; trois tests de la fenêtre pour ce que le compte compose et écoute ; dix-sept sabotages au juge |
| **N1** | Le banc des gestes | **100 %** | Les gestes des tests d'interface sont synthétisés à un seul endroit, la région « Les gestes » de `Rendu.cs` : vingt-trois aides — `SurLaPage`, à l'échelle de la planche, qui échoue sans `PageRoot` ; `Centre`, `Coin`, `Poignee`, la poignée de rotation et son centre ; `Cliquer` sur un contrôle, qui attend l'image, ou en un point, touches, bouton et mouvement nul de Q14 compris ; `CliquerDroit`, qui rend le `ContextMenu` ouvert ; `DoubleCliquer` ; `Glisser`, toujours bouton tenu ; `Touche`, `ToucheAzerty`, `Taper`, `Saisir` ; `ZoneDeSaisie` et `EntrerEnSaisie`, le seul endroit que N12 changera ; `BordDuCaractere`, `SelectionnerDuTexte`, `TexteCopie`, `UnSeulPas`, `SousLaFenetre`. Quarante et une copies retirées de dix-sept classes, puis dix des classes de Q24, Q25 et N4 à la fusion, quatre aides de classe réécrites sur celles du banc, un homonyme renommé, les deux entrées en saisie écrites en ligne remplacées ; les défauts des copies corrigés — deux glissés et une rotation sans le bouton tenu, deux clics sans l'attente d'image, dix `SurLaPage` qui retombaient sur (0, 0), deux `UnSeulPas` qui ne vérifiaient pas que le pas change le document — sans qu'aucun test change d'issue ni qu'un `poste=` perde un test. `BancTests`, dix tests sous `poste=N1`, éprouvent ce qu'Avalonia 11.3.9 sans affichage permet : le clic droit et la touche Menu, le presse-papiers et un format propre, `Ctrl+C`, le glissé dans un `TextBox` et dans la saisie d'un bloc, l'AZERTY, le double-clic à 3 px. Un contrôle des sources, `Scripts/aides-de-geste.py`, refuse toute aide de geste hors du banc ; la recette de Q23 lit le clic du banc. Trois sabotages au juge. Recette dans `Scripts/recettes/` ; relecture et juge après fusion |
| **N2** | Mettre en forme une plage, dans le noyau | **100 %** | `MiseEnForme`, fonctions pures du noyau sur le modèle de `Recalage` : `Lire` (la valeur commune de chaque propriété, nulle si mixte, police et couleur comparées résolues par la charte), `Allume`, `Poser`, `Basculer` (Gras, Italique, Souligné ; retirer pose la valeur éteinte, `Bold = false` sur un bloc gras) et `Retirer` (`Ctrl+Espace`), sur les fragments et, à l'échelle du paragraphe, sur les écarts (`Basculer` d'un fer, `Paragraphes` d'une sélection) ; une pose coupe aux bornes par `Clone`, ne tranche « le dernier l'emporte » que dans la plage, rend nulle une valeur égale au bloc comparée brute (`scheme:accent1`), recolle les voisins identiques et retire ce qui ne dit plus rien ; l'écriture passe par les opérations de la collection, sans fragment modifié sur place, et une pose sans changement à l'œil n'écrit rien. `Recalage.ParagrapheDe` et `NombreDeParagraphes` publics ; `StyleEffectif.Etendue` et `ParagrapheEffectif.Etendue` partagées avec la composition. Rien de l'application ne l'appelle encore : R6 et R7. Vingt-six tests purs, dont deux balayages de deux mille poses ; recette dans `Scripts/recettes/`, deux sabotages au juge. Relecture et juge après fusion |
| **N3** | Chaque morceau composé sait d'où il vient | **100 %** | `Morceau` porte en dernier membre `Debut`, la position de son premier caractère ; `LigneComposee` sa plage `Debut`–`Fin`, blancs rognés compris, saut exclu, et son `Paragraphe`, compté comme `Recalage` le compte. `Decouper` lit le texte tel qu'il est, sans `Replace` : seule exception, déclarée, un « \r\n » que deux fragments se partagent ne fait plus qu'un paragraphe — aucun document du dépôt n'en porte. Deux fonctions pures du noyau, `Compositeur.Abscisse` et `PositionLaPlusProche`, taquets, justification et blancs pendants compris ; `Compositeur.LigneDuTexteVide` pour le curseur d'un bloc vide. La vue pose, dans la passe même du dessin, une `TexteCompose.LignePosee` par ligne, lignes vides comprises ; `TexteCompose.Geometrie()` les rend avec `PointDe(index)` et `IndexSous(point)`, en coordonnées du bloc, colonnes et calage compris, rien pour un bloc à champ. **La sortie ne change pas, mesuré** : l'empreinte de la composition (`Scripts/empreinte-composition.sh`, commande `empreinte` du banc) — 2 492 lignes à la règle de l'écran et du PDF, 13 880 morceaux au bit près, ce que l'écran pose et souligne, les pixels de 29 pages, le PDF exporté et ses 6 634 mots — est identique avant et après sur les 18 documents du dépôt : `Samples/`, `Templates/`, ATA 21 et les Fiches. 76 tests sous `poste=N3`, 66 purs et 10 sur la vraie fenêtre ; un sabotage au juge. Recette dans `Scripts/recettes/` ; relecture et juge après fusion |
| **N4** | L'affichage au clavier | **100 %** | `F9` bascule entre l'affichage courant et la taille réelle, autour du centre de la sélection — ou du milieu de la zone visible —, qui garde sa place à l'écran ; le second `F9` rend l'affichage d'avant, zoom et défilement, tant qu'aucun autre geste n'a changé le zoom. `Ctrl+Maj+L` joue « Page entière ». La valeur du zoom de la barre principale est un champ (`Views/ChampDuZoom.axaml`) à l'allure du libellé de la maquette tant qu'il n'a pas le focus : un clic y entre et déroule la liste de Publisher — Page entière, Objets sélectionnés, de 400 à 10 % — ; un pourcentage part à Entrée, Échap renonce et rend la planche, un zoom hors de 10 à 800 % est refusé avec un message ; aucune transaction. « Limites et repères » (`Ctrl+Maj+O`) est la case « Repères » d'avant ; « Repères de lignes de base » (`Ctrl+F7`), `ShowBaselines`, ne masque que les lignes de base. Le menu Affichage montre ses gestes. Le bureau (GNOME 50.1 sous Wayland, sans KDE) ne prend aucune des quatre touches — seulement `Alt+F7`, `Ctrl+Alt+F7` et `Ctrl+Alt+F9`. Dix tests ; relecture et juge après fusion. Recette dans `Scripts/recettes/` |
| **N5** | Quitter la saisie d'un clic qui agit | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 2 |
| **N6** | Les raccourcis de la fenêtre laissent la touche au champ | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 2 |
| **N7** | Mesurer l'écart de la première voie, et s'arrêter | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 2. **Arrêt** : la voie de R7 revient au propriétaire |
| **N8** | Couper, copier, coller des blocs | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 2 |
| **N9** | La langue du texte | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 2 |
| **N10** | Les raccourcis de Publisher en français | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 3 ; écrit la fiche 24 |
| **N11** | Ne rien perdre en fermant, retrouver ses documents | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 3 |
| **N12** | Entrer dans le texte d'un clic, le curseur au point visé | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 4 |
| **N13** | Les menus de Publisher | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 4 |
| **N14** | Les champs | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 4 |
| **N15** | L'application connaît la sélection du texte | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 5 |
| **N16** | Tabulation et saut de ligne forcé | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 5, après N15 dans la même copie |
| **N17** | Les modificateurs du glisser, et le rectangle de sélection | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 5 |
| **N18** | Les menus vers le dock, et les pages, avec A8 et A9 | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 5 |
| **N19** | Le corps et la police : une liste, et une frappe | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 5 |
| **N20** | Rechercher et remplacer | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 6 |
| **N21** | La saisie montre le texte composé | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 6 ; attend la décision de voie |
| **N22** | Les caractères spéciaux | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 7 |
| **N23** | Puces et numérotation | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 8 |
| **N24** | Exposant, indice, capitales | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 9 |
| **N25** | La vérification orthographique | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 9 ; dépendance neuve à éprouver d'abord |
| **N26** | Taper dans le texte d'un groupe, et prendre un membre seul | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 10 |
| **N27** | Taper dans une case, sur la page | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 11 ; revient sur un choix écrit en C9 |
| **N28** | Lignes et colonnes à l'endroit choisi, et plusieurs cases à la fois | 0 % | Spécifié le 25 octobre 2026 ; salve Saisie 12 |
| **D** | Moteur de composition | — | Décision non ouverte ; à rouvrir quand A, B et C sont livrées |

**Par phase** — P : 4 sur 4 · A : 8 sur 15 · B : 4 sur 7 · C : 7 sur 9 · R : 5 sur 7 ·
Q : 20 sur 25 · I : 9 sur 9 · N : 4 sur 28 · D : non planifié.

Soit **61 postes sur 104**. Le 25 octobre 2026, la phase N en a ajouté vingt-huit, et la phase Q
deux (Q24, Q25), tirés de la relecture du cours sous l'angle de la saisie (fiche 23). Le
24 octobre 2026, la phase I en avait ajouté neuf, tirés de la
maquette de la nouvelle interface. Le compte avait doublé le 11 octobre 2026 sans que rien ne
recule : il porte désormais les onze postes de l'audit des fiches (A8 à A15, B6, B7,
C8), deux postes que les documents réels demandaient (R6, R7), et la phase Q, qui
recense les défauts que les postes et les relectures ont **trouvés et consignés sans les
corriger**. Ces derniers étaient écrits, mais dispersés dans la prose de leurs postes :
rien ne les comptait, et `Scripts/bilan.sh` ne lit que ce tableau.
C'est un compte de postes et non d'effort : C3 pesait à lui seul plus que toute la
phase A, et les préalables ne produisaient aucune fonctionnalité.
Le tableau dit ce qui est clos, pas ce qui reste à peiner.

---

## P — Préalables

Deux choses à faire **avant** la phase C, pas après. Elles ne produisent aucune
fonctionnalité ; elles rendent le reste réversible.

### P1 — Mettre le dépôt sous git

Le projet n'est pas versionné (`git init` n'a jamais été fait). La phase C remonte des
propriétés de classe en classe et rend le rendu récursif : ce sont exactement les
changements qu'on veut pouvoir défaire. Un plan qui les programme sans historique est
un plan sans annulation — alors même que l'application, elle, en offre 60 pas.

*Recette* : `git log` montre un commit initial contenant l'état actuel.

*Fait le 5 septembre 2026* — dépôt initialisé (`main`), commit initial `cc098e0`,
52 fichiers. Les sorties de compilation et les réglages Rider sont écartés par
`.gitignore` ; les deux guides PDF de `Books/` sont suivis.

### P2 — Un contrôle de non-régression minimal

Il n'y a pas de projet de test. Sans aller jusqu'à une stratégie de test, il faut au
moins un garde-fou sur la chaîne qui compte :

1. ouvrir `Samples/exemple-bulletin.mypub`,
2. l'enregistrer sous un autre nom, comparer les deux JSON — ils doivent être identiques,
3. l'exporter en PDF, relever le nombre de pages et la liste des avertissements.

Le troisième point se formule comme un **écart** et non comme une absence : on compare
au relevé de référence pris sur l'état actuel. Un contrôle qui exige zéro avertissement
tomberait dès qu'un document de référence porte une image liée manquante — et un garde-fou
qui échoue sur du code intact ne sert plus à rien. Le document d'exemple actuel ne
contient d'ailleurs aucune image (15 blocs de texte, 8 formes) : la référence est
aujourd'hui vide, elle ne le restera pas forcément.

Trois étapes scriptables en une trentaine de lignes, à passer avant et après chaque
poste de la phase C. C'est le filet, pas la ceinture.

*Recette* : le script tourne sur l'état actuel et produit un relevé de référence ; il
sort en code 0 tant que le relevé ne change pas.

*Fait le 5 septembre 2026* — `Scripts/controle.sh`, référence dans
`Scripts/reference.txt` (2 pages, aucun avertissement). L'étape 2 a demandé un point
d'entrée sans interface, `--resave`, et vérifié son utilité dès le premier passage :

- l'encodeur JSON par défaut échappait tout caractère non-ASCII et l'apostrophe — un
  document français enregistré par l'application sortait en `l\u0027unit\u00E9`, contre
  la lisibilité que le format revendique. Corrigé dans `DocumentSerializer`.
- `Samples/exemple-bulletin.mypub` avait été écrit à la main et non par le sérialiseur.
  Il est régénéré par lui, à données égales, pour que l'étape 2 reste l'égalité stricte
  demandée ici plutôt qu'un écart toléré. L'ordre d'écriture est depuis déclaré dans le
  modèle, de dix en dix, ce qui rend au fichier l'ordre lisible qu'il avait à la main.

Le contrôle a été éprouvé en le faisant échouer : JSON écarté du sérialiseur, compte de
pages différent, bloc image au chemin inexistant.

### P3 — Un banc de vérification permanent

P2 tient la chaîne d'écriture, et rien d'autre. Or les recettes de C1, C2a, C2b, C3c et
C6 ont toutes été jouées avec un outillage monté puis jeté : les mots
`RenderTargetBitmap`, `pdftoppm` et « pointeur synthétisé » ne se trouvent nulle part
ailleurs que dans la prose de ce fichier. Aucune de ces recettes n'est rejouable, et
rien n'arrêterait quelqu'un qui casserait l'aimantation, la composition des rotations
d'un groupe ou la fidélité écran/export.

Sept pièces : le noyau séparé de l'application (`MyPublisher.Core`), l'export rendu
déterministe, les fonctions pures sorties de la vue, un banc qui rend l'écran hors champ
(`MyPublisher.Banc`), la comparaison écran ↔ PDF (`Scripts/comparer-rendus.sh`), une
recette exécutable par poste — **y compris pour les postes déjà clos** — et un bilan
machine qui dit ce qui reste ouvert.

*Recette* : saboter volontairement l'aimantation, la mise à l'échelle des groupes, une
propriété dans un `Clone()` et une étape de migration — chaque sabotage doit être
attrapé.

*Recette jouée le 6 septembre 2026*, et elle est désormais un script :
`Scripts/eprouver-le-juge.sh`. Sept sabotages, sept attrapés. **Elle a servi dès le
premier passage** : rien n'attrapait un groupe qui cesse de mettre ses blocs à
l'échelle. C'était précisément la recette de C3c — jouée à la main en comparant des PDF
au pixel près, puis jetée. Comblée par `GroupeTests`, qui éprouve aussi qu'un groupe
réduit à zéro ne déchire pas ses blocs en `NaN`, et que passer du repère de la page à
celui du groupe puis revenir rend exactement le même point, rotation comprise.

L'exhaustivité de `Clone()` est éprouvée **par réflexion** plutôt qu'en énumérant les
propriétés à la main — énumérer reproduirait exactement l'oubli qu'on cherche à
empêcher. Toute propriété qui s'écrit dans le fichier doit survivre à une copie ; celle
qu'on ajoutera demain est donc couverte le jour où elle est écrite.

*Fait en partie le 6 septembre 2026* — le noyau, le banc et la comparaison.
`Models` et `Services` ne référençaient déjà aucun type Avalonia : le découpage n'a
déplacé aucune ligne, il a matérialisé une séparation que le code respectait. Le banc
ouvre la vraie fenêtre sans affichage, sur Skia, et rend la page à deux pixels par
point — la même échelle que `pdftoppm -r 144`, sans facteur de conversion.

Deux pièges silencieux, tous deux gardés désormais : `PageRoot` est mesuré en points
mais vit sous la mise à l'échelle du zoom, si bien que le rendre directement dessinait
son contenu 4/3 trop grand dans une image à la bonne taille — un bandeau de 118 pt
sortait à 157 pt sans que rien ne le signale ; et un écart **nul** est refusé au même
titre qu'un écart énorme, puisqu'il voudrait dire qu'on compare une image à elle-même.

*Bilan écrit le 11 octobre 2026* — `Scripts/bilan.sh` lit le tableau de suivi et
cherche, pour chaque poste, la recette qui le rejoue ; `Scripts/verifier.sh` en imprime
la conclusion à la fin de chaque passage. Le premier passage compte **sept postes clos
sans recette rejouable** : B1, C1, C2, C3, C4, C6 et R4. Ce sont, à B1 et R4 près, ceux
que le premier paragraphe de cette section nommait déjà. Certains sont tenus en partie
par les tests — `GroupeTests` pour C3c, `AimantationTests` pour C2 — mais un test qui
éprouve un calcul n'est pas une recette qui rejoue le geste du poste. P3 reste donc
ouvert sur cette seule dette.

Le même jour, le banc est passé à .NET 10 et le dépôt a quitté le partage réseau pour
un disque local (`~/src/MyPublisher`, le partage restant le dépôt distant). Sur la
machine où .NET 9 n'était plus installé, **aucun** test ne se lançait ; sur le partage
CIFS, aucun script ni le binaire compilé ne s'exécutait, faute de bit d'exécution. Les
304 tests et le contrôle repassent tels quels sous .NET 10, sans une ligne de code
changée.

#### Les sept recettes qui manquent

Chacune reprend le critère que son poste avait écrit, et le rend rejouable. Le nom du
script commence par le code du poste : c'est ainsi que `Scripts/bilan.sh` le reconnaît.
Là où le geste a été joué au pointeur synthétisé, la recette appelle les tests
d'interface qui le jouent déjà — filtre `dotnet test --filter`, comme celles de A4 et
A6 — plutôt que de le réécrire en shell.

| Script | Ce qu'il mesure |
|---|---|
| `B1-modeles.sh` | Les quatre modèles s'ouvrent en copie, sans chemin ; le fichier du modèle reste identique octet pour octet après une retouche enregistrée ailleurs ; les quatre s'exportent **sans un avertissement** |
| `C1-fond-et-filet.sh` | Une image encadrée d'un filet de 2 pt, sans forme superposée : l'écran et le PDF concordent (`comparer-rendus.sh`), et la couleur du filet se relève sur le bord |
| `C2-reperes.sh` | Les tests d'aimantation et de repères au pointeur synthétisé passent ; le même document exporté avec la grille la plus dense et sans grille donne deux images identiques au pixel près, avec et sans repères libres |
| `C3-selection-groupe.sh` | Quatre blocs alignés à gauche ont le même X ; groupés, déplacés de (30, 20) pt puis exportés, chacun se retrouve exactement déplacé de (30, 20) dans le PDF (`pdftotext -bbox`) |
| `C4-champs.sh` | Un folio sur huit pages se lit 1 à 8 page par page dans le PDF (`pdftotext`), posé seul **et** posé dans un groupe ; les coordonnées du jeu actif, et de lui seul, apparaissent dans le PDF d'un modèle de B1, chacun de trois jeux actif à son tour |
| `C6-marges.sh` | 12 pt de marge à gauche seulement : le fer gauche est décalé de 12 pt, le fer droit ne bouge pas ; 20 pt en haut seulement : le texte descend de 20 pt ; 12 pt à droite seulement : le fer droit recule de 12 pt — à l'écran comme au PDF, à 0,05 pt près |
| `R4-interligne.sh` | Un bloc de neuf lignes à l'interligne 1,0 puis 1,5 : l'avance mesurée entre deux lignes est la même à l'écran et au PDF, et vaut le multiple de la hauteur naturelle |

Chaque recette reçoit au moins un sabotage dans `Scripts/eprouver-le-juge.sh`. Une
recette qu'on n'a jamais vue échouer ne prouve rien. P3 passe à 100 % quand le bilan
ne nomme plus aucun poste clos sans recette.

#### C1, C2 et C3 rejouées

*Fait le 11 octobre 2026* — trois recettes, et le bilan ne nomme plus C1, C2 ni C3.

- `C1-fond-et-filet.sh` fabrique sa page : une image carrée cadrée pour remplir son
  bloc sans se déformer (`UniformToFill`, le seul cadrage que l'export découpe), une
  image en hauteur qui laisse voir le fond jaune de son bloc, un bloc de texte à fond
  et à filet. Sur l'écran (le banc) et sur le PDF, au milieu de chacun des douze côtés,
  le pixel juste à l'intérieur du bord porte la couleur du filet — c'est ce qui prouve
  qu'il passe **sur** le contenu —, le filet fait exactement 2 pt (quatre pixels : 1,5
  ou 2,5 pt tombent), et le PDF le centre sur le bord au quart de point. Les fonds et
  les images se relèvent à leur place, la photo ne déborde pas de son bloc, puis
  `comparer-rendus.sh` passe.
- `C2-reperes.sh` appelle sept tests au pointeur synthétisé (`ReperesTests`, trait
  `poste=C2`) : les quatre blocs lâchés près d'un bord de colonne de la recette de C2a,
  Alt qui désarme, un repère tiré de la règle du haut, aimanté sur une colonne, repris,
  retiré hors page, un repère horizontal tiré de la règle de gauche, aimanté sur un
  bord de ligne puis retiré, et un bloc posé exactement sur un repère à 333,3 pt —
  chaque geste annulable. Puis le document d'exemple, exporté sans grille, avec la grille la plus
  dense que le panneau laisse régler (24 colonnes, 24 lignes, une ligne de base par
  point) et avec des repères libres en plus : zéro pixel d'écart, page par page. La
  contre-épreuve rend les mêmes documents à l'écran, repères allumés, et exige qu'ils
  y diffèrent — sans quoi l'égalité des PDF pourrait tenir à une grille perdue.
- `C3-selection-groupe.sh` joue le geste entier (`GroupementTests`, trait `poste=C3`) :
  un clic et trois Maj-clics, « aligner à gauche » entre eux, Ctrl+Maj+G, puis le
  groupe glissé de (30, 20) pt. Le test écrit le document groupé puis déplacé ; la
  recette les exporte et mesure chaque mot par `pdftotext -bbox` : un même fer à
  154 pt, puis exactement (30,000 ; 20,000) pt de décalage pour chacun. Le test défait
  ensuite les trois gestes, un pas chacun, jusqu'aux abscisses d'origine.

Un sabotage par recette dans `eprouver-le-juge.sh` : le filet d'une image rangé sous
elle à l'écran, les repères libres retirés de la table d'aimantation, « aligner à
gauche » qui aligne sur le bord droit. **Les deux derniers ne tombaient sous aucun
test** : ni la commande d'alignement, ni celle du groupement, ni le geste d'un repère
libre n'étaient éprouvés nulle part. La relecture en a trouvé trois autres que rien
n'attrapait, et que les recettes attrapent depuis : la découpe d'une image
`UniformToFill` qui survit au filet du PDF et le rogne de moitié, la règle de gauche
qui ne pose plus de repère, l'alignement qui n'ouvre plus de pas d'annulation. Ils
sont au juge aussi.

*Ce que la recette de C1 a mesuré de Q4.* Sur les douze côtés, sans exception, l'écran
rentre le filet de 2 pt d'exactement 1,00 pt — une demi-épaisseur — là où le PDF le
centre. La recette l'exige ainsi : ce que C1 promet (la couleur sur le bord, côté
intérieur, et l'épaisseur) est demandé des deux côtés sans indulgence, puisque les deux
filets se recouvrent sur la moitié intérieure ; la position est demandée au quart de
point au PDF, qui suit la documentation ; à l'écran, l'écart au PDF est imprimé à
chaque passage et borné à ce que Q4 explique, de 0 à une demi-épaisseur vers
l'intérieur. La tolérance d'images (1,2 %) est calculée sur ce que Q4 coûte —
2 120 pt de pourtour décalés d'un point, 0,84 % de la page — et non relevée puis
arrondie. Q4 réparé, l'écart imprimé tombera à 0 : c'est sa recette, pas celle-ci, qui
resserrera alors l'exigence.

*Choisi sans certitude.* La « grille la plus dense » est celle que le panneau autorise
aujourd'hui, et non les 77 repères de la recette d'origine, qui n'en disait pas le
réglage. L'alignement de C3 est joué « par rapport aux marges » décoché, les blocs
entre eux : c'est le cas où le bord visé dépend de la sélection, donc le seul où un
mauvais bord se voit ; l'alignement sur les marges, réglage par défaut, n'est pas joué.

*Trouvé et laissé tel quel.* Le pointillé gris d'un bloc image — « repère visible tant
qu'aucune image n'est chargée », dit `PageCanvas.axaml` — est en réalité toujours
dessiné : une image cadrée sans déformation le laisse voir sur le bord du bloc, et il
n'obéit pas à l'interrupteur des repères. Il entre donc dans toute comparaison écran ↔
PDF (0,06 % de la page pour un bloc seul).

#### B1, C4, C6 et R4 rejouées

*Fait le 11 octobre 2026* — `B1-modeles.sh`, `C4-champs.sh`,
`C6-marges.sh` et `R4-interligne.sh`, chacune avec son sabotage à la fin de
`Scripts/eprouver-le-juge.sh`. Les gestes que le shell ne sait pas jouer sont dans deux
fichiers de tests d'interface, appelés par filtre et comptés comme ceux de A4 :
`ModelesTests` (`poste=B1`) frappe `Ctrl+N`, choisit le dépliant, part de lui sans son
chemin ; puis, pour chacun des quatre modèles, `Ctrl+N`, le choix dans le catalogue,
Entrée, une retouche et `Ctrl+S`. Le modèle éprouvé est donc celui que le catalogue lit,
dans le répertoire de sortie : chaque test le remet en état en sortant, parce qu'un
sabotage qui rendrait au document le chemin de son modèle l'écraserait, et que la
compilation suivante ne le remplacerait pas, la source étant plus ancienne que lui. Le
`Ctrl+S` n'est pas une seconde garantie : le test vérifie juste avant que le document
n'a pas de chemin, et sans chemin `Ctrl+S` passe par « Enregistrer sous », que la
plate-forme sans affichage referme. `ChampsTests` (`poste=C4`) change la page courante
par le modèle de vue — et non par un geste — et relit le contenu résolu que chaque bloc
reçoit pour cette page, non les pixels dessinés.

Ce que les recettes mesurent, et contre quoi. Le folio de C4 se lit au PDF page par page
**à sa place**, posé seul et dans un groupe — trouver « 1 » à « 8 » quelque part dans le
texte passerait sur un folio qui suivrait l'ordre de lecture —, la date sous une locale
anglaise, et les coordonnées d'un fichier à trois jeux : la carte de visite s'exporte
trois fois, chaque jeu actif à son tour, et seuls ses six champs doivent paraître, aucun
des douze autres, son logo relevé au pixel à sa couleur. Un code qui prendrait le premier
jeu, ou le dernier, tombe sur l'un des trois exports. C6 pose six blocs : quatre pour la
marge gauche, dont deux calés à droite — c'est le fer droit qui distingue quatre marges
d'une marge unique appliquée des deux côtés —, un avec 20 pt en haut seulement, comme la
recette jouée à la main le 5 septembre, et un calé à droite avec 12 pt à droite. La
marge du bas n'est pas éprouvée : sous un calage en haut, elle ne déplace rien. R4 ne lit pas la hauteur naturelle dans le code qu'il éprouve, mais dans la
table `hhea` du fichier de police que fontconfig désigne — 13,96875 pt pour DejaVu Sans
12 —, puisque les deux rendus appellent le même `Compositeur.Avance` et que les comparer
entre eux prouverait leur accord, jamais leur justesse. Mesuré au PDF : 13,9688 et
20,9531 pt, pour une tolérance de 0,05 pt. Pour C6, des deux côtés : 12,000 pt de
déplacement du fer gauche et 0,000 pt du fer droit sous la marge gauche, 20,000 pt du
haut sous la marge du haut, −12,000 pt du fer droit sous la marge droite.

Les deux recettes à l'encre mesurent à **quatre** pixels par point, au centre de l'encre
pondéré par la noirceur et non à son bord : le plan demande 0,05 pt, soit un dixième de
pixel à l'échelle ordinaire. À deux pixels par point, le calage des glyphes sur la grille
coûte jusqu'à 0,06 pt sur l'avance de neuf lignes, et R4 échouerait sur du code juste.

Les sabotages ont été joués un à un contre les tests, le contrôle et **chaque** recette
avant d'être écrits. Ceux de C4 (le folio du PDF figé à 1) et de C6 (l'export retire la
marge gauche à la place de la droite) passent tout le reste et ne tombent que sur leur
recette. Celui de R4 — le défaut d'origine, l'écran qui avance du corps — passe les 311
tests et tombe sur les recettes de R2, R3 et R4. Celui de B1 tombe sur les tests
`poste=B1`.

La relecture en a trouvé deux que rien ne voyait : la carte de visite imprimant le
**dernier** jeu de coordonnées au lieu de l'actif (le fichier de la recette n'avait que
deux jeux, l'actif en second), et l'écran ignorant la marge du **haut** (la recette
n'éprouvait que la marge gauche). Les recettes de C4 et de C6 ont été reprises comme dit
plus haut ; chacun de ces deux sabotages a été joué à la main contre sa recette, qui
tombe, puis ajouté au juge.

**La recette de B1 a trouvé un défaut, corrigé dans un commit à part.** La carte de
visite s'exportait avec un avertissement dès qu'on y mettait un nom : le bloc « Nom »,
16 pt de haut en gras 12 pt à l'interligne 1,25, réclame 17,46 pt depuis R4. C'est la
retombée qu'A2 avait corrigée sur les documents d'exemple, et qu'elle ne pouvait pas voir
ici : le test des documents livrés mesure le texte *saisi*, vide sous un champ. « Les
quatre modèles s'exportent sans un avertissement » était donc faux depuis le 6 septembre.
Le bloc ne porte qu'une ligne : son interligne passe à 1, ce qui ne déplace aucun bloc.
Le rendu de la carte change pourtant : le demi-interligne que la composition place
au-dessus de la première ligne disparaît, et le nom **remonte de 1,75 pt** dans son bloc
(1,746 pt relevés par `pdftotext -bbox`). Sa boîte, qui chevauchait de 0,4 pt celle de
« Fonction », posée juste dessous, s'en écarte désormais de 1,4 pt. Le message du commit
du correctif ne le dit pas ; c'est ici qu'il est consigné.

*Choisi sans certitude* — corriger le modèle plutôt que de le signaler seulement : sans
cela la recette restait rouge, et la relâcher était exclu ; le correctif est isolé pour
pouvoir être repris. Le « jeu enregistré » de C4 est un fichier écrit par la recette au
format du dialogue des coordonnées, et non passé par ce dialogue. La concordance des
positions écran ↔ PDF est tenue à 0,5 pt, « au pixel » à deux pixels par point ;
l'avance et les marges, à 0,05 pt.

*Trouvé et laissé tel quel* — le banc n'est compilé ni par `dotnet test` ni par
`controle.sh`. Sur une copie neuve, A1 et A2 échouent faute de binaire au premier passage
de `verifier.sh` ; sous l'épreuve du juge, elles mesurent un banc **périmé**, compilé
avant le sabotage, jusqu'à ce que A4 recompile la solution. Les quatre nouvelles recettes
compilent elles-mêmes. Le texte de l'écran est posé 0,2 à 0,3 pt plus bas que celui du
PDF, quel que soit l'interligne — sous le pixel. Le bloc « Société » de la carte de
visite tient tout juste dans ses 16 pt. Des compilations avortent par intermittence sur
« Internal CLR error (0x80131506) » — quatre fois dans la journée, pendant que d'autres
copies de travail compilaient sur la même machine ; sous l'épreuve du juge, un tel échec
compte le sabotage pour attrapé, et c'est arrivé une fois à celui de R4, rejoué depuis à
part. Corrigé en revanche, parce qu'il empêchait de le voir : le juge ne savait nommer
aucune recette parmi ceux qui attrapent — il les cherchait en minuscules —, et disait
« attrapé par la compilation ».

*Réparé à la fusion de la salve.* `verifier.sh` compile d'abord toute la solution, banc
compris. Un plantage du compilateur y est rejoué une fois, puis signalé par le code de
sortie 3, que le juge ne compte plus comme une prise. Le juge nomme désormais **tous**
les tests et toutes les recettes en échec, et chaque sabotage de recette nomme la
recette qui doit l'attraper : attrapé par une autre seulement, il compte comme un trou.

#### Ce que le banc a trouvé le jour où il a servi

**L'interligne n'est pas appliqué à l'export.** `AbsoluteLineHeight`
(`Models/Frame.cs`) n'est lu que par l'écran et par les vignettes du catalogue ;
`PdfExporter.DrawText` ne le lit jamais et laisse `XTextFormatter` avancer à la hauteur
naturelle de la police. Mesuré sur un bloc de neuf lignes en DejaVu Sans 12 pt, par
différence entre deux et neuf lignes :

| Interligne | Avance à l'écran | Avance au PDF |
|---|---|---|
| 1,0 | 12,00 pt | 13,93 pt |
| 1,5 | 18,00 pt | 13,93 pt |

L'écran suit `FontSize × LineHeight` ; le PDF ne suit rien. Un réglage offert dans le
panneau ne franchit donc pas l'export — et le README annonce l'interligne parmi ce que
l'application sait faire.

**Tranché le 6 septembre 2026, par les documents réels** (voir
[`Documents-reels.md`](Documents-reels.md)) : aucun des deux `.pub` déposés ne porte
`fo:line-height`. La propriété existe dans libmspub et serait émise si Publisher stockait
un interligne particulier — les deux documents s'en tiennent donc au défaut de Publisher,
qui est la **hauteur naturelle de la police**. **C'est l'export qui a raison, et l'écran
qui a tort.** `LineHeight` doit devenir un multiple de la hauteur naturelle et non du
corps : à 1, l'écran ne doit rien imposer ; au-delà, il multiplie cette hauteur-là, et
l'export applique le même multiple à la même mesure.

**En revanche, les deux moteurs coupent aux mêmes mots.** Un paragraphe de sept lignes
dans un bloc étroit se brise exactement aux mêmes endroits des deux côtés, à deux pixels
près sur la largeur d'encre. C'est plus favorable que ce que le seuil D redoutait, et
cela réduit le risque de A7 : une fois l'interligne honoré, la comparaison écran ↔ PDF
devient un contrôle utilisable même sur une page de texte.

**Conséquence sur l'ordonnancement** : ces trois choses — interligne honoré, débordement
signalé, alignement vertical — demandent la même passe de composition dans
`PdfExporter`, et le dépôt tient à ce qu'il n'y ait qu'une seule mesure. Elles forment
un seul train, l'interligne en tête parce que c'est le seul des trois dont la recette
échoue déjà.

**Relevé de départ** : formes seules, écran contre PDF, **0,38 %** de pixels différents,
entièrement sur le lissage des bords. Avec le texte, l'écart monte à 8 % — et c'est
l'interligne, pas le banc.

**Une dérive mesurée, et sans conséquence.** Sélectionner un bloc fait passer sa
position par le panneau de propriétés, donc par un aller-retour points ↔ millimètres qui
rend 100,00000000000007 pour 100. C'est un résidu du correctif de C2a — sept
centièmes de millième de milliardième de point. Vérifié : il ne salit pas le document,
n'empile aucun pas d'annulation, et l'annulation d'un déplacement rend exactement l'état
d'avant le geste. Consigné parce qu'un test d'égalité stricte du JSON bute dessus, et
qu'il vaut mieux l'avoir écrit une fois que le redécouvrir à chaque poste.

*Fait le 6 septembre 2026, second temps* — l'export est devenu reproductible :
`--date` fixe la date des champs, `--contacts` désigne les coordonnées, et le modèle de
vue reçoit les deux plutôt que de lire l'horloge et le `~/.config` de la machine. Au
passage, le document à ouvrir n'est plus « l'argument qui précède le drapeau » mais le
premier argument libre : `--date 2026-09-06 --export x.pdf` aurait sinon pris la date
pour le document. L'aimantation et la taille minimale d'un bloc sont sorties de la vue
vers `Models/Snap.cs` — la seconde y était déclarée deux fois, sans que rien n'impose
l'accord. Vingt tests, dont quatre qui jouent de vrais gestes sur la vraie couche de
saisie, et `Scripts/verifier.sh` qui enchaîne tout ce qui doit passer.


### Ce que la relecture adversariale du 6 septembre 2026 a trouvé

Quatre postes livrés par quatre agents, quatre relecteurs lancés contre eux. Ce qu'ils
ont trouvé, et ce qui en a été fait.

**Corrigé aussitôt**

- **Le panneau ne redessinait plus rien, depuis R1.** `TexteCompose` déclarait
  `AffectsRender` sur la propriété qui porte le bloc — or régler le corps ou un retrait
  ne remplace pas l'instance du bloc, la liaison ne rendait donc rien de neuf. Quinze
  réglages s'enregistraient et s'exportaient correctement sans que la page bouge. Le
  contrôle s'abonne désormais au bloc et à ses deux collections d'écarts.

  *Ce qui l'avait laissé passer quatre postes durant* : aucune vérification ne comparait
  **deux images de la même page**. Les recettes comparaient l'écran au PDF, tous deux
  recalculés depuis le modèle, tous deux justes — c'est l'écran déjà affiché qui ne
  suivait pas. Et le premier test écrit pour l'attraper ne l'attrapait pas non plus :
  `RenderTargetBitmap.Render` redessine de force, quel qu'ait été l'état
  d'invalidation. Il a fallu `CaptureRenderedFrame`, qui rend l'image réellement
  composée. Onze réglages sont désormais éprouvés ainsi, et le correctif a été retiré
  pour vérifier qu'ils échouent sans lui : huit sur onze tombent.

- **La passe des polices ne descendait pas dans les tableaux.** Trouvé par la relecture
  de B5 avant même que C9 ne soit fusionné, et devenu réel à la fusion : les polices des
  cases échappaient au contrôle. `Descendance` traite maintenant les deux types qui
  portent des enfants.

- **Un tableau de 40 × 40 figeait l'application 145 secondes.** Chaque case insérée
  faisait renaître toutes les autres — 1 673 reconstructions pour 1 600 cases — et le
  panneau les relisait à chaque fois. Un verrou de lot ne notifie plus qu'une fois par
  redimensionnement. Deux tests comptent les reconstructions plutôt que les
  millisecondes, pour dire la même chose sur une machine lente.

**Consigné, non corrigé**

- **L'écran découpe le texte au bloc, l'export non.** ***Tranché et fait le
  6 septembre 2026, au poste A2*** : c'est l'export qui découpe, et au bloc. Une
  précision que la vérification a apportée, et qui corrige ce qui suit : la paire réelle
  d'ATA 21 (13,9 et −15,7 pt) sort bien de 1,82 pt de sa **zone de texte**, mais les
  2,88 pt de `fo:padding-left` la laissent à 1,06 pt **à l'intérieur du bloc** — elle
  n'était donc rognée nulle part. La divergence réelle de ces documents est verticale, et
  elle vaut jusqu'à 36,95 pt sur un bloc d'ATA 21.

- **Le seuil de rupture de colonne est évalué deux fois, sur deux mesures.** R3 a
  partagé le *code* qui décide où rompre, pas la *mesure* : l'écran interroge Avalonia,
  l'export PDFsharp. Une ligne qui tombe à un centième de point du bas d'une colonne
  peut partir en colonne 1 d'un côté et en colonne 2 de l'autre, et tout le reste du
  texte se décale. Le message de livraison de R3 présentait ce risque comme supprimé :
  il ne l'est pas. Le supprimer vraiment demande **une seule source de mesure**, ce qui
  est la question du seuil D.

- **Certains avertissements de police portent sur des polices jamais dessinées** : la
  passe relit l'héritage des fragments pour son compte au lieu de lire les styles
  composés. Un fragment entièrement recouvert par un autre est signalé quand même.



### Ce que la relecture adversariale du 7 septembre 2026 a trouvé

Cinq postes livrés par cinq agents, cinq relecteurs lancés contre eux. Deux des
trouvailles portaient sur **l'import du poste R5**, et étaient les plus lourdes.

**Corrigé aussitôt**

- **La marque de paragraphe de Publisher fabriquait une ligne vide fantôme.** libmspub
  rend le terminateur de paragraphe comme du texte — un `insertText (\r)` à la fin de
  chaque paragraphe — et l'import le gardait. À l'intérieur d'un bloc, il se collait au
  saut de ligne suivant en `\r\n` que la composition normalise : le défaut ne se voyait
  pas. Mais celui du **dernier** paragraphe n'avait rien derrière lui et devenait une
  ligne comptée dans la hauteur. Les **vingt-sept blocs** des deux documents finissaient
  ainsi. Conséquence mesurée : **cinq débordements signalés sur ATA 21, tous faux** —
  il n'en reste aucun — et vingt-deux sur les fiches, ramenés à douze. Quatre blocs
  portaient un marqueur rouge alors que rien n'y était coupé.

  *Ce que cela emportait avec soi* : la recette de A2 avait été bâtie sur ces
  débordements-là, et le poste A7 justifiait sa borne de calage par un bloc dont les
  « 26,86 pt composés » comptaient la ligne fantôme. La recette affirme désormais
  l'inverse — qu'aucun bloc d'ATA 21 ne déborde — et c'est ce contrôle-là qui tomberait
  si la ligne revenait.

- **Deux blocs d'ATA 21 sont tournés d'un quart de tour, et l'import les posait droits.**
  Le relevé écrit `librevenge:rotate: 90.0000in` — des degrés affublés d'une unité de
  longueur, que le convertisseur de points aurait lus 6 480. La rotation est portée, et
  la recette la compte.

- **Un remplissage par image illisible arrêtait tout l'export.** Depuis C5c, un
  remplissage peut ouvrir un fichier, et il le faisait hors de toute garde : aucun PDF
  n'était écrit, alors que la même image posée en bloc image ne valait qu'un
  avertissement et que l'écran l'encaissait. La règle du dépôt est constante — signaler
  et continuer — et le fond la suit désormais.

- **Un bloc de texte vide reste un bloc.** Retombée du premier correctif : un bloc des
  fiches ne portait que sa marque de paragraphe, et se retrouvait vide donc écarté. La
  source en compte dix-huit ; l'import en rendait dix-sept.

**Consigné, non corrigé**

- La découpe d'un remplissage par image n'est mesurée nulle part : le document de
  recette de C5 ne porte que des rectangles à angles droits, et un sabotage de la
  découpe traverse toute la vérification.
- La recette de A6 emploie une fabrique de blocs qui évite le cas réel — même défaut
  que celui relevé sur R2 la veille, et la consigne le nommait pourtant.
- L'écran rentre son filet dans le bloc là où l'export le centre sur le bord.
- Le seuil de rupture de colonne reste évalué sur deux mesures (voir plus haut).


### P0 — Une décision à prendre tout de suite

**Le nom du remplissage.** `ShapeFrame.Fill` et `TextFrame.Background` désignent la même
chose sous deux noms, et `ImageFrame` n'a ni l'un ni l'autre. La convergence est
programmée en C1 — mais **la décision de nommage doit être prise maintenant**, parce
que son coût est le seul du plan qui augmente avec le temps :

- aujourd'hui, un seul fichier porte la propriété (`Samples/exemple-bulletin.mypub`,
  8 occurrences de `Fill`, aucune de `Background`) : renommer, c'est un `sed` ;
- dès que des `.mypub` circulent, c'est un chemin de migration à écrire et à tenir.

Recommandation : retenir **`Fill`** pour tous les blocs — c'est le terme du domaine
(Publisher dit *Fill*, réserve *Background* à la page), c'est le nom déjà majoritaire
dans les fichiers existants, et cela laisse `Background` libre pour le fond de page
qui arrive en C5. `FormatVersion` (`Models/Document.cs`) passe à 2 le jour où le
renommage est fait.

*Recette* : la décision est actée ici, en une ligne, avec sa date.

**Décision, 5 septembre 2026 : `Fill`, pour tous les blocs** — la recommandation
ci-dessus est retenue. Le renommage lui-même reste au poste C1, et `FormatVersion`
passera à 2 ce jour-là. La normalisation du document d'exemple y a depuis ajouté
`"Background": null` sur 15 blocs de texte : le renommage reste un `sed`.
*Renommage fait en C1, le même jour — par une migration à l'ouverture plutôt qu'un
`sed`, parce que des `.mypub` de la version 1 existent désormais hors du dépôt.*

---

## Graphe de dépendances

Ce qui commande quoi. À lire de gauche à droite : la colonne de gauche doit exister
avant la colonne de droite.

| Ce qu'il faut d'abord | Ce que ça débloque |
|---|---|
| **Sélection multiple** (C3) | groupement, alignement multiple, répartition, bibliothèque de blocs (C7) |
| **Remplissage au niveau `Frame`** (C1) | fond de page, dégradés, teintes, image de fond (C5) |
| **Notion de champ** (C4) | numéros de page, date, informations professionnelles |
| **Règles graduées** (C2a) | repères de règle posés à la souris (C2b) |
| **Modèles** (B1) | jeux de couleurs et de polices (C5b), informations professionnelles (C4) |
| **Mesure du texte** (seuil D) | ajustement automatique, chaînage, habillage |
| **Charte de l'interface** (I1) | toute la phase I |
| **Disposition de la fenêtre** (I2) | barre contextuelle (I3), vignettes (I5), navigation (I6), puis le lot 2 |
| **Banc des gestes** (N1) | toute la phase N |
| **Position de chaque morceau composé** (N3) | la mesure de la saisie (N7), le clic simple (N12), les caractères spéciaux (N22), l'orthographe (N25) |
| **Mesure de la saisie** (N7), puis la décision du propriétaire | le texte tel qu'il s'imprime pendant la frappe (N21), puis R6, R7 et tout le lot 3 de la phase N |
| **Seuil du pointeur** (Q21) | le clic simple (N12), les modificateurs du glisser (N17) |
| **Presse-papiers** (N8) et **chemins vers le dock** (N18) | le menu contextuel (A12) |
| **Préférences de l'utilisateur** (N11) | l'aimantation réglable (A11), le dictionnaire de l'utilisateur (N25) |
| **P1 + P2** | toute la phase C |

Deux remarques que le graphe rend visibles :

- **La sélection multiple est le nœud le plus rentable** : à elle seule elle ouvre
  quatre fonctions que les fiches comptaient séparément.
- **Les modèles ne dépendent de rien.** C'est pourquoi la phase B passe avant la
  phase C alors qu'elle apporte plus : rien ne l'en empêche.

---

## Phase A — Finitions à mécanique existante

Postes qui n'inventent aucune structure : la mécanique est déjà écrite, il manque
le geste, le message ou le champ. Ils étaient sept ; l'audit des fiches en a ajouté
huit, A8 à A15, dont deux — A10 et A14 — touchent au rendu plus que les autres. Aucun ne touche au format de fichier autrement qu'en
ajoutant une propriété facultative. Ils peuvent se faire dans n'importe quel ordre, et
même avant P1 — mais autant commencer par P1.

### A1 — Poignée de rotation *(fiche 10)*

La rotation existe partout — modèle, test de survol, redimensionnement, export — et ne
se règle qu'au champ numérique. Ajouter une neuvième poignée au-dessus du bloc, sur le
même tableau de poignées et le même mode de glisser que le redimensionnement.

*Recette* : tourner un bloc à la souris, l'exporter, l'angle est le même à l'écran et
dans le PDF.

*Fait le 6 septembre 2026.* Une neuvième poignée, posée au-dessus du bord haut du bloc,
sur le même tableau que les huit autres et sur le même mode de glisser que le
redimensionnement — `BeginEdit` à la prise, `CommitEdit` au relâché, donc annulable d'un
seul pas comme un déplacement. Le poste tient dans un fichier, `Views/PageCanvas.axaml.cs` ;
il n'ajoute aucune propriété au modèle, `FormatVersion` reste à 3 et aucun document du
dépôt n'a eu à être régénéré.

**Le geste suit l'écart d'angle depuis la prise, et non l'angle absolu du pointeur.** La
poignée n'est pas sur l'axe du bloc mais à un quart de tour de lui : viser l'angle absolu
ferait sauter le bloc de 90° au premier pixel de glissement. Le pivot est le centre —
celui qu'emploient déjà `Frame.ToPage` et l'export —, si bien que tourner n'écrit que
`Rotation` : ni la position ni la taille ne bougent.

**Maj aimante sur 15°, le pas de Publisher, et l'aimantation porte sur l'angle absolu.**
C'est la seule décision du poste qui ne se déduisait pas. Un pas appliqué à l'écart
parcouru conserverait son travers à un bloc déjà de guingois ; appliqué à l'angle, il
permet de redresser un bloc importé — et c'est ce qu'on attend d'un pas de rotation.
Douze positions par tour couvrent les angles droits, les diagonales et les tiers de
quart. **Maj change donc de sens d'un geste à l'autre** : elle conserve les proportions
sur une poignée d'angle et aimante l'angle sur celle-ci. C'est une divergence assumée,
et c'est celle de Publisher.

L'angle est ramené dans [0, 360[. Le champ du panneau s'arrête à ±360 : deux tours de
poignée dans le même sens le dépasseraient, et la valeur serait ramenée en silence à la
sélection suivante. Publisher affiche 350°, pas −10°.

*Recette jouée*, et elle est un script : `Scripts/recettes/A1-poignee-rotation.sh`. Elle
part d'un vrai pointeur synthétisé sur la vraie couche de saisie — onze vérifications
dans `MyPublisher.Tests.Interface/RotationTests.cs` — puis mesure ce que le geste a
produit : **90°, l'angle des trois objets tournés d'ATA 21** (`librevenge:rotate: 90`
dans le relevé de `pub2raw` ; les fiches de limitations n'en portent aucun). Le PDF et
l'écran sont ensuite rendus à deux pixels par point et leur **boîte d'encre mesurée** :
120 × 400 px posée en (340, 60) des deux côtés, soit le bloc de 200 × 60 pt debout,
tourné autour de son centre. La position absolue de cette boîte n'est pas décorative :
elle mesure le pivot, un quart de tour autour du coin supérieur gauche donnant la même
boîte 130 pt plus loin.

**Ce que la mesure d'encre ne peut pas voir, et ce qui s'en charge.** La boîte d'un bloc
tourné de +90° est exactement celle d'un bloc tourné de −90°, et l'écran comme le PDF
lisent la même propriété : ni la mesure ni la comparaison d'images n'attraperaient une
inversion de sens. C'est la valeur **signée** écrite dans le document, et un test qui
tourne dans l'autre sens et exige 270°, qui l'épinglent.

La recette a été éprouvée en la faisant échouer, cinq fois : sens inversé, aimantation
retirée, pivot placé au coin supérieur gauche, geste de rotation aiguillé vers le
redimensionnement — et, pour vérifier que la mesure d'encre n'est pas décorative, la
rotation retirée de `PdfExporter`. Cinq sabotages, cinq attrapés ; le cinquième par la
seule mesure de boîte, les quatre autres par les tests d'interface. Le binaire a été
touché à chaque restauration : `cp -a` rendrait au fichier sa date d'origine et MSBuild
laisserait vivre le binaire saboté — le piège consigné au poste R3.

#### Trouvé au passage, et corrigé à moitié — la moitié qui nuisait

**Un bloc collé au haut de la page mettait sa poignée sur la règle graduée.** C'est la
seule poignée qui se pose hors du bloc, donc la seule qui puisse sortir de la planche :
la couche de saisie s'arrête au bord de la page, et le clic tombait sur `Ruler`, qui
**posait un repère au lieu de tourner**. Il ne faisait pas rien, il faisait autre chose.
Les deux règles servent désormais la poignée de rotation avant de poser un repère ; la
capture leur revient, et elles renvoient déjà la suite du geste aux mêmes gestionnaires.
Mesuré avant correction sur un bloc posé à 5 pt du haut : rotation 0°, un repère
vertical de trop ; après : 90°, aucun repère.

**Cela ne vaut que règles affichées, et il faut le dire.** *Affichage ▸ Règles* décoché,
`Ruler` n'est plus dans l'arbre visuel : le clic tombe alors sur le plan de travail, que
personne n'écoute, et **ne fait rien**. Mesuré, comme le reste : rotation 0°, aucun
repère. C'est le cas ci-dessous, moins nuisible que celui qui est corrigé — se tromper
de geste vaut moins bien que n'en faire aucun — mais il n'est pas supprimé. Le supprimer
vraiment demanderait d'étendre la couche de saisie au-delà de la page, donc de toucher
`PageCanvas.axaml`, hors du fichier de ce poste.

#### Choisi sans certitude

**La poignée est à 22 px du bord haut, en pixels d'écran et non en points.** Elle garde
donc la même portée de main à tous les zooms, comme les huit autres gardent leur taille.
Vingt-deux plutôt que quinze pour que sa zone de préhension (7 px) ne touche jamais celle
des poignées d'angle : deux poignées qui se recouvrent, c'est un geste qui part au
hasard. Aucune fiche ne donne la distance de Publisher.

**Un disque vert, et une tige qui le relie au bloc.** Le vert est celui que Publisher
emploie pour ce geste — c'est aussi celui des repères libres dans cette application, mais
un disque de neuf pixels sur un bloc sélectionné ne se confond pas avec un trait qui
barre la page. Un neuvième carré, lui, se serait lu comme un bord à tirer.

**Le curseur est la main.** Le jeu standard d'Avalonia n'a pas de curseur de rotation, et
en embarquer une image pour un seul geste coûterait plus que ça ne dit.

#### Trouvé et laissé tel quel

**Une poignée qui sort de la page là où il n'y a pas de règle n'est pas saisissable.**
Deux cas : un bloc tourné de 180° et collé au bas de la page met sa poignée sous la
planche, et un bloc collé au haut la met hors de portée dès que les règles sont
masquées. Le clic ne fait alors rien — et non pas autre chose, contrairement au cas
corrigé ci-dessus. Le champ du panneau reste. Le traiter demanderait d'étendre la couche
de saisie au-delà de la page, donc de toucher `PageCanvas.axaml`, hors du fichier de ce
poste.

**Le curseur ne change pas au-dessus de la poignée quand elle est sur la règle.**
`UpdateCursor` n'écrit que sur la couche de saisie ; les règles portent le leur, posé une
fois pour toutes. On y voit donc la flèche de déplacement de repère alors qu'on
s'apprête à tourner. Le geste, lui, est le bon.

**Une poignée d'angle déborde de sept pixels au-dessus d'un bloc posé à Y = 0.** Sa zone
de préhension sort alors de la planche par le haut et retombe sur la règle, qui pose un
repère. Le défaut est antérieur à ce poste et vaut pour les huit poignées ; il n'est pas
corrigé ici parce que servir les huit depuis la règle changerait le geste de C2b — un
clic sur la règle près du bord d'un bloc cesserait de poser un repère.

### A2 — Signaler le débordement du texte *(fiche 12)*

Aujourd'hui un texte trop long est coupé sans un mot, à l'écran comme à l'export. Deux
gestes : un marqueur visuel sur le bloc concerné, et un avertissement dans le rapport
d'export, qui remonte déjà jusqu'à la barre d'état.

*Recette* : un bloc au texte trop long se signale à l'écran ; son export mentionne le
bloc par son nom.

C'est le poste le plus important de la phase A : il n'ajoute aucune fonction, il
supprime un risque — livrer un PDF amputé sans le savoir.

*Fait le 6 septembre 2026*, avec **A7** et le **rognage à l'export**. Les trois ne font
qu'un : ils demandent tous la hauteur composée d'un bloc, que `Compositeur` sait rendre
depuis R3, et les traiter séparément aurait produit trois mesures là où le dépôt tient à
n'en avoir qu'une.

#### Le rognage : un retour, et non une addition

`PdfExporter.DrawText` pose désormais un chemin de découpe sur le bloc, exactement là où
`PageCanvas.axaml` pose `ClipToBounds`. Ce n'est pas une fonction neuve : la fiche 12
relève qu'**avant R1, `XTextFormatter.DrawString` s'arrêtait au bord de la boîte**. La
composition partagée, qui pose chaque ligne à l'ordonnée qu'on lui donne, a emporté cette
limite sans que rien ne la remplace — et depuis, un bloc trop plein débordait sur ses
voisins dans le PDF et pas à l'écran. Publisher ne dessine pas le texte hors de sa zone ;
les deux rendus s'accordent de nouveau là-dessus.

C'est le **bloc** qu'on découpe, et non la zone de texte, parce que c'est ce que l'écran
fait : une première ligne en retrait négatif pend dans la marge intérieure gauche et doit
y rester visible.

#### Ce que la vérification a corrigé de l'énoncé

La consigne de ce poste, comme la note de R2 plus bas, présentait la paire d'ATA 21 —
`fo:margin-left 0.1930in` avec `fo:text-indent −0.2183in` — comme un cas où l'écran rogne
et le PDF non. **Ce n'est pas le cas, et l'arithmétique le dit** : 13,896 − 15,718 =
−1,822 pt, mais `fo:padding-left` vaut `0.0400in`, soit 2,88 pt. La première ligne tombe
donc à **+1,058 pt à l'intérieur du bloc**. Vérifié sur les neuf blocs d'ATA 21 et sur les
dix-huit des fiches : aucun ne sort de son bloc par la gauche ni par la droite, d'un
millième de point.

La conclusion de l'énoncé tient — c'est bien l'export qui doit rogner — mais le cas qui la
démontrait n'est pas celui-là. La divergence réelle d'ATA 21 est **verticale**, et elle
est bien plus grande : « Air is cooled » compose 36,95 pt de plus que son bloc, « Overheat
Protection » 8,65 pt. C'est donc là que la recette mesure, où le signal vaut 74 pixels à
deux pixels par point et non trois.

#### Le critère du débordement est le bloc, pas la zone de texte

Une fois la découpe posée au bloc, ce qui sort du bloc est exactement ce qui est amputé —
et rien d'autre ne l'est. Un texte qui ne dépasse que dans sa marge intérieure basse reste
entier et lisible : le signaler serait crier au loup, et c'est la leçon que B5 a payée
cher. **Deux des neuf blocs d'ATA 21 sont dans ce cas**, à 1,5 pt près ; la recette exige
le silence sur eux, nommément.

Le seuil est un demi-point, et il n'est pas là pour absorber une erreur d'arrondi — elle
est de l'ordre du millionième. Il est là parce que le marqueur de l'écran sort des
métriques d'Avalonia et l'avertissement de l'export de celles de PDFsharp : un bloc au ras
de son bord peut être signalé d'un côté et pas de l'autre. Aucun seuil ne referme cette
fenêtre ; seul un document qui se donne de l'air la referme.

#### A7 : le calage est borné, et c'est tout le poste

Trois valeurs sur le bloc de texte — haut, milieu, bas —, une propriété facultative, et
`FormatVersion` **reste à 3**. L'import de R5 les lit (`draw:textarea-vertical-align`) et
son avertissement disparaît.

Le seul bloc des deux documents réels qui porte ce réglage compose **26,86 pt dans 19,43
pt de haut**. Le calage est donc borné à zéro : un texte plus haut que sa zone se cale en
haut, quel que soit le fer demandé. Centrer y escamoterait le début du texte, et ce qui
manque à la fin se voit quand ce qui manque au début ne se voit pas. Une recette sur les
valeurs réelles n'éprouve donc *que* cette borne — d'où le document propre au poste, qui
éprouve les trois calages sur un bloc où ils se distinguent.

*Choisi sans certitude* : c'est bien cette borne, et non la troncature par le haut, que
fait Publisher — mais aucune fiche ne le dit et le relevé ne montre qu'une occurrence.

#### La désignation d'un avertissement

Le format reste « &lt;désignation&gt; : &lt;message&gt; » ; la désignation seule s'allonge
de ce qu'il faut pour désigner. La page d'abord, puis les blocs qui portent celui-ci, puis
un rang **lorsque le nom se répète dans la même liste** :

```
Page 2 · ATA 21 - Air conditioning : le texte déborde du bloc de 4,5 pt — …
Page 4 · Bande n° 9 · L1 · C2 : le texte déborde du bloc de 9,8 pt — …
```

Sans cela, deux blocs anonymes donnaient deux lignes identiques, et l'import de R5, qui
nomme « Bande » chacune des seize bandes des fiches ATR et « L1 · C2 » la deuxième case de
chacune, en donnait six sur la seule page 4. Le rang ne s'écrit que là où il apprend
quelque chose : posé sur un nom déjà unique, il ferait croire à une numérotation que le
document ne porte pas.

#### Le marqueur, et pourquoi il obéit aux repères

Un petit A rouge au coin inférieur droit du bloc, comme Publisher. Il est le frère du
contrôle qui dessine le texte et non un morceau de lui — le texte est découpé au bloc, un
marqueur dessiné dedans le serait aussi, et il doit mordre le bord. Les deux lisent la
même composition et la même règle de débordement, celle que l'export applique.

**Il s'efface avec les repères** (*Affichage ▸ Repères*), ce qui s'écarte de Publisher,
qui le montre toujours. Deux raisons, et la seconde est décisive : il n'a pas plus de sens
qu'un repère sur une page qu'on regarde pour la juger ; et sans cet interrupteur, toute
page portant un débordement afficherait une tache que le PDF n'a pas, si bien que la
comparaison écran ↔ PDF cesserait de prouver quoi que ce soit — précisément sur les pages
que ce poste concerne.

#### Ce que le poste a trouvé

**Le dépôt livrait des PDF amputés, et le document de référence du contrôle P2 le
premier.** Quatre blocs de `Samples/exemple-bulletin.mypub` — dont « Colonne de gauche »,
43,56 pt, presque trois lignes de corps — et deux de `Samples/recette-polices.mypub`
perdaient du texte à l'export sans un mot. Ce n'est pas une négligence de composition :
c'est **la retombée de R4**, qui a fait de l'interligne un multiple de la hauteur
naturelle et non du corps. Les blocs avaient été dimensionnés sous l'ancienne sémantique,
où `LineHeight 1,4` valait `1,4 × corps` ; ils réclament depuis 16 % de plus. Personne ne
l'avait vu parce que rien ne le disait — ce qui est exactement la raison d'être d'A2. Les
documents sont remis à la taille de leur texte, et un test les garde désormais tous.

**Les deux règles à mesurer ne donnent pas le même nombre de lignes.** Le bloc « Rappel »
d'`exemple-bulletin` — DejaVu Sans *italique* 14 pt sur 441,9 pt de large — se compose en
**deux lignes sous PDFsharp et en trois sous Avalonia**. L'écart n'est pas de quelques
centièmes : c'est une ligne entière, soit 23,55 pt, et il fait que l'écran signalait un
débordement que l'export ne voyait pas. C'est le défaut déjà consigné à R3 — « le seuil de
rupture de colonne est évalué deux fois, sur deux mesures » — pris ici sur un cas où il se
voit à l'œil nu. Il n'est pas corrigeable dans ce poste : le supprimer demande une seule
source de mesure, ce qui est la question du seuil D. Le bloc a été agrandi de façon à
tenir sous les deux.

#### Recette

`Scripts/recettes/A2-A7-debordement-et-rognage.sh`, sur **ATA 21 d'abord** — importé,
exporté, et mesuré aux valeurs qui y sont — puis sur `Samples/recette-debordement.mypub`
pour ce que le réel ne porte pas.

Le rognage se mesure sur des **images** et jamais sur `pdftotext` : un chemin de découpe
PDF cache le texte, il ne le retire pas du flux. `pdftotext` le trouve encore — et c'est
tant mieux, la recette de R5 compte dessus pour affirmer que le texte est passé.

Mesuré : zéro pixel d'encre sous « Overheat Protection » des deux côtés, contre 5 455
pixels dans le bas du bloc — la mesure n'est pas prise à vide ; les trois calages
verticaux à 160,002, 207,198 et 254,393 pt, au centième ; le calage au milieu d'un bloc
trop petit qui rend exactement la même ordonnée que son jumeau calé en haut ; la première
ligne pendante à 57,748 pt et les suivantes à 73,466, aux valeurs d'ATA 21, avec l'encre
qui reste **hors de sa zone de texte** et dans le bloc ; et la moitié basse du filet de
2 pt d'un bloc trop plein, qui prouve que le découpage du texte n'a pas survécu au texte.

Un bloc **pivoté de 20°** porte le même débordement : le découpage est posé dans le repère
courant, donc il tourne avec le bloc, et une découpe restée en coordonnées de page ne se
verrait sur aucun autre document du dépôt.

**Les nombres d'ATA 21 sont ceux que Calibri donne**, et Calibri est présente sur la
machine qui a écrit cette recette — le poste B5 l'avait déjà relevé. Ailleurs, fontconfig
rend autre chose, le texte ne compose plus à la même hauteur, et ces nombres-là ne veulent
plus rien dire ; la recette le voit à l'avertissement de substitution que l'export émet
déjà et passe son chemin en le disant. Ce serait un filet troué si le reste en dépendait :
la règle de la marge intérieure basse — celle qui vaut à ATA 21 son silence — est donc
éprouvée une seconde fois sur le document de la recette, en DejaVu Sans, sur une paire de
blocs qui ne diffèrent que de trois points de hauteur. Le passage à vide a été joué : la
recette reste verte, et n'éprouve alors plus rien qu'elle ne prouve ailleurs.

La recette a été éprouvée en la faisant échouer, sept fois : le découpage retiré, le
découpage posé sur la zone de texte au lieu du bloc, le découpage survivant au texte, le
calage vertical oublié à l'export, l'avertissement supprimé, le calage non borné, et le
débordement mesuré sur la zone au lieu du bloc. Les tests l'ont été de même sur cinq
autres : le calage oublié à l'écran, le marqueur jamais dessiné, le rang retiré de la
désignation, le calage perdu à l'import, et une propriété oubliée dans `Clone()`. **Douze
sabotages, douze attrapés.**

#### Trouvé au passage, et laissé tel quel

**L'écran rentre son filet dans le bloc, l'export le centre sur le bord.** Mesuré sur un
filet de 2 pt : il occupe 115,0–117,0 pt à l'écran et 115,5–117,5 pt au PDF, pour un bord
de bloc à 116,69. Avalonia décale la géométrie d'une demi-épaisseur, PDFsharp non — et la
documentation de `Frame.StrokeThickness`, « centrée sur le bord du bloc », n'est donc vraie
que d'un côté. Antérieur à ce poste, visible seulement sur les filets épais, hors
périmètre.

**Le débordement horizontal est rogné et n'est pas signalé.** Un mot plus large que sa
colonne, ou un retrait de première ligne assez négatif pour sortir du bloc, est désormais
coupé des deux côtés sans un mot — A2 ne mesure que la hauteur, comme le marqueur de
Publisher. Aucun des vingt-quatre `.pub` de la machine ne produit ce cas ; il n'en est pas
moins muet.

**Un tableau ne porte pas de calage vertical.** Ses cases naissent calées en haut, et rien
ne le règle : `TableFrame` a une police, un corps et une marge, pas de fer vertical. Le
relevé n'en demande aucun.

**`TextFrame.AbsoluteLineHeight` vaut encore `FontSize × LineHeight`.** C'est l'ancienne
sémantique, celle que R4 a remplacée par un multiple de la hauteur naturelle. Aucun des
deux rendus ne la lit — ils passent tous deux par `Compositeur.Avance` —, mais les
vignettes du catalogue (`TemplatePreview`) s'en servent encore, et elle ment du même écart
de 16 % qui a fait déborder le document de référence. B1 assume déjà que la vignette est
un schéma et non un rendu, si bien que rien ne se voit ; c'est la même forme que la
découverte sur le filet — une propriété survivante d'une sémantique changée ailleurs.

### A3 — Chemins d'images relatifs *(fiche 21)*

À l'enregistrement, écrire le chemin en relatif quand le fichier image se trouve sous le
dossier du document ; en absolu sinon. À l'ouverture, résoudre les deux.

*Recette* : déplacer un dossier contenant un `.mypub` et ses images ne casse plus rien.

*Fait le 6 septembre 2026.* La règle est celle du plan, à la lettre : relatif sous le
dossier du document, absolu ailleurs, et les deux se relisent. Un chemin qui remonterait
par `..` est écarté et reste absolu — il se casserait au premier déplacement, ce qui est
exactement ce que le poste répare. Le « sous le dossier » se mesure par
`Path.GetRelativePath` et non par un préfixe de chaîne : `…/notice` et
`…/notice-images` partagent le préfixe sans que le second soit dans le premier.

**La résolution a lieu à l'ouverture, une fois pour toutes.** C'est le choix qui commande
tout le reste. Passé `FromJson`, le modèle ne porte que des chemins absolus : ni la
liaison de l'écran, ni l'export, ni le cache d'images n'ont à savoir qu'un chemin relatif
existe, et aucun d'eux n'a changé d'une ligne. Le poste tient donc entier dans
`DocumentSerializer`, et il n'ajoute **aucune propriété au modèle** — ni `Clone()`, ni le
test d'exhaustivité par réflexion, ni `NotifySelection()` n'entrent en jeu.

**Le dossier est un paramètre de l'écriture comme de la lecture, et son absence a un
sens précis** : les chemins passent alors tels quels, ni relativisés ni résolus. C'est ce
qui règle le piège du poste. `MainWindowViewModel.Capture` sérialise le document à chaque
geste et `Restore` le relit — tous deux **sans dossier**, donc l'aller-retour est
l'identité sur les chemins, quelle que soit leur forme. Un geste annulé ne peut pas
déplacer une image. La demi-mesure serait mortelle : un instantané qui relativiserait
sans que la restauration résolve rendrait le chemin relatif *en mémoire*, où il se
résoudrait ensuite sur le dossier courant du processus — l'image disparaîtrait de la page
sans qu'aucun geste ne l'ait touchée. C'est le cinquième sabotage ci-dessous, et il est
attrapé.

Ce dossier facultatif appelle un garde-fou, et c'est un invariant plutôt qu'un test :
**`DocumentSerializer.Save` est le seul endroit du dépôt qui écrive un `.mypub` sur le
disque**. Les deux seuls appels qui ne passent pas de dossier — l'instantané de
l'historique et sa comparaison — ne touchent aucun fichier. Un second écrivain, l'archive
du poste B4 par exemple, devra passer le sien : sans quoi il graverait des chemins
absolus sans que rien n'échoue, le contrôle ne portant sur aucun document à image. La
consigne est écrite là où on la lira, sur `Save`.

**La réécriture n'est pas un « enregistrer sous ».** `--resave` rapporte les chemins au
dossier de la **source** et non à celui du fichier produit. Ce n'est pas une commodité :
c'est la procédure de régénération des `Samples/` et des `Templates/` — `--resave /tmp/x`
puis recopie par-dessus l'original — et sous une sémantique d'« enregistrer sous » elle
graverait dans un modèle livré le chemin absolu de la machine qui l'a régénéré, en
silence. Le vrai « enregistrer sous », lui, relativise bien sur le nouveau dossier ; il
est éprouvé par un test du noyau, l'application n'ayant pas de mode sans interface pour
ce geste-là. Conséquence assumée : un modèle qui porterait une image relative, ouvert en
copie et enregistré ailleurs, écrit cette image en absolu — ce qui est juste, elle vit
bien ailleurs.

**Le cache d'images de `Conv.cs` n'a pas eu à suivre, et c'est le résultat qui compte.**
Il est statique, indexé par chemin, et son `Invalidate` n'est appelé par personne : si le
modèle avait gardé la forme relue du fichier, deux documents rangés dans deux dossiers et
portant chacun `images/logo.png` se seraient partagé une seule entrée, le second
affichant l'image du premier. La résolution à l'ouverture ferme ce cas ; la clé est en
outre normalisée, sans quoi `Invalidate` ne retrouverait pas une entrée rangée sous une
autre graphie. Ce raisonnement est une affirmation sur du code : un test d'interface en
fait une mesure, avec deux documents et deux images du même nom dans un seul processus.

`FormatVersion` **reste à 3**, et c'est le seul endroit où l'on aurait pu choisir
autrement. Un fichier ancien ne porte que des chemins absolus et s'ouvre inchangé : rien
à migrer. Mais un fichier neuf portant un chemin relatif, ouvert par un binaire
antérieur, verrait ce chemin résolu sur le dossier courant du processus et perdrait le
lien **sans rien dire**, là où un numéro plus grand l'aurait fait refuser avec son
numéro. Un seul chemin y mène — enregistrer avec cette version, puis rouvrir avec une
plus ancienne, c'est-à-dire régresser de binaire — et c'est ce qui rend le choix
défendable plutôt que seulement permis. L'asymétrie est réelle ; elle est écrite ici
plutôt que tue.

*Recette jouée*, et elle est un script : `Scripts/recettes/A3-chemins-images.sh`. Un
atelier avec un `.mypub`, une image sous son dossier et une image au-dehors ; le dossier
est déplacé pour de bon — `mv`, l'ancien chemin n'existe plus — et le document s'ouvre
encore. Ce qui est mesuré n'est pas l'absence d'un avertissement mais **la couleur des
deux aplats relevée dans la page rendue**, au centre de chaque bloc : un export qui ne se
plaint de rien peut très bien avoir dessiné un trou. La forme écrite est vérifiée des
deux côtés — `images/plan.png` d'une part, le chemin absolu de l'autre — et la recette
finit par une contre-épreuve : déplacer le dossier de l'image du dehors doit la casser.
Sans elle, le passage précédent pourrait tenir sur un export qui ne dit jamais rien.

La recette a été éprouvée en la faisant échouer, six fois : l'écriture qui ne relativise
plus, la lecture qui ne résout plus, `--resave` rendu à la sémantique d'« enregistrer
sous », le garde-fou du `..` retiré, l'instantané d'annulation qui relativise sans que la
restauration résolve, et le cache indexé sur le nom du fichier. **Six sabotages, six
attrapés** — quatre par la recette, trois par les tests du noyau, quatre par ceux de
l'interface. Deux ne sont attrapés que par les tests d'interface, et ce sont les deux
qu'on ne voit pas venir : l'annulation et le cache. Un seul n'est attrapé que par la
recette, `--resave` — le contrôle de non-régression, lui, ne le verrait pas, aucun
document du dépôt ne portant d'image.

#### Ce que le poste a trouvé

**Les documents ATR ne portent aucune image liée**, et il faut le dire plutôt que de
laisser croire que la recette a évité le sujet. Les cinq PNG d'ATA 21 sont des
remplissages de forme (`draw:fill: bitmap`) que l'import de R5 ne reprend pas — le relevé
le dit, le README le répète. Il n'y a donc pas de valeur réelle à reprendre ici comme
R2 et R3 ont pu le faire : ce que la recette reproduit est le **geste** réel que la
fiche 21 désigne comme le cas de casse le plus fréquent, « j'ai déplacé mon dossier de
travail », joué littéralement.

**Une couleur symétrique est un mauvais témoin.** Le premier test d'interface relevait le
magenta, dont le rouge et le bleu sont égaux : il passait alors même que la lecture des
pixels inversait les deux voies. C'est le cyan qui l'a dit — la trame composée par
`CaptureRenderedFrame` est en RGBA et non en BGRA. Une heure perdue, et une leçon qui
vaut au-delà de ce poste : un témoin invariant par l'erreur qu'on cherche ne mesure rien.

**L'écriture passe désormais par l'arbre JSON** plutôt que par la sérialisation directe —
réécrire un chemin sur le bloc lui-même notifierait la liaison et ferait relire l'image à
l'écran, et laisserait le document modifié si l'écriture échouait ensuite. Vérifié avant
de bâtir dessus : à dossier absent, l'arbre rend **exactement** le même texte que la
sérialisation directe, dérives d'arrondi et caractères non-ASCII compris. Les dix
`.mypub` livrés — six exemples, quatre modèles — se réécrivent au caractère près, et
aucun n'a eu à être régénéré : le poste n'ajoute aucune propriété au fichier.

#### Trouvé et laissé tel quel

**Le cache d'images retient aussi les échecs, et pour toujours.** Une image absente à
l'ouverture est mémorisée comme telle pour la durée du processus : la remettre en place
puis rouvrir le document laisse le bloc vide, et rien ne le dit. `Invalidate` existe
précisément pour cela et personne ne l'appelle. Hors périmètre — c'est un défaut
antérieur au poste, que le poste ne touche pas — mais il attend son correctif, et
l'ouverture d'un document en serait le bon endroit.

**Un `.mypubz` reste nécessaire** : ce poste traite le dossier déplacé, pas le document
envoyé seul. C'est le poste B4, et la fiche 21 les distinguait déjà.

### A4 — Ordre de superposition : raccourcis et glisser-déposer *(fiche 15)*

Les quatre commandes existent et sont saines ; il leur manque des raccourcis clavier, et
la liste « Blocs (du fond vers l'avant) » invite à un glisser-déposer qui n'existe pas.

*Recette* : réordonner un bloc au clavier, puis en le tirant dans la liste.

*Fait le 6 septembre 2026.* `Ctrl+Maj+F` et `Ctrl+Maj+B` portent la sélection au premier
plan et à l'arrière-plan, et le menu Disposition les affiche. **Deux raccourcis et non
quatre** : la fiche 15 relève que Publisher n'en donne qu'aux deux extrêmes, et avancer
ou reculer d'un rang n'en a pas — on n'en invente pas. Publisher 2007 les posait sur
`Alt+F6` et `Alt+Maj+F6` ; ce sont les combinaisons modernes qui sont reprises, la
plupart des gestionnaires de fenêtres Linux captant `Alt+F6` avant l'application. Un
raccourci qui n'arrive jamais jusqu'à son destinataire ne vaut pas mieux qu'aucun.

La liste des blocs se réordonne en y tirant un bloc, un trait bleu marquant où il se
posera. **Le geste est bâti sur les pointeurs et non sur `DragDrop.DoDragDrop`** : la
plate-forme sans affichage n'inscrit aucune source de glisser-déposer — vérifié dans
`Avalonia.Headless` 11.3.9, qui offre bien la réception d'un dépôt mais pas son émission
— si bien qu'un geste bâti sur elle ne serait jouable par aucun banc, et ce poste
n'aurait été éprouvé que sur parole. C'est au demeurant l'idiome de la planche, qui
n'utilise pas davantage le survol des contrôles.

#### Ce que le poste a trouvé

**Les quatre commandes de superposition perdaient leur sélection**, et le défaut est
antérieur au poste. La liste des blocs est liée à la fois à la collection de la page et
à l'ancre de sélection ; remuer la collection fait passer la sélection de la liste par
zéro, et la liaison à double sens redescend cette remise à zéro dans le modèle de vue.
Le bloc qu'on vient de porter au premier plan cesse donc d'être pris.

Deux choses méritent d'être dites. D'abord, **le moyen n'y est pour rien** :
`ObservableCollection.Move` perd la sélection exactement comme un retrait suivi d'une
insertion. C'était l'hypothèse de départ, et elle est fausse — mesurée sur
`BringForward`, qui employait déjà `Move` et perdait la sélection tout autant. Ensuite,
le défaut ne devient gênant qu'une fois le geste au clavier : au menu on reclique sans y
penser, au raccourci on enchaîne les frappes et la deuxième ne fait plus rien. La
sélection est donc reposée après le remaniement et **avant** la fermeture de la
transaction, pour que l'instantané d'annulation garde celle qu'on voit et non une
sélection vide.

Au passage, `StepOrder` interroge désormais une copie de la sélection : la boucle
d'origine consultait `Selection` à chaque tour, alors que le premier déplacement venait
de la vider.

**Le seuil de glissement ne fait pas ce qu'on croyait, et c'est le sabotage qui l'a
dit.** Ramené à zéro, il ne change rien à l'ordre de la pile : le rang d'insertion est
un point *entre* deux blocs, et un pointeur qui tremble de deux pixels rend le rang dont
il est parti — le déplacement est alors sans objet. Ce que le seuil tient, c'est le
geste : sans lui, le moindre frémissement capture le pointeur et allume le trait
d'insertion sous le doigt de quelqu'un qui ne fait que cliquer. Le test a été réécrit
pour vérifier cela, qui est vrai, plutôt que ce qu'on lui prêtait.

### A5 — Formats de page *(fiche 01)*

Deux champs largeur/hauteur libres dans le panneau DOCUMENT — le modèle les accepte déjà,
ce sont des `double`. Et quatre formats à ajouter à la liste : B4, B5, Executive, Tabloid.

*Recette* : composer sur un format 100 × 200 mm et l'exporter à cette taille exacte.

*Fait le 6 septembre 2026.* Neuf formats au catalogue au lieu de cinq — B4 et B5 aux
dimensions ISO, Executive et Tabloid en pouces exacts — et deux champs en millimètres
dans le panneau DOCUMENT. `FormatVersion` reste à 3 : aucune propriété n'est née, ce sont
les deux `double` que le document portait déjà.

**Le sélecteur suit les dimensions, jamais l'inverse.** `PageSize.Match` rend `null`
devant une taille qu'aucune entrée ne reconnaît, et c'est un état légitime — la vignette
« Create custom page size… » du catalogue de Publisher — et non une absence de réponse.
Ce qu'il ne faut surtout pas en conclure, c'est qu'il faille effacer les dimensions.

Pour que le sélecteur se vide quand on saisit une taille libre, le modèle de vue
s'abonne désormais aux dimensions du document. Le point délicat n'est pas l'abonnement
mais sa durée de vie : **le document se remplace en bloc** — ouverture, modèle, page
neuve, annulation — et un abonnement pris une fois pour toutes laisserait le sélecteur
muet dès la première annulation, tout en gardant l'ancien document en vie.

**Poser un format écrit deux dimensions l'une après l'autre**, et le document porte
entre les deux une largeur de B5 avec une hauteur de format libre. Ré-accorder le
sélecteur sur cet état-là rejouerait le gestionnaire qui vient de l'appeler. Le verrou
couvre donc l'écriture entière et non chaque ligne ; ce qui le garde est un test qui
compte les pas d'annulation — un geste, un pas.

*Recette jouée*, `Scripts/recettes/A5-formats-de-page.sh`, sur
`Samples/recette-formats.mypub`, une page de 100 × 200 mm. Le PDF **mesure**
283,465 × 566,929 pt, relu deux fois par deux outils — `pdfinfo` lit la boîte déclarée,
`pdftotext -bbox` la redit à sa façon —, le contenu y est posé à 44,000 pt du bord
gauche (les 40 pt du bloc et ses 4 pt de marge intérieure), l'écran rend la même page à
la même taille, et les quatre formats ajoutés sortent aux dimensions de leur norme, au
centième de point.

### A6 — Taille du bloc dans la barre d'état *(fiche 02)*

Publisher affiche en permanence position **et** taille du bloc sélectionné. MyPublisher
n'affiche que la position du curseur. Pendant un redimensionnement, l'œil est sur la
page, pas sur le panneau de droite.

*Recette* : la taille se lit sans quitter la page des yeux pendant le geste.

*Fait le 6 septembre 2026.* Deux cases de plus dans la barre d'état, la position du bloc
et sa taille, séparées comme la fiche 02 les relève. Elles ne s'affichent qu'à la
sélection, et les quatre cases de droite sont désormais de largeur fixe : elles se
rafraîchissent à chaque mouvement de pointeur, et une barre dont les cases se déplacent
à chaque chiffre ne se lit plus.

**À un bloc, c'est son cadre ; à plusieurs, l'empreinte de l'ensemble.** Choix
délibéré : les quatre nombres annoncés pour un bloc seul sont exactement ceux que le
panneau donne à saisir, si bien qu'un chiffre lu en bas et un chiffre lu à droite ne
peuvent pas se contredire. La différence se voit sur un bloc tourné, dont l'empreinte
déborde le cadre — et c'est bien le cadre qu'on annonce quand il est seul.

**La mesure suit le geste et non sa fin.** Le plan de travail écrit la géométrie du bloc
à chaque mouvement du pointeur sans prévenir personne d'autre : le modèle de vue s'abonne
donc aux blocs sélectionnés. Sans cela, la barre ne se mettrait à jour qu'au
relâchement — or c'est pendant qu'on la regarde.

*Recette jouée*, `Scripts/recettes/A6-taille-dans-la-barre.sh` (devenue
`A6-Q7-taille-dans-la-barre.sh` au poste Q7). Ce poste n'a rien qu'un fichier livré
puisse porter : la recette appelle les tests d'interface par leur poste et vérifie
qu'ils ont été joués, un filtre qui ne désigne plus rien sortant en 0.

Ce que ces tests éprouvent, et pourquoi ils ne sont pas une mise en scène : le bloc est
sélectionné **avant** la première image, si bien que la case affiche déjà une taille et
que seule une taille qui change peut en remuer les pixels ; l'image est prise par
`CaptureRenderedFrame` et non par `RenderTargetBitmap.Render`, qui redessine de force et
passerait même si plus rien ne se rafraîchissait ; et le découpage porte sur la seule
case de la taille, non sur la barre entière — la position du curseur, deux cases plus
loin, change à chaque mouvement de pointeur, et une comparaison large aurait vu sa
différence à elle.

### A4, A5, A6 — ce que les sabotages ont appris

*6 septembre 2026.* Les trois recettes ont été éprouvées en les faisant échouer, seize
fois. **Douze sabotages attrapés tels que les recettes étaient écrites, quatre qui ont
demandé de réparer le contrôle avant qu'il ne les voie** — et ce sont ces quatre-là qui
ont servi. Le compte est donné dans ce sens plutôt que dans l'autre : une recette qui
laisse passer son propre défaut n'est pas verte, elle est aveugle, et l'écrire douze sur
quatorze reviendrait à s'en dispenser.

| Sabotage | Attrapé par |
|---|---|
| `Ctrl+Maj+F` retiré des `KeyBindings` | recette A4, lecture des sources |
| `Ctrl+Maj+B` déclaré deux fois | recette A4, lecture des sources |
| la ligne du README retirée | recette A4 — **au second essai** |
| le rang d'insertion toujours nul | recette A4, tests de geste |
| la sélection non reposée après remaniement | recette A4, trois tests |
| le seuil de glissement à zéro | recette A4 — **au second essai** |
| le trait d'insertion jamais allumé | recette A4 — **assertion ajoutée après coup** |
| le trait d'insertion toujours en tête de liste | recette A4, même assertion |
| l'export ignorant la hauteur de page | recette A5, quatre mesures sur cinq formats |
| le sélecteur reposant l'A4 devant un format libre | recette A5, mesure de l'écran |
| choisir un format ne faisant plus rien | recette A5, tests du panneau |
| B5 mal mesuré au catalogue | `FormatsDePageTests`, puis recette A5 — **après correction du test** |
| l'abonnement à la géométrie du bloc coupé | recette A6, comparaison de pixels |
| l'abonnement jamais pris | recette A6 |
| la ligne manquante dans `NotifySelection` | recette A6 — **après réécriture du test** |
| les cases de la barre renommées | recette A6 |

**Le premier raté : une recette qui cherchait au mauvais endroit.** Le contrôle des
raccourcis confrontait les gestes déclarés au README *entier*. Retirer la ligne du
tableau ne le faisait pas tomber — les deux raccourcis étant aussi cités dans la prose
d'à côté. Il cherche désormais dans la seule section « Raccourcis », qui est l'endroit
où l'on va chercher un raccourci. Un contrôle qu'on n'a jamais vu échouer ne prouve rien,
et celui-là aurait passé le poste sans rien garder.

**Le second raté : une propriété calculée rend la bonne valeur même quand personne n'a
été prévenu.** Le test de la barre d'état à plusieurs blocs interrogeait
`vm.FrameSizeLabel` ; retirer la ligne de `NotifySelection` — celle-là même dont le
dépôt dit qu'elle s'oublie — ne le faisait pas tomber, puisque lire une propriété
calculée la recalcule. Le test lit maintenant la **case** et non le modèle de vue.

Ce raté-là en a découvert un autre, qu'il faut consigner : à un seul bloc, la barre se
rafraîchit **par accident** même sans cette ligne. Sélectionner un bloc fait passer sa
position par le panneau de propriétés, donc par l'aller-retour points ↔ millimètres qui
laisse 7·10⁻¹⁴ pt derrière lui — la dérive mesurée au poste P3, réputée sans effet. Elle
en a un : elle notifie. C'est le cas à plusieurs blocs, où le panneau ne montre aucun
champ, qui garde réellement la ligne de `NotifySelection`.

**Ce que le sabotage du seuil de glissement a appris** est écrit au poste A4 : ce n'est
pas lui qui empêche un clic de réordonner la pile, c'est l'arithmétique du rang
d'insertion. Le test disait le contraire ; il a été réécrit sur ce que le seuil fait
vraiment.

**Le trait d'insertion n'était éprouvé par rien.** Le test du glissé mesurait l'ordre
obtenu et l'annulation, sans jamais regarder pendant le geste ; le vider entièrement
laissait la recette verte. Or c'est le seul retour que le geste donne — sans lui, on
tire à l'aveugle. Le test l'exige désormais allumé **et à sa place**, entre les deux
mouvements de pointeur, puis éteint au relâchement ; les deux sabotages
correspondants tombent.

**Les recettes compilent, elles ne se contentent pas de ce qui traîne dans `bin/`.**
La première écriture lançait les tests avec `--no-build` et se bornait à vérifier que
la bibliothèque existait. C'est exactement le piège que R3 a relevé — un binaire périmé
survit à une modification des sources — reposé dans un script dont la raison d'être est
d'être rejouable seul, y compris par quelqu'un qui vient de saboter un fichier. Elles
compilent maintenant elles-mêmes et s'arrêtent sur une compilation cassée, comme
`Scripts/controle.sh` le fait depuis P2. Vérifié en sabotant une source et en lançant la
recette **sans** compiler : elle tombe.

**Une correction de comptage, à déclarer.** La ligne « par phase » annonçait « R : 4 sur
5 » alors que les cinq lignes de la phase R portent 100 % depuis la livraison de R5 ;
le total de 15 en héritait. Corrigé à 5 et à 19, la présente livraison comprise.

#### Trouvé, hors périmètre, non corrigé

- **Les bornes du format libre sont celles du champ, pas du format.** Le panneau plafonne
  à 10 et 2 000 mm ; un `.mypub` écrit à la main ou importé peut porter n'importe quel
  nombre, et rien ne le ramène. C'est le même parti que le plafond de vingt colonnes de
  R3 — un garde-fou de saisie. **Vérifié plutôt que supposé** : ce sont les premiers
  `Minimum` et `Maximum` du panneau posés sur une liaison à double sens vers la
  géométrie, et C2a a déjà vu un champ altérer en silence la valeur qu'il affichait. Un
  document de 3 000 mm et un de 5 mm s'ouvrent sans que leur page ne bouge d'un
  milliardième de point ; deux cas de test le gardent, faute de quoi un `.pub` de
  bannière — Publisher monte à 240 pouces — serait redimensionné à la seule ouverture.
- **Le glissé dans la liste ne fait pas défiler celle-ci.** Le rang d'insertion se lit
  sur les éléments réellement construits, et la liste ne construit que ce qu'elle
  affiche : sur une page comptant plus de blocs que la liste n'en montre, on ne peut pas
  tirer le quarantième jusqu'en tête. Le geste reste juste, il est seulement borné à la
  bande visible. Les raccourcis, eux, ne connaissent pas cette limite.
- **Les deux cases neuves de la barre d'état disparaissent sans sélection**, ce qui
  décale les cases voisines au moment où l'on sélectionne. Publisher les laisse vides.
  Retenu ainsi pour ne pas montrer deux cases muettes en permanence ; à revoir si le
  décalage gêne.

### A7 — Alignement vertical du texte *(fiche 12)*

Trois valeurs — haut, milieu, bas. Propriété nouvelle et facultative, donc sans
migration. Immédiat à l'écran, calculable à l'export à partir de la hauteur mesurée.

*Recette* : un titre centré verticalement dans son bloc l'est aussi dans le PDF.

*Fait le 6 septembre 2026*, **avec A2 et le rognage à l'export** : les trois demandent la
même mesure, et le compte rendu est [au poste A2](#a2--signaler-le-débordement-du-texte-fiche-12).

### A8 à A15 — ce que l'audit des fiches avait trouvé

*Reconstitué le 11 octobre 2026.* L'audit du 6 septembre avait nommé onze postes sans en
écrire la liste, et elle s'est perdue avec les transcriptions de la machine où il a été
fait. Un seul numéro a survécu, dans un commentaire de `PubImporter` : **A10, l'objet
trait**. Les autres ont été retrouvés en reprenant les 22 fiches une à une, puis en
classant chaque écart : couvert par un poste, écarté, renvoyé au seuil D, ou sans poste.
Les écarts sans poste se regroupent d'eux-mêmes en onze postes. L'attribution des
numéros, hors A10, est donc refaite et non retrouvée.

#### A8 — Insérer des pages : nombre, position, contenu *(fiche 08)*

`AddPage` insère une page vierge après la page courante, et `DuplicatePage` copie la
page courante. Publisher demande dans un même dialogue un **nombre** de pages, une
**position** (avant ou après) et un **contenu** : page vierge, une zone de texte par
page, ou copie des objets d'une page désignée.

*Recette* : document de 3 pages, page 2 courante ; insérer 4 pages *avant*, copiées de la
page 1. On doit trouver 7 pages, et les pages 2 à 5 portent autant de blocs que la
page 1, en instances distinctes. Les noms sont renumérotés, et une seule annulation rend
les 3 pages. L'option « zone de texte » pose exactement un bloc de texte, égal à la zone
des marges. Effort : faible.

*Rattaché le 25 octobre 2026 à N18*, qui le livre avec cette recette et y ajoute : la touche
`Ctrl+Maj+N`, comme Publisher ; le champ « dupliquer les objets de la page », grisé tant que son
option n'est pas choisie (p. 22) ; `Ctrl+Maj+U`, qui duplique la page. Ce dialogue ne double aucun
champ du dock (décision 5 de la phase N).

#### A9 — Rotation par quart de tour *(fiches 14, 22)*

Deux commandes, +90° et −90°, au menu Disposition et au panneau. La rotation, sa poignée
et l'export tourné existent depuis A1 : il manque le geste rapide.

*Recette* : 350° plus 90° donne 80°, et 0° moins 90° donne 270°. Une image tournée d'un
quart de tour passe `comparer-rendus.sh` sous la tolérance, et chaque commande
s'annule en un pas. Effort : faible.

*Rattaché le 25 octobre 2026 à N18*, qui le livre avec cette recette : les deux commandes au
sous-menu *Faire pivoter ou retourner ▸* de Disposition, comme Publisher (p. 16), et deux boutons
au dock, sans icône, la recette I1 n'admettant que celles de la maquette. A12 reprend le
sous-menu.

#### A10 — Objet trait, et flèche *(fiche 02)*

`ShapeKind` ne connaît que le rectangle et l'ellipse. L'import des « Fiches
limitations » rend ses cinq traits par des rectangles sans épaisseur, ce qui se voit
juste mais se manipule mal. Il faut un `ShapeKind.Line`, une pointe de flèche
facultative à chaque bout, et un survol qui tolère quelques points autour du trait.

*Piège* : `Snap.MinFrameSize` vaut 2 mm, alors qu'un trait horizontal a une hauteur
nulle. Le trait se définit donc par ses deux extrémités, et non par un cadre.

*Recette* :
- `"Shape": "Line"` fait l'aller-retour JSON au caractère près ;
- dans le flux PDF, le bloc est un tracé (`m … l S`) et non un rectangle rempli ;
- le survol répond à moins de 3 pt du trait, et plus au-delà ;
- l'import des Fiches rend 5 traits et 0 rectangle de substitution, et la recette R5
  est ajustée en conséquence.

Effort : moyen.

*Livré le 25 octobre 2026*, avec l'outil Ligne du rail (lot 2 de la phase I), en même temps
que I3, I8 et I9 ; la relecture adversariale et les sabotages du juge viennent après la
fusion. Le trait n'a pas de clé à lui pour ses extrémités : il est un bloc couché, de
hauteur nulle, dont la largeur est la longueur, la rotation l'angle et le centre le milieu.
La rotation, l'export tourné, le déplacement, le groupe et la superposition le servent sans
rien apprendre ; tout ce qui le manipule passe par ses extrémités, jamais par un
redimensionnement de cadre. *Choisi sans certitude* :
- le survol prend un trait à 3 pt de son **encre** — de l'axe, moins la demi-épaisseur ;
- la poignée de rotation se tait sur un trait, comme dans Publisher : tirer une extrémité le
  tourne déjà, et l'angle reste au dock, autour du milieu ;
- la pointe est la flèche moyenne d'Office, longue et large de trois fois l'épaisseur,
  comptée au moins pour 2 pt ; le fût s'arrête sous elle, à mi-longueur ;
- un trait neuf prend la couleur principale du jeu, d'un point, sans fond.

*Relevé, non corrigé.* libmspub ne transmet aucune pointe de flèche — son relevé n'a pas de
clé `draw:marker-*` — : un `.pub` à flèches s'importe sans ses pointes, et les deux documents
ATR n'en portent pas. Un trait debout ou oblique, tourné, ne s'aimante pas quand on le
déplace, comme tout bloc tourné ; ses extrémités, si. « Ajuster aux marges » écrit la
hauteur des marges dans un trait, qui se dessine alors au milieu de cette hauteur, couché
sur la largeur des marges : forcer la hauteur à zéro dans le modèle rendait le bloc
dépendant de l'ordre où l'on pose sa figure et sa hauteur, ce que la copie a montré.

*Repris le 26 octobre 2026 après relecture.* Un défaut, montré par deux tests purs écrits
avant sa correction, et le juge, que le poste n'avait pas.

- **Un groupe mettait ses traits à l'échelle par leur largeur.** `GroupFrame.OnResized`
  multiplie l'abscisse et la largeur de chaque bloc quand on élargit le groupe, l'ordonnée et
  la hauteur quand on le grandit. Un trait n'a que sa largeur, que sa rotation oriente :
  élargi, le groupe allongeait ses filets debout, qui sortaient de lui ; grandi, il les
  laissait à leur longueur, décollés de ses bords ; un trait oblique changeait de longueur et
  non d'angle. La livraison disait que le groupe le servait « sans rien apprendre ». Il le
  met désormais à l'échelle par ses extrémités, comme les coins d'un bloc
  (`Un_groupe_etire_garde_ses_traits_dun_bord_a_lautre`,
  `Un_trait_oblique_suit_le_groupe_etire_et_garde_son_sens`). Un autre bloc tourné garde la
  règle d'avant, fausse elle aussi sous un étirement qui n'est pas le même dans les deux
  sens : c'est la règle du groupe, hors du poste.
- **Le juge.** Vingt-cinq sabotages ont été joués à la main contre les tests du poste et sa
  recette : vingt-trois tombaient, la recopie des pointes tombe par `CloneTests`. Treize sont
  au juge, un par chose gardée : le survol, la hauteur, `Maj`, la découpe des pointes, la
  pointe et le tracé au PDF, les bouts à l'écran, la recopie, le groupe dans ses deux sens,
  la poignée de rotation, la vignette, l'import. Joué le 26 octobre 2026 : treize sur treize. La suite d'interface passe entière (284 tests), celle du noyau aussi (461), et les recettes A10 et R5. Le vingt-cinquième n'y est pas :
  la garde qui ne repose pas un trait sur ses propres extrémités quand on quitte un champ du
  dock ne se voit d'aucun geste — un champ qu'on traverse sans rien taper n'écrit rien, et le
  test écrit pour la montrer passait avec elle et sans elle.
- *Soupçon, non montré* : un groupe fait de traits horizontaux à la même hauteur n'a pas de
  hauteur, et ne se prend qu'en tombant pile sur son cadre.
- *La recette de I5 comptait exactement* sept cibles du juge dans `TemplatePreview.cs` : la
  vignette en rectangle en fait une huitième, et elle tombait sur l'arbre propre. Elle en
  demande désormais sept au moins — I8 en pose une aussi.

#### A11 — Aimantation réglable, et aux autres blocs *(fiche 22)*

Seul `Alt` coupe aujourd'hui l'aimantation, et seulement le temps d'un geste. Publisher
offre un réglage durable, et l'aimantation aux autres objets. `Snap.Edge` est une
fonction pure ; il suffit de lui donner, en plus des repères, les bords et les milieux
des blocs non sélectionnés.

*Recette* (test pur) : un bloc A en x = 100, de 50 de large ; un bloc B lâché en x = 148
se cale à 150 quand l'aimantation aux objets est active, et reste à 148 sinon. Avec
l'aimantation coupée, rien ne se cale, pas même sur la marge. Le réglage survit à la
relecture des préférences. Effort : moyen.

*Repris le 25 octobre 2026, dans la phase N (salve Saisie 13).* La fiche 23 (c22) ajoute ce que le
poste ne visait pas : l'aimantation au **redimensionnement** et au **tracé**, absente aujourd'hui,
et *Disposition ▸ Aligner sur ▸* — repères de règle, repères de mise en page, objets —, le *Snap ▸*
de Publisher. Le réglage est rangé dans `Services/Preferences.cs`, que N11 crée : c'est le magasin
que la recette suppose et qui n'existait pas. Dépend de N17 et N11.

#### A12 — Menu contextuel du bloc *(fiche 11)*

Un clic droit sur un bloc le sélectionne et ouvre un menu qui reprend les commandes
d'Édition et de Disposition. Aucun `ContextMenu` n'existe dans les vues. Le poste est à
faire **après** A9 et B2, pour reprendre leurs commandes.

*Recette* :
- un clic droit synthétisé au centre d'un bloc le sélectionne et ouvre le menu ;
- un clic droit hors de tout bloc vide la sélection ;
- un script vérifie que chaque commande du menu est liée à la même commande que son
  pendant dans les menus Édition et Disposition.

Effort : faible.

*Repris et élargi le 25 octobre 2026, dans la phase N (salve Saisie 6).* Le menu de Publisher (p. 34,
fiche 11) commence par Couper, Copier, Coller, et mène au dialogue de format ; le relevé de la
fiche 23 (c3, c16, c25, e1, e2, f16) en fait le geste réflexe d'un utilisateur d'Office.
- **La pression du bouton droit** suit le chemin de celle du gauche jusqu'à la sélection, puis
  s'arrête sans glissé : un bloc hors de la sélection est pris seul ; une sélection qui contient le
  bloc reste ; un clic dans le vide vide la sélection.
- **Le `ContextMenu` est posé sur `PageCanvas`**, et non sur `InputLayer` : la touche Menu lève
  l'événement sur le contrôle qui a le focus, et la couche ne l'a jamais. Il s'ouvre aussi par
  `Maj+F10`, sous la sélection au clavier, et se tait sur les règles et les barres de défilement.
- **Ses entrées, selon la sélection** : Couper, Copier, Coller (N8) ; Supprimer le texte, une
  commande neuve, grisée si le bloc est vide ou à champ ; Supprimer ; Ordre ▸ ; Faire pivoter ▸
  (A9) ; Grouper, Dissocier, Regrouper ; Aligner ou répartir ▸ ; Modifier le texte ▸ Importer un
  texte… (B2) ; **Format du bloc…**, qui ouvre la section du dock propre à sa sorte (N18) ; Zoom ▸.
  Sur un bloc de coordonnées, les entrées de la balise de Publisher (p. 54) : Modifier les
  coordonnées…, Convertir en texte. Dans le vide : Coller, Repères de mise en page…,
  Arrière-plan….
- **Un double-clic sur une image ou une forme** ouvre sa section du dock (décision 5).
- **En saisie**, le menu du texte garde Couper, Copier et Coller, et ajoute Format du bloc… ; N25
  y mettra les suggestions d'orthographe en tête.

*Piège* : `ContextRequested` n'est levé qu'au relâché, et seulement si la source est le contrôle
lui-même ; la ligne du juge `var hit =` ne doit pas être recopiée ; rien du seuil D n'apparaît —
« Ajuster le texte », « Habillage » — : la recette étend à `PageCanvas.axaml` la liste d'interdits
de I2 ; N5 doit aussi rendre au bouton droit la pression qui sort de la saisie.

*Recette*, en plus de celle d'origine : chaque entrée liée à la même commande que son pendant aux
menus, avec le même geste affiché ; `Maj+F10` ouvre le menu ; un clic droit sur une règle n'ouvre
rien ; un double-clic sur une image ouvre la section Image. Dépend de N8, N17 et N18. Des deux
postes qu'il attendait, B2 est livré, et A9 l'est avec N18. Effort : moyen.

#### A13 — Rognage manuel de l'image *(fiche 14)*

Un rognage facultatif sur les quatre côtés d'un `ImageFrame`, réglé au panneau puis par
un mode de poignées. La découpe existe déjà pour le remplissage par image (C5c) : les
deux doivent partager le même calcul, dans le noyau.

*Recette* : une image de 200 × 100 px rognée de 50 % à gauche. À l'écran comme au PDF,
le rendu est la moitié droite de la source (écart ImageMagick sous un seuil). Un
fichier sans rognage reste identique octet pour octet. Effort : moyen.

*Repris le 25 octobre 2026, dans la phase N (salve Saisie 14).* En plus : « Rogner » au menu contextuel
de l'image (A12) entre dans le mode de rognage, où les poignées rognent au lieu de redimensionner,
comme l'outil de la barre Picture de Publisher (fiche 14) ; Échap en sort. Dépend de A12.

#### A14 — Contour et ombre du caractère *(fiche 17)*

C'est ce que « Ce que ce plan ne fait pas » promettait à la place de WordArt : « un
titre qui se voit », par le contour, l'ombre et le dégradé. La ligne renvoyait à C5c,
qui n'a fait que des fonds de bloc : le texte, lui, n'a rien reçu. Le poste porte le
contour (couleur, épaisseur) et l'ombre (décalage, couleur) sur le bloc de texte.

*À éprouver d'abord* : que PDFsharp sache écrire un texte en mode contour. Faute de
quoi, il faudrait tracer les glyphes un par un, et le poste change de taille.

*Recette* : avec contour, le flux PDF porte un mode de rendu `1 Tr` ou `2 Tr` ; avec
ombre, le texte est écrit deux fois, au décalage demandé ; `comparer-rendus.sh` passe
sous la tolérance. Effort : moyen, voire lourd si PDFsharp ne suit pas.

#### A15 — Sections : numérotation qui repart *(fiches 08, 22)*

Un début de section, facultatif, sur `Page`, avec son premier numéro. Le folio de C4
compte alors depuis la dernière section ouverte.

*Recette* : 8 pages, une section ouverte en page 3 et numérotée à partir de 1.
`pdftotext`, page par page, lit 1, 2, 1, 2, 3, 4, 5, 6 ; un document sans section se
numérote comme avant. Effort : faible.

---

## Phase B — Démarrage et transport

La phase A polit ce qui existe. La phase B change ce que le logiciel *est* pour qui
l'ouvre. Aucun de ces postes ne touche au modèle de données.

### B1 — Modèles *(fiche 18)* — **le poste le plus rentable du plan**

Un modèle n'est rien d'autre qu'un document : le format `.mypub` suffit déjà, et
`Samples/exemple-bulletin.mypub` prouve qu'il porte un document réaliste. Il manque
trois choses, aucune structurelle :

1. un dossier `Templates/` livré avec l'application ;
2. un écran de choix au démarrage et sur `Ctrl+N` — grille de vignettes, plus l'entrée
   « document vierge » ; les vignettes se génèrent en rendant la première page ;
3. l'ouverture **en copie** : charger le document mais laisser le chemin de fichier à
   vide, pour que `Ctrl+S` propose *Enregistrer sous* et n'écrase jamais le modèle.

Puis une poignée de modèles réellement composés : bulletin, dépliant trois volets,
carte de visite, affiche.

*Recette* : `Ctrl+N`, choisir « dépliant », modifier un texte, enregistrer — le fichier
d'origine est intact.

*Fait le 5 septembre 2026.* Les trois pièces annoncées, et rien de plus : un dossier
`Templates/` livré à côté du binaire, un catalogue au démarrage et sur `Ctrl+N`, et
l'ouverture en copie — le document est chargé sans son chemin, si bien qu'« Enregistrer »
propose « Enregistrer sous ». Aucune ligne n'a été ajoutée au modèle de données : un
modèle est un `.mypub` ordinaire, lu par le même code que les documents de l'utilisateur.

Quatre modèles composés : bulletin trois colonnes sur deux pages, dépliant trois volets,
carte de visite, affiche A3. Ils se servent de ce que les postes précédents ont apporté —
grille et lignes de base (C2a), repères de pli (C2b), folio et date (C4a), coordonnées et
logo (C4b), groupes (C3c), fond et contour (C1). C'est la contrepartie annoncée : la
moitié « coordonnées » de C4 sert enfin à quelque chose, et une carte de visite dont les
neuf blocs sont des champs se remplit toute seule.

*Écart assumé* : les vignettes sont un schéma et non un rendu. À cent cinquante pixels de
large, un A4 réduit son corps de onze points à deux pixels — un rendu fidèle ne montrerait
qu'une bouillie grise. Ce qu'on choisit dans un catalogue, c'est une structure ; c'est
donc elle qu'on dessine, le texte en filets et l'image en cadre barré. Cela évite au
passage un troisième moteur de rendu, alors que tout l'équilibre du projet tient à ce que
l'écran et l'export partagent le modèle sans couche intermédiaire.

Recette jouée : partir du dépliant, vérifier qu'aucun chemin n'est repris, modifier le
titre de couverture, enregistrer ailleurs — le fichier du modèle est intact octet pour
octet, et la copie porte bien la modification. Les quatre modèles s'exportent sans un
avertissement.

*Trouvé au passage*, et corrigé avant : l'application suivait la variante de thème du
système alors que sa palette est écrite en clair ; sur un bureau sombre, champs et menus
sortaient sombres au milieu de panneaux clairs. Corrigé aussi, dans ce poste : un bloc
image sans chemin est un emplacement qui attend son image — c'est ainsi qu'un modèle
réserve sa place — et non une image perdue ; il ne se signale donc plus à l'export.

C'est aussi le seul moyen de rendre visibles les fonctions **déjà présentes** :
l'aimantation, les modes de cadrage d'image, la justification. Un utilisateur qui
ouvre une page blanche n'en découvre aucune.

### B2 — Importer un fichier texte *(fiche 22)*

Un sélecteur de fichier qui remplit le texte du bloc. C'est le chemin naturel pour faire
entrer du contenu rédigé ailleurs — et aujourd'hui il n'y en a aucun, hors copier-coller.

Le texte se pose par `Saisie`, jamais par `Text` seul (voir Q1) : écrire `Text` ne
recale rien, et les fragments et écarts d'avant resteraient à leurs positions, sur un
texte qui n'est plus le leur, voire au-delà de sa fin. Par `Saisie`, le texte neuf prend
le style du premier caractère qu'il remplace, comme une sélection retapée ; pour
repartir du style du bloc, vider `Spans` et `Paragraphs` avant d'écrire `Text`.

*Recette* : un `.txt` de 3 000 signes arrive dans un bloc, et A2 signale qu'il déborde.

*Fait le 12 octobre 2026.* « Importer un texte… » est au menu *Insertion*, à la place
qu'y tient « Text File… » chez Publisher, et en bouton au panneau du bloc de texte. Les
deux mènent au même sélecteur de fichier (filtre « Texte », puis « Tous les
fichiers »), et la fenêtre ne fait rien d'autre : le chemin passe au modèle de vue,
`ImporterTexte(chemin)`, où un test l'atteint sans dialogue — le partage que Q3 a fait
pour `ChoisirImageDeFond`. Le décodage est une fonction pure du noyau,
`Services/TexteImporte.cs`.

**Le texte repart du style du bloc** : `Spans` et `Paragraphs` sont vidés, puis le texte
est posé par `Saisie`, qui n'a alors plus rien à recaler. C'est l'une des deux lectures
que cette section ouvrait, et la raison du choix est que l'autre fait dépendre le résultat
de ce qui était là. Par la seule `Saisie`, trois mille signes prendraient le gras et le
corps du titre qui ouvrait le bloc. Et comme la déduction de Q1 retient le plus long
début et la plus longue fin communs aux deux textes, quelques caractères y garderaient
par hasard leur style d'avant : « Titre » importé sur « Titre » resterait gras, et un test
l'épingle. Un fichier texte ne porte aucun style ; il prend celui du bloc, qu'on règle
ensuite au panneau.

L'encodage se devine dans cet ordre : une marque UTF-8 (EF BB BF) est retirée et le reste
lu en UTF-8 ; sans marque, un fichier qui est de l'UTF-8 **valide** se lit en UTF-8 ;
sinon, c'est du Latin-1. Les fins de ligne CRLF, et le `\r` seul, deviennent `\n`. La
barre d'état dit l'encodage retenu et le nombre de signes. Un seul pas d'annulation,
ouvert et refermé par `ImporterTexte`. Un bloc dont le texte est calculé par un champ
refuse l'import, menu et bouton grisés : le texte y est caché et gardé pour le jour où
l'on retire le champ.

*Mesuré.* Quinze tests sous `poste=B2` : dix purs (`TexteImporteTests` : UTF-8 avec et
sans marque, Latin-1, octets 80 à 9F, ASCII, marque suivie d'une séquence invalide,
CRLF, `\r` seul, saut final, fichier vide) et cinq dans la fenêtre (`ImportTexteTests`).
Le premier de ceux-ci joue la recette du plan sur le fichier le moins commode : trois
mille signes en Latin-1, fins de ligne CRLF, une fin de ligne en fin de fichier. Le bloc
porte le texte au signe près, le marqueur rouge de A2 paraît sur la page, qui n'y était
pas avant, et une annulation rend le document d'avant, sans autre pas dans la pile. Les
autres éprouvent l'annulation d'un bloc riche (fragment et écart compris, puis
rétablissement), le style du bloc, le refus sous un champ ou sur un fichier absent, et
le menu et le bouton : offerts au seul bloc de texte, retirés quand on lui pose un champ.

*Recette jouée* : `Scripts/recettes/B2-import-texte.sh`. Elle joue les quinze tests et
mesure, sur les documents que le premier test d'interface écrit pour elle :
- le fichier n'est pas de l'UTF-8 valide, il porte des CRLF, il fait 3 000 signes une
  fois les sauts ramenés à un caractère, et le bloc porte exactement ce texte, sans
  fragment ni écart ;
- l'export écrit « Page 1 · Article : le texte déborde du bloc de 894 pt », et le
  document d'avant ne déborde pas ;
- seize mots accentués du début du texte se relisent dans le PDF (`pdftotext`). La
  contre-épreuve relit le même fichier en UTF-8 sans repli, chaque accent devenu
  « � », et la même mesure échoue ;
- le document après une annulation est celui d'avant l'import, octet pour octet.

Cinq sabotages au juge, éprouvés à la main contre la recette et les tests du poste : le
Latin-1 lu en UTF-8, les CRLF gardés, l'import hors transaction (qui ne s'annule plus), les
fragments d'avant gardés, le menu offert à tout bloc. Chacun fait tomber au moins un test
du poste, et la recette avec eux. `Scripts/verifier.sh` passe en entier : 280 tests purs,
110 d'interface, vingt-trois recettes.

*Choisi sans certitude* :
- **Windows-1252 pour « Latin-1 ».** Les deux ne diffèrent que sur les octets 80 à 9F :
  des caractères de commande en Latin-1 strict, que nul texte n'emploie ; « ’ », « œ »,
  « € », « … » en Windows-1252, qu'un texte français écrit sous Windows emploie partout.
  Un fichier Latin-1 se lit donc à l'identique, et un fichier Windows garde ses
  apostrophes. C'est aussi ce que fait un navigateur d'un document déclaré
  « iso-8859-1 ». La barre d'état dit « Latin-1 » dans les deux cas ;
- **le saut qui clôt le fichier est retiré**, un seul. Gardé, il ouvrirait un
  paragraphe vide en bas du bloc, qui compte dans sa hauteur : c'est la ligne fantôme
  que la recette de A2 a trouvée dans l'import des `.pub`, et qui faisait signaler des
  débordements qui n'existaient pas. Une ligne vide voulue en fin de fichier en laisse
  une ;
- **une marque UTF-8 suivie d'octets invalides** se lit en UTF-8, les octets fautifs
  devenant « � », plutôt que de faire relire tout le fichier en Latin-1, marque comprise.

*Trouvé et laissé tel quel* :
- l'import remplace tout le texte du bloc, et n'insère pas au curseur de la saisie :
  la liaison ne transmet pas le curseur (voir Q1), et le geste ne le cherche pas ;
- une case de tableau ne s'importe pas : le poste dit « le bloc sélectionné », et une
  case n'est pas un bloc qu'on sélectionne ;
- la fiche 22 dit encore « `manquant` » pour « Text File… » : elle n'a pas été
  retouchée.

*Repris le 12 octobre 2026, après la relecture du poste.* La relecture a trouvé six
trous ; les six sont refermés, chacun avec ses tests et au moins un sabotage.
- **Import pendant une saisie ouverte sur la page.** La transaction de l'import
  s'emboîtait dans celle de la saisie et n'empilait rien : une seule annulation
  défaisait la frappe et l'import ensemble, et la zone de saisie restait à l'écran sur
  le texte neuf. Le geste passe maintenant par `MainWindow.ImporterTexte(chemin)`, qui
  referme la saisie sur la page (`EndTextEdit`) et la transaction du panneau
  (`ClosePropertyEdit`) avant d'appeler le modèle de vue ; celui-ci referme encore toute
  transaction restée ouverte (`EndPendingEdit`) avant d'ouvrir la sienne. Deux tests
  dans la fenêtre : une frappe sur la page puis un import font deux pas, et la zone
  quitte l'écran ; une transaction ouverte comme le fait le panneau, puis un import,
  font deux pas aussi.
- **Binaire et UTF-16.** Les marques FF FE et FE FF sont reconnues, et le reste se lit en
  UTF-16 strict ; un nombre impair d'octets derrière la marque est refusé. Un fichier
  qui porte un octet nul ou un caractère de commande — hors tabulation, saut de ligne,
  retour chariot et saut de page — est refusé, le message disant le caractère et sa
  place : c'est ce qui arrête un binaire et un fichier UTF-16 sans marque, qui écrit
  l'ASCII avec un octet nul sur deux.
- **Taille.** Au-delà de 128 Kio, le fichier est refusé avant d'être lu. Mesuré dans la
  fenêtre (le banc de la relecture, une ligne accentuée répétée, un bloc de
  240 × 160 pt) : le premier rendu après l'import prend 0,8 s à 128 Kio, 1,5 s à
  256 Kio, 3,2 s à 512 Kio, 6,6 s à 1 Mio. La relecture avait mesuré 19 s à 1 Mo et
  près de 7 Go de mémoire à 10 Mo, dans une autre passe ; l'écart entre 19 s et 6,6 s
  n'est pas expliqué.
- **Latin-1 pris pour de l'UTF-8.** Le décodage UTF-8 était déjà strict, avec repli en
  Windows-1252 sur toute séquence invalide. Il est durci d'un cas : de l'UTF-8 valide
  qui donne un caractère de commande C1 (U+0080 à U+009F) se relit en Latin-1 — c'est
  « Â » suivi de « ’ », « € » ou « œ » en Windows-1252. Ce qui reste est une **limite
  documentée** (ici, dans `TexteImporte` et au README) : un fichier Latin-1 dont tous
  les accents forment par hasard de l'UTF-8 valide se lit en UTF-8. Il faut qu'aucun
  accent ne soit suivi d'une lettre ASCII ni d'un blanc ordinaire — un texte court en
  capitales, « CAFÉ » suivi d'une espace insécable (C9 A0, lu « ɠ ») ; un seul « é »
  ordinaire ailleurs dans le fichier le fait relire en Latin-1.
- **Fichier vide.** Refusé, et avec lui un fichier qui ne porte qu'une fin de ligne ou
  qu'une marque : lus, ils donnent un texte vide qui effaçait le bloc.
- **Fichier sans droit de lecture.** Il était déjà refusé ; un test le montre
  maintenant (`chmod 000` par `File.SetUnixFileMode`) : message à la barre d'état,
  bloc, historique et indicateur de modification intacts. Le test vérifie d'abord que
  le fichier est vraiment fermé, pour ne pas passer à vide sous root.

Le refus est une exception du noyau, `TexteRefuseException`, dont le message passe tel
quel à la barre d'état après « Import impossible : ». Les fichiers refusés — vide,
binaire, trop gros, fermé — ont chacun leur test dans la fenêtre, qui vérifie aussi que
rien n'est empilé.

*Mesuré à la reprise.* Trente et un tests sous `poste=B2` : vingt purs, onze dans la
fenêtre ; la recette en exige autant, et passe. Neuf sabotages de plus à la fin du
juge, chacun appliqué à la main, vu tomber, puis restauré par `git checkout` — et les
cinq premiers rejoués sur le code repris : tous tombent encore.Les neuf : la saisie sur la page laissée
ouverte, l'import emboîté dans la transaction du panneau, la marque UTF-16
petit-boutiste ignorée, l'octet nul admis, les autres commandes admises, la borne de
taille centuplée, le repli C1 retiré, le texte vide admis, le refus d'un fichier fermé
retiré. Chacun fait tomber au moins un test du poste, et la recette avec lui.
`Scripts/verifier.sh` passe en entier : 290 tests purs, 116 d'interface,
vingt-trois recettes.

*Choisi sans certitude, à la reprise* :
- **128 Kio.** Le seuil garde le premier rendu sous la seconde, mesuré sur une seule
  machine ; il est bien au-dessus de ce qu'un bloc montre, et en dessous de ce qu'un
  long document pourrait vouloir y verser d'un coup ;
- **le saut de page (FF) est admis**, comme la consigne de la relecture le dit ; rien
  n'a mesuré ce que la composition en fait ;
- **un caractère C1 est refusé même lu en Latin-1** : Windows-1252 laisse cinq octets
  sans lettre (81, 8D, 8F, 90, 9D), qui deviennent des commandes C1 ; un fichier qui les
  porte n'est vraisemblablement pas du Windows-1252. Déduit, non mesuré sur un fichier ;
- **un texte de blancs seuls est admis** : il n'est pas vide, et le refuser serait
  deviner une intention.

*Trouvé à la reprise et laissé tel quel* :
- un fichier UTF-32 petit-boutiste commence par FF FE 00 00 : il est pris pour de
  l'UTF-16 et refusé pour ses octets nuls, avec un message qui parle d'UTF-16 sans
  marque. Déduit de l'ordre des essais, non mesuré ;

### B3 — Imprimer *(fiche 22)*

MyPublisher exporte un PDF mais n'imprime pas. Pour un logiciel de PAO, c'est un trou
dans le parcours : le PDF peut servir d'intermédiaire, mais l'utilisateur ne devrait pas
avoir à le savoir.

*Recette* : `Ctrl+P` sort le document sur une imprimante système.

*Précisé le 25 octobre 2026 par la phase N* : `Ctrl+P` est réservé à ce poste par la table française
(N10), à côté de `Ctrl+Maj+P`, l'export.

### B4 — Archive `.mypubz` *(fiche 21)*

Un zip contenant le JSON, un dossier `images/` et la liste des polices employées. Le
sérialiseur actuel fait 32 lignes ; une classe voisine suffit, le choix se fait au filtre
du sélecteur d'enregistrement. A3 traite le cas courant ; B4 traite le cas du transport.

*Recette* : ouvrir sur une autre machine une archive produite ici, images comprises.

### B5 — Avertir sur les substitutions de polices *(fiche 21)*

La substitution est aujourd'hui totalement muette : le résolveur accepte le fichier que
lui rend `fc-match` sans vérifier qu'il correspond à la famille demandée, et retombe
silencieusement sur DejaVu Sans dans trois cas — `fc-match` absent, police introuvable,
ou fichier `.ttc` que PDFsharp ne gère pas. Il a tout ce qu'il faut pour signaler
l'écart ; il ne le remonte à personne.

*Recette* : exporter un document demandant une police absente produit un avertissement
nommant la police demandée et celle réellement employée.

*Fait le 6 septembre 2026.* `fc-match` est désormais interrogé sur le fichier **et** la
famille, et l'export confronte ce qu'il obtient à ce que le document demandait :

```
Police substituée : « Verdana Pro Cond SemiBold » demandée, « Noto Sans » employée
                    (absente de cette machine).
Police substituée : « Noto Sans CJK JP » demandée, « DejaVu Sans » employée
                    (collection .ttc, que l'export ne sait pas lire).
```

Le motif entre parenthèses n'est pas décoratif : sans lui, la seconde ligne serait
incompréhensible — la police est installée, et pourtant remplacée.

**Un avertissement par police, et non par bloc.** Les fiches ATR portent 119 fragments
en Wingdings ; un avertissement par bloc en ferait une liste que personne ne lit. C'est
la police qui est substituée, pas le bloc — comme « Image introuvable » nomme le fichier
et non le cadre qui l'affiche. Le dédoublonnage porte sur la paire demandée/employée :
le romain et le gras d'une même famille absente tombant sur le même substitut ne
valent qu'une ligne, mais s'ils divergent, les deux se disent.

**Le relevé se prend sur le document, pas pendant le dessin.** PDFsharp garde en cache
la résolution d'une police pour toute la durée du processus et n'appelle donc le
résolveur qu'une fois par famille : un relevé pris au vol serait complet au premier
export et **vide au second** — or l'application exporte plusieurs fois de suite, et
c'est le dernier PDF qu'on envoie. La passe lit les polices sur le modèle : celle du
bloc, et celle de chacun de ses fragments, groupes traversés. Elle ne peut donc pas
davantage être cassée par un remaniement de `DrawText`.

#### Ce que le poste a trouvé

**fontconfig rend *tous* les noms d'une famille, séparés par des virgules.** « DejaVu
Sans Condensed » lui vaut la réponse `DejaVu Sans,DejaVu Sans Condensed`, « Lato
Semibold » vaut `Lato,Lato Semibold`, et une police coréenne vaut `Malgun Gothic,맑은
고딕`. Comparer la chaîne entière au nom demandé — ce que la première écriture faisait —
annonçait une substitution sur **81 polices installées de cette machine**. C'est le seul
défaut qui aurait tué le poste : un avertissement qui crie au loup sur des polices
présentes s'apprend à ne plus être lu, et le jour où une flèche de procédure d'urgence
est vraiment remplacée, personne ne regarde. La comparaison cherche donc le nom demandé
**parmi** les noms rendus.

Ce cas-là ne s'éprouve qu'avec une police à noms multiples réellement installée. La
recette et le test vont donc en chercher une sur la machine qui les joue, plutôt que
d'en écrire une en dur qui n'y serait pas — **et passent leur chemin s'il n'y en a
aucune**. C'est la seule réserve du poste, et elle est écrite ici plutôt que tue : sur
cette machine-ci les 81 familles concernées font que le contrôle tourne toujours.

**Un interblocage possible, corrigé au passage** : `RedirectStandardError` était activé
sans que la sortie d'erreur soit jamais lue, puis `ReadToEnd()` bloquait sur la sortie
standard. Un `fc-match` qui aurait rempli son tampon d'erreur serait resté bloqué en
écriture, et l'export avec lui. La sortie d'erreur est maintenant vidée en parallèle.

**Ce qui reste muet** : l'écran. Avalonia substitue de son côté, par son propre
gestionnaire de polices, et le dire à l'affichage demanderait une seconde mesure, prise
ailleurs, qui pourrait diverger de celle-ci. Ce que l'utilisateur voit aujourd'hui,
c'est l'avertissement de l'export, remonté dans la barre d'état à `Ctrl+E` — et il y
passe **en tête**, la barre ne montrant que le premier.

**Vérifié sur les polices des documents réels** : Calibri, Wingdings et Wingdings 3 se
résolvent exactement et ne disent rien ; *Verdana Pro Cond SemiBold* se signale. Le
relevé de `Scripts/reference.txt` n'a pas bougé — les documents du dépôt n'emploient que
DejaVu Sans et DejaVu Serif.

*Recette jouée*, et éprouvée en la faisant échouer quatre fois : résolveur qui accepte
tout ce que `fc-match` rend, comparaison par égalité stricte des chaînes, fragments de
style ignorés, dédoublonnage retiré. Quatre sabotages, quatre attrapés.

Un cinquième a servi : la comparaison de chaînes réécrite **dans** la résolution,
`Correspond` restant juste mais n'étant plus appelée. Les tests sur chaînes figées ne
voyaient rien passer — un test qui traverse tout le chemin de résolution avec une
famille à noms multiples cherchée sur la machine a été ajouté pour cela.

### B6 — Aperçu avant impression *(fiche 22)*

Une fenêtre qui montre les pages telles qu'elles sortiront, sans repère, sans marqueur
de débordement et sans sélection. Le banc sait déjà rendre une page hors champ : c'est
le même rendu, montré au lieu d'être comparé. À faire avec B3 ou juste après.

*Recette* : chaque page de l'aperçu, comparée au `pdftoppm` de l'export par la logique
de `comparer-rendus.sh`, reste sous la tolérance. Aucun pixel n'a la couleur des
repères ni celle du marqueur rouge. Effort : faible à moyen.

*Précisé le 25 octobre 2026 par la phase N* : dans l'aperçu, `F9` bascule en taille réelle et Échap
le ferme, comme dans Publisher (aide en français).

### B7 — Replier les panneaux *(fiche 04)*

La seule chose que « Ce que ce plan ne fait pas » retenait du volet Office, et
qu'aucun poste ne portait : masquer les deux colonnes latérales pour rendre leur place
à la planche, sur `Ctrl+F1` comme dans Publisher.

*Recette* :
- après `Ctrl+F1`, la planche gagne au moins la largeur des deux panneaux, et ceux-ci
  sont invisibles ; un second `Ctrl+F1` rend l'état d'avant ;
- le raccourci figure au README et n'en double aucun autre.

À vérifier d'abord : que le bureau ne capte pas `Ctrl+F1`, comme il captait `Alt+F6`
en A4. Effort : faible.

*Fait le 25 octobre 2026, avec I2*, au menu Fenêtre que la maquette lui donne ; sa recette
est celle de I2 (`I2-B7-disposition`). Ni GNOME ni KDE ne prennent `Ctrl+F1` par défaut.

---

## Phase C — Refactorings structurels

**Gate : P1 et P2 doivent être faits.** Ces postes déplacent des propriétés entre
classes, changent le format de fichier et rendent le rendu récursif. Ordre conseillé,
les dépendances étant respectées.

### C1 — Fond et bordure au niveau de `Frame` *(fiches 09, 11)*

Applique la décision P0. Remonter le remplissage, le contour, son épaisseur et l'arrondi
de `ShapeFrame` vers `Frame`, sous le nom retenu. Effet : les blocs de texte gagnent une
bordure, les images gagnent fond **et** bordure. Côté export, la fonction qui trace les
formes sait déjà faire les deux — il suffit de l'appeler avant le texte et l'image,
plutôt que de dessiner le rectangle à la main comme le fait aujourd'hui le rendu du texte.

Ajouter dans la foulée : transparence exposée (l'octet alpha la porte déjà, elle n'est
juste offerte nulle part) et traits en pointillés.

*Recette* : encadrer une image d'un filet de 2 pt sans superposer de forme ; l'export
est identique à l'écran. Le contrôle P2 passe.

*Fait le 5 septembre 2026.* Fond, contour, épaisseur, trait (continu, tirets, points)
et arrondi sont sur `Frame` ; l'opacité du fond est offerte en clair dans le panneau,
en plus du sélecteur. À l'écran comme à l'export, le fond passe sous le contenu et le
contour dessus — un filet sur une image passe sur son bord, pas sous elle — et le
contour se trace même quand le contenu échoue : une image illisible garde son cadre.
Les motifs de trait sont une seule table, en multiples de l'épaisseur, que les deux
rendus lisent ; le PDF en porte la trace (`3 w [12 6] 0 d`).

`FormatVersion` passe à 2. Un fichier de la version 1 — C3c y avait déjà ajouté le
discriminant `group` sans changer de numéro, une addition ne cassant rien — est migré
à l'ouverture, groupes compris ; un fichier plus récent est refusé avec son numéro. Le
contrôle P2 a gagné une quatrième étape : la version 1 du document d'exemple, figée
dans `Scripts/fixtures/`, doit s'ouvrir et se migrer en un point fixe.

Vérifié en rendant la même page des deux côtés — l'écran hors champ, par
`RenderTargetBitmap`, l'export par `pdftoppm` — à deux pixels par point : mêmes tirets,
mêmes points, même filet de 2 pt, mêmes arrondis ; 2,4 % des pixels diffèrent, tous
sur le lissage des bords, le texte et le repère de marge propre à l'écran.

### C2 — Repères *(fiches 05, 06, 02)*

**C2a — Grille de composition.** Colonnes, lignes, et grille de ligne de base réglée en
points. Le mécanisme d'aimantation prend déjà un tableau de repères en paramètre : toute
la mécanique consiste à l'allonger. C'est ce qui fait passer l'aimantation d'un confort
(centrer) à un outil de composition (un bulletin en trois colonnes).

**C2b — Règles et repères libres.** Règles graduées sur les deux bords, puis repères
posés au menu ou glissés depuis la règle, déplaçables, sérialisés, non imprimés. C'est
le seul mécanisme qui permette d'aligner sur une position choisie par l'utilisateur —
tant qu'il manque, l'aimantation ne sert qu'aux positions décidées d'avance.

*Recette* : composer trois colonnes, y aligner quatre blocs sans les positionner au
chiffre ; les repères n'apparaissent pas dans le PDF.

*C2a fait le 5 septembre 2026.* Six réglages sur le document — colonnes, gouttière,
lignes, espacement, pas et décalage de ligne de base — et quatre listes calculées : ce
qui se trace, et ce sur quoi on s'aimante. Le pas des lignes de base se règle en points
et non en millimètres, comme la fiche 05 y insiste : il se cale sur le corps du texte.
`SnapEdge` reçoit désormais les repères du document au lieu d'un tableau de cinq écrit
sur place ; le geste, lui, n'a pas changé. `FormatVersion` reste à 3, six propriétés
facultatives dont les défauts signifient « aucune grille ».

Recette jouée à la souris, pointeur synthétisé sur la vraie couche de saisie : quatre
blocs lâchés à des endroits différents à moins de 4 pt du bord de la deuxième colonne
s'y posent tous exactement, et `Alt` désarme bien l'aimantation. Les repères tracés sont
relus dans l'arbre visuel. Pour le PDF, le même document exporté avec la grille la plus
dense qu'on puisse régler — 77 repères — et sans grille donne deux images identiques au
pixel près.

*C2b fait le 5 septembre 2026.* Deux règles graduées en millimètres bordent la planche
et défilent avec elle — une graduation tombe donc exactement en face de la position
qu'elle annonce, sans avoir à suivre le défilement à la main. Publisher les fixe au bord
de la fenêtre ; les adosser à la page coûte moins cher et ne se trompe jamais.

Les repères libres sont portés par le document et non par la page, comme dans Publisher :
un repère sert à tenir un alignement d'une page à l'autre. On les pose en glissant depuis
une règle ou par le menu, on les déplace sur la page, on les retire en les ramenant
au-delà du bord. Ils s'aimantent eux-mêmes sur la grille, ce qui permet d'en poser un
exactement sur un bord de colonne, et leur position exacte se saisit dans le panneau —
c'est l'équivalent du dialogue « Format Ruler Guides », au même endroit que le reste des
réglages du document.

Recette jouée à la souris, pointeur synthétisé sur les vraies règles et la vraie couche
de saisie : poser, aimanter, désarmer par Alt, reprendre, retirer hors page, et un bloc
qui vient se poser exactement sur un repère choisi. Le tout est annulable. Deux exports
du même document, avec et sans repères, donnent deux images identiques au pixel près.

*À noter* : une position posée à la souris ne vaut que ce que vaut le pointeur —
Avalonia rend des positions en pixels entiers, soit 0,75 pt à 100 % de zoom. Ce qui est
exact, ce sont les positions aimantées, qui sortent de la table des repères.

*Trouvé au passage lors de C2a*, hors périmètre du poste mais corrigé : le convertisseur
millimètres arrondissait à deux décimales, et la liaison étant à double sens, la valeur
arrondie revenait dans le modèle. Une gouttière de 12 pt devenait 11,99 pt à la seule
ouverture du document, sans geste ni pas d'annulation — et il en allait de même de
toute position ou taille ne tombant pas sur la grille du centième de millimètre.
L'affichage est arrondi par le champ, qui ne touche pas à la valeur.

### C3 — Sélection multiple, puis groupement *(fiches 16, 22)*

**C3a — Sélection multiple.** La sélection est aujourd'hui unique, et c'est l'obstacle
en amont de tout le reste : le bloc sélectionné est un champ simple consommé partout —
commandes de disposition, panneau de propriétés, poignées. Le faire devenir une
collection est un remaniement réel mais circonscrit, et il ouvre à lui seul :
déplacement d'ensemble, suppression et duplication groupées, **alignement et
répartition**.

**C3b — Alignement et répartition.** Aligner à gauche/centre/droite, haut/milieu/bas ;
répartir horizontalement et verticalement. Au passage, **trancher une incohérence
existante** : le centrage actuel travaille par rapport à la *page*, alors que
l'ajustement aux marges juste à côté travaille par rapport aux *marges*. Publisher
propose explicitement le choix ; il faut soit le proposer, soit s'aligner sur les marges.

**C3c — Groupe persistant.** Un bloc « groupe » contenant des enfants en coordonnées
relatives. Le coût n'est pas dans le modèle : le rendu et l'export deviennent
**récursifs**, alors que leurs deux boucles sont aujourd'hui plates. La composition des
rotations, elle, est déjà écrite proprement.

Faire C3a et C3b d'abord : environ 80 % de l'usage pour une fraction du coût.

*Recette* : sélectionner quatre blocs, les aligner sur leur bord gauche, les grouper,
déplacer le groupe, l'exporter — la géométrie tient.

*Fait le 5 septembre 2026*, en trois temps comme prévu ci-dessus.

- **C3a** — `Selection` est une collection ; `Selected` n'en est plus que l'ancre.
  Déplacement, suppression, duplication, ordre de superposition et annulation portent
  sur l'ensemble. Le panneau de propriétés reste muet à plusieurs blocs, et les
  poignées ne s'affichent que sur un bloc seul : redimensionner un ensemble, c'est le
  grouper.
- **C3b** — six alignements et deux répartitions. L'incohérence signalée est tranchée
  par la case *Par rapport aux marges*, cochée par défaut : « centrer horizontalement »
  sur un bloc seul travaille désormais sur la zone de composition et non sur la page.
  La répartition égalise les blancs, pas les centres.
- **C3c** — `GroupFrame` porte ses blocs en coordonnées relatives ; rendu et export
  sont devenus récursifs. Redimensionner un groupe met ses blocs à l'échelle, sans
  toucher au corps du texte. Dissocier compose la rotation du groupe avec celle de
  chaque bloc, si bien qu'un groupe tourné se défait sans que rien ne bouge.

Vérifié en comparant les rendus : le PDF d'un groupe est identique au pixel près à
celui des mêmes blocs posés à plat, grouper puis dissocier rend exactement le document
de départ, et un groupe élargi de moitié donne la même image que les blocs mis à
l'échelle à la main. Les positions à l'écran ont été relevées dans l'arbre visuel :
elles concordent avec l'export.

### C4 — Champs *(fiches 08, 19)*

Un champ facultatif sur le bloc de texte : s'il est renseigné, le texte affiché est
calculé au lieu d'être littéral. Le bloc reste un bloc de texte ordinaire, avec sa
police et son alignement — **aucun type nouveau**.

Trois usages pour un seul investissement :
- **numéros de page** — le manque le plus gênant au-delà de deux pages ;
- **date** ;
- **informations professionnelles** : un jeu de coordonnées nommé, stocké hors document
  (les coordonnées suivent l'utilisateur, pas le fichier), avec plusieurs jeux
  possibles. N'a d'intérêt qu'avec les modèles de B1 — d'où l'ordre.

*Recette* : un folio posé sur un modèle se numérote seul sur huit pages, à l'écran et
dans le PDF.

*Fait le 5 septembre 2026*, en deux temps.

- **C4a** — le mécanisme, et ses trois usages immédiats : numéro de page, nombre de
  pages, date. Un `Field` facultatif sur le bloc de texte, résolu par une fonction pure
  que l'écran et l'export appellent tous deux ; le contexte vient du contrôle, un bloc
  ignorant sur quelle page il se trouve. Le texte saisi reste sous le champ et se
  retrouve intact quand on le retire.
- **C4b** — les informations professionnelles : des jeux nommés rangés dans
  `~/.config/MyPublisher/`, pas dans le document, avec le logo sur un bloc image.

La date est écrite en français quel que soit le poste et les nombres sans séparateur de
milliers : le PDF est le livrable, il ne doit pas changer de sens selon qui l'exporte.
`FormatVersion` reste à 3, une propriété facultative se lisant sans migration.

Recette jouée sur un document ordinaire de huit pages, les modèles de B1 n'existant pas
encore : c'est le folio qu'elle éprouve, et il se numérote seul de 1 à 8 à l'écran comme
dans le PDF — relu par `pdftotext` —, y compris posé dans un groupe. **La moitié
« coordonnées » du poste, elle, attend bien B1 pour servir à quelque chose** : le plan
le disait, et c'est toujours vrai. Ce qui est acquis, c'est qu'elle n'aura rien à
attendre le jour où les modèles arriveront.

### C5 — Remplissage riche et cohérence visuelle *(fiches 09, 01)*

**C5a — Fond de page.** Une propriété sur le document, un rectangle plein en tête de
chaque page à l'export. Effet immédiat sur l'allure, coût dérisoire.

**C5b — Jeu de couleurs et jeu de polices.** Une palette nommée de cinq couleurs et une
paire titre/corps, portées par le document et proposées dans les sélecteurs. C'est ce
qui manque le plus pour qu'un utilisateur reste cohérent d'un bloc à l'autre : aujourd'hui
les couleurs se saisissent en hexadécimal, une par une.

**C5c — Remplissages non unis.** Dégradé deux couleurs (l'export sait faire), image de
fond (réutilise le cadrage d'image déjà écrit), teinte (purement calculatoire depuis une
couleur de base). Le type de remplissage devient discriminé, exactement comme les blocs
le sont déjà — le mécanisme de sérialisation polymorphe est en place et éprouvé.

Textures et motifs : écartés, ressources à embarquer pour une esthétique datée.

*Recette* : un document composé avec un jeu de couleurs, un fond de page dégradé, et un
export fidèle.

*C5a et C5c faits le 6 septembre 2026. C5b reste ouvert.*

`Fill` n'est plus une couleur mais un type discriminé par `$kind` — `solid`, `gradient`,
`image`, `tint` — exactement comme les blocs le sont depuis C3c. Le fond de page arrive
avec, sous le nom que la décision P0 lui réservait : `Document.Background`, du même type,
dessiné en tête de chaque page par le même chemin que le fond d'un bloc. Une seule
propriété nouvelle sur le document, aucune sur les blocs.

**`FormatVersion` passe à 4.** Une couleur ancienne devient un remplissage uni de cette
couleur ; un fond absent le reste, sans quoi une page changerait d'aspect à la seule
ouverture. La migration descend dans les groupes **et dans les cases des tableaux** — une
case n'est pas un bloc, elle n'a pas de `$kind`, et un parcours qui n'aurait suivi que les
blocs aurait laissé des fonds de case en chaîne que le modèle refuse de relire. C'est le
premier poste où la distinction compte. La version 3 du document d'exemple rejoint les
deux précédentes dans `Scripts/fixtures/`.

#### Ce que les documents réels demandaient

**Les cinq PNG d'ATA 21 sont portés.** Ce ne sont pas des blocs image : ce sont des
remplissages de forme (`draw:fill: bitmap`), et l'import les perdait en le disant. Le
relevé les donne en base64 — jusqu'à 142 000 caractères pour une seule — alors que le
modèle référence les images par chemin.

**Où l'image est écrite, et pourquoi.** Dans les données de l'utilisateur,
`~/.local/share/MyPublisher/images/`, sous un nom tiré de son contenu. Trois raisons, et
un prix qu'il faut dire :

- les écrire à côté du `.pub` modifierait un dossier que personne n'a demandé de toucher —
  et, très concrètement, salirait `MyPublisher/Books/` à chaque passage de la recette ;
- les images d'un document ne sont pas de la configuration : elles vont dans les données,
  là où les coordonnées professionnelles vont dans `~/.config` ;
- nommer d'après le contenu rend l'import **idempotent** : réimporter le même `.pub` rend
  exactement le même document, au caractère près, ce sans quoi l'aller-retour qu'exige le
  contrôle P2 cesserait d'être vérifiable sur un document importé.

Le prix : déplacer un document ne déplace pas ses images, et une purge de ce dossier les
perd. L'export le dit — « Image de remplissage introuvable » — plutôt que de laisser un
aplat vide. C'est le compromis des images liées, et c'est le poste **B4** qui le résoudra.

**`PubImporter` reste pur**, ce qui n'allait pas de soi : il ne touche pas au disque, il
demande un chemin à un `IDepotImages` qu'on lui prête. Les tests lui en donnent un faux et
s'exécutent en quelques millisecondes, comme le poste R5 en avait pris l'habitude ; sans
dépôt, l'import dit qu'il a perdu l'image plutôt que de l'écrire de son propre chef.

**Une des cinq images tourne, et c'est 20 % du poste.** Elle porte
`librevenge:rotate: 90` : son PNG fait 1062 × 617, rapport 1,721, dans un cadre de
3,04 × 5,23 in, rapport 0,582 — exactement l'inverse. Sans le quart de tour elle arrive
couchée **et** écrasée d'un facteur trois, au milieu d'un schéma de ventilation.
`ImageFill` le porte donc, et les deux rendus l'appliquent : découpe au bloc d'abord — le
bloc, lui, ne tourne pas — puis rotation autour du centre, puis l'image posée dans le cadre
que le modèle a calculé.

#### Deux calculs qui vivent dans le noyau

Les extrémités d'un dégradé et la couleur d'une teinte. Aucun des deux rendus n'en fait
sa part : ils lisent. Ce n'est pas une élégance — c'est la leçon écrite plus haut, prise
par le bon bout. Une comparaison écran ↔ PDF ne peut pas attraper une erreur **partagée**,
mais elle ne peut pas non plus attraper une erreur qui **n'existe qu'une fois** : les deux
rendus s'accordent, et se trompent ensemble. Ce sont donc des tests du noyau qui épinglent
les valeurs, et la démonstration est faite plus bas.

L'angle d'un dégradé est en degrés, zéro de gauche à droite, croissant dans le sens des
aiguilles — l'axe Y descend, comme en PDF. Les extrémités sont posées en projetant les
coins du rectangle sur l'axe, convention de CSS : c'est la seule qui rende un dégradé à 45°
complet dans les coins plutôt que délavé. Les quatre ombrages de Publisher se ramènent à
un angle ; les deux radiaux — depuis un coin, depuis le centre — ne sont pas repris.

#### La recette, et le sabotage qu'elle a laissé passer

`Scripts/recettes/C5-Q6-fonds.sh` (nommée `C5-fonds.sh` avant Q6), en deux temps. Le premier porte sur ATA 21 lui-même : les
cinq remplissages par image arrivent, les cinq fichiers sont écrits, un seul quart de tour,
et `pdfimages -list` donne la définition posée de chaque image en largeur et en hauteur —
elles ne sont égales que si l'image n'a pas été étirée de travers. Le second mesure neuf
couleurs au pixel sur `Samples/recette-fonds.mypub`, dans le PDF puis à l'écran, aux mêmes
points, avec la comparaison d'ensemble en filet.

Éprouvée en sabotant sept fois le code, et **six seulement ont été attrapés du premier
coup**. Le septième — l'export cessant d'appliquer le quart de tour — est passé au vert :
une image non tournée garde une définition uniforme, elle est simplement rognée. Le compte
de définitions mesurait « pas déformée », pas « tournée ». La recette mesure donc
maintenant jusqu'où l'encre monte et descend dans le cadre de l'image tournée : 4,5 pt du
bord avec le quart de tour, 86 sans — la bande centrale de 219 pt sur 376. Et les deux
rendus doivent s'accorder à 3 pt près, ce qui prend en défaut une rotation oubliée d'un
seul côté.

**Le sabotage qui vaut d'être raconté** est celui des extrémités du dégradé, échangées
dans le noyau. Les deux rendus tracent alors tous les dégradés du document à l'envers — et
la comparaison écran ↔ PDF reste à **0,08 %**, exactement le chiffre du code sain. Elle
prouve l'accord, jamais la justesse. Ce sont les couleurs mesurées et cinq tests du noyau
qui l'attrapent.

Les six autres : la copie qui partage le remplissage au lieu de le cloner, l'étape de
migration retirée, l'import qui perd la rotation, l'export qui ignore le fond de page,
l'écran qui l'ignore, et la migration qui ne descend pas dans les cases d'un tableau.

#### Trois pièges rencontrés

**Le sélecteur de couleur pouvait effacer un dégradé.** `FillColor` existe depuis C1 parce
qu'un `ColorPicker` renvoie vers sa source la valeur qu'il vient d'en lire, dès qu'il
s'affiche. Le même piège revient sous une forme neuve : si l'accesseur écrivait dans un
fond d'une autre sorte, sélectionner un bloc au fond dégradé le remplacerait par un aplat.
`FillColor` ne vaut donc que pour l'aplat, et le panneau ne montre le sélecteur que dans
ce cas ; le dégradé, l'image et la teinte ont leurs propres champs. Un test le garde.

**Un remplissage se modifie sur place.** Régler l'angle d'un dégradé ne remplace pas
l'instance que porte le bloc : ni `AffectsRender` ni `NotifyPropertyChangedFor` n'en
sauraient rien, et la page ne se repeindrait pas — c'est le défaut de rafraîchissement de
R1, dans un habit neuf. Le bloc, la case et le document s'abonnent donc à leur remplissage,
et le contrôle d'affichage aussi. Le désabonnement passe par la surcharge à deux
paramètres d'`OnFillChanging` : celle à un seul ne donne pas l'ancienne valeur, et le bloc
continuerait d'entendre un remplissage qu'il ne porte plus.

**Cet abonnement a ouvert deux fuites, l'une et l'autre refermées.** La seconde est du
côté de l'écran : `FondBloc` s'abonnait au remplissage qu'on lui donnait et ne s'en
détachait que si on lui en donnait un autre. Un contrôle qui sort de l'arbre en gardant
son fond reste donc accroché à un remplissage qui, lui, survit. Ce qui a été mesuré, c'est
ce cas : un contrôle posé vingt fois à la main dans un panneau puis retiré laisse vingt
abonnés — le test qui garde le remède, relu au poste Q2 en retirant le désabonnement :
« Expected: 0, Actual: 20 ». On croyait alors que changer de page produisait la même
chose ; le poste Q2 l'a mesuré, et ce n'est pas le cas : la liste des blocs retire le
contexte de données des gabarits qu'elle démonte, le fond retombe à null et le contrôle se
désabonne — vingt changements de page, aucun abonné de trop. L'abonnement suit désormais
l'attachement à l'arbre visuel, ce qui ne dépend plus de la conduite du conteneur.
`TexteCompose` portait le même trou depuis R1, sur le bloc au lieu du remplissage ; il a
été refermé au poste Q2.

La première : `TableFrame.CellFrames`
recalcule ses blocs de texte à chaque lecture et leur *prêtait* le fond de leur case,
comme il leur prête leurs fragments. Chaque bloc jeté restait alors accroché au fond, qui
lui survit : vingt lectures, vingt-et-un abonnés — mesuré. Le panneau relisant cette liste
à chaque notification, la pile montait sans fin, et toucher au fond d'une case notifiait
des centaines de blocs que plus personne ne dessine. Le fond est donc copié là où les
fragments restent prêtés, ce qui rend le bloc et son fond ramassables ensemble. La fuite
ne se voit d'aucune façon depuis l'extérieur d'un événement : le test qui la garde compte
les abonnés par réflexion. Une première écriture, par référence faible, ne prouvait rien —
en Debug les variables locales restent vivantes jusqu'au bout de la méthode, et le test
échouait pareil sans aucun abonné.

**Le test d'exhaustivité de `Clone()` allait cesser de couvrir le fond.** Il découvre les
propriétés par réflexion et leur donne une valeur ; sa fabrique rend `null` pour tout type
qu'elle ne sait pas distinguer. `Fill` était une chaîne, donc couvert ; devenu un objet, il
serait retombé dans ce cas sans qu'aucun test ne change de couleur — exactement le trou que
les collections avaient laissé depuis R1. La fabrique sait désormais fabriquer un
remplissage, et trois tests vérifient en plus que la copie est profonde : deux blocs issus
d'une duplication doivent pouvoir diverger, et une case neuve ne partage pas le fond de sa
voisine.

#### Trouvé, non corrigé — hors périmètre

**`TexteCompose` ne se détache jamais du bloc qu'il écoute.** Il s'y abonne depuis la
correction de R1 — c'est ce qui fait que régler le corps redessine la page — mais rien ne
défait cet abonnement quand le contrôle quitte l'arbre visuel. Changer de page laisse donc
un contrôle de plus accroché à chaque bloc de texte. C'est exactement la fuite que ce
poste vient de refermer sur `FondBloc`, à l'endroit d'à côté, et le remède est le même :
suivre l'attachement. Non corrigé parce que ce contrôle appartient à R1 et qu'un poste ne
retouche pas le rendu du texte pour un fond.

**La recette de ce poste demande `pub2raw`**, comme celle de R5, sa moitié utile portant
sur ATA 21. Un arbre de travail neuf n'a pas de `.outils/` — le dossier n'est pas suivi —
et la recette échoue alors en le disant, plutôt que de se déclarer verte sur un contrôle
qu'elle n'a pas fait.

#### Écrit ailleurs, et assumé

**Le fond de la vignette d'un modèle reste un aplat.** `TemplatePreview` donne un dégradé
par sa première couleur et une image par un gris neutre. C'est l'écart que B1 a assumé —
« les vignettes sont un schéma et non un rendu » — et l'ouvrir ici reviendrait à embarquer
un troisième moteur de rendu, ce que tout l'équilibre du projet cherche à éviter.

**Un dégradé semi-transparent sort opaque du PDF.** PDFsharp écrit un ombrage axial, dont
l'alpha est celui de la page ; le format ne donne pas de transparence propre à chaque
extrémité sans un groupe de transparence, que l'export n'écrit pas. L'écran, lui, l'honore.
Consigné plutôt que tu.

**La forme ne se trace plus d'un seul trait.** Fond et contour étaient dessinés en un
appel ; ils sont désormais séparés, comme pour les autres blocs, parce qu'un remplissage
par image ne se pose pas avec un pinceau. Le rendu est le même — remplir puis contourner
est ce que fait l'opérateur combiné — et la forme hérite au passage des quatre sortes de
fond. Rien ne couvrait ce changement de chemin : le document de recette n'avait aucune
forme munie d'un contour, et le contrôle P2 compare un compte de pages, pas des pixels.
L'ellipse y a donc gagné un filet de 3 pt et l'aplat un filet de 2 pt avec un angle
arrondi de 10 — trois points de mesure de plus, et une vérification que le fond respecte
l'arrondi, le coin devant laisser passer le fond de page. Retirer le contour des formes à
l'export met les trois au rouge.

#### Choisi sans certitude

**L'échelle de la teinte.** 100 est la couleur de base, 0 le blanc, 200 le noir : une seule
échelle continue, monotone, sans valeur ambiguë. Publisher étiquette ses dix-huit pastilles
en couverture d'encre et tient deux échelles séparées, « 60 % Tint » et « 60 % Shade ». Le
côté clair correspond donc exactement — 60 ici est bien 60 % de teinte — et le côté sombre
est son miroir plutôt que l'étiquette de Publisher. Aucun des deux documents réels ne porte
de teinte : le choix est écrit pour être discuté, non pour être mesuré.

**Une mosaïque est rendue sans répétition.** `style:repeat` vaut `stretch` sur les cinq
images d'ATA 21, ce qui est le cadrage « Remplir ». Toute autre valeur est cadrée sans
déformation, avec un avertissement : le modèle ne sait pas mettre une image en mosaïque, et
l'inventer ici sortirait du poste.

**Le fond de page est porté par le document et non par la page**, comme les repères libres
et pour la même raison : c'est ainsi que Publisher le règle, et un fond qui changerait
d'une page à l'autre est une page maître — poste non planifié.

#### C5b — fait le 12 octobre 2026

**Ce qui a été fait.** Le document porte `ColorScheme` — un nom, `Main` et quatre accents —
et `FontScheme` — un nom, `Heading` et `Body`. Comme dans Publisher, un bloc qui prend une
couleur ou une police du jeu y fait **référence** : la chaîne `scheme:accent1` ou
`scheme:heading` est écrite là où le modèle attendait `#AARRGGBB` ou un nom de famille.
Écrite dans la chaîne même, la référence vaut d'un coup partout où une couleur se pose —
aplat, extrémités d'un dégradé, base d'une teinte (résolue *avant* le mélange), contour,
texte, fragment de style, case, grille, fond de page — sans propriété jumelle à tenir en
accord. Elle peut porter sa propre opacité, `scheme:accent1/80` : l'opacité du fond se règle
sans détacher le bloc du jeu. Toute autre valeur est libre, et un changement de jeu ne la
touche pas.

La résolution vit dans le noyau, `Charte` (`Models/Jeux.cs`) : `Couleur`, `Police`,
`StyleCommun`, `Fragments`, et `FillBrush.UniformeSelon`. L'export la prend sur le document ;
l'écran la reçoit par une propriété héritée posée sur la page (`JeuAffiche.Charte`), si
bien que fonds, textes composés et marqueurs de débordement l'ont tous, groupes et cases
compris, et se redessinent quand on change de jeu. Les contours et le champ de saisie y
passent par deux convertisseurs. Le style commun et les fragments arrivent **résolus à la
composition** : une police du jeu se mesure dans la police du jeu, et les coupures en
dépendent. Le contrôle des polices de l'export (B5) contrôle la police que le jeu désigne,
non « scheme:body ».

Au panneau, chacun des dix sélecteurs de couleur est devenu `SelecteurDeCouleur` : cinq
pastilles qui écrivent une référence, puis le sélecteur libre qui écrit de l'hexadécimal.
La liste des polices commence par le titre et le corps du jeu, libellés de la police qu'ils
désignent. Le panneau *DOCUMENT* choisit le jeu de couleurs et le jeu de polices parmi six
et cinq jeux livrés ; le choix s'annule. Les blocs neufs prennent leurs couleurs et leurs
polices dans le jeu, et le jeu par défaut, « Ardoise »/« Sobre », porte exactement les
valeurs que les gabarits écrivaient en dur : un document neuf ne change pas d'allure.

**`FormatVersion` passe à 5.** La migration ne rapproche aucune couleur d'un accent
qu'elle égalerait ; ce qu'elle fait, c'est garder libre une valeur que la version 4 tenait
pour libre et que la version 5 lirait comme une référence — une chaîne qui commence par
`scheme:` dans une couleur ou une police — en la préfixant d'un `!`. La version 4 du
document d'exemple rejoint les trois précédentes dans `Scripts/fixtures/`. L'import d'un
`.pub` garde ses polices libres par la même fonction ; ses couleurs le sont par
construction.

**Les modèles de B1 ont adopté un jeu.** Les quatre partageaient cinq couleurs : elles
forment le jeu « Marine », et leurs blocs y font référence — le blanc reste libre ; les
blocs en gras prennent la police du titre, les autres celle du corps.

**Ce qui a été mesuré.** Les seize documents livrés et figés (Samples, fixtures v1 à v3,
modèles), exportés par le binaire de `main` puis par celui-ci, à 144 points par pouce :
0 pixel différent, et les mêmes avertissements ; les treize Samples et modèles rendus à
l'écran par les deux bancs : 0 pixel différent, et de même entre les modèles de `main` et
les modèles adoptés. `Scripts/recettes/C5b-jeux.sh` sur
`Samples/recette-jeux.mypub` : quinze points au PDF et à l'écran, avant puis après le
passage d'Ardoise/Sobre à Océan/Édition — dix références aux couleurs d'Océan, calculées
depuis les valeurs du jeu, et cinq points libres posés chacun à côté d'une référence et
portant la couleur qu'Ardoise lui donnait, inchangés. Au PDF, Liberation Serif et
Liberation Sans n'apparaissent qu'après ; l'encre du titre et du corps change
(6 532 et 2 148 pixels au PDF, 6 275 et 2 170 à l'écran), celle de la ligne en police
libre de 3 pixels, du lissage. Écran contre PDF : 1,63 % avant, 1,57 % après. Vingt-deux
tests du noyau et treize d'interface, trait `poste=C5b`. Onze sabotages, chacun éprouvé à la
main : l'export qui ne résout plus le départ d'un dégradé, l'écran qui peint un fond sans
le résoudre, un fragment qui garde sa référence, une teinte qui mélange sa référence, une
police non résolue, la migration qui laisse une valeur v4 devenir référence, le sélecteur
qui renvoie ce qu'il affiche, l'écran qui ne se repeint plus — fond, puis texte —, et,
après la relecture, la migration qui ne surveille plus l'arrivée d'un dégradé et la
pastille qui perd l'opacité propre du fond. Le test de migration porte chacun des six noms
de propriété que la migration surveille — `Color`, `From`, `To`, `Stroke`, `Foreground`,
`FontFamily` — et en retirer un, quel qu'il soit, le fait tomber : les six ont été
essayés. Le test de la pastille part d'un fond `#80123456` et attend `scheme:accent3/80`.

**Le piège du sélecteur, pour la troisième fois.** Il est réel ici aussi : sans la garde
qui ignore l'écho de la couleur que le contrôle vient de poser, quatre tests d'interface
tombent, et la recette tombe à sa première mesure — ouvrir le document dans la fenêtre
avait réécrit la base de la teinte du fond de page en `#FF9DC3E6`, l'accent qu'elle
désignait. Le premier essai de la recette l'a montré par accident, sur un binaire resté
saboté. Le sélecteur libre est lié au contrôle par une liaison et non par un abonnement :
les deux naissent et meurent ensemble, et la règle de Q2 n'a rien à y redire.

Le test qui dit que changer de jeu repeint la page comparait d'abord la fenêtre entière, et
il passait encore sans le remède : l'image de la fenêtre change d'autre part. Il lit
désormais le pixel affiché au point même, et tombe sans l'invalidation du fond comme sans
celle du texte.

**Choisi sans certitude.** La référence dans la chaîne plutôt qu'un type de couleur : rien
dans le modèle n'empêche d'écrire `scheme:accent9` ou `scheme:accent1/zz`, que la
résolution rend illisibles et qui se traitent exactement comme une couleur libre
illisible. Mesuré à l'écran et au PDF, identiques, côte à côte avec « pas une couleur » :
un aplat, une teinte ou un contour ne se dessinent pas ; un dégradé dont une extrémité est
illisible se peint uni de l'autre, et ne se dessine pas si les deux le sont ; un texte
retombe au noir. Une police en `scheme:` illisible, `scheme:titre`, est un nom de police
comme un autre, que la machine n'a pas : l'export la substitue et le dit (B5). Cinq
emplacements, la principale et
quatre accents, comme le demandait le plan ; Publisher 2007 en a huit, liens compris.
L'échappement par `!` des valeurs v4 en forme de référence : aucun document connu n'en
porte, et le préfixe ne change ni une couleur illisible ni une police introuvable. Une
référence sans opacité propre est réputée opaque par le champ d'opacité : vrai des jeux
livrés, pas d'un jeu transparent qu'on écrirait à la main. Le jeu de polices ne porte pas
le gras du titre (« Georgia Bold » chez Publisher) : le gras reste au bloc. Dans les
modèles, « en gras » vaut « titre ». Choisir au sélecteur libre exactement la couleur qu'il
affiche pour une référence n'en fait pas une couleur libre : c'est l'écho qu'on ignore.
On ne compose pas de jeu au panneau : on en choisit un ; un jeu écrit dans le fichier est
gardé et s'applique, mais la liste reste alors sans sélection.

**Trouvé, et laissé tel quel.** Ouvrir un document dans la fenêtre réécrit ses dimensions :
les deux champs en millimètres du panneau passent la valeur par un `decimal` et la
renvoient au modèle, si bien que 595,28 devient 595,2800000000013 — relevé par une trace
de pile, sur cette branche, dans du code que ce poste ne touche pas (A5). La recette
compare donc les dimensions au millionième. La vignette d'un modèle ignore le fond de
page — aucun modèle livré n'en porte. L'import ne lit aucun jeu de couleurs d'un `.pub` :
ni cherché ni demandé. La relecture a trouvé que la liste des polices effaçait la police
d'un bloc absente de la machine : c'est le poste Q20, fait sur la même branche.

**Ce que la fusion avec `main` a demandé, le 12 octobre 2026.** Trois postes de la salve
touchaient les mêmes fonctions. À l'export, `DessinerPortes` de Q5 — la découpe au
porteur — reçoit la charte et la passe aux blocs des groupes et aux cases des tableaux.
Les fragments et le style commun de Q8 passent par `Fragments(text, content, charte)` et
`Commun(text, charte)` : un champ résolu n'a toujours aucun fragment, et la passe des
polices et le dessin découpent le même texte, désormais résolu — la passe n'a donc plus
à traduire la police elle-même. Les deux sabotages de Q8 au juge visaient l'ancien texte ;
ils sont réécrits sur le nouveau, et rejoués à la main : la recette de Q8 tombe sur
chacun. La vignette de Q9 compose ses filets avec le style et les fragments résolus, et
les peint de la couleur du jeu : sans cela, les modèles, qui ont adopté « Marine », y
perdaient la couleur et les polices de leurs textes. Les tests de Q9 lisaient la couleur
d'un texte de modèle par `Color.Parse` et composaient sans charte : ils levaient sur
`scheme:main`, et lisent maintenant la charte du modèle. Deux sabotages de plus au juge —
la vignette sans la couleur du jeu, puis sans sa police —, chacun attrapé par
`VignetteTests`. À l'écran, le filet centré de Q4 garde sa marge et prend la couleur du
jeu.

### C6 — Marges intérieures par côté *(fiche 12)*

Remplacer la marge intérieure unique et symétrique par quatre valeurs. Mécanique, sans
piège, hormis la migration de format — à grouper avec C1 pour ne payer la migration
qu'une fois.

*Recette* : un texte décalé de 12 pt à gauche seulement, à l'écran comme au PDF.

*Fait le 5 septembre 2026*, après C1 et non avec lui : la mécanique de migration
posée par C1 rendait le groupage sans objet, le poste n'a coûté qu'un numéro de
version et son étape. `PaddingLeft`, `PaddingTop`, `PaddingRight`, `PaddingBottom`
sur le bloc de texte, et `Padding` d'un seul tenant pour la vue et l'export — la
liaison XAML n'a pas changé, seul son convertisseur. `FormatVersion` passe à 3 ; un
fichier ancien reçoit quatre marges égales à son unique et s'affiche comme avant. Le
contrôle relit désormais une version passée par numéro de format : la version 2 de
l'exemple rejoint la version 1 dans `Scripts/fixtures/`.

Vérifié en mesurant l'encre : trois blocs du même texte, sans marge, avec 12 pt à
gauche seulement, avec 20 pt en haut seulement, rendus des deux côtés à deux pixels
par point — les décalages mesurés sont ceux demandés, à gauche sans bouger en haut, et
l'écran et l'export concordent.

### C7 — Bibliothèque de blocs *(fiche 20)*

Dépend de C3a. On sélectionne un ou plusieurs blocs, on les enregistre sous un nom, on
les repose dans n'importe quel document. Version utile et bien moins chère de la
*Design Gallery* : elle ne demande aucun contenu à dessiner, l'utilisateur l'apporte.
Elle réutilise la sérialisation existante — un élément est un fragment de `.mypub` — et
la copie de bloc est déjà écrite.

Elle rend au passage la zone de travail hors page (fiche 07) presque superflue : même
besoin — garder un logo sous la main — mais persistant et inter-documents. À traiter
dans le même panneau qu'une éventuelle bibliothèque d'images locale (fiche 13).

*Recette* : enregistrer un bloc-titre composé, ouvrir un autre document, le reposer.

*Précisé le 25 octobre 2026 par la phase N* : `Ctrl+Maj+E`, la touche de la bibliothèque dans
Publisher (p. 22, 39), lui est réservée par la table française (N10), et le menu contextuel (A12)
reçoit l'entrée « Ajouter à la bibliothèque… ».

### C8 — Miroir horizontal et vertical *(fiche 22)*

La notion n'existe pas dans le modèle. Elle traverse le cœur géométrique — survol,
poignées, redimensionnement, composition avec la rotation, groupes — et les deux rendus :
c'est pourquoi elle relève de la phase C et non de la phase A. Une question reste à
trancher avant d'écrire une ligne, et les fiches ne la tranchent pas : un bloc de texte
en miroir montre-t-il son texte à l'envers, ou seulement son cadre ?

Dépend de C3c, A1 et A9.

*Recette* :
- test pur : sur des blocs tirés au hasard, rotation et miroir compris, passer du
  repère de la page à celui du bloc puis revenir rend le même point ;
- un groupe en miroir porte son bloc de gauche à droite ;
- une image en miroir horizontal : la première colonne de son rendu est la dernière de
  la source, à l'écran comme au PDF ;
- `comparer-rendus.sh` passe sous la tolérance.

Effort : moyen à lourd.

*Précisé le 25 octobre 2026 par la phase N* : les deux miroirs entrent au sous-menu *Faire pivoter
ou retourner ▸* de Disposition (N18) et du menu contextuel (A12).

### C9 — Le bloc tableau

Poste que ce plan ne portait pas : il vient de
[`Documents-reels.md`](Documents-reels.md), où les fiches ATR comptent dix-neuf objets
tableau. Le relevé a été rectifié le 6 septembre 2026, et la rectification change tout :
**seize de ces dix-neuf tableaux ont trois colonnes et une seule ligne**, trois n'ont
qu'une case, aucune cellule n'est fusionnée, aucun tableau n'est imbriqué. Ce sont des
bandes à trois cases, et non des tableaux de données. Sur les 362 insertions de texte du
document, seize sont dans un tableau : l'essentiel du contenu vit dans les blocs de
texte ordinaires.

Ce que le poste doit donc livrer est un porte-cases : des colonnes, des lignes, et une
cellule qui porte un contenu de texte, son fond et son contour. Rien qui calcule, rien
qui se scinde entre deux pages, rien qui fusionne.

*Recette* : un document portant une bande de trois cases se relit et se réécrit au
caractère près, s'exporte, et l'écran concorde avec le PDF.

*Fait le 6 septembre 2026.* `TableFrame`, discriminant `table`. `FormatVersion` **reste
à 3** : un type de bloc neuf n'oblige à migrer aucun fichier ancien, exactement comme
C3c avait ajouté `group` sans changer de numéro.

**Un tableau se rend comme un groupe.** C'est le choix qui commande tout le reste. Ses
cases ne sont pas dessinées : elles sont *calculées* en blocs de texte, en coordonnées
relatives, à partir des colonnes, des lignes et des cellules. L'écran les affiche par le
gabarit du bloc de texte, l'export les trace par sa fonction de bloc de texte. Aucun des
deux rendus n'a une ligne de tableau qui lui soit propre, et ils sont donc d'accord par
construction — comme ils le sont déjà pour un groupe. Une case hérite au passage de tout
ce que les postes précédents ont apporté au texte : la composition partagée de R1, ses
fragments de style, l'interligne de R4, le fond et le contour de C1.

**Les colonnes et les lignes font la taille du bloc**, et non l'inverse. C'était la
question à trancher, parce que le bloc porte déjà une largeur et une hauteur : trois
mécanismes pouvaient les accorder, et deux suffisent. Redimensionner le tableau mesure
ses colonnes à la même aune — ce qu'un groupe fait de ses blocs, donc le tirer à la
poignée et le mettre à l'échelle dans un groupe marchent sans une ligne de plus. Élargir
une colonne élargit le bloc. Les deux sens passent par un même verrou, sans quoi ils se
relanceraient l'un l'autre. Le prix de ce choix est qu'aucun chiffre du panneau ne peut
mentir : la largeur qu'on lit sur une colonne est celle qu'on mesure sur la page.

Un détail qui n'en est pas un : l'accord des deux tailles se fait à **un milliardième de
point près**, et non à l'identique. Mesurer trois colonnes au rapport d'un
redimensionnement laisse leur somme à un ulp de la largeur demandée ; sans ce seuil,
ouvrir puis réenregistrer un document déplacerait ce dernier chiffre, et l'aller-retour
cesserait d'être identique au caractère près. C'est la même dérive que celle consignée
au poste P3, prise cette fois avant qu'elle ne nuise.

**Ce que le panneau règle** : lignes et colonnes, la largeur de chacune, la police et le
corps du tableau, la marge intérieure des cases, la grille — et, case par case, son
texte, son alignement et son fond. Une case ne se choisit pas sur la page : il n'y a pas
de sélection à l'intérieur d'un bloc, et une bande en compte trois. La grille, elle, se
règle pour toutes les cases à la fois alors que chacune porte son contour dans le
fichier : c'est ainsi que Publisher les décrit et que l'import de R5 les lira, mais
personne ne veut cocher trois cases pour encadrer une bande.

*Recette jouée*, `Scripts/recettes/C9-tableau.sh` : le document de six bandes se réécrit
au caractère près, son texte se retrouve dans le PDF, et surtout **les cases y sont
mesurées** par `pdftotext -bbox` — origine de colonne, bord intérieur, fer à droite sur
la somme des largeurs, hauteur de ligne — au centième de point.

La mesure a remplacé une comparaison d'images qui ne prouvait pas grand-chose ici : sur
une page de tableaux, aux trois quarts blanche, le lissage des glyphes et des filets
coûte à lui seul 2,40 %, et décaler tous les tableaux de 2 pt à l'export ne fait monter
l'écart qu'à 3,04 %. Le même décalage déplace chaque mesure de 2,00 pt, contre une
tolérance de 0,05 pt. La comparaison d'images reste, en filet, pour ce qu'une mesure de
position ne voit pas : une couleur perdue, un fond oublié, un filet disparu.

*Trouvé au passage*, et non corrigé — hors périmètre. Deux choses.

`TexteCompose` ne se redessine que lorsque la **référence** de son bloc change
(`AffectsRender` porte sur `Bloc` et `Contenu`). Modifier le corps ou la police d'un
bloc de texte dans le panneau ne repeint donc pas l'écran tant qu'aucune autre liaison
ne le force. Les tableaux n'en souffrent pas — leurs cases sont recalculées et changent
donc de référence à chaque retouche — mais les blocs de texte, si.

Un bloc plus large que le groupe qui le porte est **découpé à l'écran et pas au PDF**.
Vérifié sur un groupe de 120 pt contenant un rectangle et un texte de 400 pt, sans
aucun tableau : l'écran s'arrête au bord du groupe, le PDF va jusqu'au bout. Le cas ne
s'obtient pas au geste — grouper ajuste le groupe à ses blocs — mais un fichier écrit à
la main ou un import le produira. Il ne touche pas les tableaux : leur largeur est
toujours celle de leurs colonnes, y compris après relecture d'un fichier qui dirait
autre chose.

*Écart assumé* : le contenu d'une case est découpé au bloc à l'écran (`ClipToBounds`) et
ne l'est pas au PDF. Un texte trop long pour sa case sortira du cadre dans le PDF et pas
sur la page. C'est le poste A2 — signaler le débordement — et il vaut déjà pour les
blocs de texte ordinaires.

---

## Phase R — Ce que les documents réels ont ajouté

Cinq postes qu'aucune fiche n'annonçait, tous établis sur les deux `.pub` ATR déposés le
6 septembre 2026 — voir [`Documents-reels.md`](Documents-reels.md), qui les décrit et dit
pourquoi ils passent devant le reste. R1 et R4 sont clos ; ce qu'ils ont trouvé est dans
leurs commits et dans le relevé.

### R2 — Retraits et espacement de paragraphe

*Fait le 6 septembre 2026.* Le bloc de texte gagne cinq réglages en points — retrait
gauche, retrait droit, retrait de première ligne, espace avant, espace après — et une
collection d'écarts, `Paragraphs`, qui les redit autrement pour une suite de paragraphes.
Trente-six paragraphes d'ATA 21 portent un retrait de première ligne **négatif** : c'est
ainsi que Publisher compose une liste à puces, la puce se posant dans le creux. Sans lui,
elles retombent au fer à gauche et la structure du document disparaît.

**Le repérage était le choix à faire, et il diverge de R1.** Un fragment de style repère
des positions de caractères ; un écart de paragraphe repère des **indices de paragraphe**.
Trois raisons : un retrait appartient au paragraphe entier et n'en a pas de moitié, si
bien qu'un repérage par caractère obligerait à inventer une règle pour le paragraphe à
demi couvert — et une règle inventée s'applique en silence ; un indice survit à la frappe
ordinaire, là où taper un mot décale toutes les positions de caractères qui suivent ; et
c'est ce que la source donne, `pub2raw` énumérant des paragraphes. Le prix est réel et il
est dit dans le code : le fichier porte désormais deux systèmes de repérage, et l'import
de R5 devra tenir les deux dans la même passe.

**La ligne composée porte désormais son cadre** — son retrait et la largeur qui lui
reste. Ce n'est pas une commodité de rendu. La cible d'une ligne justifiée n'est plus la
largeur du bloc mais celle-là, et `Compositeur.Decalages` a **perdu son paramètre de
largeur** : il lit celle de la ligne. Un rendu ne peut donc plus se tromper de cible tout
seul. C'est le piège que le poste annonçait, et il est de ceux que la comparaison écran ↔
PDF ne voit pas : les deux rendus appelant le même calcul, ils se seraient trompés
ensemble.

`FormatVersion` reste à 3. Cinq réglages à zéro et une collection vide composent
exactement comme avant, et c'est ce qui dispense de migration — le même mécanisme qu'a
employé R1.

*Recette jouée*, et elle est un script : `Scripts/recettes/R2-retraits.sh`. Les retraits
sont **mesurés** dans le PDF par `pdftotext -bbox-layout`, qui rend la boîte de chaque
ligne à l'origine du crayon : 18,000 pt de retrait négatif, 24,000 pt de retrait de
paragraphe à gauche, 24,015 pt à droite — le bord droit d'une ligne justifiée rentre
d'autant, ce qui est le point délicat — et 12,000 pt d'espace après, à un quart de point
près. L'écran est ensuite mesuré de la même façon, encre contre encre, bande par bande :
les quatre grandeurs y sont identiques à celles du PDF, à moins d'un pixel.

**Le pourcentage de pixels différents ne suffisait pas, et c'est un enseignement.** Sur
ce document, l'accord écran ↔ PDF donne 2,94 % — le seul lissage des glyphes d'une page
de texte — tandis qu'une divergence de retrait simulée, l'écran composant sans retraits
et le PDF avec, n'en donne que 3,93 %. Le filet global ne sépare pas les deux. Il reste
dans la recette pour ce qu'il attrape — un bloc qui cesserait d'être dessiné — mais ce
n'est pas lui qui éprouve le poste.

La recette a été éprouvée en la faisant échouer, quatre fois : l'écran ignorant le
retrait, le retrait de première ligne appliqué à toutes les lignes, l'export oubliant
l'espace après, et la justification visant la largeur du bloc. Quatre sabotages, quatre
attrapés. Les tests d'interface l'ont été de même, sur les deux sabotages qui ne touchent
que l'écran.

*Trouvé au passage, et comblé.* Le test d'exhaustivité de `Clone()` par réflexion **ne
couvre pas les collections** : la fabrique de valeurs rend `null` pour elles, si bien
qu'elles restent vides des deux côtés et que l'égalité des deux JSON passe sans rien
prouver. `Spans` était dans ce cas depuis R1. Un test propre aux deux collections du bloc
de texte comble le trou, et vérifie au passage que les éléments sont copiés et non
partagés.

#### Trois choses trouvées et laissées telles quelles

**Le panneau TEXTE ne rafraîchit plus la page — et c'est R1, pas R2.** Mesuré : on change
`IndentLeft` sur un bloc, la fenêtre rend une image identique ; on change `FontSize`,
elle rend encore la même ; on change `Text`, elle change. La raison est unique et elle
vaut pour les dix propriétés typographiques du panneau, pas seulement pour les cinq de ce
poste : `TexteCompose` déclare `AffectsRender` sur `Bloc` et `Contenu`, or `Bloc` est
lié au bloc lui-même — l'instance ne change pas quand une de ses propriétés change, donc
rien n'invalide le visuel. Seul `Contenu`, calculé par liaison à partir de `Text` et de
`Field`, en produit une nouvelle valeur. Avant R1 le texte était un `TextBlock` dont
chaque propriété était liée séparément, et le rafraîchissement suivait. Hors périmètre de
R2, et à corriger en un seul endroit pour toutes : c'est le bloc qu'il faut écouter.

**L'écran découpe le texte au bloc, l'export ne le découpe pas.** `PageCanvas.axaml` pose
`ClipToBounds` sur le contenu du bloc de texte ; `PdfExporter` dessine sans découpe.
Tant que le texte reste dans son cadre, les deux concordent. Une première ligne qui pend
au-delà du bord gauche, elle, est rognée à l'écran et entière au PDF — et c'est
exactement ce que la vraie paire d'ATA 21 produit : `margin-left 0.1930in` avec
`text-indent −0.2183in` place la première ligne à **−1,8 pt**, dehors. R5 importera
trente-six paragraphes de cette sorte.

> *Rectifié au poste A2, le 6 septembre 2026, en le mesurant.* La dernière phrase est
> fausse, et c'est la marge intérieure qui la rend fausse : `fo:padding-left` vaut
> `0.0400in` sur les neuf blocs d'ATA 21, soit 2,88 pt, si bien que la première ligne
> tombe à 1,06 pt **dans** le bloc. Elle sort de sa zone de texte, que rien ne découpe,
> et non du bloc, qui seul l'est. Le constat général était juste, l'exemple non — et
> c'est l'exemple qui aurait servi de recette.

**La recette éprouve donc le mécanisme, pas encore les valeurs réelles.** Elle mesure
18 pt de retrait négatif, 24 pt de retrait de paragraphe et 12 pt d'espace après, tous
choisis pour que le texte reste dans son cadre et que la découpe ci-dessus ne se voie
pas. Les valeurs relevées sur les documents — −15,7 pt et 13,9 pt — se composeront
correctement, mais leur concordance écran ↔ PDF ne sera acquise qu'une fois la découpe
tranchée : soit l'écran cesse de rogner, soit l'export rogne à son tour.

*Laissé de côté, et à noter pour R5* : `fo:text-align` varie **par paragraphe** dans
ATA 21 — 31 justifiés contre 21 au fer à gauche — alors que l'alignement reste une
propriété du bloc. `ParagraphSpan` le porterait pour presque rien, mais le poste énumère
quatre propriétés et s'y tient.

*Choisi sans certitude* : l'espace avant s'applique aussi au premier paragraphe du bloc,
qui descend donc sous sa marge intérieure haute. C'est ce que fait Publisher dans une
zone de texte ; le relevé ne tranche pas, n'ayant aucun `fo:margin-top`.

### R3 — Colonnes à l'intérieur d'un bloc

*Fait le 6 septembre 2026.* Le bloc de texte gagne un nombre de colonnes — une par
défaut — et une gouttière. Le texte remplit la première colonne jusqu'au bas du bloc,
puis passe à la suivante, quitte à couper un paragraphe en deux : c'est ce que veut dire
« le texte coule ». La largeur qui sert à la découpe n'est plus celle du bloc mais celle
d'une colonne, `(largeur − (n − 1) × gouttière) / n`.

**Ce n'est pas C2a**, et le panneau le dit maintenant en toutes lettres : C2a pose des
colonnes sur la **page**, comme repères d'aimantation ; celles-ci sont **dans un bloc**,
et rien n'est partagé entre les deux — ni propriété, ni calcul.

**La composition range elle-même les lignes en colonnes**, en une seconde passe qui ne
touche pas au renvoi à la ligne : toutes les colonnes ayant la même largeur, la découpe
ne dépend pas de celle où la ligne atterrira. La ligne composée porte désormais sa
colonne, l'ordonnée de son sommet **dans cette colonne** et son avance ; `Composer` a
reçu pour cela la hauteur du bloc et l'interligne, et `Hauteur` a **perdu** son
paramètre d'interligne.

C'est le geste de R2 poussé d'un cran, et pour la même raison. Jusqu'ici chaque rendu
cumulait sa propre hauteur de ligne en ligne ; c'était sans conséquence tant qu'une
divergence de métrique ne produisait qu'une dérive continue de quelques centièmes de
point. **Avec les colonnes, cette dérive devient une falaise** : une ligne posée près
d'un bord de colonne tomberait dans la colonne 1 à l'écran et dans la colonne 2 au PDF,
pour un écart de métrique bien inférieur au point. Un seul endroit décide donc, et les
deux rendus lisent. Aucun des deux n'additionne plus rien.

**Ce qui dépasse la dernière colonne continue vers le bas**, hors du bloc, exactement
comme un texte trop long le fait aujourd'hui dans un bloc d'une seule colonne. Rien
n'est tronqué, et c'est délibéré : couper ici livrerait un PDF amputé sans le dire, ce
que le poste **A2** est justement chargé d'empêcher — et A2 n'est pas écrit. La règle
« une seule colonne » devient ainsi un cas particulier de la règle générale et non un
chemin de code à part, ce qui est la vraie garantie qu'aucun document existant ne bouge.
A2 trouvera d'ailleurs son compte tout fait : la dernière ligne porte son `Haut` et son
`Avance` dans sa colonne, et `Compositeur.Hauteur` rend la hauteur de la colonne la plus
remplie — non la somme des colonnes.

`FormatVersion` reste à 3. Une colonne et une gouttière inerte composent exactement
comme avant, et c'est ce qui dispense de migration — le même mécanisme qu'ont employé
R1 et R2.

**La non-régression a été mesurée avant de toucher au code**, et c'est la seule façon de
la mesurer : les sept documents du dépôt ont été exportés et rendus à l'écran *avant* le
poste, puis à nouveau *après*, une fois les fichiers régénérés par le sérialiseur.
**Vingt images, écran et PDF, toutes pages : zéro pixel de différence.** Comparer les
JSON n'aurait rien prouvé — ils changent forcément, le sérialiseur écrivant les deux
propriétés neuves ; ce sont les images qu'il fallait confronter.

Ce que ce zéro dit, et ce qu'il ne dit pas : les vingt images ne portent que des blocs
d'une seule colonne — c'est précisément ce qui en fait le bon témoin de non-régression,
et c'est aussi pourquoi il ne prouve rien sur les colonnes elles-mêmes. Celles-ci
reposent sur la recette et sur `ColonnesTests`, et sur rien d'autre.

*Décidé, et non subi* : **réduire un groupe rétrécit les colonnes d'un bloc de texte sans
toucher à sa gouttière.** Le groupe met à l'échelle la géométrie, pas la typographie —
c'est déjà ce que C3c fait du corps du texte. Ce sont donc les colonnes qui se resserrent,
pas ce qui les sépare, et un bloc écrasé au-dessous de ses gouttières garde des colonnes
d'un point plutôt que des colonnes négatives. Deux tests dans `GroupeTests`.

*Recette jouée*, et elle est un script : `Scripts/recettes/R3-colonnes.sh`, sur
`Samples/recette-colonnes.mypub` — trois colonnes portant douze paragraphes numérotés,
deux colonnes justifiées, et un témoin à une seule colonne muni d'une gouttière de 40 pt
qui ne doit rien faire.

Les colonnes sont **mesurées** dans le PDF par `pdftotext -bbox`, qui rend la boîte de
chaque **mot**. Le mot et non la ligne, contrairement à la recette de R2 : `-bbox-layout`
regroupe en lignes selon sa propre analyse de mise en page, or ce qu'on veut vérifier est
précisément qu'il y a plusieurs colonnes à la même hauteur. Mesuré : 169,000 pt d'un
bord de colonne au suivant, trois sommets identiques au centième de point, le flot dans
l'ordre — `01 02 03 04` puis `05 06 07 08` puis `09 10 11 12` —, et 241,488 pt de bord
droit justifié pour une colonne large de 241,5. Ce dernier nombre est le piège du poste,
le même qu'en R2 : une ligne justifiée dont la cible serait restée la largeur du bloc
s'étirerait par-dessus la gouttière et sous sa voisine. L'écran est ensuite mesuré de la
même façon, encre contre encre, bande par bande : **les six bandes concordent au pixel**,
sauf un pixel de sommet sur le bloc justifié.

La recette a été éprouvée en la faisant échouer, huit fois : l'écran ignorant les
colonnes, l'export les ignorant, la ligne oubliant le pas de sa colonne, la
justification visant la largeur du bloc, l'écran ne repartant pas du haut de la colonne,
le débordement partant dans une quatrième colonne inexistante, et `Clone()` oubliant le
nombre de colonnes. **Huit sabotages, huit attrapés** — quatre par la recette, deux par
les tests d'interface, deux par ceux du noyau.

*Trouvé au passage, et il faut le dire.* Le sabotage se restaurait par `cp -a`, qui rend
au fichier sa date d'origine : MSBuild le croit inchangé, ne recompile pas, et le
**binaire saboté survit à la restauration**. Un premier passage a ainsi rapporté un
sabotage « non attrapé » qui l'était, et un autre « attrapé » par un test qui ne le
regardait pas. Toute vérification qui écrit du code puis le remet doit toucher le
fichier ; `Scripts/eprouver-le-juge.sh`, qui restaure par `git checkout`, ne court pas ce
risque — `git` écrit une date neuve.

#### Ce que le relevé dit des colonnes, et ce que les fichiers disent

Le relevé (`Documents-reels.md`) présente R3 ainsi : « `fo:column-gap` est posé sur les
neuf blocs de texte d'ATA 21 et sur seize des Fiches ». C'est exact. Deux points
méritent cependant d'être ajoutés, l'un et l'autre vérifiés en relançant `pub2raw` :

**`style:column-width` n'appartient pas aux zones de texte.** Les dix-sept occurrences
des Fiches sont toutes dans un `startTableObject`, sous `librevenge:table-columns` :

```
startTableObject(librevenge:table-columns: ((style:column-width: 0.4860in),
  (style:column-width: 2.2286in), (style:column-width: 0.5220in)), svg:height: ...)
```

Ce sont les colonnes des **tableaux** — les seize bandes à trois cases du poste C9 — et
non celles d'un bloc de texte. Le rapprochement du relevé était une lecture rapide.

**`fo:column-count` n'apparaît nulle part.** La chaîne existe dans libmspub 0.1.4 — elle
est dans la bibliothèque, à côté de `fo:column-gap` — mais elle n'est émise par aucun des
**vingt-quatre** `.pub` présents sur la machine, les deux documents de référence
compris, alors que `fo:column-gap` l'est sur chaque zone de texte, toujours à la même
valeur : 0,0787 in pour ATA 21, soit 2 mm exactement, et 0,0800 in pour les Fiches. Ce
sont les deux écritures du **défaut** de Publisher, selon que l'unité du document est
métrique ou impériale.

La lecture la plus probable est donc que **ces documents sont tous à une seule colonne**,
et que la gouttière n'y est qu'un réglage par défaut qui ne sert à rien. Une seconde
lecture ne peut pas être écartée sans un `.pub` à deux colonnes sous la main : que
libmspub 0.1.4 ne restitue tout simplement pas le compte. Les deux mènent au même endroit
pour R5 — l'import devra lire `fo:column-count` s'il le rencontre, et ne pas conclure des
colonnes de la seule présence d'une gouttière.

Ce qui change, c'est ce que le poste **prouve**. Il ne reproduit pas une mise en page que
ces documents demanderaient : il donne à Publisher une fonction qu'il a, que le format
doit savoir relire, et qui manquait. Et il rend à la gouttière son sens : celle des
documents réels, 5,67 pt, est désormais la valeur par défaut d'un bloc neuf.

`Documents-reels.md` n'a pas été corrigé : le poste s'en tient à son périmètre, et ce
fichier fait foi. La rectification est ici, avec sa preuve.

#### Choisi sans certitude

**La gouttière par défaut vaut 5,67 pt** — les 2 mm d'ATA 21. Zéro aurait aussi bien fait
l'affaire pour la non-régression, une gouttière ne servant à rien à une colonne ; mais
elle aurait fait se toucher les colonnes le jour où l'on passe le nombre à deux, et ce
n'est pas ce que Publisher fait.

**L'espace avant s'applique aussi en tête de colonne**, comme R2 l'a fait appliquer au
premier paragraphe du bloc. Le relevé ne tranche pas davantage ici qu'il ne tranchait
là — il ne porte aucun `fo:margin-top`. Ce qui compte est que les deux cas soient
tranchés une fois et de la même façon, plutôt que deux fois différemment.

**Le panneau plafonne à vingt colonnes.** C'est un garde-fou du champ et non du format :
le fichier en accepte davantage, la composition ramène toute largeur de colonne à un
point minimum, et au-delà de vingt une colonne est plus étroite qu'un mot. Aucune fiche
ne donne la limite de Publisher ; le nombre est donc choisi, pas relevé.

#### Trouvé et laissé tel quel

**La vignette d'un modèle ne montre pas les colonnes.** `TemplatePreview` trace le texte
en filets pleine largeur, quel que soit le nombre de colonnes du bloc. C'est l'écart que
B1 a assumé et écrit — « les vignettes sont un schéma et non un rendu » — et il n'y a pas
de raison de le rouvrir pour ce poste.

**Le panneau TEXTE ne rafraîchit toujours pas la page**, colonnes comprises : régler le
nombre de colonnes ne redessine rien tant qu'on ne retouche pas le texte. C'est le défaut
que R2 a relevé et daté de R1, à corriger en un seul endroit pour les douze propriétés à
la fois. Les tests d'interface de ce poste posent donc les colonnes **avant** d'ouvrir la
fenêtre, comme ceux de R2 posaient les retraits.

*Au passage, et c'est un déplacement et non une correction* : l'ouverture d'une fenêtre
sur un document et le relevé de son encre sont sortis de `RetraitsTests` vers
`MyPublisher.Tests.Interface/Rendu.cs`, R3 en ayant besoin pour mesurer autre chose. Le
piège du banc — `PageRoot` mesuré en points mais vivant sous la mise à l'échelle du
zoom — n'est ainsi gardé qu'à un seul endroit. Deux copies, et la seconde oublie
l'assertion.

### R6 — Le panneau règle le paragraphe du curseur

*Spécifié le 11 octobre 2026.* Le README range parmi les limites que « l'alignement
reste une propriété du bloc ». **Ce n'est plus vrai du modèle.** Depuis R5,
`ParagraphSpan` porte un `Align`, l'import le remplit et la composition le lit. Les
documents réels en tirent parti : 31 paragraphes justifiés dans ATA 21, et 16 centrés
dans les Fiches. Ce qui manque est le geste : le panneau agit sur le bloc entier, et
rien ne crée ni ne modifie un écart de paragraphe depuis l'interface. Il en va de même
des cinq réglages de R2.

Le poste : pendant l'édition d'un bloc de texte, l'alignement, les retraits et les
espacements du panneau s'appliquent aux paragraphes que touche la sélection, ou au
paragraphe du curseur, sous forme d'écarts `ParagraphSpan`. Hors édition, ils restent
réglages du bloc, comme aujourd'hui. Deux écarts voisins identiques fusionnent, et un
écart qui ne s'écarte plus de rien disparaît.

Dépend de Q1 : tant qu'un saut de ligne tapé ne décale pas les indices de paragraphe,
un écart posé à la main glisse sur le paragraphe voisin à la frappe suivante.

*Recette* :
- tests d'interface : curseur dans le 2e de trois paragraphes, clic sur « centré » ;
  un seul écart `{Start 1, Length 1, Align Center}` ;
- l'écran et le PDF centrent ce paragraphe et lui seul, à 0,05 pt près ;
- un pas d'annulation rend le bloc d'avant ;
- les 16 paragraphes centrés des Fiches survivent à un aller-retour par l'interface.

Effort : moyen.

*Repris le 25 octobre 2026, dans la phase N (salve Saisie 7).* Les préalables sont devenus des postes :
la fonction de plage du noyau (N2), la sélection du texte connue de l'application (N15), le
curseur posé au clic (N12), et la saisie qui montre la composition (N21). Ce que la fiche 23
ajoute (b8, b9, b11) :
- les alignements de la barre et du menu, `Ctrl+L`, `Ctrl+E`, `Ctrl+R` et `Ctrl+J` (N10), les
  retraits et les espacements du dock passent par un `ParagrapheVise`, qui lit le bloc hors saisie
  et la sélection en saisie, et qui est nul quand les paragraphes visés diffèrent : alors aucun
  bouton n'est allumé. Les liaisons s'écrivent comme aujourd'hui, si bien que les comptes de la
  recette I3 tiennent ;
- deux boutons de retrait, « Diminuer » et « Augmenter le retrait », d'un taquet de 36 pt, au dock
  à côté des retraits, la maquette ne les montrant pas ;
- l'interligne reste un réglage du bloc : c'est un écart avec Publisher, qui le règle par
  paragraphe, écrit et assumé.

*Recette*, en plus : le curseur posé au clic ; l'état mixte n'allume aucun bouton ; un retrait
saisi au dock fait un pas à lui (Q25). Dépend de N2, N15, N21 et Q25.

### R7 — Éditer le texte riche à la souris

*Spécifié le 11 octobre 2026.* Le pendant de R6 à l'échelle du caractère, et la limite
qui pèse le plus sur la reprise des documents réels. Les 402 fragments des deux
documents ATR s'importent, se rendent et s'exportent, mais on ne peut pas mettre trois
mots en gras. Il faut appliquer la police, le corps, le gras, l'italique, le souligné
et la couleur à la sélection, en découpant, en fusionnant et en retirant des
`StyleSpan`.

**Une question à trancher avant d'écrire du code.** Pendant l'édition, l'écran montre
le `TextBox` en texte brut, et `TexteCompose` est masqué. Éditer du texte riche dans
une vue qui n'en montre rien n'a pas de sens. Deux voies :
- **garder le `TextBox` pour le curseur et la sélection seulement, transparent, et
  laisser `TexteCompose` dessiner dessous**. C'est la plus sobre. Le risque : que le
  curseur ne tombe pas où le texte composé est dessiné, puisque les deux mesures
  diffèrent ;
- **écrire un éditeur sur `Compositeur`**, avec le curseur, la sélection et la saisie
  calculés sur les lignes composées. C'est plus juste, et beaucoup plus lourd. Cette
  voie rejoint le seuil D.

Le poste commence par mesurer l'écart de la première voie sur les documents réels :
l'écart, en points, entre la position du curseur du `TextBox` et celle du caractère
composé. Si l'écart reste sous le pixel, on prend la première voie. Sinon, on revient
vers le propriétaire du projet pour décider.

Dépend de Q1.

*Recette* :
- tests d'interface : sélection des caractères 4 à 7 au pointeur synthétisé, puis
  `Ctrl+B`. On obtient un seul fragment `{Start 4, Length 3, Bold}`, et le PDF montre
  ces trois caractères seuls dans la fonte grasse (relevé du flux) ;
- `Ctrl+B` sur une sélection déjà grasse retire le gras, et le fragment disparaît s'il
  ne porte plus rien ;
- un pas d'annulation par commande.

Effort : lourd.

*Repris le 25 octobre 2026, dans la phase N (salve Saisie 8).* **La mesure qui commence le poste est
devenue N7**, avec sa recette et sa note de décision ; le choix de voie, qui reste un arrêt
obligatoire, se fait sur ses chiffres, et N21 porte la voie choisie. **La recette se trompait de
touche** : `Ctrl+B` n'est lié à rien, `Ctrl+Maj+B` est l'arrière-plan, et dans Publisher en
français `Ctrl+B` est « Atteindre la page ». Le gras est `Ctrl+G` : partout où la recette écrit
`Ctrl+B`, lire `Ctrl+G`.

Ce que la fiche 23 ajoute (b1, b4, b13, b18, b19) :
- la police, le corps (et ses deux touches, N10), le gras, l'italique, le souligné et ses sortes,
  la couleur et `Ctrl+Espace` s'appliquent à la sélection, par un `MiseEnFormeVisee` qui lit N15
  et écrit par N2 ; sans sélection, au mot du curseur (étude de 98) ; `Ctrl+Maj+C` et
  `Ctrl+Maj+V` copient et appliquent la mise en forme ;
- la barre montre l'état de la sélection : champs vides quand il est mixte, bouton d'état à
  `IsChecked = null`, avec son style ;
- **hors saisie, « le bloc » veut dire tous ses caractères, fragments compris** : aujourd'hui,
  `Ctrl+G` n'atteint pas le gras écrit explicitement dans un fragment importé, alors que la saisie
  montre tout en gras.

*Piège*, en plus : la garde `JamaisNul` de Q20 face à une police mixte ; la mémoire d'une case de
Q14 face à une valeur nulle ; les comptes de la recette I3.

*Recette*, en plus : sur une sélection mixte, `Ctrl+G` met le gras partout ; le corps 14 posé à la
barre fait un pas. Dépend de R6, N10, N19 et N21. Effort : moyen, les préalables portant
l'essentiel.

---

## Phase Q — Dettes consignées

*Ouverte le 11 octobre 2026.* Les postes et les relectures adversariales ont trouvé
des défauts qu'ils ont **écrits sans les corriger**, avec une raison chaque fois juste :
ce n'était pas leur périmètre. Écrits, ils étaient pourtant dispersés dans la prose de
douze sections, et rien ne les comptait. Cette phase les rassemble, chacun avec la
recette qui le prouvera corrigé. Les sections d'origine restent ce qu'elles sont : le
récit de ce qui a été trouvé, et quand.

Un défaut n'entre ici que s'il est **vérifié encore présent** dans le code du
11 octobre 2026. Plusieurs défauts consignés ont été corrigés depuis par une relecture :
le rafraîchissement du panneau, la rotation à l'import. Ils n'y figurent donc pas.

Ne sont pas repris non plus les écarts écrits comme des **choix** : les bornes du format
libre, les cases de la barre d'état qui disparaissent sans sélection, l'échelle de la
teinte, le dégradé semi-transparent qui sort opaque, et le débordement horizontal muet,
qui suit Publisher. Les rouvrir est une décision, pas une réparation.

### Q1 — Les styles suivent la frappe

**Le seul défaut de la liste qui abîme des données.** Les fragments de style se
repèrent par position de caractère, et les écarts de paragraphe par indice de
paragraphe. Aucune ligne du dépôt ne les met à jour quand le texte change : les seules
écritures de `Spans` sont dans `Clone()` et dans l'import. Quand on tape un mot en tête
d'un bloc importé, tous ses gras glissent d'autant de caractères vers la gauche du
texte. Un saut de ligne tapé décale de même tous les retraits, et
l'alignement, d'un paragraphe. Le document reste lisible, l'avertissement ne dit rien,
et l'enregistrement fixe le décalage.

Cela ne se voyait pas : aucun test ne tape dans un bloc qui porte des fragments, et
aucune recette n'enregistre un document réel après l'avoir retouché.

Le poste : à chaque modification du texte, calculer l'insertion ou la suppression par
rapport au texte d'avant, puis décaler, allonger, raccourcir ou retirer les fragments
et les écarts concernés. Un caractère inséré au contact d'un fragment prend le style du
caractère qui le précède, comme dans Publisher et Word. Ce calcul est une fonction pure
du noyau.

*Recette* :
- test pur : sur « abc def », gras sur « def » `{4, 3}`, insérer « XX » en 0 donne
  `{6, 3}` ; insérer en 5 donne `{4, 5}` ; supprimer « c d » retaille le fragment, et
  supprimer tout « def » le retire ;
- un saut de ligne tapé dans le 1er de trois paragraphes décale de 1 l'écart du 3e ;
- tests d'interface : ouvrir ATA 21, taper un mot en tête du premier bloc ; le PDF rend
  les mêmes mots en gras qu'avant la frappe (relevé du flux) ;
- sabotage : neutraliser le décalage fait tomber les trois points.

Effort : moyen. **À faire en premier** : R6 et R7 en dépendent, et chaque jour sans lui
est un jour où l'on peut abîmer un document importé.

*Fait le 11 octobre 2026.* Le calcul est une fonction pure du noyau,
`Models/Recalage.cs` : `Edition.Deduire` tire des deux textes un seul remplacement —
préfixe commun, puis suffixe commun dans ce qui reste —, `Recalage.Caractere` et
`Recalage.Paragraphe` en déduisent la plage d'après de chaque fragment et de chaque écart,
ou rien s'il est vidé. Seul `Recalage.Appliquer` touche au modèle : il change les plages
sur place et retire les vides, avant que le texte change.

La règle de style est celle de Publisher et de Word, que le propriétaire a fixée après
la relecture (`Recalage.Ancre`) :
- le texte tapé prend le style du caractère qui le précède ;
- en tête de texte ou de paragraphe (au tout début, ou juste après un saut), il prend
  celui du caractère qui le **suit**, s'il en est un dans le même paragraphe. Sinon,
  celui qui précède, ou le style du bloc en tête d'un texte vide. Un mot tapé en tête
  d'ATA 21 prend ainsi le gras et le corps du titre « Air Production » ;
- un remplacement, c'est-à-dire une sélection retapée, prend le style du **premier**
  caractère remplacé : un mot gras retapé reste gras. Avalonia remplace une sélection
  en un seul changement du texte (vérifié par une sonde), si bien que le recalage voit
  bien un remplacement, et non une suppression suivie d'une frappe.

Le texte qui était là garde toujours son style : seul le texte inséré en reçoit un.

**Le recalage n'a lieu qu'à la saisie, et c'est le choix qui règle tous les pièges.**
`Text` reste ce qu'il était, le texte tel qu'il s'écrit, et l'écrire ne recale rien. Une
propriété neuve, `Saisie` — sur `TextFrame` et sur `TableCell`, jamais écrite dans le
fichier —, recale puis écrit `Text`, et seules les trois zones où l'on tape s'y lient :
la saisie sur la page, le champ « Contenu » du panneau, le champ d'une case. Recaler à
chaque écriture de `Text` aurait demandé de deviner chaque fois si l'écriture était une
édition, et chacun des écrivains qui n'éditent pas se serait trompé à sa façon : le
sérialiseur pose les propriétés dans l'ordre du **fichier**, et un fichier qui donne les
fragments avant le texte les aurait tous poussés au-delà de lui ; l'import pose le texte
puis ses collections ; `CellFrames` reconstruit à chaque lecture un bloc auquel il
**prête** les fragments de la case, et un recalage à sa construction les aurait déplacés
à chaque redessin ; l'annulation relit un instantané. Par construction, aucun ne recale
— et un test épingle chacun, pour que la construction ne s'oublie pas. Aucune propriété
n'entre dans le format : `FormatVersion` reste à 4.

Ce que le poste a trouvé en route :

- **La règle de fusion, écrite telle quelle, reproduisait le défaut.** « Le paragraphe
  fusionné garde l'écart du premier » est juste pour l'effacement en tête de ligne. Mais
  une ligne sélectionnée de bout en bout, saut compris, et effacée, fusionne elle aussi
  deux paragraphes — et le suivant prenait alors le retrait de la ligne effacée. Quand la
  suppression part du début d'un paragraphe, ce sont des paragraphes entiers qui
  partent, et celui qui reste garde le sien. Les deux lectures d'un saut effacé parmi
  plusieurs donnent alors le même résultat.
- **La déduction est ambiguë, et l'ambiguïté tombait sur le geste le plus courant.**
  « Un\nDeux » qui devient « Un\n\nDeux » se lit aussi bien comme un retour tapé au bout
  de « Un » que comme un retour tapé en tête de « Deux ». La déduction place l'insertion
  le plus à droite. Une insertion faite **uniquement** de sauts de ligne est repoussée le
  plus à gauche (`Recalage.Placer`) : un retour tapé au bout d'un élément de liste ouvre
  un élément de liste, et non un paragraphe au réglage du suivant. Le prix est sur le
  geste inverse : un retour tapé en tête d'un paragraphe donne au paragraphe **vide**
  qu'il crée le réglage de celui d'au-dessus.
  La première version repoussait à gauche toute insertion, et la relecture a montré ce
  que cela coûtait. Coller « Item\n » en tête de la ligne « Item » faisait passer la
  ligne « Item » d'origine au réglage du titre d'au-dessus. Un collage qui porte du
  texte reste donc là où la déduction le pose, et reçoit le réglage de ce paragraphe,
  parfois celui d'en dessous : dans l'exemple, la copie prend le réglage de la ligne
  « Fin ». Une insertion ne change le réglage d'aucun paragraphe qui portait déjà du
  texte ; un test épingle le collage.
  Lever l'ambiguïté par le curseur de la zone de saisie a été écarté. La liaison ne
  transmet que le texte. Lire le curseur au moment où elle écrit, c'est compter sur
  l'ordre dans lequel Avalonia pose le texte puis déplace le curseur, que rien ne
  garantit, et cela lierait le noyau à la vue.
  Ailleurs, l'ambiguïté ne départage que du texte inséré qui répète son voisin à une
  frontière de fragment ou de paragraphe.
- **Un double-clic sur un bloc importé empile un pas d'annulation.** Le premier clic
  ouvre le geste de déplacement, qui réécrit l'ordonnée du bloc à son dernier chiffre
  près sans que le pointeur ait bougé (`107.15039999999999` devient `107.1504`) : le
  document change, l'historique le compte. Sans conséquence visible, hors du périmètre, non corrigé ; le test
  d'annulation prend donc son état de référence une fois la saisie ouverte.

Remplacer tout le texte, ce que B2 fera, donne au texte neuf le style du premier
caractère remplacé ; aucun fragment ne sort des bornes. Mais seulement par `Saisie` :
écrire `Text` ne recale rien, et la section B2 le rappelle. Retenu sans mesure : un
fragment qui débordait le texte avant l'édition y est ramené, comme la composition le
ramenait déjà en le dessinant.

Trouvé par la relecture et réparé : le panneau et la saisie sur la page se lient tous
deux à `Saisie`, et c'est `Text` que la saisie écrit. Sans l'avis
`[NotifyPropertyChangedFor(nameof(Saisie))]`, le panneau gardait un texte périmé, et la
frappe suivante dans le panneau effaçait ce qu'on avait tapé sur la page. Rien ne
l'éprouvait ; un test d'interface le fait maintenant, et le champ d'une case est
éprouvé de même.

Laissé tel quel : un fragment vidé par une suppression ne revient pas si l'on retape le
texte — y compris par l'annulation propre à la zone de saisie, quand elle a la main. Le
recalage ne voit que des textes ; l'annulation de l'application, elle, relit un
instantané et rend texte et fragments au caractère près. La saisie sur la page dessine
toujours le texte d'un seul style pendant qu'on tape : c'est R7.

*Recette* : `Scripts/recettes/Q1-styles-frappe.sh` joue au clavier les six tests
d'interface du poste sur ATA 21 importé. Elle exporte le document tapé et relève dans le
flux du PDF, par le nom de police, que les mêmes mots sont en gras au même corps, et que
« Nouveau », tapé en tête, a le gras et le corps du titre qui le suit. Puis elle rejoue
la mesure sur une contrefaçon aux fragments laissés en place, qui doit échouer.
Neuf sabotages dans `eprouver-le-juge.sh` : le décalage neutralisé, le saut de ligne
ignoré, la saisie de la page reliée à `Text`, la case qui ne recale plus, l'avis à
`Saisie` retiré du bloc puis de la case, et chacune des trois règles (le style qui suit
en tête de paragraphe, le premier remplacé, la poussée à gauche réservée aux sauts).

### Q2 — Le texte composé se détache de son bloc

`ControleDeBloc` s'abonne au bloc et à ses deux collections, mais ne se désabonne
jamais quand il quitte l'arbre visuel. Chaque changement de page laisse donc un
contrôle de plus accroché à chaque bloc de texte. C'est la fuite que C5 a refermée sur
`FondBloc`, et le remède est le même : suivre l'attachement.

*Recette* : après vingt changements de page, chaque bloc de texte n'a plus qu'un seul
abonné, compté par réflexion comme pour `FondBloc`. Effort : faible.

*Fait le 11 octobre 2026.* `ControleDeBloc` suit désormais l'attachement, exactement
comme `FondBloc` : il ne s'abonne pas hors de l'arbre visuel, l'attachement l'abonne —
en retirant d'abord, pour qu'un double attachement ne compte pas double — et le
détachement le désabonne, bloc et deux collections. Le texte composé et le marqueur de
débordement en héritent tous deux, puisqu'ils partagent cette base depuis A2.

**Le poste a trouvé que son énoncé était faux, et il faut le dire avant tout le reste.**
Changer de page ne laissait aucun abonné. Mesuré sur le code d'avant le remède : vingt
changements de page, et le bloc affiché garde ses deux contrôles — texte et marqueur —,
celui de la page quittée n'en a plus aucun. La liste des blocs de la page est un
`ItemsControl` ; il retire le contexte de données des gabarits qu'il démonte, la
liaison `Bloc="{Binding}"` retombe à null, et l'ancien `Suivre` se désabonnait en
perdant son bloc. Il en va de même pour `FondBloc`, dont le fond arrive par une liaison
de style — vérifié ici en lui retirant son remède : vingt changements de page, aucun
abonné de trop. Le « vingt passages, vingt abonnés » de C5 est celui du test qui le
garde, où le contrôle est posé à la main dans un panneau ; le passage de C5 a été
récrit pour le dire. La fuite est
réelle, mais elle ne frappe qu'un contrôle qui quitte l'arbre **en gardant son bloc** :
une fenêtre fermée sur un document resté ouvert, un aperçu, une vignette. Aucun n'existe
encore ; B6 et C7 en poseront. Le remède ne dépend donc plus de la conduite du
conteneur, et c'est ce qu'il achète.

D'où quatre tests, sous `poste=Q2`, dans `DetachementTests`. Le premier joue le geste du
plan — vingt changements de page sur un document qui porte un bloc à fragments et à
écarts, un tableau dont une case porte un fragment, et une seconde page — et compte par
réflexion les abonnés qui sont des contrôles de bloc : autant que de contrôles qui
montrent le bloc, sur le bloc et sur chacune de ses collections, et zéro pour le bloc
de la page quittée. **Il passait avant le remède**, et il est gardé pour ce qu'il est :
le geste que l'utilisateur fait. On l'a vu tomber en retirant les deux désabonnements à
la fois. Le second est celui qui éprouve le remède : un contrôle qui reçoit son bloc
avant d'être attaché, puis entre et sort vingt fois de l'arbre en le gardant — un
abonné et un seul pendant qu'il est affiché, aucun après. Le troisième garde l'autre
moitié : revenue sur sa page, le texte se redessine encore quand on règle le corps ou
qu'on ajoute un fragment. Un remède qui se détache sans se rattacher refermerait la
fuite et rouvrirait le défaut de R1 ; ce sabotage-là fait tomber les trois tests, parce
qu'un gabarit reçoit son bloc avant d'être attaché. Le quatrième, ajouté après la
relecture, garde le chemin de désabonnement qui existait déjà : un contrôle qui reste
affiché et reçoit un autre bloc — un présentateur qui recycle son gabarit le fait —
lâche l'ancien, bloc et deux collections, et n'écoute que le nouveau. Sans lui, retirer
ce désabonnement ne cassait aucun test.

Le tableau a sa raison d'être dans le premier test : ses cases sont des blocs calculés
à chaque lecture qui **empruntent** la collection de fragments de leur case — et le
contrôle s'y abonne. Le commentaire de `CellFrames` disait des fragments prêtés qu'« ils
ne portent aucun abonnement » ; c'était vrai du bloc, pas du contrôle qui le dessine, et
le commentaire le dit maintenant. Un contrôle resté accroché à cette collection serait
retenu par la case, qui vit aussi longtemps que le document.

Le compteur d'abonnés par réflexion est sorti de `FondsTests` vers `Rendu.Abonnes`, qui
sait lire n'importe quel événement — `CollectionChanged` en plus de
`PropertyChanged` — et qui lève sur un nom d'événement introuvable plutôt que de
rendre zéro, ce que les tests de fuite attendent justement.

*Recette jouée* : `Scripts/recettes/Q2-Q3-fuite-et-cache.sh`, commune aux deux postes.
Elle joue les tests ci-dessus par leur trait, en vérifie le compte, et lit d'abord une
règle dans les sources : tout contrôle de `Views/` qui s'abonne par `+=` dans un
gestionnaire de changement de propriété surcharge `OnAttachedToVisualTree` et
`OnDetachedFromVisualTree`. C'est la deuxième fois que la même fuite se présente ; la
troisième se fera arrêter là. La règle est une heuristique et la recette le dit : elle
prouve qu'on y a pensé, les tests prouvent qu'on l'a bien fait. Deux sabotages au juge :
le détachement rendu muet, que seul le deuxième test voit ; le désabonnement de l'ancien
bloc retiré, que seul le quatrième voit.

*Choisi sans certitude* : ce qui est compté. Le plan disait « un seul abonné » ; un
bloc de texte affiché en a deux qui sont des contrôles de bloc — le texte et le
marqueur — et d'autres qui n'en sont pas, les liaisons d'Avalonia. Le test compte les
premiers et les compare au nombre de contrôles qui montrent le bloc, plutôt qu'à un
nombre écrit en dur.

*Laissé tel quel* : `Spans` et `Paragraphs` ont un accesseur public en écriture. Un
code qui remplacerait la collection d'un bloc affiché laisserait le contrôle abonné à
l'ancienne et sourd à la nouvelle. Rien dans le dépôt ne le fait hors de `Clone()` et de
l'import, qui portent sur des blocs neufs ; le poste ne l'a donc pas couvert.

### Q3 — Le cache d'images oublie ses échecs

Une image absente à l'ouverture est retenue comme absente pour toute la durée du
processus : la remettre en place puis rouvrir le document laisse le bloc vide, sans
rien signaler. `Conv.Invalidate` existe, mais personne ne l'appelle. L'ouverture d'un
document est le bon endroit pour vider le cache.

*Recette* : ouvrir un document dont l'image manque, remettre le fichier, rouvrir ; la
couleur de l'image se relève dans la page rendue. Effort : faible.

*Fait le 11 octobre 2026.* `BitmapCache.OublierLesEchecs` retire du cache les chemins
dont la lecture a échoué, et eux seuls ; le modèle de vue l'appelle au début
d'`Adopt`, par où passent l'ouverture d'un fichier et le départ d'un modèle. Pas dans
l'accesseur de `Document`, que l'annulation emprunte aussi : annuler un geste n'est pas
rouvrir, et n'a pas à relire le disque. Les trois cas sont tenus par des tests.

**Pourquoi oublier les échecs plutôt que vider le cache, ou que relire les images du
document.** Un échec est mis en cache à dessein : sans cela, un bloc dont l'image manque
relirait le disque et lèverait une exception à chaque notification de liaison, donc à
chaque image d'un glissé. Il suffit qu'il cesse de l'être pour la vie du processus. Les
réussites, elles, sont justes tant que leur fichier ne bouge pas, et les relire à chaque
ouverture ne réparerait aucun défaut de ce poste. Oublier tous les échecs plutôt que
ceux du seul document a un avantage de plus : le logo des coordonnées professionnelles,
qui passe par le même cache sans être un chemin du document, en profite sans qu'il ait
fallu parcourir quoi que ce soit — blocs, groupes, cases, fond de page.

**Rouvrir n'est pas la seule façon de redemander une image.** La réinsérer (`AddImage`)
ou la choisir pour un remplissage passe par le même trou : le fichier vient d'être
désigné, `AddImage` vient même de le lire pour en prendre les proportions, mais la
liaison passe par le cache, qui rend l'échec retenu à l'ouverture. La relecture l'a
mesuré : bloc réinséré vide, 0 pixel. Ces deux gestes ont un chemin à donner, et
`Invalidate` le reçoit : `AddImage` l'appelle après la sonde, et le choix d'une image de
remplissage, passé de la fenêtre au modèle de vue (`ChoisirImageDeFond`) pour qu'un test
l'atteigne sans dialogue de fichier, l'appelle avant d'écrire le chemin. Choisir de
nouveau, pour le même remplissage, l'image qu'il désignait déjà ne change aucune
propriété et ne redessine donc rien : c'est la réouverture qui couvre ce cas-là.

*Recette jouée* : la même que Q2, sous `poste=Q3`. Un test d'interface ouvre par
`LoadFrom` — le geste du menu, et non une affectation au modèle de vue — un document
dont les deux images manquent : un bloc image, qui passe par la liaison, et une forme
au fond illustré, que `FondBloc` dessine par `Conv.Bitmap`. Deux chemins vers le cache,
deux couleurs, cyan pour le bloc parce qu'il n'est pas symétrique. Aucune ne se voit ;
les fichiers sont remis ; le document est rouvert dans la même fenêtre, le même
processus et donc le même cache ; les deux couleurs se relèvent dans l'image composée.
Puis une troisième ouverture rend **la même instance** de l'image déjà lue : c'est
l'épingle du « seulement ». Entre la remise en place des fichiers et la réouverture, un
geste annulé reconstruit tous les blocs : les deux images restent absentes, et c'est
l'épingle de « l'annulation n'est pas une réouverture ». Le même test est joué deux
fois, par `LoadFrom` et par `StartFromTemplate`. Un troisième test réinsère l'image
remise en place, et en donne une autre à un remplissage par `ChoisirImageDeFond` : les
deux couleurs paraissent.

Six sabotages au juge, et ils ne se recouvrent pas : l'appel retiré d'`Adopt` fait
tomber la réouverture ; `Cache.Clear()` à la place de l'oubli sélectif répare le défaut
tout aussi bien — et c'est pourquoi seule l'épingle le voit ; l'oubli réservé à
l'ouverture d'un fichier fait tomber la variante du modèle ; l'oubli placé dans
l'accesseur de `Document` fait tomber l'épingle de l'annulation ; l'`Invalidate` retiré
d'`AddImage`, puis celui de `ChoisirImageDeFond`, font tomber le troisième test.

*Laissé tel quel* : une image **modifiée** sur le disque pendant que le processus vit
reste périmée à l'écran, rouverture comprise, alors que l'export, qui lit le disque à
chaque fois, montre la nouvelle. C'est un écart écran ↔ PDF, mais ce n'est pas celui
du poste, et le corriger demanderait de dater les entrées du cache plutôt que de les
oublier.

### Q4 — Un filet au même endroit à l'écran et au PDF

Mesuré en A2 sur un filet de 2 pt : l'écran le rentre dans le bloc, alors que l'export
le centre sur le bord. La documentation de `Frame.StrokeThickness` dit « centré sur le
bord » : c'est donc l'écran qui a tort.

*Recette* : sur un filet de 4 pt, les bords relevés à l'écran et au PDF concordent à
0,25 pt près. Effort : faible.

*Fait le 12 octobre 2026.* Un `Rectangle` ou une `Ellipse` d'Avalonia tracent leur
trait sur leur cadre **diminué d'une demi-épaisseur** : posés sur le cadre du bloc, ils
le rentraient d'autant. L'élément du filet est donc élargi d'une demi-épaisseur vers
l'extérieur (`Conv.FiletCentre`, une marge négative), **et rendu sans arrondi de mise
en page** (`UseLayoutRounding="False"`). Sans ce second point, Avalonia arrondit la
marge au pixel de mise en page — un point, sous la mise à l'échelle — en arrondi
bancaire : seule une demi-épaisseur entière était respectée. Trois éléments sont
concernés : le contour commun des blocs (`Rectangle.frameStroke` : image, texte,
groupe, tableau, cases), le rectangle et l'ellipse des formes. L'export n'a pas bougé :
il disait vrai.

*Ce que la relecture a trouvé.* La première version ne portait que la marge, et sa
recette que des filets de 4 pt, dont la demi-épaisseur est entière : elle passait. La
relecture a mesuré, écran − PDF, +0,50 pt pour un filet de 1 pt, −0,25 pour 1,5, −0,50
pour 3, +0,50 pour 5. Or 1 pt est le filet du modèle, et 77 des 88 filets des Samples.
Le sabotage du relecteur — la demi-épaisseur arrondie à l'entier inférieur — passait
toute la vérification ; il fait désormais tomber la recette de Q4 sur 24 côtés.

*Mesuré.* Recette `Q4-filet-centre.sh` : douze blocs, 48 côtés relevés à l'écran et au
PDF à deux pixels par point — six chemins filés de 4 pt (image cadrée pour remplir,
texte, forme, forme arrondie, ellipse, groupe) et une rangée de 1 pt et de 3 pt (forme,
texte, ellipse). Avant ce poste : 48 côtés en échec, la comparaison de la page à
3,41 % pour 3,34 % calculés. Avec la seule marge : 24 côtés en échec, les filets de 1
et 3 pt, et 0,36 %. Après : 0,00 pt sur les 48 côtés et 0,04 %, seuil à 0,3 %. Le
script de la relecture, appliqué de 0,5 à 5 pt sur des formes, des textes et des
ellipses (84 côtés) : |écran − PDF| ≤ 0,01 pt à huit pixels par point. À deux pixels par point, les
filets de 0,5 et 1,5 pt montrent ±0,25 pt **au PDF seul** — pdftoppm cale sur le pixel
entier un trait dont le bord tombe au milieu d'un pixel —, et c'est pourquoi la recette
mesure 1 et 3 pt plutôt que 1,5.

La recette de C1, qui tolérait l'écart de 0 à +1,25 pt, l'exige désormais à
0 ± 0,25 pt ; sa comparaison est passée de 0,88 % à 0,04 % — le lissage du mot
« Légende » et une colonne de pixels au bord droit du portrait —, et son seuil de
1,2 % à 0,25 %, recalculé sans Q4 et écrit dans la recette. Celle de C5 est passée de
0,99 % à 0,09 %. Le premier relevé, 0,35 %, avait été pris pour le lissage de
l'ellipse : c'était encore Q4, son filet de 3 pt restant à −0,5 pt. Son seuil passe de
2 % à 0,2 %, sous ces 0,35 %.

*Ce que la réparation a déplacé.* Centré, le filet d'un bloc posé sur le bord d'un
groupe ou d'un tableau en sort d'une demi-épaisseur — et la découpe du groupe, faite au
cadre exact, l'aurait rogné de moitié à l'écran, là où il était entier, rentré. C'est
ce qui a fixé la découpe de Q5, qui en tient compte.

*Choisi sans certitude.* La marge se calcule sur l'épaisseur seule, que le filet soit
tracé ou non : sans filet, elle n'a rien à déplacer.

*Trouvé et laissé tel quel* (relevés par la relecture, non corrigés) :
- **la phase des tirets** diffère entre l'écran et le PDF : la géométrie d'un filet en
  tirets est la même des deux côtés, pas l'origine de son motif ;
- **les positions fractionnaires** sont arrondies à l'écran : `Canvas.Left` et
  `Canvas.Top` passent par le même arrondi de mise en page, au point entier ; un bloc à
  une abscisse fractionnaire se dessine à l'écran jusqu'à ≈ 0,3 pt de sa place au PDF.
  Le même remède — retirer l'arrondi — porterait sur tout le canevas, et demande sa
  propre mesure.

Six sabotages au juge : la marge du contour commun ôtée (Q4 et C1) ; celle de
l'ellipse ôtée (Q4) ; l'arrondi de mise en page rétabli sur le contour commun, sur le
rectangle des formes, sur l'ellipse (Q4 ; C5 aussi pour l'ellipse) ; la demi-épaisseur
arrondie à l'entier inférieur (Q4).

### Q5 — Un groupe rogne pareil à l'écran et au PDF

Un bloc qui dépasse de son groupe est coupé à l'écran et pas au PDF. Le geste ne
produit pas ce cas, mais un fichier écrit à la main ou un import le peut. Il faut
trancher dans le sens de A2 : **l'export rogne au groupe, comme l'écran**.

*Recette* : groupe de 120 pt portant un texte de 400 pt ; rien n'est dessiné au-delà
du groupe dans le PDF, et `comparer-rendus.sh` passe. Effort : faible.

*Fait le 12 octobre 2026.* L'écran découpait les blocs d'un groupe sans qu'aucune
ligne du dépôt le dise : c'est le thème d'Avalonia qui pose `ClipToBounds` sur la liste
qui les porte — mesuré en le retirant. La découpe est désormais calculée dans le noyau,
`Decoupe.DesBlocs`, et les deux rendus la lisent : l'export la pose par
`IntersectClip` avant de dessiner les blocs d'un groupe ou les cases d'un tableau, et
l'écran la pose sur la liste, dont le `ClipToBounds` du thème est éteint
(`DecoupeDesBlocs`, liée au porteur, à sa taille et, pour un tableau, à ses cases).

**La découpe n'est pas le cadre du groupe.** Elle en déborde, de chaque côté, du plus
large demi-filet des blocs qu'il porte, groupes imbriqués et cases comprises. Grouper
ajuste le groupe aux cadres de ses blocs, filets non compris ; depuis Q4, le filet d'un
bloc posé sur le bord en sort d'une demi-épaisseur, et une découpe au cadre exact le
rogne de moitié — mesuré en l'y ramenant : 2 pt sur un filet de 4, à l'écran comme au
PDF. **Un bloc tourné compte pour √2 fois** : le filet est joint en onglet, et la pointe
de l'angle d'un bloc tourné sort de t/2·√2 — à 45°, c'est elle qui dépasse le plus. La
première version comptait t/2 pour tous : la relecture a trouvé la pointe d'un losange
émoussée dans son groupe. Ce qu'un bloc tourné porte compte de même.

*Mesuré.* Recette `Q5-groupe-rogne.sh`, sur le cas du plan : un texte de 400 pt à fond
jaune dans un groupe de 120. Avant : 67 200 pixels dessinés au-delà du groupe dans le
PDF — les 280 pt de fond qui dépassent, sur 60 pt de haut —, aucun à l'écran ; la
comparaison de la page à 4,35 %. Après : aucun pixel des deux côtés, et 0,05 %. La même
page porte un groupe et un tableau filés de 4 pt sur leur bord : 4 pt mesurés des deux
côtés. Ajoutés après la relecture, sur ses documents : un groupe tourné de 30°, un
groupe dans un groupe — chacun découpe à sa marge, rien au-delà ni de l'un ni de
l'autre — et un losange filé de 6 pt dans un groupe ajusté à son empreinte, dont la
pointe d'onglet est relevée bleue à 3,25 pt au-dessus de l'angle, des deux côtés. Page
à six blocs : 8,03 % avant le poste, 0,13 % après, seuil 0,3 %. Douze tests du noyau
sous `poste=Q5`, que la recette joue d'abord.

*Choisi sans certitude.* Une marge commune au groupe et non propre à chaque bloc : un
bloc sans filet qui dépasse d'un groupe dont un autre bloc est filé s'arrête à un
demi-filet au-delà du bord, et non sur le bord. Une découpe par bloc l'éviterait, au
prix d'une découpe par enfant à l'écran ; aucun geste ne produit ce cas. Les tableaux
reçoivent la même découpe que les groupes, bien que leurs cases ne dépassent jamais :
c'est le même chemin de rendu des deux côtés, et le seul effet est de laisser entiers
les filets des cases du bord. Le premier pixel au-delà de la découpe, dans le PDF, porte
un soupçon de la couleur coupée — (255, 249, 223) sur un fond (255, 204, 0) — : c'est le
lissage de `pdftoppm`, que la recette tolère et dit. La marge commune a été acceptée
par le propriétaire le 12 octobre 2026.

*Laissé tel quel.* À l'écran, la découpe n'est liée qu'au porteur et à sa taille — et,
pour un tableau, à ses cases —, non à `Children` ni aux filets des blocs portés : elle
ne se recalcule pas si l'on change le filet d'un bloc déjà groupé, ou si l'on remplace
les blocs d'un groupe affiché. Aucun geste ne le fait aujourd'hui — un bloc groupé ne se
sélectionne pas, et grouper ou dissocier recrée le groupe —, mais un futur geste qui le
ferait trouverait l'écran en retard sur l'export.

Trois sabotages au juge : la découpe ôtée de l'export (Q5), la découpe ramenée au cadre
exact (Q5 et `DecoupeTests`), le facteur √2 des blocs tournés ôté (Q5 et
`DecoupeTests`).

### Q6 — Mesurer la découpe d'un remplissage par image

Le document de recette de C5 ne porte que des rectangles à angles droits : un sabotage
de la découpe d'un remplissage par image traverse toute la vérification.

*Recette* : ajouter au document de C5 un bloc arrondi rempli d'une image, relever la
couleur du fond de page dans le coin, et ajouter à `eprouver-le-juge.sh` le sabotage
qui doit tomber. Effort : faible.

*Fait le 12 octobre 2026.* `Samples/recette-fonds.mypub` porte un bloc de 200 × 120 pt
aux angles arrondis de 30 pt, rempli d'une image — un aplat magenta,
`Samples/recette-fonds-trame.png`, étiré au bloc —, sans filet. La recette de C5 y
relève quatre points de plus, au PDF puis à l'écran : l'image au centre et à 2 pt du
bord gauche, juste sous l'arc ; le fond de page dans les coins haut-gauche et
bas-droite, à 9,6 pt hors de l'arc. La couleur attendue dans les coins se calcule — le
dégradé de page, du blanc au #336699 — et concorde avec le relevé.

*Mesuré.* Deux sabotages, joués à la main sur l'ancienne recette puis sur la nouvelle.
La découpe de l'export privée de son arrondi : l'ancienne recette passe, la nouvelle
tombe sur les deux coins (magenta au lieu du fond, et l'écran en désaccord avec le PDF).
La même chose à l'écran (`FondBloc`) : la nouvelle tombe sur le désaccord écran ≠ PDF.
La comparaison de la page n'avait pas bougé en ajoutant ce bloc (0,35 %, alors), et elle
est à 0,09 % depuis la seconde réparation de Q4 : un coin rogné de travers ne pèse que
quelques centaines de pixels, et c'est bien pourquoi la mesure est ponctuelle.

### Q7 — La recette de A6 sur le cas réel

La recette de A6 fabrique ses blocs par un chemin qui évite celui de l'application :
c'est le défaut relevé sur R2, et que la consigne nommait pourtant.

*Recette* : réécrite sur la fabrique de l'application (outil armé, tracé synthétisé),
et le sabotage qu'elle laissait passer tombe. Effort : faible.

*Fait le 12 octobre 2026.* Les quatre tests du poste A6 tracent désormais leurs blocs
comme l'utilisateur : la touche de l'outil, puis un cadre tiré au pointeur sur la page.
Le premier trace à l'outil texte, dont le bloc s'ouvre aussitôt en saisie ; les trois
autres à l'outil rectangle. À plusieurs blocs, la sélection se fait au Ctrl+clic, le
second bloc restant pris de son tracé. Les libellés de la barre sont comparés en entier,
et non plus par sous-chaîne. La recette s'appelle maintenant
`Scripts/recettes/A6-Q7-taille-dans-la-barre.sh`, ce qui la fait compter pour les deux
postes au bilan ; elle refuse en outre un test du poste A6 qui poserait un bloc par
`new`, par `Carre(` ou le sélectionnerait par `SelectOnly` ou `SetSelection`, et elle
exige cinq tracés. Mesuré : les tests d'avant Q7, remis en place, font tomber ce
contrôle sur huit lignes.

**Le sabotage, l'historique ne le nommait pas.** Ni la relecture du 7 septembre ni le
journal ne disent lequel l'ancienne recette laissait passer ; ils disent seulement que
ses blocs évitaient le chemin de l'application. Je l'ai donc cherché sur ce chemin-là, et
j'en ai trouvé deux. Chacun a été appliqué, la recette lancée, puis l'arbre restauré par
`git checkout` :

| Sabotage | Ancienne recette (tests d'avant Q7) | Recette réécrite |
|---|---|---|
| `DrawWithActiveTool` ne donne plus sa largeur au bloc tracé | passe, 4 tests sur 4 | **tombe**, 3 tests sur 4 |
| `Insert` ne sélectionne plus le bloc tracé | passe, 4 tests sur 4 | **tombe**, 4 tests sur 4 |

Les deux sont au juge. **Aucun des deux n'était un trou du filet entier**, et il faut le
dire pour ne pas grossir le poste : mesuré sur toute la suite avec les tests d'avant Q7,
le premier tombait déjà par `GestesTests.Un_tableau_trace_sur_la_page_mesure_ses_cases_au_cadre`,
le second par `GestesTests.Inserer_un_tableau_pose_une_bande_a_trois_cases_et_sannule`.
C'était un trou de la recette de A6, que le juge compte pour tel depuis qu'il exige que
chaque poste attrape son propre sabotage.

**Le poste Q14 a changé ce que gardait la recette.** La relecture des sabotages de A6
avait noté qu'à un seul bloc la barre se rafraîchissait « par accident », par la dérive
de l'aller-retour en millimètres du panneau. Q14 a supprimé cette dérive. Mesuré après
Q14, retirer `NotifyFrameGeometry()` de `NotifySelection` fait tomber trois des quatre
tests d'avant Q7, et non plus le seul test à plusieurs blocs ; les quatre tests réécrits
tombent tous.

*Choisi sans certitude* : l'outil texte pour le premier test, parce que c'est l'outil le
plus employé ; mais son bloc entre en saisie, et la barre est alors lue pendant la
saisie. Un défaut qui ne frapperait la barre qu'une fois la saisie refermée échapperait
à ce test-là ; les trois autres, qui tracent des rectangles, ne passent pas par la
saisie.

### Q8 — N'avertir que des polices dessinées

La passe des polices relit l'héritage des fragments pour son compte, au lieu de lire
les styles composés. Un fragment entièrement recouvert par un autre produit donc un
avertissement pour une police que rien ne dessine.

*Recette* : un bloc en Noto Sans, dont tout le texte est recouvert par un fragment en
DejaVu Sans, avec un fragment intermédiaire « Police Inexistante » lui-même recouvert ;
l'export n'avertit de rien. Effort : faible.

*Fait le 12 octobre 2026.* La passe des polices de `PdfExporter` lit les styles
composés : pour chaque bloc de texte, page par page, elle résout le contenu comme
`DrawText` le résout — champ compris, avec la date et les coordonnées de l'export —, et
passe le contenu à `StyleEffectif.Decouper`, la découpe que la composition fait
elle-même. Seuls les styles des morceaux obtenus sont confrontés à fontconfig. Quels
fragments s'appliquent au contenu (aucun pour un champ résolu) est une seule fonction,
`Fragments`, que `DrawText` appelle désormais aussi : la passe et le dessin ne peuvent
plus parler de deux textes différents. L'ancienne fonction `Polices`, qui relisait
l'héritage pour son compte, a disparu, et avec elle l'asymétrie qu'elle assumait (« un
avertissement de trop plutôt qu'un de moins ») : ce qui n'est pas dessiné ne compte pas.

Mesuré. Le comportement d'avant, rétabli par le premier sabotage, nomme sur le document
de la recette deux polices que rien ne dessine : « Police Inexistante » et la police
propre du bloc, toutes deux remplacées par Noto Sans, que `fc-match` rend sur cette
machine pour une famille inconnue. Corrigé, l'export ne dit rien, et `pdffonts` relève
dans le PDF la seule DejaVu Sans. La contre-épreuve retire le fragment qui recouvre
tout : les deux polices sont nommées, et Noto Sans entre dans le PDF.

Neuf tests sous `poste=Q8`, dans `PolicesTests` : le cas du plan, le texte laissé à
découvert (qui avertit toujours), le fragment hors du texte, le champ dont les fragments
ne sont pas dessinés alors que sa police l'est, le champ de coordonnées vide qui ne
dessine rien, le fragment blanc — saut de ligne, espace, tabulation — et la case de
tableau, où un fragment absent dessiné avertit et le même recouvert se tait. Ce dernier
vient de la relecture : retirer les cases de la descente de la passe laissait
`verifier.sh` vert. *Recette* :
`Scripts/recettes/Q8-polices-dessinees.sh`. Elle joue le document du plan, avec un
second bloc dont la police propre est absente, plus exigeant que le bloc en Noto Sans,
qui est installée ici et ne dirait rien de toute façon. Elle relève les polices du PDF,
rejoue le document découvert, et compte les tests. Trois sabotages au juge : l'héritage
relu pour son compte (la recette tombe, et huit des neuf tests), les fragments
d'un champ lus comme s'ils étaient dessinés (seul le test du folio tombe), et les cases
de tableau retirées de la descente (seul le test de la case tombe).

*Choisi sans certitude.* Un morceau blanc — espaces, tabulations, sauts de ligne — ne
compte pas, comme `Tracer` le saute en dessinant : `pdffonts` ne relève pas sa police.
Sa largeur se mesure pourtant dans cette police, substituée ou non ; c'est le seul
effet d'une police que la passe tait. La première version comptait les espaces ; la
relecture a demandé de s'aligner sur le dessin et sur ce que dit le README. Une ligne vide prend
sa hauteur dans la police du bloc (`Naturelle(commun)` dans `Compositeur`) : un bloc
dont tout le texte est recouvert mais qui porte une ligne vide fait mesurer sa police
sans la dessiner, et ne la nomme pas. Le texte coupé par le bord du bloc est compté
comme dessiné.

*Trouvé et laissé tel quel.* L'écran garde sa propre copie de la règle « un champ résolu
n'a pas de fragments » (`ControleDeBloc.Mesurer`) : la règle n'est partagée qu'entre la
passe et le dessin du PDF, pas avec l'écran. La partager demanderait de la poser dans le
noyau ; le poste ne l'a pas fait.

### Q9 — Retirer `AbsoluteLineHeight`

C'est le survivant de l'ancienne sémantique de l'interligne (`FontSize × LineHeight`),
que R4 a remplacée. Seule la vignette des modèles la lit encore, et se trompe ainsi de
16 %. Elle doit lire l'avance de `Compositeur`, ou la propriété disparaître.

*Recette* : la propriété n'existe plus, et la vignette du bulletin a autant de lignes
que son rendu réel. Effort : faible.

*Fait le 12 octobre 2026.* `AbsoluteLineHeight` quitte `TextFrame`, avec les deux
`NotifyPropertyChangedFor` qui la tenaient à jour ; le commentaire de `LineHeight`, qui
parlait encore d'un « multiple du corps », dit désormais la sémantique de R4. La
vignette ne compte plus ses filets à la louche : elle compose chaque bloc par
`Compositeur`, avec la règle d'Avalonia, et pose un filet par ligne non vide, à
l'ordonnée, à la longueur et au fer de la ligne — une ligne justifiée court jusqu'au
bout de sa colonne —, sous le calage vertical, découpé au bloc. `EstimateLines` est
parti avec.

*Mesuré.* La vignette du bulletin, rendue à deux pixels par point
(`VignetteTests`), porte 48 filets dans ses huit blocs de texte saisi, un par ligne de
la page, chacun à deux pixels au plus de la place de sa ligne. L'ancienne en aurait
posé 45 : 13 au lieu de 14 dans chacune des trois colonnes. Le sabotage qui rétablit
l'espacement du corps décale déjà le deuxième filet des colonnes de 3,2 pixels
(1,6 pt) à 9,5 pt, et le test tombe ; il est au juge.

*Choisi sans certitude.* Un champ garde un seul filet, de toute la largeur, à l'avance
de sa police : son contenu — les coordonnées de l'utilisateur, la date du jour où il
ouvrira le document — n'est pas celui du catalogue, et la carte de visite n'est faite
que de champs. Il reste en haut de son bloc, quel que soit son calage vertical, comme
avant. Le filet est centré dans sa ligne, là où l'ancien en occupait le haut. Le test
admet deux pixels d'écart, parce que le lissage peut allumer la rangée du dessus.

*Trouvé et laissé tel quel.* La vignette mesure avec `MetriquesAvalonia`, et hérite
donc de ce que Q13 a trouvé : l'italique d'une famille qui n'en a pas — DejaVu Sans, ici
— se résout selon l'histoire du processus, en gras ou en romain penché. Les quatre
modèles portent de l'italique — le chapeau et la légende du bulletin. Dans le
catalogue, un filet d'italique peut donc être plus long ou plus court d'une ouverture à
l'autre, et une ligne passer à la suivante. Le test ne le voit pas : il compose ses
attentes dans le même processus que la vignette, avec la même résolution.

*Repris le 12 octobre 2026, après relecture.* La relecture a fait passer trois
sabotages à travers les tests : la vignette qui perd son fer et la longueur de ses
filets, celle qui ne découpe plus au bloc, et le champ qui ne laisse plus de filet. Le
test ne lisait que le nombre et l'ordonnée des filets du bulletin : ce que le
paragraphe précédent dit de la longueur, du fer et de la découpe était écrit dans le
code, mais pas éprouvé. `VignetteTests` passe de deux tests à six :

- le début et la fin de chaque filet du bulletin, à un point près de ceux de sa ligne à
  la page. Le bulletin ne centre aucun texte saisi : le test centre le titre de une et
  le chapeau, et cale le sous-titre à droite. Il éprouve ainsi les 48 lignes, dont 36
  justifiées, 3 centrées et une à droite, et exige plus de vingt justifiées, deux
  centrées et une à droite : un fer qu'aucune ligne n'éprouverait passerait vert ;
- un filet et un seul par champ de la carte de visite, de toute la largeur de sa zone de
  texte, au milieu de sa première ligne ;
- la première colonne du bulletin ramenée à cent points : sa composition en pose
  207,3 points de haut, et aucune rangée de sa couleur ne paraît entre le bas du cadre et son bas
  d'origine ;
- une même vignette qui reçoit deux documents de suite recompose ses filets.

*Mesuré.* Un sabotage de chacune des trois sortes que la relecture a fait passer — ses
textes exacts n'ont pas été gardés —, et des variantes, font tomber un test et la
recette, éprouvés à la main un par un : le fer perdu (`justifiee` toujours vraie, ou toujours fausse,
centre ou droite ramenés au retrait, longueur toujours pleine) fait tomber les
abscisses ; la découpe retirée, ou élargie au double de la hauteur, le débordement ; le
filet de champ retiré, réduit à la moitié de la largeur, ou effacé par la garde du texte
vide, la carte de visite. Les trois sont au juge, avec un quatrième : la mémoire des
filets qui ne se renouvelle plus quand le document change.

*La vignette garde ses filets.* Elle composait chaque bloc à chaque rendu. Les filets de
chaque bloc, en coordonnées de la page, sont désormais gardés pour un document et une
taille de contrôle, et rangés par ordre de rencontre : les cases d'un tableau sont des
blocs refaits à chaque lecture, une clé par référence ne les retrouverait pas. À 134
pixels de côté, moyenne de 500 rendus, deux passes, poste chargé : le bulletin passe
de 2,5 et 2,7 ms par rendu à 0,06 ms, l'affiche de 1,2 et 1,3 ms à 0,03 ms, le dépliant
de 0,34 à 0,03 ms ; la carte de visite, qui n'a que des champs, reste vers 0,02 ms.

*Choisi sans certitude.* La mémoire ne voit pas une modification du document en place.
Le catalogue n'en fait aucune — ses documents sont lus pour la vignette, et le modèle
choisi est rouvert depuis son fichier —, et c'est écrit dans le commentaire de
`TemplatePreview`. Un appelant qui afficherait un document qu'on édite devrait en poser
un autre.

*Ce que la recette ne prouve pas*, et qu'elle dit désormais en tête : la justesse de la
composition elle-même — les attentes sont composées par le même `Compositeur`, avec les
mêmes métriques —, la vignette à la taille du catalogue, les blocs tournés, les groupes,
les tableaux, le calage vertical d'un champ, les retraits, l'affiche et le dépliant, et
le gain de la mémoire, mesuré à la main seulement.

### Q10 — Saisir une poignée hors de la page

La couche de saisie s'arrête au bord de la page. Une poignée de rotation qui en sort,
sur un bloc tourné de 180° collé en bas ou sur un bloc collé en haut règles masquées,
ne se saisit pas. Une poignée d'angle sur un bloc posé à Y = 0 tombe sur la règle, qui
pose un repère. Il faut étendre la couche de saisie à la zone de travail, en gardant à
la règle son geste de C2b.

*Recette* : les deux cas de A1, au pointeur synthétisé, tournent le bloc ; la poignée
d'angle redimensionne sans poser de repère ; un clic sur la règle loin de tout bloc
pose toujours un repère. Effort : moyen.

*Absorbé par I4, le 25 octobre 2026.* La planche de I4 rend les règles fixes, hors de la
zone qui défile, et étend la couche de saisie à toute la planche : c'était ce poste. Ses
trois cas sont joués par un test de la planche (`PlancheTests`), et un repère se pose
toujours d'un clic sur la règle. La règle ne sert plus la poignée de rotation, qui ne
s'y dessine plus jamais. Les blocs et les outils, eux, s'arrêtent au bord de la page. La
recette de I4 devient `I4-Q10-planche.sh`.

### Q11 — La liste des blocs défile pendant le glissé

La liste ne construit que les éléments visibles : on ne peut pas tirer le quarantième
bloc jusqu'en tête.

*Recette* : sur une page de 60 blocs, un glissé synthétisé maintenu en haut de la liste
la fait défiler, et le bloc se pose au rang 0. Effort : faible.

### Q12 — Dépendance vulnérable

La compilation signale `Tmds.DBus.Protocol` 0.21.2, tiré par Avalonia 11.3.9 et
affecté d'une vulnérabilité de gravité haute (GHSA-xrw6-gwf8-vvr9).

*Recette* : `dotnet list package --vulnerable --include-transitive` ne rapporte rien,
et `Scripts/verifier.sh` passe. Effort : faible, si une version corrigée existe.

*Fait le 11 octobre 2026* — une ligne dans `MyPublisher/MyPublisher.csproj` : une
référence directe à `Tmds.DBus.Protocol` **0.21.3**. Avalonia n'a pas bougé.

**La version corrigée existe sur la branche même qu'emploie Avalonia.** L'avancée
GHSA-xrw6-gwf8-vvr9, publiée le 8 avril 2026, déclare corrigées la 0.92.0 et, pour
`Tmds.DBus.Protocol` seulement, un rétroportage en 0.21.3 sur la branche `release/0.21`.
Sa notice NuGet porte les mêmes cadres et les mêmes dépendances que la 0.21.2 ; seuls
le numéro et la branche diffèrent. Avalonia 11.3.9 demande « au moins 0.21.2 » : la
0.21.3 tient dans sa contrainte sans rien forcer, et c'était le plus petit pas possible.
La montée d'Avalonia dans la série 11.3.x n'a donc pas été tentée, et aucune recette
au pixel n'avait de raison de bouger — aucune n'a bougé.

**Un seul projet tire le paquet, pas trois.** `dotnet list` le signalait dans
`MyPublisher`, `MyPublisher.Banc` et `MyPublisher.Tests.Interface`, mais
`dotnet nuget why` montre que les deux derniers ne le reçoivent que par leur référence à
`MyPublisher` (Avalonia.Desktop → X11 → FreeDesktop). La référence directe va donc dans
ce seul projet, et la règle « la dépendance la plus proche l'emporte » la transmet aux
deux autres. La base ne signalait rien d'autre sur les cinq projets.

**La recette lit la base, puis prouve qu'elle l'a lue.** `Scripts/verifier-dependances.sh`
lit le relevé JSON plutôt que la phrase « has no vulnerable packages », qui se dit aussi
bien d'une base saine que d'une base vide ; elle échoue sur tout problème signalé, sur
tout projet manquant au relevé, et nommément sur l'absence de réseau. Elle fait ensuite
tirer la 0.21.2 à un projet jetable et exige qu'elle y soit signalée : sans cette
contre-épreuve, un outil qui ne sait plus rien signaler passerait pour un dépôt sain.
Enfin elle compare ce que la compilation a copié à ce que la restauration a résolu, paquet
par paquet : le premier essai l'a montré utile, sur un binaire compilé avant la
correction qui embarquait encore la 0.21.2.

*Choisi sans certitude.* La recette dépend du réseau, ou d'un cache NuGet récent :
c'est la nature d'une base qui change sans nous, et la recette tombera le jour où une
avancée nouvelle touchera un paquet d'aujourd'hui. C'est ce qu'on veut apprendre, mais
c'est aussi la première recette dont le rouge peut ne rien devoir au dépôt.

*Tranché par le propriétaire le 11 octobre 2026* : la recette sort de `verifier.sh` et
de `Scripts/recettes/`. Elle se lance à part, sous le nom `Scripts/verifier-dependances.sh`,
avant une livraison et chaque fois qu'on touche aux paquets ; `bilan.sh` la reconnaît
pour Q12. Le juge perd du même coup son sabotage de Q12, qui ne passait que par elle :
la contre-épreuve du projet témoin en tient lieu, puisqu'elle la fait échouer sur la
version fautive à chaque passage.

*Laissé tel quel.* La référence directe est une ligne qu'aucun code n'appelle, et qu'on
serait tenté de retirer au premier ménage. Elle pourra partir quand une version
d'Avalonia demandera d'elle-même une version corrigée ; la recette dira si elle part
trop tôt.

### Q13 — Couper la ligne au même mot à l'écran et au PDF

*Trouvé le 11 octobre 2026*, en vérifiant ce que disaient les en-têtes de
`verifier.sh` et de `comparer-rendus.sh`. Ils annoncent que la comparaison écran ↔ PDF
échoue sur une page de texte « parce que l'interligne n'est pas appliqué à l'export ».
R4 a corrigé l'interligne, et l'écart sur la page 1 du document d'exemple est passé de
8 % à 4,87 %. Il reste pourtant au-dessus du seuil de 3 %, et pas seulement à cause du
lissage des glyphes. **Dans la première colonne, l'écran et le PDF ne coupent pas la
même ligne au même mot** : les deux versions se superposent sur l'image des écarts.

R1 a fait partager aux deux rendus le *code* qui coupe les lignes (`Compositeur`), mais
pas la *mesure* des mots : l'écran interroge Avalonia (`MetriquesAvalonia`), l'export
interroge PDFsharp (`MetriquesPdf`). Un mot qui tient au centième de point d'un côté
passe à la ligne de l'autre, et tout le reste du bloc se décale. C'est le défaut que la
relecture du 6 septembre avait relevé pour les colonnes, et il vaut aussi pour les
lignes. Le document d'exemple le produit tel quel.

Le remède sûr est le seuil D : une seule source de mesure. Il existe un remède d'attente,
moins cher, que ce poste doit éprouver : faire **décider la coupure sur la plus large
des deux mesures**. Les deux rendus coupent alors aux mêmes mots, et chacun dessine
ensuite avec sa propre mesure, en justifiant ou non à l'intérieur de la ligne. Le
prix : un mot qui tiendrait de justesse passe à la ligne des deux côtés.

*Recette* :
- test pur : sur les blocs de `Samples/exemple-bulletin.mypub` et des deux documents
  ATR, composer avec les deux métriques donne les mêmes coupures, ligne par ligne ;
- `comparer-rendus.sh` passe sous le seuil sur les deux pages du document d'exemple.
  Quand c'est fait, `verifier.sh` peut l'appeler en bloc.

Effort : moyen. **Si la voie d'attente ne suffit pas, le poste devient la question du
seuil D**, et l'on s'arrête pour la poser au propriétaire du projet.

*Éprouvé le 12 octobre 2026, et arrêté une première fois : la voie d'attente ne suffit
pas seule.* La question est allée au propriétaire, qui a choisi de garder la voie
d'attente et de rendre la mesure de l'écran indépendante de l'histoire du processus. Ce
qui suit, jusqu'au paragraphe *Fait le*, est le récit de cet arrêt.

*Mesuré avant.* Chaque règle décidant seule, sur les blocs de texte des trois
documents, groupes et cases compris. Les chiffres de l'exemple dépendent de la face
qu'Avalonia donne à l'italique de DejaVu Sans, qui n'existe pas sur cette machine (voir
plus bas) ; ils valent dans deux états :

- italique résolu en romain — l'état de l'application ouverte sur l'exemple : **1 bloc
  et 2 lignes sur 57** coupent autrement, la première colonne, où l'écran garde
  « jamais » à 231,11 pt d'une ligne de 232 pt et où le PDF le renvoie. D'un morceau à
  l'autre, les deux mesures diffèrent au plus de 2,38 pt (0,15 % en moyenne, le PDF
  jamais plus étroit) ;
- italique résolu en gras — le gras chargé d'abord, ou les blocs composés dans l'ordre
  du document : **3 blocs et 10 lignes sur 59** — la première colonne, la légende (5
  lignes à l'écran contre 4 au PDF) et le rappel (3 contre 2). Les mesures diffèrent
  alors au plus de 10,14 pt (2,16 % en moyenne), l'écran plus large 70 fois, le PDF
  49 fois.

ATA 21 et les fiches ATR ne bougent pas d'un état à l'autre : 0 ligne sur 79 et 0 sur
152, à 2,96 pt (0,47 %) et 2,29 pt (0,20 %) d'écart au plus. Sur l'exemple et les
fiches, dans l'état romain, l'écart est le crénage : l'écran privé de crénage (essai,
non gardé) y mesure chaque morceau comme le PDF, à 0,0001 pt près. Sur ATA 21, un écart
de 2,96 pt reste sans crénage. La hauteur naturelle, qui décide du passage de colonne,
est la même des deux côtés sur les trois documents.

*La voie d'attente, écrite.* `IMetriques` gagne `LargeurDeCoupure` : chacun rend la plus
large de sa mesure et de celle de l'autre, et le compositeur ne décide plus que sur
elle, en dessinant à la sienne. L'export reçoit la règle de l'écran de l'application ;
`--export` monte d'Avalonia ce qu'il faut pour mesurer, sans fenêtre. Dans un même
processus, les trois documents coupent alors aux mêmes mots, ligne pour ligne.

*Pourquoi elle ne suffit pas.* La règle de l'écran n'est pas une fonction du texte et du
style. DejaVu Sans n'a pas d'italique sur cette machine ; Avalonia 11.3.9 lui donne la
face qu'il a déjà chargée : le gras si le gras est venu le premier, le romain sinon. La
même ligne de la légende mesure 262,83 pt ou 233,96 pt selon l'histoire du processus.
La voie d'attente fait entrer cette mesure dans la coupure du PDF, qui n'en dépendait
pas. Charger les quatre faces d'une famille dans un ordre fixe, à sa première mesure,
donne le romain à l'export, mais pas à un processus qui a déjà composé autre chose : après une
autre famille et les deux documents ATR, l'italique y repasse au gras. La confrontation
de bout en bout — le PDF du vrai binaire, lu mot par mot par `pdftotext -bbox`, contre
les lignes de l'écran — tombe ainsi, aux trois passages, sur la légende et le rappel.

*Ce que `comparer-rendus.sh` mesure vraiment.* Pas la coupure. Page 1 et page 2 de
l'exemple : 4,87 % et 3,72 % avant ; 4,78 % et 3,72 % avec la voie d'attente ; 4,80 %
et 3,91 % avec, en plus, l'écran privé de crénage, donc des morceaux de même largeur ;
0,37 % et 0,03 % sans aucun texte. Deux rastériseurs du **même** PDF, `pdftoppm` et
`pdftocairo`, diffèrent déjà de 2,36 % et 1,77 % ; l'écran et `pdftocairo`, de 4,28 % et
3,35 %. L'excédent est le dessin des glyphes. Le seuil de 3 % n'est pas à la portée
d'une page de texte, coupée juste ou non, et les en-têtes de `verifier.sh` et de
`comparer-rendus.sh` attribuaient l'écart à la coupure.

*La question, et ce que le propriétaire a tranché.* Trois voies étaient posées :
garder la voie d'attente et rendre la mesure de l'écran indépendante de l'histoire, par
une collection de polices propre au projet ; franchir le seuil D pour la seule coupure ;
ou laisser la dette. Le propriétaire a choisi la première, et demandé un outil de
comparaison mot à mot pour les pages de texte, `comparer-rendus.sh` restant celui des
formes et des images.

*Fait le 12 octobre 2026.* La voie (a), entière.

- **Les polices de l'écran.** Le texte des documents ne passe plus par la résolution
  d'Avalonia mais par `PolicesDuDocument`, une collection propre au projet : pour une
  famille, une graisse et une pente, elle demande à fontconfig le fichier que l'export
  emploie — le même appel, le même cache —, le charge une fois avec les simulations que
  fontconfig décide (`%{embolden}`, `%{matrix}`), et le garde. Rien n'y dépend de ce que
  le processus a chargé avant. `FontconfigFontResolver` rend les mêmes drapeaux à
  PDFsharp : l'italique absent de DejaVu Sans se penche désormais au PDF comme à
  l'écran, et la trouvaille laissée lors de l'arrêt (« sort droit au PDF ») est close.
  Le chrome garde les polices du système. L'application, `--export`, le banc et l'hôte
  des tests montent la collection par le même appel (`AvecLesPolicesDeMyPublisher`).
- **La coupure.** `IMetriques.LargeurDeCoupure`, comme dans la voie d'attente : chaque
  règle rend la plus large de sa mesure et de celle de l'autre, la décision ne lit
  qu'elle, le dessin garde la sienne ; la tabulation compte son taquet sur chacun des
  deux cumuls. La règle du PDF sort de `PdfExporter` (`MetriquesPdf`) et se consulte
  hors page ; un test vérifie qu'elle y mesure au bit près comme sur la page.
- **L'outil.** `Scripts/comparer-mots.sh` : le banc relève, par `TexteCompose.Poser`
  — la fonction même que le rendu appelle —, chaque ligne de l'écran mot par mot, et les
  mots que `pdftotext -bbox` lit dans le PDF du vrai binaire s'y rangent ; il compte les
  lignes coupées autrement, et vérifie la place du premier mot à 1 pt près (la mise en
  page d'Avalonia arrondit la place d'un bloc au pixel). Les en-têtes de `verifier.sh` et
  de `comparer-rendus.sh` disent désormais ce que chacun mesure.
- **La recette**, `Q13-coupure.sh` : 7 tests purs, 4 d'interface, puis `comparer-mots.sh`
  sur les deux pages de l'exemple et sur les deux documents ATR, trois fois — le banc
  charge les faces dans trois ordres —, à zéro ligne coupée autrement ; puis la
  contre-épreuve, l'écran coupant à sa seule mesure, qui doit tomber sur la première
  colonne de l'exemple. Deux sabotages en fin de juge : la décision qui relit la mesure
  du dessin, et l'écran qui contourne la collection.

*Mesuré.* Avant, refait en contournant la collection, par le même calcul que les
chiffres de l'arrêt (chaque règle décidant seule, blocs de l'exemple) : italique résolu
en **romain**, **2 lignes sur 57** coupent autrement, à 2,38 pt d'écart au plus ;
italique résolu en **gras**, **10 lignes sur 59** (3 blocs), à **10,14 pt** au plus et
2,16 % en moyenne. Après, que le gras soit chargé d'abord ou non : chaque règle décidant
seule, 2 lignes sur 57, à 2,38 pt et 0,15 % — l'écart n'est plus que le crénage, et
l'italique ne bouge plus d'un processus à l'autre ; sous la plus large des deux mesures,
**0 ligne sur 57**, et 0 sur 79 (ATA 21), 0 sur 152 (fiches). De bout en bout, par
`comparer-mots.sh`, dans les trois ordres : **0 ligne coupée autrement** sur 53 lignes
dessinées de l'exemple, 72 d'ATA 21 et 152 des fiches ; premier mot à 0,52 pt au plus.
L'italique de la légende est penché des deux côtés, à l'œil sur les deux rendus. Pour
mémoire, `comparer-rendus.sh` donne 4,52 % et 3,67 % sur les pages 1 et 2 de l'exemple
(4,87 % et 3,72 % avant) : toujours au-dessus de 3 %, pour la raison écrite plus haut.

*Choisi sans certitude.*
- « Un ordre fixe » est pris au sens du résultat : chaque variante est résolue seule, à
  sa première demande, sur le fichier de fontconfig, si bien que l'ordre n'y entre plus.
  Charger les quatre faces d'une famille dans un ordre fixe avait été essayé lors de
  l'arrêt, et ne tenait pas après d'autres familles.
- Avalonia 11.3.9 n'offre pas de porte publique pour charger des octets avec des
  simulations choisies : `PolicesDuDocument` appelle par réflexion
  `IFontManagerImpl.TryCreateGlyphTypeface(Stream, FontSimulations, …)`. Une montée de
  version qui la retirerait lève une exception à la première mesure, et les tests de Q13
  tombent ; rien ne retombe en silence sur l'ancienne résolution.
- La règle de l'écran consulte, à chaque rendu d'un bloc, une règle du PDF hors page
  neuve (un contexte de mesure PDFsharp) ; le coût n'a pas été mesuré.
- La tolérance de 1 pt de `comparer-mots.sh` sur la place du premier mot.

*Trouvé et laissé tel quel.*
- La règle d'Avalonia d'avant Q13 est plus précise que ce que disait l'arrêt. Sondé sur
  DejaVu Serif : l'italique absent sort en gras penché seulement quand le gras est la
  seule face de la famille déjà chargée ; dès que le romain l'est — avant ou après le
  gras —, ou que rien ne l'est, il sort en romain penché. Le premier ordre « gras » du
  banc (gras, gras italique, romain, italique) rendait donc le romain, et les trois
  ordres n'en faisaient que deux ; il a été repris.
- `comparer-mots.sh` signale à part les lignes dont un symbole diffère : 5 sur ATA 21,
  87 sur les fiches. Ce ne sont pas des coupures : les lettres de ces lignes sont les
  mêmes des deux côtés, seul un symbole manque au PDF (« ≤ » et « → » sur ATA 21, le
  caractère de contrôle U+0084 sur les fiches). La cause n'a pas été cherchée.
- Les blocs tournés ne se comparent pas mot à mot (1 sur l'exemple, 2 sur ATA 21) :
  `pdftotext` ne lit pas leurs lignes comme des lignes. L'outil les nomme.

*Repris le 12 octobre 2026, après relecture.* La relecture adversariale a jugé le lot
« fusionnable après corrections » : quatre défauts, et la fusion de `main` à faire.

- **La fusion.** `main` est entré dans la branche (Q9, B2, C5b et Q20, Q7, Q14, Q23).
  L'export garde la charte de C5b et reçoit en plus la règle de l'écran. Le champ de
  saisie ne passe plus par `Conv.Font` mais par le convertisseur `PoliceDuJeu` de C5b,
  qui faisait `new FontFamily(...)` : il passe désormais par `PolicesDuDocument.Famille`,
  sans quoi le changement de Q13 était perdu à la fusion. Les noms `scheme:` sont
  résolus par `Charte.StyleCommun` et `Charte.Fragments` avant `Compositeur.Composer`,
  à l'écran (`ControleDeBloc.Mesurer`) comme à l'export (`DrawText`) : la règle à mesurer
  reçoit un nom de police, jamais une référence au jeu. Un bloc de la recette tient sa
  police du jeu (« scheme:heading », Noto Serif) et coupe comme le PDF. La **vignette
  des modèles** (`TemplatePreview`, Q9) compose par `MetriquesAvalonia`, sur la largeur
  du modèle et avec le style résolu par la charte : elle coupe donc déjà sur la même
  règle que la page, et n'a pas été touchée. C'est lu dans le code : la recette de Q9
  reste verte, mais aucune mesure n'a confronté ses filets au PDF.
- **Trouvé à la fusion : un sabotage de C6 sans cible.** « L'écran ignore la marge du
  haut » visait `var ligneDeBase = bloc.PaddingTop + descente`, que la refonte de
  `TexteCompose` par le premier commit de Q13 avait réécrit. `--cibles` l'a vu. Il vise
  désormais `var haut = bloc.PaddingTop + descente + ligne.Haut;`, et il a été éprouvé
  à la main : la recette C6 tombe.
- **L'export pouvait ignorer la mesure de l'écran sans que rien ne tombe.** Remplacer la
  règle de l'écran par `null`, dans `--export` ou dans l'export de l'application,
  laissait `verifier.sh` vert : sur le corpus, la mesure du PDF est presque toujours la
  plus large. Sondé sur dix familles, quatre variantes, les mots de l'exemple et
  quelques paires : l'écran mesure plus large de plus d'un millième de point dans 370
  cas sur 11 200, au plus pour « f) » en Noto Serif (+0,9 pt à 10 pt, un crénage
  positif). La page 3 de `Samples/recette-coupure.mypub` porte donc quatre blocs en
  Noto Serif à 20 pt — romain, italique, gras, et romain par la police du jeu —, larges
  de la moitié du chemin entre les deux mesures de « Voir l'alinéa f) » : 134,88 pt au
  PDF seul, 136,68 pt à la plus large des deux, pour un bloc de 135,78 pt en romain.
  C'est éprouvé par les deux exports. Pour `--export`, `comparer-mots.sh` lit le PDF du
  vrai binaire. Pour l'export de l'application, un test d'interface appelle
  `ExportPdf` du modèle de vue et relit le PDF par `pdftotext`, avec une contre-épreuve :
  sans la mesure de l'écran, « f) » reste sur la première ligne. Les deux câblages
  sabotés à la main font tomber la recette.
- **La largeur disponible.** `Mesurer` composait sur `Bounds` — la taille du contrôle,
  que la mise en page arrondit au pixel SUPÉRIEUR : 151 pour 150,4, de 0,1 à 0,6 pt de
  plus sur les 52 blocs de la recette —, l'export sur celle du modèle. Il compose
  désormais sur `bloc.Width` et `bloc.Height` moins les marges, comme l'export. Sur les
  48 blocs du relecteur, larges de 150,4 à 173,9 pt au pas de 0,5, `comparer-mots.sh`
  passe de 19 lignes coupées autrement (5 blocs, tous en « ,4 ») à 0. Ces 48 blocs
  sont les pages 1 et 2 de `recette-coupure.mypub`. La phrase du README qui disait
  « l'écran et le PDF coupent aux mêmes mots » ne dit plus que ce qui est mesuré.
- **`comparer-mots.sh` jetait en silence un mot du PDF sans ligne à l'écran**, alors que
  son en-tête disait le contraire. Ce mot compte désormais comme écart, sauf s'il tombe
  dans un bloc non comparé. On en relève 0 sur l'exemple, ATA 21, les fiches et la
  recette. Avec le sabotage du relecteur — l'écran ne dessine plus la dernière ligne
  d'un bloc de plusieurs lignes —, on en relève 28 sur la page 1 de l'exemple, et ATA 21
  tombe aussi. Le même calcul attrape une page ou un bloc entier que l'écran ne relève pas ;
  ce cas-là n'a pas été joué.
- **La porte privée d'Avalonia.** `PolicesDuDocument` la cherche une fois par
  processus, au premier montage des polices, et reçoit le gestionnaire de la
  plate-forme par `Initialize` au lieu de lire `FontManager.PlatformImpl` par
  réflexion. Sans elle, la collection n'est pas montée et `Famille` rend la famille du
  système, c'est-à-dire la résolution d'Avalonia d'avant Q13. Un seul avertissement
  part alors sur la sortie d'erreur. `--export` échoue, avec le code 1 et sans écrire de
  PDF, et l'export de l'application le signale dans la barre d'état. Un appel qui lève
  malgré la porte retombe, pour cette face seule, sur la résolution d'Avalonia. La
  variable `MYPUBLISHER_SANS_PORTE_PRIVEE=1` fait chercher une méthode qui n'existe pas,
  si bien que c'est le vrai chemin de l'échec qui est parcouru. Deux tests l'éprouvent
  dans de vrais processus : `--export` sur l'exemple sort en 1 sans PDF, et le banc
  ouvre la vraie fenêtre, relève ses 53 lignes et avertit une fois. Ceci remplace la
  phrase de *Choisi sans certitude* selon laquelle une version sans la porte « lève une
  exception à la première mesure ».

La recette joue désormais 7 tests purs et 7 tests d'interface, dont 3 nouveaux. Elle
lance `comparer-mots.sh` sur l'exemple, ATA 21 et les fiches dans les trois ordres, puis
sur `recette-coupure.mypub` (400 lignes, 3 024 mots, 52 blocs, 0 ligne coupée
autrement), et finit par la contre-épreuve. Six sabotages s'ajoutent en fin de juge :
les deux exports sans la mesure de l'écran, la largeur arrondie, la dernière ligne non
dessinée, le refus de `--export` sans la porte, la collection adressée sans la porte.

*Choisi sans certitude.*
- Sans la porte, `--export` refuse plutôt que de retomber sur `ecran: null` : un PDF de
  référence coupé autrement que l'écran, signalé par un simple avertissement, est ce que
  Q13 combat. Le prix : plus d'export scripté tant que la porte n'est pas remplacée.
- La variable `MYPUBLISHER_SANS_PORTE_PRIVEE` vit dans le code de l'application et ne
  sert qu'à l'épreuve.
- Les largeurs de la page 3 sont figées pour les fichiers Noto Serif de cette machine.
  Avec une autre version de la police, les blocs pourraient ne plus être à la limite. Le
  test d'interface le dirait (l'écran doit renvoyer « f) », le PDF seul le garder) ;
  `comparer-mots.sh`, lui, resterait vert.

*Trouvé et laissé tel quel.*
- Le sabotage de `--export` sans la mesure de l'écran est attrapé dans la recette dès
  l'étape 2, par le test sans la porte : sans l'appel à `MesureDeLEcran`, le refus ne
  se produit plus. Éprouvée seule à la main, l'étape 4 l'attrape aussi : 24 lignes
  coupées autrement, toutes sur la page 3.

*Complété le 12 octobre 2026, après la seconde relecture* (« fusionnable après
corrections », trois points ; les deux questions du lot ont été tranchées : `--export`
qui refuse sans la porte privée est approuvé, la variable `MYPUBLISHER_SANS_PORTE_PRIVEE`
dans l'application est acceptée).

- **La hauteur n'était gardée par rien.** Composer sur `Bounds.Height` au lieu de la
  hauteur du modèle passait tout `verifier.sh` : tous les blocs de la recette avaient une
  hauteur entière. Le défaut est réel : un bloc à deux colonnes de 58,3 pt composait sur
  59 à l'écran, et la 7e ligne restait en colonne 1 quand le PDF la passait en
  colonne 2. `recette-coupure.mypub` porte désormais une page 4 : 22 blocs à deux
  colonnes, de 58,05 à 58,65 pt de haut, tirés du document du relecteur, dont on a
  retiré les deux blocs de hauteur entière. Leur texte s'arrête après « puis du PDF. »
  (35 mots) : entier, il débordait de 42 à 51 pt, et un document livré ne déborde pas
  (recette A2-A7, `DebordementTests`). Raccourci, il compose 238 lignes sur les 22
  blocs, et le sabotage de la hauteur y fait encore tomber la recette. Sain, `comparer-mots.sh` y relève 0 ligne
  coupée autrement (638 lignes, 3 794 mots, 74 blocs sur les quatre pages). L'étape 4
  de la recette exige désormais 74 blocs. Un septième sabotage vise la hauteur, éprouvé
  à la main : la recette tombe à l'étape 4, sur 106 lignes coupées autrement et 90 mots
  du PDF sans ligne à l'écran.
- **La page 3 dépend de Noto Serif.** Le test de l'export de l'application vérifie
  d'abord, bloc par bloc, que la police de cette machine départage encore les deux
  mesures, et sinon le dit (« recalculer les largeurs de la page 3 »). La vérification
  suggérée — « Voir l'alinéa f) » mesurée d'un tenant, `pdf < largeur utile < écran` —
  ne tient pas : la ligne entière mesure 135,48 pt à l'écran, sous les 135,78 du bloc.
  C'est le maximum pris MOT PAR MOT qui renvoie « f) ». La vérification cumule donc les
  mots comme le compositeur : 134,88 pt au PDF seul < 135,78 < 136,68 à la règle de
  coupure, et « f) » plus large à l'écran (16,10 pt) qu'au PDF (14,30). Avec un bloc
  porté à 137 pt, elle tombe avec son message.
- **L'arrondi était mal décrit.** Le commentaire de `Mesurer` dit maintenant « 151 pour
  150,4 ». La crainte d'un rognage, écrite plus haut sous *Choisi sans certitude*, est
  remplacée par la mesure du relecteur : 0 bloc rogné sur 52, et l'encre la plus proche
  du bord à 0,42 pt (B22, page 1).
- Le plantage « Aborted (core dumped) » du premier passage de `verifier.sh` n'est pas
  reproduit dans le code des polices. Le relecteur l'attribue à l'erreur connue de
  dotnet sur cette machine ; il sera traité sur `main`.

### Q14 — Un double-clic ne déplace pas le bloc

*Trouvé le 11 octobre 2026 par Q1.* Un double-clic sur un bloc importé, pour y entrer
en saisie, passe par le geste de déplacement (`PageCanvas.ApplyMove`) sans que le
pointeur ait bougé : l'ordonnée est réécrite à son dernier chiffre près
(107.15039999999999 devient 107.1504), le document passe pour modifié et un pas
d'annulation est empilé. Rien ne se voit, mais « annuler » ne défait plus ce qu'on
attend, et la fermeture demande d'enregistrer un document qu'on n'a pas touché.

*Recette* : sur ATA 21 importé, un double-clic synthétisé au centre d'un bloc ouvre la
saisie ; le JSON du bloc est identique au caractère près, `CanUndo` reste faux et le
document n'est pas marqué modifié. Un glissé de 1 pt, lui, empile toujours son pas.
Effort : faible.

*Fait le 12 octobre 2026.* **L'énoncé ne désignait qu'une des deux causes, et pas celle
du chiffre qu'il cite.** Mesuré sur le code d'avant le poste, en traçant qui écrit
l'ordonnée du bloc pendant un double-clic synthétisé sans mouvement : le
107.15039999999999 devenu 107.1504 est écrit par la liaison du champ « Y (mm) » du
panneau, au moment où le premier clic sélectionne le bloc (`NotifySelection`), et non
par `PageCanvas.ApplyMove`. Ce double-clic-là n'empilait **aucun** pas d'annulation et ne
marquait pas le document modifié : la réécriture tombe avant que le geste n'ouvre sa
transaction. C'est l'aller-retour que P3 disait « sans conséquence » ; il en avait une, le
JSON. Le pas d'annulation, lui, vient bien de `ApplyMove`, mais seulement quand le
pointeur signale un mouvement **nul** entre la pression et le relâchement :
l'aimantation attire alors vers un repère voisin un bloc qu'on n'a fait que cliquer.
Mesuré en pressant, bougeant de zéro et relâchant au centre de chacun des huit blocs
qu'un clic atteint sur la page 1 d'ATA 21, chacun sélectionné d'abord : trois sont
réécrits et empilent un pas.

Deux corrections, donc. **Le panneau** : les deux convertisseurs des champs numériques,
`PointsToMillimetres` et `DoubleToDecimal`, se souviennent du nombre qu'ils ont donné à
chaque champ et de la valeur exacte dont il venait, et la rendent quand ce nombre leur
revient (`Affiches`, dans `UnitConverters.cs`). Le second était touché aussi : il arrondit
à quatre décimales pour l'affichage, et l'arrondi remontait — sélectionner un bloc tourné
de 12.3456789° le laissait à 12.3457°, mesuré. **Le geste** : un pointeur resté à son
point de prise remet chaque bloc à sa position de départ et n'aimante rien. La garde est
une égalité stricte et non un seuil : un glissé d'un point déplace toujours d'un point.

Trois tests sous `poste=Q14`, dans `DoubleClicTests`, et la recette
`Scripts/recettes/Q14-double-clic.sh`. Le premier double-clique au centre de chacun des
sept blocs de texte qu'un clic atteint sur ATA 21 importé, sans sélection préalable,
chaque clic portant un mouvement nul : la saisie s'ouvre, le document est identique au
caractère près, `CanUndo` et `IsDirty` restent faux, et refermer la saisie n'y change
rien. Le deuxième fait de même sur un bloc tourné de 12.3456789° posé à 100 pt. Le
troisième glisse d'un point, Alt tenu pour écarter l'aimantation : le bloc descend d'un
point, un pas est empilé, l'annulation rend le document d'ouverture. Trois sabotages
au juge, un par correction, chacun éprouvé à la main sur la recette : sans la mémoire en
millimètres, les trois tests tombent ; sans celle de la rotation, le deuxième ; sans la
garde, le premier. Un seuil de deux points à la place de la garde fait tomber le
troisième.

Le test d'annulation de Q1 reprend son état de référence à l'ouverture du document, avant
toute sélection ; il tombe désormais lui aussi quand on retire la mémoire en millimètres.
L'import d'ATA 21 des tests est passé de `FrappeTests` à `Rendu`, que les deux fichiers
partagent.

**Une retombée hors du poste, à déclarer.** `Scripts/comparer-rendus.sh` refusait un écart
nul entre l'écran et le PDF, comme symptôme d'une image comparée à elle-même ou de deux
images vides. Le panneau ne réécrivant plus la largeur de la page — 595,28 pt revenait
595.2800000000013 — ni la géométrie du bloc, le document tourné de la recette A1 sort au
pixel près : 0 pixel différent sur 48 000 pixels d'encre, contre 0,05 % avant le poste.
La recette A1 tombait donc pour la seule raison que l'accord était parfait. Le script
vérifie maintenant les deux soupçons eux-mêmes — deux fichiers identiques octet pour
octet, ou un rendu sans encre — et non plus leur symptôme ; une page sans bloc le fait
tomber, mesuré.

*Choisi sans certitude.* La mémoire est partagée par tous les champs d'un même
convertisseur et garde jusqu'à 256 nombres. J'en ai mal décrit le risque : il n'est pas
que deux valeurs du modèle affichées sous le même décimal se confondent, mais qu'une
saisie soit prise pour un nombre affiché plus tôt, et reçoive la valeur exacte dont ce
nombre était tiré. La relecture l'a provoqué ; la reprise ci-dessous le corrige. Je n'ai
pas vérifié qu'une vraie plate-forme livre un mouvement nul entre les deux clics d'un
double-clic : le banc l'émet parce que c'est ce qui passait par l'aimantation. Q1
attribuait le défaut à `ApplyMove` ; je n'ai pas retrouvé par quel geste il l'y avait vu
passer.

*Trouvé et laissé tel quel.* Il n'y a pas de seuil de glissement sur la page : un
tremblement d'un pixel pendant un clic est un déplacement, qui passe par l'aimantation et
empile un pas. La garde ne couvre que le mouvement nul. Un seuil réglerait cela, mais
mangerait le glissé d'un point que la recette exige ; c'est une décision, pas une
réparation. Et l'on ne sait toujours pas pourquoi le champ renvoie au modèle une valeur
qu'il vient de recevoir : la correction rend ce renvoi inoffensif sans l'expliquer.

*Repris le 12 octobre 2026, après relecture.* Une relecture adversariale du lot a trouvé
un trou dans le filet et un défaut dans la mémoire ; les deux sont corrigés, avec un test
et un sabotage chacun.

**Le trou du filet.** La garde d'`ApplyMove` remet chaque bloc à son départ
(`frame.X = startX;`, `frame.Y = startY;`). Neutraliser ces deux lignes passait toute la
vérification, alors qu'un geste qui part de (+40, +30), Alt tenu, puis revient exactement
au point de prise laissait le bloc en (140, 130) au lieu de (100, 100). Le code était
juste ; aucun test ne le demandait. Le quatrième test de `DoubleClicTests`,
`Revenir_au_point_de_prise_dans_le_meme_geste_ramene_le_bloc`, fait ce geste, et le juge
porte le sabotage de `frame.X = startX;` : éprouvé à la main, la recette Q14 tombe sur ce
seul test.

**La mémoire des affichages.** Une valeur tapée recevait la valeur exacte qu'un autre bloc
avait affichée : A tourné de 30.00001°, B à 0 ; on clique A, puis B, on tape « 30 » dans le
champ de rotation, et B recevait 30.00001. `Affiches` n'a plus qu'une case : `Retenir` la
remplace, `Retrouver` ne reconnaît que le nombre égal à celui qu'elle porte et la vide dans
tous les cas. La capacité de 256 disparaît. Une case suffit parce que le renvoi d'un champ
suit son affichage aussitôt, dans le même appel (`BindingExpression.OnTargetPropertyChanged`
→ `WriteTargetValueToSource`, trace de la relecture). Le cinquième test,
`Une_valeur_tapee_ne_prend_pas_celle_quun_autre_bloc_a_affichee`, joue ce scénario au
clavier ; il tombe sur l'ancienne mémoire, et c'est elle que le sabotage remet. Les trois
sabotages d'avant, rejoués à la main sur les cinq tests, sont toujours attrapés : sans la
mémoire en millimètres, quatre tests tombent ; sans celle de la rotation, deux ; sans la
garde, un. Le commentaire d'`Affiches` dit maintenant le risque tel qu'il est.

Ce qui reste, mesuré mais non provoqué : un affichage qui ne change pas la valeur du champ
n'est suivi d'aucun renvoi, et la case reste pleine. Une sonde l'a vue porter « 0 → 0 »
après la sélection d'un bloc. Une frappe faite alors dans un autre champ du même
convertisseur, exactement égale à ce nombre, en reprendrait la valeur exacte ; cela
n'altère le document que si ce nombre était un arrondi. Je n'ai pas trouvé de geste qui
le fasse : taper 30 dans l'épaisseur d'un bloc tourné de 30.00002°, après en avoir
sélectionné un tourné de 30.00001°, donne une épaisseur de 30.

**Hors du poste, `Scripts/comparer-rendus.sh`.** Le commit 2e02036 acceptait la notation
scientifique pour l'encre seulement. Le compte des pixels différents l'accepte aussi, et
le total se calcule en entier, de la largeur et de la hauteur : `%[fx:int(w*h)]` s'écrit
2.00684e+06 pour une image de 1191 × 1685, mesuré avec l'ImageMagick des outils ; la page
de l'affiche donne maintenant « 4011288 pixels comparés ». Ma première version lisait la
largeur et la hauteur par un `read` sans saut de ligne final : `read` sortait en 1, `set -e`
arrêtait le script sans un mot, et neuf recettes tombaient. `verifier.sh` l'a attrapé ; le
commit suivant le corrige.

*Trouvé et laissé tel quel*, par la relecture, et ouvert en postes Q à part : il n'y a pas
de seuil de glissement, si bien qu'un tremblement d'un pixel près d'une marge fait sauter
le bloc de 3 pt par l'aimantation ; et un clic sans mouvement sur une poignée redimensionne
le bloc (100 → 96 pt). Et, trouvé pendant cette reprise : les deux tests de `DoubleClicTests`
qui double-cliquent dépendent de l'horloge. Sous charge, les deux clics s'écartent au-delà du
délai d'un double-clic, la saisie ne s'ouvre pas, et le test tombe sur « le double-clic n'a
pas ouvert la saisie ». Mesuré en lançant six fois en parallèle les tests `poste=Q14`, quatre
fois de suite : 5 séries sur 24 tombent, et 10 sur 24 avec l'ancienne mémoire — le défaut
précède la reprise. Une fois aussi dans un `verifier.sh` complet, alors que d'autres lots
compilaient ; relancé seul, il passe. Le remède toucherait le banc commun (le délai du
double-clic de la plate-forme sans tête), hors de ce poste.

### Q15 — Une image modifiée sur le disque se relit

*Trouvé le 11 octobre 2026 par Q3*, qui a choisi d'oublier les échecs du cache à la
réouverture et de garder ses réussites. Une image retouchée sur le disque pendant la
session reste donc périmée à l'écran, même après réouverture, alors que l'export PDF
relit le fichier à chaque fois : c'est un écart écran ↔ PDF. Remède probable : la clé
du cache porte la date de modification du fichier en plus de son chemin.

*Recette* : une image posée, puis réécrite sur le disque d'une autre couleur ; après
réouverture, l'écran et le PDF montrent la nouvelle couleur. Sans retouche, le cache
rend la même instance (l'épingle de Q3 tient). Effort : faible.

*Fait le 12 octobre 2026.* Chaque entrée de `BitmapCache` garde l'empreinte de son
fichier, sa date de modification et sa taille, prise **avant** la lecture : un fichier
réécrit pendant qu'on le lit sera relu à l'ouverture suivante, plutôt que l'inverse.
`BitmapCache.OublierLesRetouchees` retire les images dont le fichier n'a plus la même
empreinte ; `Adopt` l'appelle juste après `OublierLesEchecs`, donc à l'ouverture d'un
fichier comme au départ d'un modèle. Une image intacte reste la même instance.
L'annulation ne relit rien, comme pour Q3.

**La piste du plan n'a pas été suivie à la lettre**, et voici pourquoi. Mettre la date
dans la **clé** obligerait à interroger le disque à chaque lecture du cache, donc à
chaque notification de liaison, donc à chaque image d'un glissé, et sur un partage
réseau pour les documents qui y vivent. Les entrées périmées s'y accumuleraient en plus
sous leurs anciennes clés. L'empreinte vit donc dans l'entrée, et ne se compare qu'à
l'ouverture. La taille s'y ajoute parce que la date seule laisse passer une retouche
faite dans le même pas d'horloge que la lecture.

Mesuré. Sans l'appel dans `Adopt` (le premier sabotage), les deux tests tombent sur
« l'écran ne montre pas l'image retouchée (0 pixels) » : le défaut est reproduit. Avec
lui, l'écran montre le cyan sur toute l'image du bloc et plus un pixel magenta, et le
PDF exporté au même instant porte une image cyan, relue par PDFsharp dans le fichier.
Les deux PNG de 8 pixels, magenta puis cyan, écrits par Avalonia ont la même taille de
fichier : le test le vérifie, puisque c'est la date seule qui doit trahir ce cas.

Trois tests d'interface sous `poste=Q15`, dans `ImagesTests`. Le premier fait la retouche
ordinaire : même taille, date plus récente. Il épingle aussi l'instance rendue sans
retouche (Q3) et l'annulation qui ne relit pas. Le second fait la retouche qui garde la
date, avec une autre taille. Chacun ne trahit ainsi qu'une moitié de l'empreinte, et
les deux sabotages qui en retirent une moitié le prouvent : la taille oubliée ne fait
tomber que le second, la date oubliée que le premier. Le troisième test, ajouté après la
relecture, éprouve l'ordre. Un lecteur injecté dans le cache (`BitmapCache.Lecteur`,
interne, visible du seul banc d'interface) réécrit le fichier en cyan juste après
l'avoir lu en magenta. L'ouverture suivante doit montrer le cyan. L'empreinte prise
après la lecture le fait tomber, et lui seul : sans ce test, rien ne gardait l'ordre.
*Recette* : `Scripts/recettes/Q15-image-retouchee.sh`. Quatre sabotages au juge :
l'appel retiré, la taille oubliée, la date oubliée, l'empreinte prise après la
lecture.

*Choisi sans certitude.* Le test pose lui-même les dates. Quand l'horloge des fichiers
n'a pas avancé entre deux écritures rapprochées, il avance la date d'une seconde ; pour
le second cas, il remet la date d'avant. Le coût d'une date lue à chaque lecture du cache
n'a pas été mesuré : c'est une prudence, pas une mesure. Une image dont le fichier a
disparu change d'empreinte, est oubliée, et le bloc se vide à la réouverture, comme le
PDF le dit déjà (« Image introuvable »). Les images oubliées ne sont pas libérées
(`Dispose`) : un contrôle peut encore les afficher jusqu'à sa prochaine liaison. C'est
ce que fait déjà `Invalidate`.

*Trouvé et laissé tel quel.* Une image retouchée pendant que le document reste ouvert
n'est relue qu'à la réouverture, ou en la réinsérant ; d'ici là, l'export montre déjà la
nouvelle. L'écart écran ↔ PDF demeure donc dans cet intervalle ; le README le dit.
Mesuré en écrivant le test : après une annulation, la première image que le banc capture
à la réouverture (`CaptureRenderedFrame`) est en retard d'un tour, la page sans son
image, ni magenta ni cyan ; la capture suivante compte 32 000 pixels cyan, soit
200 × 160. Le test jette la première capture et l'écrit. Ce retard tient au banc ou au
rendu, et n'a pas été cherché plus loin.

### Q16 — Le pointillé d'un bloc image obéit aux repères

*Trouvé le 11 octobre 2026 par la recette de C1.* Le pointillé gris d'un bloc image,
« repère visible tant qu'aucune image n'est chargée » selon `PageCanvas.axaml`, est en
réalité toujours dessiné. Une image cadrée sans déformation le laisse voir sur le bord
du bloc, et il n'obéit pas à l'interrupteur des repères : il entre dans toute
comparaison écran ↔ PDF (0,06 % de la page pour un bloc seul).

*Recette* : un bloc image chargé, rendu par le banc repères éteints, ne porte aucun
pixel de la couleur du pointillé ; un bloc image sans fichier le montre repères
allumés et le cache repères éteints. Effort : faible.

*Fait le 12 octobre 2026.* Le pointillé n'est plus dessiné que si les repères sont
allumés **et** qu'aucune image n'est affichée dans le bloc : sa visibilité est le « et »
de `ShowGuides` et d'une source d'image nulle, lue sur l'élément image du gabarit. Une
image introuvable sur le disque donne une source nulle : le bloc montre alors sa place,
comme un bloc sans chemin.

*Mesuré.* Trois tests d'interface sous `poste=Q16` et la recette
`Q16-pointille-image.sh`, qui les joue puis rend au banc une page portant un bloc
chargé et un bloc sans fichier. Pixels du gris du pointillé sur le pourtour : bloc
chargé, 0 repères éteints comme allumés ; bloc sans fichier, 616 repères allumés, 0
éteints. La comparaison de la page de C1, où le défaut a été trouvé, tombe à 0,04 %
avec Q4.

*Choisi sans certitude.* Une image chargée cache le pointillé même repères allumés :
c'est ce que dit le commentaire d'origine (« tant qu'aucune image n'est chargée »), et
le cadre du bloc reste visible à la sélection. Publisher n'a pas été consulté sur ce
point.

Deux sabotages au juge, un par condition : la source d'image retirée de la condition,
puis l'interrupteur des repères ; chacun fait tomber un test différent.

### Q17 — Le texte composé suit une collection remplacée

*Trouvé le 11 octobre 2026 par Q2.* `Spans` et `Paragraphs` ont un setter public.
Remplacer la collection d'un bloc affiché laisserait `ControleDeBloc` abonné à
l'ancienne et sourd à la nouvelle. Rien ne le fait aujourd'hui hors de `Clone()` et de
l'import, qui travaillent sur des blocs pas encore affichés : c'est une mine, pas un
défaut visible. Remède : écouter le changement de la propriété elle-même, ou fermer
le setter.

*Recette* : un contrôle affiché, dont on remplace les `Spans` du bloc, redessine au
style de la nouvelle collection ; l'ancienne n'a plus d'abonné. Effort : faible.

*Fait le 12 octobre 2026.* **Le setter est fermé** : `TextFrame.Spans`,
`TextFrame.Paragraphs` et `TableCell.Spans` n'ont plus qu'un accesseur `init`.
L'initialiseur et le sérialiseur les posent encore ; plus personne après. La troisième
n'était pas nommée par le plan, mais c'est la même mine : `CellFrames` prête la
collection de la case au bloc qu'il construit pour elle, et le contrôle qui dessine la
case s'y abonne.

**Pourquoi fermer plutôt qu'écouter.** Rien ne remplaçait ces collections hors de la
construction : la solution entière compile avec `init` sans autre retouche. Les seuls
écrivains de la propriété étaient l'initialiseur de `CellFrames` et le sérialiseur, qui
la pose sur l'objet qu'il est en train de construire, et l'annulation avec lui,
puisqu'elle relit un instantané. `Clone()` et l'import remplissent la collection d'un
bloc neuf, ils ne la remplacent pas. Écouter le remplacement aurait demandé trois choses :
- que le bloc annonce un changement de `Spans`, qu'il n'annonce pas aujourd'hui ;
- que `ControleDeBloc` garde l'ancienne collection pour s'en désabonner, l'événement ne
  la donnant pas ;
- que chaque observateur à venir (l'aperçu de B6, la vignette de C7) le refasse.

Fermer règle la question pour tous les observateurs à la fois, et au compilateur, pas à
la discipline. `TableFrame` a fait l'autre choix pour ses colonnes, lignes et cases
(`Adopter`), parce qu'il s'abonne lui-même à ses collections dès le constructeur, et que
le sérialiseur les remplace après. Ici, le bloc ne s'abonne à rien. Le prix : un code qui
voudrait tout remplacer videra puis remplira, une notification par élément, que le
contrôle entend déjà.

La recette du plan, un contrôle affiché dont on remplace les fragments, ne peut donc plus
se jouer : le geste ne compile plus. *Recette* : `Scripts/recettes/Q17-collections-fermees.sh`.
Elle compile une sonde contre le noyau construit, qui remplace les trois collections sur
des objets déjà construits ; elle exige trois erreurs CS8852, une par ligne. Une seconde
sonde compile ce qui doit rester permis : l'initialiseur, le prêt d'une case, le
remplissage sur place. Six tests sous `poste=Q17`, dans `CollectionsFermeesTests` :
- la marque `IsExternalInit` sur chaque collection observable de `TextFrame` et de
  `TableCell`, découvertes par réflexion, avec un témoin que la découverte trouve bien
  les trois ;
- la lecture d'un document, qui pose encore les collections ;
- la case, qui prête toujours la même instance.

Un sabotage au juge, l'accesseur des fragments rouvert : la sonde n'obtient plus que
deux refus, et le test de réflexion tombe.

*Trouvé et laissé tel quel.* La même mine existe ailleurs, hors du périmètre :
`Page.Frames`, `GroupFrame.Children`, `Document.Pages` et les deux listes de repères ont
un accesseur public en écriture, sans annonce de changement, alors que des
`ItemsControl` s'y lient (`CurrentPage.Frames`, `Children`, `Document.Pages`). Rien dans
le dépôt ne les remplace après la construction, d'après une recherche dans les sources.

### Q18 — Paquets marqués « Legacy »

*Trouvé le 11 octobre 2026 par Q12.* `dotnet list package --deprecated` signale
`Avalonia.Diagnostics` 11.3.9 (remplaçant proposé : `AvaloniaUI.DiagnosticsSupport`) et
xunit, xunit.assert et xunit.core 2.9.3 (remplaçants : xunit.v3). Aucun n'est
vulnérable. Passer à xunit.v3 touche les deux projets de tests et leur harnais
d'interface : à faire seul, et pas au milieu d'une salve.

*Recette* : `dotnet list package --deprecated` ne signale plus rien, et les deux
projets de tests comptent autant de tests qu'avant. Effort : moyen.

### Q19 — Les autres collections ne se remplacent plus

*Trouvé le 12 octobre 2026 par Q17*, qui a fermé `Spans` et `Paragraphs`. La même mine
existe ailleurs : `Page.Frames`, `GroupFrame.Children`, `Document.Pages` et les deux
listes de repères libres ont un setter public sans notification, alors que des
`ItemsControl` s'y lient. Aucune affectation n'existe aujourd'hui dans les sources hors
du sérialiseur : c'est une mine, pas un défaut visible. Remède : le même que Q17,
l'accesseur `init`.

*Recette* : une sonde compilée contre le noyau obtient un refus CS8852 par collection,
et l'aller-retour JSON des documents livrés reste identique. Effort : faible.

### Q20 — La liste des polices garde la police du bloc

*Trouvé le 12 octobre 2026 par la relecture de C5b.* La liste des polices du panneau ne
proposait que les deux entrées du jeu et les polices de la machine. Un bloc dont la
police n'y est pas — un `.pub` composé ailleurs, un fichier écrit à la main, une valeur
que la migration de C5b a gardée libre (`!scheme:heading`) — laissait la liste sans
sélection ; et en passant de ce bloc à un autre de même sorte puis en y revenant, la
liste renvoyait null au bloc. La police était perdue, et le
rendu levait ensuite `ArgumentNullException` en composant le texte. Le défaut abîme des
données sans aucun geste de l'utilisateur, sinon sélectionner.

*Recette* : sur la vraie fenêtre, sélectionner tour à tour un texte à police absente et
un texte à police présente, libre ou du jeu ; la police absente est intacte, aucun pas
d'annulation n'est empilé, la liste la montre, et la page se compose. Même chose d'un
tableau à l'autre. Choisir une police dans la liste l'écrit toujours. Effort : faible.

*Fait le 12 octobre 2026.* Deux garde-fous. La liste (`FontFamilies`) propose, entre
les deux entrées du jeu et les polices de la machine, celles que les textes et les
tableaux du document emploient — groupes compris — et que la machine n'a pas ; une
référence du jeu n'y est jamais comptée comme absente. Le libellé de ces entrées porte
« (absente) ». La liste se fait à chaque remplacement du document — ouverture, modèle,
document neuf, annulation — et ne change pas sous les gestes : choisir une autre police
laisse la police absente dans la liste, et l'on peut y revenir. Le second garde-fou est
la liaison elle-même (`JamaisNul`) : elle ne renvoie jamais null au bloc, puisqu'aucun
geste de l'utilisateur ne choisit null. Les polices de la machine sont lues une fois,
à la construction du modèle de vue, et exposées à part (`PolicesDeLaMachine`) pour le
libellé.

*Recette jouée* : `Scripts/recettes/Q20-police-absente.sh`, six tests d'interface sous
`poste=Q20` (`PolicesAbsentesTests`) : trois passages d'un texte à l'autre — police
inexistante contre DejaVu Sans, contre `scheme:body`, et `!scheme:heading` contre
`scheme:body` —, deux tableaux, un bloc posé après que la liste a été faite, et le
choix dans la liste. La recette compte les tests joués et échoue sous six. Les deux
garde-fous retirés ensemble, c'est-à-dire le code d'avant, les six tombent : trois sur
`ArgumentNullException`, deux sur une police changée, un sur une liste sans sélection.
Deux sabotages au juge, un par garde-fou, éprouvés à la main : sans les polices
absentes dans la liste, quatre tests tombent — les trois passages d'un texte à l'autre et
le choix dans la liste ; sans `JamaisNul`, le seul test du bloc venu après la
liste tombe. Le test des tableaux en prend deux et non un tableau et un texte : le
tableau a sa propre liste au panneau, et passer d'un tableau à un texte ne montrait pas
le défaut.

*Choisi sans certitude.* Une police est « absente » quand son nom ne figure pas, à
l'identique, parmi les familles qu'Avalonia lit sur la machine. L'export, lui, résout
par fontconfig (B5) : les deux peuvent diverger sur un alias, et le libellé ne promet
rien de l'export. La liste ne suit pas un bloc posé après coup — aucun geste ne le
fait aujourd'hui — : c'est alors la liaison seule qui garde la police, sans que la liste
la montre.

*Laissé tel quel.* La comparaison est exacte, casse comprise : une police écrite
« dejavu sans » serait proposée à part et libellée absente. Lu dans le code, non
mesuré. Les fragments de style ont leur police hors du panneau, et ne sont pas concernés.

### Q21 — Un tremblement ne déplace pas le bloc

*Trouvé le 12 octobre 2026 par la relecture de Q14.* Q14 a gardé le bloc d'un clic sans
mouvement, mais un pointeur réel tremble. Mesuré : une marge à 3 pt du bord gauche du
bloc, aimantation active ; un pixel de mouvement pendant le clic fait passer X de 59,69
à 56,69 — le bloc saute sur la marge, et un pas d'annulation est empilé. Q14 n'est donc
tenu que si le pointeur ne bouge pas du tout. La spécification de Q14 veut qu'un glissé
de 1 pt compte : le remède n'est pas un seuil sur le déplacement du bloc, mais un seuil
sur celui du **pointeur** (2 ou 3 px d'écran) avant que le geste ne devienne un
glissé — l'aimantation ne joue qu'au-delà.

*Recette* : au pointeur synthétisé, un clic qui bouge d'un pixel près d'une marge laisse
le bloc à sa place et n'empile rien ; un glissé de 1 pt, mené au-delà du seuil puis
ramené, déplace encore le bloc de 1 pt. Effort : faible.

*Repris le 25 octobre 2026, dans la phase N (salve Saisie 3, avec Q22).* Le seuil, 3 px d'écran, vaut pour
tout glissé : déplacement, poignées, rotation et extrémités de trait (`PageCanvas.Trait.cs`) ; N12
le reprend pour la sélection de texte, N17 pour Ctrl+glisser. Le test de Q14 « un glissé d'un
point » est mené au-delà du seuil puis ramené. Les cibles du juge `if (dx == 0 && dy == 0)` et
`frame.X = startX;` restent, une fois chacune. Dépend de N1 et N5.

### Q22 — Un clic sur une poignée ne redimensionne pas

*Trouvé le 12 octobre 2026 par la relecture de Q14.* La garde du mouvement nul de Q14
n'existe que dans `ApplyMove`. Mesuré : une pression à 4 px du coin sud-est, sans
mouvement, fait passer le bloc de 100 à 96 pt de côté, et `CanUndo` passe à vrai. Le
défaut est antérieur à Q14 et de la même famille : la poignée prend la position du
pointeur pour le coin, au lieu d'appliquer un écart au coin d'origine.

*Recette* : au pointeur synthétisé, une pression puis un relâchement sur chacune des
huit poignées laissent le bloc identique et l'historique vide. Effort : faible.

*Repris le 25 octobre 2026, dans la phase N (salve Saisie 3, avec Q21).* La relecture du cours l'avait
cru réglé depuis I4 — le redimensionnement suit le chemin du pointeur, et un chemin nul rend le
cadre — : il ne l'est pas. Une pression sans mouvement sur une poignée repasse par `ApplyResize`
et ses cosinus, et un bloc tourné en ressort décalé, avec un pas empilé ; `ApplyRotate` arrondit
l'angle sous Maj sans le moindre mouvement — un bloc à 7° passe à 0°. *Recette*, en plus : la
poignée de rotation pressée sous Maj laisse 7° à 7° ; un bloc tourné de 12,3456789°, pressé sur
chaque poignée sans mouvement, ne bouge pas.

### Q23 — Sélectionner un bloc ne fige pas la fenêtre

*Trouvé le 12 octobre 2026, à la fusion de Q14 dans main.* Le test du double-clic sur un
bloc tourné tombait une fois sur deux, sur une machine au repos. Avalonia reconnaît un
double-clic à l'horloge — deux pressions à moins de 500 ms —, et le banc sans tête date
chaque pression du moment où il la livre. Mesuré au chronomètre autour de la pression :
sur un bloc non sélectionné, elle rend la main après **170 à 470 ms** (ATA 21, 3 passes),
la première d'un processus jusqu'à 720 ms ; sur un bloc déjà sélectionné, sous 130 ms.
Un clic d'échauffement ne suffit pas : ce n'est pas la compilation à la volée, c'est la
sélection elle-même, chaque fois. La cause n'est pas cherchée ; le panneau recomposé à
chaque changement de sélection est le premier suspect (la liste des polices, faite de
toutes celles de la machine, en est un candidat).

Pour l'utilisateur, une vraie plate-forme date le clic à la frappe et le double-clic
reste reconnu, mais la fenêtre ne répond pas pendant ce temps. En attendant, les deux
tests du double-clic sélectionnent le bloc par le modèle de vue avant le geste — le
relevé d'avant est pris plus tôt, si bien que rien de Q14 n'est perdu.

*Recette* : au pointeur synthétisé, sur ATA 21, la pression sur chaque bloc non
sélectionné rend la main en moins de 100 ms (médiane de trois passes) ; les tests du
double-clic reprennent le geste entier, le premier clic prenant le bloc, et passent vingt
fois de suite. Effort : à mesurer — la cause d'abord.

*Repris le 24 octobre 2026 en fin de lot 1 de la phase I* : la barre contextuelle et les
onglets ajoutent des contrôles recomposés à chaque sélection, et le défaut se mesure sur
l'interface qui restera.

*Livré le 25 octobre 2026*, sur l'interface de la phase I entière (`0ab3395`). La cause
d'abord, cherchée par trois diagnostics indépendants qui se sont recoupés.

**La cause : le panneau reconstruit à chaque sélection prise à vide.** Les trois sections
de l'onglet Propriétés — « Position et taille », les sections propres au type, « Fond et
contour » — et les réglages du bloc dans la barre contextuelle (`ReglagesDuBloc`) étaient
des `ContentControl` liés à `SingleSelected`. Un `ContentControl` ne garde son contenu
que d'un bloc au suivant tiré du même gabarit : la sélection vide lui donnait `null`, il
jetait ses champs, et la pression suivante les reconstruisait tous, styles et gabarits de
contrôle compris, avant de rendre la main. Un bloc de texte faisait passer la fenêtre de
735 à 1 795 visuels — 920 au dock, dont 24 `NumericUpDown` et 21 `TextBox`, 144 à la
barre. Passer d'un bloc de texte à un cadre les jetait de même. Chiffré :

- au processus chaud, la pression sur un bloc non sélectionné coûtait 47 à 55 ms, contre
  moins d'une milliseconde sur un bloc déjà pris ; sa mise en page en faisait la moitié,
  dont 80 % pour le dock ;
- retirer les trois sections du dock la ramenait de 47,6 à 7,7 ms, et la barre avec elles
  à 3,9 ms ; aucun autre retrait ne changeait la pression de plus que la mesure n'en
  distingue (± 1,5 ms) ;
- au processus froid, où les tests du double-clic cliquent chaque bloc une seule fois, la
  même reconstruction tournait en code non optimisé : la première pression d'un processus
  dépassait 300 ms, les suivantes 125 à 200 — les 170 à 470 ms du 12 octobre.

**Ce qui n'y était pour rien**, mesuré par retrait : la liste des polices de la machine,
premier suspect — elle n'est faite qu'au changement de document, et la liste n'en
construit les lignes qu'à son ouverture —, les vignettes de pages (I5), la projection sur
les règles, les onglets Styles, Couleurs et Document. Le compte des lignes en excédent
(I9) recompose le bloc à chaque sélection : 1 à 2,6 ms. « Blocs de la page » défilait à
chaque sélection par une passe de mise en page entière qui reconstruisait ses lignes,
même quand la ligne du bloc était en vue : 5 à 10 ms.

**La correction.** `Views/GabaritsGardes.cs` remplace les quatre `ContentControl` : un
panneau qui tire les réglages du bloc du premier gabarit qui l'accepte, comme le
`ContentControl` le choisissait, les construit à la première sélection d'un bloc de leur
sorte, puis les garde, masqués tant qu'aucun bloc de leur sorte n'est seul sélectionné. Une
sélection ne fait plus que montrer le bon et lui donner le bloc. Le gabarit masqué garde
son dernier bloc : il ne prend aucune entrée, et Avalonia reprend le focus à un champ qui
cesse d'être visible, comme il le perdait en quittant l'arbre. Deux retouches en
découlent. « Blocs de la page » ne défile plus que si la ligne du bloc n'est pas entière
dans la liste. Et le sélecteur de couleur de la barre, refermé, rendait le focus au dernier
champ de la fenêtre qui l'avait — un champ de la barre, désormais gardé : la transaction
ouverte à son focus engloutissait celle de la couleur, qui ne faisait plus son pas
(`BarreContextuelleTests` l'a vu) ; il le rend aussitôt.

**Mesuré après**, au pointeur synthétisé, sur ATA 21, chaque pression chronométrée jusqu'à
ce que la fenêtre n'ait plus rien à faire, image comprise, médiane de trois passes dans un
processus neuf, charge moyenne de la machine 1,2 à 2 : 30 à 52 ms par bloc de texte au lieu
de 107 à 136, 36 ms au lieu de 54 pour le cadre. Une pression ajoute un visuel à la fenêtre
(1 700 → 1 701). Ce qui reste, au profileur : l'image elle-même pour 40 % — le banc la
rend sur le fil de l'interface, où l'application ne paie que la remise au compositeur —,
la mise en page pour un quart, le défilement de la liste quand la ligne n'est pas en vue,
le compte de I9. La première pression d'un processus reste vers 300 ms : la compilation à
la volée, que la médiane écarte. La suite d'interface passe de 1 min 16 à 1 min 01.

*Recette jouée* : `Scripts/recettes/Q23-selection.sh`. Les sources d'abord : aucun réglage
du bloc n'est plus un contenu lié au bloc seul, et les deux tests du double-clic ne
sélectionnent plus par le modèle de vue. Puis quatre tests sous `poste=Q23`
(`PressionTests`) : le chronomètre de la recette, sous 100 ms sur chacun des huit blocs ;
les mêmes champs du dock et de la barre d'une sélection à l'autre, masqués sans bloc, et
une pression qui n'ajoute pas cinquante visuels ; un champ masqué qui rend le focus ; la
liste qui ne défile que pour montrer le bloc. Enfin les deux tests du double-clic, rendus
au geste entier — le premier clic prend le bloc —, vingt fois de suite, chacun dans un
processus neuf : la première pression y prend 185 à 195 ms, loin des 500 ms d'Avalonia.

*La marge du chronomètre.* Le seuil de 100 ms laisse un facteur de deux au moins à la
charge des autres sessions ; une collecte forcée avant chaque pression écarte celles que
le banc déclenche en allouant une image par rendu, et la médiane, une passe ralentie. Sur
le code d'avant, il tombe, mais pas sur chaque bloc ni avec la même marge d'une exécution à
l'autre — trois blocs sur huit, à 104 à 138 ms, à la dernière : c'est le compte des visuels
qui voit le retour de la cause, sur toute machine, et le sabotage du juge (« les réglages
du bloc se reconstruisent à chaque sélection ») est pris par lui.

*Laissé tel quel.* Le compte de I9 reste synchrone, dans la pression ; le remettre à plus
tard ne gagnerait rien que la recette mesure. `BeginEdit` sérialise tout le document à la
pression (0,5 ms sur ATA 21, 63 ms à la première d'un processus). La première sélection
après le lancement paie la compilation à la volée : une publication précompilée
(`PublishReadyToRun`) la réduirait, au prix d'un exécutable plus lourd — un choix de
publication, qui revient au propriétaire.

*Repris le 26 octobre 2026 après relecture.* Aucun défaut de l'application n'a été montré.
La relecture a éprouvé le chronomètre sous charge, comblé un trou, et repris le juge.

- **Le chronomètre ne tient pas sous charge, sans être mou.** Joué pendant que la machine
  portait une charge de 17 à 22 sur vingt cœurs — les autres sessions et les juges de la
  relecture —, il tombe deux fois sur trois sur le code livré : une médiane de 104 ms sur
  « Air Production », de 127 et 181 ms sur un bloc aux deux autres exécutions, le reste entre
  43 et 88 ms. Sous la même charge, le code d'avant (les réglages reconstruits à chaque
  sélection) donne 77 à 343 ms, sept à huit blocs sur huit au-dessus du seuil : il voit la
  cause. Il n'a pas été retouché — un test à l'horloge ne se modifie pas pour passer —, mais
  il faisait mentir le juge : deux sabotages qui ne changent rien au temps, le sélecteur de
  couleur laissé ouvert quand le bloc change sans clic et la liste qui défile vers une ligne
  déjà en vue, n'étaient attrapés que par lui, et le garde-fou attendu du sabotage du poste,
  « Q23-selection ou PressionTests », l'aurait accepté seul. Il nomme désormais
  `Les_reglages_du_bloc_sont_construits_une_fois_et_gardes`.
- **Le sélecteur de couleur, quand le bloc change sans clic.** Q23 a repris l'écoute qui le
  referme, de `ContentControl.Content` à `GabaritsGardes.Bloc`. Le test d'I3 change de bloc
  au pointeur, et le premier clic referme le volet de lui-même : il passait sans elle.
  `Changer_de_bloc_sans_le_pointeur_referme_aussi_le_selecteur` change de bloc par le modèle
  de vue, comme un raccourci ou une annulation : sans l'écoute, le volet restait ouvert sur
  le bloc d'après et sa transaction engloutissait le geste suivant. Le code livré passe.
- **Le juge** : cinq sabotages au lieu d'un — les réglages reconstruits, ceux d'une autre
  sorte restés visibles, ceux qu'on montre restés liés au bloc d'avant, le sélecteur laissé
  ouvert, le focus rendu par le sélecteur refermé à un champ gardé —, chacun attendu par le
  test qui le voit et non par la recette, qui embarque le chronomètre. Joué le 26 octobre 2026 : cinq sur cinq, le premier rejoué après que son garde-fou a été élargi aux tests de la barre — le juge ne nomme que les quatre premiers tests tombés, et le premier passage l'avait compté comme un trou. La suite d'interface passe entière (285 tests) et la recette aussi, sous une charge de 13 à 15.
- *Non montré* : la liste qui défile vers une ligne déjà en vue. Un test qui comptait les
  lignes préparées, recyclées ou déplacées pendant la pression n'en voyait aucune, avec la
  garde et sans elle, ni une passe de mise en page de plus : la dépense que la livraison a
  chronométrée (5 à 10 ms) ne se compte pas ainsi, et n'a pas de sabotage.

### Q24 — Annuler ou Rétablir ne laisse plus une saisie hors de toute transaction

*Trouvé le 25 octobre 2026 par la relecture du cours sous l'angle de la saisie (fiche 23, a14),
confirmé à la lecture du code.* C'est le seul défaut de la phase N qui peut perdre des données.
`Undo` et `Redo` referment d'abord ce qui est ouvert (`EndPendingEdit`), puis rendent la main s'il
n'y a rien à défaire. Trois cas laissent alors la saisie ouverte sans transaction :
- `Ctrl+Z` quand rien n'a changé et que rien n'est à annuler ;
- `Ctrl+Y` après **n'importe quelle** frappe, que l'historique soit plein ou vide : `CommitEdit`
  vide la pile de rétablissement, et `Redo` la trouve vide — le cas le plus fréquent ;
- (déduit) une annulation pendant qu'un champ du dock ou de la barre garde le focus :
  `_propertyEditOpen` reste vrai alors que la profondeur est retombée à zéro.

`RemarquerUneRetouche` exige une transaction ouverte et ne voit rien ; la transaction suivante
prend pour référence un document qui contient déjà la frappe. Cette frappe ne s'annulera jamais, et
elle ne marque pas le document « modifié » — une fermeture la perdrait sans question.

La correction : `Undo` et `Redo` referment ce qui est ouvert, comme aujourd'hui, puis lèvent un
avis ; **chaque propriétaire encore actif rouvre sa propre transaction** — la saisie de la page si
elle est toujours ouverte, le champ du dock ou de la barre s'il a toujours le focus.

*Piège* : ne jamais rouvrir à l'aveugle. Une boucle qui rouvrirait autant de transactions qu'il y
en avait, après une restauration qui referme la saisie par le changement de sélection, laisserait
une transaction sans propriétaire, où chaque geste suivant fondrait son pas. La règle « un pas par
champ » de Q14 et de I2 reste intacte.

*Recette* :
- document neuf, bloc tracé, `Ctrl+Z`, frappe « abc », Échap : `CanUndo`, le document modifié, le
  titre marqué, et un `Ctrl+Z` rend le texte d'avant la frappe ;
- frappe « abc », `Ctrl+Y`, frappe « d », Échap : deux pas, et la saisie ne s'est pas refermée au
  `Ctrl+Y` ;
- dans le champ Contenu, frappe, `Ctrl+Z`, frappe : la seconde frappe fait un pas ;
- après une annulation, un glissé de 1 pt, `Alt` tenu, empile encore son propre pas ;
- au juge : l'avis qui n'est plus levé.

Fichiers : `MainWindowViewModel.cs` (`Undo`, `Redo`, l'avis), `MainWindow.axaml.cs` et
`PageCanvas.axaml.cs` (le réarmement). Part dans la même copie que Q25, avant lui (salve Saisie 1). Effort : faible.

*Fait le 26 octobre 2026.* Les trois cas se montrent, chacun par un test qui tombait sur le code
d'avant : `Ctrl+Z` sans rien à annuler pendant la saisie, puis « abc » — Annuler grisé, le titre
sans marque, aucun pas ; « abc », `Ctrl+Y`, « d » — un seul pas ; et le cas déduit, dans un champ
de la barre qui garde le focus sous un `Ctrl+Z` sans rien à annuler : la fenêtre croyait sa
transaction ouverte, la profondeur était à zéro, et le corps saisi ensuite ne faisait aucun pas.

`Undo` et `Redo` referment ce qui est ouvert, comme avant, puis lèvent l'avis
`TransactionsRefermees` sur chaque chemin — ils n'ont plus de retour anticipé — et ne rouvrent rien
eux-mêmes. L'avis est déclaré dans `MainWindowViewModel.Transactions.cs` (règle 10). Chaque
propriétaire y répond pour lui seul : la planche rouvre la transaction de la saisie si elle est
toujours ouverte, et celle du glissé si le bouton est toujours tenu, que son relâché refermera ;
la fenêtre, celle du champ si le panneau où il a pris le focus — la barre ou un onglet du dock — le
garde (`IsKeyboardFocusWithin`) ; la barre, celle du sélecteur de couleur s'il est toujours ouvert.
Une annulation qui relit le document referme la saisie par le changement de sélection, et le
sélecteur avec le bloc : rien ne se rouvre pour eux. La règle « un pas par champ » de Q14 et de I2
reste intacte : `UnGesteUnPas` et ses deux recopies (I7, I8) n'ont pas changé, et la recette
rejoue les tests voisins qui la tiennent.

*Recette jouée* : `Scripts/recettes/Q24-Q25-transactions.sh`. Les sources d'abord : `Undo` et
`Redo` commencent par refermer, finissent par l'avis, hors de toute condition, sans retour ni
`BeginEdit` ; l'avis est dans le partiel du poste et n'est levé que par eux ; la fenêtre, la
planche et la barre s'y abonnent et s'en désabonnent. Puis neuf tests sous `poste=Q24`
(`TransactionsDeSaisieTests`), au clavier sur la vraie fenêtre : les deux premiers cas du plan ;
le champ de la barre sous un `Ctrl+Z` sans rien à annuler ; le champ Contenu sous `Ctrl+Y` ; une
marge de l'onglet Document sous un `Ctrl+Z` qui la défait ; un glissé, et le sélecteur de couleur,
sous un `Ctrl+Z` sans rien à annuler ; et le piège — après une annulation qui referme la saisie, un
glissé de 1 pt, `Alt` tenu, fait encore son propre pas ; et la règle de Q14 vue de la transaction :
entrer en saisie et la quitter sans rien taper ne fait aucun pas. Ces deux derniers passaient sur
le code d'avant, les sept autres tombaient. Enfin quatre tests voisins : un par champ du dock et de
la barre (I2, I3), un par raccourci pendant la saisie d'un champ. Cinq
sabotages au juge : l'avis qui n'est plus levé, une transaction rouverte pour une saisie refermée,
et chacun des trois autres propriétaires qui ne rouvre plus la sienne.

*Repris le 26 octobre 2026, avant la fusion.* La recette jouait en voisin le test du double-clic
de Q14 sur ATA 21 (`Un_double_clic_ouvre_la_saisie_sans_rien_reecrire`), et `Scripts/verifier.sh`
y tombait : « le double-clic n'a pas ouvert la saisie de « Air Production » ». La cause n'est pas
dans le code de Q24. Avalonia ne compte deux clics que si moins de 500 ms séparent les deux
pressions, et le premier clic d'une fenêtre neuve, dans un processus neuf, les dépasse parfois :
compilation à la volée et construction des réglages du bloc (Q23) comprises, 285 à 684 ms
mesurées entre les deux pressions sur le premier bloc, 50 à 90 ms sur les suivants. Alternés sous
la même charge de la machine, le test joué seul tombe 5 fois sur 12 sur `2d8b9f9`, avant Q24, et 4
fois sur 12 sur `ea27f0c`, aux mêmes écarts : chaque échec est un premier écart de plus de 500 ms.
Dans le lot de voisins de la recette, il passait le premier, à froid ; les recettes de Q14 et de
Q23 le jouent après un autre test du même processus, à chaud. Il n'est donc plus un voisin : la
règle qu'il tenait ici — aucun pas à l'entrée en saisie — l'est par un test de la classe du poste,
le bloc pris avant le double-clic, que le délai ne touche pas. Le test de Q14 n'a pas changé.

*Ce que l'implémentation a appris.*
- Les champs du bloc — la barre, les sections du dock — perdent le focus à une vraie annulation :
  `Restore` change de page avant de rendre la sélection, changer de page la vide, et les réglages du
  bloc se masquent un instant (Q23) ; Avalonia reprend le focus à un champ qui cesse d'être visible.
  La recette du plan, « dans le champ Contenu, frappe, `Ctrl+Z`, frappe », ne se joue donc pas telle
  quelle : la seconde frappe n'arrive nulle part, et rien ne se perd. Elle se joue sous `Ctrl+Y`
  dans le champ Contenu, qui garde le focus, et sous `Ctrl+Z` dans une marge de l'onglet Document,
  que la sélection ne masque pas : l'annulation l'y laissait, sans transaction.
- Deux propriétaires de plus que les deux du plan, vus à la lecture du code et confirmés chacun par
  un test qui tombait : le glissé, de la pression au relâché — `Ctrl+Z` le bouton tenu, sur un
  historique vide, et le déplacement qui suivait n'entrait dans aucun pas —, et le sélecteur de
  couleur de la barre, qui tient sa transaction de son ouverture à sa fermeture (I3) — la pastille
  choisie après un `Ctrl+Z` ne faisait aucun pas. Sur le banc, le volet s'ouvre dans la fenêtre, et
  la touche atteint ses raccourcis ; sur une plate-forme où il a sa propre fenêtre, ce n'est pas
  éprouvé.
- Les liaisons de la fenêtre prennent `Ctrl+Z` et `Ctrl+Y` même quand un champ ou la zone de
  saisie a le focus : c'est l'historique du document qui annule, et le défaut passait par lui.

*Choisi sans certitude.*
- « Document neuf, bloc tracé » est lu comme un bloc posé dans le document à l'ouverture : tracé
  dans la session, le bloc fait son pas, et `Ctrl+Z` défait le tracé — l'historique n'est plus vide,
  et ce n'est plus le cas du défaut.
- L'avis est levé à chaque Annuler et Rétablir, même quand rien n'était ouvert : chaque propriétaire
  se règle sur ce qu'il voit, et un champ resté au focus sans transaction en retrouve une.

*Relevé, non corrigé.* Un changement de sélection pendant qu'un champ que la sélection ne masque
pas a le focus referme sa transaction sans qu'il la rouvre : `Ctrl+A` dans une marge de l'onglet
Document sélectionne les blocs (la liaison de la fenêtre prend la touche), le champ garde le focus,
et la marge saisie ensuite n'entre dans aucun pas ni ne marque le document — mesuré. N6, qui laisse
la touche au champ, en ôte ce chemin ; un changement de sélection venu d'ailleurs le garderait.
`ImporterTexte`, au modèle de vue, referme aussi tout ce qui est ouvert sans rien rouvrir : le geste
de la fenêtre referme la saisie et le champ avant de l'appeler, si bien que rien n'est laissé ouvert
— un appel venu d'ailleurs le ferait.

### Q25 — Un réglage fait pendant une saisie a son propre pas

*Trouvé le 25 octobre 2026 par la même relecture, à confirmer d'abord par un test.* Les champs de
la barre ouvrent leur transaction au focus, et celle-ci s'emboîte dans celle de la saisie, ouverte
par `BeginTextEdit` : changer le corps pendant qu'on tape se fond dans le pas de la frappe, contre
la règle 2 de la phase I. Aucun test ne joue la barre pendant une saisie sur la page.

La correction : la transaction de la saisie **suit le focus du champ de la page** — elle s'ouvre
quand il le prend, se ferme quand il le perd, et à `EndTextEdit`. Les boutons G, I et S, qui ne
prennent pas le focus, gardent `UnGesteUnPas`.

*Piège* : le sélecteur de couleur retire le focus en s'ouvrant, ce qui clôt la frappe d'abord — le
comportement voulu ; quand le focus revient au champ de la page, la transaction se rouvre ; la
saisie reste ouverte quand on clique dans le dock, comme aujourd'hui.

*Recette* :
- frappe « abc », corps 14 à la barre puis Entrée, clic dans le texte, frappe « d », Échap : trois
  pas, défaits un à un et dans l'ordre (un seul aujourd'hui) ;
- de même avec un retrait saisi au dock ; la couleur prise pendant la saisie fait son propre pas ;
- au juge : la transaction de la saisie ouverte au début de la saisie et non au focus.

Fichiers : `PageCanvas.axaml.cs` (`BeginTextEdit`, `EndTextEdit`), tests. Dépend de Q24, dans la
même copie. Effort : faible.

*Fait le 26 octobre 2026.* Le défaut se montre, comme le plan le disait : sur une saisie ouverte,
« abc », le corps à 14 dans la barre puis Entrée, un clic dans le texte, « d », Échap — la première
annulation ne rendait pas l'état d'après le corps, le réglage et les deux frappes ne faisant qu'un
pas ; de même pour un retrait de première ligne saisi au dock, et pour une couleur prise au
sélecteur de la barre.

La transaction de la saisie suit le focus de sa zone sur la page (`PageCanvas.axaml.cs`) :
`BeginTextEdit` ne l'ouvre plus, la planche l'ouvre quand la zone du bloc en saisie prend le focus
et la referme quand elle le perd — `GotFocus` et `LostFocus`, qui montent de la zone jusqu'à
elle —, et `EndTextEdit` referme celle que la saisie tient, et elle seule. Le focus quitte la zone
avant d'arriver au champ : le champ de la barre ou du dock ouvre la sienne quand celle de la
saisie est déjà refermée, et fait son pas ; le clic qui rend le focus au texte en rouvre une pour la
frappe qui reprend. La saisie reste ouverte quand on clique dans la barre ou le dock, comme avant.
Les boutons G, I et S ne prennent pas le focus : la zone le garde, et `UnGesteUnPas` referme la
transaction de la saisie, empile le pas du bouton et la rouvre, sans changement. La réponse de la
planche à l'avis de Q24 suit le même focus : elle rouvre la transaction de la saisie si sa zone a
toujours le focus, et non plus si la saisie est toujours ouverte.

*Recette jouée* : la troisième partie de `Scripts/recettes/Q24-Q25-transactions.sh`. Les sources :
`BeginTextEdit` sans `BeginEdit`, `EndTextEdit` sans `CommitEdit` direct, la planche abonnée au
focus de la zone, et les deux volets de couleur de la barre abonnés à leur fermeture imminente.
Puis cinq tests sous `poste=Q25` (`TransactionsDeSaisieTests`), au clavier et à la souris sur la
vraie fenêtre : les trois cas du plan — le corps à la barre, le retrait au dock, la couleur au
sélecteur —, chacun en trois pas défaits un à un et dans l'ordre ; le gras cliqué à la barre, qui
fait son pas sans prendre le focus ; et, Q24 et Q25 ensemble, un `Ctrl+Z` sans rien à annuler
pendant la saisie, le focus dans le corps de la barre — seul le champ rouvre sa transaction. Le gras
passait sur le code d'avant, les quatre autres tombaient. Quatre sabotages au juge : la transaction
qui ne se referme plus quand la zone perd le focus, celle qui ne se rouvre plus quand elle le
reprend, la saisie qui la rouvre sous l'avis sans que sa zone ait le focus, et le sélecteur de
couleur qui referme la sienne après avoir rendu le focus.

*Ce que l'implémentation a appris.* Le volet du sélecteur de couleur rend le focus au dernier
élément qui l'avait avant de lever `Closed` : pendant une saisie, c'est la zone de la page, et sa
transaction se rouvrait dans celle de la couleur, encore ouverte — la pastille et la frappe qui
suivait ne faisaient qu'un pas. La transaction de la couleur se referme donc à `Closing`, quand le
volet commence à se refermer (`BarreContextuelle.axaml` et son code, que le plan ne nommait pas) ;
`Closed` garde ce que Q23 y avait mis — un champ de la barre qui reprend le focus le rend aussitôt —
et referme la transaction d'un volet qui se serait refermé sans prévenir. Le piège du plan se
vérifie : le sélecteur retire le focus en s'ouvrant, ce qui clôt la frappe d'abord, et la frappe
reprend dans la zone, qui a retrouvé le focus, sans clic.

*Choisi sans certitude.* Entre le double-clic et le focus de la zone, que la planche lui donne au
passage de mise en page suivant, la saisie n'a pas de transaction : rien ne s'y tape encore. Une
fenêtre quittée pendant la frappe n'est pas éprouvée sur le banc : si la plate-forme retire alors
le focus à la zone, la frappe déjà faite fait son pas, et la suite en ouvre un autre au retour.

---

## Phase I — La nouvelle interface

*Ouverte le 24 octobre 2026*, sur la maquette de la fenêtre principale déposée dans
`Maquettes/Fenêtre principale — bloc de texte sélectionné-html/Main.dc.html` : un seul
écran, la fenêtre en 1440 × 900, un bloc de texte sélectionné. Elle **réorganise** une
interface qui existe en grande partie, et elle la **rhabille** : un rail d'outils à
gauche, une barre principale à icônes qui reçoit l'alignement et l'ordre, une barre
contextuelle qui suit la sélection, un dock à onglets aux sections repliables, une
planche claire aux règles fixes. Elle suppose aussi des fonctions que le modèle n'a pas ;
la phase n'en prend que les plus légères.

La maquette fait foi pour la **disposition**, la **palette**, les **tailles des
contrôles** et les **libellés**. Elle ne fait pas foi pour les **cotes** : la page y est
dessinée à 64 % sous un libellé de 86 %, les bandes de projection et les champs X, Y, L,
H ne décrivent pas le bloc dessiné, le repère cyan passe sous la page, et elle annonce
3 colonnes dans la barre contextuelle pour 2 au dock. Aucune recette ne reprend un de ses
nombres, ni son contenu — 3 pages, « Article principal » —, qui n'est pas celui de
`exemple-bulletin.mypub`.

**Quatre décisions du propriétaire, le 24 octobre 2026.**

- Le dock a des **onglets**, comme la maquette. La ligne « Volet Office commutable » de
  [Ce que ce plan ne fait pas](#ce-que-ce-plan-ne-fait-pas) est rouverte. Ce qu'elle
  craignait — un panneau qui change de contenu sans prévenir — reste écarté : c'est
  l'utilisateur qui choisit l'onglet, et le premier, Propriétés, suit la sélection comme
  le panneau d'aujourd'hui.
- **A6 reste clos** : la position et la taille du bloc restent dans la barre d'état, et
  la bulle de dimensions de la maquette s'y **ajoute**, sur la planche (I4).
- **Le modèle ne change pas pour l'affichage** : quatre marges intérieures (C6), un
  interligne en multiple de la hauteur naturelle (R4). Le dock montre un champ de marge
  quand les quatre sont égales, quatre sinon, et l'interligne en multiple (« 1,15 × »).
  Les longueurs y portent leur unité dans la valeur, en millimètres comme la maquette,
  par les convertisseurs de Q14 et leur mémoire.
- Les deux lots passent **avant la salve 3**. Le lot 1 absorbe **B7** — replier les
  panneaux, que la maquette range au menu Fenêtre — et **Q23**, que la barre
  contextuelle aggraverait.

**Deux lots.** Le lot 1 ne touche que la vue : aucune clé sérialisée nouvelle,
`FormatVersion` inchangée, le relevé de `Scripts/reference.txt` inchangé. Le lot 2 ajoute
au modèle ce que la maquette montre et qui est **additif**, sans nouvelle version, au
précédent de `Columns` : le souligné, la visibilité d'un bloc, le trait (A10), et le
compte des lignes en excédent, qui n'est qu'une fonction. Le sérialiseur écrit toutes les
valeurs, défaut compris (`JsonIgnoreCondition.Never`) : chaque poste du lot 2 qui ajoute
une clé réenregistre donc `Samples/` et `Templates/`, et le dit dans son commit.

**Ce que la maquette montre et que la phase ne fait pas.** Rien de cela n'est dessiné,
pas même grisé : un contrôle sans moteur derrière est une commande morte.

- Le **port de chaînage**, « **Ajuster le texte** » et la section **Habillage** : le
  [seuil D](#seuil-d--le-moteur-de-composition), dont la décision reste au propriétaire.
  Le marqueur rouge de A2 reste le signal du débordement.
- La section **Effets** : A14 pour le contour et l'ombre du caractère, rien pour le reste.
- Le **style de paragraphe nommé** et la **graisse** : un poste chacun, avec nouvelle
  version — la graisse remplace un booléen. Ni l'un ni l'autre n'est planifié.
- Les **gabarits** : les pages maîtres, toujours non planifiées. Le mot « gabarit » a déjà
  deux sens dans le code — le préréglage d'un outil et les `DataTemplate` de la planche —,
  et un troisième devra s'appeler autrement.
- La **pipette**, qui demande de choisir entre copier une référence de jeu
  (`scheme:accent1`) et la couleur résolue.
- Le **nuancier libre** de l'onglet Couleurs : l'onglet montre le jeu de couleurs, qui
  existe.

**Les règles de la phase.** Elles valent pour chaque poste et se vérifient à sa relecture.

1. **Un contrôle par propriété.** Ce qui monte dans la barre contextuelle quitte le dock ;
   rien n'est doublé. Des tests prennent leur contrôle par `.Single()` : la police
   (Q20), l'angle (Q14), le cadrage de l'image, le sélecteur de couleur.
2. **Un geste, un pas d'annulation, où que soit le champ.** La transaction d'édition ne
   s'ouvre aujourd'hui qu'au focus du panneau de droite. La barre contextuelle et chaque
   onglet l'ouvrent et la ferment de la même façon. Aucun test existant ne verrait
   l'oubli : chaque poste qui déplace un champ en écrit un.
3. **La planche ne bouge pas quand la sélection change.** La barre contextuelle garde sa
   hauteur sans sélection, et montre alors la pastille « Page ». Sinon la page descend de
   40 px entre la pression du pointeur et son relâchement.
4. **Rien de neuf dans `PageRoot`.** C'est ce que l'écran compare au PDF (`Rendu.Encre`,
   le banc, `comparer-rendus.sh`). Bulle, projection, repères prolongés et poignées
   vivent dans l'`Overlay` ou hors de la page.
5. **Aucun zoom d'office.** Les tests et le banc ouvrent à l'échelle 1 ; « Page entière »
   est une commande, jamais l'effet d'une ouverture ni d'un redimensionnement.
6. **Les noms survivent.** `PageRoot`, `InputLayer`, `Overlay`, `TopRuler`, `LeftRuler`,
   `ListeDesBlocs`, `TraitDInsertion`, `FormatDePage`, `JeuDeCouleurs`,
   `NombreDeColonnes`, `RetraitPremiereLigne`, `MenuImporterTexte` et son libellé
   « Importer un te_xte… », `BoutonImporterTexte`, `PositionDuBloc`, `TailleDuBloc` :
   tests, recettes et banc les cherchent par leur nom.
7. **Le juge garde ses cibles.** Chaque texte qu'il sabote reste présent, une seule fois :
   `eprouver-le-juge.sh --cibles` passe avant chaque fusion. Un code neuf de liste, de
   vignette ou de geste ne recopie pas une ligne visée.
8. **La culture reste la culture courante.** Pas de fr-FR forcé : la virgule de la
   maquette vient de la locale, et les tests tournent sous `LC_ALL=C`.
9. **Les raccourcis passent par le tableau du README**, que lit la recette de A4 : sans
   doublon, et aucun `Ctrl`+lettre déclaré à la fois en `KeyBinding` et dans `OnKeyDown`.
   Les lettres seules ne jouent que hors saisie.
10. **Chaque poste met ce qu'il ajoute au modèle de vue dans un fichier partiel à lui**
    (`MainWindowViewModel` est `partial`) : c'est ce qui permet à deux postes de partir
    ensemble sans toucher le même fichier.
11. Chaque recette entre dans `Scripts/verifier.sh`, et chacune est éprouvée par au moins
    un sabotage du juge, comme depuis P3.

### Lot 1 — La vue

Quatre vagues, parce que la fenêtre et la planche sont chacune un seul fichier : I1 seul ;
puis I2 avec B7, en même temps que I4 ; puis I3, I5 et I6 ensemble, chacun dans son
propre contrôle ou sa propre couche, aux points d'accroche que I2 a posés ; puis Q23, sur
l'interface finie.

#### I1 — L'apparence de l'interface

Une seule source pour la palette, les tailles et les icônes de la maquette : un
dictionnaire de ressources et ses styles, chargés par `App.axaml`.

- **Couleurs** : accent `#2557C7` (survol `#1B439C`) ; texte `#1F2329` et `#2B323B` ;
  libellés `#5B6470` ; fonds `#FFFFFF`, `#F4F5F7`, `#FAFAFB` ; filets `#E2E5EA` et
  `#D5D9DF` ; bord des champs `#CDD2D9` ; barre contextuelle `#EEF2FB` filetée de
  `#D6DEEE`, pastille `#DCE5F8` ; alerte `#FDF1E8` sur `#8A2E07`.
- **Classes** : bouton-icône de 30 × 30 à rayon 6, actif plein d'accent ; champ de 26 px
  à rayon 5, corps 12 ; libellé de corps 11 ; séparateur de 1 × 20 ; titre de section
  repliable de corps 12, demi-gras.
- **Icônes** : les tracés SVG de la maquette, sur sa grille de 24, avec ses épaisseurs de
  trait (1,7 à 2,2). Un tracé à trait s'écrit en `Path` à `Stroke`, et non en `PathIcon`,
  qui ne sait que remplir.

Inter est déjà la police de l'interface. Le poste ne déplace rien : il pose l'apparence,
et les postes suivants la lisent.

*Recette* — reprise à la livraison, puis après relecture, voir plus bas :
- chaque couleur de l'interface de la maquette — sa page, qui est le document, et l'orange
  du port de chaînage exceptés — est déclarée une fois dans l'apparence, en `Color`, sous
  le nom que la recette lui donne, et l'apparence n'en écrit aucune autre, ni aucune en
  dur ailleurs que là — en chiffres sous aucune forme, ni par son nom ; aucune vue de la
  phase n'en écrit une en dur, et une vue qui cite l'apparence, ou qui écrit en dur une
  couleur de la maquette que le relevé d'avant la phase n'a pas, est une vue de la phase
  qui doit le dire (script sur les sources) ;
- chaque style de l'apparence est porté par une classe qu'aucune vue hors de la phase
  n'emploie ; chaque classe de la feuille de style de la maquette y a ses tailles et ses
  couleurs, chacune sous une clé qui la porte ; et la fenêtre d'aujourd'hui, ouverte sur
  l'exemple avec un bloc sélectionné, rend la même image au pixel avec ou sans
  l'apparence ;
- chaque icône de l'apparence est le tracé de la maquette, élément par élément ; chaque
  icône employée par une vue existe dans l'apparence, et chaque icône de l'apparence sert
  dans une vue dès que le poste qui l'emploie, d'après la table de la recette, est clos au
  tableau de suivi ;
- au banc, un bouton-icône rend 30 × 30 px à l'échelle 1, à rayon 6 ; actif, il est plein
  d'accent ; survolé ou pressé, il prend le gris de la maquette ; grisé, il s'efface sans
  fond.

Effort : faible.

*Repris le 24 octobre 2026, à la livraison.* Deux clauses de la recette d'origine ne
pouvaient pas se jouer en I1 : aucune vue de la phase n'existe encore, et aucune icône ne
sert. Écrites telles quelles, la première passait sur une liste vide et la seconde
échouait jusqu'à I9. La recette tient donc deux listes, que les postes suivants
complètent :
- `vues_de_la_phase`, **vide en I1** : chaque poste de la phase qui écrit ou réécrit une
  vue l'y ajoute, dans le commit qui la pose — I2 `MainWindow.axaml`, I3 le fichier de sa
  barre, I4 `PageCanvas.axaml`, I5 celui des vignettes. Une vue de la liste n'écrit
  aucune couleur de la maquette en dur, ni en chiffres sous aucune forme (`#2557C7`,
  `#FF2557C7`, `#FFF`) ni par son nom (`White`, `Brushes.White`) : elle cite l'apparence ;
- la table des icônes, chacune avec le poste qui l'emploie : I2 pour vingt-six, I3 pour
  les quatre alignements du texte, I6 pour la main et la loupe, A10 pour la ligne, I8
  pour l'œil, I9 pour l'alerte. La clause « chaque icône sert » s'éveille icône par
  icône, quand son poste passe à 100 % : I2 ne se clôt pas sans avoir posé ses vingt-six
  icônes. Une icône ajoutée à l'apparence sans entrer dans la table, ou l'inverse, fait
  échouer la recette.

Pour qu'une clause qui dort ne soit pas une clause morte, chaque contrôle se rejoue à
chaque passage sur un **témoin fautif**, qu'il doit refuser : une vue de la phase qui écrit
en dur l'accent, un filet et le blanc sous six formes — et ni une ombre noire, ni le
transparent ; une vue hors de la liste qui cite un filet de l'apparence ; une autre qui
prend en chiffres le gris de la planche, refusée sans relevé et admise avec ; une vue qui
emploie une icône absente ; une icône d'un poste clos que seul un commentaire nomme ; un
sélecteur nu ; une classe déjà prise ; un tracé dont un chiffre a bougé ; un reflet faux.

Deux clauses sont ajoutées, parce que le poste ne vaut que par elles. La **fidélité des
tracés**, que la recette d'origine supposait : la recette relit la maquette elle-même,
retrouve chaque svg par le libellé de son bouton, et compare élément par élément
géométrie, épaisseur, extrémités, jointures, pointillé, couleur et taille. Et l'**écran
inchangé**, que le texte du poste promettait (« le poste ne déplace rien ») sans le
mesurer.

*Repris le 24 octobre 2026, après relecture.* La relecture a trouvé quatre trous, que
chacun un sabotage a montrés ; ils sont refermés.
- **La liste des vues de la phase se tenait de mémoire.** Une planche réécrite qui
  écrivait `#DCDFE4` en oubliant de s'y inscrire passait. La liste reste, mais l'oubli ne
  passe plus. Une vue qui cite l'apparence — une couleur, un pinceau, une icône, une
  classe — sans être dans la liste est refusée. Et hors de la liste, les couleurs de la
  maquette écrites en dur sont exactement celles d'un **relevé d'avant la phase** que
  tient la recette : le gris `#FAFAFB` des panneaux de la fenêtre, le blanc de la
  fenêtre, de la page, du marqueur de débordement et des vignettes de modèle — huit
  écritures dans cinq fichiers. Une couleur de plus, c'est une vue de la phase qui ne le
  dit pas ; une de moins, un relevé périmé qui laisserait la place reprise en silence :
  l'une et l'autre échouent, et une vue qui entre dans la liste quitte le relevé.
- **Deux couleurs échangées passaient** : l'accent et son survol restaient chacun déclarés
  une fois, et les tests, qui lisent l'accent dans l'apparence, le retrouvaient sur le
  bouton actif. La recette tient maintenant le nom de chaque couleur, et relit la feuille
  de style de la maquette — `.ib`, `.ib:hover`, `.ib.on`, `.fld`, `.lbl`, `.sep`, `.sec` :
  chaque style qui les porte cite une clé de la couleur que la cascade de la maquette
  donne, et a ses tailles, soixante-deux valeurs en tout, le rayon de 6 compris. La
  projection est comparée au `rgba` de la maquette, opacité comprise ; les ombres, du
  noir hors de la palette, restent à I4 et I5.
- **`#FFF` et `White` échappaient au contrôle des couleurs en dur**, qui ne lisait que six
  ou huit chiffres. Il lit les quatre formes en chiffres, les noms d'Avalonia — relevés
  par réflexion sur `Colors`, sans égard à la casse, comme les lit le XAML — et les
  composantes en C# (`Color.FromRgb`) ; dans l'apparence, hors des déclarations, seul
  `Transparent` passe.
- **Le rayon, le survol et le grisé n'étaient pas mesurés** : un rayon de 5, un survol
  peint d'accent, une opacité oubliée passaient. Le test du bouton-icône lit son rayon ; un
  sixième test le survole, le presse et le grise.

Et trois corrections de moindre portée : une icône ne « sert » que par un emploi —
`{StaticResource IconeX}`, sa clé passée à une recherche de ressource —, et non plus par
un commentaire qui la nomme ; le miroir de « texte à droite » est calculé sur le svg de
« à gauche », et non recopié ; le bilan d'une clause ne se dit plus satisfait quand elle a
refusé quelque chose.

**Le nom.** Le poste s'appelait « la charte de l'interface », et son fichier
`Charte.axaml`. Or `Charte` est déjà, dans le noyau, le jeu de couleurs et de polices du
document (`Jeux.cs`, C5b), que `JeuAffiche.Charte` pose sur la page, dans le même dossier
`Views` : l'homonymie que la phase interdit pour « gabarit ». Le fichier devient
`Apparence.axaml`, ses tests `ApparenceTests`, sa recette `I1-apparence.sh`. Ses classes
d'icône `trait` et `plein` deviennent `iconeTrait` et `iconePlein` : « trait » nomme déjà
le motif du contour d'un bloc et l'objet trait de A10, et un `Path` de la planche classé
ainsi, dans une vue de la phase que le contrôle des classes prises exempte, aurait été
repeint sans un mot.

**Une conséquence pour I4.** `PageCanvas.axaml` peint la page en `White` — `PageRoot` et
la feuille qui le porte —, ce que le relevé admet. Inscrite à la liste, la planche ne
peut plus écrire ce blanc en dur, bien qu'il soit celui du document et non de
l'interface : I4 dit d'où il le tire, sans changer un pixel de `PageRoot` (règle 4).

*Fait le 24 octobre 2026.* `MyPublisher/Views/Apparence.axaml`, un jeu de styles et ses
ressources, inclus par `App.axaml` après le thème Fluent.
- **Palette** : vingt-six couleurs, chacune un `Color` (`CouleurAccent`…) et un pinceau
  du même nom sans « Couleur » (`Accent`…), plus `Projection`, l'accent à 14 % de I4. Ce
  sont les couleurs de la liste ci-dessus, et celles que la maquette montre ailleurs dans
  son chrome : le survol d'un bouton-icône `#E6E9EE`, la barre d'état `#4A525D`, les
  graduations `#9AA2AD` et `#C3C8D0`, l'initiale du type `#3A424D` sur `#EEF0F3`, la
  diagonale de « Aucun » `#D23B3B`, et la planche que I4 lira — `#DCDFE4`, `#12A4C4`,
  `#E3A6D6`. Hors de l'apparence : la page de la maquette (`#1F2A44`, `#C9D6EE`,
  `#F1F4FA`, `#C9D3EA`, `#3A4E7A`, et `#B5BCC6` des vignettes) et l'orange du port de
  chaînage (`#C2410C`). Les ombres en `rgba` sont laissées à I4 et I5.
- **Classes** : `ib`, `champ` (sur `TextBox`, `ComboBox` et `NumericUpDown`), `libelle`,
  `separateur` (sur un `Border`), `titreSection` (sur un `ToggleButton`, coché quand la
  section est ouverte) — les `.ib`, `.fld`, `.lbl`, `.sep` et `.sec` de la maquette,
  écrites en français, quand la plupart des classes d'avant la phase sont en anglais
  (`field`, `sidebar`, `icon`, `section`, `tool`…) ; trois noms étaient pris, `sep`,
  `section` et `field`. Et deux classes pour les tracés d'icône, `iconeTrait` et
  `iconePlein`. Le bouton-icône actif est un `ToggleButton.ib` coché.
- **Icônes** : trente-cinq, `IconeNouveau` à `IconeOeil`, chacune un `Viewbox` à la taille
  où la maquette l'affiche (12 à 18 px) sur une grille de 24 rognée, un `Path` par
  élément du svg. Elle s'emploie en contenu : `<Button Classes="ib" Content="{StaticResource
  IconeNouveau}"/>`. L'alignement à droite, que la maquette omet, est le miroir de
  l'alignement à gauche. Pas de pipette.

*Ce que l'implémentation a appris.*
- **Une icône est un contrôle, qui n'a qu'un parent.** Déclarée `x:Shared="False"`, chaque
  `{StaticResource}` en crée un exemplaire ; partagée, elle quitterait sans un mot sa
  première place à la seconde.
- **Le « currentColor » de la maquette** s'écrit, dans les classes `iconeTrait` et
  `iconePlein`, `{Binding $self.(TextElement.Foreground)}` : l'icône prend la couleur du
  texte de son hôte — sombre au repos, blanche sur l'accent, brique dans l'alerte — sans
  un style par icône ni par état. L'œil et la rotation, que la maquette peint elle-même
  en gris, citent `Libelle`.
- **Fluent peint les états d'un bouton sur le `ContentPresenter` de son modèle**, et non
  sur le bouton : chaque état du bouton-icône y est repris, sinon son gris de survol et
  le bleu de son bouton coché — qui n'est pas l'accent — passeraient. Même chose pour le
  bord des champs au survol et au focus. Un style de l'application l'emporte sur ceux du
  thème de contrôle de Fluent : retirée, la variante `:pressed` du survol ne change rien
  à l'écran, le bouton pressé étant aussi survolé. Elle reste, pour que la recette y lise
  l'état pressé de la maquette.
- **Le pointillé** se compte en épaisseurs de trait chez Avalonia, en unités chez le svg :
  « 2 2 » sous un trait de 1,7 s'écrit `1.17647,1.17647`.
- **Fluent donne aux champs 32 px de haut**, et 64 de large à la zone de texte ;
  l'apparence les lève. Les deux flèches du `NumericUpDown`, de la largeur d'un bouton
  chacune, ne tiennent pas dans un champ de 26 : `.champ` les retire, et la molette comme
  les flèches du clavier font encore le pas.
- **Le compilateur XAML remplace l'inclusion d'un fichier du projet** par son jeu de
  styles : `Application.Styles` ne contient plus de `StyleInclude`, et un test reconnaît
  l'apparence à sa première couleur. L'image composée du banc sans tête est en RGBA, quand
  `Rendu.Encre` rend en BGRA : les tests de l'apparence lisent le format au lieu de le
  supposer.
- **Le banc sans tête rend au tic de son horloge**, que `CaptureRenderedFrame` n'avance que
  d'un pas, et ce pas ne suffit pas toujours : relâché puis quitté, le bouton pressé
  restait gris sur l'image quand son fond était déjà transparent. Le test des états fait
  passer un tic de plus avant chaque image ; sans lui, la mesure de l'état pressé pouvait
  lire l'image du survol d'avant.

*Mesuré.* Rendues à 96 px, les trente-cinq icônes de l'apparence et les svg de la maquette
sous Chrome sans tête ne diffèrent d'aucun pixel de plus d'un quart de l'échelle des
gris — comparaison faite une fois, à la main : Chrome n'est pas une dépendance du dépôt,
et la recette compare les sources. Six tests d'interface sous `poste=I1` : un bouton-icône
de 30 × 30 pixels à l'échelle 1, à rayon 6, dont l'accent couvre exactement cette boîte
une fois actif, coins arrondis exceptés ; survolé puis pressé, le gris `#E6E9EE` d'un bord
à l'autre, actif et survolé toujours l'accent, grisé ni fond ni gris de Fluent et l'icône
au gris des commandes à 40 % ; chaque icône neuve à chaque emploi, dans sa boîte, à la
couleur courante ; champs de 26, séparateur, libellé et titre de section ; aucune clé de
l'apparence qui masque une ressource de Fluent ou du sélecteur de couleur ; et la fenêtre
de l'exemple, un bloc sélectionné, identique au pixel avec ou sans l'apparence — un seul
style sans classe y change 131 982 pixels.

*Choisi sans certitude.*
- `#1B439C` est le survol du bouton principal, comme dans la maquette celui d'un lien
  (`a:hover`) ; le bouton-icône a son propre survol, `#E6E9EE`, et actif il reste plein
  d'accent même survolé — ce que dit la cascade de la maquette (`.ib.on` vient après
  `.ib:hover`), que la recette lit et que le test mesure.
- Le grisé, que la maquette ne dessine pas et que I2 demande pour Annuler et Rétablir, est
  une opacité de 0,4, sans couleur de plus ; le focus d'un champ le borde d'accent.
- Le bouton principal, la pastille, l'alerte et la barre contextuelle n'ont pas de classe :
  le poste n'en nommait que cinq. Leurs couleurs sont déclarées ; le poste qui les dessine
  en fait une classe de l'apparence s'il en a l'emploi, et l'ajoute à ses tests.

Quatorze sabotages au juge, tous sur l'apparence sauf un, chacun attendu par sa recette ou
ses tests, et que `eprouver-le-juge.sh apparence` joue seuls : le bouton-icône actif peint
du gris de survol ; un sélecteur sans classe ; un tracé qui ne se ferme plus ; une icône
partagée ; une couleur écrite en chiffres dans un style ; le blanc écrit `#FFF` ; l'accent
et son survol échangés ; la projection à 60 % ; le trait figé au gris des commandes ; le
champ rendu à 32 px ; un rayon de 5 ; le survol peint d'accent ; le grisé qui garde son
encre ; et, hors de l'apparence, une vue d'avant la phase qui prend en chiffres le gris des
libellés de la maquette sans entrer dans la liste. Les textes visés portent leur
sélecteur, pour qu'un style ajouté plus tard ne les rende pas ambigus (règle 7).

#### I2 — La disposition de la fenêtre *(avec B7)*

La fenêtre de la maquette, de haut en bas.

- **Barre de menus.** La marque « M MyPublisher », les menus, et le titre du document
  centré : « exemple-bulletin.mypub — modifié ». Deux menus neufs : **Texte** — gras
  `Ctrl+G` et italique `Ctrl+I`, comme dans Publisher en français, les quatre
  alignements, et « Importer un te_xte… », qui y passe avec son nom et son libellé — et
  **Fenêtre** — le panneau Pages, le dock, et « Replier les panneaux » sur `Ctrl+F1`.
  C'est B7, livré ici avec sa recette. La barre de titre du système reste, avec son
  « • nom — MyPublisher » : Avalonia gère mal sous Linux l'extension de la zone cliente,
  et la réserve de 300 px que la maquette laisse à droite n'a pas d'objet sans elle.
- **Barre principale à icônes.** Nouveau, Ouvrir, Enregistrer · Annuler, Rétablir ·
  Aligner : à gauche, centrer horizontalement, centrer verticalement, ajuster aux marges ·
  Ordre : premier plan, arrière-plan, grouper · le zoom −, sa valeur, + et « Page
  entière » — le bouton est posé ici, sa commande vient de I6 · « Exporter en PDF » en
  bouton principal, à droite. Avancer et reculer d'un rang, répartir et « par rapport aux
  marges » restent au menu Disposition. Annuler et Rétablir se grisent quand l'historique
  est vide.
- **La place de la barre contextuelle**, de hauteur constante (règle 3), que I3 remplit.
  I2 y pose la pastille du type sélectionné, et « Page » sans sélection.
- **Rail d'outils à gauche**, de 46 px, outils seuls, dans l'ordre de la maquette :
  Sélection (V), Main (Espace) · Bloc de texte (T), Titre (H), Image, Tableau (B) ·
  Rectangle (R), Ellipse (E), Ligne (L) · Zoom (Z). Main et Zoom attendent I6, Ligne
  attend A10 : leurs boutons n'apparaissent qu'avec leur geste. L'image reste une action
  qui ouvre le choix du fichier, rangée parmi les outils. La pipette n'est pas posée.
- **Panneau Pages**, de 168 px : un en-tête avec « + », puis la liste des pages, dont le
  gabarit d'entrée est le point d'accroche des vignettes de I5 — d'ici là, le numéro.
  Pas de section Gabarits.
- **La planche** : ce poste n'y touche pas (I4).
- **Le dock**, de 292 px, à quatre onglets.
  - *Propriétés* : les sections repliables du bloc choisi. D'abord Position et taille — X,
    Y, L, H, l'angle, le nom, et la case « Conserver les proportions », dont le geste
    vient de I6. Puis la section propre au type : pour un bloc de texte, Cadre de texte
    (colonnes, gouttière, marges intérieures, alignement vertical), Paragraphe (retraits
    et espacements) et Contenu (champ, saisie, « Importer un texte… ») ; sinon Tableau,
    Image ou Groupe. Puis Fond et contour. Au pied, **Blocs de la page**, l'avant en
    tête ; la place de l'œil est réservée au lot 2.
  - *Styles* : le jeu de polices.
  - *Couleurs* : le jeu de couleurs.
  - *Document* : format, marges, grille, repères de règle, fond de page.

  Les sections sont ouvertes par défaut, et le dock retient, le temps de la session,
  celles qu'on a fermées.
- **Barre d'état** : le message — `Status`, que des tests lisent, reste —, le curseur, la
  position et la taille du bloc (A6, inchangées), « Page 1 sur 3 », le format
  (« A4 portrait », ou ses dimensions en millimètres pour un format libre), et le zoom
  avec son curseur, en échelle logarithmique entre les bornes de `SetZoom`.

**Ce qui se réécrit, et pourquoi.** L'ordre de la liste des blocs s'inverse : les cinq
tests de A4 qui lient l'indice de la liste à celui de `Page.Frames`, et le calcul du rang
de dépôt, se réécrivent à l'envers, avec les mêmes garanties. Les tests qui cherchent un
contrôle d'un onglet non choisi — `FormatDePage`, `JeuDeCouleurs`, les polices du jeu, le
fond de page — ouvrent d'abord l'onglet, par une aide commune de `Rendu`. Le README
(l'interface lue de gauche à droite, la barre « sur le bord droit », la table des
raccourcis) et les fiches 02, 03, 04 et 15 se réécrivent sur la nouvelle disposition.

*Recette* :
- au banc, à 1400 × 900, chaque zone est à sa place : le rail à gauche, le panneau Pages
  entre le rail et la planche, le dock de 292 px à droite, la barre contextuelle sous la
  barre principale ;
- sélectionner puis désélectionner un bloc ne déplace pas l'origine de `PageRoot` à
  l'écran (règle 3) ;
- chaque champ du dock, quel que soit son onglet, change le bloc en un seul pas
  d'annulation ;
- dans « Blocs de la page », un glissé du premier rang au troisième donne la même pile
  qu'avant la refonte, lue à l'envers ;
- B7 : après `Ctrl+F1`, la planche gagne au moins la largeur des deux panneaux et ceux-ci
  sont invisibles ; un second `Ctrl+F1` rend l'état d'avant. Le raccourci figure au
  README et n'en double aucun autre ;
- les recettes de A4, A5, A6, B2, C5b, C6, R2, R3, Q14 et Q20 passent, réécrites
  seulement là où ce poste le dit.

Effort : lourd — c'est la moitié de la phase.

*Repris le 25 octobre 2026, après relecture.* La relecture adversariale a confirmé
quatorze défauts ; tous sont refermés, chacun avec son test ou sa clause de recette et un
sabotage au juge — douze de plus, vingt-cinq pour le poste.
- **Les cases du menu Fenêtre ne masquaient rien.** `MenuItem.IsChecked` se lie dans un
  seul sens par défaut, à la différence de `ToggleButton.IsChecked` : le clic inversait la
  coche, et le panneau restait. Elles se lient dans les deux sens, comme les trois cases
  d'avant la phase qui avaient le même défaut — Repères, Règles, « Par rapport aux
  marges ». La recette refuse toute case de menu liée autrement, et un test clique chaque
  entrée du menu Fenêtre au pointeur.
- **Annuler restait grisé pendant une saisie qui avait changé le bloc.** Le pas n'entre
  dans l'historique qu'à la perte du focus, et un bouton grisé ne prend ni le pointeur ni
  le focus : son clic ne refermait même pas la saisie. Le bouton suit l'historique *ou*
  une transaction ouverte qui a déjà changé le document (`PeutAnnuler`), relevée à la
  première retouche du document ou d'un bloc choisi ; son clic referme la saisie puis
  l'annule, et le titre dit « modifié » dès cet instant. Rétablir se grise pendant ce
  temps : le pas qui vient videra sa pile.
- **Ctrl+G pendant une saisie du dock se fondait dans le pas du champ** (règle 2) : le
  raccourci joue sans retirer le focus. Le gras, l'italique et les alignements du menu
  Texte referment d'abord la saisie en cours, puis la rouvrent à la même profondeur : un
  nom tapé, le gras, la suite du nom font trois pas.
- **Le second Ctrl+F1 pouvait rendre un état périmé** : l'état d'avant le repli s'oublie
  dès qu'un panneau change au menu.
- **Trois sabotages passaient les tests** : un `Ctrl+F1` qui inverse chaque panneau, dont
  l'état intermédiaire n'était pas regardé ; une pastille masquée sans sélection, dont
  seul le texte l'était ; « A_juster le texte » dans un menu, la touche d'accès coupant le
  mot que la recette cherchait. La recette lit désormais un libellé comme il s'affiche.
- **Quatre écarts à la maquette.** Le survol d'un menu de la barre faisait 32 px au lieu
  de 23 : Fluent tient le menu à sa hauteur. Un champ grisé prenait le gris de Fluent,
  `#CCCCCC` bordé de `#94979D` ; l'apparence lui donne le fond et le bord de la maquette à
  40 %, comme au bouton-icône, et le champ numérique ne peint plus qu'un bord. La vignette
  de 22 manquait en tête des lignes Fond et Contour ; elle montre la couleur, ou la
  diagonale de « Aucun », et ne règle rien. Enfin, le bouton Italique, en Inter penché par
  simulation, se lisait comme une barre oblique ; il prend Georgia, à défaut une serif qui
  a une vraie face italique — la serif générique de fontconfig tombe ici sur Noto Sans.
- **Le juge sortait en vert sur un motif qui ne retenait rien** : « I2|B7 » ne désignait
  aucun sabotage, leurs intitulés commençant tous par « la fenêtre ». Le motif lit aussi
  le garde-fou attendu, et une épreuve qui n'a rien saboté échoue.
- Les fiches 02 et 15 avaient gardé leurs écarts d'avant la refonte ; ils sont réécrits.
  Le suivi porte I2 et B7 à 100 %, ce qui éveille, dans la recette de I1, la clause
  « chaque icône sert » pour les vingt-six icônes du poste.

*Choisi sans certitude.* La vignette montre et ne règle pas : la liste et le sélecteur de
couleur, au-dessous, restent le seul contrôle de chaque propriété (règle 1). Un sélecteur
ouvert depuis la vignette, dans un menu volant, serait plus près de la maquette, mais il
quitterait l'arbre visuel tant qu'il est fermé, et les tests qui prennent le sélecteur par
`.Single()` ne le trouveraient plus.

*Relevé, non corrigé.* Selon la relecture, les autres raccourcis de la fenêtre — `Ctrl+D`,
`Ctrl+Maj+G`, `Ctrl+Maj+H`, `Ctrl+Maj+F`, `Ctrl+Maj+B` — se fondent de même dans la saisie
du dock en cours. C'était le cas avant la phase ; un correctif commun, dans la fenêtre,
toucherait tous les raccourcis, et sa décision revient au propriétaire.

*Jugé le 26 octobre 2026*, après la fusion de I3, I5 et I6 : `eprouver-le-juge.sh` joué en
entier sur les vingt-cinq sabotages du poste — le motif « I2|^la fenêtre », en deux
tranches —, sur `interface` au commit `2d8b9f9`. Les vingt-cinq sont pris, chacun par son
garde-fou attendu ; rien n'était à corriger. Les sorties nommaient aussi « I5-vignettes »
pour presque tous : la recette de I5 échouait alors sous n'importe quel sabotage, défaut
refermé à la relecture de I5 ; le garde-fou attendu de chaque sabotage du poste est autre,
et il était bien là.

#### I3 — La barre contextuelle

La barre de la maquette, sous la barre principale, suit la sélection. Pour un bloc qui
porte du texte — texte, titre, tableau —, elle reçoit la mise en forme que le dock
portait :
- la police, avec la garde de Q20 ;
- le corps (« 10,5 pt ») ;
- le gras, l'italique et la couleur du texte ;
- les alignements gauche, centre, droite et justifié — la maquette omet la droite, mais
  le modèle l'a, et Publisher aussi ;
- l'interligne, en multiple.

Ces contrôles quittent le dock (règle 1). Le souligné y entre aussi — le bouton S, `Ctrl+U`
et « Souligné » au menu Texte —, si I7 est fusionné quand I3 part : *repris le 24 octobre
2026*, I7 part avant I3 et ne pose que le modèle (voir I7). La graisse et le style
nommé ne sont pas faits. Les colonnes restent au dock seul, où la maquette les met aussi.

Pour une image, une forme, un groupe ou une sélection multiple, la barre ne porte que la
pastille et les commandes propres au type que le dock n'a pas : dissocier pour un groupe,
aligner et répartir pour plusieurs blocs. Rien n'y est doublé.

La barre est un contrôle à part, dans son propre fichier, à la place que I2 lui a faite :
c'est ce qui lui permet de partir en même temps que I5 et I6.

*Recette* :
- un bloc de texte sélectionné : la barre montre sa police, son corps et son alignement ;
  un clic sur « centré » centre le bloc à l'écran et au PDF, en un pas d'annulation ;
- la police, l'angle et la couleur du texte n'ont toujours qu'un contrôle visible chacun
  (les `.Single()` de Q14, Q20 et C5b) ;
- passer d'un bloc de texte à une image change la barre sans déplacer la planche ;
- une saisie dans la barre ne vole pas les lettres seules : `T` frappé pendant que le
  corps a le focus s'écrit dans le champ, et `T` après `Échap` arme l'outil texte.

Effort : moyen.

*Repris le 26 octobre 2026, après relecture.* Le poste avait été fusionné dès que
`verifier.sh` passait, sans relecture ni sabotage, selon la règle du propriétaire. La
relecture adversariale a montré deux défauts, chacun par un test qui échouait sur le code
fusionné ; ils sont refermés, et le juge, qui n'avait aucun sabotage du poste, en a neuf.
- **Une commande de la barre ne faisait pas son pas au clic** (règle 2). « Gauche »,
  « Haut », « Horizontalement »… et « Dissocier » prenaient le focus, comme tout bouton ;
  la fenêtre ouvre à ce focus la transaction de la barre, où la commande s'emboîtait sans
  rien empiler. Son pas attendait que le focus quitte la barre, et un geste au clavier
  frappé entre-temps — une flèche, `Suppr`, `Ctrl+D` — s'y fondait : sur deux blocs,
  « Aligner en haut » puis `→` ne faisaient qu'un pas, qu'une annulation défaisait d'un
  coup. Seul « Dissocier » y échappait, son bouton disparaissant avec le groupe, et le
  focus avec lui. Un glissé sur la planche n'était pas touché : la planche prend le focus
  à la pression. Le test `Une_commande_de_la_barre_fait_son_pas_au_clic` le montrait. Les
  commandes à libellé (`Button.menu`) ne prennent plus le focus, comme les boutons à état
  du poste ; et leur clic fait rendre le focus au champ qui l'avait (`SurCommande`), avant
  que la commande ne joue — sans quoi une marge tapée à l'onglet Document, qui tient sa
  transaction ouverte, aurait absorbé l'alignement : le même test le joue, et il échouait
  encore sur la seule moitié du correctif.
- **« Souligné » s'offrait sans bloc de texte.** Entré au menu Texte avec la barre, il
  restait actif sur un tableau, une forme ou sans sélection, où il ne fait rien, quand le
  gras, l'italique et les alignements s'y grisent — « sans bloc de texte, rien n'est
  offert », dit le test du menu Texte de I2, qui ne le regardait pas. Le test
  `Sans_bloc_de_texte_le_souligne_ne_s_offre_pas` le montrait, menu ouvert : une entrée de
  menu fermé ne relit pas sa commande. La commande de I7 a désormais `EstUnBlocDeTexte`
  pour condition, que la sélection lui redit (`NotifierLaDisposition`) ; `Ctrl+U` s'y tait
  comme `Ctrl+G` sur une image, et le bouton S, gardé d'une sélection à l'autre (Q23), se
  rallume avec le bloc de texte.

La recette lit les deux corrections dans les sources — le style `Button.menu` sans focus,
aucune commande qui le reprenne, le clic qui rend le focus, lu dans le corps de
`SurCommande` et non ailleurs dans le fichier — et joue treize tests sous `poste=I3` au lieu
de onze. Neuf sabotages au juge, chacun attendu par la recette ou par
`BarreContextuelleTests`, que `eprouver-le-juge.sh I3` joue seuls : le bouton à état qui
s'inverse au clic ; le sélecteur de couleur ouvert hors de toute transaction ; `Échap` qui
garde le champ ; « Centrer le texte » qui aligne à gauche ; l'interligne resté au dock ; les
commandes de plusieurs blocs montrées pour un seul ; et, pour les deux défauts, la commande
qui prend le focus, celle qui ne le reprend pas au champ, et « Souligné » offert sans bloc
de texte.

*Éprouvé sans défaut* : la police choisie au pointeur, dans sa liste ouverte puis close,
fait un pas ; la molette ne change pas le corps sans le focus, et les flèches du clavier le
changent dans la transaction du champ ; un corps ou un interligne tapé sans validation,
puis un autre bloc pris, reste au bloc qu'on quitte ; les sortes du souligné cochent celle
du bloc après `Ctrl+U` et après une annulation ; un glissé sur la planche referme la
transaction d'un champ de la barre avant la sienne.

*Soupçons, non montrés.*
- Pour plusieurs blocs, « Gauche », « Centre » et « Milieu » de la barre doublent
  « Aligner à gauche », « Centrer horizontalement » et « Centrer verticalement » de la
  barre principale, sur la même commande et sous la même bulle. Le poste
  les demandait — « aligner et répartir pour plusieurs blocs » —, et son « rien n'y est
  doublé » parle du dock ; une commande n'est pas une propriété (règle 1). La décision
  revient au propriétaire.
- Le banc pose les menus volants sur la fenêtre, en surimpression ; l'application, sous
  X11, dans une fenêtre à part. Que le focus pris dans le sélecteur de couleur n'atteigne
  pas la barre — ce sur quoi sa transaction compte — n'est mesuré qu'au banc.

#### I4 — La planche

La planche de la maquette, à mécanique existante.

- **Fond** gris clair `#DCDFE4` au lieu de `#3A3D42`, et ombre de page double et légère.
- **Règles fixes** au bord haut et au bord gauche de la zone visible, de 20 px, graduées
  en millimètres de part et d'autre de la page : les valeurs sont négatives à gauche et
  au-dessus. Le zéro de chaque règle reste l'origine de la page. Les tests de C2 et de
  A1 se reformulent sur ce zéro, et non plus sur l'origine du contrôle ; `TopRuler` et
  `LeftRuler` gardent leur nom et leur geste.
- **Projection de la sélection** sur les deux règles, en bande d'accent à 14 %, suivie
  pendant le geste. Elle porte sur l'emprise du bloc (`Extent`), et non sur `X` et
  `Width` : un bloc tourné s'étend plus loin.
- **Repères** tracés sur toute la planche, en cyan `#12A4C4`, et non plus seulement dans
  la page, donc hors de `PageRoot` (règle 4). Lâché hors de la page, un repère est
  toujours retiré.
- **Marques** : marges en pointillé rose `#E3A6D6` ; cadre de sélection de 1,5 px
  d'accent, décalé de 4 px hors du bloc, qui tourne avec lui ; poignées carrées de 7 px,
  blanches bordées d'accent ; poignée de rotation en disque blanc bordé d'accent, au
  bout de sa tige. Le README, qui dit « disque vert », suit.
- **Bulle de dimensions** pendant le tracé et le redimensionnement : « L 102,9 mm ·
  H 138,4 mm », sous le bloc, sur fond sombre, dans l'`Overlay`. Elle s'ajoute à la barre
  d'état de A6, elle ne la remplace pas.

*Piège* : `RotationTests` prend pour poignée de rotation la première `Ellipse` visible de
l'`Overlay`. Rien de rond ne s'y ajoute avant elle : la bulle est un rectangle arrondi,
et la projection vit dans les règles.

*Piège* : inscrite à la liste des vues de la phase de la recette I1, la planche n'y écrit
plus aucune couleur de la maquette en dur, `White` compris. Or elle peint aujourd'hui la
page en `White` — `PageRoot` et la feuille qui le porte —, qui est le blanc du document
et non celui de l'interface. Le poste dit d'où il le tire, sans changer un pixel de
`PageRoot` (règle 4), et retire `PageCanvas.axaml` du relevé d'avant la phase.

*Recette* :
- `comparer-rendus.sh` et `comparer-mots.sh` passent sur l'exemple et sur ATA 21 sans
  changer de tolérance : rien n'a bougé dans `PageRoot` ;
- au banc, la projection d'un bloc tourné de 30° couvre exactement son emprise sur
  chaque règle, au pixel près ;
- un repère tiré de la règle du haut se voit à gauche de la page, sur la planche ;
  lâché sur la planche hors de la page, il disparaît ;
- pendant un redimensionnement, la bulle et la barre d'état disent la même taille.

Effort : moyen.

*Repris le 25 octobre 2026, après relecture.* La relecture a trouvé six défauts de
l'écran et huit trous du filet, chacun montré par une mesure ou par un sabotage qui
passait ; ils sont refermés.
- **La couche de saisie étendue faisait du gris un lieu de tracé et de prise.** Elle
  couvre la planche pour qu'une poignée hors de la page se saisisse. Mais un outil armé
  y posait, d'un clic à côté de la page, un bloc hors de la page, que `PageRoot` rogne
  tout entier — et la frappe partait dans un texte caché ; et un clic sur le gris
  prenait, et menait, la part rognée d'un bloc qui déborde. Blocs et outils s'arrêtent
  au bord de la page, comme avant le poste : sur le gris, un outil armé reste armé et un
  clic libère la sélection.
- **Un repère lâché sur le gris à moins de six pixels de la page restait**, aimanté sur
  le bord. Le retrait se décide sur la place où le pointeur le mène, et non sur celle où
  l'aimantation le pose. Et le repère traîné ne se décroche plus du bord un mouvement sur
  deux : il n'écarte des candidats que lui-même, et non toute valeur égale à la sienne.
- **Hors de l'échelle 1, repères et zéro des règles tombaient un pixel à côté.** Sous la
  mise à l'échelle, l'arrondi de mise en page décale `PageRoot` d'un pixel dans ce qui le
  porte, et une position passée par l'échelle tombe parfois juste sous l'entier —
  127,9999995 pour 128 à 64 %. Les deux sont lus sur `PageRoot` et arrondis avec un
  millième de rattrapage. Avalonia se trompe en portant un point de `PageRoot` droit sur
  une règle, qui n'est pas son ancêtre — 293,6 px au lieu de 281 à 30 % — : le coin est
  lu dans `Surface`, puis porté.
- **La bulle se posait décentrée** à la prise d'une poignée et au premier mouvement d'un
  tracé : mesurée cachée, elle valait zéro ; déjà mesurée, elle gardait la largeur de son
  texte d'avant. Elle est rendue visible, puis remesurée de force.
- **Les nombres des règles étaient dans la police du système** — `Typeface.Default`,
  Noto Sans sur la machine du banc —, et non en Inter, que la règle hérite désormais de
  la fenêtre, comme la bulle.

Le filet ne mesurait tout qu'à l'échelle 1, où un point vaut un pixel : une marge d'un
point au lieu d'un pixel, une règle sans l'échelle y passaient. Règles, projection, marge
et repère sont repris à l'échelle 2, repères et zéro des règles à 30 %, 41 %, 64 % et
« 100 % » atteint par les pas du zoom. La bulle est comparée à la barre sur un bloc
tourné, et en français, la culture posée le temps du test (règle 8) ; les libellés des
règles sont lus, signe compris ; le repère horizontal est lu à droite de la page et le
vertical sous elle ; la projection couvre une sélection de deux blocs.

Le test du repère qui traverse la planche échouait une passe complète sur deux. La cause
est établie : l'image composée du banc était en retard d'un tic — le repère manquait tout
entier à la première image et se trouvait, entier, à la suivante. L'écran était juste ;
l'image des tests de la planche fait passer un tic de plus, comme celle des tests de
l'apparence, et trente passes complètes de suite sont vertes.

Dix-sept sabotages de plus au juge, vingt-neuf en tout, chacun attendu par la recette ou
par `PlancheTests`. `eprouver-le-juge.sh I4` les joue seuls : leurs intitulés portent
désormais le code du poste, et le juge refuse un motif qui ne désigne aucun sabotage, au
lieu de se dire satisfait de zéro. Q10 était fait sans être dit : il est clos avec I4,
dont la recette devient `I4-Q10-planche.sh`.

#### I5 — Les vignettes de pages

Chaque page du panneau Pages se montre en vignette de 96 px de large, au rapport de la
page, avec son numéro dessous ; la page active est bordée de 2 px d'accent. La vignette
se dessine depuis la composition, comme celle des modèles depuis Q9, et non par des
`ControleDeBloc`, dont `DetachementTests` compte exactement deux.

Elle suit le document ouvert, qui se modifie en place. Le cache de `TemplatePreview` ne
convient pas tel quel : il ne s'invalide pas sur une modification en place, et ne
dessine que la première page. Si le poste le réutilise, les sept cibles du juge dans
`TemplatePreview.cs` restent présentes et uniques.

*Recette* :
- modifier un bloc de la page 2 change les pixels de la vignette 2, et non ceux de la
  vignette 1 ; une annulation les rend ;
- ajouter, dupliquer ou supprimer une page met la liste à jour, et un clic sur une
  vignette change la page courante ;
- `DetachementTests` et `VignetteTests` passent inchangés ;
- sur ATA 21, redessiner toutes les vignettes après une modification prend moins de
  50 ms (médiane de trois passes).

Effort : moyen.

*Fait le 25 octobre 2026* — relecture adversariale et juge après la fusion, selon la règle
du propriétaire : on fusionne dès que `verifier.sh` passe.

- **L'entrée du panneau** (`EntreeDePage.axaml`, le point d'accroche de I2) porte la
  vignette dans un cadre de 96 px, cadre compris, haut de 96 × hauteur ÷ largeur de la page
  (136 pour un A4, 68 à l'italienne) : un filet de 1 px au bord des champs et l'ombre de la
  maquette, 2 px d'accent pour la page courante. Le cadre de la page courante se prend sur
  le dedans : choisir une page ne déplace aucune vignette. Le numéro reste dessous, à 5 px.
  La hauteur suit le format, qui change en place, par un convertisseur à part
  (`Converters/VignetteConverters.cs`).
- **La vignette** (`Views/VignetteDePage.cs`) dérive de `TemplatePreview`, qui gagne ce
  qu'il lui fallait : la page dessinée (`Page`, la première quand elle est nulle — le
  catalogue ne change pas), l'oubli de ses filets, en entier ou pour un seul bloc posé sur
  la page, et trois réglages que seule la vignette d'une page change — pas de cadre gris,
  le fond de page du document, la feuille sur toute la boîte. Elle écoute ce que la
  planche écoute — chaque bloc, ses fragments et ses écarts de paragraphe, les enfants
  d'un groupe — et le document. Un bloc retouché fait recomposer ses filets seuls, ceux du
  groupe qui le porte s'il en a un ; un bloc posé ou retiré, un tableau retouché — ses
  cases peuvent changer de nombre —, le jeu de polices et le format font tout recomposer ;
  le fond de page et le jeu de couleurs, relus à chaque rendu, ne demandent qu'un dessin.
  Les abonnements se prennent à l'entrée dans l'arbre et se rendent à la sortie, comme
  ceux de `ControleDeBloc` (Q2).
- Le modèle de vue ne gagne rien : le clic sur une vignette est la sélection de la liste,
  liée à `CurrentPage` depuis I2. Pas de fichier partiel, donc (règle 10).
- `VignetteDePage.cs` entre à la liste des vues de la phase de la recette I1 ;
  `TemplatePreview.cs` non : son blanc est celui de la feuille, le document, que le relevé
  d'avant la phase garde — la même question que `PageRoot` pose à I4.

*Ce que l'implémentation a appris.*
- **La classe `vignette` était prise** : la fenêtre la donne aux vignettes de couleur de
  Fond et contour (I2), et son style — 22 × 22, une marge de 8 à droite — descendait
  jusqu'à l'entrée, qu'il décentrait de 4 px. La classe s'appelle `vignetteDePage`.
- **Une feuille centrée laissait un filet blanc** entre le cadre et un bandeau à fond
  perdu : la boîte est au rapport de la page à l'arrondi du pixel et au cadre près, pas
  exactement. La vignette d'une page étire sa feuille sur toute la boîte — moins de 1,5 %
  d'écart entre les deux échelles.
- **Tout recomposer coûte ce que coûte la composition elle-même** : 40 ms environ pour
  les dix-neuf blocs d'ATA 21 en Debug, dont 14 pour le seul « Ventilation » (1 099
  caractères), mesurés hors de la vignette, à l'appel de `Compositeur.Composer` que fait
  aussi la planche. Partager la règle à mesurer entre les blocs n'y change rien. D'où
  l'oubli bloc par bloc : la frappe dans un bloc ne recompose pas, à chaque lettre, les
  autres blocs de sa page.

*Mesuré.* Fidélité : la maquette rendue par Chrome sans tête en 1440 × 900 et la fenêtre
capturée dans le même état — l'exemple, un bloc de texte sélectionné — ont le cadre de la
première vignette aux mêmes pixels (x = 83, y = 167 à 302) et aux mêmes couleurs ; la
seconde vignette commence 1 px plus haut que dans la maquette, où le numéro, dans une
autre police faute d'Inter, prend 15 px de haut contre 14. L'ombre est un peu plus pâle
au pied (243 contre 237). Neuf tests sous `poste=I5`. Sur ATA 21 — deux pages, deux
vignettes —, médianes de trois passes après un tour à blanc, rendu à la demande de chaque
vignette : 14,1 à 14,4 ms après une retouche du plus long texte ; 42,6 à 43,2 ms après un
changement du jeu de polices.

*Choisi sans certitude.*
- **« Une modification »** est lue comme la retouche d'un bloc — la plus longue d'ATA 21 —,
  que le test tient sous 50 ms. Le changement du jeu de polices, qui recompose tout, passe
  aussi sous 50 ms seul (42,6 à 43,2), mais trop près pour un seuil joué à chaque passage
  de `verifier.sh` sous charge : le test le tient sous 100 ms, ce qui garde d'une
  composition faite deux fois ou à chaque rendu. Une lecture plus stricte de la recette
  — toute modification sous 50 ms — demanderait une composition plus rapide, que ce
  poste ne touche pas : la décision revient au propriétaire.
- La vignette montre le fond de page, que le catalogue ne montrait pas : sur une page
  colorée, une vignette blanche aurait menti. Un dégradé s'y donne par sa première couleur,
  une image par un gris, comme pour les blocs.
- Comme la planche, la vignette écoute les collections d'écarts d'un bloc de texte, et
  non chaque écart : un écart retouché en place, sans passer par sa collection, ne se
  verrait ni sur la page ni dans la vignette.

*Repris le 26 octobre 2026, après relecture.* Le poste avait été fusionné dès que
`verifier.sh` passait, sans relecture ni sabotage. La relecture adversariale a montré deux
défauts, chacun par une épreuve qui échouait sur le code fusionné ; ils sont refermés, et le
juge, qui n'avait aucun sabotage du poste, en a cinq.
- **Un Ctrl + clic sur la vignette courante laissait la fenêtre sans page courante.** La
  liste des pages, en sélection simple, rend sa sélection sous Ctrl + clic : `CurrentPage`
  devenait nulle — plus de planche, plus de cadre d'accent, « Page 0 » —, jusqu'au clic sur
  une autre vignette. Le test
  `Un_ctrl_clic_sur_la_vignette_courante_ne_laisse_pas_la_fenetre_sans_page` le montrait.
  La liste est désormais en `SelectionMode="AlwaysSelected"` (`MainWindow.axaml`) ; une
  annulation, qui remplace les pages, rend toujours la page d'avant.
- **La recette du poste rendait le juge aveugle.** Son étape 1c jouait
  `eprouver-le-juge.sh --cibles` ; or le juge, pour saboter, ôte justement l'un des textes
  qu'il vise, si bien que la recette échouait sous chacun des deux cent et quelques
  sabotages, quel qu'il fût — un sabotage du noyau appliqué à la main la faisait tomber sur
  « CIBLES À REPRENDRE : 1 sur 202 ». Elle « attrapait » donc tout : les sorties du juge
  nommaient « I5-vignettes » partout, et les sept premiers sabotages, qui ne nomment pas
  leur garde-fou, passaient pour pris sans qu'aucun test les eût vus. Sous le verrou du juge
  (`.juge-en-cours`), l'étape 1c ne lit plus les cibles et le dit ; hors du juge, elle les
  lit comme avant, et compte toujours les sept de `TemplatePreview.cs`. Les sept sabotages
  sans garde-fou, rejoués après la correction, sont tous pris par leurs propres tests.

La recette joue dix tests sous `poste=I5` au lieu de neuf. Cinq sabotages au juge, attendus
par la recette ou par `VignettesDePagesTests`, que `eprouver-le-juge.sh I5` joue seuls : la
vignette qui n'écoute plus ses blocs ; celle qui redessine un bloc retouché sans le
recomposer ; celle qui écoute encore sa page hors de l'arbre ; la page courante bordée d'un
seul pixel d'accent ; la liste qui se laisse vider par un Ctrl + clic. Joués avec les huit
sabotages du noyau que le motif des sept premiers retient, après la correction de la recette :
treize sabotages, treize pris par leur garde-fou. La suite d'interface passe (285 tests).

*Éprouvé sans défaut* : le texte retouché d'un enfant de groupe se voit dans la vignette ;
un bloc de texte masqué, puis le suivant retouché, puis le premier remontré, se voient
chacun.

*Soupçon, non montré.* Choisir une page fait passer son cadre de 1 à 2 px pris sur le
dedans : la boîte des deux vignettes, l'ancienne et la nouvelle courante, change de taille,
et leurs filets se recomposent en entier — ce que coûte un changement du jeu de polices,
43 ms sur ATA 21. Ce n'est pas une modification au sens de la recette, et rien ne l'a vu se
faire sentir.

#### I6 — Naviguer dans la planche, et garder les proportions

Les gestes de la maquette qui n'existent pas encore.

- **Main** : l'outil, ou `Espace` tenu, fait glisser la planche — le défilement de son
  `ScrollViewer` — sans toucher à aucun bloc. Le curseur en main sert déjà à la
  rotation : celui de l'outil est la main ouverte, puis fermée pendant le glissé.
- **Zoom** (`Z`) : un clic zoome en avant, `Alt`+clic en arrière, et le point visé reste
  sous le pointeur. `Ctrl`+molette fait de même, ce qu'elle ne fait pas aujourd'hui.
- **Page entière** : la page tient dans la zone visible, règles et marge de la planche
  déduites, dans les bornes de `SetZoom`. Jamais d'office (règle 5).
- **Conserver les proportions** : la case posée par I2. C'est un état de la session et
  non du document : elle ne passe ni par l'historique ni par l'état « modifié ».
  Cochée, le redimensionnement par une poignée de coin et la saisie de L ou de H gardent
  le rapport du bloc ; les poignées de côté non, comme dans Publisher. `Maj` pendant le
  geste fait l'inverse de la case.

`Espace` et `Z` se rangent avec les lettres seules : ils ne jouent que hors saisie, et pas
quand un bouton a le focus, puisque `Espace` l'actionnerait.

*Recette* :
- `Espace` tenu et un glissé de 100 px décalent le défilement de 100 px ; aucun bloc n'a
  bougé, et l'historique est vide ;
- un clic de l'outil Zoom en un point de la page : le même point de la page est sous le
  pointeur avant et après, à 1 px près ;
- « Page entière » sur un A4 et sur un format paysage : la page tient, et l'un de ses
  côtés touche la zone, à la marge près ;
- case cochée, un coin tiré de 20 mm garde le rapport à 0,01 près, en un pas
  d'annulation, et la case ne rend pas le document « modifié ».

Effort : moyen.

*Livré le 25 octobre 2026* ; la relecture adversariale et les sabotages du juge viennent
après la fusion. *Choisi sans certitude* :
- la case reste décochée au départ, comme I2 l'a posée : la maquette la montre cochée,
  mais elle ne fait pas foi pour un état. Décochée, un coin se tire librement, comme avant
  le poste, et `Maj` garde le rapport ;
- `Maj` inverse la case pendant le glissé d'un coin ; à la saisie de L ou de H, la case
  seule décide — une touche tenue ne se lit pas au moment où un champ valide ;
- la main fermée n'est ni dans le jeu de curseurs d'Avalonia ni dans la maquette : la
  planche la dessine, d'après la main ouverte de l'apparence ; la loupe porte un « + », ou
  un « − » sous `Alt`. Le zoom au clic suit le pas des boutons de la barre (× 1,25), la
  molette garde le sien (× 1,1).

*Relevé, non corrigé* — le premier point corrigé à la relecture, voir plus bas. Une prise de
la main suivie, au même point et dans le délai du double-clic, d'un clic ordinaire fait un
double-clic, qui ouvre la saisie du bloc
sélectionné ; le banc, dont l'horloge n'avance pas, l'a montré. Et le curseur du zoom de
la barre d'état (I2), lié dans les deux sens en échelle logarithmique, rend
7,999999999999998 pour la borne de 8.

*Repris le 26 octobre 2026, après relecture.* Le poste avait été fusionné dès que
`verifier.sh` passait, sans relecture ni sabotage. La relecture adversariale a montré un
défaut — celui que la livraison avait relevé sans le corriger — par un test qui échouait sur
le code fusionné ; il est refermé, et le juge, qui n'avait aucun sabotage du poste, en a
sept.
- **Une prise de la main faisait la moitié d'un double-clic.** `Espace` tenu, une prise sur
  le bloc de texte sélectionné ; `Espace` relâché, un clic au même point dans le délai du
  double-clic : les deux pressions faisaient un double-clic, qui ouvrait la saisie du bloc
  — la main touchait au bloc. Le test
  `Une_prise_de_la_main_puis_un_clic_au_meme_point_n_ouvrent_pas_la_saisie` le montrait. La
  planche retient si ses deux dernières pressions étaient de la main ou de la loupe, et un
  double-clic dont la première pression naviguait n'ouvre plus la saisie ; deux clics
  ordinaires, ensuite, l'ouvrent comme avant, ce que le même test vérifie. Le double-clic
  de contrôle de `L_outil_Main_du_rail_fait_defiler_la_planche_main_ouverte_puis_fermee`
  se joue désormais à un point neuf : l'horloge du banc n'avançant pas, il se comptait avec
  la dernière prise de la main au même point, et c'est justement ce que la correction
  refuse. Ce qu'il prouve ne change pas.

La recette joue dix tests sous `poste=I6` au lieu de neuf. Sept sabotages au juge, attendus
par la recette ou par `NavigationTests`, que `eprouver-le-juge.sh I6` joue seuls : la main
qui ne fait plus défiler ; la loupe qui perd le point visé ; `Ctrl` + molette laissée à la
zone qui défile ; « Page entière » qui ne remesure pas la feuille ; la case qui n'entraîne
plus l'autre côté à la saisie ; le coin tiré qui l'ignore ; la prise de la main qui fait la
moitié d'un double-clic. Joués seuls, les sept sont pris par leur garde-fou ; la suite
d'interface passe (285 tests).

*Éprouvé sans défaut* : un `Alt` + clic de la loupe zoome en arrière sans que le
relâchement d'`Alt` ouvre le menu ni lui donne le focus, et `Z` frappé ensuite arme encore
la loupe ; `Espace` sur la liste des pages ne change pas de page.

*Soupçons, non montrés.*
- La planche n'écoute pas la perte de capture du pointeur : une main dont le relâchement
  n'arriverait pas — la fenêtre quittée pendant le glissé — resterait en glissé jusqu'au
  relâchement suivant. C'est le cas de tous les gestes de la planche depuis avant la phase.
- Case cochée, un coin tiré sur un bloc très étroit garde le rapport au prix de la taille
  minimale d'un côté : la conservation des proportions sous `Maj` le faisait déjà, la case
  le rend seulement plus fréquent. La saisie de L ou de H, elle, remonte l'échelle
  (`AuMemeRapport`).

### Lot 2 — Ce que le modèle doit apprendre

Quatre ajouts additifs, **en série** : tous touchent `Frame.cs`, et trois
`PdfExporter.cs`. A10, l'objet trait, déjà spécifié en phase A, passe de la salve 5 à ce
lot, pour l'outil Ligne du rail ; I2 pose alors son bouton, et le menu Insertion gagne
« Trait ».

#### I7 — Le souligné

Le souligné se porte sur le bloc de texte, sur le fragment de style (R1) et dans le style
effectif. Il se dessine à l'écran et au PDF à la position et à l'épaisseur que donne la
police. Il a le bouton S de la barre contextuelle et `Ctrl+U`, que pose I3. *Repris le 24
octobre 2026* : I7 part en même temps que I2 et I4, qui réécrivent la fenêtre et la planche ;
il n'y touche pas, et livre le modèle, l'écran, le PDF, l'import et la commande du modèle de
vue, dans son fichier partiel. L'import `.pub` le lit si
libmspub le donne. *Vérifié à la livraison* : aucun des deux documents ATR ne souligne —
`pub2raw` n'en rend aucun `style:text-underline-*` —, ni aucun des cinq documents du CD de
Publisher 98. La recette éprouve donc l'import sur un ATA 21 dont elle souligne dix-sept
styles de caractère, un par sorte.

Au-delà du drapeau, le souligné porte sa sorte, comme dans OpenDocument : le style du trait,
simple ou double, son épaisseur, et « mots seuls ». C'est ce qui permet de relire seize des
dix-sept sortes que libmspub transmet par `fillUnderline` (`MSPUBCollector.cpp:254-330`),
au lieu de les ramener toutes à un trait simple. *Tranché à la livraison* : la forme dans le
fichier est une valeur unique — `false`, `true` pour le trait simple, celui de `Ctrl+U`, ou
un objet `{ "Line", "Double", "Thick", "WordsOnly" }` pour toute autre sorte ; au fragment,
`null` laisse le souligné du bloc. Un drapeau suivi de trois réglages aurait écrit quatre
clés de plus sur chaque bloc et chaque fragment de tous les documents, et laissé écrire des
réglages sans trait. Relevé par l'étude de Publisher 98, où ce complément portait le code T6
([`Publisher98/02-typographie.md`](Publisher98/02-typographie.md)).

*Recette* :
- `"Underline": true` fait l'aller-retour JSON, au bloc et au fragment ; un document sans
  souligné se réécrit au caractère près, une fois les Samples réenregistrés ;
- sur un document de recette souligné, le trait est à la même ordonnée à l'écran et au
  PDF à 0,25 pt près, et `comparer-rendus.sh` passe sous la tolérance ;
- le souligné suit la frappe comme le gras (Q1), et une recopie du bloc le garde
  (`Clone`) ;
- un document de recette porte une ligne par sorte, dont une justifiée : chaque trait a
  l'épaisseur que donne la table `post` de la police, et « mots seuls » ne laisse aucun
  trait sous les blancs, à l'écran comme au PDF ;
- un `.pub` dont libmspub rend `style:text-underline-type: double` s'importe en double.

Effort : moyen à fort.

*Repris le 25 octobre 2026, après relecture.* La relecture a trouvé huit défauts. Sept
sont refermés dans le code ou dans le filet ; le huitième était ce plan, resté à 0 % et
muet sur les deux points que la section laissait ouverts.
- **Un sabotage du juge ne compilait plus.** Celui de la vignette (Q9) appelait encore
  `StyleEffectif` à six paramètres : il échouait à la compilation, et le juge comptait un
  trou. Il passe maintenant le souligné du bloc, et Q9 l'attrape de nouveau.
- **Deux motifs voisins s'échangeaient sans rien faire échouer.** La recette ne mesurait
  que la part de la ligne couverte : 67 et 73 % pour le tiret et le tiret long, 56 et 50 %
  pour le tiret-point et le tiret-point-point. À 0,1 près, elle ne les séparait pas, et le
  test ne vérifiait que cinq motifs distincts. La recette lit maintenant le dessin le long
  de la ligne — la longueur des tirets en épaisseurs, le nombre de points par tiret —, et
  le test tient la valeur de chaque motif.
- **Le souligné d'un exposant n'était éprouvé nulle part.** Le document de recette porte
  « Voir la note 12 », « 12 » au corps 12, monté d'un tiers de corps. La recette mesure
  son trait sous sa ligne de base décalée, à l'épaisseur de son corps, à l'écran et au
  PDF.
- **La couleur du trait ne l'était pas non plus.** Un PDF qui soulignait en noir un texte
  de couleur passait : la recette ne lisait que des niveaux de gris. La même ligne est en
  rouge, et la recette mesure la teinte des pixels pleins du trait sur l'image en couleur.
- **« Mots seuls » soulignait une insécable au milieu d'un morceau**, et la laissait nue
  au bout d'un fragment, selon une frontière que rien ne montre. Une insécable est un
  blanc : le trait est coupé autour de chaque blanc du morceau.
- **Une police sans place de souligné posait le trait sur la ligne de base.** Elle le
  reçoit à un dixième de corps dessous, comme elle recevait déjà une épaisseur d'un
  vingtième de corps. Aucune police de la recette n'est dans ce cas : un test le tient.
- **Le juge prenait pour un succès un motif qui ne retenait rien.** « I7 » n'en retenait
  aucun : les intitulés ne portent pas le code du poste. Le motif porte maintenant aussi
  sur le garde-fou attendu, qui le porte (`I7-souligne`), et un motif qui ne retient rien
  fait échouer le juge.

Huit sabotages de plus, vingt-six en tout. Les six que la recette doit voir ont été joués
contre elle seule avant d'être écrits : les deux échanges de motifs, l'exposant et la
couleur de chaque côté.

#### I8 — Masquer un bloc

C'est l'œil de « Blocs de la page ». Un bloc masqué n'est pas dessiné à l'écran ni
exporté ; on ne peut pas le saisir sur la planche, et il ne sert ni à l'aimantation ni à
l'alignement. Il reste dans la liste, l'œil fermé, et s'y sélectionne. Le basculer est un
geste qui s'annule et rend le document « modifié » : c'est une propriété du document, pas
de la vue. Par défaut, un bloc est visible ; les cases d'un tableau, faites de blocs de
texte jetables, ne voient pas la propriété.

*Recette* :
- `"Visible": false` fait l'aller-retour JSON ; une recopie et un groupe la gardent ;
- un bloc masqué n'apparaît ni au PDF (`pdftotext` et décompte des tracés) ni dans
  `PageRoot`, et un clic à sa place prend le bloc d'en dessous ;
- l'export n'avertit pas des polices d'un bloc masqué (Q8 : seules les polices
  dessinées) ;
- un pas d'annulation rouvre l'œil.

Effort : faible à moyen.

*Tranché à la livraison*, là où la section se taisait. La maquette ne dessine que l'œil
ouvert : l'œil fermé en garde le trait et la couleur, paupière baissée et trois cils, dans
la fenêtre et non dans l'apparence, dont la recette ne relève que les icônes de la
maquette. Un bloc masqué sélectionné dans la liste n'a ni cadre ni poignée sur la planche —
une poignée tirée retoucherait ce que rien ne dessine ; il reste sélectionné seul, mais
quitte une sélection multiple quand on ferme son œil, et « Tout sélectionner » ne le prend
pas : une sélection de plusieurs blocs ne porte que ce que la planche saisit, et Suppr ou
un glissé n'emportent rien qu'on ne voie. Dissocier un groupe masqué laisse ses blocs
masqués : dissocier ne fait rien paraître. L'aimantation n'est pas touchée : elle ne vise
encore aucun bloc (A11), et un bloc masqué ne se tire pas ; A11 dira qu'un bloc masqué
n'attire rien. libmspub ne transmet aucun bloc masqué : un `.pub` s'importe tout visible.

#### I9 — Compter les lignes en excédent

C'est l'alerte de la maquette au dock : « Texte en excédent : 4 lignes ». Le noyau sait
qu'un bloc déborde, mais il le mesure en points (A2). Il lui manque une fonction pure du
`Compositeur` qui compte les lignes composées que le bloc ne montre pas, la même pour
l'écran et pour l'export. L'avertissement d'export garde sa formulation, et le relevé de
P2 ne bouge pas.

*Recette* (test pur, puis interface) :
- un bloc de dix lignes qui en montre six donne quatre ; un bloc qui ne déborde pas
  donne zéro, sans alerte ;
- sur `recette-debordement.mypub`, le compte du dock est celui des lignes absentes du PDF
  (`pdftotext` contre le texte du bloc) ;
- l'alerte suit la frappe, sans attendre un changement de sélection.

Effort : faible.

*Livré le 25 octobre 2026* ; la relecture adversariale et les sabotages du juge viennent
après la fusion. *Choisi sans certitude* :
- une ligne est montrée quand son encre tient dans le bloc — sa hauteur naturelle, que les
  deux rendus posent au milieu de son avance —, au demi-point près du marqueur. Une ligne
  que le bord coupe est en excédent tout entière : on n'en lit que le haut, et Publisher la
  renverrait. C'est ce que mesure « absente du PDF » : `pdftotext` ne voit pas la découpe,
  la recette lit la boîte de chaque mot, ramenée au repère du bloc ;
- l'export ne compte pas : son avertissement garde sa formulation en points. La fonction
  est la même pour l'écran et pour l'export au sens où une composition faite à la règle du
  PDF donne le même compte — un test de la fenêtre le vérifie bloc par bloc, la recette le
  confronte au PDF ;
- le modèle de vue compose le bloc comme `ControleDeBloc.Mesurer`, dans son partiel, plutôt
  que de partager le code de la planche : partager touchait un fichier que les postes partis
  en même temps peuvent toucher. Il se branche sur ce qu'il annonce de lui-même
  (`OnPropertyChanged`, surchargé dans le partiel), sans une ligne dans
  `MainWindowViewModel.cs` ; un second poste qui surchargerait la même méthode ne
  compilerait pas, et la fusion le dirait ;
- le banc gagne une commande, `excedent`, qui sélectionne chaque bloc de texte dans la vraie
  fenêtre et lit l'alerte affichée : la recette confronte ce qu'on lit, et non un compte
  recalculé à côté.

*Relevé, non corrigé.* La mesure de A2 descend jusqu'au bas de l'avance de la dernière
ligne, espace après le paragraphe compris. Un bloc dont seuls le demi-interligne d'un
interligne lâche, ou l'espace après le dernier paragraphe, sortent porte le marqueur rouge
et l'avertissement « ce qui dépasse n'est pas imprimé », alors qu'aucune encre n'est coupée
et que le dock ne compte rien. La variante de la recette le montre : « Interligne lache »,
trois lignes en interligne 1,5 dans 55 pt, annoncé à 2,6 pt, aucune ligne absente. Aligner
A2 sur l'encre changerait des avertissements de l'export : c'est au propriétaire d'en
décider.

*Repris le 26 octobre 2026 après relecture.* Un défaut, montré par un test pur écrit avant
sa correction, et huit trous du filet.

- **À un interligne serré, le compte annonçait une ligne que le marqueur laissait en
  paix.** La barre descend à 0,5 ; sous 1, la hauteur naturelle d'une ligne déborde son
  avance, en haut et en bas — c'est le chevauchement des lignes d'un titre serré. Le compte
  prenait ce débord pour de l'encre perdue : quatre lignes à 0,8 dans un bloc qui tient
  leurs avances — ni marqueur, ni avertissement à l'export, seul le bas des jambages de la
  dernière sous le bord — en comptaient une en excédent. La promesse du poste, « une ligne
  comptée fait toujours poser le marqueur », ne tenait qu'aux interlignes que son test
  parcourait (1, 1,15 et 1,5). L'encre se compte désormais dans l'avance de la ligne, jamais
  au-delà ; à l'interligne 1 et au-dessus rien ne change, et la recette donne contre le PDF
  les mêmes comptes qu'avant (`Un_interligne_serre_ne_compte_pas_ce_que_le_marqueur_laisse_en_paix`,
  qui reprend la boucle de la promesse à 0,5, 0,8 et 0,9).
- **Le juge.** Le poste était fusionné sans un sabotage. Dix-sept sont posés : le compte pur
  — l'avance pour l'encre, le bord de la zone pour celui du bloc, le seuil, le calage, et
  l'interligne serré —, puis ce que le modèle de vue compose et écoute, et l'alerte. Huit
  passaient avant la relecture : le compte compose le bloc de son côté, et rien ne vérifiait
  qu'il le compose avec ce que la planche lui donne — fragments, écarts de paragraphe, jeu
  de polices du document, contenu résolu d'un champ —, ni qu'il le recompose quand l'un
  d'eux change sans que la sélection change. Trois tests d'interface les attrapent :
  `L_alerte_suit_les_fragments_et_les_ecarts_du_bloc`,
  `L_alerte_suit_le_jeu_de_polices_et_son_annulation` (« Sobre » puis « Édition », à la
  police de corps plus étroite) et `Un_champ_se_compte_par_ce_qu_il_affiche_et_suit_les_coordonnees`
  (le numéro de page d'un bloc dont le texte saisi déborderait ; une adresse de huit lignes
  adoptée sans changer la sélection). Joué le 26 octobre 2026 : dix-sept sur dix-sept. La suite d'interface passe entière (287 tests), celle du noyau aussi (460), et la recette.
- *Sans sabotage* : le cas « Document » de l'écoute ne se voit d'aucun geste — l'annulation
  et l'ouverture changent la sélection après le document, et le compte suit par elle. Il est
  redondant, et gardé.
- *Soupçon, non montré* : le compte recompose le bloc à chaque propriété qu'il annonce, sa
  position comprise ; un glissé le recompose à chaque pas du pointeur, à côté de la planche.
  Q23 a mesuré une composition de 1 à 2,6 ms sur ATA 21.
- *Question au propriétaire* : un bloc masqué (I8) pris dans la liste porte encore son
  alerte au dock, alors que rien de lui ne s'imprime ni n'avertit ailleurs.

---

## Phase N — La saisie de Publisher

*Ouverte le 25 octobre 2026*, à la demande du propriétaire : « atteindre les mêmes modalités de
saisie que Publisher », en s'aidant de l'étude du cours de 2007. La relecture du livre sous cet
angle est la [fiche 23](Publisher2007/23-modalites-de-saisie.md) : 158 modalités, dont 31 faites,
12 où MyPublisher fait mieux, 55 partielles et 60 manquantes. Elle renverse un verdict des
fiches. Le geste qui **pose** un bloc est meilleur que celui de Publisher ; celui qui
**l'écrit**, non. Pendant l'édition, l'écran montre un `TextBox` en texte brut à la place de
`TexteCompose` ; on y entre au double-clic, et tout le texte est alors sélectionné ; la barre, le
menu Texte et `Ctrl+G` ne visent que le bloc entier. Il n'y a ni clic droit, ni presse-papiers
des blocs, ni Ctrl+glisser, ni rectangle de sélection, et les champs refusent en silence.

La phase reprend les postes ouverts qui sont déjà des modalités de saisie — A8, A9, A11, A12,
A13, Q21, Q22, R6, R7 — avec ce que la fiche 23 leur ajoute, écrit dans leur propre section, et
ouvre vingt-huit postes N, numérotés dans l'ordre où ils partent. Les deux défauts qu'elle a
trouvés en chemin sont rangés dans la phase Q : Q24, le seul qui peut perdre des données, et Q25.

**Six décisions du propriétaire, le 25 octobre 2026.**

1. **Le périmètre est tout, le texte en tête.**
2. **La voie de R7 se mesure, puis se décide.** N7 mesure l'écart entre le curseur du `TextBox` et
   le caractère composé ; sous le pixel, le `TextBox` transparent posé sur `TexteCompose` ;
   au-dessus, on s'arrête et le propriétaire choisit. C'est l'arrêt de la phase.
3. **On entre dans le texte d'un clic simple**, comme Publisher : le curseur tombe au point visé,
   un glissé dans le texte sélectionne du texte, le bloc se déplace par son bord. La fiche 10, qui
   comptait le double-clic parmi les acquis, est reprise.
4. **Les raccourcis sont ceux de Publisher 2007 en français**, relevés sur l'aide de Microsoft en
   français et écrits dans la fiche 24 (N10).
5. **Le dock reste.** Les chemins de Publisher y mènent : clic droit ▸ « Format du bloc… »,
   double-clic sur une image ou une forme, entrées « … » des menus. Aucun dialogue OK/Annuler ne
   double un champ du dock (règle 1).
6. **Quatre fonctions entrent dans la phase** : puces et numérotation ; exposant, indice et
   capitales ; rechercher et remplacer ; vérification orthographique.

**Les règles de la phase.** Les onze règles de la phase I valent pour chaque poste. S'y ajoutent :

12. **La touche de Publisher en français l'emporte quand sa fonction existe dans MyPublisher.**
    Une touche que le bureau capte garde son remplaçant : Premier plan et Arrière-plan restent sur
    `Ctrl+Maj+F` et `Ctrl+Maj+B`, `Alt+F6` étant pris (A4). Une touche dont la fonction n'existe
    pas reste réservée et libre, jusqu'au poste qui la sert : `Ctrl+M`, `Ctrl+K`, `Ctrl+Maj+E`,
    `Ctrl+Maj+H`. Il s'ensuit que `Ctrl+E` centre et que l'export passe sur `Ctrl+Maj+P`,
    que `Ctrl+Maj+G` groupe et dissocie, que `Ctrl+B` est « Atteindre la page », et que `Ctrl+=`
    passe à l'indice avec N24.
13. **Échap dans un champ renonce** à la valeur non validée, comme Office, puis rend la main à la
    planche. C'est l'inverse de ce que I3 avait choisi pour la barre ; N14 le reprend.
14. **Un poste qui change le sens d'un geste réécrit les tests qui le jouaient**, et le dit ; il ne
    les retire pas. Quand une cible du juge perd sa prise, le poste lui en rend une.
15. **`FormatVersion` reste à 5.** Les clés de la phase — langue, puces, capitales — sont
    additives, au précédent de `Columns`, et chaque poste qui en ajoute une réenregistre `Samples/`
    et `Templates/`. Le saut de ligne forcé est un caractère, U+2028, et non une clé.
16. **Jamais deux postes d'une même salve dans `PageCanvas.axaml.cs`** : la planche se modifie en
    série. `MainWindow.axaml`, `MainWindow.axaml.cs` et le README se partagent quand les postes y
    touchent des régions différentes, nommées dans chaque poste ; leurs conflits se règlent à la
    fusion. C'est pour cela que le tableau des raccourcis du README est rangé, depuis
    l'ouverture de la phase, en sous-tableaux par domaine.

**Ce que la phase ne fait pas.** Ce qu'Office 2007 apportait et qui a vieilli, ce que les
décisions excluent, et ce qui reste au seuil D, est écrit dans
[Ce que ce plan ne fait pas](#ce-que-ce-plan-ne-fait-pas). Le style nommé, la graisse et les pages
maîtres restent non planifiés, comme en phase I.

**Quatre lots**, en quatorze salves (voir [Ordre d'exécution](#ordre-dexécution)) : la sûreté et
les préalables ; le clavier et le clic de Publisher ; le clic droit, la recherche et le texte tel
qu'il s'imprime ; les postes lourds de fin.

### Lot 1 — Sûreté et préalables

Rien de ce lot ne change ce qu'on voit, sauf N4, N5, N6 et N8 ; tout ce qui suit en dépend. Q24 et
Q25, qui partent dans la même copie, sont décrits dans la phase Q.

#### N1 — Le banc des gestes *(fiche 23)*

*Spécifié le 25 octobre 2026.* Les tests d'interface synthétisent leurs gestes par des aides
recopiées de classe en classe :
- `SurLaPage`, treize fois, toutes à l'échelle 1, dont dix retombent en silence sur (0, 0) quand
  `PageRoot` manque ;
- `Cliquer`, six fois, dont deux sans l'attente d'image de `Rendu.Cliquer` (`NavigationTests`,
  `VignettesDePagesTests`) — le défaut que I3 a payé ;
- `Glisser`, trois fois, dont deux sans `LeftMouseButton` pendant le mouvement (`NavigationTests`,
  `ReperesTests`) ; or un `TextBox` n'étend une sélection que si le mouvement dit le bouton tenu ;
- `UnSeulPas`, quatre fois ; l'entrée en saisie par double-clic, cinq fois.

L'en-tête de `Rendu` pose la doctrine contraire : « deux copies, et la seconde oublie
l'assertion ». La phase va changer le sens de ces gestes (N12) : il faut qu'elle le fasse à un
seul endroit.

Le poste remonte dans `Rendu.cs` une aide par geste : `SurLaPage`, à l'échelle, qui échoue si
`PageRoot` manque ; `Centre`, `Coin`, `Poignee` ; `Cliquer(point, touches, bouton, mouvementNul)` ;
`CliquerDroit`, qui rend le `ContextMenu` ouvert ; `DoubleCliquer` ; `Glisser`, toujours bouton
tenu ; `Touche` et `ToucheAzerty` ; `SelectionnerDuTexte(zone, début, fin)` ; `EntrerEnSaisie`,
le seul endroit que N12 changera ; `ZoneDeSaisie` ; `UnSeulPas` ; `TexteCopie`. Les copies sont
retirées, et aucun test ne change d'issue.

Ce qu'Avalonia 11.3.9 permet sans affichage, lu dans son code : un `ContextMenu` s'ouvre au
relâché d'un clic droit dont la source est le contrôle lui-même, en surimpression dans la fenêtre
(`HeadlessWindowImpl.CreatePopup` rend `null`), et `Rendu.Cliquer` atteint ses entrées ; la touche
Menu l'ouvre aussi, `Maj+F10` non. Le presse-papiers est un magasin en mémoire, formats propres
compris. `KeyPress(Key, modificateurs, PhysicalKey, symbole)` simule une touche AZERTY. L'horloge
est un vrai chronomètre : un double-clic, ce sont deux pressions à moins de 500 ms et de 2 px. Ce
qui ne s'éprouve pas : le presse-papiers du système, les touches que prend le bureau, la touche
que X11 livre pour « < » sur un AZERTY, l'allure d'un curseur.

*Piège* : migrer copie pour copie. Le `Cliquer` de `DoubleClicTests` envoie volontairement un
mouvement nul, celui de Q14 ; l'aide le garde, en paramètre.

*Recette* :
- `BancTests`, sous `poste=N1` : un clic droit ouvre un `ContextMenu` en moins de dix images, et
  son entrée joue sa commande ; la touche Menu l'ouvre ; un texte et un format propre font
  l'aller-retour par le presse-papiers ; `Ctrl+C` dans un `TextBox` y met la sélection ; un glissé
  du 4ᵉ au 7ᵉ caractère sélectionne [4, 7), et le même glissé sans le bouton tenu ne sélectionne
  rien ; `Key.A` sur `PhysicalKey.Q` sous Ctrl déclenche une liaison `Ctrl+A` ; deux clics à 3 px
  ne font pas un double-clic ;
- la suite garde son nombre de tests sous chaque `poste=`, et les recettes Q14 et Q23 leurs vingt
  passages ;
- un contrôle des sources : aucune aide de geste n'est plus définie hors de `Rendu.cs` ;
- au juge : `Glisser` sans `LeftMouseButton` ; `EntrerEnSaisie` qui ne clique qu'une fois.

Fichiers : `MyPublisher.Tests.Interface/` seul. Effort : faible à moyen.

*Livré le 26 octobre 2026.* Aucune ligne de `MyPublisher/` ni de `MyPublisher.Core/` ne change.

**Les aides**, dans la région « Les gestes » de `Rendu.cs`, une par geste. Où viser :
`SurLaPage(fenetre, x, y)`, lue comme `NavigationTests` la lisait — le coin de `PageRoot` dans
`Surface`, puis l'échelle de la planche —, qui échoue en le disant si `PageRoot` manque ;
`Centre(fenetre, bloc)` et `Centre(controle)` ; `Coin(fenetre, bloc, fx, fy)` ; `Poignee(fenetre,
bloc, fx, fy)`, la poignée de redimensionnement, 3,25 px d'écran hors du bloc ;
`PoigneeDeRotation(fenetre)` et `CentreDeLaRotation(fenetre)`, relues dans l'`Overlay`. La souris :
`Cliquer(controle)`, celui de I3, qui attend que l'image montre le contrôle ; `Cliquer(racine,
point, touches, bouton, mouvementNul)`, qui finit par une image (`UneImage`) ;
`CliquerDroit(controle)`, qui rend le `ContextMenu` ouvert, et `MenuOuvert(racine, geste)`, qui
l'attend dix images au plus ; `DoubleCliquer(racine, point)` ; `Glisser(racine, depart, arrivee,
touches, pas = 2)`, toujours bouton tenu. Le clavier : `Touche(racine, place, touches, relacher)` en
QWERTY, `ToucheAzerty` avec la même signature, `Taper(racine, texte)`, `Saisir(fenetre, champ,
texte)`. L'inverse de `SurLaPage` : `SousLaFenetre(fenetre, point)`. Le texte :
`ZoneDeSaisie(fenetre, bloc)`, `EntrerEnSaisie(fenetre, vm, bloc, dx = 40, dy = 8)`, qui rend la
zone et que N12 sera seul à changer, `BordDuCaractere(zone, indice)`, `SelectionnerDuTexte(zone,
debut, fin)`, `TexteCopie(racine)`. L'annulation : `UnSeulPas(vm, avant, quoi)`, qui rend le bloc
sélectionné. `Attendre`, qui fait tourner la file le temps d'une tâche du presse-papiers, est hors
de la région : ce n'est pas un geste.

**La migration**, copie pour copie, inventoriée par le contrôle même de la recette : quarante-trois
définitions hors de `Rendu.cs`, dans dix-sept classes. Quarante et une copies sont retirées — les
treize `SurLaPage`, les six `Cliquer`, les trois `Glisser`, les quatre `UnSeulPas`, les quatre
`Saisir`, les deux `EntrerEnSaisie` et la fonction locale de `NavigationTests` qui double-cliquait,
`ZoneDeSaisie`, les `Centre`, la poignée de rotation et son centre, `DansLaFenetre` de
`NavigationTests`, l'`Image` de `PressionTests` ; quatre aides de classe sont réécrites sur celles
du banc — `Tracer`, les deux `Tourner`, `TaperEnTeteDejaEnSaisie` — et `TirerLaPoignee` compose
`Poignee` et `Glisser` ; la fonction locale `Centre` de `DispositionTests`, qui lit une couleur, est
renommée `CouleurAuCentre`, un homonyme comptant pour une copie. Les deux entrées en saisie écrites
en ligne (`ExcedentEcranTests`, `SouligneEcranTests`) passent par `EntrerEnSaisie`. Les défauts que
les copies portaient sont corrigés au passage : `NavigationTests.Glisser`, `ReperesTests.Glisser` et
`RotationTests.Tourner` disent le bouton tenu ; `NavigationTests.Cliquer` et
`VignettesDePagesTests.Cliquer` attendent l'image ; dix `SurLaPage` ne retombent plus sur (0, 0) ;
les `UnSeulPas` de `NavigationTests` et de `TraitTests` vérifient, comme les deux autres, que le pas
a changé le document. Aucun test n'en change d'issue : aucun ne passait pour une mauvaise raison. Le
mouvement nul de `DoubleClicTests.Cliquer` est le paramètre `mouvementNul`, et la recette de Q23,
qui lisait le premier clic de ses deux tests du double-clic, lit désormais celui du banc. À la
fusion d'`interface`, les aides que Q24, Q25 et N4 avaient écrites en tête de leurs classes ont
suivi : sept dans `TransactionsDeSaisieTests` — `SurLaPage`, `Cliquer`, `Glisser`, `Touche`,
`ZoneDeSaisie`, `EntrerEnSaisie`, `UnSeulPas` —, et dans `AffichageTests` `DansLaFenetre`, `Frapper`
— `Touche` qui relâche — et `SousLaFenetre`, qui rejoint le banc avec celle de `NavigationTests`,
dont elle était la copie. `EntrerEnSaisie` vérifie désormais, comme celle de Q25, que la zone a le
focus.

**Le banc**, `BancTests`, dix tests sous `poste=N1`, dans une fenêtre nue pour sept d'entre eux, la
vraie pour trois. Ce qu'Avalonia 11.3.9 permet sans affichage, et que le banc éprouve : un clic
droit ouvre le `ContextMenu` au relâché — le contrôle atteint lève `ContextRequested`
(`Control.OnPointerReleased`), qui remonte au premier qui porte un menu —, posé en surimpression sur
la fenêtre, et `Cliquer` atteint son entrée, qui joue sa commande ; la touche Menu l'ouvre au
relâché (`Control.OnKeyUp`, `PlatformHotkeyConfiguration.OpenContextMenu`), `Maj+F10` non ; le
presse-papiers est un magasin en mémoire (`HeadlessClipboardImplStub`) où un texte et un format
propre (`DataFormat.CreateStringApplicationFormat`) font l'aller-retour ; `Ctrl+C` dans un `TextBox`
y met la sélection ; un glissé du 4ᵉ au 7ᵉ caractère sélectionne [4, 7), le même sans le bouton dit
tenu ne sélectionne rien (`TextBox.OnPointerMoved`) ; `Key.A` sur `PhysicalKey.Q` sous Ctrl
déclenche une liaison `Ctrl+A`, la liaison comparant la touche et non sa place
(`KeyGesture.Matches`) ; deux clics à 3 px ne font pas un double-clic, à 2 px si (`MouseDevice`,
carré de 4 px, 500 ms) ; `SurLaPage` suit l'échelle 2 et échoue sans `PageRoot` ; `EntrerEnSaisie`
ouvre la zone du bloc et la rend ; dans cette zone, sur la planche, un glissé sélectionne et
`Ctrl+C` copie. Ce qui ne s'éprouve pas : le presse-papiers du système, les touches que prend le
bureau, ce que X11 livre pour une touche AZERTY — « < » d'abord —, l'allure d'un curseur.

*Recette jouée* : `Scripts/recettes/N1-banc-des-gestes.sh`. Les sources d'abord, par
`Scripts/aides-de-geste.py` : une aide de geste est un membre ou une fonction locale qui n'est pas
un test et qui synthétise lui-même une entrée — `MouseDown`, `MouseUp`, `MouseMove`, `MouseWheel`,
`KeyPress`, `KeyPressQwerty`, `KeyRelease`, `KeyReleaseQwerty`, `KeyTextInput`, `DragDrop` —, ou qui
porte le nom d'une aide de la région ; aucune n'est définie hors de `Rendu.cs`, et dans `Rendu.cs`
aucune entrée n'est synthétisée hors de la région. Un test qui joue ses entrées en ligne n'en est
pas une, ni une aide de classe qui compose celles du banc ; commentaires et littéraux sont blanchis
avant la lecture. Puis les quinze aides que nomme le poste, présentes dans la région, et les dix
tests sous `poste=N1`. La suite d'interface passe de 284 à 294 tests sur la base du poste, de 318 à
328 après la fusion d'`interface`, tous verts, sans qu'un `poste=` en perde ni en gagne hors de N1 ;
la recette de Q14 passe, celle de Q23 aussi, vingt passages sur vingt. Trois sabotages au juge :
`Glisser` sans `LeftMouseButton` et `EntrerEnSaisie` qui ne clique qu'une fois, pris par `BancTests`
; une classe qui synthétise de nouveau sa touche, que seul le contrôle des sources voit.

*Ce que l'implémentation a appris.* Le glissé dans la zone de saisie d'un bloc sélectionne déjà du
texte : la couche de saisie le laisse passer, et N15 peut s'éprouver au banc dès maintenant. Chaque
fenêtre sans tête a sa souris, donc son compte de clics : un test qui rejoue un geste au même point
dans la même demi-seconde ouvre une fenêtre neuve, ou il fait un double-clic sans le vouloir. Le
presse-papiers d'Avalonia 11.3 parle en `DataFormat` et `DataTransfer` ; l'identifiant d'un format
propre n'admet que des lettres ASCII, des chiffres, le point et le tiret (N8).

*Choisi sans certitude.* `Cliquer` en un point finit par une image, comme celui de `PressionTests` :
ceux de `GroupementTests` et de `MasquerEcranTests` ne faisaient que battre la file, celui de
`DoubleClicTests` rien, si bien que les deux clics du double-clic de Q14 sont désormais séparés
d'une image — la recette de Q23 les a joués vingt fois. `Glisser` fait deux mouvements par défaut ;
les copies qui n'en faisaient qu'un (`ReperesTests`, les deux `Tourner`, `Tracer`) passent `pas: 1`.
`Touche` presse sans relâcher, comme les tests l'ont toujours fait ; `relacher` est là pour la
touche Menu. `ToucheAzerty` ne traduit que les lettres qui changent de place et la rangée du bas, et
refuse la rangée des chiffres, dont elle ne saurait pas ce qu'un AZERTY livre sans Maj.

*Relevé, non corrigé.* Cinq glissés écrits en ligne dans le corps de tests ne disent pas le bouton
tenu — trois dans `GestesTests`, un dans `GroupementTests`, la main de `NavigationTests` — : ce ne
sont pas des aides, ils passent, et N17 les croisera. `MilieuDeLaZone` reste dans `NavigationTests`
et dans `AffichageTests`, sous le même nom et pas pour la même mesure — les bornes de la zone qui
défile là, sa vue ici — : ce n'est pas un geste, et les fondre changerait l'un des deux tests.

#### N2 — Mettre en forme une plage, dans le noyau *(fiche 23, b1, b9)*

*Spécifié le 25 octobre 2026.* Les fragments ne sont écrits aujourd'hui que par la copie, l'import
et le recalage de Q1 ; rien n'applique un style à une plage. Le poste crée `MiseEnForme`, des
fonctions pures du noyau, sur le modèle de `Recalage` :
- `Lire` : sur une plage, la valeur commune de chaque propriété, ou l'état mixte ;
- `Poser` : découper aux bornes les fragments qui chevauchent (`StyleSpan.Clone`), poser la valeur
  à l'intérieur, combler les trous par des fragments neufs, recoller les voisins identiques aux
  bornes, rendre nulle une valeur égale à celle du bloc, retirer les fragments vides. La règle
  « le dernier l'emporte » de `StyleEffectif.Decouper` n'est tranchée que dans la plage touchée ;
- `Basculer` : si toute la plage porte la valeur, la retirer, sinon la poser ;
- `Retirer`, pour `Ctrl+Espace` : rendre à la plage le style du bloc ;
- les mêmes fonctions sur `ParagraphSpan`, pour R6.

`Recalage.ParagrapheDe(texte, position)` devient public : `Sauts` et `EstSaut` sont privés
aujourd'hui. L'écriture passe par les opérations de la collection, jamais par son remplacement
(Q17) ; sans changement réel, l'opération ne fait rien.

*Piège* : `ControleDeBloc` n'écoute que la collection, et un fragment modifié sur place ne serait
pas redessiné. `BaselineShift` n'est pas nullable : 0 veut dire « comme le bloc ». On compare les
valeurs brutes (`scheme:accent1`) pour écrire, mais les valeurs résolues par la charte pour l'état
mixte. Une case de tableau n'a pas de paragraphes.

*Recette* (tests purs) :
- sur « abc def », le gras posé sur [4, 7) donne `{4, 3, Bold}` ; basculé de nouveau, il ne laisse
  aucun fragment ;
- une plage à cheval sur deux fragments importés qui se recouvrent rend les mêmes styles effectifs
  hors de la plage ;
- un gras explicite retiré sur un bloc gras devient `Bold = false` ;
- sur trois paragraphes, le centrage posé sur le deuxième donne `{Start 1, Length 1, Align
  Center}` ; posé ensuite sur le troisième, les deux écarts fusionnent ;
- au juge : la normalisation oubliée.

Fichiers : `MyPublisher.Core/Models/MiseEnForme.cs` (neuf), `Recalage.cs`, `MyPublisher.Tests`.
Effort : moyen.

*Livré le 26 octobre 2026* ; la relecture adversariale et les sabotages du juge viennent après
la fusion. `MiseEnForme` porte, sur les fragments, `Lire(texte, fragments, commun, plage,
charte)`, `Allume(…, bascule)`, `Poser(…, style)`, `Basculer(…, bascule)` et `Retirer` ; sur
les écarts, `Lire`, `Poser`, `Basculer(…, fer)` et `Retirer`, avec `Paragraphes(texte, plage)`
qui rend les paragraphes qu'une sélection touche ; `Commun(TextFrame)` et `Commun(TableFrame)`
donnent le style dont les fragments s'écartent. Ce qu'on lit et ce qu'on pose est un même type,
`StyleDeLaPlage` (et `ReglagesDeLaPlage`), dont une propriété nulle est mixte à la lecture et
intouchée à la pose. Le coupage, la superposition, le recollage et l'écriture sont écrits une
fois pour les fragments et les écarts. Vingt-six tests sous `poste=N2`, dont les quatre de la
recette chiffre pour chiffre, un test d'exhaustivité par réflexion (une propriété ajoutée au
fragment ou à l'écart, et oubliée par la mise en forme, le fait tomber) et deux balayages :
deux mille listes au hasard, recouvrements et débords compris, où hors de la plage rien ne
change à l'œil et où la pose rejouée n'écrit plus rien. Deux sabotages au juge : la
normalisation oubliée, et un fragment coupé en le retouchant sur place.

*Ce que l'implémentation a appris.* `StyleEffectif.Decouper` calculait la fin d'un fragment en
entier : « Start = 2, Length = 2 147 483 647 » débordait, et le fragment, qui court jusqu'au bout
du texte, ne recouvrait plus rien. Elle se calcule désormais en long, comme celle d'un écart,
dans `StyleEffectif.Etendue`, que la mise en forme emploie aussi : les deux voient chaque
fragment au même endroit. Le dessin ne change que pour ces longueurs absurdes.

*Choisi sans certitude* :
- retirer le gras, l'italique ou le souligné pose la valeur éteinte et non « comme le bloc » :
  sur un bloc gras, nul rendrait le gras ; la recette le demande. Le souligné basculé est
  allumé quelle qu'en soit la sorte, comme `Ctrl+U` dans Publisher, et se pose en trait simple ;
  `Lire` dit mixte une plage qui mêle deux sortes, `Allume` la dit allumée ;
- une plage vide ne change rien — c'est à R7 de dire sur quel mot porte un geste sans
  sélection —, et se lit au style que prendrait la frappe (`Recalage.Ancre`) ;
- `Lire` rend la valeur brute quand toute la plage la porte, la résolue quand seules les
  résolues s'accordent ; la charte ne résout que la police et la couleur ;
- le fer basculé de `Ctrl+E`, `Ctrl+R` ou `Ctrl+J` revient au fer à gauche, comme dans Word, et
  non au fer du bloc ;
- une sélection qui finit sur un saut de paragraphe ne touche pas le paragraphe d'après : le
  saut appartient au paragraphe qu'il clôt ;
- une pose qui ne change rien à l'œil laisse la collection telle quelle, même désordonnée : pas
  de pas d'historique sans rien à montrer ;
- une case de tableau s'écarte du style du tableau sans souligné, celui du bloc que
  `TableFrame.CellFrames` construit.

*Relevé, non corrigé.* Le recollage ne joue qu'aux deux bornes de la plage : deux fragments
voisins identiques laissés par un import ailleurs dans le bloc restent deux. Un élément qui
recouvre la plage n'est allongé sur son voisin de droite que s'ils se suivent dans la liste,
pour ne rien changer de ce qui l'emporte hors de la plage ; la liste peut donc garder deux
fragments là où un suffirait, sans que rien n'en paraisse.

#### N3 — Chaque morceau composé sait d'où il vient *(fiche 23, a21)*

*Spécifié le 25 octobre 2026.* C'est le préalable commun à la mesure de N7, au clic simple de N12,
aux deux voies de R7 et aux marques de N22. `Morceau(Texte, Style, X, Largeur)` ne dit pas d'où il
vient ; `Decouper` normalise le texte par `Replace` ; `Fermer` rogne les blancs de fin de ligne, et
le renvoi rogne la tête du mot renvoyé ; `TexteCompose.Disposer` ne pose que l'encre et saute les
lignes vides — une tabulation, un blanc rogné, un paragraphe vide n'y ont aucune place.

Le poste :
- `Morceau` gagne, en dernier membre, `int Debut` ; `LigneComposee` gagne `Debut`, `Fin` — la
  plage de la ligne, blancs rognés compris, saut exclu — et `Paragraphe` ;
- `Decouper` suit les positions du texte d'origine ;
- `TexteCompose.Disposer` expose, depuis la même passe que le dessin, une `LignePosee` par ligne,
  lignes vides comprises : la ligne, son origine après le fer, son sommet, sa ligne de base et les
  décalages de justification ;
- dans le noyau, deux fonctions pures sur une ligne : l'abscisse d'une position, et la position la
  plus proche d'une abscisse, taquets, justification et blancs pendants compris ; dans la vue,
  `TexteCompose.PointDe(index)` et `IndexSous(point)`, en coordonnées du bloc, colonnes, calage
  vertical et texte vide compris ; un bloc tourné y entre par `Frame.ToLocal`.

**La sortie du compositeur ne change pas** : coupures, largeurs, abscisses, `LigneComposee.Texte`,
écran et PDF restent identiques ; `Mots.cs` lit `MorceauPose` par le nom de ses membres, et le
banc, Q13 et les recettes de rendu ne bougent pas. Seule exception, déclarée : un « \r\n » coupé
entre deux fragments compte deux paragraphes dans `Compositeur` et un seul dans `Recalage` ; il
s'aligne sur `Recalage`. Aucun document du dépôt n'en porte, l'import retirant les « \r ».

*Piège* : les cibles du juge dans `Compositeur` et `TexteCompose` restent présentes, une seule fois
chacune ; la géométrie lit `LignePosee` au lieu de recopier leurs lignes (règle 7). Un bloc à champ
n'a pas de géométrie, son `Contenu` différant de `Text`.

*Recette* :
- tests purs sur des textes faits pour : espaces multiples, tabulations, renvois, paragraphes
  vides, « \r\n », « \r » seul, paires de substitution, colonnes. Pour chaque morceau,
  `texte.Substring(m.Debut, m.Texte.Length) == m.Texte` ; les lignes se suivent et couvrent tout le
  texte ; entre le dernier morceau d'une ligne et `Fin`, il n'y a que du blanc ; `Paragraphe` égale
  le compte de `Recalage` ;
- tests d'interface sur l'exemple, ATA 21 et les Fiches : `PointDe(m.Debut).X` égale l'abscisse de
  l'ancre à 10⁻⁶ près, et `IndexSous(PointDe(i)) == i` ;
- `verifier.sh` vert sans retouche d'aucune autre recette, Q13 compris (aucune ligne coupée
  autrement) ;
- au juge : un mot renvoyé garde la position de son blanc de tête.

Fichiers : `Compositeur.cs`, `Views/TexteCompose.cs`, tests. Effort : moyen.

*Livré le 26 octobre 2026*, sur `interface` au commit `2d8b9f9`.

**Le noyau.** `Morceau(Texte, Style, X, Largeur, Debut)` ; `LigneComposee` finit par
`Debut, Fin, Paragraphe`. `Decouper` parcourt le texte d'origine : chaque unité garde sa
place, un saut sa longueur — un « \n », un « \r » seul, ou un « \r\n » pour un seul,
même à cheval sur deux fragments. Le renvoi laisse la tête blanche du mot au bout de la ligne
qu'il quitte ; le morceau renvoyé commence après elle, et la ligne quittée finit là où commence
la suivante. Deux fonctions pures sur une ligne posée : `Compositeur.Abscisse(ligne,
decalages, position, texte, commun, metriques)`, comptée depuis l'origine de la ligne comme
`Soulignes`, et `Compositeur.PositionLaPlusProche(ligne, decalages, abscisse, texte, commun,
metriques)`, son inverse, la première à égalité. Une tabulation avance au taquet ; le point
qui suit un blanc élargi est au mot suivant ; les blancs rognés pendent après le dernier
morceau, mesurés à son style, ou au style commun sur une ligne sans morceau ; jamais le
milieu d'une paire de substitution. `Composer` ne rend toujours aucune ligne pour un texte
vide — un bloc vide ne déborde pas — ; `LigneDuTexteVide` rend celle qu'il aurait, de la
même composition.

**La vue.** `TexteCompose.Disposer` pose, dans la passe du dessin, une
`LignePosee(Ligne, Rang, Origine, Haut, LigneDeBase, Decalages)` par ligne, lignes vides
comprises — une ligne sans morceau prend la montée du style commun. `ControleDeBloc.Mesurer`
peut composer la ligne du texte vide, et `TexteMesure` porte le style commun.
`TexteCompose.Geometrie()` rend une `GeometrieDuTexte` — `Lignes`, `LigneDe(index)`,
`PointDe(index)`, `IndexSous(point)` —, rien sans bloc ni pour un bloc à champ ;
`TexteCompose.PointDe` et `IndexSous` y délèguent, en recomposant à chaque appel : qui
demande beaucoup de points garde la géométrie. `PointDe` rend le point d'insertion sur la
ligne de base de sa ligne ; une position où une ligne est renvoyée est le début de la
suivante. `IndexSous` prend la colonne la plus proche parmi celles qui portent du texte, la
ligne la plus proche dans cette colonne, puis la position la plus proche sur la ligne.

**Mesuré.** L'empreinte de la composition, relevée avant tout changement puis après, par
`Scripts/empreinte-composition.sh` et la commande `empreinte` du banc, qui n'emploie que
l'API d'avant N3 : 18 documents — les 12 de `Samples/`, les 4 de `Templates/`, ATA 21 et les
Fiches par pub2raw —, 289 contrôles de texte, 2 492 lignes composées à la règle de l'écran et
à celle du PDF, 13 880 morceaux avec leur style, leur abscisse et leur largeur au bit près, les
décalages de justification, ce que `Poser` et `Souligner` rendent, l'empreinte des pixels de
29 pages, et le PDF exporté, normalisé de ce que PDFsharp tire au hasard, avec ses 6 634 mots
et leur boîte : **identique**. Deux relevés d'avant, pris du même code, l'étaient aussi entre
eux. Aucune chaîne des 16 `.mypub` ne porte de « \r », ni aucun texte d'ATA 21 et des Fiches.
Sur les documents réels, `PointDe(m.Debut).X` est l'ancre du dessin à 10⁻⁶ près, et
`IndexSous(PointDe(i)) == i` pour chaque position de l'exemple, d'ATA 21, des Fiches et de
`recette-colonnes`. Une géométrie coûte de 0,2 à 15 ms par bloc sur ATA 21, machine chargée.

*Choisi sans certitude* : `PointDe` sur la ligne de base, et non au sommet de la ligne ; la
fin d'une ligne renvoyée rendue au début de la suivante, sans affinité ; un point à égale
distance de deux colonnes ou de deux lignes donné à la première ; la bande d'une ligne étendue
à sa ligne de base quand un interligne serré l'en fait sortir. L'outil d'empreinte est gardé,
et la recette le joue sur demande (`--empreinte <référence>`) : il demande quelques minutes et
un relevé de référence que le dépôt ne garde pas, les pixels dépendant des polices de la
machine.

*Relevé, non corrigé* : la mise en page arrondit au pixel la place et la taille du contrôle
d'un bloc — 22,77 pt de haut deviennent 23 — et un bloc tourné tourne autour du centre de son
contrôle : le repère que `Frame.ToLocal` calcule sur le modèle s'écarte de celui de l'écran de
0,5 pt sur les deux blocs tournés d'ATA 21. Le caractère visé n'en change pas ; c'est l'affaire
de N12 et de R7.

#### N4 — L'affichage au clavier *(fiche 23, d10, d11, d13, e14, f24)*

*Spécifié le 25 octobre 2026.* Publisher zoome par `F9` (p. 36), qui bascule entre l'affichage
courant et la taille réelle, et tient la page entière par `Ctrl+Maj+L` (aide en français) ; on y
tape le zoom dans sa liste (p. 8, 24) ; « Limites et repères » (`Ctrl+Maj+O`) et « Repères de
lignes de base » (`Ctrl+F7`) sont deux bascules distinctes (p. 9, 15). MyPublisher n'a qu'une case
« Repères », le zoom au curseur et le bouton « Page entière ».

Le poste :
- `F9` bascule entre l'affichage courant et 100 %, autour du centre de la sélection, ou de la
  zone visible sans sélection ; un second `F9` rend l'affichage d'avant. La commande est dans
  `MainWindowViewModel.Affichage.cs` ; la planche fait le calcul (`PageCanvas.Navigation.cs`) ;
- `Ctrl+Maj+L` joue `PageEntiereCommand` ;
- la valeur du zoom devient une liste éditable : Page entière, Objets sélectionnés, de 400 % à
  10 % ; la valeur part à Entrée, Échap renonce et rend la planche ;
- « _Limites et repères » (`Ctrl+Maj+O`) reprend la case d'aujourd'hui ; « Repères de lignes de
  _base » (`Ctrl+F7`) est une bascule neuve, `ShowBaselines`, que `PageCanvas.axaml` lit ;
- le menu Affichage montre ses gestes.

*Piège* : la maquette fait foi pour les tailles — le champ garde l'allure du libellé tant qu'il n'a
pas le focus ; règle 5, jamais de zoom d'office ; le champ n'ouvre aucune transaction. À vérifier
avant : que ni GNOME ni KDE ne prennent `F9`, `Ctrl+F7`, `Ctrl+Maj+L` ou `Ctrl+Maj+O`.

*Recette* :
- `F9` sur une page à 50 % la met à 100 %, la sélection sous le même point de la zone visible à
  1 px près ; un second `F9` rend 50 % ; rien n'entre à l'historique ;
- `Ctrl+Maj+L` donne le zoom du bouton ; « 150 » puis Entrée donne 150 % ; « 1000 » est refusé,
  avec un message ;
- `Ctrl+F7` ne masque que les lignes de base ;
- A4 passe ; au juge : `F9` qui ne rend pas l'affichage d'avant.

Fichiers : `MainWindow.axaml` (barre principale, menu Affichage, liaisons), `PageCanvas.axaml`,
`PageCanvas.Navigation.cs`, `MainWindowViewModel.Affichage.cs` (neuf), README (Affichage).
Effort : faible.

*Livré le 26 octobre 2026*, dans la salve Saisie 1 ; la relecture adversariale vient après la
fusion. **Le bureau, vérifié avant d'écrire** (`gsettings list-recursively`, schémas des
extensions compris ; pas de `~/.config/kglobalshortcutsrc`, la machine n'a pas KDE) : GNOME
50.1 sous Wayland ne prend ni `F9`, ni `Ctrl+F7`, ni `Ctrl+Maj+L`, ni `Ctrl+Maj+O`. Il prend
leurs voisines — `Alt+F7` (déplacer la fenêtre), `Ctrl+Alt+F7` et `Ctrl+Alt+F9` (changer de
session) —, que le poste ne déclare pas ; `Ctrl+Maj+O` n'est qu'un raccourci du terminal
Ptyxis, qui ne vaut que dans sa fenêtre. Le poste écrit, outre les fichiers nommés, le champ
du zoom en contrôle à part (`Views/ChampDuZoom.axaml` et son code), inscrit à la liste des
vues de la recette I1 ; la classe `AffichageTests` (dix tests) ; deux sabotages au juge — `F9`
qui oublie l'affichage d'avant, et le second `F9` qui rend le zoom sans le défilement. Le
modèle de vue décide — quel zoom, quel affichage `F9` rendra — et la planche calcule, seule à
connaître sa zone et son défilement : elle répond à deux événements, `ZoomDemande` et
`ObjetsSelectionnesDemandes`, auxquels elle s'abonne dans `PageCanvas.Navigation.cs` sans
toucher au fichier principal de la planche (règle 16). Le README dit les quatre touches au
sous-tableau « Affichage », le champ dans « Interface », et « Limites et repères » là où il
disait *Affichage ▸ Repères*.

*Ce que l'implémentation a appris.* Les liaisons de la fenêtre passent avant `OnKeyDown`, et
une liaison dont la commande s'exécute garde la touche : `Ctrl+Maj+O` n'arrive jamais à
l'aiguillage qui lit `Ctrl+O` sans regarder `Maj` et ouvrirait un document — mesuré en
remplaçant l'ouverture par une marque, que le test n'a pas vue. Les quatre commandes
s'exécutent donc toujours. Rendre le défilement d'avant ne se prouve pas sur un aller-retour
immobile : zoomer en arrière autour du même point rend le même défilement, au pixel près, et
le sabotage qui l'oublie passait ; le test fait défiler la planche entre les deux `F9`.

*Choisi sans certitude* :
- `F9` en taille réelle sans rien à rendre ne fait rien et le dit à la barre d'état (« Déjà en
  taille réelle. ») ; l'affichage retenu s'oublie dès qu'un autre geste change le zoom — la
  molette, la loupe, un bouton, le champ, « Page entière » —, pas quand on fait seulement
  défiler ;
- le point qui garde sa place est le centre de la sélection quand il est en vue ; hors de la
  vue, il vient au milieu de la zone ; un bloc masqué (I8) ne compte pas, ni pour `F9` ni pour
  « Objets sélectionnés » ;
- le champ lit « 150 », « 150 % », « 66,5 » ou « 66.5 » ; un zoom hors des bornes est refusé,
  non ramené à la borne ; les flèches parcourent la liste, `Alt` + flèche la déroule ou la
  replie ; perdre le focus vaut Échap, sans rendre la planche ;
- « Objets sélectionnés » est aussi une entrée du menu Affichage, sans touche, comme
  « Basculer en taille réelle » (`F9`) et « Page entière » (`Ctrl+Maj+L`) ; « Taille réelle »
  y prend la lettre d'accès `T`, « Basculer » la `C` ;
- `ShowBaselines` est vraie à l'ouverture et n'entre pas au document ; « Limites et repères »
  masque les lignes de base avec le reste, et les rend dans l'état de leur propre case.

*Relevé, non corrigé.* Le champ montre le zoom arrondi au pourcent, comme le libellé
d'avant : « 66,5 » tapé se relit « 67 % » — `ZoomLabel` n'est pas au poste. « Page
entière » et « Objets sélectionnés » ne se rappellent pas comme tels : le champ montre
ensuite le pourcentage qu'ils ont posé, et ne suit pas la fenêtre qu'on redimensionne
(règle 5).

#### N5 — Quitter la saisie d'un clic qui agit *(fiche 23, a8, a9)*

*Spécifié le 25 octobre 2026.* `BeginTextEdit` retire la couche de saisie du test de survol. Le
gestionnaire descendant referme bien la saisie au clic suivant, mais la pression va ensuite à
`PageRoot` : rien n'est sélectionné, il faut recliquer, et les poignées dessinées autour du bloc
en saisie sont inertes. Publisher, lui, sélectionne l'objet cliqué d'un seul geste.

Le poste fait de `InputLayer` une `CoucheDeSaisie : Border, ICustomHitTest`. Pendant la saisie,
la couche reste atteignable partout, **sauf sur la zone du texte en saisie**, qu'elle laisse au
champ. `OnPointerPressed` appelle `EndTextEdit`, puis agit comme d'habitude : il prend une poignée,
un repère, un autre bloc, ou vide la sélection. Rien n'est rejoué, aucun double-clic n'est
fabriqué. Le gestionnaire descendant reste, pour les règles.

*Piège* : l'espace de coordonnées de `HitTest` est à vérifier par le test ; la couche dérive de
`Border`, que `NavigationTests` cherche par `Trouver<Border>("InputLayer")` ; elle garde
`Name="InputLayer"` (règle 6).

*Recette* :
- en saisie, un clic sur un autre bloc le sélectionne en un seul geste : la frappe fait un pas, la
  sélection aucun ;
- une poignée du bloc en saisie se tire : un pas pour la frappe, un pour le redimensionnement ;
- un clic dans le texte en saisie déplace le curseur et laisse la saisie ouverte ; un clic dans le
  vide désélectionne ;
- au juge : `return` juste après `EndTextEdit`.

Fichiers : `Views/CoucheDeSaisie.cs` (neuf), `PageCanvas.axaml`, `PageCanvas.axaml.cs`.
Dépend de Q25, même fichier, et de N1. Effort : faible.

#### N6 — Les raccourcis de la fenêtre laissent la touche au champ *(fiche 23, a13, d2, d21)*

*Spécifié le 25 octobre 2026.* Les liaisons de la fenêtre passent avant le champ qui a le focus et
ne lui rendent la touche que si leur commande ne peut pas s'exécuter. En saisie, `Ctrl+A`
sélectionne donc tous les blocs et referme la saisie, et `Ctrl+D` duplique le bloc et fond la
frappe et la copie en un seul pas — de même dans les champs Contenu et Nom et dans les champs
numériques.

Le poste pose une règle : **tant qu'un champ de texte a le clavier, les commandes d'objet se
taisent** — `SelectAll`, `DuplicateSelected`, `GroupSelected`, `UngroupSelected`, `BringToFront`,
`SendToBack`, et demain le presse-papiers de N8. Leur touche revient au champ. Le mécanisme : un
`CanExecute` qui interroge le focus de la fenêtre (`ConsumesTyping`), et `NotifyCanExecuteChanged`
à chaque changement de focus, pour que les menus suivent. `Ctrl+A`, en saisie sur la page,
sélectionne alors tout le texte, comme l'aide en français le dit « dans une zone de texte ».
`Alt`+flèches, en saisie, déplace le bloc dans un pas à lui (aide en français : « déplacer l'objet
dont le texte a le curseur »). Annuler et Rétablir ne changent pas de sens ; Q24 les rend sûrs.

*Piège* : les menus ne se grisent que sur `CanExecuteChanged`. Ne pas déclarer un second `Ctrl+A`
sur le champ : la recette A4 refuse les doublons. La cible du juge `PageView.EndTextEdit();` reste,
une seule fois.

*Recette* :
- en saisie, `Ctrl+A` sélectionne le texte, la saisie reste ouverte et la sélection des blocs ne
  change pas ; de même dans Contenu, Nom et un champ numérique ;
- `Ctrl+D`, `Ctrl+Maj+G`, `H`, `F` et `B` ne font rien et n'empilent rien ;
- `Alt+→` déplace le bloc de 3 pt, en un pas, et la saisie continue ;
- hors saisie, tout se joue comme avant : les tests de A4 passent ;
- au juge : `SelectAll` qui ne lit plus le focus.

Fichiers : `MainWindowViewModel.cs` (attributs `CanExecute`), `MainWindowViewModel.Clavier.cs`
(neuf), `MainWindow.axaml.cs` (changement de focus). Dépend de Q24. Effort : faible.

#### N7 — Mesurer l'écart de la première voie, et s'arrêter *(fiche 23, a21 ; R7)*

*Spécifié le 25 octobre 2026*, comme R7 le prévoit : « le poste commence par mesurer l'écart de la
première voie sur les documents réels ». La mesure devient un poste, pour qu'elle ait sa recette
et qu'on puisse la rejouer — après une montée de version d'Avalonia, ou pour un prototype de la
voie 1 renforcée.

**L'outil.** Une commande du banc, `MyPublisher.Banc saisie <doc> --sortie <écarts.json>
[--au-mieux]`, sur le modèle de `mots`, qui lit les vrais contrôles de la vraie fenêtre, et
`Scripts/mesurer-la-saisie.sh`, qui la lance sur le corpus. Ce n'est pas un test : il faut ouvrir
les `.pub` par pub2raw et lire l'écran tel qu'il est, comme `comparer-mots.sh`.

**Les blocs.** Tous les blocs de texte visibles, sans champ, au premier niveau, de quatre
documents : `Samples/exemple-bulletin.mypub`, ses deux pages ; ATA 21 par pub2raw, 9 blocs dont
2 tournés, mesurés dans leur repère ; les Fiches, 18 blocs ; `Samples/recette-coupure.mypub`,
74 blocs aux limites. Les cases et les membres de groupe sont comptés, sans mesure (N26, N27).

**La grandeur.** Pour chaque position du curseur *i*, de 0 à n — hors de l'intérieur d'une paire
de substitution ou d'un « \r\n » —, dans le repère du bloc, en points : côté champ,
`TextLayout.HitTestTextPosition(i)` et la ligne de base de sa ligne, lus sur le
`PART_TextPresenter` d'une saisie ouverte par le banc, sans transaction ; côté composition,
`PointDe(i)` et la `LignePosee` de N3. L'écart vaut `max(|Δx|, |Δ ligne de base|)`. Par bloc :
l'écart maximal, son 95ᵉ centile, le nombre de positions au-dessus du seuil, la première qui
diverge, le nombre de lignes de chaque côté, et **les causes présentes** — fragments d'un autre
corps, d'une autre police ou d'une autre graisse ; écarts de paragraphe ; retraits ou espaces du
bloc ; interligne différent de 1 ; colonnes ; tabulations ; calage autre qu'en haut ; débordement ;
police de symboles ; coupure différente.

**Deux configurations** : A, le champ tel qu'il est ; B, « au mieux de la voie 1 », posée par le
banc sur l'instance du champ sans rien changer à l'application — `LineHeight` à la hauteur
naturelle résolue multipliée par l'interligne du bloc, et `UseLayoutRounding=False` sur le champ et
ses parties, l'arrondi de mise en page se faisant au point à tous les zooms (jusqu'à 0,5 pt
d'écart, comme Q4 l'avait trouvé pour les filets).

**Le zoom et le seuil.** L'écart en points ne dépend pas du zoom ; il se mesure au zoom du banc,
et une contre-épreuve au zoom 2 sur l'exemple doit donner les mêmes écarts à 0,01 pt près. Le
seuil est **0,75 pt**, un pixel au zoom 100 % ; le relevé donne aussi la valeur rapportée à 200 %.
Si le maximum de B, sur toutes les positions des quatre documents, est sous le seuil, on prend la
voie 1 ; sinon, on s'arrête.

**L'étalonnage.** `Samples/recette-saisie.mypub` porte trois blocs dont l'écart est connu : un bloc
d'un seul style, sans retrait, à interligne 1, dont l'écart en B reste sous 0,1 pt ; le même, avec
6 pt d'espace après chacun de ses trois paragraphes — environ 6 pt, puis 12 pt ; un fragment de
16 pt dans un texte de 10 pt — un écart non nul.

*Recette* : `N7-mesure-de-la-saisie.sh` contrôle les trois nombres de l'étalonnage, puis lance le
corpus, qui ne la fait pas échouer : ce qu'il donne est une décision, non un résultat à atteindre.
Au juge : la mesure oublie l'ordonnée.

**Ce que livre la mesure** : la commande et son script, le document d'étalonnage, et, dans la
section R7, un *Mesuré le …* qui donne les chiffres par document et par cause, suivi de la note de
décision. Aucune ligne de l'application.

**La note de décision** dira, chiffres à l'appui, ce que chaque voie suit et ce qu'elle ne suivra
pas.
- *Voie 1* : lier l'interligne (`InterligneConverter` donne exactement `Compositeur.Avance`, sur la
  famille résolue par la charte), couper l'arrondi, empêcher le champ de défiler vers le curseur
  quand le texte déborde, `NewLine` à « \n », lier le calage vertical sans le rendre négatif (A7),
  mettre les retraits **du bloc** dans le `Padding`, poser un pinceau de curseur, surligner la
  sélection d'une teinte translucide — une vidéo inverse redessinerait les glyphes du champ,
  décalés de l'écart.
- *Voie 1 renforcée* : un présentateur dérivé (`TextPresenter.CreateTextLayout` est
  `protected virtual`) suivrait en plus les fragments et les taquets ; fragile, `_constraint` étant
  privé et le gabarit Fluent à recopier.
- *Ce qu'aucune voie 1 ne suivra jamais* : les écarts de paragraphe (ATA 21 mêle 31 paragraphes
  justifiés et 21 au fer à gauche), le retrait négatif de première ligne de ses 36 listes, les 29
  espaces après paragraphe des Fiches, les colonnes, la règle de coupure de Q13, et, pour la voie
  simple, les polices de symboles des Fiches.
- *Voie 2a* : un champ caché fait la frappe, la méthode d'entrée et le presse-papiers ; curseur,
  sélection, souris et navigation dépendante de la mise en page se calculent sur la composition et
  se dessinent dans l'`Overlay`. Environ la voie 1 plus un poste moyen ; un seul champ caché hors
  de `PageRoot` servirait aussi aux groupes et aux cases.
- *Voie 2b* : un éditeur complet sur `Compositeur`, frappe et méthode d'entrée comprises ; très
  lourd, il rejoint le seuil D.

Rien de ce qui dépend de la voie ne part avant la réponse du propriétaire : N21, R6, R7, N22 à
N28. Les salves Saisie 3 à 5 et A12 continuent pendant ce temps.

Fichiers : `MyPublisher.Banc/`, `Scripts/mesurer-la-saisie.sh`, `Samples/recette-saisie.mypub`,
la recette. Dépend de N3. Effort : moyen.

#### N8 — Couper, copier, coller des blocs *(fiche 23, a17, a18, c21, d7)*

*Spécifié le 25 octobre 2026.* Le livre fait entrer le contenu par copier-coller (p. 21, 23, 38),
et le menu contextuel de Publisher commence par Couper, Copier, Coller (p. 34). MyPublisher n'a
aucune commande de presse-papiers hors du `TextBox` : hors saisie, `Ctrl+C` et `Ctrl+V` ne font
rien, et le menu Édition n'a pas ces entrées.

Le poste, dans `MainWindowViewModel.PressePapiers.cs` :
- le format `mypublisher-blocs` est le JSON d'un document d'une page qui porte des clones de la
  sélection (`Frame.Clone` copie en profondeur), dans l'ordre de la page ; s'y ajoute le texte brut
  des blocs, pour les autres logiciels ;
- le collage pose les blocs au même endroit, ou 12 pt plus loin si l'original y est encore, comme
  `Ctrl+D` ; un texte seul crée un bloc de texte ; Couper fait un seul pas ;
- **les touches sont traitées dans `OnKeyDown`, après la garde de la saisie, jamais en
  `KeyBinding`** : une liaison passerait avant le `TextBox` et lui volerait la touche, alors que le
  `TextBox` marque toujours `Ctrl+C`, `X` et `V` comme traitées — la fenêtre ne les voit jamais en
  saisie. Les gestionnaires s'appellent `OnCouper`, `OnCopier`, `OnColler`, pour que la recette A4
  les voie ;
- les entrées du menu Édition passent par la fenêtre, qui les rend au `TextBox` en saisie.

*Piège* : le presse-papiers est asynchrone ; les références au jeu (`scheme:accent1`) se résolvent
dans le document d'arrivée ; un collage d'une version plus récente du format est refusé ; les
chemins d'images relatifs (A3) se rebasent sur le document d'arrivée.

*Recette* :
- copier puis coller sur une autre page donne des instances distinctes, au même endroit, en un
  pas ; sur la même page, à +12 pt ;
- en saisie, `Ctrl+C` met le texte au presse-papiers, sans format de bloc, et le document ne
  change pas ;
- un texte brut collé hors saisie devient un bloc ;
- au juge : le collage réutilise l'instance copiée au lieu d'un clone.

Fichiers : `MainWindowViewModel.PressePapiers.cs` (neuf), `MainWindow.axaml` (menu Édition),
`MainWindow.axaml.cs` (`OnKeyDown`), README (Édition). Dépend de N1. Effort : moyen.

#### N9 — La langue du texte *(fiche 23, a26)*

*Spécifié le 25 octobre 2026.* Un correcteur (N25) doit savoir en quelle langue est chaque mot ; le
modèle n'en porte aucune. Les documents réels en ont une : libmspub transmet la langue de chaque
fragment — ATA 21 en déclare 73 en `fr-FR`, les Fiches 298 en `en-US` et 31 en `fr-FR`, selon le
relevé de l'étude de Publisher 98 (F2) —, et l'import la perd.

Le poste : `Document.Langue`, une étiquette BCP 47 ; `TextFrame.Langue` et `StyleSpan.Langue`,
facultatives — nulle veut dire celle du bloc, puis celle du document. Ce sont des clés additives
(règle 15). L'import `.pub` lit la langue des fragments dans le relevé de libmspub. Le dock
Document règle la langue du document, en un pas. Un document neuf prend la langue de son modèle ;
un document vierge prend `fr-FR`, jamais la langue de la machine : la culture reste la culture
courante (règle 8), mais la langue du document n'en dépend pas. La langue n'a aucun effet sur le
rendu : elle ne sert qu'à N25.

*Piège* : le nom des clés de langue dans le relevé de libmspub se vérifie sur `pub2raw` avant
d'écrire l'import ; une langue inconnue reste écrite telle quelle ; Samples et Templates sont
réenregistrés.

*Recette* :
- ATA 21 importé : 73 fragments portent `fr-FR` ; les Fiches, 298 `en-US` et 31 `fr-FR` ;
- l'aller-retour JSON garde chaque langue ; un document vierge est en `fr-FR` quel que soit
  `LC_ALL` ;
- le dock change la langue du document en un pas ;
- l'écran et le PDF ne changent pas d'un pixel (`comparer-rendus.sh`) ;
- au juge : l'import oublie la langue d'un fragment.

Fichiers : `Frame.cs`, `StyleSpan.cs`, `Document.cs`, `PubImporter.cs`, `MainWindow.axaml` (dock
Document), Samples, Templates. Effort : faible à moyen.

### Lot 2 — Le clavier et le clic de Publisher

Le lot où l'application change de main : le clic simple dans le texte (N12), les raccourcis
français (N10), les menus (N13), les champs (N14), Ctrl+glisser et le rectangle (N17). Q21 et Q22,
qui ouvrent le lot, sont repris dans leur section de la phase Q.

#### N10 — Les raccourcis de Publisher en français *(fiche 23, b2, b7, b8, b10, d8, d20)*

*Spécifié le 25 octobre 2026, sur la décision 4.* MyPublisher suit déjà le français pour `Ctrl+G`
et `Ctrl+I`. Il lui manque le reste de la table, et deux de ses touches la contredisent : `Ctrl+E`
exporte en PDF là où Publisher centre — même pendant une saisie, la touche étant traitée avant la
garde —, et `Ctrl+Maj+H` dissocie là où Publisher dissocie par `Ctrl+Maj+G`, la touche qui groupe.

**La table.** Elle s'établit d'abord, et devient la **fiche 24**,
`Docs/Publisher2007/24-raccourcis-clavier-francais.md` : l'aide de Microsoft en français,
*Raccourcis clavier dans Publisher*, relue mot pour mot, avec son adresse et sa date de
consultation ; pour chaque touche, la fonction, la page du livre de 2007 qui la montre en anglais
(p. 9, 10, 16, 22, 61), et le sort retenu par la règle 12. L'aide vaut pour Publisher 2013 à 365 ;
ses touches sont celles de 2007, sauf celles du ruban, écartées.

**Ce que le poste lie** :
- `Ctrl+E`, `Ctrl+L`, `Ctrl+R`, `Ctrl+J` : centrer, aligner à gauche, à droite, justifier — sur le
  bloc aujourd'hui, sur le paragraphe une fois R6 livré ;
- `Ctrl+]` et `Ctrl+[` : le corps d'un point de plus, d'un point de moins ; `Ctrl+Maj+>` et
  `Ctrl+Maj+<` : la taille suivante ou précédente de la liste standard, que le poste définit dans
  le noyau (8 à 72 pt, celle d'Office) et que N19 reprend ;
- les préréglages d'interligne, si la fiche 24 les donne ;
- `Ctrl+Maj+P` : exporter en PDF. L'export devient une commande du modèle de vue qui lève une
  demande à la fenêtre, comme `PageEntiereDemandee` ; le `case Key.E` sort de `OnKeyDown`.
  `Ctrl+Maj+P` est libre dans la table française, voisin de `Ctrl+P` (Imprimer, réservé à B3) ;
  `Ctrl+Maj+E` est la bibliothèque (C7), `Ctrl+Maj+S` reviendra à « Enregistrer sous », et
  `Ctrl+Alt` écrit un caractère sous AltGr sur un AZERTY ;
- `Ctrl+Maj+G` groupe ou dissocie selon la sélection ; `Ctrl+Maj+H` est libéré (la coupure de mots,
  réservée) ;
- `Alt+Retour arrière` et `F4` : annuler et rétablir, en plus de `Ctrl+Z` et `Ctrl+Y` ;
  `Ctrl+Maj+X` : supprimer l'objet, même en saisie.

**L'AZERTY.** `KeyGesture` compare la *touche* et les modificateurs exacts, jamais le caractère :
sur un AZERTY, « < » et « > » partagent une touche, « ] » et « [ » demandent AltGr, et Maj+« = »
donne « + ». Le poste commence donc par **une sonde** sur la machine du propriétaire : une
variable d'environnement qui écrit, pour chaque frappe, la touche, la touche physique, le
caractère et les modificateurs. Les raccourcis par caractère (`<`, `>`, `]`, `[`) passent par une
table unique, `RaccourcisParCaractere`, lue sur le caractère produit, Maj et AltGr ignorés ; les
lettres et les touches nommées restent des `KeyBinding`. Les tests rejouent les triplets relevés
par la sonde, par `ToucheAzerty` (N1). `Ctrl+0`, qui demande Maj sur un AZERTY, est vérifié du
même coup.

**La recette A4 étendue**, par ce poste seul : chaque geste reste déclaré une fois, en `KeyBinding`
ou dans la table ; chaque touche traitée dans `OnKeyDown` figure au README, comme les liaisons ;
chaque liaison qui a une entrée de menu l'y affiche, et chaque geste affiché à un menu est une
vraie liaison ; les touches réservées restent libres ; `Ctrl+E` n'exporte plus ; `Ctrl+Shift+F` et
`Ctrl+Shift+B` restent exigés. Le contrôle « `Ctrl`+lettre interceptée » ne vise plus que N, O, S
et les touches du presse-papiers.

*Piège* : `Ctrl+Maj+F` est la boîte Police dans l'aide, `Ctrl+Maj+B` le carnet d'adresses ; ils
restent Premier plan et Arrière-plan (règle 12), et la fiche 24 le dit. Les infobulles
« Dissocier (Ctrl+Maj+H) » de la barre et le libellé du menu Texte changent : `DispositionTests`
et `BarreContextuelleTests` les lisent. La recette I3 garde `Ctrl+U`. Reste à vérifier que ni GNOME
ni KDE ne prennent `Ctrl+Maj+P`, comme B7 l'a fait pour `Ctrl+F1`.

*Recette* :
- `Ctrl+E` centre le texte en un pas et n'exporte plus ; `Ctrl+Maj+P` exporte, même en saisie ;
- `Ctrl+L`, `R`, `J` alignent ; `Ctrl+]` et `Ctrl+[` changent le corps d'un point, en un pas ; les
  triplets AZERTY de « < » et « > » changent la taille ;
- `Ctrl+Maj+G` groupe, puis dissocie ; `Alt+Retour arrière` annule, `F4` rétablit ; `Ctrl+Maj+X`
  supprime le bloc en saisie ;
- tout cela sur le bloc, hors saisie et en saisie ;
- A4 étendue passe ; au juge : `Ctrl+E` rendu à l'export.

Fichiers : `MainWindow.axaml` (liaisons, gestes des menus), `MainWindow.axaml.cs` (`OnKeyDown`,
demande d'export), `MainWindowViewModel.Raccourcis.cs` (neuf), `BarreContextuelle.axaml`
(infobulles), README (tous les sous-tableaux), `A4-superposition.sh`, la fiche 24. Dépend de N4,
N6 et N8. Effort : moyen.

#### N11 — Ne rien perdre en fermant, retrouver ses documents *(fiche 23, d19, e19, f25)*

*Spécifié le 25 octobre 2026.* Ni Nouveau, ni Ouvrir, ni Quitter, ni la fermeture de la fenêtre ne
demandent rien : `Alt+F4` jette les modifications sans un mot. Le menu Fichier de Publisher a
Fermer (`Ctrl+F4`) et les six derniers documents (p. 61).

Le poste :
- Nouveau, Ouvrir, Fermer (`Ctrl+W`, `Ctrl+F4`), Quitter et la fermeture de la fenêtre
  (`Closing`) demandent avant de perdre des modifications : Enregistrer, Ne pas enregistrer,
  Annuler, avec un bouton par défaut et un bouton d'annulation (`IsDefault`, `IsCancel`). Une
  saisie ouverte est d'abord refermée, pour que sa frappe compte. Fermer ferme le document et rend
  le catalogue des modèles, comme Publisher rend son écran d'accueil ;
- les six derniers documents ouverts ou enregistrés sont au menu Fichier, numérotés, gardés dans
  `Services/Preferences.cs`, le magasin des réglages de l'utilisateur, hors du document, que A11
  et N25 reprennent.

*Piège* : les tests ferment des fenêtres modifiées (par exemple `MasquerEcranTests`) ; la question
n'est donc branchée que par `App.axaml.cs`, jamais par la fenêtre elle-même. Q24 passe avant, pour
que `IsDirty` dise vrai.

*Recette* :
- les trois réponses, sur Nouveau, Ouvrir, Fermer et la fenêtre : Enregistrer écrit puis ferme,
  Ne pas enregistrer ferme, Annuler laisse tout tel quel ;
- aucune question sur un document intact ;
- les documents récents survivent à un redémarrage, sans doublon, le plus récent en tête ;
- au juge : la question oubliée à la fermeture de la fenêtre.

Fichiers : `MainWindow.axaml` (menu Fichier), `MainWindow.axaml.cs` (`Closing`), `App.axaml.cs`,
un dialogue neuf, `Services/Preferences.cs` (neuf). Dépend de Q24. Effort : moyen.

#### N12 — Entrer dans le texte d'un clic, le curseur au point visé *(fiche 10 ; fiche 23, a2 à a5, a10)*

*Spécifié le 25 octobre 2026, sur la décision 3.* Aujourd'hui, un bloc de texte ne s'ouvre qu'au
double-clic (`OnDoubleTapped`), puis `BeginTextEdit` sélectionne tout ; un bloc neuf porte
« Saisissez votre texte. » ou « Titre », tout sélectionné ; *Insertion ▸ Bloc de texte* pose le bloc
sans ouvrir la saisie ; le curseur est la croix de déplacement sur toute la surface du bloc.
Publisher pose le point d'insertion au premier clic, à l'endroit visé ; un bloc neuf est vide, le
curseur dedans (p. 33).

**La mécanique** vit dans un fichier partiel neuf, `PageCanvas.Saisie.cs`. `OnPointerPressed` teste
dans l'ordre : la main et la loupe ; un outil armé ; les poignées, puis les repères ; puis la
**zone** sous le point.
- **La zone de texte** est l'intérieur d'un bloc de texte visible et sans champ, moins une **bande
  de bord** de 5 px d'écran, au plus un quart du côté ; pour un bloc sélectionné, la bande s'étend
  jusqu'au cadre de sélection. La bande de bord, et tout objet qui n'est pas du texte, se
  sélectionnent et se déplacent comme aujourd'hui.
- **Curseurs** : `Ibeam` sur le texte, `SizeAll` sur le bord.
- **Une pression dans la zone de texte, sans Ctrl ni Maj**, sélectionne ce bloc seul ; calcule la
  position visée sur la composition, `IndexSous` (N3) après `ToLocal`, c'est-à-dire sur ce qui
  était à l'écran ; ouvre la saisie **sans** tout sélectionner, focus puis `CaretIndex` à cette
  position ; et passe le glissé au champ, dont la sélection suit le pointeur depuis l'ancre. Un
  glissé de moins de 3 px ne sélectionne rien (le seuil de Q21).
- `Ctrl`+clic et `Maj`+clic dans un bloc non sélectionné l'ajoutent ou le retirent, sans saisie ;
  dans le texte en saisie, `Maj`+clic étend la sélection. Le double-clic sélectionne un mot ; il
  n'ouvre plus la saisie.
- **Un bloc neuf est vide**, le curseur dedans ; *Insertion ▸ Bloc de texte* ouvre la saisie comme
  l'outil.
- Les traits, les blocs masqués et les blocs tournés suivent la règle générale, ces derniers dans
  leur repère.

Le curseur tombe au bon caractère dès ce poste, puisqu'il est calculé sur la composition. Ce qu'on
voit *après* l'entrée dépend de la voie de R7 (N21) : jusque-là, le champ en texte brut.

**Les tests qui changent de sens** (règle 14) : dans `DoubleClicTests`, le premier clic ouvre déjà
la saisie, et le glissé d'un point prend le bloc par sa bande de bord ; dans `PressionTests`, le
chronomètre presse le bord et un test neuf chronomètre l'entrée en saisie ; dans
`BarreContextuelleTests`, le clic qui suit le sélecteur de couleur ; les aides d'entrée de N1 font
un seul clic. Les recettes `Q14-double-clic.sh` et `Q23-selection.sh` sont reprises, et la ligne
« Double-clic » du README devient « Clic ».

*Piège* : la cible du juge « un mouvement nul du pointeur aimante le bloc cliqué » n'est plus prise
quand le test presse le centre : le test réécrit presse la **bande de bord** de chaque bloc d'ATA 21
avec un mouvement nul, et cela s'éprouve à la main. La cible « les réglages du bloc se
reconstruisent » de Q23 se reprend par le bord. La ligne `var hit = DansLaPage(page) ?
_vm.CurrentPage.Frames…` reste une seule fois. Le temps de la pression qui ouvre la saisie se
mesure : moins de 100 ms en médiane sur ATA 21.

*Recette* :
- un clic sur le 5ᵉ caractère d'un bloc non sélectionné donne ce bloc seul, en saisie, avec
  `CaretIndex` 4 ; de même sur un bloc tourné de 30°, et sur un caractère d'ATA 21 repéré par N3 ;
- un glissé du centre du 3ᵉ au centre du 7ᵉ caractère sélectionne [2, 7) ;
- un glissé de 1 pt par le bord, `Alt` tenu, déplace le bloc, en un pas ; `Ctrl`+clic dans le texte
  ajoute le bloc à la sélection, sans saisie ;
- entrer puis sortir sans taper n'empile aucun pas ; taper en empile un ; un bloc tracé à l'outil
  est vide et la frappe y entre ;
- un double-clic sur un mot, joué vingt fois par la recette, sélectionne le mot ;
- au juge : la pression qui ouvre la saisie sélectionne encore tout.

Fichiers : `PageCanvas.axaml.cs`, `PageCanvas.Saisie.cs` (neuf), `MainWindowViewModel.cs`
(`BuildFrame`, le texte d'attente), les tests ci-dessus, les recettes Q14 et Q23, README (Souris).
Dépend de N1, N3, N5 et Q21. Effort : moyen.

#### N13 — Les menus de Publisher *(fiche 23, d3, d23, e4, e7, e10, e21)*

*Spécifié le 25 octobre 2026.* Les menus de MyPublisher portent des lettres d'accès en double —
Disposition : G quatre fois, D, H et L deux fois ; Insertion : T deux fois ; Texte : G deux fois ;
Affichage : R trois fois —, qui font tourner le focus d'une entrée à l'autre sans rien déclencher,
et « _Édition » est injoignable : `AccessKeyHandler` compare `Key.ToString()`, et aucune touche ne
s'appelle « É ». Les gestes s'affichent « Ctrl+Shift+G » aux menus et « Ctrl+Maj+G » dans les
bulles. Tab, sur la planche, est la navigation du focus d'Avalonia ; Publisher y passe d'un objet à
l'autre.

Le poste :
- des lettres d'accès uniques à chaque niveau, aucune accentuée ; « _Edition », comme les menus
  d'Office 2003 et 2007 en français (à vérifier) ;
- les gestes affichés en français, « Ctrl+Maj+G », « Suppr », par
  `AppBuilder.With(new KeyGestureFormatInfo(…))`, dans `Program.cs` et les deux hôtes `Banc.cs` ;
  un style ne battrait pas la liaison du gabarit ;
- Disposition rangée comme Publisher : « Ordre ▸ », « Aligner ou répartir ▸ » ; « Regrouper »
  refait le dernier groupe dissous, en un pas ;
- **Tab et Maj+Tab** passent au bloc suivant ou précédent dans l'ordre de la page, en sautant les
  masqués, quand la planche a le focus ; Tab dans le dock continue d'aller au champ suivant.

*Piège* : la cible du juge « Ajuster aux _marges », la ligne « Tr_ait » qu'exige A10 et le menu
Fenêtre que vérifie I2 restent tels quels. La navigation du focus est un gestionnaire d'instance de
la fenêtre ; `OnKeyDown` passe avant elle.

*Recette* :
- un contrôle des sources : aucune lettre d'accès en double à un même niveau, aucune accentuée ;
- `Alt`, `D`, `O`, `P` joue Premier plan ; le menu Disposition affiche « Ctrl+Maj+G » ;
- Regrouper refait le groupe en un pas ;
- Tab et Maj+Tab sautent un bloc masqué ; Tab dans le dock va au champ suivant ;
- au juge : une lettre d'accès en double.

Fichiers : `MainWindow.axaml` (menus), `MainWindow.axaml.cs` (Tab), `MainWindowViewModel.cs` (une
ligne, le groupe retenu), `Program.cs`, les deux `Banc.cs`, README (Objets), les recettes I2 et
I3, `DispositionTests`. Dépend de N10. Effort : moyen.

#### N14 — Les champs *(fiche 23, f2, f5, f7, f8, f10, f11)*

*Spécifié le 25 octobre 2026.* Les champs numériques sont des `NumericUpDown` d'Avalonia, qui
poussent la valeur à chaque frappe lisible — taper « 210 » dans la largeur de la page passe par
21 mm — et refusent en silence une saisie illisible ou hors bornes, sans la ramener à la borne. Un
seul suffixe, celui du champ, est retiré avant la lecture. Échap valide dans la barre (I3) et ne
fait rien au dock ; le catalogue et le dialogue des coordonnées ne se ferment pas par Échap.

Le poste :
- **`ChampNumerique`**, dérivé de `NumericUpDown` (`StyleKeyOverride`), ne pousse plus la valeur à
  chaque frappe : elle part à Entrée, à Tab, à la sortie du champ, à ↑ et ↓, à la molette ;
- **`Unite.Convert`** accepte mm, cm, pt, pi et pouces (« po », « in », `"`), à la virgule comme
  au point ; l'affichage reste en mm (décision du 24 octobre) ;
- **un refus** rend la valeur d'avant et le dit dans la barre d'état, bornes comprises ;
- **Échap** abandonne ce qui n'est pas validé et rend la main à la planche (règle 13) ; Entrée
  dans la barre valide et rend la planche ;
- les deux dialogues reçoivent leur bouton d'annulation (`IsCancel`) ;
- l'ordre de Tab au dock est éprouvé : il ne s'arrête plus sur les titres de section.

*Piège* : `ClosePropertyEdit` doit d'abord valider le champ en attente — `DispositionTests` garde
« 40 » au changement d'onglet. La cible du juge `texte = separateur == ","…` reste. Le test de
`BarreContextuelleTests` qui veut qu'Échap valide est réécrit (règle 14). La mémoire d'une case de
Q14 garde la valeur affichée exacte.

*Recette* :
- « 2 cm » dans X donne 20,0 mm ; « 12 pt » dans une marge donne 4,23 mm ; « 1" » donne 25,4 mm ;
- en tapant « 210 », la largeur de la page n'est écrite qu'une fois, en un pas ;
- « trente » laisse la valeur et le dit ; ↑ fait un pas ;
- Échap dans le corps rend la valeur d'avant ; Échap ferme le catalogue ;
- au juge : la valeur poussée à chaque frappe.

Fichiers : `Views/ChampNumerique.cs` (neuf), `MainWindow.axaml` (les champs du dock),
`BarreContextuelle.axaml` et son code, `MainWindow.axaml.cs` (`ClosePropertyEdit`),
`Converters/UnitConverters.cs`, les deux dialogues. Dépend de N9, pour le dock Document. Effort :
moyen.

#### N15 — L'application connaît la sélection du texte *(fiche 23, b1, b4)*

*Spécifié le 25 octobre 2026.* `CaretIndex` et `SelectionStart` n'apparaissent aujourd'hui que dans
les tests : seule la planche tient le `TextBox`, et le modèle de vue ne sait pas ce qui est
sélectionné dans le texte. R6, R7, N20 et N22 en ont besoin.

Le poste ajoute `SelectionDuTexte` — le bloc ou la case, le début, la fin, le curseur —, que la
planche tient et vide à la sortie de la saisie, et `ParagraphesDeLaSelection`, par
`Recalage.ParagrapheDe`. La sélection survit quand le focus va à la barre.

*Recette* : un glissé sur les caractères 4 à 7 donne (4, 7) ; `Maj+→` donne (4, 8) ; une frappe
donne (5, 5) ; le focus pris par le champ Corps ne change rien ; Échap vide la sélection. Au juge :
la sélection oubliée quand la barre prend le focus.

Fichiers : `PageCanvas.Saisie.cs`, `MainWindowViewModel.Saisie.cs` (neuf). Dépend de N12 et N2.
Effort : faible.

#### N16 — Tabulation et saut de ligne forcé *(fiche 23, a15, a16)*

*Spécifié le 25 octobre 2026.* Tab n'est pas inséré — `AcceptsTab` n'est pas posé — : il emmène le
focus au contrôle suivant et laisse la saisie ouverte, orpheline. Entrée, Maj+Entrée et Ctrl+Entrée
insèrent toutes un « \n », c'est-à-dire un paragraphe ; `TextBox.NewLine` vaut
`Environment.NewLine`, donc « \r\n » sous Windows. Publisher distingue la fin de paragraphe du saut
de ligne forcé (Maj+Entrée).

Le poste : `AcceptsTab="True"` sur le champ de la page ; `NewLine="\n"` sur ce champ et sur le champ
Contenu du dock ; Maj+Tab, en saisie, ne sort plus du bloc ; Maj+Entrée pose U+2028, que
`Compositeur` coupe sans fermer le paragraphe et que `Recalage` ne compte pas comme un saut. L'import
ne change pas : il rend aujourd'hui le saut forcé de Publisher par un paragraphe, et T1 de l'étude
de 98 l'en corrigera. Dans le dock, Tab reste la navigation entre champs.

*Piège* : la cible du juge `Text="{Binding Saisie, Mode=TwoWay}"` reste. Avant N21, le champ montre
la tabulation d'Avalonia et non le taquet de 36 pt.

*Recette* :
- Tab dans le 2ᵉ paragraphe met « \t » au curseur ; le mot suivant tombe au taquet de 36 pt, à
  l'écran comme au PDF, à 0,05 pt près (`pdftotext -bbox`) ; la saisie et le focus restent dans le
  champ ;
- Maj+Entrée coupe la ligne sans créer de paragraphe : le retrait de première ligne ne s'y applique
  pas, et le compte des paragraphes ne change pas ;
- la frappe fait un pas ;
- au juge : U+2028 compté comme un saut de paragraphe.

Fichiers : `PageCanvas.axaml`, `MainWindow.axaml` (champ Contenu), `PageCanvas.Saisie.cs`,
`Compositeur.cs`, `Recalage.cs`. Dépend de N15, dans la même copie, et de N3. Effort : faible à
moyen.

#### N17 — Les modificateurs du glisser, et le rectangle de sélection *(fiche 23, c8, c10 à c14)*

*Spécifié le 25 octobre 2026.* `ApplyMove` ne lit qu'`Alt` ; Ctrl et Maj font la sélection
additive et rien d'autre ; la seule copie est `Ctrl+D`, décalée de 12 pt ; une pression dans le
vide vide la sélection ; le curseur de rotation est une main.

Le poste :
- **Ctrl+glisser copie** : la bascule de Ctrl+clic attend le relâché, et si le geste passe le
  seuil de Q21, les clones bougent et les originaux restent, en un pas ;
- **Maj+glisser** tient l'axe dominant ;
- **au tracé**, Maj donne un carré ou un cercle, Ctrl trace depuis le centre ; **Ctrl sur une
  poignée** redimensionne depuis le centre ;
- **un rectangle de sélection**, tiré dans le vide ou depuis le gris, prend les blocs visibles dont
  l'emprise est tout entière dedans ; Maj ou Ctrl ajoute ;
- **Échap annule un glissé** en cours, bloc rendu à sa place, sans pas ;
- **le curseur de rotation** est dessiné avec le tracé de l'icône de rotation de l'apparence.

*Piège* : Ctrl et Maj changent de sens à la pression, et le test C3 du Maj+clic doit rester vert.
Les cibles du juge `var hit = DansLaPage(page) ? _vm.CurrentPage.Frames`, `if (!DansLaPage(page))
return;` et `var localX = dx * cos + dy * sin;` restent, une fois chacune. Le rectangle se dessine
dans l'`Overlay`, aux pinceaux de l'apparence lus par le code (règle 4).

*Recette* :
- Ctrl+glisser donne deux blocs en un pas ; Maj+glisser garde Y ;
- un tracé sous Maj a une largeur égale à sa hauteur ; Ctrl sur une poignée garde le centre ;
- un rectangle prend deux blocs sur trois, même tiré depuis le gris ;
- Échap après un glissé rend le bloc à sa place, sans pas d'annulation ;
- au juge : Ctrl+glisser qui déplace l'original.

Fichiers : `PageCanvas.axaml.cs`, `PageCanvas.Gestes.cs` (neuf), README (Souris). Dépend de Q21 et
N12. Effort : moyen.

#### N18 — Les menus vers le dock, et les pages, avec A8 et A9 *(fiche 23, b17, d16, e5, e8, e9, e12, e15, e16, e18, f17, f18)*

*Spécifié le 25 octobre 2026, sur la décision 5.* Publisher règle un objet par ses dialogues
*Format…* ; MyPublisher les a dans le dock, mais aucune entrée de menu n'y mène : les repères de
mise en page, le format des repères de règle, l'arrière-plan et le format de page ne s'atteignent
qu'en ouvrant le bon onglet et la bonne section.

Le poste :
- **le chemin vers le dock** : `MainWindowViewModel.VersLeDock.cs` lève une `DockDemande`, que la
  fenêtre traite — rouvrir le dock s'il est replié, choisir l'onglet, ouvrir la section, mettre le
  focus dans son premier champ. Il sert à *Disposition ▸ Repères de mise en page…*, *Repères de
  règle ▸ Format…*, *Page ▸ Arrière-plan…*, *Fichier ▸ Mise en page…*, *Texte ▸ Police…*,
  *Texte ▸ Paragraphe…* et *Rotation personnalisée…* ; A12 et le double-clic y prendront leur
  « Format du bloc… » ;
- **A8**, avec sa recette d'origine : le dialogue *Insérer des pages*, sur `Ctrl+Maj+N`, dont le
  champ « dupliquer les objets de la page » reste grisé tant que son option n'est pas choisie
  (p. 22) — un dialogue qui ne double aucun champ du dock ; `Ctrl+Maj+U` duplique la page ;
- **A9**, avec sa recette d'origine : *Faire pivoter ou retourner ▸* au menu Disposition, et deux
  boutons −90° et +90° au dock, sans icône, la recette I1 n'en admettant que de la maquette ;
- *Insertion ▸ Numéro de page, Date, Coordonnées* posent un bloc à champ (C4) ;
- **les pages au clavier** : `Ctrl+Pg. suiv.` et `Ctrl+Pg. préc.` changent de page ; `F5` et
  `Ctrl+B` (« Atteindre la page ») mettent le focus sur le numéro de page de la barre d'état, devenu
  un champ.

*Piège* : `GabaritsGardes` masque les sections d'une autre sorte de bloc ; la section se vise après
la mise en page. Le dialogue neuf s'inscrit dans `vues_de_la_phase` de la recette I1.

*Recette* :
- celles de A8 et de A9 ;
- chaque entrée « … » ouvre la bonne section, même dock replié, et n'empile aucun pas ;
- « 3 » puis Entrée dans le numéro de page mène à la page 3 ; « 99 » est refusé, avec un message ;
- au juge : une entrée « … » qui ouvre l'onglet sans la section.

Fichiers : `MainWindowViewModel.VersLeDock.cs` (neuf), `MainWindow.axaml` (menus, barre d'état),
`MainWindow.axaml.cs` (`DockDemande`), un dialogue neuf, la recette I1, README (Pages). Dépend de
N13. Effort : moyen.

#### N19 — Le corps et la police : une liste, et une frappe *(fiche 23, b6)*

*Spécifié le 25 octobre 2026.* Publisher propose le corps dans une liste de tailles, où l'on peut
aussi taper, et la police dans une liste où l'on tape son nom (p. 10, 48). Le corps de la barre est
un champ numérique sans liste ; la liste des polices n'est pas éditable.

Le poste : le corps devient une liste éditable des tailles standard — celle que N10 a définie dans
le noyau —, où l'on tape aussi une valeur décimale, au pas de 0,5 ; la police se tape, et la liste
propose les familles qui commencent par la frappe — « Geo » propose Georgia —, Entrée posant la
première. Une police tapée que le système n'a pas est refusée, avec un message (N14).

*Piège* : la garde de Q20 (`JamaisNul`, les polices du document absentes de la machine, libellées
« (absente) ») et la règle 1 (`.Single()` sur le corps et la police de la barre) tiennent ; la
mémoire d'une case de Q14 vaut pour le corps ; l'édition d'une `ComboBox` d'Avalonia 11.3 est à
éprouver d'abord.

*Recette* : taper « Geo » propose Georgia, et Entrée la pose en un pas ; la liste pose 14 pt en un
pas ; « 10,5 » tapé donne 10,5 pt ; une police absente du document reste proposée « (absente) » ;
au juge : la frappe qui pose la police à chaque lettre.

Fichiers : `BarreContextuelle.axaml` et son code. Dépend de N10. Effort : moyen.

### Lot 3 — Le clic droit, la recherche, et le texte tel qu'il s'imprime

A12, R6 et R7 sont repris dans leur section ; leurs ajouts y sont écrits. N21 attend la décision
de la voie (N7).

#### N20 — Rechercher et remplacer *(fiche 23, e23)*

*Spécifié le 25 octobre 2026, sur la décision 6.* Le livre ne le montre pas ; Publisher le fait
par *Edition ▸ Rechercher* et *Remplacer*, sur `Ctrl+F`, `F3`, `Maj+F4` et `Ctrl+H` dans l'aide en
français — touches réservées jusqu'ici, confirmées par la fiche 24.

Le poste :
- une **barre de recherche**, vue neuve posée sur la planche, en haut à droite, sans la déplacer
  (règle 3) et hors de `PageRoot` (règle 4), aux couleurs de l'apparence : le texte cherché, le
  texte de remplacement, *Respecter la casse*, *Mot entier*, Suivant, Remplacer, Remplacer tout ;
- le parcours va de bloc en bloc dans l'ordre des pages, puis de haut en bas et de gauche à droite
  sur la page : blocs de texte visibles et sans champ, cases de tableau comprises ; le texte trouvé
  est sélectionné en saisie (N12, N15), la page courante suivant ; un texte trouvé dans un groupe
  sélectionne le groupe et le dit, jusqu'à N26 ;
- *Remplacer* fait un pas ; *Remplacer tout* en fait un seul, pour tout le document ; le texte
  remplacé prend son style selon la règle de Q1 — celui du premier caractère remplacé — par
  `Recalage`, sans passer par l'écran.

*Piège* : la barre ne prend pas les lettres seules des outils, ni Échap au dock (règle 13 : Échap la
ferme et rend la planche). Elle s'inscrit dans `vues_de_la_phase` de la recette I1.

*Recette* :
- sur l'exemple, « bulletin » trouvé à chaque `F3`, chaque fois sélectionné en saisie à la bonne
  plage, la page suivant ;
- *Remplacer tout* « bulletin » par « journal » en un pas, les fragments gardés (le titre gras
  reste gras) ; une annulation rend le document ;
- la casse et le mot entier ; sur ATA 21, le compte égale celui d'une recherche sur le texte des
  blocs ;
- au juge : *Remplacer tout* qui empile un pas par occurrence.

Fichiers : `Views/BarreDeRecherche.axaml` (neuve), `MainWindowViewModel.Rechercher.cs` (neuf),
`MainWindow.axaml` (menu Edition, liaisons), README (Édition), la recette I1. Dépend de N15.
Effort : moyen.

#### N21 — La saisie montre le texte composé *(fiche 23, a10, a11, a20, a21)*

*Spécifié le 25 octobre 2026 ; sa forme dépend de la voie que le propriétaire choisit sur la
mesure de N7.* Le poste porte la voie choisie. Commun à toutes : `TexteCompose` reste visible
pendant la saisie, et ce qu'on édite est ce qui s'imprime ; le triple-clic sélectionne le
paragraphe (Avalonia sélectionne tout le texte) ; la sélection se montre sur le texte composé.
- **Voie 1** : le champ est transparent, posé sur la composition, avec les réglages que la note de
  N7 a mesurés — interligne lié, arrondi coupé, `NewLine`, calage, retraits du bloc dans le
  `Padding`, défilement empêché, curseur et sélection aux couleurs de l'apparence, la sélection en
  teinte translucide.
- **Voie 2a** : le champ est caché, hors du test de survol, de préférence unique et hors de
  `PageRoot` ; le curseur, la sélection, le triple-clic et le pré-édit se dessinent dans
  l'`Overlay` sur la composition. La navigation au clavier sur les lignes composées (Début, Fin,
  haut, bas, page) s'ouvre alors en poste à part, à la décision.
- **Voie 2b** ouvrirait en plus la frappe, la méthode d'entrée et le presse-papiers sans champ :
  le seuil D.

*Piège* : les couleurs viennent de l'apparence (recette I1) ; `Rendu.ZoneDeSaisie` (N1) suit le
champ là où il va ; en voie 2b, la cible Q1 de `PageCanvas.axaml` doit être déplacée.

*Recette* (commune) : pendant la saisie, `Rendu.Encre` de la page égale celle d'avant l'entrée, au
pixel, sur l'exemple et ATA 21 ; un clic sur le *k*-ième caractère d'ATA 21 met le curseur dessous,
à moins du seuil de N7 ; le triple-clic sélectionne le paragraphe ; au juge : `TexteCompose`
masqué de nouveau pendant la saisie.

Fichiers : `PageCanvas.axaml`, `PageCanvas.Saisie.cs`, `TexteCompose.cs`, l'apparence. Dépend de N7
et de la décision, et de N15. Effort : moyen en voie 1, lourd en voie 2.

#### N22 — Les caractères spéciaux *(fiche 23, a27, d14)*

*Spécifié le 25 octobre 2026.* Publisher montre les espaces, les tabulations et les fins de
paragraphe par *Affichage ▸ Caractères spéciaux* (`Ctrl+Maj+Y`, p. 9) et le bouton ¶ (p. 24).

Le poste : une bascule de la session, *Affichage ▸ Caractères spéciaux* et `Ctrl+Maj+Y`, qui
dessine dans l'`Overlay` un point pour chaque espace, une flèche pour chaque tabulation, ¶ à chaque
fin de paragraphe et ↵ à chaque saut forcé, aux positions de N3, à l'échelle et tournés avec le
bloc, d'une couleur de l'apparence ; rien dans `PageRoot`, rien au PDF ; en saisie, sur le texte
composé (N21). La maquette n'a pas d'icône ¶ : le menu et la touche suffisent.

*Recette* : sur `recette-texte-riche.mypub`, autant de ¶ que de paragraphes, chacun à
`PointDe(index du saut)` à 0,01 pt près ; `Rendu.Encre` de la page identique, bascule ouverte ou
fermée ; le PDF identique octet pour octet ; la bascule n'entre pas à l'historique ; au juge : une
marque dessinée dans `PageRoot`.

Fichiers : un fichier partiel de la planche pour les marques (neuf), `MainWindow.axaml` (menu
Affichage, liaison), README (Affichage). Dépend de N3 et N21. Effort : moyen.

#### N23 — Puces et numérotation *(fiche 23, b12)*

*Spécifié le 25 octobre 2026, sur la décision 6.* Publisher met en liste par les boutons Puces et
Numérotation (p. 10) et *Format ▸ Puces et numéros…* (p. 24). Les deux documents ATR s'en passent —
leurs listes tiennent par un retrait négatif et un caractère tapé —, mais un document neuf n'a
aucun moyen d'en faire une qui se renumérote.

Le poste : une propriété de paragraphe, `Liste`, sur le bloc et sur `ParagraphSpan` (clé additive,
règle 15) : aucune, puce (le caractère, « • » par défaut), ou numéros (« 1. », « a) », « i. ») avec
leur premier numéro. Poser une liste sur un paragraphe sans retrait lui donne le retrait suspendu
d'Office, par les réglages de R2. La composition dessine la marque dans le retrait de première
ligne, au style du premier caractère du paragraphe : un morceau sans position dans `Text`. Les
paragraphes consécutifs d'une même liste se numérotent de proche en proche ; un paragraphe hors
liste interrompt la suite ; le premier numéro la fait repartir. L'écran et le PDF passent par la
même composition. Entrée dans un élément continue la liste, par la règle de Q1 (le paragraphe
d'au-dessus) ; Entrée dans un élément vide le sort de la liste au lieu d'insérer un saut. Le
réglage vit dans la section Paragraphe du dock, et *Texte ▸ Puces et numéros…* y mène (N18) ; la
maquette n'a pas de bouton pour lui.

*Piège* : la marque n'a pas de position dans `Text` — `IndexSous` (N3) y rend le début du
paragraphe, N22 n'y dessine rien ; le compte de I9 voit les mêmes lignes ; une liste ne court pas
d'un bloc à l'autre (pas de chaînage) ; l'import ne change pas.

*Recette* :
- trois paragraphes, numéros posés sur le 2ᵉ et le 3ᵉ : « 1. » et « 2. » au PDF, au retrait de
  première ligne (`pdftotext -bbox`), et l'écran égal au PDF à 0,05 pt ;
- Entrée à la fin de l'élément 2 donne un élément « 3. », en un pas ; Entrée dans l'élément vide le
  sort de la liste ;
- l'aller-retour JSON ; les documents sans liste se rendent à l'identique ;
- au juge : la numérotation qui ne s'interrompt pas.

Fichiers : `ParagraphSpan.cs`, `Frame.cs`, `Compositeur.cs`, `Recalage.cs`, `PageCanvas.Saisie.cs`
(Entrée), `MainWindow.axaml` (dock Paragraphe), Samples, Templates. Dépend de R6. Effort : moyen à
lourd.

#### N24 — Exposant, indice, capitales *(fiche 23, b14)*

*Spécifié le 25 octobre 2026, sur la décision 6.* La barre Formatting de Publisher porte, absents
par défaut, Exposant, Indice, Petites majuscules et Majuscules (p. 10). Le modèle connaît déjà le
décalage de ligne de base, `BaselineShift`, que l'import remplit ; rien ne le pose, et les
capitales n'existent pas.

Le poste, sur la sélection (R7) ou le bloc entier hors saisie :
- **exposant et indice** posent le décalage et le corps que l'import écrit pour ceux d'Office ;
- **capitales et petites capitales** : une clé `Capitales` (aucune, capitales, petites capitales)
  sur le bloc et le fragment (règle 15). La composition dessine en capitales sans toucher à `Text`,
  et fabrique les petites capitales à 70 % de la hauteur des capitales (T10 de l'étude de 98) ; un
  caractère dont la capitale change de longueur (« ß ») reste tel quel, pour que les positions de
  N3 tiennent. L'import lit l'effet de libmspub ;
- `Ctrl+=` pour l'indice, `Ctrl+Maj+=` pour l'exposant, les touches des capitales de la fiche 24 ;
  au menu Texte et au menu contextuel du texte, la maquette n'ayant pas de bouton pour eux. Le zoom
  garde `Ctrl`+molette, `F9`, `Ctrl+Maj+L`, `Ctrl+−` et `Ctrl+0`.

*Recette* : « x2 » mis en exposant sur le « 2 » : au PDF, le glyphe à la ligne de base et au corps
attendus ; les petites capitales à 0,1 pt de 70 % de la hauteur des capitales ; `comparer-rendus.sh`
sous la tolérance ; chaque commande bascule, en un pas ; la recette R5 ajustée à ce que l'import
lit désormais ; au juge : les petites capitales à la hauteur des capitales.

Fichiers : `StyleSpan.cs`, `Frame.cs`, `Compositeur.cs`, `PubImporter.cs`,
`MainWindowViewModel.Raccourcis.cs`, `MainWindow.axaml` (menu Texte), Samples, Templates. Dépend de
R7. Effort : moyen.

#### N25 — La vérification orthographique *(fiche 23, a26)*

*Spécifié le 25 octobre 2026, sur la décision 6.* Publisher souligne les fautes pendant la frappe,
les propose au clic droit et les parcourt par `F7` ; MyPublisher n'a rien.

**À éprouver d'abord, et s'arrêter si l'une coince** : la bibliothèque, un Hunspell en code géré
(WeCantSpell.Hunspell), sa licence et sa version ; `Scripts/verifier-dependances.sh` (Q12) la
couvre. Les dictionnaires sont ceux du système, `/usr/share/hunspell` : `en_US` est installé, le
français demande `apt install hunspell-fr`, que le README dit comme il dit `libmspub`.

Le poste :
- un service du noyau, `Orthographe`, qui rend les plages fautives d'un texte dans une langue, mot
  par mot, avec un cache par mot ; la langue vient de N9 ; un dictionnaire absent le dit une fois, et
  rien n'est souligné dans cette langue ;
- un trait ondulé sous chaque faute des blocs visibles de la page courante, dans l'`Overlay`, aux
  positions de N3 — jamais dans `PageRoot` ni au PDF —, recalculé peu après la frappe ;
- `F7` ouvre la barre de N20 en mode orthographe : la faute suivante sélectionnée en saisie, ses
  suggestions, Ignorer, Ignorer tout, Remplacer, Ajouter au dictionnaire — le dictionnaire de
  l'utilisateur, par langue, dans les préférences de N11 ; au clic droit sur une faute en saisie,
  les suggestions viennent en tête du menu (A12) ;
- une case des préférences, « Vérifier pendant la frappe ».

*Piège* : les tests ne dépendent pas des paquets du système : un petit dictionnaire de test, écrit
pour, est rangé dans `Scripts/fixtures/`. Le soulignement d'une faute dans un bloc tourné tourne
avec lui.

*Recette* : avec le dictionnaire de test, « recieve » souligné et « receive » non ; `F7` parcourt
les fautes dans l'ordre de lecture ; Remplacer fait un pas ; un mot ajouté au dictionnaire survit à
un redémarrage ; `Rendu.Encre` de la page et le PDF identiques, vérification ouverte ou fermée ; sur
ATA 21 en `en-US`, le nombre de mots signalés est relevé et stable ; au juge : un trait ondulé dans
`PageRoot`.

Fichiers : `MyPublisher.Core/Services/Orthographe.cs` (neuf), le paquet, le fichier partiel des
marques de N22, la barre de N20, `MainWindow.axaml` (menu, liaison), README, `Scripts/fixtures/`.
Dépend de N9, N20, N21 et N22. Effort : lourd.

### Lot 4 — Les postes lourds de fin

Un par salve : tous les trois, et A11 et A13 qui les suivent, touchent `PageCanvas.axaml.cs`.

#### N26 — Taper dans le texte d'un groupe, et prendre un membre seul *(fiche 20 ; fiche 23, a6, c15)*

*Spécifié le 25 octobre 2026.* Le livre retape le texte d'un élément de la bibliothèque **dans le
groupe**, sans le dissocier (p. 58-60), et prend un membre d'un clic pour en remplacer l'image
(p. 58) ; seul le redimensionnement d'un membre demande de dissocier. La sélection de MyPublisher
ne voit que le premier niveau, et la saisie exige un `TextFrame` seul : il faut dissocier.

Le poste :
- un clic dans la zone de texte d'un membre traverse le groupe — `GroupFrame.Children`, puis
  `ToLocal` à chaque niveau — et ouvre la saisie de ce membre. **Le groupe reste la sélection** ;
  le texte en saisie est un autre état, `TexteEnSaisie`, distinct de `SingleSelected`, que la
  barre suit ;
- un clic sur un membre d'un groupe déjà sélectionné le prend seul : poignées, dock et barre le
  visent, et un clic hors du groupe rend la sélection ordinaire.

*Piège* : la recette Q23 exige `GabaritsGardes Bloc="{Binding SingleSelected}"` dans la barre, à
mettre à jour ; la découpe de Q5 ; les repères locaux et les rotations imbriquées ; la suppression,
les flèches et l'alignement d'un membre pris seul restent dans son groupe.

*Recette* : un clic sur le 3ᵉ caractère du texte d'un groupe ouvre sa saisie, curseur à 2, le groupe
restant sélectionné ; la frappe fait un pas, le groupe garde ses enfants ; un clic sur l'image d'un
groupe sélectionné la prend seule, et Suppr ne retire qu'elle ; au juge : la saisie qui dissocie
le groupe.

Fichiers : `PageCanvas.axaml.cs`, `PageCanvas.Saisie.cs`, `MainWindowViewModel.cs` (la sélection),
la barre, la recette Q23. Dépend de N12, N21 et R7. Effort : lourd.

#### N27 — Taper dans une case, sur la page *(fiche 23, g2 à g4)*

*Spécifié le 25 octobre 2026.* **Revient sur un choix écrit en C9** : « une case ne se choisit pas
sur la page ». On n'y saisit aujourd'hui qu'au dock, dans un champ de 54 px en police de
l'interface. Publisher y tape au clic, et Tab passe de case en case.

Une case n'existe qu'à la lecture : `CellFrames` reconstruit ses blocs à chaque changement, si bien
qu'un champ posé dans le gabarit serait détruit à chaque frappe. La saisie écrit donc
`TableCell.Saisie`. C'est direct en voie 2a, avec le champ unique hors de la page ; en voie 1, il
faut des cases stables, contraires au parti de C9, et le poste le tranche d'abord.

Les gestes : un clic dans une case y pose le curseur ; Tab passe à la case suivante, Maj+Tab à la
précédente ; Tab dans la dernière case ajoute une ligne, en un pas ; Ctrl+Tab insère une
tabulation ; les flèches passent d'une case à la voisine au bord du texte.

*Recette* : un clic dans la 2ᵉ case d'une bande, frappe « 12 » : le PDF porte « 12 » dans la case ;
Tab, Maj+Tab, puis la ligne ajoutée ; un pas par case ; au juge : Tab qui sort du tableau.

Fichiers : `PageCanvas.axaml.cs`, `PageCanvas.Saisie.cs`, `MainWindow.axaml`, `Table.cs`. Dépend de
N26 et N16. Effort : lourd.

#### N28 — Lignes et colonnes à l'endroit choisi, et plusieurs cases à la fois *(fiche 23, g5 à g7)*

*Spécifié le 25 octobre 2026.* Les compteurs du dock ajoutent et retirent des lignes et des
colonnes en fin seulement ; largeurs et hauteurs se règlent au dock ; la mise en forme vise le
tableau entier ou une case.

Le poste : insérer ou supprimer une ligne ou une colonne avant ou après la case du curseur, au menu
contextuel d'une case (A12) ; tirer une frontière de colonne ou de ligne à la souris ; sélectionner
plusieurs cases d'un glissé pour leur donner fond, alignement et mise en forme ensemble.

*Piège* : la somme des largeurs reste celle du tableau, au demi-point près (C9).

*Recette* : une colonne insérée avant la 2ᵉ décale les contenus, en un pas ; une frontière tirée de
10 pt rend 10 pt à la voisine ; trois cases sélectionnées prennent un fond en un pas ; au juge : la
somme des largeurs qui dérive.

Fichiers : `PageCanvas.axaml.cs`, `Table.cs`, `PageCanvas.axaml`. Dépend de N27 et A12. Effort :
moyen.

A11 et A13 ferment la phase, chacun dans sa salve : leurs ajouts sont écrits dans leur section.

---

## Ordre d'exécution

*Arrêté le 11 octobre 2026.* Les postes partent par **salves** de quatre à huit, chacun
dans sa copie de travail, et sont fusionnés après relecture adversariale. Une salve ne
réunit que des postes qui ne touchent pas les mêmes fichiers ; quand deux postes se
touchent, le second attend la salve suivante.

*Repris le 25 octobre 2026.* Depuis la phase I, un poste se fusionne dans `interface` **dès que
`Scripts/verifier.sh` passe** sur le résultat de la fusion ; la relecture adversariale et le juge
viennent après, en parallèle, et leurs corrections arrivent par des commits ultérieurs. La phase N
passe avant ce qui restait des salves 3 à 6, comme les deux lots de la phase I l'avaient fait, et
y prend R6, R7, A8, A9, A11, A12, A13, Q21 et Q22. Ses salves partagent `MainWindow.axaml`,
`MainWindow.axaml.cs` et le README quand les postes y touchent des régions différentes, jamais
`PageCanvas.axaml.cs` (règle 16).

| Salve | Postes | Pourquoi ensemble |
|---|---|---|
| **1** | Q1 · les sept recettes de P3 · Q2 · Q3 · Q12 | La dette qui abîme des données, le banc complet, et trois réparations courtes. **Fusionnée le 11 octobre 2026** |
| **2** | Q4 à Q9 · Q13 à Q17 · B2 · C5b · Q20 | Les écarts écran ↔ PDF, et les deux postes d'usage qui ne demandent rien. **Fusionnée le 13 octobre 2026** (juge 107/107) |
| **Lot 1** | I1 · puis I2 + B7 ∥ I4 · puis I3 ∥ I5 ∥ I6 · puis Q23 | La nouvelle interface, vue seule, en quatre vagues : la fenêtre et la planche sont chacune un seul fichier. Décidé le 24 octobre 2026. **Fusionné le 25 octobre 2026** |
| **Lot 2** | I7 · I8 · A10 · I9, en série | Ce que le modèle doit apprendre pour la maquette ; tous touchent `Frame.cs`. *Repris le 24 octobre 2026* : chaque poste part dès que ce qu'il touche est fusionné, sans attendre la fin du lot 1. I7, le modèle seul, part avec I2 et I4 ; I3 part après I2 et I7 et pose le bouton S ; I8 après I7 et I6 ; A10 après I8 ; I9 après A10. **Fusionné le 25 octobre 2026** |
| **Saisie 1** | Q24 puis Q25 (une copie) · N1 · N2 · N3 · N4 | La perte de frappe d'abord ; le banc des gestes ; les deux préalables du texte riche, dans le noyau et `TexteCompose` ; l'affichage au clavier, qui ne touche rien d'autre |
| **Saisie 2** | N5 · N6 · N7 · N8 · N9 | La sortie de saisie qui agit (`PageCanvas`) ; la touche laissée au champ (modèle de vue, changement de focus) ; la mesure de R7, au banc → **arrêt** ; le presse-papiers (`OnKeyDown`, menu Édition) ; la langue (noyau, import, dock Document) |
| **Saisie 3** | Q21 + Q22 · N10 · N11 | Le seuil du pointeur (`PageCanvas`) ; la table française et la recette A4 ; la question à la fermeture et les documents récents |
| **Saisie 4** | N12 · N13 · N14 | Le clic simple (`PageCanvas`) ; les menus ; les champs du dock et de la barre |
| **Saisie 5** | N15 puis N16 (une copie) · N17 · N18 · N19 | La sélection du texte, puis Tab et le saut forcé ; Ctrl+glisser et le rectangle (`PageCanvas`) ; les menus vers le dock, avec A8 et A9 ; le corps et la police, dans la barre |
| **Saisie 6** | A12 · N20 · N21 | Le clic droit (`PageCanvas`) ; rechercher ; la voie choisie — si N21 doit toucher `PageCanvas.axaml.cs`, il passe après A12 |
| **Saisie 7** | R6 · N22 | Le paragraphe du curseur ; les caractères spéciaux, dans l'`Overlay` |
| **Saisie 8** | R7 · N23 | Le texte riche à la souris ; les puces |
| **Saisie 9** | N24 · N25 | Exposant et capitales ; l'orthographe |
| **Saisie 10 à 14** | N26 · N27 · N28 · A11 · A13, un par salve | Tous dans `PageCanvas.axaml.cs` |
| **3** | A15 · B4 · Q11 | Ce qui reste des anciennes salves 3 et 5 : des postes courts, indépendants les uns des autres. B7 et Q23 étaient passés au lot 1, A10 au lot 2 ; Q10, absorbé par I4, est clos |
| **4** | B3 + B6 · C7 · A14 | L'impression et son aperçu, la bibliothèque, le contour du caractère, chacun précédé de son essai |
| **5** | C8 · Q18 · Q19 | Le miroir, et deux dettes sans urgence |
| **D** | Note de décision | Rédigée après la salve 5 ; **la décision revient au propriétaire du projet** |

Trois points où l'on s'arrête pour demander, plutôt que de décider seul : le choix de
voie, sur la mesure de N7 (salve Saisie 2) ; toute rupture de `FormatVersion` sans migration ;
et la recette de B3, qui demande une vraie imprimante. S'y ajoute, pour N25, la licence de la
bibliothèque d'orthographe, éprouvée avant d'écrire une ligne.

---

## Seuil D — Le moteur de composition

**Non planifié à ce stade. Décision à rouvrir quand A, B et C sont livrées.**

Trois fonctions du relevé n'existent que si le texte est *mesuré* avant d'être posé :

- **l'ajustement automatique** — réduire le corps jusqu'à ce que le texte tienne
  (fiche 12) ;
- **le chaînage entre blocs** — couper au bloc et reporter le reste dans le suivant
  d'une chaîne, avec les mentions de renvoi (fiche 12, limite n°1 du README) ;
- **l'habillage du texte** autour d'une image — composer en excluant des zones
  (fiche 22).

Ce sont trois expressions du même chantier, et il faut les décider ensemble, jamais
séparément.

**Ce que la décision engage.** Une chaîne de blocs suppose un identifiant sur les blocs,
qu'ils n'ont pas. Une mesure fiable suppose de trancher entre deux moteurs qui ne
mesurent pas à l'identique — celui de l'écran et celui du PDF — alors que tout
l'équilibre actuel du projet tient à ce que les deux partagent exactement les mêmes
données sans couche intermédiaire. Mesurer deux fois, c'est risquer de diverger ;
mesurer une fois et stocker, c'est introduire un état calculé dans un modèle qui n'en a
aucun.

*Précisé le 11 octobre 2026.* Depuis R1, les deux rendus partagent le **code** qui
coupe les lignes (`Compositeur`), mais pas encore la **mesure** des mots : chacun
interroge sa bibliothèque. Le risque de divergence n'est donc plus théorique. Q13 l'a
mesuré sur le document d'exemple, et propose une voie d'attente qu'il faut éprouver
avant de rouvrir cette décision.

**Ce qui est en jeu.** C'est le seul chantier lourd du corpus, et c'est aussi celui qui
sépare un poseur de blocs d'un logiciel de mise en page : sans lui, aucun article ne
court sur deux colonnes ni sur deux pages. Le repousser est une position défendable —
mais elle doit être prise, pas subie par défaut.

**Quand rouvrir.** Après C. À ce moment-là, C3 aura livré la sélection multiple, C2 la
grille, C4 les champs : le contexte technique sera plus favorable, et surtout on saura,
par l'usage des modèles de B1, si le besoin est réel.

---

## Ce que ce plan ne fait pas

Écarté délibérément, avec la raison :

| Fonction | Fiche | Raison |
|---|---|---|
| Barres d'outils personnalisables | 03 | Héritage d'Office 2007, abandonné par Publisher lui-même en 2010. Une barre courte et pensée vaut mieux. |
| Volet Office commutable | 04 | Réponse au manque de place d'un écran de 2007. Deux panneaux permanents sont plus lisibles. Retenu seulement : pouvoir les replier (B7). **Rouvert le 24 octobre 2026** par le propriétaire : le dock de la phase I a des onglets, que l'utilisateur choisit (I2). Ce que la ligne craignait — un panneau qui change de contenu sans prévenir — reste écarté. |
| Bibliothèque d'images en ligne | 13 | Service tiers, catalogue à maintenir, licences. Disproportionné pour un logiciel local. La variante locale rejoint C7. |
| WordArt | 17 | Glyphes à vectoriser des deux côtés de la chaîne, pour une esthétique datée. Le besoin réel — un titre qui se voit — se traite en propriétés ordinaires : contour et ombre du caractère (A14) ; le dégradé de C5c ne porte que sur les fonds. |
| Retouche d'image (luminosité, contraste, nuances de gris, couleur transparente) | 14 | Traitement pixel, coûteux à exporter proprement. Relève de l'éditeur d'images en amont. |
| Zone de travail hors page | 07 | Couverte autrement, et mieux, par C7. |
| Textures et motifs de remplissage | 09 | Ressources à embarquer, esthétique datée. |
| Bordure par côté, BorderArt | 11 | Écarté par la fiche : un filet par côté se pose avec des traits (A10), et les bordures illustrées sont datées. |
| Barre Image flottante | 14 | Ses commandes vont au panneau et au menu contextuel (A12) ; une barre de plus les dupliquerait. |
| Connecteurs, points d'édition, changer de forme automatique | 22 | Dessin vectoriel : relève d'un éditeur de dessin, pas d'un poseur de blocs. |
| Symbole, objet incorporé, lien hypertexte, signet | 22 | Hors du document imprimé que MyPublisher produit. Un symbole se tape ou se colle. |
| Embarquer les polices | 21 | Licences. B4 écrit la liste des polices employées, et B5 avertit de leur substitution. |
| Bibliothèque de conception (*Design Gallery*) | 20 | Contenu à dessiner et à maintenir. C7 en offre l'usage, avec le contenu de l'utilisateur. |
| Pages maîtres | 22 | Non écarté sur le fond — simplement non planifié. À reconsidérer avec C4, dont il partage la logique de contenu répété. La maquette de la phase I montre une section « Gabarits » ; elle n'est pas dessinée. |
| Aide « Tapez une question », Business Contact Manager, « Show Buttons on One Row », onglet Web | 23 | Office 2007, sans objet pour un logiciel local qui produit un document imprimé. Ajouté le 25 octobre 2026, avec la phase N. |
| Emboss, Engrave, 3-D, ombres de forme | 23 | Effets datés. A14 garde le contour et l'ombre du caractère, qui servent un titre. |
| Look Up, synonymes, Save as Picture, Edit Story in Word, macros, touches du ruban | 23 | Hors de ce qu'un poseur de blocs saisit ; les touches du ruban n'existent pas en 2007. |
| Dialogue Options, unité d'affichage réglable | 23 | Décision du 24 octobre 2026 : l'affichage est en mm. Une autre unité se tape et se convertit (N14). |
| Dialogues OK/Annuler qui doubleraient le dock : Format de la zone de texte, Police, Paragraphe | 11, 23 | Décision du 25 octobre 2026 : le dock reste, et les chemins de Publisher y mènent (N18, A12). Un dialogue qui ne double rien — Insérer des pages (A8) — reste un dialogue. |
| Barre de menus calquée sur celle de Publisher | 23 | La maquette fait foi pour les menus ; N13 range Disposition à la manière de Publisher, sans renommer la barre. |
| Pousser ▸, pas de déplacement réglable | 22, 23 | Les flèches, `Maj` et `Alt` suffisent. |
| Unité px, expressions dans les champs (« 210/2 ») | 23 | Le pixel n'a pas de sens pour un document imprimé ; une expression se calcule ailleurs. |

---

## Récapitulatif

Ce que chaque phase contient et ce qui la bloque. L'avancement, lui, se lit dans le
[tableau de suivi](#suivi) en tête : deux tableaux qui portent la même information
finissent toujours par se contredire.

| Phase | Postes | Nature | Bloquée par |
|---|---|---|---|
| **P** | P0 décision de nommage · P1 git · P2 contrôle · P3 banc | préalables | — |
| **A** | A1 rotation · A2 débordement · A3 chemins relatifs · A4 superposition · A5 formats · A6 barre d'état · A7 alignement vertical · A8 insertion de pages · A9 quart de tour · A10 trait · A11 aimantation · A12 menu contextuel · A13 rognage · A14 contour du caractère · A15 sections | finitions | — |
| **B** | B1 modèles · B2 import texte · B3 impression · B4 archive · B5 polices · B6 aperçu · B7 panneaux repliables | usage | — |
| **C** | C1 remplissage · C2 repères · C3 sélection multiple · C4 champs · C5 couleurs · C6 marges · C7 bibliothèque · C8 miroir · C9 tableau | structure | P1, P2 |
| **R** | R1 texte riche · R2 retraits · R3 colonnes · R4 interligne · R5 import `.pub` · R6 paragraphe au panneau · R7 texte riche à la souris | documents réels | R6 et R7 : la phase N (N2, N15, N21) |
| **Q** | Q1 à Q25 | dettes consignées | — |
| **I** | I1 apparence · I2 fenêtre · I3 barre contextuelle · I4 planche · I5 vignettes · I6 navigation et proportions · I7 souligné · I8 masquer un bloc · I9 lignes en excédent | nouvelle interface | I2 : I1 · I3, I5, I6 : I2 · lot 2 : lot 1 |
| **N** | N1 banc des gestes · N2 plage · N3 position des morceaux · N4 affichage au clavier · N5 clic de sortie · N6 touche au champ · N7 mesure de la saisie · N8 presse-papiers · N9 langue · N10 raccourcis français · N11 fermer sans perdre · N12 clic simple · N13 menus · N14 champs · N15 sélection du texte · N16 Tab et saut forcé · N17 modificateurs du glisser · N18 menus vers le dock · N19 corps et police · N20 rechercher · N21 texte composé en saisie · N22 caractères spéciaux · N23 puces · N24 exposant et capitales · N25 orthographe · N26 groupes · N27 cases · N28 lignes et colonnes | la saisie de Publisher | N21, R6, R7, N22 à N28 : la décision de voie sur N7 · N25 : la licence de sa bibliothèque |
| **D** | moteur de composition | décision | Q13, puis A, B, C |

Trois postes à faire en premier si l'on ne devait en retenir que trois, au
11 octobre 2026 : **Q1** (ne plus abîmer un document importé en tapant dedans), les
**recettes de P3** (que tout ce qui est clos se rejoue), et **Q13** (que l'écran et le
PDF coupent leurs lignes aux mêmes mots). Les deux premiers sont livrés avec la salve 1,
le jour même : reste Q13, en tête de la salve 2. Les trois que ce paragraphe nommait le
30 août — A2, B1, P0 — sont livrés. L'ordre complet est dans
[Ordre d'exécution](#ordre-dexécution).