#!/usr/bin/env bash
#
# Contrôle de non-régression minimal — poste P2 de MyPublisher/Docs/PLAN.md.
#
# Trois étapes sur le document d'exemple, à passer avant et après chaque poste
# qui touche au modèle, au format de fichier ou à l'export :
#
#   1. le relire,
#   2. le réécrire sous un autre nom : les deux JSON doivent être identiques,
#   3. l'exporter en PDF : le nombre de pages et la liste des avertissements
#      doivent correspondre au relevé de référence.
#
# La troisième étape se formule comme un écart et non comme une absence : un
# contrôle qui exigerait zéro avertissement tomberait le jour où le document de
# référence porte une image liée manquante.
#
#   Scripts/controle.sh                 contrôle ; sort en 0 tant que rien ne bouge
#   Scripts/controle.sh --enregistrer   remplace le relevé de référence, après
#                                       avoir montré ce qui change
#
set -euo pipefail

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$racine"

document="Samples/exemple-bulletin.mypub"
reference="Scripts/reference.txt"
# Configuration explicite : sans elle, un binaire Release oublié pourrait être
# ce qu'on contrôle, alors qu'on vient de compiler en Debug.
binaire="MyPublisher/bin/Debug/net9.0/MyPublisher"

enregistrer=0
case "${1-}" in
    --enregistrer) enregistrer=1 ;;
    "") ;;
    *) echo "Usage : $0 [--enregistrer]" >&2; exit 2 ;;
esac

dotnet build MyPublisher/MyPublisher.csproj -c Debug -v q --nologo >/dev/null

# Les fichiers de travail restent hors du dépôt : le contrôle ne doit rien
# laisser derrière lui, ni salir « git status ».
travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# Étapes 1 et 2 — l'aller-retour d'écriture.
if ! "$binaire" "$document" --resave "$travail/copie.mypub" >/dev/null; then
    echo "ÉCHEC : relecture du document impossible." >&2
    exit 1
fi

if ! diff -u "$document" "$travail/copie.mypub" > "$travail/ecart.diff"; then
    echo "ÉCHEC : relu puis réécrit, le document n'est plus le même." >&2
    head -40 "$travail/ecart.diff" >&2
    exit 1
fi

# Étape 3 — l'export. Le compte de pages sort sur la sortie standard, les
# avertissements sur la sortie d'erreur : on les recueille séparément.
if ! "$binaire" "$document" --export "$travail/sortie.pdf" \
        > "$travail/sortie.txt" 2> "$travail/erreur.txt"; then
    echo "ÉCHEC : export impossible." >&2
    cat "$travail/erreur.txt" >&2
    exit 1
fi

pages="$(sed -n 's/^\([0-9][0-9]*\) page(s).*/\1/p' "$travail/sortie.txt")"
if [ -z "$pages" ]; then
    echo "ÉCHEC : l'export n'annonce aucun nombre de pages." >&2
    cat "$travail/sortie.txt" >&2
    exit 1
fi

{
    echo "document : $document"
    echo "pages : $pages"
    # Triés : l'ordre des avertissements suit l'ordre des blocs, qui n'est pas
    # ce qu'on contrôle ici.
    sed -n 's/^Avertissement : /avertissement : /p' "$travail/erreur.txt" | LC_ALL=C sort
} > "$travail/releve.txt"

if [ "$enregistrer" = 1 ]; then
    if [ -f "$reference" ]; then
        echo "Écart avec le relevé de référence actuel :"
        diff -u "$reference" "$travail/releve.txt" || true
    fi
    cp "$travail/releve.txt" "$reference"
    echo "Relevé de référence enregistré dans $reference."
    exit 0
fi

if [ ! -f "$reference" ]; then
    echo "ÉCHEC : pas de relevé de référence. Le créer avec « $0 --enregistrer »." >&2
    exit 1
fi

if ! diff -u "$reference" "$travail/releve.txt"; then
    echo "ÉCHEC : l'export a changé par rapport au relevé de référence." >&2
    exit 1
fi

avertissements="$(grep -c '^avertissement : ' "$travail/releve.txt" || true)"
echo "Contrôle passé : aller-retour identique, $pages page(s), $avertissements avertissement(s)."
