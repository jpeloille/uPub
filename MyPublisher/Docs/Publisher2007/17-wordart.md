# 17 — WordArt

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 47–49.
**Verdict** : `hors périmètre`

## Ce que fait Publisher 2007

Un objet de texte décoratif : on choisit un style dans une galerie, on saisit le texte,
et le résultat est **un graphique**, pas du texte — le livre le dit explicitement
(« WordArt is a graphic »). Il se redimensionne et se tourne comme une image.

## Détail de l'IHM

1. Bouton *Insert WordArt* de la barre d'objets → une galerie de styles (le livre dit
   seulement « Click a WordArt style » ; la planche de styles elle-même n'est pas
   reproduite lisiblement).
2. Dialogue **Edit WordArt Text**, seul écran réellement lisible (p. 48) : `Font:`
   (Arial Black), `Size:` (36), boutons **B** et *I*, et une grande zone de saisie
   pré-remplie « Your Text Here ».
3. Une fois posé (p. 49) : poignées de redimensionnement, point vert de rotation, et
   une barre d'outils **WordArt** dédiée — le livre la mentionne sans en détailler
   les boutons, qui ne sont pas lisibles sur la capture.

## État dans MyPublisher

Aucun équivalent. `TextFrame` (`Models/Frame.cs:105-185`) offre police, corps, gras,
italique, couleur, alignement, interligne — de la typographie, pas de la décoration.

## Écart / ce qu'il faudrait

**Ne pas implémenter WordArt.** Deux raisons, et la seconde suffit :

- Le coût est élevé : déformer du texte le long d'une courbe suppose de convertir les
  glyphes en chemins vectoriels, côté écran comme côté PDF. Rien dans la chaîne
  actuelle ne va dans ce sens — `PdfExporter.DrawText` (`Services/PdfExporter.cs:105`)
  passe par `XTextFormatter`, qui compose du texte, pas des contours.
- L'esthétique est datée. Un document produit en 2026 avec un WordArt de 2007 a l'air
  d'un document de 2007.

**En revanche**, le besoin sous-jacent — « faire un titre qui se voit » — est légitime
et se traite pour bien moins cher, avec des propriétés ordinaires de `TextFrame` :
contour de caractère, ombre portée, et éventuellement un remplissage en dégradé (qui
arriverait de toute façon avec [09-arriere-plan-page.md](09-arriere-plan-page.md)).
PDFsharp sait tracer un texte en mode contour, et Avalonia sait afficher une ombre.
C'est la version 2026 de la même intention.
