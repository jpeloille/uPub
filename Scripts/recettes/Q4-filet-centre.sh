#!/usr/bin/env bash
#
# Recette du poste Q4 — un filet au même endroit à l'écran et au PDF.
#
# Le défaut, mesuré en A2 puis par la recette de C1 : l'écran RENTRAIT le filet dans le
# bloc d'une demi-épaisseur — un Rectangle ou une Ellipse d'Avalonia diminuent leur
# géométrie de la moitié de leur trait —, alors que l'export le centre sur le bord, comme
# le dit la documentation de « Frame.StrokeThickness ». C'est donc l'écran qui avait
# tort. Il élargit désormais l'élément du filet d'une demi-épaisseur, ce qui ramène sa
# géométrie exactement sur le cadre du bloc.
#
# La recette du PLAN : « sur un filet de 4 pt, les bords relevés à l'écran et au PDF
# concordent à 0,25 pt près ». Elle se joue sur une page fabriquée pour elle, qui porte
# un filet de 4 pt sur chacun des chemins par lesquels l'écran trace un filet :
#
#   · « Photo »    — un bloc image, cadré pour remplir (la découpe de l'image ne doit
#                    pas rogner le filet) : le contour commun, « Rectangle.frameStroke » ;
#   · « Légende »  — un bloc de texte à fond : même contour, hors du panneau découpé ;
#   · « Aplat »    — une forme rectangle, dont le contour est tracé à part ;
#   · « Arrondi »  — la même, à angles arrondis de 20 pt : le rayon doit porter sur le
#                    cadre, pas sur un cadre rentré ;
#   · « Ellipse »  — une forme ellipse, son propre élément ;
#   · « Groupe »   — un groupe filé, qui porte un bloc sans filet : le contour du groupe
#                    est tracé hors de sa découpe (poste Q5), et ne doit pas en souffrir ;
#   · une rangée de filets de 1 pt et de 3 pt — forme, texte, ellipse pour chacun. C'est
#     la relecture du 12 octobre 2026 qui les a demandés : la première version de la
#     réparation ne centrait que les filets dont la demi-épaisseur tombe sur un point
#     entier. Sous la mise à l'échelle, Avalonia arrondissait la marge négative au point
#     entier, en arrondi bancaire : +0,5 pt d'écart pour 1 pt, −0,5 pour 3, et la page
#     ne portait que des filets de 4. Le filet de 1 pt est celui du modèle, et 77 des
#     88 filets des Samples. Les épaisseurs de 0,5 ou 1,5 pt ne sont pas mesurées ici :
#     à deux pixels par point, leurs bords tombent au milieu d'un pixel, et pdftoppm
#     cale le trait sur le pixel entier — ±0,25 pt au PDF seul, qui disparaît à huit
#     pixels par point (mesuré le 12 octobre 2026, écart écran − PDF de 0,00 pt).
#
# Ce qu'elle mesure, sur l'écran (le banc) ET sur le PDF (pdftoppm), à deux pixels par
# point, au milieu de chacun des quarante-huit côtés :
#
#   1. l'épaisseur : celle du modèle, au quart de point près, des deux côtés — un filet
#      rogné de sa moitié par une découpe en donnerait la moitié ;
#   2. la position : au PDF, le filet est centré sur le bord à 0,25 pt près ; à l'écran,
#      son centre est à 0,25 pt au plus de celui du PDF. Avant ce poste, l'écart était
#      d'une demi-épaisseur vers l'intérieur : la recette tombait sur chaque côté ;
#   3. le filet grossier : « Scripts/comparer-rendus.sh », à un seuil CALCULÉ — voir
#      plus bas, au moment de l'appeler.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile, plutôt que de se fier aux binaires présents : le banc n'est pas
# reconstruit par « dotnet test », et un binaire périmé ferait passer la recette sur du
# code qu'elle n'a pas éprouvé.
if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# ------------------------------------------------------------------- la page de recette

# Un aplat magenta pour la photo, qu'aucun filet ni fond de la page ne porte.
python3 - "$travail/photo.png" <<'PY'
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

document="$travail/filet-centre.mypub"
cat > "$document" <<'JSON'
{
  "FormatVersion": 4,
  "Title": "Recette — filet centré",
  "Pages": [
    {
      "Name": "Page 1",
      "Frames": [
        { "$kind": "image", "Name": "Photo", "X": 100, "Y": 100, "Width": 200, "Height": 120,
          "Stroke": "#FF0000FF", "StrokeThickness": 4, "Path": "photo.png", "Fit": "UniformToFill" },
        { "$kind": "text", "Name": "Légende", "X": 360, "Y": 100, "Width": 160, "Height": 120,
          "Fill": { "$kind": "solid", "Color": "#FFFFF2B3" },
          "Stroke": "#FF0000FF", "StrokeThickness": 4, "Text": "" },
        { "$kind": "shape", "Name": "Aplat", "X": 100, "Y": 300, "Width": 200, "Height": 120,
          "Fill": { "$kind": "solid", "Color": "#FFFFF2B3" },
          "Stroke": "#FF0000FF", "StrokeThickness": 4, "Shape": "Rectangle" },
        { "$kind": "shape", "Name": "Arrondi", "X": 360, "Y": 300, "Width": 160, "Height": 120,
          "Fill": { "$kind": "solid", "Color": "#FFFFF2B3" },
          "Stroke": "#FF0000FF", "StrokeThickness": 4, "CornerRadius": 20, "Shape": "Rectangle" },
        { "$kind": "shape", "Name": "Ellipse", "X": 100, "Y": 500, "Width": 200, "Height": 120,
          "Fill": { "$kind": "solid", "Color": "#FFFFF2B3" },
          "Stroke": "#FF0000FF", "StrokeThickness": 4, "Shape": "Ellipse" },
        { "$kind": "group", "Name": "Groupe", "X": 360, "Y": 500, "Width": 160, "Height": 120,
          "Stroke": "#FF0000FF", "StrokeThickness": 4,
          "Children": [
            { "$kind": "shape", "Name": "Dedans", "X": 40, "Y": 30, "Width": 80, "Height": 60,
              "Fill": { "$kind": "solid", "Color": "#FFFFF2B3" }, "Stroke": null, "Shape": "Rectangle" }
          ] },
        { "$kind": "shape", "Name": "Aplat 1", "X": 40, "Y": 680, "Width": 70, "Height": 60,
          "Stroke": "#FF0000FF", "StrokeThickness": 1, "Shape": "Rectangle" },
        { "$kind": "text", "Name": "Texte 1", "X": 130, "Y": 680, "Width": 70, "Height": 60,
          "Stroke": "#FF0000FF", "StrokeThickness": 1, "Text": "" },
        { "$kind": "shape", "Name": "Ellipse 1", "X": 220, "Y": 680, "Width": 70, "Height": 60,
          "Stroke": "#FF0000FF", "StrokeThickness": 1, "Shape": "Ellipse" },
        { "$kind": "shape", "Name": "Aplat 3", "X": 310, "Y": 680, "Width": 70, "Height": 60,
          "Stroke": "#FF0000FF", "StrokeThickness": 3, "Shape": "Rectangle" },
        { "$kind": "text", "Name": "Texte 3", "X": 400, "Y": 680, "Width": 70, "Height": 60,
          "Stroke": "#FF0000FF", "StrokeThickness": 3, "Text": "" },
        { "$kind": "shape", "Name": "Ellipse 3", "X": 490, "Y": 680, "Width": 70, "Height": 60,
          "Stroke": "#FF0000FF", "StrokeThickness": 3, "Shape": "Ellipse" }
      ]
    }
  ]
}
JSON

# Le document doit se relire tel qu'on l'a écrit : un filet perdu à la lecture rendrait
# les deux images pareillement nues, et la mesure muette.
"$binaire" "$document" --resave "$travail/relu.mypub" > /dev/null
python3 - "$travail/relu.mypub" <<'PY'
import json, sys
blocs = json.load(open(sys.argv[1], encoding="utf-8"))["Pages"][0]["Frames"]
if len(blocs) != 12:
    sys.exit(f"ÉCHEC : relu, {len(blocs)} blocs au lieu de 12.")
for bloc in blocs:
    voulue = int(bloc["Name"][-1]) if bloc["Name"][-1].isdigit() else 4
    if not bloc.get("Stroke") or bloc.get("StrokeThickness") != voulue:
        sys.exit(f"ÉCHEC : relu, « {bloc['Name']} » a perdu son filet de {voulue} pt.")
if next(b for b in blocs if b["Name"] == "Arrondi").get("CornerRadius") != 20:
    sys.exit("ÉCHEC : relu, « Arrondi » a perdu ses angles.")
PY

# ------------------------------------------------------------------- les deux rendus

"$binaire" "$document" --export "$travail/sortie.pdf" > /dev/null 2> "$travail/export.txt"
if [ -s "$travail/export.txt" ]; then
    echo "ÉCHEC : l'export s'est plaint — la page ne prouverait rien." >&2
    cat "$travail/export.txt" >&2
    exit 1
fi

pdftoppm -r 144 -f 1 -l 1 -png -singlefile "$travail/sortie.pdf" "$travail/pdf"
"$banc" rendre "$document" --sortie "$travail/ecran.png" \
        --page 1 --pixels-par-point 2 --sans-reperes > /dev/null

for rendu in pdf ecran; do
    magick "$travail/$rendu.png" -background white -alpha remove -alpha off "$travail/$rendu.ppm"
done

# ------------------------------------------------------------------ 1 et 2 : mesurer

python3 - "$travail/pdf.ppm" "$travail/ecran.ppm" <<'PY'
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
    largeur, pixels = champs[0], donnees[i + 1:]

    def couleur(x, y):
        k = (y * largeur + x) * 3
        return tuple(pixels[k:k + 3])
    return couleur

rendus = {"PDF": lire(sys.argv[1]), "écran": lire(sys.argv[2])}

PX = 2
BLEU = (0, 0, 255)

def proche(c, voulue, seuil=60):
    return max(abs(a - b) for a, b in zip(c, voulue)) <= seuil

# (nom, x, y, largeur, hauteur, épaisseur du filet), en points.
blocs = [
    ("Photo",     100, 100, 200, 120, 4),
    ("Légende",   360, 100, 160, 120, 4),
    ("Aplat",     100, 300, 200, 120, 4),
    ("Arrondi",   360, 300, 160, 120, 4),
    ("Ellipse",   100, 500, 200, 120, 4),
    ("Groupe",    360, 500, 160, 120, 4),
    ("Aplat 1",    40, 680,  70,  60, 1),
    ("Texte 1",   130, 680,  70,  60, 1),
    ("Ellipse 1", 220, 680,  70,  60, 1),
    ("Aplat 3",   310, 680,  70,  60, 3),
    ("Texte 3",   400, 680,  70,  60, 3),
    ("Ellipse 3", 490, 680,  70,  60, 3),
]

def traversee(couleur, bloc, cote):
    """
    Les pixels traversés perpendiculairement au bord, au milieu du côté, avec la
    distance de chacun au bord en points, positive vers l'INTÉRIEUR. Chaque pixel est
    pris en son centre ; ±6 pt autour du bord.
    """
    _, x, y, l, h, _ = bloc
    milieu_x, milieu_y = int((x + l / 2) * PX), int((y + h / 2) * PX)
    bord = {"gauche": x, "droite": x + l, "haut": y, "bas": y + h}[cote] * PX
    for d in range(-12, 13):
        if cote in ("gauche", "haut"):
            p = int(bord) + d
            interieur = (p + 0.5 - bord) / PX
        else:
            p = int(bord) - 1 - d
            interieur = (bord - (p + 0.5)) / PX
        point = (p, milieu_y) if cote in ("gauche", "droite") else (milieu_x, p)
        yield interieur, couleur(*point)

echec = False
def refuser(message):
    global echec
    print(f"    ÉCHEC : {message}", file=sys.stderr)
    echec = True

ecarts = []
for bloc in blocs:
    nom, voulue = bloc[0], bloc[5]
    for cote in ("gauche", "droite", "haut", "bas"):
        centres = {}
        for rendu, couleur in rendus.items():
            pris = [d for d, c in traversee(couleur, bloc, cote) if proche(c, BLEU)]
            if not pris:
                refuser(f"{rendu} — « {nom} », côté {cote} : aucun filet sur le bord.")
                continue

            # 1 — l'épaisseur, au quart de point : deux pixels par point exactement.
            epaisseur = len(pris) / PX
            if abs(epaisseur - voulue) > 0.25:
                refuser(f"{rendu} — « {nom} », côté {cote} : filet de {epaisseur} pt, "
                        f"{voulue} attendus.")

            centres[rendu] = (min(pris) + max(pris)) / 2

        if len(centres) < 2:
            continue

        # 2 — au PDF, centré sur le bord ; à l'écran, au même endroit qu'au PDF.
        if abs(centres["PDF"]) > 0.25:
            refuser(f"PDF — « {nom} », côté {cote} : filet centré à {centres['PDF']:+.2f} pt "
                    f"du bord, 0 attendu (« centré sur le bord »).")
        ecart = centres["écran"] - centres["PDF"]
        ecarts.append(abs(ecart))
        if abs(ecart) > 0.25:
            refuser(f"écran — « {nom} », côté {cote} : filet à {ecart:+.2f} pt de celui du PDF "
                    f"(positif : rentré dans le bloc). 0 ± 0,25 attendu.")

if echec:
    sys.exit(1)

print("  1. quarante-huit côtés, douze blocs, filets de 1, 3 et 4 pt, deux rendus : chacun a son épaisseur")
print(f"  2. PDF centré sur le bord ; écran à {max(ecarts):.2f} pt au plus du PDF (0,25 admis)")
PY

# ------------------------------------------------------------------------- 3. filet

# Le seuil se calcule. Un filet d'épaisseur e rentré de e/2 — Q4 tel qu'il était —
# laisse sur le pourtour du bloc deux bandes de e/2, l'une couverte d'un côté, l'autre
# de l'autre : 2 × e/2 × (2 px/pt)² = 4e pixels par point de pourtour. Les six blocs de
# 4 pt portent 3 470 pt de pourtour (l'ellipse 510), soit 55 520 pixels ; la rangée de
# 1 pt, 725 pt, soit 2 900 ; celle de 3 pt, autant de pourtour, soit 8 690. Total :
# 67 110 pixels, 3,34 % des 2 007 330 de la page. Relevé avant réparation : 3,41 %.
#
# Relevé avec la première réparation, qui laissait l'arrondi de mise en page : 0,36 %,
# les seuls filets de 1 et de 3 pt à un demi-point. Après la seconde : 0,04 % — le
# lissage de l'arrondi et des ellipses, que les deux moteurs ne posent pas au même
# pixel, et le bord de la photo. Le seuil est à 0,3 % : sept fois ce relevé, pour que le
# lissage d'une autre machine ne le fasse pas tomber, et sous l'écart d'un demi-point
# sur la seule rangée du bas. Ce sont pourtant les mesures ci-dessus, côté par côté, qui
# attrapent un décalage les premières : la comparaison n'est qu'un filet.
Scripts/comparer-rendus.sh "$document" 1 0.3

echo "Q4 : des filets de 1, 3 et 4 pt sont centrés sur le bord à l'écran comme au PDF, sur les six chemins éprouvés."
