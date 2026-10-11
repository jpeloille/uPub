#!/usr/bin/env bash
#
# Recette du poste B1 — les modèles.
#
# « Ctrl+N, choisir « dépliant », modifier un texte, enregistrer — le fichier d'origine
# est intact. » Jouée à la main le 5 septembre 2026, puis jetée ; la voici rejouable,
# et étendue aux quatre modèles livrés.
#
#   1. LE CATALOGUE LIVRÉ. Les quatre modèles de MyPublisher/Templates sont ceux que la
#      compilation pose à côté du binaire, octet pour octet — le catalogue lit ceux-là,
#      pas les sources. Chacun se relit et se réécrit au caractère près : un modèle est
#      un document ordinaire, lu par le même code. Et aucun ne porte de chemin d'image
#      absolu : un modèle qui renverrait à un fichier de la machine qui l'a composé
#      s'ouvrirait troué partout ailleurs, sans que rien ne le dise ici.
#
#   2. LE GESTE, joué par MyPublisher.Tests.Interface sur la vraie fenêtre, filtre
#      « poste=B1 » : Ctrl+N ouvre le catalogue — la page blanche et les quatre
#      modèles —, Entrée part du dépliant, et le document n'a pas de chemin ; puis,
#      chacun des quatre choisi par Ctrl+N et Entrée, une retouche, Ctrl+S, puis un
#      enregistrement ailleurs : le modèle est identique octet pour octet, et la copie
#      porte la retouche. Un
#      script de shell ne frappe pas de touches ; il appelle les tests qui le font, et
#      compte ceux qui ont tourné.
#
#   3. L'EXPORT. Les quatre modèles s'exportent SANS UN AVERTISSEMENT — avec un jeu de
#      coordonnées complet, logo compris, et une date fixée. Sans coordonnées, l'export
#      avertirait à bon droit que le logo manque ; et c'est une fois les champs remplis
#      qu'un bloc trop court pour son texte se révèle. Le contrôle des documents livrés
#      d'A2 mesure le texte SAISI, qui est vide sous un champ : il ne le voit pas.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
livres="MyPublisher/bin/Debug/net10.0/Templates"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile plutôt que de lancer les tests avec « --no-build » : un binaire périmé
# survit à une modification des sources, et la recette se déclarerait verte sur du code
# qu'elle n'a pas éprouvé (cf. Scripts/recettes/A4-superposition.sh).
if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

if [ ! -x "$binaire" ]; then
    echo "ÉCHEC : binaire introuvable après compilation ($binaire)." >&2
    exit 1
fi

# Les quatre modèles, et leur nombre de pages. Écrits ici plutôt que découverts : un
# modèle qui disparaîtrait du dossier doit faire échouer la recette, pas la raccourcir.
declare -A pages=(
    [affiche.mypub]=1
    [bulletin.mypub]=2
    [carte-de-visite.mypub]=1
    [depliant-trois-volets.mypub]=2
)

# ---------------------------------------------------------------- 1. le catalogue

presents="$(cd MyPublisher/Templates && ls -1 *.mypub | sort | tr '\n' ' ')"
voulus="$(printf '%s\n' "${!pages[@]}" | sort | tr '\n' ' ')"
if [ "$presents" != "$voulus" ]; then
    echo "ÉCHEC : MyPublisher/Templates porte « $presents», la recette attend « $voulus»." >&2
    exit 1
fi

for modele in "${!pages[@]}"; do
    source="MyPublisher/Templates/$modele"

    if ! cmp -s "$source" "$livres/$modele"; then
        echo "ÉCHEC : le modèle livré à côté du binaire n'est pas celui des sources ($modele)." >&2
        exit 1
    fi

    "$binaire" "$source" --resave "$travail/relu.mypub" > /dev/null
    if ! diff -u "$source" "$travail/relu.mypub" > "$travail/ecart.diff"; then
        echo "ÉCHEC : relu puis réécrit, le modèle $modele n'est plus le même." >&2
        head -20 "$travail/ecart.diff" >&2
        exit 1
    fi
done

if grep -nE '"(Path|LogoPath)": "/' MyPublisher/Templates/*.mypub > "$travail/absolus.txt"; then
    echo "ÉCHEC : un modèle livré porte un chemin d'image absolu." >&2
    cat "$travail/absolus.txt" >&2
    exit 1
fi

echo "  1. quatre modèles livrés, identiques aux sources, relus au caractère près, sans chemin absolu"

# -------------------------------------------------------------------- 2. le geste

attendus=5
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=B1" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : le geste de B1 ne passe pas." >&2
    exit 1
fi

joues="$(grep -o 'Passed: *[0-9]*' "$travail/tests.txt" | head -1 | grep -o '[0-9]*$' || true)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=B1 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi

echo "  2. Ctrl+N, choix, retouche, Ctrl+S puis enregistrer ailleurs : $joues tests sur la vraie fenêtre"

# ------------------------------------------------------------------- 3. l'export

# Un logo de huit pixels, et un jeu de coordonnées où rien ne manque.
python3 - "$travail/logo.png" <<'PY'
import struct, sys, zlib

cote = 8
brut = b"".join(b"\x00" + bytes([31, 59, 87]) * cote for _ in range(cote))
def bloc(tag, donnees):
    corps = tag + donnees
    return struct.pack(">I", len(donnees)) + corps + struct.pack(">I", zlib.crc32(corps) & 0xFFFFFFFF)
entete = struct.pack(">IIBBBBB", cote, cote, 8, 2, 0, 0, 0)
open(sys.argv[1], "wb").write(b"\x89PNG\r\n\x1a\n" + bloc(b"IHDR", entete)
                               + bloc(b"IDAT", zlib.compress(brut)) + bloc(b"IEND", b""))
PY

cat > "$travail/coordonnees.json" <<JSON
{
  "ActiveSetName": "Recette",
  "Sets": [
    { "SetName": "Recette", "Name": "Jeanne Martin", "Title": "Responsable des publications",
      "Organisation": "Ateliers Martin", "Address": "12 rue Lagarde\n98800 Nouméa",
      "Phone": "+687 26 26 26", "Tagline": "Composé avec soin", "LogoPath": "$travail/logo.png" }
  ]
}
JSON

for modele in $(printf '%s\n' "${!pages[@]}" | sort); do
    "$binaire" "MyPublisher/Templates/$modele" --export "$travail/sortie.pdf" \
        --date 2026-09-06 --contacts "$travail/coordonnees.json" \
        > "$travail/export.txt" 2> "$travail/avertissements.txt"

    if [ -s "$travail/avertissements.txt" ]; then
        echo "ÉCHEC : le modèle $modele s'exporte avec des avertissements." >&2
        cat "$travail/avertissements.txt" >&2
        exit 1
    fi

    if ! grep -q "^${pages[$modele]} page(s) exportée(s)" "$travail/export.txt"; then
        echo "ÉCHEC : $modele — ${pages[$modele]} page(s) attendue(s)." >&2
        cat "$travail/export.txt" >&2
        exit 1
    fi

    echo "  3. $modele : ${pages[$modele]} page(s), aucun avertissement"
done

echo "B1 : quatre modèles livrés, ouverts en copie sans chemin, intacts après retouche, exportés sans un mot."
