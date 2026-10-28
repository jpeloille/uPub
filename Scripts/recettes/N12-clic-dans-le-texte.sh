#!/usr/bin/env bash
#
# Recette du poste N12 — entrer dans le texte d'un clic, le curseur au point visé (fiche 10 ;
# fiche 23, a2 à a5 et a10), sur la décision 3 du propriétaire.
#
# Un bloc de texte ne s'ouvrait qu'au double-clic, puis tout son texte était sélectionné ; un
# bloc neuf portait « Saisissez votre texte. » ou « Titre », tout sélectionné ; Insertion ▸
# Bloc de texte posait le bloc sans ouvrir la saisie ; le curseur était la croix de
# déplacement sur toute la surface du bloc. Publisher pose le point d'insertion au premier
# clic, à l'endroit visé (p. 33). Trois temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. la mécanique vit dans PageCanvas.Saisie.cs : la bande de bord de 5 px d'écran, le
#         curseur en I sur le texte ; la vue est inscrite à la liste de la recette I1 ;
#      b. le double-clic n'ouvre plus la saisie : la planche n'écoute plus DoubleTapped, et
#         l'ouverture de la saisie ne sélectionne plus tout le texte ;
#      c. OnPointerPressed entre dans le texte après avoir trouvé le bloc atteint — la ligne
#         qui le trouve reste une seule fois — et hors de Ctrl et Maj ; OnPointerMoved ne
#         sélectionne du texte qu'au-delà de la garde du seuil (Q21), posée une seule fois ;
#      d. un bloc neuf est vide, et Insertion ▸ Bloc de texte demande la saisie du bloc posé ;
#      e. l'entrée en saisie du banc (Rendu.EntrerEnSaisie) fait un seul clic ;
#      f. le README dit le clic, le glissé et le double-clic dans le texte (Souris).
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au pointeur
#    et au clavier synthétisés (trait « poste=N12 » : ClicDansLeTexteTests, et le chronomètre
#    de PressionTests) — la recette du plan :
#
#      · un clic sur le 5ᵉ caractère d'un bloc non sélectionné donne ce bloc seul, en saisie,
#        le curseur à 4 ; de même sur un bloc tourné de 30°, et sur un caractère de chaque bloc
#        de texte d'ATA 21 repéré par N3 ;
#      · un glissé du milieu du 3ᵉ au milieu du 7ᵉ caractère sélectionne [2, 7), à rebours
#        aussi ; un glissé de moins de 3 px ne sélectionne rien ;
#      · un glissé de 1 pt par le bord, Alt tenu, déplace le bloc en un pas, par chacun des
#        quatre côtés ; pour un bloc sélectionné, la bande va jusqu'au cadre de sélection ;
#        le curseur est le I sur le texte, les quatre flèches sur le bord et le cadre ;
#      · Ctrl+clic et Maj+clic dans le texte ajoutent le bloc à la sélection, sans saisie ;
#        dans le texte en saisie, Maj+clic étend la sélection ;
#      · entrer puis sortir sans taper n'empile aucun pas, taper en empile un ; un bloc tracé
#        à l'outil Texte ou Titre est vide, et la frappe y entre ; Insertion ▸ Bloc de texte
#        de même ; un bloc à champ se prend partout, sans saisie ;
#      · un double-clic sur un mot sélectionne le mot ;
#      · la pression qui ouvre la saisie rend la main en moins de 100 ms, médiane de trois
#        passes, sur chaque bloc de texte d'ATA 21 — les médianes sont affichées.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
#
# 3. LE DOUBLE-CLIC SUR UN MOT, vingt fois, chacune dans un processus neuf : Avalonia
#    reconnaît un double-clic à l'horloge, deux pressions à moins de 500 ms, et la première
#    pression d'un processus — qui prend le bloc et ouvre sa saisie — est la plus lente.
#    C'est la clause que la recette Q23 a laissée à celle-ci.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

# Le lecteur des .pub, cherché comme la recette R5 le cherche : sans lui, ATA 21 ne s'ouvre
# pas, et la recette le dit plutôt que de se déclarer verte.
outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$outil" ] || [ ! -x "$outil" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable." >&2
    echo "    sudo apt install libmspub-tools    ou    Scripts/outils-pub.sh" >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$outil"
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

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

planche = lire("MyPublisher/Views/PageCanvas.axaml.cs")
saisie_chemin = racine / "MyPublisher/Views/PageCanvas.Saisie.cs"
saisie = saisie_chemin.read_text(encoding="utf-8") if saisie_chemin.exists() else ""
toutes = {f.name: f.read_text(encoding="utf-8") for f in sorted((racine / "MyPublisher/Views").glob("PageCanvas*.cs"))}

# a. la mécanique, et la recette I1.
exige(saisie != "", "PageCanvas.Saisie.cs n'existe pas.")
exige("public partial class PageCanvas" in saisie, "PageCanvas.Saisie.cs n'est pas une partie de la planche.")
exige("private const double BandeDeBord = 5;" in saisie, "la bande de bord n'est plus de 5 px d'écran.")
exige("StandardCursorType.Ibeam" in saisie, "le texte ne montre plus le curseur en I.")
recette_i1 = lire("Scripts/recettes/I1-apparence.sh")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
exige("MyPublisher/Views/PageCanvas.Saisie.cs" in liste,
      "PageCanvas.Saisie.cs n'est pas dans la liste des vues de la phase de la recette I1.")
print("    a. PageCanvas.Saisie.cs : la bande de bord de 5 px, le curseur en I ; la vue est à la recette I1")

# b. le double-clic n'ouvre plus rien, l'entrée ne sélectionne plus tout.
for nom, texte in toutes.items():
    exige("DoubleTapped" not in texte, f"{nom} écoute encore le double-clic.")
    exige(not re.search(r"\.SelectAll\(\)", texte), f"{nom} sélectionne encore tout le texte en ouvrant la saisie.")
debut = re.search(r"private void BeginTextEdit\(TextFrame frame, int curseur\)(.*?)\n    }\n", planche, re.S)
exige(debut is not None and "PoserLaSelection(box, position, position);" in debut.group(1),
      "BeginTextEdit ne pose plus le curseur à la position visée.")
print("    b. le double-clic n'ouvre plus la saisie, et l'ouverture pose le curseur sans rien sélectionner")

# c. la pression et le glissé.
presse = re.search(r"private void OnPointerPressed\(object\? sender, PointerPressedEventArgs e\)\n    \{(.*?)\n    }\n", planche, re.S)
exige(presse is not None, "OnPointerPressed n'est plus dans la planche.")
if presse:
    corps = presse.group(1)
    ligne = "var hit = DansLaPage(page) ? _vm.CurrentPage.Frames.LastOrDefault(f => f.Visible && f.HitTest(page.X, page.Y)) : null;"
    exige(planche.count(ligne) == 1, "la ligne qui trouve le bloc atteint n'est plus là, une fois (piège du plan).")
    exige(corps.find(ligne) < corps.find("hit = BlocVise(page, hit);") < corps.find("if (!additive && EntrerDansLeTexte(hit, page, e))"),
          "OnPointerPressed n'entre pas dans le texte après avoir trouvé le bloc, hors de Ctrl et Maj.")
bouge = re.search(r"private void OnPointerMoved\(object\? sender, PointerEventArgs e\)\n    \{(.*?)\n    }\n", planche, re.S)
exige(bouge is not None, "OnPointerMoved n'est plus dans la planche.")
if bouge:
    corps = bouge.group(1)
    exige(corps.count("if (!AGlisse(surfacePoint)) return;") == 1, "la garde du seuil n'est plus là, une fois.")
    exige(corps.find("if (!AGlisse(surfacePoint)) return;") < corps.find("EtendreLaSelectionDuTexte(e);"),
          "le glissé dans le texte sélectionne avant la garde du seuil (Q21).")
print("    c. la pression entre dans le texte après le bloc atteint ; le glissé sélectionne passé le seuil")

# d. les blocs neufs.
vm = lire("MyPublisher/ViewModels/MainWindowViewModel.cs")
gabarit = re.search(r"private Frame BuildFrame\(EditorTool tool\) => tool switch(.*?)\n    };\n", vm, re.S)
exige(gabarit is not None, "BuildFrame n'est plus dans le modèle de vue.")
if gabarit:
    exige("Saisissez votre texte." not in gabarit.group(1) and gabarit.group(1).count('Text = "",') == 2,
          "un bloc de texte neuf n'est plus vide (outils Texte et Titre).")
inserer = re.search(r"private void InsertWithTool\(EditorTool tool\)(.*?)\n    }\n", vm, re.S)
exige(inserer is not None and "if (frame is TextFrame texte) DemanderLaSaisie(texte);" in inserer.group(1),
      "Insertion ▸ Bloc de texte ne demande plus la saisie du bloc posé.")
exige("_vm.SaisieDemandee += OnSaisieDemandee;" in planche, "la planche n'écoute plus la demande de saisie.")
print("    d. un bloc neuf est vide ; le menu Insertion ouvre la saisie du bloc posé")

# e. le banc.
rendu = lire("MyPublisher.Tests.Interface/Rendu.cs")
entrer = re.search(r"public static TextBox EntrerEnSaisie\((.*?)\n    }\n", rendu, re.S)
exige(entrer is not None, "Rendu.EntrerEnSaisie n'existe plus.")
if entrer:
    exige("DoubleCliquer(" not in entrer.group(1) and entrer.group(1).count("Cliquer(fenetre, SurLaPage(fenetre, x, y));") == 1,
          "Rendu.EntrerEnSaisie ne fait pas un seul clic.")
print("    e. l'entrée en saisie du banc fait un seul clic")

# f. le README.
readme = lire("README.md")
souris = re.search(r"^### Souris$(.*?)^##", readme, re.S | re.M)
exige(souris is not None, "le README n'a plus de sous-tableau « Souris ».")
if souris:
    exige(re.search(r"^\| Clic \| Dans le texte d'un bloc, y entre", souris.group(1), re.M) is not None,
          "le README ne dit plus le clic dans le texte.")
    exige(re.search(r"^\| Double-clic \| Dans le texte, sélectionne un mot", souris.group(1), re.M) is not None,
          "le README ne dit plus le double-clic sur un mot.")
    exige("Éditer le texte d'un bloc" not in souris.group(1), "le README dit encore qu'on entre dans le texte au double-clic.")
print("    f. le README dit le clic, le glissé et le double-clic dans le texte")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la bande de bord, le double-clic, la pression, les blocs neufs, le banc, le README"

# ------------------------------------------------------------------------ 2. les gestes

attendus=16
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo --filter "poste=N12" --logger "console;verbosity=detailed" > "$travail/tests.txt" 2>&1; then
    grep -E "Failed |Error Message|Assert|médiane" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les gestes du clic dans le texte (« poste=N12 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N12 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
# Les médianes, pour qui lit la recette : la pression qui ouvre la saisie, bloc par bloc.
grep -E "^ *saisie .*médiane" "$travail/tests.txt" | sed 's/^ */       /' || true
echo "  2. les gestes sur la vraie fenêtre : $joues tests (le caractère visé, le glissé, le bord, Ctrl et Maj, les pas, les blocs neufs, le mot, le chronomètre)"

# ------------------------------------------------------------------------ 3. le double-clic

for passage in $(seq 1 20); do
    if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
            --nologo -v q --filter "FullyQualifiedName~ClicDansLeTexteTests.Un_double_clic_sur_un_mot" > "$travail/double-clic.txt" 2>&1; then
        sed 's/^/    /' "$travail/double-clic.txt" >&2
        echo "ÉCHEC : le double-clic sur un mot tombe au passage $passage sur 20." >&2
        exit 1
    fi
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/double-clic.txt" | head -1)"
    if [ "${joues:-0}" -lt 1 ]; then
        echo "ÉCHEC : le test du double-clic sur un mot n'a pas été joué au passage $passage." >&2
        exit 1
    fi
done
echo "  3. le double-clic sur un mot : 20 passages sur 20, chacun dans un processus neuf"

echo "N12 : un clic dans le texte d'un bloc y entre, le curseur au caractère visé ; un glissé y sélectionne ; le bord prend le bloc ; le double-clic prend un mot ; un bloc neuf est vide, le curseur dedans."
