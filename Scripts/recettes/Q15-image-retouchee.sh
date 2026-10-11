#!/usr/bin/env bash
#
# Recette du poste Q15 — une image modifiée sur le disque se relit.
#
# Q3 a choisi d'oublier les échecs du cache d'images à l'ouverture d'un document, et de
# garder ses réussites. Une image retouchée sur le disque pendant la session restait
# donc périmée à l'écran, réouverture comprise, alors que l'export, qui lit le disque à
# chaque fois, montrait la nouvelle : un écart écran ↔ PDF.
#
# Le remède : chaque image lue garde l'empreinte de son fichier — date de modification
# et taille —, et l'ouverture d'un document oublie celles dont le fichier a changé.
#
# Ce qu'elle éprouve, par « MyPublisher.Tests.Interface » sur la vraie fenêtre, dans un
# seul processus et donc un seul cache :
#
#   · une image magenta s'ouvre ; rouvrir sans retouche rend LA MÊME instance —
#     l'épingle de Q3 tient ;
#   · le fichier est réécrit en cyan, même taille, date plus récente ; un geste annulé
#     ne relit rien — annuler n'est pas rouvrir ;
#   · la réouverture montre le cyan à l'écran, plus un pixel magenta, et le PDF exporté
#     au même instant porte une image cyan, relue dans le fichier par PDFsharp ;
#   · une retouche qui garde la date — la même seconde, sur un système de fichiers
#     qui compte à la seconde — se relit aussi : la taille le dit.
#
# Chacun des deux cas de retouche ne trahit qu'une moitié de l'empreinte : le premier
# la date, le second la taille. Le compte des tests joués est vérifié.
#
# Pourquoi des tests et non le binaire : le cache n'existe qu'à l'écran, et l'écran
# n'existe que dans un processus qui vit assez longtemps pour qu'on retouche un fichier
# sous lui.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

attendus=2
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=Q15" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les mesures de Q15 ne passent pas." >&2
    exit 1
fi

joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=Q15 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi

echo "  retouchée, l'image se relit à la réouverture, à l'écran comme au PDF ; intacte, elle reste la même instance ($joues tests)"
echo "Q15 : une image modifiée sur le disque se relit."
