#!/usr/bin/env bash
#
# Recette du poste A12 — le menu contextuel du bloc (fiche 11 ; fiche 23, c3, c16, c25, e1, e2, f16),
# repris et élargi dans la phase N. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le menu est posé sur la planche (PageCanvas), et non sur sa couche de saisie : la
#         touche Menu lève sa demande sur le contrôle qui a le focus, et la couche ne l'a jamais ;
#         il est écrit dans MainWindow.axaml, à côté des menus, sous le nom « MenuDuBloc » ; la zone
#         du texte en saisie n'a plus le menu du thème, et sa demande monte à la planche ;
#      b. chaque entrée est liée à la même commande — ou au même gestionnaire de la fenêtre — que
#         son pendant aux menus, au même paramètre, avec le même geste, la même garde et la même
#         case ; seules trois entrées sont neuves, sans pendant : Supprimer le texte, Convertir en
#         texte et Format du bloc…, chacune une commande du partiel du poste (règle 10) ;
#      c. ses entrées selon la sélection : le texte en saisie a Couper, Copier, Coller et Format
#         du bloc…, le vide Coller, Repères de mise en page… et Arrière-plan…, et chaque entrée dit
#         où elle paraît ; ses lettres d'accès sont uniques au niveau et sans accent ;
#      d. rien du seuil D — « Ajuster le texte », « Habillage » — n'y paraît, ni dans la planche :
#         la recette I2 en lit désormais aussi PageCanvas.axaml ;
#      e. la pression du bouton droit suit le chemin du gauche jusqu'à la sélection, la ligne qui
#         trouve le bloc atteint restant une, comme le juge la vise ; la demande du menu est prise
#         au passage descendant, où règles et barres de défilement se taisent ; Maj+F10 est lu par
#         la planche ; le double-clic sur une image ou une forme mène à Format du bloc… ;
#      f. le README le dit, au sous-tableau « Souris » et à celui des « Objets » ; la vue neuve est
#         dans la liste de la recette I1.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, sous
#    « poste=A12 » (MenuDuBlocTests) : un clic droit au centre d'un bloc le prend seul et ouvre le
#    menu — la recette d'origine —, dans une sélection la garde, dans le vide la vide ; chaque
#    entrée sur la commande de son pendant, au même geste affiché ; Premier plan, Couper et
#    Coller joués au menu, en un pas chacun ; Supprimer le texte en un pas, grisé sur un bloc vide
#    ou à champ ; Convertir en texte ; Format du bloc… vers la section de chaque sorte, sans pas ;
#    le double-clic sur une image ouvre la section Image ; Maj+F10 et la touche Menu ouvrent le
#    menu sous la sélection ; un clic droit sur une règle ou une barre de défilement n'ouvre rien ;
#    en saisie, le menu du texte, qui copie et coupe le texte sélectionné, la saisie gardée.
#    Puis le voisin réécrit par le sens nouveau du geste : le bouton droit de N5.
#
# Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
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
import xml.etree.ElementTree as ET

racine = pathlib.Path(sys.argv[1])
echecs = []
def exige(condition, message):
    if not condition:
        echecs.append(message)

def lire(chemin):
    f = racine / chemin
    return f.read_text(encoding="utf-8") if f.exists() else ""

A = "{https://github.com/avaloniaui}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
V = "{using:MyPublisher.Views}"
fenetre_txt = lire("MyPublisher/Views/MainWindow.axaml")
planche_txt = lire("MyPublisher/Views/PageCanvas.axaml")
code = lire("MyPublisher/Views/PageCanvas.axaml.cs")
partie = lire("MyPublisher/Views/PageCanvas.MenuDuBloc.cs")
partiel = lire("MyPublisher/ViewModels/MainWindowViewModel.MenuDuBloc.cs")
readme = lire("README.md")
fenetre = ET.fromstring(fenetre_txt)
planche = ET.fromstring(planche_txt)

# -------------------------------------------------------------- a. posé sur la planche
planches = [e for e in fenetre.iter(V + "PageCanvas") if e.get(X + "Name") == "PageView"]
exige(len(planches) == 1, "la planche « PageView » n'est pas dans la fenêtre, une fois.")
porteurs = [e for e in (planches[0] if planches else []) if e.tag == V + "PageCanvas.ContextMenu"]
exige(len(porteurs) == 1, "le menu contextuel n'est pas posé sur la planche (PageCanvas.ContextMenu).")
menus = [e for e in (porteurs[0] if porteurs else []) if e.tag == A + "ContextMenu"]
exige(len(menus) == 1 and menus[0].get(X + "Name") == "MenuDuBloc", "le menu de la planche n'est pas « MenuDuBloc ».")
menu = menus[0] if menus else ET.Element("vide")
exige(sum(1 for _ in fenetre.iter(A + "ContextMenu")) == 1, "la fenêtre porte un autre menu contextuel que celui de la planche.")
exige(not any(t.endswith("ContextMenu") or t.endswith(".ContextMenu") for t in (e.tag for e in planche.iter())),
      "la planche écrit un menu contextuel dans PageCanvas.axaml — sur la couche, la touche Menu ne l'ouvrirait pas.")
zones = [e for e in planche.iter(A + "TextBox") if e.get("Classes") == "frameEditor"]
exige(len(zones) == 1 and zones[0].get("ContextFlyout") == "{x:Null}",
      "la zone du texte en saisie garde le menu du thème : la demande du clic droit ne monterait pas à la planche.")
print("    a. le menu « MenuDuBloc » sur la planche, et non sur sa couche ; la zone en saisie sans le menu du thème")

# ----------------------------------------- b. chaque entrée sur la commande de son pendant
barres = [e for e in fenetre.iter(A + "Menu") if e.get(X + "Name") == "Menus"]
aux_menus = [e for e in barres[0].iter(A + "MenuItem")] if barres else []
def ecran(entete):
    return (entete or "").replace("__", "\0").replace("_", "").replace("\0", "_")
def cle(e):
    return (e.get("Command"), e.get("CommandParameter"), e.get("Click"))
neuves = {"{Binding SupprimerLeTexteCommand}", "{Binding ConvertirEnTexteCommand}", "{Binding FormatDuBlocCommand}"}
entrees = list(menu.iter(A + "MenuItem"))
confrontees = 0
for e in entrees:
    commande, parametre, clic = cle(e)
    if not (commande or clic or e.get("IsChecked")):
        exige(any(s.tag == A + "MenuItem" for s in e), f"« {e.get('Header')} » n'a ni commande ni sous-menu.")
        continue
    if commande in neuves:
        exige(not any(m.get("Command") == commande for m in aux_menus), f"« {e.get('Header')} », neuve, a un pendant aux menus.")
        exige(e.get("InputGesture") is None, f"« {e.get('Header')} », neuve, affiche un geste que rien ne lie.")
        continue
    if e.get("IsChecked"):
        pendants = [m for m in aux_menus if m.get("IsChecked") == e.get("IsChecked")]
    else:
        pendants = [m for m in aux_menus if cle(m) == cle(e)]
    exige(pendants, f"« {e.get('Header')} » ({commande or clic}) n'a pas de pendant aux menus.")
    # Le libellé dit la commande : une entrée liée à la commande d'une voisine — Avancer sur
    # Reculer — aurait encore un pendant. Il se lit comme à l'écran, sans lettre d'accès. Seule
    # la balise du bloc de coordonnées nomme autrement le dialogue des jeux (p. 54).
    if clic != "OnEditBusinessInfo":
        exige(any(ecran(m.get("Header")) == ecran(e.get("Header")) for m in pendants),
              f"« {ecran(e.get('Header'))} » est lié à la commande de « {ecran(pendants[0].get('Header')) if pendants else '?'} ».")
    for m in pendants:
        for attribut in ("InputGesture", "IsEnabled", "ToggleType", "IsChecked"):
            exige(m.get(attribut) == e.get(attribut),
                  f"« {e.get('Header')} » : {attribut} « {e.get(attribut)} », et « {m.get(attribut)} » à son pendant « {m.get('Header')} ».")
    confrontees += 1
exige(confrontees >= 30, f"{confrontees} entrées confrontées à leur pendant, trente attendues au moins.")
for clic in ("OnCouper", "OnCopier", "OnColler", "OnImportText", "OnEditBusinessInfo"):
    exige(any(e.get("Click") == clic for e in entrees), f"le menu n'a pas l'entrée qui joue {clic}.")
exige(sorted(e.get("Command") for e in entrees if e.get("Command") in neuves) == sorted(neuves),
      "les trois entrées neuves — Supprimer le texte, Convertir en texte, Format du bloc… — ne sont pas toutes là.")
for commande in ("SupprimerLeTexte", "ConvertirEnTexte", "FormatDuBloc"):
    exige(re.search(r"\[RelayCommand[^\]]*\]\s*private void " + commande + r"\(\)", partiel) is not None,
          f"la commande {commande} n'est pas au partiel du poste (règle 10).")
exige("UnGesteUnPas(() => bloc.Saisie = \"\");" in partiel, "Supprimer le texte ne passe pas par la saisie, en un pas.")
print(f"    b. {confrontees} entrées sur la commande, le geste et la garde de leur pendant ; trois neuves, au partiel")

# ----------------------------------------------------------------- c. selon la sélection
haut = [e for e in menu if e.tag in (A + "MenuItem", A + "Separator")]
def ou(e):
    return set((e.get("Classes") or "").split())
contextes = {"saisie", "blocs", "vide"}
for e in haut:
    exige(ou(e) & contextes, f"« {e.get('Header') or 'un séparateur'} » ne dit pas où il paraît.")
def montrees(contexte):
    return [e.get("Header") for e in haut if e.tag == A + "MenuItem" and contexte in ou(e) and not ou(e) & {"siTexte", "siCoordonnees"}]
exige(montrees("saisie") == ["Co_uper", "_Copier", "C_oller", "Format du _bloc…"],
      f"en saisie, le menu montre {montrees('saisie')}, et non Couper, Copier, Coller et Format du bloc….")
exige(montrees("vide") == ["C_oller", "Repères de m_ise en page…", "_Arrière-plan…"],
      f"dans le vide, le menu montre {montrees('vide')}.")
exige(montrees("blocs")[:3] == ["Co_uper", "_Copier", "C_oller"], "le menu des blocs ne commence pas par Couper, Copier, Coller (p. 34).")
for classe in ("saisie", "blocs", "vide", "siTexte", "siCoordonnees"):
    exige(f'"{classe}"' in partie, f"la planche ne lit pas la classe « {classe} » à l'ouverture.")
def lettre(entete):
    i = 0
    while i < len(entete) - 1:
        if entete[i] == "_":
            if entete[i + 1] == "_":
                i += 2
                continue
            return entete[i + 1]
        i += 1
    return None
for parent in [menu, *entrees]:
    niveau = [e for e in parent if e.tag == A + "MenuItem"]
    vues = {}
    for e in niveau:
        l = lettre(e.get("Header") or "")
        exige(l is not None and l.isascii() and l.isalnum(), f"« {e.get('Header')} » n'a pas de lettre d'accès sans accent.")
        if l:
            vues.setdefault(l.upper(), []).append(e.get("Header"))
    for l, entetes in vues.items():
        exige(len(entetes) == 1, f"la lettre d'accès {l} est en double au menu contextuel ({', '.join(entetes)}).")
print("    c. les entrées du texte en saisie, des blocs et du vide ; les lettres d'accès uniques au niveau")

# --------------------------------------------------------------------- d. rien du seuil D
interdits = ["Habillage", "Ajuster le texte", "Effets", "Chaînage"]
for e in [*menu.iter(), *planche.iter()]:
    valeur = (e.get("Header") or e.get("Text") or e.get("Content") or "").replace("_", "")
    for mot in interdits:
        exige(mot.lower() not in valeur.lower(), f"« {valeur} » est offert, et {mot} reste au seuil D.")
i2 = lire("Scripts/recettes/I2-B7-disposition.sh")
exige('for fichier in (FENETRE, barre, entree, VUES / "PageCanvas.axaml"):' in i2,
      "la recette I2 ne lit pas les interdits dans PageCanvas.axaml.")
print("    d. rien du seuil D au menu ni à la planche ; la recette I2 lit aussi PageCanvas.axaml")

# ---------------------------------------------------------------------------- e. la planche
cible = "var hit = DansLaPage(page) ? _vm.CurrentPage.Frames"
exige(code.count(cible) == 1 and partie.count(cible) == 0, "la ligne du juge « var hit = » n'est plus une, dans PageCanvas.axaml.cs.")
corps = re.search(r"private void OnPointerPressed\(object\? sender, PointerPressedEventArgs e\)\s*\{(.*?)\n    \}", code, re.S)
exige(corps is not None, "OnPointerPressed est introuvable.")
corps = corps.group(1) if corps else ""
exige("var droit = boutons.IsRightButtonPressed" in corps, "la pression du bouton droit n'est pas lue.")
exige(corps.find(cible) < corps.find("PrendreAuBoutonDroit(hit);"),
      "le bouton droit ne s'arrête pas à la sélection, après la ligne qui trouve le bloc atteint.")
exige(corps.find("if (_editing is not null) EndTextEdit();") < corps.find("var droit"),
      "la pression du bouton droit ne sort plus de la saisie d'abord (N5).")
exige("OuvrirSaSection(hit, e)" in corps, "le double-clic ne mène pas à la section du bloc.")
exige("AddHandler(ContextRequestedEvent, PreparerLeMenu, RoutingStrategies.Tunnel);" in partie,
      "la demande du menu n'est pas prise au passage descendant.")
exige("!ReferenceEquals(source, InputLayer) && !ReferenceEquals(source, this) && !dansLeTexte" in partie,
      "règles et barres de défilement ne se taisent pas.")
exige("e.Key != Key.F10 || e.KeyModifiers != KeyModifiers.Shift" in partie and "OuvrirLeMenuAuClavier(e)" in code,
      "Maj+F10 n'est pas lu par la planche.")
exige("_vm.FormatDuBlocCommand.Execute(null);" in partie and "ImageFrame or ShapeFrame" in partie,
      "le double-clic sur une image ou une forme ne joue pas Format du bloc….")
print("    e. le bouton droit jusqu'à la sélection, la ligne du juge une fois ; la demande au passage descendant ; Maj+F10 ; le double-clic")

# ------------------------------------------------------------------------ f. README, I1
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
tableau = tableau.group(1) if tableau else ""
souris = re.search(r"^### Souris$(.*?)(^### |\Z)", tableau, re.S | re.M)
objets = re.search(r"^### Objets$(.*?)^### ", tableau, re.S | re.M)
exige(souris is not None and "| Clic droit |" in souris.group(1), "le sous-tableau « Souris » du README ne dit pas le clic droit.")
exige(souris is not None and "une image ou une forme, ouvre sa section du dock" in souris.group(1).lower(),
      "le sous-tableau « Souris » du README ne dit pas le double-clic sur une image.")
exige(objets is not None and "| `Maj+F10` ou `Menu` |" in objets.group(1), "le sous-tableau « Objets » du README ne dit pas Maj+F10.")
i1 = lire("Scripts/recettes/I1-apparence.sh")
exige("    MyPublisher/Views/PageCanvas.MenuDuBloc.cs\n" in i1, "PageCanvas.MenuDuBloc.cs n'est pas dans vues_de_la_phase de la recette I1.")
print("    f. le README, aux sous-tableaux « Souris » et « Objets » ; la vue neuve à la recette I1")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le menu sur la planche, chaque entrée sur son pendant, selon la sélection, rien du seuil D, le bouton droit, le README"

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

jouer "poste=A12" 15 "2. A12, sur la vraie fenêtre : le clic droit, chaque entrée sur son pendant, Supprimer le texte, Format du bloc…, le double-clic, Maj+F10, les règles, la saisie"
jouer "FullyQualifiedName~ClicDeSortieTests.En_saisie_la_pression_du_bouton_droit_sort_de_la_saisie_et_atteint_la_couche" 1 \
      "   le voisin : la pression du bouton droit sort de la saisie (N5)"

echo "A12 : un clic droit prend le bloc et ouvre son menu, aux commandes des menus ; le vide, la page ; la saisie, le texte."
