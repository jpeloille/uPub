#!/usr/bin/env bash
#
# Recette du poste R4 — l'interligne : une seule mesure.
#
# Ce qu'elle éprouve, sur un bloc de neuf lignes en DejaVu Sans 12 pt, posé deux fois
# — interligne 1,0 puis 1,5 :
#
#   1. au PDF, l'avance d'une ligne à la suivante vaut le multiple de la hauteur
#      NATURELLE de la police, et non du corps. Relevée par « pdftotext -bbox-layout »,
#      au millième de point, ligne par ligne : une avance juste en moyenne mais qui
#      varierait d'une ligne à l'autre ne passe pas ;
#   2. à l'écran, la même avance, mesurée dans l'encre que le banc dessine ;
#   3. l'écran et le PDF concordent : même avance à 0,05 pt près, et chaque ligne au
#      même endroit à 0,5 pt près — un pixel à deux pixels par point, ce que le suivi
#      de PLAN.md annonce (« écran et PDF concordent au pixel »).
#
# La hauteur naturelle n'est PAS lue dans le code qu'on éprouve. Elle est calculée ici,
# dans la table « hhea » du fichier de police que fontconfig désigne — montée, descente
# et interligne du dessinateur, sur l'unité de l'em. Les deux rendus appellent le même
# « Compositeur.Avance » : les comparer entre eux prouverait leur accord, jamais leur
# justesse. C'est l'enseignement de R1, et c'est ce qu'avait trouvé le banc le jour où
# il a servi — l'écran avançait de 12,00 pt et le PDF de 13,93, et aucun des deux
# n'avait de quoi dire lequel se trompait (MyPublisher/Docs/PLAN.md, poste P3).
#
# L'encre se mesure à QUATRE pixels par point et non à deux. Le centre d'une ligne y
# est relevé au quart de point près, à cause du calage des glyphes sur la grille ; sur
# huit intervalles, l'erreur sur l'avance est au plus d'un trente-deuxième de point,
# sous la tolérance de 0,05 pt. À deux pixels par point, elle irait jusqu'à 0,06 pt,
# et la recette échouerait sur du code juste.
#
set -euo pipefail
# Les comparaisons numériques doivent être insensibles à la locale : sous une locale
# française, awk et printf écrivent « 13,97 » et les tests suivants comparent alors une
# chaîne.
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile, plutôt que de faire confiance aux binaires présents. Le banc n'est
# compilé ni par « dotnet test » ni par « Scripts/controle.sh » : sans cette étape, la
# moitié « écran » de la recette mesurerait un banc périmé, et resterait verte sur un
# sabotage qu'elle n'aurait pas vu.
if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
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

police="$(fc-match "DejaVu Sans" --format='%{file}' 2>/dev/null || true)"
case "$police" in
    *DejaVuSans.ttf) ;;
    *)  echo "ÉCHEC : DejaVu Sans est introuvable (fontconfig rend « $police »)." >&2
        echo "Sans elle, la hauteur naturelle calculée ici n'est pas celle de la police dessinée." >&2
        exit 1 ;;
esac

# ------------------------------------------------------------- le document éprouvé

# Neuf lignes, une par paragraphe, pour qu'aucune coupure ne dépende de la mesure des
# mots : c'est l'avance qu'on éprouve, pas la composition. Toutes commencent par le
# même mot, dont l'encre sert de repère d'une ligne à l'autre. Marges intérieures à
# zéro, pour que rien d'autre que l'interligne ne pose les lignes.
python3 - "$travail/interligne.mypub" <<'PY'
import json, sys

texte = "\n".join(f"Ligne {n} de la recette" for n in range(1, 10))

def bloc(nom, y, interligne):
    return {"$kind": "text", "Name": nom, "X": 60, "Y": y, "Width": 300, "Height": 220,
            "Text": texte, "FontFamily": "DejaVu Sans", "FontSize": 12,
            "Foreground": "#FF000000", "Align": "Left", "LineHeight": interligne,
            "PaddingLeft": 0, "PaddingTop": 0, "PaddingRight": 0, "PaddingBottom": 0,
            "VerticalAlign": "Top"}

document = {"FormatVersion": 4, "Title": "Recette R4 — interligne",
            "Pages": [{"Name": "Page 1", "Frames": [bloc("Interligne 1,0", 60, 1.0),
                                                    bloc("Interligne 1,5", 320, 1.5)]}]}
json.dump(document, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY

# ---------------------------------------------------- la hauteur naturelle, à la source

naturelle="$(python3 - "$police" 12 <<'PY'
import struct, sys

donnees = open(sys.argv[1], "rb").read()
corps = float(sys.argv[2])

# Le répertoire des tables d'un TrueType : quatre octets de version, le nombre de
# tables, puis seize octets par table — étiquette, somme, position, longueur.
tables = {}
nombre = struct.unpack(">H", donnees[4:6])[0]
for i in range(nombre):
    etiquette, _, position, _ = struct.unpack(">4sIII", donnees[12 + 16 * i:28 + 16 * i])
    tables[etiquette] = position

em = struct.unpack(">H", donnees[tables[b"head"] + 18:tables[b"head"] + 20])[0]
montee, descente, interligne = struct.unpack(">hhh", donnees[tables[b"hhea"] + 4:tables[b"hhea"] + 10])
print(f"{(montee - descente + interligne) * corps / em:.5f}")
PY
)"
echo "  hauteur naturelle de DejaVu Sans 12 pt, lue dans « hhea » : $naturelle pt"

"$binaire" "$travail/interligne.mypub" --export "$travail/sortie.pdf" \
    > /dev/null 2> "$travail/avertissements.txt"
if [ -s "$travail/avertissements.txt" ]; then
    echo "ÉCHEC : l'export avertit, et la mesure ne porterait plus sur ce qu'on croit." >&2
    cat "$travail/avertissements.txt" >&2
    exit 1
fi

# ----------------------------------------------------------------------- 1. le PDF

pdftotext -bbox-layout "$travail/sortie.pdf" "$travail/boites.xml"

python3 - "$travail/boites.xml" "$naturelle" <<'PY'
import re, sys

lignes = [float(y) for y in re.findall(r'<line xMin="[0-9.]+" yMin="([0-9.]+)"', open(sys.argv[1]).read())]
naturelle = float(sys.argv[2])

echec = False
for nom, multiple, haut, bas in (("1,0", 1.0, 60, 300), ("1,5", 1.5, 320, 560)):
    ys = sorted(y for y in lignes if haut <= y < bas)
    if len(ys) != 9:
        print(f"ÉCHEC : interligne {nom} — {len(ys)} lignes relevées dans le PDF, 9 attendues.", file=sys.stderr)
        sys.exit(1)
    attendue = naturelle * multiple
    ecarts = [b - a for a, b in zip(ys, ys[1:])]
    pire = max(abs(e - attendue) for e in ecarts)
    avance = (ys[-1] - ys[0]) / 8
    print(f"  PDF   — interligne {nom} : avance {avance:8.4f} pt, attendue {attendue:8.4f}"
          f" (pire intervalle à {pire:.4f} pt)")
    if pire > 0.05:
        print(f"ÉCHEC : au PDF, l'interligne {nom} n'avance pas de {attendue:.4f} pt"
              f" — un intervalle s'en écarte de {pire:.4f} pt.", file=sys.stderr)
        echec = True

sys.exit(1 if echec else 0)
PY

# ------------------------------------------------------------- 2 et 3. l'encre

pp=4
"$banc" rendre "$travail/interligne.mypub" --sortie "$travail/ecran.png" \
        --page 1 --pixels-par-point "$pp" --sans-reperes > /dev/null
pdftoppm -r "$((72 * pp))" -f 1 -l 1 -png -singlefile "$travail/sortie.pdf" "$travail/pdf"

# En niveaux de gris, au format le plus simple qui soit : « P5 », un en-tête de trois
# nombres puis un octet par pixel. Python le lit sans bibliothèque d'images.
magick "$travail/ecran.png" -colorspace gray "$travail/ecran.pgm"
magick "$travail/pdf.png" -colorspace gray "$travail/pdf.pgm"

python3 - "$travail/ecran.pgm" "$travail/pdf.pgm" "$pp" "$naturelle" <<'PY'
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

pp, naturelle = int(sys.argv[3]), float(sys.argv[4])

def centres(chemin, haut, bas):
    """Le centre vertical de l'encre de chaque ligne, en points.

    On ne regarde que les colonnes du mot « Ligne », le même sur les neuf lignes :
    les chiffres qui suivent changent d'une ligne à l'autre et déplaceraient le
    centre. Le centre est pondéré par la noirceur, ce qui le rend plus fin qu'un
    bord d'encre, que le lissage déplace d'un pixel selon la lettre."""
    largeur, _, pixels = lire(chemin)
    gauche, droite = 60 * pp, 92 * pp
    trouves, courant = [], None
    for y in range(haut * pp, bas * pp):
        poids = sum(255 - pixels[y * largeur + x] for x in range(gauche, droite))
        if poids > 0:
            courant = courant or [0, 0]
            courant[0] += poids
            courant[1] += poids * (y + 0.5)
        elif courant:
            trouves.append(courant[1] / courant[0] / pp)
            courant = None
    return trouves

echec = False
for nom, multiple, haut, bas in (("1,0", 1.0, 60, 300), ("1,5", 1.5, 320, 560)):
    ecran = centres(sys.argv[1], haut, bas)
    pdf = centres(sys.argv[2], haut, bas)
    for cote, ys in (("écran", ecran), ("PDF", pdf)):
        if len(ys) != 9:
            print(f"ÉCHEC : interligne {nom} — {len(ys)} lignes d'encre à l'{cote}, 9 attendues.",
                  file=sys.stderr)
            sys.exit(1)

    attendue = naturelle * multiple
    avances = {cote: (ys[-1] - ys[0]) / 8 for cote, ys in (("écran", ecran), ("PDF", pdf))}
    ecart = max(abs(a - b) for a, b in zip(ecran, pdf))

    for cote, avance in avances.items():
        print(f"  {cote:5} — interligne {nom} : avance de l'encre {avance:8.4f} pt, attendue {attendue:8.4f}")
        if abs(avance - attendue) > 0.05:
            print(f"ÉCHEC : à l'{cote}, l'interligne {nom} avance de {avance:.4f} pt au lieu de {attendue:.4f}.",
                  file=sys.stderr)
            echec = True

    print(f"  écart écran/PDF — interligne {nom} : {abs(avances['écran'] - avances['PDF']):.4f} pt"
          f" sur l'avance, {ecart:.3f} pt au pire sur la position d'une ligne")
    if abs(avances["écran"] - avances["PDF"]) > 0.05:
        print(f"ÉCHEC : l'écran et le PDF n'avancent pas d'autant à l'interligne {nom}.", file=sys.stderr)
        echec = True
    if ecart > 0.5:
        print(f"ÉCHEC : à l'interligne {nom}, une ligne est à {ecart:.3f} pt de sa place au PDF"
              " — plus d'un pixel à deux pixels par point.", file=sys.stderr)
        echec = True

sys.exit(1 if echec else 0)
PY

echo "R4 : l'interligne multiplie la hauteur naturelle de la police, au PDF comme à l'écran."
