#!/usr/bin/env bash
#
# Le bilan : ce qui reste ouvert, dit par la machine et non de mémoire.
#
# Il lit le tableau de suivi de MyPublisher/Docs/PLAN.md et, pour chaque poste, cherche
# sa recette dans Scripts/recettes/. Un poste à 100 % sans recette exécutable est un
# poste dont la livraison ne se rejoue pas : c'est la dette que le poste P3 voulait
# éteindre, et ce script la compte au lieu de la laisser se raconter.
#
#   Scripts/bilan.sh
#
# Une recette couvre chaque poste dont le code ouvre son nom de fichier :
# « A2-A7-debordement-et-rognage.sh » couvre A2 et A7. Quatre postes n'ont pas de
# recette au sens des autres, et c'est voulu : P0 est une décision, P1 un « git init »,
# P2 est lui-même le contrôle, P3 se rejoue par « eprouver-le-juge.sh ». D n'est pas un
# poste mais une décision à prendre.
#
# Le bilan informe, il ne juge pas : il sort en code 0 quoi qu'il trouve. Ce qui fait
# autorité reste « Scripts/verifier.sh ».
#
set -euo pipefail
# C.UTF-8 et non C : « ${#texte} » doit compter des caractères, pas des octets, pour
# que les accents n'ébrèchent pas les colonnes. « printf %-Ns », lui, compte des
# octets quelle que soit la locale — d'où « cadre ».
export LC_ALL=C.UTF-8

cadre() {
    local texte="${1:0:$2}"
    printf '%s%*s' "$texte" $(( $2 - ${#texte} + 1 )) ""
}

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$racine"

plan="MyPublisher/Docs/PLAN.md"

# Les codes couverts par une recette : les jetons « lettre + chiffres » en tête du nom.
couverts=" "
for recette in Scripts/recettes/*.sh; do
    nom="$(basename "$recette" .sh)"
    while [[ "$nom" =~ ^([A-Z][0-9]+)-(.*)$ ]]; do
        couverts+="${BASH_REMATCH[1]} "
        nom="${BASH_REMATCH[2]}"
    done
done

clos_sans_recette=()
ouverts=()

echo "$(cadre Poste 5)$(cadre Intitulé 46)$(cadre Avancement 12)Recette"
# Les lignes du tableau de suivi : « | **XN** | intitulé | avancement | … | ».
while IFS='|' read -r _ code intitule avancement _; do
    code="$(echo "$code" | tr -d '* ')"
    intitule="$(echo "$intitule" | sed 's/\*\*//g; s/`//g; s/^ *//; s/ *$//')"
    avancement="$(echo "$avancement" | sed 's/\*\*//g; s/^ *//; s/ *$//')"

    case "$code" in
        P0|P1|D) recette="sans objet" ;;
        P2)      recette="controle.sh" ;;
        P3)      recette="eprouver-le-juge.sh" ;;
        *)       if [[ "$couverts" == *" $code "* ]]; then recette="présente"; else recette="ABSENTE"; fi ;;
    esac

    echo "$(cadre "$code" 5)$(cadre "$intitule" 46)$(cadre "$avancement" 12)$recette"

    if [ "$avancement" = "100 %" ]; then
        [ "$recette" = "ABSENTE" ] && clos_sans_recette+=("$code")
    elif [ "$code" != "D" ]; then
        ouverts+=("$code")
    fi
done < <(sed -n '/^## Suivi/,/^---/p' "$plan" | grep -E '^\| \*\*[A-Z][0-9]*\*\* \|')

echo
echo "Postes ouverts           : ${ouverts[*]:-aucun}"
echo "Clos sans recette rejouable : ${clos_sans_recette[*]:-aucun}"
