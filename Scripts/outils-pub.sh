#!/usr/bin/env bash
#
# Met « pub2raw » à disposition, sans rien installer sur le système.
#
# MyPublisher délègue la lecture du format .pub à libmspub, comme il délègue les
# polices à fontconfig : le .pub est un format fermé, et libmspub est le lecteur
# qu'emploient LibreOffice et le reste du monde libre.
#
# Sur une machine ordinaire, « sudo apt install libmspub-tools » suffit et ce script
# n'a rien à faire. Il sert quand on n'a pas les droits d'installation : il récupère
# les paquets et n'en extrait que le programme et ses bibliothèques propres, dans
# « .outils/mspub », qui n'est pas suivi par le dépôt.
#
#   Scripts/outils-pub.sh          installe si besoin, et dit où
#   Scripts/outils-pub.sh --chemin écrit seulement le chemin du programme
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
local_bin="$racine/.outils/mspub/bin/pub2raw"

# Le PATH d'abord : une installation en règle vaut mieux qu'une copie locale.
if chemin="$(command -v pub2raw 2>/dev/null)"; then
    [ "${1-}" = "--chemin" ] && { echo "$chemin"; exit 0; }
    echo "pub2raw est déjà installé : $chemin"
    exit 0
fi

if [ -x "$local_bin" ]; then
    [ "${1-}" = "--chemin" ] && { echo "$local_bin"; exit 0; }
    echo "pub2raw est déjà extrait localement : $local_bin"
    exit 0
fi

if [ "${1-}" = "--chemin" ]; then
    echo "" ; exit 1
fi

echo "Récupération de libmspub…"
travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

(
    cd "$travail"
    apt-get download libmspub-tools libmspub-0.1-1 librevenge-0.0-0 libicu78 zlib1g > /dev/null
    for paquet in *.deb; do dpkg-deb -x "$paquet" racine/; done
)

mkdir -p "$racine/.outils/mspub/bin" "$racine/.outils/mspub/lib"
cp "$travail/racine/usr/bin/pub2raw" "$racine/.outils/mspub/bin/"

# Seulement les bibliothèques dont le programme a besoin : le reste du paquet ne sert
# à rien et pèserait dix fois plus.
LD_LIBRARY_PATH="$travail/racine/usr/lib/x86_64-linux-gnu" \
    ldd "$travail/racine/usr/bin/pub2raw" |
    awk -v t="$travail" '$3 ~ "^"t {print $3}' |
    while read -r bibliotheque; do cp "$bibliotheque" "$racine/.outils/mspub/lib/"; done

echo "Extrait dans $local_bin"
