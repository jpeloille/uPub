# 12 — Ajustement automatique du texte, alignement vertical, chaînage

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 36–37.
**Verdict** : `manquant`

## Ce que fait Publisher 2007

Clic droit → **Format Text Box** → onglet **Text Box**. C'est la page la plus dense du
livre pour notre sujet : elle contient à elle seule quatre fonctions que MyPublisher
n'a pas, dont la limite n°1 listée dans son README.

## Détail de l'IHM

Onglet **Text Box** :
- `Vertical alignment:` — liste déroulante (Top dans la capture), avec vignette d'aperçu.
- *Text Box Margins* — quatre champs indépendants : `Left:`, `Right:`, `Top:`,
  `Bottom:`, à 0.04" chacun par défaut.
- *Text autofitting* — trois options exclusives :
  - **Do not autofit**
  - **Shrink text on overflow** — réduit le corps juste ce qu'il faut,
  - **Best fit** — ajuste le corps pour remplir le bloc.
- Case **Rotate text within AutoShape by 90°**.
- Cases **Include "Continued on page…"** et **Include "Continued from page…"** —
  c'est-à-dire le **chaînage du texte entre blocs**, avec ses mentions de renvoi
  générées automatiquement.

## État dans MyPublisher

- Marge intérieure **unique et symétrique** : un seul `Padding`
  (`Models/Frame.cs:143`), appliqué aux quatre côtés à l'export
  (`Services/PdfExporter.cs:124-128`).
- Pas d'alignement vertical : le texte est calé en haut
  (`XStringFormats.TopLeft`, `:133` ; `VerticalContentAlignment="Top"`,
  `Views/PageCanvas.axaml:135`).
- Pas d'ajustement automatique.
- **Le débordement est silencieux.** À l'écran, `ClipToBounds="True"`
  (`Views/PageCanvas.axaml:106`) coupe le texte ; à l'export, `XTextFormatter.DrawString`
  s'arrête au bord de la boîte (`Services/PdfExporter.cs:141`). Rien n'avertit —
  `ExportReport` (`:12`) ne remonte que les images introuvables.

## Écart / ce qu'il faudrait

Par ordre de valeur croissante et de coût croissant :

1. **Signaler le débordement.** Le plus urgent, et le moins cher. Aujourd'hui on peut
   livrer un PDF au texte amputé sans jamais l'apprendre. Un marqueur visuel sur le
   bloc (Publisher affiche un petit A rouge) plus un avertissement dans `ExportReport`,
   qui remonte déjà jusqu'à la barre d'état (`MainWindowViewModel.cs:344-350`).
2. **Alignement vertical** — trois valeurs. Trivial à l'écran, calculable à l'export à
   partir de la hauteur mesurée.
3. **Marges intérieures par côté** — remplacer `double Padding` par quatre valeurs.
   Mécanique, sans piège, hormis la migration de format.
4. **Shrink to fit / Best fit** — recherche dichotomique sur `FontSize` jusqu'à ce que
   la hauteur mesurée tienne dans le bloc. La mesure est disponible des deux côtés :
   `FormattedText` chez Avalonia, `gfx.MeasureString` chez PDFsharp. Le piège classique
   est que les deux moteurs ne mesurent pas identiquement : mesurer **une seule fois**,
   au moment de l'édition, et stocker le corps calculé.
5. **Chaînage entre blocs.** C'est la limite n°1 du README (« Pas de chaînage du texte
   entre blocs : un texte trop long est coupé au bloc »). Il faut :
   - une notion de chaîne dans le modèle — un `NextFrame` sur `TextFrame`, sérialisé
     par identifiant (les `Frame` n'ont pas encore d'`Id`, il faudra l'ajouter) ;
   - un moteur de composition qui mesure, coupe au bloc et reporte le reste ;
   - les mentions de renvoi, qui découlent du reste.

   C'est le seul chantier lourd de tout ce corpus. C'est aussi celui qui sépare un
   poseur de blocs d'un vrai logiciel de mise en page : sans lui, aucun article ne
   peut courir sur deux colonnes ou deux pages. À décider consciemment, pas à repousser
   par défaut.
