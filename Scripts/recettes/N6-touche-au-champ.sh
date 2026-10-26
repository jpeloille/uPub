#!/usr/bin/env bash
#
# Recette du poste N6 — les raccourcis de la fenêtre laissent la touche au champ (fiche 23,
# a13, d2, d21).
#
# Les liaisons de la fenêtre passent avant le contrôle qui a le focus, et ne lui rendent la
# touche que si leur commande ne peut pas s'exécuter. Tant qu'un champ de texte a le clavier —
# la saisie d'un bloc sur la page, un champ du dock ou de la barre —, les commandes d'objet se
# taisent : tout sélectionner, dupliquer, grouper, dissocier, premier plan, arrière-plan. Leur
# touche revient au champ. Alt+flèche, en saisie, déplace le bloc dans un pas à lui. Deux temps.
#
# 1. LES SOURCES, lues par un script :
#
#      a. les six commandes ont un CanExecute qui lit le focus de la fenêtre
#         (ConsumesTyping), en plus de la sélection qu'elles lisaient ; Annuler et Rétablir
#         n'en ont pas — c'est l'historique du document qui annule (Q24) ;
#      b. la fenêtre prête son prédicat au modèle de vue quand elle le prend, le reprend quand
#         elle le lâche, et lève LeClavierAChange à chaque prise et perte du focus, traitées
#         comprises : les boutons qui portent ces commandes suivent, et la notification touche
#         les six ;
#      c. aucun second Ctrl+A, ni sur un champ ni ailleurs : la touche revient au champ par le
#         CanExecute, non par une liaison de plus ; OnKeyDown ne prend ni A ni D sous
#         Contrôle (règle 9) ; la cible du juge « PageView.EndTextEdit(); » reste, une fois ;
#      d. ce que le poste ajoute au modèle de vue est dans son fichier partiel (règle 10) ;
#         Alt+flèche est pris en tunnel, avant la zone de saisie, et passe par UnGesteUnPas ;
#      e. le README le dit, au tableau des raccourcis.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au clavier
#    (« ToucheAuChampTests », trait « poste=N6 ») — les clauses de la recette du plan :
#
#      · en saisie, Ctrl+A sélectionne le texte, la saisie reste ouverte et la sélection des
#        blocs ne change pas ; de même dans Contenu, Nom, le champ X, et une marge de l'onglet
#        Document, où la valeur tapée ensuite fait son pas (le relevé de Q24) ;
#      · Ctrl+D, Ctrl+Maj+F et B ne font rien en saisie et n'empilent rien, la frappe seule a
#        son pas ; Ctrl+Maj+G et H ne font rien dans un champ ;
#      · les boutons Premier plan et Arrière-plan se grisent tant qu'un champ a le clavier, et se
#        rallument ; un menu ouvert prend le focus, et rallume ses entrées ;
#      · un champ quitté rend Ctrl+D et Ctrl+A aux blocs ;
#      · Alt+→ déplace le bloc de 3 pt, en un pas, et la saisie continue, le curseur en place ;
#        Alt+Maj+↓ de 0,5 pt.
#
#    Puis les voisins : hors saisie, tout se joue comme avant — les tests de A4 —, et un
#    raccourci de texte pendant la saisie d'un champ fait encore son propre pas (I2, I3).
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

# ---------------------------------------------------------------------- 1. les sources

python3 - "$racine" <<'PY'
import pathlib, re, sys

racine = pathlib.Path(sys.argv[1])
VM = racine / "MyPublisher/ViewModels"
VUES = racine / "MyPublisher/Views"

echecs = []
def exige(condition, message):
    if not condition:
        echecs.append(message)

def corps(source, signature):
    """Le corps d'une méthode, de son accolade ouvrante à la fermante de même retrait."""
    trouve = re.search(re.escape(signature) + r"\s*\n(\s*)\{\n(.*?)\n\1\}", source, re.S)
    return trouve.group(2) if trouve else None

modele = (VM / "MainWindowViewModel.cs").read_text(encoding="utf-8")
chemin = VM / "MainWindowViewModel.Clavier.cs"
partiel = chemin.read_text(encoding="utf-8") if chemin.exists() else ""
fenetre = (VUES / "MainWindow.axaml.cs").read_text(encoding="utf-8")

# ------------------------------------------------- a. les six commandes lisent le focus

commandes = {
    "SelectAll": ("PeutToutSelectionner", None),
    "DuplicateSelected": ("PeutAgirSurLaSelection", "HasSelection"),
    "BringToFront": ("PeutAgirSurLaSelection", "HasSelection"),
    "SendToBack": ("PeutAgirSurLaSelection", "HasSelection"),
    "GroupSelected": ("PeutGrouper", "CanGroup"),
    "UngroupSelected": ("PeutDissocier", "CanUngroup"),
}
for commande, (garde, selection) in commandes.items():
    exige(re.search(rf"\[RelayCommand\(CanExecute = nameof\({garde}\)\)\]\s*private void {commande}\(\)", modele),
          f"« {commande} » n'a pas « {garde} » pour CanExecute.")
    definition = re.search(rf"private bool {garde}\(\) => (.*?);", partiel)
    exige(definition is not None, f"« {garde} » n'est pas dans le partiel du poste.")
    if definition:
        exige("!ConsumesTyping" in definition.group(1), f"« {garde} » ne lit pas le focus (ConsumesTyping).")
        if selection:
            exige(selection in definition.group(1), f"« {garde} » ne lit plus « {selection} ».")
for commande in ("Undo", "Redo"):
    exige(re.search(rf"\[RelayCommand\]\s*private void {commande}\(\)", modele),
          f"« {commande} » a pris une condition : Annuler et Rétablir ne changent pas de sens.")
print("    a. tout sélectionner, dupliquer, grouper, dissocier, premier plan et arrière-plan lisent le focus ; Annuler et Rétablir, non")

# ------------------------------------------- b. la fenêtre prête son focus, et le suit

exige(fenetre.count("_vm.SuivreLeClavier(() => ConsumesTyping(FocusManager?.GetFocusedElement()));") == 1,
      "la fenêtre ne prête pas son prédicat au modèle de vue qu'elle prend.")
exige(fenetre.count("_vm.SuivreLeClavier(null);") == 1,
      "la fenêtre ne reprend pas son prédicat au modèle de vue qu'elle lâche.")
for evenement in ("GotFocusEvent", "LostFocusEvent"):
    exige(re.search(rf"AddHandler\({evenement}, \(_, _\) => Vm\?\.LeClavierAChange\(\), RoutingStrategies\.Bubble, handledEventsToo: true\);",
                    fenetre), f"la fenêtre ne lève pas LeClavierAChange à « {evenement} », traités compris.")
notifie = corps(partiel, "public void LeClavierAChange()") or ""
for commande in commandes:
    exige(f"{commande}Command.NotifyCanExecuteChanged();" in notifie,
          f"LeClavierAChange ne notifie pas « {commande}Command » : ce qui la porte ne suivrait pas le focus.")
print("    b. la fenêtre prête son focus au modèle de vue, et lève LeClavierAChange à chaque prise et perte du focus")

# ------------------------------------------------------- c. aucun second Ctrl+A, ni D

axaml = "\n".join(p.read_text(encoding="utf-8") for p in VUES.glob("*.axaml"))
for geste in ("Ctrl+A", "Ctrl+D", "Ctrl+Shift+G", "Ctrl+Shift+H", "Ctrl+Shift+F", "Ctrl+Shift+B"):
    n = len(re.findall(rf'<KeyBinding\s+Gesture="{re.escape(geste)}"', axaml))
    exige(n == 1, f"« {geste} » est déclaré {n} fois en KeyBinding dans les vues, une attendue.")
cle = corps(fenetre, "protected override void OnKeyDown(KeyEventArgs e)") or ""
sous_controle = cle.split("if (e.KeyModifiers == KeyModifiers.None)")[0]
for touche in ("A", "D"):
    exige(not re.search(rf"case Key\.{touche}\b", sous_controle), f"OnKeyDown prend {touche} sous Contrôle (règle 9).")
exige(fenetre.count("PageView.EndTextEdit();") == 1, "la cible du juge « PageView.EndTextEdit(); » n'est plus là une fois.")
print("    c. une liaison par geste, aucune touche doublée dans OnKeyDown, la cible du juge en place")

# --------------------------------------------- d. le partiel, et Alt+flèche en tunnel

exige(chemin.exists() and "public partial class MainWindowViewModel" in partiel,
      "ce que N6 ajoute au modèle de vue n'est pas dans son partiel, MainWindowViewModel.Clavier.cs (règle 10).")
for membre in ("public bool ConsumesTyping => _unChampALeClavier();", "public void SuivreLeClavier(",
               "public void LeClavierAChange()", "public void DeplacerLeBlocEnSaisie("):
    exige(membre in partiel, f"« {membre.strip('(')} » n'est pas dans le partiel du poste.")
    exige(membre not in modele, f"« {membre.strip('(')} » est dans MainWindowViewModel.cs, hors du partiel.")
exige("UnGesteUnPas(() => NudgeSelection(dx, dy, fine));" in partiel,
      "Alt+flèche ne passe plus par UnGesteUnPas : le déplacement se fondrait dans la frappe.")
exige("AddHandler(KeyDownEvent, DeplacerLeBlocEnSaisie, RoutingStrategies.Tunnel);" in fenetre,
      "Alt+flèche n'est pas pris en tunnel, avant la zone de saisie qui y déplacerait son curseur.")
alt = corps(fenetre, "private void DeplacerLeBlocEnSaisie(object? sender, KeyEventArgs e)") or ""
exige("PageView.IsEditingText" in alt and "PageView.IsVisualAncestorOf(champ)" in alt,
      "Alt+flèche ne se limite pas à la saisie sur la page, sa zone ayant le focus.")
print("    d. le partiel du poste ; Alt+flèche en tunnel, en saisie seulement, par UnGesteUnPas")

# ------------------------------------------------------------------- e. le README

readme = (racine / "README.md").read_text(encoding="utf-8")
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
exige(tableau is not None, "le README n'a plus de section « ## Raccourcis ».")
if tableau:
    exige("| `Alt` + flèches, dans la saisie d'un bloc |" in tableau.group(1),
          "le tableau des raccourcis ne dit pas Alt+flèches en saisie.")
    exige("les raccourcis d'objet se taisent et leur touche revient au champ" in tableau.group(1),
          "la section des raccourcis ne dit pas que la touche revient au champ.")
print("    e. le README dit la règle et Alt+flèches")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : six commandes qui lisent le focus, la fenêtre qui le prête et le suit, une liaison par geste, le partiel, le README"

# ------------------------------------------------------------------------ 2. les gestes

jouer() {
    local filtre="$1" attendus="$2" quoi="$3"
    if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
            --nologo -v q --filter "$filtre" > "$travail/tests.txt" 2>&1; then
        sed 's/^/    /' "$travail/tests.txt" >&2
        echo "ÉCHEC : les tests d'interface sous « $filtre » ne passent pas." >&2
        exit 1
    fi
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
    if [ "${joues:-0}" -lt "$attendus" ]; then
        echo "ÉCHEC : $attendus tests attendus sous « $filtre », ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
    echo "  $quoi ($joues tests)"
}

jouer "poste=N6" 8 "2. la touche au champ, sur la vraie fenêtre : Ctrl+A dans la saisie et les champs, les commandes d'objet muettes, les boutons qui suivent, Alt+flèche en un pas"

# Les voisins : hors des champs, tout se joue comme avant (A4) ; un raccourci de texte pendant
# la saisie d'un champ fait encore son propre pas (I2, I3).
jouer "poste=A4" 5 "   les voisins : la superposition au clavier et à la souris, comme avant (A4)"
voisins="FullyQualifiedName~DispositionTests.Ctrl_G_pendant_une_saisie_du_dock_fait_son_propre_pas"
voisins+="|FullyQualifiedName~BarreContextuelleTests.Ctrl_U_pendant_une_saisie_de_la_barre_fait_son_propre_pas"
jouer "$voisins" 2 "   les voisins : Ctrl+G et Ctrl+U pendant la saisie d'un champ, un pas chacun (I2, I3)"

echo "N6 : tant qu'un champ a le clavier, les raccourcis d'objet lui laissent la touche, et Alt+flèche déplace le bloc en saisie, dans un pas à lui."
