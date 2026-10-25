#!/usr/bin/env bash
#
# Recette du poste I8 — masquer un bloc, l'œil de « Blocs de la page ».
#
# Un bloc masqué n'est dessiné ni à l'écran ni à l'export ; on ne le saisit pas sur la
# planche, et il ne sert ni à l'aimantation ni à l'alignement. Il reste dans la liste,
# l'œil fermé, et s'y sélectionne. Le basculer s'annule et rend le document « modifié » :
# c'est une propriété du document, « Visible », vraie par défaut, que les cases d'un
# tableau ne voient pas. Les quatre clauses du plan, chacune jouée :
#
#   1. « "Visible": false » fait l'aller-retour JSON, au bloc et dans un groupe : le
#      document de la recette, relu puis réécrit, ne change pas d'un caractère. Une recopie
#      et un groupe la gardent (tests du noyau). Chaque bloc de Samples/ et de Templates/,
#      réenregistrés par le poste, porte la clé, à « true » ; un document sans la clé
#      s'ouvre visible ;
#   2. un bloc masqué n'apparaît pas au PDF : ni son texte (« pdftotext »), ni ses tracés —
#      le PDF du document qui porte trois blocs masqués compte exactement les tracés et les
#      textes de celui qui ne les porte pas, et la contre-épreuve, les trois visibles, en
#      compte davantage. Ni dans PageRoot, et un clic à sa place prend le bloc d'en
#      dessous : tests d'interface, sur la vraie fenêtre ;
#   3. l'export n'avertit pas des polices d'un bloc masqué (Q8 : seules les polices
#      dessinées) ; visible, le même bloc fait nommer sa police ;
#   4. un pas d'annulation rouvre l'œil : test d'interface, au clic sur l'œil puis au
#      clavier. Les tests « poste=I8 » des deux projets sont comptés.
#
# Ce qu'elle ne prouve PAS :
#   - l'aimantation à un bloc masqué : l'aimantation aux autres blocs n'existe pas encore
#     (A11), et un bloc masqué ne se tire pas ; quand A11 viendra, sa recette dira qu'un
#     bloc masqué n'attire rien ;
#   - l'import d'un bloc masqué d'un .pub : libmspub n'en transmet aucun, et les deux
#     documents ATR n'en portent pas ; le test vérifie qu'ATA 21 s'importe tout visible.
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

if ! command -v pdftotext > /dev/null; then
    echo "ÉCHEC : « pdftotext » est introuvable (poppler-utils)." >&2
    exit 1
fi

# $1 : le fichier à écrire · $2 : « masques », « visibles » ou « absents » — les trois
# blocs du poste masqués, visibles, ou retirés du document.
ecrire() {
    python3 - "$1" "$2" <<'PY'
import json, sys

chemin, cas = sys.argv[1], sys.argv[2]
visible = cas == "visibles"

def texte(nom, mot, x, y, police="DejaVu Sans", **autres):
    return {"$kind": "text", "Name": nom, "X": x, "Y": y, "Width": 300, "Height": 40,
            "Text": mot, "FontFamily": police, "FontSize": 14, **autres}

def forme(nom, x, y, couleur, **autres):
    return {"$kind": "shape", "Name": nom, "X": x, "Y": y, "Width": 120, "Height": 80,
            "Fill": {"$kind": "solid", "Color": couleur}, "Stroke": "#FF000000",
            "StrokeThickness": 2, **autres}

blocs = [
    forme("Fond", 40, 40, "#FF9DC3E6"),
    texte("Apparent", "Apparent", 60, 60),
]
if cas != "absents":
    # Le texte masqué est dans une police absente : la clause 3 le lit au même export.
    blocs.append(texte("Escamote", "Escamote", 60, 200, police="Police Inexistante", Visible=visible))
    blocs.append(forme("Forme", 300, 200, "#FFCC0000", Visible=visible))
    # Un groupe masqué emporte ce qu'il porte, visible ou non.
    blocs.append({"$kind": "group", "Name": "Groupe", "X": 60, "Y": 400, "Width": 300, "Height": 80,
                  "Visible": visible,
                  "Children": [texte("Enfoui", "Enfoui", 0, 0), forme("Dedans", 160, 0, "#FF00AA00")]})

document = {"FormatVersion": 5, "Title": "Recette I8", "PageWidth": 595.28, "PageHeight": 841.89,
            "Pages": [{"Name": "Page 1", "Frames": blocs}]}
json.dump(document, open(chemin, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
}

exporter() {
    if ! "$binaire" "$1" --export "$2" --date 2026-09-06 > /dev/null 2> "$3"; then
        echo "ÉCHEC : export impossible ($1)." >&2
        cat "$3" >&2
        exit 1
    fi
}

# Les opérateurs de dessin du flux de la première page : les tracés peints (f, f*, F, S,
# s, B, B*, b, b*), les rectangles posés (re) et les textes montrés (Tj, TJ, ', ").
# PDFsharp comprime ses flux : on les décompresse, et l'on retire les chaînes avant de
# compter, pour qu'un glyphe ne passe pas pour un opérateur.
operateurs() {
    python3 - "$1" <<'PY'
import collections, re, sys, zlib

octets = open(sys.argv[1], "rb").read()
compte = collections.Counter()
# Les flux de page seuls, ceux que désigne « /Contents » : une police embarquée est aussi
# un flux comprimé, et ses octets passeraient pour des opérateurs.
pages = set(re.findall(rb"/Contents\s+(\d+)\s+0\s+R", octets))
if not pages:
    sys.exit("aucun flux de page (/Contents) dans " + sys.argv[1])
for numero in pages:
    m = re.search(rb"(?<!\d)" + numero + rb"\s+0\s+obj.*?stream\r?\n", octets, re.S)
    fin = octets.index(b"endstream", m.end())
    flux = zlib.decompressobj().decompress(octets[m.end():fin])
    flux = re.sub(rb"\((?:\\.|[^\\)])*\)|<[0-9A-Fa-f\s]*>", b" ", flux)
    for jeton in re.split(rb"[\s\[\]]+", flux):
        if jeton in (b"f", b"f*", b"F", b"S", b"s", b"B", b"B*", b"b", b"b*", b"re", b"Tj", b"TJ", b"'", b'"'):
            compte[jeton.decode()] += 1
print(" ".join(f"{k}={compte[k]}" for k in sorted(compte)))
PY
}

# ------------------------------------------------------------------------- 1

ecrire "$travail/masques.mypub" masques
if ! "$binaire" "$travail/masques.mypub" --resave "$travail/relu.mypub" > /dev/null 2> "$travail/relu.txt"; then
    echo "ÉCHEC : le document de la recette ne se relit pas." >&2
    cat "$travail/relu.txt" >&2
    exit 1
fi
"$binaire" "$travail/relu.mypub" --resave "$travail/relu-bis.mypub" > /dev/null
if ! diff -u "$travail/relu.mypub" "$travail/relu-bis.mypub" > "$travail/ecart.diff"; then
    echo "ÉCHEC : relu puis réécrit, le document masqué n'est plus le même :" >&2
    head -30 "$travail/ecart.diff" >&2
    exit 1
fi
faux="$(grep -c '"Visible": false' "$travail/relu.mypub" || true)"
if [ "$faux" != 3 ]; then
    echo "ÉCHEC : trois blocs masqués écrits, $faux relus masqués (\"Visible\": false)." >&2
    exit 1
fi

# Samples/ et Templates/, réenregistrés par le poste : chaque bloc porte la clé, à true.
nombre="$(python3 - Samples/*.mypub MyPublisher/Templates/*.mypub <<'PY'
import json, sys

fautes, blocs = [], 0
def parcourir(chemin, liste):
    global blocs
    for bloc in liste:
        blocs += 1
        if bloc.get("Visible") is not True:
            fautes.append(f"{chemin} : « {bloc.get('Name', '')} » porte Visible = {bloc.get('Visible', '(absent)')}")
        parcourir(chemin, bloc.get("Children", []))

for chemin in sys.argv[1:]:
    document = json.load(open(chemin, encoding="utf-8"))
    for page in document["Pages"]:
        parcourir(chemin, page["Frames"])
if fautes or blocs == 0:
    print("ÉCHEC : Samples et Templates devaient être réenregistrés, chaque bloc visible :", file=sys.stderr)
    for f in fautes[:20]:
        print("    " + f, file=sys.stderr)
    sys.exit(1)
print(f"{blocs}")
PY
)"
echo "  1. « \"Visible\": false » fait l'aller-retour, au bloc et dans un groupe ; les $nombre blocs de Samples et de Templates sont écrits visibles."

# ------------------------------------------------------------------------- 2 et 3

ecrire "$travail/visibles.mypub" visibles
ecrire "$travail/absents.mypub" absents
exporter "$travail/masques.mypub" "$travail/masques.pdf" "$travail/masques.txt"
exporter "$travail/visibles.mypub" "$travail/visibles.pdf" "$travail/visibles.txt"
exporter "$travail/absents.mypub" "$travail/absents.pdf" "$travail/absents.txt"

pdftotext "$travail/masques.pdf" "$travail/masques-texte.txt"
if ! grep -q "Apparent" "$travail/masques-texte.txt"; then
    echo "ÉCHEC : le bloc visible « Apparent » manque au PDF." >&2
    exit 1
fi
for mot in Escamote Enfoui; do
    if grep -q "$mot" "$travail/masques-texte.txt"; then
        echo "ÉCHEC : « $mot », masqué, est dans le PDF." >&2
        exit 1
    fi
done
pdftotext "$travail/visibles.pdf" "$travail/visibles-texte.txt"
for mot in Escamote Enfoui; do
    if ! grep -q "$mot" "$travail/visibles-texte.txt"; then
        echo "ÉCHEC : la contre-épreuve devait écrire « $mot » au PDF ; pdftotext ne l'y trouve pas." >&2
        exit 1
    fi
done

masques="$(operateurs "$travail/masques.pdf")"
absents="$(operateurs "$travail/absents.pdf")"
visibles="$(operateurs "$travail/visibles.pdf")"
if [ -z "$absents" ] || [ "$masques" != "$absents" ]; then
    echo "ÉCHEC : masqués, les trois blocs laissent des tracés au PDF :" >&2
    echo "    sans eux : ${absents:-(rien)}" >&2
    echo "    masqués  : $masques" >&2
    exit 1
fi
if [ "$visibles" = "$absents" ]; then
    echo "ÉCHEC : la contre-épreuve, les trois blocs visibles, compte les mêmes tracés ($visibles) :" >&2
    echo "le décompte ne voit rien." >&2
    exit 1
fi
echo "  2. le PDF n'a ni le texte ni les tracés des blocs masqués : $masques, comme sans eux ; visibles : $visibles."

if grep -q "Police" "$travail/masques.txt"; then
    echo "ÉCHEC : l'export avertit des polices d'un bloc masqué :" >&2
    grep "Police" "$travail/masques.txt" | sed 's/^/    /' >&2
    exit 1
fi
if ! grep -qF "« Police Inexistante »" "$travail/visibles.txt"; then
    echo "ÉCHEC : visible, le bloc en « Police Inexistante » devait la faire nommer ; avertissements :" >&2
    sed 's/^/    /' "$travail/visibles.txt" >&2
    exit 1
fi
echo "  3. l'export ne nomme pas la police d'un bloc masqué ; visible, il la nomme."

# ------------------------------------------------------------------------- 4 et les tests

# $1 projet · $2 nombre attendu · $3 libellé
tests() {
    if ! dotnet test "$1" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} --nologo -v q --filter "poste=I8" \
            > "$travail/tests.txt" 2>&1; then
        grep -E "\[FAIL\]|Assert" "$travail/tests.txt" | sed 's/^/    /' >&2 || true
        tail -30 "$travail/tests.txt" | sed 's/^/    /' >&2
        echo "ÉCHEC : les tests $3 sous « poste=I8 » ne passent pas." >&2
        exit 1
    fi
    local joues
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
    if [ "${joues:-0}" -lt "$2" ]; then
        echo "ÉCHEC : $2 tests $3 attendus sous « poste=I8 », ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
    echo "$joues"
}

noyau="$(tests MyPublisher.Tests/MyPublisher.Tests.csproj 14 "du noyau")"
interface="$(tests MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj 11 "d'interface")"
echo "  4. l'œil se ferme en un pas, que l'annulation défait ; PageRoot, clic, liste, alignement, groupe, vignette ($noyau tests du noyau, $interface d'interface)."

echo "I8 : un bloc masqué reste dans le document et dans la liste, et nulle part ailleurs."
