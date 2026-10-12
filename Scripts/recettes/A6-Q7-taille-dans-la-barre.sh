#!/usr/bin/env bash
#
# Recette des postes A6 et Q7 — la taille du bloc dans la barre d'état, éprouvée sur
# des blocs que l'application a fabriqués.
#
# A6 n'a rien qu'un fichier livré puisse porter : ni PDF, ni document, ni relevé. Ce
# qu'il ajoute est un affichage qui se rafraîchit PENDANT un geste, et cela ne se
# mesure que dans une fenêtre où l'on tire une poignée. La recette appelle donc les
# tests d'interface par leur poste, et vérifie qu'ils ont bien été joués — un filtre
# qui ne désigne plus rien sort en 0, et serait la seule façon pour elle de se dire
# verte sans avoir rien vu.
#
# Q7 a réécrit ces tests sur le cas réel. La première écriture posait ses blocs par un
# « new ShapeFrame » glissé dans le document avant d'ouvrir la fenêtre, puis les
# sélectionnait par le modèle de vue : tout le chemin par lequel un bloc naît dans
# l'application lui échappait. Les blocs sont désormais TRACÉS — la touche de l'outil,
# puis un cadre tiré au pointeur sur la page —, si bien que le gabarit de l'outil, la
# taille tracée, l'insertion et la sélection qui la suit sont éprouvés avec la barre.
# Deux sabotages de ce chemin-là laissaient l'ancienne recette verte, et la font
# désormais tomber : le tracé qui ne donne plus sa taille au bloc, et le bloc tracé
# qui n'est plus sélectionné.
#
# Ce que ces tests éprouvent encore, et pourquoi ils ne sont pas une mise en scène :
#
#   · le bloc est sélectionné — par son tracé — AVANT la première image, si bien que
#     la case de la barre affiche déjà une taille : seule une taille qui change peut
#     alors en remuer les pixels ;
#   · l'image est prise par « CaptureRenderedFrame » et non par
#     « RenderTargetBitmap.Render » — le second redessine de force, quel qu'ait été
#     l'état d'invalidation, et passerait même si plus rien ne se rafraîchissait à
#     l'écran ;
#   · le découpage porte sur la seule case mesurée, et non sur la barre entière : la
#     position du curseur, à côté, change à chaque mouvement de pointeur ;
#   · le bouton n'est pas relâché lorsque la mesure est prise ;
#   · à plusieurs blocs, la sélection se fait au Ctrl+clic, et c'est la case qu'on lit,
#     non le modèle de vue.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile, plutôt que de lancer les tests sur ce qui traîne dans « bin/ » : un
# binaire périmé survit à une modification des sources, et la recette se déclarerait
# verte sur du code qu'elle n'a pas éprouvé. « Scripts/verifier.sh », qui vient de
# compiler, l'en dispense.
if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

attendus=4
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=A6" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : la barre d'état ne suit pas les blocs tracés, ou pas leurs gestes." >&2
    exit 1
fi

joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=A6 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi

# Les tests du poste tracent leurs blocs, et n'en posent aucun à la main. C'est tout
# l'objet de Q7 : un test réécrit plus tard sur un bloc fabriqué repasserait à côté du
# chemin de l'application sans échouer pour autant — c'est ici qu'on le voit.
tests_a6="$(awk '/\[Trait\("poste", "A6"\)\]/ { dedans = 1 } dedans' MyPublisher.Tests.Interface/PanneauEtBarreTests.cs \
            | awk '/private static string Millimetres/ { exit } { print }')"
if grep -qE 'new (ShapeFrame|TextFrame)|Carre\(|SelectOnly\(|SetSelection\(' <<< "$tests_a6"; then
    echo "ÉCHEC : un test du poste A6 pose ou sélectionne un bloc à la main :" >&2
    grep -nE 'new (ShapeFrame|TextFrame)|Carre\(|SelectOnly\(|SetSelection\(' <<< "$tests_a6" | sed 's/^/    /' >&2
    exit 1
fi
traces="$(grep -c 'Tracer(fenetre, vm, PhysicalKey\.' <<< "$tests_a6" || true)"
if [ "${traces:-0}" -lt 5 ]; then
    echo "ÉCHEC : ${traces:-0} tracé(s) dans les tests du poste A6, cinq attendus." >&2
    exit 1
fi

# La case doit exister sous ce nom-là : c'est par lui que les tests la découpent dans
# l'image composée, et un renommage silencieux ferait échouer la recherche plutôt que
# la mesure — autant le dire ici.
for nom in PositionDuBloc TailleDuBloc; do
    if ! grep -q "x:Name=\"$nom\"" MyPublisher/Views/MainWindow.axaml; then
        echo "ÉCHEC : la case « $nom » a disparu de la barre d'état." >&2
        exit 1
    fi
done

echo "  Barre d'état éprouvée sur la vraie fenêtre : $joues tests, $traces blocs tracés à l'outil"
echo "A6 : position et taille du bloc affichées, et suivies pendant le geste."
echo "Q7 : sur des blocs nés du tracé, par la fabrique de l'application."
