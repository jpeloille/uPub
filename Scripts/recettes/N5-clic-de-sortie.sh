#!/usr/bin/env bash
#
# Recette du poste N5 — quitter la saisie d'un clic qui agit (fiche 23, a8 et a9).
#
# Pendant la saisie d'un texte sur la page, la couche de saisie se retirait du test de survol.
# Le clic suivant hors du texte refermait la saisie, mais la pression allait ensuite à la
# page : rien n'était pris, il fallait recliquer, et les poignées dessinées autour du bloc en
# saisie ne se tiraient pas. La couche reste désormais atteignable partout, sauf sur le texte
# en saisie ; le clic hors du texte referme la saisie, puis fait ce qu'il ferait hors saisie.
# Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. « InputLayer » est une CoucheDeSaisie, une fois, sous son nom (règle 6) ; elle dérive
#         de Border — NavigationTests la cherche par Trouver<Border>(…, "InputLayer") — et
#         répond elle-même au test de survol (ICustomHitTest) ;
#      b. la planche ne l'éteint plus : aucun « InputLayer.IsHitTestVisible » ; elle lui dit ce
#         qu'elle laisse au champ, le texte en saisie et lui seul ;
#      c. OnPointerPressed referme la saisie avant toute chose, avant même le choix du bouton
#         (la pression du bouton droit en sort aussi, et A12 la prendra là), puis agit comme
#         d'habitude ; rien n'est rejoué : la planche ne lève aucun événement de pointeur ;
#      d. le gestionnaire descendant reste, pour les règles, et laisse à la couche la pression
#         qui l'atteint ;
#      e. la couche est inscrite à la liste des vues de la phase de la recette I1.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au pointeur
#    et au clavier synthétisés (« ClicDeSortieTests », trait « poste=N5 ») — la recette du
#    plan, et ce qui l'entoure :
#
#      · en saisie, un clic sur un autre bloc le sélectionne d'un seul geste : la frappe fait
#        un pas, la sélection aucun ; un glissé depuis un autre bloc le déplace dès la
#        pression qui sort de la saisie : deux pas ;
#      · une poignée du bloc en saisie se tire : un pas pour la frappe, un pour le
#        redimensionnement ;
#      · un clic dans le texte en saisie déplace le curseur et laisse la saisie ouverte — au
#        coin du texte, à 200 %, la planche décalée de la fenêtre : c'est ce qui mesure le
#        repère du point que le compositeur passe à la couche ; sur un bloc tourné, la couche
#        laisse le texte tourné et non le rectangle droit qui l'enveloppe ;
#      · un clic dans le vide désélectionne ; un repère se reprend d'un seul geste ; un glissé
#        depuis la règle pose un repère neuf ;
#      · la pression du bouton droit hors du texte sort de la saisie et atteint la couche ; le
#        survol d'un autre bloc pendant la saisie montre le curseur de déplacement.
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

planche_axaml = lire("MyPublisher/Views/PageCanvas.axaml")
planche = lire("MyPublisher/Views/PageCanvas.axaml.cs")
couche_chemin = racine / "MyPublisher/Views/CoucheDeSaisie.cs"
couche = couche_chemin.read_text(encoding="utf-8") if couche_chemin.exists() else ""
navigation = lire("MyPublisher.Tests.Interface/NavigationTests.cs")

# a. la couche.
exige(len(re.findall(r'Name="InputLayer"', planche_axaml)) == 1
      and '<views:CoucheDeSaisie Name="InputLayer" Background="Transparent"/>' in planche_axaml,
      "« InputLayer » n'est pas une CoucheDeSaisie, une fois, dans la planche.")
exige(re.search(r"public class CoucheDeSaisie : Border, ICustomHitTest\b", couche) is not None,
      "CoucheDeSaisie ne dérive pas de Border, ou ne répond pas elle-même au test de survol.")
exige(re.search(r"public bool HitTest\(Point point\)", couche) is not None,
      "CoucheDeSaisie n'a pas de HitTest.")
exige('Trouver<Border>(fenetre, "InputLayer")' in navigation,
      "NavigationTests ne cherche plus la couche comme un Border : la règle 6 n'est plus éprouvée.")
print("    a. « InputLayer » est une CoucheDeSaisie, un Border qui répond lui-même au test de survol")

# b. la couche n'est plus éteinte ; la planche lui dit ce qu'elle laisse.
exige("IsHitTestVisible" not in planche, "la planche éteint encore la couche de saisie (IsHitTestVisible).")
exige(planche.count("InputLayer.Laisse = LaisseAuTexteEnSaisie;") == 1,
      "la planche ne dit pas à la couche ce qu'elle laisse au champ, une fois.")
laisse = re.search(r"private bool LaisseAuTexteEnSaisie\(Point surLaCouche\)(.*?)\n    }\n", planche, re.S)
exige(laisse is not None, "LaisseAuTexteEnSaisie n'est plus dans la planche.")
if laisse:
    exige("_zoneDeSaisie" in laisse.group(1) and "InputLayer.TranslatePoint(surLaCouche, zone)" in laisse.group(1),
          "ce que la couche laisse n'est pas le texte en saisie, lu dans le repère de la couche.")
print("    b. la couche reste atteignable ; elle laisse au champ le texte en saisie")

# c. la pression qui sort de la saisie agit.
presse = re.search(r"private void OnPointerPressed\(object\? sender, PointerPressedEventArgs e\)\n    \{(.*?)\n    }\n", planche, re.S)
exige(presse is not None, "OnPointerPressed n'est plus dans la planche.")
if presse:
    instructions = [l.strip() for l in presse.group(1).splitlines() if l.strip() and not l.strip().startswith("//")]
    exige(instructions[:1] == ["if (_editing is not null) EndTextEdit();"],
          "OnPointerPressed ne commence pas par refermer la saisie, avant le choix du bouton.")
    exige(instructions.count("if (_editing is not null) EndTextEdit();") == 1
          and not re.search(r"EndTextEdit\(\);\s*\}?\s*return;", presse.group(1)),
          "OnPointerPressed rend la main après avoir refermé la saisie.")
# Le double-clic sur une image ou une forme, depuis A12, se lit — une fois, la pression comptée
# par Avalonia, dans PageCanvas.MenuDuBloc.cs — et ne se fabrique pas : toute autre mention du
# compte des clics reste refusée.
for fichier in sorted((racine / "MyPublisher/Views").glob("PageCanvas*.cs")):
    texte = fichier.read_text(encoding="utf-8")
    lus = re.findall(r"\be\.ClickCount != 2\b", texte) if fichier.name == "PageCanvas.MenuDuBloc.cs" else []
    exige(not re.search(r"RaiseEvent\s*\(|DoubleTappedEvent", texte) and texte.count("ClickCount") == len(lus) <= 1,
          f"{fichier.name} rejoue un geste ou fabrique un double-clic.")
print("    c. OnPointerPressed referme la saisie avant toute chose, puis agit ; rien n'est rejoué")

# d. le gestionnaire descendant.
exige(planche.count("AddHandler(PointerPressedEvent, OnTunnelPointerPressed, RoutingStrategies.Tunnel);") == 1,
      "le gestionnaire descendant n'est plus posé : les règles ne referment plus la saisie.")
descendant = re.search(r"private void OnTunnelPointerPressed\(.*?\n    }\n", planche, re.S)
exige(descendant is not None and "if (ReferenceEquals(e.Source, InputLayer)) return;" in descendant.group(0),
      "le gestionnaire descendant ne laisse pas à la couche la pression qui l'atteint.")
print("    d. le gestionnaire descendant reste, pour les règles")

# e. la recette I1.
recette_i1 = lire("Scripts/recettes/I1-apparence.sh")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
exige("MyPublisher/Views/CoucheDeSaisie.cs" in liste,
      "CoucheDeSaisie.cs n'est pas dans la liste des vues de la phase de la recette I1.")
print("    e. la couche est inscrite à la liste des vues de la phase de la recette I1")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la couche, ce qu'elle laisse, la pression qui sort de la saisie et agit, le gestionnaire descendant"

# ------------------------------------------------------------------------ 2. les gestes

attendus=10
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=N5" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les gestes du clic de sortie (« poste=N5 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N5 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les gestes sur la vraie fenêtre : $joues tests (un autre bloc, une poignée, le texte, le vide, un repère, la règle, le bouton droit, le survol)"

echo "N5 : en saisie, un clic hors du texte la referme et agit d'un seul geste — il prend un autre bloc, une poignée, un repère, ou vide la sélection."
