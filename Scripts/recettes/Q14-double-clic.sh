#!/usr/bin/env bash
#
# Recette du poste Q14 — un double-clic ne déplace pas le bloc.
#
# Le défaut a été trouvé par Q1 sur ATA 21 importé, et la recette se joue là : sur un
# bloc posé à des coordonnées rondes et loin des repères, il ne se montre pas. Un
# double-clic pour entrer en saisie réécrivait l'ordonnée du bloc à son dernier
# chiffre (107.15039999999999 devenait 107.1504), et sur trois des huit blocs qu'un
# clic atteint en leur centre, il empilait un pas d'annulation et marquait le
# document modifié.
#
# Deux causes, séparées par la mesure, et chacune a son sabotage :
#
#   · le panneau, à la sélection : le champ en millimètres renvoyait au modèle la
#     valeur qu'il venait de recevoir, et l'aller-retour points → millimètres →
#     points n'est pas l'identité ; le champ de rotation, lui, la renvoyait arrondie ;
#   · le déplacement : un mouvement nul du pointeur passait par l'aimantation, qui
#     attirait vers un repère voisin un bloc qu'on n'avait fait que cliquer.
#
# Ce que les tests d'interface éprouvent, joués dans la vraie fenêtre au pointeur
# synthétisé :
#
#   1. sur chaque bloc de texte qu'un clic atteint en son centre, un double-clic
#      ouvre la saisie ; le document est le même au caractère près, aucun pas
#      d'annulation n'est empilé, le document n'est pas marqué modifié, et refermer
#      la saisie n'y change rien. Chaque clic porte un mouvement nul entre la
#      pression et le relâchement, comme une plate-forme peut en livrer ;
#   2. le même double-clic sur un bloc tourné d'un angle quelconque, comme la poignée
#      en pose, garde cet angle : le champ de rotation arrondissait à quatre décimales,
#      et l'arrondi remontait dans le modèle ;
#   3. un glissé d'un point, lui, déplace le bloc d'un point et empile son pas, que
#      l'annulation défait jusqu'au document d'ouverture.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

# Le lecteur des .pub, cherché comme la recette R5 le cherche : sans lui, rien ne
# s'affirme, et la recette le dit plutôt que de se déclarer verte.
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

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

attendus=3
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=Q14" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : un double-clic réécrit encore le document, ou un glissé d'un point ne compte plus." >&2
    exit 1
fi

joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=Q14 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi

echo "  Double-clic et glissé d'un point joués sur ATA 21 importé : $joues tests"
echo "Q14 : un double-clic ouvre la saisie sans rien réécrire ; un glissé d'un point compte toujours."
