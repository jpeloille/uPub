# 11 — Format de bloc : remplissage et bordures

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 34–35.
**Verdict** : `partiel`

## Ce que fait Publisher 2007

Clic droit sur un bloc → **Format Text Box**. C'est le dialogue central de mise en
forme, à six onglets : *Colors and Lines*, *Size*, *Layout*, *Picture*, *Text Box*,
*Web*. Cette fiche couvre le premier ; l'onglet *Text Box* est traité en
[12-ajustement-texte.md](12-ajustement-texte.md).

Point structurant : **n'importe quel bloc a un fond et une bordure**, y compris un bloc
de texte et un cadre d'image.

## Détail de l'IHM

Onglet **Colors and Lines** :
- *Fill* — `Color:` avec **No Fill**, une palette, **More Colors…** et
  **Fill Effects…** (le dialogue de la fiche 09) ; `Transparency:` en pourcentage.
- *Line* — `Color:`, `Dashed:` (styles de tiret), `Style:` (filets simples, doubles…),
  `Weight:` en points (« 6 pt » dans la capture), bouton **BorderArt…** (bordures
  décoratives).
- Case **Draw border inside frame** — le trait est-il tracé à l'intérieur du cadre ou
  à cheval sur lui. Détail qui compte dès qu'on aligne au pixel.
- Boutons de **présélection par côté** : appliquer la bordure aux quatre côtés, à aucun,
  ou à un côté choisi dans l'aperçu.
- Case **Apply settings to new text boxes** — mémorise le réglage comme nouveau défaut.

Le menu contextuel qui y mène (p. 34) contient aussi : Cut, Copy, Paste, Delete Text,
Delete Object, Add to Content Library…, Save as Picture…, Change Text ▸,
Proofing Tools ▸, Order ▸, Format Text Box…, Look Up…, Zoom ▸, Hyperlink…

## État dans MyPublisher

Asymétrie nette selon le type de bloc :

- **`ShapeFrame`** a `Fill`, `Stroke`, `StrokeThickness`, `CornerRadius`
  (`Models/Frame.cs:209-212`), exportés correctement, y compris les coins arrondis
  (`Services/PdfExporter.cs:90-98`).
- **`TextFrame`** a désormais un **fond** — `Background`, nullable, avec une case
  « Appliquer un fond » et un sélecteur de couleur dans le panneau
  (`Models/Frame.cs:124-127` et `:154-164` ; `Views/MainWindow.axaml:339-345`), rendu à
  l'écran (`Views/PageCanvas.axaml:107`) comme à l'export
  (`Services/PdfExporter.cs:111-113`). La distinction *absence de fond* / *fond blanc*
  est correctement tenue de bout en bout. **Il lui manque encore la bordure** : pour
  encadrer un texte, il faut toujours superposer une forme.
- **`ImageFrame`** non plus (`:187-200`) : pas de filet autour d'une image.
- Le trait est toujours plein : `ParsePen` ne construit qu'un `XPen` simple
  (`Services/PdfExporter.cs:196-201`), sans style de tiret ni bordure par côté.
- La transparence existe déjà en pratique, portée par l'octet alpha du `#AARRGGBB`
  (`ParseColor`, `:204-220`), mais n'est exposée nulle part dans l'interface.
- Pas de menu contextuel sur les blocs.

## Écart / ce qu'il faudrait

1. **Généraliser fond et bordure à tous les blocs, au niveau de `Frame`.** Le fond
   vient d'arriver sur `TextFrame` seul ; le porter à `Frame` éviterait de le
   réécrire une troisième fois pour `ImageFrame`, et donnerait la bordure au passage.
   Côté export, `DrawShape` (`Services/PdfExporter.cs:74`) sait déjà tracer fond et
   contour — il suffit de l'appeler avant `DrawText` et `DrawImage` plutôt que de
   dessiner le rectangle à la main comme le fait aujourd'hui `DrawText`
   (`:111-113`). Côté écran, deux liaisons dans les `DataTemplate` de
   `Views/PageCanvas.axaml:90-137`.
   *Attention à la compatibilité* : les `.mypub` existants n'ont ces champs que sur les
   formes, et le tout récent `Background` porte un nom différent de `Fill`.
   `FormatVersion` (`Models/Document.cs:18`) est là pour ça — c'est le moment de
   trancher le nom, avant que des fichiers ne circulent.
2. **Exposer la transparence** — un curseur 0-100 % qui écrit l'alpha. Le modèle et
   l'export la gèrent déjà tous les deux.
3. **Traits en pointillés** — `XPen.DashStyle` côté PDF, `StrokeDashArray` côté
   Avalonia (déjà utilisé pour le cadre de marge, `PageCanvas.axaml:52`).
4. **Menu contextuel** sur le bloc, reprenant les commandes du panneau et de la barre
   d'outils. Pur confort, mais c'est le geste réflexe de tout utilisateur venant
   d'Office.
5. Bordure par côté et *BorderArt* : à ignorer.
