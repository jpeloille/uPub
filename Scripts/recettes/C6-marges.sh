#!/usr/bin/env bash
#
# Recette du poste C6 — marges intérieures par côté.
#
# « Un texte décalé de 12 pt à gauche seulement, à l'écran comme au PDF. » Elle était
# jouée à la main le 5 septembre 2026, en mesurant l'encre, avec un troisième bloc
# portant « 20 pt en haut seulement », puis jetée. La voici rejouable, sur six blocs
# du même texte en DejaVu Sans 14 pt :
#
#   · fer à gauche, sans marge          — le témoin du fer gauche ;
#   · fer à gauche, 12 pt à gauche      — le sujet ;
#   · fer à droite, sans marge          — le témoin du fer droit ;
#   · fer à droite, 12 pt à gauche      — le sujet, vu par l'autre bord ;
#   · fer à gauche, 20 pt en haut       — la marge du haut, comparée au premier témoin ;
#   · fer à droite, 12 pt à droite      — la marge droite, comparée au second.
#
# Ce qu'elle éprouve :
#
#   1. les marges s'écrivent et se relisent : réécrit par « --resave », chaque bloc
#      porte ses quatre marges, et zéro sur les côtés qu'on n'a pas touchés ;
#   2. au PDF, relevé par « pdftotext -bbox » au millième de point : 12 pt à gauche
#      mettent le fer gauche à X + 12 sans bouger ni le fer droit ni le haut ; 20 pt en
#      haut descendent le texte de 20 pt sans bouger le fer gauche ; 12 pt à droite
#      ramènent le fer droit de 12 pt sans bouger le haut ;
#   3. à l'écran comme au PDF, mesuré dans l'ENCRE : les mêmes déplacements, à 0,05 pt
#      près ;
#   4. l'écran et le PDF posent le texte au même endroit à 0,5 pt près — un pixel à
#      deux pixels par point.
#
# La marge du bas n'est pas éprouvée : sous un calage en haut, elle ne déplace rien.
#
# Le fer droit est ce qui distingue « quatre marges » d'« une marge » : une marge
# unique, appliquée des deux côtés, décalerait AUSSI le fer gauche de 12 pt. Seul un
# texte calé à droite dit si la marge de gauche a mordu sur la droite. C'est pourquoi
# le fer droit a son propre témoin, et son propre sujet.
#
# Pourquoi le centre de l'encre, et pas son bord : le bord se lit au pixel près, que le
# lissage déplace d'un cran selon la lettre ; le centre pondéré par la noirceur se lit
# au centième de pixel. À quatre pixels par point, 0,05 pt vaut un cinquième de pixel :
# seule une mesure de ce genre tient la tolérance que le plan demande. Et comme le
# témoin et le sujet portent le même texte, ce qui ne tient qu'aux lettres s'annule
# dans la différence.
#
set -euo pipefail
# Les comparaisons numériques doivent être insensibles à la locale : sous une locale
# française, awk et printf écrivent « 12,00 » et les tests suivants comparent alors une
# chaîne.
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile : le banc n'est compilé ni par « dotnet test » ni par le contrôle, et la
# moitié « écran » mesurerait sinon un banc périmé.
if ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

for outil in "$binaire" "$banc"; do
    if [ ! -x "$outil" ]; then
        echo "ÉCHEC : binaire introuvable après compilation ($outil)." >&2
        exit 1
    fi
done

# Six blocs de 300 × 40 pt, en X = 60, l'un sous l'autre à 60 pt d'intervalle. Une
# seule ligne chacun : le bloc est assez large pour que rien ne se coupe, avec ou sans
# marge, et une coupure déplacée ne peut pas se faire passer pour un décalage.
python3 - "$travail/marges.mypub" <<'PY'
import json, sys

def bloc(nom, y, fer, gauche, haut=0, droite=0):
    return {"$kind": "text", "Name": nom, "X": 60, "Y": y, "Width": 300, "Height": 40,
            "Text": "Marge interieure", "FontFamily": "DejaVu Sans", "FontSize": 14,
            "Foreground": "#FF000000", "Align": fer, "LineHeight": 1.0,
            "PaddingLeft": gauche, "PaddingTop": haut, "PaddingRight": droite, "PaddingBottom": 0,
            "VerticalAlign": "Top"}

document = {"FormatVersion": 4, "Title": "Recette C6 — marges intérieures",
            "Pages": [{"Name": "Page 1", "Frames": [
                bloc("Gauche, temoin", 60, "Left", 0),
                bloc("Gauche, 12 pt", 120, "Left", 12),
                bloc("Droite, temoin", 180, "Right", 0),
                bloc("Droite, 12 pt", 240, "Right", 12),
                bloc("Gauche, 20 pt en haut", 300, "Left", 0, haut=20),
                bloc("Droite, 12 pt a droite", 360, "Right", 0, droite=12)]}]}
json.dump(document, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY

# -------------------------------------------------------------------------------- 1

"$binaire" "$travail/marges.mypub" --resave "$travail/relu.mypub" > /dev/null
python3 - "$travail/relu.mypub" <<'PY'
import json, sys

blocs = {b["Name"]: b for b in json.load(open(sys.argv[1], encoding="utf-8"))["Pages"][0]["Frames"]}
attendues = {"Gauche, 12 pt": (12, 0, 0, 0), "Droite, 12 pt": (12, 0, 0, 0),
             "Gauche, 20 pt en haut": (0, 20, 0, 0), "Droite, 12 pt a droite": (0, 0, 12, 0)}
for nom, attendu in attendues.items():
    marges = tuple(blocs[nom].get(cote) for cote in ("PaddingLeft", "PaddingTop", "PaddingRight", "PaddingBottom"))
    if marges != attendu:
        sys.exit(f"ÉCHEC : « {nom} » se réécrit avec les marges {marges}, et non {attendu}.")
print("  1. réécrit, chaque bloc garde ses quatre marges (gauche, haut, droite, bas) :"
      " (12, 0, 0, 0) deux fois, (0, 20, 0, 0) et (0, 0, 12, 0)")
PY

"$binaire" "$travail/marges.mypub" --export "$travail/sortie.pdf" \
    > /dev/null 2> "$travail/avertissements.txt"
if [ -s "$travail/avertissements.txt" ]; then
    echo "ÉCHEC : l'export avertit, et la mesure ne porterait plus sur ce qu'on croit." >&2
    cat "$travail/avertissements.txt" >&2
    exit 1
fi

# -------------------------------------------------------------------------------- 2

# « -bbox » rend chaque mot à l'origine du crayon et non au bord de l'encre : le fer
# gauche s'y lit donc exactement, sans approche de lettre.
pdftotext -bbox "$travail/sortie.pdf" "$travail/mots.xml"

python3 - "$travail/mots.xml" <<'PY'
import re, sys

mots = [tuple(map(float, m)) for m in re.findall(
    r'<word xMin="([0-9.]+)" yMin="([0-9.]+)" xMax="([0-9.]+)" yMax="[0-9.]+">', open(sys.argv[1]).read())]

def ligne(haut):
    """Le bord gauche, le haut et le bord droit de la ligne posée dans ce bloc."""
    dedans = [m for m in mots if haut <= m[1] < haut + 40]
    if len(dedans) != 2:
        sys.exit(f"ÉCHEC : {len(dedans)} mots relevés dans le bloc posé en Y = {haut}, 2 attendus.")
    return min(m[0] for m in dedans), min(m[1] for m in dedans), max(m[2] for m in dedans)

echec = False
def verifier(libelle, mesure, attendu):
    global echec
    print(f"  2. PDF   — {libelle:44} {mesure:9.3f} pt (attendu {attendu:g})")
    if abs(mesure - attendu) > 0.05:
        print(f"ÉCHEC : au PDF, {libelle} — {mesure:.3f} mesuré, {attendu:g} attendu.", file=sys.stderr)
        echec = True

gt, g12, dt, d12 = ligne(60), ligne(120), ligne(180), ligne(240)
h20, dd12 = ligne(300), ligne(360)
# Le fer gauche se lit à sa place absolue : X + marge, puisque le crayon part de là.
verifier("fer gauche du témoin, bloc en X = 60", gt[0], 60)
verifier("fer gauche avec 12 pt de marge", g12[0], 72)
verifier("fer gauche avec 20 pt en haut", h20[0], 60)
# Le fer droit et le haut se lisent par différence : pdftotext les déduit des chasses
# qu'il lit dans la police, et ce sont celles du témoin qui servent de zéro.
verifier("fer droit : déplacement dû à la marge gauche", d12[2] - dt[2], 0)
verifier("haut : déplacement dû à la marge gauche", g12[1] - 120 - (gt[1] - 60), 0)
verifier("haut : déplacement dû à 20 pt en haut", h20[1] - 300 - (gt[1] - 60), 20)
verifier("fer droit : déplacement dû à 12 pt à droite", dd12[2] - dt[2], -12)
verifier("haut : déplacement dû à la marge droite", dd12[1] - 360 - (dt[1] - 180), 0)
sys.exit(1 if echec else 0)
PY

# ---------------------------------------------------------------------- 3 et 4

pp=4
"$banc" rendre "$travail/marges.mypub" --sortie "$travail/ecran.png" \
        --page 1 --pixels-par-point "$pp" --sans-reperes > /dev/null
pdftoppm -r "$((72 * pp))" -f 1 -l 1 -png -singlefile "$travail/sortie.pdf" "$travail/pdf"

# En niveaux de gris, au format le plus simple qui soit : « P5 », un en-tête de trois
# nombres puis un octet par pixel. Python le lit sans bibliothèque d'images.
magick "$travail/ecran.png" -colorspace gray "$travail/ecran.pgm"
magick "$travail/pdf.png" -colorspace gray "$travail/pdf.pgm"

python3 - "$travail/ecran.pgm" "$travail/pdf.pgm" "$pp" <<'PY'
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

pp = int(sys.argv[3])

def centre(image, haut):
    """Le centre de l'encre du bloc posé en Y = haut, en points de page, pondéré par
    la noirceur. La fenêtre déborde le bloc de 5 pt de chaque côté : un texte poussé
    hors de son bloc s'y verrait encore, au lieu de sortir de la mesure."""
    largeur, _, pixels = image
    total = sx = sy = 0
    for y in range(haut * pp, (haut + 40) * pp):
        rangee = y * largeur
        for x in range(55 * pp, 365 * pp):
            p = 255 - pixels[rangee + x]
            if p:
                total += p
                sx += p * (x + 0.5)
                sy += p * (y + 0.5)
    if total == 0:
        sys.exit(f"ÉCHEC : aucune encre dans le bloc posé en Y = {haut}.")
    return sx / total / pp, sy / total / pp - haut

images = {"écran": lire(sys.argv[1]), "PDF": lire(sys.argv[2])}
centres = {cote: [centre(image, haut) for haut in (60, 120, 180, 240, 300, 360)]
           for cote, image in images.items()}

echec = False
def verifier(libelle, mesure, attendu, tolerance):
    global echec
    print(f"  {libelle:58} {mesure:8.3f} pt")
    if abs(mesure - attendu) > tolerance:
        print(f"ÉCHEC : {libelle} — {mesure:.3f} pt mesuré, {attendu:g} attendu"
              f" (tolérance {tolerance:g}).", file=sys.stderr)
        echec = True

for cote, (gt, g12, dt, d12, h20, dd12) in centres.items():
    verifier(f"3. {cote:5} — 12 pt à gauche : fer gauche, déplacé de", g12[0] - gt[0], 12, 0.05)
    verifier(f"3. {cote:5} — 12 pt à gauche : fer droit, déplacé de", d12[0] - dt[0], 0, 0.05)
    verifier(f"3. {cote:5} — 12 pt à gauche : haut, déplacé de", g12[1] - gt[1], 0, 0.05)
    verifier(f"3. {cote:5} — 20 pt en haut : haut, déplacé de", h20[1] - gt[1], 20, 0.05)
    verifier(f"3. {cote:5} — 20 pt en haut : fer gauche, déplacé de", h20[0] - gt[0], 0, 0.05)
    verifier(f"3. {cote:5} — 12 pt à droite : fer droit, déplacé de", dd12[0] - dt[0], -12, 0.05)
    verifier(f"3. {cote:5} — 12 pt à droite : haut, déplacé de", dd12[1] - dt[1], 0, 0.05)

for i, nom in enumerate(("gauche, témoin", "gauche, 12 pt", "droite, témoin", "droite, 12 pt",
                         "gauche, 20 pt en haut", "droite, 12 pt à droite")):
    (ex, ey), (px, py) = centres["écran"][i], centres["PDF"][i]
    verifier(f"4. écran/PDF — bloc {nom}, écart", max(abs(ex - px), abs(ey - py)), 0, 0.5)

sys.exit(1 if echec else 0)
PY

echo "C6 : les marges gauche, haute et droite déplacent chacune leur bord et lui seul, à l'écran comme au PDF."
