#!/usr/bin/env bash
#
# Recette du poste Q16 — le pointillé d'un bloc image obéit aux repères.
#
# Le défaut, trouvé par la recette de C1 : le pointillé gris d'un bloc image, « repère
# visible tant qu'aucune image n'est chargée » selon PageCanvas.axaml, était TOUJOURS
# dessiné. Une image cadrée sans déformation le laissait voir sur le bord du bloc, et il
# n'obéissait pas à l'interrupteur des repères : il entrait dans toute comparaison de
# l'écran au PDF. Il n'est plus dessiné que si les repères sont allumés ET qu'aucune
# image n'est chargée.
#
# Ce qu'elle éprouve, en deux temps qui ne se remplacent pas :
#
#   1. les tests d'interface du poste (« poste=Q16 ») : la vraie fenêtre, une image
#      chargée repères éteints puis allumés, un bloc sans image repères allumés puis
#      éteints — l'interrupteur basculé dans la même fenêtre ;
#   2. le banc, tel que les recettes le jouent pour comparer l'écran au PDF : une page
#      qui porte un bloc image chargé et un bloc sans fichier, rendue repères éteints
#      puis allumés. Le gris du pointillé (#B0B0B0) est compté sur le pourtour de chacun.
#      Attendu : rien sur l'image chargée, dans les deux cas ; sur le bloc sans fichier,
#      le pointillé repères allumés, et rien repères éteints.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# ------------------------------------------------------------------- 1. les tests

attendus=3
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=Q16" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests d'interface sous « poste=Q16 » ne passent pas." >&2
    exit 1
fi
joues="$(grep -o 'Passed: *[0-9]*' "$travail/tests.txt" | head -1 | grep -o '[0-9]*$' || true)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=Q16 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi
echo "  1. la fenêtre : image chargée sans pointillé, repères éteints ou allumés ; bloc vide pointillé selon les repères ($attendus tests)"

# ------------------------------------------------------------------- 2. le banc

# Une image carrée, magenta, dans un bloc en largeur : cadrée sans déformation, elle
# laisse nus les bords gauche et droit du bloc, là où le pointillé se voyait.
python3 - "$travail/carre.png" <<'PY'
import struct, sys, zlib

def png(chemin, r, v, b, largeur, hauteur):
    brut = b"".join(b"\x00" + bytes([r, v, b]) * largeur for _ in range(hauteur))
    def bloc(tag, donnees):
        corps = tag + donnees
        return struct.pack(">I", len(donnees)) + corps + struct.pack(">I", zlib.crc32(corps) & 0xFFFFFFFF)
    entete = struct.pack(">IIBBBBB", largeur, hauteur, 8, 2, 0, 0, 0)
    with open(chemin, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + bloc(b"IHDR", entete)
                + bloc(b"IDAT", zlib.compress(brut)) + bloc(b"IEND", b""))

png(sys.argv[1], 255, 0, 255, 8, 8)
PY

document="$travail/pointille.mypub"
cat > "$document" <<'JSON'
{
  "FormatVersion": 4,
  "Title": "Recette — pointillé d'un bloc image",
  "Pages": [
    {
      "Name": "Page 1",
      "Frames": [
        { "$kind": "image", "Name": "Chargée", "X": 150, "Y": 200, "Width": 240, "Height": 120,
          "Stroke": null, "Path": "carre.png", "Fit": "Uniform" },
        { "$kind": "image", "Name": "Attente", "X": 150, "Y": 450, "Width": 240, "Height": 120,
          "Stroke": null, "Path": "", "Fit": "Uniform" }
      ]
    }
  ]
}
JSON

"$banc" rendre "$document" --sortie "$travail/eteints.png" --page 1 --pixels-par-point 2 --sans-reperes > /dev/null
"$banc" rendre "$document" --sortie "$travail/allumes.png" --page 1 --pixels-par-point 2 > /dev/null

python3 - "$travail/eteints.png" "$travail/allumes.png" <<'PY'
import sys
from PIL import Image

PX = 2

def aplatir(chemin):
    image = Image.open(chemin).convert("RGBA")
    fond = Image.new("RGBA", image.size, (255, 255, 255, 255))
    return Image.alpha_composite(fond, image).convert("RGB")

rendus = {"repères éteints": aplatir(sys.argv[1]), "repères allumés": aplatir(sys.argv[2])}

def gris(c):
    r, v, b = c
    return abs(r - v) <= 6 and abs(v - b) <= 6 and 150 <= r <= 215

def pointille(image, x, y, l, h, bande=4):
    """Les pixels du gris du pointillé, sur 2 pt de part et d'autre du pourtour."""
    g, t, d, b = int(x * PX), int(y * PX), int((x + l) * PX), int((y + h) * PX)
    return sum(1 for j in range(t - bande, b + bande) for i in range(g - bande, d + bande)
               if (i < g + bande or i >= d - bande or j < t + bande or j >= b - bande)
               and gris(image.getpixel((i, j))))

charge = (150, 200, 240, 120)
attente = (150, 450, 240, 120)

echec = False
def refuser(message):
    global echec
    print(f"    ÉCHEC : {message}", file=sys.stderr)
    echec = True

for quoi, image in rendus.items():
    # Que l'image soit bien chargée : sinon le bloc « chargé » serait un second bloc
    # vide, et l'absence de pointillé repères éteints n'y prouverait rien.
    if image.getpixel((270 * PX, 260 * PX)) != (255, 0, 255):
        refuser(f"{quoi} — l'image n'est pas dessinée au centre du bloc chargé.")

    n = pointille(image, *charge)
    print(f"    {quoi:16} bloc chargé   : {n:5} pixels de pointillé")
    if n:
        refuser(f"{quoi} — {n} pixels de pointillé autour d'une image chargée ; aucun attendu.")

    n = pointille(image, *attente)
    print(f"    {quoi:16} bloc sans image : {n:5} pixels de pointillé")
    # Le pourtour fait 720 pt, 1 440 pixels ; le motif « 4,3 » en couvre 4 sur 7.
    if quoi == "repères allumés" and n < 400:
        refuser(f"{quoi} — le bloc sans image ne montre que {n} pixels de pointillé : "
                f"le repère qui marque sa place a disparu.")
    if quoi == "repères éteints" and n:
        refuser(f"{quoi} — le bloc sans image montre {n} pixels de pointillé : "
                f"il n'obéit pas à l'interrupteur des repères.")

sys.exit(1 if echec else 0)
PY
echo "  2. le banc : le pointillé ne se dessine que sur un bloc sans image, et repères allumés"

echo "Q16 : le pointillé d'un bloc image obéit aux repères, et se retire quand l'image est là."
