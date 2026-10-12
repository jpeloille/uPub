#!/usr/bin/env bash
#
# Confronte, mot à mot, les lignes que l'écran dessine à celles que le PDF imprime.
#
# C'est la comparaison des pages de texte. « comparer-rendus.sh » compte des pixels :
# sur une page de texte il ne mesure que le dessin des glyphes — deux rastériseurs du
# MÊME PDF, pdftoppm et pdftocairo, y diffèrent déjà de 2,36 % sur la page 1 du
# document d'exemple —, et il ne voyait pas qu'une ligne passait à la suivante : la
# coupure divergente de la première colonne de l'exemple n'y pesait que 0,09 point
# (MyPublisher/Docs/PLAN.md, poste Q13). Il reste l'outil des formes et des images.
#
#   Scripts/comparer-mots.sh <doc.mypub|doc.pub> [page|tout] [tolérance en pt]
#
# Ce qu'il fait :
#
#   - l'écran : le banc ouvre le document dans la vraie fenêtre et relève, page par
#     page, chaque ligne de chaque bloc de texte telle que TexteCompose la dessine —
#     son texte, sa bande verticale, son cadre horizontal, et l'abscisse de chaque
#     morceau (« MyPublisher.Banc mots ») ;
#   - le PDF : le vrai binaire exporte, et « pdftotext -bbox » en rend les mots avec
#     leur boîte, en points ;
#   - chaque mot du PDF est rangé dans la ligne de l'écran dont la bande contient son
#     centre et dont le cadre contient son début. Une ligne est COUPÉE AUTREMENT quand
#     les mots du PDF qui y tombent ne font pas son texte ; un mot du PDF qui ne tombe
#     dans aucune ligne, au droit d'un bloc relevé, en est une aussi — la ligne que le
#     PDF a de plus.
#
# Le verdict porte sur les lignes coupées autrement : il en faut zéro. La tolérance,
# en points, porte sur la place du premier mot de chaque ligne, qui doit être la même
# des deux côtés à la tolérance près. Elle vaut 1 pt par défaut, et ce n'est pas un
# souhait : la mise en page d'Avalonia arrondit la place d'un bloc au pixel — à
# l'échelle du banc, un bloc posé à 56,69 pt est dessiné à 57 —, et le PDF ne
# l'arrondit pas. Un premier mot déplacé d'une ligne entière, lui, dépasse 10 pt.
#
# Les blocs tournés ne sont pas comparés — pdftotext ne lit pas leurs lignes comme
# des lignes — et l'outil le dit. Les variables MYPUBLISHER_CHARGER_D_ABORD
# (romain|gras|italique) et MYPUBLISHER_COUPURE_SEULE=1 passent au banc : la première
# change l'ordre dans lequel le processus charge les faces, la seconde fait couper
# l'écran à sa seule mesure, ce que faisait le dépôt avant Q13 — c'est la
# contre-épreuve de la recette de Q13.
#
set -euo pipefail

# Les nombres d'ImageMagick et d'awk n'interviennent pas ici, mais python et les
# messages, si : une locale française écrirait « 0,5 » où l'on compare « 0.5 ».
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$racine"

document="${1-}"
pages="${2-tout}"
tolerance="${3-1.0}"

if [ -z "$document" ] || [ ! -f "$document" ]; then
    echo "Usage : $0 <doc.mypub|doc.pub> [page|tout] [tolérance en pt]" >&2
    exit 2
fi

application="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

for binaire in "$application" "$banc"; do
    if [ ! -x "$binaire" ]; then
        echo "ÉCHEC : binaire introuvable ($binaire). Compiler d'abord." >&2
        exit 1
    fi
done

if ! command -v pdftotext > /dev/null; then
    echo "ÉCHEC : « pdftotext » est introuvable (poppler-utils)." >&2
    exit 1
fi

# Un .pub se lit par pub2raw, cherché comme la recette R5 le cherche.
case "$document" in
    *.pub)
        outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
        if [ -z "$outil" ] || [ ! -x "$outil" ]; then
            echo "ÉCHEC : « pub2raw » est introuvable, et le document est un .pub." >&2
            exit 1
        fi
        export MYPUBLISHER_PUB2RAW="$outil"
        if [ -d "$racine/.outils/mspub/lib" ]; then
            export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
        fi
        ;;
esac

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# La date des champs : la même des deux côtés, le jour où l'on compare.
jour="$(date +%F)"

if ! "$banc" mots "$document" --sortie "$travail/ecran.json" > "$travail/banc.txt" 2>&1; then
    echo "ÉCHEC : le banc n'a pas relevé les lignes de l'écran." >&2
    sed 's/^/    /' "$travail/banc.txt" >&2
    exit 1
fi

# Sans affichage, comme un script le lance : c'est le chemin où l'export monte lui-même
# la mesure de l'écran.
if ! env -u DISPLAY -u WAYLAND_DISPLAY "$application" "$document" --date "$jour" \
        --export "$travail/sortie.pdf" > "$travail/export.txt" 2>&1; then
    echo "ÉCHEC : l'export n'a pas abouti." >&2
    sed 's/^/    /' "$travail/export.txt" >&2
    exit 1
fi
pdftotext -bbox "$travail/sortie.pdf" "$travail/mots.html"

python3 - "$travail/ecran.json" "$travail/mots.html" "$pages" "$tolerance" "$(head -1 "$travail/banc.txt")" <<'PY'
import json, re, sys
from html import unescape

ecran_json, mots_html, pages_demandees, tolerance, banc = sys.argv[1:6]
tolerance = float(tolerance)

# Les mots du PDF, page par page : (xMin, yMin, xMax, yMax, texte), en points.
pages_pdf = []
for page in re.split(r"<page ", open(mots_html, encoding="utf-8").read())[1:]:
    pages_pdf.append([
        (float(m.group(1)), float(m.group(2)), float(m.group(3)), float(m.group(4)), unescape(m.group(5)))
        for m in re.finditer(r'<word xMin="([-\d.]+)" yMin="([-\d.]+)" xMax="([-\d.]+)" yMax="([-\d.]+)">(.*?)</word>', page)])

blocs = json.load(open(ecran_json, encoding="utf-8"))
nombre = max([b["Page"] for b in blocs] + [len(pages_pdf)])
pages = list(range(1, nombre + 1)) if pages_demandees == "tout" else [int(pages_demandees)]

if len(pages_pdf) < max(pages):
    print(f"ÉCHEC : le PDF n'a que {len(pages_pdf)} page(s), page {max(pages)} demandée.", file=sys.stderr)
    sys.exit(1)

def lettres(texte):
    """Les lettres et les chiffres d'un texte, et rien d'autre.

    Une ligne coupée autrement change de mots, donc de lettres : c'est sur elles qu'on
    compare. Le reste ne dit rien d'une coupure et brouillerait tout. Les blancs : une
    tabulation se dessine en avance, pas en caractère, et pdftotext recolle deux mots
    qu'une tabulation de 0,17 pt sépare. Les symboles : un caractère que la police du
    PDF ne porte pas — « ≤ », une puce de Wingdings —, l'écran le prend dans une police
    de secours, le PDF ne l'écrit pas ; on le signale à part."""
    return "".join(c for c in texte if c.isalnum())

def sans_blancs(texte):
    return "".join(texte.split())

coupees, deplacees, ecartes, symboles = [], [], [], []
lignes_vues = blocs_vus = mots_vus = 0
ecart_max = 0.0

def dans(m, b):
    return b["X"] - tolerance <= m[0] and m[2] <= b["X"] + b["Largeur"] + tolerance \
        and b["Y"] - tolerance <= m[1] and m[3] <= b["Y"] + b["Hauteur"] + tolerance

for numero in pages:
    releves = [b for b in blocs if b["Page"] == numero]
    # Les mots d'un bloc qu'on ne compare pas — un titre tourné — ne doivent pas
    # tomber dans les lignes des autres.
    mots_pdf = [m for m in pages_pdf[numero - 1]
                if not any(not b["Compare"] and dans(m, b) for b in releves)]

    # Chaque mot du PDF va à UNE ligne de l'écran : parmi celles dont la bande contient
    # son centre et dont le cadre contient son début, celle qui porte un mot à la même
    # place — les fiches ATR superposent des blocs, et deux lignes de deux blocs
    # peuvent occuper la même bande. Un mot qui a changé de ligne trouve une ligne
    # quand même, mais pas la sienne : la comparaison des lettres le verra.
    lignes_page = [(bloc, rang, ligne) for bloc in releves if bloc["Compare"]
                   for rang, ligne in enumerate(bloc["Lignes"])]
    par_ligne = {}
    for m in mots_pdf:
        centre = (m[1] + m[3]) / 2
        candidates = [(bloc, rang, ligne) for bloc, rang, ligne in lignes_page
                      if ligne["Haut"] <= centre <= ligne["Bas"]
                      and ligne["Gauche"] - tolerance <= m[0] <= ligne["Droite"] + tolerance]
        if candidates:
            def distance(c):
                # Un mot que l'écran dessine d'un tenant, le PDF peut l'écrire en deux —
                # une puce de Wingdings collée au mot qui la suit : une partie suffit.
                return min((abs(w["X"] - m[0]) + (0 if lettres(m[4]) in lettres(w["Texte"]) else 1000)
                            for w in c[2]["Mots"] if w["Texte"]), default=float("inf"))
            bloc, rang, ligne = min(candidates, key=distance)
            par_ligne.setdefault(id(ligne), []).append(m)

    for bloc in releves:
        if not bloc["Compare"]:
            ecartes.append(f"page {numero} · {bloc['Nom']} ({bloc['Motif']})")
            continue
        blocs_vus += 1
        for rang, ligne in enumerate(bloc["Lignes"]):
            mots = [m for m in ligne["Mots"] if m["Texte"]]
            if not mots:
                continue
            lignes_vues += 1
            mots_vus += len(mots)
            # Les mots du PDF rangés dans cette ligne. Une ligne qui coupe autrement n'a
            # plus, à cette hauteur, les mêmes mots.
            pdf = sorted(par_ligne.get(id(ligne), []), key=lambda m: m[0])
            texte_ecran = " ".join(m["Texte"] for m in mots)
            texte_pdf = " ".join(m[4] for m in pdf)
            if lettres(texte_ecran) != lettres(texte_pdf):
                coupees.append(f"page {numero} · {bloc['Nom']}, ligne {rang + 1} :\n"
                               f"        écran « {texte_ecran} »\n"
                               f"        PDF   « {texte_pdf} »")
                continue
            if sans_blancs(texte_ecran) != sans_blancs(texte_pdf):
                manquants = sorted(set(sans_blancs(texte_ecran)) - set(sans_blancs(texte_pdf)))
                symboles.append(f"page {numero} · {bloc['Nom']}, ligne {rang + 1} : "
                                f"{' '.join(repr(c)[1:-1] for c in manquants) or 'ordre des symboles'}")
            # La place de la ligne : son premier mot porteur de lettres, des deux côtés,
            # quand c'est le même mot. Ce qui ne coupe pas autrement doit encore se
            # poser au même endroit, à la tolérance près.
            premier_ecran = next((m for m in mots if lettres(m["Texte"])), None)
            premier_pdf = next((m for m in pdf if lettres(m[4])), None)
            # Seulement quand ils s'écrivent pareil : un symbole que le PDF n'écrit pas
            # déplace le début du mot sans déplacer le mot.
            if premier_ecran and premier_pdf and premier_ecran["Texte"] == premier_pdf[4]:
                decalage = abs(premier_pdf[0] - premier_ecran["X"])
                ecart_max = max(ecart_max, decalage)
                if decalage > tolerance:
                    deplacees.append(f"page {numero} · {bloc['Nom']}, ligne {rang + 1} : « {premier_ecran['Texte']} » "
                                     f"à {premier_ecran['X']:.2f} pt à l'écran, {premier_pdf[0]:.2f} pt au PDF")

for e in coupees:
    print(f"  coupée autrement — {e}")
for e in deplacees:
    print(f"  déplacée — {e}")
for e in ecartes:
    print(f"  non comparé — {e}")
for e in symboles:
    print(f"  symbole différent — {e}")

etiquette = "toutes les pages" if pages_demandees == "tout" else f"page {pages[0]}"
print(f"{etiquette} : {lignes_vues} lignes, {mots_vus} mots dans {blocs_vus} blocs ; "
      f"{len(coupees)} ligne(s) coupée(s) autrement ; {len(deplacees)} déplacée(s) de plus de {tolerance:g} pt "
      f"(écart max du premier mot {ecart_max:.2f} pt) ; {len(ecartes)} bloc(s) non comparé(s) ; "
      f"{len(symboles)} ligne(s) dont un symbole diffère. Banc : {banc}")

if blocs_vus == 0 or mots_vus == 0:
    print("ÉCHEC : aucun mot confronté. Le banc a-t-il relevé la page ?", file=sys.stderr)
    sys.exit(1)
if coupees or deplacees:
    print("ÉCHEC : l'écran et le PDF ne posent pas les mêmes mots aux mêmes lignes.", file=sys.stderr)
    sys.exit(1)
PY
