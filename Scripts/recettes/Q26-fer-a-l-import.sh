#!/usr/bin/env bash
#
# Recette du poste Q26 — l'import garde le fer des cases et des paragraphes à gauche. Deux défauts
# de PubImporter.cs, trouvés par R6 : une case dont le bloc de texte n'est pas ouvert dans la source
# recevait son texte sans le fer de son paragraphe — les 16 valeurs centrées des Fiches arrivaient
# au fer à gauche — ; et un paragraphe au fer à gauche sans autre réglage prenait le fer de son bloc —
# ATA 21 comptait 36 paragraphes justifiés à l'import pour 31 dans la source.
#
# Elle se joue sur les DEUX documents réels de MyPublisher/Books/, confrontés à ce que « pub2raw »
# en relève : c'est la source qui fait foi, et non un compte pris sur l'import. Quatre temps.
#
# 1. LES SOURCES : le format ne change pas (FormatVersion reste 5) ; un document déjà importé garde
#    ce qu'il a, il faut le réimporter.
#
# 2. LES DEUX DOCUMENTS, importés par le vrai binaire (« --resave ») et confrontés au relevé :
#      · ATA 21 : chaque paragraphe au fer de la source — 31 justifiés et 21 à gauche, autant que
#        la source. L'import rend encore le saut de ligne forcé par un paragraphe (T1 de l'étude
#        de 98) : un paragraphe de la source est lu au paragraphe du modèle où il commence ;
#      · les Fiches : chaque paragraphe de bloc au fer de la source, et chaque case au fer de son
#        premier paragraphe — les 16 cases centrées le sont.
#
# 3. LE PDF DES FICHES, mesuré par « pdftotext -bbox » : la première ligne de chacune des 16 cases
#    centrées a le milieu de son encre au milieu de sa case, à 0,05 pt.
#
# 4. LES TESTS, sous « poste=Q26 » :
#      · « MyPublisher.Tests » (FerALImportTests), sur des relevés écrits à la main : la case sans
#        bloc de texte prend le fer de son paragraphe, celle à plusieurs paragraphes le fer du
#        premier ; le paragraphe à gauche dans un bloc justifié garde son fer par un écart qui ne dit
#        que lui ; le fer du bloc est celui de la plupart de ses paragraphes ; un bloc sans fer
#        divergent ne reçoit aucun écart de plus ;
#      · « MyPublisher.Tests.Interface » (FerALImportDesDocumentsReelsTests) : les deux documents
#        confrontés au relevé, paragraphe par paragraphe et case par case ; puis l'aller-retour de R6
#        sur les Fiches — chaque bande prise d'un clic, l'écran centrant la valeur à 0,05 pt, le dock
#        montrant « Centré » sans rien écrire, le document intact, enregistré et relu.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# Le lecteur du format fermé, cherché comme la recette R5 le cherche. Sans lui, la recette ne peut
# rien affirmer : elle le dit et échoue.
outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$outil" ] || [ ! -x "$outil" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable (sudo apt install libmspub-tools, ou Scripts/outils-pub.sh)." >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$outil"
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

# ------------------------------------------------------------------------ 1. les sources

if ! grep -q "public const int CurrentFormatVersion = 5;" MyPublisher.Core/Models/Document.cs; then
    echo "ÉCHEC : le format a changé de version ; Q26 ne change pas le format (règle 15)." >&2
    exit 1
fi
echo "  1. les sources : FormatVersion reste 5"

# ------------------------------------------------------------------------ 2. les deux documents

ata="MyPublisher/Books/ATA 21 - Air conditioning.pub"
fiches="MyPublisher/Books/Fiches limitations et procédures d'urgences.pub"
"$outil" "$ata" > "$travail/ata21.txt"
"$outil" "$fiches" > "$travail/fiches.txt"
"$binaire" "$ata" --resave "$travail/ata21.mypub" > /dev/null
"$binaire" "$fiches" --resave "$travail/fiches.mypub" > /dev/null

cat > "$travail/fers.py" <<'PY'
import json, re

def source(chemin):
    """Les paragraphes de chaque bloc de texte, dans l'ordre, et ceux de chaque case, par
    (tableau, ligne, colonne) : pour chacun, son fer et les sauts de ligne forcés qu'il porte."""
    blocs, cases, courant, tableau = [], {}, None, -1
    for ligne in open(chemin, encoding="utf-8", errors="replace"):
        nu = ligne.strip()
        if nu.startswith("startTextObject"):
            courant = []
            blocs.append(courant)
        elif nu.startswith("startTableObject"):
            tableau += 1
        elif nu.startswith("openTableCell"):
            courant = []
            rang = lambda cle: int(re.search(cle + r": (\d+)", nu).group(1))
            cases[(tableau, rang("librevenge:row"), rang("librevenge:column"))] = courant
        elif nu.startswith("openParagraph") and courant is not None:
            fer = re.search(r"fo:text-align: (\w+)", nu)
            courant.append([fer.group(1) if fer else "left", 0])
        elif nu.startswith("insertLineBreak") and courant:
            courant[-1][1] += 1
    return blocs, cases

FERS = {"Left": "left", "Center": "center", "Right": "right", "Justify": "justify"}

def fers_du_bloc(bloc):
    """Le fer que la composition lit pour chaque paragraphe du modèle : l'écart qui le couvre, le
    dernier posé l'emportant, sinon le bloc."""
    fers = []
    for i in range(len(bloc.get("Text", "").split("\n"))):
        fer = bloc.get("Align", "Left")
        for ecart in bloc.get("Paragraphs", []):
            if ecart["Start"] <= i < ecart["Start"] + ecart["Length"] and ecart.get("Align"):
                fer = ecart["Align"]
        fers.append(FERS[fer])
    return fers

def importe(chemin):
    document = json.load(open(chemin, encoding="utf-8"))
    cadres = [(i, f) for i, page in enumerate(document["Pages"]) for f in page["Frames"]]
    return document, [f for _, f in cadres if f["$kind"] == "text"], [(i, f) for i, f in cadres if f["$kind"] == "table"]
PY

python3 - "$travail" <<'PY'
import collections, sys
sys.path.insert(0, sys.argv[1])
from fers import source, fers_du_bloc, importe, FERS

travail = sys.argv[1]
echecs = []

def confronter(nom, releve, mypub):
    blocs_source, cases_source = source(f"{travail}/{releve}")
    _, blocs, tableaux = importe(f"{travail}/{mypub}")
    if len(blocs) != len(blocs_source):
        echecs.append(f"{nom} : {len(blocs_source)} blocs de texte dans la source, {len(blocs)} à l'import")
        return None, None
    lus = collections.Counter()
    for k, (bloc, paragraphes) in enumerate(zip(blocs, blocs_source)):
        fers = fers_du_bloc(bloc)
        if len(fers) != sum(1 + sauts for _, sauts in paragraphes):
            echecs.append(f"{nom}, « {bloc['Name']} » : {len(fers)} paragraphes à l'import pour {len(paragraphes)} dans la source")
            continue
        rang = 0
        for p, (fer, sauts) in enumerate(paragraphes):
            if fers[rang] != fer:
                echecs.append(f"{nom}, « {bloc['Name']} », paragraphe {p} : {fer} dans la source, {fers[rang]} à l'import")
            lus[fers[rang]] += 1
            rang += 1 + sauts
    cases = collections.Counter()
    for (t, ligne, colonne), paragraphes in cases_source.items():
        tableau = tableaux[t][1]
        case = next(c for c in tableau["Cells"] if c["Row"] == ligne and c["Column"] == colonne)
        attendu = paragraphes[0][0] if paragraphes else "left"
        obtenu = FERS[case.get("Align", "Left")]
        if obtenu != attendu:
            echecs.append(f"{nom}, case « {case.get('Text', '')} » : {attendu} dans la source, {obtenu} à l'import")
        cases[obtenu] += 1
    return lus, cases

lus, _ = confronter("ATA 21", "ata21.txt", "ata21.mypub")
if lus is not None:
    if (lus["justify"], lus["left"]) != (31, 21):
        echecs.append(f"ATA 21 : {lus['justify']} paragraphes justifiés et {lus['left']} à gauche, 31 et 21 dans la source")
    print(f"    ATA 21 : {lus['justify']} paragraphes justifiés, {lus['left']} à gauche, lus au fer de la source")

lus, cases = confronter("Fiches", "fiches.txt", "fiches.mypub")
if lus is not None:
    if cases["center"] != 16:
        echecs.append(f"Fiches : {cases['center']} cases centrées, 16 dans la source")
    print(f"    Fiches : {sum(lus.values())} paragraphes de bloc et {sum(cases.values())} cases, "
          f"dont {cases['center']} centrées, lus au fer de la source")

if echecs:
    for message in echecs[:20]:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  2. les deux documents : chaque paragraphe et chaque case au fer que pub2raw relève"

# ------------------------------------------------------------------------ 3. le PDF des Fiches

"$binaire" "$travail/fiches.mypub" --export "$travail/fiches.pdf" > /dev/null 2>&1
pdftotext -bbox "$travail/fiches.pdf" "$travail/fiches.html" 2> /dev/null

python3 - "$travail" <<'PY'
import html, re, sys
sys.path.insert(0, sys.argv[1])
from fers import importe

travail = sys.argv[1]
_, _, tableaux = importe(f"{travail}/fiches.mypub")
pages = re.split(r"<page ", open(f"{travail}/fiches.html", encoding="utf-8").read())[1:]
mots = [(i, float(m[0]), float(m[1]), float(m[2]), float(m[3]), html.unescape(m[4]))
        for i, page in enumerate(pages)
        for m in re.findall(r'<word xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="([\d.]+)">(.*?)</word>', page)]

echecs, mesures = [], []
for i, tableau in tableaux:
    if tableau.get("Rotation", 0):
        echecs.append(f"« {tableau['Name']} » est tourné : la mesure ne le suit pas")
        continue
    for case in tableau["Cells"]:
        if case.get("Align") != "Center":
            continue
        gauche = tableau["X"] + sum(c["Width"] for c in tableau["Columns"][:case["Column"]])
        largeur = tableau["Columns"][case["Column"]]["Width"]
        haut = tableau["Y"] + sum(r["Height"] for r in tableau["Rows"][:case["Row"]])
        hauteur = tableau["Rows"][case["Row"]]["Height"]
        jetons = case["Text"].split()
        # Les mots de la case : dans son cadre, et pris à son texte — un bloc voisin qui la
        # chevauche n'y compte pas.
        dedans = [m for m in mots if m[0] == i and gauche <= m[1] and m[3] <= gauche + largeur
                  and haut - 2 <= m[2] and m[4] <= haut + hauteur + 2 and m[5] in jetons]
        if not dedans:
            echecs.append(f"« {case['Text']} » : aucun mot dans sa case au PDF")
            continue
        premiere = min(m[2] for m in dedans)
        ligne = sorted((m for m in dedans if abs(m[2] - premiere) < 0.5), key=lambda m: m[1])
        if [m[5] for m in ligne] != jetons[:len(ligne)]:
            echecs.append(f"« {case['Text']} » : la première ligne au PDF est « {' '.join(m[5] for m in ligne)} »")
            continue
        ecart = (ligne[0][1] + ligne[-1][3]) / 2 - (gauche + largeur / 2)
        mesures.append(ecart)
        if abs(ecart) > 0.05:
            echecs.append(f"« {case['Text']} » : le milieu de la ligne est à {ecart:+.4f} pt du milieu de la case")

if len(mesures) != 16:
    echecs.append(f"{len(mesures)} cases centrées mesurées au PDF, 16 attendues")
if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
print(f"    {len(mesures)} cases centrées au PDF, le milieu de la ligne à {max(abs(e) for e in mesures):.4f} pt au plus du milieu de la case")
PY
echo "  3. le PDF des Fiches : les 16 cases centrées, mesurées par pdftotext -bbox"

# ------------------------------------------------------------------------ 4. les tests

jouer() {   # $1 projet · $2 nombre minimal attendu · $3 libellé
    local projet="$1" attendus="$2" libelle="$3" sortie="$travail/tests-$(basename "$1" .csproj).txt"
    if ! dotnet test "$projet" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} --nologo --filter "poste=Q26" > "$sortie" 2>&1; then
        grep -E "Failed |Error Message|Assert" -A3 "$sortie" | sed 's/^/    /' >&2 || sed 's/^/    /' "$sortie" >&2
        echo "ÉCHEC : $libelle (« poste=Q26 ») ne passe pas." >&2
        exit 1
    fi
    local joues
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$sortie" | head -1)"
    if [ "${joues:-0}" -lt "$attendus" ]; then
        echo "ÉCHEC : $attendus tests attendus sous « poste=Q26 » dans $projet, ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
    echo "    $libelle : $joues tests"
}

jouer MyPublisher.Tests/MyPublisher.Tests.csproj 5 "l'import sur des relevés écrits à la main"
jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj 3 "les documents réels, et l'aller-retour des Fiches"
echo "  4. les tests : les cases, les paragraphes à gauche, les deux documents, l'aller-retour par l'interface"

echo "Q26 : l'import garde le fer des cases et des paragraphes à gauche — les 16 cases centrées des Fiches, les 31 paragraphes justifiés d'ATA 21, comme la source."
