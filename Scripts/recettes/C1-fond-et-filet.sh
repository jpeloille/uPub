#!/usr/bin/env bash
#
# Recette du poste C1 — fond et bordure au niveau de « Frame ».
#
# La recette d'origine : « encadrer une image d'un filet de 2 pt sans superposer de
# forme ; l'export est identique à l'écran ». Jouée en rendant la même page des deux
# côtés à deux pixels par point, puis jetée. Elle se rejoue sur une page fabriquée pour
# elle, qui porte les trois cas que C1 a ouverts :
#
#   · « Photo »   — une image carrée, cadrée pour remplir son bloc sans se déformer
#                   (« UniformToFill ») : elle en déborde en haut et en bas, et l'export
#                   la découpe au bloc. C'est le seul cadrage qui découpe, et la
#                   découpe doit être levée avant le filet bleu de 2 pt tracé ensuite,
#                   sans quoi elle le rognerait de sa moitié extérieure ;
#   · « Portrait » — une image en hauteur, cadrée sans déformation dans un bloc en
#                    largeur : elle laisse voir à gauche et à droite le FOND jaune du
#                    bloc, que les images n'avaient pas avant C1 ; même filet ;
#   · « Légende » — un bloc de texte à fond et à filet rouge de 2 pt : la bordure que
#                   les blocs de texte n'avaient pas non plus.
#
# Ce qu'elle mesure, sur l'écran (le banc) ET sur le PDF (pdftoppm), à deux pixels par
# point :
#
#   1. LE FILET SUR LE BORD. Au milieu de chacun des douze côtés, le pixel juste à
#      l'intérieur du bord porte la couleur du filet. C'est ce qui prouve qu'il passe
#      SUR l'image et non sous elle — un filet tracé avant l'image serait couvert par
#      elle exactement là. Puis son épaisseur : 2 pt, soit quatre pixels exactement —
#      un filet de 1,5 ou de 2,5 pt, ou rogné de moitié par une découpe, tombe ;
#   2. SA POSITION. Au PDF, le filet est centré sur le bord, à un quart de point près :
#      c'est ce que dit la documentation de « Frame.StrokeThickness ». À l'écran, il est
#      au même endroit qu'au PDF, au même quart de point — voir plus bas ;
#   3. LE FOND ET LE CONTENU. Le fond jaune du portrait se relève de part et d'autre de
#      l'image, celui de la légende à côté du mot, et chaque image en son centre ; ce
#      que la photo aurait de trop, au-dessus et au-dessous du bloc, n'est pas dessiné ;
#   4. le filet grossier : l'écran et le PDF donnent la même image
#      (« Scripts/comparer-rendus.sh »).
#
# ── Le filet à l'écran, et le poste Q4 ──────────────────────────────────────────────
#
# Jusqu'au 12 octobre 2026, l'écran ne centrait pas le filet sur le bord : il le
# RENTRAIT dans le bloc d'une demi-épaisseur (Avalonia diminue la géométrie d'un
# Rectangle pour que son trait tienne dans ses limites). Mesuré sur cette page : au PDF,
# le filet de 2 pt couvrait [bord − 1, bord + 1] ; à l'écran, [bord, bord + 2]. C'était
# le défaut du poste Q4, et cette recette le TOLÉRAIT, pour ne pas tomber sur un défaut
# qui n'était pas de son poste : un écart écran − PDF admis de 0 à +1 pt, et une
# comparaison d'images à 1,2 %, calculée sur ce que Q4 coûtait.
#
# Q4 est réparé — l'écran élargit l'élément du filet d'une demi-épaisseur, ce qui ramène
# sa géométrie sur le cadre —, et ce que la recette tolérait, elle le refuse désormais :
#
#   · l'écart écran − PDF est exigé à 0 ± 0,25 pt sur chaque côté, comme la position du
#     PDF sur le bord. Une tolérance laissée de 0 à +1 pt laisserait Q4 revenir sans un
#     mot : c'est exactement l'intervalle qu'il occupait ;
#   · le seuil de la comparaison d'images est recalculé sans Q4. Ce que Q4 coûtait : un
#     filet décalé d'un point sur le pourtour des trois blocs laisse deux bandes d'un
#     point de large sur 2 120 pt de pourtour, soit 2 × 2 120 × 4 px/pt² = 16 960 pixels,
#     0,84 % de la page (relevé : 0,88 %). Ce qui reste sans lui : 0,04 %, relevé le
#     12 octobre 2026 — le lissage du mot « Légende » et une colonne de pixels au bord
#     droit du portrait, que les deux moteurs ne rééchantillonnent pas pareil. Le seuil
#     est à 0,25 % : six fois ce relevé, pour que le lissage d'une autre machine ne le
#     fasse pas tomber, et plus de trois fois sous le coût de Q4 — qui ne se cache donc
#     plus dans la marge de la comparaison. Un fond perdu de part et d'autre du portrait
#     (5,4 % de la page) tombe a fortiori.
#
# La recette du poste Q4 lui-même (« Scripts/recettes/Q4-filet-centre.sh ») mesure le
# filet sur tous les chemins qui en tracent un — formes, ellipse, arrondi, groupe ;
# celle-ci ne garde que les deux blocs de C1.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile, plutôt que de se fier aux binaires présents : un binaire périmé survit à
# une modification des sources, et la recette se déclarerait verte sur du code qu'elle
# n'a pas éprouvé (cf. la recette de A4). Le banc, surtout, n'est pas reconstruit par
# « dotnet test » quand on joue cette recette seule.
if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# ------------------------------------------------------------------- la page de recette

# Deux aplats : magenta pour la photo, vert pour le portrait — deux couleurs qu'aucun
# fond ni filet de la page ne porte. Huit pixels suffisent, le cadrage les étire.
python3 - "$travail/photo.png" "$travail/portrait.png" <<'PY'
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

png(sys.argv[1], 255, 0, 255, 8, 8)    # magenta, carré : déborde d'un bloc en largeur
png(sys.argv[2], 0, 160, 0, 8, 16)     # vert, en hauteur : laisse voir le fond
PY

# Les chemins d'images sont relatifs au document (poste A3) : la page se déplace avec
# son dossier temporaire sans qu'on ait à les écrire en absolu.
document="$travail/fond-et-filet.mypub"
cat > "$document" <<'JSON'
{
  "FormatVersion": 4,
  "Title": "Recette — fond et filet",
  "Pages": [
    {
      "Name": "Page 1",
      "Frames": [
        { "$kind": "image", "Name": "Photo", "X": 100, "Y": 100, "Width": 240, "Height": 180,
          "Stroke": "#FF0000FF", "StrokeThickness": 2, "Path": "photo.png", "Fit": "UniformToFill" },
        { "$kind": "image", "Name": "Portrait", "X": 100, "Y": 360, "Width": 240, "Height": 180,
          "Fill": { "$kind": "solid", "Color": "#FFFFCC00" },
          "Stroke": "#FF0000FF", "StrokeThickness": 2, "Path": "portrait.png", "Fit": "Uniform" },
        { "$kind": "text", "Name": "Légende", "X": 380, "Y": 100, "Width": 160, "Height": 60,
          "Fill": { "$kind": "solid", "Color": "#FFBFE3F5" },
          "Stroke": "#FFFF0000", "StrokeThickness": 2, "Text": "Légende" }
      ]
    }
  ]
}
JSON

# Le document doit d'abord se lire tel qu'on l'a écrit : un filet ou un fond perdus à la
# lecture rendraient les deux images pareillement vides, et la comparaison muette.
"$binaire" "$document" --resave "$travail/relu.mypub" > /dev/null
python3 - "$travail/relu.mypub" <<'PY'
import json, sys
blocs = json.load(open(sys.argv[1], encoding="utf-8"))["Pages"][0]["Frames"]
for bloc in blocs:
    if not bloc.get("Stroke") or bloc.get("StrokeThickness") != 2:
        sys.exit(f"ÉCHEC : relu, « {bloc['Name']} » a perdu son filet de 2 pt.")
for nom in ("Portrait", "Légende"):
    if not next(b for b in blocs if b["Name"] == nom).get("Fill"):
        sys.exit(f"ÉCHEC : relu, « {nom} » a perdu son fond.")
# Le cadrage de la photo est ce qui met la découpe en jeu : perdu, la recette jouerait
# un autre cas sans le dire.
if next(b for b in blocs if b["Name"] == "Photo").get("Fit") != "UniformToFill":
    sys.exit("ÉCHEC : relu, « Photo » n'est plus cadrée pour remplir (UniformToFill).")
PY

# ------------------------------------------------------------------- les deux rendus

"$binaire" "$document" --export "$travail/sortie.pdf" > /dev/null 2> "$travail/export.txt"
if [ -s "$travail/export.txt" ]; then
    echo "ÉCHEC : l'export s'est plaint — une image manque, la page ne prouverait rien." >&2
    cat "$travail/export.txt" >&2
    exit 1
fi

pdftoppm -r 144 -f 1 -l 1 -png -singlefile "$travail/sortie.pdf" "$travail/pdf"
"$banc" rendre "$document" --sortie "$travail/ecran.png" \
        --page 1 --pixels-par-point 2 --sans-reperes > /dev/null

# En PPM, que le lecteur ci-dessous lit sans bibliothèque : le banc écrit du RGBA, que
# l'on aplatit sur du blanc comme la page.
for rendu in pdf ecran; do
    magick "$travail/$rendu.png" -background white -alpha remove -alpha off "$travail/$rendu.ppm"
done

# ------------------------------------------------------------------ 1, 2 et 3 : mesurer

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
    largeur, hauteur, pixels = champs[0], champs[1], donnees[i + 1:]

    def couleur(x, y):
        k = (y * largeur + x) * 3
        return tuple(pixels[k:k + 3])
    return couleur

rendus = {"PDF": lire(sys.argv[1]), "écran": lire(sys.argv[2])}

PX = 2            # pixels par point, des deux côtés
BLEU, ROUGE = (0, 0, 255), (255, 0, 0)

def proche(c, voulue, seuil=60):
    return max(abs(a - b) for a, b in zip(c, voulue)) <= seuil

# (nom, x, y, largeur, hauteur, couleur du filet), en points.
blocs = [
    ("Photo",    100, 100, 240, 180, BLEU),
    ("Portrait", 100, 360, 240, 180, BLEU),
    ("Légende",  380, 100, 160,  60, ROUGE),
]

def traversee(couleur, bloc, cote):
    """
    Les pixels traversés perpendiculairement au bord, au milieu du côté, de l'intérieur
    vers l'extérieur, avec la distance de chacun au bord en points — positive vers
    l'INTÉRIEUR du bloc. Chaque pixel est pris en son centre.
    """
    _, x, y, l, h, _ = bloc
    milieu_x, milieu_y = int((x + l / 2) * PX), int((y + h / 2) * PX)
    bord = {"gauche": x, "droite": x + l, "haut": y, "bas": y + h}[cote] * PX
    for d in range(-8, 9):                      # ±4 pt autour du bord
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
    nom, teinte = bloc[0], bloc[5]
    for cote in ("gauche", "droite", "haut", "bas"):
        centres = {}
        for rendu, couleur in rendus.items():
            pris = [d for d, c in traversee(couleur, bloc, cote) if proche(c, teinte)]
            if not pris:
                refuser(f"{rendu} — « {nom} », côté {cote} : aucun filet sur le bord.")
                continue

            # 1 — le pixel juste à l'intérieur du bord porte le filet.
            if not any(0 < d < 0.5 for d in pris):
                refuser(f"{rendu} — « {nom} », côté {cote} : le bord, côté intérieur, "
                        f"n'a pas la couleur du filet. Le contenu passe-t-il dessus ?")

            # … et le filet fait son épaisseur. Un pixel vaut un demi-point, et le bord
            # tombe sur un pixel entier : l'épaisseur se mesure donc au demi-point
            # près, et le filet sain en donne exactement quatre pixels, des deux côtés.
            # Un seuil d'un demi-point laisserait passer un filet de 1,5 ou de 2,5 pt ;
            # celui-ci n'admet que 2.
            epaisseur = len(pris) / PX
            if abs(epaisseur - 2) > 0.25:
                refuser(f"{rendu} — « {nom} », côté {cote} : filet de {epaisseur} pt, 2 attendus.")

            # La distance du centre du filet au bord, vers l'intérieur.
            centres[rendu] = (min(pris) + max(pris)) / 2

        if len(centres) < 2:
            continue

        # 2 — au PDF, le filet est centré sur le bord.
        if abs(centres["PDF"]) > 0.25:
            refuser(f"PDF — « {nom} », côté {cote} : filet centré à {centres['PDF']:+.2f} pt "
                    f"du bord, 0 attendu (« centré sur le bord »).")

        # 2 — à l'écran, le filet est au même endroit qu'au PDF : Q4 est réparé, et un
        # écart d'une demi-épaisseur vers l'intérieur serait son retour.
        ecart = centres["écran"] - centres["PDF"]
        ecarts.append(ecart)
        if abs(ecart) > 0.25:
            refuser(f"écran — « {nom} », côté {cote} : filet à {ecart:+.2f} pt de celui du PDF "
                    f"(positif : rentré dans le bloc). 0 ± 0,25 attendu — à +1 pt, c'est Q4 qui revient.")

# On s'arrête ici en cas d'échec : la suite résumerait comme acquis ce qui vient de
# tomber.
if echec:
    sys.exit(1)

print(f"  1. douze côtés, deux rendus : le filet est sur le bord, côté intérieur, et fait 2 pt")
# L'écart est imprimé à chaque passage : c'est la mesure qui garde Q4 fermé.
etendue = (f"{min(ecarts):+.2f} pt" if max(ecarts) - min(ecarts) < 0.01
           else f"de {min(ecarts):+.2f} à {max(ecarts):+.2f} pt")
print(f"  2. PDF : filet centré sur le bord ; écran : à {etendue} du PDF (0 ± 0,25 admis)")

# 3 — le fond et le contenu, relevés là où rien d'autre ne passe.
JAUNE, VERT, MAGENTA, CIEL, BLANC = (255, 204, 0), (0, 160, 0), (255, 0, 255), (191, 227, 245), (255, 255, 255)
releves = [
    ("le fond du portrait, à gauche de l'image",  (120, 450), JAUNE),
    ("le fond du portrait, à droite de l'image",  (320, 450), JAUNE),
    ("le portrait lui-même",                       (220, 450), VERT),
    ("la photo",                                   (220, 190), MAGENTA),
    # Cadrée pour remplir, la photo carrée monte et descend de 30 pt hors de son bloc :
    # à 10 pt du bord, dehors, il ne doit rester que la page.
    ("au-dessus de la photo, ce qui déborde",      (220,  90), BLANC),
    ("au-dessous de la photo, ce qui déborde",     (220, 290), BLANC),
    ("le fond de la légende, à côté du mot",       (520, 145), CIEL),
]
for rendu, couleur in rendus.items():
    for quoi, (x, y), voulue in releves:
        obtenue = couleur(x * PX, y * PX)
        if not proche(obtenue, voulue, 12):
            refuser(f"{rendu} — {quoi} : {obtenue} en ({x}, {y}) pt, {voulue} attendu.")
if echec:
    sys.exit(1)
print("  3. fond jaune autour du portrait, fond de la légende, les deux images, et la photo "
      "découpée à son bloc, des deux côtés")
PY

# ------------------------------------------------------------------------- 4. filet

Scripts/comparer-rendus.sh "$document" 1 0.25

echo "C1 : fond et filet sur une image et sur un texte, à l'écran comme au PDF ; le filet passe sur le contenu."
