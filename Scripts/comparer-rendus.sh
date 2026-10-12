#!/usr/bin/env bash
#
# Compare ce que l'écran dessine à ce que le PDF imprime, page par page.
#
# C'est la vérification que les postes C1, C2a, C2b, C3c et C6 ont tous exigée, et
# qu'aucun n'a laissée derrière lui : « rendus des deux côtés à deux pixels par
# point ». Elle existe désormais en une commande.
#
# Deux pixels par point des deux côtés : le banc rend la page à 2 px/pt, et
# « pdftoppm -r 144 » rend 144 points par pouce sur 72 points typographiques, soit
# exactement les mêmes deux pixels. Aucun facteur de conversion n'entre dans la
# comparaison.
#
#   Scripts/comparer-rendus.sh <doc.mypub> [page] [tolérance %]
#
# La tolérance par défaut vient d'un relevé, pas d'un souhait : le poste C1 avait
# mesuré 2,4 % de pixels différents entre les deux rendus, tous sur le lissage des
# bords, le texte et le repère de marge propre à l'écran (MyPublisher/Docs/PLAN.md).
# On compare donc les repères éteints, ce qui retire la seule différence voulue, et
# l'on garde de la marge pour le lissage.
#
# ATTENTION — sur une page portant du texte, cette comparaison ÉCHOUE aujourd'hui, et
# c'est une vraie trouvaille, pas un défaut du banc. Elle a d'abord trouvé que
# l'interligne n'était pas appliqué à l'export : R4 l'a corrigé, et la page 1 du
# document d'exemple est passée de 8 % à 4,87 %. Il reste que l'écran et le PDF
# mesurent les mots chacun avec sa bibliothèque, et coupent une ligne de la première
# colonne à un mot différent (cf. MyPublisher/Docs/PLAN.md, poste Q13). Formes seules :
# 0,38 %. C'est pourquoi ce script n'est pas appelé en bloc par verifier.sh, mais
# recette par recette, avec son document et sa tolérance.
#
set -euo pipefail

# Les comparaisons numériques et les formats d'ImageMagick doivent être insensibles
# à la locale : sous une locale française, awk écrit « 7,97 » et les tests suivants
# comparent alors une chaîne, en silence.
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$racine"

document="${1-}"
page="${2-1}"
tolerance="${3-3.0}"

if [ -z "$document" ] || [ ! -f "$document" ]; then
    echo "Usage : $0 <doc.mypub> [page] [tolérance %]" >&2
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

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# L'écran, repères éteints : ils ne s'impriment jamais, les comparer au PDF
# reviendrait à mesurer une différence qu'on a demandée.
"$banc" rendre "$document" --sortie "$travail/ecran.png" \
        --page "$page" --pixels-par-point 2 --sans-reperes > "$travail/banc.txt"

# Le PDF, par le même binaire que celui qu'utilise l'utilisateur.
"$application" "$document" --export "$travail/sortie.pdf" > /dev/null 2> "$travail/export.txt"
pdftoppm -r 144 -f "$page" -l "$page" -png -singlefile \
         "$travail/sortie.pdf" "$travail/pdf"

# Les deux moteurs n'arrondissent pas la taille de page au même pixel : on ramène
# le PDF sur la géométrie de l'écran plutôt que de refuser de comparer pour un
# pixel de bord. Le fond est blanc, comme la page.
geometrie="$(magick "$travail/ecran.png" -format "%wx%h" info:)"
magick "$travail/pdf.png" -background white -extent "$geometrie" "$travail/pdf-cadre.png"

# Le total se calcule ici et non par « %[fx:w*h] » : passé le million, ImageMagick
# l'écrit en notation scientifique (« 2.00733e+06 pixels comparés »), même sous int().
read -r largeur hauteur < <(magick "$travail/ecran.png" -format "%w %h" info:)
total=$((largeur * hauteur))
# « -fuzz 12% » : ce qu'on cherche est un décalage de forme, pas une nuance de
# lissage. Sans ce seuil, chaque bord de lettre compterait pour une différence.
# ImageMagick 7 écrit « 501819 (0.249994) » : le compte, puis la même chose
# normalisée. On ne garde que le compte.
differents="$(magick compare -metric AE -fuzz 12% \
    "$travail/ecran.png" "$travail/pdf-cadre.png" null: 2>&1 | awk '{print $1}' || true)"

# Passé le million, le compte vient lui aussi en notation scientifique (« 1.70797e+06 »),
# comme l'encre plus bas : la forme est acceptée, awk sait la lire.
if ! [[ "$differents" =~ ^[0-9]+(\.[0-9]+)?(e[+-]?[0-9]+)?$ ]]; then
    echo "ÉCHEC : la comparaison n'a pas rendu un nombre de pixels ($differents)." >&2
    exit 1
fi

ecart="$(awk -v d="$differents" -v t="$total" 'BEGIN { printf "%.2f", 100 * d / t }')"

echo "page $page : $ecart % de pixels différents (seuil $tolerance %, $total pixels comparés)"

# Un écart nul était refusé d'office : il signalait qu'on comparait une image à
# elle-même, ou deux images vides. Il est devenu un résultat possible au poste Q14 :
# le panneau ne réécrit plus la largeur de la page ni la géométrie du bloc qu'on
# sélectionne, et le document tourné de la recette A1 sort au pixel près des deux
# côtés — 0 pixel différent sur 48 000 pixels d'encre. Ce sont donc les deux
# soupçons eux-mêmes qu'on vérifie, et non plus leur symptôme : deux fichiers
# identiques octet pour octet, ou un rendu sans encre.
if cmp -s "$travail/ecran.png" "$travail/pdf-cadre.png"; then
    echo "ÉCHEC : les deux rendus sont le même fichier, octet pour octet." >&2
    exit 1
fi
magick -size "$geometrie" xc:white "$travail/blanc.png"
for rendu in ecran pdf-cadre; do
    encre="$(magick compare -metric AE -fuzz 12% \
        "$travail/$rendu.png" "$travail/blanc.png" null: 2>&1 | awk '{print $1}' || true)"
    # Au-delà d'un million, ImageMagick écrit le compte en notation scientifique
    # (« 1.70797e+06 ») : la forme est acceptée, awk sait la lire.
    if ! [[ "$encre" =~ ^[0-9]+(\.[0-9]+)?(e[+-]?[0-9]+)?$ ]]; then
        echo "ÉCHEC : l'encre du rendu « $rendu » n'a pas été comptée ($encre)." >&2
        exit 1
    fi
    if awk -v n="$encre" 'BEGIN { exit !(n + 0 == 0) }'; then
        echo "ÉCHEC : le rendu « $rendu » de la page $page ne porte aucune encre ($encre)." >&2
        echo "Les deux rendus sont-ils vraiment produits ?" >&2
        exit 1
    fi
done

if awk -v e="$ecart" -v t="$tolerance" 'BEGIN { exit !(e > t) }'; then
    echo "ÉCHEC : l'écran et le PDF ont divergé." >&2
    magick compare -metric AE -fuzz 12% \
        "$travail/ecran.png" "$travail/pdf-cadre.png" "$racine/ecart-page$page.png" 2>/dev/null || true
    echo "Différences visibles dans ecart-page$page.png" >&2
    exit 1
fi
