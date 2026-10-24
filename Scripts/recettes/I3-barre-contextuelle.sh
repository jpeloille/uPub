#!/usr/bin/env bash
#
# Recette du poste I3 — la barre contextuelle.
#
# La barre, sous la barre principale, suit la sélection. Pour un bloc qui porte du texte,
# elle reçoit la mise en forme que le dock portait — la police, le corps, le gras,
# l'italique, le souligné (bouton S, Ctrl+U, « Souligné » au menu Texte, sur la commande
# de I7), les quatre alignements, l'interligne en multiple, la couleur du texte —, qui
# quitte le dock. Pour un groupe, « Dissocier » ; pour plusieurs blocs, aligner et
# répartir ; pour une image, une forme ou la page, la pastille seule. Deux temps, comme
# la recette de I2.
#
#   1. LES SOURCES, lues par un script.
#      a. Ctrl+U est déclaré une fois en KeyBinding, sur la commande « Souligné » de I7 ; le
#         menu Texte le montre une fois, sous « _Souligné », sur la même commande ; il est au
#         tableau des raccourcis du README, et OnKeyDown ne prend pas la touche U (règle 9).
#      b. Un contrôle par propriété (règle 1) : le dock ne se lie plus à la police, au corps,
#         à l'interligne, au gras, à l'italique, au souligné ni à la couleur du texte d'un
#         bloc, ni à l'alignement d'un bloc — l'alignement de chaque case d'un tableau y
#         reste, c'est une autre propriété ; la barre se lie à chacune une fois, dans le
#         gabarit du bloc de texte comme dans celui du tableau, qui n'a ni alignement ni
#         souligné. Les colonnes restent au dock seul.
#      c. Un geste, un pas (règle 2) : chaque bouton à état de la barre joue une commande,
#         ne prend pas le focus et ne se coche que par sa liaison au bloc, dans un seul
#         sens ; le sélecteur de couleur, qui s'ouvre hors de la barre, ouvre et ferme sa
#         propre transaction ; Échap quitte un champ de la barre. Les quatre icônes de
#         l'alignement du texte, que la recette I1 attend de ce poste, y servent chacune.
#      d. Ce que le poste ajoute au modèle de vue est dans son fichier partiel (règle 10), et
#         les vues qu'il écrit sont dans la liste des vues de la phase de la recette I1, qui
#         vérifie qu'aucune n'écrit une couleur de la maquette en dur.
#   2. LES TESTS d'interface « poste=I3 », sur la vraie fenêtre : un bloc de texte montre sa
#      police, son corps et son alignement ; un clic sur « Centrer le texte » le centre à
#      l'écran et au PDF, en un pas ; chaque contrôle de la barre fait un pas, deux clics
#      deux pas, et un bouton coché le reste ; le souligné a son bouton, Ctrl+U, le menu
#      Texte et ses sortes ; Ctrl+U pendant une saisie de la barre fait son propre pas ; le
#      sélecteur de couleur referme son pas quand le bloc change ; chaque propriété n'a
#      qu'un contrôle visible ; un tableau a sa mise en forme ; passer d'un texte à une image
#      — puis à plusieurs blocs, à un groupe, à rien — change la barre sans déplacer la
#      planche ; T dans le corps s'écrit, et T après Échap arme l'outil texte.
#      Puis les tests qui prennent leur contrôle par « .Single() » — la police (Q20),
#      l'angle (Q14), la couleur (C5b) — passent encore, la barre en place.
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
A = "{https://github.com/avaloniaui}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
VIEWS = "{using:MyPublisher.Views}"

echecs = []
def refuser(message):
    echecs.append(message)

fenetre = ET.fromstring((VUES / "MainWindow.axaml").read_text(encoding="utf-8"))
barre_texte = (VUES / "BarreContextuelle.axaml").read_text(encoding="utf-8")
barre = ET.fromstring(barre_texte)
code_barre = (VUES / "BarreContextuelle.axaml.cs").read_text(encoding="utf-8")
code = (VUES / "MainWindow.axaml.cs").read_text(encoding="utf-8")
readme = (racine / "README.md").read_text(encoding="utf-8")

# ------------------------------------------------------------------------- a. Ctrl+U

liaisons = [e for e in fenetre.iter(A + "KeyBinding") if e.get("Gesture") == "Ctrl+U"]
if [e.get("Command") for e in liaisons] != ["{Binding SoulignerCommand}"]:
    refuser(f"raccourcis : « Ctrl+U » est déclaré {len(liaisons)} fois en KeyBinding, une attendue sur SoulignerCommand.")
menus = [e for e in fenetre.iter(A + "MenuItem") if e.get("InputGesture") == "Ctrl+U"]
textes = [e for e in fenetre.iter(A + "MenuItem") if e.get("Header") == "_Texte"]
if len(menus) != 1 or menus[0].get("Command") != "{Binding SoulignerCommand}" or menus[0].get("Header") != "_Souligné":
    refuser("menus : « Souligné » n'est pas montré une fois, avec Ctrl+U, sur SoulignerCommand.")
elif not textes or menus[0] not in list(textes[0]):
    refuser("menus : « Souligné » n'est pas au menu Texte.")
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
if tableau is None or "`Ctrl+U`" not in tableau.group(1):
    refuser("README : « Ctrl+U » n'est pas au tableau des raccourcis.")
if re.search(r"case Key\.U\b", code):
    refuser("raccourcis : OnKeyDown prend la touche U, que « Ctrl+U » déclare aussi (règle 9).")
print("    a. Ctrl+U déclaré une fois, au menu Texte et au tableau du README, sur la commande de I7")

# ------------------------------------------------------------- b. un contrôle par propriété

PROPRIETES = ("FontFamily", "FontSize", "LineHeight", "Bold", "Italic", "Underline", "Foreground")

def liaisons_de(element, propriete):
    """Les attributs d'un élément et de ses descendants liés à une propriété du bloc, sous
    toutes les formes de la liaison — « {Binding P} », « {Binding P, …} », « {Binding P.X} »
    et le « <Binding Path="P"/> » d'une liaison multiple."""
    motif = re.compile(r"\{Binding\s+(?:Path=)?" + propriete + r"\b")
    trouvees = []
    for e in element.iter():
        for nom, valeur in e.attrib.items():
            if motif.search(valeur) or (e.tag == A + "Binding" and nom == "Path" and re.match(propriete + r"\b", valeur)):
                trouvees.append((e, nom))
    return trouvees

for p in PROPRIETES:
    restes = liaisons_de(fenetre, p)
    if restes:
        refuser(f"règle 1 : le dock se lie encore à « {p} » ({len(restes)} fois) — elle est montée dans la barre.")
# L'alignement : celui d'une case de tableau reste au dock ; celui d'un bloc, non.
cases = [g for g in fenetre.iter(A + "DataTemplate") if g.get("DataType") == "models:TableCell"]
dans_les_cases = {id(e) for g in cases for e in g.iter()}
alignements = [e for e, _ in liaisons_de(fenetre, "Align") if id(e) not in dans_les_cases]
if alignements:
    refuser(f"règle 1 : le dock se lie encore à l'alignement d'un bloc ({len(alignements)} fois).")

gabarits = {g.get("DataType"): g for g in barre.iter(A + "DataTemplate")}
attendus = {
    "models:TextFrame": {"FontFamily": 1, "FontSize": 1, "LineHeight": 1, "Bold": 1, "Italic": 1, "Underline": 1,
                         "Foreground": 1, "Align": 4},
    "models:TableFrame": {"FontFamily": 1, "FontSize": 1, "LineHeight": 1, "Bold": 1, "Italic": 1, "Underline": 0,
                          "Foreground": 1, "Align": 0},
}
for type_, comptes in attendus.items():
    gabarit = gabarits.get(type_)
    if gabarit is None:
        refuser(f"barre : pas de gabarit pour {type_}.")
        continue
    for p, n in comptes.items():
        # Un contrôle par propriété : la vignette de la couleur la MONTRE (liaison multiple,
        # dans un seul sens) ; seul le sélecteur, par sa « Valeur », la règle.
        controles = [e for e, nom in liaisons_de(gabarit, p) if e.tag != A + "Binding"]
        if len(controles) != n:
            refuser(f"barre : {type_} se lie {len(controles)} fois à « {p} », {n} attendue(s).")
colonnes = liaisons_de(barre, "Columns") + liaisons_de(barre, "ColumnCount")
if colonnes or re.search(r'Text="Colonnes"', barre_texte):
    refuser("barre : les colonnes y sont — elles restent au dock seul, où la maquette les met aussi.")
print("    b. police, corps, interligne, gras, italique, souligné, couleur et alignement : la barre seule, une fois chacun")

# --------------------------------------------------------------- c. un geste, un pas

boutons = [e for e in barre.iter(VIEWS + "BoutonDEtat")]
if len(boutons) < 7:
    refuser(f"barre : {len(boutons)} boutons à état, au moins sept attendus (G, I, S, quatre alignements).")
for b in boutons:
    bulle = b.get(A + "ToolTip.Tip") or b.get("ToolTip.Tip")
    if b.get("Focusable") != "False":
        refuser(f"barre : le bouton « {bulle} » prend le focus.")
    if not (b.get("Command") or "").startswith("{Binding"):
        refuser(f"barre : le bouton « {bulle} » ne joue pas de commande — une liaison à double sens fond ses clics.")
    if "Mode=OneWay" not in (b.get("IsChecked") or ""):
        refuser(f"barre : le bouton « {bulle} » se coche autrement que par le bloc, dans un seul sens.")
souligne = [b for b in boutons if (b.get(A + "ToolTip.Tip") or b.get("ToolTip.Tip")) == "Souligné (Ctrl+U)"]
if len(souligne) != 1 or "SoulignerCommand" not in souligne[0].get("Command", ""):
    refuser("barre : le bouton S n'est pas posé une fois sur SoulignerCommand.")
bouton_d_etat = (VUES / "BoutonDEtat.cs").read_text(encoding="utf-8")
if not re.search(r"protected override void Toggle\(\)\s*\{\s*\}", bouton_d_etat):
    refuser("BoutonDEtat : le bouton s'inverse lui-même au clic.")
volets = [e for e in barre.iter(A + "Flyout")]
if not volets or any(v.get("Opened") != "SurCouleurOuverte" or v.get("Closed") != "SurCouleurFermee" for v in volets):
    refuser("barre : le sélecteur de couleur n'ouvre ni ne ferme sa transaction.")
if not re.search(r"SurCouleurOuverte.*?vm\.BeginEdit\(\);", code_barre, re.S) or \
        not re.search(r"SurCouleurFermee.*?\.CommitEdit\(\);", code_barre, re.S):
    refuser("barre : l'ouverture du sélecteur n'ouvre pas de transaction, ou sa fermeture ne la referme pas.")
if not re.search(r"Key\.Escape.*?ClearFocus\(\)", code_barre, re.S):
    refuser("barre : Échap ne quitte pas un champ de la barre.")
for icone in ("IconeTexteAGauche", "IconeTexteCentre", "IconeTexteADroite", "IconeTexteJustifie"):
    if barre_texte.count("{StaticResource " + icone + "}") != 1:
        refuser(f"icônes : {icone} ne sert pas une fois dans la barre.")
print(f"    c. {len(boutons)} boutons à état sur leur commande, sans focus ; la couleur dans sa transaction ; Échap ; quatre icônes")

# ----------------------------------------------------------- d. le partiel, les listes

partiel = racine / "MyPublisher/ViewModels/MainWindowViewModel.BarreContextuelle.cs"
if not partiel.exists() or "public partial class MainWindowViewModel" not in partiel.read_text(encoding="utf-8") \
        or "private void SoulignerEn(" not in partiel.read_text(encoding="utf-8"):
    refuser("ce que I3 ajoute au modèle de vue n'est pas dans son fichier partiel (règle 10).")
recette_i1 = (racine / "Scripts/recettes/I1-apparence.sh").read_text(encoding="utf-8")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
for vue in ("MyPublisher/Views/MainWindow.axaml", "MyPublisher/Views/BarreContextuelle.axaml",
            "MyPublisher/Views/BoutonDEtat.cs"):
    if vue not in liste:
        refuser(f"{vue} n'est pas dans la liste des vues de la phase de la recette I1.")
print("    d. le partiel du modèle de vue ; trois vues inscrites à la liste de la recette I1")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : Ctrl+U, un contrôle par propriété, un geste un pas, le partiel"

# ------------------------------------------------------------------------ 2. les tests

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

jouer "poste=I3" 11 "2. la barre sur la vraie fenêtre : police, corps, alignement ; centré à l'écran et au PDF ; un pas par contrôle ; souligné ; un contrôle par propriété ; planche immobile ; lettres seules"
jouer "poste=Q20|poste=Q14|poste=C5b" 20 "   les « .Single() » de la police (Q20), de l'angle (Q14) et de la couleur (C5b), la barre en place"

echo "I3 : la barre contextuelle suit la sélection, et porte la mise en forme du texte que le dock a quittée."
