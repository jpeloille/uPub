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
#   1. la propriété n'existe plus. « Ni dans les sources » est le grep ci-dessous ;
#      « ni sur le type compilé » est porté par le test de réflexion
#      VignetteTests.La_propriete_AbsoluteLineHeight_n_existe_plus, joué au point 2 ;
#   2. les tests de la vignette (trait poste=Q9), qui la rendent pour de vrai à deux
#      pixels par point :
#      - le bulletin porte un filet par ligne que la page affiche, bloc par bloc,
#        chacun à un point près de l'ordonnée de sa ligne. La place, et pas seulement
#        le nombre : c'est l'espacement que l'ancienne vignette manquait ;
#      - chaque filet commence et finit, à un point près, où la page pose sa ligne :
#        colonnes justifiées, titre et chapeau centrés, sous-titre au fer à droite ;
#      - la carte de visite, faite de champs, porte un filet et un seul par champ,
#        de toute la largeur de sa zone de texte, au milieu de sa première ligne ;
#      - un bloc qui déborde ne laisse paraître aucun filet sous son cadre, et en
#        garde dans son cadre ;
#      - une vignette qui change de document recompose ses filets au lieu de
#        resservir ceux du précédent.
#
# Ce qu'elle prouve : que la vignette pose ses filets là où la composition pose les
# lignes — nombre, ordonnée, début et fin —, au point près, sur les huit blocs de
# texte saisi du bulletin, à trois fers (justifié, centré, à droite ; le fer à gauche
# paraît dans les dernières lignes de paragraphe des colonnes), et sur les six champs
# de la carte de visite.
#
# Ce qu'elle ne prouve PAS :
#   - que la composition elle-même est juste. Les attentes sont calculées par le même
#     Compositeur, avec les mêmes métriques d'Avalonia, dans le même processus : une
#     erreur de mesure commune — l'italique de DejaVu Sans que Q13 a vu se résoudre
#     selon l'histoire du processus — passe des deux côtés à la fois. Elle prouve
#     l'accord avec la composition, pas avec les pixels de l'écran ni du PDF ;
#   - la vignette à la taille du catalogue. Elle est rendue à deux pixels par point
#     pour se lire au pixel ; à 134 pixels de côté, l'épaisseur plancher d'un pixel et
#     le lissage ne sont pas éprouvés ;
#   - les blocs tournés, les groupes, les tableaux, le calage vertical d'un champ, les
#     retraits : aucun ne paraît dans les blocs comptés. Les blocs tournés sont écartés
#     exprès, leurs filets ne se lisent pas en rangées ;
#   - l'affiche et le dépliant : seuls le bulletin et la carte sont rendus ;
#   - le gain de la mémoire des filets : le test dit qu'elle se renouvelle quand le
#     document change, pas qu'elle sert, ni qu'elle se renouvelle quand la taille change.
#     La mesure du gain est dans le commentaire de TemplatePreview, faite à la main.
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
if [ -z "$joues" ] || [ "$joues" -lt 6 ]; then
    echo "ÉCHEC : ${joues:-0} test(s) de la vignette joué(s), six attendus." >&2
    sed 's/^/    /' "$travail/tests.txt" >&2
    exit 1
fi

echo "  1. « AbsoluteLineHeight » n'existe plus, ni dans les sources ni sur le type."
echo "  2. la vignette porte un filet par ligne de la page et par champ, à sa place et à son fer,"
echo "     découpé au bloc, et le recompose quand le document change ($joues tests joués)."
echo "Q9 : la vignette des modèles lit les lignes de la composition."
