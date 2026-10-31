#!/usr/bin/env bash
#
# Recette du poste N26 — taper dans le texte d'un groupe, et prendre un membre seul (fiche 20 ;
# fiche 23, a6 et c15 ; spécifié le 25 octobre 2026). Le livre retape le texte d'un élément de la
# bibliothèque dans le groupe, sans le dissocier, et prend un membre d'un clic pour en remplacer
# l'image (p. 58-60) ; seul le redimensionnement d'un membre y demande de dissocier. La sélection de
# MyPublisher ne voyait que le premier niveau, et la saisie exigeait un bloc de texte seul. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le modèle de vue : la sélection garde son modèle — les blocs de la page — et s'augmente de
#         deux états, au partiel du poste (règle 10) : TexteEnSaisie, que la planche dit, et
#         MembreSeul ; BlocVise les rassemble. La suppression, les flèches et l'alignement le
#         consultent d'abord (DeleteSelected, NudgeSelection, AlignEach) ;
#      b. la barre et le dock suivent le bloc visé : « ReglagesDuBloc » et les trois sections du dock
#         sont liés à BlocVise — la recette Q23 le vérifie aussi —, « Position et taille » ne se
#         règle pas pour un membre ; la mise en forme des caractères (R7, N24) et celle des
#         paragraphes (R6) visent BlocVise ;
#      c. la planche : le clic dans le texte traverse le groupe (CheminSous) et prend le groupe, la
#         racine du chemin ; rien n'y dissocie un groupe ;
#      d. la découpe de Q5 suit le nombre des blocs du groupe et la révision que la planche avance
#         quand le filet d'un membre visé change.
#
# 2. LES TESTS, sous le trait « poste=N26 », dans la vraie fenêtre (SaisieDansUnGroupeTests) : la
#    recette du plan — un clic sur le 3ᵉ caractère du texte d'un groupe ouvre sa saisie, curseur à 2,
#    le groupe restant sélectionné ; la frappe fait un pas, le groupe garde ses enfants ; un clic sur
#    l'image d'un groupe sélectionné la prend seule, et Suppr ne retire qu'elle — ; la barre, le dock,
#    R6, R7 et N24 sur le texte en saisie ; un groupe tourné qui porte un texte tourné, où le curseur,
#    les caractères spéciaux de N22 et le trait de N25 tombent là où le modèle met le texte ; les
#    flèches et l'alignement d'un membre dans son groupe, groupe tourné compris ; le redimensionnement
#    réservé ; couper et recoller un membre ; le glissé ; le double-clic et le groupe imbriqué ; la découpe.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
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

# ------------------------------------------------------------------------ 1. les sources

python3 - "$racine" <<'PY'
import pathlib, re, sys
import xml.etree.ElementTree as ET

racine = pathlib.Path(sys.argv[1])
VIEWS = "{using:MyPublisher.Views}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

def lire(chemin):
    return (racine / chemin).read_text(encoding="utf-8")

def code(texte):
    """Le texte sans ses commentaires « /// » et « // »."""
    return re.sub(r"//.*", "", texte)

# a. le modèle de vue.
partiel = code(lire("MyPublisher/ViewModels/MainWindowViewModel.MembreDuGroupe.cs"))
principal = code(lire("MyPublisher/ViewModels/MainWindowViewModel.cs"))
for membre in ("public TextFrame? TexteEnSaisie", "public Frame? MembreSeul",
               "public Frame? BlocVise => _texteEnSaisie ?? _membreSeul ?? SingleSelected;",
               "public void SuivreLeTexteEnSaisie(TextFrame? bloc)", "public void PrendreLeMembre(Frame? membre)"):
    exige(membre in partiel, f"le partiel du poste ne porte plus « {membre} ».")
exige("public Frame? SingleSelected => Selection.Count == 1 ? Selection[0] : null;" in principal,
      "le bloc seul de la sélection a changé : le poste n'en change pas le modèle.")
for methode, crochet in (("DeleteSelected", "SupprimerLeMembreVise()"), ("NudgeSelection", "DeplacerLeMembreVise(dx, dy, fine)"),
                         ("AlignEach", "AlignerLeMembreVise(shift, label)")):
    corps = re.search(r"void " + methode + r"\(.*?\n    \}", principal, re.S)
    exige(corps is not None and f"if ({crochet}) return;" in corps.group(0),
          f"{methode} ne laisse plus un membre visé à son groupe.")
print("    a. le modèle de vue : le texte en saisie, le membre pris seul, le bloc visé ; Suppr, les flèches et l'alignement d'abord dans le groupe")

# b. la barre et le dock.
for nom, attendus in (("MainWindow.axaml", 3), ("BarreContextuelle.axaml", 1)):
    vue = ET.parse(racine / "MyPublisher/Views" / nom).getroot()
    gardes = [e for e in vue.iter(VIEWS + "GabaritsGardes")]
    lies = [g for g in gardes if g.get("Bloc") == "{Binding BlocVise}"]
    exige(len(lies) == attendus == len(gardes), f"{nom} : {len(lies)} réglages gardés liés au bloc visé sur {len(gardes)}, {attendus} attendu(s).")
    if nom == "MainWindow.axaml":
        exige(any(g.get("IsEnabled") == "{Binding PositionReglable}" for g in lies),
              "« Position et taille » se règle pour un membre de groupe.")
    else:
        exige(any(g.get(X + "Name") == "ReglagesDuBloc" for g in lies), "la barre : « ReglagesDuBloc » ne suit pas le bloc visé.")
for chemin in ("MyPublisher/ViewModels/MainWindowViewModel.MiseEnFormeVisee.cs", "MyPublisher/ViewModels/MainWindowViewModel.ParagrapheVise.cs"):
    texte = code(lire(chemin))
    exige("public TextFrame? Bloc => _vm.BlocVise as TextFrame;" in texte and "SingleSelected" not in texte,
          f"{pathlib.Path(chemin).name} ne vise pas le bloc visé.")
print("    b. la barre, les trois sections du dock, R6, R7 et N24 suivent le bloc visé ; la position d'un membre ne se règle pas")

# c. la planche.
planche = "".join(code(lire(f"MyPublisher/Views/{f}")) for f in
                  ("PageCanvas.axaml.cs", "PageCanvas.Saisie.cs", "PageCanvas.MembreDuGroupe.cs"))
exige("Ungroup" not in planche, "la planche dissocie un groupe.")
saisie = code(lire("MyPublisher/Views/PageCanvas.Saisie.cs"))
exige("private bool DansLeTexte(Frame bloc, Point page) => DansLeTexteDuChemin(CheminSous(bloc, page), page);" in saisie,
      "le texte d'un bloc ne se lit plus à travers ses groupes.")
exige(re.search(r"var racine = chemin\[0\];\s*if \(_vm\.Selection\.Count != 1 \|\| !_vm\.Selection\.Contains\(racine\)\) _vm\.SelectOnly\(racine\);\s*BeginTextEdit\(texte, curseur\);", saisie),
      "l'entrée dans le texte ne prend plus le groupe, racine du chemin.")
print("    c. la planche : le clic traverse le groupe et prend le groupe ; rien n'y dissocie")

# d. la découpe.
gabarit = lire("MyPublisher/Views/PageCanvas.axaml")
groupe = re.search(r'<DataTemplate DataType="models:GroupFrame">(.*?)</DataTemplate>', gabarit, re.S)
exige(groupe is not None and '<Binding Path="Children.Count"/>' in groupe.group(1)
      and '<Binding Path="$self.(views:PageCanvas.RevisionDeLaDecoupe)"/>' in groupe.group(1),
      "la découpe d'un groupe ne suit plus ses blocs.")
print("    d. la découpe de Q5 suit les blocs du groupe et le filet d'un membre visé")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources"

# ------------------------------------------------------------------------ 2. les tests

attendus=12
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo --filter "poste=N26" > "$travail/tests.txt" 2>&1; then
    grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests du poste (« poste=N26 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N26 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. la vraie fenêtre : $joues tests (le 3ᵉ caractère, curseur à 2, le groupe sélectionné, un pas ; la barre, le dock, R6, R7 et N24 ; les rotations imbriquées, N22 et N25 ; l'image prise seule et Suppr ; couper et recoller ; les flèches et l'alignement ; le redimensionnement réservé ; le glissé ; le double-clic et l'imbrication ; la découpe)"

echo "N26 : le texte d'un groupe se tape dans le groupe, qui reste la sélection ; un membre se prend seul, et se supprime, se déplace et s'aligne dans son groupe."
