# 05 — Repères de mise en page : marges, colonnes, lignes de base

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 15–19.
**Verdict** : `manquant`

## Ce que fait Publisher 2007

`View/Boundaries and Guides` affiche les repères ; `Arrange/Layout Guides…` les règle.
Ces repères ne s'impriment pas : ils servent à aligner, et l'aimantation s'y accroche.
Le livre insiste sur un point (p. 16) : on cale le pas des lignes de base sur le corps
du texte employé.

## Détail de l'IHM

Dialogue **Layout Guides**, trois onglets, avec un aperçu vivant à droite :

- **Margin Guides** — case *Two-page master* (rubrique « Master Pages »), puis
  *Left*, *Right*, *Top*, *Bottom* en pouces.
- **Grid Guides** — *Column Guides* : `Columns:` (4 dans la capture) et `Spacing:`
  (0.2") ; *Row Guides* : `Rows:` (2) et `Spacing:` (0.2") ; case
  **Add center guide between columns and rows**.
- **Baseline Guides** — *Horizontal Baseline* : `Spacing:` (12pt) et `Offset:` (12pt).
  Noter l'unité : les lignes de base se règlent en **points**, pas en pouces, parce
  qu'elles se calent sur la typographie.

## État dans MyPublisher

- Seules les marges existent (`Models/Document.cs:31-40`), tracées en pointillés bleus
  sur la page (`Views/PageCanvas.axaml:47-52`) et jamais exportées.
- L'aimantation fonctionne déjà, sur cinq repères par axe : bord 0, marge, milieu de
  page, marge opposée, bord opposé (`Views/PageCanvas.axaml.cs:434-441`), avec une
  tolérance de 6 px écran (`:35`) et trois points d'accroche par bloc — début, milieu,
  fin (`SnapEdge`, `:451`).
- Ni colonnes, ni lignes, ni grille de ligne de base.

## Écart / ce qu'il faudrait

L'ossature est là et c'est ce qui rend l'ajout peu coûteux : **`SnapEdge` prend déjà un
`double[] guides`**. Toute la mécanique consiste à allonger ce tableau.

1. Ajouter à `Document` : `ColumnCount`, `ColumnSpacing`, `RowCount`, `RowSpacing`,
   `BaselineSpacing`, `BaselineOffset`. Sérialisation automatique, le format est
   versionné (`FormatVersion`, `Models/Document.cs:18`).
2. Les calculer en une propriété `VerticalGuides` / `HorizontalGuides`, consommée à la
   fois par le tracé (à côté du rectangle de marge) et par `ApplyMove`.
3. Régler les lignes de base en points — cohérent avec le choix d'unité du projet,
   qui modélise déjà toute la géométrie en points typographiques.

Bénéfice concret : c'est ce qui fait passer l'aimantation actuelle (5 repères, utile
pour centrer) à une vraie grille de composition (utile pour composer un bulletin en
trois colonnes). Rapport valeur/effort élevé.

Le cas *Two-page master* relève des pages maîtres, absentes du projet et signalées
comme telles dans le README ; voir [22-fonctions-hors-sommaire.md](22-fonctions-hors-sommaire.md).
