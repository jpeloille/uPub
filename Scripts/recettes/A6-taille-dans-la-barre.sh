#!/usr/bin/env bash
#
# Recette du poste A6 — taille du bloc dans la barre d'état.
#
# Ce poste n'a rien qu'un fichier livré puisse porter : ni PDF, ni document, ni relevé.
# Ce qu'il ajoute est un affichage qui se rafraîchit PENDANT un geste, et cela ne se
# mesure que dans une fenêtre où l'on tire une poignée. La recette appelle donc les
# tests d'interface par leur nom, et vérifie qu'ils ont bien été joués — un filtre qui
# ne désigne plus rien sort en 0, et serait la seule façon pour elle de se dire verte
# sans avoir rien vu.
#
# Ce que ces tests-là éprouvent, et pourquoi ils ne sont pas une mise en scène :
#
#   · le bloc est sélectionné AVANT la première image, si bien que la case de la barre
#     affiche déjà une taille : seule une taille qui change peut alors en remuer les
#     pixels ;
#   · l'image est prise par « CaptureRenderedFrame » et non par
#     « RenderTargetBitmap.Render » — le second redessine de force, quel qu'ait été
#     l'état d'invalidation, et passerait même si plus rien ne se rafraîchissait à
#     l'écran. C'est le piège qui a laissé vivre un défaut d'affichage quatre postes
#     durant ;
#   · le découpage porte sur la seule case de la taille, et non sur la barre entière :
#     la position du curseur, deux cases plus loin, change à chaque mouvement de
#     pointeur, et une comparaison large aurait vu sa différence à elle ;
#   · le bouton n'est pas relâché lorsque la mesure est prise.
#
# La vérification a été faite en retirant l'abonnement à la géométrie du bloc : la
# comparaison de pixels tombe à elle seule, l'assertion sur le texte écartée.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile, plutôt que de lancer les tests avec « --no-build ».
#
# C'est le piège que R3 a relevé et qu'un « --no-build » réintroduirait : un binaire
# périmé survit à une modification des sources, et la recette se déclare verte sur du
# code qu'elle n'a pas éprouvé. Elle est faite pour être rejouée seule, y compris par
# quelqu'un qui vient de saboter un fichier ; elle doit donc compiler elle-même, comme
# « Scripts/controle.sh » le fait depuis le poste P2.
if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

tests="MyPublisher.Tests.Interface/bin/Debug/net10.0/MyPublisher.Tests.Interface.dll"
if [ ! -f "$tests" ]; then
    echo "ÉCHEC : tests d'interface introuvables après compilation ($tests)." >&2
    echo "Le cadriciel cible du .csproj a-t-il changé ?" >&2
    exit 1
fi

attendus=4
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=A6" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : la barre d'état ne suit pas les gestes." >&2
    exit 1
fi

joues="$(grep -o 'Passed: *[0-9]*' "$travail/tests.txt" | head -1 | grep -o '[0-9]*$' || true)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=A6 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
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

echo "  Barre d'état éprouvée sur la vraie fenêtre : $joues tests"
echo "A6 : position et taille du bloc affichées, et suivies pendant le geste."
