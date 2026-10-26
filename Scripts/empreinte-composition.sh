#!/usr/bin/env bash
#
# Relève l'empreinte de la composition du corpus, pour prouver qu'un poste ne la change
# pas — et non l'affirmer. Écrit pour le poste N3, qui donne à chaque morceau composé sa
# position dans le texte en promettant que « la sortie du compositeur ne change pas » :
# coupures, largeurs, abscisses, texte des lignes, écran et PDF.
#
#   Scripts/empreinte-composition.sh <dossier>                  relève, dans <dossier>
#   Scripts/empreinte-composition.sh --comparer <avant> <après> compare deux relevés
#
# Le corpus : Samples/*.mypub, MyPublisher/Templates/*.mypub, et les deux documents ATR
# de MyPublisher/Books/, ouverts par pub2raw. Pour chaque document, deux fichiers :
#
#   <nom>.ecran.txt — « MyPublisher.Banc empreinte », sur la vraie fenêtre : pour chaque
#       contrôle de texte de chaque page, groupes et cases compris, les lignes que le noyau
#       compose à la règle de l'écran et à celle du PDF — texte, cadre, hauteur, colonne,
#       et chaque morceau avec son style, son abscisse et sa largeur, au bit près, puis les
#       décalages de justification —, ce que l'écran dessine (TexteCompose.Poser et
#       Souligner, les fonctions mêmes du rendu), et l'empreinte des pixels de chaque page ;
#   <nom>.pdf.txt — le PDF du vrai binaire (« --export », sans affichage, à date fixe) :
#       son empreinte SHA-256, une fois retirés ce que PDFsharp tire au hasard ou lit à
#       l'horloge à chaque export — l'étiquette des sous-ensembles de polices
#       (« NUMXXR+ »), les uuid du XMP, l'/ID, l'heure de création du fichier —, puis les
#       mots que « pdftotext -bbox » y lit, avec leur boîte.
#
# Deux exports successifs du même document donnent la même empreinte : c'est vérifié par
# « --comparer » sur deux relevés pris du même code, qui doivent être égaux.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$racine"

if [ "${1-}" = "--comparer" ]; then
    avant="${2-}"
    apres="${3-}"
    if [ ! -d "$avant" ] || [ ! -d "$apres" ]; then
        echo "Usage : $0 --comparer <avant> <après>" >&2
        exit 2
    fi
    if diff -r "$avant" "$apres" > /dev/null; then
        echo "Empreintes identiques : $(find "$avant" -type f | wc -l) fichiers, $(cat "$avant"/*.ecran.txt | grep -c '^      ligne ') lignes composées, $(cat "$avant"/*.ecran.txt | grep -c '^  pixels ') pages."
        exit 0
    fi
    echo "ÉCHEC : les empreintes diffèrent." >&2
    diff -r "$avant" "$apres" | head -60 >&2
    exit 1
fi

sortie="${1-}"
if [ -z "$sortie" ]; then
    echo "Usage : $0 <dossier> | --comparer <avant> <après>" >&2
    exit 2
fi
mkdir -p "$sortie"

application="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"
for binaire in "$application" "$banc"; do
    if [ ! -x "$binaire" ]; then
        echo "ÉCHEC : binaire introuvable ($binaire). Compiler d'abord." >&2
        exit 1
    fi
done

outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$outil" ] || [ ! -x "$outil" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable : les deux documents ATR ne s'ouvrent pas." >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$outil"
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

documents=(Samples/*.mypub MyPublisher/Templates/*.mypub
           "MyPublisher/Books/ATA 21 - Air conditioning.pub"
           "MyPublisher/Books/Fiches limitations et procédures d'urgences.pub")

for document in "${documents[@]}"; do
    nom="$(basename "$document")"
    nom="${nom%.*}"
    nom="${nom// /-}"

    if ! "$banc" empreinte "$document" --sortie "$sortie/$nom.ecran.txt" > "$travail/banc.txt" 2>&1; then
        echo "ÉCHEC : le banc n'a pas relevé $document." >&2
        sed 's/^/    /' "$travail/banc.txt" >&2
        exit 1
    fi

    if ! env -u DISPLAY -u WAYLAND_DISPLAY "$application" "$document" --date 2026-09-06 \
            --export "$travail/sortie.pdf" > "$travail/export.txt" 2>&1; then
        echo "ÉCHEC : l'export de $document n'a pas abouti." >&2
        sed 's/^/    /' "$travail/export.txt" >&2
        exit 1
    fi

    python3 -I - "$travail/sortie.pdf" > "$sortie/$nom.pdf.txt" <<'PY'
import hashlib, re, sys
donnees = open(sys.argv[1], "rb").read()
# Ce que PDFsharp tire au hasard ou lit à l'horloge à chaque export, et rien d'autre :
# l'étiquette de six majuscules des sous-ensembles de polices, les uuid du XMP, l'/ID du
# fichier, l'heure de création — celle du fichier, et non la date des champs, que
# « --date » fixe.
donnees = re.sub(rb"/[A-Z]{6}\+", b"/XXXXXX+", donnees)
donnees = re.sub(rb"uuid:[0-9a-f-]{36}", b"uuid:X", donnees)
donnees = re.sub(rb"/ID\s*\[\s*<[0-9A-Fa-f]+>\s*<[0-9A-Fa-f]+>\s*\]", b"/ID[X]", donnees)
donnees = re.sub(rb"\(D:\d{14}[^)]*\)", b"(D:X)", donnees)
donnees = re.sub(rb"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:[+-]\d{2}:\d{2}|Z)?", b"X", donnees)
print(f"pdf {len(donnees)} octets, sha256 normalisé {hashlib.sha256(donnees).hexdigest()}")
PY
    # pdftotext dit « no word list » d'une page sans un mot : ce n'est pas une erreur.
    pdftotext -bbox "$travail/sortie.pdf" - 2> /dev/null | grep -E '^ *<(page|word)' >> "$sortie/$nom.pdf.txt"
    echo "  $nom : $(head -1 "$travail/banc.txt")"
done

echo "Empreinte de ${#documents[@]} documents dans $sortie."
