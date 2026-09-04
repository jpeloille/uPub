# 09 — Arrière-plan de page et effets de remplissage

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 24–32.
**Verdict** : `manquant`

## Ce que fait Publisher 2007

`Format/Background` ouvre un volet de vignettes prêtes à l'emploi ; **More backgrounds**
donne accès au dialogue **Fill Effects**, qui est le cœur du sujet. Ce même dialogue
sert au remplissage des blocs (le livre le dit p. 35 : « If you click Fill Effects,
you'll be able to add the textures, etc., that you used to add a page background »).
Un seul modèle de remplissage, réutilisé partout.

## Détail de l'IHM

Dialogue **Fill Effects**, cinq onglets, avec un aperçu *Sample* permanent et une case
**Rotate fill effect with shape** commune à tous les onglets :

- **Gradient** — *Colors* : `One color` / `Two colors` / `Preset` ; `Color 1:`,
  `Color 2:`. *Transparency* : `From:` et `To:` en pourcentage. *Shading styles* :
  Horizontal, Vertical, Diagonal up, Diagonal down, From corner, From center ; puis
  quatre *Variants* du style retenu.
- **Texture** — grille de textures nommées (« Denim ») et bouton **Other Texture…**
  pour charger un fichier.
- **Pattern** — 48 motifs, avec `Foreground:` et `Background:` réglables ; le libellé
  indique le motif choisi (« 50% »).
- **Picture** — **Select Picture…** et case **Lock picture aspect ratio**.
- **Tint** — `Base color:` puis une grille de 18 pastilles teinte/nuance dérivées de
  cette couleur, avec le libellé du taux (« 60% Tint »).

## État dans MyPublisher

- **Aucun fond de page.** La page est un aplat blanc (`Views/PageCanvas.axaml:30,43`)
  et l'export n'écrit rien avant les blocs (`Services/PdfExporter.cs:33-40`).
- Les formes n'ont qu'une couleur unie, stockée en `#AARRGGBB` (`Models/Frame.cs:209`),
  rendue par un `XSolidBrush` unique à l'export (`Services/PdfExporter.cs:190-194`).
- Les blocs de texte ont depuis peu un fond uni propre (`Background`,
  `Models/Frame.cs:124-127`) ; les blocs d'image n'ont toujours rien —
  voir [11-format-zone-texte.md](11-format-zone-texte.md).

## Écart / ce qu'il faudrait

Par ordre décroissant de valeur pour l'effort :

1. **Fond de page uni** — une propriété `Background` sur `Document`, un rectangle plein
   dessiné en tête de chaque page à l'export. Une heure de travail, effet immédiat sur
   l'allure d'un document.
2. **Dégradé deux couleurs** sur les blocs — PDFsharp fournit `XLinearGradientBrush`,
   donc l'export suit sans invention. Les six styles d'ombrage de Publisher se ramènent
   à un angle.
3. **Image de fond** — réutilise entièrement `ImageFrame` et `FitRect`
   (`Services/PdfExporter.cs:171`) ; la case *Lock picture aspect ratio* correspond
   exactement à l'`ImageFit` déjà modélisé (`Models/Frame.cs:9`).
4. **Teinte** — purement calculatoire à partir d'une couleur de base : aucune ressource
   à embarquer, et c'est un vrai service rendu à qui compose avec un jeu de couleurs
   (voir [01-nouveau-document.md](01-nouveau-document.md)).
5. **Textures et motifs** — demandent d'embarquer des images ou de tramer. Faible
   priorité, esthétique datée.

**Conséquence de modèle à anticiper.** `string? Fill` ne suffira plus. Il faut un type
de remplissage discriminé — uni, dégradé, image, teinte — exactement comme `Frame` est
déjà polymorphe par `$kind` (`Models/Frame.cs:19-22`). Le mécanisme de sérialisation
est donc déjà en place et éprouvé ; c'est le bon moment pour le faire, avant que des
`.mypub` existants ne se multiplient.
