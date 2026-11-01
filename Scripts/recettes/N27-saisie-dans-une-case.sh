#!/usr/bin/env bash
#
# Recette du poste N27 — taper dans une case, sur la page (fiche 23, g2 à g4 ; spécifié le 25 octobre
# 2026). Revient sur un choix écrit en C9 : « une case ne se choisit pas sur la page ». On n'y saisissait
# qu'au dock, dans un champ de 54 px ; Publisher y tape au clic, et Tab passe de case en case. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le choix : une case n'existe qu'à la lecture — « CellFrames » reconstruit ses blocs à chaque
#         changement, le parti de C9 tient — : la saisie d'une case passe par un champ unique hors de
#         PageRoot, posé dans l'Overlay, qui écrit « TableCell.Saisie », comme le champ du dock ; rien
#         ne garde les blocs des cases d'une lecture à l'autre ;
#      b. le modèle de vue : la case en saisie au partiel du poste (règle 10), la ligne ajoutée en un
#         pas ; la sélection du texte porte la case ;
#      c. la planche : le clic dans une case passe par l'entrée dans le texte (N12, N26), le clavier
#         des cases avant celui des lignes composées (N29), la saisie refermée oublie la case ; la
#         recherche (N20) et la vérification (N25) entrent dans la case ;
#      d. le README dit les touches des cases, dans le sous-tableau Texte.
#
# 2. LES TESTS, sous le trait « poste=N27 », dans la vraie fenêtre (SaisieDansUneCaseTests) : la recette
#    du plan — un clic dans la 2ᵉ case d'une bande, « 12 », le PDF porte « 12 » dans la case, un pas,
#    le tableau restant la sélection, Échap qui le rend — ; Tab, Maj+Tab, puis la ligne ajoutée en un
#    pas ; un pas par case et Ctrl+Tab ; Ctrl+Z dans la transaction de la saisie (Q24, Q25) ; les
#    flèches au bord du texte ; la page et le dock qui se suivent ; la barre, le dock et le menu du
#    texte (A12) pendant la saisie d'une case, Ctrl+G qui met le tableau en gras ; un tableau tourné,
#    où le curseur, la sélection, les caractères spéciaux de N22 et le trait de N25 tombent sur la
#    case ; la recherche (N20) et la vérification (N25) qui entrent dans la case ; la case d'un
#    tableau tourné dans un groupe tourné ; le bord du tableau qui le prend et le déplace.
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

racine = pathlib.Path(sys.argv[1])
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

def lire(chemin):
    return (racine / chemin).read_text(encoding="utf-8")

def code(texte):
    """Le texte sans ses commentaires « /// » et « // »."""
    return re.sub(r"//.*", "", texte)

# a. le choix.
case = code(lire("MyPublisher/Views/PageCanvas.SaisieDansUneCase.cs"))
exige("Overlay.Children.Add(champ);" in case, "le champ de la case n'est plus posé dans l'Overlay.")
exige("PageRoot" not in case, "le champ de la case touche à PageRoot (règle 4).")
exige(re.search(r'champ\.Bind\(TextBox\.TextProperty, new Binding\(nameof\(TableCell\.Saisie\)\) \{ Source = case_, Mode = BindingMode\.TwoWay \}\);', case),
      "le champ de la case n'écrit plus « TableCell.Saisie ».")
frame = code(lire("MyPublisher.Core/Models/Frame.cs"))
corps = re.search(r"public IReadOnlyList<Frame> CellFrames\s*\{\s*get\s*\{(.*?)\n        \}", frame, re.S)
exige(corps is not None and "var cases = new List<Frame>(Cells.Count);" in corps.group(1) and "_cache" not in frame,
      "les blocs des cases ne sont plus reconstruits à chaque lecture : le parti de C9 a changé.")
gabarit = lire("MyPublisher/Views/PageCanvas.axaml")
exige(gabarit.count('Text="{Binding Saisie, Mode=TwoWay}"') == 1,
      "le champ du gabarit du bloc de texte a changé, ou il y en a un second sous PageRoot.")
print("    a. le choix : un champ unique hors de PageRoot, qui écrit TableCell.Saisie ; les cases reconstruites à chaque lecture (C9)")

# b. le modèle de vue.
partiel = code(lire("MyPublisher/ViewModels/MainWindowViewModel.SaisieDansUneCase.cs"))
for membre in ("public TableCell? CaseEnSaisie", "public void SuivreLaCaseEnSaisie(TableFrame? tableau, TableCell? case_)",
               "UnGesteUnPas(() => premiere = tableau.AjouterUneLigne());"):
    exige(membre in partiel, f"le partiel du poste ne porte plus « {membre} ».")
exige("SaisieDansUneCase" not in code(lire("MyPublisher/ViewModels/MainWindowViewModel.cs")),
      "le fichier principal du modèle de vue porte le poste (règle 10).")
print("    b. le modèle de vue : la case en saisie au partiel du poste, la ligne ajoutée en un pas")

# c. la planche.
saisie = code(lire("MyPublisher/Views/PageCanvas.Saisie.cs"))
exige("if (chemin[^1] is TableFrame) return EntrerDansLaCase(chemin, page, e);" in saisie,
      "le clic dans une case ne passe plus par l'entrée dans le texte.")
touche = re.search(r"private void ToucheDeLaSaisie\(.*?\n    \}", saisie, re.S)
exige(touche is not None and touche.group(0).find("ToucheDeLaCase(zone, e)") >= 0
      and touche.group(0).find("ToucheDeLaCase(zone, e)") < touche.group(0).find("ToucheDesLignes(zone, e)"),
      "les touches des cases ne passent plus avant les lignes composées (N29).")
exige("OublierLaCase();" in code(lire("MyPublisher/Views/PageCanvas.axaml.cs")),
      "la saisie refermée n'oublie plus la case.")
recherche = code(lire("MyPublisher/ViewModels/MainWindowViewModel.Rechercher.cs"))
exige("public bool EnSaisie => Bloc is not null || Case is not null;" in recherche,
      "la recherche n'entre plus dans les cases.")
print("    c. la planche : le clic, le clavier des cases avant celui des lignes, la case oubliée ; la recherche y entre")

# d. le README.
readme = lire("README.md")
texte = re.search(r"^### Texte$(.*?)^### ", readme, re.S | re.M)
exige(texte is not None and "Dans une case d'un tableau : `Tab` / `Maj+Tab`, `Ctrl+Tab`" in texte.group(1),
      "le sous-tableau Texte du README ne dit plus les touches des cases.")
print("    d. le README : les touches des cases, au sous-tableau Texte")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources"

# ------------------------------------------------------------------------ 2. les tests

attendus=11
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo --filter "poste=N27" > "$travail/tests.txt" 2>&1; then
    grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests du poste (« poste=N27 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N27 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. la vraie fenêtre : $joues tests (la 2ᵉ case, « 12 », le PDF, un pas, Échap ; Tab, Maj+Tab et la ligne ajoutée ; un pas par case et Ctrl+Tab ; Ctrl+Z ; les flèches au bord ; la page et le dock ; la barre, le dock et le menu ; le tableau tourné, N22 et N25 ; la recherche et F7 ; le tableau d'un groupe tourné ; le bord)"

echo "N27 : une case se tape sur la page, d'un clic, le tableau restant la sélection ; Tab passe de case en case et ajoute une ligne au bout ; la page et le dock se suivent."
