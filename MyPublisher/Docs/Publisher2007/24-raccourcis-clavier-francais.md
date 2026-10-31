# 24 — Les raccourcis clavier de Publisher en français

**Source** : l'aide de Microsoft en français, *Raccourcis clavier dans Publisher*,
<https://support.microsoft.com/fr-fr/publisher/keyboard-shortcuts-in-publisher>, consultée le
27 octobre 2026 à 6 h 32, heure de Nouméa (le 26 octobre, 19 h 32 UTC, selon l'en-tête `date` de la
réponse). Elle « s’applique à » Publisher pour Microsoft 365, « Éditeur 2021 » (sic), Publisher
2019, Publisher 2016 et Publisher 2013. La page a été lue en entier, dans son HTML tel que le serveur le rend, et
chaque fonction et chaque touche ci-dessous en est recopiée mot pour mot, coquilles comprises
(« Ret. arr », « Pg. suiv », l'espace avant une virgule). Le livre de 2007 (*Microsoft Publisher
2007 Introduction — Student Guide*, `MyPublisher/Books/`) est relu aux pages 9, 10, 16, 22, 36 et
61, dont les captures portent des touches.
**Verdict** : `mixte` — l'aide compte 102 lignes hors du ruban, dont quelques-unes deux fois.
MyPublisher en lie 31, dont 15 touchées par N10 ; il en garde 2 à un autre usage et en remplace
2 ; 5 reviennent à un autre poste du plan, 27 sont réservées et 35 écartées. Le poste N18 en a lié
six de plus, le 29 octobre 2026 — Ctrl+Maj+N, Ctrl+Maj+U, Ctrl+Pg. suiv, Ctrl+Pg. préc, et F5 ou
Ctrl+B deux fois —, dont quatre étaient à venir et deux réservées. Les secondes touches
du presse-papiers, `Ctrl+Inser`, `Maj+Suppr` et `Maj+Inser`, d'abord relevées sans être liées,
le sont depuis le 28 octobre 2026, à la demande du propriétaire. Le poste N20 en a lié deux de
plus, le 31 octobre 2026, réservées jusque-là : F3, Ctrl+F ou Maj+F4, et Ctrl+H — rechercher et
remplacer. Le poste N22 en a lié une, le même jour, réservée jusque-là : Ctrl+Maj+Y, les caractères
spéciaux. Le poste R7 en a lié cinq, le même jour, réservées jusque-là : Ctrl+Maj+C et Ctrl+Maj+V
deux fois — copier et coller la mise en forme — et Ctrl+Espace, le style du texte rendu aux
caractères.

## Pourquoi une fiche de plus

La décision 4 de la phase N veut les raccourcis de Publisher 2007 en français, et la règle 12 dit
lesquels l'emportent : **la touche de Publisher en français l'emporte quand sa fonction existe
dans MyPublisher**. Une touche que le bureau capte garde son remplaçant ; une touche dont la
fonction n'existe pas reste réservée et libre, jusqu'au poste qui la sert. Cette fiche est la
table que la règle lit : chaque touche de l'aide, avec la page du livre qui la montre en anglais
et le sort que la règle lui fait. Le poste N10 lie ce qu'elle dit de lier ; la recette de A4
vérifie que les touches réservées restent libres.

L'aide vaut pour Publisher 2013 à 365. Ses touches sont celles de 2007, sauf celles du ruban —
la première section de l'aide, « Raccourcis du ruban », des parcours `Alt, JD, SE…` des onglets
contextuels —, qui sont écartées en bloc : MyPublisher a une barre de menus, comme Publisher 2007,
et non un ruban. N13, les menus de Publisher, s'occupe des lettres d'accès.

## Comment lire les tableaux

- *Fonction* et *Touche* sont celles de l'aide, mot pour mot ; les sections sont les siennes.
- *Livre de 2007* : la page et l'intitulé anglais de la capture qui montre la touche, ou **HR**,
  hors relevé, quand le livre ne la montre pas. La p. 10 est la liste *Add or Remove Buttons ▸
  Formatting*, la p. 9 le menu *View*, la p. 16 le menu *Arrange*, la p. 22 le menu *Insert*, la
  p. 61 le menu *File*.
- *Sort*, selon la règle 12 :
  - **lié** — MyPublisher lie la touche ; le poste qui l'a fait est entre parenthèses ;
  - **gardé** — MyPublisher garde la touche à un autre usage, que la règle 12 nomme ;
  - **remplacé** — le bureau prend la touche ; la fonction garde son remplaçant ;
  - **à venir** — la touche revient à un autre poste du plan, qui la liera avec sa fonction ;
  - **réservé** — la fonction n'existe pas : la touche reste libre jusqu'au poste qui la sert,
    ou sans poste ;
  - **relevé, non lié** — la fonction existe, la touche n'est liée à rien, et aucun poste ne la
    prend : à décider. Aucune ligne ne l'est plus depuis le 28 octobre 2026 ;
  - **écarté** — hors du périmètre : le ruban, l'aperçu avant impression, la messagerie, les
    macros, les langues de l'Asie de l'Est.

## Le clavier AZERTY : ce que la touche livre

`KeyGesture` compare la *touche* et les modificateurs exacts, jamais le caractère. Sur un AZERTY,
« < » et « > » partagent une touche, « ] » et « [ » demandent AltGr, et Maj + « = » donne « + ».
Le poste N10 a donc mis une **sonde** dans l'application : la variable `MYPUBLISHER_SONDE_CLAVIER`
nomme un fichier où chaque frappe écrit sa touche, sa place, le caractère qu'elle produit et ses
modificateurs (`Views/SondeDuClavier.cs`). Le propriétaire l'a jouée le 27 octobre 2026 sur sa
machine — GNOME sous Wayland, l'application en X11 par XWayland, disposition `fr` —, la plupart
des frappes dans la saisie d'un bloc. Ce qu'elle a relevé :

| Frappe | `Key` | `PhysicalKey` | Caractère | `KeyModifiers` | Prise avant N10 |
|---|---|---|---|---|---|
| « < » | `OemComma` | `IntlBackslash` | « < » | `None` | non |
| Maj + « < » | `OemPeriod` | `IntlBackslash` | « > » | `Shift` | non |
| `Ctrl` + « < » | `OemComma` | `IntlBackslash` | « < » | `Control` | non (la saisie écrit « < ») |
| `Ctrl+Maj` + « < » | `OemPeriod` | `IntlBackslash` | « > » | `Control, Shift` | non (la saisie écrit « > ») |
| AltGr | `RightAlt` | `AltRight` | — | `None` | non |
| AltGr + « ( » | `D5` | `Digit5` | « [ » | `None` | non |
| AltGr + « = » | `OemCloseBrackets` | `Equal` | « } » | `None` | non |
| « à » | `D0` | `Digit0` | « à » | `None` | non |
| AltGr + « à » | `D0` | `Digit0` | « @ » | `None` | non |
| `Ctrl` + « = » | `OemPlus` | `Equal` | « = » | `Control` | oui (zoom avant) |
| `Ctrl+Maj` + « = » | `OemPlus` | `Equal` | « + » | `Control, Shift` | non |
| `Ctrl` + « ) » | `OemMinus` | `Minus` | « ) » | `Control` | oui (zoom arrière) |
| `Ctrl+Maj+P` | `P` | `P` | « P » | `Control, Shift` | non |
| `Ctrl` + Retour arrière | `Back` | `Backspace` | `\x08` | `Control` | non |
| `F4` | `F4` | `F4` | — | `None` | non, dans la saisie comme sur la page |

Trois choses s'en lisent. **La touche nommée suit le caractère**, aux places d'un QWERTY
américain — « < » livre `OemComma`, « > » `OemPeriod`, « } » `OemCloseBrackets` —, **sauf sur la
rangée des chiffres**, où elle suit la place à tous les niveaux : « à » et « @ » livrent `D0`,
« [ » livre `D5` ; un caractère qui n'a pas de place sur un QWERTY, « ) », livre la touche de sa
place, `OemMinus`. **Le caractère se lit sous `Ctrl`** comme sans lui. **AltGr ne se lit pas
dans les modificateurs** : il arrive en frappe à part, et la touche qui suit vient sans
modificateur ; sous `Ctrl`, elle viendrait avec `Control` seul. Une frappe que rien ne prend
écrit son caractère dans la saisie, `Ctrl` compris ; une frappe qu'une liaison prend n'y écrit
rien (`Ctrl` + « = », `Ctrl` + « ) »).

D'où la règle du poste. **Les raccourcis qui sont des caractères** — `Ctrl+]`, `Ctrl+[`,
`Ctrl+Maj+>`, `Ctrl+Maj+<` — **passent par une table unique, `RaccourcisParCaractere`**, lue
sur le caractère produit, `Maj` et AltGr comptés pour rien : sur l'AZERTY, la taille précédente
se tape `Ctrl` + « < », la suivante `Ctrl+Maj` + « < », le point de plus `Ctrl+AltGr` + « ) » et
le point de moins `Ctrl+AltGr` + « ( ». Les lettres et les touches nommées restent des
`KeyBinding`. `Ctrl+0`, la taille réelle, se tape `Ctrl` + « à », **sans** `Maj` : la touche livre
`D0` à tous les niveaux.

La sonde n'a pas vu `Ctrl+AltGr` + « ) », ni `Ctrl+AltGr` + « ( », ni `Ctrl` + « à » sous
`Control`, ni « ] » du tout — le propriétaire a frappé AltGr + « = », qui donne « } ». Ce que les
tests en rejouent est déduit des règles ci-dessus, et le dit (`Rendu.ToucheAzerty`) : « ] » en
`OemCloseBrackets`, comme « } » ; « [ » et « à » sous `Ctrl` en `D5` et `D0`, avec `Control`.
Le propriétaire a vérifié à l'écran, le 28 octobre 2026, que `Ctrl+AltGr` + « ) » augmente le
corps d'un point et ne zoome pas : la frappe n'arrive donc pas en `OemMinus`, que le zoom arrière
aurait pris, et la déduction « ] » en `OemCloseBrackets` tient.

## La table de l'aide

Les sections de l'aide, dans son ordre, sans celle du ruban.
### Créer, ouvrir, fermer ou enregistrer une composition

#### Créer, ouvrir, fermer une composition

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Ouvrir une nouvelle instance de Publisher | Ctrl+N | p. 61, *New* `Ctrl+N` | **lié** avant N10 : Nouveau, qui propose le catalogue des modèles dans la même fenêtre |
| Afficher la boîte de dialogue Ouvrir une composition | Ctrl+O | p. 61, *Open* `Ctrl+O` | **lié** avant N10 |
| Fermer la composition actuelle | Ctrl+F4 ou Ctrl+W | p. 61, *Close* `Ctrl+F4` | **à venir** : N11, dans la même salve |
| Afficher la boîte de dialogue Enregistrer sous | Ctrl+S | p. 61, *Save* `Ctrl+S` | **lié** avant N10 : Enregistrer, qui ne demande le fichier qu'au premier enregistrement — le livre dit *Save*, l'aide traduit « Enregistrer sous » |

### Modifier ou mettre en forme du texte ou des objets

#### Modifier ou mettre en forme du texte

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Afficher le volet Office Rechercher et remplacer, dans lequel l’option Rechercher est sélectionnée | F3, Ctrl+F ou Maj+F4 | HR | **lié** (N20) : `Ctrl+F` ouvre la barre de recherche, le focus au texte cherché ; `F3` et `Maj+F4` cherchent le suivant, et ouvrent la barre s'il n'y a rien à chercher |
| Afficher le volet Office Rechercher et remplacer , avec l’option Remplacer sélectionnée | Ctrl+H | HR | **lié** (N20) : la barre de recherche, le focus au texte de remplacement |
| Vérifier l’orthographe | F7 | HR | **réservé** : N25 |
| Afficher le volet Office Dictionnaire des synonymes | Maj+F7 | HR | **réservé**, aucun poste : MyPublisher n'a pas de dictionnaire des synonymes |
| Afficher le volet Office Rechercher | Maintenez la touche Alt enfoncée et cliquez sur un mot | HR | **écarté** : un clic, vers un volet de recherche documentaire que rien ne prévoit |
| Sélectionner tout le texte. Si le curseur se trouve dans une zone de texte, cela sélectionne tout le texte de l’article actuel. Si le curseur ne se trouve dans aucune zone de texte, cela sélectionne tous les objets d’une page. | Ctrl+A | HR | **lié** (N6) : les blocs de la page ; dans la saisie d'un bloc ou dans un champ, tout son texte |
| Mettre le texte en gras | Ctrl+G | p. 10, *Bold* `Ctrl+B` | **lié** avant N10 : le gras du bloc ; depuis R7, en saisie, la sélection du texte ou le mot du curseur, et hors saisie tous les caractères du bloc |
| Mettre le texte en italique | Ctrl+I | p. 10, *Italic* `Ctrl+I` | **lié** avant N10 |
| Souligner le texte | Ctrl+U | p. 10, *Underline* `Ctrl+U` | **lié** (I3) |
| Transformer le texte en petites majuscules ou celles-ci en minuscules ou majuscules | Ctrl+Maj+K | HR | **réservé** : N24 |
| Ouvrir la boîte de dialogue Police | Ctrl+Maj+F | HR | **gardé** : Premier plan (A4, règle 12), `Alt+F6` étant pris par le bureau ; la boîte Police n'existe pas, le dock en tient lieu |
| Copier la mise en forme | Ctrl+Maj+C | HR | **lié** (R7) : la mise en forme des caractères visés, au menu Texte aussi |
| Coller la mise en forme | Ctrl+Maj+V | HR | **lié** (R7) : posée sur les caractères visés, en un pas |
| Activer ou désactiver l’option Caractères spéciaux | Ctrl+Maj+Y | p. 9, *Special Characters* `Ctrl+Shift+Y` | **lié** (N22) : *Affichage ▸ Caractères spéciaux*, une bascule de la session, même en saisie |
| Appliquer à la mise en forme des caractères le style de texte actuel | Ctrl+Espace | HR | **lié** (R7) : *Texte ▸ Effacer la mise en forme* — les caractères visés reprennent le style du bloc, par `MiseEnForme.Retirer` (N2) ; la langue reste |
| Appliquer ou supprimer la mise en indice | Ctrl+= | HR | **gardé** au zoom avant jusqu'à N24, qui le prend (règle 12). Sur l'AZERTY du propriétaire, `Ctrl` + « = » arrive en `OemPlus` sous `Control`, et le zoom le prend (sonde) |
| Appliquer ou supprimer la mise en exposant | Ctrl+Maj+= | HR | **réservé** : N24. Sur l'AZERTY, `OemPlus` sous `Control, Shift`, symbole « + », que rien ne prend (sonde) |
| Augmenter l’espace entre les lettres d’un mot (crénage) | Ctrl+Maj+] | HR | **réservé**, aucun poste : MyPublisher n'a pas de crénage. La table du corps lit le caractère : sur un QWERTY, `Ctrl+Maj+]` produit « } », qu'elle ignore ; sur l'AZERTY, `Maj` + AltGr + « ) » n'a pas été relevé |
| Réduire l’espace entre les lettres d’un mot (crénage) | Ctrl+Maj+[ | HR | **réservé**, aucun poste : MyPublisher n'a pas de crénage |
| Augmenter la taille de police de 1.0 point | Ctrl+] | HR | **lié** (N10) : le corps d'un point de plus, sur le bloc de texte ou le tableau ; lu sur le caractère |
| Réduire la taille de police de 1.0 point | Ctrl+[ | HR | **lié** (N10) : le corps d'un point de moins ; lu sur le caractère |
| Augmenter à la taille suivante dans la zone Taille de police | CTRL+MAJ+> | p. 10, *Increase Font Size* `Ctrl+>` | **lié** (N10) : la taille suivante de la liste standard, 8 à 72 pt ; lu sur le caractère |
| Diminuer à la taille suivante dans la zone Taille de police | CTRL+MAJ+< | p. 10, *Decrease Font Size* `Ctrl+<` | **lié** (N10) : la taille précédente ; lu sur le caractère |
| Centrer un paragraphe | CTRL+E | p. 10, *Center* `Ctrl+E` | **lié** (N10) : sur le bloc, sur le paragraphe avec R6. L'export, qui l'avait, passe à `Ctrl+Maj+P` |
| Aligner un paragraphe à gauche | Ctrl+L | p. 10, *Align Left* `Ctrl+L` | **lié** (N10), comme `Ctrl+E` |
| Aligner un paragraphe à droite | Ctrl+R | p. 10, *Align Right* `Ctrl+R` | **lié** (N10), comme `Ctrl+E` |
| Aligner un paragraphe sur les deux côtés (justifié) | Ctrl+J | p. 10, *Justify* `Ctrl+J` | **lié** (N10), comme `Ctrl+E` |
| Distribuer un paragraphe de manière égale et horizontale | Ctrl+Maj+D | HR | **réservé**, aucun poste : pas d'alignement distribué |
| Appliquer l’alignement Journal à un paragraphe (langues de l’Asie de l’Est uniquement) | Ctrl+Maj+J | HR | **écarté** : langues de l'Asie de l'Est |
| Afficher la boîte de dialogue Coupure de mots | Ctrl+Maj+H | HR | **réservé**, aucun poste : la coupure de mots. N10 le libère : il dissociait |
| Insérer l’heure actuelle | Alt+Maj+T | HR | **réservé** : N18, qui pose les champs au menu Insertion |
| Insérer la date actuelle | Alt+Maj+D | HR | **réservé** : N18 |
| Insérer le numéro de la page actuelle | Alt+Maj+P | HR | **réservé** : N18 |
| Empêcher le mot d’être coupé par un tiret si la coupure se produit à la fin d’une ligne | Ctrl+Maj+0 (zéro) | HR | **réservé**, aucun poste : pas de coupure de mots. Sur l'AZERTY, la touche « à/0 » livre `D0` à tous les niveaux : `Ctrl+Maj` + « à » arrive en `Ctrl+Shift+D0` (déduit de la sonde) |

#### Copier des mises en forme de texte

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Copier la mise en forme du texte sélectionné | Ctrl+Maj+C | HR | **lié** (R7), comme plus haut |
| Appliquer la mise en forme copiée au texte | Ctrl+Maj+V | HR | **lié** (R7), comme plus haut |

#### Copier, couper, coller ou supprimer du texte ou des objets

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Copier le texte ou l’objet sélectionné | Ctrl+C ou Ctrl+Inser | HR | `Ctrl+C` **lié** (N8) ; `Ctrl+Inser` **lié** (N10, 28 octobre 2026) : hors saisie, les blocs ; en saisie et dans un champ, le texte, par le champ lui-même |
| Couper le texte ou l’objet sélectionné | Ctrl+X ou Maj+Suppr | HR | `Ctrl+X` **lié** (N8) ; `Maj+Suppr` **lié** (N10, 28 octobre 2026), comme `Ctrl+Inser` — hors saisie, il coupe, et ne supprime plus |
| Coller du texte ou un objet | Ctrl+V ou Maj+Inser | HR | `Ctrl+V` **lié** (N8) ; `Maj+Inser` **lié** (N10, 28 octobre 2026), comme `Ctrl+Inser` |
| Supprimer l’objet sélectionné | Supprimer ou Ctrl+Maj+X | HR | `Suppr` **lié** avant N10, comme Retour arrière ; `Ctrl+Maj+X` **lié** (N10), même en saisie |

#### Annuler ou rétablir une opération

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Annuler votre dernière action | Ctrl+Z ou Alt+Ret. arr | HR | `Ctrl+Z` **lié** ; `Alt+Retour arrière` **lié** (N10) |
| Répéter votre dernière action | Ctrl+Y ou F4 | HR | `Ctrl+Y` **lié** ; `F4` **lié** (N10) — Rétablir ; le « Répéter » d'Office, qui rejoue le dernier geste quand il n'y a rien à rétablir, n'est pas repris |

#### Repositionner un objet

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Déplacer un objet sélectionné vers le haut, le bas, la gauche ou la droite | Touches de direction | HR | **lié** avant N10 : 3 pt, `Maj` 0,5 pt |
| Si le texte de l’objet sélectionné contient un curseur, déplacer l’objet vers le haut, le bas, la gauche ou la droite | Alt+touches de direction | HR | **lié** (N6) |

#### Définir la superposition des objets

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Placer un objet au premier plan | Alt+F6 | HR (fiche 15) | **remplacé** : `Ctrl+Maj+F` (A4), le bureau prenant `Alt+F6` |
| Placer un objet à l’arrière-plan | Alt+Maj+F6 | HR (fiche 15) | **remplacé** : `Ctrl+Maj+B` (A4) |

#### Aligner les objets

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Activer ou désactiver l’option Aligner sur les repères | F10, Maj+R, Maj+S, Maj+M | HR | **écarté** : un parcours de la barre de menus de 2007 (`F10` la prend), vers une option que MyPublisher n'a pas — l'aimantation se suspend par `Alt` pendant le glissé (fiche 22) |

#### Sélectionner ou grouper des objets

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Sélectionner tous les objets de la page (si votre curseur se trouve dans une zone de texte, cela sélectionne tout le texte d’un article) | Ctrl+A | HR | **lié** (N6) : les blocs de la page ; dans la saisie d'un bloc ou dans un champ, tout son texte |
| Grouper les objets sélectionnés ou dissocier les objets groupés | Ctrl+Maj+G | p. 16, *Group* et *Ungroup* `Ctrl+Shift+G` ; p. 44, 46 | **lié** (N10) : groupe les blocs pris, ou dissocie le groupe pris |
| Effacer la sélection du texte sélectionné | Échap | HR | **lié** avant N10 : Échap referme la saisie ; la sélection du texte attend N15 |
| Effacer la sélection d’un objet sélectionné | Échap | HR | **lié** avant N10 : Échap désarme l'outil, sinon désélectionne |
| Sélectionner l’objet dans le groupe (si cet objet contient le texte sélectionné) | Échap | HR | **réservé** : N26 |

### Utiliser les pages

#### Sélectionner ou insérer des pages

> Si votre composition est en affichage Page double, ces commandes s’appliquent à la double page sélectionnée. Si ce n’est pas le cas, elles s’appliquent seulement à la page sélectionnée.

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Afficher la boîte de dialogue Atteindre la page | F5 ou Ctrl+B | HR | **lié** (N18) : le focus au numéro de page de la barre d'état, devenu un champ, plutôt qu'un dialogue |
| Insérer une page simple ou une page double. Si vous créez un bulletin d’informations, cela ouvre la boîte de dialogue Insérer des pages de type Composition | Ctrl+Maj+N | p. 22, *Page…* `Ctrl+Shift+N` | **lié** (N18) : le dialogue Insérer des pages de A8 |
| Insérer une page en double après la page sélectionnée | Ctrl+Maj+U | p. 22, *Duplicate Page* `Ctrl+Shift+U` | **lié** (N18) : Page ▸ Dupliquer la page — ibus prend la touche pour la saisie Unicode, voir « Ce que le bureau prend » |

#### Passer d’une page à l’autre

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Afficher la boîte de dialogue Atteindre la page. | F5 ou Ctrl+B | HR | **lié** (N18), comme plus haut |
| Atteindre la page suivante | Ctrl+Pg. suiv | HR | **lié** (N18) |
| Atteindre la page précédente | Ctrl+Pg. préc | HR | **lié** (N18) |
| Passer de la page active à la page maître, et vice versa | Ctrl+M | p. 9, *Master Page* `Ctrl+M` | **réservé**, aucun poste : les pages maîtres ne sont pas planifiées |

#### Utiliser la page maître

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Passer de la page active à la page maître, et vice versa. | Ctrl+M | p. 9, *Master Page* `Ctrl+M` | **réservé**, aucun poste : les pages maîtres ne sont pas planifiées |

#### Afficher ou masquer les bords ou les repères

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Activer ou désactiver l’option Limites. | Ctrl+Maj+O | p. 9, *Boundaries and Guides* `Ctrl+Shift+O` | **lié** (N4) |
| Activer ou désactiver l’option Repères de ligne de base (non disponible en mode Web) | Ctrl+F7 | p. 9, *Baseline Guides* `Ctrl+F7` | **lié** (N4) |
| Activer ou désactiver l’option Repères de ligne de base verticale (langues d’Asie de l’Est uniquement ; non disponible en mode Web) | Ctrl+Maj+F7 | HR | **écarté** : langues de l'Asie de l'Est |

#### Zoom

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Passer de l’affichage actuel à la taille réelle | F9 | p. 36, « click the F9 key » | **lié** (N4) |
| Afficher la page entière | Ctrl+Maj+L | HR | **lié** (N4) |

### Impression

#### Utiliser l’aperçu avant impression

> Ces raccourcis clavier sont disponibles lorsque vous êtes en mode Page et affectent le volet d’aperçu avant impression.

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Passer de l’affichage actuel à la taille réelle | F9 | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Faire défiler vers le haut ou vers le bas | Haut ou Bas | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Faire défiler l’affichage vers la gauche ou vers la droite | Gauche ou Droite | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Faire défiler l’affichage vers le haut selon un pas important | Pg. préc ou Ctrl+Haut | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Faire défiler l’affichage vers le bas selon un pas important | Pg. suiv ou Ctrl+Bas | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Faire défiler l’affichage vers la gauche selon un pas important | Ctrl+Gauche | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Faire défiler l’affichage vers la droite selon un pas important | Ctrl+Droite | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Atteindre directement le coin supérieur gauche de la page | Origine | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Atteindre directement le coin inférieur droit de la page | Fin | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Afficher la boîte de dialogue Atteindre la page | F5 ou Ctrl+B | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Atteindre la page précédente | Ctrl+Pg. préc | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Atteindre la page suivante | Ctrl+Pg. suiv | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Passer à la fenêtre suivante (si plusieurs compositions sont ouvertes) | Ctrl+F6 | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Quitter l’Aperçu avant impression et afficher le mode Imprimer | Ctrl+P | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |
| Quitter le mode Aperçu avant impression | Échap | HR | **écarté** : MyPublisher n'a pas d'aperçu avant impression |

#### Imprimer une composition

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Ouvrir la boîte de dialogue Impression. | Ctrl+P | p. 61, *Print…* `Ctrl+P` | **réservé** : B3 |

### Utiliser les pages web et la messagerie électronique

#### Insérer des liens hypertexte

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Afficher la boîte de dialogue Insérer un lien hypertexte (assurez-vous que votre curseur se trouve dans une zone de texte). | Ctrl+K | p. 22, *Hyperlink…* `Ctrl+K` | **réservé**, aucun poste : pas de lien hypertexte |

#### Envoyer un message électronique

> Après avoir choisi Envoyer en tant que message (partage>de fichiers>Email), vous pouvez utiliser les raccourcis clavier suivants.

> Outlook doit être ouvert avant de pouvoir envoyer des messages électroniques. Si Outlook n’est pas ouvert, le message sera placé dans votre dossier Boîte d’envoi.

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Envoyer la page ou composition actuelle | Alt+S | HR | **écarté** : pas de messagerie |
| Ouvrez le carnet d’adresses (le curseur doit se trouver dans l’en-tête du message) | Ctrl+Maj+B | HR | **écarté** ; la touche reste Arrière-plan (A4, règle 12) |
| Ouvrez le vérificateur de conception (le curseur doit se trouver dans l’en-tête du message) | Alt+K | HR | **écarté** : pas de messagerie |
| Vérifiez les noms des lignes À, Cc et Cci (le curseur doit se trouver dans l’en-tête du message) | Ctrl+K | HR | **écarté** : pas de messagerie |
| Ouvrez le carnet d’adresses avec la zone À sélectionnée (le curseur doit se trouver dans l’en-tête du message) | Alt+. (point) | HR | **écarté** : pas de messagerie |
| Ouvrez le carnet d’adresses avec la zone Cc sélectionnée (le curseur doit se trouver dans l’en-tête du message) | Alt+C | HR | **écarté** : pas de messagerie |
| Ouvrez le carnet d’adresses avec la zone Cci sélectionnée (le curseur doit se trouver dans l’en-tête du message et le champ Cci doit être visible) | Alt+B | HR | **écarté** : pas de messagerie |
| Accéder à la zone Objet | Alt+J | HR | **écarté** : pas de messagerie |
| Ouvrir la boîte de dialogue Options des messages d’Outlook | Alt+P | HR | **écarté** : pas de messagerie |
| Ouvrir la boîte de dialogue Personnalisé pour créer un indicateur d’e-mail (le curseur doit se trouver dans l’en-tête de l’e-mail) | Ctrl+Maj+G | HR | **écarté** ; la touche groupe et dissocie (N10) |
| Déplacer le curseur vers le champ suivant de l’en-tête de l’e-mail (le curseur doit se trouver dans l’en-tête de l’e-mail) | Tab | HR | **écarté** : pas de messagerie |
| Déplacer le curseur dans le champ précédent de l’en-tête de message électronique | Maj+Tab | HR | **écarté** : pas de messagerie |
| Alterner entre le point d’insertion dans l’en-tête d’un message électronique et le bouton Envoyer dans la barre d’outils Publipostage | Ctrl+Tab | HR | **écarté** : pas de messagerie |
| Ouvrir le Carnet d’adresses lorsque le curseur est placé dans l’en-tête de message électronique | Ctrl+Maj+B | HR | **écarté** ; la touche reste Arrière-plan (A4, règle 12) |

### Automatiser les tâches

#### Utiliser des macros

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Afficher la boîte de dialogue Macros. | Alt+F8 | HR | **écarté** : pas de macros |

#### Utiliser Visual Basic

| Fonction (aide, mot pour mot) | Touche (aide) | Livre de 2007 | Sort |
|---|---|---|---|
| Afficher l’éditeur Visual Basic | Alt+F11 | HR | **écarté** : pas de Visual Basic |

## Ce que MyPublisher lie hors de l'aide

| Touche | Fonction | D'où |
|---|---|---|
| `Ctrl+Maj+P` | Exporter en PDF, même en saisie | N10 : libre dans l'aide, voisin d'Imprimer ; l'export avait `Ctrl+E` |
| `Ctrl+Maj+Z` | Rétablir | les autres applications du bureau |
| `Ctrl+D` | Dupliquer la sélection | MyPublisher |
| `Ctrl+F1` | Replier les panneaux | p. 9, *Task Pane* `Ctrl+F1` (B7) |
| `Ctrl` + molette, `Ctrl` `+` / `-` / `0` | Zoom | MyPublisher ; `Ctrl+=` passera à l'indice avec N24 |
| `V`, `T`, `H`, `R`, `E`, `L`, `B`, `Z` | Les outils du rail, hors saisie | MyPublisher |
| `Espace` tenu | La main | MyPublisher (I6) |
| Retour arrière | Supprimer la sélection, hors saisie | MyPublisher, comme Suppr |

## Ce que le bureau prend

Relevé par `gsettings list-recursively` le 26 octobre 2026 (GNOME 50.1 sous Wayland ; la machine
n'a pas KDE) : ni `Ctrl+Maj+P`, ni `Alt+Retour arrière`, ni `F4`, ni `Ctrl+Maj+X` ne sont pris.
Leurs voisins le sont : `Alt+F4` ferme la fenêtre, `Ctrl+Alt+F4` change de session. Aucun raccourci
personnalisé (`custom-keybindings` vide) ; ibus prend `Ctrl+Maj+U` (saisie Unicode), `Super+.` et
`Super+;` (émojis). `Alt+F6`, que Publisher donne au premier plan, est pris par la plupart des
gestionnaires de fenêtres (A4).

Relevé de nouveau le 29 octobre 2026 pour le poste N18, par `gsettings list-recursively` (GNOME
50.1, IBus 1.5.34, moteur `xkb:fr::fra`) : ni `Ctrl+Maj+N`, ni `Ctrl+Pg. suiv`, ni `Ctrl+Pg. préc`, ni
`F5`, ni `Ctrl+B` ne sont pris par le bureau — `Ctrl+Pg. suiv`, `Ctrl+Pg. préc` et `Ctrl+Maj+N` ne
le sont que par le terminal Ptyxis, chez lui ; le bureau prend `Super+Pg. suiv` et
`Ctrl+Alt+F5`, leurs voisins. **`Ctrl+Maj+U` reste à ibus** (`org.freedesktop.ibus.panel.emoji
unicode-hotkey ['<Control><Shift>u']`), et Avalonia 11.3.9 lui passe les touches : lu décompilé,
`X11PlatformOptions.EnableIme` vaut vrai par défaut, et la méthode de saisie se choisit sur
`QT_IM_MODULE` ou `XMODIFIERS`, ici `ibus`. La touche est liée comme Publisher la donne ; qu'elle
arrive à la fenêtre sur ce bureau n'est pas mesuré — la sonde du clavier
(`MYPUBLISHER_SONDE_CLAVIER`) le dira.

Relevé de nouveau le 31 octobre 2026 pour le poste N20, par `gsettings list-recursively` : ni
`Ctrl+F`, ni `Ctrl+H`, ni `F3`, ni `Maj+F4` ne sont pris par le bureau ; seul le terminal Ptyxis
prend `Ctrl+Maj+F`, chez lui.

Relevé de nouveau le 31 octobre 2026 pour le poste N22, par `gsettings list-recursively` (GNOME
Shell 50.1, IBus 1.5.34) : aucune liaison du bureau, d'ibus ni des extensions ne porte la lettre Y,
sous quelque modificateur que ce soit ; `Ctrl+Maj+Y` est libre. La machine n'a toujours pas KDE
(pas de `~/.config/kglobalshortcutsrc`).
