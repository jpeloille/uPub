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
# Trois étapes, de la plus rapide à la plus lente, pour que l'échec le plus probable
# tombe le premier :
#
#   1. les tests — quelques centaines de millisecondes pour le noyau, quelques
#      secondes pour l'interface,
#   2. le contrôle de non-régression — compile, relit, réécrit, exporte,
#   3. les recettes des postes, une par poste.
#
# La comparaison écran ↔ PDF n'est PAS lancée ici sur tous les documents : sur une page
# de texte elle échoue aujourd'hui pour une raison connue et écrite — l'écran et le PDF
# mesurent les mots chacun avec sa bibliothèque, et peuvent couper une ligne à un mot
# différent (cf. PLAN.md, poste Q13). Elle est appelée par les recettes qui la
# concernent, avec leur document et leur tolérance.
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

etape "Tests"
if ! dotnet test MyPublisher.sln --nologo -v q 2>&1 | grep -E "^(Passed!|Failed!|Erreur|error)"; then
    echo "ÉCHEC : les tests ne passent pas." >&2
    echec=1
fi

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
