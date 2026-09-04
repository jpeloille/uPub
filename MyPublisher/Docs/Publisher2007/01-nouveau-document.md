# 01 — Nouveau document : formats, jeu de couleurs, jeu de polices

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 4–7.
**Verdict** : `partiel`

## Ce que fait Publisher 2007

Au démarrage, Publisher n'ouvre pas un document vierge : il ouvre un **catalogue**.
La colonne de gauche liste les types de publication (Blank Page Sizes, Advertisements,
Award Certificates, Banners, Brochures, Business Cards, Business Forms, Calendars,
Catalogs, E-mail, Envelopes, Flyers, Gift Certificates, Greeting Cards, Import Word
Documents, Invitation Cards, Labels, Letterhead, Menus, Newsletters, Paper Folding
Projects, Postcards, Programs, Quick Publications, Resumes, Signs, Web Sites, With
Compliments Cards). On choisit un format, on personnalise couleurs et polices à droite,
puis on clique **Create**.

## Détail de l'IHM

**Formats vierges proposés** (dimensions affichées en pouces) : A3 16,535 × 11,693 —
A4 11,693 × 8,268 — A5 8,268 × 5,827 — B4 13,898 × 9,843 — B5 9,843 × 6,929 —
Executive 10,5 × 7,25 — Legal 14 × 8,5 — Letter 11 × 8,5 — Tabloid 17 × 11, chacun en
portrait **et** en paysage comme deux entrées distinctes, plus une vignette
**Create custom page size…**

**Panneau droit** :
- *Page Size Information* : rappel du format retenu.
- *Customize* → **Color scheme** : liste nommée et alphabétique (Alpine, Apex, Aqua,
  Aspect, Berry, Black & Gray, Black & White, Bluebird, Brown, Burgundy, Cavern,
  Cherry, Citrus, Civic…), chaque entrée montrant ses 5 pastilles.
- *Customize* → **Font scheme** : une paire titre/corps nommée, affichée dans ses
  propres polices (Apex : Lucida Sans / Book Antiqua — Archival : Georgia Bold /
  Georgia — Aspect : Verdana / Verdana — Basis : Arial Bold / Arial — Binary :
  Verdana / Georgia — Breve : Bodoni MT Black / Franklin Gothic Book…).

Les deux jeux restent modifiables après création : ce sont des propriétés du document,
pas un choix figé à la création.

## État dans MyPublisher

- Cinq formats seulement : A3, A4, A5, Letter, Legal (`Models/Units.cs:26-30`), déclinés
  en portrait/paysage par une case à cocher (`MainWindowViewModel.cs:75` et `:134`).
- Pas de taille personnalisée, alors que le modèle la supporte déjà : `PageWidth` et
  `PageHeight` sont de simples `double` (`Models/Document.cs:24-27`).
- Aucun jeu de couleurs ni de polices. Les couleurs se saisissent en `#AARRGGBB` en
  clair dans le panneau de propriétés (`Views/MainWindow.axaml:335` pour le texte,
  `:344` pour les formes).
- `NewDocument` crée directement une page vierge A4 sans rien demander
  (`MainWindowViewModel.cs:304`, `Models/Document.cs:47`).

## Écart / ce qu'il faudrait

1. **Taille personnalisée** — deux champs largeur/hauteur en mm dans le panneau
   DOCUMENT. Le modèle est déjà prêt ; c'est de la liaison XAML.
2. **Formats manquants** — B4, B5, Executive, Tabloid : une ligne chacun dans
   `PageSize.All`.
3. **Jeu de couleurs** — un enregistrement `(nom, 5 couleurs)` porté par `Document`,
   présenté comme palette dans les sélecteurs de couleur. C'est ce qui manque le plus :
   aujourd'hui rien n'aide l'utilisateur à rester cohérent d'un bloc à l'autre.
4. **Jeu de polices** — une paire (titre, corps) consommée par `BuildFrame`
   (`MainWindowViewModel.cs:364-381`), où le gabarit « Titre » code aujourd'hui en dur
   28 pt gras.

Voir aussi [18-modeles.md](18-modeles.md) : jeux de couleurs et de polices ne prennent
tout leur sens qu'avec des modèles.
