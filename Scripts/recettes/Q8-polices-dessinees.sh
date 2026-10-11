#!/usr/bin/env bash
#
# Recette du poste Q8 — n'avertir que des polices dessinées.
#
# La passe des polices de l'export (B5) relisait l'héritage des fragments pour son
# compte : la police du bloc, puis celle de chaque fragment, sans regarder ce qui
# recouvrait quoi. Un fragment entièrement recouvert par un autre faisait donc avertir
# d'une police que rien ne dessine. Elle lit maintenant les styles composés, par la
# découpe que la composition emploie elle-même.
#
# Ce qu'elle éprouve, sur un document écrit pour elle :
#
#   1. le cas du plan : un bloc en Noto Sans dont tout le texte est recouvert par un
#      fragment en DejaVu Sans, avec un fragment intermédiaire en « Police
#      Inexistante » lui-même recouvert ; et un second bloc pareil, dont la police
#      propre est absente elle aussi. L'export n'avertit d'aucune police ;
#   2. le PDF le confirme : il n'embarque que DejaVu Sans. Ni la police des blocs ni
#      le substitut de « Police Inexistante » — Noto Sans sur cette machine — n'y
#      sont dessinés. C'est ce qui relie le silence de l'avertissement au dessin, et
#      non à une passe qui se tairait ;
#   3. contre-épreuve : le même document sans le fragment qui recouvre tout. Les
#      polices redeviennent dessinées, et l'export les nomme — « Police Inexistante »
#      comprise. Sans ce point, une passe devenue muette passerait le premier ;
#   4. les tests du noyau marqués « poste=Q8 » : à découvert, hors du texte, champ,
#      champ vide, saut de ligne. Leur compte est vérifié.
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

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
if [ ! -x "$binaire" ]; then
    echo "ÉCHEC : binaire introuvable ($binaire)." >&2
    exit 1
fi

for outil in pdffonts; do
    if ! command -v "$outil" > /dev/null; then
        echo "ÉCHEC : « $outil » est introuvable (poppler-utils)." >&2
        exit 1
    fi
done

# ------------------------------------------------------------------ le document

# $1 : le fichier à écrire · $2 : « oui » pour poser le fragment qui recouvre tout.
ecrire() {
    python3 - "$1" "$2" <<'PY'
import json, sys

chemin, recouvert = sys.argv[1], sys.argv[2] == "oui"
texte = "Descendre au niveau de vol inferieur."

def bloc(nom, y, police):
    fragments = [{"Start": 3, "Length": 12, "FontFamily": "Police Inexistante"}]
    if recouvert:
        # Le dernier fragment posé l'emporte : celui-ci recouvre tout le texte,
        # l'intermédiaire compris.
        fragments.append({"Start": 0, "Length": len(texte), "FontFamily": "DejaVu Sans"})
    return {"$kind": "text", "Name": nom, "X": 40, "Y": y, "Width": 360, "Height": 40,
            "Text": texte, "FontFamily": police, "FontSize": 12, "Spans": fragments}

document = {
    "FormatVersion": 4, "Title": "Recette Q8", "PageWidth": 595.28, "PageHeight": 841.89,
    "Pages": [{"Name": "Page 1", "Frames": [
        bloc("Plan", 40, "Noto Sans"),
        bloc("Absente", 100, "Police Absente De Cette Machine"),
    ]}],
}
json.dump(document, open(chemin, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
}

# Les avertissements de police, un par ligne ; l'export doit réussir.
polices_signalees() {
    if ! "$binaire" "$1" --export "$2" > /dev/null 2> "$travail/erreur.txt"; then
        echo "ÉCHEC : export impossible ($1)." >&2
        cat "$travail/erreur.txt" >&2
        exit 1
    fi
    grep '^Avertissement : Police' "$travail/erreur.txt" || true
}

# Les polices que le PDF embarque, une par ligne, sans le préfixe de sous-ensemble
# (« ABCDEF+ »). Le nom tient dans la première colonne de « pdffonts », 36 caractères,
# et peut porter des espaces.
familles_du_pdf() {
    pdffonts "$1" | tail -n +3 | cut -c1-36 | sed 's/^[A-Z]\{6\}+//; s/ *$//' | sort -u
}

# ------------------------------------------------------------------------- 1

ecrire "$travail/recouvert.mypub" oui
signale="$(polices_signalees "$travail/recouvert.mypub" "$travail/recouvert.pdf")"
if [ -n "$signale" ]; then
    echo "ÉCHEC : tout le texte est en DejaVu Sans, et l'export avertit pourtant :" >&2
    echo "$signale" >&2
    exit 1
fi
echo "  1. polices recouvertes : aucun avertissement."

# ------------------------------------------------------------------------- 2

familles="$(familles_du_pdf "$travail/recouvert.pdf")"
if [ -z "$familles" ] || printf '%s\n' "$familles" | grep -qv '^DejaVu Sans'; then
    echo "ÉCHEC : le PDF devait n'embarquer que DejaVu Sans ; il embarque :" >&2
    printf '%s\n' "${familles:-(rien)}" | sed 's/^/    /' >&2
    exit 1
fi
echo "  2. le PDF n'embarque que : $(printf '%s\n' "$familles" | paste -sd,) — rien d'autre n'est dessiné."

# ------------------------------------------------------------------------- 3

ecrire "$travail/decouvert.mypub" non
signale="$(polices_signalees "$travail/decouvert.mypub" "$travail/decouvert.pdf")"
for demandee in "Police Inexistante" "Police Absente De Cette Machine"; do
    if ! printf '%s\n' "$signale" | grep -qF "« $demandee »"; then
        echo "ÉCHEC : sans le fragment qui recouvre tout, « $demandee » est dessinée" >&2
        echo "et l'export ne la nomme pas. Avertissements obtenus :" >&2
        echo "${signale:-    (aucun)}" >&2
        exit 1
    fi
done
if ! familles_du_pdf "$travail/decouvert.pdf" | grep -q '^Noto Sans'; then
    echo "ÉCHEC : la contre-épreuve devait dessiner le substitut, Noto Sans ; le PDF embarque :" >&2
    familles_du_pdf "$travail/decouvert.pdf" | sed 's/^/    /' >&2
    exit 1
fi
echo "  3. contre-épreuve : découvertes, les deux polices absentes sont nommées et leur substitut est dans le PDF."

# ------------------------------------------------------------------------- 4

attendus=6
if ! dotnet test MyPublisher.Tests/MyPublisher.Tests.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=Q8" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests du noyau de Q8 ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=Q8 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  4. $joues tests du noyau joués."

echo "Q8 : l'export n'avertit que des polices qu'il dessine."
