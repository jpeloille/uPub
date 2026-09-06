#!/usr/bin/env bash
#
# Recette du poste R3 — colonnes à l'intérieur d'un bloc de texte.
#
# Ce qu'elle éprouve, sur « Samples/recette-colonnes.mypub », qui porte trois blocs :
#
#   A. trois colonnes, gouttière 12 pt, douze paragraphes numérotés « flot 01 » à
#      « flot 12 » — le texte doit remplir la première colonne puis passer à la
#      seconde, dans l'ordre ;
#   B. deux colonnes justifiées — la cible d'une ligne justifiée doit être le bord
#      droit de SA colonne, et non celui du bloc ;
#   C. un témoin à UNE colonne portant le même texte que A, et une gouttière de
#      40 pt : elle ne sert à rien à une colonne et ne doit rien changer.
#
# Quatre temps :
#
#   1. le document se relit et se réécrit au caractère près, colonnes comprises ;
#   2. les colonnes MESURÉES dans le PDF sont celles qu'on a demandées — pas de
#      169 pt d'une colonne à l'autre, trois sommets à la même hauteur, le flot dans
#      l'ordre, et les bords droits justifiés au bord des colonnes ;
#   3. l'écran mesure la même chose que le PDF, encre contre encre ;
#   4. le filet grossier, pour ce qu'il attrape encore.
#
# Le point 2 se mesure par « pdftotext -bbox », qui rend la boîte de chaque MOT.
# Pourquoi le mot et non la ligne, comme le fait la recette de R2 : parce que
# « -bbox-layout » regroupe en lignes selon sa propre analyse de mise en page, et que
# ce qu'on veut vérifier ici est précisément qu'il y a plusieurs colonnes à la même
# hauteur. Mesurer au mot ne demande rien à personne : chaque mot porte son abscisse,
# son ordonnée et son texte, et c'est nous qui les groupons.
#
# Une seule des mesures du point 2 dépend de la police, et il vaut mieux le savoir avant
# d'aller la chercher : le bord droit justifié du bloc B, attendu à 241,500 pt, est
# mesuré à 241,488 — le blanc réparti ne tombe jamais tout à fait juste. Les 0,012 pt
# d'écart tiennent dans la tolérance de 0,25, mais c'est le seul nombre ici qu'un
# changement de version de DejaVu Sans pourrait déplacer. Tout le reste — pas de colonne,
# sommets, fers — est de la géométrie et ne bouge pas d'une police à l'autre.
#
# Le point 3 ne peut pas se contenter du pourcentage de pixels différents, et c'est
# l'enseignement de R2 : sur une page de texte le seul lissage des glyphes en coûte
# plus qu'une divergence de mise en page. On mesure donc l'encre elle-même, colonne par
# colonne. Une colonne vide s'y voit tout de suite — un écran qui ignorerait le nombre
# de colonnes composerait tout dans la première, et les deux autres bandes seraient
# blanches.
#
set -euo pipefail
# Les comparaisons numériques doivent être insensibles à la locale : sous une locale
# française, awk écrit « 168,99 » et le test suivant compare alors une chaîne.
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

document="Samples/recette-colonnes.mypub"
binaire="MyPublisher/bin/Debug/net9.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net9.0/MyPublisher.Banc"

for outil in "$binaire" "$banc"; do
    if [ ! -x "$outil" ]; then
        echo "ÉCHEC : binaire introuvable ($outil). Compiler d'abord." >&2
        exit 1
    fi
done

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# Deux pixels par point des deux côtés, comme « Scripts/comparer-rendus.sh » : le banc
# rend à 2 px/pt et « pdftoppm -r 144 » rend 144 points par pouce sur 72 points
# typographiques. Aucun facteur de conversion n'entre dans les mesures ci-dessous.
pp=2
# Trois pixels, soit un point et demi. La plus petite grandeur mesurée ici est la
# gouttière, 12 pt, soit 24 px : le seuil est huit fois plus fin que le signal.
seuil=3

# La géométrie du document, redite ici : une recette qui lirait ses attentes dans le
# fichier qu'elle éprouve ne prouverait rien.
blocX=50            # le fer des trois blocs
blocL=495           # leur largeur
colL=157            # (495 − 2 × 12) / 3
pasA=169            # colL + gouttière
colB=241.5          # (495 − 12) / 2
pasB=253.5

verifier() {   # <libellé> <mesuré> <attendu> <tolérance>
    awk -v m="$2" -v a="$3" -v t="$4" -v l="$1" 'BEGIN {
        d = m - a; if (d < 0) d = -d;
        if (d > t) {
            printf "ÉCHEC : %s — %.3f mesuré, %.3f attendu (tolérance %.3f).\n",
                   l, m, a, t > "/dev/stderr";
            exit 1;
        }
        printf "  %-52s %8.3f\n", l, m;
    }'
}

egal() {   # <libellé> <mesuré> <attendu>
    if [ "$2" != "$3" ]; then
        echo "ÉCHEC : $1 — « $2 » mesuré, « $3 » attendu." >&2
        exit 1
    fi
    printf "  %-52s %s\n" "$1" "$2"
}

# ---------------------------------------------------------------------------- 1

"$binaire" "$document" --resave "$travail/copie.mypub" > /dev/null
if ! diff -u "$document" "$travail/copie.mypub" > "$travail/ecart.diff"; then
    echo "ÉCHEC : relu puis réécrit, le document en colonnes n'est plus le même." >&2
    head -30 "$travail/ecart.diff" >&2
    exit 1
fi

"$binaire" "$document" --export "$travail/sortie.pdf" > /dev/null

# ---------------------------------------------------------------------------- 2

pdftotext -bbox "$travail/sortie.pdf" "$travail/mots.xml"

# Une ligne du relevé par mot : « xMin yMin xMax texte ».
sed -n 's/.*<word xMin="\([0-9.]*\)" yMin="\([0-9.]*\)" xMax="\([0-9.]*\)"[^>]*>\(.*\)<\/word>.*/\1 \2 \3 \4/p' \
    "$travail/mots.xml" > "$travail/mots.txt"

if [ "$(wc -l < "$travail/mots.txt")" -lt 100 ]; then
    echo "ÉCHEC : le relevé de mots est vide ou tronqué." >&2
    exit 1
fi

# Les mots d'un bloc, repérés par la bande verticale qu'il occupe sur la page.
bande() { awk -v a="$1" -v b="$2" '$2 >= a && $2 < b' "$travail/mots.txt"; }

echo "── bloc A : trois colonnes"

# Les trois fers du bloc A, dans l'ordre : ce sont les abscisses distinctes des mots
# « flot ». Une seule voudrait dire que les colonnes n'existent pas.
fersA="$(bande 55 130 | awk '$4 == "flot" { printf "%.0f\n", $1 }' | sort -n -u)"
egal "bloc A : nombre de colonnes occupees" "$(echo "$fersA" | wc -l)" 3

read -r a0 a1 a2 <<< "$(echo "$fersA" | tr '\n' ' ')"
verifier "bloc A : fer de la colonne 1 (pt)"           "$a0" "$blocX" 0.25
verifier "bloc A : pas de la colonne 1 a la 2 (pt)"    "$((a1 - a0))" "$pasA" 0.25
verifier "bloc A : pas de la colonne 2 a la 3 (pt)"    "$((a2 - a1))" "$pasA" 0.25

# Les trois colonnes commencent à la même hauteur : une colonne qui démarrerait plus bas
# voudrait dire qu'on a continué à cumuler au lieu de repartir du haut.
sommets() { bande 55 130 | awk -v x="$1" '$1 > x - 1 && $1 < x + 1 { if (n == 0 || $2 < m) { m = $2; n = 1 } } END { print m }'; }
s0="$(sommets "$a0")"; s1="$(sommets "$a1")"; s2="$(sommets "$a2")"
verifier "bloc A : ecart de sommet colonnes 1-2 (pt)" \
    "$(awk -v a="$s0" -v b="$s1" 'BEGIN { print a - b }')" 0 0.05
verifier "bloc A : ecart de sommet colonnes 1-3 (pt)" \
    "$(awk -v a="$s0" -v b="$s2" 'BEGIN { print a - b }')" 0 0.05

# Et surtout : le texte COULE. Les numéros se suivent dans une colonne, et la colonne
# suivante reprend là où la précédente s'arrête. C'est ce que le poste promet, et c'est
# la seule chose ici qu'une simple mise en page à trois colonnes ne donnerait pas
# gratuitement.
flot() { bande 55 130 | awk -v x="$1" '$1 > x + 1 && $1 < x + 40 { print $2, $4 }' | sort -n | awk '{ printf "%s", $2 }'; }
egal "bloc A : flot de la colonne 1" "$(flot "$a0")" "01020304"
egal "bloc A : flot de la colonne 2" "$(flot "$a1")" "05060708"
egal "bloc A : flot de la colonne 3" "$(flot "$a2")" "09101112"

echo "── bloc B : deux colonnes justifiees"

# Le piège du poste, et il est exactement celui que R2 a rencontré sur les retraits :
# une ligne justifiée dont la cible serait restée la largeur du BLOC s'étirerait par
# dessus la gouttière et sous sa voisine. Le bord droit de la première colonne doit
# tomber à 241,5 pt du fer, pas à 495.
bordB1="$(bande 165 280 | awk '$1 < 300 && $3 > m { m = $3 } END { print m }')"
verifier "bloc B : bord droit justifie de la colonne 1 (pt)" \
    "$(awk -v b="$bordB1" -v x="$blocX" 'BEGIN { print b - x }')" "$colB" 0.25

ferB2="$(bande 165 280 | awk '$1 > 300 { if (n == 0 || $1 < m) { m = $1; n = 1 } } END { print m }')"
verifier "bloc B : fer de la colonne 2 (pt)" \
    "$(awk -v f="$ferB2" -v x="$blocX" 'BEGIN { print f - x }')" "$pasB" 0.25

bordB2="$(bande 165 280 | awk '$1 > 300 && $3 > m { m = $3 } END { print m }')"
verifier "bloc B : bord droit justifie de la colonne 2 (pt)" \
    "$(awk -v b="$bordB2" -v x="$blocX" 'BEGIN { print b - x }')" "$blocL" 0.25

echo "── bloc C : temoin a une colonne"

# La non-régression, prise à sa racine : un bloc d'une seule colonne compose comme il
# composait avant ce poste. Sa gouttière vaut 40 pt et ne doit rien faire — c'est le
# défaut du poste qui « signifie comme avant ».
fersC="$(bande 295 480 | awk '$4 == "flot" { printf "%.0f\n", $1 }' | sort -n -u)"
egal "temoin : nombre de colonnes occupees" "$(echo "$fersC" | wc -l)" 1
verifier "temoin : fer unique (pt)" "$fersC" "$blocX" 0.25
egal "temoin : les douze lignes se suivent" \
    "$(bande 295 480 | awk '$1 > 70 { print $2, $4 }' | sort -n | awk '{ printf "%s", $2 }')" \
    "010203040506070809101112"

# ---------------------------------------------------------------------------- 3

echo "── l'ecran contre le PDF, encre contre encre"

# L'écran, repères éteints : ils ne s'impriment jamais.
"$banc" rendre "$document" --sortie "$travail/ecran.png" \
        --page 1 --pixels-par-point "$pp" --sans-reperes > /dev/null
pdftoppm -r "$((72 * pp))" -f 1 -l 1 -png -singlefile "$travail/sortie.pdf" "$travail/pdf"

geometrie="$(magick "$travail/ecran.png" -format "%wx%h" info:)"
magick "$travail/pdf.png" -background white -extent "$geometrie" "$travail/pdf-cadre.png"

# La boîte englobante de l'encre dans un rectangle de la page, en pixels : « l h x y ».
# « %@ » la rend sans avoir à rogner l'image, et le seuil de 30 % écarte le lissage des
# bords sans écarter le trait lui-même. Les quatre arguments sont en POINTS.
boite() {   # <image> <x> <y> <largeur> <hauteur>
    magick "$1" -crop "$(( $4 * pp ))x$(( $5 * pp ))+$(( $2 * pp ))+$(( $3 * pp ))" +repage \
           -fuzz 30% -format "%@" info: | sed 's/[x+]/ /g'
}

# Une bande, mesurée des deux côtés, et confrontée à ce qu'on attend d'elle.
confronter() {   # <libellé> <x> <y> <largeur> <hauteur> <largeur d'encre attendue en pt>
    local libelle="$1" x="$2" y="$3" l="$4" h="$5" attendu="$6"
    local e p
    e="$(boite "$travail/ecran.png" "$x" "$y" "$l" "$h")"
    p="$(boite "$travail/pdf-cadre.png" "$x" "$y" "$l" "$h")"

    if [ -z "$e" ] || [ -z "$p" ]; then
        echo "ÉCHEC : $libelle — aucune encre. La colonne est-elle vide ?" >&2
        exit 1
    fi

    read -r el eh ex ey <<< "$e"
    read -r pl ph px py <<< "$p"

    verifier "ecran — $libelle : largeur d'encre (px)" "$el" "$(( attendu * pp ))" "$seuil"
    verifier "PDF   — $libelle : largeur d'encre (px)" "$pl" "$(( attendu * pp ))" "$seuil"
    verifier "ecart ecran/PDF — $libelle : largeur (px)" "$(( el - pl ))" 0 "$seuil"
    verifier "ecart ecran/PDF — $libelle : hauteur (px)" "$(( eh - ph ))" 0 "$seuil"
    verifier "ecart ecran/PDF — $libelle : fer (px)"     "$(( ex - px ))" 0 "$seuil"
    verifier "ecart ecran/PDF — $libelle : sommet (px)"  "$(( ey - py ))" 0 "$seuil"
}

# Le bloc A pris d'un seul tenant : son encre s'étend sur deux pas de colonne plus la
# largeur d'un « flot NN », soit 2 × 169 + 37 = 375 pt. Un rendu qui composerait tout
# dans la première colonne n'en couvrirait que 37, et la mesure le dirait aussitôt.
confronter "bloc A entier" "$blocX" 60 "$blocL" 62 375

# Puis chaque colonne séparément : chacune doit porter son encre, et la même des deux
# côtés. Une bande vide arrête la recette.
confronter "bloc A colonne 1" "$blocX"                     60 "$colL" 62 37
confronter "bloc A colonne 2" "$(( blocX + pasA ))"        60 "$colL" 62 37
confronter "bloc A colonne 3" "$(( blocX + 2 * pasA ))"    60 "$colL" 62 37

# Le bloc justifié couvre toute la largeur du bloc, ses deux colonnes bord à bord.
confronter "bloc B entier" "$blocX" 170 "$blocL" 104 494

# Le témoin, lui, tient dans la largeur d'un seul « flot NN » : sa gouttière de 40 pt
# n'a rien élargi, et rien n'est passé dans une seconde colonne qui n'existe pas.
confronter "temoin une colonne" "$blocX" 300 "$blocL" 170 37

# ---------------------------------------------------------------------------- 4

# Le filet grossier, pour finir : il n'éprouve rien de ce qui précède, mais il attrape
# ce que les bandes ne regardent pas — un bloc qui cesserait d'être dessiné. 1,28 %
# mesurés le 6 septembre 2026, entièrement sur le lissage des glyphes ; le seuil laisse
# ce qu'il faut au-dessus et rien de plus.
Scripts/comparer-rendus.sh "$document" 1 2.0

echo "R3 : aller-retour identique, colonnes mesurées au PDF, écran d'accord."
