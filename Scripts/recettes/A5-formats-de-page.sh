#!/usr/bin/env bash
#
# Recette du poste A5 — formats de page.
#
# Ce qu'elle éprouve, sur « Samples/recette-formats.mypub », une page de 100 × 200 mm
# — le format que le plan donne pour recette, et qui ne figure dans aucun catalogue :
#
#   1. le document se relit et se réécrit au caractère près ;
#   2. le PDF exporté mesure EXACTEMENT 283,46 × 566,93 pt, soit 100 × 200 mm ;
#   3. le contenu est posé aux bonnes coordonnées dans cette page-là — un export qui
#      aurait gardé le gabarit A4 en décalant tout donnerait la bonne taille de page
#      et le mauvais placement ;
#   4. l'écran rend la même page à la même taille, à deux pixels par point ;
#   5. les quatre formats ajoutés — B4, B5, Executive, Tabloid — sortent aux
#      dimensions de leur norme ;
#   6. le panneau tient le format libre : le sélecteur se vide sans rien effacer, et
#      revenir à un format du catalogue ne coûte qu'un pas d'annulation. Joué par
#      « MyPublisher.Tests.Interface », faute de fenêtre dans un shell.
#
# Ce que le point 6 ajoute a été mesuré, et il n'est pas ce qu'on croyait. Le point 4
# ouvre une VRAIE fenêtre — le banc de rendu en est une — et il attrape donc déjà un
# sélecteur qui reposerait l'A4 devant un format libre : simulé, il fait sortir l'écran
# en 1192 × 1684 au lieu de 568 × 1134. Ce qu'il n'attrape pas, c'est ce qui demande un
# geste : le banc ouvre et rend, il ne choisit jamais de format. Un « OnSelectedPageSize
# Changed » qui ne ferait plus rien laisse les huit mesures ci-dessus intactes, et seul
# le point 6 tombe. C'est là sa raison d'être, et non celle qu'on lui prêtait.
#
# Le point 5 mérite un mot. Le shell n'a pas accès à « PageSize.All » : ce que le
# catalogue contient est épinglé par « FormatsDePageTests » et par le test d'interface
# qui relit le sélecteur. Ce que cette recette ajoute, et qu'aucun test ne dit, c'est
# que ces dimensions-là traversent l'export jusqu'au fichier livré — les documents sont
# dérivés du même échantillon, dont on ne change que les deux nombres, ce qui isole
# exactement la variable mesurée.
#
# La mesure vient de « pdfinfo », qui lit la boîte de page déclarée dans le PDF, et de
# « pdftotext -bbox », qui la redit à sa façon en même temps qu'il rend la boîte du
# texte. Deux lectures indépendantes du même nombre : un export qui écrirait la taille
# dans l'en-tête sans la respecter passerait la première et pas la seconde.
#
set -euo pipefail
# Les comparaisons numériques doivent être insensibles à la locale : sous une locale
# française, awk écrit « 283,46 » et le test suivant compare alors une chaîne.
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

document="Samples/recette-formats.mypub"
binaire="MyPublisher/bin/Debug/net9.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net9.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile, plutôt que de se contenter de ce qui traîne dans « bin/ ».
#
# C'est le piège que R3 a relevé : un binaire périmé survit à une modification des
# sources, et la recette se déclare verte sur du code qu'elle n'a pas éprouvé. Elle est
# faite pour être rejouée seule, y compris par quelqu'un qui vient de saboter un
# fichier ; elle compile donc elle-même, comme « Scripts/controle.sh » depuis P2.
if ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

for outil in "$binaire" "$banc"; do
    if [ ! -x "$outil" ]; then
        echo "ÉCHEC : binaire introuvable après compilation ($outil)." >&2
        echo "Le cadriciel cible du .csproj a-t-il changé ?" >&2
        exit 1
    fi
done

echec=0

# Tolérance : un centième de point. Le format est écrit en millimètres dans le modèle
# et converti une seule fois ; ce qui sort du PDF doit tomber sur le même nombre.
tolerance=0.01

comparer() {
    local libelle="$1" attendu="$2" obtenu="$3"
    if awk -v a="$attendu" -v o="$obtenu" -v t="$tolerance" \
        'BEGIN { exit ((a - o) < t && (o - a) < t) ? 0 : 1 }'; then
        printf '    %-34s %10.3f   mesuré %10.3f\n' "$libelle" "$attendu" "$obtenu"
    else
        printf '    %-34s %10.3f   mesuré %10.3f   <- ÉCART\n' "$libelle" "$attendu" "$obtenu" >&2
        echec=1
    fi
}

# --------------------------------------------------------------- 1. aller-retour

"$binaire" "$document" --resave "$travail/copie.mypub" > /dev/null
if ! diff -q "$document" "$travail/copie.mypub" > /dev/null; then
    echo "ÉCHEC : le document ne se réécrit pas à l'identique." >&2
    diff "$document" "$travail/copie.mypub" | head -20 >&2
    exit 1
fi

# ------------------------------------------------- 2 et 3. le PDF du format libre

"$binaire" "$document" --export "$travail/libre.pdf" > /dev/null
pdfinfo "$travail/libre.pdf" > "$travail/libre.txt"

# « Page size: 283.465 x 566.929 pts »
largeur="$(sed -n 's/^Page size: *\([0-9.]*\) x \([0-9.]*\).*/\1/p' "$travail/libre.txt")"
hauteur="$(sed -n 's/^Page size: *\([0-9.]*\) x \([0-9.]*\).*/\2/p' "$travail/libre.txt")"

echo "  Format libre, 100 × 200 mm :"
comparer "largeur de page (pt)" 283.465 "$largeur"
comparer "hauteur de page (pt)" 566.929 "$hauteur"

# Le contenu, mesuré dans cette page-là. Le bloc de texte est posé à X = 40, Y = 40 ;
# « pdftotext -bbox » rend la boîte de chaque mot à l'origine du crayon, si bien que
# l'abscisse gauche est celle du bloc et l'ordonnée haute celle de la ligne.
pdftotext -bbox "$travail/libre.pdf" "$travail/libre.xml"

lu="$(python3 - "$travail/libre.xml" <<'PY'
import re, sys

texte = open(sys.argv[1], encoding="utf-8").read()
page = re.search(r'<page width="([0-9.]+)" height="([0-9.]+)"', texte)
mot = re.search(r'<word xMin="([0-9.]+)" yMin="([0-9.]+)"[^>]*>REPERE</word>', texte)

if page is None or mot is None:
    print("ABSENT")
else:
    print(page.group(1), page.group(2), mot.group(1), mot.group(2))
PY
)"

if [ "$lu" = "ABSENT" ]; then
    echo "ÉCHEC : le mot « REPERE » n'a pas été retrouvé dans le PDF." >&2
    exit 1
fi

set -- $lu
comparer "page relue par pdftotext (l, pt)" 283.465 "$1"
comparer "page relue par pdftotext (h, pt)" 566.929 "$2"
# Le bloc est posé à X = 40 et porte les 4 pt de marge intérieure par défaut : le
# crayon part donc de 44. C'est le nombre qui dit que la page n'a pas été composée sur
# un gabarit A4 puis recadrée — un décalage de mise en page se lirait ici.
comparer "abscisse du crayon (pt)" 44.000 "$3"
# Le sommet de la boîte du mot est celui de la ligne, non celui du bloc : sous les 4 pt
# de marge haute, le texte descend encore de ce que DejaVu Sans 14 pt réclame. On
# encadre plutôt qu'on ne fixe — ce qu'on mesure ici est le placement du bloc, non la
# métrique de la police, qui est l'affaire des recettes de R2 et R4.
if awk -v y="$4" 'BEGIN { exit (y >= 44 && y <= 50) ? 0 : 1 }'; then
    printf '    %-34s %10s   mesuré %10.3f\n' "ordonnée du crayon (pt)" "[44 ; 50]" "$4"
else
    printf '    %-34s %10s   mesuré %10.3f   <- ÉCART\n' "ordonnée du crayon (pt)" "[44 ; 50]" "$4" >&2
    echec=1
fi

# ------------------------------------------------------------------ 4. l'écran

"$banc" rendre "$document" --sortie "$travail/libre.png" --pixels-par-point 2 --sans-reperes \
    > "$travail/banc.txt"

# « 568x1134 pixels écrits dans … ». Deux pixels par point, mais la mise en page
# arrondit d'abord la planche au pixel entier : 283,465 pt sortent en 284 pt de large,
# soit 568 pixels, et 566,929 en 567, soit 1134. On admet donc un pixel de mise en
# page — deux pixels d'image — de part et d'autre, ce qui laisse tout de même la
# mesure séparer 100 mm de n'importe quel format du catalogue.
pixels="$(sed -n 's/^\([0-9]*\)x\([0-9]*\) pixels.*/\1 \2/p' "$travail/banc.txt")"
set -- $pixels
comparer_a_deux_pixels() {
    local libelle="$1" attendu="$2" obtenu="$3"
    if awk -v a="$attendu" -v o="$obtenu" 'BEGIN { exit ((a - o) <= 2 && (o - a) <= 2) ? 0 : 1 }'; then
        printf '    %-34s %10.3f   mesuré %10s\n' "$libelle" "$attendu" "$obtenu"
    else
        printf '    %-34s %10.3f   mesuré %10s   <- ÉCART\n' "$libelle" "$attendu" "$obtenu" >&2
        echec=1
    fi
}
comparer_a_deux_pixels "écran, largeur (px à 2/pt)" 566.929 "$1"
comparer_a_deux_pixels "écran, hauteur (px à 2/pt)" 1133.858 "$2"

# ------------------------------------------------- 5. les quatre formats ajoutés

echo "  Formats ajoutés au catalogue :"
for entree in "B4:708.66:1000.63" "B5:498.90:708.66" "Executive:522.00:756.00" "Tabloid:792.00:1224.00"; do
    nom="${entree%%:*}"
    reste="${entree#*:}"
    attenduL="${reste%%:*}"
    attenduH="${reste#*:}"

    python3 - "$document" "$travail/$nom.mypub" "$attenduL" "$attenduH" <<'PY'
import json, sys

source, cible, largeur, hauteur = sys.argv[1:5]
document = json.load(open(source, encoding="utf-8"))
document["PageWidth"] = float(largeur)
document["PageHeight"] = float(hauteur)
json.dump(document, open(cible, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY

    # Repassé par le sérialiseur avant l'export : ce qu'on mesure est bien un document
    # que l'application a écrit, et non un JSON bricolé par la recette.
    "$binaire" "$travail/$nom.mypub" --resave "$travail/$nom-relu.mypub" > /dev/null
    "$binaire" "$travail/$nom-relu.mypub" --export "$travail/$nom.pdf" > /dev/null
    pdfinfo "$travail/$nom.pdf" > "$travail/$nom.txt"

    obtenuL="$(sed -n 's/^Page size: *\([0-9.]*\) x \([0-9.]*\).*/\1/p' "$travail/$nom.txt")"
    obtenuH="$(sed -n 's/^Page size: *\([0-9.]*\) x \([0-9.]*\).*/\2/p' "$travail/$nom.txt")"

    comparer "$nom — largeur (pt)" "$attenduL" "$obtenuL"
    comparer "$nom — hauteur (pt)" "$attenduH" "$obtenuH"
done

if [ "$echec" != 0 ]; then
    echo "A5 : ÉCHEC." >&2
    exit 1
fi

# --------------------------------------------------------------- 6. le panneau

# Ce qui précède ouvre une fenêtre (le banc, au point 4) mais ne fait aucun geste
# dedans. Choisir un format dans le sélecteur, compter les pas d'annulation, relire ce
# que le sélecteur affiche : rien de tout cela ne s'obtient d'un shell. Vérifié en
# faisant échouer les deux côtés séparément — voir l'en-tête.
attendus=5
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj \
        --nologo -v q --filter "poste=A5" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : le panneau ne tient pas le format libre." >&2
    exit 1
fi

joues="$(grep -o 'Passed: *[0-9]*' "$travail/tests.txt" | head -1 | grep -o '[0-9]*$' || true)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=A5 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi

echo "  Panneau éprouvé sur la vraie fenêtre : $joues tests"
echo "A5 : le format libre et les quatre formats ajoutés sortent aux dimensions demandées."
