# Publisher 2007 — relevé de fonctions et écarts avec MyPublisher

Dépouillement de *Microsoft Publisher 2007 Introduction — Student Guide*
(Downers Grove Public Library, Rev. F du 24/09/2013, 68 pages), rangé dans
`MyPublisher/Books/`.

Une fiche par fonction, pas une fiche par entrée du sommaire : les cinq onglets de
*Fill Effects* sont un seul dialogue, les trois exercices ne sont pas des fonctions.
Chaque fiche suit le même plan — ce que fait Publisher, le détail relevé dans les
captures d'écran, l'état du code de MyPublisher (avec les références `fichier:ligne`),
et l'écart.

Le livre est un guide pour débutants, très illustré : l'essentiel de l'information
utile est **dans les captures**, pas dans le texte. Les fiches citent donc les libellés
de dialogues relevés à l'image.

## Tableau

| # | Fonction | p. | Verdict |
|---|---|---|---|
| [01](01-nouveau-document.md) | Nouveau document : formats, jeu de couleurs, jeu de polices | 4–7 | `partiel` |
| [02](02-anatomie-ecran.md) | Anatomie de l'écran et palette d'objets | 8, 33 | `partiel` |
| [03](03-barres-outils-personnalisables.md) | Barres d'outils personnalisables | 9–13 | `hors périmètre` |
| [04](04-volet-office.md) | Volet Office | 14 | `hors périmètre` |
| [05](05-reperes-mise-en-page.md) | Repères : marges, colonnes, lignes de base | 15–19 | `manquant` |
| [06](06-reperes-regles.md) | Repères de règle | 20 | `manquant` |
| [07](07-zone-de-travail.md) | Zone de travail hors page | 21 | `manquant` |
| [08](08-insertion-pages.md) | Insertion et gestion des pages | 22 | `partiel` |
| [09](09-arriere-plan-page.md) | Arrière-plan de page et effets de remplissage | 24–32 | `manquant` |
| [10](10-creer-zone-texte.md) | Créer une zone de texte | 33 | `fait` |
| [11](11-format-zone-texte.md) | Format de bloc : remplissage et bordures | 34–35 | `partiel` |
| [12](12-ajustement-texte.md) | Ajustement du texte, alignement vertical, chaînage | 36–37 | `manquant` |
| [13](13-clip-art.md) | Bibliothèque d'images (Clip Art) | 39–40 | `hors périmètre` |
| [14](14-image-fichier.md) | Insertion d'image et barre d'outils Image | 41–43 | `partiel` |
| [15](15-ordre-superposition.md) | Ordre de superposition | 44–45 | `fait` |
| [16](16-groupement.md) | Groupement et dissociation | 46 | `manquant` |
| [17](17-wordart.md) | WordArt | 47–49 | `hors périmètre` |
| [18](18-modeles.md) | Modèles | 51–53 | `manquant` |
| [19](19-informations-professionnelles.md) | Informations professionnelles | 54–55 | `manquant` |
| [20](20-bibliotheque-conception.md) | Bibliothèque de conception | 56–60 | `manquant` |
| [21](21-pack-and-go.md) | Pack and Go : emporter les ressources | 61–67 | `manquant` |
| [22](22-fonctions-hors-sommaire.md) | Fonctions vues dans les captures, hors sommaire | diverses | `mixte` |

Sur 21 fonctions comparables : 2 faites, 5 partielles, 10 manquantes, 4 hors périmètre.

## Ce qui ressort du dépouillement

**1. Le geste de base est déjà meilleur que celui du modèle.** Tracé d'un bloc, clic
simple qui pose la taille par défaut, désarmement automatique de l'outil, huit poignées,
aimantation, panneau de calques, annulation sur 60 pas couvrant aussi les champs du
panneau : sur les fiches 10 et 15, MyPublisher fait mieux que Publisher 2007. Il n'y a
rien à rattraper de ce côté.

**2. Ce qui manque n'est pas l'édition, c'est la mise en page.** Les dix fonctions
`manquantes` se regroupent en trois familles :
- *composer* — colonnes et lignes de base (05), repères libres (06), habillage (22) ;
- *maîtriser le texte* — débordement, ajustement, chaînage (12) ;
- *démarrer et transporter* — modèles (18), archive avec ressources (21).

**3. Trois chantiers structurels commandent le reste.** La sélection multiple (16)
débloque groupement, alignement et répartition. Le moteur de composition (12) commande
le chaînage **et** l'habillage. Le remplissage discriminé (09) commande fonds de page,
dégradés et teintes. Chacun est un choix d'architecture à faire une fois, tôt.

## Ordre de traitement suggéré

> Cette liste a depuis été reprise en phases, avec dépendances et critères de recette,
> dans [`../PLAN.md`](../PLAN.md).


Classé par rapport valeur / effort, tel qu'il ressort des fiches :

1. **Poignée de rotation** (10) — toute la mécanique existe, il manque le geste.
2. **Modèles** (18) — le format `.mypub` suffit déjà ; il manque un dossier, un écran
   de choix et une ligne pour ouvrir en copie. Effet immédiat sur l'usage réel, et
   seul moyen de rendre visibles les fonctions déjà présentes.
3. **Avertissement de débordement de texte** (12) — évite de livrer un PDF amputé sans
   le savoir. Peu de code.
4. **Chemins d'images relatifs** (21) — quelques lignes dans le sérialiseur, supprime
   le cas de casse le plus fréquent.
5. **Fond et bordure sur tous les blocs** (11) — remonter quatre propriétés de
   `ShapeFrame` vers `Frame` ; `DrawShape` existe déjà côté export.
6. **Colonnes et grille** (05) — `SnapEdge` prend déjà un tableau de repères ; il
   suffit de l'allonger. Fait passer l'aimantation d'un confort à un outil de
   composition.
7. **Sélection multiple** (16) — ouvre alignement, répartition et groupement.
8. **Fond de page et dégradés** (09) — décider tôt du remplissage discriminé.
9. **Impression** (22) — le parcours s'arrête aujourd'hui au PDF.
10. **Chaînage et habillage** (12, 22) — le grand chantier, à décider consciemment.

## À part

- **Sourcing.** Les libellés de dialogues et listes d'options cités dans les fiches sont
  relevés sur les captures du PDF, relues à 100 dpi. Le livre étant un guide de
  découverte, plusieurs barres d'outils y figurent sans libellé lisible et plusieurs
  sous-menus n'y sont jamais déroulés : partout où une liste vient de l'interface
  Office 2007 plutôt que d'un relevé, la fiche le dit explicitement (fiches 02, 14,
  17 et 22).
- **Références au code.** Les `fichier:ligne` valent pour l'état du dépôt au
  30 août 2026. Le code bouge : `TextFrame.Background` est arrivé pendant la rédaction
  de ces fiches, et les fiches 09 et 11 en tiennent compte.
- `MyPublisher/Books/Publisher_2013.pdf` n'a pas été dépouillé : la demande portait sur
  l'édition 2007. Publisher 2013 utilise le ruban et a abandonné plusieurs fonctions
  décrites ici (fiches 03 et 04) — un second passage y trouverait surtout des
  différences d'interface.
