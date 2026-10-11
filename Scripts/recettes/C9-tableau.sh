#!/usr/bin/env bash
#
# Recette du poste C9 — le bloc tableau.
#
# Ce qu'elle éprouve, sur « Samples/recette-tableau.mypub » : six bandes à trois cases
# comme celles des fiches ATR, un tableau à deux lignes, et une case seule.
#
#   1. le document se relit et se réécrit au caractère près, cases comprises ;
#   2. le texte de chaque case se retrouve dans le PDF ;
#   3. les cases sont **mesurées** dans le PDF, au centième de point : origine de
#      colonne, bord intérieur, alignement à droite sur la somme des largeurs, et
#      hauteur de ligne ;
#   4. l'écran et le PDF donnent la même image.
#
# Le troisième point est celui qui compte. La comparaison d'images du quatrième est un
# filet grossier sur une page de tableaux, qui est aux trois quarts blanche : mesuré, le
# lissage des glyphes et des filets coûte à lui seul 2,40 %, et décaler tous les
# tableaux de 2 pt à l'export ne fait monter l'écart qu'à 3,04 %. Le même décalage
# déplace en revanche chaque mesure du point 3 de 2,00 pt, contre une tolérance de
# 0,05 pt. C'est donc la mesure qui garde la géométrie, et l'image qui garde le reste —
# une couleur perdue, un fond oublié, un filet qui disparaît.
#
# Les positions attendues se lisent dans le document : le tableau est à X = 56,69 pt,
# ses colonnes font 160, 160 et 162 pt, ses cases ont 3 pt de marge intérieure.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

document="Samples/recette-tableau.mypub"
binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# 1 — l'aller-retour d'écriture, colonnes, lignes et cases comprises.
"$binaire" "$document" --resave "$travail/copie.mypub" > /dev/null
if ! diff -u "$document" "$travail/copie.mypub" > "$travail/ecart.diff"; then
    echo "ÉCHEC : relu puis réécrit, le document à tableaux n'est plus le même." >&2
    head -30 "$travail/ecart.diff" >&2
    exit 1
fi

# 2 — le texte de chaque case arrive dans le PDF.
"$binaire" "$document" --export "$travail/sortie.pdf" > /dev/null
pdftotext "$travail/sortie.pdf" "$travail/sortie.txt"

for mot in "Vitesse maximale" "VNE 250 kt" "lisse" "Volets 15 degres" "Trains sortis" \
           "Une seule case"; do
    if ! tr '\n' ' ' < "$travail/sortie.txt" | grep -qF "$mot"; then
        echo "ÉCHEC : « $mot » ne se retrouve pas dans le PDF." >&2
        exit 1
    fi
done

# 3 — les cases sont à leur place, mesurée dans le PDF lui-même.
pdftotext -bbox "$travail/sortie.pdf" "$travail/bbox.html"
bbox="$travail/bbox.html"

# Un attribut du n-ième mot d'un texte donné, en points, origine en haut à gauche.
valeur() {
    awk -v mot=">$1</word>" -v rang="$2" -v attribut="$3" '
        index($0, mot) {
            n++
            if (n != rang) next
            if (match($0, attribut "=\"[0-9.-]+\"")) {
                s = substr($0, RSTART, RLENGTH)
                sub(attribut "=\"", "", s)
                sub("\"", "", s)
                print s
            }
            exit
        }' "$bbox"
}

verifier() {   # $1 mesuré · $2 attendu · $3 tolérance · $4 libellé
    if [ -z "$1" ]; then
        echo "ÉCHEC : $4 — aucun mot mesuré dans le PDF." >&2
        exit 1
    fi

    awk -v m="$1" -v a="$2" -v t="$3" -v l="$4" 'BEGIN {
        d = m - a; if (d < 0) d = -d
        if (d > t) {
            printf "ÉCHEC : %s — mesuré %.4f pt, attendu %.4f pt (± %.2f)\n", l, m, a, t \
                > "/dev/stderr"
            exit 1
        }
        printf "  %-46s %9.4f pt\n", l, m
    }'
}

# Origine de la première colonne : X du tableau plus la marge intérieure des cases.
verifier "$(valeur Vitesse 1 xMin)" 59.69 0.05 "colonne 1, texte au fer à gauche"

# Bord droit de la dernière colonne : 56,69 + 160 + 160 + 162 − 3. Une largeur de
# colonne fausse le déplace. La tolérance est celle de l'approche du « e » final :
# la boîte d'encre s'arrête un peu avant la chasse du glyphe, 0,04 pt mesuré.
verifier "$(valeur lisse 1 xMax)" 535.69 0.50 "colonne 3, texte au fer à droite"

# Bord intérieur d'une colonne de 240 pt, dans le tableau à deux lignes.
verifier "$(valeur 185 1 xMin)" 299.69 0.05 "colonne 2, bord intérieur"

# Hauteur de ligne : deux lignes de 24 pt l'une sous l'autre.
haut="$(valeur Volets 2 yMin)"
bas="$(valeur Trains 1 yMin)"
verifier "$(awk -v h="$haut" -v b="$bas" 'BEGIN { printf "%.6f", b - h }')" \
         24 0.05 "hauteur de la ligne 1"

# Deux bandes consécutives de 26 pt : les tableaux ne dérivent pas les uns par
# rapport aux autres.
verifier "$(awk -v a="$(valeur Vitesse 1 yMin)" -v b="$(valeur Vitesse 2 yMin)" \
            'BEGIN { printf "%.6f", b - a }')" \
         26 0.05 "écart entre deux bandes"

# 4 — l'écran et le PDF donnent la même image. Seuil réglé sur le relevé du jour :
# 2,40 % de lissage, et de la marge pour une machine dont les polices s'anticrénellent
# autrement.
Scripts/comparer-rendus.sh "$document" 1 3.5

echo "C9 : aller-retour identique, cases mesurées au centième de point dans le PDF, écran et export d'accord."
