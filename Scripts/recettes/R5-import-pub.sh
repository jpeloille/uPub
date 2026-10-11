#!/usr/bin/env bash
#
# Recette du poste R5 — ouvrir un .pub.
#
# Elle ne se joue pas sur un document fabriqué pour elle mais sur les DEUX documents
# réels de MyPublisher/Books/ : ATA 21 et les fiches de limitations et procédures
# d'urgence. C'est la leçon de la relecture de R2 — « un poste dont la recette évite
# son propre défaut connu n'est pas mesuré, il est mis en scène ».
#
# Ce qu'elle éprouve, pour chacun :
#
#   1. le .pub s'ouvre et se réécrit en .mypub ;
#   2. ce qui a été restitué correspond, objet pour objet, à ce que le relevé de la
#      source contient — pages, blocs de texte, tableaux, cellules, formes, fragments
#      de style ;
#   3. le document importé s'exporte, avec le bon nombre de pages ;
#   4. son texte se retrouve dans le PDF.
#
# Le point 2 est le seul qui vaille : il compare l'import à la SOURCE et non à un
# relevé de référence qu'on aurait pris sur l'import lui-même. Un import qui perdrait
# la moitié d'un document passerait un contrôle de non-régression sans broncher.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"

# MyPublisher délègue la lecture du format fermé à libmspub, comme il délègue les
# polices à fontconfig. Sans l'outil, la recette ne peut rien affirmer : elle le dit
# et échoue, plutôt que de se déclarer verte sur un contrôle qu'elle n'a pas fait.
outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$outil" ] || [ ! -x "$outil" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable." >&2
    echo "MyPublisher délègue la lecture du .pub à libmspub :" >&2
    echo "    sudo apt install libmspub-tools" >&2
    echo "ou, sans droits d'installation : Scripts/outils-pub.sh" >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$outil"

# Les bibliothèques extraites localement ne sont pas dans le chemin du système.
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

controler() {
    local source="$1" pages="$2"

    local nom
    nom="$(basename "$source" .pub)"

    "$outil" "$source" > "$travail/releve.txt"
    "$binaire" "$source" --resave "$travail/$nom.mypub" > /dev/null

    # Le compte de la source, tiré du relevé, et celui de l'import, tiré du .mypub.
    python3 - "$travail/releve.txt" "$travail/$nom.mypub" "$nom" <<'PY'
import collections, json, sys

releve, mypub, nom = sys.argv[1], sys.argv[2], sys.argv[3]

source = collections.Counter(
    l.strip().split("(")[0].strip()
    for l in open(releve, encoding="utf-8", errors="replace"))

document = json.load(open(mypub, encoding="utf-8"))
compte = collections.Counter()
compte["page"] = len(document["Pages"])

def parcours(bloc):
    compte[bloc["$kind"]] += 1
    if bloc["$kind"] == "text":
        compte["fragment"] += len(bloc.get("Spans", []))
        compte["caractere"] += len(bloc.get("Text", ""))
    for enfant in bloc.get("Children", []):
        parcours(enfant)
    for case in bloc.get("Cells", []):
        compte["cellule"] += 1
        compte["caractere"] += len(case.get("Text", ""))

for page in document["Pages"]:
    for bloc in page["Frames"]:
        parcours(bloc)

# Les styles, et non les fragments. L'import remonte le style le plus répandu au bloc
# et supprime les fragments qui n'en disent alors plus rien : compter les fragments
# reprocherait à l'import d'avoir bien fait son travail. Ce qui doit se conserver, ce
# sont les styles RÉELLEMENT employés — perdre un gras ou une police, voilà une perte.
import re

def styles_source(chemin):
    styles, courant = set(), None
    for ligne in open(chemin, encoding="utf-8", errors="replace"):
        nu = ligne.strip()
        if nu.startswith("openSpan"):
            args = dict(re.findall(r"([a-z:-]+): ([^,)]+)", nu))
            courant = (
                args.get("style:font-name", ""),
                round(float(args.get("fo:font-size", "0in").rstrip("in")) * 72, 1),
                args.get("fo:font-weight", "") == "bold",
                args.get("fo:font-style", "") == "italic",
                (args.get("fo:color", "") or "").lower(),
            )
        elif nu.startswith("insertText (") and courant is not None:
            # Un fragment sans caractère ne recouvre rien : il n'a pas de style à perdre.
            if len(nu[len("insertText ("):-1]) > 0:
                styles.add(courant)
        elif nu.startswith("closeSpan"):
            courant = None
    return styles

def styles_importes(document):
    styles = set()
    def parcours(bloc):
        if bloc["$kind"] == "text":
            commun = (bloc["FontFamily"], round(bloc["FontSize"], 1), bloc["Bold"],
                      bloc["Italic"], "#" + bloc["Foreground"][3:].lower())
            styles.add(commun)
            for f in bloc.get("Spans", []):
                styles.add((
                    f["FontFamily"] or commun[0],
                    round(f["FontSize"], 1) if f["FontSize"] is not None else commun[1],
                    commun[2] if f["Bold"] is None else f["Bold"],
                    commun[3] if f["Italic"] is None else f["Italic"],
                    ("#" + f["Foreground"][3:].lower()) if f["Foreground"] else commun[4],
                ))
        for enfant in bloc.get("Children", []):
            parcours(enfant)
    for page in document["Pages"]:
        for bloc in page["Frames"]:
            parcours(bloc)
    return styles

manquants = styles_source(releve) - styles_importes(document)

attendu = {
    "pages":              (source["startPage"],        compte["page"]),
    "blocs de texte":     (source["startTextObject"],  compte["text"]),
    "tableaux":           (source["startTableObject"], compte["table"]),
    "cellules":           (source["openTableCell"],    compte["cellule"]),
    "formes":             (source["drawPolygon"] + source["drawPath"], compte["shape"]),
    "styles perdus":      (0, len(manquants)),
}

echec = False
for libelle, (attendus, obtenus) in attendu.items():
    marque = "" if attendus == obtenus else "   <- ÉCART"
    if attendus != obtenus:
        echec = True
    print(f"    {libelle:20} source {attendus:>4}   importé {obtenus:>4}{marque}")

for style in sorted(manquants)[:5]:
    print(f"    style perdu : {style}", file=sys.stderr)

if compte["caractere"] == 0:
    print("    aucun caractère importé", file=sys.stderr)
    echec = True

print(f"    {'caractères':20}                 importé {compte['caractere']:>4}")
sys.exit(1 if echec else 0)
PY

    # L'export, et le bon nombre de pages.
    "$binaire" "$travail/$nom.mypub" --export "$travail/$nom.pdf" > "$travail/export.txt"
    local obtenues
    obtenues="$(sed -n 's/^\([0-9][0-9]*\) page(s).*/\1/p' "$travail/export.txt")"
    if [ "$obtenues" != "$pages" ]; then
        echo "ÉCHEC : $nom exporté en $obtenues page(s), $pages attendue(s)." >&2
        exit 1
    fi

    # Le texte est bien passé jusqu'au PDF.
    pdftotext "$travail/$nom.pdf" "$travail/$nom.txt"
    if [ "$(wc -c < "$travail/$nom.txt")" -lt 1000 ]; then
        echo "ÉCHEC : le PDF de $nom ne porte presque aucun texte." >&2
        exit 1
    fi
}

echo "  ATA 21 :"
controler "MyPublisher/Books/ATA 21 - Air conditioning.pub" 2

echo "  Fiches limitations et procédures d'urgence :"
controler "MyPublisher/Books/Fiches limitations et procédures d'urgences.pub" 5

echo "R5 : les deux documents Publisher s'ouvrent, objet pour objet, et s'exportent."
