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
#   1. sur ATA 21 : l'export nomme les blocs qui débordent et chiffre de combien ;
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
#   6. le texte qui ne dépasse que dans la marge intérieure basse ne se signale PAS —
#      un avertissement qui crie au loup ne se lit plus, et c'est le cas de deux des
#      neuf blocs d'ATA 21 ;
#   7. aucun document livré par le dépôt ne déborde.
#
# Les nombres d'ATA 21 sont ceux que **Calibri** donne : c'est la police du document, et
# elle est présente sur la machine qui a écrit cette recette. Ailleurs, fontconfig rend
# autre chose, le texte ne compose plus à la même hauteur, et ces nombres-là ne veulent
# plus rien dire. La recette le voit à l'avertissement de substitution que l'export émet
# déjà (poste B5), et passe son chemin en le disant plutôt que d'inventer un échec. Ce
# qu'elle éprouve alors, elle l'éprouve sur son propre document, en DejaVu Sans — dont
# tout le reste du banc dépend déjà.
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
# Le seuil de 50 % écarte le lissage des bords sans écarter le trait lui-même. Les
# points se convertissent par awk et non par l'arithmétique du shell : la bande la plus
# fine mesurée ici fait deux points et demi, et l'arrondi à l'entier la ferait tomber
# du mauvais côté d'un bord de zone de texte.
noir() {   # <image> <x> <y> <largeur> <hauteur>
    local geometrie
    geometrie="$(awk -v x="$2" -v y="$3" -v w="$4" -v h="$5" -v p="$pp" \
        'BEGIN { printf "%dx%d+%d+%d", w * p, h * p, x * p, y * p }')"
    magick "$1" -crop "$geometrie" +repage \
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

# La police du document est-elle bien celle que la machine emploie ? L'export le dit
# déjà, poste B5. Sinon, tout ce qui suit mesurerait la police de substitution.
if grep -q 'Police substituée : « Calibri »' "$travail/ata.txt"; then
    echo "    Calibri est substituée sur cette machine : les mesures d'ATA 21 sont passées."
    echo "    (le reste de la recette les éprouve en DejaVu Sans, sur son propre document)"
    reel=0
else
    reel=1
fi

if [ "$reel" = 1 ]; then

# ATA 21 ne déborde NULLE PART, et c'est le contrôle.
#
# Cette recette affirmait d'abord le contraire : cinq blocs signalés, dont « Overheat
# Protection » à 8,6 pt. Les cinq étaient faux. libmspub rend la marque de paragraphe
# de Publisher comme du texte — un « insertText (\r) » à la fin de chaque paragraphe —
# et l'import la gardait : celle du dernier paragraphe, n'ayant rien derrière elle,
# devenait une ligne vide fantôme qui comptait dans la hauteur. Les vingt-sept blocs
# des deux documents réels finissaient ainsi.
#
# Le poste R5 écarte désormais ce \r. Plus aucun bloc d'ATA 21 ne déborde, et quatre
# blocs ont perdu le marqueur rouge qu'ils portaient à tort. Si la ligne fantôme
# revenait, ce contrôle-ci tomberait le premier.
deborde=$(grep -c 'déborde du bloc' "$travail/ata.txt" || true)
if [ "$deborde" != 0 ]; then
    echo "ÉCHEC : ATA 21 ne doit déborder nulle part, $deborde bloc(s) signalé(s)." >&2
    grep 'déborde' "$travail/ata.txt" >&2
    echo "    La marque de paragraphe de Publisher est-elle revenue dans le texte ?" >&2
    exit 1
fi
echo "    aucun débordement — la ligne fantôme de la marque de paragraphe est écartée"

# Les deux blocs tournés d'un quart de tour arrivent tournés. L'import les posait
# droits sans un mot ; c'est la relecture de A1 qui l'a relevé.
tournes=$(python3 -c "
import json, sys
d = json.load(open('$travail/ata.mypub'))
print(sum(1 for p in d['Pages'] for f in p['Frames'] if f.get('Rotation')))
")
if [ "$tournes" != 2 ]; then
    echo "ÉCHEC : ATA 21 porte deux blocs tournés, $tournes retrouvé(s)." >&2
    exit 1
fi
echo "    les deux blocs tournés d'un quart de tour le sont encore"

fi

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
declare -a trop=("Trop plein" "Milieu qui deborde" "Haut qui deborde" "Trop plein pivote"
                 "Marge basse depassee")
# « Marge basse absorbe » est le cas d'ATA 21 porté sur une police que le dépôt possède :
# quatre lignes de 12,80 pt dans une zone de 50, soit 1,22 pt de trop, que les 3 pt de
# marge intérieure basse reçoivent. Rien n'est coupé, rien ne doit être dit. Son jumeau
# « Marge basse depassee », de trois points plus court, sort du bloc d'autant et se dit.
declare -a tiennent=("Temoin qui tient" "Calage haut" "Calage milieu" "Calage bas"
                     "Retrait pendant" "Marge basse absorbe")

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
verifier "blocs signalés (sur onze)" "$compte" 5 0

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
verifier "retrait pendant : 1re ligne, hors de la zone (pt)" "$(fer Nnnnn)" 57.748 0.05
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

    # La paire d'ATA 21, mesurée cette fois sur des PIXELS et non sur « pdftotext » :
    # celui-ci trouve le texte que la découpe cache, il ne peut donc rien dire d'elle.
    # La première ligne pend jusqu'à 57,748 pt, à gauche de sa zone de texte qui
    # commence à 59,57. Une découpe posée sur la zone au lieu du bloc l'effacerait ; la
    # deuxième ligne, qui rentre à 73,466, ne pose rien dans cette bande-là et le
    # prouve.
    pendante="$(noir "$image" 57 433 2.5 12)"
    if [ "$pendante" -lt 4 ]; then
        echo "ÉCHEC ($cote) : la première ligne pendante est rognée — $pendante px." >&2
        echo "C'est le BLOC qu'on découpe, pas la zone de texte." >&2
        exit 1
    fi
    verifier "$cote — même bande, ligne rentrée : rien (px)" "$(noir "$image" 57 446 2.5 12)" 0 0
    echo "  ($cote — encre de la ligne pendante hors de sa zone : $pendante px)"
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

echo "A2/A7 : ATA 21 ne déborde nulle part, calages mesurés au PDF, rognage vérifié des deux côtés."
