# 08 — Insertion et gestion des pages

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 22.
**Verdict** : `partiel`

## Ce que fait Publisher 2007

`Insert/Page…` (`Ctrl+Shift+N`) ouvre un dialogue qui ne se contente pas d'ajouter une
page vierge : il propose de la préparer.

## Détail de l'IHM

Dialogue **Insert Page** :
- `Number of new pages:` — on insère plusieurs pages d'un coup.
- Position : **Before current page** / **After current page**.
- *Options*, exclusives :
  - **Insert blank pages**
  - **Create one text box on each page** — la page arrive prête à recevoir du texte.
  - **Duplicate all objects on page:** `N` — recopie le contenu d'une page donnée.

Le menu Insert contient par ailleurs, visibles sur la même capture :
`Duplicate Page` (`Ctrl+Shift+U`), `Section…`, `Page Numbers…`, `Date and Time…`.

## État dans MyPublisher

Les trois opérations de base existent et sont saines :

- `AddPage` (`ViewModels/MainWindowViewModel.cs:579`) — une page vierge, insérée après
  la page courante, sélectionnée dans la foulée.
- `DuplicatePage` (`:593`) — clone tous les blocs de la page courante via `Frame.Clone()`.
- `DeletePage` (`:610`), refusée s'il ne reste qu'une page (`CanDeletePage`, `:608`).
- Renumérotation automatique des pages encore nommées « Page N » (`RenumberPages`, `:633`),
  qui préserve les noms personnalisés — détail soigné que Publisher n'a pas.

Tout cela passe par le même mécanisme d'annulation que le reste (`BeginEdit`/`CommitEdit`).

## Écart / ce qu'il faudrait

1. **Nombre de pages et position** — une boîte de dialogue à trois champs. `AddPage`
   calcule déjà son index d'insertion (`:583`), il suffit de le décaler et de boucler.
2. **Dupliquer les objets d'une page donnée** — le code existe intégralement dans
   `DuplicatePage` ; il ne manque que le paramètre « quelle page ».
3. **Numéros de page automatiques** — absent, et c'est le manque le plus gênant pour un
   document de plus de deux pages. Suppose un jeton de champ dans un `TextFrame`
   (même mécanisme que les informations professionnelles,
   voir [19-informations-professionnelles.md](19-informations-professionnelles.md)),
   résolu à l'affichage et à l'export.
4. **Sections** — numérotation repartant de 1, plages de pages. Peu prioritaire.

Point d'attention : une page ne porte aujourd'hui **aucune dimension propre**
(`Models/Document.cs:7-13`), la taille est celle du document. C'est un choix
raisonnable, et Publisher fait pareil ; à conserver.
