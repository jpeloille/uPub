#!/usr/bin/env bash
#
# Recette des postes C5a et C5c — fond de page, et remplissages non unis.
#
# Elle se joue en deux temps, et le premier est le seul qui porte sur du réel.
#
# 1. ATA 21, le document ATR lui-même. Ses cinq PNG ne sont pas des blocs image : ce
#    sont des remplissages de forme, et jusqu'à ce poste l'import les perdait en le
#    disant. On vérifie qu'ils arrivent, qu'ils sont écrits, et — c'est la mesure qui
#    compte — qu'ils atteignent le PDF SANS DÉFORMATION. Une des cinq porte
#    « librevenge:rotate: 90 » : son PNG fait 1062 × 617 dans un cadre au rapport
#    exactement inverse, si bien que perdre le quart de tour l'écrase d'un facteur trois.
#    « pdfimages -list » donne la définition horizontale et verticale de chaque image
#    posée ; elles ne sont égales que si l'image n'a pas été étirée de travers. C'est un
#    contrôle que ni un compte d'objets ni une comparaison d'images ne rendraient.
#
# 2. Samples/recette-fonds.mypub, pour le dégradé, la teinte et le fond de page —
#    qu'aucun des deux documents réels ne porte, et il faut le dire : sur ces trois-là
#    la recette éprouve le mécanisme, pas des valeurs relevées. Les couleurs sont
#    MESURÉES au pixel, dans le PDF puis à l'écran, aux mêmes points.
#
# Pourquoi mesurer plutôt que comparer les deux images. Parce que l'écran et l'export
# appellent les mêmes calculs — « GradientFill.Extremites », « TintFill.Uniforme » — et
# qu'une erreur dedans serait commise des deux côtés : les images concorderaient sur un
# dégradé tracé à l'envers. La comparaison d'ensemble reste, en filet, pour ce qu'une
# couleur ponctuelle ne voit pas ; ce sont les tests du noyau et les valeurs ci-dessous
# qui éprouvent le poste.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

for exe in "$binaire" "$banc"; do
    if [ ! -x "$exe" ]; then
        echo "ÉCHEC : binaire introuvable ($exe). Compiler d'abord." >&2
        exit 1
    fi
done

# La lecture du .pub est déléguée à libmspub, comme dans la recette du poste R5. Sans
# l'outil, la moitié qui compte ne peut rien affirmer : on le dit et on échoue.
outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$outil" ] || [ ! -x "$outil" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable." >&2
    echo "    sudo apt install libmspub-tools" >&2
    echo "ou, sans droits d'installation : Scripts/outils-pub.sh" >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$outil"
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# ------------------------------------------------------------------ 1. ATA 21

echo "  ATA 21 — les cinq images du document réel :"

"$binaire" "MyPublisher/Books/ATA 21 - Air conditioning.pub" \
    --resave "$travail/ata21.mypub" > "$travail/import.txt" 2> "$travail/import-err.txt"

if grep -q "Remplissage par image perdu" "$travail/import.txt" "$travail/import-err.txt"; then
    echo "ÉCHEC : l'import perd encore les remplissages par image." >&2
    grep -h "Remplissage" "$travail/import.txt" "$travail/import-err.txt" >&2
    exit 1
fi

python3 - "$travail/ata21.mypub" "$travail/cadre.txt" <<'PY'
import json, os, sys

document = json.load(open(sys.argv[1], encoding="utf-8"))
fonds = []
cadres = []

def parcours(bloc, page):
    fond = bloc.get("Fill")
    if isinstance(fond, dict) and fond.get("$kind") == "image":
        fonds.append(fond)
        if fond.get("Rotation"):
            cadres.append((page, bloc["X"], bloc["Y"], bloc["Width"], bloc["Height"]))
    for enfant in bloc.get("Children", []):
        parcours(enfant, page)

for numero, page in enumerate(document["Pages"], start=1):
    for bloc in page["Frames"]:
        parcours(bloc, numero)

# Le cadre de l'image tournée, que l'étape suivante ira mesurer sur les deux rendus.
with open(sys.argv[2], "w", encoding="utf-8") as sortie:
    for cadre in cadres:
        print(*cadre, file=sortie)

echec = False
print(f"    remplissages par image   source    5   importé {len(fonds):>4}")
if len(fonds) != 5:
    echec = True

# Les fichiers sont réellement écrits : un chemin qui ne mène nulle part vaudrait une
# image perdue, avec le silence en plus.
manquants = [f["Path"] for f in fonds if not os.path.isfile(f["Path"])]
print(f"    fichiers écrits          attendu   5   trouvé  {len(fonds) - len(manquants):>4}")
for chemin in manquants:
    print(f"    fichier absent : {chemin}", file=sys.stderr)
    echec = True

# Une seule des cinq porte le quart de tour, et c'est celle du relevé.
tournees = [f for f in fonds if f.get("Rotation")]
print(f"    quarts de tour           attendu   1   trouvé  {len(tournees):>4}")
if len(tournees) != 1 or tournees[0]["Rotation"] != 90:
    echec = True

sys.exit(1 if echec else 0)
PY

"$binaire" "$travail/ata21.mypub" --export "$travail/ata21.pdf" > /dev/null
pdfimages -list "$travail/ata21.pdf" > "$travail/images.txt"

python3 - "$travail/images.txt" <<'PY'
import sys

lignes = [l.split() for l in open(sys.argv[1], encoding="utf-8")
          if l.strip() and l.split()[0].isdigit()]

print(f"    images dans le PDF       attendu   5   trouvé  {len(lignes):>4}")
echec = len(lignes) != 5

# La définition horizontale et verticale de l'image telle qu'elle est POSÉE. Elles ne
# sont égales que si l'image n'a pas été étirée de travers — c'est ainsi qu'on mesure
# qu'un quart de tour a bien été appliqué. Sans lui, l'image couchée d'ATA 21 sort à
# 349 ppi en largeur pour 118 en hauteur.
for champs in lignes:
    x, y = float(champs[-4]), float(champs[-3])
    ecart = abs(x - y) / max(x, y, 1) * 100
    marque = "" if ecart <= 2 else "   <- DÉFORMÉE"
    if ecart > 2:
        echec = True
    print(f"    image {champs[3]:>5} x {champs[4]:<5} posée à {x:>5.0f} x {y:<5.0f} ppi"
          f"   écart {ecart:5.1f} %{marque}")

sys.exit(1 if echec else 0)
PY

# Le compte de définitions ci-dessus dit qu'aucune image n'est ÉTIRÉE de travers. Il ne
# dit pas qu'elle a TOURNÉ : une image posée dans un cadre couché mais non tournée garde
# une échelle uniforme, et se fait simplement rogner. Ce qui sépare les deux, c'est
# qu'une image tournée remplit son cadre du haut en bas, tandis que la même sans quart de
# tour n'en occupe que la bande centrale — ici 219 pt sur 376, soit 78 pt de blanc en
# haut et autant en bas. On mesure donc jusqu'où l'encre monte et descend dans le cadre,
# des deux côtés.
page="$(cut -d' ' -f1 "$travail/cadre.txt")"
pdftoppm -r 144 -f "$page" -l "$page" -png -singlefile "$travail/ata21.pdf" "$travail/ata21-pdf"
"$banc" rendre "$travail/ata21.mypub" --sortie "$travail/ata21-ecran.png" \
        --page "$page" --pixels-par-point 2 --sans-reperes > /dev/null

python3 - "$travail/cadre.txt" "$travail/ata21-pdf.png" "$travail/ata21-ecran.png" <<'PY'
import sys
from PIL import Image

cadres = [l.split() for l in open(sys.argv[1], encoding="utf-8") if l.strip()]
_, x, y, largeur, hauteur = (float(v) for v in cadres[0])

echec = False
marges = {}
for nom, chemin in (("PDF", sys.argv[2]), ("écran", sys.argv[3])):
    image = Image.open(chemin).convert("L")
    cadre = image.crop(tuple(round(v * 2) for v in (x, y, x + largeur, y + hauteur)))
    pixels = cadre.load()
    lignes = [j for j in range(cadre.height)
              if any(pixels[i, j] < 200 for i in range(cadre.width))]

    if not lignes:
        print(f"    {nom} : aucune encre dans le cadre de l'image tournée", file=sys.stderr)
        echec = True
        continue

    haut, bas = lignes[0] / 2, (cadre.height - 1 - lignes[-1]) / 2
    marges[nom] = (haut, bas)
    marque = "" if max(haut, bas) <= 15 else "   <- L'IMAGE NE REMPLIT PAS SON CADRE"
    if max(haut, bas) > 15:
        echec = True
    print(f"    image tournée, {nom:5} : encre à {haut:5.1f} pt du haut et {bas:5.1f} pt du bas"
          f" (cadre de {hauteur:.0f} pt){marque}")

# Et les deux rendus doivent s'accorder : la rotation est appliquée deux fois, une par
# moteur, et c'est le seul endroit du poste où les deux écritures peuvent diverger.
if len(marges) == 2:
    ecart = max(abs(a - b) for a, b in zip(marges["PDF"], marges["écran"]))
    if ecart > 3:
        print(f"    l'écran et le PDF ne posent pas l'image au même endroit ({ecart:.1f} pt)",
              file=sys.stderr)
        echec = True

sys.exit(1 if echec else 0)
PY

# ------------------------------------------------------------------ 2. les couleurs

echo "  Fonds mesurés au pixel, PDF puis écran :"

document="Samples/recette-fonds.mypub"
"$binaire" "$document" --export "$travail/fonds.pdf" > /dev/null
pdftoppm -r 144 -f 1 -l 1 -png -singlefile "$travail/fonds.pdf" "$travail/pdf"
"$banc" rendre "$document" --sortie "$travail/ecran.png" \
        --page 1 --pixels-par-point 2 --sans-reperes > /dev/null

python3 - "$travail/pdf.png" "$travail/ecran.png" <<'PY'
import sys
from PIL import Image

pdf = Image.open(sys.argv[1]).convert("RGB")
ecran = Image.open(sys.argv[2]).convert("RGB")

def teinte(image, x, y):
    return image.getpixel((round(x * 2), round(y * 2)))

def hexa(rvb):
    return "#%02X%02X%02X" % rvb

# Point mesuré, couleur attendue, et ce que le point éprouve.
#
# Les valeurs viennent des règles du modèle et non d'un relevé pris sur le rendu : la
# teinte de #804020 à 50 % est #C0A090 — c'est la moitié du chemin vers le blanc, et
# c'est le même nombre que le test du noyau épingle. Le fond de page monte du blanc au
# #336699 sur toute la hauteur, il est donc presque blanc en haut ; les deux dégradés
# sont mesurés à leurs deux bords, ce qui les prendrait en défaut s'ils étaient tracés
# à l'envers.
points = [
    ("fond de page, haut",   300,  20, (250, 251, 253), 6),
    ("fond de page, bas",    300, 820, ( 56, 106, 156), 6),
    ("teinte 50 %",          160, 140, (192, 160, 144), 2),
    ("aplat",                420, 140, ( 31,  59,  87), 2),
    ("dégradé, bord gauche",  62, 280, (254,   0,   1), 6),
    ("dégradé, bord droit",  518, 280, (  1,   0, 254), 6),
    ("ellipse, sommet",      290, 385, (  0, 247,   0), 8),
    ("ellipse, bas",         290, 535, (  0,   8,   0), 8),
    # Hors de l'ellipse, c'est le fond de page qu'on doit voir : un remplissage qui ne
    # serait pas découpé à la forme couvrirait ce point.
    ("hors de l'ellipse",     62, 385, (162, 185, 208), 8),
    # Les deux contours, et le coin arrondi. Ils ne sont pas décoratifs : jusqu'à C5c,
    # une forme se traçait d'un seul appel, fond et contour ensemble ; un remplissage par
    # image ne se posant pas au pinceau, les deux ont été séparés. Ces trois points sont
    # le seul endroit qui mesure ce changement de chemin — sans eux, une ellipse ou un
    # angle arrondi qui perdrait son filet passerait le contrôle sans un mot.
    ("filet de l'ellipse",   290, 380.5, (204,   0,   0), 8),
    ("filet de l'aplat",   320.5, 140,   (204,   0,   0), 8),
    # Dans le coin qu'arrondit le bloc, c'est le fond de page qui doit passer : un
    # arrondi perdu par le fond le peindrait de l'aplat.
    ("coin arrondi",         321, 101,   (231, 237, 243), 8),
]

echec = False
for nom, x, y, attendue, marge in points:
    obtenue = teinte(pdf, x, y)
    vue = teinte(ecran, x, y)

    ecart = max(abs(a - b) for a, b in zip(obtenue, attendue))
    accord = max(abs(a - b) for a, b in zip(obtenue, vue))

    marque = ""
    if ecart > marge:
        marque = "   <- COULEUR"
        echec = True
    # L'écran et le PDF au même point : deux moteurs, deux rasterisations, un point de
    # quantification d'écart au plus.
    if accord > 3:
        marque += "   <- ÉCRAN ≠ PDF"
        echec = True

    print(f"    {nom:22} PDF {hexa(obtenue)}  attendu {hexa(attendue)}"
          f"  écran {hexa(vue)}{marque}")

sys.exit(1 if echec else 0)
PY

# Le filet : la page entière, écran contre PDF. Elle ne sépare pas grand-chose sur des
# aplats — c'est justement pourquoi les mesures ci-dessus existent — mais elle attrape
# ce qu'aucune d'elles ne regarde : un fond qui disparaîtrait ailleurs sur la page.
#
# Le seuil est à 2 % et non à 1 : les deux filets ajoutés au document — celui de
# l'ellipse surtout, qui est une longue courbe — portent l'écart de 0,08 % à 0,99 %, à
# eux seuls et sur le seul lissage de leurs bords. Un seuil qui frôlerait la mesure
# tomberait un jour sur une autre machine, pour rien.
Scripts/comparer-rendus.sh "$document" 1 2.0 > "$travail/comparaison.txt" 2>&1 || {
    echo "ÉCHEC : l'écran et le PDF divergent sur l'ensemble de la page." >&2
    cat "$travail/comparaison.txt" >&2
    exit 1
}
sed -n 's/^/    /p' "$travail/comparaison.txt" | tail -2

echo "C5 : les cinq images d'ATA 21 arrivent sans déformation, et les fonds se mesurent."
