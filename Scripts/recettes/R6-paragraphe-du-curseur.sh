#!/usr/bin/env bash
#
# Recette du poste R6 — le panneau règle le paragraphe du curseur (spécifié le 11 octobre 2026,
# repris dans la phase N, salve Saisie 7 ; fiche 23, b8, b9 et b11).
#
# Le modèle porte depuis R5 un fer par paragraphe, et depuis R2 cinq réglages par paragraphe ;
# l'interface ne réglait que le bloc entier. Pendant la saisie, l'alignement — barre, menu
# Texte, Ctrl+L, Ctrl+E, Ctrl+R, Ctrl+J — et les retraits et espacements du dock s'appliquent
# désormais aux paragraphes que touche la sélection, ou à celui du curseur, en écarts de
# paragraphe ; hors saisie, au bloc, comme avant. Tous passent par un ParagrapheVise, qui lit le
# bloc hors saisie et la sélection en saisie, et dont un réglage est nul quand les paragraphes
# visés diffèrent. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le modèle de vue : ce que R6 ajoute est dans son partiel (règle 10) ; le ParagrapheVise
#         lit et pose par MiseEnForme (N2) en saisie, le bloc hors saisie ; la commande des
#         alignements passe par lui ; l'interligne n'y est pas — il reste au bloc ;
#      b. la barre : les quatre boutons d'alignement se lient au ParagrapheVise, sous le nom
#         « Align » d'avant, dans un seul sens — les comptes de la recette I3 tiennent ;
#         l'interligne reste lié au bloc ;
#      c. le dock : les cinq champs de la section Paragraphe se lient au ParagrapheVise, sous
#         leurs noms d'avant ; « Diminuer le retrait » et « Augmenter le retrait » sont posés à
#         côté, sur leurs commandes, sans prendre le focus ;
#      d. le README dit la portée des quatre touches, et ne range plus l'alignement par
#         paragraphe parmi les limites.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre (trait
#    « poste=R6 » : ParagrapheDuCurseurTests) — la recette du plan, et ce qui l'entoure :
#
#      · le curseur posé au clic dans le 2e de trois paragraphes, un clic sur « Centrer le
#        texte » : un seul écart {Start 1, Length 1, Align Center} ; l'écran centre ce paragraphe
#        et lui seul à 0,05 pt, le PDF le pose au même point à 0,05 pt ; un pas d'annulation rend
#        le bloc d'avant ;
#      · hors saisie, le bloc, comme avant ; l'état mixte n'allume aucun bouton et laisse le
#        champ vide ; deux écarts voisins fusionnent, un écart qui ne dit plus rien disparaît ;
#      · les quatre touches et le menu Texte sur une sélection de deux paragraphes ; un retrait
#        saisi au dock pendant la saisie va au paragraphe du curseur et fait un pas à lui (Q25) ;
#        Diminuer et Augmenter le retrait, de taquet en taquet ;
#      · les paragraphes justifiés d'ATA 21 survivent à un aller-retour par l'interface — les 16
#        centrés des Fiches, que le plan nommait, sont dans des cases de tableau (voir PLAN.md).
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
import pathlib, re, sys, xml.etree.ElementTree as ET

racine = pathlib.Path(sys.argv[1])
A = "{https://github.com/avaloniaui}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
VIEWS = "{using:MyPublisher.Views}"
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

def lire(chemin):
    return (racine / chemin).read_text(encoding="utf-8")

def corps(texte, signature):
    trouve = re.search(re.escape(signature) + r"(.*?)\n    }\n", texte, re.S)
    return trouve.group(1) if trouve else ""

# a. le modèle de vue.
partiel = lire("MyPublisher/ViewModels/MainWindowViewModel.ParagrapheVise.cs")
disposition = lire("MyPublisher/ViewModels/MainWindowViewModel.Disposition.cs")
exige("public sealed class ParagrapheVise" in partiel and "public partial class MainWindowViewModel" in partiel,
      "le ParagrapheVise n'est pas dans le partiel de R6 (règle 10).")
exige("MiseEnForme.Poser(" in partiel and re.search(r"Paragraphes is \{ \} paragraphes\s*\?\s*MiseEnForme\.Poser\(", partiel) is not None,
      "le ParagrapheVise ne pose pas en écarts (MiseEnForme.Poser) les paragraphes visés en saisie.")
exige("MiseEnForme.Lire(bloc.Text" in partiel, "le ParagrapheVise ne lit pas les paragraphes visés par MiseEnForme.Lire.")
exige(re.search(r"private void AlignerLeTexte\(TextAlign alignement\) => AlignerLesParagraphesVises\(alignement\);", disposition) is not None,
      "la commande des alignements ne passe pas par le ParagrapheVise.")
exige("LineHeight" not in re.sub(r"///.*", "", partiel), "le ParagrapheVise règle l'interligne, qui reste au bloc.")
print("    a. le partiel de R6 : le ParagrapheVise lit et pose par MiseEnForme en saisie ; les alignements passent par lui")

# b. la barre.
barre = ET.fromstring(lire("MyPublisher/Views/BarreContextuelle.axaml"))
gabarit = next((g for g in barre.iter(A + "DataTemplate") if g.get("DataType") == "models:TextFrame"), None)
exige(gabarit is not None, "la barre n'a plus de gabarit pour le bloc de texte.")
if gabarit is not None:
    panneaux = [e for e in gabarit.iter() if e.get(X + "Name") == "AlignementsDuParagraphe"]
    exige(len(panneaux) == 1, "les alignements de la barre ne sont pas rangés sous « AlignementsDuParagraphe ».")
    if panneaux:
        panneau = panneaux[0]
        exige("ParagrapheVise}" in (panneau.get("DataContext") or "") and panneau.get(X + "DataType") == "vm:ParagrapheVise",
              "les alignements de la barre ne se lient pas au ParagrapheVise.")
        boutons = [b for b in panneau.iter(VIEWS + "BoutonDEtat")]
        exige(len(boutons) == 4 and all((b.get("IsChecked") or "").startswith("{Binding Align, Mode=OneWay") for b in boutons),
              "les quatre boutons d'alignement ne se cochent pas par « Align », dans un seul sens.")
    interligne = [e for e in gabarit.iter() if e.get(X + "Name") == "InterligneDuBloc"]
    exige(len(interligne) == 1 and not any(interligne[0] is e for p in panneaux for e in p.iter()),
          "l'interligne de la barre n'est plus lié au bloc.")
print("    b. la barre : les quatre alignements sur le ParagrapheVise, sous « Align » ; l'interligne au bloc")

# c. le dock.
fenetre = ET.fromstring(lire("MyPublisher/Views/MainWindow.axaml"))
sections = [e for e in fenetre.iter() if e.get(X + "Name") == "ContenuParagraphe"]
exige(len(sections) == 1, "la section Paragraphe du dock (« ContenuParagraphe ») n'est pas là une fois.")
if sections:
    section = sections[0]
    exige("ParagrapheVise}" in (section.get("DataContext") or "") and section.get(X + "DataType") == "vm:ParagrapheVise",
          "la section Paragraphe du dock ne se lie pas au ParagrapheVise.")
    champs = {}
    for c in section.iter(VIEWS + "ChampNumerique"):
        lien = re.match(r"\{Binding (\w+), Converter=\{x:Static conv:PointsToMillimetres\.Instance\}\}", c.get("Value") or "")
        if lien:
            champs[lien.group(1)] = c.get(X + "Name")
    exige(set(champs) == {"IndentLeft", "IndentRight", "IndentFirstLine", "SpaceBefore", "SpaceAfter"},
          f"les champs de la section Paragraphe ne sont pas les cinq réglages de R2 : {sorted(champs)}.")
    exige(champs.get("IndentFirstLine") == "RetraitPremiereLigne", "« RetraitPremiereLigne » a perdu son nom (règle 6).")
    for nom, commande in (("DiminuerLeRetrait", "DiminuerLeRetraitCommand"), ("AugmenterLeRetrait", "AugmenterLeRetraitCommand")):
        boutons = [b for b in section.iter(A + "Button") if b.get(X + "Name") == nom]
        exige(len(boutons) == 1 and commande in (boutons[0].get("Command") or "") and boutons[0].get("Focusable") == "False",
              f"« {nom} » n'est pas posé une fois dans la section, sur {commande}, sans prendre le focus.")
print("    c. le dock : les cinq réglages sur le ParagrapheVise ; Diminuer et Augmenter le retrait, sans focus")

# d. le README.
readme = lire("README.md")
exige(re.search(r"^\| `Ctrl\+L` / `Ctrl\+E` / `Ctrl\+R` / `Ctrl\+J` \| .*en saisie, les paragraphes que touche la sélection \|$",
                readme, re.M) is not None,
      "le README ne dit pas que les quatre touches alignent les paragraphes de la sélection en saisie.")
exige("ne règlent que le bloc entier" not in readme, "le README range encore l'alignement par paragraphe parmi les limites.")
print("    d. le README dit la portée des touches ; la limite est levée, l'interligne y reste")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le partiel, la barre, le dock, le README"

# ------------------------------------------------------------------------ 2. les gestes

attendus=15
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo --filter "poste=R6" > "$travail/tests.txt" 2>&1; then
    grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : le paragraphe du curseur (« poste=R6 ») ne passe pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=R6 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les gestes sur la vraie fenêtre : $joues tests (le centrage du 2e paragraphe à l'écran et au PDF, en un pas ; le bloc hors saisie ; l'état mixte ; la fusion ; les touches et le menu ; le retrait du dock en un pas ; les taquets ; ATA 21 aller et retour)"

echo "R6 : pendant la saisie, l'alignement, les retraits et les espacements règlent le paragraphe du curseur, ou ceux de la sélection ; hors saisie, le bloc."
