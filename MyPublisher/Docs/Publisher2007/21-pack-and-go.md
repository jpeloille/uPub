# 21 — Pack and Go : emporter le document avec ses ressources

**Source** : *Microsoft Publisher 2007 Student Guide*, p. 61–67.
**Verdict** : `manquant`

## Ce que fait Publisher 2007

> « You can send your publication to a zip file. The file includes all graphics and all
> type used. »

Un assistant qui règle le problème des ressources liées : au moment de porter le
document ailleurs — autre machine, imprimeur — il rassemble la publication, ses images
et ses polices dans une archive.

## Détail de l'IHM

`File/Pack and Go ▸` — deux destinations :
- **Take to Another Computer…**
- **Take to a Commercial Printing Service…**

Assistant, quatre écrans :
1. Rappel de ce qu'il va faire : *Embed fonts. Include linked graphics.*
2. Emplacement : graver sur disque, support amovible, disquette, ou
   **Other location:** avec **Browse…**
3. Cases à cocher : **Embed TrueType fonts**, **Include linked graphics**,
   **Create links for embedded graphics**.
4. Confirmation.

Les pages 66–67 montrent la décompression par l'explorateur Windows : hors sujet pour
nous.

À noter au passage, sur la même capture de menu (p. 61), deux entrées voisines :
**Publish as PDF or XPS…** — que MyPublisher fait déjà — et
**Import Word Document…**

## État dans MyPublisher

Le problème existe, entier, et le README le nomme :

- Les images sont référencées par **chemin absolu** (`Models/Frame.cs:189`) : « déplacer
  un document sans ses images casse les liens ».
- À l'export, une image manquante ne produit qu'un avertissement dans le rapport
  (`Services/PdfExporter.cs:146-150`) — le PDF sort avec un trou.
- Les polices sont résolues par fontconfig sur la machine
  (`Services/FontconfigFontResolver.cs`), avec substitution silencieuse si la famille
  demandée est absente. Un document ouvert sur une autre machine peut donc changer
  d'apparence sans prévenir.

## Écart / ce qu'il faudrait

La bonne réponse ici n'est pas un assistant en quatre écrans, c'est **un format
d'archive** — même résultat, aucune interaction.

1. **`.mypubz`** : un zip contenant le JSON du document, un dossier `images/`, et la
   liste des polices employées. `Services/DocumentSerializer.cs` fait 32 lignes ; une
   classe voisine suffit, et le choix se fait au filtre du sélecteur d'enregistrement.
   L'ouverture détecte le format et travaille sur une copie extraite.
2. **Variante minimale, à faire de toute façon** : à l'enregistrement, écrire les
   chemins d'images en **relatif** quand le fichier est sous le dossier du document.
   Quelques lignes dans le sérialiseur, et le cas le plus fréquent — « j'ai déplacé mon
   dossier de travail » — disparaît.
3. **Avertir sur les polices.** Aujourd'hui la substitution est totalement muette :
   `Match` interroge `fc-match`, accepte le fichier qu'il retourne sans jamais
   vérifier qu'il correspond à la famille demandée
   (`Services/FontconfigFontResolver.cs:65-100`), et retombe silencieusement sur
   DejaVu Sans dans trois cas — `fc-match` absent (exception avalée, `:94-97`),
   police introuvable, ou fichier `.ttc` non géré par PDFsharp (`:88-89`). Le
   résolveur dispose donc de tout ce qu'il faut pour signaler l'écart ; il ne le
   remonte simplement à personne. Un avertissement dans `ExportReport`, qui remonte
   déjà jusqu'à la barre d'état, suffirait.

L'embarquement des polices dans l'archive pose des questions de licence que Publisher
esquive et que nous n'avons pas à trancher tout de suite : le point 3 (avertir) couvre
l'essentiel du risque pour un coût très inférieur.
