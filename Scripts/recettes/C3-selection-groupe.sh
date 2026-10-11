#!/usr/bin/env bash
#
# Recette du poste C3 — sélection multiple, alignement, groupe.
#
# La recette d'origine tenait en une phrase : « sélectionner quatre blocs, les aligner
# sur leur bord gauche, les grouper, déplacer le groupe, l'exporter — la géométrie
# tient ». Jouée à la main, au pixel près, puis jetée. Elle se rejoue ainsi :
#
#   1. le geste — « GroupementTests », trait « poste=C3 », sur la vraie fenêtre : un
#      clic et trois Maj-clics prennent les quatre blocs, la commande du menu les aligne
#      à gauche ENTRE EUX (case « par rapport aux marges » décochée), Ctrl+Maj+G les
#      groupe, et la souris déplace le groupe de (30, 20) pt. Le test écrit le document
#      deux fois, groupé puis déplacé, dans le dossier que nomme
#      MYPUBLISHER_RECETTE_GROUPE. Puis il défait tout, un pas par geste : le
#      déplacement, le groupement, l'alignement — et les blocs reviennent à leur place ;
#   2. le document groupé porte un groupe de quatre blocs, et les quatre ont le même X
#      sur la page : celui du plus à gauche, 150 pt — un alignement sur un autre bord
#      donnerait un X commun, mais pas celui-là ;
#   3. dans son PDF, les quatre mots partent du même fer, à 154 pt — 150 pt de bloc et
#      4 pt de marge intérieure —, mesuré par « pdftotext -bbox » ;
#   4. dans le PDF du document déplacé, chaque mot est EXACTEMENT décalé de (30, 20) pt
#      par rapport au précédent, au centième de point ;
#   5. filet : l'écran et le PDF du document déplacé donnent la même image.
#
# Pourquoi mesurer dans le PDF et non dans le document : le document dit où le groupe
# CROIT être. Un export qui oublierait la translation du groupe, ou l'appliquerait
# deux fois, écrirait un document juste et un PDF faux — c'est le rendu récursif que
# C3c a introduit, et la seule chose que l'utilisateur imprime.
#
# Pourquoi les mots ont un fond : quatre mots seuls ne pèsent que quelques milliers de
# pixels. Un écran qui poserait le groupe ailleurs que le PDF resterait sous le seuil
# du point 5 ; quatre aplats de 40 pt de haut, non — 0,08 % d'écart relevé, contre
# près de 2 % pour un groupe décalé de 30 pt d'un seul côté.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile, plutôt que de lancer les tests avec « --no-build » : un binaire périmé
# survit à une modification des sources (cf. la recette de A4).
if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# ------------------------------------------------------------------------- 1. le geste

# Le dossier est neuf : « dotnet test » sort en 0 quand AUCUN test ne correspond au
# filtre, et une recette qui mesurerait des documents laissés là par un passage
# précédent se déclarerait verte sans avoir rien joué.
documents="$travail/documents"
mkdir "$documents"

if ! MYPUBLISHER_RECETTE_GROUPE="$documents" \
     dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=C3" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : le geste de C3 ne passe pas." >&2
    exit 1
fi

for nom in groupe deplace; do
    if [ ! -f "$documents/$nom.mypub" ]; then
        echo "ÉCHEC : le geste n'a pas écrit « $nom.mypub » — aucun test ne l'a joué." >&2
        sed 's/^/    /' "$travail/tests.txt" >&2
        exit 1
    fi
done

echo "  1. geste joué : clic, trois Maj-clics, aligner à gauche, Ctrl+Maj+G, glisser de (30, 20)"

# ----------------------------------------------------------- 2. le document groupé

python3 - "$documents/groupe.mypub" <<'PY'
import json, sys

document = json.load(open(sys.argv[1], encoding="utf-8"))
blocs = document["Pages"][0]["Frames"]

if len(blocs) != 1 or blocs[0]["$kind"] != "group":
    sys.exit(f"ÉCHEC : la page porte {len(blocs)} bloc(s), un seul groupe attendu.")

groupe = blocs[0]
enfants = groupe["Children"]
if [e["Name"] for e in enfants] != ["Alpha", "Bravo", "Charlie", "Delta"]:
    sys.exit(f"ÉCHEC : le groupe porte {[e['Name'] for e in enfants]}, les quatre blocs attendus.")

# Les enfants sont en coordonnées relatives : leur place sur la page est celle du
# groupe plus la leur. Au millionième et non à l'identique — l'aller-retour en
# millimètres du panneau, consigné au poste P3, laisse 10⁻¹³ pt derrière lui.
abscisses = [groupe["X"] + e["X"] for e in enfants]
for nom, x in zip(("Alpha", "Bravo", "Charlie", "Delta"), abscisses):
    if abs(x - 150) > 1e-6:
        sys.exit(f"ÉCHEC : « {nom} » est à X = {x} sur la page, 150 attendu "
                 f"(le bord gauche du plus à gauche des quatre).")

print("  2. groupe de quatre blocs, tous à X = 150 pt sur la page")
PY

# ------------------------------------------------------------- 3 et 4. le PDF mesuré

for nom in groupe deplace; do
    "$binaire" "$documents/$nom.mypub" --export "$travail/$nom.pdf" > /dev/null
    pdftotext -bbox "$travail/$nom.pdf" "$travail/$nom.html"
done

# « pdftotext -bbox » rend la boîte de chaque mot à l'origine du crayon : xMin est le
# fer du texte, sans l'approche de la première lettre.
python3 - "$travail/groupe.html" "$travail/deplace.html" <<'PY'
import re, sys

def mots(chemin):
    texte = open(chemin, encoding="utf-8").read()
    trouves = {}
    for x, y, mot in re.findall(r'<word xMin="([\d.]+)" yMin="([\d.]+)"[^>]*>([^<]+)</word>', texte):
        if mot in trouves:
            sys.exit(f"ÉCHEC : « {mot} » paraît deux fois dans {chemin}.")
        trouves[mot] = (float(x), float(y))
    return trouves

attendus = ["Alpha", "Bravo", "Charlie", "Delta"]
avant, apres = mots(sys.argv[1]), mots(sys.argv[2])

for nom, releve in (("groupé", avant), ("déplacé", apres)):
    if sorted(releve) != attendus:
        sys.exit(f"ÉCHEC : le PDF {nom} porte {sorted(releve)}, {attendus} attendus.")

# 3 — un même fer, et le bon.
for mot in attendus:
    x = avant[mot][0]
    if abs(x - 154) > 0.01:
        sys.exit(f"ÉCHEC : dans le PDF groupé, « {mot} » part de {x:.3f} pt, 154 attendus "
                 f"(150 pt de bloc, 4 pt de marge intérieure).")
print("  3. PDF groupé : les quatre mots partent de 154,00 pt")

# 4 — chacun exactement décalé de (30, 20).
for mot in attendus:
    dx = apres[mot][0] - avant[mot][0]
    dy = apres[mot][1] - avant[mot][1]
    print(f"     {mot:8} décalé de ({dx:.3f}, {dy:.3f}) pt")
    if abs(dx - 30) > 0.01 or abs(dy - 20) > 0.01:
        sys.exit(f"ÉCHEC : « {mot} » s'est déplacé de ({dx:.3f}, {dy:.3f}) pt au PDF, "
                 f"(30, 20) attendus.")
print("  4. PDF déplacé : chaque mot exactement décalé de (30, 20) pt")
PY

# ------------------------------------------------------------------------- 5. filet

# Relevé sur ce document : 0,08 %, le lissage des glyphes et des quatre aplats. Le
# seuil est six fois plus haut, et quatre fois plus bas que ce que coûterait un groupe
# décalé de 30 pt d'un seul côté.
Scripts/comparer-rendus.sh "$documents/deplace.mypub" 1 0.5

echo "C3 : sélectionnés, alignés, groupés et déplacés à la souris, les quatre blocs sortent au PDF où le geste les a mis."
