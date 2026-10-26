#!/usr/bin/env bash
#
# Recette du poste N1 — le banc des gestes.
#
# Les tests d'interface synthétisaient leurs gestes par des aides recopiées de classe en
# classe : treize « SurLaPage », six « Cliquer », trois « Glisser », quatre « UnSeulPas »,
# cinq entrées en saisie par double-clic. Elles sont remontées dans Rendu.cs, région « Les
# gestes », une par geste, et les copies retirées. La phase N va changer le sens de ces
# gestes (N12 : on entre dans le texte d'un clic) : elle le fera à un seul endroit. Deux
# temps, comme la recette de I3.
#
#   1. LES SOURCES, lues par Scripts/aides-de-geste.py.
#      a. Aucune aide de geste n'est définie hors de la région « Les gestes » de Rendu.cs.
#         Une aide de geste est un membre ou une fonction locale qui n'est pas un test et qui
#         synthétise lui-même une entrée — appelle MouseDown, MouseUp, MouseMove, MouseWheel,
#         KeyPress, KeyPressQwerty, KeyRelease, KeyReleaseQwerty, KeyTextInput ou DragDrop —,
#         ou qui porte le nom d'une aide publique de la région. Un test qui joue ses entrées
#         en ligne n'en est pas une, ni une aide de classe qui compose celles du banc sans
#         rien synthétiser ; commentaires et littéraux sont blanchis avant la lecture. Dans
#         Rendu.cs même, aucune entrée n'est synthétisée hors de la région.
#      b. Les aides que nomme le plan sont dans la région : SurLaPage, Centre, Coin, Poignee,
#         Cliquer, CliquerDroit, DoubleCliquer, Glisser, Touche, ToucheAzerty,
#         SelectionnerDuTexte, EntrerEnSaisie, ZoneDeSaisie, UnSeulPas, TexteCopie.
#   2. LES TESTS d'interface « poste=N1 » (BancTests), dix au moins : un clic droit ouvre
#      un ContextMenu en moins de dix images, et son entrée joue sa commande ; la touche Menu
#      l'ouvre, Maj+F10 non ; un texte et un format propre font l'aller-retour par le
#      presse-papiers ; Ctrl+C dans une zone de texte y met la sélection ; un glissé du 4ᵉ au
#      7ᵉ caractère sélectionne [4, 7), et le même sans le bouton tenu ne sélectionne rien ;
#      Key.A sur la place de Q, sous Ctrl, déclenche une liaison Ctrl+A ; deux clics à 3 px
#      ne font pas un double-clic, à 2 px si ; SurLaPage suit l'échelle et échoue sans
#      PageRoot ; EntrerEnSaisie ouvre la zone du bloc et la rend ; dans cette zone, un
#      glissé sélectionne et Ctrl+C copie.
#
# Ce qu'elle ne joue pas : le nombre de tests sous chaque « poste= », que la migration ne
# devait pas changer, ni les vingt passages des recettes Q14 et Q23 — chacune joue les siens.
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

# ---------------------------------------------------------------------- 1. les sources

# -I : rien du dossier courant ; -B : pas de cache écrit dans Scripts/.
if ! python3 -I -B Scripts/aides-de-geste.py "$racine" > "$travail/aides.txt" 2>&1; then
    sed 's/^/    /' "$travail/aides.txt" >&2
    echo "ÉCHEC : une aide de geste est définie hors de Rendu.cs — elle y a sa place, une seule fois." >&2
    exit 1
fi
echo "    a. $(tail -1 "$travail/aides.txt" | sed 's/ (.*) ;/ ;/')"

python3 -I -B Scripts/aides-de-geste.py --noms "$racine" > "$travail/noms.txt"
manquent=""
for aide in SurLaPage Centre Coin Poignee Cliquer CliquerDroit DoubleCliquer Glisser Touche ToucheAzerty \
            SelectionnerDuTexte EntrerEnSaisie ZoneDeSaisie UnSeulPas TexteCopie; do
    grep -qx "$aide" "$travail/noms.txt" || manquent="$manquent $aide"
done
if [ -n "$manquent" ]; then
    echo "ÉCHEC : la région « Les gestes » de Rendu.cs n'a plus :$manquent." >&2
    exit 1
fi
echo "    b. les quinze aides du plan sont dans la région"
echo "  1. les sources : une aide par geste, dans Rendu.cs seul"

# ------------------------------------------------------------------------ 2. les tests

attendus=10
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=N1" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests du banc des gestes ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N1 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. le banc sans affichage : clic droit et touche Menu, presse-papiers, Ctrl+C, glissé bouton tenu, AZERTY, double-clic à 3 px, SurLaPage à l'échelle, entrée en saisie ($joues tests)"

echo "N1 : les gestes des tests d'interface sont synthétisés à un seul endroit, Rendu.cs, et le banc éprouve ce qu'Avalonia sans affichage permet."
