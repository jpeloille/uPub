#!/usr/bin/env bash
#
# Recette du poste N8 — couper, copier, coller des blocs (fiche 23, a17, a18, c21, d7). Deux
# temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. Ctrl+X, Ctrl+C et Ctrl+V sont pris par OnKeyDown, sous Contrôle seul, APRÈS la garde
#         de la saisie, et menés à OnCouper, OnCopier, OnColler — les noms que la recette A4
#         lit ; aucun n'est déclaré en KeyBinding : une liaison passerait avant le champ qui a
#         le focus et lui volerait la touche (règle 9) ;
#      b. le menu Édition porte Couper, Copier, Coller, une fois chacune, sur les gestionnaires
#         de la fenêtre et non sur une commande — c'est la fenêtre qui rend le geste au champ
#         de texte — et montre leur touche ; le tableau des raccourcis du README les dit ;
#      c. ce que le poste ajoute au modèle de vue est dans son fichier partiel (règle 10) : le
#         format « mypublisher-blocs », un identifiant qu'Avalonia 11.3 admet (lettres ASCII,
#         chiffres, point, tiret) ; la copie pose des clones — que le collage en pose aussi,
#         c'est un test qui le dit, le garde du juge — ; le format
#         s'écrit et se relit sans dossier — les chemins d'images du modèle, absolus depuis A3,
#         y passent tels quels, et l'enregistrement du document d'arrivée les rebase ;
#      d. le presse-papiers n'est touché que par la fenêtre : le modèle de vue n'en sait rien.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au clavier et
#    au menu (« PressePapiersTests », trait « poste=N8 ») — les clauses de la recette du plan,
#    et ce qui les entoure :
#
#      · copier trois blocs puis coller sur une autre page donne des instances distinctes, au
#        même endroit, dans l'ordre de la page, en un pas ; le texte brut part avec eux ; sur
#        la même page, le collage tombe à +12 pt, puis +24, comme Ctrl+D ;
#      · couper fait un seul pas, et le collage rend les blocs à leur place ;
#      · le collage pose des clones, et non les blocs du contenu qu'on lui passe (le garde du
#        juge) ;
#      · en saisie, Ctrl+C met le texte au presse-papiers, sans format de bloc, et le document
#        ne change pas ; Ctrl+V et Ctrl+X restent au texte ; de même dans un champ du dock ;
#      · un texte brut collé hors saisie devient un bloc de texte, en un pas ;
#      · un collage d'une version plus récente du format est refusé ; les références au jeu se
#        résolvent dans le document d'arrivée ; les chemins d'images s'y rebasent ;
#      · le menu Édition coupe, copie et colle, et rend le geste à la zone de saisie, qui
#        reprend le focus et sa sélection.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
#
# Le presse-papiers éprouvé est celui du banc sans affichage, un magasin en mémoire
# (HeadlessClipboardImplStub) où un format propre fait l'aller-retour (N1) : aucun faux n'est
# écrit. Le presse-papiers du système, et ce qu'un autre logiciel lit du texte brut, ne
# s'éprouvent pas ici.
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
readme = lire("README.md")
partiel_chemin = racine / "MyPublisher/ViewModels/MainWindowViewModel.PressePapiers.cs"
partiel = partiel_chemin.read_text(encoding="utf-8") if partiel_chemin.exists() else ""

gestes = {"X": ("Ctrl+X", "OnCouper", "Co_uper", "MenuCouper"),
          "C": ("Ctrl+C", "OnCopier", "_Copier", "MenuCopier"),
          "V": ("Ctrl+V", "OnColler", "C_oller", "MenuColler")}

# a. les touches, dans OnKeyDown, après la garde, jamais en KeyBinding.
corps = re.search(r"protected override void OnKeyDown\(KeyEventArgs e\)(.*?)\n    }\n", code, re.S)
exige(corps is not None, "OnKeyDown n'est plus dans MainWindow.axaml.cs.")
if corps:
    texte = corps.group(1)
    garde = texte.find("if (PageView.IsEditingText || ConsumesTyping(FocusManager?.GetFocusedElement()))")
    exige(garde >= 0, "OnKeyDown n'a plus sa garde de la saisie.")
    bloc = re.search(r"if \(e\.KeyModifiers == KeyModifiers\.Control\)\s*\{\s*switch \(e\.Key\)\s*\{(.*?)\}\s*\}", texte, re.S)
    exige(bloc is not None, "OnKeyDown n'aiguille plus le presse-papiers sous Contrôle seul.")
    if bloc and garde >= 0:
        exige(bloc.start() > garde, "le presse-papiers est aiguillé avant la garde de la saisie : il volerait la touche au champ.")
    for touche, (geste, gestionnaire, _, _) in gestes.items():
        cas = re.findall(r"case Key\." + touche + r": " + gestionnaire + r"\(this, e\); e\.Handled = true; return;", texte)
        exige(len(cas) == 1, f"{geste} n'est pas mené une fois à {gestionnaire} dans OnKeyDown.")
        if bloc:
            exige(("case Key." + touche + ": " + gestionnaire) in bloc.group(1), f"{geste} n'est pas pris sous Contrôle seul.")
liaisons = [e.get("Gesture") for e in fenetre.iter(A + "KeyBinding")]
for geste, *_ in gestes.values():
    exige(geste not in liaisons, f"« {geste} » est déclaré en KeyBinding : il passerait avant le champ (règle 9).")
print("    a. Ctrl+X, C et V pris par OnKeyDown après la garde de la saisie, sur OnCouper, OnCopier, OnColler ; aucun en KeyBinding")

# b. le menu Édition et le README.
edition = [e for e in fenetre.iter(A + "MenuItem") if e.get("Header") == "_Edition"]
exige(len(edition) == 1, "le menu Édition n'est pas dans la fenêtre, une seule fois.")
if edition:
    for geste, gestionnaire, entete, nom in gestes.values():
        entrees = [e for e in edition[0].iter(A + "MenuItem") if e.get("Click") == gestionnaire]
        exige(len(entrees) == 1, f"le menu Édition ne mène pas une fois à {gestionnaire}.")
        if len(entrees) == 1:
            entree = entrees[0]
            exige(entree.get(X + "Name") == nom and entree.get("Header") == entete and entree.get("InputGesture") == geste,
                  f"l'entrée {nom} n'est pas « {entete} », avec « {geste} ».")
            exige(entree.get("Command") is None, f"l'entrée {nom} passe par une commande, et non par la fenêtre.")
for gestionnaire in ("OnCouper", "OnCopier", "OnColler"):
    exige(len(re.findall(r"private (?:async )?void " + gestionnaire + r"\(object\? sender, RoutedEventArgs e\)", code)) == 1,
          f"{gestionnaire} n'est pas un gestionnaire de la fenêtre, une seule fois.")
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
exige(tableau is not None and "| `Ctrl+X` / `Ctrl+C` / `Ctrl+V` |" in tableau.group(1),
      "le tableau des raccourcis du README ne dit pas Ctrl+X, Ctrl+C et Ctrl+V.")
print("    b. Couper, Copier, Coller au menu Édition, sur les gestionnaires de la fenêtre, et au README")

# c. le partiel du modèle de vue.
exige("public partial class MainWindowViewModel" in partiel,
      "ce que N8 ajoute au modèle de vue n'est pas dans son fichier partiel (règle 10).")
format_ = re.search(r'public const string FormatDesBlocs = "([^"]*)";', partiel)
exige(format_ is not None and format_.group(1) == "mypublisher-blocs", "le format propre n'est pas « mypublisher-blocs ».")
if format_:
    exige(re.fullmatch(r"[A-Za-z0-9.\-]+", format_.group(1)) is not None,
          "l'identifiant du format a un caractère qu'Avalonia refuse.")
exige("page.Frames.Add(bloc.Clone());" in partiel, "la copie ne pose plus des clones de la sélection.")
exige("DocumentSerializer.ToJson(porteur)" in partiel and "DocumentSerializer.FromJson(json)" in partiel,
      "le format ne s'écrit plus, ou ne se relit plus, sans dossier : les chemins d'images ne passeraient plus tels quels.")
print("    c. le partiel : « mypublisher-blocs », des clones à la copie, sans dossier")

# d. le presse-papiers à la fenêtre seule.
for fichier in (racine / "MyPublisher").rglob("*.cs"):
    if {"obj", "bin"} & set(fichier.relative_to(racine).parts) or fichier.name == "MainWindow.axaml.cs":
        continue
    exige(not re.search(r"\bClipboard\b", fichier.read_text(encoding="utf-8")),
          f"{fichier.relative_to(racine)} touche au presse-papiers : il est à la fenêtre.")
exige(code.count("DataFormat.CreateStringApplicationFormat(MainWindowViewModel.FormatDesBlocs)") == 1,
      "la fenêtre ne déclare pas le format des blocs une fois, d'après le modèle de vue.")
print("    d. le presse-papiers n'est touché que par la fenêtre")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : les touches après la garde, le menu Édition, le README, le partiel, le presse-papiers à la fenêtre"

# ------------------------------------------------------------------------ 2. les gestes

attendus=11
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=N8" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les gestes du presse-papiers (« poste=N8 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N8 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les gestes sur la vraie fenêtre : $joues tests (copier, couper, coller, en saisie et au menu, les pièges du format)"

echo "N8 : les blocs se coupent, se copient et se collent, à leur place ou 12 pt plus loin ; en saisie, le texte reste au champ."
