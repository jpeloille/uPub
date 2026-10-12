#!/usr/bin/env bash
#
# Recette du poste Q13 — couper la ligne au même mot à l'écran et au PDF.
#
# L'écran mesure les mots avec Avalonia, l'export avec PDFsharp, et les deux passent
# par le même compositeur. Avant ce poste, chacun décidait de ses coupures à sa propre
# mesure, et la mesure de l'écran dépendait de l'histoire du processus : DejaVu Sans
# n'a pas d'italique ici, et Avalonia lui donnait la face déjà chargée — le gras si le
# gras était venu le premier. Désormais :
#
#   - les polices du texte des documents sont résolues par MyPublisher, une fois par
#     variante, sur le fichier que fontconfig choisit pour l'export, avec ses
#     simulations (PolicesDuDocument) ; l'italique absent se penche des deux côtés ;
#   - la coupure se décide sur la plus large des deux mesures, et chacun dessine à la
#     sienne.
#
# Ce qu'elle éprouve :
#
#   1. les tests purs du noyau — la décision lit la mesure de coupure, le dessin la
#      sienne ; la règle du PDF hors page mesure au bit près comme sur la page ; une
#      face absente se simule sur le fichier choisi, et pas une face présente ;
#   2. les tests de l'interface : les deux vraies règles coupent aux mêmes mots sur
#      l'exemple et les deux documents ATR ; la contre-épreuve, où chaque règle décide
#      seule ; l'italique qui sort penché et jamais gras, le gras chargé d'abord ;
#      trois PROCESSUS qui chargent les faces dans trois ordres et relèvent les mêmes
#      lignes, au bit près ; l'export du MODÈLE DE VUE, relu par pdftotext, qui coupe
#      comme l'écran là où l'écran mesure plus large que le PDF ; et, sans la porte
#      privée d'Avalonia, un « --export » qui refuse et une fenêtre qui se replie ;
#   3. « comparer-mots.sh » — le banc contre le PDF du vrai binaire, mot à mot — sur
#      les deux pages de l'exemple et sur toutes celles des deux documents ATR, à zéro
#      ligne coupée autrement, et cela trois fois : le banc charge d'abord le romain,
#      puis le gras, puis l'italique ;
#   4. « comparer-mots.sh » sur le document de la recette, Samples/recette-coupure.mypub,
#      à zéro ligne coupée autrement. Il porte les cas que le corpus ne porte pas,
#      trouvés à la relecture de Q13 : 48 blocs aux largeurs en demi-points, de 150,4 à
#      173,9 pt, où l'arrondi de la mise en page faisait composer l'écran sur une autre
#      largeur que le PDF (19 lignes coupées autrement) ; et, page 3, quatre blocs en
#      Noto Serif dont la largeur tombe entre la mesure du PDF et celle de l'écran —
#      PLUS LARGE ici —, l'un d'eux par la police du jeu du document. Un « --export »
#      qui oublierait la mesure de l'écran y coupe autrement ;
#   5. contre-épreuve du point 3 — l'écran coupant à sa seule mesure, ce que faisait
#      le dépôt avant ce poste, comparer-mots.sh tombe sur la première colonne de
#      l'exemple, là où le défaut a été trouvé.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

# Le lecteur des .pub, cherché comme la recette R5 le cherche : les tests et la
# comparaison ouvrent les deux documents ATR.
outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$outil" ] || [ ! -x "$outil" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable." >&2
    echo "    sudo apt install libmspub-tools    ou    Scripts/outils-pub.sh" >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$outil"
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

# Le banc ne se compile pas par « dotnet test » : on compile toute la solution.
if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln --nologo -v q > /dev/null 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    exit 1
fi

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# « dotnet test » sort en 0 quand AUCUN test ne correspond au filtre : on compte ceux
# qui ont passé, et l'on exige le nombre attendu.
jouer() {
    local projet="$1" attendus="$2" quoi="$3"
    if ! dotnet test "$projet" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
            --nologo -v q --filter "poste=Q13" > "$travail/tests.txt" 2>&1; then
        echo "ÉCHEC : les $quoi ne passent pas." >&2
        sed 's/^/    /' "$travail/tests.txt" >&2
        exit 1
    fi
    local joues
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
    if [ -z "$joues" ] || [ "$joues" -lt "$attendus" ]; then
        echo "ÉCHEC : ${joues:-0} $quoi joué(s), $attendus attendus." >&2
        sed 's/^/    /' "$travail/tests.txt" >&2
        exit 1
    fi
    echo "$joues"
}

# ------------------------------------------------------------------------------- 1

joues="$(jouer MyPublisher.Tests/MyPublisher.Tests.csproj 7 "tests du noyau")"
echo "  1. le noyau : $joues tests purs joués."

# ------------------------------------------------------------------------------- 2

joues="$(jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj 7 "tests d'interface")"
echo "  2. l'interface : $joues tests joués, dont trois processus aux faces chargées dans trois ordres,"
echo "     l'export du modèle de vue relu par pdftotext, et le repli sans la porte privée."

# ------------------------------------------------------------------------------- 3

exemple="Samples/exemple-bulletin.mypub"
ata="MyPublisher/Books/ATA 21 - Air conditioning.pub"
fiches="MyPublisher/Books/Fiches limitations et procédures d'urgences.pub"

comparer() {
    local ordre="$1" document="$2" page="$3"
    if ! MYPUBLISHER_CHARGER_D_ABORD="$ordre" Scripts/comparer-mots.sh "$document" "$page" \
            > "$travail/mots.txt" 2>&1; then
        echo "ÉCHEC : $(basename "$document"), page $page, faces chargées d'abord : $ordre —" >&2
        sed 's/^/    /' "$travail/mots.txt" >&2
        exit 1
    fi
    # Le banc dit l'ordre qu'il a suivi : sans cela, une variable mal transmise ferait
    # jouer trois fois le même passage.
    if ! grep -q "faces chargées d'abord : $ordre" "$travail/mots.txt"; then
        echo "ÉCHEC : le banc n'a pas chargé les faces dans l'ordre « $ordre »." >&2
        sed 's/^/    /' "$travail/mots.txt" >&2
        exit 1
    fi
    echo "       $(basename "$document") : $(tail -1 "$travail/mots.txt" | sed 's/ Banc : .*//')"
}

for ordre in romain gras italique; do
    echo "  3. faces chargées d'abord : $ordre"
    comparer "$ordre" "$exemple" 1
    comparer "$ordre" "$exemple" 2
    comparer "$ordre" "$ata" tout
    comparer "$ordre" "$fiches" tout
done

# ------------------------------------------------------------------------------- 4

recette="Samples/recette-coupure.mypub"
if ! Scripts/comparer-mots.sh "$recette" tout > "$travail/recette.txt" 2>&1; then
    echo "ÉCHEC : $(basename "$recette") —" >&2
    sed 's/^/    /' "$travail/recette.txt" >&2
    exit 1
fi
# Le document doit encore porter ses cas : 52 blocs, dont les quatre de la page 3. Un
# document appauvri passerait vert sur moins qu'il ne prétend.
if ! grep -q "dans 52 blocs" "$travail/recette.txt"; then
    echo "ÉCHEC : $(basename "$recette") ne relève plus ses 52 blocs." >&2
    sed 's/^/    /' "$travail/recette.txt" >&2
    exit 1
fi
echo "  4. $(basename "$recette") : $(tail -1 "$travail/recette.txt" | sed 's/ Banc : .*//')"

# ------------------------------------------------------------------------------- 5

if MYPUBLISHER_COUPURE_SEULE=1 Scripts/comparer-mots.sh "$exemple" 1 > "$travail/contre.txt" 2>&1; then
    echo "ÉCHEC : comparer-mots.sh ne voit pas un écran qui coupe à sa seule mesure." >&2
    sed 's/^/    /' "$travail/contre.txt" >&2
    exit 1
fi
if ! grep -q "Colonne de gauche" "$travail/contre.txt" || ! grep -q "CONTRE-ÉPREUVE" "$travail/contre.txt"; then
    echo "ÉCHEC : la contre-épreuve tombe, mais pas sur la première colonne de l'exemple." >&2
    sed 's/^/    /' "$travail/contre.txt" >&2
    exit 1
fi
echo "  5. contre-épreuve : l'écran coupant à sa seule mesure, comparer-mots.sh tombe :"
grep -A2 "Colonne de gauche" "$travail/contre.txt" | head -3 | sed 's/^/    /'

echo "Q13 : l'écran et le PDF coupent leurs lignes aux mêmes mots, quel que soit l'ordre des polices."
