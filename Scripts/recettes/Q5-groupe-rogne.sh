#!/usr/bin/env bash
#
# Recette du poste Q5 — un groupe rogne pareil à l'écran et au PDF.
#
# Le défaut, relevé en C9 : un bloc plus large que son groupe s'arrêtait au bord du
# groupe à l'écran — le thème d'Avalonia découpe la liste qui porte ses blocs — et
# courait jusqu'au bout dans le PDF. Le PLAN tranche dans le sens de A2 : l'export
# découpe au groupe, comme l'écran. Les deux lisent désormais la même zone, calculée
# dans le noyau (« Decoupe.DesBlocs »).
#
# Cette zone déborde du cadre du groupe du plus large demi-filet de ses blocs, et c'est
# la deuxième moitié de la recette. Grouper ajuste le groupe aux cadres de ses blocs,
# filets non compris ; depuis que le filet est centré sur le bord à l'écran (poste Q4),
# une découpe au cadre exact rognerait de sa moitié extérieure le filet de tout bloc
# posé sur le bord d'un groupe ou d'un tableau — à l'écran comme, désormais, au PDF.
#
# La page porte trois blocs :
#
#   · « Large »  — le cas du PLAN : un groupe de 120 pt portant un texte de 400 pt, à
#                  fond jaune, sans filet ;
#   · « Filé »   — un groupe dont un bloc, filé de 4 pt, touche le bord du groupe en
#                  haut, à gauche et à droite ;
#   · « Bande »  — un tableau dont les cases sont filées de 4 pt : le tableau découpe
#                  ses cases comme un groupe ses blocs.
#
# Ce qu'elle mesure, sur l'écran (le banc) ET sur le PDF (pdftoppm), à deux pixels par
# point :
#
#   1. rien n'est dessiné au-delà du groupe « Large » : toute la bande qui va de son bord
#      droit à celui du texte est blanche, alors que le fond jaune est bien là en deçà ;
#   2. les filets de 4 pt sur le bord du groupe « Filé » et du tableau font 4 pt, des
#      deux côtés : une découpe au cadre exact en laisserait 2 ;
#   3. le filet grossier : « Scripts/comparer-rendus.sh ».
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# Les tests du noyau qui épinglent la zone : sans filet, le cadre du groupe ; avec, le
# plus large demi-filet de ses blocs, groupes imbriqués et cases de tableau compris.
attendus=7
if ! dotnet test MyPublisher.Tests/MyPublisher.Tests.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=Q5" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests du noyau sous « poste=Q5 » ne passent pas." >&2
    exit 1
fi
joues="$(grep -o 'Passed: *[0-9]*' "$travail/tests.txt" | head -1 | grep -o '[0-9]*$' || true)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=Q5 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi

# ------------------------------------------------------------------- la page de recette

document="$travail/groupe-rogne.mypub"
cat > "$document" <<'JSON'
{
  "FormatVersion": 4,
  "Title": "Recette — un groupe rogne",
  "Pages": [
    {
      "Name": "Page 1",
      "Frames": [
        { "$kind": "group", "Name": "Large", "X": 100, "Y": 100, "Width": 120, "Height": 100,
          "Stroke": null,
          "Children": [
            { "$kind": "text", "Name": "Long", "X": 0, "Y": 20, "Width": 400, "Height": 60,
              "Fill": { "$kind": "solid", "Color": "#FFFFCC00" }, "Stroke": null,
              "Text": "Un texte de quatre cents points dans un groupe de cent vingt" }
          ] },
        { "$kind": "group", "Name": "Filé", "X": 100, "Y": 300, "Width": 200, "Height": 100,
          "Stroke": null,
          "Children": [
            { "$kind": "shape", "Name": "Bandeau", "X": 0, "Y": 0, "Width": 200, "Height": 50,
              "Fill": { "$kind": "solid", "Color": "#FF00A000" },
              "Stroke": "#FF0000FF", "StrokeThickness": 4, "Shape": "Rectangle" },
            { "$kind": "shape", "Name": "Socle", "X": 50, "Y": 70, "Width": 100, "Height": 30,
              "Fill": { "$kind": "solid", "Color": "#FFFFCC00" }, "Stroke": null, "Shape": "Rectangle" }
          ] },
        { "$kind": "table", "Name": "Bande", "X": 100, "Y": 500, "Width": 300, "Height": 40,
          "Stroke": null, "FontSize": 10, "CellPadding": 3,
          "Columns": [ { "Width": 150 }, { "Width": 150 } ],
          "Rows": [ { "Height": 40 } ],
          "Cells": [
            { "Row": 0, "Column": 0, "Text": "",
              "Fill": { "$kind": "solid", "Color": "#FFEFF2F6" }, "Stroke": "#FF0000FF", "StrokeThickness": 4 },
            { "Row": 0, "Column": 1, "Text": "",
              "Fill": { "$kind": "solid", "Color": "#FFEFF2F6" }, "Stroke": "#FF0000FF", "StrokeThickness": 4 }
          ] }
      ]
    }
  ]
}
JSON

# Relu tel qu'écrit : un texte ramené à la largeur du groupe, ou des cases perdues,
# feraient passer la recette sur un autre cas que le sien.
"$binaire" "$document" --resave "$travail/relu.mypub" > /dev/null
python3 - "$travail/relu.mypub" <<'PY'
import json, sys
blocs = {b["Name"]: b for b in json.load(open(sys.argv[1], encoding="utf-8"))["Pages"][0]["Frames"]}
large = blocs["Large"]
if large["Width"] != 120 or large["Children"][0]["Width"] != 400:
    sys.exit("ÉCHEC : relu, le texte de 400 pt dans un groupe de 120 n'est plus ce cas-là.")
if [c.get("StrokeThickness") for c in blocs["Bande"]["Cells"]] != [4, 4]:
    sys.exit("ÉCHEC : relu, les cases du tableau ont perdu leur filet de 4 pt.")
PY

# ------------------------------------------------------------------- les deux rendus

"$binaire" "$document" --export "$travail/sortie.pdf" > /dev/null 2> "$travail/export.txt"
if [ -s "$travail/export.txt" ]; then
    echo "ÉCHEC : l'export s'est plaint." >&2
    cat "$travail/export.txt" >&2
    exit 1
fi

pdftoppm -r 144 -f 1 -l 1 -png -singlefile "$travail/sortie.pdf" "$travail/pdf"
"$banc" rendre "$document" --sortie "$travail/ecran.png" \
        --page 1 --pixels-par-point 2 --sans-reperes > /dev/null

python3 - "$travail/pdf.png" "$travail/ecran.png" <<'PY'
import sys
from PIL import Image

PX = 2
rendus = {}
for nom, chemin in (("PDF", sys.argv[1]), ("écran", sys.argv[2])):
    image = Image.open(chemin).convert("RGBA")
    fond = Image.new("RGBA", image.size, (255, 255, 255, 255))
    rendus[nom] = Image.alpha_composite(fond, image).convert("RGB")

def proche(c, voulue, seuil=60):
    return max(abs(a - b) for a, b in zip(c, voulue)) <= seuil

JAUNE, BLEU, BLANC = (255, 204, 0), (0, 0, 255), (255, 255, 255)

echec = False
def refuser(message):
    global echec
    print(f"    ÉCHEC : {message}", file=sys.stderr)
    echec = True

# 1 — au-delà du groupe « Large » (bord droit à x = 220 pt), sur la hauteur du texte
# (y de 120 à 180 pt) et jusqu'au bout du texte (x = 500 pt) : rien. On commence au
# pixel qui suit le bord, et l'on compte tout ce qui s'écarte du blanc de plus de 48
# sur un canal : le lissage de pdftoppm laisse sur ce premier pixel un soupçon de la
# découpe — (255, 249, 223) relevé —, alors que le fond jaune non découpé y vaut
# (255, 204, 0), à 255 du blanc.
for nom, image in rendus.items():
    if not proche(image.getpixel((210 * PX, 150 * PX)), JAUNE, 12):
        refuser(f"{nom} — le fond jaune du texte n'est pas dessiné dans le groupe : "
                f"la mesure qui suit ne prouverait rien.")
    encre = sum(1 for x in range(220 * PX, 500 * PX) for y in range(120 * PX, 180 * PX)
                if not proche(image.getpixel((x, y)), BLANC, 48))
    if encre:
        refuser(f"{nom} — {encre} pixels dessinés au-delà du groupe, sur la bande du texte "
                f"qui dépasse ; aucun attendu.")
if echec:
    sys.exit(1)
print("  1. un texte de 400 pt dans un groupe de 120 : rien au-delà du groupe, à l'écran comme au PDF")

# 2 — l'épaisseur des filets posés sur le bord, mesurée au travers du bord : 4 pt, soit
# huit pixels de bleu. (x, y) est le point du bord ; (dx, dy) le sens de la traversée.
bords = [
    ("« Filé », bord haut",    (200, 300), (0, 1)),
    ("« Filé », bord gauche",  (100, 325), (1, 0)),
    ("« Filé », bord droit",   (300, 325), (1, 0)),
    ("tableau, bord haut",     (175, 500), (0, 1)),
    ("tableau, bord gauche",   (100, 520), (1, 0)),
    ("tableau, bord droit",    (400, 520), (1, 0)),
    ("tableau, bord bas",      (175, 540), (0, 1)),
]
for nom, image in rendus.items():
    for quoi, (x, y), (dx, dy) in bords:
        bleus = sum(1 for d in range(-12, 12)
                    if proche(image.getpixel((x * PX + d * dx, y * PX + d * dy)), BLEU))
        if abs(bleus / PX - 4) > 0.25:
            refuser(f"{nom} — {quoi} : filet de {bleus / PX} pt, 4 attendus. "
                    f"La découpe du porteur le rogne-t-elle ?")
if echec:
    sys.exit(1)
print("  2. un filet de 4 pt sur le bord d'un groupe ou d'un tableau garde ses 4 pt, des deux côtés")
PY

# ------------------------------------------------------------------------- 3. filet

# Relevé avant ce poste : 4,35 % — le fond et le texte qui couraient 280 pt plus loin
# dans le PDF (67 200 pixels de fond jaune comptés par la mesure 1, 3,3 % de la page),
# et les filets de 4 pt rentrés à l'écran par Q4. Après : 0,05 %, le lissage du texte
# et des coins. Le seuil est à 0,3 % : six fois ce relevé, et onze fois sous la seule
# bande de fond que l'export ne découpait pas.
Scripts/comparer-rendus.sh "$document" 1 0.3

echo "Q5 : l'export découpe au groupe comme l'écran, et la découpe laisse aux filets du bord leur épaisseur."
