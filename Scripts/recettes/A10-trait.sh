#!/usr/bin/env bash
#
# Recette du poste A10 — l'objet trait, et la flèche, avec l'outil Ligne du rail.
#
# Le trait se définit par ses deux extrémités, et non par un cadre : c'est le piège que le
# plan consignait — la taille minimale d'un bloc vaut 2 mm, et un trait horizontal a une
# hauteur nulle. Il est une forme couchée, sans hauteur, que sa rotation oriente ; ses
# pointes de flèche sont facultatives, une à chaque bout. Quatre temps, dont les quatre
# clauses du plan.
#
#   1. LE FICHIER. Un document de recette, écrit à la main, porte six traits : à plat,
#      debout, tracé de droite à gauche, oblique en tirets, une flèche à deux pointes, et
#      un trait dans un groupe, collé à son bord. Relu puis réécrit par l'application, il
#      garde « "Shape": "Line" » et ses pointes ; réécrit une seconde fois, il ne change
#      plus d'un caractère (clause 1). Les Samples et les Templates, réenregistrés par le
#      poste, ne portent aucune forme qui soit un trait déguisé.
#
#   2. LE PDF. Le flux de la page, décompressé, trace chaque trait d'un coup de crayon —
#      « x y m x y l S », aux coordonnées de ses extrémités, raccourci sous ses pointes —
#      et ne porte aucun rectangle (« re ») : le trait n'est plus un rectangle sans
#      hauteur (clause 2). Chaque pointe est un triangle plein, « m l l h f ».
#
#   3. L'ÉCRAN ET LE PDF. Le banc rend la page à deux pixels par point, comme pdftoppm à
#      144 ppp : au milieu de chaque trait, l'encre a l'épaisseur du modèle et le même
#      axe des deux côtés, à 0,25 pt près ; les bouts carrés s'arrêtent aux extrémités ;
#      les pointes de la flèche se voient au même endroit ; celle du trait groupé n'est
#      pas rognée par la découpe du groupe ; « comparer-rendus.sh » passe sous 0,3 %.
#
#   4. L'IMPORT ET L'INTERFACE.
#      a. L'import des « Fiches limitations » rend 5 traits et 0 rectangle de
#         substitution — aucune forme rectangle sans largeur ou sans hauteur —, chacun de
#         la page entière, à plat ; la recette R5 compte les traits à part des cadres
#         (clause 4).
#      b. Les sources : le rail porte « Ligne (L) » entre Ellipse et la loupe, avec
#         l'icône de l'apparence — la clause « chaque icône sert » de la recette I1 s'éveille
#         pour elle quand ce poste est clos ; L se range avec les lettres seules de la
#         fenêtre et au tableau du README ; « Trait » au menu Insertion ; ce que le poste
#         ajoute au modèle de vue est dans son fichier partiel (règle 10), et les vues qu'il
#         écrit sont à la liste de la recette I1.
#      c. Les tests du noyau (« TraitTests ») : extrémités, angle, contrainte à 0, 45 ou
#         90°, survol à moins de 3 pt de l'encre et pas au-delà (clause 3), aller-retour,
#         pointes, découpe d'un groupe, groupe étiré — repris à la relecture du
#         26 octobre 2026 —, flux PDF.
#      d. Les tests d'interface « poste=A10 », sur la vraie fenêtre : l'outil et sa touche,
#         le tracé en un pas et Maj, le clic qui pose à plat, le menu, le survol au
#         pointeur (clause 3), les deux poignées d'extrémité sans rotation, Maj sur une
#         extrémité, le clic qui ne change rien, le dock — extrémités à la place du cadre,
#         rien de touché en montrant le trait, pointe cochée en un pas, épaisseur du
#         contour et angle —, l'encre à l'écran, la vignette de la page.
#
# Ce qu'elle ne prouve PAS :
#   - qu'un .pub à flèches s'importe avec ses pointes : libmspub n'en transmet aucune — son
#     relevé n'a pas de clé « draw:marker-* » —, et les deux documents ATR n'en ont pas ;
#   - l'allure d'un trait dans la vignette au-delà du schéma : il y a son tracé et ses
#     pointes, sans les tirets.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# ------------------------------------------------------------------------ 1. le fichier

# Les traits sont écrits par leur cadre couché : X et Y au début d'un trait à plat, la
# largeur pour longueur, aucune hauteur, la rotation pour angle autour du milieu. Le
# « debout » va de (480, 100) à (480, 300) : milieu (480, 200), longueur 200, 90°.
document="$travail/traits.mypub"
cat > "$document" <<'JSON'
{
  "FormatVersion": 5,
  "Title": "Recette — traits",
  "Pages": [
    {
      "Name": "Page 1",
      "Frames": [
        { "$kind": "shape", "Name": "À plat", "X": 100, "Y": 100, "Width": 300, "Height": 0,
          "Fill": null, "Stroke": "#FF0000FF", "StrokeThickness": 4, "Shape": "Line" },
        { "$kind": "shape", "Name": "Debout", "X": 380, "Y": 200, "Width": 200, "Height": 0, "Rotation": 90,
          "Fill": null, "Stroke": "#FF0000FF", "StrokeThickness": 3, "Shape": "Line" },
        { "$kind": "shape", "Name": "À rebours", "X": 100, "Y": 380, "Width": 300, "Height": 0, "Rotation": 180,
          "Fill": null, "Stroke": "#FF0000FF", "StrokeThickness": 2, "Shape": "Line", "EndArrow": true },
        { "$kind": "shape", "Name": "Flèche", "X": 100, "Y": 250, "Width": 300, "Height": 0,
          "Fill": null, "Stroke": "#FF0000FF", "StrokeThickness": 2, "Shape": "Line",
          "StartArrow": true, "EndArrow": true },
        { "$kind": "shape", "Name": "Oblique", "X": 100, "Y": 520, "Width": 300, "Height": 0, "Rotation": 30,
          "Fill": null, "Stroke": "#FF0000FF", "StrokeThickness": 1.5, "StrokeStyle": "Dashed", "Shape": "Line" },
        { "$kind": "group", "Name": "Groupe", "X": 100, "Y": 680, "Width": 300, "Height": 40,
          "Children": [
            { "$kind": "shape", "Name": "Au bord", "X": 0, "Y": 0, "Width": 300, "Height": 0,
              "Fill": null, "Stroke": "#FF0000FF", "StrokeThickness": 2, "Shape": "Line", "EndArrow": true }
          ] }
      ]
    }
  ]
}
JSON

"$binaire" "$document" --resave "$travail/relu.mypub" > /dev/null
"$binaire" "$travail/relu.mypub" --resave "$travail/relu-bis.mypub" > /dev/null
if ! diff -u "$travail/relu.mypub" "$travail/relu-bis.mypub" > "$travail/ecart.diff"; then
    head -30 "$travail/ecart.diff" >&2
    echo "ÉCHEC : relu puis réécrit, le document des traits n'est plus le même." >&2
    exit 1
fi

python3 - "$travail/relu.mypub" "$racine" <<'PY'
import json, pathlib, sys

relu = json.load(open(sys.argv[1], encoding="utf-8"))
racine = pathlib.Path(sys.argv[2])
echecs = []

def tous(blocs):
    for bloc in blocs:
        yield bloc
        yield from tous(bloc.get("Children", []))

traits = [b for b in tous(relu["Pages"][0]["Frames"]) if b["$kind"] == "shape"]
attendus = {"À plat": (False, False), "Debout": (False, False), "À rebours": (False, True),
            "Flèche": (True, True), "Oblique": (False, False), "Au bord": (False, True)}
for trait in traits:
    if trait.get("Shape") != "Line":
        echecs.append(f"« {trait['Name']} » a perdu \"Shape\": \"Line\" ({trait.get('Shape')}).")
    if (trait.get("StartArrow"), trait.get("EndArrow")) != attendus.get(trait["Name"]):
        echecs.append(f"« {trait['Name']} » a perdu ses pointes : {trait.get('StartArrow')}, {trait.get('EndArrow')}.")
    if trait.get("Height") != 0:
        echecs.append(f"« {trait['Name']} » a reçu une hauteur : {trait.get('Height')}.")
if len(traits) != 6:
    echecs.append(f"{len(traits)} traits relus, 6 attendus.")

# Les Samples et les Templates, réenregistrés par le poste : chaque forme porte ses deux
# pointes, et aucune n'est un trait déguisé en rectangle sans largeur ou sans hauteur.
formes = 0
for chemin in sorted([*racine.glob("Samples/*.mypub"), *racine.glob("MyPublisher/Templates/*.mypub")]):
    for page in json.load(open(chemin, encoding="utf-8"))["Pages"]:
        for bloc in tous(page["Frames"]):
            if bloc["$kind"] != "shape":
                continue
            formes += 1
            if "StartArrow" not in bloc or "EndArrow" not in bloc:
                echecs.append(f"{chemin.name} : « {bloc.get('Name')} » n'a pas été réenregistré (pointes absentes).")
            if bloc.get("Shape") != "Line" and (bloc["Width"] == 0 or bloc["Height"] == 0):
                echecs.append(f"{chemin.name} : « {bloc.get('Name')} » est un rectangle sans épaisseur.")

if echecs:
    for e in echecs:
        print(f"    ÉCHEC : {e}", file=sys.stderr)
    sys.exit(1)
print(f"  1. six traits relus et réécrits au caractère près, « Shape »: « Line » et leurs pointes gardés ; "
      f"{formes} formes des Samples et des Templates réenregistrées, aucun trait déguisé")
PY

# ------------------------------------------------------------------------ 2. le PDF

"$binaire" "$travail/relu.mypub" --export "$travail/traits.pdf" > /dev/null 2> "$travail/export.txt"
if [ -s "$travail/export.txt" ]; then
    cat "$travail/export.txt" >&2
    echo "ÉCHEC : l'export des traits s'est plaint." >&2
    exit 1
fi

python3 - "$travail/traits.pdf" <<'PY'
import math, re, sys, zlib

# Le flux de la page, décompressé : PDFsharp le comprime, et ce qui suit son « stream »
# n'est que ce que zlib reconnaît.
donnees = open(sys.argv[1], "rb").read()
flux = ""
for m in re.finditer(rb"stream\r?\n", donnees):
    fin = donnees.find(b"endstream", m.end())
    try:
        flux += zlib.decompressobj().decompress(donnees[m.end():fin]).decode("latin-1") + "\n"
    except zlib.error:
        pass

# Un interprète de poche : la pile « q / Q », les matrices « cm », et chaque chemin tracé
# (« S ») ou rempli (« f »), ses points portés en coordonnées de page. PDFsharp écrit un
# bloc tourné ou groupé sous une matrice, et retourne lui-même l'axe Y : on ne compare
# donc pas des nombres du flux, mais l'endroit où ils tombent sur la page.
H = 841.89
pile, ctm, chemin = [], (1, 0, 0, 1, 0, 0), []
traces, remplis = [], []

def porter(x, y, m):
    a, b, c, d, e, f = m
    return (a * x + c * y + e, b * x + d * y + f)

def composer(m, n):
    a, b, c, d, e, f = m
    x0, y0 = porter(0, 0, n)
    x1, y1 = porter(1, 0, n)
    x2, y2 = porter(0, 1, n)
    xa, ya = porter(a, b, (x1 - x0, y1 - y0, x2 - x0, y2 - y0, 0, 0))
    xc, yc = porter(c, d, (x1 - x0, y1 - y0, x2 - x0, y2 - y0, 0, 0))
    xe, ye = porter(e, f, n)
    return (xa, ya, xc, yc, xe, ye)

nombres = []
for jeton in re.findall(r"\[[^\]]*\]|/\w+|[-\d.]+|[A-Za-z*]+", flux):
    if re.fullmatch(r"-?[\d.]+", jeton):
        nombres.append(float(jeton))
        continue
    if jeton == "q":
        pile.append(ctm)
    elif jeton == "Q":
        ctm = pile.pop()
    elif jeton == "cm":
        ctm = composer(tuple(nombres[-6:]), ctm)
    elif jeton in ("m", "l"):
        x, y = porter(nombres[-2], nombres[-1], ctm)
        if jeton == "m":
            chemin = []
        chemin.append((x, H - y))
    elif jeton == "S":
        traces.append(chemin)
        chemin = []
    elif jeton == "f":
        remplis.append(chemin)
        chemin = []
    elif jeton == "n":
        chemin = []
    nombres = []

echecs = []

def proche(p, q):
    return abs(p[0] - q[0]) < 0.01 and abs(p[1] - q[1]) < 0.01

# Chaque trait d'un seul coup de crayon, d'une extrémité à l'autre en coordonnées de page.
# Le fût s'arrête sous ses pointes, à une demi-pointe : 3 pt pour un trait de 2 pt, dont
# la pointe mesure 6 × 3. « À rebours » part de (400, 380) : sa fin, pointée, est à gauche.
c, s = math.cos(math.radians(30)), math.sin(math.radians(30))
attendus = {
    "À plat": ((100, 100), (400, 100)),
    "Debout": ((480, 100), (480, 300)),
    "À rebours": ((400, 380), (103, 380)),
    "Flèche": ((103, 250), (397, 250)),
    "Oblique": ((250 - 150 * c, 520 - 150 * s), (250 + 150 * c, 520 + 150 * s)),
    "Au bord": ((100, 680), (397, 680)),
}
for nom, (debut, fin) in attendus.items():
    # Dans l'ordre : le fût part du début du trait, comme le modèle le dessine.
    if not any(len(t) == 2 and proche(t[0], debut) and proche(t[1], fin) for t in traces):
        echecs.append(f"« {nom} » : aucun tracé « m … l S » de {debut} à {fin} sur la page.")
if len(traces) != 6 or any(len(t) != 2 for t in traces):
    echecs.append(f"{len(traces)} tracés dans le flux, 6 coups de crayon de deux points attendus : {traces}")
if not re.search(r"\[[\d.\s]+\]\s*0\s+d", flux):
    echecs.append("« Oblique » : aucun motif de tirets (« [ … ] 0 d ») dans le flux.")
if re.search(r"\bre\b", flux):
    echecs.append("le flux porte un rectangle (« re ») : un trait y est encore un cadre.")

# Les pointes : des triangles pleins, la pointe sur l'extrémité.
pointes = [r for r in remplis if len(r) == 3]
for nom, bout in (("Flèche", (100, 250)), ("Flèche", (400, 250)), ("À rebours", (100, 380)), ("Au bord", (400, 680))):
    if not any(proche(r[0], bout) for r in pointes):
        echecs.append(f"« {nom} » : aucune pointe pleine sur {bout}.")
if len(pointes) != 4 or len(remplis) != 4:
    echecs.append(f"{len(pointes)} pointes pleines dans le flux ({len(remplis)} remplissages), 4 attendues.")

if echecs:
    for e in echecs:
        print(f"    ÉCHEC : {e}", file=sys.stderr)
    sys.exit(1)
print(f"  2. au PDF, six tracés « m … l S » d'une extrémité à l'autre, aucun rectangle ; "
      f"{len(pointes)} pointes pleines « m l l h f », chacune sur son extrémité")
PY

# ------------------------------------------------------------- 3. l'écran et le PDF

pdftoppm -r 144 -f 1 -l 1 -png -singlefile "$travail/traits.pdf" "$travail/pdf"
"$banc" rendre "$travail/relu.mypub" --sortie "$travail/ecran.png" \
        --page 1 --pixels-par-point 2 --sans-reperes > /dev/null
for rendu in pdf ecran; do
    magick "$travail/$rendu.png" -background white -alpha remove -alpha off "$travail/$rendu.ppm"
done

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

    def bleu(x, y):
        if not (0 <= x < largeur and 0 <= y < hauteur):
            return False
        k = (y * largeur + x) * 3
        r, v, b = pixels[k:k + 3]
        return b >= 195 and r <= 60 and v <= 60
    return bleu

PX = 2
rendus = {"PDF": lire(sys.argv[1]), "écran": lire(sys.argv[2])}
echecs = []

def traversee(bleu, x, y, vertical):
    """Les pixels encrés sur ±8 pt autour de (x, y), en travers du trait ; leur centre et leur nombre en points."""
    pris = []
    for d in range(-16, 17):
        p = (int(x * PX), int(y * PX) + d) if not vertical else (int(x * PX) + d, int(y * PX))
        if bleu(*p):
            pris.append((d + 0.5) / PX)
    return pris

# (nom, x, y du milieu, en travers vertical ?, épaisseur)
mesures = [
    ("À plat", 250, 100, False, 4),
    ("Debout", 480, 200, True, 3),
    ("À rebours", 250, 380, False, 2),
    ("Flèche", 250, 250, False, 2),
    ("Au bord", 250, 680, False, 2),
]
ecarts = []
for nom, x, y, vertical, epaisseur in mesures:
    centres = {}
    for rendu, bleu in rendus.items():
        pris = traversee(bleu, x, y, vertical)
        if not pris:
            echecs.append(f"{rendu} — « {nom} » : aucune encre au milieu du trait.")
            continue
        if abs(len(pris) / PX - epaisseur) > 0.25:
            echecs.append(f"{rendu} — « {nom} » : {len(pris) / PX} pt d'encre, {epaisseur} attendus.")
        centres[rendu] = (min(pris) + max(pris)) / 2
    if len(centres) == 2:
        if abs(centres["PDF"]) > 0.25:
            echecs.append(f"PDF — « {nom} » : axe à {centres['PDF']:+.2f} pt de sa place.")
        ecarts.append(abs(centres["écran"] - centres["PDF"]))
        if ecarts[-1] > 0.25:
            echecs.append(f"écran — « {nom} » : axe à {centres['écran'] - centres['PDF']:+.2f} pt de celui du PDF.")

# Les bouts carrés s'arrêtent aux extrémités, à un pixel près : rien à 1 pt au-delà, de
# l'encre à 1 pt en deçà — des deux côtés, sur le trait à plat et sur le trait debout.
for rendu, bleu in rendus.items():
    for nom, dedans, dehors in (("À plat", (101, 100), (99, 100)), ("À plat", (399, 100), (401, 100)),
                                ("Debout", (480, 101), (480, 99)), ("Debout", (480, 299), (480, 301))):
        if not bleu(dedans[0] * PX, dedans[1] * PX) or bleu(dehors[0] * PX, dehors[1] * PX):
            echecs.append(f"{rendu} — « {nom} » : le bout ne s'arrête pas sur l'extrémité ({dedans} / {dehors}).")

    # Les pointes : à 4 pt de la pointe, une pointe de 6 × 3 couvre ±2 pt autour de l'axe,
    # là où le trait de 2 pt n'en couvre qu'un. Le début de « À rebours », lui, est nu.
    for nom, x, y in (("Flèche", 104, 250), ("Flèche", 396, 250), ("À rebours", 104, 380), ("Au bord", 396, 680)):
        for dy in (-1.7, 1.7):
            if not bleu(int(x * PX), int((y + dy) * PX)):
                echecs.append(f"{rendu} — « {nom} » : pas de pointe en ({x}, {y + dy}).")
    if bleu(int(396 * PX), int(378.3 * PX)):
        echecs.append(f"{rendu} — « À rebours » : une pointe à son début, qui n'en a pas.")

if echecs:
    for e in echecs:
        print(f"    ÉCHEC : {e}", file=sys.stderr)
    sys.exit(1)
print(f"  3. écran et PDF : épaisseur et axe de cinq traits à {max(ecarts):.2f} pt au plus (0,25 admis), "
      "bouts sur les extrémités, pointes au même endroit — celle du trait groupé, au bord, non rognée")
PY

# Le seuil se calcule comme celui de Q4 : un trait d'épaisseur e décalé d'une
# demi-épaisseur laisse deux bandes de e/2 sur sa longueur, soit 2 × e/2 × 4 = 4e pixels
# par point de longueur. Les six traits portent 1 900 pt de long, d'épaisseur moyenne 2,3 :
# un décalage pareil coûterait près de 17 500 pixels, 0,87 % de la page. Relevé au poste :
# « 0.00 % » au centième près — le lissage des bords du trait oblique et des pointes ne
# pèse pas un centième. Le seuil à 0,3 % laisse sa marge au lissage d'une autre machine,
# sous le tiers du défaut.
Scripts/comparer-rendus.sh "$travail/relu.mypub" 1 0.3

# ------------------------------------------------------------- 4. import et interface

# a. Les Fiches : 5 traits, 0 rectangle de substitution.
outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$outil" ] || [ ! -x "$outil" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable (sudo apt install libmspub-tools, ou Scripts/outils-pub.sh)." >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$outil"
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi
"$binaire" "MyPublisher/Books/Fiches limitations et procédures d'urgences.pub" --resave "$travail/fiches.mypub" \
    > /dev/null 2>&1
python3 - "$travail/fiches.mypub" "$racine" <<'PY'
import json, pathlib, re, sys

document = json.load(open(sys.argv[1], encoding="utf-8"))
racine = pathlib.Path(sys.argv[2])
echecs = []

formes = [(i + 1, b) for i, page in enumerate(document["Pages"]) for b in page["Frames"] if b["$kind"] == "shape"]
traits = [(p, b) for p, b in formes if b.get("Shape") == "Line"]
substituts = [(p, b) for p, b in formes if b.get("Shape") != "Line" and (b["Width"] == 0 or b["Height"] == 0)]
if len(traits) != 5:
    echecs.append(f"l'import des Fiches rend {len(traits)} trait(s), 5 attendus.")
if substituts:
    echecs.append(f"l'import des Fiches rend {len(substituts)} rectangle(s) de substitution, 0 attendu.")
for page, trait in traits:
    # Le trait pointillé du pied de chaque page, de -0,2187 in à 8,4695 in : 625,55 pt.
    if trait["Rotation"] != 0 or trait["Height"] != 0 or abs(trait["Width"] - 625.55) > 0.01:
        echecs.append(f"page {page} : le trait n'est pas à plat sur la largeur de la page "
                      f"({trait['Width']} × {trait['Height']}, {trait['Rotation']}°).")
    if trait["StrokeStyle"] != "Dashed" or trait["Fill"] is not None:
        echecs.append(f"page {page} : le trait a perdu ses tirets, ou porte un fond.")

r5 = (racine / "Scripts/recettes/R5-import-pub.sh").read_text(encoding="utf-8")
if '"dont traits":' not in r5 or 'source["drawPath"],         compte["trait"]' not in r5:
    echecs.append("la recette R5 ne compte plus les traits à part des cadres.")

if echecs:
    for e in echecs:
        print(f"    ÉCHEC : {e}", file=sys.stderr)
    sys.exit(1)
print("    a. les Fiches : 5 traits, un par page, à plat et en tirets, et 0 rectangle de substitution ; "
      "R5 compte les traits à part")
PY

# b. Les sources.
python3 - "$racine" <<'PY'
import pathlib, re, sys
import xml.etree.ElementTree as ET

racine = pathlib.Path(sys.argv[1])
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
fenetre_axaml = (racine / "MyPublisher/Views/MainWindow.axaml").read_text(encoding="utf-8")
fenetre = ET.fromstring(fenetre_axaml)

rail = next(e for e in fenetre.iter() if e.get(X + "Name") == "RailDOutils")
outils = [e.get("{https://github.com/avaloniaui}ToolTip.Tip") or e.get("ToolTip.Tip")
          for e in rail.iter() if e.tag.endswith("Button")]
exige(outils[-3:] == ["Ellipse (E)", "Ligne (L)", "Zoom (Z)"],
      f"le rail ne porte pas Ligne (L) entre Ellipse et la loupe : {outils}")
ligne = next((e for e in rail.iter() if e.get(X + "Name") == "OutilLigne"), None)
exige(ligne is not None and ligne.get("Content") == "{StaticResource IconeLigne}"
      and "ConverterParameter=Line" in (ligne.get("IsChecked") or ""),
      "l'outil Ligne n'a pas l'icône de l'apparence, ou n'arme pas l'outil Line.")
exige(len(re.findall(r'\{StaticResource IconeLigne\}', fenetre_axaml)) == 1, "l'icône de la ligne sert plus d'une fois.")
exige(re.search(r'<MenuItem Header="Tr_ait" Command="\{Binding AddLineCommand\}"/>', fenetre_axaml) is not None,
      "le menu Insertion n'a pas « Trait ».")

code = (racine / "MyPublisher/Views/MainWindow.axaml.cs").read_text(encoding="utf-8")
lettres = code[code.index("if (e.KeyModifiers == KeyModifiers.None)"):]
exige(re.search(r"case Key\.L: vm\.ActiveTool = EditorTool\.Line;", lettres) is not None,
      "L n'arme pas l'outil Line parmi les lettres seules de la fenêtre.")
exige(code.index("ConsumesTyping(FocusManager") < code.index("case Key.L:"),
      "L est lue avant le renvoi des frappes à une saisie.")
exige("Key.L" not in fenetre_axaml, "L est aussi déclarée en KeyBinding (règle 9).")

readme = (racine / "README.md").read_text(encoding="utf-8")
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M).group(1)
exige(len(re.findall(r"`L`", tableau)) == 1 and "ligne" in tableau, "le tableau du README ne dit pas L, ou la dit deux fois.")
exige(re.search(r"`Maj` en traçant un trait", tableau) is not None, "le tableau du README ne dit pas Maj sur un trait.")

partiel = racine / "MyPublisher/ViewModels/MainWindowViewModel.Trait.cs"
exige(partiel.exists() and "TracerUnTrait" in partiel.read_text(encoding="utf-8"),
      "ce que le poste ajoute au modèle de vue n'est pas dans son fichier partiel.")
commun = (racine / "MyPublisher/ViewModels/MainWindowViewModel.cs").read_text(encoding="utf-8")
exige("PoserLesExtremites" not in commun and "DebutDuTrait" not in commun,
      "le modèle de vue commun porte du code du trait (règle 10).")

recette_i1 = (racine / "Scripts/recettes/I1-apparence.sh").read_text(encoding="utf-8")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
for vue in ("MyPublisher/Views/MainWindow.axaml", "MyPublisher/Views/PageCanvas.axaml",
            "MyPublisher/Views/PageCanvas.axaml.cs", "MyPublisher/Views/TraitBloc.cs",
            "MyPublisher/Views/PageCanvas.Trait.cs"):
    exige(vue in liste, f"{vue} n'est pas dans la liste des vues de la phase de la recette I1.")
exige(re.search(r'"IconeLigne\s+A10\s+bouton:Ligne \(L\)"', recette_i1) is not None,
      "la table des icônes de la recette I1 n'attribue plus IconeLigne à A10.")

if echecs:
    for e in echecs:
        print(f"    ÉCHEC : {e}", file=sys.stderr)
    sys.exit(1)
print("    b. le rail, sa touche et le README, le menu Insertion, le fichier partiel, la liste de la recette I1")
PY

# c. Les tests du noyau.
if ! dotnet test MyPublisher.Tests/MyPublisher.Tests.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "FullyQualifiedName~MyPublisher.Tests.TraitTests." > "$travail/noyau.txt" 2>&1; then
    grep -E "\[FAIL\]|Assert" "$travail/noyau.txt" | sed 's/^/    /' >&2 || true
    echo "ÉCHEC : les tests du noyau du trait ne passent pas." >&2
    exit 1
fi
noyau="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/noyau.txt" | head -1)"
if [ "${noyau:-0}" -lt 28 ]; then
    echo "ÉCHEC : 28 tests du noyau attendus sous TraitTests, ${noyau:-0} joués." >&2
    exit 1
fi
echo "    c. le noyau : extrémités, angle, Maj, survol à 3 pt, aller-retour, pointes, découpe, groupe étiré, flux PDF ($noyau tests)"

# d. Les tests d'interface.
attendus=13
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=A10" > "$travail/interface.txt" 2>&1; then
    grep -E "\[FAIL\]|Assert" "$travail/interface.txt" | sed 's/^/    /' >&2 || true
    tail -30 "$travail/interface.txt" | sed 's/^/    /' >&2
    echo "ÉCHEC : les tests d'interface sous « poste=A10 » ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/interface.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=A10 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "    d. l'interface : outil, tracé et Maj, menu, survol, poignées d'extrémité, dock, encre, vignette ($joues tests)"
echo "  4. l'import des Fiches, les sources, le noyau et l'interface"

echo "A10 : le trait se définit par ses extrémités, porte ses pointes, se trace à l'outil Ligne, et sort pareil à l'écran et au PDF."
