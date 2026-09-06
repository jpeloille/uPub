#!/usr/bin/env bash
#
# Recette des postes A2 (signaler le débordement), A7 (calage vertical) et du rognage
# à l'export. Les trois n'en font qu'un : ils demandent tous la hauteur composée du
# bloc, et les traiter séparément produirait trois mesures.
#
# Elle se joue d'abord sur le RÉEL — « MyPublisher/Books/ATA 21 - Air conditioning.pub »,
# importé et exporté avec les valeurs qui y sont — puis sur un document à elle pour ce
# que le réel ne porte pas. C'est la leçon de la relecture de R2 : « un poste dont la
# recette évite son propre défaut connu n'est pas mesuré, il est mis en scène ».
#
# Ce qu'elle éprouve :
#
#   1. sur ATA 21 : l'export nomme les blocs qui débordent, chiffre de combien, et se
#      TAIT sur les deux blocs dont le texte ne dépasse que dans la marge intérieure
#      basse — un avertissement qui crie au loup ne se lit plus ;
#   2. sur ATA 21 toujours : le PDF ne dessine plus rien sous un bloc trop plein, comme
#      l'écran ne le fait pas, et les deux mesures concordent ;
#   3. sur le document de la recette : les trois calages verticaux sont mesurés dans le
#      PDF, et un texte trop haut pour son bloc reste calé en haut quel que soit le
#      calage demandé — c'est le seul cas que les documents réels portent ;
#   4. la paire d'ATA 21 — margin-left 0,1930in, text-indent −0,2183in, marge intérieure
#      0,0400in — pend hors de sa zone de texte et RESTE dans le bloc : c'est le bloc
#      qu'on rogne, pas la zone ;
#   5. le rognage tourne avec le bloc, et il ne survit pas au texte : le filet de 2 pt
#      d'un bloc trop plein est entier des deux côtés ;
#   6. aucun document livré par le dépôt ne déborde.
#
# Pourquoi le rognage se mesure sur des IMAGES et jamais sur « pdftotext » : un chemin
# de découpe PDF cache le texte, il ne le retire pas du flux. « pdftotext » le trouve
# encore — et c'est tant mieux, la recette de R5 compte dessus. Ce qu'on veut vérifier
# est ce que l'œil voit, donc ce sont des pixels qu'on compte.
#
set -euo pipefail
# Les comparaisons numériques doivent être insensibles à la locale : sous une locale
# française, awk écrit « 17,99 » et le test suivant compare alors une chaîne.
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

document="Samples/recette-debordement.mypub"
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

# Deux pixels par point des deux côtés : le banc rend à 2 px/pt et « pdftoppm -r 144 »
# rend 144 points par pouce sur 72 points typographiques.
pp=2

# Un nombre attendu, avec sa tolérance, ou l'échec de la recette.
verifier() {   # <libellé> <mesuré> <attendu> <tolérance>
    awk -v m="$2" -v a="$3" -v t="$4" -v l="$1" 'BEGIN {
        d = m - a; if (d < 0) d = -d;
        if (d > t) {
            printf "ÉCHEC : %s — %.3f mesuré, %.3f attendu (tolérance %.3f).\n",
                   l, m, a, t > "/dev/stderr";
            exit 1;
        }
        printf "  %-56s %9.3f\n", l, m;
    }'
}

# Le nombre de pixels sombres d'une bande, donnée en POINTS : x, y, largeur, hauteur.
# Le seuil de 50 % écarte le lissage des bords sans écarter le trait lui-même.
noir() {   # <image> <x> <y> <largeur> <hauteur>
    magick "$1" -crop "$(( $4 * pp ))x$(( $5 * pp ))+$(( $2 * pp ))+$(( $3 * pp ))" +repage \
           -colorspace Gray -threshold 50% -negate -format "%[fx:mean*w*h]" info:
}

# L'écran et le PDF d'une même page, à la même échelle et à la même taille.
rendre() {   # <doc.mypub> <page> <préfixe de sortie>
    "$banc" rendre "$1" --sortie "$3-ecran.png" \
            --page "$2" --pixels-par-point "$pp" --sans-reperes > /dev/null
    pdftoppm -r "$((72 * pp))" -f "$2" -l "$2" -png -singlefile "$3.pdf" "$3-brut"
    magick "$3-brut.png" -background white \
           -extent "$(magick "$3-ecran.png" -format '%wx%h' info:)" "$3-pdf.png"
}

# ------------------------------------------------------------------ 1. le réel

# MyPublisher délègue la lecture du format fermé à libmspub. Sans l'outil, la recette
# ne peut rien affirmer du réel : elle le dit et échoue, plutôt que de se déclarer
# verte sur un contrôle qu'elle n'a pas fait.
outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$outil" ] || [ ! -x "$outil" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable." >&2
    echo "    sudo apt install libmspub-tools   —   ou : Scripts/outils-pub.sh" >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$outil"
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

"$binaire" "MyPublisher/Books/ATA 21 - Air conditioning.pub" --resave "$travail/ata.mypub" > /dev/null
"$binaire" "$travail/ata.mypub" --export "$travail/ata.pdf" > /dev/null 2> "$travail/ata.txt"

echo "  ATA 21 — ce que l'export en dit :"

# « Overheat Protection » : 82,13 pt de bloc pour 90,78 pt composés. Le nombre est
# celui du document, pas un nombre choisi.
attendu="Page 1 · Overheat Protection : le texte déborde du bloc de 8,6 pt"
if ! grep -qF "$attendu" "$travail/ata.txt"; then
    echo "ÉCHEC : l'export ne signale pas le débordement d'« Overheat Protection »." >&2
    grep 'déborde' "$travail/ata.txt" >&2 || echo "    (aucun avertissement de débordement)" >&2
    exit 1
fi
echo "    $attendu"

# Et il se tait sur les deux blocs qui ne dépassent que dans leur marge basse : leur
# texte compose 1,5 pt de plus que sa zone, et les 2,88 pt de « fo:padding-bottom »
# le reçoivent. Rien n'est coupé — les signaler serait crier au loup.
for muet in "The aircraft uses external air" "Air inlets location"; do
    if grep -F "déborde" "$travail/ata.txt" | grep -qF "$muet"; then
        echo "ÉCHEC : « $muet » ne dépasse que dans sa marge basse, rien n'y est coupé." >&2
        exit 1
    fi
done
echo "    silence sur les deux blocs que leur marge intérieure basse absorbe"

# Deux blocs portent le même nom, un par page : sans le préfixe de page, les deux
# avertissements seraient indiscernables.
if [ "$(grep -c 'ATA 21 - Air conditioning : le texte déborde' "$travail/ata.txt")" != 2 ] ||
   ! grep -q "^Avertissement : Page 2 · ATA 21 - Air conditioning : le texte déborde" "$travail/ata.txt"; then
    echo "ÉCHEC : les deux blocs homonymes ne sont pas désignés séparément." >&2
    grep 'déborde' "$travail/ata.txt" >&2
    exit 1
fi
echo "    les deux blocs homonymes se distinguent par leur page"

echo "  ATA 21 — ce que le PDF en montre :"

cp "$travail/ata.pdf" "$travail/ata-p1.pdf"
rendre "$travail/ata.mypub" 1 "$travail/ata-p1"

# « Overheat Protection » : x 36, y 571,82, 523,27 × 82,13 — donc bas à 653,95. Sans
# rognage, les 8,6 pt qui dépassent posent de l'encre entre 655 et 672. Rien d'autre
# n'occupe cette bande : c'est le dernier bloc de la page.
for cote in ecran pdf; do
    dedans="$(noir "$travail/ata-p1-$cote.png" 36 630 523 23)"
    dessous="$(noir "$travail/ata-p1-$cote.png" 36 655 523 17)"

    if [ "$dedans" -lt 500 ]; then
        echo "ÉCHEC ($cote) : le bas du bloc « Overheat Protection » ne porte pas de texte." >&2
        echo "La mesure suivante ne prouverait alors rien." >&2
        exit 1
    fi

    verifier "$cote — encre sous le bloc trop plein (px)" "$dessous" 0 20
    echo "  ($cote — encre dans le bas du bloc : $dedans px, la mesure n'est pas à vide)"
done

# ---------------------------------------------------- 2. le document de la recette

echo "  Document de la recette :"

"$binaire" "$document" --resave "$travail/copie.mypub" > /dev/null
if ! diff -u "$document" "$travail/copie.mypub" > "$travail/ecart.diff"; then
    echo "ÉCHEC : relu puis réécrit, le document n'est plus le même." >&2
    head -30 "$travail/ecart.diff" >&2
    exit 1
fi

"$binaire" "$document" --export "$travail/recette.pdf" > /dev/null 2> "$travail/recette.txt"

# Quatre blocs débordent, et quatre seulement : le témoin de même taille et les trois
# blocs de calage tiennent. Un marqueur posé sur tous les blocs passerait sans ce
# compte-là.
declare -a trop=("Trop plein" "Milieu qui deborde" "Haut qui deborde" "Trop plein pivote")
declare -a tiennent=("Temoin qui tient" "Calage haut" "Calage milieu" "Calage bas" "Retrait pendant")

for nom in "${trop[@]}"; do
    if ! grep -qF "Page 1 · $nom : le texte déborde du bloc" "$travail/recette.txt"; then
        echo "ÉCHEC : « $nom » déborde et n'est pas signalé." >&2
        exit 1
    fi
done

for nom in "${tiennent[@]}"; do
    if grep -qF "Page 1 · $nom : le texte déborde" "$travail/recette.txt"; then
        echo "ÉCHEC : « $nom » tient dans son bloc et se signale pourtant." >&2
        exit 1
    fi
done

compte="$(grep -c 'déborde du bloc' "$travail/recette.txt" || true)"
verifier "blocs signalés (sur neuf)" "$compte" 4 0

# --- le calage vertical, MESURÉ dans le PDF.
#
# « pdftotext -bbox » rend la boîte de chaque mot à l'origine du crayon. Les trois
# blocs de calage portent deux lignes et 120 pt de haut ; leur premier mot commence
# par une capitale, si bien que leurs trois sommets d'encre se comparent directement.
pdftotext -bbox "$travail/recette.pdf" "$travail/boites.xml"

sommet() {   # <mot>
    sed -n "s/.*<word xMin=\"[0-9.]*\" yMin=\"\([0-9.]*\)\".*>$1<\/word>.*/\1/p" \
        "$travail/boites.xml" | sort -n | head -1
}
fer() {      # <mot>
    sed -n "s/.*<word xMin=\"\([0-9.]*\)\" yMin=\"\([0-9.]*\)\".*>$1<\/word>.*/\2 \1/p" \
        "$travail/boites.xml" | sort -n | head -1 | awk '{print $2}'
}

# Deux lignes de DejaVu Sans 11 pt à interligne 1 : l'avance est la hauteur naturelle
# de la police. On la mesure sur le bloc calé en haut plutôt que de l'écrire — elle
# dépend de la police installée, et c'est l'écart entre les calages qu'on éprouve.
avance="$(sed -n 's/.*<word xMin="[0-9.]*" yMin="\([0-9.]*\)".*>Haut<\/word>.*/\1/p' \
    "$travail/boites.xml" | sort -n | awk 'NR==1{a=$1} NR==2{print $1-a}')"
reste="$(awk -v a="$avance" 'BEGIN { print 120 - 2 * a }')"

verifier "calage haut : sommet de l'encre (pt)"   "$(sommet Haut)"   160 0.05
verifier "calage milieu : sommet de l'encre (pt)" "$(sommet Milieu)" \
    "$(awk -v r="$reste" 'BEGIN { print 160 + r / 2 }')" 0.05
verifier "calage bas : sommet de l'encre (pt)"    "$(sommet Bas)" \
    "$(awk -v r="$reste" 'BEGIN { print 160 + r }')" 0.05

# Le cas réel des fiches : « draw:textarea-vertical-align: middle » sur un bloc dont le
# texte est plus haut que lui. Centrer escamoterait le début du texte ; les deux blocs
# doivent commencer exactement à la même ligne.
milieu="$(sed -n 's/.*<word xMin="\([0-9.]*\)" yMin="\([0-9.]*\)".*>Ligne<\/word>.*/\2 \1/p' \
    "$travail/boites.xml" | awk '$1 > 300 && $1 < 400 && $2 < 100' | sort -n | head -1 | awk '{print $1}')"
haut="$(sed -n 's/.*<word xMin="\([0-9.]*\)" yMin="\([0-9.]*\)".*>Ligne<\/word>.*/\2 \1/p' \
    "$travail/boites.xml" | awk '$1 > 300 && $1 < 400 && $2 > 250' | sort -n | head -1 | awk '{print $1}')"
verifier "milieu qui déborde : cale en haut, comme son jumeau (pt)" \
    "$(awk -v a="$milieu" -v b="$haut" 'BEGIN { print a - b }')" 0 0.05

# --- la paire d'ATA 21 : elle sort de sa zone de texte et reste dans le bloc.
#
# Bloc à x = 56,69 avec 2,88 pt de marge intérieure : la zone de texte commence à
# 59,57. Retrait gauche 13,896 et retrait de première ligne −15,718 posent la première
# ligne à 57,748 — soit 1,822 pt à GAUCHE de sa zone, et 1,058 pt à l'intérieur du
# bloc. Les lignes suivantes rentrent à 73,466.
verifier "retrait pendant : 1re ligne, hors de la zone (pt)" "$(fer Ooooo)" 57.748 0.05
verifier "retrait pendant : lignes suivantes (pt)"           "$(fer suivantes)" 73.466 0.05

# --- le rognage, sur les images.
cp "$travail/recette.pdf" "$travail/recette-p1.pdf"
rendre "$document" 1 "$travail/recette-p1"

for cote in ecran pdf; do
    image="$travail/recette-p1-$cote.png"

    # « Trop plein » : x 56,69, y 56,69, 160 × 60. Huit lignes pour quatre et demie.
    verifier "$cote — encre sous « Trop plein » (px)" "$(noir "$image" 57 120 160 40)" 0 20

    # Le bloc pivoté de 20° : son empreinte descend à 645,55 pt. Sans rognage, les
    # lignes cachées tomberaient au-delà, en oblique. Une découpe posée en coordonnées
    # de page au lieu du repère du bloc ne se verrait sur aucun autre document.
    verifier "$cote — encre sous le bloc pivoté (px)" "$(noir "$image" 20 650 210 40)" 0 20
done

# Le filet de 2 pt du bloc trop plein est centré sur le bord du bloc à l'export : sa
# moitié basse est DEHORS, au-delà de 116,69 pt. Le découpage du texte ne doit pas lui
# survivre, sans quoi il se réduirait à sa moitié haute — c'est la parade que l'export
# s'était déjà donnée pour le cadrage d'image « remplir », et le contour se trace après
# le texte.
#
# La mesure ne porte que sur le PDF, à dessein : l'écran, lui, RENTRE son filet dans le
# bloc au lieu de le centrer sur le bord — Avalonia décale la géométrie d'une demi-
# épaisseur. Mesuré ici : la moitié basse du filet occupe 115,5–117,5 pt au PDF et
# 115,0–117,0 pt à l'écran. C'est un écart réel entre les deux rendus, antérieur à ce
# poste et hors de son périmètre ; il est consigné dans PLAN.md.
moitie="$(noir "$travail/recette-p1-pdf.png" 57 117 160 1)"
if [ "$moitie" -lt 200 ]; then
    echo "ÉCHEC : la moitié basse du filet manque au PDF — $moitie px sur 640." >&2
    echo "Le découpage du texte a survécu au texte et rogné le contour." >&2
    exit 1
fi
echo "  (PDF — moitié basse du filet, hors du bloc : $moitie px sur 640)"

# Le filet grossier, pour finir : il n'éprouve rien de ce qui précède, mais il attrape
# ce que les bandes ne regardent pas — un bloc qui cesserait d'être dessiné.
Scripts/comparer-rendus.sh "$document" 1 4.0

# ------------------------------------------ 3. aucun document livré ne déborde

# Le garde-fou, et il a servi : quatre blocs du document de référence du contrôle P2 et
# deux de celui de B5 débordaient en silence depuis que R4 a fait de l'interligne un
# multiple de la hauteur naturelle et non du corps. Personne ne l'avait vu parce que
# rien ne le disait.
#
# La mesure est ici celle de PDFsharp ; celle d'Avalonia est éprouvée du côté des tests
# d'interface, sur les mêmes fichiers. Un document livré doit tenir sous les deux.
fautifs=0
for livre in Samples/*.mypub MyPublisher/Templates/*.mypub; do
    [ "$livre" = "$document" ] && continue
    "$binaire" "$livre" --export "$travail/livre.pdf" > /dev/null 2> "$travail/livre.txt"
    if grep -q 'déborde du bloc' "$travail/livre.txt"; then
        echo "ÉCHEC : $livre livre un PDF amputé." >&2
        grep 'déborde du bloc' "$travail/livre.txt" >&2
        fautifs=$((fautifs + 1))
    fi
done
if [ "$fautifs" != 0 ]; then exit 1; fi
echo "  aucun des documents livrés ne déborde"

echo "A2/A7 : débordement signalé sur ATA 21, calages mesurés au PDF, rognage vérifié des deux côtés."
