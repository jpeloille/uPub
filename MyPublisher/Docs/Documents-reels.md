# Deux documents réels, et ce qu'ils exigent

Relevé de `Books/ATA 21 - Air conditioning.pub` et
`Books/Fiches limitations et procédures d'urgences.pub` — deux documents ATR composés
sous Publisher, déposés le 6 septembre 2026. Ce ne sont pas des exemples : c'est le
travail que MyPublisher doit pouvoir reprendre.

Les fiches de `Publisher2007/` disent ce que Publisher *sait faire*. Ces deux
documents-ci disent ce dont on *se sert*. Quand les deux divergent, ce sont eux qui
tranchent.

## Comment ils ont été lus

Le `.pub` est un format fermé ; ces relevés ne sont donc pas des lectures à l'œil mais
la sortie de `pub2raw` (libmspub 0.1.4), qui restitue la suite d'appels de dessin que
Publisher adresserait à un moteur de rendu. Les outils sont extraits localement dans
`/tmp/mspub/`, sans installation système :

```bash
apt-get download libmspub-tools libmspub-0.1-1 librevenge-0.0-0 libicu78 zlib1g
dpkg-deb -x <chaque .deb> /tmp/mspub/root/
LD_LIBRARY_PATH=/tmp/mspub/root/usr/lib/x86_64-linux-gnu \
  /tmp/mspub/root/usr/bin/pub2raw document.pub
```

## Ce qu'ils contiennent

| | ATA 21 | Fiches limitations |
|---|---:|---:|
| Pages | 2 | 5 |
| Format | A4 portrait | A4 portrait |
| Blocs de texte | 9 | 18 |
| **Tableaux** | 0 | **19** (19 lignes, 51 cellules) |
| Polygones / chemins | 10 / 0 | 0 / 5 |
| Paragraphes | 52 | 169 |
| **Fragments de style** | **73** | **329** |
| Images | 5 PNG, en **remplissage de forme** | — |
| Polices | Calibri | Verdana Pro Cond SemiBold, **Wingdings 3**, **Wingdings**, Calibri |
| Corps | 10, 11, 12 pt | 8, 10, 11 pt |

---

## La réponse sur l'interligne

**Aucun des deux documents ne porte `fo:line-height`.** La propriété existe bien dans
libmspub — la chaîne est dans la bibliothèque, et elle est émise quand Publisher stocke
un interligne non standard — mais elle est absente ici : les deux documents s'en tiennent
au **défaut de Publisher, qui est la hauteur naturelle de la police**.

Donc, sur la divergence relevée au poste P3 :

- **l'export a raison.** `XTextFormatter` avance à la hauteur naturelle de la police,
  soit 13,93 pt pour du DejaVu Sans 12 pt. C'est ce que fait Publisher ;
- **l'écran a tort.** Il avance de `FontSize × LineHeight`, soit exactement 12,00 pt à
  interligne 1 — plus serré que le naturel, et faux pour tout document venant de
  Publisher.

**Ce qu'il faut faire** : `LineHeight` doit être un multiple de la hauteur **naturelle**
de la police, et non du corps. À 1, l'écran ne doit rien imposer et laisser la police
décider ; au-delà, il multiplie cette hauteur-là. L'export applique ensuite le même
multiple à la même hauteur naturelle. Une seule mesure, deux rendus d'accord.

---

## Ce que le plan ne prévoit pas

Quatre manques, tous établis sur ces documents, aucun couvert par les 25 postes.

### R1 — Le texte riche, fragment par fragment

**C'est le manque décisif.** Un paragraphe d'ATA 21 porte jusqu'à 3 fragments de style,
un des Fiches jusqu'à **7** : gras au milieu d'une phrase, corps différent sur un mot,
police différente pour un symbole, indice.

`TextFrame` porte aujourd'hui **une** police, **un** corps, **un** gras pour tout le
bloc. Tant que c'est le cas, **aucun des deux documents ne peut être reproduit** — pas
approché de loin, reproduit. 402 fragments sur les deux documents ne sont pas une
finition.

Le modèle doit gagner une notion de fragment : une suite de morceaux portant chacun
police, corps, gras, italique, couleur et position verticale, là où le bloc ne porte
plus que ce qui vaut pour tout le monde. Le rendu écran suit (`Inlines`), l'export aussi,
et le format gagne une version.

### R2 — Les propriétés de paragraphe

| Propriété | ATA 21 | Fiches | Ce que c'est |
|---|---:|---:|---|
| `fo:text-indent` négatif | 36 § | — | **retrait négatif** : c'est ainsi qu'on fait une liste à puces |
| `fo:margin-left` | 39 § | 1 § | retrait de paragraphe |
| `fo:margin-bottom` | — | 29 § | espace **après** le paragraphe |
| `fo:text-align` | justifié 31, gauche 21 | gauche 152, centré 16 | déjà fait |

MyPublisher n'a aucun retrait ni aucun espacement de paragraphe. Les trente-six
paragraphes en retrait négatif d'ATA 21 sont ses listes à puces : sans retrait négatif,
elles retombent au fer à gauche et la structure du document disparaît.

### R3 — Les colonnes **dans** le bloc

`fo:column-gap` est posé sur **les neuf** blocs de texte d'ATA 21 et sur seize des Fiches.
Publisher met des colonnes à l'intérieur d'une zone de texte.

Ce n'est pas C2a. C2a pose des colonnes sur la **page**, comme repères d'aimantation ;
ici le texte coule en colonnes **à l'intérieur d'un bloc**. Deux fonctions différentes,
et c'est la seconde qui sert.

### R4 — Les tableaux sont nécessaires, mais bien plus simples qu'annoncé

*Rectifié le 6 septembre 2026, après avoir compté au lieu d'estimer.* Un premier
relevé disait « 19 tableaux, 51 cellules contre 18 blocs de texte : c'est le
document ». C'était mal lu.

Le compte exact : **19 objets tableau, dont 16 de trois colonnes sur une seule ligne**
et 3 d'une seule cellule. 51 cellules, mais **16 seulement portent du texte** — et sur
les 362 insertions de texte du document, 16 sont dans un tableau. L'essentiel du
contenu vit dans les blocs de texte ordinaires.

Ce sont donc des **bandes à trois cases**, pas des tableaux de données : une ligne,
trois colonnes, aucune cellule fusionnée, aucun tableau imbriqué. C9 reste
**nécessaire** — sans lui seize éléments des fiches n'existent pas, et l'import du
poste R5 n'a nulle part où les mettre — mais il est bien plus petit que « un type de
bloc entier comparable à celui de Word ». Un bloc `table` portant lignes, colonnes,
et une cellule = un contenu de texte, suffit.

---

## Ce qui monte, ce qui descend

**B5 — les substitutions de polices — cesse d'être une commodité.** 119 des 329 fragments
des Fiches sont en **Wingdings** ou **Wingdings 3** : ce sont les flèches et symboles des
procédures d'urgence. Une substitution silencieuse ne dégrade pas l'esthétique — elle
remplace une flèche « descendre » par une lettre au milieu d'une procédure d'urgence.
Sur ce corpus-là, un avertissement de substitution est une question de sûreté, pas de
confort. À quoi s'ajoute *Verdana Pro Cond SemiBold* : une graisse portée par le **nom de
famille**, que le booléen `Bold` du modèle ne sait pas exprimer.

**C5c — le remplissage par image — monte aussi.** Les cinq PNG d'ATA 21 ne sont pas des
blocs image : ce sont des **remplissages de forme** (`draw:fill: bitmap`). C'est la façon
dont le document réel se sert des images.

**Le seuil D descend.** Rien dans ces deux documents n'appelle un chaînage de texte, un
ajustement automatique ou un habillage. Ce sont des fiches et des tableaux, où chaque
bloc se suffit. Le chantier que le plan désigne comme le plus lourd de tout le corpus
n'est **pas** ce dont ces documents ont besoin. Il reste au périmètre, il ne commande
plus rien.

**A7 — l'alignement vertical — descend** : une seule occurrence sur les deux documents.

---

## Ordre que cela suggère

1. **L'interligne** — la divergence écran/export, tranchée ci-dessus. Elle fausse
   aujourd'hui tout document venant de Publisher.
2. **R1, le texte riche** — sans lui, rien de tout cela ne se reproduit.
3. **R2, les paragraphes** — retrait négatif et espacement.
4. **C9, les tableaux** — remonté de la dernière place, mais réduit à sa taille réelle :
   des bandes d'une ligne et trois colonnes.
5. **B5, les substitutions** — remonté, pour la raison ci-dessus.
6. **R3, les colonnes dans le bloc** · **C5c, remplissage par image**.

Puis le reste des postes, dans l'ordre du plan. Le seuil D en queue, sans urgence.
