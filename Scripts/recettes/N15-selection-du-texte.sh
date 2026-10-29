#!/usr/bin/env bash
#
# Recette du poste N15 — l'application connaît la sélection du texte (fiche 23, b1 et b4).
#
# CaretIndex et SelectionStart n'apparaissaient que dans les tests : seule la planche tenait le
# champ de la saisie, et le modèle de vue ne savait pas ce qui était sélectionné dans le texte.
# R6, R7, N20 et N21 en ont besoin. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. « SelectionDuTexte » — le bloc ou la case, le début, la fin, le curseur — est au
#         modèle de vue, dans son fichier partiel de la saisie (règle 10), avec la propriété
#         qui la porte et « ParagraphesDeLaSelection », comptés par « MiseEnForme.Paragraphes »,
#         c'est-à-dire par « Recalage.ParagrapheDe » ;
#      b. la planche la tient depuis PageCanvas.Saisie.cs, et non depuis son fichier principal
#         (règle 16) : elle suit le champ de la saisie au focus, ne dit plus rien d'un champ
#         sans focus — le champ oublie sa sélection en le perdant —, et la vide quand le champ
#         se cache, à la sortie de la saisie.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au pointeur
#    et au clavier synthétisés (trait « poste=N15 » : SelectionDuTexteTests) — la recette du
#    plan, puis ses bords :
#
#      · un glissé sur les caractères 4 à 7 donne (4, 7) ; Maj+→ donne (4, 8) ; une frappe
#        donne (5, 5) ;
#      · le focus pris par le champ Corps ne change rien, alors que le champ de la saisie a
#        réduit sa sélection à un curseur ; Échap dans le corps non plus ;
#      · Échap vide la sélection ; un clic hors du bloc, ou une autre sélection des blocs,
#        aussi ;
#      · le glissé à rebours donne la même plage, le curseur au début ; le clic d'entrée et
#        les flèches donnent le curseur seul, dans le bloc visé ; le texte raccourci au dock
#        ramène la sélection dans ses bornes ; les paragraphes de la sélection sont comptés
#        comme les écarts ; la forme rangée (début avant fin, dans les bornes), et une case
#        sans paragraphes.
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

vm = lire("MyPublisher/ViewModels/MainWindowViewModel.Saisie.cs")
saisie = lire("MyPublisher/Views/PageCanvas.Saisie.cs")
planche = lire("MyPublisher/Views/PageCanvas.axaml.cs")

# a. le modèle de vue.
exige("public sealed record SelectionDuTexte(Frame Bloc, TableCell? Case, int Debut, int Fin, int Curseur)" in vm,
      "« SelectionDuTexte » n'est plus le bloc ou la case, le début, la fin et le curseur.")
exige("public SelectionDuTexte? SelectionDuTexte => _selectionDuTexte;" in vm,
      "le modèle de vue ne porte plus la sélection du texte.")
exige(re.search(r"public Plage\? ParagraphesDeLaSelection =>.*?MiseEnForme\.Paragraphes\(", vm, re.S) is not None,
      "« ParagraphesDeLaSelection » ne compte plus les paragraphes par MiseEnForme.Paragraphes.")
mise_en_forme = lire("MyPublisher.Core/Models/MiseEnForme.cs")
paragraphes = re.search(r"public static Plage Paragraphes\(string texte, Plage caracteres\)(.*?)\n    }\n", mise_en_forme, re.S)
exige(paragraphes is not None and "Recalage.ParagrapheDe(" in paragraphes.group(1),
      "MiseEnForme.Paragraphes ne compte plus par Recalage.ParagrapheDe.")
print("    a. SelectionDuTexte au modèle de vue, et ses paragraphes comptés par Recalage.ParagrapheDe")

# b. la planche, depuis son partiel de la saisie.
exige("SuivreLaSelectionDuTexte(" not in planche,
      "le fichier principal de la planche tient la sélection du texte (règle 16 : PageCanvas.Saisie.cs).")
exige(saisie.count("_vm.SuivreLaSelectionDuTexte(SelectionDuTexte.De(_editing, null, zone.SelectionStart, zone.SelectionEnd,") == 1,
      "la planche ne dit plus la sélection du champ de la saisie, de l'ancre au curseur.")
exige(saisie.count("AddHandler(GotFocusEvent, (_, args) => SuivreLaZoneDeSaisie(args.Source as TextBox),") == 1,
      "la planche ne suit plus le champ de la saisie quand il prend le focus.")
garde = re.search(r"private void OnZoneSuivieChangee\(object\? sender, AvaloniaPropertyChangedEventArgs e\)(.*?)\n    }\n", saisie, re.S)
exige(garde is not None, "OnZoneSuivieChangee n'est plus dans PageCanvas.Saisie.cs.")
if garde:
    corps = garde.group(1)
    exige("if (e.Property == IsVisibleProperty && !zone.IsVisible)" in corps and "OublierLaSelectionDuTexte(zone);" in corps,
          "la sélection du texte n'est plus vidée quand le champ se cache, à la sortie de la saisie.")
    exige(corps.count("if (!zone.IsFocused)") == 1 and corps.find("if (!zone.IsFocused)") < corps.find("DireLaSelectionDuTexte(zone);"),
          "un champ sans focus dit encore sa sélection : la barre qui prend le focus la ferait oublier.")
print("    b. la planche la tient depuis PageCanvas.Saisie.cs : au focus, jamais sans lui, vidée à la sortie")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le modèle de vue, ses paragraphes, la planche"

# ------------------------------------------------------------------------ 2. les gestes

attendus=8
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo --filter "poste=N15" > "$travail/tests.txt" 2>&1; then
    grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les gestes de la sélection du texte (« poste=N15 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N15 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les gestes sur la vraie fenêtre : $joues tests (le glissé, Maj+→, la frappe, le corps, Échap, les bords)"

echo "N15 : le modèle de vue connaît la sélection du texte en saisie — le bloc, le début, la fin, le curseur, ses paragraphes —, la garde quand la barre prend le focus, et la perd à la sortie."
