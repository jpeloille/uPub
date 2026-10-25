#!/usr/bin/env bash
#
# Recette du poste N3 — chaque morceau composé sait d'où il vient.
#
# Le morceau porte la position de son premier caractère dans le texte, la ligne sa plage —
# blancs rognés compris, saut exclu — et le rang de son paragraphe ; la découpe suit les
# positions du texte d'origine, sans le normaliser. La vue en tire, depuis la passe même du
# dessin, une ligne posée par ligne, lignes vides comprises, et passe d'une position à un
# point du bloc (PointDe) et retour (IndexSous) : c'est le préalable du clic simple (N12), de
# la mesure de la saisie (N7) et des deux voies de R7. Et la sortie du compositeur ne change
# pas. Ce qu'elle prouve :
#
#   1. LES SOURCES, lues par un script.
#      a. Morceau finit par « int Debut », LigneComposee par « int Debut, int Fin, int
#         Paragraphe » ; la découpe ne normalise plus le texte par Replace.
#      b. La géométrie lit ce que le dessin pose (règle 7) : Disposer ajoute une LignePosee
#         par ligne, et la géométrie ne compose rien elle-même — ni Composer, ni Mesurer.
#      c. L'exception déclarée — un « \r\n » que deux fragments se partagent compte un
#         paragraphe et non deux — ne touche aucun document du dépôt : aucune chaîne de
#         Samples/ ni de MyPublisher/Templates/ ne porte de « \r », et l'import d'un .pub
#         retire les siens.
#   2. LES TESTS PURS « poste=N3 » du noyau, sur des textes faits pour : espaces multiples,
#      tabulations, renvois, paragraphes vides, « \r\n », « \r » seul, paires de
#      substitution, colonnes. Chaque morceau est le texte à sa position ; les lignes se
#      suivent et couvrent le texte ; après le dernier morceau, rien que du blanc ; le
#      paragraphe est celui de Recalage ; la sortie garde textes, abscisses et largeurs ;
#      l'abscisse d'une position et la position d'une abscisse se répondent, taquets,
#      justification et blancs pendants compris.
#   3. LES TESTS D'INTERFACE « poste=N3 », sur la vraie fenêtre, l'exemple, ATA 21, les
#      Fiches et recette-colonnes : PointDe(m.Debut).X est l'ancre où le dessin a posé le
#      morceau, au millionième de point ; IndexSous(PointDe(i)) == i pour chaque position ;
#      un bloc tourné y entre par Frame.ToLocal ; un bloc vide a sa ligne, un champ n'a pas
#      de géométrie.
#   4. L'EMPREINTE, sur demande : « --empreinte <référence> » relève l'empreinte de la
#      composition du corpus (Scripts/empreinte-composition.sh : les lignes et les morceaux
#      à la règle de l'écran et à celle du PDF, ce que l'écran pose et souligne, les pixels
#      de chaque page, le PDF exporté et ses mots) et la compare à un relevé de référence,
#      pris avant le poste à prouver. Elle demande quelques minutes et un relevé que le
#      dépôt ne garde pas — les pixels dépendent des polices de la machine — : verifier.sh
#      ne la joue pas.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

reference=""
if [ "${1:-}" = "--empreinte" ]; then
    reference="${2:-}"
    if [ ! -d "$reference" ]; then
        echo "Usage : $0 [--empreinte <dossier de référence>]" >&2
        exit 2
    fi
    reference="$(cd "$reference" && pwd)"
fi

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# ---------------------------------------------------------------------- 1. les sources

python3 - "$racine" <<'PY'
import json, pathlib, re, sys

racine = pathlib.Path(sys.argv[1])
compositeur = (racine / "MyPublisher.Core/Models/Compositeur.cs").read_text(encoding="utf-8")
vue = (racine / "MyPublisher/Views/TexteCompose.cs").read_text(encoding="utf-8")
importeur = (racine / "MyPublisher.Core/Services/PubImporter.cs").read_text(encoding="utf-8")

echecs = []
def refuser(message):
    echecs.append(message)

# ------------------------------------------------------------- a. le morceau et la ligne

if not re.search(r"record struct Morceau\([^)]*,\s*int Debut\);", compositeur):
    refuser("Morceau ne finit pas par « int Debut ».")
if not re.search(r"record struct LigneComposee\([^)]*int Debut, int Fin, int Paragraphe\)", compositeur, re.S):
    refuser("LigneComposee ne finit pas par « int Debut, int Fin, int Paragraphe ».")
decouper = re.search(r"private static List<Unite> Decouper\(.*?\n    \}\n", compositeur, re.S)
if decouper is None:
    refuser("Decouper est introuvable.")
elif ".Replace(" in decouper.group(0):
    refuser("Decouper normalise encore le texte par Replace : les positions du texte d'origine s'y perdent.")
print("    a. Morceau.Debut, LigneComposee.Debut, Fin et Paragraphe ; la découpe sans Replace")

# --------------------------------------------------------- b. la géométrie lit le dessin

disposer = re.search(r"Disposer\(\s*MetriquesAvalonia metriques.*?\n    \}\n", vue, re.S)
if disposer is None or "posees.Add(new LignePosee(" not in disposer.group(0):
    refuser("Disposer ne pose pas de LignePosee : la géométrie ne lit pas la passe du dessin.")
geometrie = vue[vue.find("public sealed class GeometrieDuTexte"):]
if "public sealed class GeometrieDuTexte" not in vue:
    refuser("GeometrieDuTexte est introuvable.")
elif re.search(r"Compositeur\.Composer\(|Mesurer\(|Disposer\(", geometrie):
    refuser("GeometrieDuTexte compose elle-même : elle doit lire les lignes posées (règle 7).")
methode = re.search(r"public GeometrieDuTexte\? Geometrie\(\).*?\n    \}\n", vue, re.S)
if methode is None or "Disposer(" not in methode.group(0):
    refuser("TexteCompose.Geometrie ne passe pas par Disposer.")
print("    b. une LignePosee par ligne, posée par Disposer ; la géométrie n'en recompose aucune")

# ------------------------------------------------------- c. aucun « \r » dans le dépôt

def chaines(valeur):
    if isinstance(valeur, str):
        yield valeur
    elif isinstance(valeur, dict):
        for v in valeur.values():
            yield from chaines(v)
    elif isinstance(valeur, list):
        for v in valeur:
            yield from chaines(v)

documents = sorted((racine / "Samples").glob("*.mypub")) + sorted((racine / "MyPublisher/Templates").glob("*.mypub"))
if len(documents) < 10:
    refuser(f"{len(documents)} documents .mypub trouvés, au moins dix attendus.")
for chemin in documents:
    porteurs = [c for c in chaines(json.loads(chemin.read_text(encoding="utf-8"))) if "\r" in c]
    if porteurs:
        refuser(f"{chemin.relative_to(racine)} porte {len(porteurs)} chaîne(s) avec « \\r » : l'exception de N3 le toucherait.")
if 'brut.Replace("\\r", "")' not in importeur:
    refuser("l'import d'un .pub ne retire plus les « \\r ».")
print(f"    c. aucun « \\r » dans les {len(documents)} documents .mypub du dépôt ; l'import d'un .pub retire les siens")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le morceau et la ligne savent d'où ils viennent ; la géométrie lit le dessin ; aucun « \\r »"

# ------------------------------------------------------------------------ 2. 3. les tests

jouer() {
    local projet="$1" filtre="$2" attendus="$3" quoi="$4"
    if ! dotnet test "$projet" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
            --nologo -v q --filter "$filtre" > "$travail/tests.txt" 2>&1; then
        sed 's/^/    /' "$travail/tests.txt" >&2
        echo "ÉCHEC : les tests sous « $filtre » ($projet) ne passent pas." >&2
        exit 1
    fi
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
    if [ "${joues:-0}" -lt "$attendus" ]; then
        echo "ÉCHEC : $attendus tests attendus sous « $filtre », ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
    echo "  $quoi ($joues tests)"
}

jouer MyPublisher.Tests/MyPublisher.Tests.csproj "poste=N3" 66 \
    "2. le noyau : positions, plages, paragraphes, sortie inchangée, abscisse et position la plus proche"
jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj "poste=N3" 10 \
    "3. la vraie fenêtre : PointDe à l'ancre du dessin, IndexSous(PointDe(i)) == i, bloc tourné, bloc vide, champ"

# ------------------------------------------------------------------------ 4. l'empreinte

if [ -n "$reference" ]; then
    if ! Scripts/empreinte-composition.sh "$travail/empreinte" > "$travail/empreinte.txt" 2>&1; then
        sed 's/^/    /' "$travail/empreinte.txt" >&2
        echo "ÉCHEC : l'empreinte n'a pas pu être relevée." >&2
        exit 1
    fi
    if ! Scripts/empreinte-composition.sh --comparer "$reference" "$travail/empreinte" > "$travail/comparaison.txt" 2>&1; then
        sed 's/^/    /' "$travail/comparaison.txt" >&2
        exit 1
    fi
    echo "  4. l'empreinte : $(cat "$travail/comparaison.txt")"
else
    echo "  4. l'empreinte : non relevée (« --empreinte <référence> » la compare à un relevé d'avant)"
fi

echo "N3 : chaque morceau composé sait d'où il vient, et la vue passe d'une position à un point du bloc et retour."
