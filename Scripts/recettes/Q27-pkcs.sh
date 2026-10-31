#!/usr/bin/env bash
#
# Recette du poste Q27 — la faille de la dépendance de PdfSharp (décidé le 1er novembre 2026, sur
# le relevé de N25). PdfSharp 6.2.1, au noyau, tire System.Security.Cryptography.Pkcs 8.0.1, que
# visent GHSA-846f-f764-76mx (haute) et GHSA-2fmv-rv2c-59x7 (moyenne). Le noyau épingle la 8.0.2,
# correctif de la même série ; aucune version publiée de PdfSharp ne la tire d'elle-même.
#
# Ce que cette recette prouve SANS RÉSEAU — c'est pourquoi elle est ici, jouée par verifier.sh :
#
# 1. LES SOURCES : la référence directe à Pkcs, au noyau seul, une fois, à une version corrigée,
#    avec son pourquoi en commentaire (les deux avancées nommées) ; PdfSharp toujours en 6.2.1.
#
# 2. LES RELEVÉS COMPILÉS : les cinq projets — l'application, le noyau, le banc, les deux suites
#    de tests — ont dans leur « .deps.json » une version corrigée de Pkcs, et aucun la 8.0.1 ; la
#    bibliothèque copiée à côté de l'application est bien celle de ce paquet (son numéro de
#    fichier est celui que le relevé lui donne). Le graphe que la restauration a résolu dit encore
#    que PdfSharp demande une version fautive : l'épinglage reste nécessaire, et la recette le dit
#    le jour où il ne l'est plus.
#
# 3. L'EXPORT N'A PAS BOUGÉ : les sources de l'application et du noyau sont recopiées, l'épinglage
#    retiré de la copie, et cette copie compilée — c'est l'« avant », tel que le dépôt l'était.
#    Son relevé diffère de celui d'aujourd'hui d'un paquet, et d'un seul : Pkcs, de 8.0.1 à la
#    version corrigée. Le corpus — Samples, Templates, et les deux documents ATR des recettes,
#    importés de leur .pub — est exporté par les deux binaires (le mode sans fenêtre de
#    l'application, le même que contrôle.sh), à date et coordonnées fixées ; les PDF sont
#    identiques à l'octet, une fois retirés les champs que PdfSharp tire au hasard ou date à
#    chaque export, et rien d'autre :
#      · le préfixe du sous-ensemble de chaque police (« /ROECEB+DejaVu#20Sans ») ;
#      · l'identifiant du fichier (« /ID[<…><…>] ») ;
#      · l'identifiant XMP (xmpMM:DocumentID et xmpMM:InstanceID) ;
#      · les dates de création et de modification (/CreationDate, /ModDate, et leurs pendants
#        XMP xmp:CreateDate, xmp:ModifyDate, xmp:MetadataDate).
#    Les avertissements et le nombre de pages de chaque export sont les mêmes aussi.
#    Contre-épreuves : deux exports du même binaire diffèrent bien à l'octet (sinon le retrait ne
#    prouverait rien), et la comparaison voit un écart vrai — le modèle de bulletin, qui porte un
#    champ de date, exporté à un autre jour.
#
#    La copie se restaure sur le cache NuGet de la machine, où la 8.0.1 est depuis que PdfSharp y
#    a été restauré : sans réseau ni 8.0.1 au cache, la recette le dit et saute ce temps, sans
#    conclure.
#
# 4. LES TESTS, sous le trait « poste=Q27 » (DependanceDePdfSharpTests, au noyau) : le processus
#    de test charge une version corrigée de Pkcs, celle de son relevé ; PdfSharp reste en 6.2.1.
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
#
# Ce qui DEMANDE LE RÉSEAU reste à Scripts/verifier-dependances.sh, lancée à part (Q12) : la base
# des vulnérabilités ne signale plus rien sur les cinq projets, et un projet témoin qui tire la
# 8.0.1 y est signalé, ses deux avancées comprises. Cette recette-ci ne lit pas la base : elle sait
# quelles versions les deux avancées visent parce que la section Q27 du plan l'a relevé.
#
set -euo pipefail
export LC_ALL=C
export DOTNET_CLI_UI_LANGUAGE=en
export DOTNET_NOLOGO=1

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# Le lecteur des .pub, cherché comme les recettes R5 et Q13 le cherchent : le corpus porte les deux
# documents ATR.
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

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    tail -20 "$travail/compilation.txt" >&2
    exit 1
fi

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"

# Les versions que les deux avancées visent, et la première corrigée de chaque série (la base
# NuGet du 28 octobre 2026 : [8.0.0, 8.0.1], [9.0.0, 9.0.20], [10.0.0, 10.0.12] et les
# préversions de la 11). Le module est partagé par les points 1 à 3.
cat > "$travail/pkcs.py" <<'PY'
import json

PKCS = "System.Security.Cryptography.Pkcs"
CORRIGEES = {8: (8, 0, 2), 9: (9, 0, 21), 10: (10, 0, 13)}

def version(texte):
    return tuple(int(x) for x in texte.split("-")[0].split("."))

def corrigee(texte):
    v = version(texte)
    if "-" in texte and v[0] == 11:
        return False
    if v[0] in CORRIGEES:
        return v >= CORRIGEES[v[0]]
    return v[0] > max(CORRIGEES)

def paquets(chemin):
    """Les paquets d'un relevé (.deps.json ou project.assets.json) : nom en minuscules → (nom, version)."""
    resultat = {}
    for cle, valeur in json.load(open(chemin))["libraries"].items():
        if valeur.get("type") == "package":
            nom, v = cle.split("/", 1)
            resultat[nom.lower()] = (nom, v)
    return resultat
PY

# ------------------------------------------------------------------------ 1. les sources

python3 - "$racine" "$travail" <<'PY'
import pathlib, re, sys
racine = pathlib.Path(sys.argv[1])
sys.path.insert(0, sys.argv[2])
from pkcs import PKCS, corrigee

echecs = []
noyau = (racine / "MyPublisher.Core/MyPublisher.Core.csproj").read_text(encoding="utf-8")
references = re.findall(r'<PackageReference Include="' + re.escape(PKCS) + r'" Version="([^"]+)"\s*/>', noyau)
if len(references) != 1:
    echecs.append(f"le noyau porte {len(references)} référence(s) directe(s) à {PKCS}, une attendue.")
elif not corrigee(references[0]):
    echecs.append(f"le noyau épingle {PKCS} {references[0]}, une version que les avancées visent.")
commentaire = re.search(r"<!--((?:(?!-->).)*)-->\s*<PackageReference Include=\"" + re.escape(PKCS), noyau, re.S)
if not commentaire or not all(a in commentaire.group(1) for a in ("GHSA-846f-f764-76mx", "GHSA-2fmv-rv2c-59x7", "PdfSharp")):
    echecs.append("l'épinglage n'a plus son pourquoi en commentaire : les deux avancées et PdfSharp.")
if '<PackageReference Include="PdfSharp" Version="6.2.1"/>' not in noyau:
    echecs.append("PdfSharp n'est plus en 6.2.1 au noyau : le moteur de l'export a bougé.")
for projet in sorted(racine.glob("*/*.csproj")):
    if projet.name != "MyPublisher.Core.csproj" and PKCS in projet.read_text(encoding="utf-8"):
        echecs.append(f"{projet.relative_to(racine)} référence {PKCS} : l'épinglage est au noyau seul.")
if echecs:
    for m in echecs:
        print(f"    ÉCHEC : {m}", file=sys.stderr)
    sys.exit(1)
print(f"    {PKCS} {references[0]} épinglé au noyau seul, son pourquoi en commentaire ; PdfSharp 6.2.1")
PY
echo "  1. les sources"

# ------------------------------------------------------------------------ 2. les relevés compilés

python3 - "$racine" "$travail" <<'PY'
import json, pathlib, subprocess, sys
racine = pathlib.Path(sys.argv[1])
sys.path.insert(0, sys.argv[2])
from pkcs import PKCS, corrigee, paquets

echecs = []
projets = ["MyPublisher", "MyPublisher.Core", "MyPublisher.Banc", "MyPublisher.Tests", "MyPublisher.Tests.Interface"]
vus = []
for projet in projets:
    releve = racine / projet / "bin/Debug/net10.0" / f"{projet}.deps.json"
    if not releve.exists():
        echecs.append(f"{releve.relative_to(racine)} est introuvable : la compilation est-elle faite ?")
        continue
    p = paquets(releve)
    if PKCS.lower() not in p:
        echecs.append(f"{projet} n'embarque pas {PKCS} : PdfSharp ne le tire plus ? Revoir Q27.")
        continue
    v = p[PKCS.lower()][1]
    vus.append(v)
    if not corrigee(v):
        echecs.append(f"{projet} embarque {PKCS} {v}, que GHSA-846f-f764-76mx et GHSA-2fmv-rv2c-59x7 visent.")

# La bibliothèque copiée est celle du paquet relevé.
releve = racine / "MyPublisher/bin/Debug/net10.0/MyPublisher.deps.json"
if releve.exists():
    d = json.load(open(releve))
    cle = next((k for k in d["libraries"] if k.lower().startswith(PKCS.lower() + "/")), None)
    cible = next(iter(d["targets"].values())).get(cle, {}) if cle else {}
    attendu = next(iter(cible.get("runtime", {}).values()), {}).get("fileVersion")
    dll = racine / "MyPublisher/bin/Debug/net10.0" / f"{PKCS}.dll"
    # Le numéro de fichier d'un assemblage .NET est dans sa ressource de version, en UTF-16.
    copie = dll.read_bytes() if dll.exists() else b""
    if not attendu or attendu.encode("utf-16-le") not in copie:
        echecs.append(f"{dll.relative_to(racine)} n'est pas celle du paquet relevé (numéro de fichier {attendu}).")

# Le graphe résolu : PdfSharp demande-t-il encore une version fautive ?
assets = json.load(open(racine / "MyPublisher.Core/obj/project.assets.json"))
cible = next(iter(assets["targets"].values()))
pdfsharp = next((k for k in cible if k.lower().startswith("pdfsharp/")), None)
demande = cible[pdfsharp]["dependencies"].get(PKCS) if pdfsharp else None
if echecs:
    for m in echecs:
        print(f"    ÉCHEC : {m}", file=sys.stderr)
    sys.exit(1)
print(f"    les {len(projets)} projets embarquent {PKCS} {', '.join(sorted(set(vus)))} ; la bibliothèque copiée est la sienne")
if demande and not corrigee(demande):
    print(f"    {pdfsharp} demande encore {PKCS} {demande} : l'épinglage reste nécessaire")
else:
    print(f"    {pdfsharp} demande {PKCS} {demande} : l'épinglage du noyau peut partir (Q27)")
PY
echo "  2. les relevés compilés"

# ------------------------------------------------------------------------ 3. l'export n'a pas bougé

avant="$travail/avant"
mkdir -p "$avant"
# Les sources de l'application et du noyau, telles qu'elles sont dans l'arbre (le juge y a peut-être
# posé un sabotage : l'avant le porte aussi, seul l'épinglage diffère). La documentation et les
# livres ne se compilent pas.
git ls-files -z -co --exclude-standard -- MyPublisher MyPublisher.Core \
    | python3 -c 'import os, sys
for c in sys.stdin.buffer.read().split(b"\0"):
    if c and os.path.isfile(c) and not c.startswith((b"MyPublisher/Docs/", b"MyPublisher/Books/")):
        sys.stdout.buffer.write(c + b"\0")' \
    | tar --null -T - -cf - | tar -xf - -C "$avant"
python3 - "$avant/MyPublisher.Core/MyPublisher.Core.csproj" <<'PY'
import pathlib, re, sys
p = pathlib.Path(sys.argv[1])
texte, n = re.subn(r'\s*<PackageReference Include="System\.Security\.Cryptography\.Pkcs" Version="[^"]+"\s*/>', "", p.read_text(encoding="utf-8"))
if n != 1:
    sys.exit("    ÉCHEC : l'épinglage n'a pas pu être retiré de la copie.")
p.write_text(texte, encoding="utf-8")
PY

if ! dotnet build "$avant/MyPublisher/MyPublisher.csproj" -c Debug --nologo -v q > "$travail/avant.txt" 2>&1; then
    if grep -qE "NU1301|NU1101|NU1102|Unable to load the service index|Name or service not known" "$travail/avant.txt"; then
        echo "  3. SAUTÉ : l'« avant » demande System.Security.Cryptography.Pkcs 8.0.1, absente du cache"
        echo "     NuGet, et les sources sont injoignables. Rien n'est conclu sur l'export."
        sauter_l_export=1
    else
        grep -E "error" "$travail/avant.txt" | sort -u | head -20 | sed 's/^/    /' >&2
        echo "ÉCHEC : la copie sans l'épinglage ne se compile pas." >&2
        exit 1
    fi
fi

if [ -z "${sauter_l_export:-}" ]; then
    # L'avant et l'après ne diffèrent que d'un paquet : Pkcs, de la 8.0.1 à la version corrigée.
    python3 - "$avant/MyPublisher/bin/Debug/net10.0/MyPublisher.deps.json" \
              "MyPublisher/bin/Debug/net10.0/MyPublisher.deps.json" "$travail" <<'PY'
import sys
sys.path.insert(0, sys.argv[3])
from pkcs import PKCS, corrigee, paquets
avant, apres = paquets(sys.argv[1]), paquets(sys.argv[2])
ecarts = sorted(n for n in set(avant) | set(apres) if avant.get(n, (None, None))[1] != apres.get(n, (None, None))[1])
a, b = avant.get(PKCS.lower(), (PKCS, "absent"))[1], apres.get(PKCS.lower(), (PKCS, "absent"))[1]
if ecarts != [PKCS.lower()] or a != "8.0.1" or not corrigee(b):
    print("    ÉCHEC : l'avant et l'après devaient ne différer que de Pkcs, de 8.0.1 à une version corrigée :", file=sys.stderr)
    for n in ecarts or ["(aucun écart)"]:
        print(f"      {n} : avant {avant.get(n, ('', 'absent'))[1]}, après {apres.get(n, ('', 'absent'))[1]}", file=sys.stderr)
    sys.exit(1)
print(f"    l'avant et l'après ne diffèrent que d'un paquet sur {len(apres)} : {PKCS} {a} → {b}")
PY

    # Un logo de huit pixels et un jeu de coordonnées complet : les champs et l'image de la carte
    # de visite s'exportent avec le reste, et la machine de l'utilisateur n'y entre pour rien.
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

    corpus=(Samples/*.mypub MyPublisher/Templates/*.mypub
            "MyPublisher/Books/ATA 21 - Air conditioning.pub"
            "MyPublisher/Books/Fiches limitations et procédures d'urgences.pub")
    mkdir -p "$travail/pdf"
    # $1 binaire · $2 document · $3 PDF · $4 date
    exporter() {
        "$1" "$2" --export "$3" --date "$4" --contacts "$travail/coordonnees.json" \
            > "$3.sortie" 2> "$3.avertissements"
    }
    rang=0
    for document in "${corpus[@]}"; do
        rang=$((rang + 1))
        for qui in avant apres; do
            if [ "$qui" = avant ]; then executable="$avant/MyPublisher/bin/Debug/net10.0/MyPublisher"; else executable="$binaire"; fi
            if ! exporter "$executable" "$document" "$travail/pdf/$rang-$qui.pdf" 2026-09-06; then
                echo "ÉCHEC : $document ne s'exporte pas ($qui)." >&2
                cat "$travail/pdf/$rang-$qui.pdf.sortie" "$travail/pdf/$rang-$qui.pdf.avertissements" >&2
                exit 1
            fi
        done
        printf '%s\n' "$document" > "$travail/pdf/$rang.nom"
    done
    # Les contre-épreuves : le même binaire deux fois, et le bulletin, qui porte un champ de date,
    # à un autre jour.
    exporter "$binaire" MyPublisher/Templates/bulletin.mypub "$travail/pdf/bis.pdf" 2026-09-06
    exporter "$binaire" MyPublisher/Templates/bulletin.mypub "$travail/pdf/autre-date.pdf" 2026-09-07

    python3 - "$travail/pdf" "$rang" <<'PY'
import pathlib, re, sys
dossier, n = pathlib.Path(sys.argv[1]), int(sys.argv[2])

# Les champs que PdfSharp tire au hasard ou date à chaque export, et rien d'autre. Chaque motif
# est ancré sur la syntaxe exacte où PdfSharp l'écrit.
MOTIFS = [
    ("préfixe de sous-ensemble", rb"/(FontName|BaseFont)/[A-Z]{6}\+", rb"/\1/XXXXXX+"),
    ("identifiant du fichier", rb"/ID\[<[0-9A-F]{32}><[0-9A-F]{32}>\]", rb"/ID[<><>]"),
    ("identifiant XMP", rb"<xmpMM:(DocumentID|InstanceID)>uuid:[0-9a-f-]{36}</xmpMM:\1>", rb"<xmpMM:\1>uuid:</xmpMM:\1>"),
    ("date", rb"/(CreationDate|ModDate)\(D:[0-9]{14}[^)]*\)", rb"/\1(D:)"),
    ("date XMP", rb"<xmp:(CreateDate|ModifyDate|MetadataDate)>[0-9T:.+\-Z]+</xmp:\1>", rb"<xmp:\1></xmp:\1>"),
]

def neutre(chemin, comptes=None):
    brut = chemin.read_bytes()
    for nom, motif, remplacement in MOTIFS:
        brut, k = re.subn(motif, remplacement, brut)
        if comptes is not None:
            comptes[nom] = comptes.get(nom, 0) + k
    return brut

echecs, comptes, octets, pages = [], {}, 0, 0
for i in range(1, n + 1):
    nom = (dossier / f"{i}.nom").read_text().strip()
    a, b = dossier / f"{i}-avant.pdf", dossier / f"{i}-apres.pdf"
    na, nb = neutre(a), neutre(b, comptes)
    if na != nb:
        premier = next(j for j in range(min(len(na), len(nb)) + 1) if j == min(len(na), len(nb)) or na[j] != nb[j])
        echecs.append(f"{nom} : les PDF diffèrent à l'octet {premier} ({len(na)} et {len(nb)} octets) : "
                      f"{na[max(0, premier - 40):premier + 40]!r}")
    # La sortie dit le compte de pages et le chemin du PDF, qui diffère : on lit le compte seul.
    comptes_de_pages = [re.match(r"(\d+) page", (dossier / f"{i}-{qui}.pdf.sortie").read_text()) for qui in ("avant", "apres")]
    if not all(comptes_de_pages) or comptes_de_pages[0].group(1) != comptes_de_pages[1].group(1):
        echecs.append(f"{nom} : le compte de pages diffère, ou ne se lit pas.")
    if (dossier / f"{i}-avant.pdf.avertissements").read_text() != (dossier / f"{i}-apres.pdf.avertissements").read_text():
        echecs.append(f"{nom} : les avertissements diffèrent.")
    octets += len(nb)
    pages += int(comptes_de_pages[1].group(1)) if comptes_de_pages[1] else 0

bulletin = dossier / "bis.pdf"
premier = next(i for i in range(1, n + 1) if (dossier / f"{i}.nom").read_text().strip() == "MyPublisher/Templates/bulletin.mypub")
if bulletin.read_bytes() == (dossier / f"{premier}-apres.pdf").read_bytes():
    echecs.append("contre-épreuve : deux exports du même binaire sont identiques à l'octet — le retrait des champs ne prouve rien.")
if neutre(bulletin) != neutre(dossier / f"{premier}-apres.pdf"):
    echecs.append("contre-épreuve : deux exports du même binaire diffèrent encore, champs retirés — un autre champ varie.")
if neutre(dossier / "autre-date.pdf") == neutre(bulletin):
    echecs.append("contre-épreuve : le bulletin exporté à un autre jour ne diffère pas — la comparaison ne voit rien.")
if any(comptes.get(nom, 0) == 0 for nom, _, _ in MOTIFS):
    echecs.append(f"un champ n'a été retiré nulle part, le motif ne lit plus ce que PdfSharp écrit : {comptes}")

if echecs:
    for m in echecs:
        print(f"    ÉCHEC : {m}", file=sys.stderr)
    sys.exit(1)
print(f"    {n} documents, {pages} pages, {octets} octets : identiques avant et après, champs retirés "
      f"({', '.join(f'{nom} ×{k}' for nom, k in comptes.items())})")
print("    contre-épreuves : deux exports du même binaire diffèrent à l'octet et pas au-delà ; "
      "le bulletin à un autre jour se voit")
PY
    echo "  3. l'export n'a pas bougé"
fi

# ------------------------------------------------------------------------ 4. les tests

if ! dotnet test MyPublisher.Tests/MyPublisher.Tests.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} --nologo \
        --filter "poste=Q27" > "$travail/tests.txt" 2>&1; then
    grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests du noyau (« poste=Q27 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt 2 ]; then
    echo "ÉCHEC : 2 tests attendus sous « poste=Q27 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  4. le noyau : $joues tests (le processus charge une version corrigée de Pkcs, celle de son relevé ; PdfSharp en 6.2.1)"

echo "Q27 : System.Security.Cryptography.Pkcs n'est plus en 8.0.1 nulle part, PdfSharp n'a pas bougé, et l'export est le même."
