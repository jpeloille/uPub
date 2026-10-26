#!/usr/bin/env bash
#
# Recette du poste N4 — l'affichage au clavier, aux touches de Publisher en français : F9,
# qui bascule entre l'affichage courant et la taille réelle ; Ctrl+Maj+L, la page entière ;
# le champ du zoom de la barre principale ; « Limites et repères » (Ctrl+Maj+O) et
# « Repères de lignes de base » (Ctrl+F7), deux bascules distinctes. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. les quatre touches sont déclarées une fois chacune en KeyBinding, chacune sur sa
#         commande ; le menu Affichage les montre, une fois chacune, sur la commande ou la
#         case qu'elles jouent ; le tableau des raccourcis du README les dit (« Maj » pour
#         « Shift ») ; OnKeyDown ne prend ni F9, ni F7, ni L sous Contrôle ;
#      b. « Limites et repères » est la case « Repères » d'avant sous son nom de Publisher :
#         liée à ShowGuides dans les deux sens, et l'entrée « Repères » a disparu ; les
#         lignes de base lisent ShowBaselines, que la planche seule connaît : aucun fichier
#         du noyau ne la nomme, elle est un état de la session ;
#      c. ce que le poste ajoute au modèle de vue est dans son fichier partiel (règle 10) ;
#         le champ du zoom est un contrôle à part, posé dans la barre principale entre les
#         deux boutons de zoom, à l'allure du libellé de la maquette — 44 px, corps 12,
#         centré, chiffres tabulaires — ; lui et la planche de navigation sont inscrits à la
#         liste des vues de la phase de la recette I1 ;
#      d. le champ n'ouvre aucune transaction : ni lui ni le partiel ne nomment BeginEdit ;
#      e. aucun zoom d'office (règle 5) : les deux demandes faites à la planche ne partent
#         que d'un endroit chacune, et ni une ouverture ni un changement de taille ne les
#         lèvent.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au
#    clavier et au pointeur synthétisés (« AffichageTests », trait « poste=N4 ») — les
#    clauses de la recette du plan, et ce qui les entoure :
#
#      · F9 sur une page à 50 % la met à 100 %, la sélection sous le même point de la zone
#        visible à 1 px près ; un second F9 rend 50 % et le défilement d'avant ; rien
#        n'entre à l'historique. Sans sélection, le milieu de la zone garde ce qu'il
#        montre ; un autre geste qui change le zoom fait oublier l'affichage d'avant ;
#      · Ctrl+Maj+L donne le zoom du bouton « Page entière » ; « 150 » puis Entrée donne
#        150 %, la frappe seule ne zoomant pas ; « 1000 » est refusé, avec un message ;
#        Échap renonce et rend la planche ; la liste se choisit au clic et aux flèches,
#        « Objets sélectionnés » tenant le bloc au milieu de la zone ; au repos, le champ
#        a l'allure du libellé, sans fond ni bord ;
#      · Ctrl+F7 ne masque que les lignes de base — marges, lignes et colonnes restent — ;
#        Ctrl+Maj+O masque tout, et n'ouvre pas de document ; le menu suit et montre ses
#        gestes.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
#
# Le bureau, vérifié avant d'écrire (26 octobre 2026, GNOME 50.1 sous Wayland, sans KDE) :
# aucune des quatre touches n'y est prise. La recette ne le rejoue pas — ce que capte le
# bureau d'une machine n'est pas une propriété du dépôt.
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

def lire(chemin):
    return (racine / chemin).read_text(encoding="utf-8")

fenetre_axaml = lire("MyPublisher/Views/MainWindow.axaml")
fenetre = ET.fromstring(fenetre_axaml)
code = lire("MyPublisher/Views/MainWindow.axaml.cs")
planche_axaml = lire("MyPublisher/Views/PageCanvas.axaml")
readme = lire("README.md")
partiel_chemin = racine / "MyPublisher/ViewModels/MainWindowViewModel.Affichage.cs"
partiel = partiel_chemin.read_text(encoding="utf-8") if partiel_chemin.exists() else ""
champ_axaml = lire("MyPublisher/Views/ChampDuZoom.axaml")
champ_code = lire("MyPublisher/Views/ChampDuZoom.axaml.cs")

# a. les touches : déclarées, montrées au menu, dites au README, hors d'OnKeyDown.
affichage = [e for e in fenetre.iter(A + "MenuItem") if e.get("Header") == "_Affichage"]
exige(len(affichage) == 1, "le menu Affichage n'est pas dans la fenêtre, une seule fois.")
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
exige(tableau is not None, "le README n'a plus de section « ## Raccourcis ».")
tableau = tableau.group(1) if tableau else ""

touches = (
    ("F9", "BasculerLaTailleReelleCommand", ("Command", "{Binding BasculerLaTailleReelleCommand}")),
    ("Ctrl+Shift+L", "PageEntiereCommand", ("Command", "{Binding PageEntiereCommand}")),
    ("Ctrl+Shift+O", "BasculerLesReperesCommand", ("IsChecked", "{Binding ShowGuides, Mode=TwoWay}")),
    ("Ctrl+F7", "BasculerLesLignesDeBaseCommand", ("IsChecked", "{Binding ShowBaselines, Mode=TwoWay}")),
)
for geste, commande, (attribut, valeur) in touches:
    declares = [e for e in fenetre.iter(A + "KeyBinding") if e.get("Gesture") == geste]
    exige(len(declares) == 1, f"« {geste} » est déclaré {len(declares)} fois en KeyBinding (une attendue).")
    if len(declares) == 1:
        exige(declares[0].get("Command") == "{Binding " + commande + "}", f"« {geste} » ne mène pas à {commande}.")
    exige(re.search(r"\[RelayCommand[^\]]*\]\s*private void " + commande[:-len("Command")] + r"\(", partiel + lire(
        "MyPublisher/ViewModels/MainWindowViewModel.Disposition.cs")) is not None,
        f"{commande} n'est pas une commande du modèle de vue.")
    if affichage:
        montres = [e for e in affichage[0].iter(A + "MenuItem") if e.get("InputGesture") == geste]
        exige(len(montres) == 1 and montres[0].get(attribut) == valeur,
              f"le menu Affichage ne montre pas « {geste} » une fois, sur {valeur}.")
    exige("`" + geste.replace("Shift", "Maj") + "`" in tableau,
          f"« {geste.replace('Shift', 'Maj')} » n'est pas au tableau des raccourcis du README.")
corps = re.search(r"protected override void OnKeyDown\(KeyEventArgs e\)(.*?)\n    }\n", code, re.S)
exige(corps is not None, "OnKeyDown n'est plus dans MainWindow.axaml.cs.")
if corps:
    # Sous Contrôle, la fenêtre ne prend que les touches de son premier aiguillage ; F9 et
    # F7 ne sont prises nulle part.
    sous_controle = corps.group(1).split("if (e.KeyModifiers == KeyModifiers.None)")[0]
    for touche in ("F9", "F7", "L"):
        exige(not re.search(r"case Key\." + touche + r"\b", sous_controle),
              f"OnKeyDown prend la touche {touche} sous Contrôle : sa liaison serait doublée (règle 9).")
    exige(not re.search(r"case Key\.F(9|7)\b", corps.group(1)), "OnKeyDown prend F9 ou F7.")
print("    a. F9, Ctrl+Maj+L, Ctrl+Maj+O et Ctrl+F7 déclarés une fois, montrés au menu Affichage, dits au README")

# b. les deux bascules.
if affichage:
    entetes = [e.get("Header") for e in affichage[0] if e.tag == A + "MenuItem"]
    exige("_Repères" not in entetes, "l'entrée « Repères » d'avant est encore au menu Affichage.")
    exige(entetes[:2] == ["_Limites et repères", "Repères de lignes de _base"],
          f"le menu Affichage ne commence pas par les deux bascules de Publisher : {entetes}")
exige("[ObservableProperty] private bool _showBaselines = true;" in partiel,
      "ShowBaselines n'est pas une propriété du partiel, vraie d'emblée.")
exige(planche_axaml.count("DataContext).ShowBaselines") == 1,
      "les lignes de base de la planche ne lisent pas ShowBaselines, une fois.")
for fichier in (racine / "MyPublisher.Core").rglob("*.cs"):
    if {"obj", "bin"} & set(fichier.relative_to(racine).parts):
        continue
    exige("ShowBaselines" not in fichier.read_text(encoding="utf-8"),
          f"{fichier.relative_to(racine)} nomme ShowBaselines : c'est un état de la session, pas du document.")
print("    b. « Limites et repères » sur ShowGuides, « Repères de lignes de base » sur ShowBaselines, que le noyau ignore")

# c. le partiel, le champ, les vues inscrites.
exige("public partial class MainWindowViewModel" in partiel,
      "ce que N4 ajoute au modèle de vue n'est pas dans son fichier partiel (règle 10).")
for membre in ("BasculerLaTailleReelle", "SaisirLeZoom", "ZoomsProposes", "ShowBaselines", "_affichageDAvant"):
    exige(membre in partiel, f"{membre} n'est pas dans le partiel du poste.")
barre = [e for e in fenetre.iter() if e.get(X + "Name") == "ValeurDuZoom"]
exige(len(barre) == 1 and barre[0].tag.endswith("}ChampDuZoom"),
      "la valeur du zoom de la barre principale n'est pas le champ du zoom.")
if len(barre) == 1:
    # Entre « Zoom arrière » et « Zoom avant », comme le libellé de la maquette.
    parent = next(p for p in fenetre.iter() if barre[0] in list(p))
    freres = list(parent)
    i = freres.index(barre[0])
    exige(0 < i < len(freres) - 1
          and freres[i - 1].get("Command") == "{Binding ZoomOutCommand}"
          and freres[i + 1].get("Command") == "{Binding ZoomInCommand}",
          "le champ du zoom n'est pas entre « Zoom arrière » et « Zoom avant ».")
style = re.search(r'<Style Selector="TextBox#SaisieDuZoom">(.*?)</Style>', champ_axaml, re.S)
exige(style is not None, "le champ du zoom n'a plus son style de repos.")
if style:
    for propriete, valeur in (("Width", "44"), ("FontSize", "12"), ("TextAlignment", "Center"),
                              ("FontFeatures", "+tnum"), ("Background", "Transparent"),
                              ("BorderBrush", "Transparent")):
        exige(f'<Setter Property="{propriete}" Value="{valeur}"/>' in style.group(1),
              f"au repos, le champ du zoom n'a pas {propriete} = {valeur}, comme le libellé de la maquette.")
maquette = (racine / "Maquettes/Fenêtre principale — bloc de texte sélectionné-html/Main.dc.html").read_text(encoding="utf-8")
exige('<span style="font-size: 12px; width: 44px; text-align: center; font-variant-numeric: tabular-nums">' in maquette,
      "la maquette ne porte plus le libellé du zoom que le champ imite : relire son style.")
recette_i1 = lire("Scripts/recettes/I1-apparence.sh")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
for vue in ("MyPublisher/Views/ChampDuZoom.axaml", "MyPublisher/Views/PageCanvas.Navigation.cs",
            "MyPublisher/Views/MainWindow.axaml", "MyPublisher/Views/PageCanvas.axaml"):
    exige(vue in liste, f"{vue} n'est pas dans la liste des vues de la phase de la recette I1.")
print("    c. le partiel du modèle de vue ; le champ entre les deux boutons, à l'allure du libellé, inscrit à la recette I1")

# d. aucune transaction.
for nom, texte in (("ChampDuZoom.axaml.cs", champ_code), ("MainWindowViewModel.Affichage.cs", partiel)):
    exige(not re.search(r"\b(BeginEdit|CommitEdit)\s*\(", texte), f"{nom} ouvre une transaction : le zoom n'est pas le document.")
print("    d. ni le champ ni le partiel n'ouvrent de transaction")

# e. aucun zoom d'office.
sources = "\n".join(p.read_text(encoding="utf-8") for p in (racine / "MyPublisher").rglob("*.cs")
                    if not {"obj", "bin"} & set(p.relative_to(racine).parts))
exige(len(re.findall(r"ZoomDemande\?\.Invoke\(", sources)) == 1 and "ZoomDemande?.Invoke(this, demande);" in partiel,
      "le zoom est demandé à la planche ailleurs que dans DemanderLeZoom.")
exige(len(re.findall(r"ObjetsSelectionnesDemandes\?\.Invoke\(", sources)) == 1
      and "ObjetsSelectionnesDemandes?.Invoke(this, EventArgs.Empty);" in partiel,
      "« Objets sélectionnés » est demandé à la planche ailleurs que par sa commande.")
exige(not re.search(r"(SizeChanged|Opened|LayoutUpdated|AttachedToVisualTree)[^;\n]*(DemanderLeZoom|SaisirLeZoom|BasculerLaTailleReelle|ZoomerSurLaSelection)", sources),
      "une ouverture ou un redimensionnement change le zoom (règle 5).")
print("    e. aucune demande de zoom d'office")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : les touches, les deux bascules, le partiel et le champ, aucune transaction, aucun zoom d'office"

# ------------------------------------------------------------------------ 2. les gestes

attendus=10
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=N4" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les gestes de l'affichage au clavier (« poste=N4 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N4 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les gestes sur la vraie fenêtre : $joues tests (F9, Ctrl+Maj+L, le champ et sa liste, les deux bascules, le menu)"

echo "N4 : F9 bascule en taille réelle et rend l'affichage d'avant, Ctrl+Maj+L tient la page, le zoom se tape, et les lignes de base ont leur bascule."
