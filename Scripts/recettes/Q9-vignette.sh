#!/usr/bin/env bash
#
# Recette du poste Q9 — retirer « AbsoluteLineHeight ».
#
# La propriété était le survivant de l'interligne d'avant R4 : « FontSize × LineHeight »,
# un multiple du corps, quand R4 a fait de l'interligne un multiple de la hauteur
# naturelle de la police. Seule la vignette des modèles la lisait encore, et espaçait
# ainsi ses filets de 16 % trop court en DejaVu Sans ; elle en comptait de plus le
# nombre à la louche. Elle lit désormais les lignes de la composition.
#
# Ce qu'elle éprouve :
#
#   1. la propriété n'existe plus : ni dans les sources, ni sur le type compilé ;
#   2. la vignette du bulletin, rendue pour de vrai à deux pixels par point, porte un
#      filet par ligne que la page affiche, bloc par bloc, chacun à un point près de
#      la place de sa ligne. La place, et pas seulement le nombre : c'est l'espacement
#      que l'ancienne vignette manquait, et un compte seul ne l'aurait pas vu tant que
#      tous ses filets tenaient dans le bloc.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# ------------------------------------------------------------------------------- 1

if grep -rn --include=*.cs --include=*.axaml "AbsoluteLineHeight" \
        MyPublisher MyPublisher.Core MyPublisher.Banc > "$travail/restes.txt"; then
    echo "ÉCHEC : « AbsoluteLineHeight » est encore dans les sources :" >&2
    sed 's/^/    /' "$travail/restes.txt" >&2
    exit 1
fi

# ------------------------------------------------------------------------------- 2

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln --nologo -v q > /dev/null 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    exit 1
fi

# « dotnet test » sort en 0 quand AUCUN test ne correspond au filtre : on compte.
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj \
        ${MYPUBLISHER_DEJA_COMPILE:+--no-build} --nologo -v q --filter "poste=Q9" \
        > "$travail/tests.txt" 2>&1; then
    echo "ÉCHEC : les tests de la vignette ne passent pas." >&2
    sed 's/^/    /' "$travail/tests.txt" >&2
    exit 1
fi

joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ -z "$joues" ] || [ "$joues" -lt 2 ]; then
    echo "ÉCHEC : ${joues:-0} test(s) de la vignette joué(s), deux attendus." >&2
    sed 's/^/    /' "$travail/tests.txt" >&2
    exit 1
fi

echo "  1. « AbsoluteLineHeight » n'existe plus, ni dans les sources ni sur le type."
echo "  2. la vignette du bulletin porte un filet par ligne de la page, à sa place ($joues tests joués)."
echo "Q9 : la vignette des modèles lit les lignes de la composition."
