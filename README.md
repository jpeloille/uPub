# MyPublisher

Un logiciel de PAO (mise en page) natif Linux, dans l'esprit de Microsoft Publisher :
on pose des blocs sur une page, on les déplace au pouce, et on exporte un PDF conforme.

Écrit en C# / .NET 10 avec Avalonia. Aucune dépendance à Wine, Mono ou GTK.

## Construire et lancer

```bash
dotnet build MyPublisher/MyPublisher.csproj
dotnet run --project MyPublisher                    # document vierge
dotnet run --project MyPublisher -- mon-doc.mypub   # ouvre un document
```

Sans interface, pour un script ou une chaîne d'intégration :

```bash
dotnet run --project MyPublisher -- mon-doc.mypub --export sortie.pdf   # exporte un PDF
dotnet run --project MyPublisher -- mon-doc.mypub --resave copie.mypub  # relit et réécrit
```

La réécriture ne sert pas à l'usage courant : elle donne prise au contrôle
ci-dessous. Ce n'est pas un « enregistrer sous » — elle réécrit le document tel qu'il
s'écrit **chez lui**, chemins d'images compris, la copie n'ayant d'autre destin que
d'être recopiée par-dessus l'original.

## Contrôle de non-régression

```bash
Scripts/verifier.sh                 # tests, contrôle, recettes — c'est lui qui fait autorité
Scripts/controle.sh                 # le seul contrôle de la chaîne d'écriture
Scripts/controle.sh --enregistrer   # remplace le relevé de référence, après examen
Scripts/eprouver-le-juge.sh         # sabote le code et vérifie qu'on s'en aperçoit
Scripts/eprouver-le-juge.sh --cibles   # chaque texte visé est là, une seule fois
Scripts/eprouver-le-juge.sh --tranche 1/4   # un sabotage sur quatre (copies en parallèle)
Scripts/comparer-rendus.sh doc.mypub 1   # formes et images : l'écran contre le PDF, à deux pixels par point
Scripts/comparer-mots.sh doc.mypub 1     # texte : l'écran contre le PDF, mot à mot, ligne à ligne
Scripts/bilan.sh                    # poste par poste : clos ou non, recette ou non
Scripts/verifier-dependances.sh     # aucune dépendance vulnérable (réseau ; à part)
```

Les recettes demandent trois outils hors .NET : `pdftoppm` (poppler-utils), `magick`
(ImageMagick 7) et `pub2raw` (libmspub-tools) :

```bash
sudo apt install poppler-utils imagemagick libmspub-tools
```

La recette de Q12, `Scripts/verifier-dependances.sh`, se lance à part : elle demande
l'accès à nuget.org pour lire la base des vulnérabilités connues, et échoue en le disant
quand elle ne peut pas la lire — jamais un vert faute de réseau. À jouer avant une
livraison, et chaque fois qu'on touche aux paquets.

Le dépôt se travaille sur un disque local. Un partage réseau monté en CIFS ne garde
pas le bit d'exécution : les scripts et le binaire compilé y tombent tous en
`Permission denied`, et `git status` y montre des fichiers modifiés qui ne le sont pas.

`Scripts/verifier.sh` est l'enveloppe : il enchaîne les tests, le contrôle ci-dessous,
puis les recettes de chaque poste. Le lancer avant et après un poste, plutôt que le
contrôle seul, qui ne lance pas les tests.

Deux projets de tests. `MyPublisher.Tests` éprouve le noyau sans plate-forme graphique,
en quelques centaines de millisecondes. `MyPublisher.Tests.Interface` ouvre la vraie
fenêtre **sans affichage**, sur Skia, et y synthétise de vrais pointeurs : c'est ainsi
qu'on vérifie qu'un bloc lâché près d'un bord de colonne s'y aimante, et qu'un
déplacement annulé rend exactement le document d'avant. `MyPublisher.Banc` fait la même
chose en ligne de commande, pour les scripts.

`Scripts/eprouver-le-juge.sh` est le contrôle du contrôle : il casse le code une chose à
la fois — l'aimantation, une propriété perdue à la copie, une étape de migration, la
mise à l'échelle d'un groupe — et exige que chaque sabotage soit attrapé. Un contrôle
qu'on n'a jamais vu échouer ne prouve rien. Chaque sabotage vise un texte présent une seule fois
dans son fichier ; `--cibles` le contrôle en quelques secondes, sans rien saboter, et
se lance avant d'ajouter un sabotage ou après une fusion.

Quatre vérifications : `Samples/exemple-bulletin.mypub` se relit, se réécrit au
caractère près, et son export PDF donne le même nombre de pages et les mêmes
avertissements que le relevé de `Scripts/reference.txt` ; puis chacune de ses
versions passées, figées dans `Scripts/fixtures/` — une par version du format —
s'ouvre encore et se migre en un point fixe. C'est un filet,
pas une stratégie de test : il tient la chaîne ouvrir → enregistrer → exporter, celle
dont tout le reste dépend, et le passé qu'on s'est engagé à relire.

## Partir d'un modèle

Au démarrage et sur `Ctrl+N`, MyPublisher propose un catalogue : bulletin trois
colonnes, dépliant trois volets, carte de visite, affiche A3 — ou la page blanche.

Un modèle n'est rien d'autre qu'un document : les fichiers vivent dans `Templates/`,
à côté du binaire, et s'ouvrent, se retouchent et se réenregistrent comme n'importe
quel `.mypub`. En ajouter un, c'est y déposer un fichier.

Partir d'un modèle le charge **sans reprendre son chemin** : `Ctrl+S` propose donc
*Enregistrer sous*, et le modèle d'origine ne peut pas être écrasé par mégarde.

Les modèles se servent des fonctions que l'application offre déjà — grille de
composition, repères de pli, folio automatique, coordonnées professionnelles — ce
qui est aussi le seul moyen de les rendre visibles : une page blanche n'apprend rien.

## Interface

La fenêtre suit la maquette de `Maquettes/` (phase I du plan). De haut en bas : la
**barre de menus**, avec le titre du document au milieu ; la **barre principale**, à
icônes — document, annulation, alignement, ordre, zoom, et l'export en bouton plein à
droite ; la **barre contextuelle**, qui suit la sélection ; l'espace de travail ; la
**barre d'état**. L'espace de travail se lit de gauche à droite : le **rail d'outils**,
le panneau **Pages**, la **planche**, et le **dock** à quatre onglets — *Propriétés*,
*Styles*, *Couleurs*, *Document*.

Le **rail** arme un outil : on trace ensuite le cadre directement sur la page, à la taille
voulue. Un simple clic sans glisser pose le bloc à sa taille par défaut. L'outil se
désarme tout seul après usage, pour qu'on puisse ajuster immédiatement ce qu'on vient de
poser ; un bloc de texte passe même directement en saisie. L'image y est rangée parmi les
outils, mais ne se trace pas : elle demande d'abord un fichier.

Deux outils du rail ne posent rien : ils naviguent. La **main** fait glisser la planche,
sans toucher à aucun bloc ; `Espace` tenu la prête sans changer d'outil. La **loupe**
zoome d'un pas au clic, en arrière sous `Alt`, et le point visé reste sous le pointeur —
comme sous `Ctrl` + molette. **Page entière**, dans la barre principale, tient la page
dans la zone visible. Aucun de ces gestes n'entre dans l'historique ni ne rend le
document « modifié », et le zoom ne change jamais de lui-même, ni à l'ouverture ni quand
la fenêtre change de taille.

Le panneau **Pages** montre chaque page en vignette, au rapport de la page, son numéro
dessous ; la page courante est bordée de bleu, et un clic sur une autre y mène. La vignette
est un schéma, comme celles du catalogue de modèles — le texte en filets, à la place des
lignes de la page, l'image en cadre barré, les formes et le fond de page dans leurs
couleurs —, et elle suit chaque retouche, l'annulation comprise.

La **barre contextuelle** dit d'abord ce qui est sélectionné — « Bloc de texte »,
« Image », « 3 blocs », « Page » quand rien ne l'est — et porte ce qui s'y rapporte. Pour
un bloc de texte : la police, le corps, le gras, l'italique, le souligné — le bouton S
bascule le trait simple, le chevron qui le suit propose le double, l'épais et « mots
seuls » —, les quatre alignements, l'interligne en multiple et la couleur du texte, que
sa vignette ouvre. Un tableau y a la même mise en forme, sans l'alignement, qui est celui
de chaque case. Un groupe s'y dissocie, plusieurs blocs s'y alignent et s'y répartissent.
Ce que la barre porte, le dock ne le porte plus. Chaque clic y est un pas d'annulation ;
`Échap` quitte un de ses champs et rend les lettres seules aux outils.

L'onglet *Propriétés* montre le bloc sélectionné en sections repliables : Position et
taille, puis ce qui est propre au type — pour un bloc de texte, Cadre de texte,
Paragraphe et Contenu —, puis Fond et contour. Une section fermée le reste d'un bloc à
l'autre, le temps de la session. Les longueurs portent leur unité dans le champ
(« 52,0 mm ») ; on les saisit avec ou sans elle, à la virgule comme au point. Chaque champ,
quel que soit son onglet — et chaque champ de la barre contextuelle —, est **un pas
d'annulation**. La case **Conserver les
proportions**, réglage de la session et non du document, fait garder au bloc son rapport
quand on tire un coin ou qu'on saisit sa largeur ou sa hauteur ; les poignées de côté ne le
gardent pas, et `Maj` pendant le glissé fait l'inverse de la case. *Styles* et *Couleurs* portent les
jeux du document ; *Document*, le format, les marges, la grille, les repères de règle et
le fond de page.

Au pied de *Propriétés*, **Blocs de la page** montre la pile de la page, **l'avant en
tête**, et se laisse réordonner en y tirant un bloc — un trait bleu marque où il se
posera. Le clavier fait la même chose en plus rapide sur les deux extrêmes : `Ctrl+Maj+F`
et `Ctrl+Maj+B`. L'**œil** de chaque ligne masque le bloc, ou le montre : masqué, il n'est
ni dessiné ni exporté, un clic à sa place prend le bloc d'en dessous, et il ne s'aligne ni
ne se répartit. Il reste dans la liste, l'œil fermé, et s'y sélectionne ; « Tout
sélectionner » le laisse. Le geste s'annule, et se garde à l'enregistrement.

La **barre d'état** affiche en permanence, à droite, la position du curseur, puis la
position et la taille du bloc sélectionné, la page, le format et le zoom, avec son curseur
en échelle logarithmique. La position et la taille suivent le geste pendant qu'on déplace
ou qu'on redimensionne : au moment où l'on tire une poignée, l'œil est sur la page et non
sur le dock.

`Ctrl+F1` — ou *Fenêtre ▸ Replier les panneaux* — masque le panneau Pages et le dock pour
rendre leur place à la planche ; un second `Ctrl+F1` les rend tels qu'ils étaient. Le menu
Fenêtre masque aussi l'un ou l'autre.

## Ce que l'application sait faire

- Documents multi-pages, portrait ou paysage, marges réglables. Neuf formats au
  catalogue — A3, A4, A5, B4, B5, Executive, Letter, Legal, Tabloid — et, pour tout le
  reste, une **largeur et une hauteur libres** saisies en millimètres : le sélecteur de
  format se vide alors, ce sont les dimensions qui font foi.
- Champs : un bloc de texte peut afficher son numéro de page, le nombre de pages, la
  date ou une coordonnée professionnelle au lieu d'un texte saisi ; un bloc image peut
  afficher le logo. Le folio se numérote seul, à l'écran comme au PDF.
- Informations professionnelles (*Édition ▸ Informations professionnelles…*) : des jeux
  de coordonnées nommés — nom, fonction, société, adresse, téléphone, devise, logo —
  saisis une fois et réinjectés dans tous les blocs qui portent le champ correspondant.
  Plusieurs jeux coexistent, on bascule de l'un à l'autre.
- Blocs de texte (police système, corps, gras, italique, alignement dont justifié,
  calage vertical du texte dans son bloc — haut, milieu, bas —, interligne, marges
  intérieures réglées côté par côté, couleur), images (trois modes de cadrage) et
  formes (rectangle, ellipse, trait).
- **Le trait, et la flèche** : l'outil Ligne (`L`) le trace d'une extrémité à l'autre,
  `Maj` le tenant à 0, 45 ou 90° ; *Insertion ▸ Trait* le pose à plat. Il se reprend par
  ses deux extrémités, à la planche ou au dock, se prend à trois points de son encre, et
  porte une pointe de flèche facultative à chaque bout. Son épaisseur, son tracé et sa
  couleur sont ceux du contour. Un trait horizontal n'a pas de hauteur, et n'en reçoit
  jamais : il ne se redimensionne pas comme un cadre.
- **Tableaux** : des bandes de cases, avec leurs colonnes et leurs lignes. Chaque case
  porte son texte, son alignement, son fond et son contour ; la grille se règle d'un
  geste pour toutes. Les colonnes et les lignes font la taille du bloc : élargir une
  colonne élargit le tableau, et tirer le tableau à la poignée mesure ses cases à la
  même aune. Une case se remplit dans la section Tableau du dock, où elle est nommée par
  son rang — « L1 · C2 ».
- **Plusieurs styles dans un même bloc** : une portion de texte porte sa propre police,
  son corps, son gras, son italique, son souligné, sa couleur, et se pose en indice ou en
  exposant.
  Ces styles suivent la frappe : un mot tapé dans un bloc importé laisse ses gras sur
  leurs mots, et un saut de ligne laisse chaque retrait à son paragraphe. Le texte tapé
  prend le style du caractère qui le précède ; en tête de texte ou de paragraphe, celui
  du caractère qui le suit — un mot tapé devant un titre en gras est en gras. Une
  sélection retapée garde le style de son premier caractère. C'est la règle de
  Publisher et de Word.
  L'interligne s'exprime en multiple de la hauteur **naturelle** de la police — « 1,25 »
  veut dire une fois et quart ce que la police demande, jamais une fois et quart le
  corps.
- **Le souligné** a les dix-sept sortes de Publisher, rangées en quatre réglages comme
  dans OpenDocument : le trait — plein, pointillé, tirets, tirets longs, tiret-point,
  tiret-point-point, vague —, simple ou double, de l'épaisseur de la police ou deux fois
  plus épais, et « mots seuls », qui ne passe pas sous les blancs. Il se pose au bloc ou
  à une portion, se dessine à la place et à l'épaisseur que la police donne (sa table
  `post`), à l'écran comme au PDF, et passe sous les blancs qu'une ligne justifiée
  élargit. Un `.pub` importé garde le sien. Le bouton S de la barre contextuelle,
  `Ctrl+U` et *Texte ▸ Souligné* basculent le trait simple du bloc sélectionné — ou
  retirent son souligné, quelle qu'en soit la sorte ; le chevron du bouton pose le
  double, l'épais ou « mots seuls ».
- **Retraits et espacement de paragraphe** : retrait à gauche, à droite, et retrait de
  première ligne — négatif, il fait pendre cette ligne à gauche des suivantes, ce qui
  est la façon d'aligner une liste à puces. Plus un espace avant et un espace après le
  paragraphe. Le dock les règle pour le bloc entier ; le fichier, lui, sait déjà les
  porter paragraphe par paragraphe, ce dont les documents Publisher se servent
  abondamment.
- **Colonnes à l'intérieur d'un bloc de texte** : on donne au bloc un nombre de
  colonnes et une gouttière, et le texte remplit la première jusqu'au bas du bloc
  avant de passer à la suivante. C'est autre chose que la grille de composition plus
  bas, qui pose des colonnes sur la *page* comme repères d'aimantation ; ici le texte
  coule d'une colonne à l'autre dans un seul bloc. Ce qui dépasse la dernière colonne
  continue sous le bloc, comme un texte trop long l'a toujours fait.
- Tout bloc — texte, image, forme, groupe — porte un fond et un contour : épaisseur,
  trait continu, tirets ou points, angles arrondis. Encadrer une image d'un filet ne
  demande donc plus de lui superposer une forme. Le contour est centré sur le bord du
  bloc, à l'écran comme au PDF — mesuré de 0,5 à 5 pt sur des blocs de texte, des
  rectangles et des ellipses ; un contour en tirets ne commence pas son motif au même point des deux côtés.
- **Le fond n'est pas forcément uni** : c'est au choix un aplat avec son opacité, un
  dégradé de deux couleurs le long d'un angle, une image, ou une teinte dérivée d'une
  couleur de base. Textures et motifs : non, délibérément.
- **Fond de page** : le même choix, posé sur la publication entière — onglet
  *Document* du dock. Un dégradé de page se règle en trois gestes et change tout l'air d'un
  document.
- **Jeu de couleurs et jeu de polices**, onglets *Couleurs* et *Styles* du dock : cinq couleurs nommées et
  une paire titre/corps, portées par le document. Chaque sélecteur de couleur montre les
  cinq pastilles du jeu, et la liste des polices commence par le titre et le corps du
  jeu. Comme dans Publisher, ce qu'on y prend est une **référence** : changer de jeu
  recolore — ou change de police — tous les blocs qui s'y réfèrent, à l'écran comme au
  PDF, et eux seuls. Une couleur choisie au sélecteur reste libre. Les blocs neufs
  prennent leurs couleurs et leurs polices dans le jeu ; six jeux de couleurs et cinq
  jeux de polices sont livrés, et les quatre modèles portent le jeu « Marine ».
- Rotation libre de n'importe quel bloc, y compris pour la sélection et le
  redimensionnement. Elle se prend **à la souris**, par une neuvième poignée posée
  au-dessus du bloc — un disque blanc bordé d'accent, au bout d'une tige — ou se saisit
  au degré près dans le dock. `Maj` pendant le geste tourne par pas de 15°, et remet
  d'aplomb un bloc qui ne l'est pas.
- Groupes : plusieurs blocs réunis se déplacent, se tournent et se redimensionnent
  d'une pièce — le redimensionnement met les blocs du groupe à la même échelle. Un
  bloc d'un groupe ne se sélectionne pas directement : on dissocie pour le retoucher.
- Sélection d'un bloc ou de plusieurs (`Ctrl` + clic), déplacement d'ensemble,
  redimensionnement par huit poignées, rotation par une neuvième. Les poignées ne
  paraissent que sur un bloc seul : pour tourner ou redimensionner plusieurs blocs, on
  les groupe. Le cadre de sélection passe à 4 px du bloc et tourne avec lui ; pendant
  qu'on trace un bloc ou qu'on le redimensionne, une bulle sous lui dit sa largeur et
  sa hauteur — les mêmes que la barre d'état. Un clic sur le gris de la planche libère
  la sélection, même là où passe la part rognée d'un bloc qui déborde, et une poignée
  qui sort de la page s'y saisit encore ; un outil armé n'y trace rien et reste armé.
- Grille de composition : colonnes et lignes avec leur gouttière, grille de ligne de
  base réglée en points. L'aimantation s'y accroche comme aux marges, aux bords et à
  l'axe médian de la page.
- Règles fixes au bord haut et au bord gauche de la planche, graduées en millimètres de
  part et d'autre de la page — négatives à gauche et au-dessus, le zéro au coin de la
  page —, où la sélection se projette en bande bleue pâle. Repères libres posés où l'on
  veut, et tracés sur toute la planche : on les glisse depuis une règle, on les déplace,
  on les retire en les lâchant hors de la page, et l'aimantation s'y accroche. Aucun
  repère ne s'imprime ; *Affichage ▸ Repères* et *Règles* les effacent de l'écran — et
  *Repères* efface aussi les marges en pointillé et le marqueur de débordement, qui ne
  s'impriment pas davantage.
- Édition du texte directement sur la page (double-clic).
- **Importer un texte** — *Texte ▸ Importer un texte…*, ou le bouton du même nom à la
  section Contenu d'un bloc de texte : un fichier texte remplit le bloc sélectionné, au style du
  bloc. Il se lit en UTF-8, avec ou sans BOM, en UTF-16 s'il porte sa marque (FF FE ou
  FE FF), ou en Latin-1 quand il n'est pas de l'UTF-8 valide (lu comme Windows-1252, qui
  garde aussi « ’ », « œ » et « € ») ; les fins de ligne CRLF deviennent un seul saut.
  Un texte trop long se signale comme tout autre, et une seule annulation rend le bloc
  d'avant ; une saisie ouverte sur la page est refermée d'abord, en un pas à elle.
  Sont refusés, bloc intact et raison à la barre d'état : un fichier vide, un fichier
  de plus de 128 Kio, un fichier qui porte un octet nul ou un caractère de commande
  (un binaire, de l'UTF-16 sans marque), un fichier qu'on n'a pas le droit de lire.
  *Limite* : un fichier Latin-1 dont tous les accents forment par hasard de l'UTF-8
  valide — un texte court en capitales, « CAFÉ » suivi d'une espace insécable — se lit
  en UTF-8 ; un seul « é » suivi d'une lettre ailleurs dans le fichier suffit à le
  faire lire en Latin-1.
- Ordre de superposition ; alignement (gauche, centre, droite, haut, milieu, bas) et
  répartition d'une sélection ; ajustement aux marges. L'alignement se fait par
  rapport à la zone de composition, ou — case *Disposition ▸ Par rapport aux marges*
  décochée — par rapport à la page pour un bloc seul et aux blocs entre eux dès
  qu'ils sont plusieurs.
- Annulation / rétablissement sur 60 pas, couvrant aussi bien les gestes à la
  souris que les champs du dock, quel que soit leur onglet, et ceux de la barre
  contextuelle.
- Enregistrement au format `.mypub` (JSON lisible) et export PDF.
- **Un dossier de travail se déplace sans casser ses images** : les images rangées à
  côté du document — ou sous son dossier — y sont référencées relativement à lui.
  Celles qui vivent ailleurs gardent leur chemin absolu.
- **Une image retouchée dans un autre outil se relit en rouvrant le document.** Tant
  qu'on ne le rouvre pas, l'écran garde l'image qu'il a lue, alors que l'export relit
  toujours le fichier ; l'annulation ne relit rien.
- **Ouverture des documents Microsoft Publisher (`.pub`)** : pages, blocs de texte avec
  leurs fragments de style, retraits et fers par paragraphe, tabulations, tableaux,
  cadres, traits, et les **images posées en remplissage de forme** — c'est ainsi que
  Publisher illustre, et c'est ainsi que le font les documents à reprendre. L'import dit
  ce qu'il n'a pas su rendre plutôt que de le perdre en silence.
- **Un bloc trop plein le dit.** Le texte qui ne tient pas dans son bloc n'est imprimé
  ni à l'écran ni au PDF — c'est ainsi que Publisher se comporte — et le bloc porte
  alors un petit **A rouge** à son coin, comme dans Publisher. L'export le répète et
  chiffre ce qui manque : « Page 1 · Consigne : le texte déborde du bloc de 8,6 pt — ce
  qui dépasse n'est pas imprimé. » Il se tait, en revanche, quand le texte ne fait
  qu'entamer la marge intérieure basse : rien n'y est coupé.
- **L'export dit quelles polices il n'a pas trouvées**, et par quoi il les a
  remplacées : « Police substituée : « Wingdings » demandée, « DejaVu Sans »
  employée ». Une substitution muette ne dégrade pas seulement l'allure — sur une
  fiche de procédure, elle remplace une flèche par une lettre. Il ne nomme que les
  polices qu'il dessine : un fragment entièrement recouvert par un autre ne dit rien.
  Dans la barre contextuelle, la liste des polices propose aussi celles que le document
  emploie sans que la machine les ait, marquées « (absente) » : sélectionner un tel bloc
  ne touche pas à sa police.

## Raccourcis

Rangés par domaine, chacun dans son sous-tableau. Les recettes lisent tous les
sous-tableaux comme un seul : un geste n'y figure qu'une fois, quel que soit le
domaine où il est rangé.

### Fichier

| Raccourci | Action |
|---|---|
| `Ctrl+N` / `Ctrl+O` / `Ctrl+S` | Nouveau (catalogue de modèles) / Ouvrir / Enregistrer |
| `Ctrl+E` | Exporter en PDF |

### Édition

| Raccourci | Action |
|---|---|
| `Ctrl+Z` / `Ctrl+Y` (ou `Ctrl+Maj+Z`) | Annuler / Rétablir |
| `Ctrl+D` | Dupliquer la sélection |
| `Ctrl+A` | Sélectionner tous les blocs de la page |
| `Échap` | Désarme l'outil, sinon désélectionne ; dans un champ de la barre contextuelle, le quitte |

### Objets

| Raccourci | Action |
|---|---|
| `Ctrl+Maj+G` / `Ctrl+Maj+H` | Grouper / Dissocier |
| `Ctrl+Maj+F` / `Ctrl+Maj+B` | Premier plan / Arrière-plan |
| `Suppr` | Supprimer la sélection |
| Flèches | Déplacer de 3 pt (`Maj` : 0,5 pt) |

### Texte

| Raccourci | Action |
|---|---|
| `Ctrl+G` / `Ctrl+I` / `Ctrl+U` | Gras / Italique / Souligné du bloc de texte sélectionné |

### Affichage

| Raccourci | Action |
|---|---|
| `Ctrl+F1` | Replier les panneaux, puis les rendre |
| `Ctrl` + molette, `Ctrl` `+`/`-`/`0` | Zoom — à la molette, le point visé reste sous le pointeur |
| `Espace` tenu | La main : un glissé fait défiler la planche, sans changer d'outil (hors saisie, et pas quand un bouton a le focus) |
| Clic, `Alt` + clic de la loupe | Zoom avant, zoom arrière, autour du point visé |

### Outils

| Raccourci | Action |
|---|---|
| `V` / `T` / `H` / `R` / `E` / `L` / `B` / `Z` | Outil sélection / texte / titre / rectangle / ellipse / ligne / tableau / loupe |

### Souris

| Raccourci | Action |
|---|---|
| Double-clic | Éditer le texte d'un bloc |
| `Ctrl` ou `Maj` + clic | Ajoute un bloc à la sélection, ou l'en retire |
| `Alt` pendant le déplacement | Désactive l'aimantation |
| `Maj` sur une poignée d'angle | Conserve les proportions — ou les libère, quand « Conserver les proportions » est cochée |
| `Maj` sur la poignée de rotation | Tourne par pas de 15° |
| `Maj` en traçant un trait, ou sur l'une de ses extrémités | Tient le trait à 0, 45 ou 90° |

## Choix techniques

**L'écran et l'export composent le texte avec la même fonction.** `Compositeur`, dans le
noyau, découpe le texte en lignes, les range dans les colonnes du bloc et répartit le
blanc d'une ligne justifiée. Il ne connaît ni Avalonia ni PDFsharp : la règle à mesurer
lui est passée en paramètre, et chaque rendu lui passe la sienne. Tant que la découpe
appartenait à chacun — Avalonia à l'écran, `XTextFormatter` au PDF — rien ne garantissait
qu'ils coupent aux mêmes endroits, et l'interligne du modèle ne franchissait tout
simplement pas l'export.

Chaque ligne en sort avec son retrait, sa largeur disponible, sa colonne et l'ordonnée de
son sommet. Aucun des deux rendus n'additionne donc plus de hauteur pour son compte, et
ce n'est pas une élégance depuis qu'il y a des colonnes : le passage à la colonne
suivante est un seuil, et un écart de métrique d'un centième de point suffirait à faire
basculer une ligne d'un côté et pas de l'autre.

**La coupure se décide sur la plus large des deux mesures.** Les deux rendus ne
mesurent pas un mot à l'identique — Avalonia crénèle, PDFsharp non —, et un mot qui
tient au centième de point d'un côté passait à la ligne de l'autre. Chacun décide donc
sur le maximum de sa mesure et de celle de l'autre, qui ne dépend pas de l'ordre de ses
termes, puis dessine à la sienne. L'export reçoit pour cela la règle de l'écran ;
`--export`, qui tourne sans fenêtre, monte d'Avalonia ce qu'il faut pour mesurer.

**Les polices du texte des documents sont résolues par MyPublisher.** Avalonia rabat une
face absente — l'italique de DejaVu Sans, qui n'en a pas — sur la face qu'il a déjà
chargée : l'italique sortait en gras dans un processus, en romain penché dans un
autre. `PolicesDuDocument` demande à fontconfig le fichier que l'export emploie, et le
charge avec les simulations que fontconfig décide : une fois par variante, quoi que le
processus ait chargé avant. L'italique absent se penche à l'écran et au PDF ; le chrome de
l'application garde les polices du système. L'application, l'export sans fenêtre, le
banc et l'hôte des tests montent ces polices de la même façon
(`AvecLesPolicesDeMyPublisher`).

**Un remplissage ne se calcule qu'une fois.** Les extrémités d'un dégradé et la couleur
d'une teinte sont calculées dans le modèle ; l'écran et l'export les lisent, aucun des
deux ne les refait. Ce n'est pas une élégance mais la seule parade contre une erreur
qu'aucune vérification ne verrait : une comparaison de l'écran au PDF ne peut pas
attraper une erreur **partagée**, et elle ne peut pas davantage attraper une erreur qui
n'existe qu'une fois — les deux rendus s'accordent, et se trompent ensemble. Échanger les
deux extrémités d'un dégradé trace tous les dégradés du document à l'envers et laisse la
comparaison des deux rendus au vert, au chiffre près. Ce sont des tests du noyau qui
épinglent ces valeurs-là.

**Un tableau se rend comme un groupe.** Ses cases ne sont pas dessinées : elles sont
calculées en blocs de texte, en coordonnées relatives, à partir de ses colonnes, de ses
lignes et de ses cellules. L'écran les affiche par le gabarit du bloc de texte, l'export
les trace par sa fonction de bloc de texte — aucun des deux n'a de rendu de tableau qui
lui soit propre. Une case hérite ainsi de tout ce que le texte sait déjà faire :
composition partagée, fragments de style, interligne, fond et contour.

**Toute la géométrie est en points typographiques (1/72 pouce).** Avalonia raisonne
en pixels indépendants du périphérique (1/96 pouce) et le PDF en points : modéliser
en pixels imposerait une multiplication par 0,75 à chaque export, avec une dérive
d'arrondi qui s'accumule sur un document multi-pages. La conversion n'existe qu'à
un seul endroit, `MainWindowViewModel.RenderScale`, appliquée comme transformation
de mise en page sur le plan de travail. À l'intérieur de cette transformation, une
unité vaut exactement un point : les liaisons XAML n'ont donc aucune conversion à
faire, et l'export PDF se fait à l'identité.

**Le modèle (`Models/`) ne référence aucun type Avalonia.** Les couleurs y sont des
chaînes `#AARRGGBB` — ou une référence au jeu de couleurs, `scheme:accent1`, que
`Charte` résout, une fois, pour l'écran comme pour l'export —, les alignements des
énumérations maison. La traduction vers les
types Avalonia se fait dans `Converters/`. C'est ce qui permet au rendu écran et à
l'export PDF de partager exactement les mêmes données, sans couche de conversion
intermédiaire susceptible de diverger.

**Les polices sont résolues par fontconfig.** PDFsharp ne connaît pas les polices
Linux : sans `FontconfigFontResolver`, l'export échoue dès qu'on demande autre chose
qu'une police embarquée. On délègue à `fc-match`, qui gère aussi les substitutions
quand la famille demandée n'existe pas sur la machine.

`fc-match` rendant **toujours** quelque chose, on lui demande la famille en même temps
que le fichier, et on la confronte à celle qu'on voulait : c'est ce qui permet
d'avertir. La comparaison cherche le nom demandé **parmi** ceux que fontconfig annonce,
et non l'égalité des chaînes : fontconfig rend tous les noms d'une même famille d'un
coup — `DejaVu Sans,DejaVu Sans Condensed` — et l'égalité ferait passer pour substituées
des dizaines de polices pourtant installées. Un avertissement qui crie au loup ne se lit
plus.

**Les icônes sont les tracés de la maquette**, sur sa grille commune de 24 unités, avec
leurs épaisseurs de trait, déclarées une fois dans `Views/Apparence.axaml` avec la palette
et les classes de l'interface (poste I1). Chaque icône est dessinée à la taille où la
maquette l'affiche, la grille entière mise à l'échelle d'un bloc : une mise à l'échelle
icône par icône la normaliserait sur sa propre boîte englobante, l'épaisseur du trait
varierait alors de 40 % d'un bouton à l'autre et l'ensemble cesserait de faire système.

**Les interactions passent par une couche de saisie transparente** posée sur la page,
et non par le test de survol des blocs. Les contrôles internes (image, zone de texte)
n'interceptent donc jamais le pointeur, et les blocs sont testés dans l'ordre de
superposition exact.

## Où sont rangés les réglages

Les **modèles** sont dans `Templates/`, à côté du binaire — ce sont des `.mypub`
ordinaires, qu'on peut ajouter ou corriger sans recompiler.

Le document contient ce qu'on compose ; les **coordonnées professionnelles** n'en font
pas partie. Elles vivent dans `~/.config/MyPublisher/informations.json`, parce qu'elles
suivent la personne et non le fichier : un document envoyé à un collègue affiche les
coordonnées de ce collègue, ce qui est précisément l'intérêt d'un modèle. Rien n'y est
écrit tant qu'on n'a pas validé le dialogue.

## Format de fichier

`.mypub` est du JSON indenté, versionné par `FormatVersion` (aujourd'hui 5). Un
fichier d'une version antérieure est migré à l'ouverture — la version 1 nommait
`Background` le fond des blocs de texte, il s'appelle `Fill` partout depuis ; la
version 2 n'avait qu'une marge intérieure, il y en a quatre ; la version 3 écrivait
le fond en couleur, c'est désormais un objet ; la version 4 ne connaissait pas les
jeux, et ses couleurs et ses polices restent libres — et un fichier d'une version plus
récente est refusé plutôt que lu de travers.

Le document porte `ColorScheme` (un nom, `Main` et quatre accents) et `FontScheme`
(un nom, `Heading` et `Body`). Là où une couleur est attendue, `scheme:main` ou
`scheme:accent1` à `scheme:accent4` y font référence, avec une opacité propre
facultative (`scheme:accent1/80`) ; là où une police est attendue, `scheme:heading` ou
`scheme:body`. Toute autre valeur est libre.

Un remplissage porte son propre discriminant `$kind` : `solid` et sa couleur,
`gradient` et ses deux couleurs avec son angle, `image` et son chemin, `tint` et sa
couleur de base avec son taux. C'est vrai du fond d'un bloc, de celui d'une case de
tableau et du fond de page.

Le document porte le format de page, les marges, le fond de page, la grille de
composition et les repères libres — ces derniers sont communs à toutes les pages,
comme dans Publisher.
Un bloc de texte porte deux collections d'écarts à ce qu'il dit pour tout le monde :
`Spans`, des portions de texte repérées **par position de caractère**, et `Paragraphs`,
des retraits repérés **par indice de paragraphe**. Le repérage diffère à dessein — un
retrait appartient au paragraphe entier, et un indice de paragraphe ne bouge pas quand on
tape un mot. Vides, les deux collections ne changent rien.

Le souligné, `Underline`, s'écrit sur le bloc et sur chaque portion : `false` pour aucun,
`true` pour le trait simple, et pour toute autre sorte un objet qui porte ses quatre
réglages — `{ "Line": "Dotted", "Double": false, "Thick": true, "WordsOnly": false }`, le
trait étant l'un de `Solid`, `Dotted`, `Dash`, `LongDash`, `DotDash`, `DotDotDash`,
`Wave`. Sur une portion, `null` laisse celui du bloc.

Tout bloc porte `Visible`, `true` par défaut ; à `false`, il n'est ni dessiné ni
exporté, ni rien de ce qu'il porte s'il est un groupe. Une case de tableau ne le porte
pas : c'est le tableau qui se masque.

Un trait est une forme, `"Shape": "Line"`, couchée et sans hauteur : sa largeur est sa
longueur, sa rotation son angle, et son milieu le centre du bloc. `StartArrow` et
`EndArrow` portent ses pointes de flèche ; elles valent `false` sur toute autre forme.

Chaque bloc porte un discriminant `$kind` (`text`, `image`, `shape`, `group`, `table`)
et, pour
un texte, un `Field` éventuel — de même pour un bloc image, dont le champ ne peut valoir
que le logo. Renseigné, il fait calculer le contenu à l'affichage plutôt que de lire le
texte saisi ou le chemin, lesquels restent écrits dans le fichier et se retrouvent
intacts si l'on retire le champ.

Un groupe contient ses blocs en coordonnées relatives à son coin supérieur gauche,
écrits après sa géométrie — le rendu et l'export composent les deux repères. Un bloc
qui dépasse de son groupe y est coupé, à l'écran comme au PDF ; la coupe laisse passer
le demi-filet des blocs posés sur le bord. Un tableau
suit la même règle : ses colonnes et ses lignes sont écrites après sa taille, et ses
cases sont repérées par leur ligne et leur colonne, si bien qu'un tableau dont la
plupart des cases sont vides n'écrit que celles qui disent quelque chose.

**Une image rangée sous le dossier du document y est référencée en relatif** —
`images/plan.png` — et une image rangée ailleurs garde son chemin absolu. Déplacer un
dossier de travail avec son `.mypub` et ses images ne casse donc plus rien. Un chemin
qui remonterait par `..` est écarté : il se casserait au premier déplacement, ce qui est
justement ce qu'on évite. La résolution a lieu à l'**ouverture**, une fois pour toutes :
partout ailleurs — à l'écran, à l'export — un chemin d'image est absolu.

## Limites connues

- Pas de chaînage du texte entre blocs : un texte trop long est coupé au bloc — le bloc
  le signale et l'export l'écrit, mais il n'y a nulle part où faire couler la suite. Les
  colonnes coulent à l'intérieur d'un bloc, jamais d'un bloc au suivant. Un texte trop
  LARGE, lui, est coupé sans rien dire : le signalement ne porte que sur la hauteur,
  comme le marqueur de Publisher.
- Le recalage des styles à la frappe compare le texte d'avant et celui d'après ; il ne
  voit pas le curseur. Quand le texte inséré répète ce qui l'entoure, il peut le lire
  une place plus loin que là où on l'a tapé : le style ou le réglage de paragraphe que
  reçoit le texte inséré peut alors différer de Publisher, jamais ceux du texte qui
  était là. Deux cas se voient. Un retour à la ligne tapé en tête d'un paragraphe crée
  un paragraphe vide au réglage de celui d'au-dessus. Une ligne copiée avec son saut et
  collée en tête d'elle-même donne deux lignes, dont la seconde prend le réglage de la
  ligne qui suit. Un fragment vidé par une suppression ne revient pas non plus si l'on
  retape le texte ; l'annulation du document le rend, au caractère près.
- Les fragments de style se lisent, s'écrivent et se rendent, mais ne se saisissent pas
  encore à la souris : on ne peut pas sélectionner trois mots dans un bloc pour les
  mettre en gras. La barre contextuelle, le menu Texte et `Ctrl+G` agissent sur le bloc
  entier.
- L'alignement, les retraits et l'espacement varient d'un paragraphe à l'autre — un
  document Publisher importé les garde, et ils s'affichent et s'exportent —, mais la
  barre contextuelle et le dock ne règlent que le bloc entier.
- La coupure des lignes se décide, à l'écran comme au PDF, sur la plus large des deux
  mesures, et sur la largeur du bloc telle que le modèle la donne. Ce qui est vérifié,
  par `Scripts/comparer-mots.sh` dans la recette de Q13 : aucune ligne coupée
  autrement sur l'exemple, les deux documents ATR et `Samples/recette-coupure.mypub`
  — des largeurs au demi-point, et des blocs où l'écran mesure plus large que le PDF —,
  sur cette machine et avec ses polices. Ce n'est pas une preuve pour toute police ni
  toute largeur. Chacun dessine ensuite à sa mesure : à l'intérieur d'une ligne, un mot
  peut se poser à quelques points près de part et d'autre (2,96 pt d'écart au plus,
  mesuré sur un morceau d'ATA 21), et un mot qui tiendrait de justesse d'un seul côté
  passe à la ligne des deux. Un caractère que la police du PDF ne porte pas — « ≤ » et
  « → » dans ATA 21 — se dessine à l'écran et manque au PDF.
- « --export » mesure le texte comme l'écran, par une méthode qu'Avalonia 11.3.9 cache
  dans son assemblage de référence. Une version qui la retirerait ferait échouer
  l'export sans fenêtre, sans écrire de PDF ; l'application, elle, se replierait sur la
  résolution des polices d'Avalonia, et le dirait dans la barre d'état à l'export.
- Les tableaux ne fusionnent pas de cellules, ne s'imbriquent pas et ne se scindent pas
  entre deux pages : ce sont des bandes de cases, ce dont les documents à reprendre ont
  besoin, et rien de plus.
- Pas de pages maîtres.
- Les images sont liées, pas embarquées dans le `.mypub`. Un dossier déplacé d'un
  bloc emporte les siennes ; un document envoyé seul arrive sans elles.
- Pas de gestion des couleurs CMJN ni de fond perdu : l'export est en RVB.
- Le format `.pub` se lit mais ne s'écrit pas. La lecture demande `libmspub`
  (`apt install libmspub-tools`) : le format est fermé, et c'est le lecteur qu'emploie
  tout le monde libre. Sans droits d'installation, `Scripts/outils-pub.sh` l'extrait
  localement. Ne sont pas repris : les taquets de tabulation autres que ceux
  par défaut.
- Pas de textures ni de motifs de remplissage : écartés délibérément.

Ces limites, et une trentaine d'autres écarts fonctionnels, sont détaillées fiche par
fiche dans [`MyPublisher/Docs/Publisher2007/`](MyPublisher/Docs/Publisher2007/00-index.md),
issu du dépouillement du guide Microsoft Publisher 2007.

La trajectoire retenue pour les traiter — phases, dépendances et critères de recette —
est dans [`MyPublisher/Docs/PLAN.md`](MyPublisher/Docs/PLAN.md).
