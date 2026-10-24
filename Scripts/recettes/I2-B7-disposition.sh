#!/usr/bin/env bash
#
# Recette des postes I2 et B7 — la disposition de la fenêtre, et les panneaux qui se
# replient.
#
# I2 refait la fenêtre sur la maquette « Fenêtre principale — bloc de texte sélectionné » :
# barre de menus, barre principale à icônes, place de la barre contextuelle, rail d'outils,
# panneau Pages, dock à quatre onglets, barre d'état. B7 y est livré : Ctrl+F1 replie les
# deux panneaux latéraux. Deux temps, comme la recette de A4.
#
#   1. LES SOURCES, lues par un script.
#      a. B7 : Ctrl+F1 est déclaré une fois, au menu Fenêtre comme au clavier, n'est pris ni
#         par OnKeyDown ni par un autre geste, et figure au tableau des raccourcis du README
#         — comme Ctrl+G et Ctrl+I, que le menu Texte reçoit. Le doublon d'un geste se
#         tait : Avalonia joue le premier et laisse l'autre inerte.
#      b. Les points d'accroche que les postes suivants attendent sont posés, chacun sous
#         son nom : la barre contextuelle (I3) dans son propre fichier, au focus de la
#         transaction ; l'entrée du panneau Pages (I5) dans le sien ; « Page entière » et
#         « Conserver les proportions » (I6), liés au modèle de vue ; la place de l'œil (I8)
#         et celle de l'alerte d'excédent (I9).
#      c. Ce que la phase ne fait pas n'est pas dessiné, pas même grisé : ni Habillage, ni
#         Effets, ni « Ajuster le texte », ni Gabarits, ni pipette, ni graisse, ni style de
#         paragraphe nommé, ni nuancier — confirmé par le propriétaire le 24 octobre 2026.
#         Seul ce qui s'affiche compte : un commentaire qui les nomme ne dessine rien.
#      d. Les noms que tests, recettes et banc cherchent survivent (règle 6), et le dock a
#         ses quatre onglets, dans l'ordre de la maquette.
#      e. Les vues que le poste écrit sont dans la liste des vues de la phase de la recette
#         I1 — qui vérifie, elle, qu'aucune n'écrit une couleur de la maquette en dur — et
#         ce que le poste ajoute au modèle de vue est dans son fichier partiel (règle 10).
#   2. LES TESTS d'interface « poste=I2 », sur la vraie fenêtre à 1400 × 900 : chaque zone à
#      sa place et à sa couleur ; la planche qui ne bouge pas entre la pression qui
#      sélectionne, le relâchement et Échap (règle 3) ; chaque champ du dock, quel que
#      soit son onglet, en un seul pas d'annulation (règle 2) ; les longueurs avec leur
#      unité ; « Blocs de la page » l'avant en tête ; Ctrl+F1 ; Annuler et Rétablir grisés ;
#      le menu Texte ; les sections fermées retenues ; la barre d'état.
#
# Le glissé dans « Blocs de la page », réécrit à l'envers, se joue sous la recette de A4,
# qui l'éprouvait déjà.
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
FENETRE = VUES / "MainWindow.axaml"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
A = "{https://github.com/avaloniaui}"

echecs = []
def refuser(message):
    echecs.append(message)

fenetre_texte = FENETRE.read_text(encoding="utf-8")
fenetre = ET.fromstring(fenetre_texte)
code = (VUES / "MainWindow.axaml.cs").read_text(encoding="utf-8")
readme = (racine / "README.md").read_text(encoding="utf-8")

def nom(e):
    return e.get(X + "Name") or e.get("Name")

def par_nom(racine_xml, valeur):
    return [e for e in racine_xml.iter() if nom(e) == valeur]

# ------------------------------------------------------------------ a. B7 et les raccourcis

liaisons = [e.get("Gesture") for e in fenetre.iter(A + "KeyBinding")]
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
if tableau is None:
    refuser("README : la section « ## Raccourcis » a disparu.")
    tableau = ""
else:
    tableau = tableau.group(1)

for geste, commande in (("Ctrl+F1", "ReplierLesPanneauxCommand"), ("Ctrl+G", "BasculerGrasCommand"),
                        ("Ctrl+I", "BasculerItaliqueCommand")):
    declares = [e for e in fenetre.iter(A + "KeyBinding") if e.get("Gesture") == geste]
    if len(declares) != 1:
        refuser(f"raccourcis : « {geste} » est déclaré {len(declares)} fois en KeyBinding (une attendue).")
    elif declares[0].get("Command") != "{Binding " + commande + "}":
        refuser(f"raccourcis : « {geste} » ne mène pas à {commande}.")
    menus = [e for e in fenetre.iter(A + "MenuItem") if e.get("InputGesture") == geste]
    if len(menus) != 1 or menus[0].get("Command") != "{Binding " + commande + "}":
        refuser(f"raccourcis : le menu ne montre pas « {geste} » une fois, sur {commande}.")
    # Le tableau du README, et lui seul : une mention au fil du texte ne s'y substitue pas.
    if not re.search(r"`" + re.escape(geste) + r"`", tableau):
        refuser(f"README : « {geste} » n'est pas au tableau des raccourcis.")
    lettre = geste.split("+")[1]
    if re.search(r"case Key\." + lettre + r"\b", code):
        refuser(f"raccourcis : OnKeyDown prend la touche {lettre}, que « {geste} » déclare aussi (règle 9).")
doublons = sorted({g for g in liaisons if liaisons.count(g) > 1})
if doublons:
    refuser("raccourcis : gestes déclarés deux fois : " + ", ".join(doublons))
# Le menu Fenêtre : les deux panneaux, et le repli.
fenetres = [e for e in fenetre.iter(A + "MenuItem") if e.get("Header") == "Fe_nêtre"]
if len(fenetres) != 1:
    refuser("menus : le menu Fenêtre manque.")
else:
    entrees = [(e.get("Header"), e.get("IsChecked") or e.get("Command")) for e in fenetres[0] if e.tag == A + "MenuItem"]
    attendues = [("Panneau _Pages", "{Binding PanneauPagesVisible}"), ("_Dock", "{Binding DockVisible}"),
                 ("_Replier les panneaux", "{Binding ReplierLesPanneauxCommand}")]
    if entrees != attendues:
        refuser(f"menus : le menu Fenêtre porte {entrees}, attendu {attendues}.")
print("    a. Ctrl+F1, Ctrl+G et Ctrl+I déclarés une fois, au menu et au tableau du README ; menu Fenêtre")

# ---------------------------------------------------------- b. les points d'accroche

def exige(condition, message):
    if not condition:
        refuser("accroches : " + message)

barre = VUES / "BarreContextuelle.axaml"
exige(barre.exists() and "<UserControl" in barre.read_text(encoding="utf-8"),
      "BarreContextuelle.axaml, contrôle à part pour I3, manque.")
hotes = [e for e in fenetre.iter(A + "{using:MyPublisher.Views}BarreContextuelle")] or \
        [e for e in fenetre.iter() if e.tag.endswith("}BarreContextuelle")]
exige(len(hotes) == 1, f"la fenêtre héberge {len(hotes)} barre(s) contextuelle(s), une attendue.")
if hotes:
    exige(hotes[0].get("GotFocus") == "OnPropertyPanelGotFocus" and hotes[0].get("LostFocus") == "OnPropertyPanelLostFocus",
          "la barre contextuelle n'ouvre ni ne ferme la transaction au focus (règle 2).")
    exige(hotes[0].get("IsVisible") is None, "la barre contextuelle se masque : la planche bougerait (règle 3).")
entree = VUES / "EntreeDePage.axaml"
exige(entree.exists(), "EntreeDePage.axaml, l'entrée du panneau Pages que I5 change en vignette, manque.")
exige(len([e for e in fenetre.iter() if e.tag.endswith("}EntreeDePage")]) == 1,
      "le panneau Pages ne présente pas ses pages par EntreeDePage.")
exige([e.get("Command") for e in par_nom(fenetre, "BoutonPageEntiere")] == ["{Binding PageEntiereCommand}"],
      "« Page entière » n'est pas posé sur PageEntiereCommand (I6).")
exige([e.get("IsChecked") for e in par_nom(fenetre, "ConserverLesProportions")]
      == ["{Binding #Root.((vm:MainWindowViewModel)DataContext).ConserverLesProportions}"],
      "« Conserver les proportions » n'est pas lié au modèle de vue (I6).")
exige(len(par_nom(fenetre, "PlaceDeLOeil")) == 1, "la place de l'œil (I8) n'est pas réservée.")
exige(len(par_nom(fenetre, "PlaceDeLAlerteDExcedent")) == 1, "la place de l'alerte d'excédent (I9) n'est pas réservée.")
print("    b. barre contextuelle et entrée de page dans leurs fichiers ; Page entière, proportions, œil, alerte")

# ---------------------------------------------- c. ce que la phase ne fait pas n'est pas dessiné

INTERDITS = ["Habillage", "Effets", "Ajuster le texte", "Gabarit", "Pipette", "Graisse", "Style de paragraphe",
             "Corps de texte", "Chaînage", "Nuancier", "Regular"]
for fichier in (FENETRE, barre, entree):
    if not fichier.exists():
        continue
    arbre = ET.fromstring(fichier.read_text(encoding="utf-8"))
    for e in arbre.iter():
        for attribut in ("Text", "Header", "Content", "{https://github.com/avaloniaui}ToolTip.Tip", "ToolTip.Tip"):
            valeur = e.get(attribut) or ""
            for mot in INTERDITS:
                if mot.lower() in valeur.lower():
                    refuser(f"dessin : {fichier.name} affiche « {valeur} » — {mot} n'est pas fait par la phase, "
                            f"et un contrôle sans moteur derrière est une commande morte.")
        if (e.text or "").strip():
            for mot in INTERDITS:
                if mot.lower() in e.text.lower():
                    refuser(f"dessin : {fichier.name} affiche « {e.text.strip()} ».")
print(f"    c. rien de ce que la phase ne fait pas n'est dessiné ({len(INTERDITS)} libellés cherchés)")

# ------------------------------------------------------------- d. les noms, les onglets

for n in ("ListeDesBlocs", "TraitDInsertion", "FormatDePage", "JeuDeCouleurs", "NombreDeColonnes",
          "RetraitPremiereLigne", "BoutonImporterTexte", "PositionDuBloc", "TailleDuBloc", "PageView"):
    exige(len(par_nom(fenetre, n)) == 1, f"« {n} » n'est plus dans la fenêtre, une fois (règle 6).")
menu_import = par_nom(fenetre, "MenuImporterTexte")
exige(len(menu_import) == 1 and menu_import[0].get("Header") == "Importer un te_xte…",
      "« MenuImporterTexte » a perdu son libellé « Importer un te_xte… » (règle 6).")
planche = (VUES / "PageCanvas.axaml").read_text(encoding="utf-8")
for n in ("PageRoot", "InputLayer", "Overlay", "TopRuler", "LeftRuler"):
    exige(re.search(r'(x:)?Name="' + n + '"', planche) is not None, f"« {n} » n'est plus dans la planche (règle 6).")
onglets = [e.get("Header") for e in fenetre.iter(A + "TabItem")]
exige(onglets == ["Propriétés", "Styles", "Couleurs", "Document"],
      f"le dock a les onglets {onglets}, et non Propriétés, Styles, Couleurs, Document.")
print("    d. les noms de la règle 6 survivent ; le dock a ses quatre onglets")

# ------------------------------------------------------------ e. les listes, le partiel

recette_i1 = (racine / "Scripts/recettes/I1-apparence.sh").read_text(encoding="utf-8")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
for vue in ("MyPublisher/Views/MainWindow.axaml", "MyPublisher/Views/BarreContextuelle.axaml",
            "MyPublisher/Views/EntreeDePage.axaml"):
    exige(vue in liste, f"{vue} n'est pas dans la liste des vues de la phase de la recette I1.")
partiel = racine / "MyPublisher/ViewModels/MainWindowViewModel.Disposition.cs"
exige(partiel.exists() and "public partial class MainWindowViewModel" in partiel.read_text(encoding="utf-8"),
      "ce que I2 ajoute au modèle de vue n'est pas dans son fichier partiel (règle 10).")
if partiel.exists():
    # Sans égard à la casse : « [ObservableProperty] » déclare le champ « _conserverLes… ».
    contenu = partiel.read_text(encoding="utf-8").lower()
    for membre in ("ReplierLesPanneaux", "ConserverLesProportions", "PageEntiere", "Pastille"):
        exige(membre.lower() in contenu, f"{membre} n'est pas dans le partiel du poste.")
print("    e. trois vues inscrites à la liste de la recette I1 ; le partiel du modèle de vue")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : raccourcis, points d'accroche, rien de mort, noms et onglets"

# ------------------------------------------------------------------------ 2. les tests

attendus=14
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=I2" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests d'interface sous « poste=I2 » ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=I2 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. la fenêtre à 1400 × 900 : zones, planche immobile, un pas par champ et par onglet, unités, Ctrl+F1, menus, sections, barre d'état ($joues tests)"

echo "I2 : la fenêtre a la disposition de la maquette, et ses points d'accroche."
echo "B7 : Ctrl+F1 replie les deux panneaux, et le second les rend."
