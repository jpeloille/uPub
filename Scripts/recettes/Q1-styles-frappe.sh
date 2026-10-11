#!/usr/bin/env bash
#
# Recette du poste Q1 — les styles suivent la frappe.
#
# Le défaut abîmait des DONNÉES : taper un mot en tête d'un bloc importé faisait
# glisser tous ses gras d'autant de caractères, et l'enregistrement fixait le décalage.
# La recette se joue donc là où il a été trouvé — ATA 21, importé par pub2raw — et
# par le geste qui le déclenchait : une frappe au clavier dans la vraie fenêtre.
#
# Ce qu'elle éprouve :
#
#   1. la frappe — « dotnet test » joue les tests d'interface du poste : un mot tapé
#      en tête du premier bloc riche, un saut de ligne, l'annulation, le champ du
#      panneau, qui suit aussi la frappe sur la page, et celui d'une case de tableau.
#      Le premier écrit le document tapé là où la variable d'environnement le
#      demande : c'est lui qu'on mesure ensuite ;
#   2. le document tapé, exporté, rend en gras dans le flux du PDF EXACTEMENT les mêmes
#      mots qu'avant la frappe, au même corps — le mot tapé en plus, en tête, au style
#      du titre qui le suit (en tête de texte, la frappe prend le style du caractère
#      qui suit, comme dans Publisher) ;
#   3. rien d'autre de la page n'a bougé : chaque ligne hors du bloc tapé est restée
#      ce qu'elle était ;
#   4. contre-épreuve — le même document tapé, mais aux fragments laissés à leurs
#      positions d'avant (ce que faisait le dépôt jusqu'ici), ÉCHOUE au point 2.
#
# Le point 2 lit le PDF et non le fichier : un recalage juste dans le .mypub et perdu à
# l'export ne prouverait rien. Le gras se lit au nom de la police que le flux emploie,
# tel que « pdftohtml -xml » le relève — « Noto Sans,Bold » —, et non à l'œil.
#
# Le point 4 est ce qui donne son poids au point 2 : sans lui, la comparaison pourrait
# passer parce qu'elle ne sait pas voir un gras déplacé. Elle le voit — on le lui montre.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
source_pub="MyPublisher/Books/ATA 21 - Air conditioning.pub"

# Le même lecteur que la recette R5, cherché de la même façon : sans lui, rien ne
# s'affirme, et la recette le dit plutôt que de se déclarer verte.
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

# ------------------------------------------------------------------------------- 1

# Le fichier est retiré d'abord : « dotnet test » sort en 0 quand AUCUN test ne
# correspond au filtre, et un document resté d'un passage précédent ferait passer la
# suite sans que rien n'ait été tapé.
tape="$travail/tape.mypub"
rm -f "$tape"
if ! MYPUBLISHER_RECETTE_FRAPPE="$tape" \
     dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=Q1" > "$travail/tests.txt" 2>&1; then
    echo "ÉCHEC : les tests de frappe ne passent pas." >&2
    sed 's/^/    /' "$travail/tests.txt" >&2
    exit 1
fi

joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ -z "$joues" ] || [ "$joues" -lt 6 ] || [ ! -f "$tape" ]; then
    echo "ÉCHEC : ${joues:-0} test(s) de frappe joué(s), six attendus, ou aucun document tapé." >&2
    sed 's/^/    /' "$travail/tests.txt" >&2
    exit 1
fi

echo "  1. la frappe : $joues tests d'interface joués, le document tapé est écrit."

# ------------------------------------------------------------------------------- 2 · 3

# Le document d'avant est l'import lui-même, réécrit ; celui d'après sort du test.
avant="$travail/avant.mypub"
"$binaire" "$source_pub" --resave "$avant" > /dev/null

exporter() {
    "$binaire" "$1" --export "$2" > /dev/null 2>&1
    pdftohtml -xml -i -q -zoom 1 -f 1 -l 1 -stdout "$2" > "${2%.pdf}.xml"
}

exporter "$avant" "$travail/avant.pdf"
exporter "$tape" "$travail/tape.pdf"

# La comparaison, écrite une fois et jouée deux fois : sur le document tapé, où elle
# doit passer, puis sur sa contrefaçon, où elle doit échouer.
cat > "$travail/comparer.py" <<'PY'
import json, re, sys
import xml.etree.ElementTree as ET

avant_mypub, avant_xml, apres_xml, tape = sys.argv[1:5]

# Le bloc mesuré : le premier bloc de texte de la page qui porte des fragments,
# celui que le test d'interface a tapé.
document = json.load(open(avant_mypub, encoding="utf-8"))
bloc = next(b for b in document["Pages"][0]["Frames"] if b["$kind"] == "text" and b.get("Spans"))
x, y, l, h = bloc["X"], bloc["Y"], bloc["Width"], bloc["Height"]

def lire(chemin):
    racine = ET.parse(chemin).getroot()
    page = racine.find("page")
    # Le préfixe de sous-ensemble (« FTADUK+ ») change à chaque export : seul le nom
    # de la police dit quelque chose.
    polices = {f.get("id"): (f.get("family").split("+", 1)[-1], float(f.get("size")))
               for f in page.iter("fontspec")}
    dedans, dehors = [], []
    for t in page.iter("text"):
        texte = "".join(t.itertext())
        haut, gauche = float(t.get("top")), float(t.get("left"))
        famille, corps = polices[t.get("font")]
        gras = "Bold" in famille
        if y - 2 <= haut <= y + h and x - 2 <= gauche <= x + l:
            dedans += [(mot, gras, corps) for mot in texte.split()]
        else:
            dehors.append((haut, gauche, texte, famille, corps))
    return dedans, dehors

mots_avant, reste_avant = lire(avant_xml)
mots_apres, reste_apres = lire(apres_xml)

echec = False
gras_avant = [m for m, g, _ in mots_avant if g]
gras_apres = [m for m, g, _ in mots_apres if g]
print(f"    gras avant la frappe : {' '.join(gras_avant)}")
print(f"    gras après la frappe : {' '.join(gras_apres)}")

# Le mot tapé en tête prend le style du caractère qui le suit — le premier mot d'avant,
# un titre en gras, plus grand que le bloc : son gras et son corps.
_, gras_titre, corps_titre = mots_avant[0] if mots_avant else ("", False, 0)
if [(m, g) for m, g, _ in mots_apres[:len(tape.split())]] != [(m, gras_titre) for m in tape.split()]:
    print(f"    ÉCHEC : le mot tapé n'est pas en tête, au style du titre qui suit : {mots_apres[:3]}", file=sys.stderr)
    echec = True
elif any(c != corps_titre for _, _, c in mots_apres[:len(tape.split())]):
    print(f"    ÉCHEC : le mot tapé n'est pas au corps du titre ({corps_titre}) : {mots_apres[:3]}", file=sys.stderr)
    echec = True
if not gras_titre or corps_titre == round(bloc["FontSize"]):
    print(f"    ÉCHEC : le premier mot d'avant n'est pas un titre gras d'un autre corps que le bloc : {mots_avant[:1]}", file=sys.stderr)
    echec = True

if mots_apres[len(tape.split()):] != mots_avant:
    for i, (a, b) in enumerate(zip(mots_avant, mots_apres[len(tape.split()):])):
        if a != b:
            print(f"    ÉCHEC : mot {i} — avant {a}, après {b}", file=sys.stderr)
            break
    else:
        print(f"    ÉCHEC : {len(mots_avant)} mots avant, {len(mots_apres) - len(tape.split())} après", file=sys.stderr)
    echec = True

if not gras_avant:
    print("    ÉCHEC : aucun mot gras dans le bloc d'avant — la mesure ne mesure rien.", file=sys.stderr)
    echec = True

if reste_avant != reste_apres:
    print("    ÉCHEC : la page a changé hors du bloc tapé.", file=sys.stderr)
    echec = True

sys.exit(1 if echec else 0)
PY

mot="$(python3 - "$tape" "$avant" <<'PY'
import json, sys
tape = json.load(open(sys.argv[1], encoding="utf-8"))
avant = json.load(open(sys.argv[2], encoding="utf-8"))
# Le même rang dans la pile : la frappe ne déplace aucun bloc.
i, a = next((i, b) for i, b in enumerate(avant["Pages"][0]["Frames"])
            if b["$kind"] == "text" and b.get("Spans"))
t = tape["Pages"][0]["Frames"][i]
assert t["Text"].endswith(a["Text"]), "le texte tapé ne finit pas par le texte d'avant"
print(t["Text"][: len(t["Text"]) - len(a["Text"])])
PY
)"

if ! python3 "$travail/comparer.py" "$avant" "$travail/avant.xml" "$travail/tape.xml" "$mot"; then
    echo "ÉCHEC : après la frappe, le PDF ne rend pas les mêmes mots en gras." >&2
    exit 1
fi

echo "  2. le PDF rend en gras les mêmes mots qu'avant la frappe, au même corps ; le mot tapé, au style du titre qui suit."
echo "  3. hors du bloc tapé, la page est restée la même."

# ------------------------------------------------------------------------------- 4

# La contrefaçon : le texte tapé, mais les fragments et les écarts d'avant, à leurs
# positions d'avant. C'est exactement ce que le dépôt écrivait avant ce poste.
python3 - "$tape" "$avant" "$travail/decale.mypub" <<'PY'
import json, sys
tape = json.load(open(sys.argv[1], encoding="utf-8"))
avant = json.load(open(sys.argv[2], encoding="utf-8"))
# Le même rang dans la pile : la frappe ne déplace aucun bloc.
i, a = next((i, b) for i, b in enumerate(avant["Pages"][0]["Frames"])
            if b["$kind"] == "text" and b.get("Spans"))
t = tape["Pages"][0]["Frames"][i]
t["Spans"], t["Paragraphs"] = a["Spans"], a["Paragraphs"]
json.dump(tape, open(sys.argv[3], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY

exporter "$travail/decale.mypub" "$travail/decale.pdf"
if python3 "$travail/comparer.py" "$avant" "$travail/avant.xml" "$travail/decale.xml" "$mot" \
        > "$travail/contre.txt" 2>&1; then
    echo "ÉCHEC : la comparaison ne voit pas un gras laissé à sa position d'avant." >&2
    sed 's/^/    /' "$travail/contre.txt" >&2
    exit 1
fi

echo "  4. contre-épreuve : les fragments laissés en place, la même comparaison échoue."

echo "Q1 : taper dans un bloc importé garde ses gras sur leurs mots, jusque dans le PDF."
