#!/usr/bin/env bash
#
# Recette du poste Q20 — une police absente de la machine survit à la sélection.
#
# Le défaut : la liste des polices du panneau ne proposait que celles de la machine. Un
# bloc dont la police n'y est pas — un .pub composé ailleurs, un document écrit à la
# main, une valeur que la migration de C5b a gardée libre (« !scheme:heading ») —
# laissait la liste sans sélection ; et en passant de ce bloc à un autre puis en y
# revenant, la liste renvoyait null au bloc. La police était perdue, et le rendu levait
# ensuite « ArgumentNullException » en composant le texte.
#
# Le geste se joue sur la vraie fenêtre, par MyPublisher.Tests.Interface, filtre
# « poste=Q20 » : sélectionner tour à tour un texte à police absente et un texte à
# police présente — libre ou prise dans le jeu —, cinq fois ; la police absente est
# intacte, la sélection n'a laissé aucun pas d'annulation, la liste montre la police
# en la disant absente, et la page se compose encore. Même chose d'un tableau à l'autre,
# et pour un bloc posé après que la liste a été faite, que seul le second garde-fou
# tient. Puis le geste inverse : choisir une police dans la liste l'écrit toujours, la
# liste la montre, et l'on peut revenir à la police absente. Un script de shell ne
# sélectionne rien ; il appelle les tests qui le font, et compte ceux qui ont tourné.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# Trois cas pour le passage d'un texte à l'autre, deux tableaux, un bloc venu après la
# liste, et le choix dans la liste.
attendus=6
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=Q20" > "$travail/tests.txt" 2>&1; then
    grep -E "\[FAIL\]|Expected|Actual|Exception" "$travail/tests.txt" | head -20 | sed 's/^/    /' >&2
    echo "ÉCHEC : une police absente ne survit pas à la sélection." >&2
    exit 1
fi

joues="$(grep -o 'Passed: *[0-9]*' "$travail/tests.txt" | head -1 | grep -o '[0-9]*$' || true)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=Q20 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi

echo "Q20 : une police absente de la machine survit à la sélection, et la liste la montre ($joues tests sur la vraie fenêtre)."
