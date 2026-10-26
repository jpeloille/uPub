#!/usr/bin/env bash
#
# Recette du poste N7 — mesurer l'écart de la première voie de R7, et s'arrêter.
#
# La première voie garderait le champ de saisie pour le curseur et la sélection, transparent,
# sur le texte composé. « MyPublisher.Banc saisie » mesure, sur la vraie fenêtre, l'écart entre
# le point d'insertion du champ (HitTestTextPosition sur le PART_TextPresenter d'une saisie
# ouverte par le banc, sans transaction, et la ligne de base de sa ligne) et celui du caractère
# composé (PointDe et la LignePosee de N3) : max(|Δx|, |Δ ligne de base|), en points, dans le
# repère du bloc. Le poste n'écrit aucune ligne de l'application. Ce qu'elle prouve :
#
#   1. LES SOURCES, lues par un script.
#      a. Le banc a sa commande « saisie », et Scripts/mesurer-la-saisie.sh la lance ;
#         l'application ne référence pas le banc, qui n'est pas livré avec elle.
#      b. La mesure lit ce qu'elle doit lire : le PART_TextPresenter, HitTestTextPosition et la
#         ligne de base de la ligne du champ ; la géométrie de N3 côté composition. La
#         configuration B se pose sur l'instance du champ et lui est rendue.
#      c. L'étalonnage, Samples/recette-saisie.mypub, est ce qu'il dit : un bloc d'un seul style,
#         sans retrait ni espace, à interligne 1, en trois paragraphes ; le même, avec 6 pt
#         d'espace après chacun ; un fragment de 16 pt dans un texte de 10 pt.
#   2. L'ÉTALONNAGE, mesuré par le banc en A et en B : le premier bloc reste sous 0,1 pt ; le
#      deuxième donne environ 6 pt à son deuxième paragraphe et 12 pt au troisième, et rien au
#      premier ; le troisième, un écart non nul, au-dessus du seuil. Une mesure qui oublierait
#      l'ordonnée rendrait zéro au deuxième bloc.
#   3. LES TESTS D'INTERFACE « poste=N7 » (MesureDeLaSaisieTests), sur la commande elle-même :
#      l'étalonnage en A et en B ; la saisie du banc est celle du double-clic ; mesurer ne change
#      ni le document, ni l'historique, ni le champ, et ne laisse aucune transaction ouverte ;
#      l'écart ne dépend pas du zoom ; un bloc tourné se mesure dans son repère ; les positions
#      sautent l'intérieur d'une paire et d'un « \r\n » ; le corpus a les blocs que le plan
#      annonce.
#   4. LE CORPUS, par Scripts/mesurer-la-saisie.sh : l'exemple, ATA 21 et les Fiches par
#      pub2raw, recette-coupure, en A et en B, et la contre-épreuve au zoom 2. Il doit pouvoir
#      se mesurer ; ce qu'il donne ne fait pas échouer la recette — c'est une décision, que le
#      propriétaire prend (PLAN.md, N7 et R7), non un résultat à atteindre.
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

# ---------------------------------------------------------------------- 1. les sources

python3 -I - "$racine" <<'PY'
import json, pathlib, re, sys

racine = pathlib.Path(sys.argv[1])
echecs = []
def refuser(message):
    echecs.append(message)

programme = (racine / "MyPublisher.Banc/Program.cs").read_text(encoding="utf-8")
saisie = (racine / "MyPublisher.Banc/Saisie.cs").read_text(encoding="utf-8")
script = racine / "Scripts/mesurer-la-saisie.sh"
application = (racine / "MyPublisher/MyPublisher.csproj").read_text(encoding="utf-8")

# ------------------------------------------------------------------ a. la commande
if '"saisie" => Saisie.Lancer(args)' not in programme:
    refuser("le banc n'a pas de commande « saisie » (Program.cs).")
if not script.exists() or not (script.stat().st_mode & 0o111):
    refuser("Scripts/mesurer-la-saisie.sh manque, ou n'est pas exécutable.")
elif "saisie" not in script.read_text(encoding="utf-8") or "--au-mieux" not in script.read_text(encoding="utf-8"):
    refuser("Scripts/mesurer-la-saisie.sh ne lance pas la commande « saisie » en A et en B.")
if "Banc" in application:
    refuser("MyPublisher.csproj référence le banc : l'application livrée l'embarquerait.")
print("    a. la commande « saisie » du banc, le script qui la lance ; l'application ne référence pas le banc")

# ------------------------------------------------------------------ b. ce que la mesure lit
for attendu, quoi in [
    ('p.Name == "PART_TextPresenter"', "le PART_TextPresenter du champ"),
    ("miseEnPage.HitTestTextPosition(indice)", "HitTestTextPosition"),
    ("lignes[k].Baseline", "la ligne de base de la ligne du champ"),
    ("indices.Select(geometrie.PointDe)", "PointDe, côté composition"),
    ("zone.ClearValue(TextBox.LineHeightProperty)", "l'interligne de B rendu au champ"),
    ("partie.UseLayoutRounding = arrondi", "l'arrondi de B rendu au champ"),
]:
    if attendu not in saisie:
        refuser(f"la mesure ne lit plus {quoi} (« {attendu} » introuvable dans Saisie.cs).")
if "BeginEdit" in saisie or "CommitEdit" in saisie:
    refuser("la mesure ouvre une transaction : elle doit lire le champ sans rien changer.")
print("    b. le PART_TextPresenter, HitTestTextPosition et la ligne de base ; PointDe ; B rendu au champ ; aucune transaction")

# ------------------------------------------------------------------ c. l'étalonnage
document = json.loads((racine / "Samples/recette-saisie.mypub").read_text(encoding="utf-8"))
blocs = [f for p in document["Pages"] for f in p["Frames"] if f.get("$kind") == "text"]
if len(blocs) != 3:
    refuser(f"recette-saisie.mypub porte {len(blocs)} blocs de texte, trois attendus.")
else:
    seul, apres, fragment = blocs
    def simple(b):
        return (b["LineHeight"] == 1 and not b["Paragraphs"] and b["Columns"] == 1 and b["VerticalAlign"] == "Top"
                and all(b[k] == 0 for k in ("IndentLeft", "IndentRight", "IndentFirstLine", "SpaceBefore")))
    if not (simple(seul) and seul["SpaceAfter"] == 0 and not seul["Spans"] and seul["Text"].count("\n") == 2):
        refuser("le premier bloc n'est pas d'un seul style, sans retrait ni espace, à interligne 1, en trois paragraphes.")
    if not (simple(apres) and apres["SpaceAfter"] == 6 and apres["Text"] == seul["Text"] and not apres["Spans"]):
        refuser("le deuxième bloc n'est pas le premier avec 6 pt d'espace après chaque paragraphe.")
    if not (fragment["FontSize"] == 10 and len(fragment["Spans"]) == 1 and fragment["Spans"][0]["FontSize"] == 16):
        refuser("le troisième bloc ne porte pas un fragment de 16 pt dans un texte de 10 pt.")
print("    c. l'étalonnage : un seul style ; le même, 6 pt après chaque paragraphe ; un fragment de 16 pt dans 10")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la commande, ce qu'elle lit, et le document d'étalonnage"

# ------------------------------------------------------------------- 2. l'étalonnage

banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"
for configuration in A B; do
    options=()
    [ "$configuration" = B ] && options=(--au-mieux)
    if ! "$banc" saisie Samples/recette-saisie.mypub --sortie "$travail/etalonnage-$configuration.json" \
            ${options[@]+"${options[@]}"} > "$travail/etalonnage-$configuration.txt" 2>&1; then
        sed 's/^/    /' "$travail/etalonnage-$configuration.txt" >&2
        echo "ÉCHEC : le banc n'a pas mesuré l'étalonnage ($configuration)." >&2
        exit 1
    fi
done

python3 -I - "$travail" <<'PY'
import json, pathlib, sys

travail = pathlib.Path(sys.argv[1])
echecs = []
for configuration in "AB":
    blocs = json.loads((travail / f"etalonnage-{configuration}.json").read_text(encoding="utf-8"))["Blocs"]
    def ecarts(b, debut=0, fin=None):
        fin = len(b["Texte"]) if fin is None else fin
        return [max(abs(p[1]), abs(p[2])) for p in b["Positions"] if debut <= p[0] <= fin]
    seul, apres, fragment = blocs
    texte = apres["Texte"]
    un = texte.index("\n")
    deux = texte.index("\n", un + 1)
    nombres = (max(ecarts(seul)), max(ecarts(apres, 0, un)), max(ecarts(apres, un + 1, deux)),
               max(ecarts(apres, deux + 1)), max(ecarts(fragment)))
    if not nombres[0] < 0.1:
        echecs.append(f"{configuration} : le bloc d'un seul style s'écarte de {nombres[0]:.3f} pt (moins de 0,1 attendu).")
    if not nombres[1] < 0.1:
        echecs.append(f"{configuration} : le premier paragraphe du deuxième bloc s'écarte de {nombres[1]:.3f} pt (rien attendu).")
    if not 5.75 <= nombres[2] <= 6.25:
        echecs.append(f"{configuration} : le deuxième paragraphe s'écarte de {nombres[2]:.3f} pt (environ 6 attendus).")
    if not 11.75 <= nombres[3] <= 12.25:
        echecs.append(f"{configuration} : le troisième paragraphe s'écarte de {nombres[3]:.3f} pt (environ 12 attendus).")
    if not nombres[4] > 0.75:
        echecs.append(f"{configuration} : le fragment de 16 pt s'écarte de {nombres[4]:.3f} pt (au-dessus du seuil attendu).")
    print(f"    {configuration} : un seul style {nombres[0]:.3f} pt ; six points après {nombres[1]:.3f}, "
          f"{nombres[2]:.3f}, {nombres[3]:.3f} pt ; le fragment de 16 pt {nombres[4]:.3f} pt")
if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  2. l'étalonnage : sous 0,1 pt ; 6 puis 12 pt ; un écart non nul"

# ------------------------------------------------------------------------- 3. les tests

attendus=13
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=N7" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests de la mesure de la saisie (« poste=N7 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N7 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  3. la commande sur la vraie fenêtre : $joues tests (étalonnage, double-clic, rien de changé, zoom, bloc tourné, positions, corpus)"

# ------------------------------------------------------------------------- 4. le corpus

if ! Scripts/mesurer-la-saisie.sh "$travail/corpus" > "$travail/corpus.txt" 2>&1; then
    sed 's/^/    /' "$travail/corpus.txt" >&2
    echo "ÉCHEC : le corpus n'a pas pu se mesurer." >&2
    exit 1
fi
sed -n '/^   les quatre/p; /^5\. /p' "$travail/corpus.txt" | sed 's/^ */    /'
echo "  4. le corpus mesuré, en A et en B, et la contre-épreuve au zoom 2 (Scripts/mesurer-la-saisie.sh) : une décision, non un verdict"

echo "N7 : l'écart de la première voie se mesure et se rejoue ; la voie de R7 revient au propriétaire."
