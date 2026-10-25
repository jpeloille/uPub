#!/usr/bin/env bash
#
# Recette du poste Q23 — sélectionner un bloc ne fige pas la fenêtre.
#
# Trouvé à la fusion de Q14 : une pression sur un bloc non sélectionné rendait la main
# après 170 à 470 ms sur ATA 21, et le double-clic, qu'Avalonia reconnaît à l'horloge
# (deux pressions à moins de 500 ms), tombait une fois sur deux. La cause, mesurée le
# 25 octobre 2026 : les trois sections du dock et les réglages de la barre contextuelle
# étaient des ContentControl liés au bloc seul, qu'une sélection vide vidait, et que la
# pression suivante reconstruisait — un millier de visuels par pression. Ils sont
# désormais construits une fois par sorte de bloc, et gardés (Views/GabaritsGardes.cs).
#
# Trois temps :
#
#   1. LES SOURCES : aucun réglage du bloc n'est plus un ContentControl lié au bloc seul —
#      les trois sections du dock et la barre sont des GabaritsGardes —, et les tests du
#      double-clic ne sélectionnent plus le bloc par le modèle de vue avant le geste ;
#   2. LES TESTS, par leur trait « poste=Q23 » (PressionTests) : au pointeur synthétisé,
#      sur ATA 21, la pression sur chaque bloc non sélectionné rend la main en moins de
#      100 ms, médiane de trois passes ; les champs du dock et de la barre sont les mêmes
#      d'une sélection à l'autre, et une pression n'ajoute presque rien à la fenêtre ; un
#      champ masqué rend le focus ; la liste des blocs ne défile que pour montrer le bloc ;
#   3. LE DOUBLE-CLIC AU GESTE ENTIER, le premier clic prenant le bloc : les deux tests de
#      Q14 qui le jouent, vingt fois de suite, chacun dans un processus neuf — c'est la
#      première pression d'un processus qui est la plus lente, et c'est là qu'ils tombaient.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

# Le lecteur des .pub, cherché comme la recette R5 le cherche : sans lui, ATA 21 ne s'ouvre
# pas, et la recette le dit plutôt que de se déclarer verte.
outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$outil" ] || [ ! -x "$outil" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable." >&2
    echo "    sudo apt install libmspub-tools    ou    Scripts/outils-pub.sh" >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$outil"
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# ------------------------------------------------------------------------ 1. les sources

python3 - "$racine" <<'PY'
import pathlib, re, sys
import xml.etree.ElementTree as ET

racine = pathlib.Path(sys.argv[1])
A = "{https://github.com/avaloniaui}"
VIEWS = "{using:MyPublisher.Views}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

for nom, attendus in (("MainWindow.axaml", 3), ("BarreContextuelle.axaml", 1)):
    vue = ET.parse(racine / "MyPublisher/Views" / nom).getroot()
    lies = [e for e in vue.iter() if e.get("Content") == "{Binding SingleSelected}"]
    exige(not lies, f"{nom} : {len(lies)} contrôle(s) dont le contenu est le bloc seul — une sélection vide "
                    "les vide, et la pression suivante les reconstruit.")
    gardes = [e for e in vue.iter(VIEWS + "GabaritsGardes") if e.get("Bloc") == "{Binding SingleSelected}"]
    exige(len(gardes) == attendus, f"{nom} : {len(gardes)} réglages gardés liés au bloc seul, {attendus} attendu(s).")
    for g in gardes:
        exige(list(g.iter(A + "DataTemplate")), f"{nom} : des réglages gardés sans gabarit.")
    if nom == "BarreContextuelle.axaml":
        exige(any(g.get(X + "Name") == "ReglagesDuBloc" for g in gardes),
              "la barre : les réglages du bloc ne s'appellent plus « ReglagesDuBloc ».")
print("    a. les trois sections du dock et la barre : des réglages gardés, aucun contenu lié au bloc seul")

double_clic = (racine / "MyPublisher.Tests.Interface/DoubleClicTests.cs").read_text(encoding="utf-8")
for test in ("Un_double_clic_ouvre_la_saisie_sans_rien_reecrire", "Un_double_clic_garde_langle_dun_bloc_tourne_a_la_main"):
    corps = re.search(r"public void " + test + r"\(\)\s*\{(.*?)\n    \}", double_clic, re.S)
    exige(corps is not None, f"le test « {test} » n'est plus dans DoubleClicTests.")
    if corps:
        exige("SetSelection([bloc])" not in corps.group(1) and "SelectOnly(bloc)" not in corps.group(1),
              f"« {test} » sélectionne encore le bloc par le modèle de vue avant le geste.")
        exige(re.search(r"Cliquer\(fenetre, centre\);\s*Assert\.Same\(bloc, vm\.SingleSelected\);\s*Cliquer\(fenetre, centre\);",
                        corps.group(1)) is not None,
              f"« {test} » ne prend plus le bloc par son premier clic.")
print("    b. les deux tests du double-clic : le premier clic prend le bloc")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources"

# ------------------------------------------------------------------------ 2. les tests

attendus=4
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo --filter "poste=Q23" --logger "console;verbosity=detailed" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : une pression sur un bloc non sélectionné fige encore la fenêtre, ou les réglages du bloc se reconstruisent." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=Q23 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi
# Les médianes, pour qui lit la recette : ce que la main ressent sur chaque bloc.
grep -E "médiane" "$travail/tests.txt" | sed 's/^ */       /' || true
echo "  2. les tests du poste : $joues"

# ------------------------------------------------------------------------ 3. le double-clic

for passage in $(seq 1 20); do
    if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
            --nologo -v q --filter "FullyQualifiedName~DoubleClicTests.Un_double_clic" > "$travail/double-clic.txt" 2>&1; then
        sed 's/^/    /' "$travail/double-clic.txt" >&2
        echo "ÉCHEC : le double-clic au geste entier tombe au passage $passage sur 20." >&2
        exit 1
    fi
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/double-clic.txt" | head -1)"
    if [ "${joues:-0}" -lt 2 ]; then
        echo "ÉCHEC : 2 tests du double-clic attendus, ${joues:-0} joués au passage $passage." >&2
        exit 1
    fi
done
echo "  3. le double-clic au geste entier : 20 passages sur 20, chacun dans un processus neuf"

echo "Q23 : une pression sur un bloc non sélectionné rend la main en moins de 100 ms ; les réglages du bloc sont construits une fois et gardés."
