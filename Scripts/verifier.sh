#!/usr/bin/env bash
#
# Le parapluie. C'est cette commande-là qui fait autorité, et aucune autre.
#
# « Scripts/controle.sh » reste ce qu'il est — le filet sur la chaîne d'écriture — mais
# il ne lance pas les tests. Quelqu'un qui vérifie à la main en l'appelant verrait vert
# là où le banc voit rouge : d'où cette enveloppe, qui enchaîne tout ce qui doit passer
# avant qu'un poste soit dit livré.
#
#   Scripts/verifier.sh
#
# D'abord la compilation de toute la solution, banc compris : « dotnet test » ne
# compile pas MyPublisher.Banc, et les recettes A1 et A2 mesuraient le banc de
# l'exécution précédente — sous le juge, celui du sabotage d'avant. Puis trois étapes,
# de la plus rapide à la plus lente, pour que l'échec le plus probable tombe le premier :
#
#   1. les tests — quelques centaines de millisecondes pour le noyau, quelques
#      secondes pour l'interface,
#   2. le contrôle de non-régression — compile, relit, réécrit, exporte,
#   3. les recettes des postes, une par poste.
#
# Les deux comparaisons écran ↔ PDF ne sont pas lancées ici en bloc, mais par les
# recettes qui les concernent, avec leur document et leur tolérance :
#
#   - « comparer-mots.sh » pour le texte : il confronte, mot à mot, les lignes que le
#     banc dessine à celles du PDF, et dit lesquelles coupent autrement. La recette de
#     Q13 l'appelle sur l'exemple et les deux documents ATR ;
#   - « comparer-rendus.sh » pour les formes et les images, au pixel. Sur une page de
#     texte, il ne mesure pas la coupure mais le dessin des glyphes : deux rastériseurs
#     du MÊME PDF y diffèrent déjà de 2,36 % sur la page 1 de l'exemple, au-dessus de
#     ce que son seuil laisse à l'écran (cf. PLAN.md, poste Q13).
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$racine"

echec=0
etape() {
    echo
    echo "── $1"
}

etape "Compilation"
# Un plantage du compilateur lui-même (« Internal CLR error », vu quand plusieurs
# copies compilent à la fois) ne dit rien du code : on recompile une fois avant de
# conclure, et s'il persiste on le nomme pour ce qu'il est, sans le confondre avec
# une erreur du code — le juge compterait sinon un sabotage pour attrapé.
compiler() { dotnet build MyPublisher.sln --nologo -v q > /tmp/compilation-$$.txt 2>&1; }
if ! compiler; then
    if grep -qE "Internal CLR error|0x80131506|MSB4166" /tmp/compilation-$$.txt && compiler; then
        echo "  passe (au second essai : le compilateur avait planté)"
    elif grep -qE "Internal CLR error|0x80131506|MSB4166" /tmp/compilation-$$.txt; then
        sed 's/^/    /' /tmp/compilation-$$.txt | tail -20 >&2
        echo "ÉCHEC : le compilateur a planté deux fois — l'environnement, pas le code." >&2
        rm -f /tmp/compilation-$$.txt
        exit 3
    else
        grep -E "error" /tmp/compilation-$$.txt | sort -u | head -20 | sed 's/^/    /' >&2
        echo "ÉCHEC : la compilation ne passe pas." >&2
        rm -f /tmp/compilation-$$.txt
        exit 1
    fi
else
    echo "  passe"
fi
rm -f /tmp/compilation-$$.txt
# Compilé une fois pour toutes : les recettes, qui compilent quand on les joue seules,
# s'en dispensent ici. Onze compilations de moins par passage, et autant d'occasions
# de moins pour le plantage du compilateur, qui frappait au milieu des recettes.
export MYPUBLISHER_DEJA_COMPILE=1

etape "Tests"
# Les tests en échec sont nommés, et pas seulement comptés : le juge cherche dans cette
# sortie qui a attrapé son sabotage, et un sabotage pris par les seuls tests du noyau
# passait pour pris par personne.
#
# La commande dotnet elle-même plante parfois sur cette machine (« Internal CLR error »,
# code 134, sans un seul résultat) : vu deux fois le 12 octobre 2026, sous charge. Comme
# pour la compilation, on rejoue une fois ; un second plantage sort en 3, l'environnement
# et non le code. Un test en échec (« [FAIL] ») n'est jamais rejoué.
tester() {
    local code=0
    dotnet test MyPublisher.sln --no-build --nologo -v q > /tmp/tests-$$.txt 2>&1 || code=$?
    if [ "$code" != 0 ] && ! grep -q "\[FAIL\]" /tmp/tests-$$.txt &&
       { [ "$code" = 134 ] || grep -qE "Internal CLR error|0x80131506" /tmp/tests-$$.txt; }; then
        return 3
    fi
    return "$code"
}
code_tests=0
tester || code_tests=$?
if [ "$code_tests" = 3 ]; then
    echo "  dotnet test a planté sans résultat ; second essai"
    code_tests=0
    tester || code_tests=$?
    if [ "$code_tests" = 3 ]; then
        tail -20 /tmp/tests-$$.txt | sed 's/^/    /' >&2
        echo "ÉCHEC : dotnet test a planté deux fois — l'environnement, pas le code." >&2
        rm -f /tmp/tests-$$.txt
        exit 3
    fi
fi
if [ "$code_tests" != 0 ]; then
    grep -E "^(Passed!|Failed!|Erreur|error)" /tmp/tests-$$.txt || true
    # Une théorie porte ses paramètres entre le nom et « [FAIL] » : on lit le nom seul.
    grep -E "\[FAIL\]" /tmp/tests-$$.txt | grep -oE "[A-Za-z0-9_]+Tests\.[A-Za-z0-9_]+" | sort -u | sed 's/^/  en échec : /' >&2 || true
    echo "ÉCHEC : les tests ne passent pas." >&2
    echec=1
else
    grep -E "^(Passed!|Failed!)" /tmp/tests-$$.txt
fi
rm -f /tmp/tests-$$.txt

etape "Contrôle de non-régression"
if ! Scripts/controle.sh; then
    echec=1
fi

etape "Recettes des postes"
if [ -d Scripts/recettes ] && compgen -G "Scripts/recettes/*.sh" > /dev/null; then
    for recette in Scripts/recettes/*.sh; do
        nom="$(basename "$recette" .sh)"
        if "$recette" > /tmp/recette-$$.txt 2>&1; then
            echo "  $nom : passe"
        else
            echo "  $nom : ÉCHOUE" >&2
            sed 's/^/    /' /tmp/recette-$$.txt >&2
            echec=1
        fi
        rm -f /tmp/recette-$$.txt
    done
else
    echo "  aucune recette écrite pour l'instant."
fi

etape "Bilan"
Scripts/bilan.sh | tail -2

echo
if [ "$echec" = 0 ]; then
    echo "Vérification passée."
else
    echo "VÉRIFICATION EN ÉCHEC." >&2
fi
exit "$echec"
