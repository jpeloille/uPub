#!/usr/bin/env bash
#
# Recette du poste C4 — les champs.
#
# Deux moitiés, comme le poste.
#
# 1. LE FOLIO (C4a) — « un folio posé sur un modèle se numérote seul sur huit pages,
#    à l'écran et dans le PDF ». Un document de huit pages identiques, comme un modèle
#    qu'on aurait dupliqué, porte sur chaque page :
#
#      · un folio posé seul ;
#      · un second folio et le nombre de pages, dans un groupe — un groupe dessine ses
#        blocs par un autre chemin, et rien n'assure qu'il leur passe la page ;
#      · la date.
#
#    Au PDF, chaque page est relue seule par « pdftotext -f N -l N -bbox » et chaque
#    champ y est repéré à SA place : le folio seul dit N, celui du groupe dit N aussi,
#    le nombre de pages dit 8 — page par page, et non « on trouve 1 à 8 quelque part
#    dans le texte », ce qui passerait sur un folio qui suivrait l'ordre de lecture.
#    La date est fixée par « --date » et s'écrit en français même quand l'export
#    tourne sous une locale anglaise : le PDF est le livrable, il ne doit pas changer
#    de sens selon qui l'exporte.
#
#    À l'écran, le folio se calcule pour la page AFFICHÉE : on l'éprouve en changeant
#    la page courante de la fenêtre, ce qu'un script de shell ne fait pas. Les tests
#    « poste=C4 » de MyPublisher.Tests.Interface le font — par le modèle de vue, et non
#    par un geste de l'utilisateur — et relisent le contenu résolu que chaque bloc
#    reçoit pour la page courante, non les pixels dessinés. La recette les appelle et
#    compte ceux qui ont tourné.
#
# 2. LES COORDONNÉES (C4b) — « les coordonnées d'un jeu enregistré apparaissent dans
#    le PDF d'un modèle de B1 ». Le plan disait que cette moitié attendait les modèles ;
#    ils sont là. Un fichier de coordonnées porte TROIS jeux, et la carte de visite
#    s'exporte trois fois : le premier jeu actif, puis celui du milieu, puis le dernier.
#    Chaque fois, elle doit imprimer les six champs du jeu actif, aucun des douze
#    autres, et son logo — un aplat de couleur propre à chaque jeu (cyan, magenta,
#    jaune), qu'on relève au pixel au centre du bloc. Un code qui prendrait le premier
#    jeu, ou le dernier, au lieu de celui que nomme « ActiveSetName », tomberait sur
#    l'un des trois exports. (Avec deux jeux et l'actif en second, prendre le dernier
#    passait.)
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile plutôt que de lancer les tests avec « --no-build » : un binaire périmé
# survit à une modification des sources, et la recette se déclarerait verte sur du code
# qu'elle n'a pas éprouvé (cf. Scripts/recettes/A4-superposition.sh).
if ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

if [ ! -x "$binaire" ]; then
    echo "ÉCHEC : binaire introuvable après compilation ($binaire)." >&2
    exit 1
fi

# Un jeu vide : la première moitié ne lit pas les coordonnées, et l'export ne doit pas
# aller chercher celles de la machine — un fichier hors du dépôt.
echo '{ "ActiveSetName": "", "Sets": [] }' > "$travail/sans-coordonnees.json"

# ------------------------------------------------------------ 1. le folio, au PDF

# Les champs portent un texte saisi, « ? », qui ne doit JAMAIS paraître : il est sous
# le champ. Le voir dans le PDF voudrait dire que le champ n'a pas été résolu.
python3 - "$travail/huit-pages.mypub" <<'PY'
import json, sys

def champ(nom, champ, x, y, largeur=60):
    return {"$kind": "text", "Name": nom, "X": x, "Y": y, "Width": largeur, "Height": 24,
            "Text": "?", "FontFamily": "DejaVu Sans", "FontSize": 11, "LineHeight": 1.0,
            "Foreground": "#FF000000", "Field": champ}

def page(n):
    return {"Name": f"Page {n}", "Frames": [
        champ("Folio", "PageNumber", 480, 760),
        {"$kind": "group", "Name": "Pied", "X": 60, "Y": 760, "Width": 200, "Height": 24,
         "Children": [champ("Folio du groupe", "PageNumber", 0, 0),
                      champ("Nombre de pages", "PageCount", 100, 0)]},
        champ("Date", "Date", 300, 60, 200)]}

document = {"FormatVersion": 4, "Title": "Recette C4 — huit pages",
            "Pages": [page(n) for n in range(1, 9)]}
json.dump(document, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY

# Sous une locale anglaise, exprès : c'est là qu'une date suivant la culture de la
# machine écrirait « September ».
LC_ALL=en_US.UTF-8 LANG=en_US.UTF-8 "$binaire" "$travail/huit-pages.mypub" \
    --export "$travail/huit-pages.pdf" --date 2026-09-06 --contacts "$travail/sans-coordonnees.json" \
    > "$travail/export.txt" 2> "$travail/avertissements.txt"

if [ -s "$travail/avertissements.txt" ]; then
    echo "ÉCHEC : l'export du document de huit pages avertit." >&2
    cat "$travail/avertissements.txt" >&2
    exit 1
fi

for n in 1 2 3 4 5 6 7 8; do
    pdftotext -f "$n" -l "$n" -bbox "$travail/huit-pages.pdf" "$travail/page-$n.xml"
done

python3 - "$travail" <<'PY'
import re, sys

dossier = sys.argv[1]
# Où chaque champ est posé, en abscisse de page : le groupe est en X = 60, ses deux
# enfants à 0 et 100 dans son repère.
places = {"folio seul": (480, 540), "folio du groupe": (60, 120), "nombre de pages": (160, 220)}

echec = False
for n in range(1, 9):
    contenu = open(f"{dossier}/page-{n}.xml", encoding="utf-8").read()
    mots = [(float(x), float(y), texte) for x, y, texte in re.findall(
        r'<word xMin="([0-9.]+)" yMin="([0-9.]+)" xMax="[0-9.]+" yMax="[0-9.]+">([^<]*)</word>', contenu)]

    pied = {nom: [t for x, y, t in mots if a <= x < b and 755 <= y < 790] for nom, (a, b) in places.items()}
    attendu = {"folio seul": [str(n)], "folio du groupe": [str(n)], "nombre de pages": ["8"]}
    date = " ".join(t for x, y, t in mots if 55 <= y < 90)

    lu = f"folio {pied['folio seul']}, groupe {pied['folio du groupe']}, pages {pied['nombre de pages']}"
    print(f"  page {n} : {lu}, date « {date} »")
    if pied != attendu:
        print(f"ÉCHEC : page {n} — {lu} ; attendu {attendu}.", file=sys.stderr)
        echec = True
    if date != "6 septembre 2026":
        print(f"ÉCHEC : page {n} — la date se lit « {date} », et non « 6 septembre 2026 ».", file=sys.stderr)
        echec = True
    if any(t == "?" for _, _, t in mots):
        print(f"ÉCHEC : page {n} — le texte saisi sous un champ est imprimé.", file=sys.stderr)
        echec = True

sys.exit(1 if echec else 0)
PY

# --------------------------------------------------------- 1. le folio, à l'écran

attendus=2
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj \
        --nologo -v q --filter "poste=C4" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : le folio ne se numérote pas à l'écran." >&2
    exit 1
fi

joues="$(grep -o 'Passed: *[0-9]*' "$travail/tests.txt" | head -1 | grep -o '[0-9]*$' || true)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=C4 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi
echo "  à l'écran : $joues tests joués sur la fenêtre, contenu résolu page par page"

# --------------------------------------------- 2. les coordonnées, dans un modèle

modele="MyPublisher/Templates/carte-de-visite.mypub"

# Les logos : un aplat de huit pixels par jeu, d'une couleur qu'aucun autre élément de
# la carte ne porte. La couleur relevée dit de quel jeu vient le logo.
python3 - "$travail" <<'PY'
import struct, sys, zlib

def png(chemin, rvb):
    cote = 8
    brut = b"".join(b"\x00" + bytes(rvb) * cote for _ in range(cote))
    def bloc(tag, donnees):
        corps = tag + donnees
        return struct.pack(">I", len(donnees)) + corps + struct.pack(">I", zlib.crc32(corps) & 0xFFFFFFFF)
    entete = struct.pack(">IIBBBBB", cote, cote, 8, 2, 0, 0, 0)
    open(chemin, "wb").write(b"\x89PNG\r\n\x1a\n" + bloc(b"IHDR", entete)
                             + bloc(b"IDAT", zlib.compress(brut)) + bloc(b"IEND", b""))

for nom, rvb in (("cyan", (0, 255, 255)), ("magenta", (255, 0, 255)), ("jaune", (255, 255, 0))):
    png(f"{sys.argv[1]}/logo-{nom}.png", rvb)
PY

# Le logo, au centre de son bloc, lu dans le modèle lui-même plutôt qu'écrit ici : si
# le modèle déplace son logo, la recette le suit.
centre="$(python3 - "$modele" <<'PY'
import json, sys
document = json.load(open(sys.argv[1], encoding="utf-8"))
logo = next(f for f in document["Pages"][0]["Frames"] if f.get("Field") == "ContactLogo")
print(round(2 * (logo["X"] + logo["Width"] / 2)), round(2 * (logo["Y"] + logo["Height"] / 2)))
PY
)"
read -r cx cy <<< "$centre"

# Les trois jeux. Aucune valeur de l'un n'est contenue dans une valeur d'un autre : un
# intrus trouvé dans le texte vient bien de son jeu.
champs_siege=("Marcel Siegeot" "Directeur" "Ateliers du Siège" "1 rue du Siège" "01 00 00 00 00" "Le siège parle")
champs_antenne=("Jeanne Antenne" "Responsable" "Antenne Sud" "12 rue Lagarde" "687 26 26 26" "Au plus près")
champs_agence=("Paul Nordin" "Gérant" "Comptoir Boréal" "7 quai de l'Est" "687 47 47 47" "Cap au nord")

# $1 nom du jeu actif · $2 couleur de son logo · $3 nom du tableau de ses six champs
exporter_la_carte() {
    local actif="$1" teinte="$2"
    local -n champs_actifs="$3"

    # Le fichier tel que le dialogue des coordonnées l'écrit ; seul « ActiveSetName »
    # change d'un export à l'autre.
    cat > "$travail/coordonnees.json" <<JSON
{
  "ActiveSetName": "$actif",
  "Sets": [
    { "SetName": "Siège", "Name": "${champs_siege[0]}", "Title": "${champs_siege[1]}",
      "Organisation": "${champs_siege[2]}", "Address": "${champs_siege[3]}",
      "Phone": "${champs_siege[4]}", "Tagline": "${champs_siege[5]}", "LogoPath": "$travail/logo-cyan.png" },
    { "SetName": "Antenne de Nouméa", "Name": "${champs_antenne[0]}", "Title": "${champs_antenne[1]}",
      "Organisation": "${champs_antenne[2]}", "Address": "${champs_antenne[3]}",
      "Phone": "${champs_antenne[4]}", "Tagline": "${champs_antenne[5]}", "LogoPath": "$travail/logo-magenta.png" },
    { "SetName": "Agence du Nord", "Name": "${champs_agence[0]}", "Title": "${champs_agence[1]}",
      "Organisation": "${champs_agence[2]}", "Address": "${champs_agence[3]}",
      "Phone": "${champs_agence[4]}", "Tagline": "${champs_agence[5]}", "LogoPath": "$travail/logo-jaune.png" }
  ]
}
JSON

    "$binaire" "$modele" --export "$travail/carte.pdf" --date 2026-09-06 \
        --contacts "$travail/coordonnees.json" > /dev/null 2> "$travail/avertissements-carte.txt"

    if [ -s "$travail/avertissements-carte.txt" ]; then
        echo "ÉCHEC : jeu « $actif » actif, la carte de visite s'exporte avec des avertissements." >&2
        cat "$travail/avertissements-carte.txt" >&2
        exit 1
    fi

    pdftotext -layout "$travail/carte.pdf" "$travail/carte.txt"
    local texte
    texte="$(tr -s ' \n' '  ' < "$travail/carte.txt")"

    local valeur autre du_jeu
    for valeur in "${champs_actifs[@]}"; do
        if [[ "$texte" != *"$valeur"* ]]; then
            echo "ÉCHEC : jeu « $actif » actif, « $valeur », de ce jeu, n'est pas sur la carte de visite." >&2
            echo "    $texte" >&2
            exit 1
        fi
    done
    # Les intrus : les dix-huit valeurs, moins les six du jeu actif.
    for valeur in "${champs_siege[@]}" "${champs_antenne[@]}" "${champs_agence[@]}"; do
        du_jeu=0
        for autre in "${champs_actifs[@]}"; do [ "$autre" = "$valeur" ] && du_jeu=1; done
        if [ "$du_jeu" = 0 ] && [[ "$texte" == *"$valeur"* ]]; then
            echo "ÉCHEC : jeu « $actif » actif, « $valeur » vient d'un jeu qui n'est pas actif." >&2
            exit 1
        fi
    done

    # Deux pixels par point, comme partout ailleurs dans ce dépôt.
    pdftoppm -r 144 -f 1 -l 1 -png -singlefile "$travail/carte.pdf" "$travail/carte"
    local couleur r v b juste=0
    couleur="$(magick "$travail/carte.png" -format "%[fx:int(255*p{$cx,$cy}.r)] %[fx:int(255*p{$cx,$cy}.g)] %[fx:int(255*p{$cx,$cy}.b)]" info:)"
    read -r r v b <<< "$couleur"
    case "$teinte" in
        cyan)    [ "$r" -le 12 ] && [ "$v" -ge 243 ] && [ "$b" -ge 243 ] && juste=1 ;;
        magenta) [ "$r" -ge 243 ] && [ "$v" -le 12 ] && [ "$b" -ge 243 ] && juste=1 ;;
        jaune)   [ "$r" -ge 243 ] && [ "$v" -ge 243 ] && [ "$b" -le 12 ] && juste=1 ;;
    esac

    echo "  jeu « $actif » actif : ses six champs, aucun des deux autres jeux ; logo ($r, $v, $b) au centre de son bloc, attendu $teinte"
    if [ "$juste" = 0 ]; then
        echo "ÉCHEC : jeu « $actif » actif, le logo relevé en ($cx, $cy px) n'est pas le sien ($teinte attendu)." >&2
        exit 1
    fi
}

# Le premier, le milieu, le dernier : prendre le premier jeu tombe sur le deuxième
# export, prendre le dernier sur le premier.
exporter_la_carte "Siège" cyan champs_siege
exporter_la_carte "Antenne de Nouméa" magenta champs_antenne
exporter_la_carte "Agence du Nord" jaune champs_agence

echo "C4 : le folio se numérote seul sur huit pages, groupe compris ; les coordonnées remplissent un modèle."
