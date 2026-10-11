#!/usr/bin/env bash
#
# Recette du poste R2 — retraits et espacement de paragraphe.
#
# Ce qu'elle éprouve, sur « Samples/recette-retraits.mypub » : une liste à puces en
# retrait négatif, un paragraphe en retrait au milieu d'un bloc justifié, et un
# espacement après le paragraphe — les trois choses que les deux .pub réels demandent
# (MyPublisher/Docs/Documents-reels.md).
#
#   1. le document se relit et se réécrit au caractère près, écarts de paragraphe
#      compris ;
#   2. les retraits MESURÉS dans le PDF sont ceux qu'on a demandés — 18 pt de retrait
#      négatif, 24 pt de retrait de paragraphe à gauche ET à droite, 12 pt d'espace
#      après ;
#   3. l'écran mesure la même chose que le PDF, encre contre encre.
#
# Le point 2 se mesure par « pdftotext -bbox-layout », qui rend la boîte de chaque ligne
# en points PDF, à l'origine du crayon et non au bord de l'encre : un retrait s'y lit
# donc au centième de point, sans que les approches de glyphes ne brouillent la mesure.
#
# Le point 3 ne peut pas se contenter du pourcentage de pixels différents. Deux raisons.
# D'abord ce document est une page de texte : le seul lissage des glyphes y coûte 2,9 %,
# et une divergence de retrait simulée — l'écran composant sans retraits, le PDF avec —
# n'en produit que 3,9 %. Le filet global ne les sépare pas. Ensuite, et c'est
# l'enseignement de R1 : les deux rendus appelant le même calcul, une comparaison
# écran ↔ PDF prouve leur accord, jamais leur justesse. On mesure donc l'encre elle-même,
# bande par bande, des deux côtés — c'est la vérification du poste C6, « vérifié en
# mesurant l'encre », rendue rejouable.
#
set -euo pipefail
# Les comparaisons numériques doivent être insensibles à la locale : sous une locale
# française, awk écrit « 17,99 » et le test suivant compare alors une chaîne.
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

document="Samples/recette-retraits.mypub"
binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

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
# Trois pixels, soit un point et demi. La plus petite grandeur mesurée ici est
# l'espacement, 12 pt, soit 24 px : le seuil est huit fois plus fin que le signal.
seuil=3

# ---------------------------------------------------------------------------- 1

"$binaire" "$document" --resave "$travail/copie.mypub" > /dev/null
if ! diff -u "$document" "$travail/copie.mypub" > "$travail/ecart.diff"; then
    echo "ÉCHEC : relu puis réécrit, le document aux retraits n'est plus le même." >&2
    head -30 "$travail/ecart.diff" >&2
    exit 1
fi

"$binaire" "$document" --export "$travail/sortie.pdf" > /dev/null
pdftotext "$travail/sortie.pdf" "$travail/sortie.txt"

for mot in "Ouvrir la vanne" "vingt-quatre points" "espace apres paragraphe"; do
    if ! tr '\n' ' ' < "$travail/sortie.txt" | grep -qF "$mot"; then
        echo "ÉCHEC : « $mot » ne se retrouve pas dans le PDF." >&2
        exit 1
    fi
done

# ---------------------------------------------------------------------------- 2

pdftotext -bbox-layout "$travail/sortie.pdf" "$travail/boites.xml"

# Une ligne du relevé par ligne composée : « xMin yMin xMax ».
sed -n 's/.*<line xMin="\([0-9.]*\)" yMin="\([0-9.]*\)" xMax="\([0-9.]*\)".*/\1 \2 \3/p' \
    "$travail/boites.xml" > "$travail/lignes.txt"

if [ "$(wc -l < "$travail/lignes.txt")" -lt 15 ]; then
    echo "ÉCHEC : le relevé de lignes est vide ou tronqué." >&2
    exit 1
fi

# Les lignes d'un bloc, repérées par la bande verticale qu'il occupe sur la page.
bande() { awk -v a="$1" -v b="$2" '$2 >= a && $2 < b' "$travail/lignes.txt"; }

# Un nombre attendu, avec sa tolérance, ou l'échec de la recette.
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

# La liste à puces : la première ligne de chaque paragraphe pend à gauche des suivantes.
# On mesure l'écart entre le fer le plus à gauche et le plus à droite du bloc.
pendu="$(bande 55 175 | awk 'NR==1{n=$1;x=$1} {if($1<n)n=$1; if($1>x)x=$1} END{print x-n}')"
verifier "retrait negatif : la 1re ligne pend de (pt)" "$pendu" 18 0.25

# Le témoin, sans retrait : toutes ses lignes au même fer. C'est lui qui rend la mesure
# ci-dessus lisible — il montre que la méthode ne fabrique pas d'écart toute seule.
plat="$(bande 175 295 | awk 'NR==1{n=$1;x=$1} {if($1<n)n=$1; if($1>x)x=$1} END{print x-n}')"
verifier "temoin sans retrait : ecart entre fers (pt)" "$plat" 0 0.25

# Le paragraphe en retrait, au milieu d'un bloc justifié : il rentre à gauche…
retrait="$(bande 295 445 | awk 'NR==1{n=$1;x=$1} {if($1<n)n=$1; if($1>x)x=$1} END{print x-n}')"
verifier "retrait de paragraphe : bord gauche (pt)" "$retrait" 24 0.25

# … et sa justification vise la largeur rognée, donc son bord droit rentre d'autant.
# C'est le piège annoncé du poste : une ligne justifiée en retrait dont la cible serait
# restée la largeur du bloc s'étirerait jusque sous ses voisines.
bordBloc="$(bande 295 445 | awk '$1 < 70 && $3 > m { m = $3 } END { print m }')"
bordRetrait="$(bande 295 445 | awk '$1 > 70 && $3 > m { m = $3 } END { print m }')"
verifier "retrait de paragraphe : bord droit justifie (pt)" \
    "$(awk -v a="$bordBloc" -v b="$bordRetrait" 'BEGIN { print a - b }')" 24 0.25

# L'espacement, mesuré par différence avec un bloc identique qui n'en porte pas :
# l'avance d'une ligne à l'autre n'a pas à être connue pour que l'écart le soit.
avance() { bande "$1" "$2" | awk 'NR==1{p=$2;next} {s += $2 - p; p = $2; n++} END {print s/n}'; }
verifier "espace apres le paragraphe (pt)" \
    "$(awk -v a="$(avance 445 565)" -v b="$(avance 565 685)" 'BEGIN { print a - b }')" 12 0.25

# ---------------------------------------------------------------------------- 3

# L'écran, repères éteints : ils ne s'impriment jamais.
"$banc" rendre "$document" --sortie "$travail/ecran.png" \
        --page 1 --pixels-par-point "$pp" --sans-reperes > /dev/null
pdftoppm -r "$((72 * pp))" -f 1 -l 1 -png -singlefile "$travail/sortie.pdf" "$travail/pdf"

geometrie="$(magick "$travail/ecran.png" -format "%wx%h" info:)"
largeurPx="${geometrie%x*}"
magick "$travail/pdf.png" -background white -extent "$geometrie" "$travail/pdf-cadre.png"

# La boîte englobante de l'encre dans une bande horizontale, en pixels : « l h x y ».
# « %@ » la rend sans avoir à rogner l'image, et le seuil de 30 % écarte le lissage des
# bords sans écarter le trait lui-même.
boite() {   # <image> <haut en points> <hauteur en points>
    magick "$1" -crop "${largeurPx}x$(( $3 * pp ))+0+$(( $2 * pp ))" +repage \
           -fuzz 30% -format "%@" info: | sed 's/[x+]/ /g'
}

fer()     { boite "$1" "$2" "$3" | awk '{ print $3 }'; }
bordD()   { boite "$1" "$2" "$3" | awk '{ print $3 + $1 }'; }
hauteurE(){ boite "$1" "$2" "$3" | awk '{ print $2 }'; }

# Les quatre mesures du document, prises sur l'encre d'une image, en pixels. Les bandes
# sont écrites en points et relevées sur le PDF : chacune ne contient qu'une ligne, ou
# qu'un paragraphe, et rien de son voisin.
mesures() {   # <image>
    local i="$1"
    echo "$(( $(fer "$i" 76 15) - $(fer "$i" 196 15) ))" \
         "$(( $(fer "$i" 332 47) - $(fer "$i" 300 15) ))" \
         "$(( $(bordD "$i" 300 15) - $(bordD "$i" 332 47) ))" \
         "$(( $(hauteurE "$i" 450 100) - $(hauteurE "$i" 570 100) ))"
}

read -r ecranPendu ecranGauche ecranDroit ecranEspace <<< "$(mesures "$travail/ecran.png")"
read -r pdfPendu pdfGauche pdfDroit pdfEspace <<< "$(mesures "$travail/pdf-cadre.png")"

# La hauteur d'encre du bloc espacé dépasse celle du témoin de deux espaces, non d'un :
# trois paragraphes d'une ligne, deux intervalles.
for mesure in "retrait negatif:$ecranPendu:$pdfPendu:18" \
              "retrait gauche:$ecranGauche:$pdfGauche:24" \
              "retrait droit:$ecranDroit:$pdfDroit:24" \
              "espacement (deux intervalles):$ecranEspace:$pdfEspace:24"; do
    IFS=: read -r libelle ecran pdf attendu <<< "$mesure"
    verifier "ecran — $libelle (px)" "$ecran" "$(( attendu * pp ))" "$seuil"
    verifier "PDF   — $libelle (px)" "$pdf" "$(( attendu * pp ))" "$seuil"
    verifier "ecart ecran/PDF — $libelle (px)" "$(( ecran - pdf ))" 0 "$seuil"
done

# Le filet grossier, pour finir : il n'éprouve rien de ce qui précède, mais il attrape
# ce que les bandes ne regardent pas — un bloc qui cesserait d'être dessiné. 2,94 %
# mesurés le 6 septembre 2026, entièrement sur le lissage des glyphes d'une page de
# texte dense ; le seuil laisse ce qu'il faut au-dessus et rien de plus.
Scripts/comparer-rendus.sh "$document" 1 4.0

echo "R2 : aller-retour identique, retraits et espacement mesurés au PDF, écran d'accord."
