# 04 — Volet Office (Task Pane)

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 14.
**Verdict** : `hors périmètre`

## Ce que fait Publisher 2007

Un panneau latéral gauche, refermable par une croix, réouvrable par `View/Task Pane`
ou `Ctrl+F1`. Son contenu change selon la tâche : options de publication, arrière-plans
(p. 25), recherche de cliparts (p. 39), jeux de couleurs et de polices.

## État dans MyPublisher

Le rôle est tenu par deux panneaux (phase I, poste I2) : les pages à gauche, et à droite
un dock à quatre onglets — Propriétés, Styles, Couleurs, Document — dont l'utilisateur
choisit l'onglet ; le premier suit la sélection (`Views/MainWindow.axaml`). Les deux se
replient d'un geste, `Ctrl+F1`, ou un à un au menu Fenêtre : c'est le poste B7, livré
avec I2.

## Écart / ce qu'il faudrait

Le volet commutable de Publisher est une réponse au manque de place d'un écran de 2007.
Sur un écran actuel, deux panneaux permanents sont plus lisibles qu'un panneau qui
change de contenu sans prévenir.

Une seule chose vaut d'être reprise, et elle est mineure : **pouvoir replier les
panneaux** pour travailler sur une double page en grand. Un `Ctrl+F1` qui masque les
deux colonnes latérales, sans plus — fait le 24 octobre 2026 (B7). Le dock a des onglets,
comme la maquette : décision du propriétaire du même jour. Ce que ce relevé craignait, un
panneau qui change de contenu sans prévenir, reste écarté : c'est l'utilisateur qui
choisit l'onglet.
