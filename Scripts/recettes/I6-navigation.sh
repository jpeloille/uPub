#!/usr/bin/env bash
#
# Recette du poste I6 — naviguer dans la planche, et garder les proportions : la main,
# la loupe, « Page entière », et la case « Conserver les proportions » que I2 avait posée
# sans geste. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le rail porte la main, deuxième, et la loupe, dernière, chacune liée à son outil
#         et sous le libellé de la maquette — « Main (Espace) », « Zoom (Z) » ;
#      b. « Z » et « Espace » se rangent avec les lettres seules de la fenêtre : dans le
#         « switch » des touches sans modificateur, APRÈS le renvoi des frappes à une
#         saisie, Espace écarté quand un bouton a le focus ; aucun des deux n'est un
#         KeyBinding ; le relâchement d'Espace rend l'outil d'avant. Le tableau des
#         raccourcis du README les dit, chacun une fois, avec Ctrl + molette, Alt + clic
#         et Maj qui inverse la case ;
#      c. les champs L et H du dock passent par le modèle de vue, un champ par côté — c'est
#         ce qui permet à la case d'entraîner l'autre côté — ; la case reste un état de la
#         session : aucun fichier du noyau ne la nomme ;
#      d. ce que le poste ajoute au modèle de vue est dans son fichier partiel (règle 10),
#         et la planche qu'il écrit est inscrite à la liste des vues de la phase de la
#         recette I1 — elle cite l'apparence, pour ses curseurs ;
#      e. « Page entière » n'est appelée que par sa commande : ni l'ouverture, ni un
#         changement de taille ne zooment d'office (règle 5).
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au
#    pointeur et au clavier synthétisés (« NavigationTests », trait « poste=I6 ») — ce sont
#    les quatre clauses de la recette du plan, et ce qui les entoure :
#
#      · Espace tenu et un glissé de 100 px décalent le défilement de 100 px, en partant
#        d'un bloc qu'aucun pixel ne déplace ; l'historique reste vide, le document non
#        modifié ; Espace relâché, le même glissé reprend le bloc. L'outil Main du rail
#        fait de même, main ouverte puis fermée ; un double-clic n'y ouvre pas la saisie, ni
#        une prise de la main suivie d'un clic au même point, Espace relâché ;
#        Échap le désarme. Espace ne tient pas la main dans un champ, ni sur un bouton ;
#      · un clic de la loupe en un point de la page : le même point de la page est sous le
#        pointeur avant et après, à 1 px près — deux clics en avant, un Alt + clic en
#        arrière ; Ctrl + molette de même, dans les deux sens, sur une page qui déborde ;
#      · « Page entière » sur un A4 et sur un A4 paysage : la page tient, sans défilement,
#        et l'un de ses côtés touche la zone, à la marge de la planche près ; une page
#        minuscule s'arrête à la borne du zoom. Rien de cela ne touche au document ;
#      · case cochée, un coin tiré de 20 mm garde le rapport à 0,01 près, en un pas
#        d'annulation, et la case ne rend pas le document « modifié » ; un côté tiré ne le
#        garde pas ; Maj fait l'inverse de la case ; la saisie de L ou de H garde le
#        rapport, en un pas, et décochée n'écrit que le côté saisi.
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
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

A = "{https://github.com/avaloniaui}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"

fenetre_axaml = (racine / "MyPublisher/Views/MainWindow.axaml").read_text(encoding="utf-8")
fenetre = ET.fromstring(fenetre_axaml)
code = (racine / "MyPublisher/Views/MainWindow.axaml.cs").read_text(encoding="utf-8")
readme = (racine / "README.md").read_text(encoding="utf-8")

# a. le rail : les outils dans l'ordre, la main deuxième, la loupe dernière.
rail = next((e for e in fenetre.iter() if e.get(X + "Name") == "RailDOutils"), None)
exige(rail is not None, "le rail d'outils (« RailDOutils ») n'est plus dans la fenêtre.")
if rail is not None:
    outils = [e for e in rail.iter() if e.tag in (A + "ToggleButton", A + "Button")]
    libelles = [e.get("ToolTip.Tip") for e in outils]
    exige(len(libelles) >= 2 and libelles[1] == "Main (Espace)",
          f"la main n'est pas le deuxième outil du rail : {libelles}")
    exige(libelles and libelles[-1] == "Zoom (Z)", f"la loupe n'est pas le dernier outil du rail : {libelles}")
    for nom, outil, icone in (("OutilMain", "Main", "IconeMain"), ("OutilZoom", "Zoom", "IconeLoupe")):
        bouton = [e for e in outils if e.get(X + "Name") == nom]
        exige(len(bouton) == 1, f"« {nom} » n'est pas dans le rail, une seule fois.")
        if len(bouton) == 1:
            exige(bouton[0].get("Content") == "{StaticResource %s}" % icone,
                  f"« {nom} » ne porte pas l'icône {icone} de l'apparence.")
            exige(f"ConverterParameter={outil}}}" in (bouton[0].get("IsChecked") or ""),
                  f"« {nom} » n'est pas lié à l'outil {outil}.")
print("    a. le rail : la main deuxième, la loupe dernière, chacune liée à son outil")

# b. les touches : dans le switch des touches seules, après le renvoi aux saisies.
corps = re.search(r"protected override void OnKeyDown\(KeyEventArgs e\)(.*?)\n    }\n", code, re.S)
exige(corps is not None, "OnKeyDown n'est plus dans MainWindow.axaml.cs.")
if corps:
    texte = corps.group(1)
    saisie = texte.find("ConsumesTyping(")
    seules = texte.find("if (e.KeyModifiers == KeyModifiers.None)")
    z = texte.find("case Key.Z: vm.ActiveTool = EditorTool.Zoom;")
    espace = texte.find("case Key.Space when FocusManager?.GetFocusedElement() is not Button:")
    exige(0 <= saisie < seules, "les touches seules ne viennent plus après le renvoi des frappes à une saisie.")
    exige(z > seules, "« Z » n'arme pas la loupe parmi les touches seules.")
    exige(espace > seules, "« Espace » ne tient pas la main parmi les touches seules, bouton écarté.")
    exige("PageView.MainTenue = true" in texte, "« Espace » ne dit plus à la planche que la main est tenue.")
relache = re.search(r"protected override void OnKeyUp\(KeyEventArgs e\)(.*?)\n    }\n", code, re.S)
exige(relache is not None and "if (e.Key == Key.Space) PageView.MainTenue = false;" in relache.group(1),
      "le relâchement d'Espace ne rend plus l'outil d'avant.")
gestes = re.findall(r'<KeyBinding\s+Gesture="([^"]+)"', fenetre_axaml)
exige(not [g for g in gestes if g in ("Space", "Z")], "Espace ou Z est déclaré en KeyBinding : il jouerait pendant une saisie.")

tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
exige(tableau is not None, "le README n'a plus de section « ## Raccourcis ».")
if tableau:
    lignes = [l for l in tableau.group(1).splitlines() if l.startswith("|")]
    def touches(ligne):
        return re.findall(r"`([^`]+)`", ligne.split("|")[1])
    toutes = [t for l in lignes for t in touches(l)]
    for touche in ("Z", "Espace"):
        exige(toutes.count(touche) == 1, f"« {touche} » figure {toutes.count(touche)} fois au tableau du README, et non une.")
    exige(any("molette" in l and "pointeur" in l for l in lignes),
          "le tableau du README ne dit pas que Ctrl + molette garde le point visé sous le pointeur.")
    exige(any("Alt" in l and "clic" in l and "loupe" in l.lower() for l in lignes),
          "le tableau du README ne dit pas Alt + clic de la loupe.")
    exige(any("Maj" in l and "Conserver les proportions" in l for l in lignes),
          "le tableau du README ne dit pas que Maj fait l'inverse de la case.")
print("    b. Z et Espace parmi les touches seules, hors saisie et hors bouton ; le README les dit, une fois chacun")

# c. les champs L et H, la case dans la session.
largeur = [e for e in fenetre.iter() if (e.get("Value") or "").startswith("{Binding #Root.((vm:MainWindowViewModel)DataContext).LargeurDuBloc")]
hauteur = [e for e in fenetre.iter() if (e.get("Value") or "").startswith("{Binding #Root.((vm:MainWindowViewModel)DataContext).HauteurDuBloc")]
exige(len(largeur) == 1 and len(hauteur) == 1, "L et H ne passent pas, chacun par un seul champ, par le modèle de vue.")
# Les gabarits du bloc — et non ceux d'une colonne ou d'une ligne de tableau, qui ont aussi
# leur largeur et leur hauteur.
gabarits = [t for t in fenetre.iter(A + "DataTemplate") if t.get("DataType") == "models:Frame"]
directs = [e for t in gabarits for e in t.iter() if re.match(r"\{Binding (Width|Height)[,}]", e.get("Value") or "")]
exige(gabarits and not directs, "un champ du dock écrit encore la largeur ou la hauteur droit au bloc, hors de la case.")
for fichier in (racine / "MyPublisher.Core").rglob("*.cs"):
    if "/obj/" in str(fichier) or "/bin/" in str(fichier):
        continue
    exige("ConserverLesProportions" not in fichier.read_text(encoding="utf-8"),
          f"{fichier.relative_to(racine)} nomme la case : elle est un état de la session, pas du document.")
print("    c. L et H passent par le modèle de vue, un champ chacun ; le noyau ignore la case")

# d. le partiel, la vue inscrite.
partiel = racine / "MyPublisher/ViewModels/MainWindowViewModel.Navigation.cs"
exige(partiel.exists() and "public partial class MainWindowViewModel" in partiel.read_text(encoding="utf-8"),
      "ce que I6 ajoute au modèle de vue n'est pas dans son fichier partiel (règle 10).")
if partiel.exists():
    contenu = partiel.read_text(encoding="utf-8")
    for membre in ("NavigueDansLaPlanche", "LargeurDuBloc", "HauteurDuBloc", "AuMemeRapport"):
        exige(membre in contenu, f"{membre} n'est pas dans le partiel du poste.")
recette_i1 = (racine / "Scripts/recettes/I1-apparence.sh").read_text(encoding="utf-8")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
for vue in ("MyPublisher/Views/PageCanvas.Navigation.cs", "MyPublisher/Views/MainWindow.axaml",
            "MyPublisher/Views/PageCanvas.axaml.cs"):
    exige(vue in liste, f"{vue} n'est pas dans la liste des vues de la phase de la recette I1.")
print("    d. le partiel du modèle de vue ; la planche de navigation inscrite à la recette I1")

# e. aucun zoom d'office : « Page entière » n'a qu'un appelant, sa commande.
navigation = (racine / "MyPublisher/Views/PageCanvas.Navigation.cs").read_text(encoding="utf-8")
toile = (racine / "MyPublisher/Views/PageCanvas.axaml.cs").read_text(encoding="utf-8")
sources = "\n".join(p.read_text(encoding="utf-8") for p in (racine / "MyPublisher").rglob("*.cs")
                    if "/obj/" not in str(p) and "/bin/" not in str(p))
appels = re.findall(r"OnPageEntiereDemandee\b", sources)
exige(len(appels) == 3 and "_vm.PageEntiereDemandee += OnPageEntiereDemandee;" in toile
      and "_vm.PageEntiereDemandee -= OnPageEntiereDemandee;" in toile,
      "« Page entière » est appelée ailleurs que par sa commande : un zoom d'office (règle 5).")
exige(not re.search(r"(SizeChanged|Opened|LayoutUpdated)[^;\n]*(PageEntiere|SetZoom)", sources),
      "une ouverture ou un redimensionnement change le zoom (règle 5).")
print("    e. « Page entière » n'a pour appelant que sa commande")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le rail, les touches, les champs, le partiel, aucun zoom d'office"

# ------------------------------------------------------------------------ 2. les gestes

attendus=10
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=I6" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les gestes de la planche (« poste=I6 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=I6 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les gestes sur la vraie fenêtre : $joues tests (main, Espace, loupe, molette, page entière, proportions)"

echo "I6 : la main et la loupe naviguent dans la planche sans toucher au document, « Page entière » tient la page, et la case garde le rapport du bloc."
