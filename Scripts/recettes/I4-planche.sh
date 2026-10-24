#!/usr/bin/env bash
#
# Recette du poste I4 — la planche de la maquette.
#
# Le poste rhabille ce qui entoure la page, et rien de la page elle-même : fond gris
# clair et ombre double de la feuille, règles FIXES au bord de la zone visible, graduées
# de part et d'autre de la page, projection de la sélection sur les deux règles, repères
# libres prolongés sur toute la planche, marges en pointillé rose, cadre de sélection à
# 4 px du bloc, poignées blanches bordées d'accent, bulle de dimensions. Deux temps.
#
# 1. L'ÉCRAN, joué par « MyPublisher.Tests.Interface » sur la vraie fenêtre, à l'échelle 1
#    où un point de page vaut un pixel, au pointeur synthétisé (« PlancheTests », trait
#    « poste=I4 ») et mesuré sur l'image que la fenêtre compose réellement :
#
#      · la planche au gris de la maquette, la page au blanc du papier, sous une ombre
#        portée vers le bas qui s'éteint à 40 px ;
#      · les règles restent où elles sont quand la page défile, leur zéro la suit, et
#        elles se graduent des deux côtés du zéro ;
#      · la projection d'un bloc tourné de 30° couvre EXACTEMENT son emprise sur chaque
#        règle, au pixel près — l'emprise, et non le cadre, qu'une projection lisant X et
#        la largeur aurait suivi —, dans l'accent à 14 % ; elle suit le bloc pendant le
#        geste, et le cadre d'un tracé ;
#      · un repère tiré de la règle du haut se voit à gauche de la page, sur la planche ;
#        lâché là, hors de la page, il disparaît ;
#      · un repère posé traverse la planche de bord à bord, et la page rendue seule —
#        PageRoot, que l'écran compare au PDF — est la même au pixel avec ou sans lui ;
#      · les marges en pointillé rose d'un pixel ; le cadre de sélection à 4 px du bloc,
#        d'un trait et demi d'accent, qui tourne avec lui ; les poignées blanches bordées
#        d'accent, la rotation en disque au bout de sa tige ;
#      · pendant un redimensionnement, la bulle et la barre d'état disent la même taille ;
#        pendant un tracé, la bulle dit celle du cadre ;
#      · la couche de saisie couvre la planche : une poignée hors de la page s'y saisit,
#        sans faire sauter le bord qu'elle tire, et un clic sur le gris libère la sélection.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
#
# 2. RIEN N'A BOUGÉ DANS PAGEROOT (règle 4 de la phase). L'écran et le PDF sont confrontés
#    sur l'exemple et sur ATA 21, pages 1 et 2 :
#
#      · « comparer-mots.sh », à sa tolérance par défaut : aucune ligne coupée autrement,
#        aucun mot du PDF sans ligne à l'écran ;
#      · « comparer-rendus.sh », à la tolérance relevée AVANT le poste. Sur ces pages de
#        texte, il échoue à son seuil par défaut de 3 % depuis toujours — il mesure le
#        lissage des glyphes, deux rastériseurs du même PDF y diffèrent déjà (poste Q13).
#        La recette tient donc l'écart relevé sur le dépôt d'avant le poste, page par
#        page, et exige qu'il ne monte pas : rendu sans repères, PageRoot y donnait
#        alors, et y donne encore, les mêmes pixels — mesuré une fois à la livraison,
#        zéro pixel d'écart sur les quatre pages entre l'écran d'avant et celui d'après.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

# Le lecteur des .pub, cherché comme les recettes R5 et Q13 le cherchent : ATA 21 est un
# document Publisher.
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

# Le banc ne se compile pas par « dotnet test » : on compile toute la solution.
if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# ------------------------------------------------------------------------- 1. l'écran

attendus=16
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=I4" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests de la planche (« poste=I4 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=I4 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  1. la planche, mesurée sur l'image composée : $joues tests (fond, ombre, règles, projection, repères, marges, cadre, poignées, bulle, saisie)"

# ------------------------------------------------------------ 2. rien n'a bougé dans PageRoot

exemple="Samples/exemple-bulletin.mypub"
ata="MyPublisher/Books/ATA 21 - Air conditioning.pub"

for document in "$exemple" "$ata"; do
    if ! Scripts/comparer-mots.sh "$document" tout > "$travail/mots.txt" 2>&1; then
        echo "ÉCHEC : $(basename "$document") — l'écran ne coupe plus aux mêmes mots que le PDF." >&2
        sed 's/^/    /' "$travail/mots.txt" >&2
        exit 1
    fi
    echo "     $(basename "$document") : $(tail -1 "$travail/mots.txt" | sed 's/ Banc : .*//')"
done

# L'écart d'avant le poste, page par page : « document page pourcentage ». Relevé le 24
# octobre 2026 sur le commit 6712a50 (I1), par « comparer-rendus.sh » lui-même.
releve=(
    "$exemple|1|4.22"
    "$exemple|2|3.62"
    "$ata|1|6.09"
    "$ata|2|6.60"
)
for ligne in "${releve[@]}"; do
    IFS='|' read -r document page seuil <<< "$ligne"
    if ! Scripts/comparer-rendus.sh "$document" "$page" "$seuil" > "$travail/rendus.txt" 2>&1; then
        rm -f "$racine/ecart-page$page.png"
        echo "ÉCHEC : $(basename "$document"), page $page — l'écran s'écarte du PDF plus qu'avant le poste :" >&2
        sed 's/^/    /' "$travail/rendus.txt" >&2
        echo "PageRoot a bougé : la planche ne doit rien y ajouter (règle 4 de la phase)." >&2
        exit 1
    fi
    echo "     $(basename "$document") : $(head -1 "$travail/rendus.txt")"
done
echo "  2. PageRoot : les mêmes mots que le PDF, et pas plus d'écart qu'avant le poste"

echo "I4 : la planche porte les règles fixes, la projection, les repères, les marques et la bulle de la maquette, sans rien changer à la page."
