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
Scripts/comparer-rendus.sh doc.mypub 1   # l'écran contre le PDF, à deux pixels par point
Scripts/bilan.sh                    # poste par poste : clos ou non, recette ou non
```

Les recettes demandent trois outils hors .NET : `pdftoppm` (poppler-utils), `magick`
(ImageMagick 7) et `pub2raw` (libmspub-tools) :

```bash
sudo apt install poppler-utils imagemagick libmspub-tools
```

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
qu'on n'a jamais vu échouer ne prouve rien.

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

L'espace de travail se lit de gauche à droite : liste des **pages** et des **blocs**,
puis la **planche**, puis les **propriétés** du bloc sélectionné, et enfin la **barre
d'outils** verticale sur le bord droit.

La liste « Blocs (du fond vers l'avant) » montre la pile de la page, et se laisse
réordonner en y tirant un bloc — un trait bleu marque où il se posera. Le clavier fait
la même chose en plus rapide sur les deux extrêmes : `Ctrl+Maj+F` et `Ctrl+Maj+B`.

La **barre d'état** affiche en permanence, à droite, la position du curseur, puis la
position et la taille du bloc sélectionné. Les deux dernières suivent le geste pendant
qu'on déplace ou qu'on redimensionne : au moment où l'on tire une poignée, l'œil est
sur la page et non sur le panneau de propriétés.

Les six premiers boutons de cette barre **arment un outil** : on trace ensuite le cadre
directement sur la page, à la taille voulue. Un simple clic sans glisser pose le bloc à
sa taille par défaut. L'outil se désarme tout seul après usage, pour qu'on puisse
ajuster immédiatement ce qu'on vient de poser ; un bloc de texte passe même directement
en saisie. Les boutons suivants sont des actions immédiates sur le bloc sélectionné :
ordre de superposition, puis centrage et ajustement aux marges.

L'insertion d'image n'est pas un outil de tracé — elle demande d'abord un fichier — et
la barre du haut regroupe les commandes du document : fichier, annulation, zoom.

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
  formes (rectangle, ellipse).
- **Tableaux** : des bandes de cases, avec leurs colonnes et leurs lignes. Chaque case
  porte son texte, son alignement, son fond et son contour ; la grille se règle d'un
  geste pour toutes. Les colonnes et les lignes font la taille du bloc : élargir une
  colonne élargit le tableau, et tirer le tableau à la poignée mesure ses cases à la
  même aune. Une case se remplit dans le panneau de propriétés, où elle est nommée par
  son rang — « L1 · C2 ».
- **Plusieurs styles dans un même bloc** : une portion de texte porte sa propre police,
  son corps, son gras, son italique, sa couleur, et se pose en indice ou en exposant.
  L'interligne s'exprime en multiple de la hauteur **naturelle** de la police — « 1,25 »
  veut dire une fois et quart ce que la police demande, jamais une fois et quart le
  corps.
- **Retraits et espacement de paragraphe** : retrait à gauche, à droite, et retrait de
  première ligne — négatif, il fait pendre cette ligne à gauche des suivantes, ce qui
  est la façon d'aligner une liste à puces. Plus un espace avant et un espace après le
  paragraphe. Le panneau les règle pour le bloc entier ; le fichier, lui, sait déjà les
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
  demande donc plus de lui superposer une forme.
- **Le fond n'est pas forcément uni** : c'est au choix un aplat avec son opacité, un
  dégradé de deux couleurs le long d'un angle, une image, ou une teinte dérivée d'une
  couleur de base. Textures et motifs : non, délibérément.
- **Fond de page** : le même choix, posé sur la publication entière — panneau
  *DOCUMENT*. Un dégradé de page se règle en trois gestes et change tout l'air d'un
  document.
- Rotation libre de n'importe quel bloc, y compris pour la sélection et le
  redimensionnement. Elle se prend **à la souris**, par une neuvième poignée posée
  au-dessus du bloc — un disque vert au bout d'une tige — ou se saisit au degré près
  dans le panneau. `Maj` pendant le geste tourne par pas de 15°, et remet d'aplomb un
  bloc qui ne l'est pas.
- Groupes : plusieurs blocs réunis se déplacent, se tournent et se redimensionnent
  d'une pièce — le redimensionnement met les blocs du groupe à la même échelle. Un
  bloc d'un groupe ne se sélectionne pas directement : on dissocie pour le retoucher.
- Sélection d'un bloc ou de plusieurs (`Ctrl` + clic), déplacement d'ensemble,
  redimensionnement par huit poignées, rotation par une neuvième. Les poignées ne
  paraissent que sur un bloc seul : pour tourner ou redimensionner plusieurs blocs, on
  les groupe.
- Grille de composition : colonnes et lignes avec leur gouttière, grille de ligne de
  base réglée en points. L'aimantation s'y accroche comme aux marges, aux bords et à
  l'axe médian de la page.
- Règles graduées bordant la planche, et repères libres posés où l'on veut : on les
  glisse depuis une règle, on les déplace, on les retire en les ramenant hors de la
  page, et l'aimantation s'y accroche. Aucun repère ne s'imprime ; *Affichage ▸
  Repères* et *Règles* les effacent de l'écran — et *Repères* efface aussi le marqueur
  de débordement, qui ne s'imprime pas davantage.
- Édition du texte directement sur la page (double-clic).
- Ordre de superposition ; alignement (gauche, centre, droite, haut, milieu, bas) et
  répartition d'une sélection ; ajustement aux marges. L'alignement se fait par
  rapport à la zone de composition, ou — case *Disposition ▸ Par rapport aux marges*
  décochée — par rapport à la page pour un bloc seul et aux blocs entre eux dès
  qu'ils sont plusieurs.
- Annulation / rétablissement sur 60 pas, couvrant aussi bien les gestes à la
  souris que les champs du panneau de propriétés.
- Enregistrement au format `.mypub` (JSON lisible) et export PDF.
- **Un dossier de travail se déplace sans casser ses images** : les images rangées à
  côté du document — ou sous son dossier — y sont référencées relativement à lui.
  Celles qui vivent ailleurs gardent leur chemin absolu.
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
  fiche de procédure, elle remplace une flèche par une lettre.

## Raccourcis

| Raccourci | Action |
|---|---|
| `Ctrl+N` / `Ctrl+O` / `Ctrl+S` | Nouveau (catalogue de modèles) / Ouvrir / Enregistrer |
| `Ctrl+E` | Exporter en PDF |
| `Ctrl+Z` / `Ctrl+Y` (ou `Ctrl+Maj+Z`) | Annuler / Rétablir |
| `Ctrl+D` | Dupliquer la sélection |
| `Ctrl+A` | Sélectionner tous les blocs de la page |
| `Ctrl+Maj+G` / `Ctrl+Maj+H` | Grouper / Dissocier |
| `Ctrl+Maj+F` / `Ctrl+Maj+B` | Premier plan / Arrière-plan |
| `Suppr` | Supprimer la sélection |
| Flèches | Déplacer de 3 pt (`Maj` : 0,5 pt) |
| `Ctrl` + molette, `Ctrl` `+`/`-`/`0` | Zoom |
| `V` / `T` / `H` / `R` / `E` / `B` | Outil sélection / texte / titre / rectangle / ellipse / tableau |
| `Échap` | Désarme l'outil, sinon désélectionne |
| Double-clic | Éditer le texte d'un bloc |
| `Ctrl` ou `Maj` + clic | Ajoute un bloc à la sélection, ou l'en retire |
| `Alt` pendant le déplacement | Désactive l'aimantation |
| `Maj` sur une poignée d'angle | Conserve les proportions |
| `Maj` sur la poignée de rotation | Tourne par pas de 15° |

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
chaînes `#AARRGGBB`, les alignements des énumérations maison. La traduction vers les
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

**Les icônes de la barre d'outils sont tracées à l'échelle 1:1** sur une grille commune
de 24 unités, avec `Stretch="None"`. Une mise à l'échelle uniforme normaliserait chaque
icône sur sa propre boîte englobante : l'épaisseur du trait varierait alors de 40 % d'un
bouton à l'autre et l'ensemble cesserait de faire système.

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

`.mypub` est du JSON indenté, versionné par `FormatVersion` (aujourd'hui 4). Un
fichier d'une version antérieure est migré à l'ouverture — la version 1 nommait
`Background` le fond des blocs de texte, il s'appelle `Fill` partout depuis ; la
version 2 n'avait qu'une marge intérieure, il y en a quatre ; la version 3 écrivait
le fond en couleur, c'est désormais un objet — et un fichier d'une version plus
récente est refusé plutôt que lu de travers.

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

Chaque bloc porte un discriminant `$kind` (`text`, `image`, `shape`, `group`, `table`)
et, pour
un texte, un `Field` éventuel — de même pour un bloc image, dont le champ ne peut valoir
que le logo. Renseigné, il fait calculer le contenu à l'affichage plutôt que de lire le
texte saisi ou le chemin, lesquels restent écrits dans le fichier et se retrouvent
intacts si l'on retire le champ.

Un groupe contient ses blocs en coordonnées relatives à son coin supérieur gauche,
écrits après sa géométrie — le rendu et l'export composent les deux repères. Un tableau
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
- Les fragments de style se lisent, s'écrivent et se rendent, mais ne se saisissent pas
  encore à la souris : on ne peut pas sélectionner trois mots dans un bloc pour les
  mettre en gras. Le panneau agit sur le bloc entier. Il en va de même des retraits
  posés paragraphe par paragraphe.
- L'alignement — gauche, centré, droite, justifié — reste une propriété du bloc, alors
  qu'un document Publisher le fait varier d'un paragraphe à l'autre.
- Les réglages typographiques du panneau — police, corps, gras, alignement, interligne,
  marges, retraits, colonnes — s'enregistrent et s'exportent bien, mais la page ne se
  redessine pas au moment où on les change : il faut retoucher le texte, ou rouvrir le
  document, pour les voir. Seul le contenu rafraîchit l'affichage.
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
  par défaut, et la rotation des formes et des blocs de texte.
- Pas de textures ni de motifs de remplissage : écartés délibérément.

Ces limites, et une trentaine d'autres écarts fonctionnels, sont détaillées fiche par
fiche dans [`MyPublisher/Docs/Publisher2007/`](MyPublisher/Docs/Publisher2007/00-index.md),
issu du dépouillement du guide Microsoft Publisher 2007.

La trajectoire retenue pour les traiter — phases, dépendances et critères de recette —
est dans [`MyPublisher/Docs/PLAN.md`](MyPublisher/Docs/PLAN.md).
