#!/usr/bin/env bash
#
# Recette du poste A1 — la poignée de rotation.
#
# Le poste ne livre pas une propriété : il livre un GESTE. La recette part donc d'un
# vrai pointeur, synthétisé sur la vraie couche de saisie de la vraie fenêtre, et non
# d'un angle posé au champ du panneau — sans quoi elle éprouverait ce qui existait
# déjà.
#
#   1. le geste — « dotnet test » joue les dix vérifications de RotationTests : l'angle
#      obtenu, son SENS, l'aimantation sous Maj, l'annulation d'un pas, le fait que la
#      page se redessine, et la poignée qui ne paraît que sur un bloc seul. Le dernier
#      de ces tests écrit le document tourné là où la variable d'environnement le
#      demande : c'est celui-ci qu'on mesure ensuite ;
#   2. l'angle écrit dans le document vaut 90,000° — SIGNÉ ;
#   3. le PDF porte cet angle-là, mesuré à l'encre et non supposé ;
#   4. l'écran porte exactement le même ;
#   5. filet : l'écran et le PDF donnent la même image.
#
# **90°, parce que c'est l'angle des documents réels.** ATA 21 porte trois objets
# tournés, tous à 90° (« librevenge:rotate: 90 » dans le relevé de pub2raw) ; les
# fiches de limitations n'en portent aucun. C'est le seul angle que le corpus impose,
# et il tombe sur le pas d'aimantation, ce qui le rend exact plutôt qu'approché.
#
# **Le point 2 n'est pas une redite du point 3.** La boîte d'encre d'un bloc tourné de
# +90° est exactement celle d'un bloc tourné de −90°, et l'écran comme le PDF lisent la
# même propriété : ni la mesure d'encre ni la comparaison d'images ne peuvent voir une
# inversion de sens. Seule la valeur signée le peut — et le sens du geste lui-même est
# épinglé par « Un_geste_dans_lautre_sens_tourne_dans_lautre_sens », joué au point 1.
#
# **Le point 3 mesure aussi le PIVOT.** La boîte est attendue à une position absolue :
# tourner autour du coin supérieur gauche au lieu du centre donnerait la même boîte
# 60 × 200, posée 130 pt plus loin.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net9.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net9.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

document="$travail/tourne.mypub"

# 1 — le geste. Le fichier est retiré d'abord : « dotnet test » sort en 0 quand AUCUN
# test ne correspond au filtre, et une recette qui mesurerait un document laissé là par
# un passage précédent se déclarerait verte sans avoir rien joué.
rm -f "$document"
if ! MYPUBLISHER_RECETTE_ROTATION="$document" \
     dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj \
        --nologo -v q --filter "FullyQualifiedName~RotationTests" > "$travail/tests.txt" 2>&1; then
    echo "ÉCHEC : le geste de rotation ne passe pas." >&2
    sed 's/^/    /' "$travail/tests.txt" >&2
    exit 1
fi

if [ ! -f "$document" ]; then
    echo "ÉCHEC : le geste n'a pas écrit son document — aucun test ne l'a joué." >&2
    sed 's/^/    /' "$travail/tests.txt" >&2
    exit 1
fi

sed -n 's/^ *\(Passed!.*\)/  geste  : \1/p' "$travail/tests.txt" || true

# 2 — l'angle obtenu est celui visé, et il est signé.
python3 - "$document" <<'PY'
import json, sys

document = json.load(open(sys.argv[1], encoding="utf-8"))
blocs = document["Pages"][0]["Frames"]

if len(blocs) != 1:
    sys.exit(f"ÉCHEC : {len(blocs)} bloc(s) dans le document du geste, un seul attendu.")

bloc = blocs[0]
if bloc["Rotation"] != 90:
    sys.exit(f"ÉCHEC : le bloc est tourné de {bloc['Rotation']}°, 90° attendus "
             f"(l'angle des trois objets tournés d'ATA 21).")

# Tourner ne déplace ni ne redimensionne. À un millionième de point près et non à
# l'identique : sélectionner un bloc fait passer sa géométrie par le panneau, donc par
# un aller-retour en millimètres qui laisse 10⁻¹³ pt derrière lui — la dérive consignée
# au poste P3, mesurée et sans effet.
for propriete, attendu in (("X", 100), ("Y", 100), ("Width", 200), ("Height", 60)):
    if abs(bloc[propriete] - attendu) > 1e-6:
        sys.exit(f"ÉCHEC : {propriete} vaut {bloc[propriete]}, {attendu} attendu — "
                 f"le geste a déplacé ou redimensionné le bloc au lieu de le tourner.")

print(f"  angle  : {bloc['Rotation']}° écrits dans le document, position et taille intactes")
PY

# 3 et 4 — la boîte d'encre, des deux côtés.
#
# Le bloc fait 200 × 60 pt et son centre est en (200, 130). Tourné d'un quart de tour
# autour de ce centre, il occupe x ∈ [170, 230] et y ∈ [30, 230], soit — à deux pixels
# par point, l'échelle des deux rendus — une boîte de 120 × 400 posée en (340, 60).
attendu="120x400+340+60"

"$binaire" "$document" --export "$travail/sortie.pdf" > /dev/null
pdftoppm -r 144 -f 1 -l 1 -png -singlefile "$travail/sortie.pdf" "$travail/pdf"
"$banc" rendre "$document" --sortie "$travail/ecran.png" \
        --page 1 --pixels-par-point 2 --sans-reperes > /dev/null

# « -fuzz 20 % » : on cherche le bord de l'aplat rouge, pas la nuance de son lissage.
boite() { magick "$1" -fuzz 20% -trim -format "%w %h %X %Y" info:; }

# La mesure part sur la sortie standard, où l'appelant la capture ; la ligne qu'on
# lit, elle, part sur la sortie d'erreur, sinon la substitution de commande l'avalerait
# et la recette ne dirait plus ce qu'elle a mesuré.
mesurer() {
    local nom="$1" image="$2"
    local largeur hauteur x y
    read -r largeur hauteur x y <<< "$(boite "$image")"
    # magick écrit l'offset avec son signe : +340 et non 340.
    x="${x#+}"; y="${y#+}"

    printf "  %s : boîte d'encre %s x %s en (%s, %s)\n" "$nom" "$largeur" "$hauteur" "$x" "$y" >&2
    echo "${largeur}x${hauteur}+${x}+${y}"
}

pdf="$(mesurer "PDF  " "$travail/pdf.png")"
ecran="$(mesurer "écran" "$travail/ecran.png")"

# Deux pixels de tolérance : les deux moteurs n'arrondissent pas l'origine de la page
# au même pixel, et le lissage des bords déplace la coupe d'un pixel.
python3 - "$attendu" "$pdf" "$ecran" <<'PY'
import re, sys

def lire(texte):
    return [int(n) for n in re.findall(r"\d+", texte)]

attendu, pdf, ecran = (lire(a) for a in sys.argv[1:4])

for nom, obtenu in (("le PDF", pdf), ("l'écran", ecran)):
    if any(abs(o - a) > 2 for o, a in zip(obtenu, attendu)):
        sys.exit(f"ÉCHEC : {nom} pose une boîte d'encre {obtenu}, {attendu} attendu — "
                 f"l'angle ou le pivot ne sont pas ceux du document.")

if any(abs(p - e) > 2 for p, e in zip(pdf, ecran)):
    sys.exit(f"ÉCHEC : l'écran {ecran} et le PDF {pdf} ne tournent pas de la même façon.")

print("  accord : l'écran et le PDF portent le même angle, au pixel près")
PY

# 5 — le filet. Il n'éprouve pas l'angle : les deux rendus lisent la même propriété et
# se tromperaient ensemble. Il attrape ce qu'une mesure de boîte ne voit pas — un fond
# perdu, un bloc qui cesse d'être dessiné. Le relevé sur ce document est 0,05 %, le
# seul lissage des quatre bords ; le seuil est dix fois plus haut.
Scripts/comparer-rendus.sh "$document" 1 0.5

echo "A1 : un bloc tourné à la souris porte l'angle visé, s'annule, et sort le même au PDF."
