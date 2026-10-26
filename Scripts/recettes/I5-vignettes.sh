#!/usr/bin/env bash
#
# Recette du poste I5 — les vignettes de pages.
#
# Chaque page du panneau Pages se montre en vignette de 96 px de large, au rapport de la
# page, son numéro dessous ; la page courante est bordée de 2 px d'accent. La vignette se
# dessine depuis la composition, comme celle des modèles depuis Q9 — VignetteDePage dérive
# de TemplatePreview —, et non par des ControleDeBloc. Elle suit le document ouvert, qui se
# modifie en place : elle écoute sa page, bloc par bloc, et le document. Trois temps.
#
#   1. LES SOURCES, lues par un script.
#      a. L'entrée du panneau (EntreeDePage.axaml) présente une VignetteDePage, une seule,
#         dans un cadre de 96 px ; VignetteDePage dérive de TemplatePreview ; ni l'une ni
#         l'autre ne pose un ControleDeBloc — DetachementTests en compte exactement deux par
#         bloc de texte affiché.
#      b. Les deux vues du poste sont dans la liste des vues de la phase de la recette I1,
#         qui vérifie qu'aucune n'écrit une couleur de la maquette en dur. TemplatePreview
#         n'y entre pas : son blanc est celui de la feuille, le document, et non celui de
#         l'interface — comme celui de PageRoot, que la recette I1 laisse à I4.
#      c. Les sept textes que le juge sabote dans TemplatePreview.cs pour ce poste sont
#         présents, chacun une fois : « eprouver-le-juge.sh --cibles » le vérifie pour tous
#         les textes visés — hors du juge, qui ôte justement l'un d'eux à chaque sabotage.
#         Au moins sept, et non sept exactement, depuis la relecture du 26 octobre 2026 :
#         I8 et A10 y ont posé chacun le leur, et le compte exact faisait tomber cette
#         recette à chaque sabotage d'un autre poste dans la vignette.
#   2. LES TESTS d'interface « poste=I5 », sur la vraie fenêtre, les pixels lus sur l'image
#      composée :
#      - chaque page a sa vignette de 96 × 136 pour un A4, centrée, le numéro à 5 px
#        dessous, la courante bordée de 2 px d'accent et son numéro en demi-gras, les
#        autres d'un filet au bord des champs ; choisir une page ne déplace rien ; une page
#        à l'italienne donne une vignette de 96 × 68 ;
#      - chaque vignette montre les blocs de SA page, et le fond de page du document ;
#      - modifier un bloc de la page 2 change les pixels de la vignette 2, dès la retouche,
#        et non ceux de la vignette 1 ; une annulation les rend, au pixel ; rétablir les
#        reprend ;
#      - un enfant de groupe recoloré, un enfant posé puis déplacé, une page vidée : chaque
#        geste se voit ;
#      - ajouter, dupliquer ou supprimer une page met la liste à jour — nombre, numéros,
#        page courante bordée —, et la copie montre les pixels de l'original ;
#      - un clic sur une vignette change la page courante, sans rien mettre à l'historique ;
#        un Ctrl + clic sur la vignette courante la garde : la fenêtre ne reste jamais sans
#        page courante ;
#      - une vignette sortie de l'arbre n'écoute plus ni le document, ni la page, ni un
#        bloc, ni une collection d'écarts, ni un groupe — vingt passages ;
#      - sur ATA 21, redessiner toutes les vignettes après une retouche du plus long bloc de
#        texte prend moins de 50 ms (médiane de trois passes) ; après un changement du jeu
#        de polices, qui recompose tout, moins de 100 ms — voir PLAN.md, poste I5.
#   3. DetachementTests et VignetteTests passent, et aucun commit du poste n'y a touché.
#
# Ce qu'elle ne prouve PAS :
#   - que la vignette est fidèle à la page au-delà du schéma : le texte y est en filets,
#     l'image en cadre barré, un dégradé par sa première couleur, comme dans le catalogue
#     (B1, Q9) ; un champ tient un filet de toute la largeur ;
#   - qu'un fragment de style retouché en place se voit : la vignette écoute les
#     collections d'écarts, comme la planche, et non chaque écart ;
#   - la mesure en Release, ni sur une autre machine : elle est prise sous le banc, en
#     Debug, par un rendu à la demande de chaque vignette.
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

python3 - "$racine" <<'PY'
import pathlib, re, sys, xml.etree.ElementTree as ET

racine = pathlib.Path(sys.argv[1])
VUES = racine / "MyPublisher/Views"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"

echecs = []
def exige(condition, message):
    if not condition:
        echecs.append(message)

entree_texte = (VUES / "EntreeDePage.axaml").read_text(encoding="utf-8")
entree = ET.fromstring(entree_texte)
vignette = (VUES / "VignetteDePage.cs").read_text(encoding="utf-8")

# a. L'entrée et sa vignette.
vignettes = [e for e in entree.iter() if e.tag.endswith("}VignetteDePage")]
exige(len(vignettes) == 1, f"EntreeDePage.axaml pose {len(vignettes)} VignetteDePage, une attendue.")
if vignettes:
    exige(vignettes[0].get("Page") == "{Binding}", "la vignette ne montre pas la page de son entrée.")
    exige("Document" in (vignettes[0].get("Document") or ""), "la vignette ne reçoit pas le document ouvert.")
cadres = [e for e in entree.iter() if (e.get(X + "Name") or e.get("Name")) == "CadreDeVignette"]
exige(len(cadres) == 1, "le cadre de la vignette (CadreDeVignette) manque.")
exige(re.search(r'<Setter Property="Width" Value="96"/>', entree_texte) is not None,
      "la vignette n'a plus 96 px de large.")
exige(re.search(r"public sealed class VignetteDePage\s*:\s*TemplatePreview\b", vignette) is not None,
      "VignetteDePage ne dérive plus de TemplatePreview : la vignette ne se dessine plus depuis la composition des modèles.")
def sans_commentaires(texte):
    """Le code seul : un commentaire qui nomme un contrôle de bloc n'en pose aucun."""
    texte = re.sub(r"<!--.*?-->", "", texte, flags=re.S)
    return re.sub(r"//.*", "", texte)
for nom, texte in (("EntreeDePage.axaml", entree_texte), ("VignetteDePage.cs", vignette)):
    exige(not re.search(r"\b(TexteCompose|MarqueurDebordement|ControleDeBloc)\b", sans_commentaires(texte)),
          f"{nom} pose un contrôle de bloc : la vignette se dessine depuis la composition.")
print("    a. l'entrée pose une vignette de 96 px, dérivée de celle des modèles, sans contrôle de bloc")

# b. La liste des vues de la phase.
recette_i1 = (racine / "Scripts/recettes/I1-apparence.sh").read_text(encoding="utf-8")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
for vue in ("MyPublisher/Views/EntreeDePage.axaml", "MyPublisher/Views/VignetteDePage.cs"):
    exige(vue in liste, f"{vue} n'est pas dans la liste des vues de la phase de la recette I1.")
print("    b. les deux vues du poste sont inscrites à la liste de la recette I1")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY

# c. Les cibles du juge, dont les sept de TemplatePreview.cs. Pas sous le juge lui-même, qui
#    tient son verrou pendant qu'il sabote : il vient d'ôter l'un de ces textes, et cette
#    lecture échouait sous chaque sabotage, quel qu'il fût. La recette « attrapait » alors
#    les deux cent et quelques du juge, et ceux qui ne nomment pas leur garde-fou — les sept
#    premiers, sur le noyau — passaient pour pris sans qu'aucun test les eût vus (relecture
#    du 26 octobre 2026).
sept="$(grep -c "^    MyPublisher/Views/TemplatePreview.cs" Scripts/eprouver-le-juge.sh || true)"
if [ "${sept:-0}" -lt 7 ]; then
    echo "ÉCHEC : le juge vise $sept texte(s) dans TemplatePreview.cs, sept au moins attendus." >&2
    exit 1
fi
if [ -e .juge-en-cours ]; then
    echo "    c. sous le juge, qui sabote ces textes : leur présence ne se lit pas ici — les sept de TemplatePreview.cs restent visés"
else
    if ! Scripts/eprouver-le-juge.sh --cibles > "$travail/cibles.txt" 2>&1; then
        sed 's/^/    /' "$travail/cibles.txt" >&2
        echo "ÉCHEC : un texte visé par le juge a disparu ou s'est dédoublé (règle 7)." >&2
        exit 1
    fi
    echo "    c. les textes visés par le juge sont présents, une fois chacun — les sept de TemplatePreview.cs compris"
fi
echo "  1. les sources : l'entrée, sa vignette, la liste de la recette I1, les cibles du juge"

# ------------------------------------------------------------------------ 2. les tests

attendus=10
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=I5" --logger "console;verbosity=detailed" > "$travail/tests.txt" 2>&1; then
    grep -E "\[FAIL\]|Assert|ATA 21" "$travail/tests.txt" | sed 's/^/    /' >&2 || true
    tail -40 "$travail/tests.txt" | sed 's/^/    /' >&2
    echo "ÉCHEC : les tests d'interface sous « poste=I5 » ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=I5 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. allure, page propre et fond, retouche et annulation, groupes, ajout, copie et suppression, clic, abonnements ($joues tests)"
grep -E "^ *ATA 21" "$travail/tests.txt" | sed 's/^ */       /'

# ---------------------------------------------- 3. DetachementTests et VignetteTests

if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "FullyQualifiedName~.DetachementTests.|FullyQualifiedName~.VignetteTests." \
        > "$travail/temoins.txt" 2>&1; then
    sed 's/^/    /' "$travail/temoins.txt" >&2
    echo "ÉCHEC : DetachementTests ou VignetteTests ne passent plus." >&2
    exit 1
fi
temoins="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/temoins.txt" | head -1)"
if [ "${temoins:-0}" -lt 10 ]; then
    echo "ÉCHEC : ${temoins:-0} test(s) de DetachementTests et VignetteTests joué(s), dix au moins attendus." >&2
    exit 1
fi
touches="$(git log --format='%h %s' -- MyPublisher.Tests.Interface/DetachementTests.cs \
    MyPublisher.Tests.Interface/VignetteTests.cs | grep -E '^[0-9a-f]+ I5 ?:' || true)"
if [ -n "$touches" ]; then
    echo "$touches" | sed 's/^/    /' >&2
    echo "ÉCHEC : un commit du poste I5 a touché DetachementTests ou VignetteTests, qui devaient passer inchangés." >&2
    exit 1
fi
echo "  3. DetachementTests et VignetteTests passent, et aucun commit du poste n'y touche ($temoins tests)"

echo "I5 : chaque page du panneau a sa vignette, tirée de la composition, qui suit le document."
