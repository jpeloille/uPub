#!/usr/bin/env bash
#
# Recette du poste A3 — chemins d'images relatifs.
#
# Ce qu'elle éprouve, sur un atelier fabriqué pour elle :
#
#   1. à la réécriture, une image rangée SOUS le dossier du document s'écrit en
#      relatif, et une image rangée ailleurs garde son chemin absolu ;
#   2. le dossier entier se déplace — pour de bon, par « mv », l'ancien chemin
#      n'existe plus — et le document s'ouvre encore, images comprises : les deux
#      images se retrouvent dans le PDF, à leur place et de leur couleur ;
#   3. réécrit depuis son nouveau dossier, le document porte exactement les mêmes deux
#      formes de chemin : la forme n'a pas dérivé au passage ;
#   4. contre-épreuve — l'image du dehors, elle, ne survit PAS au déplacement de SON
#      dossier. C'est ce qui prouve que son chemin est bien resté absolu, et que
#      l'export du point 2 ne se taisait pas par indifférence.
#
# La mesure du point 2 est une couleur relevée dans la page rendue, et non l'absence
# d'un avertissement : un export qui ne se plaint de rien peut très bien avoir dessiné
# un trou. Les deux aplats sont magenta et cyan, deux couleurs qu'aucun autre élément
# de la page ne porte.
#
# Pourquoi un document fabriqué et non l'un des deux .pub ATR : ceux-ci ne portent
# aucune image LIÉE. Les cinq PNG d'ATA 21 sont des remplissages de forme
# (« draw:fill: bitmap ») que l'import de R5 ne reprend pas — le relevé
# MyPublisher/Docs/Documents-reels.md le dit, et le README le répète parmi ses limites.
# Ce que cette recette reproduit n'est donc pas un document réel mais le geste réel que
# la fiche 21 désigne comme le cas de casse le plus fréquent : « j'ai déplacé mon
# dossier de travail ».
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
if [ ! -x "$binaire" ]; then
    echo "ÉCHEC : binaire introuvable ($binaire). Compiler d'abord." >&2
    exit 1
fi

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

atelier="$travail/atelier"
dehors="$travail/hors-atelier"
mkdir -p "$atelier/images" "$dehors"

# ---------------------------------------------------------------- l'atelier de départ

# Deux aplats, écrits comme n'importe quel outil les écrirait. Huit pixels de côté
# suffisent : le bloc est en cadrage « Fill », l'image est étirée à sa taille.
python3 - "$atelier/images/plan.png" "$dehors/logo.png" <<'PY'
import struct, sys, zlib

def png(chemin, r, v, b, cote=8):
    brut = b"".join(b"\x00" + bytes([r, v, b]) * cote for _ in range(cote))
    def bloc(tag, donnees):
        corps = tag + donnees
        return struct.pack(">I", len(donnees)) + corps + struct.pack(">I", zlib.crc32(corps) & 0xFFFFFFFF)
    entete = struct.pack(">IIBBBBB", cote, cote, 8, 2, 0, 0, 0)
    with open(chemin, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + bloc(b"IHDR", entete)
                + bloc(b"IDAT", zlib.compress(brut)) + bloc(b"IEND", b""))

png(sys.argv[1], 255, 0, 255)   # magenta : l'image de l'atelier
png(sys.argv[2], 0, 255, 255)   # cyan    : l'image du dehors
PY

# Le document, écrit à la main en chemins ABSOLUS des deux côtés — c'est l'état dans
# lequel tout ce qui a été enregistré jusqu'ici se trouve.
cat > "$atelier/notice.mypub" <<JSON
{
  "FormatVersion": 3,
  "Title": "Notice",
  "Pages": [
    {
      "Name": "Page 1",
      "Frames": [
        { "\$kind": "image", "Name": "Plan", "X": 60, "Y": 60, "Width": 200, "Height": 160,
          "Path": "$atelier/images/plan.png", "Fit": "Fill" },
        { "\$kind": "image", "Name": "Logo", "X": 60, "Y": 300, "Width": 200, "Height": 160,
          "Path": "$dehors/logo.png", "Fit": "Fill" }
      ]
    }
  ]
}
JSON

# ------------------------------------------------------------------------------- 1

# La réécriture, telle que la documentation la prescrit : « --resave » vers un fichier
# de travail, puis recopie par-dessus l'original. Les chemins sont donc rapportés au
# dossier du document, et non à /tmp.
"$binaire" "$atelier/notice.mypub" --resave "$travail/reecrit.mypub" > /dev/null
cp "$travail/reecrit.mypub" "$atelier/notice.mypub"

if ! grep -q '"Path": "images/plan.png"' "$atelier/notice.mypub"; then
    echo "ÉCHEC : l'image de l'atelier n'est pas écrite en relatif." >&2
    grep '"Path"' "$atelier/notice.mypub" >&2
    exit 1
fi

if ! grep -qF "\"Path\": \"$dehors/logo.png\"" "$atelier/notice.mypub"; then
    echo "ÉCHEC : l'image du dehors ne garde pas son chemin absolu." >&2
    grep '"Path"' "$atelier/notice.mypub" >&2
    exit 1
fi

if grep -qF "$atelier" "$atelier/notice.mypub"; then
    echo "ÉCHEC : le document porte encore le chemin absolu de son propre dossier." >&2
    grep -F "$atelier" "$atelier/notice.mypub" >&2
    exit 1
fi

echo "  1. relatif sous le dossier, absolu ailleurs : les deux formes sont écrites."

# ------------------------------------------------------------------------------- 2

# Le déplacement, pour de bon : après ce « mv », plus rien n'existe à l'ancien chemin.
mkdir -p "$travail/archives"
mv "$atelier" "$travail/archives/notice-2026"
deplace="$travail/archives/notice-2026/notice.mypub"

if [ -e "$atelier" ]; then
    echo "ÉCHEC : le dossier de départ existe encore, le déplacement n'a pas eu lieu." >&2
    exit 1
fi

"$binaire" "$deplace" --export "$travail/sortie.pdf" \
    > "$travail/export.txt" 2> "$travail/avertissements.txt"

if grep -q 'Image introuvable' "$travail/avertissements.txt"; then
    echo "ÉCHEC : le dossier déplacé a perdu une image." >&2
    cat "$travail/avertissements.txt" >&2
    exit 1
fi

# Deux pixels par point, comme partout ailleurs dans ce dépôt.
pdftoppm -r 144 -f 1 -l 1 "$travail/sortie.pdf" "$travail/page"
page="$(ls "$travail"/page-*.ppm | head -1)"

# Les deux aplats sont relevés au centre de leur bloc : (160, 140) pt et (160, 380) pt,
# soit le double en pixels. Une couleur juste à cet endroit-là veut dire que l'image a
# été trouvée, lue, et posée où il fallait.
python3 - "$page" <<'PY'
import sys

def lire(chemin):
    donnees = open(chemin, "rb").read()
    champs, i = [], 2
    while len(champs) < 3:
        while donnees[i:i + 1].isspace(): i += 1
        if donnees[i:i + 1] == b"#":
            while donnees[i:i + 1] != b"\n": i += 1
            continue
        debut = i
        while not donnees[i:i + 1].isspace(): i += 1
        champs.append(int(donnees[debut:i]))
    return champs[0], champs[1], donnees[i + 1:]

largeur, hauteur, pixels = lire(sys.argv[1])

def couleur(x, y):
    i = (y * largeur + x) * 3
    return tuple(pixels[i:i + 3])

attendu = {"le plan de l'atelier": ((320, 280), (255, 0, 255)),
           "le logo du dehors":    ((320, 760), (0, 255, 255))}

echec = False
for nom, ((x, y), voulue) in attendu.items():
    obtenue = couleur(x, y)
    ecart = max(abs(a - b) for a, b in zip(obtenue, voulue))
    print(f"    {nom:22} en ({x:4}, {y:4}) : {obtenue}   attendu {voulue}")
    if ecart > 12:
        print(f"    ÉCHEC : {nom} n'est pas dans le PDF.", file=sys.stderr)
        echec = True

sys.exit(1 if echec else 0)
PY

echo "  2. le dossier déplacé s'ouvre encore, et ses deux images sont dans le PDF."

# ------------------------------------------------------------------------------- 3

"$binaire" "$deplace" --resave "$travail/relu.mypub" > /dev/null
if ! diff -u "$deplace" "$travail/relu.mypub" > "$travail/ecart.diff"; then
    echo "ÉCHEC : réécrit depuis son nouveau dossier, le document a changé." >&2
    head -20 "$travail/ecart.diff" >&2
    exit 1
fi

echo "  3. réécrit depuis son nouveau dossier, le document est identique au caractère près."

# ------------------------------------------------------------------------------- 4

# La contre-épreuve. Le chemin du logo est absolu : déplacer SON dossier doit le
# casser. Sans cette étape, le point 2 pourrait passer sur un export qui ne dit jamais
# rien — et la recette entière ne prouverait plus qu'elle-même.
mv "$dehors" "$travail/hors-atelier-ailleurs"

"$binaire" "$deplace" --export "$travail/sortie-bis.pdf" \
    > /dev/null 2> "$travail/avertissements-bis.txt" || true

if ! grep -qF "Image introuvable : $dehors/logo.png" "$travail/avertissements-bis.txt"; then
    echo "ÉCHEC : l'image du dehors survit à un déplacement, son chemin n'était donc pas absolu." >&2
    cat "$travail/avertissements-bis.txt" >&2
    exit 1
fi

if grep -q 'plan.png' "$travail/avertissements-bis.txt"; then
    echo "ÉCHEC : l'image de l'atelier s'est perdue en même temps." >&2
    cat "$travail/avertissements-bis.txt" >&2
    exit 1
fi

echo "  4. l'image du dehors, elle, se casse quand son dossier bouge : son chemin est bien absolu."

echo "A3 : un dossier déplacé garde ses images ; ce qui est dehors reste en absolu."
