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
# La page porte six blocs :
#
#   · « Large »  — le cas du PLAN : un groupe de 120 pt portant un texte de 400 pt, à
#                  fond jaune, sans filet ;
#   · « Filé »   — un groupe dont un bloc, filé de 4 pt, touche le bord du groupe en
#                  haut, à gauche et à droite ;
#   · « Bande »  — un tableau dont les cases sont filées de 4 pt : le tableau découpe
#                  ses cases comme un groupe ses blocs ;
#   · « Tourné » — un groupe tourné de 30°, qui porte un bloc trop long et un bloc filé :
#                  la découpe tourne avec le groupe, à l'écran comme au PDF ;
#   · « Externe » — un groupe qui en porte un autre, « Interne » : chacun découpe ce qui
#                  le dépasse, à sa propre marge ;
#   · « Losange » — un bloc tourné de 45° dans un groupe ajusté à son empreinte, filé de
#                  6 pt : la pointe d'onglet de son angle sort de 3·√2 pt, et non de 3.
#                  Ces trois-là ont été demandés par la relecture du 12 octobre 2026, qui
#                  avait trouvé la pointe émoussée.
#
# Ce qu'elle mesure, sur l'écran (le banc) ET sur le PDF (pdftoppm), à deux pixels par
# point :
#
#   1. rien n'est dessiné au-delà du groupe « Large » : toute la bande qui va de son bord
#      droit à celui du texte est blanche, alors que le fond jaune est bien là en deçà ;
#   2. les filets de 4 pt sur le bord du groupe « Filé » et du tableau font 4 pt, des
#      deux côtés : une découpe au cadre exact en laisserait 2 ;
#   3. les groupes imbriqués : rien au-delà d'« Interne » (marge comprise), ni au-delà
#      d'« Externe », alors que chacun montre bien le bloc qui dépasse en deçà ;
#   4. la pointe du losange : sous son sommet, à 3,25 pt au-dessus de l'angle, le filet
#      est là — une marge de 3 pt l'aurait coupé à 3 ;
#   5. le filet grossier : « Scripts/comparer-rendus.sh », qui voit aussi le groupe
#      tourné.
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
attendus=12
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
          ] },
        { "$kind": "group", "Name": "Tourné", "X": 400, "Y": 280, "Width": 120, "Height": 100, "Rotation": 30,
          "Stroke": null,
          "Children": [
            { "$kind": "shape", "Name": "Long", "X": 0, "Y": 20, "Width": 400, "Height": 60,
              "Fill": { "$kind": "solid", "Color": "#FFFFCC00" }, "Stroke": null, "Shape": "Rectangle" },
            { "$kind": "shape", "Name": "Filé", "X": 0, "Y": 0, "Width": 120, "Height": 20,
              "Fill": { "$kind": "solid", "Color": "#FF00A000" }, "Stroke": "#FF0000FF", "StrokeThickness": 4, "Shape": "Rectangle" }
          ] },
        { "$kind": "group", "Name": "Externe", "X": 100, "Y": 600, "Width": 200, "Height": 150,
          "Stroke": null,
          "Children": [
            { "$kind": "group", "Name": "Interne", "X": 20, "Y": 20, "Width": 100, "Height": 60,
              "Stroke": null,
              "Children": [
                { "$kind": "shape", "Name": "Très long", "X": 0, "Y": 30, "Width": 300, "Height": 30,
                  "Fill": { "$kind": "solid", "Color": "#FFFFCC00" }, "Stroke": null, "Shape": "Rectangle" },
                { "$kind": "shape", "Name": "Bord", "X": 0, "Y": 0, "Width": 100, "Height": 30,
                  "Fill": { "$kind": "solid", "Color": "#FF00A000" }, "Stroke": "#FF0000FF", "StrokeThickness": 4, "Shape": "Rectangle" }
              ] },
            { "$kind": "shape", "Name": "Bas", "X": 0, "Y": 100, "Width": 260, "Height": 50,
              "Fill": { "$kind": "solid", "Color": "#FFFF00FF" }, "Stroke": null, "Shape": "Rectangle" }
          ] },
        { "$kind": "group", "Name": "Enfant tourné", "X": 380, "Y": 600, "Width": 141.42, "Height": 141.42,
          "Stroke": null,
          "Children": [
            { "$kind": "shape", "Name": "Losange", "X": 20.71, "Y": 20.71, "Width": 100, "Height": 100, "Rotation": 45,
              "Fill": { "$kind": "solid", "Color": "#FF00A000" }, "Stroke": "#FF0000FF", "StrokeThickness": 6, "Shape": "Rectangle" }
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

# 3 — les groupes imbriqués. « Interne » (x de 120 à 220 pt) a pour marge le demi-filet
# de « Bord », 2 pt : son bloc « Très long » (y de 650 à 680) s'arrête à 222. « Externe »
# (x de 100 à 300) a la même marge, ses descendants compris : « Bas » (y de 700 à 750)
# s'arrête à 302.
MAGENTA = (255, 0, 255)
for nom, image in rendus.items():
    for quoi, dedans, couleur, zone in (
        ("« Très long », dans « Interne »", (200, 665), JAUNE, (223, 652, 370, 678)),
        ("« Bas », dans « Externe »",       (280, 725), MAGENTA, (303, 702, 370, 748)),
    ):
        if not proche(image.getpixel((dedans[0] * PX, dedans[1] * PX)), couleur, 12):
            refuser(f"{nom} — {quoi} n'est pas dessiné en deçà du bord : la mesure ne prouverait rien.")
        x0, y0, x1, y1 = zone
        encre = sum(1 for x in range(x0 * PX, x1 * PX) for y in range(y0 * PX, y1 * PX)
                    if not proche(image.getpixel((x, y)), BLANC, 48))
        if encre:
            refuser(f"{nom} — {quoi} : {encre} pixels au-delà de la découpe ; aucun attendu.")
if echec:
    sys.exit(1)
print("  3. groupes imbriqués : chacun découpe ce qui le dépasse, à sa marge, des deux côtés")

# 4 — la pointe d'onglet. Le losange a son sommet sur le bord haut du groupe, à
# x = 380 + 70,71 et y = 600 ; filé de 6 pt et joint en onglet, sa pointe monte à
# 600 − 3·√2 = 595,76. Le pixel pris à y = 596,75 (centre de la rangée 1 193), sous le
# sommet, est dans la pointe, à 1 pt de son bout : bleu. Une marge de 3 pt couperait à
# 597, et ce pixel serait blanc.
for nom, image in rendus.items():
    teinte = image.getpixel((round(450.71 * PX), 1193))
    if not proche(teinte, BLEU):
        refuser(f"{nom} — la pointe d'onglet du losange est coupée : {teinte} à 3,25 pt "
                f"au-dessus de son angle, le filet attendu.")
if echec:
    sys.exit(1)
print("  4. la pointe d'onglet d'un bloc tourné dans son groupe n'est pas émoussée, des deux côtés")
PY

# ------------------------------------------------------------------------- 3. filet

# Relevé avant ce poste, sur la page à trois blocs : 4,35 % — le fond et le texte qui
# couraient 280 pt plus loin dans le PDF (67 200 pixels de fond jaune comptés par la
# mesure 1, 3,3 % de la page), et les filets rentrés de Q4. Sur la page à six blocs :
# 8,03 % avant, 0,13 % après — le lissage du texte, des coins, et des bords obliques du
# groupe tourné et du losange. Le seuil est à 0,3 % : plus du double de ce relevé, et
# onze fois sous la seule bande de fond que l'export ne découpait pas.
Scripts/comparer-rendus.sh "$document" 1 0.3

echo "Q5 : l'export découpe au groupe comme l'écran, et la découpe laisse aux filets du bord leur épaisseur."
