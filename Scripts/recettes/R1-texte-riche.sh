#!/usr/bin/env bash
#
# Recette du poste R1 — texte riche.
#
# Ce qu'elle éprouve, sur « Samples/recette-texte-riche.mypub » :
#
#   1. le document se relit et se réécrit au caractère près, fragments compris ;
#   2. son texte se retrouve entier dans le PDF ;
#   3. l'écran et le PDF donnent la même image.
#
# Le troisième point est le seul qui compte vraiment. Avant R1, l'écran découpait ses
# lignes avec Avalonia et l'export avec le noyau : ils s'accordaient sur les documents
# essayés, mais c'était une observation, pas une propriété. Depuis, les deux appellent
# la même composition — cette recette est ce qui le vérifie.
#
# La tolérance est celle du lissage des glyphes, mesurée : sur ce document, l'écart
# tient à 0,8 %. Un décalage de mise en page — une coupe différente, un fragment
# oublié, une justification qui compte les morceaux au lieu des mots — le ferait
# bondir bien au-delà.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

document="Samples/recette-texte-riche.mypub"
binaire="MyPublisher/bin/Debug/net9.0/MyPublisher"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# 1 — l'aller-retour d'écriture, fragments compris.
"$binaire" "$document" --resave "$travail/copie.mypub" > /dev/null
if ! diff -u "$document" "$travail/copie.mypub" > "$travail/ecart.diff"; then
    echo "ÉCHEC : relu puis réécrit, le document aux fragments n'est plus le même." >&2
    head -30 "$travail/ecart.diff" >&2
    exit 1
fi

# 2 — le texte arrive entier dans le PDF. On compare mot à mot plutôt que ligne à
# ligne : les coupes dépendent de la largeur, pas le contenu.
"$binaire" "$document" --export "$travail/sortie.pdf" > /dev/null
pdftotext "$travail/sortie.pdf" "$travail/sortie.txt"

for mot in gras italique rouge "corps plus grand" "DejaVu Serif"; do
    if ! tr '\n' ' ' < "$travail/sortie.txt" | grep -qF "$mot"; then
        echo "ÉCHEC : « $mot » ne se retrouve pas dans le PDF." >&2
        exit 1
    fi
done

# 3 — l'écran et le PDF donnent la même image.
Scripts/comparer-rendus.sh "$document" 1 1.5

echo "R1 : aller-retour identique, texte complet au PDF, écran et export d'accord."
