#!/usr/bin/env bash
#
# Recette du poste N22 — les caractères spéciaux (fiche 23, a27 et d14) : Affichage ▸ Caractères
# spéciaux et Ctrl+Maj+Y, comme Publisher (le livre, p. 9 et 24). Une bascule de la session qui
# montre sur le texte de la page un point pour chaque espace, une flèche pour chaque tabulation,
# ¶ à chaque fin de paragraphe et ↵ à chaque saut de ligne forcé, aux places de N3, à l'échelle et
# tournés avec le bloc, à une couleur de l'apparence ; rien dans PageRoot, rien au PDF ; en
# saisie, sur le texte composé (N21). Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. la bascule : une propriété du partiel du poste (règle 10), fermée d'emblée, et sa
#         commande ; rien dans le noyau ni dans l'enregistrement — elle n'entre pas au document ;
#      b. Ctrl+Maj+Y lié une fois, à cette commande ; l'entrée « Caractères spéciaux » du menu
#         Affichage montre sa touche et coche la bascule ; le README le dit au sous-tableau
#         Affichage ;
#      c. le dessin : le contrôle des marques est posé dans l'Overlay par le partiel du poste,
#         jamais dans PageRoot, que l'écran compare au PDF (règle 4) ; le fichier principal de la
#         planche n'est pas touché (règle 16) ; la couleur vient de l'apparence, par le style de
#         la planche, et aucune n'est écrite en dur ; l'export n'en sait rien ;
#      d. les deux vues neuves sont à la recette I1.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre (trait
#    « poste=N22 » : CaracteresSpeciauxTests) — la recette du plan, et ce qui l'entoure :
#
#      · Ctrl+Maj+Y et le menu basculent la session, sans pas d'annulation ni « modifié » ;
#      · sur recette-texte-riche.mypub, autant de ¶ que de paragraphes, chacun au PointDe de son
#        saut à 0,01 pt ; l'encre de la page identique, bascule ouverte ou fermée ; le contrôle
#        dans l'Overlay, rien de lui dans PageRoot ;
#      · sur l'exemple et ATA 21, chaque espace, tabulation, saut forcé et fin de paragraphe
#        marqué, au point de N3 ;
#      · un texte fait pour (espaces doubles, tabulation, saut forcé, paragraphe vide, « \r\n ») ;
#        l'espace qui pend au bout d'une ligne renvoyée, l'espace élargie d'une ligne justifiée ;
#      · en saisie, Ctrl+Maj+Y garde la saisie, et les marques suivent la frappe ;
#      · à l'image : le ¶ d'un bloc tourné à 200 % se dessine à l'accent, et celui d'une ligne qui
#        sort du bloc ne se dessine pas ;
#      · une autre page, un bloc masqué, un bloc ajouté ; bascule fermée, plus rien n'est écouté.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
#
# Le PDF ne change pas : Scripts/comparer-rendus.sh le vérifie.
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

def lire(chemin):
    chemin = racine / chemin
    return chemin.read_text(encoding="utf-8") if chemin.exists() else ""

partiel = lire("MyPublisher/ViewModels/MainWindowViewModel.CaracteresSpeciaux.cs")
fenetre_axaml = lire("MyPublisher/Views/MainWindow.axaml")
planche_axaml = lire("MyPublisher/Views/PageCanvas.axaml")
planche = lire("MyPublisher/Views/PageCanvas.axaml.cs")
dessin = lire("MyPublisher/Views/PageCanvas.CaracteresSpeciaux.cs")
vue = lire("MyPublisher/Views/CaracteresSpeciaux.cs")
readme = lire("README.md")

# a. la bascule.
exige("[ObservableProperty] private bool _caracteresSpeciauxVisibles;" in partiel,
      "la bascule n'est pas une propriété du partiel du poste, fermée d'emblée.")
exige(re.search(r"\[RelayCommand\]\s*private void BasculerLesCaracteresSpeciaux\(\) =>\s*"
                r"CaracteresSpeciauxVisibles = !CaracteresSpeciauxVisibles;", partiel) is not None,
      "la commande BasculerLesCaracteresSpeciaux ne bascule pas la propriété.")
for fichier in list((racine / "MyPublisher.Core").rglob("*.cs")) + list((racine / "MyPublisher/Services").rglob("*.cs")):
    if {"obj", "bin"} & set(fichier.relative_to(racine).parts):
        continue
    exige("CaracteresSpeciaux" not in fichier.read_text(encoding="utf-8"),
          f"{fichier.relative_to(racine)} parle des caractères spéciaux : la bascule n'est pas au document.")
print("    a. la bascule : une propriété de la session, fermée d'emblée, hors du document")

# b. la touche, le menu, le README.
A = "{https://github.com/avaloniaui}"
fenetre = ET.fromstring(fenetre_axaml)
liaisons = [(e.get("Gesture"), e.get("Command")) for e in fenetre.iter(A + "KeyBinding")]
exige([c for g, c in liaisons if g == "Ctrl+Shift+Y"] == ["{Binding BasculerLesCaracteresSpeciauxCommand}"],
      "Ctrl+Maj+Y n'est pas lié une fois à la bascule des caractères spéciaux.")
affichage = [e for e in fenetre.iter(A + "MenuItem") if e.get("Header") == "_Affichage"]
entree = [e for e in affichage[0] if e.get("Name") == "MenuCaracteresSpeciaux"] if affichage else []
exige(len(entree) == 1, "le menu Affichage n'a pas d'entrée « Caractères spéciaux ».")
if entree:
    e = entree[0]
    exige(e.get("InputGesture") == "Ctrl+Shift+Y" and e.get("ToggleType") == "CheckBox"
          and e.get("IsChecked") == "{Binding CaracteresSpeciauxVisibles, Mode=TwoWay}",
          "l'entrée « Caractères spéciaux » ne montre pas Ctrl+Maj+Y ou ne coche pas la bascule.")
section = re.search(r"^### Affichage$(.*?)^### ", readme, re.S | re.M)
exige(section is not None and re.search(r"^\| `Ctrl\+Maj\+Y` \| Caractères spéciaux", section.group(1), re.M) is not None,
      "le sous-tableau Affichage du README ne dit pas Ctrl+Maj+Y.")
print("    b. Ctrl+Maj+Y lié une fois, montré au menu Affichage, dit au README")

# c. le dessin, dans l'Overlay.
exige("Overlay.Children.Insert(0, _caracteresSpeciaux);" in dessin,
      "le contrôle des caractères spéciaux n'est pas posé dans l'Overlay.")
exige(re.search(r"PageRoot\.Children\.(Add|Insert)", dessin) is None,
      "le partiel pose quelque chose dans PageRoot, que l'écran compare au PDF.")
racine_page = re.search(r'<Panel Name="PageRoot"(.*?)</LayoutTransformControl>', planche_axaml, re.S)
exige(racine_page is not None and "CaracteresSpeciaux" not in racine_page.group(1),
      "les caractères spéciaux sont dans PageRoot.")
exige("CaracteresSpeciaux" not in planche, "le fichier principal de la planche parle des caractères spéciaux (règle 16).")
style = re.search(r'<Style Selector="views\|CaracteresSpeciaux">(.*?)</Style>', planche_axaml, re.S)
exige(style is not None and '<Setter Property="Encre" Value="{StaticResource ' in style.group(1),
      "les caractères spéciaux ne prennent pas leur couleur à l'apparence.")
for nom, texte in (("CaracteresSpeciaux.cs", vue), ("PageCanvas.CaracteresSpeciaux.cs", dessin)):
    exige(texte != "", f"{nom} est introuvable.")
    exige(re.search(r"#[0-9A-Fa-f]{6}|Colors\.|Brushes\.", texte) is None, f"{nom} écrit une couleur en dur.")
exige("geometrie.AbscisseSur(posee" in dessin and "compose.Geometrie()" in dessin,
      "les caractères ne sont pas posés sur la géométrie du texte dessiné (N3).")
print("    c. le dessin : dans l'Overlay, hors de PageRoot et du fichier principal, à la couleur de l'apparence")

# d. la recette I1.
recette_i1 = lire("Scripts/recettes/I1-apparence.sh")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
for vue_neuve in ("MyPublisher/Views/CaracteresSpeciaux.cs", "MyPublisher/Views/PageCanvas.CaracteresSpeciaux.cs"):
    exige(vue_neuve in liste, f"{vue_neuve} n'est pas dans la liste des vues de la phase de la recette I1.")
print("    d. les deux vues neuves sont à la recette I1")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la bascule, la touche et le menu, le dessin dans l'Overlay, la recette I1"

# ------------------------------------------------------------------------ 2. les gestes

attendus=8
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo --filter "poste=N22" > "$travail/tests.txt" 2>&1; then
    grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les caractères spéciaux (« poste=N22 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N22 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les gestes sur la vraie fenêtre : $joues tests (la bascule, le ¶ au point de N3 sur le texte riche, l'exemple et ATA 21, l'encre de la page, chaque sorte, l'espace pendante et justifiée, la saisie, l'image tournée à 200 %, la page et les abonnements)"

echo "N22 : Ctrl+Maj+Y montre les espaces, les tabulations, les fins de paragraphe et les sauts forcés sur le texte de la page, sans rien changer à la page ni au PDF."
