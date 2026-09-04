# 06 — Repères de règle

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 20.
**Verdict** : `manquant`

## Ce que fait Publisher 2007

Des repères libres, posés où l'on veut, qui ne s'impriment pas. Le livre les présente
comme « particulièrement utiles quand on place des images en même temps que du texte » —
c'est-à-dire pour aligner des éléments hétérogènes entre eux, là où les colonnes de la
fiche 05 ne suffisent plus.

## Détail de l'IHM

`Arrange/Ruler Guides ▸` donne quatre commandes :
- **Add Horizontal Ruler Guide**
- **Add Vertical Ruler Guide**
- **Format Ruler Guides…**
- **Clear All Ruler Guides**

Deux gestes pour les poser : par le menu, ou en glissant depuis la règle vers la page.
Le curseur change de forme au survol du repère, qu'on déplace ensuite au bouton gauche.

## État dans MyPublisher

Absent. Le README le liste dans les limites connues : « Pas de pages maîtres ni de
repères personnalisés ».

## Écart / ce qu'il faudrait

1. **Modèle** : deux listes de positions, en points, portées par `Document` (partagées
   par toutes les pages, comme dans Publisher) ou par `Page`. Sérialisées dans le
   `.mypub`, qui est du JSON versionné.
2. **Rendu** : dans le `Canvas` de `Views/PageCanvas.axaml:46-53`, à côté du rectangle
   de marge, avec le même traitement — trait fin, non exporté.
3. **Aimantation** : ajouter les positions au tableau de `SnapEdge`
   (`Views/PageCanvas.axaml.cs:434-441`). Aucun code nouveau, juste des valeurs de plus.
4. **Pose et déplacement** : le glisser depuis la règle suppose des règles, donc la
   fiche [02-anatomie-ecran.md](02-anatomie-ecran.md). Sans elles, une commande de menu
   « Ajouter un repère horizontal » posé au centre puis déplaçable suffit à couvrir
   l'usage.

À noter : c'est le seul mécanisme du livre qui permette d'aligner sur une position
arbitraire choisie par l'utilisateur. Tant qu'il manque, l'aimantation ne sert que
pour les positions que l'application a décidées d'avance.
