# MyPublisher

Un logiciel de PAO (mise en page) natif Linux, dans l'esprit de Microsoft Publisher :
on pose des blocs sur une page, on les déplace au pouce, et on exporte un PDF conforme.

Écrit en C# / .NET 9 avec Avalonia. Aucune dépendance à Wine, Mono ou GTK.

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
ci-dessous.

## Contrôle de non-régression

```bash
Scripts/controle.sh                 # sort en 0 tant que rien ne bouge
Scripts/controle.sh --enregistrer   # remplace le relevé de référence, après examen
```

Trois vérifications sur `Samples/exemple-bulletin.mypub` : il se relit, il se réécrit
au caractère près, et son export PDF donne le même nombre de pages et les mêmes
avertissements que le relevé de `Scripts/reference.txt`. C'est un filet, pas une
stratégie de test : il tient la chaîne ouvrir → enregistrer → exporter, celle dont
tout le reste dépend.

## Interface

L'espace de travail se lit de gauche à droite : liste des **pages** et des **blocs**,
puis la **planche**, puis les **propriétés** du bloc sélectionné, et enfin la **barre
d'outils** verticale sur le bord droit.

Les cinq premiers boutons de cette barre **arment un outil** : on trace ensuite le cadre
directement sur la page, à la taille voulue. Un simple clic sans glisser pose le bloc à
sa taille par défaut. L'outil se désarme tout seul après usage, pour qu'on puisse
ajuster immédiatement ce qu'on vient de poser ; un bloc de texte passe même directement
en saisie. Les boutons suivants sont des actions immédiates sur le bloc sélectionné :
ordre de superposition, puis centrage et ajustement aux marges.

L'insertion d'image n'est pas un outil de tracé — elle demande d'abord un fichier — et
la barre du haut regroupe les commandes du document : fichier, annulation, zoom.

## Ce que l'application sait faire

- Documents multi-pages, formats A3 à Legal, portrait ou paysage, marges réglables.
- Blocs de texte (police système, corps, gras, italique, alignement dont justifié,
  interligne, marge intérieure, couleur), images (trois modes de cadrage) et
  formes (rectangle, rectangle arrondi, ellipse) avec remplissage et contour.
- Rotation libre de n'importe quel bloc, y compris pour la sélection et le
  redimensionnement.
- Sélection d'un bloc ou de plusieurs (`Ctrl` + clic), déplacement d'ensemble,
  redimensionnement par huit poignées, aimantation sur les marges, les bords et
  l'axe médian de la page.
- Édition du texte directement sur la page (double-clic).
- Ordre de superposition ; alignement (gauche, centre, droite, haut, milieu, bas) et
  répartition d'une sélection ; ajustement aux marges. L'alignement se fait par
  rapport à la zone de composition, ou — case *Disposition ▸ Par rapport aux marges*
  décochée — par rapport à la page pour un bloc seul et aux blocs entre eux dès
  qu'ils sont plusieurs.
- Annulation / rétablissement sur 60 pas, couvrant aussi bien les gestes à la
  souris que les champs du panneau de propriétés.
- Enregistrement au format `.mypub` (JSON lisible) et export PDF.

## Raccourcis

| Raccourci | Action |
|---|---|
| `Ctrl+N` / `Ctrl+O` / `Ctrl+S` | Nouveau / Ouvrir / Enregistrer |
| `Ctrl+E` | Exporter en PDF |
| `Ctrl+Z` / `Ctrl+Y` | Annuler / Rétablir |
| `Ctrl+D` | Dupliquer la sélection |
| `Ctrl+A` | Sélectionner tous les blocs de la page |
| `Suppr` | Supprimer la sélection |
| Flèches | Déplacer de 3 pt (`Maj` : 0,5 pt) |
| `Ctrl` + molette, `Ctrl` `+`/`-`/`0` | Zoom |
| `V` / `T` / `H` / `R` / `E` | Outil sélection / texte / titre / rectangle / ellipse |
| `Échap` | Désarme l'outil, sinon désélectionne |
| Double-clic | Éditer le texte d'un bloc |
| `Ctrl` ou `Maj` + clic | Ajoute un bloc à la sélection, ou l'en retire |
| `Alt` pendant le déplacement | Désactive l'aimantation |
| `Maj` sur une poignée d'angle | Conserve les proportions |

## Choix techniques

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

**Les icônes de la barre d'outils sont tracées à l'échelle 1:1** sur une grille commune
de 24 unités, avec `Stretch="None"`. Une mise à l'échelle uniforme normaliserait chaque
icône sur sa propre boîte englobante : l'épaisseur du trait varierait alors de 40 % d'un
bouton à l'autre et l'ensemble cesserait de faire système.

**Les interactions passent par une couche de saisie transparente** posée sur la page,
et non par le test de survol des blocs. Les contrôles internes (image, zone de texte)
n'interceptent donc jamais le pointeur, et les blocs sont testés dans l'ordre de
superposition exact.

## Format de fichier

`.mypub` est du JSON indenté, versionné par `FormatVersion`. Chaque bloc porte un
discriminant `$kind` (`text`, `image`, `shape`). Les images sont référencées par
chemin absolu : déplacer un document sans ses images casse les liens.

## Limites connues

- Pas de chaînage du texte entre blocs : un texte trop long est coupé au bloc.
- Pas de pages maîtres ni de repères personnalisés.
- Les images sont liées, pas embarquées dans le `.mypub`.
- Pas de gestion des couleurs CMJN ni de fond perdu : l'export est en RVB.
- Le format `.pub` de Microsoft Publisher n'est ni lu ni écrit (format fermé).

Ces limites, et une trentaine d'autres écarts fonctionnels, sont détaillées fiche par
fiche dans [`MyPublisher/Docs/Publisher2007/`](MyPublisher/Docs/Publisher2007/00-index.md),
issu du dépouillement du guide Microsoft Publisher 2007.

La trajectoire retenue pour les traiter — phases, dépendances et critères de recette —
est dans [`MyPublisher/Docs/PLAN.md`](MyPublisher/Docs/PLAN.md).
