# 19 — Informations professionnelles (Business Information Set)

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 54–55.
**Verdict** : `manquant`

## Ce que fait Publisher 2007

Un jeu de coordonnées saisi **une fois**, réinjecté automatiquement dans tous les blocs
prévus pour lui, dans tous les modèles. On clique le bloc de coordonnées d'un modèle,
un bouton contextuel apparaît, et on édite le jeu — la publication se met à jour.

## Détail de l'IHM

Dialogue **Edit Business Information Set** :
- `Individual name:`
- `Job position or title:`
- `Organization name:`
- `Address:` (multiligne)
- `Phone, fax, and e-mail:` (multiligne)
- `Tagline or motto:`
- `Logo:` — aperçu, boutons **Change…** et **Remove**
- `Business Information set name:` — **plusieurs jeux peuvent coexister** (« Downers
  Grove 1 »), ce qui permet de basculer entre plusieurs identités.

Le livre note que la publication prend ces informations **par défaut**, et qu'on y
accède aussi depuis le catalogue de démarrage.

## État dans MyPublisher

Absent, et le concept avec. Un `TextFrame` ne porte que du texte littéral
(`Models/Frame.cs:107`) ; rien ne distingue un texte saisi d'un texte réinjecté.

## Écart / ce qu'il faudrait

Cette fonction n'a d'intérêt qu'avec des modèles : seule, elle ne sert à rien. La faire
**après** [18-modeles.md](18-modeles.md), et pour la servir.

L'implémentation est plus légère qu'il n'y paraît et n'ajoute **aucun type de bloc** :

1. Un jeu clé → valeur stocké **hors document**, dans les préférences de l'application
   (les coordonnées suivent l'utilisateur, pas le fichier), avec plusieurs jeux nommés.
2. Un champ `Field` optionnel sur `TextFrame`. S'il est renseigné, le texte affiché
   vient du jeu ; sinon, c'est du texte littéral. Le bloc reste un `TextFrame`
   ordinaire, avec sa police, son corps et son alignement.
3. Le logo est une `ImageFrame` avec le même mécanisme.

Le même dispositif de champs sert ensuite aux numéros de page
(voir [08-insertion-pages.md](08-insertion-pages.md)) et à la date — ce qui en fait un
investissement à trois usages plutôt qu'un.
