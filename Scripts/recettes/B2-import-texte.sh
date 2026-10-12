#!/usr/bin/env bash
#
# Recette du poste B2 — importer un fichier texte dans un bloc.
#
# Le plan la donne en une ligne : « un .txt de 3 000 signes arrive dans un bloc, et A2
# signale qu'il déborde ». Le fichier qu'on importe est le moins commode qui soit :
# Latin-1, fins de ligne CRLF, une fin de ligne en fin de fichier — ce qu'un éditeur de
# Windows écrit. Le sélecteur de fichier ne s'ouvre pas sans affichage ; le geste est
# pris là où la fenêtre le passe au modèle de vue, « ImporterTexte », avec un chemin.
#
# Ce qu'elle éprouve :
#
#   1. le geste — « dotnet test » joue les tests du poste, ceux du noyau (encodage, fins
#      de ligne, refus du vide, du binaire, de l'UTF-16 sans marque et du trop gros) et
#      ceux de la fenêtre (le bloc rempli, le marqueur de débordement à l'écran, le
#      style du bloc, l'annulation, l'import pendant une saisie ouverte, les fichiers
#      refusés ou fermés en lecture, le menu et le panneau). Le premier test
#      de la fenêtre écrit le fichier importé et les documents d'avant, d'après et
#      d'après l'annulation là où la variable d'environnement le demande : c'est sur eux
#      que la suite mesure, indépendamment du code qui les a produits ;
#   2. le fichier est bien ce qu'on dit : pas de l'UTF-8, des CRLF, et trois mille signes
#      une fois les sauts ramenés à un caractère — et le bloc porte exactement ce texte ;
#   3. A2 le signale à l'export : « Article : le texte déborde du bloc ». Le document
#      d'avant, lui, ne déborde pas ;
#   4. les accents arrivent jusqu'au PDF : chaque mot accentué du début du texte se relit
#      dans le flux. Contre-épreuve : le même fichier lu en UTF-8 sans repli sur le
#      Latin-1 — ce qu'un lecteur naïf fait — échoue à cette même mesure ;
#   5. une annulation rend le bloc d'avant, octet pour octet.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

if [ ! -x "$binaire" ]; then
    echo "ÉCHEC : binaire introuvable ($binaire). Compiler d'abord." >&2
    exit 1
fi

# ------------------------------------------------------------------------------- 1

# Le dossier n'existe pas avant les tests : « dotnet test » sort en 0 quand AUCUN test
# ne correspond au filtre, et des documents restés d'un passage précédent feraient
# passer la suite sans que rien n'ait été importé.
import="$travail/import"

jouer() {   # <projet> <minimum>
    local projet="$1" minimum="$2" sortie="$travail/tests-$1.txt" joues
    if ! MYPUBLISHER_RECETTE_IMPORT="$import" \
         dotnet test "$projet/$projet.csproj" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
            --nologo -v q --filter "poste=B2" > "$sortie" 2>&1; then
        echo "ÉCHEC : les tests du poste ne passent pas ($projet)." >&2
        sed 's/^/    /' "$sortie" >&2
        exit 1
    fi

    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$sortie" | head -1)"
    if [ -z "$joues" ] || [ "$joues" -lt "$minimum" ]; then
        echo "ÉCHEC : ${joues:-0} test(s) joué(s) dans $projet, $minimum attendus." >&2
        sed 's/^/    /' "$sortie" >&2
        exit 1
    fi
    echo "  1. $projet : $joues tests du poste joués."
}

jouer MyPublisher.Tests 20
jouer MyPublisher.Tests.Interface 11

for fichier in source.txt avant.mypub importe.mypub annule.mypub; do
    if [ ! -f "$import/$fichier" ]; then
        echo "ÉCHEC : le test d'import n'a pas écrit « $fichier »." >&2
        exit 1
    fi
done

# ------------------------------------------------------------------------------- 2

if ! python3 - "$import/source.txt" "$import/importe.mypub" <<'PY'
import json, sys

octets = open(sys.argv[1], "rb").read()
try:
    octets.decode("utf-8")
    sys.exit("    ÉCHEC : le fichier importé est de l'UTF-8 valide ; il devait être en Latin-1.")
except UnicodeDecodeError:
    pass
if b"\r\n" not in octets:
    sys.exit("    ÉCHEC : le fichier importé ne porte pas de fins de ligne CRLF.")

texte = octets.decode("latin-1").replace("\r\n", "\n")
if texte.endswith("\n"):
    texte = texte[:-1]
if len(texte) != 3000:
    sys.exit(f"    ÉCHEC : {len(texte)} signes dans le fichier, 3000 attendus.")

document = json.load(open(sys.argv[2], encoding="utf-8"))
bloc = next(b for b in document["Pages"][0]["Frames"] if b.get("Name") == "Article")
if bloc["Text"] != texte:
    sys.exit("    ÉCHEC : le bloc ne porte pas le texte du fichier, au signe près.")
if bloc.get("Spans") or bloc.get("Paragraphs"):
    sys.exit("    ÉCHEC : le bloc garde des fragments ou des écarts d'avant l'import.")
PY
then
    echo "ÉCHEC : le fichier ou le bloc n'est pas ce que la recette annonce." >&2
    exit 1
fi
echo "  2. le fichier : Latin-1, CRLF, 3000 signes ; le bloc porte ce texte au signe près, au style du bloc."

# ------------------------------------------------------------------------------- 3

exporter() {   # <document> <pdf>
    "$binaire" "$1" --export "$2" > /dev/null 2> "${2%.pdf}.txt"
}

exporter "$import/avant.mypub" "$travail/avant.pdf"
exporter "$import/importe.mypub" "$travail/importe.pdf"

if grep -q 'déborde du bloc' "$travail/avant.txt"; then
    echo "ÉCHEC : le bloc déborde déjà avant l'import — la mesure ne mesure rien." >&2
    sed 's/^/    /' "$travail/avant.txt" >&2
    exit 1
fi
if ! grep -qF 'Page 1 · Article : le texte déborde du bloc' "$travail/importe.txt"; then
    echo "ÉCHEC : l'export ne signale pas le débordement du bloc rempli." >&2
    sed 's/^/    /' "$travail/importe.txt" >&2
    exit 1
fi
echo "  3. A2 : $(grep -oF 'Article : le texte déborde du bloc de' "$travail/importe.txt") $(sed -n 's/.*déborde du bloc de \([0-9.]* pt\).*/\1/p' "$travail/importe.txt") ; avant l'import, rien."

# ------------------------------------------------------------------------------- 4

# La mesure, écrite une fois et jouée deux fois : sur le document importé, où elle doit
# passer, puis sur sa contrefaçon, où elle doit échouer. « pdftotext » lit aussi le texte
# que la découpe du bloc cache (voir la recette de A2) : on ne lit donc que le début,
# qui se voit.
cat > "$travail/accents.py" <<'PY'
import re, subprocess, sys

source, pdf = sys.argv[1:3]
texte = open(source, "rb").read().decode("latin-1").replace("\r\n", "\n")
# Coupé à un blanc : un mot tronqué à la coupe ne se relirait pas entier.
debut = texte[: texte.rfind(" ", 0, 400)]
attendus = [m for m in re.findall(r"\w+", debut) if not m.isascii()]
if len(attendus) < 5:
    sys.exit(f"    ÉCHEC : {len(attendus)} mot(s) accentué(s) au début du texte — la mesure ne mesure rien.")

lu = subprocess.run(["pdftotext", "-enc", "UTF-8", pdf, "-"], capture_output=True, check=True).stdout.decode("utf-8")
mots = set(re.findall(r"\w+", lu))
manquants = [m for m in attendus if m not in mots]
if manquants:
    sys.exit(f"    ÉCHEC : mots accentués absents du PDF : {' '.join(manquants[:8])}")
print(f"    {len(attendus)} mots accentués relus dans le PDF : {' '.join(attendus[:6])}…")
PY

if ! python3 "$travail/accents.py" "$import/source.txt" "$travail/importe.pdf"; then
    echo "ÉCHEC : les accents du fichier Latin-1 ne sont pas arrivés jusqu'au PDF." >&2
    exit 1
fi

# La contrefaçon : le même fichier lu en UTF-8 sans repli, chaque octet accentué devenu
# « � ». C'est ce qu'aurait donné un lecteur qui ne devine pas l'encodage.
python3 - "$import/source.txt" "$import/importe.mypub" "$travail/naif.mypub" <<'PY'
import json, sys
texte = open(sys.argv[1], "rb").read().decode("utf-8", errors="replace").replace("\r\n", "\n")
document = json.load(open(sys.argv[2], encoding="utf-8"))
bloc = next(b for b in document["Pages"][0]["Frames"] if b.get("Name") == "Article")
bloc["Text"] = texte[:-1] if texte.endswith("\n") else texte
json.dump(document, open(sys.argv[3], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
exporter "$travail/naif.mypub" "$travail/naif.pdf"
if python3 "$travail/accents.py" "$import/source.txt" "$travail/naif.pdf" > "$travail/contre.txt" 2>&1; then
    echo "ÉCHEC : la mesure des accents passe sur un texte lu sans repli Latin-1." >&2
    sed 's/^/    /' "$travail/contre.txt" >&2
    exit 1
fi
echo "  4. les accents arrivent jusqu'au PDF ; contre-épreuve : lus en UTF-8 sans repli, la même mesure échoue."

# ------------------------------------------------------------------------------- 5

if ! cmp -s "$import/avant.mypub" "$import/annule.mypub"; then
    echo "ÉCHEC : après une annulation, le document n'est pas celui d'avant l'import." >&2
    diff "$import/avant.mypub" "$import/annule.mypub" | head -20 | sed 's/^/    /' >&2
    exit 1
fi
if cmp -s "$import/avant.mypub" "$import/importe.mypub"; then
    echo "ÉCHEC : le document importé est celui d'avant — l'import n'a rien posé." >&2
    exit 1
fi
echo "  5. une annulation rend le document d'avant l'import, octet pour octet."

echo "B2 : 3000 signes en Latin-1 arrivent dans le bloc avec leurs accents, A2 signale le débordement, une annulation le défait."
