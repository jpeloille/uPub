#!/usr/bin/env bash
#
# Recette du poste A4 — ordre de superposition : raccourcis et glisser-déposer.
#
# Deux temps, et ils ne se recouvrent pas.
#
# 1. LE TABLEAU DES RACCOURCIS, lu dans les sources. C'est ce qu'un script fait mieux
#    qu'un test : il confronte des listes écrites à plusieurs endroits par plusieurs mains —
#
#      · les gestes déclarés dans « Window.KeyBindings » ;
#      · les touches que « MainWindow.OnKeyDown » intercepte ;
#      · la table des raccourcis lus sur le caractère, « RaccourcisParCaractere »
#        (poste N10, MainWindowViewModel.Raccourcis.cs) ;
#      · les gestes que les menus affichent ;
#      · le tableau des raccourcis du README, qui est ce que l'utilisateur lit.
#
#    Un raccourci en double se tait : Avalonia joue le premier et laisse l'autre
#    inerte, sans erreur ni avertissement de compilation. Un raccourci absent du README
#    existe sans que personne ne le sache. Un geste affiché à un menu sans liaison ment.
#    Aucun n'échoue à l'exécution, et c'est pourquoi ils se vérifient ici. Étendue par le
#    poste N10, la confrontation tient aussi :
#
#      a. chaque geste déclaré une fois, en KeyBinding ou dans la table, jamais dans les deux ;
#      b. chaque touche traitée dans OnKeyDown au README, comme les liaisons et la table ;
#      c. chaque liaison qui a une entrée de menu l'y affiche, et chaque geste affiché à un
#         menu est une vraie liaison ;
#      d. les touches réservées de la table française libres (règle 12 de la phase N) ;
#      e. Ctrl+E qui n'exporte plus : il centre, et l'export est sur Ctrl+Maj+P ;
#      f. Ctrl+Maj+F et Ctrl+Maj+B toujours déclarés, Premier plan et Arrière-plan ;
#      g. sous Contrôle, OnKeyDown n'intercepte que N, O, S et les touches du presse-papiers —
#         Ctrl+Inser compris, avec Maj+Inser et Maj+Suppr sous Maj seul (N10, 28 octobre 2026) —,
#         et aucune n'est aussi une liaison.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre. Un
#    script de shell ne synthétise ni pointeur ni frappe : ce qui demande la fenêtre
#    est éprouvé là-bas, et la recette l'appelle par son nom plutôt que de le déclarer
#    fait. Le compte des tests joués est vérifié — un filtre qui ne désigne plus rien
#    sort en 0, et serait la seule façon pour cette recette de se dire verte sans avoir
#    rien vu.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile, plutôt que de lancer les tests avec « --no-build ».
#
# C'est le piège que R3 a relevé et qu'un « --no-build » réintroduirait : un binaire
# périmé survit à une modification des sources, et la recette se déclare verte sur du
# code qu'elle n'a pas éprouvé. Elle est faite pour être rejouée seule, y compris par
# quelqu'un qui vient de saboter un fichier ; elle doit donc compiler elle-même, comme
# « Scripts/controle.sh » le fait depuis le poste P2.
if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

tests="MyPublisher.Tests.Interface/bin/Debug/net10.0/MyPublisher.Tests.Interface.dll"
if [ ! -f "$tests" ]; then
    echo "ÉCHEC : tests d'interface introuvables après compilation ($tests)." >&2
    echo "Le cadriciel cible du .csproj a-t-il changé ?" >&2
    exit 1
fi

# --------------------------------------------------- 1. le tableau des raccourcis

echo "  Raccourcis déclarés, interceptés et documentés :"
python3 - MyPublisher/Views/MainWindow.axaml MyPublisher/Views/MainWindow.axaml.cs README.md \
          MyPublisher/ViewModels/MainWindowViewModel.Raccourcis.cs <<'PY'
import re, sys
import xml.etree.ElementTree as ET

axaml, code, readme, raccourcis = (open(chemin, encoding="utf-8").read() for chemin in sys.argv[1:5])
echecs = []
def refuser(message):
    echecs.append(message)

A = "{https://github.com/avaloniaui}"
fenetre = ET.fromstring(axaml)

bloc = re.search(r"<Window\.KeyBindings>(.*?)</Window\.KeyBindings>", axaml, re.S)
if bloc is None:
    sys.exit("    aucun bloc « Window.KeyBindings » dans MainWindow.axaml")

# Les liaisons : le geste, la commande, et son paramètre — les alignements partagent une commande.
liaisons = [(e.get("Gesture"), e.get("Command"), e.get("CommandParameter")) for e in fenetre.iter(A + "KeyBinding")]
gestes = [g for g, _, _ in liaisons]

# ------------------------------------------------- la table des raccourcis par caractère (N10)
table = re.search(r"RaccourcisParCaractere => .*?\{(.*?)\};", raccourcis, re.S)
if table is None:
    sys.exit("    la table « RaccourcisParCaractere » est introuvable (MainWindowViewModel.Raccourcis.cs)")
caracteres = re.findall(r'\["(.)"\] = (\w+)Command', table.group(1))
if len(caracteres) < 4:
    refuser(f"la table des raccourcis par caractère n'a que {len(caracteres)} entrées, quatre attendues")

# ------------------------------------------------------- les touches qu'OnKeyDown traite
corps = re.search(r"protected override void OnKeyDown\(KeyEventArgs e\)\s*\n(\s*)\{\n(.*?)\n\1\}", code, re.S)
if corps is None:
    sys.exit("    OnKeyDown est introuvable dans MainWindow.axaml.cs")
# Chaque « if » qui lit les modificateurs ouvre une section : Contrôle (avec ou sans d'autres
# touches), aucun modificateur, puis, après « var fine », les touches qui se lisent sans eux.
sections, mode = {"ctrl+": set(), "ctrl": set(), "maj": set(), "aucun": set(), "libre": set()}, None
for ligne in corps.group(2).splitlines():
    if re.search(r"if \(e\.KeyModifiers\.HasFlag\(KeyModifiers\.Control\)\)", ligne):
        mode = "ctrl+"
    elif re.search(r"if \(e\.KeyModifiers == KeyModifiers\.Control\)", ligne):
        mode = "ctrl"
    elif re.search(r"if \(e\.KeyModifiers == KeyModifiers\.Shift\)", ligne):
        mode = "maj"
    elif re.search(r"if \(e\.KeyModifiers == KeyModifiers\.None\)", ligne):
        mode = "aucun"
    elif "var fine" in ligne:
        mode = "libre"
    if mode and re.search(r"\bcase\b", ligne):
        sections[mode].update(re.findall(r"Key\.(\w+)", ligne))
interceptees_ctrl = sections["ctrl+"] | sections["ctrl"]
traitees = ({f"Ctrl+{k}" for k in interceptees_ctrl} | {f"Shift+{k}" for k in sections["maj"]}
            | set(sections["aucun"]) | set(sections["libre"]))

# Ce que le README écrit pour une touche : « Maj » pour Shift, et les noms français.
noms = {"Shift": "Maj", "Back": "Retour arrière", "Delete": "Suppr", "Insert": "Inser", "Escape": "Échap", "Space": "Espace",
        "Left": "Flèches", "Right": "Flèches", "Up": "Flèches", "Down": "Flèches",
        # Comme les menus et les bulles les écrivent (GestesEnFrancais, N13) ; poste N18.
        "PageDown": "Pg.Suiv", "PageUp": "Pg.Préc"}
def au_readme(geste):
    return "+".join(noms.get(partie, partie) for partie in geste.split("+"))

tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
if tableau is None:
    sys.exit("    le README n'a plus de section « ## Raccourcis »")
tableau = tableau.group(1)

# ------------------------------- a. chaque geste une fois, en KeyBinding ou dans la table
# Un doublon ne se signale nulle part : Avalonia retient le premier, et le second ne
# fait jamais rien. C'est le mode de panne que ce poste pouvait introduire.
doublons = sorted({g for g in gestes if gestes.count(g) > 1})
if doublons:
    refuser("gestes déclarés deux fois : " + ", ".join(doublons))
lettres = [c for c, _ in caracteres]
if len(set(lettres)) != len(lettres):
    refuser("un caractère est deux fois dans la table des raccourcis par caractère")
# La touche qu'un QWERTY donne à chaque caractère : la déclarer aussi en KeyBinding ferait
# jouer deux fois le même geste — la liaison d'abord, la table ensuite.
oem = {"]": "OemCloseBrackets", "[": "OemOpenBrackets", "<": "OemComma", ">": "OemPeriod"}
for c, commande in caracteres:
    for geste in (f"Ctrl+{oem.get(c, c)}", f"Ctrl+Shift+{oem.get(c, c)}"):
        if geste in gestes:
            refuser(f"« Ctrl+{c} » est dans la table et en KeyBinding ({geste})")
    if f"{commande}Command" in bloc.group(1):
        refuser(f"la commande de « Ctrl+{c} », {commande}Command, est aussi liée en KeyBinding")

# ----------------------------------------- b. le README dit les liaisons, OnKeyDown et la table
# Le README doit dire ce que l'application écoute, et le dire dans SON TABLEAU : c'est
# là qu'on va chercher un raccourci, et une mention au fil du texte ne s'y substitue
# pas. La première écriture de cette recette cherchait dans le README entier, et elle
# ne tombait pas quand on retirait la ligne du tableau — les deux raccourcis étant
# aussi cités dans la prose. Un contrôle qu'on n'a pas vu échouer ne prouve rien. Les
# touches de zoom sont déclarées en double par nature — pavé numérique et rangée du haut —
# et le tableau les résume d'une ligne ; on ne les confronte pas.
zoom = {"Ctrl+OemPlus", "Ctrl+Add", "Ctrl+OemMinus", "Ctrl+Subtract", "Ctrl+D0", "Ctrl+NumPad0"}
confrontes = [g for g in gestes if g not in zoom]
manquants = [g for g in confrontes if au_readme(g) not in tableau]
manquants += [f"OnKeyDown : {g}" for g in sorted(traitees)
              if not (f"`{au_readme(g)}`" in tableau or (au_readme(g) == "Flèches" and "Flèches" in tableau))]
manquants += [f"table : Ctrl+{c}" for c in lettres if f"`Ctrl+{c}`" not in tableau and f"`Ctrl+Maj+{c}`" not in tableau]
if manquants:
    refuser("absents du tableau du README : " + ", ".join(manquants))

# ----------------------------------- c. les menus affichent les liaisons, et rien d'autre
# Les gestes que la fenêtre ne voit pas : le bureau ferme la fenêtre à Alt+F4, qu'elle reçoit
# en fermeture (poste N11).
du_bureau = {"Alt+F4"}
vraies = set(gestes) | traitees | {f"Ctrl+{c}" for c in lettres} | du_bureau
menus = [(e.get("Header"), e.get("InputGesture"), e.get("Command"), e.get("CommandParameter"))
         for e in fenetre.iter(A + "MenuItem")]
for entete, geste, commande, parametre in menus:
    if geste and geste not in vraies:
        refuser(f"le menu « {entete} » affiche « {geste} », que rien ne lie")
    if commande:
        siennes = [g for g, c, p in liaisons if c == commande and p == parametre]
        if siennes and geste not in siennes:
            refuser(f"le menu « {entete} » n'affiche pas sa touche ({' ou '.join(siennes)})")

# ---------------------------------------------- d. les touches réservées restent libres
# Leur fonction n'existe pas encore dans MyPublisher : elles attendent le poste qui la sert
# (règle 12 ; fiche 24). Ctrl+M, la page maître ; Ctrl+K, le lien hypertexte ; Ctrl+P, Imprimer
# (B3) ; Ctrl+Maj+E, la bibliothèque (C7) ; Ctrl+Maj+H, la coupure de mots. Ctrl+B, Atteindre la
# page, a quitté la liste avec le poste qui le sert (N18) : il est lié, et le README le dit.
reservees = ["Ctrl+M", "Ctrl+K", "Ctrl+P", "Ctrl+Shift+E", "Ctrl+Shift+H"]
for geste in reservees:
    lettre = geste.split("+")[-1]
    prise = (geste in gestes or any(g == geste for _, g, _, _ in menus) or lettre in sections["ctrl+"]
             or (geste.count("+") == 1 and lettre in sections["ctrl"]))
    if prise:
        refuser(f"« {geste} », réservé par la table française, est pris")

# ------------------------------------------------------------ e. Ctrl+E n'exporte plus
if ("Ctrl+E", "{Binding AlignerLeTexteCommand}", "{x:Static models:TextAlign.Center}") not in liaisons:
    refuser("« Ctrl+E » ne centre pas le texte")
if "E" in interceptees_ctrl:
    refuser("OnKeyDown prend encore E sous Contrôle")
if ("Ctrl+Shift+P", "{Binding ExporterEnPdfCommand}", None) not in liaisons:
    refuser("l'export n'est pas sur « Ctrl+Shift+P »")
if [g for g, c, _ in liaisons if c == "{Binding ExporterEnPdfCommand}"] != ["Ctrl+Shift+P"]:
    refuser("l'export a une autre touche que « Ctrl+Shift+P »")

# --------------------------------------------- f. Premier plan et Arrière-plan, exigés
for attendu in ("Ctrl+Shift+F", "Ctrl+Shift+B"):
    if attendu not in gestes:
        refuser(f"« {attendu} » n'est pas déclaré")

# --------------------------------- g. Ctrl+lettre : N, O, S et le presse-papiers, seulement
# Les touches que la fenêtre intercepte sous Contrôle n'arrivent pas à une liaison du même
# nom : un « Ctrl+S » déclaré comme KeyBinding passerait avant elles, ou jamais.
if interceptees_ctrl != {"N", "O", "S", "X", "C", "V", "Insert"}:
    refuser("OnKeyDown intercepte sous Contrôle " + ", ".join(sorted(interceptees_ctrl))
            + " — N, O, S, X, C, V et Insert attendues")
if sections["maj"] != {"Insert", "Delete"}:
    refuser("OnKeyDown intercepte sous Maj seul " + ", ".join(sorted(sections["maj"])) + " — Insert et Delete attendues")
for geste in gestes:
    if geste in ("Shift+Insert", "Shift+Delete", "Ctrl+Insert"):
        refuser(f"« {geste} » est déclaré en KeyBinding : il passerait avant le champ, qui le tient lui-même")
for geste in gestes:
    parties = geste.split("+")
    if parties[0] == "Ctrl" and len(parties) == 2 and parties[1] in interceptees_ctrl:
        refuser(f"« {geste} » est intercepté par OnKeyDown avant sa commande")

for message in echecs:
    print("    " + message, file=sys.stderr)
print(f"    {len(gestes)} gestes déclarés, {len(set(gestes))} distincts, {len(lettres)} lus sur le caractère, "
      f"{len(traitees)} touches d'OnKeyDown, {sum(1 for m in menus if m[1])} gestes de menu ; "
      f"{len(confrontes)} liaisons confrontées au tableau du README")
sys.exit(1 if echecs else 0)
PY

# -------------------------------------------------------------------- 2. les gestes

attendus=5
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=A4" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les gestes de A4 ne passent pas." >&2
    exit 1
fi

joues="$(grep -o 'Passed: *[0-9]*' "$travail/tests.txt" | head -1 | grep -o '[0-9]*$' || true)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=A4 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi

echo "  Gestes joués sur la vraie fenêtre : $joues tests (clavier, glissé, clic simple)"
echo "A4 : raccourcis déclarés sans doublon et documentés, superposition au clavier et à la souris."
