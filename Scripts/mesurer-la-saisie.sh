#!/usr/bin/env bash
#
# Mesure l'écart de la première voie de R7 sur le corpus — poste N7.
#
# La première voie garde le champ de saisie pour le curseur et la sélection, transparent,
# et laisse le texte composé se dessiner dessous. Elle ne vaut que si le curseur du champ
# tombe là où le caractère composé est dessiné. Ce script le mesure, position par position,
# sur la vraie fenêtre : « MyPublisher.Banc saisie » ouvre la saisie de chaque bloc et lit,
# pour chaque position du curseur, le point d'insertion du champ (HitTestTextPosition sur
# le PART_TextPresenter, et la ligne de base de sa ligne) et celui de la composition
# (PointDe et la LignePosee de N3), dans le repère du bloc, en points. L'écart vaut
# max(|Δx|, |Δ ligne de base|).
#
#   Scripts/mesurer-la-saisie.sh [dossier]
#
# Le dossier reçoit un relevé JSON par document et par configuration, et « rapport.txt »,
# que le script écrit aussi sur sa sortie ; sans dossier, un dossier temporaire, effacé.
#
# Ce qu'il joue :
#
#   1. l'étalonnage, Samples/recette-saisie.mypub, dont les trois écarts sont connus ;
#   2. le corpus : l'exemple (ses deux pages), ATA 21 et les Fiches par pub2raw, et
#      Samples/recette-coupure.mypub (74 blocs aux limites de la coupure). Chaque bloc de
#      texte visible, sans champ, au premier niveau ; les cases et les membres de groupe
#      sont comptés, sans mesure (N26, N27) ;
#   3. deux configurations : A, le champ tel qu'il est ; B, « au mieux de la voie 1 » —
#      l'interligne de la composition posé sur l'instance du champ, et l'arrondi de mise en
#      page retiré du champ et de ses parties (« --au-mieux ») ;
#   4. la contre-épreuve : l'exemple au zoom 2 (200 %), qui doit donner les mêmes écarts
#      à 0,01 pt près, l'écart en points ne dépendant pas du zoom.
#
# Le seuil est 0,75 pt, un pixel au zoom 100 % ; le rapport donne aussi chaque valeur en
# pixels à 100 % (÷ 0,75) et à 200 % (÷ 0,375). Si le maximum de B sur toutes les
# positions des quatre documents est sous le seuil, la voie 1 tient ; sinon, on s'arrête
# et le propriétaire choisit (PLAN.md, N7 et R7). Le script ne sort en erreur que si une
# mesure n'a pas pu se faire : ce qu'il trouve est une décision, non un résultat à
# atteindre.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$racine"

banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"
if [ ! -x "$banc" ]; then
    echo "ÉCHEC : binaire introuvable ($banc). Compiler d'abord." >&2
    exit 1
fi

# Les .pub se lisent par pub2raw, cherché comme la recette R5 le cherche.
outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$outil" ] || [ ! -x "$outil" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable (Scripts/outils-pub.sh, ou « sudo apt install libmspub-tools »)." >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$outil"
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

if [ -n "${1-}" ]; then
    sortie="$1"
    mkdir -p "$sortie"
    sortie="$(cd "$sortie" && pwd)"
else
    sortie="$(mktemp -d)"
    trap 'rm -rf "$sortie"' EXIT
fi

mesurer() {
    local nom="$1" document="$2"
    shift 2
    if ! "$banc" saisie "$document" --sortie "$sortie/$nom.json" "$@" > "$sortie/$nom.txt" 2>&1; then
        echo "ÉCHEC : la mesure de $document ($*) n'a pas abouti." >&2
        sed 's/^/    /' "$sortie/$nom.txt" >&2
        exit 1
    fi
}

for configuration in A B; do
    options=()
    [ "$configuration" = B ] && options=(--au-mieux)
    mesurer "etalonnage-$configuration" "Samples/recette-saisie.mypub" ${options[@]+"${options[@]}"}
    mesurer "exemple-$configuration" "Samples/exemple-bulletin.mypub" ${options[@]+"${options[@]}"}
    mesurer "ata21-$configuration" "MyPublisher/Books/ATA 21 - Air conditioning.pub" ${options[@]+"${options[@]}"}
    mesurer "fiches-$configuration" "MyPublisher/Books/Fiches limitations et procédures d'urgences.pub" ${options[@]+"${options[@]}"}
    mesurer "coupure-$configuration" "Samples/recette-coupure.mypub" ${options[@]+"${options[@]}"}
    mesurer "exemple-$configuration-zoom2" "Samples/exemple-bulletin.mypub" --zoom 2 ${options[@]+"${options[@]}"}
done

python3 -I - "$sortie" <<'PY' | tee "$sortie/rapport.txt"
import json, math, pathlib, sys

dossier = pathlib.Path(sys.argv[1])
SEUIL = 0.75
documents = [("exemple", "l'exemple"), ("ata21", "ATA 21"), ("fiches", "les Fiches"), ("coupure", "recette-coupure")]

def lire(nom):
    return json.loads((dossier / f"{nom}.json").read_text(encoding="utf-8"))

def ecart(p):
    return max(abs(p[1]), abs(p[2]))

def centile(valeurs, rang=0.95):
    triees = sorted(valeurs)
    if not triees:
        return 0.0
    return triees[min(len(triees) - 1, max(0, math.ceil(rang * len(triees)) - 1))]

def pt(v):
    return f"{v:8.3f}"

def px(v):
    # en pixels à 100 % (un pixel vaut 0,75 pt) et à 200 % (0,375 pt)
    return f"{v / 0.75:7.1f} / {v / 0.375:7.1f}"

def resume(blocs):
    ecarts = [ecart(p) for b in blocs for p in b["Positions"]]
    return {
        "blocs": len(blocs),
        "positions": len(ecarts),
        "max": max(ecarts, default=0.0),
        "p95": centile(ecarts),
        "dessus": sum(1 for e in ecarts if e > SEUIL),
        "blocs_dessus": sum(1 for b in blocs if any(ecart(p) > SEUIL for p in b["Positions"])),
    }

print("Mesure de la saisie (N7) : écart, en points, entre le curseur du champ et le caractère composé.")
print(f"Seuil : {SEUIL} pt, un pixel au zoom 100 %. En pixels : « à 100 % / à 200 % ».")
print()

# ------------------------------------------------------------------- 1. l'étalonnage
print("1. L'étalonnage, Samples/recette-saisie.mypub")
for configuration in "AB":
    for b in lire(f"etalonnage-{configuration}")["Blocs"]:
        texte = b["Texte"]
        paragraphes = []
        debut = 0
        for rang, morceau in enumerate(texte.split("\n")):
            fin = debut + len(morceau)
            dedans = [ecart(p) for p in b["Positions"] if debut <= p[0] <= fin]
            paragraphes.append(max(dedans, default=0.0))
            debut = fin + 1
        par = " ; ".join(f"{v:.3f}" for v in paragraphes)
        print(f"   {configuration}  {b['Nom']:<30} max {b['Maximum']:7.3f} pt   par paragraphe : {par}")
print()

# ---------------------------------------------------------------------- 2. le corpus
print("2. Le corpus : par document")
print(f"   {'document':<16} {'blocs':>5} {'positions':>9} {'conf':>4} {'max (pt)':>9} {'p95 (pt)':>9} "
      f"{'> seuil':>8} {'blocs >':>7}   {'max en px 100 / 200 %':>21}   {'p95 en px 100 / 200 %':>21}")
tous = {"A": [], "B": []}
for nom, titre in documents:
    for configuration in "AB":
        releve = lire(f"{nom}-{configuration}")
        tous[configuration].extend(releve["Blocs"])
        r = resume(releve["Blocs"])
        print(f"   {titre:<16} {r['blocs']:>5} {r['positions']:>9} {configuration:>4} {pt(r['max'])} {pt(r['p95'])} "
              f"{r['dessus']:>8} {r['blocs_dessus']:>7}   {px(r['max']):>21}   {px(r['p95']):>21}")
    releve = lire(f"{nom}-A")
    ecartes = releve["Ecartes"]
    tournes = sum(1 for b in releve["Blocs"] if b["Rotation"] % 360 != 0)
    raisons = {}
    for e in ecartes:
        raisons[e["Raison"]] = raisons.get(e["Raison"], 0) + 1
    detail = ", ".join(f"{n} {r}" for r, n in sorted(raisons.items())) or "aucun"
    faconnage = max((b["Faconnage"] for b in releve["Blocs"]), default=0.0)
    print(f"   {'':<16} dont {tournes} tourné(s) ; comptés sans mesure : {detail} ; "
          f"la composition s'écarte de son propre dessin (crénage, ligature) d'au plus {faconnage:.3f} pt")
for configuration in "AB":
    r = resume(tous[configuration])
    print(f"   {'les quatre':<16} {r['blocs']:>5} {r['positions']:>9} {configuration:>4} {pt(r['max'])} {pt(r['p95'])} "
          f"{r['dessus']:>8} {r['blocs_dessus']:>7}   {px(r['max']):>21}   {px(r['p95']):>21}")
print()

# ----------------------------------------------------------------------- 3. les causes
print("3. Par cause, sur les quatre documents (un bloc porte souvent plusieurs causes)")
causes = []
for b in tous["A"]:
    for c in b["Causes"]:
        if c not in causes:
            causes.append(c)
cles = lambda b: (b["Page"], b["Nom"], b["Texte"])
b_de = {}
for b in tous["B"]:
    b_de.setdefault(cles(b), []).append(b)
paires = []
for a in tous["A"]:
    b = b_de[cles(a)].pop(0)
    paires.append((a, b))
print(f"   {'cause':<26} {'blocs':>5} {'dont > seuil en B':>18} {'max A':>8} {'max B':>8} {'p95 B':>8}")
for c in causes:
    avec = [(a, b) for a, b in paires if c in a["Causes"]]
    ecarts_b = [ecart(p) for _, b in avec for p in b["Positions"]]
    print(f"   {c:<26} {len(avec):>5} {sum(1 for _, b in avec if b['Maximum'] > SEUIL):>18} "
          f"{max(a['Maximum'] for a, _ in avec):8.3f} {max(b['Maximum'] for _, b in avec):8.3f} {centile(ecarts_b):8.3f}")
sans = [(a, b) for a, b in paires if not a["Causes"]]
print(f"   {'(aucune)':<26} {len(sans):>5} {sum(1 for _, b in sans if b['Maximum'] > SEUIL):>18} "
      + (f"{max(a['Maximum'] for a, _ in sans):8.3f} {max(b['Maximum'] for _, b in sans):8.3f}" if sans else "       —        —"))
lignes = sum(1 for a, _ in paires if a["LignesComposees"] != a["LignesDuChamp"])
print(f"   blocs dont le champ n'a pas le nombre de lignes de la composition : {lignes} sur {len(paires)} en A, "
      f"{sum(1 for _, b in paires if b['LignesComposees'] != b['LignesDuChamp'])} en B")
print()
print("   Les positions au-dessus du seuil, par ce qui les y met, lu sur leur ligne (le motif) :")
motifs = []
comptes = {}
for nom, titre in documents:
    for configuration in "AB":
        for bloc in lire(f"{nom}-{configuration}")["Blocs"]:
            for position in bloc["Positions"]:
                if len(position) > 3:
                    if position[3] not in motifs:
                        motifs.append(position[3])
                    cle = (nom, configuration, position[3])
                    comptes[cle] = comptes.get(cle, 0) + 1
print(f"      {'motif':<26}" + "".join(f" {titre[:10]:>10} A {titre[:10]:>10} B" for _, titre in documents))
ordre = sorted(motifs, key=lambda m: -sum(comptes.get((n, "B", m), 0) for n, _ in documents))
for motif in ordre:
    print(f"      {motif:<26}" + "".join(
        f" {comptes.get((n, 'A', motif), 0):>12} {comptes.get((n, 'B', motif), 0):>12}" for n, _ in documents))
print()

# ------------------------------------------------------------------ 4. la contre-épreuve
print("4. La contre-épreuve : l'exemple au zoom 2")
for configuration in "AB":
    base, zoom2 = lire(f"exemple-{configuration}"), lire(f"exemple-{configuration}-zoom2")
    pire = 0.0
    total = 0
    for x, y in zip(base["Blocs"], zoom2["Blocs"]):
        if x["Nom"] != y["Nom"] or len(x["Positions"]) != len(y["Positions"]):
            print(f"   ÉCHEC : les deux relevés ne mesurent pas les mêmes positions ({x['Nom']}).")
            sys.exit(1)
        for p, q in zip(x["Positions"], y["Positions"]):
            total += 1
            pire = max(pire, abs(p[1] - q[1]), abs(p[2] - q[2]))
    if len(base["Blocs"]) != len(zoom2["Blocs"]):
        print("   ÉCHEC : les deux relevés n'ont pas les mêmes blocs.")
        sys.exit(1)
    verdict = "mêmes écarts à 0,01 pt près" if pire <= 0.01 else "ÉCARTS DIFFÉRENTS"
    print(f"   {configuration} : {total} positions, zoom {zoom2['Zoom']} (un point vaut {zoom2['Echelle']:.4f} px "
          f"contre {base['Echelle']:.4f}) ; plus grande différence {pire:.4f} pt — {verdict}")
print()

# --------------------------------------------------------------------------- 5. le seuil
maximum = resume(tous["B"])["max"]
if maximum < SEUIL:
    print(f"5. Verdict : le maximum de B, {maximum:.3f} pt, est SOUS le seuil de {SEUIL} pt — la voie 1 tient.")
else:
    print(f"5. Verdict : le maximum de B, {maximum:.3f} pt, est AU-DESSUS du seuil de {SEUIL} pt — "
          "on s'arrête : la voie de R7 revient au propriétaire.")
PY
