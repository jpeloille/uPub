# 18 — Modèles

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 51–53.
**Verdict** : `manquant`

## Ce que fait Publisher 2007

> « In most instances, you will use a template that can be modified. » (p. 3)

C'est la phrase d'ouverture du livre, et elle décrit le mode d'emploi réel du logiciel.
On ne part presque jamais d'une page blanche : on choisit une brochure, un bulletin,
une carte de visite, et on remplace le contenu. Le reste du livre — jeux de couleurs,
jeux de polices, informations professionnelles, bibliothèque de conception — n'existe
que pour servir ce mode de travail.

## Détail de l'IHM

Catalogue de démarrage, type de publication choisi à gauche (Brochures dans l'exemple) :

- Les modèles sont regroupés en **Newer Designs** / **Classic Designs** / **Blank Sizes**,
  et par usage (Informational, Price List…), chacun sous un nom : Arrows, Bounce,
  Brocade, Color Band, Marker, Modular, Perforation, PhotoScope, Simple Divider, Tabs.
- Panneau droit, à la sélection d'un modèle :
  - *Customize* : **Color scheme**, **Font scheme**, **Business information**
    (voir fiches [01](01-nouveau-document.md) et
    [19](19-informations-professionnelles.md)) ;
  - *Options* propres au type : `Page size:` (3-panel), case
    **Include customer address**, `Form:` (None).
- Bouton **Create**.

Ensuite (p. 53) : « Save your file. Highlight text and type over it to add new text.
Click graphics and delete them. Insert graphics and resize them. » Le modèle est un
document ordinaire, pas un format à part.

## État dans MyPublisher

Aucun modèle. `NewDocument` crée directement une page A4 vierge
(`ViewModels/MainWindowViewModel.cs:304-318` → `Document.CreateDefault`,
`Models/Document.cs:47`). Le dépôt contient un exemple, `Samples/exemple-bulletin.mypub`,
qu'il faut ouvrir à la main.

## Écart / ce qu'il faudrait

**C'est probablement le meilleur rapport valeur/effort de tout ce livre**, parce que
l'essentiel est déjà là : dans Publisher comme ici, un modèle n'est rien d'autre qu'un
document. Le format `.mypub` est du JSON versionné, l'application sait déjà l'ouvrir
(`LoadFrom`, `MainWindowViewModel.cs:320`), et `Samples/exemple-bulletin.mypub` prouve
que le format porte un document réaliste.

Il manque trois choses, aucune ne touchant au modèle de données :

1. **Un dossier `Templates/`** livré avec l'application, contenant des `.mypub`.
2. **Un écran de choix au démarrage et sur `Ctrl+N`** : une grille de vignettes plus
   l'entrée « document vierge ». Les vignettes peuvent être générées à la volée en
   rendant la première page.
3. **L'ouverture « en copie »** : charger le document mais laisser `FilePath` à `null`
   (`:326`), pour que `Ctrl+S` propose *Enregistrer sous* et n'écrase jamais le modèle.
   Une ligne.

Une poignée de modèles bien faits — bulletin, dépliant trois volets, carte de visite,
affiche — changerait plus l'usage du logiciel que n'importe quelle fonction de cette
liste. C'est aussi le seul moyen de rendre visibles les fonctions déjà présentes :
un utilisateur qui ouvre une page blanche ne découvre ni l'aimantation, ni les modes
de cadrage d'image, ni la justification.
